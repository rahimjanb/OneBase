using System.Text;
using System.Text.Json;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Security;

namespace OneBase.AI.Orchestration;

/// <summary>
/// Главный AI-консультант: вопрос → маршрутизация → AI-сотрудники параллельно → единый управленческий ответ.
/// AI-сотрудники без доступа у пользователя не привлекаются; консультант прямо говорит, каких данных не видит.
/// </summary>
public sealed class OrchestratedConsultantEngine(
    AiAgentStore agents,
    IUserPermissions permissions,
    AiRouter router,
    AgentOrchestrator orchestrator,
    IAiGateway gateway,
    TimeProvider clock) : IConsultantEngine
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, Func<ConsultantProgress, Task> progress, CancellationToken ct)
    {
        var all = await agents.ListAsync(ct);
        var consultant = all.FirstOrDefault(a => a.IsConsultant);
        if (consultant is { Enabled: false })
        {
            throw new LlmNotConfiguredException("Главный консультант выключен администратором в «Настройки → AI → Агенты».");
        }

        var own = await permissions.GetAsync(turn.UserId, ct);
        var departments = all.Where(a => !a.IsConsultant && a.Enabled).ToList();
        var available = departments.Where(a => a.RequiredPermission is null || own.Contains(a.RequiredPermission)).ToList();
        var forbidden = departments.Except(available).ToList();
        var context = new AiCallContext(turn.UserId, AgentDefaults.Consultant, turn.ConversationId, "consultant");

        await progress(new ConsultantProgress("routing", null, null, ProgressStatus.Running));
        var routing = await router.RouteAsync(turn.Question, turn.History, available, context, ct);
        await progress(new ConsultantProgress("routing", null, null, ProgressStatus.Done,
            routing.Agents.Count == 0 ? "без AI-сотрудников" : string.Join(", ", routing.Agents.Select(c => available.First(a => a.Code == c).Name))));

        var chosen = available.Where(a => routing.Agents.Contains(a.Code)).ToList();
        var (runs, agentUsage) = chosen.Count == 0
            ? ([], AiTokenUsage.None)
            : await orchestrator.RunAsync(chosen, routing.Tasks, turn.Question, HistoryContext(turn.History), turn.UserId, turn.ConversationId, progress, ct);

        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Running));
        var results = runs.Select(r => r.Result).ToList();
        List<LlmMessage> messages =
        [
            new(LlmRole.System, ComposePrompt(consultant, results, forbidden)),
            .. turn.History,
            new(LlmRole.User, turn.Question),
        ];
        var answer = await gateway.CompleteAsync(messages, [], new AiCallOptions
        {
            Model = consultant?.Model,
            Temperature = consultant?.Temperature,
            MaxOutputTokens = consultant?.MaxOutputTokens,
            Context = context with { Purpose = "consultant.answer" },
        }, ct);
        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Done));

        var sources = new List<DataSource>();
        foreach (var s in results.SelectMany(r => r.DataSources))
        {
            if (!sources.Any(x => x.Title == s.Title && x.Period == s.Period))
            {
                sources.Add(s);
            }
        }

        return new ConsultantAnswer(
            answer.Text?.Trim() ?? string.Empty,
            new ConsultantDetails(results, sources, answer.Model.ToString(), answer.UsedFallback, routing.Reason.Length > 0 ? routing.Reason : null),
            routing.Usage.Add(agentUsage).Add(answer.Usage));
    }

    /// <summary>Контекст для сотрудников: о чём шла речь в чате (для уточняющих вопросов).</summary>
    private static string? HistoryContext(IReadOnlyList<LlmMessage> history)
    {
        if (history.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder("Ранее в чате:\n");
        foreach (var m in history.TakeLast(4))
        {
            var text = m.Content.Length > 400 ? m.Content[..400] + "…" : m.Content;
            sb.AppendLine($"{(m.Role == LlmRole.User ? "Вопрос" : "Ответ")}: {text}");
        }

        return sb.ToString();
    }

    private string ComposePrompt(AgentConfig? consultant, IReadOnlyList<AgentResult> results, IReadOnlyList<AgentConfig> forbidden)
    {
        var today = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5));
        var sb = new StringBuilder();
        sb.AppendLine(consultant?.Prompt ?? AgentDefaults.Director.Prompt);
        sb.AppendLine();
        sb.AppendLine($"Сегодня {today:dd.MM.yyyy} (Ташкент). Суммы — в сумах, вес — в кг.");
        sb.AppendLine();
        sb.AppendLine(ConsultantPrompts.QualityRules);
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("К этому вопросу AI-сотрудники не привлекались: данных OneBase в этом ответе нет. Отвечай по истории чата и общим знаниям;");
            sb.AppendLine("если вопрос о показателях компании — скажи, что для ответа нужно уточнить вопрос (период, отдел), и не называй цифр компании.");
        }
        else
        {
            sb.AppendLine("Результаты AI-сотрудников (единственный источник данных компании для этого ответа):");
            sb.AppendLine(JsonSerializer.Serialize(results.Select(r => new
            {
                r.Agent,
                r.AgentName,
                r.Status,
                r.Summary,
                r.Findings,
                r.Metrics,
                r.Problems,
                r.Recommendations,
                DataSources = r.DataSources.Select(s => s.Period is null ? s.Title : $"{s.Title} ({s.Period})"),
                r.Error,
            }), Json));
            sb.AppendLine();
            sb.AppendLine("""
                Сформируй единый ответ:
                1. Главное — 1–3 предложения с ключевыми цифрами.
                2. По отделам — только привлечённые; факты с цифрами и периодом, со ссылкой на источник.
                3. Взаимосвязи и основные причины (анализ), если данных достаточно.
                4. Рекомендации — приоритетные действия, основанные на фактах; отметь, что это рекомендации.
                Если сотрудник вернул no_data или ошибку — прямо скажи, каких данных нет. Не пересказывай JSON дословно.
                """);
        }

        if (forbidden.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"У пользователя нет доступа к данным: {string.Join(", ", forbidden.Select(f => $"{f.Name} ({f.Role})"))}. " +
                "Не раскрывай и не оценивай эти данные; если вопрос о них — скажи, что у пользователя нет доступа, и предложи обратиться к администратору.");
        }

        return sb.ToString();
    }
}

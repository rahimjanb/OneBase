using System.Text;
using Microsoft.Extensions.Logging;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Memory;
using OneBase.AI.Monitoring;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.AI.Reports;
using OneBase.AI.Security;
using OneBase.AI.Tools;

namespace OneBase.AI.Analytik;

/// <summary>
/// AI-аналитик раздела «Аналитик» (OneBase Intelligence). Тот же конвейер, что у консультанта, на общих частях: маршрутизатор (AiRouter) выбирает
/// аналитиков из команды «Аналитик», AgentOrchestrator запускает их параллельно (AgentRunner: инструменты отделов и OneBase Intelligence, права в
/// ToolExecutor, проверка чисел итога), Chief Analyst сводит итоги в одно заключение (GroundedComposer — числа сверяются с данными), модели — по
/// маршрутизации моделей OneBase (простое — дешёвой моделью, сложное — сильной). Математика — в инструментах (код), не в модели.
/// </summary>
public sealed partial class AnalytikEngine(
    AiAgentStore agents,
    IUserPermissions permissions,
    AiRouter router,
    AgentOrchestrator orchestrator,
    IAiGateway gateway,
    AiMemoryStore memory,
    ReportService reports,
    ToolExecutor executor,
    IToolRegistry tools,
    TimeProvider clock,
    IAiSettingsSource aiSettings,
    ILogger<AnalytikEngine> logger) : IConsultantEngine
{
    /// <summary>Аналитиков на один вопрос без «по всей компании» — не больше: каждый — отдельный цикл модели.</summary>
    private const int MaxAgents = 5;

    internal const string Format = """
        Формат ответа раздела «Аналитик» (он важнее правил оформления выше):
        - Простой вопрос (одна цифра или один факт) — коротко: цифра, период и дата данных, 1–3 предложения.
        - Сложный вопрос (анализ, сравнение, «почему», «что делать», несколько отделов) — отчёт в Markdown; разделы по порядку, пустые пропускай:
          1. **Краткий вывод** — 2–3 предложения с главными цифрами.
          2. **Основные показатели** — таблица: показатель, значение, прошлый период, изменение.
          3. **Подробный анализ**.
          4. **Сравнение периодов**.
          5. **Найденные проблемы** — с масштабом.
          6. **Причины и взаимосвязи** — подтверждённое данными отдельно; предположения помечай «гипотеза».
          7. **Прогноз** — с диапазоном; это не гарантия.
          8. **Рекомендации** — конкретные действия с приоритетом и отделом.
          9. **Источники данных** — разделы OneBase и периоды словами (без названий инструментов, SQL и технических подробностей).
          10. **Актуальность** — по какую дату и время данные.
        - В тексте различай: факт (из данных), расчёт (по фактам), прогноз, предположение, рекомендация.
        - Данные OneBase ты не меняешь: закупки, заявки, остатки и планы оформляет человек. Можешь подготовить текст черновика заявки.
        """;

    public async Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, Func<ConsultantProgress, Task> progress, CancellationToken ct)
    {
        var startedAt = clock.GetUtcNow();
        var timings = AiTurnTimings.Current;
        var contextStage = timings?.Stage("context");
        var asksReport = ReportIntent.Requested(turn.Question) && (turn.Channel?.AllowReports ?? true);
        var all = (await agents.ListAsync(ct)).Where(a => AnalytikAgents.IsAnalyst(a.Code)).ToList();
        var chief = all.FirstOrDefault(a => a.Code == AnalytikAgents.Chief);
        if (chief is { Enabled: false })
        {
            throw new LlmNotConfiguredException("Chief Analyst выключен администратором в «Настройки → AI → Агенты».");
        }

        var own = await permissions.GetAsync(turn.UserId, ct);
        var team = all.Where(a => a.Code != AnalytikAgents.Chief && a.Enabled && (a.RequiredPermission is null || own.Contains(a.RequiredPermission))).ToList();
        var sources = await executor.DataSourcesAsync(team.Select(a => a.Code).ToList(), turn.UserId, ct);
        var available = team.Where(a => sources.TryGetValue(a.Code, out var s) && s.Count > 0).ToList();
        var forbidden = all.Where(a => a.Code != AnalytikAgents.Chief && a.Enabled).Except(available).ToList();
        var context = new AiCallContext(turn.UserId, AnalytikAgents.Chief, turn.ConversationId, turn.Channel?.Name ?? "analytik");

        var names = all.ToDictionary(a => a.Code, a => a.Name);
        var remembered = await memory.ConversationAsync(turn.UserId, turn.ConversationId, available.Select(a => a.Code).ToList(), 8, ct);
        var rememberedText = remembered.Count == 0 ? null : AiMemoryStore.Format(remembered, names);
        contextStage?.Dispose();

        await progress(new ConsultantProgress("routing", null, null, ProgressStatus.Running));
        RoutingDecision routing;
        using (timings?.Stage("routing"))
        {
            routing = available.Count == 0
                ? new RoutingDecision([], new Dictionary<string, string>(), "Нет аналитиков с доступными данными.", false, AiTokenUsage.None)
                : Enforce(await router.RouteAsync(turn.Question, turn.History, available, context, ct, rememberedText), turn, available);
        }

        await progress(new ConsultantProgress("routing", null, null, ProgressStatus.Done,
            routing.Agents.Count == 0 ? "без аналитиков" : string.Join(", ", routing.Agents.Select(c => names.GetValueOrDefault(c, c)))));

        var chosen = available.Where(a => routing.Agents.Contains(a.Code)).ToList();
        var contexts = new Dictionary<string, string?>();
        var memoryUsed = remembered.Count;
        foreach (var a in chosen)
        {
            var earlier = await memory.AgentAsync(turn.UserId, a.Code, turn.ConversationId, 3, ct);
            memoryUsed += earlier.Count;
            contexts[a.Code] = AgentContext(turn.History, remembered.Where(m => m.AgentCode == a.Code).ToList(), earlier);
        }

        var aiConfig = await aiSettings.GetSettingsAsync(ct);
        var modelRouting = aiConfig.ModelRouting;
        var reviewPlan = modelRouting.On && modelRouting.Review
            ? new ReviewPlan(modelRouting.ReviewMaxAttempts, () => modelRouting.QuestionBudgetUsd is not { } limit || timings is null || timings.CostUsd < limit * 0.75m)
            : null;

        var agentsStage = timings?.Stage("agents");
        var (runs, agentUsage) = chosen.Count == 0
            ? ([], AiTokenUsage.None)
            : await orchestrator.RunAsync(chosen, routing.Tasks, turn.Question, contexts, turn.UserId, turn.ConversationId, progress, ct, asksReport,
                turn.Channel?.AgentModel, reviewPlan);
        agentsStage?.Dispose();
        var created = asksReport ? await reports.CreatedAsync(turn.UserId, turn.ConversationId, startedAt, ct) : [];

        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Running));
        var results = runs.Select(r => r.Result).ToList();
        var evidence = Evidence(runs, turn, rememberedText);
        List<LlmMessage> messages =
        [
            new(LlmRole.System, OrchestratedConsultantEngine.ComposeRules(chief) + "\n" + Format),
            new(LlmRole.System, ComposePrompt(results, forbidden, rememberedText, asksReport ? created : null, turn.Channel, AnswerLanguages.Of(turn.Question))),
            .. turn.History,
            new(LlmRole.User, turn.Question),
        ];
        var toolErrors = runs.Sum(r => r.ToolErrors?.Count ?? 0);
        var complexity = AnswerComplexityRules.Assess(turn.Question, turn.History, runs, routing, false, toolErrors);
        var finalChoice = AiModelRouting.Final(aiConfig, turn.Channel?.Model, chief?.Model, complexity);
        if (reviewPlan is not null)
        {
            reviewPlan.Escalation = AiModelRouting.Escalation(modelRouting, finalChoice.Model ?? aiConfig.Primary);
        }

        GroundedAnswer answer;
        using (timings?.Stage("compose"))
        {
            answer = await new GroundedComposer(gateway).ComposeAsync(messages, new AiCallOptions
            {
                Model = finalChoice.Model,
                ModelReason = finalChoice.Reason,
                Temperature = chief?.Temperature,
                MaxOutputTokens = turn.Channel?.MaxOutputTokens ?? chief?.MaxOutputTokens ?? Math.Max(aiConfig.MaxOutputTokens, AgentRunner.AgentOutputTokens),
                CacheConversation = false,
                Context = context with { Purpose = "analytik.answer" },
            }, evidence, turn.Question, ct, null, turn.Draft, reviewPlan);
        }

        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Done,
            answer.Grounding.Checked == 0 ? null : $"чисел сверено с данными: {answer.Grounding.Checked}"));

        try
        {
            await memory.SaveAsync(turn.UserId, turn.ConversationId, routing.Tasks, turn.Question, results, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: не удалось сохранить память чата {ConversationId}", turn.ConversationId);
        }

        var dataSources = new List<DataSource>();
        foreach (var s in results.SelectMany(r => r.DataSources))
        {
            if (!dataSources.Any(x => x.Title == s.Title && x.Period == s.Period))
            {
                dataSources.Add(s);
            }
        }

        return new ConsultantAnswer(answer.Text,
            new ConsultantDetails(results, dataSources, answer.Model.ToString(), answer.UsedFallback, routing.Reason.Length > 0 ? routing.Reason : null, memoryUsed,
                created.Count > 0 ? created : null, answer.Grounding, null),
            routing.Usage.Add(agentUsage).Add(answer.Usage));
    }

    /// <summary>
    /// Выбор аналитиков после маршрутизатора: только доступные; разговор — без них; маршрутизатор не ответил или никого не выбрал для вопроса о
    /// данных — по словам вопроса (AnalytikAgents.Keywords), иначе BI-аналитик; «по всей компании» — все доступные; иначе не больше MaxAgents.
    /// </summary>
    internal static RoutingDecision Enforce(RoutingDecision routing, ConsultantTurn turn, IReadOnlyList<AgentConfig> available)
    {
        var codes = available.Select(a => a.Code).ToHashSet(StringComparer.Ordinal);
        var previous = turn.History.LastOrDefault(m => m.Role == LlmRole.User)?.Content;
        if (RouteIntent.IsSmallTalk(turn.Question) && !(previous is not null && RouteIntent.IsFollowUp(turn.Question)))
        {
            return routing with { Agents = [], Tasks = new Dictionary<string, string>() };
        }

        if (RouteIntent.IsCompanyWide(turn.Question) || CompanyWide().IsMatch(RouteIntent.Normalize(turn.Question)))
        {
            return routing with { Agents = available.Select(a => a.Code).ToList(), Reason = "Вопрос о компании в целом — все доступные аналитики." };
        }

        var agents = routing.ByKeywords ? [] : routing.Agents.Where(codes.Contains).Distinct().ToList();
        if (agents.Count == 0)
        {
            var text = RouteIntent.Normalize(turn.Question + " " + (RouteIntent.IsFollowUp(turn.Question) ? previous : ""));
            agents = AnalytikAgents.Keywords.Where(k => codes.Contains(k.Key) && k.Value.Any(text.Contains)).Select(k => k.Key).ToList();
            if (agents.Count == 0 && codes.Contains(AnalytikAgents.Bi))
            {
                agents = [AnalytikAgents.Bi];
            }

            routing = routing with { Reason = routing.Reason.Length > 0 ? routing.Reason + " Выбор уточнён по словам вопроса." : "Выбор по словам вопроса." };
        }

        var tasks = routing.Tasks.Where(t => agents.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value);
        return routing with { Agents = agents.Take(MaxAgents).ToList(), Tasks = tasks };
    }

    /// <summary>Вопрос обо всей компании в формулировках «Аналитика» (сверх RouteIntent): вся компания, комплексный анализ, все проблемы и риски.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"вс(ю|ей|ея)\s+компани|весь\s+бизнес|комплексн\p{L}*\s+анализ|всех\s+отдел|все\s+проблем|все\s+риск|вмешательств\p{L}*\s+директор|butun\s+kompaniya|бутун\s+компания")]
    private static partial System.Text.RegularExpressions.Regex CompanyWide();

    private static EvidenceSet Evidence(IReadOnlyList<AgentRun> runs, ConsultantTurn turn, string? remembered)
    {
        var evidence = new EvidenceSet();
        foreach (var run in runs)
        {
            if (run.Evidence is { } e)
            {
                evidence.AddAll(e);
            }
        }

        evidence.AddDerived(runs.SelectMany(r => NumberClaims.Find(AgentRunner.ResultText(r.Result))).Where(evidence.Supports).Select(c => c.Value).ToList());
        evidence.AddText(remembered);
        if (runs.Count == 0)
        {
            foreach (var m in turn.History.Where(m => m.Role == LlmRole.Assistant))
            {
                evidence.AddText(m.Content);
            }
        }

        return evidence;
    }

    private static string? AgentContext(IReadOnlyList<LlmMessage> history, IReadOnlyList<MemoryNote> inChat, IReadOnlyList<MemoryNote> earlier)
    {
        var sb = new StringBuilder();
        if (history.Count > 0)
        {
            sb.AppendLine("Ранее в чате:");
            foreach (var m in history.TakeLast(4))
            {
                sb.AppendLine($"{(m.Role == LlmRole.User ? "Вопрос" : "Ответ")}: {(m.Content.Length > 400 ? m.Content[..400] + "…" : m.Content)}");
            }
        }

        if (inChat.Count > 0)
        {
            sb.AppendLine("Ты уже анализировал в этом чате:");
            sb.Append(AiMemoryStore.Format(inChat));
        }

        if (earlier.Count > 0)
        {
            sb.AppendLine("Твои выводы из других чатов (могли устареть — цифры бери только из инструментов):");
            sb.Append(AiMemoryStore.Format(earlier));
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    private string ComposePrompt(IReadOnlyList<AgentResult> results, IReadOnlyList<AgentConfig> forbidden, string? remembered, IReadOnlyList<ReportRef>? reportsCreated,
        ConsultantChannel? channel, AnswerLanguage language)
    {
        var now = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5));
        var sb = new StringBuilder();
        sb.AppendLine($"Сейчас {now:dd.MM.yyyy HH:mm} (Ташкент).");
        sb.AppendLine($"Язык вопроса — {AnswerLanguages.Label(language)}: отвечай на нём.");
        if (channel?.Instructions is { Length: > 0 } instructions)
        {
            sb.AppendLine(instructions);
        }

        sb.AppendLine();
        if (results.Count == 0)
        {
            sb.AppendLine("Аналитики к вопросу не привлекались: данные OneBase не запрашивались. Ответь по истории чата; если вопрос о показателях компании —");
            sb.AppendLine("попроси уточнить (период, отдел) и не называй цифр компании. Не утверждай, что данных нет: поиск не выполнялся.");
        }
        else
        {
            sb.AppendLine("Результаты аналитиков (единственный источник данных компании для этого ответа; состояние каждого — в поле outcome):");
            sb.AppendLine(OrchestratedConsultantEngine.ComposeData(results, name => tools.Find(name)?.Title, language));
        }

        if (remembered is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Что аналитики выяснили раньше в этом чате (для сравнения и уточняющих вопросов):");
            sb.AppendLine(remembered);
        }

        if (reportsCreated is not null)
        {
            sb.AppendLine();
            sb.AppendLine(reportsCreated.Count > 0
                ? $"Сформированы отчёты Excel: {string.Join("; ", reportsCreated.Select(r => $"«{r.Title}» (листов {r.SheetCount}, строк {r.RowCount})"))} — они под ответом. Коротко скажи, что в них."
                : "Пользователь просил отчёт Excel, но он не сформирован — скажи об этом и почему (по outcome аналитиков).");
        }

        if (forbidden.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Аналитики без доступных пользователю данных: {string.Join(", ", forbidden.Select(f => f.Name))}. Если вопрос о их области — скажи, что у пользователя " +
                          "нет доступа к этим данным, и не оценивай их.");
        }

        return sb.ToString();
    }
}

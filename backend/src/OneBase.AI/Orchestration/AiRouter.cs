using System.Text;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;

namespace OneBase.AI.Orchestration;

/// <summary>Решение маршрутизатора: какие AI-сотрудники и с какими задачами; пустой список — консультант отвечает сам.</summary>
public sealed record RoutingDecision(IReadOnlyList<string> Agents, IReadOnlyDictionary<string, string> Tasks, string Reason, bool ByKeywords, AiTokenUsage Usage);

/// <summary>
/// AI Router: по вопросу (и истории чата) определяет нужных AI-сотрудников. Если модель не ответила разборчиво —
/// запасной выбор по ключевым словам; если ни одно слово не подошло — все доступные сотрудники.
/// </summary>
public sealed class AiRouter(IAiGateway gateway, IAiSettingsSource settings)
{
    /// <summary>Основы слов, по которым вопрос относится к отделу (запасной маршрут).</summary>
    internal static readonly IReadOnlyDictionary<string, string[]> Keywords = new Dictionary<string, string[]>
    {
        ["sales"] = ["продаж", "регион", "филиал", "план", "агент", "тп", "клиент", "акб", "товар", "категор", "ассортимент", "визит", "страйк", "выручк", "менеджер"],
        ["finance"] = ["финанс", "прибыл", "выручк", "расход", "оплат", "деньг", "бюджет", "долг", "задолж", "затрат", "доход"],
        ["marketing"] = ["маркет", "реклам", "лид", "канал", "кампан", "бренд", "продвиж"],
        ["hr"] = ["сотрудник", "персонал", "кадр", "ваканс", "нагрузк", "текучест", "hr", "найм", "кандидат", "эффективност"],
        ["production"] = ["производ", "выпуск", "цех", "мощност", "простой", "себестоим", "завод"],
        ["supply"] = ["остат", "склад", "закуп", "поставщ", "дефицит", "снабж", "запас", "заканч"],
    };

    public async Task<RoutingDecision> RouteAsync(
        string question, IReadOnlyList<LlmMessage> history, IReadOnlyList<AgentConfig> available, AiCallContext context, CancellationToken ct,
        string? memory = null)
    {
        if (available.Count == 0)
        {
            return new RoutingDecision([], new Dictionary<string, string>(), "Нет доступных AI-сотрудников.", false, AiTokenUsage.None);
        }

        var s = await settings.GetSettingsAsync(ct);
        var result = await gateway.CompleteAsync(
            [new LlmMessage(LlmRole.System, Prompt(available)), new LlmMessage(LlmRole.User, Request(question, history, memory))],
            [],
            new AiCallOptions { Model = s.Router, JsonOutput = true, MaxOutputTokens = 2048, Context = context with { Purpose = "consultant.route" } },
            ct);

        var json = JsonText.ExtractObject(result.Text);
        if (json is null)
        {
            return ByKeywords(question, available, result.Usage);
        }

        var codes = available.Select(a => a.Code).ToHashSet();
        var agents = JsonText.Array(json.Value, "agents")
            .Select(a => a.ValueKind == System.Text.Json.JsonValueKind.String ? a.GetString() : null)
            .OfType<string>()
            .Select(a => a.Trim().ToLowerInvariant())
            .Where(codes.Contains)
            .Distinct()
            .ToList();

        var tasks = new Dictionary<string, string>();
        if (json.Value.TryGetProperty("tasks", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var p in t.EnumerateObject())
            {
                if (agents.Contains(p.Name) && p.Value.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                {
                    tasks[p.Name] = p.Value.GetString()!.Trim();
                }
            }
        }

        return new RoutingDecision(agents, tasks, JsonText.Str(json.Value, "reason") ?? string.Empty, false, result.Usage);
    }

    internal static RoutingDecision ByKeywords(string question, IReadOnlyList<AgentConfig> available, AiTokenUsage usage)
    {
        var q = question.ToLowerInvariant();
        var agents = available
            .Where(a => Keywords.TryGetValue(a.Code, out var words) && words.Any(q.Contains))
            .Select(a => a.Code)
            .ToList();
        if (agents.Count == 0)
        {
            agents = available.Select(a => a.Code).ToList();
        }

        return new RoutingDecision(agents, new Dictionary<string, string>(), "Выбор по ключевым словам вопроса (маршрутизатор не ответил разборчиво).", true, usage);
    }

    private static string Prompt(IReadOnlyList<AgentConfig> available)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ты — маршрутизатор главного AI-консультанта компании в OneBase. Реши, каких AI-сотрудников привлечь к вопросу пользователя.");
        sb.AppendLine();
        sb.AppendLine("Доступные AI-сотрудники:");
        foreach (var a in available)
        {
            sb.AppendLine($"- {a.Code} — {a.Name} ({a.Role}): {a.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("""
            Правила:
            - Привлекай только тех, чьи данные нужны для ответа. «Почему продажи упали?» → sales. «Почему прибыль упала?» → finance, sales, marketing, supply, production.
              «Как увеличить продажи?» → sales, marketing, finance. «Почему производство отстаёт?» → production, supply, finance.
              «Что происходит в компании и что улучшить?» → все доступные.
            - Если вопрос не требует данных компании (приветствие, благодарность, общий вопрос о методике) — agents: [].
            - Если это уточнение к предыдущему вопросу («а сравни с августом», «а по Самарканду?») — учитывай историю и сформулируй задачу полностью: что, за какой период, по какому региону.
            - Для каждого выбранного сотрудника дай конкретную задачу на его область.
            Верни только JSON:
            {"agents":["sales"],"tasks":{"sales":"полная формулировка задачи"},"reason":"одним предложением — почему эти сотрудники"}
            """);
        return sb.ToString();
    }

    private static string Request(string question, IReadOnlyList<LlmMessage> history, string? memory)
    {
        if (history.Count == 0 && memory is null)
        {
            return $"Вопрос: {question}";
        }

        var sb = new StringBuilder();
        if (memory is not null)
        {
            sb.AppendLine("Уже проанализировано в этом чате:");
            sb.AppendLine(memory);
        }

        sb.AppendLine("История чата (последние сообщения):");
        foreach (var m in history.TakeLast(6))
        {
            var text = m.Content.Length > 600 ? m.Content[..600] + "…" : m.Content;
            sb.AppendLine($"{(m.Role == LlmRole.User ? "Пользователь" : "Консультант")}: {text}");
        }

        sb.AppendLine();
        sb.Append($"Новый вопрос: {question}");
        return sb.ToString();
    }
}

using System.Text;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.AI.Tools.Data;
using OneBase.Domain.AI;

namespace OneBase.AI.Orchestration;

/// <summary>Решение маршрутизатора: какие AI-сотрудники и с какими задачами; пустой список — консультант отвечает сам.</summary>
public sealed record RoutingDecision(IReadOnlyList<string> Agents, IReadOnlyDictionary<string, string> Tasks, string Reason, bool ByKeywords, AiTokenUsage Usage);

/// <summary>
/// AI Router: по вопросу (и истории чата) определяет нужных AI-сотрудников. Если модель не ответила разборчиво —
/// запасной выбор по ключевым словам; если ни одно слово не подошло — все доступные сотрудники.
/// </summary>
public sealed class AiRouter(IAiGateway gateway, IAiSettingsSource settings, SalesGlossary? glossary = null, IModelComparisons? comparisons = null)
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
        // Названия категорий, товаров и регионов знает только отдел продаж — словарь нужен, когда он доступен пользователю.
        var hasSales = available.Any(a => a.Code == "sales");
        var glossaryText = hasSales && glossary is not null ? await glossary.TextAsync(ct) : null;
        var messages = Messages(available, glossaryText, question, history, memory);

        // Модель категории «Маршрутизация» (обычно Haiku): включена — задача идёт ею, при сбое — исходной моделью без повтора дешёвой.
        var route = AiTaskRouting.For(s, AiTaskCategory.Route);
        var options = Options(context, route);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await gateway.CompleteAsync(messages, [], options, ct);
        var elapsed = watch.ElapsedMilliseconds;
        var decision = Parse(result.Text, available, result.Usage);
        var usage = result.Usage;
        if (decision is null && route.Routed && !result.UsedFallback)
        {
            // Модель категории ответила некорректно (не JSON или только несуществующие сотрудники) — один раз исходной моделью.
            // Бюджет проверяет шлюз: исчерпан — выбор по ключевым словам, без ещё одного списания.
            try
            {
                var again = await gateway.CompleteAsync(messages, [],
                    options with { Model = route.Baseline, Fallback = null, RetrySameModel = true, Context = options.Context with { Purpose = "consultant.route.fallback" } }, ct);
                usage = usage.Add(again.Usage);
                decision = Parse(again.Text, available, usage);
            }
            catch (Exception ex) when (ex is LlmNotConfiguredException or AiProviderException)
            {
                decision = null;
            }
        }

        // Режим «Сравнение»: пользователь получает этот выбор, модель категории решает ту же задачу в фоне — итог только в журнал сравнения.
        if (route.Shadow is { } shadow && comparisons is not null)
        {
            var baseline = new ComparisonSide(result.Model.ToString(), decision is null ? null : RouteResult(decision), decision is not null, result.Usage, elapsed);
            _ = comparisons.ShadowAsync(AiTaskCategory.Route, messages, options, shadow, question, baseline,
                text => Parse(text, available, AiTokenUsage.None) is { } d ? (RouteResult(d), true) : (null, false), RouteSimilarity);
        }

        return decision ?? ByKeywords(question, available, usage, hasSales && glossary is not null ? await glossary.KeywordsAsync(ct) : null);
    }

    /// <summary>Сообщения маршрутизатору: правила и список сотрудников (неизменные — кэшируются у провайдера) и вопрос с историей.</summary>
    internal static IReadOnlyList<LlmMessage> Messages(IReadOnlyList<AgentConfig> available, string? glossaryText, string question,
        IReadOnlyList<LlmMessage> history, string? memory) =>
        [new LlmMessage(LlmRole.System, Prompt(available, glossaryText)), new LlmMessage(LlmRole.User, Request(question, history, memory))];

    /// <summary>
    /// Выбор сотрудников — простая задача: рассуждения всегда минимальные, чтобы не ждать десятки секунд. Лимит — с запасом на рассуждения:
    /// у Claude 5 они идут всегда и входят в max_tokens.
    /// </summary>
    internal static AiCallOptions Options(AiCallContext context, AiTaskRoute route) => new()
    {
        Model = route.Model,
        Fallback = route.Fallback,
        RetrySameModel = !route.Routed,
        JsonOutput = true,
        MaxOutputTokens = 4096,
        ReasoningEffort = AiReasoning.Low,
        Context = context with { Purpose = "consultant.route" },
    };

    /// <summary>Разбор ответа маршрутизатора; null — ответ некорректен: не JSON, нет списка agents или в нём только несуществующие сотрудники.</summary>
    internal static RoutingDecision? Parse(string? text, IReadOnlyList<AgentConfig> available, AiTokenUsage usage)
    {
        var json = JsonText.ExtractObject(text);
        if (json is null || !json.Value.TryGetProperty("agents", out var list) || list.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return null;
        }

        var codes = available.Select(a => a.Code).ToHashSet();
        var named = list.EnumerateArray()
            .Select(a => a.ValueKind == System.Text.Json.JsonValueKind.String ? a.GetString() : null)
            .OfType<string>()
            .Select(a => a.Trim().ToLowerInvariant())
            .ToList();
        var agents = named.Where(codes.Contains).Distinct().ToList();
        if (named.Count > 0 && agents.Count == 0)
        {
            return null;
        }

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

        return new RoutingDecision(agents, tasks, JsonText.Str(json.Value, "reason") ?? string.Empty, false, usage);
    }

    /// <summary>Выбор в сравнимом виде: коды сотрудников по алфавиту через запятую; пусто — консультант отвечает сам.</summary>
    internal static string RouteResult(RoutingDecision decision) => string.Join(",", decision.Agents.Order(StringComparer.Ordinal));

    /// <summary>Мера Жаккара по сотрудникам двух выборов: 1 — одинаковые (в том числе оба пустые), 0 — без общих.</summary>
    internal static double RouteSimilarity(string? a, string? b)
    {
        static HashSet<string> Set(string? v) => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var x = Set(a);
        var y = Set(b);
        var union = x.Union(y).Count();
        return union == 0 ? 1 : (double)x.Intersect(y).Count() / union;
    }

    /// <summary>salesWords — названия категорий, типов и регионов из словаря компании: вопрос про «помадку» — к продажам.</summary>
    internal static RoutingDecision ByKeywords(string question, IReadOnlyList<AgentConfig> available, AiTokenUsage usage, IReadOnlyList<string>? salesWords = null)
    {
        var agents = KeywordAgents(question, available, salesWords).ToList();
        if (agents.Count == 0)
        {
            agents = available.Select(a => a.Code).ToList();
        }

        return new RoutingDecision(agents, new Dictionary<string, string>(), "Выбор по ключевым словам вопроса (маршрутизатор не ответил разборчиво).", true, usage);
    }

    /// <summary>AI-сотрудники, к области которых относятся слова текста; ни одно не подошло — пусто (без «все доступные»).</summary>
    internal static IReadOnlyList<string> KeywordAgents(string text, IReadOnlyList<AgentConfig> available, IReadOnlyList<string>? salesWords = null)
    {
        var q = text.ToLowerInvariant();
        var tokens = NameMatch.Tokens(text);
        return available
            .Where(a => (Keywords.TryGetValue(a.Code, out var words) && words.Any(q.Contains))
                || (a.Code == "sales" && salesWords is not null && salesWords.Any(w => NameMatch.MentionedIn(tokens, w))))
            .Select(a => a.Code)
            .ToList();
    }

    private static string Prompt(IReadOnlyList<AgentConfig> available, string? glossaryText)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ты — маршрутизатор главного AI-консультанта компании в OneBase. Реши, каких AI-сотрудников привлечь к вопросу пользователя.");
        sb.AppendLine();
        sb.AppendLine("Доступные AI-сотрудники:");
        foreach (var a in available)
        {
            sb.AppendLine($"- {a.Code} — {a.Name} ({a.Role}): {a.Description}");
        }

        if (glossaryText is not null)
        {
            sb.AppendLine();
            sb.AppendLine(glossaryText);
            sb.AppendLine("Вопрос о товаре, категории, бренде, артикуле или регионе из словаря — это вопрос о продажах (sales); в задаче сотруднику сохрани название как в вопросе.");
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

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Research;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Security;

namespace OneBase.AI.Analytik;

/// <summary>
/// analytics_research — исследование одним серверным вызовом (Research Planner + Executor): план операций, параллельные срезы и расчёты,
/// кэш одинаковых срезов, повтор с исправлением, выводы кодом с уровнем доказательности. Модель не делает десятки вызовов и не считает
/// формулами: она выбирает шаблон и период и пересказывает выводы. Есть у консультанта и у аналитиков (источник sales.stats).
/// </summary>
public sealed class ResearchTool(ResearchService research, SemanticEngine engine) : DataTool
{
    public const string ToolName = "analytics_research";

    public override string Name => ToolName;
    public override string Title => "Исследование (план, расчёты, выводы)";
    public override int Level => ToolLevels.Analyze;
    public override string? Source => KnowledgeSources.SalesStats;
    public override string? RequiredPermission => Permissions.SalesRead;
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.FinanceRead];

    public override string Description =>
        "Полное исследование одним вызовом — сервер сам строит план, параллельно выполняет срезы и статистические расчёты и возвращает выводы с " +
        "уровнем доказательности (FACT, CALCULATED, STATISTICALLY_SUPPORTED, CORRELATION, HYPOTHESIS, FORECAST, UNVERIFIED). Шаблоны: " +
        string.Join("; ", ResearchKinds.All.Select(k => $"{k} — {ResearchKinds.Title(k)}")) + ".";

    public override string? UsageHint =>
        "Корреляции во всех показателях, причины снижения продаж, эффективность ТП по визитам и продажам, регионы «визиты растут — продажи падают», " +
        "влияние товарных групп, аномалии во всех показателях, эффективность акций, состояние всей компании — analytics_research одним вызовом, " +
        "а не десятки срезов и формул. Отвечай по его выводам и сохраняй уровень каждого: корреляция — «связано», гипотеза — «возможно».";

    public override JsonElement InputSchema { get; } = Schema(
        ("kind", new { type = "string", @enum = ResearchKinds.All.ToArray(), description = string.Join("; ", ResearchKinds.All.Select(k => $"{k}: {ResearchKinds.Title(k)}")) }),
        ("question", new { type = "string", description = "Вопрос пользователя (для журнала исследования)." }),
        ("days", new { type = "integer", description = "Последние N дней (например, 103). Без дат — по шаблону: 30 дней (сравнения), 60 (аномалии), 90 (корреляции)." }),
        ("date_from", new { type = "string", description = "Начало периода YYYY-MM-DD." }),
        ("date_to", new { type = "string", description = "Конец периода YYYY-MM-DD." }),
        ("metric", new { type = "string", @enum = new[] { "revenue", "kg", "orders" }, description = "Главный показатель (по умолчанию выручка)." }),
        ("filters", new
        {
            type = "array",
            description = "Фильтры (как analytics_query): {dimension, op, values} — например, регион «Самарканд».",
            items = new
            {
                type = "object",
                properties = new { dimension = new { type = "string" }, op = new { type = "string" }, values = new { type = "array", items = new { type = "string" } } },
                required = new[] { "dimension", "values" },
            },
        }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        ResearchRequest request;
        try
        {
            request = Parse(args, engine.Today);
        }
        catch (FormatException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.InvalidArguments, ex.Message);
        }

        ResearchResult r;
        try
        {
            r = await research.RunAsync(request, new ResearchCaller(context.UserId, context.Depth > 0 ? "investigation" : "chat", context.ConversationId, context.AgentCode), ct);
        }
        catch (ComputeException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.InvalidArguments, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, ex.Message);
        }

        return Present(r);
    }

    public static ResearchRequest Parse(ToolArgs args, DateOnly today)
    {
        static DateOnly? Date(string? s, string name) => s is null ? null
            : DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : throw new FormatException($"{name}: дата «{s}», ожидается YYYY-MM-DD.");
        var kind = args.Str("kind") ?? throw new FormatException($"Нужен шаблон исследования kind: {string.Join(", ", ResearchKinds.All)}.");
        if (!ResearchKinds.All.Contains(kind))
        {
            throw new FormatException($"Шаблона «{kind}» нет. Есть: {string.Join(", ", ResearchKinds.All)}.");
        }

        var parameters = new Dictionary<string, string>();
        if (args.Int("days", 7, SemanticEngine.MaxDays) is { } days)
        {
            parameters["days"] = days.ToString(CultureInfo.InvariantCulture);
        }

        return new ResearchRequest
        {
            Kind = kind, Question = args.Str("question"), From = Date(args.Str("date_from"), "date_from"), To = Date(args.Str("date_to"), "date_to"),
            Metric = args.Str("metric"), Filters = AnalyticsQueryTool.Filters(args.Element("filters")), Params = parameters,
        };
    }

    /// <summary>Итог для модели: выводы по разделам (без больших таблиц — они на странице исследования), гипотезы, операции, ограничения, время.</summary>
    internal static ToolResult Present(ResearchResult r)
    {
        var link = $"/analytik/investigations/{r.Id}";
        var payload = new
        {
            ResearchId = r.Id, r.Kind, r.Title, r.Status,
            Period = new { From = r.Plan.From.ToString("yyyy-MM-dd"), To = r.Plan.To.ToString("yyyy-MM-dd") },
            Compare = r.Plan.CompareFrom is { } cf ? new { From = cf.ToString("yyyy-MM-dd"), To = r.Plan.CompareTo!.Value.ToString("yyyy-MM-dd") } : null,
            Findings = r.Findings.GroupBy(f => f.Section).ToDictionary(g => g.Key, g => g.Select(f => new { f.Level, f.Text }).ToList()),
            Hypotheses = r.Hypotheses.Select(h => new { h.Statement, h.Status, h.Level, h.Basis }),
            Operations = r.Plan.Operations.Select(o => new { o.Id, o.Title, o.Status, o.ErrorReason, o.FromCache, o.ElapsedMs }),
            r.Limitations,
            Sources = r.Sources.Select(s => new { s.Title, DataThrough = s.DataThrough?.ToString("yyyy-MM-dd"), SyncedAt = s.SyncedAt?.ToString("yyyy-MM-dd HH:mm") }),
            Timings = r.Timings,
            Link = link,
            Instructions = "Ответ — по разделам findings: краткий вывод, показатели, закономерности, корреляции, значимость, причины и гипотезы, различия, риски, " +
                           "рекомендации, ограничения, источники. Числа — только отсюда. Уровень вывода сохраняй словами: FACT — факт, CALCULATED — расчёт, " +
                           "STATISTICALLY_SUPPORTED — статистически подтверждено, CORRELATION — «связано», не «из-за», HYPOTHESIS — «возможно», FORECAST — прогноз, " +
                           "UNVERIFIED — не проверено. Операции со статусом не SUCCESS назови в ограничениях своими словами (не «данных нет», если это сбой).",
        };
        return new ToolResult(true, JsonSerializer.Serialize(payload, Json), null,
            [new DataSource($"Аналитик → Исследование «{r.Title}»", $"{r.Plan.From:dd.MM.yyyy} — {r.Plan.To:dd.MM.yyyy}", link)]);
    }
}

/// <summary>
/// Ответ чата Аналитика по исследованию-шаблону: исследование — кодом (ResearchService), ответ — одним вызовом модели по его выводам с проверкой
/// чисел (GroundedComposer). Модель недоступна, выключена или платные вызовы заблокированы — ответом становится отчёт исследования (ResearchBrief):
/// пользователь получает выводы без модели.
/// </summary>
public sealed class ResearchAnswerer(ResearchService research, IAiGateway gateway, IAiSettingsSource aiSettings, TimeProvider clock, ILogger<ResearchAnswerer> logger)
{
    public const string Rules = """
        Ответ по исследованию OneBase. Данные — только выводы исследования ниже: числа бери оттуда, не пересчитывай и не добавляй своих.
        Структура (пустые разделы пропускай): 1) краткий вывод; 2) основные показатели; 3) найденные закономерности; 4) корреляции; 5) статистическая
        значимость; 6) основные причины и гипотезы (со статусом проверки); 7) региональные и товарные различия; 8) риски; 9) рекомендации;
        10) ограничения данных; 11) источники (разделы OneBase и даты — без названий инструментов, SQL и таблиц).
        Уровень каждого вывода сохраняй словами: факт; расчёт; статистически подтверждено; корреляция — «связано», «меняются вместе», никогда «из-за»;
        гипотеза — «возможно», «может быть одной из причин»; прогноз — «ожидается»; не проверено — «проверить не удалось». Гипотезу не выдавай за факт.
        Своих прогнозов и связей между показателями не добавляй: прогноз — только если он есть в выводах; в разделе «Корреляции» — только посчитанные
        связи (с r и p или q), совпадение по времени двух событий корреляцией не называй.
        Если часть данных не получена — скажи, чего именно не хватает и почему (сбой источника, нет прав, нет данных), а не просто «данных нет».
        Пиши для руководителя: конкретно, без технических деталей; подробности — на странице исследования (ссылка в конце).
        """;

    public async Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, ResearchMatch match, AgentConfig? chief, Func<ConsultantProgress, Task> progress, CancellationToken ct)
    {
        var timings = AiTurnTimings.Current;
        await progress(new ConsultantProgress("plan", null, null, ProgressStatus.Running, $"исследование: {ResearchKinds.Title(match.Kind).ToLowerInvariant()}"));
        ResearchResult r;
        using (timings?.Stage("data"))
        {
            r = await research.RunAsync(new ResearchRequest
                {
                    Kind = match.Kind, Question = turn.Question, From = match.From, To = match.To,
                    Params = match.Days is { } d ? new Dictionary<string, string> { ["days"] = d.ToString(CultureInfo.InvariantCulture) } : null,
                },
                new ResearchCaller(turn.UserId, "chat", turn.ConversationId, AnalytikAgents.Chief),
                ct, step => progress(new ConsultantProgress("plan", null, null, ProgressStatus.Running, step)));
        }

        await progress(new ConsultantProgress("plan", null, null, ProgressStatus.Done,
            $"операций {r.Plan.Operations.Count}, выводов {r.Findings.Count}, {(r.Timings.CacheHits > 0 ? $"из кэша {r.Timings.CacheHits}, " : "")}{r.Timings.TotalMs / 1000.0:0.0} с"));
        var link = $"/analytik/investigations/{r.Id}";
        var brief = ResearchBrief.Render(r, link);
        var sources = r.Sources.Select(s => new DataSource(s.Title, s.DataThrough is { } dt ? $"по {dt:dd.MM.yyyy}" : null, null))
            .Append(new DataSource($"Аналитик → Исследование «{r.Title}»", $"{r.Plan.From:dd.MM.yyyy} — {r.Plan.To:dd.MM.yyyy}", link)).ToList();

        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Running));
        var evidence = new EvidenceSet();
        evidence.AddText(brief);
        var config = await aiSettings.GetSettingsAsync(ct);
        var choice = AiModelRouting.Final(config, turn.Channel?.Model, chief?.Model, new AnswerComplexity(Math.Max(5, config.ModelRouting.ComplexityThreshold), ["research"]));
        var now = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5));
        var data = new StringBuilder()
            .AppendLine($"Сейчас {now:dd.MM.yyyy HH:mm} (Ташкент).")
            .AppendLine($"Язык вопроса — {AnswerLanguages.Label(AnswerLanguages.Of(turn.Question))}: отвечай на нём.")
            .AppendLine()
            .AppendLine("Выводы исследования (уровень — в скобках):")
            .AppendLine(brief)
            .ToString();
        try
        {
            GroundedAnswer answer;
            using (timings?.Stage("compose"))
            {
                answer = await new GroundedComposer(gateway).ComposeAsync(
                    [new LlmMessage(LlmRole.System, OrchestratedConsultantEngine.ComposeRules(chief) + "\n" + Rules), new LlmMessage(LlmRole.System, data), .. turn.History,
                     new LlmMessage(LlmRole.User, turn.Question)],
                    new AiCallOptions
                    {
                        Model = choice.Model, ModelReason = choice.Reason, Temperature = chief?.Temperature,
                        MaxOutputTokens = turn.Channel?.MaxOutputTokens ?? chief?.MaxOutputTokens ?? Math.Max(config.MaxOutputTokens, AgentRunner.AgentOutputTokens),
                        CacheConversation = false,
                        Context = new AiCallContext(turn.UserId, AnalytikAgents.Chief, turn.ConversationId, turn.Channel?.Name ?? "analytik") { Purpose = "analytik.research" },
                    },
                    evidence, turn.Question, ct, null, turn.Draft);
            }

            await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Done));
            var text = answer.Text.Contains(link, StringComparison.Ordinal) ? answer.Text : answer.Text.TrimEnd() + $"\n\nПодробности исследования: {link}";
            return new ConsultantAnswer(text,
                new ConsultantDetails([], sources, answer.Model.ToString(), answer.UsedFallback,
                    $"Исследование «{r.Title}»: операций {r.Plan.Operations.Count}, выводов {r.Findings.Count}, статус {r.Status}.", 0, null, answer.Grounding, null, r.Id),
                answer.Usage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Модель недоступна (не настроена, выключена, бюджет, блокировка платных вызовов) — ответ без модели: отчёт исследования.
            logger.LogInformation(ex, "Исследование {Id}: ответ без модели", r.Id);
            await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Done, "ответ без модели — отчёт исследования"));
            return new ConsultantAnswer(brief,
                new ConsultantDetails([], sources, null, true, $"Исследование «{r.Title}» (ответ без модели: {Reason(ex)}).", 0, null, null, null, r.Id),
                AiTokenUsage.None);
        }
    }

    private static string Reason(Exception ex) => ex switch
    {
        LlmNotConfiguredException => "модель не настроена",
        _ => "модель недоступна",
    };
}

using System.Text.Json;
using System.Text.Json.Serialization;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.Application.Analytik.Research;

/// <summary>Шаблоны исследований: что исследуется одним серверным вызовом, без модели.</summary>
public static class ResearchKinds
{
    public const string Correlations = "correlations";
    public const string DeclineDrivers = "decline_drivers";
    public const string AgentEfficiency = "agent_efficiency";
    public const string VisitsVsSales = "visits_vs_sales";
    public const string GroupInfluence = "group_influence";
    public const string Anomalies = "anomalies";
    public const string PromoEffect = "promo_effect";
    public const string CompanyHealth = "company_health";

    public static readonly IReadOnlyList<string> All = [Correlations, DeclineDrivers, AgentEfficiency, VisitsVsSales, GroupInfluence, Anomalies, PromoEffect, CompanyHealth];

    public static string Title(string kind) => kind switch
    {
        Correlations => "Корреляции и закономерности",
        DeclineDrivers => "Причины изменения продаж",
        AgentEfficiency => "Эффективность ТП: визиты и продажи",
        VisitsVsSales => "Визиты и продажи по регионам",
        GroupInfluence => "Влияние товарных групп на выручку",
        Anomalies => "Аномалии показателей",
        PromoEffect => "Эффективность акций",
        CompanyHealth => "Состояние компании и риски",
        _ => kind,
    };
}

/// <summary>
/// Статусы операций и сбоев (ТЗ Phase 2, Smart Fallback). Техническая ошибка никогда не превращается в «данных нет»: у каждого сбоя свой статус.
/// </summary>
public static class ResearchStatuses
{
    public const string Pending = "PENDING";
    public const string Running = "RUNNING";
    public const string Success = "SUCCESS";
    public const string PartialResult = "PARTIAL_RESULT";
    public const string DataNotFound = "DATA_NOT_FOUND";
    public const string ToolUnsupported = "TOOL_UNSUPPORTED";
    public const string SourceUnavailable = "SOURCE_UNAVAILABLE";
    public const string NoAccess = "NO_ACCESS";
    public const string InsufficientGranularity = "INSUFFICIENT_GRANULARITY";
    public const string CalculationFailed = "CALCULATION_FAILED";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Skipped = "SKIPPED";
    public const string Cancelled = "CANCELLED";

    /// <summary>Операция дала пригодный результат (полный или частичный).</summary>
    public static bool Usable(string status) => status is Success or PartialResult;

    public static string Title(string status) => status switch
    {
        Success => "готово",
        PartialResult => "частично",
        DataNotFound => "данных нет",
        ToolUnsupported => "не поддерживается",
        SourceUnavailable => "источник недоступен",
        NoAccess => "нет доступа",
        InsufficientGranularity => "нет нужной детализации",
        CalculationFailed => "расчёт не выполнен",
        ValidationFailed => "проверка не пройдена",
        Skipped => "пропущено",
        Cancelled => "отменено",
        Running => "выполняется",
        _ => "ожидает",
    };

    /// <summary>Статус по коду отказа движка срезов (invalid, denied, unsupported, too_heavy, timeout, error).</summary>
    public static string OfQuery(string code, string message) => code switch
    {
        "denied" => NoAccess,
        "unsupported" when message.Contains("по дням", StringComparison.OrdinalIgnoreCase) || message.Contains("помесяч", StringComparison.OrdinalIgnoreCase)
            => InsufficientGranularity,
        "unsupported" => ToolUnsupported,
        "invalid" => ToolUnsupported,
        "too_heavy" or "timeout" or "error" => SourceUnavailable,
        _ => SourceUnavailable,
    };
}

/// <summary>
/// Операция плана: id, вид (query — срез движка, compute — метод вычислительного движка, promo — эффект акций страницы «Акции»), что делает,
/// источник, параметры, зависимости, таймаут; во время выполнения — статус, ссылка на результат (id среза или расчёта), причина сбоя, попытки,
/// из кэша ли результат, время, короткий итог.
/// </summary>
public sealed class ResearchOperation
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required string Source { get; init; }
    public AnalyticsQuery? Query { get; init; }
    public ComputeRequest? Compute { get; init; }
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = [];
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>Не обязательна для вывода: её сбой даёт PARTIAL исследования, а не FAILED.</summary>
    public bool Optional { get; init; }

    public string Status { get; set; } = ResearchStatuses.Pending;
    public string? ResultReference { get; set; }
    public string? ErrorReason { get; set; }
    public int Attempts { get; set; }
    public bool FromCache { get; set; }
    public long ElapsedMs { get; set; }
    public string? Summary { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// План исследования по контракту ТЗ: id, вопрос, цель, нужные показатели и источники, операции с зависимостями, ожидаемые результаты, правила
/// проверки, область доступа, статус выполнения; плюс шаблон, период и волны (операции, идущие параллельно).
/// </summary>
public sealed class ResearchPlan
{
    public Guid ResearchId { get; set; }
    public required string Kind { get; init; }
    public required string UserQuestion { get; init; }
    public required string Objective { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public DateOnly? CompareFrom { get; init; }
    public DateOnly? CompareTo { get; init; }
    public IReadOnlyList<string> RequiredMetrics { get; init; } = [];
    public IReadOnlyList<string> RequiredSources { get; init; } = [];
    public List<ResearchOperation> Operations { get; init; } = [];
    public IReadOnlyList<string> ExpectedResults { get; init; } = [];
    public IReadOnlyList<string> ValidationRules { get; init; } = [];
    public string AccessScope { get; set; } = "";
    public string ExecutionStatus { get; set; } = "planned";
    public List<string> Limitations { get; init; } = [];

    /// <summary>Зависимости плана: операция → от каких зависит.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Dependencies => Operations.ToDictionary(o => o.Id, o => o.DependsOn);

    /// <summary>Волны: операции без невыполненных зависимостей идут параллельно; цикл разрывается следующей операцией.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Waves()
    {
        var done = new HashSet<string>();
        var left = Operations.ToList();
        var waves = new List<IReadOnlyList<string>>();
        while (left.Count > 0)
        {
            var wave = left.Where(o => o.DependsOn.All(done.Contains) || o.DependsOn.All(d => Operations.All(x => x.Id != d))).ToList();
            if (wave.Count == 0)
            {
                wave = [left[0]];
            }

            waves.Add(wave.Select(o => o.Id).ToList());
            foreach (var o in wave)
            {
                done.Add(o.Id);
                left.Remove(o);
            }
        }

        return waves;
    }
}

/// <summary>
/// Вывод исследования: раздел ответа (summary, metrics, patterns, correlations, significance, causes, differences, risks, recommendations,
/// limitations), уровень доказательности (EvidenceLevels), текст, значение и единица, на какие операции опирается.
/// </summary>
public sealed record ResearchFinding(string Section, string Level, string Text, double? Value = null, string? Unit = null, IReadOnlyList<string>? Evidence = null);

/// <summary>Гипотеза исследования и её проверка кодом: confirmed | partially_confirmed | rejected | inconclusive.</summary>
public sealed record ResearchHypothesis(string Id, string Statement, string Status, string Level, string Basis, IReadOnlyList<string> Evidence);

/// <summary>Время исследования по этапам и ресурсы: срезы, расчёты, проверка, всего; операций, повторов, попаданий в кэш.</summary>
public sealed record ResearchTimings(long PlanMs, long SqlMs, long StatisticsMs, long EvidenceMs, long TotalMs, int Operations, int Queries, int Computations,
    int CacheHits, int Retries, double CacheHitRatio);

/// <summary>Итог исследования: план с результатами операций, выводы, гипотезы, ограничения, источники, время, статус.</summary>
public sealed record ResearchResult(
    Guid Id,
    string Kind,
    string Title,
    string Status,
    ResearchPlan Plan,
    IReadOnlyList<ResearchFinding> Findings,
    IReadOnlyList<ResearchHypothesis> Hypotheses,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<AnalyticsSource> Sources,
    ResearchTimings Timings,
    object? Data);

/// <summary>Запрос исследования: шаблон, вопрос (для журнала), период, фильтры, главный показатель, дополнительные параметры.</summary>
public sealed record ResearchRequest
{
    public required string Kind { get; init; }
    public string? Question { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public IReadOnlyList<AnalyticsFilter>? Filters { get; init; }
    public string? Metric { get; init; }
    public IReadOnlyDictionary<string, string>? Params { get; init; }
}

/// <summary>Кто запускает: пользователь (его права), откуда (chat, api, investigation), чат, агент, родительское исследование.</summary>
public sealed record ResearchCaller(Guid? UserId, string Origin, Guid? ConversationId = null, string? AgentCode = null);

internal static class ResearchJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
}

using Microsoft.EntityFrameworkCore;
using OneBase.Application.Analytik.Catalog;
using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik;

/// <summary>
/// Таблицы схемы analytik. Отдельно от IAppDbContext (тот же экземпляр DbContext в области DI): одно SaveChangesAsync сохраняет и их, и
/// остальные изменения области.
/// </summary>
public interface IAnalytikDb
{
    DbSet<CatalogTable> CatalogTables { get; }
    DbSet<CatalogColumn> CatalogColumns { get; }
    DbSet<CatalogRelation> CatalogRelations { get; }
    DbSet<CatalogRun> CatalogRuns { get; }
    DbSet<CatalogAnnotation> CatalogAnnotations { get; }
    DbSet<AnalytikQuery> Queries { get; }
    DbSet<AnalytikInsight> Insights { get; }
    DbSet<AnalytikForecast> Forecasts { get; }
    DbSet<AnalytikReport> Reports { get; }
    DbSet<AnalytikJobRun> JobRuns { get; }
    DbSet<AnalytikMetricSnapshot> MetricSnapshots { get; }
    DbSet<AnalytikScenarioBaseline> ScenarioBaselines { get; }
    DbSet<AnalytikScenario> Scenarios { get; }
    DbSet<AnalytikComputation> Computations { get; }
    DbSet<AnalytikInvestigation> Investigations { get; }
    DbSet<AnalytikInsightEvent> InsightEvents { get; }
    DbSet<AnalytikDecision> Decisions { get; }
    DbSet<AnalytikBenchmarkRun> BenchmarkRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Параметр запроса: имя (@p0…) и значение — уходит драйверу отдельно от текста SQL.</summary>
public sealed record SqlParam(string Name, object? Value);

/// <summary>Пределы одного запроса: строк, секунд выполнения.</summary>
public sealed record SqlLimits(int MaxRows, int TimeoutSeconds);

/// <summary>Результат запроса: колонки, их типы PostgreSQL, строки (до MaxRows), обрезан ли результат, время.</summary>
public sealed record SqlResult(IReadOnlyList<string> Columns, IReadOnlyList<string> Types, IReadOnlyList<object?[]> Rows, bool Truncated, long ElapsedMs);

/// <summary>Запрос не уложился в лимит времени (statement_timeout).</summary>
public sealed class AnalytikTimeoutException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Выполнение SQL аналитического движка: только чтение (транзакция READ ONLY), statement_timeout и lock_timeout на каждый запрос, предел строк,
/// своё подключение (ConnectionStrings:AnalytikReadOnly — отдельная роль PostgreSQL только на чтение; без неё — основное) и не больше
/// Analytik:MaxConcurrentQueries запросов одновременно, чтобы аналитика не отнимала соединения у остальных модулей.
/// </summary>
public interface IAnalytikSql
{
    Task<SqlResult> QueryAsync(string sql, IReadOnlyList<SqlParam> parameters, SqlLimits limits, CancellationToken ct);

    /// <summary>Подключение — отдельная роль только на чтение (а не основное подключение OneBase).</summary>
    bool DedicatedReadOnlyRole { get; }
}

/// <summary>Чтение структуры базы из pg_catalog: схемы, таблицы, колонки, ключи, индексы, оценки строк и размеры.</summary>
public interface ISchemaReader
{
    Task<SchemaSnapshot> ReadAsync(CancellationToken ct);
}

/// <summary>Настройки Аналитика (секция Analytik в конфигурации).</summary>
public sealed class AnalytikOptions
{
    /// <summary>Компания (развёртывание) — в журнал запросов; мультиарендности в OneBase нет.</summary>
    public string Tenant { get; init; } = "onebase";

    public int QueryTimeoutSeconds { get; init; } = 15;

    /// <summary>Строк в ответе запроса (список).</summary>
    public int MaxRows { get; init; } = 500;

    /// <summary>Групп в ответе запроса с группировкой.</summary>
    public int MaxGroups { get; init; } = 2000;

    public int MaxConcurrentQueries { get; init; } = 4;

    /// <summary>Предел оценки стоимости запроса PostgreSQL (EXPLAIN): тяжелее — отказ с советом сузить условия, без выполнения.</summary>
    public double MaxQueryCost { get; init; } = 5_000_000;

    /// <summary>До какой оценки строк таблица считается точно (count(*)) при обновлении каталога.</summary>
    public long ExactCountThreshold { get; init; } = 200_000;

    /// <summary>Фоновые задачи Аналитика (каталог, риски, отчёты).</summary>
    public bool WorkerEnabled { get; init; } = true;

    /// <summary>Текст отчётов от модели в фоне — выключено: фоновые отчёты считаются кодом, без платных вызовов.</summary>
    public bool NarrativesEnabled { get; init; }

    /// <summary>
    /// Срок поставки, если он не задан у поставщика и не виден по его заказам: в расчёте закупки он помечается допущением, а не выдаётся за факт.
    /// </summary>
    public int DefaultLeadTimeDays { get; init; } = 7;

    /// <summary>Время ежедневного отчёта по Ташкенту (ЧЧ:ММ).</summary>
    public string DailyAt { get; init; } = "07:30";

    /// <summary>
    /// Сколько секунд AI-аналитик ждёт идущего обновления отстающих данных (Linko, Verifix) перед ответом; не успело — отвечает по тому, что есть,
    /// и говорит, по какую дату данные. 0 — не ждать.
    /// </summary>
    public int FreshWaitSeconds { get; init; } = 45;

    /// <summary>
    /// Исследования сложных вопросов в чате Аналитика (Advanced Intelligence): auto — по сложности вопроса (правила, без вызова модели),
    /// always — каждый вопрос о данных, off — прежний путь «аналитики → сводка».
    /// </summary>
    public string InvestigationMode { get; init; } = "auto";

    /// <summary>С какого балла сложности вопрос исследуется (InvestigationTriage).</summary>
    public int InvestigationThreshold { get; init; } = 3;

    /// <summary>С какого балла исследование глубокое: скептик и независимая проверка ответа моделью.</summary>
    public int InvestigationDeepThreshold { get; init; } = 6;

    /// <summary>Шагов в плане, гипотез, перепланирований и циклов проверки ответа — не больше.</summary>
    public int InvestigationMaxSteps { get; init; } = 8;

    public int InvestigationMaxHypotheses { get; init; } = 6;
    public int InvestigationMaxReplans { get; init; } = 1;
    public int InvestigationReviewCycles { get; init; } = 2;

    /// <summary>Шагов одновременно (каждый — цикл модели аналитика).</summary>
    public int InvestigationParallelSteps { get; init; } = 4;
}

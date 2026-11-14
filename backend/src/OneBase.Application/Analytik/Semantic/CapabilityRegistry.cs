using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>Факт движка в реестре: источник, детализация, показатели, измерения, шаги, диапазон данных, последняя загрузка, статус, ограничения.</summary>
public sealed record CapabilityFact(
    string Code,
    string Title,
    string Source,
    string Detail,
    IReadOnlyList<string> Metrics,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Grains,
    DateOnly? First,
    DateOnly? Last,
    DateTimeOffset? SyncedAt,
    string Status,
    string? Limits,
    bool Allowed);

/// <summary>Данные, которые отдаёт не движок, а готовый инструмент отдела: где искать, прежде чем сказать «нет данных».</summary>
public sealed record CapabilityTool(string Area, string Title, string Tools, string Detail);

/// <summary>Запуск загрузки Linko из истории (linko.SyncRuns).</summary>
public sealed record SyncRunInfo(string Mode, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, string Status, int Rows, DateOnly? EarliestChanged, string? Error);

/// <summary>Реестр возможностей: что можно посчитать, по каким измерениям, за какие даты, откуда и чего в источниках нет вовсе.</summary>
public sealed record CapabilityView(
    IReadOnlyList<MetricDef> Metrics,
    IReadOnlyList<DimensionDef> Dimensions,
    IReadOnlyList<string> Grains,
    IReadOnlyList<CapabilityFact> Facts,
    IReadOnlyList<CapabilityTool> Tools,
    IReadOnlyList<string> Missing,
    IReadOnlyList<SyncRunInfo> SyncRuns,
    DateTimeOffset At);

/// <summary>
/// DataCapabilityRegistry: статическая часть — реестр показателей и измерений (SemanticCatalog), живая — диапазоны данных и последние загрузки
/// (считаются при каждом обращении, поэтому реестр всегда после последней синхронизации). Перед ответом «данных нет» AI проверяет здесь:
/// есть ли показатель, измерение, детализация, период, другой источник или инструмент.
/// </summary>
public sealed class CapabilityRegistry(SemanticEngine engine, AnalytikAccess access, IAppDbContext db, IAnalytikDb adb, TimeProvider clock)
{
    public static readonly IReadOnlyList<CapabilityTool> OtherTools =
    [
        new("Первичка", "Отгрузки завода дилерам (кг, коробки, суммы, план первички)", "get_primary_shipments",
            "Помесячно, регион — по складу дилера. Это не вторичка: выручка движка и отгрузки не складываются"),
        new("Экспорт", "Экспорт по странам и продажи «Завода»", "get_export_sales; analytics_query segment=factory", "Помесячно; движок — с любым шагом"),
        new("Визиты", "Визиты ТП, страйк, проблемные агенты", "get_visits, get_problem_agents, get_sales_days", "По дням внутри месяца"),
        new("Планы ТП и KPI Linko", "Факт и план ТП так, как их считает Linko (staff_balance)", "get_sales_kpi, get_kpi",
            "Факт Linko отличается от факта OneBase (другие правила) — не смешивать"),
        new("Сырьё и закупки", "План закупок сырья (MRP), склад сырья, поставщики, цены, заявки и заказы", "analytik_procurement_plan, analytik_warehouse, get_procurement_overview, get_purchase_orders",
            "Снабжение OneBase; готовая продукция — в Linko (движок: остаток, дни запаса, потребность)"),
        new("Производство", "Задания, смены, выпуск, брак, техкарты", "analytik_production", "Производство OneBase"),
        new("Персонал", "Сотрудники, посещаемость, опоздания, табели", "analytik_hr, get_hr_employees, get_hr_timesheet", "Verifix"),
        new("Финансы", "P&L дилеров, дебиторка, DSO, оплаты", "analytik_finance, get_pnl, get_dso", "P&L — из Google Sheets / AI Data Manager"),
        new("Прогнозы и риски", "Прогнозы продаж, найденные риски и отклонения", "analytik_forecast, analytik_risks", "Аналитик"),
        new("Любая таблица", "Каталог всей базы и запрос по типизированному плану", "analytik_find_tables, analytik_query",
            "Когда в реестре нет готового показателя: таблицы и колонки — из каталога, правила отбора задаёт запрос"),
    ];

    public static readonly IReadOnlyList<string> Missing =
    [
        "1С не подключена; факты продаж из Excel или CSV в OneBase не загружаются — единственный источник фактов продаж — Linko.",
        "История остатков не загружается (в Linko есть balances_by_date): остатки — только текущий снимок.",
        "Удаление заказов в Linko не передаётся (Linko не отдаёт признак удаления) — удалённый в Linko заказ остаётся до ручной очистки; отмена (статус) учитывается.",
        "Планы регионов — только в кг (Google Sheets); денежный план есть только у ТП (Linko).",
        "Связи строк заказа с акциями Linko нет (API её не отдаёт).",
        "Территории Linko (borders) ни с точками, ни с заказами не связаны — измерение «территория» недоступно.",
        "Себестоимости продукции в OneBase нет — маржинальность по продажам не считается (P&L дилеров — отдельный источник).",
    ];

    /// <summary>Паспорта показателей движка: реестр + профиль + диапазон данных и свежесть источника + совместимые факты + доступ пользователя.</summary>
    public async Task<IReadOnlyList<MetricPassport>> PassportsAsync(QueryCaller caller, CancellationToken ct)
    {
        var view = await ViewAsync(caller, ct);
        var facts = view.Facts.ToDictionary(f => f.Code);
        var principal = caller.UserId is { } id ? await access.ResolveAsync(id, ct) : AnalytikPrincipal.SystemPrincipal;
        var list = new List<MetricPassport>();
        foreach (var m in SemanticCatalog.Metrics)
        {
            var p = MetricPassports.Profile(m);
            var mFacts = SemanticCatalog.FactsOf([m]);
            var fact = facts.GetValueOrDefault(m.Fact);
            var dims = SemanticCatalog.Dimensions.Where(d => mFacts.All(f => d.Facts.Contains(f))).Select(d => d.Code).ToList();
            var compatible = facts.Keys.Where(f => f != m.Fact && f != SemanticCatalog.Stock && m.Fact != SemanticCatalog.Stock
                                                   && SemanticCatalog.Dimensions.Any(d => d.Code != "source" && d.Facts.Contains(f) && d.Facts.Contains(m.Fact))).ToList();
            list.Add(new MetricPassport(m.Code, m.Fact, m.Title, p.Meaning, m.Formula, m.Source, p.Tables, p.Field, p.DataType, m.Unit, p.Aggregation, p.Additive,
                m.Calculated, p.PartOf, p.DependsOn ?? [], fact?.Grains ?? [], dims, compatible, fact?.First, fact?.Last, fact?.SyncedAt, fact?.Status ?? "missing",
                m.Note ?? fact?.Limits, m.Permission, principal.Has(m.Permission) && (fact?.Allowed ?? false)));
        }

        return list;
    }

    /// <summary>
    /// Карта показателей отделов с живым состоянием: available — в таблицах-источниках есть строки (оценка каталога базы), empty — таблицы
    /// пусты, missing — источника нет в OneBase.
    /// </summary>
    public async Task<IReadOnlyList<BusinessMetricState>> BusinessAsync(QueryCaller caller, CancellationToken ct)
    {
        var principal = caller.UserId is { } id ? await access.ResolveAsync(id, ct) : AnalytikPrincipal.SystemPrincipal;
        var tables = (await adb.CatalogTables.AsNoTracking().Select(t => new { t.Schema, t.Name, t.RowEstimate }).ToListAsync(ct))
            .GroupBy(t => $"{t.Schema}.{t.Name}", StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Max(t => t.RowEstimate), StringComparer.OrdinalIgnoreCase);
        var list = new List<BusinessMetricState>();
        foreach (var b in MetricPassports.Business)
        {
            // Через запятую — нужны все таблицы (строк — по самой малой), через «|» — достаточно любой (по самой большой).
            var groups = b.Tables.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(g => g.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(t => t.Split(' ')[0]).Where(t => t.Contains('.')).ToList())
                .Where(g => g.Count > 0).ToList();
            var rows = groups.Count == 0 ? 0 : groups.Min(g => g.Max(t => tables.GetValueOrDefault(t)));
            var status = b.Kind == "missing" ? "missing" : rows > 0 ? "available" : "empty";
            var permission = b.Domain switch
            {
                "Финансы" => Permissions.FinanceRead,
                "HR" => Permissions.HrRead,
                _ => Permissions.SalesRead,
            };
            list.Add(new BusinessMetricState(b, status, rows, principal.Has(permission)));
        }

        return list;
    }

    /// <summary>Совместимость пары показателей для пользователя: его права и фактические диапазоны данных фактов.</summary>
    public async Task<CompatibilityResult> CompatibilityAsync(QueryCaller caller, string metricA, string metricB, IReadOnlyList<string>? dimensions, string? grain,
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var principal = caller.UserId is { } id ? await access.ResolveAsync(id, ct) : AnalytikPrincipal.SystemPrincipal;
        var ranges = new Dictionary<string, (DateOnly? First, DateOnly? Last)>();
        foreach (var fact in new[] { SemanticCatalog.Sales, SemanticCatalog.Plan, SemanticCatalog.Payments, SemanticCatalog.Visits })
        {
            ranges[fact] = await engine.DataRangeAsync(fact, ct);
        }

        return DataCompatibility.Check(metricA, metricB, dimensions, grain, principal, ranges, from, to);
    }

    public async Task<CapabilityView> ViewAsync(QueryCaller caller, CancellationToken ct)
    {
        var principal = caller.UserId is { } id ? await access.ResolveAsync(id, ct) : AnalytikPrincipal.SystemPrincipal;
        var syncs = await engine.SyncTimesAsync(ct);
        var today = engine.Today;
        var facts = new List<CapabilityFact>();

        async Task Add(string code, string title, string source, string detail, string permission, string? syncKey, string? limits)
        {
            var (first, last) = await engine.DataRangeAsync(code, ct);
            var sync = syncKey is null ? (null, null) : syncs.GetValueOrDefault(syncKey);
            var status = last is null ? "missing"
                : sync.Error is { Length: > 0 } ? "error"
                : code is SemanticCatalog.Sales or SemanticCatalog.Payments or SemanticCatalog.Visits && today.DayNumber - last.Value.DayNumber > 2 ? "stale"
                : "ok";
            var metrics = SemanticCatalog.Metrics.Where(m => m.Fact == code).Select(m => m.Code).ToList();
            var dims = SemanticCatalog.Dimensions.Where(d => d.Facts.Contains(code)).Select(d => d.Code).ToList();
            var grains = code switch
            {
                SemanticCatalog.Plan => new[] { "month", "quarter", "year" },
                SemanticCatalog.Stock => [],
                _ => SemanticCatalog.Grains.ToArray(),
            };
            facts.Add(new CapabilityFact(code, title, source, detail, metrics, dims, grains, first, last, sync.Item1, status, limits,
                principal.Has(permission) && !(code == SemanticCatalog.Stock && principal.Sales.Restricted)));
        }

        await Add(SemanticCatalog.Sales, "Продажи (вторичка, «Завод»)", "Linko", "Строка документа, день (дата приёмки заказа / создания возврата)",
            Permissions.SalesRead, "orders", "Факты продаж — только Linko; заказы не в сумах в выручку не входят (курса нет)");
        await Add(SemanticCatalog.Plan, "План продаж", "Google Sheets / AI Data Manager (регионы), Linko (ТП)", "Месяц: регион × категория (кг) или ТП (кг, сумма)",
            Permissions.SalesRead, "staff_balance", "Только помесячно; по дням и неделям не делится");
        await Add(SemanticCatalog.Payments, "Оплаты точек", "Linko", "Оплата, день", Permissions.FinanceRead, "payments", "Без товаров; регион — по филиалу точки");
        await Add(SemanticCatalog.Visits, "Визиты ТП", "Linko", "Визит: день, ТП, точка, в маршруте или нет, статус", Permissions.SalesRead, "visits",
            "Регион — по филиалу точки; план визитов — без сегодняшних и будущих невыполненных; визит с заказом — заказ ТП в точке в день визита");
        await Add(SemanticCatalog.Stock, "Остатки готовой продукции", "Linko", "Товар × склад, текущий снимок", Permissions.SalesRead, "product_balances",
            "Истории нет; в единицах товара");

        var runs = await db.LinkoSyncRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(10)
            .Select(r => new SyncRunInfo(r.Mode, r.StartedAt, r.FinishedAt, r.Status, r.Rows, r.EarliestChanged, r.Error))
            .ToListAsync(ct);
        return new CapabilityView(SemanticCatalog.Metrics, SemanticCatalog.Dimensions, SemanticCatalog.Grains, facts, OtherTools, Missing, runs, clock.GetUtcNow());
    }
}

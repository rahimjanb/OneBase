using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.Stock;
using OneBase.Application.Security;
using OneBase.Domain.Sales;

namespace OneBase.Api.Controllers;

/// <summary>
/// Аналитика продаж по уровням drill-down: старт → республика → РМ → регион → агент.
/// Все данные — из нашей БД (синхронизированной с Linko).
/// Общие параметры: year, month (по умолчанию — месяц последних данных). Планы — только из Linko.
/// </summary>
[ApiController]
[Route("api/sales")]
[HasPermission(Permissions.SalesRead)]
public sealed class SalesController(SalesDataLoader loader) : ControllerBase
{
    public sealed record PeriodQuery(int? Year, int? Month);

    [HttpGet("months")]
    public Task<IReadOnlyList<SalesMonth>> Months(CancellationToken ct) => loader.MonthsAsync(ct);

    [HttpGet("overview")]
    public async Task<OverviewView> Overview([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await Load(q, ct)).CachedOverview();

    /// <summary>from/to — диапазон дней для календаря визитов (по умолчанию с 1-го по последний день данных).</summary>
    [HttpGet("republic")]
    public async Task<GroupView> Republic([FromQuery] PeriodQuery q, [FromQuery] int? from, [FromQuery] int? to, CancellationToken ct) =>
        (await Load(q, ct)).CachedRepublic(from, to);

    [HttpGet("directions/{id}")]
    public async Task<ActionResult<GroupView>> Direction(string id, [FromQuery] PeriodQuery q, [FromQuery] int? from, [FromQuery] int? to, CancellationToken ct)
    {
        var analytics = await Load(q, ct);
        return analytics.HasDirection(id) ? analytics.CachedDirection(id, from, to) : NotFound();
    }

    /// <summary>metric — календарь месяца: kg | sum | akb; category — для АКБ по категории.</summary>
    [HttpGet("regions/{id:guid}")]
    public async Task<ActionResult<RegionView>> Region(
        Guid id,
        [FromQuery] PeriodQuery q,
        [FromQuery] int? from,
        [FromQuery] int? to,
        [FromQuery] string metric = "kg",
        [FromQuery] long? category = null,
        CancellationToken ct = default)
    {
        var analytics = await Load(q, ct);
        metric = metric is "sum" or "akb" ? metric : "kg";
        return analytics.HasRegion(id) ? analytics.CachedRegion(id, from, to, metric, metric == "akb" ? category : null) : NotFound();
    }

    [HttpGet("agents/{id:long}")]
    public async Task<ActionResult<AgentView>> Agent(long id, [FromQuery] PeriodQuery q, CancellationToken ct)
    {
        var analytics = await Load(q, ct);
        return analytics.HasAgent(id) ? analytics.CachedAgent(id) : NotFound();
    }

    /// <summary>Все планы из Linko (staff_balance) за месяц: агенты по регионам и планы команд (супервайзеров).</summary>
    [HttpGet("plans")]
    public async Task<PlansView> Plans([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await Load(q, ct)).CachedPlans();

    [HttpGet("problems")]
    public async Task<ProblemsView> Problems(
        [FromQuery] PeriodQuery q,
        [FromQuery] string? direction,
        [FromQuery] FlagKind? criterion,
        [FromQuery] bool vacancies = false,
        CancellationToken ct = default) =>
        (await Load(q, ct)).CachedProblems(string.IsNullOrEmpty(direction) ? null : direction, criterion, vacancies);

    /// <summary>Магазин за месяц; agent — только продажи этого ТП в магазин.</summary>
    [HttpGet("stores/{id:long}")]
    public async Task<ActionResult<StoreView>> Store(long id, [FromQuery] PeriodQuery q, [FromQuery] long? agent, CancellationToken ct)
    {
        var analytics = await Load(q, ct);
        return analytics.HasMarket(id) ? analytics.CachedStore(id, agent) : NotFound();
    }

    /// <summary>Экспорт и опт — филиал «Завод»: отдельно от вторички.</summary>
    [HttpGet("export")]
    public async Task<ExportView> Export([FromQuery] PeriodQuery q, CancellationToken ct) => (await Load(q, ct)).CachedExport();

    /// <summary>Вкладка «Ассортимент»: республика, направление (direction) или регион (region).</summary>
    [HttpGet("assortment")]
    public async Task<AssortmentView> Assortment([FromQuery] PeriodQuery q, [FromQuery] string? direction, [FromQuery] Guid? region, CancellationToken ct) =>
        (await Load(q, ct)).CachedAssortment(string.IsNullOrEmpty(direction) ? null : direction, region);

    /// <summary>
    /// Категория отчёта (id карточки) в охвате: республика, направление (direction), регион (region), ТП (agent) или экспорт (export=true).
    /// </summary>
    [HttpGet("categories/{id}")]
    public async Task<ActionResult<CategoryView>> Category(
        string id,
        [FromQuery] PeriodQuery q,
        [FromQuery] string? direction,
        [FromQuery] Guid? region,
        [FromQuery] long? agent,
        [FromQuery] bool export = false,
        CancellationToken ct = default)
    {
        var view = (await Load(q, ct)).CachedCategory(id, Scope(direction, region, agent, export));
        return view is null ? NotFound() : view;
    }

    /// <summary>Артикул в охвате (как у категории): где товар идёт, а где нет — по регионам, ТП региона или магазинам.</summary>
    [HttpGet("products/{id:long}")]
    public async Task<ActionResult<ProductView>> Product(
        long id,
        [FromQuery] PeriodQuery q,
        [FromQuery] string? direction,
        [FromQuery] Guid? region,
        [FromQuery] long? agent,
        [FromQuery] bool export = false,
        CancellationToken ct = default)
    {
        var view = (await Load(q, ct)).CachedProduct(id, Scope(direction, region, agent, export));
        return view is null ? NotFound() : view;
    }

    /// <summary>Рекомендуемый остаток: остатки Linko (штуки → кг → коробки) по складам регионов и скорость продаж.</summary>
    [HttpGet("stock")]
    public Task<StockView> Stock([FromServices] StockService stock, [FromQuery] string? region, CancellationToken ct) =>
        stock.GetAsync(string.IsNullOrEmpty(region) ? null : region, ct);

    /// <summary>Аутсток: дни без товара у дилеров за месяц и упущенные продажи — по восстановленному назад от снимка остатку.</summary>
    [HttpGet("outstock")]
    public Task<OutstockView> Outstock([FromServices] OutstockService outstock, [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? region, CancellationToken ct) =>
        outstock.GetAsync(year, month, string.IsNullOrEmpty(region) ? null : region, ct);

    /// <summary>Первичка: отгрузки завода дилерам (перемещения со склада завода) за месяц и год.</summary>
    [HttpGet("primary")]
    public Task<PrimaryView> Primary([FromServices] PrimaryService primary, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) =>
        primary.GetAsync(year, month, ct);

    /// <summary>
    /// Первичка → Экспорт: заказы филиала «Завод» экспортным точкам (тип EXPORT) за месяц и год, по странам и дням.
    /// </summary>
    [HttpGet("primary/export")]
    public Task<PrimaryView> PrimaryExport([FromServices] PrimaryService primary, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) =>
        primary.GetExportAsync(year, month, ct);

    private Task<SalesAnalytics> Load(PeriodQuery q, CancellationToken ct) => loader.LoadAsync(q.Year, q.Month, ct);

    private static AssortmentScope Scope(string? direction, Guid? region, long? agent, bool export) =>
        new(string.IsNullOrEmpty(direction) ? null : direction, region, agent, export);
}

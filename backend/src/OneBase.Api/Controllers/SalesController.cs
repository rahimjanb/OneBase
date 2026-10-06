using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.SkuSales;
using OneBase.Application.Sales.Stock;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>
/// Аналитика продаж по уровням drill-down: старт → республика → РМ → регион → агент.
/// Все данные — из нашей БД (синхронизированной с Linko).
/// Общие параметры: year, month (по умолчанию — месяц последних данных), plan=rop|factory — «План РОП» (по умолчанию) или
/// «План «Завод»» для страниц с планом подразделений; на месяц без планов регионов этого вида — планы ТП из Linko.
/// plan — строка: «factory» без учёта регистра — «Завод», любое другое значение — РОП, без ошибки 400 (SalesPlans.Parse).
/// </summary>
[ApiController]
[Route("api/sales")]
[HasPermission(Permissions.SalesRead)]
public sealed class SalesController(SalesDataLoader loader) : ControllerBase
{
    public sealed record PeriodQuery(int? Year, int? Month, string? Plan = null);

    [HttpGet("months")]
    public Task<IReadOnlyList<SalesMonth>> Months(CancellationToken ct) => loader.MonthsAsync(ct);

    [HttpGet("overview")]
    public async Task<OverviewView> Overview([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await Load(q, ct)).CachedOverview();

    /// <summary>
    /// from/to — диапазон дней для календаря визитов (по умолчанию с 1-го по последний день данных; from=to — один день);
    /// metric и category — календарь месяца по регионам, как у региона.
    /// </summary>
    [HttpGet("republic")]
    public async Task<GroupView> Republic(
        [FromQuery] PeriodQuery q,
        [FromQuery] int? from,
        [FromQuery] int? to,
        [FromQuery] string metric = "kg",
        [FromQuery] long? category = null,
        CancellationToken ct = default)
    {
        metric = metric is "sum" or "akb" ? metric : "kg";
        return (await Load(q, ct)).CachedRepublic(from, to, metric, metric == "akb" ? category : null);
    }

    [HttpGet("directions/{id}")]
    public async Task<ActionResult<GroupView>> Direction(
        string id,
        [FromQuery] PeriodQuery q,
        [FromQuery] int? from,
        [FromQuery] int? to,
        [FromQuery] string metric = "kg",
        [FromQuery] long? category = null,
        CancellationToken ct = default)
    {
        var analytics = await Load(q, ct);
        metric = metric is "sum" or "akb" ? metric : "kg";
        return analytics.HasDirection(id) ? analytics.CachedDirection(id, from, to, metric, metric == "akb" ? category : null) : NotFound();
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
        var analytics = await LoadDefaultPlan(q, ct);
        return analytics.HasAgent(id) ? analytics.CachedAgent(id) : NotFound();
    }

    /// <summary>Все планы из Linko (staff_balance) за месяц: агенты по регионам и планы команд (супервайзеров).</summary>
    [HttpGet("plans")]
    public async Task<PlansView> Plans([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await LoadDefaultPlan(q, ct)).CachedPlans();

    [HttpGet("problems")]
    public async Task<ProblemsView> Problems(
        [FromQuery] PeriodQuery q,
        [FromQuery] string? direction,
        [FromQuery] FlagKind? criterion,
        [FromQuery] bool vacancies = false,
        CancellationToken ct = default) =>
        (await LoadDefaultPlan(q, ct)).CachedProblems(string.IsNullOrEmpty(direction) ? null : direction, criterion, vacancies);

    /// <summary>Магазин за месяц; agent — только продажи этого ТП в магазин.</summary>
    [HttpGet("stores/{id:long}")]
    public async Task<ActionResult<StoreView>> Store(long id, [FromQuery] PeriodQuery q, [FromQuery] long? agent, CancellationToken ct)
    {
        var analytics = await LoadDefaultPlan(q, ct);
        return analytics.HasMarket(id) ? analytics.CachedStore(id, agent) : NotFound();
    }

    /// <summary>Экспорт и опт — филиал «Завод»: отдельно от вторички.</summary>
    [HttpGet("export")]
    public async Task<ExportView> Export([FromQuery] PeriodQuery q, CancellationToken ct) => (await LoadDefaultPlan(q, ct)).CachedExport();

    /// <summary>Вкладка «Ассортимент»: республика, направление (direction) или регион (region).</summary>
    [HttpGet("assortment")]
    public async Task<AssortmentView> Assortment([FromQuery] PeriodQuery q, [FromQuery] string? direction, [FromQuery] Guid? region, CancellationToken ct) =>
        (await LoadDefaultPlan(q, ct)).CachedAssortment(string.IsNullOrEmpty(direction) ? null : direction, region);

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
        var view = (await LoadDefaultPlan(q, ct)).CachedCategory(id, Scope(direction, region, agent, export));
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
        var view = (await LoadDefaultPlan(q, ct)).CachedProduct(id, Scope(direction, region, agent, export));
        return view is null ? NotFound() : view;
    }

    /// <summary>
    /// «Продажи по SKU» за отрезок месяцев: from, to — «ГГГГ-ММ» (по умолчанию — с января года последних данных по их месяц; не длиннее
    /// 24 месяцев, from не позже to), region — id региона (пусто — вся страна), top=all|only|not, abc=all|A|B|C, cat — категории отчёта
    /// через запятую («Кекс,Помадка»). Ошибка в параметрах — 400 с текстом для пользователя.
    /// </summary>
    [HttpGet("sku-sales")]
    [HasPermission(Permissions.SalesRead)]
    public async Task<ActionResult<SkuSalesView>> SkuSales(
        [FromServices] SkuSalesService skuSales,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? region,
        [FromQuery] string? top,
        [FromQuery] string? abc,
        [FromQuery] string? cat,
        CancellationToken ct)
    {
        var result = await skuSales.GetAsync(from, to, region, top, abc, cat, ct);
        return result.View is { } view ? view : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Рекомендуемый остаток: остатки Linko (штуки → кг → коробки) по складам регионов, скорость продаж с поправкой на аутсток и рекомендуемый заказ.
    /// scope — country (по умолчанию) | rm:&lt;id направления&gt; | region:&lt;id&gt; | plant | export (region=&lt;id&gt; — то же, что region:&lt;id&gt;);
    /// q — поиск (цифры — точный код без нулей слева, иначе точный код, затем подстрока); cat — категории через запятую; pack — фасовки, кг,
    /// через запятую; top=all|only|not; status=all|deficit|overstock|dead. Итоги и плитки — по отфильтрованным строкам.
    /// </summary>
    [HttpGet("stock")]
    public Task<StockView> Stock(
        [FromServices] StockService stock,
        [FromQuery] string? region,
        [FromQuery] string? scope,
        [FromQuery] string? q,
        [FromQuery] string? cat,
        [FromQuery] string? pack,
        [FromQuery] string? top,
        [FromQuery] string? status,
        CancellationToken ct) =>
        stock.QueryAsync(new StockQuery(
            string.IsNullOrEmpty(scope) ? (string.IsNullOrEmpty(region) ? null : $"{StockScopes.Region}:{region}") : scope,
            q,
            OutstockService.ParseCategories(cat),
            StockService.ParsePacks(pack),
            top,
            status), ct);

    /// <summary>
    /// Аутсток: дни без товара у дилеров за месяц и упущенные продажи — по восстановленному назад от снимка остатку. Без year/month —
    /// последний закрытый месяц; region — область страницы, calendar — регион календаря по дням (итоги при этом по всей области).
    /// </summary>
    [HttpGet("outstock")]
    public Task<OutstockView> Outstock(
        [FromServices] OutstockService outstock,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string? region,
        [FromQuery] string? scope,
        [FromQuery] string? cat,
        [FromQuery] string? calendar,
        CancellationToken ct) =>
        outstock.GetAsync(new OutstockQuery(year, month, string.IsNullOrEmpty(region) ? null : region, scope, OutstockService.ParseCategories(cat),
            string.IsNullOrEmpty(calendar) ? null : calendar), ct);

    /// <summary>
    /// Первичка: отгрузки завода дилерам (перемещения со склада завода) и точкам завода (заказы базаров, сетей, фирменного магазина) за месяц
    /// и год, нетто возвратов. Календарь отгрузок: from/to — период «с … по …» (дни месяца; по умолчанию все дни с отгрузкой, перепутанные
    /// границы меняются местами), day и dealer — разбор: что отгрузили в этот день и/или этому контрагенту.
    /// </summary>
    [HttpGet("primary")]
    public Task<PrimaryView> Primary(
        [FromServices] PrimaryService primary,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int? from,
        [FromQuery] int? to,
        [FromQuery] int? day,
        [FromQuery] string? dealer,
        CancellationToken ct) =>
        primary.GetAsync(year, month, new PrimaryCalendarQuery(from, to, day, string.IsNullOrEmpty(dealer) ? null : dealer), ct);

    /// <summary>
    /// Первичка → Экспорт: заказы филиала «Завод» экспортным точкам (тип EXPORT) за месяц и год, по странам и дням; from/to/day/dealer —
    /// календарь отгрузок, как у первички (dealer — страна).
    /// </summary>
    [HttpGet("primary/export")]
    public Task<PrimaryView> PrimaryExport(
        [FromServices] PrimaryService primary,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int? from,
        [FromQuery] int? to,
        [FromQuery] int? day,
        [FromQuery] string? dealer,
        CancellationToken ct) =>
        primary.GetExportAsync(year, month, new PrimaryCalendarQuery(from, to, day, string.IsNullOrEmpty(dealer) ? null : dealer), ct);

    /// <summary>Расчёт месяца по выбранному плану — для страниц с планом подразделений: старт, республика, РМ, регион.</summary>
    private Task<SalesAnalytics> Load(PeriodQuery q, CancellationToken ct) => loader.LoadAsync(q.Year, q.Month, SalesPlans.Parse(q.Plan), ct);

    /// <summary>
    /// Страницы без плана подразделений (ТП — его план из Linko, магазин, ассортимент, экспорт, «Проблемные агенты», «Планы»):
    /// расчёт по плану по умолчанию — переключатель плана не строит ради них второй расчёт месяца.
    /// </summary>
    private Task<SalesAnalytics> LoadDefaultPlan(PeriodQuery q, CancellationToken ct) => loader.LoadAsync(q.Year, q.Month, ct);

    private static AssortmentScope Scope(string? direction, Guid? region, long? agent, bool export) =>
        new(string.IsNullOrEmpty(direction) ? null : direction, region, agent, export);
}

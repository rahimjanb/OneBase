using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Access;
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
public sealed class SalesController(SalesDataLoader loader, SalesAccess access) : ControllerBase
{
    public sealed record PeriodQuery(int? Year, int? Month, string? Plan = null);

    [SalesNoData]
    [HttpGet("months")]
    public Task<IReadOnlyList<SalesMonth>> Months(CancellationToken ct) => loader.MonthsAsync(ct);

    [SalesOrganizationOnly]
    [HttpGet("overview")]
    public async Task<OverviewView> Overview([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await Load(q, ct)).CachedOverview();

    /// <summary>
    /// from/to — диапазон дней для календаря визитов (по умолчанию с 1-го по последний день данных; from=to — один день);
    /// metric и category — календарь месяца по регионам, как у региона.
    /// </summary>
    [SalesOrganizationOnly]
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

    [SalesScoped]
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
        // Чужое направление — 404, как будто его нет (РМ видит только свои; СВР и ТП — ни одного).
        if (!(await ScopeAsync(ct)).CanSeeDirection(GuidOf(id)) && !(await ScopeAsync(ct)).IsOrganization)
        {
            return NotFound();
        }

        var analytics = await Load(q, ct);
        metric = metric is "sum" or "akb" ? metric : "kg";
        return analytics.HasDirection(id) ? analytics.CachedDirection(id, from, to, metric, metric == "akb" ? category : null) : NotFound();
    }

    /// <summary>
    /// «Мои регионы»: РМ и супервайзер — все регионы своего охвата одним экраном (у РМ — и регионы его направлений), как страница направления.
    /// Вся организация и ТП — 404: у них своя страница (страна, карточка ТП).
    /// </summary>
    [SalesScoped]
    [HttpGet("my")]
    public async Task<ActionResult<GroupView>> MyRegions(
        [FromQuery] PeriodQuery q,
        [FromQuery] int? from,
        [FromQuery] int? to,
        [FromQuery] string metric = "kg",
        [FromQuery] long? category = null,
        CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        if (!(scope.IsRm || scope.IsSupervisor) || scope.RegionIds.Count == 0)
        {
            return NotFound();
        }

        var analytics = await Load(q, ct);
        metric = metric is "sum" or "akb" ? metric : "kg";
        return analytics.CachedMyRegions(scope.RegionIds.ToList(), from, to, metric, metric == "akb" ? category : null);
    }

    /// <summary>metric — календарь месяца: kg | sum | akb; category — для АКБ по категории.</summary>
    [SalesScoped]
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
        if (!(await ScopeAsync(ct)).CanSeeRegion(id))
        {
            return NotFound();
        }

        var analytics = await Load(q, ct);
        metric = metric is "sum" or "akb" ? metric : "kg";
        return analytics.HasRegion(id) ? analytics.CachedRegion(id, from, to, metric, metric == "akb" ? category : null) : NotFound();
    }

    /// <summary>«Агенты»: все ТП месяца — выполнение плана, прогноз, выручка, АКБ, визиты, страйк и замечания; фильтры и поиск — на странице.</summary>
    [SalesScoped]
    [HttpGet("agents")]
    public async Task<AgentsView> Agents([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await LoadDefaultPlan(q, ct)).CachedAgents((await ScopeAsync(ct)).Visibility);

    [SalesScoped]
    [HttpGet("agents/{id:long}")]
    public async Task<ActionResult<AgentView>> Agent(long id, [FromQuery] PeriodQuery q, CancellationToken ct)
    {
        var analytics = await LoadDefaultPlan(q, ct);
        if (!analytics.HasAgent(id))
        {
            return NotFound();
        }

        // ТП чужого региона (по региону месяца) или другой ТП — 404.
        var view = analytics.CachedAgent(id);
        return (await ScopeAsync(ct)).CanSeeAgent(id, GuidOf(view.RegionId)) ? view : NotFound();
    }

    /// <summary>Все планы из Linko (staff_balance) за месяц: агенты по регионам и планы команд (супервайзеров).</summary>
    [SalesScoped]
    [HttpGet("plans")]
    public async Task<PlansView> Plans([FromQuery] PeriodQuery q, CancellationToken ct) =>
        (await LoadDefaultPlan(q, ct)).CachedPlans((await ScopeAsync(ct)).Visibility);

    /// <summary>
    /// «KPI»: показатели Linko по людям — выполнение, премии и фонд оплаты по регионам и направлениям. Оклад, бонусы и премии — только с
    /// правом «Финансы» (finance.read); без него те же цифры выполнения без денег.
    /// </summary>
    [SalesScoped]
    [HttpGet("kpi")]
    public async Task<KpiView> Kpi(
        [FromQuery] PeriodQuery q,
        [FromServices] OneBase.AI.Security.IUserPermissions permissions,
        CancellationToken ct)
    {
        var money = (await permissions.GetAsync(User.GetUserId(), ct)).Contains(Permissions.FinanceRead);
        return (await LoadDefaultPlan(q, ct)).CachedKpi(money, (await ScopeAsync(ct)).Visibility);
    }

    public sealed record KpiRegionInput(long LinkoUserId, Guid? RegionId);

    /// <summary>
    /// Регион человека для «KPI» и всего раздела продаж (профиль агента): у СВР в Linko филиала нет. RegionId = null — снять привязку,
    /// регион снова определяется сам.
    /// </summary>
    [SalesOrganizationOnly]
    [HttpPut("kpi/region")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IActionResult> KpiRegion(
        [FromBody] KpiRegionInput input,
        [FromServices] OneBase.Application.Abstractions.IAppDbContext db,
        [FromServices] SalesCacheSignal signal,
        CancellationToken ct)
    {
        if (!await db.LinkoUsers.AnyAsync(u => u.Id == input.LinkoUserId, ct))
        {
            return NotFound(new { error = "Пользователь Linko не найден." });
        }

        if (input.RegionId is { } regionId && !await db.SalesRegions.AnyAsync(r => r.Id == regionId, ct))
        {
            return NotFound(new { error = "Регион не найден." });
        }

        var profile = await db.SalesAgentProfiles.FirstOrDefaultAsync(p => p.LinkoUserId == input.LinkoUserId, ct);
        if (profile is null)
        {
            if (input.RegionId is null)
            {
                return NoContent();
            }

            db.SalesAgentProfiles.Add(profile = new OneBase.Domain.Sales.SalesAgentProfile { LinkoUserId = input.LinkoUserId });
        }

        profile.RegionId = input.RegionId;
        await db.SaveChangesAsync(ct);
        signal.Invalidate();
        return NoContent();
    }

    [SalesScoped]
    [HttpGet("problems")]
    public async Task<ProblemsView> Problems(
        [FromQuery] PeriodQuery q,
        [FromQuery] string? direction,
        [FromQuery] FlagKind? criterion,
        [FromQuery] bool vacancies = false,
        CancellationToken ct = default) =>
        (await LoadDefaultPlan(q, ct)).CachedProblems(string.IsNullOrEmpty(direction) ? null : direction, criterion, vacancies, (await ScopeAsync(ct)).Visibility);

    /// <summary>Магазин за месяц; agent — только продажи этого ТП в магазин.</summary>
    [SalesScoped]
    [HttpGet("stores/{id:long}")]
    public async Task<ActionResult<StoreView>> Store(long id, [FromQuery] PeriodQuery q, [FromQuery] long? agent, CancellationToken ct,
        [FromServices] OneBase.Application.Abstractions.IAppDbContext? db = null)
    {
        var analytics = await LoadDefaultPlan(q, ct);
        if (!analytics.HasMarket(id))
        {
            return NotFound();
        }

        var scope = await ScopeAsync(ct);
        if (scope.IsAgent)
        {
            // ТП видит магазин только своими продажами в нём: продавал в этом или прошлом месяце или магазин закреплён за ним в Linko.
            // Чужие продажи в магазине (agent в запросе) и чужие магазины — 404. (У StoreView.AgentId — запрошенный ТП, не признак.)
            var mine = analytics.CachedStore(id, scope.AgentId);
            var sold = mine.Orders > 0 || mine.FactKg != 0 || mine.PrevMonthKg != 0;
            var responsible = db is not null && await db.LinkoMarkets.AnyAsync(m => m.Id == id && m.ResponsibleAgentId == scope.AgentId, ct);
            return sold || responsible ? mine : NotFound();
        }

        var view = analytics.CachedStore(id, agent);
        return scope.CanSeeRegion(GuidOf(view.RegionId)) ? view : NotFound();
    }

    /// <summary>Экспорт и опт — филиал «Завод»: отдельно от вторички.</summary>
    [SalesOrganizationOnly]
    [HttpGet("export")]
    public async Task<ExportView> Export([FromQuery] PeriodQuery q, CancellationToken ct) => (await LoadDefaultPlan(q, ct)).CachedExport();

    /// <summary>Вкладка «Ассортимент»: республика, направление (direction) или регион (region).</summary>
    [SalesScoped]
    [HttpGet("assortment")]
    public async Task<ActionResult<AssortmentView>> Assortment([FromQuery] PeriodQuery q, [FromQuery] string? direction, [FromQuery] Guid? region, CancellationToken ct)
    {
        // Охват РМ/СВР: без параметров — все свои регионы («Мои регионы»; один регион — он сам); страна и чужое — 404. ТП ассортимента
        // региона не видит.
        var scope = await ScopeAsync(ct);
        IReadOnlyList<Guid>? mine = null;
        if (!scope.IsOrganization && string.IsNullOrEmpty(direction) && region is null)
        {
            (region, mine) = OwnRegions(scope);
        }

        if (!AllowedCatalog(scope, direction, region, null, false, mine))
        {
            return NotFound();
        }

        return (await LoadDefaultPlan(q, ct)).CachedAssortment(string.IsNullOrEmpty(direction) ? null : direction, region, mine);
    }

    /// <summary>
    /// Категория отчёта (id карточки) в охвате: республика, направление (direction), регион (region), ТП (agent) или экспорт (export=true).
    /// </summary>
    [SalesScoped]
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
        var analytics = await LoadDefaultPlan(q, ct);
        if (await CatalogScopeAsync(analytics, direction, region, agent, export, ct) is not { } scoped)
        {
            return NotFound();
        }

        var view = analytics.CachedCategory(id, scoped);
        return view is null ? NotFound() : view;
    }

    /// <summary>Артикул в охвате (как у категории): где товар идёт, а где нет — по регионам, ТП региона или магазинам.</summary>
    [SalesScoped]
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
        var analytics = await LoadDefaultPlan(q, ct);
        if (await CatalogScopeAsync(analytics, direction, region, agent, export, ct) is not { } scoped)
        {
            return NotFound();
        }

        var view = analytics.CachedProduct(id, scoped);
        return view is null ? NotFound() : view;
    }

    /// <summary>
    /// «Акт сверки»: взаиморасчёты с контрагентами за месяц (month=2026-9) — отгрузки и возвраты Linko, оплаты и расходы из реестров Google
    /// Sheets, остаток из акта бухгалтерии. Финансовые данные — нужны права «Продажи» и «Финансы».
    /// </summary>
    [SalesOrganizationOnly]
    [HttpGet("akt")]
    [HasPermission(Permissions.FinanceRead)]
    public async Task<OneBase.Application.Sales.Settlements.AktView> Akt(
        [FromServices] OneBase.Application.Sales.Settlements.SettlementsService settlements,
        [FromQuery] string? month,
        CancellationToken ct) =>
        OneBase.Application.Sales.Settlements.AktCompute.Build(await settlements.BaseAsync(ct), month);

    /// <summary>«DSO»: долги дилеров по FIFO, просрочка по норме и прогноз оплат. Права «Продажи» и «Финансы».</summary>
    [SalesOrganizationOnly]
    [HttpGet("dso")]
    [HasPermission(Permissions.FinanceRead)]
    public async Task<OneBase.Application.Sales.Settlements.DsoView> Dso(
        [FromServices] OneBase.Application.Sales.Settlements.SettlementsService settlements,
        CancellationToken ct) =>
        OneBase.Application.Sales.Settlements.DsoCompute.Build(await settlements.BaseAsync(ct));

    /// <summary>
    /// «Акции»: скидки и бонусы вторички по акциям Linko за месяц года (month=2026-9), РМ (rm) и отмеченные акции (promos=12,-1,-1000 —
    /// id акций, -1 — ручная скидка, меньше -1 — группа неразличимых акций). Нет строк со скидкой за год — 404.
    /// </summary>
    [SalesOrganizationOnly]
    [HttpGet("promo")]
    public async Task<ActionResult<OneBase.Application.Sales.Promo.PromoView>> Promo(
        [FromServices] OneBase.Application.Sales.Promo.PromoService promo,
        [FromQuery] string? month,
        [FromQuery] string? rm,
        [FromQuery] string? promos,
        CancellationToken ct)
    {
        var ids = (promos ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => long.TryParse(x, out var id) ? id : (long?)null)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .Take(200)
            .ToList();
        return await promo.GetAsync(month, rm, ids, ct) is { } view ? view : NotFound(new { error = "Строк со скидками за год нет." });
    }

    /// <summary>
    /// «ДМП»: стеллажи в магазинах на дату данных — приход на склады дилеров, остатки, установки в магазины и как магазины заказывают до и
    /// после установки. Товаров-стеллажей (Sales:DmpProductTypes) в копии Linko нет — 404.
    /// </summary>
    /// <summary>
    /// «Система» за месяц (year, month): маршрут — заказы от плана визитов Linko, качество дистрибуции — ядро ассортимента и верхние точки,
    /// деньги и запас дилеров — только с правом «Финансы» (без него Money = null, MoneyHidden = true).
    /// </summary>
    [SalesOrganizationOnly]
    [HttpGet("system")]
    public async Task<OneBase.Application.Sales.Health.SystemView> SystemHealth(
        [FromQuery] PeriodQuery q,
        [FromServices] OneBase.Application.Sales.Health.SystemService system,
        [FromServices] OneBase.AI.Security.IUserPermissions permissions,
        CancellationToken ct)
    {
        var money = (await permissions.GetAsync(User.GetUserId(), ct)).Contains(Permissions.FinanceRead);
        return await system.GetAsync(q.Year, q.Month, money, ct);
    }

    [SalesOrganizationOnly]
    [HttpGet("dmp")]
    public async Task<ActionResult<OneBase.Application.Sales.Dmp.DmpView>> Dmp([FromServices] OneBase.Application.Sales.Dmp.DmpService dmp, CancellationToken ct) =>
        await dmp.GetAsync(ct) is { } view ? view : NotFound(new { error = "Стеллажей в Linko нет: товаров типа «Оборудование» не найдено." });

    /// <summary>
    /// «Продажи по SKU» за отрезок месяцев: from, to — «ГГГГ-ММ» (по умолчанию — с января года последних данных по их месяц; не длиннее
    /// 24 месяцев, from не позже to), region — id региона (пусто — вся страна), top=all|only|not, abc=all|A|B|C, cat — категории отчёта
    /// через запятую («Кекс,Помадка»). Ошибка в параметрах — 400 с текстом для пользователя.
    /// </summary>
    [SalesOrganizationOnly]
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
    [SalesOrganizationOnly]
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
    /// «Собрать в Excel» на «Рек. остатке»: те же охват и фильтры, что у /stock, колонки — columns (ключи StockExport.Columns через запятую,
    /// пусто — как в таблице), warehouses=true — ещё лист «По складам». Имя файла — в пути, чтобы его взяли iOS и браузеры.
    /// </summary>
    [SalesOrganizationOnly]
    [HttpGet("stock/xlsx/{fileName}")]
    [HasPermission(Permissions.SalesExport)]
    public async Task<IActionResult> StockWorkbook(
        [FromServices] StockService stock,
        [FromServices] OneBase.Application.Reports.IReportWorkbookWriter writer,
        string fileName,
        [FromQuery] string? region,
        [FromQuery] string? scope,
        [FromQuery] string? q,
        [FromQuery] string? cat,
        [FromQuery] string? pack,
        [FromQuery] string? top,
        [FromQuery] string? status,
        [FromQuery] string? columns,
        [FromQuery] bool warehouses,
        CancellationToken ct)
    {
        var view = await stock.QueryAsync(new StockQuery(
            string.IsNullOrEmpty(scope) ? (string.IsNullOrEmpty(region) ? null : $"{StockScopes.Region}:{region}") : scope,
            q,
            OutstockService.ParseCategories(cat),
            StockService.ParsePacks(pack),
            top,
            status), ct);
        var content = StockExport.Build(view, StockExport.Parse(columns), warehouses);
        var name = OneBase.AI.Reports.ReportService.FileName(Path.GetFileNameWithoutExtension(fileName));
        return File(writer.Write(content, DateTimeOffset.UtcNow), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    /// <summary>Автозаказ региона на «Рек. остатке»: товары с коробкой и рекомендация в коробках (без фильтров страницы).</summary>
    [SalesOrganizationOnly]
    [HttpGet("stock/autoorder")]
    public async Task<ActionResult<AutoOrderView>> AutoOrder([FromServices] StockService stock, [FromQuery] string? region, CancellationToken ct)
    {
        try
        {
            return await stock.AutoOrderAsync(region, ct);
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { error = e.Message });
        }
    }

    /// <summary>
    /// «Скачать акт»: акт приёма-передачи «Завод → регион» в Excel по коробкам автозаказа (остатков завода в нём нет). Имя файла — в пути,
    /// чтобы его взяли iOS и браузеры.
    /// </summary>
    [SalesOrganizationOnly]
    [HttpPost("stock/autoorder/act/{fileName}")]
    [HasPermission(Permissions.SalesExport)]
    public async Task<IActionResult> AutoOrderAct(
        [FromServices] StockService stock,
        [FromServices] ITransferActWriter writer,
        string fileName,
        [FromBody] AutoOrderActInput input,
        CancellationToken ct)
    {
        try
        {
            var act = await stock.TransferActAsync(input, ct);
            var name = Path.ChangeExtension(OneBase.AI.Reports.ReportService.FileName(Path.GetFileNameWithoutExtension(fileName)), ".xlsx");
            return File(writer.Write(act), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { error = e.Message });
        }
    }

    /// <summary>
    /// Аутсток: дни без товара у дилеров за месяц и упущенные продажи — по восстановленному назад от снимка остатку. Без year/month —
    /// последний закрытый месяц; region — область страницы, calendar — регион календаря по дням (итоги при этом по всей области).
    /// </summary>
    [SalesOrganizationOnly]
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
    [SalesOrganizationOnly]
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
    [SalesOrganizationOnly]
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

    /// <summary>
    /// Обзор «Первички»: отгрузка завода всего и два направления (республика, экспорт) за период — period=month (year, month), quarter (year,
    /// quarter) или ytd (с начала года, по умолчанию); тонны, сумма, доли по тоннам и по сумме, контрагенты, страны, заказы, сумма за тонну.
    /// </summary>
    [SalesOrganizationOnly]
    [HttpGet("primary/overview")]
    public Task<PrimaryOverview> PrimaryOverview(
        [FromServices] PrimaryService primary,
        [FromQuery] string? period,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int? quarter,
        CancellationToken ct) =>
        primary.OverviewAsync(period, year, month, quarter, ct);

    /// <summary>«Excel» обзора «Первички» за тот же период: направления и по месяцам. Имя файла — в пути (iOS и браузеры берут его оттуда).</summary>
    [SalesOrganizationOnly]
    [HttpGet("primary/overview/xlsx/{fileName}")]
    [HasPermission(Permissions.SalesExport)]
    public async Task<IActionResult> PrimaryOverviewWorkbook(
        [FromServices] PrimaryService primary,
        [FromServices] OneBase.Application.Reports.IReportWorkbookWriter writer,
        string fileName,
        [FromQuery] string? period,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int? quarter,
        CancellationToken ct)
    {
        var view = await primary.OverviewAsync(period, year, month, quarter, ct);
        var name = OneBase.AI.Reports.ReportService.FileName(Path.GetFileNameWithoutExtension(fileName));
        return File(writer.Write(PrimaryOverviewExport.Build(view), DateTimeOffset.UtcNow), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    /// <summary>Расчёт месяца по выбранному плану — для страниц с планом подразделений: старт, республика, РМ, регион.</summary>
    private Task<SalesAnalytics> Load(PeriodQuery q, CancellationToken ct) => loader.LoadAsync(q.Year, q.Month, SalesPlans.Parse(q.Plan), ct);

    /// <summary>
    /// Страницы без плана подразделений (ТП — его план из Linko, магазин, ассортимент, экспорт, «Проблемные агенты», «Планы»):
    /// расчёт по плану по умолчанию — переключатель плана не строит ради них второй расчёт месяца.
    /// </summary>
    private Task<SalesAnalytics> LoadDefaultPlan(PeriodQuery q, CancellationToken ct) => loader.LoadAsync(q.Year, q.Month, ct);

    private static AssortmentScope Scope(string? direction, Guid? region, long? agent, bool export, IReadOnlyList<Guid>? regions = null) =>
        new(string.IsNullOrEmpty(direction) ? null : direction, region, agent, export, regions);

    /// <summary>Свои регионы РМ и СВР без выбора: один — он сам (регион), несколько — «Мои регионы»; ТП и вся организация — ничего.</summary>
    private static (Guid? Region, IReadOnlyList<Guid>? Regions) OwnRegions(SalesScope scope)
    {
        if (!(scope.IsRm || scope.IsSupervisor) || scope.RegionIds.Count == 0)
        {
            return (scope.IsAgent || scope.IsOrganization ? null : Guid.Empty, null);
        }

        return scope.RegionIds.Count == 1 ? (scope.RegionIds.First(), null) : (null, scope.RegionIds.Order().ToList());
    }

    private SalesScope? _scope;

    /// <summary>Охват текущего пользователя (из назначения и оргструктуры, не из запроса) — один раз на запрос.</summary>
    private async Task<SalesScope> ScopeAsync(CancellationToken ct) => _scope ??= await access.ScopeAsync(User.GetUserId(), ct);

    private static Guid? GuidOf(string? id) => Guid.TryParse(id, out var g) ? g : null;

    /// <summary>
    /// Охват категории и товара: вся организация — как запрошено; РМ — своё направление, свой регион или ТП своих регионов; СВР — свой регион
    /// или его ТП; ТП — только он сам. Без параметров у ограниченного охвата — своё (направление, регион, ТП); экспорт «Завода» и страна — нет.
    /// null — охват не разрешён (404).
    /// </summary>
    private async Task<AssortmentScope?> CatalogScopeAsync(SalesAnalytics analytics, string? direction, Guid? region, long? agent, bool export, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        IReadOnlyList<Guid>? mine = null;
        if (!scope.IsOrganization && string.IsNullOrEmpty(direction) && region is null && agent is null && !export)
        {
            if (scope.IsAgent) agent = scope.AgentId;
            else (region, mine) = OwnRegions(scope);
        }

        var agentRegion = agent is { } a && analytics.HasAgent(a) ? GuidOf(analytics.CachedAgent(a).RegionId) : null;
        return AllowedCatalog(scope, direction, region, agent is null ? null : (agent.Value, agentRegion), export, mine) ? Scope(direction, region, agent, export, mine) : null;
    }

    private static bool AllowedCatalog(SalesScope scope, string? direction, Guid? region, (long Id, Guid? Region)? agent, bool export, IReadOnlyList<Guid>? regions = null)
    {
        if (scope.IsOrganization)
        {
            return true;
        }

        if (export)
        {
            return false;
        }

        if (regions is { Count: > 0 })
        {
            return regions.All(r => scope.CanSeeRegion(r));
        }

        if (agent is { } a)
        {
            return scope.CanSeeAgent(a.Id, a.Region);
        }

        if (region is { } r)
        {
            return scope.CanSeeRegion(r);
        }

        return !string.IsNullOrEmpty(direction) && scope.CanSeeDirection(GuidOf(direction));
    }
}

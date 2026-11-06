using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Procurement;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.Promo;
using OneBase.Application.Sales.Settlements;
using OneBase.Application.Sales.Stock;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.AI;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Twin;

/// <summary>
/// Цифровой двойник (Operational Twin): снимок предприятия для 3D-модели территории. Своих расчётов нет — каждый показатель берётся тем же
/// методом сервиса раздела, что и страница раздела, с контекстом пользователя (WorkContext, HrContext, ProcurementContext, права из токена):
/// что пользователь не видит в разделе, того нет и в двойнике (блок — noaccess). Источники собираются параллельно, каждый в своём scope
/// (DbContext не потокобезопасен) и в своём try: сбой одного — error у этого блока, остальные работают.
/// </summary>
public sealed partial class TwinService(IAppDbContext db, IServiceScopeFactory scopes, IAuditLogger audit, SalesOptions salesOptions, ILogger<TwinService> logger)
{
    /// <summary>Сколько открытых заявок разбирать по отделам (страницы по 200): больше — у компании другой масштаб, считаем первые.</summary>
    private const int MaxRequestPages = 25;

    private const string RawMaterialsCode = "raw_materials";

    private static readonly ProcurementRequestStatus[] ApprovalStatuses =
    [
        ProcurementRequestStatus.Submitted, ProcurementRequestStatus.HeadApproval, ProcurementRequestStatus.FinanceApproval, ProcurementRequestStatus.DirectorApproval,
    ];

    private sealed record Dept(Guid Id, string Code, string Name);

    private sealed record TasksResult(TwinPart<TwinTasks> Total, IReadOnlyDictionary<string, TwinPart<TwinDeptTasks>> ByDept);

    private sealed record HrResult(TwinPart<TwinHr> Total, IReadOnlyDictionary<string, TwinPart<TwinDeptHr>> ByDept);

    private sealed record ProcurementResult(
        TwinPart<TwinSupply> Supply,
        TwinPart<TwinStock> Stock,
        IReadOnlyDictionary<string, TwinDeptSupply> ByDept,
        TwinPart<IReadOnlyDictionary<string, TwinDeptSpend>> Spend);

    public async Task<TwinSnapshot> SnapshotAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var departments = await DepartmentsAsync(ct);
        var byName = departments.GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Code, StringComparer.OrdinalIgnoreCase);

        var tasks = Guard("tasks", sp => TasksAsync(sp, userId, permissions, ct), ct);
        var hr = Guard("hr", sp => HrAsync(sp, userId, permissions, departments, ct), ct);
        var procurement = Guard("procurement", sp => ProcurementAsync(sp, userId, permissions, byName, ct), ct);
        // Расчёты продаж (вторичка, первичка, остатки, дебиторка, акции) — по очереди: без кэша каждый — тяжёлый запрос к зеркалу Linko,
        // пять сразу упираются в общую память PostgreSQL (53100, как и прогрев кэша — не больше двух расчётов разом). Остальное — параллельно.
        var salesSide = SalesSideAsync(permissions, ct);

        // Свои запросы — в основном scope, пока источники собираются в своих.
        var objects = await ObjectsAsync(ct);
        var alerts = permissions.Contains(Permissions.AgentsRun) ? await AlertsAsync(permissions, ct) : TwinPart<TwinAlerts>.NoAccess;
        var users = permissions.Contains(Permissions.UsersManage)
            ? await db.Users.AsNoTracking().Where(u => u.IsActive && u.DepartmentId != null).GroupBy(u => u.DepartmentId!.Value)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            : null;

        var (tasksState, tasksResult) = await tasks;
        var (hrState, hrResult) = await hr;
        var (procurementState, procurementResult) = await procurement;
        var (sales, primary, plant, finance, promo) = await salesSide;

        var views = departments.Select(d => new TwinDepartmentView(
                d.Id,
                d.Code,
                d.Name,
                tasksResult?.ByDept.GetValueOrDefault(d.Code) ?? Fallback<TwinDeptTasks>(tasksState),
                hrResult?.ByDept.GetValueOrDefault(d.Code) ?? Fallback<TwinDeptHr>(hrState),
                procurementResult is null
                    ? Fallback<TwinDeptSupply>(procurementState)
                    : procurementResult.Supply.State != TwinStates.Ok
                        ? new TwinPart<TwinDeptSupply>(procurementResult.Supply.State, null)
                        : TwinPart<TwinDeptSupply>.Of(procurementResult.ByDept.GetValueOrDefault(d.Code)
                                                      ?? new TwinDeptSupply(0, 0, 0, 0, 0, procurementResult.Supply.Data!.OverdueToBuy is null ? null : 0, 0)),
                procurementResult is null
                    ? Fallback<TwinDeptSpend>(procurementState)
                    : procurementResult.Spend.Data is { } spend
                        ? TwinPart<TwinDeptSpend>.Of(spend.GetValueOrDefault(d.Code) ?? new TwinDeptSpend(0, 0))
                        : new TwinPart<TwinDeptSpend>(procurementResult.Spend.State, null),
                users is null ? null : users.GetValueOrDefault(d.Id)))
            .ToList();

        return new TwinSnapshot(
            DateTimeOffset.UtcNow,
            permissions.Contains(Permissions.UsersManage),
            objects,
            views,
            sales,
            primary,
            hrResult?.Total ?? Fallback<TwinHr>(hrState),
            tasksResult?.Total ?? Fallback<TwinTasks>(tasksState),
            procurementResult?.Supply ?? Fallback<TwinSupply>(procurementState),
            procurementResult?.Stock ?? Fallback<TwinStock>(procurementState),
            plant,
            finance,
            promo,
            alerts);
    }

    private async Task<(TwinPart<TwinSales>, TwinPart<TwinPrimary>, TwinPart<TwinPlant>, TwinPart<TwinFinance>, TwinPart<TwinPromo>)> SalesSideAsync(
        IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var sales = await Part(Permissions.SalesRead, permissions, "sales", sp => SalesAsync(sp, ct), ct);
        var primary = await Part(Permissions.SalesRead, permissions, "primary", sp => PrimaryAsync(sp, ct), ct);
        var plant = await Part(Permissions.SalesRead, permissions, "plant", sp => PlantAsync(sp, ct), ct);
        var finance = permissions.Contains(Permissions.SalesRead)
            ? await Part(Permissions.FinanceRead, permissions, "finance", sp => FinanceAsync(sp, ct), ct)
            : TwinPart<TwinFinance>.NoAccess;
        var promo = await Part(Permissions.SalesRead, permissions, "promo", sp => PromoAsync(sp, ct), ct);
        return (sales, primary, plant, finance, promo);
    }

    private static TwinPart<T> Fallback<T>(string state) where T : class => new(state == TwinStates.Ok ? TwinStates.NoData : state, null);

    /// <summary>Источник в своём scope: нет права в сервисе раздела — noaccess, сбой — error (в журнал), отмена запроса — дальше.</summary>
    private async Task<(string State, T? Value)> Guard<T>(string source, Func<IServiceProvider, Task<T>> load, CancellationToken ct) where T : class
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return (TwinStates.Ok, await load(scope.ServiceProvider));
        }
        catch (FieldForbiddenException)
        {
            return (TwinStates.NoAccess, null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Цифровой двойник: источник {Source} не ответил", source);
            return (TwinStates.Error, null);
        }
    }

    private async Task<TwinPart<T>> Part<T>(string permission, IReadOnlySet<string> permissions, string source, Func<IServiceProvider, Task<TwinPart<T>>> load,
        CancellationToken ct) where T : class
    {
        if (!permissions.Contains(permission))
        {
            return TwinPart<T>.NoAccess;
        }

        var (state, part) = await Guard(source, load, ct);
        return part ?? new TwinPart<T>(state, null);
    }

    private async Task<List<Dept>> DepartmentsAsync(CancellationToken ct)
    {
        var list = await db.Departments.AsNoTracking().Select(d => new Dept(d.Id, d.Code, d.Name)).ToListAsync(ct);
        return list.OrderBy(d => Array.IndexOf(WorkTaskService.Order, d.Code) is var i and >= 0 ? i : WorkTaskService.Order.Length).ThenBy(d => d.Name).ToList();
    }

    // ---------- Задачи ----------

    private static async Task<TasksResult> TasksAsync(IServiceProvider sp, Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        if (!permissions.Contains(Permissions.WorkTasks))
        {
            throw new FieldForbiddenException("Задачи — с правом «Задачи».");
        }

        var tasks = sp.GetRequiredService<WorkTaskService>();
        var ctx = await tasks.ContextAsync(userId, permissions, ct);
        var departments = await tasks.DepartmentsAsync(ctx, ct);
        var overview = await tasks.OverviewAsync(ctx, 0, ct);
        // Разбивка обзора — по открытым задачам: отдела без открытых в ней нет (значит 0); у отдела, которого пользователь не видит, в ней
        // могут быть только его собственные задачи — такие отделы не показываем (как «Задачи» раздела).
        var groups = overview.Departments.ToDictionary(d => d.Code);
        var byDept = departments.ToDictionary(
            d => d.Code,
            d => d.HasTasks
                ? TwinPart<TwinDeptTasks>.Of(groups.GetValueOrDefault(d.Code) is { } g
                    ? new TwinDeptTasks(d.OpenTasks, d.OverdueTasks, g.Review, g.Blocked)
                    : new TwinDeptTasks(d.OpenTasks, d.OverdueTasks, 0, 0))
                : TwinPart<TwinDeptTasks>.NoAccess);
        return new TasksResult(
            TwinPart<TwinTasks>.Of(new TwinTasks(overview.Open, overview.Overdue, overview.Review, overview.Blocked, overview.DueThisWeek, overview.DoneThisMonth)),
            byDept);
    }

    // ---------- HR ----------

    private static async Task<HrResult> HrAsync(IServiceProvider sp, Guid userId, IReadOnlySet<string> permissions, IReadOnlyList<Dept> departments,
        CancellationToken ct)
    {
        var hr = sp.GetRequiredService<HrService>();
        var ctx = await hr.ContextAsync(userId, permissions, ct);
        if (!ctx.HasAccess)
        {
            throw new FieldForbiddenException("Нет доступа к HR.");
        }

        var sync = await hr.SyncBriefAsync(ct);
        if (!sync.Configured)
        {
            var none = departments.ToDictionary(d => d.Code, _ => TwinPart<TwinDeptHr>.NoData);
            return new HrResult(TwinPart<TwinHr>.NoData, none);
        }

        var overview = await hr.OverviewAsync(ctx, null, ct);
        var divisions = await hr.DivisionsAsync(ctx, ct);
        var departmentOf = DivisionDepartments(divisions);

        var byDept = new Dictionary<string, TwinPart<TwinDeptHr>>();
        foreach (var d in departments)
        {
            var linked = divisions.Count(x => departmentOf.GetValueOrDefault(x.Id) == d.Code);
            // Руководитель видит только подразделения своих отделов: чужой отдел для него — noaccess, а не «нет данных».
            if (!ctx.SeesAll && !ctx.HeadOf.Contains(d.Code))
            {
                byDept[d.Code] = TwinPart<TwinDeptHr>.NoAccess;
                continue;
            }

            if (linked == 0)
            {
                byDept[d.Code] = TwinPart<TwinDeptHr>.NoData;
                continue;
            }

            var stats = overview.Divisions.Where(g => g.Id is { } id && departmentOf.GetValueOrDefault(id) == d.Code).ToList();
            var scheduled = stats.Sum(s => s.Scheduled);
            var present = stats.Sum(s => s.Present);
            byDept[d.Code] = TwinPart<TwinDeptHr>.Of(new TwinDeptHr(linked, stats.Sum(s => s.Active), scheduled, present, stats.Sum(s => s.Late),
                stats.Sum(s => s.Absent), Pct(present, scheduled)));
        }

        var total = new TwinHr(overview.SeesAll, overview.IsToday, overview.Active, overview.Scheduled, overview.Present, overview.Late, overview.Absent,
            overview.OnLeave, Pct(overview.Present, overview.Scheduled), sync.LastSuccessAt);
        return new HrResult(TwinPart<TwinHr>.Of(total), byDept);
    }

    /// <summary>
    /// Подразделение Verifix → код отдела OneBase: своя привязка или ближайшего предка (как доступ руководителя в HrService — дочерние
    /// подразделения входят в отдел родителя).
    /// </summary>
    public static Dictionary<long, string> DivisionDepartments(IReadOnlyList<HrDivisionView> divisions)
    {
        var byId = divisions.ToDictionary(d => d.Id);
        var result = new Dictionary<long, string>();
        foreach (var division in divisions)
        {
            var seen = new HashSet<long>();
            for (var current = division; current is not null && seen.Add(current.Id);
                 current = current.ParentId is { } parent ? byId.GetValueOrDefault(parent) : null)
            {
                if (current.DepartmentCode is { Length: > 0 } code)
                {
                    result[division.Id] = code;
                    break;
                }
            }
        }

        return result;
    }

    private static decimal? Pct(int part, int whole) => whole > 0 ? Math.Round(100m * part / whole, 1) : null;

    // ---------- Снабжение и склад ----------

    private static async Task<ProcurementResult> ProcurementAsync(IServiceProvider sp, Guid userId, IReadOnlySet<string> permissions,
        IReadOnlyDictionary<string, string> codeByName, CancellationToken ct)
    {
        var requests = sp.GetRequiredService<ProcurementRequestService>();
        var ctx = await requests.ContextAsync(userId, permissions, ct);
        if (!ctx.CanOpen)
        {
            throw new FieldForbiddenException("Нет доступа к заявкам на закупку.");
        }

        var categories = await requests.CategoriesAsync(ctx, true, ct);
        var raw = categories.FirstOrDefault(c => c.Code == RawMaterialsCode);

        // Открытые заявки, видимые пользователю, — тем же поиском, что «Заявки» (scope=all — всё, что ему видно).
        var rows = new List<ProcurementRequestRow>();
        for (var page = 1; page <= MaxRequestPages; page++)
        {
            var result = await requests.SearchAsync(ctx, new ProcurementRequestSearch("all", "open", Page: page, PageSize: 200), ct);
            rows.AddRange(result.Items);
            if (result.Items.Count == 0 || rows.Count >= result.Total)
            {
                break;
            }
        }

        var readsAll = ctx.ReadsAll || ctx.IsDirector;
        Dictionary<string, int>? overdue = null;
        if (readsAll)
        {
            var toBuy = await sp.GetRequiredService<ProcurementPurchaseService>().ToBuyAsync(ctx, ct);
            overdue = toBuy.Requests.Where(r => r.Overdue && codeByName.ContainsKey(r.DepartmentName))
                .GroupBy(r => codeByName[r.DepartmentName]).ToDictionary(g => g.Key, g => g.Count());
        }

        var today = FieldClock.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        TwinPart<IReadOnlyDictionary<string, TwinDeptSpend>> spend = TwinPart<IReadOnlyDictionary<string, TwinDeptSpend>>.NoAccess;
        decimal? spendTotal = null;
        if (readsAll)
        {
            var expenses = await sp.GetRequiredService<ProcurementInsightService>().ExpensesAsync(ctx, ProcurementFilter.Of(monthStart, today), ct);
            spendTotal = expenses.Total;
            spend = TwinPart<IReadOnlyDictionary<string, TwinDeptSpend>>.Of(expenses.ByDepartment.Where(a => codeByName.ContainsKey(a.Key))
                .ToDictionary(a => codeByName[a.Key], a => new TwinDeptSpend(a.Amount, a.Count)));
        }

        bool IsRaw(ProcurementRequestRow r) => raw is not null && r.Categories.Contains(raw.Name);
        TwinDeptSupply Count(IEnumerable<ProcurementRequestRow> list, int? overdueCount)
        {
            var l = list.ToList();
            return new TwinDeptSupply(l.Count, l.Count(r => ApprovalStatuses.Contains(r.Status)), l.Count(r => r.Priority == ProcurementPriority.Urgent),
                l.Count(r => r.Status == ProcurementRequestStatus.RevisionRequired), l.Count(IsRaw), overdueCount, l.Sum(r => r.PurchaseTotal));
        }

        var byDept = rows.GroupBy(r => r.DepartmentCode)
            .ToDictionary(g => g.Key, g => Count(g, overdue is null ? null : overdue.GetValueOrDefault(g.Key)));
        // Просрочка без открытых строк невозможна (просроченная — утверждённая и открытая), но отдел из «К закупке» добавим, если поиск обрезан.
        foreach (var (code, n) in overdue ?? [])
        {
            byDept.TryAdd(code, new TwinDeptSupply(0, 0, 0, 0, 0, n, 0));
        }

        var all = Count(rows, overdue?.Values.Sum());
        var supply = new TwinSupply(readsAll, all.Open, all.Approval, all.Urgent, all.Revision, all.Raw, all.Overdue, spendTotal, monthStart, today);

        var stock = TwinPart<TwinStock>.NoAccess;
        if (ctx.SeesStock)
        {
            var stockService = sp.GetRequiredService<ProcurementStockService>();
            var summary = await stockService.SummaryAsync(ctx, ct);
            int? rawLow = raw is null
                ? null
                : (await stockService.StockAsync(ctx, new ProcurementStockSearch(null, raw.Id, "low", null, false, 1, 5), ct)).Total;
            var warehouse = await sp.GetRequiredService<IAppDbContext>().ProcurementWarehouses.AsNoTracking().OrderByDescending(w => w.IsDefault)
                .Select(w => new { w.Id, w.Name }).FirstOrDefaultAsync(ct);
            stock = TwinPart<TwinStock>.Of(new TwinStock(warehouse?.Id, warehouse?.Name, summary.Items, summary.Low, summary.Empty, summary.WithReserve,
                summary.ToIssueRequests, rawLow));
        }

        return new ProcurementResult(TwinPart<TwinSupply>.Of(supply), stock, byDept, spend);
    }

    // ---------- Продажи, первичка, склад «Завод», дебиторка, акции ----------

    private static async Task<TwinPart<TwinSales>> SalesAsync(IServiceProvider sp, CancellationToken ct)
    {
        var o = (await sp.GetRequiredService<SalesDataLoader>().LoadAsync(null, null, ct)).CachedOverview();
        var k = o.Kpi;
        var byForecast = !o.Period.Closed && k.ForecastExecution is not null;
        var level = SalesMath.ExecutionLevelOf(byForecast ? k.ForecastExecution : k.Execution);
        return TwinPart<TwinSales>.Of(new TwinSales(o.Period.Year, o.Period.Month, o.Period.Closed, o.Period.DataThrough, k.FactKg, k.PlanKg, k.Execution,
            k.ForecastExecution, level?.ToString(), byForecast ? "forecast" : "execution", k.Revenue, k.Akb, o.Flags.Critical, o.Flags.Risk, o.Vacancies));
    }

    private static async Task<TwinPart<TwinPrimary>> PrimaryAsync(IServiceProvider sp, CancellationToken ct)
    {
        var p = await sp.GetRequiredService<PrimaryService>().GetAsync(null, null, ct);
        return TwinPart<TwinPrimary>.Of(new TwinPrimary(p.Year, p.Month, p.MonthTotal.Kg, p.MonthTotal.SumFactory, p.PlanMonthKg, p.MonthExecution,
            p.ForecastExecution, p.HasPlan, p.AsOf));
    }

    private static async Task<TwinPart<TwinPlant>> PlantAsync(IServiceProvider sp, CancellationToken ct)
    {
        var v = await sp.GetRequiredService<StockService>().QueryAsync(new StockQuery(), ct);
        if (v.Factory is not { } factory || v.FactoryTotals is not { } t)
        {
            return TwinPart<TwinPlant>.NoData;
        }

        return TwinPart<TwinPlant>.Of(new TwinPlant(factory.StockId, factory.Name, t.Kg, t.DaysOfCover, t.Skus, t.Deficit, t.Overstock, t.Dead, v.SnapshotDate,
            v.SyncedAt));
    }

    private static async Task<TwinPart<TwinFinance>> FinanceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var d = DsoCompute.Build(await sp.GetRequiredService<SettlementsService>().BaseAsync(ct));
        // Без реестра оплат долг и просрочка — все отгрузки как неоплаченные; раздел DSO их не показывает, двойник тоже.
        if (!d.HasPayments)
        {
            return TwinPart<TwinFinance>.NoData;
        }

        return TwinPart<TwinFinance>.Of(new TwinFinance(d.AsOf, d.Debt, d.Overdue, d.OverdueShare, d.OverdueDealers, d.PaidMonth, d.ShippedMonth));
    }

    private static async Task<TwinPart<TwinPromo>> PromoAsync(IServiceProvider sp, CancellationToken ct)
    {
        var p = await sp.GetRequiredService<PromoService>().GetAsync(null, null, null, ct);
        return p is null
            ? TwinPart<TwinPromo>.NoData
            : TwinPart<TwinPromo>.Of(new TwinPromo(p.Month.Key, p.Tiles.Total, p.Tiles.Promos, p.Tiles.ToSales));
    }

    // ---------- Находки AI ----------

    /// <summary>Действующие находки проактивного анализа, видимые по правам (как AI Alerts); категория — код отдела.</summary>
    private async Task<TwinPart<TwinAlerts>> AlertsAsync(IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var rows = await db.AiAlerts.AsNoTracking().Where(a => a.ResolvedAt == null && a.Severity != AiAlertSeverity.Opportunity)
            .Select(a => new { a.Id, a.Category, a.Severity, a.Title, a.Href, a.RequiredPermission, a.LastSeenAt })
            .ToListAsync(ct);
        var items = rows.Where(a => a.RequiredPermission is null || permissions.Contains(a.RequiredPermission))
            .OrderBy(a => a.Severity).ThenByDescending(a => a.LastSeenAt)
            .Take(100)
            .Select(a => new TwinAlert(a.Id, a.Category, a.Severity.ToString(), a.Title, a.Href))
            .ToList();
        return TwinPart<TwinAlerts>.Of(new TwinAlerts(items));
    }
}

using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Security;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;
using OneBase.Domain.Work;

namespace OneBase.Application.Analytik.Business;

public sealed record ProductionOrderRow(string Number, string Recipe, DateOnly PlannedDate, string Status, decimal PlannedQty, decimal? OutputQty, decimal? CompletionPercent,
    string Unit);

/// <summary>
/// Сколько можно произвести из доступного сырья по действующей техкарте: на базу техкарты нужно сырья (с потерями), выпуск ограничивает
/// позиция с наименьшим запасом; себестоимость сырья на базу — по последним ценам прихода (часть строк без цены — CostComplete = false).
/// </summary>
public sealed record ProducibleRow(Guid RecipeId, string Code, string Name, string OutputUnit, decimal BaseQuantity, decimal Producible, string? LimitingItem,
    decimal? MaterialCost, bool CostComplete, int Lines, int LinesWithoutPrice, IReadOnlyList<ProducibleComponent> TopComponents);

public sealed record ProducibleComponent(string Item, decimal Required, string Unit, decimal Available, decimal? Cost, decimal? SharePercent);

public sealed record ProductionBlock(
    AnalytikPeriod Period, IReadOnlyList<Kpi> Kpis, IReadOnlyList<ProductionOrderRow> Orders, IReadOnlyList<ProducibleRow> Producible,
    IReadOnlyList<ProductionDeviationLine> Deviations, IReadOnlyList<ProductionPriceChange> Prices, IReadOnlyList<ProductionDaysOfStock> LowDays,
    IReadOnlyList<string> Notes, IReadOnlyList<string> Sources) : IAnalytikBlock;

public sealed record HrDivisionRow(string Division, int Late, int LateMinutes, int EarlyLeave, int Absent, int Employees);

public sealed record HrBlock(AnalytikPeriod Period, IReadOnlyList<Kpi> Kpis, IReadOnlyList<HrDivisionRow> Divisions, IReadOnlyList<string> Notes,
    IReadOnlyList<string> Sources) : IAnalytikBlock;

public sealed record WorkDepartmentRow(string Department, int Open, int Overdue, int DoneInPeriod, int Blocked, decimal? OnTimePercent);

public sealed record WorkBlock(AnalytikPeriod Period, IReadOnlyList<Kpi> Kpis, IReadOnlyList<WorkDepartmentRow> Departments, IReadOnlyList<string> Notes,
    IReadOnlyList<string> Sources) : IAnalytikBlock;

/// <summary>
/// Аналитика производства, персонала и задач для «Аналитика» — через сервисы разделов и их правила видимости: производство —
/// ProcurementContext (как в разделе «Производство»), HR — HrContext (сотрудники своих подразделений или все), задачи — только управляющему
/// Аналитиком (видимость задач по отделам решает раздел «Задачи»). Только читает.
/// </summary>
public sealed class OperationsAnalytik(
    IAppDbContext db,
    ProcurementRequestService procurement,
    ProductionAnalyticsService production,
    ProductionRecipeService recipes,
    HrService hr,
    TimeProvider clock)
{
    public static readonly string[] ProductionPermissions =
        [Permissions.ProductionRead, Permissions.ProductionManage, Permissions.ProductionRecipes, Permissions.ProcurementRead, Permissions.ProcurementManage];

    private static readonly ProcurementContext SystemContext =
        new(Guid.Empty, new HashSet<string> { Permissions.ProductionRead, Permissions.ProcurementRead, Permissions.ProcurementManage }, null, new HashSet<Guid>(),
            IsDirector: true);

    private Task<ProcurementContext> ContextAsync(AnalytikPrincipal p, CancellationToken ct) =>
        p.UserId is { } id && !p.System ? procurement.ContextAsync(id, p.Permissions, ct) : Task.FromResult(SystemContext);

    // ---- производство ----

    public async Task<ProductionBlock?> ProductionAsync(AnalytikPeriod period, AnalytikPrincipal principal, CancellationToken ct, int producibleTop = 30)
    {
        if (!principal.HasAny(ProductionPermissions))
        {
            return null;
        }

        var ctx = await ContextAsync(principal, ct);
        var notes = new List<string>();
        var overview = await production.OverviewAsync(ctx, new ProcurementFilter(period.From, period.To), null, ct);

        var orders = await db.ProductionOrders.AsNoTracking()
            .Where(o => o.PlannedDate >= period.From && o.PlannedDate <= period.To && o.Status != ProductionOrderStatus.Cancelled)
            .OrderBy(o => o.PlannedDate)
            .Select(o => new { o.Number, Recipe = o.Recipe!.Name, o.PlannedDate, o.Status, o.PlannedQty, o.OutputQty, Unit = o.Recipe.OutputUnit })
            .ToListAsync(ct);
        var shifts = await db.ProductionShifts.AsNoTracking()
            .Where(s => s.ShiftDate >= period.From && s.ShiftDate <= period.To && s.Status == ProductionShiftStatus.Approved)
            .Select(s => new { s.GoodOutput, Defects = s.Defects.Sum(d => (decimal?)d.QuantityKg) ?? 0 })
            .ToListAsync(ct);
        var previousOutput = await db.ProductionShifts.AsNoTracking()
            .Where(s => s.ShiftDate >= period.PreviousFrom && s.ShiftDate <= period.PreviousTo && s.Status == ProductionShiftStatus.Approved)
            .SumAsync(s => (decimal?)s.GoodOutput, ct) ?? 0;

        var planned = orders.Sum(o => o.PlannedQty);
        var output = orders.Where(o => o.Status == ProductionOrderStatus.Completed).Sum(o => o.OutputQty ?? 0);
        var good = shifts.Sum(s => s.GoodOutput ?? 0);
        var defects = shifts.Sum(s => s.Defects);
        if (orders.Count == 0)
        {
            notes.Add("Производственных заданий с плановой датой в периоде нет — план выпуска не с чем сравнить.");
        }

        if (shifts.Count == 0)
        {
            notes.Add("Утверждённых смен в периоде нет — фактический выпуск и брак по сменам не посчитаны.");
        }

        notes.Add("Мощности линий и загрузка в OneBase не учитываются (этап «линии и мощности» не построен).");

        const string src = "OneBase → Производство";
        var kpis = new List<Kpi>
        {
            Kpi.Of("production.planned", "План выпуска по заданиям", planned, "кг", null, KpiKinds.Fact, src + " → Задания", "/production/orders"),
            Kpi.Of("production.output", "Выпуск по закрытым заданиям", output, "кг", null, KpiKinds.Fact, src + " → Задания", "/production/orders"),
            Kpi.Of("production.execution", "Выполнение плана выпуска", planned > 0 ? Math.Round(output / planned * 100, 1) : null, "%", null, KpiKinds.Calculated,
                src + " → Задания", "/production/orders", planned > 0 ? null : "нет плана в периоде"),
            Kpi.Of("production.good", "Выпуск годного по сменам", shifts.Count > 0 ? good : null, "кг", previousOutput > 0 ? previousOutput : null, KpiKinds.Fact,
                src + " → Смены", "/production/shifts"),
            Kpi.Of("production.defect_rate", "Брак", good + defects > 0 ? Math.Round(defects / (good + defects) * 100, 2) : null, "%", null, KpiKinds.Calculated,
                src + " → Смены", "/production/shifts"),
            Kpi.Of("production.overconsumption", "Перерасход: позиций", overview.Overconsumed, "шт", null, KpiKinds.Fact, src + " → Расход и норма", "/production/reports"),
            Kpi.Of("production.norm_vs_actual", "Факт к норме расхода", overview.NormTotal > 0 ? Math.Round(overview.ActualTotal / overview.NormTotal * 100, 1) : null,
                "%", null, KpiKinds.Calculated, src + " → Расход и норма", "/production/reports"),
            Kpi.Of("production.open_orders", "Открытые задания", overview.OpenOrders, "шт", null, KpiKinds.Fact, src, "/production/orders"),
            Kpi.Of("production.overdue_orders", "Просроченные задания", overview.OverdueOrders, "шт", null, KpiKinds.Fact, src, "/production/orders"),
            Kpi.Of("production.stock_value", "Стоимость сырья на складе", overview.StockValue, "сум", null, KpiKinds.Calculated, src + " → Обзор", "/production"),
        };

        return new ProductionBlock(period, kpis,
            orders.Select(o => new ProductionOrderRow(o.Number, o.Recipe, o.PlannedDate, o.Status.ToString(), o.PlannedQty, o.OutputQty,
                o.PlannedQty > 0 && o.OutputQty is { } q ? Math.Round(q / o.PlannedQty * 100, 1) : null, o.Unit)).ToList(),
            await ProducibleAsync(ctx, producibleTop, ct), overview.Deviations, overview.Prices, overview.LowDays, notes,
            [$"{src} → Задания и смены", $"{src} → Обзор (расход и норма, цены, дни запаса)", $"{src} → Техкарты", "OneBase → Снабжение → Склад"]);
    }

    /// <summary>Сколько можно произвести по действующим техкартам из доступного сырья и себестоимость сырья на базу техкарты.</summary>
    public async Task<IReadOnlyList<ProducibleRow>> ProducibleAsync(ProcurementContext ctx, int top, CancellationToken ct, decimal priceChange = 0)
    {
        var active = await db.ProductionRecipes.AsNoTracking().Where(r => r.Status == ProductionRecipeStatus.Active)
            .OrderBy(r => r.Code).Take(top).Select(r => new { r.Id, r.Code, r.Name, r.OutputUnit, r.OutputQuantity }).ToListAsync(ct);
        var prices = await LastPricesAsync(ct);
        var result = new List<ProducibleRow>();
        foreach (var r in active)
        {
            var requirement = await recipes.RequirementAsync(ctx, r.Id, r.OutputQuantity, null, ct);
            var lines = requirement.Lines.Where(l => l.Required > 0).ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            var ratio = lines.Min(l => l.Available / l.Required);
            var limiting = lines.MinBy(l => l.Available / l.Required);
            var costs = lines.Select(l => (Line: l, Cost: prices.TryGetValue(l.ItemId, out var price) ? Math.Round(l.Required * price * (1 + priceChange), 0) : (decimal?)null)).ToList();
            var known = costs.Where(c => c.Cost is not null).Sum(c => c.Cost!.Value);
            var withoutPrice = costs.Count(c => c.Cost is null);
            var top3 = costs.OrderByDescending(c => c.Cost ?? -1).Take(5)
                .Select(c => new ProducibleComponent(c.Line.Name, c.Line.Required, c.Line.UnitCode, c.Line.Available, c.Cost,
                    c.Cost is { } v && known > 0 ? Math.Round(v / known * 100, 1) : null)).ToList();
            result.Add(new ProducibleRow(r.Id, r.Code, r.Name, r.OutputUnit, r.OutputQuantity, Math.Round(r.OutputQuantity * Math.Max(0, ratio), 2), limiting?.Name,
                costs.Any(c => c.Cost is not null) ? known : null, withoutPrice == 0, lines.Count, withoutPrice, top3));
        }

        return result.OrderBy(r => r.Producible).ToList();
    }

    /// <summary>Последняя цена прихода позиции (базовая единица).</summary>
    public async Task<Dictionary<Guid, decimal>> LastPricesAsync(CancellationToken ct) =>
        (await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => m.Kind == ProcurementMovementKind.Receipt && m.UnitCost != null && m.UnitCost > 0)
            .Select(m => new { m.ItemId, m.CreatedAt, m.UnitCost })
            .ToListAsync(ct))
        .GroupBy(m => m.ItemId)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First().UnitCost!.Value);

    // ---- персонал ----

    public async Task<HrBlock?> HrAsync(AnalytikPeriod period, AnalytikPrincipal principal, CancellationToken ct)
    {
        // Фоновая задача видит всех (как HR); пользователь — по своему HrContext (подразделения или все).
        var ctx = principal.System
            ? new HrContext(Guid.Empty, true, false, null, [])
            : principal.UserId is { } userId ? await hr.ContextAsync(userId, principal.Permissions, ct) : null;
        if (ctx is null)
        {
            return null;
        }

        if (!ctx.HasAccess)
        {
            return null;
        }

        var today = FieldClock.LocalDate(clock.GetUtcNow());
        var overview = await hr.OverviewAsync(ctx, today, ct);
        var current = await hr.DeviationsAsync(ctx, period.From, Min(period.To, today), null, null, null, ct);
        var previous = await hr.DeviationsAsync(ctx, period.PreviousFrom, period.PreviousTo, null, null, null, ct);
        var notes = new List<string>();
        // Табель Verifix загружен не с начала прошлого периода — рост «на 2000 %» был бы сравнением с пустотой: сравнение не показываем.
        var since = await db.HrTimesheetDays.AsNoTracking().MinAsync(d => (DateOnly?)d.Date, ct);
        var comparable = since is { } first && first <= period.PreviousFrom;
        var covered = since is { } start && start <= period.To;
        if (!covered)
        {
            notes.Add(since is { } later
                ? $"Табель Verifix в OneBase — с {later:dd.MM.yyyy}: за период {period.Label} данных о посещаемости нет."
                : "Табеля Verifix в OneBase нет.");
        }
        else if (since is { } loaded && loaded > period.PreviousFrom)
        {
            notes.Add(loaded > period.From
                ? $"Табель Verifix в OneBase — с {loaded:dd.MM.yyyy}: период покрыт не полностью, сравнения с прошлым периодом нет."
                : $"Табель Verifix в OneBase — с {loaded:dd.MM.yyyy}: прошлый период покрыт не полностью, сравнение не показано.");
        }

        int? Count(IReadOnlyList<HrDeviation> list, string kind) => covered ? list.Count(d => d.Kind == kind) : null;
        int? Before(string kind) => comparable ? Count(previous, kind) : null;
        if (!ctx.SeesAll)
        {
            notes.Add("Показаны только ваши подразделения.");
        }

        notes.Add("ФОТ, оценок и KPI сотрудников вне продаж в OneBase нет.");
        const string src = "OneBase → HR (Verifix)";
        var kpis = new List<Kpi>
        {
            Kpi.Of("hr.active", "Сотрудников работает", overview.Active, "чел.", null, KpiKinds.Fact, src, "/hr"),
            Kpi.Of("hr.present_today", "Пришли сегодня", overview.Present, "чел.", null, KpiKinds.Fact, src, "/hr"),
            Kpi.Of("hr.absent_today", "Отсутствуют сегодня", overview.Absent, "чел.", null, KpiKinds.Fact, src, "/hr"),
            Kpi.Of("hr.late", "Опоздания за период", Count(current, "late"), "случ.", Before("late"), KpiKinds.Fact, src + " → Отклонения", "/hr"),
            Kpi.Of("hr.late_minutes", "Минут опозданий", covered ? current.Where(d => d.Kind == "late").Sum(d => d.Minutes ?? 0) : null,
                "мин", comparable ? previous.Where(d => d.Kind == "late").Sum(d => d.Minutes ?? 0) : null, KpiKinds.Fact, src + " → Отклонения", "/hr"),
            Kpi.Of("hr.absent", "Отсутствия за период", Count(current, "absent"), "дн.", Before("absent"), KpiKinds.Fact, src + " → Отклонения", "/hr"),
            Kpi.Of("hr.early", "Ранние уходы за период", Count(current, "early"), "случ.", Before("early"), KpiKinds.Fact, src + " → Отклонения", "/hr"),
        };
        var divisions = current.GroupBy(d => d.Division ?? "Без подразделения")
            .Select(g => new HrDivisionRow(g.Key, g.Count(d => d.Kind == "late"), g.Where(d => d.Kind == "late").Sum(d => d.Minutes ?? 0),
                g.Count(d => d.Kind == "early"), g.Count(d => d.Kind == "absent"), g.Select(d => d.EmployeeId).Distinct().Count()))
            .OrderByDescending(d => d.Late + d.Absent).Take(30).ToList();
        return new HrBlock(period, kpis, divisions, notes, [src + ": сотрудники, посещаемость, отклонения"]);
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    // ---- задачи отделов ----

    /// <summary>Задачи по отделам: только управляющему Аналитиком (видимость задач отделов решает раздел «Задачи»).</summary>
    public async Task<WorkBlock?> WorkAsync(AnalytikPeriod period, AnalytikPrincipal principal, CancellationToken ct)
    {
        if (!principal.Manager)
        {
            return null;
        }

        var today = FieldClock.LocalDate(clock.GetUtcNow());
        var from = FieldClock.DayStartUtc(period.From);
        var to = FieldClock.DayStartUtc(period.To.AddDays(1));
        var tasks = await db.WorkTasks.AsNoTracking().Where(t => t.ArchivedAt == null)
            .Select(t => new { Department = t.Department!.Name, t.Status, t.DueDate, t.CompletedAt, t.IsBlocked })
            .ToListAsync(ct);
        bool Open(WorkTaskStatus s) => s is WorkTaskStatus.New or WorkTaskStatus.InProgress or WorkTaskStatus.Review;
        var rows = tasks.GroupBy(t => t.Department)
            .Select(g =>
            {
                var done = g.Where(t => t.Status == WorkTaskStatus.Done && t.CompletedAt >= from && t.CompletedAt < to).ToList();
                var onTime = done.Count(t => t.DueDate is null || FieldClock.LocalDate(t.CompletedAt!.Value) <= t.DueDate);
                return new WorkDepartmentRow(g.Key, g.Count(t => Open(t.Status)), g.Count(t => Open(t.Status) && t.DueDate < today), done.Count,
                    g.Count(t => Open(t.Status) && t.IsBlocked), done.Count > 0 ? Math.Round((decimal)onTime / done.Count * 100, 1) : null);
            })
            .OrderByDescending(r => r.Overdue).ToList();
        const string src = "OneBase → Задачи";
        var kpis = new List<Kpi>
        {
            Kpi.Of("work.open", "Открытых задач", rows.Sum(r => r.Open), "шт", null, KpiKinds.Fact, src, "/tasks"),
            Kpi.Of("work.overdue", "Просрочено", rows.Sum(r => r.Overdue), "шт", null, KpiKinds.Fact, src, "/tasks"),
            Kpi.Of("work.done", "Выполнено за период", rows.Sum(r => r.DoneInPeriod), "шт", null, KpiKinds.Fact, src, "/tasks"),
        };
        return new WorkBlock(period, kpis, rows, [], [src]);
    }
}

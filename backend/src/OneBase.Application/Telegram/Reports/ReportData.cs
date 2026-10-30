using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Promo;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Сумма и вес строк одной категории в заказе.</summary>
public readonly record struct CategoryAmount(decimal Sum, decimal Kg);

/// <summary>
/// Заказ ТП: время создания (местное — как в Linko), сумма и вес по строкам, категории отчёта (сумма и вес строк категории) и акции строк
/// со скидкой (пусто — заказ без акции).
/// </summary>
public sealed record FactOrder(long Id, long AgentId, long? MarketId, DateOnly Date, TimeOnly Time, decimal Sum, decimal Kg,
    IReadOnlyDictionary<string, CategoryAmount> Categories, IReadOnlyList<string> Promos)
{
    public bool HasPromo => Promos.Count > 0;
}

/// <summary>Визит Linko: в плане (маршруте) или нет, выполнен ли, во сколько.</summary>
public sealed record FactVisit(long AgentId, long MarketId, DateOnly Day, TimeOnly Time, bool InPlan, bool Done);

/// <summary>План ТП на месяц из Linko (staff_balance): сумма, кг, АКБ и кг по категориям отчёта.</summary>
public sealed record MonthPlan(decimal? Sum, decimal? Kg, decimal? Akb, IReadOnlyDictionary<string, decimal> CategoryKg);

/// <summary>
/// Данные отчёта для ТП области за отрезок дней: планы месяцев, заказы, визиты, категории отчёта и их средняя цена за кг (для плана
/// категорий в сумах), свежесть данных Linko.
/// </summary>
public sealed class ReportFacts
{
    public required IReadOnlyList<string> Categories { get; init; }

    /// <summary>Средняя цена категории, сум/кг — по продажам 30 дней до отчёта (план категории в сумах = план, кг × цена).</summary>
    public required IReadOnlyDictionary<string, decimal> CategoryPrice { get; init; }

    public required IReadOnlyDictionary<(long Agent, int Year, int Month), MonthPlan> Plans { get; init; }
    public required IReadOnlyList<FactOrder> Orders { get; init; }
    public required IReadOnlyList<FactVisit> Visits { get; init; }

    /// <summary>
    /// Дневные планы ТП на день отчёта («ТП без заказов»: зафиксированы утром — DailyPlanService, DailyPlanMath); нет строки — план не
    /// посчитан (показывается «—»).
    /// </summary>
    public IReadOnlyDictionary<long, DailyPlanRow> DailyPlans { get; init; } = new Dictionary<long, DailyPlanRow>();

    public DailyPlanRow? DailyPlanOf(long agent) => DailyPlans.GetValueOrDefault(agent);

    /// <summary>Последнее успешное обновление заказов и визитов Linko.</summary>
    public DateTimeOffset? OrdersAsOf { get; init; }
    public DateTimeOffset? VisitsAsOf { get; init; }

    public MonthPlan? PlanOf(long agent, DateOnly day) => Plans.GetValueOrDefault((agent, day.Year, day.Month));

    /// <summary>План на день: месячный ÷ рабочие дни месяца (пн–сб); воскресенье — без плана.</summary>
    public static decimal? Daily(decimal? month, DateOnly day) =>
        day.DayOfWeek == DayOfWeek.Sunday ? null : FieldKpi.DailyPlan(month, day.Year, day.Month);

    public decimal? DayPlanSum(long agent, DateOnly day) => Daily(PlanOf(agent, day)?.Sum, day);
    public decimal? DayPlanKg(long agent, DateOnly day) => Daily(PlanOf(agent, day)?.Kg, day);

    /// <summary>План на день по категории, кг; null — плана категории у ТП нет.</summary>
    public decimal? DayPlanCategoryKg(long agent, DateOnly day, string category) =>
        PlanOf(agent, day)?.CategoryKg.TryGetValue(category, out var kg) == true ? Daily(kg, day) : null;

    /// <summary>План категории в сумах: кг × средняя цена категории (у плана «А + Б» — средняя цена обеих); цены нет — null.</summary>
    public decimal? CategorySum(string category, decimal? kg)
    {
        if (kg is not { } k)
        {
            return null;
        }

        var prices = category.Split(" + ").Select(c => CategoryPrice.TryGetValue(c, out var p) ? p : (decimal?)null).ToList();
        return prices.All(p => p is not null) && prices.Count > 0 ? k * prices.Average(p => p!.Value) : null;
    }

    /// <summary>Категории плана ТП и категории отчёта — по порядку настройки, затем составные планы («Могуль + Шоколад»).</summary>
    public IReadOnlyList<string> CategoriesOf(IEnumerable<long> agents, DateOnly day)
    {
        var planned = agents.SelectMany(a => PlanOf(a, day)?.CategoryKg.Keys ?? []).Distinct().ToList();
        return Categories.Concat(planned.Where(p => !Categories.Contains(p)).Order()).ToList();
    }

    public IEnumerable<FactOrder> OrdersOf(long agent, DateOnly from, DateOnly to, TimeOnly? until = null) =>
        Orders.Where(o => o.AgentId == agent && o.Date >= from && o.Date <= to && (until is not { } u || o.Time < u));

    public IEnumerable<FactVisit> VisitsOf(long agent, DateOnly from, DateOnly to) => Visits.Where(v => v.AgentId == agent && v.Day >= from && v.Day <= to);

    /// <summary>Сумма и вес заказов по категории (у составной категории — сумма частей).</summary>
    public static CategoryAmount Amount(IEnumerable<FactOrder> orders, string category)
    {
        var parts = category.Split(" + ");
        decimal sum = 0, kg = 0;
        foreach (var o in orders)
        {
            foreach (var part in parts)
            {
                if (o.Categories.TryGetValue(part, out var a))
                {
                    sum += a.Sum;
                    kg += a.Kg;
                }
            }
        }

        return new CategoryAmount(sum, kg);
    }

    /// <summary>Заказов, где есть категория (один заказ с тремя категориями — в каждой из трёх, но в «Всего» — один раз).</summary>
    public static int OrdersWith(IEnumerable<FactOrder> orders, string category)
    {
        var parts = category.Split(" + ");
        return orders.Count(o => parts.Any(o.Categories.ContainsKey));
    }
}

/// <summary>Точка ТП без закупа: последняя подтверждённая покупка (доставленный заказ, дата приёмки), её сумма, дни, средний закуп, на маршруте ли сегодня.</summary>
public sealed record SilentMarket(long Id, string Name, long AgentId, DateOnly LastPurchase, decimal LastSum, int Days, decimal KgPerMonth,
    IReadOnlyList<string> Categories, bool OnRoute);

/// <summary>
/// «Клиенты без закупа» ТП: активная база (точки, где он ответственный в Linko и есть подтверждённая покупка за год; без тестовых), без
/// закупа N+ дней, точек на маршруте сегодня, из них без закупа, сумма их последних закупов; список — точки без закупа.
/// </summary>
public sealed record NoPurchaseAgent(long AgentId, int Base, int Silent, int OnRoute, int SilentOnRoute, decimal SilentOnRouteLastSum, IReadOnlyList<SilentMarket> Markets);

/// <summary>
/// Источник данных автоотчётов — только данные OneBase (зеркало Linko и оргструктура «Продаж»), никаких строк из Excel: заказы ТП,
/// взятые им за день (как «взято» Sales Base: по дню ввода, все статусы кроме отменённых — свежий заказ в Linko стоит в «ещё не отгружен»
/// до отгрузки; без «Завода»), строки заказов по категориям отчёта, акции (как на странице «Акции»), визиты Linko, планы staff_balance.
/// </summary>
public sealed class ReportDataService(IAppDbContext db, SalesDataLoader loader, SalesOptions options, ILinkoSalesProvider linko, PromoService promo, IMemoryCache cache,
    DailyPlanService dailyPlans)
{
    private static readonly string[] TestWords = ["тест", "test"];

    /// <summary>withDailyPlans — дневные планы ТП на день to («ТП без заказов»): сохранённые утром или посчитанные при первом обращении.</summary>
    public async Task<ReportFacts> LoadAsync(IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, bool withPromos, bool withDailyPlans, CancellationToken ct)
    {
        var months = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).Select(d => (d.Year, d.Month)).Distinct().ToList();
        var analytics = await loader.LoadAsync(to.Year, to.Month, ct);
        var categories = analytics.ReportCategoryNames();

        var plans = new Dictionary<(long, int, int), MonthPlan>();
        foreach (var (year, month) in months)
        {
            var monthAnalytics = year == to.Year && month == to.Month ? analytics : await loader.LoadAsync(year, month, ct);
            var categoryPlans = monthAnalytics.ReportCategoryPlans(year, month);
            foreach (var p in await linko.AgentPlansAsync(agents, year, month, ct))
            {
                plans[(p.LinkoUserId, year, month)] = new MonthPlan(p.PlanSum, p.PlanKg, p.PlanAkb,
                    categoryPlans.TryGetValue(p.LinkoUserId, out var c) ? c : new Dictionary<string, decimal>());
            }

            foreach (var (agent, c) in categoryPlans.Where(x => agents.Contains(x.Key) && !plans.ContainsKey((x.Key, year, month))))
            {
                plans[(agent, year, month)] = new MonthPlan(null, null, null, c);
            }
        }

        var ids = agents.ToArray();
        var notTaken = LinkoMirrorSalesProvider.NotTaken;
        var orders = (await db.LinkoOrders.AsNoTracking()
                .Where(o => o.AgentId != null && ids.Contains(o.AgentId.Value) && o.CreatedDate >= from && o.CreatedDate <= to && !notTaken.Contains(o.Status))
                .Select(o => new { o.Id, AgentId = o.AgentId!.Value, o.MarketId, o.CreatedDate, o.CreatedAt, o.TotalPrice, o.TotalWeight, o.BranchName })
                .ToListAsync(ct))
            .Where(o => !options.IsExcludedBranch(o.BranchName) && !options.IsIgnoredBranch(o.BranchName))
            .ToList();
        var orderIds = orders.Select(o => o.Id).ToArray();
        var lines = (await db.LinkoOrderLines.AsNoTracking().Where(l => orderIds.Contains(l.OrderId))
                .Select(l => new { l.OrderId, l.ProductId, l.TotalPrice, l.TotalWeight }).ToListAsync(ct))
            .ToLookup(l => l.OrderId);
        var promos = withPromos ? await promo.OrderPromosAsync(orderIds, ct) : [];

        var facts = orders.Select(o =>
        {
            var own = lines[o.Id].ToList();
            var byCategory = own
                .Select(l => (Category: l.ProductId is { } p ? analytics.ReportCategoryOf(p) : null, l.TotalPrice, l.TotalWeight))
                .Where(x => x.Category is not null)
                .GroupBy(x => x.Category!)
                .ToDictionary(g => g.Key, g => new CategoryAmount(g.Sum(x => x.TotalPrice), g.Sum(x => x.TotalWeight)));
            // Сумма и вес — по строкам (как категории); заказ без строк — по шапке.
            var sum = own.Count > 0 ? own.Sum(l => l.TotalPrice) : o.TotalPrice;
            var kg = own.Count > 0 ? own.Sum(l => l.TotalWeight) : o.TotalWeight;
            return new FactOrder(o.Id, o.AgentId, o.MarketId, o.CreatedDate, TimeOnly.FromDateTime(o.CreatedAt), sum, kg, byCategory,
                promos.TryGetValue(o.Id, out var names) ? names : []);
        }).ToList();

        var visits = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.UserId != null && v.MarketId != null && ids.Contains(v.UserId.Value) && v.Day >= from && v.Day <= to)
            .Select(v => new { AgentId = v.UserId!.Value, MarketId = v.MarketId!.Value, v.Day, v.Date, v.IsInPlan, v.Status })
            .ToListAsync(ct);
        var states = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders" || s.Entity == "visits")
            .ToDictionaryAsync(s => s.Entity, s => s.LastSuccessAt, ct);

        return new ReportFacts
        {
            Categories = categories,
            CategoryPrice = await CategoryPricesAsync(analytics, from, ct),
            Plans = plans,
            DailyPlans = withDailyPlans ? await dailyPlans.EnsureAsync(agents, to, DailyPlanService.Report, ct) : new Dictionary<long, DailyPlanRow>(),
            Orders = facts,
            Visits = visits.Select(v => new FactVisit(v.AgentId, v.MarketId, v.Day, TimeOnly.FromDateTime(v.Date), v.IsInPlan, v.Status == "done")).ToList(),
            OrdersAsOf = states.GetValueOrDefault("orders"),
            VisitsAsOf = states.GetValueOrDefault("visits"),
        };
    }

    /// <summary>Средняя цена категорий, сум/кг, по принятым заказам вторички за 30 дней до дня отчёта; кэш на день.</summary>
    private Task<IReadOnlyDictionary<string, decimal>> CategoryPricesAsync(SalesAnalytics analytics, DateOnly day, CancellationToken ct) =>
        cache.GetOrCreateAsync($"telegram-reports:category-prices:{day:yyyyMMdd}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            var since = day.AddDays(-30);
            var notTaken = LinkoMirrorSalesProvider.NotTaken;
            var rows = await (
                    from l in db.LinkoOrderLines
                    join o in db.LinkoOrders on l.OrderId equals o.Id
                    where o.CreatedDate >= since && o.CreatedDate < day && !notTaken.Contains(o.Status) && l.ProductId != null && l.TotalWeight > 0
                    group l by l.ProductId into g
                    select new { ProductId = g.Key!.Value, Sum = g.Sum(x => x.TotalPrice), Kg = g.Sum(x => x.TotalWeight) })
                .ToListAsync(ct);
            return (IReadOnlyDictionary<string, decimal>)rows
                .Select(r => (Category: analytics.ReportCategoryOf(r.ProductId), r.Sum, r.Kg))
                .Where(r => r.Category is not null)
                .GroupBy(r => r.Category!)
                .Where(g => g.Sum(x => x.Kg) > 0)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Sum) / g.Sum(x => x.Kg));
        })!;

    /// <summary>
    /// «Клиенты без закупа» ТП на день: база — точки, где ТП ответственный в Linko, с подтверждённой покупкой за год (доставленный заказ, дата
    /// приёмки, за вычетом возвратов по нему; целиком возвращённый заказ — не покупка), без тестовых; без закупа — последняя покупка days и
    /// более дней назад; на маршруте — визит в плане на этот день. Сумма последнего закупа и средний закуп — за вычетом возвратов; порядок —
    /// по среднему закупу, кг/мес (крупнейшие клиенты), затем по сумме последнего закупа.
    /// </summary>
    public async Task<IReadOnlyDictionary<long, NoPurchaseAgent>> NoPurchaseAsync(IReadOnlyCollection<long> agents, DateOnly day, int days,
        IReadOnlyList<FactVisit> visits, CancellationToken ct)
    {
        var ids = agents.ToArray();
        var markets = (await db.LinkoMarkets.AsNoTracking()
                .Where(m => m.ResponsibleAgentId != null && ids.Contains(m.ResponsibleAgentId.Value))
                .Select(m => new { m.Id, m.Name, m.MarketTypeName, AgentId = m.ResponsibleAgentId!.Value })
                .ToListAsync(ct))
            .Where(m => !TestWords.Any(w => m.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || (m.MarketTypeName?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToList();
        var marketIds = markets.Select(m => m.Id).ToArray();
        var sold = options.FieldSoldStatuses;
        var returned = options.ReturnStatuses;
        var yearAgo = day.AddDays(-365);
        // Покупки — доставленные заказы по дате приёмки за вычетом возвратов по ним (строка возврата Linko ссылается на заказ). Заказ, который
        // вернули целиком (99 % веса и больше), — не покупка: иначе «последний закуп» был бы заказом, который магазин вернул.
        var delivered = await db.LinkoOrders.AsNoTracking()
            .Where(o => o.MarketId != null && marketIds.Contains(o.MarketId.Value) && sold.Contains(o.Status) && o.AcceptedDate != null
                        && o.AcceptedDate >= yearAgo.AddDays(-90) && o.AcceptedDate <= day)
            .Select(o => new { o.Id, MarketId = o.MarketId!.Value, Accepted = o.AcceptedDate!.Value, o.TotalPrice, o.TotalWeight })
            .ToListAsync(ct);
        var orderIds = delivered.Select(o => o.Id).ToArray();
        var returns = orderIds.Length == 0
            ? []
            : await (from l in db.LinkoOrderReturnLines
                    join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                    where l.OrderId != null && orderIds.Contains(l.OrderId.Value) && returned.Contains(r.Status) && r.CreatedDate <= day
                    group l by l.OrderId into g
                    select new { OrderId = g.Key!.Value, Sum = g.Sum(x => x.Price * x.Amount), Kg = g.Sum(x => x.TotalWeight) })
                .ToDictionaryAsync(x => x.OrderId, ct);
        var history = delivered
            .Select(o =>
            {
                var back = returns.GetValueOrDefault(o.Id);
                var kg = o.TotalWeight - (back?.Kg ?? 0);
                var sum = Math.Max(0, o.TotalPrice - (back?.Sum ?? 0));
                var whollyReturned = back is not null && (o.TotalWeight > 0 ? kg <= o.TotalWeight * 0.01m : sum <= o.TotalPrice * 0.01m);
                return new { o.Id, o.MarketId, o.Accepted, TotalPrice = sum, TotalWeight = Math.Max(0, kg), WhollyReturned = whollyReturned };
            })
            .Where(o => !o.WhollyReturned)
            .ToList();
        var last = history.Where(o => o.Accepted >= yearAgo).GroupBy(o => o.MarketId).ToDictionary(g => g.Key, g => g.Max(o => o.Accepted));

        var silent = markets.Where(m => last.TryGetValue(m.Id, out var d) && day.DayNumber - d.DayNumber >= days).ToList();
        var silentIds = silent.Select(m => m.Id).ToHashSet();
        history = history.Where(o => silentIds.Contains(o.MarketId)).ToList();
        var analytics = await loader.LoadAsync(day.Year, day.Month, ct);
        var historyIds = history.Select(o => o.Id).ToArray();
        var historyTypes = (await db.LinkoOrderLines.AsNoTracking().Where(l => historyIds.Contains(l.OrderId) && l.ProductId != null)
                .Select(l => new { l.OrderId, ProductId = l.ProductId!.Value }).Distinct().ToListAsync(ct))
            .ToLookup(l => l.OrderId, l => analytics.ReportCategoryOf(l.ProductId));
        var byMarket = history.ToLookup(o => o.MarketId);
        var route = visits.Where(v => v.Day == day && v.InPlan).GroupBy(v => v.AgentId).ToDictionary(g => g.Key, g => g.Select(v => v.MarketId).ToHashSet());
        var order = analytics.ReportCategoryNames().ToList();

        return agents.ToDictionary(agent => agent, agent =>
        {
            var own = markets.Where(m => m.AgentId == agent && last.ContainsKey(m.Id)).ToList();
            var onRoute = route.GetValueOrDefault(agent) ?? [];
            var list = silent.Where(m => m.AgentId == agent).Select(m =>
            {
                var lastDay = last[m.Id];
                var orders = byMarket[m.Id].ToList();
                var cats = orders.SelectMany(o => historyTypes[o.Id]).OfType<string>().Distinct().OrderBy(c => order.IndexOf(c)).ToList();
                return new SilentMarket(m.Id, m.Name, agent, lastDay, orders.Where(o => o.Accepted == lastDay).Sum(o => o.TotalPrice), day.DayNumber - lastDay.DayNumber,
                    Math.Round(orders.Where(o => o.Accepted > lastDay.AddDays(-90)).Sum(o => o.TotalWeight) / 3, 1), cats, onRoute.Contains(m.Id));
            }).OrderByDescending(m => m.KgPerMonth).ThenByDescending(m => m.LastSum).ToList(); // крупнейшие — по среднему закупу, а не по последнему заказу
            return new NoPurchaseAgent(agent, own.Count, list.Count, onRoute.Count, list.Count(m => m.OnRoute),
                list.Where(m => m.OnRoute).Sum(m => m.LastSum), list);
        });
    }
}

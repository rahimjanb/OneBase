using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Domain.Incentive;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>Спящая точка ТП (список спящих из Linko): приоритет визита, последний Yamelly, кг Yamelly за год, в маршруте ли, берёт ли другое, когда разбужена.</summary>
public sealed record IncentiveSleepingPoint(
    long MarketId,
    string Name,
    long AgentId,
    int Priority,
    DateOnly? LastYamelly,
    decimal YamellyKgYear,
    bool InRoute,
    bool BuysOther,
    DateOnly? WokenOn);

/// <summary>Статус кэшбека заказа: оформлен (ждёт доставки), подтверждён доставкой, отменён (заказ отменён или возврат ниже порога).</summary>
public static class IncentiveCashbackStatus
{
    public const string Issued = "issued";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Заказ Yamelly спящей точке своего списка: сумма Yamelly по прайсу (до скидки) и после доставленных возвратов, выполнен ли порог YM-01, кэшбек
/// при оформлении и после возвратов, статус кэшбека.
/// </summary>
public sealed record IncentiveYamellyOrder(
    long OrderId,
    long AgentId,
    long MarketId,
    DateOnly Created,
    DateTime CreatedAt,
    DateOnly? Accepted,
    decimal ListSum,
    decimal NetListSum,
    bool Qualifies,
    decimal Cashback,
    decimal NetCashback,
    string CashbackStatus);

/// <summary>Заказ ТП с Twizos: кг и сумма Twizos по прайсу, подарок по правилу TW-02 и выданные подарки (бонусные строки Linko по картинке подарка).</summary>
public sealed record IncentiveTwizosOrder(
    long OrderId,
    long AgentId,
    long MarketId,
    DateOnly Created,
    DateTime CreatedAt,
    string Status,
    decimal Kg,
    decimal ListSum,
    string? Gift,
    IReadOnlyDictionary<string, int> BonusGifts);

/// <summary>SKU Twizos региона: АКБ (точек с закупом) и кг за окно.</summary>
public sealed record IncentiveTopSku(long ProductId, string Name, string? Code, decimal UnitKg, int Akb, decimal Kg);

/// <summary>Баланс кэшбека ТП по неделе пн–вс: разбужено, оформлено, подтверждено, выплачено, к выплате.</summary>
public sealed record IncentiveCashbackWeek(DateOnly Monday, int Woken, decimal Issued, decimal Confirmed, decimal Paid)
{
    public decimal ToPay => Math.Max(0, Confirmed - Paid);
}

/// <summary>
/// Баланс кэшбека ТП (ТЗ, раздел 7): оформлено (порог YM-01 выполнен, заказ не отменён), ждёт доставки, подтверждено (доставлено, за вычетом
/// возвратов), отменено, выплачено (отметки бухгалтерии), к выплате (подтверждено за прошедшие недели − выплачено), можно ещё заработать
/// (оставшиеся спящие × минимальный кэшбек). Недели — пн–вс; подтверждённое — по неделе доставки.
/// </summary>
public sealed record IncentiveCashbackBalance(
    long AgentId,
    int Listed,
    int Woken,
    decimal Issued,
    decimal Pending,
    decimal Confirmed,
    decimal Cancelled,
    decimal Paid,
    decimal ToPay,
    decimal CanEarn,
    IReadOnlyList<IncentiveCashbackWeek> Weeks);

/// <summary>Территория YM-03: продано кг, план, % и ТП территории с кг.</summary>
public sealed record IncentiveTerritoryState(string Name, decimal PlanKg, decimal Kg, IReadOnlyDictionary<long, decimal> Agents)
{
    public decimal Share => PlanKg > 0 ? Kg / PlanKg : 0;
}

/// <summary>
/// Данные акций ноября по зеркалу Linko: список спящих ТП (из Linko, по закреплению) и порядок визитов, заказы Yamelly
/// спящим с порогом YM-01 и кэшбеком YM-02 по статусам, заказы Twizos с подарком TW-02 и выданными подарками, средний чек точек, ТОП SKU
/// региона, лучший агент дня TW-01, остатки подарков на складах, территориальный план YM-03.
/// </summary>
public sealed class IncentivePromoService(IAppDbContext db, SalesOptions sales, IncentiveService incentive)
{
    private static readonly string[] NotTaken = LinkoMirrorSalesProvider.NotTaken;

    /// <summary>Товары по типам Linko с подтипами.</summary>
    public async Task<long[]> ProductsOfTypesAsync(IEnumerable<long> typeIds, CancellationToken ct)
    {
        var tree = await LinkoTypeTree.LoadAsync(db, ct);
        var types = SalesCategories.WithDescendants(typeIds.ToArray(), tree.Parents).ToArray();
        return types.Length == 0
            ? []
            : await db.LinkoProducts.AsNoTracking().Where(p => p.TypeId != null && types.Contains(p.TypeId.Value)).Select(p => p.Id).ToArrayAsync(ct);
    }

    // ---------- Спящие (YM-01) ----------

    /// <summary>Строка списка спящих: точка Linko, с какого дня в списке, с какого исключена.</summary>
    public sealed record SleepingEntry(long MarketId, DateOnly ListedOn, DateOnly? RemovedOn)
    {
        public bool On(DateOnly day) => ListedOn <= day && (RemovedOn is not { } off || off > day);
    }

    /// <summary>Спящие по Linko на день asOf и день первой покупки Yamelly с ListSince до asOf (по точкам, которые её сделали).</summary>
    public sealed record SleepingScan(HashSet<long> Sleeping, Dictionary<long, DateOnly> BoughtSince);

    private readonly Dictionary<Guid, List<SleepingEntry>> computedLists = [];

    /// <summary>
    /// Спящие по Linko на день asOf: доставленные заказы Yamelly с ListFrom до ListSince были, с ListSince до asOf — нет (без «Завода» и «К К
    /// Мерч»). Для точек, бравших Yamelly с ListSince, — день первой такой покупки.
    /// </summary>
    public async Task<SleepingScan> ScanSleepingAsync(IncentiveSettings s, DateOnly asOf, CancellationToken ct)
    {
        if (s.Offers.Yamelly is not { } ym)
        {
            return new SleepingScan([], []);
        }

        var yamelly = await ProductsOfTypesAsync(ym.ProductTypeIds ?? [], ct);
        if (yamelly.Length == 0)
        {
            return new SleepingScan([], []);
        }

        var (first, since) = (ym.ListFrom, ym.ListSince);
        var sold = sales.FieldSoldStatuses;
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.MarketId != null && o.CreatedDate >= first && o.CreatedDate < asOf && sold.Contains(o.Status) && l.ProductId != null
                      && yamelly.Contains(l.ProductId.Value)
                group o.CreatedDate by new { Market = o.MarketId!.Value, o.BranchName } into g
                select new
                {
                    g.Key.Market, g.Key.BranchName,
                    Before = g.Max(d => d < since ? 1 : 0),
                    After = g.Min(d => d >= since ? (DateOnly?)d : null),
                })
            .ToListAsync(ct);
        var markets = rows.Where(r => Counted(r.BranchName)).GroupBy(r => r.Market)
            .Select(g => new { Market = g.Key, Before = g.Any(r => r.Before == 1), After = g.Min(r => r.After) }).ToList();
        return new SleepingScan(markets.Where(m => m.Before && m.After is null).Select(m => m.Market).ToHashSet(),
            markets.Where(m => m.After is not null).ToDictionary(m => m.Market, m => m.After!.Value));
    }

    /// <summary>
    /// Список спящих акции: снимок incentive.SleepingMarkets; пока снимка нет (до первой карточки по расписанию или пересчёта) — расчёт по Linko на
    /// начало Yamelly, без записи.
    /// </summary>
    public async Task<List<SleepingEntry>> SleepingListAsync(IncentiveSettings s, CancellationToken ct)
    {
        var rows = await db.IncentiveSleepingMarkets.AsNoTracking().Where(x => x.ProgramId == s.Id)
            .Select(x => new SleepingEntry(x.MarketId, x.ListedOn, x.RemovedOn)).ToListAsync(ct);
        if (rows.Count > 0 || s.Offers.Yamelly is not { } ym)
        {
            return rows;
        }

        if (!computedLists.TryGetValue(s.Id, out var computed))
        {
            computed = (await ScanSleepingAsync(s, ym.Start, ct)).Sleeping.Select(m => new SleepingEntry(m, ym.Start, null)).ToList();
            computedLists[s.Id] = computed;
        }

        return computed;
    }

    /// <summary>Забыть расчётный список (после записи снимка).</summary>
    public void ForgetSleepingList(Guid programId) => computedLists.Remove(programId);

    /// <summary>Точки списка спящих на день (в списке с ListedOn, не убраны) — ID Linko.</summary>
    public async Task<HashSet<long>> ListedAsync(IncentiveSettings s, DateOnly date, CancellationToken ct) =>
        (await SleepingListAsync(s, ct)).Where(x => x.On(date)).Select(x => x.MarketId).ToHashSet();

    /// <summary>
    /// Спящие точки ТП на день: точки списка, закреплённые за ним в Linko, с приоритетом визита (ТЗ 4.3), последним Yamelly и кг Yamelly за год,
    /// в плане визитов ли на 14 дней вперёд, брала ли другие категории за 90 дней, и днём «разбудил» (порог YM-01 до date включительно; в этот
    /// день — до until).
    /// </summary>
    public async Task<Dictionary<long, List<IncentiveSleepingPoint>>> SleepingAsync(IncentiveSettings s, IReadOnlyCollection<long> agents, DateOnly date, TimeOnly? until,
        CancellationToken ct)
    {
        var result = agents.Distinct().ToDictionary(a => a, _ => new List<IncentiveSleepingPoint>());
        if (s.Offers.Yamelly is not { } ym || agents.Count == 0)
        {
            return result;
        }

        var ids = result.Keys.ToArray();
        var listed = await ListedAsync(s, date, ct);
        var markets = await db.LinkoMarkets.AsNoTracking()
            .Where(m => m.ResponsibleAgentId != null && ids.Contains(m.ResponsibleAgentId.Value) && listed.Contains(m.Id))
            .Select(m => new { m.Id, m.Name, Agent = m.ResponsibleAgentId!.Value }).ToListAsync(ct);
        if (markets.Count == 0)
        {
            return result;
        }

        var marketIds = markets.Select(m => m.Id).ToArray();
        var yamelly = await ProductsOfTypesAsync(ym.ProductTypeIds ?? [], ct);
        var yearStart = new DateOnly(ym.Start.Year, 1, 1);
        var history = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.MarketId != null && marketIds.Contains(o.MarketId.Value) && o.CreatedDate >= yearStart && o.CreatedDate < ym.Start && !NotTaken.Contains(o.Status)
                      && l.ProductId != null && yamelly.Contains(l.ProductId.Value)
                group new { o.CreatedDate, l.TotalWeight } by o.MarketId!.Value into g
                select new { Market = g.Key, Last = g.Max(x => x.CreatedDate), Kg = g.Sum(x => x.TotalWeight) })
            .ToDictionaryAsync(x => x.Market, ct);
        var otherFrom = date.AddDays(-90);
        var buysOther = (await (from l in db.LinkoOrderLines.AsNoTracking()
                    join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                    where o.MarketId != null && marketIds.Contains(o.MarketId.Value) && o.CreatedDate >= otherFrom && o.CreatedDate < date && !NotTaken.Contains(o.Status)
                          && l.ProductId != null && !yamelly.Contains(l.ProductId.Value)
                    select o.MarketId!.Value).Distinct().ToListAsync(ct))
            .ToHashSet();
        var routeTo = date.AddDays(14);
        var inRoute = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.UserId != null && ids.Contains(v.UserId.Value) && v.MarketId != null && marketIds.Contains(v.MarketId.Value) && v.IsInPlan
                            && v.Day >= date && v.Day <= routeTo)
                .Select(v => new { Agent = v.UserId!.Value, Market = v.MarketId!.Value }).Distinct().ToListAsync(ct))
            .Select(x => (x.Agent, x.Market)).ToHashSet();
        var woken = (await YamellyOrdersAsync(s, ids, ym.Start, date, until, ct))
            .Where(o => o.Qualifies && o.CashbackStatus != IncentiveCashbackStatus.Cancelled)
            .GroupBy(o => o.MarketId).ToDictionary(g => g.Key, g => g.Min(o => o.Created));

        foreach (var m in markets)
        {
            var h = history.GetValueOrDefault(m.Id);
            var route = inRoute.Contains((m.Agent, m.Id));
            var other = buysOther.Contains(m.Id);
            result[m.Agent].Add(new IncentiveSleepingPoint(m.Id, m.Name, m.Agent,
                IncentivePromoRules.SleepingPriority(route, other, h?.Last, ym.Start), h?.Last, h?.Kg ?? 0, route, other, woken.TryGetValue(m.Id, out var w) ? w : null));
        }

        foreach (var list in result.Values)
        {
            list.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority)
                : b.YamellyKgYear != a.YamellyKgYear ? b.YamellyKgYear.CompareTo(a.YamellyKgYear)
                : a.MarketId.CompareTo(b.MarketId));
        }

        return result;
    }

    /// <summary>
    /// Заказы ТП Yamelly спящим точкам своего списка (точка закреплена за ТП и была в списке в день заказа) с from по to (в последний день — до
    /// until): порог YM-01 по строкам, кэшбек по прайсу; доставленные возвраты строк Yamelly уменьшают сумму — ниже порога кэшбек отменяется.
    /// </summary>
    public async Task<List<IncentiveYamellyOrder>> YamellyOrdersAsync(IncentiveSettings s, IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, TimeOnly? until,
        CancellationToken ct)
    {
        if (s.Offers.Yamelly is not { } ym || agents.Count == 0)
        {
            return [];
        }

        var first = from < ym.Start ? ym.Start : from;
        var last = to > ym.End ? ym.End : to;
        if (last < first)
        {
            return [];
        }

        var ids = agents.Distinct().ToArray();
        var yamelly = await ProductsOfTypesAsync(ym.ProductTypeIds ?? [], ct);
        var listed = (await SleepingListAsync(s, ct)).Where(x => x.ListedOn <= last).ToDictionary(x => x.MarketId);
        var owned = await db.LinkoMarkets.AsNoTracking().Where(m => m.ResponsibleAgentId != null && ids.Contains(m.ResponsibleAgentId.Value))
            .Select(m => new { m.Id, Agent = m.ResponsibleAgentId!.Value }).ToListAsync(ct);
        var own = owned.Where(m => listed.ContainsKey(m.Id)).ToDictionary(m => m.Id, m => m.Agent);
        if (own.Count == 0 || yamelly.Length == 0)
        {
            return [];
        }

        var marketIds = own.Keys.ToArray();
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.AgentId != null && ids.Contains(o.AgentId.Value) && o.MarketId != null && marketIds.Contains(o.MarketId.Value)
                      && o.CreatedDate >= first && o.CreatedDate <= last && l.ProductId != null && yamelly.Contains(l.ProductId.Value)
                select new
                {
                    o.Id, Agent = o.AgentId!.Value, Market = o.MarketId!.Value, o.CreatedDate, o.CreatedAt, o.AcceptedDate, o.Status, o.IsFullReturn, o.BranchName,
                    l.ProductId, l.Amount, l.TotalWeight, l.OriginPrice, l.TotalPrice, l.TotalDiscount,
                })
            .ToListAsync(ct);
        rows = rows.Where(r => Counted(r.BranchName) && own.GetValueOrDefault(r.Market) == r.Agent
                               && listed[r.Market].On(r.CreatedDate)
                               && (r.CreatedDate != last || until is not { } u || TimeOnly.FromDateTime(r.CreatedAt) < u))
            .ToList();
        var orderIds = rows.Select(r => r.Id).Distinct().ToArray();
        var back = sales.ReturnStatuses;
        var returned = (await (from rl in db.LinkoOrderReturnLines.AsNoTracking()
                    join r in db.LinkoOrderReturns.AsNoTracking() on rl.ReturnId equals r.Id
                    where back.Contains(r.Status) && rl.OrderId != null && orderIds.Contains(rl.OrderId.Value) && rl.ProductId != null && yamelly.Contains(rl.ProductId.Value)
                    select new { Order = rl.OrderId!.Value, Product = rl.ProductId!.Value, rl.Amount }).ToListAsync(ct))
            .GroupBy(x => (x.Order, x.Product)).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var sold = sales.FieldSoldStatuses;

        return rows.GroupBy(r => r.Id).Select(g =>
        {
            var o = g.First();
            static decimal Unit(decimal amount, decimal kg) => amount > 0 ? Math.Round(kg / amount, 3) : 0;
            static decimal ListPrice(decimal? origin, decimal amount, decimal total, decimal? discount) =>
                origin is { } price && price > 0 ? price * amount : total + (discount ?? 0);
            var lines = g.Select(x => (Product: x.ProductId!.Value, x.Amount, Unit: Unit(x.Amount, x.TotalWeight), List: ListPrice(x.OriginPrice, x.Amount, x.TotalPrice, x.TotalDiscount)))
                .ToList();
            var listSum = lines.Sum(x => x.List);
            var qualifies = IncentivePromoRules.Qualifies(ym, lines.Select(x => (x.Unit, x.Amount)));
            var net = lines.Select(x =>
            {
                var gone = Math.Min(x.Amount, returned.GetValueOrDefault((o.Id, x.Product)));
                return (x.Unit, Amount: x.Amount - gone, List: x.Amount > 0 ? x.List * (x.Amount - gone) / x.Amount : 0);
            }).ToList();
            var netQualifies = IncentivePromoRules.Qualifies(ym, net.Select(x => (x.Unit, x.Amount)));
            var netSum = net.Sum(x => x.List);
            var status = !qualifies || NotTaken.Contains(o.Status) || o.IsFullReturn ? IncentiveCashbackStatus.Cancelled
                : sold.Contains(o.Status) ? netQualifies ? IncentiveCashbackStatus.Confirmed : IncentiveCashbackStatus.Cancelled
                : IncentiveCashbackStatus.Issued;
            return new IncentiveYamellyOrder(o.Id, o.Agent, o.Market, o.CreatedDate, o.CreatedAt, o.AcceptedDate, listSum, netSum, qualifies,
                qualifies ? IncentivePromoRules.Cashback(ym, listSum) : 0, netQualifies ? IncentivePromoRules.Cashback(ym, netSum) : 0, status);
        }).OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderId).ToList();
    }

    /// <summary>
    /// Баланс кэшбека ТП на день asOf: заказы с начала Yamelly по asOf (в этот день — до until), выплаты бухгалтерии, недели пн–вс. «К выплате» —
    /// подтверждённое за прошедшие недели (неделя доставки раньше недели asOf) минус выплаченное за них.
    /// </summary>
    public async Task<Dictionary<long, IncentiveCashbackBalance>> CashbackAsync(IncentiveSettings s, IReadOnlyCollection<long> agents, DateOnly asOf, TimeOnly? until,
        CancellationToken ct)
    {
        var result = new Dictionary<long, IncentiveCashbackBalance>();
        if (s.Offers.Yamelly is not { } ym || agents.Count == 0)
        {
            return result;
        }

        var ids = agents.Distinct().ToArray();
        var orders = await YamellyOrdersAsync(s, ids, ym.Start, asOf, until, ct);
        var sleeping = await SleepingAsync(s, ids, asOf, until, ct);
        var paid = await db.IncentivePayouts.AsNoTracking().Where(p => p.ProgramId == s.Id && ids.Contains(p.AgentId) && p.Kind == IncentivePayoutKind.Cashback)
            .Select(p => new { p.AgentId, p.WeekStart, p.Amount }).ToListAsync(ct);
        var thisWeek = Battle.BattleRules.Monday(asOf);
        foreach (var agent in ids)
        {
            var own = orders.Where(o => o.AgentId == agent).ToList();
            var points = sleeping.GetValueOrDefault(agent) ?? [];
            var woken = points.Count(p => p.WokenOn is not null);
            var payouts = paid.Where(p => p.AgentId == agent).ToList();
            var mondays = own.Select(o => Battle.BattleRules.Monday(o.Created))
                .Concat(own.Where(o => o.CashbackStatus == IncentiveCashbackStatus.Confirmed && o.Accepted is not null).Select(o => Battle.BattleRules.Monday(o.Accepted!.Value)))
                .Concat(payouts.Select(p => p.WeekStart)).Distinct().Order().ToList();
            var weeks = mondays.Select(m => new IncentiveCashbackWeek(m,
                points.Count(p => p.WokenOn is { } w && Battle.BattleRules.Monday(w) == m),
                own.Where(o => o.CashbackStatus != IncentiveCashbackStatus.Cancelled && Battle.BattleRules.Monday(o.Created) == m).Sum(o => o.Cashback),
                own.Where(o => o.CashbackStatus == IncentiveCashbackStatus.Confirmed && o.Accepted is { } a && Battle.BattleRules.Monday(a) == m).Sum(o => o.NetCashback),
                payouts.Where(p => p.WeekStart == m).Sum(p => p.Amount))).ToList();
            var issued = own.Where(o => o.CashbackStatus != IncentiveCashbackStatus.Cancelled).Sum(o => o.Cashback);
            var pending = own.Where(o => o.CashbackStatus == IncentiveCashbackStatus.Issued).Sum(o => o.Cashback);
            var confirmed = own.Where(o => o.CashbackStatus == IncentiveCashbackStatus.Confirmed).Sum(o => o.NetCashback);
            var cancelled = own.Where(o => o.Qualifies && o.CashbackStatus == IncentiveCashbackStatus.Cancelled).Sum(o => o.Cashback);
            var toPay = weeks.Where(w => w.Monday < thisWeek).Sum(w => w.ToPay);
            result[agent] = new IncentiveCashbackBalance(agent, points.Count, woken, issued, pending, confirmed, cancelled, payouts.Sum(p => p.Amount), toPay,
                (points.Count - woken) * ym.MinCashback, weeks);
        }

        return result;
    }

    // ---------- Twizos (TW-01, TW-02) ----------

    /// <summary>
    /// Заказы ТП с Twizos с from по to (кроме отменённых; в последний день — до until): кг и сумма Twizos по прайсу, подарок по правилу TW-02,
    /// выданные подарки (бонусные строки Linko). Без «Завода» и «К К Мерч».
    /// </summary>
    public async Task<List<IncentiveTwizosOrder>> TwizosOrdersAsync(IncentiveSettings s, IReadOnlyCollection<long> agents, DateOnly first, DateOnly last, TimeOnly? until,
        CancellationToken ct)
    {
        if (agents.Count == 0 || last < first)
        {
            return [];
        }

        var ids = agents.Distinct().ToArray();
        var twizos = await incentive.ProductIdsAsync(s, ct);
        var tw = s.Offers.Twizos;
        var giftProducts = (tw?.GiftTiers ?? []).SelectMany(g => (g.ProductIds ?? []).Select(p => (Product: p, g.Gift))).ToList();
        var giftIds = giftProducts.Select(g => g.Product).ToArray();
        var products = twizos.Concat(giftIds).Distinct().ToArray();
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.AgentId != null && ids.Contains(o.AgentId.Value) && o.MarketId != null && o.CreatedDate >= first && o.CreatedDate <= last && !NotTaken.Contains(o.Status)
                      && l.ProductId != null && products.Contains(l.ProductId.Value)
                select new
                {
                    o.Id, Agent = o.AgentId!.Value, Market = o.MarketId!.Value, o.CreatedDate, o.CreatedAt, o.Status, o.BranchName,
                    Product = l.ProductId!.Value, l.Amount, l.TotalWeight, l.OriginPrice, l.TotalPrice, l.TotalDiscount,
                })
            .ToListAsync(ct);
        var twizosSet = twizos.ToHashSet();
        var giftOf = giftProducts.GroupBy(g => g.Product).ToDictionary(g => g.Key, g => g.First().Gift);
        return rows.Where(r => Counted(r.BranchName) && (r.CreatedDate != last || until is not { } u || TimeOnly.FromDateTime(r.CreatedAt) < u))
            .GroupBy(r => r.Id)
            .Select(g =>
            {
                var o = g.First();
                var lines = g.Where(x => twizosSet.Contains(x.Product)).ToList();
                var list = lines.Sum(x => x.OriginPrice is { } p && p > 0 ? p * x.Amount : x.TotalPrice + (x.TotalDiscount ?? 0));
                var bonus = g.Where(x => giftOf.ContainsKey(x.Product)).GroupBy(x => giftOf[x.Product]).ToDictionary(b => b.Key, b => (int)b.Sum(x => x.Amount));
                return new IncentiveTwizosOrder(o.Id, o.Agent, o.Market, o.CreatedDate, o.CreatedAt, o.Status, lines.Sum(x => x.TotalWeight), list,
                    tw is null ? null : IncentivePromoRules.Gift(tw, list)?.Gift, bonus);
            })
            .Where(o => o.Kg > 0 || o.BonusGifts.Count > 0)
            .OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderId)
            .ToList();
    }

    /// <summary>
    /// Средний чек точек (ТЗ 4.2): сумма доставленных заказов до скидки (все категории) ÷ дней с заказом за days дней до date; брала ли Twizos за
    /// это окно (заказы кроме отменённых).
    /// </summary>
    public async Task<Dictionary<long, (decimal Check, bool Bought)>> ChecksAsync(IncentiveSettings s, IReadOnlyCollection<long> markets, DateOnly date, int days,
        CancellationToken ct)
    {
        if (markets.Count == 0)
        {
            return [];
        }

        var ids = markets.Distinct().ToArray();
        var since = date.AddDays(-Math.Max(1, days));
        var sold = sales.FieldSoldStatuses;
        var orders = await db.LinkoOrders.AsNoTracking()
            .Where(o => o.MarketId != null && ids.Contains(o.MarketId.Value) && o.CreatedDate >= since && o.CreatedDate < date && sold.Contains(o.Status))
            .Select(o => new { Market = o.MarketId!.Value, o.CreatedDate, Sum = o.TotalPrice + o.DiscountPrice, o.BranchName }).ToListAsync(ct);
        var twizos = await incentive.ProductIdsAsync(s, ct);
        var bought = (await (from l in db.LinkoOrderLines.AsNoTracking()
                    join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                    where o.MarketId != null && ids.Contains(o.MarketId.Value) && o.CreatedDate >= since && o.CreatedDate < date && !NotTaken.Contains(o.Status)
                          && l.ProductId != null && twizos.Contains(l.ProductId.Value)
                    select o.MarketId!.Value).Distinct().ToListAsync(ct))
            .ToHashSet();
        var checks = orders.Where(o => Counted(o.BranchName)).GroupBy(o => o.Market)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(o => o.Sum) / g.Select(o => o.CreatedDate).Distinct().Count(), 0));
        return ids.ToDictionary(m => m, m => (checks.GetValueOrDefault(m), bought.Contains(m)));
    }

    /// <summary>
    /// ТОП SKU Twizos региона для наборов (ТЗ 4.2): за TopDays дней по доставленным заказам в филиалах; по АКБ — пачки 35 г, по весу — 140 г (200 г
    /// в наборы не ставим). По два.
    /// </summary>
    public async Task<(IReadOnlyList<IncentiveTopSku> ByAkb, IReadOnlyList<IncentiveTopSku> ByKg)> TopSkuAsync(IncentiveSettings s, IReadOnlyCollection<string> branches,
        DateOnly date, CancellationToken ct)
    {
        var days = s.Offers.Twizos?.TopDays ?? 90;
        var since = date.AddDays(-days);
        var twizos = await incentive.ProductIdsAsync(s, ct);
        var names = branches.ToArray();
        var sold = sales.FieldSoldStatuses;
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.BranchName != null && names.Contains(o.BranchName) && o.CreatedDate >= since && o.CreatedDate < date && sold.Contains(o.Status)
                      && l.ProductId != null && twizos.Contains(l.ProductId.Value) && l.Amount > 0
                group new { l.TotalWeight, l.Amount, o.MarketId } by l.ProductId!.Value into g
                select new { Product = g.Key, Kg = g.Sum(x => x.TotalWeight), Amount = g.Sum(x => x.Amount), Akb = g.Select(x => x.MarketId).Distinct().Count() })
            .ToListAsync(ct);
        var ids = rows.Select(r => r.Product).ToArray();
        var info = await db.LinkoProducts.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => (p.Name, p.Code), ct);
        var skus = rows.Select(r => new IncentiveTopSku(r.Product, info.GetValueOrDefault(r.Product).Name ?? $"#{r.Product}", info.GetValueOrDefault(r.Product).Code,
            Math.Round(r.Kg / r.Amount, 3), r.Akb, r.Kg)).ToList();
        static bool Is(decimal unit, decimal kg) => Math.Abs(unit - kg) <= 0.002m;
        return (skus.Where(x => Is(x.UnitKg, 0.035m)).OrderByDescending(x => x.Akb).ThenByDescending(x => x.Kg).Take(2).ToList(),
            skus.Where(x => Is(x.UnitKg, 0.14m)).OrderByDescending(x => x.Kg).ThenByDescending(x => x.Akb).Take(2).ToList());
    }

    /// <summary>Филиалы Linko точек, закреплённых за ТП (регион ТП для ТОП SKU).</summary>
    public async Task<IReadOnlyList<string>> BranchesAsync(IReadOnlyCollection<long> agents, CancellationToken ct)
    {
        var ids = agents.Distinct().ToArray();
        return await db.LinkoMarkets.AsNoTracking()
            .Where(m => m.ResponsibleAgentId != null && ids.Contains(m.ResponsibleAgentId.Value) && m.BranchName != null)
            .GroupBy(m => m.BranchName!).OrderByDescending(g => g.Count()).Select(g => g.Key).ToListAsync(ct);
    }

    /// <summary>
    /// Лучший агент дня TW-01 среди участников: кг Twizos во взятых заказах дня (в 13:00 — до until), группы — розница и рынок и опт (список оптовых
    /// ТП настроек). Нет акции TW-01 или день вне её — пусто.
    /// </summary>
    public async Task<IReadOnlyList<IncentivePromoRules.BestRow>> BestAsync(IncentiveSettings s, IReadOnlyCollection<long> participants, DateOnly date, TimeOnly? until,
        CancellationToken ct)
    {
        if (s.Offers.Best is not { } best || !best.Covers(date) || participants.Count == 0
            || !Reports.DailyPlanMath.IsWorkingDay(date, await incentive.HolidaysAsync(s, ct)))
        {
            return [];
        }

        var taken = await incentive.TakenAsync(s, participants, date, date, until, ct);
        return IncentivePromoRules.BestOfDay(best, participants.Distinct().Select(a => (a, s.InWholesaleGroup(a), taken.GetValueOrDefault((a, date)))));
    }

    /// <summary>
    /// TW-01 по дням с from по to (рабочие дни периода TW-01): закрытый день — из итогов дня (incentive.DayResults: победа к выплате не меняется от
    /// исправленных задним числом заказов), остальные — по заказам (в день to — до lastUntil).
    /// </summary>
    public async Task<Dictionary<DateOnly, IReadOnlyList<IncentivePromoRules.BestRow>>> BestDaysAsync(IncentiveSettings s, IReadOnlyCollection<long> participants,
        DateOnly from, DateOnly to, TimeOnly? lastUntil, CancellationToken ct)
    {
        var result = new Dictionary<DateOnly, IReadOnlyList<IncentivePromoRules.BestRow>>();
        if (s.Offers.Best is not { } best || participants.Count == 0)
        {
            return result;
        }

        var first = from < best.Start ? best.Start : from;
        var last = to > best.End ? best.End : to;
        if (last < first)
        {
            return result;
        }

        var holidays = await incentive.HolidaysAsync(s, ct);
        var days = Enumerable.Range(0, last.DayNumber - first.DayNumber + 1).Select(first.AddDays).Where(d => Reports.DailyPlanMath.IsWorkingDay(d, holidays)).ToList();
        var closed = (await db.IncentiveDayResults.AsNoTracking().Where(r => r.ProgramId == s.Id && r.Date >= first && r.Date <= last).ToListAsync(ct))
            .GroupBy(r => r.Date).ToDictionary(g => g.Key, g => g.ToList());
        var open = days.Where(d => !closed.ContainsKey(d)).ToList();
        var ids = participants.Distinct().ToArray();
        var taken = open.Count == 0 ? [] : await incentive.TakenAsync(s, ids, open.Min(), open.Max(), open.Max() == to ? lastUntil : null, ct);
        foreach (var d in days)
        {
            result[d] = closed.TryGetValue(d, out var rows)
                ? rows.Select(x => new IncentivePromoRules.BestRow(x.AgentId, x.Wholesale, x.TwizosKg, x.BestPlace, x.BestWinner)).ToList()
                : IncentivePromoRules.BestOfDay(best, ids.Select(a => (a, s.InWholesaleGroup(a), taken.GetValueOrDefault((a, d)))));
        }

        return result;
    }

    /// <summary>Выплаты ТП (отметки бухгалтерии) за недели с from по to: вид → сумма.</summary>
    public async Task<Dictionary<(long Agent, string Kind), decimal>> PaidAsync(IncentiveSettings s, IReadOnlyCollection<long> agents, DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        var ids = agents.Distinct().ToArray();
        var monday = Battle.BattleRules.Monday(from);
        return (await db.IncentivePayouts.AsNoTracking().Where(p => p.ProgramId == s.Id && ids.Contains(p.AgentId) && p.WeekStart >= monday && p.WeekStart <= to)
                .Select(p => new { p.AgentId, p.Kind, p.Amount }).ToListAsync(ct))
            .GroupBy(p => (p.AgentId, p.Kind)).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
    }

    /// <summary>Остатки подарков TW-02 на складах Linko (бонусные товары подарков): склад → подарок → штук.</summary>
    public async Task<Dictionary<string, Dictionary<string, decimal>>> GiftStockAsync(IncentiveSettings s, CancellationToken ct)
    {
        var gifts = (s.Offers.Twizos?.GiftTiers ?? []).SelectMany(g => (g.ProductIds ?? []).Select(p => (Product: p, g.Gift))).ToList();
        var ids = gifts.Select(g => g.Product).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var giftOf = gifts.GroupBy(g => g.Product).ToDictionary(g => g.Key, g => g.First().Gift);
        var rows = await (from b in db.LinkoProductBalances.AsNoTracking()
                join st in db.LinkoStocks.AsNoTracking() on b.StockId equals st.Id
                where ids.Contains(b.ProductId)
                select new { Stock = st.Name, b.ProductId, b.Balance })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.Stock.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(g => g.Key, g => g.GroupBy(r => giftOf[r.ProductId]).ToDictionary(x => x.Key, x => x.Sum(r => r.Balance)), StringComparer.CurrentCultureIgnoreCase);
    }

    /// <summary>
    /// Территориальный план YM-03 на день: кг товаров плана (типы Linko; не заданы — помадка, шоколад и остальные кондитерские типы настроек) в
    /// доставленных заказах территории с начала периода по date (по дате приёмки) минус доставленные возвраты; кг по ТП.
    /// </summary>
    public async Task<IReadOnlyList<IncentiveTerritoryState>> TerritoriesAsync(IncentiveSettings s, DateOnly date, CancellationToken ct)
    {
        if (s.Offers.Territory is not { } t || t.Territories is not { Count: > 0 } territories || t.ProductTypeIds is not { Count: > 0 } types)
        {
            return [];
        }

        var last = date < t.End ? date : t.End;
        if (last < t.Start)
        {
            return territories.Select(x => new IncentiveTerritoryState(x.Name, x.PlanKg, 0, new Dictionary<long, decimal>())).ToList();
        }

        var products = await ProductsOfTypesAsync(types, ct);
        var branches = territories.SelectMany(x => x.Branches).Distinct().ToArray();
        var sold = sales.FieldSoldStatuses;
        var back = sales.ReturnStatuses;
        var lines = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.BranchName != null && branches.Contains(o.BranchName) && sold.Contains(o.Status) && o.AcceptedDate >= t.Start && o.AcceptedDate <= last
                      && l.ProductId != null && products.Contains(l.ProductId.Value)
                group l by new { Branch = o.BranchName!, Agent = o.AgentId } into g
                select new { g.Key.Branch, g.Key.Agent, Kg = g.Sum(x => x.TotalWeight) })
            .ToListAsync(ct);
        var returns = await (from rl in db.LinkoOrderReturnLines.AsNoTracking()
                join r in db.LinkoOrderReturns.AsNoTracking() on rl.ReturnId equals r.Id
                join o in db.LinkoOrders.AsNoTracking() on rl.OrderId equals (long?)o.Id
                where back.Contains(r.Status) && r.CreatedDate <= last && o.BranchName != null && branches.Contains(o.BranchName) && sold.Contains(o.Status)
                      && o.AcceptedDate >= t.Start && o.AcceptedDate <= last && rl.ProductId != null && products.Contains(rl.ProductId.Value)
                group rl by new { Branch = o.BranchName!, Agent = o.AgentId } into g
                select new { g.Key.Branch, g.Key.Agent, Kg = g.Sum(x => x.TotalWeight) })
            .ToListAsync(ct);
        return territories.Select(x =>
        {
            var own = x.Branches.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            var agents = lines.Where(l => own.Contains(l.Branch)).Select(l => (l.Agent, Kg: l.Kg))
                .Concat(returns.Where(r => own.Contains(r.Branch)).Select(r => (r.Agent, Kg: -r.Kg)))
                .Where(a => a.Agent is not null).GroupBy(a => a.Agent!.Value).ToDictionary(g => g.Key, g => g.Sum(a => a.Kg));
            var kg = lines.Where(l => own.Contains(l.Branch)).Sum(l => l.Kg) - returns.Where(r => own.Contains(r.Branch)).Sum(r => r.Kg);
            return new IncentiveTerritoryState(x.Name, x.PlanKg, kg, agents);
        }).ToList();
    }

    private bool Counted(string? branch) => !sales.IsExcludedBranch(branch) && !sales.IsIgnoredBranch(branch);
}

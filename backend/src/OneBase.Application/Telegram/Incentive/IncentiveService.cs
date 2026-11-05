using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Накопительная акция — расчёт по зеркалу Linko (только чтение): накоплено ТП (доставлено по дате приёмки − доставленные возвраты по этим
/// заказам + заказы сегодня), кг по дням недели по взятым заказам, праздники для нормы. Филиалы «Завод» и «К К Мерч» не входят — как во
/// вторичке. Настройки акции — incentive.Programs.
/// </summary>
public sealed class IncentiveService(IAppDbContext db, SalesOptions sales)
{
    private static readonly string[] NotTaken = LinkoMirrorSalesProvider.NotTaken;

    /// <summary>Акция по коду (включённая или нет); null — нет такой.</summary>
    public async Task<IncentiveSettings?> ProgramAsync(string code, CancellationToken ct) =>
        await db.IncentivePrograms.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, ct) is { } p ? IncentiveSettings.From(p) : null;

    /// <summary>Включённая акция, которая идёт в этот день (несколько — раньше начавшаяся); null — акции нет.</summary>
    public async Task<IncentiveSettings?> ActiveAsync(DateOnly day, CancellationToken ct) =>
        await db.IncentivePrograms.AsNoTracking().Where(p => p.Enabled && p.StartDate <= day && p.EndDate >= day)
            .OrderBy(p => p.StartDate).ThenBy(p => p.Code).FirstOrDefaultAsync(ct) is { } p ? IncentiveSettings.From(p) : null;

    /// <summary>Товары акции: типы из настроек и все их подтипы Linko («Снек» → «Снек 140 г», «Снек 200 г», «Снек 35 г»).</summary>
    public async Task<long[]> ProductIdsAsync(IncentiveSettings s, CancellationToken ct)
    {
        var tree = await LinkoTypeTree.LoadAsync(db, ct);
        var types = SalesCategories.WithDescendants(s.ProductTypeIds, tree.Parents).ToArray();
        return await db.LinkoProducts.AsNoTracking().Where(p => p.TypeId != null && types.Contains(p.TypeId.Value)).Select(p => p.Id).ToArrayAsync(ct);
    }

    /// <summary>Праздники справочника «Продаж» за период акции (для рабочих дней и нормы).</summary>
    public async Task<IReadOnlyDictionary<DateOnly, string>> HolidaysAsync(IncentiveSettings s, CancellationToken ct) =>
        (await db.SalesHolidays.AsNoTracking().Where(h => h.Date >= s.Start && h.Date <= s.End).Select(h => new { h.Date, h.Name }).ToListAsync(ct))
        .GroupBy(h => h.Date).ToDictionary(g => g.Key, g => g.First().Name);

    public async Task<DateTimeOffset?> OrdersAsOfAsync(CancellationToken ct) =>
        await db.LinkoSyncStates.AsNoTracking().Where(x => x.Entity == "orders").Select(x => x.LastSuccessAt).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Накоплено участников на момент. Доставлено — строки товаров акции в доставленных заказах ТП (Sales:FieldSoldStatuses) с приёмкой с
    /// начала акции по вчера (не позже конца акции); возвраты — доставленные возвраты этих строк, созданные по вчера, — днём приёмки заказа;
    /// сегодня — заказы ТП за день (кроме отменённых; в 13:00 — созданные до Until), если день в акции.
    /// </summary>
    public async Task<IncentiveStandings> StandingsAsync(IncentiveSettings s, IReadOnlyCollection<IncentiveAgent> agents, IncentiveMoment moment, CancellationToken ct)
    {
        var products = await ProductIdsAsync(s, ct);
        var ids = agents.Select(a => a.Id).Distinct().ToArray();
        var yesterday = moment.Date.AddDays(-1);
        var until = yesterday < s.End ? yesterday : s.End;
        var days = new Dictionary<long, Dictionary<DateOnly, decimal>>();
        var delivered = new Dictionary<long, decimal>();
        var returned = new Dictionary<long, decimal>();
        var today = new Dictionary<long, decimal>();

        // Итог по ТП и кг по дням: возврат уменьшает день приёмки своего заказа.
        void Add(Dictionary<long, decimal> totals, long agent, DateOnly day, decimal kg, int sign = 1)
        {
            totals[agent] = totals.GetValueOrDefault(agent) + kg;
            var own = days.TryGetValue(agent, out var d) ? d : days[agent] = [];
            own[day] = own.GetValueOrDefault(day) + sign * kg;
        }

        if (ids.Length > 0 && products.Length > 0 && until >= s.Start)
        {
            var sold = sales.FieldSoldStatuses;
            var back = sales.ReturnStatuses;
            var start = s.Start;
            var lines = await (from l in db.LinkoOrderLines.AsNoTracking()
                    join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                    where o.AgentId != null && ids.Contains(o.AgentId.Value) && sold.Contains(o.Status) && o.AcceptedDate >= start && o.AcceptedDate <= until
                          && l.ProductId != null && products.Contains(l.ProductId.Value)
                    group l by new { Agent = o.AgentId!.Value, Day = o.AcceptedDate!.Value, o.BranchName } into g
                    select new { g.Key.Agent, g.Key.Day, g.Key.BranchName, Kg = g.Sum(x => x.TotalWeight) })
                .ToListAsync(ct);
            foreach (var r in lines.Where(r => Counted(r.BranchName)))
            {
                Add(delivered, r.Agent, r.Day, r.Kg);
            }

            var returns = await (from rl in db.LinkoOrderReturnLines.AsNoTracking()
                    join r in db.LinkoOrderReturns.AsNoTracking() on rl.ReturnId equals r.Id
                    join o in db.LinkoOrders.AsNoTracking() on rl.OrderId equals (long?)o.Id
                    where back.Contains(r.Status) && r.CreatedDate <= yesterday && rl.ProductId != null && products.Contains(rl.ProductId.Value)
                          && o.AgentId != null && ids.Contains(o.AgentId.Value) && sold.Contains(o.Status) && o.AcceptedDate >= start && o.AcceptedDate <= until
                    group rl by new { Agent = o.AgentId!.Value, Day = o.AcceptedDate!.Value, o.BranchName } into g
                    select new { g.Key.Agent, g.Key.Day, g.Key.BranchName, Kg = g.Sum(x => x.TotalWeight) })
                .ToListAsync(ct);
            foreach (var r in returns.Where(r => Counted(r.BranchName)))
            {
                Add(returned, r.Agent, r.Day, r.Kg, -1);
            }
        }

        if (moment.WithToday && s.Covers(moment.Date) && ids.Length > 0 && products.Length > 0)
        {
            var taken = await TakenAsync(products, ids, moment.Date, moment.Date, moment.Until, ct);
            foreach (var ((agent, day), kg) in taken)
            {
                Add(today, agent, day, kg);
            }
        }

        var standings = agents.DistinctBy(a => a.Id).Select(a => new IncentiveStanding(a, s.IsWholesale(a.Id), delivered.GetValueOrDefault(a.Id),
            returned.GetValueOrDefault(a.Id), today.GetValueOrDefault(a.Id), days.TryGetValue(a.Id, out var d) ? d : new Dictionary<DateOnly, decimal>())).ToList();
        return new IncentiveStandings(s, moment, standings, await OrdersAsOfAsync(ct));
    }

    /// <summary>
    /// Лучшие клиенты ТП для дозаказа (ТЗ, раздел 6): ТОП-5 ТТ по кг товара акции за 60 дней до date (доставлено по дате приёмки − доставленные
    /// возвраты по этим заказам), цель на неделю — ≈ 60% среднего недельного закупа, до 5 кг.
    /// </summary>
    public async Task<IReadOnlyList<IncentiveTopMarket>> TopMarketsAsync(IncentiveSettings s, long agentId, DateOnly date, CancellationToken ct)
    {
        var products = await ProductIdsAsync(s, ct);
        if (products.Length == 0)
        {
            return [];
        }

        var first = date.AddDays(-IncentiveRules.TopDays);
        var last = date.AddDays(-1);
        var sold = sales.FieldSoldStatuses;
        var back = sales.ReturnStatuses;
        var delivered = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.AgentId == agentId && o.MarketId != null && sold.Contains(o.Status) && o.AcceptedDate >= first && o.AcceptedDate <= last
                      && l.ProductId != null && products.Contains(l.ProductId.Value)
                group l by new { Market = o.MarketId!.Value, o.BranchName } into g
                select new { g.Key.Market, g.Key.BranchName, Kg = g.Sum(x => x.TotalWeight) })
            .ToListAsync(ct);
        var returned = await (from rl in db.LinkoOrderReturnLines.AsNoTracking()
                join r in db.LinkoOrderReturns.AsNoTracking() on rl.ReturnId equals r.Id
                join o in db.LinkoOrders.AsNoTracking() on rl.OrderId equals (long?)o.Id
                where back.Contains(r.Status) && r.CreatedDate <= last && rl.ProductId != null && products.Contains(rl.ProductId.Value)
                      && o.AgentId == agentId && o.MarketId != null && sold.Contains(o.Status) && o.AcceptedDate >= first && o.AcceptedDate <= last
                group rl by o.MarketId!.Value into g
                select new { Market = g.Key, Kg = g.Sum(x => x.TotalWeight) })
            .ToDictionaryAsync(x => x.Market, x => x.Kg, ct);
        var top = delivered.Where(x => Counted(x.BranchName)).GroupBy(x => x.Market)
            .Select(g => (Market: g.Key, Kg: g.Sum(x => x.Kg) - returned.GetValueOrDefault(g.Key)))
            .Where(x => x.Kg > 0).OrderByDescending(x => x.Kg).ThenBy(x => x.Market).Take(IncentiveRules.TopMarkets).ToList();
        var ids = top.Select(x => x.Market).ToArray();
        var names = await db.LinkoMarkets.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        return top.Select(x => new IncentiveTopMarket(x.Market, names.GetValueOrDefault(x.Market, $"#{x.Market}"), x.Kg, IncentiveRules.WeekTarget(x.Kg))).ToList();
    }

    /// <summary>
    /// Кг товаров акции во взятых заказах ТП по дням (день ввода, все статусы кроме отменённых — как факт дня в автоотчётах); в последний день
    /// — заказы, созданные до lastDayUntil (13:00). Для карточки недели («кг по дням») и «сегодня».
    /// </summary>
    public async Task<Dictionary<(long Agent, DateOnly Day), decimal>> TakenAsync(IncentiveSettings s, IReadOnlyCollection<long> agents, DateOnly from, DateOnly to,
        TimeOnly? lastDayUntil, CancellationToken ct) =>
        await TakenAsync(await ProductIdsAsync(s, ct), agents.Distinct().ToArray(), from < s.Start ? s.Start : from, to > s.End ? s.End : to, lastDayUntil, ct);

    private async Task<Dictionary<(long Agent, DateOnly Day), decimal>> TakenAsync(long[] products, long[] ids, DateOnly first, DateOnly last, TimeOnly? lastDayUntil,
        CancellationToken ct)
    {
        if (ids.Length == 0 || products.Length == 0 || last < first)
        {
            return [];
        }

        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.AgentId != null && ids.Contains(o.AgentId.Value) && o.CreatedDate >= first && o.CreatedDate <= last && !NotTaken.Contains(o.Status)
                      && l.ProductId != null && products.Contains(l.ProductId.Value)
                select new { Agent = o.AgentId!.Value, o.CreatedDate, o.CreatedAt, o.BranchName, l.TotalWeight })
            .ToListAsync(ct);
        return rows
            .Where(r => Counted(r.BranchName) && (r.CreatedDate != last || lastDayUntil is not { } u || TimeOnly.FromDateTime(r.CreatedAt) < u))
            .GroupBy(r => (r.Agent, r.CreatedDate))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalWeight));
    }

    private bool Counted(string? branch) => !sales.IsExcludedBranch(branch) && !sales.IsIgnoredBranch(branch);
}

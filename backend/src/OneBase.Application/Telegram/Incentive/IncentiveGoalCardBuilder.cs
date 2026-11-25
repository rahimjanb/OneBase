using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Battle;
using OneBase.Domain.Incentive;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Данные семи карточек «Целей на маршрут» v2 (ТЗ v2 от 24.11.2026, раздел 6): ТП — маршрут дня 08:30, полдень 13:00, итог дня 18:00, итог недели
/// (сб 18:00), итог месяца (30.11); СВР — итог дня команды (18:30); РМ — итог недели зоны (сб 19:00). Факт дня — по заказам и визитам Linko, два
/// баланса кешбека (Могул и Twizos) — по журналу начислений. Только чтение Linko; журнал обновляет IncentiveCashbackLedger.
/// </summary>
public sealed class IncentiveGoalCardBuilder(IAppDbContext db, IncentiveRouteService routes, IncentiveGoalService goals, IncentivePromoService promos,
    IncentiveCashbackLedger ledger)
{
    /// <summary>Как часто перед карточками обновлять журнал кешбека (рассылка — сотни карточек подряд).</summary>
    public static readonly TimeSpan LedgerMaxAge = TimeSpan.FromMinutes(3);

    /// <summary>Карточки v2 у запуска: день в периоде целей v2 и запуск из семи карточек (рейтинг и сводка руководства — прежние).</summary>
    public static bool Applies(IncentiveSettings s, string slot, DateOnly date) =>
        IncentiveRouteService.V2(s, date) is not null && slot is not (IncentiveSlot.Rating or IncentiveSlot.Summary);

    public Task RefreshAsync(IncentiveSettings s, DateOnly date, CancellationToken ct) => ledger.RefreshIfStaleAsync(s, date, LedgerMaxAge, ct);

    /// <summary>
    /// Карточка дня ТП: итоги маршрута, балансы (оформлено — за день; можно ещё — утром кешбек всех целей дня, потом — невзятых), ТОП SKU региона.
    /// </summary>
    public async Task<IncentiveGoalDay> DayAsync(IncentiveSettings s, IncentiveAgent agent, IncentiveRoute route, string slot, DateOnly date,
        Dictionary<string, (IReadOnlyList<IncentiveTopSku>, IReadOnlyList<IncentiveTopSku>)> topCache, CancellationToken ct)
    {
        var totals = IncentiveGoalTotals.Of(route);
        var morning = slot == IncentiveSlot.Morning;
        var balances = await BalancesAsync(s, [agent.Id], date, date, false, morning ? totals.MogulGoalCashback : totals.MogulLeft,
            morning ? totals.TwizosGoalCashback : totals.TwizosLeft, ct);
        var branch = (await promos.BranchesAsync([agent.Id], ct)).FirstOrDefault() ?? "";
        if (!topCache.TryGetValue(branch, out var top))
        {
            topCache[branch] = top = branch.Length == 0 ? ([], []) : await promos.TopSkuAsync(s, [branch], date, ct);
        }

        return new IncentiveGoalDay(route, totals, balances, top.Item1, top.Item2, agent.Region);
    }

    /// <summary>
    /// Итог недели ТП (сб 18:00): дни с целями v2 (6 полян), кешбек недели, балансы (к выплате — с этой неделей: выплата в пн–вт), недели выплат,
    /// клиенты Twizos, новые, стойки, кг, осталось спящих.
    /// </summary>
    public async Task<IncentiveGoalWeek> WeekAsync(IncentiveSettings s, IncentiveGoalsPromo g, IncentiveAgent agent, DateOnly date, CancellationToken ct)
    {
        var monday = BattleRules.Monday(date);
        var saturday = monday.AddDays(5);
        var days = (await routes.DaysAsync(s, agent.Id, monday, date, null, ct))
            .Where(d => d.Stores.Any(x => x.Goal is not null))
            .Select(d => new IncentiveGoalWeekDay(d.Date, d, IncentiveGoalTotals.Of(d)))
            .ToList();
        var sunday = monday.AddDays(6);
        var mogul = await ledger.RowsAsync(s, IncentiveCashbackKind.Mogul, [agent.Id], monday, sunday, ct);
        var twizos = await ledger.RowsAsync(s, IncentiveCashbackKind.Twizos, [agent.Id], monday, sunday, ct);
        var sleepingLeft = s.Offers.Yamelly is not null
            ? (await promos.SleepingAsync(s, [agent.Id], date, null, ct))[agent.Id].Count(p => p.WokenOn is null)
            : 0;
        var last = g.End < saturday.AddDays(7);
        var minCashback = s.Offers.Yamelly?.MinCashback ?? 0;
        var balances = await BalancesAsync(s, [agent.Id], monday, date, true, last ? 0 : sleepingLeft * minCashback, last ? 0 : null, ct);
        var weeks = await WeeksAsync(s, g, [agent.Id], date, ct);
        return new IncentiveGoalWeek(monday, saturday, days, balances, weeks,
            mogul.Where(x => x.Status != IncentiveCashbackStatus.Cancelled).Sum(x => x.Issued),
            twizos.Where(x => x.Status != IncentiveCashbackStatus.Cancelled).Sum(x => x.Issued),
            days.Sum(d => d.Totals.Woken), days.Sum(d => d.Totals.MogulGoals), days.Sum(d => d.Totals.TwizosClients), days.Sum(d => d.Totals.TwizosGoals),
            days.Sum(d => d.Totals.TwizosNew), twizos.Count(x => x.Stand && x.Status != IncentiveCashbackStatus.Cancelled), days.Sum(d => d.Totals.TwizosKg),
            sleepingLeft, last);
    }

    /// <summary>
    /// Итог месяца ТП (30.11): спящие и разбуженные, Twizos с начала целей v2 (клиенты, новые, стойки, кг), Могул по неделям, «Мой заработок» —
    /// Могул и Twizos (начислено, выплачено), лучший агент дня — из прежнего расчёта (earnings).
    /// </summary>
    public async Task<IncentiveGoalMonth> MonthAsync(IncentiveSettings s, IncentiveGoalsPromo g, IncentiveAgent agent, DateOnly date, IncentiveEarnings? earnings,
        CancellationToken ct)
    {
        var ym = s.Offers.Yamelly;
        var points = ym is null ? [] : (await promos.SleepingAsync(s, [agent.Id], date, null, ct))[agent.Id];
        var mogul = (await ledger.RowsAsync(s, IncentiveCashbackKind.Mogul, [agent.Id], DateOnly.MinValue, date, ct))
            .Where(x => x.Status != IncentiveCashbackStatus.Cancelled).ToList();
        var twizos = (await ledger.RowsAsync(s, IncentiveCashbackKind.Twizos, [agent.Id], g.Start, date, ct))
            .Where(x => x.Status != IncentiveCashbackStatus.Cancelled).ToList();
        var weeks = new List<IncentiveGoalMonthWeek>();
        if (ym is not null)
        {
            var end = date < ym.End ? date : ym.End;
            for (var from = ym.Start; from <= end;)
            {
                var to = BattleRules.Monday(from).AddDays(6);
                to = to > end ? end : to;
                var (f, t) = (from, to);
                weeks.Add(new IncentiveGoalMonthWeek(f, t, points.Count(p => p.WokenOn is { } w && w >= f && w <= t),
                    mogul.Where(x => x.OrderDate >= f && x.OrderDate <= t).Sum(x => x.Issued)));
                from = to.AddDays(1);
            }
        }

        var kg = (await promos.TwizosOrdersAsync(s, [agent.Id], g.Start, date, null, ct)).Sum(o => o.Kg);
        var paid = await db.IncentivePayouts.AsNoTracking().Where(p => p.ProgramId == s.Id && p.AgentId == agent.Id)
            .Select(p => new { p.Kind, p.Amount }).ToListAsync(ct);
        static decimal Accrued(IEnumerable<IncentiveCashbackAccrual> rows) =>
            rows.Sum(x => x.Status == IncentiveCashbackStatus.Confirmed ? x.Amount : x.Issued);
        return new IncentiveGoalMonth(points.Count, points.Count(p => p.WokenOn is not null), ym?.WakeShare ?? 0, g.Start,
            twizos.Select(x => x.MarketId).Distinct().Count(),
            twizos.Where(x => x.Segment != IncentiveTwizosSegment.Existing).Select(x => x.MarketId).Distinct().Count(),
            twizos.Count(x => x.Stand), kg, weeks,
            new IncentiveGoalEarn(Accrued(mogul), paid.Where(p => p.Kind == IncentivePayoutKind.Cashback).Sum(p => p.Amount)),
            new IncentiveGoalEarn(Accrued(twizos), paid.Where(p => p.Kind == IncentivePayoutKind.Twizos).Sum(p => p.Amount)),
            s.Offers.Best is null || earnings is null ? null : new IncentiveGoalEarn(earnings.BestAccrued, earnings.BestPaid),
            earnings?.BestWins ?? 0, s.Offers.Best?.End);
    }

    /// <summary>
    /// Итог дня команды СВР (18:30): ТП команды с итогами маршрута дня (цели не записываются: нет утренней карточки — считаются на лету), балансы
    /// команды, подарки (выдано за день — по заказам, остаток — склад региона) и стойки (выдано, остаток, цель региона).
    /// </summary>
    public async Task<IncentiveGoalTeam> TeamAsync(IncentiveSettings s, IncentiveGoalsPromo g, IncentiveTeamSpec spec, DateOnly date, CancellationToken ct)
    {
        var agents = spec.Regions.SelectMany(r => r.Agents).DistinctBy(a => a.Id).ToList();
        var rows = new List<IncentiveGoalTeamRow>();
        foreach (var agent in agents)
        {
            var route = await routes.DayAsync(s, agent.Id, date, null, ct, persist: false);
            rows.Add(new IncentiveGoalTeamRow(agent, IncentiveGoalTotals.Of(route)));
        }

        // По результату, как на образце 6: кешбек дня (Могул + Twizos), затем взятые цели.
        rows = rows.OrderByDescending(r => r.Totals.Cashback).ThenByDescending(r => r.Totals.Woken + r.Totals.TwizosClients)
            .ThenBy(r => r.Agent.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var region = spec.Regions.FirstOrDefault()?.Name ?? "";
        var stock = goals.ByRegion(g, await promos.GiftStockAsync(s, ct));
        var regionStock = spec.Regions.Select(r => stock.GetValueOrDefault(r.Name)).Where(x => x is not null).SelectMany(x => x!)
            .GroupBy(kv => kv.Key).ToDictionary(x => x.Key, x => (int)Math.Floor(x.Sum(kv => kv.Value)));
        var items = new List<IncentiveGoalStock>();
        foreach (var gift in s.Offers.Twizos?.GiftTiers ?? [])
        {
            items.Add(new IncentiveGoalStock(gift.Gift, rows.Sum(r => r.Totals.Gifts.GetValueOrDefault(gift.Gift)),
                regionStock.TryGetValue(gift.Gift, out var left) ? left : null));
        }

        var stands = await goals.StandStockAsync(g, ct);
        var quotas = await goals.StandQuotasAsync(g, date, ct);
        var regionNames = spec.Regions.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int? standLeft = regionNames.Any(stands.ContainsKey) ? regionNames.Sum(n => stands.GetValueOrDefault(n)) : null;
        var target = quotas.Where(q => regionNames.Contains(q.Region)).Select(q => (int?)q.Target).Sum();
        items.Add(new IncentiveGoalStock("stand", rows.Sum(r => r.Totals.Stands), standLeft, target));
        var balances = await BalancesAsync(s, agents.Select(a => a.Id).ToList(), date, date, false, rows.Sum(r => r.Totals.MogulLeft), rows.Sum(r => r.Totals.TwizosLeft), ct);
        return new IncentiveGoalTeam(spec.Title, region, rows, items, balances);
    }

    /// <summary>
    /// Итог недели зоны РМ (сб 19:00): регионы — спящие (в списке и разбужено с начала YM-01), клиенты Twizos за неделю и новые, кг, стойки (всего с
    /// начала продаж Twizos и за неделю), подарки за неделю и остаток на складе региона, балансы региона; зона — сумма.
    /// </summary>
    public async Task<IncentiveGoalZone> ZoneAsync(IncentiveSettings s, IncentiveGoalsPromo g, IncentiveTeamSpec spec, DateOnly date, CancellationToken ct)
    {
        var monday = BattleRules.Monday(date);
        var sunday = monday.AddDays(6);
        var last = g.End < monday.AddDays(12);
        var all = spec.Regions.SelectMany(r => r.Agents).Select(a => a.Id).Distinct().ToList();
        var sleeping = s.Offers.Yamelly is null ? [] : await promos.SleepingAsync(s, all, date, null, ct);
        var twizos = (await ledger.RowsAsync(s, IncentiveCashbackKind.Twizos, all, monday, sunday, ct)).Where(x => x.Status != IncentiveCashbackStatus.Cancelled).ToList();
        var orders = await promos.TwizosOrdersAsync(s, all, monday, date, null, ct);
        var stands = await goals.StandOrdersAsync(g, all, g.HistoryFrom, date, ct);
        var stock = goals.ByRegion(g, await promos.GiftStockAsync(s, ct));
        var minCashback = s.Offers.Yamelly?.MinCashback ?? 0;
        var regions = new List<IncentiveGoalZoneRegion>();
        foreach (var region in spec.Regions)
        {
            var ids = region.Agents.Select(a => a.Id).ToHashSet();
            var points = ids.SelectMany(id => sleeping.GetValueOrDefault(id) ?? []).ToList();
            var tw = twizos.Where(x => ids.Contains(x.AgentId)).ToList();
            var own = orders.Where(o => ids.Contains(o.AgentId)).ToList();
            var st = stands.Where(o => ids.Contains(o.AgentId)).ToList();
            var listed = points.Count;
            var woken = points.Count(p => p.WokenOn is not null);
            var balances = await BalancesAsync(s, ids, monday, date, true, last ? 0 : (listed - woken) * minCashback, last ? 0 : null, ct);
            var giftStock = (stock.GetValueOrDefault(region.Name) ?? []).ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value));
            regions.Add(new IncentiveGoalZoneRegion(region.Name, ids.Count, listed, woken, tw.Select(x => x.MarketId).Distinct().Count(),
                tw.Where(x => x.Segment != IncentiveTwizosSegment.Existing).Select(x => x.MarketId).Distinct().Count(), own.Sum(o => o.Kg),
                st.Sum(o => o.Count), st.Where(o => o.Created >= monday).Sum(o => o.Count), own.Sum(o => o.BonusGifts.Values.Sum()), giftStock, balances));
        }

        var zone = regions.Aggregate(IncentiveGoalBalances.Zero, (acc, r) => acc.Add(r.Balances));
        return new IncentiveGoalZone(spec.Title, monday, date, regions, zone, last, s.Offers.Yamelly?.WakeShare ?? 0);
    }

    /// <summary>Недели выплат ТП с начала акций по неделю date: доставлено Могул и Twizos (по неделе доставки) и выплачено (оба вида).</summary>
    private async Task<IReadOnlyList<IncentiveGoalWeekRow>> WeeksAsync(IncentiveSettings s, IncentiveGoalsPromo g, IReadOnlyCollection<long> agents, DateOnly date,
        CancellationToken ct)
    {
        var start = s.Offers.Yamelly is { } ym && ym.Start < g.Start ? ym.Start : g.Start;
        var mogul = await ledger.RowsAsync(s, IncentiveCashbackKind.Mogul, agents, DateOnly.MinValue, date, ct);
        var twizos = await ledger.RowsAsync(s, IncentiveCashbackKind.Twizos, agents, DateOnly.MinValue, date, ct);
        var ids = agents.ToArray();
        var paid = await db.IncentivePayouts.AsNoTracking()
            .Where(p => p.ProgramId == s.Id && ids.Contains(p.AgentId) && (p.Kind == IncentivePayoutKind.Cashback || p.Kind == IncentivePayoutKind.Twizos))
            .Select(p => new { p.WeekStart, p.Amount }).ToListAsync(ct);
        static decimal Delivered(IEnumerable<IncentiveCashbackAccrual> rows, DateOnly monday, DateOnly date) =>
            rows.Where(x => x.Status == IncentiveCashbackStatus.Confirmed && x.DeliveredOn is { } d && d <= date && BattleRules.Monday(d) == monday).Sum(x => x.Amount);
        var result = new List<IncentiveGoalWeekRow>();
        for (var monday = BattleRules.Monday(start); monday <= BattleRules.Monday(date); monday = monday.AddDays(7))
        {
            var row = new IncentiveGoalWeekRow(monday, monday.AddDays(6), Delivered(mogul, monday, date), Delivered(twizos, monday, date),
                paid.Where(p => p.WeekStart == monday).Sum(p => p.Amount));
            if (row.Mogul + row.Twizos + row.Paid > 0)
            {
                result.Add(row);
            }
        }

        return result;
    }

    /// <summary>
    /// Балансы двух кешбеков по журналу: оформлено с from по to (кроме отменённого), ждёт доставки, подтверждено (доставлено по to), выплачено, к
    /// выплате — подтверждённое за прошедшие недели минус выплаченное за них (weekEnd — и за неделю to: итог недели, выплата в пн–вт), можно ещё.
    /// </summary>
    private async Task<IncentiveGoalBalances> BalancesAsync(IncentiveSettings s, IReadOnlyCollection<long> agents, DateOnly from, DateOnly to, bool weekEnd,
        decimal? mogulCanEarn, decimal? twizosCanEarn, CancellationToken ct)
    {
        var ids = agents.Distinct().ToArray();
        var paid = await db.IncentivePayouts.AsNoTracking().Where(p => p.ProgramId == s.Id && ids.Contains(p.AgentId))
            .Select(p => new { p.Kind, p.WeekStart, p.Amount }).ToListAsync(ct);
        var thisWeek = BattleRules.Monday(to);

        async Task<IncentiveGoalBalance> One(string kind, decimal? canEarn)
        {
            var rows = await ledger.RowsAsync(s, kind, ids, DateOnly.MinValue, to, ct);
            var payout = IncentiveCashbackKind.PayoutKind(kind);
            var confirmed = rows.Where(x => x.Status == IncentiveCashbackStatus.Confirmed && x.DeliveredOn is { } d && d <= to).ToList();
            var confirmedIds = confirmed.Select(x => x.Id).ToHashSet();
            var weekPaid = paid.Where(p => p.Kind == payout).GroupBy(p => p.WeekStart).ToDictionary(x => x.Key, x => x.Sum(p => p.Amount));
            var toPay = confirmed.GroupBy(x => BattleRules.Monday(x.DeliveredOn!.Value))
                .Where(w => w.Key < thisWeek || (weekEnd && w.Key == thisWeek))
                .Sum(w => Math.Max(0, w.Sum(x => x.Amount) - weekPaid.GetValueOrDefault(w.Key)));
            return new IncentiveGoalBalance(
                rows.Where(x => x.Status != IncentiveCashbackStatus.Cancelled && x.OrderDate >= from).Sum(x => x.Issued),
                rows.Where(x => x.Status != IncentiveCashbackStatus.Cancelled && !confirmedIds.Contains(x.Id)).Sum(x => x.Issued),
                confirmed.Sum(x => x.Amount),
                weekPaid.Values.Sum(),
                toPay,
                canEarn);
        }

        return new IncentiveGoalBalances(await One(IncentiveCashbackKind.Mogul, mogulCanEarn), await One(IncentiveCashbackKind.Twizos, twizosCanEarn));
    }
}

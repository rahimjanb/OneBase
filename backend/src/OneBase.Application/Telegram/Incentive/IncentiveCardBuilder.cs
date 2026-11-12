using OneBase.Domain.Incentive;
using OneBase.Application.Telegram.Battle;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Собирает карточки накопительной акции на момент запуска из расчёта акции: накоплено и место (среди всех участников), ход к призам и норма
/// (утром и в 13:00 — с сегодняшнего дня, вечером, в субботу и в конце месяца — с завтрашнего), маршрут дня, неделя «6 полян», месяц «город
/// героя», рейтинг, сводка команды. Только чтение Linko; цели магазинов на день ставит IncentiveRouteService (если Persist).
/// </summary>
public sealed class IncentiveCardBuilder(IncentiveService incentive, IncentiveRouteService routes, IncentivePromoService promos, IncentiveResultsService results)
{
    public static readonly TimeOnly DefaultDayStart = new(8, 30);
    public static readonly TimeOnly DefaultMidday = new(13, 0);
    public static readonly TimeOnly DefaultAfternoon = new(16, 0);
    public static readonly TimeOnly DefaultDayEnd = new(18, 0);

    public async Task<IncentiveCards> BuildAsync(IncentiveSettings s, IncentiveCardRequest r, CancellationToken ct)
    {
        var holidays = await incentive.HolidaysAsync(s, ct);
        if (r.Persist)
        {
            // Первая карточка по расписанию с начала Yamelly записывает снимок списка спящих по Linko (до него список считается без записи).
            await results.EnsureSleepingAsync(s, r.Date, ct);
        }

        var slot = r.Slot;
        var midday = IncentiveSlot.IsInterim(slot);
        var until = midday ? r.Until ?? (slot == IncentiveSlot.Afternoon ? DefaultAfternoon : DefaultMidday) : (TimeOnly?)null;
        var moment = slot == IncentiveSlot.Morning ? IncentiveMoment.Morning(r.Date) : IncentiveMoment.Day(r.Date, until);
        // Утро — план дня: магазины «впереди», заказы дня не учитываются (как и «сегодня» — 0), даже если карточку строят позже 08:30.
        var routeUntil = slot == IncentiveSlot.Morning ? TimeOnly.MinValue : until;
        var teamAgents = r.Team?.Regions.SelectMany(x => x.Agents) ?? [];
        var everyone = r.Participants.Concat(r.Cards).Concat(teamAgents).DistinctBy(a => a.Id).ToList();
        var standings = await incentive.StandingsAsync(s, everyone, moment, ct);

        // Норма — на дни, которые ещё можно работать: утром и в полдень — с сегодняшнего, после итога дня — с завтрашнего.
        var firstDay = slot == IncentiveSlot.Morning || IncentiveSlot.IsInterim(slot) ? r.Date : r.Date.AddDays(1);
        IncentiveProgress Progress(decimal total, bool wholesale = false) => IncentiveRules.Progress(s, total, firstDay, holidays, wholesale);
        var lastWorked = slot == IncentiveSlot.Morning ? r.Date.AddDays(-1) : r.Date;
        var worked = Math.Max(1, IncentiveRules.WorkDays(s.Start, lastWorked < s.End ? lastWorked : s.End, holidays));

        var monday = BattleRules.Monday(r.Date);
        IncentiveStandings? weekStart = null;
        IncentiveStandings? lastWeek = null;
        if (slot == IncentiveSlot.Week)
        {
            weekStart = await incentive.StandingsAsync(s, r.Cards, IncentiveMoment.Morning(monday), ct);
            lastWeek = await incentive.StandingsAsync(s, everyone, IncentiveMoment.Day(monday.AddDays(-2)), ct);
        }

        // Акции ноября: TW-01 по дням периода карточки (день, неделя пн–…, месяц), ТОП SKU — по региону, один раз на филиал.
        var promoted = s.Offers.Twizos is not null || s.Offers.Yamelly is not null || s.Offers.Best is not null;
        var promoUntil = slot == IncentiveSlot.Morning ? TimeOnly.MinValue : until;
        var periodFrom = slot switch
        {
            IncentiveSlot.Week => monday,
            IncentiveSlot.Month => new DateOnly(r.Date.Year, r.Date.Month, 1),
            _ => r.Date,
        };
        var names = everyone.ToDictionary(a => a.Id, a => a.Name);
        var bestDays = promoted ? await promos.BestDaysAsync(s, names.Keys, periodFrom, r.Date, promoUntil, ct) : [];
        var topCache = new Dictionary<string, (IReadOnlyList<IncentiveTopSku>, IReadOnlyList<IncentiveTopSku>)>();

        var cards = new List<IncentiveAgentCard>();
        foreach (var agent in r.Cards.DistinctBy(a => a.Id))
        {
            var standing = standings.Of(agent.Id)!;
            var progress = Progress(standing.Total, standing.Wholesale);
            var dayStart = IncentiveRules.Progress(s, standing.Total - standing.Today, r.Date, holidays, standing.Wholesale);
            var route = IncentiveSlot.IsDay(slot) ? await routes.DayAsync(s, agent.Id, r.Date, routeUntil, ct, r.Persist, dayStart.Norm) : null;
            var week = slot == IncentiveSlot.Week ? await WeekAsync(s, agent.Id, monday, r.Date, weekStart!.Of(agent.Id)!.Total, holidays, ct) : null;
            var month = slot == IncentiveSlot.Month ? Month(s, standing, progress, r.Date, holidays) : null;
            var promo = promoted
                ? await PromoAsync(s, r, agent, standing, dayStart, route, week, periodFrom, promoUntil, bestDays, names, topCache, holidays, ct)
                : null;
            cards.Add(new IncentiveAgentCard(agent, standing, progress, dayStart, route, week, month, standings.Rank(agent.Id), lastWeek?.Rank(agent.Id),
                standings.Rating(standing.Wholesale).Count, promo));
        }

        // Карточки руководителей по акциям ноября (команда СВР, зона РМ, сводка) — вместо сводки команды накопительной.
        IncentiveTeamDay? teamDay = null;
        IncentiveZone? zone = null;
        if (IncentiveSlot.IsManagers(slot) && r.Team is not null)
        {
            if (slot == IncentiveSlot.Team)
            {
                teamDay = await TeamDayAsync(s, r, standings, holidays, bestDays, ct);
            }
            else
            {
                zone = await ZoneAsync(s, r, standings, names.Keys, holidays, ct);
            }
        }

        IncentiveTeam? team = null;
        if (r.Team is { } spec && !IncentiveSlot.IsManagers(slot))
        {
            var regions = new List<IncentiveTeamRegion>();
            foreach (var region in spec.Regions)
            {
                var rows = new List<IncentiveTeamRow>();
                foreach (var agent in region.Agents.DistinctBy(a => a.Id))
                {
                    var st = standings.Of(agent.Id)!;
                    var norm = IncentiveRules.Progress(s, st.Total - st.Today, r.Date, holidays, st.Wholesale).Norm;
                    decimal? routeKg = IncentiveSlot.IsDay(slot) ? (await routes.DayAsync(s, agent.Id, r.Date, routeUntil, ct, r.Persist, norm)).TargetKg : null;
                    rows.Add(new IncentiveTeamRow(agent, st.Total, st.Today, routeKg, Progress(st.Total, st.Wholesale), Math.Round(st.Total / worked, 1), standings.Rank(agent.Id), st.Wholesale));
                }

                regions.Add(new IncentiveTeamRegion(region.Name, region.Supervisor, rows.OrderByDescending(x => x.Total).ThenBy(x => x.Agent.Name).ToList()));
            }

            team = new IncentiveTeam(spec.Role, spec.Title, regions);
        }

        IncentiveRating? rating = null;
        if (slot == IncentiveSlot.Rating)
        {
            var scope = r.RatingAgents?.ToHashSet();
            List<IncentiveRatingRow> Rows(bool wholesale) => standings.Rating(wholesale)
                .Where(a => scope is null || scope.Contains(a.Agent.Id))
                .Where(a => !wholesale || a.Total > 0)
                .Take(10)
                .Select((a, i) => new IncentiveRatingRow(i + 1, a.Agent, a.Total, IncentiveRules.Reached(s.TiersFor(wholesale), a.Total),
                    IncentiveRules.Next(s.TiersFor(wholesale), a.Total), IncentiveRules.Next(s.TiersFor(wholesale), a.Total) is { } next ? next.Kg - a.Total : null))
                .ToList();
            rating = new IncentiveRating(r.RatingScope, Rows(false), s.SeparateWholesale ? Rows(true) : [], Progress(0).WorkDaysLeft);
        }

        var share = IncentiveRules.MiddayShare(r.DayStart ?? DefaultDayStart, until ?? DefaultMidday, r.DayEnd ?? DefaultDayEnd);
        return new IncentiveCards(slot, r.Date, s, r.Language, cards, team, rating, share, standings.OrdersAsOf, r.Stale, teamDay, zone);
    }

    /// <summary>
    /// Итог дня команды СВР (19:30): ТП команды — спящие маршрута и разбужено, кг Twizos за день, цель маршрута (нет маршрута — норма дня),
    /// наборов с подарком, кэшбек дня, отказов, ход к призам с завтрашнего дня, место TW-01 за день; подарки команды по видам. Цели дня не
    /// записываются: нет утренней карточки — цели считаются на лету.
    /// </summary>
    private async Task<IncentiveTeamDay> TeamDayAsync(IncentiveSettings s, IncentiveCardRequest r, IncentiveStandings standings, IReadOnlyDictionary<DateOnly, string> holidays,
        Dictionary<DateOnly, IReadOnlyList<IncentivePromoRules.BestRow>> bestDays, CancellationToken ct)
    {
        var spec = r.Team!;
        var rows = new List<IncentiveTeamDayRow>();
        var gifts = new Dictionary<string, int>();
        foreach (var agent in spec.Regions.SelectMany(x => x.Agents).DistinctBy(a => a.Id))
        {
            var st = standings.Of(agent.Id)!;
            var dayStart = IncentiveRules.Progress(s, st.Total - st.Today, r.Date, holidays, st.Wholesale);
            var route = await routes.DayAsync(s, agent.Id, r.Date, null, ct, persist: false, dayStart.Norm);
            foreach (var (gift, count) in route.GiftsTaken)
            {
                gifts[gift] = gifts.GetValueOrDefault(gift) + count;
            }

            var best = bestDays.GetValueOrDefault(r.Date)?.FirstOrDefault(x => x.AgentId == agent.Id);
            var goal = route.Stores.Count > 0 && route.TargetKg > 0 ? route.TargetKg : dayStart.Norm;
            var cashback = route.Stores.Concat(route.Extra ?? []).Sum(x => x.Cashback) + (route.OutOfRoute ?? []).Sum(x => x.Cashback);
            rows.Add(new IncentiveTeamDayRow(agent, route.SleepingPlanned, route.SleepingWoken, st.Today, goal, route.GiftsTaken.Values.Sum(), cashback,
                route.Stores.Count(x => x.Status == BattleStoreStatus.Refused && !x.Plain), IncentiveRules.Progress(s, st.Total, r.Date.AddDays(1), holidays, st.Wholesale),
                st.Total, best?.Place, best?.Winner ?? false, st.Wholesale));
        }

        return new IncentiveTeamDay(spec.Title, rows.OrderByDescending(x => x.Kg).ThenByDescending(x => x.SleepingWoken).ThenBy(x => x.Agent.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList(), gifts);
    }

    /// <summary>
    /// Итог недели зоны РМ (неделя пн–день карточки) или сводка руководства (прошлая неделя пн–сб; плюс территории YM-03 и бюджет): по регионам —
    /// спящие в списке и разбужено с начала акции, кг Twizos и наборы за неделю, выданные подарки (бонусные строки Linko), остаток подарков на
    /// складах филиалов региона, победы TW-01 (место — среди всех участников), норма недели; к выплате ТП (подтверждённый кэшбек и призы TW-01 за
    /// неделю минус выплаченное), лидер зоны.
    /// </summary>
    private async Task<IncentiveZone> ZoneAsync(IncentiveSettings s, IncentiveCardRequest r, IncentiveStandings standings, IReadOnlyCollection<long> everyone,
        IReadOnlyDictionary<DateOnly, string> holidays, CancellationToken ct)
    {
        var spec = r.Team!;
        var summary = r.Slot == IncentiveSlot.Summary;
        var monday = BattleRules.Monday(r.Date);
        var from = summary ? monday.AddDays(-7) : monday;
        var to = summary ? from.AddDays(5) : r.Date;
        var ids = spec.Regions.SelectMany(x => x.Agents).Select(a => a.Id).Distinct().ToList();
        var o = s.Offers;
        var sleeping = o.Yamelly is not null ? await promos.SleepingAsync(s, ids, to, null, ct) : [];
        var taken = await incentive.TakenAsync(s, ids, from, to, null, ct);
        var twizos = await promos.TwizosOrdersAsync(s, ids, from, to, null, ct);
        var best = o.Best is not null ? await promos.BestDaysAsync(s, everyone, from, to, null, ct) : [];
        var stock = o.Twizos is not null ? await promos.GiftStockAsync(s, ct) : [];
        var workDays = IncentiveRules.WorkDays(from, to, holidays);
        var regions = new List<IncentiveZoneRegion>();
        foreach (var region in spec.Regions)
        {
            var own = region.Agents.Select(a => a.Id).Distinct().ToHashSet();
            var points = own.SelectMany(a => sleeping.GetValueOrDefault(a) ?? []).ToList();
            var orders = twizos.Where(x => own.Contains(x.AgentId)).ToList();
            var gifts = orders.SelectMany(x => x.BonusGifts).GroupBy(g => g.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
            var branches = own.Count == 0 || stock.Count == 0 ? [] : await promos.BranchesAsync(own, ct);
            var regionStock = branches.Select(b => stock.GetValueOrDefault(b)).OfType<Dictionary<string, decimal>>().SelectMany(x => x)
                .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
            var weekKg = taken.Where(x => own.Contains(x.Key.Agent)).Sum(x => x.Value);
            decimal? norm = null;
            foreach (var agent in own)
            {
                if (standings.Of(agent) is { } st && IncentiveRules.Progress(s, st.Total - weekKg, from, holidays, st.Wholesale).Norm is { } n)
                {
                    norm = (norm ?? 0) + n * workDays;
                }
            }

            regions.Add(new IncentiveZoneRegion(region.Name, region.Supervisor, own.Count, points.Count, points.Count(p => p.WokenOn is { } w && w <= to), weekKg,
                orders.Count(x => x.Gift is not null), gifts, regionStock, best.Values.Sum(day => day.Count(x => x.Winner && own.Contains(x.AgentId))), norm,
                own.Sum(a => standings.Of(a)?.Total ?? 0)));
        }

        var cashback = o.Yamelly is not null ? await promos.CashbackAsync(s, ids, to, null, ct) : [];
        var paid = await promos.PaidAsync(s, ids, from, to, ct);
        var wins = best.Values.Sum(day => day.Count(x => x.Winner && ids.Contains(x.AgentId)));
        var bestToPay = Math.Max(0, wins * (o.Best?.Prize ?? 0) - paid.Where(p => p.Key.Kind == IncentivePayoutKind.Best).Sum(p => p.Value));
        var leader = ids.Select(standings.Of).OfType<IncentiveStanding>().OrderByDescending(x => x.Total).FirstOrDefault();
        IReadOnlyList<IncentiveTerritoryState>? territories = null;
        IncentiveBudgetView? budget = null;
        if (summary)
        {
            territories = await promos.TerritoriesAsync(s, to, ct);
            budget = await BudgetAsync(s, ids, everyone, to, ct);
        }

        return new IncentiveZone(spec.Title, from, to, regions, cashback.Values.Sum(b => b.Weeks.Where(w => w.Monday <= BattleRules.Monday(to)).Sum(w => w.ToPay)), bestToPay,
            leader?.Agent, leader?.Total ?? 0, leader is null ? null : IncentiveRules.Progress(s, leader.Total, to.AddDays(1), holidays, leader.Wholesale), territories, budget,
            ids.Sum(a => standings.Of(a)?.Total ?? 0));
    }

    /// <summary>
    /// Бюджет акций с начала по to: кэшбек начислен (подтверждённый — после возвратов) и выплачен, призы TW-01 (победы × приз) и выплачено, бюджеты из
    /// настроек; подарки выдано (бонусные строки Linko) и выделено на акцию.
    /// </summary>
    private async Task<IncentiveBudgetView> BudgetAsync(IncentiveSettings s, IReadOnlyCollection<long> ids, IReadOnlyCollection<long> everyone, DateOnly to, CancellationToken ct)
    {
        var o = s.Offers;
        var plan = o.Budget ?? new IncentiveBudget();
        var start = new[] { o.Yamelly?.Start, o.Best?.Start }.OfType<DateOnly>().DefaultIfEmpty(s.Start).Min();
        var cashback = o.Yamelly is { } ym
            ? (await promos.YamellyOrdersAsync(s, ids, ym.Start, to, null, ct)).Where(x => x.CashbackStatus != IncentiveCashbackStatus.Cancelled)
                .Sum(x => x.CashbackStatus == IncentiveCashbackStatus.Confirmed ? x.NetCashback : x.Cashback)
            : 0;
        var bestDays = o.Best is { } b ? await promos.BestDaysAsync(s, everyone, b.Start, to < b.End ? to : b.End, null, ct) : [];
        var wins = bestDays.Values.Sum(day => day.Count(x => x.Winner && ids.Contains(x.AgentId)));
        var paid = await promos.PaidAsync(s, ids, start, to, ct);
        var gifts = (await promos.TwizosOrdersAsync(s, ids, start, to, null, ct)).SelectMany(x => x.BonusGifts).GroupBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
        var allotted = (o.Twizos?.GiftTiers ?? []).Where(g => g.Stock > 0).ToDictionary(g => g.Gift, g => g.Stock);
        return new IncentiveBudgetView(cashback, paid.Where(p => p.Key.Kind == IncentivePayoutKind.Cashback).Sum(p => p.Value), plan.Cashback,
            wins * (o.Best?.Prize ?? 0), paid.Where(p => p.Key.Kind == IncentivePayoutKind.Best).Sum(p => p.Value), plan.Best, gifts, allotted);
    }

    /// <summary>
    /// Неделя пн–сб: кг взятых заказов по дням, маршруты дней (поставленные цели), норма дня на понедельник утром и норма недели, лучшие клиенты
    /// для дозаказа (60 дней по день карточки включительно).
    /// </summary>
    private async Task<IncentiveWeekView> WeekAsync(IncentiveSettings s, long agentId, DateOnly monday, DateOnly through, decimal totalMonday,
        IReadOnlyDictionary<DateOnly, string> holidays, CancellationToken ct)
    {
        var saturday = monday.AddDays(5);
        var taken = await incentive.TakenAsync(s, [agentId], monday, through < saturday ? through : saturday, null, ct);
        var days = await routes.DaysAsync(s, agentId, monday, saturday, null, ct);
        var dayNorm = IncentiveRules.Progress(s, totalMonday, monday, holidays, s.IsWholesale(agentId)).Norm;
        decimal? norm = dayNorm is { } n ? n * IncentiveRules.WorkDays(monday, saturday, holidays) : null;
        var top = await incentive.TopMarketsAsync(s, agentId, through.AddDays(1), ct);
        return new IncentiveWeekView(monday, saturday, days.Select(d => new IncentiveWeekDay(d.Date, taken.GetValueOrDefault((agentId, d.Date)), d)).ToList(), dayNorm, norm, top);
    }

    /// <summary>
    /// Месяц: кг месяца и прошлого (доставлено по дате приёмки, сегодня — с заказами дня), недели пн–вс внутри месяца с рабочими днями, норма
    /// недели — ориентир месяца для ближайшей ступени (порог × доля), разложенный по рабочим дням.
    /// </summary>
    private static IncentiveMonthView Month(IncentiveSettings s, IncentiveStanding standing, IncentiveProgress progress, DateOnly date,
        IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var start = new DateOnly(date.Year, date.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var previous = start.AddMonths(-1);
        var tier = progress.Next ?? s.Tiers.LastOrDefault();
        var guide = tier is null ? null : IncentiveRules.MonthGuide(s, tier, date.Year, date.Month);
        var monthDays = IncentiveRules.WorkDays(start, end, holidays);
        var weeks = new List<IncentiveMonthWeek>();
        for (var from = start; from <= end;)
        {
            var to = BattleRules.Monday(from).AddDays(6);
            if (to > end)
            {
                to = end;
            }

            var days = IncentiveRules.WorkDays(from, to, holidays);
            if (days > 0)
            {
                weeks.Add(new IncentiveMonthWeek(weeks.Count + 1, from, to, standing.Between(from, to < date ? to : date),
                    guide is { } g && monthDays > 0 ? Math.Round(g * days / monthDays, 1) : null));
            }

            from = to.AddDays(1);
        }

        return new IncentiveMonthView(date.Year, date.Month, standing.Month(date.Year, date.Month), standing.Month(previous.Year, previous.Month), weeks, tier, guide,
            date.Year == s.End.Year && date.Month == s.End.Month || date >= s.End);
    }

    /// <summary>
    /// Акции ноября на карточке ТП: спящие (в списке, разбужено, цель, визитов в день) и баланс кэшбека на момент; TW-01 за день и побед за период;
    /// ТОП SKU региона; как собраны цели маршрута; дни недели (кг, наборы, спящие, кэшбек, победа); наборы и подарки за период; заработок (неделя,
    /// месяц): кэшбек начислен (подтверждённый — после возвратов) и призы TW-01, выплачено — по отметкам бухгалтерии.
    /// </summary>
    private async Task<IncentivePromoView> PromoAsync(IncentiveSettings s, IncentiveCardRequest r, IncentiveAgent agent, IncentiveStanding standing,
        IncentiveProgress dayStart, IncentiveRoute? route, IncentiveWeekView? week, DateOnly periodFrom, TimeOnly? until,
        Dictionary<DateOnly, IReadOnlyList<IncentivePromoRules.BestRow>> bestDays, IReadOnlyDictionary<long, string> names,
        Dictionary<string, (IReadOnlyList<IncentiveTopSku>, IReadOnlyList<IncentiveTopSku>)> topCache, IReadOnlyDictionary<DateOnly, string> holidays,
        CancellationToken ct)
    {
        var o = s.Offers;
        IncentiveSleepingView? sleeping = null;
        IncentiveCashbackBalance? cashback = null;
        if (o.Yamelly is { } ym)
        {
            var points = (await promos.SleepingAsync(s, [agent.Id], r.Date, until, ct))[agent.Id];
            var wokenBefore = points.Count(p => p.WokenOn is { } w && w < r.Date);
            var daysLeft = r.Date > ym.End ? 0 : IncentiveRules.WorkDays(r.Date < ym.Start ? ym.Start : r.Date, ym.End, holidays);
            sleeping = new IncentiveSleepingView(points.Count, points.Count(p => p.WokenOn is not null), IncentivePromoRules.WakeTarget(points.Count, ym.WakeShare),
                IncentivePromoRules.VisitsPerDay(points.Count - wokenBefore, daysLeft));
            cashback = (await promos.CashbackAsync(s, [agent.Id], r.Date, until, ct)).GetValueOrDefault(agent.Id);
        }

        IncentiveBestView? best = null;
        if (o.Best is { } b)
        {
            var wholesale = s.InWholesaleGroup(agent.Id);
            var today = bestDays.GetValueOrDefault(r.Date) ?? [];
            var mine = today.FirstOrDefault(x => x.AgentId == agent.Id);
            var leader = today.FirstOrDefault(x => x.Wholesale == wholesale && x.Place == 1);
            var wins = bestDays.Count(d => d.Value.Any(x => x.AgentId == agent.Id && x.Winner));
            best = new IncentiveBestView(mine?.Kg ?? standing.Today, mine?.Place, mine?.Winner ?? false, wholesale, b.MinKg(wholesale),
                leader is null ? null : names.GetValueOrDefault(leader.AgentId), leader?.Kg ?? 0, wins, b.Prize);
        }

        var branch = (await promos.BranchesAsync([agent.Id], ct)).FirstOrDefault() ?? "";
        if (!topCache.TryGetValue(branch, out var top))
        {
            topCache[branch] = top = branch.Length == 0 ? ([], []) : await promos.TopSkuAsync(s, [branch], r.Date, ct);
        }

        IncentiveGoalsSummary? goals = null;
        if (route is not null && o.Twizos is { } tw)
        {
            var stores = route.Stores;
            var extra = (route.Extra ?? []).Where(x => x.Sets is { Count: > 0 }).ToList();
            goals = new IncentiveGoalsSummary(stores.Count,
                stores.Count(x => x.Sub == IncentiveTargetSub.Reorder), stores.Count(x => x.Sub == IncentiveTargetSub.Connect && !x.PlanAdded),
                stores.Count(x => x.PlanAdded), stores.Count(x => x.Sleeping), stores.Count(x => x.Plain), stores.Sum(x => x.TargetKg), dayStart.Norm,
                dayStart.Norm is { } n ? Math.Round(n * tw.Reserve, 1) : null, extra.Count, extra.Sum(x => x.TargetKg));
        }

        var days = new List<IncentivePromoDay>();
        IReadOnlyList<IncentiveTwizosOrder> period = [];
        if (route is null)
        {
            period = await promos.TwizosOrdersAsync(s, [agent.Id], periodFrom, r.Date, null, ct);
        }

        if (week is not null)
        {
            var yamelly = await promos.YamellyOrdersAsync(s, [agent.Id], week.Monday, r.Date, null, ct);
            foreach (var d in week.Days)
            {
                var woken = yamelly.Where(x => x.Created == d.Date && x.Qualifies && x.CashbackStatus != IncentiveCashbackStatus.Cancelled).ToList();
                days.Add(new IncentivePromoDay(d.Date, d.Kg, period.Count(x => x.Created == d.Date && x.Gift is not null), d.Route.SleepingPlanned, woken.Count,
                    woken.Sum(x => x.Cashback), bestDays.GetValueOrDefault(d.Date)?.Any(x => x.AgentId == agent.Id && x.Winner) ?? false, week.DayNorm));
            }
        }

        var gifts = route is not null ? route.GiftsTaken
            : period.Where(x => x.Gift is not null).GroupBy(x => x.Gift!).ToDictionary(g => g.Key, g => g.Count());
        var sets = gifts.Values.Sum();

        IncentiveEarnings? earnings = null;
        if (r.Slot is IncentiveSlot.Week or IncentiveSlot.Month)
        {
            var paid = await promos.PaidAsync(s, [agent.Id], periodFrom, r.Date, ct);
            var accrued = (await promos.YamellyOrdersAsync(s, [agent.Id], periodFrom, r.Date, null, ct))
                .Where(x => x.CashbackStatus != IncentiveCashbackStatus.Cancelled)
                .Sum(x => x.CashbackStatus == IncentiveCashbackStatus.Confirmed ? x.NetCashback : x.Cashback);
            var wins = best?.Wins ?? 0;
            earnings = new IncentiveEarnings(accrued, paid.GetValueOrDefault((agent.Id, IncentivePayoutKind.Cashback)), wins, wins * (o.Best?.Prize ?? 0),
                paid.GetValueOrDefault((agent.Id, IncentivePayoutKind.Best)));
        }

        var cashbackToday = route is null ? 0 : route.Stores.Concat(route.Extra ?? []).Sum(x => x.Cashback) + (route.OutOfRoute ?? []).Sum(x => x.Cashback);
        return new IncentivePromoView(sleeping, cashback, cashbackToday, best, top.Item1, top.Item2, goals, days, sets, gifts, earnings);
    }
}

using OneBase.Application.Telegram.Battle;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Собирает карточки накопительной акции на момент запуска из расчёта акции: накоплено и место (среди всех участников), ход к призам и норма
/// (утром и в 13:00 — с сегодняшнего дня, вечером, в субботу и в конце месяца — с завтрашнего), маршрут дня, неделя «6 полян», месяц «город
/// героя», рейтинг, сводка команды. Только чтение Linko; цели магазинов на день ставит IncentiveRouteService (если Persist).
/// </summary>
public sealed class IncentiveCardBuilder(IncentiveService incentive, IncentiveRouteService routes)
{
    public static readonly TimeOnly DefaultDayStart = new(8, 30);
    public static readonly TimeOnly DefaultMidday = new(13, 0);
    public static readonly TimeOnly DefaultDayEnd = new(18, 0);

    public async Task<IncentiveCards> BuildAsync(IncentiveSettings s, IncentiveCardRequest r, CancellationToken ct)
    {
        var holidays = await incentive.HolidaysAsync(s, ct);
        var slot = r.Slot;
        var midday = slot == IncentiveSlot.Midday;
        var until = midday ? r.Until ?? DefaultMidday : (TimeOnly?)null;
        var moment = slot == IncentiveSlot.Morning ? IncentiveMoment.Morning(r.Date) : IncentiveMoment.Day(r.Date, until);
        // Утро — план дня: магазины «впереди», заказы дня не учитываются (как и «сегодня» — 0), даже если карточку строят позже 08:30.
        var routeUntil = slot == IncentiveSlot.Morning ? TimeOnly.MinValue : until;
        var teamAgents = r.Team?.Regions.SelectMany(x => x.Agents) ?? [];
        var everyone = r.Participants.Concat(r.Cards).Concat(teamAgents).DistinctBy(a => a.Id).ToList();
        var standings = await incentive.StandingsAsync(s, everyone, moment, ct);

        // Норма — на дни, которые ещё можно работать: утром и в полдень — с сегодняшнего, после итога дня — с завтрашнего.
        var firstDay = slot is IncentiveSlot.Morning or IncentiveSlot.Midday ? r.Date : r.Date.AddDays(1);
        IncentiveProgress Progress(decimal total) => IncentiveRules.Progress(s, total, firstDay, holidays);
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

        var cards = new List<IncentiveAgentCard>();
        foreach (var agent in r.Cards.DistinctBy(a => a.Id))
        {
            var standing = standings.Of(agent.Id)!;
            var progress = Progress(standing.Total);
            var route = IncentiveSlot.IsDay(slot) ? await routes.DayAsync(s, agent.Id, r.Date, routeUntil, ct, r.Persist) : null;
            var week = slot == IncentiveSlot.Week ? await WeekAsync(s, agent.Id, monday, r.Date, weekStart!.Of(agent.Id)!.Total, holidays, ct) : null;
            var month = slot == IncentiveSlot.Month ? Month(s, standing, progress, r.Date, holidays) : null;
            var dayStart = IncentiveRules.Progress(s, standing.Total - standing.Today, r.Date, holidays);
            cards.Add(new IncentiveAgentCard(agent, standing, progress, dayStart, route, week, month, standings.Rank(agent.Id), lastWeek?.Rank(agent.Id),
                standings.Rating(standing.Wholesale).Count));
        }

        IncentiveTeam? team = null;
        if (r.Team is { } spec)
        {
            var regions = new List<IncentiveTeamRegion>();
            foreach (var region in spec.Regions)
            {
                var rows = new List<IncentiveTeamRow>();
                foreach (var agent in region.Agents.DistinctBy(a => a.Id))
                {
                    var st = standings.Of(agent.Id)!;
                    decimal? routeKg = IncentiveSlot.IsDay(slot) ? (await routes.DayAsync(s, agent.Id, r.Date, routeUntil, ct, r.Persist)).TargetKg : null;
                    rows.Add(new IncentiveTeamRow(agent, st.Total, st.Today, routeKg, Progress(st.Total), Math.Round(st.Total / worked, 1), standings.Rank(agent.Id), st.Wholesale));
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
                .Select((a, i) => new IncentiveRatingRow(i + 1, a.Agent, a.Total, IncentiveRules.Reached(s.Tiers, a.Total), IncentiveRules.Next(s.Tiers, a.Total),
                    IncentiveRules.Next(s.Tiers, a.Total) is { } next ? next.Kg - a.Total : null))
                .ToList();
            rating = new IncentiveRating(r.RatingScope, Rows(false), s.SeparateWholesale ? Rows(true) : [], Progress(0).WorkDaysLeft);
        }

        var share = IncentiveRules.MiddayShare(r.DayStart ?? DefaultDayStart, until ?? DefaultMidday, r.DayEnd ?? DefaultDayEnd);
        return new IncentiveCards(slot, r.Date, s, r.Language, cards, team, rating, share, standings.OrdersAsOf, r.Stale);
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
        var dayNorm = IncentiveRules.Progress(s, totalMonday, monday, holidays).Norm;
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
}

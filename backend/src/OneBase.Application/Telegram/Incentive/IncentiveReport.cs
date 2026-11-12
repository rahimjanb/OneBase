using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using OneBase.Domain.Incentive;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// 9. «Накопительная акция»: документ отчёта несёт карточки (Incentive) для рисовальщика, а плитки и таблицы — для подписи, журнала, ответов
/// Telegram-агента и резерва без картинки. Подпись под картинкой — по шаблонам ТЗ, по-русски и по-узбекски.
/// </summary>
public static class IncentiveReport
{
    /// <summary>
    /// Заказы Linko не обновились к отправке: утром — не обновлялись с 18:00 вчера (доставленное «по вчера» неполное), днём, в субботу и в конце
    /// месяца — ни разу за день. Что тогда делать — настройка акции (отметка «данные на …» или пропуск).
    /// </summary>
    public static bool IsStale(ReportJobInfo job, DateOnly date, DateTimeOffset? ordersAsOf)
    {
        if (ordersAsOf is not { } at)
        {
            return true;
        }

        var dayStart = ReportSchedules.At(date, TimeOnly.MinValue, ReportSchedules.DefaultTimeZone);
        return at < (job.Slot == IncentiveSlot.Morning ? dayStart.AddHours(-6) : dayStart);
    }

    /// <summary>Не отправляется: в журнал — «пропущено» с причиной.</summary>
    public static ReportDocument Skip(ReportContext c, string reason) =>
        ReportKit.Document(c, ReportKit.Title(c), Date(c.Date), [], [], [], [], reason, reason, incomplete: false, applies: false, skip: reason);

    public static ReportDocument Build(ReportContext c, IncentiveCards cards)
    {
        var notes = new List<string>();
        var kpis = new List<ReportKpi>();
        var tables = new List<ReportTable>();
        var monday = BattleRules.Monday(c.Date);
        var period = cards.Slot switch
        {
            _ when cards.Zone is { } z => Range(z.From, z.To),
            IncentiveSlot.Week => Range(monday, monday.AddDays(5)),
            IncentiveSlot.Month => $"{IncentiveTexts.MonthName(c.Date.Month, c.Language)} {c.Date.Year}",
            _ => Date(c.Date),
        };
        if (cards.Stale)
        {
            notes.Add(c.F("Данные Linko на {0}: заказы не обновились к отправке — показан последний подтверждённый результат",
                cards.DataAsOf is { } at ? ReportSchedules.Local(at, ReportSchedules.DefaultTimeZone).ToString("dd.MM HH:mm") : "—"));
        }

        var many = cards.Agents.Count > 1;
        foreach (var a in cards.Agents)
        {
            var name = many ? a.Agent.Name + ": " : "";
            var next = a.Progress.Next;
            kpis.Add(new(name + c.T("Накоплено"), $"{IncentiveTexts.Kg(a.Total)} {c.T("кг")}", IncentiveTexts.MonthsLine(cards.Program, a.Standing, cards.Date, c.Language)));
            kpis.Add(new(name + c.T("До приза"), next is null ? c.T("все призы взяты") : $"{IncentiveTexts.To(next, c.Language)} {IncentiveTexts.Kg(a.Progress.ToNext ?? 0)} {c.T("кг")}",
                a.DayStart.Norm is { } norm ? c.F("норма дня {0}", $"{IncentiveTexts.Kg(norm)} {c.T("кг")}") : null));
            if (a.Route is { } route)
            {
                if (IncentiveSlot.IsDay(cards.Slot))
                {
                    kpis.Add(new(name + c.T("Сегодня"), $"{IncentiveTexts.Kg(a.Today)} / {IncentiveTexts.Kg(route.TargetKg)} {c.T("кг")}"));
                }

                tables.Add(RouteTable(c, cards.Program, a, route));
                if (route.Source == BattleRouteSource.Own)
                {
                    notes.Add(c.F("{0}: плана визитов в Linko на этот день нет — маршрут построен OneBase по истории визитов и заказов", a.Agent.Name));
                }
            }

            if (a.Week is { } week)
            {
                tables.Add(WeekTable(c, a, week));
                if (week.TopMarkets is { Count: > 0 } top)
                {
                    tables.Add(new ReportTable(c.F("{0} · лучшие клиенты для дозаказа", a.Agent.Name),
                        [new(c.T("Магазин"), false, 2.4f), new(c.T("За 60 дней, кг")), new(c.T("Цель на неделю, кг"))],
                        top.Select(m => (IReadOnlyList<ReportCell>)[m.Name, IncentiveTexts.Kg(m.Kg), new ReportCell("+" + IncentiveTexts.Kg(m.WeekTarget), ReportTone.Good)]).ToList()));
                }
            }

            if (a.Month is { } month)
            {
                tables.Add(MonthTable(c, a, month));
            }
        }

        if (cards.Team is { } team)
        {
            kpis.Add(new(c.T("Накоплено командой"), $"{IncentiveTexts.Kg(team.Total)} {c.T("кг")}", c.F("{0} ТП", team.Agents.Count)));
            kpis.Add(new(c.T("Темп ниже нормы"), c.F("{0} ТП", team.Behind), c.F("у порога {0} · взяли приз {1}", team.Near, team.Reached)));
            tables.Add(TeamTable(c, team));
        }

        if (cards.TeamDay is { } teamDay)
        {
            kpis.Add(new(c.F("{0} команды", cards.Program.Product), $"{IncentiveTexts.Kg(teamDay.Kg)} {c.T("кг")}", c.F("наборов {0}", teamDay.Sets)));
            kpis.Add(new(c.T("Спящие разбужено"), $"{teamDay.SleepingWoken} / {teamDay.SleepingPlanned}", c.F("кэшбек команды {0}", Number(teamDay.Cashback))));
            tables.Add(new ReportTable(c.F("{0} · итог дня команды", teamDay.Title),
                [new(c.T("ТП"), false, 2.2f), new(c.T("Спящие")), new(c.T("Кг")), new(c.T("Цель, кг")), new(c.T("Наборы")), new(c.T("Кэшбек")), new(c.T("Отказов"))],
                teamDay.Rows.Select(r => (IReadOnlyList<ReportCell>)
                [
                    r.Agent.Name, $"{r.SleepingWoken}/{r.SleepingPlanned}", IncentiveTexts.Kg(r.Kg), r.Goal is { } g ? IncentiveTexts.Kg(g) : "—", Number(r.Sets),
                    new ReportCell(Number(r.Cashback), r.Cashback > 0 ? ReportTone.Good : ReportTone.Normal), Number(r.Refused),
                ]).ToList()));
        }

        if (cards.Zone is { } zone)
        {
            kpis.Add(new(c.F("{0} за неделю", cards.Program.Product), $"{IncentiveTexts.Kg(zone.WeekKg)} {c.T("кг")}", c.F("наборов с подарком {0}", zone.WeekSets)));
            kpis.Add(new(c.T("Спящие разбужено"), Number(zone.Woken), zone.WokenShare is { } ws ? Percent(ws) : null));
            kpis.Add(new(c.T("К выплате ТП"), Number(zone.CashbackToPay + zone.BestToPay), c.F("кэшбек {0} + TW-01 {1}", Number(zone.CashbackToPay), Number(zone.BestToPay))));
            tables.Add(new ReportTable(c.F("{0} · регионы", zone.Title),
                [new(c.T("Регион"), false, 1.6f), new(c.T("Спящие")), new(c.T("Разбужено, %")), new(c.T("Кг за неделю")), new(c.T("Подарков")), new("TW-01")],
                zone.Regions.Select(r => (IReadOnlyList<ReportCell>)
                [
                    r.Name, $"{r.Woken}/{r.Listed}", PercentCell(r.WokenShare), IncentiveTexts.Kg(r.WeekKg), Number(r.Gifts.Values.Sum()), Number(r.BestWins),
                ]).ToList()));
            if (zone.Territories is { Count: > 0 } territories)
            {
                tables.Add(new ReportTable(c.T("YM-03 · план по территориям"), [new(c.T("Территория"), false, 1.6f), new(c.T("План, кг")), new(c.T("Продано, кг")), new(c.T("% плана"))],
                    territories.Select(t => (IReadOnlyList<ReportCell>)[t.Name, IncentiveTexts.Kg(t.PlanKg), IncentiveTexts.Kg(t.Kg), PercentCell(t.Share)]).ToList()));
            }
        }

        if (cards.Rating is { } rating)
        {
            tables.Add(RatingTable(c, c.T("Рейтинг акции — розница"), rating.Retail));
            if (rating.Wholesale.Count > 0)
            {
                tables.Add(RatingTable(c, c.T("Рейтинг акции — оптовые агенты"), rating.Wholesale));
            }
        }

        var applies = cards.Agents.Count > 0 || cards.Team is not null || cards.Rating is not null || cards.TeamDay is not null || cards.Zone is not null;
        var (caption, summary) = IncentiveTexts.Caption(cards);
        return ReportKit.Document(c, ReportKit.Title(c), period, kpis, tables, [], notes, caption, summary, cards.Stale, applies,
            applies ? null : c.T("Карточек нет: в области нет ТП акции.")) with { Incentive = cards };
    }

    private static string Status(ReportContext c, string status) => status switch
    {
        BattleStoreStatus.Taken => c.T("взято"),
        BattleStoreStatus.Partial => c.T("частично"),
        BattleStoreStatus.Refused => c.T("отказ"),
        _ => c.T("впереди"),
    };

    private static ReportTone Tone(string status) => status switch
    {
        BattleStoreStatus.Taken => ReportTone.Good,
        BattleStoreStatus.Refused => ReportTone.Bad,
        _ => ReportTone.Normal,
    };

    private static ReportTable RouteTable(ReportContext c, IncentiveSettings program, IncentiveAgentCard a, IncentiveRoute route)
    {
        var rows = route.Stores.Select(s => (IReadOnlyList<ReportCell>)
        [
            $"{s.Order}", s.Name, s.Sub switch { IncentiveTargetSub.Connect => c.T("подключить"), IncentiveTargetSub.Reorder => c.T("дозаказ"), _ => "" },
            IncentiveTexts.StoreSet(program, s.Set, s.Prize, c.Language).Contents,
            IncentiveTexts.Kg1(s.TargetKg), IncentiveTexts.Kg1(s.Kg), new ReportCell(Status(c, s.Status), Tone(s.Status)),
        ]).ToList();
        if (rows.Count == 0)
        {
            rows.Add(["", c.T("Маршрута нет"), "", "", "", "", ""]);
        }

        return new ReportTable(c.F("{0} · {1} · маршрут дня", a.Agent.Name, Date(route.Date)),
            [new("№", false, 0.4f), new(c.T("Магазин"), false, 2.4f), new(c.T("Цель"), false, 1f), new(c.T("Набор"), false, 1.4f), new(c.T("Цель, кг")), new(c.T("Факт, кг")),
                new(c.T("Статус"), false, 1f)], rows);
    }

    private static ReportTable WeekTable(ReportContext c, IncentiveAgentCard a, IncentiveWeekView w)
    {
        var rows = w.Days.Select(d => (IReadOnlyList<ReportCell>)
            [Date(d.Date), IncentiveTexts.Kg(d.Kg), Number(d.Route.Stores.Count), Number(d.Route.Taken), w.DayNorm is { } n ? d.Kg >= n ? c.T("да") : c.T("нет") : "—"]).ToList();
        return new ReportTable(c.F("{0} · неделя {1}", a.Agent.Name, Range(w.Monday, w.Saturday)),
            [new(c.T("День"), false, 1.2f), new(c.T("Кг")), new(c.T("Магазинов")), new(c.T("Взято")), new(c.T("Норма дня"))], rows,
            [c.T("Итого"), new(IncentiveTexts.Kg(w.Kg), Bold: true), "", "", w.Norm is { } wn ? IncentiveTexts.Kg(wn) : "—"]);
    }

    private static ReportTable MonthTable(ReportContext c, IncentiveAgentCard a, IncentiveMonthView m)
    {
        var rows = m.Weeks.Select(w => (IReadOnlyList<ReportCell>)
            [$"{w.Number}: {Range(w.From, w.To)}", IncentiveTexts.Kg(w.Kg), w.Norm is { } n ? IncentiveTexts.Kg(n) : "—", PercentCell(w.Share)]).ToList();
        return new ReportTable(c.F("{0} · {1}", a.Agent.Name, $"{IncentiveTexts.MonthName(m.Month, c.Language)} {m.Year}"),
            [new(c.T("Неделя"), false, 1.6f), new(c.T("Кг")), new(c.T("Норма недели")), new(c.T("% нормы"))], rows,
            [c.T("Итого"), new(IncentiveTexts.Kg(m.Kg), Bold: true), m.Guide is { } g ? IncentiveTexts.Kg(g) : "—", PercentCell(m.Guide is > 0 ? m.Kg / m.Guide : null, true)]);
    }

    private static ReportTable TeamTable(ReportContext c, IncentiveTeam team)
    {
        var rows = team.Agents.Select(r => (IReadOnlyList<ReportCell>)
        [
            r.Agent.Name, r.Agent.Region ?? "", IncentiveTexts.Kg(r.Total), IncentiveTexts.Kg(r.Today),
            r.Progress.Next is { } n ? $"{IncentiveTexts.To(n, c.Language)} {IncentiveTexts.Kg(r.Progress.ToNext ?? 0)}" : c.T("все призы взяты"),
            new ReportCell($"{(r.Progress.Norm is { } norm ? IncentiveTexts.Kg(norm) : "—")} / {IncentiveTexts.Kg(r.Pace)}", r.Behind ? ReportTone.Bad : ReportTone.Good),
        ]).ToList();
        return new ReportTable(c.F("{0} · ТП акции", team.Title),
            [new(c.T("ТП"), false, 2.2f), new(c.T("Регион"), false, 1.2f), new(c.T("Накоплено")), new(c.T("Сегодня")), new(c.T("До приза"), false, 1.6f), new(c.T("Норма / темп"))], rows,
            [c.T("Итого"), "", new(IncentiveTexts.Kg(team.Total), Bold: true), new(IncentiveTexts.Kg(team.Today), Bold: true), "", ""]);
    }

    private static ReportTable RatingTable(ReportContext c, string title, IReadOnlyList<IncentiveRatingRow> rows) =>
        new(title, [new(c.T("Место"), false, 0.6f), new(c.T("Агент"), false, 2.4f), new(c.T("Регион"), false, 1.2f), new(c.T("Кг")), new(c.T("Статус"), false, 1.8f)],
            rows.Select(r => (IReadOnlyList<ReportCell>)
            [
                $"{r.Place}", r.Agent.Name, r.Agent.Region ?? "", IncentiveTexts.Kg(r.Kg),
                r.Next is { } n ? $"{IncentiveTexts.To(n, c.Language)} {IncentiveTexts.Kg(r.ToNext ?? 0)}" : c.T("все призы взяты"),
            ]).ToList());
}

using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using OneBase.Domain.Telegram;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Battle;

/// <summary>
/// 7. «Поле боя» (ТЗ v2): карточки целей ТП — маршрут дня 08:30, полдень 13:00, итог дня 18:00, итог недели в субботу. Документ отчёта несёт
/// карточки (Battle) для рисовальщика и текстовые плитки и таблицы — для подписи, журнала и резерва без картинки. Подпись под картинкой —
/// по-русски и по-узбекски всегда (так в группе «Candy Gold | Agent»).
/// </summary>
public static class BattleReport
{
    public static ReportDocument Build(ReportContext c, BattleCards cards)
    {
        var notes = new List<string>();
        var problems = new List<string>();
        string caption, summary, period;
        var kpis = new List<ReportKpi>();
        var tables = new List<ReportTable>();
        var applies = true;
        string? skip = null;
        var incomplete = false;

        if (cards.IsWeek)
        {
            var weeks = cards.Weeks;
            var first = weeks.FirstOrDefault();
            period = first is null ? Date(c.Date) : Range(first.Monday, first.Saturday);
            if (weeks.Count == 0 || weeks.All(w => w.Progress.Total == 0))
            {
                applies = false;
                skip = c.T("На этой неделе целей не было: у ТП нет плана визитов в Linko или подходящих точек.");
            }

            foreach (var w in weeks)
            {
                kpis.Add(new(c.F("{0}: Твизос", w.AgentName), $"{w.TwizosTaken} / {w.TwizosTotal}"));
                kpis.Add(new(c.F("{0}: Могул", w.AgentName), $"{w.MogulTaken} / {w.MogulTotal}"));
                kpis.Add(new(c.F("{0}: бонус недели", w.AgentName), Number(w.Progress.Bonus), Percent(w.Progress.Share)));
                tables.Add(WeekTable(c, w));
            }

            var me = weeks.Count == 1 ? weeks[0] : null;
            caption = me is null
                ? c.F("Итоги недели «Поле боя»: {0} ТП, взято {1} из {2} целей.", weeks.Count, weeks.Sum(w => w.Progress.Taken), weeks.Sum(w => w.Progress.Total))
                : $"{c.F("Итог недели: {0} из {1} ({2}) — бонус {3} сум.", me.Progress.Taken, me.Progress.Total, Percent(me.Progress.Share), Number(me.Progress.Bonus))}\n" +
                  $"Hafta yakuni: {me.Progress.Total} tadan {me.Progress.Taken} ta ({Percent(me.Progress.Share)}) — bonus {Number(me.Progress.Bonus)} so‘m.";
            summary = me is null
                ? c.F("Неделя: {0} ТП, взято {1} из {2}", weeks.Count, weeks.Sum(w => w.Progress.Taken), weeks.Sum(w => w.Progress.Total))
                : c.F("Неделя: взято {0} из {1}, бонус {2}", me.Progress.Taken, me.Progress.Total, Number(me.Progress.Bonus));
        }
        else
        {
            var days = cards.Days;
            period = Date(c.Date);
            if (days.Count == 0 || days.All(d => d.Stores.Count == 0))
            {
                applies = false;
                skip = c.T("Целей на этот день нет: в Linko нет плана визитов ТП и подходящих точек для собственного маршрута.");
            }

            // Полдень и итог дня — по заказам: заказы Linko за день не загружены — карточку не шлём (ТЗ), а не отправляем с нулями.
            if (c.Job.Slot is "midday" or "evening")
            {
                incomplete = ReportKit.Freshness(c, notes, needsFact: true);
                if (incomplete && applies)
                {
                    applies = false;
                    skip = c.T("Заказы Linko за день не обновились к отправке — карточка не отправлена (данные обновятся к следующей).");
                }
            }

            foreach (var d in days)
            {
                var name = days.Count == 1 ? "" : d.AgentName + ": ";
                kpis.Add(new(name + c.T("Твизос — взято"), $"{d.TwizosTaken} / {d.TwizosTotal}"));
                kpis.Add(new(name + c.T("Могул — взято"), $"{d.MogulTaken} / {d.MogulTotal}"));
                kpis.Add(new(name + c.T("Магазины с целями — пройдено"), $"{d.StoresPassed} / {d.Stores.Count}",
                    d.RoutePoints > 0 ? c.F("в маршруте {0} точек", d.RoutePoints) : null));
                tables.Add(DayTable(c, d));
                if (d.RouteSource == BattleRouteSource.Own)
                {
                    notes.Add(c.F("{0}: плана визитов в Linko на этот день нет — маршрут построен OneBase по истории визитов и заказов", d.AgentName));
                }

                if (!d.MogulActive)
                {
                    notes.Add(c.T("Сетка Могул A–H в Linko на этот день не действует — целей Могул нет"));
                }

                if (d.RoutePoints > d.Stores.Count && d.Stores.Count > 0)
                {
                    notes.Add(c.F("{0}: в маршруте Linko {1} точек, цели — в {2} (активная база от 50 000 сум/мес, не больше 10 целей Твизос и 10 Могул)",
                        d.AgentName, d.RoutePoints, d.Stores.Count));
                }
            }

            var me = days.Count == 1 ? days[0] : null;
            var total = days.Sum(d => d.Total);
            var taken = days.Sum(d => d.Taken);
            if (me is null)
            {
                caption = c.F("«Поле боя» · {0} ТП: {1} магазинов, {2} целей, взято {3}.", days.Count, days.Sum(d => d.Stores.Count), total, taken);
                summary = c.F("{0} ТП: взято {1} из {2}", days.Count, taken, total);
            }
            else
            {
                var bonusMax = Number(cards.BonusSteps.Count == 0 ? 0 : cards.BonusSteps.Max(s => s.Sum));
                var ahead = me.Stores.Count - me.StoresPassed;
                var next = me.Week.NextStep;
                (caption, summary) = c.Job.Slot switch
                {
                    "morning" => (
                        (me.RoutePoints > me.Stores.Count
                            ? $"{c.F("Доброе утро! В маршруте {0} точек, цели — в {1} магазинах: {2} целей. Твизос — призовые наборы 300/400/600, Могул — скидка по категории. Бонус недели — до {3} сум.", me.RoutePoints, me.Stores.Count, total, bonusMax)}\n" +
                              $"Xayrli tong! Marshrutda {me.RoutePoints} ta nuqta, maqsadlar — {me.Stores.Count} ta do‘konda: {total} ta maqsad. Twizos — 300/400/600 ming so‘mlik sovg‘ali to‘plamlar, Mogul — toifa bo‘yicha chegirma. Haftalik bonus — {bonusMax} so‘mgacha."
                            : $"{c.F("Доброе утро! Маршрут: {0} магазинов, {1} целей. Твизос — призовые наборы 300/400/600, Могул — скидка по категории. Бонус недели — до {2} сум.", me.Stores.Count, total, bonusMax)}\n" +
                              $"Xayrli tong! Marshrut: {me.Stores.Count} ta do‘kon, {total} ta maqsad. Twizos — 300/400/600 ming so‘mlik sovg‘ali to‘plamlar, Mogul — toifa bo‘yicha chegirma. Haftalik bonus — {bonusMax} so‘mgacha."),
                        c.F("Маршрут: {0} магазинов, {1} целей", me.Stores.Count, total)),
                    "midday" => (
                        $"{c.F("Полдень: взято {0} из {1}. Впереди {2} магазинов — начните с оранжевых.", taken, total, ahead)}\n" +
                        $"Tush: {total} tadan {taken} tasi bajarildi. Oldinda {ahead} ta do‘kon — to‘q sariqlardan boshlang.",
                        c.F("Полдень: взято {0} из {1}", taken, total)),
                    _ => (
                        $"{(next is null
                            ? c.F("Итог дня: {0} из {1}. Все ступени бонуса недели взяты!", taken, total)
                            : c.F("Итог дня: {0} из {1}. До бонуса {2} сум — ещё {3} целей.", taken, total, Number(next.Sum), me.Week.ToNextStep))}\n" +
                        (next is null
                            ? $"Kun yakuni: {total} tadan {taken} ta. Haftalik bonusning barcha pog‘onalari olindi!"
                            : $"Kun yakuni: {total} tadan {taken} ta. {Number(next.Sum)} so‘m bonusgacha yana {me.Week.ToNextStep} ta maqsad."),
                        c.F("Итог дня: взято {0} из {1}", taken, total)),
                };
            }
        }

        return ReportKit.Document(c, ReportKit.Title(c), period, kpis, tables, problems, notes, caption, summary, incomplete, applies, skip) with { Battle = cards };
    }

    /// <summary>Цели дня таблицей — для текста без картинки и ответов Telegram-агента.</summary>
    private static ReportTable DayTable(ReportContext c, BattleDay d)
    {
        var rows = d.Stores.SelectMany(s => s.Goals.Select(g => (IReadOnlyList<ReportCell>)
        [
            $"{s.Order}", s.Name, GoalText(c, g), Number(g.TargetSum),
            new ReportCell(StatusText(c, g), g.IsTaken ? ReportTone.Good : g.Status == BattleGoalStatus.Refused ? ReportTone.Bad : ReportTone.Normal),
        ])).ToList();
        if (rows.Count == 0)
        {
            rows.Add(["", c.T("Целей нет"), "", "", ""]);
        }

        return new ReportTable(c.F("{0} · {1} · цели дня", d.AgentName, Date(d.Date)),
            [new("№", false, 0.4f), new(c.T("Магазин"), false, 2.4f), new(c.T("Цель"), false, 2f), new(c.T("Сумма, сум")), new(c.T("Статус"), false, 1f)], rows);
    }

    private static ReportTable WeekTable(ReportContext c, BattleWeek w)
    {
        var rows = w.Days.Select(d => (IReadOnlyList<ReportCell>)[Date(d.Date), Number(d.Stores.Count), Number(d.Total), Number(d.Taken), PercentCell(d.Total == 0 ? null : d.Share)]).ToList();
        return new ReportTable(c.F("{0} · неделя {1}", w.AgentName, Range(w.Monday, w.Saturday)),
            [new(c.T("День"), false, 1.2f), new(c.T("Магазинов")), new(c.T("Целей")), new(c.T("Взято")), new(c.T("% дня"))], rows,
            [c.T("Итого"), Number(w.Days.Sum(d => d.Stores.Count)), Number(w.Progress.Total), new(Number(w.Progress.Taken), Bold: true), PercentCell(w.Progress.Total == 0 ? null : w.Progress.Share, true)]);
    }

    /// <summary>«Твизос · подключить», «Твизос · набор 400», «Могул E · подключить», «Могул G · объём».</summary>
    public static string GoalText(ReportContext c, BattleGoalView g) => g.Kind == BattleGoalKind.Twizos
        ? g.Sub == BattleGoalSub.Connect ? c.T("Твизос · подключить") : c.F("Твизос · набор {0}", g.Level)
        : g.Sub == BattleGoalSub.Connect ? c.F("Могул {0} · подключить", g.Level) : c.F("Могул {0} · объём", g.Level);

    public static string StatusText(ReportContext c, BattleGoalView g) => g.Status switch
    {
        BattleGoalStatus.Taken => g.TakenPercent is { } p ? c.F("взято · {0}%", p) : c.T("взято"),
        BattleGoalStatus.Refused => c.T("отказ"),
        _ => c.T("впереди"),
    };
}

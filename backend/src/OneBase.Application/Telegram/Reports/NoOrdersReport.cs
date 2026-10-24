using OneBase.Domain.Telegram;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// 3. ТП без заказов (лист «3 Агенты без заказов»): 13:00 — контроль, 17:00 — личное напоминание ТП, 18:00 — итог дня. Без заказов —
/// у ТП нет ни одного заказа, принятого им за день до среза. ТП получает напоминание, только если это правда про него (иначе — «пропущено»).
/// «План, сум» и «% плана» — дневной план, зафиксированный утром (DailyPlanMath: остаток месячного плана ÷ оставшиеся рабочие дни);
/// итог региона — из сумм (DailyPlanMath.Sum); ТП без плана месяца — «—», не ноль, и вне расчёта региона; месячный план выполнен — «выполнен».
/// </summary>
public static class NoOrdersReport
{
    private sealed record Row(long Id, string Name, string Region, DayMetrics M, DailyPlanRow? Plan);

    public static ReportDocument Build(ReportContext c)
    {
        var day = c.Date;
        var notes = new List<string>();
        var incomplete = ReportKit.Freshness(c, notes, needsFact: true);
        var visitKnown = ReportKit.VisitPlanKnown(c, day, day);
        var agents = c.Scope.Agents
            .Select(a => new Row(a.Id, a.Name, c.Scope.Regions.First(r => r.Agents.Contains(a)).Name, ReportKit.Day(c, a.Id, day, visitKnown), c.Facts.DailyPlanOf(a.Id)))
            .ToList();
        bool None13(Row a) => a.M.Orders13 == 0;
        bool None(Row a) => a.M.Orders == 0;
        var kpis = new List<ReportKpi>
        {
            new(c.T("ТП в работе"), Number(agents.Count)),
            new(c.F("Без заказов к {0}", c.MiddayText), Number(agents.Count(None13)), null, agents.Any(None13) ? ReportTone.Bad : ReportTone.Good),
        };
        if (c.Job.Slot != "midday")
        {
            kpis.Add(new(c.F("Без заказов к {0}", c.CutoffText), Number(agents.Count(None)), null, agents.Any(None) ? ReportTone.Bad : ReportTone.Good));
        }

        var tables = new List<ReportTable>();
        var problems = new List<string>();
        string caption;
        var applies = true;
        string? skip = null;

        if (c.Role == TelegramReportRoles.Tp)
        {
            var me = agents[0];
            var noneNow = c.Job.Slot == "midday" ? me.M.Orders13 == 0 : me.M.Orders == 0;
            if (c.Job.Slot is "midday" or "reminder" && !noneNow)
            {
                applies = false;
                skip = c.F("У ТП есть заказы ({0}) — напоминание не нужно", c.Job.Slot == "midday" ? me.M.Orders13 : me.M.Orders);
            }

            var cols = new List<ReportColumn> { new("№", false, 0.4f), new(c.T("Показатель"), false, 2f), new(c.MiddayText) };
            if (c.Job.Slot != "midday")
            {
                cols.Add(new(c.CutoffText));
            }

            IReadOnlyList<ReportCell> Row(string n, string label, ReportCell at13, ReportCell now) =>
                c.Job.Slot == "midday" ? [n, label, at13] : [n, label, at13, now];
            var share13 = DailyPlanMath.PercentCell(me.M.FactSum13, me.Plan, c.T);
            var share = DailyPlanMath.PercentCell(me.M.FactSum, me.Plan, c.T);
            tables.Add(new ReportTable(c.T("1. Мой статус"), cols,
            [
                Row("1", c.T("Заказов, шт"), Number(me.M.Orders13), Number(me.M.Orders)),
                Row("2", c.T("Факт, сум"), Number(me.M.FactSum13), Number(me.M.FactSum)),
                Row("3", c.T("План на день, сум"), DailyPlanMath.PlanCell(me.Plan), DailyPlanMath.PlanCell(me.Plan)),
                Row("4", c.T("% дневного плана"), share13, share),
                Row("5", c.T("Визиты факт / план"), $"{me.M.Visits13} / {Number(me.M.VisitPlan)}", $"{me.M.Visits} / {Number(me.M.VisitPlan)}"),
            ]));
            caption = c.Job.Slot switch
            {
                "midday" => c.F("⚠ {0}, на {1} у вас нет ни одного заказа. План на день: {2} сум. Визитов: {3} / {4}. Возьмите первый заказ как можно скорее!",
                    me.Name, c.MiddayText, DailyPlanMath.PlanCell(me.Plan).Text, me.M.Visits13, Number(me.M.VisitPlan)),
                "reminder" => c.F("⏰ Напоминание: до конца дня {0}, заказов пока нет. Зайдите в оставшиеся точки маршрута и возьмите заказ.", HoursLeft(c)),
                _ => c.F("Итог дня: заказов {0} на {1} сум, план дня {2}.", me.M.Orders, Number(me.M.FactSum), share.Text),
            };
        }
        else if (c.Role == TelegramReportRoles.Rm)
        {
            var rows = c.Scope.Regions.Select((r, i) =>
            {
                var own = agents.Where(a => a.Region == r.Name).ToList();
                var none13 = own.Where(None13).ToList();
                var row = new List<ReportCell> { $"{i + 1}", r.Name, Number(own.Count), new(Number(none13.Count), none13.Count > 0 ? ReportTone.Bad : ReportTone.Normal) };
                if (c.Job.Slot != "midday")
                {
                    var none = own.Where(None).ToList();
                    row.Add(new(Number(none.Count), none.Count > 0 ? ReportTone.Bad : ReportTone.Normal));
                }

                var list = (c.Job.Slot == "midday" ? none13 : own.Where(None).ToList());
                row.AddRange([Number(list.Sum(a => a.M.VisitPlan ?? 0)), Number(list.Sum(a => c.Job.Slot == "midday" ? a.M.Visits13 : a.M.Visits)),
                    list.Count == 0 ? "—" : string.Join(", ", list.Select(a => a.Name))]);
                return (IReadOnlyList<ReportCell>)row;
            }).ToList();
            var cols = new List<ReportColumn> { new("№", false, 0.4f), new(c.T("Регион"), false, 1.6f), new(c.T("ТП, чел."), Weight: 0.7f), new(c.F("Без заказов {0}, ТП", c.MiddayText), Weight: 0.9f) };
            if (c.Job.Slot != "midday")
            {
                cols.Add(new(c.F("Без заказов {0}, ТП", c.CutoffText), Weight: 0.9f));
            }

            cols.AddRange([new(c.T("Визиты план (ТП без заказов)"), Weight: 1f), new(c.T("Визиты факт (ТП без заказов)"), Weight: 1f), new(c.T("Кто без заказов"), false, 3.4f)]);
            tables.Add(new ReportTable(c.T("1. ТП без заказов по регионам"), cols, rows));
            caption = $"{c.T(ReportCatalog.Title(c.Job.Report))} · {c.Scope.Title} · {Date(day)}\n" +
                      c.F("Без заказов к {0}: {1} из {2} ТП.", c.Job.Slot == "midday" ? c.MiddayText : c.CutoffText,
                          c.Job.Slot == "midday" ? agents.Count(None13) : agents.Count(None), agents.Count);
        }
        else
        {
            var cols = new List<ReportColumn> { new("№", false, 0.4f), new(c.T("ТП"), false, 2.4f), new(c.T("План, сум")), new(c.F("Заказов {0}", c.MiddayText), Weight: 0.7f),
                new(c.F("Факт {0}, сум", c.MiddayText)), new(c.F("% плана {0}", c.MiddayText), Weight: 0.7f), new(c.T("Визиты план"), Weight: 0.7f),
                new(c.F("Визиты {0}", c.MiddayText), Weight: 0.7f), new(c.F("Статус {0}", c.MiddayText), false, 1.1f) };
            if (c.Job.Slot != "midday")
            {
                cols.AddRange([new(c.F("Заказов {0}", c.CutoffText), Weight: 0.7f), new(c.F("Статус {0}", c.CutoffText), false, 1.1f)]);
            }

            cols.Add(new(c.T("Последний заказ"), Weight: 0.8f));
            ReportCell Status(int orders) => orders == 0 ? new(c.T("Нет заказов"), ReportTone.Bad, true) : new(c.T("ОК"), ReportTone.Good);
            var rows = agents.Select((a, i) =>
            {
                var row = new List<ReportCell> { $"{i + 1}", a.Name, DailyPlanMath.PlanCell(a.Plan), Number(a.M.Orders13), Number(a.M.FactSum13),
                    DailyPlanMath.PercentCell(a.M.FactSum13, a.Plan, c.T), Number(a.M.VisitPlan), Number(a.M.Visits13), Status(a.M.Orders13) };
                if (c.Job.Slot != "midday")
                {
                    row.AddRange([Number(a.M.Orders), Status(a.M.Orders)]);
                }

                row.Add(a.M.LastOrder?.ToString("HH:mm") ?? "—");
                return (IReadOnlyList<ReportCell>)row;
            }).ToList();
            var t = DayMetrics.Sum(agents.Select(a => a.M).ToList());
            // Итог региона: план дня — сумма планов ТП с расчётом, процент — из сумм их плана и факта (не среднее процентов).
            var regional = DailyPlanMath.Sum(agents.Select(a => (a.Plan, a.M.FactSum13)));
            var total = new List<ReportCell> { "", new(ReportKit.TotalTitle(c), Bold: true), DailyPlanMath.TotalPlanCell(regional), Number(t.Orders13), Number(t.FactSum13),
                DailyPlanMath.TotalPercentCell(regional, c.T), Number(t.VisitPlan), Number(t.Visits13),
                new(c.F("{0} без заказов", agents.Count(None13)), agents.Any(None13) ? ReportTone.Bad : ReportTone.Good, true) };
            if (c.Job.Slot != "midday")
            {
                total.AddRange([Number(t.Orders), new(c.F("{0} без заказов", agents.Count(None)), agents.Any(None) ? ReportTone.Bad : ReportTone.Good, true)]);
            }

            total.Add("");
            tables.Add(new ReportTable(c.T("1. Статус заказов по ТП"), cols, rows, total,
                c.T("«Нет заказов» — красным; план — остаток плана месяца ÷ оставшиеся рабочие дни")));
            caption = $"{c.T(ReportCatalog.Title(c.Job.Report))} · {c.Scope.Title} · {Date(day)}\n" +
                      c.F("Без заказов к {0}: {1} из {2} ТП.", c.Job.Slot == "midday" ? c.MiddayText : c.CutoffText,
                          c.Job.Slot == "midday" ? agents.Count(None13) : agents.Count(None), agents.Count);
        }

        if (c.Role != TelegramReportRoles.Tp)
        {
            var list = (c.Job.Slot == "midday" ? agents.Where(None13) : agents.Where(None)).ToList();
            if (list.Count > 0)
            {
                problems.Add(c.F("Связаться с ТП без заказов: {0}", string.Join(", ", list.Take(10).Select(a => $"{a.Name} ({(c.Job.Slot == "midday" ? a.M.Visits13 : a.M.Visits)}/{Number(a.M.VisitPlan)} {c.T("виз.")})"))));
            }
        }

        PlanNotes(c, agents, notes);
        var summary = c.F("Без заказов {0} из {1} ТП", c.Job.Slot == "midday" ? agents.Count(None13) : agents.Count(None), agents.Count);
        return ReportKit.Document(c, ReportKit.Title(c), Date(day), kpis, tables, problems, notes, caption, summary, incomplete, applies, skip);
    }

    /// <summary>
    /// Замечания о дневном плане: у кого нет плана месяца в Linko (не ноль — «—», вне расчёта региона), выходной или праздник, нет рабочих
    /// дней — чтобы «—» в таблице не приняли за ошибку.
    /// </summary>
    private static void PlanNotes(ReportContext c, IReadOnlyList<Row> agents, List<string> notes)
    {
        var noPlan = agents.Where(a => a.Plan is null || a.Plan.Status == DailyPlanMath.NoPlan).Select(a => a.Name).ToList();
        if (noPlan.Count > 0)
        {
            notes.Add(c.Role == TelegramReportRoles.Tp
                ? c.T("Нет плана месяца в Linko — план дня не считается")
                : c.F("Нет плана месяца в Linko: {0} — план дня «—», в % плана региона не входят",
                    string.Join(", ", noPlan.Take(10)) + (noPlan.Count > 10 ? $" +{noPlan.Count - 10}" : "")));
        }

        if (agents.Any(a => a.Plan?.Status == DailyPlanMath.DayOff))
        {
            notes.Add(c.T("Выходной или праздник — план на день не считается"));
        }

        if (agents.Any(a => a.Plan?.Status == DailyPlanMath.NoDays))
        {
            notes.Add(c.T("Рабочих дней до конца месяца не осталось — план на день не считается"));
        }
    }

    /// <summary>«1 час» — сколько осталось до 18:00 от времени напоминания.</summary>
    private static string HoursLeft(ReportContext c)
    {
        var left = new TimeOnly(18, 0) - (c.JobTime ?? c.Job.Time);
        var hours = Math.Max(1, (int)Math.Round(left.TotalHours));
        return c.F(hours == 1 ? "{0} час" : hours < 5 ? "{0} часа" : "{0} часов", hours);
    }
}

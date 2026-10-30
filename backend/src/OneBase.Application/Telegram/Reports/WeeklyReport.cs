using System.Globalization;
using OneBase.Domain.Telegram;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// 6. Итоги недели (лист «6 Итоги недели»): неделя пн–сб. Суббота 18:00 — итоги текущей недели; понедельник 8:30 — обновлённые итоги
/// завершившейся: всё пересчитывается заново из данных (поздние заказы учтены, ничего не прибавляется повторно). План — сумма дневных планов
/// рабочих дней недели; АКБ — точки с заказом за неделю против плана АКБ месяца Linko; место — по % выполнения плана (сум).
/// </summary>
public static class WeeklyReport
{
    public sealed record WeekMetrics(decimal? PlanSum, decimal FactSum, decimal? PlanKg, decimal FactKg, int? VisitPlan, int Visits, int PromoOrders, decimal? AkbTarget, int Akb)
    {
        public decimal? Share => ReportFormat.Share(FactSum, PlanSum);
        public decimal? ShareKg => ReportFormat.Share(FactKg, PlanKg);
        public decimal? VisitShare => VisitPlan is > 0 ? (decimal)Visits / VisitPlan.Value : null;
        public decimal? AkbShare => ReportFormat.Share(Akb, AkbTarget);

        /// <summary>План в Linko только в кг (так у большинства регионов): выполнение и место — по кг.</summary>
        public bool ByKg => PlanSum is null && PlanKg is not null;
        public decimal? Main => ByKg ? ShareKg : Share;
    }

    public static (DateOnly From, DateOnly To) Period(ReportJobInfo job, DateOnly date) => ReportSchedules.Week(date, previous: job.Slot == "monday");

    public static WeekMetrics Of(ReportContext c, IReadOnlyList<Sales.Metrics.ReportPerson> agents, DateOnly from, DateOnly to, bool visitKnown)
    {
        var days = ReportSchedules.WorkingDays(from, to).ToList();
        decimal? Plan(Func<long, DateOnly, decimal?> f)
        {
            var values = agents.SelectMany(a => days.Select(d => ReportKit.Whole(f(a.Id, d)))).Where(v => v is not null).ToList();
            return values.Count == 0 ? null : values.Sum(v => v!.Value);
        }

        var orders = agents.SelectMany(a => c.Facts.OrdersOf(a.Id, from, to)).ToList();
        var visits = agents.SelectMany(a => c.Facts.VisitsOf(a.Id, from, to)).ToList();
        var akb = agents.Select(a => c.Facts.PlanOf(a.Id, to)?.Akb).Where(v => v is not null).ToList();
        return new WeekMetrics(Plan(c.Facts.DayPlanSum), orders.Sum(o => o.Sum), Plan(c.Facts.DayPlanKg), orders.Sum(o => o.Kg),
            visitKnown ? visits.Count(v => v.InPlan) : null, visits.Where(v => v.Done).Select(v => (v.Day, v.MarketId)).Distinct().Count(),
            orders.Count(o => o.HasPromo), akb.Count == 0 ? null : akb.Sum(v => v!.Value), orders.Where(o => o.MarketId != null).Select(o => o.MarketId).Distinct().Count());
    }

    public static ReportDocument Build(ReportContext c)
    {
        var (from, to) = Period(c.Job, c.Date);
        var notes = new List<string>();
        var incomplete = ReportKit.Freshness(c, notes, needsFact: true, from, to);
        var visitKnown = ReportKit.VisitPlanKnown(c, from, to);
        if (!visitKnown)
        {
            notes.Add(c.T("План визитов за неделю не загружен из Linko"));
        }

        var units = ReportKit.Units(c).Select(u => (u.Name, u.Agents, M: Of(c, u.Agents, from, to, visitKnown))).ToList();
        var total = Of(c, c.Scope.Agents, from, to, visitKnown);
        var period = Range(from, to);
        var categories = DailyPlanReport.Categories(c, from, to);
        var kpis = new List<ReportKpi>
        {
            total.ByKg
                ? new ReportKpi(c.T("Факт недели, кг"), Kg(total.FactKg), c.F("план {0} кг", Kg(total.PlanKg)), Tone(total.ShareKg))
                : new ReportKpi(c.T("Факт недели, сум"), Number(total.FactSum), c.F("план {0}", Number(total.PlanSum)), Tone(total.Share)),
            total.ByKg
                ? new ReportKpi(c.T("Выполнение"), Percent(total.ShareKg), $"{Number(total.FactSum)} {c.T("сум")}", Tone(total.ShareKg))
                : new ReportKpi(c.T("Выполнение"), Percent(total.Share), $"{Kg(total.FactKg)} / {Kg(total.PlanKg)} {c.T("кг")}", Tone(total.Share)),
            new(c.T("Визиты"), total.VisitPlan is null ? Number(total.Visits) : $"{total.Visits} / {total.VisitPlan}", Percent(total.VisitShare)),
            new(c.T("АКБ"), total.AkbTarget is null ? Number(total.Akb) : $"{total.Akb} / {Number(total.AkbTarget)}", Percent(total.AkbShare), Tone(total.AkbShare)),
            new(c.T("Заказов с акцией"), Number(total.PromoOrders)),
        };

        var tables = new List<ReportTable>();
        string caption;
        if (c.Role == TelegramReportRoles.Tp)
        {
            var me = c.Scope.Agent!;
            var peers = (c.Peers ?? [me]).Select(p => (p.Id, M: Of(c, [p], from, to, visitKnown))).OrderByDescending(p => p.M.Main ?? -1).ToList();
            var place = peers.FindIndex(p => p.Id == me.Id) + 1;
            tables.Add(new ReportTable(c.T("1. Мои итоги недели"), [new("№", false, 0.4f), new(c.T("Показатель"), false, 2.4f), new(c.T("План")), new(c.T("Факт")), new("%", Weight: 0.6f)],
            [
                ["1", c.T("Продажи, сум"), Number(total.PlanSum), Number(total.FactSum), PercentCell(total.Share)],
                ["2", c.T("Продажи, кг"), Kg(total.PlanKg), Kg(total.FactKg), PercentCell(total.ShareKg)],
                ["3", c.T("Визиты"), Number(total.VisitPlan), Number(total.Visits), PercentCell(total.VisitShare)],
                ["4", c.T("АКБ (точки с заказом)"), Number(total.AkbTarget), Number(total.Akb), PercentCell(total.AkbShare)],
                ["5", c.T("Заказов с акцией"), "", Number(total.PromoOrders), ""],
                ["6", c.T("Место в регионе (по % плана)"), "", c.F("{0} из {1}", place, peers.Count), ""],
            ]));
            tables.Add(CategoryTable(c, c.Scope.Agents, categories, from, to));
            tables.Add(DaysTable(c, c.Scope.Agents, from, to));
            caption = total.ByKg
                ? c.Job.Slot == "monday"
                    ? c.F("Обновлённые итоги прошлой недели (с учётом заказов после субботней рассылки): {0} кг ({1} плана).", Kg(total.FactKg), Percent(total.ShareKg))
                    : c.F("Итоги недели: {0} кг ({1} плана), {2} сум, визитов {3} из {4}, АКБ {5} из {6}. Место в регионе: {7} из {8}.", Kg(total.FactKg),
                        Percent(total.ShareKg), Number(total.FactSum), total.Visits, Number(total.VisitPlan), total.Akb, Number(total.AkbTarget), place, peers.Count)
                : c.Job.Slot == "monday"
                    ? c.F("Обновлённые итоги прошлой недели (с учётом заказов после субботней рассылки): {0} сум ({1} плана).", Number(total.FactSum), Percent(total.Share))
                    : c.F("Итоги недели: {0} сум ({1} плана), {2} кг, визитов {3} из {4}, АКБ {5} из {6}. Место в регионе: {7} из {8}.", Number(total.FactSum),
                        Percent(total.Share), Kg(total.FactKg), total.Visits, Number(total.VisitPlan), total.Akb, Number(total.AkbTarget), place, peers.Count);
        }
        else
        {
            var rm = c.Role == TelegramReportRoles.Rm;
            var ranked = units.OrderByDescending(u => u.M.Main ?? -1).Select((u, i) => (u.Name, u.M, Place: i + 1)).ToList();
            var ordered = rm ? units.Select(u => ranked.First(r => r.Name == u.Name)).ToList() : ranked;
            tables.Add(new ReportTable(rm ? c.T("1. План и факт недели по регионам") : c.T("1. Рейтинг ТП за неделю"),
                [new("№", false, 0.35f), new(ReportKit.UnitTitle(c), false, 2.2f), new(c.T("План, сум"), Weight: 1.15f), new(c.T("Факт, сум"), Weight: 1.15f), new(c.T("% (сум)"), Weight: 0.6f),
                    new(c.T("План, кг"), Weight: 0.8f), new(c.T("Факт, кг"), Weight: 0.8f), new(c.T("% (кг)"), Weight: 0.6f), new(c.T("Визиты план"), Weight: 0.7f),
                    new(c.T("Визиты факт"), Weight: 0.7f), new(c.T("% визитов"), Weight: 0.6f), new(c.T("Заказов с акцией"), Weight: 0.75f), new(c.T("АКБ цель"), Weight: 0.6f),
                    new(c.T("АКБ факт"), Weight: 0.6f), new(c.T("% АКБ"), Weight: 0.55f), new(c.T("Место"), Weight: 0.5f)],
                ordered.Select((u, i) => (IReadOnlyList<ReportCell>)[$"{i + 1}", u.Name, Number(u.M.PlanSum), Number(u.M.FactSum), PercentCell(u.M.Share), Kg(u.M.PlanKg),
                    Kg(u.M.FactKg), PercentCell(u.M.ShareKg), Number(u.M.VisitPlan), Number(u.M.Visits), Percent(u.M.VisitShare), Number(u.M.PromoOrders),
                    Number(u.M.AkbTarget), Number(u.M.Akb), PercentCell(u.M.AkbShare), new($"{u.Place}", Bold: true)]).ToList(),
                ["", new(ReportKit.TotalTitle(c), Bold: true), new(Number(total.PlanSum), Bold: true), new(Number(total.FactSum), Bold: true), PercentCell(total.Share, true),
                    Kg(total.PlanKg), Kg(total.FactKg), PercentCell(total.ShareKg, true), Number(total.VisitPlan), Number(total.Visits), Percent(total.VisitShare),
                    Number(total.PromoOrders), Number(total.AkbTarget), Number(total.Akb), PercentCell(total.AkbShare, true), ""],
                total.ByKg
                    ? rm ? c.T("Место — по % выполнения плана (кг)") : c.T("Отсортировано по % выполнения плана (кг)")
                    : rm ? c.T("Место — по % выполнения плана (сум)") : c.T("Отсортировано по % выполнения плана (сум)")));
            tables.Add(CategoryTable(c, c.Scope.Agents, categories, from, to));
            tables.Add(DaysTable(c, c.Scope.Agents, from, to));
            caption = $"{c.T(ReportCatalog.Title(c.Job.Report))} · {c.Scope.Title} · {period}\n" +
                      (c.Job.Slot == "monday" ? c.T("Обновлённые итоги с учётом заказов после субботней рассылки.") + " " : "") +
                      (total.ByKg
                          ? c.F("Факт {0} кг ({1} плана), {2} сум, визитов {3} из {4}, АКБ {5} из {6}.", Kg(total.FactKg), Percent(total.ShareKg), Number(total.FactSum), total.Visits,
                              Number(total.VisitPlan), total.Akb, Number(total.AkbTarget))
                          : c.F("Факт {0} сум ({1} плана), {2} кг, визитов {3} из {4}, АКБ {5} из {6}.", Number(total.FactSum), Percent(total.Share), Kg(total.FactKg), total.Visits,
                              Number(total.VisitPlan), total.Akb, Number(total.AkbTarget)));
        }

        var problems = new List<string>();
        if (c.Role != TelegramReportRoles.Tp)
        {
            var low = units.Where(u => u.M.Main is < ReportFormat.RedBelow).OrderBy(u => u.M.Main).Select(u => $"{u.Name} {Percent(u.M.Main)}").ToList();
            if (low.Count > 0)
            {
                problems.Add(c.F("Ниже 80% плана недели: {0}", string.Join(", ", low.Take(8))));
            }
        }

        if (c.Job.Slot == "monday")
        {
            notes.Add(c.T("Пересчитано заново за пн–сб: заказы, проведённые после субботней рассылки, учтены один раз"));
        }

        var summary = total.ByKg
            ? c.F("Неделя {0}: {1} ({2})", period, $"{Kg(total.FactKg)} {c.T("кг")}", Percent(total.ShareKg))
            : c.F("Неделя {0}: {1} ({2})", period, Short(total.FactSum, c.Language), Percent(total.Share));
        var title = ReportKit.Title(c); // период недели — справа в шапке
        return ReportKit.Document(c, title, period, kpis, tables, problems, notes, caption, summary, incomplete);
    }

    private static ReportTable CategoryTable(ReportContext c, IReadOnlyList<Sales.Metrics.ReportPerson> agents, IReadOnlyList<string> categories, DateOnly from, DateOnly to)
    {
        var days = ReportSchedules.WorkingDays(from, to).ToList();
        var orders = agents.SelectMany(a => c.Facts.OrdersOf(a.Id, from, to)).ToList();
        var rows = categories.Select((cat, i) =>
        {
            var kgPlans = agents.SelectMany(a => days.Select(d => ReportKit.Whole(c.Facts.DayPlanCategoryKg(a.Id, d, cat)))).Where(v => v is not null).ToList();
            var planKg = kgPlans.Count == 0 ? (decimal?)null : kgPlans.Sum(v => v!.Value);
            var planSum = ReportKit.Whole(c.Facts.CategorySum(cat, planKg));
            var fact = ReportFacts.Amount(orders, cat);
            return (IReadOnlyList<ReportCell>)[$"{i + 1}", cat, Number(planSum), Number(fact.Sum), PercentCell(ReportFormat.Share(fact.Sum, planSum)), Kg(planKg), Kg(fact.Kg),
                PercentCell(ReportFormat.Share(fact.Kg, planKg))];
        }).ToList();
        return new ReportTable(c.T("Категории товаров за неделю — сум и кг"),
            [new("№", false, 0.4f), new(c.T("Категория"), false, 2f), new(c.T("План, сум")), new(c.T("Факт, сум")), new(c.T("% (сум)"), Weight: 0.7f), new(c.T("План, кг")),
                new(c.T("Факт, кг")), new(c.T("% (кг)"), Weight: 0.7f)], rows, null, c.T("сум — план, кг × средняя цена категории за 30 дней"));
    }

    private static ReportTable DaysTable(ReportContext c, IReadOnlyList<Sales.Metrics.ReportPerson> agents, DateOnly from, DateOnly to)
    {
        var names = c.Language == "uz" ? new[] { "Du", "Se", "Ch", "Pa", "Ju", "Sh", "Ya" } : new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
        var rows = new List<IReadOnlyList<ReportCell>>();
        decimal planTotal = 0, factTotal = 0;
        var known = false;
        var i = 0;
        var workDays = ReportSchedules.WorkingDays(from, to).ToList();
        // Плана в сумах ни у кого нет, а в кг есть — дни по кг (иначе в таблице одни «—»).
        var byKg = !workDays.Any(d => agents.Any(a => c.Facts.DayPlanSum(a.Id, d) is not null)) && workDays.Any(d => agents.Any(a => c.Facts.DayPlanKg(a.Id, d) is not null));
        string Cell(decimal? v) => byKg ? Kg(v) : Number(v);
        foreach (var day in workDays)
        {
            var plans = agents.Select(a => ReportKit.Whole(byKg ? c.Facts.DayPlanKg(a.Id, day) : c.Facts.DayPlanSum(a.Id, day))).Where(v => v is not null).ToList();
            decimal? plan = plans.Count == 0 ? null : plans.Sum(v => v!.Value);
            var fact = agents.Sum(a => c.Facts.OrdersOf(a.Id, day, day).Sum(o => byKg ? o.Kg : o.Sum));
            known |= plan is not null;
            planTotal += plan ?? 0;
            factTotal += fact;
            rows.Add([$"{++i}", $"{names[((int)day.DayOfWeek + 6) % 7]} {day.ToString("dd.MM", CultureInfo.InvariantCulture)}", Cell(plan), Cell(fact), PercentCell(ReportFormat.Share(fact, plan))]);
        }

        return new ReportTable(byKg ? c.T("Продажи по дням недели, кг") : c.T("Продажи по дням недели, сум"),
            [new("№", false, 0.4f), new(c.T("День"), false, 1.4f), new(byKg ? c.T("План, кг") : c.T("План, сум")), new(byKg ? c.T("Факт, кг") : c.T("Факт, сум")), new(c.T("% плана"), Weight: 0.7f)],
            rows, ["", new(c.T("Итого неделя"), Bold: true), new(Cell(known ? planTotal : null), Bold: true), new(Cell(factTotal), Bold: true),
                PercentCell(ReportFormat.Share(factTotal, known ? planTotal : null), true)]);
    }
}

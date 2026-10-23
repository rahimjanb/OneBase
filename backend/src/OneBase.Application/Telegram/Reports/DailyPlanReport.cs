using OneBase.Domain.Telegram;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// 1. Дневной план (лист «1 Дневной план»): 8:30 — план дня в сумах и кг, по категориям, план визитов; 13:00 — промежуточный итог и
/// «осталось»; 18:00 — итог дня с обоими срезами. РМ — по регионам, СВ — по ТП региона, ТП — личные цифры и категории.
/// </summary>
public static class DailyPlanReport
{
    public static ReportDocument Build(ReportContext c)
    {
        var day = c.Date;
        var notes = new List<string>();
        var incomplete = ReportKit.Freshness(c, notes, needsFact: !c.Job.IsMorning);
        var visitKnown = ReportKit.VisitPlanKnown(c, day, day);
        if (!visitKnown)
        {
            notes.Add(c.T("План визитов на этот день не загружен из Linko"));
        }

        var units = ReportKit.Units(c).Select(u => (u.Name, u.Agents, M: DayMetrics.Sum(u.Agents.Select(a => ReportKit.Day(c, a.Id, day, visitKnown)).ToList()))).ToList();
        var total = DayMetrics.Sum(units.Select(u => u.M).ToList());
        ReportKit.WithoutPlan(c, notes, c.Role == TelegramReportRoles.Rm
            ? c.Scope.Agents.Select(a => (a.Name, ReportKit.Day(c, a.Id, day, visitKnown)))
            : units.Select(u => (u.Name, u.M)));

        var categories = Categories(c, day, day);
        var tables = c.Role == TelegramReportRoles.Tp ? Personal(c, units[0].M, categories) : Summary(c, units, total, categories);
        var problems = Problems(c, units, total);
        var kpis = Kpis(c, total);
        var caption = Caption(c, total);
        var summary = $"{c.T("План")} {Short(total.PlanSum, c.Language)}" + (c.Job.IsMorning ? "" : $" · {c.T("факт")} {Short(total.FactSum, c.Language)} ({Percent(total.Share)})");
        return ReportKit.Document(c, ReportKit.Title(c), Date(day), kpis, tables, problems, notes, caption, summary, incomplete);
    }

    private static List<ReportKpi> Kpis(ReportContext c, DayMetrics t)
    {
        var list = new List<ReportKpi>
        {
            new(c.T("План дня, сум"), Number(t.PlanSum), $"{Kg(t.PlanKg)} {c.T("кг")}"),
        };
        if (!c.Job.IsMorning)
        {
            var share = c.Evening ? t.Share : t.Share13;
            var fact = c.Evening ? t.FactSum : t.FactSum13;
            var factKg = c.Evening ? t.FactKg : t.FactKg13;
            list.Add(new(c.F("Факт {0}, сум", c.Evening ? c.CutoffText : c.MiddayText), Number(fact), $"{Kg(factKg)} {c.T("кг")}", Tone(share)));
            list.Add(new(c.T("Выполнение"), Percent(share), c.Evening ? null : c.F("осталось {0} сум", Number(Math.Max(0, (t.PlanSum ?? 0) - fact))), Tone(share)));
        }

        list.Add(new(c.T("Визиты"), t.VisitPlan is null ? "—" : c.Job.IsMorning ? Number(t.VisitPlan) : $"{(c.Evening ? t.Visits : t.Visits13)} / {t.VisitPlan}",
            c.Job.IsMorning ? c.T("по маршруту") : c.T("факт / план")));
        if (!c.Job.IsMorning)
        {
            list.Add(new(c.T("Заказов"), Number(c.Evening ? t.Orders : t.Orders13)));
        }

        return list;
    }

    /// <summary>РМ и СВ: план дня, план по категориям в сумах и кг, промежуточные итоги.</summary>
    private static List<ReportTable> Summary(ReportContext c, IReadOnlyList<(string Name, IReadOnlyList<Sales.Metrics.ReportPerson> Agents, DayMetrics M)> units,
        DayMetrics total, IReadOnlyList<string> categories)
    {
        var day = c.Date;
        var unit = ReportKit.UnitTitle(c);
        var rm = c.Role == TelegramReportRoles.Rm;
        var tables = new List<ReportTable>();

        if (c.Job.IsMorning || !c.Evening)
        {
            // Третья колонка: у РМ — ТП в регионе; у СВ в 13:00 — заказов к срезу; утром заказов ещё нет — колонки нет.
            var third = rm ? c.T("ТП, чел.") : c.Job.IsMorning ? null : c.T("Заказов");
            IReadOnlyList<ReportCell> Line(string n, ReportCell name, DayMetrics m, bool bold)
            {
                var row = new List<ReportCell> { n, name };
                if (third is not null)
                {
                    row.Add(rm ? Number(m.Agents) : Number(m.Orders13));
                }

                row.AddRange([new(Number(m.PlanSum), Bold: bold), new(Kg(m.PlanKg), Bold: bold), Number(m.VisitPlan), Number(m.PricePerKg)]);
                return row;
            }

            var columns = new List<ReportColumn> { new("№", false, 0.4f), new(unit, false, 3) };
            if (third is not null)
            {
                columns.Add(new(third, Weight: 0.9f));
            }

            columns.AddRange([new(c.T("План, сум")), new(c.T("План, кг")), new(c.T("Визиты план")), new(c.T("Ср. цена, сум/кг"))]);
            tables.Add(new ReportTable(c.T("1. Общий план продаж на день — сум и кг"), columns,
                units.Select((u, i) => Line($"{i + 1}", u.Name, u.M, false)).ToList(),
                Line("", new(ReportKit.TotalTitle(c), Bold: true), total, true)));
        }

        if (categories.Count > 0 && c.Job.IsMorning)
        {
            tables.Add(CategoryPlan(c, units, categories, sums: true, c.T("2. План по категориям товаров на день, сум")));
            tables.Add(CategoryPlan(c, units, categories, sums: false, c.T("3. План по категориям товаров на день, кг")));
        }

        if (!c.Job.IsMorning)
        {
            var evening = c.Evening;
            var columns = new List<ReportColumn> { new("№", false, 0.4f), new(unit, false, 2.6f), new(c.T("План, сум"), Weight: 1.2f), new(c.F("Факт {0}, сум", c.MiddayText), Weight: 1.2f),
                new(c.F("% {0}", c.MiddayText), Weight: 0.7f), new(c.T("План, кг")), new(c.F("Факт {0}, кг", c.MiddayText)) };
            if (evening)
            {
                columns.AddRange([new(c.F("Факт {0}, сум", c.CutoffText), Weight: 1.2f), new(c.F("% {0}", c.CutoffText), Weight: 0.7f), new(c.F("Факт {0}, кг", c.CutoffText))]);
            }

            columns.AddRange([new(c.T("Визиты план"), Weight: 0.8f), new(c.F("Визиты {0}", c.MiddayText), Weight: 0.8f)]);
            if (evening)
            {
                columns.Add(new(c.F("Визиты {0}", c.CutoffText), Weight: 0.8f));
            }

            IReadOnlyList<ReportCell> Row(string n, string name, DayMetrics m, bool bold)
            {
                var row = new List<ReportCell> { n, new(name, Bold: bold), new(Number(m.PlanSum), Bold: bold), new(Number(m.FactSum13), Bold: bold), PercentCell(m.Share13, bold),
                    Kg(m.PlanKg), Kg(m.FactKg13) };
                if (evening)
                {
                    row.AddRange([new(Number(m.FactSum), Bold: bold), PercentCell(m.Share, bold), Kg(m.FactKg)]);
                }

                row.AddRange([Number(m.VisitPlan), Number(m.Visits13)]);
                if (evening)
                {
                    row.Add(Number(m.Visits));
                }

                return row;
            }

            tables.Add(new ReportTable(evening ? c.T("Итоги дня — сум и кг") : c.T("Промежуточный итог — сум и кг"), columns,
                units.Select((u, i) => Row($"{i + 1}", u.Name, u.M, false)).ToList(), Row("", ReportKit.TotalTitle(c), total, true),
                c.T("% — выполнение дневного плана; красным — ниже 80%")));

            if (categories.Count > 0)
            {
                tables.Add(CategoryShare(c, units, categories, evening));
            }
        }

        return tables;
    }

    private static ReportTable CategoryPlan(ReportContext c, IReadOnlyList<(string Name, IReadOnlyList<Sales.Metrics.ReportPerson> Agents, DayMetrics M)> units,
        IReadOnlyList<string> categories, bool sums, string title)
    {
        decimal? Plan(IReadOnlyList<Sales.Metrics.ReportPerson> agents, string cat)
        {
            var kg = agents.Select(a => c.Facts.DayPlanCategoryKg(a.Id, c.Date, cat)).Where(x => x is not null).ToList();
            var totalKg = kg.Count == 0 ? (decimal?)null : kg.Sum(x => x!.Value);
            return sums ? c.Facts.CategorySum(cat, totalKg) : totalKg;
        }

        string Cell(decimal? v) => sums ? Number(v) : Kg(v);
        var columns = new List<ReportColumn> { new("№", false, 0.4f), new(ReportKit.UnitTitle(c), false, 2.4f) };
        columns.AddRange(categories.Select(cat => new ReportColumn(cat)));
        columns.Add(new(sums ? c.T("Итого, сум") : c.T("Итого, кг"), Weight: 1.1f));
        var rows = units.Select((u, i) =>
        {
            var values = categories.Select(cat => Plan(u.Agents, cat)).ToList();
            var row = new List<ReportCell> { $"{i + 1}", u.Name };
            row.AddRange(values.Select(Cell).Select(x => (ReportCell)x));
            row.Add(new(Cell(values.Any(v => v is not null) ? values.Sum(v => v ?? 0) : null), Bold: true));
            return (IReadOnlyList<ReportCell>)row;
        }).ToList();
        var all = c.Scope.Agents;
        var totals = categories.Select(cat => Plan(all, cat)).ToList();
        var total = new List<ReportCell> { "", new(ReportKit.TotalTitle(c), Bold: true) };
        total.AddRange(totals.Select(v => new ReportCell(Cell(v), Bold: true)));
        total.Add(new(Cell(totals.Any(v => v is not null) ? totals.Sum(v => v ?? 0) : null), Bold: true));
        return new ReportTable(title, columns, rows, total, sums ? c.T("сум — план, кг × средняя цена категории за 30 дней") : null);
    }

    /// <summary>Выполнение плана по категориям к срезу, % (сум).</summary>
    private static ReportTable CategoryShare(ReportContext c, IReadOnlyList<(string Name, IReadOnlyList<Sales.Metrics.ReportPerson> Agents, DayMetrics M)> units,
        IReadOnlyList<string> categories, bool evening)
    {
        var cutoff = evening ? (TimeOnly?)null : c.MiddayCutoff;
        decimal? Share(IReadOnlyList<Sales.Metrics.ReportPerson> agents, string cat)
        {
            var kg = agents.Select(a => c.Facts.DayPlanCategoryKg(a.Id, c.Date, cat)).Where(x => x is not null).ToList();
            var plan = c.Facts.CategorySum(cat, kg.Count == 0 ? null : kg.Sum(x => x!.Value));
            var fact = agents.Sum(a => ReportFacts.Amount(c.Facts.OrdersOf(a.Id, c.Date, c.Date, cutoff ?? (c.Cutoff == TimeOnly.MaxValue ? null : c.Cutoff)), cat).Sum);
            return ReportFormat.Share(fact, plan);
        }

        var columns = new List<ReportColumn> { new("№", false, 0.4f), new(ReportKit.UnitTitle(c), false, 2.4f) };
        columns.AddRange(categories.Select(cat => new ReportColumn(cat)));
        columns.Add(new(c.T("Итого"), Weight: 0.9f));
        var rows = units.Select((u, i) =>
        {
            var row = new List<ReportCell> { $"{i + 1}", u.Name };
            row.AddRange(categories.Select(cat => PercentCell(Share(u.Agents, cat))));
            row.Add(PercentCell(evening ? u.M.Share : u.M.Share13, true));
            return (IReadOnlyList<ReportCell>)row;
        }).ToList();
        var all = c.Scope.Agents;
        var total = new List<ReportCell> { "", new(ReportKit.TotalTitle(c), Bold: true) };
        total.AddRange(categories.Select(cat => PercentCell(Share(all, cat), true)));
        var t = DayMetrics.Sum(units.Select(u => u.M).ToList());
        total.Add(PercentCell(evening ? t.Share : t.Share13, true));
        return new ReportTable(c.F("Выполнение плана по категориям к {0}, % (сум)", evening ? c.CutoffText : c.MiddayText), columns, rows, total,
            c.T("Красным — ниже 80%"));
    }

    /// <summary>ТП: мой план и итоги, мой план по категориям.</summary>
    private static List<ReportTable> Personal(ReportContext c, DayMetrics m, IReadOnlyList<string> categories)
    {
        var agent = c.Scope.Agent!.Id;
        var day = c.Date;
        var morning = c.Job.IsMorning;
        var evening = c.Evening;
        var columns = new List<ReportColumn> { new("№", false, 0.4f), new(c.T("Показатель"), false, 2.2f), new(c.T("План на день")) };
        if (!morning)
        {
            columns.AddRange([new(c.F("Факт {0}", c.MiddayText)), new(c.F("% {0}", c.MiddayText), Weight: 0.7f), new(c.F("Осталось после {0}", c.MiddayText))]);
        }

        if (evening)
        {
            columns.AddRange([new(c.F("Факт {0}", c.CutoffText)), new(c.F("% {0}", c.CutoffText), Weight: 0.7f)]);
        }

        IReadOnlyList<ReportCell> Row(string n, string name, string plan, string f13, decimal? s13, string left, string f18, decimal? s18)
        {
            var row = new List<ReportCell> { n, name, plan };
            if (!morning)
            {
                row.AddRange([f13, PercentCell(s13), left]);
            }

            if (evening)
            {
                row.AddRange([new ReportCell(f18, Bold: true), PercentCell(s18, true)]);
            }

            return row;
        }

        var visitShare13 = Share(m.Visits13, m.VisitPlan);
        var visitShare = Share(m.Visits, m.VisitPlan);
        var tables = new List<ReportTable>
        {
            new(c.T(morning ? "1. Мой план на день" : evening ? "1. Мой план на день и итог дня" : "1. Мой план на день и промежуточный итог"), columns,
            [
                Row("1", c.T("Продажи, сум"), Number(m.PlanSum), Number(m.FactSum13), m.Share13, Number(Left(m.PlanSum, m.FactSum13)), Number(m.FactSum), m.Share),
                Row("2", c.T("Продажи, кг"), Kg(m.PlanKg), Kg(m.FactKg13), m.ShareKg13, Kg(Left(m.PlanKg, m.FactKg13)), Kg(m.FactKg), m.ShareKg),
                Row("3", c.T("Визиты"), Number(m.VisitPlan), Number(m.Visits13), visitShare13, Number(m.VisitPlan is { } vp ? Math.Max(0, vp - m.Visits13) : null), Number(m.Visits), visitShare),
                Row("4", c.T("Заказы, шт"), "—", Number(m.Orders13), null, "—", Number(m.Orders), null),
            ]),
        };

        if (categories.Count > 0)
        {
            var cols = new List<ReportColumn> { new("№", false, 0.4f), new(c.T("Категория"), false, 1.8f), new(c.T("План, сум")), new(c.T("План, кг")) };
            if (!morning)
            {
                cols.AddRange([new(c.F("Факт {0}, сум", c.MiddayText)), new(c.F("Факт {0}, кг", c.MiddayText))]);
            }

            if (evening)
            {
                cols.AddRange([new(c.F("Факт {0}, сум", c.CutoffText)), new(c.F("Факт {0}, кг", c.CutoffText)), new(c.F("% {0} (сум)", c.CutoffText), Weight: 0.8f)]);
            }

            if (!morning)
            {
                cols.Add(new(c.F("Осталось после {0}, сум", c.MiddayText), Weight: 1.2f));
            }

            var by13 = c.Facts.OrdersOf(agent, day, day, c.MiddayCutoff).ToList();
            var all = c.Facts.OrdersOf(agent, day, day, c.Cutoff == TimeOnly.MaxValue ? null : c.Cutoff).ToList();
            var rows = categories.Select((cat, i) =>
            {
                var planKg = ReportKit.Whole(c.Facts.DayPlanCategoryKg(agent, day, cat));
                var planSum = ReportKit.Whole(c.Facts.CategorySum(cat, planKg));
                var f13 = ReportFacts.Amount(by13, cat);
                var f18 = ReportFacts.Amount(all, cat);
                var row = new List<ReportCell> { $"{i + 1}", cat, Number(planSum), Kg(planKg) };
                if (!morning)
                {
                    row.AddRange([Number(f13.Sum), Kg(f13.Kg)]);
                }

                if (evening)
                {
                    row.AddRange([Number(f18.Sum), Kg(f18.Kg), PercentCell(ReportFormat.Share(f18.Sum, planSum))]);
                }

                if (!morning)
                {
                    row.Add(Number(Left(planSum, f13.Sum)));
                }

                return (IReadOnlyList<ReportCell>)row;
            }).ToList();
            tables.Add(new ReportTable(c.T("2. Мой план по категориям — сум и кг"), cols, rows, null, c.T("сум — план, кг × средняя цена категории за 30 дней")));
        }

        return tables;
    }

    private static decimal? Left(decimal? plan, decimal fact) => plan is { } p ? Math.Max(0, p - fact) : null;

    /// <summary>Категории отчёта, где у области есть план или продажи за отрезок (пустые колонки не показываются).</summary>
    public static IReadOnlyList<string> Categories(ReportContext c, DateOnly from, DateOnly to)
    {
        var agents = c.Scope.Agents.Select(a => a.Id).ToList();
        return c.Facts.CategoriesOf(agents, to)
            .Where(cat => agents.Any(a => ReportSchedules.WorkingDays(from, to).Any(d => c.Facts.DayPlanCategoryKg(a, d, cat) is not null)
                                          || ReportFacts.Amount(c.Facts.OrdersOf(a, from, to), cat).Kg != 0))
            .ToList();
    }

    private static List<string> Problems(ReportContext c, IReadOnlyList<(string Name, IReadOnlyList<Sales.Metrics.ReportPerson> Agents, DayMetrics M)> units, DayMetrics t)
    {
        var list = new List<string>();
        if (c.Job.IsMorning)
        {
            if (t.PlanSum is { } plan && t.VisitPlan is > 0)
            {
                list.Add(c.F("Средний заказ для плана: {0} сум на визит по маршруту", Number(plan / t.VisitPlan.Value)));
            }

            return list;
        }

        if (!c.Evening)
        {
            if (t.PlanSum is { } plan && plan > t.FactSum13)
            {
                var hours = Math.Max(1m, (decimal)(new TimeOnly(18, 0) - c.MiddayCutoff).TotalHours);
                list.Add(c.F("До плана осталось {0} сум / {1} кг — по {2} сум в час до 18:00", Number(plan - t.FactSum13), Kg(Math.Max(0, (t.PlanKg ?? 0) - t.FactKg13)), Number((plan - t.FactSum13) / hours)));
            }

            if (c.Role != TelegramReportRoles.Tp)
            {
                var weak = units.Where(u => u.M.Share13 is < 0.25m).Select(u => $"{u.Name} {Percent(u.M.Share13)}").ToList();
                if (weak.Count > 0)
                {
                    list.Add(c.F("Ниже 25% к {0}: {1} — связаться и помочь с маршрутом", c.MiddayText, string.Join(", ", weak.Take(8))));
                }

                var none = units.Where(u => u.M.Orders13 == 0).Select(u => u.Name).ToList();
                if (none.Count > 0)
                {
                    list.Add(c.F("Без заказов к {0}: {1}", c.MiddayText, string.Join(", ", none.Take(8))));
                }
            }

            return list;
        }

        if (c.Role != TelegramReportRoles.Tp)
        {
            var low = units.Where(u => u.M.Share is < ReportFormat.RedBelow).OrderBy(u => u.M.Share)
                .Select(u => $"{u.Name} {Percent(u.M.Share)} ({Signed(u.M.FactSum - (u.M.PlanSum ?? 0))})").ToList();
            if (low.Count > 0)
            {
                list.Add(c.F("Ниже 80% плана: {0}", string.Join(", ", low.Take(8))));
            }

            var best = units.Where(u => u.M.Share is >= 1m).OrderByDescending(u => u.M.Share).Select(u => $"{u.Name} {Percent(u.M.Share)}").ToList();
            if (best.Count > 0)
            {
                list.Add(c.F("Выполнили план: {0}", string.Join(", ", best.Take(8))));
            }
        }
        else if (t.Share is < 1m && t.PlanSum is { } plan)
        {
            list.Add(c.F("Не хватило {0} сум до плана — завтра начните с крупных точек маршрута", Number(plan - t.FactSum)));
        }

        return list;
    }

    private static string Caption(ReportContext c, DayMetrics t)
    {
        var head = $"{c.T(ReportCatalog.Title(c.Job.Report))} · {c.Scope.Title} · {Date(c.Date)}";
        if (c.Role == TelegramReportRoles.Tp)
        {
            // Строку «Отчёт · ТП · день» добавит ReportKit.Document.
            return c.Job.IsMorning
                ? c.F("Доброе утро! План на сегодня: {0} сум / {1} кг, визитов по маршруту: {2}.", Number(t.PlanSum), Kg(t.PlanKg), Number(t.VisitPlan))
                : !c.Evening
                    ? c.F("Промежуточный итог: {0} сум ({1}), {2} кг. До плана осталось {3} сум / {4} кг. Визитов: {5} из {6}.", Number(t.FactSum13), Percent(t.Share13),
                        Kg(t.FactKg13), Number(Left(t.PlanSum, t.FactSum13)), Kg(Left(t.PlanKg, t.FactKg13)), t.Visits13, Number(t.VisitPlan))
                    : c.F("Итог дня: {0} сум ({1}), {2} кг, заказов: {3}, визитов: {4} из {5}.", Number(t.FactSum), Percent(t.Share), Kg(t.FactKg), t.Orders, t.Visits,
                        Number(t.VisitPlan));
        }

        return c.Job.IsMorning
            ? $"{head}\n{c.F("План дня: {0} сум / {1} кг, визитов по маршруту: {2}.", Number(t.PlanSum), Kg(t.PlanKg), Number(t.VisitPlan))}"
            : !c.Evening
                ? $"{head}\n{c.F("К {0}: {1} сум ({2}), {3} кг. До плана {4} сум.", c.MiddayText, Number(t.FactSum13), Percent(t.Share13), Kg(t.FactKg13), Number(Left(t.PlanSum, t.FactSum13)))}"
                : $"{head}\n{c.F("Итог дня: {0} сум ({1}), {2} кг, заказов {3}, визитов {4} из {5}.", Number(t.FactSum), Percent(t.Share), Kg(t.FactKg), t.Orders, t.Visits, Number(t.VisitPlan))}";
    }
}

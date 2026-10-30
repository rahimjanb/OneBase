using OneBase.Domain.Telegram;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// 5. Заказы по категориям товаров (лист «5 Заказы по категориям»), 13:00 и 18:00: количество заказов, сумма и вес по категориям отчёта
/// («Настройки продаж»), план дня и выполнение. Сумма и вес — строки заказа этой категории; заказ с тремя категориями — в каждой из трёх, но в
/// «Заказов всего» — один раз.
/// </summary>
public static class CategoriesReport
{
    public static ReportDocument Build(ReportContext c)
    {
        var day = c.Date;
        var notes = new List<string>();
        var incomplete = ReportKit.Freshness(c, notes, needsFact: true);
        var until = c.Cutoff == TimeOnly.MaxValue ? (TimeOnly?)null : c.Cutoff;
        var categories = DailyPlanReport.Categories(c, day, day);
        var units = ReportKit.Units(c).Select(u => (u.Name, u.Agents, Orders: u.Agents.SelectMany(a => c.Facts.OrdersOf(a.Id, day, day, until)).ToList())).ToList();
        var all = units.SelectMany(u => u.Orders).ToList();

        (decimal? Sum, decimal? Kg) Plan(IEnumerable<Sales.Metrics.ReportPerson> agents, string cat)
        {
            var kg = agents.Select(a => c.Facts.DayPlanCategoryKg(a.Id, day, cat)).Where(x => x is not null).ToList();
            var total = kg.Count == 0 ? (decimal?)null : kg.Sum(x => x!.Value);
            return (ReportKit.Whole(c.Facts.CategorySum(cat, total)), ReportKit.Whole(total));
        }

        var kpis = new List<ReportKpi> { new(c.T("Заказов всего"), Number(all.Count)), new(c.T("Сумма, сум"), Short(all.Sum(o => o.Sum), c.Language)),
            new(c.T("Вес, кг"), Kg(all.Sum(o => o.Kg))) };
        var planAll = categories.Select(cat => Plan(c.Scope.Agents, cat).Sum).Where(x => x is not null).ToList();
        var planTotal = planAll.Count == 0 ? (decimal?)null : planAll.Sum(x => x!.Value);
        var factInPlan = categories.Where(cat => Plan(c.Scope.Agents, cat).Sum is not null).Sum(cat => ReportFacts.Amount(all, cat).Sum);
        kpis.Add(new(c.T("Выполнение по категориям"), Percent(ReportFormat.Share(factInPlan, planTotal)), c.T("сум, категории с планом"), Tone(ReportFormat.Share(factInPlan, planTotal))));

        var tables = new List<ReportTable>();
        string caption;
        if (c.Role == TelegramReportRoles.Tp)
        {
            var me = c.Scope.Agent!;
            var rows = categories.Select((cat, i) =>
            {
                var amount = ReportFacts.Amount(all, cat);
                var (planSum, planKg) = Plan([me], cat);
                return (IReadOnlyList<ReportCell>)[$"{i + 1}", cat, Number(ReportFacts.OrdersWith(all, cat)), Number(amount.Sum), Kg(amount.Kg), Number(planSum), Kg(planKg),
                    PercentCell(ReportFormat.Share(amount.Sum, planSum)), PercentCell(ReportFormat.Share(amount.Kg, planKg))];
            }).ToList();
            var (ps, pk) = (ReportKit.Whole(c.Facts.DayPlanSum(me.Id, day)), ReportKit.Whole(c.Facts.DayPlanKg(me.Id, day)));
            tables.Add(new ReportTable(c.T("1. Мои заказы по категориям против плана дня"),
                [new("№", false, 0.4f), new(c.T("Категория"), false, 1.8f), new(c.T("Заказов, шт")), new(c.T("Сумма, сум")), new(c.T("Вес, кг")), new(c.T("План, сум")),
                    new(c.T("План, кг")), new(c.T("% плана (сум)")), new(c.T("% плана (кг)"))],
                rows, ["", new(c.T("Итого (заказов — уникальных)"), Bold: true), new(Number(all.Count), Bold: true), new(Number(all.Sum(o => o.Sum)), Bold: true),
                    new(Kg(all.Sum(o => o.Kg)), Bold: true), Number(ps), Kg(pk), PercentCell(ReportFormat.Share(all.Sum(o => o.Sum), ps), true),
                    PercentCell(ReportFormat.Share(all.Sum(o => o.Kg), pk), true)]));
            var weak = categories.Select(cat => (cat, S: ReportFormat.Share(ReportFacts.Amount(all, cat).Sum, Plan([me], cat).Sum))).Where(x => x.S is < ReportFormat.RedBelow).Select(x => x.cat).ToList();
            caption = (ps is null && pk is not null
                          ? c.F("Категории за день: {0} заказов на {1} сум, {2} кг ({3} плана по кг).", all.Count, Number(all.Sum(o => o.Sum)), Kg(all.Sum(o => o.Kg)),
                              Percent(ReportFormat.Share(all.Sum(o => o.Kg), pk)))
                          : c.F("Категории за день: {0} заказов на {1} сум ({2} плана).", all.Count, Number(all.Sum(o => o.Sum)), Percent(ReportFormat.Share(all.Sum(o => o.Sum), ps)))) +
                      (weak.Count > 0 ? " " + c.F("Ниже 80%: {0}.", string.Join(", ", weak)) : "");
        }
        else
        {
            tables.Add(Matrix(c, units, categories, c.T("1. Количество заказов по категориям, шт"), (orders, cat) => Number(ReportFacts.OrdersWith(orders, cat)),
                orders => Number(orders.Count), c.T("Заказов всего"), c.T("Один заказ может содержать несколько категорий")));
            tables.Add(Matrix(c, units, categories, c.T("2. Сумма заказов по категориям, сум"), (orders, cat) => Number(ReportFacts.Amount(orders, cat).Sum),
                orders => Number(orders.Sum(o => o.Sum)), c.T("Итого, сум"), null));
            tables.Add(Matrix(c, units, categories, c.T("3. Вес заказов по категориям, кг"), (orders, cat) => Kg(ReportFacts.Amount(orders, cat).Kg),
                orders => Kg(orders.Sum(o => o.Kg)), c.T("Итого, кг"), null));

            var columns = new List<ReportColumn> { new("№", false, 0.4f), new(ReportKit.UnitTitle(c), false, 2.4f) };
            columns.AddRange(categories.Select(cat => new ReportColumn(cat)));
            columns.Add(new(c.T("Итого")));
            IReadOnlyList<ReportCell> ShareRow(string n, string name, IReadOnlyList<Sales.Metrics.ReportPerson> agents, IReadOnlyList<FactOrder> orders, bool bold)
            {
                var row = new List<ReportCell> { n, new(name, Bold: bold) };
                row.AddRange(categories.Select(cat => PercentCell(ReportFormat.Share(ReportFacts.Amount(orders, cat).Sum, Plan(agents, cat).Sum), bold)));
                // Итог — от плана дня в сумах; плана в сумах нет, а в кг есть (большинство регионов) — по кг.
                var plans = agents.Select(a => ReportKit.Whole(c.Facts.DayPlanSum(a.Id, day))).Where(p => p is not null).ToList();
                var kgPlans = agents.Select(a => ReportKit.Whole(c.Facts.DayPlanKg(a.Id, day))).Where(p => p is not null).ToList();
                row.Add(PercentCell(plans.Count == 0 && kgPlans.Count > 0
                    ? ReportFormat.Share(orders.Sum(o => o.Kg), kgPlans.Sum(p => p!.Value))
                    : ReportFormat.Share(orders.Sum(o => o.Sum), plans.Count == 0 ? null : plans.Sum(p => p!.Value)), true));
                return row;
            }

            tables.Add(new ReportTable(c.T("4. Выполнение дневного плана по категориям, % (сум)"), columns,
                units.Select((u, i) => ShareRow($"{i + 1}", u.Name, u.Agents, u.Orders, false)).ToList(),
                ShareRow("", ReportKit.TotalTitle(c), c.Scope.Agents, all, true), c.T("Красным — ниже 80%")));
            caption = $"{c.T(ReportCatalog.Title(c.Job.Report))} · {c.Scope.Title} · {Date(day)}\n" +
                      c.F("Заказов {0} на {1} сум, {2} кг; выполнение по категориям {3}.", all.Count, Number(all.Sum(o => o.Sum)), Kg(all.Sum(o => o.Kg)),
                          Percent(ReportFormat.Share(factInPlan, planTotal)));
        }

        var problems = categories.Select(cat => (cat, S: ReportFormat.Share(ReportFacts.Amount(all, cat).Sum, Plan(c.Scope.Agents, cat).Sum)))
            .Where(x => x.S is < ReportFormat.RedBelow).OrderBy(x => x.S).Select(x => $"{x.cat} {Percent(x.S)}").ToList() is { Count: > 0 } low
            ? [c.F("Отстают от плана: {0}", string.Join(", ", low))]
            : new List<string>();
        if (categories.Count == 0)
        {
            notes.Add(c.T("Категории отчёта не настроены или по ним нет плана и продаж"));
        }

        var summary = c.F("Заказов {0} на {1}", all.Count, Short(all.Sum(o => o.Sum), c.Language));
        return ReportKit.Document(c, ReportKit.Title(c), Date(day), kpis, tables, problems, notes, caption, summary, incomplete);
    }

    private static ReportTable Matrix(ReportContext c, IReadOnlyList<(string Name, IReadOnlyList<Sales.Metrics.ReportPerson> Agents, List<FactOrder> Orders)> units,
        IReadOnlyList<string> categories, string title, Func<IReadOnlyList<FactOrder>, string, string> cell, Func<IReadOnlyList<FactOrder>, string> total, string totalTitle, string? hint)
    {
        var columns = new List<ReportColumn> { new("№", false, 0.4f), new(ReportKit.UnitTitle(c), false, 2.4f) };
        columns.AddRange(categories.Select(cat => new ReportColumn(cat)));
        columns.Add(new(totalTitle, Weight: 1.1f));
        IReadOnlyList<ReportCell> Row(string n, string name, IReadOnlyList<FactOrder> orders, bool bold)
        {
            var row = new List<ReportCell> { n, new(name, Bold: bold) };
            row.AddRange(categories.Select(cat => new ReportCell(cell(orders, cat), Bold: bold)));
            row.Add(new(total(orders), Bold: true));
            return row;
        }

        return new ReportTable(title, columns, units.Select((u, i) => Row($"{i + 1}", u.Name, u.Orders, false)).ToList(),
            Row("", ReportKit.TotalTitle(c), units.SelectMany(u => u.Orders).ToList(), true), hint);
    }
}

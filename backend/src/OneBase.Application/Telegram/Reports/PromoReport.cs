using OneBase.Domain.Telegram;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// 4. Заказы с акцией (лист «4 Заказы с акцией»), 13:00 и 18:00. Заказ с акцией — в нём есть строка со скидкой (структурный признак Linko:
/// скидка строки); название акции — сверкой с акциями Linko, как на странице «Акции» (не по тексту). Сумма акционного заказа — весь заказ.
/// </summary>
public static class PromoReport
{
    private const int Columns = 4;

    public static ReportDocument Build(ReportContext c)
    {
        var day = c.Date;
        var notes = new List<string>();
        var incomplete = ReportKit.Freshness(c, notes, needsFact: true);
        var until = c.Cutoff == TimeOnly.MaxValue ? (TimeOnly?)null : c.Cutoff;
        // Названия акций — данные; «Ручная скидка» (скидка без акции каталога) — подпись OneBase, она переводится.
        var units = ReportKit.Units(c)
            .Select(u => (u.Name, Orders: u.Agents.SelectMany(a => c.Facts.OrdersOf(a.Id, day, day, until))
                .Select(o => o with { Promos = o.Promos.Select(c.T).ToList() }).ToList()))
            .ToList();
        var all = units.SelectMany(u => u.Orders).ToList();
        var promo = all.Where(o => o.HasPromo).ToList();
        var promos = promo.SelectMany(o => o.Promos).GroupBy(p => p).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).ToList();
        var shown = promos.Take(Columns).ToList();
        var other = promos.Count > Columns;

        var kpis = new List<ReportKpi>
        {
            new(c.T("Заказов всего"), Number(all.Count)),
            new(c.T("С акцией"), Number(promo.Count), Percent(ReportFormat.Share(promo.Count, all.Count))),
            new(c.T("Сумма акционных заказов"), Short(promo.Sum(o => o.Sum), c.Language), Percent(ReportFormat.Share(promo.Sum(o => o.Sum), all.Sum(o => o.Sum))) + " " + c.T("продаж")),
        };

        var tables = new List<ReportTable>();
        string caption;
        if (c.Role == TelegramReportRoles.Tp)
        {
            var rows = promos.Select((p, i) =>
            {
                var own = promo.Where(o => o.Promos.Contains(p)).ToList();
                return (IReadOnlyList<ReportCell>)[$"{i + 1}", p, Number(own.Count), Number(own.Sum(o => o.Sum)), Percent(ReportFormat.Share(own.Count, all.Count))];
            }).ToList();
            if (rows.Count == 0)
            {
                rows.Add(["", c.T("Заказов с акцией нет"), "", "", ""]);
            }

            tables.Add(new ReportTable(c.T("1. Мои заказы с акцией"),
                [new("№", false, 0.4f), new(c.T("Акция"), false, 3.2f), new(c.T("Заказов")), new(c.T("Сумма, сум")), new(c.T("Доля от моих заказов"))],
                rows, ["", new(c.T("Итого с акцией"), Bold: true), new(Number(promo.Count), Bold: true), new(Number(promo.Sum(o => o.Sum)), Bold: true),
                    PercentCell(ReportFormat.Share(promo.Count, all.Count), true)]));
            tables.Add(new ReportTable(c.T("Всего моих заказов за день"), [new(c.T("Заказов"), false), new(c.T("Сумма, сум"))],
                [[Number(all.Count), Number(all.Sum(o => o.Sum))]]));
            caption = c.F("Акции за день: {0} заказ(ов) с акцией из {1} ({2}), на сумму {3} сум.", promo.Count, all.Count,
                Percent(ReportFormat.Share(promo.Count, all.Count)), Number(promo.Sum(o => o.Sum)));
        }
        else
        {
            var unit = ReportKit.UnitTitle(c);
            tables.Add(new ReportTable(c.T("1. Заказы с акцией и их доля"),
                [new("№", false, 0.4f), new(unit, false, 2.6f), new(c.T("Заказов всего")), new(c.T("С акцией")), new(c.T("Доля акционных заказов")),
                    new(c.T("Сумма акционных заказов, сум"), Weight: 1.3f), new(c.T("Факт продаж, сум"), Weight: 1.2f), new(c.T("Доля акций в продажах"))],
                units.Select((u, i) =>
                {
                    var p = u.Orders.Where(o => o.HasPromo).ToList();
                    return (IReadOnlyList<ReportCell>)[$"{i + 1}", u.Name, Number(u.Orders.Count), Number(p.Count), Percent(ReportFormat.Share(p.Count, u.Orders.Count)),
                        Number(p.Sum(o => o.Sum)), Number(u.Orders.Sum(o => o.Sum)), Percent(ReportFormat.Share(p.Sum(o => o.Sum), u.Orders.Sum(o => o.Sum)))];
                }).ToList(),
                ["", new(ReportKit.TotalTitle(c), Bold: true), new(Number(all.Count), Bold: true), new(Number(promo.Count), Bold: true),
                    new(Percent(ReportFormat.Share(promo.Count, all.Count)), Bold: true), new(Number(promo.Sum(o => o.Sum)), Bold: true),
                    new(Number(all.Sum(o => o.Sum)), Bold: true), new(Percent(ReportFormat.Share(promo.Sum(o => o.Sum), all.Sum(o => o.Sum))), Bold: true)]));

            if (promos.Count > 0)
            {
                tables.Add(Breakdown(c, units, shown, other, sums: false, c.T("2. Разбивка по акциям — количество заказов")));
                tables.Add(Breakdown(c, units, shown, other, sums: true, c.T("3. Разбивка по акциям — сумма, сум")));
            }

            caption = $"{c.T(ReportCatalog.Title(c.Job.Report))} · {c.Scope.Title} · {Date(day)}\n" +
                      c.F("Заказов с акцией {0} из {1} ({2}), на сумму {3} сум.", promo.Count, all.Count, Percent(ReportFormat.Share(promo.Count, all.Count)),
                          Number(promo.Sum(o => o.Sum)));
        }

        if (all.Count > 0 && promo.Count == 0)
        {
            notes.Add(c.T("Строк со скидкой в заказах за день нет"));
        }

        var problems = new List<string>();
        if (promos.Count > 0)
        {
            problems.Add(c.F("Больше всего заказов по акции «{0}»", promos[0]));
        }

        var summary = c.F("С акцией {0} из {1} заказов", promo.Count, all.Count);
        return ReportKit.Document(c, ReportKit.Title(c), Date(day), kpis, tables, problems, notes, caption, summary, incomplete);
    }

    private static ReportTable Breakdown(ReportContext c, IReadOnlyList<(string Name, List<FactOrder> Orders)> units, IReadOnlyList<string> shown, bool other,
        bool sums, string title)
    {
        string Cell(IEnumerable<FactOrder> orders) => sums ? Number(orders.Sum(o => o.Sum)) : Number(orders.Count());
        var columns = new List<ReportColumn> { new("№", false, 0.4f), new(ReportKit.UnitTitle(c), false, 2.4f) };
        columns.AddRange(shown.Select(p => new ReportColumn(p, Weight: 1.3f)));
        if (other)
        {
            columns.Add(new(c.T("Другие акции")));
        }

        columns.Add(new(c.T("Итого")));
        IReadOnlyList<ReportCell> Row(string n, string name, IReadOnlyList<FactOrder> orders, bool bold)
        {
            var promo = orders.Where(o => o.HasPromo).ToList();
            var row = new List<ReportCell> { n, new(name, Bold: bold) };
            row.AddRange(shown.Select(p => new ReportCell(Cell(promo.Where(o => o.Promos.Contains(p))), Bold: bold)));
            if (other)
            {
                row.Add(new(Cell(promo.Where(o => !o.Promos.Any(shown.Contains))), Bold: bold));
            }

            row.Add(new(Cell(promo), Bold: true));
            return row;
        }

        return new ReportTable(title, columns, units.Select((u, i) => Row($"{i + 1}", u.Name, u.Orders, false)).ToList(),
            Row("", ReportKit.TotalTitle(c), units.SelectMany(u => u.Orders).ToList(), true), c.T("Заказ с двумя акциями — в обеих колонках, в «Итого» — один раз"));
    }
}

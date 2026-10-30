using OneBase.Domain.Telegram;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// 2. Клиенты без закупа (лист «2 Клиенты без закупа»), 8:30: точки ТП без подтверждённой покупки N и более дней. РМ — сводно по регионам,
/// СВ — по ТП региона, ТП — его точки на сегодняшнем маршруте («зайти обязательно») и все его точки без закупа (крупнейшие).
/// </summary>
public static class NoPurchaseReport
{
    private const int ListLimit = 15;

    public static ReportDocument Build(ReportContext c)
    {
        var data = c.NoPurchase ?? new Dictionary<long, NoPurchaseAgent>();
        var notes = new List<string>();
        if (!ReportKit.VisitPlanKnown(c, c.Date, c.Date))
        {
            notes.Add(c.T("Маршрут на сегодня не загружен из Linko — «на маршруте» неизвестно"));
        }

        NoPurchaseAgent Of(long agent) => data.GetValueOrDefault(agent) ?? new NoPurchaseAgent(agent, 0, 0, 0, 0, 0, []);
        var units = ReportKit.Units(c).Select(u => (u.Name, u.Agents, Rows: u.Agents.Select(a => Of(a.Id)).ToList())).ToList();
        var all = units.SelectMany(u => u.Rows).ToList();
        var total = Sum(all, units.Sum(u => u.Agents.Count));
        var days = c.NoPurchaseDays;

        var kpis = new List<ReportKpi>
        {
            new(c.T("База: закуп за 12 мес., точек"), Number(total.Base)),
            new(c.F("Без закупа ≥{0} дн.", days), Number(total.Silent), Percent(ReportFormat.Share(total.Silent, total.Base)), total.Silent > 0 ? ReportTone.Warn : ReportTone.Good),
            new(c.T("На маршруте сегодня"), Number(total.OnRoute)),
            new(c.T("Из них без закупа"), Number(total.SilentOnRoute), c.T("зайти обязательно"), total.SilentOnRoute > 0 ? ReportTone.Bad : ReportTone.Good),
        };

        var tables = new List<ReportTable>();
        var problems = new List<string>();
        string caption;
        if (c.Role == TelegramReportRoles.Tp)
        {
            var me = all[0];
            var route = me.Markets.Where(m => m.OnRoute).ToList();
            tables.Add(List(c, c.T("Список: клиенты без закупа на сегодняшнем маршруте"), route, c.T("Сортировка по среднему закупу, кг/мес")));
            tables.Add(List(c, c.T("Все мои клиенты без закупа (крупнейшие)"), me.Markets.Take(ListLimit).ToList(),
                me.Markets.Count > ListLimit ? c.F("показаны {0} из {1}", ListLimit, me.Markets.Count) : null));
            caption = c.F("Сегодня на маршруте {0} клиент(ов) без закупа больше {1} дней (всего у вас: {2}). Зайдите к ним первыми и возьмите заказ.",
                me.SilentOnRoute, days, me.Silent);
            if (route.Count > 0)
            {
                problems.Add(c.F("Начните маршрут с точек: {0}", string.Join(", ", route.Take(5).Select(m => m.Name))));
            }
        }
        else
        {
            var rm = c.Role == TelegramReportRoles.Rm;
            // Третья колонка — «ТП, чел.» у РМ; у СВ её нет (доля базы без закупа — в своей колонке).
            var byUnit = new ReportTable(rm ? c.T("1. Клиенты без закупа по регионам") : c.T("1. Клиенты без закупа по ТП"),
                [new("№", false, 0.4f), new(ReportKit.UnitTitle(c), false, 2.6f), new(rm ? c.T("ТП, чел.") : c.T("Без закупа, %"), Weight: 0.8f),
                    new(c.T("База: закуп за 12 мес., точек")), new(c.F("Без закупа ≥{0} дн., точек", days)), new(c.T("% базы без закупа"), Weight: 0.8f),
                    new(c.T("Точек на маршруте сегодня")), new(c.T("Из них без закупа (в списке)")), new(c.T("Сумма последних закупов по списку, сум"), Weight: 1.3f)],
                units.Select((u, i) =>
                {
                    var s = Sum(u.Rows, u.Agents.Count);
                    var share = ReportFormat.Share(s.Silent, s.Base);
                    return (IReadOnlyList<ReportCell>)[$"{i + 1}", u.Name, rm ? Number(u.Agents.Count) : Percent(share), Number(s.Base), Number(s.Silent),
                        new(Percent(share), share is > 0.3m ? ReportTone.Bad : ReportTone.Normal), Number(s.OnRoute), new(Number(s.SilentOnRoute), s.SilentOnRoute > 0 ? ReportTone.Warn : ReportTone.Normal),
                        Number(s.LastSum)];
                }).ToList(),
                ["", new(ReportKit.TotalTitle(c), Bold: true), rm ? Number(total.Agents) : Percent(ReportFormat.Share(total.Silent, total.Base)), Number(total.Base),
                    new(Number(total.Silent), Bold: true), Percent(ReportFormat.Share(total.Silent, total.Base)), Number(total.OnRoute), new(Number(total.SilentOnRoute), Bold: true),
                    Number(total.LastSum)]);
            tables.Add(rm ? byUnit : ReportKit.WithoutColumn(byUnit, 2));
            if (!rm)
            {
                // СВ видит точки своего региона: крупнейшие молчащие (по среднему закупу, кг/мес — не по одному последнему заказу) — с ТП.
                var biggest = all.SelectMany(a => a.Markets).OrderByDescending(m => m.KgPerMonth).ThenByDescending(m => m.LastSum).Take(ListLimit).ToList();
                if (biggest.Count > 0)
                {
                    tables.Add(List(c, c.T("2. Крупнейшие клиенты без закупа в регионе"), biggest, null, withAgent: true));
                }
            }

            var worst = units.Select(u => (u.Name, S: Sum(u.Rows, u.Agents.Count))).Where(x => x.S.Base > 0).OrderByDescending(x => (decimal)x.S.Silent / x.S.Base).Take(5)
                .Where(x => x.S.Silent > 0).Select(x => $"{x.Name} {Percent(ReportFormat.Share(x.S.Silent, x.S.Base))}").ToList();
            if (worst.Count > 0)
            {
                problems.Add(c.F("Больше всего без закупа: {0}", string.Join(", ", worst)));
            }

            caption = $"{c.T(ReportCatalog.Title(c.Job.Report))} · {c.Scope.Title} · {Date(c.Date)}\n" +
                      c.F("Без закупа ≥{0} дн.: {1} из {2} точек ({3}); на маршруте сегодня — {4}.", days, total.Silent, total.Base,
                          Percent(ReportFormat.Share(total.Silent, total.Base)), total.SilentOnRoute);
        }

        if (total.Base == 0)
        {
            notes.Add(c.T("У ТП нет точек с подтверждённой покупкой за год, где он ответственный в Linko"));
        }

        notes.Add(c.F("База — точки, где ТП ответственный в Linko, с подтверждённой покупкой за год; без закупа — последняя доставленная покупка {0}+ дней назад", days));
        notes.Add(c.T("Покупка — доставленный заказ за вычетом возвратов; заказ, возвращённый целиком, покупкой не считается. Дата — дата приёмки в Linko"));
        if (ReportKit.VacancyNote(c) is { } vacancies)
        {
            notes.Add(vacancies);
        }
        var summary = c.F("Без закупа {0} из {1} точек, на маршруте {2}", total.Silent, total.Base, total.SilentOnRoute);
        return ReportKit.Document(c, ReportKit.Title(c), Date(c.Date), kpis, tables, problems, notes, caption, summary, incomplete: false);
    }

    private sealed record Totals(int Agents, int Base, int Silent, int OnRoute, int SilentOnRoute, decimal LastSum);

    private static Totals Sum(IReadOnlyList<NoPurchaseAgent> rows, int agents) =>
        new(agents, rows.Sum(r => r.Base), rows.Sum(r => r.Silent), rows.Sum(r => r.OnRoute), rows.Sum(r => r.SilentOnRoute), rows.Sum(r => r.SilentOnRouteLastSum));

    private static ReportTable List(ReportContext c, string title, IReadOnlyList<SilentMarket> markets, string? hint, bool withAgent = false)
    {
        var names = c.Scope.Agents.ToDictionary(a => a.Id, a => a.Name);
        var columns = new List<ReportColumn> { new("№", false, 0.4f), new(c.T("Точка"), false, 2.6f), new(c.T("Код ТТ"), Weight: 0.7f) };
        if (withAgent)
        {
            columns.Add(new(c.T("ТП"), false, 1.6f));
        }

        columns.AddRange([new(c.T("Категории (история)"), false, 2f), new(c.T("Ср. закуп, кг/мес"), Weight: 0.8f), new(c.T("Дата посл. закупа"), Weight: 0.9f),
            new(c.T("Посл. закуп, сум"), Weight: 1.1f), new(c.T("Дней без закупа"), Weight: 0.7f)]);
        var rows = markets.Select((m, i) =>
        {
            var row = new List<ReportCell> { $"{i + 1}", m.Name, $"{m.Id}" };
            if (withAgent)
            {
                row.Add(names.GetValueOrDefault(m.AgentId, "—"));
            }

            row.AddRange([string.Join(", ", m.Categories), Kg(m.KgPerMonth), Date(m.LastPurchase), Number(m.LastSum),
                new ReportCell(Number(m.Days), m.Days >= 60 ? ReportTone.Bad : ReportTone.Warn)]);
            return (IReadOnlyList<ReportCell>)row;
        }).ToList();
        if (rows.Count == 0)
        {
            var empty = new List<ReportCell> { "", c.T("Таких точек нет"), "" };
            empty.AddRange(Enumerable.Repeat(new ReportCell(""), columns.Count - 3));
            rows.Add(empty);
        }

        return new ReportTable(title, columns, rows, null, hint);
    }
}

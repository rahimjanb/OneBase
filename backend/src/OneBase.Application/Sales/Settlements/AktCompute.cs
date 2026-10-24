using System.Globalization;

namespace OneBase.Application.Sales.Settlements;

public sealed record AktMonthOption(string Key, int Year, int Month, bool Partial);

public sealed record AktArt(string Key, string Label);

/// <summary>Строка акта: остаток на начало, отгрузка, возврат, касса, банк, расходы по колонкам, итог расходов, остаток на конец.</summary>
public sealed record AktRow(
    string Key,
    string Name,
    decimal Open,
    decimal Ship,
    decimal Return,
    decimal Cash,
    decimal Bank,
    IReadOnlyDictionary<string, decimal> Expenses,
    decimal Total,
    decimal Close,
    bool Anchored,
    decimal? CashbackPending);

public sealed record AktGroup(string Key, string Name, IReadOnlyList<AktRow> Rows, AktRow Totals);

public sealed record AktLine(DateOnly Date, string Kind, decimal Amount, string? Note, string? Code = null);

/// <summary>Карточка контрагента за месяц: отгрузки и возвраты Linko, оплаты, расходы реестра и рассчитанный кешбэк.</summary>
public sealed record AktCard(string Key, string Name, int? Norm, IReadOnlyList<AktLine> Docs, IReadOnlyList<AktLine> Payments, IReadOnlyList<AktLine> Expenses,
    IReadOnlyList<AktLine> Cashback);

/// <summary>Проверка акта: вид (страница собирает текст по нему), текст для API, контрагенты; Date — последняя строка реестра расходов (expenses).</summary>
public sealed record AktCheck(string Kind, string Text, IReadOnlyList<string> Items, DateOnly? Date = null);

public sealed record AktView(
    DateOnly AsOf,
    IReadOnlyList<AktMonthOption> Months,
    AktMonthOption Month,
    IReadOnlyList<AktArt> Arts,
    IReadOnlyList<AktGroup> Groups,
    IReadOnlyList<AktCard> Cards,
    IReadOnlyList<AktCheck> Checks,
    DateOnly? AnchorDate,
    DateOnly? PaymentsTo,
    DateOnly? ExpensesTo,
    bool HasPayments,
    bool HasExpenses,
    bool HasBalances);

/// <summary>
/// «Акт сверки» по месяцам — как вкладка «Акт сверки» «Полевого контроля»: остаток на конец = начало + отгрузка − возврат − касса − банк −
/// расходы завода на контрагента. Начало месяца с опорным остатком — остаток из акта бухгалтерии; дальше по цепочке: начало = конец прошлого
/// месяца (и назад от опоры). Расходы — по кодам статей реестра: 601 — ретробонус (за вычетом кешбэка), 602 — ЗП, 603/604 — прочие, 606 —
/// логистика, 609 — акции; кешбэк 2% — по FIFO (отгрузка закрыта в месяце не позже нормы).
/// </summary>
public static class AktCompute
{
    public static readonly IReadOnlyList<AktArt> Arts =
    [
        new("cashback", "2% кешбэк"), new("retro", "Ретробонус"), new("promo", "Скидки и акции"), new("logi", "Логистика"), new("salary", "ЗП супервайзеров"),
        new("other", "Прочие расходы"),
    ];

    private static readonly (string Key, string Name)[] Groups = [("dealer", "Дилеры"), ("direct", "Прямые клиенты и сети"), ("other", "Прочие контрагенты")];

    public static AktView Build(SettlementsBase data, string? monthKey)
    {
        var first = data.PaymentsFrom is { } pf ? new DateOnly(pf.Year, pf.Month, 1) : new DateOnly(data.AsOf.Year, data.AsOf.Month, 1).AddMonths(-2);
        var last = new DateOnly(data.AsOf.Year, data.AsOf.Month, 1);
        var months = new List<AktMonthOption>();
        for (var m = first; m <= last; m = m.AddMonths(1))
        {
            // Месяц неполный: данные не за все дни — или сегодня его последний день (отчётный день «сегодня»: день ещё идёт).
            var partial = data.AsOf.Day < DateTime.DaysInMonth(m.Year, m.Month) || (data.Today is { } today && today < m.AddMonths(1));
            months.Add(new AktMonthOption($"{m.Year}-{m.Month}", m.Year, m.Month, m == last && partial));
        }

        var month = months.FirstOrDefault(m => m.Key == monthKey) ?? months[^1];
        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1);

        // Есть акт бухгалтерии — строки акта: его контрагенты в его группах, все (пустая строка — тоже информация); прочие контрагенты Linko
        // и реестров в акт не входят, их движение — в проверке «outside». Без акта — все контрагенты с остатком или движением.
        var byAct = data.ActGroups is { Count: > 0 } && data.Books.Any(b => b.ActGroup is not null);
        var rows = new List<(DealerBook Book, AktRow Row)>();
        var outside = new List<(DealerBook Book, decimal Moved)>();
        var cards = new List<AktCard>();
        foreach (var book in data.Books)
        {
            if (byAct && book.ActGroup is null)
            {
                var moved = book.Docs.Where(d => d.Date >= start && d.Date < end).Sum(d => d.Amount) + book.Payments.Where(p => p.Date >= start && p.Date < end).Sum(p => p.Amount);
                if (moved != 0)
                {
                    outside.Add((book, moved));
                }

                continue;
            }

            var fifo = SettlementMath.Fifo(book, first.AddMonths(-1) < start.AddMonths(-6) ? start.AddMonths(-6) : first.AddMonths(-1), data.AsOf);
            var row = RowOf(book, fifo, start, data.AsOf);
            if (!byAct && row.Open == 0 && row.Ship == 0 && row.Return == 0 && row.Cash == 0 && row.Bank == 0 && row.Total == 0)
            {
                continue;
            }

            rows.Add((book, row));
            var cashback = SettlementMath.Cashback(fifo, start.Year, start.Month, book.Norm)
                .Select(c => new AktLine(c.Item.Closed!.Value, "cashback", c.Amount,
                    $"Отгрузка {c.Item.Date:dd.MM.yyyy}, сумма {c.Item.Amount.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))}, закрыта {c.Item.Closed:dd.MM.yyyy} (DSO {c.Dso}), норма {book.Norm ?? SettlementMath.DefaultNorm}"))
                .ToList();
            cards.Add(new AktCard(book.Key, book.Name, book.Norm,
                book.Docs.Where(d => d.Date >= start && d.Date < end).OrderBy(d => d.Date).Select(d => new AktLine(d.Date, d.Return ? "return" : "ship", d.Amount, d.Doc)).ToList(),
                book.Payments.Where(p => p.Date >= start && p.Date < end).OrderBy(p => p.Date).Select(p => new AktLine(p.Date, p.Cash ? "cash" : "bank", p.Amount, p.Note)).ToList(),
                book.Expenses.Where(e => e.Date >= start && e.Date < end).OrderBy(e => e.Date).Select(e => new AktLine(e.Date, e.Art, e.Amount, e.Note, e.Code)).ToList(),
                cashback));
        }

        var groups = (byAct ? data.ActGroups!.Select(g => (Key: g, Name: ActGroupName(g))).ToArray() : Groups)
            .Select(g =>
            {
                var list = rows.Where(r => (byAct ? r.Book.ActGroup : r.Book.Group) == g.Key).Select(r => r.Row).OrderByDescending(r => r.Close).ToList();
                return new AktGroup(g.Key, g.Name, list, Sum(g.Name, list));
            })
            .Where(g => g.Rows.Count > 0)
            .ToList();

        var checks = new List<AktCheck>();
        if (data.ExpensesTo is { } exp && exp < end.AddDays(-1) && rows.Any(r => r.Row.Ship != 0 || r.Row.Cash != 0 || r.Row.Bank != 0))
        {
            checks.Add(new AktCheck("expenses", exp < start
                ? $"Расходы за {MonthName(start)} в реестр ещё не внесены (последняя строка — {exp:dd.MM.yyyy}): расходы месяца — только кешбэк по FIFO."
                : $"Реестр расходов внесён по {exp:dd.MM.yyyy}: расходы после этой даты в акт ещё не попали.", [], exp));
        }

        var unanchored = rows.Where(r => r.Book.AnchorDate is null && (r.Row.Ship != 0 || r.Row.Cash != 0 || r.Row.Bank != 0)).Select(r => r.Book.Name).ToList();
        if (unanchored.Count > 0)
        {
            checks.Add(new AktCheck("anchor",
                $"Без остатка из акта бухгалтерии — остаток считается от начала реестров (может отличаться): {unanchored.Count}.", unanchored));
        }

        var noSheet = rows.Where(r => r.Book.Group != "other" && r.Book.Payments.Count == 0 && r.Book.Expenses.Count == 0 && r.Book.AnchorDate is null && r.Row.Ship != 0)
            .Select(r => r.Book.Name).ToList();
        if (noSheet.Count > 0)
        {
            checks.Add(new AktCheck("unmatched", $"Отгрузки Linko, а в таблицах контрагента с таким именем нет (оплаты и остаток не найдены): {noSheet.Count}.", noSheet));
        }

        var noLinko = rows.Where(r => r.Book.Group == "other" && r.Book.Docs.Count == 0 && (r.Row.Cash != 0 || r.Row.Bank != 0)).Select(r => r.Book.Name).ToList();
        if (noLinko.Count > 0)
        {
            checks.Add(new AktCheck("nolinko", $"Оплаты есть, а отгрузок Linko под этим именем нет (контрагент таблиц не сопоставлен с дилером региона или клиентом): {noLinko.Count}.", noLinko));
        }

        if (outside.Count > 0)
        {
            var ru = CultureInfo.GetCultureInfo("ru-RU");
            checks.Add(new AktCheck("outside",
                $"Отгрузки или оплаты месяца у контрагентов, которых нет в акте бухгалтерии (в акт не входят): {outside.Count}.",
                outside.OrderByDescending(x => x.Moved).Select(x => $"{x.Book.Name} — {x.Moved.ToString("N0", ru)}").ToList()));
        }

        // Кешбэк сверх 601 — сигнал, только когда расходы месяца внесены до конца: пока 601 не провели, «сверх» — у всех.
        var pending = rows.Where(r => r.Row.CashbackPending is > 1).Select(r => $"{r.Book.Name} — {r.Row.CashbackPending!.Value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))}").ToList();
        if (pending.Count > 0 && data.ExpensesTo >= end.AddDays(-1))
        {
            checks.Add(new AktCheck("cashback", $"Кешбэк по FIFO больше, чем проведено по статье 601 (разница добавлена в расходы акта): {pending.Count}.", pending));
        }

        var anchorDate = data.Books.Where(b => b.AnchorSource == "act").Select(b => b.AnchorDate).Max();
        return new AktView(data.AsOf, months, month, Arts, groups, cards, checks, anchorDate, data.PaymentsTo, data.ExpensesTo, data.HasPayments, data.HasExpenses,
            data.HasBalances);
    }

    /// <summary>Движение месяца в колонках акта; кешбэк — по FIFO, ретробонус — 601 за вычетом кешбэка (не меньше нуля).</summary>
    private static (decimal Ship, decimal Return, decimal Cash, decimal Bank, Dictionary<string, decimal> Expenses, decimal Total, decimal Pending) Movement(
        DealerBook book, IReadOnlyList<FifoItem> fifo, DateOnly start)
    {
        var end = start.AddMonths(1);
        var docs = book.Docs.Where(d => d.Date >= start && d.Date < end).ToList();
        var pays = book.Payments.Where(p => p.Date >= start && p.Date < end).ToList();
        var exps = book.Expenses.Where(e => e.Date >= start && e.Date < end).ToList();
        var cashback = SettlementMath.Cashback(fifo, start.Year, start.Month, book.Norm).Sum(c => c.Amount);
        var retro601 = exps.Where(e => e.Code == "601").Sum(e => e.Amount);
        var expenses = new Dictionary<string, decimal>
        {
            ["cashback"] = cashback,
            ["retro"] = Math.Max(0, retro601 - cashback),
            ["promo"] = exps.Where(e => e.Art == "promo").Sum(e => e.Amount),
            ["logi"] = exps.Where(e => e.Art == "logi").Sum(e => e.Amount),
            ["salary"] = exps.Where(e => e.Art == "salary").Sum(e => e.Amount),
            ["other"] = exps.Where(e => e.Art == "other").Sum(e => e.Amount),
        };
        return (docs.Where(d => !d.Return).Sum(d => d.Amount), docs.Where(d => d.Return).Sum(d => d.Amount), pays.Where(p => p.Cash).Sum(p => p.Amount),
            pays.Where(p => !p.Cash).Sum(p => p.Amount), expenses, expenses.Values.Sum(), Math.Max(0, cashback - retro601));
    }

    /// <summary>
    /// Строка месяца: начало — остаток из опоры, перенесённый цепочкой акта (вперёд: начало = конец прошлого месяца; назад: начало = начало
    /// следующего − движение месяца); без опоры — долг по реестрам.
    /// </summary>
    private static AktRow RowOf(DealerBook book, IReadOnlyList<FifoItem> fifo, DateOnly start, DateOnly asOf)
    {
        decimal open;
        if (book.AnchorDate is { } anchorDate && book.AnchorAmount is { } anchor)
        {
            var anchorMonth = new DateOnly(anchorDate.Year, anchorDate.Month, 1);
            // Остаток не на 1-е число — к началу месяца назад по движению реестров.
            open = anchorDate == anchorMonth ? anchor : anchor - SettlementMath.Delta(book, anchorMonth, anchorDate);
            if (start > anchorMonth)
            {
                for (var m = anchorMonth; m < start; m = m.AddMonths(1))
                {
                    var mv = Movement(book, fifo, m);
                    open += mv.Ship - mv.Return - mv.Cash - mv.Bank - mv.Total;
                }
            }
            else if (start < anchorMonth)
            {
                for (var m = start; m < anchorMonth; m = m.AddMonths(1))
                {
                    var mv = Movement(book, fifo, m);
                    open -= mv.Ship - mv.Return - mv.Cash - mv.Bank - mv.Total;
                }
            }
        }
        else
        {
            open = SettlementMath.BalanceAt(book, start);
        }

        var (ship, ret, cash, bank, expenses, total, pending) = Movement(book, fifo, start);
        return new AktRow(book.Key, book.Name, open, ship, ret, cash, bank, expenses, total, open + ship - ret - cash - bank - total, book.AnchorDate is not null,
            pending > 0 ? pending : null);
    }

    private static AktRow Sum(string name, IReadOnlyList<AktRow> rows) =>
        new("total", name, rows.Sum(r => r.Open), rows.Sum(r => r.Ship), rows.Sum(r => r.Return), rows.Sum(r => r.Cash), rows.Sum(r => r.Bank),
            Arts.ToDictionary(a => a.Key, a => rows.Sum(r => r.Expenses.GetValueOrDefault(a.Key))), rows.Sum(r => r.Total), rows.Sum(r => r.Close), true,
            rows.Sum(r => r.CashbackPending ?? 0) is > 0 and var p ? p : null);

    /// <summary>Заголовок группы акта: «Диллер» → «Дилеры», без заголовка — «Контрагенты», остальные как в листе.</summary>
    private static string ActGroupName(string group) => group.Trim().ToLowerInvariant() switch
    {
        "" => "Контрагенты",
        "диллер" or "дилер" or "дилеры" => "Дилеры",
        _ => group.Trim(),
    };

    private static readonly string[] Names =["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    private static string MonthName(DateOnly d) => $"{Names[d.Month - 1]} {d.Year}";
}

using System.Text.RegularExpressions;

namespace OneBase.Application.Sales.Settlements;

/// <summary>Документ дилера: отгрузка (Amount &gt; 0) или возврат (Amount &gt; 0, Return = true), сумма Linko.</summary>
public sealed record SettlementDoc(DateOnly Date, decimal Amount, string Doc, bool Return = false);

/// <summary>Оплата дилера из «Kassa Bank»: касса или банк; отрицательная — возврат денег дилеру.</summary>
public sealed record SettlementPayment(DateOnly Date, decimal Amount, bool Cash, string? Note);

/// <summary>Расход завода на дилера из «Прочие расходы»: статья реестра («601-Дилер бонуслари»), её код и колонка акта.</summary>
public sealed record SettlementExpense(DateOnly Date, decimal Amount, string Code, string Art, string? Note);

/// <summary>
/// Контрагент взаиморасчётов: дилер (склад региона), прямой клиент завода или контрагент из таблиц без отгрузок Linko. Group — «dealer»,
/// «direct», «other». Anchor — опорный остаток (долг на дату) из акта сверки бухгалтерии или «Ostatka». ActGroup — группа строки в акте
/// бухгалтерии («Диллер», «Ритейл», «Долги»); null — контрагента в акте нет.
/// </summary>
public sealed record DealerBook(
    string Key,
    string Name,
    string Group,
    int? Norm,
    bool InTerms,
    DateOnly? AnchorDate,
    decimal? AnchorAmount,
    string? AnchorSource,
    IReadOnlyList<SettlementDoc> Docs,
    IReadOnlyList<SettlementPayment> Payments,
    IReadOnlyList<SettlementExpense> Expenses,
    string? ActGroup = null);

/// <summary>Отгрузка в FIFO: дата, сумма, остаток долга по ней; закрыта — дата погашения.</summary>
public sealed class FifoItem(DateOnly date, decimal amount, string doc)
{
    public DateOnly Date { get; } = date;
    public decimal Amount { get; } = amount;
    public string Doc { get; } = doc;
    public decimal Remaining { get; set; } = amount;
    public DateOnly? Closed { get; set; }
}

/// <summary>
/// Взаиморасчёты дилера — чистые функции. Остаток на дату — от опорного остатка вперёд и назад по движению: отгрузка увеличивает долг,
/// возврат, оплата и расход завода на дилера уменьшают. FIFO — как скрипт таблицы DSO: оплаты, возвраты и «Прочие расходы» гасят самые старые
/// неоплаченные отгрузки; долг на опорную дату раскладывается на последние отгрузки до неё.
/// </summary>
public static partial class SettlementMath
{
    /// <summary>Кешбэк за оплату в срок: 2% отгрузки, закрытой FIFO не позже нормы (как «DSO 2%» таблицы DSO).</summary>
    public const decimal CashbackRate = 0.02m;

    /// <summary>Окно фактического DSO: отгрузки, закрытые за последние 90 дней.</summary>
    public const int FactDays = 90;

    /// <summary>Норма без условий в «Ostatka».</summary>
    public const int DefaultNorm = 20;

    [GeneratedRegex(@"^\s*(\d{3})")]
    private static partial Regex CodePrefix();

    [GeneratedRegex(@"нақд|наќд|нaқд|naqd|касс|cash|нал", RegexOptions.IgnoreCase)]
    private static partial Regex CashWallet();

    /// <summary>Наличные ли: кошелёк «Нақд» (касса), остальное — банк.</summary>
    public static bool IsCash(string? wallet) => wallet is not null && CashWallet().IsMatch(wallet);

    /// <summary>Код статьи из «601-Дилер бонуслари» → «601»; без кода — сама статья.</summary>
    public static string CodeOf(string? article) => article is not null && CodePrefix().Match(article) is { Success: true } m ? m.Groups[1].Value : (article ?? "").Trim();

    /// <summary>Колонка акта по коду статьи: 601 — ретробонус, 602 — ЗП, 603/604 — прочие, 606 — логистика, 609 — акции; остальное — прочие.</summary>
    public static string ArtOf(string code) => code switch
    {
        "601" => "retro",
        "602" => "salary",
        "606" => "logi",
        "609" => "promo",
        _ => "other",
    };

    /// <summary>Движение долга за дни [from; to): отгрузки − возвраты − оплаты − расходы.</summary>
    public static decimal Delta(DealerBook book, DateOnly from, DateOnly to) =>
        book.Docs.Where(d => d.Date >= from && d.Date < to).Sum(d => d.Return ? -d.Amount : d.Amount)
        - book.Payments.Where(p => p.Date >= from && p.Date < to).Sum(p => p.Amount)
        - book.Expenses.Where(e => e.Date >= from && e.Date < to).Sum(e => e.Amount);

    /// <summary>Долг на начало дня date: от опорного остатка вперёд или назад; без опоры — движение с начала реестров.</summary>
    public static decimal BalanceAt(DealerBook book, DateOnly date)
    {
        if (book.AnchorDate is { } anchor && book.AnchorAmount is { } amount)
        {
            return date >= anchor ? amount + Delta(book, anchor, date) : amount - Delta(book, date, anchor);
        }

        return Delta(book, DateOnly.MinValue, date);
    }

    /// <summary>
    /// FIFO с даты start: долг на начало start раскладывается на последние отгрузки до неё (неоплаченные целиком, самая ранняя — частично),
    /// дальше по дням: отгрузки дня встают в очередь, кредиты дня (возвраты, оплаты, расходы) гасят самые старые. Переплата — аванс, гасит
    /// следующие отгрузки в день отгрузки. Отрицательная оплата (деньги вернули дилеру) — долг без отгрузки.
    /// </summary>
    public static IReadOnlyList<FifoItem> Fifo(DealerBook book, DateOnly start, DateOnly asOf)
    {
        var items = new List<FifoItem>();
        var opening = BalanceAt(book, start);
        var advance = 0m;
        if (opening > 0)
        {
            var left = opening;
            foreach (var doc in book.Docs.Where(d => !d.Return && d.Date < start).OrderByDescending(d => d.Date).ThenByDescending(d => d.Amount))
            {
                if (left <= 0)
                {
                    break;
                }

                var part = Math.Min(left, doc.Amount);
                items.Insert(0, new FifoItem(doc.Date, doc.Amount, doc.Doc) { Remaining = part });
                left -= part;
            }

            if (left > 0)
            {
                items.Insert(0, new FifoItem(start.AddDays(-1), left, "остаток на начало") { Remaining = left });
            }
        }
        else
        {
            advance = -opening;
        }

        var debits = book.Docs.Where(d => !d.Return && d.Date >= start && d.Date <= asOf).Select(d => (d.Date, d.Amount, d.Doc))
            .Concat(book.Payments.Where(p => p.Amount < 0 && p.Date >= start && p.Date <= asOf).Select(p => (p.Date, -p.Amount, Doc: "возврат оплаты")))
            .ToLookup(x => x.Date);
        var credits = book.Docs.Where(d => d.Return && d.Date >= start && d.Date <= asOf).Select(d => (d.Date, d.Amount))
            .Concat(book.Payments.Where(p => p.Amount > 0 && p.Date >= start && p.Date <= asOf).Select(p => (p.Date, p.Amount)))
            .Concat(book.Expenses.Where(e => e.Amount != 0 && e.Date >= start && e.Date <= asOf).Select(e => (e.Date, e.Amount)))
            .ToLookup(x => x.Date);

        for (var day = start; day <= asOf; day = day.AddDays(1))
        {
            foreach (var (date, amount, doc) in debits[day].OrderBy(x => x.Doc, StringComparer.Ordinal))
            {
                var item = new FifoItem(date, amount, doc);
                if (advance > 0)
                {
                    var used = Math.Min(advance, item.Remaining);
                    item.Remaining -= used;
                    advance -= used;
                    if (item.Remaining <= 0.005m)
                    {
                        item.Remaining = 0;
                        item.Closed = day;
                    }
                }

                items.Add(item);
            }

            var credit = credits[day].Sum(x => x.Amount);
            if (credit < 0)
            {
                // Сторно расхода — долг без отгрузки.
                items.Add(new FifoItem(day, -credit, "сторно"));
                credit = 0;
            }

            foreach (var item in items.Where(i => i.Closed is null))
            {
                if (credit <= 0)
                {
                    break;
                }

                var used = Math.Min(credit, item.Remaining);
                item.Remaining -= used;
                credit -= used;
                if (item.Remaining <= 0.005m)
                {
                    item.Remaining = 0;
                    item.Closed = day;
                }
            }

            advance += credit;
        }

        return items;
    }

    /// <summary>
    /// Фактический DSO, дней (1 знак): средневзвешенное по сумме (дата погашения − дата отгрузки) отгрузок, закрытых за FactDays дней до asOf;
    /// доля закрытых позже нормы. Нет закрытий — null.
    /// </summary>
    public static (decimal? Fact, decimal? LateShare) Fact(IReadOnlyList<FifoItem> items, DateOnly asOf, int? norm)
    {
        var closed = items.Where(i => i.Closed is { } c && c > asOf.AddDays(-FactDays) && c <= asOf && i.Doc != "остаток на начало").ToList();
        var total = closed.Sum(i => i.Amount);
        if (total <= 0)
        {
            return (null, null);
        }

        var fact = Math.Round(closed.Sum(i => i.Amount * (i.Closed!.Value.DayNumber - i.Date.DayNumber)) / total, 1, MidpointRounding.AwayFromZero);
        decimal? late = norm is { } n ? Math.Round(closed.Where(i => i.Closed!.Value.DayNumber - i.Date.DayNumber > n).Sum(i => i.Amount) / total, 3) : null;
        return (fact, late);
    }

    /// <summary>Кешбэк месяца: 2% отгрузок, закрытых FIFO в месяце не позже нормы (норма не задана — 20 дней), до сума.</summary>
    public static IReadOnlyList<(FifoItem Item, int Dso, decimal Amount)> Cashback(IReadOnlyList<FifoItem> items, int year, int month, int? norm)
    {
        var limit = norm ?? DefaultNorm;
        return items
            .Where(i => i.Closed is { } c && c.Year == year && c.Month == month && i.Doc is not ("остаток на начало" or "возврат оплаты" or "сторно"))
            .Select(i => (Item: i, Dso: i.Closed!.Value.DayNumber - i.Date.DayNumber))
            .Where(x => x.Dso <= limit)
            .Select(x => (x.Item, x.Dso, Math.Round(x.Item.Amount * CashbackRate, 0, MidpointRounding.AwayFromZero)))
            .ToList();
    }
}

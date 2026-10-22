namespace OneBase.Application.Sales.Settlements;

/// <summary>Неоплаченная отгрузка: дата, сумма, остаток долга, дней с отгрузки, срок по норме, ожидаемая оплата (по фактическому DSO).</summary>
public sealed record DsoOpen(DateOnly Date, decimal Amount, decimal Remaining, int Age, DateOnly? Due, DateOnly Expected, int Late, int LateExpected, string Doc);

public sealed record DsoDealer(
    string Key,
    string Name,
    string Status,
    int? Norm,
    bool DefaultNorm,
    decimal? Fact,
    decimal? LateShare,
    decimal Debt,
    decimal Overdue,
    decimal PaidMonth,
    decimal ShippedMonth,
    decimal Paid8Weeks,
    DateOnly? LastPay,
    int? DaysSincePay,
    decimal CashShare,
    IReadOnlyList<DsoOpen> Open,
    IReadOnlyList<DsoWeek>? Weeks = null);

public sealed record DsoWeek(string Key, DateOnly? From, DateOnly? To, decimal ByNorm, decimal Expected, decimal Cash, decimal Bank);

public sealed record DsoPayMonth(string Key, int Year, int Month, decimal Cash, decimal Bank);

public sealed record DsoPayDealer(string Name, string Group, IReadOnlyList<DsoPayMonth> Months);

public sealed record DsoChannel(string Key, string Name, IReadOnlyList<DsoChannelMonth> Months);

/// <summary>Отгрузки и поступления канала за месяц по дням (для выбора «по … число»).</summary>
public sealed record DsoChannelMonth(string Key, int Year, int Month, IReadOnlyList<decimal> Shipped, IReadOnlyList<decimal> Paid);

public sealed record DsoView(
    DateOnly AsOf,
    DateOnly MonthFrom,
    int FactDays,
    int DefaultNorm,
    decimal Debt,
    int OpenCount,
    int DebtDealers,
    decimal Overdue,
    decimal OverdueShare,
    int OverdueDealers,
    decimal DueWeek,
    decimal ExpectedWeek,
    decimal PaidMonth,
    decimal ShippedMonth,
    IReadOnlyList<DsoDealer> Dealers,
    IReadOnlyList<DsoWeek> Weeks,
    IReadOnlyList<DsoPayMonth> PayMonths,
    IReadOnlyList<DsoPayDealer> PayDealers,
    IReadOnlyList<DsoChannel> Channels,
    bool HasPayments,
    bool HasTerms,
    DateOnly? PaymentsTo);

/// <summary>
/// DSO — долги дилеров и прогноз оплат, как вкладка «DSO» «Полевого контроля»: неоплаченные отгрузки по FIFO (оплаты, возвраты и «Прочие
/// расходы» гасят самые старые), срок по норме = отгрузка + норма («Ostatka»; не задана — 20 дней, договор без нормы — без срока), ожидаемая
/// оплата = отгрузка + фактический DSO дилера за 90 дней. Дни — от даты данных, а не от сегодня.
/// </summary>
public static class DsoCompute
{
    /// <summary>Без отгрузок дольше этого — дилер «не работает с нами»: долг к взысканию, в прогноз не входит.</summary>
    public const int GoneDays = 60;

    public static DsoView Build(SettlementsBase data)
    {
        var t = data.AsOf;
        var monthFrom = new DateOnly(t.Year, t.Month, 1);
        var cashFrom = monthFrom.AddMonths(-2);

        // Доля наличных по всем плательщикам за 3 месяца — у дилера без оплат.
        var allRecent = data.Books.SelectMany(b => b.Payments).Where(p => p.Date >= cashFrom && p.Date <= t).ToList();
        var overallCash = Share(allRecent.Where(p => p.Cash).Sum(p => p.Amount), allRecent.Sum(p => p.Amount));

        var dealers = new List<DsoDealer>();
        // Дилеры DSO — контрагенты с условиями оплаты («Ostatka»): как лист DSO таблицы; без условий (сети, разовые клиенты) — только в «Акте сверки».
        // Нет условий ни у кого (лист не привязан) — дилеры складов регионов.
        var withTerms = data.Books.Any(b => b.InTerms);
        foreach (var book in data.Books.Where(b => withTerms ? b.InTerms : b.Group == "dealer"))
        {
            var fifo = SettlementMath.Fifo(book, t.AddDays(-240), t);
            var open = fifo.Where(i => i.Remaining > 0.5m).ToList();
            var lastShip = book.Docs.Where(d => !d.Return).Select(d => (DateOnly?)d.Date).Max();
            var debt = open.Sum(i => i.Remaining);
            if (debt <= 0.5m && book.Payments.All(p => p.Date < monthFrom))
            {
                continue; // долга нет и в месяце не платил — в DSO не показываем
            }

            var noTerm = book.InTerms && book.Norm is null;
            var gone = lastShip is null || lastShip < t.AddDays(-GoneDays);
            var status = gone && debt > 0.5m ? "gone" : noTerm ? "noterm" : "active";
            var norm = status == "active" ? book.Norm ?? SettlementMath.DefaultNorm : (int?)null;
            var (fact, late) = SettlementMath.Fact(fifo, t, norm);
            var recent = book.Payments.Where(p => p.Date >= cashFrom && p.Date <= t).ToList();
            var cashShare = recent.Count > 0 && recent.Sum(p => p.Amount) > 0 ? Share(recent.Where(p => p.Cash).Sum(p => p.Amount), recent.Sum(p => p.Amount)) : overallCash;
            var items = open.Select(i =>
                {
                    DateOnly? due = norm is { } n ? i.Date.AddDays(n) : null;
                    var expected = i.Date.AddDays((int)Math.Round(fact ?? norm ?? book.Norm ?? SettlementMath.DefaultNorm, MidpointRounding.AwayFromZero));
                    return new DsoOpen(i.Date, i.Amount, i.Remaining, t.DayNumber - i.Date.DayNumber, due, expected, due is { } d ? t.DayNumber - d.DayNumber : 0,
                        t.DayNumber - expected.DayNumber, i.Doc);
                })
                .OrderBy(i => i.Date)
                .ToList();
            var lastPay = book.Payments.Where(p => p.Amount > 0).Select(p => (DateOnly?)p.Date).Max();
            dealers.Add(new DsoDealer(book.Key, book.Name, status, norm, status == "active" && book.Norm is null, fact, late, debt,
                status == "active" ? items.Where(i => i.Late > 0).Sum(i => i.Remaining) : 0,
                book.Payments.Where(p => p.Date >= monthFrom && p.Date <= t).Sum(p => p.Amount),
                book.Docs.Where(d => !d.Return && d.Date >= monthFrom && d.Date <= t).Sum(d => d.Amount),
                book.Payments.Where(p => p.Date > t.AddDays(-56) && p.Date <= t).Sum(p => p.Amount),
                lastPay, lastPay is { } lp ? t.DayNumber - lp.DayNumber : null, cashShare, items));
        }

        var order = new Dictionary<string, int> { ["active"] = 0, ["noterm"] = 1, ["gone"] = 2 };
        // Прогноз дилера (раскрытая строка) — те же недели по его отгрузкам; ушедшие в прогноз не входят.
        dealers = dealers.OrderBy(d => order[d.Status]).ThenByDescending(d => d.Debt).Select(d => d.Status == "gone" ? d : d with { Weeks = Weeks(t, [d]) }).ToList();
        var active = dealers.Where(d => d.Status == "active").ToList();
        var forecast = dealers.Where(d => d.Status != "gone").ToList();
        var overdue = active.Sum(d => d.Overdue);
        var activeDebt = active.Sum(d => d.Debt);

        return new DsoView(
            t,
            monthFrom,
            SettlementMath.FactDays,
            SettlementMath.DefaultNorm,
            dealers.Sum(d => d.Debt),
            dealers.Sum(d => d.Open.Count),
            dealers.Count(d => d.Debt > 0.5m),
            overdue,
            activeDebt > 0 ? overdue / activeDebt : 0,
            active.Count(d => d.Overdue > 0),
            active.SelectMany(d => d.Open).Where(i => i.Due is { } due && due >= t && due < t.AddDays(7)).Sum(i => i.Remaining),
            forecast.SelectMany(d => d.Open).Where(i => i.Expected >= t && i.Expected < t.AddDays(7)).Sum(i => i.Remaining),
            dealers.Sum(d => d.PaidMonth),
            dealers.Sum(d => d.ShippedMonth),
            dealers,
            Weeks(t, forecast),
            PayMonths(data.Books.SelectMany(b => b.Payments)),
            data.Books.Where(b => b.Payments.Count > 0)
                .Select(b => new DsoPayDealer(b.Name, b.Group, PayMonths(b.Payments)))
                .ToList(),
            Channels(data),
            data.HasPayments,
            data.HasTerms,
            data.PaymentsTo);
    }

    /// <summary>
    /// Прогноз по неделям: «срок уже прошёл», шесть недель от даты данных и «позже». По норме — по сроку (только дилеры со сроком), по факту —
    /// по ожидаемой дате (и дилеры без срока), наличные и перечисление — по доле наличных в оплатах дилера за 3 месяца.
    /// </summary>
    public static IReadOnlyList<DsoWeek> Weeks(DateOnly t, IReadOnlyList<DsoDealer> dealers)
    {
        var buckets = new List<(string Key, DateOnly? From, DateOnly? To)> { ("late", null, t) };
        for (var i = 0; i < 6; i++)
        {
            buckets.Add(($"w{i}", t.AddDays(7 * i), t.AddDays(7 * i + 7)));
        }

        buckets.Add(("later", t.AddDays(42), null));
        int Index(DateOnly d) => d < t ? 0 : d >= t.AddDays(42) ? 7 : 1 + (d.DayNumber - t.DayNumber) / 7;
        var norm = new decimal[8];
        var fact = new decimal[8];
        var cash = new decimal[8];
        foreach (var dealer in dealers)
        {
            foreach (var item in dealer.Open)
            {
                if (item.Due is { } due && dealer.Status == "active")
                {
                    norm[Index(due)] += item.Remaining;
                }

                fact[Index(item.Expected)] += item.Remaining;
                cash[Index(item.Expected)] += item.Remaining * dealer.CashShare;
            }
        }

        return buckets.Select((b, i) => new DsoWeek(b.Key, b.From, b.To is { } to && b.Key != "late" ? to.AddDays(-1) : b.To, norm[i], fact[i], cash[i], fact[i] - cash[i])).ToList();
    }

    private static IReadOnlyList<DsoPayMonth> PayMonths(IEnumerable<SettlementPayment> payments) =>
        payments.GroupBy(p => (p.Date.Year, p.Date.Month))
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g => new DsoPayMonth($"{g.Key.Year}-{g.Key.Month}", g.Key.Year, g.Key.Month, g.Where(p => p.Cash).Sum(p => p.Amount), g.Where(p => !p.Cash).Sum(p => p.Amount)))
            .ToList();

    /// <summary>Отгрузки (Linko) и поступления (касса и банк) по группам контрагентов за последние 6 месяцев — по дням месяца.</summary>
    private static IReadOnlyList<DsoChannel> Channels(SettlementsBase data)
    {
        var groups = new[] { ("dealer", "Дилеры"), ("direct", "Прямые клиенты и сети"), ("other", "Прочие контрагенты") };
        var last = new DateOnly(data.AsOf.Year, data.AsOf.Month, 1);
        return groups
            .Select(g =>
            {
                var books = data.Books.Where(b => b.Group == g.Item1).ToList();
                var months = Enumerable.Range(0, 6).Select(i => last.AddMonths(-i))
                    .Select(m =>
                    {
                        var days = DateTime.DaysInMonth(m.Year, m.Month);
                        var shipped = new decimal[days];
                        var paid = new decimal[days];
                        foreach (var d in books.SelectMany(b => b.Docs).Where(d => d.Date.Year == m.Year && d.Date.Month == m.Month))
                        {
                            shipped[d.Date.Day - 1] += d.Return ? -d.Amount : d.Amount;
                        }

                        foreach (var p in books.SelectMany(b => b.Payments).Where(p => p.Date.Year == m.Year && p.Date.Month == m.Month))
                        {
                            paid[p.Date.Day - 1] += p.Amount;
                        }

                        return new DsoChannelMonth($"{m.Year}-{m.Month}", m.Year, m.Month, shipped, paid);
                    })
                    .ToList();
                return new DsoChannel(g.Item1, g.Item2, months);
            })
            .Where(c => c.Months.Any(m => m.Shipped.Any(x => x != 0) || m.Paid.Any(x => x != 0)))
            .ToList();
    }

    private static decimal Share(decimal part, decimal total) => total > 0 ? Math.Clamp(part / total, 0, 1) : 0;
}

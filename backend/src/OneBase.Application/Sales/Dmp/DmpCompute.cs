namespace OneBase.Application.Sales.Dmp;

/// <summary>Приход стеллажей на склад региона: перемещение с завода, принятое дилером (дата — приёмка).</summary>
public sealed record DmpReceipt(string Region, DateOnly Date, decimal Qty);

/// <summary>Остаток стеллажей на складе: Region — регион склада; Factory — склад завода (в остаток дилеров не входит).</summary>
public sealed record DmpBalance(string Region, bool Factory, decimal Qty);

/// <summary>Вид строки стеллажа в документе магазина.</summary>
public enum DmpMove
{
    /// <summary>Заказ доставлен — стеллаж установлен.</summary>
    Installed,

    /// <summary>Заказ отдан — стеллаж выдан, доставка не подтверждена.</summary>
    Issued,

    /// <summary>Возврат стеллажа от магазина.</summary>
    Returned,
}

/// <summary>Строка стеллажа в заказе или возврате магазина. Date — дата реализации (у возврата — дата документа).</summary>
public sealed record DmpShelfLine(long DocumentId, DmpMove Move, DateOnly Date, long MarketId, long? AgentId, string Region, decimal Qty);

/// <summary>
/// Продажа магазина без стеллажей: строки заказа (Kg &gt; 0) или возврата (Kg &lt; 0) одного документа за день. FocusKg — вес категории, под
/// которую стоит стеллаж.
/// </summary>
public sealed record DmpSale(long MarketId, long DocumentId, bool Return, DateOnly Date, decimal Kg, decimal FocusKg);

/// <summary>Заказы магазина за окно: число заказов, вес и вес категории стеллажа (возвраты вычтены).</summary>
public sealed record DmpOrders(int Orders, decimal Kg, decimal FocusKg)
{
    public static readonly DmpOrders Empty = new(0, 0, 0);
}

/// <summary>Вес магазина за месяц: весь и категории стеллажа (возвраты вычтены).</summary>
public sealed record DmpMonthKg(int Year, int Month, decimal Kg, decimal FocusKg);

/// <summary>
/// Регион (склад дилера): получено с завода (и даты прихода), остаток на складе, установлено в магазины (за вычетом возвратов), выдано без
/// подтверждения, магазинов с установленным стеллажом.
/// </summary>
public sealed record DmpRegionRow(
    string Region,
    decimal Received,
    IReadOnlyList<DateOnly> ReceivedDates,
    decimal Left,
    decimal Installed,
    decimal Issued,
    int Shops);

/// <summary>
/// Магазин со стеллажом. Date — первая установка (без неё — первая выдача); Qty — установлено за вычетом возвратов, Issued — выдано без
/// подтверждения. Fill — заказ, которым привезли стеллаж (наполнение). Before и After — заказы магазина за Days дней до и после установки
/// (Days — дней от установки до даты данных, окна равные; заказ наполнения ни в одно не входит). LastOrder и IdleDays — последний заказ
/// и сколько дней назад.
/// </summary>
public sealed record DmpShopRow(
    long MarketId,
    string Market,
    string Region,
    long? AgentId,
    string? Agent,
    DateOnly Date,
    decimal Qty,
    decimal Issued,
    DmpOrders Fill,
    int Days,
    DmpOrders Before,
    DmpOrders After,
    DateOnly? LastOrder,
    int? IdleDays,
    IReadOnlyList<DmpMonthKg> Months);

/// <summary>
/// «ДМП»: стеллажи в магазинах — как вкладка «ДМП» «Полевого контроля». AsOf — дата данных; Products — товары-стеллажи; FocusCategory —
/// категория стеллажа; FactoryLeft — остаток на складе завода; Months — месяцы помесячного веса магазинов (последний — месяц AsOf).
/// </summary>
public sealed record DmpView(
    DateOnly AsOf,
    IReadOnlyList<string> Products,
    string? FocusCategory,
    decimal Received,
    decimal Left,
    decimal FactoryLeft,
    decimal Installed,
    decimal Issued,
    int Shops,
    IReadOnlyList<DmpRegionRow> Regions,
    IReadOnlyList<DmpShopRow> ShopRows,
    IReadOnlyList<DmpMonthKg> Months);

/// <summary>
/// Расчёт «ДМП» из строк копии Linko (DmpService их загружает). Путь стеллажа: завод → склад дилера (перемещение) → магазин (заказ со
/// строкой стеллажа: «доставлен» — установлен, «отдан» — выдан, не подтверждён; возврат стеллажа вычитается). По магазину — как он
/// заказывает: наполнение (заказ со стеллажом), заказы до и после установки за равное число дней, вес по месяцам.
/// </summary>
public static class DmpCompute
{
    public const int MonthsShown = 4;

    public static DmpView Build(
        DateOnly asOf,
        IReadOnlyList<string> products,
        string? focusCategory,
        IReadOnlyList<DmpReceipt> receipts,
        IReadOnlyList<DmpBalance> balances,
        IReadOnlyList<DmpShelfLine> shelves,
        IReadOnlyList<DmpSale> sales,
        IReadOnlyDictionary<long, string> markets,
        IReadOnlyDictionary<long, string> agents)
    {
        var months = Enumerable.Range(0, MonthsShown)
            .Select(i => new DateOnly(asOf.Year, asOf.Month, 1).AddMonths(i - MonthsShown + 1))
            .Select(m => new DmpMonthKg(m.Year, m.Month, 0, 0))
            .ToList();
        var salesOf = sales.Where(s => s.Date <= asOf).GroupBy(s => s.MarketId).ToDictionary(g => g.Key, g => g.ToList());

        var shops = new List<DmpShopRow>();
        foreach (var group in shelves.Where(s => s.Date <= asOf).GroupBy(s => s.MarketId))
        {
            var lines = group.OrderBy(s => s.Date).ThenBy(s => s.DocumentId).ToList();
            var installed = lines.Where(s => s.Move == DmpMove.Installed).ToList();
            var issued = lines.Where(s => s.Move == DmpMove.Issued).ToList();
            if (installed.Count == 0 && issued.Count == 0)
            {
                continue; // только возврат: стеллаж в периоде не ставили
            }

            var first = installed.Count > 0 ? installed[0] : issued[0];
            var qty = Math.Max(0, installed.Sum(s => s.Qty) - lines.Where(s => s.Move == DmpMove.Returned).Sum(s => s.Qty));
            var own = salesOf.GetValueOrDefault(group.Key) ?? [];
            var fillIds = installed.Concat(issued).Select(s => s.DocumentId).ToHashSet();
            var fillDoc = first.DocumentId;
            var days = Math.Max(0, asOf.DayNumber - first.Date.DayNumber);
            var rest = own.Where(s => !(fillIds.Contains(s.DocumentId) && !s.Return)).ToList();
            var orders = own.Where(s => !s.Return && s.Kg > 0).ToList();
            var last = orders.Count > 0 ? orders.Max(s => s.Date) : (DateOnly?)null;

            shops.Add(new DmpShopRow(
                group.Key,
                markets.GetValueOrDefault(group.Key) ?? $"Магазин {group.Key}",
                first.Region,
                first.AgentId,
                first.AgentId is { } a ? agents.GetValueOrDefault(a) : null,
                first.Date,
                qty,
                issued.Sum(s => s.Qty),
                Orders(own.Where(s => s.DocumentId == fillDoc && !s.Return)),
                days,
                Orders(rest.Where(s => s.Date >= first.Date.AddDays(-days) && s.Date < first.Date)),
                Orders(rest.Where(s => s.Date > first.Date && s.Date <= first.Date.AddDays(days))),
                last,
                last is { } l ? asOf.DayNumber - l.DayNumber : null,
                months.Select(m =>
                {
                    var inMonth = own.Where(s => s.Date.Year == m.Year && s.Date.Month == m.Month).ToList();
                    return m with { Kg = inMonth.Sum(s => s.Kg), FocusKg = inMonth.Sum(s => s.FocusKg) };
                }).ToList()));
        }

        var dealerBalances = balances.Where(b => !b.Factory).ToList();
        var regionNames = receipts.Select(r => r.Region)
            .Concat(dealerBalances.Select(b => b.Region))
            .Concat(shops.Select(s => s.Region))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var regions = regionNames
            .Select(name =>
            {
                bool Same(string region) => string.Equals(region, name, StringComparison.OrdinalIgnoreCase);
                var own = shops.Where(s => Same(s.Region)).ToList();
                return new DmpRegionRow(
                    name,
                    receipts.Where(r => Same(r.Region)).Sum(r => r.Qty),
                    receipts.Where(r => Same(r.Region)).Select(r => r.Date).Distinct().Order().ToList(),
                    dealerBalances.Where(b => Same(b.Region)).Sum(b => b.Qty),
                    own.Sum(s => s.Qty),
                    own.Sum(s => s.Issued),
                    own.Count(s => s.Qty > 0));
            })
            .Where(r => r.Received != 0 || r.Left != 0 || r.Installed != 0 || r.Issued != 0)
            .OrderByDescending(r => r.Installed).ThenByDescending(r => r.Received).ThenBy(r => r.Region, StringComparer.CurrentCulture)
            .ToList();

        return new DmpView(
            asOf,
            products,
            focusCategory,
            regions.Sum(r => r.Received),
            regions.Sum(r => r.Left),
            balances.Where(b => b.Factory).Sum(b => b.Qty),
            regions.Sum(r => r.Installed),
            regions.Sum(r => r.Issued),
            regions.Sum(r => r.Shops),
            regions,
            shops.OrderByDescending(s => s.Date).ThenBy(s => s.Region, StringComparer.CurrentCulture).ThenBy(s => s.Market, StringComparer.CurrentCulture).ToList(),
            months);
    }

    /// <summary>Заказы окна: число документов-заказов с весом, вес и вес категории за вычетом возвратов того же окна.</summary>
    private static DmpOrders Orders(IEnumerable<DmpSale> sales)
    {
        var list = sales.ToList();
        return list.Count == 0
            ? DmpOrders.Empty
            : new DmpOrders(list.Where(s => !s.Return && s.Kg > 0).Select(s => s.DocumentId).Distinct().Count(), list.Sum(s => s.Kg), list.Sum(s => s.FocusKg));
    }
}

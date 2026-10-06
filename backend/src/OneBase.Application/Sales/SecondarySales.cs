using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales;

// Правила вторичных продаж (дилер → торговая точка) в одном месте. Загрузчик делает только грубую выборку
// из копии Linko, а что считать продажей, возвратом и заказом для визита — решается здесь, чистыми функциями.

/// <summary>Строка заказа из копии Linko — до применения правил.</summary>
public sealed record RawOrderLine(
    long OrderId,
    string Status,
    DateOnly CreatedDate,
    DateOnly? DeliveryDate,
    DateOnly? AcceptedDate,
    long? BranchId,
    string? BranchName,
    long? AgentId,
    long? MarketId,
    long? ProductId,
    long? TypeId,
    decimal Kg,
    decimal Revenue,
    string? Currency = null);

/// <summary>Строка возврата (returned_products) — до применения правил. Вес и сумма — по строке, не по шапке.</summary>
public sealed record RawReturnLine(
    long ReturnId,
    string Status,
    DateOnly CreatedDate,
    long? BranchId,
    string? BranchName,
    long? AgentId,
    long? MarketId,
    long? ProductId,
    long? TypeId,
    decimal Kg,
    decimal Revenue);

/// <summary>Шапка документа возврата — только для контроля качества: вес шапки против веса строк.</summary>
public sealed record RawReturnHeader(long ReturnId, string Status, DateOnly CreatedDate, string? BranchName, decimal HeaderKg, int Lines, decimal LinesKg);

/// <summary>
/// Качество данных месяца: чего не хватает и что учтено иначе, чем выглядит в Linko. Показывается пользователю,
/// чтобы «ноль» не путали с «нет данных».
/// </summary>
public sealed record SalesDataQuality(
    int DeliveredWithoutAcceptance,
    int ZeroHeaderReturns,
    decimal ZeroHeaderReturnsKg,
    int ReturnsWithoutLines,
    decimal ReturnsWithoutLinesHeaderKg,
    int AcceptedInFuture = 0)
{
    public static readonly SalesDataQuality Empty = new(0, 0, 0, 0, 0);
}

/// <summary>Выручка заказов в валюте, отличной от основной: курса нет, поэтому с сумами не складывается.</summary>
public sealed record CurrencyTotal(string Currency, decimal Amount, int Orders);

public sealed record SecondarySalesResult(
    IReadOnlyList<SaleLine> Lines,
    IReadOnlyList<SaleLine> Excluded,
    SalesDataQuality Quality,
    IReadOnlyList<CurrencyTotal>? OtherCurrency = null,
    IReadOnlyList<CurrencyTotal>? ExcludedOtherCurrency = null);

public static class SecondarySales
{
    /// <summary>Узбекистан — UTC+5: отчётный день считается по местному времени.</summary>
    public static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    /// <summary>
    /// Последний день, который входит в отчёт: последний полный день — вчера по местному времени. Приёмки сегодняшнего дня
    /// появятся завтра (до тех пор они в «Качестве данных»), как отчётный день N в «Полевом контроле».
    /// </summary>
    public static DateOnly ReportCutoff(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(CompanyOffset).DateTime).AddDays(-1);

    /// <summary>
    /// Отчётный день месяца: последний день с продажами (не позже ReportCutoff), но не дальше конца месяца.
    /// Данных за месяц ещё нет — день перед его началом (отработано 0 дней).
    /// </summary>
    public static DateOnly DataThrough(DateOnly monthStart, DateOnly? lastData)
    {
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        return lastData is null || lastData < monthStart ? monthStart.AddDays(-1) : lastData > monthEnd ? monthEnd : lastData.Value;
    }

    /// <summary>
    /// Дни приёмки «после отчётного дня» в месяце — со следующего за отчётным днём по конец месяца: заказы, принятые сегодня или с приёмкой
    /// в будущем, войдут в отчёт позже («Качество данных»). Приёмки других месяцев сюда не относятся; у закрытого месяца таких дней нет — null.
    /// </summary>
    public static (DateOnly From, DateOnly To)? AcceptedAfterReport(DateOnly monthStart, DateOnly dataThrough)
    {
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        if (dataThrough >= monthEnd)
        {
            return null;
        }

        var from = dataThrough.AddDays(1);
        return (from < monthStart ? monthStart : from, monthEnd);
    }

    /// <summary>Дата реализации заказа по настройке (по умолчанию — приёмка, accepted_time).</summary>
    public static DateOnly? SaleDate(RawOrderLine l, SaleDateField field) => field switch
    {
        SaleDateField.Accepted => l.AcceptedDate,
        SaleDateField.Delivery => l.DeliveryDate ?? l.CreatedDate,
        _ => l.CreatedDate,
    };

    /// <summary>
    /// Факт месяца по [from; to] — отчётный день включительно:
    /// заказы — статус из SoldStatuses (delivered и given), дата реализации в периоде, филиал не исключён;
    /// возвраты — статус из ReturnStatuses, дата создания возврата в периоде (другой даты у возврата нет), филиал не исключён,
    /// вес и сумма по строкам документа со знаком минус.
    /// Строки исключённых филиалов («Завод») собираются отдельно — это экспорт и опт, не вторичка;
    /// строки пропускаемых филиалов (IgnoredBranches, «К К Мерч») не попадают никуда.
    /// Исключение — точки-каналы (Sales:ChannelRegions: базар, сети) из филиалов первички: в закрытом месяце (to — последний день
    /// месяца from) их строки идут во вторичку с виртуальным филиалом своего региона-канала; в идущем месяце — как раньше.
    /// </summary>
    public static SecondarySalesResult Build(
        IEnumerable<RawOrderLine> orders,
        IEnumerable<RawReturnLine> returns,
        IEnumerable<RawReturnHeader> returnHeaders,
        DateOnly from,
        DateOnly to,
        SalesOptions options,
        int deliveredWithoutAcceptance = 0)
    {
        var sold = options.SoldStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var returned = options.ReturnStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = new List<SaleLine>();
        var excluded = new List<SaleLine>();
        var otherCurrency = new List<(string Currency, decimal Amount, long OrderId, bool Excluded)>();
        var channelsOn = ChannelsOn(from, to, options);

        foreach (var o in orders)
        {
            if (!sold.Contains(o.Status) || SaleDate(o, options.DateField) is not { } date || date < from || date > to)
            {
                continue;
            }

            var branch = channelsOn ? ChannelBranch(o.MarketId, o.BranchName, date, options) : null;
            if (branch is null && options.IsIgnoredBranch(o.BranchName))
            {
                continue;
            }

            var isExcluded = branch is null && options.IsExcludedBranch(o.BranchName);
            var revenue = o.Revenue;
            if (!options.IsBaseCurrency(o.Currency))
            {
                // Курса в данных нет — выручку в долларах и т.п. с сумами не складываем; вес заказа учитывается.
                otherCurrency.Add((o.Currency!.Trim(), o.Revenue, o.OrderId, isExcluded));
                revenue = 0;
            }

            var line = new SaleLine(date, o.AgentId, o.MarketId, branch ?? o.BranchId, o.TypeId, o.ProductId, o.Kg, revenue, o.OrderId, o.CreatedDate);
            (isExcluded ? excluded : lines).Add(line);
        }

        static List<CurrencyTotal> Totals(IEnumerable<(string Currency, decimal Amount, long OrderId, bool Excluded)> source) =>
            source.GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
                .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount), g.Select(x => x.OrderId).Distinct().Count()))
                .OrderBy(x => x.Currency)
                .ToList();

        foreach (var r in returns)
        {
            if (!returned.Contains(r.Status) || r.CreatedDate < from || r.CreatedDate > to)
            {
                continue;
            }

            var branch = channelsOn ? ChannelBranch(r.MarketId, r.BranchName, r.CreatedDate, options) : null;
            if (branch is null && options.IsIgnoredBranch(r.BranchName))
            {
                continue;
            }

            var line = new SaleLine(r.CreatedDate, r.AgentId, r.MarketId, branch ?? r.BranchId, r.TypeId, r.ProductId, -r.Kg, -r.Revenue, null);
            (branch is null && options.IsExcludedBranch(r.BranchName) ? excluded : lines).Add(line);
        }

        var headers = returnHeaders
            .Where(h => returned.Contains(h.Status) && h.CreatedDate >= from && h.CreatedDate <= to
                        && !options.IsExcludedBranch(h.BranchName) && !options.IsIgnoredBranch(h.BranchName))
            .ToList();
        var zeroHeader = headers.Where(h => h.HeaderKg == 0 && h.LinesKg > 0).ToList();
        var noLines = headers.Where(h => h.Lines == 0 && h.HeaderKg > 0).ToList();

        return new SecondarySalesResult(lines, excluded, new SalesDataQuality(
                deliveredWithoutAcceptance,
                zeroHeader.Count,
                zeroHeader.Sum(h => h.LinesKg),
                noLines.Count,
                noLines.Sum(h => h.HeaderKg)),
            Totals(otherCurrency.Where(x => !x.Excluded)),
            Totals(otherCurrency.Where(x => x.Excluded)));
    }

    /// <summary>Каналы считаются за закрытый месяц: период [from; to] доходит до последнего дня месяца from.</summary>
    public static bool ChannelsOn(DateOnly from, DateOnly to, SalesOptions options) =>
        options.ChannelRegions.Count > 0 && to >= new DateOnly(from.Year, from.Month, 1).AddMonths(1).AddDays(-1);

    /// <summary>
    /// Виртуальный филиал региона-канала для строки точки-канала из филиала первички («Завод», «К К Мерч») не раньше первого месяца
    /// канала (SalesChannelRegion.Since), иначе null.
    /// </summary>
    public static long? ChannelBranch(long? marketId, string? branchName, DateOnly date, SalesOptions options) =>
        options.IsPrimaryOrderBranch(branchName) && options.ChannelOf(marketId) is { } channel && (channel.Since is not { } since || date >= since)
            ? channel.BranchId
            : null;

    /// <summary>
    /// Заказы для сшивки с визитами (календарь визитов): визит совпадает с днём ВВОДА заказа (created_date), а приёмка обычно
    /// на сутки позже. Берутся проданные заказы, созданные в [from; to], без исключённых и пропускаемых филиалов; дата строки — дата создания.
    /// </summary>
    public static IReadOnlyList<SaleLine> VisitOrders(IEnumerable<RawOrderLine> orders, DateOnly from, DateOnly to, SalesOptions options)
    {
        var sold = options.SoldStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return orders
            .Where(o => sold.Contains(o.Status) && o.CreatedDate >= from && o.CreatedDate <= to
                        && !options.IsExcludedBranch(o.BranchName) && !options.IsIgnoredBranch(o.BranchName))
            .Select(o => new SaleLine(o.CreatedDate, o.AgentId, o.MarketId, o.BranchId, o.TypeId, o.ProductId, o.Kg, o.Revenue, o.OrderId, o.CreatedDate))
            .ToList();
    }
}

/// <summary>
/// Категории отчёта поверх типов товаров Linko. У категорий из настройки — отрицательные id (−1, −2, … в порядке
/// настройки), у типов вне настройки — их собственный id: такие показываются отдельно и попадают в диагностику.
/// </summary>
public sealed class SalesCategories
{
    private readonly Dictionary<long, long> _groupOfType;
    private readonly Dictionary<long, string> _names;

    private SalesCategories(Dictionary<long, long> groupOfType, Dictionary<long, string> names)
    {
        _groupOfType = groupOfType;
        _names = names;
    }

    public static readonly SalesCategories None = new([], []);

    public static SalesCategories Build(IReadOnlyDictionary<string, string> configured, IReadOnlyDictionary<long, string> linkoTypes)
    {
        var groupOfType = new Dictionary<long, long>();
        var names = new Dictionary<long, string>();
        var index = 0;
        foreach (var (name, types) in configured)
        {
            var group = -(++index);
            names[group] = name;
            foreach (var part in types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(part, out var type))
                {
                    groupOfType.TryAdd(type, group);
                }
            }
        }

        foreach (var (type, name) in linkoTypes)
        {
            if (!groupOfType.ContainsKey(type))
            {
                groupOfType[type] = type;
                names[type] = name;
            }
        }

        return new SalesCategories(groupOfType, names);
    }

    /// <summary>Категория отчёта для типа товара; null — у товара нет типа.</summary>
    public long? GroupOf(long? type) => type is { } t ? _groupOfType.GetValueOrDefault(t, t) : null;

    public string NameOf(long? group) =>
        group is { } g ? _names.TryGetValue(g, out var n) ? n : $"Тип {g}" : "Без категории";

    /// <summary>Группа категории отчёта по её названию из настройки; null — такой категории нет.</summary>
    public long? GroupByName(string name) =>
        _names.Where(n => n.Key < 0 && string.Equals(n.Value, name, StringComparison.OrdinalIgnoreCase)).Select(n => (long?)n.Key).FirstOrDefault();

    /// <summary>Категория из настройки отчёта (а не тип Linko вне её).</summary>
    public static bool IsConfigured(long? group) => group is < 0;

    /// <summary>Пары «тип → категория» для SQL-агрегатов.</summary>
    public IReadOnlyDictionary<long, long> Mapping => _groupOfType;
}

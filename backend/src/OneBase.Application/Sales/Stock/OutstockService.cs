using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Sales.Stock;

/// <summary>
/// Пара «товар × регион» за период: дни в нуле, упущенные продажи и чья это потеря (дилер / завод / нет данных по заводу).
/// Days — флаги по дням периода: «1» — товар утром был, «0» — не было. Received — «1» в дни, когда с завода привезли.
/// </summary>
public sealed record OutstockPair(
    string RegionId,
    string Region,
    long ProductId,
    string Product,
    string? Code,
    string Category,
    bool Top,
    decimal PeriodKg,
    decimal PeriodSum,
    decimal PerDayKg,
    decimal? AvgPrice,
    int ZeroDays,
    int DealerDays,
    int FactoryDays,
    int UnknownDays,
    int NegativeDays,
    decimal LostKg,
    decimal LostSum,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    decimal UnknownLossSum,
    bool Core,
    bool Chronic,
    decimal SnapshotKg,
    string Days,
    string Received);

/// <summary>Карточка категории: Share — доля во всех потерях области, LossShare — упущенные кг к проданным («к факту»).</summary>
public sealed record OutstockCategory(string Name, decimal LostKg, decimal LostSum, decimal SoldKg, decimal Share, decimal? LossShare, int Pairs, int ZeroDays, bool Selected);

/// <summary>Товар из тройки самых дорогих потерь региона; Share — доля в потерях региона.</summary>
public sealed record OutstockTopProduct(long ProductId, string Name, string? Code, bool Top, decimal LostSum, decimal Share);

public sealed record OutstockRegion(
    string Id,
    string Name,
    string? Dealer,
    decimal SoldKg,
    decimal LostKg,
    decimal LostSum,
    decimal? LossShare,
    int ZeroDays,
    int Pairs,
    int CorePairs,
    int Chronic,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    decimal UnknownLossSum,
    IReadOnlyList<OutstockTopProduct> Top);

/// <summary>Товар по всем регионам: ZeroShare — доля дней в нуле среди дней регионов, где товар хотя бы день стоял в нуле.</summary>
public sealed record OutstockProduct(
    long Id,
    string Name,
    string? Code,
    string Category,
    bool Top,
    decimal SoldKg,
    decimal? ZeroShare,
    decimal LostKg,
    decimal LostSum,
    int Regions,
    int RegionsSold,
    int CorePairs,
    int Chronic,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    decimal UnknownLossSum);

public sealed record OutstockMatrixRow(string Id, string Name, string? Dealer, IReadOnlyList<decimal> Sum, IReadOnlyList<decimal> Kg, decimal TotalSum, decimal TotalKg);

/// <summary>Дилеры × категории: упущено по каждой категории у каждого региона, в сумах и в кг.</summary>
public sealed record OutstockMatrix(IReadOnlyList<string> Categories, IReadOnlyList<OutstockMatrixRow> Rows, IReadOnlyList<decimal> TotalSum, IReadOnlyList<decimal> TotalKg);

/// <summary>Итоги области. DealerDaysShare — доля дней «потеря дилера» среди всех дней в нуле (с днями без данных по заводу).</summary>
public sealed record OutstockTotals(
    decimal LostKg,
    decimal LostSum,
    decimal SoldKg,
    decimal SoldSum,
    decimal? LossShare,
    int Pairs,
    int PairsWithLoss,
    int ProductsWithLoss,
    int RegionsWithLoss,
    int ZeroDays,
    int DealerDays,
    int FactoryDays,
    int UnknownDays,
    decimal? DealerDaysShare,
    int CorePairs,
    decimal CoreSum,
    int Chronic,
    decimal ChronicSum,
    decimal DealerLossSum,
    decimal FactoryLossSum,
    decimal UnknownLossSum,
    int Cells,
    int NegativeCells,
    decimal? NegativeSharePct);

public sealed record OutstockRegionRef(string Id, string Name, string? Dealer);

/// <summary>Регион в «Выводах»: EveryNthKg — «каждый N-й килограмм могли продать, но не продали».</summary>
public sealed record OutstockInsightRegion(string Id, string Name, string? Dealer, decimal LostSum, decimal? LossShare, int? EveryNthKg);

public sealed record OutstockInsightProduct(long Id, string Name, decimal LostSum, int Regions);

/// <summary>
/// Числа для «Выводов» — считает сервер, страница только подставляет их в текст: регион с самой большой потерей, худший по доле
/// (среди регионов с продажами больше OutstockMath.WorstRegionMinKg, не тот же), категория с самой большой потерей (если категорий
/// больше одной), самый дорогой товар, товары, чаще всего попадающие в ядро, доля дней «потеря дилера».
/// </summary>
public sealed record OutstockInsights(
    OutstockInsightRegion? TopRegion,
    OutstockInsightRegion? WorstRegion,
    string? TopCategory,
    decimal? TopCategoryShare,
    OutstockInsightProduct? TopProduct,
    IReadOnlyList<string> CoreFrequent,
    decimal? DealerDaysShare);

public static class OutstockScope
{
    public const string Top = "top";
    public const string All = "all";
    public const string Rest = "rest";
}

/// <summary>Запрос страницы: месяц (пусто — последний закрытый), регион области, ТОП/все/кроме, категории, регион календаря.</summary>
public sealed record OutstockQuery(
    int? Year = null,
    int? Month = null,
    string? RegionId = null,
    string? Scope = null,
    IReadOnlyList<string>? Categories = null,
    string? CalendarRegionId = null);

/// <summary>
/// Страница аутстока. Calendar — пары региона CalendarRegionId для календаря по дням (итоги и таблицы при этом остаются по области);
/// CanReset — выбраны категории или область не «ТОП» (ссылка «Сбросить»).
/// </summary>
public sealed record OutstockView(
    int Year,
    int Month,
    DateOnly From,
    DateOnly To,
    int Days,
    int DaysInMonth,
    DateOnly SnapshotDate,
    DateTimeOffset? SyncedAt,
    string? RegionId,
    string Scope,
    IReadOnlyList<string> SelectedCategories,
    bool CanReset,
    bool TopConfigured,
    string TopHint,
    bool FactoryKnown,
    IReadOnlyList<OutstockRegionRef> Regions,
    OutstockTotals Totals,
    IReadOnlyList<OutstockCategory> Categories,
    IReadOnlyList<OutstockPair> Pairs,
    IReadOnlyList<OutstockRegion> ByRegion,
    IReadOnlyList<OutstockProduct> ByProduct,
    OutstockMatrix Matrix,
    OutstockInsights Insights,
    string? CalendarRegionId,
    IReadOnlyList<OutstockPair> Calendar);

/// <summary>Восстановленный месяц целиком — все пары с продажами по всем регионам; регион, ТОП и категории накладываются сверху без пересчёта.</summary>
public sealed record OutstockBase(
    int Year,
    int Month,
    DateOnly From,
    DateOnly To,
    int Days,
    int DaysInMonth,
    DateOnly SnapshotDate,
    DateTimeOffset? SyncedAt,
    bool FactoryKnown,
    bool TopConfigured,
    string TopHint,
    IReadOnlyList<OutstockRegionRef> Regions,
    IReadOnlyList<OutstockPair> Pairs);

/// <summary>
/// Аутсток за месяц — по умолчанию за последний закрытый (месяц перед месяцем снимка остатков). Остаток дилера по дням
/// восстанавливается назад от снимка Linko (StockSnapshot) по продажам (заказы по дате приёмки) и приходу с завода (перемещения
/// «Завод → склад региона», принятые к моменту снимка, по дате выдачи, минус возвраты). Остаток завода — так же от его снимка по
/// перемещениям; выпуска цехов в Linko нет, поэтому прошлый остаток завода завышен, а доля его потерь — оценка снизу. Считаются
/// только товары, которые регион в периоде продавал. ТОП-товары — коды из Sales:TopProducts (TopProductSet).
/// </summary>
public sealed class OutstockService(IAppDbContext db, SalesOptions options, IMemoryCache cache, SalesCacheSignal signal)
{
    public Task<OutstockView> GetAsync(int? year, int? month, string? regionId, string? scope = null, IReadOnlyList<string>? categories = null, CancellationToken ct = default) =>
        GetAsync(new OutstockQuery(year, month, regionId, scope, categories), ct);

    public async Task<OutstockView> GetAsync(OutstockQuery q, CancellationToken ct = default)
    {
        var snapshot = await StockSnapshotBuilder.GetAsync(cache, signal, db, options, ct);
        var (year, month) = q.Year is { } y && q.Month is >= 1 and <= 12 ? (y, q.Month.Value) : (snapshot.ClosedYear, snapshot.ClosedMonth);
        var b = await BaseAsync(year, month, ct);
        return Compose(b, q with { Year = year, Month = month, Categories = q.Categories ?? [] });
    }

    /// <summary>Восстановленный месяц — для страницы и для поправки скорости в рекомендуемом остатке (дни в нуле по парам).</summary>
    public Task<OutstockBase> BaseAsync(int year, int month, CancellationToken ct = default) =>
        SalesViewCache.GetAsync(cache, signal, $"sales:outstock:{year}-{month}", () => BuildAsync(year, month, ct));

    /// <summary>Категории из адреса: «Кекс,Помадка».</summary>
    public static IReadOnlyList<string> ParseCategories(string? cat) =>
        string.IsNullOrWhiteSpace(cat) ? [] : cat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Область страницы поверх восстановленного месяца: регион, ТОП/все/кроме ТОПа, выбранные категории; календарь — отдельным регионом.</summary>
    public static OutstockView Compose(OutstockBase b, OutstockQuery q)
    {
        var regionId = string.IsNullOrEmpty(q.RegionId) ? null : q.RegionId;
        var s = b.TopConfigured
            ? q.Scope switch { OutstockScope.All => OutstockScope.All, OutstockScope.Rest => OutstockScope.Rest, _ => OutstockScope.Top }
            : OutstockScope.All;
        var chosen = (q.Categories ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dealerOf = b.Regions.ToDictionary(r => r.Id, r => r.Dealer);

        var scoped = b.Pairs
            .Where(p => (regionId is null || p.RegionId == regionId) && (s == OutstockScope.All || p.Top == (s == OutstockScope.Top)))
            .ToList();

        // Карточки категорий — до фильтра по категориям: по ним и выбирают. Показываются все категории с потерями.
        var scopedLoss = scoped.Sum(p => p.LostSum);
        var categories = scoped.GroupBy(p => p.Category)
            .Select(g =>
            {
                var lost = g.Sum(p => p.LostSum);
                var lostKg = g.Sum(p => p.LostKg);
                var sold = g.Sum(p => p.PeriodKg);
                return new OutstockCategory(g.Key, lostKg, lost, sold, scopedLoss > 0 ? lost / scopedLoss : 0, sold > 0 ? lostKg / sold : null,
                    g.Count(p => p.ZeroDays > 0), g.Sum(p => p.ZeroDays), chosen.Contains(g.Key));
            })
            .Where(c => c.Selected || c.LostSum > 0 || c.ZeroDays > 0)
            .OrderByDescending(c => c.LostSum)
            .ThenBy(c => c.Name)
            .ToList();
        var selected = categories.Where(c => c.Selected).Select(c => c.Name).ToList();

        var filtered = chosen.Count == 0 ? scoped : scoped.Where(p => chosen.Contains(p.Category)).ToList();

        // Ядро потерь: самые дорогие пары, которые вместе дают 80% упущенного — внутри выбранной области.
        var ordered = filtered.OrderByDescending(p => p.LostSum).ThenByDescending(p => p.LostKg).ThenBy(p => p.Region).ThenBy(p => p.Product).ToList();
        var coreCount = OutstockMath.CoreCount(ordered.Select(p => p.LostSum).ToList());
        ordered = ordered.Select((p, i) => i < coreCount && p.LostSum > 0 ? p with { Core = true } : p).ToList();
        var withLoss = ordered.Where(p => p.ZeroDays > 0).ToList();

        var soldKg = ordered.Sum(p => p.PeriodKg);
        var lostKgTotal = ordered.Sum(p => p.LostKg);
        var cells = ordered.Count * b.Days;
        var negative = ordered.Sum(p => p.NegativeDays);
        var zeroDays = withLoss.Sum(p => p.ZeroDays);
        var dealerDays = withLoss.Sum(p => p.DealerDays);
        var totals = new OutstockTotals(
            lostKgTotal,
            ordered.Sum(p => p.LostSum),
            soldKg,
            ordered.Sum(p => p.PeriodSum),
            soldKg > 0 ? lostKgTotal / soldKg : null,
            ordered.Count,
            withLoss.Count,
            withLoss.Select(p => p.ProductId).Distinct().Count(),
            withLoss.Select(p => p.RegionId).Distinct().Count(),
            zeroDays,
            dealerDays,
            withLoss.Sum(p => p.FactoryDays),
            withLoss.Sum(p => p.UnknownDays),
            zeroDays > 0 ? (decimal)dealerDays / zeroDays : null,
            ordered.Count(p => p.Core),
            ordered.Where(p => p.Core).Sum(p => p.LostSum),
            ordered.Count(p => p.Chronic),
            ordered.Where(p => p.Chronic).Sum(p => p.LostSum),
            ordered.Sum(p => p.DealerLossSum),
            ordered.Sum(p => p.FactoryLossSum),
            ordered.Sum(p => p.UnknownLossSum),
            cells,
            negative,
            cells == 0 ? null : Math.Round(100m * negative / cells, 1));

        var byRegion = ordered.GroupBy(p => (p.RegionId, p.Region))
            .Select(g =>
            {
                var lost = g.Sum(p => p.LostSum);
                var lostKg = g.Sum(p => p.LostKg);
                var sold = g.Sum(p => p.PeriodKg);
                var top = g.Where(p => p.LostSum > 0).OrderByDescending(p => p.LostSum).Take(3)
                    .Select(p => new OutstockTopProduct(p.ProductId, p.Product, p.Code, p.Top, p.LostSum, lost > 0 ? p.LostSum / lost : 0))
                    .ToList();
                return new OutstockRegion(g.Key.RegionId, g.Key.Region, dealerOf.GetValueOrDefault(g.Key.RegionId), sold, lostKg, lost, sold > 0 ? lostKg / sold : null,
                    g.Sum(p => p.ZeroDays), g.Count(p => p.ZeroDays > 0), g.Count(p => p.Core), g.Count(p => p.Chronic),
                    g.Sum(p => p.DealerLossSum), g.Sum(p => p.FactoryLossSum), g.Sum(p => p.UnknownLossSum), top);
            })
            .OrderByDescending(r => r.LostSum)
            .ThenBy(r => r.Name)
            .ToList();

        var byProduct = ordered.GroupBy(p => p.ProductId)
            .Select(g =>
            {
                var first = g.First();
                var days = g.Count(p => p.ZeroDays > 0) * b.Days;
                var zero = g.Sum(p => p.ZeroDays);
                return new OutstockProduct(g.Key, first.Product, first.Code, first.Category, first.Top, g.Sum(p => p.PeriodKg), days > 0 ? (decimal)zero / days : null,
                    g.Sum(p => p.LostKg), g.Sum(p => p.LostSum), g.Count(p => p.ZeroDays > 0), g.Count(), g.Count(p => p.Core), g.Count(p => p.Chronic),
                    g.Sum(p => p.DealerLossSum), g.Sum(p => p.FactoryLossSum), g.Sum(p => p.UnknownLossSum));
            })
            .Where(p => p.Regions > 0)
            .OrderByDescending(p => p.LostSum)
            .ThenBy(p => p.Name)
            .ToList();

        // Дилеры × категории: колонки — категории по убыванию потерь, строки — регионы с потерями.
        var cats = withLoss.GroupBy(p => p.Category)
            .Select(g => (Name: g.Key, Sum: g.Sum(p => p.LostSum)))
            .OrderByDescending(x => x.Sum)
            .ThenBy(x => x.Name)
            .Select(x => x.Name)
            .ToList();
        var catIndex = cats.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
        var rows = byRegion.Where(r => r.LostSum > 0 || r.LostKg > 0)
            .Select(r =>
            {
                var sum = new decimal[cats.Count];
                var kgs = new decimal[cats.Count];
                foreach (var p in withLoss.Where(p => p.RegionId == r.Id))
                {
                    var i = catIndex[p.Category];
                    sum[i] += p.LostSum;
                    kgs[i] += p.LostKg;
                }

                return new OutstockMatrixRow(r.Id, r.Name, r.Dealer, sum, kgs, r.LostSum, r.LostKg);
            })
            .ToList();
        var matrix = new OutstockMatrix(cats, rows, cats.Select((_, i) => rows.Sum(r => r.Sum[i])).ToList(), cats.Select((_, i) => rows.Sum(r => r.Kg[i])).ToList());

        var calendarRegion = string.IsNullOrEmpty(q.CalendarRegionId) ? null : q.CalendarRegionId;
        IReadOnlyList<OutstockPair> calendar = calendarRegion is null ? [] : withLoss.Where(p => p.RegionId == calendarRegion).ToList();

        return new OutstockView(
            b.Year,
            b.Month,
            b.From,
            b.To,
            b.Days,
            b.DaysInMonth,
            b.SnapshotDate,
            b.SyncedAt,
            regionId,
            s,
            selected,
            selected.Count > 0 || (b.TopConfigured && s != OutstockScope.Top),
            b.TopConfigured,
            b.TopHint,
            b.FactoryKnown,
            b.Regions,
            totals,
            categories,
            withLoss, // пары без дней в нуле — только в итогах и группах, в списке они не нужны
            byRegion,
            byProduct,
            matrix,
            Insights(byRegion, byProduct, categories, selected, ordered, totals),
            calendarRegion,
            calendar);
    }

    /// <summary>Цифры «Выводов» по отфильтрованным парам: регион с самой большой потерей, худший по доле, категория, товар, ядро, доля дилера.</summary>
    private static OutstockInsights Insights(
        IReadOnlyList<OutstockRegion> byRegion,
        IReadOnlyList<OutstockProduct> byProduct,
        IReadOnlyList<OutstockCategory> categories,
        IReadOnlyList<string> selected,
        IReadOnlyList<OutstockPair> pairs,
        OutstockTotals totals)
    {
        var regions = byRegion.Where(r => r.LostSum > 0).ToList();
        var top = regions.FirstOrDefault();
        var worst = regions
            .Where(r => r.SoldKg > OutstockMath.WorstRegionMinKg && r.LossShare is not null)
            .OrderByDescending(r => r.LossShare)
            .FirstOrDefault();
        OutstockInsightRegion? Region(OutstockRegion? r) =>
            r is null ? null : new OutstockInsightRegion(r.Id, r.Name, r.Dealer, r.LostSum, r.LossShare, OutstockMath.EveryNthKg(r.SoldKg, r.LostKg));

        var cards = selected.Count > 0 ? categories.Where(c => c.Selected).ToList() : categories.ToList();
        var topCategory = cards.Count > 1 ? cards.OrderByDescending(c => c.LostSum).First() : null;
        var product = byProduct.FirstOrDefault(p => p.LostSum > 0);
        var frequent = pairs.Where(p => p.Core)
            .GroupBy(p => p.Product)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Sum(p => p.LostSum))
            .Take(3)
            .Select(g => g.Key)
            .ToList();

        return new OutstockInsights(
            Region(top),
            worst is not null && top is not null && worst.Id != top.Id ? Region(worst) : null,
            topCategory?.Name,
            topCategory?.Share,
            product is null ? null : new OutstockInsightProduct(product.Id, product.Name, product.LostSum, product.Regions),
            frequent,
            totals.DealerDaysShare);
    }

    private async Task<OutstockBase> BuildAsync(int year, int month, CancellationToken ct)
    {
        var snapshot = await StockSnapshotBuilder.GetAsync(cache, signal, db, options, ct);
        var snapshotDate = snapshot.SnapshotDate;
        var monthStart = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var monthEnd = new DateOnly(year, month, daysInMonth);
        var to = snapshotDate < monthEnd ? snapshotDate : monthEnd;

        // Дилер региона — из справочника; регионы — склады дилеров снимка.
        var dealerOf = snapshot.KeptRegions.ToDictionary(r => r.Id.ToString(), r => r.Dealer);
        var regions = snapshot.Regions.Select(r => new OutstockRegionRef(r.Id, r.Name, dealerOf.GetValueOrDefault(r.Id))).ToList();

        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        var top = TopProductSet.Of(options);
        var (topConfigured, topHint) = top.Configured
            ? (true, $"ТОП — {top.Count} товаров из настройки Sales:TopProducts (коды Linko)")
            : (false, "ТОП-товары не настроены (Sales:TopProducts) — показаны все SKU");
        var factoryKnown = snapshot.Factory is not null;

        if (to < monthStart)
        {
            return new OutstockBase(year, month, monthStart, monthStart, 0, daysInMonth, snapshotDate, snapshot.SyncedAt, factoryKnown, topConfigured, topHint, regions, []);
        }

        // Диапазон восстановления — от первого дня месяца до снимка (у закрытого месяца — через все дни после него).
        var n = snapshotDate.DayNumber - monthStart.DayNumber + 1;
        var analyzed = to.DayNumber - monthStart.DayNumber + 1;
        int Index(DateOnly d) => d.DayNumber - monthStart.DayNumber;

        // Момент снимка по местному времени: Linko отдаёт даты заказов и перемещений без пояса, в местном времени.
        // Движения позже этого момента в снимке ещё не отражены и назад не прибавляются.
        var snapshotLocal = snapshot.SnapshotLocal;
        // Приёмка позже синхронизации заказов — плановая дата (у «отдан» Linko ставит её вперёд): товар уже ушёл со склада к снимку.
        var ordersSynced = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "orders", ct))?.LastSuccessAt;
        var ordersLocal = ordersSynced is { } os ? os.ToOffset(StockSnapshotBuilder.CompanyOffset).DateTime : snapshotLocal;
        var sold = options.SoldStatuses;
        var excluded = options.NotSecondaryBranchesLower();
        var regionOfBranch = snapshot.RegionOfBranch();

        // Продажи по дням: заказы дилеров магазинам по дате приёмки, без филиалов «Завод», «Экспорт» и пропускаемых, в статусах
        // вторички (доставлен и отдан: у «отдан» в Linko есть приёмка) — по одним и тем же заказам и средние продажи с ценой,
        // и остаток назад: проданное «отданным» заказом тоже ушло со склада дилера.
        var sales = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.AcceptedDate >= monthStart && l.ProductId != null
                      && (o.BranchName == null || !excluded.Contains(o.BranchName.ToLower()))
                group l by new { o.AcceptedDate, o.BranchId, l.ProductId, Late = o.AcceptedAt > snapshotLocal && o.AcceptedAt <= ordersLocal } into g
                select new
                {
                    Date = g.Key.AcceptedDate!.Value,
                    g.Key.BranchId,
                    ProductId = g.Key.ProductId!.Value,
                    g.Key.Late,
                    Kg = g.Sum(x => x.TotalWeight),
                    Sum = g.Sum(x => x.TotalPrice),
                })
            .ToListAsync(ct);

        var soldByDay = new Dictionary<(string Region, long Product), decimal[]>();
        var revenue = new Dictionary<(string Region, long Product), (decimal Kg, decimal Sum)>();
        foreach (var s in sales)
        {
            if (s.BranchId is not { } branch || !regionOfBranch.TryGetValue(branch, out var region))
            {
                continue;
            }

            var key = (region, s.ProductId);
            var i = Index(s.Date);
            if (!s.Late)
            {
                if (!soldByDay.TryGetValue(key, out var arr))
                {
                    soldByDay[key] = arr = new decimal[n];
                }

                arr[Math.Min(i, n - 1)] += s.Kg; // плановая приёмка после снимка — товар ушёл к снимку, день снимка
            }

            if (i < analyzed)
            {
                var r = revenue.GetValueOrDefault(key);
                revenue[key] = (r.Kg + s.Kg, r.Sum + s.Sum);
            }
        }

        // Приход дилеру и движения завода — перемещения в статусах отгрузки по дате выдачи (без неё — приёмки, без неё —
        // создания), как отгрузки по дням в первичке. Дилеру приход засчитывается, только если перемещение принято к моменту
        // снимка (OutstockMath.ReceiptCounts): непринятого товара в снимке ещё нет. Возвраты дилера заводу вычитаются из прихода.
        var regionOfStock = snapshot.Regions.ToDictionary(r => r.StockId, r => r.Id);
        var factoryStock = snapshot.Factory?.StockId;
        var shipped = options.ShippedTransferStatuses;
        var coarseFrom = monthStart.AddDays(-45);
        var transfers = await (
                from l in db.LinkoStockTransferLines
                join t in db.LinkoStockTransfers on l.TransferId equals t.Id
                where shipped.Contains(t.Status) && l.ProductId != null && t.CreatedDate >= coarseFrom
                select new { t.FromStockId, t.ToStockId, t.GivenAt, t.AcceptedAt, t.CreatedAt, ProductId = l.ProductId!.Value, l.TotalWeight })
            .ToListAsync(ct);

        var receivedByDay = new Dictionary<(string Region, long Product), decimal[]>();
        var factoryOut = new Dictionary<long, decimal[]>();
        var factoryIn = new Dictionary<long, decimal[]>();
        decimal[] Series<TKey>(Dictionary<TKey, decimal[]> map, TKey key) where TKey : notnull
        {
            if (!map.TryGetValue(key, out var arr))
            {
                map[key] = arr = new decimal[n];
            }

            return arr;
        }

        foreach (var t in transfers)
        {
            var fromFactory = factoryStock is { } f && t.FromStockId == f;
            var toFactory = factoryStock is { } f2 && t.ToStockId == f2;
            var when = t.GivenAt ?? t.AcceptedAt ?? t.CreatedAt;
            if ((!fromFactory && !toFactory) || when is null || when.Value > snapshotLocal)
            {
                continue;
            }

            var date = DateOnly.FromDateTime(when.Value);
            if (date < monthStart || date > snapshotDate)
            {
                continue;
            }

            var i = Index(date);
            if (fromFactory)
            {
                Series(factoryOut, t.ProductId)[i] += t.TotalWeight;
                if (t.ToStockId is { } toStock && regionOfStock.TryGetValue(toStock, out var region) && OutstockMath.ReceiptCounts(t.AcceptedAt, snapshotLocal))
                {
                    Series(receivedByDay, (region, t.ProductId))[i] += t.TotalWeight;
                }
            }

            if (toFactory)
            {
                Series(factoryIn, t.ProductId)[i] += t.TotalWeight;
                if (t.FromStockId is { } fromStock && regionOfStock.TryGetValue(fromStock, out var region))
                {
                    Series(receivedByDay, (region, t.ProductId))[i] -= t.TotalWeight; // возврат дилера заводу
                }
            }
        }

        // Склад завода известен по товару, если у него есть остаток на заводе или движения завода в периоде; иначе чья потеря — «нет данных».
        var factoryMorning = new Dictionary<long, decimal[]?>();
        decimal[]? FactoryMorning(long productId)
        {
            if (factoryMorning.TryGetValue(productId, out var cached))
            {
                return cached;
            }

            var stock = factoryStock is { } fs ? snapshot.KgOf(productId, fs) : null;
            var known = factoryKnown && (stock is not null && stock != 0 || factoryOut.ContainsKey(productId) || factoryIn.ContainsKey(productId));
            decimal[]? morning = null;
            if (known)
            {
                (morning, _) = OutstockMath.Reconstruct(stock ?? 0, factoryOut.GetValueOrDefault(productId) ?? new decimal[n], factoryIn.GetValueOrDefault(productId) ?? new decimal[n]);
            }

            return factoryMorning[productId] = morning;
        }

        var regionName = regions.ToDictionary(r => r.Id, r => r.Name);
        var stockOfRegion = snapshot.Regions.ToDictionary(r => r.Id, r => r.StockId);
        var pairs = new List<OutstockPair>();
        foreach (var (key, (periodKg, periodSum)) in revenue)
        {
            if (!regionName.TryGetValue(key.Region, out var name) || periodKg <= 0)
            {
                continue; // регион без склада или товар, который регион в периоде не продавал, аутстоком не считается
            }

            var soldArr = soldByDay.GetValueOrDefault(key) ?? new decimal[n];
            var snapshotKg = snapshot.KgOf(key.Product, stockOfRegion[key.Region]) ?? 0;
            var received = receivedByDay.GetValueOrDefault(key) ?? new decimal[n];
            var (morning, negative) = OutstockMath.Reconstruct(snapshotKg, soldArr, received);
            var factory = FactoryMorning(key.Product);

            var flags = new char[analyzed];
            var arrivals = new char[analyzed];
            int zero = 0, dealer = 0, factoryDays = 0, unknown = 0, negativeDays = 0;
            for (var i = 0; i < analyzed; i++)
            {
                var present = OutstockMath.InStock(morning[i]);
                flags[i] = present ? '1' : '0';
                arrivals[i] = received[i] > 0 ? '1' : '0';
                if (negative[i])
                {
                    negativeDays++;
                }

                if (present)
                {
                    continue;
                }

                zero++;
                switch (OutstockMath.Blame(factory?[i]))
                {
                    case LossOwner.Dealer:
                        dealer++;
                        break;
                    case LossOwner.Factory:
                        factoryDays++;
                        break;
                    default:
                        unknown++;
                        break;
                }
            }

            var perDay = periodKg / analyzed;
            var avgPrice = periodKg > 0 ? periodSum / periodKg : (decimal?)null;
            var lostKg = OutstockMath.LostKg(zero, periodKg, analyzed);
            var lostSum = avgPrice is { } price ? lostKg * price : 0;
            var product = snapshot.Products.GetValueOrDefault(key.Product);
            var group = categories.GroupOf(product?.TypeId);
            pairs.Add(new OutstockPair(
                key.Region,
                name,
                key.Product,
                product?.Name ?? $"Товар {key.Product}",
                product?.Code,
                categories.NameOf(group),
                top.Contains(product?.Code),
                periodKg,
                periodSum,
                perDay,
                avgPrice,
                zero,
                dealer,
                factoryDays,
                unknown,
                negativeDays,
                lostKg,
                lostSum,
                zero == 0 ? 0 : lostSum * dealer / zero,
                zero == 0 ? 0 : lostSum * factoryDays / zero,
                zero == 0 ? 0 : lostSum * unknown / zero,
                false,
                OutstockMath.IsChronic(zero, analyzed),
                snapshotKg,
                new string(flags),
                new string(arrivals)));
        }

        var withSales = pairs.Select(p => p.RegionId).ToHashSet();
        regions = regions.Where(r => withSales.Contains(r.Id)).ToList();

        return new OutstockBase(year, month, monthStart, to, analyzed, daysInMonth, snapshotDate, snapshot.SyncedAt, factoryKnown, topConfigured, topHint, regions, pairs);
    }
}

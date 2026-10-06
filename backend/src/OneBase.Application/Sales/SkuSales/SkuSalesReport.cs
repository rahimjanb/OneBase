using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales.SkuSales;

// «Продажи по SKU» (DOC §9.6, DOC-filters §6): вторичка за любой отрезок месяцев по SKU — кг, выручка, АКБ SKU, ABC, регионы, месяцы.
// SQL-агрегат даёт ISalesHistoryReader.SkuSalesAsync; здесь — чистый расчёт страницы по агрегату, все числа считает сервер.

/// <summary>Продажи товара в регионе за месяц: кг и выручка нетто (заказы минус возвраты). RegionId = null — филиал вне структуры.</summary>
public sealed record SkuMonthSales(long ProductId, Guid? RegionId, int Year, int Month, decimal Kg, decimal Revenue);

/// <summary>АКБ SKU в регионе за отрезок: различные ТТ региона с положительной месячной строкой товара хотя бы в одном месяце.</summary>
public sealed record SkuRegionAkb(long ProductId, Guid? RegionId, int Akb);

/// <summary>Агрегат отрезка: кг и выручка по товару × региону × месяцу, АКБ SKU по товару (вся страна) и по товару × региону.</summary>
public sealed record SkuSalesAggregate(IReadOnlyList<SkuMonthSales> Months, IReadOnlyDictionary<long, int> Akb, IReadOnlyList<SkuRegionAkb> RegionAkb)
{
    public static readonly SkuSalesAggregate Empty = new([], new Dictionary<long, int>(), []);
}

/// <summary>Фильтр ТОП: все, только ТОП, кроме ТОПа.</summary>
public static class SkuSalesTop
{
    public const string All = "all";
    public const string Only = "only";
    public const string Not = "not";
}

/// <summary>Фильтры страницы: регион (null — вся страна), ТОП, группа ABC («all» — все), категории отчёта по названию (пусто — все).</summary>
public sealed record SkuSalesFilter(Guid? Region, string Top, string Abc, IReadOnlyList<string> Categories);

/// <summary>Справочники расчёта: товары, категории отчёта (Sales:Categories), регионы вторички, список ТОП.</summary>
public sealed record SkuSalesCatalog(
    IReadOnlyDictionary<long, ProductInfo> Products,
    SalesCategories Categories,
    IReadOnlyList<RegionInfo> Regions,
    TopProductSet Top);

/// <summary>Месяц отрезка: Days — дней с данными (у идущего месяца — по отчётный день, у будущего — 0), Partial — месяц не закрыт.</summary>
public sealed record SkuSalesMonth(int Year, int Month, int Days, bool Partial);

public sealed record SkuSalesRegionRef(string Id, string Name);

/// <summary>Первый и последний квартал отрезка для «динамики»: подписи и дни с данными в месяцах отрезка этих кварталов.</summary>
public sealed record SkuSalesQuarters(string First, string Last, int FirstDays, int LastDays);

/// <summary>
/// Строка SKU в охвате (регион или страна) после фильтров. Rank, Share, Cumulative и Abc — по отфильтрованному набору до фильтра ABC:
/// SKU по убыванию кг, Share — доля кг, Cumulative — накопленная доля, A — до 80%, B — до 95%, C — остальные. Akb — АКБ SKU за отрезок
/// (различные ТТ с положительной месячной строкой). Regions — регионов с кг больше нуля (вся страна). FirstKgPerDay / LastKgPerDay —
/// кг в день в первом и последнем квартале отрезка (SkuSalesQuarters), Dynamics — изменение, только если в первом квартале кг в день
/// больше нуля (отрицательное нетто — не база); IsNew — в первом квартале продаж не было (кг в день не больше нуля), а в последнем есть.
/// Months — кг по месяцам отрезка.
/// </summary>
public sealed record SkuSalesRow(
    long ProductId,
    string Name,
    string? Code,
    string Category,
    bool IsTop,
    int Rank,
    decimal Kg,
    decimal Revenue,
    decimal? PricePerKg,
    decimal? Share,
    decimal? Cumulative,
    string Abc,
    int Akb,
    int Regions,
    decimal? FirstKgPerDay,
    decimal? LastKgPerDay,
    decimal? Dynamics,
    bool IsNew,
    IReadOnlyList<decimal> Months);

/// <summary>Итог месяца по строкам таблицы: кг и кг в день (дней с данными).</summary>
public sealed record SkuSalesMonthTotal(int Year, int Month, decimal Kg, decimal? KgPerDay, bool Partial);

/// <summary>
/// Плитки: кг, выручка, цена за кг, SKU с продажами (кг больше нуля), из них группы A, регионов с продажами, доля топ-5 SKU в весе,
/// доля выборки — вес строк таблицы к весу набора карточек категорий (те же фильтры региона, ТОП и ABC, без фильтра категорий — как у эталона).
/// </summary>
public sealed record SkuSalesTotals(decimal Kg, decimal Revenue, decimal? PricePerKg, int Skus, int SkusA, int Regions, decimal? Top5Share, decimal? ShareOfAll);

/// <summary>
/// Карточка категории — переключатель фильтра (как у эталона): фильтры региона, ТОП и ABC действуют (ABC — внутри набора без фильтра
/// категорий), фильтр категорий — нет, иначе выбрав одну, на другую не переключиться; доли — от суммы карточек.
/// </summary>
public sealed record SkuSalesCategory(string Name, decimal Kg, decimal Revenue, int Skus, decimal? KgShare, decimal? RevenueShare, decimal? PricePerKg, bool Selected);

/// <summary>SKU × регионы (по всей стране, регион — колонка): кг, выручка, АКБ и доля SKU в весе региона — в порядке Regions вида.</summary>
public sealed record SkuSalesRegionRow(
    long ProductId,
    string Name,
    string? Code,
    string Category,
    bool IsTop,
    string Abc,
    decimal Kg,
    decimal Revenue,
    decimal? CountryShare,
    IReadOnlyList<decimal> RegionKg,
    IReadOnlyList<decimal> RegionRevenue,
    IReadOnlyList<int> RegionAkb,
    IReadOnlyList<decimal?> RegionShare);

public sealed record SkuSalesRegionMatrix(IReadOnlyList<SkuSalesRegionRow> Rows, IReadOnlyList<decimal> TotalKg, IReadOnlyList<decimal> TotalRevenue, decimal Kg, decimal Revenue);

/// <summary>Категория × регионы: кг и доля категории в весе региона; TotalShare — доля категории во всём весе.</summary>
public sealed record SkuSalesCategoryRegionRow(string Name, IReadOnlyList<decimal> Kg, IReadOnlyList<decimal?> Share, decimal TotalKg, decimal? TotalShare);

public sealed record SkuSalesCategoryMatrix(IReadOnlyList<SkuSalesCategoryRegionRow> Rows, IReadOnlyList<decimal> TotalKg, decimal Kg);

public sealed record SkuSalesTopItem(long ProductId, string Name, bool IsTop, decimal Kg, decimal? Share);

/// <summary>Топ-10 SKU региона по весу; доля — от веса региона.</summary>
public sealed record SkuSalesTopRegion(string Id, string Name, decimal Kg, IReadOnlyList<SkuSalesTopItem> Items);

/// <summary>
/// «Выводы» — концентрация веса в охвате (регион, ТОП, категории; до фильтра ABC): главный SKU и его доли в весе и деньгах, доли топ-5,
/// топ-10 и топ-25 SKU, SKU в группах A, B и C, доля веса группы C.
/// </summary>
public sealed record SkuSalesFacts(
    int Skus,
    decimal Kg,
    decimal Revenue,
    int Regions,
    string? TopName,
    decimal TopKg,
    decimal TopRevenue,
    decimal? TopKgShare,
    decimal? TopRevenueShare,
    decimal? Top5Share,
    decimal? Top10Share,
    decimal? Top25Share,
    int SkusA,
    int SkusB,
    int SkusC,
    decimal? GroupCShare);

/// <summary>
/// «Продажи по SKU» за отрезок месяцев. Rows, MonthTotals, Totals и Facts — в охвате фильтра региона; RegionMatrix, CategoryRegions
/// и TopRegions — по всей стране (регион — колонка), с фильтрами ТОП, категорий и ABC. OutsideReport — товаров вне категорий отчёта
/// (бонус, подарки) с продажами за отрезок: в отчёт не входят.
/// </summary>
public sealed record SkuSalesView(
    string From,
    string To,
    DateOnly? DataThrough,
    IReadOnlyList<SkuSalesMonth> Months,
    string? RegionId,
    string? RegionName,
    string Top,
    string Abc,
    IReadOnlyList<string> SelectedCategories,
    bool TopConfigured,
    IReadOnlyList<SkuSalesRegionRef> Regions,
    SkuSalesTotals Totals,
    IReadOnlyList<SkuSalesCategory> Categories,
    SkuSalesQuarters? Quarters,
    IReadOnlyList<SkuSalesRow> Rows,
    IReadOnlyList<SkuSalesMonthTotal> MonthTotals,
    SkuSalesRegionMatrix RegionMatrix,
    SkuSalesCategoryMatrix CategoryRegions,
    IReadOnlyList<SkuSalesTopRegion> TopRegions,
    SkuSalesFacts Facts,
    int OutsideReport);

/// <summary>Расчёт страницы «Продажи по SKU» по агрегату — чистые функции.</summary>
public static class SkuSalesReport
{
    /// <summary>Сколько SKU в «топе» каждого региона.</summary>
    public const int TopPerRegion = 10;

    private static readonly string[] MonthsShort = ["янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];
    private static readonly string[] Quarters = ["I", "II", "III", "IV"];

    /// <summary>Товар категорий отчёта: кг и выручка по региону × месяцу, АКБ SKU по стране и по регионам.</summary>
    private sealed class Sku(long id, string name, string? code, long? group, string category, bool isTop, int regions, int months)
    {
        public long Id { get; } = id;
        public string Name { get; } = name;
        public string? Code { get; } = code;
        public long? Group { get; } = group;
        public string Category { get; } = category;
        public bool IsTop { get; } = isTop;
        public decimal[,] Kg { get; } = new decimal[regions, months];
        public decimal[,] Revenue { get; } = new decimal[regions, months];
        public int[] RegionAkb { get; } = new int[regions];
        public int Akb { get; set; }

        /// <summary>Кг в регионе (region ≥ 0) или по всей стране (−1) за месяцы отрезка.</summary>
        public decimal KgOf(int region, IEnumerable<int> months) => Sum(Kg, region, months);

        public decimal RevenueOf(int region, IEnumerable<int> months) => Sum(Revenue, region, months);

        private static decimal Sum(decimal[,] values, int region, IEnumerable<int> months)
        {
            var total = 0m;
            foreach (var m in months)
            {
                if (region >= 0)
                {
                    total += values[region, m];
                    continue;
                }

                for (var r = 0; r < values.GetLength(0); r++)
                {
                    total += values[r, m];
                }
            }

            return total;
        }
    }

    /// <summary>SKU в наборе с рангом и группой ABC.</summary>
    private sealed record Ranked(Sku Sku, decimal Kg, decimal Revenue, int Akb, int Rank, decimal? Share, decimal? Cumulative, string Abc);

    /// <summary>
    /// Месяцы отрезка [from; to] с днями данных: закрытый месяц — все дни, идущий — по отчётный день dataThrough, месяц после него — 0.
    /// </summary>
    public static IReadOnlyList<SkuSalesMonth> MonthsOf(int fromYear, int fromMonth, int toYear, int toMonth, DateOnly? dataThrough)
    {
        var list = new List<SkuSalesMonth>();
        for (var start = new DateOnly(fromYear, fromMonth, 1); start <= new DateOnly(toYear, toMonth, 1); start = start.AddMonths(1))
        {
            var length = DateTime.DaysInMonth(start.Year, start.Month);
            var days = dataThrough is not { } last || last < start ? 0 : Math.Min(length, last.DayNumber - start.DayNumber + 1);
            list.Add(new SkuSalesMonth(start.Year, start.Month, days, days < length));
        }

        return list;
    }

    public static SkuSalesView Build(
        string from,
        string to,
        IReadOnlyList<SkuSalesMonth> months,
        DateOnly? dataThrough,
        SkuSalesFilter filter,
        SkuSalesCatalog catalog,
        SkuSalesAggregate aggregate)
    {
        var monthIndex = months.Select((m, i) => (m, i)).ToDictionary(x => (x.m.Year, x.m.Month), x => x.i);
        var allMonths = Enumerable.Range(0, months.Count).ToList();

        // Товары категорий отчёта; остальные типы Linko (бонус, подарки) в отчёт не входят — только счётчик.
        var hasReport = catalog.Categories.Mapping.Values.Any(g => SalesCategories.IsConfigured(g));
        long? GroupOf(long product) => catalog.Categories.GroupOf(catalog.Products.GetValueOrDefault(product)?.CategoryId);
        bool InReport(long product) => !hasReport || SalesCategories.IsConfigured(GroupOf(product));
        var inRange = aggregate.Months.Where(m => monthIndex.ContainsKey((m.Year, m.Month))).ToList();

        // Регионы — колонки: регионы вторички по убыванию веса за отрезок и «Без региона», если у филиалов вне структуры есть продажи.
        var known = catalog.Regions.Select(r => r.Id).ToHashSet();
        Guid KeyOf(Guid? id) => id is { } g && known.Contains(g) ? g : SalesAnalytics.NoRegionId;
        var weight = inRange.Where(m => InReport(m.ProductId)).GroupBy(m => KeyOf(m.RegionId)).ToDictionary(g => g.Key, g => g.Sum(m => m.Kg));
        var regionIds = catalog.Regions
            .OrderByDescending(r => weight.GetValueOrDefault(r.Id))
            .ThenBy(r => r.Name, StringComparer.CurrentCulture)
            .Select(r => r.Id)
            .ToList();
        // «Без региона» выбран, а продаж вне структуры за отрезок нет — пустой охват (колонка с нулями), а не вся страна.
        if (filter.Region == SalesAnalytics.NoRegionId
            || inRange.Any(m => KeyOf(m.RegionId) == SalesAnalytics.NoRegionId && InReport(m.ProductId) && (m.Kg != 0 || m.Revenue != 0)))
        {
            regionIds.Add(SalesAnalytics.NoRegionId);
        }

        var regionIndex = regionIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        int RegionOf(Guid? id) => regionIndex.TryGetValue(KeyOf(id), out var i) ? i : -1;

        var skus = new Dictionary<long, Sku>();
        var outside = new HashSet<long>();
        foreach (var row in inRange)
        {
            if (!InReport(row.ProductId))
            {
                if (row.Kg != 0 || row.Revenue != 0)
                {
                    outside.Add(row.ProductId);
                }

                continue;
            }

            var r = RegionOf(row.RegionId);
            if (r < 0)
            {
                continue;
            }

            var m = monthIndex[(row.Year, row.Month)];
            var product = catalog.Products.GetValueOrDefault(row.ProductId);
            var group = GroupOf(row.ProductId);

            if (!skus.TryGetValue(row.ProductId, out var sku))
            {
                sku = new Sku(row.ProductId, product?.Name ?? $"Товар {row.ProductId}", product?.Code, group, catalog.Categories.NameOf(group),
                    catalog.Top.Contains(product?.Code), regionIds.Count, months.Count);
                skus[row.ProductId] = sku;
            }

            sku.Kg[r, m] += row.Kg;
            sku.Revenue[r, m] += row.Revenue;
        }

        foreach (var (product, akb) in aggregate.Akb)
        {
            if (skus.TryGetValue(product, out var sku))
            {
                sku.Akb = akb;
            }
        }

        foreach (var row in aggregate.RegionAkb)
        {
            if (skus.TryGetValue(row.ProductId, out var sku) && RegionOf(row.RegionId) is var r && r >= 0)
            {
                sku.RegionAkb[r] += row.Akb;
            }
        }

        var categoryNames = skus.Values.Select(s => s.Category).Distinct().ToList();
        var selected = filter.Categories
            .Select(c => categoryNames.FirstOrDefault(n => string.Equals(n, c.Trim(), StringComparison.OrdinalIgnoreCase)) ?? c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var chosen = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Регион фильтра — только из списка регионов (SkuSalesService проверяет раньше): чужой id не превращается в «всю страну».
        var scope = filter.Region is { } regionId
            ? regionIndex.TryGetValue(regionId, out var picked) ? picked : throw new ArgumentException("Регион не найден.", nameof(filter))
            : -1;

        bool TopOk(Sku s) => filter.Top switch
        {
            SkuSalesTop.Only => s.IsTop,
            SkuSalesTop.Not => !s.IsTop,
            _ => true,
        };
        bool CategoryOk(Sku s) => chosen.Count == 0 || chosen.Contains(s.Category);
        bool AbcOk(Ranked x) => filter.Abc is not (AbcGroups.A or AbcGroups.B or AbcGroups.C) || x.Abc == filter.Abc;

        // Охват: строки с продажами в регионе (или по стране), ТОП и категории; ABC — по этому набору, до фильтра ABC.
        var based = Rank(skus.Values.Where(s => TopOk(s) && CategoryOk(s)), scope, allMonths);
        var rows = based.Where(AbcOk).ToList();
        var national = scope < 0 ? based : Rank(skus.Values.Where(s => TopOk(s) && CategoryOk(s)), -1, allMonths);
        var nationalRows = national.Where(AbcOk).ToList();

        // Динамика — кг в день в первом и последнем квартале отрезка (если отрезок захватывает два квартала и больше).
        var quarterGroups = allMonths.GroupBy(i => (months[i].Year, Quarter: (months[i].Month - 1) / 3)).ToList();
        var first = quarterGroups.Count >= 2 ? quarterGroups[0].ToList() : null;
        var last = quarterGroups.Count >= 2 ? quarterGroups[^1].ToList() : null;
        var firstDays = first?.Sum(i => months[i].Days) ?? 0;
        var lastDays = last?.Sum(i => months[i].Days) ?? 0;
        SkuSalesQuarters? quarters = first is null || last is null
            ? null
            : new SkuSalesQuarters(QuarterLabel(months, first), QuarterLabel(months, last), firstDays, lastDays);

        var realRegions = Enumerable.Range(0, regionIds.Count).Where(r => regionIds[r] != SalesAnalytics.NoRegionId).ToList();
        var regionCount = skus.Values.ToDictionary(s => s.Id, s => realRegions.Count(r => s.KgOf(r, allMonths) > 0));
        var tableRows = rows.Select(x =>
        {
            decimal? firstPerDay = first is null ? null : SalesMath.Ratio(x.Sku.KgOf(scope, first), firstDays);
            decimal? lastPerDay = last is null ? null : SalesMath.Ratio(x.Sku.KgOf(scope, last), lastDays);
            // Динамика — только от положительной базы: отрицательное нетто первого квартала (возвраты больше продаж) не «рост со знаком».
            return new SkuSalesRow(x.Sku.Id, x.Sku.Name, x.Sku.Code, x.Sku.Category, x.Sku.IsTop, x.Rank, x.Kg, x.Revenue, PricePerKg(x.Revenue, x.Kg),
                x.Share, x.Cumulative, x.Abc, x.Akb, regionCount[x.Sku.Id], firstPerDay, lastPerDay,
                firstPerDay is > 0 && lastPerDay is { } l ? SalesMath.Delta(firstPerDay.Value, l) : null,
                firstPerDay is <= 0 && lastPerDay > 0m,
                allMonths.Select(m => x.Sku.KgOf(scope, [m])).ToList());
        }).ToList();

        var monthTotals = allMonths.Select(m =>
        {
            var kg = rows.Sum(x => x.Sku.KgOf(scope, [m]));
            return new SkuSalesMonthTotal(months[m].Year, months[m].Month, kg, SalesMath.Ratio(kg, months[m].Days), months[m].Partial);
        }).ToList();

        // Карточки категорий — как у эталона: фильтры региона, ТОП и ABC (ABC — внутри набора без фильтра категорий), без фильтра категорий
        // (иначе выбрав одну, на другую не переключиться). «Доля выборки» плиток — вес строк таблицы к весу этого же набора.
        var cardRows = Rank(skus.Values.Where(TopOk), scope, allMonths).Where(AbcOk).ToList();
        var cardKg = cardRows.Sum(x => x.Kg);
        var cardRevenue = cardRows.Sum(x => x.Revenue);
        var categories = cardRows.GroupBy(x => x.Sku.Category)
            .Select(g => new SkuSalesCategory(g.Key, g.Sum(x => x.Kg), g.Sum(x => x.Revenue), g.Count(x => x.Kg > 0), SalesMath.Ratio(g.Sum(x => x.Kg), cardKg),
                SalesMath.Ratio(g.Sum(x => x.Revenue), cardRevenue), PricePerKg(g.Sum(x => x.Revenue), g.Sum(x => x.Kg)), chosen.Contains(g.Key)))
            .Concat(selected.Where(c => cardRows.All(x => !string.Equals(x.Sku.Category, c, StringComparison.OrdinalIgnoreCase)))
                .Select(c => new SkuSalesCategory(c, 0, 0, 0, null, null, null, true)))
            .OrderByDescending(c => c.Kg)
            .ThenBy(c => c.Name)
            .ToList();

        // Плитки — по строкам таблицы.
        var kgTotal = rows.Sum(x => x.Kg);
        var revenueTotal = rows.Sum(x => x.Revenue);
        var totals = new SkuSalesTotals(kgTotal, revenueTotal, PricePerKg(revenueTotal, kgTotal), rows.Count(x => x.Kg > 0), rows.Count(x => x.Abc == AbcGroups.A),
            RegionsWithSales(rows, scope, realRegions, allMonths), SalesMath.Ratio(rows.OrderByDescending(x => x.Kg).Take(5).Sum(x => x.Kg), kgTotal),
            SalesMath.Ratio(kgTotal, cardKg));

        var regionNames = regionIds.Select(id => catalog.Regions.FirstOrDefault(r => r.Id == id)?.Name ?? "Без региона").ToList();
        return new SkuSalesView(
            from,
            to,
            dataThrough,
            months,
            filter.Region?.ToString(),
            scope >= 0 ? regionNames[scope] : null,
            filter.Top,
            filter.Abc,
            selected,
            catalog.Top.Configured,
            regionIds.Select((id, i) => new SkuSalesRegionRef(id.ToString(), regionNames[i])).ToList(),
            totals,
            categories,
            quarters,
            tableRows,
            monthTotals,
            RegionMatrix(nationalRows, regionIds.Count, allMonths),
            CategoryMatrix(nationalRows, regionIds.Count, allMonths),
            TopRegions(nationalRows, regionIds, regionNames, scope, allMonths),
            FactsOf(based, scope, realRegions, allMonths),
            outside.Count);
    }

    /// <summary>
    /// Ранжирование набора в охвате (регион или −1 — страна): SKU с продажами (кг или выручка не ноль) по убыванию кг; доля и накопленная
    /// доля кг — от итога набора; группа ABC — по накопленной доле (SalesMath.AbcOf).
    /// </summary>
    private static List<Ranked> Rank(IEnumerable<Sku> skus, int region, IReadOnlyList<int> months)
    {
        var list = skus.Select(s => (Sku: s, Kg: s.KgOf(region, months), Revenue: s.RevenueOf(region, months), Akb: region >= 0 ? s.RegionAkb[region] : s.Akb))
            .Where(x => x.Kg != 0 || x.Revenue != 0)
            .OrderByDescending(x => x.Kg)
            .ThenByDescending(x => x.Revenue)
            .ThenBy(x => x.Sku.Name, StringComparer.Ordinal)
            .ToList();
        var total = list.Sum(x => x.Kg);
        var cumulative = 0m;
        return list.Select((x, i) =>
        {
            cumulative += x.Kg;
            var share = SalesMath.Ratio(cumulative, total);
            return new Ranked(x.Sku, x.Kg, x.Revenue, x.Akb, i + 1, SalesMath.Ratio(x.Kg, total), share, SalesMath.AbcOf(share));
        }).ToList();
    }

    private static decimal? PricePerKg(decimal revenue, decimal kg) => kg > 0 ? revenue / kg : null;

    /// <summary>Регионов (без «Без региона»), где у строк набора кг больше нуля; у охвата одного региона — он сам (если там есть продажи).</summary>
    private static int RegionsWithSales(IReadOnlyList<Ranked> rows, int scope, IReadOnlyList<int> regions, IReadOnlyList<int> months) =>
        scope >= 0
            ? rows.Sum(x => x.Sku.KgOf(scope, months)) > 0 ? 1 : 0
            : regions.Count(r => rows.Sum(x => x.Sku.KgOf(r, months)) > 0);

    /// <summary>«I кв. 2026»; если отрезок или данные захватывают квартал не целиком — с месяцами: «IV кв. 2026: окт».</summary>
    private static string QuarterLabel(IReadOnlyList<SkuSalesMonth> months, IReadOnlyList<int> indexes)
    {
        var firstMonth = months[indexes[0]];
        var label = $"{Quarters[(firstMonth.Month - 1) / 3]} кв. {firstMonth.Year}";
        if (indexes.Count == 3 && indexes.All(i => !months[i].Partial))
        {
            return label;
        }

        var lastMonth = months[indexes[^1]];
        return indexes.Count == 1
            ? $"{label}: {MonthsShort[firstMonth.Month - 1]}"
            : $"{label}: {MonthsShort[firstMonth.Month - 1]}–{MonthsShort[lastMonth.Month - 1]}";
    }

    /// <summary>SKU × регионы: по каждому региону кг, выручка, АКБ SKU и доля SKU в весе региона (от суммы строк матрицы).</summary>
    private static SkuSalesRegionMatrix RegionMatrix(IReadOnlyList<Ranked> rows, int regions, IReadOnlyList<int> months)
    {
        var range = Enumerable.Range(0, regions).ToList();
        var regionKg = range.Select(r => rows.Sum(x => x.Sku.KgOf(r, months))).ToList();
        var regionRevenue = range.Select(r => rows.Sum(x => x.Sku.RevenueOf(r, months))).ToList();
        var kg = rows.Sum(x => x.Kg);
        var list = rows.Select(x =>
        {
            var byRegion = range.Select(r => x.Sku.KgOf(r, months)).ToList();
            return new SkuSalesRegionRow(x.Sku.Id, x.Sku.Name, x.Sku.Code, x.Sku.Category, x.Sku.IsTop, x.Abc, x.Kg, x.Revenue, SalesMath.Ratio(x.Kg, kg),
                byRegion, range.Select(r => x.Sku.RevenueOf(r, months)).ToList(), range.Select(r => x.Sku.RegionAkb[r]).ToList(),
                range.Select(r => byRegion[r] == 0 ? null : SalesMath.Ratio(byRegion[r], regionKg[r])).ToList());
        }).ToList();
        return new SkuSalesRegionMatrix(list, regionKg, regionRevenue, kg, rows.Sum(x => x.Revenue));
    }

    /// <summary>Категории × регионы: кг категории в регионе и её доля в весе региона; итог и доля во всём весе.</summary>
    private static SkuSalesCategoryMatrix CategoryMatrix(IReadOnlyList<Ranked> rows, int regions, IReadOnlyList<int> months)
    {
        var range = Enumerable.Range(0, regions).ToList();
        var regionKg = range.Select(r => rows.Sum(x => x.Sku.KgOf(r, months))).ToList();
        var kg = regionKg.Sum();
        var list = rows.GroupBy(x => x.Sku.Category)
            .Select(g =>
            {
                var byRegion = range.Select(r => g.Sum(x => x.Sku.KgOf(r, months))).ToList();
                var total = byRegion.Sum();
                return new SkuSalesCategoryRegionRow(g.Key, byRegion, range.Select(r => byRegion[r] == 0 ? null : SalesMath.Ratio(byRegion[r], regionKg[r])).ToList(),
                    total, SalesMath.Ratio(total, kg));
            })
            .OrderByDescending(c => c.TotalKg)
            .ToList();
        return new SkuSalesCategoryMatrix(list, regionKg, kg);
    }

    /// <summary>Топ-10 SKU каждого региона (или только выбранного) по весу; доля — от веса региона по строкам набора.</summary>
    private static List<SkuSalesTopRegion> TopRegions(IReadOnlyList<Ranked> rows, IReadOnlyList<Guid> regionIds, IReadOnlyList<string> names, int scope,
        IReadOnlyList<int> months) =>
        Enumerable.Range(0, regionIds.Count)
            .Where(r => scope < 0 || r == scope)
            .Select(r =>
            {
                var byRegion = rows.Select(x => (x.Sku, Kg: x.Sku.KgOf(r, months))).ToList();
                var total = byRegion.Sum(x => x.Kg);
                var items = byRegion.Where(x => x.Kg > 0)
                    .OrderByDescending(x => x.Kg)
                    .ThenBy(x => x.Sku.Name, StringComparer.Ordinal)
                    .Take(TopPerRegion)
                    .Select(x => new SkuSalesTopItem(x.Sku.Id, x.Sku.Name, x.Sku.IsTop, x.Kg, SalesMath.Ratio(x.Kg, total)))
                    .ToList();
                return new SkuSalesTopRegion(regionIds[r].ToString(), names[r], total, items);
            })
            .Where(x => x.Items.Count > 0)
            .OrderByDescending(x => x.Kg)
            .ToList();

    /// <summary>«Выводы» по набору охвата до фильтра ABC: главный SKU, доли топ-5 / 10 / 25, группы A, B, C.</summary>
    private static SkuSalesFacts FactsOf(IReadOnlyList<Ranked> rows, int scope, IReadOnlyList<int> regions, IReadOnlyList<int> months)
    {
        var kg = rows.Sum(x => x.Kg);
        var revenue = rows.Sum(x => x.Revenue);
        var top = rows.FirstOrDefault();
        decimal? TopShare(int n) => SalesMath.Ratio(rows.Take(n).Sum(x => x.Kg), kg);
        var groupC = rows.Where(x => x.Abc == AbcGroups.C).ToList();
        return new SkuSalesFacts(
            rows.Count(x => x.Kg > 0),
            kg,
            revenue,
            RegionsWithSales(rows, scope, regions, months),
            top?.Sku.Name,
            top?.Kg ?? 0,
            top?.Revenue ?? 0,
            top is null ? null : SalesMath.Ratio(top.Kg, kg),
            top is null ? null : SalesMath.Ratio(top.Revenue, revenue),
            TopShare(5),
            TopShare(10),
            TopShare(25),
            rows.Count(x => x.Abc == AbcGroups.A),
            rows.Count(x => x.Abc == AbcGroups.B),
            groupC.Count,
            SalesMath.Ratio(groupC.Sum(x => x.Kg), kg));
    }
}

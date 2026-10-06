using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales.SkuSales;

/// <summary>
/// Параметры «Продаж по SKU» из адреса: месяцы «ГГГГ-ММ» с … по … (по умолчанию — с января года последних данных по их месяц), регион,
/// ТОП (all | only | not), группа ABC (all | A | B | C), категории отчёта через запятую.
/// </summary>
public sealed record SkuSalesQuery(int FromYear, int FromMonth, int ToYear, int ToMonth, Guid? Region, string Top, string Abc, IReadOnlyList<string> Categories)
{
    /// <summary>Самый длинный отрезок — 24 месяца.</summary>
    public const int MaxMonths = 24;

    private static readonly Regex MonthFormat = new(@"^(\d{4})-(\d{1,2})$", RegexOptions.CultureInvariant);

    public string From => $"{FromYear:D4}-{FromMonth:D2}";

    public string To => $"{ToYear:D4}-{ToMonth:D2}";

    public string Key => $"{From}:{To}:{Region}:{Top}:{Abc}:{string.Join(",", Categories.Select(c => c.ToLowerInvariant()).Order(StringComparer.Ordinal))}";

    /// <summary>Разбор параметров; Error — текст для пользователя (ответ 400). latest — отчётный день (месяц последних данных).</summary>
    public static (SkuSalesQuery? Query, string? Error) Parse(string? from, string? to, string? region, string? top, string? abc, string? cat, DateOnly latest)
    {
        if (!TryMonth(to, out var end, (latest.Year, latest.Month)))
        {
            return (null, $"Конец периода «{to}» — месяц в формате ГГГГ-ММ, например {latest:yyyy-MM}.");
        }

        // Год дальше следующего за годом последних данных — опечатка, а не период: пустой отчёт за 2036 год не строим.
        if (end.Year > latest.Year + 1)
        {
            return (null, $"Период — до {latest:yyyy-MM}.");
        }

        if (!TryMonth(from, out var start, (end.Year, 1)))
        {
            return (null, $"Начало периода «{from}» — месяц в формате ГГГГ-ММ, например {end.Year:D4}-01.");
        }

        var length = (end.Year * 12 + end.Month) - (start.Year * 12 + start.Month) + 1;
        if (length < 1)
        {
            return (null, $"Начало периода ({start.Year:D4}-{start.Month:D2}) позже конца ({end.Year:D4}-{end.Month:D2}).");
        }

        if (length > MaxMonths)
        {
            return (null, $"Период — не длиннее {MaxMonths} месяцев (выбрано {length}).");
        }

        Guid? regionId = null;
        if (!string.IsNullOrWhiteSpace(region))
        {
            if (!Guid.TryParse(region.Trim(), out var parsed))
            {
                return (null, "Регион — идентификатор региона из списка регионов.");
            }

            regionId = parsed;
        }

        var topValue = top?.Trim().ToLowerInvariant() switch
        {
            SkuSalesTop.Only => SkuSalesTop.Only,
            SkuSalesTop.Not => SkuSalesTop.Not,
            _ => SkuSalesTop.All,
        };
        var abcValue = abc?.Trim().ToUpperInvariant() switch
        {
            AbcGroups.A => AbcGroups.A,
            AbcGroups.B => AbcGroups.B,
            AbcGroups.C => AbcGroups.C,
            _ => "all",
        };
        var categories = string.IsNullOrWhiteSpace(cat)
            ? []
            : cat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return (new SkuSalesQuery(start.Year, start.Month, end.Year, end.Month, regionId, topValue, abcValue, categories), null);
    }

    private static bool TryMonth(string? value, out (int Year, int Month) month, (int Year, int Month) fallback)
    {
        month = fallback;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var match = MonthFormat.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var m = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (year < 2000 || m is < 1 or > 12)
        {
            return false;
        }

        month = (year, m);
        return true;
    }
}

/// <summary>Ответ «Продаж по SKU»: страница или ошибка в параметрах (400).</summary>
public sealed record SkuSalesResult(SkuSalesView? View, string? Error);

/// <summary>Регионы «Продаж по SKU» (регионы вторички по названию) и филиал Linko → регион (старые филиалы — в текущем регионе).</summary>
public sealed record SkuSalesRegions(IReadOnlyList<Metrics.RegionInfo> Regions, IReadOnlyDictionary<long, Guid> BranchRegions);

/// <summary>
/// «Продажи по SKU» (DOC §9.6): вторичка по SKU за любой отрезок месяцев — новый SQL-агрегат ISalesHistoryReader.SkuSalesAsync по тем же
/// правилам, что вторичка месяца, и расчёт страницы в SkuSalesReport. Новых источников данных нет. Агрегат отрезка и страница
/// кэшируются (SalesViewCache) до следующей синхронизации; тяжёлый агрегат считается в один поток (RawGate).
/// </summary>
public sealed class SkuSalesService(IAppDbContext db, SalesOptions options, SalesDataLoader loader, ISalesHistoryReader history, IMemoryCache cache, SalesCacheSignal signal)
{
    /// <summary>
    /// Один SQL-агрегат за раз: после синхронизации кэш пуст, и несколько запросов одного отрезка (5–8 с каждый) считают его один раз —
    /// остальные дожидаются и берут из кэша.
    /// </summary>
    private static readonly SemaphoreSlim RawGate = new(1, 1);

    public async Task<SkuSalesResult> GetAsync(string? from, string? to, string? region, string? top, string? abc, string? cat, CancellationToken ct = default)
    {
        var lastData = await loader.LastDataAsync(ct);
        var (query, error) = SkuSalesQuery.Parse(from, to, region, top, abc, cat, lastData ?? DateOnly.FromDateTime(DateTime.Today));
        if (query is null)
        {
            return new SkuSalesResult(null, error);
        }

        var catalog = await SalesViewCache.GetAsync(cache, signal, "sales:sku-sales:catalog", () => CatalogAsync(ct));
        if (query.Region is { } regionId && catalog.Catalog.Regions.All(r => r.Id != regionId) && regionId != SalesAnalytics.NoRegionId)
        {
            return new SkuSalesResult(null, "Регион не найден.");
        }

        var view = await SalesViewCache.GetAsync(cache, signal, $"sales:sku-sales:{query.Key}:{lastData}", async () =>
        {
            var months = SkuSalesReport.MonthsOf(query.FromYear, query.FromMonth, query.ToYear, query.ToMonth, lastData);
            var aggregate = Window(query, lastData) is { } window ? await RawAsync(window.From, window.To, catalog.BranchRegions, ct) : SkuSalesAggregate.Empty;
            return SkuSalesReport.Build(query.From, query.To, months, lastData,
                new SkuSalesFilter(query.Region, query.Top, query.Abc, query.Categories), catalog.Catalog, aggregate);
        });
        return new SkuSalesResult(view, null);
    }

    /// <summary>
    /// Дни агрегата отрезка: с 1-го числа первого месяца по конец последнего, но не позже отчётного дня lastData (у идущего месяца —
    /// по него). null — отчётного дня нет или он раньше начала отрезка: данных за отрезок нет, агрегат не читается.
    /// </summary>
    public static (DateOnly From, DateOnly To)? Window(SkuSalesQuery query, DateOnly? lastData)
    {
        var start = new DateOnly(query.FromYear, query.FromMonth, 1);
        var monthEnd = new DateOnly(query.ToYear, query.ToMonth, 1).AddMonths(1).AddDays(-1);
        var end = lastData is { } last && last < monthEnd ? last : monthEnd;
        return lastData is null || end < start ? null : (start, end);
    }

    /// <summary>
    /// Регионы вторички из регионов OneBase (по одному на филиал Linko): без «Завода» и «К К Мерч» (Sales:ExcludedBranches, IgnoredBranches),
    /// старые филиалы («Андижон (эски) 2») — в текущем регионе, как во вторичке месяца (SalesStructure / OldBranches); регионы — по названию.
    /// </summary>
    public static SkuSalesRegions RegionsOf(IEnumerable<SalesRegion> regions, SalesOptions options)
    {
        var rows = regions.ToList();
        // Регион OneBase назван по филиалу Linko: «Завод» (экспорт и опт) и «К К Мерч» регионами вторички не являются.
        var skipped = rows.Where(r => options.IsExcludedBranch(r.Name) || options.IsIgnoredBranch(r.Name)).Select(r => r.LinkoBranchId).ToHashSet();
        var structure = SalesStructure.Build([], rows, skipped, options.OldBranchSuffix);
        var branchRegions = structure.Regions.ToDictionary(r => r.BranchId, r => r.Id);
        foreach (var (branch, regionId) in structure.BranchAliases)
        {
            branchRegions.TryAdd(branch, regionId);
        }

        return new SkuSalesRegions(structure.Regions.OrderBy(r => r.Name, StringComparer.CurrentCulture).ToList(), branchRegions);
    }

    /// <summary>Агрегат дней [from; to] из кэша; на промахе — один расчёт за раз (RawGate), после ожидания кэш проверяется снова.</summary>
    private async Task<SkuSalesAggregate> RawAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, Guid> branchRegions, CancellationToken ct)
    {
        var key = $"sales:sku-sales:raw:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}";
        if (cache.TryGetValue(key, out SkuSalesAggregate? cached) && cached is not null)
        {
            return cached;
        }

        await RawGate.WaitAsync(ct);
        try
        {
            return await SalesViewCache.GetAsync(cache, signal, key, () => history.SkuSalesAsync(from, to, branchRegions, ct)); // пока ждали, мог посчитать другой запрос
        }
        finally
        {
            RawGate.Release();
        }
    }

    private sealed record CatalogData(SkuSalesCatalog Catalog, IReadOnlyDictionary<long, Guid> BranchRegions);

    /// <summary>Справочники: товары, категории отчёта, регионы вторички (RegionsOf) и список ТОП.</summary>
    private async Task<CatalogData> CatalogAsync(CancellationToken ct)
    {
        var products = await db.LinkoProducts.AsNoTracking()
            .Select(p => new ProductInfo(p.Id, p.Name, p.Code, p.TypeId))
            .ToDictionaryAsync(p => p.Id, ct);
        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var regions = RegionsOf(await db.SalesRegions.AsNoTracking().ToListAsync(ct), options);
        var catalog = new SkuSalesCatalog(products, SalesCategories.Build(options.Categories, types), regions.Regions, TopProductSet.Of(options));
        return new CatalogData(catalog, regions.BranchRegions);
    }
}

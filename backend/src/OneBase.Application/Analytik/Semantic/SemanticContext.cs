using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>Точки-канал (Sales:ChannelRegions): виртуальный филиал, название региона-канала, точки, с какого месяца считается.</summary>
public sealed record ChannelSpec(long Branch, string Name, IReadOnlyList<long> Markets, DateOnly Since);

/// <summary>
/// Справочники движка — те же, что у страниц «Продаж»: оргструктура вторички (SalesStructure: регионы по филиалам Linko, старые филиалы
/// в текущем регионе, регионы-каналы), категории отчёта (SalesCategories по дереву типов Linko), исключённые и пропускаемые филиалы.
/// </summary>
public sealed record SemanticContext(
    SalesStructureData Structure,
    IReadOnlyDictionary<long, Guid> BranchRegion,
    IReadOnlyDictionary<long, string> BranchNames,
    IReadOnlyDictionary<Guid, RegionInfo> Regions,
    IReadOnlyDictionary<Guid, DirectionInfo> Directions,
    SalesCategories Categories,
    IReadOnlyDictionary<long, string> TypeNames,
    IReadOnlyList<ChannelSpec> Channels,
    IReadOnlyList<string> ExcludedLower,
    IReadOnlyList<string> IgnoredLower,
    IReadOnlyList<string> PrimaryLower)
{
    /// <summary>Группа (категория отчёта) типа Linko: настроенная категория (id меньше нуля) или сам тип.</summary>
    public long? GroupOf(long? type) => Categories.GroupOf(type);

    public string GroupName(long? group) => group is null ? "Без группы" : Categories.NameOf(group);

    /// <summary>Все группы, которые встречаются у типов Linko: категории отчёта и типы вне настройки.</summary>
    public IReadOnlyList<(long Id, string Name)> Groups =>
        TypeNames.Keys.Select(t => Categories.GroupOf(t)!.Value).Distinct().Select(g => (g, Categories.NameOf(g)))
            .OrderBy(g => g.Item2, StringComparer.CurrentCulture).ToList();
}

/// <summary>Справочники движка: кэш на 5 минут, сбрасывается с синхронизацией Linko и правкой «Настроек продаж» (SalesCacheSignal).</summary>
public sealed class SemanticContextLoader(IAppDbContext db, SalesOptions options, IMemoryCache cache, SalesCacheSignal signal)
{
    private const string CacheKey = "analytik:semantic:context";

    public async Task<SemanticContext> LoadAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out SemanticContext? cached) && cached is not null)
        {
            return cached;
        }

        var regions = await db.SalesRegions.AsNoTracking().ToListAsync(ct);
        var directions = await db.SalesDirections.AsNoTracking().ToListAsync(ct);
        // Регион OneBase назван по филиалу Linko: «Завод» (экспорт и опт) и «К К Мерч» регионами вторички не являются (как SkuSalesService.RegionsOf).
        var skipped = regions.Where(r => options.IsExcludedBranch(r.Name) || options.IsIgnoredBranch(r.Name)).Select(r => r.LinkoBranchId).ToHashSet();
        var structure = SalesStructure.Build(directions, regions, skipped, options.OldBranchSuffix, options.ChannelRegions);

        var branchRegion = structure.Regions.ToDictionary(r => r.BranchId, r => r.Id);
        foreach (var (branch, region) in structure.BranchAliases)
        {
            branchRegion.TryAdd(branch, region);
        }

        var branchNames = regions.GroupBy(r => r.LinkoBranchId).ToDictionary(g => g.Key, g => g.First().Name);
        var channels = new List<ChannelSpec>();
        for (var i = 0; i < options.ChannelRegions.Count; i++)
        {
            var c = options.ChannelRegions[i];
            var branch = SalesOptions.ChannelBranchId(i);
            branchNames[branch] = c.Name;
            if (c.Markets.Length > 0)
            {
                channels.Add(new ChannelSpec(branch, c.Name, c.Markets, c.SinceMonth() ?? DateOnly.MinValue));
            }
        }

        var types = await LinkoTypeTree.LoadAsync(db, ct);
        var lower = (IEnumerable<string> names) => names.Select(n => n.Trim().ToLowerInvariant()).Where(n => n.Length > 0).Distinct().ToList();
        var context = new SemanticContext(
            structure,
            branchRegion,
            branchNames,
            structure.Regions.ToDictionary(r => r.Id),
            structure.Directions.ToDictionary(d => d.Id),
            SalesCategories.Build(options.Categories, types),
            types.Names,
            channels,
            lower(options.ExcludedBranches),
            lower(options.IgnoredBranches),
            lower(options.PrimaryOrderBranches));
        cache.Set(CacheKey, context, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }.AddExpirationToken(signal.Token));
        return context;
    }
}

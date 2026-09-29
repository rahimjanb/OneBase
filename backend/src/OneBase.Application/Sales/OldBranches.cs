using System.Text.RegularExpressions;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales;

/// <summary>
/// Старые филиалы Linko («Жиззах (эски)», «Андижон (эски) 2») — тот же регион, что и текущий филиал с этим названием:
/// точки перенесли в новый филиал, а продажи прошлых месяцев остались на старом. Без объединения у региона
/// пустая история, а старый филиал висит отдельным «регионом».
/// </summary>
public static class OldBranches
{
    /// <param name="regions">Регионы OneBase (по одному филиалу Linko).</param>
    /// <param name="suffix">Окончание названия старого филиала (регулярное выражение); пусто — не объединять.</param>
    /// <returns>Регионы без старых филиалов и старый филиал → регион, в котором он считается.</returns>
    public static (List<RegionInfo> Regions, Dictionary<long, Guid> Aliases) Merge(IReadOnlyList<RegionInfo> regions, string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return (regions.ToList(), []);
        }

        var old = new Regex(suffix, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var current = regions
            .Where(r => !old.IsMatch(r.Name))
            .GroupBy(r => r.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var kept = new List<RegionInfo>();
        var aliases = new Dictionary<long, Guid>();
        foreach (var region in regions)
        {
            if (old.IsMatch(region.Name) && current.TryGetValue(old.Replace(region.Name, "").Trim(), out var target))
            {
                aliases[region.BranchId] = target.Id;
            }
            else
            {
                kept.Add(region);
            }
        }

        return (kept, aliases);
    }
}

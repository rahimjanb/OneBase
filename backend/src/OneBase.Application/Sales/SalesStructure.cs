using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales;

/// <summary>
/// Оргструктура вторички. RegionAliases — регион старого филиала → текущий регион, в котором он считается (для планов регионов);
/// BranchAliases — то же по филиалу Linko (для строк продаж).
/// </summary>
public sealed record SalesStructureData(
    IReadOnlyList<DirectionInfo> Directions,
    IReadOnlyList<RegionInfo> Regions,
    IReadOnlyDictionary<long, Guid> BranchAliases,
    IReadOnlyDictionary<Guid, Guid> RegionAliases)
{
    /// <summary>Регион вторички, в котором считается регион OneBase: он сам или текущий регион старого филиала; null — регион вне вторички.</summary>
    public Guid? RegionOf(Guid regionId) =>
        RegionAliases.TryGetValue(regionId, out var target) ? target
        : Regions.Any(r => r.Id == regionId) ? regionId
        : null;
}

/// <summary>
/// Оргструктура вторички — данные OneBase поверх филиалов Linko (в Linko их нет): направления — РМ 1–4 и каналы (sales."Directions"),
/// у региона — направление, СВР и дилер (sales."Regions"). Ведутся в «Настройках продаж».
/// </summary>
public static class SalesStructure
{
    /// <param name="directions">Направления OneBase.</param>
    /// <param name="regions">Регионы OneBase — по одному на филиал Linko.</param>
    /// <param name="skippedBranches">Филиалы вне вторички: «Завод» (у него отдельный блок) и «К К Мерч» (его во вторичке нет).</param>
    /// <param name="oldBranchSuffix">Окончание названия старого филиала (Sales:OldBranchSuffix): такой филиал считается в текущем регионе
    /// с тем же названием, его собственные направление, СВР и дилер не используются.</param>
    /// <param name="channels">Регионы-каналы (Sales:ChannelRegions): регион с виртуальным филиалом (−1, −2 …) и постоянным id из названия;
    /// направление — канал из «Настроек продаж» с тем же названием или новое (тоже с постоянным id).</param>
    public static SalesStructureData Build(
        IEnumerable<SalesDirection> directions,
        IEnumerable<SalesRegion> regions,
        IReadOnlySet<long> skippedBranches,
        string? oldBranchSuffix,
        IReadOnlyList<SalesChannelRegion>? channels = null)
    {
        var rows = regions.Where(r => !skippedBranches.Contains(r.LinkoBranchId)).ToList();
        var (kept, aliases) = OldBranches.Merge(
            rows.Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, r.DirectionId, Clean(r.SupervisorName), Clean(r.DealerName))).ToList(),
            oldBranchSuffix);

        var directionList = directions.Select(d => new DirectionInfo(d.Id, d.Name, d.Kind == DirectionKind.Channel, Clean(d.ManagerName), Clean(d.Description), d.SortOrder)).ToList();
        var regionList = kept.ToList();
        for (var i = 0; i < (channels?.Count ?? 0); i++)
        {
            var channel = channels![i];
            if (Clean(channel.Name) is not { } name)
            {
                continue;
            }

            Guid? directionId = null;
            if (Clean(channel.Direction) is { } directionName)
            {
                var direction = directionList.FirstOrDefault(d => string.Equals(d.Name.Trim(), directionName, StringComparison.OrdinalIgnoreCase));
                if (direction is null)
                {
                    direction = new DirectionInfo(StableId("sales-channel-direction:" + directionName.ToLowerInvariant()), directionName, true, null,
                        Clean(channel.Description), 1000 + i);
                    directionList.Add(direction);
                }

                directionId = direction.Id;
            }

            regionList.Add(new RegionInfo(StableId("sales-channel-region:" + name.ToLowerInvariant()), SalesOptions.ChannelBranchId(i), name, directionId, null, null));
        }

        return new SalesStructureData(
            directionList,
            regionList,
            aliases,
            rows.Where(r => aliases.ContainsKey(r.LinkoBranchId)).ToDictionary(r => r.Id, r => aliases[r.LinkoBranchId]));
    }

    /// <summary>Постоянный id из строки: у региона-канала нет строки в sales."Regions", а ссылки на его страницу не должны меняться.</summary>
    public static Guid StableId(string key) => new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(key)));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

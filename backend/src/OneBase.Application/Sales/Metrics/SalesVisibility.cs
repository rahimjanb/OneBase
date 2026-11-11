namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// Кого видно в списках раздела (охват пользователя, Access.SalesScope): вся организация; регионы (РМ — регионы своих направлений, СВР — свои);
/// один ТП. Списки «Агенты», «Планы», «KPI», «Проблемные агенты» считаются сразу по видимым людям — итоги совпадают с тем, что на странице,
/// и чужие цифры в них не попадают. Key — часть ключа кэша представления.
/// </summary>
public sealed record SalesVisibility(string Key, IReadOnlySet<Guid>? Regions, long? Agent)
{
    public static readonly SalesVisibility All = new("all", null, null);

    public bool IsAll => Regions is null && Agent is null;

    /// <summary>Регион целиком: вся организация или регион из охвата; ТП регион целиком не видит.</summary>
    public bool Region(Guid regionId) => Agent is null && (Regions is null || Regions.Contains(regionId));

    /// <summary>Человек (ТП, СВР) с регионом месяца: ТП — только себя, РМ и СВР — людей своих регионов.</summary>
    public bool Sees(long agentId, Guid regionId) => Agent is { } a ? a == agentId : Regions is null || Regions.Contains(regionId);

    public static SalesVisibility OfRegions(IEnumerable<Guid> regions)
    {
        var set = regions.ToHashSet();
        return new SalesVisibility($"r:{string.Join(',', set.Order())}", set, null);
    }

    public static SalesVisibility OfAgent(long agentId) => new($"a:{agentId}", null, agentId);
}

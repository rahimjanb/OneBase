namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// Вторичка месяца для раздела «Акции»: продажи регионов и агентов, отчётный день и АКБ «Могуль» (точки, купившие помадку) по регионам и агентам.
/// ElapsedDays — прошедшие дни месяца с долей неполного сегодняшнего (SalesMath.ElapsedDays): база «кг в день» идущего месяца.
/// </summary>
public sealed record PromoSalesBasis(
    int DataDay,
    IReadOnlyDictionary<string, decimal> RegionRevenue,
    IReadOnlyDictionary<long, decimal> AgentRevenue,
    IReadOnlyDictionary<long, Guid> AgentRegion,
    IReadOnlyDictionary<Guid, int> MogulByRegion,
    IReadOnlyDictionary<long, int> MogulByAgent,
    decimal? ElapsedDays = null);

public sealed partial class SalesAnalytics
{
    /// <summary>«Могуль» — цеховое название помадки: типы товаров «Помадка …».</summary>
    private bool IsMogul(SaleLine line) =>
        line.CategoryId is { } c && _d.Categories.TryGetValue(c, out var name) && name.StartsWith("Помадка", StringComparison.OrdinalIgnoreCase);

    public PromoSalesBasis PromoBasis() => View("promo-basis", () =>
    {
        var mogul = _d.Current.Where(l => l.MarketId != null && l.Kg > 0 && IsMogul(l)).ToList();
        return new PromoSalesBasis(
            _d.WorkedDays,
            // Вторичка регионов месяца, с регионами-каналами («Урикзор», «Сети») закрытого месяца — знаменатель «% к продажам».
            _d.Current.GroupBy(RegionOf).Where(g => _regions.ContainsKey(g.Key)).ToDictionary(g => _regions[g.Key].Name, g => g.Sum(l => l.Revenue)),
            _d.Current.Where(l => l.AgentId != null).GroupBy(l => l.AgentId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Revenue)),
            new Dictionary<long, Guid>(_agentRegion),
            mogul.GroupBy(RegionOf).ToDictionary(g => g.Key, g => g.Select(l => l.MarketId!.Value).Distinct().Count()),
            mogul.Where(l => l.AgentId != null).GroupBy(l => l.AgentId!.Value).ToDictionary(g => g.Key, g => g.Select(l => l.MarketId!.Value).Distinct().Count()),
            _elapsed);
    })!;
}

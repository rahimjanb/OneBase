namespace OneBase.Application.Sales;

/// <summary>
/// ТОП-товары — коды товаров Linko из Sales:TopProducts (список ТОП «Полевого контроля», 59 кодов), без учёта регистра и пробелов
/// по краям. Одно правило для ассортимента, «Продаж по SKU», остатка и аутстока: метка «ТОП» и фильтры «только ТОП / кроме ТОПа».
/// Пустой список — ТОП не настроен: меток нет.
/// </summary>
public sealed class TopProductSet
{
    private readonly HashSet<string> _codes;

    private TopProductSet(IEnumerable<string> codes) =>
        _codes = codes.Select(c => c.Trim()).Where(c => c.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static readonly TopProductSet Empty = new([]);

    public static TopProductSet From(IEnumerable<string>? codes) => new(codes ?? []);

    public static TopProductSet Of(SalesOptions options) => From(options.TopProducts);

    /// <summary>Список ТОП задан.</summary>
    public bool Configured => _codes.Count > 0;

    public int Count => _codes.Count;

    /// <summary>Товар с этим кодом Linko — в списке ТОП.</summary>
    public bool Contains(string? code) => code is { } c && _codes.Contains(c.Trim());
}

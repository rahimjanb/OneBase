using System.Globalization;
using System.Text.RegularExpressions;

namespace OneBase.Application.Sales.Stock;

/// <summary>Фасовка из названия товара: вес коробки и, если указано, сколько в ней штук и по сколько.</summary>
public sealed record PackInfo(decimal? BoxKg, int? PiecesPerBox, decimal? PieceKg);

/// <summary>Вес единицы учёта: откуда взят.</summary>
public static class WeightSources
{
    /// <summary>Σ total_weight ÷ Σ amount по строкам заказов — единственное место, где Linko даёт штуки и кг одной строкой.</summary>
    public const string Orders = "orders";

    /// <summary>Не нашлось ни продаж, ни проверяемой фасовки — в кг и коробки не пересчитывается.</summary>
    public const string None = "none";
}

/// <summary>
/// Остатки: Linko отдаёт их в ШТУКАХ (единицах учёта). Верно: кг = штуки × вес штуки; коробки = кг ÷ вес коробки.
/// Неверно: штуки × вес коробки — это завышает остаток во столько раз, сколько штук в коробке.
/// </summary>
public static partial class StockMath
{
    /// <summary>Вес единицы учёта по продажам; null — продаж не было (или без веса).</summary>
    public static decimal? UnitKg(decimal totalWeight, decimal amount) =>
        amount > 0 && totalWeight > 0 ? totalWeight / amount : null;

    public static decimal? Kg(decimal pieces, decimal? unitKg) => unitKg is { } u ? pieces * u : null;

    public static decimal? Boxes(decimal? kg, decimal? boxKg) => kg is { } k && boxKg is > 0 ? k / boxKg.Value : null;

    /// <summary>
    /// Коробка из названия подтверждается данными: в ней должно быть целое число единиц учёта — с точностью
    /// ±0,05 единицы (или 0,5% для больших коробок, где средний вес штуки по продажам даёт шум).
    /// Иначе название не совпадает с тем, как товар учитывается в Linko, и коробки не считаются.
    /// </summary>
    public static bool BoxConsistent(decimal boxKg, decimal unitKg)
    {
        if (boxKg <= 0 || unitKg <= 0)
        {
            return false;
        }

        var ratio = boxKg / unitKg;
        var n = Math.Round(ratio);
        return n >= 1 && Math.Abs(ratio - n) <= Math.Max(0.05m, 0.005m * n);
    }

    /// <summary>Остаток строкой из API («0.000000000») — как число; непустая строка не значит «есть товар».</summary>
    public static decimal? ParseQuantity(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>
    /// Фасовка из названия: «(4-шт по 0,5-кг) 2-кг», «(24шт по 40-гр ) 0,96-кг», «(10-шт по 300гр), 3-кг», «…, 3-кг», «2,5-кг», «3 кг».
    /// Вес коробки — последнее «N кг» в названии, а если его нет — штук × вес штуки из скобок.
    /// </summary>
    public static PackInfo? ParsePack(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        int? pieces = null;
        decimal? pieceKg = null;
        var inner = InnerPack().Match(name);
        if (inner.Success)
        {
            pieces = int.Parse(inner.Groups["n"].Value, CultureInfo.InvariantCulture);
            pieceKg = Number(inner.Groups["w"].Value) is { } w ? (inner.Groups["u"].Value.StartsWith("г", StringComparison.OrdinalIgnoreCase) ? w / 1000 : w) : null;
        }

        decimal? boxKg = null;
        var kgMatches = BoxKg().Matches(inner.Success ? name.Remove(inner.Index, inner.Length) : name);
        if (kgMatches.Count > 0)
        {
            boxKg = Number(kgMatches[^1].Groups["w"].Value);
        }
        else if (pieces is { } p && pieceKg is { } pk)
        {
            boxKg = p * pk;
        }

        return boxKg is null && pieceKg is null ? null : new PackInfo(boxKg, pieces, pieceKg);
    }

    private static decimal? Number(string s) =>
        decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : null;

    [GeneratedRegex(@"\(\s*(?<n>\d+)\s*-?\s*шт\.?\s*по\s*(?<w>\d+(?:[.,]\d+)?)\s*-?\s*(?<u>кг|гр|г)\b[^)]*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InnerPack();

    [GeneratedRegex(@"(?<w>\d+(?:[.,]\d+)?)\s*-?\s*кг\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BoxKg();
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace OneBase.Application.Sales.Stock;

/// <summary>Фасовка из названия товара: вес коробки и, если указано, сколько в ней штук и по сколько.</summary>
public sealed record PackInfo(decimal? BoxKg, int? PiecesPerBox, decimal? PieceKg);

/// <summary>Вес единицы учёта: откуда взят.</summary>
public static class WeightSources
{
    /// <summary>Σ total_weight ÷ Σ amount по строкам заказов с 1-го числа последнего закрытого месяца — единственное место, где Linko даёт штуки и кг одной строкой.</summary>
    public const string Orders = "orders";

    /// <summary>Свежих продаж нет — тот же расчёт по строкам заказов за год.</summary>
    public const string OrdersYear = "ordersYear";

    /// <summary>Продаж за год не было — вес единицы взят из фасовки в названии («(8-шт по 0,5-кг)»); кг и коробки — оценка (≈).</summary>
    public const string Name = "name";

    /// <summary>Не нашлось ни продаж, ни проверяемой фасовки — в кг и коробки не пересчитывается.</summary>
    public const string None = "none";
}

/// <summary>
/// Остатки: Linko отдаёт их в ШТУКАХ (единицах учёта). Верно: кг = штуки × вес штуки; коробки = кг ÷ вес коробки.
/// Неверно: штуки × вес коробки — это завышает остаток во столько раз, сколько штук в коробке.
/// </summary>
public static partial class StockMath
{
    /// <summary>Горизонт рекомендуемого заказа дилера: довести остаток до запаса на столько дней.</summary>
    public const int OrderCoverDays = 15;

    /// <summary>Дни в нуле за закрытый месяц, которые вычитаются из базы скорости, — не больше этого (знаменатель ≥ 87 − 30 = 57).</summary>
    public const int MaxZeroDays = 30;

    /// <summary>Коробка из одной единицы учёта (в названии только вес самой единицы) считается коробкой, если единица не легче этого веса: блок 1,5 кг — да, пачка 0,18 кг — нет.</summary>
    public const decimal MinSingleUnitBoxKg = 1m;

    /// <summary>Строк отгрузок завода, по которым коробка опознаётся (меньше — случайность).</summary>
    public const int MinShipmentLines = 5;

    /// <summary>Доля строк отгрузок, кратных коробке: одна-две нестандартные строки (довесок, образцы) коробку не ломают.</summary>
    public const decimal ShipmentBoxShare = 0.9m;

    /// <summary>Вес единицы учёта по продажам; null — продаж не было (или без веса).</summary>
    public static decimal? UnitKg(decimal totalWeight, decimal amount) =>
        amount > 0 && totalWeight > 0 ? totalWeight / amount : null;

    public static decimal? Kg(decimal pieces, decimal? unitKg) => unitKg is { } u ? pieces * u : null;

    public static decimal? Boxes(decimal? kg, decimal? boxKg) => kg is { } k && boxKg is > 0 ? k / boxKg.Value : null;

    /// <summary>Весовой товар: единица учёта — килограмм.</summary>
    public static bool IsByWeight(decimal unitKg) => Math.Abs(unitKg - 1) < 0.001m;

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

    /// <summary>
    /// Одно правило коробки для остатка и первички: у весового товара (единица — 1 кг) принимается любой вес коробки из названия,
    /// у штучного — только если в коробке целое число единиц. Название с одним весом, равным весу единицы («…, 0.18-кг» при пачке 0,18 кг),
    /// называет пачку, а не коробку: такая «коробка» из одной единицы принимается, только если единица не легче <see cref="MinSingleUnitBoxKg"/>.
    /// Когда название коробки не даёт, коробка берётся по отгрузкам завода (shipmentUnits — единиц в коробке по
    /// <see cref="BoxUnitsFromShipments"/>): 493 «0.18-кг» завод отгружает по 10 пачек — коробка 1,8 кг. Коробка из одной единицы
    /// по отгрузкам — по тому же правилу, что из названия (шоубокс 0,5 кг — не коробка, блок 1 кг — коробка).
    /// </summary>
    public static (decimal? BoxKg, string Note) BoxWeight(PackInfo? pack, decimal? unitKg, decimal? shipmentUnits = null)
    {
        var (box, note) = BoxFromName(pack, unitKg);
        if (box is not null || unitKg is not { } unit || shipmentUnits is not { } units || units <= 0)
        {
            return (box, note);
        }

        return units >= 2 || unit >= MinSingleUnitBoxKg
            ? (units * unit, $"по отгрузкам завода: {units:0.###} × {unit:0.###} кг = {units * unit:0.###} кг")
            : (null, $"{note}; завод отгружает по одной единице");
    }

    private static (decimal? BoxKg, string Note) BoxFromName(PackInfo? pack, decimal? unitKg)
    {
        if (pack?.BoxKg is not { } box)
        {
            return (null, "в названии нет веса коробки");
        }

        if (unitKg is not { } unit)
        {
            return (null, "нет продаж — вес штуки неизвестен");
        }

        if (IsByWeight(unit))
        {
            return (box, $"по названию: {box:0.###} кг, весовой товар");
        }

        if (!BoxConsistent(box, unit))
        {
            return (null, $"в названии {box:0.###} кг — не делится на вес штуки {unit:0.###} кг, коробки не считаются");
        }

        var n = Math.Round(box / unit);
        return n == 1 && box < MinSingleUnitBoxKg
            ? (null, $"в названии вес единицы {box:0.###} кг, а не коробки — коробки не считаются")
            : (box, $"по названию: {box:0.###} кг = {n:0} × {unit:0.###} кг");
    }

    /// <summary>
    /// Единиц учёта в коробке по отгрузкам завода: завод отгружает целыми коробками, поэтому количества строк перемещений кратны коробке
    /// (в таблице первички «Полевого контроля» коробки стоят прямо в строке отгрузки; в Linko их нет — только количество). Берётся
    /// наибольший делитель, которому кратны не меньше <see cref="ShipmentBoxShare"/> строк: нестандартная строка его не ломает.
    /// У весового товара количества в кг, и делитель может быть дробным (2,5 кг у KEKO). Строк меньше <see cref="MinShipmentLines"/> — null.
    /// </summary>
    public static decimal? BoxUnitsFromShipments(IReadOnlyCollection<decimal> amounts)
    {
        var scaled = amounts.Where(a => a > 0).Select(a => (long)Math.Round(a * 1000)).Where(a => a > 0).ToList();
        if (scaled.Count < MinShipmentLines)
        {
            return null;
        }

        // Кандидаты — сами количества, их попарные общие делители и общий делитель всех строк.
        var distinct = scaled.Distinct().OrderByDescending(a => a).Take(200).ToList();
        var candidates = new HashSet<long>(distinct) { distinct.Aggregate(Gcd) };
        for (var i = 0; i < distinct.Count; i++)
        {
            for (var j = i + 1; j < distinct.Count; j++)
            {
                candidates.Add(Gcd(distinct[i], distinct[j]));
            }
        }

        var needed = Math.Ceiling(scaled.Count * ShipmentBoxShare);
        foreach (var d in candidates.OrderByDescending(c => c))
        {
            if (scaled.Count(a => a % d == 0) >= needed)
            {
                return d / 1000m;
            }
        }

        return null;
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }

    /// <summary>Деньги за вес: кг ÷ вес единицы × цена единицы — цена в прайсе стоит за единицу учёта, а не за килограмм.</summary>
    public static decimal? SumOfKg(decimal? kg, decimal? unitKg, decimal? price) =>
        kg is { } k && unitKg is > 0 && price is { } p ? k / unitKg.Value * p : null;

    /// <summary>Остаток строкой из API («0.000000000») — как число; непустая строка не значит «есть товар».</summary>
    public static decimal? ParseQuantity(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>
    /// Фасовка из названия: «(4-шт по 0,5-кг) 2-кг», «(24шт по 40-гр ) 0,96-кг», «(10-шт по 300гр), 3-кг», «(180гр 12шт)», «(200г / 20шт)»,
    /// «( 400г ) 10 шт.», «…, 3-кг», «2,5-кг», «3 кг». Вес коробки — последнее «N кг» в названии, а если его нет — штук × вес штуки из скобок.
    /// </summary>
    public static PackInfo? ParsePack(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        int? pieces = null;
        decimal? pieceKg = null;
        var rest = name;
        var inner = InnerPack().Match(name);
        if (inner.Success)
        {
            pieces = int.Parse(inner.Groups["n"].Value, CultureInfo.InvariantCulture);
            pieceKg = Number(inner.Groups["w"].Value) is { } w ? (inner.Groups["u"].Value.StartsWith("г", StringComparison.OrdinalIgnoreCase) ? w / 1000 : w) : null;
            rest = name.Remove(inner.Index, inner.Length);
        }
        else
        {
            var grams = GramsTimesPieces().Match(name);
            if (grams.Success)
            {
                pieces = int.Parse(grams.Groups["n"].Value, CultureInfo.InvariantCulture);
                pieceKg = Number(grams.Groups["w"].Value) is { } g ? g / 1000 : null;
                rest = name.Remove(grams.Index, grams.Length);
            }
        }

        decimal? boxKg = null;
        var kgMatches = BoxKg().Matches(rest);
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

    /// <summary>
    /// Фасовка для фильтра — последний вес в названии (кг или граммы → кг): у весового это блок («ROHAT BAMBUK, 3-кг»),
    /// у штучного — пачка («180гр 12шт» → 0,18). Это заявленный вес из названия, а не вес коробки.
    /// </summary>
    public static decimal? PackKg(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var matches = AnyWeight().Matches(name);
        if (matches.Count == 0)
        {
            return null;
        }

        var last = matches[^1];
        if (Number(last.Groups["w"].Value) is not { } w)
        {
            return null;
        }

        var unit = last.Groups["u"].Value.ToLowerInvariant();
        var kg = unit is "кг" or "kg" ? w : w / 1000;
        return Math.Round(kg, 4);
    }

    /// <summary>Числовой ключ кода: цифры без нулей слева («002» → «2»); null — код не из одних цифр.</summary>
    public static string? NumericKey(string? code)
    {
        var c = code?.Trim();
        if (string.IsNullOrEmpty(c) || !c.All(char.IsAsciiDigit))
        {
            return null;
        }

        var key = c.TrimStart('0');
        return key.Length == 0 ? "0" : key;
    }

    /// <summary>
    /// Поиск по товарам: «002» или «2» — точное совпадение кода (нули слева не важны), иначе точное совпадение кода без учёта регистра;
    /// если такого кода нет — подстрока в коде и названии («keko», «187-1»). Пустой запрос — все строки.
    /// </summary>
    public static IReadOnlyList<T> Search<T>(IReadOnlyList<T> rows, string? query, Func<T, string?> code, Func<T, string> name)
    {
        var q = query?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(q))
        {
            return rows;
        }

        var numeric = NumericKey(q);
        var exact = rows.Where(r => string.Equals(code(r)?.Trim(), q, StringComparison.OrdinalIgnoreCase) || (numeric is not null && NumericKey(code(r)) == numeric)).ToList();
        return exact.Count > 0
            ? exact
            : rows.Where(r => (code(r) ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) || name(r).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Скорость с поправкой на аутсток: продажи за базу ÷ (дней базы − дней в нуле). Дни в нуле — не больше <see cref="MaxZeroDays"/>
    /// и не больше дней базы − 1, чтобы скорость не «взрывалась» на почти пустом товаре. Возвращает и зачтённые дни в нуле.
    /// </summary>
    public static (decimal KgPerDay, int ZeroDays) CorrectedKgPerDay(decimal baseKg, int baseDays, int zeroDays)
    {
        if (baseDays <= 0 || baseKg <= 0)
        {
            return (0, 0); // продаж за базу нет (или возвратов больше) — поправлять нечего
        }

        var z = Math.Max(0, Math.Min(zeroDays, Math.Min(MaxZeroDays, baseDays - 1)));
        return (baseKg / (baseDays - z), z);
    }

    /// <summary>
    /// Рекомендуемый заказ дилера, кг: довести остаток до запаса на coverDays по скорости с поправкой, вверх до целой коробки.
    /// Хватает (нужно ≤ 0,0001 кг) — 0; вес коробки неизвестен — ровно сколько не хватает.
    /// </summary>
    public static decimal OrderKg(decimal kgPerDay, decimal stockKg, decimal? boxKg, int coverDays = OrderCoverDays)
    {
        var need = kgPerDay * coverDays - stockKg;
        if (need <= 0.0001m)
        {
            return 0;
        }

        return boxKg is > 0 ? Math.Ceiling(need / boxKg.Value - 0.000000001m) * boxKg.Value : need;
    }

    /// <summary>Заказ у завода: заказы всех дилеров, которые его остаток не покрывает. Своего запаса «на 15 дней» у завода нет.</summary>
    public static decimal FactoryOrderKg(decimal dealerOrdersKg, decimal factoryKg) => Math.Max(0, dealerOrdersKg - factoryKg);

    /// <summary>Последний закрытый месяц на дату снимка — месяц перед месяцем снимка.</summary>
    public static (int Year, int Month) ClosedMonth(DateOnly snapshotDate)
    {
        var first = new DateOnly(snapshotDate.Year, snapshotDate.Month, 1).AddMonths(-1);
        return (first.Year, first.Month);
    }

    /// <summary>
    /// Окно базы скорости: days дней до последнего полного дня данных — вчера (отчётный день) или последней приёмки не позже вчера.
    /// </summary>
    public static (DateOnly From, DateOnly To) VelocityWindow(DateOnly reportCutoff, DateOnly? lastAccepted, int days)
    {
        var to = lastAccepted is { } last && last < reportCutoff ? last : reportCutoff;
        return (to.AddDays(-(Math.Max(1, days) - 1)), to);
    }

    private static decimal? Number(string s) =>
        decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : null;

    [GeneratedRegex(@"\(\s*(?<n>\d+)\s*-?\s*шт\.?\s*по\s*(?<w>\d+(?:[.,]\d+)?)\s*-?\s*(?<u>кг|гр|г)\b[^)]*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InnerPack();

    /// <summary>«180гр 12шт», «200г / 20шт», «400г ) 10 шт», «250гр × 9шт» — граммы и штук в коробке; между ними только знак умножения, косая или скобки.</summary>
    [GeneratedRegex(@"(?<w>\d+(?:[.,]\d+)?)\s*-?\s*(?:гр|г)\b\s*[()]?\s*[×x/]?\s*[()]?\s*(?<n>\d+)\s*-?\s*шт", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GramsTimesPieces();

    [GeneratedRegex(@"(?<w>\d+(?:[.,]\d+)?)\s*-?\s*кг\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BoxKg();

    [GeneratedRegex(@"(?<w>\d+(?:[.,]\d+)?)\s*[-–]?\s*(?<u>кг|kg|гр|gr|г|g)(?![а-яa-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnyWeight();
}

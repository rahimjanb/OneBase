namespace OneBase.Application.Sales.Tajik;

/// <summary>Товар 1С Таджикистана для расчёта веса: цена единицы в сомони и, если нашёлся товар Linko, его цена за кг в сумах и вес штуки.</summary>
public sealed record TajikWeightInput(string Code, string Name, decimal? TajikUnitPrice, decimal? LinkoPricePerKg, decimal? LinkoUnitKg);

/// <summary>Вес единицы 1С и откуда он взят: name — из названия; box/piece — коробка или штука по цене; linko — вес единицы Linko; none — неизвестен.</summary>
public sealed record TajikUnitWeight(decimal? Kg, string Source);

/// <summary>
/// Вес единицы 1С Таджикистана. «Азалия 1,5 кг» — коробка 1,5 кг (вес из названия). «420гр*8шт» и «200г*20шт» — двояко: у помадки единица 1С —
/// коробка из 8 штук (128 сомони), у Twizos — пачка 200 г (15,1 сомони). Какая из двух — решает цена: цена за кг в сомони, делённая на цену
/// за кг того же товара в Linko (сумы), у верного варианта близка к тому же отношению у товаров с однозначным весом (медиана). Нет цены Linko —
/// штука (меньший вес), с пометкой «неоднозначно».
/// </summary>
public static class TajikWeights
{
    public const string FromName = "name";
    public const string Box = "box";
    public const string Piece = "piece";
    public const string Ambiguous = "ambiguous";
    public const string FromLinko = "linko";
    public const string Unknown = "none";

    public static IReadOnlyDictionary<string, TajikUnitWeight> Resolve(IEnumerable<TajikWeightInput> products)
    {
        var list = products.ToList();
        var parsed = list.ToDictionary(p => p.Code, p => TajikText.WeightOf(p.Name));

        // Отношение «сомони за кг / сум за кг» у товаров с однозначным весом — эталон для выбора между коробкой и штукой.
        var ratios = list
            .Where(p => parsed[p.Code].Single is > 0 && p.TajikUnitPrice is > 0 && p.LinkoPricePerKg is > 0)
            .Select(p => p.TajikUnitPrice!.Value / parsed[p.Code].Single!.Value / p.LinkoPricePerKg!.Value)
            .OrderBy(r => r)
            .ToList();
        decimal? reference = ratios.Count == 0 ? null : ratios[ratios.Count / 2];

        var result = new Dictionary<string, TajikUnitWeight>(StringComparer.Ordinal);
        foreach (var p in list)
        {
            var (single, piece, count) = parsed[p.Code];
            if (single is > 0)
            {
                result[p.Code] = new TajikUnitWeight(single, FromName);
            }
            else if (piece is > 0 && count is > 0)
            {
                var box = piece.Value * count.Value;
                if (reference is > 0 && p.TajikUnitPrice is > 0 && p.LinkoPricePerKg is > 0)
                {
                    var asPiece = Distance(p.TajikUnitPrice.Value / piece.Value / p.LinkoPricePerKg.Value, reference.Value);
                    var asBox = Distance(p.TajikUnitPrice.Value / box / p.LinkoPricePerKg.Value, reference.Value);
                    result[p.Code] = asBox < asPiece ? new TajikUnitWeight(box, Box) : new TajikUnitWeight(piece, Piece);
                }
                else
                {
                    result[p.Code] = new TajikUnitWeight(piece, Ambiguous);
                }
            }
            else if (p.LinkoUnitKg is > 0)
            {
                result[p.Code] = new TajikUnitWeight(p.LinkoUnitKg, FromLinko);
            }
            else
            {
                result[p.Code] = new TajikUnitWeight(null, Unknown);
            }
        }

        return result;
    }

    /// <summary>Во сколько раз отношение отличается от эталона (в любую сторону), логарифмом.</summary>
    private static double Distance(decimal ratio, decimal reference) => Math.Abs(Math.Log((double)(ratio / reference)));
}

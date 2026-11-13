namespace OneBase.Application.Analytik.Semantic;

/// <summary>
/// Исправление отклонённого среза: подсказка, исправленный запрос (или несколько — если срез нужно разделить) и можно ли выполнить
/// исправление сразу (Safe — смысл вопроса не меняется: опечатка в коде показателя, план только помесячный).
/// </summary>
public sealed record RepairSuggestion(string Hint, IReadOnlyList<AnalyticsQuery> Queries, bool Safe);

/// <summary>
/// Query Repair: по отказу движка срезов (AnalyticsQueryException) строит исправленный запрос детерминированно — без модели. План по дням →
/// по месяцам; несовместимые показатели или фильтры → отдельные срезы по фактам; остатки → отдельный срез без дат; слишком длинный период →
/// части; тяжёлый срез → шаг крупнее или без самого подробного измерения; опечатка в коде показателя или измерения → ближайший код. Модель
/// получает подсказку и готовый запрос, а не повторяет тот же неудачный вызов.
/// </summary>
public static class QueryRepair
{
    private static readonly string[] Finer = ["market", "agent", "product", "sku", "branch"];

    public static RepairSuggestion? Suggest(AnalyticsQuery q, AnalyticsQueryException ex)
    {
        var message = ex.Message;
        var dims = (q.Dimensions ?? []).Select(d => SemanticCatalog.Dimension(d)?.Code ?? d.Trim().ToLowerInvariant()).ToList();
        var metrics = q.Metrics.Select(m => m.Trim().ToLowerInvariant()).ToList();
        switch (ex.Code)
        {
            case "unsupported" when message.Contains("помесячный", StringComparison.Ordinal):
            {
                var from = q.DateFrom is { } f ? SemanticEngine.Trunc(f, "month") : (DateOnly?)null;
                var to = q.DateTo is { } t ? SemanticEngine.Trunc(t, "month").AddMonths(1).AddDays(-1) : (DateOnly?)null;
                return new RepairSuggestion("План есть только по месяцам: тот же срез с шагом month (план, факт и выполнение по месяцам).",
                    [q with { Grain = "month", DateFrom = from, DateTo = to }], true);
            }

            case "unsupported" when message.Contains("отдельным срезом", StringComparison.Ordinal) || message.Contains("нельзя разложить", StringComparison.Ordinal)
                                    || message.Contains("нельзя отфильтровать", StringComparison.Ordinal):
                return SplitByFact(q, metrics, dims);

            case "too_heavy" when message.Contains("Период длиннее", StringComparison.Ordinal) && q.DateFrom is { } pf && q.DateTo is { } pt:
            {
                var parts = new List<AnalyticsQuery>();
                for (var start = pf; start <= pt; start = start.AddDays(366))
                {
                    var end = start.AddDays(365) < pt ? start.AddDays(365) : pt;
                    parts.Add(q with { DateFrom = start, DateTo = end });
                }

                return new RepairSuggestion($"Период длиннее {SemanticEngine.MaxDays} дней: тот же срез частями по году — итоги сложи calculate.", parts, false);
            }

            case "too_heavy" or "timeout":
                return Lighter(q, dims);

            case "invalid" when message.StartsWith("Показатель «", StringComparison.Ordinal):
            {
                var fixedMetrics = metrics.Select(m => SemanticCatalog.Metric(m)?.Code ?? Nearest(m, SemanticCatalog.Metrics.Select(x => x.Code))).ToList();
                return fixedMetrics.Any(m => m is null)
                    ? new RepairSuggestion("Неизвестный показатель — выбери код из списка в сообщении.", [], false)
                    : new RepairSuggestion($"Показатели: {string.Join(", ", fixedMetrics)}.", [q with { Metrics = fixedMetrics.OfType<string>().Distinct().ToList() }], true);
            }

            case "invalid" when message.StartsWith("Измерение «", StringComparison.Ordinal):
            {
                var fixedDims = dims.Select(d => SemanticCatalog.Dimension(d)?.Code ?? Nearest(d, SemanticCatalog.Dimensions.SelectMany(x => (x.Aliases ?? []).Append(x.Code))))
                    .Select(d => d is null ? null : SemanticCatalog.Dimension(d)?.Code).ToList();
                return fixedDims.Any(d => d is null)
                    ? new RepairSuggestion("Неизвестное измерение — выбери код из списка в сообщении.", [], false)
                    : new RepairSuggestion($"Измерения: {string.Join(", ", fixedDims)}.", [q with { Dimensions = fixedDims.OfType<string>().Distinct().ToList() }], true);
            }

            case "invalid" when message.StartsWith("Не больше 12 показателей", StringComparison.Ordinal):
                return new RepairSuggestion("Показатели — частями по 12 в срезе.", metrics.Chunk(12).Select(c => q with { Metrics = c.ToList() }).ToList(), false);

            case "invalid" when message.StartsWith("Не больше 5 измерений", StringComparison.Ordinal):
                return new RepairSuggestion("Оставь 5 главных измерений, остальное — фильтром по значению.", [q with { Dimensions = dims.Take(5).ToList() }], false);

            case "invalid" when message.Contains("Варианты:", StringComparison.Ordinal) || message.Contains("не найдено", StringComparison.Ordinal):
                return new RepairSuggestion("Значение фильтра не найдено: возьми название из вариантов сообщения или найди его справочником (find_products, get_store, " +
                                            "get_agent), затем повтори срез.", [], false);

            default:
                return null;
        }
    }

    /// <summary>Несовместимые показатели и измерения — отдельные срезы: каждый факт со своими допустимыми измерениями и фильтрами.</summary>
    private static RepairSuggestion? SplitByFact(AnalyticsQuery q, IReadOnlyList<string> metrics, IReadOnlyList<string> dims)
    {
        var groups = metrics.Select(m => SemanticCatalog.Metric(m)).OfType<MetricDef>()
            .GroupBy(m => SemanticCatalog.FactsOf([m]).OrderBy(f => f, StringComparer.Ordinal).First()).ToList();
        if (groups.Count == 0)
        {
            return null;
        }

        var queries = new List<AnalyticsQuery>();
        var dropped = new List<string>();
        foreach (var g in groups)
        {
            var fact = g.Key;
            var okDims = dims.Where(d => SemanticCatalog.Dimension(d) is { } def && def.Facts.Contains(fact)).ToList();
            dropped.AddRange(dims.Except(okDims).Select(d => $"{SemanticCatalog.Metric(g.First().Code)!.Title} без «{SemanticCatalog.Dimension(d)?.Title ?? d}»"));
            var filters = (q.Filters ?? []).Where(f => SemanticCatalog.Dimension(f.Dimension) is { } def && def.Facts.Contains(fact)).ToList();
            var stock = fact == SemanticCatalog.Stock;
            queries.Add(q with
            {
                Metrics = g.Select(m => m.Code).ToList(), Dimensions = okDims.Where(d => !stock || d != "date").ToList(), Filters = filters,
                Compare = stock ? null : q.Compare, Grain = stock ? null : q.Grain,
            });
        }

        return queries.Count <= 1 && dropped.Count == 0
            ? null
            : new RepairSuggestion("Показатели разных фактов — отдельными срезами" + (dropped.Count > 0 ? $" ({string.Join("; ", dropped.Distinct())})" : "") +
                                   ". Связь между ними — по общему измерению (регион, дата).", queries, false);
    }

    /// <summary>Тяжёлый срез: шаг крупнее (день → неделя → месяц), иначе без самого подробного измерения (точка, агент, товар), иначе половина периода.</summary>
    private static RepairSuggestion Lighter(AnalyticsQuery q, IReadOnlyList<string> dims)
    {
        if (dims.Contains("date") && (q.Grain ?? "day") is "day" or "week")
        {
            var grain = (q.Grain ?? "day") == "day" ? "week" : "month";
            return new RepairSuggestion($"Срез тяжёлый: тот же срез с шагом {grain}.", [q with { Grain = grain }], false);
        }

        if (Finer.FirstOrDefault(dims.Contains) is { } fine)
        {
            return new RepairSuggestion($"Срез тяжёлый: без измерения «{SemanticCatalog.Dimension(fine)!.Title}» (или с фильтром по региону) и топом через sort/limit.",
                [q with { Dimensions = dims.Where(d => d != fine).ToList() }], false);
        }

        if (q.DateFrom is { } f && q.DateTo is { } t && t.DayNumber - f.DayNumber > 30)
        {
            var mid = f.AddDays((t.DayNumber - f.DayNumber) / 2);
            return new RepairSuggestion("Срез тяжёлый: период частями.", [q with { DateTo = mid }, q with { DateFrom = mid.AddDays(1) }], false);
        }

        return new RepairSuggestion("Срез тяжёлый: сузьте фильтром (регион, группа) или уберите измерение.", [], false);
    }

    /// <summary>Ближайший код (расстояние Левенштейна ≤ 2 и единственный такой) или null.</summary>
    public static string? Nearest(string value, IEnumerable<string> candidates)
    {
        var v = value.Trim().ToLowerInvariant();
        var scored = candidates.Distinct().Select(c => (Code: c, Distance: Levenshtein(v, c.ToLowerInvariant()))).Where(x => x.Distance <= 2).OrderBy(x => x.Distance).ToList();
        return scored.Count == 0 || (scored.Count > 1 && scored[0].Distance == scored[1].Distance) ? null : scored[0].Code;
    }

    private static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }
        }

        return d[a.Length, b.Length];
    }
}

using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>
/// Названия ↔ id для движка: фильтры принимают id или названия («Самарканд», «Кекс», имя агента, часть названия товара) — сервер сопоставляет
/// их сам; совпадений нет — ошибка со списком вариантов (модель исправит запрос), а не пустой результат. Подписи строк результата — отсюда же.
/// </summary>
public sealed class SemanticResolver(IAppDbContext db)
{
    public const string NoValue = "∅";
    private const int MaxMatches = 300;

    private static readonly string[] NullWords = ["без региона", "без группы", "без агента", "без точки", "без товара", "не задан", "нет", "none", "null", NoValue];

    public async Task<ResolvedFilter> ResolveAsync(AnalyticsFilter filter, string dimension, SemanticContext ctx, List<string> notes, CancellationToken ct)
    {
        var op = (filter.Op ?? "in").Trim().ToLowerInvariant();
        if (op is not ("in" or "eq" or "not_in" or "ne" or "contains"))
        {
            throw new AnalyticsQueryException("invalid", $"Фильтр «{filter.Dimension}»: оператор «{filter.Op}» не поддерживается (in, not_in, contains).");
        }

        var values = (filter.Values ?? []).Select(v => v?.Trim() ?? "").Where(v => v.Length > 0).ToList();
        if (values.Count == 0)
        {
            throw new AnalyticsQueryException("invalid", $"Фильтр «{filter.Dimension}»: не указаны значения.");
        }

        var exclude = op is "not_in" or "ne";
        var contains = op == "contains";
        var matchNull = values.Any(v => NullWords.Contains(v.ToLowerInvariant()));
        values = values.Where(v => !NullWords.Contains(v.ToLowerInvariant())).ToList();
        var resolved = new List<object>();
        foreach (var value in values)
        {
            resolved.AddRange(await MatchAsync(dimension, value, contains, ctx, notes, ct));
        }

        var distinct = resolved.Distinct().ToList();
        return new ResolvedFilter(dimension, exclude, distinct, matchNull, $"{dimension} {(exclude ? "не " : "")}{string.Join(", ", filter.Values ?? [])}");
    }

    private async Task<IReadOnlyList<object>> MatchAsync(string dimension, string value, bool contains, SemanticContext ctx, List<string> notes, CancellationToken ct)
    {
        switch (dimension)
        {
            case "region":
                return Pick(value, contains, ctx.Regions.Values.Select(r => ((object)r.Id, r.Name)), dimension, notes, Guid.TryParse(value, out var g) ? g : null);
            case "direction":
                return Pick(value, contains, ctx.Directions.Values.Select(d => ((object)d.Id, d.Name)), dimension, notes, Guid.TryParse(value, out var gd) ? gd : null);
            case "branch":
                return Pick(value, contains, ctx.BranchNames.Select(b => ((object)b.Key, b.Value)), dimension, notes, long.TryParse(value, out var b) ? b : null);
            case "product_group":
                return Pick(value, contains, ctx.Groups.Select(x => ((object)x.Id, x.Name)), dimension, notes, long.TryParse(value, out var pg) ? pg : null);
            case "product_type":
                return Pick(value, contains, ctx.TypeNames.Select(x => ((object)x.Key, x.Value)), dimension, notes, long.TryParse(value, out var pt) ? pt : null);
            case "manager":
                return Text(value, contains, ctx.Directions.Values.Select(d => d.ManagerName), dimension);
            case "supervisor":
                return Text(value, contains, ctx.Regions.Values.Select(r => r.Supervisor), dimension);
            case "dealer":
                return Text(value, contains, ctx.Regions.Values.Select(r => r.Dealer), dimension);
            case "market_type":
                return Text(value, contains, await db.LinkoMarkets.AsNoTracking().Select(m => m.MarketTypeName).Distinct().ToListAsync(ct), dimension);
            case "source":
                return ["linko"];
            case "sales_channel":
            {
                var v = value.ToLowerInvariant();
                var options = new List<(object, string)> { ("regions", "Регионы (вторичка)"), ("factory", "Завод (экспорт и опт)") };
                options.AddRange(ctx.Channels.Select(c => ((object)$"ch:{c.Branch}", c.Name)));
                if (v.StartsWith("ch:", StringComparison.Ordinal) && options.Any(o => Equals(o.Item1, v)))
                {
                    return [v];
                }

                if (v is "вторичка" or "регионы" or "regions")
                {
                    return ["regions"];
                }

                if (v.Contains("завод") || v.Contains("экспорт") || v.Contains("опт") || v == "factory")
                {
                    return ["factory"];
                }

                return Pick(value, contains, options, dimension, notes, null);
            }
            case "agent":
            {
                if (long.TryParse(value, out var id))
                {
                    return [id];
                }

                var v = value.ToLowerInvariant();
                var users = await db.LinkoUsers.AsNoTracking()
                    .Select(u => new { u.Id, Name = ((u.FirstName ?? "") + " " + (u.SecondName ?? "")).Trim(), u.Username })
                    .ToListAsync(ct);
                return Pick(value, contains, users.Select(u => ((object)u.Id, u.Name.Length > 0 ? u.Name : u.Username ?? u.Id.ToString())), dimension, notes, null);
            }
            case "market":
            {
                if (long.TryParse(value, out var id))
                {
                    return [id];
                }

                var v = value.ToLowerInvariant();
                var exact = await db.LinkoMarkets.AsNoTracking().Where(m => m.Name.ToLower() == v).Select(m => m.Id).Take(MaxMatches).ToListAsync(ct);
                if (exact.Count > 0 && !contains)
                {
                    return exact.Cast<object>().ToList();
                }

                var found = await db.LinkoMarkets.AsNoTracking().Where(m => m.Name.ToLower().Contains(v)).Select(m => m.Id).Take(MaxMatches + 1).ToListAsync(ct);
                return Many(found, value, dimension, notes);
            }
            case "product":
            case "sku":
            {
                if (long.TryParse(value, out var id) && dimension == "product")
                {
                    return [id];
                }

                var v = value.ToLowerInvariant();
                var exact = await db.LinkoProducts.AsNoTracking()
                    .Where(p => p.Name.ToLower() == v || (p.Code != null && p.Code.ToLower() == v)).Select(p => p.Id).ToListAsync(ct);
                if (exact.Count > 0 && !contains)
                {
                    return exact.Cast<object>().ToList();
                }

                var found = await db.LinkoProducts.AsNoTracking()
                    .Where(p => p.Name.ToLower().Contains(v) || (p.Code != null && p.Code.ToLower().Contains(v))).Select(p => p.Id).Take(MaxMatches + 1).ToListAsync(ct);
                return Many(found, value, dimension, notes);
            }
            case "stock":
            {
                if (long.TryParse(value, out var id))
                {
                    return [id];
                }

                var stocks = await db.LinkoStocks.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync(ct);
                return Pick(value, contains, stocks.Select(s => ((object)s.Id, s.Name)), dimension, notes, null);
            }
            default:
                throw new AnalyticsQueryException("invalid", $"По измерению «{dimension}» фильтр не задаётся (период — dateFrom / dateTo).");
        }
    }

    private static IReadOnlyList<object> Many(List<long> found, string value, string dimension, List<string> notes)
    {
        if (found.Count == 0)
        {
            throw new AnalyticsQueryException("invalid", $"«{value}» не найдено в справочнике ({dimension}).");
        }

        if (found.Count > MaxMatches)
        {
            throw new AnalyticsQueryException("invalid", $"«{value}» подходит больше чем {MaxMatches} записям ({dimension}) — уточните название.");
        }

        if (found.Count > 1)
        {
            notes.Add($"«{value}» ({dimension}) — {found.Count} совпадений, учтены все.");
        }

        return found.Cast<object>().ToList();
    }

    /// <summary>Сопоставление по названию: id, точное совпадение (без регистра), иначе часть названия; нет — ошибка с вариантами.</summary>
    private static IReadOnlyList<object> Pick(string value, bool contains, IEnumerable<(object Id, string Name)> candidates, string dimension,
        List<string> notes, object? id)
    {
        var list = candidates.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
        if (id is not null)
        {
            return list.Any(c => Equals(c.Id, id)) ? [id] : throw new AnalyticsQueryException("invalid", $"{dimension}: id «{value}» не найден.");
        }

        var v = value.Trim().ToLowerInvariant();
        var exact = list.Where(c => c.Name.Trim().ToLowerInvariant() == v).Select(c => c.Id).ToList();
        if (exact.Count > 0 && !contains)
        {
            return exact;
        }

        var partial = list.Where(c => c.Name.ToLowerInvariant().Contains(v)).Select(c => c.Id).Distinct().ToList();
        if (partial.Count == 0)
        {
            var options = string.Join(", ", list.Select(c => c.Name).Distinct().OrderBy(n => n, StringComparer.CurrentCulture).Take(25));
            throw new AnalyticsQueryException("invalid", $"«{value}» не найдено ({dimension}). Варианты: {options}.");
        }

        if (partial.Count > 1 && !contains)
        {
            notes.Add($"«{value}» ({dimension}) — {partial.Count} совпадения по части названия, учтены все: " +
                      string.Join(", ", list.Where(c => partial.Contains(c.Id)).Select(c => c.Name).Distinct().Take(10)) + ".");
        }

        return partial;
    }

    private static IReadOnlyList<object> Text(string value, bool contains, IEnumerable<string?> candidates, string dimension)
    {
        var list = candidates.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()).Distinct().ToList();
        var v = value.Trim().ToLowerInvariant();
        var hits = list.Where(c => contains ? c.ToLowerInvariant().Contains(v) : c.ToLowerInvariant() == v).ToList();
        if (hits.Count == 0)
        {
            hits = list.Where(c => c.ToLowerInvariant().Contains(v)).ToList();
        }

        return hits.Count > 0
            ? hits.Select(h => (object)h.ToLowerInvariant()).ToList()
            : throw new AnalyticsQueryException("invalid", $"«{value}» не найдено ({dimension}). Варианты: {string.Join(", ", list.Take(25))}.");
    }

    /// <summary>Подписи значений измерения в результате: id → название (справочники движка и Linko).</summary>
    public async Task<Dictionary<object, string>> LabelsAsync(string dimension, IReadOnlyCollection<object> ids, SemanticContext ctx, CancellationToken ct)
    {
        var map = new Dictionary<object, string>();
        switch (dimension)
        {
            case "region":
                foreach (var id in ids.OfType<Guid>())
                {
                    map[id] = ctx.Regions.TryGetValue(id, out var r) ? r.Name : id.ToString();
                }

                break;
            case "direction":
                foreach (var id in ids.OfType<Guid>())
                {
                    map[id] = ctx.Directions.TryGetValue(id, out var d) ? d.Name : id.ToString();
                }

                break;
            case "branch":
                foreach (var id in ids.OfType<long>())
                {
                    map[id] = ctx.BranchNames.TryGetValue(id, out var n) ? n : $"Филиал {id}";
                }

                break;
            case "product_group":
                foreach (var id in ids.OfType<long>())
                {
                    map[id] = ctx.GroupName(id);
                }

                break;
            case "product_type":
                foreach (var id in ids.OfType<long>())
                {
                    map[id] = ctx.TypeNames.TryGetValue(id, out var n) ? n : $"Тип {id}";
                }

                break;
            case "sales_channel":
                foreach (var key in ids.OfType<string>())
                {
                    map[key] = key == "regions" ? "Регионы (вторичка)" : key == "factory" ? "Завод (экспорт и опт)"
                        : ctx.Channels.FirstOrDefault(c => $"ch:{c.Branch}" == key)?.Name ?? key;
                }

                break;
            case "agent":
            {
                var list = ids.OfType<long>().ToList();
                var users = await db.LinkoUsers.AsNoTracking().Where(u => list.Contains(u.Id))
                    .Select(u => new { u.Id, u.FirstName, u.SecondName, u.Username }).ToListAsync(ct);
                foreach (var u in users)
                {
                    var name = $"{u.FirstName} {u.SecondName}".Trim();
                    map[u.Id] = name.Length > 0 ? name : u.Username ?? $"ТП {u.Id}";
                }

                break;
            }
            case "market":
            {
                var list = ids.OfType<long>().ToList();
                foreach (var m in await db.LinkoMarkets.AsNoTracking().Where(m => list.Contains(m.Id)).Select(m => new { m.Id, m.Name }).ToListAsync(ct))
                {
                    map[m.Id] = m.Name;
                }

                break;
            }
            case "product":
            case "sku":
            {
                var list = ids.OfType<long>().ToList();
                foreach (var p in await db.LinkoProducts.AsNoTracking().Where(p => list.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Code }).ToListAsync(ct))
                {
                    map[p.Id] = dimension == "sku" ? (string.IsNullOrWhiteSpace(p.Code) ? $"{p.Id}" : p.Code!) : p.Name;
                }

                break;
            }
            case "stock":
            {
                var list = ids.OfType<long>().ToList();
                foreach (var s in await db.LinkoStocks.AsNoTracking().Where(s => list.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToListAsync(ct))
                {
                    map[s.Id] = s.Name;
                }

                break;
            }
            case "source":
                foreach (var key in ids.OfType<string>())
                {
                    map[key] = key == "linko" ? "Linko" : key;
                }

                break;
        }

        return map;
    }

    /// <summary>Подпись пустого значения измерения.</summary>
    public static string Empty(string dimension) => dimension switch
    {
        "region" => "Без региона",
        "branch" => "Без филиала",
        "direction" => "Без направления",
        "agent" => "Без агента",
        "market" => "Без точки",
        "product" or "sku" => "Без товара",
        "product_group" => "Без группы",
        "product_type" => "Без типа",
        _ => "Не задано",
    };
}

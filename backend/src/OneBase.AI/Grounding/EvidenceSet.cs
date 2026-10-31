using System.Text.Json;

namespace OneBase.AI.Grounding;

/// <summary>
/// Доказательства для проверки ответа: числа из результатов инструментов (данные OneBase) и точных расчётов (calculate). Из JSON — все
/// числа, числа в строках («Помадка 230 гр», «12,5 млн») и длины списков (сколько регионов, дилеров); доли (≤ 10) — ещё и в процентах.
/// Числа пользователя и прошлых ответов сюда не попадают: это не доказательство.
/// </summary>
public sealed class EvidenceSet
{
    /// <summary>Сколько ключевых чисел берётся для производных (сумма, разница, доля, рост) — пар выходит до 40².</summary>
    private const int MaxDerivedKeys = 40;

    private readonly List<decimal> _values = [];
    private readonly List<decimal> _percents = [];
    private bool _sorted = true;

    public int Count => _values.Count;

    public bool IsEmpty => _values.Count == 0;

    public void Add(decimal value)
    {
        var a = Math.Abs(value);
        _values.Add(a);
        _percents.Add(a);
        if (a <= 10m)
        {
            _percents.Add(a * 100m);
        }

        _sorted = false;
    }

    public void AddAll(EvidenceSet other)
    {
        _values.AddRange(other._values);
        _percents.AddRange(other._percents);
        _sorted = false;
    }

    /// <summary>Числа текста — со всеми малыми и с масштабом («1,2 млн» → 1 200 000).</summary>
    public void AddText(string? text)
    {
        foreach (var claim in NumberClaims.Find(text, keepSmall: true))
        {
            Add(claim.Value);
        }
    }

    /// <summary>Результат инструмента: JSON обходится целиком, иначе — числа текста.</summary>
    public void AddContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            Walk(doc.RootElement);
        }
        catch (JsonException)
        {
            AddText(content);
        }
    }

    /// <summary>
    /// Производные от ключевых чисел (показатели AI-сотрудников, уже подтверждённые данными): суммы, разницы, доли и рост в процентах —
    /// консультант сводит их в ответе, не вызывая расчёт.
    /// </summary>
    public void AddDerived(IEnumerable<decimal> keys)
    {
        var list = keys.Select(Math.Abs).Where(k => k > 0).Distinct().Take(MaxDerivedKeys).ToList();
        foreach (var a in list)
        {
            foreach (var b in list)
            {
                if (a == b)
                {
                    continue;
                }

                Add(a + b);
                Add(a - b);
                Add(a / b * 100m);
                Add((a - b) / b * 100m);
            }
        }
    }

    /// <summary>Подтверждает ли данные число: значение в пределах округления (процент — и как доля).</summary>
    public bool Supports(NumberClaim claim)
    {
        if (!_sorted)
        {
            _values.Sort();
            _percents.Sort();
            _sorted = true;
        }

        var value = Math.Abs(claim.Value);
        // Округление автора плюс тысячная доля — на копейки и хвосты деления.
        var tolerance = claim.Tolerance + value * 0.001m;
        if (Within(claim.Percent ? _percents : _values, value - tolerance, value + tolerance))
        {
            return true;
        }

        // Масштаб в подписи строки или столбца («| Выручка, млн сум | 13 457,8 |»): то же число в единицах данных, округление — в масштабе подписи.
        if (claim.ContextScale is { } scale && !claim.Percent)
        {
            var scaled = value * scale;
            var scaledTolerance = claim.Tolerance * scale + scaled * 0.001m;
            return Within(_values, scaled - scaledTolerance, scaled + scaledTolerance);
        }

        return false;
    }

    private static bool Within(List<decimal> sorted, decimal from, decimal to)
    {
        var lo = 0;
        var hi = sorted.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (sorted[mid] < from)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return lo < sorted.Count && sorted[lo] <= to;
    }

    private void Walk(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number when e.TryGetDecimal(out var d):
                Add(d);
                break;
            case JsonValueKind.String:
                AddText(e.GetString());
                break;
            case JsonValueKind.Array:
                Add(e.GetArrayLength());
                foreach (var item in e.EnumerateArray())
                {
                    Walk(item);
                }

                break;
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    Walk(p.Value);
                }

                break;
        }
    }
}

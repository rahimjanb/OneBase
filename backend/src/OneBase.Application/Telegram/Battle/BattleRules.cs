using System.Text.RegularExpressions;

namespace OneBase.Application.Telegram.Battle;

/// <summary>Чистые правила «Поля боя»: уровни наборов, сетка Могул, лестница бонуса, уровень героя, разбор акций Linko, порядок точек.</summary>
public static partial class BattleRules
{
    /// <summary>Набор Твизос по среднему закупу ТТ: 300 тыс — до 1 млн, 400 — до 3 млн, 600 — от 3 млн; меньше TwizosMinAvg — null.</summary>
    public static BattleTwizosSet? TwizosSet(BattleOptions o, decimal avgMonth)
    {
        if (avgMonth < o.TwizosMinAvg)
        {
            return null;
        }

        return o.TwizosSets.FirstOrDefault(s => s.MaxAvg is null || avgMonth < s.MaxAvg) ?? o.TwizosSets[^1];
    }

    /// <summary>Следующий уровень набора (300 → 400 → 600); после последнего — null.</summary>
    public static BattleTwizosSet? NextTwizosSet(BattleOptions o, BattleTwizosSet set)
    {
        var i = Array.IndexOf(o.TwizosSets, set);
        return i >= 0 && i + 1 < o.TwizosSets.Length ? o.TwizosSets[i + 1] : null;
    }

    /// <summary>Уровень, который ТТ ещё не брала одной накладной: максимальная накладная Твизос ≥ набора — следующий набор.</summary>
    public static BattleTwizosSet? TwizosSetToOffer(BattleOptions o, decimal avgMonth, decimal maxInvoice)
    {
        var set = TwizosSet(o, avgMonth);
        while (set is not null && maxInvoice >= set.Sum)
        {
            set = NextTwizosSet(o, set);
        }

        return set;
    }

    /// <summary>Категория Могул по среднему закупу Могул в месяц; новые клиенты и до 150 тыс — H.</summary>
    public static BattleMogulTier MogulTier(BattleOptions o, decimal avgMogul) =>
        o.MogulGrid.OrderByDescending(t => t.MinAvg).FirstOrDefault(t => avgMogul >= t.MinAvg) ?? o.MogulGrid[^1];

    public static BattleMogulTier? MogulTierByLetter(BattleOptions o, string letter) =>
        o.MogulGrid.FirstOrDefault(t => string.Equals(t.Letter, letter, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Цель объёма по максимальной накладной Могул месяца: ниже порога 1 — порог 1 (5%); между порогами — порог 2 (8/10%); выше порога 2 — null.
    /// Возвращает (сумма цели, процент, второй порог для карточки).
    /// </summary>
    public static (decimal Target, int Percent, decimal? Second, int? SecondPercent)? MogulVolumeTarget(BattleOptions o, BattleMogulTier tier, decimal maxInvoice)
    {
        if (maxInvoice < tier.Threshold1)
        {
            return (tier.Threshold1, o.MogulPercent, tier.Threshold2, tier.Percent2);
        }

        return maxInvoice < tier.Threshold2 ? (tier.Threshold2, tier.Percent2, null, null) : null;
    }

    /// <summary>Бонус недели по доле взятых целей — ступень лестницы, до которой дотянули; ниже первой — 0.</summary>
    public static decimal Bonus(BattleOptions o, int taken, int total)
    {
        if (total == 0)
        {
            return 0;
        }

        var share = 100m * taken / total;
        return o.Bonus.Where(s => share >= s.Percent).Select(s => s.Sum).DefaultIfEmpty(0).Max();
    }

    /// <summary>Следующая ступень и сколько целей до неё; все ступени взяты — null.</summary>
    public static (BattleBonusStep Step, int Goals)? NextStep(BattleOptions o, int taken, int total)
    {
        if (total == 0)
        {
            return null;
        }

        foreach (var step in o.Bonus.OrderBy(s => s.Percent))
        {
            var need = (int)Math.Ceiling(total * step.Percent / 100m);
            if (taken < need)
            {
                return (step, need - taken);
            }
        }

        return null;
    }

    /// <summary>Сколько целей нужно на ступень (для меток на шкале).</summary>
    public static int GoalsFor(BattleBonusStep step, int total) => (int)Math.Ceiling(total * step.Percent / 100m);

    /// <summary>Уровень героя по доле взятых целей (ступени лестницы): Новобранец, Воин, Командир, Легенда.</summary>
    public static string Level(BattleOptions o, int taken, int total)
    {
        var share = total == 0 ? 0 : 100m * taken / total;
        var steps = o.Bonus.OrderBy(s => s.Percent).ToList();
        return share >= steps[^1].Percent ? "Легенда"
            : steps.Count >= 3 && share >= steps[2].Percent ? "Командир"
            : share >= steps[0].Percent ? "Воин"
            : "Новобранец";
    }

    [GeneratedRegex(@"(\d{1,2})\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentRegex();

    [GeneratedRegex("[\"«“]\\s*([A-Ha-h])\\s*[\"»”]", RegexOptions.CultureInvariant)]
    private static partial Regex LetterRegex();

    /// <summary>Акция сетки Могул по названию Linko: «Акция на Могул Октябрь 2026г. 5% "A"» → (A, 5); не сетка — null.</summary>
    public static (string Letter, int Percent)? ParseMogulPromo(BattleOptions o, string name)
    {
        if (!o.MogulPromoWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var percent = PercentRegex().Match(name);
        var letter = LetterRegex().Match(name);
        return percent.Success && letter.Success ? (letter.Groups[1].Value.ToUpperInvariant(), int.Parse(percent.Groups[1].Value)) : null;
    }

    /// <summary>
    /// Порядок точек дня: Linko порядка не отдаёт (у плановых визитов время 00:00) — ближайший сосед от самой северной точки; точки без
    /// координат — в конце в исходном порядке.
    /// </summary>
    public static List<T> OrderByRoute<T>(IReadOnlyList<T> points, Func<T, (double? Lat, double? Lon)> coords)
    {
        var located = points.Where(p => coords(p) is { Lat: not null, Lon: not null }).ToList();
        var rest = points.Where(p => coords(p) is not { Lat: not null, Lon: not null }).ToList();
        var result = new List<T>(points.Count);
        if (located.Count > 0)
        {
            var current = located.OrderByDescending(p => coords(p).Lat).First();
            located.Remove(current);
            result.Add(current);
            while (located.Count > 0)
            {
                var (lat, lon) = coords(current);
                var next = located.OrderBy(p => Distance(lat!.Value, lon!.Value, coords(p).Lat!.Value, coords(p).Lon!.Value)).First();
                located.Remove(next);
                result.Add(next);
                current = next;
            }
        }

        result.AddRange(rest);
        return result;
    }

    private static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        var dx = (lon2 - lon1) * Math.Cos((lat1 + lat2) / 2 * Math.PI / 180);
        var dy = lat2 - lat1;
        return dx * dx + dy * dy;
    }

    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static bool Mentions(string? text, IEnumerable<string> words) =>
        text is { Length: > 0 } && words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

    /// <summary>Исключение ТП: слово из цифр — ID ТП в Linko («534»; «1534» не совпадает), остальные — слова в полном имени Linko.</summary>
    public static bool MentionsAgent(long agentId, string? linkoName, IEnumerable<string> words) =>
        words.Any(w => w.Length > 0 && w.All(char.IsDigit) ? long.TryParse(w, out var id) && id == agentId : Mentions(linkoName, [w]));
}

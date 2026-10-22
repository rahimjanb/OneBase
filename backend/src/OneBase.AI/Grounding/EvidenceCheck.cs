using System.Text.RegularExpressions;

namespace OneBase.AI.Grounding;

/// <summary>
/// Итог проверки ответа по данным: Checked — сколько чисел-утверждений проверено, Supported — подтверждены данными, Unsupported — не найдены
/// в данных, Quoted — числа пользователя, которые ответ приводит с оговоркой («не подтверждается данными», «по вашим словам»).
/// </summary>
public sealed record GroundingReport(int Checked, IReadOnlyList<string> Supported, IReadOnlyList<string> Unsupported, IReadOnlyList<string> Quoted)
{
    public static readonly GroundingReport Empty = new(0, [], [], []);

    public bool Grounded => Unsupported.Count == 0;
}

/// <summary>
/// Evidence Check: каждое число ответа (кроме разделов «Рекомендации» и «Прогноз») должно найтись в данных OneBase — результатах инструментов и
/// точных расчётов — с учётом округления. Число пользователя доказательством не является: в ответе оно допустимо только с оговоркой, что данными
/// не подтверждается (или «по вашим словам»); без оговорки — это согласие с неподтверждённым фактом.
/// </summary>
public static partial class EvidenceCheck
{
    [GeneratedRegex(@"не\s+подтвержд|не\s+найден|нет\s+в\s+данных|в\s+данных\s+нет|не\s+совпада|расходит|расхожд|не\s+соответств|отлича|не\s+сход|неверн|ошибочн|ошибк|по\s+ваш|вы\s+указал|вы\s+назвал|ваше\s+значени|вашем\s+отч|tasdiqlanmag|topilmad|mos\s+kelmay|siz\s+aytgan", RegexOptions.IgnoreCase)]
    private static partial Regex Reservation();

    /// <summary>
    /// question — вопрос пользователя; quotable — оспоренный прошлый ответ: их числа в ответе допустимы только с оговоркой («не подтверждается»,
    /// «было неверно»), иначе это повтор неподтверждённого как факта.
    /// </summary>
    public static GroundingReport Run(string? answer, EvidenceSet evidence, string? question = null, string? quotable = null)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return GroundingReport.Empty;
        }

        var claims = NumberClaims.Checkable(answer);
        var userNumbers = NumberClaims.Find(question, keepSmall: true).Concat(NumberClaims.Find(quotable, keepSmall: true)).ToList();
        var supported = new List<string>();
        var unsupported = new List<string>();
        var quoted = new List<string>();
        foreach (var claim in claims)
        {
            if (evidence.Supports(claim))
            {
                supported.Add(claim.Text);
            }
            else if (userNumbers.Any(u => Same(u, claim)) && Reservation().IsMatch(NumberClaims.Sentence(answer, claim.Index)))
            {
                quoted.Add(claim.Text);
            }
            else
            {
                unsupported.Add(claim.Text);
            }
        }

        return new GroundingReport(claims.Count, supported.Distinct().ToList(), unsupported.Distinct().ToList(), quoted.Distinct().ToList());
    }

    /// <summary>Одно и то же число (в пределах округления любого из двух).</summary>
    public static bool Same(NumberClaim a, NumberClaim b) =>
        Math.Abs(Math.Abs(a.Value) - Math.Abs(b.Value)) <= Math.Max(a.Tolerance, b.Tolerance) + Math.Abs(a.Value) * 0.001m;
}

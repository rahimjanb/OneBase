using System.Globalization;
using System.Text.RegularExpressions;
using OneBase.AI.Grounding;
using OneBase.Application.Analytik.Compute;

namespace OneBase.AI.Analytik.Intelligence;

/// <summary>
/// Self-Challenging Reviewer (правила кода) и поиск противоречий. Ревизор не соглашается с ответом по умолчанию: проверяет числа (EvidenceCheck
/// уже сделал композитор — здесь остаток), причинные утверждения против статусов гипотез, упоминание ограничений при неполных данных, годы,
/// которых нет в данных, и для финансовых выводов — пересчёт расчётов. Каждое замечание — с действием: переписать ответ или вернуть проверку.
/// </summary>
public static partial class InvestigationReviewer
{
    /// <summary>Противоречия: один показатель с разными значениями у разных аналитиков, одна гипотеза с разными статусами.</summary>
    public static IReadOnlyList<Contradiction> Contradictions(IReadOnlyList<StepOutcome> steps, IReadOnlyList<HypothesisVerdict> verdicts)
    {
        var list = new List<Contradiction>();
        var reported = steps.SelectMany(s => s.Metrics.Select(m => (Step: s, Name: Normalize(m.Name), m.Value, m.Unit)))
            .Where(x => x.Name.Length > 0)
            .GroupBy(x => (x.Name, Unit: (x.Unit ?? "").Trim().ToLowerInvariant()));
        foreach (var g in reported)
        {
            var values = g.Select(x => (x.Step, Value: Number(x.Value))).Where(x => x.Value is not null).ToList();
            var bySteps = values.GroupBy(v => v.Step.Step.Id).Select(v => v.First()).ToList();
            if (bySteps.Count < 2)
            {
                continue;
            }

            var min = bySteps.Min(v => v.Value!.Value);
            var max = bySteps.Max(v => v.Value!.Value);
            if (max != 0 && Math.Abs(max - min) / Math.Abs(max) > 0.02m)
            {
                list.Add(new Contradiction("metric",
                    $"«{g.First().Name}»: {string.Join(" / ", bySteps.Select(v => $"{v.Value:N0} ({v.Step.Step.Analyst}, шаг {v.Step.Step.Id})"))} — разные значения одного показателя.",
                    bySteps.Select(v => v.Step.Step.Id).ToList()));
            }
        }

        foreach (var g in verdicts.GroupBy(v => Normalize(v.Hypothesis.Statement)).Where(g => g.Count() > 1))
        {
            var statuses = g.Select(v => v.Status).Distinct().ToList();
            var confirmed = statuses.Any(s => s is HypothesisStatuses.Confirmed or HypothesisStatuses.PartiallyConfirmed);
            if (confirmed && statuses.Contains(HypothesisStatuses.Rejected))
            {
                list.Add(new Contradiction("hypothesis", $"Гипотеза «{g.First().Hypothesis.Statement}» подтверждена одной проверкой и отклонена другой.",
                    g.Select(v => v.Hypothesis.Id).ToList()));
            }
        }

        return list;
    }

    /// <summary>Замечания к ответу по правилам кода.</summary>
    public static IReadOnlyList<ReviewIssue> Check(string answer, string question, GroundingSummary grounding, IReadOnlyList<StepOutcome> steps,
        IReadOnlyList<HypothesisVerdict> verdicts, IReadOnlyList<string> periods)
    {
        var issues = new List<ReviewIssue>();
        if (grounding.Unsupported.Count > 0)
        {
            issues.Add(new ReviewIssue("numbers", "error", $"Числа не подтверждены данными: {string.Join("; ", grounding.Unsupported.Take(5))}."));
        }

        // Причинные утверждения: в предложении «из-за / привело к / причина…» без оговорки — должна быть подтверждённая гипотеза с этими словами.
        var supported = verdicts.Where(v => v.Status is HypothesisStatuses.Confirmed or HypothesisStatuses.PartiallyConfirmed).ToList();
        var weak = verdicts.Where(v => v.Status is HypothesisStatuses.Rejected or HypothesisStatuses.Inconclusive).ToList();
        foreach (var sentence in Sentences(answer).Where(s => CausalClaim().IsMatch(s) && !Hedge().IsMatch(s)))
        {
            if (weak.FirstOrDefault(v => v.Status == HypothesisStatuses.Rejected && Overlaps(sentence, v.Hypothesis.Statement)) is { } rejected)
            {
                issues.Add(new ReviewIssue("rejected_cause", "error",
                    $"Причиной названо то, что проверка отклонила («{rejected.Hypothesis.Statement}»): «{Short(sentence)}»."));
            }
            else if (!supported.Any(v => Overlaps(sentence, v.Hypothesis.Statement)) && !StepEvidence(sentence, steps))
            {
                issues.Add(new ReviewIssue("unproven_cause", "error",
                    $"Причинное утверждение без подтверждённой гипотезы и без данных шагов: «{Short(sentence)}» — пометь как гипотезу или убери."));
            }
        }

        // Неполные данные — в ответе должны быть ограничения.
        var incomplete = steps.Any(s => s.Status is not "completed") || verdicts.Any(v => v.Status == HypothesisStatuses.Inconclusive);
        if (incomplete && !Limits().IsMatch(answer.ToLowerInvariant()))
        {
            issues.Add(new ReviewIssue("limitations", "error",
                "Часть данных не получена или гипотезы не доказаны, а в ответе нет раздела об ограничениях — назови, чего не хватает."));
        }

        // Годы, которых нет ни в вопросе, ни в периодах данных, — признак неверного периода.
        var known = string.Join(" ", periods.Append(question));
        foreach (Match m in Year().Matches(answer))
        {
            if (!known.Contains(m.Value, StringComparison.Ordinal))
            {
                issues.Add(new ReviewIssue("period", "warning", $"В ответе год {m.Value}, которого нет в периодах данных — проверь период вывода.", "none"));
                break;
            }
        }

        return issues;
    }

    /// <summary>Вопрос о деньгах (выручка, прибыль, оплаты, долги, маржа, себестоимость): финансовые выводы требуют пересчёта расчётов.</summary>
    public static bool Financial(string question) => Money().IsMatch(question.ToLowerInvariant());

    /// <summary>Утверждение опирается на данные шага (вклад региона, группы, агента), а не придумано: общие слова с выводами или находками шага.</summary>
    private static bool StepEvidence(string sentence, IReadOnlyList<StepOutcome> steps) =>
        steps.Any(s => s.Findings.Append(s.Summary).Any(f => Overlaps(sentence, f)));

    /// <summary>Общие значимые слова (основы от 5 букв): не меньше двух — предложение говорит о той же гипотезе.</summary>
    public static bool Overlaps(string a, string b)
    {
        var wa = Stems(a);
        var wb = Stems(b);
        var common = wa.Intersect(wb).Count();
        return common >= 2 || (common == 1 && Math.Min(wa.Count, wb.Count) == 1);
    }

    private static HashSet<string> Stems(string text) =>
        Word().Matches(text.ToLowerInvariant()).Select(m => m.Value.Length > 6 ? m.Value[..6] : m.Value).Where(w => w.Length >= 5 && !Stop.Contains(w)).ToHashSet();

    private static readonly HashSet<string> Stop = ["продаж", "выручк", "период", "данные", "данных", "снизил", "выросл", "упали", "стали", "котор", "потому", "причин"];

    private static IEnumerable<string> Sentences(string text) =>
        SentenceSplit().Split(text).Select(s => s.Trim()).Where(s => s.Length > 10);

    private static string Short(string s) => s.Length > 160 ? s[..160] + "…" : s;

    private static string Normalize(string s) => Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");

    private static decimal? Number(string value)
    {
        var claims = NumberClaims.Find(value, true);
        return claims.Count == 1 ? claims[0].Value : null;
    }

    [GeneratedRegex(@"из-за|из за|вследствие|привел[оаи]?\s+к|привёл\s+к|вызван|причин[аойуе]\b|потому что|because|sababli|сабабли", RegexOptions.IgnoreCase)]
    private static partial Regex CausalClaim();

    [GeneratedRegex(@"гипотез|предполож|возможн|вероятн|может быть|не доказ|не подтвер|не исключ|требует проверки|не установл|нужн[оа]? провер", RegexOptions.IgnoreCase)]
    private static partial Regex Hedge();

    [GeneratedRegex(@"ограничен|не хватает|нет данных|данных нет|неполн|не получен|не загружен|не удалось|недоступн|не доказан")]
    private static partial Regex Limits();

    [GeneratedRegex(@"\b20[1-3]\d\b")]
    private static partial Regex Year();

    [GeneratedRegex(@"выручк|прибыл|оплат|деньг|долг|марж|себестоим|финанс|расход|бюджет|cash|profit|revenue")]
    private static partial Regex Money();

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Word();

    [GeneratedRegex(@"(?<=[.!?…])\s+|\n+")]
    private static partial Regex SentenceSplit();
}

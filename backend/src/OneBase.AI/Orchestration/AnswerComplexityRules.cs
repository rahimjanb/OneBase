using System.Text.RegularExpressions;
using OneBase.AI.Agents;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;

namespace OneBase.AI.Orchestration;

/// <summary>
/// Сложность вопроса для модели итогового ответа — детерминированными правилами по уже полученным данным, без отдельного вызова модели.
/// Баллы: поиск причин по данным отделов (+3); рекомендации (+1), а из данных нескольких отделов — ещё +1 (межотдельный синтез);
/// несколько отделов (+1 — одно это до порога 3 не доводит); много данных — больше 8 вызовов инструментов или 3+ источников (+1); 3+ точных
/// расчёта (+1); ошибки и нехватка доказательств у сотрудников — сбой, неподтверждённые числа, ошибки инструментов (+2), только частичный результат (+1);
/// возражение к прошлому ответу (+1); неоднозначность — сотрудники выбраны по ключевым словам (+1). Длина вопроса, токены и одно лишь
/// число отделов в балл не входят. Короткое уточнение («а по Хоразму?», «а за сентябрь?», «только помадка») наследует намерение прошлого
/// вопроса: «а в Бухаре?» после «почему упали продажи?» — тоже поиск причин.
/// </summary>
public static partial class AnswerComplexityRules
{
    public static AnswerComplexity Assess(string question, IReadOnlyList<LlmMessage> history, IReadOnlyList<AgentRun> runs, RoutingDecision routing,
        bool correction, int toolErrors = 0)
    {
        var intent = question;
        if (IsFollowUp(question) && history.LastOrDefault(m => m.Role == LlmRole.User) is { } previous)
        {
            intent = $"{previous.Content}\n{question}";
        }

        var withData = runs.Where(r => r.Result.Status is "completed" or "partial").ToList();
        var departments = runs.Select(r => r.Result.Agent).Distinct().Count();
        var signals = new List<string>();
        var score = 0;

        void Add(int points, string signal)
        {
            score += points;
            signals.Add($"{signal} +{points}");
        }

        if (withData.Count > 0 && Causal().IsMatch(intent))
        {
            Add(3, "поиск причин по данным");
        }

        if (runs.Count > 0 && Recommendations().IsMatch(intent))
        {
            Add(1, "рекомендации");
            if (departments >= 2)
            {
                Add(1, "межотдельный синтез");
            }
        }

        if (departments >= 2)
        {
            Add(1, $"отделов: {departments}");
        }

        var toolCalls = runs.Sum(r => r.ToolCalls);
        var sources = runs.SelectMany(r => r.Result.DataSources).Select(s => s.Title).Distinct().Count();
        if (toolCalls > 8 || sources >= 3)
        {
            Add(1, $"много данных: инструментов {toolCalls}, источников {sources}");
        }

        var calculations = runs.Sum(r => r.Calculations);
        if (calculations >= 3)
        {
            Add(1, $"расчётов: {calculations}");
        }

        // Сбой, неподтверждённые числа, ошибки инструментов — +2. Один лишь «partial» (сотрудник сам отметил, что данных не всё — обычно идущий
        // месяц) — +1: иначе любой вопрос о текущем месяце считался бы сложным (замер на настоящей модели: «Как продажи за октябрь?» ушёл бы в Opus).
        var problems = runs.Count(r => r.Result.Status == "failed" || r.Result.Unverified is { Count: > 0 }) + toolErrors;
        var partial = runs.Count(r => r.Result.Status == "partial" && r.Result.Unverified is not { Count: > 0 });
        if (problems > 0)
        {
            Add(2, $"ошибки и нехватка доказательств: {problems}");
        }
        else if (partial > 0)
        {
            Add(1, $"частичный результат: {partial}");
        }

        if (correction)
        {
            Add(1, "возражение к прошлому ответу");
        }

        if (routing.ByKeywords && runs.Count > 0)
        {
            Add(1, "неоднозначный вопрос: сотрудники выбраны по ключевым словам");
        }

        return new AnswerComplexity(score, signals);
    }

    /// <summary>Короткое уточнение к прошлому вопросу: «а по Хоразму?», «а за сентябрь?», «только помадка», «и в Бухаре».</summary>
    public static bool IsFollowUp(string question)
    {
        var q = question.Trim().ToLowerInvariant();
        return q.Length <= 60 && FollowUp().IsMatch(q);
    }

    [GeneratedRegex(@"почему|причин|из-за чего|из за чего|за сч[её]т чего|что повлиял|как повлиял|в ч[её]м дело|объясни|отчего|nega|sabab", RegexOptions.IgnoreCase)]
    private static partial Regex Causal();

    [GeneratedRegex(@"что (нужно |надо |стоит )?(сделать|делать|улучшить|изменить)|как (увеличить|улучшить|поднять|исправить|снизить|сократить)|рекоменд|стратег|приоритет|план действий", RegexOptions.IgnoreCase)]
    private static partial Regex Recommendations();

    [GeneratedRegex(@"^(а|и|только|тогда|ещё|еще|теперь|без|кроме)\b|^(за|по|в|во|на)\s+\S+\??$", RegexOptions.IgnoreCase)]
    private static partial Regex FollowUp();
}

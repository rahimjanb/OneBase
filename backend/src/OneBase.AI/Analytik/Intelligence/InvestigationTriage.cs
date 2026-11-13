using System.Text.RegularExpressions;
using OneBase.AI.Llm;
using OneBase.AI.Orchestration;

namespace OneBase.AI.Analytik.Intelligence;

/// <summary>Решение о глубине: исследовать ли (Investigate), глубокое ли (Deep — со скептиком и проверкой моделью), балл и признаки.</summary>
public sealed record TriageResult(bool Investigate, bool Deep, int Score, IReadOnlyList<string> Signals);

/// <summary>
/// Сложность вопроса до сбора данных — правилами, без вызова модели: простой вопрос («как продажи за октябрь?») идёт прежним быстрым путём
/// (ни одного лишнего вызова), сложный — исследованием. Баллы: поиск причин (+3), явная просьба исследовать (+3), гипотезы и проверка (+3),
/// несколько отделов (+1 за каждый сверх первого), длинный горизонт (+1), рекомендации (+1), прогноз или сценарий вместе с причинами (+1).
/// Короткое уточнение наследует намерение прошлого вопроса.
/// </summary>
public static partial class InvestigationTriage
{
    public static TriageResult Assess(string question, IReadOnlyList<LlmMessage> history, string mode, int threshold, int deepThreshold)
    {
        var intent = question;
        if (AnswerComplexityRules.IsFollowUp(question) && history.LastOrDefault(m => m.Role == LlmRole.User) is { } previous)
        {
            intent = $"{previous.Content}\n{question}";
        }

        var text = RouteIntent.Normalize(intent);
        var signals = new List<string>();
        var score = 0;

        void Add(int points, string signal)
        {
            score += points;
            signals.Add($"{signal} +{points}");
        }

        if (Causal().IsMatch(text))
        {
            Add(3, "поиск причин");
        }

        if (Explore().IsMatch(text))
        {
            Add(3, "просьба исследовать");
        }

        if (Hypotheses().IsMatch(text))
        {
            Add(3, "гипотезы и проверка");
        }

        var departments = AnalytikAgents.Keywords.Count(k => k.Value.Any(text.Contains));
        if (departments >= 2)
        {
            Add(departments - 1, $"отделов: {departments}");
        }

        if (LongHorizon().IsMatch(text))
        {
            Add(1, "длинный горизонт");
        }

        if (Recommend().IsMatch(text))
        {
            Add(1, "рекомендации");
        }

        if (score > 0 && WhatIf().IsMatch(text))
        {
            Add(1, "прогноз или сценарий");
        }

        var m = (mode ?? "auto").Trim().ToLowerInvariant();
        var investigate = m switch
        {
            "off" => false,
            "always" => !RouteIntent.IsSmallTalk(question),
            _ => score >= threshold,
        };
        return new TriageResult(investigate, investigate && score >= deepThreshold, score, signals);
    }

    [GeneratedRegex(@"почему|причин|из-за чего|из за чего|за сч[её]т чего|что повлиял|как повлиял|в ч[её]м дело|отчего|root cause|nega|sabab|нега\b|сабаб")]
    private static partial Regex Causal();

    [GeneratedRegex(@"исследу|разбер(ись|и)|проанализируй\s+(глубоко|подробно|детально)|глубок\p{L}*\s+анализ|детальн\p{L}*\s+анализ|найди\s+причин|докопай|расследу|chuqur\s+tahlil")]
    private static partial Regex Explore();

    [GeneratedRegex(@"гипотез|провер(ь|ить)\s+(гипотез|предполож|верно ли|правда ли)|подтверди|опровергни|связан[оа]?\s+ли|влияет\s+ли|зависит\s+ли")]
    private static partial Regex Hypotheses();

    [GeneratedRegex(@"за\s+(год|квартал|полгода|\d+\s+месяц)|с\s+начала\s+года|динамик|тенденц|по\s+месяцам|yil\s+davomida")]
    private static partial Regex LongHorizon();

    [GeneratedRegex(@"что\s+(нужно\s+|надо\s+|стоит\s+)?(сделать|делать|улучшить|изменить)|как\s+(увеличить|улучшить|поднять|исправить|снизить|сократить)|рекоменд|приоритет|план\s+действий")]
    private static partial Regex Recommend();

    [GeneratedRegex(@"прогноз|что\s+будет|что\s+если|сценари|nima\s+bo'ladi")]
    private static partial Regex WhatIf();
}

using OneBase.Domain.AI;

namespace OneBase.AI.Gateway;

/// <summary>Назначение вызова модели — для выбора модели по правилам (AiModelRouting).</summary>
public enum AiCallPurpose
{
    /// <summary>Маршрутизатор: какие AI-сотрудники нужны (модель — категория «Маршрутизация», AiTaskRouting).</summary>
    Router,

    /// <summary>AI-сотрудник отдела: цикл с инструментами.</summary>
    Agent,

    /// <summary>Итоговый ответ консультанта.</summary>
    Final,

    /// <summary>Перепроверка: исправление ответа или повтор сотрудника.</summary>
    Review,
}

/// <summary>
/// Настройки маршрутизации моделей по назначению вызова (ai.Settings), уже проверенные: неизвестный режим — off, fixed, standard; порог и
/// попытки — в допустимых пределах; бюджет вопроса — положительный или без ограничения. Пустая модель никогда не заменяется более дорогой:
/// для сотрудников — модель стандартных ответов, затем основная; для сложных ответов — модель стандартных ответов.
/// </summary>
public sealed record AiModelRoutingSettings(
    string Mode,
    AiModelRef? Agent,
    string FinalMode,
    AiModelRef? FinalDefault,
    AiModelRef? FinalComplex,
    string FallbackMode,
    int ComplexityThreshold,
    int ReviewMaxAttempts,
    decimal? QuestionBudgetUsd)
{
    public const int MinThreshold = 1;
    public const int MaxThreshold = 10;
    public const int MaxReviewAttempts = 3;

    public static readonly AiModelRoutingSettings Off = new(AiTaskMode.Off, null, AiFinalMode.Fixed, null, null, AiFallbackMode.Standard, 3, 2, null);

    /// <summary>Правила применяются к ответам.</summary>
    public bool On => Mode == AiTaskMode.On;

    /// <summary>Режим «Сравнение»: ответ как раньше, выбор правил — в журнал.</summary>
    public bool Shadow => Mode == AiTaskMode.Shadow;

    public bool Review => FallbackMode == AiFallbackMode.Review;

    public static AiModelRoutingSettings From(string? mode, AiModelRef? agent, string? finalMode, AiModelRef? finalDefault, AiModelRef? finalComplex,
        string? fallbackMode, int threshold, int reviewAttempts, decimal? questionBudget) => new(
        AiTaskMode.All.Contains(mode ?? "") ? mode! : AiTaskMode.Off,
        agent,
        AiFinalMode.All.Contains(finalMode ?? "") ? finalMode! : AiFinalMode.Fixed,
        finalDefault,
        finalComplex,
        AiFallbackMode.All.Contains(fallbackMode ?? "") ? fallbackMode! : AiFallbackMode.Standard,
        Math.Clamp(threshold, MinThreshold, MaxThreshold),
        Math.Clamp(reviewAttempts, 0, MaxReviewAttempts),
        questionBudget is > 0 ? questionBudget : null);
}

/// <summary>Выбранная модель вызова и почему; Model null — основная модель из настроек (как раньше).</summary>
public sealed record AiModelChoice(AiModelRef? Model, string Reason);

/// <summary>
/// Выбор модели по назначению вызова — вместе с AiTaskRouting (маршрутизатор) единственное место, где решается, какая модель отвечает.
/// Приоритет у явного выбора человека: модель канала (Telegram) и модель, заданная AI-сотруднику или консультанту в «Агентах»; без них —
/// правила маршрутизации моделей (режим «Включено»), иначе основная модель. Режим «Сравнение» считает выбор правил тем же кодом (Planned),
/// но ответ идёт как раньше.
/// </summary>
public static class AiModelRouting
{
    /// <summary>Модель AI-сотрудника отдела.</summary>
    public static AiModelChoice Agent(AiSettingsSnapshot settings, AiModelRef? channel, AiModelRef? own)
    {
        if (channel is not null)
        {
            return new AiModelChoice(channel, "модель канала");
        }

        if (own is not null)
        {
            return new AiModelChoice(own, "модель сотрудника в «Агентах»");
        }

        return settings.ModelRouting.On ? PlannedAgent(settings.ModelRouting) : new AiModelChoice(null, "основная модель");
    }

    /// <summary>Модель сотрудника по правилам (без явного выбора человека) — и для режима «Сравнение».</summary>
    public static AiModelChoice PlannedAgent(AiModelRoutingSettings r) =>
        r.Agent is { } agent ? new AiModelChoice(agent, "модель сотрудников")
        : r.FinalDefault is { } standard ? new AiModelChoice(standard, "модель сотрудников не задана — модель стандартных ответов")
        : new AiModelChoice(null, "модель сотрудников не задана — основная модель");

    /// <summary>Модель итогового ответа: по сложности (dynamic) или всегда стандартная (fixed).</summary>
    public static AiModelChoice Final(AiSettingsSnapshot settings, AiModelRef? channel, AiModelRef? own, AnswerComplexity complexity)
    {
        if (channel is not null)
        {
            return new AiModelChoice(channel, "модель канала");
        }

        if (own is not null)
        {
            return new AiModelChoice(own, "модель консультанта в «Агентах»");
        }

        return settings.ModelRouting.On ? PlannedFinal(settings.ModelRouting, complexity) : new AiModelChoice(null, "основная модель");
    }

    /// <summary>Модель итогового ответа по правилам — и для режима «Сравнение».</summary>
    public static AiModelChoice PlannedFinal(AiModelRoutingSettings r, AnswerComplexity complexity)
    {
        var standard = r.FinalDefault is { } d
            ? new AiModelChoice(d, r.FinalMode == AiFinalMode.Dynamic ? $"стандартный вопрос: сложность {complexity.Score} < {r.ComplexityThreshold}" : "модель итогового ответа")
            : new AiModelChoice(null, "модель стандартных ответов не задана — основная модель");
        if (r.FinalMode != AiFinalMode.Dynamic || complexity.Score < r.ComplexityThreshold)
        {
            return standard;
        }

        return r.FinalComplex is { } complex
            ? new AiModelChoice(complex, $"сложный вопрос: сложность {complexity.Score} ≥ {r.ComplexityThreshold} ({string.Join(", ", complexity.Signals)})")
            : standard with { Reason = $"сложный вопрос, но модель сложных ответов не задана — {standard.Reason}" };
    }

    /// <summary>
    /// Переход при перепроверке итогового ответа (только review): на модель сложных ответов, если ответ писала другая модель. Обратного
    /// перехода нет — цикла «стандартная → сложная → стандартная» не бывает. null — переходить некуда.
    /// </summary>
    public static AiModelChoice? Escalation(AiModelRoutingSettings r, AiModelRef? current) =>
        r.On && r.Review && r.FinalComplex is { } complex && complex != current
            ? new AiModelChoice(complex, "перепроверка: ответ стандартной модели не прошёл проверку — модель сложных ответов")
            : null;
}

/// <summary>Сложность вопроса для выбора модели итогового ответа: балл и признаки, из которых он сложился (для журнала).</summary>
public sealed record AnswerComplexity(int Score, IReadOnlyList<string> Signals)
{
    public static readonly AnswerComplexity None = new(0, []);
}

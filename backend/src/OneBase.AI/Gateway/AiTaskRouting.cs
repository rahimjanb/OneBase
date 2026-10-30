using OneBase.Domain.AI;

namespace OneBase.AI.Gateway;

/// <summary>
/// Категории простых задач, которым можно назначить отдельную (более дешёвую) модель. Сложное — цикл AI-сотрудника с инструментами,
/// итоговый ответ консультанта, AI Data Manager — всегда идёт основной моделью и сюда не входит.
/// </summary>
public enum AiTaskCategory
{
    /// <summary>Маршрутизация: какие AI-сотрудники нужны вопросу и какие задачи им дать (классификация по отделам).</summary>
    Route,

    /// <summary>Извлечение структурированных полей в JSON: подбор колонок листа Google Sheets к полям OneBase.</summary>
    Extract,

    /// <summary>
    /// Модели ответа консультанта по правилам (AiModelRouting) — только для журнала сравнения в режиме «Сравнение»: фактическая модель
    /// сотрудников и итогового ответа против выбора правил и прогноз стоимости на тех же токенах. Своей модели категории нет.
    /// </summary>
    Answer,
}

/// <summary>
/// Как идёт задача категории. Model — модель вызова (null — основная). Fallback — исходная модель на случай сбоя модели категории.
/// Shadow — модель для сравнения в фоне (режим «Сравнение»: пользователь получает результат основной модели). Baseline — исходная модель.
/// </summary>
public sealed record AiTaskRoute(AiTaskCategory Category, AiModelRef? Model, AiModelRef? Fallback, AiModelRef? Shadow, AiModelRef? Baseline)
{
    /// <summary>Задача идёт моделью категории (режим «Включено»).</summary>
    public bool Routed => Model is not null;
}

/// <summary>Выбор модели для категории простых задач по настройкам AI — единственное место, где решается, какая модель её выполняет.</summary>
public static class AiTaskRouting
{
    public static string Code(AiTaskCategory category) => category switch
    {
        AiTaskCategory.Route => "route",
        AiTaskCategory.Answer => "answer",
        _ => "extract",
    };

    public static AiTaskRoute For(AiSettingsSnapshot settings, AiTaskCategory category)
    {
        var (candidate, mode) = category switch
        {
            AiTaskCategory.Route => (settings.Router, settings.RouterMode),
            _ => (settings.Extract, settings.ExtractMode),
        };
        var baseline = settings.Primary;

        // Общий флаг выключен, модель категории не выбрана или совпадает с основной, режим «Выкл» — как раньше: основной моделью.
        if (!settings.TaskRoutingEnabled || candidate is null || candidate == baseline || mode == AiTaskMode.Off)
        {
            return new AiTaskRoute(category, null, null, null, baseline);
        }

        return mode == AiTaskMode.Shadow
            ? new AiTaskRoute(category, null, null, candidate, baseline)
            : new AiTaskRoute(category, candidate, baseline, null, baseline);
    }
}

namespace OneBase.Domain.AI;

/// <summary>Глубина рассуждений моделей (reasoning effort).</summary>
public static class AiReasoning
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    public static readonly IReadOnlyList<string> All = [Low, Medium, High];
}

/// <summary>
/// Режим модели категории простых задач. off — задачи идут основной моделью; shadow — сравнение: пользователь получает результат основной
/// модели, модель категории в фоне решает ту же задачу, итоги сравнения пишутся в ai.ModelComparisons; on — задачи идут моделью категории,
/// при ошибке или некорректном результате — основной моделью.
/// </summary>
public static class AiTaskMode
{
    public const string Off = "off";
    public const string Shadow = "shadow";
    public const string On = "on";

    public static readonly IReadOnlyList<string> All = [Off, Shadow, On];
}

/// <summary>Итоговый ответ консультанта: fixed — всегда модель FinalDefault; dynamic — по сложности вопроса FinalDefault или FinalComplex.</summary>
public static class AiFinalMode
{
    public const string Fixed = "fixed";
    public const string Dynamic = "dynamic";
    public static readonly IReadOnlyList<string> All = [Fixed, Dynamic];
}

/// <summary>
/// Что делать с проблемным результатом. standard — как раньше: неподтверждённые числа — одно исправление той же моделью. review — разбор
/// причины: повтор упавшего инструмента без модели, повтор сотрудника с исправленной задачей, исправление ответа и ограниченный переход
/// на модель сложных ответов; число попыток и бюджет вопроса ограничены.
/// </summary>
public static class AiFallbackMode
{
    public const string Standard = "standard";
    public const string Review = "review";
    public static readonly IReadOnlyList<string> All = [Standard, Review];
}

/// <summary>Общие настройки AI: основной и резервный провайдер, модели для задач, параметры генерации. Одна строка.</summary>
public class AiSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>AI включён для пользователей.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Основная модель: консультант и агенты, у которых своя модель не задана.</summary>
    public string? PrimaryProvider { get; set; }
    public string? PrimaryModel { get; set; }

    /// <summary>Резерв: вызывается, если основной провайдер вернул ошибку или не ответил.</summary>
    public string? FallbackProvider { get; set; }
    public string? FallbackModel { get; set; }

    /// <summary>Быстрая модель для маршрутизации вопросов (категория задач «Маршрутизация»); пусто — основная.</summary>
    public string? RouterProvider { get; set; }
    public string? RouterModel { get; set; }

    /// <summary>
    /// Модели по категориям простых задач включены (общий флаг). false — все задачи идут основной моделью, сравнение не запускается:
    /// так одним переключателем возвращается прежнее поведение.
    /// </summary>
    public bool TaskRoutingEnabled { get; set; }

    /// <summary>Режим категории «Маршрутизация» (выбор AI-сотрудников и задач им): off | shadow | on — см. <see cref="AiTaskMode"/>.</summary>
    public string RouterMode { get; set; } = AiTaskMode.Off;

    /// <summary>Модель категории «Извлечение JSON» (подбор колонок Google Sheets); пусто — основная.</summary>
    public string? ExtractProvider { get; set; }
    public string? ExtractModel { get; set; }

    /// <summary>Режим категории «Извлечение JSON».</summary>
    public string ExtractMode { get; set; } = AiTaskMode.Off;

    /// <summary>Модель эмбеддингов для базы знаний (семантический поиск).</summary>
    public string? EmbeddingProvider { get; set; }
    public string? EmbeddingModel { get; set; }

    /// <summary>Температура по умолчанию; пусто — значение провайдера.</summary>
    public double? Temperature { get; set; }

    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>
    /// Глубина рассуждений: low | medium | high (у Claude — effort, у reasoning-моделей OpenAI — reasoning_effort); пусто — по умолчанию модели.
    /// Чем глубже, тем дольше и дороже ответ.
    /// </summary>
    public string? ReasoningEffort { get; set; } = AiReasoning.Low;

    /// <summary>Дневной бюджет на AI, USD (по ценам моделей); исчерпан — новые вызовы моделей не выполняются до завтра. Пусто — без ограничения.</summary>
    public decimal? DailyBudgetUsd { get; set; }

    /// <summary>Месячный бюджет на AI, USD. Пусто — без ограничения.</summary>
    public decimal? MonthlyBudgetUsd { get; set; }

    /// <summary>При какой доле бюджета, %, уведомить администраторов AI.</summary>
    public int BudgetWarnPercent { get; set; } = 80;

    /// <summary>Сколько вызовов моделей идёт одновременно (остальные ждут очереди); 0 — без ограничения.</summary>
    public int MaxConcurrentCalls { get; set; } = 8;

    /// <summary>
    /// Маршрутизация моделей по назначению вызова (сотрудники, итоговый ответ, перепроверка): off — как раньше, основной моделью; shadow —
    /// ответ как раньше, выбор по правилам и прогноз стоимости — только в журнал сравнения; on — модели по правилам ниже.
    /// </summary>
    public string ModelRoutingMode { get; set; } = AiTaskMode.Off;

    /// <summary>Модель AI-сотрудников отделов, у которых своя модель не задана.</summary>
    public string? AgentProvider { get; set; }
    public string? AgentModel { get; set; }

    /// <summary>Выбор модели итогового ответа: fixed | dynamic — см. <see cref="AiFinalMode"/>.</summary>
    public string FinalAnswerMode { get; set; } = AiFinalMode.Fixed;

    /// <summary>Модель итогового ответа на стандартные вопросы.</summary>
    public string? FinalDefaultProvider { get; set; }
    public string? FinalDefaultModel { get; set; }

    /// <summary>Модель итогового ответа на сложные вопросы (межотдельный синтез, причины, сложная интерпретация) и перехода при перепроверке.</summary>
    public string? FinalComplexProvider { get; set; }
    public string? FinalComplexModel { get; set; }

    /// <summary>Проблемный результат: standard | review — см. <see cref="AiFallbackMode"/>.</summary>
    public string FallbackMode { get; set; } = AiFallbackMode.Standard;

    /// <summary>С какого балла сложности итоговый ответ идёт моделью сложных ответов (1–10).</summary>
    public int ComplexityThreshold { get; set; } = 3;

    /// <summary>Сколько дополнительных попыток перепроверки на один ответ (0–3).</summary>
    public int ReviewMaxAttempts { get; set; } = 2;

    /// <summary>Бюджет одного вопроса, USD: дошли — новые вызовы моделей этого ответа не выполняются. Пусто — без ограничения.</summary>
    public decimal? QuestionBudgetUsd { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

namespace OneBase.Domain.AI;

/// <summary>Один вызов модели (генерация или эмбеддинги): для учёта токенов и стоимости.</summary>
public class AiUsage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UserId { get; set; }
    public string? AgentCode { get; set; }
    public Guid? ConversationId { get; set; }

    /// <summary>Зачем вызвана модель: consultant.route, agent, consultant.answer, knowledge.index…</summary>
    public required string Purpose { get; set; }

    public required string Provider { get; set; }
    public required string Model { get; set; }
    /// <summary>Вход по базовой цене — без токенов кэша промпта.</summary>
    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    /// <summary>Прочитано из кэша промпта (Anthropic cache_read_input_tokens) — тариф ниже базового входа.</summary>
    public int CacheReadTokens { get; set; }

    /// <summary>Записано в кэш промпта (Anthropic cache_creation_input_tokens) — тариф выше базового входа.</summary>
    public int CacheWriteTokens { get; set; }

    /// <summary>Стоимость по ценам модели на момент вызова, USD; null — цена модели (или цена кэша при кэш-токенах) не задана.</summary>
    public decimal? CostUsd { get; set; }

    public long DurationMs { get; set; }

    /// <summary>Из DurationMs — ожидание свободного места (лимит одновременных вызовов), мс; null — записи до замера очереди.</summary>
    public long? QueueWaitMs { get; set; }

    /// <summary>Попытка этой модели в вызове (1 — первая, 2 — повтор после сбоя); null — записи до замера попыток.</summary>
    public int? Attempt { get; set; }

    public bool Success { get; set; }

    /// <summary>Ответ консультанта, к которому относится вызов: все вызовы одного ответа (маршрутизация, сотрудники, итог, перепроверка).</summary>
    public Guid? TurnId { get; set; }

    /// <summary>Почему выбрана эта модель (правило маршрутизации моделей): «сложный вопрос: …», «модель сотрудников», «основная».</summary>
    public string? ModelReason { get; set; }

    /// <summary>Вызов сделан резервной моделью после сбоя основной.</summary>
    public bool UsedFallback { get; set; }

    /// <summary>
    /// Стоимость рассчитана по ценам модели из «Настройки → AI → Модели» (провайдер стоимость не возвращает); PriceBasis — какие цены
    /// применены (за 1 млн токенов), чтобы расчёт оставался проверяемым после смены цен. null — стоимость не рассчитана (нет цены).
    /// </summary>
    public string? PriceBasis { get; set; }

    /// <summary>Текст ошибки провайдера — без ключей (очищен в AI Gateway).</summary>
    public string? Error { get; set; }
}

/// <summary>
/// AI-журнал: один вопрос консультанту или задача AI-сотруднику — кто спросил, какие сотрудники, модели и инструменты работали,
/// источники, токены, стоимость, время, ответ и ошибки.
/// </summary>
public class AiAuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid UserId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? MessageId { get; set; }

    /// <summary>consultant — вопрос в чате; agent — задача одному сотруднику.</summary>
    public required string Kind { get; set; }

    public required string Question { get; set; }

    /// <summary>Коды AI-сотрудников через запятую.</summary>
    public string? Agents { get; set; }

    public string? Model { get; set; }
    public string? Tools { get; set; }

    /// <summary>Источники данных ответа (jsonb).</summary>
    public string? Sources { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal? CostUsd { get; set; }
    public long DurationMs { get; set; }

    /// <summary>Ответ взят из кэша ответов консультанта (тот же вопрос, данные не обновлялись) — без вызовов модели.</summary>
    public bool FromCache { get; set; }

    /// <summary>completed | failed.</summary>
    public required string Status { get; set; }

    /// <summary>Начало ответа (полный ответ — в сообщении чата).</summary>
    public string? ResponsePreview { get; set; }

    public string? Error { get; set; }

    /// <summary>Проверка ответа по данным (jsonb): числа — подтверждены или нет, исправлялся ли ответ; перепроверка после возражения.</summary>
    public string? Grounding { get; set; }

    /// <summary>Итог перепроверки, если вопрос оспаривал прошлый ответ: confirmed, corrected, insufficient.</summary>
    public string? CorrectionVerdict { get; set; }

    /// <summary>Оспоренный ответ (сообщение чата) — исходный ответ для сравнения.</summary>
    public Guid? CorrectionOfMessageId { get; set; }

    /// <summary>
    /// Замер ответа (jsonb, AiTimingsView): этапы, вызовы модели с ожиданием очереди и попытками, инструменты с размером результата.
    /// Только длительности, имена и размеры — без текста и данных.
    /// </summary>
    public string? Timings { get; set; }

    /// <summary>Ответ консультанта (общий для его вызовов в ai.Usage.TurnId).</summary>
    public Guid? TurnId { get; set; }

    /// <summary>
    /// Маршрутизация моделей ответа (jsonb): режим, сложность вопроса и её признаки, выбранные модели и причины, перепроверки, итог по
    /// вызовам (число, по моделям и назначениям, токены, стоимость — расчётная, неполная, если у части вызовов нет цены).
    /// </summary>
    public string? Routing { get; set; }
}

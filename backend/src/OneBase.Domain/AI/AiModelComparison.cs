namespace OneBase.Domain.AI;

/// <summary>
/// Сравнение исходной модели (основной) и модели категории на одной и той же задаче: в режиме «Сравнение» на задачах пользователей (shadow)
/// или на эталонном наборе (eval). Пользователь при этом получает результат исходной модели. Здесь — итоги, токены, стоимость и время обеих.
/// </summary>
public class AiModelComparison
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Категория задач: route | extract.</summary>
    public required string Category { get; set; }

    /// <summary>shadow — задача пользователя в режиме сравнения; eval — эталонный набор (запуск администратором).</summary>
    public required string Source { get; set; }

    /// <summary>Код задачи эталонного набора; для shadow — null.</summary>
    public string? CaseCode { get; set; }

    /// <summary>Задача (вопрос пользователя или описание листа), до 500 символов — для разбора расхождений администратором.</summary>
    public string? Task { get; set; }

    public required string BaselineModel { get; set; }
    public required string CandidateModel { get; set; }

    /// <summary>Итог в сравнимом виде (route — коды сотрудников через запятую; extract — «столбец→поле»), до 1000 символов.</summary>
    public string? BaselineResult { get; set; }
    public string? CandidateResult { get; set; }

    /// <summary>Ожидаемый итог эталонной задачи; для shadow — null.</summary>
    public string? Expected { get; set; }

    /// <summary>Итоги совпали (для extract — доля совпавших столбцов не ниже порога).</summary>
    public bool Agree { get; set; }

    /// <summary>Доля совпадения, 0–1 (route — мера Жаккара по сотрудникам; extract — доля столбцов с тем же полем).</summary>
    public double Similarity { get; set; }

    /// <summary>Ответ разобран (JSON корректен и по схеме).</summary>
    public bool BaselineValid { get; set; }
    public bool CandidateValid { get; set; }

    /// <summary>Совпадает с ожидаемым (только eval).</summary>
    public bool? BaselineCorrect { get; set; }
    public bool? CandidateCorrect { get; set; }

    public int BaselineInputTokens { get; set; }
    public int BaselineCacheReadTokens { get; set; }
    public int BaselineCacheWriteTokens { get; set; }
    public int BaselineOutputTokens { get; set; }
    public decimal? BaselineCostUsd { get; set; }
    public long BaselineMs { get; set; }

    public int CandidateInputTokens { get; set; }
    public int CandidateCacheReadTokens { get; set; }
    public int CandidateCacheWriteTokens { get; set; }
    public int CandidateOutputTokens { get; set; }
    public decimal? CandidateCostUsd { get; set; }
    public long CandidateMs { get; set; }

    /// <summary>Ошибка модели категории (сбой провайдера, некорректный ответ), до 500 символов.</summary>
    public string? CandidateError { get; set; }
}

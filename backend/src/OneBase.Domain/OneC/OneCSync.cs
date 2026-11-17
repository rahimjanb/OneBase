namespace OneBase.Domain.OneC;

// Состояние синхронизации 1С → OneBase. Служба интеграции без состояния: всё, что нужно для продолжения после сбоя, — здесь.

/// <summary>
/// Запуск синхронизации. Не больше одного идущего запуска (уникальный частичный индекс по Status = 'Running'): второй запуск — даже со второй
/// копии службы — получает «уже идёт». Зависший запуск без пульса закрывается.
/// </summary>
public class OneCSyncRun
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>incremental, historical, test, retry.</summary>
    public required string Kind { get; set; }

    /// <summary>auto — по расписанию, manual — командой администратора.</summary>
    public string Trigger { get; set; } = "auto";

    public Guid? StartedById { get; set; }
    public Guid? CommandId { get; set; }
    public Guid? HistoricalImportId { get; set; }
    public Guid? RetryOfId { get; set; }
    public required string Provider { get; set; }
    public string? WorkerId { get; set; }
    public string? InfobaseId { get; set; }

    /// <summary>Running, Succeeded, Partial, Failed, Cancelled.</summary>
    public string Status { get; set; } = "Running";

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset HeartbeatAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Step { get; set; }

    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Deleted { get; set; }
    public int Rejected { get; set; }
    public int Batches { get; set; }
    public int Errors { get; set; }
    public string? Message { get; set; }

    /// <summary>Шаги запуска (jsonb RunStep[]).</summary>
    public string? StepsJson { get; set; }
}

/// <summary>Курсор потока (изменения 1С, первичная загрузка справочника). Сдвигается только вместе с записью пакета.</summary>
public class OneCSyncCheckpoint
{
    public required string Stream { get; set; }
    public string OrganizationId { get; set; } = "";
    public string? Cursor { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? RunId { get; set; }
    public string? BatchKey { get; set; }
    public long Rows { get; set; }
}

/// <summary>Записанный пакет — ключ идемпотентности: повтор того же пакета не применяется второй раз.</summary>
public class OneCImportBatch
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string BatchKey { get; set; }
    public Guid RunId { get; set; }
    public required string Stream { get; set; }
    public string OrganizationId { get; set; } = "";
    public int Records { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Deleted { get; set; }
    public int Rejected { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public int DurationMs { get; set; }

    /// <summary>Отклонённые записи (jsonb) — их вернёт повтор пакета.</summary>
    public string? RejectionsJson { get; set; }
}

/// <summary>
/// Неуспешная операция (аналог очереди недоставленных): страница 1С не получена, пакет не записан или запись не прошла проверку.
/// Запись и пакет хранятся целиком — данные не теряются. Повтор — по кнопке или автоматически следующим запуском.
/// </summary>
public class OneCSyncFailure
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? RunId { get; set; }

    /// <summary>fetch, batch, record.</summary>
    public required string Kind { get; set; }

    public required string Stream { get; set; }
    public string? OrganizationId { get; set; }
    public DateOnly? Period { get; set; }
    public string? Cursor { get; set; }
    public string? EntityType { get; set; }
    public string? SourceEntityId { get; set; }
    public required string Error { get; set; }

    /// <summary>Запись или пакет (jsonb).</summary>
    public string? Payload { get; set; }

    public int Attempts { get; set; } = 1;

    /// <summary>Open — ждёт повтора, Retrying — администратор попросил повторить, Resolved — повтор удался, Ignored — отложено.</summary>
    public string Status { get; set; } = "Open";

    /// <summary>Одна и та же ошибка не плодит строк: отпечаток (вид, поток, организация, период, курсор, запись).</summary>
    public required string Fingerprint { get; set; }

    public DateTimeOffset FirstAt { get; set; }
    public DateTimeOffset LastAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>
/// Историческая загрузка за период: по месяцам, по организациям, по потокам (курсы, документы, проводки, остатки). Положение сохраняется
/// вместе с каждым пакетом — после сбоя загрузка продолжается с него; по каждому месяцу — сверка количества и сумм с 1С.
/// </summary>
public class OneCHistoricalImport
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int BatchSize { get; set; } = 500;

    /// <summary>Pending, Running, Succeeded, Failed, Cancelled.</summary>
    public string Status { get; set; } = "Pending";

    public Guid RequestedById { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool ReferencesDone { get; set; }

    public DateOnly? PositionPeriod { get; set; }
    public int PositionOrganization { get; set; }
    public string? PositionStream { get; set; }
    public string? PositionCursor { get; set; }

    public int PeriodsTotal { get; set; }
    public int PeriodsDone { get; set; }
    public long Records { get; set; }

    /// <summary>Сверки по месяцам (jsonb PeriodCheck[]).</summary>
    public string ChecksJson { get; set; } = "[]";

    public string? Message { get; set; }
}

/// <summary>Команда администратора службе: проверить подключение, тестовая синхронизация, «синхронизировать сейчас», история, повтор ошибок.</summary>
public class OneCWorkerCommand
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Kind { get; set; }
    public string? PayloadJson { get; set; }
    public Guid? RequestedById { get; set; }
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Pending, Taken, Done, Failed, Expired.</summary>
    public string Status { get; set; } = "Pending";

    public DateTimeOffset? TakenAt { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
    public string? ResultMessage { get; set; }
    public string? ResultJson { get; set; }
}

/// <summary>Состояние службы со слов самой службы (пульс раз в 15–30 с): адрес 1С, HTTPS, сертификат, авторизация — без секретов.</summary>
public class OneCWorkerState
{
    public required string WorkerId { get; set; }
    public string? Version { get; set; }
    public required string Provider { get; set; }
    public bool Enabled { get; set; }
    public bool Busy { get; set; }
    public Guid? CurrentRunId { get; set; }
    public DateTimeOffset? NextSyncAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? BaseUrl { get; set; }

    /// <summary>Последняя проверка подключения (jsonb ConnectionReport).</summary>
    public string? ConnectionJson { get; set; }

    public DateTimeOffset? ConnectionCheckedAt { get; set; }
}

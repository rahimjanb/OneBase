using System.Text.Json;

namespace OneBase.Finance.Integration.Contracts;

// Протокол «служба интеграции 1С → внутренний API OneBase» (/api/internal/onec). Все запросы делает служба; API отвечает данными
// и командами администратора. Ключ службы — заголовок X-OneBase-Integration-Token.

public static class IntegrationHeaders
{
    public const string Token = "X-OneBase-Integration-Token";
}

/// <summary>Курсор потока: сохраняется только в той же транзакции, что и пакет, — либо записано всё, либо ничего.</summary>
public sealed record ImportCheckpoint(string Stream, string OrganizationId, string? Cursor);

/// <summary>Снимок остатков за месяц: строки, которых нет в последнем полном снимке, удаляются после <see cref="Final"/>.</summary>
public sealed record BalanceSnapshot(string OrganizationId, DateOnly Period, Guid SnapshotId, bool Final, IReadOnlyList<string>? Accounts = null);

/// <summary>Положение исторической загрузки: сохраняется вместе с пакетом, по нему загрузка продолжается после сбоя.</summary>
public sealed record HistoricalPosition(Guid ImportId, DateOnly Period, int OrganizationIndex, string Stream, string? Cursor);

/// <summary>
/// Пакет записей. <see cref="BatchKey"/> — ключ идемпотентности: повтор того же пакета (обрыв связи после записи) ничего не меняет
/// и возвращает прежний итог.
/// </summary>
public sealed record ImportBatchRequest
{
    public required Guid RunId { get; init; }
    public required string BatchKey { get; init; }
    public required string Stream { get; init; }

    /// <summary>Организация пакета; "" — общий справочник базы. Записи другой организации отклоняются.</summary>
    public string? OrganizationId { get; init; }

    public List<ImportOrganization>? Organizations { get; init; }
    public List<ImportAccount>? Accounts { get; init; }
    public List<ImportCurrency>? Currencies { get; init; }
    public List<ImportExchangeRate>? ExchangeRates { get; init; }
    public List<ImportCounterparty>? Counterparties { get; init; }
    public List<ImportContract>? Contracts { get; init; }
    public List<ImportReference>? References { get; init; }
    public List<ImportItem>? Items { get; init; }
    public List<ImportDocument>? Documents { get; init; }
    public List<ImportEntrySet>? EntrySets { get; init; }
    public List<ImportBalance>? Balances { get; init; }
    public List<ImportDeletion>? Deletions { get; init; }

    public ImportCheckpoint? Checkpoint { get; init; }
    public BalanceSnapshot? Snapshot { get; init; }
    public HistoricalPosition? Historical { get; init; }

    public int Count =>
        (Organizations?.Count ?? 0) + (Accounts?.Count ?? 0) + (Currencies?.Count ?? 0) + (ExchangeRates?.Count ?? 0)
        + (Counterparties?.Count ?? 0) + (Contracts?.Count ?? 0) + (References?.Count ?? 0) + (Items?.Count ?? 0)
        + (Documents?.Count ?? 0) + (EntrySets?.Count ?? 0) + (Balances?.Count ?? 0) + (Deletions?.Count ?? 0);
}

public sealed record ImportRejection(string EntityType, string SourceId, string Reason);

public sealed record ImportBatchResult
{
    public required string BatchKey { get; init; }

    /// <summary>Пакет с этим ключом уже был записан — повтор ничего не изменил.</summary>
    public bool Replayed { get; init; }

    public int Created { get; init; }
    public int Updated { get; init; }
    public int Unchanged { get; init; }
    public int Deleted { get; init; }
    public int Rejected { get; init; }
    public List<ImportRejection> Rejections { get; init; } = [];
}

// ---------- Состояние службы и команды администратора ----------

/// <summary>Шаг диагностики подключения: сеть (TCP), TLS, авторизация, API.</summary>
public sealed record DiagnosticStep(string Code, bool Ok, string Message, int DurationMs);

/// <summary>Результат проверки подключения к 1С. Без пароля и токена: адрес — без логина в URL.</summary>
public sealed record ConnectionReport
{
    public DateTimeOffset CheckedAt { get; init; }
    public bool Ok { get; init; }
    public string? BaseUrl { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    public bool Https { get; init; }

    /// <summary>Сертификат проверен (цепочка и имя): true/false; null — до TLS не дошли.</summary>
    public bool? TlsValid { get; init; }

    public string? CertificateSubject { get; init; }
    public DateTimeOffset? CertificateExpires { get; init; }

    /// <summary>Авторизация: true — принята, false — отклонена, null — не проверялась.</summary>
    public bool? AuthOk { get; init; }

    public string? InfobaseId { get; init; }
    public string? InfobaseName { get; init; }
    public string? ApiVersion { get; init; }
    public string? Configuration { get; init; }
    public string? Message { get; init; }
    public List<DiagnosticStep> Steps { get; init; } = [];
}

public sealed record WorkerHeartbeat
{
    public required string WorkerId { get; init; }
    public string? Version { get; init; }

    /// <summary>mock — тестовый источник, http — настоящая 1С.</summary>
    public required string Provider { get; init; }

    /// <summary>OneC__Enabled у службы.</summary>
    public bool Enabled { get; init; }

    public bool Busy { get; init; }
    public Guid? CurrentRunId { get; init; }
    public DateTimeOffset? NextSyncAt { get; init; }
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Последняя проверка подключения (передаётся, когда изменилась).</summary>
    public ConnectionReport? Connection { get; init; }

    /// <summary>Адрес 1С из настроек службы (без логина и пароля).</summary>
    public string? BaseUrl { get; init; }
}

/// <summary>Настройки, которые служба берёт у OneBase: их меняет администратор на странице «1С».</summary>
public sealed record WorkerSettings
{
    public bool Paused { get; init; }
    public int IntervalMinutes { get; init; } = 30;
    public int BatchSize { get; init; } = 500;

    /// <summary>База 1С, данные которой уже лежат в OneBase. Другая база — загрузка останавливается до решения администратора.</summary>
    public string? AcceptedInfobaseId { get; init; }

    /// <summary>Незавершённая историческая загрузка — служба продолжает её и после перезапуска.</summary>
    public Guid? ActiveHistoricalImportId { get; init; }
}

public static class WorkerCommandKinds
{
    public const string TestConnection = "test_connection";
    public const string TestSync = "test_sync";
    public const string SyncNow = "sync_now";
    public const string Historical = "historical_import";
    public const string RetryFailures = "retry_failures";

    public static readonly IReadOnlyList<string> All = [TestConnection, TestSync, SyncNow, Historical, RetryFailures];
}

public sealed record WorkerCommand(Guid Id, string Kind, JsonElement? Payload, DateTimeOffset RequestedAt);

public sealed record HeartbeatResponse(WorkerSettings Settings, IReadOnlyList<WorkerCommand> Commands);

public sealed record CommandResult(bool Ok, string? Message, JsonElement? Result = null);

// ---------- Запуски синхронизации ----------

public static class RunKinds
{
    public const string Incremental = "incremental";
    public const string Historical = "historical";
    public const string Test = "test";
    public const string Retry = "retry";
}

public static class RunStatuses
{
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Partial = "Partial";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public sealed record StartRunRequest
{
    public required string Kind { get; init; }

    /// <summary>auto — по расписанию, manual — командой администратора.</summary>
    public string Trigger { get; init; } = "auto";

    public Guid? CommandId { get; init; }
    public Guid? HistoricalImportId { get; init; }
    public Guid? RetryOfId { get; init; }
    public string? InfobaseId { get; init; }
    public string? InfobaseName { get; init; }
    public required string Provider { get; init; }
    public string? WorkerId { get; init; }

    /// <summary>Запуск сорвался до начала (1С недоступна, авторизация): OneBase записывает его в журнал как неуспешный, без блокировки.</summary>
    public string? FailedBeforeStart { get; init; }
}

public sealed record StartRunResponse
{
    public bool Started { get; init; }
    public Guid? RunId { get; init; }

    /// <summary>already_running, paused, infobase_mismatch, infobase_missing.</summary>
    public string? Reason { get; init; }

    public string? Message { get; init; }
    public List<ImportCheckpoint> Checkpoints { get; init; } = [];
    public WorkerSettings? Settings { get; init; }
}

public sealed record RunStep
{
    public required string Stream { get; init; }
    public string? OrganizationId { get; init; }
    public int Rows { get; init; }
    public int Batches { get; init; }
    public string? Error { get; init; }
    public int DurationMs { get; init; }
}

public sealed record RunProgress(string? Step, string? Message);

/// <summary>Ответ на пульс запуска: Stop — администратор поставил паузу, запуск завершается на границе пакета.</summary>
public sealed record RunProgressResponse(bool Stop, string? Reason);

public sealed record FinishRunRequest
{
    public required string Status { get; init; }
    public string? Message { get; init; }
    public List<RunStep> Steps { get; init; } = [];
}

// ---------- Ошибки (очередь неуспешных операций) ----------

public static class FailureKinds
{
    /// <summary>Страница 1С не получена (сеть, тайм-аут, ошибка 1С): повтор — заново запросить ту же страницу.</summary>
    public const string Fetch = "fetch";

    /// <summary>Пакет не записан в OneBase: повтор — отправить сохранённый пакет.</summary>
    public const string Batch = "batch";

    /// <summary>OneBase отклонил нормализованную запись: она сохранена целиком (формат OneBase), повтор — отправить её снова.</summary>
    public const string Record = "record";

    /// <summary>Запись 1С не прошла проверку в службе: сохранена целиком (формат 1С), повтор — проверить заново.</summary>
    public const string Validation = "validation";

    /// <summary>Повторяются только по кнопке: записи надо сначала исправить в 1С.</summary>
    public static bool ManualOnly(string kind) => kind is Record or Validation;
}

public sealed record FailureReport
{
    public required Guid RunId { get; init; }
    public required string Kind { get; init; }
    public required string Stream { get; init; }
    public string? OrganizationId { get; init; }
    public DateOnly? Period { get; init; }
    public string? Cursor { get; init; }
    public string? EntityType { get; init; }
    public string? SourceId { get; init; }
    public required string Error { get; init; }

    /// <summary>Запись или пакет целиком — чтобы ничего не потерять и повторить без 1С.</summary>
    public JsonElement? Payload { get; init; }
}

public sealed record FailureItem
{
    public Guid Id { get; init; }
    public required string Kind { get; init; }
    public required string Stream { get; init; }
    public string? OrganizationId { get; init; }
    public DateOnly? Period { get; init; }
    public string? Cursor { get; init; }
    public string? EntityType { get; init; }
    public string? SourceId { get; init; }
    public int Attempts { get; init; }
    public JsonElement? Payload { get; init; }
}

public sealed record FailureOutcome(bool Resolved, string? Error);

// ---------- Историческая загрузка ----------

public static class HistoricalStatuses
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public sealed record HistoricalImportState
{
    public Guid Id { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public int BatchSize { get; init; }
    public required string Status { get; init; }
    public bool ReferencesDone { get; init; }
    public HistoricalPosition? Position { get; init; }
    public List<string> Organizations { get; init; } = [];
}

/// <summary>Сверка месяца: сколько записей и на какую сумму отдала 1С и сколько записано в OneBase.</summary>
public sealed record PeriodCheck
{
    public DateOnly Period { get; init; }
    public required string OrganizationId { get; init; }
    public required string Stream { get; init; }
    public int? ExpectedCount { get; init; }
    public decimal? ExpectedAmount { get; init; }
    public int ImportedCount { get; init; }
    public decimal ImportedAmount { get; init; }
    public bool Ok { get; init; }
    public string? Note { get; init; }
}

public sealed record HistoricalProgressRequest
{
    public HistoricalPosition? Position { get; init; }
    public bool ReferencesDone { get; init; }
    public PeriodCheck? Check { get; init; }

    /// <summary>Итог загрузки: Succeeded / Failed / Cancelled; null — загрузка продолжается.</summary>
    public string? Status { get; init; }

    public string? Message { get; init; }
}

/// <summary>Сколько записей потока организации за период лежит в OneBase — для сверки с итогами 1С.</summary>
public sealed record ImportedTotals(int Count, decimal Amount);

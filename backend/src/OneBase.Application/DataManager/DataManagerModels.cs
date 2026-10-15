using System.Text.Json;
using System.Text.Json.Serialization;
using OneBase.Domain.AI;
using OneBase.Domain.Sales;

namespace OneBase.Application.DataManager;

/// <summary>Настройки AI Data Manager (секция «DataManager»).</summary>
public sealed class DataManagerOptions
{
    public const string Section = "DataManager";

    /// <summary>Модель Claude; ключ Anthropic — из «Настройки → AI → Провайдеры» или ANTHROPIC_API_KEY.</summary>
    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>Глубина рассуждений: low, medium, high, xhigh, max.</summary>
    public string Effort { get; set; } = "high";

    public int MaxOutputTokens { get; set; } = 16_000;

    /// <summary>Сколько запросов к модели за один ход (инструменты → ответ).</summary>
    public int MaxSteps { get; set; } = 24;

    public int TurnTimeoutMinutes { get; set; } = 10;

    /// <summary>Пауза перед повтором запроса к Claude после временной ошибки (сверх повторов самого SDK).</summary>
    public int RetryDelaySeconds { get; set; } = 10;

    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;

    public int MaxRowsPerSheet { get; set; } = 20_000;

    public int MaxColumns { get; set; } = 60;

    /// <summary>Строк в одном плане изменений.</summary>
    public int MaxChangeItems { get; set; } = 5_000;
}

public sealed record DataSheetInfo(string Name, int Rows, int Columns, bool Truncated);

/// <summary>Тот же файл (по SHA-256) уже загружали — когда и в каких операциях он применён.</summary>
public sealed record DataFileDuplicate(Guid FileId, string FileName, DateTimeOffset UploadedAt, IReadOnlyList<Guid> AppliedChangeSets);

public sealed record DataFileView(
    Guid Id,
    string FileName,
    long SizeBytes,
    string Sha256,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DataSheetInfo> Sheets,
    IReadOnlyList<DataFileDuplicate> Duplicates);

public sealed record ChangeCounts(int Create, int Update, int Delete, int Unchanged, int Conflict)
{
    [JsonIgnore]
    public int Applicable => Create + Update + Delete;
}

/// <summary>Показатели региона или республики до и после — оценка по формулам OneBase (факт ÷ план).</summary>
public sealed record ImpactRow(string Name, decimal? PlanBefore, decimal? PlanAfter, decimal? FactKg, decimal? ExecutionBefore, decimal? ExecutionAfter);

public sealed record ChangeImpact(string Period, string? PlanSourceBefore, ImpactRow? Republic, IReadOnlyList<ImpactRow> Regions, string Note);

public sealed record PreviousVersionInfo(Guid ChangeSetId, string? FileName, DateTimeOffset? AppliedAt, int CorrectedRows);

public sealed record ChangeSetSummary(
    ChangeCounts Counts,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<RegionPlanTotal> Totals,
    IReadOnlyList<ChangeImpact> Impact,
    PreviousVersionInfo? Previous,
    string Source,
    string? FileName,
    string? Sheet,
    int SkippedEmpty,
    IReadOnlyList<string> Notes,
    PnlSummary? Pnl = null);

public sealed record DataChangeItemView(
    Guid Id,
    int Position,
    string EntityType,
    string Label,
    DataChangeAction Action,
    string? Before,
    string? After,
    string? SourceRef,
    string? Message,
    DataChangeItemStatus Status);

public sealed record DataChangeSetView(
    Guid Id,
    string Title,
    string Target,
    DataChangeSetStatus Status,
    string Mode,
    string? Period,
    string? PlanKind,
    DateTimeOffset CreatedAt,
    Guid UserId,
    string? UserName,
    Guid? ConversationId,
    Guid? FileId,
    string? FileName,
    JsonElement Summary,
    IReadOnlyList<DataChangeItemView> Items,
    int ItemCount,
    DateTimeOffset? AppliedAt,
    string? AppliedBy,
    JsonElement? Verification,
    DateTimeOffset? RolledBackAt,
    string? RolledBackBy,
    string? Error,
    bool CanApply,
    bool CanCancel,
    bool CanRollback);

public sealed record DataChangeSetSummaryView(
    Guid Id,
    string Title,
    string Target,
    DataChangeSetStatus Status,
    string? Period,
    string? PlanKind,
    DateTimeOffset CreatedAt,
    string? UserName,
    string? FileName,
    ChangeCounts? Counts,
    DateTimeOffset? AppliedAt,
    string? AppliedBy,
    DateTimeOffset? RolledBackAt);

/// <summary>
/// Кто готовит план: пользователь (из сессии, а не из ответа модели) и чат. Source — синхронизация из внешнего источника
/// («Google Sheets «План 2026» / План»): попадает в название операции вместо «из сообщения», источник плана — google_sheets.
/// </summary>
public sealed record DataActor(Guid UserId, Guid? ConversationId, string? Source = null)
{
    /// <summary>Источник значений без файла: сообщение пользователя или внешняя таблица.</summary>
    public string ValuesSource => Source is null ? "message" : "google_sheets";

    /// <summary>Хвост названия операции без файла.</summary>
    public string ValuesLabel => Source is null ? " · из сообщения" : $" · {Source}";
}

public sealed record RegionPlanRequest(
    PlanKind Kind,
    bool Replace,
    Guid? FileId,
    PlanSheetMapping? Mapping,
    IReadOnlyList<PlanValue>? Values,
    string? Note);

/// <summary>Итог применения или отката.</summary>
public sealed record ApplyOutcome(DataChangeSetStatus Status, int Applied, int Skipped, string Message, IReadOnlyList<string> Problems);

public sealed record StoredCheck(string Label, string? Expected, string? Actual, bool Ok);

public sealed record VerificationResult(
    DateTimeOffset CheckedAt,
    int Checked,
    int Mismatches,
    IReadOnlyList<StoredCheck> Problems,
    IReadOnlyList<ChangeImpact> Kpi,
    IReadOnlyList<string> Notes,
    IReadOnlyList<PnlMonthTotal>? Pnl = null);

/// <summary>
/// Применение и откат плана изменений — одной транзакцией, с проверкой, что данные не изменились после подготовки
/// (реализация в Infrastructure: транзакции есть только у конкретного DbContext).
/// </summary>
public interface IDataChangeApplier
{
    Task<ApplyOutcome> ApplyAsync(Guid changeSetId, Guid userId, CancellationToken ct = default);

    Task<ApplyOutcome> RollbackAsync(Guid changeSetId, Guid userId, CancellationToken ct = default);
}

public static class DataTargets
{
    public const string RegionPlans = "region_plans";
    public const string SalesTargets = "sales_targets";
    public const string RegionProfiles = "region_profiles";
    public const string Pnl = "pnl";

    public static string Label(string target) => target switch
    {
        RegionPlans => "Планы регионов",
        SalesTargets => "Цели KPI",
        RegionProfiles => "Дилеры и СВР регионов",
        Pnl => "P&L дилеров",
        _ => target,
    };

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

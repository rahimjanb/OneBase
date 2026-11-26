using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.OneC;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Application.Finance;

public sealed record OneCWorkerView(
    string WorkerId, string Provider, string? Version, bool Online, bool Enabled, bool Busy, DateTimeOffset LastSeenAt, DateTimeOffset? NextSyncAt,
    string? BaseUrl, ConnectionReport? Connection);

public sealed record OneCRunView(
    Guid Id, string Kind, string Trigger, string Status, string Provider, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, int? DurationSeconds,
    string? Step, int Created, int Updated, int Unchanged, int Deleted, int Rejected, int Batches, int Errors, string? Message);

public sealed record OneCRunDetails(OneCRunView Run, IReadOnlyList<RunStep> Steps);

public sealed record OneCHistoricalView(
    Guid Id, DateOnly From, DateOnly To, int BatchSize, string Status, DateTimeOffset RequestedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    int PeriodsTotal, int PeriodsDone, long Records, DateOnly? CurrentPeriod, string? CurrentStream, bool ReferencesDone, string? Message,
    IReadOnlyList<PeriodCheck> Checks);

public sealed record OneCCommandView(Guid Id, string Kind, string Status, DateTimeOffset RequestedAt, DateTimeOffset? DoneAt, string? Message);

public sealed record OneCCounts(int Organizations, int Accounts, int Counterparties, int Contracts, int Items, int References, int Rates,
    int Documents, int Entries, int Balances, int Changes);

/// <summary>Состояние интеграции для страницы «Настройки → Интеграции → Финансы → 1С». Секретов нет: ни пароля 1С, ни ключа службы.</summary>
public sealed record OneCStatusView
{
    /// <summary>not_configured, waiting_worker, offline, error, paused, mismatch, connected.</summary>
    public required string State { get; init; }

    public required string StateText { get; init; }
    public required OneCSettings Settings { get; init; }
    public OneCWorkerView? Worker { get; init; }
    public OneCRunView? Running { get; init; }
    public OneCRunView? LastRun { get; init; }
    public OneCRunView? LastSuccess { get; init; }
    public DateTimeOffset? NextSyncAt { get; init; }
    public int OpenFailures { get; init; }
    public int RejectedRecords { get; init; }
    public OneCHistoricalView? Historical { get; init; }
    public required OneCCounts Counts { get; init; }
    public IReadOnlyList<OneCCommandView> Commands { get; init; } = [];

    /// <summary>Загруженные организации 1С — для выбора, какие загружать.</summary>
    public IReadOnlyList<OneCOrganizationOption> Organizations { get; init; } = [];
}

public sealed record OneCOrganizationOption(string Id, string Name, string? Inn, bool Deleted);

public sealed record OneCFailureView(
    Guid Id, string Kind, string Stream, string? OrganizationId, DateOnly? Period, string? EntityType, string? SourceId, string Error, int Attempts,
    string Status, DateTimeOffset FirstAt, DateTimeOffset LastAt, DateTimeOffset? ResolvedAt, bool HasPayload);

public sealed record OneCChangeView(DateTimeOffset At, string EntityType, string SourceEntityId, string Action, string? Changes, Guid? RunId);

public sealed record OneCPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Действия администратора с интеграцией 1С (право integrations.manage). Служба получает их командами с пульсом.</summary>
public sealed class OneCAdminService(IAppDbContext db, IOneCSettingsStore settings, TimeProvider clock)
{
    /// <summary>Служба считается на связи, если пульс был не позже этого.</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(2);

    public const int MaxHistoryYears = 10;

    public async Task<OneCStatusView> StatusAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var s = await settings.GetAsync(ct);
        var workerRow = await db.OneCWorkerStates.AsNoTracking().OrderByDescending(w => w.LastSeenAt).FirstOrDefaultAsync(ct);
        var worker = workerRow is null ? null : Worker(workerRow, now);
        var runs = db.OneCSyncRuns.AsNoTracking();
        var running = await runs.Where(r => r.Status == RunStatuses.Running).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
        var last = await runs.Where(r => r.Status != RunStatuses.Running && r.Kind != RunKinds.Test).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
        var success = await runs.Where(r => r.Status == RunStatuses.Succeeded && r.Kind != RunKinds.Test).OrderByDescending(r => r.FinishedAt).FirstOrDefaultAsync(ct);
        var job = await db.OneCHistoricalImports.AsNoTracking().OrderByDescending(j => j.RequestedAt).FirstOrDefaultAsync(ct);
        var failures = db.OneCSyncFailures.AsNoTracking().Where(f => f.Status == "Open" || f.Status == "Retrying");
        var commands = await db.OneCWorkerCommands.AsNoTracking().OrderByDescending(c => c.RequestedAt).Take(8)
            .Select(c => new OneCCommandView(c.Id, c.Kind, c.Status, c.RequestedAt, c.DoneAt, c.ResultMessage)).ToListAsync(ct);

        var mismatch = s.PendingInfobaseId is not null && s.PendingInfobaseId != s.AcceptedInfobaseId;
        var (state, text) = (worker, s) switch
        {
            _ when mismatch => ("mismatch", $"Служба подключена к другой базе 1С («{s.PendingInfobaseName ?? s.PendingInfobaseId}»), а в OneBase — данные базы «{s.AcceptedInfobaseName ?? s.AcceptedInfobaseId}». Загрузка остановлена."),
            _ when !s.HasToken => ("not_configured", "Не настроено: создайте ключ службы интеграции и запустите службу."),
            (null, _) => ("waiting_worker", "Служба интеграции ещё ни разу не выходила на связь."),
            ({ Online: false }, _) => ("offline", $"Служба интеграции не на связи с {worker!.LastSeenAt.ToOffset(OneCFormat.Offset):dd.MM HH:mm}."),
            ({ Connection: { Ok: false } c }, _) => ("error", c.Message ?? "Нет подключения к 1С."),
            _ when s.Paused => ("paused", "Синхронизация приостановлена."),
            ({ Connection: null }, _) => ("waiting_worker", "Служба на связи, проверка подключения к 1С ещё не выполнялась."),
            _ => ("connected", worker!.Provider == "mock" ? "Подключено к тестовому источнику (демо-данные 1С)." : "Подключено к 1С."),
        };

        return new OneCStatusView
        {
            State = state, StateText = text, Settings = s, Worker = worker, Running = running is null ? null : Run(running), LastRun = last is null ? null : Run(last),
            LastSuccess = success is null ? null : Run(success), NextSyncAt = s.Paused ? null : worker?.NextSyncAt,
            OpenFailures = await failures.CountAsync(f => f.Kind != FailureKinds.Record && f.Kind != FailureKinds.Validation, ct),
            RejectedRecords = await failures.CountAsync(f => f.Kind == FailureKinds.Record || f.Kind == FailureKinds.Validation, ct),
            Historical = job is null ? null : Historical(job), Counts = await CountsAsync(ct), Commands = commands,
            Organizations = await db.OneCOrganizations.AsNoTracking().OrderBy(o => o.Name)
                .Select(o => new OneCOrganizationOption(o.SourceEntityId, o.Name, o.Inn, o.IsDeleted)).ToListAsync(ct),
        };
    }

    public async Task<OneCCounts> CountsAsync(CancellationToken ct) => new(
        await db.OneCOrganizations.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCAccounts.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCCounterparties.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCContracts.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCItems.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCReferences.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCExchangeRates.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCDocuments.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCEntries.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCBalances.CountAsync(x => !x.IsDeleted, ct),
        await db.OneCChanges.CountAsync(ct));

    /// <summary>Команда службе. Такая же команда, ещё не забранная службой, не дублируется.</summary>
    public async Task<OneCCommandView> RequestAsync(string kind, object? payload, Guid userId, CancellationToken ct)
    {
        if (!WorkerCommandKinds.All.Contains(kind))
        {
            throw new OneCImportException($"Неизвестная команда: {kind}.");
        }

        var json = payload is null ? null : JsonSerializer.Serialize(payload, OneCFormat.Json);
        var existing = await db.OneCWorkerCommands.FirstOrDefaultAsync(c => c.Kind == kind && c.Status == "Pending", ct);
        if (existing is not null && existing.PayloadJson == json)
        {
            return Command(existing);
        }

        var command = new OneCWorkerCommand { Kind = kind, PayloadJson = json, RequestedById = userId, RequestedAt = clock.GetUtcNow() };
        db.OneCWorkerCommands.Add(command);
        await db.SaveChangesAsync(ct);
        return Command(command);
    }

    public async Task<OneCHistoricalView> StartHistoricalAsync(DateOnly from, DateOnly to, int? batchSize, Guid userId, CancellationToken ct)
    {
        var today = OneCFormat.LocalDate(clock.GetUtcNow());
        if (from > to)
        {
            throw new OneCImportException("Начало периода позже конца.");
        }

        if (to > today)
        {
            throw new OneCImportException("Конец периода — в будущем.");
        }

        if (from < today.AddYears(-MaxHistoryYears))
        {
            throw new OneCImportException($"Историю можно загрузить не дальше {MaxHistoryYears} лет назад.");
        }

        var s = await settings.GetAsync(ct);
        var job = new OneCHistoricalImport
        {
            From = OneCFormat.MonthStart(from), To = to, BatchSize = OneCSettings.ClampBatch(batchSize ?? s.BatchSize), RequestedById = userId,
            RequestedAt = clock.GetUtcNow(), PeriodsTotal = Months(OneCFormat.MonthStart(from), to),
        };
        db.OneCHistoricalImports.Add(job);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw new OneCConflictException("Историческая загрузка уже запущена. Дождитесь окончания или отмените её.");
        }

        await RequestAsync(WorkerCommandKinds.Historical, new { importId = job.Id }, userId, ct);
        return Historical(job);
    }

    public async Task<bool> CancelHistoricalAsync(CancellationToken ct)
    {
        var job = await db.OneCHistoricalImports.FirstOrDefaultAsync(j => j.Status == HistoricalStatuses.Pending || j.Status == HistoricalStatuses.Running, ct);
        if (job is null)
        {
            return false;
        }

        job.Status = HistoricalStatuses.Cancelled;
        job.FinishedAt = clock.GetUtcNow();
        job.Message = "Отменена администратором. Загруженные месяцы сохранены.";
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Журнал запусков с отбором: дни начала (по Ташкенту, включительно), статус, вид (incremental, historical, test, retry).</summary>
    public async Task<OneCPage<OneCRunView>> RunsAsync(int page, int pageSize, CancellationToken ct, DateOnly? from = null, DateOnly? to = null,
        string? status = null, string? kind = null)
    {
        (page, pageSize) = (Math.Max(1, page), Math.Clamp(pageSize, 10, 100));
        var q = db.OneCSyncRuns.AsNoTracking();
        if (from is { } f)
        {
            var at = Tashkent(f);
            q = q.Where(r => r.StartedAt >= at);
        }

        if (to is { } t)
        {
            var at = Tashkent(t.AddDays(1));
            q = q.Where(r => r.StartedAt < at);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            q = q.Where(r => r.Kind == kind);
        }

        var rows = await q.OrderByDescending(r => r.StartedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new OneCPage<OneCRunView>(rows.Select(Run).ToList(), await q.CountAsync(ct), page, pageSize);
    }

    public async Task<OneCRunDetails?> RunAsync(Guid id, CancellationToken ct)
    {
        var run = await db.OneCSyncRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? null : new OneCRunDetails(Run(run),
            run.StepsJson is null ? [] : JsonSerializer.Deserialize<List<RunStep>>(run.StepsJson, OneCFormat.Json) ?? []);
    }

    /// <summary>Начало дня по Ташкенту.</summary>
    private static DateTimeOffset Tashkent(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5));

    /// <summary>Ошибки с отбором: состояние (open / resolved), поток (начало имени: history:documents, versions:document, ref:…), дни.</summary>
    public async Task<OneCPage<OneCFailureView>> FailuresAsync(string? status, int page, int pageSize, CancellationToken ct, string? stream = null,
        DateOnly? from = null, DateOnly? to = null)
    {
        (page, pageSize) = (Math.Max(1, page), Math.Clamp(pageSize, 10, 100));
        var q = db.OneCSyncFailures.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(stream))
        {
            q = q.Where(x => x.Stream.StartsWith(stream));
        }

        if (from is { } f)
        {
            var at = Tashkent(f);
            q = q.Where(x => x.LastAt >= at);
        }

        if (to is { } t)
        {
            var at = Tashkent(t.AddDays(1));
            q = q.Where(x => x.LastAt < at);
        }

        q = status switch
        {
            "open" => q.Where(f => f.Status == "Open" || f.Status == "Retrying"),
            "resolved" => q.Where(f => f.Status == "Resolved" || f.Status == "Ignored"),
            _ => q,
        };
        var rows = await q.OrderByDescending(f => f.LastAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new OneCFailureView(f.Id, f.Kind, f.Stream, f.OrganizationId, f.Period, f.EntityType, f.SourceEntityId, f.Error, f.Attempts, f.Status,
                f.FirstAt, f.LastAt, f.ResolvedAt, f.Payload != null)).ToListAsync(ct);
        return new OneCPage<OneCFailureView>(rows, await q.CountAsync(ct), page, pageSize);
    }

    /// <summary>Повторить неуспешные операции: выбранные или все открытые. Служба повторит их по команде.</summary>
    public async Task<int> RetryFailuresAsync(IReadOnlyCollection<Guid>? ids, Guid userId, CancellationToken ct)
    {
        var q = db.OneCSyncFailures.Where(f => f.Status == "Open" || f.Status == "Ignored");
        if (ids is { Count: > 0 })
        {
            q = q.Where(f => ids.Contains(f.Id));
        }
        else
        {
            q = q.Where(f => f.Status == "Open");
        }

        var rows = await q.ToListAsync(ct);
        foreach (var f in rows)
        {
            f.Status = "Retrying";
        }

        await db.SaveChangesAsync(ct);
        if (rows.Count > 0)
        {
            await RequestAsync(WorkerCommandKinds.RetryFailures, null, userId, ct);
        }

        return rows.Count;
    }

    public async Task<bool> IgnoreFailureAsync(Guid id, CancellationToken ct)
    {
        var f = await db.OneCSyncFailures.FirstOrDefaultAsync(x => x.Id == id && (x.Status == "Open" || x.Status == "Retrying"), ct);
        if (f is null)
        {
            return false;
        }

        f.Status = "Ignored";
        f.ResolvedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<OneCPage<OneCChangeView>> ChangesAsync(string? entityType, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = (Math.Max(1, page), Math.Clamp(pageSize, 10, 100));
        var q = db.OneCChanges.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            q = q.Where(c => c.EntityType == entityType);
        }

        var rows = await q.OrderByDescending(c => c.At).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new OneCChangeView(c.At, c.EntityType, c.SourceEntityId, c.Action, c.Changes, c.RunId)).ToListAsync(ct);
        return new OneCPage<OneCChangeView>(rows, await q.CountAsync(ct), page, pageSize);
    }

    /// <summary>
    /// Сменить базу 1С: загруженные данные прежней базы (схема onec), курсоры, очередь ошибок и незавершённые загрузки удаляются; журнал запусков
    /// остаётся. Нужна, когда к службе подключили другую базу (например, после тестовой копии — рабочую). Только при остановленной синхронизации.
    /// </summary>
    public async Task<int> ClearDataAsync(CancellationToken ct)
    {
        if (await db.OneCSyncRuns.AnyAsync(r => r.Status == RunStatuses.Running, ct))
        {
            throw new OneCConflictException("Идёт синхронизация — дождитесь её окончания.");
        }

        var removed = 0;
        removed += await db.OneCDocumentLines.ExecuteDeleteAsync(ct);
        removed += await db.OneCDocuments.ExecuteDeleteAsync(ct);
        removed += await db.OneCEntries.ExecuteDeleteAsync(ct);
        removed += await db.OneCBalances.ExecuteDeleteAsync(ct);
        removed += await db.OneCExchangeRates.ExecuteDeleteAsync(ct);
        removed += await db.OneCContracts.ExecuteDeleteAsync(ct);
        removed += await db.OneCCounterparties.ExecuteDeleteAsync(ct);
        removed += await db.OneCItems.ExecuteDeleteAsync(ct);
        removed += await db.OneCReferences.ExecuteDeleteAsync(ct);
        removed += await db.OneCAccounts.ExecuteDeleteAsync(ct);
        removed += await db.OneCCurrencies.ExecuteDeleteAsync(ct);
        removed += await db.OneCOrganizations.ExecuteDeleteAsync(ct);
        await db.OneCChanges.ExecuteDeleteAsync(ct);
        await db.OneCSyncCheckpoints.ExecuteDeleteAsync(ct);
        await db.OneCSyncFailures.ExecuteDeleteAsync(ct);
        await db.OneCImportBatches.ExecuteDeleteAsync(ct);
        var active = await db.OneCHistoricalImports.Where(j => j.Status == HistoricalStatuses.Pending || j.Status == HistoricalStatuses.Running).ToListAsync(ct);
        foreach (var job in active)
        {
            job.Status = HistoricalStatuses.Cancelled;
            job.FinishedAt = clock.GetUtcNow();
            job.Message = "Отменена: данные 1С очищены для подключения другой базы.";
        }

        await db.SaveChangesAsync(ct);
        await settings.AcceptInfobaseAsync(null, null, false, ct);
        return removed;
    }

    public static int Months(DateOnly from, DateOnly to) => Math.Max(0, (to.Year - from.Year) * 12 + to.Month - from.Month + 1);

    private static OneCWorkerView Worker(OneCWorkerState w, DateTimeOffset now) => new(
        w.WorkerId, w.Provider, w.Version, now - w.LastSeenAt <= OnlineWindow, w.Enabled, w.Busy, w.LastSeenAt, w.NextSyncAt, w.BaseUrl,
        w.ConnectionJson is null ? null : JsonSerializer.Deserialize<ConnectionReport>(w.ConnectionJson, OneCFormat.Json));

    public static OneCRunView Run(OneCSyncRun r) => new(
        r.Id, r.Kind, r.Trigger, r.Status, r.Provider, r.StartedAt, r.FinishedAt,
        r.FinishedAt is { } f ? (int)Math.Round((f - r.StartedAt).TotalSeconds) : null, r.Step,
        r.Created, r.Updated, r.Unchanged, r.Deleted, r.Rejected, r.Batches, r.Errors, r.Message);

    public static OneCHistoricalView Historical(OneCHistoricalImport j) => new(
        j.Id, j.From, j.To, j.BatchSize, j.Status, j.RequestedAt, j.StartedAt, j.FinishedAt, j.PeriodsTotal, j.PeriodsDone, j.Records,
        j.PositionPeriod, j.PositionStream, j.ReferencesDone, j.Message,
        JsonSerializer.Deserialize<List<PeriodCheck>>(j.ChecksJson, OneCFormat.Json) ?? []);

    private static OneCCommandView Command(OneCWorkerCommand c) => new(c.Id, c.Kind, c.Status, c.RequestedAt, c.DoneAt, c.ResultMessage);
}

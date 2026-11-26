using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.OneC;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Application.Finance;

/// <summary>
/// Управление синхронизацией 1С со стороны OneBase: пульс службы и команды администратора, запуски (не больше одного идущего), курсоры,
/// очередь неуспешных операций и историческая загрузка. Служба интеграции хранит у себя только учётные данные 1С.
/// </summary>
public sealed class OneCSyncService(IAppDbContext db, IOneCSettingsStore settings, TimeProvider clock)
{
    /// <summary>Запуск без пульса дольше этого — служба остановилась на полпути: запуск закрывается, курсоры остаются на последнем пакете.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    /// <summary>Команды, которые служба не забрала за сутки, снимаются.</summary>
    public static readonly TimeSpan CommandLifetime = TimeSpan.FromDays(1);

    /// <summary>Неуспешная страница или пакет повторяются автоматически не чаще: 5 мин × 2^(попытка-1), до 6 попыток.</summary>
    public const int MaxAutoAttempts = 6;

    // ---------- Пульс службы и команды ----------

    public async Task<HeartbeatResponse> HeartbeatAsync(WorkerHeartbeat beat, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var workerId = OneCFormat.Short(beat.WorkerId, 100);
        var state = await db.OneCWorkerStates.FirstOrDefaultAsync(w => w.WorkerId == workerId, ct);
        if (state is null)
        {
            state = new OneCWorkerState { WorkerId = workerId, Provider = beat.Provider };
            db.OneCWorkerStates.Add(state);
        }

        state.Version = beat.Version is null ? null : OneCFormat.Short(beat.Version, 50);
        state.Provider = OneCFormat.Short(beat.Provider, 16);
        state.Enabled = beat.Enabled;
        state.Busy = beat.Busy;
        state.CurrentRunId = beat.CurrentRunId;
        state.NextSyncAt = beat.NextSyncAt;
        state.StartedAt = beat.StartedAt;
        state.LastSeenAt = now;
        state.BaseUrl = beat.BaseUrl is null ? null : OneCFormat.Short(beat.BaseUrl, 500);
        if (beat.Connection is { } connection)
        {
            state.ConnectionJson = JsonSerializer.Serialize(connection, OneCFormat.Json);
            state.ConnectionCheckedAt = connection.CheckedAt;
        }

        await CloseStaleRunsAsync(now, ct);
        var expired = await db.OneCWorkerCommands.Where(c => c.Status == "Pending" && c.RequestedAt < now - CommandLifetime).ToListAsync(ct);
        foreach (var command in expired)
        {
            command.Status = "Expired";
            command.DoneAt = now;
            command.ResultMessage = "Служба интеграции не забрала команду за сутки.";
        }

        var pending = await db.OneCWorkerCommands.Where(c => c.Status == "Pending").OrderBy(c => c.RequestedAt).Take(10).ToListAsync(ct);
        foreach (var command in pending)
        {
            command.Status = "Taken";
            command.TakenAt = now;
        }

        await db.SaveChangesAsync(ct);
        return new HeartbeatResponse(await WorkerSettingsAsync(ct), pending.Select(c => new WorkerCommand(c.Id, c.Kind,
            c.PayloadJson is null ? null : JsonSerializer.Deserialize<JsonElement>(c.PayloadJson), c.RequestedAt)).ToList());
    }

    public async Task CompleteCommandAsync(Guid id, CommandResult result, CancellationToken ct)
    {
        var command = await db.OneCWorkerCommands.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new OneCImportException("Команда не найдена.");
        command.Status = result.Ok ? "Done" : "Failed";
        command.DoneAt = clock.GetUtcNow();
        command.ResultMessage = result.Message is null ? null : OneCFormat.Short(result.Message, 2000);
        command.ResultJson = result.Result is { } json ? json.GetRawText() : null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<WorkerSettings> WorkerSettingsAsync(CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        var active = await db.OneCHistoricalImports.AsNoTracking()
            .Where(j => j.Status == HistoricalStatuses.Pending || j.Status == HistoricalStatuses.Running).Select(j => (Guid?)j.Id).FirstOrDefaultAsync(ct);
        return new WorkerSettings
        {
            Paused = s.Paused, IntervalMinutes = s.IntervalMinutes, BatchSize = s.BatchSize, AcceptedInfobaseId = s.AcceptedInfobaseId, ActiveHistoricalImportId = active,
            ConnectionVersion = s.ConnectionVersion, OrganizationIds = s.OrganizationIds,
            Modules = s.Modules.Count == OneCModules.All.Count ? [] : s.Modules,
        };
    }

    /// <summary>Подключение к 1С со страницы «1С» с секретами — только службе (внутренний API, ключ службы).</summary>
    public async Task<WorkerConnection> ConnectionAsync(CancellationToken ct)
    {
        var c = await settings.ConnectionAsync(ct);
        return new WorkerConnection
        {
            BaseUrl = c.BaseUrl, Username = c.Username, Password = c.Password, ApiSecret = c.ApiSecret, CaCertificatePem = c.CaCertificatePem, Version = c.Version,
        };
    }

    // ---------- Запуски ----------

    public async Task<StartRunResponse> StartRunAsync(StartRunRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var s = await settings.GetAsync(ct);
        var workerSettings = await WorkerSettingsAsync(ct);
        if (request.FailedBeforeStart is { } failure)
        {
            // В журнал — как неуспешный запуск (без блокировки): администратор видит, что обмен не состоялся и почему.
            db.OneCSyncRuns.Add(new OneCSyncRun
            {
                Kind = request.Kind, Trigger = request.Trigger, CommandId = request.CommandId, HistoricalImportId = request.HistoricalImportId,
                Provider = OneCFormat.Short(request.Provider, 16), WorkerId = request.WorkerId is null ? null : OneCFormat.Short(request.WorkerId, 100),
                Status = RunStatuses.Failed, StartedAt = now, HeartbeatAt = now, FinishedAt = now, Errors = 1, Message = OneCFormat.Short(failure, 2000),
            });
            await db.SaveChangesAsync(ct);
            return new StartRunResponse { Started = false, Reason = "failed_before_start", Message = failure, Settings = workerSettings };
        }

        if (s.Paused && request.Trigger == "auto")
        {
            return new StartRunResponse { Started = false, Reason = "paused", Message = "Синхронизация приостановлена администратором.", Settings = workerSettings };
        }

        if (string.IsNullOrWhiteSpace(request.InfobaseId))
        {
            return new StartRunResponse { Started = false, Reason = "infobase_missing", Message = "1С не сообщила идентификатор информационной базы.", Settings = workerSettings };
        }

        if (s.AcceptedInfobaseId is null)
        {
            // Первое подключение: принимаем эту базу.
            await settings.AcceptInfobaseAsync(request.InfobaseId, request.InfobaseName, request.Provider == "mock", ct);
            workerSettings = await WorkerSettingsAsync(ct);
        }
        else if (s.AcceptedInfobaseId != request.InfobaseId)
        {
            if (s.PendingInfobaseId != request.InfobaseId)
            {
                await settings.NotePendingInfobaseAsync(request.InfobaseId, request.InfobaseName, ct);
            }

            return new StartRunResponse
            {
                Started = false, Reason = "infobase_mismatch", Settings = workerSettings,
                Message = $"Подключена другая база 1С ({OneCFormat.Short(request.InfobaseName ?? request.InfobaseId, 100)}), а в OneBase — данные базы «{s.AcceptedInfobaseName ?? s.AcceptedInfobaseId}». "
                    + "Загрузка остановлена, чтобы не смешать данные. Решение — в «Настройки → Интеграции → Финансы → 1С».",
            };
        }

        await CloseStaleRunsAsync(now, ct);
        var run = new OneCSyncRun
        {
            Kind = request.Kind, Trigger = request.Trigger, CommandId = request.CommandId, HistoricalImportId = request.HistoricalImportId,
            RetryOfId = request.RetryOfId, Provider = OneCFormat.Short(request.Provider, 16), WorkerId = request.WorkerId is null ? null : OneCFormat.Short(request.WorkerId, 100),
            InfobaseId = OneCFormat.Short(request.InfobaseId, 100), StartedAt = now, HeartbeatAt = now, Status = RunStatuses.Running,
        };
        if (request.CommandId is { } commandId)
        {
            run.StartedById = await db.OneCWorkerCommands.Where(c => c.Id == commandId).Select(c => c.RequestedById).FirstOrDefaultAsync(ct);
        }

        db.OneCSyncRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Уникальный частичный индекс: другой запуск уже идёт (в том числе со второй копии службы).
            db.ChangeTracker.Clear();
            var running = await db.OneCSyncRuns.AsNoTracking().Where(r => r.Status == RunStatuses.Running).Select(r => new { r.Kind, r.StartedAt }).FirstOrDefaultAsync(ct);
            return new StartRunResponse
            {
                Started = false, Reason = "already_running", Settings = workerSettings,
                Message = running is null ? "Синхронизация уже идёт." : $"Уже идёт синхронизация ({running.Kind}) с {running.StartedAt.ToOffset(OneCFormat.Offset):HH:mm}.",
            };
        }

        var checkpoints = await db.OneCSyncCheckpoints.AsNoTracking()
            .Select(x => new ImportCheckpoint(x.Stream, x.OrganizationId, x.Cursor)).ToListAsync(ct);
        return new StartRunResponse { Started = true, RunId = run.Id, Checkpoints = checkpoints, Settings = workerSettings };
    }

    /// <summary>Пульс запуска. Stop — администратор поставил паузу (для автоматических запусков) или запуск уже закрыт.</summary>
    public async Task<RunProgressResponse> ProgressAsync(Guid runId, RunProgress progress, CancellationToken ct)
    {
        var run = await db.OneCSyncRuns.FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new OneCImportException("Запуск не найден.");
        if (run.Status != RunStatuses.Running)
        {
            return new RunProgressResponse(true, "Запуск закрыт: " + (run.Message ?? run.Status));
        }

        run.HeartbeatAt = clock.GetUtcNow();
        run.Step = progress.Step is null ? run.Step : OneCFormat.Short(progress.Step, 200);
        await db.SaveChangesAsync(ct);
        var s = await settings.GetAsync(ct);
        return s.Paused && run.Trigger == "auto" ? new RunProgressResponse(true, "Синхронизация приостановлена администратором.") : new RunProgressResponse(false, null);
    }

    public async Task FinishRunAsync(Guid runId, FinishRunRequest request, CancellationToken ct)
    {
        var run = await db.OneCSyncRuns.FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new OneCImportException("Запуск не найден.");
        if (run.Status != RunStatuses.Running)
        {
            return;
        }

        run.Status = request.Status is RunStatuses.Succeeded or RunStatuses.Partial or RunStatuses.Failed or RunStatuses.Cancelled ? request.Status : RunStatuses.Failed;
        run.FinishedAt = clock.GetUtcNow();
        run.HeartbeatAt = run.FinishedAt.Value;
        run.Message = request.Message is null ? null : OneCFormat.Short(request.Message, 2000);
        run.Errors = request.Steps.Count(s => s.Error is not null);
        run.StepsJson = JsonSerializer.Serialize(request.Steps, OneCFormat.Json);
        run.Step = null;
        await db.SaveChangesAsync(ct);
    }

    private async Task CloseStaleRunsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var stale = await db.OneCSyncRuns.Where(r => r.Status == RunStatuses.Running && r.HeartbeatAt < now - StaleAfter).ToListAsync(ct);
        foreach (var run in stale)
        {
            run.Status = RunStatuses.Failed;
            run.FinishedAt = now;
            run.Message = "Служба интеграции перестала отвечать во время синхронизации. Записанные пакеты сохранены, следующий запуск продолжит с последнего курсора.";
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    // ---------- Ошибки ----------

    public async Task<Guid> ReportFailureAsync(FailureReport report, CancellationToken ct)
    {
        var id = await OneCFailures.ReportAsync(db, clock, report, ct);
        await db.SaveChangesAsync(ct);
        return id;
    }

    /// <summary>
    /// Что служба должна повторить сейчас: всё, что администратор попросил повторить, и неуспешные страницы и пакеты, у которых подошло время
    /// автоматического повтора. Отклонённые записи сами не повторяются — только по кнопке (их надо исправить в 1С).
    /// </summary>
    public async Task<List<FailureItem>> DueFailuresAsync(int limit, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var candidates = await db.OneCSyncFailures.AsNoTracking()
            .Where(f => f.Status == "Retrying" || (f.Status == "Open" && f.Kind != FailureKinds.Record && f.Kind != FailureKinds.Validation && f.Attempts < MaxAutoAttempts))
            .OrderBy(f => f.LastAt).Take(limit * 4).ToListAsync(ct);
        return candidates
            .Where(f => f.Status == "Retrying" || f.LastAt + TimeSpan.FromMinutes(5 * Math.Pow(2, f.Attempts - 1)) <= now)
            .Take(limit)
            .Select(f => new FailureItem
            {
                Id = f.Id, Kind = f.Kind, Stream = f.Stream, OrganizationId = f.OrganizationId, Period = f.Period, Cursor = f.Cursor, EntityType = f.EntityType,
                SourceId = f.SourceEntityId, Attempts = f.Attempts, Payload = f.Payload is null ? null : JsonSerializer.Deserialize<JsonElement>(f.Payload),
            }).ToList();
    }

    public async Task FailureOutcomeAsync(Guid id, FailureOutcome outcome, CancellationToken ct)
    {
        var f = await db.OneCSyncFailures.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new OneCImportException("Ошибка не найдена.");
        var now = clock.GetUtcNow();
        f.LastAt = now;
        if (outcome.Resolved)
        {
            f.Status = "Resolved";
            f.ResolvedAt = now;
        }
        else
        {
            f.Status = "Open";
            f.Attempts++;
            f.Error = OneCFormat.Short(outcome.Error ?? f.Error, 2000);
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- Историческая загрузка ----------

    public async Task<HistoricalImportState> HistoricalAsync(Guid id, CancellationToken ct)
    {
        var job = await db.OneCHistoricalImports.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw new OneCImportException("Историческая загрузка не найдена.");
        var organizations = await db.OneCOrganizations.AsNoTracking().Where(o => !o.IsDeleted).OrderBy(o => o.SourceEntityId).Select(o => o.SourceEntityId).ToListAsync(ct);
        // Только выбранные на странице «1С» организации (пусто — все).
        var chosen = (await settings.GetAsync(ct)).OrganizationIds;
        if (chosen.Count > 0)
        {
            organizations = organizations.Where(chosen.Contains).ToList();
        }

        return new HistoricalImportState
        {
            Id = job.Id, From = job.From, To = job.To, BatchSize = job.BatchSize, Status = job.Status, ReferencesDone = job.ReferencesDone,
            Organizations = organizations,
            Position = job.PositionPeriod is { } period && job.PositionStream is { } stream
                ? new HistoricalPosition(job.Id, period, job.PositionOrganization, stream, job.PositionCursor)
                : null,
        };
    }

    public async Task HistoricalProgressAsync(Guid id, HistoricalProgressRequest request, CancellationToken ct)
    {
        var job = await db.OneCHistoricalImports.FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw new OneCImportException("Историческая загрузка не найдена.");
        var now = clock.GetUtcNow();
        if (job.Status is HistoricalStatuses.Pending)
        {
            job.Status = HistoricalStatuses.Running;
            job.StartedAt ??= now;
        }

        if (request.ReferencesDone)
        {
            job.ReferencesDone = true;
        }

        if (request.Position is { } p)
        {
            job.PositionPeriod = p.Period;
            job.PositionOrganization = p.OrganizationIndex;
            job.PositionStream = p.Stream;
            job.PositionCursor = p.Cursor;
        }

        if (request.Check is { } check)
        {
            var checks = JsonSerializer.Deserialize<List<PeriodCheck>>(job.ChecksJson, OneCFormat.Json) ?? [];
            checks.RemoveAll(x => x.Period == check.Period && x.OrganizationId == check.OrganizationId && x.Stream == check.Stream);
            checks.Add(check);
            job.ChecksJson = JsonSerializer.Serialize(checks.OrderBy(x => x.Period).ThenBy(x => x.OrganizationId).ThenBy(x => x.Stream), OneCFormat.Json);
            job.PeriodsDone = checks.Select(x => x.Period).Distinct().Count(period =>
                checks.Count(x => x.Period == period && x.Stream == OneCStreams.HistoryBalances)
                    >= Math.Max(1, checks.Where(x => x.Period == period && x.OrganizationId != "").Select(x => x.OrganizationId).Distinct().Count()));
        }

        if (request.Status is { } status && job.Status is HistoricalStatuses.Pending or HistoricalStatuses.Running)
        {
            job.Status = status is HistoricalStatuses.Succeeded or HistoricalStatuses.Failed or HistoricalStatuses.Cancelled ? status : HistoricalStatuses.Failed;
            job.FinishedAt = now;
            job.Message = request.Message is null ? null : OneCFormat.Short(request.Message, 2000);
        }
        else if (request.Message is not null)
        {
            job.Message = OneCFormat.Short(request.Message, 2000);
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Очередь неуспешных операций: одна и та же нерешённая ошибка — одна строка (отпечаток), повтор увеличивает число попыток.</summary>
public static class OneCFailures
{
    public static string Fingerprint(FailureReport r) =>
        OneCFormat.Sha256($"{r.Kind}|{r.Stream}|{r.OrganizationId}|{r.Period:yyyy-MM-dd}|{r.Cursor}|{r.EntityType}|{r.SourceId}");

    public static async Task<Guid> ReportAsync(IAppDbContext db, TimeProvider clock, FailureReport r, CancellationToken ct)
    {
        if (r.Kind is not (FailureKinds.Fetch or FailureKinds.Batch or FailureKinds.Record or FailureKinds.Validation))
        {
            throw new OneCImportException($"Неизвестный вид ошибки: {r.Kind}.");
        }

        var now = clock.GetUtcNow();
        var fingerprint = Fingerprint(r);
        var row = db.OneCSyncFailures.Local.FirstOrDefault(f => f.Fingerprint == fingerprint && f.Status is "Open" or "Retrying")
            ?? await db.OneCSyncFailures.FirstOrDefaultAsync(f => f.Fingerprint == fingerprint && (f.Status == "Open" || f.Status == "Retrying"), ct);
        if (row is not null)
        {
            row.Attempts++;
            row.LastAt = now;
            row.Status = "Open";
            row.Error = OneCFormat.Short(r.Error, 2000);
            row.RunId = r.RunId;
            row.Payload = r.Payload is { } p ? p.GetRawText() : row.Payload;
            return row.Id;
        }

        row = new OneCSyncFailure
        {
            RunId = r.RunId, Kind = r.Kind, Stream = OneCFormat.Short(r.Stream, 64), OrganizationId = r.OrganizationId, Period = r.Period,
            Cursor = r.Cursor is null ? null : OneCFormat.Short(r.Cursor, 2000), EntityType = r.EntityType is null ? null : OneCFormat.Short(r.EntityType, 48),
            SourceEntityId = r.SourceId is null ? null : OneCFormat.Short(r.SourceId, 100), Error = OneCFormat.Short(r.Error, 2000),
            Payload = r.Payload is { } payload ? payload.GetRawText() : null, Fingerprint = fingerprint, FirstAt = now, LastAt = now,
        };
        db.OneCSyncFailures.Add(row);
        return row.Id;
    }
}

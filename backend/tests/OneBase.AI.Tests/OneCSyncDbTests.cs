using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Finance;
using OneBase.Application.Security;
using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker;
using OneBase.Finance.Integration.Worker.Api;
using OneBase.Finance.Integration.Worker.Mock;
using OneBase.Finance.Integration.Worker.Normalization;
using OneBase.Finance.Integration.Worker.OneC;
using OneBase.Finance.Integration.Worker.Sync;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Внутренний API OneBase в том же процессе: настоящие OneCSyncService и OneCImportService на тестовой PostgreSQL, ошибки — как у HTTP-клиента.
/// Можно уронить вызов (Fault) или «потерять» ответ после записи пакета (LoseNextImportResponse).
/// </summary>
public sealed class InProcessOneBaseApi(IServiceProvider services) : IOneBaseIntegrationApi
{
    public Func<string, Exception?>? Fault { get; set; }
    public Action<string>? OnCall { get; set; }
    public bool LoseNextImportResponse { get; set; }
    public int Replays { get; private set; }

    private async Task<T> Call<T>(string name, Func<IServiceProvider, Task<T>> body)
    {
        OnCall?.Invoke(name);
        if (Fault?.Invoke(name) is { } fault)
        {
            throw fault;
        }

        using var scope = services.CreateScope();
        try
        {
            return await body(scope.ServiceProvider);
        }
        catch (OneCConflictException e)
        {
            throw new OneBaseApiException(HttpStatusCode.Conflict, e.Message, false);
        }
        catch (OneCImportException e)
        {
            throw new OneBaseApiException(HttpStatusCode.BadRequest, e.Message, false);
        }
    }

    private static OneCSyncService Sync(IServiceProvider sp) => sp.GetRequiredService<OneCSyncService>();

    public Task<HeartbeatResponse> HeartbeatAsync(WorkerHeartbeat beat, CancellationToken ct) => Call("heartbeat", sp => Sync(sp).HeartbeatAsync(beat, ct));

    public Task CompleteCommandAsync(Guid id, CommandResult result, CancellationToken ct) => Call("complete", async sp => { await Sync(sp).CompleteCommandAsync(id, result, ct); return 0; });

    public Task<StartRunResponse> StartRunAsync(StartRunRequest request, CancellationToken ct) => Call("start", sp => Sync(sp).StartRunAsync(request, ct));

    public Task<RunProgressResponse> ProgressAsync(Guid runId, RunProgress progress, CancellationToken ct) => Call("progress", sp => Sync(sp).ProgressAsync(runId, progress, ct));

    public Task FinishRunAsync(Guid runId, FinishRunRequest request, CancellationToken ct) => Call("finish", async sp => { await Sync(sp).FinishRunAsync(runId, request, ct); return 0; });

    public async Task<ImportBatchResult> ImportAsync(ImportBatchRequest request, bool dryRun, CancellationToken ct)
    {
        var result = await Call("import", sp => sp.GetRequiredService<OneCImportService>().ImportAsync(request, dryRun, ct));
        if (result.Replayed)
        {
            Replays++;
        }

        if (LoseNextImportResponse)
        {
            LoseNextImportResponse = false;
            throw new OneBaseApiException(null, "OneBase /batches: обрыв связи после записи", true);
        }

        return result;
    }

    public Task<ImportedTotals> TotalsAsync(string stream, string organizationId, DateOnly from, DateOnly to, CancellationToken ct) =>
        Call("totals", sp => sp.GetRequiredService<OneCImportService>().TotalsAsync(stream, organizationId, from, to, ct));

    public Task<Guid> ReportFailureAsync(FailureReport report, CancellationToken ct) => Call("failure", sp => Sync(sp).ReportFailureAsync(report, ct));

    public Task<List<FailureItem>> DueFailuresAsync(int limit, CancellationToken ct) => Call("due", sp => Sync(sp).DueFailuresAsync(limit, ct));

    public Task FailureOutcomeAsync(Guid id, FailureOutcome outcome, CancellationToken ct) => Call("outcome", async sp => { await Sync(sp).FailureOutcomeAsync(id, outcome, ct); return 0; });

    public Task<HistoricalImportState> HistoricalAsync(Guid id, CancellationToken ct) => Call("historical", sp => Sync(sp).HistoricalAsync(id, ct));

    public Task HistoricalProgressAsync(Guid id, HistoricalProgressRequest request, CancellationToken ct) =>
        Call("historical-progress", async sp => { await Sync(sp).HistoricalProgressAsync(id, request, ct); return 0; });
}

/// <summary>
/// Интеграция 1С целиком: служба (SyncEngine) с тестовым источником → внутренний API → PostgreSQL. Повторный импорт без дублей, история с
/// продолжением и сверкой, исправления и отмены в 1С, обрыв связи и недоступность 1С и OneBase без потери данных, один запуск за раз, пауза,
/// изоляция организаций, валюта без пересчёта, защита от смешивания баз, тестовая синхронизация без записи, отчёты = проводки 1С.
/// </summary>
public sealed class OneCSyncDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateTimeOffset Now = new(2026, 3, 20, 15, 0, 0, TimeSpan.FromHours(5));
    private static readonly DateOnly Start = new(2026, 1, 1);

    private sealed record Stand(SyncEngine Engine, OneCMockDataProvider Mock, InProcessOneBaseApi Api, TestClock Clock);

    private Stand Create(double volume = 1, int maxAttempts = 3)
    {
        var clock = new TestClock { Now = Now };
        var mock = new OneCMockDataProvider(new MockOptions { StartDate = Start, Volume = volume }, clock);
        var api = new InProcessOneBaseApi(fixture.Services);
        var options = new IntegrationOptions { MaxAttempts = maxAttempts, WorkerId = "test-worker", BatchSize = 300, BalancesMonthsBack = 6 };
        var retry = new RetryPolicy(options, NullLogger<RetryPolicy>.Instance) { Delay = (_, _) => Task.CompletedTask };
        return new Stand(new SyncEngine(mock, api, new OneCNormalizer(), retry, options, clock, NullLogger<SyncEngine>.Instance), mock, api, clock);
    }

    private async Task ResetAsync()
    {
        fixture.Clock.Now = null;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.OneCDocumentLines.ExecuteDeleteAsync();
        await db.OneCDocuments.ExecuteDeleteAsync();
        await db.OneCEntries.ExecuteDeleteAsync();
        await db.OneCBalances.ExecuteDeleteAsync();
        await db.OneCExchangeRates.ExecuteDeleteAsync();
        await db.OneCContracts.ExecuteDeleteAsync();
        await db.OneCCounterparties.ExecuteDeleteAsync();
        await db.OneCItems.ExecuteDeleteAsync();
        await db.OneCReferences.ExecuteDeleteAsync();
        await db.OneCAccounts.ExecuteDeleteAsync();
        await db.OneCCurrencies.ExecuteDeleteAsync();
        await db.OneCOrganizations.ExecuteDeleteAsync();
        await db.OneCChanges.ExecuteDeleteAsync();
        await db.OneCSyncCheckpoints.ExecuteDeleteAsync();
        await db.OneCSyncFailures.ExecuteDeleteAsync();
        await db.OneCImportBatches.ExecuteDeleteAsync();
        await db.OneCSyncRuns.ExecuteDeleteAsync();
        await db.OneCHistoricalImports.ExecuteDeleteAsync();
        await db.OneCWorkerCommands.ExecuteDeleteAsync();
        await db.OneCWorkerStates.ExecuteDeleteAsync();
        var settings = scope.ServiceProvider.GetRequiredService<IOneCSettingsStore>();
        await settings.AcceptInfobaseAsync(null, null, false);
        await settings.SaveAsync(new OneCSettingsChange(Paused: false, BatchSize: 300), fixture.AdminId);
    }

    private T Get<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Первая синхронизация + история с 1 января по 20 марта.</summary>
    private async Task<(Stand Stand, SyncOutcome History)> LoadHistoryAsync(double volume = 1)
    {
        await ResetAsync();
        var stand = Create(volume);
        Assert.Equal(RunStatuses.Succeeded, (await stand.Engine.IncrementalAsync("auto", null, default)).Status);
        using var scope = fixture.Services.CreateScope();
        var job = await Get<OneCAdminService>(scope).StartHistoricalAsync(Start, new DateOnly(2026, 3, 20), 300, fixture.AdminId, default);
        var outcome = await stand.Engine.HistoricalAsync(job.Id, null, default);
        return (stand, outcome);
    }

    /// <summary>Успех запуска — с ошибками шагов в тексте, если нет.</summary>
    private static void Ok(SyncOutcome outcome) =>
        Assert.True(outcome.Status == RunStatuses.Succeeded,
            $"{outcome.Status}: {outcome.Message} | {string.Join("; ", (outcome.Steps ?? []).Where(x => x.Error is not null).Select(x => $"{x.Stream}: {x.Error}"))}");

    private static List<OneCDocumentDto> Visible(Stand s) =>
        MockCatalog.Organizations.SelectMany(o => s.Mock.DocumentsOf(o.Id, Start, new DateOnly(2026, 3, 20))).ToList();

    [PostgresFact]
    public async Task First_sync_loads_references_and_current_balances_then_follows_changes()
    {
        await ResetAsync();
        var s = Create();
        var first = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal(RunStatuses.Succeeded, first.Status);
        Assert.Contains("Первое подключение", first.Message);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = Get<OneBaseDbContext>(scope);
            Assert.Equal(2, await db.OneCOrganizations.CountAsync());
            Assert.Equal(MockCatalog.Accounts.Length, await db.OneCAccounts.CountAsync());
            Assert.True(await db.OneCBalances.CountAsync(b => b.Period == new DateOnly(2026, 3, 1)) > 50);
            Assert.Equal(0, await db.OneCDocuments.CountAsync());
            Assert.NotNull(await db.OneCSyncCheckpoints.SingleAsync(c => c.Stream == OneCStreams.Changes));
            Assert.Equal(OneCStreams.Done, (await db.OneCSyncCheckpoints.SingleAsync(c => c.Stream == "ref:account")).Cursor);
            var settings = await Get<IOneCSettingsStore>(scope).GetAsync();
            Assert.Equal((MockCatalog.InfobaseId, true), (settings.AcceptedInfobaseId, settings.DemoData));
        }

        // Повтор — справочники не перечитываются, ничего нового.
        var second = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal(RunStatuses.Succeeded, second.Status);
        Assert.DoesNotContain(second.Steps!, x => x.Stream == "ref:account" && x.Rows > 0);

        // Прошли сутки: в 1С появились документы — они приходят изменениями.
        s.Clock.Now = Now.AddDays(1);
        var third = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal(RunStatuses.Succeeded, third.Status);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Get<OneBaseDbContext>(scope);
            Assert.True(await db.OneCDocuments.CountAsync() > 0);
            Assert.True(await db.OneCEntries.CountAsync() > 0);
        }
    }

    [PostgresFact]
    public async Task Historical_import_is_reconciled_and_repeating_it_creates_no_duplicates()
    {
        var (s, history) = await LoadHistoryAsync();
        Assert.Equal(RunStatuses.Succeeded, history.Status);
        var docs = Visible(s);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = Get<OneBaseDbContext>(scope);
            Assert.Equal(docs.Count, await db.OneCDocuments.CountAsync(d => d.DocumentDate >= Start));
            var job = await db.OneCHistoricalImports.SingleAsync();
            Assert.Equal((HistoricalStatuses.Succeeded, 3, 3), (job.Status, job.PeriodsDone, job.PeriodsTotal));
            var checks = System.Text.Json.JsonSerializer.Deserialize<List<PeriodCheck>>(job.ChecksJson, OneCFormat.Json)!;
            Assert.NotEmpty(checks);
            Assert.All(checks, c => Assert.True(c.Ok, c.Note));
            Assert.Contains(checks, c => c.Stream == OneCStreams.HistoryEntries && c.ExpectedCount > 100);
        }

        // Повторная загрузка того же периода: ничего нового, ни одного дубликата.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Get<OneBaseDbContext>(scope);
            var before = (await db.OneCDocuments.CountAsync(), await db.OneCEntries.CountAsync(), await db.OneCBalances.CountAsync());
            var job = await Get<OneCAdminService>(scope).StartHistoricalAsync(Start, new DateOnly(2026, 3, 20), 300, fixture.AdminId, default);
            var again = await s.Engine.HistoricalAsync(job.Id, null, default);
            Assert.Equal(RunStatuses.Succeeded, again.Status);
            Assert.Contains("новых 0", again.Message);
            Assert.Equal(before, (await db.OneCDocuments.CountAsync(), await db.OneCEntries.CountAsync(), await db.OneCBalances.CountAsync()));
        }
    }

    [PostgresFact]
    public async Task Interrupted_history_resumes_from_the_saved_position()
    {
        await ResetAsync();
        var s = Create();
        await s.Engine.IncrementalAsync("auto", null, default);
        Guid jobId;
        using (var scope = fixture.Services.CreateScope())
        {
            jobId = (await Get<OneCAdminService>(scope).StartHistoricalAsync(Start, new DateOnly(2026, 3, 20), 300, fixture.AdminId, default)).Id;
        }

        // OneBase пропадает на 6-м пакете истории: порция завершается, положение остаётся на последнем записанном пакете.
        var imports = 0;
        s.Api.Fault = name => name == "import" && ++imports >= 6 ? new OneBaseApiException(HttpStatusCode.ServiceUnavailable, "OneBase недоступен", true) : null;
        var broken = await s.Engine.HistoricalAsync(jobId, null, default);
        Assert.Equal(RunStatuses.Failed, broken.Status);
        using (var scope = fixture.Services.CreateScope())
        {
            var job = await Get<OneBaseDbContext>(scope).OneCHistoricalImports.SingleAsync();
            Assert.Equal(HistoricalStatuses.Running, job.Status);
            Assert.NotNull(job.PositionStream);
            Assert.True(job.Records > 0);
        }

        s.Api.Fault = null;
        var resumed = await s.Engine.HistoricalAsync(jobId, null, default);
        Assert.Equal(RunStatuses.Succeeded, resumed.Status);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Get<OneBaseDbContext>(scope);
            Assert.Equal(Visible(s).Count, await db.OneCDocuments.CountAsync(d => d.DocumentDate >= Start));
            var job = await db.OneCHistoricalImports.SingleAsync();
            Assert.Equal(HistoricalStatuses.Succeeded, job.Status);
            Assert.All(System.Text.Json.JsonSerializer.Deserialize<List<PeriodCheck>>(job.ChecksJson, OneCFormat.Json)!, c => Assert.True(c.Ok, c.Note));
            // Неуспешный пакет сохранён целиком в очереди ошибок.
            Assert.Contains(await db.OneCSyncFailures.ToListAsync(), f => f.Kind == FailureKinds.Batch && f.Payload != null);
        }
    }

    [PostgresFact]
    public async Task Corrections_cancellations_and_deletions_in_1C_reach_OneBase_with_history()
    {
        var (s, _) = await LoadHistoryAsync();
        var org = MockCatalog.Organizations[0];
        var sales = s.Mock.DocumentsOf(org.Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)).Where(d => d.Type == OneCDocumentTypes.Sale).Take(3).ToList();
        var (edited, cancelled, removed) = (sales[0], sales[1], sales[2]);
        var customer = edited.CounterpartyId!;
        s.Clock.Now = Now.AddHours(1);
        s.Mock.EditDocument(edited.Id!, 1.1m);
        s.Mock.UnpostDocument(cancelled.Id!);
        s.Mock.DeleteDocument(removed.Id!);
        s.Mock.RenameCounterparty(customer, "ООО «Переименованный покупатель»");

        Ok(await s.Engine.IncrementalAsync("auto", null, default));

        using var scope = fixture.Services.CreateScope();
        var db = Get<OneBaseDbContext>(scope);
        var doc = await db.OneCDocuments.SingleAsync(d => d.SourceEntityId == edited.Id);
        Assert.Equal(Math.Round(edited.AmountAccounting!.Value * 1.1m, 2), doc.AmountAccounting);
        Assert.Equal("v2", doc.SourceVersion);
        var docChange = await db.OneCChanges.SingleAsync(c => c.SourceEntityId == edited.Id && c.EntityType == OneCEntityTypes.Document);
        Assert.Equal("updated", docChange.Action);
        Assert.Contains("AmountAccounting", docChange.Changes);
        var newEntries = await db.OneCEntries.Where(e => e.RegistrarSourceId == edited.Id && !e.IsDeleted).SumAsync(e => e.Amount);
        Assert.Equal(s.Mock.Set.Document(edited.Id!).Entries.Sum(e => e.Amount), newEntries);
        Assert.Equal("reposted", (await db.OneCChanges.SingleAsync(c => c.SourceEntityId == edited.Id && c.EntityType == OneCEntityTypes.Entry)).Action);

        var unposted = await db.OneCDocuments.SingleAsync(d => d.SourceEntityId == cancelled.Id);
        Assert.False(unposted.Posted);
        Assert.True(await db.OneCEntries.Where(e => e.RegistrarSourceId == cancelled.Id).AllAsync(e => e.IsDeleted));
        Assert.Contains(await db.OneCChanges.Where(c => c.SourceEntityId == cancelled.Id).Select(c => c.Action).ToListAsync(), a => a == "cancelled");

        Assert.True((await db.OneCDocuments.SingleAsync(d => d.SourceEntityId == removed.Id)).IsDeleted);
        Assert.True(await db.OneCEntries.Where(e => e.RegistrarSourceId == removed.Id).AllAsync(e => e.IsDeleted));
        Assert.Equal("ООО «Переименованный покупатель»", (await db.OneCCounterparties.SingleAsync(c => c.SourceEntityId == customer)).Name);

        // Остатки февраля пересчитаны после правок: дебиторка = ведомость 1С.
        var mockReceivables = s.Mock.Set.Balances(0, new DateOnly(2026, 2, 1)).Where(b => b.Account == "4010").Sum(b => b.Closing!.Debit - b.Closing.Credit);
        var ours = await db.OneCBalances.Where(b => b.SourceOrganizationId == org.Id && b.Period == new DateOnly(2026, 2, 1) && b.Account == "4010" && !b.IsDeleted)
            .SumAsync(b => b.ClosingDebit - b.ClosingCredit);
        Assert.Equal(mockReceivables, ours);
    }

    [PostgresFact]
    public async Task Lost_connection_and_onebase_outage_lose_nothing()
    {
        var (s, _) = await LoadHistoryAsync();
        var doc = s.Mock.DocumentsOf(MockCatalog.Organizations[0].Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10)).First(d => d.Type == OneCDocumentTypes.Sale);
        string? Cursor()
        {
            using var scope = fixture.Services.CreateScope();
            return Get<OneBaseDbContext>(scope).OneCSyncCheckpoints.AsNoTracking().Single(c => c.Stream == OneCStreams.Changes).Cursor;
        }

        decimal Amount()
        {
            using var scope = fixture.Services.CreateScope();
            return Get<OneBaseDbContext>(scope).OneCDocuments.AsNoTracking().Single(d => d.SourceEntityId == doc.Id).AmountAccounting;
        }

        var cursor = Cursor();
        var original = Amount();
        s.Clock.Now = Now.AddHours(1);
        s.Mock.EditDocument(doc.Id!, 2m);

        // 1С недоступна полностью: запуск не начинается, в журнал — неудача с причиной.
        s.Mock.Faults.Down = true;
        var down = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal(RunStatuses.Failed, down.Status);
        Assert.Contains("1С недоступна", down.Message);
        s.Mock.Faults.Down = false;

        // Связь рвётся на чтении изменений (все 3 попытки): запуск — ошибка, курсор не сдвинут, правка не потеряна.
        s.Mock.Faults.FailNext("changes", OneCErrorKind.Network, times: 3);
        var lost = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal(RunStatuses.Failed, lost.Status);
        Assert.Equal((cursor, original), (Cursor(), Amount()));

        // Кратковременный сбой (503 с Retry-After) — повтор внутри запуска.
        s.Mock.Faults.FailNext("changes", OneCErrorKind.Unavailable, times: 2, TimeSpan.FromSeconds(5));
        Ok(await s.Engine.IncrementalAsync("auto", null, default));
        Assert.Equal(Math.Round(doc.AmountAccounting!.Value * 2, 2), Amount());
        Assert.NotEqual(cursor, Cursor());

        // OneBase не принимает пакеты: пакет — в очередь ошибок целиком, курсор не сдвинут; повтор после восстановления — данные на месте.
        s.Clock.Now = Now.AddHours(2);
        s.Mock.EditDocument(doc.Id!, 0.5m);
        var before = Cursor();
        s.Api.Fault = name => name == "import" ? new OneBaseApiException(HttpStatusCode.BadGateway, "OneBase недоступен", true) : null;
        Assert.Equal(RunStatuses.Failed, (await s.Engine.IncrementalAsync("auto", null, default)).Status);
        Assert.Equal(before, Cursor());
        using (var scope = fixture.Services.CreateScope())
        {
            var failure = await Get<OneBaseDbContext>(scope).OneCSyncFailures.SingleAsync(f => f.Kind == FailureKinds.Batch && f.Status == "Open");
            Assert.Contains(doc.Id!, failure.Payload);
            await Get<OneCAdminService>(scope).RetryFailuresAsync([failure.Id], fixture.AdminId, default);
        }

        s.Api.Fault = null;
        var retried = await s.Engine.RetryFailuresAsync(null, default);
        Assert.Equal(RunStatuses.Succeeded, retried.Status);
        Assert.Equal(Math.Round(Math.Round(doc.AmountAccounting!.Value * 2, 2) * 0.5m, 2), Amount());
        using (var scope = fixture.Services.CreateScope())
        {
            Assert.Equal("Resolved", (await Get<OneBaseDbContext>(scope).OneCSyncFailures.SingleAsync(f => f.Kind == FailureKinds.Batch)).Status);
        }

        Assert.Equal(RunStatuses.Succeeded, (await s.Engine.IncrementalAsync("auto", null, default)).Status);
    }

    [PostgresFact]
    public async Task Lost_response_is_not_applied_twice()
    {
        await ResetAsync();
        var s = Create();
        s.Api.LoseNextImportResponse = true;
        var outcome = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal(RunStatuses.Succeeded, outcome.Status);
        Assert.Equal(1, s.Api.Replays);
        using var scope = fixture.Services.CreateScope();
        var db = Get<OneBaseDbContext>(scope);
        Assert.Equal(2, await db.OneCOrganizations.CountAsync());
        var run = await db.OneCSyncRuns.SingleAsync();
        Assert.Equal(await db.OneCImportBatches.CountAsync(), run.Batches);
    }

    [PostgresFact]
    public async Task Only_one_sync_runs_at_a_time_and_stale_runs_are_closed()
    {
        await ResetAsync();
        var s = Create();
        await s.Engine.IncrementalAsync("auto", null, default);
        using (var scope = fixture.Services.CreateScope())
        {
            var other = await Get<OneCSyncService>(scope).StartRunAsync(new StartRunRequest { Kind = RunKinds.Incremental, Provider = "mock", InfobaseId = MockCatalog.InfobaseId }, default);
            Assert.True(other.Started);
        }

        var skipped = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal("Skipped", skipped.Status);
        Assert.Contains("Уже идёт", skipped.Message);

        // Запуск без пульса 15 минут — служба остановилась: OneBase закрывает его и даёт начать новый.
        fixture.Clock.Now = DateTimeOffset.UtcNow.AddMinutes(16);
        try
        {
            Assert.Equal(RunStatuses.Succeeded, (await s.Engine.IncrementalAsync("auto", null, default)).Status);
            using var scope = fixture.Services.CreateScope();
            Assert.Contains(await Get<OneBaseDbContext>(scope).OneCSyncRuns.ToListAsync(), r => r.Status == RunStatuses.Failed && r.Message!.Contains("перестала отвечать"));
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Pause_stops_automatic_sync_but_the_administrator_can_run_it()
    {
        await ResetAsync();
        var s = Create();
        using var scope = fixture.Services.CreateScope();
        var settings = Get<IOneCSettingsStore>(scope);
        await settings.SaveAsync(new OneCSettingsChange(Paused: true), fixture.AdminId);
        var paused = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal("Skipped", paused.Status);
        Assert.Contains("приостановлена", paused.Message);
        Assert.Equal(RunStatuses.Succeeded, (await s.Engine.IncrementalAsync("manual", null, default)).Status);

        // Пауза во время запуска: запуск останавливается на границе пакета.
        await settings.SaveAsync(new OneCSettingsChange(Paused: false), fixture.AdminId);
        s.Clock.Now = Now.AddDays(1);
        var progress = 0;
        s.Api.OnCall = name =>
        {
            if (name == "progress" && ++progress == 1)
            {
                settings.SaveAsync(new OneCSettingsChange(Paused: true), fixture.AdminId).GetAwaiter().GetResult();
            }
        };
        var stopped = await s.Engine.IncrementalAsync("auto", null, default);
        Assert.Equal(RunStatuses.Cancelled, stopped.Status);
        Assert.Contains("приостановлена", stopped.Message);
    }

    [PostgresFact]
    public async Task Organizations_are_isolated()
    {
        await ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var run = await Get<OneCSyncService>(scope).StartRunAsync(new StartRunRequest { Kind = RunKinds.Incremental, Provider = "mock", InfobaseId = "ib", Trigger = "manual" }, default);
        var import = Get<OneCImportService>(scope);
        ImportDocument Doc(string org, decimal amount) => new()
        {
            SourceId = "same-id", OrganizationId = org, Type = OneCDocumentTypes.Sale, SourceType = "Реализация", Number = "1", Date = Now, Posted = true,
            Amount = amount, AmountAccounting = amount,
        };

        await import.ImportAsync(new ImportBatchRequest { RunId = run.RunId!.Value, BatchKey = "a", Stream = "t", OrganizationId = "A", Documents = [Doc("A", 100)] }, false, default);
        await import.ImportAsync(new ImportBatchRequest { RunId = run.RunId.Value, BatchKey = "b", Stream = "t", OrganizationId = "B", Documents = [Doc("B", 200)] }, false, default);
        var foreign = await import.ImportAsync(new ImportBatchRequest { RunId = run.RunId.Value, BatchKey = "c", Stream = "t", OrganizationId = "A", Documents = [Doc("B", 999)] }, false, default);

        Assert.Equal(1, foreign.Rejected);
        Assert.Contains("другой организации", foreign.Rejections[0].Reason);
        var db = Get<OneBaseDbContext>(scope);
        Assert.Equal([("A", 100m), ("B", 200m)], (await db.OneCDocuments.OrderBy(d => d.SourceOrganizationId).Select(d => new { d.SourceOrganizationId, d.AmountAccounting }).ToListAsync())
            .Select(d => (d.SourceOrganizationId, d.AmountAccounting)));
        var failure = await db.OneCSyncFailures.SingleAsync();
        Assert.Equal(FailureKinds.Record, failure.Kind);
        Assert.Contains("999", failure.Payload);
    }

    [PostgresFact]
    public async Task Currency_amounts_and_rates_stay_exactly_as_in_1C()
    {
        var (s, _) = await LoadHistoryAsync();
        var usd = Visible(s).First(d => d.Currency == "USD" && d.Type == OneCDocumentTypes.Sale);
        using var scope = fixture.Services.CreateScope();
        var db = Get<OneBaseDbContext>(scope);
        var doc = await db.OneCDocuments.SingleAsync(d => d.SourceEntityId == usd.Id);
        Assert.Equal((usd.Amount, usd.AmountAccounting!.Value, usd.Rate!.Value, usd.Rate.Date, usd.Rate.Source), (doc.Amount, doc.AmountAccounting, doc.RateValue!.Value, doc.RateDate, doc.RateSource));
        var entry = s.Mock.Set.Document(usd.Id!).Entries.First(e => e.Debit!.Currency == "USD");
        var stored = await db.OneCEntries.SingleAsync(e => e.RegistrarSourceId == usd.Id && e.LineNumber == entry.LineNumber);
        Assert.Equal(("USD", entry.Debit!.CurrencyAmount, entry.Amount), (stored.DebitCurrency, stored.DebitCurrencyAmount, stored.Amount));
        var rate = s.Mock.Set.Rates.First(r => r.Currency == "EUR" && r.Date == new DateOnly(2026, 2, 10));
        Assert.Equal(rate.Rate, (await db.OneCExchangeRates.SingleAsync(r => r.Currency == "EUR" && r.Date == rate.Date)).Rate);

        // Курс изменился позже — исторический документ не пересчитывается.
        var updatedAt = doc.UpdatedAt;
        s.Clock.Now = Now.AddDays(2);
        Assert.Equal(RunStatuses.Succeeded, (await s.Engine.IncrementalAsync("auto", null, default)).Status);
        var again = await db.OneCDocuments.AsNoTracking().SingleAsync(d => d.SourceEntityId == usd.Id);
        Assert.Equal((doc.AmountAccounting, updatedAt), (again.AmountAccounting, again.UpdatedAt));
    }

    [PostgresFact]
    public async Task Another_infobase_is_refused_until_the_administrator_clears_data()
    {
        await ResetAsync();
        var s = Create();
        await s.Engine.IncrementalAsync("auto", null, default);
        using var scope = fixture.Services.CreateScope();
        var sync = Get<OneCSyncService>(scope);
        var refused = await sync.StartRunAsync(new StartRunRequest { Kind = RunKinds.Incremental, Provider = "http", InfobaseId = "prod-base", InfobaseName = "Рабочая" }, default);
        Assert.Equal((false, "infobase_mismatch"), (refused.Started, refused.Reason));
        var status = await Get<OneCAdminService>(scope).StatusAsync(default);
        Assert.Equal("mismatch", status.State);
        Assert.Contains("Рабочая", status.StateText);

        Assert.True(await Get<OneCAdminService>(scope).ClearDataAsync(default) > 0);
        Assert.Equal(0, await Get<OneBaseDbContext>(scope).OneCAccounts.CountAsync());
        var accepted = await sync.StartRunAsync(new StartRunRequest { Kind = RunKinds.Incremental, Provider = "http", InfobaseId = "prod-base", InfobaseName = "Рабочая" }, default);
        Assert.True(accepted.Started);
        var settings = await Get<IOneCSettingsStore>(scope).GetAsync();
        Assert.Equal(("prod-base", false, null), (settings.AcceptedInfobaseId, settings.DemoData, settings.PendingInfobaseId));
    }

    [PostgresFact]
    public async Task Test_sync_checks_everything_and_writes_nothing()
    {
        await ResetAsync();
        var s = Create();
        var outcome = await s.Engine.TestAsync(null, default);
        Assert.Equal(RunStatuses.Succeeded, outcome.Status);
        Assert.Contains("без записи", outcome.Message);
        using var scope = fixture.Services.CreateScope();
        var counts = await Get<OneCAdminService>(scope).CountsAsync(default);
        Assert.Equal(0, counts.Organizations + counts.Accounts + counts.Documents + counts.Entries + counts.Balances);
        Assert.Equal(0, await Get<OneBaseDbContext>(scope).OneCSyncCheckpoints.CountAsync());
        Assert.Equal(RunKinds.Test, (await Get<OneBaseDbContext>(scope).OneCSyncRuns.SingleAsync()).Kind);
    }

    [PostgresFact]
    public async Task Finance_reports_agree_with_1C_entries_and_balances()
    {
        var (s, _) = await LoadHistoryAsync();
        var org = MockCatalog.Organizations[0];
        var february = new FinanceFilter(org.Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        var entries = s.Mock.Set.Documents.Where(d => d.Org == 0 && s.Mock.Set.Visible(d) && d.Doc.Posted).SelectMany(d => d.Entries)
            .Where(e => MockDataSet.DayOf(e.Period!.Value) is var day && day >= february.From && day <= february.To).ToList();
        var expected = FinanceChart.Pnl(entries.Select(e => new FinanceChart.Turnover(e.Debit!.Account!, e.Credit!.Account!, february.From, e.Amount)));
        using var scope = fixture.Services.CreateScope();
        var reports = Get<FinanceReportService>(scope);

        var pnl = await reports.PnlAsync(february, default);
        Assert.Equal(expected["net_revenue"], pnl.Lines.Single(l => l.Code == "net_revenue").Total);
        Assert.Equal(expected["net_profit"], pnl.Lines.Single(l => l.Code == "net_profit").Total);
        Assert.True(expected["net_revenue"] > 1_000_000_000m);
        Assert.Equal("1С:Бухгалтерия — тестовый источник (демо-данные)", pnl.Meta.Source);
        Assert.True(pnl.Meta.Demo);

        var balances = s.Mock.Set.Balances(0, february.From);
        var trial = await reports.TrialBalanceAsync(february, default);
        Assert.Equal(balances.Where(b => b.Account == "4010").Sum(b => b.Closing!.Debit), trial.Rows.Single(r => r.Account == "4010").ClosingDebit);
        Assert.Equal(trial.Total.TurnoverDebit, trial.Total.TurnoverCredit);

        var receivables = await reports.SettlementsAsync(february, "receivables", 500, default);
        var byCustomer = balances.Where(b => b.Account == "4010").GroupBy(b => b.Subconto[0].Id).Select(g => g.Sum(b => b.Closing!.Debit - b.Closing.Credit)).Where(x => x > 0).Sum();
        Assert.Equal(byCustomer, receivables.Total);
        Assert.Equal(receivables.Total, receivables.Buckets.Values.Sum());

        var cash = await reports.CashAsync(february, default);
        static bool Cash(string a) => FinanceChart.Is(a, FinanceChart.Cash);
        var inflow = entries.Where(e => Cash(e.Debit!.Account!) && !Cash(e.Credit!.Account!) && !FinanceChart.Is(e.Credit.Account!, FinanceChart.ExchangeDifferences)).Sum(e => e.Amount);
        Assert.Equal(inflow, cash.Inflow);

        var overview = await reports.OverviewAsync(february, default);
        Assert.Equal(expected["net_profit"], overview.Pnl.Single(p => p.Code == "net_profit").Value);
        var page = await reports.EntriesAsync(february, "4010", null, null, 1, 20, default);
        Assert.Equal(entries.Count(e => e.Debit!.Account!.StartsWith("4010") || e.Credit!.Account!.StartsWith("4010")), page.Total);
        var doc = await reports.DocumentAsync(page.Items.First(i => i.DocumentId is not null).DocumentId!.Value, default);
        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Entries);
    }

    [PostgresFact]
    public async Task Ai_tools_answer_from_1C_only_when_the_administrator_allows_it()
    {
        var (_, _) = await LoadHistoryAsync();
        using var scope = fixture.Services.CreateScope();
        var tools = scope.ServiceProvider.GetServices<OneBase.AI.Tools.ITool>().Where(t => t.Name.StartsWith("finance_", StringComparison.Ordinal)).ToDictionary(t => t.Name);
        Assert.Equal(9, tools.Count);
        Assert.All(tools.Values, t => Assert.Equal((Permissions.FinanceRead, "finance.accounting", false), (t.RequiredPermission, t.Source, t.IsCritical)));
        var context = new OneBase.AI.Tools.ToolContext("finance", fixture.AdminId, 0);
        var args = System.Text.Json.JsonSerializer.SerializeToElement(new { from = "2026-02-01", to = "2026-02-28", organization = "Кондитер" });

        // По умолчанию доступ AI выключен: ни одной цифры 1С.
        var denied = await tools["finance_overview"].ExecuteAsync(context, args);
        Assert.False(denied.Success);
        Assert.Contains("выключен администратором", denied.Content);

        await Get<IOneCSettingsStore>(scope).SaveAsync(new OneCSettingsChange(AiEnabled: true), fixture.AdminId);
        var overview = await tools["finance_overview"].ExecuteAsync(context, args);
        Assert.True(overview.Success, overview.Content);
        using var json = System.Text.Json.JsonDocument.Parse(overview.Content);
        var root = json.RootElement;
        var expected = await Get<FinanceReportService>(scope).PnlAsync(new FinanceFilter(MockCatalog.Organizations[0].Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), default);
        var netProfit = root.GetProperty("pnl").EnumerateArray().First(p => p.GetProperty("code").GetString() == "net_profit").GetProperty("sum").GetDecimal();
        Assert.Equal(Math.Round(expected.Lines.Single(l => l.Code == "net_profit").Total), netProfit);
        var meta = root.GetProperty("meta");
        Assert.Contains("демо", meta.GetProperty("source").GetString());
        Assert.Equal("ООО «Демо Кондитер»", meta.GetProperty("organization").GetString());
        Assert.StartsWith("/finance?from=2026-02-01&to=2026-02-28&organization=", meta.GetProperty("link").GetString());
        Assert.Equal("2026-02-01", meta.GetProperty("period").GetProperty("from").GetString());
        Assert.Contains("1С:Бухгалтерия → Финансы", overview.Sources![0].Title);

        foreach (var name in tools.Keys)
        {
            var result = await tools[name].ExecuteAsync(context, args);
            Assert.True(result.Success, $"{name}: {result.Content}");
        }

        var unknown = await tools["finance_pnl"].ExecuteAsync(context, System.Text.Json.JsonSerializer.SerializeToElement(new { organization = "Нет такой" }));
        Assert.False(unknown.Success);
        Assert.Contains("Демо Кондитер", unknown.Content);
        await Get<IOneCSettingsStore>(scope).SaveAsync(new OneCSettingsChange(AiEnabled: false), fixture.AdminId);
    }

    [PostgresFact]
    public async Task Large_volume_is_imported_in_batches_and_reconciled()
    {
        var (_, history) = await LoadHistoryAsync(volume: 4);
        Assert.Equal(RunStatuses.Succeeded, history.Status);
        using var scope = fixture.Services.CreateScope();
        var db = Get<OneBaseDbContext>(scope);
        Assert.True(await db.OneCEntries.CountAsync() > 15_000);
        Assert.True(await db.OneCImportBatches.CountAsync() > 50);
        var job = await db.OneCHistoricalImports.SingleAsync();
        Assert.All(System.Text.Json.JsonSerializer.Deserialize<List<PeriodCheck>>(job.ChecksJson, OneCFormat.Json)!, c => Assert.True(c.Ok, c.Note));
    }

    [PostgresFact]
    public async Task Service_token_is_stored_as_a_hash_only()
    {
        await ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var settings = Get<IOneCSettingsStore>(scope);
        var token = await settings.CreateTokenAsync(fixture.AdminId);
        Assert.True(await settings.VerifyTokenAsync(token));
        Assert.False(await settings.VerifyTokenAsync(token + "x"));
        Assert.False(await settings.VerifyTokenAsync(null));
        var row = await Get<OneBaseDbContext>(scope).Integrations.AsNoTracking().SingleAsync(i => i.Code == "onec");
        Assert.DoesNotContain(token, row.SettingsJson);
        Assert.Equal("••••" + token[^4..], row.SecretHint);
        var next = await settings.CreateTokenAsync(fixture.AdminId);
        Assert.False(await settings.VerifyTokenAsync(token));
        Assert.True(await settings.VerifyTokenAsync(next));
    }
}

/// <summary>Кто что может: финансы — finance.read, настройки 1С — integrations.manage, служба — только своим ключом.</summary>
public class OneCAccessTests
{
    private static string? Permission(Type type) => type.GetCustomAttribute<HasPermissionAttribute>()?.Policy?.Replace(PermissionPolicyProvider.Prefix, "");

    [Fact]
    public void Endpoints_require_the_right_permissions()
    {
        Assert.Equal(Permissions.FinanceRead, Permission(typeof(FinanceController)));
        Assert.Equal(Permissions.IntegrationsManage, Permission(typeof(OneCController)));
        Assert.Null(Permission(typeof(OneCInternalController)));
        Assert.NotNull(typeof(OneCInternalController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(typeof(OneCTokenFilter), typeof(OneCInternalController).GetCustomAttribute<ServiceFilterAttribute>()!.ServiceType);
        Assert.Equal("api/internal/onec", typeof(OneCInternalController).GetCustomAttribute<RouteAttribute>()!.Template);
        // Ни один метод финансов не меняет данные: только GET.
        Assert.All(typeof(FinanceController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            m => Assert.NotNull(m.GetCustomAttribute<HttpGetAttribute>()));
    }
}

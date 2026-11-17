using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker.Api;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Integration.Worker.Sync;

/// <summary>Состояние службы — для пульса в OneBase и /health контейнера. Без секретов.</summary>
public sealed class WorkerStatus
{
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public volatile bool Busy;
    public DateTimeOffset? NextSyncAt { get; set; }
    public ConnectionReport? Connection { get; set; }
    public bool ConnectionSent { get; set; }
    public DateTimeOffset? LastHeartbeatOk { get; set; }
    public string? LastHeartbeatError { get; set; }
    public WorkerSettings Settings { get; set; } = new();
    public SyncOutcome? LastOutcome { get; set; }

    public static string Version { get; } = typeof(WorkerStatus).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1.0";
}

/// <summary>
/// Цикл службы. Пульс в OneBase — отдельной задачей каждые HeartbeatSeconds (даже во время долгой загрузки): состояние, проверка подключения,
/// команды администратора, пауза. Работа — по одной: команды, затем историческая загрузка (порциями), затем синхронизация по расписанию
/// (интервал задаёт администратор, по умолчанию 30 минут). Служба с OneC__Enabled=false на связи и проверяет подключение, но данные не загружает.
/// </summary>
public sealed class SyncWorker(
    SyncEngine engine,
    ConnectionDiagnostics diagnostics,
    IOneBaseIntegrationApi api,
    IOneCDataProvider provider,
    OneCOptions oneC,
    IntegrationOptions options,
    WorkerStatus status,
    TimeProvider clock,
    ILogger<SyncWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Channel<WorkerCommand> _commands = Channel.CreateUnbounded<WorkerCommand>();
    private readonly HashSet<Guid> _seen = [];
    private DateTimeOffset _nextCheck;
    private DateTimeOffset _historyNotBefore = DateTimeOffset.MinValue;
    private int _failuresInRow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Служба интеграции 1С {Version}: источник {Provider} ({Url}), загрузка {Enabled}, OneBase — {Api}",
            WorkerStatus.Version, provider.Name, provider.DisplayUrl ?? "адрес не задан", oneC.Enabled ? "включена" : "выключена (OneC__Enabled=false)", "внутренний API");
        status.NextSyncAt = clock.GetUtcNow().AddMinutes(1);
        await CheckConnectionAsync(stoppingToken);
        var heartbeat = HeartbeatLoopAsync(stoppingToken);
        try
        {
            await WorkLoopAsync(stoppingToken);
        }
        finally
        {
            await heartbeat.ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        var errors = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var connection = status.ConnectionSent ? null : status.Connection;
                var response = await api.HeartbeatAsync(new WorkerHeartbeat
                {
                    WorkerId = options.WorkerId, Version = WorkerStatus.Version, Provider = provider.Name, Enabled = oneC.Enabled, Busy = status.Busy,
                    CurrentRunId = engine.CurrentRunId, NextSyncAt = status.NextSyncAt, StartedAt = status.StartedAt, Connection = connection, BaseUrl = provider.DisplayUrl,
                }, ct);
                if (connection is not null && ReferenceEquals(connection, status.Connection))
                {
                    status.ConnectionSent = true;
                }

                status.Settings = response.Settings;
                status.LastHeartbeatOk = clock.GetUtcNow();
                status.LastHeartbeatError = null;
                foreach (var command in response.Commands.Where(c => _seen.Add(c.Id)))
                {
                    await _commands.Writer.WriteAsync(command, ct);
                }

                errors = 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                status.LastHeartbeatError = e.Message;
                if (errors++ % 20 == 0)
                {
                    logger.LogWarning("1С: нет связи с OneBase — {Error}", e.Message);
                }
            }

            await Delay(TimeSpan.FromSeconds(Math.Clamp(options.HeartbeatSeconds, 5, 300)), ct);
        }
    }

    private async Task WorkLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_commands.Reader.TryRead(out var command))
                {
                    await RunCommandAsync(command, ct);
                    continue;
                }

                var now = clock.GetUtcNow();
                if (now >= _nextCheck)
                {
                    await CheckConnectionAsync(ct);
                }

                var settings = status.Settings;
                if (oneC.Enabled && status.LastHeartbeatOk is not null && !settings.Paused)
                {
                    if (settings.ActiveHistoricalImportId is { } importId && clock.GetUtcNow() >= _historyNotBefore)
                    {
                        // Порции истории идут одна за другой; после сбоя или отказа — пауза 5 минут (1С недоступна, другая база…).
                        var portion = await Busy(() => engine.HistoricalAsync(importId, null, ct));
                        _historyNotBefore = portion.Status is RunStatuses.Succeeded or RunStatuses.Partial ? DateTimeOffset.MinValue : clock.GetUtcNow().AddMinutes(5);
                    }

                    if (clock.GetUtcNow() >= status.NextSyncAt)
                    {
                        var interval = TimeSpan.FromMinutes(Math.Clamp(settings.IntervalMinutes, 5, 1440));
                        status.NextSyncAt = clock.GetUtcNow() + interval;
                        var outcome = await Busy(() => engine.IncrementalAsync("auto", null, ct));
                        if (outcome.Status == RunStatuses.Failed && _failuresInRow++ < 3)
                        {
                            // Сбой — повтор через 5 минут (не больше трёх раз подряд), дальше — по обычному расписанию.
                            var retryAt = clock.GetUtcNow().AddMinutes(5);
                            status.NextSyncAt = retryAt < status.NextSyncAt ? retryAt : status.NextSyncAt;
                        }
                        else if (outcome.Status != RunStatuses.Failed)
                        {
                            _failuresInRow = 0;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogWarning("1С: цикл службы — {Error}", e.Message);
                await Delay(TimeSpan.FromSeconds(30), ct);
            }

            // Ждём команду или секунду.
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wait.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                await _commands.Reader.WaitToReadAsync(wait.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
            }
        }
    }

    private async Task<SyncOutcome> Busy(Func<Task<SyncOutcome>> work)
    {
        status.Busy = true;
        try
        {
            var outcome = await work();
            status.LastOutcome = outcome;
            return outcome;
        }
        finally
        {
            status.Busy = false;
        }
    }

    private async Task CheckConnectionAsync(CancellationToken ct)
    {
        _nextCheck = clock.GetUtcNow().AddMinutes(Math.Clamp(options.ConnectionCheckMinutes, 1, 1440));
        try
        {
            var report = await diagnostics.RunAsync(ct);
            status.Connection = report;
            status.ConnectionSent = false;
            if (!report.Ok)
            {
                logger.LogWarning("1С: проверка подключения — {Message}", report.Message);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning("1С: проверка подключения не выполнена — {Error}", e.Message);
        }
    }

    private async Task RunCommandAsync(WorkerCommand command, CancellationToken ct)
    {
        logger.LogInformation("1С: команда администратора {Kind}", command.Kind);
        CommandResult result;
        try
        {
            result = command.Kind switch
            {
                WorkerCommandKinds.TestConnection => await TestConnectionAsync(ct),
                WorkerCommandKinds.TestSync => Result(await Busy(() => engine.TestAsync(command.Id, ct))),
                _ when !oneC.Enabled => new CommandResult(false, "Загрузка данных выключена у службы (OneC__Enabled=false). Проверка подключения и тестовая синхронизация доступны."),
                WorkerCommandKinds.SyncNow => Result(await Busy(() => engine.IncrementalAsync("manual", command.Id, ct))),
                WorkerCommandKinds.RetryFailures => Result(await Busy(() => engine.RetryFailuresAsync(command.Id, ct))),
                WorkerCommandKinds.Historical when command.Payload is { } p && p.TryGetProperty("importId", out var id) =>
                    Result(await Busy(() => engine.HistoricalAsync(id.GetGuid(), command.Id, ct))),
                _ => new CommandResult(false, $"Неизвестная команда: {command.Kind}"),
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            result = new CommandResult(false, e.Message);
        }

        try
        {
            await api.CompleteCommandAsync(command.Id, result, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning("1С: итог команды не записан в OneBase — {Error}", e.Message);
        }
    }

    private async Task<CommandResult> TestConnectionAsync(CancellationToken ct)
    {
        await CheckConnectionAsync(ct);
        var report = status.Connection;
        return new CommandResult(report?.Ok == true, report?.Message, report is null ? null : JsonSerializer.SerializeToElement(report, Json));
    }

    private static CommandResult Result(SyncOutcome outcome) => new(
        outcome.Status is RunStatuses.Succeeded or RunStatuses.Partial, outcome.Message,
        JsonSerializer.SerializeToElement(new { outcome.Status, outcome.RunId, outcome.Details }, Json));

    private async Task Delay(TimeSpan span, CancellationToken ct)
    {
        try
        {
            await Task.Delay(span, clock, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}

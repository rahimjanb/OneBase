using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Очередь автоотчётов в PostgreSQL. Наступил запуск расписания — строка журнала запусков (один запуск на код и день: второй экземпляр
/// службы его не начнёт) и по строке отправки на каждый чат каждого получателя этого запуска; ключ «запуск × день × чат × область»
/// уникален — человек в двух получателях или повторный круг дубля не создадут. Затем строки отправляются по одной; ошибка одного получателя
/// не останавливает остальных. Запуск, пропущенный дольше CatchUpMinutes (сервер не работал), не отправляется — в журнал «пропущен».
/// «Отправляется» дольше 10 минут (сервер упал посреди отправки) — «неизвестно»: лучше пропуск, чем дубль.
/// </summary>
public sealed class TelegramReportRunner(IServiceScopeFactory scopes, TimeProvider clock, ILogger<TelegramReportRunner> logger)
{
    /// <summary>Пауза между отправками одного аккаунта (в тестах — без ожидания).</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    /// <summary>Обновление данных Linko перед отправкой; в тестах — подставное.</summary>
    public Func<IServiceProvider, CancellationToken, Task>? Refresh { get; set; }

    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(3);

    /// <summary>Свежее этого данные Linko перед отправкой не обновляются.</summary>
    public static readonly TimeSpan FreshEnough = TimeSpan.FromMinutes(10);

    public async Task<int> TickAsync(CancellationToken ct)
    {
        await EnqueueDueAsync(ct);
        var sent = await SendPendingAsync(ct);
        await FinishRunsAsync(ct);
        return sent;
    }

    /// <summary>Наступившие запуски → журнал запусков и строки отправок «в очереди»; пропущенные после простоя — «пропущен».</summary>
    public async Task<int> EnqueueDueAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var now = clock.GetUtcNow();
        await db.TelegramReportDeliveries.Where(d => d.Status == TelegramReportSender.Sending && d.LastAttemptAt < now - TelegramRules.StaleSending)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, TelegramReportSender.Unknown).SetProperty(d => d.NextAttemptAt, (DateTimeOffset?)null)
                .SetProperty(d => d.Error, "Неизвестно, дошёл ли отчёт: сервер прервал отправку. Автоповтора нет, чтобы не отправить дважды — проверьте чат и при необходимости отправьте вручную."), ct);
        // Строки прежнего вида дневного отчёта, оставшиеся в очереди при обновлении, — не отправляются.
        await db.TelegramReportDeliveries.Where(d => d.Report == null && d.Status == TelegramReportSender.Pending)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, TelegramReportSender.Skipped).SetProperty(d => d.NextAttemptAt, (DateTimeOffset?)null)
                .SetProperty(d => d.Error, "Автоотчёты обновлены: отправка прежнего вида отменена."), ct);

        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var jobs = await db.TelegramReportJobs.AsNoTracking().Where(j => j.Enabled).ToListAsync(ct);
        var added = 0;
        var refreshed = false;
        foreach (var job in jobs)
        {
            if (ReportCatalog.Find(job.Code) is not { } info)
            {
                continue;
            }

            if (ReportSchedules.Missed(job, now) is { } missed && !await db.TelegramReportRuns.AnyAsync(r => r.Job == job.Code && r.Period == missed.Period, ct))
            {
                await CreateRunAsync(db, new TelegramReportRun
                {
                    Job = job.Code, Period = missed.Period, ScheduledAt = missed.At, StartedAt = now, FinishedAt = now, Status = "skipped",
                    Error = $"Сервер не работал в {ReportSchedules.Local(missed.At, job.TimeZone):dd.MM HH:mm}: отчёт старше {job.CatchUpMinutes} мин не отправлен.",
                }, ct);
            }

            if (ReportSchedules.Due(job, now) is not { } slot)
            {
                continue;
            }

            var run = await db.TelegramReportRuns.FirstOrDefaultAsync(r => r.Job == job.Code && r.Period == slot.Period, ct);
            if (run is { Status: "skipped" })
            {
                continue; // запуск пропущен после простоя — не отправляется
            }

            if (settings?.AccountId is not { } account)
            {
                if (run is null)
                {
                    await CreateRunAsync(db, new TelegramReportRun
                    {
                        Job = job.Code, Period = slot.Period, ScheduledAt = slot.At, StartedAt = now, FinishedAt = now, Status = "failed",
                        Error = "Не выбран аккаунт Telegram автоотчётов — «Автоматические отчёты → Расписание».",
                    }, ct);
                }

                continue;
            }

            if (run is null)
            {
                if (settings.RefreshBeforeSend && !refreshed)
                {
                    await RefreshAsync(sp, ct);
                    refreshed = true;
                }

                run = await CreateRunAsync(db, new TelegramReportRun { Job = job.Code, Period = slot.Period, ScheduledAt = slot.At, StartedAt = now }, ct);
                if (run is null)
                {
                    continue; // запуск начал другой экземпляр службы
                }

                run.DataAsOf = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
                await db.SaveChangesAsync(ct);
            }

            var queued = await EnqueueRunAsync(db, run, info, account, ct);
            if (queued > 0 && run.Status != "running")
            {
                // Получатель добавлен, пока не прошло время догона: запуск снова идёт.
                run.Status = "running";
                run.FinishedAt = null;
                await db.SaveChangesAsync(ct);
            }

            added += queued;
        }

        return added;
    }

    /// <summary>Строки отправок запуска: каждый включённый получатель с этим запуском × каждый его чат; ключ — запуск, день, чат, область.</summary>
    private async Task<int> EnqueueRunAsync(OneBaseDbContext db, TelegramReportRun run, ReportJobInfo info, Guid account, CancellationToken ct)
    {
        var recipients = (await db.TelegramReportSubscriptions.AsNoTracking().Include(s => s.Chats).Where(s => s.Enabled).ToListAsync(ct))
            .Where(s => ReportCatalog.Parse(s.Reports).Contains(info.Code) && info.Roles.Contains(s.Role))
            .ToList();
        var done = (await db.TelegramReportDeliveries.AsNoTracking()
                .Where(d => d.Trigger == "schedule" && d.Report == info.Code && d.Period == run.Period && d.ChatId != null)
                .Select(d => new { Chat = d.ChatId!.Value, d.ScopeKey }).ToListAsync(ct))
            .Select(d => (d.Chat, d.ScopeKey ?? ""))
            .ToHashSet();
        var added = 0;
        foreach (var recipient in recipients)
        {
            var key = ReportScope.Of(recipient).Key;
            foreach (var chat in recipient.Chats.Where(c => done.Add((c.ChatId, key))))
            {
                db.TelegramReportDeliveries.Add(new TelegramReportDelivery
                {
                    Report = info.Code,
                    RunId = run.Id,
                    Period = run.Period,
                    Trigger = "schedule",
                    DataMode = "real",
                    SubscriptionId = recipient.Id,
                    Role = recipient.Role,
                    ScopeKey = key,
                    AccountId = account,
                    ChatId = chat.ChatId,
                    RecipientTitle = "…",
                    ScopeTitle = "…",
                    RegionId = recipient.RegionId,
                    SupervisorId = recipient.SupervisorId,
                    CreatedById = recipient.CreatedById,
                });
                try
                {
                    await db.SaveChangesAsync(ct);
                    added++;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    // Эту строку уже поставил другой экземпляр или прошлый круг — второй раз не отправляем.
                    Detach(db);
                }
            }
        }

        if (added > 0)
        {
            logger.LogInformation("Автоотчёт {Job} за {Period}: в очередь {Count} отправок", info.Code, run.Period, added);
        }

        return added;
    }

    private static async Task<TelegramReportRun?> CreateRunAsync(OneBaseDbContext db, TelegramReportRun run, CancellationToken ct)
    {
        db.TelegramReportRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(ct);
            return run;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            Detach(db);
            return null;
        }
    }

    private static void Detach(OneBaseDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Свежие заказы и визиты перед отправкой: последнее обновление старше 10 минут — обычное обновление Linko (идёт плановое — дождаться,
    /// до 10 минут). Linko недоступен — отчёт уйдёт по последним данным с пометкой «данные на …» или «неполные данные».
    /// </summary>
    private async Task RefreshAsync(IServiceProvider sp, CancellationToken ct)
    {
        if (Refresh is { } refresh)
        {
            await refresh(sp, ct);
            return;
        }

        var db = sp.GetRequiredService<IAppDbContext>();
        var last = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        if (last is { } l && clock.GetUtcNow() - l < FreshEnough)
        {
            return;
        }

        var coordinator = sp.GetService<LinkoSyncCoordinator>();
        if (coordinator is null)
        {
            return;
        }

        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(TimeSpan.FromMinutes(10));
            if (await coordinator.RunAsync(LinkoSyncMode.Incremental, limit.Token) is null)
            {
                // Идёт другое обновление (плановое в 11:00 / 17:00) — дождаться его.
                while (coordinator.IsRunning && !limit.IsCancellationRequested)
                {
                    await Delay(TimeSpan.FromSeconds(15), limit.Token);
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Автоотчёты: данные Linko перед отправкой не обновились — отчёты уйдут по последним данным");
        }
    }

    /// <summary>Отправить готовые строки (по расписанию и ручные с повтором) по одной, с паузой. Возвращает, сколько ушло.</summary>
    public async Task<int> SendPendingAsync(CancellationToken ct)
    {
        var sent = 0;
        var tried = new HashSet<Guid>();
        while (!ct.IsCancellationRequested)
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<IAppDbContext>();
            var now = clock.GetUtcNow();
            var delivery = await db.TelegramReportDeliveries
                .Where(d => d.Status == TelegramReportSender.Pending && d.Report != null && d.ChatId != null && (d.NextAttemptAt == null || d.NextAttemptAt <= now)
                            && !tried.Contains(d.Id))
                .OrderBy(d => d.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (delivery is null)
            {
                return sent;
            }

            tried.Add(delivery.Id);
            var result = await ProcessAsync(sp, delivery, ct);
            if (result.Status == TelegramReportSender.Sent)
            {
                sent++;
                await Delay(Pause, ct);
            }
        }

        return sent;
    }

    /// <summary>
    /// Строка отправки: получатель ещё включён и подписан, аккаунт подключён, чат доступен (иначе — «пропущено» с причиной), отчёт области
    /// (область — из ключа строки: права проверяются при построении) и отправка.
    /// </summary>
    private static async Task<ReportSendResult> ProcessAsync(IServiceProvider sp, TelegramReportDelivery delivery, CancellationToken ct)
    {
        var db = sp.GetRequiredService<IAppDbContext>();
        var sender = sp.GetRequiredService<TelegramReportSender>();
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == delivery.ChatId, ct);
        delivery.RecipientTitle = TelegramReportSender.Trim(chat?.Title ?? "—", 255);
        if (delivery.SubscriptionId is { } subscriptionId)
        {
            var subscription = await db.TelegramReportSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
            if (subscription is null || !subscription.Enabled || (delivery.Trigger == "schedule" && !ReportCatalog.Parse(subscription.Reports).Contains(delivery.Report!)))
            {
                return await sender.SkipAsync(delivery, subscription is null ? "Получатель удалён." : "Получатель отключён или отписан от этого отчёта.");
            }
        }

        if (ReportScope.Parse(delivery.ScopeKey) is not { } scope)
        {
            return await sender.FailAsync(delivery, "Область отчёта не определена.");
        }

        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var account = settings?.AccountId is { } accountId ? await db.TelegramAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == accountId, ct) : null;
        if (account is not null)
        {
            delivery.AccountId = account.Id; // аккаунт сменили после постановки в очередь — шлём с текущего
        }

        if (account is not { Status: TelegramAccountStatus.Connected })
        {
            return await sender.FailAsync(delivery, "Аккаунт Telegram автоотчётов не подключён — «Настройки → Telegram → Подключение».");
        }

        if (TelegramReportSender.ChatProblem(chat, account.Id) is { } problem)
        {
            return await sender.SkipAsync(delivery, problem);
        }

        var job = delivery.Report!;
        var period = delivery.Period;
        return await sender.AttemptAsync(delivery, chat!, token => sender.BuildAsync(job, scope, period, null, null, token), retry: true, ct);
    }

    /// <summary>Запуски, у которых не осталось отправок в очереди: итог (done / partial) и счётчики.</summary>
    public async Task FinishRunsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var now = clock.GetUtcNow();
        foreach (var run in await db.TelegramReportRuns.Where(r => r.Status == "running").ToListAsync(ct))
        {
            var statuses = await db.TelegramReportDeliveries.AsNoTracking().Where(d => d.RunId == run.Id).Select(d => d.Status).ToListAsync(ct);
            run.Planned = statuses.Count;
            run.Sent = statuses.Count(s => s == TelegramReportSender.Sent);
            run.Failed = statuses.Count(s => s is TelegramReportSender.Failed or TelegramReportSender.Unknown);
            run.Skipped = statuses.Count(s => s == TelegramReportSender.Skipped);
            if (statuses.Any(s => s is TelegramReportSender.Pending or TelegramReportSender.Sending))
            {
                continue;
            }

            run.Status = run.Failed > 0 ? "partial" : "done";
            run.FinishedAt = now;
            run.Error = run.Planned == 0 ? "Нет получателей с этим отчётом." : null;
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Фоновая служба автоотчётов: раз в 30 секунд — наступившие запуски, очередь отправки, итоги запусков. В среде, где Telegram выключен
/// (локально: Telegram:Enabled=false — аккаунт работает на проде), не запускается: две копии одной сессии разорвали бы её.
/// </summary>
internal sealed class TelegramReportWorker(TelegramReportRunner runner, IConfiguration config, ILogger<TelegramReportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Telegram:Enabled", true))
        {
            logger.LogInformation("Автоотчёты: Telegram выключен в этой среде (Telegram:Enabled=false)");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            do
            {
                try
                {
                    await runner.TickAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Обрыв сети или базы — не остановка сервера: в журнал, следующий круг через 30 с.
                    logger.LogWarning(ex, "Автоотчёты: круг не выполнен");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // остановка сервера
        }
    }
}

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
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Очередь автоотчётов (строки журнала в PostgreSQL): наступило время расписания — по строке на каждый чат каждой автоотправки с этим
/// отчётом (уникальный индекс: автоотправка × чат × отчёт × день — второй экземпляр службы или повторный круг дубля не создадут); затем
/// строки отправляются по одной. «Отправляется» дольше 10 минут (сервер упал посреди отправки) — ошибка без автоповтора: лучше пропуск,
/// чем дубль.
/// </summary>
public sealed class TelegramReportRunner(IServiceScopeFactory scopes, TimeProvider clock, ILogger<TelegramReportRunner> logger)
{
    /// <summary>Пауза между отправками одного аккаунта (в тестах — без ожидания).</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(3);

    /// <summary>Один круг: запуски, которым пора, → строки в очередь; затем отправка готовых строк.</summary>
    public async Task<int> TickAsync(CancellationToken ct)
    {
        await EnqueueDueAsync(ct);
        return await SendPendingAsync(ct);
    }

    /// <summary>Наступившие запуски расписаний → строки журнала «ожидает»: каждая включённая автоотправка этого отчёта × каждый её чат.</summary>
    public async Task<int> EnqueueDueAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var now = clock.GetUtcNow();
        await db.TelegramReportDeliveries.Where(d => d.Status == TelegramReportSender.Sending && d.LastAttemptAt < now - TelegramRules.StaleSending)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, TelegramReportSender.Failed).SetProperty(d => d.NextAttemptAt, (DateTimeOffset?)null)
                .SetProperty(d => d.Error, "Неизвестно, дошёл ли отчёт: сервер прервал отправку. Автоповтора нет, чтобы не отправить дважды — проверьте чат и при необходимости отправьте вручную."), ct);

        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings?.AccountId is not { } account)
        {
            return 0;
        }

        var added = 0;
        foreach (var schedule in await db.TelegramReportSchedules.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct))
        {
            if (DailyReportSchedules.Due(schedule, now) is not { } slot)
            {
                continue;
            }

            var kind = schedule.Kind;
            var subscriptions = await db.TelegramReportSubscriptions.AsNoTracking().Include(s => s.Chats)
                .Where(s => s.Enabled && (kind == TelegramReportKind.Morning ? s.Morning : kind == TelegramReportKind.Midday ? s.Midday : s.Evening))
                .ToListAsync(ct);
            var done = (await db.TelegramReportDeliveries.AsNoTracking()
                    .Where(d => d.Trigger == "schedule" && d.Kind == kind && d.Period == slot.Period && d.SubscriptionId != null && d.ChatId != null)
                    .Select(d => new { Subscription = d.SubscriptionId!.Value, Chat = d.ChatId!.Value }).ToListAsync(ct))
                .Select(d => (d.Subscription, d.Chat))
                .ToHashSet();
            var queued = 0;
            foreach (var subscription in subscriptions)
            {
                foreach (var chat in subscription.Chats.Where(c => !done.Contains((subscription.Id, c.ChatId))))
                {
                    db.TelegramReportDeliveries.Add(new TelegramReportDelivery
                    {
                        Kind = kind,
                        Period = slot.Period,
                        Trigger = "schedule",
                        DataMode = "real",
                        SubscriptionId = subscription.Id,
                        AccountId = account,
                        ChatId = chat.ChatId,
                        RecipientTitle = "…",
                        ScopeTitle = "…",
                        RegionId = subscription.RegionId,
                        SupervisorId = subscription.SupervisorId,
                        CreatedById = subscription.CreatedById,
                    });
                    try
                    {
                        await db.SaveChangesAsync(ct);
                        added++;
                        queued++;
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                    {
                        // Эту строку уже поставил другой экземпляр или прошлый круг — второй раз не отправляем.
                        foreach (var entry in db.ChangeTracker.Entries<TelegramReportDelivery>().Where(e => e.State == EntityState.Added).ToList())
                        {
                            entry.State = EntityState.Detached;
                        }
                    }
                }
            }

            if (queued > 0)
            {
                logger.LogInformation("Автоотчёт {Kind} за {Period}: в очередь {Count} отправок", kind, slot.Period, queued);
            }
        }

        return added;
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
                .Where(d => d.Status == TelegramReportSender.Pending && d.SubscriptionId != null && d.ChatId != null && (d.NextAttemptAt == null || d.NextAttemptAt <= now)
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

    /// <summary>Строка автоотправки: аккаунт подключён, чат доступен, отчёт региона и супервайзера на реальных данных, отправка.</summary>
    private static async Task<ReportSendResult> ProcessAsync(IServiceProvider sp, TelegramReportDelivery delivery, CancellationToken ct)
    {
        var db = sp.GetRequiredService<IAppDbContext>();
        var sender = sp.GetRequiredService<TelegramReportSender>();
        var subscription = await db.TelegramReportSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == delivery.SubscriptionId, ct);
        if (subscription is null)
        {
            return await sender.FailAsync(delivery, "Автоотправка удалена.");
        }

        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == delivery.ChatId, ct);
        delivery.RecipientTitle = TelegramReportSender.Trim(chat?.Title ?? "—", 255);
        delivery.ScopeTitle = await sender.ScopeTitleAsync(subscription.RegionId, subscription.SupervisorId, subscription.SupervisorName, ct);
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
            return await sender.FailAsync(delivery, problem);
        }

        var kind = delivery.Kind;
        var period = delivery.Period;
        return await sender.AttemptAsync(delivery, chat!,
            token => sender.BuildRealAsync(kind, subscription.RegionId, subscription.SupervisorId, subscription.SupervisorName, period, check: false, token), retry: true, ct);
    }
}

/// <summary>
/// Фоновая служба автоотчётов: раз в 30 секунд — наступившие запуски расписаний и очередь отправки. В среде, где Telegram выключен
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

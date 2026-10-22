using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Одна отправка автоотчёта в один чат: отчёт → сверка итогов → PNG 16:9 → Telegram → журнал. Временная ошибка сети — повтор (до 3
/// попыток), FLOOD_WAIT — ожидание без попытки, остальное — ошибка в журнале с понятной причиной.
/// </summary>
public sealed class TelegramReportSender(
    IAppDbContext db,
    ITelegramGateway gateway,
    IDailyReportRenderer renderer,
    DailyReportBuilder builder,
    TelegramMaterialService materials,
    TimeProvider clock,
    ILogger<TelegramReportSender> logger)
{
    public const string Sent = "sent";
    public const string Pending = "pending";
    public const string Sending = "sending";
    public const string Failed = "failed";

    /// <summary>Почему в чат нельзя отправить; null — можно.</summary>
    public static string? ChatProblem(TelegramChat? chat, Guid? accountId) => chat switch
    {
        null => "Чат не найден у аккаунта автоотчётов — обновите чаты.",
        _ when chat.AccountId != accountId => "Чат принадлежит другому аккаунту Telegram.",
        { CanSend: false } => chat.CannotSendReason ?? "В этот чат писать нельзя.",
        _ => null,
    };

    /// <summary>Отчёт на реальных данных — с показателем из расписания вида и языком из настроек.</summary>
    public async Task<DailyReport> BuildRealAsync(TelegramReportKind kind, Guid regionId, long? supervisorId, string? supervisorName, DateOnly period, bool check,
        CancellationToken ct)
    {
        var schedule = await db.TelegramReportSchedules.AsNoTracking().FirstOrDefaultAsync(s => s.Kind == kind, ct) ?? DailyReportSchedules.Default(kind);
        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var now = DailyReportSchedules.Local(clock.GetUtcNow(), schedule.TimeZone);
        return await builder.BuildAsync(kind, regionId, supervisorId, supervisorName, period, schedule.Metric == "kg" ? ReportMetric.Kg : ReportMetric.Sum,
            settings?.Language ?? "ru", now, check, ct);
    }

    /// <summary>«Ташкент · Азиз Каримов» — регион и супервайзер для журнала.</summary>
    public async Task<string> ScopeTitleAsync(Guid regionId, long? supervisorId, string? supervisorName, CancellationToken ct)
    {
        var region = await db.SalesRegions.AsNoTracking().Where(r => r.Id == regionId).Select(r => new { r.Name, r.SupervisorName }).FirstOrDefaultAsync(ct);
        var supervisor = supervisorId is not null ? supervisorName : region?.SupervisorName;
        var title = string.Join(" · ", new[] { region?.Name ?? "—", supervisor }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return Trim(title, 255);
    }

    /// <summary>
    /// Одна попытка: отчёт → сверка → картинки → Telegram. Строка журнала обновляется в любом исходе. retry — временная ошибка ставит
    /// повтор (по расписанию и вручную); без него (тест) — сразу ошибка.
    /// </summary>
    public async Task<ReportSendResult> AttemptAsync(TelegramReportDelivery delivery, TelegramChat chat, Func<CancellationToken, Task<DailyReport>> build, bool retry,
        CancellationToken ct)
    {
        delivery.Status = Sending;
        delivery.Attempts++;
        delivery.LastAttemptAt = clock.GetUtcNow();
        delivery.ChatId = chat.Id;
        delivery.PeerId = chat.PeerId;
        await db.SaveChangesAsync(ct);

        try
        {
            var report = await build(ct);
            if (DailyReportMath.Verify(report) is { Count: > 0 } problems)
            {
                // Итоги не сходятся с суммой строк — такой отчёт не уходит: лучше без отчёта, чем с неверными цифрами.
                return await FailAsync(delivery, "Отчёт не отправлен: итоги не сходятся с суммой строк. " + string.Join(" ", problems));
            }

            var pages = renderer.Render(report);
            delivery.Pages = pages.Count;
            delivery.Bytes = pages.Sum(p => (long)p.Length);
            delivery.Summary = Trim(DailyReportText.Summary(report), 500);
            delivery.DataAsOf = report.DataAsOf?.ToUniversalTime(); // в базу — только UTC
            var files = pages.Select((p, i) => new TelegramFile($"onebase-{delivery.Kind.ToString().ToLowerInvariant()}-{report.Date:yyyyMMdd}-{i + 1}.png", "image/png", p, Photo: true))
                .ToList();
            var sent = await gateway.SendAsync(delivery.AccountId, new TelegramPeerRef(chat.PeerId, chat.Type, chat.IsChannelPeer, chat.AccessHash),
                new TelegramOutgoing(Trim(DailyReportText.Caption(report), TelegramRules.MaxCaptionLength), false, files), ct);

            delivery.Status = Sent;
            delivery.SentAt = clock.GetUtcNow();
            delivery.Error = null;
            delivery.NextAttemptAt = null;
            delivery.MessageIds = Trim(string.Join(',', sent.MessageIds), 500);
            await db.SaveChangesAsync(CancellationToken.None);

            // Реальный отчёт — материал чата: Telegram-агент ответит на «что в отчёте?» по тому, что ушло. Демонстрационный — нет.
            if (!report.Demo)
            {
                try
                {
                    await materials.RecordTextAsync(delivery.AccountId, chat.PeerId, "report",
                        $"{DailyReportTexts.Name(report.Kind, report.Language)} · {report.Region} · {report.Date:dd.MM.yyyy}", DailyReportText.Plain(report), null,
                        delivery.CreatedById, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Автоотчёт {Delivery}: материал для чата не записан", delivery.Id);
                }
            }

            logger.LogInformation("Автоотчёт {Kind} за {Period} отправлен: {Recipient}, страниц {Pages}", delivery.Kind, delivery.Period, delivery.RecipientTitle, pages.Count);
            return Result(delivery);
        }
        catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Flood)
        {
            // Telegram просит подождать — это не ошибка получателя: попытка не считается.
            delivery.Status = Pending;
            delivery.Attempts = Math.Max(0, delivery.Attempts - 1);
            delivery.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Max(ex.WaitSeconds ?? 30, 5));
            delivery.Error = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            return Result(delivery);
        }
        catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Transient && retry && delivery.Attempts < TelegramRules.MaxAttempts)
        {
            delivery.Status = Pending;
            delivery.NextAttemptAt = clock.GetUtcNow() + TelegramRules.RetryDelay(delivery.Attempts);
            delivery.Error = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            return Result(delivery);
        }
        catch (TelegramException ex)
        {
            return await FailAsync(delivery, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return await FailAsync(delivery, ex.Message);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Автоотчёт {Delivery}: ошибка формирования или отправки", delivery.Id);
            return await FailAsync(delivery, "Внутренняя ошибка OneBase — подробности в журнале сервера.");
        }
    }

    public async Task<ReportSendResult> FailAsync(TelegramReportDelivery delivery, string error)
    {
        delivery.Status = Failed;
        delivery.Error = Trim(error, 1000);
        delivery.NextAttemptAt = null;
        await db.SaveChangesAsync(CancellationToken.None);
        logger.LogWarning("Автоотчёт {Kind} за {Period} не отправлен ({Recipient}): {Error}", delivery.Kind, delivery.Period, delivery.RecipientTitle, delivery.Error);
        return Result(delivery);
    }

    public static ReportSendResult Result(TelegramReportDelivery d) =>
        new(d.Id, d.Status, d.RecipientTitle, d.ScopeTitle, d.Error, d.Pages, d.SentAt, d.NextAttemptAt);

    public static string Trim(string text, int max) => text.Length > max ? text[..(max - 1)] + "…" : text;
}

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using ReportDocument = OneBase.Application.Telegram.Reports.ReportDocument;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Одна отправка автоотчёта в один чат: отчёт области → PNG 16:9 → Telegram → журнал. Условие не выполнено (напоминание ТП с заказами) —
/// «пропущено». Картинка не построилась — уходит текст с ключевыми цифрами (резерв). FLOOD_WAIT — ожидание без попытки; сбой связи —
/// повтор (до 3 попыток); тайм-аут посреди отправки — «неизвестно, дошёл ли»: автоповтора нет, чтобы не отправить дважды.
/// </summary>
public sealed class TelegramReportSender(
    IAppDbContext db,
    ITelegramGateway gateway,
    IReportRenderer renderer,
    ReportEngine engine,
    TelegramMaterialService materials,
    TimeProvider clock,
    ILogger<TelegramReportSender> logger)
{
    public const string Sent = "sent";
    public const string Pending = "pending";
    public const string Sending = "sending";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
    public const string Unknown = "unknown";

    /// <summary>Почему в чат нельзя отправить; null — можно.</summary>
    public static string? ChatProblem(TelegramChat? chat, Guid? accountId) => chat switch
    {
        null => "Чат не найден у аккаунта автоотчётов — обновите чаты в «Настройки → Telegram → Чаты».",
        _ when chat.AccountId != accountId => "Чат принадлежит другому аккаунту Telegram.",
        { CanSend: false } => chat.CannotSendReason ?? "В этот чат писать нельзя (бот заблокирован или нет прав).",
        _ => null,
    };

    /// <summary>
    /// Отчёт запуска для области на реальных данных. at — момент формирования (предпросмотр «на 13:00»); по умолчанию — сейчас в поясе
    /// расписания. Язык — из настроек, если не задан.
    /// </summary>
    public async Task<ReportDocument> BuildAsync(string job, ReportScope scope, DateOnly period, DateTimeOffset? at, string? language, CancellationToken ct)
    {
        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var schedule = await db.TelegramReportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Code == job, ct);
        var now = at ?? ReportSchedules.Local(clock.GetUtcNow(), schedule?.TimeZone);
        return await engine.BuildAsync(job, scope, period, now, language ?? settings?.Language ?? "ru", settings?.NoPurchaseDays ?? 30, ct);
    }

    /// <summary>
    /// Одна попытка: отчёт → картинки → Telegram. Строка журнала обновляется в любом исходе. retry — временная ошибка ставит повтор (по
    /// расписанию и вручную); без него (тест) — сразу ошибка.
    /// </summary>
    public async Task<ReportSendResult> AttemptAsync(TelegramReportDelivery delivery, TelegramChat chat, Func<CancellationToken, Task<ReportDocument>> build, bool retry,
        CancellationToken ct, bool test = false)
    {
        var watch = Stopwatch.StartNew();
        delivery.Status = Sending;
        delivery.Attempts++;
        delivery.LastAttemptAt = clock.GetUtcNow();
        delivery.ChatId = chat.Id;
        delivery.PeerId = chat.PeerId;
        await db.SaveChangesAsync(ct);

        try
        {
            var report = await build(ct) with { Test = test };
            delivery.ScopeTitle = Trim(report.Scope, 255);
            delivery.Summary = Trim(report.Summary, 500);
            delivery.DataAsOf = report.DataAsOf?.ToUniversalTime(); // в базу — только UTC
            if (!report.Applies)
            {
                return await SkipAsync(delivery, report.SkipReason ?? "Условие отчёта не выполнено.", watch);
            }

            IReadOnlyList<byte[]> pages;
            try
            {
                pages = renderer.Render(report);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Картинка не построилась — уходит текст: получатель всё равно узнаёт цифры.
                logger.LogError(ex, "Автоотчёт {Delivery}: картинка не построена — отправка текстом", delivery.Id);
                pages = [];
            }

            delivery.Pages = pages.Count;
            delivery.Bytes = pages.Sum(p => (long)p.Length);
            var files = pages.Select((p, i) => new TelegramFile($"onebase-{report.Job.Replace('.', '-')}-{report.Date:yyyyMMdd}-{i + 1}.png", "image/png", p, Photo: true)).ToList();
            var text = pages.Count > 0 ? Trim(report.Caption, TelegramRules.MaxCaptionLength) : Trim(report.Plain(), TelegramRules.MaxMessageLength);
            var sent = await gateway.SendAsync(delivery.AccountId, new TelegramPeerRef(chat.PeerId, chat.Type, chat.IsChannelPeer, chat.AccessHash),
                new TelegramOutgoing(text, false, files), ct);

            delivery.Status = Sent;
            delivery.SentAt = clock.GetUtcNow();
            delivery.Error = pages.Count == 0 ? "Картинка не построилась — отчёт отправлен текстом." : null;
            delivery.NextAttemptAt = null;
            delivery.MessageIds = Trim(string.Join(',', sent.MessageIds), 500);
            delivery.DurationMs = (int)watch.ElapsedMilliseconds;
            await db.SaveChangesAsync(CancellationToken.None);

            // Реальный отчёт — материал чата: Telegram-агент ответит на «что в отчёте?» по тому, что ушло.
            try
            {
                await materials.RecordTextAsync(delivery.AccountId, chat.PeerId, "report", $"{report.Title} · {report.Scope} · {report.Period}", report.Plain(), null,
                    delivery.CreatedById, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Автоотчёт {Delivery}: материал для чата не записан", delivery.Id);
            }

            logger.LogInformation("Автоотчёт {Job} за {Period} отправлен: {Recipient}, страниц {Pages}", delivery.Report, delivery.Period, delivery.RecipientTitle, pages.Count);
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
        catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Transient && IsTimeout(ex))
        {
            // Тайм-аут посреди отправки: Telegram мог принять сообщение. Повтор — только вручную, после проверки чата.
            delivery.Status = Unknown;
            delivery.NextAttemptAt = null;
            delivery.Error = "Неизвестно, дошёл ли отчёт: Telegram не ответил вовремя. Автоповтора нет — проверьте чат и при необходимости отправьте вручную.";
            delivery.DurationMs = (int)watch.ElapsedMilliseconds;
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
            return await FailAsync(delivery, ex.Kind == TelegramErrorKind.Permanent ? $"Получатель недоступен: {ex.Message}" : ex.Message, watch);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
        {
            return await FailAsync(delivery, ex.Message, watch);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Автоотчёт {Delivery}: ошибка формирования или отправки", delivery.Id);
            return await FailAsync(delivery, "Внутренняя ошибка OneBase — подробности в журнале сервера.", watch);
        }
    }

    private static bool IsTimeout(TelegramException ex) =>
        ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("не ответил", StringComparison.OrdinalIgnoreCase);

    public async Task<ReportSendResult> FailAsync(TelegramReportDelivery delivery, string error, Stopwatch? watch = null)
    {
        delivery.Status = Failed;
        delivery.Error = Trim(error, 1000);
        delivery.NextAttemptAt = null;
        delivery.DurationMs = watch is null ? delivery.DurationMs : (int)watch.ElapsedMilliseconds;
        await db.SaveChangesAsync(CancellationToken.None);
        logger.LogWarning("Автоотчёт {Job} за {Period} не отправлен ({Recipient}): {Error}", delivery.Report, delivery.Period, delivery.RecipientTitle, delivery.Error);
        return Result(delivery);
    }

    public async Task<ReportSendResult> SkipAsync(TelegramReportDelivery delivery, string reason, Stopwatch? watch = null)
    {
        delivery.Status = Skipped;
        delivery.Error = Trim(reason, 1000);
        delivery.NextAttemptAt = null;
        delivery.DurationMs = watch is null ? delivery.DurationMs : (int)watch.ElapsedMilliseconds;
        await db.SaveChangesAsync(CancellationToken.None);
        return Result(delivery);
    }

    public static ReportSendResult Result(TelegramReportDelivery d) =>
        new(d.Id, d.Status, d.RecipientTitle, d.ScopeTitle, d.Error, d.Pages, d.SentAt, d.NextAttemptAt);

    public static string Trim(string text, int max) => text.Length > max ? text[..(max - 1)] + "…" : text;
}

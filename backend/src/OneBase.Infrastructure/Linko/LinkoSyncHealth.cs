using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Sales;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Linko;

/// <summary>
/// Контроль обмена с Linko после каждой синхронизации по расписанию (Sales:Sync:IntervalMinutes и Times): если этот запуск не обновил заказы, возвраты,
/// визиты, остатки, перемещения или бронь — уведомление (колокольчик и push) тем, кто управляет интеграциями; когда обмен восстановился —
/// второе уведомление. По одному на проблему: открытая проблема хранится в строке «alert». Данные устарели — дольше самого долгого
/// промежутка между запусками и часа запаса (при запуске каждые 30 минут — 1,5 часа).
/// </summary>
public sealed class LinkoSyncHealth(OneBaseDbContext db, SalesOptions options, TimeProvider time, ILogger<LinkoSyncHealth> logger)
{
    /// <summary>Без привязки к запуску данные устарели, если не обновлялись дольше самого долгого промежутка между запусками и часа запаса.</summary>
    public TimeSpan StaleAfter => LinkoSyncSchedule.LongestGap(LinkoSyncSchedule.TimesOf(options.Sync)) + TimeSpan.FromHours(1);

    /// <summary>
    /// Проверяет обмен; true — отправлено уведомление (о проблеме или о восстановлении). <paramref name="since"/> — начало только что
    /// прошедшей синхронизации: всё, что она не обновила, — проблема; null — проверка по <see cref="StaleAfter"/>.
    /// </summary>
    public async Task<bool> CheckAsync(DateTimeOffset? since = null, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var states = await db.LinkoSyncStates.ToListAsync(ct);
        var staleAfter = since is { } s && s <= now ? now - s : StaleAfter;
        var problem = LinkoSyncSchedule.Problem(states, now, staleAfter);
        var alert = states.FirstOrDefault(s => s.Entity == LinkoSyncSchedule.AlertState);
        var open = alert?.LastError;
        if ((problem is null && open is null) || (problem is not null && open is not null))
        {
            return false; // ничего не поменялось: проблемы нет — молчим, уже сообщили — не повторяем
        }

        if (alert is null)
        {
            alert = new LinkoSyncState { Entity = LinkoSyncSchedule.AlertState };
            db.LinkoSyncStates.Add(alert);
        }

        var recipients = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role.Permissions.Any(p => p.Code == Permissions.IntegrationsManage)))
            .Select(u => u.Id)
            .ToListAsync(ct);
        var (title, body) = problem is not null
            ? ("Обмен с Linko не работает — данные в отчётах устарели",
                $"{problem} {NextRunText(now)}Проверьте токен и доступность Linko в «Настройки → Интеграции» и обновите данные кнопкой «Обновить».")
            : ("Обмен с Linko восстановлен", "Данные снова обновляются автоматически, отчёты пересчитаны.");
        foreach (var recipient in recipients)
        {
            db.UserNotifications.Add(new UserNotification
            {
                RecipientId = recipient,
                Kind = UserNotificationKind.DataSyncProblem,
                Title = title,
                Body = body.Length > 2000 ? body[..1999] + "…" : body,
                Link = "/settings/integrations/sales/linko",
            });
        }

        alert.LastError = problem is null ? null : problem.Length > 1000 ? problem[..1000] : problem;
        alert.LastRunAt = now;
        if (problem is null)
        {
            alert.LastSuccessAt = now;
        }

        await db.SaveChangesAsync(ct);
        if (problem is not null)
        {
            logger.LogWarning("Linko: обмен не работает — {Problem}; уведомлено {Count}", problem, recipients.Count);
        }
        else
        {
            logger.LogInformation("Linko: обмен восстановлен; уведомлено {Count}", recipients.Count);
        }

        return true;
    }

    private string NextRunText(DateTimeOffset now)
    {
        var times = LinkoSyncSchedule.TimesOf(options.Sync);
        return times.Count == 0
            ? ""
            : $"Следующая синхронизация по расписанию — {LinkoSyncSchedule.NextRun(now, times).ToOffset(LinkoSyncSchedule.CompanyOffset):dd.MM HH:mm} (Ташкент). ";
    }
}

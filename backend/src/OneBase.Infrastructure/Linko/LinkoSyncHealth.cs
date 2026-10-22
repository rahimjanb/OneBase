using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Sales;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Linko;

/// <summary>
/// Контроль обмена с Linko после каждой фоновой синхронизации: если заказы, возвраты, визиты, остатки или перемещения не обновлялись
/// дольше Sales:Sync:StaleAlertMinutes (не меньше трёх интервалов синхронизации) — уведомление (колокольчик и push) тем, кто управляет
/// интеграциями; когда обмен восстановился — второе уведомление. По одному на проблему: открытая проблема хранится в строке «alert».
/// Синхронизация сама повторяется каждые Sales:Sync:IntervalMinutes — уведомление нужно, только если она не справляется.
/// </summary>
public sealed class LinkoSyncHealth(OneBaseDbContext db, SalesOptions options, TimeProvider time, ILogger<LinkoSyncHealth> logger)
{
    public TimeSpan StaleAfter => TimeSpan.FromMinutes(Math.Max(options.Sync.StaleAlertMinutes, 3 * Math.Max(1, options.Sync.IntervalMinutes)));

    /// <summary>Проверяет обмен; true — отправлено уведомление (о проблеме или о восстановлении).</summary>
    public async Task<bool> CheckAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var states = await db.LinkoSyncStates.ToListAsync(ct);
        var problem = LinkoSyncSchedule.Problem(states, now, StaleAfter);
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
                $"{problem} OneBase повторяет синхронизацию каждые {options.Sync.IntervalMinutes} мин. Проверьте токен и доступность Linko в «Настройки → Интеграции».")
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
}

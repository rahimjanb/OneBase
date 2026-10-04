using OneBase.Application.Abstractions;
using OneBase.Domain.Work;

namespace OneBase.Application.Work;

/// <summary>
/// Уведомления пользователей OneBase (колокольчик в шапке). Добавляет записи в контекст — сохраняет вызывающий сервис
/// вместе со своими изменениями; push на телефон отправляется после сохранения (перехватчик сохранения в инфраструктуре).
/// </summary>
public sealed class UserNotifier(IAppDbContext db)
{
    public void Notify(Guid? recipientId, UserNotificationKind kind, string title, string? body = null, string? link = null)
    {
        if (recipientId is null)
        {
            return;
        }

        db.UserNotifications.Add(new UserNotification
        {
            RecipientId = recipientId.Value,
            Kind = kind,
            Title = Trim(title, 300)!,
            Body = Trim(body, 2000),
            Link = Trim(link, 500),
        });
    }

    private static string? Trim(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..(max - 1)] + "…";
}

using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Work;

/// <summary>
/// Подписка браузера на push-уведомления (Web Push): одна на устройство и сайт. У пользователя их может быть несколько —
/// телефон, компьютер, сайт OneBase и сайт Sales Base. Endpoint уникален: тот же браузер при повторной подписке
/// обновляет запись, а не создаёт новую.
/// </summary>
public class PushSubscription : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Адрес push-сервиса браузера (FCM, Mozilla, Apple).</summary>
    public required string Endpoint { get; set; }

    /// <summary>Открытый ключ браузера (base64url) для шифрования сообщения.</summary>
    public required string P256dh { get; set; }

    /// <summary>Секрет аутентификации браузера (base64url).</summary>
    public required string Auth { get; set; }

    /// <summary>Сайт, с которого подписались (https://sales.1base.uz) — уведомление открывает ссылку на нём.</summary>
    public string? Origin { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }

    /// <summary>Неудачи подряд; подписки, которые push-сервис объявил недействительными (404/410), удаляются сразу.</summary>
    public int Failures { get; set; }
}

/// <summary>
/// Ключи VAPID для Web Push — одна строка, создаются при первом обращении. Закрытый ключ хранится зашифрованным
/// (ASP.NET Data Protection, ключи которой лежат в этой же базе), поэтому .env на сервере менять не нужно.
/// </summary>
public class PushKeys
{
    public int Id { get; set; } = 1;
    public required string PublicKey { get; set; }
    public required string ProtectedPrivateKey { get; set; }
    public required string Subject { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

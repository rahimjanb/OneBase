using OneBase.Domain.Common;

namespace OneBase.Domain.Files;

/// <summary>
/// Подключение папки отдела к Windows (WebDAV по HTTPS): один логин на отдел. Пароль хранится дважды — хэшем для проверки входа и
/// зашифрованным Data Protection (<see cref="ProtectedPassword"/>), чтобы его можно было показать сотрудникам отдела ещё раз.
/// Открытого пароля в базе нет. Секретная ссылка (адрес без пароля /k/{код}/{отдел}) — так же: хэш для входа и шифр для «Показать».
/// </summary>
public class FileConnection : Entity
{
    public Guid DepartmentId { get; set; }

    /// <summary>Логин: «production_8f3a1».</summary>
    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    public required string ProtectedPassword { get; set; }

    /// <summary>SHA-256 кода секретной ссылки — по нему WebDAV находит подключение без логина и пароля; null — ссылки ещё нет.</summary>
    public string? LinkTokenHash { get; set; }

    /// <summary>Код ссылки, зашифрованный Data Protection, — для «Показать» и «Скопировать».</summary>
    public string? ProtectedLinkToken { get; set; }

    /// <summary>Когда ссылку выпустили последний раз.</summary>
    public DateTimeOffset? LinkChangedAt { get; set; }

    /// <summary>Что даёт подключение в папке своего отдела: чтение или чтение и запись.</summary>
    public AccessLevel Access { get; set; } = AccessLevel.Write;

    public Guid? CreatedById { get; set; }

    /// <summary>Отозвано: вход по этому логину больше не работает. Новое подключение отдела — новая запись.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Когда пароль сменили последний раз (при создании — время создания).</summary>
    public DateTimeOffset PasswordChangedAt { get; set; } = DateTimeOffset.UtcNow;
}

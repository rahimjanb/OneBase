using OneBase.Domain.Common;

namespace OneBase.Domain.Files;

/// <summary>
/// Личная ссылка Windows сотрудника (адрес без пароля /k/{код}): диск со всем, что сотруднику доступно, — папками его отделов и общими
/// папками, которые выдали ему лично или его отделу. Права проверяются по сотруднику при каждом запросе: сменили роль, отключили
/// пользователя или забрали доступ — диск сразу видит меньше или перестаёт открываться. Код хранится как у ссылки отдела: хэш для входа
/// и шифр Data Protection для «Показать».
/// </summary>
public class FileUserLink : Entity
{
    public Guid UserId { get; set; }

    public required string LinkTokenHash { get; set; }

    public required string ProtectedLinkToken { get; set; }

    /// <summary>Действует до (Files:LinkLifetimeDays от выпуска).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Отозвана (новая ссылка или «Отключить»): вход по ней больше не работает. Новая ссылка — новая запись.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}

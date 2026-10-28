using OneBase.Domain.Common;

namespace OneBase.Domain.Files;

/// <summary>
/// Блокировка файла WebDAV (LOCK, RFC 4918): Word, Excel или Проводник открыли файл на запись. Пока она действует, менять файл может
/// только тот, кто предъявит токен (заголовок If) — второй редактор получает 423 и открывает файл только для чтения, сайт тоже не
/// перезапишет его. Истёкшая блокировка не действует (Office продлевает свою, пока документ открыт).
/// </summary>
public class FileLock : Entity
{
    public Guid FileItemId { get; set; }

    /// <summary>«opaquelocktoken:…» — выдаётся в Lock-Token.</summary>
    public required string Token { get; set; }

    /// <summary>Кто заблокировал: сотрудник (вход по личной ссылке) или подключение отдела.</summary>
    public Guid? OwnerUserId { get; set; }

    public Guid? OwnerConnectionId { get; set; }

    /// <summary>Владелец из запроса LOCK (Office пишет имя пользователя Windows) — для «Файл открыт: …».</summary>
    public string? Owner { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

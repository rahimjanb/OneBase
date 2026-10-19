using OneBase.Domain.Common;

namespace OneBase.Domain.Identity;

/// <summary>
/// Сеанс входа: действует, пока пользователь сам не выйдет. Токен входа несёт его Id (claim «sid») и живёт долго, а сервер проверяет
/// сеанс на каждом запросе — выход или смена пароля администратором закрывают доступ сразу, отключение пользователя — тоже (UserAccessCache).
/// </summary>
public class UserSession : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Браузер или приложение, из которого вошли.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Адрес, с которого вошли.</summary>
    public string? Ip { get; set; }

    /// <summary>Когда сеанс закрыт (выход, смена пароля); null — действует.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}

namespace OneBase.Application.Abstractions;

/// <summary>
/// Общий кэш строковых значений с временем жизни: Redis (переживает перезапуск API), без Redis — память процесса.
/// Ошибки кэша не роняют запрос: промах — просто null. Ключ должен содержать всё, от чего зависит значение (пользователь, права, версия данных).
/// </summary>
public interface ISharedCache
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);
}

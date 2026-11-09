using Microsoft.Extensions.DependencyInjection;

namespace OneBase.Application.Recovery;

/// <summary>Фоновая служба, не запущенная в режиме изоляции: имя типа (для тестов и журнала) и что она делает.</summary>
public sealed record IsolatedWorker(string Type, string Label);

/// <summary>
/// Режим изоляции восстановленной копии: Recovery:Isolated (в окружении — Recovery__Isolated=true). Копия для проверки работает на
/// настоящих данных, но наружу не выходит: не запускаются фоновые службы, которые пишут людям или ходят во внешние системы (Telegram,
/// Linko, Google, Verifix, push, напоминания, AI по расписанию), все внешние HTTP-запросы отклоняются, Telegram не подключается, платные
/// вызовы моделей заблокированы. Иначе копия отправила бы уведомления настоящим сотрудникам и разорвала бы рабочую сессию Telegram.
/// Один экземпляр на процесс: его создаёт первая регистрация (AddInfrastructure или AddAi), остальные берут тот же.
/// </summary>
public sealed class RecoveryIsolation(bool isolated)
{
    public const string ConfigKey = "Recovery:Isolated";

    /// <summary>Текст плашки в интерфейсе.</summary>
    public const string Banner = "Копия для проверки: внешние интеграции выключены";

    private readonly List<IsolatedWorker> _disabled = [];

    public bool Isolated { get; } = isolated;

    public IReadOnlyList<IsolatedWorker> DisabledWorkers
    {
        get
        {
            lock (_disabled)
            {
                return _disabled.ToArray();
            }
        }
    }

    /// <summary>
    /// Можно ли зарегистрировать фоновую службу, выходящую наружу: вне изоляции — да; в изоляции — нет, и служба попадает в список
    /// выключенных (его показывает проверка восстановления).
    /// </summary>
    public bool AllowOutbound<TWorker>(string label)
    {
        if (!Isolated)
        {
            return true;
        }

        lock (_disabled)
        {
            if (_disabled.All(w => w.Type != typeof(TWorker).Name))
            {
                _disabled.Add(new IsolatedWorker(typeof(TWorker).Name, label));
            }
        }

        return false;
    }

    /// <summary>
    /// Значение настройки. Пусто, false, 0, no, off — изоляции нет; true, 1, yes, on — есть. Любое другое значение (опечатка) — тоже
    /// изоляция: лишний раз выключить интеграции безопаснее, чем написать настоящим людям из копии.
    /// </summary>
    public static bool Parse(string? value)
    {
        var text = value?.Trim().ToLowerInvariant();
        return text switch
        {
            null or "" or "false" or "0" or "no" or "off" => false,
            _ => true,
        };
    }

    /// <summary>Экземпляр процесса: уже зарегистрированный в services или новый по значению настройки (и тогда регистрируется).</summary>
    public static RecoveryIsolation Register(IServiceCollection services, string? configValue)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(RecoveryIsolation))?.ImplementationInstance is RecoveryIsolation existing)
        {
            return existing;
        }

        var isolation = new RecoveryIsolation(Parse(configValue));
        services.AddSingleton(isolation);
        return isolation;
    }
}

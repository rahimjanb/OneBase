namespace OneBase.Finance.Integration.Worker;

/// <summary>
/// Подключение к 1С (раздел OneC; переменные окружения OneC__*). Пароль и токен — только здесь, у службы: в OneBase они не передаются
/// и в журнал не пишутся.
/// </summary>
public sealed class OneCOptions
{
    public const string Section = "OneC";

    /// <summary>Синхронизация включена. Выключено — служба на связи с OneBase и проверяет подключение, но данные не загружает.</summary>
    public bool Enabled { get; set; }

    /// <summary>http — настоящая 1С (API VENKON), mock — тестовый источник с демо-данными.</summary>
    public string Provider { get; set; } = "http";

    /// <summary>Адрес HTTP-сервиса 1С, например https://192.168.9.141/buh/hs/onebase/v1 (точный адрес — от VENKON).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Токен вместо логина и пароля (Authorization: Bearer), если VENKON выберет этот вариант.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// API Secret — дополнительный ключ HTTP-сервиса 1С (заголовок X-OneBase-Secret), вместе с логином и паролем пользователя 1С или без них
    /// (расширение «OneBaseExchange» хранит только его SHA-256). Как и пароль — только у службы, в журнал не пишется.
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Файл сертификата (PEM) внутреннего центра сертификации или самоподписанного сертификата 1С. Проверка сертификата не отключается:
    /// цепочка строится до этого корня, имя сервера проверяется.
    /// </summary>
    public string CaCertificatePath { get; set; } = string.Empty;

    /// <summary>Тот же сертификат текстом PEM — так его передаёт страница «1С» в OneBase (вместо файла).</summary>
    public string CaCertificatePem { get; set; } = string.Empty;

    /// <summary>Разрешить http (только для проверки на этом компьютере). В production служба http не принимает.</summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>Пути методов API 1С, если VENKON назовёт их иначе (ключ — метод спецификации: ping, organizations, documents…).</summary>
    public Dictionary<string, string> Paths { get; set; } = [];

    /// <summary>Тестовый источник в production запрещён (демо-данные не должны попасть в рабочую базу) — кроме явного разрешения.</summary>
    public bool AllowMockInProduction { get; set; }

    public MockOptions Mock { get; set; } = new();
}

/// <summary>Тестовый источник: объём демо-данных.</summary>
public sealed class MockOptions
{
    /// <summary>С какой даты есть демо-данные (по умолчанию — 1 января прошлого года).</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Множитель числа документов в день (1 — около 15 документов на организацию).</summary>
    public double Volume { get; set; } = 1;

    public int Seed { get; set; } = 20261117;

    /// <summary>
    /// Как тестовый источник объявляет отслеживание изменений (v1.1): changes — план обмена (/changes, по умолчанию, как раньше),
    /// versions — только чтением через /versions и ids= (как расширение 1С «OneBaseExchange»).
    /// </summary>
    public string ChangeTracking { get; set; } = "changes";
}

/// <summary>Внутренний API OneBase (раздел OneBase; OneBase__InternalApiUrl, OneBase__IntegrationToken).</summary>
public sealed class OneBaseApiOptions
{
    public const string Section = "OneBase";

    /// <summary>Адрес API OneBase внутри сети docker (сервис api, порт 8080), без nginx.</summary>
    public string InternalApiUrl { get; set; } = "http://api:8080";

    /// <summary>Ключ службы: создаётся в «Настройки → Интеграции → Финансы → 1С».</summary>
    public string IntegrationToken { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 180;
}

/// <summary>Расписание и пакеты (раздел Integration). Интервал и размер пакета администратор меняет в OneBase — здесь значения по умолчанию.</summary>
public sealed class IntegrationOptions
{
    public const string Section = "Integration";

    public int IntervalMinutes { get; set; } = 30;
    public int BatchSize { get; set; } = 500;

    /// <summary>Как часто служба выходит на связь с OneBase (команды администратора, пауза).</summary>
    public int HeartbeatSeconds { get; set; } = 15;

    /// <summary>Как часто служба сама проверяет подключение к 1С.</summary>
    public int ConnectionCheckMinutes { get; set; } = 10;

    /// <summary>Историческая загрузка идёт порциями: после этого времени запуск закрывается, следующий продолжает с того же места.</summary>
    public int HistoricalBudgetMinutes { get; set; } = 15;

    /// <summary>Попыток на один запрос (1С или OneBase) при сбоях сети и временных ошибках.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Начальная пауза между попытками; дальше — вдвое больше, до MaxDelaySeconds.</summary>
    public double BaseDelaySeconds { get; set; } = 2;

    public double MaxDelaySeconds { get; set; } = 60;

    /// <summary>Остатки пересчитываются за месяцы, где были изменения, но не дальше этого числа месяцев назад.</summary>
    public int BalancesMonthsBack { get; set; } = 24;

    /// <summary>
    /// Как узнавать об изменениях в 1С: auto — как объявила 1С в /ping (changeTracking), versions — сверкой версий только чтением
    /// (/versions + ids=, в 1С ничего не пишется), changes — по плану обмена (/changes).
    /// </summary>
    public string ChangeTracking { get; set; } = "auto";

    /// <summary>Сверка версий при каждой синхронизации: текущий месяц и столько прошлых (документы, перепроведённые задним числом).</summary>
    public int ReconcileMonthsBack { get; set; } = 2;

    /// <summary>С этого часа (по Ташкенту) раз в сутки сверяются все месяцы, за которые в OneBase есть документы.</summary>
    public int FullReconcileHour { get; set; } = 2;

    public string WorkerId { get; set; } = Environment.MachineName;
}

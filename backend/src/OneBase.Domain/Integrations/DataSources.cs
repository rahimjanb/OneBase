using OneBase.Domain.Common;

namespace OneBase.Domain.Integrations;

/// <summary>Как OneBase читает таблицу Google: по ссылке (доступ «все, у кого есть ссылка») или сервисным аккаунтом Google.</summary>
public enum SheetAccess
{
    Link,
    ServiceAccount,
}

/// <summary>Источник данных «Google Sheets»: таблица (spreadsheet) и доступ к ней. Ключ сервисного аккаунта хранится зашифрованным.</summary>
public class DataSourceConnection : Entity
{
    public required string Name { get; set; }

    /// <summary>ID таблицы из ссылки docs.google.com/spreadsheets/d/{id}.</summary>
    public required string SpreadsheetId { get; set; }

    public required string Url { get; set; }

    public SheetAccess Access { get; set; }

    /// <summary>JSON-ключ сервисного аккаунта, зашифрованный ASP.NET Data Protection; в браузер не отдаётся.</summary>
    public string? ProtectedKey { get; set; }

    /// <summary>Почта сервисного аккаунта — с ней нужно поделиться таблицей.</summary>
    public string? ServiceAccountEmail { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }
    public bool? LastCheckOk { get; set; }
    public string? LastCheckMessage { get; set; }

    /// <summary>Листы таблицы на момент последней проверки (jsonb: имя, строк, столбцов).</summary>
    public string? Sheets { get; set; }

    public Guid? UpdatedById { get; set; }
}

/// <summary>
/// Привязка раздела OneBase к листу Google Sheets: какие поля раздела обновлять (jsonb — поле и сопоставление столбцов), расписание
/// синхронизации и её последний итог. Обновляются только выбранные поля.
/// </summary>
public class DataSourceBinding : Entity
{
    /// <summary>Раздел продаж: secondary, primary, plans, pnl… (DataSourceCatalog).</summary>
    public required string Section { get; set; }

    public Guid ConnectionId { get; set; }
    public DataSourceConnection? Connection { get; set; }

    /// <summary>
    /// Листы таблицы (jsonb, массив названий) с одинаковой разметкой: строки всех листов собираются вместе. ["*"] — все листы
    /// таблицы, и новые тоже (лист на месяц: «Январь», «Февраль»…).
    /// </summary>
    public required string Sheets { get; set; }

    /// <summary>Строка заголовков (с 1).</summary>
    public int HeaderRow { get; set; } = 1;

    /// <summary>Поля и сопоставление столбцов (jsonb, DataSourceFieldBinding[]).</summary>
    public required string Fields { get; set; }

    /// <summary>manual — только кнопкой, interval — каждые IntervalMinutes, daily — каждый день в DailyAt (Ташкент).</summary>
    public string Schedule { get; set; } = "manual";

    public int? IntervalMinutes { get; set; }

    /// <summary>Время ежедневной синхронизации «ЧЧ:ММ» по Ташкенту.</summary>
    public string? DailyAt { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Кто настроил привязку — от его имени изменения записываются в журнал AI Data Manager.</summary>
    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>ok, unchanged, conflicts, error.</summary>
    public string? LastStatus { get; set; }
    public string? LastMessage { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }

    public List<DataSourceSyncRun> Runs { get; set; } = [];
}

/// <summary>Одна синхронизация привязки: когда, чем запущена, итог по каждому полю (jsonb).</summary>
public class DataSourceSyncRun : Entity
{
    public Guid BindingId { get; set; }
    public DataSourceBinding? Binding { get; set; }

    /// <summary>manual — кнопкой, schedule — по расписанию.</summary>
    public required string Trigger { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>ok, unchanged, conflicts, error.</summary>
    public required string Status { get; set; }

    public string? Message { get; set; }

    /// <summary>Итог по полям (jsonb, DataSourceFieldResult[]): операция AI Data Manager, сколько строк записано, ошибки.</summary>
    public string? Details { get; set; }

    public Guid? UserId { get; set; }
}

/// <summary>
/// Конфликт источников данных: строка таблицы, которую нельзя записать (регион не найден, не число, разные значения), или попытка
/// изменить поле, которым источник не владеет (ручная правка поля, которое ведёт Google Sheets). Подтверждённое значение остаётся,
/// конфликт виден в «Источниках данных». Строка таблицы — открыт, пока её не исправят (закрывается следующей синхронизацией);
/// отказ в записи чужого поля — событие, закрыт сразу.
/// </summary>
public class DataSourceConflict : Entity
{
    /// <summary>Кто пытался записать: google_sheets, data_manager (чат и файлы AI Data Manager), manual (настройки продаж).</summary>
    public required string Source { get; set; }

    /// <summary>Привязка Google Sheets (у строк таблицы); при удалении привязки — null, конфликт закрывается.</summary>
    public Guid? BindingId { get; set; }

    /// <summary>Поле OneBase (DataSourceCatalog): plan_rop, dealer, pnl…</summary>
    public required string Field { get; set; }

    /// <summary>Что затронуто: «Хоразм · Кекс · сентябрь 2026», регион, дилер.</summary>
    public string? Entity { get; set; }

    /// <summary>Период: «2026-10» или диапазон.</summary>
    public string? Period { get; set; }

    /// <summary>row — строка таблицы не записана; owner — запись в поле, которым источник не владеет.</summary>
    public required string Kind { get; set; }

    public required string Message { get; set; }

    /// <summary>Ячейка таблицы («План!B5»), если есть.</summary>
    public string? SourceRef { get; set; }

    /// <summary>Правило владения на момент конфликта: «поле ведёт Google Sheets «План»».</summary>
    public string? Rule { get; set; }

    /// <summary>Что решено: «не записано», «отклонено», «исправлено в таблице»…</summary>
    public required string Decision { get; set; }

    /// <summary>Нужно ли вмешательство: исправить таблицу или сопоставление.</summary>
    public bool NeedsAction { get; set; }

    /// <summary>Отпечаток (привязка, поле, вид, сущность, текст): повтор того же конфликта не плодит записи.</summary>
    public required string Fingerprint { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
    public int Occurrences { get; set; } = 1;

    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedById { get; set; }

    /// <summary>Кто пытался записать (ручная правка, чат) или от чьего имени шла синхронизация.</summary>
    public Guid? UserId { get; set; }
}

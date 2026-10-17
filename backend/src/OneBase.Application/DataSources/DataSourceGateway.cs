using OneBase.Application.Abstractions;

namespace OneBase.Application.DataSources;

/// <summary>Поле, которое ведёт привязка, и какие месяцы она берёт.</summary>
public sealed record SheetFieldInfo(string Field, string Label, string? Period);

/// <summary>Привязка раздела к листам таблицы: какие поля ведёт, листы, расписание, последняя синхронизация.</summary>
public sealed record SheetBindingInfo(
    Guid Id,
    string Section,
    string SectionName,
    IReadOnlyList<string> Sheets,
    IReadOnlyList<SheetFieldInfo> Fields,
    string Schedule,
    bool Enabled,
    DateTimeOffset? LastRunAt,
    string? LastStatus,
    string? LastMessage);

/// <summary>Подключённая таблица Google Sheets: листы (на момент последней проверки) и привязки разделов.</summary>
public sealed record SheetConnectionInfo(Guid Id, string Name, bool? Readable, IReadOnlyList<SheetInfo> Sheets, IReadOnlyList<SheetBindingInfo> Bindings);

/// <summary>Итог синхронизации привязки — по полям.</summary>
public sealed record SheetSyncResult(string Status, string? Message, IReadOnlyList<DataSourceFieldResult> Fields);

/// <summary>
/// «Источники данных» для AI Data Manager: подключённые таблицы Google Sheets, свежий лист и синхронизация привязки. Реализация —
/// Infrastructure (DataSourceService). Ошибки — DataManagerException с текстом для администратора.
/// </summary>
public interface IDataSourceGateway
{
    Task<IReadOnlyList<SheetConnectionInfo>> ConnectionsAsync(CancellationToken ct = default);

    /// <summary>Лист таблицы, прочитанный сейчас (sheet null — первый лист).</summary>
    Task<(string Connection, ReportSheet Sheet)> ReadSheetAsync(Guid connectionId, string? sheet, CancellationToken ct = default);

    /// <summary>Синхронизировать привязку сейчас — от имени userId (нужно право «AI Data Manager»), как кнопка «Синхронизировать сейчас».</summary>
    Task<SheetSyncResult> SyncAsync(Guid bindingId, Guid userId, CancellationToken ct = default);
}

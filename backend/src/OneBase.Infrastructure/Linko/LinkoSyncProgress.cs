namespace OneBase.Infrastructure.Linko;

public enum LinkoSyncMode
{
    /// <summary>Обычное обновление: изменения по last_tm, визиты — за последние дни.</summary>
    Incremental,

    /// <summary>Полная перезагрузка окна истории без удаления данных.</summary>
    Full,

    /// <summary>Удалить всё загруженное из Linko и загрузить заново.</summary>
    Reset,
}

public sealed record LinkoSyncProgressInfo(string Mode, string Phase, string? Entity, int Rows, DateTimeOffset StartedAt);

/// <summary>Текущий этап синхронизации — для индикатора в интерфейсе.</summary>
public sealed class LinkoSyncProgress
{
    private LinkoSyncProgressInfo? _current;

    public LinkoSyncProgressInfo? Current => _current;

    public void Start(LinkoSyncMode mode) =>
        _current = new LinkoSyncProgressInfo(mode.ToString(), "Подготовка", null, 0, DateTimeOffset.UtcNow);

    public void Step(string phase, string? entity) =>
        _current = _current is null ? null : _current with { Phase = phase, Entity = entity, Rows = 0 };

    public void AddRows(int rows) =>
        _current = _current is null ? null : _current with { Rows = _current.Rows + rows };

    public void Finish() => _current = null;
}

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

/// <summary>
/// Состояние синхронизации для интерфейса. Percent — оценка выполненного (0–100), RemainingSeconds — сколько примерно
/// осталось (null — пока рано оценивать), StepsDone/StepsTotal — шаги плана.
/// </summary>
public sealed record LinkoSyncProgressInfo(
    string Mode,
    string Phase,
    string? Entity,
    int Rows,
    DateTimeOffset StartedAt,
    int Percent = 0,
    int StepsDone = 0,
    int StepsTotal = 0,
    long? Expected = null,
    int? RemainingSeconds = null);

/// <summary>Шаг плана синхронизации: entity и phase — как в Step(); Weight — примерная доля времени шага.</summary>
public sealed record LinkoSyncPlanStep(string? Entity, string Phase, int Weight);

/// <summary>
/// Текущий этап синхронизации — для индикатора в интерфейсе. Процент считается по плану шагов с весами (история заказов
/// и визитов весит больше справочников), внутри шага — по строкам: сколько загружено из ожидаемых (*_count Linko).
/// </summary>
public sealed class LinkoSyncProgress(TimeProvider time)
{
    private readonly object _lock = new();
    private LinkoSyncProgressInfo? _current;
    private IReadOnlyList<LinkoSyncPlanStep> _plan = [];
    private int _done = -1;
    private long _expected;

    /// <summary>Идёт синхронизация (дёшево, без расчёта процента).</summary>
    public bool IsActive => Volatile.Read(ref _current) is not null;

    public LinkoSyncProgressInfo? Current
    {
        get
        {
            lock (_lock)
            {
                return _current is null ? null : Estimate(_current);
            }
        }
    }

    public void Start(LinkoSyncMode mode)
    {
        lock (_lock)
        {
            _plan = [];
            _done = -1;
            _expected = 0;
            _current = new LinkoSyncProgressInfo(mode.ToString(), "Подготовка", null, 0, time.GetUtcNow());
        }
    }

    /// <summary>План шагов — когда стало известно, будет ли история и планы агентов.</summary>
    public void Plan(IReadOnlyList<LinkoSyncPlanStep> steps)
    {
        lock (_lock)
        {
            _plan = steps;
            _done = -1;
        }
    }

    /// <summary>Начало шага: предыдущие шаги плана считаются пройденными (в том числе завершившиеся ошибкой).</summary>
    public void Step(string phase, string? entity)
    {
        lock (_lock)
        {
            if (_current is null)
            {
                return;
            }

            var index = IndexOf(entity, phase);
            if (index >= 0)
            {
                _done = index;
            }

            _expected = 0;
            _current = _current with { Phase = phase, Entity = entity, Rows = 0 };
        }
    }

    /// <summary>Сколько строк ожидается в шаге (*_count Linko); шаг из нескольких запросов складывает их.</summary>
    public void Expect(long rows)
    {
        lock (_lock)
        {
            if (_current is not null && rows > 0)
            {
                _expected += rows;
            }
        }
    }

    public void AddRows(int rows)
    {
        lock (_lock)
        {
            if (_current is not null)
            {
                _current = _current with { Rows = _current.Rows + rows };
            }
        }
    }

    public void Finish()
    {
        lock (_lock)
        {
            _current = null;
            _plan = [];
        }
    }

    private int IndexOf(string? entity, string phase)
    {
        for (var i = 0; i < _plan.Count; i++)
        {
            if (_plan[i].Entity == entity && _plan[i].Phase == phase)
            {
                return i;
            }
        }

        return -1;
    }

    private LinkoSyncProgressInfo Estimate(LinkoSyncProgressInfo info)
    {
        var total = _plan.Sum(s => s.Weight);
        if (total == 0 || _done < 0)
        {
            return info with { StepsTotal = _plan.Count, Expected = _expected > 0 ? _expected : null };
        }

        // Пройдено: шаги до текущего целиком, текущий — по доле загруженных строк (без счётчика — 0).
        var before = _plan.Take(_done).Sum(s => s.Weight);
        var fraction = _expected > 0 ? Math.Min(1.0, info.Rows / (double)_expected) : 0;
        var share = (before + _plan[_done].Weight * fraction) / total;
        var percent = (int)Math.Clamp(Math.Floor(share * 100), 0, 99);

        // Сколько осталось — по скорости с начала синхронизации; пока пройдено мало, оценка была бы случайной.
        var elapsed = (time.GetUtcNow() - info.StartedAt).TotalSeconds;
        int? remaining = share >= 0.03 && elapsed >= 10 ? (int)Math.Ceiling(elapsed * (1 - share) / share) : null;

        return info with
        {
            Percent = percent,
            StepsDone = _done,
            StepsTotal = _plan.Count,
            Expected = _expected > 0 ? _expected : null,
            RemainingSeconds = remaining,
        };
    }
}

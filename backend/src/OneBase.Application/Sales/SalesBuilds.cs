using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales;

/// <summary>
/// Сборки месяцев продаж в процессе. Один и тот же отчёт (месяц, вид плана, версия данных) не собирается дважды одновременно —
/// второй запрос ждёт первую сборку; разные месяцы собираются параллельно, но не больше MaxParallel сразу (каждая сборка держит
/// в памяти строки двух месяцев). Счётчики и последние сборки — для замеров; в журнал — только ключ отчёта и длительности этапов.
/// </summary>
public sealed class SalesBuilds(ILogger<SalesBuilds> logger)
{
    public const int MaxParallel = 2;
    private const int RecentLimit = 50;

    private readonly ConcurrentDictionary<string, Task<SalesAnalytics>> _running = new();
    private readonly SemaphoreSlim _slots = new(MaxParallel, MaxParallel);
    private readonly ConcurrentQueue<SalesBuildRecord> _recent = new();
    private long _hits;
    private long _builds;
    private long _joined;

    /// <summary>Отчёт отдан из кэша.</summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>Отчёт собран заново (холодная загрузка).</summary>
    public long Builds => Interlocked.Read(ref _builds);

    /// <summary>Запрос дождался чужой сборки того же отчёта вместо своей.</summary>
    public long Joined => Interlocked.Read(ref _joined);

    /// <summary>Тяжёлые агрегаты давних месяцев (АКБ) считает одна сборка за раз: вторая ждёт и берёт посчитанное из кэша.</summary>
    internal SemaphoreSlim HistoryGate { get; } = new(1, 1);

    /// <summary>Последние сборки — от старых к новым.</summary>
    public IReadOnlyList<SalesBuildRecord> Recent => _recent.ToArray();

    internal void Hit() => Interlocked.Increment(ref _hits);

    /// <summary>
    /// Сборка отчёта <paramref name="key"/> (ключ включает версию данных): идущая — ждём её, иначе собираем сами. Чужая сборка
    /// прервалась (запрос, который её начал, отменили) — пробуем снова, уже своей.
    /// </summary>
    internal async Task<SalesAnalytics> RunAsync(string key, Func<Task<SalesAnalytics>> build, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var mine = new TaskCompletionSource<SalesAnalytics>(TaskCreationOptions.RunContinuationsAsynchronously);
            var running = _running.GetOrAdd(key, mine.Task);
            if (running != mine.Task)
            {
                Interlocked.Increment(ref _joined);
                try
                {
                    return await running.WaitAsync(ct);
                }
                catch (Exception) when (!ct.IsCancellationRequested && attempt < 2)
                {
                    continue;
                }
            }

            try
            {
                await _slots.WaitAsync(ct);
                try
                {
                    Interlocked.Increment(ref _builds);
                    var result = await build();
                    mine.TrySetResult(result);
                    return result;
                }
                finally
                {
                    _slots.Release();
                }
            }
            catch (OperationCanceledException)
            {
                mine.TrySetCanceled();
                throw;
            }
            catch (Exception ex)
            {
                mine.TrySetException(ex);
                _ = mine.Task.Exception; // ждавших может не быть — исключение не должно остаться ненаблюдённым
                throw;
            }
            finally
            {
                _running.TryRemove(new KeyValuePair<string, Task<SalesAnalytics>>(key, mine.Task));
            }
        }
    }

    internal void Record(string key, SalesBuildTimer timer)
    {
        var record = new SalesBuildRecord(key, DateTimeOffset.UtcNow, timer.ElapsedMs, timer.Stages);
        _recent.Enqueue(record);
        while (_recent.Count > RecentLimit && _recent.TryDequeue(out _))
        {
        }

        logger.LogInformation("Продажи: отчёт {Key} собран за {Ms} мс ({Stages})", key, record.Ms, timer);
    }
}

/// <summary>Холодная сборка отчёта продаж: ключ, когда, сколько и из чего сложилось время.</summary>
public sealed record SalesBuildRecord(string Key, DateTimeOffset At, long Ms, IReadOnlyList<SalesBuildStage> Stages);

public sealed record SalesBuildStage(string Name, long Ms);

/// <summary>Этапы холодной сборки месяца продаж: чтения из БД и расчёт. Только длительности — без данных.</summary>
public sealed class SalesBuildTimer
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<SalesBuildStage> _stages = [];
    private long _mark;

    public long ElapsedMs => _watch.ElapsedMilliseconds;

    public IReadOnlyList<SalesBuildStage> Stages => _stages;

    public void Lap(string stage)
    {
        var now = _watch.ElapsedMilliseconds;
        _stages.Add(new SalesBuildStage(stage, now - _mark));
        _mark = now;
    }

    public override string ToString() => string.Join(", ", _stages.Where(s => s.Ms > 0).Select(s => $"{s.Name} {s.Ms}"));
}

/// <summary>
/// Дополнительный шаг прогрева отчётов продаж после сброса кэша (SalesCacheWarmer): регистрирует модуль, который знает свои запросы
/// по умолчанию (AI-инструменты — остатки, первичка, продажи по SKU). Выполняется в области прогрева, по одному; ошибка шага — в журнал.
/// </summary>
public interface ISalesWarmStep
{
    string Name { get; }

    Task WarmAsync(CancellationToken ct);
}

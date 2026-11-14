using System.Collections.Concurrent;
using System.Text.Json;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>
/// Кэш срезов одного исследования: одинаковый срез (тот же запрос и та же область прав) выполняется один раз, даже если его одновременно
/// просят несколько операций; остальные получают тот же результат (и тот же id записи журнала). Живёт, пока идёт исследование.
/// </summary>
public sealed class SliceCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Lazy<Task<AnalyticsResult>>> _items = new();
    private int _hits;
    private int _misses;

    public int Hits => _hits;

    public int Misses => _misses;

    public double HitRatio => _hits + _misses == 0 ? 0 : (double)_hits / (_hits + _misses);

    public Task<AnalyticsResult> GetAsync(AnalyticsQuery query, string scope, Func<Task<AnalyticsResult>> run)
    {
        var key = scope + "\u001f" + JsonSerializer.Serialize(query, Json);
        var fresh = new Lazy<Task<AnalyticsResult>>(run, LazyThreadSafetyMode.ExecutionAndPublication);
        // Упавший или отменённый срез не кэшируется: повтор (после исправления или по таймауту операции) выполняется заново.
        if (_items.TryGetValue(key, out var old) && old.IsValueCreated && (old.Value.IsFaulted || old.Value.IsCanceled) && _items.TryUpdate(key, fresh, old))
        {
            Interlocked.Increment(ref _misses);
            return fresh.Value;
        }

        if (_items.TryAdd(key, fresh))
        {
            Interlocked.Increment(ref _misses);
            return fresh.Value;
        }

        Interlocked.Increment(ref _hits);
        return _items[key].Value;
    }
}

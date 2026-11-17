using OneBase.Finance.Integration.Worker.Api;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Integration.Worker.Sync;

/// <summary>
/// Повторы с нарастающей паузой (2, 4, 8… с, не больше MaxDelay) и случайным разбросом ±20 %. Повторяются только временные ошибки (сеть,
/// тайм-аут, 429/5xx, 503 с Retry-After); авторизация, TLS и неверный запрос — сразу ошибка. Пауза Retry-After от сервера уважается
/// (не больше 2 минут). Отмена (CancellationToken) прерывает и паузу.
/// </summary>
public sealed class RetryPolicy(IntegrationOptions options, ILogger<RetryPolicy> logger)
{
    /// <summary>Задержка — подменяется в тестах, чтобы не ждать.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (span, ct) => Task.Delay(span, ct);

    public int MaxAttempts => Math.Max(1, options.MaxAttempts);

    public async Task<T> ExecuteAsync<T>(string what, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action(ct);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                // Остановка службы — это отмена, а не сбой 1С или OneBase.
                ct.ThrowIfCancellationRequested();
                throw;
            }
            catch (Exception e) when (attempt < MaxAttempts && Classify(e) is { Transient: true } c)
            {
                var delay = NextDelay(attempt, c.RetryAfter);
                logger.LogWarning("{What}: попытка {Attempt} из {Max} не удалась ({Error}); повтор через {Delay:N0} с", what, attempt, MaxAttempts,
                    e.Message, delay.TotalSeconds);
                await Delay(delay, ct);
            }
        }
    }

    public Task ExecuteAsync(string what, Func<CancellationToken, Task> action, CancellationToken ct) =>
        ExecuteAsync<bool>(what, async t =>
        {
            await action(t);
            return true;
        }, ct);

    public TimeSpan NextDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } after && after > TimeSpan.Zero)
        {
            return after > TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(2) : after;
        }

        var seconds = Math.Min(options.MaxDelaySeconds, options.BaseDelaySeconds * Math.Pow(2, attempt - 1));
        var jitter = 0.8 + Random.Shared.NextDouble() * 0.4;
        return TimeSpan.FromSeconds(seconds * jitter);
    }

    public static (bool Transient, TimeSpan? RetryAfter) Classify(Exception e) => e switch
    {
        OneCException o => (o.Transient, o.RetryAfter),
        OneBaseApiException a => (a.Transient, a.RetryAfter),
        HttpRequestException => (true, null),
        TaskCanceledException or TimeoutException => (true, null),
        IOException => (true, null),
        _ => (false, null),
    };
}

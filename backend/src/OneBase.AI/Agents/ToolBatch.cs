using System.Runtime.ExceptionServices;

namespace OneBase.AI.Agents;

/// <summary>
/// Параллельный запуск независимых вызовов одного шага сотрудника: не больше maxParallel сразу, результаты — в порядке вызовов. Сбой одного
/// вызова отменяет ещё не законченные (как при вызовах по очереди, где следующие уже не выполнились бы) и пробрасывается именно он, а не
/// вызванная им отмена; отмена всего запроса — OperationCanceledException. Все начатые вызовы дожидаются завершения — ни один не остаётся
/// работать после выхода (у каждого своя область DI и свой DbContext, которые он закрывает сам).
/// </summary>
internal static class ToolBatch
{
    public static async Task<T[]> RunAsync<T>(int count, int maxParallel, Func<int, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        var results = new T[count];
        if (count == 0)
        {
            return results;
        }

        using var failed = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var slots = new SemaphoreSlim(Math.Max(1, maxParallel));
        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            await slots.WaitAsync(failed.Token);
            try
            {
                results[i] = await work(i, failed.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await failed.CancelAsync();
                throw;
            }
            finally
            {
                slots.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Первая по порядку настоящая ошибка, а не отмена, которую она вызвала у соседей.
            var error = tasks.Select(t => t.Exception?.InnerException).FirstOrDefault(e => e is not null and not OperationCanceledException);
            if (error is not null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }

            ct.ThrowIfCancellationRequested();
            throw;
        }

        return results;
    }
}

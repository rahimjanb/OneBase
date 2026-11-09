using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment.Jobs;

/// <summary>
/// Очереди фоновых заданий подбора. Входящие и исходящие Telegram (hr.telegram.incoming, hr.telegram.outgoing) — уже существующие очереди:
/// разбор входящих по чатам (TelegramAgentWorker, по порядку в каждом чате) и очередь исходящих в базе (telegram.OutgoingMessages) — здесь их нет.
/// </summary>
public static class RecruitmentQueues
{
    public const string Download = "hr.resume.download";
    public const string Ocr = "hr.resume.ocr";
    public const string Analysis = "hr.resume.analysis";
    public const string Scoring = "hr.candidate.scoring";
    public const string Scheduling = "hr.interview.scheduling";
    public const string Notifications = "hr.interview.notifications";

    public static readonly string[] All = [Download, Ocr, Analysis, Scoring, Scheduling, Notifications];

    /// <summary>Сколько заданий очереди выполняется одновременно: OCR и анализ — платные вызовы AI и память, поэтому по 2.</summary>
    public static int Concurrency(string queue) => queue switch
    {
        Download => 3,
        Notifications => 3,
        Scheduling => 1,
        _ => 2,
    };

    /// <summary>Аренда: столько задание может идти; дольше — сервер, видимо, упал, задание берётся снова.</summary>
    public static TimeSpan Lease(string queue) => queue is Ocr or Analysis or Scoring ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(3);
}

/// <summary>Задание не выполнить и повторять бессмысленно (файл удалён в Telegram, документ повреждён) — сразу Failed.</summary>
public sealed class RecruitmentJobFailure(string message) : Exception(message);

/// <summary>Обработчик очереди. Исключение — повтор с паузой (до MaxAttempts); RecruitmentJobFailure — сразу ошибка; OnFailedAsync — после последней.</summary>
public interface IRecruitmentJobHandler
{
    string Queue { get; }

    Task RunAsync(RecruitmentJob job, CancellationToken ct);

    Task OnFailedAsync(RecruitmentJob job, string error, CancellationToken ct);
}

/// <summary>Сверка раз в минуту (фоновая служба): найти застрявшее и поставить задания снова; вернуть, сколько поставлено.</summary>
public interface IRecruitmentSweeper
{
    Task<int> SweepAsync(CancellationToken ct);
}

/// <summary>
/// Очередь заданий в PostgreSQL: поставить (тот же ключ, пока задание не выполнено, второй раз не ставится), захватить (условный UPDATE с
/// арендой — одно задание не возьмут двое; упавшее посреди работы берётся снова после аренды), завершить, повторить с паузой или провалить.
/// </summary>
public sealed class RecruitmentJobQueue(IAppDbContext db, TimeProvider clock)
{
    /// <summary>Паузы перед повторами: 15 с, 2 мин, 10 мин.</summary>
    public static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];

    public async Task<bool> EnqueueAsync(string queue, string key, Guid relatedId, string? payload = null, TimeSpan? delay = null, int maxAttempts = 3,
        CancellationToken ct = default)
    {
        if (await db.RecruitmentJobs.AnyAsync(j => j.Queue == queue && j.Key == key && (j.Status == RecruitmentJobStatus.Pending || j.Status == RecruitmentJobStatus.Running), ct))
        {
            return false;
        }

        var now = clock.GetUtcNow();
        var job = new RecruitmentJob
        {
            Queue = queue,
            Key = key.Length <= 120 ? key : key[..120],
            RelatedId = relatedId,
            Payload = payload,
            MaxAttempts = Math.Clamp(maxAttempts, 1, 10),
            RunAfter = now + (delay ?? TimeSpan.Zero),
            CreatedAt = now,
        };
        db.RecruitmentJobs.Add(job);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.RecruitmentJobs.Remove(job); // поставили одновременно — уже в очереди
            return false;
        }
    }

    /// <summary>Захватить до max заданий очереди: ждущие, чьё время пришло, и «зависшие» (аренда истекла — сервер упал посреди работы).</summary>
    public async Task<List<RecruitmentJob>> ClaimAsync(string queue, int max, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ids = await db.RecruitmentJobs.AsNoTracking()
            .Where(j => j.Queue == queue && ((j.Status == RecruitmentJobStatus.Pending && j.RunAfter <= now) || (j.Status == RecruitmentJobStatus.Running && j.LockedUntil < now)))
            .OrderBy(j => j.RunAfter).ThenBy(j => j.Id).Select(j => j.Id).Take(max * 2).ToListAsync(ct);
        var lease = now + RecruitmentQueues.Lease(queue);
        var claimed = new List<RecruitmentJob>();
        foreach (var id in ids)
        {
            if (claimed.Count >= max)
            {
                break;
            }

            var taken = await db.RecruitmentJobs
                .Where(j => j.Id == id && ((j.Status == RecruitmentJobStatus.Pending && j.RunAfter <= now) || (j.Status == RecruitmentJobStatus.Running && j.LockedUntil < now)))
                .ExecuteUpdateAsync(u => u.SetProperty(j => j.Status, RecruitmentJobStatus.Running).SetProperty(j => j.LockedUntil, lease)
                    .SetProperty(j => j.Attempts, j => j.Attempts + 1), ct);
            if (taken == 1)
            {
                claimed.Add(await db.RecruitmentJobs.AsNoTracking().FirstAsync(j => j.Id == id, ct));
            }
        }

        return claimed;
    }

    public Task CompleteAsync(long id, CancellationToken ct) =>
        db.RecruitmentJobs.Where(j => j.Id == id).ExecuteUpdateAsync(u => u.SetProperty(j => j.Status, RecruitmentJobStatus.Done)
            .SetProperty(j => j.FinishedAt, clock.GetUtcNow()).SetProperty(j => j.LockedUntil, (DateTimeOffset?)null), ct);

    /// <summary>Ошибка: попытки остались и ошибка не окончательная — повтор после паузы (true); иначе — Failed (false).</summary>
    public async Task<bool> RetryOrFailAsync(RecruitmentJob job, string error, bool permanent, CancellationToken ct)
    {
        var text = error.Length <= 1000 ? error : error[..1000];
        var now = clock.GetUtcNow();
        if (!permanent && job.Attempts < job.MaxAttempts)
        {
            var pause = Backoff[Math.Min(job.Attempts - 1, Backoff.Length - 1)];
            await db.RecruitmentJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(u => u.SetProperty(j => j.Status, RecruitmentJobStatus.Pending)
                .SetProperty(j => j.RunAfter, now + pause).SetProperty(j => j.LastError, text).SetProperty(j => j.LockedUntil, (DateTimeOffset?)null), ct);
            return true;
        }

        await db.RecruitmentJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(u => u.SetProperty(j => j.Status, RecruitmentJobStatus.Failed)
            .SetProperty(j => j.LastError, text).SetProperty(j => j.FinishedAt, now).SetProperty(j => j.LockedUntil, (DateTimeOffset?)null), ct);
        return false;
    }

    /// <summary>Выполненные и проваленные задания старше 30 дней — удалить (история — в аудите и у документов).</summary>
    public Task<int> CleanupAsync(CancellationToken ct)
    {
        var before = clock.GetUtcNow().AddDays(-30);
        return db.RecruitmentJobs.Where(j => (j.Status == RecruitmentJobStatus.Done || j.Status == RecruitmentJobStatus.Failed) && j.FinishedAt < before).ExecuteDeleteAsync(ct);
    }
}

/// <summary>
/// Выполнение заданий: каждое — в своей области DI (свой контекст базы), ошибка — повтор или провал, после провала — OnFailedAsync обработчика.
/// Фоновая служба зовёт RunAsync с ограничением одновременных заданий очереди; тесты — DrainAsync (по одному, пока очередь не пуста).
/// </summary>
public sealed class RecruitmentJobRunner(IServiceScopeFactory scopes, ILogger<RecruitmentJobRunner> logger)
{
    public async Task RunAsync(RecruitmentJob job, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<RecruitmentJobQueue>();
        var handler = scope.ServiceProvider.GetServices<IRecruitmentJobHandler>().FirstOrDefault(h => h.Queue == job.Queue);
        if (handler is null)
        {
            await queue.RetryOrFailAsync(job, $"Нет обработчика очереди {job.Queue}.", permanent: true, ct);
            return;
        }

        try
        {
            await handler.RunAsync(job, ct);
            await queue.CompleteAsync(job.Id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // остановка сервера: аренда истечёт, задание возьмут снова
        }
        catch (Exception ex)
        {
            var permanent = ex is RecruitmentJobFailure;
            logger.LogWarning(ex, "Подбор: задание {Queue} #{Id} ({Key}), попытка {Attempt}, не выполнено", job.Queue, job.Id, job.Key, job.Attempts);
            if (!await queue.RetryOrFailAsync(job, ex.Message, permanent, CancellationToken.None))
            {
                using var failScope = scopes.CreateScope();
                var failHandler = failScope.ServiceProvider.GetServices<IRecruitmentJobHandler>().First(h => h.Queue == job.Queue);
                try
                {
                    await failHandler.OnFailedAsync(job, ex.Message, CancellationToken.None);
                }
                catch (Exception failure)
                {
                    logger.LogError(failure, "Подбор: обработка провала задания {Queue} #{Id} не выполнена", job.Queue, job.Id);
                }
            }
        }
    }

    /// <summary>Выполнить всё, что готово, по одному (тесты, проверка): до max заданий.</summary>
    public async Task<int> DrainAsync(CancellationToken ct, int max = 200)
    {
        var done = 0;
        while (done < max)
        {
            RecruitmentJob? next = null;
            using (var scope = scopes.CreateScope())
            {
                var queue = scope.ServiceProvider.GetRequiredService<RecruitmentJobQueue>();
                foreach (var name in RecruitmentQueues.All)
                {
                    next = (await queue.ClaimAsync(name, 1, ct)).FirstOrDefault();
                    if (next is not null)
                    {
                        break;
                    }
                }
            }

            if (next is null)
            {
                return done;
            }

            await RunAsync(next, ct);
            done++;
        }

        return done;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Recruitment.Documents;
using OneBase.Application.Recruitment.Jobs;

namespace OneBase.Infrastructure.Recruitment;

/// <summary>
/// Очереди фоновых заданий подбора (раз в 2 с): у каждой очереди — свой предел одновременных заданий (<see cref="RecruitmentQueues.Concurrency"/>),
/// так что распознавание и разбор резюме не занимают ни скачивание, ни уведомления, а разбор входящих Telegram и ответы кандидатам идут своим
/// путём и не ждут их вовсе. Раз в минуту — сверка застрявших документов и чистка старых заданий. Перезапуск: задания в базе, «зависшие» берутся
/// снова по истечении аренды. Задания скачивают из Telegram и вызывают AI — в копии для проверки (DR) служба не запускается.
/// </summary>
internal sealed class RecruitmentJobWorker(IServiceScopeFactory scopes, RecruitmentJobRunner runner, ILogger<RecruitmentJobWorker> logger) : BackgroundService
{
    private readonly Dictionary<string, SemaphoreSlim> _slots = RecruitmentQueues.All.ToDictionary(q => q, q => new SemaphoreSlim(RecruitmentQueues.Concurrency(q)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            var lastSweep = DateTimeOffset.MinValue;
            do
            {
                try
                {
                    await PollAsync(stoppingToken);
                    if (DateTimeOffset.UtcNow - lastSweep > TimeSpan.FromMinutes(1))
                    {
                        lastSweep = DateTimeOffset.UtcNow;
                        await SweepAsync(stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Подбор: круг очередей заданий не выполнен");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // остановка сервера: начатые задания вернутся в очередь по истечении аренды
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        foreach (var queue in RecruitmentQueues.All)
        {
            var slots = _slots[queue];
            var free = slots.CurrentCount;
            if (free == 0)
            {
                continue;
            }

            List<OneBase.Domain.Recruitment.RecruitmentJob> claimed;
            using (var scope = scopes.CreateScope())
            {
                claimed = await scope.ServiceProvider.GetRequiredService<RecruitmentJobQueue>().ClaimAsync(queue, free, ct);
            }

            foreach (var job in claimed)
            {
                await slots.WaitAsync(ct);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await runner.RunAsync(job, ct);
                    }
                    finally
                    {
                        slots.Release();
                    }
                }, CancellationToken.None);
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var queued = await scope.ServiceProvider.GetRequiredService<ResumeDocumentProcessor>().ReconcileAsync(ct);
        foreach (var sweeper in scope.ServiceProvider.GetServices<IRecruitmentSweeper>())
        {
            queued += await sweeper.SweepAsync(ct);
        }

        var cleaned = await scope.ServiceProvider.GetRequiredService<RecruitmentJobQueue>().CleanupAsync(ct);
        if (queued + cleaned > 0)
        {
            logger.LogInformation("Подбор: сверка заданий — поставлено снова {Queued}, удалено старых {Cleaned}", queued, cleaned);
        }
    }
}

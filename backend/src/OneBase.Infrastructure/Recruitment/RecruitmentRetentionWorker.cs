using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Recruitment;

namespace OneBase.Infrastructure.Recruitment;

/// <summary>
/// Раз в час — удаление данных кандидатов с истёкшим сроком хранения (<see cref="RecruitmentRetentionService"/>). Внутренняя служба: наружу не
/// ходит и никому не пишет, поэтому работает и в копии для проверки.
/// </summary>
internal sealed class RecruitmentRetentionWorker(IServiceScopeFactory scopes, ILogger<RecruitmentRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<RecruitmentRetentionService>().RunAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Подбор: круг удаления по сроку хранения не выполнен");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // остановка сервера
        }
    }
}

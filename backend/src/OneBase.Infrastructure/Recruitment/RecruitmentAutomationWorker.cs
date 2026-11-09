using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Recruitment;

namespace OneBase.Infrastructure.Recruitment;

/// <summary>
/// Раз в 5 минут — автоматика подбора: напоминания кандидатам о собеседовании и одно напоминание не ответившему (через очередь исходящих HR),
/// проверка номеров HR (исключение, если номер отключился или ограничен), автооценка резюме (если включена). Ставит сообщения людям — в копии
/// для проверки не регистрируется. Сбой круга — в журнал, следующий круг — как обычно.
/// </summary>
internal sealed class RecruitmentAutomationWorker(IServiceScopeFactory scopes, ILogger<RecruitmentAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var (reminders, followUps, scored) = await scope.ServiceProvider.GetRequiredService<RecruitmentAutomation>().RunAsync(stoppingToken);
                    if (reminders + followUps + scored > 0)
                    {
                        logger.LogInformation("Подбор: напоминаний о собеседовании {Reminders}, напоминаний не ответившим {FollowUps}, оценено {Scored}", reminders, followUps, scored);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Подбор: круг автоматики не выполнен");
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

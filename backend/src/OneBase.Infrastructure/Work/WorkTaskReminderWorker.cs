using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Field;
using OneBase.Application.Work;

namespace OneBase.Infrastructure.Work;

/// <summary>
/// Напоминания раздела «Задачи»: раз в 15 минут после 09:00 по Ташкенту — «срок завтра» и «просрочена». Одно уведомление на срок
/// (отметка ставится условным обновлением в базе — см. WorkTaskService.RemindAsync), поэтому перезапуск и частые проверки не дублируют.
/// Выключается Work:Reminders:Enabled = false.
/// </summary>
internal sealed class WorkTaskReminderWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<WorkTaskReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly From = new(9, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Work:Reminders:Enabled", true))
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5));
                if (TimeOnly.FromDateTime(now.DateTime) < From)
                {
                    continue;
                }

                try
                {
                    using var scope = scopes.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<WorkTaskService>().RemindAsync(FieldClock.Today, stoppingToken);
                    if (result.DueSoon + result.Overdue > 0)
                    {
                        logger.LogInformation("Задачи: напоминаний «срок завтра» {DueSoon}, «просрочена» {Overdue}", result.DueSoon, result.Overdue);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Задачи: напоминания о сроках не отправлены");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }
}

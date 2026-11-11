using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Analytik.Readiness;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Hr;
using OneBase.Infrastructure.Hr;
using OneBase.Infrastructure.Linko;

namespace OneBase.Infrastructure.Analytik;

/// <summary>
/// Подключения Аналитика к внешним системам: Linko (продажи, оплаты, остатки, планы) и Verifix (посещаемость) — через их же настройки и
/// координаторы синхронизации (параллельных запусков не бывает, кнопка «Обновить» и расписание работают как раньше).
/// </summary>
internal sealed class AnalytikSourceSync(
    LinkoSettingsStore linkoSettings,
    LinkoSyncCoordinator linko,
    SalesOptions sales,
    VerifixSettingsStore verifixSettings,
    HrSyncCoordinator hr,
    VerifixOptions verifixOptions,
    IServiceScopeFactory scopes,
    ILogger<AnalytikSourceSync> logger) : IAnalytikSourceSync
{
    /// <summary>Посещаемость устарела, если Verifix не обновлялся дольше 6 часов (как в контроле качества данных).</summary>
    public static readonly TimeSpan VerifixStaleAfter = TimeSpan.FromHours(6);

    public async Task<IReadOnlyList<SourceLink>> LinksAsync(CancellationToken ct)
    {
        var l = await linkoSettings.GetAsync(ct);
        var times = LinkoSyncSchedule.TimesOf(sales.Sync);
        var v = await verifixSettings.GetAsync(ct);
        return
        [
            new SourceLink(SourceCodes.Linko, "Linko", l.IsReady, sales.Sync.Enabled && times.Count > 0, linko.IsRunning,
                // Как у контроля обмена (LinkoSyncHealth): самый долгий промежуток между запусками и час запаса.
                LinkoSyncSchedule.LongestGap(times) + TimeSpan.FromHours(1), "/settings/integrations/sales/linko",
                !l.HasCredentials ? "Linko не подключён: в «Настройки → Интеграции → Linko» не указан адрес или токен."
                : !l.Enabled ? "Обмен с Linko выключен в «Настройки → Интеграции → Linko»."
                : null,
                $"{Permissions.SalesManage}|{Permissions.IntegrationsManage}"),
            new SourceLink(SourceCodes.Verifix, "Verifix", v.IsReady, verifixOptions.WorkerEnabled && v.Data.AutoSync, hr.IsRunning, VerifixStaleAfter,
                $"/settings/integrations/{VerifixSettingsStore.Department}/{VerifixSettingsStore.Code}",
                !v.HasCredentials ? "Verifix не подключён: в «Настройки → Интеграции → Verifix» не указаны адрес и ключи."
                : !v.Enabled ? "Обмен с Verifix выключен в «Настройки → Интеграции → Verifix»."
                : null,
                Permissions.IntegrationsManage),
        ];
    }

    public async Task<SyncStartResult> StartAsync(string code, CancellationToken ct)
    {
        switch (code)
        {
            case SourceCodes.Linko:
                if (!(await linkoSettings.GetAsync(ct)).IsReady)
                {
                    return SyncStartResult.NotConnected;
                }

                // Обычное обновление по изменениям: всё, что изменилось в Linko после последней удачной загрузки, — даже после долгого перерыва.
                return linko.TryStartInBackground(LinkoSyncMode.Incremental) ? SyncStartResult.Started : SyncStartResult.AlreadyRunning;
            case SourceCodes.Verifix:
                var (result, _) = await hr.StartInBackgroundAsync(HrSyncTrigger.Auto, null, null, ct);
                return result switch
                {
                    HrSyncStartResult.Started => SyncStartResult.Started,
                    HrSyncStartResult.AlreadyRunning => SyncStartResult.AlreadyRunning,
                    _ => SyncStartResult.NotConnected,
                };
            default:
                throw new ArgumentException($"Неизвестный источник данных: {code}", nameof(code));
        }
    }

    public async Task<bool> AlertAsync(string code, string cause, CancellationToken ct)
    {
        if (code != SourceCodes.Linko)
        {
            return false; // Verifix: отставание видно в рисках Аналитика (качество данных) и в журнале синхронизации HR
        }

        using var scope = scopes.CreateScope();
        var sent = await scope.ServiceProvider.GetRequiredService<LinkoSyncHealth>().CheckAsync(null, ct, cause);
        if (sent)
        {
            logger.LogInformation("Аналитик: администраторам интеграций отправлено уведомление об обмене с Linko ({Cause})", cause);
        }

        return sent;
    }
}

using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Readiness;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Field;
using OneBase.Domain.Analytik;

namespace OneBase.AI.Analytik;

/// <summary>Фоновая задача Аналитика и её период (ключ запуска).</summary>
public sealed record AnalytikJob(string Name, string PeriodKey);

/// <summary>
/// Автономная работа Аналитика 24/7 без платных вызовов: каждые 5 минут — контроль данных (DataKeeper: отстающие Linko и Verifix обновляются
/// заранее, ежедневный отчёт ждёт вчерашних продаж до DailyAt + 2 часа); каждые 30 минут — каталог базы (по отпечатку схемы: без изменений — только числа
/// строк) и дневные агрегаты; каждый час — риски и отклонения (правила кодом) с уведомлениями; ежедневно в Analytik:DailyAt — снимки прогнозов
/// и сверка прошлых с фактом, ежедневный отчёт; по понедельникам — недельный; 1-го числа — месячный. Каждая задача выполняется один раз за
/// свой период (уникальный analytik.JobRuns(Job, PeriodKey)) — даже при нескольких экземплярах API; упавшая — повторяется через час.
/// </summary>
internal sealed class AnalytikWorker(IServiceScopeFactory scopes, AnalytikOptions options, TimeProvider clock, ILogger<AnalytikWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.WorkerEnabled)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(Tick);
            do
            {
                await KeepAsync(stoppingToken);
                foreach (var job in Due(clock.GetUtcNow(), options.DailyAt))
                {
                    if (job.Name == "daily" && await HoldDailyAsync(stoppingToken))
                    {
                        continue; // вчерашние продажи ещё догружаются — отчёт на следующем тике (не позже DailyAt + 2 часа)
                    }

                    await RunAsync(job, stoppingToken);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // остановка приложения
        }
    }

    /// <summary>Какие задачи пора выполнить в этот момент (по Ташкенту) — ключи периодов.</summary>
    internal static IReadOnlyList<AnalytikJob> Due(DateTimeOffset utc, string dailyAt)
    {
        var local = utc.ToOffset(FieldClock.Offset);
        var jobs = new List<AnalytikJob>
        {
            new("refresh", local.ToString("yyyy-MM-dd'T'HH", CultureInfo.InvariantCulture) + (local.Minute < 30 ? ":00" : ":30")),
            new("hourly", local.ToString("yyyy-MM-dd'T'HH", CultureInfo.InvariantCulture)),
        };
        var at = TimeOnly.TryParse(dailyAt, CultureInfo.InvariantCulture, out var t) ? t : new TimeOnly(7, 30);
        if (TimeOnly.FromDateTime(local.DateTime) >= at)
        {
            var day = DateOnly.FromDateTime(local.DateTime);
            jobs.Add(new AnalytikJob("daily", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            if (day.DayOfWeek == DayOfWeek.Monday)
            {
                jobs.Add(new AnalytikJob("weekly", $"{ISOWeek.GetYear(local.DateTime)}-W{ISOWeek.GetWeekOfYear(local.DateTime):00}"));
            }

            if (day.Day == 1)
            {
                jobs.Add(new AnalytikJob("monthly", day.ToString("yyyy-MM", CultureInfo.InvariantCulture)));
            }
        }

        return jobs;
    }

    /// <summary>Контроль данных (DataKeeper): отстающие источники обновляются заранее, не исправимое само — администраторам. Ошибка — в журнал.</summary>
    private async Task KeepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DataKeeper>().RunAsync("schedule", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: контроль данных не выполнен");
        }
    }

    private async Task<bool> HoldDailyAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<DataKeeper>().HoldDailyAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: проверка данных перед ежедневным отчётом не выполнена");
            return false;
        }
    }

    public async Task RunAsync(AnalytikJob job, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AnalytikJobs>();
        try
        {
            await runner.RunAsync(job, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: задача {Job} ({Period}) не выполнена", job.Name, job.PeriodKey);
        }
    }
}

/// <summary>Тела фоновых задач Аналитика (вызываются и вручную: «Запустить сейчас» для управляющего).</summary>
public sealed class AnalytikJobs(
    IAnalytikDb adb,
    OneBase.Application.Abstractions.IAppDbContext db,
    CatalogService catalog,
    RiskEngine risks,
    ForecastService forecasts,
    AnalytikReportService reports,
    SalesAnalytik sales,
    SupplyPlanner supply,
    TimeProvider clock,
    ILogger<AnalytikJobs> logger,
    OneBase.Application.Analytik.Decisions.DecisionService? decisions = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly string[] Names = ["refresh", "hourly", "daily", "weekly", "monthly"];

    /// <summary>Выполнить задачу периода, если она ещё не выполнялась (или упала больше часа назад). false — уже выполнена или выполняется.</summary>
    public async Task<bool> RunAsync(AnalytikJob job, CancellationToken ct, bool force = false)
    {
        var run = await ClaimAsync(job, force, ct);
        if (run is null)
        {
            return false;
        }

        try
        {
            var summary = job.Name switch
            {
                "refresh" => await RefreshAsync(ct),
                "hourly" => await HourlyAsync(ct),
                "daily" => await DailyAsync(run.Id, ct),
                "weekly" => await ReportAsync("weekly", Range(7), run.Id, ct),
                "monthly" => await ReportAsync("monthly", PreviousMonth(), run.Id, ct),
                _ => throw new ArgumentException(job.Name),
            };
            run.Status = "ok";
            run.Summary = JsonSerializer.Serialize(summary, Json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: задача {Job} упала", job.Name);
            // Несохранённые изменения упавшей задачи (они и могли быть причиной) не должны помешать записать её итог.
            db.ChangeTracker.Clear();
            adb.JobRuns.Update(run);
            run.Status = "failed";
            run.Error = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
        }

        run.FinishedAt = clock.GetUtcNow();
        await adb.SaveChangesAsync(CancellationToken.None);
        return run.Status == "ok";
    }

    /// <summary>
    /// Захват периода задачи: новая строка с уникальным ключом (Job, PeriodKey); уже есть — выполнена или выполняется другим экземпляром.
    /// Упавшую больше часа назад (или по force) — захватываем заново.
    /// </summary>
    private async Task<AnalytikJobRun?> ClaimAsync(AnalytikJob job, bool force, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var existing = await adb.JobRuns.FirstOrDefaultAsync(r => r.Job == job.Name && r.PeriodKey == job.PeriodKey, ct);
        if (existing is not null)
        {
            var retry = force || (existing.Status == "failed" && existing.StartedAt < now.AddHours(-1))
                        || (existing.Status == "running" && existing.StartedAt < now.AddHours(-3));
            if (!retry)
            {
                return null;
            }

            existing.Status = "running";
            existing.StartedAt = now;
            existing.FinishedAt = null;
            existing.Error = null;
            await adb.SaveChangesAsync(ct);
            return existing;
        }

        var run = new AnalytikJobRun { Job = job.Name, PeriodKey = job.PeriodKey, StartedAt = now, Status = "running" };
        adb.JobRuns.Add(run);
        try
        {
            await adb.SaveChangesAsync(ct);
            return run;
        }
        catch (DbUpdateException ex)
        {
            adb.JobRuns.Entry(run).State = EntityState.Detached;
            // Уникальный ключ (Job, PeriodKey): другой экземпляр успел захватить этот период. Иначе — ошибка базы: в журнал, повтор на следующем тике.
            if (!await adb.JobRuns.AsNoTracking().AnyAsync(r => r.Job == job.Name && r.PeriodKey == job.PeriodKey, ct))
            {
                logger.LogWarning(ex, "Аналитик: задача {Job} ({Period}) не захвачена", job.Name, job.PeriodKey);
            }

            return null;
        }
    }

    private DateOnly Today => FieldClock.LocalDate(clock.GetUtcNow());

    private AnalytikPeriod Range(int days)
    {
        var to = Today.AddDays(-1);
        return AnalytikPeriod.Of("custom", to.AddDays(-days + 1), to, Today);
    }

    private AnalytikPeriod PreviousMonth()
    {
        var first = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        return AnalytikPeriod.Of("custom", first, first.AddMonths(1).AddDays(-1), Today);
    }

    private async Task<object> RefreshAsync(CancellationToken ct)
    {
        var result = await catalog.RefreshAsync("schedule", null, false, ct);
        var snapshots = await SnapshotMetricsAsync(ct);
        var drafts = await PurgeDraftBaselinesAsync(ct);
        return new { result.Unchanged, result.Tables, result.Added, result.Changed, result.Removed, Snapshots = snapshots, DraftBaselines = drafts };
    }

    /// <summary>Черновые снимки симулятора (экран считал, сценарий не сохранили) старше 2 дней — удаляются; снимки сценариев и их истории закреплены.</summary>
    internal async Task<int> PurgeDraftBaselinesAsync(CancellationToken ct)
    {
        var before = clock.GetUtcNow().ToUniversalTime().AddDays(-2);
        return await adb.ScenarioBaselines
            .Where(b => !b.Pinned && b.CreatedAt < before && !adb.Scenarios.Any(s => s.BaselineId == b.Id))
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>Дневные агрегаты: продажи последних 3 дней (догружаются синхронизацией) и стоимость склада сырья на сегодня — без пересчёта всей базы.</summary>
    private async Task<int> SnapshotMetricsAsync(CancellationToken ct)
    {
        var system = AnalytikPrincipal.SystemPrincipal;
        var today = Today;
        var values = new List<(DateOnly Date, string Metric, decimal Value, string Unit)>();
        foreach (var metric in new[] { "revenue", "kg", "orders" })
        {
            foreach (var p in await sales.DailyAsync(metric, today.AddDays(-3), today, system, ct))
            {
                values.Add((p.Date, $"sales.{metric}", p.Value, metric == "revenue" ? "сум" : metric == "kg" ? "кг" : "шт"));
            }
        }

        var plan = await supply.PlanAsync(system, ct);
        values.Add((today, "procurement.stock_value", plan.StockValue, "сум"));
        values.Add((today, "procurement.critical_items", plan.Risks.GetValueOrDefault("critical"), "позиций"));

        var keys = values.Select(v => v.Metric).Distinct().ToList();
        var from = values.Min(v => v.Date);
        var existing = await adb.MetricSnapshots.Where(s => keys.Contains(s.Metric) && s.Dimension == "" && s.Date >= from).ToListAsync(ct);
        foreach (var v in values)
        {
            var row = existing.FirstOrDefault(s => s.Metric == v.Metric && s.Date == v.Date);
            if (row is null)
            {
                adb.MetricSnapshots.Add(new AnalytikMetricSnapshot { Date = v.Date, Metric = v.Metric, Value = v.Value, Unit = v.Unit, ComputedAt = clock.GetUtcNow() });
            }
            else
            {
                row.Value = v.Value;
                row.ComputedAt = clock.GetUtcNow();
            }
        }

        await adb.SaveChangesAsync(ct);
        return values.Count;
    }

    private async Task<object> HourlyAsync(CancellationToken ct)
    {
        var result = await risks.RunAsync(notify: true, ct);
        return new { result.Insights, result.Rules, result.Errors, result.DurationMs };
    }

    private async Task<object> DailyAsync(Guid runId, CancellationToken ct)
    {
        var (saved, evaluated) = await forecasts.SnapshotAsync(ct);
        var report = await reports.CreateAsync("daily", AnalytikPeriod.Of("yesterday", null, null, Today), AnalytikPrincipal.SystemPrincipal, null, runId, ct);
        // Память решений: решения, у которых наступил срок проверки, оцениваются по факту (по правам автора).
        var decided = 0;
        if (decisions is not null)
        {
            try
            {
                decided = await decisions.EvaluateDueAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Аналитик: решения в срок не оценены");
            }
        }

        return new { ForecastsSaved = saved, ForecastsEvaluated = evaluated, ReportId = report.Id, DecisionsEvaluated = decided };
    }

    private async Task<object> ReportAsync(string kind, AnalytikPeriod period, Guid runId, CancellationToken ct)
    {
        var report = await reports.CreateAsync(kind, period, AnalytikPrincipal.SystemPrincipal, null, runId, ct);
        return new { ReportId = report.Id, report.Title };
    }
}

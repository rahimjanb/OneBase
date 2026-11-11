using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Field;
using OneBase.Domain.AI;

namespace OneBase.AI.Analytik;

public sealed record HealthComponent(string Title, int Points, string Reason);

/// <summary>
/// Состояние компании 0–100 и из чего оно сложилось: каждое снижение — с причиной (проверяемо, без «чёрного ящика»). Coverage — не null, когда
/// пользователь видит не всё (часть разделов или своя область продаж): оценка тогда только по его данным, а не по компании.
/// </summary>
public sealed record CompanyHealth(int Score, string Level, IReadOnlyList<HealthComponent> Components, string? Coverage = null);

public sealed record SupplySummary(IReadOnlyDictionary<string, int> Risks, IReadOnlyList<SupplyHorizonTotal> Horizons, IReadOnlyList<SupplyRecommendation> Top,
    IReadOnlyDictionary<string, int> WarehouseStatus, decimal StockValue, int ItemsWithoutPrice, IReadOnlyList<string> Notes);

public sealed record ForecastSummary(string Metric, string Title, string Unit, decimal Total, decimal Lower, decimal Upper, int Steps, string Granularity, string Method,
    decimal? Wape, bool Reliable, IReadOnlyList<Application.Analytik.Forecasting.ForecastPoint> Points);

public sealed record FreshnessRow(string Source, DateTimeOffset? At, string Status, string? Note = null);

public sealed record RecentAnalysis(Guid Id, string Title, DateTimeOffset At);

/// <summary>
/// Executive Intelligence Dashboard: состояние компании, показатели отделов, критические риски, рекомендации, прогноз, свежесть данных и
/// последние исследования — только то, что пользователю видно по его правам и области продаж. Errors — блоки, которые не удалось посчитать
/// (остальные показываются).
/// </summary>
public sealed record DashboardView(
    AnalytikPeriod Period, DateTimeOffset At, CompanyHealth Health, SalesBlock? Sales, FinanceBlock? Finance, ProductionBlock? Production, SupplySummary? Supply,
    HrBlock? Hr, WorkBlock? Work, IReadOnlyList<InsightView> Risks, IReadOnlyList<string> Recommendations, ForecastSummary? Forecast,
    IReadOnlyList<FreshnessRow> Freshness, IReadOnlyList<RecentAnalysis> Recent, string? Scope, IReadOnlyList<string> Errors);

public sealed class DashboardService(
    IAppDbContext db,
    IAnalytikDb adb,
    SalesAnalytik sales,
    FinanceAnalytik finance,
    OperationsAnalytik operations,
    SupplyPlanner supply,
    RiskEngine risks,
    ForecastService forecasts,
    CatalogService catalog,
    TimeProvider clock,
    ILogger<DashboardService> logger)
{
    /// <summary>withForecast = false — без прогноза (экран грузит его отдельным запросом: история прогноза — полгода продаж, первый расчёт после запуска долгий).</summary>
    public async Task<DashboardView> BuildAsync(AnalytikPeriod period, AnalytikPrincipal principal, CancellationToken ct, bool withForecast = true)
    {
        var errors = new List<string>();

        async Task<T?> Safe<T>(string name, Func<Task<T?>> run) where T : class
        {
            try
            {
                return await run();
            }
            catch (QueryPlanException)
            {
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Аналитик: блок панели «{Block}» не посчитан", name);
                errors.Add(name);
                return null;
            }
        }

        var salesBlock = await Safe("Продажи", () => sales.BlockAsync(period, principal, ct));
        var financeBlock = await Safe("Финансы", () => finance.BlockAsync(period, principal, salesBlock, ct));
        var productionBlock = await Safe("Производство", () => operations.ProductionAsync(period, principal, ct, 10));
        var supplyPlan = SupplyPlanner.CanView(principal) ? await Safe("Склад и снабжение", () => supply.PlanAsync(principal, ct)) : null;
        var hrBlock = await Safe("Персонал", () => operations.HrAsync(period, principal, ct));
        var workBlock = await Safe("Задачи", () => operations.WorkAsync(period, principal, ct));
        var insightList = await Safe("Риски", async () => (IReadOnlyList<InsightView>?)await risks.ListAsync(principal, null, false, 200, ct)) ?? [];
        var forecast = withForecast && SalesAnalytik.CanView(principal)
            ? await Safe("Прогноз", async () =>
            {
                var view = await forecasts.ForecastAsync("sales.revenue", "day", 30, principal, ct);
                return new ForecastSummary(view.Metric, view.Title, view.Unit, view.Result.Total, view.Result.TotalLower, view.Result.TotalUpper, view.Result.Points.Count,
                    "day", view.Result.Method, view.Result.Wape, view.Result.Reliable, view.Result.Points);
            })
            : null;

        var supplySummary = supplyPlan is null
            ? null
            : new SupplySummary(supplyPlan.Risks, supplyPlan.Horizons, supplyPlan.Items.Where(i => i.Risk is "critical" or "high").Take(8).ToList(),
                supplyPlan.WarehouseStatus, supplyPlan.StockValue, supplyPlan.ItemsWithoutPrice, supplyPlan.DataNotes);
        // Сначала риски бизнеса, затем сигналы качества данных: устаревший обмен не должен вытеснять дефицит сырья.
        var top = insightList.OrderBy(i => i.Kind == Application.Analytik.Risk.InsightKinds.Quality).ThenByDescending(i => Severities.Rank(i.Severity)).Take(12).ToList();
        var recommendations = insightList.Where(i => i.Recommendations.Count > 0 && i.Severity != Severities.Info)
            .Take(6).Select(i => $"{i.Title} — {i.Recommendations[0]}").ToList();

        var health = Health(insightList, salesBlock, supplyPlan, productionBlock) with { Coverage = Coverage(principal, salesBlock, financeBlock, productionBlock, supplyPlan, hrBlock) };
        return new DashboardView(period, clock.GetUtcNow(), health, salesBlock, financeBlock, productionBlock,
            supplySummary, hrBlock, workBlock, top, recommendations, forecast, await FreshnessAsync(principal, ct), await RecentAsync(principal, ct),
            principal.Sales.Restricted ? principal.Sales.Description : null, errors);
    }

    /// <summary>
    /// Состояние компании: 100 минус снижения — критические риски (−12, не больше −36), предупреждения (−4, не больше −20), прогноз выполнения
    /// плана продаж ниже 95 % (−7) или 80 % (−15), критичный дефицит сырья (−10), просроченные задания производства (−5). Учитывается только
    /// то, что пользователь видит.
    /// </summary>
    internal static CompanyHealth Health(IReadOnlyList<InsightView> insights, SalesBlock? sales, SupplyPlanView? supply, ProductionBlock? production)
    {
        var parts = new List<HealthComponent>();
        var critical = insights.Count(i => i.Severity == Severities.Critical && i.Kind != InsightKinds.Quality);
        var warnings = insights.Count(i => i.Severity == Severities.Warning && i.Kind != InsightKinds.Quality);
        if (critical > 0)
        {
            parts.Add(new HealthComponent("Критические риски", -Math.Min(36, critical * 12), $"{critical} активных"));
        }

        if (warnings > 0)
        {
            parts.Add(new HealthComponent("Предупреждения", -Math.Min(20, warnings * 4), $"{warnings} активных"));
        }

        if (sales?.Plan?.ForecastExecution is { } forecast)
        {
            if (forecast < 80)
            {
                parts.Add(new HealthComponent("План продаж", -15, $"прогноз выполнения {forecast:0.#}%"));
            }
            else if (forecast < 95)
            {
                parts.Add(new HealthComponent("План продаж", -7, $"прогноз выполнения {forecast:0.#}%"));
            }
        }

        if (supply?.Risks.GetValueOrDefault("critical") is > 0 and var shortage)
        {
            parts.Add(new HealthComponent("Сырьё", -10, $"{shortage} позиций в критичном дефиците"));
        }

        if (production?.Kpis.FirstOrDefault(k => k.Code == "production.overdue_orders")?.Value is > 0 and var overdue)
        {
            parts.Add(new HealthComponent("Производство", -5, $"{overdue:0} просроченных заданий"));
        }

        var score = Math.Max(0, 100 + parts.Sum(p => p.Points));
        return new CompanyHealth(score, score >= 80 ? "good" : score >= 60 ? "attention" : "critical", parts);
    }

    /// <summary>
    /// Что вошло в оценку, если пользователь видит не всю компанию: иначе «всё под контролем» у кадровика при критичных рисках продаж вводило бы
    /// в заблуждение. Null — видны все разделы по всей организации.
    /// </summary>
    internal static string? Coverage(AnalytikPrincipal principal, SalesBlock? sales, FinanceBlock? finance, ProductionBlock? production, SupplyPlanView? supply, HrBlock? hr)
    {
        var seen = new List<string>();
        if (sales is not null)
        {
            seen.Add(principal.Sales.Restricted ? $"продажи ({principal.Sales.Description})" : "продажи");
        }

        if (finance is not null)
        {
            seen.Add("финансы");
        }

        if (production is not null)
        {
            seen.Add("производство");
        }

        if (supply is not null)
        {
            seen.Add("склад и снабжение");
        }

        if (hr is not null)
        {
            seen.Add("персонал");
        }

        var full = !principal.Sales.Restricted && sales is not null && finance is not null && production is not null && supply is not null && hr is not null;
        return full ? null : seen.Count == 0 ? "Оценка без данных разделов: вам не доступен ни один раздел." : $"Только по доступным вам данным: {string.Join(", ", seen)}.";
    }

    private async Task<IReadOnlyList<FreshnessRow>> FreshnessAsync(AnalytikPrincipal principal, CancellationToken ct)
    {
        var rows = new List<FreshnessRow>();
        var now = clock.GetUtcNow();
        if (SalesAnalytik.CanView(principal))
        {
            var orders = await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "orders", ct);
            rows.Add(new FreshnessRow("Linko: заказы", orders?.LastSuccessAt,
                orders?.LastSuccessAt is { } at && now - at < TimeSpan.FromHours(3) ? "ok" : "stale", orders?.LastError));
        }

        if (principal.Has(Application.Security.Permissions.HrRead))
        {
            var hr = await db.HrSyncRuns.AsNoTracking().Where(r => r.Status == "Succeeded" || r.Status == "Partial").MaxAsync(r => (DateTimeOffset?)r.FinishedAt, ct);
            rows.Add(new FreshnessRow("Verifix: посещаемость", hr, hr is { } at && now - at < TimeSpan.FromHours(6) ? "ok" : "stale"));
        }

        var model = await catalog.ModelAsync(ct);
        rows.Add(new FreshnessRow("Каталог базы", model.RefreshedAt, model.RefreshedAt is { } c && now - c < TimeSpan.FromHours(2) ? "ok" : "stale"));
        var hourly = await adb.JobRuns.AsNoTracking().Where(j => j.Job == "hourly" && j.Status == "ok").MaxAsync(j => (DateTimeOffset?)j.FinishedAt, ct);
        rows.Add(new FreshnessRow("Поиск рисков", hourly, hourly is { } h && now - h < TimeSpan.FromHours(2) ? "ok" : "stale"));
        return rows;
    }

    private async Task<IReadOnlyList<RecentAnalysis>> RecentAsync(AnalytikPrincipal principal, CancellationToken ct) =>
        principal.UserId is not { } userId
            ? []
            : await db.AiConversations.AsNoTracking()
                .Where(c => c.UserId == userId && c.ArchivedAt == null && c.Kind == ConversationKinds.Analytik)
                .OrderByDescending(c => c.LastMessageAt).Take(6)
                .Select(c => new RecentAnalysis(c.Id, c.Title, c.LastMessageAt))
                .ToListAsync(ct);
}

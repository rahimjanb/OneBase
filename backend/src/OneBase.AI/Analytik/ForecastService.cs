using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Forecasting;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Analytik;
using OneBase.Domain.Procurement;

namespace OneBase.AI.Analytik;

/// <summary>Точность прошлых прогнозов показателя: сколько прогнозов сверено с фактом и их WAPE.</summary>
public sealed record ForecastAccuracy(int Evaluated, decimal? Wape, DateOnly? From, DateOnly? To);

/// <summary>Прогноз показателя: история (на которой он построен), результат с диапазоном и сценариями, точность прошлых прогнозов, область, источники.</summary>
public sealed record ForecastView(
    string Metric, string Title, string Unit, string Granularity, IReadOnlyList<SeriesPoint> History, ForecastResult Result, ForecastAccuracy? Past,
    string? Scope, IReadOnlyList<string> Sources);

/// <summary>
/// Прогнозы Аналитика: ряды из данных OneBase (продажи — по правилам «Продаж», оплаты точек, расход сырья), прогноз — Forecaster (математика,
/// без модели), права — пользователя (продажи — в его области). Снимки прогнозов сохраняются (analytik.Forecasts) и потом сверяются с фактом —
/// так видна точность прошлых прогнозов.
/// </summary>
public sealed class ForecastService(IAppDbContext db, IAnalytikDb adb, SalesAnalytik sales, TimeProvider clock)
{
    public static readonly IReadOnlyDictionary<string, (string Title, string Unit)> Metrics = new Dictionary<string, (string, string)>
    {
        ["sales.revenue"] = ("Выручка вторички", "сум"),
        ["sales.kg"] = ("Продажи, кг", "кг"),
        ["sales.orders"] = ("Заказы", "шт"),
        ["finance.payments"] = ("Оплаты точек (принятые)", "сум"),
        ["procurement.consumption"] = ("Расход сырья", "ед."),
    };

    private DateOnly Today => FieldClock.LocalDate(clock.GetUtcNow());

    public async Task<ForecastView> ForecastAsync(string metric, string granularity, int horizon, AnalytikPrincipal p, CancellationToken ct, Guid? itemId = null,
        decimal? scenarioChange = null)
    {
        if (!Metrics.TryGetValue(metric, out var meta))
        {
            throw new QueryPlanException(QueryErrors.Invalid, $"Показатель прогноза: {string.Join(", ", Metrics.Keys)}.");
        }

        if (granularity is not ("day" or "week" or "month"))
        {
            throw new QueryPlanException(QueryErrors.Invalid, "Шаг прогноза: day, week или month.");
        }

        var (history, unit, scope) = await HistoryAsync(metric, granularity, p, itemId, ct);
        if (history.Count == 0)
        {
            throw new QueryPlanException(QueryErrors.Invalid, $"Для «{meta.Title}» нет истории в OneBase — прогноз не строится.");
        }

        var result = Forecaster.Forecast(history, granularity, Math.Clamp(horizon, 1, granularity == "day" ? 120 : granularity == "week" ? 26 : 12));
        if (scenarioChange is { } change && change != 0)
        {
            result = result with
            {
                Points = Forecaster.Scenario(result.Points, change),
                Total = Math.Round(result.Total * (1 + change), 2),
                TotalLower = Math.Round(result.TotalLower * (1 + change), 2),
                TotalUpper = Math.Round(result.TotalUpper * (1 + change), 2),
                Assumptions = [.. result.Assumptions, $"Сценарий: значения прогноза умножены на {1 + change:0.##} ({change:+0%;-0%})."],
            };
        }

        var past = p.Sales.Restricted ? null : await AccuracyAsync(metric, itemId?.ToString() ?? "", granularity, ct);
        return new ForecastView(metric, meta.Title, unit ?? meta.Unit, granularity, history, result, past, scope, SourcesOf(metric));
    }

    private static IReadOnlyList<string> SourcesOf(string metric) => metric switch
    {
        "finance.payments" => ["OneBase → Linko: оплаты точек (принятые)"],
        "procurement.consumption" => ["OneBase → Снабжение → Склад: движения (выдачи и выдачи в производство минус возвраты)"],
        _ => ["OneBase → Продажи (Linko): по правилам отчётов продаж"],
    };

    /// <summary>Ряд показателя: дни — последние 182 (неделя — 52 недели, месяц — 24 месяца); текущий неполный интервал не входит.</summary>
    internal async Task<(IReadOnlyList<SeriesPoint> Series, string? Unit, string? Scope)> HistoryAsync(string metric, string granularity, AnalytikPrincipal p,
        Guid? itemId, CancellationToken ct)
    {
        var today = Today;
        var end = today.AddDays(-1);
        var from = granularity switch
        {
            "day" => end.AddDays(-181),
            "week" => Monday(end).AddDays(-7 * 52),
            _ => new DateOnly(end.Year, end.Month, 1).AddMonths(-24),
        };
        IReadOnlyList<SeriesPoint> daily;
        string? unit = null;
        string? scope = null;
        switch (metric)
        {
            case "sales.revenue" or "sales.kg" or "sales.orders":
                daily = await sales.DailyAsync(metric[6..], from, end, p, ct);
                scope = p.Sales.Restricted ? p.Sales.Description : null;
                break;
            case "finance.payments":
                if (!p.Has(Permissions.FinanceRead) || p.Sales.Restricted)
                {
                    throw new QueryPlanException(QueryErrors.Denied, "Прогноз оплат — с правом «Финансы» и доступом ко всей организации.");
                }

                var rows = await db.LinkoPayments.AsNoTracking()
                    .Where(x => !x.IsDelete && x.Status == "accepted" && x.CreatedDate >= from && x.CreatedDate <= end)
                    .GroupBy(x => x.CreatedDate!.Value).Select(g => new { Day = g.Key, Sum = g.Sum(x => x.Amount) }).ToListAsync(ct);
                daily = Days(from, end, d => rows.FirstOrDefault(r => r.Day == d)?.Sum ?? 0);
                break;
            case "procurement.consumption":
            {
                if (!SupplyPlanner.CanView(p))
                {
                    throw new QueryPlanException(QueryErrors.Denied, "Прогноз расхода сырья — снабжению, складу, производству и директору.");
                }

                var since = FieldClock.DayStartUtc(from);
                var used = await db.ProcurementStockMovements.AsNoTracking()
                    .Where(m => m.CreatedAt >= since && (itemId == null || m.ItemId == itemId)
                        && (m.Kind == ProcurementMovementKind.Issue || m.Kind == ProcurementMovementKind.ProductionIssue || m.Kind == ProcurementMovementKind.ProductionReturn))
                    .Select(m => new { m.CreatedAt, m.Quantity })
                    .ToListAsync(ct);
                var byDay = used.GroupBy(m => FieldClock.LocalDate(m.CreatedAt)).ToDictionary(g => g.Key, g => -g.Sum(m => m.Quantity));
                daily = Days(from, end, d => Math.Max(0, byDay.GetValueOrDefault(d)));
                unit = itemId is { } id ? await db.ProcurementItems.Where(i => i.Id == id).Select(i => i.UnitCode).FirstOrDefaultAsync(ct) : "ед. (разные единицы)";
                break;
            }

            default:
                throw new QueryPlanException(QueryErrors.Invalid, metric);
        }

        return (Aggregate(daily, granularity, end), unit, scope);
    }

    private static IReadOnlyList<SeriesPoint> Days(DateOnly from, DateOnly to, Func<DateOnly, decimal> value) =>
        Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).Select(d => new SeriesPoint(d, value(d))).ToList();

    /// <summary>Дни → недели (с понедельника) или месяцы; неполный последний интервал отбрасывается.</summary>
    internal static IReadOnlyList<SeriesPoint> Aggregate(IReadOnlyList<SeriesPoint> daily, string granularity, DateOnly end)
    {
        if (granularity == "day")
        {
            return daily;
        }

        DateOnly Key(DateOnly d) => granularity == "week" ? Monday(d) : new DateOnly(d.Year, d.Month, 1);
        DateOnly Last(DateOnly start) => granularity == "week" ? start.AddDays(6) : start.AddMonths(1).AddDays(-1);
        return daily.GroupBy(p => Key(p.Date))
            .Where(g => Last(g.Key) <= end && g.Min(p => p.Date) == g.Key)
            .Select(g => new SeriesPoint(g.Key, g.Sum(p => p.Value)))
            .OrderBy(p => p.Date)
            .ToList();
    }

    private static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    // ---- снимки и точность ----

    /// <summary>
    /// Фоновая задача: сохранить сегодняшние прогнозы ключевых показателей (продажи по дням на 30 дней, по месяцам на 3) и сверить прошлые
    /// прогнозы с фактом, где факт уже есть. Повтор в тот же день ничего не дублирует (уникальный ключ).
    /// </summary>
    public async Task<(int Saved, int Evaluated)> SnapshotAsync(CancellationToken ct)
    {
        var p = AnalytikPrincipal.SystemPrincipal;
        var today = Today;
        var saved = 0;
        foreach (var (metric, granularity, horizon) in new[] { ("sales.revenue", "day", 30), ("sales.revenue", "month", 3), ("sales.kg", "month", 3) })
        {
            var (history, _, _) = await HistoryAsync(metric, granularity, p, null, ct);
            if (history.Count < 8)
            {
                continue;
            }

            var result = Forecaster.Forecast(history, granularity, horizon);
            var existing = await adb.Forecasts.Where(f => f.Metric == metric && f.Dimension == "" && f.Granularity == granularity && f.MadeOn == today)
                .Select(f => f.TargetDate).ToListAsync(ct);
            foreach (var (point, step) in result.Points.Select((x, i) => (x, i + 1)).Where(x => !existing.Contains(x.x.Date)))
            {
                adb.Forecasts.Add(new AnalytikForecast
                {
                    Metric = metric, Granularity = granularity, Method = result.Method, MadeOn = today, TargetDate = point.Date, Horizon = step,
                    Predicted = point.Value, Lower = point.Lower, Upper = point.Upper,
                });
                saved++;
            }
        }

        var evaluated = 0;
        var due = await adb.Forecasts.Where(f => f.Actual == null && f.TargetDate < today && f.Dimension == "").ToListAsync(ct);
        foreach (var group in due.GroupBy(f => (f.Metric, f.Granularity)))
        {
            var (actual, _, _) = await HistoryAsync(group.Key.Metric, group.Key.Granularity, p, null, ct);
            var byDate = actual.ToDictionary(a => a.Date, a => a.Value);
            foreach (var f in group)
            {
                if (byDate.TryGetValue(f.TargetDate, out var value))
                {
                    f.Actual = value;
                    f.EvaluatedAt = clock.GetUtcNow();
                    evaluated++;
                }
            }
        }

        await adb.SaveChangesAsync(ct);
        return (saved, evaluated);
    }

    public async Task<ForecastAccuracy?> AccuracyAsync(string metric, string dimension, string granularity, CancellationToken ct)
    {
        var rows = await adb.Forecasts.AsNoTracking()
            .Where(f => f.Metric == metric && f.Dimension == dimension && f.Granularity == granularity && f.Actual != null)
            .OrderByDescending(f => f.TargetDate).Take(400).ToListAsync(ct);
        if (rows.Count == 0)
        {
            return null;
        }

        var actual = rows.Sum(f => Math.Abs(f.Actual!.Value));
        return new ForecastAccuracy(rows.Count, actual == 0 ? null : Math.Round(rows.Sum(f => Math.Abs(f.Actual!.Value - f.Predicted)) / actual, 4),
            rows.Min(f => f.TargetDate), rows.Max(f => f.TargetDate));
    }
}

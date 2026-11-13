using OneBase.Application.Analytik.Forecasting;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Analytik.Supply;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Математика Аналитика без базы: прогноз (тренд, сезонность, диапазон, выбор метода), отклонения, расчёт закупки (MRP).</summary>
public sealed class AnalytikMathTests
{
    private static readonly DateOnly Start = new(2026, 1, 5);

    private static List<SeriesPoint> Daily(int days, Func<int, double> value) =>
        Enumerable.Range(0, days).Select(i => new SeriesPoint(Start.AddDays(i), (decimal)value(i))).ToList();

    [Fact]
    public void Forecast_recovers_weekly_seasonality_with_trend()
    {
        // Будни 100 + рост 1 в день, суббота 40, воскресенье 20 — 16 недель.
        double Value(int i) => (i % 7) switch { 5 => 40, 6 => 20, _ => 100 } + i;
        var history = Daily(112, Value);
        var result = Forecaster.Forecast(history, "day", 14);

        Assert.Contains(result.Method, new[] { Forecaster.HoltWinters, Forecaster.SeasonalNaive });
        Assert.Equal(7, result.Seasonality);
        Assert.Equal(14, result.Points.Count);
        Assert.True(result.Wape < 0.05m, $"WAPE {result.Wape}");
        Assert.True(result.Reliable);
        for (var k = 0; k < 14; k++)
        {
            var actual = Value(112 + k);
            var p = result.Points[k];
            Assert.Equal(Start.AddDays(112 + k), p.Date);
            Assert.True(Math.Abs((double)p.Value - actual) < actual * 0.12, $"{p.Date}: {p.Value} против {actual}");
            Assert.True(p.Lower <= p.Value && p.Value <= p.Upper);
        }

        // Диапазон растёт с горизонтом.
        Assert.True(result.Points[^1].Upper - result.Points[^1].Lower > result.Points[0].Upper - result.Points[0].Lower);
    }

    [Fact]
    public void Forecast_of_linear_trend_and_nonnegative_bounds()
    {
        var growing = Enumerable.Range(0, 24).Select(i => new SeriesPoint(new DateOnly(2024, 1, 1).AddMonths(i), 1000 + 50 * i)).ToList();
        var result = Forecaster.Forecast(growing, "month", 3);
        Assert.Contains(result.Method, new[] { Forecaster.LinearTrend, Forecaster.Holt });
        Assert.InRange((double)result.Points[0].Value, 2150, 2250);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Points[0].Date);

        var falling = Daily(30, i => Math.Max(0, 50 - 2 * i));
        var down = Forecaster.Forecast(falling, "day", 30);
        Assert.All(down.Points, p => Assert.True(p.Lower >= 0 && p.Value >= 0));
    }

    [Fact]
    public void Short_history_is_marked_unreliable_and_scenarios_scale()
    {
        var result = Forecaster.Forecast(Daily(5, i => 10 + i), "day", 7);
        Assert.False(result.Reliable);
        Assert.Contains(result.Assumptions, a => a.Contains("ориентировочный", StringComparison.Ordinal));
        var plus20 = Forecaster.Scenario(result.Points, 0.2m);
        Assert.Equal(Math.Round(result.Points[0].Value * 1.2m, 2), plus20[0].Value);
        Assert.Throws<ArgumentException>(() => Forecaster.Forecast([], "day", 7));
    }

    [Fact]
    public void Anomaly_compares_same_weekday_and_ignores_normal_weekend_drop()
    {
        double Value(int i) => (i % 7) switch { 5 => 40, 6 => 20, _ => 100 } + (i % 3);
        var normal = Daily(63, Value);
        // Воскресенье с 20 — норма для воскресенья, хотя от будней ×5 меньше.
        Assert.True(Math.Abs(AnomalyMath.Latest(normal, "day")!.Z) < AnomalyMath.Threshold);

        var drop = Daily(64, i => i == 63 ? 30 : Value(i));
        var hit = AnomalyMath.Latest(drop, "day")!;
        Assert.Equal("down", hit.Direction);
        Assert.True(hit.Z <= -AnomalyMath.StrongThreshold);
        Assert.Equal("critical", AnomalyMath.Severity(hit.Z));
        Assert.True(hit.ChangePercent < -60);

        Assert.Single(AnomalyMath.Scan(drop, "day", 14));
        Assert.Null(AnomalyMath.Latest(Daily(3, _ => 1), "day"));
    }

    private static SupplyItemInput Item(decimal onHand, decimal reserved, double perDay, int days = 90, decimal minQty = 0, decimal? price = 12_000m,
        SupplyCommitment[]? committed = null, SupplyIncoming[]? incoming = null, int? leadTime = 10, decimal unusable = 0) =>
        new(Guid.NewGuid(), "RM-1", "Мука", "кг", onHand, reserved, unusable, minQty,
            Enumerable.Range(0, days).Select(i => new SeriesPoint(Today.AddDays(-days + i), (decimal)perDay)).ToList(),
            committed ?? [], incoming ?? [], leadTime, leadTime is null ? null : "поставщик", price, price is null ? null : "последний приход", "ООО Мука");

    private static readonly DateOnly Today = new(2026, 11, 11);

    [Fact]
    public void Procurement_formula_without_double_counting_reserves()
    {
        // 1000 на складе, 200 в резерве заявок → доступно 800; расход 20/день ровно (σ = 0 → страховой = минимум 100).
        var plan = SupplyPlanMath.Plan(Item(1000, 200, 20, minQty: 100), Today, 7);
        Assert.Equal(800, plan.Available);
        Assert.Equal(20, plan.AvgDailyConsumption);
        Assert.Equal(100, plan.SafetyStock);
        Assert.Equal(40, plan.DaysOfCover);

        // 30 дней: 600 + 100 − 800 = 0 (хватает); 60: 1200 + 100 − 800 = 500; 90: 1800 + 100 − 800 = 1100; резерв не вычитается второй раз.
        Assert.Equal(0, plan.Horizon(30)!.Required);
        Assert.Equal(500, plan.Horizon(60)!.Required);
        Assert.Equal(1100, plan.Horizon(90)!.Required);
        Assert.Equal(Today.AddDays(41), plan.ShortageDate);
        Assert.Equal(Today.AddDays(31), plan.OrderBy);
        Assert.Equal(500 * 12_000m, plan.Horizon(60)!.Cost);
        Assert.Equal("low", plan.Risk);
        Assert.True(plan.Deferrable);
    }

    [Fact]
    public void Confirmed_incoming_and_production_orders_change_the_need()
    {
        var withOrders = SupplyPlanMath.Plan(Item(300, 0, 10, committed: [new SupplyCommitment(Today.AddDays(3), 500, "ПЗ-1")]), Today, 7);
        // Задание 500 за 3 дня больше среднего расхода (10 × 7 = 70): спрос на 7 дней = 500, а не 500 + 70.
        Assert.Equal(500, withOrders.Horizon(7)!.Demand);
        Assert.Equal(Today.AddDays(3), withOrders.ShortageDate);
        Assert.Equal("critical", withOrders.Risk);
        Assert.True(withOrders.OrderNow);

        var withDelivery = SupplyPlanMath.Plan(Item(300, 0, 10, committed: [new SupplyCommitment(Today.AddDays(3), 500, "ПЗ-1")],
            incoming: [new SupplyIncoming(400, Today.AddDays(2), "Заказ ЗП-7", false)]), Today, 7);
        Assert.Equal(400, withDelivery.Horizon(7)!.Incoming);
        Assert.Null(withDelivery.ShortageDate is { } d && d <= Today.AddDays(7) ? d : null);
        Assert.True(withDelivery.Horizon(7)!.Required < withOrders.Horizon(7)!.Required);
    }

    [Fact]
    public void Missing_price_and_lead_time_are_not_invented()
    {
        var plan = SupplyPlanMath.Plan(Item(100, 0, 10, price: null, leadTime: null), Today, 7);
        Assert.Null(plan.UnitPrice);
        Assert.All(plan.Horizons, h => Assert.Null(h.Cost));
        Assert.True(plan.LeadTimeAssumed);
        Assert.Equal(7, plan.LeadTimeDays);
        Assert.Contains(plan.Assumptions, a => a.Contains("Цены закупки нет", StringComparison.Ordinal));
        Assert.Contains(plan.Assumptions, a => a.Contains("Срок поставки не задан", StringComparison.Ordinal));
    }

    [Fact]
    public void Unusable_stock_and_no_consumption()
    {
        var expired = SupplyPlanMath.Plan(Item(500, 0, 10, unusable: 500), Today, 7);
        Assert.Equal(0, expired.Available);
        Assert.Equal("critical", expired.Risk);

        var idle = SupplyPlanMath.Plan(Item(500, 0, 0), Today, 7);
        Assert.Equal("none", idle.Risk);
        Assert.All(idle.Horizons, h => Assert.Equal(0, h.Required));
        Assert.Null(idle.ShortageDate);
    }

    [Fact]
    public void Backtest_checks_past_months_with_the_same_procedure_and_skips_months_without_history()
    {
        // С 1 января 2026 будни 100, суббота 40, воскресенье 20 и рост 0,2 в день; до января — нули (данные ещё не загружены). Сегодня 10.06.
        var from = new DateOnly(2025, 10, 1);
        var today = new DateOnly(2026, 6, 10);
        var data = new DateOnly(2026, 1, 1);
        var daily = Enumerable.Range(0, today.DayNumber - from.DayNumber).Select(i => from.AddDays(i)).Select(d => new SeriesPoint(d,
            d < data ? 0 : (decimal)(((int)d.DayOfWeek) switch { 6 => 40, 0 => 20, _ => 100 } + 0.2 * (d.DayNumber - data.DayNumber)))).ToList();

        var result = ForecastBacktest.Run(daily, today);

        // Март, апрель, май — полные месяцы с историей до них; текущий июнь в точность не входит.
        Assert.Equal(new[] { new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 1) }, result.Months.Select(m => m.Month));
        Assert.All(result.Months, m =>
        {
            Assert.False(m.Saved);
            Assert.True(m.Accuracy > 0.95m, $"{m.Month}: точность {m.Accuracy}, прогноз {m.Forecast}, факт {m.Fact}");
        });
        // Факт месяца — сумма дней; прогноз — того, что было известно до 1-го числа.
        var may = result.Months[^1];
        Assert.Equal(daily.Where(p => p.Date.Month == 5 && p.Date.Year == 2026).Sum(p => p.Value), may.Fact);
        // Прошлые прогнозы — с 1 марта по вчера, каждый день один раз.
        Assert.Equal(new DateOnly(2026, 3, 1), result.Past[0].Date);
        Assert.Equal(today.AddDays(-1), result.Past[^1].Date);
        Assert.Equal(result.Past.Count, result.Past.Select(p => p.Date).Distinct().Count());
        Assert.Equal(today.DayNumber - new DateOnly(2026, 3, 1).DayNumber, result.Past.Count);

        // Данные с 15 мая: полных месяцев с историей нет, до июня — 17 дней (порог — 14), июнь — с 1-го.
        var late = daily.Select(p => p.Date < new DateOnly(2026, 5, 15) ? p with { Value = 0 } : p).ToList();
        var short_ = ForecastBacktest.Run(late, today);
        Assert.Empty(short_.Months);
        Assert.Equal(new DateOnly(2026, 6, 1), short_.Past[0].Date);
        Assert.Empty(ForecastBacktest.Run(daily.Select(p => p with { Value = 0 }).ToList(), today).Past);
    }

    [Fact]
    public void Backtest_accuracy_is_one_minus_relative_error_and_never_negative()
    {
        Assert.Equal(0.9421m, ForecastBacktest.Accuracy(11.4m, 12.1m));
        Assert.Equal(0.8814m, ForecastBacktest.Accuracy(13.2m, 11.8m));
        Assert.Equal(0m, ForecastBacktest.Accuracy(30m, 10m));
        Assert.Null(ForecastBacktest.Accuracy(5m, 0m));
    }

    /// <summary>Обычный день: будни 400, воскресенье 200; последний день месяца — ×edgeLast, 1-е число — ×edgeFirst.</summary>
    private static List<SeriesPoint> Edged(DateOnly from, DateOnly to, double edgeLast, double edgeFirst) =>
        Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).Select(d =>
        {
            var v = d.DayOfWeek == DayOfWeek.Sunday ? 200.0 : 400.0;
            v *= MonthEdges.KindOf(d) == MonthEdges.Last ? edgeLast : MonthEdges.KindOf(d) == MonthEdges.First ? edgeFirst : 1;
            return new SeriesPoint(d, (decimal)v);
        }).ToList();

    [Fact]
    public void Month_edges_are_found_and_returned_to_the_forecast()
    {
        // Полгода истории до 30.09 (последний день — всплеск конца месяца), прогноз на октябрь — с 1-го числа.
        var history = Edged(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30), 2.0, 0.6);
        var result = Forecaster.Forecast(history, "day", 31);

        var last = Assert.Single(result.Calendar, e => e.Kind == MonthEdges.Last);
        var first = Assert.Single(result.Calendar, e => e.Kind == MonthEdges.First);
        Assert.InRange(last.Factor, 1.9, 2.1);
        Assert.InRange(first.Factor, 0.55, 0.65);
        Assert.Contains(result.Assumptions, a => a.StartsWith("Учтён конец месяца"));

        // Октябрь целиком близок к правде: всплеск 30.09 не размножен на весь месяц.
        var truth = Edged(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 2.0, 0.6).Sum(p => p.Value);
        Assert.InRange((double)(result.Total / truth), 0.95, 1.05);
        // 31.10 — снова всплеск, 01.10 — провал; обычная пятница — как обычно; диапазон у края шире.
        var byDate = result.Points.ToDictionary(p => p.Date);
        var friday = byDate[new DateOnly(2026, 10, 16)];
        Assert.InRange((double)(byDate[new DateOnly(2026, 10, 31)].Value / friday.Value), 1.8, 2.2);
        Assert.InRange((double)(byDate[new DateOnly(2026, 10, 1)].Value / friday.Value), 0.5, 0.7);
        Assert.True(byDate[new DateOnly(2026, 10, 31)].Upper - byDate[new DateOnly(2026, 10, 31)].Value > friday.Upper - friday.Value);
        Assert.True(result.Wape < 0.05m, $"WAPE {result.Wape}");
    }

    [Fact]
    public void Month_edges_are_not_applied_without_a_steady_effect()
    {
        // Без края месяца — как раньше.
        Assert.Empty(Forecaster.Forecast(Edged(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30), 1.0, 1.0), "day", 30).Calendar);
        // Край то вверх, то вниз (меньше трёх четвертей месяцев в одну сторону) — не эффект.
        var noisy = Edged(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30), 1.0, 1.0)
            .Select(p => MonthEdges.KindOf(p.Date) == MonthEdges.Last ? p with { Value = p.Value * (p.Date.Month % 2 == 0 ? 2.0m : 0.5m) } : p).ToList();
        Assert.Empty(Forecaster.Forecast(noisy, "day", 30).Calendar);
        // Двух месяцев мало.
        Assert.Empty(Forecaster.Forecast(Edged(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30), 2.0, 0.6), "day", 30).Calendar);
        // Недели и месяцы — без края месяца.
        var weekly = Enumerable.Range(0, 40).Select(i => new SeriesPoint(new DateOnly(2026, 1, 5).AddDays(7 * i), 1000 + i)).ToList();
        Assert.Empty(Forecaster.Forecast(weekly, "week", 5).Calendar);
    }

    [Fact]
    public void Backtest_from_a_month_end_spike_no_longer_repeats_it_for_the_whole_month()
    {
        // История с краем месяца ×2: прогноз на 1-е число (последняя точка — всплеск) близок к факту месяца.
        var from = new DateOnly(2026, 1, 1);
        var today = new DateOnly(2026, 11, 13);
        var daily = Edged(from, today.AddDays(-1), 2.0, 0.6);
        var result = ForecastBacktest.Run(daily, today);
        Assert.Equal(3, result.Months.Count);
        Assert.All(result.Months, m => Assert.True(m.Accuracy > 0.95m, $"{m.Month}: {m.Accuracy}"));
    }
}

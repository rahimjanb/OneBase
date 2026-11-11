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
}

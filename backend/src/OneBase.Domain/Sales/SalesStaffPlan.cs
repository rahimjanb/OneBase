namespace OneBase.Domain.Sales;

/// <summary>
/// План и факт агента по KPI-показателю из Linko (staff_balance — «пересчёт»).
/// Зарплата и бонусы не хранятся: для аналитики продаж они не нужны.
/// </summary>
public class SalesStaffPlan
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Year { get; set; }
    public int Month { get; set; }
    public long LinkoUserId { get; set; }

    public long IndicatorId { get; set; }
    public required string IndicatorName { get; set; }

    /// <summary>product_sales_weight — план в кг; active_client_count — план АКБ; и другие типы Linko.</summary>
    public required string PlanType { get; set; }

    public int Level { get; set; }
    public decimal PlanAmount { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal FactAmount { get; set; }
    public decimal PlanForecast { get; set; }
    public decimal? FactPercent { get; set; }
    public decimal? ForecastPercent { get; set; }

    public DateTimeOffset LoadedAt { get; set; }
}

public static class StaffPlanTypes
{
    public const string SalesWeight = "product_sales_weight";
    public const string ActiveClients = "active_client_count";
}

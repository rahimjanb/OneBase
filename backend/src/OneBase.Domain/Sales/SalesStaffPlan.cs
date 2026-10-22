namespace OneBase.Domain.Sales;

/// <summary>
/// План и факт агента по KPI-показателю из Linko (staff_balance — «пересчёт»). Оклад и начисленная премия — для раздела «KPI»
/// (видны только с правом finance.read); контакты сотрудников не хранятся.
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

    /// <summary>Оклад сотрудника за месяц (salary_amount; одинаков во всех его показателях).</summary>
    public decimal SalaryAmount { get; set; }

    /// <summary>Премия по показателю, начисленная Linko на момент пересчёта (bonus_amount).</summary>
    public decimal BonusAmount { get; set; }

    public DateTimeOffset LoadedAt { get; set; }
}

/// <summary>
/// KPI-показатель Linko (API планов, performance_indicator): тип плана и уровни премии. Порог и «Бонус 100%» раздела «KPI» — первый
/// уровень с ненулевым бонусом.
/// </summary>
public class SalesStaffIndicator
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string PlanType { get; set; }
    public bool ForSupervisor { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Уровни (jsonb): [{ "from": 80, "to": 100, "bonus": 1770000, "type": "percent_from_bonus_amount" }], проценты — как в Linko.</summary>
    public required string Levels { get; set; }

    public DateTimeOffset LoadedAt { get; set; }
}

public static class StaffPlanTypes
{
    public const string SalesWeight = "product_sales_weight";
    public const string ActiveClients = "active_client_count";

    /// <summary>План по выручке, сум.</summary>
    public const string SalesSum = "sales_sum";

    /// <summary>План в штуках по группе товаров (не деньги).</summary>
    public const string SalesPieces = "product_sales_amount";
}

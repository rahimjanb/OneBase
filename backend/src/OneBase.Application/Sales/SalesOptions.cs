namespace OneBase.Application.Sales;

public enum SaleDateField
{
    /// <summary>Дата создания заказа (created_date).</summary>
    Created,

    /// <summary>Дата доставки (date_delivery).</summary>
    Delivery,
}

/// <summary>Настройки аналитики продаж (секция "Sales" в appsettings).</summary>
public sealed class SalesOptions
{
    public const string Section = "Sales";

    /// <summary>Статусы заказов, которые считаются продажей.</summary>
    public string[] SoldStatuses { get; set; } = ["delivered"];

    /// <summary>Статусы возвратов, которые вычитаются из продаж.</summary>
    public string[] ReturnStatuses { get; set; } = ["delivered"];

    public SaleDateField DateField { get; set; } = SaleDateField.Created;

    public SalesSyncOptions Sync { get; set; } = new();

    /// <summary>
    /// Должности Linko, чьи планы из staff_balance не считаются планом ТП: у супервайзеров план — это план их команды,
    /// и при суммировании он задвоил бы планы агентов. Сравнение — «содержит», без учёта регистра.
    /// </summary>
    public string[] StaffPlanExcludeJobs { get; set; } = ["Супервайзер", "Supervisor"];

    public FlagThresholds Flags { get; set; } = new();
}

public sealed class SalesSyncOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 20;

    /// <summary>Сколько месяцев истории загружать при первой синхронизации.</summary>
    public int BackfillMonths { get; set; } = 12;
}

/// <summary>Пороги флагов «Проблемных агентов». Доли — от 0 до 1.</summary>
public sealed class FlagThresholds
{
    /// <summary>Минимум визитов за месяц, чтобы агента оценивать.</summary>
    public int MinVisits { get; set; } = 20;

    public decimal ConversionCritical { get; set; } = 0.15m;
    public decimal ConversionCriticalOfMedian { get; set; } = 0.50m;
    public decimal ConversionRisk { get; set; } = 0.25m;
    public decimal ConversionRiskOfMedian { get; set; } = 0.70m;

    /// <summary>«Ходит, но не продаёт» (риск): сумма с визита ниже этой доли медианы.</summary>
    public decimal SumPerVisitRiskOfMedian { get; set; } = 0.40m;

    /// <summary>«Мелкий чек» (риск): средний чек ниже этой доли медианы...</summary>
    public decimal SmallCheckOfMedian { get; set; } = 0.60m;

    /// <summary>...при конверсии не ниже этой...</summary>
    public decimal SmallCheckMinConversion { get; set; } = 0.25m;

    /// <summary>...и не меньше этого числа заказов.</summary>
    public int SmallCheckMinOrders { get; set; } = 20;

    /// <summary>«Узкий ассортимент» (критично): категорий не больше этого числа. Риск — на одну меньше цели.</summary>
    public int NarrowAssortmentCritical { get; set; } = 2;

    public decimal TempoCritical { get; set; } = 0.75m;
    public decimal TempoRisk { get; set; } = 0.90m;
}

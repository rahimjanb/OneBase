using OneBase.Domain.Common;

namespace OneBase.Domain.Sales;

public enum DirectionKind
{
    /// <summary>Региональный менеджер.</summary>
    RegionalManager,

    /// <summary>Отдельный канал (Базар, ключевые клиенты).</summary>
    Channel,
}

/// <summary>Направление продаж: РМ или канал. Объединяет регионы.</summary>
public class SalesDirection : Entity
{
    public required string Name { get; set; }
    public DirectionKind Kind { get; set; }
    public string? ManagerName { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public List<SalesRegion> Regions { get; set; } = [];
}

/// <summary>Регион = branch в Linko, плюс данные, которых в Linko нет.</summary>
public class SalesRegion : Entity
{
    public long LinkoBranchId { get; set; }
    public required string Name { get; set; }

    public Guid? DirectionId { get; set; }
    public SalesDirection? Direction { get; set; }

    public string? SupervisorName { get; set; }
    public string? DealerName { get; set; }
}

/// <summary>Торговый представитель в оргструктуре OneBase.</summary>
public class SalesAgentProfile
{
    public long LinkoUserId { get; set; }

    /// <summary>Регион агента. Если не задан — определяется по его заказам.</summary>
    public Guid? RegionId { get; set; }
    public SalesRegion? Region { get; set; }

    /// <summary>Вакансия: не участвует в рейтинге и медианах.</summary>
    public bool IsVacancy { get; set; }
    public string? Note { get; set; }
}

public enum PlanKind
{
    /// <summary>План РОП.</summary>
    Rop,

    /// <summary>План завода.</summary>
    Factory,
}

/// <summary>План региона, кг за месяц. CategoryId = null — план без разбивки по категориям.</summary>
public class SalesRegionPlan : Entity
{
    public Guid RegionId { get; set; }
    public SalesRegion Region { get; set; } = null!;
    public PlanKind Kind { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public long? CategoryId { get; set; }
    public decimal PlanKg { get; set; }
}

/// <summary>План торгового представителя, кг за месяц.</summary>
public class SalesAgentPlan : Entity
{
    public long LinkoUserId { get; set; }
    public PlanKind Kind { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public long? CategoryId { get; set; }
    public decimal PlanKg { get; set; }
}

/// <summary>Редактируемые цели (конверсия, выручка на ТТ и т.д.).</summary>
public class SalesTarget
{
    public required string Key { get; set; }
    public decimal Value { get; set; }
}

public static class SalesTargetKeys
{
    public const string VisitConversion = "visit_conversion";
    public const string RevenuePerOutlet = "revenue_per_outlet";
    public const string AkbPerAgent = "akb_per_agent";
    public const string CategoriesPerOutlet = "categories_per_outlet";

    public static readonly IReadOnlyDictionary<string, decimal> Defaults = new Dictionary<string, decimal>
    {
        [VisitConversion] = 0.70m,
        [RevenuePerOutlet] = 1_439_000m,
        [AkbPerAgent] = 125m,
        [CategoriesPerOutlet] = 4m,
    };
}

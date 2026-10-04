namespace OneBase.Domain.Field;

/// <summary>Приоритет точки, задачи и рекомендации.</summary>
public enum FieldPriority
{
    Low,
    Medium,
    High,
    Urgent,
}

/// <summary>Состояние точки в Sales Base (Linko своего статуса точки не отдаёт).</summary>
public enum FieldCustomerStatus
{
    Active,
    Problem,
    Inactive,
}

/// <summary>
/// Оверлей торговой точки Linko (linko.Markets): только то, чего в Linko нет. Ключ — id точки Linko, без внешнего ключа.
/// Название, адрес, координаты, филиал и ответственный агент — из Linko.
/// </summary>
public class FieldCustomer
{
    public long MarketId { get; set; }

    /// <summary>Агент Sales Base; null — ответственный агент из Linko (Markets.ResponsibleAgentId).</summary>
    public Guid? AssignedAgentId { get; set; }

    public FieldPriority Priority { get; set; } = FieldPriority.Medium;
    public FieldCustomerStatus Status { get; set; } = FieldCustomerStatus.Active;

    /// <summary>План продаж точки на месяц, сум.</summary>
    public decimal? MonthlyTarget { get; set; }

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Note { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Витрина по точке: последние визит и продажа, продажи за 7/30/90 дней и прошлые 30 дней (для тренда).
/// Пересчитывается после синхронизации Linko — списки точек сортируются и фильтруются по ней на сервере.
/// </summary>
public class FieldCustomerStats
{
    public long MarketId { get; set; }
    public DateOnly? LastOrderDate { get; set; }
    public DateOnly? LastVisitDate { get; set; }
    public decimal Sales7 { get; set; }
    public decimal Sales30 { get; set; }
    public decimal Sales90 { get; set; }

    /// <summary>Продажи за 30 дней до последних 30 (31–60 дней назад).</summary>
    public decimal SalesPrev30 { get; set; }

    public decimal Kg30 { get; set; }
    public int Orders30 { get; set; }
    public int Visits30 { get; set; }
    public DateTimeOffset RefreshedAt { get; set; }
}

namespace OneBase.Domain.Sales;

// Зеркало данных Linko SFA. Первичный ключ — id записи в Linko (LinkoId), upsert по нему.

public class LinkoUser
{
    public long Id { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? SecondName { get; set; }
    public bool IsActive { get; set; }
    public long? JobId { get; set; }
    public string? JobName { get; set; }
    public long? PositionId { get; set; }
    public string? PositionName { get; set; }

    public string DisplayName => string.Join(' ', new[] { FirstName, SecondName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class LinkoMarket
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public long? MarketTypeId { get; set; }
    public string? MarketTypeName { get; set; }
    public long? ResponsibleAgentId { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? Address { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public decimal Tm { get; set; }
}

public class LinkoProduct
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public string? Code { get; set; }
    public long? TypeId { get; set; }
    public bool IsWeighted { get; set; }
    public decimal Tm { get; set; }
}

public class LinkoProductType
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public long? ParentId { get; set; }
    public decimal Tm { get; set; }
}

public class LinkoBorder
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public long? ParentId { get; set; }
    public bool IsDelete { get; set; }
    public decimal Tm { get; set; }
}

public class LinkoMarketUser
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long MarketId { get; set; }
    public bool IsDelete { get; set; }
}

public class LinkoOrder
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateOnly CreatedDate { get; set; }

    /// <summary>Плановая дата доставки (date_delivery). Для отчёта не годится: на воскресенье назначают, а принимают в понедельник.</summary>
    public DateOnly? DeliveryDate { get; set; }

    /// <summary>
    /// Момент приёмки товара магазином (accepted_time) — дата реализации. Linko отдаёт его без часового пояса,
    /// по местному времени (Ташкент, UTC+5): у заказов, не менявшихся после приёмки, accepted_time − tm ровно +5 ч.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>Дата приёмки — по ней заказ попадает в факт продаж.</summary>
    public DateOnly? AcceptedDate { get; set; }

    public required string Status { get; set; }
    public long? MarketId { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public long? AgentId { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal DiscountPrice { get; set; }
    public bool IsFullReturn { get; set; }
    public string? Currency { get; set; }
    public decimal Tm { get; set; }

    public List<LinkoOrderLine> Lines { get; set; } = [];
}

public class LinkoOrderLine
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long? ProductId { get; set; }
    public decimal Amount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal Price { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal TotalWeight { get; set; }
}

public class LinkoOrderReturn
{
    public long Id { get; set; }
    public DateOnly CreatedDate { get; set; }
    public required string Status { get; set; }
    public long? MarketId { get; set; }
    public long? AgentId { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal Tm { get; set; }

    public List<LinkoOrderReturnLine> Lines { get; set; } = [];
}

public class LinkoOrderReturnLine
{
    public long Id { get; set; }
    public long ReturnId { get; set; }
    public long? OrderId { get; set; }
    public long? ProductId { get; set; }
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public decimal TotalWeight { get; set; }
}

public class LinkoVisit
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public DateOnly Day { get; set; }
    public required string Status { get; set; }
    public bool IsInPlan { get; set; }
    public long? MarketId { get; set; }
    public long? UserId { get; set; }
}

public class LinkoKpiPlan
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Plan { get; set; }
    public long? IndicatorId { get; set; }
    public string? IndicatorName { get; set; }
}

/// <summary>Состояние синхронизации сущности: курсор last_tm и результат последнего запуска.</summary>
public class LinkoSyncState
{
    public required string Entity { get; set; }
    public decimal? LastTm { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public string? LastError { get; set; }
    public int LastRows { get; set; }
}

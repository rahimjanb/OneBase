using OneBase.Domain.Common;

namespace OneBase.Domain.Field;

public enum FieldRouteStatus
{
    Planned,
    InProgress,
    Completed,
    Cancelled,
}

/// <summary>Откуда маршрут: вручную, из плана визитов Linko, от AI-планирования.</summary>
public enum FieldRouteSource
{
    Manual,
    Linko,
    Ai,
}

public enum FieldPointStatus
{
    Planned,
    InProgress,
    Visited,
    Skipped,
    Cancelled,
}

/// <summary>Маршрут агента на день. Один маршрут на агента и дату.</summary>
public class FieldRoute : Entity
{
    public DateOnly Date { get; set; }
    public Guid AgentId { get; set; }
    public FieldMember? Agent { get; set; }
    public Guid? SupervisorId { get; set; }

    public FieldRouteStatus Status { get; set; } = FieldRouteStatus.Planned;
    public FieldRouteSource Source { get; set; } = FieldRouteSource.Manual;

    /// <summary>Длина маршрута по прямой между точками × коэффициент дороги, км.</summary>
    public decimal DistanceKm { get; set; }

    /// <summary>Дорога + время на визиты, минут.</summary>
    public int EstimatedMinutes { get; set; }

    public Guid? CreatedById { get; set; }
    public List<FieldRoutePoint> Points { get; set; } = [];
}

public class FieldRoutePoint
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RouteId { get; set; }
    public FieldRoute? Route { get; set; }

    /// <summary>Точка Linko (linko.Markets.Id).</summary>
    public long MarketId { get; set; }

    public int Sequence { get; set; }
    public TimeOnly? PlannedTime { get; set; }
    public DateTimeOffset? ActualArrival { get; set; }
    public DateTimeOffset? ActualDeparture { get; set; }
    public FieldPointStatus Status { get; set; } = FieldPointStatus.Planned;
    public Guid? VisitId { get; set; }
    public string? Note { get; set; }
}

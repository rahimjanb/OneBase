using OneBase.Domain.Common;

namespace OneBase.Domain.Field;

public enum FieldVisitStatus
{
    InProgress,
    Completed,
    Cancelled,
}

/// <summary>Чем закончился визит.</summary>
public enum FieldVisitResult
{
    Order,
    Sale,
    Refusal,
    Revisit,
    Closed,
    NoDecisionMaker,
    Other,
}

/// <summary>
/// Проверка геопозиции при начале визита. Плохой GPS работу не блокирует: визит начинается, а отметка видна супервайзеру.
/// </summary>
public enum FieldGeoStatus
{
    /// <summary>В пределах радиуса точки (с поправкой на точность GPS).</summary>
    Ok,

    /// <summary>Дальше радиуса.</summary>
    Far,

    /// <summary>Браузер не отдал координаты (запрет, нет сигнала).</summary>
    NoGps,

    /// <summary>У точки нет координат — сверить не с чем.</summary>
    NoTarget,
}

/// <summary>Визит Sales Base: агент в точке, с координатами, результатом и комментарием.</summary>
public class FieldVisit : Entity
{
    public long MarketId { get; set; }
    public Guid AgentId { get; set; }
    public FieldMember? Agent { get; set; }
    public Guid? SupervisorId { get; set; }
    public Guid? RouteId { get; set; }
    public Guid? RoutePointId { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Точность GPS по данным браузера, м.</summary>
    public double? AccuracyM { get; set; }

    /// <summary>Расстояние от агента до точки при начале визита, м.</summary>
    public double? DistanceM { get; set; }

    public FieldGeoStatus GeoStatus { get; set; } = FieldGeoStatus.NoGps;
    public FieldVisitStatus Status { get; set; } = FieldVisitStatus.InProgress;
    public FieldVisitResult? Result { get; set; }

    /// <summary>Сумма заказа или продажи, если агент её указал.</summary>
    public decimal? Amount { get; set; }

    public string? Comment { get; set; }

    /// <summary>Ключ фото в объектном хранилище (MinIO).</summary>
    public string? PhotoKey { get; set; }

    /// <summary>Совместный выезд, в рамках которого сделан визит.</summary>
    public Guid? JointVisitId { get; set; }
}

public enum FieldJointVisitStatus
{
    Planned,
    Done,
    Cancelled,
}

/// <summary>Совместный выезд: супервайзер + агент или два агента.</summary>
public class FieldJointVisit : Entity
{
    public DateOnly Date { get; set; }
    public TimeOnly? Time { get; set; }

    /// <summary>Точка Linko; null — выезд по территории без конкретной точки.</summary>
    public long? MarketId { get; set; }

    public required string Objective { get; set; }
    public string? Result { get; set; }
    public string? Comment { get; set; }
    public FieldJointVisitStatus Status { get; set; } = FieldJointVisitStatus.Planned;
    public Guid? CreatedById { get; set; }
    public List<FieldJointVisitParticipant> Participants { get; set; } = [];
}

public class FieldJointVisitParticipant
{
    public Guid JointVisitId { get; set; }
    public FieldJointVisit? JointVisit { get; set; }
    public Guid MemberId { get; set; }
    public FieldMember? Member { get; set; }
}

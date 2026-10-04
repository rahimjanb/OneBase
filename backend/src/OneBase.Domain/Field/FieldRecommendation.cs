using OneBase.Domain.Common;

namespace OneBase.Domain.Field;

/// <summary>Правило AI-планирования, которое сработало.</summary>
public enum FieldRecommendationKind
{
    /// <summary>Продажи точки упали относительно прошлых 30 дней.</summary>
    SalesDecline,

    /// <summary>Точка с продажами давно не посещалась.</summary>
    NotVisited,

    /// <summary>Точка перестала покупать.</summary>
    LostCustomer,

    /// <summary>Агент отстаёт от темпа плана.</summary>
    AgentBehindPlan,

    /// <summary>Точка маршрута пропущена — нужен повторный визит.</summary>
    SkippedPoint,

    /// <summary>Точка с высоким потенциалом покупает меньше обычного.</summary>
    HighPotential,

    /// <summary>У точки нет действующего агента (вакансия, уволен, не в Sales Base) — кому её назначить.</summary>
    Reassign,
}

public enum FieldRecommendationStatus
{
    Pending,
    Approved,
    Rejected,
    Expired,
}

/// <summary>
/// Рекомендация AI. План сразу не меняется: РМ или супервайзер подтверждает (можно с правкой) или отклоняет;
/// подтверждённая создаёт задачу. Причина, исходные данные и уверенность сохраняются вместе с решением.
/// </summary>
public class FieldRecommendation : Entity
{
    public FieldRecommendationKind Kind { get; set; }

    /// <summary>Ключ дедупликации: правило + точка + агент. Пока рекомендация ждёт решения, вторая такая же не создаётся.</summary>
    public required string Key { get; set; }

    public required string Title { get; set; }
    public required string Reason { get; set; }

    /// <summary>Исходные цифры, по которым сработало правило (jsonb).</summary>
    public string SourceData { get; set; } = "{}";

    /// <summary>Уверенность 0–1.</summary>
    public decimal Confidence { get; set; }

    public FieldRecommendationStatus Status { get; set; } = FieldRecommendationStatus.Pending;

    /// <summary>Кому предлагается задача (агент) или о ком рекомендация (агент, отстающий от плана).</summary>
    public Guid? AgentId { get; set; }

    /// <summary>Супервайзер, который решает по рекомендации.</summary>
    public Guid? SupervisorId { get; set; }

    public long? MarketId { get; set; }
    public FieldPriority Priority { get; set; } = FieldPriority.Medium;
    public DateOnly? DueDate { get; set; }

    public Guid? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public Guid? TaskId { get; set; }
}

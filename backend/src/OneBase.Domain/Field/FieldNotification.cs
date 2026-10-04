using OneBase.Domain.Common;

namespace OneBase.Domain.Field;

public enum FieldNotificationKind
{
    TaskAssigned,
    TaskOverdue,
    TaskVerified,
    TaskChanged,
    RouteChanged,
    AgentChanged,
    JointVisit,
    Recommendation,
    Problem,
}

/// <summary>Уведомление внутри приложения. Доставка push/SMS позже подключается к тому же событию.</summary>
public class FieldNotification : Entity
{
    public Guid RecipientId { get; set; }
    public FieldNotificationKind Kind { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }

    /// <summary>Ссылка внутри Sales Base (/field/...).</summary>
    public string? Link { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>Настройки Sales Base — одна строка (Id = 1). Меняет РМ.</summary>
public class FieldSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Радиус точки: в его пределах визит считается начатым на месте, м.</summary>
    public int GeoRadiusM { get; set; } = 150;

    /// <summary>Сколько неточности GPS прощается сверх радиуса, м (точность браузера бывает 50–500 м).</summary>
    public int GpsToleranceM { get; set; } = 100;

    /// <summary>Сколько точек ставить в автоматический маршрут.</summary>
    public int MaxRoutePoints { get; set; } = 25;

    public int VisitMinutes { get; set; } = 15;
    public int TravelSpeedKmh { get; set; } = 25;

    /// <summary>Начало рабочего дня — от него считается плановое время точек.</summary>
    public TimeOnly DayStart { get; set; } = new(9, 0);

    /// <summary>Точка с продажами считается давно не посещённой через столько дней.</summary>
    public int NotVisitedDays { get; set; } = 14;

    /// <summary>Падение продаж точки за 30 дней, при котором создаётся рекомендация, %.</summary>
    public int DeclinePct { get; set; } = 30;

    /// <summary>Продажи точки за прошлые 30 дней, ниже которых падение не рассматривается, сум.</summary>
    public decimal DeclineMinSales { get; set; } = 1_000_000;

    /// <summary>Агент отстаёт от темпа плана больше чем на столько процентных пунктов.</summary>
    public int BehindPlanPct { get; set; } = 20;

    /// <summary>Автопланирование: рекомендации подтверждаются сами. По умолчанию выключено.</summary>
    public bool AutoPlanning { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

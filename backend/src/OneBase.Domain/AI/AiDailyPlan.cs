using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

public enum AiDailyPlanStatus
{
    /// <summary>План готовится в фоне.</summary>
    Generating,
    Ready,

    /// <summary>Модель недоступна или ответила не по формату — на экране «Рекомендации временно недоступны».</summary>
    Failed,
}

/// <summary>
/// «План на день от AI» сотрудника: одна строка на пользователя и день (Ташкент). Повторное открытие «Моего дня» отдаёт сохранённый план —
/// модель вызывается при первом открытии за день, по «Обновить» (не чаще раза в 15 минут) и когда задачи изменились (InputHash).
/// </summary>
public class AiDailyPlan : Entity
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public AiDailyPlanStatus Status { get; set; } = AiDailyPlanStatus.Generating;

    /// <summary>Рекомендации (OneBase.AI.Work.DailyPlanItem[]) в JSON.</summary>
    public string? ItemsJson { get; set; }

    /// <summary>Отпечаток данных, по которым составлен план: задачи, сроки, события, рабочий день.</summary>
    public string? InputHash { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? GeneratedAt { get; set; }

    /// <summary>Сколько раз за день план составляла модель (потолок — DailyPlanService.MaxGenerationsPerDay).</summary>
    public int Generations { get; set; }

    public string? Error { get; set; }
}

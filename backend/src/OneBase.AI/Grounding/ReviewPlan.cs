using OneBase.AI.Gateway;

namespace OneBase.AI.Grounding;

/// <summary>
/// Перепроверка одного ответа (режим review): общий счётчик дополнительных попыток — повторы сотрудников и исправления итогового ответа
/// вместе, не больше ReviewMaxAttempts; переход при сложной ошибке синтеза — на модель сложных ответов, один раз и только вверх (Escalation,
/// null — некуда); BudgetLeft — хватает ли бюджета вопроса ещё на один вызов. Потокобезопасен: сотрудники работают параллельно.
/// </summary>
public sealed class ReviewPlan(int attempts, Func<bool>? budgetLeft = null)
{
    private int _left = Math.Max(0, attempts);
    private int _escalated;

    public int Left => Volatile.Read(ref _left);

    /// <summary>Модель сложных ответов для перехода при перепроверке итогового ответа; задаёт консультант, когда выбрана модель ответа.</summary>
    public AiModelChoice? Escalation { get; set; }

    public bool BudgetLeft => budgetLeft?.Invoke() ?? true;

    /// <summary>Взять попытку; false — попытки кончились.</summary>
    public bool TryTake()
    {
        while (true)
        {
            var left = Volatile.Read(ref _left);
            if (left <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _left, left - 1, left) == left)
            {
                return true;
            }
        }
    }

    /// <summary>Переход на модель сложных ответов — не больше одного раза за ответ; false — переход уже был или переходить некуда.</summary>
    public bool TryEscalate() => Escalation is not null && Interlocked.Exchange(ref _escalated, 1) == 0;
}

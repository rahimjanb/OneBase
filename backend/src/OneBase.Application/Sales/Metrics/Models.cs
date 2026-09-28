namespace OneBase.Application.Sales.Metrics;

// Входные данные расчёта — простые записи без EF, чтобы формулы были чистыми и тестируемыми.

/// <summary>
/// Строка продажи: одна позиция заказа (+) или возврата (−) — кг и выручка со знаком.
/// OrderId = null у возвратов: они не считаются заказами.
/// </summary>
public sealed record SaleLine(
    DateOnly Date,
    long? AgentId,
    long? MarketId,
    long? BranchId,
    long? CategoryId,
    long? ProductId,
    decimal Kg,
    decimal Revenue,
    long? OrderId);

public enum VisitStatus
{
    Done,
    Pending,
    Undone,
}

public sealed record VisitRecord(DateOnly Date, long AgentId, long MarketId, VisitStatus Status, bool InPlan);

public sealed record AgentInfo(long Id, string Name, bool IsActive, Guid? ProfileRegionId, bool IsVacancy, bool InDirectory);

public sealed record RegionInfo(Guid Id, long BranchId, string Name, Guid? DirectionId, string? Supervisor, string? Dealer);

public sealed record DirectionInfo(Guid Id, string Name, bool IsChannel, string? ManagerName, string? Description, int SortOrder);

public sealed record MarketInfo(long Id, string Name, long? ResponsibleAgentId, long? BranchId);

/// <summary>Факт кг по месяцам (история для «План и факт по месяцам» и темпа агента).</summary>
public sealed record MonthlyFact(int Year, int Month, long? AgentId, long? BranchId, decimal Kg);

public sealed record PlanRow(Guid? RegionId, long? AgentId, int Month, long? CategoryId, decimal PlanKg);

public sealed record SalesTargets(decimal Conversion, decimal RevenuePerOutlet, decimal AkbPerAgent, decimal CategoriesPerOutlet);

/// <summary>Всё, что нужно для расчёта одного месяца.</summary>
public sealed class MonthData
{
    public required int Year { get; init; }
    public required int Month { get; init; }

    /// <summary>Дата последних данных (для прогноза и «тех же дней»).</summary>
    public required DateOnly DataThrough { get; init; }

    /// <summary>Продажи текущего месяца (с 1-го по DataThrough).</summary>
    public required IReadOnlyList<SaleLine> Current { get; init; }

    /// <summary>Продажи всего прошлого месяца.</summary>
    public required IReadOnlyList<SaleLine> Previous { get; init; }

    /// <summary>Визиты текущего месяца (включая запланированные).</summary>
    public required IReadOnlyList<VisitRecord> Visits { get; init; }

    /// <summary>Факт по месяцам: 12 месяцев до текущего включительно.</summary>
    public required IReadOnlyList<MonthlyFact> History { get; init; }

    /// <summary>Планы на выбранный месяц (регионов и агентов).</summary>
    public required IReadOnlyList<PlanRow> Plans { get; init; }

    /// <summary>Планы регионов на весь год выбранного месяца (для графика по месяцам).</summary>
    public required IReadOnlyList<PlanRow> YearRegionPlans { get; init; }

    public required IReadOnlyDictionary<long, AgentInfo> Agents { get; init; }
    public required IReadOnlyList<RegionInfo> Regions { get; init; }
    public required IReadOnlyList<DirectionInfo> Directions { get; init; }
    public required IReadOnlyDictionary<long, MarketInfo> Markets { get; init; }
    public required IReadOnlyDictionary<long, string> Categories { get; init; }

    /// <summary>ТТ, закреплённые за агентами через market_users (агент → ТТ).</summary>
    public required IReadOnlyList<(long AgentId, long MarketId)> MarketAssignments { get; init; }

    public required SalesTargets Targets { get; init; }
    public required FlagThresholds Thresholds { get; init; }

    public DateOnly MonthStart => new(Year, Month, 1);
    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);
    public int WorkedDays => SalesMath.WorkedDays(MonthStart, DataThrough);
}

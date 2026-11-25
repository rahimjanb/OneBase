using System.Text.Json;
using OneBase.Application.Telegram.Battle;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>Коробки набора: код коробки («35», «140», «200») и сколько коробок — в порядке записи набора («140x8+35x2»).</summary>
public sealed record IncentiveBoxCount(string Box, int Count);

/// <summary>
/// Цель Могул спящей точке (ТЗ v2, 4.2): средний закуп в месяц (кг) и месяцев с покупкой, цель кг (× 1,2), фасовка и кг коробки, коробок (вверх, не
/// меньше порога YM-01), кг, сумма по прайсу, после скидки 7% и кешбек ТП 10% от суммы после скидки.
/// </summary>
public sealed record IncentiveMogulGoal(
    decimal AvgKg,
    int Months,
    decimal TargetKg,
    string Box,
    decimal BoxKg,
    int Boxes,
    decimal Kg,
    decimal Sum,
    decimal AfterDiscount,
    decimal Cashback,
    string? BoxUz = null);

/// <summary>
/// Цель Twizos точке (ТЗ v2, 4.3): сегмент (existing / new / stand) и % кешбека, обычная накладная (действующим), средний чек ТТ, набор
/// («140x1+35x3»), сумма по прайсу, кг, подарок TW-02 (mug / thermos / kettle) и кешбек ТП от суммы набора. Stand — набор со стойкой.
/// </summary>
public sealed record IncentiveTwizosGoal(
    string Segment,
    decimal Percent,
    decimal? AvgInvoice,
    decimal AvgCheck,
    string Set,
    decimal Sum,
    decimal Kg,
    string? Gift,
    decimal Cashback,
    bool Stand)
{
    /// <summary>Коробки набора.</summary>
    public IReadOnlyList<IncentiveBoxCount> Boxes => IncentiveGoalRules.ParseSet(Set);
}

/// <summary>Цели точки маршрута v2 (incentive.RouteTargets.Goals): Могул и / или Twizos; обе null — точка без акции (серая).</summary>
public sealed record IncentiveRouteGoal(IncentiveMogulGoal? Mogul, IncentiveTwizosGoal? Twizos)
{
    public static IncentiveRouteGoal? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<IncentiveRouteGoal>(json, IncentiveSettings.Json);

    public string ToJson() => JsonSerializer.Serialize(this, IncentiveSettings.Json);

    /// <summary>Кешбек, который можно заработать на точке: Могул + Twizos.</summary>
    public decimal Cashback => (Mogul?.Cashback ?? 0) + (Twizos?.Cashback ?? 0);
}

/// <summary>
/// Итог точки по целям v2 за день (ТЗ v2, 4.6): отметка Могул и Twizos (taken ✓ / partial ½ / refused ✗ / ahead — впереди; null — цели нет),
/// коробок Могул по YM-01 и кешбек Могул, сумма и кг Twizos (в лучшей накладной и за день), сегмент и кешбек Twizos по заказам, подарки, стойка.
/// </summary>
public sealed record IncentiveGoalFact(
    string? MogulStatus,
    int MogulBoxes,
    decimal MogulCashback,
    string? TwizosStatus,
    decimal TwizosSum,
    decimal TwizosKg,
    decimal TwizosCashback,
    string? TwizosSegment,
    IReadOnlyList<string> Gifts,
    bool Stand)
{
    public static readonly IncentiveGoalFact None = new(null, 0, 0, null, 0, 0, 0, null, [], false);

    public decimal Cashback => MogulCashback + TwizosCashback;
}

/// <summary>Квота стоек Twizos региона (ТЗ v2, 4.3.1): остаток на складе (null — склада нет), цель (0 → 40), ТП в регионе, квота ТП.</summary>
public sealed record IncentiveStandQuota(string Region, int? Stock, int Target, int Agents, int PerAgent, bool Manual);

/// <summary>Заказ Twizos с кешбеком v2: сумма по прайсу, после возвратов, стойка в заказе, сегмент и % на момент заказа, кешбек, статус.</summary>
public sealed record IncentiveTwizosCashbackOrder(
    long OrderId,
    long AgentId,
    long MarketId,
    DateOnly Created,
    DateTime CreatedAt,
    DateOnly? Delivered,
    decimal ListSum,
    decimal NetSum,
    bool Stand,
    string Segment,
    decimal Percent,
    decimal Cashback,
    decimal NetCashback,
    string Status);

/// <summary>Отметки результата точки — те же, что у «Поля боя» (ahead / taken / partial / refused).</summary>
public static class IncentiveGoalStatus
{
    public const string Ahead = BattleStoreStatus.Ahead;
    public const string Taken = BattleStoreStatus.Taken;
    public const string Partial = BattleStoreStatus.Partial;
    public const string Refused = BattleStoreStatus.Refused;
}

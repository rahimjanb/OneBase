using OneBase.Domain.Common;

namespace OneBase.Domain.Battle;

/// <summary>Вид цели «Поля боя»: Твизос — подключить категорию или взять призовой набор; Могул — довести накладную до порога сетки A–H.</summary>
public static class BattleGoalKind
{
    public const string Twizos = "twizos";
    public const string Mogul = "mogul";
}

/// <summary>Подвид: connect — ТТ без закупа категории (новый АКБ); prize — призовой набор Твизос; volume — объём по акции Могул.</summary>
public static class BattleGoalSub
{
    public const string Connect = "connect";
    public const string Prize = "prize";
    public const string Volume = "volume";
}

public static class BattleGoalStatus
{
    public const string Pending = "pending";
    public const string Taken = "taken";

    /// <summary>Визит или заказ в ТТ есть, цель не взята.</summary>
    public const string Refused = "refused";
}

/// <summary>Откуда маршрут дня: план визитов Linko или собственный маршрут OneBase (у ТП нет плана в Linko).</summary>
public static class BattleRouteSource
{
    public const string Linko = "linko";
    public const string Own = "own";
}

/// <summary>
/// Цель ТП на магазин маршрута на день (ТЗ «Поле боя» v2). Создаётся при первом построении карточки недели по плану визитов Linko и правилам
/// отбора, дальше не пересчитывается: карточки 13:00 и 18:00 оценивают те же цели, а следующая неделя смотрит на невзятые. Статус и факт
/// обновляются при каждой оценке. Market* и Avg* — снимок на момент постановки, чтобы карточка не менялась задним числом.
/// </summary>
public class BattleGoal : Entity
{
    public DateOnly Date { get; set; }

    /// <summary>Понедельник недели цели.</summary>
    public DateOnly Week { get; set; }

    /// <summary>ТП — пользователь Linko.</summary>
    public long AgentId { get; set; }

    public long MarketId { get; set; }
    public required string MarketName { get; set; }

    /// <summary>Порядок магазина в маршруте дня (1 — первый).</summary>
    public int Order { get; set; }

    public required string RouteSource { get; set; }

    public required string Kind { get; set; }
    public required string Sub { get; set; }

    /// <summary>Твизос — уровень набора (300, 400, 600 — тысяч сум); Могул — категория A–H.</summary>
    public required string Level { get; set; }

    /// <summary>Сумма цели (сум, до скидки): набор Твизос или порог 5% Могул.</summary>
    public decimal TargetSum { get; set; }

    /// <summary>Второй порог Могул (8% или 10%); у Твизос — null.</summary>
    public decimal? SecondSum { get; set; }

    public int Percent { get; set; }
    public int? SecondPercent { get; set; }

    /// <summary>Код товара для картинки (Твизос — вкус, который предлагаем первым).</summary>
    public string? ProductCode { get; set; }

    /// <summary>Средний закуп ТТ, сум/мес, на момент постановки (по категории цели — у Могул средний закуп Могул).</summary>
    public decimal AvgMonth { get; set; }

    public string Status { get; set; } = BattleGoalStatus.Pending;

    /// <summary>Сумма накладной, закрывшей цель (до скидки), и её номер в Linko.</summary>
    public decimal? TakenSum { get; set; }
    public long? TakenOrderId { get; set; }

    /// <summary>Какой порог взят у Могул (5, 8, 10); у Твизос — процент не нужен.</summary>
    public int? TakenPercent { get; set; }

    public DateTimeOffset? EvaluatedAt { get; set; }

    /// <summary>Оценка по доставленным накладным (итог недели): после неё статус не меняется по текущим заказам.</summary>
    public bool Final { get; set; }
}

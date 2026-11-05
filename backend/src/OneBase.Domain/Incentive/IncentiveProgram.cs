using OneBase.Domain.Common;

namespace OneBase.Domain.Incentive;

/// <summary>
/// Накопительная акция для ТП (первая — Twizos, ТЗ «Проект 3» от 27.10.2026): ТП копит кг товаров акции (типы Linko) за период, порог —
/// приз. Ступени, ориентиры месяцев и цели магазинов — JSON (Application: IncentiveSettings); оптовые ТП — отдельный рейтинг. Будущая
/// акция — новая строка с другими товарами, датами и призами: рассылка (автоотчёт «Накопительная акция») та же.
/// </summary>
public class IncentiveProgram : Entity
{
    /// <summary>Код акции: «twizos-2026q4».</summary>
    public required string Code { get; set; }

    /// <summary>«Накопительная акция Twizos».</summary>
    public required string Name { get; set; }

    /// <summary>Товар на карточках: «Twizos».</summary>
    public required string Product { get; set; }

    /// <summary>Типы товаров Linko; подтипы относятся к ним же («Снек» → «Снек 140 г»).</summary>
    public long[] ProductTypeIds { get; set; } = [];

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Ступени: порог, кг; приз; коротко; картинка (tablet, tv, phone, gift) — JSON-массив.</summary>
    public string TiersJson { get; set; } = "[]";

    /// <summary>Ориентиры месяцев: доля порога на месяц (октябрь 20%, ноябрь 35%, декабрь 45%) — JSON-массив.</summary>
    public string MonthsJson { get; set; } = "[]";

    /// <summary>Цели магазинов маршрута: набор по среднему закупу ТТ (от суммы), его вес, приз клиенту, уровень набора «Поля боя» — JSON-массив.</summary>
    public string StoreSetsJson { get; set; } = "[]";

    /// <summary>
    /// Магазинов с целью на маршруте дня — не больше (по ТЗ цель ставится каждому магазину маршрута; на образце — 12): сначала магазины с целью
    /// «Поля боя», потом дозаказ и подключение по среднему закупу.
    /// </summary>
    public int MaxStores { get; set; } = 12;

    /// <summary>Оптовые ТП (ID Linko): при SeparateWholesale — свой рейтинг, в рейтинге розницы их нет.</summary>
    public long[] WholesaleAgentIds { get; set; } = [];

    public bool SeparateWholesale { get; set; } = true;

    /// <summary>СВ получает копии карточек своих ТП.</summary>
    public bool SvCopies { get; set; } = true;

    /// <summary>СВ получает сводку команды.</summary>
    public bool SvSummary { get; set; } = true;

    /// <summary>Что получает РМ: summary — сводка по регионам, cards — карточки ТП.</summary>
    public string RmMode { get; set; } = IncentiveRmMode.Summary;

    /// <summary>Заказы Linko не обновились: stamp — отправить последние данные с отметкой времени, skip — не отправлять (в журнал — причина).</summary>
    public string StaleData { get; set; } = IncentiveStaleData.Stamp;

    public bool Enabled { get; set; }

    public Guid? UpdatedById { get; set; }
}

public static class IncentiveRmMode
{
    public const string Summary = "summary";
    public const string Cards = "cards";
}

public static class IncentiveStaleData
{
    public const string Stamp = "stamp";
    public const string Skip = "skip";
}

/// <summary>«Подключить» — ТТ без товара акции за окно закупа; «дозаказ» — уже брала.</summary>
public static class IncentiveTargetSub
{
    public const string Connect = "connect";
    public const string Reorder = "reorder";
}

/// <summary>
/// Цель ТП на магазин маршрута дня в кг (карточка «Маршрут дня»): ставится один раз при первом построении карточки дня и дальше не
/// пересчитывается — 13:00 и 18:00 оценивают то, что ТП видел утром. Где «Поле боя» ставит магазину Твизос-цель, набор — тот же
/// (BattleLevel). Статус не хранится: считается по заказам и визитам дня при каждом построении.
/// </summary>
public class IncentiveRouteTarget : Entity
{
    public Guid ProgramId { get; set; }

    /// <summary>ТП — пользователь Linko.</summary>
    public long AgentId { get; set; }

    public DateOnly Date { get; set; }

    public long MarketId { get; set; }
    public required string MarketName { get; set; }

    /// <summary>Порядок магазина в маршруте дня (1 — первый).</summary>
    public int Order { get; set; }

    /// <summary>linko — план визитов Linko, own — собственный маршрут OneBase (как у «Поля боя»).</summary>
    public required string RouteSource { get; set; }

    public required string Sub { get; set; }

    /// <summary>Набор: «140 г + 200 г».</summary>
    public required string Set { get; set; }

    /// <summary>Вес набора, кг (точный: 7,64; на карточке — 7,6).</summary>
    public decimal TargetKg { get; set; }

    /// <summary>Приз клиенту за набор: «чайник Tefal»; у набора 35 г — нет.</summary>
    public string? Prize { get; set; }

    /// <summary>Уровень набора «Поля боя» (300, 400, 600), если цель взята оттуда.</summary>
    public string? BattleLevel { get; set; }

    /// <summary>Средний закуп ТТ, сум/мес, на момент постановки.</summary>
    public decimal AvgMonth { get; set; }
}

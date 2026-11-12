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

    /// <summary>
    /// Акции ноября вокруг накопительной (ТЗ «Карточки ТК и расчёт целей по акциям ноября», 06.11.2026) — JSON (Application: IncentivePromos):
    /// Yamelly спящим (YM-01 скидка, YM-02 кэшбек), лучший агент дня (TW-01), подарки за набор (TW-02: подарок по сумме накладной, пары, запас
    /// на отказы), приз для расчёта цели (TW-03), территориальный план (YM-03). Пусто — только накопительная, как до ноября.
    /// </summary>
    public string PromosJson { get; set; } = "{}";

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

/// <summary>«Подключить» — ТТ без товара акции за окно закупа; «дозаказ» — уже брала; «без цели» — точка маршрута без набора (серая на карте).</summary>
public static class IncentiveTargetSub
{
    public const string Connect = "connect";
    public const string Reorder = "reorder";
    public const string None = "none";
}

/// <summary>Где точка цели: на маршруте дня или «Добери вне маршрута» (точка ТП с другого дня — звонок или доп. визит).</summary>
public static class IncentivePlacement
{
    public const string Route = "route";
    public const string Extra = "extra";
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

    /// <summary>Наборы цели по кодам настроек через «+»: «T+K140» — термос и кружка (пара = два заказа), «K35» — старт; null — набора нет.</summary>
    public string? Sets { get; set; }

    /// <summary>«+ план»: новому клиенту с крупным чеком сразу пара — маршрут без этого не закрывал норму дня с запасом.</summary>
    public bool PlanAdded { get; set; }

    /// <summary>Точка из списка спящих Yamelly ТП (YM-01): в выноске — предложение Yamelly −7% и кэшбек ТП.</summary>
    public bool Sleeping { get; set; }

    /// <summary>route — маршрут дня, extra — «Добери вне маршрута».</summary>
    public string Placement { get; set; } = IncentivePlacement.Route;

    /// <summary>Средний чек ТТ (сумма дня с заказом до скидки, все категории), сум — по нему выбран набор.</summary>
    public decimal AvgCheck { get; set; }
}

/// <summary>
/// Точка списка спящих (YM-01): снимок по Linko — брала Yamelly в году, но не брала с даты отсечки (01.10). Снимок — на начало акции, пересчёт — по
/// субботам на следующую неделю и вручную во вкладке «Акции». Чья точка — по закреплению Linko (ответственный ТП). Точка, которая больше не
/// спящая, не удаляется, а получает RemovedOn: кэшбек по заказам, пока она была в списке, сохраняется.
/// </summary>
public class IncentiveSleepingMarket : Entity
{
    public Guid ProgramId { get; set; }

    /// <summary>ТТ — ID Linko.</summary>
    public long MarketId { get; set; }

    /// <summary>С какого дня точка в списке (начало акции или день пересчёта).</summary>
    public DateOnly ListedOn { get; set; }

    /// <summary>С какого дня точки нет в списке (пересчёт без неё); null — в списке.</summary>
    public DateOnly? RemovedOn { get; set; }

    /// <summary>Приоритет из внешнего списка, справочно; список строится по Linko — не заполняется, порядок визитов считается по данным Linko.</summary>
    public string? FilePriority { get; set; }

    /// <summary>Кто пересчитал список вручную; null — по расписанию.</summary>
    public Guid? UploadedById { get; set; }
}

/// <summary>
/// Итог дня ТП по акциям (история расчётов): фиксируется при закрытии дня. Победа TW-01 и начисления берутся отсюда — к выплате не меняются
/// от заказов, исправленных задним числом; дни без итога считаются по заказам.
/// </summary>
public class IncentiveDayResult : Entity
{
    public Guid ProgramId { get; set; }
    public long AgentId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Группа TW-01 и порогов TW-03: опт (рынок и опт) или розница.</summary>
    public bool Wholesale { get; set; }

    /// <summary>Кг Twizos в заказах дня (кроме отменённых), включая точки вне маршрута.</summary>
    public decimal TwizosKg { get; set; }

    /// <summary>Из них — точки вне маршрута дня.</summary>
    public decimal OutOfRouteKg { get; set; }

    /// <summary>Наборов с подарком (заказов с суммой Twizos от порога кружки).</summary>
    public int Sets { get; set; }

    public int Mugs { get; set; }
    public int Thermoses { get; set; }
    public int Kettles { get; set; }

    /// <summary>Спящих Yamelly в маршруте дня и разбужено за день (порог YM-01 выполнен).</summary>
    public int SleepingPlanned { get; set; }

    public int SleepingWoken { get; set; }

    /// <summary>Кэшбек YM-02, оформленный за день, сум.</summary>
    public decimal Cashback { get; set; }

    /// <summary>Место в TW-01 своей группы (null — меньше порога) и победа (приз дня).</summary>
    public int? BestPlace { get; set; }

    public bool BestWinner { get; set; }

    public decimal BestPrize { get; set; }

    public DateTimeOffset ClosedAt { get; set; }
}

/// <summary>Выплата ТП: cashback — кэшбек YM-02, best — призы TW-01.</summary>
public static class IncentivePayoutKind
{
    public const string Cashback = "cashback";
    public const string Best = "best";
}

/// <summary>Отметка бухгалтерии «выплачено» ТП за неделю пн–вс (кэшбек YM-02 или призы TW-01): сумма и день выплаты.</summary>
public class IncentivePayout : Entity
{
    public Guid ProgramId { get; set; }
    public long AgentId { get; set; }

    /// <summary>Понедельник недели, за которую выплата.</summary>
    public DateOnly WeekStart { get; set; }

    public required string Kind { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public string? Note { get; set; }
    public Guid? PaidById { get; set; }
}

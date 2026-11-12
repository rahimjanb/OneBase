namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Карточки накопительной акции: 08:30 маршрут дня, 13:00 полдень, 16:00 промежуточный итог, 18:00 итог дня, суббота — неделя и рейтинг, 1-е
/// число — итог прошлого месяца. Акции ноября: СВР 19:30 — итог дня команды, РМ сб 19:00 — итог недели зоны, пн 09:00 — сводка руководства.
/// </summary>
public static class IncentiveSlot
{
    public const string Morning = "morning";
    public const string Midday = "midday";
    public const string Afternoon = "afternoon";
    public const string Evening = "evening";
    public const string Week = "week";
    public const string Month = "month";
    public const string Rating = "rating";
    public const string Team = "team";
    public const string Zone = "zone";
    public const string Summary = "summary";

    /// <summary>Карточка дня — с маршрутом и магазинами.</summary>
    public static bool IsDay(string slot) => slot is Morning or Midday or Afternoon or Evening;

    /// <summary>Промежуточный срез дня (13:00, 16:00): заказы — до времени запуска.</summary>
    public static bool IsInterim(string slot) => slot is Midday or Afternoon;

    /// <summary>Карточки руководителей по акциям ноября: команда СВР, зона РМ, сводка.</summary>
    public static bool IsManagers(string slot) => slot is Team or Zone or Summary;
}

/// <summary>
/// ТП в итоге дня команды (СВР 19:30): спящие в маршруте и разбужено, кг Twizos, цель маршрута (или норма), наборов с подарком, кэшбек дня,
/// отказов, ход к призам (с завтрашнего дня), место и победа TW-01 за день.
/// </summary>
public sealed record IncentiveTeamDayRow(IncentiveAgent Agent, int SleepingPlanned, int SleepingWoken, decimal Kg, decimal? Goal, int Sets, decimal Cashback, int Refused,
    IncentiveProgress Progress, decimal Total, int? BestPlace, bool BestWinner, bool Wholesale)
{
    /// <summary>✓ — цель Twizos дня взята, × — ни кг, ни спящих, ½ — остальное.</summary>
    public string Status => Goal is > 0 && Kg >= Goal ? Battle.BattleStoreStatus.Taken
        : Kg <= 0 && SleepingWoken == 0 ? Battle.BattleStoreStatus.Refused
        : Battle.BattleStoreStatus.Partial;
}

/// <summary>Итог дня команды СВР: ТП по результату, подарки команды по видам.</summary>
public sealed record IncentiveTeamDay(string Title, IReadOnlyList<IncentiveTeamDayRow> Rows, IReadOnlyDictionary<string, int> Gifts)
{
    public int SleepingPlanned => Rows.Sum(r => r.SleepingPlanned);
    public int SleepingWoken => Rows.Sum(r => r.SleepingWoken);
    public decimal Kg => Rows.Sum(r => r.Kg);
    public int Sets => Rows.Sum(r => r.Sets);
    public decimal Cashback => Rows.Sum(r => r.Cashback);
    public int Refused => Rows.Sum(r => r.Refused);
}

/// <summary>
/// Регион в итоге недели зоны или сводке: СВ, спящих в списке и разбужено (с начала акции), кг Twizos и наборов с подарком за неделю, выдано
/// подарков за неделю, остаток подарков на складах филиалов региона, побед TW-01 за неделю, норма недели региона (сумма норм ТП), накоплено.
/// </summary>
public sealed record IncentiveZoneRegion(string Name, string? Supervisor, int Agents, int Listed, int Woken, decimal WeekKg, int WeekSets,
    IReadOnlyDictionary<string, int> Gifts, IReadOnlyDictionary<string, decimal> Stock, int BestWins, decimal? WeekNorm, decimal Total)
{
    public decimal? WokenShare => Listed > 0 ? (decimal)Woken / Listed : null;
    public decimal? WeekShare => WeekNorm is > 0 ? WeekKg / WeekNorm.Value : null;
}

/// <summary>Расход бюджета акций (сводка руководства): кэшбек YM-02 и призы TW-01 начислено, выплачено и бюджет; подарки выдано и выделено.</summary>
public sealed record IncentiveBudgetView(decimal CashbackAccrued, decimal CashbackPaid, decimal CashbackPlan, decimal BestAccrued, decimal BestPaid, decimal BestPlan,
    IReadOnlyDictionary<string, int> GiftsGiven, IReadOnlyDictionary<string, int> GiftsAllotted);

/// <summary>
/// Итог недели зоны (РМ, сб 19:00) или сводка руководства (пн 09:00, за прошлую неделю): регионы, к выплате ТП (кэшбек за подтверждённые
/// доставки и призы TW-01), лидер зоны на дороге к призам; у сводки — территории YM-03 и бюджет.
/// </summary>
public sealed record IncentiveZone(string Title, DateOnly From, DateOnly To, IReadOnlyList<IncentiveZoneRegion> Regions, decimal CashbackToPay, decimal BestToPay,
    IncentiveAgent? Leader, decimal LeaderTotal, IncentiveProgress? LeaderProgress, IReadOnlyList<IncentiveTerritoryState>? Territories = null,
    IncentiveBudgetView? Budget = null, decimal TotalKg = 0)
{
    public int Listed => Regions.Sum(r => r.Listed);
    public int Woken => Regions.Sum(r => r.Woken);
    public decimal WeekKg => Regions.Sum(r => r.WeekKg);
    public int WeekSets => Regions.Sum(r => r.WeekSets);
    public decimal? WokenShare => Listed > 0 ? (decimal)Woken / Listed : null;
}

/// <summary>День на «поляне» недели: кг взятых за день заказов и магазины маршрута со статусами (маршрута не было — пусто).</summary>
public sealed record IncentiveWeekDay(DateOnly Date, decimal Kg, IncentiveRoute Route);

/// <summary>
/// Неделя ТП (пн–сб): дни, норма дня на понедельник утром и норма недели (норма дня × рабочие дни недели), лучшие клиенты для дозаказа на
/// следующую неделю (ТЗ, раздел 6).
/// </summary>
public sealed record IncentiveWeekView(DateOnly Monday, DateOnly Saturday, IReadOnlyList<IncentiveWeekDay> Days, decimal? DayNorm, decimal? Norm,
    IReadOnlyList<IncentiveTopMarket>? TopMarkets = null)
{
    public decimal Kg => Days.Sum(d => d.Kg);
}

/// <summary>Неделя месяца на карточке «город героя»: кг (доставлено по дате приёмки, в последний день — с заказами дня) и норма недели.</summary>
public sealed record IncentiveMonthWeek(int Number, DateOnly From, DateOnly To, decimal Kg, decimal? Norm)
{
    public decimal? Share => Norm is > 0 ? Kg / Norm.Value : null;
}

/// <summary>
/// Месяц ТП: кг месяца и прошлого месяца, недели, ориентир месяца (порог ступени × доля месяца — лист «Модель»), последний ли это месяц
/// акции.
/// </summary>
public sealed record IncentiveMonthView(int Year, int Month, decimal Kg, decimal PreviousKg, IReadOnlyList<IncentiveMonthWeek> Weeks, IncentiveTier? GuideTier,
    decimal? Guide, bool Final);

/// <summary>Спящие Yamelly ТП: в списке, разбужено с начала акции, цель «разбудить» (30% списка), визитов к спящим в день, осталось.</summary>
public sealed record IncentiveSleepingView(int Listed, int Woken, int WakeTarget, int VisitsPerDay)
{
    public int Remaining => Math.Max(0, Listed - Woken);
}

/// <summary>
/// TW-01 на карточке: кг Twizos ТП за день, место в своей группе (null — меньше порога), победа, порог группы, лидер группы и его кг, побед за
/// период карточки (неделя, месяц) и приз за победу.
/// </summary>
public sealed record IncentiveBestView(decimal Kg, int? Place, bool Winner, bool Wholesale, decimal MinKg, string? Leader, decimal LeaderKg, int Wins, decimal Prize);

/// <summary>
/// «Как собраны цели на сегодня» (образец 08:30): точек маршрута, пары (брали Twizos), старт (не брали), «+ план», спящие Yamelly, без цели;
/// кг целей, норма дня до приза, норма с запасом, добор вне маршрута (точек и кг).
/// </summary>
public sealed record IncentiveGoalsSummary(int Stores, int Pairs, int Starts, int PlanAdded, int Sleeping, int Plain, decimal GoalsKg, decimal? Norm, decimal? Needed,
    int Extra, decimal ExtraKg);

/// <summary>День на поляне недели или в месяце: кг Twizos, наборов с подарком, спящих в маршруте и разбужено, кэшбек дня, победа TW-01, норма дня.</summary>
public sealed record IncentivePromoDay(DateOnly Date, decimal Kg, int Sets, int SleepingPlanned, int SleepingWoken, decimal Cashback, bool BestWinner, decimal? Norm)
{
    /// <summary>Доля нормы дня (% поляны).</summary>
    public decimal? Share => Norm is > 0 ? Kg / Norm.Value : null;
}

/// <summary>Заработок ТП по акциям за период карточки: кэшбек YM-02 и призы TW-01 — начислено и выплачено.</summary>
public sealed record IncentiveEarnings(decimal CashbackAccrued, decimal CashbackPaid, int BestWins, decimal BestAccrued, decimal BestPaid)
{
    public decimal Accrued => CashbackAccrued + BestAccrued;
    public decimal Paid => CashbackPaid + BestPaid;
    public decimal Left => Math.Max(0, Accrued - Paid);
}

/// <summary>
/// Акции ноября на карточке ТП: спящие и кэшбек YM-02 (баланс по статусам, оформлено сегодня), TW-01, ТОП SKU региона для наборов, как собраны
/// цели дня, дни недели или месяца, наборы и подарки за период, заработок. null — у акции нет акций ноября.
/// </summary>
public sealed record IncentivePromoView(
    IncentiveSleepingView? Sleeping,
    IncentiveCashbackBalance? Cashback,
    decimal CashbackToday,
    IncentiveBestView? Best,
    IReadOnlyList<IncentiveTopSku> TopByAkb,
    IReadOnlyList<IncentiveTopSku> TopByKg,
    IncentiveGoalsSummary? Goals,
    IReadOnlyList<IncentivePromoDay> Days,
    int Sets,
    IReadOnlyDictionary<string, int> Gifts,
    IncentiveEarnings? Earnings);

/// <summary>
/// Личная карточка ТП: накоплено (по месяцам — в Standing), ход к призам на момент карточки и на начало дня (норма дня — не меняется от
/// заказов дня), маршрут дня (08:30, 13:00, 18:00), неделя (суббота), месяц (конец месяца), место в своей группе (розница или опт) и место
/// неделю назад; акции ноября (Promo).
/// </summary>
public sealed record IncentiveAgentCard(
    IncentiveAgent Agent,
    IncentiveStanding Standing,
    IncentiveProgress Progress,
    IncentiveProgress DayStart,
    IncentiveRoute? Route,
    IncentiveWeekView? Week,
    IncentiveMonthView? Month,
    int? Rank,
    int? RankBefore,
    int Ranked,
    IncentivePromoView? Promo = null)
{
    public decimal Total => Standing.Total;
    public decimal Today => Standing.Today;
    public bool Wholesale => Standing.Wholesale;
}

/// <summary>Строка рейтинга: место в области рейтинга, ТП, кг, взятая и ближайшая ступень, сколько до неё.</summary>
public sealed record IncentiveRatingRow(int Place, IncentiveAgent Agent, decimal Kg, IncentiveTier? Reached, IncentiveTier? Next, decimal? ToNext);

/// <summary>Рейтинг акции: розница (ТОП) и опт отдельно; область — вся акция, направление или регион; рабочих дней до конца акции.</summary>
public sealed record IncentiveRating(string? Scope, IReadOnlyList<IncentiveRatingRow> Retail, IReadOnlyList<IncentiveRatingRow> Wholesale, int WorkDaysLeft);

/// <summary>
/// ТП в сводке команды: накоплено, сегодня, цель маршрута дня, ход к призам, темп (кг в рабочий день с начала акции), место. Отстаёт — темп
/// ниже нормы; у порога — от 80% до ближайшей ступени.
/// </summary>
public sealed record IncentiveTeamRow(IncentiveAgent Agent, decimal Total, decimal Today, decimal? RouteKg, IncentiveProgress Progress, decimal Pace, int? Place, bool Wholesale)
{
    public const decimal NearShare = 0.8m;

    public bool Behind => Progress.Norm is { } norm && Pace < norm;
    public bool Near => Progress.Next is { } next && Total >= next.Kg * NearShare;
    public bool Reached => Progress.Reached is not null;
}

/// <summary>Регион сводки: супервайзер и его ТП.</summary>
public sealed record IncentiveTeamRegion(string Name, string? Supervisor, IReadOnlyList<IncentiveTeamRow> Agents)
{
    public decimal Total => Agents.Sum(a => a.Total);
    public decimal Today => Agents.Sum(a => a.Today);
    public decimal? RouteKg => Agents.Any(a => a.RouteKg is not null) ? Agents.Sum(a => a.RouteKg ?? 0) : null;
    public int Behind => Agents.Count(a => a.Behind);
    public int Near => Agents.Count(a => a.Near);
    public int Reached => Agents.Count(a => a.Reached);
}

/// <summary>Сводка команды: СВ — его регион и ТП списком; РМ — регионы направления и лучшие ТП.</summary>
public sealed record IncentiveTeam(string Role, string Title, IReadOnlyList<IncentiveTeamRegion> Regions)
{
    public IReadOnlyList<IncentiveTeamRow> Agents => Regions.SelectMany(r => r.Agents).DistinctBy(a => a.Agent.Id)
        .OrderByDescending(a => a.Total).ThenBy(a => a.Agent.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    public decimal Total => Agents.Sum(a => a.Total);
    public decimal Today => Agents.Sum(a => a.Today);
    public decimal? RouteKg => Agents.Any(a => a.RouteKg is not null) ? Agents.Sum(a => a.RouteKg ?? 0) : null;
    public int Behind => Agents.Count(a => a.Behind);
    public int Near => Agents.Count(a => a.Near);
    public int Reached => Agents.Count(a => a.Reached);
}

/// <summary>
/// Что рисует карточка акции: запуск, день, акция, язык надписей; личные карточки ТП, сводка команды (СВ, РМ), рейтинг; доля дневной цели к
/// полудню (чёрная метка 13:00); свежесть данных Linko и несвежие ли они (отметка «данные на …»).
/// </summary>
public sealed record IncentiveCards(
    string Slot,
    DateOnly Date,
    IncentiveSettings Program,
    string Language,
    IReadOnlyList<IncentiveAgentCard> Agents,
    IncentiveTeam? Team,
    IncentiveRating? Rating,
    decimal MiddayShare,
    DateTimeOffset? DataAsOf,
    bool Stale,
    IncentiveTeamDay? TeamDay = null,
    IncentiveZone? Zone = null);

/// <summary>Регион для сводки команды: название, супервайзер, ТП.</summary>
public sealed record IncentiveTeamRegionSpec(string Name, string? Supervisor, IReadOnlyList<IncentiveAgent> Agents);

/// <summary>Сводка команды для СВ (role sv — один регион) или РМ (rm — регионы направления).</summary>
public sealed record IncentiveTeamSpec(string Role, string Title, IReadOnlyList<IncentiveTeamRegionSpec> Regions);

/// <summary>
/// Запрос карточек: запуск и день (13:00 — заказы до Until), язык; Participants — все участники акции (место считается среди них); Cards — чьи
/// личные карточки; Team — сводка; RatingScope и RatingAgents — область рейтинга (null — вся акция). Persist — ставить ли цели магазинов на
/// день в базу (предпросмотр будущего дня — без записи). DayStart и DayEnd — время утреннего и вечернего запуска (метка 13:00).
/// </summary>
public sealed record IncentiveCardRequest(
    string Slot,
    DateOnly Date,
    string Language,
    IReadOnlyList<IncentiveAgent> Participants,
    IReadOnlyList<IncentiveAgent> Cards,
    TimeOnly? Until = null,
    IncentiveTeamSpec? Team = null,
    string? RatingScope = null,
    IReadOnlyCollection<long>? RatingAgents = null,
    bool Persist = true,
    bool Stale = false,
    TimeOnly? DayStart = null,
    TimeOnly? DayEnd = null);

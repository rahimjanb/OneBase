namespace OneBase.Application.Telegram.Incentive;

/// <summary>Карточки накопительной акции: 08:30 маршрут дня, 13:00 полдень, 18:00 итог дня, суббота — неделя и рейтинг, конец месяца — итог месяца.</summary>
public static class IncentiveSlot
{
    public const string Morning = "morning";
    public const string Midday = "midday";
    public const string Evening = "evening";
    public const string Week = "week";
    public const string Month = "month";
    public const string Rating = "rating";

    /// <summary>Карточка дня — с маршрутом и магазинами.</summary>
    public static bool IsDay(string slot) => slot is Morning or Midday or Evening;
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

/// <summary>
/// Личная карточка ТП: накоплено (по месяцам — в Standing), ход к призам на момент карточки и на начало дня (норма дня — не меняется от
/// заказов дня), маршрут дня (08:30, 13:00, 18:00), неделя (суббота), месяц (конец месяца), место в своей группе (розница или опт) и место
/// неделю назад.
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
    int Ranked)
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
    bool Stale);

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

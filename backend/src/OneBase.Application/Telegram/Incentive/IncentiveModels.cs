using System.Text.Json;
using OneBase.Domain.Incentive;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Ступень акции: порог, кг за период; приз («Планшет Samsung»), коротко («планшет»), картинка (tablet, tv, phone, gift), по-узбекски; Of — «до
/// кого» по-русски («до планшета»; нет — коротко).
/// </summary>
public sealed record IncentiveTier(decimal Kg, string Prize, string Short, string Image, string? PrizeUz = null, string? ShortUz = null, string? Of = null);

/// <summary>Ориентир месяца: доля порога на месяц (октябрь 20%, ноябрь 35%, декабрь 45% — лист «Модель» файла расчёта).</summary>
public sealed record IncentiveMonthShare(int Year, int Month, decimal Share);

/// <summary>
/// Цель магазина маршрута: набор для ТТ со средним закупом от MinAvg (сум/мес), его вес в кг (точный: 140 г × 26 = 3,64), приз клиенту
/// (кружка, термос, чайник Tefal), уровень набора «Поля боя» (300, 400, 600 — по нему берётся набор, если «Поле боя» уже поставило цель).
/// </summary>
public sealed record IncentiveStoreSet(decimal MinAvg, string Contents, decimal Kg, string? Prize = null, string? Level = null, string? ContentsUz = null, string? PrizeUz = null);

/// <summary>Настройки акции из строки incentive.Programs: ступени, ориентиры и цели магазинов разобраны из JSON.</summary>
public sealed record IncentiveSettings(
    Guid Id,
    string Code,
    string Name,
    string Product,
    IReadOnlyList<long> ProductTypeIds,
    DateOnly Start,
    DateOnly End,
    IReadOnlyList<IncentiveTier> Tiers,
    IReadOnlyList<IncentiveMonthShare> Months,
    IReadOnlyList<IncentiveStoreSet> StoreSets,
    IReadOnlySet<long> Wholesale,
    bool SeparateWholesale,
    bool SvCopies,
    bool SvSummary,
    string RmMode,
    string StaleData,
    bool Enabled,
    int MaxStores = IncentiveRules.MaxStores)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IncentiveSettings From(IncentiveProgram p) => new(
        p.Id, p.Code, p.Name, p.Product, p.ProductTypeIds, p.StartDate, p.EndDate,
        Parse<IncentiveTier>(p.TiersJson).OrderBy(t => t.Kg).ToList(),
        Parse<IncentiveMonthShare>(p.MonthsJson),
        Parse<IncentiveStoreSet>(p.StoreSetsJson).OrderByDescending(s => s.MinAvg).ToList(),
        p.WholesaleAgentIds.ToHashSet(), p.SeparateWholesale, p.SvCopies, p.SvSummary, p.RmMode, p.StaleData, p.Enabled, p.MaxStores);

    private static List<T> Parse<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<T>>(json, Json) ?? [];

    public bool Covers(DateOnly day) => day >= Start && day <= End;

    /// <summary>Оптовый ТП в своём рейтинге (при общем зачёте — все в одном).</summary>
    public bool IsWholesale(long agentId) => SeparateWholesale && Wholesale.Contains(agentId);
}

/// <summary>Участник акции: ТП из команды региона.</summary>
public sealed record IncentiveAgent(long Id, string Name, string? Region);

/// <summary>
/// Момент карточки: утро — доставлено по вчера; 13:00 — плюс заказы сегодня до Until; 18:00, неделя, месяц, рейтинг — плюс все заказы сегодня
/// (ТЗ, раздел 6: «Σ кг с 01.10 по вчера; 13:00/18:00 — плюс заказы сегодня»).
/// </summary>
public sealed record IncentiveMoment(DateOnly Date, bool WithToday, TimeOnly? Until = null)
{
    public static IncentiveMoment Morning(DateOnly date) => new(date, false);
    public static IncentiveMoment Day(DateOnly date, TimeOnly? until = null) => new(date, true, until);
}

/// <summary>
/// Накоплено ТП: доставлено (приёмка с начала акции по вчера) − доставленные возвраты по этим заказам + заказы сегодня (по моменту). Days — кг
/// по дням: доставленное нетто — по дате приёмки заказа, заказы сегодня — сегодняшним днём; сумма Days — Total.
/// </summary>
public sealed record IncentiveStanding(IncentiveAgent Agent, bool Wholesale, decimal Delivered, decimal Returned, decimal Today, IReadOnlyDictionary<DateOnly, decimal> Days)
{
    public decimal Total => Delivered - Returned + Today;

    public decimal Month(int year, int month) => Days.Where(d => d.Key.Year == year && d.Key.Month == month).Sum(d => d.Value);

    public decimal Between(DateOnly from, DateOnly to) => Days.Where(d => d.Key >= from && d.Key <= to).Sum(d => d.Value);
}

/// <summary>Накопленное всех участников на момент: место в розничном (или оптовом) рейтинге, свежесть заказов Linko.</summary>
public sealed record IncentiveStandings(IncentiveSettings Program, IncentiveMoment Moment, IReadOnlyList<IncentiveStanding> Agents, DateTimeOffset? OrdersAsOf)
{
    /// <summary>Рейтинг группы: по накопленному, при равенстве — по имени.</summary>
    public IReadOnlyList<IncentiveStanding> Rating(bool wholesale) => IncentiveRules.Rating(Agents.Where(a => a.Wholesale == wholesale));

    public IncentiveStanding? Of(long agentId) => Agents.FirstOrDefault(a => a.Agent.Id == agentId);

    /// <summary>Место ТП в своей группе (1 — первый); null — ТП не участник.</summary>
    public int? Rank(long agentId)
    {
        if (Of(agentId) is not { } me)
        {
            return null;
        }

        var index = Rating(me.Wholesale).ToList().FindIndex(a => a.Agent.Id == agentId);
        return index >= 0 ? index + 1 : null;
    }
}

/// <summary>Сколько осталось до ступени (0 — взята).</summary>
public sealed record IncentiveTierLeft(IncentiveTier Tier, decimal Left)
{
    public bool Reached => Left <= 0;
}

/// <summary>
/// Ход к призам: взятая ступень (один приз — высшая), ближайшая невзятая, остатки до каждой, рабочие дни до конца акции и норма в день до
/// ближайшей невзятой (ТЗ: (порог − накоплено) ÷ рабочие дни пн–сб до 31.12; праздники — справочник «Продаж»); null — все взяты или дней нет.
/// </summary>
public sealed record IncentiveProgress(decimal Total, IncentiveTier? Reached, IncentiveTier? Next, IReadOnlyList<IncentiveTierLeft> Tiers, int WorkDaysLeft, decimal? Norm)
{
    public decimal? ToNext => Next is null ? null : Math.Max(0, Next.Kg - Total);
}

/// <summary>Магазин маршрута дня с целью в кг и итогом: впереди, взят (кг ≥ цели), частично, отказ (визит или заказ без товара акции).</summary>
public sealed record IncentiveStore(
    long MarketId,
    string Name,
    int Order,
    string Sub,
    string Set,
    decimal TargetKg,
    string? Prize,
    string? BattleLevel,
    string Status,
    decimal Kg,
    bool Visited);

/// <summary>Маршрут дня ТП: магазины с целями (порядок маршрута), откуда маршрут, точек в плане визитов Linko на день (0 — плана нет).</summary>
public sealed record IncentiveRoute(long AgentId, DateOnly Date, string? Source, IReadOnlyList<IncentiveStore> Stores, int RoutePoints)
{
    public decimal TargetKg => Stores.Sum(s => s.TargetKg);
    public decimal Kg => Stores.Sum(s => s.Kg);
    public int Passed => Stores.Count(s => s.Status != Battle.BattleStoreStatus.Ahead);
    public int Taken => Stores.Count(s => s.Status == Battle.BattleStoreStatus.Taken);
    public int Partial => Stores.Count(s => s.Status == Battle.BattleStoreStatus.Partial);
    public int Refused => Stores.Count(s => s.Status == Battle.BattleStoreStatus.Refused);
    public int Connected => Stores.Count(s => s.Sub == IncentiveTargetSub.Connect && s.Status == Battle.BattleStoreStatus.Taken);
    public int Reordered => Stores.Count(s => s.Sub == IncentiveTargetSub.Reorder && s.Status == Battle.BattleStoreStatus.Taken);
}

/// <summary>Лучший клиент для дозаказа: ТТ, кг товара акции за 60 дней (доставлено − возвраты) и цель на неделю.</summary>
public sealed record IncentiveTopMarket(long MarketId, string Name, decimal Kg, decimal WeekTarget);

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Строка «Кешбек · баланс» карточек v2 (ТЗ v2, 5): оформлено за период карточки (день, неделя), ждёт доставки, подтверждено (доставлено),
/// выплачено, к выплате, можно ещё (кешбек невзятых целей); null — неизвестно (на карточке «—»).
/// </summary>
public sealed record IncentiveGoalBalance(decimal Issued, decimal Pending, decimal Confirmed, decimal Paid, decimal ToPay, decimal? CanEarn)
{
    public static readonly IncentiveGoalBalance Zero = new(0, 0, 0, 0, 0, 0);

    public IncentiveGoalBalance Add(IncentiveGoalBalance o) =>
        new(Issued + o.Issued, Pending + o.Pending, Confirmed + o.Confirmed, Paid + o.Paid, ToPay + o.ToPay,
            CanEarn is null && o.CanEarn is null ? null : (CanEarn ?? 0) + (o.CanEarn ?? 0));
}

/// <summary>Два отдельных баланса кешбека: Могул (YM-02, 10%) и Twizos (5 / 8 / 10%).</summary>
public sealed record IncentiveGoalBalances(IncentiveGoalBalance Mogul, IncentiveGoalBalance Twizos)
{
    public static readonly IncentiveGoalBalances Zero = new(IncentiveGoalBalance.Zero, IncentiveGoalBalance.Zero);

    public IncentiveGoalBalances Add(IncentiveGoalBalances o) => new(Mogul.Add(o.Mogul), Twizos.Add(o.Twizos));
}

/// <summary>
/// Итоги маршрута дня v2: целей Могул и Twizos, разбужено (YM-01, на маршруте и вне его), клиентов Twizos (заказ Twizos, на маршруте и вне),
/// кг Twizos, кешбек Могул и Twizos за день, кешбек целей (можно заработать) и невзятых целей, подарки, стойки, пройдено точек, отказы.
/// </summary>
public sealed record IncentiveGoalTotals(
    int Stores,
    int MogulGoals,
    int TwizosGoals,
    int Plain,
    int Both,
    int Woken,
    int TwizosClients,
    int TwizosNew,
    decimal TwizosKg,
    decimal MogulCashback,
    decimal TwizosCashback,
    decimal MogulGoalCashback,
    decimal TwizosGoalCashback,
    decimal MogulLeft,
    decimal TwizosLeft,
    IReadOnlyDictionary<string, int> Gifts,
    int Stands,
    int Passed,
    IReadOnlyList<int> Refused,
    IReadOnlyDictionary<string, int> Segments)
{
    /// <summary>Доля взятых целей дня: (разбужено + клиентов Twizos) ÷ (целей Могул + Twizos).</summary>
    public decimal Share => MogulGoals + TwizosGoals == 0 ? 0 : Math.Min(1, (decimal)(Woken + TwizosClients) / (MogulGoals + TwizosGoals));

    public decimal Cashback => MogulCashback + TwizosCashback;

    /// <summary>Итоги маршрута v2 (точки с целями v2 и продажи вне маршрута).</summary>
    public static IncentiveGoalTotals Of(IncentiveRoute route)
    {
        var stores = route.Stores;
        var outside = route.OutOfRoute ?? [];
        var facts = stores.Select(s => s.Fact ?? IncentiveGoalFact.None).Concat(outside.Select(o => o.Fact ?? IncentiveGoalFact.None)).ToList();
        var gifts = facts.SelectMany(f => f.Gifts).GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());
        var segments = stores.Where(s => s.Goal?.Twizos is not null).GroupBy(s => s.Goal!.Twizos!.Segment).ToDictionary(g => g.Key, g => g.Count());
        decimal Left(IncentiveStore s, bool mogul)
        {
            var goal = mogul ? s.Goal?.Mogul?.Cashback : s.Goal?.Twizos?.Cashback;
            var status = mogul ? s.Fact?.MogulStatus : s.Fact?.TwizosStatus;
            var done = mogul ? s.Fact?.MogulCashback ?? 0 : s.Fact?.TwizosCashback ?? 0;
            return goal is not { } g || status == IncentiveGoalStatus.Taken ? 0 : Math.Max(0, g - done);
        }

        return new IncentiveGoalTotals(
            stores.Count,
            stores.Count(s => s.Goal?.Mogul is not null),
            stores.Count(s => s.Goal?.Twizos is not null),
            stores.Count(s => s.Plain),
            stores.Count(s => s.Goal is { Mogul: not null, Twizos: not null }),
            facts.Count(f => f.MogulCashback > 0),
            facts.Count(f => f.TwizosKg > 0),
            facts.Count(f => f.TwizosKg > 0 && f.TwizosSegment is not null and not Domain.Incentive.IncentiveTwizosSegment.Existing),
            facts.Sum(f => f.TwizosKg),
            facts.Sum(f => f.MogulCashback),
            facts.Sum(f => f.TwizosCashback),
            stores.Sum(s => s.Goal?.Mogul?.Cashback ?? 0),
            stores.Sum(s => s.Goal?.Twizos?.Cashback ?? 0),
            stores.Sum(s => Left(s, true)),
            stores.Sum(s => Left(s, false)),
            gifts,
            facts.Count(f => f.Stand),
            stores.Count(s => s.Status != IncentiveGoalStatus.Ahead),
            stores.Where(s => !s.Plain && s.Status == IncentiveGoalStatus.Refused).Select(s => s.Order).ToList(),
            segments);
    }
}

/// <summary>Карточка дня ТП v2 (08:30 / 13:00 / 18:00): маршрут, итоги, балансы двух кешбеков, ТОП SKU региона (35 г и 140 г) для «Что предлагаем».</summary>
public sealed record IncentiveGoalDay(
    IncentiveRoute Route,
    IncentiveGoalTotals Totals,
    IncentiveGoalBalances Balances,
    IReadOnlyList<IncentiveTopSku> Top35,
    IReadOnlyList<IncentiveTopSku> Top140,
    string? Region);

/// <summary>День недели v2: маршрут с итогами.</summary>
public sealed record IncentiveGoalWeekDay(DateOnly Date, IncentiveRoute Route, IncentiveGoalTotals Totals);

/// <summary>Неделя пн–вс в истории кешбека: доставлено (к выплате) Могул и Twizos, выплачено.</summary>
public sealed record IncentiveGoalWeekRow(DateOnly Monday, DateOnly Sunday, decimal Mogul, decimal Twizos, decimal Paid)
{
    public decimal Left => Math.Max(0, Mogul + Twizos - Paid);
}

/// <summary>
/// Итог недели ТП v2 (сб 18:00): 6 полян, балансы (оформлено за неделю), недели выплат, кешбек недели Могул и Twizos, клиентов Twizos и новых,
/// стоек, кг; осталось спящих в списке; последняя ли неделя акций.
/// </summary>
public sealed record IncentiveGoalWeek(
    DateOnly Monday,
    DateOnly Saturday,
    IReadOnlyList<IncentiveGoalWeekDay> Days,
    IncentiveGoalBalances Balances,
    IReadOnlyList<IncentiveGoalWeekRow> Weeks,
    decimal MogulWeek,
    decimal TwizosWeek,
    int Woken,
    int MogulGoals,
    int TwizosClients,
    int TwizosGoals,
    int TwizosNew,
    int Stands,
    decimal TwizosKg,
    int SleepingLeft,
    bool Last);

/// <summary>Неделя Могула в итоге месяца: разбужено точек и кешбек (оформлено).</summary>
public sealed record IncentiveGoalMonthWeek(DateOnly From, DateOnly To, int Woken, decimal Cashback);

/// <summary>Строка «Мой заработок»: начислено, выплачено, остаток.</summary>
public sealed record IncentiveGoalEarn(decimal Accrued, decimal Paid)
{
    public decimal Left => Math.Max(0, Accrued - Paid);
}

/// <summary>
/// Итог месяца ТП v2 (30.11): спящих в списке и разбужено, цель «разбудить»; клиентов Twizos с начала целей v2, новых, стоек, кг; Могул по
/// неделям; заработок — Могул, Twizos, лучший агент дня (побед, по какой день).
/// </summary>
public sealed record IncentiveGoalMonth(
    int Listed,
    int Woken,
    decimal WakeShare,
    DateOnly TwizosFrom,
    int TwizosClients,
    int TwizosNew,
    int Stands,
    decimal TwizosKg,
    IReadOnlyList<IncentiveGoalMonthWeek> MogulWeeks,
    IncentiveGoalEarn Mogul,
    IncentiveGoalEarn Twizos,
    IncentiveGoalEarn? Best,
    int BestWins,
    DateOnly? BestUntil);

/// <summary>Данные v2 личной карточки ТП: день, неделя или месяц.</summary>
public sealed record IncentiveGoalAgent(IncentiveGoalDay? Day, IncentiveGoalWeek? Week, IncentiveGoalMonth? Month);

/// <summary>ТП в итоге дня команды СВР v2: итоги маршрута дня и кешбек.</summary>
public sealed record IncentiveGoalTeamRow(IncentiveAgent Agent, IncentiveGoalTotals Totals)
{
    /// <summary>✓ — взято от половины целей; ½ — от четверти; × — меньше.</summary>
    public string Status => Totals.Share >= 0.5m ? IncentiveGoalStatus.Taken : Totals.Share >= 0.25m ? IncentiveGoalStatus.Partial : IncentiveGoalStatus.Refused;
}

/// <summary>Подарок или стойка у СВР и РМ: выдано за период и остаток на складе региона (null — склада нет); у стойки — цель региона.</summary>
public sealed record IncentiveGoalStock(string Item, int Issued, int? Left, int? Target = null);

/// <summary>Итог дня команды СВР v2 (18:30): регион, ТП с итогами, подарки и стойки (выдано сегодня / остаток).</summary>
public sealed record IncentiveGoalTeam(string Title, string Region, IReadOnlyList<IncentiveGoalTeamRow> Rows, IReadOnlyList<IncentiveGoalStock> Stock, IncentiveGoalBalances Balances);

/// <summary>
/// Регион в итоге недели зоны РМ v2: спящих в списке и разбужено (с начала YM-01), клиентов Twizos за неделю и новых, кг, стоек всего и за
/// неделю, подарков за неделю, остаток подарков на складе (подарок → штук), ТП.
/// </summary>
public sealed record IncentiveGoalZoneRegion(string Name, int Agents, int Listed, int Woken, int TwizosClients, int TwizosNew, decimal TwizosKg, int StandsTotal,
    int StandsWeek, int Gifts, IReadOnlyDictionary<string, int> GiftStock, IncentiveGoalBalances Balances)
{
    public decimal WokenShare => Listed == 0 ? 0 : (decimal)Woken / Listed;
}

/// <summary>Итог недели зоны РМ v2 (сб 19:00): регионы, балансы зоны за неделю, последняя ли неделя акций, цель «разбудить».</summary>
public sealed record IncentiveGoalZone(string Title, DateOnly From, DateOnly To, IReadOnlyList<IncentiveGoalZoneRegion> Regions, IncentiveGoalBalances Balances, bool Last,
    decimal WakeShare);

namespace OneBase.Application.Telegram.Battle;

/// <summary>Статус магазина на карте: впереди (розовый), всё взято (зелёный ✓), частично (оранжевый ½), отказ (красный ✕).</summary>
public static class BattleStoreStatus
{
    public const string Ahead = "ahead";
    public const string Taken = "taken";
    public const string Partial = "partial";
    public const string Refused = "refused";
}

/// <summary>Цель на карточке: вид, подвид, уровень (набор или категория), суммы, что предлагать, статус и факт.</summary>
public sealed record BattleGoalView(
    Guid Id,
    string Kind,
    string Sub,
    string Level,
    decimal TargetSum,
    decimal? SecondSum,
    int Percent,
    int? SecondPercent,
    string? ProductCode,
    string Status,
    decimal? TakenSum,
    int? TakenPercent)
{
    /// <summary>Новый АКБ — подключение Твизос или Могул.</summary>
    public bool IsNew => Sub == Domain.Battle.BattleGoalSub.Connect;

    public bool IsTaken => Status == Domain.Battle.BattleGoalStatus.Taken;
}

/// <summary>Магазин маршрута дня с целями.</summary>
public sealed record BattleStore(long MarketId, string Name, int Order, IReadOnlyList<BattleGoalView> Goals, string Status, bool Visited, decimal OrdersSum);

/// <summary>Ход недели: взято из поставленных, новые АКБ, бонус по лестнице, следующая ступень и сколько до неё целей.</summary>
public sealed record BattleWeekProgress(int Taken, int Total, int NewTaken, int NewTotal, decimal Bonus, BattleBonusStep? NextStep, int ToNextStep, bool NewAkbOk)
{
    public decimal Share => Total == 0 ? 0 : (decimal)Taken / Total;
}

/// <summary>Карточка дня ТП: маршрут с целями, счётчики, ход недели, свежесть данных.</summary>
public sealed record BattleDay(
    long AgentId,
    string AgentName,
    string? Region,
    DateOnly Date,
    string RouteSource,
    IReadOnlyList<BattleStore> Stores,
    bool MogulActive,
    BattleWeekProgress Week,
    DateTimeOffset? OrdersAsOf)
{
    public IEnumerable<BattleGoalView> Goals => Stores.SelectMany(s => s.Goals);
    public int TwizosTotal => Goals.Count(g => g.Kind == Domain.Battle.BattleGoalKind.Twizos);
    public int TwizosTaken => Goals.Count(g => g.Kind == Domain.Battle.BattleGoalKind.Twizos && g.IsTaken);
    public int MogulTotal => Goals.Count(g => g.Kind == Domain.Battle.BattleGoalKind.Mogul);
    public int MogulTaken => Goals.Count(g => g.Kind == Domain.Battle.BattleGoalKind.Mogul && g.IsTaken);
    public int Taken => TwizosTaken + MogulTaken;
    public int Total => TwizosTotal + MogulTotal;
    public int StoresPassed => Stores.Count(s => s.Status != BattleStoreStatus.Ahead);
    public int Refusals => Stores.Count(s => s.Status == BattleStoreStatus.Refused);
}

/// <summary>День недели на «доске побед»: магазины со статусами, взято из поставленных.</summary>
public sealed record BattleWeekDay(DateOnly Date, IReadOnlyList<BattleStore> Stores, int Taken, int Total)
{
    public decimal Share => Total == 0 ? 0 : (decimal)Taken / Total;
}

/// <summary>Карточка недели ТП: 6 дней, ход недели, место в регионе среди участников.</summary>
public sealed record BattleWeek(
    long AgentId,
    string AgentName,
    string? Region,
    DateOnly Monday,
    DateOnly Saturday,
    IReadOnlyList<BattleWeekDay> Days,
    BattleWeekProgress Progress,
    int? Rank,
    int Participants,
    DateTimeOffset? OrdersAsOf)
{
    public int TwizosTaken => Days.Sum(d => d.Stores.SelectMany(s => s.Goals).Count(g => g.Kind == Domain.Battle.BattleGoalKind.Twizos && g.IsTaken));
    public int TwizosTotal => Days.Sum(d => d.Stores.SelectMany(s => s.Goals).Count(g => g.Kind == Domain.Battle.BattleGoalKind.Twizos));
    public int MogulTaken => Days.Sum(d => d.Stores.SelectMany(s => s.Goals).Count(g => g.Kind == Domain.Battle.BattleGoalKind.Mogul && g.IsTaken));
    public int MogulTotal => Days.Sum(d => d.Stores.SelectMany(s => s.Goals).Count(g => g.Kind == Domain.Battle.BattleGoalKind.Mogul));
    public BattleWeekDay? BestDay => Days.Where(d => d.Total > 0).OrderByDescending(d => d.Share).ThenByDescending(d => d.Taken).FirstOrDefault();
}

/// <summary>Плитка призового набора на карточке.</summary>
public sealed record BattlePrizeTile(string Level, decimal Sum, string Contents, string Prize);

/// <summary>
/// Что рисует карточка: день (08:30, 13:00, 18:00) или неделя (суббота), по одной карточке на ТП (у СВ — карточки его ТП страницами),
/// призовые наборы и строка Могул для шапки, язык подписей.
/// </summary>
public sealed record BattleCards(
    string Slot,
    IReadOnlyList<BattleDay> Days,
    IReadOnlyList<BattleWeek> Weeks,
    IReadOnlyList<BattlePrizeTile> Prizes,
    string MogulLine,
    IReadOnlyList<BattleBonusStep> BonusSteps,
    string Language)
{
    public bool IsWeek => Slot == "saturday";
}

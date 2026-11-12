using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>
/// Подарок клиенту за набор Twizos (TW-02): заказ с суммой Twizos от MinSum (по прайсу, до скидки) — подарок Gift (картинка: mug, thermos,
/// kettle). ProductIds — бонусные товары Linko этого подарка (строка заказа с ценой 0: «Бокал», «Термос», «Электрочайник») — по ним остатки на
/// складах. Stock — сколько подарков выделено на акцию (ТЗ: 2000 / 2000 / 1000), справочно для сводок.
/// </summary>
public sealed record IncentiveGiftTier(decimal MinSum, string Gift, string Name, string? NameUz = null, IReadOnlyList<long>? ProductIds = null, int Stock = 0);

/// <summary>Пара подарков ТТ, которая уже брала Twizos: средний чек меньше MaxCheck (null — без верхней границы) — наборы по кодам («T», «K140»).</summary>
public sealed record IncentivePairRule(decimal? MaxCheck, IReadOnlyList<string> Sets);

/// <summary>
/// Цели Twizos по точкам маршрута (ТЗ, раздел 4.2) и подарки TW-02. Брали Twizos — пара по чеку (Pairs); не брали — старт: чек меньше SmallCheck —
/// SmallStartSet (35 г × 3 кор.), иначе StartSet (140 г × 1 кор.); чек меньше MinCheck — без цели. Сумма кг целей меньше нормы дня × Reserve
/// (запас на отказы) — новым клиентам с чеком от PairNewMinCheck сразу пара («+ план»); всё равно мало — «Добери вне маршрута»: до ExtraMax
/// точек ТП с других дней с самым большим чеком. Средний чек — сумма дня с заказом (доставлено, до скидки) за CheckDays дней; ТОП SKU региона — за
/// TopDays дней. TargetPrize — приз для расчёта цели (картинка ступени: tablet, tv, phone; ТЗ — телефон). Conversion — доля предложений набора,
/// которые становятся покупкой (для «предложений в день»). ExtraPairs — пары для добора вне маршрута (образец: от 1,2 млн — чайник + кружка, иначе
/// термос + кружка); нет — правила шага 1.
/// </summary>
public sealed record IncentiveTwizosPromo(
    IReadOnlyList<IncentiveGiftTier>? Gifts = null,
    IReadOnlyList<IncentivePairRule>? Pairs = null,
    string StartSet = "K140",
    string SmallStartSet = "K35",
    decimal SmallCheck = 250_000,
    decimal MinCheck = 60_000,
    decimal PairNewMinCheck = 300_000,
    decimal Reserve = 1.3m,
    decimal Conversion = 0.4m,
    int ExtraMax = 5,
    int CheckDays = 90,
    int TopDays = 90,
    string? TargetPrize = "phone",
    IReadOnlyList<IncentivePairRule>? ExtraPairs = null)
{
    [JsonIgnore]
    public IReadOnlyList<IncentiveGiftTier> GiftTiers => (Gifts ?? []).OrderBy(g => g.MinSum).ToList();
    [JsonIgnore]
    public IReadOnlyList<IncentivePairRule> PairRules => (Pairs ?? []).OrderBy(p => p.MaxCheck ?? decimal.MaxValue).ToList();
    [JsonIgnore]
    public IReadOnlyList<IncentivePairRule> ExtraPairRules => (ExtraPairs ?? Pairs ?? []).OrderBy(p => p.MaxCheck ?? decimal.MaxValue).ToList();

    /// <summary>Подарок по картинке (mug, thermos, kettle).</summary>
    public IncentiveGiftTier? GiftByImage(string? gift) => GiftTiers.FirstOrDefault(g => g.Gift == gift);
}

/// <summary>Фасовка Yamelly: вес пачки (0,42 кг) — пачек в коробке (8); пачка строки заказа узнаётся по весу (кг строки ÷ количество).</summary>
public sealed record IncentivePacking(decimal UnitKg, int PerBox, string Name);

/// <summary>
/// Yamelly спящим (YM-01, YM-02): точка из списка спящих ТП (чья — по закреплению Linko) берёт в одной накладной MinBoxes целых коробок одной
/// фасовки — скидка DiscountPercent на весь Yamelly накладной (её даёт Linko), ТП — кэшбек CashbackPercent от суммы Yamelly после скидки (по
/// прайсу: 283 200 → 263 376 → 26 338). Точка «разбужена». Цель месяца — WakeShare списка; «можно ещё заработать» — оставшиеся спящие ×
/// MinCashback. Товары — типы Linko (помадка с подтипами). Список спящих — из Linko: брали Yamelly с ListFrom (по умолчанию 1 января), но не брали
/// с ListSince (по умолчанию 1-е число месяца перед началом акции: для 06.11 — 01.10); пересчёт — по субботам на следующую неделю.
/// </summary>
public sealed record IncentiveYamellyPromo(
    DateOnly Start,
    DateOnly End,
    IReadOnlyList<long>? ProductTypeIds = null,
    IReadOnlyList<IncentivePacking>? Packings = null,
    string Product = "Yamelly",
    decimal DiscountPercent = 7,
    decimal CashbackPercent = 10,
    int MinBoxes = 2,
    decimal WakeShare = 0.3m,
    decimal MinCashback = 26_338,
    DateOnly? SleepingFrom = null,
    DateOnly? SleepingSince = null)
{
    public bool Covers(DateOnly day) => day >= Start && day <= End;

    /// <summary>С какого дня смотреть покупки Yamelly для списка спящих.</summary>
    [JsonIgnore]
    public DateOnly ListFrom => SleepingFrom ?? new DateOnly(Start.Year, 1, 1);

    /// <summary>С какого дня точка «не брала» Yamelly — спящая.</summary>
    [JsonIgnore]
    public DateOnly ListSince => SleepingSince ?? new DateOnly(Start.Year, Start.Month, 1).AddMonths(-1);
}

/// <summary>Лучший агент дня Twizos (TW-01): пн–сб в период; победитель группы (розница / рынок и опт) — больше всех кг за день, не меньше порога.</summary>
public sealed record IncentiveBestPromo(DateOnly Start, DateOnly End, decimal RetailMinKg = 6, decimal WholesaleMinKg = 30, decimal Prize = 500_000)
{
    public bool Covers(DateOnly day) => day >= Start && day <= End;

    public decimal MinKg(bool wholesale) => wholesale ? WholesaleMinKg : RetailMinKg;
}

/// <summary>Территория YM-03: филиалы Linko и план в кг за период.</summary>
public sealed record IncentiveTerritory(string Name, IReadOnlyList<string> Branches, decimal PlanKg);

/// <summary>
/// Территориальный план (YM-03), все конфеты (типы Linko): выполнение от Threshold — премия ТП, продавшему от AgentMinKg, и СВР, пропорционально %
/// выполнения.
/// </summary>
public sealed record IncentiveTerritoryPromo(
    DateOnly Start,
    DateOnly End,
    IReadOnlyList<IncentiveTerritory>? Territories = null,
    IReadOnlyList<long>? ProductTypeIds = null,
    decimal Threshold = 0.85m,
    decimal AgentMinKg = 50,
    decimal AgentPrize = 1_000_000,
    decimal SupervisorPrize = 2_000_000);

/// <summary>Бюджет акций на период, сум (для сводки руководства): кэшбек YM-02, призы TW-01, премии YM-03; 0 — не задан.</summary>
public sealed record IncentiveBudget(decimal Cashback = 0, decimal Best = 0, decimal Territory = 0);

/// <summary>
/// Акции ноября вокруг накопительной (incentive.Programs.PromosJson; ТЗ «Карточки ТК и расчёт целей по акциям ноября»). Нет раздела — нет акции:
/// без Twizos — цели магазинов по старому правилу (набор по среднему закупу), без Yamelly — нет спящих и кэшбека.
/// </summary>
public sealed record IncentivePromos(
    IncentiveTwizosPromo? Twizos = null,
    IncentiveYamellyPromo? Yamelly = null,
    IncentiveBestPromo? Best = null,
    IncentiveTerritoryPromo? Territory = null,
    IncentiveBudget? Budget = null)
{
    public static readonly IncentivePromos None = new();

    public static IncentivePromos Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return None;
        }

        return JsonSerializer.Deserialize<IncentivePromos>(json, IncentiveSettings.Json) ?? None;
    }

    public string ToJson() => JsonSerializer.Serialize(this, IncentiveSettings.Json);

    /// <summary>Наборы Twizos ноября без 200 г (ТЗ, раздел 4.2): код, подарок, состав, сумма по прайсу, кг.</summary>
    public static readonly IReadOnlyList<IncentiveStoreSet> NovemberSets =
    [
        new(0, "35 г × 3 кор.", 2.94m, "кружка", null, "35 g × 3 quti", "krujka", "K35", 310_800, "mug"),
        new(0, "140 г × 1 кор.", 3.64m, "кружка", null, "140 g × 1 quti", "krujka", "K140", 288_600, "mug"),
        new(0, "140 г ×1 + 35 г ×1", 4.62m, "термос", null, "140 g ×1 + 35 g ×1", "termos", "T", 392_200, "thermos"),
        new(0, "140 г ×1 + 35 г ×3", 6.58m, "чайник", null, "140 g ×1 + 35 g ×3", "choynak", "C", 599_400, "kettle"),
    ];

    /// <summary>Акции ноября 2026 по ТЗ (раздел 2 и 8): то, что миграция записывает акции Twizos.</summary>
    public static IncentivePromos November2026 => new(
        new IncentiveTwizosPromo(
            [
                new(288_600, "mug", "кружка", "krujka", [297], 2000),
                new(392_200, "thermos", "термос", "termos", [136], 2000),
                new(584_600, "kettle", "чайник", "choynak", [333], 1000),
            ],
            [new(600_000, ["T", "K140"]), new(1_200_000, ["C", "K140"]), new(null, ["C", "T"])],
            ExtraPairs: [new(1_200_000, ["T", "K140"]), new(null, ["C", "K140"])]),
        new IncentiveYamellyPromo(new(2026, 11, 6), new(2026, 11, 30), [8],
            [new(0.42m, 8, "420 г"), new(1m, 4, "1 кг")]),
        new IncentiveBestPromo(new(2026, 11, 6), new(2026, 11, 30)),
        new IncentiveTerritoryPromo(new(2026, 11, 1), new(2026, 11, 30),
            [
                new("Самарканд", ["Самарканд"], 22_500), new("Кибрай", ["Кибрай"], 600), new("Олмалык", ["Олмалик", "AAA"], 730), new("Пскент", ["Пскент"], 600),
            ],
            // Все конфеты: бамбук, шоколад, печенье, песочный, помадка, трубочки, кекс (типы Linko с подтипами) — уточнить у Улугбека.
            [4, 5, 6, 7, 8, 9, 10]),
        new IncentiveBudget());
}

namespace OneBase.Application.Telegram.Battle;

/// <summary>Призовой набор Твизос: уровень («300»), закуп набора до скидки, состав, приз, для ТТ со средним закупом до MaxAvg (null — без верха).</summary>
public sealed record BattleTwizosSet(string Level, decimal Sum, string Contents, string Prize, decimal? MaxAvg, decimal Income);

/// <summary>Ступень сетки Могул: категория по среднему закупу Могул в месяц (от MinAvg), порог 5%, второй порог и его процент.</summary>
public sealed record BattleMogulTier(string Letter, decimal MinAvg, decimal Threshold1, decimal Threshold2, int Percent2);

/// <summary>Ступень бонуса недели: доля взятых целей → сумма.</summary>
public sealed record BattleBonusStep(int Percent, decimal Sum);

/// <summary>
/// Правила «Поля боя» (ТЗ v2 от 26.10.2026) — раздел Battle в appsettings; значения по умолчанию — из ТЗ. Даты и проценты акций приходят
/// из Linko, здесь — то, чего в Linko нет: пороги сетки, наборы Твизос, фильтры ТТ и ТП, лестница бонуса.
/// </summary>
public sealed class BattleOptions
{
    /// <summary>Типы товаров Linko категории Твизос (претцель) и Могул (помадка); подтипы Linko относятся к ним же.</summary>
    public long[] TwizosTypeIds { get; set; } = [22];
    public long[] MogulTypeIds { get; set; } = [8, 17, 18, 19, 20, 21];

    /// <summary>Окно среднего закупа: последние N месяцев, включая текущий.</summary>
    public int WindowMonths { get; set; } = 4;

    /// <summary>Активная база: средний закуп Candy Gold, сум/мес, не меньше.</summary>
    public decimal ActiveBaseMinAvg { get; set; } = 50_000;

    /// <summary>Цель Твизос не ставится ТТ со средним закупом меньше: набор больше её месячного закупа.</summary>
    public decimal TwizosMinAvg { get; set; } = 300_000;

    public int MaxTwizosGoals { get; set; } = 10;
    public int MaxMogulConnect { get; set; } = 5;
    public int MaxMogulVolume { get; set; } = 5;

    /// <summary>Магазинов с целями на карточке дня — не больше (вторая страница не нужна).</summary>
    public int MaxStores { get; set; } = 20;

    /// <summary>Типы точек, которым цели не ставятся: сети и КА, экспорт, опт, офисы, персонал, тестовые.</summary>
    public string[] ExcludedMarketTypeWords { get; set; } = ["EXPORT", "опт", "Office", "Персонал", "Супермаркет", "тест", "test"];

    /// <summary>Филиалы (дилеры) вне программы: старые дилеры и экспорт.</summary>
    public string[] ExcludedBranchWords { get; set; } = ["эски", "Завод"];

    /// <summary>ТП вне программы — по словам в имени (оптовые рынки).</summary>
    public string[] ExcludedAgentWords { get; set; } = ["OPTOM", "BOZOR"];

    /// <summary>Где своя механика Могул: цели Могул не ставятся (по филиалу ТТ и по имени ТП).</summary>
    public string[] MogulExcludedBranchWords { get; set; } = ["Самарканд", "Samarqand", "Кибрай", "Qibray", "Фергана", "Farg"];
    public string[] MogulExcludedAgentWords { get; set; } = ["534"];

    /// <summary>Слова в названии акции Linko, по которым узнаётся сетка Могул («Акция на Могул Октябрь 2026г. 5% "A"»).</summary>
    public string[] MogulPromoWords { get; set; } = ["Могул", "Mogul"];

    public BattleTwizosSet[] TwizosSets { get; set; } =
    [
        new("300", 288_600, "140 г × 1 коробка", "кружка", 1_000_000, 101_400),
        new("400", 392_200, "140 г + 35 г", "термос", 3_000_000, 137_800),
        new("600", 584_600, "140 г + 200 г", "чайник Tefal", null, 205_400),
    ];

    /// <summary>Топ-вкусы Твизос, которые предлагаем первыми (коды товаров Linko): Kabob, Smetana va piyoz, Smetana va ko'katlar, Dengiz tuzi.</summary>
    public string[] TwizosTopCodes { get; set; } = ["604", "613", "610", "601"];

    public BattleMogulTier[] MogulGrid { get; set; } =
    [
        new("A", 10_000_000, 20_000_000, 35_000_000, 8),
        new("B", 5_000_000, 12_000_000, 20_000_000, 8),
        new("C", 2_500_000, 6_000_000, 10_000_000, 8),
        new("D", 1_000_000, 3_000_000, 4_500_000, 8),
        new("E", 500_000, 1_500_000, 2_500_000, 8),
        new("F", 300_000, 800_000, 1_200_000, 10),
        new("G", 150_000, 400_000, 800_000, 10),
        new("H", 0, 300_000, 700_000, 10),
    ];

    public int MogulPercent { get; set; } = 5;

    /// <summary>Фруктовые позиции от этой суммы в накладной — ещё −5% на фрукты (строка на карточке).</summary>
    public decimal FruitMinSum { get; set; } = 70_000;
    public int FruitPercent { get; set; } = 5;

    public BattleBonusStep[] Bonus { get; set; } =
    [
        new(25, 500_000),
        new(35, 1_000_000),
        new(50, 1_500_000),
        new(65, 2_000_000),
    ];

    /// <summary>Условие бонуса: минимум новых АКБ за неделю (подключение Твизос + подключение Могул); таких целей меньше — все.</summary>
    public int MinNewAkb { get; set; } = 5;
}

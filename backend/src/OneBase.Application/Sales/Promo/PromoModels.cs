namespace OneBase.Application.Sales.Promo;

/// <summary>
/// Строка акций (как в «Полевом контроле»): день приёмки × регион × акция (−1 — ручная скидка) × агент × точка; скидка (у бонуса —
/// стоимость бонусного товара), количество и «продажа по акции» — за вычетом возвратов.
/// </summary>
public sealed record PromoRow(int Day, string Region, long Promo, long? Agent, long? Market, decimal Discount, decimal Qty, decimal Sale);

/// <summary>Скидка строки по товару — для «Товары» и «Эффект акций».</summary>
public sealed record PromoSkuRow(string Region, long Promo, long ProductId, decimal Discount, decimal Qty);

/// <summary>Месяц раздела: строки, товары и «в пути» (заказы «отдан» со скидкой, только у последнего месяца); Days — по какой день данные.</summary>
public sealed record PromoMonth(
    int Year,
    int Month,
    int Days,
    IReadOnlyList<PromoRow> Rows,
    IReadOnlyList<PromoSkuRow> Sku,
    IReadOnlyList<(string Region, long Promo, decimal Discount)> Pending)
{
    public string Key => $"{Year}-{Month}";
}

// ---------- Ответ API ----------

public sealed record PromoMonthOption(string Key, int Year, int Month, int Days, bool Partial);

public sealed record PromoTiles(
    decimal Total,
    decimal ByPromos,
    decimal Manual,
    int Promos,
    decimal Out,
    decimal Pending,
    decimal SumTo,
    decimal? Sales,
    decimal? ToSales,
    int SalesDay,
    int Days);

public sealed record PromoAttention(string Agent, string Region, decimal Sum, decimal Pct, int MonthsWith, int MonthsTotal);

/// <summary>Вывод: ключ фразы (русская строка с {параметрами}) и параметры — переводит страница.</summary>
public sealed record PromoConclusion(string Text, IReadOnlyDictionary<string, string> Args);

public sealed record PromoByPromo(long Id, string Name, string Kind, DateOnly? Begin, DateOnly? Till, int Regions, int Shops, decimal Qty, decimal Sum, decimal Share, decimal Out, bool Selected);

public sealed record PromoTotals(int Regions, int Shops, decimal Qty, decimal Sum, decimal Share, decimal Out);

public sealed record PromoEffect(
    long Id,
    string Name,
    string Status,
    int Regions,
    int Skus,
    decimal? BaseKgDay,
    decimal? CurKgDay,
    decimal? Growth,
    decimal? ControlGrowth,
    decimal? ExtraKg,
    decimal Discount,
    decimal? ReturnPerSum);

public sealed record PromoShopCell(long? Id, string Name, decimal? Purchase, decimal PromoSale, decimal? Share, decimal Cost, decimal? DiscountPct);

public sealed record PromoAgentPromo(long Id, string Name, int Participants, decimal Avg, decimal Max, decimal PromoSale, decimal Cost, decimal? DiscountPct, IReadOnlyList<PromoShopCell> Shops);

public sealed record PromoAgentRow(long? Id, string Name, decimal? Sales, decimal PromoSale, decimal? Share, decimal? NoPromo, int Mogul, int AkbPromo, decimal Cost, decimal? CostPct, IReadOnlyList<PromoAgentPromo> Promos);

public sealed record PromoRegionRow(
    string Name,
    string? Rm,
    decimal? Sales,
    decimal? Growth,
    decimal PromoSale,
    decimal? Share,
    decimal? NoPromo,
    int Mogul,
    int AkbPromo,
    decimal Cost,
    decimal? CostPct,
    IReadOnlyList<PromoAgentRow> Agents);

public sealed record PromoRegionTotals(decimal Sales, decimal PromoSale, decimal? Share, int Mogul, int AkbPromo, decimal Cost, decimal? CostPct);

public sealed record PromoManualRow(string Agent, string Region, decimal Sum, decimal? Sales, decimal? Pct, int Shops, IReadOnlyList<decimal> History, int MonthsWith);

public sealed record PromoCalendarRow(string Key, string Name, string? Sub, IReadOnlyList<decimal> Sale, IReadOnlyList<decimal> Discount, IReadOnlyList<decimal> Qty, IReadOnlyList<int> Akb, int AkbMonth, IReadOnlyList<PromoCalendarRow> Children);

public sealed record PromoCalendar(int Days, IReadOnlyList<int> Sundays, IReadOnlyList<PromoCalendarRow> ByRegion, IReadOnlyList<PromoCalendarRow> ByPromo, PromoCalendarRow Total);

public sealed record PromoAgentTop(string Agent, string Region, decimal Sum, decimal Manual, int Shops, decimal Share);

public sealed record PromoShopTop(long? Id, string Name, string Region, string? Agent, decimal Sum, IReadOnlyList<string> Promos);

public sealed record PromoWaitingShop(long? Id, string Name, string Region, string? Agent, string Kind, int MonthsDiscount, int MonthsBuy, decimal AvgDiscountMonth, decimal AvgNoDiscount, decimal? DiscountToPurchase, IReadOnlyList<decimal?> Purchases, IReadOnlyList<bool> DiscountMonths);

public sealed record PromoProductTop(string Name, decimal Qty, decimal Sum, decimal Share);

public sealed record PromoByMonth(string Key, int Year, int Month, decimal? Sales, decimal Sum, decimal? ToSales, decimal Manual, bool Current);

public sealed record PromoView(
    DateTimeOffset? AsOf,
    IReadOnlyList<PromoMonthOption> Months,
    PromoMonthOption Month,
    IReadOnlyList<string> RmOptions,
    string? Rm,
    IReadOnlyList<long> Selected,
    PromoTiles Tiles,
    IReadOnlyList<PromoAttention> Attention,
    IReadOnlyList<PromoConclusion> Conclusions,
    IReadOnlyList<PromoByPromo> ByPromo,
    PromoTotals? SelectedTotals,
    decimal PromoTotal,
    int EffectDay,
    IReadOnlyList<PromoEffect> Effects,
    int SalesDay,
    IReadOnlyList<PromoRegionRow> Regions,
    PromoRegionTotals RegionTotals,
    IReadOnlyList<string> ManualMonths,
    IReadOnlyList<PromoManualRow> Manual,
    PromoCalendar Calendar,
    IReadOnlyList<PromoAgentTop> Agents,
    int AgentCount,
    IReadOnlyList<PromoShopTop> Shops,
    int ShopCount,
    IReadOnlyList<string> WaitingMonths,
    IReadOnlyList<PromoWaitingShop> Waiting,
    int WaitingOnly,
    int WaitingWait,
    IReadOnlyList<PromoProductTop> Products,
    int ProductCount,
    IReadOnlyList<PromoByMonth> ByMonth);

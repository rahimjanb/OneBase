namespace OneBase.Application.Sales.Tajik;

/// <summary>Месяц, за который в OneBase есть продажи 1С Таджикистана.</summary>
public sealed record TajikMonth(int Year, int Month);

/// <summary>Период страницы: месяц, по какой день данные, закрыт ли месяц, с какой даты вообще есть данные, месяцы с данными.</summary>
public sealed record TajikPeriod(int Year, int Month, DateOnly? DataThrough, bool Closed, DateOnly? DataFrom, IReadOnlyList<TajikMonth> Months);

/// <summary>Состояние загрузки: настроено ли подключение, последний запуск и последний успешный.</summary>
public sealed record TajikSyncInfo(bool Configured, bool Enabled, DateTimeOffset? LastSuccessAt, DateTimeOffset? LastRunAt, string? LastStatus, string? LastError);

/// <summary>Итоги: сумма (сомони), вес (кг), количество (единицы 1С), скидка, накладные, точки, ТП, товары.</summary>
public sealed record TajikTotals(decimal Amount, decimal Kg, decimal Qty, decimal Discount, int Documents, int Stores, int Agents, int Products);

/// <summary>Сравнение с прошлым месяцем за те же дни: окно и его итоги.</summary>
public sealed record TajikCompare(DateOnly From, DateOnly To, decimal Kg, decimal Amount, decimal? VsKg, decimal? VsAmount);

public sealed record TajikCategoryShare(string Name, decimal Kg, decimal? Share);

public sealed record TajikCategoryRow(string Name, decimal Kg, decimal Amount, decimal Qty, int Stores, decimal? Share, decimal PrevKg);

/// <summary>Регион: id (Guid региона или none — «Регион не определён»), продажи, точки, ТП, доля в весе, к прошлому месяцу, категории.</summary>
public sealed record TajikRegionRow(
    string Id,
    string Name,
    decimal Kg,
    decimal Amount,
    int Documents,
    int Stores,
    int Agents,
    decimal? Share,
    decimal PrevKg,
    decimal? VsPrev,
    IReadOnlyList<TajikCategoryShare> Categories);

public sealed record TajikAgentRow(string Name, decimal Kg, decimal Amount, int Documents, int Stores, IReadOnlyList<string> Regions);

/// <summary>Где продан товар: точка, количество, вес, сумма.</summary>
public sealed record TajikProductStore(string Id, string Name, string RegionName, decimal Qty, decimal Kg, decimal Amount);

/// <summary>
/// Товар: код и название 1С, категория отчёта (по товару Linko с тем же номером или по слову в названии), вес единицы и откуда он
/// (<see cref="TajikWeights"/>), продажи. Where — у страниц региона: в каких точках продан.
/// </summary>
public sealed record TajikProductRow(
    string Code,
    string Name,
    string? Category,
    string? LinkoName,
    decimal? UnitKg,
    string WeightSource,
    decimal Qty,
    decimal Kg,
    decimal Amount,
    decimal Discount,
    int Stores,
    decimal? AvgPrice,
    IReadOnlyList<TajikProductStore>? Where = null);

/// <summary>Точка: id (по названию), ТП, регион и откуда он (manual — закреплена, name — по слову в названии, agent — регион ТП, none).</summary>
public sealed record TajikStoreRow(
    string Id,
    string Name,
    string? Agent,
    string RegionId,
    string RegionName,
    string RegionSource,
    decimal Kg,
    decimal Amount,
    int Documents,
    DateOnly LastDate,
    IReadOnlyList<TajikCategoryShare> Categories);

/// <summary>Остаток склада дилера: количество (единицы 1С), вес, продано за 30 дней до снимка, на сколько дней хватит.</summary>
public sealed record TajikStockRow(string Code, string Name, string Warehouse, string? Category, decimal Qty, decimal? Kg, decimal SoldQty30, decimal? DaysOfCover);

/// <summary>Точки без региона: сколько, их вес и доля.</summary>
public sealed record TajikUnresolved(int Stores, decimal Kg, decimal? Share);

/// <summary>«Экспорт → Таджикистан»: итоги, категории, все регионы, ТП, товары и остаток склада дилера.</summary>
public sealed record TajikOverview(
    TajikPeriod Period,
    TajikSyncInfo Sync,
    TajikTotals Totals,
    TajikCompare? Prev,
    IReadOnlyList<TajikCategoryRow> Categories,
    IReadOnlyList<TajikRegionRow> Regions,
    IReadOnlyList<TajikAgentRow> Agents,
    IReadOnlyList<TajikProductRow> Products,
    DateOnly? StockDate,
    IReadOnlyList<TajikStockRow> Stock,
    TajikUnresolved Unresolved,
    int UnknownWeightProducts);

/// <summary>Регион: итоги, категории, точки (с категориями), товары с точками, ТП. Category — фильтр по категории (название).</summary>
public sealed record TajikRegionView(
    TajikPeriod Period,
    string Id,
    string Name,
    IReadOnlyList<string> Keywords,
    string? Category,
    TajikTotals Totals,
    TajikCompare? Prev,
    IReadOnlyList<TajikCategoryRow> Categories,
    IReadOnlyList<TajikStoreRow> Stores,
    IReadOnlyList<TajikProductRow> Products,
    IReadOnlyList<TajikAgentRow> Agents);

public sealed record TajikDocumentLine(string Code, string Name, string? Category, decimal Qty, decimal Price, decimal Amount, decimal Discount, decimal? DiscountPercent, decimal? Kg);

public sealed record TajikDocument(string Number, DateOnly Date, string? Agent, decimal Amount, decimal Kg, decimal Discount, IReadOnlyList<TajikDocumentLine> Lines);

public sealed record TajikStoreMonth(int Year, int Month, decimal Kg, decimal Amount, int Documents);

/// <summary>Точка: регион, ТП, итоги месяца, категории, товары, накладные месяца со строками и продажи по месяцам.</summary>
public sealed record TajikStoreView(
    TajikPeriod Period,
    string Id,
    string Name,
    string? Agent,
    string RegionId,
    string RegionName,
    string RegionSource,
    TajikTotals Totals,
    IReadOnlyList<TajikCategoryRow> Categories,
    IReadOnlyList<TajikProductRow> Products,
    IReadOnlyList<TajikDocument> Documents,
    IReadOnlyList<TajikStoreMonth> Months);

public sealed record TajikSetupRegion(string Id, string Name, IReadOnlyList<string> Keywords, int Stores);

public sealed record TajikSetupAgent(string Name, string? RegionId, int Stores, int Unresolved);

public sealed record TajikSetupStore(string Id, string Name, string? Agent, string RegionId, string RegionName, string Source, string? ManualRegionId, decimal Kg, DateOnly LastDate);

/// <summary>Настройка регионов: регионы со словами, ТП с регионом по умолчанию, все точки с регионом и откуда он.</summary>
public sealed record TajikSetupView(IReadOnlyList<TajikSetupRegion> Regions, IReadOnlyList<TajikSetupAgent> Agents, IReadOnlyList<TajikSetupStore> Stores);

public sealed record TajikRegionInput(string? Id, string Name, IReadOnlyList<string>? Keywords);

/// <summary>Закрепить за регионом: kind = agent (Name — ТП) или store (StoreId); RegionId null — снять.</summary>
public sealed record TajikAssignmentInput(string Kind, string? Name, string? StoreId, string? RegionId);

namespace OneBase.Application.Sales.Metrics;

/// <summary>
/// Раздел «KPI»: премии и выполнение KPI-показателей Linko по людям (ТП и СВР) и регионам за месяц. Деньги (оклад, бонусы, премии,
/// фонд оплаты) — только с правом finance.read; без него поля денег — null (Money = false). Оба режима — «на сегодня» (цифры Linko)
/// и «прогноз» (факт растянут на месяц) — считает сервер, страница только переключает.
/// </summary>
public sealed record KpiView(
    PeriodInfo Period,
    bool Money,
    bool Running,
    decimal Share,
    DateTimeOffset? LoadedAt,
    int TpCount,
    int SvrCount,
    decimal FactKgTp,
    decimal? ForecastKgTp,
    KpiMoney Now,
    KpiMoney Forecast,
    IReadOnlyList<KpiRegion> Regions,
    IReadOnlyList<KpiDirection> Directions,
    IReadOnlyList<string> MissingRegions,
    IReadOnlyList<KpiRegionOption> RegionOptions);

/// <summary>Премии и фонд оплаты (фикса + премии): СВР, ТП, всего.</summary>
public sealed record KpiMoney(decimal? PremiumSvr, decimal? PayrollSvr, decimal? PremiumTp, decimal? PayrollTp, decimal? Premium, decimal? Payroll);

/// <summary>Регион со своими колонками-метками и людьми; Id — Guid региона OneBase или «none» (люди без региона).</summary>
public sealed record KpiRegion(
    string Id,
    string Name,
    string? Direction,
    string Unit,
    int Svr,
    int Tp,
    decimal FactKgSvr,
    decimal FactKgTp,
    KpiMoney Now,
    KpiMoney Forecast,
    IReadOnlyList<KpiColumn> Columns,
    IReadOnlyList<KpiPerson> People);

/// <summary>Колонка карточки региона: метка показателя («Тотал», «Могуль») и единица; Key уникален в регионе.</summary>
public sealed record KpiColumn(string Key, string Label, string Unit);

public sealed record KpiPerson(
    long Id,
    string Name,
    string Role,
    string? Region,
    string RegionSource,
    decimal FactKg,
    decimal? Fix,
    decimal? Bonus100,
    decimal? PremiumNow,
    decimal? PremiumForecast,
    decimal? SalaryNow,
    decimal? SalaryForecast,
    IReadOnlyList<KpiCell> Cells);

/// <summary>
/// Показатель человека: план, продажи, возвраты, факт (Linko), выполнение до тысячных и цвет; порог премии и «Бонус 100%» (уровни Linko);
/// премия — начисленная Linko (на сегодня) и пересчитанная по уровням от прогноза.
/// </summary>
public sealed record KpiCell(
    string Key,
    long IndicatorId,
    string Name,
    string Unit,
    decimal Plan,
    decimal Sale,
    decimal Return,
    decimal Fact,
    decimal Pct,
    string Tone,
    int Level,
    decimal? Threshold,
    decimal? Bonus100,
    decimal? PremiumNow,
    decimal FactForecast,
    decimal PctForecast,
    string ToneForecast,
    decimal? PremiumForecast);

/// <summary>Направление (РМ) с регионами раздела: регионы — по фонду оплаты (без денег — по факту кг ТП).</summary>
public sealed record KpiDirection(string Name, IReadOnlyList<string> RegionIds, int Tp, decimal? PremiumNow, decimal? PremiumForecast, decimal? PayrollNow, decimal? PayrollForecast);

public sealed record KpiRegionOption(string Id, string Name);

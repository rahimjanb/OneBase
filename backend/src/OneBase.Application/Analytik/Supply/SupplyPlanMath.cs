using OneBase.Application.Analytik.Forecasting;

namespace OneBase.Application.Analytik.Supply;

/// <summary>Подтверждённая поставка: сколько (в базовой единице), когда ожидается (null — дата не известна), откуда, просрочена ли.</summary>
public sealed record SupplyIncoming(decimal Quantity, DateOnly? Expected, string Source, bool Overdue);

/// <summary>Потребность производственного задания, ещё не выданная со склада: дата задания и количество.</summary>
public sealed record SupplyCommitment(DateOnly Date, decimal Quantity, string Source);

/// <summary>
/// Данные позиции для расчёта закупки: остатки (физический, резерв заявок, непригодный — просрочен, на контроле, заблокирован), минимум,
/// дневной расход за историю, потребность открытых заданий, подтверждённые поставки, срок поставки, цена, поставщик, единица закупки.
/// </summary>
public sealed record SupplyItemInput(
    Guid ItemId, string Sku, string Name, string Unit,
    decimal OnHand, decimal Reserved, decimal Unusable, decimal MinQty,
    IReadOnlyList<SeriesPoint> DailyConsumption,
    IReadOnlyList<SupplyCommitment> Committed,
    IReadOnlyList<SupplyIncoming> Incoming,
    int? LeadTimeDays, string? LeadTimeSource,
    decimal? UnitPrice, string? PriceSource,
    string? Supplier,
    decimal? PurchaseFactor = null, string? PurchaseUnit = null);

/// <summary>Потребность на горизонт: спрос, страховой запас, подтверждённые поставки, сколько закупить, сколько это стоит (null — нет цены).</summary>
public sealed record SupplyHorizonNeed(int Days, decimal Demand, decimal SafetyStock, decimal Incoming, decimal Required, decimal? Cost);

/// <summary>Рекомендация по позиции — всё, из чего она получена, причины и допущения.</summary>
public sealed record SupplyRecommendation(
    Guid ItemId, string Sku, string Name, string Unit,
    decimal OnHand, decimal Reserved, decimal Unusable, decimal Available, decimal MinQty,
    decimal AvgDailyConsumption, int HistoryDays, int ActiveDays,
    decimal SafetyStock, decimal? DaysOfCover,
    DateOnly? ShortageDate, DateOnly? OrderBy, bool OrderNow,
    int LeadTimeDays, bool LeadTimeAssumed, string LeadTimeSource,
    decimal? UnitPrice, string? PriceSource, string? Supplier,
    decimal IncomingTotal, decimal CommittedTotal,
    IReadOnlyList<SupplyHorizonNeed> Horizons,
    string Risk, bool Deferrable,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Assumptions)
{
    public SupplyHorizonNeed? Horizon(int days) => Horizons.FirstOrDefault(h => h.Days == days);
}

/// <summary>
/// Расчёт потребности в закупке (MRP) по формуле ТЗ:
/// <c>Закупить = спрос за горизонт + страховой запас − доступный остаток − подтверждённые поставки</c>.
/// Без двойного вычитания: резерв заявок уже вычтен из доступного остатка и спросом второй раз не считается; задания производства резерва
/// не создают (их невыданная потребность — спрос), а их расход уже входит в историю — поэтому спрос за горизонт = max(задания, средний
/// расход × дни), а не сумма. Поставки — только подтверждённые (заказ в работе, тендер с договором, машина на территории). Срок поставки и цена
/// не придумываются: нет срока — берётся значение по умолчанию и это помечается допущением; нет цены — стоимость не считается.
/// </summary>
public static class SupplyPlanMath
{
    public static readonly IReadOnlyList<int> HorizonDays = [7, 14, 30, 60, 90];

    /// <summary>Сколько дней вперёд ищется дата дефицита.</summary>
    public const int SimulationDays = 180;

    /// <summary>Уровень сервиса для страхового запаса (95 %).</summary>
    public const double ServiceZ = 1.645;

    private static readonly HashSet<string> CountedUnits = new(StringComparer.OrdinalIgnoreCase) { "шт", "упак", "пачка", "коробка", "компл", "рулон" };

    public static SupplyRecommendation Plan(SupplyItemInput item, DateOnly today, int defaultLeadTimeDays, int historyWindow = 90)
    {
        var reasons = new List<string>();
        var assumptions = new List<string>();
        var available = Math.Max(0, item.OnHand - item.Reserved - item.Unusable);

        var window = item.DailyConsumption.OrderBy(p => p.Date).TakeLast(historyWindow).ToList();
        var historyDays = window.Count;
        var activeDays = window.Count(p => p.Value > 0);
        var rate = historyDays == 0 ? 0 : window.Sum(p => p.Value) / historyDays;
        var sigma = historyDays < 2 ? 0 : Std(window.Select(p => (double)p.Value).ToArray());
        if (historyDays < 30)
        {
            assumptions.Add($"История расхода короткая ({historyDays} дн.) — средний расход ориентировочный.");
        }
        else if (activeDays < 5)
        {
            assumptions.Add($"Расход был только в {activeDays} дн. из {historyDays} — потребность может быть неравномерной.");
        }

        var leadTimeAssumed = item.LeadTimeDays is null;
        var leadTime = item.LeadTimeDays ?? defaultLeadTimeDays;
        var leadSource = item.LeadTimeSource ?? $"не задан — принят {defaultLeadTimeDays} дн. (настройка Аналитика)";
        if (leadTimeAssumed)
        {
            assumptions.Add($"Срок поставки не задан у поставщика и не виден по приездам — принят {defaultLeadTimeDays} дн.");
        }

        var safety = SafetyStock(sigma, leadTime, item.MinQty);

        // Задания: прошедшая дата — потребность «сейчас».
        var committed = item.Committed.Select(c => (Day: Math.Max(0, c.Date.DayNumber - today.DayNumber), c.Quantity)).ToList();
        var committedTotal = committed.Sum(c => c.Quantity);
        var incoming = item.Incoming.Select(i => (Day: i.Expected is { } d ? Math.Max(0, d.DayNumber - today.DayNumber) : (int?)null, i.Quantity, i.Overdue)).ToList();
        var incomingTotal = incoming.Sum(i => i.Quantity);
        if (incoming.Any(i => i.Overdue))
        {
            reasons.Add($"Есть просроченные поставки: {Fmt(incoming.Where(i => i.Overdue).Sum(i => i.Quantity))} {item.Unit} — учтены как ожидаемые сегодня.");
        }

        if (incoming.Any(i => i.Day is null))
        {
            assumptions.Add("У части поставок нет даты — они учитываются только в горизонтах от 30 дней.");
        }

        decimal CommittedBy(int day) => committed.Where(c => c.Day <= day).Sum(c => c.Quantity);
        decimal Demand(int days) => Math.Max(CommittedBy(days), rate * days);
        decimal IncomingBy(int day, bool undated) => incoming.Where(i => i.Day is { } d ? d <= day : undated).Sum(i => i.Quantity);

        // Остаток по дням: доступно + пришедшие поставки − спрос (большее из заданий и среднего расхода нарастающим итогом).
        DateOnly? shortage = null;
        for (var day = 0; day <= SimulationDays && (rate > 0 || committedTotal > 0); day++)
        {
            var stock = available + IncomingBy(day, false) - Demand(day);
            if (stock < 0)
            {
                shortage = today.AddDays(day);
                break;
            }
        }

        DateOnly? orderBy = shortage?.AddDays(-leadTime);
        var orderNow = orderBy is { } ob && ob <= today;
        if (orderBy is { } o && o < today)
        {
            orderBy = today;
        }

        var horizons = new List<SupplyHorizonNeed>();
        foreach (var days in HorizonDays)
        {
            var demand = Math.Round(Demand(days), 3);
            var arriving = IncomingBy(days, days >= 30);
            var required = RoundUp(Math.Max(0, demand + safety - available - arriving), item);
            horizons.Add(new SupplyHorizonNeed(days, demand, safety, arriving, required, item.UnitPrice is { } price ? Math.Round(required * price, 0) : null));
        }

        var need30 = horizons.First(h => h.Days == 30).Required;
        var need90 = horizons.First(h => h.Days == 90).Required;
        var demandExists = rate > 0 || committedTotal > 0;
        string risk;
        if (!demandExists)
        {
            risk = available < item.MinQty ? "medium" : "none";
            reasons.Add(rate == 0 && historyDays > 0 ? $"Расхода за {historyDays} дн. не было." : "Нет истории расхода и открытых заданий.");
        }
        else if ((shortage is { } s && s.DayNumber - today.DayNumber <= leadTime) || available <= 0)
        {
            risk = "critical";
        }
        else if ((shortage is { } s30 && s30.DayNumber - today.DayNumber <= 30) || orderNow)
        {
            risk = "high";
        }
        else if (need30 > 0 || available < safety)
        {
            risk = "medium";
        }
        else
        {
            risk = need90 > 0 ? "low" : "none";
        }

        if (demandExists)
        {
            reasons.Add($"Средний расход {Fmt(rate)} {item.Unit}/день за {historyDays} дн." + (committedTotal > 0 ? $"; открытые задания производства — ещё {Fmt(committedTotal)} {item.Unit}." : "."));
            reasons.Add($"Доступно {Fmt(available)} {item.Unit} (на складе {Fmt(item.OnHand)}, в резерве {Fmt(item.Reserved)}" +
                        (item.Unusable > 0 ? $", непригодно {Fmt(item.Unusable)}" : "") + ").");
            if (shortage is { } sd)
            {
                reasons.Add($"Без закупки остаток закончится {sd:dd.MM.yyyy}; срок поставки {leadTime} дн. — заказать до {(orderBy ?? today):dd.MM.yyyy}.");
            }

            if (need30 > 0)
            {
                reasons.Add($"На 30 дней: спрос {Fmt(horizons[2].Demand)} + страховой запас {Fmt(safety)} − доступно {Fmt(available)} − поставки {Fmt(horizons[2].Incoming)} = {Fmt(need30)} {item.Unit}.");
            }
        }

        if (item.UnitPrice is null)
        {
            assumptions.Add("Цены закупки нет в OneBase — стоимость не рассчитана.");
        }

        return new SupplyRecommendation(item.ItemId, item.Sku, item.Name, item.Unit, item.OnHand, item.Reserved, item.Unusable, available, item.MinQty,
            Math.Round(rate, 3), historyDays, activeDays, safety, rate > 0 ? Math.Round(available / rate, 1) : null, shortage, orderBy, orderNow,
            leadTime, leadTimeAssumed, leadSource, item.UnitPrice, item.PriceSource, item.Supplier, incomingTotal, committedTotal, horizons, risk,
            need30 == 0 && need90 > 0, reasons, assumptions);
    }

    /// <summary>Страховой запас: max(Z₉₅ · σ дневного расхода · √срока поставки, минимальный остаток) — общий для плана закупок и симулятора.</summary>
    public static decimal SafetyStock(double sigma, int leadTimeDays, decimal minQty) =>
        Math.Round(Math.Max((decimal)(ServiceZ * sigma * Math.Sqrt(Math.Max(1, leadTimeDays))), minQty), 3);

    /// <summary>Округление вверх: до единицы закупки (мешок 50 кг), штучные — до целых, остальное — до 0,001.</summary>
    internal static decimal RoundUp(decimal quantity, SupplyItemInput item) => RoundUp(quantity, item.PurchaseFactor, item.Unit);

    /// <summary>Округление закупки вверх (общее для плана закупок и симулятора): до единицы закупки, штучные — до целых, остальное — до 0,001.</summary>
    public static decimal RoundUp(decimal quantity, decimal? purchaseFactor, string unit)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        if (purchaseFactor is > 0 and var factor)
        {
            return Math.Ceiling(quantity / factor) * factor;
        }

        return CountedUnits.Contains(unit) ? Math.Ceiling(quantity) : Math.Ceiling(quantity * 1000) / 1000;
    }

    /// <summary>Стандартное отклонение выборки (n − 1) — разброс дневного расхода.</summary>
    public static double StdDev(IReadOnlyList<double> values) => values.Count < 2 ? 0 : Std(values.ToArray());

    private static double Std(double[] values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1));
    }

    private static string Fmt(decimal value) => Math.Round(value, 2).ToString("#,0.##", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));

    public static int RiskRank(string risk) => risk switch
    {
        "critical" => 0,
        "high" => 1,
        "medium" => 2,
        "low" => 3,
        _ => 4,
    };
}

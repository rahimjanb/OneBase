namespace OneBase.Application.Analytik.Compute;

/// <summary>Политика запаса позиции: средний и σ спроса в день, срок поставки, страховой запас, точка заказа, EOQ, заказов в год, годовые затраты.</summary>
public sealed record InventoryPolicy(string Key, double AvgDaily, double SigmaDaily, double LeadTimeDays, double ServiceLevel, double Z, double SafetyStock,
    double ReorderPoint, double? Eoq, double? OrdersPerYear, double? AnnualCost, double? Stock, double? DaysOfSupply, bool BelowReorder);

/// <summary>Оборачиваемость: оборот за период ÷ средний запас, дни запаса, класс («быстрая», «нормальная», «медленная», «без движения»).</summary>
public sealed record TurnoverItem(string Key, double Flow, double AvgStock, double? Turnover, double? DaysOfSupply, string Class);

/// <summary>Возраст запаса: корзины 0–30, 31–60, 61–90, 91–180, больше 180 дней без движения; неликвид — дольше порога.</summary>
public sealed record AgingBucket(string Bucket, int Items, double Stock);

public sealed record AgingResult(IReadOnlyList<AgingBucket> Buckets, IReadOnlyList<string> DeadStock, double DeadStockShare);

/// <summary>
/// Управление запасами: EOQ (формула Уилсона), страховой запас z·√(L·σd² + d²·σL²), точка заказа d·L + SS, оборачиваемость, дни запаса, возраст.
/// </summary>
public static class InventoryMath
{
    /// <summary>EOQ = √(2·D·S / H): D — спрос в год, S — затраты на заказ, H — хранение единицы в год.</summary>
    public static double Eoq(double annualDemand, double orderCost, double holdingCostPerUnitYear)
    {
        StatMath.Require(annualDemand > 0, "Годовой спрос должен быть больше нуля.");
        StatMath.Require(orderCost > 0 && holdingCostPerUnitYear > 0, "Затраты на заказ и хранение должны быть больше нуля.");
        return Math.Sqrt(2 * annualDemand * orderCost / holdingCostPerUnitYear);
    }

    public static double SafetyStock(double z, double sigmaDaily, double leadTimeDays, double avgDaily = 0, double sigmaLeadTimeDays = 0) =>
        z * Math.Sqrt(leadTimeDays * sigmaDaily * sigmaDaily + avgDaily * avgDaily * sigmaLeadTimeDays * sigmaLeadTimeDays);

    /// <summary>Политика по дневному расходу (история), сроку поставки, уровню сервиса и, если заданы, затратам на заказ и хранение.</summary>
    public static InventoryPolicy Policy(string key, IReadOnlyList<double> daily, double leadTimeDays, double serviceLevel = 0.95, double? orderCost = null,
        double? holdingCostPerUnitYear = null, double? stock = null, double sigmaLeadTimeDays = 0)
    {
        StatMath.Require(daily.Count >= 7, "Для политики запаса нужно хотя бы 7 дней истории расхода.");
        StatMath.Require(leadTimeDays > 0, "Срок поставки должен быть больше нуля.");
        StatMath.Require(serviceLevel is > 0.5 and < 1, "Уровень сервиса — от 0,5 до 1 (например, 0,95).");
        var avg = StatMath.Mean(daily);
        var sigma = StatMath.StdDev(daily);
        var z = StatMath.NormalQuantile(serviceLevel);
        var ss = SafetyStock(z, sigma, leadTimeDays, avg, sigmaLeadTimeDays);
        var rop = avg * leadTimeDays + ss;
        double? eoq = null, orders = null, cost = null;
        if (orderCost is > 0 && holdingCostPerUnitYear is > 0 && avg > 0)
        {
            var annual = avg * 365;
            eoq = Eoq(annual, orderCost.Value, holdingCostPerUnitYear.Value);
            orders = annual / eoq;
            cost = orders * orderCost.Value + eoq / 2 * holdingCostPerUnitYear.Value + ss * holdingCostPerUnitYear.Value;
        }

        return new InventoryPolicy(key, avg, sigma, leadTimeDays, serviceLevel, z, ss, rop, eoq, orders, cost, stock,
            stock is { } s && avg > 0 ? s / avg : null, stock is { } st && st <= rop);
    }

    /// <summary>Оборачиваемость за период из days дней: оборот ÷ средний запас; дни запаса = средний запас ÷ дневной оборот.</summary>
    public static TurnoverItem Turnover(string key, double flow, double avgStock, int days)
    {
        StatMath.Require(days > 0, "Период должен быть больше нуля дней.");
        if (avgStock <= 0)
        {
            return new TurnoverItem(key, flow, avgStock, null, null, flow > 0 ? "без запаса" : "без движения");
        }

        if (flow <= 0)
        {
            return new TurnoverItem(key, flow, avgStock, 0, null, "без движения");
        }

        var turnover = flow / avgStock;
        var dos = avgStock / (flow / days);
        var cls = dos <= 15 ? "быстрая" : dos <= 60 ? "нормальная" : "медленная";
        return new TurnoverItem(key, flow, avgStock, turnover, dos, cls);
    }

    public static AgingResult Aging(IReadOnlyList<(string Key, double Stock, int? DaysSinceMovement)> items, int deadAfterDays = 90)
    {
        StatMath.Require(items.Count > 0, "Нет позиций для анализа возраста запаса.");
        (string Name, int Max)[] buckets = [("0–30", 30), ("31–60", 60), ("61–90", 90), ("91–180", 180), ("больше 180", int.MaxValue)];
        var withStock = items.Where(i => i.Stock > 0).ToList();
        var result = new List<AgingBucket>();
        var lower = -1;
        foreach (var (name, max) in buckets)
        {
            var inBucket = withStock.Where(i => (i.DaysSinceMovement ?? int.MaxValue) > lower && (i.DaysSinceMovement ?? int.MaxValue) <= max).ToList();
            result.Add(new AgingBucket(name, inBucket.Count, inBucket.Sum(i => i.Stock)));
            lower = max;
        }

        var dead = withStock.Where(i => (i.DaysSinceMovement ?? int.MaxValue) > deadAfterDays).ToList();
        var total = withStock.Sum(i => i.Stock);
        return new AgingResult(result, dead.Select(i => i.Key).ToList(), total > 0 ? dead.Sum(i => i.Stock) / total : 0);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.Settlements;
using OneBase.Application.Sales.Stock;

namespace OneBase.Application.Sales.Health;

/// <summary>Товар ядра: название и категория отчёта.</summary>
public sealed record SystemCoreSku(long ProductId, string Name, string Category);

/// <summary>Качество дистрибуции: ядро (Months — месяцы окна «ГГГГ-М», последний — выбранный), республика и регионы со строками ТП.</summary>
public sealed record SystemQualityView(
    int CoreMin,
    decimal CoreShare,
    IReadOnlyList<string> Months,
    IReadOnlyList<SystemCoreSku> Core,
    SystemQualityRow Total,
    IReadOnlyList<SystemQualityRow> Regions);

/// <summary>
/// Дилер в «Деньгах и запасе»: долг и просрочка (DSO на дату данных), долг по корзинам старения и резерв по шкале, DSO факт и норма;
/// запас — остаток склада региона (кг) на 1-е и сейчас, в днях вторички месяца; первичка и вторичка региона за месяц, кг. Region — регион,
/// у которого он дилер (null — прямой клиент: запаса нет).
/// </summary>
public sealed record SystemDealer(
    string Name,
    string? Region,
    string Status,
    decimal Debt,
    decimal Overdue,
    IReadOnlyList<decimal> Aging,
    decimal Reserve,
    decimal? FactDso,
    int? Norm,
    bool DefaultNorm,
    decimal? StockKg,
    decimal? Stock1Kg,
    decimal? DaysNow,
    decimal? Days1,
    decimal? PrimaryKg,
    decimal? SecondaryKg,
    bool Overstock);

/// <summary>
/// Деньги и запас дилеров: долг — на дату данных DSO (AsOf); запас «на StockFrom → сейчас» (остаток — снимок Linko на StockAt), первичка и
/// вторичка — с StockFrom по дату данных: с 1-го числа месяца снимка, а в первые дни месяца (меньше SystemMath.MinStockDays) — с 1-го
/// прошлого. Shares — доля долга в корзине.
/// </summary>
public sealed record SystemMoneyView(
    DateOnly AsOf,
    IReadOnlyList<SystemBucket> Buckets,
    IReadOnlyList<decimal> Shares,
    decimal Debt,
    decimal Overdue,
    decimal Reserve,
    DateTime? StockAt,
    DateOnly StockFrom,
    IReadOnlyList<SystemDealer> Dealers);

/// <summary>
/// «Система» за месяц: маршрут и качество дистрибуции — из Linko (визиты по плану и вне плана, заказы); деньги — DSO OneBase (отгрузки и
/// реестры оплат Google Sheets) — только с правом «Финансы» (Money = null, MoneyHidden — права нет) и если оплаты привязаны (MoneyMissing).
/// </summary>
public sealed record SystemView(
    PeriodInfo Period,
    SystemRouteRow RouteTotal,
    IReadOnlyList<SystemRouteRow> Route,
    SystemQualityView Quality,
    SystemMoneyView? Money,
    bool MoneyHidden,
    bool MoneyMissing);

/// <summary>
/// «Система» — как вкладка «Система» «Полевого контроля»: маршрут (заказы от плана визитов), качество точки (ядро ассортимента, точки с одним
/// SKU, верхние 20%) и деньги с запасом дилеров (корзины старения, резерв, запас в днях, первичка против вторички). Всё из уже собранных
/// данных OneBase: месяц аналитики (SalesDataLoader), DSO (SettlementsService), снимок остатков и первичка.
/// </summary>
public sealed class SystemService(
    IAppDbContext db,
    SalesOptions options,
    SalesDataLoader loader,
    SettlementsService settlements,
    PrimaryService primary,
    IMemoryCache cache,
    SalesCacheSignal signal)
{
    public async Task<SystemView> GetAsync(int? year, int? month, bool money, CancellationToken ct = default)
    {
        var analytics = await loader.LoadAsync(year, month, ct);
        var period = analytics.Period;
        return await SalesViewCache.GetAsync(cache, signal, $"sales:system:{period.Year}-{period.Month}:{money}", () => BuildAsync(analytics, money, ct));
    }

    private async Task<SystemView> BuildAsync(SalesAnalytics analytics, bool withMoney, CancellationToken ct)
    {
        var period = analytics.Period;
        var (routeTotal, route) = analytics.SystemRoute();

        // Ядро — по весу трёх месяцев (выбранный и два прошлых) по стране; регулярность верхних точек — покупки в прошлых двух.
        var start = new DateOnly(period.Year, period.Month, 1);
        var others = new List<SalesAnalytics>();
        for (var i = SystemMath.CoreMonths - 1; i >= 1; i--)
        {
            var m = start.AddMonths(-i);
            others.Add(await loader.LoadAsync(m.Year, m.Month, ct));
        }

        var productKg = others.Append(analytics).SelectMany(a => a.SystemProductKg())
            .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));
        var core = SystemMath.Core(productKg, p => analytics.SystemCategoryOf(p)?.Group);
        var (qualityTotal, quality) = analytics.SystemQuality(core.ToHashSet(), others.Select(a => a.SystemBuyers()).ToList());
        var qualityView = new SystemQualityView(
            SystemMath.CoreMin,
            SystemMath.CoreShare,
            others.Append(analytics).Select(a => $"{a.Period.Year}-{a.Period.Month}").ToList(),
            core.Select(p => new SystemCoreSku(p, analytics.ProductName(p), analytics.SystemCategoryOf(p)?.Name ?? "—"))
                .OrderBy(c => c.Category, StringComparer.CurrentCulture).ThenByDescending(c => productKg.GetValueOrDefault(c.ProductId)).ToList(),
            qualityTotal,
            quality);

        SystemMoneyView? moneyView = null;
        var missing = false;
        if (withMoney)
        {
            moneyView = await MoneyAsync(ct);
            missing = moneyView is null;
        }

        return new SystemView(period, routeTotal, route, qualityView, moneyView, !withMoney, missing);
    }

    private async Task<SystemMoneyView?> MoneyAsync(CancellationToken ct)
    {
        var dso = DsoCompute.Build(await settlements.BaseAsync(ct));
        if (!dso.HasPayments)
        {
            return null;
        }

        // Регион дилера — по имени дилера региона и его другим именам («Настройки продаж»).
        var regionRows = await db.SalesRegions.AsNoTracking().Select(r => new { r.Name, r.DealerName, r.DealerAliases }).ToListAsync(ct);
        var regionOfDealer = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in regionRows)
        {
            foreach (var name in new[] { r.DealerName }.Concat(r.DealerAliases ?? []).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                regionOfDealer.TryAdd(PnlMath.DealerKey(name!), r.Name);
            }
        }

        // Запас: снимок остатков Linko; первичка и вторичка — с 1-го числа месяца снимка, в первые дни месяца — с 1-го прошлого.
        var snapshot = await StockSnapshotBuilder.GetAsync(cache, signal, db, options, ct);
        var stockKg = snapshot.Regions.ToDictionary(
            r => r.Name,
            r => snapshot.Pieces.Where(p => p.Key.StockId == r.StockId).Sum(p => snapshot.KgOf(p.Key.ProductId, r.StockId) ?? 0),
            StringComparer.OrdinalIgnoreCase);
        var current = await loader.LoadAsync(snapshot.SnapshotDate.Year, snapshot.SnapshotDate.Month, ct);
        var months = new List<SalesAnalytics> { current };
        if ((current.Period.ElapsedDays ?? current.Period.WorkedDays) < SystemMath.MinStockDays)
        {
            var previous = new DateOnly(current.Period.Year, current.Period.Month, 1).AddMonths(-1);
            months.Insert(0, await loader.LoadAsync(previous.Year, previous.Month, ct));
        }

        var secondary = Sum(months.Select(m => m.SystemRegionKg()));
        var shipments = new List<IReadOnlyDictionary<string, decimal>>();
        foreach (var m in months)
        {
            shipments.Add((await primary.GetAsync(m.Period.Year, m.Period.Month, ct)).Dealers
                .GroupBy(d => d.Sub ?? d.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Month.Kg), StringComparer.OrdinalIgnoreCase));
        }

        var shipped = Sum(shipments);
        var elapsed = months.Sum(m => m.Period.ElapsedDays ?? m.Period.WorkedDays);

        var dealers = dso.Dealers
            .Where(d => d.Debt != 0 || d.Status == "active")
            .Select(d =>
            {
                var aging = SystemMath.Aging(d.Open.Select(o => (o.Age, o.Remaining)));
                var region = regionOfDealer.GetValueOrDefault(d.Key);
                decimal? Of(IReadOnlyDictionary<string, decimal> map) => region is not null && map.TryGetValue(region, out var v) ? v : null;
                var (stockNow, prim, sec) = (Of(stockKg), Of(shipped), Of(secondary));
                var (stock1, daysNow, days1, overstock) = SystemMath.Stock(stockNow, prim, sec, elapsed);
                return new SystemDealer(d.Name, region, d.Status, d.Debt, d.Overdue, aging, SystemMath.Reserve(aging), d.Fact, d.Norm, d.DefaultNorm,
                    stockNow, stock1, daysNow, days1, prim, sec, overstock);
            })
            .OrderByDescending(d => d.Debt)
            .ToList();
        var debt = dealers.Sum(d => d.Debt);
        var totals = SystemMath.Buckets.Select((_, i) => dealers.Sum(d => d.Aging[i])).ToList();
        return new SystemMoneyView(dso.AsOf, SystemMath.Buckets, totals.Select(v => debt > 0 ? v / debt : 0).ToList(), debt, dealers.Sum(d => d.Overdue),
            dealers.Sum(d => d.Reserve), snapshot.SnapshotLocal, new DateOnly(months[0].Period.Year, months[0].Period.Month, 1), dealers);
    }

    private static Dictionary<string, decimal> Sum(IEnumerable<IReadOnlyDictionary<string, decimal>> maps) =>
        maps.SelectMany(m => m).GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(p => p.Value), StringComparer.OrdinalIgnoreCase);
}

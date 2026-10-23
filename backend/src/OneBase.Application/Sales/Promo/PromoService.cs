using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.SkuSales;

namespace OneBase.Application.Sales.Promo;

/// <summary>Акция или группа неразличимых акций раздела (Id &lt; −1 — группа): название, вид, сроки, участники группы.</summary>
public sealed record PromoGroup(long Id, string Name, string Kind, DateOnly? Begin, DateOnly? Till, IReadOnlyList<long> Members);

/// <summary>Всё, что нужно разделу «Акции» за месяцы года: строки акций, справочники, закупки точек, вторичка регионов и SKU.</summary>
public sealed record PromoBase(
    DateTimeOffset? AsOf,
    DateOnly LastData,
    IReadOnlyList<PromoMonth> Months,
    IReadOnlyDictionary<long, PromoGroup> Promos,
    IReadOnlyDictionary<string, string?> RmOfRegion,
    IReadOnlyList<string> RmOrder,
    IReadOnlyDictionary<long, string> Agents,
    IReadOnlyDictionary<long, string> Markets,
    IReadOnlyDictionary<long, IReadOnlyDictionary<string, decimal>> MarketPurchases,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> RegionSales,
    IReadOnlyDictionary<long, string> Products,
    IReadOnlyDictionary<long, string?> ProductCodes,
    SkuSalesAggregate Sku,
    IReadOnlyDictionary<Guid, string> RegionNames);

/// <summary>
/// «Акции»: скидки и бонусы вторички по акциям Linko — как вкладка «Акции» «Полевого контроля». Строки — доставленные заказы со скидкой по
/// дате приёмки, сумма — скидка строки (у бонуса — стоимость бонусного товара) за вычетом возвратов; акция строки восстанавливается
/// (PromoMatch). Знаменатели — вторичка OneBase (SkuSalesService, SalesAnalytics).
/// </summary>
public sealed class PromoService(
    IAppDbContext db,
    SalesOptions options,
    SalesDataLoader loader,
    SkuSalesService skuSales,
    IMemoryCache cache,
    SalesCacheSignal signal)
{
    private static readonly StringComparer Russian = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), true);
    private const int HistoryMonths = 6;

    public async Task<PromoView?> GetAsync(string? month, string? rm, IReadOnlyCollection<long>? promos, CancellationToken ct = default)
    {
        var data = await SalesViewCache.GetAsync(cache, signal, "sales:promo:base", () => BuildBaseAsync(ct));
        if (data.Months.Count == 0)
        {
            return null;
        }

        var current = data.Months.FirstOrDefault(m => m.Key == month) ?? data.Months[^1];
        var basis = (await loader.LoadAsync(current.Year, current.Month, ct)).PromoBasis();
        return PromoCompute.Build(data, current, string.IsNullOrWhiteSpace(rm) ? null : rm, promos ?? [], basis);
    }

    /// <summary>
    /// Акции заказов — для автоотчётов: строки со скидкой этих заказов сверяются с акциями Linko так же, как на странице «Акции» (PromoMatch).
    /// Ключ — заказ, значение — названия акций его строк («Ручная скидка» — скидка без акции). Заказов без строк со скидкой в ответе нет.
    /// </summary>
    public async Task<Dictionary<long, IReadOnlyList<string>>> OrderPromosAsync(IReadOnlyCollection<long> orderIds, CancellationToken ct = default)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        var ids = orderIds.ToArray();
        var raw = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where ids.Contains(o.Id) && l.TotalDiscount > 0 && l.ProductId != null
                select new { o.Id, o.CreatedDate, o.AcceptedDate, o.BranchId, o.BranchName, o.AgentId, o.MarketId, o.IsFullReturn,
                    ProductId = l.ProductId!.Value, l.Amount, l.ReturnAmount, l.TotalPrice, Discount = l.TotalDiscount!.Value, Percent = l.DiscountPercent ?? 0 })
            .ToListAsync(ct);
        if (raw.Count == 0)
        {
            return [];
        }

        var structure = SkuSalesService.RegionsOf(await db.SalesRegions.AsNoTracking().ToListAsync(ct), options);
        var regionNames = structure.Regions.ToDictionary(r => r.Id, r => r.Name);
        var regionList = regionNames.Values.ToList();
        var catalog = await CatalogAsync(ct);
        var names = catalog.ToDictionary(p => p.Id, p => p.Name);
        var lines = raw.Select(r => new PromoLine(r.Id, r.CreatedDate, r.AcceptedDate ?? r.CreatedDate, r.BranchId,
                r.BranchId is { } b && structure.BranchRegions.TryGetValue(b, out var region) && regionNames.TryGetValue(region, out var name) ? name : r.BranchName ?? "—",
                r.AgentId, r.MarketId, r.ProductId, r.Amount, r.ReturnAmount, r.TotalPrice, r.Discount, r.Percent, r.IsFullReturn))
            .ToList();
        var parts = PromoMatch.AllocateAll(lines, catalog, (promo, region) => PromoMatch.RegionMentioned(promo, region, regionList));
        string NameOf(IReadOnlyList<long> promos) =>
            promos.Count == 0 ? "Ручная скидка" : GroupName(promos.Select(p => names.GetValueOrDefault(p, $"Акция {p}")).ToList());
        return lines
            .SelectMany((line, i) => parts[i].Select(part => (line.OrderId, Name: NameOf(part.Promos))))
            .GroupBy(x => x.OrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).Distinct().ToList());
    }

    // ---------- Загрузка ----------

    /// <summary>Акции Linko с товарами: со скидкой или условием и бонусные.</summary>
    private async Task<List<PromoInfo>> CatalogAsync(CancellationToken ct)
    {
        var promoProducts = await db.LinkoPromotionProducts.AsNoTracking().ToListAsync(ct);
        return (await db.LinkoPromotions.AsNoTracking().ToListAsync(ct))
            .Select(p => new PromoInfo(p.Id, p.Name, p.Type, p.PromotionType, p.DiscountType, p.Discount, p.BeginDate, p.TillDate, p.IsApplyAll,
                promoProducts.Where(x => x.PromotionId == p.Id && x.Type != "bonus").GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.First().Amount),
                promoProducts.Where(x => x.PromotionId == p.Id && x.Type == "bonus").Select(x => x.ProductId).ToHashSet()))
            .ToList();
    }

    private async Task<PromoBase> BuildBaseAsync(CancellationToken ct)
    {
        var lastData = await loader.LastDataAsync(ct) ?? DateOnly.FromDateTime(DateTime.Today);
        var yearStart = new DateOnly(lastData.Year, 1, 1);
        var yearEnd = new DateOnly(lastData.Year, lastData.Month, 1).AddMonths(1).AddDays(-1);

        var regionRows = await db.SalesRegions.AsNoTracking().ToListAsync(ct);
        var structure = SkuSalesService.RegionsOf(regionRows, options);
        var regionNames = structure.Regions.ToDictionary(r => r.Id, r => r.Name);
        var directions = await db.SalesDirections.AsNoTracking().ToListAsync(ct);
        var rmOfRegion = structure.Regions.ToDictionary(
            r => r.Name,
            r => r.DirectionId is { } d && directions.FirstOrDefault(x => x.Id == d) is { } dir ? dir.ManagerName ?? dir.Name : (string?)null);
        var rmOrder = directions.OrderBy(d => d.SortOrder).Select(d => d.ManagerName ?? d.Name).Distinct().ToList();
        string RegionOf(long? branch, string? branchName) =>
            branch is { } b && structure.BranchRegions.TryGetValue(b, out var id) && regionNames.TryGetValue(id, out var name) ? name : branchName ?? "—";

        // Акции и товары.
        var catalog = await CatalogAsync(ct);
        var byId = catalog.ToDictionary(p => p.Id);
        var regionList = regionNames.Values.ToList();
        bool InName(string name, string region) => PromoMatch.RegionMentioned(name, region, regionList);

        // Строки со скидкой: доставленные по дате приёмки (год) и «отдан» последнего месяца («в пути»).
        var monthStart = new DateOnly(lastData.Year, lastData.Month, 1);
        var raw = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where l.TotalDiscount > 0 && l.ProductId != null
                      && ((o.Status == "delivered" && o.AcceptedDate >= yearStart && o.AcceptedDate <= yearEnd)
                          || (o.Status == "given" && o.CreatedDate >= monthStart && o.CreatedDate <= yearEnd))
                select new { o.Status, o.CreatedDate, o.AcceptedDate, o.BranchId, o.BranchName, o.AgentId, o.MarketId, o.IsFullReturn, o.Id,
                    ProductId = l.ProductId!.Value, l.Amount, l.ReturnAmount, l.TotalPrice, Discount = l.TotalDiscount!.Value, Percent = l.DiscountPercent ?? 0 })
            .ToListAsync(ct);

        var groups = new Dictionary<string, PromoGroup>();
        long GroupOf(IReadOnlyList<long> ids)
        {
            if (ids.Count == 0)
            {
                return PromoMatch.Manual;
            }

            if (ids.Count == 1)
            {
                return ids[0];
            }

            var key = string.Join(",", ids);
            if (!groups.TryGetValue(key, out var group))
            {
                var members = ids.Select(i => byId[i]).ToList();
                group = new PromoGroup(GroupId(key), GroupName(members.Select(m => m.Name).ToList()), KindOf(members[0]),
                    members.Min(m => m.Begin), members.Max(m => m.Till), ids);
                groups[key] = group;
            }

            return group.Id;
        }

        // Только филиалы вторички: «Завод» (прямые и экспортные отгрузки) и «К К Мерч» в акции дилеров не входят.
        raw = raw.Where(r => r.BranchId is { } b && structure.BranchRegions.ContainsKey(b)).ToList();
        var lines = raw.Select(r => (Raw: r, Line: new PromoLine(r.Id, r.CreatedDate, r.AcceptedDate ?? r.CreatedDate, r.BranchId, RegionOf(r.BranchId, r.BranchName),
            r.AgentId, r.MarketId, r.ProductId, r.Amount, r.ReturnAmount, r.TotalPrice, r.Discount, r.Percent, r.IsFullReturn))).ToList();
        // Строка — одна или несколько частей (сложенные скидки двух акций): у каждой своя акция и доля скидки строки.
        var parts = PromoMatch.AllocateAll(lines.Select(x => x.Line).ToList(), catalog, InName);

        // Пересекающиеся группы («Могул 5% A/B/C» и «Могул 5% A/B/C/E») — одна строка: состав группы — объединение.
        var merged = MergeGroups(parts.SelectMany(p => p).Select(p => p.Promos).Where(p => p.Count > 1));
        var attributed = lines
            .SelectMany((x, i) => parts[i].Select(part => (x.Raw, x.Line, Promo: GroupOf(part.Promos.Count > 1 ? merged[Key(part.Promos)] : part.Promos), part.Share)))
            .ToList();

        var promosOut = catalog.ToDictionary(p => p.Id, p => new PromoGroup(p.Id, p.Name, KindOf(p), p.Begin, p.Till, [p.Id]));
        foreach (var g in groups.Values)
        {
            promosOut[g.Id] = g;
        }

        string NameOf(long id) => promosOut.TryGetValue(id, out var g) ? g.Name : "";

        // Месяцы года: строки и товары по дню приёмки.
        var months = new List<PromoMonth>();
        for (var m = yearStart; m <= monthStart; m = m.AddMonths(1))
        {
            var mStart = m;
            var mEnd = m.AddMonths(1).AddDays(-1);
            var delivered = attributed.Where(x => x.Raw.Status == "delivered" && x.Line.Accepted >= mStart && x.Line.Accepted <= mEnd).ToList();
            var rows = delivered
                .Select(x =>
                {
                    var kept = x.Line.Kept;
                    var discount = x.Line.Discount * kept * x.Share;
                    var sale = x.Line.IsBonus ? (x.Promo >= 0 || x.Promo <= -1000 ? discount * PromoMatch.BonusSaleFactor(NameOf(x.Promo)) : 0) : x.Line.TotalPrice * kept;
                    var qty = x.Line.FullReturn ? 0 : Math.Max(0, x.Line.Amount - x.Line.ReturnAmount);
                    return (x.Line, x.Promo, discount, qty, sale);
                })
                .GroupBy(x => (x.Line.Accepted.Day, x.Line.Region, x.Promo, x.Line.AgentId, x.Line.MarketId))
                .Select(g => new PromoRow(g.Key.Day, g.Key.Region, g.Key.Promo, g.Key.AgentId, g.Key.MarketId, g.Sum(x => x.discount), g.Sum(x => x.qty), g.Sum(x => x.sale)))
                .ToList();
            var skuRows = delivered
                .GroupBy(x => (x.Line.Region, x.Promo, x.Line.ProductId))
                .Select(g => new PromoSkuRow(g.Key.Region, g.Key.Promo, g.Key.ProductId, g.Sum(x => x.Line.Discount * x.Line.Kept * x.Share),
                    g.Sum(x => x.Line.FullReturn ? 0 : Math.Max(0, x.Line.Amount - x.Line.ReturnAmount))))
                .ToList();
            var pending = mStart == monthStart
                ? attributed.Where(x => x.Raw.Status == "given")
                    .GroupBy(x => (x.Line.Region, x.Promo))
                    .Select(g => (g.Key.Region, g.Key.Promo, g.Sum(x => x.Line.Discount * x.Share)))
                    .ToList()
                : [];
            var days = mStart == monthStart ? lastData.Day : DateTime.DaysInMonth(m.Year, m.Month);
            months.Add(new PromoMonth(m.Year, m.Month, days, rows, skuRows, pending));
        }

        // Справочники: агенты, точки, товары.
        var agentIds = attributed.Select(x => x.Line.AgentId).Where(a => a != null).Select(a => a!.Value).Distinct().ToList();
        var agents = (await db.LinkoUsers.AsNoTracking().Where(u => agentIds.Contains(u.Id)).ToListAsync(ct)).ToDictionary(u => u.Id, u => u.DisplayName);
        var marketIds = attributed.Select(x => x.Line.MarketId).Where(a => a != null).Select(a => a!.Value).Distinct().ToList();
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => marketIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code }).ToListAsync(ct);

        // Закупки точек по месяцам (сум): «отдан» и доставленные по месяцу приёмки минус возвраты (доставленные) по месяцу возврата.
        var histFrom = monthStart.AddMonths(-(HistoryMonths - 1)) < yearStart ? yearStart : monthStart.AddMonths(-(HistoryMonths - 1));
        var bought = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where (o.Status == "delivered" || o.Status == "given") && o.AcceptedDate >= histFrom && o.AcceptedDate <= yearEnd
                      && o.MarketId != null && marketIds.Contains(o.MarketId.Value)
                group l by new { Market = o.MarketId!.Value, o.AcceptedDate!.Value.Year, o.AcceptedDate!.Value.Month } into g
                select new { g.Key.Market, g.Key.Year, g.Key.Month, Sum = g.Sum(x => x.TotalPrice) })
            .ToListAsync(ct);
        var returned = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                where r.Status == "delivered" && r.CreatedDate >= histFrom && r.CreatedDate <= yearEnd && r.MarketId != null && marketIds.Contains(r.MarketId.Value)
                group l by new { Market = r.MarketId!.Value, r.CreatedDate.Year, r.CreatedDate.Month } into g
                select new { g.Key.Market, g.Key.Year, g.Key.Month, Sum = g.Sum(x => x.Amount * x.Price) })
            .ToListAsync(ct);
        var purchases = bought.Select(b => (b.Market, Key: $"{b.Year}-{b.Month}", b.Sum))
            .Concat(returned.Select(r => (r.Market, Key: $"{r.Year}-{r.Month}", Sum: -r.Sum)))
            .GroupBy(x => x.Market)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, decimal>)g.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(y => y.Sum)));

        // Вторичка регионов по месяцам и SKU × регион × месяц — тем же агрегатом, что «Продажи по SKU».
        var sku = await skuSales.AggregateAsync(yearStart, lastData, structure.BranchRegions, ct);
        var regionSales = sku.Months
            .Where(x => x.RegionId is { } r && regionNames.ContainsKey(r))
            .GroupBy(x => $"{x.Year}-{x.Month}")
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, decimal>)g.GroupBy(x => regionNames[x.RegionId!.Value]).ToDictionary(x => x.Key, x => x.Sum(y => y.Revenue)));

        // Регионы-каналы («Урикзор», «Сети»): точки завода, отдельные регионы закрытых месяцев — их продажи по строкам проданных заказов
        // (как вторичка канала в SecondarySales); в агрегате по SKU их нет (филиал «Завод» не вторичка).
        var channelMarkets = options.ChannelRegions.SelectMany(c => c.Markets.Select(m => (Market: m, c.Name, Since: c.SinceMonth()))).ToList();
        if (channelMarkets.Count > 0)
        {
            var ids = channelMarkets.Select(x => x.Market).Distinct().ToList();
            var sold = options.SoldStatuses;
            var channelSales = await (
                    from l in db.LinkoOrderLines
                    join o in db.LinkoOrders on l.OrderId equals o.Id
                    where o.MarketId != null && ids.Contains(o.MarketId.Value) && sold.Contains(o.Status) && o.AcceptedDate >= yearStart && o.AcceptedDate < monthStart
                    group l by new { Market = o.MarketId!.Value, o.BranchName, o.AcceptedDate!.Value.Year, o.AcceptedDate!.Value.Month } into g
                    select new { g.Key.Market, g.Key.BranchName, g.Key.Year, g.Key.Month, Sum = g.Sum(x => x.TotalPrice) })
                .ToListAsync(ct);
            var byMonth = regionSales.ToDictionary(x => x.Key, x => x.Value.ToDictionary(y => y.Key, y => y.Value));
            foreach (var c in channelSales.Where(c => options.IsPrimaryOrderBranch(c.BranchName)))
            {
                var channel = channelMarkets.First(x => x.Market == c.Market);
                if (channel.Since is { } since && new DateOnly(c.Year, c.Month, 1) < since)
                {
                    continue; // канал — отдельный регион с месяца Since
                }

                var name = channel.Name.Trim();
                var key = $"{c.Year}-{c.Month}";
                if (!byMonth.TryGetValue(key, out var regionsOfMonth))
                {
                    byMonth[key] = regionsOfMonth = [];
                }

                regionsOfMonth[name] = regionsOfMonth.GetValueOrDefault(name) + c.Sum;
            }

            regionSales = byMonth.ToDictionary(x => x.Key, x => (IReadOnlyDictionary<string, decimal>)x.Value);
        }

        var asOf = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "orders", ct))?.LastSuccessAt;
        return new PromoBase(asOf, lastData, months, promosOut, rmOfRegion, rmOrder, agents, markets, purchases, regionSales,
            products.ToDictionary(p => p.Id, p => p.Name), products.ToDictionary(p => p.Id, p => p.Code), sku, regionNames);
    }

    private static string Key(IReadOnlyList<long> ids) => string.Join(",", ids);

    /// <summary>Группы с общими акциями сливаются (объединение составов): состав группы → итоговый состав.</summary>
    public static Dictionary<string, IReadOnlyList<long>> MergeGroups(IEnumerable<IReadOnlyList<long>> groups)
    {
        var parent = new Dictionary<long, long>();
        long Find(long x)
        {
            while (parent[x] != x)
            {
                x = parent[x] = parent[parent[x]];
            }

            return x;
        }

        var list = groups.DistinctBy(Key).ToList();
        foreach (var g in list)
        {
            foreach (var id in g)
            {
                parent.TryAdd(id, id);
            }

            foreach (var id in g.Skip(1))
            {
                parent[Find(id)] = Find(g[0]);
            }
        }

        var members = parent.Keys.GroupBy(Find).ToDictionary(x => x.Key, x => (IReadOnlyList<long>)x.Order().ToList());
        return list.ToDictionary(Key, g => members[Find(g[0])]);
    }

    /// <summary>Номер группы акций — от её состава (FNV-1a), меньше −1: не меняется между пересчётами, его можно держать в адресе страницы.</summary>
    public static long GroupId(string members)
    {
        var hash = 2166136261u;
        foreach (var ch in members)
        {
            hash = (hash ^ ch) * 16777619u;
        }

        return -1000 - (hash % 1_000_000_000);
    }

    /// <summary>Вид акции для таблицы: бонус товаром, ручная, скидка.</summary>
    private static string KindOf(PromoInfo p) => p.Type == "bonus" ? "bonus" : p.DiscountType == "manual" ? "manual" : "discount";

    /// <summary>Название группы: общее начало и различия через «/» — «Акция на Могул сентябрь 2026г. 8% "A" / "D" / "S"».</summary>
    public static string GroupName(IReadOnlyList<string> names)
    {
        if (names.Count == 1)
        {
            return names[0];
        }

        // Общее начало без учёта регистра («Акция на Могул Сентябрь» и «… сентябрь»), различия — через «/».
        var prefix = names.Aggregate((a, b) => new string(a.Zip(b).TakeWhile(p => char.ToLowerInvariant(p.First) == char.ToLowerInvariant(p.Second)).Select(p => p.First).ToArray()));
        var cut = prefix.LastIndexOf(' ');
        prefix = cut > 0 ? prefix[..(cut + 1)] : "";
        if (prefix.Length < 6)
        {
            return string.Join(" / ", names);
        }

        return prefix + string.Join(" / ", names.Select(n => n[prefix.Length..].Trim()).OrderBy(n => n, StringComparer.Ordinal));
    }
}

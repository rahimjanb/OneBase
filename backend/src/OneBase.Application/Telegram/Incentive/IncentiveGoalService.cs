using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>Заказ со стойкой Twizos (ДМП): ТП, точка, день, статус Linko, стоек в заказе.</summary>
public sealed record IncentiveStandOrder(long OrderId, long AgentId, long MarketId, DateOnly Created, string Status, int Count);

/// <summary>
/// Данные «Целей на маршрут» v2 по зеркалу Linko (только чтение): средний закуп Могул и фасовка спящей точки, история Twizos точки (брала ли,
/// обычная накладная), остатки стоек на складах регионов и квоты ТП, установки стоек, заказы Twizos с кешбеком по сегменту на момент заказа.
/// </summary>
public sealed class IncentiveGoalService(IAppDbContext db, SalesOptions sales, SalesDataLoader loader, IncentiveService incentive, IncentivePromoService promos)
{
    private static readonly string[] NotTaken = LinkoMirrorSalesProvider.NotTaken;

    /// <summary>Сумма строки по прайсу: прайс × количество, нет прайса — к оплате + скидка.</summary>
    private static decimal ListPrice(decimal? origin, decimal amount, decimal total, decimal? discount) =>
        origin is { } price && price > 0 ? price * amount : total + (discount ?? 0);

    /// <summary>
    /// Средний закуп Могул спящих точек (ТЗ v2, 4.2): кг Могула в доставленных заказах с начала года (ListFrom) до отсечки спящих (ListSince) ÷
    /// месяцев с покупкой; фасовка — какую точка брала больше по кг. Точек без покупок в ответе нет (средний закуп 0, фасовка — первая).
    /// </summary>
    public async Task<Dictionary<long, (decimal AvgKg, int Months, IncentiveMogulBox? Box)>> MogulHistoryAsync(IncentiveSettings s, IncentiveGoalsPromo g,
        IReadOnlyCollection<long> markets, CancellationToken ct)
    {
        if (s.Offers.Yamelly is not { } ym || markets.Count == 0)
        {
            return [];
        }

        var products = await promos.ProductsOfTypesAsync(ym.ProductTypeIds ?? [], ct);
        if (products.Length == 0)
        {
            return [];
        }

        var ids = markets.Distinct().ToArray();
        var sold = sales.FieldSoldStatuses;
        var (since, until) = (ym.ListFrom, ym.ListSince);
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.MarketId != null && ids.Contains(o.MarketId.Value) && sold.Contains(o.Status) && o.CreatedDate >= since && o.CreatedDate < until
                      && l.ProductId != null && products.Contains(l.ProductId.Value)
                select new { Market = o.MarketId!.Value, o.CreatedDate, o.BranchName, l.Amount, l.TotalWeight })
            .ToListAsync(ct);
        return rows.Where(r => promos.Counted(r.BranchName)).GroupBy(r => r.Market).ToDictionary(x => x.Key, x =>
        {
            var months = x.Select(r => (r.CreatedDate.Year, r.CreatedDate.Month)).Distinct().Count();
            var byUnit = x.Where(r => r.Amount > 0).GroupBy(r => Math.Round(r.TotalWeight / r.Amount, 3)).ToDictionary(u => u.Key, u => u.Sum(r => r.TotalWeight));
            return (months > 0 ? x.Sum(r => r.TotalWeight) / months : 0m, months, IncentiveGoalRules.MogulBox(g, byUnit));
        });
    }

    /// <summary>
    /// История Twizos точек до дня before (ТЗ v2, 4.3): брала ли Twizos с начала продаж (HistoryFrom; заказы кроме отменённых) и обычная накладная —
    /// сумма Twizos по прайсу за день с заказом ÷ таких дней (как в Excel-образце).
    /// </summary>
    public async Task<Dictionary<long, (bool Bought, decimal AvgInvoice)>> TwizosHistoryAsync(IncentiveSettings s, IncentiveGoalsPromo g, IReadOnlyCollection<long> markets,
        DateOnly before, CancellationToken ct)
    {
        if (markets.Count == 0)
        {
            return [];
        }

        var ids = markets.Distinct().ToArray();
        var twizos = await incentive.ProductIdsAsync(s, ct);
        var since = g.HistoryFrom;
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.MarketId != null && ids.Contains(o.MarketId.Value) && !NotTaken.Contains(o.Status) && o.CreatedDate >= since && o.CreatedDate < before
                      && l.ProductId != null && twizos.Contains(l.ProductId.Value)
                select new { Market = o.MarketId!.Value, o.CreatedDate, o.BranchName, l.Amount, l.OriginPrice, l.TotalPrice, l.TotalDiscount })
            .ToListAsync(ct);
        return rows.Where(r => promos.Counted(r.BranchName) && r.Amount > 0).GroupBy(r => r.Market).ToDictionary(x => x.Key, x =>
        {
            var days = x.Select(r => r.CreatedDate).Distinct().Count();
            return (true, days == 0 ? 0 : x.Sum(r => ListPrice(r.OriginPrice, r.Amount, r.TotalPrice, r.TotalDiscount)) / days);
        });
    }

    /// <summary>
    /// Остатки стоек Twizos на складах регионов (ДМП: колонка «На складе» = остаток товара стойки в Linko), без завода: регион → штук. Регион склада —
    /// StandStockRegions настроек («AAA» → Олмалик), иначе как в «Продажах».
    /// </summary>
    public async Task<Dictionary<string, int>> StandStockAsync(IncentiveGoalsPromo g, CancellationToken ct)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ids = (g.StandProductIds ?? []).ToArray();
        if (ids.Length == 0)
        {
            return result;
        }

        var stocks = await db.LinkoStocks.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var balances = await db.LinkoProductBalances.AsNoTracking().Where(b => ids.Contains(b.ProductId)).Select(b => new { b.StockId, b.Balance }).ToListAsync(ct);
        foreach (var b in balances)
        {
            if (!stocks.TryGetValue(b.StockId, out var name) || sales.IsFactoryStock(name) || sales.IsExportStock(name))
            {
                continue;
            }

            var region = g.StockRegion(name) ?? sales.StockRegionName(name);
            result[region] = result.GetValueOrDefault(region) + (int)Math.Floor(b.Balance);
        }

        foreach (var key in result.Keys.ToList())
        {
            result[key] = Math.Max(0, result[key]);
        }

        return result;
    }

    /// <summary>Остатки по складам (склад → вид → штук) — по регионам: без завода и экспорта; «AAA» — Олмалик (StandStockRegions).</summary>
    public Dictionary<string, Dictionary<string, decimal>> ByRegion(IncentiveGoalsPromo g, IReadOnlyDictionary<string, Dictionary<string, decimal>> byStock)
    {
        var result = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stock, items) in byStock)
        {
            if (sales.IsFactoryStock(stock) || sales.IsExportStock(stock))
            {
                continue;
            }

            var region = g.StockRegion(stock) ?? sales.StockRegionName(stock);
            if (!result.TryGetValue(region, out var into))
            {
                result[region] = into = [];
            }

            foreach (var (item, count) in items)
            {
                into[item] = into.GetValueOrDefault(item) + count;
            }
        }

        return result;
    }

    /// <summary>
    /// Квоты стоек по регионам оргструктуры (ТЗ v2, 4.3.1): остаток на складе региона (или ручная поправка), цель (0 → 40), ТП в команде региона,
    /// квота ТП = вверх(цель ÷ ТП). Установленная стойка уходит со склада — квота на следующий день меньше.
    /// </summary>
    public async Task<IReadOnlyList<IncentiveStandQuota>> StandQuotasAsync(IncentiveGoalsPromo g, DateOnly date, CancellationToken ct)
    {
        var teams = (await loader.LoadAsync(date.Year, date.Month, ct)).CachedReportTeams();
        var stock = await StandStockAsync(g, ct);
        return teams.Where(t => t.Agents.Count > 0).Select(t =>
        {
            var manual = g.ManualStock(t.Name);
            int? onHand = manual ?? (stock.TryGetValue(t.Name, out var v) ? v : null);
            var target = IncentiveGoalRules.StandTarget(g, onHand);
            return new IncentiveStandQuota(t.Name, onHand, target, t.Agents.Count, IncentiveGoalRules.StandQuota(target, t.Agents.Count), manual is not null);
        }).ToList();
    }

    /// <summary>Квота стоек ТП: квота региона его команды; ТП нет в оргструктуре — null (стойку не предлагаем).</summary>
    public async Task<IncentiveStandQuota?> AgentQuotaAsync(IncentiveGoalsPromo g, long agentId, DateOnly date, CancellationToken ct)
    {
        var teams = (await loader.LoadAsync(date.Year, date.Month, ct)).CachedReportTeams();
        var team = teams.FirstOrDefault(t => t.Agents.Any(a => a.Id == agentId));
        return team is null ? null : (await StandQuotasAsync(g, date, ct)).FirstOrDefault(q => q.Region == team.Name);
    }

    /// <summary>Заказы магазинов со стойкой Twizos с from по to (статусы ДМП: доставлено и выдано): ТП, точка, день, стоек.</summary>
    public async Task<List<IncentiveStandOrder>> StandOrdersAsync(IncentiveGoalsPromo g, IReadOnlyCollection<long>? agents, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var stands = (g.StandProductIds ?? []).ToArray();
        if (stands.Length == 0 || to < from)
        {
            return [];
        }

        var statuses = sales.DmpInstalledStatuses.Concat(sales.DmpIssuedStatuses).ToArray();
        var (first, last) = (from, to);
        var ids = agents?.Distinct().ToArray();
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.AgentId != null && o.MarketId != null && o.CreatedDate >= first && o.CreatedDate <= last && statuses.Contains(o.Status)
                      && l.ProductId != null && stands.Contains(l.ProductId.Value) && (ids == null || ids.Contains(o.AgentId.Value))
                select new { o.Id, Agent = o.AgentId!.Value, Market = o.MarketId!.Value, o.CreatedDate, o.Status, o.BranchName, l.Amount })
            .ToListAsync(ct);
        return rows.Where(r => promos.Counted(r.BranchName)).GroupBy(r => r.Id)
            .Select(x => new IncentiveStandOrder(x.Key, x.First().Agent, x.First().Market, x.First().CreatedDate, x.First().Status, (int)Math.Round(x.Sum(r => r.Amount))))
            .Where(o => o.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Заказы Twizos с кешбеком v2 (ТЗ v2, 5) с from по to внутри периода целей (в последний день — до until; agents = null — все ТП). Сумма Twizos
    /// по прайсу, после доставленных возвратов, стойка в заказе; сегмент на момент заказа — брала ли точка Twizos раньше этого заказа (заказы кроме
    /// отменённых с начала продаж): брала — 5%, не брала — 8%, со стойкой от 600 000 — 10%. Статус: отменён или полный возврат — cancelled;
    /// доставлен — confirmed (после возвратов ничего не осталось — cancelled); иначе issued. Без «Завода» и «К К Мерч».
    /// </summary>
    public async Task<List<IncentiveTwizosCashbackOrder>> TwizosCashbackOrdersAsync(IncentiveSettings s, IncentiveGoalsPromo g, IReadOnlyCollection<long>? agents,
        DateOnly from, DateOnly to, TimeOnly? until, CancellationToken ct)
    {
        var first = from < g.Start ? g.Start : from;
        var last = to > g.End ? g.End : to;
        if (last < first || agents is { Count: 0 })
        {
            return [];
        }

        var twizos = await incentive.ProductIdsAsync(s, ct);
        var stands = (g.StandProductIds ?? []).ToArray();
        var products = twizos.Concat(stands).Distinct().ToArray();
        var ids = agents?.Distinct().ToArray();
        var rows = await (from l in db.LinkoOrderLines.AsNoTracking()
                join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                where o.AgentId != null && o.MarketId != null && o.CreatedDate >= first && o.CreatedDate <= last
                      && l.ProductId != null && products.Contains(l.ProductId.Value) && (ids == null || ids.Contains(o.AgentId.Value))
                select new
                {
                    o.Id, Agent = o.AgentId!.Value, Market = o.MarketId!.Value, o.CreatedDate, o.CreatedAt, o.AcceptedDate, o.Status, o.IsFullReturn, o.BranchName,
                    Product = l.ProductId!.Value, l.Amount, l.OriginPrice, l.TotalPrice, l.TotalDiscount,
                })
            .ToListAsync(ct);
        rows = rows.Where(r => promos.Counted(r.BranchName) && (r.CreatedDate != last || until is not { } u || TimeOnly.FromDateTime(r.CreatedAt) < u)).ToList();
        var twizosSet = twizos.ToHashSet();
        var standSet = stands.ToHashSet();
        var orderIds = rows.Where(r => twizosSet.Contains(r.Product)).Select(r => r.Id).Distinct().ToArray();
        if (orderIds.Length == 0)
        {
            return [];
        }

        var back = sales.ReturnStatuses;
        var returned = (await (from rl in db.LinkoOrderReturnLines.AsNoTracking()
                    join r in db.LinkoOrderReturns.AsNoTracking() on rl.ReturnId equals r.Id
                    where back.Contains(r.Status) && rl.OrderId != null && orderIds.Contains(rl.OrderId.Value) && rl.ProductId != null && twizos.Contains(rl.ProductId.Value)
                    select new { Order = rl.OrderId!.Value, Product = rl.ProductId!.Value, rl.Amount }).ToListAsync(ct))
            .GroupBy(x => (x.Order, x.Product)).ToDictionary(x => x.Key, x => x.Sum(r => r.Amount));

        // Заказы Twizos тех же точек с начала продаж (кроме отменённых): брала ли точка Twizos раньше заказа.
        var markets = rows.Select(r => r.Market).Distinct().ToArray();
        var historyFrom = g.HistoryFrom;
        var history = (await (from l in db.LinkoOrderLines.AsNoTracking()
                    join o in db.LinkoOrders.AsNoTracking() on l.OrderId equals o.Id
                    where o.MarketId != null && markets.Contains(o.MarketId.Value) && !NotTaken.Contains(o.Status) && o.CreatedDate >= historyFrom && o.CreatedDate <= last
                          && l.ProductId != null && twizos.Contains(l.ProductId.Value) && l.Amount > 0
                    select new { Market = o.MarketId!.Value, o.Id, o.CreatedAt, o.BranchName }).Distinct().ToListAsync(ct))
            .Where(h => promos.Counted(h.BranchName))
            .GroupBy(h => h.Market).ToDictionary(x => x.Key, x => x.Select(h => (h.CreatedAt, h.Id)).ToList());
        var sold = sales.FieldSoldStatuses;

        return rows.Where(r => orderIds.Contains(r.Id)).GroupBy(r => r.Id).Select(x =>
        {
            var o = x.First();
            var lines = x.Where(r => twizosSet.Contains(r.Product)).ToList();
            var list = lines.Sum(r => ListPrice(r.OriginPrice, r.Amount, r.TotalPrice, r.TotalDiscount));
            var net = lines.Sum(r =>
            {
                var price = ListPrice(r.OriginPrice, r.Amount, r.TotalPrice, r.TotalDiscount);
                var gone = Math.Min(r.Amount, returned.GetValueOrDefault((o.Id, r.Product)));
                return r.Amount > 0 ? price * (r.Amount - gone) / r.Amount : 0;
            });
            var stand = x.Any(r => standSet.Contains(r.Product) && r.Amount > 0);
            var before = history.TryGetValue(o.Market, out var h) && h.Any(e => e.CreatedAt < o.CreatedAt || (e.CreatedAt == o.CreatedAt && e.Id < o.Id));
            var (segment, percent) = IncentiveGoalRules.CashbackSegment(g, before, stand, list);
            var status = NotTaken.Contains(o.Status) || o.IsFullReturn ? IncentiveCashbackStatus.Cancelled
                : sold.Contains(o.Status) ? net > 0 ? IncentiveCashbackStatus.Confirmed : IncentiveCashbackStatus.Cancelled
                : IncentiveCashbackStatus.Issued;
            return new IncentiveTwizosCashbackOrder(o.Id, o.Agent, o.Market, o.CreatedDate, o.CreatedAt, status == IncentiveCashbackStatus.Confirmed ? o.AcceptedDate : null,
                list, net, stand, segment, percent, IncentiveGoalRules.Cashback(list, percent), IncentiveGoalRules.Cashback(net, percent), status);
        }).Where(o => o.ListSum > 0).OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderId).ToList();
    }
}

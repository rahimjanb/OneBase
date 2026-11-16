using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Sales.Visits;

/// <summary>Строка заказа: товар, количество, кг, сумма.</summary>
public sealed record AgentDayLine(long? ProductId, string Product, decimal Qty, decimal Kg, decimal Sum);

/// <summary>Заказ ТП в точке за день: время ввода, статус Linko, сумма, вес, строки.</summary>
public sealed record AgentDayOrder(long Id, TimeOnly At, string Status, decimal Sum, decimal Kg, IReadOnlyList<AgentDayLine> Lines);

/// <summary>
/// Точка дня ТП. Status: done — визит выполнен; missed — точка маршрута прошедшего дня без визита; planned — точка маршрута сегодняшнего дня, визит
/// ещё впереди; no_visit — заказ без выполненного визита (по телефону или другим путём). At — отметка визита Linko (момент закрытия визита или
/// оформления заказа); GapMinutes — минут с прошлой отметки (время на прошлой точке плюс дорога). VisitNumber — какой по счёту это визит ТП в эту
/// точку после её последнего заказа у него. Без заказа — когда точка заказывала в последний раз и что она обычно берёт.
/// </summary>
public sealed record AgentDayStop(
    int No,
    long MarketId,
    string Market,
    string? Address,
    string? MarketType,
    double? Lat,
    double? Lon,
    string Status,
    bool InPlan,
    TimeOnly? At,
    int? GapMinutes,
    int? VisitNumber,
    IReadOnlyList<AgentDayOrder> Orders,
    decimal Sum,
    decimal Kg,
    DateOnly? LastOrderDay,
    IReadOnlyList<string> Usual,
    IReadOnlyList<string> UsualNotOrdered);

/// <summary>
/// Итог дня ТП: точки маршрута (план), выполнено (по маршруту и вне), пропущено, ещё впереди, визиты с заказом и страйк, заказы, сумма и кг,
/// заказы без визита, отменённые, первая и последняя отметка, рабочее окно, медиана и самый долгий промежуток между отметками.
/// </summary>
public sealed record AgentDaySummary(
    int Planned,
    int Done,
    int DoneInPlan,
    int OffPlan,
    int Missed,
    int Ahead,
    int WithOrder,
    double? Strike,
    int Orders,
    decimal Sum,
    decimal Kg,
    int OrdersWithoutVisit,
    int Cancelled,
    TimeOnly? FirstMark,
    TimeOnly? LastMark,
    int? SpanMinutes,
    int? MedianGapMinutes,
    int? LongestGapMinutes,
    string? LongestGapTo);

/// <summary>День ТП: кто, регион, день, итог, точки по порядку отметок (выполненные, затем пропущенные и предстоящие), замечания, свежесть визитов Linko.</summary>
public sealed record AgentDayView(
    long AgentId,
    string Agent,
    DateOnly Day,
    AgentDaySummary Summary,
    IReadOnlyList<AgentDayStop> Stops,
    IReadOnlyList<string> Notes,
    DateTimeOffset? VisitsSyncedAt,
    DateTimeOffset? OrdersSyncedAt);

/// <summary>
/// «День ТП»: куда ходил (визиты Linko: выполненные, пропущенные и предстоящие точки маршрута, вне маршрута), что продал (заказы того дня по точкам
/// со строками), что не продал (визиты без заказа: последний заказ точки и что она обычно берёт; в заказе — чего из обычного не взяли), время между
/// отметками. Только чтение зеркала Linko; права проверяет вызывающий (страница «Продаж», инструмент ИИ).
/// </summary>
public sealed class AgentDayService(IAppDbContext db, TimeProvider clock)
{
    public const string Cancelled = "cancelled";
    private const int HistoryDays = 120;
    private const int UsualDays = 60;
    private const int UsualShown = 5;

    public const string TimeNote =
        "Время на точке Linko не передаёт: у визита одна отметка — момент закрытия визита или оформления заказа. «С прошлой отметки» — время на " +
        "прошлой точке вместе с дорогой.";

    public async Task<AgentDayView?> BuildAsync(long agentId, DateOnly day, CancellationToken ct)
    {
        var user = await db.LinkoUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == agentId, ct);
        var visits = await db.LinkoVisits.AsNoTracking().Where(v => v.UserId == agentId && v.Day == day && v.MarketId != null)
            .Select(v => new { Market = v.MarketId!.Value, v.Date, v.Status, v.IsInPlan }).ToListAsync(ct);
        var orders = await db.LinkoOrders.AsNoTracking().Where(o => o.AgentId == agentId && o.CreatedDate == day && o.MarketId != null)
            .Select(o => new { o.Id, Market = o.MarketId!.Value, o.CreatedAt, o.Status, o.TotalPrice, o.TotalWeight }).ToListAsync(ct);
        if (user is null && visits.Count == 0 && orders.Count == 0)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).DateTime);
        var live = orders.Where(o => o.Status != Cancelled).ToList();
        var orderIds = live.Select(o => o.Id).ToList();
        var lines = await (from l in db.LinkoOrderLines.AsNoTracking()
                           where orderIds.Contains(l.OrderId)
                           join p in db.LinkoProducts.AsNoTracking() on l.ProductId equals (long?)p.Id into pp
                           from p in pp.DefaultIfEmpty()
                           select new { l.OrderId, l.ProductId, Name = p == null ? null : p.Name, l.Amount, l.TotalWeight, l.TotalPrice }).ToListAsync(ct);
        var linesByOrder = lines.ToLookup(l => l.OrderId);

        var markets = visits.Select(v => v.Market).Concat(live.Select(o => o.Market)).Distinct().ToList();
        var info = await db.LinkoMarkets.AsNoTracking().Where(m => markets.Contains(m.Id))
            .Select(m => new { m.Id, m.Name, m.Address, m.MarketTypeName, m.Lat, m.Lon }).ToDictionaryAsync(m => m.Id, ct);

        // История пар для номера визита и «последнего заказа»: визиты этого ТП и заказы точки (любого ТП) до дня.
        var since = day.AddDays(-HistoryDays);
        var pastVisits = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.UserId == agentId && v.Status == "done" && v.Day >= since && v.Day < day && v.MarketId != null && markets.Contains(v.MarketId!.Value))
            .Select(v => new { Market = v.MarketId!.Value, v.Day }).Distinct().ToListAsync(ct);
        var pastOrders = await db.LinkoOrders.AsNoTracking()
            .Where(o => o.Status != Cancelled && o.CreatedDate >= since && o.CreatedDate < day && o.MarketId != null && markets.Contains(o.MarketId!.Value))
            .Select(o => new { o.Id, Market = o.MarketId!.Value, Agent = o.AgentId, o.CreatedDate }).ToListAsync(ct);
        var usualFrom = day.AddDays(-UsualDays);
        var usualOrderIds = pastOrders.Where(o => o.CreatedDate >= usualFrom).Select(o => o.Id).ToList();
        var usualLines = await (from l in db.LinkoOrderLines.AsNoTracking()
                                where usualOrderIds.Contains(l.OrderId) && l.ProductId != null
                                join p in db.LinkoProducts.AsNoTracking() on l.ProductId equals (long?)p.Id into pp
                                from p in pp.DefaultIfEmpty()
                                select new { l.OrderId, ProductId = l.ProductId!.Value, Name = p == null ? null : p.Name }).ToListAsync(ct);
        var marketOfOrder = pastOrders.ToDictionary(o => o.Id, o => o.Market);
        var usualByMarket = usualLines.GroupBy(l => marketOfOrder[l.OrderId])
            .ToDictionary(g => g.Key, g => g.GroupBy(l => l.ProductId).Select(x => (Id: x.Key, Name: x.First().Name ?? $"Товар {x.Key}", Orders: x.Select(l => l.OrderId).Distinct().Count()))
                .OrderByDescending(x => x.Orders).ThenBy(x => x.Name, StringComparer.CurrentCulture).Take(UsualShown).ToList());

        int? VisitNumber(long market)
        {
            var lastOwnOrder = pastOrders.Where(o => o.Market == market && o.Agent == agentId).Select(o => (DateOnly?)o.CreatedDate).Max();
            return pastVisits.Count(v => v.Market == market && (lastOwnOrder is null || v.Day > lastOwnOrder)) + 1;
        }

        AgentDayOrder Order(long id, DateTime at, string status, decimal sum, decimal kg) => new(id, TimeOnly.FromDateTime(at), status, sum, kg,
            linesByOrder[id].Select(l => new AgentDayLine(l.ProductId, l.Name ?? (l.ProductId is { } p ? $"Товар {p}" : "—"), l.Amount, l.TotalWeight, l.TotalPrice))
                .OrderByDescending(l => l.Sum).ToList());

        AgentDayStop Stop(int no, long market, string status, bool inPlan, DateTime? at, int? gap, bool visited)
        {
            var own = live.Where(o => o.Market == market).OrderBy(o => o.CreatedAt).Select(o => Order(o.Id, o.CreatedAt, o.Status, o.TotalPrice, o.TotalWeight)).ToList();
            var m = info.GetValueOrDefault(market);
            var usual = usualByMarket.GetValueOrDefault(market) ?? [];
            var bought = own.SelectMany(o => o.Lines).Select(l => l.ProductId).OfType<long>().ToHashSet();
            return new AgentDayStop(no, market, m?.Name ?? $"ТТ {market}", m?.Address, m?.MarketTypeName, m?.Lat, m?.Lon, status, inPlan,
                at is { } t ? TimeOnly.FromDateTime(t) : null, gap, visited ? VisitNumber(market) : null, own, own.Sum(o => o.Sum), own.Sum(o => o.Kg),
                pastOrders.Where(o => o.Market == market).Select(o => (DateOnly?)o.CreatedDate).Max(),
                usual.Select(u => u.Name).ToList(),
                own.Count == 0 ? [] : usual.Where(u => !bought.Contains(u.Id)).Select(u => u.Name).Take(3).ToList());
        }

        // Выполненные — по времени отметки (повтор в той же точке — первая отметка), затем пропущенные и предстоящие точки маршрута, затем заказы без визита.
        var done = visits.Where(v => v.Status == "done").GroupBy(v => v.Market)
            .Select(g => new { Market = g.Key, At = g.Min(v => v.Date), InPlan = g.Any(v => v.IsInPlan) }).OrderBy(v => v.At).ToList();
        var doneMarkets = done.Select(v => v.Market).ToHashSet();
        var stops = new List<AgentDayStop>();
        DateTime? previous = null;
        var gaps = new List<(int Minutes, string To)>();
        foreach (var v in done)
        {
            int? gap = previous is { } p ? (int)Math.Round((v.At - p).TotalMinutes) : null;
            var stop = Stop(stops.Count + 1, v.Market, "done", v.InPlan, v.At, gap, true);
            if (gap is { } g)
            {
                gaps.Add((g, stop.Market));
            }

            stops.Add(stop);
            previous = v.At;
        }

        var pending = visits.Where(v => v.Status != "done" && v.IsInPlan && !doneMarkets.Contains(v.Market)).Select(v => v.Market).Distinct().ToList();
        stops.AddRange(pending.Select(market => Stop(stops.Count + 1, market, day < today ? "missed" : "planned", true, null, null, false))
            .OrderBy(s => s.Market, StringComparer.CurrentCulture).Select((s, i) => s with { No = done.Count + i + 1 }));
        var noVisit = live.Select(o => o.Market).Distinct().Where(m => !doneMarkets.Contains(m)).ToList();
        foreach (var market in noVisit)
        {
            var existing = stops.FindIndex(s => s.MarketId == market);
            if (existing >= 0)
            {
                continue; // пропущенная точка маршрута, где заказ всё же оформлен без визита: заказ уже в её строке
            }

            stops.Add(Stop(stops.Count + 1, market, "no_visit", false, null, null, false));
        }

        var planned = visits.Where(v => v.IsInPlan).Select(v => v.Market).Distinct().Count();
        var withOrder = stops.Count(s => s.Status == "done" && s.Orders.Count > 0);
        var marks = done.Select(v => v.At).ToList();
        var sortedGaps = gaps.Select(g => g.Minutes).Order().ToList();
        var longest = gaps.Count == 0 ? ((int Minutes, string To)?)null : gaps.MaxBy(g => g.Minutes);
        var summary = new AgentDaySummary(
            planned,
            done.Count,
            done.Count(v => v.InPlan),
            done.Count(v => !v.InPlan),
            stops.Count(s => s.Status == "missed"),
            stops.Count(s => s.Status == "planned"),
            withOrder,
            done.Count == 0 ? null : (double)withOrder / done.Count,
            live.Count,
            live.Sum(o => o.TotalPrice),
            live.Sum(o => o.TotalWeight),
            live.Count(o => !doneMarkets.Contains(o.Market)),
            orders.Count(o => o.Status == Cancelled),
            marks.Count == 0 ? null : TimeOnly.FromDateTime(marks.Min()),
            marks.Count == 0 ? null : TimeOnly.FromDateTime(marks.Max()),
            marks.Count < 2 ? null : (int)Math.Round((marks.Max() - marks.Min()).TotalMinutes),
            sortedGaps.Count == 0 ? null : sortedGaps[sortedGaps.Count / 2],
            longest?.Minutes,
            longest?.To);

        var notes = new List<string> { TimeNote };
        if (day >= today)
        {
            notes.Add("День ещё идёт: точки маршрута без визита — «впереди», данные Linko — на момент последней загрузки.");
        }

        if (visits.Count == 0 && live.Count == 0)
        {
            notes.Add("В этот день у ТП нет ни визитов, ни заказов в Linko.");
        }

        var sync = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "visits" || s.Entity == "orders").Select(s => new { s.Entity, s.LastSuccessAt })
            .ToListAsync(ct);
        return new AgentDayView(agentId, user is { } u && !string.IsNullOrWhiteSpace(u.DisplayName) ? u.DisplayName : $"ТП {agentId}", day, summary, stops, notes,
            sync.FirstOrDefault(s => s.Entity == "visits")?.LastSuccessAt, sync.FirstOrDefault(s => s.Entity == "orders")?.LastSuccessAt);
    }
}

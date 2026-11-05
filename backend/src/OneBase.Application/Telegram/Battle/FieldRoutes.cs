using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;

namespace OneBase.Application.Telegram.Battle;

/// <summary>
/// Маршрут ТП без плана визитов Linko — общий для «Поля боя» и накопительной акции: обе карточки дня строят один и тот же собственный
/// маршрут. Только чтение Linko.
/// </summary>
public static class FieldRoutes
{
    /// <summary>
    /// Собственный маршрут (нет плана в Linko): точки, где ТП ответственный, по дню недели — чаще всего в плане за 8 недель, иначе чаще всего
    /// заказывали за 90 дней, иначе ровно по дням.
    /// </summary>
    public static async Task<Dictionary<long, DayOfWeek>> OwnWeekdaysAsync(IAppDbContext db, SalesOptions sales, long agentId, DateOnly monday, CancellationToken ct)
    {
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => m.ResponsibleAgentId == agentId).Select(m => m.Id).ToListAsync(ct);
        if (markets.Count == 0)
        {
            return [];
        }

        var ids = markets.ToArray();
        var since = monday.AddDays(-56);
        var planned = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.UserId == agentId && v.MarketId != null && ids.Contains(v.MarketId.Value) && v.IsInPlan && v.Day >= since && v.Day < monday)
                .Select(v => new { MarketId = v.MarketId!.Value, v.Day }).ToListAsync(ct))
            .GroupBy(v => v.MarketId)
            .ToDictionary(g => g.Key, g => g.GroupBy(v => v.Day.DayOfWeek).OrderByDescending(x => x.Count()).First().Key);
        var sold = sales.FieldSoldStatuses;
        var orderSince = monday.AddDays(-90);
        var ordered = (await db.LinkoOrders.AsNoTracking()
                .Where(o => o.MarketId != null && ids.Contains(o.MarketId.Value) && sold.Contains(o.Status) && o.AcceptedDate != null && o.AcceptedDate >= orderSince && o.AcceptedDate < monday)
                .Select(o => new { MarketId = o.MarketId!.Value, Day = o.AcceptedDate!.Value }).ToListAsync(ct))
            .GroupBy(o => o.MarketId)
            .ToDictionary(g => g.Key, g => g.GroupBy(o => o.Day.DayOfWeek).Where(x => x.Key != DayOfWeek.Sunday).OrderByDescending(x => x.Count()).Select(x => (DayOfWeek?)x.Key).FirstOrDefault());
        DayOfWeek[] workDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];
        return markets.ToDictionary(id => id, id =>
            planned.TryGetValue(id, out var p) ? p : ordered.TryGetValue(id, out var o) && o is { } day ? day : workDays[(int)(id % 6)]);
    }
}

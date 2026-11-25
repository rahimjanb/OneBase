using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Battle;
using OneBase.Domain.Incentive;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>Итог обновления журнала кешбека: строк добавлено, изменено, всего по видам.</summary>
public sealed record IncentiveLedgerRefresh(int Added, int Updated, int Mogul, int Twizos);

/// <summary>
/// Журнал начислений кешбека ТП (incentive.CashbackAccruals; ТЗ v2, раздел 5): Могул (YM-02) и Twizos — два отдельных баланса. Одна строка на
/// «акция × вид × заказ Linko»: повторная синхронизация обновляет сумму и статус по заказу и возвратам, а не начисляет второй раз. Сегмент и %
/// Twizos фиксируются при первой записи заказа. Строка, которую расчёт больше не возвращает (точку перезакрепили, список пересчитан), остаётся как
/// была: начисление — за тем ТП, кто его заработал. Регионы без Могула (YM-03) в журнал Могула не попадают.
/// </summary>
public sealed class IncentiveCashbackLedger(IAppDbContext db, IncentivePromoService promos, IncentiveGoalService goals, TimeProvider time)
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTimeOffset> Refreshed = new();

    /// <summary>
    /// Обновить журнал, если его не обновляли дольше maxAge (в этом процессе): карточки строятся по одной на получателя — журнал перед рассылкой
    /// обновляется один раз, а не сотни. Факт дня на карточке считается по заказам напрямую, журнал даёт балансы.
    /// </summary>
    public async Task RefreshIfStaleAsync(IncentiveSettings s, DateOnly today, TimeSpan maxAge, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (Refreshed.TryGetValue(s.Id, out var at) && now - at < maxAge && now >= at)
        {
            return;
        }

        await RefreshAsync(s, today, ct);
        Refreshed[s.Id] = now;
    }

    /// <summary>
    /// Обновить журнал по заказам Linko: Могул — с начала YM-01 по сегодня (до конца акции), Twizos — с начала целей v2. Если строки этих заказов уже
    /// записал другой экземпляр (уникальный индекс), расчёт повторяется по свежему журналу.
    /// </summary>
    public async Task<IncentiveLedgerRefresh> RefreshAsync(IncentiveSettings s, DateOnly today, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await RefreshOnceAsync(s, today, ct);
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                foreach (var entry in db.ChangeTracker.Entries<IncentiveCashbackAccrual>().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }

    private async Task<IncentiveLedgerRefresh> RefreshOnceAsync(IncentiveSettings s, DateOnly today, CancellationToken ct)
    {
        var fresh = new List<IncentiveCashbackAccrual>();
        var g = s.Offers.Goals;
        if (s.Offers.Yamelly is { } ym && today >= ym.Start)
        {
            var listed = (await promos.SleepingListAsync(s, ct)).Select(x => x.MarketId).ToArray();
            var agents = await db.LinkoMarkets.AsNoTracking().Where(m => m.ResponsibleAgentId != null && listed.Contains(m.Id))
                .Select(m => m.ResponsibleAgentId!.Value).Distinct().ToListAsync(ct);
            var orders = (await promos.YamellyOrdersAsync(s, agents, ym.Start, today < ym.End ? today : ym.End, null, ct)).Where(o => o.Qualifies).ToList();
            var markets = orders.Select(o => o.MarketId).Distinct().ToArray();
            var branches = await db.LinkoMarkets.AsNoTracking().Where(m => markets.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.BranchName, ct);
            foreach (var o in orders.Where(o => g is null || !g.MogulExcluded(branches.GetValueOrDefault(o.MarketId))))
            {
                fresh.Add(new IncentiveCashbackAccrual
                {
                    ProgramId = s.Id, Kind = IncentiveCashbackKind.Mogul, OrderId = o.OrderId, AgentId = o.AgentId, MarketId = o.MarketId, OrderDate = o.Created,
                    OrderCreatedAt = o.CreatedAt, Segment = "sleeping", Percent = ym.CashbackPercent, ListSum = o.ListSum,
                    BaseSum = IncentivePromoRules.AfterDiscount(ym, o.ListSum), NetSum = IncentivePromoRules.AfterDiscount(ym, o.NetListSum), Issued = o.Cashback,
                    Amount = o.CashbackStatus switch
                    {
                        IncentiveCashbackStatus.Cancelled => 0,
                        IncentiveCashbackStatus.Confirmed => o.NetCashback,
                        _ => o.Cashback,
                    },
                    Status = o.CashbackStatus, DeliveredOn = o.CashbackStatus == IncentiveCashbackStatus.Confirmed ? o.Accepted : null,
                });
            }
        }

        if (g is not null && today >= g.Start)
        {
            foreach (var o in await goals.TwizosCashbackOrdersAsync(s, g, null, g.Start, today, null, ct))
            {
                fresh.Add(new IncentiveCashbackAccrual
                {
                    ProgramId = s.Id, Kind = IncentiveCashbackKind.Twizos, OrderId = o.OrderId, AgentId = o.AgentId, MarketId = o.MarketId, OrderDate = o.Created,
                    OrderCreatedAt = o.CreatedAt, Segment = o.Segment, Percent = o.Percent, ListSum = o.ListSum, BaseSum = o.ListSum, NetSum = o.NetSum,
                    Issued = o.Cashback,
                    Amount = o.Status switch
                    {
                        IncentiveCashbackStatus.Cancelled => 0,
                        IncentiveCashbackStatus.Confirmed => o.NetCashback,
                        _ => o.Cashback,
                    },
                    Status = o.Status, DeliveredOn = o.Delivered, Stand = o.Stand,
                });
            }
        }

        var existing = await db.IncentiveCashbackAccruals.Where(a => a.ProgramId == s.Id).ToDictionaryAsync(a => (a.Kind, a.OrderId), ct);
        var now = time.GetUtcNow();
        int added = 0, updated = 0;
        foreach (var row in fresh)
        {
            if (!existing.TryGetValue((row.Kind, row.OrderId), out var have))
            {
                row.CreatedAt = now;
                db.IncentiveCashbackAccruals.Add(row);
                added++;
                continue;
            }

            // Сегмент и % — на момент заказа (первая запись); сумма и статус — по заказу сейчас.
            var percent = have.Percent;
            var issued = row.Kind == IncentiveCashbackKind.Twizos ? IncentiveGoalRules.Cashback(row.BaseSum, percent) : row.Issued;
            var amount = row.Status switch
            {
                IncentiveCashbackStatus.Cancelled => 0,
                IncentiveCashbackStatus.Confirmed => row.Kind == IncentiveCashbackKind.Twizos ? IncentiveGoalRules.Cashback(row.NetSum, percent) : row.Amount,
                _ => issued,
            };
            if (have.Status == row.Status && have.ListSum == row.ListSum && have.NetSum == row.NetSum && have.Amount == amount && have.Issued == issued
                && have.DeliveredOn == row.DeliveredOn && have.AgentId == row.AgentId && have.Stand == row.Stand)
            {
                continue;
            }

            have.AgentId = row.AgentId;
            have.ListSum = row.ListSum;
            have.BaseSum = row.BaseSum;
            have.NetSum = row.NetSum;
            have.Issued = issued;
            have.Amount = amount;
            have.Status = row.Status;
            have.DeliveredOn = row.DeliveredOn;
            have.Stand = row.Stand;
            have.UpdatedAt = now;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries<IncentiveCashbackAccrual>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        return new IncentiveLedgerRefresh(added, updated, fresh.Count(r => r.Kind == IncentiveCashbackKind.Mogul), fresh.Count(r => r.Kind == IncentiveCashbackKind.Twizos));
    }

    /// <summary>Строки журнала вида kind у ТП с заказами по день asOf (agents = null — все).</summary>
    public async Task<List<IncentiveCashbackAccrual>> RowsAsync(IncentiveSettings s, string kind, IReadOnlyCollection<long>? agents, DateOnly from, DateOnly asOf,
        CancellationToken ct)
    {
        var ids = agents?.Distinct().ToArray();
        return await db.IncentiveCashbackAccruals.AsNoTracking()
            .Where(a => a.ProgramId == s.Id && a.Kind == kind && a.OrderDate >= from && a.OrderDate <= asOf && (ids == null || ids.Contains(a.AgentId)))
            .OrderBy(a => a.OrderCreatedAt).ThenBy(a => a.OrderId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Баланс кешбека вида kind по ТП на день asOf (ТЗ v2, 5): оформлено (не отменено, при оформлении), ждёт доставки, подтверждено (доставлено, после
    /// возвратов), отменено, выплачено (отметки бухгалтерии этого вида), к выплате — подтверждённое за прошедшие недели пн–вс (по неделе доставки)
    /// минус выплаченное за них; недели. Listed, Woken и CanEarn заполняет тот, кто знает цели (карточка).
    /// </summary>
    public async Task<Dictionary<long, IncentiveCashbackBalance>> BalancesAsync(IncentiveSettings s, string kind, IReadOnlyCollection<long> agents, DateOnly asOf,
        CancellationToken ct)
    {
        var ids = agents.Distinct().ToArray();
        var rows = await RowsAsync(s, kind, ids, DateOnly.MinValue, asOf, ct);
        var payoutKind = IncentiveCashbackKind.PayoutKind(kind);
        var paid = await db.IncentivePayouts.AsNoTracking().Where(p => p.ProgramId == s.Id && ids.Contains(p.AgentId) && p.Kind == payoutKind)
            .Select(p => new { p.AgentId, p.WeekStart, p.Amount }).ToListAsync(ct);
        var thisWeek = BattleRules.Monday(asOf);
        var result = new Dictionary<long, IncentiveCashbackBalance>();
        foreach (var agent in ids)
        {
            var own = rows.Where(r => r.AgentId == agent).ToList();
            var payouts = paid.Where(p => p.AgentId == agent).ToList();
            var confirmed = own.Where(r => r.Status == IncentiveCashbackStatus.Confirmed && r.DeliveredOn is { } d && d <= asOf).ToList();
            var mondays = own.Select(r => BattleRules.Monday(r.OrderDate)).Concat(confirmed.Select(r => BattleRules.Monday(r.DeliveredOn!.Value)))
                .Concat(payouts.Select(p => p.WeekStart)).Distinct().Order().ToList();
            var weeks = mondays.Select(m => new IncentiveCashbackWeek(m, 0,
                own.Where(r => r.Status != IncentiveCashbackStatus.Cancelled && BattleRules.Monday(r.OrderDate) == m).Sum(r => r.Issued),
                confirmed.Where(r => BattleRules.Monday(r.DeliveredOn!.Value) == m).Sum(r => r.Amount),
                payouts.Where(p => p.WeekStart == m).Sum(p => p.Amount))).ToList();
            result[agent] = new IncentiveCashbackBalance(agent, 0, 0,
                own.Where(r => r.Status != IncentiveCashbackStatus.Cancelled).Sum(r => r.Issued),
                own.Where(r => r.Status != IncentiveCashbackStatus.Cancelled && !confirmed.Contains(r)).Sum(r => r.Issued),
                confirmed.Sum(r => r.Amount),
                own.Where(r => r.Status == IncentiveCashbackStatus.Cancelled).Sum(r => r.Issued),
                payouts.Sum(p => p.Amount),
                weeks.Where(w => w.Monday < thisWeek).Sum(w => w.ToPay),
                0,
                weeks);
        }

        return result;
    }
}

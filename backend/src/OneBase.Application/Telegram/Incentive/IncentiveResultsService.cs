using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Incentive;

namespace OneBase.Application.Telegram.Incentive;

/// <summary>Итог пересчёта списка спящих: добавлено, исключено, возвращено, осталось, всего в списке.</summary>
public sealed record IncentiveSleepingRefresh(int Added, int Removed, int Restored, int Kept, int Active);

/// <summary>Закрытие дня по расписанию: записано итогов ТП; в субботу — пересчёт списка спящих на следующую неделю.</summary>
public sealed record IncentiveDayClose(string Program, DateOnly Date, int Written, IncentiveSleepingRefresh? Sleeping);

/// <summary>
/// История расчётов акций ноября: итог дня ТП (incentive.DayResults) фиксируется при закрытии дня — кг Twizos (в т. ч. вне маршрута), наборы и
/// подарки, спящие в маршруте и разбуженные, кэшбек дня, место и победа TW-01. Повторное закрытие того же дня перезаписывает итог (заказы дня
/// могли догрузиться). Отметки выплат бухгалтерии — incentive.Payouts. Снимки списка спящих из Linko — incentive.SleepingMarkets.
/// </summary>
public sealed class IncentiveResultsService(IAppDbContext db, IncentiveService incentive, IncentivePromoService promos, TimeProvider time)
{
    /// <summary>Закрытие дня (ТЗ, раздел 5): 19:00 по Ташкенту — после итога дня ТП 18:00 и до итога команды СВР 19:30.</summary>
    public static readonly TimeOnly CloseAt = new(19, 0);

    /// <summary>
    /// Закрыть день по расписанию (служба автоотчётов, с 19:00, пн–сб): действующая акция с акциями ноября, день ещё не закрыт после 19:00 —
    /// итоги ТП за день и TW-01; в субботу — список спящих на следующую неделю (по Linko на воскресенье, изменения — с понедельника). Нет акции или
    /// день уже закрыт (другой экземпляр, перезапуск) — null.
    /// </summary>
    public async Task<IncentiveDayClose?> CloseDueAsync(DateOnly date, IReadOnlyCollection<long> participants, CancellationToken ct)
    {
        if (await incentive.ActiveAsync(date, ct) is not { } s || s.Offers is { Twizos: null, Yamelly: null, Best: null })
        {
            return null;
        }

        var closeFrom = Reports.ReportSchedules.At(date, CloseAt, Reports.ReportSchedules.DefaultTimeZone).ToUniversalTime();
        if (await db.IncentiveDayResults.AnyAsync(r => r.ProgramId == s.Id && r.Date == date && r.ClosedAt >= closeFrom, ct))
        {
            return null;
        }

        var written = await CloseDayAsync(s, participants, date, ct);
        IncentiveSleepingRefresh? sleeping = null;
        if (date.DayOfWeek == DayOfWeek.Saturday && s.Offers.Yamelly is { } ym && date.AddDays(2) <= ym.End)
        {
            sleeping = await RefreshSleepingAsync(s, date.AddDays(1), date.AddDays(2), null, ct);
        }

        return new IncentiveDayClose(s.Code, date, written, sleeping);
    }

    /// <summary>Снимок списка спящих, если его ещё нет: по Linko на начало Yamelly (первая карточка по расписанию с начала акции).</summary>
    public async Task EnsureSleepingAsync(IncentiveSettings s, DateOnly date, CancellationToken ct)
    {
        if (s.Offers.Yamelly is not { } ym || date < ym.Start || await db.IncentiveSleepingMarkets.AnyAsync(x => x.ProgramId == s.Id, ct))
        {
            return;
        }

        await RefreshSleepingAsync(s, ym.Start, ym.Start, null, ct);
    }

    /// <summary>
    /// Пересчитать список спящих по Linko на день asOf, изменения — с listedOn. Новые спящие добавляются, исключённые и снова спящие —
    /// возвращаются. Точка списка, которая с дня включения брала Yamelly (разбужена или взяла меньше порога), остаётся: по ней кэшбек и счёт
    /// «разбужено». Точка, которая по данным Linko больше не спящая и Yamelly с включения не брала (заказ догрузился задним числом), исключается
    /// с listedOn — история кэшбека сохраняется.
    /// </summary>
    public async Task<IncentiveSleepingRefresh> RefreshSleepingAsync(IncentiveSettings s, DateOnly asOf, DateOnly listedOn, Guid? userId, CancellationToken ct)
    {
        if (s.Offers.Yamelly is not { } ym)
        {
            return new IncentiveSleepingRefresh(0, 0, 0, 0, 0);
        }

        if (listedOn > ym.Start && !await db.IncentiveSleepingMarkets.AnyAsync(x => x.ProgramId == s.Id, ct))
        {
            // Снимка ещё нет: сначала список на начало Yamelly — иначе точки, разбуженные до пересчёта, выпали бы из списка вместе с кэшбеком.
            await RefreshSleepingAsync(s, ym.Start, ym.Start, null, ct);
        }

        var scan = await promos.ScanSleepingAsync(s, asOf, ct);
        var rows = await db.IncentiveSleepingMarkets.Where(x => x.ProgramId == s.Id).ToListAsync(ct);
        var byId = rows.ToDictionary(x => x.MarketId);
        var now = time.GetUtcNow();
        int added = 0, removed = 0, restored = 0, kept = 0;
        foreach (var row in rows.Where(x => x.RemovedOn is null))
        {
            if (scan.Sleeping.Contains(row.MarketId) || (scan.BoughtSince.TryGetValue(row.MarketId, out var bought) && bought >= row.ListedOn))
            {
                kept++;
                continue;
            }

            row.RemovedOn = listedOn;
            row.UpdatedAt = now;
            removed++;
        }

        foreach (var id in scan.Sleeping)
        {
            if (byId.TryGetValue(id, out var row))
            {
                if (row.RemovedOn is not null)
                {
                    row.RemovedOn = null;
                    row.ListedOn = listedOn;
                    row.UpdatedAt = now;
                    restored++;
                }

                continue;
            }

            db.IncentiveSleepingMarkets.Add(new IncentiveSleepingMarket { ProgramId = s.Id, MarketId = id, ListedOn = listedOn, UploadedById = userId, CreatedAt = now });
            added++;
        }

        await db.SaveChangesAsync(ct);
        promos.ForgetSleepingList(s.Id);
        return new IncentiveSleepingRefresh(added, removed, restored, kept, kept + added + restored);
    }

    /// <summary>Закрыть день date для участников: записать итоги тех, у кого были цели, заказы Twizos или спящие. Возвращает число записанных ТП.</summary>
    public async Task<int> CloseDayAsync(IncentiveSettings s, IReadOnlyCollection<long> participants, DateOnly date, CancellationToken ct)
    {
        var ids = participants.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        var taken = await incentive.TakenAsync(s, ids, date, date, null, ct);
        var best = s.Offers.Best is { } b && b.Covers(date)
            ? IncentivePromoRules.BestOfDay(b, ids.Select(a => (a, s.InWholesaleGroup(a), taken.GetValueOrDefault((a, date))))).ToDictionary(x => x.AgentId)
            : [];
        var twizos = await promos.TwizosOrdersAsync(s, ids, date, date, null, ct);
        var yamelly = await promos.YamellyOrdersAsync(s, ids, date, date, null, ct);
        var targets = await db.IncentiveRouteTargets.AsNoTracking().Where(t => t.ProgramId == s.Id && t.Date == date && ids.Contains(t.AgentId))
            .Select(t => new { t.AgentId, t.MarketId, t.Sleeping, t.Placement }).ToListAsync(ct);
        var existing = await db.IncentiveDayResults.Where(r => r.ProgramId == s.Id && r.Date == date && ids.Contains(r.AgentId)).ToDictionaryAsync(r => r.AgentId, ct);
        var now = time.GetUtcNow();
        var written = 0;
        foreach (var agent in ids)
        {
            var own = targets.Where(t => t.AgentId == agent).ToList();
            var routeIds = own.Select(t => t.MarketId).ToHashSet();
            var orders = twizos.Where(o => o.AgentId == agent).ToList();
            var woken = yamelly.Where(o => o.AgentId == agent && o.Qualifies && o.CashbackStatus != IncentiveCashbackStatus.Cancelled).ToList();
            var kg = taken.GetValueOrDefault((agent, date));
            if (own.Count == 0 && orders.Count == 0 && woken.Count == 0 && kg == 0)
            {
                continue;
            }

            var row = existing.GetValueOrDefault(agent) ?? new IncentiveDayResult { ProgramId = s.Id, AgentId = agent, Date = date };
            var gifts = orders.Where(o => o.Gift is not null).GroupBy(o => o.Gift!).ToDictionary(g => g.Key, g => g.Count());
            var mine = best.GetValueOrDefault(agent);
            row.Wholesale = s.InWholesaleGroup(agent);
            row.TwizosKg = kg;
            row.OutOfRouteKg = orders.Where(o => !routeIds.Contains(o.MarketId)).Sum(o => o.Kg);
            row.Sets = gifts.Values.Sum();
            row.Mugs = gifts.GetValueOrDefault("mug");
            row.Thermoses = gifts.GetValueOrDefault("thermos");
            row.Kettles = gifts.GetValueOrDefault("kettle");
            row.SleepingPlanned = own.Count(t => t.Sleeping && t.Placement == IncentivePlacement.Route);
            row.SleepingWoken = woken.Count;
            row.Cashback = woken.Sum(o => o.Cashback);
            row.BestPlace = mine?.Place;
            row.BestWinner = mine?.Winner ?? false;
            row.BestPrize = mine?.Winner == true ? s.Offers.Best!.Prize : 0;
            row.ClosedAt = now;
            if (!existing.ContainsKey(agent))
            {
                row.CreatedAt = now;
                db.IncentiveDayResults.Add(row);
            }
            else
            {
                row.UpdatedAt = now;
            }

            written++;
        }

        await db.SaveChangesAsync(ct);
        return written;
    }

    /// <summary>
    /// Отметить выплату ТП за неделю (понедельник weekStart): вид — кэшбек YM-02 или призы TW-01; сумма 0 — снять отметку. Повторная отметка той
    /// же недели и вида — замена суммы.
    /// </summary>
    public async Task SetPayoutAsync(IncentiveSettings s, long agentId, DateOnly weekStart, string kind, decimal amount, DateOnly paidOn, string? note, Guid? userId,
        CancellationToken ct)
    {
        if (kind is not (IncentivePayoutKind.Cashback or IncentivePayoutKind.Best) || amount < 0 || weekStart != Battle.BattleRules.Monday(weekStart))
        {
            throw new ArgumentException("Выплата: вид cashback или best, сумма не меньше нуля, неделя — с понедельника.");
        }

        var row = await db.IncentivePayouts.FirstOrDefaultAsync(p => p.ProgramId == s.Id && p.AgentId == agentId && p.WeekStart == weekStart && p.Kind == kind, ct);
        if (amount == 0)
        {
            if (row is not null)
            {
                db.IncentivePayouts.Remove(row);
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        if (row is null)
        {
            row = new IncentivePayout { ProgramId = s.Id, AgentId = agentId, WeekStart = weekStart, Kind = kind };
            db.IncentivePayouts.Add(row);
        }
        else
        {
            row.UpdatedAt = time.GetUtcNow();
        }

        row.Amount = amount;
        row.PaidOn = paidOn;
        row.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        row.PaidById = userId;
        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Audit;
using OneBase.Domain.Incentive;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Список спящих во вкладке «Акции»: точек в списке, исключено, с какого дня действует, когда меняли, сколько закреплено за активными ТП, правило
/// (брали Yamelly с from, не брали с since); Computed — снимка ещё нет, список посчитан по Linko на начало акции.
/// </summary>
public sealed record IncentiveSleepingSummary(string Code, int Active, int Removed, DateOnly? ListedFrom, DateTimeOffset? ChangedAt, int WithActiveAgent,
    DateOnly? BoughtFrom, DateOnly? NotSince, bool Computed);

/// <summary>Итог пересчёта: добавлено, исключено, возвращено, осталось — и сводка после него.</summary>
public sealed record IncentiveSleepingRefreshResult(int Added, int Removed, int Restored, int Kept, IncentiveSleepingSummary Summary);

/// <summary>
/// Список спящих (YM-01) акции — по Linko (IncentiveResultsService.RefreshSleepingAsync): сводка и пересчёт вручную на сегодня (изменения — с
/// сегодняшнего дня). Снимок на начало акции пишет первая карточка по расписанию, пересчёт на следующую неделю — суббота.
/// </summary>
public sealed class IncentiveSleepingService(IAppDbContext db, IncentiveService incentive, IncentivePromoService promos, IncentiveResultsService results,
    IAuditLogger audit, TimeProvider clock)
{
    private DateOnly Today() => DateOnly.FromDateTime(ReportSchedules.Local(clock.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime);

    public async Task<IncentiveSleepingSummary> SummaryAsync(string code, CancellationToken ct) => await SummaryAsync(await ProgramAsync(code, ct), ct);

    public async Task<IncentiveSleepingRefreshResult> RefreshAsync(string code, Guid userId, CancellationToken ct)
    {
        var s = await ProgramAsync(code, ct);
        if (s.Offers.Yamelly is not { } ym)
        {
            throw new ReportValidationException("В акции нет Yamelly спящим — список спящих не нужен.");
        }

        var today = Today();
        var day = today < ym.Start ? ym.Start : today;
        var r = await results.RefreshSleepingAsync(s, day, day, userId, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.incentive.sleeping", nameof(IncentiveProgram), s.Code,
            new { AsOf = day, r.Added, r.Removed, r.Restored, r.Kept }, ct);
        return new IncentiveSleepingRefreshResult(r.Added, r.Removed, r.Restored, r.Kept, await SummaryAsync(s, ct));
    }

    private async Task<IncentiveSettings> ProgramAsync(string code, CancellationToken ct) =>
        await incentive.ProgramAsync(code, ct) ?? throw new KeyNotFoundException("Акция не найдена — обновите страницу.");

    private async Task<IncentiveSleepingSummary> SummaryAsync(IncentiveSettings s, CancellationToken ct)
    {
        var saved = await db.IncentiveSleepingMarkets.AsNoTracking().Where(x => x.ProgramId == s.Id)
            .Select(x => new { Changed = x.UpdatedAt ?? x.CreatedAt }).ToListAsync(ct);
        var list = await promos.SleepingListAsync(s, ct);
        var active = list.Where(x => x.RemovedOn is null).Select(x => x.MarketId).ToArray();
        var agents = await (from m in db.LinkoMarkets.AsNoTracking()
                join u in db.LinkoUsers.AsNoTracking() on m.ResponsibleAgentId equals (long?)u.Id
                where active.Contains(m.Id) && u.IsActive
                select m.Id).CountAsync(ct);
        var ym = s.Offers.Yamelly;
        return new IncentiveSleepingSummary(s.Code, active.Length, list.Count - active.Length, list.Count == 0 ? null : list.Min(x => x.ListedOn),
            saved.Count == 0 ? null : saved.Max(x => x.Changed), agents, ym?.ListFrom, ym?.ListSince, saved.Count == 0 && list.Count > 0);
    }
}

using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Audit;
using OneBase.Domain.Incentive;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Неделя ТП (пн–вс) во вкладке «Кэшбек и выплаты»: разбужено, кэшбек YM-02 оформлен, подтверждён доставкой, выплачен и к выплате; победы TW-01,
/// приз, выплачено и к выплате; когда и с какой пометкой бухгалтерия отметила выплату.
/// </summary>
public sealed record IncentivePayoutWeek(DateOnly Monday, int Woken, decimal Issued, decimal Confirmed, decimal CashbackPaid, decimal CashbackToPay, int BestWins,
    decimal BestPrize, decimal BestPaid, decimal BestToPay, DateOnly? PaidOn, string? Note);

/// <summary>ТП: спящих в списке и разбужено, баланс кэшбека по статусам, победы и призы TW-01, недели.</summary>
public sealed record IncentivePayoutRow(long AgentId, string Name, string? Region, int Listed, int Woken, decimal Issued, decimal Pending, decimal Confirmed,
    decimal CashbackPaid, decimal CashbackToPay, decimal CanEarn, int BestWins, decimal BestAccrued, decimal BestPaid, IReadOnlyList<IncentivePayoutWeek> Weeks)
{
    public decimal BestToPay => Math.Max(0, BestAccrued - BestPaid);
}

/// <summary>Кэшбек и выплаты акции на день: ТП с начислениями или спящими, приз TW-01 за победу, свежесть данных Linko.</summary>
public sealed record IncentivePayoutsView(string Code, DateOnly Date, DateTimeOffset? DataAsOf, decimal BestPrize, IReadOnlyList<IncentivePayoutRow> Rows);

/// <summary>Отметка выплаты: ТП, неделя (понедельник), вид (cashback — YM-02, best — TW-01), сумма (0 — снять отметку), когда выплачено, пометка.</summary>
public sealed record IncentivePayoutInput(long AgentId, DateOnly WeekStart, string Kind, decimal Amount, DateOnly? PaidOn, string? Note);

/// <summary>
/// «Акции → Кэшбек и выплаты» (ТЗ, раздел 7): баланс кэшбека YM-02 и призов TW-01 по ТП оргструктуры и неделям; бухгалтерия отмечает выплату
/// за неделю — «к выплате» на карточках ТП и в итогах уменьшается. Только с правом на данные продаж.
/// </summary>
public sealed class IncentivePayoutsService(IAppDbContext db, IncentiveService incentive, IncentivePromoService promos, IncentiveResultsService results,
    ReportEngine engine, IAuditLogger audit, TimeProvider clock)
{
    private DateOnly Today() => DateOnly.FromDateTime(ReportSchedules.Local(clock.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime);

    public async Task<IncentivePayoutsView> PayoutsAsync(string code, DateOnly? date, bool canUseSales, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException("Нет права на данные продаж (sales.read) — кэшбек и выплаты не показываются.");
        }

        var s = await ProgramAsync(code, ct);
        var day = date ?? Today();
        var (teams, _) = await engine.StructureAsync(day, ct);
        var agents = teams.SelectMany(t => t.Agents.Select(a => (a.Id, a.Name, Region: t.Name))).DistinctBy(a => a.Id).ToList();
        var ids = agents.Select(a => a.Id).ToList();
        var cashback = s.Offers.Yamelly is not null ? await promos.CashbackAsync(s, ids, day, null, ct) : [];
        var wins = new Dictionary<(long Agent, DateOnly Monday), int>();
        if (s.Offers.Best is { } best && day >= best.Start)
        {
            foreach (var (winDay, rows) in await promos.BestDaysAsync(s, ids, best.Start, day < best.End ? day : best.End, null, ct))
            {
                foreach (var row in rows.Where(r => r.Winner))
                {
                    var key = (row.AgentId, BattleRules.Monday(winDay));
                    wins[key] = wins.GetValueOrDefault(key) + 1;
                }
            }
        }

        var payouts = await db.IncentivePayouts.AsNoTracking().Where(p => p.ProgramId == s.Id).ToListAsync(ct);
        var prize = s.Offers.Best?.Prize ?? 0;
        var result = new List<IncentivePayoutRow>();
        foreach (var (id, name, region) in agents)
        {
            var balance = cashback.GetValueOrDefault(id);
            var own = payouts.Where(p => p.AgentId == id).ToList();
            var ownWins = wins.Where(w => w.Key.Agent == id).ToDictionary(w => w.Key.Monday, w => w.Value);
            if ((balance is null || balance is { Listed: 0, Issued: 0 }) && ownWins.Count == 0 && own.Count == 0)
            {
                continue;
            }

            var mondays = (balance?.Weeks.Select(w => w.Monday) ?? []).Concat(ownWins.Keys).Concat(own.Select(p => p.WeekStart)).Distinct().Order().ToList();
            var weeks = mondays.Select(m =>
            {
                var w = balance?.Weeks.FirstOrDefault(x => x.Monday == m);
                var paidCashback = own.Where(p => p.WeekStart == m && p.Kind == IncentivePayoutKind.Cashback).Sum(p => p.Amount);
                var paidBest = own.Where(p => p.WeekStart == m && p.Kind == IncentivePayoutKind.Best).Sum(p => p.Amount);
                var won = ownWins.GetValueOrDefault(m);
                var last = own.Where(p => p.WeekStart == m).OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt).FirstOrDefault();
                return new IncentivePayoutWeek(m, w?.Woken ?? 0, w?.Issued ?? 0, w?.Confirmed ?? 0, paidCashback, Math.Max(0, (w?.Confirmed ?? 0) - paidCashback), won,
                    won * prize, paidBest, Math.Max(0, won * prize - paidBest), last?.PaidOn, last?.Note);
            }).ToList();
            result.Add(new IncentivePayoutRow(id, name, region, balance?.Listed ?? 0, balance?.Woken ?? 0, balance?.Issued ?? 0, balance?.Pending ?? 0,
                balance?.Confirmed ?? 0, own.Where(p => p.Kind == IncentivePayoutKind.Cashback).Sum(p => p.Amount), weeks.Sum(w => w.CashbackToPay), balance?.CanEarn ?? 0,
                ownWins.Values.Sum(), ownWins.Values.Sum() * prize, own.Where(p => p.Kind == IncentivePayoutKind.Best).Sum(p => p.Amount), weeks));
        }

        var asOf = await db.LinkoSyncStates.AsNoTracking().Where(x => x.Entity == "orders").Select(x => x.LastSuccessAt).FirstOrDefaultAsync(ct);
        return new IncentivePayoutsView(s.Code, day, asOf, prize, result.OrderByDescending(r => r.CashbackToPay + r.BestToPay).ThenByDescending(r => r.Issued)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    /// <summary>Отметить выплату (или снять — сумма 0) и вернуть обновлённый баланс.</summary>
    public async Task<IncentivePayoutsView> SetPayoutAsync(string code, IncentivePayoutInput input, Guid userId, bool canUseSales, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException("Нет права на данные продаж (sales.read) — выплаты не отмечаются.");
        }

        var s = await ProgramAsync(code, ct);
        if (input.Amount is < 0 or > 1_000_000_000 || !await db.LinkoUsers.AnyAsync(u => u.Id == input.AgentId, ct))
        {
            throw new ReportValidationException("Выплата: ТП из Linko, сумма от 0 (снять отметку) до 1 млрд сум.");
        }

        if (input.Note is { Length: > 200 })
        {
            throw new ReportValidationException("Пометка — до 200 знаков.");
        }

        try
        {
            await results.SetPayoutAsync(s, input.AgentId, input.WeekStart, input.Kind, input.Amount, input.PaidOn ?? Today(), input.Note, userId, ct);
        }
        catch (ArgumentException ex)
        {
            throw new ReportValidationException(ex.Message);
        }

        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.incentive.payout", nameof(IncentiveProgram), s.Code,
            new { input.AgentId, input.WeekStart, input.Kind, input.Amount, input.PaidOn }, ct);
        return await PayoutsAsync(code, null, canUseSales, ct);
    }

    private async Task<IncentiveSettings> ProgramAsync(string code, CancellationToken ct) =>
        await incentive.ProgramAsync(code, ct) ?? throw new KeyNotFoundException("Акция не найдена — обновите страницу.");
}

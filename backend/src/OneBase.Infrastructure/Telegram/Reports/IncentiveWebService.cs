using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Access;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Incentive;
using OneBase.Domain.Telegram;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>ТП в охвате пользователя: ID Linko, имя, регион оргструктуры.</summary>
public sealed record BattleAgentRef(long Id, string Name, string Region);

/// <summary>Регион «Поля боя»: ТП, спящих в списке и разбужено, квота стоек, два баланса кешбека.</summary>
public sealed record BattleRegionRow(string Name, int Agents, int Listed, int Woken, IncentiveStandQuota? Stands, IncentiveGoalBalances Balances);

/// <summary>
/// Обзор «Поля боя» в охвате пользователя: акция и период целей v2, свежесть данных Linko и журнала, спящие, клиенты Twizos (новые, стойки) с начала
/// целей v2, два баланса кешбека, регионы, ТП.
/// </summary>
public sealed record BattleOverview(string Program, string Name, DateOnly Date, DateOnly Start, DateOnly End, bool Enabled, DateTimeOffset? DataAsOf, int LedgerRows,
    IncentiveGoalBalances Balances, int Listed, int Woken, int TwizosClients, int TwizosNew, int Stands, IReadOnlyList<BattleRegionRow> Regions, IReadOnlyList<BattleAgentRef> Agents,
    string Scope);

/// <summary>Маршрут ТП на день с целями v2, итогами и балансами.</summary>
public sealed record BattleRouteView(BattleAgentRef Agent, DateOnly Date, IncentiveRoute Route, IncentiveGoalTotals Totals, IncentiveGoalBalances Balances);

/// <summary>ТП в итоге дня команды: итоги маршрута.</summary>
public sealed record BattleTeamRow(BattleAgentRef Agent, IncentiveGoalTotals Totals);

/// <summary>Строка журнала кешбека с именами ТП и точки.</summary>
public sealed record BattleLedgerRow(long OrderId, DateOnly OrderDate, long AgentId, string Agent, long MarketId, string Market, string Kind, string Segment, decimal Percent,
    decimal ListSum, decimal NetSum, decimal Issued, decimal Amount, string Status, DateOnly? DeliveredOn, bool Stand);

/// <summary>
/// Веб-модуль «Поле боя» («Продажи → Поле боя»): те же расчёты, что у карточек v2, в охвате пользователя «Продаж» — ТП видит только себя, СВР —
/// свои регионы, РМ — регионы своих направлений, без охвата — вся организация. Цели дня не записываются (как предпросмотр).
/// </summary>
public sealed class IncentiveWebService(IAppDbContext db, ReportEngine engine, IncentiveRouteService routes, IncentivePromoService promos, IncentiveGoalService goals,
    IncentiveCashbackLedger ledger, IncentiveCardBuilder cards, TimeProvider clock)
{
    private const int TeamLimit = 60;
    private const int LedgerLimit = 1000;

    public DateOnly Today() => DateOnly.FromDateTime(ReportSchedules.Local(clock.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime);

    /// <summary>Акция с целями v2: включённая и самая поздняя по началу; нет — null.</summary>
    public async Task<IncentiveSettings?> ProgramAsync(CancellationToken ct)
    {
        var programs = await db.IncentivePrograms.AsNoTracking().OrderByDescending(p => p.Enabled).ThenByDescending(p => p.StartDate).ToListAsync(ct);
        return programs.Select(IncentiveSettings.From).FirstOrDefault(s => s.Offers.Goals is not null);
    }

    /// <summary>ТП оргструктуры месяца в охвате: регион — команда региона; ТП-пользователь — только он.</summary>
    public async Task<IReadOnlyList<(BattleAgentRef Agent, ReportRegionTeam Team)>> AgentsAsync(SalesScope scope, DateOnly date, CancellationToken ct)
    {
        var (teams, _) = await engine.StructureAsync(date, ct);
        return teams
            .Where(t => scope.IsOrganization || scope.IsAgent || (scope.IsRm ? t.DirectionId is { } d && scope.DirectionIds.Contains(d) : scope.RegionIds.Contains(t.Id)))
            .SelectMany(t => t.Agents.Select(a => (Agent: new BattleAgentRef(a.Id, a.Name, t.Name), Team: t)))
            .Where(x => !scope.IsAgent || x.Agent.Id == scope.AgentId)
            .DistinctBy(x => x.Agent.Id)
            .ToList();
    }

    public async Task<BattleOverview?> OverviewAsync(SalesScope scope, string scopeTitle, DateOnly date, CancellationToken ct)
    {
        if (await ProgramAsync(ct) is not { Offers.Goals: { } g } s)
        {
            return null;
        }

        await ledger.RefreshIfStaleAsync(s, date, IncentiveGoalCardBuilder.LedgerMaxAge, ct);
        var agents = await AgentsAsync(scope, date, ct);
        var ids = agents.Select(a => a.Agent.Id).ToList();
        var sleeping = s.Offers.Yamelly is null || ids.Count == 0 ? [] : await promos.SleepingAsync(s, ids, date, null, ct);
        var twizos = ids.Count == 0 ? [] : (await ledger.RowsAsync(s, IncentiveCashbackKind.Twizos, ids, g.Start, date, ct))
            .Where(r => r.Status != IncentiveCashbackStatus.Cancelled).ToList();
        var mogulBalances = ids.Count == 0 ? [] : await ledger.BalancesAsync(s, IncentiveCashbackKind.Mogul, ids, date, ct);
        var twizosBalances = ids.Count == 0 ? [] : await ledger.BalancesAsync(s, IncentiveCashbackKind.Twizos, ids, date, ct);
        var quotas = await goals.StandQuotasAsync(g, date, ct);

        static IncentiveGoalBalance Sum(IEnumerable<IncentiveCashbackBalance> rows)
        {
            var list = rows.ToList();
            return new IncentiveGoalBalance(list.Sum(b => b.Issued), list.Sum(b => b.Pending), list.Sum(b => b.Confirmed), list.Sum(b => b.Paid), list.Sum(b => b.ToPay), null);
        }

        IncentiveGoalBalances BalancesOf(IEnumerable<long> agentIds)
        {
            var set = agentIds.ToHashSet();
            return new IncentiveGoalBalances(Sum(mogulBalances.Where(kv => set.Contains(kv.Key)).Select(kv => kv.Value)),
                Sum(twizosBalances.Where(kv => set.Contains(kv.Key)).Select(kv => kv.Value)));
        }

        var regions = agents.GroupBy(a => a.Agent.Region).Select(r =>
        {
            var regionIds = r.Select(x => x.Agent.Id).ToList();
            var points = regionIds.SelectMany(id => sleeping.GetValueOrDefault(id) ?? []).ToList();
            return new BattleRegionRow(r.Key, regionIds.Count, points.Count, points.Count(p => p.WokenOn is not null), quotas.FirstOrDefault(q => q.Region == r.Key),
                BalancesOf(regionIds));
        }).OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var points = sleeping.Values.SelectMany(x => x).ToList();
        var asOf = await db.LinkoSyncStates.AsNoTracking().Where(x => x.Entity == "orders").Select(x => x.LastSuccessAt).FirstOrDefaultAsync(ct);
        var ledgerRows = await db.IncentiveCashbackAccruals.CountAsync(a => a.ProgramId == s.Id, ct);
        return new BattleOverview(s.Code, s.Name, date, g.Start, g.End, s.Enabled, asOf, ledgerRows, BalancesOf(ids), points.Count, points.Count(p => p.WokenOn is not null),
            twizos.Select(r => r.MarketId).Distinct().Count(), twizos.Where(r => r.Segment != IncentiveTwizosSegment.Existing).Select(r => r.MarketId).Distinct().Count(),
            twizos.Count(r => r.Stand), regions, agents.Select(a => a.Agent).OrderBy(a => a.Region).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            scopeTitle);
    }

    /// <summary>Маршрут ТП на день (ТП вне охвата — null).</summary>
    public async Task<BattleRouteView?> RouteAsync(SalesScope scope, long agentId, DateOnly date, CancellationToken ct)
    {
        if (await ProgramAsync(ct) is not { } s || (await AgentsAsync(scope, date, ct)).FirstOrDefault(a => a.Agent.Id == agentId).Agent is not { } agent)
        {
            return null;
        }

        var route = await routes.DayAsync(s, agentId, date, date == Today() ? TimeOnly.FromDateTime(ReportSchedules.Local(clock.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime) : null,
            ct, persist: false);
        var totals = IncentiveGoalTotals.Of(route);
        var mogul = (await ledger.BalancesAsync(s, IncentiveCashbackKind.Mogul, [agentId], date, ct)).GetValueOrDefault(agentId);
        var twizos = (await ledger.BalancesAsync(s, IncentiveCashbackKind.Twizos, [agentId], date, ct)).GetValueOrDefault(agentId);
        static IncentiveGoalBalance Of(IncentiveCashbackBalance? b, decimal can) =>
            b is null ? IncentiveGoalBalance.Zero with { CanEarn = can } : new IncentiveGoalBalance(b.Issued, b.Pending, b.Confirmed, b.Paid, b.ToPay, can);
        return new BattleRouteView(agent, date, route, totals, new IncentiveGoalBalances(Of(mogul, totals.MogulLeft), Of(twizos, totals.TwizosLeft)));
    }

    /// <summary>Итоги дня ТП региона (СВР, РМ, вся организация — по региону; ТП — только он). Не больше 60 ТП за раз.</summary>
    public async Task<IReadOnlyList<BattleTeamRow>> TeamAsync(SalesScope scope, string? region, DateOnly date, CancellationToken ct)
    {
        if (await ProgramAsync(ct) is not { } s)
        {
            return [];
        }

        var agents = (await AgentsAsync(scope, date, ct)).Where(a => region is null || a.Agent.Region == region).Take(TeamLimit).ToList();
        var rows = new List<BattleTeamRow>();
        foreach (var (agent, _) in agents)
        {
            rows.Add(new BattleTeamRow(agent, IncentiveGoalTotals.Of(await routes.DayAsync(s, agent.Id, date, null, ct, persist: false))));
        }

        return rows.OrderByDescending(r => r.Totals.Cashback).ThenBy(r => r.Agent.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Журнал кешбека в охвате: ТП (null — все в охвате), вид (null — оба), период по дню заказа; новые сверху, до 1000 строк.</summary>
    public async Task<IReadOnlyList<BattleLedgerRow>> LedgerAsync(SalesScope scope, long? agentId, string? kind, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (await ProgramAsync(ct) is not { } s)
        {
            return [];
        }

        var agents = (await AgentsAsync(scope, to, ct)).Select(a => a.Agent).Where(a => agentId is null || a.Id == agentId).ToDictionary(a => a.Id, a => a.Name);
        var ids = agents.Keys.ToArray();
        var rows = await db.IncentiveCashbackAccruals.AsNoTracking()
            .Where(a => a.ProgramId == s.Id && ids.Contains(a.AgentId) && a.OrderDate >= from && a.OrderDate <= to && (kind == null || a.Kind == kind))
            .OrderByDescending(a => a.OrderCreatedAt).ThenByDescending(a => a.OrderId).Take(LedgerLimit).ToListAsync(ct);
        var marketIds = rows.Select(r => r.MarketId).Distinct().ToArray();
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => marketIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        return rows.Select(r => new BattleLedgerRow(r.OrderId, r.OrderDate, r.AgentId, agents.GetValueOrDefault(r.AgentId, $"#{r.AgentId}"), r.MarketId,
            markets.GetValueOrDefault(r.MarketId, $"#{r.MarketId}"), r.Kind, r.Segment, r.Percent, r.ListSum, r.NetSum, r.Issued, r.Amount, r.Status, r.DeliveredOn, r.Stand)).ToList();
    }

    /// <summary>Квоты стоек регионов охвата.</summary>
    public async Task<IReadOnlyList<IncentiveStandQuota>> StandsAsync(SalesScope scope, DateOnly date, CancellationToken ct)
    {
        if (await ProgramAsync(ct) is not { Offers.Goals: { } g })
        {
            return [];
        }

        var regions = (await AgentsAsync(scope, date, ct)).Select(a => a.Agent.Region).ToHashSet();
        return (await goals.StandQuotasAsync(g, date, ct)).Where(q => regions.Contains(q.Region)).OrderBy(q => q.Region, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Картинка карточки v2 для просмотра и выгрузки PNG: слоты ТП — по ТП охвата; team — команда региона; zone — регионы охвата. Цели дня не
    /// записываются. null — нет акции, ТП вне охвата или карточка не v2.
    /// </summary>
    public async Task<byte[]?> CardAsync(SalesScope scope, string slot, long? agentId, string? region, DateOnly date, string language, CancellationToken ct)
    {
        if (await ProgramAsync(ct) is not { } s || !IncentiveGoalCardBuilder.Applies(s, slot, date))
        {
            return null;
        }

        var agents = await AgentsAsync(scope, date, ct);
        var participants = agents.Select(a => new IncentiveAgent(a.Agent.Id, a.Agent.Name, a.Agent.Region)).ToList();
        IncentiveCardRequest request;
        if (slot is IncentiveSlot.Team or IncentiveSlot.Zone)
        {
            var chosen = agents.Where(a => slot == IncentiveSlot.Zone ? region is null || a.Agent.Region == region : a.Agent.Region == (region ?? agents.FirstOrDefault().Agent?.Region))
                .GroupBy(a => a.Agent.Region)
                .Select(r => new IncentiveTeamRegionSpec(r.Key, r.First().Team.SupervisorName, r.Select(x => new IncentiveAgent(x.Agent.Id, x.Agent.Name, x.Agent.Region)).ToList()))
                .ToList();
            if (chosen.Count == 0)
            {
                return null;
            }

            var title = slot == IncentiveSlot.Team ? $"СВР · {chosen[0].Name}" : string.Join(" · ", chosen.Take(3).Select(r => r.Name));
            request = new IncentiveCardRequest(slot, date, language, participants, [], null,
                new IncentiveTeamSpec(slot == IncentiveSlot.Team ? TelegramReportRoles.Sv : TelegramReportRoles.Rm, title, chosen), Persist: false);
        }
        else
        {
            var agent = participants.FirstOrDefault(a => a.Id == agentId);
            if (agent is null)
            {
                return null;
            }

            var until = IncentiveSlot.IsInterim(slot) ? (TimeOnly?)(slot == IncentiveSlot.Afternoon ? IncentiveCardBuilder.DefaultAfternoon : IncentiveCardBuilder.DefaultMidday) : null;
            request = new IncentiveCardRequest(slot, date, language, participants, [agent], until, Persist: false);
        }

        var built = await cards.BuildAsync(s, request, ct);
        if (!built.V2)
        {
            return null;
        }

        var (caption, summary) = IncentiveTexts.Caption(built);
        var document = new OneBase.Application.Telegram.Reports.ReportDocument("incentive." + slot, ReportType.Incentive, "web", "9", "web", null, date, $"{date:dd.MM.yyyy}", clock.GetUtcNow(), built.DataAsOf,
            language, [], [], [], [], caption, summary) { Incentive = built, AsFiles = true };
        return new SkiaReportRenderer().Render(document).FirstOrDefault();
    }

    /// <summary>Обновить журнал кешбека сейчас (вся организация).</summary>
    public async Task<IncentiveLedgerRefresh?> RefreshAsync(CancellationToken ct) =>
        await ProgramAsync(ct) is { } s ? await ledger.RefreshAsync(s, Today(), ct) : null;
}

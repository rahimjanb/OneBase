using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Battle;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Строит отчёт запуска для области получателя на реальных данных OneBase. Область проверяется по данным «Продаж» месяца при каждом
/// построении (ТП — только свои цифры, СВ — свой регион, РМ — регионы направления): получатель не получит чужих данных, даже если
/// настройки устарели.
/// </summary>
public sealed class ReportEngine(IAppDbContext db, SalesDataLoader loader, ReportDataService data, BattleGoalService battle, BattleOptions battleOptions)
{
    /// <summary>Регионы месяца с супервайзерами и ТП и направления РМ (кэш раздела «Продажи»).</summary>
    public async Task<(IReadOnlyList<ReportRegionTeam> Teams, IReadOnlyList<ReportDirection> Directions)> StructureAsync(DateOnly date, CancellationToken ct)
    {
        var analytics = await loader.LoadAsync(date.Year, date.Month, ct);
        return (analytics.CachedReportTeams(), analytics.ReportDirections());
    }

    /// <summary>
    /// Отчёт запуска job за день date. now — момент формирования в часовом поясе расписания: у промежуточных запусков (13:00, 17:00) за
    /// сегодня факт — заказы до этого момента, за прошлый день — до времени запуска.
    /// </summary>
    public async Task<ReportDocument> BuildAsync(string job, ReportScope scope, DateOnly date, DateTimeOffset now, string language, int noPurchaseDays,
        CancellationToken ct)
    {
        var info = ReportCatalog.Find(job) ?? throw new ArgumentException($"Неизвестный отчёт «{job}».");
        if (!info.Roles.Contains(scope.Role))
        {
            throw new ArgumentException("Этот запуск — не для роли получателя (напоминание 17:00 — только ТП).");
        }

        var schedules = await db.TelegramReportJobs.AsNoTracking().Where(j => j.Code == job || j.Code == "daily.midday").ToListAsync(ct);
        var jobTime = schedules.FirstOrDefault(j => j.Code == job)?.Time ?? info.Time;
        var midday = schedules.FirstOrDefault(j => j.Code == "daily.midday")?.Time ?? ReportCatalog.Find("daily.midday")!.Time;
        var (from, to) = info.Report == ReportType.Weekly ? WeeklyReport.Period(info, date) : (date, date);

        var (teams, directions) = await StructureAsync(to, ct);
        var resolved = ReportScopes.Resolve(scope, teams, directions, ru => ReportTexts.Get(language, ru));
        var today = DateOnly.FromDateTime(now.DateTime);
        var cutoff = info.IsMidday ? date == today ? TimeOnly.FromDateTime(now.DateTime) : jobTime : TimeOnly.MaxValue;
        if (info.Report == ReportType.Battle)
        {
            return await BuildBattleAsync(info, resolved, teams, date, now, language, cutoff, midday, jobTime, ct);
        }

        var peers = info.Report == ReportType.Weekly && scope.Role == TelegramReportRoles.Tp
            ? teams.First(r => r.Id == resolved.Regions[0].Id).Agents
            : null;
        var agents = resolved.Agents.Select(a => a.Id).Concat(peers?.Select(p => p.Id) ?? []).Distinct().ToList();
        var facts = await data.LoadAsync(agents, from, to, withPromos: info.Report is ReportType.Promo or ReportType.Weekly,
            withDailyPlans: info.Report == ReportType.NoOrders, ct);

        var noPurchase = info.Report == ReportType.NoPurchase
            ? await data.NoPurchaseAsync(resolved.Agents.Select(a => a.Id).ToList(), date, noPurchaseDays, facts.Visits, ct)
            : null;
        var context = new ReportContext(info, resolved, date, now, language, facts, midday, cutoff, noPurchaseDays, noPurchase, jobTime, peers);
        return info.Report switch
        {
            ReportType.Daily => DailyPlanReport.Build(context),
            ReportType.NoPurchase => NoPurchaseReport.Build(context),
            ReportType.NoOrders => NoOrdersReport.Build(context),
            ReportType.Promo => PromoReport.Build(context),
            ReportType.Categories => CategoriesReport.Build(context),
            _ => WeeklyReport.Build(context),
        };
    }

    /// <summary>
    /// «Поле боя»: ТП — своя карточка; СВ — карточки ТП региона, подписанных на «Поле боя» (никого не подписали — все ТП региона, до десяти).
    /// Полдень — по заказам до момента формирования, итог дня — весь день, неделя — пн–сб по заказам (подтверждение по доставке — позже).
    /// </summary>
    private async Task<ReportDocument> BuildBattleAsync(ReportJobInfo info, ResolvedScope resolved, IReadOnlyList<ReportRegionTeam> teams, DateOnly date,
        DateTimeOffset now, string language, TimeOnly cutoff, TimeOnly midday, TimeOnly jobTime, CancellationToken ct)
    {
        var region = resolved.Regions[0];
        var regionAgents = teams.First(t => t.Id == region.Id).Agents;
        IReadOnlyList<ReportPerson> agents;
        if (resolved.Scope.Role == TelegramReportRoles.Tp)
        {
            agents = [resolved.Agent!];
        }
        else
        {
            var subscribed = (await db.TelegramReportSubscriptions.AsNoTracking()
                    .Where(s => s.Enabled && s.Role == TelegramReportRoles.Tp && s.AgentId != null && s.Reports.Contains("battle."))
                    .Select(s => s.AgentId!.Value).ToListAsync(ct))
                .ToHashSet();
            var participants = regionAgents.Where(a => subscribed.Contains(a.Id)).ToList();
            agents = participants.Count > 0 ? participants : regionAgents.Take(10).ToList();
        }

        var until = info.IsMidday ? cutoff : (TimeOnly?)null;
        var days = new List<BattleDay>();
        var weeks = new List<BattleWeek>();
        var peers = regionAgents.Select(a => a.Id).ToList();
        var monday = BattleRules.Monday(date);
        foreach (var agent in agents)
        {
            if (await battle.IsExcludedAgentAsync(agent.Id, ct))
            {
                continue;
            }

            if (info.Slot == "saturday")
            {
                weeks.Add(await battle.WeekAsync(agent.Id, agent.Name, region.Name, monday, date, until, final: false, peers, ct));
            }
            else
            {
                days.Add(await battle.DayAsync(agent.Id, agent.Name, region.Name, date, until, final: false, ct));
            }
        }

        var ordersAsOf = days.Select(d => d.OrdersAsOf).Concat(weeks.Select(w => w.OrdersAsOf)).FirstOrDefault()
                         ?? await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        var facts = new ReportFacts
        {
            Categories = [], CategoryPrice = new Dictionary<string, decimal>(), Plans = new Dictionary<(long, int, int), MonthPlan>(), Orders = [], Visits = [],
            OrdersAsOf = ordersAsOf, VisitsAsOf = ordersAsOf,
        };
        var context = new ReportContext(info, resolved, date, now, language, facts, midday, cutoff, 30, null, jobTime, null);
        var prizes = battleOptions.TwizosSets.Select(s => new BattlePrizeTile(s.Level, s.Sum, s.Contents, s.Prize)).ToList();
        var mogulLine = ReportTexts.Format(language, "Могул — одной накладной · фрукты от {0} сум → ещё −{1}%", ReportFormat.Number(battleOptions.FruitMinSum), battleOptions.FruitPercent);
        var cards = new BattleCards(info.Slot, days, weeks, prizes, mogulLine, battleOptions.Bonus, language);
        return BattleReport.Build(context, cards);
    }
}

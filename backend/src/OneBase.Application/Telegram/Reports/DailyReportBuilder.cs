using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Отчёт на реальных данных OneBase — для автоотправки «регион → супервайзер»:
/// <list type="bullet">
/// <item>регион, РМ направления, супервайзеры и агенты — из раздела «Продажи» (оргструктура OneBase и Linko, как в «KPI» и команде региона);</item>
/// <item>план продаж — месячный план агента в Linko (staff_balance: sales_sum или product_sales_weight) ÷ рабочие дни месяца (пн–сб);</item>
/// <item>факт продаж — заказы, принятые агентом в этот день (без отменённых и недоставленных);</item>
/// <item>план визитов — визиты Linko в плане на день; факт — точки с выполненным визитом.</item>
/// </list>
/// Чего нет — null («нет данных»), а не ноль: плана нет в Linko; сегодняшние данные ещё не загружены.
/// </summary>
public sealed class DailyReportBuilder(IAppDbContext db, SalesDataLoader loader, ILinkoSalesProvider linko)
{
    /// <summary>Регионы месяца с супервайзерами и агентами (кэш раздела «Продажи»).</summary>
    public async Task<IReadOnlyList<ReportRegionTeam>> TeamsAsync(DateOnly date, CancellationToken ct) =>
        (await loader.LoadAsync(date.Year, date.Month, ct)).CachedReportTeams();

    public async Task<DailyReport> BuildAsync(TelegramReportKind kind, Guid regionId, long? supervisorId, string? supervisorName, DateOnly date, ReportMetric metric,
        string language, DateTimeOffset now, bool check, CancellationToken ct)
    {
        string T(string ru) => DailyReportTexts.Get(language, ru);
        var region = (await TeamsAsync(date, ct)).FirstOrDefault(r => r.Id == regionId)
                     ?? throw new KeyNotFoundException("Регион не найден в данных продаж за этот месяц.");
        var supervisor = supervisorId is { } sid
            ? region.Supervisors.FirstOrDefault(s => s.Id == sid)?.Name ?? supervisorName ?? $"ID {sid}"
            : region.SupervisorName is { Length: > 0 } svr ? svr : T("не указан");
        var notes = new List<string>();
        var agents = region.Agents;
        var ids = agents.Select(a => a.Id).ToList();
        var plans = (await linko.AgentPlansAsync(ids, date.Year, date.Month, ct)).ToDictionary(p => p.LinkoUserId);

        // Факт — только если данные за этот день уже загружены: иначе нули выглядели бы как «ничего не продали».
        var syncedAt = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        var factKnown = kind != TelegramReportKind.Morning && syncedAt is { } synced && synced >= FieldClock.DayStartUtc(date);
        var taken = factKnown ? (await linko.AgentDaySalesAsync(ids, date, date, SalesBasis.Taken, ct)).GroupBy(d => d.LinkoUserId).ToDictionary(g => g.Key, g => g.First()) : [];
        if (kind != TelegramReportKind.Morning && !factKnown)
        {
            notes.Add(syncedAt is { } at
                ? DailyReportTexts.Format(language, "Продажи за этот день ещё не загружены (последнее обновление {0}) — факт не показан", at.ToOffset(now.Offset).ToString("dd.MM HH:mm"))
                : T("Продажи за этот день ещё не загружены — факт не показан"));
        }

        // Визиты: план — визиты в плане на день; нет ни одной строки визитов на день — план не загружен (не ноль).
        var visits = await linko.VisitsAsync(ids, date, date, ct);
        var visitPlanKnown = visits.Count > 0;
        if (!visitPlanKnown)
        {
            notes.Add(T("План визитов на этот день не загружен"));
        }

        var rows = new List<ReportAgentRow>();
        var withoutPlan = new List<string>();
        foreach (var agent in agents)
        {
            var plan = plans.GetValueOrDefault(agent.Id);
            var daily = Whole(FieldKpi.DailyPlan(metric == ReportMetric.Kg ? plan?.PlanKg : plan?.PlanSum, date.Year, date.Month));
            if (daily is null)
            {
                withoutPlan.Add(agent.Name);
            }

            var day = taken.GetValueOrDefault(agent.Id);
            decimal? fact = factKnown ? Whole(metric == ReportMetric.Kg ? day?.Kg ?? 0 : day?.Sum ?? 0) : null;
            var own = visits.Where(v => v.LinkoUserId == agent.Id).ToList();
            int? visitPlan = visitPlanKnown ? own.Count(v => v.InPlan) : null;
            int? visitFact = kind == TelegramReportKind.Morning || !factKnown ? null : own.Where(v => v.Done).Select(v => v.MarketId).Distinct().Count();
            rows.Add(new ReportAgentRow(agent.Name, daily, fact, visitPlan, visitFact, day?.Orders ?? 0));
        }

        if (agents.Count == 0)
        {
            notes.Add(T("В регионе нет агентов за этот месяц"));
        }

        if (withoutPlan.Count > 0)
        {
            notes.Add(DailyReportTexts.Format(language, "Нет плана у агентов: {0}", string.Join(", ", withoutPlan.Take(12)) + (withoutPlan.Count > 12 ? $" +{withoutPlan.Count - 12}" : "")));
        }

        var teams = new List<ReportTeam> { new(supervisor, null, rows, DailyReportMath.Totals(rows)) };
        var plansLoaded = ids.Count == 0
            ? null
            : await db.SalesStaffPlans.AsNoTracking().Where(p => p.Year == date.Year && p.Month == date.Month && ids.Contains(p.LinkoUserId))
                .MaxAsync(p => (DateTimeOffset?)p.LoadedAt, ct);
        var sources = new List<ReportSource>
        {
            new(T("Планы — Linko (staff_balance), месячный ÷ рабочие дни (пн–сб)"), "Linko", plansLoaded),
            new(T("Продажи — заказы, принятые агентом за день (Linko)"), "Linko", syncedAt),
            new(T("Регион, супервайзер и агенты — «Продажи» OneBase"), "OneBase", null),
        };

        return new DailyReport(kind, date, now, region.Name, region.Manager, null, metric, language, Demo: false, check,
            teams, DailyReportMath.Combine(teams.Select(t => t.Totals).ToList()), sources, notes,
            kind == TelegramReportKind.Evening && factKnown ? DailyReportMath.Best(teams) : [],
            factKnown ? DailyReportMath.Attention(kind, teams, T, metric) : [],
            kind == TelegramReportKind.Morning ? null : syncedAt?.ToOffset(now.Offset));
    }

    /// <summary>
    /// Целые сумы и килограммы — как на картинке: иначе итог (сумма точных значений) расходился бы на 1 с суммой показанных строк
    /// (план на день = месячный ÷ рабочие дни — дробный). Меньше 1 — с десятой, чтобы маленький план не стал нулём.
    /// </summary>
    private static decimal? Whole(decimal? value) => value is { } v
        ? Math.Abs(v) is > 0 and < 1 ? Math.Round(v, 1, MidpointRounding.AwayFromZero) : Math.Round(v, 0, MidpointRounding.AwayFromZero)
        : null;
}

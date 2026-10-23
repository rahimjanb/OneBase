using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Строит отчёт запуска для области получателя на реальных данных OneBase. Область проверяется по данным «Продаж» месяца при каждом
/// построении (ТП — только свои цифры, СВ — свой регион, РМ — регионы направления): получатель не получит чужих данных, даже если
/// настройки устарели.
/// </summary>
public sealed class ReportEngine(IAppDbContext db, SalesDataLoader loader, ReportDataService data)
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
        var peers = info.Report == ReportType.Weekly && scope.Role == TelegramReportRoles.Tp
            ? teams.First(r => r.Id == resolved.Regions[0].Id).Agents
            : null;
        var agents = resolved.Agents.Select(a => a.Id).Concat(peers?.Select(p => p.Id) ?? []).Distinct().ToList();
        var facts = await data.LoadAsync(agents, from, to, withPromos: info.Report is ReportType.Promo or ReportType.Weekly, ct);

        var today = DateOnly.FromDateTime(now.DateTime);
        var cutoff = info.IsMidday ? date == today ? TimeOnly.FromDateTime(now.DateTime) : jobTime : TimeOnly.MaxValue;
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
}

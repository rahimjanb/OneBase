using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Дневные планы ТП для «ТП без заказов»: считаются один раз утром на бизнес-дату (всем ТП оргструктуры «Продаж») и сохраняются
/// (TelegramReportDailyPlan); отправки 13:00, 17:00 и 18:00, предпросмотр и тест читают одну и ту же строку — изменение факта после
/// расчёта план дня не меняет. Источники: план месяца — staff_balance Linko (sales_sum, ILinkoSalesProvider.AgentPlansAsync); факт по
/// вчера включительно — проданное по дате приёмки по правилам «Продаж» (SalesAnalytics.AgentRevenue); праздники — справочник OneBase
/// (sales.Holidays). Для прошлого дня (предпросмотр за прошлую дату) считается на лету и не сохраняется.
/// </summary>
public sealed class DailyPlanService(IAppDbContext db, SalesDataLoader loader, ILinkoSalesProvider linko, TimeProvider clock)
{
    public const string Morning = "morning";
    public const string Report = "report";
    public const string Admin = "admin";
    public const string PlanAppeared = "plan-appeared";

    /// <summary>С этого времени по Ташкенту утренняя задача считает планы дня (до первого отчёта в 8:30).</summary>
    public static readonly TimeOnly MorningAt = new(8, 0);

    public DateOnly Today() => DateOnly.FromDateTime(ReportSchedules.Local(clock.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime);

    /// <summary>Праздники справочника за отрезок: дата → название.</summary>
    public async Task<IReadOnlyDictionary<DateOnly, string>> HolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.SalesHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).ToDictionaryAsync(h => h.Date, h => h.Name, ct);

    /// <summary>Утренняя задача: планы дня всем ТП оргструктуры (уже посчитанные не трогаются). Возвращает, сколько строк добавлено.</summary>
    public async Task<int> PrepareAsync(DateOnly date, CancellationToken ct)
    {
        var agents = await AllAgentsAsync(date, ct);
        var before = await db.TelegramReportDailyPlans.CountAsync(p => p.Date == date && p.SupersededAt == null, ct);
        await EnsureAsync(agents, date, Morning, ct);
        return await db.TelegramReportDailyPlans.CountAsync(p => p.Date == date && p.SupersededAt == null, ct) - before;
    }

    /// <summary>
    /// Строки на день для ТП. Сохранённые — как есть (план дня в течение дня не меняется); нет строки — считается и, если день —
    /// сегодняшний, сохраняется; у сохранённой строки «нет плана» план появился в Linko — прежняя уходит в историю, новая с пометкой
    /// plan-appeared (уже отправленные отчёты не меняются). Повторный запуск и второй экземпляр службы дубля не создают.
    /// </summary>
    public async Task<IReadOnlyDictionary<long, DailyPlanRow>> EnsureAsync(IReadOnlyCollection<long> agents, DateOnly date, string trigger, CancellationToken ct)
    {
        var ids = agents.Distinct().ToArray();
        var result = new Dictionary<long, DailyPlanRow>();
        if (ids.Length == 0)
        {
            return result;
        }

        var stored = await ActiveAsync(ids, date, ct);
        foreach (var p in stored)
        {
            result[p.LinkoUserId] = RowOf(p);
        }

        var missing = ids.Where(id => !result.ContainsKey(id)).ToList();
        var withoutPlan = stored.Where(p => p.Status == DailyPlanMath.NoPlan).ToList();
        if (missing.Count == 0 && withoutPlan.Count == 0)
        {
            return result;
        }

        var computed = await ComputeAsync(missing.Concat(withoutPlan.Select(p => p.LinkoUserId)).Distinct().ToList(), date, ct);
        var persist = date == Today();
        var now = clock.GetUtcNow();
        foreach (var p in withoutPlan)
        {
            var row = computed[p.LinkoUserId];
            if (row.Status == DailyPlanMath.NoPlan)
            {
                continue; // плана в Linko по-прежнему нет
            }

            result[p.LinkoUserId] = row;
            if (persist)
            {
                p.SupersededAt = now;
                db.TelegramReportDailyPlans.Add(EntityOf(p.LinkoUserId, date, row, PlanAppeared, "План месяца появился в Linko после утреннего расчёта", null, now));
            }
        }

        foreach (var id in missing)
        {
            result[id] = computed[id];
            if (persist)
            {
                db.TelegramReportDailyPlans.Add(EntityOf(id, date, computed[id], trigger, null, null, now));
            }
        }

        if (!persist)
        {
            return result;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Строку на этот день уже сохранил другой экземпляр службы (уникальный индекс): свои несохранённые — отбросить, взять его.
            foreach (var entry in db.ChangeTracker.Entries<TelegramReportDailyPlan>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
            {
                entry.State = EntityState.Detached;
            }

            foreach (var p in await ActiveAsync(ids, date, ct))
            {
                result[p.LinkoUserId] = RowOf(p);
            }
        }

        return result;
    }

    /// <summary>
    /// Пересчёт администратором с причиной: действующие строки дня (одного ТП или всех) уходят в историю, новые считаются по текущим
    /// данным. Строк на день ещё нет — считаются все ТП оргструктуры.
    /// </summary>
    public async Task<IReadOnlyList<TelegramReportDailyPlan>> RecalculateAsync(DateOnly date, long? agent, string reason, Guid userId, CancellationToken ct)
    {
        var active = await db.TelegramReportDailyPlans
            .Where(p => p.Date == date && p.SupersededAt == null && (agent == null || p.LinkoUserId == agent))
            .ToListAsync(ct);
        var ids = active.Select(p => p.LinkoUserId).ToList();
        if (agent is { } one && !ids.Contains(one))
        {
            ids.Add(one);
        }

        if (ids.Count == 0)
        {
            ids = (await AllAgentsAsync(date, ct)).ToList();
        }

        var computed = await ComputeAsync(ids, date, ct);
        var now = clock.GetUtcNow();
        foreach (var p in active)
        {
            p.SupersededAt = now;
            p.SupersededById = userId;
        }

        var added = ids.Select(id => EntityOf(id, date, computed[id], Admin, reason, userId, now)).ToList();
        db.TelegramReportDailyPlans.AddRange(added);
        await db.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>Расчёт на лету по текущим данным (без сохранения): план месяца Linko, факт по вчера, праздники месяца.</summary>
    public async Task<IReadOnlyDictionary<long, DailyPlanRow>> ComputeAsync(IReadOnlyCollection<long> agents, DateOnly date, CancellationToken ct)
    {
        var analytics = await loader.LoadAsync(date.Year, date.Month, ct);
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        var plans = (await linko.AgentPlansAsync(agents, date.Year, date.Month, ct))
            .GroupBy(p => p.LinkoUserId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.PlanSum).FirstOrDefault(s => s is not null));
        var holidays = await HolidaysAsync(monthStart, monthStart.AddMonths(1).AddDays(-1), ct);
        var yesterday = date.AddDays(-1);
        return agents.Distinct().ToDictionary(a => a, a => DailyPlanMath.Compute(
            plans.GetValueOrDefault(a), yesterday < monthStart ? 0 : analytics.AgentRevenue(a, monthStart, yesterday), date, holidays));
    }

    public static DailyPlanRow RowOf(TelegramReportDailyPlan p) =>
        new(p.MonthPlanSum, p.FactBeforeSum, p.RemainingSum, p.WorkDaysLeft, p.DailyPlanSum, p.Status, p.Note);

    private async Task<List<long>> AllAgentsAsync(DateOnly date, CancellationToken ct)
    {
        var analytics = await loader.LoadAsync(date.Year, date.Month, ct);
        return analytics.CachedReportTeams().SelectMany(t => t.Agents.Select(a => a.Id)).Distinct().ToList();
    }

    private Task<List<TelegramReportDailyPlan>> ActiveAsync(long[] ids, DateOnly date, CancellationToken ct) =>
        db.TelegramReportDailyPlans.Where(p => p.Date == date && p.SupersededAt == null && ids.Contains(p.LinkoUserId)).ToListAsync(ct);

    private static TelegramReportDailyPlan EntityOf(long agent, DateOnly date, DailyPlanRow row, string trigger, string? reason, Guid? userId, DateTimeOffset now) => new()
    {
        LinkoUserId = agent,
        Date = date,
        MonthPlanSum = row.MonthPlan,
        FactBeforeSum = row.FactBefore,
        RemainingSum = row.Remaining,
        WorkDaysLeft = row.WorkDaysLeft,
        DailyPlanSum = row.DailyPlan,
        Status = row.Status,
        Note = row.Note,
        Trigger = trigger,
        Reason = reason,
        ComputedAt = now,
        CreatedById = userId,
    };
}

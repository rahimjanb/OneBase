using OneBase.Application.Telegram.Reports;

namespace OneBase.Application.Telegram.Weekly;

/// <summary>Чистые формулы еженедельного отчёта: рабочие дни, прогноз, нужный темп, КПУ «добрать», корзины спящих, цели недели.</summary>
public static class WeeklyReportMath
{
    /// <summary>
    /// Обязательные данные PDF: заказы и визиты Linko и KPI Linko. Неделя прошла — заказы и визиты обновлены после её конца (полночь после
    /// субботы, Ташкент); неделя идёт — не устарели (6 ч). KPI — есть люди области. Остальное (акции, остатки, DSO, ДМП) — «нет данных» в PDF.
    /// </summary>
    public static IReadOnlyList<string> Incomplete(WeeklySupervisorReport r)
    {
        var weekOver = new DateTimeOffset(r.PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5));
        var finished = r.DataThrough >= r.PeriodEnd;
        var bad = r.SourceStatuses
            .Where(s => s.Key is WeeklySources.Orders or WeeklySources.Visits && (s.AsOf is not { } at || (finished ? at < weekOver : s.State != WeeklySourceStates.Ok)))
            .Select(s => s.Key)
            .ToList();
        if (!r.KpiMetrics.Available)
        {
            bad.Add(WeeklySources.Kpi);
        }

        return bad;
    }

    /// <summary>Неделя отчёта (пн–сб), в которую входит день; воскресенье относится к прошедшей неделе.</summary>
    public static (DateOnly Monday, DateOnly Saturday) Week(DateOnly day)
    {
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(5));
    }

    /// <summary>Последняя полная неделя (пн–сб) по отчётный день: если суббота недели дня ещё не прошла — предыдущая.</summary>
    public static (DateOnly Monday, DateOnly Saturday) LastFullWeek(DateOnly dataThrough)
    {
        var (monday, saturday) = Week(dataThrough);
        return dataThrough >= saturday ? (monday, saturday) : (monday.AddDays(-7), saturday.AddDays(-7));
    }

    /// <summary>По какой день строить PDF запуска дня runDay: последняя полная неделя пн–сб (суббота — эта неделя, понедельник — прошлая).</summary>
    public static DateOnly ScheduledThrough(DateOnly runDay) => LastFullWeek(runDay).Saturday;

    public static int WorkDays(DateOnly from, DateOnly to, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            n += DailyPlanMath.IsWorkingDay(d, holidays) ? 1 : 0;
        }

        return n;
    }

    /// <summary>Рабочие дни месяца и недели по отчётный день: прошло — по dataThrough включительно, осталось — после него.</summary>
    public static WeeklyCalendar Calendar(DateOnly dataThrough, DateOnly weekFrom, DateOnly weekTo, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var monthStart = new DateOnly(dataThrough.Year, dataThrough.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var total = WorkDays(monthStart, monthEnd, holidays);
        var done = WorkDays(monthStart, dataThrough, holidays);
        return new WeeklyCalendar(total, done, Math.Max(0, total - done), WorkDays(weekFrom, weekTo, holidays),
            WorkDays(weekFrom, dataThrough < weekTo ? dataThrough : weekTo, holidays), total == 0 ? null : (decimal)done / total);
    }

    /// <summary>Прогноз месяца по рабочим дням: факт ÷ прошло × всего; ничего не прошло — null.</summary>
    public static decimal? Forecast(decimal fact, int done, int total) => done <= 0 || total <= 0 ? null : fact / done * total;

    public static decimal? Ratio(decimal? value, decimal? basis) => value is { } v && basis is { } b && b != 0 ? v / b : null;

    /// <summary>Наименьшая база «к прошлой неделе / месяцу», кг: от меньшей процент ничего не значит (15 кг → 43 кг = «+185%»).</summary>
    public const decimal MinChangeBaseKg = 50;

    /// <summary>
    /// Изменение к базе: 1,22 → +22%. Базы нет, она меньше MinChangeBaseKg или значение меньше нуля (неделя ушла в возвраты) — null:
    /// такой процент ничего не значит («−1496% к прошлой неделе»).
    /// </summary>
    public static decimal? Change(decimal value, decimal? basis) => basis is { } b && b >= MinChangeBaseKg && value >= 0 ? value / b - 1 : null;

    /// <summary>Нужный темп до доли плана: осталось (не меньше нуля) и в день по оставшимся рабочим дням; плана нет — null.</summary>
    public static WeeklyPace? Pace(decimal? plan, decimal share, decimal fact, int daysLeft)
    {
        if (plan is not > 0)
        {
            return null;
        }

        var target = plan.Value * share;
        var remaining = Math.Max(0, target - fact);
        return new WeeklyPace(target, remaining, remaining == 0 || daysLeft <= 0 ? null : remaining / daysLeft);
    }

    /// <summary>
    /// Показатель КПУ: добрать до порога = план × порог − факт (не меньше нуля), в день — по оставшимся рабочим дням; статус: порог
    /// пройден, «реально добрать» (до порога меньше gapPp п.п.) или «далеко»; премия — начисленная, если порог пройден, иначе
    /// «Бонус 100%» × порог (сколько даст выход на порог).
    /// </summary>
    public static WeeklyKpiCell KpiCell(string label, string unit, decimal plan, decimal fact, decimal pct, decimal? threshold, decimal? bonus100, decimal? premiumNow,
        int daysLeft, decimal gapPp)
    {
        if (threshold is not { } t)
        {
            return new WeeklyKpiCell(label, unit, plan, fact, pct, null, null, null, WeeklyKpiStatuses.NoThreshold, premiumNow);
        }

        if (pct >= t)
        {
            return new WeeklyKpiCell(label, unit, plan, fact, pct, t, null, null, WeeklyKpiStatuses.Passed, premiumNow);
        }

        var need = Math.Max(0, plan * t - fact);
        var status = (t - pct) * 100 < gapPp ? WeeklyKpiStatuses.Realistic : WeeklyKpiStatuses.Far;
        return new WeeklyKpiCell(label, unit, plan, fact, pct, t, need, daysLeft > 0 ? need / daysLeft : null, status, bonus100 is { } b ? b * t : null);
    }

    /// <summary>Округление целей: до целого, половина — вверх.</summary>
    public static int Round(decimal value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>Цель АКБ / ТТ по акциям: было × коэффициент, не меньше минимума (минимум — только если было больше нуля или задан).</summary>
    public static int Goal(int was, decimal factor, int min = 0) => Math.Max(min, Round(was * factor));

    /// <summary>Сколько спящих вернуть: доля от числа, округлённо; спящих нет — 0.</summary>
    public static int Return(int sleeping, decimal share) => sleeping <= 0 ? 0 : Round(sleeping * share);

    /// <summary>Цель продаж недели: план месяца ÷ рабочие дни месяца × рабочие дни недели; плана нет — null.</summary>
    /// <summary>
    /// План региона, разложенный по ТП: основа — первая из bases (по порядку важности: весовые планы, планы в сумах, факт), где вес есть хотя
    /// бы у половины ТП; такой нет — где ТП с весом больше всего. Доля ТП — его вес ÷ сумма весов. Ни в одной основе весов нет — пусто.
    /// </summary>
    public static Dictionary<long, decimal> SplitPlan(decimal regionPlan, IReadOnlyList<IReadOnlyDictionary<long, decimal>> bases)
    {
        static int Covered(IReadOnlyDictionary<long, decimal> b) => b.Values.Count(v => v > 0);
        var weights = bases.FirstOrDefault(b => Covered(b) > 0 && Covered(b) * 2 >= b.Count)
                      ?? bases.Where(b => Covered(b) > 0).OrderByDescending(Covered).FirstOrDefault();
        if (weights is null || regionPlan <= 0)
        {
            return [];
        }

        var total = weights.Values.Where(v => v > 0).Sum();
        return weights.ToDictionary(kv => kv.Key, kv => kv.Value > 0 ? regionPlan * kv.Value / total : 0);
    }

    public static decimal? SalesGoal(decimal? monthPlan, int monthWorkDays, int weekWorkDays) =>
        monthPlan is > 0 && monthWorkDays > 0 ? Math.Round(monthPlan.Value / monthWorkDays * weekWorkDays, 0, MidpointRounding.AwayFromZero) : null;

    /// <summary>
    /// Корзина спящей ТТ на отчётный день: последняя покупка в этом месяце, 15–29 дней назад — «15–30»; брала в прошлом месяце, в этом нет —
    /// «30+»; иначе — не спящая (null).
    /// </summary>
    public static string? SleepBucket(DateOnly? lastThisMonth, bool boughtPrevMonth, DateOnly day, int fromDays, int sleepDays)
    {
        if (lastThisMonth is { } last)
        {
            var days = day.DayNumber - last.DayNumber;
            return days >= fromDays && days < sleepDays ? WeeklySleepBuckets.Days15To30 : null;
        }

        return boughtPrevMonth ? WeeklySleepBuckets.Days30Plus : null;
    }
}

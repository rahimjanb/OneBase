using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Дневной план ТП для «ТП без заказов» — зафиксированный утром на бизнес-дату (TelegramReportDailyPlan) или посчитанный на лету для
/// прошлого дня. MonthPlan — план месяца, сум (null — в Linko нет); FactBefore — факт с 1-го по вчера включительно; Remaining — остаток;
/// WorkDaysLeft — рабочие дни с этого дня по конец месяца; DailyPlan — остаток ÷ дни (точный, округляется только при показе).
/// Status — DailyPlanMath.Ok / NoPlan / Done / DayOff / NoDays; Note — диагностика (почему плана нет).
/// </summary>
public sealed record DailyPlanRow(decimal? MonthPlan, decimal FactBefore, decimal? Remaining, int WorkDaysLeft, decimal? DailyPlan, string Status, string? Note)
{
    /// <summary>Строка входит в расчёт региона: план на день есть или месячный план уже выполнен (план дня 0).</summary>
    public bool Applies => Status is DailyPlanMath.Ok or DailyPlanMath.Done;
}

/// <summary>Итог региона: план дня — сумма планов ТП с расчётом, факт — их же продажи; процент — из сумм, а не среднее процентов.</summary>
public sealed record DailyPlanTotal(decimal Plan, decimal Fact, int Included, int Done, int NoPlan, int Other);

/// <summary>
/// Расчёт дневного плана (ТЗ «Расчёт дневного плана в автоотправке»): остаток месячного плана ÷ оставшиеся рабочие дни. Рабочие дни —
/// пн–сб без праздников справочника OneBase, сегодняшний день включается. Чистые функции — без базы и времени.
/// </summary>
public static class DailyPlanMath
{
    public const string Ok = "ok";
    public const string NoPlan = "no-plan";
    public const string Done = "done";
    public const string DayOff = "day-off";
    public const string NoDays = "no-days";

    public static bool IsWorkingDay(DateOnly day, IReadOnlyDictionary<DateOnly, string> holidays) =>
        day.DayOfWeek != DayOfWeek.Sunday && !holidays.ContainsKey(day);

    /// <summary>Рабочие дни с day включительно по последний день его месяца.</summary>
    public static int WorkDaysLeft(DateOnly day, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var end = new DateOnly(day.Year, day.Month, 1).AddMonths(1).AddDays(-1);
        var count = 0;
        for (var d = day; d <= end; d = d.AddDays(1))
        {
            if (IsWorkingDay(d, holidays))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// План на день. Порядок правил: нет плана месяца (или он 0) — «—», не ноль; выходной или праздник — не считается; остаток ≤ 0 — план
    /// выполнен (план дня 0, процент «выполнен»); рабочих дней не осталось — «—»; иначе остаток ÷ дни.
    /// </summary>
    public static DailyPlanRow Compute(decimal? monthPlan, decimal factBefore, DateOnly day, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        if (monthPlan is not > 0)
        {
            return new DailyPlanRow(monthPlan, factBefore, null, 0, null, NoPlan, monthPlan is null ? "Нет плана месяца в Linko" : "План месяца в Linko равен 0");
        }

        var remaining = monthPlan.Value - factBefore;
        if (day.DayOfWeek == DayOfWeek.Sunday)
        {
            return new DailyPlanRow(monthPlan, factBefore, remaining, 0, null, DayOff, "Воскресенье — план на день не считается");
        }

        if (holidays.TryGetValue(day, out var holiday))
        {
            return new DailyPlanRow(monthPlan, factBefore, remaining, 0, null, DayOff, $"Праздник «{holiday}» — план на день не считается");
        }

        var days = WorkDaysLeft(day, holidays);
        if (remaining <= 0)
        {
            return new DailyPlanRow(monthPlan, factBefore, remaining, days, 0, Done, "План месяца выполнен");
        }

        if (days == 0)
        {
            return new DailyPlanRow(monthPlan, factBefore, remaining, 0, null, NoDays, "Рабочих дней до конца месяца не осталось");
        }

        return new DailyPlanRow(monthPlan, factBefore, remaining, days, remaining / days, Ok, null);
    }

    /// <summary>«План, сум»: целые сумы с пробелами; выполнен — «0»; не применим или строки нет — «—».</summary>
    public static ReportCell PlanCell(DailyPlanRow? row, bool bold = false) => row?.Status switch
    {
        Ok => new ReportCell(Number(row.DailyPlan), Bold: bold),
        Done => new ReportCell("0", Bold: bold),
        _ => new ReportCell("—", ReportTone.Muted, bold),
    };

    /// <summary>«% плана»: факт ÷ план дня целыми процентами; месячный план выполнен — «выполнен»; план не применим — «—».</summary>
    public static ReportCell PercentCell(decimal fact, DailyPlanRow? row, Func<string, string> t, bool bold = false) => row?.Status switch
    {
        Ok => ReportFormat.PercentCell(fact / row.DailyPlan!.Value, bold), // у Ok план дня всегда больше нуля
        Done => new ReportCell(t("выполнен"), ReportTone.Good, bold),
        _ => new ReportCell("—", ReportTone.Muted, bold),
    };

    /// <summary>
    /// Итог региона: в расчёт входят ТП с планом дня и с выполненным планом; без плана месяца — не нули, а «вне расчёта» (NoPlan);
    /// каждый ТП — один раз.
    /// </summary>
    public static DailyPlanTotal Sum(IEnumerable<(DailyPlanRow? Plan, decimal Fact)> rows)
    {
        decimal plan = 0, fact = 0;
        int included = 0, done = 0, noPlan = 0, other = 0;
        foreach (var (row, f) in rows)
        {
            switch (row?.Status)
            {
                case Ok:
                    plan += row.DailyPlan!.Value;
                    fact += f;
                    included++;
                    break;
                case Done:
                    fact += f;
                    included++;
                    done++;
                    break;
                case null:
                case NoPlan:
                    noPlan++;
                    break;
                default:
                    other++;
                    break;
            }
        }

        return new DailyPlanTotal(plan, fact, included, done, noPlan, other);
    }

    public static ReportCell TotalPlanCell(DailyPlanTotal total, bool bold = true) =>
        total.Included == 0 ? new ReportCell("—", ReportTone.Muted, bold) : new ReportCell(Number(total.Plan), Bold: bold);

    /// <summary>Процент региона — из сумм; план региона 0 — «выполнен» только когда выполнен у всех ТП расчёта, иначе «—»; деления нет.</summary>
    public static ReportCell TotalPercentCell(DailyPlanTotal total, Func<string, string> t, bool bold = true) =>
        total.Plan > 0
            ? ReportFormat.PercentCell(total.Fact / total.Plan, bold)
            : total.Included > 0 && total.Done == total.Included
                ? new ReportCell(t("выполнен"), ReportTone.Good, bold)
                : new ReportCell("—", ReportTone.Muted, bold);
}

using System.Text.RegularExpressions;
using OneBase.Application.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// Дневной план ТП для «ТП без заказов» (ТЗ «Расчёт дневного плана»): остаток месячного плана ÷ оставшиеся рабочие дни (пн–сб без праздников,
/// сегодня включительно); сценарии «нет плана», выходной и праздник, план выполнен, нет рабочих дней, план 0; ячейки «План, сум» и
/// «% плана»; итог региона из сумм; формат чисел и округление. Чистые функции — без базы.
/// </summary>
public sealed class DailyPlanTests
{
    private static readonly IReadOnlyDictionary<DateOnly, string> NoHolidays = new Dictionary<DateOnly, string>();
    private static readonly DateOnly Wed21 = new(2026, 10, 21); // среда: 21–24, 26–31 октября — 10 рабочих дней
    private static string T(string ru) => ru;

    [Fact]
    public void Standard_example_remainder_over_ten_working_days_and_forty_percent_by_13_00()
    {
        Assert.Equal(10, DailyPlanMath.WorkDaysLeft(Wed21, NoHolidays));
        var row = DailyPlanMath.Compute(100_000_000m, 50_000_000m, Wed21, NoHolidays);
        Assert.Equal((DailyPlanMath.Ok, 50_000_000m, 10, 5_000_000m), (row.Status, row.Remaining, row.WorkDaysLeft, row.DailyPlan));
        Assert.Equal("5 000 000", Plain(DailyPlanMath.PlanCell(row).Text));
        Assert.Equal("40%", DailyPlanMath.PercentCell(2_000_000m, row, T).Text);
        Assert.Equal("0%", DailyPlanMath.PercentCell(0m, row, T).Text); // факт к 13:00 — ноль
    }

    [Theory]
    [InlineData(100_000_000, 100_000_000)] // выполнен точно
    [InlineData(100_000_000, 120_000_000)] // перевыполнен
    public void Fulfilled_month_plan_gives_zero_daily_plan_and_the_word_done(decimal plan, decimal fact)
    {
        var row = DailyPlanMath.Compute(plan, fact, Wed21, NoHolidays);
        Assert.Equal((DailyPlanMath.Done, 0m), (row.Status, row.DailyPlan));
        Assert.Equal("0", DailyPlanMath.PlanCell(row).Text);
        Assert.Equal(("выполнен", ReportTone.Good), (DailyPlanMath.PercentCell(3_000_000m, row, T).Text, DailyPlanMath.PercentCell(3_000_000m, row, T).Tone));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Missing_or_zero_month_plan_is_a_dash_not_a_zero(int? plan)
    {
        var row = DailyPlanMath.Compute(plan, 5_000_000m, Wed21, NoHolidays);
        Assert.Equal(DailyPlanMath.NoPlan, row.Status);
        Assert.Null(row.DailyPlan);
        Assert.Contains(plan is null ? "Нет плана" : "равен 0", row.Note);
        Assert.Equal(("—", "—"), (DailyPlanMath.PlanCell(row).Text, DailyPlanMath.PercentCell(1_000_000m, row, T).Text));
        Assert.Equal("—", DailyPlanMath.PlanCell(null).Text); // строки на день нет совсем
    }

    [Fact]
    public void Sunday_and_holiday_have_no_daily_plan_and_no_division()
    {
        var sunday = new DateOnly(2026, 10, 25);
        var onSunday = DailyPlanMath.Compute(100_000_000m, 10_000_000m, sunday, NoHolidays);
        Assert.Equal((DailyPlanMath.DayOff, 0), (onSunday.Status, onSunday.WorkDaysLeft));
        Assert.Equal(("—", "—"), (DailyPlanMath.PlanCell(onSunday).Text, DailyPlanMath.PercentCell(1_000_000m, onSunday, T).Text));

        var holidays = new Dictionary<DateOnly, string> { [Wed21] = "Тестовый праздник" };
        var onHoliday = DailyPlanMath.Compute(100_000_000m, 10_000_000m, Wed21, holidays);
        Assert.Equal(DailyPlanMath.DayOff, onHoliday.Status);
        Assert.Contains("Тестовый праздник", onHoliday.Note);
        // Праздник выпадает из оставшихся рабочих дней: со вторника 20-го — 11 дней без праздника, 10 с ним.
        Assert.Equal(11, DailyPlanMath.WorkDaysLeft(new DateOnly(2026, 10, 20), NoHolidays));
        Assert.Equal(10, DailyPlanMath.WorkDaysLeft(new DateOnly(2026, 10, 20), holidays));
        Assert.Equal(DailyPlanMath.Ok, DailyPlanMath.Compute(100_000_000m, 10_000_000m, new DateOnly(2026, 10, 20), holidays).Status);
    }

    [Fact]
    public void Last_day_of_month_takes_the_whole_remainder_and_no_days_left_is_a_dash()
    {
        var saturday31 = new DateOnly(2026, 10, 31);
        var row = DailyPlanMath.Compute(100_000_000m, 70_000_000m, saturday31, NoHolidays);
        Assert.Equal((DailyPlanMath.Ok, 1, 30_000_000m), (row.Status, row.WorkDaysLeft, row.DailyPlan));

        // Воскресенье 31-го (май 2026): рабочих дней не осталось — выходной, деления нет; строка «нет рабочих дней» показывается как «—».
        var sunday31 = new DateOnly(2026, 5, 31);
        Assert.Equal(0, DailyPlanMath.WorkDaysLeft(sunday31, NoHolidays));
        Assert.Equal(DailyPlanMath.DayOff, DailyPlanMath.Compute(100_000_000m, 70_000_000m, sunday31, NoHolidays).Status);
        var noDays = new DailyPlanRow(100_000_000m, 70_000_000m, 30_000_000m, 0, null, DailyPlanMath.NoDays, "Рабочих дней до конца месяца не осталось");
        Assert.Equal(("—", "—"), (DailyPlanMath.PlanCell(noDays).Text, DailyPlanMath.PercentCell(5_000_000m, noDays, T).Text));
        Assert.False(noDays.Applies);
    }

    [Fact]
    public void Regional_total_is_the_sum_of_daily_plans_and_the_percent_comes_from_sums_not_from_averaging()
    {
        var a = DailyPlanMath.Compute(100_000_000m, 50_000_000m, Wed21, NoHolidays); // план дня 5 млн, факт 2 млн — 40%
        var b = DailyPlanMath.Compute(200_000_000m, 50_000_000m, Wed21, NoHolidays); // план дня 15 млн, факт 15 млн — 100%
        var done = DailyPlanMath.Compute(80_000_000m, 90_000_000m, Wed21, NoHolidays); // выполнен: план 0, факт 1 млн — в расчёт входит
        var none = DailyPlanMath.Compute(null, 0m, Wed21, NoHolidays); // без плана — вне расчёта, не ноль

        var total = DailyPlanMath.Sum([(a, 2_000_000m), (b, 15_000_000m), (done, 1_000_000m), (none, 4_000_000m), (null, 9_000_000m)]);
        Assert.Equal((20_000_000m, 18_000_000m, 3, 1, 2), (total.Plan, total.Fact, total.Included, total.Done, total.NoPlan));
        Assert.Equal("20 000 000", Plain(DailyPlanMath.TotalPlanCell(total).Text));
        Assert.Equal("90%", DailyPlanMath.TotalPercentCell(total, T).Text); // 18 / 20, а не среднее (40 + 100) / 2 = 70

        // План региона 0: «выполнен» только когда выполнен у всех в расчёте; без ТП с планом — «—».
        var allDone = DailyPlanMath.Sum([(done, 1_000_000m), (done, 0m)]);
        Assert.Equal("выполнен", DailyPlanMath.TotalPercentCell(allDone, T).Text);
        Assert.Equal("0", DailyPlanMath.TotalPlanCell(allDone).Text);
        var nobody = DailyPlanMath.Sum([(none, 1_000_000m), (null, 2_000_000m)]);
        Assert.Equal(("—", "—"), (DailyPlanMath.TotalPlanCell(nobody).Text, DailyPlanMath.TotalPercentCell(nobody, T).Text));
    }

    [Fact]
    public void Numbers_are_whole_sums_with_thousand_separators_and_percents_are_whole()
    {
        Assert.Equal("1 050 160", Plain(ReportFormat.Number(1_050_160m)));
        var row = DailyPlanMath.Compute(60_000_000m, 10_000_000m, new DateOnly(2026, 10, 29), NoHolidays); // 50 млн на 3 дня (29, 30, 31)
        Assert.Equal(50_000_000m / 3, row.DailyPlan); // внутри — точно
        Assert.Equal("16 666 667", Plain(DailyPlanMath.PlanCell(row).Text)); // на картинке — целые сумы
        Assert.Equal("67%", DailyPlanMath.PercentCell(50_000_000m / 3 * 2 / 3, row, T).Text);
        Assert.DoesNotContain("NaN", DailyPlanMath.PercentCell(0m, row, T).Text);
        Assert.DoesNotContain("∞", DailyPlanMath.PercentCell(0m, DailyPlanMath.Compute(10m, 10m, Wed21, NoHolidays), T).Text);
    }

    /// <summary>Разряды на картинке разделены неразрывным пробелом — для сравнения приводим к обычному.</summary>
    private static string Plain(string text) => Regex.Replace(text, @"[\s  ]", " ");
}

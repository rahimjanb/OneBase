using OneBase.Application.Analytik.Business;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Периоды Аналитика и с чем каждый сравнивается: тот же отрезок раньше, целые месяцы — с целыми месяцами.</summary>
public sealed class AnalytikPeriodTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    private static (string, string, string, string) Range(AnalytikPeriod p) =>
        ($"{p.From:yyyy-MM-dd}", $"{p.To:yyyy-MM-dd}", $"{p.PreviousFrom:yyyy-MM-dd}", $"{p.PreviousTo:yyyy-MM-dd}");

    [Theory]
    [InlineData("today", "2026-11-11", "2026-11-11", "2026-11-10", "2026-11-10")]
    [InlineData("yesterday", "2026-11-10", "2026-11-10", "2026-11-09", "2026-11-09")]
    [InlineData("week", "2026-11-05", "2026-11-11", "2026-10-29", "2026-11-04")]
    [InlineData("month", "2026-11-01", "2026-11-11", "2026-10-01", "2026-10-11")]
    [InlineData("quarter", "2026-10-01", "2026-11-11", "2026-07-01", "2026-08-11")]
    [InlineData("year", "2026-01-01", "2026-11-11", "2025-01-01", "2025-11-11")]
    [InlineData(null, "2026-11-01", "2026-11-11", "2026-10-01", "2026-10-11")]
    public void Standard_periods_compare_with_the_same_stretch_before(string? kind, string from, string to, string prevFrom, string prevTo) =>
        Assert.Equal((from, to, prevFrom, prevTo), Range(AnalytikPeriod.Of(kind, null, null, Today)));

    [Fact]
    public void Month_to_date_on_the_31st_is_capped_by_the_shorter_previous_month()
    {
        var p = AnalytikPeriod.Of("month", null, null, new DateOnly(2026, 10, 31));
        Assert.Equal(("2026-10-01", "2026-10-31", "2026-09-01", "2026-09-30"), Range(p));
    }

    [Fact]
    public void Whole_custom_months_compare_with_the_preceding_whole_months()
    {
        // Октябрь — с сентябрём (а не с 31.08 — 30.09), квартал из трёх месяцев — с тремя месяцами перед ним, февраль — с январём.
        Assert.Equal(("2026-10-01", "2026-10-31", "2026-09-01", "2026-09-30"),
            Range(AnalytikPeriod.Of("custom", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Today)));
        Assert.Equal(("2026-07-01", "2026-09-30", "2026-04-01", "2026-06-30"),
            Range(AnalytikPeriod.Of("custom", new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), Today)));
        Assert.Equal(("2026-02-01", "2026-02-28", "2026-01-01", "2026-01-31"),
            Range(AnalytikPeriod.Of("custom", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), Today)));
    }

    [Fact]
    public void Other_custom_ranges_compare_with_a_range_of_the_same_length()
    {
        Assert.Equal(("2026-10-05", "2026-10-18", "2026-09-21", "2026-10-04"),
            Range(AnalytikPeriod.Of("custom", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 18), Today)));
    }

    [Fact]
    public void Invalid_custom_ranges_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => AnalytikPeriod.Of("custom", null, new DateOnly(2026, 10, 1), Today));
        Assert.Throws<ArgumentException>(() => AnalytikPeriod.Of("custom", new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 1), Today));
        Assert.Throws<ArgumentException>(() => AnalytikPeriod.Of("custom", new DateOnly(2025, 1, 1), new DateOnly(2026, 6, 1), Today));
        Assert.Throws<ArgumentException>(() => AnalytikPeriod.Of("decade", null, null, Today));
    }
}

using OneBase.Application.Sales.Primary;

namespace OneBase.Sales.Tests;

/// <summary>Обзор «Первички»: период (месяц, квартал, с начала года), итог — сумма направлений, доли, сумма за тонну, выгрузка.</summary>
public class PrimaryOverviewTests
{
    private static readonly DateOnly Nov5 = new(2026, 11, 5);

    [Fact]
    public void Period_defaults_to_year_to_date_of_the_data_year_and_ends_at_the_data_day()
    {
        var p = PrimaryOverviewMath.Resolve(null, null, null, null, Nov5, Nov5);
        Assert.Equal(("ytd", 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), (DateOnly?)Nov5), (p.Type, p.Year, p.From, p.To, p.Through));
        Assert.Equal("2026 с начала года (01.01.2026 – 05.11.2026)", PrimaryOverviewMath.Label(p));
    }

    [Fact]
    public void Month_and_quarter_default_to_the_data_month_and_past_years_end_in_december()
    {
        var month = PrimaryOverviewMath.Resolve("month", 2026, null, null, Nov5, Nov5);
        Assert.Equal((11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), (DateOnly?)Nov5), (month.Month!.Value, month.From, month.To, month.Through));
        Assert.Equal("ноябрь 2026 (01.11.2026 – 05.11.2026)", PrimaryOverviewMath.Label(month));

        var quarter = PrimaryOverviewMath.Resolve("Quarter", 2026, null, null, Nov5, Nov5);
        Assert.Equal((4, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)), (quarter.Quarter!.Value, quarter.From, quarter.To));
        Assert.Equal("IV квартал 2026 (01.10.2026 – 05.11.2026)", PrimaryOverviewMath.Label(quarter));

        var second = PrimaryOverviewMath.Resolve("quarter", 2026, null, 2, Nov5, Nov5);
        Assert.Equal((new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30), (DateOnly?)new DateOnly(2026, 6, 30)), (second.From, second.To, second.Through));

        var lastYear = PrimaryOverviewMath.Resolve("month", 2025, null, null, Nov5, Nov5);
        Assert.Equal((12, (DateOnly?)new DateOnly(2025, 12, 31)), (lastYear.Month!.Value, lastYear.Through));
        // Месяц в будущем: данных нет.
        var future = PrimaryOverviewMath.Resolve("month", 2026, 12, null, Nov5, Nov5);
        Assert.Null(future.Through);
        Assert.Equal("декабрь 2026 (данных нет)", PrimaryOverviewMath.Label(future));
        // Непонятный тип — с начала года; год вне диапазона — год данных.
        Assert.Equal(("ytd", 2026), (PrimaryOverviewMath.Resolve("week", 1900, 13, 7, Nov5, Nov5).Type, PrimaryOverviewMath.Resolve("week", 1900, 13, 7, Nov5, Nov5).Year));
    }

    [Fact]
    public void Total_is_the_sum_of_directions_shares_add_up_and_price_per_ton_is_null_without_tons()
    {
        var (tons, amount, d) = PrimaryOverviewMath.Combine(
            new PrimaryDirectionSums(3_666_300m, 114_700_000_000m, 34, null, 412),
            new PrimaryDirectionSums(774_700m, 28_960_000_000m, null, 11, 88));
        Assert.Equal((4441.0m, 143_660_000_000m), (tons, amount));
        Assert.Equal(("republic", 3666.3m, 0.8256m, 0.7984m), (d[0].Id, d[0].Tons, d[0].ShareTons!.Value, d[0].ShareAmount!.Value));
        Assert.Equal(1m, d[0].ShareTons!.Value + d[1].ShareTons!.Value);
        Assert.Equal(31_285m, Math.Round(d[0].AvgPricePerTon!.Value / 1000)); // ≈ 31,3 млн сум за тонну
        Assert.Equal((11, 88), (d[1].Countries!.Value, d[1].Orders));

        var (zeroTons, _, empty) = PrimaryOverviewMath.Combine(new PrimaryDirectionSums(0, 0, 0, null, 0), new PrimaryDirectionSums(0, 0, null, 0, 0));
        Assert.Equal(0m, zeroTons);
        Assert.All(empty, x => Assert.Null(x.AvgPricePerTon));
        Assert.All(empty, x => Assert.Null(x.ShareTons));
    }

    [Fact]
    public void Workbook_has_directions_with_total_and_months()
    {
        var period = PrimaryOverviewMath.Resolve("quarter", 2026, null, 4, Nov5, Nov5);
        var (tons, amount, directions) = PrimaryOverviewMath.Combine(new PrimaryDirectionSums(1000m, 30_000_000m, 3, null, 2), new PrimaryDirectionSums(500m, 20_000_000m, null, 2, 1));
        var view = new PrimaryOverview(period, tons, amount, directions,
            [new PrimaryOverviewMonth(2026, 10, 0.6m, 18_000_000m, 0.3m, 12_000_000m), new PrimaryOverviewMonth(2026, 11, 0.4m, 12_000_000m, 0.2m, 8_000_000m)], [2026], Nov5);
        var book = PrimaryOverviewExport.Build(view);
        Assert.Equal("Первичка — IV квартал 2026 (01.10.2026 – 05.11.2026)", book.Title);
        Assert.Equal(["Направления", "По месяцам"], book.Sheets.Select(s => s.Name));
        Assert.True(book.Sheets[0].Total);
        Assert.Equal("Республика", book.Sheets[0].Rows[0][0].GetString());
        Assert.Equal(66.7m, book.Sheets[0].Rows[0][3].GetDecimal()); // доля по тоннам, %
        Assert.Equal(30_000_000m, book.Sheets[0].Rows[0][7].GetDecimal()); // сум за тонну
        Assert.Equal(["10.2026", "11.2026"], book.Sheets[1].Rows.Select(r => r[0].GetString()));
    }
}

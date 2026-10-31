using OneBase.AI.Grounding;

namespace OneBase.AI.Tests;

/// <summary>
/// Масштаб числа в подписи, а не у самого числа: «| Выручка, млн сум | 13 457,8 | 13 974,5 |» (ответ консультанта 31.10.2026 — оба числа ушли в
/// «не подтверждено», хотя это выручка из данных), заголовок столбца «Средний чек, тыс. сум», подпись «Выручка (млн сум): 13 457,8».
/// </summary>
public class ScaleLabelGroundingTests
{
    private static EvidenceSet Evidence(params decimal[] values)
    {
        var e = new EvidenceSet();
        foreach (var v in values)
        {
            e.Add(v);
        }

        return e;
    }

    [Fact]
    public void Scale_in_the_row_label_applies_to_the_row_numbers()
    {
        const string answer = """
            | Показатель | Октябрь | Сентябрь | Изменение |
            |---|---|---|---|
            | Выручка, млн сум | 13 457,8 | 13 974,5 | -3,7% (-516,7 млн) |
            | Заказы, шт. | 27 314 | 29 576 | -7,6% |
            """;

        var report = EvidenceCheck.Run(answer, Evidence(13_457_812_345m, 13_974_543_210m, 516_730_865m, 3.7m, 27_314m, 29_576m, 7.6m));

        Assert.Empty(report.Unsupported);
        Assert.Contains("13 457,8", report.Supported);
        Assert.Contains("13 974,5", report.Supported);
    }

    [Fact]
    public void Scale_in_the_column_header_applies_to_that_column_only()
    {
        const string answer = """
            | Регион | Выручка, тыс. сум | Заказы |
            |---|---|---|
            | Ташкент | 1 234 567,9 | 2 714 |
            """;

        var report = EvidenceCheck.Run(answer, Evidence(1_234_567_890m, 2_714m));
        Assert.Empty(report.Unsupported);

        // Заказы — без масштаба: 2 714 не превращается в 2 714 000.
        Assert.Contains("2 714", EvidenceCheck.Run(answer, Evidence(1_234_567_890m, 2_714_000m)).Unsupported);
    }

    [Fact]
    public void Scale_in_a_label_before_the_number_applies()
    {
        Assert.Empty(EvidenceCheck.Run("Средний чек (тыс. сум): 454,1 в октябре.", Evidence(454_072m)).Unsupported);
        Assert.Empty(EvidenceCheck.Run("Выручка, млн сум — 13 457,8.", Evidence(13_457_812_345m)).Unsupported);
    }

    [Fact]
    public void Without_a_scale_label_the_number_is_still_checked_as_written()
    {
        // Масштаб не указан нигде — 13 457,8 сум не равно 13,4 млрд: число не подтверждено.
        Assert.Contains("13 457,8", EvidenceCheck.Run("Выручка — 13 457,8, заказов 27 314.", Evidence(13_457_812_345m, 27_314m)).Unsupported);
        // «млн» у соседнего числа через запятую — не подпись этого числа.
        Assert.Contains("27 314", EvidenceCheck.Run("Выручка 13,5 млн сум, заказов 27 314.", Evidence(13_457_812m, 27_314_000_000m)).Unsupported);
        // Проценты масштабом подписи не умножаются.
        Assert.Contains("7,2%", EvidenceCheck.Run("| Средний чек, тыс. сум | рост 7,2% |", Evidence(7_200m)).Unsupported);
        // Своё число с другим масштабом — не перекрашивается подписью строки и проверяется как написано.
        Assert.Empty(EvidenceCheck.Run("| Выручка, млн сум | 13 457,8 | -516,7 млн |", Evidence(13_457_812_345m, 516_730_865m)).Unsupported);
    }

    [Fact]
    public void Context_scale_is_found_for_table_and_label_numbers_only()
    {
        var claims = NumberClaims.Find("| Выручка, млн сум | 13 457,8 | 12% |\nИтого 450 тыс.\nЗаказы: 27 314");
        Assert.Equal(1_000_000m, claims.Single(c => c.Text == "13 457,8").ContextScale);
        Assert.Null(claims.Single(c => c.Text == "12%").ContextScale);
        Assert.Null(claims.Single(c => c.Text.StartsWith("450")).ContextScale);
        Assert.Null(claims.Single(c => c.Text == "27 314").ContextScale);
    }
}

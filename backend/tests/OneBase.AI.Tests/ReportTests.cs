using System.Text.Json;
using ClosedXML.Excel;
using OneBase.AI.Consultant;
using OneBase.AI.Reports;
using OneBase.AI.Tools;
using OneBase.Application.Reports;
using OneBase.Infrastructure.Reports;
using Xunit;

namespace OneBase.AI.Tests;

public sealed class ReportTests
{
    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>Ответ инструмента продаж по регионам — как у get_sales_by_branch.</summary>
    private const string Branches = """
        {"period":{"year":2026,"month":10},"republic":{"factKg":209935,"planKg":561493},
         "regions":[
           {"name":"Самарканд","factKg":43665.4,"planKg":90545,"executionPct":48.2,"revenue":1592773050,"kpi":{"akb":812}},
           {"name":"Термез","factKg":30264,"planKg":46240,"executionPct":65.4,"revenue":1100000000,"kpi":{"akb":640}}
         ]}
        """;

    private static ReportSpec.RunTool Tool(string json, List<string>? calls = null) => (tool, input, _) =>
    {
        calls?.Add($"{tool}|{input.GetRawText()}");
        return Task.FromResult(tool == "get_sales_by_branch"
            ? new ToolResult(true, json, null, [new DataSource("Продажи → Республика", "октябрь 2026")])
            : ToolResult.Error($"Инструмент '{tool}' не найден."));
    };

    [Theory]
    [InlineData("Сделай отчёт по продажам за октябрь", true)]
    [InlineData("выгрузи ТП с невыполнением плана", true)]
    [InlineData("дай таблицу регионов в Excel", true)]
    [InlineData("скинь в эксель", true)]
    [InlineData("нужен файл xlsx по остаткам", true)]
    [InlineData("Сентябрь bo'yicha hisobot", true)]
    [InlineData("Как прошли продажи за этот месяц?", false)]
    [InlineData("Какие регионы не выполняют план?", false)]
    [InlineData("Как идёт экспорт в Таджикистан?", false)]
    public void Report_is_made_only_when_the_user_asks_for_it(string question, bool expected) =>
        Assert.Equal(expected, ReportIntent.Requested(question));

    [Fact]
    public async Task Sheet_rows_come_from_the_data_tool_not_from_the_model()
    {
        var calls = new List<string>();
        var args = J("""
            {"title":"Продажи по регионам, октябрь 2026","sheets":[
              {"name":"Регионы","source":{"tool":"get_sales_by_branch","input":{"year":2026,"month":10},"path":"regions"},
               "columns":[{"field":"name","header":"Регион"},{"field":"factKg","header":"Факт, кг"},{"field":"executionPct","header":"Выполнение"},
                          {"field":"revenue","header":"Выручка"},{"field":"kpi.akb","header":"АКБ"}],"total":true},
              {"name":"Республика","source":{"tool":"get_sales_by_branch","input":{"year":2026,"month":10},"path":"republic"}}
            ]}
            """);

        var report = await ReportSpec.BuildAsync(args, Tool(Branches, calls), CancellationToken.None);

        Assert.Single(calls); // один вызов инструмента на одинаковые аргументы
        var regions = report.Sheets[0];
        Assert.Equal(2, regions.Rows.Count);
        Assert.Equal(43665.4m, regions.Rows[0][1].GetDecimal());
        Assert.Equal(812, regions.Rows[0][4].GetInt32()); // вложенное поле
        Assert.Equal(
            [ReportFormats.Text, ReportFormats.Kg, ReportFormats.Percent, ReportFormats.Money, ReportFormats.Integer],
            regions.Columns.Select(c => c.Format));
        Assert.True(regions.Total);
        var republic = report.Sheets[1];
        Assert.Equal(["factKg", "planKg"], republic.Columns.Select(c => c.Header)); // без columns — простые поля объекта
        Assert.Single(republic.Rows);
        Assert.Equal(["Продажи → Республика (октябрь 2026)"], report.Sources);
        Assert.Equal(3, report.RowCount);
    }

    [Fact]
    public async Task Wrong_path_or_field_returns_what_is_there()
    {
        var path = await Assert.ThrowsAsync<ReportSpecException>(() => ReportSpec.BuildAsync(J("""
            {"title":"T","sheets":[{"name":"A","source":{"tool":"get_sales_by_branch","path":"agents"},"columns":[{"field":"name","header":"Имя"}]}]}
            """), Tool(Branches), CancellationToken.None));
        Assert.Contains("regions (строк: 2)", path.Message);

        var field = await Assert.ThrowsAsync<ReportSpecException>(() => ReportSpec.BuildAsync(J("""
            {"title":"T","sheets":[{"name":"A","source":{"tool":"get_sales_by_branch","path":"regions"},"columns":[{"field":"fact","header":"Факт"}]}]}
            """), Tool(Branches), CancellationToken.None));
        Assert.Contains("fact", field.Message);
        Assert.Contains("factKg", field.Message);
        Assert.Contains("kpi.akb", field.Message);

        var tool = await Assert.ThrowsAsync<ReportSpecException>(() => ReportSpec.BuildAsync(J("""
            {"title":"T","sheets":[{"name":"A","source":{"tool":"get_secret","path":""},"columns":[{"field":"x","header":"X"}]}]}
            """), Tool(Branches), CancellationToken.None));
        Assert.Contains("не найден", tool.Message);
    }

    [Fact]
    public async Task Own_rows_are_a_small_table_with_columns()
    {
        var report = await ReportSpec.BuildAsync(J("""
            {"title":"Итоги","sheets":[{"name":"Сводка","columns":[{"header":"Показатель"},{"header":"Значение","format":"kg"}],
              "rows":[["Факт",209935],["План",561493],["Без значения"]],"note":"Источник: OneBase"}]}
            """), Tool(Branches), CancellationToken.None);

        var sheet = Assert.Single(report.Sheets);
        Assert.Equal(3, sheet.Rows.Count);
        Assert.Equal(JsonValueKind.Null, sheet.Rows[2][1].ValueKind); // недостающее значение — пустая ячейка
        Assert.Equal("Источник: OneBase", sheet.Note);
        Assert.Contains("null", JsonSerializer.Serialize(report)); // пустая ячейка сохраняется в jsonb как null

        await Assert.ThrowsAsync<ReportSpecException>(() => ReportSpec.BuildAsync(J("""
            {"title":"T","sheets":[{"name":"A","columns":[{"header":"X"}],"rows":[[1,2]]}]}
            """), Tool(Branches), CancellationToken.None)); // значений больше, чем колонок
        await Assert.ThrowsAsync<ReportSpecException>(() => ReportSpec.BuildAsync(J("""
            {"title":"T","sheets":[{"name":"A","columns":[{"header":"X","format":"currency"}],"rows":[[1]]}]}
            """), Tool(Branches), CancellationToken.None)); // неизвестный формат
        await Assert.ThrowsAsync<ReportSpecException>(() => ReportSpec.BuildAsync(J("""{"title":"","sheets":[]}"""), Tool(Branches), CancellationToken.None));
    }

    [Fact]
    public async Task Workbook_has_numbers_formats_totals_and_a_frozen_header()
    {
        var report = await ReportSpec.BuildAsync(J("""
            {"title":"Продажи по регионам","sheets":[{"name":"Регионы: октябрь/2026","source":{"tool":"get_sales_by_branch","path":"regions"},
              "columns":[{"field":"name","header":"Регион"},{"field":"factKg","header":"Факт, кг"},{"field":"executionPct","header":"Выполнение"}],"total":true}]}
            """), Tool(Branches), CancellationToken.None);

        var bytes = new ReportWorkbookWriter().Write(report, new DateTimeOffset(2026, 10, 15, 6, 0, 0, TimeSpan.Zero));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = Assert.Single(workbook.Worksheets);
        Assert.Equal("Регионы октябрь2026", ws.Name); // без недопустимых для листа знаков
        Assert.Equal("Продажи по регионам", ws.Cell(1, 1).GetString());
        Assert.StartsWith("Сформировано в OneBase 15.10.2026 11:00", ws.Cell(2, 1).GetString());
        Assert.Equal("Факт, кг", ws.Cell(4, 2).GetString());
        Assert.Equal(43665.4, ws.Cell(5, 2).GetDouble(), 3);
        Assert.Equal(0.482, ws.Cell(5, 3).GetDouble(), 3); // проценты — долей с форматом %
        Assert.Equal("0.0%", ws.Cell(5, 3).Style.NumberFormat.Format);
        Assert.Equal("Итого", ws.Cell(7, 1).GetString());
        Assert.Equal("SUM(B5:B6)", ws.Cell(7, 2).FormulaA1);
        Assert.False(ws.Cell(7, 3).HasFormula); // проценты не складываются
        Assert.Equal(4, ws.SheetView.SplitRow);
    }

    [Fact]
    public void File_name_is_the_title_without_forbidden_characters() =>
        Assert.Equal("Продажи по ТП Самарканд, 10 2026.xlsx", ReportService.FileName("Продажи по ТП «Самарканд», 10/2026"));
}

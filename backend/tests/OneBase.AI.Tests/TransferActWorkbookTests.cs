using ClosedXML.Excel;
using OneBase.Application.Sales.Stock;
using OneBase.Infrastructure.Reports;

namespace OneBase.AI.Tests;

/// <summary>Акт приёма-передачи автозаказа в Excel: бланк Linko, строки числами, итог, подписи; остатков завода в акте нет.</summary>
public class TransferActWorkbookTests
{
    [Fact]
    public void Act_is_an_excel_sheet_by_the_linko_form_with_numbers_and_total()
    {
        var act = new TransferAct("Завод", "Андижон", new DateOnly(2026, 10, 23), "Склад завода",
        [
            new TransferActRow("250 Хрустящие палочки ROHAT BAMBUK, 3-кг", 4, "кг", 12, 39_700, 476_400),
            new TransferActRow("494 Хрустящие палочки Bamboo 400г 10 шт.", 1, "шт", 10, 34_000, 340_000),
        ], 816_400);

        var bytes = new TransferActWorkbookWriter().Write(act);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet(1);
        var cells = ws.CellsUsed().ToList();
        string Text(string value) => cells.Single(c => c.GetString() == value).Address.ToString()!;

        Assert.Contains(cells, c => c.GetString() == "ПРИЕМА-ПЕРЕДАЧИ");
        Assert.Contains(cells, c => c.GetString() == "Завод → Андижон");
        Assert.Contains(cells, c => c.GetString() == "23 Октябрь 2026");

        var header = ws.Cell(Text("Наименование")).Address.RowNumber;
        Assert.Equal(["Наименование", "Кор.", "Ед. изм.", "Кол-во", "Цена за шт, SUM", "Цена, SUM"], Enumerable.Range(1, 6).Select(c => ws.Cell(header, c).GetString()));
        var first = ws.Row(header + 1);
        Assert.Equal("250 Хрустящие палочки ROHAT BAMBUK, 3-кг", first.Cell(1).GetString());
        Assert.Equal((4d, 12d, 39_700d, 476_400d), (first.Cell(2).GetDouble(), first.Cell(4).GetDouble(), first.Cell(5).GetDouble(), first.Cell(6).GetDouble()));
        Assert.Equal("шт", ws.Cell(header + 2, 3).GetString());
        Assert.Equal("Итого:", ws.Cell(header + 3, 1).GetString());
        Assert.Equal(816_400d, ws.Cell(header + 3, 6).GetDouble());

        Assert.Contains(cells, c => c.GetString() == "Сдал");
        Assert.Contains(cells, c => c.GetString() == "Принял");
        Assert.DoesNotContain(cells, c => c.GetString().Contains("доступно", StringComparison.OrdinalIgnoreCase)); // остатков завода в акте нет
        Assert.Equal(XLPaperSize.A4Paper, ws.PageSetup.PaperSize);
    }
}

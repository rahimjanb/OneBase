using System.Globalization;
using System.Text.Json;
using OneBase.Application.Abstractions;
using OneBase.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OneBase.Infrastructure.Reports;

/// <summary>
/// Отчёт → PDF (QuestPDF): A4 альбомная; шапка — название, когда сформирован (Ташкент) и источники; каждый лист — подзаголовок и
/// таблица с повтором шапки на новой странице, числа справа в форматах колонок, итоговая строка, пояснение; внизу — номера страниц.
/// Те же листы, что в Excel (ReportWorkbookWriter). Очень длинный лист обрезается — полностью он в Excel, об этом написано под таблицей.
/// </summary>
public sealed class ReportPdfWriter : IReportPdfWriter
{
    public const int MaxRowsPerSheet = 3000;

    private static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly Color Navy = Color.FromHex("#1F3864");
    private static readonly Color Ink = Color.FromHex("#1E293B");
    private static readonly Color Muted = Color.FromHex("#64748B");
    private static readonly Color Line = Color.FromHex("#E2E8F0");
    private static readonly Color Head = Color.FromHex("#E8EEF8");
    private static readonly Color Total = Color.FromHex("#F3F5F9");

    public byte[] Write(AiReportContent report, DateTimeOffset createdAt)
    {
        QuestPdfSetup.EnsureReady();
        var uz = UiLanguage.IsUzbek;
        var stamp = (uz ? UiLanguage.Uzbek($"OneBase'da tuzilgan {createdAt.ToOffset(CompanyOffset):dd.MM.yyyy HH:mm} (Toshkent)") : $"Сформировано в OneBase {createdAt.ToOffset(CompanyOffset):dd.MM.yyyy HH:mm} (Ташкент)")
                    + (report.Sources.Count > 0 ? (uz ? UiLanguage.Uzbek(" · manbalar: ") : " · источники: ") + string.Join("; ", report.Sources) : "");
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontFamily("Inter").FontSize(8.5f).FontColor(Ink));
            page.Header().PaddingBottom(8).Column(column =>
            {
                column.Item().Text(report.Title).FontSize(15).SemiBold().FontColor(Navy);
                column.Item().Text(stamp).FontSize(7.5f).FontColor(Muted);
            });
            page.Content().Column(column =>
            {
                column.Spacing(14);
                if (report.Sheets.Count == 0)
                {
                    column.Item().Text(uz ? UiLanguage.Uzbek("Ma'lumot yo'q.") : "Данных нет.").FontColor(Muted);
                }

                foreach (var sheet in report.Sheets)
                {
                    column.Item().Element(e => Sheet(e, sheet, uz));
                }
            });
            page.Footer().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void Sheet(IContainer container, AiReportSheet sheet, bool uz)
    {
        var rows = sheet.Rows.Take(MaxRowsPerSheet).ToList();
        container.Column(column =>
        {
            column.Item().PaddingBottom(4).Text(sheet.Name).FontSize(11).SemiBold().FontColor(Navy);
            if (sheet.Rows.Count == 0)
            {
                column.Item().Text(uz ? UiLanguage.Uzbek("Bu filtr bo'yicha ma'lumot yo'q.") : "По этому фильтру данных нет.").FontColor(Muted);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    foreach (var c in sheet.Columns)
                    {
                        columns.RelativeColumn(c.Format == ReportFormats.Text ? 2.2f : 1);
                    }
                });
                table.Header(header =>
                {
                    foreach (var c in sheet.Columns)
                    {
                        var cell = header.Cell().Background(Head).BorderBottom(0.75f).BorderColor(Line).PaddingVertical(3).PaddingHorizontal(4);
                        (ReportFormats.IsNumeric(c.Format) ? cell.AlignRight() : cell).Text(c.Header).SemiBold().FontSize(7.5f);
                    }
                });
                foreach (var row in rows)
                {
                    for (var i = 0; i < sheet.Columns.Count; i++)
                    {
                        var format = sheet.Columns[i].Format;
                        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(2.5f).PaddingHorizontal(4);
                        (ReportFormats.IsNumeric(format) ? cell.AlignRight() : cell).Text(Format(i < row.Count ? row[i] : default, format));
                    }
                }

                if (sheet.Total)
                {
                    for (var i = 0; i < sheet.Columns.Count; i++)
                    {
                        var c = sheet.Columns[i];
                        var cell = table.Cell().Background(Total).PaddingVertical(3).PaddingHorizontal(4);
                        if (i == 0)
                        {
                            cell.Text(uz ? UiLanguage.Uzbek("Jami") : "Итого").SemiBold();
                        }
                        else if (ReportFormats.IsSummable(c.Format) && c.Sum)
                        {
                            var sum = sheet.Rows.Sum(r => i < r.Count && r[i].ValueKind == JsonValueKind.Number ? r[i].GetDecimal() : 0m);
                            cell.AlignRight().Text(Number(sum, c.Format)).SemiBold();
                        }
                        else
                        {
                            cell.Text("");
                        }
                    }
                }
            });
            if (sheet.Rows.Count > rows.Count)
            {
                column.Item().PaddingTop(4).Text(uz
                    ? UiLanguage.Uzbek($"PDF'da birinchi {rows.Count:#,0} qator ko'rsatilgan (jami {sheet.Rows.Count:#,0}) — to'liq ro'yxat Excel'da.")
                    : $"В PDF — первые {rows.Count.ToString("#,0", Ru)} строк из {sheet.Rows.Count.ToString("#,0", Ru)}; полностью — в Excel.").FontSize(7.5f).FontColor(Muted);
            }

            if (!string.IsNullOrWhiteSpace(sheet.Note))
            {
                column.Item().PaddingTop(4).Text(sheet.Note).FontSize(7.5f).FontColor(Muted);
            }
        });
    }

    private static string Format(JsonElement value, string format)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return Number(value.GetDecimal(), format);
            case JsonValueKind.String:
                var text = value.GetString() ?? "";
                if (format == ReportFormats.Date && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date.ToString("dd.MM.yyyy", Ru);
                }

                return text;
            case JsonValueKind.True:
                return "✓";
            default:
                return "";
        }
    }

    private static string Number(decimal value, string format) => format switch
    {
        ReportFormats.Integer => value.ToString("#,0", Ru),
        ReportFormats.Percent => value.ToString("0.0", Ru) + " %",
        ReportFormats.Money => value.ToString("#,0.##", Ru),
        _ => value.ToString("#,0.###", Ru),
    };
}

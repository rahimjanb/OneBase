using System.Text.Json;

namespace OneBase.Application.Reports;

/// <summary>Форматы колонок отчёта: как число показывается в предпросмотре и в Excel.</summary>
public static class ReportFormats
{
    public const string Text = "text";
    public const string Integer = "integer";
    public const string Number = "number";
    public const string Kg = "kg";
    public const string Money = "money";

    /// <summary>Проценты: значение в процентах (27,8 — это 27,8 %), в Excel — доля с форматом «0,0 %».</summary>
    public const string Percent = "percent";

    /// <summary>Дата «yyyy-MM-dd» или «yyyy-MM-ddTHH:mm…» — в Excel дата.</summary>
    public const string Date = "date";

    public static readonly IReadOnlyList<string> All = [Text, Integer, Number, Kg, Money, Percent, Date];

    public static bool IsNumeric(string format) => format is Integer or Number or Kg or Money or Percent;

    /// <summary>Суммируемые колонки: итоговая строка листа складывает только их (проценты и даты не складываются).</summary>
    public static bool IsSummable(string format) => format is Integer or Number or Kg or Money;
}

/// <summary>Колонка листа: заголовок и формат (ReportFormats).</summary>
public sealed record AiReportColumn(string Header, string Format);

/// <summary>
/// Лист отчёта. Ячейки строк — JSON: число, строка или null. Total — внизу итоговая строка (сумма суммируемых колонок).
/// Note — пояснение под таблицей (источник, оговорки).
/// </summary>
public sealed record AiReportSheet(string Name, IReadOnlyList<AiReportColumn> Columns, IReadOnlyList<IReadOnlyList<JsonElement>> Rows, bool Total = false, string? Note = null);

/// <summary>Отчёт: название, листы и источники данных (страницы OneBase, из которых взяты цифры).</summary>
public sealed record AiReportContent(string Title, IReadOnlyList<AiReportSheet> Sheets, IReadOnlyList<string> Sources)
{
    public int RowCount => Sheets.Sum(s => s.Rows.Count);
}

/// <summary>Файл Excel (xlsx) из отчёта.</summary>
public interface IReportWorkbookWriter
{
    byte[] Write(AiReportContent report, DateTimeOffset createdAt);
}

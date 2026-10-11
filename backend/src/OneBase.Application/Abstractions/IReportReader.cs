namespace OneBase.Application.Abstractions;

/// <summary>
/// Лист отчёта как таблица строк: Rows[0] — первая строка листа (в Excel — строка 1), ячейки — столбцы A, B, C…
/// Числа — в инвариантной записи (1234.5), даты — ГГГГ-ММ-ДД, остальное — текст ячейки.
/// </summary>
public sealed record ReportSheet(string Name, IReadOnlyList<IReadOnlyList<string>> Rows, int TotalRows, int TotalColumns, bool Truncated);

public sealed record ReportDocument(IReadOnlyList<ReportSheet> Sheets);

/// <summary>Чтение табличных отчётов (Excel .xlsx, CSV) для AI Data Manager. Реализация — в Infrastructure (ClosedXML).</summary>
public interface IReportReader
{
    IReadOnlyCollection<string> Extensions { get; }

    /// <exception cref="NotSupportedException">Формат не поддерживается или файл повреждён — текст для пользователя.</exception>
    ReportDocument Read(Stream content, string fileName);
}

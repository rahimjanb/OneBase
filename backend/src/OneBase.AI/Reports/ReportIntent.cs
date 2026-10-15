using System.Text.RegularExpressions;

namespace OneBase.AI.Reports;

/// <summary>
/// Просит ли пользователь отчёт-файл: «отчёт», Excel, xlsx, «выгрузи», «скачать», «таблицей в эксель» (и по-узбекски «hisobot»).
/// Только тогда AI-сотрудникам даётся инструмент отчёта — сам по себе консультант отчёты не формирует. «Экспорт» — раздел продаж,
/// а не выгрузка: по нему отчёт не включается.
/// </summary>
public static partial class ReportIntent
{
    public static bool Requested(string? question) => question is { Length: > 0 } q && Words().IsMatch(q);

    [GeneratedRegex(@"отч[её]т|excel|[эе]кс[еэ]л|иксел|\bxlsx?\b|выгру[зж]|скача|hisobot|\breport\b|spreadsheet", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Words();
}

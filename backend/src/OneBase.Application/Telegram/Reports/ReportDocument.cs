using System.Globalization;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Цвет значения на картинке: выполнение ≥ 100% — Good, 80–99% — Warn, ниже 80% — Bad (порог ТЗ), Muted — «нет данных».</summary>
public enum ReportTone
{
    Normal,
    Good,
    Warn,
    Bad,
    Muted,
}

/// <summary>Ячейка таблицы: текст как на картинке.</summary>
public sealed record ReportCell(string Text, ReportTone Tone = ReportTone.Normal, bool Bold = false)
{
    public static implicit operator ReportCell(string text) => new(text);
}

/// <summary>Колонка: заголовок, выравнивание (числа — вправо), относительная ширина.</summary>
public sealed record ReportColumn(string Title, bool Right = true, float Weight = 1);

/// <summary>Таблица раздела: заголовок «1. Общий план продаж на день — сум и кг», колонки, строки, итог, подпись справа.</summary>
public sealed record ReportTable(string Title, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<ReportCell>> Rows,
    IReadOnlyList<ReportCell>? Total = null, string? Hint = null);

/// <summary>Плитка KPI: «Факт 13:00» — «12,4 млн сум» — «41% плана».</summary>
public sealed record ReportKpi(string Label, string Value, string? Hint = null, ReportTone Tone = ReportTone.Normal);

/// <summary>
/// Отчёт, готовый к картинке и подписи: что за запуск, для какой роли и области, срез данных, плитки KPI, таблицы, проблемы и действия,
/// замечания о данных. Applies = false — условие отчёта не выполнено (напоминание ТП, у которого есть заказы): не отправляется, в журнал —
/// «пропущено» с SkipReason. Incomplete — данные неполные (Linko не обновился, нет плана): отчёт уходит с пометкой.
/// </summary>
public sealed record ReportDocument(
    string Job,
    ReportType Type,
    string Role,
    string Title,
    string Scope,
    string? Manager,
    DateOnly Date,
    string Period,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? DataAsOf,
    string Language,
    IReadOnlyList<ReportKpi> Kpis,
    IReadOnlyList<ReportTable> Tables,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Notes,
    string Caption,
    string Summary,
    bool Incomplete = false,
    bool Applies = true,
    string? SkipReason = null)
{
    /// <summary>Тестовая отправка (только выбранный чат): на картинке — полоса «ТЕСТОВАЯ ОТПРАВКА».</summary>
    public bool Test { get; init; }

    /// <summary>Текст для Telegram-агента («что было в отчёте?») и резервной отправки без картинки.</summary>
    public string Plain()
    {
        var lines = new List<string> { $"{Title} · {Scope} · {Period}", Caption };
        lines.AddRange(Kpis.Select(k => $"{k.Label}: {k.Value}{(k.Hint is null ? "" : $" ({k.Hint})")}"));
        foreach (var table in Tables)
        {
            lines.Add("");
            lines.Add(table.Title);
            lines.Add(string.Join(" | ", table.Columns.Select(c => c.Title)));
            lines.AddRange(table.Rows.Select(r => string.Join(" | ", r.Select(c => c.Text))));
            if (table.Total is { } total)
            {
                lines.Add(string.Join(" | ", total.Select(c => c.Text)));
            }
        }

        if (Problems.Count > 0)
        {
            lines.Add("");
            lines.AddRange(Problems.Select(p => "• " + p));
        }

        if (Notes.Count > 0)
        {
            lines.Add("");
            lines.AddRange(Notes.Select(n => "· " + n));
        }

        return string.Join("\n", lines);
    }
}

/// <summary>PNG-картинки отчёта: страницы 16:9 одного альбома.</summary>
public interface IReportRenderer
{
    IReadOnlyList<byte[]> Render(ReportDocument document);
}

/// <summary>Числа отчётов: целые с пробелами между разрядами, проценты, сокращения «млн», цвет выполнения.</summary>
public static class ReportFormat
{
    private const char Group = ' ';

    /// <summary>Порог ТЗ: выполнение ниже 80% — красным.</summary>
    public const decimal RedBelow = 0.8m;

    /// <summary>300 000 000 — целые, неразрывные пробелы между разрядами.</summary>
    public static string Number(decimal value)
    {
        var rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
        var text = Math.Abs(rounded).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', Group);
        return rounded < 0 ? "−" + text : text;
    }

    public static string Number(decimal? value) => value is { } v ? Number(v) : "—";

    /// <summary>Килограммы: до 100 — с десятой (24,7), больше — целые.</summary>
    public static string Kg(decimal? value) => value switch
    {
        null => "—",
        { } v when Math.Abs(v) < 100 && v != Math.Round(v) => Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ','),
        { } v => Number(v),
    };

    /// <summary>+12 500 000 / −3 000 000.</summary>
    public static string Signed(decimal? value) => value switch
    {
        null => "—",
        > 0 => "+" + Number(value.Value),
        _ => Number(value.Value),
    };

    /// <summary>Доля → «73%»; без знаменателя — «—».</summary>
    public static string Percent(decimal? share) => share is { } s ? $"{Math.Round(s * 100, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%" : "—";

    public static decimal? Share(decimal? fact, decimal? plan) => fact is { } f && plan is > 0 ? f / plan.Value : null;

    public static decimal? Share(int fact, int? plan) => plan is > 0 ? (decimal)fact / plan.Value : null;

    /// <summary>Цвет выполнения: ≥ 100% — зелёный, 80–99% — жёлтый, ниже 80% — красный; без плана — серый.</summary>
    public static ReportTone Tone(decimal? share) => share switch
    {
        null => ReportTone.Muted,
        >= 1m => ReportTone.Good,
        >= RedBelow => ReportTone.Warn,
        _ => ReportTone.Bad,
    };

    public static ReportCell PercentCell(decimal? share, bool bold = false) => new(Percent(share), Tone(share), bold);

    /// <summary>Коротко для подписи и журнала: 35,4 млн / 1,2 млрд / 540 тыс.</summary>
    public static string Short(decimal? value, string language)
    {
        if (value is not { } v)
        {
            return "—";
        }

        var uz = language == "uz";
        var abs = Math.Abs(v);
        var (div, unit) = abs >= 1_000_000_000m ? (1_000_000_000m, uz ? "mlrd" : "млрд")
            : abs >= 1_000_000m ? (1_000_000m, uz ? "mln" : "млн")
            : abs >= 1_000m ? (1_000m, uz ? "ming" : "тыс.")
            : (1m, "");
        var number = Math.Round(v / div, div == 1 ? 0 : 1, MidpointRounding.AwayFromZero).ToString(div == 1 ? "0" : "0.#", CultureInfo.InvariantCulture).Replace('.', ',');
        return unit.Length == 0 ? number + (uz ? " so'm" : " сум") : $"{number} {unit} {(uz ? "so'm" : "сум")}";
    }

    /// <summary>Дата «23.10.2026», отрезок «19.10–24.10».</summary>
    public static string Date(DateOnly d) => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string Range(DateOnly from, DateOnly to) =>
        from == to ? Date(from) : $"{from.ToString("dd.MM", CultureInfo.InvariantCulture)}–{to.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
}

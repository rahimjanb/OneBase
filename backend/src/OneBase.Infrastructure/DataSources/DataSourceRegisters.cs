using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Pnl;
using OneBase.Domain.Integrations;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.DataSources;

/// <summary>
/// Реестры взаиморасчётов из Google Sheets (поля ledger и terms): лист — владелец данных, OneBase держит его копию и заменяет её целиком при
/// каждой синхронизации, как копию Linko. Через план изменений AI Data Manager эти поля не идут: это тысячи операций (касса, расходы), а не
/// планы, и править их в OneBase нельзя — только в таблице (FieldOwnership: поле ведёт привязка).
/// </summary>
public static class DataSourceRegisters
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "yyyy-MM-dd HH:mm:ss"];
    private static readonly string[] Summary = ["итого", "всего", "total", "жами", "итог"];

    public static DateOnly? ParseDate(string? text)
    {
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return null;
        }

        return DateTime.TryParseExact(t, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? DateOnly.FromDateTime(d) : null;
    }

    private static bool IsSummary(string text)
    {
        var name = DataValues.Normalize(text);
        return Summary.Any(s => name == s || name.StartsWith(s + " ", StringComparison.Ordinal) || name.StartsWith(s + ":", StringComparison.Ordinal));
    }

    /// <summary>
    /// Строки реестра со всех листов привязки: без контрагента, даты или суммы, раньше DateFrom и строки «Итого» пропускаются; Categories —
    /// отбор по началу категории («101» — «101-Махаллий сотувдан тушум»).
    /// </summary>
    public static List<SalesDealerLedgerEntry> Ledger(DataSourceField field, Guid bindingId, int headerRow, LedgerFieldMapping m, ReportDocument document,
        IReadOnlyList<string> sheets)
    {
        var list = new List<SalesDealerLedgerEntry>();
        foreach (var name in sheets)
        {
            var sheet = ReportExtraction.Sheet(document, name);
            var date = ReportExtraction.Column(sheet, headerRow, m.DateColumn);
            var dealer = ReportExtraction.Column(sheet, headerRow, m.DealerColumn);
            var amount = ReportExtraction.Column(sheet, headerRow, m.AmountColumn);
            int? kind = string.IsNullOrWhiteSpace(m.KindColumn) ? null : ReportExtraction.Column(sheet, headerRow, m.KindColumn);
            int? category = string.IsNullOrWhiteSpace(m.CategoryColumn) ? null : ReportExtraction.Column(sheet, headerRow, m.CategoryColumn);
            int? comment = string.IsNullOrWhiteSpace(m.CommentColumn) ? null : ReportExtraction.Column(sheet, headerRow, m.CommentColumn);
            var filters = (m.Categories ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).ToList();

            for (var r = Math.Max(headerRow, 0); r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                var who = ReportExtraction.Cell(row, dealer);
                if (who.Length == 0 || IsSummary(who))
                {
                    continue;
                }

                var cat = category is { } c ? ReportExtraction.Cell(row, c) : null;
                if (filters.Count > 0 && (cat is null || !filters.Any(f => cat.StartsWith(f, StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                var day = ParseDate(ReportExtraction.Cell(row, date));
                var value = DataValues.ParseNumber(ReportExtraction.Cell(row, amount), out var ok);
                if (day is null || day < m.DateFrom || !ok || value is null or 0)
                {
                    continue;
                }

                list.Add(new SalesDealerLedgerEntry
                {
                    Field = field.Key,
                    BindingId = bindingId,
                    Date = day.Value,
                    Dealer = Clip(PnlMath.DealerName(who), 200),
                    DealerKey = Clip(PnlMath.DealerKey(who), 200),
                    Amount = value.Value,
                    Kind = kind is { } k ? Clip(ReportExtraction.Cell(row, k), 64) is { Length: > 0 } x ? x : null : null,
                    Category = cat is { Length: > 0 } ? Clip(cat, 200) : null,
                    Comment = comment is { } cm ? Clip(ReportExtraction.Cell(row, cm), 500) is { Length: > 0 } y ? y : null : null,
                    SourceRef = Clip($"{sheet.Name}!{DataValues.ColumnLetter(amount)}{r + 1}", 120),
                });
            }
        }

        return list;
    }

    /// <summary>
    /// Остатки на дату (заголовки групп акта — «Диллер», «Ритейл», «Долги» — это подытоги, не контрагенты; группа — в Category строк под ней):
    /// строки контрагент → сумма (строки реестра dealer_balances, Date — дата остатка); дата — из ячейки DateCell каждого листа
    /// («F1»), иначе Date. Нет даты — ошибка: остаток без даты нельзя привязать к акту.
    /// </summary>
    public static List<SalesDealerLedgerEntry> Balances(DataSourceField field, Guid bindingId, int headerRow, BalanceFieldMapping m, ReportDocument document,
        IReadOnlyList<string> sheets)
    {
        var list = new List<SalesDealerLedgerEntry>();
        foreach (var name in sheets)
        {
            var sheet = ReportExtraction.Sheet(document, name);
            DateOnly? date = m.Date;
            if (!string.IsNullOrWhiteSpace(m.DateCell))
            {
                var cell = System.Text.RegularExpressions.Regex.Match(m.DateCell.Trim().ToUpperInvariant(), "^([A-Z]+)([0-9]+)$");
                if (!cell.Success || DataValues.ColumnIndex(cell.Groups[1].Value) is not { } col || !int.TryParse(cell.Groups[2].Value, out var row) || row < 1)
                {
                    throw new DataManagerException($"Ячейка даты «{m.DateCell}» — укажите адрес вида F1.");
                }

                date = row <= sheet.Rows.Count ? ParseDate(ReportExtraction.Cell(sheet.Rows[row - 1], col)) : null;
                if (date is null)
                {
                    throw new DataManagerException($"«{sheet.Name}»: в ячейке {m.DateCell} нет даты начала периода.");
                }
            }

            if (date is null)
            {
                throw new DataManagerException("Не задана дата остатков.");
            }

            var dealer = ReportExtraction.Column(sheet, headerRow, m.DealerColumn);
            var amount = ReportExtraction.Column(sheet, headerRow, m.AmountColumn);
            string? group = null;
            for (var r = Math.Max(headerRow, 0); r < sheet.Rows.Count; r++)
            {
                var who = ReportExtraction.Cell(sheet.Rows[r], dealer);
                if (DataValues.Normalize(who) is "диллер" or "дилер" or "дилеры" or "ритейл" or "долги")
                {
                    // Заголовок группы акта стоит над её контрагентами — группа строк ниже (Category).
                    group = who.Trim();
                    continue;
                }

                var value = DataValues.ParseNumber(ReportExtraction.Cell(sheet.Rows[r], amount), out var ok);
                if (who.Length == 0 || IsSummary(who) || !ok || value is null || DataValues.Normalize(who) == "контрагент")
                {
                    continue;
                }

                list.Add(new SalesDealerLedgerEntry
                {
                    Field = field.Key,
                    BindingId = bindingId,
                    Date = date.Value,
                    Dealer = Clip(PnlMath.DealerName(who), 200),
                    DealerKey = Clip(PnlMath.DealerKey(who), 200),
                    Amount = value.Value,
                    Category = group is null ? null : Clip(group, 200),
                    SourceRef = Clip($"{sheet.Name}!{DataValues.ColumnLetter(amount)}{r + 1}", 120),
                });
            }
        }

        return list;
    }

    /// <summary>Условия контрагентов: срок оплаты (целые дни; пусто — без срока) и остаток долга на дату привязки.</summary>
    public static List<SalesDealerTerm> Terms(Guid bindingId, int headerRow, TermsFieldMapping m, ReportDocument document, IReadOnlyList<string> sheets, DateTimeOffset now)
    {
        var map = new Dictionary<string, SalesDealerTerm>();
        foreach (var name in sheets)
        {
            var sheet = ReportExtraction.Sheet(document, name);
            var dealer = ReportExtraction.Column(sheet, headerRow, m.DealerColumn);
            int? norm = string.IsNullOrWhiteSpace(m.NormColumn) ? null : ReportExtraction.Column(sheet, headerRow, m.NormColumn);
            int? opening = string.IsNullOrWhiteSpace(m.OpeningColumn) ? null : ReportExtraction.Column(sheet, headerRow, m.OpeningColumn);
            for (var r = Math.Max(headerRow, 0); r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                var who = ReportExtraction.Cell(row, dealer);
                if (who.Length == 0 || IsSummary(who))
                {
                    continue;
                }

                var days = norm is { } n ? DataValues.ParseNumber(ReportExtraction.Cell(row, n), out var okNorm) : null;
                var debt = opening is { } o ? DataValues.ParseNumber(ReportExtraction.Cell(row, o), out var okDebt) : null;
                var key = Clip(PnlMath.DealerKey(who), 200);
                map[key] = new SalesDealerTerm
                {
                    DealerKey = key,
                    Dealer = Clip(PnlMath.DealerName(who), 200),
                    NormDays = days is > 0 and < 1000 ? (int)Math.Round(days.Value) : null,
                    Opening = debt,
                    OpeningDate = debt is null ? null : m.OpeningDate,
                    BindingId = bindingId,
                    UpdatedAt = now,
                };
            }
        }

        return map.Values.ToList();
    }

    /// <summary>Заменяет реестр поля копией листа; одинаковое содержимое — «без изменений».</summary>
    public static async Task<DataSourceFieldResult> ReplaceLedgerAsync(OneBaseDbContext db, DataSourceField field, List<SalesDealerLedgerEntry> rows,
        SalesCacheSignal signal, CancellationToken ct)
    {
        static string Sign(SalesDealerLedgerEntry e) =>
            string.Join('|', e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.DealerKey, e.Amount.ToString(CultureInfo.InvariantCulture), e.Kind, e.Category, e.Comment);

        var old = await db.SalesDealerLedger.AsNoTracking().Where(e => e.Field == field.Key).ToListAsync(ct);
        if (old.Select(Sign).Order(StringComparer.Ordinal).SequenceEqual(rows.Select(Sign).Order(StringComparer.Ordinal)))
        {
            return new DataSourceFieldResult(field.Key, field.Label, DataSourceRules.Unchanged, "без изменений");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.SalesDealerLedger.Where(e => e.Field == field.Key).ExecuteDeleteAsync(ct);
        db.SalesDealerLedger.AddRange(rows);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        signal.Invalidate();
        return new DataSourceFieldResult(field.Key, field.Label, DataSourceRules.Ok,
            $"записано строк: {rows.Count} (было {old.Count}), сумма {rows.Sum(r => r.Amount).ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))}",
            Created: rows.Count, Deleted: old.Count);
    }

    public static async Task<DataSourceFieldResult> ReplaceTermsAsync(OneBaseDbContext db, DataSourceField field, List<SalesDealerTerm> rows, SalesCacheSignal signal,
        CancellationToken ct)
    {
        static string Sign(SalesDealerTerm t) => $"{t.DealerKey}|{t.NormDays}|{t.Opening?.ToString(CultureInfo.InvariantCulture)}|{t.OpeningDate}";
        var old = await db.SalesDealerTerms.AsNoTracking().ToListAsync(ct);
        if (old.Select(Sign).Order(StringComparer.Ordinal).SequenceEqual(rows.Select(Sign).Order(StringComparer.Ordinal)))
        {
            return new DataSourceFieldResult(field.Key, field.Label, DataSourceRules.Unchanged, "без изменений");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.SalesDealerTerms.ExecuteDeleteAsync(ct);
        db.SalesDealerTerms.AddRange(rows);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        signal.Invalidate();
        return new DataSourceFieldResult(field.Key, field.Label, DataSourceRules.Ok, $"записано контрагентов: {rows.Count} (было {old.Count})",
            Created: rows.Count, Deleted: old.Count);
    }

    private static string Clip(string text, int max) => text.Length > max ? text[..max] : text;
}

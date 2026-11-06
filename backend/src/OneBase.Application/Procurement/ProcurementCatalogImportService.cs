using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Позиция каталога, как она будет записана: строка файла, артикул, название, единица, категория, срок хранения, контроль, новая или нет.</summary>
public sealed record ProcurementCatalogImportSample(int Row, string Sku, string Name, string UnitCode, string Category, int? ShelfLifeDays, string QualityControl, bool TracksBatches,
    bool IsNew);

/// <summary>Итог загрузки каталога (DryRun — только проверка, ничего не сохранено): добавится, обновится, без изменений, пропущено, ошибки строк.</summary>
public sealed record ProcurementCatalogImportResult(bool DryRun, IReadOnlyList<string> Columns, int Created, int Updated, int Unchanged, int Skipped, IReadOnlyList<string> Errors,
    IReadOnlyList<ProcurementCatalogImportSample> Sample);

/// <summary>
/// Импорт каталога материалов из Excel или CSV (этап 7): по артикулу создаёт или обновляет позиции склада — название, единица, категория,
/// минимум, учёт партий, срок хранения, контроль качества, условия хранения. Правила те же, что в форме позиции: единицу не меняют у позиции с
/// движениями, партии включают без остатка (иначе «Ввести партии»), контроль качества — только с партиями. Остатки не трогает. Пустая ячейка —
/// поле не меняется. Загружает кладовщик или снабжение; dryRun — что и куда попадёт, без сохранения.
/// </summary>
public sealed class ProcurementCatalogImportService(IAppDbContext db, IReportReader reader, ProcurementStockLedger ledger, IAuditLogger audit)
{
    public const int MaxRows = 20_000;
    private const int MaxErrors = 60;

    private static readonly TableImport.Column[] Headers = TableImport.Columns(
    [
        ("sku", "Артикул", ["артикул", "арт", "код", "код товара", "sku", "artikul", "kod"]),
        ("name", "Название", ["наименование", "название", "материал", "товар", "позиция", "номи", "nomi", "mahsulot", "material"]),
        ("unit", "Единица", ["ед", "ед изм", "единица", "единица измерения", "ўлчов", "улчов", "o lchov", "olchov", "birlik"]),
        ("category", "Категория", ["категория", "группа", "тоифа", "toifa", "guruh"]),
        ("min", "Минимум", ["минимум", "мин остаток", "минимальный остаток", "min", "minimal qoldiq"]),
        ("batches", "По партиям", ["по партиям", "партии", "учёт партий", "учет партий", "partiyalar"]),
        ("shelf", "Срок хранения, дней", ["срок хранения", "срок хранения дней", "срок годности дней", "годен дней", "saqlash muddati"]),
        ("qc", "Контроль качества", ["контроль качества", "контроль", "входной контроль", "sifat nazorati"]),
        ("storage", "Условия хранения", ["условия хранения", "хранение", "saqlash sharoiti"]),
    ]);

    public async Task<ProcurementCatalogImportResult> ImportAsync(ProcurementContext ctx, Stream content, string fileName, CancellationToken ct, bool dryRun = false)
    {
        if (!ctx.Warehouse && !ctx.Manages)
        {
            throw new FieldForbiddenException("Каталог загружает кладовщик или снабжение.");
        }

        ReportDocument document;
        try
        {
            document = reader.Read(content, fileName, MaxRows);
        }
        catch (NotSupportedException e)
        {
            throw new FieldValidationException(e.Message);
        }

        var table = TableImport.Find(document, Headers, f => f.ContainsKey("sku") && (f.ContainsKey("name") || f.ContainsKey("unit") || f.ContainsKey("shelf") || f.ContainsKey("qc")))
                    ?? throw new FieldValidationException("Не найдена строка заголовков: нужен столбец «Артикул» и хотя бы один из «Название», «Ед.», «Срок хранения», «Контроль качества».");
        var units = (await db.ProcurementUnits.AsNoTracking().Select(u => u.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categories = await db.ProcurementCategories.AsNoTracking().Where(c => c.IsActive).ToListAsync(ct);
        var fallback = categories.FirstOrDefault(c => c.Code == "raw_materials") ?? categories.OrderBy(c => c.SortOrder).FirstOrDefault();
        var items = await db.ProcurementItems.ToListAsync(ct);
        var bySku = items.GroupBy(i => i.Sku.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
        var moved = (await db.ProcurementStockMovements.AsNoTracking().Select(m => m.ItemId).Distinct().ToListAsync(ct)).ToHashSet();
        var stocked = (await db.ProcurementStockBalances.AsNoTracking().Where(b => b.OnHand != 0).Select(b => b.ItemId).Distinct().ToListAsync(ct)).ToHashSet();
        var defaultWarehouse = await ledger.DefaultWarehouseIdAsync(ct);

        var errors = new List<string>();
        var sample = new List<ProcurementCatalogImportSample>();
        var seen = new HashSet<string>();
        int created = 0, updated = 0, unchanged = 0, skipped = 0;
        for (var r = table.Header + 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            string? Cell(string field)
            {
                if (!table.Columns.TryGetValue(field, out var i) || i >= row.Count)
                {
                    return null;
                }

                var value = row[i].Trim();
                return value.Length == 0 ? null : value;
            }

            var sku = Cell("sku");
            if (sku is null)
            {
                if (row.Any(c => c.Trim().Length > 0))
                {
                    Skip($"Строка {r + 1}: нет артикула — пропущена.");
                }

                continue;
            }

            if (sku.Length > 64 || !seen.Add(sku.ToLowerInvariant()))
            {
                Skip(sku.Length > 64 ? $"Строка {r + 1}: артикул длиннее 64 знаков." : $"Строка {r + 1}: артикул «{sku}» повторяется в файле — взята первая строка.");
                continue;
            }

            var item = bySku.GetValueOrDefault(sku.ToLowerInvariant());
            var isNew = item is null;
            var name = Cell("name");
            var unitText = Cell("unit");
            var unit = unitText is null ? null : units.FirstOrDefault(u => string.Equals(u, unitText, StringComparison.OrdinalIgnoreCase) || string.Equals(u.TrimEnd('.'), unitText.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
            var categoryText = Cell("category");
            var category = categoryText is null ? null : categories.FirstOrDefault(c => string.Equals(c.Name, categoryText, StringComparison.OrdinalIgnoreCase));
            if (isNew && name is null)
            {
                Skip($"Строка {r + 1} («{sku}»): новой позиции нужно название.");
                continue;
            }

            if (name is { Length: > 300 })
            {
                Skip($"Строка {r + 1} («{sku}»): название длиннее 300 знаков.");
                continue;
            }

            if (unitText is not null && unit is null)
            {
                Skip($"Строка {r + 1} («{sku}»): единицы «{unitText}» нет в справочнике.");
                continue;
            }

            if (isNew && unit is null)
            {
                Skip($"Строка {r + 1} («{sku}»): новой позиции нужна единица измерения.");
                continue;
            }

            if (categoryText is not null && category is null)
            {
                Skip($"Строка {r + 1} («{sku}»): категории «{categoryText}» нет — заведите её в «Заявки → Настройки».");
                continue;
            }

            decimal? min = null;
            if (Cell("min") is { } minText)
            {
                if (!TryNumber(minText, out var m) || m < 0 || decimal.Round(m, 3) != m)
                {
                    Skip($"Строка {r + 1} («{sku}»): минимум «{minText}» — число не меньше нуля, до трёх знаков.");
                    continue;
                }

                min = m;
            }

            int? shelf = null;
            if (Cell("shelf") is { } shelfText)
            {
                if (!int.TryParse(shelfText.Replace(" ", ""), out var d) || d < 1 || d > 3650)
                {
                    Skip($"Строка {r + 1} («{sku}»): срок хранения «{shelfText}» — целое число дней от 1 до 3650.");
                    continue;
                }

                shelf = d;
            }

            bool? batches = Cell("batches") is { } b ? Yes(b) : null;
            if (Cell("batches") is { } bText && batches is null)
            {
                Skip($"Строка {r + 1} («{sku}»): «По партиям» — да или нет, а не «{bText}».");
                continue;
            }

            ProcurementQualityControl? qc = null;
            if (Cell("qc") is { } qcText)
            {
                qc = Control(qcText);
                if (qc is null)
                {
                    Skip($"Строка {r + 1} («{sku}»): контроль качества — «нет», «документы» или «лаборатория», а не «{qcText}».");
                    continue;
                }
            }

            var storage = Cell("storage");
            if (storage is { Length: > 300 })
            {
                Skip($"Строка {r + 1} («{sku}»): условия хранения длиннее 300 знаков.");
                continue;
            }

            // Учёт партий: контроль качества его требует; включить у позиции с остатком нельзя — сначала «Ввести партии»; выключить с партиями — тоже нельзя.
            var tracks = batches ?? (qc is { } c && c != ProcurementQualityControl.None ? true : item?.TracksBatches ?? false);
            if (qc is { } control && control != ProcurementQualityControl.None && !tracks)
            {
                Skip($"Строка {r + 1} («{sku}»): контроль качества — у позиции, которая ведётся по партиям.");
                continue;
            }

            if (item is not null && tracks != item.TracksBatches && stocked.Contains(item.Id))
            {
                Skip($"Строка {r + 1} («{sku}»): у позиции есть остаток — {(tracks ? "разложите его по партиям кнопкой «Ввести партии» в карточке" : "учёт партий не выключить")}.");
                continue;
            }

            if (item is not null && unit is not null && unit != item.UnitCode && moved.Contains(item.Id))
            {
                Skip($"Строка {r + 1} («{sku}»): по позиции есть движения — единицу «{item.UnitCode}» не изменить.");
                continue;
            }

            if (isNew)
            {
                if (fallback is null && category is null)
                {
                    Skip($"Строка {r + 1} («{sku}»): укажите категорию.");
                    continue;
                }

                item = new ProcurementItem
                {
                    Sku = sku, Name = name!, UnitCode = unit!, CategoryId = (category ?? fallback)!.Id, MinQty = min ?? 0, TracksBatches = tracks, ShelfLifeDays = shelf,
                    StorageConditions = storage, QualityControl = tracks ? qc ?? ProcurementQualityControl.None : ProcurementQualityControl.None,
                };
                if (!dryRun)
                {
                    db.ProcurementItems.Add(item);
                    db.ProcurementStockBalances.Add(new ProcurementStockBalance { WarehouseId = defaultWarehouse, ItemId = item.Id });
                }

                bySku[sku.ToLowerInvariant()] = item;
                created++;
            }
            else
            {
                var before = (item!.Name, item.UnitCode, item.CategoryId, item.MinQty, item.TracksBatches, item.ShelfLifeDays, item.QualityControl, item.StorageConditions);
                var after = (name ?? item.Name, unit ?? item.UnitCode, category?.Id ?? item.CategoryId, min ?? item.MinQty, tracks, shelf ?? item.ShelfLifeDays,
                    tracks ? qc ?? item.QualityControl : ProcurementQualityControl.None, storage ?? item.StorageConditions);
                if (before == after)
                {
                    unchanged++;
                }
                else
                {
                    if (!dryRun)
                    {
                        (item.Name, item.UnitCode, item.CategoryId, item.MinQty, item.TracksBatches, item.ShelfLifeDays, item.QualityControl, item.StorageConditions) = after;
                        item.UpdatedAt = DateTimeOffset.UtcNow;
                    }

                    updated++;
                }
            }

            if (sample.Count < 20)
            {
                var cat = categories.FirstOrDefault(x => x.Id == (category?.Id ?? item!.CategoryId))?.Name ?? "";
                sample.Add(new ProcurementCatalogImportSample(r + 1, sku, name ?? item!.Name, unit ?? item!.UnitCode, cat, shelf ?? item!.ShelfLifeDays,
                    (tracks ? qc ?? item!.QualityControl : ProcurementQualityControl.None).ToString(), tracks, isNew));
            }
        }

        if (!dryRun && created + updated > 0)
        {
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.catalog.imported", "procurement_item", null,
                new { file = fileName.Length <= 200 ? fileName : fileName[^200..], created, updated, unchanged, skipped }, ct);
        }

        return new ProcurementCatalogImportResult(dryRun, TableImport.Mapped(Headers, table), created, updated, unchanged, skipped, errors, sample);

        void Skip(string text)
        {
            skipped++;
            if (errors.Count < MaxErrors)
            {
                errors.Add(text);
            }
        }
    }

    private static bool TryNumber(string text, out decimal value) =>
        decimal.TryParse(text.Replace(" ", "").Replace(' '.ToString(), "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static bool? Yes(string text) => TableImport.Norm(text) switch
    {
        "да" or "есть" or "1" or "ha" or "+" or "yes" or "x" => true,
        "нет" or "0" or "yo q" or "yoq" or "-" or "no" => false,
        _ => null,
    };

    private static ProcurementQualityControl? Control(string text) => TableImport.Norm(text) switch
    {
        "нет" or "без контроля" or "-" or "yo q" or "yoq" or "0" => ProcurementQualityControl.None,
        "документы" or "проверка документов" or "hujjatlar" => ProcurementQualityControl.Documents,
        "лаборатория" or "документы и лаборатория" or "lab" or "laboratoriya" => ProcurementQualityControl.Lab,
        _ => null,
    };
}

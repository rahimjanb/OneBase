using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Строка примера в итоге загрузки: начальный остаток позиции или строка нового документа.</summary>
public sealed record ProcurementStockImportSample(DateOnly? Date, string Kind, string? Place, string Sku, string Name, string UnitCode, decimal Quantity);

/// <summary>
/// Итог загрузки движений склада (или проверки файла — DryRun, ничего не сохранено). Mode: opening — первая загрузка, по файлу заводятся
/// начальные остатки; movements — проводятся только новые строки. Columns — какой столбец файла куда пошёл. Rows — строк движений в файле,
/// Known — учтены раньше, New — учитываются сейчас; Receipts, Issues, WriteOffs — документы; Posted и Skipped — строки; Clamped — позиции,
/// у которых по файлу остаток меньше нуля (заведены нулём); NewItems и NewDepartments — что добавится в каталог и в отделы. UnknownFile —
/// склад уже загружали, а из этого файла не учтено ни одной строки: все его строки пойдут новыми движениями (другой журнал?).
/// </summary>
public sealed record ProcurementStockImportResult(
    bool DryRun,
    string Mode,
    IReadOnlyList<string> Columns,
    int Rows,
    int Known,
    int New,
    int Receipts,
    int Issues,
    int WriteOffs,
    int Adjustments,
    int Posted,
    int Skipped,
    int Items,
    int NewItems,
    IReadOnlyList<string> NewDepartments,
    int Clamped,
    DateOnly? FirstDate,
    DateOnly? LastDate,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProcurementStockImportSample> Sample,
    bool UnknownFile = false);

/// <summary>
/// Загрузка движений склада из Excel/CSV — журнала, который ведут в таблице (дата, приход/расход, артикул, название, единица, количество,
/// цех или склад). Столбцы узнаются сами (см. Headers). Первая загрузка — начальные остатки: по всем строкам файла считается остаток каждой
/// позиции (приход минус расход) и заводится одной инвентаризацией; история остаётся в файле. Следующие загрузки того же, дописанного
/// файла проводят только новые строки (учтённые помнятся по отпечатку — ProcurementStockImportRow): приход, выдача отделу, списание
/// («Утилизация», «Испарения»…) — по документу на каждую отправку формы (дата заполнения, вид, цех). Цех или склад, которого нет среди
/// отделов, становится новым отделом; артикул, которого нет в каталоге, — новой позицией («Сырьё»). Остаток не уходит в минус: такая
/// строка пропускается и загрузится в следующий раз (после прихода или инвентаризации). Загружает кладовщик.
/// </summary>
public sealed class ProcurementStockImportService(IAppDbContext db, IReportReader reader, ProcurementStockLedger ledger, IProcurementNumbers numbers, IAuditLogger audit)
{
    /// <summary>Строк на листе — с запасом: журнал склада растёт на тысячи строк в месяц.</summary>
    public const int MaxRows = 200_000;

    private const int MaxErrors = 60;

    private static readonly TableImport.Column[] Headers = TableImport.Columns(
    [
        ("filled", "Дата заполнения", ["дата заполнения", "отметка времени", "timestamp", "время заполнения", "дата и время заполнения", "тулдирилган вакт",
            "тўлдирилган вақт", "vaqt belgisi"]),
        ("date", "Дата", ["дата", "дата движения", "дата операции", "дата документа", "сана", "sana", "date", "кун"]),
        ("kind", "Приход / расход", ["п р", "приход расход", "приход или расход", "вид", "тип", "операция", "вид движения", "движение", "кирим чиким", "кирим чиқим",
            "kirim chiqim"]),
        ("in", "Приход", ["приход", "кирим", "kirim", "поступление"]),
        ("out", "Расход", ["расход", "чиким", "чиқим", "chiqim", "выдача", "отпуск"]),
        ("sku", "Артикул", ["артикул", "код", "sku", "код товара", "артикул товара", "kod"]),
        ("name", "Название", ["хомашё номи", "хомашё", "наименование", "название", "наименование товара", "товар", "номенклатура", "материал", "сырье", "номи", "nomi",
            "маҳсулот", "mahsulot"]),
        ("unit", "Ед. изм.", ["ед изм", "ед измерения", "единица", "единица измерения", "ед", "улчов бирлиги", "ўлчов бирлиги", "o lchov birligi", "unit"]),
        ("qty", "Количество", ["количество", "кол во", "миқдор", "микдор", "miqdor", "сони", "soni", "qty"]),
        ("place", "Цех / склад", ["цех ва склад номлари", "цех склад", "цех", "склад", "получатель", "откуда куда", "куда", "откуда", "кому", "отдел", "подразделение",
            "булим", "bo lim"]),
        ("batch", "Партия", ["партия", "номер партии", "partiya"]),
        ("reference", "Номер документа", ["номер документа", "накладная", "номер накладной", "№ документа", "hujjat"]),
        ("price", "Цена", ["цена", "цена за единицу", "нарх", "narx"]),
        ("comment", "Комментарий", ["комментарий", "примечание", "изох", "izoh"]),
    ]);

    /// <summary>Цех или склад расхода, который значит списание.</summary>
    private static readonly string[] WriteOffStems = ["утилиз", "испар", "списан", "брак", "порч", "недостач", "потер", "isrof", "исроф", "бузил"];

    /// <summary>Получатель расхода → существующий отдел по смыслу («• Отдел снабжения •», «Савдо булими»).</summary>
    private static readonly (string Code, string[] Stems)[] DepartmentStems =
    [
        ("supply", ["снабжен", "таъминот", "taminot", "ta minot"]),
        ("marketing", ["маркетинг", "marketing"]),
        ("sales", ["продаж", "савдо", "savdo", "сотув", "sotuv"]),
        ("finance", ["финанс", "молия", "moliya", "бухгалт"]),
        ("hr", ["кадр", "персонал"]),
        ("production", ["производств", "ишлаб чикариш", "ishlab chiqarish"]),
    ];

    /// <summary>Единица из файла → код справочника единиц.</summary>
    private static readonly Dictionary<string, string> Units = new()
    {
        ["кг"] = "кг", ["kg"] = "кг", ["килограмм"] = "кг", ["кило"] = "кг",
        ["г"] = "г", ["гр"] = "г", ["грамм"] = "г", ["gr"] = "г",
        ["т"] = "т", ["тн"] = "т", ["тонна"] = "т", ["tonna"] = "т",
        ["л"] = "л", ["лт"] = "л", ["литр"] = "л", ["litr"] = "л", ["l"] = "л",
        ["шт"] = "шт", ["штук"] = "шт", ["штука"] = "шт", ["дона"] = "шт", ["dona"] = "шт",
        ["м"] = "м", ["метр"] = "м", ["metr"] = "м",
        ["м2"] = "м²", ["м²"] = "м²", ["кв м"] = "м²", ["м3"] = "м³", ["м³"] = "м³", ["куб м"] = "м³",
        ["упак"] = "упак", ["уп"] = "упак", ["упаковка"] = "упак",
        ["пачка"] = "пачка", ["пач"] = "пачка", ["рулон"] = "рулон",
        ["коробка"] = "коробка", ["кор"] = "коробка", ["кути"] = "коробка", ["қути"] = "коробка",
        ["компл"] = "компл", ["комплект"] = "компл",
    };

    private enum Flow
    {
        In,
        Out,
        WriteOff,
    }

    /// <summary>Строка движения из файла (разобранная). ItemKey — артикул (без регистра) или, без артикула, название.</summary>
    private sealed record Line(int Row, DateTimeOffset? Filled, DateOnly Date, Flow Flow, string? Place, string? Sku, string Name, string? Unit, decimal Quantity,
        decimal? Price, string? Batch, string? Reference, string? Comment)
    {
        public string Key { get; set; } = "";
        public string ItemKey => Sku is { } sku ? $"sku:{sku.ToLowerInvariant()}" : $"name:{TableImport.Norm(Name)}";
    }

    private sealed class ItemPlan(ProcurementItem item, bool isNew)
    {
        public ProcurementItem Item { get; } = item;
        public bool IsNew { get; } = isNew;
        public string Label => $"{Item.Sku} {Item.Name}";
    }

    public async Task<ProcurementStockImportResult> ImportAsync(ProcurementContext ctx, Stream content, string fileName, CancellationToken ct, bool dryRun = false)
    {
        if (!ctx.Warehouse)
        {
            throw new FieldForbiddenException("Движения склада загружает кладовщик.");
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

        var table = TableImport.Find(document, Headers, f =>
            (f.ContainsKey("sku") || f.ContainsKey("name")) && (f.ContainsKey("qty") || f.ContainsKey("in") || f.ContainsKey("out"))
            && (f.ContainsKey("date") || f.ContainsKey("filled")));
        if (table is null)
        {
            throw new FieldValidationException(
                "Не найдена строка заголовков движений: нужны столбцы с датой, артикулом или названием и количеством («Дата», «П/Р», «Артикул», «Количество», «Цех»…).");
        }

        if (table.Sheet.TotalRows > table.Sheet.Rows.Count)
        {
            throw new FieldValidationException($"На листе «{table.Sheet.Name}» больше {MaxRows} строк — разделите файл на части.");
        }

        var errors = new List<string>();
        void Error(string text)
        {
            if (errors.Count < MaxErrors)
            {
                errors.Add(text);
            }
        }

        var units = await db.ProcurementUnits.AsNoTracking().Select(u => u.Code).ToListAsync(ct);
        var (lines, rowCount, invalid) = Parse(table, units, Error);
        if (!table.Columns.ContainsKey("unit"))
        {
            Error("В файле нет столбца с единицей измерения — новые позиции каталога заводятся в штуках.");
        }

        return await ledger.RetryAsync(() => PlanAsync(ctx, fileName, table, lines, rowCount, invalid, errors, dryRun, ct));
    }

    private async Task<ProcurementStockImportResult> PlanAsync(ProcurementContext ctx, string fileName, TableImport.Table table, List<Line> lines, int rowCount,
        int invalid, List<string> parseErrors, bool dryRun, CancellationToken ct)
    {
        var errors = new List<string>(parseErrors);
        void Error(string text)
        {
            if (errors.Count < MaxErrors)
            {
                errors.Add(text);
            }
        }

        var skipped = invalid;
        var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var known = (await db.ProcurementStockImportRows.AsNoTracking().Where(r => r.WarehouseId == warehouse).Select(r => r.Key).ToListAsync(ct)).ToHashSet();
        var opening = known.Count == 0;
        var relevant = opening ? lines : lines.Where(l => !known.Contains(l.Key)).ToList();
        var knownCount = lines.Count - relevant.Count;

        // Позиции каталога: по артикулу (без регистра), без артикула — по названию; нет в каталоге — новая («Сырьё», единица — самая частая в файле).
        var catalog = await db.ProcurementItems.ToListAsync(ct);
        var bySku = catalog.GroupBy(i => i.Sku.Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
        var byName = catalog.GroupBy(i => TableImport.Norm(i.Name)).ToDictionary(g => g.Key, g => g.First());
        var category = await db.ProcurementCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Code == "raw_materials" ? 0 : 1).ThenBy(c => c.Name)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct) ?? throw new FieldConflictException("В снабжении нет ни одной категории — добавьте категорию в настройках заявок.");
        var plans = new Dictionary<string, ItemPlan>();
        foreach (var group in relevant.GroupBy(l => l.ItemKey))
        {
            var first = group.First();
            var existing = first.Sku is { } sku ? bySku.GetValueOrDefault(sku.ToLowerInvariant()) : byName.GetValueOrDefault(TableImport.Norm(first.Name));
            if (existing is not null)
            {
                plans[group.Key] = new ItemPlan(existing, false);
                continue;
            }

            var latest = group.OrderBy(l => l.Date).ThenBy(l => l.Row).Last();
            var unit = group.Where(l => l.Unit is not null).GroupBy(l => l.Unit!).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "шт";
            plans[group.Key] = new ItemPlan(new ProcurementItem
            {
                Sku = first.Sku ?? "", Name = latest.Name.Length <= 300 ? latest.Name : latest.Name[..300], CategoryId = category, UnitCode = unit,
            }, true);
        }

        // Единица строки должна совпасть с карточкой: остаток считается в одной единице.
        var accepted = new List<Line>();
        var wrongUnit = new Dictionary<(ItemPlan Plan, string Unit), List<int>>();
        foreach (var line in relevant)
        {
            var plan = plans[line.ItemKey];
            if (line.Unit is { } unit && unit != plan.Item.UnitCode)
            {
                skipped++;
                (wrongUnit.TryGetValue((plan, unit), out var rows) ? rows : wrongUnit[(plan, unit)] = []).Add(line.Row);
                continue;
            }

            accepted.Add(line);
        }

        foreach (var ((plan, unit), rows) in wrongUnit)
        {
            Error($"«{plan.Label}»: единица «{unit}» не совпадает с карточкой склада ({plan.Item.UnitCode}) — {(rows.Count == 1 ? "строка" : "строки")} "
                  + $"{string.Join(", ", rows.Take(8))}{(rows.Count > 8 ? "…" : "")} {(rows.Count == 1 ? "пропущена" : "пропущены")}.");
        }

        // Отделы для выдачи: по названию, по смыслу («Савдо булими» — продажи) или новый отдел с названием цеха.
        var departments = await db.Departments.ToListAsync(ct);
        var newDepartments = new List<Department>();
        Department DepartmentFor(string place)
        {
            var name = Clean(place);
            var loose = TableImport.Loose(name);
            var found = departments.Concat(newDepartments).FirstOrDefault(d => TableImport.Loose(d.Name) == loose);
            if (found is not null)
            {
                return found;
            }

            var norm = TableImport.Norm(name);
            if (DepartmentStems.FirstOrDefault(d => d.Stems.Any(norm.Contains)).Code is { } code && departments.FirstOrDefault(d => d.Code == code) is { } byMeaning)
            {
                return byMeaning;
            }

            var slug = Slug(name);
            var unique = slug;
            for (var n = 2; departments.Concat(newDepartments).Any(d => d.Code == unique); n++)
            {
                unique = $"{slug}-{n}";
            }

            var created = new Department { Code = unique, Name = name.Length <= 200 ? name : name[..200] };
            newDepartments.Add(created);
            return created;
        }

        // Все отделы выдачи — заранее: новые добавляются в базу одним разом.
        foreach (var line in accepted.Where(l => l.Flow == Flow.Out))
        {
            DepartmentFor(line.Place ?? "Производство");
        }

        // Остатки (у новых позиций — новые строки с нулями); при проверке файла ничего из этого не сохраняется.
        var balances = await ledger.BalancesAsync(warehouse, plans.Values.Select(p => p.Item.Id), ct);
        var onHand = plans.Values.ToDictionary(p => p, p => (balances[p.Item.Id].OnHand, balances[p.Item.Id].Reserved));
        var last = accepted.Count == 0 ? FieldClock.Today : accepted.Max(l => l.Date);
        var import = new ProcurementStockImport
        {
            WarehouseId = warehouse, FileName = fileName.Length <= 260 ? fileName : fileName[^260..], Mode = opening ? ProcurementStockImportMode.Opening : ProcurementStockImportMode.Movements,
            Rows = rowCount, ActorId = ctx.UserId,
        };
        var sample = new List<ProcurementStockImportSample>();
        var counted = new List<(Line Line, Guid? DocumentId)>();
        int receipts = 0, issues = 0, writeOffs = 0, adjustments = 0, clamped = 0;
        if (!dryRun)
        {
            foreach (var plan in plans.Values.Where(p => p.IsNew))
            {
                plan.Item.Sku = plan.Item.Sku.Length > 0 ? plan.Item.Sku : await numbers.NextAsync("SKU", ct);
                db.ProcurementItems.Add(plan.Item);
            }

            db.Departments.AddRange(newDepartments);
            db.ProcurementStockImports.Add(import);
        }

        if (opening)
        {
            // Начальные остатки: приход минус расход по всем строкам файла; меньше нуля — нулём; одна инвентаризация на разницу с учётом.
            var facts = accepted.GroupBy(l => plans[l.ItemKey]).Select(g => (Plan: g.Key, Fact: g.Sum(l => l.Flow == Flow.In ? l.Quantity : -l.Quantity))).ToList();
            ProcurementStockDocument? doc = null;
            foreach (var (plan, total) in facts.OrderBy(f => f.Plan.Item.Sku, StringComparer.OrdinalIgnoreCase))
            {
                var fact = total;
                if (fact < 0)
                {
                    clamped++;
                    Error($"«{plan.Label}»: по файлу остаток {ProcurementRules.Quantity(fact)} {plan.Item.UnitCode} — заведён нулём.");
                    fact = 0;
                }

                var (have, reserved) = onHand[plan];
                if (fact < reserved)
                {
                    Error($"«{plan.Label}»: по файлу {ProcurementRules.Quantity(fact)}, а в резерве заявок {ProcurementRules.Quantity(reserved)} — остаток не изменён.");
                    continue;
                }

                if (sample.Count < 8 && fact > 0)
                {
                    sample.Add(new ProcurementStockImportSample(last, "Opening", null, plan.Item.Sku, plan.Item.Name, plan.Item.UnitCode, fact));
                }

                if (fact == have)
                {
                    continue;
                }

                adjustments++;
                if (!dryRun)
                {
                    doc ??= await OpeningDocumentAsync(warehouse, ctx.UserId, fileName, last, ct);
                    ledger.Move(doc, balances[plan.Item.Id], ProcurementMovementKind.Adjustment, fact - have, plan.Label);
                }
            }

            counted.AddRange(accepted.Select(l => (l, doc?.Id)));
        }
        else
        {
            // Новые строки: документ на каждую отправку (дата заполнения или дата, вид, цех), позиция в документе — одной строкой.
            var groups = accepted.GroupBy(l => (Moment: l.Filled ?? Noon(l.Date), l.Flow, Place: TableImport.Norm(l.Place)))
                .OrderBy(g => g.Key.Moment).ThenBy(g => g.Min(l => l.Row));
            foreach (var group in groups)
            {
                var first = group.First();
                var moment = first.Filled is { } filled && FieldClock.LocalDate(filled) == first.Date ? filled : Noon(first.Date);
                var ok = new List<(ItemPlan Plan, decimal Quantity, decimal? Price, List<Line> Rows)>();
                foreach (var item in group.GroupBy(l => plans[l.ItemKey]))
                {
                    var quantity = item.Sum(l => l.Quantity);
                    var (have, reserved) = onHand[item.Key];
                    var after = first.Flow == Flow.In ? have + quantity : have - quantity;
                    if (after < 0 || after < reserved)
                    {
                        skipped += item.Count();
                        Error($"Строка {item.Min(l => l.Row)} («{item.Key.Label}», {first.Date:dd.MM.yyyy}): на складе {ProcurementRules.Quantity(have)} {item.Key.Item.UnitCode}"
                              + (reserved > 0 ? $", в резерве {ProcurementRules.Quantity(reserved)}" : "")
                              + $" — {ProcurementRules.Quantity(quantity)} не провести; строка загрузится в следующий раз.");
                        continue;
                    }

                    onHand[item.Key] = (after, reserved);
                    ok.Add((item.Key, quantity, first.Flow == Flow.In ? item.LastOrDefault(l => l.Price is not null)?.Price : null, item.ToList()));
                }

                if (ok.Count == 0)
                {
                    continue;
                }

                var kind = first.Flow switch
                {
                    Flow.In => ProcurementStockDocumentKind.Receipt,
                    Flow.WriteOff => ProcurementStockDocumentKind.WriteOff,
                    _ => ProcurementStockDocumentKind.Issue,
                };
                switch (kind)
                {
                    case ProcurementStockDocumentKind.Receipt:
                        receipts++;
                        break;
                    case ProcurementStockDocumentKind.WriteOff:
                        writeOffs++;
                        break;
                    default:
                        issues++;
                        break;
                }

                foreach (var (plan, quantity, _, _) in ok.Where(_ => sample.Count < 8))
                {
                    sample.Add(new ProcurementStockImportSample(first.Date, first.Flow.ToString(), first.Place is null ? null : Clean(first.Place), plan.Item.Sku, plan.Item.Name,
                        plan.Item.UnitCode, quantity));
                }

                ProcurementStockDocument? doc = null;
                if (!dryRun)
                {
                    doc = await ledger.DocumentAsync(kind, warehouse, ctx.UserId, ct);
                    doc.CreatedAt = moment;
                    var batches = group.Select(l => l.Batch).OfType<string>().Distinct().ToList();
                    var reference = group.Select(l => l.Reference).OfType<string>().FirstOrDefault()
                                    ?? (batches.Count == 0 ? null : (batches.Count == 1 ? "Партия " : "Партии ") + string.Join(", ", batches));
                    doc.Reference = Cut(reference, 100);
                    var place = first.Place is null ? null : Clean(first.Place);
                    var comments = group.Select(l => l.Comment).OfType<string>().Distinct().ToList();
                    switch (kind)
                    {
                        case ProcurementStockDocumentKind.Receipt:
                            doc.Comment = Cut(string.Join(" · ", new[] { place is null ? null : $"Откуда: {place}" }.Concat(comments).OfType<string>()), 1000);
                            break;
                        case ProcurementStockDocumentKind.WriteOff:
                            doc.Comment = Cut(string.Join(" · ", new[] { place ?? "Списание" }.Concat(comments)), 1000);
                            break;
                        default:
                            var department = place is null ? DepartmentFor("Производство") : DepartmentFor(place);
                            doc.DepartmentId = department.Id;
                            doc.Receiver = Cut(place, 200);
                            doc.Comment = comments.Count == 0 ? null : Cut(string.Join(" · ", comments), 1000);
                            break;
                    }

                    doc.Comment = string.IsNullOrEmpty(doc.Comment) ? $"Из файла «{Cut(fileName, 200)}»" : doc.Comment;
                    foreach (var (plan, quantity, price, _) in ok)
                    {
                        var delta = kind == ProcurementStockDocumentKind.Receipt ? quantity : -quantity;
                        var movementKind = kind switch
                        {
                            ProcurementStockDocumentKind.Receipt => ProcurementMovementKind.Receipt,
                            ProcurementStockDocumentKind.WriteOff => ProcurementMovementKind.WriteOff,
                            _ => ProcurementMovementKind.Issue,
                        };
                        ledger.Move(doc, balances[plan.Item.Id], movementKind, delta, plan.Label, price is { } p ? decimal.Round(p, 2) : null).CreatedAt = moment;
                    }
                }

                counted.AddRange(ok.SelectMany(x => x.Rows).Select(l => (l, doc?.Id)));
            }
        }

        var newItems = plans.Values.Count(p => p.IsNew);
        var result = new ProcurementStockImportResult(dryRun, opening ? "opening" : "movements", TableImport.Mapped(Headers, table), rowCount, knownCount, relevant.Count,
            receipts, issues, writeOffs, adjustments, counted.Count, skipped, plans.Count, newItems, newDepartments.Select(d => d.Name).ToList(), clamped,
            counted.Count == 0 ? null : counted.Min(c => c.Line.Date), counted.Count == 0 ? null : counted.Max(c => c.Line.Date), errors, sample,
            !opening && knownCount == 0 && relevant.Count > 0);
        if (dryRun || counted.Count == 0)
        {
            db.ChangeTracker.Clear(); // проверка файла (или нечего проводить): ничего не сохраняется
            return result;
        }

        (import.Posted, import.Skipped, import.Documents, import.LastDate) = (counted.Count, skipped, receipts + issues + writeOffs + (adjustments > 0 ? 1 : 0), result.LastDate);
        db.ProcurementStockImportRows.AddRange(counted.Select(c => new ProcurementStockImportRow { ImportId = import.Id, WarehouseId = warehouse, Key = c.Line.Key, DocumentId = c.DocumentId }));
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.stock.imported", "procurement_stock_import", import.Id.ToString(),
            new { import.FileName, Mode = import.Mode.ToString(), import.Rows, import.Posted, import.Skipped, import.Documents, NewItems = newItems, NewDepartments = result.NewDepartments },
            ct);
        return result;
    }

    private async Task<ProcurementStockDocument> OpeningDocumentAsync(Guid warehouse, Guid actor, string fileName, DateOnly last, CancellationToken ct)
    {
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.Inventory, warehouse, actor, ct);
        doc.Reference = "Начальные остатки";
        doc.Comment = Cut($"Начальные остатки по файлу «{fileName}» — движения по {last:dd.MM.yyyy}", 1000);
        return doc;
    }

    // ---------- Разбор файла ----------

    /// <summary>Строки файла → движения. Пустые строки (и строки, где заполнены только служебные столбцы) пропускаются молча.</summary>
    private static (List<Line> Lines, int Rows, int Invalid) Parse(TableImport.Table table, IReadOnlyCollection<string> units, Action<string> error)
    {
        var lines = new List<Line>();
        int rows = 0, invalid = 0;
        var bad = new Dictionary<string, List<int>>(); // причина → строки (одинаковые причины — одной записью)
        var columns = table.Columns;
        var today = FieldClock.Today;
        var data = new[] { "sku", "name", "qty", "in", "out", "kind" };
        for (var r = table.Header + 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            string? Cell(string field)
            {
                if (!columns.TryGetValue(field, out var i) || i >= row.Count)
                {
                    return null;
                }

                var value = row[i].Trim();
                var norm = TableImport.Norm(value);
                return value.Length == 0 || (norm.Length == 0 && value is not ("+" or "-")) || TableImport.Blank.Contains(norm) ? null : value;
            }

            if (data.All(f => Cell(f) is null))
            {
                continue;
            }

            rows++;
            var number = r + 1;
            void Bad(string text)
            {
                invalid++;
                (bad.TryGetValue(text, out var list) ? list : bad[text] = []).Add(number);
            }

            var sku = Cell("sku");
            var name = Cell("name") ?? sku;
            if (name is null)
            {
                Bad("нет артикула и названия");
                continue;
            }

            DateTimeOffset? filled = Cell("filled") is { } filledText && When(filledText) is { } at ? new DateTimeOffset(at, FieldClock.Offset).ToUniversalTime() : null;
            var dateText = Cell("date");
            DateOnly date;
            if (dateText is not null)
            {
                if (When(dateText) is not { } parsed)
                {
                    Bad($"дата «{dateText}» не распознана");
                    continue;
                }

                date = DateOnly.FromDateTime(parsed);
            }
            else if (filled is { } f)
            {
                date = FieldClock.LocalDate(f);
            }
            else
            {
                Bad("нет даты");
                continue;
            }

            if (date > today)
            {
                Bad($"дата {date:dd.MM.yyyy} ещё не наступила");
                continue;
            }

            string? unit = null;
            if (Cell("unit") is { } unitText)
            {
                var norm = TableImport.Norm(unitText);
                unit = Units.GetValueOrDefault(norm) ?? units.FirstOrDefault(u => string.Equals(u, unitText.Trim(), StringComparison.OrdinalIgnoreCase));
                if (unit is null)
                {
                    Bad($"единица «{unitText}» неизвестна");
                    continue;
                }
            }

            var place = Cell("place");
            var price = Cell("price") is { } priceText ? Number(priceText) : null;
            var batch = Cell("batch");
            var reference = Cell("reference");
            var comment = Cell("comment");
            var flows = new List<(Flow Flow, decimal Quantity)>();
            if (columns.ContainsKey("kind") || (!columns.ContainsKey("in") && !columns.ContainsKey("out")))
            {
                var qtyText = Cell("qty");
                if (qtyText is null || Number(qtyText) is not { } quantity)
                {
                    Bad($"количество «{qtyText}» не распознано");
                    continue;
                }

                if (quantity == 0)
                {
                    continue; // ноль ничего не меняет
                }

                Flow? flow = Cell("kind") is { } kindText ? Kind(kindText) : quantity > 0 ? Flow.In : Flow.Out;
                if (flow is null)
                {
                    Bad($"не понятно, приход это или расход («{Cell("kind")}»)");
                    continue;
                }

                if (quantity < 0 && columns.ContainsKey("kind"))
                {
                    Bad($"количество {qtyText} меньше нуля");
                    continue;
                }

                flows.Add((flow.Value, Math.Abs(quantity)));
            }
            else
            {
                foreach (var (field, flow) in new[] { ("in", Flow.In), ("out", Flow.Out) })
                {
                    if (Cell(field) is { } text && Number(text) is { } quantity && quantity > 0)
                    {
                        flows.Add((flow, quantity));
                    }
                }

                if (flows.Count == 0)
                {
                    continue; // ни прихода, ни расхода — ничего не меняет
                }
            }

            foreach (var (flow, quantity) in flows)
            {
                var final = flow == Flow.Out && place is not null && WriteOffStems.Any(TableImport.Norm(place).Contains) ? Flow.WriteOff : flow;
                lines.Add(new Line(number, filled, date, final, place, sku is null ? null : Cut(sku, 64), name, unit, decimal.Round(quantity, 3), price, batch, reference, comment));
            }
        }

        foreach (var (reason, numbers) in bad.OrderBy(b => b.Value[0]))
        {
            error(numbers.Count == 1
                ? $"Строка {numbers[0]}: {reason} — строка пропущена."
                : $"Строки {string.Join(", ", numbers.Take(8))}{(numbers.Count > 8 ? "…" : "")} (всего {numbers.Count}): {reason} — строки пропущены.");
        }

        // Отпечаток строки: дата заполнения, дата, вид, цех, позиция и номер повтора такой же строки в файле (одну позицию бывает дважды за отправку).
        var seen = new Dictionary<string, int>();
        foreach (var line in lines)
        {
            var basis = $"{line.Filled?.ToString("o", CultureInfo.InvariantCulture)}|{line.Date:yyyy-MM-dd}|{line.Flow}|{TableImport.Norm(line.Place)}|{line.ItemKey}";
            var n = seen[basis] = seen.GetValueOrDefault(basis) + 1;
            line.Key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{basis}|{n}")))[..40];
        }

        return (lines, rows, invalid);
    }

    private static Flow? Kind(string text)
    {
        var n = TableImport.Norm(text);
        if (text.Trim() == "+" || n is "п" || new[] { "приход", "кирим", "kirim", "поступ", "возврат", "приемк" }.Any(n.StartsWith))
        {
            return Flow.In;
        }

        if (new[] { "списан", "утилиз" }.Any(n.StartsWith))
        {
            return Flow.WriteOff;
        }

        if (text.Trim() == "-" || n is "р" || new[] { "расход", "чиким", "чиқим", "chiqim", "выдач", "отпуск" }.Any(n.StartsWith))
        {
            return Flow.Out;
        }

        return null;
    }

    private static readonly string[] DateFormats =
    [
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy",
        "dd.MM.yyyy H:mm:ss", "d.M.yyyy H:mm:ss", "dd.MM.yyyy H:mm", "d.M.yyyy H:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd H:mm", "yyyy-MM-ddTHH:mm:ss",
        "dd/MM/yyyy H:mm:ss", "d/M/yyyy H:mm:ss",
    ];

    /// <summary>Дата (и время) из ячейки: «31.07.2025», «04.08.2025 9:49:27», «2025-07-31»; число — дата Excel.</summary>
    private static DateTime? When(string text)
    {
        var value = text.Trim();
        if (DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 20_000 and < 80_000
            ? DateTime.FromOADate(serial)
            : null;
    }

    /// <summary>Число из ячейки: «14 150,00», «2 984,00000», «1,500.5», «17060».</summary>
    private static decimal? Number(string text)
    {
        var value = new string(text.Where(c => !char.IsWhiteSpace(c) && c is not (' ' or ' ' or '\'')).ToArray());
        if (value.Contains(',') && value.Contains('.'))
        {
            value = value.Replace(",", "");
        }

        value = value.Replace(',', '.');
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && Math.Abs(number) <= ProcurementRules.MaxQuantity ? number : null;
    }

    /// <summary>Полдень дня по Ташкенту — время движения, если в строке нет времени заполнения (в базе — UTC).</summary>
    private static DateTimeOffset Noon(DateOnly date) => new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), FieldClock.Offset).ToUniversalTime();

    /// <summary>Название цеха или склада без украшений: «• Отдел снабжения •» → «Отдел снабжения».</summary>
    private static string Clean(string place) => Regex.Replace(place.Trim().Trim('•', '·', '*', '-', '–', '—', '"', '«', '»', ' '), @"\s+", " ");

    private static string? Cut(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];

    private static readonly Dictionary<char, string> Latin = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo", ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k",
        ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts",
        ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya", ['ў'] = "o", ['қ'] = "q", ['ғ'] = "g",
        ['ҳ'] = "h",
    };

    /// <summary>Код нового отдела латиницей: «Цех Кекс» → «tseh-keks».</summary>
    private static string Slug(string name)
    {
        var text = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            text.Append(Latin.TryGetValue(c, out var latin) ? latin : char.IsAsciiLetterOrDigit(c) ? c.ToString() : "-");
        }

        var slug = Regex.Replace(text.ToString(), "-+", "-").Trim('-');
        return slug.Length == 0 ? "otdel" : slug[..Math.Min(slug.Length, 56)];
    }
}

using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

public sealed record ProcurementDepartmentOption(Guid Id, string Code, string Name);

/// <summary>Категория закупок; IsProduction — сырьё и материалы производства (маршрут сырья, когда он включён).</summary>
public sealed record ProcurementCategoryView(Guid Id, string Name, string? Code, int SortOrder, bool IsActive, int Used, bool IsProduction = false);

public sealed record ProcurementUnitView(string Code, string Name);

/// <summary>Для формы заявки: за какие отделы можно подать заявку (и какой по умолчанию), активные категории, единицы измерения.</summary>
public sealed record ProcurementFormData(
    IReadOnlyList<ProcurementDepartmentOption> Departments,
    Guid? DefaultDepartmentId,
    IReadOnlyList<ProcurementCategoryView> Categories,
    IReadOnlyList<ProcurementUnitView> Units);

/// <summary>Категория из формы; IsProduction null — не менять.</summary>
public sealed record ProcurementCategoryInput(string? Name, int? SortOrder, bool IsActive = true, bool? IsProduction = null);

/// <summary>Вложение к загрузке: имя, размер и содержимое (поток читается один раз). Тип файла определяется по расширению.</summary>
public sealed record ProcurementUpload(string FileName, long SizeBytes, Stream Content);

public sealed record ProcurementDownload(string FileName, string ContentType, string ObjectKey);

public sealed partial class ProcurementRequestService
{
    public const long MaxAttachmentBytes = 20L * 1024 * 1024;
    public const int MaxAttachments = 20;

    /// <summary>Разрешённые вложения: документы, таблицы, картинки, архив. Тип для скачивания — по расширению, а не от браузера.</summary>
    internal static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip",
    };

    // ---------- Форма ----------

    public async Task<ProcurementFormData> FormAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireOpen(ctx);
        var departments = await CreatableDepartmentsAsync(ctx, ct);
        Guid? fallback = departments.Count == 1 ? departments[0].Id : null;
        var preferred = ctx.DepartmentId is { } own && departments.Any(d => d.Id == own) ? own : fallback;
        var categories = await db.ProcurementCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ProcurementCategoryView(c.Id, c.Name, c.Code, c.SortOrder, c.IsActive, 0, c.IsProduction))
            .ToListAsync(ct);
        var units = await db.ProcurementUnits.AsNoTracking().OrderBy(u => u.SortOrder).Select(u => new ProcurementUnitView(u.Code, u.Name)).ToListAsync(ct);
        return new ProcurementFormData(departments.Select(d => new ProcurementDepartmentOption(d.Id, d.Code, d.Name)).ToList(), preferred, categories, units);
    }

    /// <summary>
    /// За какие отделы можно подать заявку: свой и те, которыми руководишь; снабжение и директор (видят все заявки) — за любой отдел.
    /// </summary>
    private async Task<List<Department>> CreatableDepartmentsAsync(ProcurementContext ctx, CancellationToken ct)
    {
        var q = db.Departments.AsNoTracking();
        if (!ctx.ReadsAll)
        {
            var ids = ctx.HeadOf.Append(ctx.DepartmentId ?? Guid.Empty).ToArray();
            q = q.Where(d => ids.Contains(d.Id));
        }

        var list = await q.ToListAsync(ct);
        return list.OrderBy(d => Array.IndexOf(WorkTaskService.Order, d.Code) is var i and >= 0 ? i : WorkTaskService.Order.Length).ThenBy(d => d.Name).ToList();
    }

    private async Task<Department> ResolveDepartmentAsync(ProcurementContext ctx, Guid? requested, CancellationToken ct)
    {
        var allowed = await CreatableDepartmentsAsync(ctx, ct);
        if (requested is { } id)
        {
            return allowed.FirstOrDefault(d => d.Id == id)
                   ?? throw new FieldForbiddenException("Заявку можно подать за свой отдел или за отдел, которым вы руководите.");
        }

        if (ctx.DepartmentId is { } own && allowed.FirstOrDefault(d => d.Id == own) is { } mine)
        {
            return mine;
        }

        return allowed.Count switch
        {
            1 => allowed[0],
            0 => throw new FieldValidationException("У вас не указан отдел — попросите администратора указать его в карточке пользователя."),
            _ => throw new FieldValidationException("Выберите отдел заявки."),
        };
    }

    // ---------- Проверка полей и позиций ----------

    private sealed record Refs(
        IReadOnlyDictionary<Guid, ProcurementCategory> Categories,
        IReadOnlyDictionary<string, string> Units,
        IReadOnlyDictionary<Guid, ProcurementItem> Catalog);

    private sealed record ItemDraft(
        Guid? Id,
        string Name,
        Guid? ItemId,
        Guid CategoryId,
        string UnitCode,
        decimal Quantity,
        decimal FromStock,
        string? PurchaseReason,
        decimal? Price,
        string? Comment);

    /// <summary>Справочники для проверки позиций: категории, единицы и товары каталога, на которые ссылаются позиции.</summary>
    private async Task<Refs> RefsAsync(IReadOnlyList<ProcurementItemInput>? input, CancellationToken ct)
    {
        var ids = (input ?? []).Select(i => i.ItemId).OfType<Guid>().Distinct().ToList();
        return new Refs(
            await db.ProcurementCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct),
            await db.ProcurementUnits.AsNoTracking().ToDictionaryAsync(u => u.Code, u => u.Name, ct),
            ids.Count == 0 ? new Dictionary<Guid, ProcurementItem>() : await db.ProcurementItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct));
    }

    /// <summary>
    /// Проверка позиций. Товар из каталога задаёт категорию и единицу; «со склада» — от 0 до количества и только у товара каталога.
    /// Уже выданное не уменьшается и не меняет товар. approvedEdit — правка утверждённой заявки снабжением: товар и часть «со склада»
    /// у существующих позиций не меняются (резерв уже стоит), новые позиции — только к закупке.
    /// </summary>
    private static List<ItemDraft> CheckItems(IReadOnlyList<ProcurementItemInput>? input, Refs refs, IReadOnlyDictionary<Guid, ProcurementRequestItem> existing, bool approvedEdit)
    {
        if (input is null || input.Count == 0)
        {
            throw new FieldValidationException("Добавьте хотя бы одну позицию.");
        }

        if (input.Count > ProcurementRules.MaxItems)
        {
            throw new FieldValidationException($"В одной заявке — до {ProcurementRules.MaxItems} позиций.");
        }

        var result = new List<ItemDraft>(input.Count);
        var seen = new HashSet<Guid>();
        for (var i = 0; i < input.Count; i++)
        {
            var it = input[i];
            var n = i + 1;
            // Позиция этой заявки (id чужой позиции — как новая); повтор id в одном запросе — тоже новая.
            var previous = it.Id is { } pid && seen.Add(pid) && existing.TryGetValue(pid, out var p) ? p : null;
            var locked = previous is not null && (previous.IssuedQty > 0 || (approvedEdit && previous.FromStock > 0));
            var itemId = locked ? previous!.ItemId : it.ItemId;

            ProcurementItem? catalog = null;
            if (itemId is { } cid)
            {
                if (!refs.Catalog.TryGetValue(cid, out catalog))
                {
                    throw new FieldValidationException(locked
                        ? $"Позиция {n}: по ней уже есть резерв или выдача со склада — товар не меняется."
                        : $"Позиция {n}: товар не найден в каталоге склада.");
                }

                if (!catalog.IsActive && previous?.ItemId != cid)
                {
                    throw new FieldValidationException($"Позиция {n}: товар «{catalog.Name}» отключён в каталоге склада.");
                }
            }

            var name = Text(it.Name, 300, $"Позиция {n}: наименование") ?? catalog?.Name ?? throw new FieldValidationException($"Позиция {n}: укажите наименование.");
            Guid categoryId;
            if (catalog is not null)
            {
                categoryId = catalog.CategoryId;
            }
            else
            {
                if (it.CategoryId is not { } chosen || !refs.Categories.TryGetValue(chosen, out var category))
                {
                    throw new FieldValidationException($"Позиция {n}: выберите категорию.");
                }

                if (!category.IsActive && previous?.CategoryId != chosen)
                {
                    throw new FieldValidationException($"Позиция {n}: категория «{category.Name}» отключена — выберите другую.");
                }

                categoryId = chosen;
            }

            var unit = catalog?.UnitCode ?? it.UnitCode?.Trim();
            if (string.IsNullOrEmpty(unit) || !refs.Units.ContainsKey(unit))
            {
                throw new FieldValidationException($"Позиция {n}: выберите единицу измерения.");
            }

            if (ProcurementRules.CheckNumbers(it.Quantity, it.EstimatedPrice) is { } error)
            {
                throw new FieldValidationException($"Позиция {n}: {char.ToLowerInvariant(error[0])}{error[1..]}");
            }

            // Часть «со склада»: только у товара каталога; у правки утверждённой — как была (новые позиции — к закупке).
            var fromStock = catalog is null ? 0m : approvedEdit ? previous?.FromStock ?? 0m : it.FromStock;
            if (fromStock < 0 || fromStock > it.Quantity || decimal.Round(fromStock, 3) != fromStock)
            {
                throw new FieldValidationException($"Позиция {n}: «со склада» — от 0 до количества позиции.");
            }

            if (previous is not null && it.Quantity - fromStock < previous.OrderedQty)
            {
                throw new FieldValidationException(
                    $"Позиция {n}: у поставщиков уже заказано {ProcurementRules.Quantity(previous.OrderedQty)} {previous.UnitCode} — к закупке не меньше заказанного.");
            }

            if (previous is not null && (it.Quantity < previous.IssuedQty || fromStock < previous.IssuedQty))
            {
                throw new FieldValidationException(
                    $"Позиция {n}: уже выдано {ProcurementRules.Quantity(previous.IssuedQty)} {previous.UnitCode} — количество и «со склада» не меньше выданного.");
            }

            result.Add(new ItemDraft(previous?.Id, name, catalog?.Id, categoryId, unit, it.Quantity, fromStock,
                Text(it.PurchaseReason, 500, $"Позиция {n}: обоснование закупки"), it.EstimatedPrice, Text(it.Comment, 500, $"Позиция {n}: комментарий")));
        }

        return result;
    }

    private static (string Title, string? Reason, string? Comment) CheckFields(ProcurementRequestInput input, DateOnly? currentDesired)
    {
        var title = Text(input.Title, 200, "Название") ?? throw new FieldValidationException("Укажите название заявки.");
        var reason = Text(input.Reason, 2000, "Причина закупки");
        if (input.Submit && reason is null)
        {
            throw new FieldValidationException("Укажите причину закупки — без неё заявку не отправить.");
        }

        if (!Enum.IsDefined(input.Priority))
        {
            throw new FieldValidationException("Неизвестный приоритет.");
        }

        // Прошлая дата допустима только если её не меняли (заявка вернулась на доработку спустя время).
        if (input.DesiredDate is { } desired && desired != currentDesired)
        {
            var today = FieldClock.Today;
            if (desired < today)
            {
                throw new FieldValidationException("Желаемая дата получения — не раньше сегодняшнего дня.");
            }

            if (desired > today.AddYears(2))
            {
                throw new FieldValidationException("Желаемая дата получения — не дальше двух лет.");
            }
        }

        return (title, reason, Text(input.Comment, 2000, "Комментарий"));
    }

    private static ProcurementRequestItem NewItem(Guid requestId, int lineNo, ItemDraft d) => new()
    {
        RequestId = requestId,
        LineNo = lineNo,
        Name = d.Name,
        ItemId = d.ItemId,
        CategoryId = d.CategoryId,
        UnitCode = d.UnitCode,
        Quantity = d.Quantity,
        FromStock = d.FromStock,
        PurchaseReason = d.PurchaseReason,
        EstimatedPrice = d.Price,
        Comment = d.Comment,
    };

    /// <summary>Поля и позиции заявки до и после правки: что изменилось — текстом для истории и JSON «было/стало».</summary>
    private sealed record Snapshot(
        string Title,
        string Department,
        string Priority,
        string? DesiredDate,
        string? Reason,
        string? Comment,
        IReadOnlyList<Snapshot.Line> Items)
    {
        public sealed record Line(Guid Id, string Name, string Category, string Unit, decimal Quantity, decimal? Price, string? Comment, decimal FromStock = 0);

        public static Snapshot Of(ProcurementRequest r, string department, IEnumerable<ProcurementRequestItem> items, Refs refs) => new(
            r.Title, department, ProcurementRules.Label(r.Priority), r.DesiredDate?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), r.Reason, r.Comment,
            items.OrderBy(i => i.LineNo).Select(i => new Line(i.Id, i.Name, refs.Categories.TryGetValue(i.CategoryId, out var c) ? c.Name : "",
                i.UnitCode, i.Quantity, i.EstimatedPrice, i.Comment, i.FromStock)).ToList());

        public static (string Text, string Json)? Diff(Snapshot a, Snapshot b)
        {
            var parts = new List<string>();
            var before = new Dictionary<string, object?>();
            var after = new Dictionary<string, object?>();

            void Field(string key, string label, string? x, string? y, bool showValues)
            {
                if (x == y)
                {
                    return;
                }

                before[key] = x;
                after[key] = y;
                parts.Add(showValues ? $"{label}: {x ?? "—"} → {y ?? "—"}" : label);
            }

            Field("title", "название", a.Title, b.Title, false);
            Field("department", "отдел", a.Department, b.Department, true);
            Field("priority", "приоритет", a.Priority, b.Priority, true);
            Field("desiredDate", "желаемая дата", a.DesiredDate, b.DesiredDate, true);
            Field("reason", "причина закупки", a.Reason, b.Reason, false);
            Field("comment", "комментарий", a.Comment, b.Comment, false);

            var oldItems = a.Items.ToDictionary(i => i.Id);
            var newItems = b.Items.ToDictionary(i => i.Id);
            var itemsBefore = new List<Line>();
            var itemsAfter = new List<Line>();
            foreach (var gone in a.Items.Where(i => !newItems.ContainsKey(i.Id)))
            {
                itemsBefore.Add(gone);
                parts.Add($"удалена позиция «{gone.Name}»");
            }

            foreach (var line in b.Items)
            {
                if (!oldItems.TryGetValue(line.Id, out var old))
                {
                    itemsAfter.Add(line);
                    parts.Add($"добавлена позиция «{line.Name}» — {ProcurementRules.Quantity(line.Quantity)} {line.Unit}");
                    continue;
                }

                if (old == line)
                {
                    continue;
                }

                itemsBefore.Add(old);
                itemsAfter.Add(line);
                var changes = new List<string>();
                if (old.Name != line.Name)
                {
                    changes.Add($"наименование «{line.Name}»");
                }

                if (old.Quantity != line.Quantity || old.Unit != line.Unit)
                {
                    changes.Add($"количество {ProcurementRules.Quantity(old.Quantity)} {old.Unit} → {ProcurementRules.Quantity(line.Quantity)} {line.Unit}");
                }

                if (old.Price != line.Price)
                {
                    changes.Add($"цена {Price(old.Price)} → {Price(line.Price)}");
                }

                if (old.Category != line.Category)
                {
                    changes.Add($"категория «{line.Category}»");
                }

                if (old.Comment != line.Comment)
                {
                    changes.Add("комментарий");
                }

                if (old.FromStock != line.FromStock)
                {
                    changes.Add($"со склада {ProcurementRules.Quantity(old.FromStock)} → {ProcurementRules.Quantity(line.FromStock)}");
                }

                parts.Add($"«{old.Name}»: {string.Join(", ", changes)}");
            }

            if (itemsBefore.Count > 0 || itemsAfter.Count > 0)
            {
                before["items"] = itemsBefore;
                after["items"] = itemsAfter;
            }

            if (parts.Count == 0)
            {
                return null;
            }

            var json = JsonSerializer.Serialize(new { before, after }, JsonOptions);
            return ($"Изменено: {string.Join("; ", parts)}", json);
        }

        private static string Price(decimal? p) => p is { } v ? ProcurementRules.Money(v) : "—";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    // ---------- Вложения ----------

    /// <summary>Добавлять вложения: инициатор и снабжение, пока заявка не закрыта.</summary>
    private static bool CanAttach(ProcurementContext ctx, ProcurementRequest r) =>
        !ProcurementRules.Closed.Contains(r.Status) && (ProcurementRules.IsOwner(ctx, r) || ctx.Manages);

    /// <summary>Удалить вложение: кто загрузил (пока заявку можно дополнять) или снабжение.</summary>
    private static bool CanDeleteDocument(ProcurementContext ctx, ProcurementRequest r, ProcurementDocument d) =>
        CanAttach(ctx, r) && (d.UploadedById == ctx.UserId || ctx.Manages);

    /// <summary>
    /// Вложение в хранилище файлов (MinIO, ключ procurement/requests/{заявка}/{вложение}); описание — в базе. Не сохранилось описание —
    /// файл удаляется из хранилища (без «сирот»).
    /// </summary>
    public async Task<Guid> AttachAsync(ProcurementContext ctx, Guid requestId, ProcurementUpload upload, IFileStorage storage, CancellationToken ct)
    {
        RequireOpen(ctx);
        var r = await db.ProcurementRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId, ct) ?? throw new FieldNotFoundException("Заявка не найдена.");
        if (!ProcurementRules.CanView(ctx, r))
        {
            throw new FieldForbiddenException(NotVisible);
        }

        if (!CanAttach(ctx, r))
        {
            throw new FieldForbiddenException(ProcurementRules.Closed.Contains(r.Status)
                ? "Заявка закрыта — вложения не добавляются."
                : "Вложения добавляют инициатор заявки и снабжение.");
        }

        if (upload.SizeBytes <= 0)
        {
            throw new FieldValidationException("Файл пустой.");
        }

        if (upload.SizeBytes > MaxAttachmentBytes)
        {
            throw new FieldValidationException($"Файл больше {MaxAttachmentBytes / 1024 / 1024} МБ.");
        }

        var name = WorkTaskService.SafeFileName(upload.FileName);
        if (!AllowedTypes.TryGetValue(Path.GetExtension(name), out var contentType))
        {
            throw new FieldValidationException("Можно прикрепить PDF, Word, Excel, PowerPoint, картинку, текст или ZIP.");
        }

        if (await db.ProcurementDocuments.CountAsync(d => d.OwnerType == ProcurementRules.RequestDoc && d.OwnerId == requestId, ct) >= MaxAttachments)
        {
            throw new FieldValidationException($"К заявке — до {MaxAttachments} вложений.");
        }

        var doc = new ProcurementDocument
        {
            OwnerType = ProcurementRules.RequestDoc,
            OwnerId = requestId,
            FileName = name,
            ContentType = contentType,
            SizeBytes = upload.SizeBytes,
            ObjectKey = string.Empty,
            UploadedById = ctx.UserId,
        };
        doc.ObjectKey = $"procurement/requests/{requestId:N}/{doc.Id:N}";
        await storage.PutAsync(doc.ObjectKey, upload.Content, upload.SizeBytes, contentType, ct);
        db.ProcurementDocuments.Add(doc);
        Log(r, ctx.UserId, "attachment", null, null, $"Вложение: {name}", null, null);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(doc.ObjectKey, CancellationToken.None);
            throw;
        }

        return doc.Id;
    }

    public async Task<ProcurementDownload> DownloadAsync(ProcurementContext ctx, Guid documentId, CancellationToken ct)
    {
        RequireOpen(ctx);
        var (doc, _) = await RequireDocumentAsync(ctx, documentId, ct);
        return new ProcurementDownload(doc.FileName, AllowedTypes.GetValueOrDefault(Path.GetExtension(doc.FileName), "application/octet-stream"), doc.ObjectKey);
    }

    public async Task DeleteDocumentAsync(ProcurementContext ctx, Guid documentId, IFileStorage storage, CancellationToken ct)
    {
        RequireOpen(ctx);
        var (doc, r) = await RequireDocumentAsync(ctx, documentId, ct);
        if (!CanDeleteDocument(ctx, r, doc))
        {
            throw new FieldForbiddenException("Удалить вложение может тот, кто его загрузил, или снабжение — пока заявка не закрыта.");
        }

        db.ProcurementDocuments.Remove(doc);
        Log(r, ctx.UserId, "attachment", null, null, $"Удалено вложение: {doc.FileName}", null, null);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(doc.ObjectKey, ct);
    }

    private async Task<(ProcurementDocument Doc, ProcurementRequest Request)> RequireDocumentAsync(ProcurementContext ctx, Guid documentId, CancellationToken ct)
    {
        var doc = await db.ProcurementDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.OwnerType == ProcurementRules.RequestDoc, ct)
                  ?? throw new FieldNotFoundException("Файл не найден.");
        var r = await db.ProcurementRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == doc.OwnerId, ct) ?? throw new FieldNotFoundException("Заявка не найдена.");
        if (!ProcurementRules.CanView(ctx, r))
        {
            throw new FieldForbiddenException(NotVisible);
        }

        return (doc, r);
    }

    // ---------- Категории ----------

    /// <summary>Категории: активные — всем, кто открывает раздел; все (с отключёнными и числом позиций) — тем, кто их настраивает.</summary>
    public async Task<List<ProcurementCategoryView>> CategoriesAsync(ProcurementContext ctx, bool all, CancellationToken ct)
    {
        RequireOpen(ctx);
        var withInactive = all && ctx.Configures;
        return await db.ProcurementCategories.AsNoTracking()
            .Where(c => withInactive || c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ProcurementCategoryView(c.Id, c.Name, c.Code, c.SortOrder, c.IsActive, db.ProcurementRequestItems.Count(i => i.CategoryId == c.Id), c.IsProduction))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateCategoryAsync(ProcurementContext ctx, ProcurementCategoryInput input, CancellationToken ct)
    {
        RequireConfigures(ctx);
        var name = await CategoryNameAsync(input.Name, null, ct);
        var order = input.SortOrder ?? ((await db.ProcurementCategories.MaxAsync(c => (int?)c.SortOrder, ct) ?? 0) + 10);
        var category = new ProcurementCategory { Name = name, SortOrder = order, IsActive = input.IsActive, IsProduction = input.IsProduction ?? false };
        db.ProcurementCategories.Add(category);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.category.created", "procurement_category", category.Id.ToString(),
            new { category.Name, category.SortOrder, category.IsActive, category.IsProduction }, ct);
        return category.Id;
    }

    /// <summary>Переименовать, переставить, отключить или включить. Категории не удаляются: по ним остаются старые заявки.</summary>
    public async Task UpdateCategoryAsync(ProcurementContext ctx, Guid id, ProcurementCategoryInput input, CancellationToken ct)
    {
        RequireConfigures(ctx);
        var category = await db.ProcurementCategories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new FieldNotFoundException("Категория не найдена.");
        var before = new { category.Name, category.SortOrder, category.IsActive, category.IsProduction };
        category.Name = await CategoryNameAsync(input.Name, id, ct);
        category.SortOrder = input.SortOrder ?? category.SortOrder;
        category.IsActive = input.IsActive;
        category.IsProduction = input.IsProduction ?? category.IsProduction;
        category.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.category.updated", "procurement_category", category.Id.ToString(),
            new { before, after = new { category.Name, category.SortOrder, category.IsActive, category.IsProduction } }, ct);
    }

    private static void RequireConfigures(ProcurementContext ctx)
    {
        RequireOpen(ctx);
        if (!ctx.Configures)
        {
            throw new FieldForbiddenException("Категории закупок настраивают директор и администратор.");
        }
    }

    private async Task<string> CategoryNameAsync(string? value, Guid? self, CancellationToken ct)
    {
        var name = Text(value, 100, "Название категории");
        if (name is null || name.Length < 2)
        {
            throw new FieldValidationException("Название категории — от 2 символов.");
        }

        var lower = name.ToLower();
        if (await db.ProcurementCategories.AnyAsync(c => c.Id != self && c.Name.ToLower() == lower, ct))
        {
            throw new FieldValidationException($"Категория «{name}» уже есть.");
        }

        return name;
    }
}

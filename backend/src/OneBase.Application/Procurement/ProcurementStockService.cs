using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Строка остатков склада: позиция каталога, на складе, в резерве, доступно, минимум, «ниже минимума».</summary>
public sealed record ProcurementStockRow(
    Guid ItemId,
    string Sku,
    string Name,
    Guid CategoryId,
    string CategoryName,
    string UnitCode,
    decimal OnHand,
    decimal Reserved,
    decimal Available,
    decimal MinQty,
    bool Low,
    bool IsActive,
    string? Barcode,
    string? Description,
    DateTimeOffset? LastMovementAt);

/// <summary>Остатки: поиск (SKU, название, штрихкод), категория, фильтр (low — ниже минимума, reserved — есть резерв, empty — нет на складе, inactive — отключённые).</summary>
public sealed record ProcurementStockSearch(string? Search = null, Guid? CategoryId = null, string? Filter = null, string? Sort = null, bool Desc = false, int Page = 1, int PageSize = 50);

public sealed record ProcurementStockSummary(int Items, int Low, int Empty, int WithReserve, int ToIssueRequests);

/// <summary>Позиция каталога из формы: SKU, название, категория, единица, штрихкод, описание, минимальный остаток, активна.</summary>
public sealed record ProcurementCatalogInput(string? Sku, string? Name, Guid? CategoryId, string? UnitCode, string? Barcode, string? Description, decimal MinQty, bool IsActive = true);

/// <summary>Товар каталога для формы заявки: сколько доступно на складе сейчас.</summary>
public sealed record ProcurementCatalogOption(Guid Id, string Sku, string Name, Guid CategoryId, string CategoryName, string UnitCode, decimal OnHand, decimal Available);

public sealed record ProcurementMovementRow(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid DocumentId,
    string DocumentNumber,
    ProcurementStockDocumentKind DocumentKind,
    Guid ItemId,
    string Sku,
    string ItemName,
    string UnitCode,
    ProcurementMovementKind Kind,
    decimal Quantity,
    decimal BalanceBefore,
    decimal BalanceAfter,
    decimal? UnitCost,
    string? ActorName,
    string? DepartmentName,
    Guid? RequestId,
    string? RequestNumber,
    string? Receiver,
    string? Reference,
    string? Comment,
    Guid? OrderId = null,
    string? OrderNumber = null);

/// <summary>Журнал движений: позиция, вид (Receipt, Issue, WriteOff, Adjustment), даты (Ташкент), поиск по SKU, названию, номеру документа.</summary>
public sealed record ProcurementMovementSearch(Guid? ItemId = null, string? Kind = null, DateOnly? From = null, DateOnly? To = null, string? Search = null, int Page = 1, int PageSize = 50);

public sealed record ProcurementItemCard(ProcurementStockRow Item, IReadOnlyList<ProcurementMovementRow> Movements, IReadOnlyList<ProcurementItemReservation> Reservations);

public sealed record ProcurementItemReservation(Guid RequestId, string RequestNumber, string RequestTitle, string DepartmentName, decimal Quantity, DateTimeOffset CreatedAt);

/// <summary>Строка складского документа: позиция и количество (для инвентаризации — фактический остаток), цена за единицу (приход).</summary>
public sealed record ProcurementStockLineInput(Guid ItemId, decimal Quantity, decimal? UnitCost = null);

/// <summary>
/// Складской документ из формы: строки, комментарий (у списания — причина, обязательна), номер внешнего документа (накладная), отдел и
/// получатель (выдача отделу), ключ повтора формы.
/// </summary>
public sealed record ProcurementStockOperationInput(
    IReadOnlyList<ProcurementStockLineInput>? Lines,
    string? Comment,
    string? Reference = null,
    Guid? DepartmentId = null,
    string? Receiver = null,
    Guid? ClientKey = null);

/// <summary>Заявка к выдаче со склада: активный резерв по позициям.</summary>
public sealed record ProcurementToIssue(
    Guid RequestId,
    string Number,
    string Title,
    ProcurementRequestStatus Status,
    string DepartmentName,
    string InitiatorName,
    DateOnly? DesiredDate,
    DateTimeOffset? SubmittedAt,
    IReadOnlyList<ProcurementToIssueLine> Lines);

public sealed record ProcurementToIssueLine(Guid RequestItemId, Guid ItemId, string Sku, string Name, string UnitCode, decimal Left, decimal OnHand);

/// <summary>
/// Склад снабжения: остатки и каталог, журнал движений, приход, выдача отделу, списание, инвентаризация, заявки к выдаче. Смотрят склад
/// кладовщик, снабжение и директор; проводит операции кладовщик; каталог ведут кладовщик и снабжение. Остаток меняется только документом
/// со строкой журнала (ProcurementStockLedger) — без отрицательного остатка и ниже резерва, при одновременном изменении — заново.
/// </summary>
public sealed class ProcurementStockService(IAppDbContext db, ProcurementStockLedger ledger)
{
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static void RequireStock(ProcurementContext ctx)
    {
        if (!ctx.SeesStock)
        {
            throw new FieldForbiddenException("Склад снабжения видят кладовщик, снабжение и директор.");
        }
    }

    private static void RequireWarehouse(ProcurementContext ctx)
    {
        if (!ctx.Warehouse)
        {
            throw new FieldForbiddenException("Приход, выдачу, списание и инвентаризацию проводит кладовщик.");
        }
    }

    // ---------- Остатки и каталог ----------

    public async Task<PageResult<ProcurementStockRow>> StockAsync(ProcurementContext ctx, ProcurementStockSearch s, CancellationToken ct)
    {
        RequireStock(ctx);
        var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var q = Rows(warehouse);
        q = s.Filter switch
        {
            "low" => q.Where(r => r.IsActive && r.MinQty > 0 && r.Available < r.MinQty),
            "reserved" => q.Where(r => r.Reserved > 0),
            "empty" => q.Where(r => r.IsActive && r.OnHand <= 0),
            "inactive" => q.Where(r => !r.IsActive),
            "all" => q,
            _ => q.Where(r => r.IsActive),
        };
        if (s.CategoryId is { } category)
        {
            q = q.Where(r => r.CategoryId == category);
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(r => r.Sku.ToLower().Contains(text) || r.Name.ToLower().Contains(text) || (r.Barcode != null && r.Barcode.ToLower() == text));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        q = s.Sort switch
        {
            "sku" => s.Desc ? q.OrderByDescending(r => r.Sku) : q.OrderBy(r => r.Sku),
            "onHand" => s.Desc ? q.OrderByDescending(r => r.OnHand) : q.OrderBy(r => r.OnHand),
            "available" => s.Desc ? q.OrderByDescending(r => r.Available) : q.OrderBy(r => r.Available),
            "reserved" => s.Desc ? q.OrderByDescending(r => r.Reserved) : q.OrderBy(r => r.Reserved),
            "category" => s.Desc ? q.OrderByDescending(r => r.CategoryName) : q.OrderBy(r => r.CategoryName).ThenBy(r => r.Name),
            "moved" => s.Desc ? q.OrderByDescending(r => r.LastMovementAt) : q.OrderBy(r => r.LastMovementAt),
            _ => s.Desc ? q.OrderByDescending(r => r.Name) : q.OrderBy(r => r.Name),
        };
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<ProcurementStockRow>(rows.Select(r => r.Row()).ToList(), total, page, pageSize);
    }

    /// <summary>Строка остатков для запроса: класс со свойствами — по ним EF строит фильтры и сортировку в SQL (по record не умеет).</summary>
    private sealed class StockData
    {
        public Guid ItemId { get; init; }
        public string Sku { get; init; } = "";
        public string Name { get; init; } = "";
        public Guid CategoryId { get; init; }
        public string CategoryName { get; init; } = "";
        public string UnitCode { get; init; } = "";
        public decimal OnHand { get; init; }
        public decimal Reserved { get; init; }
        public decimal Available { get; init; }
        public decimal MinQty { get; init; }
        public bool IsActive { get; init; }
        public string? Barcode { get; init; }
        public string? Description { get; init; }
        public DateTimeOffset? LastMovementAt { get; init; }

        public ProcurementStockRow Row() => new(ItemId, Sku, Name, CategoryId, CategoryName, UnitCode, OnHand, Reserved, Available, MinQty,
            MinQty > 0 && Available < MinQty, IsActive, Barcode, Description, LastMovementAt);
    }

    /// <summary>Позиции каталога с остатком на складе (нет строки остатка — нули).</summary>
    private IQueryable<StockData> Rows(Guid warehouse) =>
        from i in db.ProcurementItems.AsNoTracking()
        join b in db.ProcurementStockBalances.AsNoTracking().Where(b => b.WarehouseId == warehouse) on i.Id equals b.ItemId into bs
        from b in bs.DefaultIfEmpty()
        select new StockData
        {
            ItemId = i.Id,
            Sku = i.Sku,
            Name = i.Name,
            CategoryId = i.CategoryId,
            CategoryName = i.Category!.Name,
            UnitCode = i.UnitCode,
            OnHand = b == null ? 0m : b.OnHand,
            Reserved = b == null ? 0m : b.Reserved,
            Available = b == null ? 0m : b.OnHand - b.Reserved,
            MinQty = i.MinQty,
            IsActive = i.IsActive,
            Barcode = i.Barcode,
            Description = i.Description,
            LastMovementAt = db.ProcurementStockMovements.Where(m => m.ItemId == i.Id).Max(m => (DateTimeOffset?)m.CreatedAt),
        };

    public async Task<ProcurementStockSummary> SummaryAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireStock(ctx);
        var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var rows = Rows(warehouse).Where(r => r.IsActive);
        var toIssue = await db.ProcurementStockReservations.Where(x => x.Status == ProcurementReservationStatus.Active).Select(x => x.RequestId).Distinct().CountAsync(ct);
        return new ProcurementStockSummary(
            await rows.CountAsync(ct),
            await rows.CountAsync(r => r.MinQty > 0 && r.Available < r.MinQty, ct),
            await rows.CountAsync(r => r.OnHand <= 0, ct),
            await rows.CountAsync(r => r.Reserved > 0, ct),
            toIssue);
    }

    /// <summary>Поиск товара для формы заявки: активные позиции по SKU, названию, штрихкоду — с доступным остатком. Видят все, кто подаёт заявки.</summary>
    public async Task<List<ProcurementCatalogOption>> CatalogSearchAsync(ProcurementContext ctx, string? search, CancellationToken ct)
    {
        if (!ctx.CanOpen)
        {
            throw new FieldForbiddenException("Раздел «Заявки» вам недоступен — нужно право «Заявки».");
        }

        var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var q = Rows(warehouse).Where(r => r.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLower();
            q = q.Where(r => r.Sku.ToLower().Contains(text) || r.Name.ToLower().Contains(text) || (r.Barcode != null && r.Barcode.ToLower() == text));
        }

        return await q.OrderByDescending(r => r.Available > 0).ThenBy(r => r.Name).Take(12)
            .Select(r => new ProcurementCatalogOption(r.ItemId, r.Sku, r.Name, r.CategoryId, r.CategoryName, r.UnitCode, r.OnHand, r.Available))
            .ToListAsync(ct);
    }

    public async Task<ProcurementItemCard> ItemAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireStock(ctx);
        var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var row = (await Rows(warehouse).FirstOrDefaultAsync(r => r.ItemId == id, ct))?.Row() ?? throw new FieldNotFoundException("Позиция каталога не найдена.");
        var movements = await MovementsAsync(ctx, new ProcurementMovementSearch(id, PageSize: 50), ct);
        var reservations = await db.ProcurementStockReservations.AsNoTracking()
            .Where(x => x.ItemId == id && x.Status == ProcurementReservationStatus.Active)
            .Join(db.ProcurementRequests.AsNoTracking(), x => x.RequestId, r => r.Id, (x, r) => new { x, r.Number, r.Title, Department = r.Department!.Name })
            .OrderBy(x => x.x.CreatedAt)
            .Select(x => new ProcurementItemReservation(x.x.RequestId, x.Number, x.Title, x.Department, x.x.Quantity - x.x.Issued, x.x.CreatedAt))
            .ToListAsync(ct);
        return new ProcurementItemCard(row, movements.Items, reservations);
    }

    /// <summary>Новая позиция каталога (кладовщик или снабжение): SKU уникален; строка остатка на центральном складе — с нулями.</summary>
    public async Task<Guid> CreateItemAsync(ProcurementContext ctx, ProcurementCatalogInput input, CancellationToken ct)
    {
        RequireCatalog(ctx);
        var item = new ProcurementItem { Sku = "", Name = "", UnitCode = "" };
        await ApplyAsync(item, input, ct);
        db.ProcurementItems.Add(item);
        db.ProcurementStockBalances.Add(new ProcurementStockBalance { WarehouseId = await ledger.DefaultWarehouseIdAsync(ct), ItemId = item.Id });
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    /// <summary>Правка позиции каталога; единицу нельзя сменить, если по позиции уже были движения (остаток посчитан в прежней).</summary>
    public async Task UpdateItemAsync(ProcurementContext ctx, Guid id, ProcurementCatalogInput input, CancellationToken ct)
    {
        RequireCatalog(ctx);
        var item = await db.ProcurementItems.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new FieldNotFoundException("Позиция каталога не найдена.");
        var unit = item.UnitCode;
        await ApplyAsync(item, input, ct);
        if (unit != item.UnitCode && await db.ProcurementStockMovements.AnyAsync(m => m.ItemId == id, ct))
        {
            throw new FieldValidationException("Единицу измерения не сменить: по позиции уже есть движения — остаток посчитан в прежней.");
        }

        item.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
    }

    private static void RequireCatalog(ProcurementContext ctx)
    {
        if (!ctx.Warehouse && !ctx.Manages)
        {
            throw new FieldForbiddenException("Каталог склада ведут кладовщик и снабжение.");
        }
    }

    private async Task ApplyAsync(ProcurementItem item, ProcurementCatalogInput input, CancellationToken ct)
    {
        var sku = Text(input.Sku, 64, "Артикул (SKU)") ?? throw new FieldValidationException("Укажите артикул (SKU).");
        var name = Text(input.Name, 300, "Название") ?? throw new FieldValidationException("Укажите название позиции.");
        var lower = sku.ToLower();
        if (await db.ProcurementItems.AnyAsync(i => i.Id != item.Id && i.Sku.ToLower() == lower, ct))
        {
            throw new FieldValidationException($"Артикул «{sku}» уже есть в каталоге.");
        }

        if (input.CategoryId is not { } categoryId || !await db.ProcurementCategories.AnyAsync(c => c.Id == categoryId && (c.IsActive || c.Id == item.CategoryId), ct))
        {
            throw new FieldValidationException("Выберите категорию.");
        }

        var unit = input.UnitCode?.Trim();
        if (string.IsNullOrEmpty(unit) || !await db.ProcurementUnits.AnyAsync(u => u.Code == unit, ct))
        {
            throw new FieldValidationException("Выберите единицу измерения.");
        }

        if (input.MinQty < 0 || input.MinQty > ProcurementRules.MaxQuantity || decimal.Round(input.MinQty, 3) != input.MinQty)
        {
            throw new FieldValidationException("Минимальный остаток — не отрицательный, не больше трёх знаков после запятой.");
        }

        item.Sku = sku;
        item.Name = name;
        item.CategoryId = categoryId;
        item.UnitCode = unit;
        item.Barcode = Text(input.Barcode, 64, "Штрихкод");
        item.Description = Text(input.Description, 1000, "Описание");
        item.MinQty = input.MinQty;
        item.IsActive = input.IsActive;
    }

    // ---------- Журнал движений ----------

    public async Task<PageResult<ProcurementMovementRow>> MovementsAsync(ProcurementContext ctx, ProcurementMovementSearch s, CancellationToken ct)
    {
        RequireStock(ctx);
        var q = db.ProcurementStockMovements.AsNoTracking();
        if (s.ItemId is { } itemId)
        {
            q = q.Where(m => m.ItemId == itemId);
        }

        if (!string.IsNullOrWhiteSpace(s.Kind) && Enum.TryParse<ProcurementMovementKind>(s.Kind, true, out var kind))
        {
            q = q.Where(m => m.Kind == kind);
        }

        if (s.From is { } from)
        {
            var fromUtc = FieldClock.DayStartUtc(from);
            q = q.Where(m => m.CreatedAt >= fromUtc);
        }

        if (s.To is { } to)
        {
            var toUtc = FieldClock.DayStartUtc(to.AddDays(1));
            q = q.Where(m => m.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(m => m.Item!.Sku.ToLower().Contains(text) || m.Item.Name.ToLower().Contains(text) || m.Document!.Number.ToLower().Contains(text));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        var rows = await q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new ProcurementMovementRow(m.Id, m.CreatedAt, m.DocumentId, m.Document!.Number, m.Document.Kind, m.ItemId, m.Item!.Sku, m.Item.Name,
                m.Item.UnitCode, m.Kind, m.Quantity, m.BalanceBefore, m.BalanceAfter, m.UnitCost, m.Document.Actor == null ? null : m.Document.Actor.FullName,
                m.Document.Department == null ? null : m.Document.Department.Name, m.Document.RequestId,
                db.ProcurementRequests.Where(r => r.Id == m.Document.RequestId).Select(r => r.Number).FirstOrDefault(),
                m.Document.Receiver, m.Document.Reference, m.Document.Comment, m.Document.OrderId, m.Document.Order == null ? null : m.Document.Order.Number))
            .ToListAsync(ct);
        return new PageResult<ProcurementMovementRow>(rows, total, page, pageSize);
    }

    // ---------- Операции ----------

    /// <summary>Приход на склад: количество больше нуля, цена за единицу — по желанию; номер накладной — в Reference.</summary>
    public Task<Guid> ReceiptAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) =>
        PostAsync(ctx, ProcurementStockDocumentKind.Receipt, input, ct);

    /// <summary>Списание (брак, порча, утеря): не больше свободного остатка, причина обязательна.</summary>
    public Task<Guid> WriteOffAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) =>
        PostAsync(ctx, ProcurementStockDocumentKind.WriteOff, input, ct);

    /// <summary>Инвентаризация: в строках — фактический остаток; разница с учётом — корректировка. Факт меньше резерва — нельзя (сначала снять резерв).</summary>
    public Task<Guid> InventoryAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) =>
        PostAsync(ctx, ProcurementStockDocumentKind.Inventory, input, ct);

    /// <summary>Выдача отделу без заявки: не больше свободного остатка (резерв заявок не трогается), отдел обязателен.</summary>
    public Task<Guid> IssueAsync(ProcurementContext ctx, ProcurementStockOperationInput input, CancellationToken ct) =>
        PostAsync(ctx, ProcurementStockDocumentKind.Issue, input, ct);

    private Task<Guid> PostAsync(ProcurementContext ctx, ProcurementStockDocumentKind kind, ProcurementStockOperationInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireWarehouse(ctx);
        if (input.ClientKey is { } key)
        {
            var done = await db.ProcurementStockDocuments.AsNoTracking().Where(d => d.ActorId == ctx.UserId && d.ClientKey == key).Select(d => d.Id).FirstOrDefaultAsync(ct);
            if (done != Guid.Empty)
            {
                return done;
            }
        }

        var lines = (input.Lines ?? []).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Добавьте хотя бы одну позицию.");
        }

        if (lines.Count > 200)
        {
            throw new FieldValidationException("В одном документе — до 200 позиций.");
        }

        if (lines.GroupBy(l => l.ItemId).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Позиция повторяется — укажите её одной строкой.");
        }

        var comment = Text(input.Comment, 1000, "Комментарий");
        if (kind == ProcurementStockDocumentKind.WriteOff && comment is null)
        {
            throw new FieldValidationException("Укажите причину списания.");
        }

        Department? department = null;
        if (kind == ProcurementStockDocumentKind.Issue)
        {
            department = input.DepartmentId is { } dep ? await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dep, ct) : null;
            if (department is null)
            {
                throw new FieldValidationException("Выберите отдел-получатель.");
            }
        }

        var ids = lines.Select(l => l.ItemId).ToList();
        var items = await db.ProcurementItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
        var balances = await ledger.BalancesAsync(warehouse, ids, ct);
        var doc = await ledger.DocumentAsync(kind, warehouse, ctx.UserId, ct);
        doc.Comment = comment;
        doc.Reference = Text(input.Reference, 100, "Номер документа");
        doc.DepartmentId = department?.Id;
        doc.Receiver = Text(input.Receiver, 200, "Кто получил");
        doc.ClientKey = input.ClientKey;

        foreach (var line in lines)
        {
            var item = items.GetValueOrDefault(line.ItemId) ?? throw new FieldValidationException("Позиция не найдена в каталоге склада.");
            var label = $"{item.Sku} {item.Name}";
            var inventory = kind == ProcurementStockDocumentKind.Inventory;
            if (line.Quantity < 0 || (!inventory && line.Quantity == 0) || line.Quantity > ProcurementRules.MaxQuantity || decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                throw new FieldValidationException(inventory
                    ? $"«{label}»: фактический остаток — не отрицательный, не больше трёх знаков после запятой."
                    : $"«{label}»: количество — больше нуля, не больше трёх знаков после запятой.");
            }

            if (line.UnitCost is { } cost && (kind != ProcurementStockDocumentKind.Receipt || cost < 0 || cost > ProcurementRules.MaxPrice || decimal.Round(cost, 2) != cost))
            {
                throw new FieldValidationException($"«{label}»: цена за единицу — только у прихода, не отрицательная, до двух знаков.");
            }

            if (!item.IsActive && kind == ProcurementStockDocumentKind.Receipt)
            {
                throw new FieldValidationException($"«{label}»: позиция отключена в каталоге — включите её, чтобы принять на склад.");
            }

            var balance = balances[line.ItemId];
            switch (kind)
            {
                case ProcurementStockDocumentKind.Receipt:
                    ledger.Move(doc, balance, ProcurementMovementKind.Receipt, line.Quantity, label, line.UnitCost);
                    break;
                case ProcurementStockDocumentKind.WriteOff:
                    ledger.Move(doc, balance, ProcurementMovementKind.WriteOff, -line.Quantity, label);
                    break;
                case ProcurementStockDocumentKind.Issue:
                    ledger.Move(doc, balance, ProcurementMovementKind.Issue, -line.Quantity, label);
                    break;
                default:
                    if (line.Quantity < balance.Reserved)
                    {
                        throw new FieldConflictException(
                            $"«{label}»: факт {ProcurementRules.Quantity(line.Quantity)} меньше резерва заявок {ProcurementRules.Quantity(balance.Reserved)} — сначала выдайте или снимите резерв.");
                    }

                    ledger.Move(doc, balance, ProcurementMovementKind.Adjustment, line.Quantity - balance.OnHand, label);
                    break;
            }

        }

        await db.SaveChangesAsync(ct);
        return doc.Id;
    });

    /// <summary>Отделы — получатели выдачи со склада (все отделы компании).</summary>
    public async Task<List<ProcurementDepartmentOption>> DepartmentsAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireStock(ctx);
        var list = await db.Departments.AsNoTracking().Select(d => new ProcurementDepartmentOption(d.Id, d.Code, d.Name)).ToListAsync(ct);
        return list.OrderBy(d => Array.IndexOf(OneBase.Application.Work.WorkTaskService.Order, d.Code) is var i and >= 0 ? i : 99).ThenBy(d => d.Name).ToList();
    }

    // ---------- К выдаче ----------

    /// <summary>Заявки с активным резервом — что выдать со склада: по позициям, сколько осталось в резерве и сколько на складе.</summary>
    public async Task<List<ProcurementToIssue>> ToIssueAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireStock(ctx);
        var rows = await db.ProcurementStockReservations.AsNoTracking()
            .Where(x => x.Status == ProcurementReservationStatus.Active)
            .Join(db.ProcurementRequestItems.AsNoTracking(), x => x.RequestItemId, i => i.Id, (x, i) => new { x, i })
            .Select(v => new
            {
                v.x.RequestId,
                v.x.RequestItemId,
                v.x.ItemId,
                Left = v.x.Quantity - v.x.Issued,
                v.i.Name,
                v.i.UnitCode,
                v.i.LineNo,
                Sku = v.i.Item!.Sku,
                OnHand = db.ProcurementStockBalances.Where(b => b.WarehouseId == v.x.WarehouseId && b.ItemId == v.x.ItemId).Select(b => b.OnHand).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var requestIds = rows.Select(r => r.RequestId).Distinct().ToList();
        var requests = await db.ProcurementRequests.AsNoTracking().Where(r => requestIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Number, r.Title, r.Status, Department = r.Department!.Name, Initiator = r.Initiator!.FullName, r.DesiredDate, r.SubmittedAt })
            .ToListAsync(ct);
        return requests
            .OrderBy(r => r.DesiredDate ?? DateOnly.MaxValue).ThenBy(r => r.SubmittedAt)
            .Select(r => new ProcurementToIssue(r.Id, r.Number, r.Title, r.Status, r.Department, r.Initiator, r.DesiredDate, r.SubmittedAt,
                rows.Where(x => x.RequestId == r.Id).OrderBy(x => x.LineNo)
                    .Select(x => new ProcurementToIssueLine(x.RequestItemId, x.ItemId, x.Sku, x.Name, x.UnitCode, x.Left, x.OnHand)).ToList()))
            .ToList();
    }

    private static string? Text(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= max ? text : throw new FieldValidationException($"{field} — до {max} символов.");
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;
using OneBase.Domain.Work;

namespace OneBase.Application.Production;

/// <summary>Задание из формы: рецептура (действующая версия), цех, склад выдачи, план и дата, мастер, комментарий; ClientKey — ключ повтора; Version — при правке.</summary>
public sealed record ProductionOrderInput(Guid? RecipeId, Guid? DepartmentId, Guid? WarehouseId, decimal PlannedQty, DateOnly? PlannedDate, Guid? ResponsibleId, string? Comment,
    Guid? ClientKey = null, uint? Version = null);

public sealed record ProductionOrderSearch(string? Search = null, string? Status = null, Guid? DepartmentId = null, DateOnly? From = null, DateOnly? To = null, int Page = 1,
    int PageSize = 30);

public sealed record ProductionOrderRow(Guid Id, string Number, Guid RecipeId, string RecipeCode, int RecipeVersion, string RecipeName, string OutputUnit, decimal PlannedQty,
    decimal? OutputQty, DateOnly PlannedDate, ProductionOrderStatus Status, string DepartmentName, string WarehouseName, string? ResponsibleName, bool Overconsumed);

/// <summary>Журнал заданий: страница и счётчики — черновики, выпущены, в работе (по всему журналу).</summary>
public sealed record ProductionOrderList(IReadOnlyList<ProductionOrderRow> Items, int Total, int Page, int PageSize, bool CanCreate, int Drafts, int Released, int InProgress);

/// <summary>
/// Строка задания: норма рецептуры на её выход и потери, потребность на план, выдано, возвращено, расход (выдано − возвращено), осталось выдать,
/// доступно на складе задания; после закрытия — норма на фактический выход, отклонение и пометка перерасхода.
/// </summary>
public sealed record ProductionOrderLineView(Guid Id, int LineNo, Guid ItemId, string Sku, string Name, string UnitCode, bool TracksBatches, decimal Norm, decimal LossPercent,
    decimal Required, decimal Issued, decimal Returned, decimal Consumed, decimal Left, decimal Available, decimal? ActualNorm, decimal? Deviation, decimal? DeviationPercent,
    bool Overconsumed);

/// <summary>Что на руках у цеха по заданию: позиция и партия (у позиции без партий — без партии), выдано, возвращено, можно вернуть.</summary>
public sealed record ProductionOrderHolding(Guid ItemId, string Name, string UnitCode, Guid? BatchId, string? BatchNumber, DateOnly? ExpiresOn, decimal Issued, decimal Returned,
    decimal Left, string? SupplierBatchNo = null);

public sealed record ProductionOrderMove(string ItemName, string UnitCode, string? BatchNumber, decimal Quantity);

/// <summary>Документ выдачи (PIS-) или возврата (PRT-) по заданию.</summary>
public sealed record ProductionOrderDocument(Guid Id, string Number, ProcurementStockDocumentKind Kind, DateTimeOffset At, string? ActorName, string? Comment, string? Receiver,
    IReadOnlyList<ProductionOrderMove> Moves);

/// <summary>Что можно: изменить (черновик), выпустить, выдать и вернуть (кладовщик склада задания), закрыть, отменить, оприходовать выход (кладовщик).</summary>
public sealed record ProductionOrderAbilities(bool Edit, bool Release, bool Issue, bool Return, bool Complete, bool Cancel, bool Receive = false);

/// <summary>
/// Сверка по позиции: план (потребность на план), норма на фактический выпуск, по результатам смен (норма на фактический брак этапов и
/// материалов), факт склада (выдано − возвращено), взвешено в сменах; отклонение факта от расчёта по результатам (а их нет — от нормы на
/// факт). Null — нет данных (не подставляется ноль).
/// </summary>
public sealed record ProductionCheckLine(Guid ItemId, string Name, string UnitCode, decimal Plan, decimal? NormOnFact, decimal? ByResults, decimal Fact, decimal? Weighed,
    decimal? Deviation, decimal? DeviationPercent);

/// <summary>Сверка задания: от какого выхода (смены или закрытие), смен утверждено, неполный ли расчёт по результатам, строки, расчёт по результатам.</summary>
public sealed record ProductionOrderCheck(decimal Output, string OutputSource, int Shifts, IReadOnlyList<string> Missing, IReadOnlyList<ProductionCheckLine> Lines,
    CalcResult? ByResults);

/// <summary>
/// Выход на склад: позиция склада, сколько (в её единице), документ прихода и партия, если уже оприходован; цена партии — материалы задания
/// по цене выданных партий на единицу выхода.
/// </summary>
public sealed record ProductionOrderOutput(Guid ItemId, string ItemName, string UnitCode, decimal? Quantity, string? DocumentNumber, string? BatchNumber, decimal? UnitCost = null);

public sealed record ProductionOrderDetail(
    Guid Id,
    string Number,
    Guid RecipeId,
    string RecipeCode,
    int RecipeVersion,
    string RecipeName,
    string OutputName,
    string OutputUnit,
    decimal RecipeOutput,
    Guid DepartmentId,
    string DepartmentName,
    Guid WarehouseId,
    string WarehouseName,
    decimal PlannedQty,
    DateOnly PlannedDate,
    ProductionOrderStatus Status,
    decimal? OutputQty,
    Guid? ResponsibleId,
    string? ResponsibleName,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReleasedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    uint Version,
    decimal ThresholdPercent,
    IReadOnlyList<ProductionOrderLineView> Lines,
    IReadOnlyList<ProductionOrderHolding> Holdings,
    IReadOnlyList<ProductionOrderDocument> Documents,
    IReadOnlyList<ProcurementEventView> History,
    ProductionOrderAbilities Can,
    CalcResult? Plan = null,
    ProductionOrderCheck? Check = null,
    ProductionOrderOutput? Output = null,
    int ShiftsOpen = 0,
    decimal? ShiftsGoodOutput = null);

/// <summary>Строка выдачи: позиция задания, сколько, партия (не указана — FEFO из годных).</summary>
public sealed record ProductionIssueLine(Guid ItemId, decimal Quantity, Guid? BatchId = null);

/// <summary>Выдача по заданию: строки, причина (обязательна, если выдают больше потребности), кто получил, ключ повтора формы.</summary>
public sealed record ProductionIssueInput(IReadOnlyList<ProductionIssueLine>? Lines, string? Reason = null, string? Receiver = null, Guid? ClientKey = null);

/// <summary>Строка возврата: позиция, сколько, партия (у позиции с партиями — обязательно: возврат — в ту же партию).</summary>
public sealed record ProductionReturnLine(Guid ItemId, decimal Quantity, Guid? BatchId = null);

public sealed record ProductionReturnInput(IReadOnlyList<ProductionReturnLine>? Lines, string? Comment = null, Guid? ClientKey = null);

public sealed record ProductionCompleteInput(decimal OutputQty, string? Comment = null, uint? Version = null);

public sealed record ProductionMoveResult(Guid DocumentId, string Number);

/// <summary>Выбор для формы задания: действующие рецептуры, цеха (отделы), склады, мастера.</summary>
public sealed record ProductionOrderForm(IReadOnlyList<ProductionRecipeOption> Recipes, IReadOnlyList<ProductionNamed> Departments, IReadOnlyList<ProductionNamed> Warehouses,
    IReadOnlyList<ProductionNamed> People);

public sealed record ProductionNamed(Guid Id, string Name);

/// <summary>
/// Задания на выпуск (D8 — расход по фактической выдаче): производство создаёт задание по действующей версии рецептуры и выпускает его —
/// потребность по нормам фиксируется, о нехватке годного сырья на складе задания узнают мастер и снабжение. Кладовщик склада задания
/// выдаёт сырьё партиями (только допущенные и не просроченные, подсказка FEFO; больше потребности — с причиной) и принимает возврат в те
/// же партии (не больше выданного). Закрытие — фактический выход: норма на факт выхода, отклонение по каждой позиции; перерасход больше
/// порога из настроек снабжения — пометка и уведомление руководителю производства. Готовая продукция на склад OneBase не приходуется.
/// Отменить можно, только если у цеха на руках ничего нет. Резерва сырья под задание нет (D14): доступность проверяется при выдаче.
/// Потребность и норма на факт — расчётный механизм по цепочке версии (ProductionCalc); плановый расчёт хранится снимком в задании.
/// </summary>
public sealed class ProductionOrderService(IAppDbContext db, IProcurementNumbers numbers, ProcurementStockLedger ledger, UserNotifier notifier, ProductionShiftService shifts)
{
    public const string ProductionDepartment = "production";

    private static void RequireView(ProcurementContext ctx)
    {
        if (!ctx.SeesProduction)
        {
            throw new FieldForbiddenException("Задания на выпуск видят производство, технолог, кладовщик, снабжение и директор.");
        }
    }

    private static void RequireManage(ProcurementContext ctx)
    {
        if (!ctx.Produces)
        {
            throw new FieldForbiddenException("Задания на выпуск ведёт производство — нужно право «Производство: задания на выпуск».");
        }
    }

    public async Task<ProductionOrderList> SearchAsync(ProcurementContext ctx, ProductionOrderSearch s, CancellationToken ct)
    {
        RequireView(ctx);
        var q = db.ProductionOrders.AsNoTracking();
        if (s.Search?.Trim().ToLower() is { Length: > 0 } text)
        {
            q = q.Where(o => o.Number.ToLower().Contains(text) || o.Recipe!.Name.ToLower().Contains(text) || o.Recipe.Code.ToLower().Contains(text));
        }

        if (Enum.TryParse<ProductionOrderStatus>(s.Status, true, out var status))
        {
            q = q.Where(o => o.Status == status);
        }
        else if (s.Status == "open")
        {
            q = q.Where(o => o.Status == ProductionOrderStatus.Draft || o.Status == ProductionOrderStatus.Released || o.Status == ProductionOrderStatus.InProgress);
        }

        if (s.DepartmentId is { } department)
        {
            q = q.Where(o => o.DepartmentId == department);
        }

        if (s.From is { } from)
        {
            q = q.Where(o => o.PlannedDate >= from);
        }

        if (s.To is { } to)
        {
            q = q.Where(o => o.PlannedDate <= to);
        }

        var total = await q.CountAsync(ct);
        var size = Math.Clamp(s.PageSize, 1, 100);
        var page = Math.Max(1, s.Page);
        var rows = await q.OrderByDescending(o => o.PlannedDate).ThenByDescending(o => o.Number).Skip((page - 1) * size).Take(size)
            .Select(o => new ProductionOrderRow(o.Id, o.Number, o.RecipeId, o.Recipe!.Code, o.Recipe.VersionNo, o.Recipe.Name, o.Recipe.OutputUnit, o.PlannedQty, o.OutputQty,
                o.PlannedDate, o.Status, o.Department!.Name, o.Warehouse!.Name, o.Responsible == null ? null : o.Responsible.FullName, o.Lines.Any(l => l.Overconsumed)))
            .ToListAsync(ct);
        var counts = await db.ProductionOrders.AsNoTracking().GroupBy(o => o.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return new ProductionOrderList(rows, total, page, size, ctx.Produces, counts.GetValueOrDefault(ProductionOrderStatus.Draft),
            counts.GetValueOrDefault(ProductionOrderStatus.Released), counts.GetValueOrDefault(ProductionOrderStatus.InProgress));
    }

    public async Task<ProductionOrderForm> FormAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireView(ctx);
        var recipes = await db.ProductionRecipes.AsNoTracking().Where(r => r.Status == ProductionRecipeStatus.Active).OrderBy(r => r.Name)
            .Select(r => new ProductionRecipeOption(r.Id, r.Code, r.VersionNo, r.Name, r.OutputName, r.OutputUnit, r.OutputQuantity)).ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Code != ProductionDepartment).ThenBy(d => d.Name).Select(d => new ProductionNamed(d.Id, d.Name))
            .ToListAsync(ct);
        var warehouses = await db.ProcurementWarehouses.AsNoTracking().Where(w => w.IsActive).OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name)
            .Select(w => new ProductionNamed(w.Id, w.Name)).ToListAsync(ct);
        var people = await db.Users.AsNoTracking().Where(u => u.IsActive && u.Roles.Any(r => r.Role.Name != SystemRoles.Admin
                && r.Role.Permissions.Any(p => p.Code == Permissions.ProductionManage)))
            .OrderBy(u => u.FullName).Select(u => new ProductionNamed(u.Id, u.FullName)).ToListAsync(ct);
        return new ProductionOrderForm(recipes, departments, warehouses, people);
    }

    public async Task<ProductionOrderDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireView(ctx);
        var o = await db.ProductionOrders.AsNoTracking().Include(x => x.Recipe).Include(x => x.Department).Include(x => x.Warehouse).Include(x => x.Responsible)
                    .Include(x => x.Lines).ThenInclude(l => l.Item).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Задание на выпуск не найдено.");
        var settings = await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var threshold = settings?.OverconsumptionPercent ?? ProcurementSettings.DefaultOverconsumptionPercent;
        var available = await ProductionStock.AvailableAsync(db, o.Lines.Select(l => l.ItemId), o.WarehouseId, ledger.Today, ct);
        var recipe = (await ProductionRecipeService.LoadAsync(db, r => r.Id == o.RecipeId, false, ct, q => q.Include(r => r.OutputItem))).Single();
        var results = await shifts.OrderResultsAsync(o, recipe, ct);
        var used = ResultItems(ProductionRecipeCalc.From(recipe), results?.Calculation);
        var lines = o.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var consumed = l.IssuedQty - l.ReturnedQty;
            // После закрытия: отклонение — к расходу по результатам смен (как в сверке), без смен — к норме на фактический выход.
            (decimal Quantity, decimal? Percent)? dev = l.ActualNormQty is { } norm
                ? ProductionRules.Deviation(consumed, used is not null && used.TryGetValue(l.ItemId, out var u) ? u : norm)
                : null;
            return new ProductionOrderLineView(l.Id, l.LineNo, l.ItemId, l.Item!.Sku, l.Item.Name, l.Item.UnitCode, l.Item.TracksBatches, l.NormQty, l.LossPercent, l.RequiredQty,
                l.IssuedQty, l.ReturnedQty, consumed, ProductionRules.LeftToIssue(l.RequiredQty, l.IssuedQty, l.ReturnedQty), available.GetValueOrDefault(l.ItemId), l.ActualNormQty,
                dev?.Quantity, dev?.Percent, l.Overconsumed);
        }).ToList();
        var moves = await db.ProcurementStockMovements.AsNoTracking().Where(m => m.Document!.ProductionOrderId == id)
            .Select(m => new
            {
                m.DocumentId, m.Document!.Number, m.Document.Kind, At = m.Document.PostedAt ?? m.Document.CreatedAt, Actor = m.Document.Actor == null ? null : m.Document.Actor.FullName,
                m.Document.Comment, m.Document.Receiver, m.ItemId, ItemName = m.Item!.Name, m.Item.UnitCode, m.BatchId, BatchNumber = m.Batch == null ? null : m.Batch.Number,
                ExpiresOn = m.Batch == null ? null : m.Batch.ExpiresOn, SupplierBatchNo = m.Batch == null ? null : m.Batch.SupplierBatchNo, m.Quantity, m.UnitCost,
            })
            .ToListAsync(ct);
        var documents = moves.GroupBy(m => m.DocumentId).Select(g =>
        {
            var f = g.First();
            return new ProductionOrderDocument(g.Key, f.Number, f.Kind, f.At, f.Actor, f.Comment, f.Receiver,
                g.Select(m => new ProductionOrderMove(m.ItemName, m.UnitCode, m.BatchNumber, Math.Abs(m.Quantity))).ToList());
        }).OrderByDescending(d => d.At).ToList();
        var lineItems = o.Lines.Select(l => l.ItemId).ToHashSet();
        var holdings = moves.Where(m => lineItems.Contains(m.ItemId)).GroupBy(m => (m.ItemId, m.BatchId)).Select(g =>
        {
            var f = g.First();
            var issued = -g.Where(m => m.Kind == ProcurementStockDocumentKind.ProductionIssue).Sum(m => m.Quantity);
            var returned = g.Where(m => m.Kind == ProcurementStockDocumentKind.ProductionReturn).Sum(m => m.Quantity);
            return new ProductionOrderHolding(f.ItemId, f.ItemName, f.UnitCode, f.BatchId, f.BatchNumber, f.ExpiresOn, issued, returned, issued - returned, f.SupplierBatchNo);
        }).OrderBy(h => h.Name).ThenBy(h => h.ExpiresOn).ToList();
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == ProductionRules.OrderDoc && e.DocId == id).OrderBy(e => e.CreatedAt)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var net = o.Lines.Sum(l => l.IssuedQty - l.ReturnedQty);
        var plan = o.PlanSnapshot is { } json ? JsonSerializer.Deserialize<CalcResult>(json, SnapshotJson) : null;
        var shiftsOpen = await db.ProductionShifts.CountAsync(s => s.OrderId == id && (s.Status == ProductionShiftStatus.Draft || s.Status == ProductionShiftStatus.Closed), ct);
        var check = Check(o, recipe, results, lines);
        var outputMove = moves.FirstOrDefault(m => m.Kind == ProcurementStockDocumentKind.ProductionOutput);
        var output = recipe.OutputItem is { } outItem
            ? new ProductionOrderOutput(outItem.Id, outItem.Name, outItem.UnitCode,
                o.OutputQty is { } q ? ProductionCalc.Convert(q, recipe.OutputUnit, outItem.UnitCode) : null, outputMove?.Number, outputMove?.BatchNumber,
                outputMove?.UnitCost)
            : null;
        var keeper = ctx.Posts(o.WarehouseId) && o.Warehouse!.IsActive;
        var can = new ProductionOrderAbilities(
            ctx.Produces && ProductionRules.Editable(o.Status),
            ctx.Produces && o.Status == ProductionOrderStatus.Draft,
            keeper && ProductionRules.Issuable(o.Status),
            keeper && ProductionRules.Issuable(o.Status) && holdings.Any(h => h.Left > 0),
            ctx.Produces && ProductionRules.Issuable(o.Status),
            ctx.Produces && ProductionRules.Cancellable(o.Status, net),
            keeper && o.Status == ProductionOrderStatus.Completed && output is { DocumentNumber: null, Quantity: > 0 });
        return new ProductionOrderDetail(o.Id, o.Number, o.RecipeId, o.Recipe!.Code, o.Recipe.VersionNo, o.Recipe.Name, o.Recipe.OutputName, o.Recipe.OutputUnit,
            o.Recipe.OutputQuantity, o.DepartmentId, o.Department!.Name, o.WarehouseId, o.Warehouse!.Name, o.PlannedQty, o.PlannedDate, o.Status, o.OutputQty, o.ResponsibleId,
            o.Responsible?.FullName, o.Comment, o.CreatedAt, o.ReleasedAt, o.CompletedAt, o.CancelledAt, o.Version, threshold, lines, holdings, documents, history, can, plan, check,
            output, shiftsOpen, results?.GoodOutput);
    }

    /// <summary>Новое задание — черновик по действующей версии рецептуры; строки — потребность по расчёту цепочки на план (снимок расчёта — в задании).</summary>
    public async Task<Guid> CreateAsync(ProcurementContext ctx, ProductionOrderInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        if (input.ClientKey is { } key
            && await db.ProductionOrders.AsNoTracking().Where(o => o.CreatedById == ctx.UserId && o.ClientKey == key).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct) is { } done)
        {
            return done;
        }

        var recipe = (await ProductionRecipeService.LoadAsync(db, r => r.Id == input.RecipeId, false, ct)).FirstOrDefault()
                     ?? throw new FieldValidationException("Выберите рецептуру.");
        if (recipe.Status != ProductionRecipeStatus.Active)
        {
            throw new FieldConflictException($"Рецептура {recipe.Code} (версия {recipe.VersionNo}) не действующая — задание создают по действующей версии.");
        }

        var order = new ProductionOrder
        {
            Number = await numbers.NextAsync(ProductionRules.OrderPrefix, ct), RecipeId = recipe.Id, CreatedById = ctx.UserId, ClientKey = input.ClientKey,
        };
        await ApplyAsync(order, input, ct);
        foreach (var line in Plan(order, recipe))
        {
            order.Lines.Add(new ProductionOrderLine
            {
                OrderId = order.Id, LineNo = line.LineNo, ItemId = line.ItemId, NormQty = line.Norm, LossPercent = line.LossPercent, RequiredQty = line.Required,
            });
        }

        db.ProductionOrders.Add(order);
        Log(ctx, order.Id, "created", null, ProductionOrderStatus.Draft,
            $"Создано задание {order.Number}: {recipe.Name} (версия {recipe.VersionNo}) — {ProcurementRules.Quantity(order.PlannedQty)} {recipe.OutputUnit} на {order.PlannedDate:dd.MM.yyyy}");
        await SaveAsync(ct);
        return order.Id;
    }

    /// <summary>Правка черновика: цех, склад, план (потребность пересчитывается), дата, мастер, комментарий.</summary>
    public async Task UpdateAsync(ProcurementContext ctx, Guid id, ProductionOrderInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var order = await db.ProductionOrders.Include(o => o.Lines).Include(o => o.Recipe).FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw new FieldNotFoundException("Задание на выпуск не найдено.");
        if (!ProductionRules.Editable(order.Status))
        {
            throw new FieldConflictException("Выпущенное задание не правят — отмените его и создайте новое.");
        }

        if (input.Version is { } version && version != order.Version)
        {
            throw new FieldConflictException("Задание уже изменили в другом окне — обновите страницу.");
        }

        await ApplyAsync(order, input, ct);
        var recipe = (await ProductionRecipeService.LoadAsync(db, r => r.Id == order.RecipeId, false, ct)).Single();
        var required = Plan(order, recipe).ToDictionary(l => l.ItemId, l => l.Required);
        foreach (var line in order.Lines)
        {
            line.RequiredQty = required.GetValueOrDefault(line.ItemId);
        }

        order.UpdatedAt = DateTimeOffset.UtcNow;
        Log(ctx, order.Id, "updated", null, null,
            $"Изменено: {ProcurementRules.Quantity(order.PlannedQty)} {order.Recipe!.OutputUnit} на {order.PlannedDate:dd.MM.yyyy}");
        await SaveAsync(ct);
    }

    /// <summary>Выпустить: потребность фиксируется; не хватает годного сырья на складе задания — мастеру и снабжению уведомление (выпуск не останавливается).</summary>
    public async Task ReleaseAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireManage(ctx);
        var order = await db.ProductionOrders.Include(o => o.Lines).ThenInclude(l => l.Item).Include(o => o.Recipe).Include(o => o.Warehouse)
                        .FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw new FieldNotFoundException("Задание на выпуск не найдено.");
        if (order.Status != ProductionOrderStatus.Draft)
        {
            throw new FieldConflictException("Задание уже выпущено.");
        }

        if (!order.Warehouse!.IsActive)
        {
            throw new FieldConflictException($"Склад «{order.Warehouse.Name}» отключён — выберите другой склад.");
        }

        order.Status = ProductionOrderStatus.Released;
        order.ReleasedAt = DateTimeOffset.UtcNow;
        var available = await ProductionStock.AvailableAsync(db, order.Lines.Select(l => l.ItemId), order.WarehouseId, ledger.Today, ct);
        var short_ = order.Lines.Where(l => l.RequiredQty > available.GetValueOrDefault(l.ItemId)).OrderBy(l => l.LineNo)
            .Select(l => $"{l.Item!.Name} — нужно {ProcurementRules.Quantity(l.RequiredQty)} {l.Item.UnitCode}, годного {ProcurementRules.Quantity(available.GetValueOrDefault(l.ItemId))}")
            .ToList();
        Log(ctx, order.Id, "released", ProductionOrderStatus.Draft, ProductionOrderStatus.Released,
            short_.Count == 0 ? "Задание выпущено: сырья на складе хватает" : $"Задание выпущено; не хватает: {string.Join("; ", short_)}");
        if (short_.Count > 0)
        {
            var people = (await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementManage, ct)).Append(order.ResponsibleId ?? ctx.UserId).Distinct();
            foreach (var user in people)
            {
                notifier.Notify(user, UserNotificationKind.ProcurementStatus, $"Не хватает сырья под задание {order.Number}",
                    string.Join("; ", short_.Take(5)) + (short_.Count > 5 ? $" и ещё {short_.Count - 5}" : ""), $"/production/orders/{order.Id}");
            }
        }

        await SaveAsync(ct);
    }

    /// <summary>
    /// Выдача по заданию (кладовщик склада задания): позиции задания, у позиции с партиями — только годные партии (указанная или FEFO), без
    /// партий — из свободного остатка. Больше, чем осталось выдать до потребности, — только с причиной. Документ PIS-; первая выдача — «в работе».
    /// </summary>
    public Task<ProductionMoveResult> IssueAsync(ProcurementContext ctx, Guid id, ProductionIssueInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        if (await DoneAsync(ctx, input.ClientKey, ct) is { } done)
        {
            return done;
        }

        var (order, warehouse) = await LoadForMoveAsync(ctx, id, ct);
        var lines = Lines(input.Lines?.Select(l => (l.ItemId, l.Quantity, l.BatchId)));
        var reason = Clean(input.Reason, 1000);
        // Больше, чем осталось выдать до потребности (строки одной позиции складываются), — только с причиной.
        var over = new List<string>();
        foreach (var group in lines.GroupBy(l => l.ItemId))
        {
            var line = order.Lines.FirstOrDefault(l => l.ItemId == group.Key) ?? throw new FieldValidationException("Позиции нет в задании — выдают только сырьё задания.");
            var extra = group.Sum(l => l.Qty) - ProductionRules.LeftToIssue(line.RequiredQty, line.IssuedQty, line.ReturnedQty);
            if (extra > 0)
            {
                over.Add($"{line.Item!.Name}: сверх потребности {ProcurementRules.Quantity(extra)} {line.Item.UnitCode}");
            }
        }

        if (over.Count > 0 && reason is null)
        {
            throw new FieldValidationException($"Выдаёте больше потребности ({string.Join("; ", over)}) — укажите причину.");
        }

        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.ProductionIssue, warehouse.Id, ctx.UserId, ct);
        doc.ProductionOrderId = order.Id;
        doc.ClientKey = input.ClientKey;
        doc.PostedAt = DateTimeOffset.UtcNow;
        doc.DepartmentId = order.DepartmentId;
        doc.Receiver = Clean(input.Receiver, 200);
        doc.Comment = reason ?? $"Выдача по заданию {order.Number}";
        var balances = await ledger.BalancesAsync(warehouse.Id, lines.Select(l => l.ItemId), ct);
        var issued = new List<string>();
        foreach (var (itemId, qty, batchId) in lines)
        {
            var line = order.Lines.First(l => l.ItemId == itemId);
            var label = line.Item!.Name;
            var balance = balances[itemId];
            if (ledger.Tracks(itemId))
            {
                foreach (var (batch, take) in await ledger.PickAsync(balance, warehouse.Name, label, qty, batchId, usableOnly: true, keepReserve: true, ct))
                {
                    ledger.MoveBatch(doc, balance, batch, ProcurementMovementKind.ProductionIssue, -take, label, batch.Batch!.UnitCost);
                    issued.Add($"{label} {ProcurementRules.Quantity(take)} {line.Item.UnitCode} ({batch.Batch.Number})");
                }
            }
            else
            {
                if (batchId is not null)
                {
                    throw new FieldValidationException($"«{label}» ведётся без партий — партию не указывают.");
                }

                ledger.Move(doc, balance, ProcurementMovementKind.ProductionIssue, -qty, label);
                issued.Add($"{label} {ProcurementRules.Quantity(qty)} {line.Item.UnitCode}");
            }

            line.IssuedQty += qty;
        }

        var from = order.Status;
        order.Status = ProductionOrderStatus.InProgress;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        Log(ctx, order.Id, "issued", from, order.Status == from ? null : order.Status, $"Выдано в производство ({doc.Number}): {string.Join(", ", issued)}",
            over.Count > 0 ? $"Сверх потребности — {reason}" : null);
        await db.SaveChangesAsync(ct);
        return new ProductionMoveResult(doc.Id, doc.Number);
    });

    /// <summary>Возврат из производства (кладовщик склада задания): в ту же партию, не больше выданного по заданию этой партии (без партий — позиции). Документ PRT-.</summary>
    public Task<ProductionMoveResult> ReturnAsync(ProcurementContext ctx, Guid id, ProductionReturnInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        if (await DoneAsync(ctx, input.ClientKey, ct) is { } done)
        {
            return done;
        }

        var (order, warehouse) = await LoadForMoveAsync(ctx, id, ct);
        var lines = Lines(input.Lines?.Select(l => (l.ItemId, l.Quantity, l.BatchId)));
        var held = await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => m.Document!.ProductionOrderId == order.Id && m.WarehouseId == warehouse.Id)
            .GroupBy(m => new { m.ItemId, m.BatchId }).Select(g => new { g.Key.ItemId, g.Key.BatchId, Net = -g.Sum(m => m.Quantity) })
            .ToListAsync(ct);
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.ProductionReturn, warehouse.Id, ctx.UserId, ct);
        doc.ProductionOrderId = order.Id;
        doc.ClientKey = input.ClientKey;
        doc.PostedAt = DateTimeOffset.UtcNow;
        doc.DepartmentId = order.DepartmentId;
        doc.Comment = Clean(input.Comment, 1000) ?? $"Возврат по заданию {order.Number}";
        var balances = await ledger.BalancesAsync(warehouse.Id, lines.Select(l => l.ItemId), ct);
        var returned = new List<string>();
        foreach (var group in lines.GroupBy(l => (l.ItemId, l.BatchId)))
        {
            var (itemId, batchId) = group.Key;
            var qty = group.Sum(l => l.Qty);
            var line = order.Lines.FirstOrDefault(l => l.ItemId == itemId) ?? throw new FieldValidationException("Позиции нет в задании.");
            var label = line.Item!.Name;
            var tracked = ledger.Tracks(itemId);
            if (tracked && batchId is null)
            {
                throw new FieldValidationException($"«{label}»: укажите партию — возврат идёт в ту же партию, из которой выдали.");
            }

            var onHand = held.FirstOrDefault(h => h.ItemId == itemId && h.BatchId == (tracked ? batchId : null))?.Net ?? 0;
            if (qty > onHand)
            {
                throw new FieldConflictException(onHand <= 0
                    ? $"«{label}»: {(tracked ? "из этой партии " : "")}по заданию не выдавали — возвращать нечего."
                    : $"«{label}»: по заданию на руках {ProcurementRules.Quantity(onHand)} {line.Item.UnitCode} — больше не вернуть.");
            }

            var balance = balances[itemId];
            if (tracked)
            {
                var batch = await db.ProcurementBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct) ?? throw new FieldNotFoundException("Партия не найдена.");
                ledger.MoveBatch(doc, balance, await ledger.BatchBalanceAsync(warehouse.Id, batch, ct), ProcurementMovementKind.ProductionReturn, qty, label, batch.UnitCost);
                returned.Add($"{label} {ProcurementRules.Quantity(qty)} {line.Item.UnitCode} ({batch.Number})");
            }
            else
            {
                ledger.Move(doc, balance, ProcurementMovementKind.ProductionReturn, qty, label);
                returned.Add($"{label} {ProcurementRules.Quantity(qty)} {line.Item.UnitCode}");
            }

            line.ReturnedQty += qty;
        }

        order.UpdatedAt = DateTimeOffset.UtcNow;
        Log(ctx, order.Id, "returned", null, null, $"Возврат из производства ({doc.Number}): {string.Join(", ", returned)}");
        await db.SaveChangesAsync(ct);
        return new ProductionMoveResult(doc.Id, doc.Number);
    });

    /// <summary>
    /// Закрыть задание: фактический выход, норма на факт по каждой позиции; перерасход больше порога (к расходу по результатам смен, без смен — к
    /// норме) — пометка и уведомление.
    /// </summary>
    public async Task CompleteAsync(ProcurementContext ctx, Guid id, ProductionCompleteInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var order = await db.ProductionOrders.Include(o => o.Lines).ThenInclude(l => l.Item).Include(o => o.Recipe).FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw new FieldNotFoundException("Задание на выпуск не найдено.");
        if (!ProductionRules.Issuable(order.Status))
        {
            throw new FieldConflictException(order.Status == ProductionOrderStatus.Draft ? "Сначала выпустите задание." : "Задание уже закрыто или отменено.");
        }

        if (input.Version is { } version && version != order.Version)
        {
            throw new FieldConflictException("Задание уже изменили в другом окне — обновите страницу.");
        }

        if (input.OutputQty < 0 || input.OutputQty > ProductionRules.MaxQuantity || decimal.Round(input.OutputQty, 3) != input.OutputQty)
        {
            throw new FieldValidationException("Фактический выход — не меньше нуля, не больше трёх знаков после запятой.");
        }

        var threshold = (await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct))?.OverconsumptionPercent ?? ProcurementSettings.DefaultOverconsumptionPercent;
        // Норма на фактический выход — тот же расчёт цепочки закреплённой версии (утверждённая версия не меняется).
        var recipe = (await ProductionRecipeService.LoadAsync(db, r => r.Id == order.RecipeId, false, ct)).Single();
        var normOnOutput = Calculate(recipe, input.OutputQty).Items.ToDictionary(i => i.ItemId, i => i.Quantity);
        // Перерасход — к расходу по результатам утверждённых смен (фактические замесы и брак), без смен — к норме на фактический выход.
        var used = ResultItems(ProductionRecipeCalc.From(recipe), (await shifts.OrderResultsAsync(order, recipe, ct))?.Calculation);
        var over = new List<string>();
        foreach (var line in order.Lines.OrderBy(l => l.LineNo))
        {
            line.ActualNormQty = normOnOutput.GetValueOrDefault(line.ItemId);
            var consumed = line.IssuedQty - line.ReturnedQty;
            var basis = used is not null && used.TryGetValue(line.ItemId, out var u) ? u : line.ActualNormQty.Value;
            line.Overconsumed = ProductionRules.Overconsumed(consumed, basis, threshold);
            if (line.Overconsumed)
            {
                var (dq, dp) = ProductionRules.Deviation(consumed, basis);
                over.Add($"{line.Item!.Name}: +{ProcurementRules.Quantity(dq)} {line.Item.UnitCode}{(dp is { } p ? $" ({ProcurementRules.Quantity(p)} %)" : "")}");
            }
        }

        var from = order.Status;
        order.Status = ProductionOrderStatus.Completed;
        order.OutputQty = input.OutputQty;
        order.CompletedAt = DateTimeOffset.UtcNow;
        order.UpdatedAt = order.CompletedAt;
        Log(ctx, order.Id, "completed", from, ProductionOrderStatus.Completed,
            $"Задание закрыто: выход {ProcurementRules.Quantity(input.OutputQty)} {order.Recipe!.OutputUnit} из {ProcurementRules.Quantity(order.PlannedQty)}"
            + (over.Count > 0 ? $"; перерасход больше {ProcurementRules.Quantity(threshold)} %: {string.Join("; ", over)}" : ""), Clean(input.Comment, 1000));
        if (over.Count > 0)
        {
            var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.Department!.Code == ProductionDepartment && h.User!.IsActive).Select(h => h.UserId)
                .ToListAsync(ct);
            foreach (var user in heads.Append(order.ResponsibleId ?? ctx.UserId).Distinct())
            {
                notifier.Notify(user, UserNotificationKind.ProcurementStatus, $"Перерасход сырья по заданию {order.Number}",
                    string.Join("; ", over.Take(5)) + (over.Count > 5 ? $" и ещё {over.Count - 5}" : ""), $"/production/orders/{order.Id}");
            }
        }

        await SaveAsync(ct);
    }

    /// <summary>Отменить: черновик, выпущенное или в работе, если у цеха на руках ничего нет (выданное сначала возвращают); причина обязательна.</summary>
    public async Task CancelAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct)
    {
        RequireManage(ctx);
        var order = await db.ProductionOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new FieldNotFoundException("Задание на выпуск не найдено.");
        var reason = Clean(comment, 1000) ?? throw new FieldValidationException("Напишите, почему задание отменяется.");
        var net = order.Lines.Sum(l => l.IssuedQty - l.ReturnedQty);
        if (!ProductionRules.Cancellable(order.Status, net))
        {
            throw new FieldConflictException(net != 0
                ? "У цеха на руках сырьё по заданию — сначала оформите возврат, затем отмените."
                : "Закрытое или отменённое задание не отменить.");
        }

        var from = order.Status;
        order.Status = ProductionOrderStatus.Cancelled;
        order.CancelledAt = DateTimeOffset.UtcNow;
        order.UpdatedAt = order.CancelledAt;
        Log(ctx, order.Id, "cancelled", from, ProductionOrderStatus.Cancelled, "Задание отменено", reason);
        await SaveAsync(ct);
    }

    /// <summary>
    /// Оприходовать выход закрытого задания на склад задания (кладовщик): позиция склада выхода из версии рецептуры, количество — фактический
    /// выход в её единице; позиция с партиями — новая партия (номер задания, дата выпуска, срок — по сроку хранения позиции; контроль качества
    /// — как у позиции). Документ PRC-; второй приход по заданию не проводится (проверка и уникальный индекс).
    /// </summary>
    public Task<ProductionMoveResult> ReceiveOutputAsync(ProcurementContext ctx, Guid id, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        var order = await db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new FieldNotFoundException("Задание на выпуск не найдено.");
        // Сначала права: приходует только кладовщик склада задания — и повторный запрос тоже.
        var warehouse = await ledger.WarehouseAsync(ctx, order.WarehouseId, ct);
        if (await db.ProcurementStockDocuments.AsNoTracking().Where(d => d.ProductionOrderId == id && d.Kind == ProcurementStockDocumentKind.ProductionOutput)
                .Select(d => new ProductionMoveResult(d.Id, d.Number)).FirstOrDefaultAsync(ct) is { } done)
        {
            return done;
        }

        if (order.Status != ProductionOrderStatus.Completed || order.OutputQty is not > 0)
        {
            throw new FieldConflictException("Выход приходуют по закрытому заданию с фактическим выходом больше нуля.");
        }

        var recipe = await db.ProductionRecipes.AsNoTracking().Include(r => r.OutputItem).FirstAsync(r => r.Id == order.RecipeId, ct);
        var item = recipe.OutputItem ?? throw new FieldConflictException("У версии рецептуры не указана позиция склада выхода — выход на склад OneBase не приходуется.");
        var qty = ProductionCalc.Convert(order.OutputQty.Value, recipe.OutputUnit, item.UnitCode)
                  ?? throw new FieldConflictException($"Выход в «{recipe.OutputUnit}» не переводится в единицу позиции «{item.UnitCode}».");
        var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.ProductionOutput, warehouse.Id, ctx.UserId, ct);
        doc.ProductionOrderId = order.Id;
        doc.PostedAt = DateTimeOffset.UtcNow;
        doc.DepartmentId = order.DepartmentId;
        doc.Comment = $"Выход задания {order.Number}";
        var balance = (await ledger.BalancesAsync(warehouse.Id, [item.Id], ct))[item.Id];
        string? batchNo = null;
        if (ledger.Tracks(item.Id))
        {
            // Цена партии выхода — материалы задания по цене выданных партий на единицу выхода (без труда и накладных); часть без цены — без цены.
            var cost = await ProductionCost.MaterialsAsync(db, order.Id, ct) is { } materials ? decimal.Round(materials / qty, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
            var today = ledger.Today;
            var batch = await ledger.NewBatchAsync(doc, warehouse.Id, item.Id, order.Number, today, item.ShelfLifeDays is { } days ? today.AddDays(days) : null, cost, null, ct,
                item.QualityControl);
            ledger.MoveBatch(doc, balance, batch, ProcurementMovementKind.ProductionOutput, qty, item.Name, cost);
            batchNo = batch.Batch!.Number;
        }
        else
        {
            ledger.Move(doc, balance, ProcurementMovementKind.ProductionOutput, qty, item.Name);
        }

        Log(ctx, order.Id, "output", null, null,
            $"Выход оприходован ({doc.Number}): {item.Name} {ProcurementRules.Quantity(qty)} {item.UnitCode}{(batchNo is null ? "" : $", партия {batchNo}")}");
        await db.SaveChangesAsync(ct);
        return new ProductionMoveResult(doc.Id, doc.Number);
    });

    /// <summary>
    /// Расход по результатам смен по позициям склада: норма на фактические замесы, брак этапов и материалов, упаковку (штучное — вверх до
    /// целого). Смен нет или карта с ошибками — null. С ним сверяется факт: отклонение и перерасход — к нему, а без смен — к норме на выход.
    /// </summary>
    internal static Dictionary<Guid, decimal>? ResultItems(CalcRecipe calc, CalcResult? results) =>
        results is { Problems.Count: 0 } r
            ? r.Lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.Sum(l =>
            {
                var line = calc.Lines.First(x => x.Key == l.Key);
                var value = ProductionCalc.ToItemUnit(line, l.WithDefects ?? l.Gross) ?? l.ItemGross;
                return ProductionCalc.Counted(l.ItemUnit) ? Math.Ceiling(value) : decimal.Round(value, 3, MidpointRounding.AwayFromZero);
            }))
            : null;

    /// <summary>
    /// Сверка «норма — факт — отклонение» по позициям: выход — фактический при закрытии, а до него — годный по утверждённым сменам; норма на
    /// этот выход и расчёт по результатам смен (фактический брак этапов и материалов, замесы, упаковки) — в единице склада.
    /// </summary>
    private static ProductionOrderCheck? Check(ProductionOrder o, ProductionRecipe recipe, ProductionOrderResults? results, IReadOnlyList<ProductionOrderLineView> lines)
    {
        var output = o.OutputQty ?? results?.GoodOutput;
        if (output is null)
        {
            return null;
        }

        var calc = ProductionRecipeCalc.From(recipe);
        var norm = ProductionCalc.Plan(calc, output.Value);
        var normItems = norm.Problems.Count == 0 ? norm.Items.ToDictionary(i => i.ItemId, i => i.Quantity) : null;
        var byResults = ResultItems(calc, results?.Calculation);
        var rows = lines.Select(l =>
        {
            decimal? n = normItems?.GetValueOrDefault(l.ItemId);
            decimal? b = byResults?.GetValueOrDefault(l.ItemId);
            var basis = b ?? n;
            (decimal Quantity, decimal? Percent)? dev = basis is { } x ? ProductionRules.Deviation(l.Consumed, x) : null;
            return new ProductionCheckLine(l.ItemId, l.Name, l.UnitCode, l.Required, n, b, l.Consumed, results?.Weighed.TryGetValue(l.ItemId, out var w) == true ? w : null,
                dev?.Quantity, dev?.Percent);
        }).ToList();
        return new ProductionOrderCheck(output.Value, o.OutputQty is not null ? "completion" : "shifts", results?.Shifts ?? 0, results?.Calculation.Missing ?? [], rows,
            results?.Calculation);
    }

    internal static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Плановый расчёт версии на количество; карта не считается (не должно быть у утверждённой) — конфликт, а не нули.</summary>
    private static CalcResult Calculate(ProductionRecipe recipe, decimal quantity)
    {
        var calc = ProductionCalc.Plan(ProductionRecipeCalc.From(recipe), quantity);
        if (calc.Problems.Count > 0)
        {
            throw new FieldConflictException($"Рецептура {recipe.Code} (версия {recipe.VersionNo}) не считается: {string.Join(" ", calc.Problems.Take(3))}");
        }

        return calc;
    }

    /// <summary>Расчёт на план задания: снимок — в задании, строки — по позициям.</summary>
    private static IReadOnlyList<(Guid ItemId, int LineNo, decimal Norm, decimal LossPercent, decimal Required)> Plan(ProductionOrder order, ProductionRecipe recipe)
    {
        var calc = Calculate(recipe, order.PlannedQty);
        order.PlanSnapshot = JsonSerializer.Serialize(calc, SnapshotJson);
        return ProductionRecipeCalc.OrderLines(recipe, calc);
    }

    private async Task ApplyAsync(ProductionOrder order, ProductionOrderInput input, CancellationToken ct)
    {
        if (input.DepartmentId is not { } department || !await db.Departments.AnyAsync(d => d.Id == department, ct))
        {
            throw new FieldValidationException("Выберите цех (отдел), который выпускает продукцию.");
        }

        if (input.WarehouseId is not { } warehouse || !await db.ProcurementWarehouses.AnyAsync(w => w.Id == warehouse && w.IsActive, ct))
        {
            throw new FieldValidationException("Выберите действующий склад, с которого выдаётся сырьё.");
        }

        if (!ProductionRules.ValidQuantity(input.PlannedQty, 3))
        {
            throw new FieldValidationException("План — больше нуля, не больше трёх знаков после запятой.");
        }

        if (input.PlannedDate is not { } date)
        {
            throw new FieldValidationException("Укажите дату выпуска.");
        }

        if (input.ResponsibleId is { } responsible && !await db.Users.AnyAsync(u => u.Id == responsible && u.IsActive, ct))
        {
            throw new FieldValidationException("Мастер — действующий пользователь OneBase.");
        }

        order.DepartmentId = department;
        order.WarehouseId = warehouse;
        order.PlannedQty = input.PlannedQty;
        order.PlannedDate = date;
        order.ResponsibleId = input.ResponsibleId;
        order.Comment = Clean(input.Comment, 2000);
    }

    /// <summary>Задание для выдачи или возврата: сначала права (кладовщик склада задания), потом состояние — выдают по выпущенному или в работе.</summary>
    private async Task<(ProductionOrder Order, ProcurementWarehouse Warehouse)> LoadForMoveAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        var order = await db.ProductionOrders.Include(o => o.Lines).ThenInclude(l => l.Item).FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw new FieldNotFoundException("Задание на выпуск не найдено.");
        var warehouse = await ledger.WarehouseAsync(ctx, order.WarehouseId, ct);
        if (!ProductionRules.Issuable(order.Status))
        {
            throw new FieldConflictException(order.Status == ProductionOrderStatus.Draft
                ? "Задание ещё не выпущено — выдавать по нему рано."
                : "Задание закрыто или отменено — выдачи и возвраты по нему не проводятся.");
        }

        return (order, warehouse);
    }

    private static List<(Guid ItemId, decimal Qty, Guid? BatchId)> Lines(IEnumerable<(Guid ItemId, decimal Quantity, Guid? BatchId)>? input)
    {
        var lines = (input ?? []).Where(l => l.Quantity != 0).Select(l => (l.ItemId, Qty: l.Quantity, l.BatchId)).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Укажите количество хотя бы по одной позиции.");
        }

        if (lines.Count > ProcurementRules.MaxItems)
        {
            throw new FieldValidationException($"В одном документе — до {ProcurementRules.MaxItems} строк.");
        }

        if (lines.Any(l => l.Qty < 0 || l.Qty > ProductionRules.MaxQuantity || decimal.Round(l.Qty, 3) != l.Qty))
        {
            throw new FieldValidationException("Количество — больше нуля, не больше трёх знаков после запятой.");
        }

        return lines;
    }

    private async Task<ProductionMoveResult?> DoneAsync(ProcurementContext ctx, Guid? key, CancellationToken ct) =>
        key is { } k
            ? await db.ProcurementStockDocuments.AsNoTracking().Where(d => d.ActorId == ctx.UserId && d.ClientKey == k)
                .Select(d => new ProductionMoveResult(d.Id, d.Number)).FirstOrDefaultAsync(ct)
            : null;

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new FieldConflictException("Задание уже изменили в другом окне — обновите страницу.");
        }
    }

    private void Log(ProcurementContext ctx, Guid id, string action, ProductionOrderStatus? from, ProductionOrderStatus? to, string text, string? comment = null) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProductionRules.OrderDoc, DocId = id, ActorId = ctx.UserId, Action = action, FromStatus = from?.ToString(), ToStatus = to?.ToString(),
            Text = text.Length <= 1000 ? text : text[..999] + "…", Comment = comment,
        });

    private static string? Clean(string? text, int max)
    {
        var t = text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length <= max ? t : throw new FieldValidationException($"Текст — до {max} знаков.");
    }
}

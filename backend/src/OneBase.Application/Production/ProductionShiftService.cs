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

public sealed record ProductionShiftCreateInput(Guid? OrderId, DateOnly? ShiftDate, int ShiftNo = 1, string? Line = null, Guid? OperatorId = null, Guid? SupervisorId = null,
    Guid? ClientKey = null);

/// <summary>Результат этапа из формы: заполнен, масса до и после, влажность, возврат переработки на этап, причина отклонения, комментарий.</summary>
public sealed record ProductionShiftStageInput(int StageNo, bool Done = false, decimal? InputKg = null, decimal? OutputKg = null, decimal? Moisture = null, decimal? ReworkKg = null,
    string? Reason = null, string? Comment = null);

/// <summary>Брак продукта из формы: этап, вид брака версии (или своё название), кг, переработка.</summary>
public sealed record ProductionShiftDefectInput(int StageNo, Guid? DefectTypeId, string? Name, decimal QuantityKg, ProductionReworkStatus? Rework = null);

/// <summary>Материал из формы: позиция рецептуры, взвешено или брак материала, количество в единице склада, этап.</summary>
public sealed record ProductionShiftMaterialInput(Guid ItemId, ProductionShiftMaterialKind Kind, decimal Quantity, int? StageNo = null);

/// <summary>Ввод смены целиком (автосохранение): шапка, годный выход, упаковки, замесы, этапы, брак, материалы; Version — xmin.</summary>
public sealed record ProductionShiftInput(DateOnly? ShiftDate, int ShiftNo, string? Line, Guid? SupervisorId, decimal? GoodOutput, decimal? Packs, decimal? Boxes, string? Comment,
    IReadOnlyList<decimal>? Batches, IReadOnlyList<ProductionShiftStageInput>? Stages, IReadOnlyList<ProductionShiftDefectInput>? Defects,
    IReadOnlyList<ProductionShiftMaterialInput>? Materials, uint? Version = null, string? Reason = null);

public sealed record ProductionShiftSearch(string? Search = null, string? Status = null, Guid? OrderId = null, DateOnly? From = null, DateOnly? To = null, int Page = 1,
    int PageSize = 30);

public sealed record ProductionShiftRow(Guid Id, string Number, ProductionShiftKind Kind, string? CorrectsNumber, Guid OrderId, string OrderNumber, string RecipeName,
    string OutputUnit, DateOnly ShiftDate, int ShiftNo, string? Line, string OperatorName, ProductionShiftStatus Status, decimal? GoodOutput, decimal DefectKg);

public sealed record ProductionShiftList(IReadOnlyList<ProductionShiftRow> Items, int Total, int Page, int PageSize, bool CanCreate, int Drafts, int Closed);

public sealed record ShiftDefectTypeInfo(Guid Id, string Name, decimal NormPercent, bool Reworkable, Guid? ItemId, string? ItemName);

public sealed record ShiftMaterialInfo(Guid ItemId, string Name, string UnitCode, ProductionLineBasis Basis);

/// <summary>Этап версии для ввода: тип, выход и допуск, норматив брака, влажность, переработка, виды брака, материалы этапа.</summary>
public sealed record ShiftStageInfo(int No, string Name, ProductionStageKind Kind, decimal YieldPercent, decimal? YieldMinPercent, decimal? YieldMaxPercent,
    decimal DefectPercent, decimal? MoistureNorm, string? MoistureUnit, decimal? MoistureTolerance, bool Reworkable, IReadOnlyList<ShiftDefectTypeInfo> DefectTypes,
    IReadOnlyList<ShiftMaterialInfo> Materials);

public sealed record ProductionShiftMaterialView(Guid ItemId, string Name, string UnitCode, ProductionShiftMaterialKind Kind, decimal Quantity, int? StageNo);

/// <summary>Что можно со сменой: править и закрыть (ввод), утвердить и вернуть (закрытую — не закрывший её), корректировка (утверждённую), отменить (ввод).</summary>
public sealed record ProductionShiftAbilities(bool Edit, bool Close, bool Approve, bool Return, bool Correct, bool Cancel);

public sealed record ProductionShiftDetail(
    Guid Id,
    string Number,
    ProductionShiftKind Kind,
    Guid? CorrectsId,
    string? CorrectsNumber,
    Guid? CorrectionId,
    Guid OrderId,
    string OrderNumber,
    Guid RecipeId,
    string RecipeCode,
    int RecipeVersion,
    string RecipeName,
    string OutputUnit,
    decimal PlannedQty,
    string DepartmentName,
    DateOnly ShiftDate,
    int ShiftNo,
    string? Line,
    Guid OperatorId,
    string OperatorName,
    Guid? SupervisorId,
    string? SupervisorName,
    ProductionShiftStatus Status,
    decimal? GoodOutput,
    decimal? Packs,
    decimal? Boxes,
    string? Comment,
    string? Reason,
    DateTimeOffset? ClosedAt,
    string? ClosedByName,
    DateTimeOffset? ApprovedAt,
    string? ApprovedByName,
    uint Version,
    decimal? StandardBatchKg,
    IReadOnlyList<ShiftStageInfo> Template,
    IReadOnlyList<decimal> Batches,
    IReadOnlyList<ProductionShiftStageInput> Stages,
    IReadOnlyList<ProductionShiftDefectInput> Defects,
    IReadOnlyList<ProductionShiftMaterialView> Materials,
    IReadOnlyList<string> Problems,
    CalcResult Calculation,
    IReadOnlyList<ProcurementEventView> History,
    ProductionShiftAbilities Can,
    IReadOnlyList<ShiftMaterialInfo>? Ingredients = null);

public sealed record ShiftOrderOption(Guid Id, string Number, string RecipeName, string OutputUnit, decimal PlannedQty, DateOnly PlannedDate, string DepartmentName);

public sealed record ProductionShiftForm(IReadOnlyList<ShiftOrderOption> Orders, IReadOnlyList<ProductionNamed> People);

/// <summary>Результаты задания по утверждённым сменам: расчёт по результатам, сколько смен, годный выход, взвешено по позициям.</summary>
public sealed record ProductionOrderResults(CalcResult Calculation, int Shifts, decimal GoodOutput, IReadOnlyDictionary<Guid, decimal> Weighed);

/// <summary>
/// Сменные отчёты производства. Оператор (право «Производство: ввод смены») или мастер создаёт смену по выпущенному заданию и вводит
/// результаты: фактические замесы, выпечку (до и после — вне допуска нужна причина), брак по этапам и видам, переработку, упаковку, взвешенный
/// расход и брак материалов; ввод сохраняется автоматически (версия — xmin). Закрыть — только полную смену (проверки полноты); утвердить или
/// вернуть с причиной — мастер или начальник производства, не закрывший её. Утверждённая не правится — исправляет корректировка, которая
/// после утверждения заменяет смену. Рецептуру, нормы и задания оператор не меняет. Расчёт по результатам — ProductionCalc.ByResults.
/// </summary>
public sealed class ProductionShiftService(IAppDbContext db, IProcurementNumbers numbers, UserNotifier notifier, TimeProvider clock)
{
    public const string Prefix = "SHF";
    public const string ShiftDoc = "production_shift";
    public const int MaxBatches = 300;
    public const int MaxRows = 300;

    private static void RequireView(ProcurementContext ctx)
    {
        if (!ctx.SeesProduction)
        {
            throw new FieldForbiddenException("Смены видят производство, технолог, оператор, кладовщик, снабжение и директор.");
        }
    }

    private static void RequireEnter(ProcurementContext ctx)
    {
        if (!ctx.Operates)
        {
            throw new FieldForbiddenException("Смену вводит оператор производства или мастер — нужно право «Производство: ввод смены».");
        }
    }

    /// <summary>Утверждает смену мастер (задания на выпуск) или начальник производства (руководитель отдела «Производство»).</summary>
    private async Task<bool> ApproverAsync(ProcurementContext ctx, CancellationToken ct) =>
        ctx.Produces || (ctx.HeadOf.Count > 0 && await db.Departments.AnyAsync(d => d.Code == ProductionOrderService.ProductionDepartment && ctx.HeadOf.Contains(d.Id), ct));

    public async Task<ProductionShiftList> SearchAsync(ProcurementContext ctx, ProductionShiftSearch s, CancellationToken ct)
    {
        RequireView(ctx);
        var q = db.ProductionShifts.AsNoTracking();
        if (s.Search?.Trim().ToLower() is { Length: > 0 } text)
        {
            q = q.Where(x => x.Number.ToLower().Contains(text) || x.Order!.Number.ToLower().Contains(text) || x.Order.Recipe!.Name.ToLower().Contains(text));
        }

        if (Enum.TryParse<ProductionShiftStatus>(s.Status, true, out var status))
        {
            q = q.Where(x => x.Status == status);
        }
        else if (s.Status == "open")
        {
            q = q.Where(x => x.Status == ProductionShiftStatus.Draft || x.Status == ProductionShiftStatus.Closed);
        }

        if (s.OrderId is { } order)
        {
            q = q.Where(x => x.OrderId == order);
        }

        if (s.From is { } from)
        {
            q = q.Where(x => x.ShiftDate >= from);
        }

        if (s.To is { } to)
        {
            q = q.Where(x => x.ShiftDate <= to);
        }

        var total = await q.CountAsync(ct);
        var size = Math.Clamp(s.PageSize, 1, 100);
        var page = Math.Max(1, s.Page);
        var rows = await q.OrderByDescending(x => x.ShiftDate).ThenByDescending(x => x.ShiftNo).ThenByDescending(x => x.Number).Skip((page - 1) * size).Take(size)
            .Select(x => new ProductionShiftRow(x.Id, x.Number, x.Kind, x.CorrectsId == null ? null : db.ProductionShifts.Where(c => c.Id == x.CorrectsId).Select(c => c.Number).FirstOrDefault(),
                x.OrderId, x.Order!.Number, x.Order.Recipe!.Name, x.Order.Recipe.OutputUnit, x.ShiftDate, x.ShiftNo, x.Line, x.Operator!.FullName, x.Status, x.GoodOutput,
                x.Defects.Sum(d => (decimal?)d.QuantityKg) ?? 0))
            .ToListAsync(ct);
        var counts = await db.ProductionShifts.AsNoTracking().GroupBy(x => x.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return new ProductionShiftList(rows, total, page, size, ctx.Operates, counts.GetValueOrDefault(ProductionShiftStatus.Draft), counts.GetValueOrDefault(ProductionShiftStatus.Closed));
    }

    /// <summary>Для новой смены: задания в работе (выпущены или в работе) и кто вводит смены (операторы и мастера, не администратор).</summary>
    public async Task<ProductionShiftForm> FormAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireEnter(ctx);
        var orders = await db.ProductionOrders.AsNoTracking()
            .Where(o => o.Status == ProductionOrderStatus.Released || o.Status == ProductionOrderStatus.InProgress)
            .OrderBy(o => o.PlannedDate).ThenBy(o => o.Number)
            .Select(o => new ShiftOrderOption(o.Id, o.Number, o.Recipe!.Name, o.Recipe.OutputUnit, o.PlannedQty, o.PlannedDate, o.Department!.Name))
            .ToListAsync(ct);
        var ids = (await ProcurementPeople.WithPermissionAsync(db, Permissions.ProductionOperate, ct))
            .Concat(await ProcurementPeople.WithPermissionAsync(db, Permissions.ProductionManage, ct)).Append(ctx.UserId).Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).OrderBy(u => u.FullName).Select(u => new ProductionNamed(u.Id, u.FullName))
            .ToListAsync(ct);
        return new ProductionShiftForm(orders, people);
    }

    /// <summary>Новая смена по выпущенному заданию (или в работе): дата, номер смены, линия, оператор (по умолчанию — создающий), начальник смены.</summary>
    public async Task<Guid> CreateAsync(ProcurementContext ctx, ProductionShiftCreateInput input, CancellationToken ct)
    {
        RequireEnter(ctx);
        if (input.ClientKey is { } key
            && await db.ProductionShifts.AsNoTracking().Where(x => x.CreatedById == ctx.UserId && x.ClientKey == key).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct) is { } done)
        {
            return done;
        }

        var order = await db.ProductionOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == input.OrderId, ct) ?? throw new FieldValidationException("Выберите задание на выпуск.");
        if (!ProductionRules.Issuable(order.Status))
        {
            throw new FieldConflictException(order.Status == ProductionOrderStatus.Draft
                ? "Задание ещё не выпущено — смену по нему не открыть."
                : "Задание закрыто или отменено — новые смены по нему не вводят.");
        }

        var shift = new ProductionShift
        {
            Number = await numbers.NextAsync(Prefix, ct), OrderId = order.Id, ShiftDate = input.ShiftDate ?? ProcurementBatchRules.Today(clock), ShiftNo = input.ShiftNo,
            Line = Clean(input.Line, 100), OperatorId = input.OperatorId ?? ctx.UserId, SupervisorId = input.SupervisorId, CreatedById = ctx.UserId, ClientKey = input.ClientKey,
        };
        if (shift.ShiftNo is < 1 or > 9)
        {
            throw new FieldValidationException("Номер смены — от 1 до 9.");
        }

        await RequireUsersAsync(shift.OperatorId, shift.SupervisorId, ct);
        db.ProductionShifts.Add(shift);
        Log(ctx, shift.Id, "created", null, ProductionShiftStatus.Draft, $"Открыта смена {shift.Number} по заданию {order.Number}: {shift.ShiftDate:dd.MM.yyyy}, смена {shift.ShiftNo}");
        await SaveAsync(ct);
        return shift.Id;
    }

    public async Task<ProductionShiftDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireView(ctx);
        var s = await Load(db.ProductionShifts.AsNoTracking()).Include(x => x.Operator).Include(x => x.Supervisor).Include(x => x.ApprovedBy)
                    .Include(x => x.Order).ThenInclude(o => o!.Department).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Смена не найдена.");
        var recipe = (await ProductionRecipeService.LoadAsync(db, r => r.Id == s.Order!.RecipeId, false, ct)).Single();
        var template = Template(recipe);
        var calc = ProductionCalc.ByResults(ProductionRecipeCalc.From(recipe), ProductionShiftRules.Actual(recipe, [s]));
        var problems = ProductionShiftRules.CloseProblems(template, s);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == ShiftDoc && e.DocId == id).OrderBy(e => e.CreatedAt)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var names = await db.Users.AsNoTracking().Where(u => u.Id == s.ClosedById).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var corrects = s.CorrectsId is { } cid ? await db.ProductionShifts.AsNoTracking().Where(x => x.Id == cid).Select(x => x.Number).FirstOrDefaultAsync(ct) : null;
        var correction = await db.ProductionShifts.AsNoTracking().Where(x => x.CorrectsId == id && x.Status != ProductionShiftStatus.Cancelled).OrderByDescending(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var approver = await ApproverAsync(ctx, ct);
        var closer = s.ClosedById == ctx.UserId;
        var can = new ProductionShiftAbilities(
            ctx.Operates && s.Status == ProductionShiftStatus.Draft,
            ctx.Operates && s.Status == ProductionShiftStatus.Draft && problems.Count == 0,
            approver && s.Status == ProductionShiftStatus.Closed && !closer,
            s.Status == ProductionShiftStatus.Closed && ((approver && !closer) || (ctx.Operates && closer)),
            ctx.Operates && s.Status == ProductionShiftStatus.Approved && correction is null,
            ctx.Operates && s.Status == ProductionShiftStatus.Draft);
        var mixing = recipe.Stages.FirstOrDefault(x => x.Kind == ProductionStageKind.Mixing);
        return new ProductionShiftDetail(s.Id, s.Number, s.Kind, s.CorrectsId, corrects, correction, s.OrderId, s.Order!.Number, recipe.Id, recipe.Code, recipe.VersionNo,
            recipe.Name, recipe.OutputUnit, s.Order.PlannedQty, s.Order.Department!.Name, s.ShiftDate, s.ShiftNo, s.Line, s.OperatorId, s.Operator!.FullName, s.SupervisorId,
            s.Supervisor?.FullName, s.Status, s.GoodOutput, s.Packs, s.Boxes, s.Comment, s.Reason, s.ClosedAt, names, s.ApprovedAt, s.ApprovedBy?.FullName, s.Version,
            calc.Batches?.StandardKg ?? (mixing is null ? null : ProductionCalc.Plan(ProductionRecipeCalc.From(recipe), recipe.OutputQuantity).Batches?.StandardKg),
            template, s.Batches.OrderBy(b => b.No).Select(b => b.MassKg).ToList(),
            s.Stages.OrderBy(x => x.StageNo).Select(x => new ProductionShiftStageInput(x.StageNo, x.Done, x.InputKg, x.OutputKg, x.Moisture, x.ReworkKg, x.Reason, x.Comment)).ToList(),
            s.Defects.OrderBy(d => d.StageNo).ThenBy(d => d.Name).Select(d => new ProductionShiftDefectInput(d.StageNo, d.DefectTypeId, d.Name, d.QuantityKg, d.Rework)).ToList(),
            s.Materials.OrderBy(m => m.Kind).ThenBy(m => m.Item!.Name).Select(m => new ProductionShiftMaterialView(m.ItemId, m.Item!.Name, m.Item.UnitCode, m.Kind, m.Quantity, m.StageNo))
                .ToList(),
            problems, calc, history, can,
            recipe.Stages.Count == 0 ? recipe.Lines.OrderBy(l => l.LineNo).Select(l => new ShiftMaterialInfo(l.ItemId, l.Item!.Name, l.Item.UnitCode, l.Basis)).ToList() : []);
    }

    /// <summary>Автосохранение ввода (только смена «ввод»): всё содержимое заменяется присланным; версия — xmin, в ответ — новая.</summary>
    public async Task<uint> SaveAsync(ProcurementContext ctx, Guid id, ProductionShiftInput input, CancellationToken ct)
    {
        RequireEnter(ctx);
        var shift = await Load(db.ProductionShifts).Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Смена не найдена.");
        if (shift.Status != ProductionShiftStatus.Draft)
        {
            throw new FieldConflictException("Закрытую и утверждённую смену не правят — исправления вносит корректировка.");
        }

        if (input.Version is { } version && version != shift.Version)
        {
            throw new FieldConflictException("Смену уже изменили в другом окне — обновите страницу.");
        }

        var recipe = (await ProductionRecipeService.LoadAsync(db, r => r.Id == shift.Order!.RecipeId, false, ct)).Single();
        var template = Template(recipe);
        if (input.ShiftNo is < 1 or > 9)
        {
            throw new FieldValidationException("Номер смены — от 1 до 9.");
        }

        Qty(input.GoodOutput, "Годный выход");
        Qty(input.Packs, "Упаковок");
        Qty(input.Boxes, "Коробок");
        await RequireUsersAsync(shift.OperatorId, input.SupervisorId, ct);
        var batches = input.Batches ?? [];
        if (batches.Count > MaxBatches || batches.Any(b => b <= 0 || b > ProductionRules.MaxQuantity || decimal.Round(b, 3) != b))
        {
            throw new FieldValidationException($"Замесы — до {MaxBatches}, масса каждого больше нуля, до грамма.");
        }

        var stages = input.Stages ?? [];
        var defects = input.Defects ?? [];
        var materials = input.Materials ?? [];
        if (stages.Count > MaxRows || defects.Count > MaxRows || materials.Count > MaxRows)
        {
            throw new FieldValidationException($"Строк в смене — до {MaxRows}.");
        }

        foreach (var x in stages)
        {
            var info = template.FirstOrDefault(t => t.No == x.StageNo) ?? throw new FieldValidationException($"Этапа {x.StageNo} нет в рецептуре задания.");
            Qty(x.InputKg, $"«{info.Name}»: масса до");
            Qty(x.OutputKg, $"«{info.Name}»: масса после");
            Qty(x.ReworkKg, $"«{info.Name}»: возврат переработки");
            if (x.Moisture is < 0 or > 1_000_000)
            {
                throw new FieldValidationException($"«{info.Name}»: влажность — не меньше нуля.");
            }
        }

        if (stages.GroupBy(x => x.StageNo).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Этап в смене — одной строкой.");
        }

        var lines = defects.Select(d =>
        {
            var info = template.FirstOrDefault(t => t.No == d.StageNo) ?? throw new FieldValidationException($"Этапа {d.StageNo} нет в рецептуре задания.");
            var type = d.DefectTypeId is { } tid
                ? info.DefectTypes.FirstOrDefault(t => t.Id == tid && t.ItemId is null) ?? throw new FieldValidationException($"«{info.Name}»: такого вида брака продукта на этапе нет.")
                : null;
            var name = type?.Name ?? Clean(d.Name, 100) ?? throw new FieldValidationException($"«{info.Name}»: выберите вид брака.");
            if (d.QuantityKg <= 0 || d.QuantityKg > ProductionRules.MaxQuantity || decimal.Round(d.QuantityKg, 3) != d.QuantityKg)
            {
                throw new FieldValidationException($"«{info.Name}», брак «{name}»: кг — больше нуля, до грамма.");
            }

            var rework = d.Rework ?? (type?.Reworkable ?? info.Reworkable ? ProductionReworkStatus.Allowed : ProductionReworkStatus.Forbidden);
            if (rework != ProductionReworkStatus.Forbidden && !(type?.Reworkable ?? info.Reworkable))
            {
                throw new FieldValidationException($"«{info.Name}», брак «{name}»: переработка этого брака не разрешена технологом.");
            }

            return new ProductionShiftDefect { ShiftId = shift.Id, StageNo = d.StageNo, DefectTypeId = type?.Id, Name = name, QuantityKg = d.QuantityKg, Rework = rework };
        }).ToList();
        var items = recipe.Lines.Select(l => l.ItemId).ToHashSet();
        var materialRows = materials.Select(m =>
        {
            if (!items.Contains(m.ItemId))
            {
                throw new FieldValidationException("Материала нет в рецептуре задания.");
            }

            if (!Enum.IsDefined(m.Kind) || m.Quantity <= 0 || m.Quantity > ProductionRules.MaxQuantity || decimal.Round(m.Quantity, 3) != m.Quantity)
            {
                throw new FieldValidationException("Количество материала — больше нуля, не больше трёх знаков после запятой.");
            }

            if (m.StageNo is { } no && template.All(t => t.No != no))
            {
                throw new FieldValidationException($"Этапа {no} нет в рецептуре задания.");
            }

            return new ProductionShiftMaterial { ShiftId = shift.Id, ItemId = m.ItemId, Kind = m.Kind, Quantity = m.Quantity, StageNo = m.StageNo };
        }).ToList();
        if (materialRows.GroupBy(m => (m.ItemId, m.Kind, m.StageNo)).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Материал в смене — одной строкой на этап и вид (взвешено или брак).");
        }

        shift.ShiftDate = input.ShiftDate ?? shift.ShiftDate;
        shift.ShiftNo = input.ShiftNo;
        shift.Line = Clean(input.Line, 100);
        shift.SupervisorId = input.SupervisorId;
        shift.GoodOutput = input.GoodOutput;
        shift.Packs = input.Packs;
        shift.Boxes = input.Boxes;
        shift.Comment = Clean(input.Comment, 2000);
        if (shift.Kind == ProductionShiftKind.Correction)
        {
            shift.Reason = Clean(input.Reason, 1000);
        }

        shift.UpdatedAt = DateTimeOffset.UtcNow;
        Replace(shift.Batches, batches.Select((m, i) => new ProductionShiftBatch { ShiftId = shift.Id, No = i + 1, MassKg = m }), db.ProductionShiftBatches);
        Replace(shift.Stages, stages.Select(x => new ProductionShiftStage
        {
            ShiftId = shift.Id, StageNo = x.StageNo, Done = x.Done, InputKg = x.InputKg, OutputKg = x.OutputKg, Moisture = x.Moisture, ReworkKg = x.ReworkKg,
            Reason = Clean(x.Reason, 1000), Comment = Clean(x.Comment, 1000),
        }), db.ProductionShiftStages);
        Replace(shift.Defects, lines, db.ProductionShiftDefects);
        Replace(shift.Materials, materialRows, db.ProductionShiftMaterials);
        await SaveAsync(ct);
        return shift.Version;
    }

    /// <summary>
    /// Закрыть смену: проверки полноты (выход, замесы, все этапы, выпечка до и после, отклонение с причиной, упаковки; переработка не больше
    /// направленного в неё брака по заданию). Мастеру задания и начальнику производства — уведомление.
    /// </summary>
    public async Task CloseAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireEnter(ctx);
        var shift = await Load(db.ProductionShifts).Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Смена не найдена.");
        if (shift.Status != ProductionShiftStatus.Draft)
        {
            throw new FieldConflictException("Закрывают смену в статусе «ввод».");
        }

        var recipe = (await ProductionRecipeService.LoadAsync(db, r => r.Id == shift.Order!.RecipeId, false, ct)).Single();
        if (ProductionShiftRules.CloseProblems(Template(recipe), shift) is { Count: > 0 } problems)
        {
            throw new FieldValidationException($"Смена не заполнена: {string.Join(" ", problems.Take(4))}{(problems.Count > 4 ? $" И ещё {problems.Count - 4}." : "")}");
        }

        await RequireReworkAsync(shift, ct);
        shift.Status = ProductionShiftStatus.Closed;
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.ClosedById = ctx.UserId;
        Log(ctx, shift.Id, "closed", ProductionShiftStatus.Draft, ProductionShiftStatus.Closed,
            $"Смена закрыта: годный выход {ProcurementRules.Quantity(shift.GoodOutput ?? 0)} {recipe.OutputUnit}, брак {ProcurementRules.Quantity(shift.Defects.Sum(d => d.QuantityKg))} кг");
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.Department!.Code == ProductionOrderService.ProductionDepartment && h.User!.IsActive)
            .Select(h => h.UserId).ToListAsync(ct);
        foreach (var user in heads.Append(shift.Order!.ResponsibleId ?? shift.Order.CreatedById).Append(shift.SupervisorId ?? Guid.Empty).Where(u => u != Guid.Empty && u != ctx.UserId)
                     .Distinct())
        {
            notifier.Notify(user, UserNotificationKind.ProcurementStatus, $"Смена {shift.Number} ждёт утверждения", $"Задание {shift.Order.Number}, {shift.ShiftDate:dd.MM.yyyy}",
                $"/production/shifts/{shift.Id}");
        }

        await SaveAsync(ct);
    }

    /// <summary>Утвердить закрытую смену: мастер или начальник производства, не закрывший её. Корректировка после утверждения заменяет исправляемую смену.</summary>
    public async Task ApproveAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        var shift = await db.ProductionShifts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Смена не найдена.");
        if (!await ApproverAsync(ctx, ct))
        {
            throw new FieldForbiddenException("Смену утверждает мастер или начальник производства.");
        }

        if (shift.Status != ProductionShiftStatus.Closed)
        {
            throw new FieldConflictException("Утверждают закрытую смену.");
        }

        if (shift.ClosedById == ctx.UserId)
        {
            throw new FieldForbiddenException("Закрытую вами смену утверждает другой — мастер или начальник производства.");
        }

        shift.Status = ProductionShiftStatus.Approved;
        shift.ApprovedAt = DateTimeOffset.UtcNow;
        shift.ApprovedById = ctx.UserId;
        Log(ctx, shift.Id, "approved", ProductionShiftStatus.Closed, ProductionShiftStatus.Approved, "Смена утверждена");
        if (shift.CorrectsId is { } original && await db.ProductionShifts.FirstOrDefaultAsync(x => x.Id == original, ct) is { } replaced)
        {
            replaced.Status = ProductionShiftStatus.Superseded;
            replaced.UpdatedAt = DateTimeOffset.UtcNow;
            Log(ctx, replaced.Id, "superseded", ProductionShiftStatus.Approved, ProductionShiftStatus.Superseded, $"Смену заменила корректировка {shift.Number}");
        }

        notifier.Notify(shift.OperatorId, UserNotificationKind.ProcurementStatus, $"Смена {shift.Number} утверждена", null, $"/production/shifts/{shift.Id}");
        await SaveAsync(ct);
    }

    /// <summary>Вернуть закрытую смену во ввод: утверждающий — с причиной (оператору уведомление), закрывший — сам.</summary>
    public async Task ReturnAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct)
    {
        var shift = await db.ProductionShifts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Смена не найдена.");
        if (shift.Status != ProductionShiftStatus.Closed)
        {
            throw new FieldConflictException("Вернуть можно закрытую смену.");
        }

        var reason = Clean(comment, 1000);
        var own = shift.ClosedById == ctx.UserId;
        if (own)
        {
            RequireEnter(ctx);
        }
        else
        {
            if (!await ApproverAsync(ctx, ct))
            {
                throw new FieldForbiddenException("Вернуть смену может мастер или начальник производства.");
            }

            if (reason is null)
            {
                throw new FieldValidationException("Напишите, что исправить в смене.");
            }
        }

        shift.Status = ProductionShiftStatus.Draft;
        shift.UpdatedAt = DateTimeOffset.UtcNow;
        Log(ctx, shift.Id, "returned", ProductionShiftStatus.Closed, ProductionShiftStatus.Draft, own ? "Смена открыта снова для ввода" : "Смена возвращена на ввод", reason);
        if (!own)
        {
            notifier.Notify(shift.OperatorId, UserNotificationKind.ProcurementStatus, $"Смена {shift.Number} возвращена", reason, $"/production/shifts/{shift.Id}");
        }

        await SaveAsync(ct);
    }

    /// <summary>Корректировка утверждённой смены: новая смена-копия во вводе (причина обязательна при закрытии); у смены одна открытая корректировка.</summary>
    public async Task<Guid> CorrectAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireEnter(ctx);
        var source = await Load(db.ProductionShifts.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Смена не найдена.");
        if (source.Status != ProductionShiftStatus.Approved)
        {
            throw new FieldConflictException("Корректируют утверждённую смену.");
        }

        if (await db.ProductionShifts.AnyAsync(x => x.CorrectsId == id && (x.Status == ProductionShiftStatus.Draft || x.Status == ProductionShiftStatus.Closed), ct))
        {
            throw new FieldConflictException("У смены уже есть корректировка в работе.");
        }

        var copy = new ProductionShift
        {
            Number = await numbers.NextAsync(Prefix, ct), OrderId = source.OrderId, Kind = ProductionShiftKind.Correction, CorrectsId = source.Id, ShiftDate = source.ShiftDate,
            ShiftNo = source.ShiftNo, Line = source.Line, OperatorId = source.OperatorId, SupervisorId = source.SupervisorId, GoodOutput = source.GoodOutput, Packs = source.Packs,
            Boxes = source.Boxes, Comment = source.Comment, CreatedById = ctx.UserId,
        };
        foreach (var b in source.Batches)
        {
            copy.Batches.Add(new ProductionShiftBatch { ShiftId = copy.Id, No = b.No, MassKg = b.MassKg });
        }

        foreach (var x in source.Stages)
        {
            copy.Stages.Add(new ProductionShiftStage
            {
                ShiftId = copy.Id, StageNo = x.StageNo, Done = x.Done, InputKg = x.InputKg, OutputKg = x.OutputKg, Moisture = x.Moisture, ReworkKg = x.ReworkKg, Reason = x.Reason,
                Comment = x.Comment,
            });
        }

        foreach (var d in source.Defects)
        {
            copy.Defects.Add(new ProductionShiftDefect { ShiftId = copy.Id, StageNo = d.StageNo, DefectTypeId = d.DefectTypeId, Name = d.Name, QuantityKg = d.QuantityKg, Rework = d.Rework });
        }

        foreach (var m in source.Materials)
        {
            copy.Materials.Add(new ProductionShiftMaterial { ShiftId = copy.Id, ItemId = m.ItemId, StageNo = m.StageNo, Kind = m.Kind, Quantity = m.Quantity });
        }

        db.ProductionShifts.Add(copy);
        Log(ctx, copy.Id, "created", null, ProductionShiftStatus.Draft, $"Корректировка смены {source.Number}");
        Log(ctx, source.Id, "correction", null, null, $"Создана корректировка {copy.Number}");
        await SaveAsync(ct);
        return copy.Id;
    }

    /// <summary>Отменить смену во вводе (ошибочно открытую) — с причиной; в расчёты не входит.</summary>
    public async Task CancelAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct)
    {
        RequireEnter(ctx);
        var shift = await db.ProductionShifts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Смена не найдена.");
        var reason = Clean(comment, 1000) ?? throw new FieldValidationException("Напишите, почему смена отменяется.");
        if (shift.Status != ProductionShiftStatus.Draft)
        {
            throw new FieldConflictException("Отменить можно смену во вводе.");
        }

        shift.Status = ProductionShiftStatus.Cancelled;
        shift.UpdatedAt = DateTimeOffset.UtcNow;
        Log(ctx, shift.Id, "cancelled", ProductionShiftStatus.Draft, ProductionShiftStatus.Cancelled, "Смена отменена", reason);
        await SaveAsync(ct);
    }

    /// <summary>Результаты задания по утверждённым сменам (без заменённых корректировкой): расчёт по результатам, годный выход, взвешено по позициям.</summary>
    public async Task<ProductionOrderResults?> OrderResultsAsync(ProductionOrder order, ProductionRecipe recipe, CancellationToken ct)
    {
        var shifts = await Load(db.ProductionShifts.AsNoTracking()).Where(x => x.OrderId == order.Id && x.Status == ProductionShiftStatus.Approved).ToListAsync(ct);
        if (shifts.Count == 0)
        {
            return null;
        }

        var calc = ProductionCalc.ByResults(ProductionRecipeCalc.From(recipe), ProductionShiftRules.Actual(recipe, shifts));
        var weighed = shifts.SelectMany(x => x.Materials).Where(m => m.Kind == ProductionShiftMaterialKind.Weighed).GroupBy(m => m.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));
        return new ProductionOrderResults(calc, shifts.Count, shifts.Sum(x => x.GoodOutput ?? 0), weighed);
    }

    /// <summary>Этапы версии для ввода смены: выход и допуск, норматив брака, влажность, виды брака продукта и материалов, материалы этапа.</summary>
    internal static IReadOnlyList<ShiftStageInfo> Template(ProductionRecipe recipe) =>
        recipe.Stages.OrderBy(s => s.StageNo).Select(s => new ShiftStageInfo(s.StageNo, s.Name, s.Kind, s.YieldPercent, s.YieldMinPercent, s.YieldMaxPercent,
            recipe.DefectTypes.Where(d => d.StageId == s.Id && d.ItemId == null).Sum(d => d.NormPercent), s.MoistureNorm, s.MoistureUnit, s.MoistureTolerance, s.Reworkable,
            recipe.DefectTypes.Where(d => d.StageId == s.Id).OrderBy(d => d.ItemId != null).ThenBy(d => d.Name)
                .Select(d => new ShiftDefectTypeInfo(d.Id, d.Name, d.NormPercent, d.Reworkable, d.ItemId, d.Item?.Name)).ToList(),
            recipe.Lines.Where(l => l.StageId == s.Id).OrderBy(l => l.LineNo).Select(l => new ShiftMaterialInfo(l.ItemId, l.Item!.Name, l.Item.UnitCode, l.Basis)).ToList()))
        .ToList();

    internal static IQueryable<ProductionShift> Load(IQueryable<ProductionShift> q) =>
        q.Include(x => x.Batches).Include(x => x.Stages).Include(x => x.Defects).Include(x => x.Materials).ThenInclude(m => m.Item);

    /// <summary>Возвращено из переработки по заданию (утверждённые смены и эта) — не больше брака, направленного на переработку: одно и то же не используют дважды.</summary>
    private async Task RequireReworkAsync(ProductionShift shift, CancellationToken ct)
    {
        var back = shift.Stages.Sum(x => x.ReworkKg ?? 0);
        if (back == 0)
        {
            return;
        }

        var others = await db.ProductionShifts.AsNoTracking().Where(x => x.OrderId == shift.OrderId && x.Id != shift.Id && x.Id != shift.CorrectsId
                && (x.Status == ProductionShiftStatus.Approved || x.Status == ProductionShiftStatus.Closed))
            .Select(x => new
            {
                Sent = x.Defects.Where(d => d.Rework == ProductionReworkStatus.Sent).Sum(d => (decimal?)d.QuantityKg) ?? 0,
                Back = x.Stages.Sum(st => st.ReworkKg) ?? 0,
            })
            .ToListAsync(ct);
        var sent = others.Sum(x => x.Sent) + shift.Defects.Where(d => d.Rework == ProductionReworkStatus.Sent).Sum(d => d.QuantityKg);
        var used = others.Sum(x => x.Back) + back;
        if (used > sent)
        {
            throw new FieldConflictException(
                $"Возвращено из переработки {ProcurementRules.Quantity(used)} кг, а направлено на переработку по заданию {ProcurementRules.Quantity(sent)} кг — больше вернуть нельзя.");
        }
    }

    private async Task RequireUsersAsync(Guid operatorId, Guid? supervisorId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == operatorId && u.IsActive, ct))
        {
            throw new FieldValidationException("Оператор — действующий пользователь OneBase.");
        }

        if (supervisorId is { } sid && !await db.Users.AnyAsync(u => u.Id == sid && u.IsActive, ct))
        {
            throw new FieldValidationException("Начальник смены — действующий пользователь OneBase.");
        }
    }

    private void Replace<T>(ICollection<T> current, IEnumerable<T> next, DbSet<T> set) where T : class
    {
        foreach (var old in current.ToList())
        {
            current.Remove(old);
            set.Remove(old);
        }

        foreach (var row in next)
        {
            current.Add(row);
            set.Add(row);
        }
    }

    private static void Qty(decimal? value, string label)
    {
        if (value is { } v && (v < 0 || v > ProductionRules.MaxQuantity || decimal.Round(v, 3) != v))
        {
            throw new FieldValidationException($"{label} — не меньше нуля, не больше трёх знаков после запятой.");
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new FieldConflictException("Смену уже изменили в другом окне — обновите страницу.");
        }
        catch (DbUpdateException e) when (e.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            throw new FieldConflictException("Смену уже изменили одновременно — обновите страницу.");
        }
    }

    private void Log(ProcurementContext ctx, Guid id, string action, ProductionShiftStatus? from, ProductionShiftStatus? to, string text, string? comment = null) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ShiftDoc, DocId = id, ActorId = ctx.UserId, Action = action, FromStatus = from?.ToString(), ToStatus = to?.ToString(),
            Text = text.Length <= 1000 ? text : text[..999] + "…", Comment = comment,
        });

    private static string? Clean(string? text, int max)
    {
        var t = text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length <= max ? t : throw new FieldValidationException($"Текст — до {max} знаков.");
    }
}

/// <summary>Правила смены (чистые функции — проверяются тестами): полнота перед закрытием и сборка фактических результатов для расчёта.</summary>
public static class ProductionShiftRules
{
    /// <summary>
    /// Чего не хватает, чтобы закрыть смену: годный выход; замесы (если есть замес); каждый этап отмечен заполненным; выпечка — масса до и
    /// после, выход вне допуска — с причиной; упаковки (если есть упаковка); у корректировки — причина.
    /// </summary>
    public static IReadOnlyList<string> CloseProblems(IReadOnlyList<ShiftStageInfo> stages, ProductionShift s)
    {
        var problems = new List<string>();
        if (s.GoodOutput is null)
        {
            problems.Add("Внесите годный выход смены.");
        }

        if (stages.Any(x => x.Kind == ProductionStageKind.Mixing) && s.Batches.Count == 0)
        {
            problems.Add("Внесите фактические замесы.");
        }

        foreach (var info in stages)
        {
            var row = s.Stages.FirstOrDefault(x => x.StageNo == info.No);
            if (row is not { Done: true })
            {
                problems.Add($"Этап «{info.Name}»: отметьте, что результаты и брак внесены.");
                continue;
            }

            if (info.Kind != ProductionStageKind.Baking)
            {
                continue;
            }

            if (row.InputKg is not > 0 || row.OutputKg is null)
            {
                problems.Add($"Этап «{info.Name}»: внесите массу до и после выпечки.");
                continue;
            }

            var bake = ProductionCalc.Bake(row.InputKg.Value, row.OutputKg.Value, info.YieldPercent, info.YieldMinPercent, info.YieldMaxPercent);
            if (bake.OutOfRange && string.IsNullOrWhiteSpace(row.Reason))
            {
                problems.Add($"Этап «{info.Name}»: выход {bake.YieldPercent:0.##} % вне допуска — укажите причину.");
            }
        }

        if (stages.Any(x => x.Kind == ProductionStageKind.Packing) && s.Packs is null)
        {
            problems.Add("Внесите, сколько упаковок упаковано.");
        }

        if (s.Kind == ProductionShiftKind.Correction && string.IsNullOrWhiteSpace(s.Reason))
        {
            problems.Add("Корректировка: укажите причину исправления.");
        }

        return problems;
    }

    /// <summary>
    /// Фактические результаты смен для расчёта по результатам: годный выход, брак продукта по этапам (этап заполнен во всех сменах — иначе
    /// неполный), замесы, упаковки и коробки, брак материалов (в единице строки рецептуры), возврат переработки по этапам.
    /// </summary>
    public static CalcActual Actual(ProductionRecipe recipe, IReadOnlyList<ProductionShift> shifts)
    {
        var calc = ProductionRecipeCalc.From(recipe);
        var stageDefects = new Dictionary<int, decimal>();
        foreach (var stage in calc.Stages)
        {
            if (shifts.Count > 0 && shifts.All(s => s.Stages.Any(x => x.StageNo == stage.No && x.Done)))
            {
                stageDefects[stage.No] = shifts.Sum(s => s.Defects.Where(d => d.StageNo == stage.No).Sum(d => d.QuantityKg));
            }
        }

        var materialDefects = new Dictionary<Guid, decimal>();
        foreach (var m in shifts.SelectMany(s => s.Materials).Where(m => m.Kind == ProductionShiftMaterialKind.Defect))
        {
            var candidates = calc.Lines.Where(l => l.ItemId == m.ItemId).ToList();
            var line = candidates.FirstOrDefault(l => l.StageNo == m.StageNo) ?? candidates.FirstOrDefault(l => calc.Stages.Any(s => s.No == l.StageNo && s.Kind == ProductionStageKind.Packing))
                       ?? candidates.FirstOrDefault();
            if (line is null)
            {
                continue;
            }

            var perLine = ProductionCalc.ToItemUnit(line, 1) is { } f and > 0 ? m.Quantity / f : m.Quantity;
            materialDefects[line.Key] = materialDefects.GetValueOrDefault(line.Key) + perLine;
        }

        var rework = shifts.SelectMany(s => s.Stages).Where(x => x.ReworkKg > 0).GroupBy(x => x.StageNo).ToDictionary(g => g.Key, g => g.Sum(x => x.ReworkKg!.Value));
        var batches = shifts.SelectMany(s => s.Batches.OrderBy(b => b.No)).Select(b => b.MassKg).ToList();
        var packs = shifts.Any(s => s.Packs is not null) ? shifts.Sum(s => s.Packs ?? 0) : (decimal?)null;
        var boxes = shifts.Any(s => s.Boxes is not null) ? shifts.Sum(s => s.Boxes ?? 0) : (decimal?)null;
        return new CalcActual(shifts.Sum(s => s.GoodOutput ?? 0), stageDefects, batches.Count > 0 ? batches : null, packs, boxes, materialDefects, rework);
    }
}

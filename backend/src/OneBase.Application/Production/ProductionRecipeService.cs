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

/// <summary>
/// Строка рецептуры из формы: позиция, норма, потери; этап — ключ этапа из той же формы (у рецептуры без этапов — пусто); база нормы; группа;
/// единица строки (пусто — единица позиции; единицы массы переводятся); масса единицы, кг (для единиц не массы).
/// </summary>
public sealed record ProductionRecipeLineInput(Guid ItemId, decimal Quantity, decimal LossPercent = 0, string? Comment = null, string? StageKey = null,
    ProductionLineBasis Basis = ProductionLineBasis.Output, string? Group = null, string? UnitCode = null, decimal? UnitKg = null);

/// <summary>Этап из формы: ключ (связывает строки и виды брака, не хранится), название, тип, участок, выход и допуск, база брака, влажность, переработка.</summary>
public sealed record ProductionRecipeStageInput(string? Key, string? Name, ProductionStageKind Kind = ProductionStageKind.Other, string? Area = null, decimal YieldPercent = 100,
    decimal? YieldMinPercent = null, decimal? YieldMaxPercent = null, ProductionDefectBase DefectBase = ProductionDefectBase.Output, decimal? MoistureNorm = null,
    string? MoistureUnit = null, decimal? MoistureTolerance = null, bool Reworkable = false, string? Comment = null);

/// <summary>Вид брака из формы: этап (ключ), название, норматив %, переработка; ItemId — брак материала этого этапа (иначе — брак продукта).</summary>
public sealed record ProductionDefectTypeInput(string? StageKey, string? Name, decimal NormPercent = 0, bool Reworkable = false, Guid? ItemId = null, string? Comment = null);

/// <summary>
/// Рецептура из формы: код (у новой), название, выход (товар Linko, название, единица, количество), комментарий, строки; Version — версия при
/// правке; карта — номер, технолог, дата начала действия; замес — мин. и макс. масса, частичный; упаковка — масса штуки и упаковки, упаковок в
/// коробке; этапы и виды брака.
/// </summary>
public sealed record ProductionRecipeInput(string? Code, string? Name, long? LinkoProductId, string? OutputName, string? OutputUnit, decimal OutputQuantity, string? Comment,
    IReadOnlyList<ProductionRecipeLineInput>? Lines, uint? Version = null, string? TechCardNo = null, Guid? TechnologistId = null, DateOnly? EffectiveFrom = null,
    decimal? BatchMinKg = null, decimal? BatchMaxKg = null, bool PartialBatches = false, decimal? PieceKg = null, decimal? PackKg = null, int? PacksPerBox = null,
    IReadOnlyList<ProductionRecipeStageInput>? Stages = null, IReadOnlyList<ProductionDefectTypeInput>? DefectTypes = null, Guid? OutputItemId = null);

/// <summary>
/// Строка списка (карточка продукта): рецептура по коду — действующая версия (а нет — последняя); неутверждённая версия (черновик или на
/// утверждении) и её статус; сколько версий; номер карты, технолог, дата начала действия, этапов.
/// </summary>
public sealed record ProductionRecipeRow(Guid Id, string Code, int VersionNo, string Name, string OutputName, string OutputUnit, decimal OutputQuantity,
    ProductionRecipeStatus Status, int Lines, DateTimeOffset? ActivatedAt, Guid? DraftId, int Versions, string? TechCardNo = null, string? TechnologistName = null,
    DateOnly? EffectiveFrom = null, int Stages = 0, ProductionRecipeStatus? DraftStatus = null, ProductionRecipePhotoView? Photo = null,
    DateTimeOffset? UpdatedAt = null);

/// <summary>Сколько карточек в каждом фильтре состояния (с учётом поиска): все, утверждённые, черновики, на утверждении, архив.</summary>
public sealed record ProductionRecipeCounts(int All, int Active, int Draft, int Pending, int Archived);

/// <summary>Список техкарт; CanCreate — ведёт техкарты (создаёт, импортирует, меняет фото).</summary>
public sealed record ProductionRecipeList(IReadOnlyList<ProductionRecipeRow> Items, int Total, bool CanCreate, ProductionRecipeCounts? Counts = null);

/// <summary>Единица закупки позиции: 1 UnitCode = Factor единиц склада («мешок = 50 кг»).</summary>
public sealed record ProductionItemUnit(string UnitCode, decimal Factor);

public sealed record ProductionRecipeLineView(Guid Id, int LineNo, Guid ItemId, string Sku, string Name, string UnitCode, decimal Quantity, decimal LossPercent, string? Comment,
    bool ItemActive, bool TracksBatches, Guid? StageId = null, ProductionLineBasis Basis = ProductionLineBasis.Output, string? Group = null, string? LineUnit = null,
    decimal? UnitKg = null, string? Category = null, IReadOnlyList<ProductionItemUnit>? ItemUnits = null);

/// <summary>Для формы карты: единицы справочника и технологи (право «Производство: рецептуры» по роли; сам технолог — тоже).</summary>
public sealed record ProductionRecipeForm(IReadOnlyList<ProductionUnitOption> Units, IReadOnlyList<ProductionNamed> Technologists);

public sealed record ProductionUnitOption(string Code, string Name);

/// <summary>Этап версии: норматив брака — сумма видов брака продукта этапа.</summary>
public sealed record ProductionRecipeStageView(Guid Id, int StageNo, string Name, ProductionStageKind Kind, string? Area, decimal YieldPercent, decimal? YieldMinPercent,
    decimal? YieldMaxPercent, ProductionDefectBase DefectBase, decimal DefectPercent, decimal? MoistureNorm, string? MoistureUnit, decimal? MoistureTolerance, bool Reworkable,
    string? Comment);

public sealed record ProductionDefectTypeView(Guid Id, Guid StageId, string Name, decimal NormPercent, bool Reworkable, Guid? ItemId, string? ItemName, string? Comment);

public sealed record ProductionRecipeVersionView(Guid Id, int VersionNo, ProductionRecipeStatus Status, DateTimeOffset? ActivatedAt, string? ActivatedByName, DateTimeOffset CreatedAt);

/// <summary>
/// Что можно с версией: править (черновик), на утверждение (черновик), утвердить и вернуть (на утверждении — другой технолог или начальник
/// производства; вернуть — и автор), новая версия (нет неутверждённой), архивировать (действующую), задание на выпуск.
/// </summary>
public sealed record ProductionRecipeAbilities(bool Edit, bool Submit, bool Approve, bool Return, bool NewVersion, bool Archive, bool Order, bool Photo = false);

public sealed record ProductionRecipeDetail(
    Guid Id,
    string Code,
    int VersionNo,
    string Name,
    long? LinkoProductId,
    string? LinkoProductName,
    string OutputName,
    string OutputUnit,
    decimal OutputQuantity,
    ProductionRecipeStatus Status,
    string? Comment,
    DateTimeOffset? ActivatedAt,
    string? ActivatedByName,
    DateTimeOffset CreatedAt,
    uint Version,
    IReadOnlyList<ProductionRecipeLineView> Lines,
    IReadOnlyList<ProductionRecipeVersionView> Versions,
    IReadOnlyList<ProcurementEventView> History,
    ProductionRecipeAbilities Can,
    int Orders,
    string? TechCardNo = null,
    Guid? TechnologistId = null,
    string? TechnologistName = null,
    DateOnly? EffectiveFrom = null,
    DateTimeOffset? SubmittedAt = null,
    string? SubmittedByName = null,
    decimal? BatchMinKg = null,
    decimal? BatchMaxKg = null,
    bool PartialBatches = false,
    decimal? PieceKg = null,
    decimal? PackKg = null,
    int? PacksPerBox = null,
    IReadOnlyList<ProductionRecipeStageView>? Stages = null,
    IReadOnlyList<ProductionDefectTypeView>? DefectTypes = null,
    IReadOnlyList<string>? Problems = null,
    decimal? StandardBatchKg = null,
    string? ProductCode = null,
    string? ProductGroup = null,
    IReadOnlyList<string>? Approvers = null,
    Guid? OutputItemId = null,
    string? OutputItemName = null,
    string? OutputItemUnit = null,
    ProductionRecipePhotoView? Photo = null);

/// <summary>Потребность на количество выхода: по каждой позиции — норма на выход рецептуры, потери, сколько нужно, доступно на складе и не хватает.</summary>
public sealed record ProductionRequirementLine(Guid ItemId, string Sku, string Name, string UnitCode, decimal Norm, decimal LossPercent, decimal Required, decimal Available,
    decimal Shortage);

/// <summary>Потребность и полный плановый расчёт (этапы, замесы, компоненты по этапам).</summary>
public sealed record ProductionRequirement(Guid RecipeId, decimal Quantity, string OutputUnit, Guid? WarehouseId, string WarehouseName,
    IReadOnlyList<ProductionRequirementLine> Lines, CalcResult? Calculation = null);

/// <summary>Отличие версий: раздел (карта, этап, компонент, брак), что (этап, позиция), поле, было и стало (null — нет в версии).</summary>
public sealed record ProductionRecipeChange(string Section, string Subject, string Field, string? Before, string? After);

public sealed record ProductionRecipeComparison(Guid LeftId, int LeftVersion, Guid RightId, int RightVersion, IReadOnlyList<ProductionRecipeChange> Changes);

/// <summary>Товар Linko для выхода рецептуры (справочник только для чтения).</summary>
public sealed record ProductionProductOption(long Id, string Name, string? Code, string? Unit);

/// <summary>Действующая рецептура для формы задания: выход и единица.</summary>
public sealed record ProductionRecipeOption(Guid Id, string Code, int VersionNo, string Name, string OutputName, string OutputUnit, decimal OutputQuantity);

/// <summary>
/// Рецептуры и технологические карты (технолог): этапы цепочки (тип, выход, база брака, влажность, участок), компоненты по этапам (норма на
/// выход рецептуры, на замес, на штуку, упаковку, коробку; потери; единица строки), виды брака продукта и материалов, замес и упаковка.
/// Версии: черновик правится свободно; «на утверждение» — только карта без ошибок расчёта; утверждает другой технолог или начальник
/// производства (руководитель отдела «Производство»), не автор; утверждённая — действующая, прежняя уходит в архив; утверждённая и
/// архивная не правятся — «новая версия» копирует версию в черновик (одна неутверждённая на код). Задание закрепляет версию. Видят
/// производство, технолог, кладовщик, снабжение, директор; правит — технолог (право «Производство: рецептуры»). Каждое изменение — в истории.
/// </summary>
public sealed class ProductionRecipeService(IAppDbContext db, TimeProvider clock, UserNotifier notifier)
{
    public const int MaxDefectTypes = 200;

    private static void RequireView(ProcurementContext ctx)
    {
        if (!ctx.SeesProduction)
        {
            throw new FieldForbiddenException("Рецептуры видят производство, технолог, кладовщик, снабжение и директор.");
        }
    }

    private static void RequireEdit(ProcurementContext ctx)
    {
        if (!ctx.Recipes)
        {
            throw new FieldForbiddenException("Рецептуры ведёт технолог — нужно право «Производство: рецептуры».");
        }
    }

    public async Task<ProductionRecipeList> ListAsync(ProcurementContext ctx, string? search, string? status, CancellationToken ct)
    {
        RequireView(ctx);
        var all = await db.ProductionRecipes.AsNoTracking()
            .Select(r => new
            {
                r.Id, r.Code, r.VersionNo, r.Name, r.OutputName, r.OutputUnit, r.OutputQuantity, r.Status, r.ActivatedAt, Lines = r.Lines.Count, r.TechCardNo,
                Technologist = r.Technologist == null ? null : r.Technologist.FullName, r.EffectiveFrom, Stages = r.Stages.Count, Changed = r.UpdatedAt ?? r.CreatedAt,
            })
            .ToListAsync(ct);
        var photos = await db.ProductionRecipePhotos.AsNoTracking().ToDictionaryAsync(p => p.Code, ProductionRecipePhotoService.View, ct);
        var rows = all.GroupBy(r => r.Code).Select(g =>
        {
            var shown = g.FirstOrDefault(r => r.Status == ProductionRecipeStatus.Active) ?? g.OrderByDescending(r => r.VersionNo).First();
            var open = g.FirstOrDefault(r => ProductionRules.Open(r.Status));
            return new ProductionRecipeRow(shown.Id, shown.Code, shown.VersionNo, shown.Name, shown.OutputName, shown.OutputUnit, shown.OutputQuantity, shown.Status, shown.Lines,
                shown.ActivatedAt, open?.Id, g.Count(), shown.TechCardNo, shown.Technologist, shown.EffectiveFrom, shown.Stages, open?.Status,
                photos.GetValueOrDefault(shown.Code), g.Max(r => r.Changed));
        });
        if (search?.Trim().ToLowerInvariant() is { Length: > 0 } text)
        {
            rows = rows.Where(r => r.Code.Contains(text, StringComparison.OrdinalIgnoreCase) || r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                                                                                           || r.OutputName.Contains(text, StringComparison.OrdinalIgnoreCase)
                                                                                           || (r.TechCardNo?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var found = rows.ToList();
        var counts = new ProductionRecipeCounts(found.Count, found.Count(r => r.Status == ProductionRecipeStatus.Active),
            found.Count(r => r.DraftStatus == ProductionRecipeStatus.Draft), found.Count(r => r.DraftStatus == ProductionRecipeStatus.PendingApproval),
            found.Count(r => r.Status == ProductionRecipeStatus.Archived));
        rows = status switch
        {
            "active" => found.Where(r => r.Status == ProductionRecipeStatus.Active),
            "draft" => found.Where(r => r.DraftStatus == ProductionRecipeStatus.Draft),
            "pending" => found.Where(r => r.DraftStatus == ProductionRecipeStatus.PendingApproval),
            "archived" => found.Where(r => r.Status == ProductionRecipeStatus.Archived),
            _ => found,
        };
        var list = rows.OrderBy(r => r.Status == ProductionRecipeStatus.Archived).ThenBy(r => r.Name).ThenBy(r => r.Code).ToList();
        return new ProductionRecipeList(list, list.Count, ctx.Recipes, counts);
    }

    public async Task<ProductionRecipeDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireView(ctx);
        var r = (await LoadAsync(db, x => x.Id == id, false, ct, q => q.Include(x => x.ActivatedBy).Include(x => x.Technologist).Include(x => x.SubmittedBy).Include(x => x.OutputItem)))
                .FirstOrDefault()
                ?? throw new FieldNotFoundException("Рецептура не найдена.");
        var versions = await db.ProductionRecipes.AsNoTracking().Where(x => x.Code == r.Code).OrderByDescending(x => x.VersionNo)
            .Select(x => new ProductionRecipeVersionView(x.Id, x.VersionNo, x.Status, x.ActivatedAt, x.ActivatedBy == null ? null : x.ActivatedBy.FullName, x.CreatedAt))
            .ToListAsync(ct);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == ProductionRules.RecipeDoc && e.DocId == id).OrderBy(e => e.CreatedAt)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var product = r.LinkoProductId is { } pid
            ? await db.LinkoProducts.AsNoTracking().Where(p => p.Id == pid).Select(p => new { p.Name, p.Code, p.TypeId }).FirstOrDefaultAsync(ct)
            : null;
        var group = product?.TypeId is { } tid ? await db.LinkoProductTypes.AsNoTracking().Where(t => t.Id == tid).Select(t => t.Name).FirstOrDefaultAsync(ct) : null;
        var categories = await db.ProcurementCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var orders = await db.ProductionOrders.CountAsync(o => o.RecipeId == id, ct);
        var hasOpen = versions.Any(v => ProductionRules.Open(v.Status));
        var stages = r.Stages.OrderBy(s => s.StageNo).ToList();
        var lines = r.Lines.OrderBy(l => l.LineNo).Select(l => new ProductionRecipeLineView(l.Id, l.LineNo, l.ItemId, l.Item!.Sku, l.Item.Name, l.Item.UnitCode, l.Quantity,
            l.LossPercent, l.Comment, l.Item.IsActive, l.Item.TracksBatches, l.StageId, l.Basis, l.GroupName, l.UnitCode ?? l.Item.UnitCode, l.UnitKg,
            categories.GetValueOrDefault(l.Item.CategoryId), l.Item.Units.Select(u => new ProductionItemUnit(u.UnitCode, u.Factor)).ToList())).ToList();
        var calc = ProductionRecipeCalc.From(r);
        var problems = ProductionCalc.Check(calc);
        var mixing = stages.FirstOrDefault(s => s.Kind == ProductionStageKind.Mixing);
        var approvers = r.Status == ProductionRecipeStatus.PendingApproval ? await ApproverNamesAsync(r.SubmittedById, ct) : [];
        var approver = await ApproverAsync(ctx, ct);
        var can = new ProductionRecipeAbilities(
            ctx.Recipes && r.Status == ProductionRecipeStatus.Draft,
            ctx.Recipes && r.Status == ProductionRecipeStatus.Draft && lines.Count > 0 && problems.Count == 0,
            approver && r.Status == ProductionRecipeStatus.PendingApproval && r.SubmittedById != ctx.UserId,
            r.Status == ProductionRecipeStatus.PendingApproval && ((approver && r.SubmittedById != ctx.UserId) || (ctx.Recipes && r.SubmittedById == ctx.UserId)),
            ctx.Recipes && !ProductionRules.Open(r.Status) && !hasOpen,
            ctx.Recipes && r.Status == ProductionRecipeStatus.Active,
            ctx.Produces && r.Status == ProductionRecipeStatus.Active,
            ctx.Recipes);
        var photo = await db.ProductionRecipePhotos.AsNoTracking().FirstOrDefaultAsync(p => p.Code == r.Code, ct);
        return new ProductionRecipeDetail(r.Id, r.Code, r.VersionNo, r.Name, r.LinkoProductId, product?.Name, r.OutputName, r.OutputUnit, r.OutputQuantity, r.Status, r.Comment,
            r.ActivatedAt, r.ActivatedBy?.FullName, r.CreatedAt, r.Version, lines, versions, history, can, orders, r.TechCardNo, r.TechnologistId, r.Technologist?.FullName,
            r.EffectiveFrom, r.SubmittedAt, r.SubmittedBy?.FullName, r.BatchMinKg, r.BatchMaxKg, r.PartialBatches, r.PieceKg, r.PackKg, r.PacksPerBox,
            stages.Select(s => new ProductionRecipeStageView(s.Id, s.StageNo, s.Name, s.Kind, s.Area, s.YieldPercent, s.YieldMinPercent, s.YieldMaxPercent, s.DefectBase,
                r.DefectTypes.Where(d => d.StageId == s.Id && d.ItemId == null).Sum(d => d.NormPercent), s.MoistureNorm, s.MoistureUnit, s.MoistureTolerance, s.Reworkable,
                s.Comment)).ToList(),
            r.DefectTypes.OrderBy(d => stages.FindIndex(s => s.Id == d.StageId)).ThenBy(d => d.ItemId != null).ThenBy(d => d.Name)
                .Select(d => new ProductionDefectTypeView(d.Id, d.StageId, d.Name, d.NormPercent, d.Reworkable, d.ItemId, d.Item?.Name, d.Comment)).ToList(),
            problems, mixing is null ? null : calc.Lines.Where(l => l.StageNo == mixing.StageNo).Sum(l => l.Quantity * (ProductionCalc.MassFactor(l.Unit) ?? l.UnitKg ?? 0)),
            product?.Code, group, approvers, r.OutputItemId, r.OutputItem?.Name, r.OutputItem?.UnitCode, photo is null ? null : ProductionRecipePhotoService.View(photo));
    }

    /// <summary>Новая рецептура — черновик версии 1 с новым кодом.</summary>
    public async Task<Guid> CreateAsync(ProcurementContext ctx, ProductionRecipeInput input, CancellationToken ct)
    {
        RequireEdit(ctx);
        var code = input.Code?.Trim();
        if (!ProductionRules.ValidCode(code))
        {
            throw new FieldValidationException("Код рецептуры — до 32 знаков: буквы, цифры, «-», «_», «.».");
        }

        if (await db.ProductionRecipes.AnyAsync(r => r.Code == code, ct))
        {
            throw new FieldConflictException($"Рецептура с кодом «{code}» уже есть — откройте её и создайте новую версию.");
        }

        var recipe = new ProductionRecipe { Code = code!, Name = "", OutputName = "", OutputUnit = "", CreatedById = ctx.UserId };
        await ApplyAsync(recipe, input, ct);
        db.ProductionRecipes.Add(recipe);
        Log(ctx, recipe.Id, "created", null, ProductionRecipeStatus.Draft,
            $"Создана рецептура {recipe.Code}, версия 1: {recipe.Name} — {Lines(recipe.Lines.Count)}{Stages(recipe.Stages.Count)} на {ProcurementRules.Quantity(recipe.OutputQuantity)} {recipe.OutputUnit}");
        await db.SaveChangesAsync(ct);
        return recipe.Id;
    }

    /// <summary>Правка черновика (версия — xmin: изменили в другом окне — 409).</summary>
    public async Task UpdateAsync(ProcurementContext ctx, Guid id, ProductionRecipeInput input, CancellationToken ct)
    {
        RequireEdit(ctx);
        var recipe = (await LoadAsync(db, r => r.Id == id, true, ct)).FirstOrDefault() ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (recipe.Status != ProductionRecipeStatus.Draft)
        {
            throw new FieldConflictException(recipe.Status == ProductionRecipeStatus.PendingApproval
                ? "Версия на утверждении не правится — верните её в черновик."
                : "Утверждённую и архивную версию не правят — создайте новую версию.");
        }

        if (input.Version is { } version && version != recipe.Version)
        {
            throw new FieldConflictException("Рецептуру уже изменили в другом окне — обновите страницу.");
        }

        var before = Snapshot(recipe, recipe.Lines.Where(l => l.Item is not null).Select(l => l.Item!).DistinctBy(i => i.Id).ToDictionary(i => i.Id));
        var items = await ApplyAsync(recipe, input, ct);
        recipe.UpdatedAt = DateTimeOffset.UtcNow;
        var after = Snapshot(recipe, items);
        if (before != after)
        {
            Log(ctx, recipe.Id, "updated", null, null, $"Изменён черновик версии {recipe.VersionNo}", after);
        }

        await SaveAsync(ct);
    }

    /// <summary>
    /// На утверждение: черновик с позициями, без ошибок расчёта и без отключённых позиций. Утвердить могут другие технологи и начальник
    /// производства — им уведомление.
    /// </summary>
    public async Task SubmitAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireEdit(ctx);
        var recipe = (await LoadAsync(db, r => r.Id == id, true, ct)).FirstOrDefault() ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (recipe.Status != ProductionRecipeStatus.Draft)
        {
            throw new FieldConflictException("На утверждение отправляют черновик.");
        }

        Ready(recipe);
        recipe.Status = ProductionRecipeStatus.PendingApproval;
        recipe.SubmittedAt = DateTimeOffset.UtcNow;
        recipe.SubmittedById = ctx.UserId;
        recipe.UpdatedAt = recipe.SubmittedAt;
        Log(ctx, recipe.Id, "submitted", ProductionRecipeStatus.Draft, ProductionRecipeStatus.PendingApproval, $"Версия {recipe.VersionNo} отправлена на утверждение");
        foreach (var user in (await ApproverIdsAsync(ctx.UserId, ct)).Take(50))
        {
            notifier.Notify(user, UserNotificationKind.ProcurementStatus, $"Рецептура {recipe.Code} ждёт утверждения",
                $"{recipe.Name}, версия {recipe.VersionNo}", $"/production/recipes/{recipe.Id}");
        }

        await SaveAsync(ct);
    }

    /// <summary>
    /// Утвердить версию на утверждении: другой технолог или начальник производства, не отправивший её. Версия становится действующей, прежняя
    /// действующая того же кода уходит в архив; дата начала действия — указанная или сегодня.
    /// </summary>
    public async Task ApproveAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        var recipe = (await LoadAsync(db, r => r.Id == id, true, ct)).FirstOrDefault() ?? throw new FieldNotFoundException("Рецептура не найдена.");
        await RequireApproverAsync(ctx, recipe, ct);
        if (recipe.Status != ProductionRecipeStatus.PendingApproval)
        {
            throw new FieldConflictException("Утверждают версию на утверждении.");
        }

        Ready(recipe);
        var current = await db.ProductionRecipes.FirstOrDefaultAsync(r => r.Code == recipe.Code && r.Status == ProductionRecipeStatus.Active, ct);
        if (current is not null)
        {
            current.Status = ProductionRecipeStatus.Archived;
            current.UpdatedAt = DateTimeOffset.UtcNow;
            Log(ctx, current.Id, "archived", ProductionRecipeStatus.Active, ProductionRecipeStatus.Archived,
                $"Версия {current.VersionNo} ушла в архив: утверждена версия {recipe.VersionNo}");
            // Сначала архив, затем новая действующая: уникальный индекс «одна действующая на код» не должен видеть двух сразу.
            await SaveAsync(ct);
        }

        recipe.Status = ProductionRecipeStatus.Active;
        recipe.ActivatedAt = DateTimeOffset.UtcNow;
        recipe.ActivatedById = ctx.UserId;
        recipe.EffectiveFrom ??= ProcurementBatchRules.Today(clock);
        Log(ctx, recipe.Id, "activated", ProductionRecipeStatus.PendingApproval, ProductionRecipeStatus.Active,
            $"Версия {recipe.VersionNo} утверждена, действует с {recipe.EffectiveFrom:dd.MM.yyyy}");
        notifier.Notify(recipe.SubmittedById, UserNotificationKind.ProcurementStatus, $"Рецептура {recipe.Code} утверждена", $"{recipe.Name}, версия {recipe.VersionNo}",
            $"/production/recipes/{recipe.Id}");
        await SaveAsync(ct);
    }

    /// <summary>Вернуть в черновик: утверждающий — с причиной (автору уведомление); автор — отозвать, причина по желанию.</summary>
    public async Task ReturnAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct)
    {
        var recipe = await db.ProductionRecipes.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (recipe.Status != ProductionRecipeStatus.PendingApproval)
        {
            throw new FieldConflictException("Вернуть можно версию на утверждении.");
        }

        var reason = Clean(comment, 1000);
        var own = recipe.SubmittedById == ctx.UserId;
        if (own)
        {
            RequireEdit(ctx);
        }
        else
        {
            await RequireApproverAsync(ctx, recipe, ct);
            if (reason is null)
            {
                throw new FieldValidationException("Напишите, что исправить в версии.");
            }
        }

        recipe.Status = ProductionRecipeStatus.Draft;
        recipe.UpdatedAt = DateTimeOffset.UtcNow;
        Log(ctx, recipe.Id, "returned", ProductionRecipeStatus.PendingApproval, ProductionRecipeStatus.Draft,
            own ? $"Версия {recipe.VersionNo} отозвана с утверждения" : $"Версия {recipe.VersionNo} возвращена в черновик", reason);
        if (!own)
        {
            notifier.Notify(recipe.SubmittedById, UserNotificationKind.ProcurementStatus, $"Рецептура {recipe.Code} возвращена на доработку", reason,
                $"/production/recipes/{recipe.Id}");
        }

        await SaveAsync(ct);
    }

    /// <summary>
    /// Новая версия — копия этой (утверждённой или архивной) в черновик с номером на один больше последнего: карта, этапы, компоненты, виды брака;
    /// дата начала действия — пустая. Неутверждённая версия у кода одна.
    /// </summary>
    public async Task<Guid> NewVersionAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireEdit(ctx);
        var source = (await LoadAsync(db, r => r.Id == id, false, ct)).FirstOrDefault() ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (await db.ProductionRecipes.AsNoTracking().FirstOrDefaultAsync(r => r.Code == source.Code && ProductionRules.OpenStatuses.Contains(r.Status), ct) is { } open)
        {
            throw new FieldConflictException($"У рецептуры {source.Code} уже есть {(open.Status == ProductionRecipeStatus.Draft ? "черновик" : "версия на утверждении")} — версия {open.VersionNo}.");
        }

        var next = await db.ProductionRecipes.Where(r => r.Code == source.Code).MaxAsync(r => r.VersionNo, ct) + 1;
        var copy = new ProductionRecipe
        {
            Code = source.Code,
            VersionNo = next,
            Name = source.Name,
            LinkoProductId = source.LinkoProductId,
            OutputName = source.OutputName,
            OutputUnit = source.OutputUnit,
            OutputQuantity = source.OutputQuantity,
            Comment = source.Comment,
            CreatedById = ctx.UserId,
            TechCardNo = source.TechCardNo,
            TechnologistId = source.TechnologistId,
            BatchMinKg = source.BatchMinKg,
            BatchMaxKg = source.BatchMaxKg,
            PartialBatches = source.PartialBatches,
            PieceKg = source.PieceKg,
            PackKg = source.PackKg,
            PacksPerBox = source.PacksPerBox,
            OutputItemId = source.OutputItemId,
        };
        var stages = new Dictionary<Guid, ProductionRecipeStage>();
        foreach (var s in source.Stages.OrderBy(s => s.StageNo))
        {
            var stage = new ProductionRecipeStage
            {
                RecipeId = copy.Id, StageNo = s.StageNo, Name = s.Name, Kind = s.Kind, Area = s.Area, YieldPercent = s.YieldPercent, YieldMinPercent = s.YieldMinPercent,
                YieldMaxPercent = s.YieldMaxPercent, DefectBase = s.DefectBase, MoistureNorm = s.MoistureNorm, MoistureUnit = s.MoistureUnit,
                MoistureTolerance = s.MoistureTolerance, Reworkable = s.Reworkable, Comment = s.Comment,
            };
            stages[s.Id] = stage;
            copy.Stages.Add(stage);
        }

        foreach (var line in source.Lines.OrderBy(l => l.LineNo))
        {
            copy.Lines.Add(new ProductionRecipeLine
            {
                RecipeId = copy.Id, LineNo = line.LineNo, ItemId = line.ItemId, Quantity = line.Quantity, LossPercent = line.LossPercent, Comment = line.Comment,
                StageId = line.StageId is { } sid ? stages[sid].Id : null, Basis = line.Basis, GroupName = line.GroupName, UnitCode = line.UnitCode, UnitKg = line.UnitKg,
            });
        }

        foreach (var d in source.DefectTypes)
        {
            copy.DefectTypes.Add(new ProductionDefectType
            {
                RecipeId = copy.Id, StageId = stages[d.StageId].Id, Name = d.Name, ItemId = d.ItemId, NormPercent = d.NormPercent, Reworkable = d.Reworkable, Comment = d.Comment,
            });
        }

        db.ProductionRecipes.Add(copy);
        Log(ctx, copy.Id, "created", null, ProductionRecipeStatus.Draft, $"Создана версия {next} — копия версии {source.VersionNo}");
        await SaveAsync(ct);
        return copy.Id;
    }

    /// <summary>
    /// Новая версия с другим составом (импорт из Excel): черновик с номером на один больше последнего по данным формы — проверки и история как у
    /// правки черновика; одно сохранение — либо версия создана целиком, либо нет. Неутверждённая версия у кода одна.
    /// </summary>
    internal async Task<Guid> NewVersionFromAsync(ProcurementContext ctx, Guid sourceId, ProductionRecipeInput input, string note, CancellationToken ct)
    {
        RequireEdit(ctx);
        var source = await db.ProductionRecipes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == sourceId, ct) ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (await db.ProductionRecipes.AsNoTracking().FirstOrDefaultAsync(r => r.Code == source.Code && ProductionRules.OpenStatuses.Contains(r.Status), ct) is { } open)
        {
            throw new FieldConflictException($"У рецептуры {source.Code} уже есть {(open.Status == ProductionRecipeStatus.Draft ? "черновик" : "версия на утверждении")} — версия {open.VersionNo}.");
        }

        var next = await db.ProductionRecipes.Where(r => r.Code == source.Code).MaxAsync(r => r.VersionNo, ct) + 1;
        var copy = new ProductionRecipe { Code = source.Code, VersionNo = next, Name = "", OutputName = "", OutputUnit = "", CreatedById = ctx.UserId };
        var items = await ApplyAsync(copy, input, ct);
        db.ProductionRecipes.Add(copy);
        Log(ctx, copy.Id, "created", null, ProductionRecipeStatus.Draft, $"Создана версия {next} ({note}) — на основе версии {source.VersionNo}", Snapshot(copy, items));
        await SaveAsync(ct);
        return copy.Id;
    }

    /// <summary>Архивировать действующую версию: по коду не останется действующей — новые задания по ней не создать.</summary>
    public async Task ArchiveAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct)
    {
        RequireEdit(ctx);
        var recipe = await db.ProductionRecipes.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (recipe.Status != ProductionRecipeStatus.Active)
        {
            throw new FieldConflictException("В архив отправляют действующую версию.");
        }

        recipe.Status = ProductionRecipeStatus.Archived;
        recipe.UpdatedAt = DateTimeOffset.UtcNow;
        Log(ctx, recipe.Id, "archived", ProductionRecipeStatus.Active, ProductionRecipeStatus.Archived, $"Версия {recipe.VersionNo} отправлена в архив", Clean(comment, 1000));
        await SaveAsync(ct);
    }

    /// <summary>Сравнение двух версий одного кода: карта, этапы (по названию), компоненты (этап + позиция), виды брака (этап + название).</summary>
    public async Task<ProductionRecipeComparison> CompareAsync(ProcurementContext ctx, Guid leftId, Guid rightId, CancellationToken ct)
    {
        RequireView(ctx);
        var pair = await LoadAsync(db, r => r.Id == leftId || r.Id == rightId, false, ct, q => q.Include(r => r.Technologist).Include(r => r.OutputItem));
        var left = pair.FirstOrDefault(r => r.Id == leftId) ?? throw new FieldNotFoundException("Версия не найдена.");
        var right = pair.FirstOrDefault(r => r.Id == rightId) ?? throw new FieldNotFoundException("Версия не найдена.");
        if (left.Code != right.Code)
        {
            throw new FieldValidationException("Сравнивают версии одной рецептуры.");
        }

        return new ProductionRecipeComparison(left.Id, left.VersionNo, right.Id, right.VersionNo, ProductionRecipeDiff.Compare(left, right));
    }

    /// <summary>
    /// Потребность на quantity выхода — плановый расчёт по цепочке (этапы, замесы, компоненты) и доступно (годное и свободное) на складе
    /// warehouseId (не указан — на всех складах).
    /// </summary>
    public async Task<ProductionRequirement> RequirementAsync(ProcurementContext ctx, Guid id, decimal quantity, Guid? warehouseId, CancellationToken ct)
    {
        RequireView(ctx);
        if (!ProductionRules.ValidQuantity(quantity, 3))
        {
            throw new FieldValidationException("Количество выхода — больше нуля, не больше трёх знаков после запятой.");
        }

        var r = (await LoadAsync(db, x => x.Id == id, false, ct)).FirstOrDefault() ?? throw new FieldNotFoundException("Рецептура не найдена.");
        var name = warehouseId is { } wid
            ? await db.ProcurementWarehouses.AsNoTracking().Where(w => w.Id == wid).Select(w => w.Name).FirstOrDefaultAsync(ct) ?? throw new FieldNotFoundException("Склад не найден.")
            : "все склады";
        var calc = ProductionCalc.Plan(ProductionRecipeCalc.From(r), quantity);
        var available = await ProductionStock.AvailableAsync(db, r.Lines.Select(l => l.ItemId), warehouseId, ProcurementBatchRules.Today(clock), ct);
        var lines = ProductionRecipeCalc.OrderLines(r, calc).Select(l =>
        {
            var item = r.Lines.First(x => x.ItemId == l.ItemId).Item!;
            var free = available.GetValueOrDefault(l.ItemId);
            return new ProductionRequirementLine(l.ItemId, item.Sku, item.Name, item.UnitCode, l.Norm, l.LossPercent, l.Required, free, Math.Max(0, l.Required - free));
        }).ToList();
        return new ProductionRequirement(r.Id, quantity, r.OutputUnit, warehouseId, name, lines, calc);
    }

    /// <summary>Форма карты: единицы справочника и технологи (по роли, не администратор; спрашивающий технолог — тоже).</summary>
    public async Task<ProductionRecipeForm> FormAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireEdit(ctx);
        var units = await db.ProcurementUnits.AsNoTracking().OrderBy(u => u.SortOrder).Select(u => new ProductionUnitOption(u.Code, u.Name)).ToListAsync(ct);
        var ids = (await ProcurementPeople.WithPermissionAsync(db, Permissions.ProductionRecipes, ct)).Append(ctx.UserId).Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).OrderBy(u => u.FullName).Select(u => new ProductionNamed(u.Id, u.FullName))
            .ToListAsync(ct);
        return new ProductionRecipeForm(units, people);
    }

    /// <summary>Действующие рецептуры — для формы задания на выпуск.</summary>
    public async Task<IReadOnlyList<ProductionRecipeOption>> OptionsAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireView(ctx);
        return await db.ProductionRecipes.AsNoTracking().Where(r => r.Status == ProductionRecipeStatus.Active).OrderBy(r => r.Name)
            .Select(r => new ProductionRecipeOption(r.Id, r.Code, r.VersionNo, r.Name, r.OutputName, r.OutputUnit, r.OutputQuantity)).ToListAsync(ct);
    }

    /// <summary>Товары Linko для выхода рецептуры: поиск по названию или коду, до 20.</summary>
    public async Task<IReadOnlyList<ProductionProductOption>> ProductsAsync(ProcurementContext ctx, string? search, CancellationToken ct)
    {
        RequireView(ctx);
        var q = db.LinkoProducts.AsNoTracking();
        if (search?.Trim().ToLower() is { Length: > 0 } text)
        {
            q = q.Where(p => p.Name.ToLower().Contains(text) || (p.Code != null && p.Code.ToLower().Contains(text)));
        }

        return await q.OrderBy(p => p.Name).Take(20).Select(p => new ProductionProductOption(p.Id, p.Name, p.Code, p.Measurement)).ToListAsync(ct);
    }

    /// <summary>
    /// Рецептуры со всем, что нужно расчёту: строки с позициями, этапы, виды брака (с материалом) — тремя запросами, без декартова произведения
    /// строк на этапы и брак. track — для правки (связи этапов и брака подставит отслеживание), иначе — заполняются здесь.
    /// </summary>
    internal static async Task<List<ProductionRecipe>> LoadAsync(IAppDbContext db, System.Linq.Expressions.Expression<Func<ProductionRecipe, bool>> where, bool track,
        CancellationToken ct, Func<IQueryable<ProductionRecipe>, IQueryable<ProductionRecipe>>? include = null)
    {
        IQueryable<ProductionRecipe> q = track ? db.ProductionRecipes : db.ProductionRecipes.AsNoTracking();
        q = q.Include(r => r.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.Units);
        var recipes = await (include is null ? q : include(q)).Where(where).ToListAsync(ct);
        var ids = recipes.Select(r => r.Id).ToList();
        var stages = await (track ? db.ProductionRecipeStages : db.ProductionRecipeStages.AsNoTracking()).Where(s => ids.Contains(s.RecipeId)).ToListAsync(ct);
        var defects = await (track ? db.ProductionDefectTypes : db.ProductionDefectTypes.AsNoTracking()).Include(d => d.Item).Where(d => ids.Contains(d.RecipeId))
            .ToListAsync(ct);
        if (!track)
        {
            foreach (var r in recipes)
            {
                r.Stages = stages.Where(s => s.RecipeId == r.Id).ToList();
                r.DefectTypes = defects.Where(d => d.RecipeId == r.Id).ToList();
            }
        }

        return recipes;
    }

    /// <summary>Готова к утверждению: есть позиции, все включены в каталоге, карта считается без ошибок.</summary>
    private static void Ready(ProductionRecipe recipe)
    {
        if (recipe.Lines.Count == 0)
        {
            throw new FieldValidationException("Добавьте в рецептуру хотя бы одну позицию.");
        }

        if (recipe.Lines.FirstOrDefault(l => !l.Item!.IsActive) is { } off)
        {
            throw new FieldConflictException($"«{off.Item!.Name}» отключена в каталоге склада — замените её в рецептуре.");
        }

        if (ProductionCalc.Check(ProductionRecipeCalc.From(recipe)) is { Count: > 0 } problems)
        {
            throw new FieldValidationException($"Карта не считается: {string.Join(" ", problems.Take(3))}{(problems.Count > 3 ? $" И ещё {problems.Count - 3}." : "")}");
        }
    }

    /// <summary>Утверждает: технолог (право «Производство: рецептуры») или начальник производства (руководитель отдела «Производство»).</summary>
    private async Task<bool> ApproverAsync(ProcurementContext ctx, CancellationToken ct) =>
        ctx.Recipes || (ctx.HeadOf.Count > 0 && await db.Departments.AnyAsync(d => d.Code == ProductionOrderService.ProductionDepartment && ctx.HeadOf.Contains(d.Id), ct));

    private async Task RequireApproverAsync(ProcurementContext ctx, ProductionRecipe recipe, CancellationToken ct)
    {
        if (!await ApproverAsync(ctx, ct))
        {
            throw new FieldForbiddenException("Утверждает технолог или начальник производства.");
        }

        if (recipe.SubmittedById == ctx.UserId && recipe.Status == ProductionRecipeStatus.PendingApproval)
        {
            throw new FieldForbiddenException("Свою версию не утверждают — её утверждает другой технолог или начальник производства.");
        }
    }

    /// <summary>Кто может утвердить (кроме автора): технологи по праву роли (не администратор) и руководители отдела «Производство».</summary>
    private async Task<List<Guid>> ApproverIdsAsync(Guid? author, CancellationToken ct)
    {
        var technologists = await ProcurementPeople.WithPermissionAsync(db, Permissions.ProductionRecipes, ct);
        var heads = await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.Department!.Code == ProductionOrderService.ProductionDepartment && h.User!.IsActive)
            .Select(h => h.UserId).ToListAsync(ct);
        return technologists.Concat(heads).Distinct().Where(u => u != author).ToList();
    }

    private async Task<IReadOnlyList<string>> ApproverNamesAsync(Guid? author, CancellationToken ct)
    {
        var ids = await ApproverIdsAsync(author, ct);
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).OrderBy(u => u.FullName).Select(u => u.FullName).ToListAsync(ct);
    }

    private async Task<Dictionary<Guid, ProcurementItem>> ApplyAsync(ProductionRecipe recipe, ProductionRecipeInput input, CancellationToken ct)
    {
        var name = Clean(input.Name, 200) ?? throw new FieldValidationException("Укажите название рецептуры.");
        var unit = input.OutputUnit?.Trim();
        var units = await db.ProcurementUnits.AsNoTracking().Select(u => u.Code).ToListAsync(ct);
        if (string.IsNullOrEmpty(unit) || !units.Contains(unit))
        {
            throw new FieldValidationException("Выберите единицу выхода из справочника.");
        }

        if (!ProductionRules.ValidQuantity(input.OutputQuantity, 3))
        {
            throw new FieldValidationException("Выход рецептуры — больше нуля, не больше трёх знаков после запятой.");
        }

        if (input.LinkoProductId is { } pid && !await db.LinkoProducts.AnyAsync(p => p.Id == pid, ct))
        {
            throw new FieldValidationException("Товар Linko не найден — выберите из списка.");
        }

        if (input.OutputItemId is { } outId)
        {
            var output = await db.ProcurementItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == outId, ct)
                         ?? throw new FieldValidationException("Позиция склада выхода не найдена — выберите из каталога.");
            if (!output.IsActive && recipe.OutputItemId != outId)
            {
                throw new FieldValidationException($"«{output.Name}» отключена в каталоге склада.");
            }

            if (ProductionCalc.Convert(1, unit, output.UnitCode) is null)
            {
                throw new FieldValidationException($"Выход в «{unit}» не переводится в единицу позиции «{output.UnitCode}» — выберите позицию в той же единице или в единице массы.");
            }
        }

        if (input.TechnologistId is { } tech && !await db.Users.AnyAsync(u => u.Id == tech && u.IsActive, ct))
        {
            throw new FieldValidationException("Технолог — действующий пользователь OneBase.");
        }

        Optional(input.BatchMinKg, 3, "Минимальная масса замеса");
        Optional(input.BatchMaxKg, 3, "Максимальная масса замеса");
        if (input.BatchMinKg > input.BatchMaxKg)
        {
            throw new FieldValidationException("Минимальная масса замеса — не больше максимальной.");
        }

        Optional(input.PieceKg, 6, "Масса штуки");
        Optional(input.PackKg, 6, "Масса упаковки");
        if (input.PacksPerBox is { } perBox and (<= 0 or > 1_000_000))
        {
            throw new FieldValidationException("Упаковок в коробке — от 1 до 1 000 000.");
        }

        // Этапы: ключи из формы связывают строки и виды брака; порядок — порядок в списке.
        var stageInputs = input.Stages ?? [];
        if (stageInputs.Count > ProductionCalc.MaxStages)
        {
            throw new FieldValidationException($"Этапов — до {ProductionCalc.MaxStages}.");
        }

        var stages = new Dictionary<string, ProductionRecipeStage>(StringComparer.Ordinal);
        foreach (var (s, no) in stageInputs.Select((s, i) => (s, i + 1)))
        {
            var key = s.Key?.Trim() is { Length: > 0 } k ? k : throw new FieldValidationException($"Этап {no}: нет ключа этапа.");
            var title = Clean(s.Name, 100) ?? throw new FieldValidationException($"Этап {no}: укажите название.");
            if (!Enum.IsDefined(s.Kind) || !Enum.IsDefined(s.DefectBase))
            {
                throw new FieldValidationException($"«{title}»: неизвестный тип операции или база брака.");
            }

            Percent(s.YieldPercent, $"«{title}»: нормативный выход", required: true);
            Percent(s.YieldMinPercent, $"«{title}»: минимальный выход");
            Percent(s.YieldMaxPercent, $"«{title}»: максимальный выход");
            if (s.YieldMinPercent > s.YieldPercent || s.YieldMaxPercent < s.YieldPercent)
            {
                throw new FieldValidationException($"«{title}»: минимальный выход ≤ нормативный ≤ максимальный.");
            }

            if (s.MoistureNorm is < 0 or > 1_000_000 || s.MoistureTolerance is < 0 or > 1_000_000)
            {
                throw new FieldValidationException($"«{title}»: влажность и допуск — не меньше нуля.");
            }

            if (!stages.TryAdd(key, new ProductionRecipeStage
                {
                    RecipeId = recipe.Id, StageNo = no, Name = title, Kind = s.Kind, Area = Clean(s.Area, 100), YieldPercent = s.YieldPercent, YieldMinPercent = s.YieldMinPercent,
                    YieldMaxPercent = s.YieldMaxPercent, DefectBase = s.DefectBase, MoistureNorm = s.MoistureNorm,
                    MoistureUnit = s.MoistureNorm is null ? null : Clean(s.MoistureUnit, 16) ?? "%", MoistureTolerance = s.MoistureTolerance, Reworkable = s.Reworkable,
                    Comment = Clean(s.Comment, 500),
                }))
            {
                throw new FieldValidationException($"Этап {no}: ключ этапа повторяется.");
            }
        }

        var lines = input.Lines ?? [];
        if (lines.Count > ProductionRules.MaxLines)
        {
            throw new FieldValidationException($"Позиций в рецептуре — до {ProductionRules.MaxLines}.");
        }

        var ids = lines.Select(l => l.ItemId).ToList();
        var items = await db.ProcurementItems.AsNoTracking().Include(i => i.Units).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var seen = new HashSet<(string?, Guid)>();
        var newLines = new List<ProductionRecipeLine>();
        foreach (var (line, no) in lines.Select((l, i) => (l, i + 1)))
        {
            var item = items.GetValueOrDefault(line.ItemId) ?? throw new FieldValidationException($"Строка {no}: позиция склада не найдена.");
            if (!item.IsActive && recipe.Lines.All(l => l.ItemId != item.Id))
            {
                throw new FieldValidationException($"«{item.Name}» отключена в каталоге склада.");
            }

            var stageKey = line.StageKey?.Trim() is { Length: > 0 } sk ? sk : null;
            ProductionRecipeStage? stage = null;
            if (stages.Count > 0 && (stageKey is null || !stages.TryGetValue(stageKey, out stage)))
            {
                throw new FieldValidationException($"«{item.Name}»: выберите этап.");
            }

            if (stages.Count == 0 && stageKey is not null)
            {
                throw new FieldValidationException($"«{item.Name}»: этапа нет в рецептуре.");
            }

            if (!seen.Add((stageKey, item.Id)))
            {
                throw new FieldValidationException(stage is null
                    ? $"«{item.Name}» уже есть в рецептуре — одна позиция — одна строка."
                    : $"«{item.Name}» уже есть на этапе «{stage.Name}» — одна позиция — одна строка этапа.");
            }

            if (!ProductionRules.ValidQuantity(line.Quantity, 6))
            {
                throw new FieldValidationException($"«{item.Name}»: норма — больше нуля, не больше шести знаков после запятой.");
            }

            if (line.LossPercent < 0 || line.LossPercent > 100 || decimal.Round(line.LossPercent, 2) != line.LossPercent)
            {
                throw new FieldValidationException($"«{item.Name}»: потери — от 0 до 100 %, не больше двух знаков после запятой.");
            }

            if (!Enum.IsDefined(line.Basis))
            {
                throw new FieldValidationException($"«{item.Name}»: неизвестная база нормы.");
            }

            var lineUnit = line.UnitCode?.Trim() is { Length: > 0 } u ? u : item.UnitCode;
            if (!units.Contains(lineUnit))
            {
                throw new FieldValidationException($"«{item.Name}»: единицы «{lineUnit}» нет в справочнике.");
            }

            if (ProductionCalc.Convert(1, lineUnit, item.UnitCode) is null && item.Units.All(u => u.UnitCode != lineUnit))
            {
                throw new FieldValidationException(
                    $"«{item.Name}»: «{lineUnit}» не переводится в единицу склада «{item.UnitCode}» — переводятся кг, г, т и единицы закупки позиции.");
            }

            Optional(line.UnitKg, 6, $"«{item.Name}»: масса единицы");
            newLines.Add(new ProductionRecipeLine
            {
                RecipeId = recipe.Id, LineNo = no, ItemId = line.ItemId, Quantity = line.Quantity, LossPercent = line.LossPercent, Comment = Clean(line.Comment, 500),
                Stage = stage, StageId = stage?.Id, Basis = line.Basis, GroupName = Clean(line.Group, 64), UnitCode = lineUnit == item.UnitCode ? null : lineUnit,
                UnitKg = ProductionCalc.MassFactor(lineUnit) is null ? line.UnitKg : null,
            });
        }

        var defectInputs = input.DefectTypes ?? [];
        if (defectInputs.Count > MaxDefectTypes)
        {
            throw new FieldValidationException($"Видов брака — до {MaxDefectTypes}.");
        }

        var defects = new List<ProductionDefectType>();
        foreach (var d in defectInputs)
        {
            var title = Clean(d.Name, 100) ?? throw new FieldValidationException("Укажите название вида брака.");
            var stage = d.StageKey?.Trim() is { Length: > 0 } key && stages.TryGetValue(key, out var st)
                ? st
                : throw new FieldValidationException($"Брак «{title}»: выберите этап.");
            if (defects.Any(x => x.Stage == stage && string.Equals(x.Name, title, StringComparison.OrdinalIgnoreCase)))
            {
                throw new FieldValidationException($"Брак «{title}» уже есть на этапе «{stage.Name}».");
            }

            if (d.NormPercent < 0 || d.NormPercent >= 100 || decimal.Round(d.NormPercent, 2) != d.NormPercent)
            {
                throw new FieldValidationException($"Брак «{title}»: норматив — от 0 до 100 % (не включая 100), не больше двух знаков после запятой.");
            }

            if (d.ItemId is { } itemId && !newLines.Any(l => l.Stage == stage && l.ItemId == itemId))
            {
                throw new FieldValidationException($"Брак «{title}»: материала нет на этапе «{stage.Name}».");
            }

            defects.Add(new ProductionDefectType
            {
                RecipeId = recipe.Id, Stage = stage, StageId = stage.Id, Name = title, ItemId = d.ItemId, NormPercent = d.NormPercent, Reworkable = d.Reworkable,
                Comment = Clean(d.Comment, 500),
            });
        }

        foreach (var stage in stages.Values)
        {
            if (defects.Where(d => d.Stage == stage && d.ItemId is null).Sum(d => d.NormPercent) >= 100)
            {
                throw new FieldValidationException($"Этап «{stage.Name}»: сумма нормативов брака продукта — меньше 100 %.");
            }
        }

        recipe.Name = name;
        recipe.LinkoProductId = input.LinkoProductId;
        recipe.OutputName = Clean(input.OutputName, 200) ?? name;
        recipe.OutputUnit = unit;
        recipe.OutputQuantity = input.OutputQuantity;
        recipe.Comment = Clean(input.Comment, 2000);
        recipe.TechCardNo = Clean(input.TechCardNo, 32);
        recipe.TechnologistId = input.TechnologistId;
        recipe.EffectiveFrom = input.EffectiveFrom;
        recipe.BatchMinKg = input.BatchMinKg;
        recipe.BatchMaxKg = input.BatchMaxKg;
        recipe.PartialBatches = input.PartialBatches;
        recipe.PieceKg = input.PieceKg;
        recipe.PackKg = input.PackKg;
        recipe.PacksPerBox = input.PacksPerBox;
        recipe.OutputItemId = input.OutputItemId;
        foreach (var old in recipe.DefectTypes.ToList())
        {
            recipe.DefectTypes.Remove(old);
            db.ProductionDefectTypes.Remove(old);
        }

        foreach (var old in recipe.Lines.ToList())
        {
            recipe.Lines.Remove(old);
            db.ProductionRecipeLines.Remove(old);
        }

        foreach (var old in recipe.Stages.ToList())
        {
            recipe.Stages.Remove(old);
            db.ProductionRecipeStages.Remove(old);
        }

        foreach (var stage in stages.Values)
        {
            recipe.Stages.Add(stage);
            db.ProductionRecipeStages.Add(stage);
        }

        foreach (var line in newLines)
        {
            recipe.Lines.Add(line);
            db.ProductionRecipeLines.Add(line);
        }

        foreach (var defect in defects)
        {
            recipe.DefectTypes.Add(defect);
            db.ProductionDefectTypes.Add(defect);
        }

        return items;
    }

    private static void Optional(decimal? value, int digits, string label)
    {
        if (value is { } v && !ProductionRules.ValidQuantity(v, digits))
        {
            throw new FieldValidationException($"{label} — больше нуля, не больше {(digits == 3 ? "трёх" : "шести")} знаков после запятой.");
        }
    }

    private static void Percent(decimal? value, string label, bool required = false)
    {
        if (value is { } v ? v <= 0 || v > 1000 || decimal.Round(v, 3) != v : required)
        {
            throw new FieldValidationException($"{label} — от 0 до 1 000 % (больше нуля), не больше трёх знаков после запятой.");
        }
    }

    /// <summary>Рецептура одной строкой — для истории правок черновика: выход, этапы с выходом и браком, компоненты; названия позиций — из items.</summary>
    private static string Snapshot(ProductionRecipe r, IReadOnlyDictionary<Guid, ProcurementItem> items)
    {
        var stages = r.Stages.OrderBy(s => s.StageNo).ToList();
        string Stage(Guid? id) => stages.FirstOrDefault(s => s.Id == id)?.Name is { } n ? $"[{n}] " : "";
        string? N(decimal? v) => v is { } x ? ProcurementRules.Quantity(x) : null;
        var parts = new List<string> { $"{r.Name}; выход {ProcurementRules.Quantity(r.OutputQuantity)} {r.OutputUnit}" };
        if (r.TechCardNo is not null)
        {
            parts.Add($"карта {r.TechCardNo}");
        }

        if (r.OutputItemId is not null)
        {
            parts.Add("выход приходуется на склад");
        }

        if (r.BatchMinKg is not null || r.BatchMaxKg is not null || r.PartialBatches)
        {
            parts.Add($"замес: мин. {N(r.BatchMinKg) ?? "—"}, макс. {N(r.BatchMaxKg) ?? "—"} кг{(r.PartialBatches ? ", частичный" : "")}");
        }

        if (r.PieceKg is not null || r.PackKg is not null || r.PacksPerBox is not null)
        {
            parts.Add($"упаковка: штука {N(r.PieceKg) ?? "—"} кг, упаковка {N(r.PackKg) ?? "—"} кг, в коробке {r.PacksPerBox?.ToString() ?? "—"}");
        }

        if (stages.Count > 0)
        {
            parts.Add("этапы: " + string.Join(" → ", stages.Select(s =>
            {
                var defect = r.DefectTypes.Where(d => d.StageId == s.Id && d.ItemId == null).Sum(d => d.NormPercent);
                return $"{s.Name}{(s.YieldPercent != 100 ? $" (выход {s.YieldPercent:0.###} %)" : "")}{(defect > 0 ? $" (брак {defect:0.##} %)" : "")}";
            })));
        }

        parts.AddRange(r.Lines.OrderBy(l => l.LineNo).Select(l => items.GetValueOrDefault(l.ItemId) is { } i
            ? $"{Stage(l.StageId)}{i.Name} {ProcurementRules.Quantity(l.Quantity)} {l.UnitCode ?? i.UnitCode}{Basis(l.Basis)}{(l.LossPercent > 0 ? $" (потери {l.LossPercent:0.##} %)" : "")}"
            : ProcurementRules.Quantity(l.Quantity)));
        parts.AddRange(r.DefectTypes.OrderBy(d => d.Name).Select(d =>
            $"брак {Stage(d.StageId)}{d.Name} {d.NormPercent:0.##} %{(d.ItemId is { } i && items.GetValueOrDefault(i) is { } item ? $" ({item.Name})" : "")}"));
        return string.Join("; ", parts);
    }

    internal static string Basis(ProductionLineBasis basis) => basis switch
    {
        ProductionLineBasis.Batch => " на замес",
        ProductionLineBasis.Piece => " на штуку",
        ProductionLineBasis.Pack => " на упаковку",
        ProductionLineBasis.Box => " на коробку",
        _ => "",
    };

    private static string Lines(int n) => n switch
    {
        0 => "без позиций",
        _ => $"позиций: {n}",
    };

    private static string Stages(int n) => n == 0 ? "" : $", этапов: {n}";

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new FieldConflictException("Рецептуру уже изменили в другом окне — обновите страницу.");
        }
        catch (DbUpdateException e) when (e.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            throw new FieldConflictException("Версию рецептуры уже изменили одновременно — обновите страницу.");
        }
    }

    private void Log(ProcurementContext ctx, Guid id, string action, ProductionRecipeStatus? from, ProductionRecipeStatus? to, string text, string? comment = null) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProductionRules.RecipeDoc, DocId = id, ActorId = ctx.UserId, Action = action, FromStatus = from?.ToString(), ToStatus = to?.ToString(),
            Text = text.Length <= 1000 ? text : text[..999] + "…", Comment = comment is { Length: > 2000 } ? comment[..1999] + "…" : comment,
        });

    private static string? Clean(string? text, int max)
    {
        var t = text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length <= max ? t : throw new FieldValidationException($"Текст — до {max} знаков.");
    }
}

/// <summary>Версия рецептуры → вход расчётного механизма; строки задания по позициям из расчёта.</summary>
internal static class ProductionRecipeCalc
{
    /// <summary>
    /// Этапы с нормативом брака продукта (сумма видов брака продукта этапа), компоненты в единице строки с потерями (технологические + нормативы
    /// брака этого материала на этапе, не больше 100 %). Нужны загруженные строки с позициями, этапы и виды брака.
    /// </summary>
    public static CalcRecipe From(ProductionRecipe r)
    {
        var stages = r.Stages.OrderBy(s => s.StageNo).ToList();
        return new CalcRecipe(r.OutputQuantity, r.OutputUnit,
            stages.Select(s => new CalcStage(s.StageNo, s.Name, s.Kind, s.YieldPercent, r.DefectTypes.Where(d => d.StageId == s.Id && d.ItemId == null).Sum(d => d.NormPercent),
                s.DefectBase)).ToList(),
            r.Lines.OrderBy(l => l.LineNo).Select(l =>
            {
                var material = l.StageId is null ? 0 : r.DefectTypes.Where(d => d.StageId == l.StageId && d.ItemId == l.ItemId).Sum(d => d.NormPercent);
                var unit = l.UnitCode ?? l.Item!.UnitCode;
                return new CalcLine(l.Id, l.ItemId, l.Item!.Name, unit, l.Item.UnitCode, stages.FirstOrDefault(s => s.Id == l.StageId)?.StageNo, l.Basis,
                    l.Quantity, Math.Min(100, l.LossPercent + material), l.UnitKg, l.Item.Units.FirstOrDefault(u => u.UnitCode == unit)?.Factor);
            }).ToList(),
            r.BatchMinKg, r.BatchMaxKg, r.PartialBatches, r.PieceKg, r.PackKg, r.PacksPerBox);
    }

    /// <summary>
    /// Строки задания — по позиции склада, в порядке первой строки рецептуры: потребность — из расчёта. Норма и потери: у рецептуры без этапов —
    /// как в строке (прежняя формула), с этапами — потребность на выход рецептуры (с потерями и браком), потери 0.
    /// </summary>
    public static IReadOnlyList<(Guid ItemId, int LineNo, decimal Norm, decimal LossPercent, decimal Required)> OrderLines(ProductionRecipe r, CalcResult calc)
    {
        var perOutput = r.Stages.Count > 0 ? ProductionCalc.Plan(From(r), r.OutputQuantity).Items.ToDictionary(i => i.ItemId, i => i.Quantity) : null;
        return calc.Items.Select(i =>
        {
            var first = r.Lines.Where(l => l.ItemId == i.ItemId).OrderBy(l => l.LineNo).First();
            return perOutput is null
                ? (i.ItemId, first.LineNo, first.Quantity, first.LossPercent, i.Quantity)
                : (i.ItemId, first.LineNo, perOutput.GetValueOrDefault(i.ItemId), 0m, i.Quantity);
        }).OrderBy(l => l.LineNo).ToList();
    }
}

/// <summary>Сравнение версий: что добавлено, убрано и изменено — по карте, этапам, компонентам и видам брака.</summary>
internal static class ProductionRecipeDiff
{
    public static IReadOnlyList<ProductionRecipeChange> Compare(ProductionRecipe a, ProductionRecipe b)
    {
        var changes = new List<ProductionRecipeChange>();
        Section(changes, "Карта", Card(a), Card(b));
        Section(changes, "Этап", StagesOf(a), StagesOf(b));
        Section(changes, "Компонент", LinesOf(a), LinesOf(b));
        Section(changes, "Брак", DefectsOf(a), DefectsOf(b));
        return changes;
    }

    private static void Section(List<ProductionRecipeChange> changes, string section, Dictionary<string, Dictionary<string, string?>> left,
        Dictionary<string, Dictionary<string, string?>> right)
    {
        foreach (var key in left.Keys.Union(right.Keys))
        {
            var (l, r) = (left.GetValueOrDefault(key), right.GetValueOrDefault(key));
            if (l is null || r is null)
            {
                changes.Add(new ProductionRecipeChange(section, key, l is null ? "добавлено" : "убрано", l is null ? null : Summary(l), r is null ? null : Summary(r)));
                continue;
            }

            foreach (var field in l.Keys.Union(r.Keys))
            {
                var (before, after) = (l.GetValueOrDefault(field), r.GetValueOrDefault(field));
                if (before != after)
                {
                    changes.Add(new ProductionRecipeChange(section, key, field, before, after));
                }
            }
        }
    }

    private static string Summary(Dictionary<string, string?> fields) => string.Join(", ", fields.Where(f => f.Value is not null).Select(f => $"{f.Key}: {f.Value}"));

    private static string? N(decimal? v) => v is { } x ? ProcurementRules.Quantity(x) : null;

    private static Dictionary<string, Dictionary<string, string?>> Card(ProductionRecipe r) => new()
    {
        ["Рецептура"] = new()
        {
            ["Название"] = r.Name, ["Выход"] = $"{N(r.OutputQuantity)} {r.OutputUnit}", ["Номер карты"] = r.TechCardNo, ["Технолог"] = r.Technologist?.FullName,
            ["Замес, мин. кг"] = N(r.BatchMinKg), ["Замес, макс. кг"] = N(r.BatchMaxKg), ["Частичный замес"] = r.PartialBatches ? "да" : "нет",
            ["Масса штуки, кг"] = N(r.PieceKg), ["Масса упаковки, кг"] = N(r.PackKg), ["Упаковок в коробке"] = r.PacksPerBox?.ToString(), ["Комментарий"] = r.Comment,
            ["Позиция склада выхода"] = r.OutputItem?.Name,
        },
    };

    private static string StageName(ProductionRecipe r, Guid? id) => r.Stages.FirstOrDefault(s => s.Id == id)?.Name ?? "без этапа";

    private static Dictionary<string, Dictionary<string, string?>> StagesOf(ProductionRecipe r) =>
        r.Stages.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g =>
        {
            var s = g.First();
            return new Dictionary<string, string?>
            {
                ["Порядок"] = s.StageNo.ToString(), ["Тип"] = ProductionRules.Label(s.Kind), ["Участок"] = s.Area, ["Выход, %"] = N(s.YieldPercent),
                ["Выход мин., %"] = N(s.YieldMinPercent), ["Выход макс., %"] = N(s.YieldMaxPercent), ["База брака"] = ProductionRules.Label(s.DefectBase),
                ["Влажность"] = s.MoistureNorm is { } m ? $"{N(m)} {s.MoistureUnit}" : null, ["Допуск влажности"] = N(s.MoistureTolerance),
                ["Переработка"] = s.Reworkable ? "да" : "нет",
            };
        });

    private static Dictionary<string, Dictionary<string, string?>> LinesOf(ProductionRecipe r) =>
        r.Lines.GroupBy(l => $"{StageName(r, l.StageId)} · {l.Item?.Name}").ToDictionary(g => g.Key, g =>
        {
            var l = g.First();
            return new Dictionary<string, string?>
            {
                ["Норма"] = $"{N(l.Quantity)} {l.UnitCode ?? l.Item?.UnitCode}{ProductionRecipeService.Basis(l.Basis)}", ["Потери, %"] = N(l.LossPercent), ["Группа"] = l.GroupName,
                ["Масса единицы, кг"] = N(l.UnitKg),
            };
        });

    private static Dictionary<string, Dictionary<string, string?>> DefectsOf(ProductionRecipe r) =>
        r.DefectTypes.GroupBy(d => $"{StageName(r, d.StageId)} · {d.Name}").ToDictionary(g => g.Key, g =>
        {
            var d = g.First();
            return new Dictionary<string, string?>
            {
                ["Норматив, %"] = N(d.NormPercent), ["Материал"] = d.Item?.Name, ["Переработка"] = d.Reworkable ? "да" : "нет",
            };
        });
}

/// <summary>Доступное сырьё (годное и свободное от резерва заявок): по складу или по всем действующим складам.</summary>
internal static class ProductionStock
{
    public static async Task<Dictionary<Guid, decimal>> AvailableAsync(IAppDbContext db, IEnumerable<Guid> itemIds, Guid? warehouseId, DateOnly today, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToList();
        var active = db.ProcurementWarehouses.Where(w => w.IsActive).Select(w => w.Id);
        var balances = await db.ProcurementStockBalances.AsNoTracking()
            .Where(b => ids.Contains(b.ItemId) && (warehouseId == null ? active.Contains(b.WarehouseId) : b.WarehouseId == warehouseId))
            .Select(b => new { b.WarehouseId, b.ItemId, b.OnHand, b.Reserved, b.Item!.TracksBatches })
            .ToListAsync(ct);
        var usable = await db.ProcurementBatchBalances.AsNoTracking()
            .Where(b => ids.Contains(b.ItemId) && b.OnHand > 0 && (warehouseId == null ? active.Contains(b.WarehouseId) : b.WarehouseId == warehouseId)
                        && b.Batch!.QualityStatus == ProcurementBatchQuality.Approved && b.Batch.ExpiresOn != null && b.Batch.ExpiresOn >= today)
            .GroupBy(b => new { b.WarehouseId, b.ItemId })
            .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, Qty = g.Sum(b => b.OnHand) })
            .ToListAsync(ct);
        var result = new Dictionary<Guid, decimal>();
        foreach (var b in balances)
        {
            var good = b.TracksBatches ? usable.FirstOrDefault(u => u.WarehouseId == b.WarehouseId && u.ItemId == b.ItemId)?.Qty ?? 0 : b.OnHand;
            result[b.ItemId] = result.GetValueOrDefault(b.ItemId) + Math.Max(0, good - b.Reserved);
        }

        return result;
    }
}

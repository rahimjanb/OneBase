using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;

namespace OneBase.Application.Production;

public sealed record ProductionRecipeLineInput(Guid ItemId, decimal Quantity, decimal LossPercent = 0, string? Comment = null);

/// <summary>Рецептура из формы: код (у новой), название, выход (товар Linko, название, единица, количество), комментарий, строки; Version — версия при правке.</summary>
public sealed record ProductionRecipeInput(string? Code, string? Name, long? LinkoProductId, string? OutputName, string? OutputUnit, decimal OutputQuantity, string? Comment,
    IReadOnlyList<ProductionRecipeLineInput>? Lines, uint? Version = null);

/// <summary>Строка списка: рецептура по коду — действующая версия (а нет — последняя), есть ли черновик, сколько версий.</summary>
public sealed record ProductionRecipeRow(Guid Id, string Code, int VersionNo, string Name, string OutputName, string OutputUnit, decimal OutputQuantity,
    ProductionRecipeStatus Status, int Lines, DateTimeOffset? ActivatedAt, Guid? DraftId, int Versions);

public sealed record ProductionRecipeList(IReadOnlyList<ProductionRecipeRow> Items, int Total, bool CanCreate);

public sealed record ProductionRecipeLineView(Guid Id, int LineNo, Guid ItemId, string Sku, string Name, string UnitCode, decimal Quantity, decimal LossPercent, string? Comment,
    bool ItemActive, bool TracksBatches);

public sealed record ProductionRecipeVersionView(Guid Id, int VersionNo, ProductionRecipeStatus Status, DateTimeOffset? ActivatedAt, string? ActivatedByName, DateTimeOffset CreatedAt);

/// <summary>Что можно с версией: править (черновик), ввести в действие (черновик), новая версия (нет черновика), архивировать (действующую), задание на выпуск.</summary>
public sealed record ProductionRecipeAbilities(bool Edit, bool Activate, bool NewVersion, bool Archive, bool Order);

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
    int Orders);

/// <summary>Потребность на количество выхода: по каждой позиции — норма, потери, сколько нужно, доступно на складе и не хватает.</summary>
public sealed record ProductionRequirementLine(Guid ItemId, string Sku, string Name, string UnitCode, decimal Norm, decimal LossPercent, decimal Required, decimal Available,
    decimal Shortage);

public sealed record ProductionRequirement(Guid RecipeId, decimal Quantity, string OutputUnit, Guid? WarehouseId, string WarehouseName,
    IReadOnlyList<ProductionRequirementLine> Lines);

/// <summary>Товар Linko для выхода рецептуры (справочник только для чтения).</summary>
public sealed record ProductionProductOption(long Id, string Name, string? Code, string? Unit);

/// <summary>Действующая рецептура для формы задания: выход и единица.</summary>
public sealed record ProductionRecipeOption(Guid Id, string Code, int VersionNo, string Name, string OutputName, string OutputUnit, decimal OutputQuantity);

/// <summary>
/// Рецептуры (технолог): состав продукции — позиции склада с нормой на выход рецептуры и потерями. Версии: черновик правится свободно;
/// «ввести в действие» делает версию действующей, прежняя действующая уходит в архив; действующая и архивная не правятся — «новая версия»
/// копирует версию в черновик (один черновик на код). Задание на выпуск закрепляет версию. Видят производство, технолог, кладовщик, снабжение,
/// директор; правит — технолог (право «Производство: рецептуры»). Каждое изменение — в истории.
/// </summary>
public sealed class ProductionRecipeService(IAppDbContext db, TimeProvider clock)
{
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
            .Select(r => new { r.Id, r.Code, r.VersionNo, r.Name, r.OutputName, r.OutputUnit, r.OutputQuantity, r.Status, r.ActivatedAt, Lines = r.Lines.Count })
            .ToListAsync(ct);
        var rows = all.GroupBy(r => r.Code).Select(g =>
        {
            var shown = g.FirstOrDefault(r => r.Status == ProductionRecipeStatus.Active) ?? g.OrderByDescending(r => r.VersionNo).First();
            var draft = g.FirstOrDefault(r => r.Status == ProductionRecipeStatus.Draft);
            return new ProductionRecipeRow(shown.Id, shown.Code, shown.VersionNo, shown.Name, shown.OutputName, shown.OutputUnit, shown.OutputQuantity, shown.Status, shown.Lines,
                shown.ActivatedAt, draft?.Id, g.Count());
        });
        if (search?.Trim().ToLowerInvariant() is { Length: > 0 } text)
        {
            rows = rows.Where(r => r.Code.Contains(text, StringComparison.OrdinalIgnoreCase) || r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                                                                                           || r.OutputName.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        rows = status switch
        {
            "active" => rows.Where(r => r.Status == ProductionRecipeStatus.Active),
            "draft" => rows.Where(r => r.Status == ProductionRecipeStatus.Draft || r.DraftId != null),
            "archived" => rows.Where(r => r.Status == ProductionRecipeStatus.Archived),
            _ => rows,
        };
        var list = rows.OrderBy(r => r.Status == ProductionRecipeStatus.Archived).ThenBy(r => r.Name).ThenBy(r => r.Code).ToList();
        return new ProductionRecipeList(list, list.Count, ctx.Recipes);
    }

    public async Task<ProductionRecipeDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireView(ctx);
        var r = await db.ProductionRecipes.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Item).Include(x => x.ActivatedBy).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Рецептура не найдена.");
        var versions = await db.ProductionRecipes.AsNoTracking().Where(x => x.Code == r.Code).OrderByDescending(x => x.VersionNo)
            .Select(x => new ProductionRecipeVersionView(x.Id, x.VersionNo, x.Status, x.ActivatedAt, x.ActivatedBy == null ? null : x.ActivatedBy.FullName, x.CreatedAt))
            .ToListAsync(ct);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == ProductionRules.RecipeDoc && e.DocId == id).OrderBy(e => e.CreatedAt)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var product = r.LinkoProductId is { } pid ? await db.LinkoProducts.AsNoTracking().Where(p => p.Id == pid).Select(p => p.Name).FirstOrDefaultAsync(ct) : null;
        var orders = await db.ProductionOrders.CountAsync(o => o.RecipeId == id, ct);
        var hasDraft = versions.Any(v => v.Status == ProductionRecipeStatus.Draft);
        var lines = r.Lines.OrderBy(l => l.LineNo).Select(l => new ProductionRecipeLineView(l.Id, l.LineNo, l.ItemId, l.Item!.Sku, l.Item.Name, l.Item.UnitCode, l.Quantity,
            l.LossPercent, l.Comment, l.Item.IsActive, l.Item.TracksBatches)).ToList();
        var can = new ProductionRecipeAbilities(
            ctx.Recipes && r.Status == ProductionRecipeStatus.Draft,
            ctx.Recipes && r.Status == ProductionRecipeStatus.Draft && lines.Count > 0,
            ctx.Recipes && r.Status != ProductionRecipeStatus.Draft && !hasDraft,
            ctx.Recipes && r.Status == ProductionRecipeStatus.Active,
            ctx.Produces && r.Status == ProductionRecipeStatus.Active);
        return new ProductionRecipeDetail(r.Id, r.Code, r.VersionNo, r.Name, r.LinkoProductId, product, r.OutputName, r.OutputUnit, r.OutputQuantity, r.Status, r.Comment,
            r.ActivatedAt, r.ActivatedBy?.FullName, r.CreatedAt, r.Version, lines, versions, history, can, orders);
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
            $"Создана рецептура {recipe.Code}, версия 1: {recipe.Name} — {Lines(recipe.Lines.Count)} на {ProcurementRules.Quantity(recipe.OutputQuantity)} {recipe.OutputUnit}");
        await db.SaveChangesAsync(ct);
        return recipe.Id;
    }

    /// <summary>Правка черновика (версия — xmin: изменили в другом окне — 409).</summary>
    public async Task UpdateAsync(ProcurementContext ctx, Guid id, ProductionRecipeInput input, CancellationToken ct)
    {
        RequireEdit(ctx);
        var recipe = await db.ProductionRecipes.Include(r => r.Lines).ThenInclude(l => l.Item).FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (recipe.Status != ProductionRecipeStatus.Draft)
        {
            throw new FieldConflictException("Действующую и архивную версию не правят — создайте новую версию.");
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

    /// <summary>Ввести черновик в действие: прежняя действующая версия того же кода уходит в архив.</summary>
    public async Task ActivateAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireEdit(ctx);
        var recipe = await db.ProductionRecipes.Include(r => r.Lines).ThenInclude(l => l.Item).FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (recipe.Status != ProductionRecipeStatus.Draft)
        {
            throw new FieldConflictException("Ввести в действие можно только черновик.");
        }

        if (recipe.Lines.Count == 0)
        {
            throw new FieldValidationException("Добавьте в рецептуру хотя бы одну позицию.");
        }

        if (recipe.Lines.FirstOrDefault(l => !l.Item!.IsActive) is { } off)
        {
            throw new FieldConflictException($"«{off.Item!.Name}» отключена в каталоге склада — замените её в рецептуре.");
        }

        var current = await db.ProductionRecipes.FirstOrDefaultAsync(r => r.Code == recipe.Code && r.Status == ProductionRecipeStatus.Active, ct);
        if (current is not null)
        {
            current.Status = ProductionRecipeStatus.Archived;
            current.UpdatedAt = DateTimeOffset.UtcNow;
            Log(ctx, current.Id, "archived", ProductionRecipeStatus.Active, ProductionRecipeStatus.Archived,
                $"Версия {current.VersionNo} ушла в архив: в действие введена версия {recipe.VersionNo}");
            // Сначала архив, затем новая действующая: уникальный индекс «одна действующая на код» не должен видеть двух сразу.
            await SaveAsync(ct);
        }

        recipe.Status = ProductionRecipeStatus.Active;
        recipe.ActivatedAt = DateTimeOffset.UtcNow;
        recipe.ActivatedById = ctx.UserId;
        Log(ctx, recipe.Id, "activated", ProductionRecipeStatus.Draft, ProductionRecipeStatus.Active, $"Версия {recipe.VersionNo} введена в действие");
        await SaveAsync(ct);
    }

    /// <summary>Новая версия — копия этой (действующей или архивной) в черновик с номером на один больше последнего; черновик у кода один.</summary>
    public async Task<Guid> NewVersionAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireEdit(ctx);
        var source = await db.ProductionRecipes.AsNoTracking().Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (await db.ProductionRecipes.AsNoTracking().FirstOrDefaultAsync(r => r.Code == source.Code && r.Status == ProductionRecipeStatus.Draft, ct) is { } draft)
        {
            throw new FieldConflictException($"У рецептуры {source.Code} уже есть черновик — версия {draft.VersionNo}.");
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
        };
        foreach (var line in source.Lines.OrderBy(l => l.LineNo))
        {
            copy.Lines.Add(new ProductionRecipeLine { RecipeId = copy.Id, LineNo = line.LineNo, ItemId = line.ItemId, Quantity = line.Quantity, LossPercent = line.LossPercent, Comment = line.Comment });
        }

        db.ProductionRecipes.Add(copy);
        Log(ctx, copy.Id, "created", null, ProductionRecipeStatus.Draft, $"Создана версия {next} — копия версии {source.VersionNo}");
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

    /// <summary>Потребность на quantity выхода: по нормам и потерям, доступно (годное и свободное) на складе warehouseId (не указан — на всех складах).</summary>
    public async Task<ProductionRequirement> RequirementAsync(ProcurementContext ctx, Guid id, decimal quantity, Guid? warehouseId, CancellationToken ct)
    {
        RequireView(ctx);
        if (!ProductionRules.ValidQuantity(quantity, 3))
        {
            throw new FieldValidationException("Количество выхода — больше нуля, не больше трёх знаков после запятой.");
        }

        var r = await db.ProductionRecipes.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Item).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Рецептура не найдена.");
        var name = warehouseId is { } wid
            ? await db.ProcurementWarehouses.AsNoTracking().Where(w => w.Id == wid).Select(w => w.Name).FirstOrDefaultAsync(ct) ?? throw new FieldNotFoundException("Склад не найден.")
            : "все склады";
        var available = await ProductionStock.AvailableAsync(db, r.Lines.Select(l => l.ItemId), warehouseId, ProcurementBatchRules.Today(clock), ct);
        var lines = r.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var required = ProductionRules.Requirement(l.Quantity, quantity, r.OutputQuantity, l.LossPercent);
            var free = available.GetValueOrDefault(l.ItemId);
            return new ProductionRequirementLine(l.ItemId, l.Item!.Sku, l.Item.Name, l.Item.UnitCode, l.Quantity, l.LossPercent, required, free, Math.Max(0, required - free));
        }).ToList();
        return new ProductionRequirement(r.Id, quantity, r.OutputUnit, warehouseId, name, lines);
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

    private async Task<Dictionary<Guid, ProcurementItem>> ApplyAsync(ProductionRecipe recipe, ProductionRecipeInput input, CancellationToken ct)
    {
        var name = Clean(input.Name, 200) ?? throw new FieldValidationException("Укажите название рецептуры.");
        var unit = input.OutputUnit?.Trim();
        if (string.IsNullOrEmpty(unit) || !await db.ProcurementUnits.AnyAsync(u => u.Code == unit, ct))
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

        var lines = input.Lines ?? [];
        if (lines.Count > ProductionRules.MaxLines)
        {
            throw new FieldValidationException($"Позиций в рецептуре — до {ProductionRules.MaxLines}.");
        }

        var ids = lines.Select(l => l.ItemId).ToList();
        var items = await db.ProcurementItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var seen = new HashSet<Guid>();
        foreach (var (line, no) in lines.Select((l, i) => (l, i + 1)))
        {
            var item = items.GetValueOrDefault(line.ItemId) ?? throw new FieldValidationException($"Строка {no}: позиция склада не найдена.");
            if (!item.IsActive && recipe.Lines.All(l => l.ItemId != item.Id))
            {
                throw new FieldValidationException($"«{item.Name}» отключена в каталоге склада.");
            }

            if (!seen.Add(item.Id))
            {
                throw new FieldValidationException($"«{item.Name}» уже есть в рецептуре — одна позиция — одна строка.");
            }

            if (!ProductionRules.ValidQuantity(line.Quantity, 6))
            {
                throw new FieldValidationException($"«{item.Name}»: норма — больше нуля, не больше шести знаков после запятой.");
            }

            if (line.LossPercent < 0 || line.LossPercent > 100 || decimal.Round(line.LossPercent, 2) != line.LossPercent)
            {
                throw new FieldValidationException($"«{item.Name}»: потери — от 0 до 100 %, не больше двух знаков после запятой.");
            }
        }

        recipe.Name = name;
        recipe.LinkoProductId = input.LinkoProductId;
        recipe.OutputName = Clean(input.OutputName, 200) ?? name;
        recipe.OutputUnit = unit;
        recipe.OutputQuantity = input.OutputQuantity;
        recipe.Comment = Clean(input.Comment, 2000);
        foreach (var old in recipe.Lines.ToList())
        {
            recipe.Lines.Remove(old);
            db.ProductionRecipeLines.Remove(old);
        }

        foreach (var (line, no) in lines.Select((l, i) => (l, i + 1)))
        {
            var added = new ProductionRecipeLine
            {
                RecipeId = recipe.Id, LineNo = no, ItemId = line.ItemId, Quantity = line.Quantity, LossPercent = line.LossPercent, Comment = Clean(line.Comment, 500),
            };
            recipe.Lines.Add(added);
            db.ProductionRecipeLines.Add(added);
        }

        return items;
    }

    /// <summary>Рецептура одной строкой — для истории правок черновика; названия позиций — из items.</summary>
    private static string Snapshot(ProductionRecipe r, IReadOnlyDictionary<Guid, ProcurementItem> items) =>
        $"{r.Name}; выход {ProcurementRules.Quantity(r.OutputQuantity)} {r.OutputUnit}; " +
        string.Join("; ", r.Lines.OrderBy(l => l.LineNo).Select(l => items.GetValueOrDefault(l.ItemId) is { } i
            ? $"{i.Name} {ProcurementRules.Quantity(l.Quantity)} {i.UnitCode}{(l.LossPercent > 0 ? $" (потери {l.LossPercent:0.##} %)" : "")}"
            : ProcurementRules.Quantity(l.Quantity)));

    private static string Lines(int n) => n switch
    {
        0 => "без позиций",
        _ => $"позиций: {n}",
    };

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
            Text = text.Length <= 1000 ? text : text[..999] + "…", Comment = comment,
        });

    private static string? Clean(string? text, int max)
    {
        var t = text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length <= max ? t : throw new FieldValidationException($"Текст — до {max} знаков.");
    }
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

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;

namespace OneBase.Application.Production.Import;

public sealed record RecipeImportUploaded(Guid Id, bool Existing);

public sealed record RecipeImportListRow(Guid Id, string FileName, long FileSize, string Status, DateTimeOffset CreatedAt, string? CreatedBy, DateTimeOffset? AppliedAt,
    int Recipes, int Ready, int NeedsReview, int Applied);

public sealed record RecipeImportSheetView(string Name, bool Hidden, bool HasRecipes, int Products, int WithComposition, IReadOnlyList<string> Notes);

/// <summary>
/// Сводка загрузки: рецептуры (найдено столбцов, с составом, пустые, дубли), по статусам; ячейки (нормы больше нуля, нули, отрицательные,
/// ошибки, текст); строки, найденные на складе и нет, артикулов без позиции; полуфабрикаты; замечаний block и warn.
/// </summary>
public sealed record RecipeImportTotals(int Recipes, int WithComposition, int Empty, int Duplicates, int Ready, int NeedsReview, int Unchanged, int Applied,
    int Excluded, int Norms, int Zeros, int Negatives, int ErrorCells, int TextCells, int LinesMatched, int LinesUnmatched, int ArticlesUnmatched, int SemiFinished,
    int Blockers, int Warnings);

public sealed record RecipeImportRecipeRow(Guid Id, string Sheet, string Column, string? Article, string Name, string? Code, decimal? BaseQuantity, string? BaseUnit,
    int Components, int Matched, int Unmatched, int Errors, string Status, string Action, bool Include, bool IsSemiFinished, int Blockers, int Warnings,
    string? FirstIssue, Guid? RecipeId);

public sealed record RecipeImportView(Guid Id, string FileName, long FileSize, string FileHash, string Status, DateTimeOffset CreatedAt, string? CreatedBy,
    DateTimeOffset? AppliedAt, IReadOnlyList<RecipeImportSheetView> Sheets, RecipeImportTotals Totals, IReadOnlyList<RecipeImportRecipeRow> Recipes, bool CanEdit);

/// <summary>
/// Строка сравнения «Excel → техкарта»: ячейка, артикул, название, единица, «Вес», значение как в файле (исходная запись, формула, число),
/// позиция склада и как найдена, решение технолога, что попадёт в карту (норма и единица) и итог строки словами.
/// </summary>
public sealed record RecipeImportLineView(Guid Id, int Row, string Cell, string? Article, string? Name, string? Unit, string? Weight, string Kind, string? Raw,
    string? Formula, decimal? Value, string? Text, Guid? ItemId, string? ItemSku, string? ItemName, string? ItemUnit, string Match, string Decision,
    decimal? ManualValue, string? DecisionComment, decimal? CardQuantity, string? CardUnit, string Outcome);

/// <summary>Отличие: added — появится в карте, removed — уйдёт, changed — изменится (норма, единица).</summary>
public sealed record RecipeImportChange(string Kind, string Subject, string? Before, string? After);

public sealed record RecipeImportRecipeView(RecipeImportRecipeRow Row, string? GroupName, string? BaseLabel, string? TotalCell, string? TotalRaw, string? TotalFormula,
    string? LossCell, string? LossRaw, long? LinkoProductId, string? LinkoName, long? LinkoSuggestedId, string? LinkoSuggestedName, Guid? OutputItemId,
    string? OutputItemName, Guid? ExistingRecipeId, string? ExistingLabel, IReadOnlyList<RecipeImportIssue> Issues, IReadOnlyList<RecipeImportLineView> Lines,
    IReadOnlyList<RecipeImportChange> CardChanges, IReadOnlyList<RecipeImportChange> FileChanges, string? FileChangesAgainst);

public sealed record RecipeImportRecipeInput(string? Code, long? LinkoProductId, bool Include);

public sealed record RecipeImportLineInput(Guid? ItemId, RecipeImportDecision Decision, decimal? ManualValue, string? Comment);

public sealed record RecipeImportMapInput(string Article, Guid ItemId);

public sealed record RecipeImportMissingRow(string Article, IReadOnlyList<string> Names, IReadOnlyList<string> Units, int Recipes, int Lines, bool SemiFinished);

public sealed record RecipeImportApplyInput(IReadOnlyList<Guid>? RecipeIds);

public sealed record RecipeImportApplyRow(Guid ImportRecipeId, string Name, string Outcome, Guid? RecipeId, string? Error);

public sealed record RecipeImportApplyResult(int Created, int NewVersions, int Failed, int Skipped, IReadOnlyList<RecipeImportApplyRow> Rows);

/// <summary>
/// Импорт техкарт из Excel («Производство → Технолог → Техкарты → Импортировать технологические карты из Excel»). Загрузка файла (тот же файл
/// по SHA-256 — та же загрузка), разбор всех листов (<see cref="RecipeWorkbook"/>), сопоставление компонентов со складом (артикул →
/// запомненное сотрудником → вручную), анализ (<see cref="RecipeImportAnalyzer"/>), решения технолога, «Проверить заново» и создание
/// черновиков существующими механизмами техкарт: новая карта — <see cref="ProductionRecipeService.CreateAsync"/>, другой состав у
/// существующей — черновик новой версии; утверждённые не меняются. Двойное нажатие не создаёт второй черновик (условный захват строки и
/// уникальные индексы техкарт). Права — «Производство: рецептуры». Всё — в истории (ProcurementAuditLog, recipe_import).
/// </summary>
public sealed class RecipeImportService(IAppDbContext db, ProductionRecipeService recipes, TimeProvider clock)
{
    public const long MaxFileBytes = 25L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static void RequireImport(ProcurementContext ctx)
    {
        if (!ctx.Recipes)
        {
            throw new FieldForbiddenException("Импортировать техкарты может технолог — нужно право «Производство: рецептуры».");
        }
    }

    // ---------- Список и загрузка ----------

    public async Task<IReadOnlyList<RecipeImportListRow>> ListAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireImport(ctx);
        var imports = await db.ProductionRecipeImports.AsNoTracking().OrderByDescending(i => i.CreatedAt).Take(50)
            .Select(i => new
            {
                i.Id, i.FileName, i.FileSize, i.Status, i.CreatedAt, i.CreatedById, i.AppliedAt,
                Recipes = i.Recipes.Count,
                Ready = i.Recipes.Count(r => r.Status == RecipeImportRecipeStatus.Ready),
                Review = i.Recipes.Count(r => r.Status == RecipeImportRecipeStatus.NeedsReview),
                Applied = i.Recipes.Count(r => r.Status == RecipeImportRecipeStatus.Applied),
            }).ToListAsync(ct);
        var users = await Names(imports.Select(i => (Guid?)i.CreatedById), ct);
        return imports.Select(i => new RecipeImportListRow(i.Id, i.FileName, i.FileSize, i.Status.ToString(), i.CreatedAt, users.GetValueOrDefault(i.CreatedById),
            i.AppliedAt, i.Recipes, i.Ready, i.Review, i.Applied)).ToList();
    }

    public async Task<RecipeImportUploaded> UploadAsync(ProcurementContext ctx, string? fileName, Stream content, CancellationToken ct)
    {
        RequireImport(ctx);
        var name = Path.GetFileName(fileName ?? "").Trim();
        if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new FieldValidationException("Загрузите файл Excel .xlsx (старый .xls сохраните в Excel как .xlsx).");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0 || buffer.Length > MaxFileBytes)
        {
            throw new FieldValidationException($"Файл пустой или больше {MaxFileBytes / 1024 / 1024} МБ.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
        if (await db.ProductionRecipeImports.AsNoTracking().Where(i => i.FileHash == hash).Select(i => (Guid?)i.Id).FirstOrDefaultAsync(ct) is { } existing)
        {
            return new RecipeImportUploaded(existing, true); // тот же файл — та же загрузка, не новая
        }

        buffer.Position = 0;
        IReadOnlyList<RecipeSheet> sheets;
        try
        {
            sheets = RecipeWorkbook.Parse(XlsxBook.Read(buffer));
        }
        catch (XlsxFormatException e)
        {
            throw new FieldValidationException(e.Message);
        }

        if (!sheets.Any(s => s.HasRecipes))
        {
            throw new FieldValidationException("В файле нет листов с рецептурами: нужна строка «Артикул | Наименование сырья | Ед.изм.», продукты — по столбцам правее.");
        }

        var import = new ProductionRecipeImport
        {
            FileName = name.Length <= 255 ? name : name[^255..],
            FileHash = hash,
            FileSize = buffer.Length,
            CreatedById = ctx.UserId,
            Sheets = JsonSerializer.Serialize(sheets.Select(s => new RecipeImportSheetView(s.Name, s.Hidden, s.HasRecipes, s.Recipes.Count,
                s.Recipes.Count(r => r.Positive.Any()), s.Notes)).ToList(), Json),
        };
        foreach (var source in sheets.SelectMany(s => s.Recipes))
        {
            import.Recipes.Add(Recipe(import.Id, source));
        }

        await CarryOverAsync(import, ct);
        var lookups = await LookupsAsync(ct);
        Match(import.Recipes, lookups, await MapsAsync(ct));
        RecipeImportAnalyzer.Analyze(import.Recipes.ToList(), lookups);
        db.ProductionRecipeImports.Add(import);
        var totals = Totals(import.Recipes);
        Log(ctx, import.Id, "uploaded",
            $"Загружен «{import.FileName}»: листов с рецептурами {sheets.Count(s => s.HasRecipes)} из {sheets.Count}, рецептур с составом {totals.WithComposition}, " +
            $"готовы {totals.Ready}, нужна проверка {totals.NeedsReview}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            // Тот же файл загрузили одновременно из другого окна — берём ту загрузку.
            var other = await db.ProductionRecipeImports.AsNoTracking().Where(i => i.FileHash == hash).Select(i => i.Id).FirstAsync(ct);
            return new RecipeImportUploaded(other, true);
        }

        return new RecipeImportUploaded(import.Id, false);
    }

    private static ProductionRecipeImportRecipe Recipe(Guid importId, RecipeSource s)
    {
        var recipe = new ProductionRecipeImportRecipe
        {
            ImportId = importId, SourceKey = Cut(s.SourceKey, 300)!, Sheet = Cut(s.Sheet, 100)!, Column = s.Column, HeaderRow = s.HeaderRow,
            Article = Cut(s.Article, 64), Name = Cut(s.Name, 500)!, GroupName = Cut(s.Group, 200), BaseQuantity = s.BaseQuantity, BaseUnit = s.BaseUnit,
            BaseLabel = Cut(s.BaseLabel, 200), TotalCell = s.Total?.Ref, TotalRaw = Cut(s.Total?.Raw ?? s.Total?.Text, 100), TotalFormula = Cut(s.Total?.Formula, 500),
            LossCell = s.Loss?.Ref, LossRaw = Cut(s.Loss?.Raw ?? s.Loss?.Text, 100), Issues = "[]",
        };
        foreach (var l in s.Lines)
        {
            recipe.Lines.Add(new ProductionRecipeImportLine
            {
                ImportRecipeId = recipe.Id, Row = l.Row, Cell = l.Cell, Article = Cut(l.Article, 64), Name = Cut(l.Name, 500), Unit = Cut(l.Unit, 32),
                Weight = Cut(l.Weight, 32), Kind = l.Kind.ToString(), Raw = Cut(l.Raw, 100), Formula = Cut(l.Formula, 500), Value = l.Value, Text = Cut(l.Text, 200),
            });
        }

        return recipe;
    }

    /// <summary>
    /// Решения прошлых загрузок по той же рецептуре (лист и артикул): код, товар Linko, исключение столбца; по строкам — решения технолога, если
    /// значение в ячейке то же.
    /// </summary>
    private async Task CarryOverAsync(ProductionRecipeImport import, CancellationToken ct)
    {
        var keys = import.Recipes.Select(r => r.SourceKey).ToList();
        var previous = await db.ProductionRecipeImportRecipes.AsNoTracking().Include(r => r.Lines)
            .Where(r => keys.Contains(r.SourceKey)).OrderByDescending(r => r.Import!.CreatedAt).ToListAsync(ct);
        foreach (var recipe in import.Recipes)
        {
            if (previous.FirstOrDefault(p => p.SourceKey == recipe.SourceKey && p.Column == recipe.Column) is not { } last)
            {
                continue;
            }

            recipe.Code = last.Article is null ? last.Code : null;
            recipe.LinkoProductId = last.LinkoProductId;
            recipe.Include = last.Include;
            foreach (var line in recipe.Lines)
            {
                if (last.Lines.FirstOrDefault(p => p.Cell == line.Cell && p.Raw == line.Raw && p.Kind == line.Kind && p.Decision != RecipeImportDecision.None) is { } done)
                {
                    (line.Decision, line.ManualValue, line.DecisionComment, line.DecidedById, line.DecidedAt) =
                        (done.Decision, done.ManualValue, done.DecisionComment, done.DecidedById, done.DecidedAt);
                }
            }
        }
    }

    // ---------- Просмотр ----------

    public async Task<RecipeImportView> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireImport(ctx);
        var import = await db.ProductionRecipeImports.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new FieldNotFoundException("Загрузка не найдена.");
        var list = await db.ProductionRecipeImportRecipes.AsNoTracking().Include(r => r.Lines).Where(r => r.ImportId == id).ToListAsync(ct);
        var order = Order(import);
        var users = await Names([import.CreatedById], ct);
        return new RecipeImportView(import.Id, import.FileName, import.FileSize, import.FileHash, import.Status.ToString(), import.CreatedAt,
            users.GetValueOrDefault(import.CreatedById), import.AppliedAt, Sheets(import), Totals(list),
            list.OrderBy(r => order.GetValueOrDefault(r.Sheet)).ThenBy(r => r.Column.Length).ThenBy(r => r.Column, StringComparer.Ordinal).Select(Row).ToList(),
            ctx.Recipes);
    }

    public async Task<RecipeImportRecipeView> RecipeAsync(ProcurementContext ctx, Guid id, Guid recipeId, CancellationToken ct)
    {
        RequireImport(ctx);
        var recipe = await db.ProductionRecipeImportRecipes.AsNoTracking().Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == recipeId && r.ImportId == id, ct)
                     ?? throw new FieldNotFoundException("Рецептура загрузки не найдена.");
        var lookups = await LookupsAsync(ct);
        var cardLines = RecipeImportAnalyzer.CardLines(recipe, lookups);
        var linkoIds = new[] { recipe.LinkoProductId, recipe.LinkoSuggestedId }.Where(x => x is not null).Select(x => x!.Value).ToList();
        var linko = await db.LinkoProducts.AsNoTracking().Where(p => linkoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var ready = RecipeImportAnalyzer.ReadyLines(recipe, lookups);
        var lines = recipe.Lines.OrderBy(l => l.Row).Select(l => LineView(l, lookups, ready)).ToList();

        // Что изменится в карте OneBase с тем же кодом.
        var cardChanges = new List<RecipeImportChange>();
        string? existingLabel = null;
        if (recipe.ExistingRecipeId is { } existingId && await db.ProductionRecipes.AsNoTracking().Include(r => r.Lines).ThenInclude(l => l.Item)
                .FirstOrDefaultAsync(r => r.Id == existingId, ct) is { } card)
        {
            existingLabel = $"{card.Code}, версия {card.VersionNo} — {ProductionRules.Label(card.Status)}";
            var before = card.Lines.Where(l => l.StageId is null).GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.First());
            var after = (cardLines ?? []).GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.First());
            foreach (var key in before.Keys.Union(after.Keys))
            {
                var name = lookups.CatalogById.GetValueOrDefault(key)?.Name ?? before.GetValueOrDefault(key)?.Item?.Name ?? key.ToString();
                var b = before.GetValueOrDefault(key);
                var a = after.GetValueOrDefault(key);
                var bt = b is null ? null : $"{ExcelNumbers.Format(b.Quantity)} {b.UnitCode ?? b.Item?.UnitCode}";
                var at = a is null ? null : $"{ExcelNumbers.Format(a.Quantity)} {a.UnitCode ?? lookups.CatalogById.GetValueOrDefault(a.ItemId)?.UnitCode}";
                if (b is null || a is null || bt != at)
                {
                    cardChanges.Add(new RecipeImportChange(b is null ? "added" : a is null ? "removed" : "changed", name, bt, at));
                }
            }

            if (card.Lines.Any(l => l.StageId is not null))
            {
                cardChanges.Insert(0, new RecipeImportChange("changed", "Этапы", "компоненты распределены по этапам", "состав без этапов"));
            }
        }

        // Что изменилось в файле с прошлой загрузки той же рецептуры.
        var fileChanges = new List<RecipeImportChange>();
        string? against = null;
        var uploadedAt = await db.ProductionRecipeImports.AsNoTracking().Where(i => i.Id == id).Select(i => i.CreatedAt).FirstAsync(ct);
        var prev = await db.ProductionRecipeImportRecipes.AsNoTracking().Include(r => r.Lines).Include(r => r.Import)
            .Where(r => r.SourceKey == recipe.SourceKey && r.Column == recipe.Column && r.ImportId != id && r.Import!.CreatedAt < uploadedAt)
            .OrderByDescending(r => r.Import!.CreatedAt).FirstOrDefaultAsync(ct);
        if (prev is not null)
        {
            against = $"«{prev.Import!.FileName}» от {prev.Import.CreatedAt.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy HH:mm}";
            string Key(ProductionRecipeImportLine l) => $"{l.Article} «{l.Name}»";
            var before = prev.Lines.Where(l => l.Kind != nameof(RecipeCellKind.Zero)).GroupBy(Key).ToDictionary(g => g.Key, g => string.Join(" + ", g.Select(x => x.Raw ?? x.Text)));
            var after = recipe.Lines.Where(l => l.Kind != nameof(RecipeCellKind.Zero)).GroupBy(Key).ToDictionary(g => g.Key, g => string.Join(" + ", g.Select(x => x.Raw ?? x.Text)));
            foreach (var key in before.Keys.Union(after.Keys))
            {
                var (b, a) = (before.GetValueOrDefault(key), after.GetValueOrDefault(key));
                if (b != a)
                {
                    fileChanges.Add(new RecipeImportChange(b is null ? "added" : a is null ? "removed" : "changed", key, Pretty(b), Pretty(a)));
                }
            }
        }

        return new RecipeImportRecipeView(Row(recipe), recipe.GroupName, recipe.BaseLabel, recipe.TotalCell, recipe.TotalRaw, recipe.TotalFormula, recipe.LossCell,
            recipe.LossRaw, recipe.LinkoProductId, recipe.LinkoProductId is { } lp ? linko.GetValueOrDefault(lp) : null, recipe.LinkoSuggestedId,
            recipe.LinkoSuggestedId is { } ls ? linko.GetValueOrDefault(ls) : null, recipe.OutputItemId,
            recipe.OutputItemId is { } oi ? lookups.CatalogById.GetValueOrDefault(oi)?.Name : null, recipe.ExistingRecipeId, existingLabel,
            RecipeImportAnalyzer.Issues(recipe.Issues), lines, cardChanges, fileChanges, against);
    }

    private static string? Pretty(string? raw) => raw is null ? null : string.Join(" + ", raw.Split(" + ").Select(p => ExcelNumbers.Parse(p) is { } n ? ExcelNumbers.Format(n) : p));

    private static RecipeImportLineView LineView(ProductionRecipeImportLine l, ImportLookups lookups, IReadOnlyList<ImportCardLine> card)
    {
        var item = l.ItemId is { } id ? lookups.CatalogById.GetValueOrDefault(id) : null;
        var cardLine = card.FirstOrDefault(c => c.Cell == l.Cell);
        var outcome = l.Decision == RecipeImportDecision.Exclude ? "не переносится — решение технолога"
            : l.Kind switch
            {
                nameof(RecipeCellKind.Zero) => "0 — не входит в состав",
                nameof(RecipeCellKind.Negative) => "отрицательная — отходы, в карту не переносится",
                nameof(RecipeCellKind.Error) when l.Decision != RecipeImportDecision.ManualValue => "ошибка в ячейке — нужна норма вручную",
                nameof(RecipeCellKind.Text) when l.Decision != RecipeImportDecision.ManualValue => "текст вместо числа — нужна норма вручную",
                _ when item is null => "нет позиции склада — сопоставьте",
                _ when cardLine is not null => l.Decision == RecipeImportDecision.ManualValue ? "в карту — норма вручную" : "в карту — как в Excel",
                _ => "проверьте замечания по ячейке",
            };
        return new RecipeImportLineView(l.Id, l.Row, l.Cell, l.Article, l.Name, l.Unit, l.Weight, l.Kind, l.Raw, l.Formula, l.Value, l.Text, l.ItemId, item?.Sku,
            item?.Name, item?.UnitCode, l.Match.ToString(), l.Decision.ToString(), l.ManualValue, l.DecisionComment, cardLine?.Quantity,
            cardLine is null ? null : cardLine.UnitCode ?? item?.UnitCode, outcome);
    }

    public async Task<IReadOnlyList<RecipeImportMissingRow>> MissingAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireImport(ctx);
        var lines = await db.ProductionRecipeImportLines.AsNoTracking()
            .Where(l => db.ProductionRecipeImportRecipes.Any(r => r.Id == l.ImportRecipeId && r.ImportId == id) && l.ItemId == null && l.Kind == nameof(RecipeCellKind.Number))
            .Select(l => new { l.Article, l.Name, l.Unit, l.ImportRecipeId }).ToListAsync(ct);
        var products = await db.ProductionRecipeImportRecipes.AsNoTracking().Where(r => r.ImportId == id && r.Article != null).Select(r => r.Article!).ToListAsync(ct);
        return lines.GroupBy(l => l.Article ?? "").Select(g => new RecipeImportMissingRow(g.Key, g.Select(x => x.Name ?? "").Distinct().ToList(),
                g.Select(x => x.Unit ?? "").Distinct().ToList(), g.Select(x => x.ImportRecipeId).Distinct().Count(), g.Count(), products.Contains(g.Key)))
            .OrderByDescending(r => r.Lines).ThenBy(r => r.Article, StringComparer.Ordinal).ToList();
    }

    // ---------- Решения технолога ----------

    public async Task<RecipeImportRecipeView> UpdateRecipeAsync(ProcurementContext ctx, Guid id, Guid recipeId, RecipeImportRecipeInput input, CancellationToken ct)
    {
        RequireImport(ctx);
        var (import, all) = await LoadAsync(id, ct);
        var recipe = all.FirstOrDefault(r => r.Id == recipeId) ?? throw new FieldNotFoundException("Рецептура загрузки не найдена.");
        Editable(recipe);
        var code = input.Code?.Trim() is { Length: > 0 } c ? c : null;
        if (code is not null && !ProductionRules.ValidCode(code))
        {
            throw new FieldValidationException("Код техкарты — до 32 знаков: буквы, цифры, «-», «_», «.».");
        }

        if (input.LinkoProductId is { } pid && !await db.LinkoProducts.AnyAsync(p => p.Id == pid, ct))
        {
            throw new FieldValidationException("Товар Linko не найден — выберите из списка.");
        }

        var before = $"код {recipe.Code ?? recipe.Article ?? "—"}, Linko {recipe.LinkoProductId?.ToString() ?? "—"}, {(recipe.Include ? "включена" : "исключена")}";
        recipe.Code = recipe.Article is null || code != recipe.Article ? code : null;
        recipe.LinkoProductId = input.LinkoProductId;
        recipe.Include = input.Include;
        await ReanalyzeAsync(all, ct);
        Log(ctx, import.Id, "recipe_updated",
            $"«{recipe.Sheet}» {recipe.Column} «{recipe.Name}»: было {before}; стало код {recipe.Code ?? recipe.Article ?? "—"}, Linko {recipe.LinkoProductId?.ToString() ?? "—"}, {(recipe.Include ? "включена" : "исключена")}");
        await SaveAsync(ct);
        return await RecipeAsync(ctx, id, recipeId, ct);
    }

    /// <summary>
    /// Решение по строке: позиция склада (вручную — запоминается для следующих загрузок), не переносить, норма вручную (не больше 6 знаков
    /// после запятой, с причиной), единица подтверждена. Решение и кто его принял — в строке и в истории.
    /// </summary>
    public async Task<RecipeImportRecipeView> DecideLineAsync(ProcurementContext ctx, Guid id, Guid lineId, RecipeImportLineInput input, CancellationToken ct)
    {
        RequireImport(ctx);
        var (import, all) = await LoadAsync(id, ct);
        var recipe = all.FirstOrDefault(r => r.Lines.Any(l => l.Id == lineId)) ?? throw new FieldNotFoundException("Строка загрузки не найдена.");
        Editable(recipe);
        var line = recipe.Lines.First(l => l.Id == lineId);
        var comment = input.Comment?.Trim() is { Length: > 0 } c ? (c.Length <= 500 ? c : throw new FieldValidationException("Причина — до 500 знаков.")) : null;
        switch (input.Decision)
        {
            case RecipeImportDecision.Exclude when comment is null:
                throw new FieldValidationException("Напишите, почему строка не переносится в техкарту.");
            case RecipeImportDecision.ManualValue when input.ManualValue is not { } v || v <= 0 || ExcelNumbers.Scale(v) > RecipeImportAnalyzer.MaxScale:
                throw new FieldValidationException($"Норма вручную — больше нуля, не больше {RecipeImportAnalyzer.MaxScale} знаков после запятой.");
            case RecipeImportDecision.ManualValue when comment is null:
                throw new FieldValidationException("Напишите, откуда норма (например, «уточнено у технолога по рецептуре»).");
            case RecipeImportDecision.UnitConfirmed when !string.IsNullOrWhiteSpace(line.Unit):
                throw new FieldValidationException("Единица в Excel указана — подтверждать нечего.");
        }

        var parts = new List<string>();
        if (input.ItemId is { } itemId && itemId != line.ItemId)
        {
            var item = await db.ProcurementItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct) ?? throw new FieldValidationException("Позиция склада не найдена.");
            line.ItemId = item.Id;
            line.Match = RecipeImportMatch.Manual;
            await RememberAsync(ctx, line.Article, line.Name, item.Id, ct);
            parts.Add($"позиция «{item.Sku} {item.Name}»");
        }

        line.Decision = input.Decision;
        line.ManualValue = input.Decision == RecipeImportDecision.ManualValue ? input.ManualValue : null;
        line.DecisionComment = input.Decision == RecipeImportDecision.None ? null : comment;
        line.DecidedById = ctx.UserId;
        line.DecidedAt = clock.GetUtcNow();
        parts.Add(input.Decision switch
        {
            RecipeImportDecision.Exclude => $"не переносить: {comment}",
            RecipeImportDecision.ManualValue => $"норма вручную {ExcelNumbers.Format(input.ManualValue!.Value)}: {comment}",
            RecipeImportDecision.UnitConfirmed => "единица позиции склада подтверждена",
            _ => "как в Excel",
        });
        await ReanalyzeAsync(all, ct);
        Log(ctx, import.Id, "line_decided", $"«{recipe.Sheet}» {line.Cell} {line.Article} «{line.Name}» (было «{line.Raw ?? line.Text}»): {string.Join("; ", parts)}");
        await SaveAsync(ct);
        return await RecipeAsync(ctx, id, recipe.Id, ct);
    }

    /// <summary>Сопоставить артикул Excel с позицией склада во всей загрузке (все строки с этим артикулом) и запомнить для следующих загрузок.</summary>
    public async Task<RecipeImportView> MapArticleAsync(ProcurementContext ctx, Guid id, RecipeImportMapInput input, CancellationToken ct)
    {
        RequireImport(ctx);
        var article = input.Article?.Trim() is { Length: > 0 } a ? a : throw new FieldValidationException("Укажите артикул из Excel.");
        var item = await db.ProcurementItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == input.ItemId, ct) ?? throw new FieldValidationException("Позиция склада не найдена.");
        var (import, all) = await LoadAsync(id, ct);
        var lines = all.Where(r => r.Status != RecipeImportRecipeStatus.Applied).SelectMany(r => r.Lines).Where(l => l.Article == article).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException($"Артикула {article} нет в непримененных рецептурах загрузки.");
        }

        foreach (var line in lines)
        {
            line.ItemId = item.Id;
            line.Match = RecipeImportMatch.Manual;
        }

        foreach (var name in lines.Select(l => l.Name).Distinct())
        {
            await RememberAsync(ctx, article, name, item.Id, ct);
        }

        await ReanalyzeAsync(all, ct);
        Log(ctx, import.Id, "mapped", $"Артикул {article} («{lines[0].Name}») → позиция склада «{item.Sku} {item.Name}», строк: {lines.Count}");
        await SaveAsync(ct);
        return await GetAsync(ctx, id, ct);
    }

    /// <summary>«Проверить заново»: каталог склада, Linko и карты OneBase могли измениться — сопоставление по артикулу и анализ повторяются (ручные решения сохраняются).</summary>
    public async Task<RecipeImportView> RematchAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireImport(ctx);
        var (import, all) = await LoadAsync(id, ct);
        var lookups = await LookupsAsync(ct);
        Match(all.Where(r => r.Status != RecipeImportRecipeStatus.Applied), lookups, await MapsAsync(ct));
        RecipeImportAnalyzer.Analyze(all, lookups);
        Log(ctx, import.Id, "rematched", $"Проверено заново: готовы {all.Count(r => r.Status == RecipeImportRecipeStatus.Ready)}, нужна проверка {all.Count(r => r.Status == RecipeImportRecipeStatus.NeedsReview)}");
        await SaveAsync(ct);
        return await GetAsync(ctx, id, ct);
    }

    // ---------- Создание черновиков ----------

    /// <summary>
    /// Создать черновики техкарт по готовым рецептурам (все готовые или выбранные): новая карта — CreateAsync; другой состав у существующей —
    /// черновик новой версии. Перед созданием — анализ заново. Рецептура «захватывается» условным обновлением статуса: двойное нажатие или
    /// второе окно её не создадут второй раз. Ошибка одной не мешает другим — она остаётся «нужна проверка» с причиной.
    /// </summary>
    public async Task<RecipeImportApplyResult> ApplyAsync(ProcurementContext ctx, Guid id, RecipeImportApplyInput input, CancellationToken ct)
    {
        RequireImport(ctx);
        ProductionRecipeImport import;
        List<ProductionRecipeImportRecipe> all;
        ImportLookups lookups;
        for (var attempt = 1; ; attempt++)
        {
            (import, all) = await LoadAsync(id, ct);
            lookups = await LookupsAsync(ct);
            RecipeImportAnalyzer.Analyze(all, lookups);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                // Другое окно в это время забрало рецептуры — перечитать: созданные уже «черновик создан» и анализом не трогаются.
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new FieldConflictException("Загрузку в это время меняют из другого окна — обновите страницу.");
            }
        }

        var wanted = input.RecipeIds is { Count: > 0 } ids ? ids.ToHashSet() : null;
        var targets = all.Where(r => r.Include && r.Status == RecipeImportRecipeStatus.Ready && (wanted is null || wanted.Contains(r.Id))).ToList();
        var rows = new List<RecipeImportApplyRow>();
        int created = 0, versions = 0, failed = 0;
        db.ChangeTracker.Clear();
        foreach (var r in targets)
        {
            var now = clock.GetUtcNow();
            var claimed = await db.ProductionRecipeImportRecipes.Where(x => x.Id == r.Id && x.Status == RecipeImportRecipeStatus.Ready)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, RecipeImportRecipeStatus.Applied).SetProperty(x => x.AppliedAt, now)
                    .SetProperty(x => x.AppliedById, ctx.UserId), ct);
            if (claimed == 0)
            {
                rows.Add(new RecipeImportApplyRow(r.Id, r.Name, "skipped", null, "Уже создаётся или создана из другого окна."));
                continue;
            }

            try
            {
                var lines = RecipeImportAnalyzer.CardLines(r, lookups) ?? throw new FieldValidationException("Состав не собирается — проверьте замечания.");
                var input2 = await CardInputAsync(r, lines, ct);
                Guid recipeId;
                if (r.Action == RecipeImportAction.NewVersion && r.ExistingRecipeId is { } existing)
                {
                    recipeId = await recipes.NewVersionFromAsync(ctx, existing, input2, $"из Excel «{import.FileName}», «{r.Sheet}» {r.Column}", ct);
                    versions++;
                }
                else
                {
                    recipeId = await recipes.CreateAsync(ctx, input2, ct);
                    created++;
                }

                await db.ProductionRecipeImportRecipes.Where(x => x.Id == r.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.RecipeId, recipeId), ct);
                rows.Add(new RecipeImportApplyRow(r.Id, r.Name, r.Action == RecipeImportAction.NewVersion ? "new_version" : "created", recipeId, null));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                failed++;
                var issues = RecipeImportAnalyzer.Issues(r.Issues).Append(new RecipeImportIssue(RecipeImportIssue.Block, "apply_failed", $"Черновик не создан: {e.Message}")).ToList();
                await db.ProductionRecipeImportRecipes.Where(x => x.Id == r.Id).ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.Status, RecipeImportRecipeStatus.NeedsReview).SetProperty(x => x.AppliedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.AppliedById, (Guid?)null).SetProperty(x => x.Issues, RecipeImportAnalyzer.Serialize(issues)), ct);
                rows.Add(new RecipeImportApplyRow(r.Id, r.Name, "failed", null, e.Message));
            }
        }

        var skipped = all.Count(r => r.Include && r.Status != RecipeImportRecipeStatus.Ready && (wanted is null || wanted.Contains(r.Id)));
        if (created + versions > 0)
        {
            await db.ProductionRecipeImports.Where(i => i.Id == id).ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, RecipeImportStatus.Applied)
                .SetProperty(i => i.AppliedAt, clock.GetUtcNow()).SetProperty(i => i.AppliedById, ctx.UserId), ct);
        }

        Log(ctx, id, "applied", $"Созданы черновики техкарт: новых {created}, новых версий {versions}; не созданы {failed}; не готовы или исключены {skipped}");
        await db.SaveChangesAsync(ct);
        return new RecipeImportApplyResult(created, versions, failed, skipped, rows);
    }

    /// <summary>
    /// Данные формы техкарты из рецептуры загрузки: название и выход — как в Excel, база — из листа, нормы на выход без этапов, товар Linko и
    /// позиция выхода (полуфабрикат); у новой версии карта (номер, технолог, замес, упаковка) — из прежней версии. Пометка — откуда и что
    /// осталось сделать технологу.
    /// </summary>
    private async Task<ProductionRecipeInput> CardInputAsync(ProductionRecipeImportRecipe r, IReadOnlyList<ImportCardLine> lines, CancellationToken ct)
    {
        var import = await db.ProductionRecipeImports.AsNoTracking().FirstAsync(i => i.Id == r.ImportId, ct);
        var negatives = r.Lines.Where(l => l.Kind == nameof(RecipeCellKind.Negative) && l.Decision != RecipeImportDecision.Exclude)
            .Select(l => $"{l.Name} {ExcelNumbers.Format(l.Value ?? 0)} {l.Unit}").ToList();
        var comment = $"Импорт из Excel «{import.FileName}», лист «{r.Sheet}», столбец {r.Column}: нормы на {ExcelNumbers.Format(r.BaseQuantity!.Value)} {r.BaseUnit} " +
                      $"({r.BaseLabel}). Требуется распределение по этапам технологом." +
                      (negatives.Count > 0 ? $" Возвратные отходы в Excel (в карту не перенесены): {string.Join(", ", negatives)}." : "");
        if (comment.Length > 2000)
        {
            comment = comment[..1999] + "…";
        }

        var cardLines = lines.Select(l => new ProductionRecipeLineInput(l.ItemId, l.Quantity, 0, l.Comment, null, ProductionLineBasis.Output, null, l.UnitCode)).ToList();
        var code = r.Code ?? r.Article!;
        if (r.Action == RecipeImportAction.NewVersion && r.ExistingRecipeId is { } existingId
            && await db.ProductionRecipes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == existingId, ct) is { } prev)
        {
            return new ProductionRecipeInput(null, r.Name.Trim(), r.LinkoProductId ?? prev.LinkoProductId, r.Name.Trim(), r.BaseUnit, r.BaseQuantity.Value, comment,
                cardLines, null, prev.TechCardNo, prev.TechnologistId, null, prev.BatchMinKg, prev.BatchMaxKg, prev.PartialBatches, prev.PieceKg, prev.PackKg,
                prev.PacksPerBox, null, null, r.OutputItemId ?? prev.OutputItemId);
        }

        return new ProductionRecipeInput(code, r.Name.Trim(), r.LinkoProductId, r.Name.Trim(), r.BaseUnit, r.BaseQuantity.Value, comment, cardLines,
            OutputItemId: r.OutputItemId);
    }

    // ---------- Общее ----------

    private async Task<(ProductionRecipeImport Import, List<ProductionRecipeImportRecipe> Recipes)> LoadAsync(Guid id, CancellationToken ct)
    {
        var import = await db.ProductionRecipeImports.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new FieldNotFoundException("Загрузка не найдена.");
        var list = await db.ProductionRecipeImportRecipes.Include(r => r.Lines).Where(r => r.ImportId == id).ToListAsync(ct);
        return (import, list);
    }

    private static void Editable(ProductionRecipeImportRecipe recipe)
    {
        if (recipe.Status == RecipeImportRecipeStatus.Applied)
        {
            throw new FieldConflictException("По этой рецептуре черновик уже создан — правьте техкарту.");
        }
    }

    private async Task ReanalyzeAsync(List<ProductionRecipeImportRecipe> all, CancellationToken ct) => RecipeImportAnalyzer.Analyze(all, await LookupsAsync(ct));

    /// <summary>Позиция склада строки: точный артикул; иначе сопоставление, запомненное сотрудником (артикул и название); ручное в этой загрузке не трогается.</summary>
    private static void Match(IEnumerable<ProductionRecipeImportRecipe> recipes, ImportLookups lookups, IReadOnlyDictionary<(string, string), Guid> maps)
    {
        foreach (var line in recipes.SelectMany(r => r.Lines))
        {
            if (line.Match == RecipeImportMatch.Manual && line.ItemId is { } manual && lookups.CatalogById.ContainsKey(manual))
            {
                continue;
            }

            if (line.Article is { } a && lookups.CatalogBySku.GetValueOrDefault(a) is { } item)
            {
                (line.ItemId, line.Match) = (item.Id, RecipeImportMatch.Sku);
            }
            else if (maps.GetValueOrDefault((line.Article ?? "", line.Name ?? "")) is var saved && saved != Guid.Empty && lookups.CatalogById.ContainsKey(saved))
            {
                (line.ItemId, line.Match) = (saved, RecipeImportMatch.Saved);
            }
            else
            {
                (line.ItemId, line.Match) = (null, RecipeImportMatch.None);
            }
        }
    }

    private async Task RememberAsync(ProcurementContext ctx, string? article, string? name, Guid itemId, CancellationToken ct)
    {
        var (a, n) = (article ?? "", name ?? "");
        var map = await db.ProductionRecipeImportItemMaps.FirstOrDefaultAsync(m => m.Article == a && m.Name == n, ct);
        if (map is null)
        {
            db.ProductionRecipeImportItemMaps.Add(new ProductionRecipeImportItemMap { Article = Cut(a, 64)!, Name = Cut(n, 500)!, ItemId = itemId, CreatedById = ctx.UserId });
        }
        else
        {
            map.ItemId = itemId;
            map.CreatedById = ctx.UserId;
            map.UpdatedAt = clock.GetUtcNow();
        }
    }

    private async Task<IReadOnlyDictionary<(string, string), Guid>> MapsAsync(CancellationToken ct) =>
        (await db.ProductionRecipeImportItemMaps.AsNoTracking().ToListAsync(ct)).ToDictionary(m => (m.Article, m.Name), m => m.ItemId);

    /// <summary>Каталог склада, товары Linko, карты OneBase (отпечатки составов) — для анализа.</summary>
    private async Task<ImportLookups> LookupsAsync(CancellationToken ct)
    {
        var items = await db.ProcurementItems.AsNoTracking().Include(i => i.Units).ToListAsync(ct);
        var catalog = items.Select(i => new ImportCatalogItem(i.Id, i.Sku, i.Name, i.UnitCode, i.IsActive, i.Units.Select(u => u.UnitCode).ToList())).ToList();
        var linko = await db.LinkoProducts.AsNoTracking().Where(p => p.Code != null).Select(p => new ImportLinkoProduct(p.Id, p.Code, p.Name)).ToListAsync(ct);
        var cards = await db.ProductionRecipes.AsNoTracking().Include(r => r.Lines).Where(r => r.Status != ProductionRecipeStatus.Archived
            || !db.ProductionRecipes.Any(o => o.Code == r.Code && o.Status != ProductionRecipeStatus.Archived)).ToListAsync(ct);
        var stages = (await db.ProductionRecipeStages.AsNoTracking().Select(s => s.RecipeId).Distinct().ToListAsync(ct)).ToHashSet();
        return new ImportLookups(
            catalog.ToDictionary(i => i.Sku, StringComparer.Ordinal),
            catalog.ToDictionary(i => i.Id),
            linko.GroupBy(p => p.Code!.Trim(), StringComparer.Ordinal).ToDictionary(g => g.Key, g => (IReadOnlyList<ImportLinkoProduct>)g.ToList(), StringComparer.Ordinal),
            cards.GroupBy(c => c.Code, StringComparer.Ordinal).ToDictionary(g => g.Key, g => (IReadOnlyList<ImportExistingCard>)g.Select(c =>
                new ImportExistingCard(c.Id, c.Code, c.VersionNo, c.Status, stages.Contains(c.Id), RecipeImportAnalyzer.Fingerprint(c))).ToList(), StringComparer.Ordinal));
    }

    private static RecipeImportTotals Totals(ICollection<ProductionRecipeImportRecipe> list)
    {
        var lines = list.SelectMany(r => r.Lines).ToList();
        var positive = lines.Where(l => l.Kind == nameof(RecipeCellKind.Number)).ToList();
        var issues = list.Select(r => RecipeImportAnalyzer.Issues(r.Issues)).ToList();
        return new RecipeImportTotals(list.Count, list.Count(r => r.Lines.Any(l => l.Kind == nameof(RecipeCellKind.Number))),
            list.Count(r => r.Status == RecipeImportRecipeStatus.Empty), list.Count(r => r.Status == RecipeImportRecipeStatus.Duplicate),
            list.Count(r => r.Status == RecipeImportRecipeStatus.Ready && r.Include), list.Count(r => r.Status == RecipeImportRecipeStatus.NeedsReview && r.Include),
            list.Count(r => r.Status == RecipeImportRecipeStatus.Unchanged), list.Count(r => r.Status == RecipeImportRecipeStatus.Applied), list.Count(r => !r.Include),
            positive.Count, lines.Count(l => l.Kind == nameof(RecipeCellKind.Zero)), lines.Count(l => l.Kind == nameof(RecipeCellKind.Negative)),
            lines.Count(l => l.Kind == nameof(RecipeCellKind.Error)), lines.Count(l => l.Kind == nameof(RecipeCellKind.Text)), positive.Count(l => l.ItemId is not null),
            positive.Count(l => l.ItemId is null), positive.Where(l => l.ItemId is null).Select(l => l.Article ?? "").Distinct().Count(),
            list.Count(r => r.IsSemiFinished && r.Lines.Any(l => l.Kind == nameof(RecipeCellKind.Number))),
            issues.Sum(i => i.Count(x => x.Level == RecipeImportIssue.Block)), issues.Sum(i => i.Count(x => x.Level == RecipeImportIssue.Warn)));
    }

    private static RecipeImportRecipeRow Row(ProductionRecipeImportRecipe r)
    {
        var issues = RecipeImportAnalyzer.Issues(r.Issues);
        var positive = r.Lines.Where(l => l.Kind == nameof(RecipeCellKind.Number)).ToList();
        var first = issues.FirstOrDefault(i => i.Level == RecipeImportIssue.Block) ?? issues.FirstOrDefault(i => i.Level == RecipeImportIssue.Warn);
        return new RecipeImportRecipeRow(r.Id, r.Sheet, r.Column, r.Article, r.Name, r.Code ?? r.Article, r.BaseQuantity, r.BaseUnit, positive.Count,
            positive.Count(l => l.ItemId is not null), positive.Count(l => l.ItemId is null),
            r.Lines.Count(l => l.Kind is nameof(RecipeCellKind.Error) or nameof(RecipeCellKind.Text)), r.Status.ToString(), r.Action.ToString(), r.Include,
            r.IsSemiFinished, issues.Count(i => i.Level == RecipeImportIssue.Block), issues.Count(i => i.Level == RecipeImportIssue.Warn), first?.Text, r.RecipeId);
    }

    private static IReadOnlyList<RecipeImportSheetView> Sheets(ProductionRecipeImport import) =>
        JsonSerializer.Deserialize<List<RecipeImportSheetView>>(import.Sheets, Json) ?? [];

    private static Dictionary<string, int> Order(ProductionRecipeImport import) =>
        Sheets(import).Select((s, i) => (s.Name, i)).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().i);

    private async Task<Dictionary<Guid, string>> Names(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new FieldConflictException("Загрузку уже изменили в другом окне — обновите страницу.");
        }
    }

    private void Log(ProcurementContext ctx, Guid id, string action, string text) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProductionRules.RecipeImportDoc, DocId = id, ActorId = ctx.UserId, Action = action, Text = text.Length <= 1000 ? text : text[..999] + "…",
        });

    private static string? Cut(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}

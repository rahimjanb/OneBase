using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OneBase.Domain.Production;

namespace OneBase.Application.Production.Import;

/// <summary>Замечание по рецептуре: block — черновик не создаётся, пока не решено; warn — проверьте; info — для сведения. Cell — ячейка Excel.</summary>
public sealed record RecipeImportIssue(string Level, string Code, string Text, string? Cell = null)
{
    public const string Block = "block";
    public const string Warn = "warn";
    public const string Info = "info";
}

/// <summary>Позиция каталога склада для сопоставления: артикул, название, единица склада, включена ли, единицы закупки.</summary>
public sealed record ImportCatalogItem(Guid Id, string Sku, string Name, string UnitCode, bool Active, IReadOnlyCollection<string> PurchaseUnits);

public sealed record ImportLinkoProduct(long Id, string? Code, string Name);

/// <summary>Карта OneBase с тем же кодом: последняя версия, статус, есть ли этапы, отпечаток состава, создана ли этой же загрузкой.</summary>
public sealed record ImportExistingCard(Guid Id, string Code, int VersionNo, ProductionRecipeStatus Status, bool HasStages, string Fingerprint);

/// <summary>Что есть в OneBase для анализа: каталог по артикулу, товары Linko по коду, карты по коду (действующая, неутверждённая, последняя).</summary>
public sealed record ImportLookups(
    IReadOnlyDictionary<string, ImportCatalogItem> CatalogBySku,
    IReadOnlyDictionary<Guid, ImportCatalogItem> CatalogById,
    IReadOnlyDictionary<string, IReadOnlyList<ImportLinkoProduct>> LinkoByCode,
    IReadOnlyDictionary<string, IReadOnlyList<ImportExistingCard>> CardsByCode);

/// <summary>Строка будущей карты: позиция склада, норма (как в Excel или введённая технологом), единица строки (null — единица позиции), ячейка.</summary>
public sealed record ImportCardLine(Guid ItemId, decimal Quantity, string? UnitCode, string Cell, string Comment);

/// <summary>
/// Анализ рецептур загрузки — без базы: проверки Excel (база, артикул, ошибки и текст в нормах, точность, отрицательные «отходы», сумма «Вес»
/// и итог листа), дубли столбцов, полуфабрикаты, сопоставление со складом (артикул, запомненное, ручное; единица; включена ли), товар
/// Linko, сравнение с картой OneBase того же кода. Результат — в полях рецептур и строк: статус, действие, замечания, отпечаток состава.
/// </summary>
public static class RecipeImportAnalyzer
{
    public const int MaxScale = 6;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Analyze(IReadOnlyList<ProductionRecipeImportRecipe> recipes, ImportLookups lookups)
    {
        // Полуфабрикат — продукт, который входит компонентом в другие рецептуры файла.
        var componentArticles = recipes.SelectMany(r => r.Lines).Where(l => l.Kind == nameof(RecipeCellKind.Number) && l.Article is { Length: > 0 })
            .Select(l => l.Article!).ToHashSet(StringComparer.Ordinal);
        var groups = recipes.Where(r => r.Article is { Length: > 0 } && Positive(r).Any()).GroupBy(r => r.Article!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var recipe in recipes)
        {
            var issues = new List<RecipeImportIssue>();
            recipe.IsSemiFinished = recipe.Article is { } a && componentArticles.Contains(a);
            if (recipe.Status == RecipeImportRecipeStatus.Applied)
            {
                continue; // черновик уже создан — анализ его не меняет
            }

            if (!Positive(recipe).Any() && !recipe.Lines.Any(l => l.Kind is nameof(RecipeCellKind.Error) or nameof(RecipeCellKind.Text)))
            {
                recipe.Status = RecipeImportRecipeStatus.Empty;
                recipe.Action = RecipeImportAction.None;
                recipe.Fingerprint = null;
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Info, "empty", "Нет норм больше нуля — рецептура пустая, карта не создаётся.", Cell(recipe)));
                recipe.Issues = Serialize(issues);
                continue;
            }

            // Тот же артикул в нескольких столбцах.
            var duplicateOf = (ProductionRecipeImportRecipe?)null;
            if (recipe.Article is { } article && groups.TryGetValue(article, out var group))
            {
                var same = group.All(g => Composition(g) == Composition(recipe) && g.Name.Trim() == recipe.Name.Trim());
                var included = group.Where(g => g.Include).ToList();
                if (same)
                {
                    var primary = group.First();
                    if (!ReferenceEquals(primary, recipe))
                    {
                        duplicateOf = primary;
                    }
                }
                else if (recipe.Include && included.Count > 1)
                {
                    var others = string.Join(", ", group.Where(g => !ReferenceEquals(g, recipe)).Select(g => $"«{g.Sheet}» {g.Column} «{g.Name}»"));
                    issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "article_conflict",
                        $"Артикул {article} есть и в других столбцах с другим составом или названием: {others}. Оставьте один столбец — другие исключите.", Cell(recipe)));
                }
            }

            if (duplicateOf is not null)
            {
                recipe.Status = RecipeImportRecipeStatus.Duplicate;
                recipe.Action = RecipeImportAction.None;
                recipe.Fingerprint = null;
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Info, "duplicate",
                    $"Дубль: тот же продукт и тот же состав, что в «{duplicateOf.Sheet}» {duplicateOf.Column}. Карта создаётся один раз — по нему.", Cell(recipe)));
                recipe.Issues = Serialize(issues);
                continue;
            }

            if (recipe.BaseQuantity is null || recipe.BaseUnit is null)
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "no_base",
                    "Не определено, на какое количество заданы нормы (нет подписи «1 тонна учун кг») — карта не создаётся."));
            }

            var code = recipe.Code ?? recipe.Article;
            if (code is null)
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "no_article", "У продукта нет артикула — введите код техкарты.", Cell(recipe)));
            }
            else if (!ProductionRules.ValidCode(code))
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "bad_code",
                    $"Артикул «{code}» не подходит для кода техкарты (до 32 знаков: буквы, цифры, «-», «_», «.») — введите код.", Cell(recipe)));
            }

            if (recipe.Name.Trim().Length > 200)
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "long_name",
                    $"Название продукта длиннее 200 знаков ({recipe.Name.Trim().Length}) — техкарта хранит до 200, название не сокращается автоматически.", Cell(recipe)));
            }

            var cardLines = CardLines(recipe, lookups, issues);
            WeightCheck(recipe, issues);
            Negatives(recipe, issues);

            // Полуфабрикат: выход — позиция склада с тем же артикулом.
            recipe.OutputItemId = null;
            if (recipe.IsSemiFinished && code is not null)
            {
                if (lookups.CatalogBySku.GetValueOrDefault(code) is { } output)
                {
                    if (!output.Active)
                    {
                        issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "output_inactive",
                            $"Полуфабрикат «{output.Name}» ({output.Sku}) отключён в каталоге склада."));
                    }
                    else if (ProductionCalc.Convert(1, recipe.BaseUnit ?? "кг", output.UnitCode) is null)
                    {
                        issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "output_unit",
                            $"Полуфабрикат «{output.Name}» ведётся в «{output.UnitCode}» — выход в «{recipe.BaseUnit}» в эту единицу не переводится."));
                    }
                    else
                    {
                        recipe.OutputItemId = output.Id;
                    }
                }
                else
                {
                    issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "semi_no_item",
                        $"Полуфабрикат {code} «{recipe.Name}» входит в другие рецептуры, но не заведён в каталоге склада — заведите позицию с артикулом {code}: " +
                        "выход приходуется на неё, а другие карты выдают её со склада."));
                }
            }

            Linko(recipe, code, lookups, issues);

            var fingerprint = cardLines is null || recipe.BaseQuantity is null ? null : Fingerprint(recipe.BaseQuantity.Value, recipe.BaseUnit!, recipe.OutputItemId, cardLines);
            recipe.Fingerprint = fingerprint;
            recipe.ExistingRecipeId = null;
            var action = RecipeImportAction.Create;
            var unchanged = false;
            if (code is not null && lookups.CardsByCode.GetValueOrDefault(code) is { Count: > 0 } cards)
            {
                var open = cards.FirstOrDefault(c => c.Status is ProductionRecipeStatus.Draft or ProductionRecipeStatus.PendingApproval);
                var active = cards.FirstOrDefault(c => c.Status == ProductionRecipeStatus.Active);
                var latest = cards.OrderByDescending(c => c.VersionNo).First();
                if (open is not null)
                {
                    recipe.ExistingRecipeId = open.Id;
                    if (fingerprint is not null && open.Fingerprint == fingerprint)
                    {
                        unchanged = true;
                        issues.Add(new RecipeImportIssue(RecipeImportIssue.Info, "same_open",
                            $"У техкарты {code} уже есть {(open.Status == ProductionRecipeStatus.Draft ? "черновик" : "версия на утверждении")} {open.VersionNo} с этим же составом."));
                    }
                    else
                    {
                        issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "open_version",
                            $"У техкарты {code} есть {(open.Status == ProductionRecipeStatus.Draft ? "черновик" : "версия на утверждении")} {open.VersionNo} с другим составом — " +
                            "сначала утвердите или верните её, затем загрузите снова."));
                    }
                }
                else
                {
                    var basis = active ?? latest;
                    recipe.ExistingRecipeId = basis.Id;
                    if (fingerprint is not null && basis.Fingerprint == fingerprint)
                    {
                        unchanged = true;
                        issues.Add(new RecipeImportIssue(RecipeImportIssue.Info, "same_card",
                            $"Состав совпадает с {(basis.Status == ProductionRecipeStatus.Active ? "действующей" : "последней")} версией {basis.VersionNo} техкарты {code}."));
                    }
                    else if (basis.HasStages)
                    {
                        issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "card_has_stages",
                            $"Техкарта {code} (версия {basis.VersionNo}) уже распределена технологом по этапам — состав из Excel не переносится поверх этапов. " +
                            "Изменения переносит технолог: «Новая версия» в карте."));
                    }
                    else
                    {
                        action = RecipeImportAction.NewVersion;
                        issues.Add(new RecipeImportIssue(RecipeImportIssue.Info, "new_version",
                            $"Состав отличается от версии {basis.VersionNo} техкарты {code} — будет создан черновик новой версии; утверждённая версия не меняется."));
                    }
                }
            }

            recipe.Issues = Serialize(issues);
            if (issues.Any(i => i.Level == RecipeImportIssue.Block))
            {
                recipe.Status = RecipeImportRecipeStatus.NeedsReview;
                recipe.Action = RecipeImportAction.None;
            }
            else if (unchanged)
            {
                recipe.Status = RecipeImportRecipeStatus.Unchanged;
                recipe.Action = RecipeImportAction.None;
            }
            else
            {
                recipe.Status = RecipeImportRecipeStatus.Ready;
                recipe.Action = action;
            }
        }
    }

    /// <summary>
    /// Строки будущей карты по строкам Excel: норма больше нуля (или введённая технологом), позиция склада, единица. Ошибки и текст без решения,
    /// больше 6 знаков, нет позиции, единица не переводится, позиция повторяется — замечание block; null — карту собрать нельзя.
    /// </summary>
    public static IReadOnlyList<ImportCardLine>? CardLines(ProductionRecipeImportRecipe recipe, ImportLookups lookups, List<RecipeImportIssue>? issues = null) =>
        Collect(recipe, lookups, issues ?? []) is var (lines, blocked) && !blocked ? lines : null;

    /// <summary>Строки, которые уже можно перенести в карту, — даже если по другим строкам рецептура не готова (для сверки на экране).</summary>
    public static IReadOnlyList<ImportCardLine> ReadyLines(ProductionRecipeImportRecipe recipe, ImportLookups lookups) => Collect(recipe, lookups, []).Lines;

    private static (IReadOnlyList<ImportCardLine> Lines, bool Blocked) Collect(ProductionRecipeImportRecipe recipe, ImportLookups lookups, List<RecipeImportIssue> issues)
    {
        var result = new List<ImportCardLine>();
        var blocked = false;
        foreach (var line in recipe.Lines.OrderBy(l => l.Row))
        {
            var label = $"{line.Article ?? "без артикула"} «{line.Name ?? "без названия"}»";
            if (line.Decision == RecipeImportDecision.Exclude)
            {
                if (line.Kind != nameof(RecipeCellKind.Zero))
                {
                    issues.Add(new RecipeImportIssue(RecipeImportIssue.Info, "excluded",
                        $"{label} не переносится — решение технолога{(line.DecisionComment is { } c ? $": {c}" : "")}.", line.Cell));
                }

                continue;
            }

            decimal? value;
            switch (line.Kind)
            {
                case nameof(RecipeCellKind.Zero):
                case nameof(RecipeCellKind.Negative):
                    continue; // ноль — не входит в состав; отрицательные — отдельным замечанием
                case nameof(RecipeCellKind.Error):
                case nameof(RecipeCellKind.Text):
                    if (line.Decision == RecipeImportDecision.ManualValue && line.ManualValue is { } manual)
                    {
                        value = manual;
                        break;
                    }

                    issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, line.Kind == nameof(RecipeCellKind.Error) ? "cell_error" : "cell_text",
                        line.Kind == nameof(RecipeCellKind.Error)
                            ? $"{label}: в ячейке ошибка формулы {line.Text}{(line.Formula is { Length: > 0 } f && f != "shared" ? $" (={f})" : "")} — значение не подставляется. Введите норму вручную или не переносите строку."
                            : $"{label}: в ячейке текст «{line.Text}», а не число — не исправляется автоматически. Введите норму вручную или не переносите строку.",
                        line.Cell));
                    blocked = true;
                    continue;
                default:
                    value = line.Decision == RecipeImportDecision.ManualValue && line.ManualValue is { } m ? m : line.Value;
                    break;
            }

            if (value is not { } quantity || quantity <= 0)
            {
                continue;
            }

            if (ExcelNumbers.Scale(quantity) > MaxScale)
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "precision",
                    $"{label}: норма {ExcelNumbers.Format(quantity)} — больше {MaxScale} знаков после запятой, а техкарта хранит {MaxScale}. Значение не округляется: " +
                    "введите норму вручную или не переносите строку.", line.Cell));
                blocked = true;
                continue;
            }

            if (line.ItemId is not { } itemId || lookups.CatalogById.GetValueOrDefault(itemId) is not { } item)
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "no_item",
                    $"{label}: позиции с таким артикулом нет в каталоге склада — сопоставьте вручную или заведите позицию.", line.Cell));
                blocked = true;
                continue;
            }

            if (!item.Active)
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "item_inactive", $"{label}: позиция «{item.Name}» отключена в каталоге склада.", line.Cell));
                blocked = true;
                continue;
            }

            var unit = line.Unit?.Trim();
            string? lineUnit;
            if (string.IsNullOrEmpty(unit))
            {
                if (line.Decision != RecipeImportDecision.UnitConfirmed)
                {
                    issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "no_unit",
                        $"{label}: единица в Excel не указана. Подтвердите единицу позиции склада «{item.UnitCode}» или не переносите строку.", line.Cell));
                    blocked = true;
                    continue;
                }

                lineUnit = null;
            }
            else if (unit == item.UnitCode)
            {
                lineUnit = null;
            }
            else if (ProductionCalc.Convert(1, unit, item.UnitCode) is not null || item.PurchaseUnits.Contains(unit))
            {
                lineUnit = unit;
            }
            else
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "unit_mismatch",
                    $"{label}: единица в Excel «{unit}», у позиции склада — «{item.UnitCode}», и одна в другую не переводится. Единицы не меняются автоматически.",
                    line.Cell));
                blocked = true;
                continue;
            }

            if (line.Name is { } name && Squash(name) != Squash(item.Name))
            {
                issues.Add(new RecipeImportIssue(RecipeImportIssue.Info, "name_differs",
                    $"{label}: в каталоге склада по этому артикулу — «{item.Name}».", line.Cell));
            }

            result.Add(new ImportCardLine(item.Id, quantity, lineUnit, line.Cell, $"Excel: «{recipe.Sheet}» {line.Cell}"));
        }

        foreach (var dup in result.GroupBy(l => l.ItemId).Where(g => g.Count() > 1))
        {
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "duplicate_item",
                $"«{lookups.CatalogById[dup.Key].Name}» дважды в составе ({string.Join(", ", dup.Select(l => l.Cell))}) — в техкарте позиция одна строка. " +
                "Не переносите одну из строк; повтор не складывается автоматически."));
            blocked = true;
        }

        if (result.Count > ProductionRules.MaxLines)
        {
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "too_many", $"Компонентов {result.Count} — в техкарте до {ProductionRules.MaxLines}."));
            blocked = true;
        }

        if (result.Count == 0 && !blocked)
        {
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Block, "no_lines", "Нет ни одной строки для техкарты."));
            blocked = true;
        }

        return (result, blocked);
    }

    /// <summary>Сумма компонентов с отметкой «Вес» против итога листа «1 тонна учун кг» — только предупреждение, итог не исправляется.</summary>
    private static void WeightCheck(ProductionRecipeImportRecipe recipe, List<RecipeImportIssue> issues)
    {
        var sum = Positive(recipe).Where(l => l.Weight?.Trim() == "1").Sum(l => l.Value ?? 0);
        if (sum == 0 && Positive(recipe).Any())
        {
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Warn, "no_mass",
                $"В рецептуре нет сырья, входящего в массу («Вес»): только {Positive(recipe).Count()} строк(и) упаковки и прочего. Проверьте, заполнен ли столбец {recipe.Column}.",
                Cell(recipe)));
        }

        if (recipe.TotalCell is null)
        {
            return;
        }

        if (ExcelNumbers.Parse(recipe.TotalRaw) is not { } total)
        {
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Warn, "total_error",
                $"Итог листа «на тонну» в {recipe.TotalCell} — {(string.IsNullOrEmpty(recipe.TotalRaw) ? "пусто" : $"«{recipe.TotalRaw}»")}{(recipe.TotalFormula is { Length: > 0 } f && f != "shared" ? $" (={f})" : "")}: " +
                $"сверить сумму сырья ({ExcelNumbers.Format(sum)} кг) не с чем.", recipe.TotalCell));
        }
        else if (Math.Abs(total - sum) > 0.01m)
        {
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Warn, "total_mismatch",
                $"Сумма сырья с отметкой «Вес» — {ExcelNumbers.Format(sum)} кг, а итог листа в {recipe.TotalCell} — {ExcelNumbers.Format(total)} кг" +
                $"{(recipe.TotalFormula is { Length: > 0 } f && f != "shared" ? $" (={f})" : "")}. Нормы переносятся как в ячейках, итог не исправляется.",
                recipe.TotalCell));
        }
    }

    /// <summary>Отрицательные нормы — возвратные отходы («Отход Кекс», «Отход этикетка»): в техкарту (норма больше нуля) не переносятся.</summary>
    private static void Negatives(ProductionRecipeImportRecipe recipe, List<RecipeImportIssue> issues)
    {
        foreach (var line in recipe.Lines.Where(l => l.Kind == nameof(RecipeCellKind.Negative) && l.Decision != RecipeImportDecision.Exclude))
        {
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Warn, "negative",
                $"{line.Article} «{line.Name}» {ExcelNumbers.Format(line.Value ?? 0)} {line.Unit} — отрицательная норма (возвратные отходы) в техкарту не переносится: " +
                "в OneBase норма больше нуля. Учитывайте отходы отдельно.", line.Cell));
        }
    }

    private static void Linko(ProductionRecipeImportRecipe recipe, string? code, ImportLookups lookups, List<RecipeImportIssue> issues)
    {
        recipe.LinkoSuggestedId = null;
        if (recipe.LinkoProductId is not null || code is null || recipe.IsSemiFinished)
        {
            return;
        }

        if (lookups.LinkoByCode.GetValueOrDefault(code) is { Count: 1 } exact)
        {
            recipe.LinkoProductId = exact[0].Id;
            return;
        }

        var number = code.Contains('-') ? code[(code.LastIndexOf('-') + 1)..] : null;
        if (number is { Length: > 0 } && number.All(char.IsAsciiDigit) && lookups.LinkoByCode.GetValueOrDefault(number) is { Count: 1 } byNumber)
        {
            recipe.LinkoSuggestedId = byNumber[0].Id;
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Warn, "linko_suggested",
                $"Товар Linko с кодом {number} — «{byNumber[0].Name}»: похоже на этот продукт по номеру артикула. Подтвердите или выберите другой."));
        }
        else
        {
            issues.Add(new RecipeImportIssue(RecipeImportIssue.Info, "no_linko", "Товар Linko не найден по артикулу — карту можно создать и без него."));
        }
    }

    /// <summary>Отпечаток состава карты: база, позиция выхода, строки (позиция, норма, единица) по порядку позиций.</summary>
    public static string Fingerprint(decimal baseQuantity, string baseUnit, Guid? outputItemId, IEnumerable<ImportCardLine> lines)
    {
        var text = new StringBuilder($"{ExcelNumbers.Format(baseQuantity)}|{baseUnit}|{outputItemId}");
        foreach (var l in lines.OrderBy(l => l.ItemId))
        {
            text.Append($";{l.ItemId}={ExcelNumbers.Format(l.Quantity)}{l.UnitCode}");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>Отпечаток карты OneBase тем же способом (строки на выход без этапа; карта с этапами сравнивается как «другая»).</summary>
    public static string Fingerprint(ProductionRecipe card) =>
        Fingerprint(card.OutputQuantity, card.OutputUnit, card.OutputItemId,
            card.Lines.Select(l => new ImportCardLine(l.ItemId, l.Quantity, l.UnitCode, "", "")));

    public static IReadOnlyList<RecipeImportIssue> Issues(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<RecipeImportIssue>>(json, Json) ?? [];

    public static string Serialize(IReadOnlyList<RecipeImportIssue> issues) => JsonSerializer.Serialize(issues, Json);

    private static IEnumerable<ProductionRecipeImportLine> Positive(ProductionRecipeImportRecipe r) => r.Lines.Where(l => l.Kind == nameof(RecipeCellKind.Number));

    /// <summary>Состав как в Excel (для дублей столбцов): артикул и значение каждой непустой ячейки по строкам.</summary>
    private static string Composition(ProductionRecipeImportRecipe r) =>
        string.Join(";", r.Lines.Where(l => l.Kind != nameof(RecipeCellKind.Zero)).OrderBy(l => l.Article, StringComparer.Ordinal).ThenBy(l => l.Raw, StringComparer.Ordinal)
            .Select(l => $"{l.Article}={l.Raw}|{l.Kind}"));

    private static string Cell(ProductionRecipeImportRecipe r) => $"{r.Column}{r.HeaderRow}";

    private static string Squash(string s) => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}

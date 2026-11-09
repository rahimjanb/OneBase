using OneBase.Domain.Common;

namespace OneBase.Domain.Production;

/// <summary>Загрузка Excel: разобрана (можно проверять, сопоставлять и создавать черновики) или из неё уже создавались черновики.</summary>
public enum RecipeImportStatus
{
    Analyzed,
    Applied,
}

/// <summary>
/// Рецептура загрузки: готова (можно создать черновик), нужна ручная проверка, дубль другого столбца (тот же продукт, тот же состав),
/// пустая (нет норм больше нуля), без изменений (состав совпадает с картой OneBase), черновик создан.
/// </summary>
public enum RecipeImportRecipeStatus
{
    Ready,
    NeedsReview,
    Duplicate,
    Empty,
    Unchanged,
    Applied,
}

/// <summary>Что сделает загрузка с картой: создаст новую, создаст новую версию существующей, ничего (без изменений или не готова).</summary>
public enum RecipeImportAction
{
    None,
    Create,
    NewVersion,
}

/// <summary>Как найдена позиция склада компонента: по артикулу, по сопоставлению, запомненному сотрудником, вручную в этой загрузке, не найдена.</summary>
public enum RecipeImportMatch
{
    None,
    Sku,
    Saved,
    Manual,
}

/// <summary>
/// Решение технолога по строке Excel: нет (как в файле), не переносить в карту (с причиной), норма введена вручную (ошибка или текст в
/// ячейке), единица подтверждена (в Excel не указана — берётся единица позиции склада).
/// </summary>
public enum RecipeImportDecision
{
    None,
    Exclude,
    ManualValue,
    UnitConfirmed,
}

/// <summary>
/// Загрузка файла техкарт (Производство → Технолог → Техкарты → «Импортировать из Excel»): имя, SHA-256 содержимого (тот же файл повторно —
/// та же загрузка), размер, кто и когда, сводка по листам (JSON), когда последний раз создавались черновики. Version — xmin.
/// </summary>
public class ProductionRecipeImport : Entity
{
    public required string FileName { get; set; }
    public required string FileHash { get; set; }
    public long FileSize { get; set; }
    public RecipeImportStatus Status { get; set; } = RecipeImportStatus.Analyzed;
    public required string Sheets { get; set; }
    public Guid CreatedById { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public Guid? AppliedById { get; set; }
    public uint Version { get; set; }
    public ICollection<ProductionRecipeImportRecipe> Recipes { get; set; } = [];
}

/// <summary>
/// Рецептура из файла (столбец продукта): ключ источника «лист | артикул», лист и столбец, артикул и название продукта как в Excel, группа,
/// база норм (количество, единица, откуда), итог листа «на тонну» и потери (как в файле), код карты (артикул или введённый технологом),
/// товар Linko (точный код или подтверждённый), позиция склада выхода (полуфабрикат), включать ли в создание, статус, замечания (JSON),
/// отпечаток состава (для «без изменений»), что будет сделано и что сделано (карта OneBase).
/// </summary>
public class ProductionRecipeImportRecipe : Entity
{
    public Guid ImportId { get; set; }
    public ProductionRecipeImport? Import { get; set; }
    public required string SourceKey { get; set; }
    public required string Sheet { get; set; }
    public required string Column { get; set; }
    public int HeaderRow { get; set; }
    public string? Article { get; set; }
    public required string Name { get; set; }
    public string? GroupName { get; set; }
    public decimal? BaseQuantity { get; set; }
    public string? BaseUnit { get; set; }
    public string? BaseLabel { get; set; }
    public string? TotalCell { get; set; }
    public string? TotalRaw { get; set; }
    public string? TotalFormula { get; set; }
    public string? LossCell { get; set; }
    public string? LossRaw { get; set; }
    public string? Code { get; set; }
    public long? LinkoProductId { get; set; }
    public long? LinkoSuggestedId { get; set; }
    public bool IsSemiFinished { get; set; }
    public Guid? OutputItemId { get; set; }
    public bool Include { get; set; } = true;
    public RecipeImportRecipeStatus Status { get; set; } = RecipeImportRecipeStatus.NeedsReview;
    public RecipeImportAction Action { get; set; }
    public required string Issues { get; set; }
    public string? Fingerprint { get; set; }
    public Guid? ExistingRecipeId { get; set; }
    public Guid? RecipeId { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public Guid? AppliedById { get; set; }

    /// <summary>xmin: анализ не перезапишет рецептуру, которую в это время забрало создание черновика.</summary>
    public uint Version { get; set; }

    public ICollection<ProductionRecipeImportLine> Lines { get; set; } = [];
}

/// <summary>
/// Строка Excel в рецептуре: строка и ячейка, артикул, название, единица и «Вес» как в файле, что в ячейке (число, ноль, отрицательное,
/// ошибка, текст), исходная запись и формула, число как в Excel (точно), позиция склада и как найдена, решение технолога (не переносить,
/// норма вручную, единица подтверждена) с причиной, кто и когда решил.
/// </summary>
public class ProductionRecipeImportLine : Entity
{
    public Guid ImportRecipeId { get; set; }
    public int Row { get; set; }
    public required string Cell { get; set; }
    public string? Article { get; set; }
    public string? Name { get; set; }
    public string? Unit { get; set; }
    public string? Weight { get; set; }
    public required string Kind { get; set; }
    public string? Raw { get; set; }
    public string? Formula { get; set; }
    public decimal? Value { get; set; }
    public string? Text { get; set; }
    public Guid? ItemId { get; set; }
    public RecipeImportMatch Match { get; set; }
    public RecipeImportDecision Decision { get; set; }
    public decimal? ManualValue { get; set; }
    public string? DecisionComment { get; set; }
    public Guid? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

/// <summary>
/// Сопоставление «артикул и название в Excel → позиция склада», подтверждённое сотрудником: следующие загрузки находят позицию сами
/// (пометка «запомнено»). Похожие названия сами не объединяются — только так.
/// </summary>
public class ProductionRecipeImportItemMap : Entity
{
    public required string Article { get; set; }
    public required string Name { get; set; }
    public Guid ItemId { get; set; }
    public Guid CreatedById { get; set; }
}

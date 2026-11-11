using OneBase.Domain.Common;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;

namespace OneBase.Domain.Production;

/// <summary>
/// Состояние версии рецептуры: черновик (правится) → на утверждении (не правится; вернуть — в черновик) → утверждена, она же действующая
/// (одна на код) → архивная.
/// </summary>
public enum ProductionRecipeStatus
{
    Draft,
    PendingApproval,
    Active,
    Archived,
}

/// <summary>Тип операции этапа: замес (компоненты — на замес, только первый этап), выпечка, начинка, глазурь, добавки, охлаждение, упаковка, другое.</summary>
public enum ProductionStageKind
{
    Mixing,
    Baking,
    Filling,
    Glazing,
    Adding,
    Cooling,
    Packing,
    Other,
}

/// <summary>
/// База норматива брака этапа: Output — от обработанного на этапе (компоненты этапа на брак потрачены); Input — от поступившего на этап
/// (отсеян до обработки: компоненты этапа не тратятся); Finished — от готовой продукции.
/// </summary>
public enum ProductionDefectBase
{
    Output,
    Input,
    Finished,
}

/// <summary>База нормы компонента: на выход рецептуры, на один замес (этап замеса), на штуку, на упаковку, на коробку (упаковка).</summary>
public enum ProductionLineBasis
{
    Output,
    Batch,
    Piece,
    Pack,
    Box,
}

/// <summary>
/// Рецептура (одна версия технологической карты): код и номер версии, название, выход — товар Linko (ID без внешнего ключа: схема linko
/// очищается при сбросе синхронизации), название и единица выхода, на какое количество выхода заданы нормы (например, 1 000 кг). Карта:
/// номер, технолог, дата начала действия; замес — мин. и макс. масса, частичный замес (стандартная масса — сумма рецептуры замеса);
/// упаковка — масса штуки и упаковки, упаковок в коробке. Этапы — цепочка изготовления; без этапов — прежняя рецептура (нормы на выход).
/// Черновик правится свободно; на утверждение → утверждает другой технолог или начальник производства; утверждённая версия — действующая,
/// прежняя уходит в архив; утверждённая и архивная не правятся — новая версия копирует их в черновик. Задание закрепляет версию. Version — xmin.
/// </summary>
public class ProductionRecipe : Entity
{
    public required string Code { get; set; }
    public int VersionNo { get; set; } = 1;
    public required string Name { get; set; }
    public long? LinkoProductId { get; set; }
    public required string OutputName { get; set; }
    public required string OutputUnit { get; set; }
    public decimal OutputQuantity { get; set; }
    public ProductionRecipeStatus Status { get; set; } = ProductionRecipeStatus.Draft;
    public DateTimeOffset? ActivatedAt { get; set; }
    public Guid? ActivatedById { get; set; }
    public User? ActivatedBy { get; set; }
    public string? Comment { get; set; }
    public Guid? CreatedById { get; set; }
    public string? TechCardNo { get; set; }
    public Guid? TechnologistId { get; set; }
    public User? Technologist { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public User? SubmittedBy { get; set; }
    public decimal? BatchMinKg { get; set; }
    public decimal? BatchMaxKg { get; set; }
    public bool PartialBatches { get; set; }
    public decimal? PieceKg { get; set; }
    public decimal? PackKg { get; set; }
    public int? PacksPerBox { get; set; }

    /// <summary>Позиция склада выхода (полуфабрикат или готовая продукция по решению): выход приходуется партией при закрытии задания.</summary>
    public Guid? OutputItemId { get; set; }
    public ProcurementItem? OutputItem { get; set; }

    public uint Version { get; set; }
    public ICollection<ProductionRecipeLine> Lines { get; set; } = [];
    public ICollection<ProductionRecipeStage> Stages { get; set; } = [];
    public ICollection<ProductionDefectType> DefectTypes { get; set; } = [];
}

/// <summary>
/// Фото продукта техкарты — одно на код, а не на версию: новая версия (в том числе из импорта Excel) показывает то же фото. Хранится уже
/// подготовленным: центральный кроп 4:3 без EXIF (до 1600 px), превью 960 и 480 px — JPEG в хранилище файлов. FileId меняется при каждой
/// загрузке — по нему адрес картинки, поэтому браузер кэширует её навсегда. Tint — средний цвет фото (подложка, пока картинка грузится).
/// </summary>
public class ProductionRecipePhoto : Entity
{
    public required string Code { get; set; }
    public Guid FileId { get; set; }
    public required string ObjectKey { get; set; }
    public required string ThumbKey { get; set; }
    public required string SmallKey { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeBytes { get; set; }
    public string? Tint { get; set; }
    public Guid? UploadedById { get; set; }
}

/// <summary>
/// Этап технологической цепочки версии: порядок (следующий этап — следующий по порядку), название, тип операции, участок; нормативный
/// выход, % (выход ÷ весь вход этапа с добавленными компонентами; мин. и макс. — допуск факта), база брака, влажность (норма, единица,
/// допустимое отклонение), можно ли перерабатывать брак. Норматив брака этапа — сумма видов брака продукта этого этапа.
/// </summary>
public class ProductionRecipeStage : Entity
{
    public Guid RecipeId { get; set; }
    public ProductionRecipe? Recipe { get; set; }
    public int StageNo { get; set; }
    public required string Name { get; set; }
    public ProductionStageKind Kind { get; set; } = ProductionStageKind.Other;
    public string? Area { get; set; }
    public decimal YieldPercent { get; set; } = 100;
    public decimal? YieldMinPercent { get; set; }
    public decimal? YieldMaxPercent { get; set; }
    public ProductionDefectBase DefectBase { get; set; } = ProductionDefectBase.Output;
    public decimal? MoistureNorm { get; set; }
    public string? MoistureUnit { get; set; }
    public decimal? MoistureTolerance { get; set; }
    public bool Reworkable { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Вид брака этапа: брак продукта (ItemId пусто; норматив — в брак этапа, расход материалов — по месту этапа в цепочке) или брак материала
/// этапа (ItemId — позиция строки этапа, например этикетки; норматив — к расходу этого материала, продукт не трогает); переработка.
/// </summary>
public class ProductionDefectType : Entity
{
    public Guid RecipeId { get; set; }
    public ProductionRecipe? Recipe { get; set; }
    public Guid StageId { get; set; }
    public ProductionRecipeStage? Stage { get; set; }
    public required string Name { get; set; }
    public Guid? ItemId { get; set; }
    public ProcurementItem? Item { get; set; }
    public decimal NormPercent { get; set; }
    public bool Reworkable { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Строка рецептуры: позиция склада, этап (у рецептуры без этапов — пусто), база нормы, норма в единице строки (не указана — единица позиции;
/// единицы массы переводятся), масса единицы в кг (для единиц не массы — сумма замеса и масса потока), группа, технологические потери, %.
/// </summary>
public class ProductionRecipeLine : Entity
{
    public Guid RecipeId { get; set; }
    public ProductionRecipe? Recipe { get; set; }
    public int LineNo { get; set; }
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }
    public Guid? StageId { get; set; }
    public ProductionRecipeStage? Stage { get; set; }
    public ProductionLineBasis Basis { get; set; } = ProductionLineBasis.Output;
    public string? GroupName { get; set; }
    public string? UnitCode { get; set; }
    public decimal? UnitKg { get; set; }
    public decimal Quantity { get; set; }
    public decimal LossPercent { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Задание на выпуск: черновик → выпущено (потребность — снимок) → в работе (первая выдача) → закрыто (фактический выход); отменено.</summary>
public enum ProductionOrderStatus
{
    Draft,
    Released,
    InProgress,
    Completed,
    Cancelled,
}

/// <summary>
/// Задание на выпуск PRD-: закреплённая версия рецептуры, цех (отдел), склад выдачи сырья, план и дата, мастер. Строки — потребность по нормам
/// (снимок при выпуске), выдано и возвращено (меняются документами выдачи и возврата той же записью). Закрытие — фактический выход: норма на
/// факт и отклонение по каждой позиции. Готовая продукция на склад OneBase не приходуется — ею владеет Linko. Version — xmin.
/// </summary>
public class ProductionOrder : Entity
{
    public required string Number { get; set; }
    public Guid RecipeId { get; set; }
    public ProductionRecipe? Recipe { get; set; }
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid WarehouseId { get; set; }
    public ProcurementWarehouse? Warehouse { get; set; }
    public decimal PlannedQty { get; set; }
    public DateOnly PlannedDate { get; set; }
    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Draft;
    public decimal? OutputQty { get; set; }
    public Guid? ResponsibleId { get; set; }
    public User? Responsible { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? ClientKey { get; set; }

    /// <summary>Снимок планового расчёта на план (этапы, замесы, компоненты) — JSON; изменения формул и версий не меняют посчитанное задание.</summary>
    public string? PlanSnapshot { get; set; }

    public uint Version { get; set; }
    public ICollection<ProductionOrderLine> Lines { get; set; } = [];
}

/// <summary>
/// Строка задания: позиция, норма рецептуры на её выход и потери (снимок), потребность на план; выдано и возвращено; при закрытии — норма на
/// фактический выход и пометка перерасхода. Фактический расход = выдано − возвращено.
/// </summary>
public class ProductionOrderLine : Entity
{
    public Guid OrderId { get; set; }
    public ProductionOrder? Order { get; set; }
    public int LineNo { get; set; }
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }
    public decimal NormQty { get; set; }
    public decimal LossPercent { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal IssuedQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public decimal? ActualNormQty { get; set; }
    public bool Overconsumed { get; set; }
}

/// <summary>Смена: ввод (оператор) → закрыта (проверки пройдены, ждёт утверждения) → утверждена (не правится); исправленная корректировкой — заменена; отменена.</summary>
public enum ProductionShiftStatus
{
    Draft,
    Closed,
    Approved,
    Superseded,
    Cancelled,
}

/// <summary>Сменный документ или корректировка утверждённого (копия с исправлениями; после утверждения заменяет исправляемый).</summary>
public enum ProductionShiftKind
{
    Regular,
    Correction,
}

/// <summary>Брак и переработка: запрещён к переработке, разрешён, направлен на переработку.</summary>
public enum ProductionReworkStatus
{
    Forbidden,
    Allowed,
    Sent,
}

/// <summary>Материал смены: фактическое взвешивание расхода или брак материала (этикетки, плёнка, коробки) — в единице склада.</summary>
public enum ProductionShiftMaterialKind
{
    Weighed,
    Defect,
}

/// <summary>
/// Сменный отчёт SHF- по заданию на выпуск (версия рецептуры — из задания): дата и номер смены, линия, оператор, начальник смены; годный
/// выход, упаковок и коробок; замесы, результаты этапов (выпечка — до и после, причина отклонения; возврат переработки), брак по видам,
/// взвешенный расход и брак материалов. Закрытие — с проверкой полноты; утверждённая не правится — исправляет корректировка (Kind
/// Correction, CorrectsId), которая после утверждения заменяет исправляемую (Superseded). Version — xmin (автосохранение).
/// </summary>
public class ProductionShift : Entity
{
    public required string Number { get; set; }
    public Guid OrderId { get; set; }
    public ProductionOrder? Order { get; set; }
    public ProductionShiftKind Kind { get; set; } = ProductionShiftKind.Regular;
    public Guid? CorrectsId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public int ShiftNo { get; set; } = 1;
    public string? Line { get; set; }
    public Guid OperatorId { get; set; }
    public User? Operator { get; set; }
    public Guid? SupervisorId { get; set; }
    public User? Supervisor { get; set; }
    public ProductionShiftStatus Status { get; set; } = ProductionShiftStatus.Draft;
    public decimal? GoodOutput { get; set; }
    public decimal? Packs { get; set; }
    public decimal? Boxes { get; set; }
    public string? Comment { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedById { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? ClientKey { get; set; }
    public uint Version { get; set; }
    public ICollection<ProductionShiftBatch> Batches { get; set; } = [];
    public ICollection<ProductionShiftStage> Stages { get; set; } = [];
    public ICollection<ProductionShiftDefect> Defects { get; set; } = [];
    public ICollection<ProductionShiftMaterial> Materials { get; set; } = [];
}

/// <summary>Фактический замес смены: порядковый номер и масса, кг.</summary>
public class ProductionShiftBatch : Entity
{
    public Guid ShiftId { get; set; }
    public int No { get; set; }
    public decimal MassKg { get; set; }
}

/// <summary>
/// Результат этапа в смене: этап заполнен (Done — иначе расчёт неполный), масса до и после (выпечка), влажность, причина отклонения выхода от
/// допуска, возвращено в производство на этот этап из переработки брака (кг — свежее сырьё на него не тратится), комментарий.
/// </summary>
public class ProductionShiftStage : Entity
{
    public Guid ShiftId { get; set; }
    public int StageNo { get; set; }
    public bool Done { get; set; }
    public decimal? InputKg { get; set; }
    public decimal? OutputKg { get; set; }
    public decimal? Moisture { get; set; }
    public decimal? ReworkKg { get; set; }
    public string? Reason { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Брак продукта в смене: этап, вид брака версии (название — снимок), кг, переработка.</summary>
public class ProductionShiftDefect : Entity
{
    public Guid ShiftId { get; set; }
    public int StageNo { get; set; }
    public Guid? DefectTypeId { get; set; }
    public required string Name { get; set; }
    public decimal QuantityKg { get; set; }
    public ProductionReworkStatus Rework { get; set; } = ProductionReworkStatus.Forbidden;
}

/// <summary>Материал в смене: позиция склада (строка рецептуры), этап, взвешено или брак материала, количество в единице склада.</summary>
public class ProductionShiftMaterial : Entity
{
    public Guid ShiftId { get; set; }
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }
    public int? StageNo { get; set; }
    public ProductionShiftMaterialKind Kind { get; set; }
    public decimal Quantity { get; set; }
}

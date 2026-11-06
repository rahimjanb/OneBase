using OneBase.Domain.Common;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;

namespace OneBase.Domain.Production;

/// <summary>Состояние версии рецептуры: черновик (правится), действующая (одна на код), архивная.</summary>
public enum ProductionRecipeStatus
{
    Draft,
    Active,
    Archived,
}

/// <summary>
/// Рецептура (одна версия): код и номер версии, название, выход — товар Linko (ID без внешнего ключа: схема linko очищается при сбросе
/// синхронизации), название и единица выхода, на какое количество выхода заданы нормы. Черновик правится свободно; «ввести в действие»
/// делает версию действующей, прежняя действующая уходит в архив; действующая и архивная не правятся — новая версия копирует их в
/// черновик. Задание на выпуск закрепляет версию. Version — xmin.
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
    public uint Version { get; set; }
    public ICollection<ProductionRecipeLine> Lines { get; set; } = [];
}

/// <summary>Строка рецептуры: позиция склада, норма в единице позиции на выход рецептуры, технологические потери, %.</summary>
public class ProductionRecipeLine : Entity
{
    public Guid RecipeId { get; set; }
    public ProductionRecipe? Recipe { get; set; }
    public int LineNo { get; set; }
    public Guid ItemId { get; set; }
    public ProcurementItem? Item { get; set; }
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

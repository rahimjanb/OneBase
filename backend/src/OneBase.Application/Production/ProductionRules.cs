using System.Text.RegularExpressions;
using OneBase.Domain.Production;

namespace OneBase.Application.Production;

/// <summary>
/// Правила производства (чистые функции — проверяются тестами): потребность по норме (без этапов; цепочка — ProductionCalc), отклонение факта
/// от нормы, перерасход, переходы заданий на выпуск и версий рецептуры, проверка кода рецептуры.
/// </summary>
public static partial class ProductionRules
{
    public const string OrderPrefix = "PRD";
    public const string RecipeDoc = "recipe";
    public const string OrderDoc = "production_order";
    public const int MaxLines = 100;
    public const decimal MaxQuantity = 1_000_000_000m;

    /// <summary>
    /// Потребность в позиции на план: норма × (план ÷ выход рецептуры) × (1 + потери ÷ 100), до 0,001 единицы позиции (половина — вверх).
    /// Выход не больше нуля — 0 (такую рецептуру не ввести в действие).
    /// </summary>
    public static decimal Requirement(decimal norm, decimal planned, decimal output, decimal lossPercent) =>
        output <= 0 ? 0 : decimal.Round(norm * planned / output * (100 + lossPercent) / 100, 3, MidpointRounding.AwayFromZero);

    /// <summary>Отклонение факта от нормы: количество (плюс — перерасход) и процент от нормы до 0,1 (нормы нет — процента нет).</summary>
    public static (decimal Quantity, decimal? Percent) Deviation(decimal actual, decimal norm) =>
        (actual - norm, norm == 0 ? null : decimal.Round((actual - norm) / norm * 100, 1, MidpointRounding.AwayFromZero));

    /// <summary>Перерасход больше порога: факт выше нормы больше чем на threshold %; при нулевой норме — любой расход.</summary>
    public static bool Overconsumed(decimal actual, decimal norm, decimal thresholdPercent) =>
        norm > 0 ? actual > norm * (100 + thresholdPercent) / 100 : actual > 0;

    /// <summary>Сколько ещё выдать до потребности (не меньше нуля): потребность − (выдано − возвращено).</summary>
    public static decimal LeftToIssue(decimal required, decimal issued, decimal returned) => Math.Max(0, required - (issued - returned));

    public static bool Editable(ProductionOrderStatus status) => status == ProductionOrderStatus.Draft;

    /// <summary>По заданию выдают и возвращают, пока оно выпущено или в работе.</summary>
    public static bool Issuable(ProductionOrderStatus status) => status is ProductionOrderStatus.Released or ProductionOrderStatus.InProgress;

    /// <summary>Закрыть можно выпущенное или в работе; отменить — черновик, выпущенное или в работе, если на руках у цеха ничего нет.</summary>
    public static bool Cancellable(ProductionOrderStatus status, decimal netIssued) =>
        (status is ProductionOrderStatus.Draft or ProductionOrderStatus.Released or ProductionOrderStatus.InProgress) && netIssued == 0;

    public static string Label(ProductionOrderStatus status) => status switch
    {
        ProductionOrderStatus.Draft => "Черновик",
        ProductionOrderStatus.Released => "Выпущено",
        ProductionOrderStatus.InProgress => "В работе",
        ProductionOrderStatus.Completed => "Закрыто",
        _ => "Отменено",
    };

    public static string Label(ProductionRecipeStatus status) => status switch
    {
        ProductionRecipeStatus.Draft => "Черновик",
        ProductionRecipeStatus.PendingApproval => "На утверждении",
        ProductionRecipeStatus.Active => "Утверждена",
        _ => "Архив",
    };

    /// <summary>Неутверждённая версия: черновик или на утверждении — у кода одна.</summary>
    public static readonly ProductionRecipeStatus[] OpenStatuses = [ProductionRecipeStatus.Draft, ProductionRecipeStatus.PendingApproval];

    public static bool Open(ProductionRecipeStatus status) => status is ProductionRecipeStatus.Draft or ProductionRecipeStatus.PendingApproval;

    public static string Label(ProductionStageKind kind) => kind switch
    {
        ProductionStageKind.Mixing => "Замес",
        ProductionStageKind.Baking => "Выпечка",
        ProductionStageKind.Filling => "Начинка",
        ProductionStageKind.Glazing => "Глазурь",
        ProductionStageKind.Adding => "Добавки",
        ProductionStageKind.Cooling => "Охлаждение",
        ProductionStageKind.Packing => "Упаковка",
        _ => "Другое",
    };

    public static string Label(ProductionDefectBase basis) => basis switch
    {
        ProductionDefectBase.Input => "от поступившего на этап",
        ProductionDefectBase.Finished => "от готовой продукции",
        _ => "от обработанного на этапе",
    };

    /// <summary>Код рецептуры: 1–32 знака — буквы, цифры, «-», «_», «.»; пробелы по краям убираются, регистр сохраняется.</summary>
    public static bool ValidCode(string? code) => code is { Length: > 0 and <= 32 } && CodePattern().IsMatch(code);

    /// <summary>Количество с не более чем digits знаками после запятой, больше нуля и не больше MaxQuantity.</summary>
    public static bool ValidQuantity(decimal value, int digits) => value > 0 && value <= MaxQuantity && decimal.Round(value, digits) == value;

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N}._\-]*$")]
    private static partial Regex CodePattern();
}

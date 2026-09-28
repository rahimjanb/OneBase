using OneBase.Domain.Sales;

namespace OneBase.Application.Sales;

public enum PlanImportTarget
{
    /// <summary>План региона (РОП/завод): регион × категория × месяц.</summary>
    Region,

    /// <summary>План ТП: агент × категория × месяц.</summary>
    Agent,
}

public sealed record PlanImportResult(int Imported, IReadOnlyList<string> Errors);

/// <summary>Импорт планов из Excel (.xlsx) или CSV и шаблон для заполнения.</summary>
public interface ISalesPlanImporter
{
    Task<PlanImportResult> ImportAsync(
        Stream file,
        string fileName,
        PlanImportTarget target,
        PlanKind kind,
        CancellationToken cancellationToken = default);

    Task<byte[]> BuildTemplateAsync(PlanImportTarget target, CancellationToken cancellationToken = default);
}

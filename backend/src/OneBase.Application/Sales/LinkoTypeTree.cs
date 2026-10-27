using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Sales;

/// <summary>Справочник типов товаров Linko: названия и родители (подтипы «Кекс 1,5 кг» → «Кекс»).</summary>
public sealed record LinkoTypeTree(IReadOnlyDictionary<long, string> Names, IReadOnlyDictionary<long, long?> Parents)
{
    public static async Task<LinkoTypeTree> LoadAsync(IAppDbContext db, CancellationToken ct)
    {
        var rows = await db.LinkoProductTypes.AsNoTracking().Select(t => new { t.Id, t.Name, t.ParentId }).ToListAsync(ct);
        return new LinkoTypeTree(rows.ToDictionary(t => t.Id, t => t.Name), rows.ToDictionary(t => t.Id, t => t.ParentId));
    }
}

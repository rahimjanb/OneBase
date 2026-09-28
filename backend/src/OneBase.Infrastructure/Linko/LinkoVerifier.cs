using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Linko;

public sealed record LinkoVerifyRow(string Entity, long Linko, long OneBase, bool Ok);

public sealed record LinkoVerifyResult(DateOnly From, DateOnly To, IReadOnlyList<LinkoVerifyRow> Rows)
{
    public bool Ok => Rows.All(r => r.Ok);
}

/// <summary>Сверка полноты: *_count в Linko против числа строк в OneBase (документы — в окне истории).</summary>
public sealed class LinkoVerifier(LinkoClient client, OneBaseDbContext db, LinkoSyncService sync)
{
    public async Task<LinkoVerifyResult> VerifyAsync(CancellationToken ct = default)
    {
        var (from, to) = sync.BackfillWindow();
        var window = new Dictionary<string, string?>
        {
            ["begin_date"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["end_date"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        var checks = new (string Entity, IDictionary<string, string?>? Filter, Func<Task<int>> Rows)[]
        {
            ("users", null, () => db.LinkoUsers.CountAsync(ct)),
            ("product_types", null, () => db.LinkoProductTypes.CountAsync(ct)),
            ("products", null, () => db.LinkoProducts.CountAsync(ct)),
            ("borders", null, () => db.LinkoBorders.CountAsync(ct)),
            ("markets", null, () => db.LinkoMarkets.CountAsync(ct)),
            ("market_users", null, () => db.LinkoMarketUsers.CountAsync(ct)),
            ("orders", window, () => db.LinkoOrders.CountAsync(o => o.CreatedDate >= from && o.CreatedDate <= to, ct)),
            ("order_returns", window, () => db.LinkoOrderReturns.CountAsync(r => r.CreatedDate >= from && r.CreatedDate <= to, ct)),
            ("visits", window, () => db.LinkoVisits.CountAsync(v => v.Day >= from && v.Day <= to, ct)),
        };

        var rows = new List<LinkoVerifyRow>();
        foreach (var (entity, filter, local) in checks)
        {
            var remote = await client.CountAsync(entity, filter, ct);
            var count = await local();
            rows.Add(new LinkoVerifyRow(entity, remote, count, remote == count));
        }

        return new LinkoVerifyResult(from, to, rows);
    }
}

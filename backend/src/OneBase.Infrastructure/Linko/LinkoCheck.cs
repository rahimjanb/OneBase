using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Linko;

/// <summary>
/// Команда `dotnet run --project src/OneBase.Api -- linko-check [--full]`:
/// синхронизирует данные и сверяет *_count Linko с числом строк в нашей БД.
/// </summary>
public static class LinkoCheck
{
    public static async Task<int> RunAsync(IServiceProvider services, bool full)
    {
        var linko = services.GetRequiredService<LinkoOptions>();
        if (!linko.IsConfigured)
        {
            Console.Error.WriteLine("LINKO_BASE_URL / LINKO_TOKEN не заданы (.env).");
            return 1;
        }

        var coordinator = services.GetRequiredService<LinkoSyncCoordinator>();
        Console.WriteLine($"Синхронизация с {linko.BaseUrl} …");
        var report = await coordinator.RunAsync(full) ?? throw new InvalidOperationException("Синхронизация уже идёт");

        foreach (var e in report.Entities.Where(e => e.Error is not null))
        {
            Console.WriteLine($"  ОШИБКА {e.Entity}: {e.Error}");
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<LinkoClient>();
        var (from, to) = scope.ServiceProvider.GetRequiredService<LinkoSyncService>().BackfillWindow();
        var window = new Dictionary<string, string?>
        {
            ["begin_date"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["end_date"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        var checks = new (string Entity, IDictionary<string, string?>? Filter, Func<Task<int>> Rows)[]
        {
            ("users", null, () => db.LinkoUsers.CountAsync()),
            ("product_types", null, () => db.LinkoProductTypes.CountAsync()),
            ("products", null, () => db.LinkoProducts.CountAsync()),
            ("borders", null, () => db.LinkoBorders.CountAsync()),
            ("markets", null, () => db.LinkoMarkets.CountAsync()),
            ("market_users", null, () => db.LinkoMarketUsers.CountAsync()),
            ("orders", window, () => db.LinkoOrders.CountAsync(o => o.CreatedDate >= from && o.CreatedDate <= to)),
            ("order_returns", window, () => db.LinkoOrderReturns.CountAsync(r => r.CreatedDate >= from && r.CreatedDate <= to)),
            ("visits", window, () => db.LinkoVisits.CountAsync(v => v.Day >= from && v.Day <= to)),
        };

        Console.WriteLine();
        Console.WriteLine($"Окно документов: {from:dd.MM.yyyy} – {to:dd.MM.yyyy}");
        Console.WriteLine($"{"Сущность",-15} {"Linko",10} {"OneBase",10}  Итог");
        var mismatches = 0;
        foreach (var (entity, filter, rows) in checks)
        {
            var remote = await client.CountAsync(entity, filter);
            var local = await rows();
            var ok = remote == local;
            mismatches += ok ? 0 : 1;
            Console.WriteLine($"{entity,-15} {remote,10} {local,10}  {(ok ? "OK" : "РАСХОЖДЕНИЕ")}");
        }

        return mismatches == 0 && report.Success ? 0 : 2;
    }
}

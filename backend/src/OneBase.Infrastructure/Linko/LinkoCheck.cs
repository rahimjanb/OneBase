using Microsoft.Extensions.DependencyInjection;

namespace OneBase.Infrastructure.Linko;

/// <summary>
/// Команда `dotnet run --project src/OneBase.Api -- linko-check [--full]`:
/// синхронизирует данные и сверяет *_count Linko с числом строк в нашей БД.
/// Не запускайте одновременно с работающим API — две синхронизации будут мешать друг другу.
/// </summary>
public static class LinkoCheck
{
    public static async Task<int> RunAsync(IServiceProvider services, bool full)
    {
        var linko = await services.GetRequiredService<LinkoSettingsStore>().GetAsync();
        if (!linko.HasCredentials)
        {
            Console.Error.WriteLine("Linko не настроен: задайте адрес и токен в «Настройки → Интеграции → Продажи → Linko» или LINKO_BASE_URL / LINKO_TOKEN в .env.");
            return 1;
        }

        var coordinator = services.GetRequiredService<LinkoSyncCoordinator>();
        Console.WriteLine($"Синхронизация с {linko.BaseUrl} …");
        var report = await coordinator.RunAsync(full ? LinkoSyncMode.Full : LinkoSyncMode.Incremental) ?? throw new InvalidOperationException("Синхронизация уже идёт");

        foreach (var e in report.Entities.Where(e => e.Error is not null))
        {
            Console.WriteLine($"  ОШИБКА {e.Entity}: {e.Error}");
        }

        using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<LinkoVerifier>().VerifyAsync();

        Console.WriteLine();
        Console.WriteLine($"Окно документов: {result.From:dd.MM.yyyy} – {result.To:dd.MM.yyyy}");
        Console.WriteLine($"{"Сущность",-15} {"Linko",10} {"OneBase",10}  Итог");
        foreach (var row in result.Rows)
        {
            Console.WriteLine($"{row.Entity,-15} {row.Linko,10} {row.OneBase,10}  {(row.Ok ? "OK" : "РАСХОЖДЕНИЕ")}");
        }

        return result.Ok && report.Success ? 0 : 2;
    }
}

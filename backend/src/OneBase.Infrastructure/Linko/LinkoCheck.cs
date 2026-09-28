using System.Text.Json;
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

    /// <summary>
    /// Команда `linko-fields <сущность>`: какие поля реально приходят из Linko (только имена полей и типы значений,
    /// без самих данных; у логических полей — число true/false). Нужна, чтобы не гадать о структуре ответа.
    /// </summary>
    public static async Task<int> FieldsAsync(IServiceProvider services, string entity)
    {
        var fields = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);
        var total = await services.GetRequiredService<LinkoClient>().ReadAllAsync<JsonElement>(entity, null, page =>
        {
            foreach (var item in page)
            {
                Collect(item, "", fields);
            }

            return Task.CompletedTask;
        });

        Console.WriteLine($"{entity}: {total} записей");
        foreach (var (path, kinds) in fields)
        {
            Console.WriteLine($"  {path,-40} {string.Join(", ", kinds.Select(k => $"{k.Key}={k.Value}"))}");
        }

        return 0;
    }

    private static void Collect(JsonElement element, string prefix, SortedDictionary<string, SortedDictionary<string, int>> fields)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Collect(property.Value, prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}", fields);
                }

                return;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, $"{prefix}[]", fields);
                }

                return;
        }

        var kind = element.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.ValueKind.ToString().ToLowerInvariant(),
        };
        var kinds = fields.TryGetValue(prefix, out var existing) ? existing : fields[prefix] = new SortedDictionary<string, int>(StringComparer.Ordinal);
        kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
    }
}

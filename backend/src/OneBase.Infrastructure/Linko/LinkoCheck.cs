using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Команда `linko-audit [ГГГГ-ММ]`: сверка допущений о Linko API с фактическими ответами. Только GET-запросы,
    /// печатаются агрегаты (счётчики, форматы, распределения), без имён, телефонов и других персональных данных.
    /// </summary>
    public static async Task<int> AuditAsync(IServiceProvider services, string? monthArg)
    {
        var client = services.GetRequiredService<LinkoClient>();
        var month = monthArg is { Length: 7 } m && DateOnly.TryParse(m + "-01", out var parsed) ? parsed : new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        var monthEnd = month.AddMonths(1).AddDays(-1);
        string D(DateOnly d) => d.ToString("yyyy-MM-dd");
        var window = new Dictionary<string, string?> { ["begin_date"] = D(month), ["end_date"] = D(monthEnd) };

        async Task Step(string title, Func<Task> body)
        {
            Console.WriteLine();
            Console.WriteLine($"== {title}");
            try
            {
                await body();
            }
            catch (LinkoApiException ex)
            {
                Console.WriteLine($"   ошибка: {ex.Message}");
            }
        }

        async Task<long?> Count(string entity, IDictionary<string, string?>? filters = null)
        {
            try
            {
                return await client.CountAsync(entity, filters);
            }
            catch (LinkoApiException ex)
            {
                Console.WriteLine($"   {entity}_count: {ex.Message}");
                return null;
            }
        }

        Console.WriteLine($"Месяц проверки: {month:yyyy-MM}");

        await Step("Заказы: поля и даты (одна страница за 20-е число месяца)", async () =>
        {
            var day = D(month.AddDays(19));
            var page = await client.GetJsonAsync("orders/", new Dictionary<string, string?> { ["begin_date"] = day, ["end_date"] = day, ["limit"] = "1000", ["offset"] = "0" });
            var rows = page.GetProperty("results").EnumerateArray().ToList();
            Console.WriteLine($"   заказов на странице: {rows.Count}");
            Console.WriteLine($"   поля заказа: {string.Join(", ", rows.SelectMany(r => r.EnumerateObject().Select(p => p.Name)).Distinct().Order())}");

            var delivered = rows.Where(r => Str(r, "status") == "delivered").ToList();
            var accepted = delivered.Select(r => Str(r, "accepted_time")).ToList();
            Console.WriteLine($"   delivered: {delivered.Count}, accepted_time пустой у {accepted.Count(a => string.IsNullOrEmpty(a))}");
            Console.WriteLine($"   форматы accepted_time: {string.Join("; ", accepted.Where(a => !string.IsNullOrEmpty(a)).GroupBy(Shape).Select(g => $"{g.Key} ×{g.Count()}"))}");

            var times = accepted.Select(a => DateTime.TryParse(a, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t) ? t : (DateTime?)null).Where(t => t != null).Select(t => t!.Value).ToList();
            Console.WriteLine($"   часы accepted_time (как пришли): {string.Join(" ", Enumerable.Range(0, 24).Select(h => $"{h}:{times.Count(t => t.Hour == h)}"))}");
            var lags = delivered
                .Select(r => (Created: DateOnly.TryParse(Str(r, "created_date"), out var c) ? c : (DateOnly?)null, Accepted: DateTime.TryParse(Str(r, "accepted_time"), out var a) ? DateOnly.FromDateTime(a) : (DateOnly?)null))
                .Where(x => x.Created != null && x.Accepted != null)
                .GroupBy(x => x.Accepted!.Value.DayNumber - x.Created!.Value.DayNumber)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key:+0;-0;0} дн.: {g.Count()}");
            Console.WriteLine($"   лаг accepted − created: {string.Join(", ", lags)}");
            var deliveryDiffers = delivered.Count(r => DateTime.TryParse(Str(r, "accepted_time"), out var a) && Str(r, "date_delivery") != DateOnly.FromDateTime(a).ToString("yyyy-MM-dd"));
            Console.WriteLine($"   date_delivery ≠ дате accepted_time: {deliveryDiffers} из {delivered.Count}");
            Console.WriteLine($"   статусы на странице: {string.Join(", ", rows.GroupBy(r => Str(r, "status")).Select(g => $"{g.Key} {g.Count()}"))}");

            // Часовой пояс accepted_time: tm — момент последнего изменения в UTC (unix). У заказа, который после приёмки
            // не меняли, accepted_time и tm — один момент: разница 0 ч — accepted_time в UTC, +5 ч — по Ташкенту.
            var shifts = delivered
                .Select(r => (Accepted: DateTime.TryParse(Str(r, "accepted_time"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var a) ? a : (DateTime?)null,
                    Tm: double.TryParse(Str(r, "tm"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tm) ? DateTimeOffset.FromUnixTimeMilliseconds((long)(tm * 1000)).UtcDateTime : (DateTime?)null))
                .Where(x => x.Accepted != null && x.Tm != null)
                .Select(x => Math.Round((x.Accepted!.Value - x.Tm!.Value).TotalHours, 1))
                .ToList();
            Console.WriteLine($"   accepted_time − tm(UTC), часы: {string.Join(", ", shifts.GroupBy(h => h).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key:+0.0;-0.0;0}: {g.Count()}"))}");
        });

        await Step("Заказы: фильтр is_synced (orders_count за месяц)", async () =>
        {
            Console.WriteLine($"   без is_synced: {await Count("orders", window)}");
            Console.WriteLine($"   is_synced=true: {await Count("orders", new Dictionary<string, string?>(window) { ["is_synced"] = "true" })}");
            Console.WriteLine($"   is_synced=false: {await Count("orders", new Dictionary<string, string?>(window) { ["is_synced"] = "false" })}");
            Console.WriteLine($"   status=delivered: {await Count("orders", new Dictionary<string, string?>(window) { ["status"] = "delivered" })}");
        });

        await Step("Возвраты: предел страницы и строки", async () =>
        {
            var total = await Count("order_returns", window);
            var page = await client.GetJsonAsync("order_returns/", new Dictionary<string, string?>(window) { ["limit"] = "1000", ["offset"] = "0" });
            var rows = page.GetProperty("results").EnumerateArray().ToList();
            Console.WriteLine($"   order_returns_count за месяц: {total}; запросили limit=1000 — пришло {rows.Count}");
            var lines = rows.SelectMany(r => r.TryGetProperty("returned_products", out var p) && p.ValueKind == JsonValueKind.Array ? p.EnumerateArray() : Enumerable.Empty<JsonElement>()).ToList();
            Console.WriteLine($"   поля строки возврата: {string.Join(", ", lines.SelectMany(l => l.EnumerateObject().Select(p => p.Name)).Distinct().Order())}");
            Console.WriteLine($"   строк с order_id: {lines.Count(l => l.TryGetProperty("order_id", out var o) && o.ValueKind == JsonValueKind.Number)} из {lines.Count}");
            var zeroHeader = rows.Count(r => Dec(r, "total_weight") == 0 && r.TryGetProperty("returned_products", out var p) && p.EnumerateArray().Any(l => Dec(l, "total_weight") > 0));
            Console.WriteLine($"   документов с нулевым весом шапки, но весом в строках: {zeroHeader} из {rows.Count}");
            Console.WriteLine($"   статусы: {string.Join(", ", rows.GroupBy(r => Str(r, "status")).Select(g => $"{g.Key} {g.Count()}"))}");
        });

        await Step("Визиты", async () =>
        {
            Console.WriteLine($"   visits_count за месяц: {await Count("visits", window)}");
            var page = await client.GetJsonAsync("visits/", new Dictionary<string, string?>(window) { ["limit"] = "1", ["offset"] = "0" });
            var row = page.GetProperty("results").EnumerateArray().FirstOrDefault();
            if (row.ValueKind == JsonValueKind.Object)
            {
                Console.WriteLine($"   поля визита: {string.Join(", ", row.EnumerateObject().Select(p => p.Name))}; формат date: {Shape(Str(row, "date"))}");
            }

            // Часы визитов за один день: рабочий день 8–19 в «как пришло» — местное время; 3–14 — UTC.
            var day = D(month.AddDays(19));
            var visits = await client.GetJsonAsync("visits/", new Dictionary<string, string?> { ["begin_date"] = day, ["end_date"] = day, ["status"] = "done", ["limit"] = "1000", ["offset"] = "0" });
            var hours = visits.GetProperty("results").EnumerateArray()
                .Select(v => DateTime.TryParse(Str(v, "date"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t) ? t.Hour : -1)
                .Where(h => h >= 0).ToList();
            Console.WriteLine($"   часы выполненных визитов за {day} ({hours.Count}): {string.Join(" ", Enumerable.Range(0, 24).Select(h => $"{h}:{hours.Count(x => x == h)}"))}");
        });

        await Step("Остатки (product_balances)", async () =>
        {
            Console.WriteLine($"   product_balances_count: {await Count("product_balances")}");
            var page = await client.GetJsonAsync("product_balances/", new Dictionary<string, string?> { ["limit"] = "1000", ["offset"] = "0" });
            var rows = page.GetProperty("results").EnumerateArray().ToList();
            Console.WriteLine($"   на странице: {rows.Count}; поля: {string.Join(", ", rows.SelectMany(r => r.EnumerateObject().Select(p => p.Name)).Distinct().Order())}");
            Console.WriteLine($"   тип balance: {string.Join(", ", rows.GroupBy(r => r.TryGetProperty("balance", out var b) ? b.ValueKind.ToString() : "нет").Select(g => $"{g.Key} ×{g.Count()}"))}");
            Console.WriteLine($"   balance = 0: {rows.Count(r => Dec(r, "balance") == 0)}, < 0: {rows.Count(r => Dec(r, "balance") < 0)}, дробных: {rows.Count(r => Dec(r, "balance") is { } v && v != decimal.Truncate(v))}");
            var today = D(DateOnly.FromDateTime(DateTime.Today));
            Console.WriteLine($"   balances_by_date_count на {today}: {await Count("balances_by_date", new Dictionary<string, string?> { ["date"] = today })}");
        });

        await Step("Склады и перемещения", async () =>
        {
            using var scope = services.CreateScope();
            var regions = (await scope.ServiceProvider.GetRequiredService<Persistence.OneBaseDbContext>().SalesRegions.Select(r => r.Name).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var stocks = await client.GetJsonAsync("stocks/", new Dictionary<string, string?> { ["limit"] = "1000", ["offset"] = "0" });
            var names = stocks.GetProperty("results").EnumerateArray().Select(s => (Id: Str(s, "id"), Name: Str(s, "name") ?? "")).ToList();
            Console.WriteLine($"   складов: {names.Count}; совпадают с регионом по имени: {names.Count(s => regions.Contains(s.Name.Trim()))}");
            Console.WriteLine($"   склады: {string.Join(" | ", names.Select(s => $"{s.Id}:{s.Name}{(regions.Contains(s.Name.Trim()) ? " ✓" : "")}"))}");

            Console.WriteLine($"   stock_transfers_count всего: {await Count("stock_transfers")}");
            var transfers = await client.GetJsonAsync("stock_transfers/", new Dictionary<string, string?>(window) { ["limit"] = "1000", ["offset"] = "0" });
            var rows = transfers.GetProperty("results").EnumerateArray().ToList();
            var stockName = names.Where(s => s.Id != null).ToDictionary(s => s.Id!, s => s.Name);
            string Stock(JsonElement t, string key) => t.TryGetProperty(key, out var s) && s.ValueKind == JsonValueKind.Object && stockName.TryGetValue(Str(s, "id") ?? "", out var n) ? n : "?";
            Console.WriteLine($"   перемещений за месяц (1-я страница): {rows.Count}; статусы: {string.Join(", ", rows.GroupBy(r => Str(r, "status")).Select(g => $"{g.Key} {g.Count()}"))}");
            Console.WriteLine($"   форматы created_date: {string.Join("; ", rows.GroupBy(r => Shape(Str(r, "created_date"))).Select(g => $"{g.Key} ×{g.Count()}"))}");
            foreach (var g in rows.GroupBy(r => $"{Stock(r, "from_stock")} → {Stock(r, "to_stock")}").OrderByDescending(g => g.Sum(r => Dec(r, "total_weight") ?? 0)).Take(12))
            {
                Console.WriteLine($"     {g.Key}: {g.Count()} шт., {g.Sum(r => Dec(r, "total_weight") ?? 0):N0} кг");
            }
        });

        await Step("Прочие ресурсы: количество записей", async () =>
        {
            foreach (var (entity, filters) in new (string, IDictionary<string, string?>?)[]
                     {
                         ("users", null), ("markets", null), ("market_users", null), ("products", null), ("product_types", null),
                         ("borders", null), ("contracts", null), ("price_lists", null), ("price_list_items", null), ("providers", null),
                         ("currencies", null), ("payments", window), ("kpi_plans", window), ("gears", null), ("market_balls", null), ("promotions", null),
                     })
            {
                Console.WriteLine($"   {entity}_count: {await Count(entity, filters)}");
            }

            var currencies = await client.GetJsonAsync("currencies/");
            Console.WriteLine($"   currencies (форма ответа): {currencies.ValueKind}");
        });

        return 0;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        } : null;

    private static decimal? Dec(JsonElement e, string name) =>
        decimal.TryParse(Str(e, name), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>Форма значения даты: цифры заменены на 9 — видно формат и наличие часового пояса, но не сами данные.</summary>
    private static string Shape(string? value) => value is null ? "null" : System.Text.RegularExpressions.Regex.Replace(value, "[0-9]", "9");

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

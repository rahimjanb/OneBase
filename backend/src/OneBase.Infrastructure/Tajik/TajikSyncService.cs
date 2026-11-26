using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales.Tajik;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Tajik;

/// <summary>
/// Загрузка 1С Таджикистан → OneBase (только чтение из 1С). Продажи: окно с 1-го числа прошлого месяца (первая загрузка и ночная сверка —
/// с даты начала истории); накладная — номер и дата. Новая — добавляется; изменённая — прежние строки получают RemovedAt, новые добавляются;
/// пропавшая из окна — RemovedAt (не удаляется). Если 1С вернула меньше половины накладных окна или часть строк не читается — пропавшие
/// не помечаются (защита от неполного ответа). Остаток — снимок дня: строки сегодняшнего дня заменяются, прошлые дни остаются.
/// </summary>
public sealed class TajikSyncService(
    OneBaseDbContext db,
    ITajikSettingsStore settings,
    TajikApiClient client,
    TajikDataVersion version,
    TimeProvider time,
    ILogger<TajikSyncService> logger)
{
    public const string Schedule = "schedule";
    public const string Manual = "manual";
    public const string Full = "full";

    public async Task<TajikSyncRun> RunAsync(string trigger, CancellationToken ct)
    {
        var started = time.GetUtcNow().ToUniversalTime();
        var run = new TajikSyncRun { StartedAt = started, Trigger = trigger, Status = "running" };
        db.TajikSyncRuns.Add(run);
        await db.SaveChangesAsync(ct);

        string? token = null;
        try
        {
            var s = await settings.GetAsync(ct);
            token = await settings.TokenAsync(ct);
            if (s.BaseUrl.Length == 0 || token is null)
            {
                throw new TajikApiException("Не заданы адрес API или ключ доступа 1С Таджикистан.");
            }

            var today = TajikText.Today(started);
            var hasData = await db.TajikSaleLines.AnyAsync(ct);
            var window = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            var from = trigger == Full || !hasData ? s.HistoryFrom : (window > s.HistoryFrom ? window : s.HistoryFrom);
            run.From = from;

            var page = await client.SalesAsync(s.BaseUrl, token, from, ct);
            await ApplySalesAsync(run, page, from, started, ct);

            string? stockError = null;
            try
            {
                var stock = await client.StockAsync(s.BaseUrl, token, ct);
                run.StockRows = await ApplyStockAsync(stock.Items, today, started, ct);
            }
            catch (TajikApiException e)
            {
                stockError = e.Message;
            }

            run.Status = stockError is null ? "ok" : "partial";
            run.Error = stockError is null ? null : "Остаток склада не загружен: " + stockError;
        }
        catch (TajikApiException e)
        {
            run.Status = "failed";
            run.Error = e.Message;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Неожиданная ошибка (БД и т. п.): в журнал — тип и текст без ключа.
            logger.LogError(e, "1С Таджикистан: синхронизация упала");
            run.Status = "failed";
            run.Error = TajikApiClient.Clean(e.GetType().Name + ": " + e.Message, token);
        }

        run.FinishedAt = time.GetUtcNow().ToUniversalTime();
        db.ChangeTracker.Clear();
        db.TajikSyncRuns.Update(run);
        await db.SaveChangesAsync(CancellationToken.None);
        version.Bump();
        if (run.Status == "failed")
        {
            logger.LogWarning("1С Таджикистан: синхронизация не прошла — {Error}", run.Error);
        }
        else
        {
            logger.LogInformation("1С Таджикистан: с {From:dd.MM.yyyy} — {Documents} накладных, новых {Added}, изменено {Changed}, пропало {Removed}; остаток — {Stock} строк",
                run.From, run.Documents, run.Added, run.Changed, run.Removed, run.StockRows);
        }

        return run;
    }

    private async Task ApplySalesAsync(TajikSyncRun run, TajikApiPage<TajikApiSale> page, DateOnly from, DateTimeOffset now, CancellationToken ct)
    {
        // API с неверной датой молча отдаёт «с 01.11» — строки раньше окна не трогаем.
        // Округление до точности столбцов — иначе сравнение с сохранённым видело бы «изменение» на каждом запуске.
        var items = page.Items.Where(i => i.Date >= from)
            .Select(i => i with
            {
                Qty = Math.Round(i.Qty, 3, MidpointRounding.AwayFromZero),
                Price = Math.Round(i.Price, 4, MidpointRounding.AwayFromZero),
                Amount = Math.Round(i.Amount, 2, MidpointRounding.AwayFromZero),
                Discount = Math.Round(i.Discount, 2, MidpointRounding.AwayFromZero),
                DiscountPercent = i.DiscountPercent is { } d ? Math.Clamp(Math.Round(d, 4, MidpointRounding.AwayFromZero), -99999m, 99999m) : null,
            })
            .ToList();
        run.Rejected = page.Rejected;
        run.Lines = items.Count;

        // Строки накладной API отдаёт в разном порядке от запроса к запросу (проверено 26.11.2026): номер строки — по постоянному ключу
        // (код, количество, цена, сумма…), сравнение с сохранённым — без учёта порядка.
        var fetched = new Dictionary<(string, DateOnly), List<TajikApiSale>>();
        foreach (var item in items)
        {
            var key = (item.Number, item.Date);
            if (!fetched.TryGetValue(key, out var list))
            {
                fetched[key] = list = [];
            }

            list.Add(item);
        }

        foreach (var key in fetched.Keys.ToList())
        {
            fetched[key] = fetched[key].OrderBy(l => Key(l.Code, l.Name, l.Qty, l.Price, l.Amount, l.Discount, l.DiscountPercent, l.Customer, l.Manager), StringComparer.Ordinal).ToList();
        }

        run.Documents = fetched.Count;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.TajikSaleLines.Where(l => l.RemovedAt == null && l.DocDate >= from).ToListAsync(ct);
        var stored = existing.GroupBy(l => (l.DocNumber, l.DocDate)).ToDictionary(g => g.Key, g => g.OrderBy(l => l.LineNo).ToList());
        if (stored.Count >= 20 && fetched.Count < stored.Count / 2)
        {
            throw new TajikApiException(
                $"1С Таджикистан вернула {fetched.Count} накладных с {from:dd.MM.yyyy}, а в OneBase их {stored.Count} — похоже на неполный ответ, ничего не изменено.");
        }

        foreach (var (key, lines) in fetched)
        {
            if (stored.TryGetValue(key, out var old))
            {
                if (Same(old, lines))
                {
                    continue;
                }

                foreach (var line in old)
                {
                    line.RemovedAt = now;
                }

                run.Changed++;
            }
            else
            {
                run.Added++;
            }

            db.TajikSaleLines.AddRange(lines.Select((l, i) => new TajikSaleLine
            {
                DocNumber = Cut(l.Number, 64),
                DocDate = l.Date,
                LineNo = i + 1,
                Customer = Cut(l.Customer, 500),
                Manager = l.Manager is null ? null : Cut(l.Manager, 200),
                ProductCode = Cut(l.Code, 64),
                ProductName = Cut(l.Name, 500),
                Qty = l.Qty,
                Price = l.Price,
                Amount = l.Amount,
                Discount = l.Discount,
                DiscountPercent = l.DiscountPercent,
                SyncedAt = now,
            }));
        }

        // Пропавшие накладные — только при полном и читаемом ответе.
        if (page.Rejected == 0)
        {
            foreach (var (key, old) in stored.Where(s => !fetched.ContainsKey(s.Key)))
            {
                foreach (var line in old)
                {
                    line.RemovedAt = now;
                }

                run.Removed++;
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<int> ApplyStockAsync(IReadOnlyList<TajikApiStock> items, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        // Одинаковый товар на одном складе дважды — складываем.
        var rows = items.GroupBy(i => (i.Code, i.Warehouse))
            .Select(g => new TajikStockItem
            {
                Date = today,
                ProductCode = Cut(g.Key.Code, 64),
                Warehouse = Cut(g.Key.Warehouse, 200),
                ProductName = Cut(g.Last().Name, 500),
                Qty = g.Sum(i => i.Qty),
                SyncedAt = now,
            })
            .ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.TajikStockItems.Where(s => s.Date == today).ExecuteDeleteAsync(ct);
        db.TajikStockItems.AddRange(rows);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return rows.Count;
    }

    /// <summary>Строка не длиннее столбца (длинное название из 1С не должно ронять загрузку).</summary>
    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    /// <summary>Ключ строки для порядка и сравнения: те же значения в том же виде, что сохраняются в БД (обрезка, точность столбцов).</summary>
    private static string Key(string code, string name, decimal qty, decimal price, decimal amount, decimal discount, decimal? percent, string customer, string? manager) =>
        string.Join('\u001f', Cut(code, 64), qty.ToString("0.000", CultureInfo.InvariantCulture), price.ToString("0.0000", CultureInfo.InvariantCulture),
            amount.ToString("0.00", CultureInfo.InvariantCulture), discount.ToString("0.00", CultureInfo.InvariantCulture),
            percent?.ToString("0.0000", CultureInfo.InvariantCulture) ?? "", Cut(name, 500), Cut(customer, 500), manager is null ? "" : Cut(manager, 200));

    /// <summary>Накладная не изменилась: тот же набор строк, порядок не важен.</summary>
    private static bool Same(List<TajikSaleLine> old, List<TajikApiSale> lines) =>
        old.Count == lines.Count
        && old.Select(l => Key(l.ProductCode, l.ProductName, l.Qty, l.Price, l.Amount, l.Discount, l.DiscountPercent, l.Customer, l.Manager)).Order(StringComparer.Ordinal)
            .SequenceEqual(lines.Select(l => Key(l.Code, l.Name, l.Qty, l.Price, l.Amount, l.Discount, l.DiscountPercent, l.Customer, l.Manager)).Order(StringComparer.Ordinal));
}

/// <summary>Один запуск за раз: кнопка «Синхронизировать» и расписание не идут параллельно.</summary>
public sealed class TajikSyncCoordinator(IServiceScopeFactory scopes)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool Running => _gate.CurrentCount == 0;

    /// <summary>Запуск; уже идёт другой — null.</summary>
    public async Task<TajikSyncRun?> RunAsync(string trigger, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0, ct))
        {
            return null;
        }

        try
        {
            using var scope = scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<TajikSyncService>().RunAsync(trigger, ct);
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// Расписание 1С Таджикистан: раз в интервал со страницы (30 минут по умолчанию) — окно с прошлого месяца; раз в сутки после 02:00 (Душанбе)
/// — сверка всей истории. Подключение не задано или выключено — ничего не делает.
/// </summary>
internal sealed class TajikSyncWorker(
    TajikSyncCoordinator coordinator,
    ITajikSettingsStore settings,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<TajikSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), time, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "1С Таджикистан: ошибка расписания синхронизации");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), time, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        if (!s.IsReady)
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var now = time.GetUtcNow().ToUniversalTime();
        var last = await db.TajikSyncRuns.AsNoTracking().OrderByDescending(r => r.Id).Select(r => (DateTimeOffset?)r.StartedAt).FirstOrDefaultAsync(ct);
        if (last is { } l && now - l < TimeSpan.FromMinutes(s.IntervalMinutes))
        {
            return;
        }

        var local = now.ToOffset(TajikText.Offset);
        var dayStart = new DateTimeOffset(local.Date, TajikText.Offset).ToUniversalTime();
        var fullToday = await db.TajikSyncRuns.AsNoTracking().AnyAsync(r => r.Trigger == TajikSyncService.Full && r.Status != "failed" && r.StartedAt >= dayStart, ct);
        var trigger = !fullToday && local.Hour >= 2 ? TajikSyncService.Full : TajikSyncService.Schedule;
        await coordinator.RunAsync(trigger, ct);
    }
}

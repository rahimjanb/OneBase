using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales;
using OneBase.Domain.Integrations;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Linko;

public sealed record LinkoEntityResult(string Entity, int Rows, string? Error);

public sealed record LinkoSyncReport(DateTimeOffset StartedAt, DateTimeOffset FinishedAt, IReadOnlyList<LinkoEntityResult> Entities)
{
    public bool Success => Entities.All(e => e.Error is null);
}

/// <summary>
/// Загружает данные Linko в нашу БД. Только чтение из Linko: POST sync/synced не вызываются.
/// Полная загрузка идёт в два этапа — сначала текущий и прошлый месяц (дашборды работают сразу), потом история.
/// Обычное обновление — изменения по last_tm; визиты (без tm) — за последние дни, раз в 6 часов — за весь месяц.
/// Данные разных серверов Linko не смешиваются: при смене сервера нужно очистить данные.
/// </summary>
public sealed class LinkoSyncService(
    LinkoClient client,
    OneBaseDbContext db,
    SalesOptions options,
    LinkoSettingsStore settings,
    LinkoSyncProgress progress,
    SalesCacheSignal cacheSignal,
    TimeProvider time,
    ILogger<LinkoSyncService> logger)
{
    public const string SourceState = "source";
    private const string VisitsMonthState = "visits_month";
    private static readonly TimeSpan VisitsMonthInterval = TimeSpan.FromHours(6);

    private static readonly string[] Dictionaries =
    [
        "users", "product_types", "products", "borders", "markets", "market_users",
        "stocks", "price_lists", "price_list_items", "providers", "currencies", "contracts",
    ];

    public async Task<LinkoSyncReport> SyncAsync(LinkoSyncMode mode, CancellationToken ct = default)
    {
        var started = time.GetUtcNow();
        var results = new List<LinkoEntityResult>();
        var connection = await settings.GetAsync(ct);

        if (mode == LinkoSyncMode.Reset)
        {
            progress.Step("Очистка старых данных", null);
            await PurgeAsync(ct);
        }
        else if (await SourceChangedAsync(connection.BaseUrl, ct) is { } message)
        {
            logger.LogWarning("Linko: синхронизация не запущена — {Message}", message);
            await SetErrorAsync(SourceState, message, ct);
            return new LinkoSyncReport(started, time.GetUtcNow(), [new LinkoEntityResult(SourceState, 0, message)]);
        }

        await MarkSourceAsync(connection.BaseUrl, ct);

        // Первая загрузка (или после очистки) — всегда полная.
        var full = mode != LinkoSyncMode.Incremental
            || !await db.LinkoSyncStates.AnyAsync(s => s.Entity == "orders" && s.LastSuccessAt != null, ct);
        progress.Plan(PlanOf(full, connection.HasPlanCredentials));

        // 1. Справочники.
        foreach (var entity in Dictionaries)
        {
            results.Add(await RunStepAsync(entity, "Справочники", accumulate: false, state => entity switch
            {
                "users" => SyncUsersAsync(ct),
                "product_types" => SyncProductTypesAsync(full ? null : state.LastTm, ct),
                "products" => SyncProductsAsync(full ? null : state.LastTm, ct),
                "borders" => SyncBordersAsync(full ? null : state.LastTm, ct),
                "markets" => SyncMarketsAsync(full ? null : state.LastTm, ct),
                "market_users" => SyncMarketUsersAsync(ct),
                "stocks" => SyncStocksAsync(full ? null : state.LastTm, ct),
                "price_lists" => SyncPriceListsAsync(full ? null : state.LastTm, ct),
                "price_list_items" => SyncPriceListItemsAsync(full ? null : state.LastTm, ct),
                "providers" => SyncProvidersAsync(ct),
                "currencies" => SyncCurrenciesAsync(ct),
                "contracts" => SyncContractsAsync(full ? null : state.LastTm, ct),
                _ => throw new InvalidOperationException($"Неизвестный справочник Linko: {entity}"),
            }, ct));
        }

        // 2. Документы: текущий и прошлый месяц (при полной загрузке) или изменения.
        var (recentFrom, recentTo) = RecentWindow();
        var phase = full ? "Текущий и прошлый месяц" : "Обновление";
        results.Add(await RunStepAsync("orders", phase, accumulate: false, state =>
            SyncOrdersAsync(full || state.LastTm is null ? Window(recentFrom, recentTo) : Since(state.LastTm), ct), ct));
        results.Add(await RunStepAsync("order_returns", phase, accumulate: false, state =>
            SyncReturnsAsync(full || state.LastTm is null ? Window(recentFrom, recentTo) : Since(state.LastTm), ct), ct));
        results.Add(await RunStepAsync("visits", phase, accumulate: false, _ => SyncVisitsAsync(full, recentFrom, recentTo, ct), ct));

        // Склад и деньги: остатки (штуки), перемещения (завод → склады регионов — первичка), платежи.
        results.Add(await RunStepAsync("product_balances", phase, accumulate: false, state =>
            SyncProductBalancesAsync(full || state.LastTm is null, state.LastTm, ct), ct));
        results.Add(await RunStepAsync("stock_transfers", phase, accumulate: false, state =>
            SyncStockTransfersAsync(full || state.LastTm is null, state.LastTm, ct), ct));
        results.Add(await RunStepAsync("payments", phase, accumulate: false, state =>
            SyncPaymentsAsync(full || state.LastTm is null ? Window(recentFrom, recentTo) : Since(state.LastTm), ct), ct));

        await EnsureRegionsAsync(ct);

        // Планы агентов из API планов Linko (staff_balance) — если задан его токен.
        if (connection.HasPlanCredentials)
        {
            results.Add(await RunStepAsync("staff_balance", "Планы агентов", accumulate: false, state => SyncStaffPlansAsync(full || state.LastTm is null, state.LastTm, ct), ct));
        }

        cacheSignal.Invalidate(); // свежие месяцы и планы уже видны в дашбордах

        // 3. История — после того как свежие данные уже доступны.
        if (full)
        {
            var (historyFrom, _) = BackfillWindow();
            var historyTo = recentFrom.AddDays(-1);
            if (historyFrom <= historyTo)
            {
                results.Add(await RunStepAsync("orders", "История", accumulate: true, _ => SyncOrdersAsync(Window(historyFrom, historyTo), ct), ct));
                results.Add(await RunStepAsync("order_returns", "История", accumulate: true, _ => SyncReturnsAsync(Window(historyFrom, historyTo), ct), ct));
                results.Add(await RunStepAsync("visits", "История", accumulate: true, _ => SyncVisitRangeAsync(historyFrom, historyTo, ct), ct));
                await EnsureRegionsAsync(ct);
            }
        }

        results.Add(await RunStepAsync("kpi_plans", "Планы Linko", accumulate: false, _ => SyncKpiPlansAsync(ct), ct));
        return new LinkoSyncReport(started, time.GetUtcNow(), results);
    }

    /// <summary>
    /// План шагов для индикатора «сколько выполнено»: те же entity и phase, что у шагов ниже. Вес — примерная доля времени:
    /// при полной загрузке основное время уходит на историю заказов и визитов, справочники — быстрые.
    /// </summary>
    private IReadOnlyList<LinkoSyncPlanStep> PlanOf(bool full, bool staffPlans)
    {
        var phase = full ? "Текущий и прошлый месяц" : "Обновление";
        var steps = Dictionaries
            .Select(d => new LinkoSyncPlanStep(d, "Справочники", d is "markets" or "market_users" or "price_list_items" ? 2 : 1))
            .ToList();
        steps.AddRange(
        [
            new("orders", phase, 6), new("order_returns", phase, 2), new("visits", phase, 6),
            new("product_balances", phase, 2), new("stock_transfers", phase, 2), new("payments", phase, 2),
        ]);
        if (staffPlans)
        {
            steps.Add(new("staff_balance", "Планы агентов", 2));
        }

        if (full && BackfillWindow().From <= RecentWindow().From.AddDays(-1))
        {
            steps.AddRange([new("orders", "История", 30), new("order_returns", "История", 6), new("visits", "История", 30)]);
        }

        steps.Add(new("kpi_plans", "Планы Linko", 3));
        steps.Add(new(null, "Пересчёт отчётов", 2)); // прогрев отчётов в LinkoSyncCoordinator
        return steps;
    }

    /// <summary>Окно полной загрузки документов: с 1-го числа (BackfillMonths назад) по конец текущего месяца.</summary>
    public (DateOnly From, DateOnly To) BackfillWindow()
    {
        var first = MonthStart(Today());
        return (first.AddMonths(-options.Sync.BackfillMonths), first.AddMonths(1).AddDays(-1));
    }

    /// <summary>Свежее окно: с 1-го числа прошлого месяца по конец текущего.</summary>
    private (DateOnly From, DateOnly To) RecentWindow()
    {
        var first = MonthStart(Today());
        return (first.AddMonths(-1), first.AddMonths(1).AddDays(-1));
    }

    private DateOnly Today() => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    private static DateOnly MonthStart(DateOnly d) => new(d.Year, d.Month, 1);

    // ---------- Очистка и источник данных ----------

    /// <summary>
    /// Удаляет всё загруженное из Linko и связанное с его идентификаторами в OneBase:
    /// регионы (branch), профили агентов и планы. Направления, цели и настройки подключения сохраняются.
    /// </summary>
    private async Task PurgeAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE linko."OrderLines", linko."Orders", linko."OrderReturnLines", linko."OrderReturns",
                     linko."Visits", linko."Markets", linko."MarketUsers", linko."Users", linko."Products",
                     linko."ProductTypes", linko."Borders", linko."KpiPlans", linko."SyncState",
                     linko."Stocks", linko."ProductBalances", linko."StockTransferLines", linko."StockTransfers",
                     linko."Payments", linko."PriceLists", linko."PriceListItems", linko."Providers",
                     linko."Currencies", linko."Contracts"
            """, ct);
        await db.SalesStaffPlans.ExecuteDeleteAsync(ct);
        await db.SalesAgentPlans.ExecuteDeleteAsync(ct);
        await db.SalesRegionPlans.ExecuteDeleteAsync(ct);
        await db.SalesAgentProfiles.ExecuteDeleteAsync(ct);
        await db.SalesRegions.ExecuteDeleteAsync(ct);
        cacheSignal.InvalidateHistory();
        logger.LogWarning("Данные Linko очищены перед полной загрузкой");
    }

    private async Task<string?> SourceChangedAsync(string baseUrl, CancellationToken ct)
    {
        var row = await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == LinkoSettingsStore.Code, ct);
        if (row?.DataSourceUrl is not { Length: > 0 } source || SameHost(source, baseUrl))
        {
            return null;
        }

        return $"Сервер Linko изменён: данные в OneBase загружены с {Host(source)}, а подключение настроено на {Host(baseUrl)}. " +
               "Чтобы не смешивать данные, нажмите «Очистить данные и загрузить заново».";
    }

    private async Task MarkSourceAsync(string baseUrl, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var row = await db.Integrations.FirstOrDefaultAsync(i => i.Code == LinkoSettingsStore.Code, ct);
        if (row is null)
        {
            row = new IntegrationConnection { Code = LinkoSettingsStore.Code, DepartmentCode = LinkoSettingsStore.Department };
            db.Integrations.Add(row);
        }

        row.DataSourceUrl = baseUrl;
        await db.LinkoSyncStates.Where(s => s.Entity == SourceState).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static bool SameHost(string a, string b) => string.Equals(Host(a), Host(b), StringComparison.OrdinalIgnoreCase);

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    /// <summary>Каждый филиал (branch) из заказов становится регионом OneBase — без направления, пока его не назначат.</summary>
    private async Task EnsureRegionsAsync(CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var known = await db.SalesRegions.Select(r => r.LinkoBranchId).ToListAsync(ct);
        var branches = await db.LinkoOrders
            .Where(o => o.BranchId != null && !known.Contains(o.BranchId!.Value))
            .GroupBy(o => o.BranchId!.Value)
            .Select(g => new { Id = g.Key, Name = g.Max(o => o.BranchName) })
            .ToListAsync(ct);

        foreach (var branch in branches)
        {
            db.SalesRegions.Add(new SalesRegion { LinkoBranchId = branch.Id, Name = branch.Name ?? $"Филиал {branch.Id}" });
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    // ---------- Шаги ----------

    private async Task<LinkoEntityResult> RunStepAsync(
        string entity,
        string phase,
        bool accumulate,
        Func<LinkoSyncState, Task<(int Rows, decimal? MaxTm)>> work,
        CancellationToken ct)
    {
        progress.Step(phase, entity);
        var state = await ReloadStateAsync(entity, ct);
        state.LastRunAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        var snapshot = new LinkoSyncState { Entity = entity, LastTm = state.LastTm, LastRows = state.LastRows };

        try
        {
            var (rows, maxTm) = await work(snapshot);

            state = await ReloadStateAsync(entity, ct);
            state.LastTm = Max(state.LastTm, maxTm);
            state.LastSuccessAt = time.GetUtcNow();
            state.LastError = null;
            state.LastRows = accumulate ? state.LastRows + rows : rows;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Linko {Entity} ({Phase}): {Rows} записей", entity, phase, rows);
            return new LinkoEntityResult(entity, rows, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Linko {Entity}: ошибка синхронизации", entity);
            await SetErrorAsync(entity, ex.Message, ct);
            return new LinkoEntityResult(entity, 0, ex.Message);
        }
    }

    private async Task SetErrorAsync(string entity, string message, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var state = await ReloadStateAsync(entity, ct);
        state.LastRunAt = time.GetUtcNow();
        state.LastError = message.Length > 1000 ? message[..1000] : message;
        await db.SaveChangesAsync(ct);
    }

    private async Task<LinkoSyncState> ReloadStateAsync(string entity, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var state = await db.LinkoSyncStates.FirstOrDefaultAsync(s => s.Entity == entity, ct);
        if (state is null)
        {
            state = new LinkoSyncState { Entity = entity };
            db.LinkoSyncStates.Add(state);
        }

        return state;
    }

    // ---------- Справочники ----------

    private async Task<(int, decimal?)> SyncUsersAsync(CancellationToken ct)
    {
        var rows = await client.ReadAllAsync<LinkoUserDto>("users", null, page => UpsertAsync(
            page, d => d.Id, db.LinkoUsers,
            d => new LinkoUser { Id = d.Id },
            (d, e) =>
            {
                e.Username = d.Username;
                e.FirstName = d.FirstName;
                e.SecondName = d.SecondName;
                e.IsActive = d.IsActive ?? false;
                e.JobId = d.Job?.Id;
                e.JobName = d.Job?.Name;
                e.PositionId = d.Position?.Id;
                e.PositionName = d.Position?.Name;
            }, ct), ct);
        return (rows, null);
    }

    private async Task<(int, decimal?)> SyncProductTypesAsync(decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoProductTypeDto>("product_types", Since(cursor), page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoProductTypes,
                d => new LinkoProductType { Id = d.Id, Name = d.Name ?? "" },
                (d, e) =>
                {
                    e.Name = d.Name ?? "";
                    e.ParentId = d.Parent?.Id;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncProductsAsync(decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoProductDto>("products", Since(cursor), page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoProducts,
                d => new LinkoProduct { Id = d.Id, Name = d.Name ?? "" },
                (d, e) =>
                {
                    e.Name = d.Name ?? "";
                    e.Code = d.Code;
                    e.TypeId = d.Type?.Id;
                    e.IsWeighted = d.Measurement?.IsWeighted ?? false;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncBordersAsync(decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoBorderDto>("borders", Since(cursor), page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoBorders,
                d => new LinkoBorder { Id = d.Id, Name = d.Name ?? "" },
                (d, e) =>
                {
                    e.Name = d.Name ?? "";
                    e.ParentId = d.ParentId;
                    e.IsDelete = d.IsDelete ?? false;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncMarketsAsync(decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoMarketDto>("markets", Since(cursor), page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoMarkets,
                d => new LinkoMarket { Id = d.Id, Name = d.Name ?? "" },
                (d, e) =>
                {
                    e.Name = d.Name ?? "";
                    e.MarketTypeId = d.MarketType?.Id;
                    e.MarketTypeName = d.MarketType?.Name;
                    e.ResponsibleAgentId = d.ResponsibleAgent?.Id;
                    e.BranchId = d.Branch?.Id;
                    e.BranchName = d.Branch?.Name;
                    e.Address = d.Address;
                    e.Lat = d.Location?.Lat;
                    e.Lon = d.Location?.Lon;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncMarketUsersAsync(CancellationToken ct)
    {
        var rows = await client.ReadAllAsync<LinkoMarketUserDto>("market_users", null, page => UpsertAsync(
            page, d => d.Id, db.LinkoMarketUsers,
            d => new LinkoMarketUser { Id = d.Id },
            (d, e) =>
            {
                e.UserId = d.UserId;
                e.MarketId = d.MarketId;
                e.IsDelete = d.IsDelete ?? false;
            }, ct), ct);
        return (rows, null);
    }

    private async Task<(int, decimal?)> SyncStocksAsync(decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoStockDto>("stocks", Since(cursor), page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoStocks,
                d => new LinkoStock { Id = d.Id, Name = d.Name ?? "" },
                (d, e) =>
                {
                    e.Name = d.Name ?? "";
                    e.Code = d.Code;
                    e.Address = d.Address;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncPriceListsAsync(decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoPriceListDto>("price_lists", Since(cursor), page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoPriceLists,
                d => new LinkoPriceList { Id = d.Id, Name = d.Name ?? "" },
                (d, e) =>
                {
                    e.Name = d.Name ?? "";
                    e.CurrencyId = d.CurrencyId;
                    e.Code = d.Code;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncPriceListItemsAsync(decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoPriceListItemDto>("price_list_items", Since(cursor), page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoPriceListItems,
                d => new LinkoPriceListItem { Id = d.Id },
                (d, e) =>
                {
                    e.ProductId = d.ProductId;
                    e.PriceListId = d.PriceListId;
                    e.Price = d.Price ?? 0;
                    e.CurrencyId = d.CurrencyId;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncProvidersAsync(CancellationToken ct)
    {
        var rows = await client.ReadAllAsync<LinkoProviderDto>("providers", null, page => UpsertAsync(
            page, d => d.Id, db.LinkoProviders,
            d => new LinkoProvider { Id = d.Id, Name = d.Name ?? "" },
            (d, e) =>
            {
                e.Name = d.Name ?? "";
                e.Tm = d.Tm ?? 0;
            }, ct), ct);
        return (rows, null);
    }

    /// <summary>Валюты Linko отдаёт простым массивом, без results и без пагинации.</summary>
    private async Task<(int, decimal?)> SyncCurrenciesAsync(CancellationToken ct)
    {
        var list = await client.ListAsync<LinkoCurrencyDto>("currencies/", ct);
        await UpsertAsync(list, d => d.Id, db.LinkoCurrencies,
            d => new LinkoCurrency { Id = d.Id, Name = d.Name ?? "" },
            (d, e) => e.Name = d.Name ?? "", ct);
        return (list.Count, null);
    }

    private async Task<(int, decimal?)> SyncContractsAsync(decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoContractDto>("contracts", Since(cursor), page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoContracts,
                d => new LinkoContract { Id = d.Id },
                (d, e) =>
                {
                    e.Date = d.Date is { } date ? DateOnly.FromDateTime(date) : null;
                    e.Number = d.Number;
                    e.Status = d.Status;
                    e.IsDelete = d.IsDelete ?? false;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    /// <summary>
    /// Остатки (в штуках). Полная загрузка — снимок целиком: строки, которых в Linko больше нет, удаляются из копии.
    /// Обновление — только изменённые (tm ≥ last_tm).
    /// </summary>
    private async Task<(int, decimal?)> SyncProductBalancesAsync(bool full, decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var seen = new HashSet<(long, long)>();
        var rows = await client.ReadAllAsync<LinkoProductBalanceDto>("product_balances", full ? null : Since(cursor), async page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            progress.AddRows(page.Count);
            var items = page.Where(p => p.Product is not null && p.Stock is not null)
                .GroupBy(p => (Product: p.Product!.Id, Stock: p.Stock!.Id))
                .Select(g => g.Last())
                .ToList();
            var products = items.Select(i => i.Product!.Id).Distinct().ToList();
            var existing = await db.LinkoProductBalances.Where(b => products.Contains(b.ProductId)).ToListAsync(ct);
            var byKey = existing.ToDictionary(b => (b.ProductId, b.StockId));

            foreach (var item in items)
            {
                var k = (item.Product!.Id, item.Stock!.Id);
                seen.Add(k);
                if (!byKey.TryGetValue(k, out var entity))
                {
                    entity = new LinkoProductBalance { ProductId = k.Item1, StockId = k.Item2 };
                    db.LinkoProductBalances.Add(entity);
                    byKey[k] = entity;
                }

                entity.Balance = item.Balance ?? 0;
                entity.Tm = item.Tm ?? 0;
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }, ct);

        if (full && rows > 0)
        {
            var stale = (await db.LinkoProductBalances.Select(b => new { b.ProductId, b.StockId }).ToListAsync(ct))
                .Where(b => !seen.Contains((b.ProductId, b.StockId)))
                .GroupBy(b => b.StockId);
            foreach (var group in stale)
            {
                var products = group.Select(b => b.ProductId).ToList();
                await db.LinkoProductBalances.Where(b => b.StockId == group.Key && products.Contains(b.ProductId)).ExecuteDeleteAsync(ct);
            }
        }

        return (rows, maxTm);
    }

    /// <summary>Перемещения между складами: при полной загрузке — все (их немного), при обновлении — изменённые по last_tm.</summary>
    private async Task<(int, decimal?)> SyncStockTransfersAsync(bool full, decimal? cursor, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoStockTransferDto>("stock_transfers", full ? null : Since(cursor), async page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            var ids = page.Select(p => p.Id).ToList();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.LinkoStockTransferLines.Where(l => ids.Contains(l.TransferId)).ExecuteDeleteAsync(ct);
            await UpsertAsync(page, d => d.Id, db.LinkoStockTransfers,
                d => new LinkoStockTransfer { Id = d.Id, Status = "" },
                (d, e) =>
                {
                    e.Status = d.Status ?? "";
                    e.FromStockId = d.FromStock?.Id;
                    e.ToStockId = d.ToStock?.Id;
                    e.CreatedAt = d.CreatedDate;
                    e.CreatedDate = d.CreatedDate is { } c ? DateOnly.FromDateTime(c) : null;
                    e.DeliveryDate = d.DateDelivery is { } dd ? DateOnly.FromDateTime(dd) : null;
                    e.GivenAt = d.GivenTime;
                    e.AcceptedAt = d.AcceptedTime;
                    e.AcceptedDate = d.AcceptedTime is { } a ? DateOnly.FromDateTime(a) : null;
                    e.TotalWeight = d.TotalWeight ?? 0;
                    e.TotalPrice = d.TotalPrice ?? 0;
                    e.PriceListId = d.PriceListId;
                    e.CurrencyId = d.CurrencyId;
                    e.InvoiceNumber = d.InvoiceNumber;
                    e.Tm = d.Tm ?? 0;
                }, ct);

            db.LinkoStockTransferLines.AddRange(page.SelectMany(t => (t.Products ?? []).Select(l => new LinkoStockTransferLine
            {
                Id = l.Id,
                TransferId = t.Id,
                ProductId = l.Product?.Id,
                Amount = l.Amount ?? 0,
                TotalWeight = l.TotalWeight ?? 0,
                TotalWeightNetto = l.TotalWeightNetto ?? 0,
                Price = l.Price ?? 0,
                TotalPrice = l.TotalPrice ?? 0,
            })));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }, ct);
        return (rows, maxTm);
    }

    /// <summary>Платежи: при полной загрузке — свежее окно (по дате создания), при обновлении — изменённые по last_tm.</summary>
    private async Task<(int, decimal?)> SyncPaymentsAsync(Dictionary<string, string?> filters, CancellationToken ct)
    {
        decimal? maxTm = null;
        var rows = await client.ReadAllAsync<LinkoPaymentDto>("payments", filters, page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            return UpsertAsync(page, d => d.Id, db.LinkoPayments,
                d => new LinkoPayment { Id = d.Id },
                (d, e) =>
                {
                    e.Amount = d.Amount ?? 0;
                    e.CurrencyId = d.Currency?.Id is > 0 ? d.Currency.Id : null;
                    e.CurrencyName = d.Currency?.Name;
                    e.MarketId = d.Market?.Id is > 0 ? d.Market.Id : null;
                    e.PaymentType = d.PaymentType;
                    e.Type = d.Type;
                    e.Status = d.Status;
                    e.UserId = d.User?.Id is > 0 ? d.User.Id : null; // пустой объект {} — нет агента
                    e.CreatedDate = d.CreatedDate is { } c ? DateOnly.FromDateTime(c) : null;
                    e.AcceptedAt = d.AcceptedTime;
                    e.OrderId = d.OrderId;
                    e.IsDelete = d.IsDelete ?? false;
                    e.Tm = d.Tm ?? 0;
                }, ct);
        }, ct);
        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncKpiPlansAsync(CancellationToken ct)
    {
        var (from, to) = BackfillWindow();
        var rows = await client.ReadAllAsync<LinkoKpiPlanDto>("kpi_plans", Window(from, to.AddMonths(12)), page => UpsertAsync(
            page, d => d.Id, db.LinkoKpiPlans,
            d => new LinkoKpiPlan { Id = d.Id },
            (d, e) =>
            {
                e.UserId = d.User?.Id;
                e.Year = d.Year;
                e.Month = d.Month;
                e.Plan = d.Plan ?? 0;
                e.IndicatorId = d.PerformanceIndicator?.Id;
                e.IndicatorName = d.PerformanceIndicator?.Name;
            }, ct), ct);
        return (rows, null);
    }

    // ---------- Документы ----------

    private async Task<(int, decimal?)> SyncOrdersAsync(Dictionary<string, string?> filters, CancellationToken ct)
    {
        decimal? maxTm = null;

        var rows = await client.ReadAllAsync<LinkoOrderDto>("orders", filters, async page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            var ids = page.Select(p => p.Id).ToList();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.LinkoOrderLines.Where(l => ids.Contains(l.OrderId)).ExecuteDeleteAsync(ct);
            await UpsertAsync(page, d => d.Id, db.LinkoOrders,
                d => new LinkoOrder { Id = d.Id, Status = "" },
                (d, e) =>
                {
                    e.CreatedAt = d.CreatedDate;
                    e.CreatedDate = DateOnly.FromDateTime(d.CreatedDate);
                    e.DeliveryDate = d.DateDelivery is { } dd ? DateOnly.FromDateTime(dd) : null;
                    e.AcceptedAt = d.AcceptedTime;
                    e.AcceptedDate = d.AcceptedTime is { } at ? DateOnly.FromDateTime(at) : null;
                    e.Status = d.Status ?? "";
                    e.MarketId = d.Market?.Id;
                    e.BranchId = d.Branch?.Id;
                    e.BranchName = d.Branch?.Name;
                    e.AgentId = d.Agent?.Id;
                    e.TotalPrice = d.TotalPrice ?? 0;
                    e.TotalWeight = d.TotalWeight ?? 0;
                    e.DiscountPrice = d.DiscountPrice ?? 0;
                    e.IsFullReturn = d.IsFullReturn ?? false;
                    e.Currency = d.Currency?.Name;
                    e.Tm = d.Tm ?? 0;
                }, ct);

            db.LinkoOrderLines.AddRange(page.SelectMany(o => (o.Products ?? []).Select(l => new LinkoOrderLine
            {
                Id = l.Id,
                OrderId = o.Id,
                ProductId = l.Product?.Id,
                Amount = l.Amount ?? 0,
                ReturnAmount = l.ReturnAmount ?? 0,
                Price = l.Price ?? 0,
                TotalPrice = l.TotalPrice ?? 0,
                TotalWeight = l.TotalWeight ?? 0,
            })));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }, ct);

        return (rows, maxTm);
    }

    private async Task<(int, decimal?)> SyncReturnsAsync(Dictionary<string, string?> filters, CancellationToken ct)
    {
        decimal? maxTm = null;

        var rows = await client.ReadAllAsync<LinkoReturnDto>("order_returns", filters, async page =>
        {
            maxTm = Max(maxTm, page.Max(p => p.Tm));
            var ids = page.Select(p => p.Id).ToList();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.LinkoOrderReturnLines.Where(l => ids.Contains(l.ReturnId)).ExecuteDeleteAsync(ct);
            await UpsertAsync(page, d => d.Id, db.LinkoOrderReturns,
                d => new LinkoOrderReturn { Id = d.Id, Status = "" },
                (d, e) =>
                {
                    e.CreatedDate = DateOnly.FromDateTime(d.CreatedDate);
                    e.Status = d.Status ?? "";
                    e.MarketId = d.Market?.Id;
                    e.AgentId = d.Agent?.Id;
                    e.BranchId = d.Branch?.Id;
                    e.BranchName = d.Branch?.Name;
                    e.TotalPrice = d.TotalPrice ?? 0;
                    e.TotalWeight = d.TotalWeight ?? 0;
                    e.Tm = d.Tm ?? 0;
                }, ct);

            db.LinkoOrderReturnLines.AddRange(page.SelectMany(r => (r.ReturnedProducts ?? []).Select(l => new LinkoOrderReturnLine
            {
                Id = l.Id,
                ReturnId = r.Id,
                OrderId = l.OrderId,
                ProductId = l.Product?.Id,
                Amount = l.Amount ?? 0,
                Price = l.Price ?? 0,
                TotalWeight = l.TotalWeight ?? 0,
            })));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }, ct);

        return (rows, maxTm);
    }

    /// <summary>
    /// Визиты (у них нет tm): при полной загрузке — свежее окно; при обновлении — за последние 3 дня,
    /// а раз в 6 часов — за весь текущий месяц (в первые дни месяца — и прошлый): статусы доезжают с опозданием.
    /// </summary>
    private async Task<(int, decimal?)> SyncVisitsAsync(bool full, DateOnly recentFrom, DateOnly recentTo, CancellationToken ct)
    {
        if (full)
        {
            var result = await SyncVisitRangeAsync(recentFrom, recentTo, ct);
            await MarkVisitsMonthAsync(ct);
            return result;
        }

        var today = Today();
        var monthState = await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == VisitsMonthState, ct);
        if (monthState?.LastSuccessAt is { } last && time.GetUtcNow() - last < VisitsMonthInterval)
        {
            return await SyncVisitRangeAsync(today.AddDays(-2), today, ct);
        }

        var from = MonthStart(today);
        if (today.Day <= 3)
        {
            from = from.AddMonths(-1);
        }

        var monthResult = await SyncVisitRangeAsync(from, today, ct);
        await MarkVisitsMonthAsync(ct);
        return monthResult;
    }

    private async Task MarkVisitsMonthAsync(CancellationToken ct)
    {
        var state = await ReloadStateAsync(VisitsMonthState, ct);
        state.LastRunAt = state.LastSuccessAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private async Task<(int, decimal?)> SyncVisitRangeAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await client.ReadAllAsync<LinkoVisitDto>("visits", Window(from, to), page => UpsertAsync(
            page, d => d.Id, db.LinkoVisits,
            d => new LinkoVisit { Id = d.Id, Status = "" },
            (d, e) =>
            {
                e.Date = d.Date;
                e.Day = DateOnly.FromDateTime(d.Date);
                e.Status = d.Status ?? "";
                e.IsInPlan = d.IsInPlan ?? false;
                e.MarketId = d.Market?.Id;
                e.UserId = d.User?.Id;
            }, ct), ct);
        return (rows, null);
    }
    // ---------- Планы агентов (staff_balance) ----------

    /// <summary>
    /// Полная загрузка — все месяцы окна истории. Обычное обновление — только если Linko сделал новый пересчёт
    /// (last_date изменился): текущий месяц, а в первые 5 дней месяца — и прошлый.
    /// </summary>
    private async Task<(int, decimal?)> SyncStaffPlansAsync(bool full, decimal? lastStamp, CancellationToken ct)
    {
        var lastDate = await client.StaffLastDateAsync(ct);
        decimal? stamp = lastDate is { } d ? d.Ticks / TimeSpan.TicksPerSecond : null;

        if (!full && stamp is not null && lastStamp is not null && stamp <= lastStamp)
        {
            return (0, lastStamp); // пересчёта не было — нечего обновлять
        }

        var today = Today();
        var current = MonthStart(today);
        // Следующий месяц — для карточки «План на следующий месяц»: планы на него появляются в Linko заранее.
        var months = new List<DateOnly> { current, current.AddMonths(1) };
        if (full)
        {
            var (from, _) = BackfillWindow();
            for (var m = current.AddMonths(-1); m >= from; m = m.AddMonths(-1))
            {
                months.Add(m);
            }
        }
        else if (today.Day <= 5)
        {
            months.Add(current.AddMonths(-1));
        }

        var total = 0;
        foreach (var month in months)
        {
            var rows = await client.StaffBalanceAsync(month.Year, month.Month, ct);
            total += await ReplaceStaffMonthAsync(month.Year, month.Month, rows, ct);
        }

        return (total, stamp);
    }

    private async Task<int> ReplaceStaffMonthAsync(int year, int month, IReadOnlyList<LinkoStaffBalanceDto> rows, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var entities = rows
            .Where(r => r.User is not null && r.PerformanceIndicator is not null)
            .DistinctBy(r => (r.User!.Id, r.PerformanceIndicator!.Id))
            .Select(r => new SalesStaffPlan
            {
                Year = year,
                Month = month,
                LinkoUserId = r.User!.Id,
                IndicatorId = r.PerformanceIndicator!.Id,
                IndicatorName = r.PerformanceIndicator.Name ?? $"Показатель {r.PerformanceIndicator.Id}",
                PlanType = r.PerformanceIndicator.PlanType ?? "unknown",
                Level = r.Level ?? 0,
                PlanAmount = r.PlanAmount ?? 0,
                SalesAmount = r.SalesAmount ?? 0,
                ReturnAmount = r.ReturnAmount ?? 0,
                FactAmount = r.FactAmount ?? 0,
                PlanForecast = r.PlanForecast ?? 0,
                FactPercent = r.FactPercent,
                ForecastPercent = r.ForecastPercent,
                LoadedAt = now,
            })
            .ToList();

        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.SalesStaffPlans.Where(p => p.Year == year && p.Month == month).ExecuteDeleteAsync(ct);
        db.SalesStaffPlans.AddRange(entities);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();

        progress.AddRows(entities.Count);
        return entities.Count;
    }

    // ---------- Общее ----------

    private async Task UpsertAsync<TDto, TEntity>(
        IReadOnlyList<TDto> page,
        Func<TDto, long> key,
        DbSet<TEntity> set,
        Func<TDto, TEntity> create,
        Action<TDto, TEntity> apply,
        CancellationToken ct)
        where TEntity : class
    {
        progress.AddRows(page.Count);
        var ids = page.Select(key).Distinct().ToList();
        var existing = await set
            .Where(e => ids.Contains(EF.Property<long>(e, "Id")))
            .ToDictionaryAsync(e => (long)db.Entry(e).Property("Id").CurrentValue!, ct);

        foreach (var dto in page.DistinctBy(key))
        {
            if (!existing.TryGetValue(key(dto), out var entity))
            {
                entity = create(dto);
                set.Add(entity);
            }

            apply(dto, entity);
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static Dictionary<string, string?> Since(decimal? cursor) =>
        cursor is null ? [] : new() { ["last_tm"] = cursor.Value.ToString(CultureInfo.InvariantCulture) };

    private static Dictionary<string, string?> Window((DateOnly From, DateOnly To) window) => Window(window.From, window.To);

    private static Dictionary<string, string?> Window(DateOnly from, DateOnly to) => new()
    {
        ["begin_date"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["end_date"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static decimal? Max(decimal? a, decimal? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);
}

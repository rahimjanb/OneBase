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

    private static readonly string[] Dictionaries = ["users", "product_types", "products", "borders", "markets", "market_users"];

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
            await SetErrorAsync(SourceState, message, ct);
            return new LinkoSyncReport(started, time.GetUtcNow(), [new LinkoEntityResult(SourceState, 0, message)]);
        }

        await MarkSourceAsync(connection.BaseUrl, ct);

        // Первая загрузка (или после очистки) — всегда полная.
        var full = mode != LinkoSyncMode.Incremental
            || !await db.LinkoSyncStates.AnyAsync(s => s.Entity == "orders" && s.LastSuccessAt != null, ct);

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
                _ => SyncMarketUsersAsync(ct),
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
                     linko."ProductTypes", linko."Borders", linko."KpiPlans", linko."SyncState"
            """, ct);
        await db.SalesStaffPlans.ExecuteDeleteAsync(ct);
        await db.SalesAgentPlans.ExecuteDeleteAsync(ct);
        await db.SalesRegionPlans.ExecuteDeleteAsync(ct);
        await db.SalesAgentProfiles.ExecuteDeleteAsync(ct);
        await db.SalesRegions.ExecuteDeleteAsync(ct);
        cacheSignal.Invalidate();
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
        var months = new List<DateOnly> { current };
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

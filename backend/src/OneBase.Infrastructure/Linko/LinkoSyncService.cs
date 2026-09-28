using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales;
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
/// Справочники — инкрементально по last_tm (где он есть), документы — окном по датам при первой загрузке,
/// дальше — изменения по last_tm. Визиты (без tm) — окном текущего месяца.
/// </summary>
public sealed class LinkoSyncService(
    LinkoClient client,
    OneBaseDbContext db,
    SalesOptions options,
    TimeProvider time,
    ILogger<LinkoSyncService> logger)
{
    public static readonly string[] Entities =
    [
        "users", "product_types", "products", "borders", "markets", "market_users",
        "orders", "order_returns", "visits", "kpi_plans",
    ];

    public async Task<LinkoSyncReport> SyncAsync(bool full, CancellationToken ct = default)
    {
        var started = time.GetUtcNow();
        var results = new List<LinkoEntityResult>();

        foreach (var entity in Entities)
        {
            results.Add(await RunStepAsync(entity, full, ct));
        }

        return new LinkoSyncReport(started, time.GetUtcNow(), results);
    }

    /// <summary>Окно первичной загрузки документов: с 1-го числа (BackfillMonths назад) по конец текущего месяца.</summary>
    public (DateOnly From, DateOnly To) BackfillWindow()
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var first = new DateOnly(today.Year, today.Month, 1);
        return (first.AddMonths(-options.Sync.BackfillMonths), first.AddMonths(1).AddDays(-1));
    }

    private async Task<LinkoEntityResult> RunStepAsync(string entity, bool full, CancellationToken ct)
    {
        var state = await ReloadStateAsync(entity, ct);
        state.LastRunAt = time.GetUtcNow();
        var cursor = full ? null : state.LastTm;
        var firstRun = state.LastSuccessAt is null;
        await db.SaveChangesAsync(ct);

        try
        {
            var (rows, maxTm) = entity switch
            {
                "users" => await SyncUsersAsync(ct),
                "product_types" => await SyncProductTypesAsync(cursor, ct),
                "products" => await SyncProductsAsync(cursor, ct),
                "borders" => await SyncBordersAsync(cursor, ct),
                "markets" => await SyncMarketsAsync(cursor, ct),
                "market_users" => await SyncMarketUsersAsync(ct),
                "orders" => await SyncOrdersAsync(cursor, ct),
                "order_returns" => await SyncReturnsAsync(cursor, ct),
                "visits" => await SyncVisitsAsync(full || firstRun, ct),
                "kpi_plans" => await SyncKpiPlansAsync(ct),
                _ => throw new ArgumentOutOfRangeException(nameof(entity)),
            };

            state = await ReloadStateAsync(entity, ct);
            state.LastTm = maxTm ?? state.LastTm;
            state.LastSuccessAt = time.GetUtcNow();
            state.LastError = null;
            state.LastRows = rows;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Linko {Entity}: {Rows} записей", entity, rows);
            return new LinkoEntityResult(entity, rows, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Linko {Entity}: ошибка синхронизации", entity);
            db.ChangeTracker.Clear();
            state = await ReloadStateAsync(entity, ct);
            state.LastError = ex.Message;
            await db.SaveChangesAsync(ct);
            return new LinkoEntityResult(entity, 0, ex.Message);
        }
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

    private async Task<(int, decimal?)> SyncOrdersAsync(decimal? cursor, CancellationToken ct)
    {
        var filters = cursor is null ? Window(BackfillWindow()) : Since(cursor);
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

    private async Task<(int, decimal?)> SyncReturnsAsync(decimal? cursor, CancellationToken ct)
    {
        var filters = cursor is null ? Window(BackfillWindow()) : Since(cursor);
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

    private async Task<(int, decimal?)> SyncVisitsAsync(bool backfill, CancellationToken ct)
    {
        // У визитов нет tm: при первой загрузке — всё окно, дальше — текущий месяц
        // (в первые дни месяца — ещё и прошлый: статусы визитов могут доезжать с опозданием).
        var (from, to) = BackfillWindow();
        if (!backfill)
        {
            var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
            from = new DateOnly(today.Year, today.Month, 1);
            if (today.Day <= 3)
            {
                from = from.AddMonths(-1);
            }
        }

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

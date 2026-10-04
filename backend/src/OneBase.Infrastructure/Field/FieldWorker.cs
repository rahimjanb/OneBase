using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Field;

/// <summary>
/// Витрина field.CustomerStats: по каждой точке Linko — последний заказ и визит, продажи за 7/30/90 дней и прошлые 30.
/// Пересчёт одним SQL в транзакции (≈50 тыс. точек); продажи — доставленные по дате приёмки, визиты — выполненные в Linko
/// и завершённые в Sales Base.
/// </summary>
public sealed class FieldStatsRefresher(OneBaseDbContext db, SalesOptions options, ILogger<FieldStatsRefresher> logger)
{
    public async Task<int> RefreshAsync(DateOnly today, CancellationToken ct)
    {
        const string sql = """
            DELETE FROM field."CustomerStats";
            INSERT INTO field."CustomerStats" ("MarketId", "LastOrderDate", "LastVisitDate", "Sales7", "Sales30", "Sales90", "SalesPrev30", "Kg30", "Orders30", "Visits30", "RefreshedAt")
            SELECT m."Id",
                   lo.last_order,
                   GREATEST(lv.last_visit, fv.last_visit),
                   COALESCE(o.s7, 0), COALESCE(o.s30, 0), COALESCE(o.s90, 0), COALESCE(o.p30, 0), COALESCE(o.k30, 0), COALESCE(o.c30, 0),
                   COALESCE(lv.c30, 0) + COALESCE(fv.c30, 0),
                   now()
            FROM linko."Markets" m
            LEFT JOIN (
                SELECT "MarketId",
                       sum("TotalPrice") FILTER (WHERE "AcceptedDate" > @today - 7) AS s7,
                       sum("TotalPrice") FILTER (WHERE "AcceptedDate" > @today - 30) AS s30,
                       sum("TotalPrice") AS s90,
                       sum("TotalPrice") FILTER (WHERE "AcceptedDate" <= @today - 30 AND "AcceptedDate" > @today - 60) AS p30,
                       sum("TotalWeight") FILTER (WHERE "AcceptedDate" > @today - 30) AS k30,
                       count(*) FILTER (WHERE "AcceptedDate" > @today - 30) AS c30
                FROM linko."Orders"
                WHERE "Status" = ANY(@sold) AND "MarketId" IS NOT NULL AND "AcceptedDate" > @today - 90 AND "AcceptedDate" <= @today
                GROUP BY "MarketId") o ON o."MarketId" = m."Id"
            LEFT JOIN (
                SELECT "MarketId", max("AcceptedDate") AS last_order
                FROM linko."Orders"
                WHERE "Status" = ANY(@sold) AND "MarketId" IS NOT NULL AND "AcceptedDate" <= @today
                GROUP BY "MarketId") lo ON lo."MarketId" = m."Id"
            LEFT JOIN (
                SELECT "MarketId", max("Day") AS last_visit, count(*) FILTER (WHERE "Day" > @today - 30) AS c30
                FROM linko."Visits"
                WHERE "Status" = 'done' AND "MarketId" IS NOT NULL AND "Day" <= @today
                GROUP BY "MarketId") lv ON lv."MarketId" = m."Id"
            LEFT JOIN (
                SELECT "MarketId",
                       max(("StartedAt" AT TIME ZONE 'Asia/Tashkent')::date) AS last_visit,
                       count(*) FILTER (WHERE ("StartedAt" AT TIME ZONE 'Asia/Tashkent')::date > @today - 30) AS c30
                FROM field."Visits"
                WHERE "Status" = 'Completed'
                GROUP BY "MarketId") fv ON fv."MarketId" = m."Id";
            """;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Database.ExecuteSqlRawAsync(sql, [new NpgsqlParameter("today", today), new NpgsqlParameter("sold", options.SoldStatuses)], ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Sales Base: витрина точек пересчитана ({Rows} строк)", rows);
        return rows;
    }

    /// <summary>Когда витрина пересчитывалась последний раз (null — пусто).</summary>
    public async Task<DateTimeOffset?> LastRefreshAsync(CancellationToken ct) =>
        await db.FieldCustomerStats.AsNoTracking().MaxAsync(s => (DateTimeOffset?)s.RefreshedAt, ct);

    /// <summary>Последняя успешная загрузка заказов или визитов из Linko.</summary>
    public async Task<DateTimeOffset?> LastSyncAsync(CancellationToken ct) =>
        await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders" || s.Entity == "visits" || s.Entity == "visits_month").MaxAsync(s => s.LastSuccessAt, ct);
}

/// <summary>
/// Фоновые задачи Sales Base (каждые 10 минут): пересчёт витрины после синхронизации Linko или смены дня; раз в день после 6:00 —
/// AI-планирование по всей организации, напоминания о просроченных задачах, устаревание старых рекомендаций.
/// Выключается Field:Worker:Enabled=false.
/// </summary>
public sealed class FieldWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<FieldWorker> logger) : BackgroundService
{
    private DateOnly? _statsDay;
    private DateOnly? _dailyDone;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Field:Worker:Enabled", true))
        {
            logger.LogInformation("Sales Base: фоновые задачи выключены (Field:Worker:Enabled=false)");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sales Base: ошибка фоновой задачи");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var today = FieldClock.Today;
        using var scope = scopes.CreateScope();
        var refresher = scope.ServiceProvider.GetRequiredService<FieldStatsRefresher>();
        var last = await refresher.LastRefreshAsync(ct);
        var sync = await refresher.LastSyncAsync(ct);
        if (last is null || _statsDay != today || (sync is not null && sync > last))
        {
            await refresher.RefreshAsync(today, ct);
            _statsDay = today;
        }

        if (_dailyDone == today || FieldClock.Now.Hour < 6)
        {
            return;
        }

        // Шаги независимы: ошибка в правилах AI не должна отменять напоминания о просроченных задачах.
        // Каждый шаг — в своей области (свой DbContext): после ошибки SaveChanges контекст с несохранёнными изменениями непригоден.
        var expired = await StepAsync("устаревание рекомендаций", sp => sp.GetRequiredService<FieldRecommendationService>().ExpireAsync(today, ct));
        var created = await StepAsync("AI-планирование", async sp => (await sp.GetRequiredService<FieldRecommendationService>().GenerateAsync(null, today, ct)).Created);
        var overdue = await StepAsync("напоминания о просрочке", sp => sp.GetRequiredService<FieldTaskService>().NotifyOverdueAsync(today, ct));
        _dailyDone = today;
        logger.LogInformation("Sales Base: рекомендаций создано {Created}, устарело {Expired}, напоминаний о просрочке {Overdue}", created, expired, overdue);
    }

    private async Task<int?> StepAsync(string name, Func<IServiceProvider, Task<int>> step)
    {
        try
        {
            using var stepScope = scopes.CreateScope();
            return await step(stepScope.ServiceProvider);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sales Base: ошибка шага «{Step}»", name);
            return null;
        }
    }
}

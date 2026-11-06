using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.DataSources;
using OneBase.Application.Field;
using OneBase.Application.Procurement;

namespace OneBase.Infrastructure.DataSources;

/// <summary>Таблицы Google для журнала склада — через подключения «Источников данных»: только названия и листы, ключ остаётся на сервере.</summary>
internal sealed class StockSheetSource(IAppDbContext db, DataSourceService sources) : IStockSheetSource
{
    public async Task<IReadOnlyList<StockSheetConnection>> ConnectionsAsync(CancellationToken ct)
    {
        var rows = await db.DataSourceConnections.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.Sheets }).ToListAsync(ct);
        return rows.Select(c => new StockSheetConnection(c.Id, c.Name, string.IsNullOrWhiteSpace(c.Sheets)
            ? []
            : (JsonSerializer.Deserialize<List<SheetInfo>>(c.Sheets, DataSourceRules.Json) ?? []).Select(s => s.Name).ToList())).ToList();
    }

    public async Task<(string Connection, ReportSheet Sheet)> ReadAsync(Guid connectionId, string sheet, CancellationToken ct)
    {
        try
        {
            var (connection, _, read) = await sources.SheetAsync(connectionId, sheet, ct);
            return (connection, read);
        }
        catch (DataSourceException e)
        {
            throw new FieldConflictException(e.Message);
        }
    }
}

/// <summary>Загрузки журнала склада из таблиц Google по расписанию — раз в минуту проверяет, чьё время пришло.</summary>
internal sealed class StockSheetImportWorker(IServiceScopeFactory scopes, ILogger<StockSheetImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ProcurementSheetImportService>().RunDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Склад: загрузка журнала из таблицы по расписанию упала");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

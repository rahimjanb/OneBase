using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.DataSources;

/// <summary>
/// AI Data Manager → «Источники данных»: список подключённых таблиц с привязками, свежий лист (Sheets API или файл) и синхронизация
/// привязки — тем же DataSourceService, что у страницы настроек, с теми же правами и проверками владельца полей.
/// </summary>
public sealed class DataSourceGateway(DataSourceService sources, OneBaseDbContext db) : IDataSourceGateway
{
    public async Task<IReadOnlyList<SheetConnectionInfo>> ConnectionsAsync(CancellationToken ct = default)
    {
        var connections = await db.DataSourceConnections.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var bindings = await db.DataSourceBindings.AsNoTracking().OrderBy(b => b.CreatedAt).ToListAsync(ct);
        return connections.Select(c => new SheetConnectionInfo(
                c.Id,
                c.Name,
                c.LastCheckOk,
                string.IsNullOrWhiteSpace(c.Sheets) ? [] : JsonSerializer.Deserialize<List<SheetInfo>>(c.Sheets, DataSourceRules.Json) ?? [],
                bindings.Where(b => b.ConnectionId == c.Id).Select(b => new SheetBindingInfo(
                    b.Id,
                    b.Section,
                    DataSourceCatalog.Section(b.Section)?.Name ?? b.Section,
                    DataSourceRules.ReadSheets(b.Sheets),
                    DataSourceRules.ReadFields(b.Fields)
                        .Select(f => new SheetFieldInfo(f.Field, DataSourceCatalog.Field(f.Field)?.Label ?? f.Field,
                            f.CurrentMonth ? "текущий месяц (лист без месяца)" : DataSourceCatalog.Field(f.Field)?.Kind == "profile" ? null : DataSourceRules.PeriodLabel(f.Period)))
                        .ToList(),
                    DataSourceOwnershipService.ScheduleText(b),
                    b.Enabled,
                    b.LastRunAt,
                    b.LastStatus,
                    b.LastMessage)).ToList()))
            .ToList();
    }

    public async Task<(string Connection, ReportSheet Sheet)> ReadSheetAsync(Guid connectionId, string? sheet, CancellationToken ct = default)
    {
        try
        {
            var (name, _, data) = await sources.SheetAsync(connectionId, sheet, ct);
            return (name, data);
        }
        catch (DataSourceException ex)
        {
            throw new DataManagerException(ex.Message);
        }
    }

    public async Task<SheetSyncResult> SyncAsync(Guid bindingId, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var run = await sources.RunAsync(bindingId, "chat", userId, ct);
            return new SheetSyncResult(run.Status, run.Message, run.Fields);
        }
        catch (DataSourceException ex)
        {
            throw new DataManagerException(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            throw new DataManagerException("Привязка не найдена — посмотрите list_google_sheets.");
        }
    }
}

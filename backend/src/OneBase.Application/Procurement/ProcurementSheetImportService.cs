using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.DataSources;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

/// <summary>Таблица Google из «Источников данных» для загрузки журнала склада: ID, название, листы (с последней проверки подключения).</summary>
public sealed record StockSheetConnection(Guid Id, string Name, IReadOnlyList<string> Sheets);

/// <summary>Чтение таблиц Google для склада (реализация — инфраструктура «Источников данных»; ключ сервисного аккаунта в браузер не отдаётся).</summary>
public interface IStockSheetSource
{
    Task<IReadOnlyList<StockSheetConnection>> ConnectionsAsync(CancellationToken ct);

    /// <summary>Значения листа: название таблицы и лист. Нет доступа, листа нет, сеть — FieldConflictException с текстом для кладовщика.</summary>
    Task<(string Connection, ReportSheet Sheet)> ReadAsync(Guid connectionId, string sheet, CancellationToken ct);
}

public sealed record ProcurementSheetImportInput(Guid? WarehouseId, Guid? ConnectionId, string? Sheet, string? Schedule, int? IntervalMinutes = null, string? DailyAt = null,
    bool Enabled = true, uint? Version = null);

public sealed record ProcurementSheetImportView(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    ProcurementDataOwner DataOwner,
    Guid ConnectionId,
    string ConnectionName,
    string Sheet,
    string Schedule,
    int? IntervalMinutes,
    string? DailyAt,
    bool Enabled,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? LastRunAt,
    string? LastStatus,
    string? LastMessage,
    string OwnerName,
    uint Version);

/// <summary>Загрузки журнала из таблиц Google по складам кладовщика и таблицы, из которых их можно настроить.</summary>
public sealed record ProcurementSheetImportsView(IReadOnlyList<ProcurementSheetImportView> Items, IReadOnlyList<StockSheetConnection> Connections);

/// <summary>
/// Загрузка журнала склада из таблицы Google (этап 7, переходный период): лист таблицы из «Источников данных» → тот же разбор, что у файла
/// (те же столбцы, проверки, отчёт; повторная загрузка не проводит строки второй раз). Настраивает и запускает кладовщик склада; по расписанию —
/// от имени того, кто сохранил настройку (его права проверяются при каждом запуске). Склад «ведётся в OneBase» загрузку не принимает.
/// </summary>
public sealed class ProcurementSheetImportService(
    IAppDbContext db,
    IStockSheetSource sheets,
    ProcurementStockImportService import,
    ProcurementStockLedger ledger,
    ProcurementRequestService requests,
    UserNotifier notifier,
    TimeProvider clock)
{
    private static void RequireWarehouse(ProcurementContext ctx)
    {
        if (!ctx.Warehouse)
        {
            throw new FieldForbiddenException("Загрузку журнала из таблицы настраивает кладовщик.");
        }
    }

    public async Task<ProcurementSheetImportsView> ListAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireWarehouse(ctx);
        var connections = await sheets.ConnectionsAsync(ct);
        var names = connections.ToDictionary(c => c.Id, c => c.Name);
        var rows = await db.ProcurementSheetImports.AsNoTracking().OrderBy(s => s.Warehouse!.Name).ThenBy(s => s.Sheet)
            .Select(s => new
            {
                s.Id, s.WarehouseId, Warehouse = s.Warehouse!.Name, s.Warehouse.DataOwner, s.ConnectionId, s.Sheet, s.Schedule, s.IntervalMinutes, s.DailyAt, s.Enabled,
                s.NextRunAt, s.LastRunAt, s.LastStatus, s.LastMessage, Owner = s.Owner!.FullName, s.Version,
            })
            .ToListAsync(ct);
        return new ProcurementSheetImportsView(
            rows.Where(r => ctx.Posts(r.WarehouseId)).Select(r => new ProcurementSheetImportView(r.Id, r.WarehouseId, r.Warehouse, r.DataOwner, r.ConnectionId,
                names.GetValueOrDefault(r.ConnectionId, "—"), r.Sheet, r.Schedule, r.IntervalMinutes, r.DailyAt, r.Enabled, r.NextRunAt, r.LastRunAt, r.LastStatus, r.LastMessage,
                r.Owner, r.Version)).ToList(),
            connections);
    }

    public async Task<Guid> SaveAsync(ProcurementContext ctx, Guid? id, ProcurementSheetImportInput input, CancellationToken ct)
    {
        RequireWarehouse(ctx);
        var warehouse = await ledger.WarehouseAsync(ctx, input.WarehouseId, ct, ProcurementWarehouseUse.Import);
        if (input.ConnectionId is not { } connectionId || !await db.DataSourceConnections.AnyAsync(c => c.Id == connectionId, ct))
        {
            throw new FieldValidationException("Выберите таблицу Google — её подключает администратор в «Настройки → Источники данных».");
        }

        var sheet = input.Sheet?.Trim();
        if (string.IsNullOrEmpty(sheet) || sheet.Length > 200)
        {
            throw new FieldValidationException("Укажите лист таблицы.");
        }

        var schedule = input.Schedule ?? DataSourceRules.Manual;
        if (schedule is not (DataSourceRules.Manual or DataSourceRules.Interval or DataSourceRules.Daily))
        {
            throw new FieldValidationException("Расписание: кнопкой, каждые N минут или раз в день.");
        }

        if (DataSourceRules.ScheduleError(schedule, input.IntervalMinutes, input.DailyAt) is { } error)
        {
            throw new FieldValidationException(error);
        }

        var binding = id is { } existing
            ? await db.ProcurementSheetImports.FirstOrDefaultAsync(s => s.Id == existing, ct) ?? throw new FieldNotFoundException("Загрузка не найдена.")
            : null;
        if (binding is not null && input.Version is { } version && version != binding.Version)
        {
            throw new FieldConflictException("Настройку уже изменили в другом окне — обновите страницу.");
        }

        if (await db.ProcurementSheetImports.AnyAsync(s => s.Id != (binding == null ? Guid.Empty : binding.Id) && s.WarehouseId == warehouse.Id
                                                           && s.ConnectionId == connectionId && s.Sheet == sheet, ct))
        {
            throw new FieldConflictException($"Лист «{sheet}» уже загружается на склад «{warehouse.Name}».");
        }

        if (binding is null)
        {
            binding = new ProcurementSheetImport { WarehouseId = warehouse.Id, ConnectionId = connectionId, Sheet = sheet, OwnerId = ctx.UserId };
            db.ProcurementSheetImports.Add(binding);
        }

        binding.WarehouseId = warehouse.Id;
        binding.ConnectionId = connectionId;
        binding.Sheet = sheet;
        binding.Schedule = schedule;
        binding.IntervalMinutes = schedule == DataSourceRules.Interval ? input.IntervalMinutes : null;
        binding.DailyAt = schedule == DataSourceRules.Daily ? input.DailyAt : null;
        binding.Enabled = input.Enabled;
        binding.OwnerId = ctx.UserId;
        binding.NextRunAt = input.Enabled ? DataSourceRules.NextRun(schedule, binding.IntervalMinutes, binding.DailyAt, clock.GetUtcNow()) : null;
        binding.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new FieldConflictException("Настройку уже изменили в другом окне — обновите страницу.");
        }

        return binding.Id;
    }

    public async Task DeleteAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireWarehouse(ctx);
        var binding = await db.ProcurementSheetImports.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new FieldNotFoundException("Загрузка не найдена.");
        if (!ctx.Posts(binding.WarehouseId))
        {
            throw new FieldForbiddenException("Загрузку на этот склад настраивает его кладовщик.");
        }

        db.ProcurementSheetImports.Remove(binding);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Загрузить сейчас (dryRun — только проверить, ничего не сохраняя): лист читается заново, проводятся только новые строки.</summary>
    public async Task<ProcurementStockImportResult> RunAsync(ProcurementContext ctx, Guid id, bool dryRun, CancellationToken ct)
    {
        RequireWarehouse(ctx);
        var binding = await db.ProcurementSheetImports.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new FieldNotFoundException("Загрузка не найдена.");
        return await RunCoreAsync(ctx, binding, dryRun, ct);
    }

    /// <summary>Загрузки по расписанию, время которых пришло: от имени сохранившего настройку; ошибка — ему уведомление (один раз, пока ошибка та же).</summary>
    public async Task RunDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.ProcurementSheetImports.AsNoTracking().Where(s => s.Enabled && s.NextRunAt != null && s.NextRunAt <= now).OrderBy(s => s.NextRunAt).Take(20)
            .ToListAsync(ct);
        foreach (var binding in due)
        {
            var permissions = await db.EffectivePermissionsAsync(binding.OwnerId, ct);
            try
            {
                if (permissions is null || !permissions.Contains(Permissions.ProcurementWarehouse))
                {
                    throw new FieldForbiddenException("У сохранившего настройку больше нет права «Склад снабжения» — сохраните настройку заново от имени кладовщика.");
                }

                var ctx = await requests.ContextAsync(binding.OwnerId, permissions, ct);
                await RunCoreAsync(ctx, binding, false, ct);
            }
            catch (Exception e) when (e is FieldValidationException or FieldConflictException or FieldForbiddenException or FieldNotFoundException)
            {
                db.ChangeTracker.Clear();
                await FinishAsync(binding.Id, "error", e.Message, ct);
            }
        }
    }

    private async Task<ProcurementStockImportResult> RunCoreAsync(ProcurementContext ctx, ProcurementSheetImport binding, bool dryRun, CancellationToken ct)
    {
        var warehouse = await ledger.WarehouseAsync(ctx, binding.WarehouseId, ct, ProcurementWarehouseUse.Import);
        var (connection, sheet) = await sheets.ReadAsync(binding.ConnectionId, binding.Sheet, ct);
        var result = await import.ImportDocumentAsync(ctx, warehouse, new ReportDocument([sheet]), $"{connection} / {binding.Sheet}", ct, dryRun);
        if (!dryRun)
        {
            db.ChangeTracker.Clear();
            var message = result.Errors.Count > 0
                ? $"Проведено строк: {result.Posted}, пропущено (уже были): {result.Skipped}; ошибок: {result.Errors.Count} — первая: {result.Errors[0]}"
                : $"Проведено строк: {result.Posted}, пропущено (уже были): {result.Skipped}";
            await FinishAsync(binding.Id, result.Errors.Count > 0 ? "warning" : "ok", message, ct);
        }

        return result;
    }

    private async Task FinishAsync(Guid id, string status, string message, CancellationToken ct)
    {
        var binding = await db.ProcurementSheetImports.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (binding is null)
        {
            return;
        }

        var text = message.Length <= 2000 ? message : message[..1999] + "…";
        if (status == "error" && (binding.LastStatus != "error" || binding.LastMessage != text))
        {
            notifier.Notify(binding.OwnerId, UserNotificationKind.DataSyncProblem, $"Загрузка журнала склада из таблицы не прошла: {binding.Sheet}", text, "/procurement/movements");
        }

        binding.LastRunAt = clock.GetUtcNow();
        binding.LastStatus = status;
        binding.LastMessage = text;
        binding.NextRunAt = binding.Enabled ? DataSourceRules.NextRun(binding.Schedule, binding.IntervalMinutes, binding.DailyAt, clock.GetUtcNow()) : null;
        await db.SaveChangesAsync(ct);
    }
}

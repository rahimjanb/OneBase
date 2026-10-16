using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.DataSources;

public sealed record ConnectionView(
    Guid Id, string Name, string Url, string SpreadsheetId, string Access, string? ServiceAccountEmail,
    DateTimeOffset? LastCheckedAt, bool? LastCheckOk, string? LastCheckMessage, IReadOnlyList<SheetInfo> Sheets, int Bindings);

public sealed record BindingView(
    Guid Id, string Section, Guid ConnectionId, string ConnectionName, IReadOnlyList<string> Sheets, int HeaderRow,
    IReadOnlyList<DataSourceFieldBinding> Fields, string Schedule, int? IntervalMinutes, string? DailyAt, bool Enabled,
    DateTimeOffset? LastRunAt, string? LastStatus, string? LastMessage, DateTimeOffset? NextRunAt, bool Running);

/// <summary>Поле раздела, которое уже обновляет привязка другого раздела (план РОП — и во «Вторичке», и в «Планах»).</summary>
public sealed record BoundElsewhere(string Field, string Label, string Section, string SectionName, Guid BindingId);

public sealed record SectionView(string Key, string Name, string Href, string Facts, IReadOnlyList<DataSourceField> Fields,
    IReadOnlyList<BindingView> Bindings, IReadOnlyList<BoundElsewhere> BoundElsewhere);

public sealed record LinkoSourceView(bool Configured, bool Enabled, string? Host, DateTimeOffset? LastSyncAt);

/// <summary>План вида на текущий месяц: сколько регионов с планом сейчас и сколько было в прошлом месяце (0 и 0 — план не ведётся).</summary>
public sealed record MonthPlanView(string Field, string Label, string Period, int Regions, int PreviousRegions);

public sealed record DataSourcesOverview(LinkoSourceView Linko, IReadOnlyList<ConnectionView> Connections, IReadOnlyList<SectionView> Sections,
    IReadOnlyList<DataSourceField> Fields, bool CanApply, IReadOnlyList<MonthPlanView> MonthPlans);

public sealed record SheetPreview(string Sheet, int Rows, int Columns, IReadOnlyList<string> Letters, IReadOnlyList<IReadOnlyList<string>> Cells);

public sealed record RunView(Guid Id, string Trigger, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, string? Message,
    IReadOnlyList<DataSourceFieldResult> Fields, string? UserName);

public sealed record ConnectionInput(string Name, string Url, string Access, string? ServiceAccountKey);

/// <summary>Привязка: листы с одинаковой разметкой (["*"] — все листы), строка заголовков, поля и расписание.</summary>
public sealed record BindingInput(string Section, Guid ConnectionId, IReadOnlyList<string> Sheets, int HeaderRow, IReadOnlyList<DataSourceFieldBinding> Fields,
    string Schedule, int? IntervalMinutes, string? DailyAt, bool Enabled);

/// <summary>Синхронизации, идущие сейчас, — чтобы одна привязка не запускалась дважды (кнопка и расписание одновременно).</summary>
public sealed class DataSourceRunLocks
{
    private readonly ConcurrentDictionary<Guid, byte> _running = new();

    public bool TryEnter(Guid id) => _running.TryAdd(id, 0);

    public void Exit(Guid id) => _running.TryRemove(id, out _);

    public bool IsRunning(Guid id) => _running.ContainsKey(id);
}

/// <summary>
/// «Настройки → Источники данных»: подключения Google Sheets, привязки разделов продаж к листам и синхронизация. Значения листа
/// читаются тем же разбором, что отчёты AI Data Manager, и записываются его планом изменений (подготовка → применение одной
/// транзакцией → проверка) от имени того, кто настроил привязку: каждая синхронизация видна в журнале операций и откатывается
/// там же. Обновляются только выбранные в привязке поля; строки с ошибкой (регион не найден, не число) — синхронизация поля
/// не записывает ничего, пока таблицу не исправят.
/// </summary>
public sealed class DataSourceService(
    OneBaseDbContext db,
    IGoogleSheetsClient sheets,
    DataChangeService changes,
    IDataChangeApplier applier,
    LinkoSettingsStore linko,
    IDataProtectionProvider protection,
    IMemoryCache cache,
    DataSourceRunLocks locks,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<DataSourceService> logger)
{
    private const int MaxMessage = 1000;
    private readonly IDataProtector _protector = protection.CreateProtector("OneBase.DataSources.GoogleServiceAccount");

    public async Task<DataSourcesOverview> OverviewAsync(Guid userId, CancellationToken ct)
    {
        var connections = await db.DataSourceConnections.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var bindings = await db.DataSourceBindings.AsNoTracking().OrderBy(b => b.CreatedAt).ToListAsync(ct);
        var names = connections.ToDictionary(c => c.Id, c => c.Name);
        var views = bindings.Select(b => View(b, names.GetValueOrDefault(b.ConnectionId) ?? "—")).ToList();

        var settings = await linko.GetAsync(ct);
        var lastSync = await db.LinkoSyncStates.AsNoTracking().MaxAsync(s => (DateTimeOffset?)s.LastSuccessAt, ct);
        var host = Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var uri) ? uri.Host : null;

        var sections = DataSourceCatalog.Sections.Select(s =>
        {
            var own = views.Where(b => b.Section == s.Key).ToList();
            var elsewhere = views.Where(b => b.Section != s.Key)
                .SelectMany(b => b.Fields.Where(f => s.Fields.Contains(f.Field)).Select(f => new BoundElsewhere(
                    f.Field, DataSourceCatalog.Field(f.Field)!.Label, b.Section, DataSourceCatalog.Section(b.Section)?.Name ?? b.Section, b.Id)))
                .ToList();
            return new SectionView(s.Key, s.Name, s.Href, s.Facts, s.Fields.Select(f => DataSourceCatalog.Field(f)!).ToList(), own, elsewhere);
        }).ToList();

        return new DataSourcesOverview(
            new LinkoSourceView(settings.IsReady, settings.Enabled, host, lastSync),
            connections.Select(c => ConnectionView(c, bindings.Count(b => b.ConnectionId == c.Id))).ToList(),
            sections,
            DataSourceCatalog.Fields,
            await CanApplyAsync(userId, ct),
            await MonthPlansAsync(ct));
    }

    /// <summary>Планы на текущий месяц по видам: есть ли, и были ли в прошлом месяце.</summary>
    private async Task<IReadOnlyList<MonthPlanView>> MonthPlansAsync(CancellationToken ct)
    {
        var (year, month) = DataSourceRules.CurrentPeriod(clock.GetUtcNow());
        var previous = new DateOnly(year, month, 1).AddMonths(-1);
        var counts = await db.SalesRegionPlans.AsNoTracking()
            .Where(p => (p.Year == year && p.Month == month) || (p.Year == previous.Year && p.Month == previous.Month))
            .GroupBy(p => new { p.Kind, p.Year, p.Month })
            .Select(g => new { g.Key.Kind, g.Key.Year, g.Key.Month, Regions = g.Select(p => p.RegionId).Distinct().Count() })
            .ToListAsync(ct);
        return DataSourceCatalog.Fields.Where(f => f.PlanKind is not null).Select(f => new MonthPlanView(
                f.Key, f.Label, $"{MonthTitle(month)} {year}",
                counts.Where(c => c.Kind == f.PlanKind && c.Year == year && c.Month == month).Sum(c => c.Regions),
                counts.Where(c => c.Kind == f.PlanKind && c.Year == previous.Year && c.Month == previous.Month).Sum(c => c.Regions)))
            .ToList();
    }

    private static string MonthTitle(int month) => char.ToUpperInvariant(DataValues.MonthNames[month - 1][0]) + DataValues.MonthNames[month - 1][1..];

    /// <summary>
    /// Начало месяца (1–10-е число): план вида был в прошлом месяце, а на текущий его нет — уведомление (колокольчик и push) тем, кто
    /// загружает планы (право «AI Data Manager»). Одно уведомление на вид и месяц.
    /// </summary>
    public async Task CheckMonthPlansAsync(CancellationToken ct)
    {
        var local = clock.GetUtcNow().ToOffset(DataSourceRules.CompanyOffset);
        if (local.Day > 10)
        {
            return;
        }

        var missing = (await MonthPlansAsync(ct)).Where(p => p.Regions == 0 && p.PreviousRegions > 0).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var recipients = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role.Permissions.Any(p => p.Code == Permissions.DataManage)))
            .Select(u => u.Id)
            .ToListAsync(ct);
        var previous = MonthTitle(local.AddMonths(-1).Month).ToLowerInvariant();
        foreach (var plan in missing)
        {
            var title = $"{plan.Period}: «{plan.Label}» ещё не загружен";
            var already = await db.UserNotifications.AsNoTracking()
                .Where(n => n.Kind == OneBase.Domain.Work.UserNotificationKind.MonthPlanMissing && n.Title == title)
                .Select(n => n.RecipientId)
                .ToListAsync(ct);
            foreach (var recipient in recipients.Except(already))
            {
                db.UserNotifications.Add(new OneBase.Domain.Work.UserNotification
                {
                    RecipientId = recipient,
                    Kind = OneBase.Domain.Work.UserNotificationKind.MonthPlanMissing,
                    Title = title,
                    Body = $"В {previous} план был по {plan.PreviousRegions} регионам, на {plan.Period.ToLowerInvariant()} — нет. Обновите таблицу Google Sheets " +
                           "(привязка с месяцем из столбца, из названия листа или «текущий месяц» подхватит его сама) или загрузите план через AI Data Manager.",
                    Link = "/settings/data-sources",
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- Подключения ----------

    /// <summary>Новое подключение: таблица проверяется сразу — листы видны в настройке привязки.</summary>
    public async Task<ConnectionView> CreateConnectionAsync(ConnectionInput input, Guid userId, CancellationToken ct)
    {
        var connection = new DataSourceConnection { Name = "", SpreadsheetId = "", Url = "" };
        Apply(connection, input, requireKey: true);
        connection.UpdatedById = userId;
        db.DataSourceConnections.Add(connection);
        await CheckAsync(connection, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "datasource.connection.created", nameof(DataSourceConnection), connection.Id.ToString(),
            new { connection.Name, connection.SpreadsheetId, Access = connection.Access.ToString() }, ct);
        return ConnectionView(connection, 0);
    }

    public async Task<ConnectionView> UpdateConnectionAsync(Guid id, ConnectionInput input, Guid userId, CancellationToken ct)
    {
        var connection = await db.DataSourceConnections.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException();
        Apply(connection, input, requireKey: connection.ProtectedKey is null);
        connection.UpdatedById = userId;
        connection.UpdatedAt = clock.GetUtcNow();
        await CheckAsync(connection, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "datasource.connection.updated", nameof(DataSourceConnection), connection.Id.ToString(),
            new { connection.Name, connection.SpreadsheetId, Access = connection.Access.ToString() }, ct);
        return ConnectionView(connection, await db.DataSourceBindings.CountAsync(b => b.ConnectionId == id, ct));
    }

    public async Task DeleteConnectionAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var connection = await db.DataSourceConnections.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException();
        var used = await db.DataSourceBindings.CountAsync(b => b.ConnectionId == id, ct);
        if (used > 0)
        {
            throw new DataSourceException($"Таблица используется в привязках разделов ({used}) — сначала удалите их.");
        }

        db.DataSourceConnections.Remove(connection);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "datasource.connection.deleted", nameof(DataSourceConnection), id.ToString(), new { connection.Name }, ct);
    }

    /// <summary>Проверка таблицы: читается ли и какие в ней листы.</summary>
    public async Task<ConnectionView> TestConnectionAsync(Guid id, CancellationToken ct)
    {
        var connection = await db.DataSourceConnections.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException();
        await CheckAsync(connection, ct);
        await db.SaveChangesAsync(ct);
        return ConnectionView(connection, await db.DataSourceBindings.CountAsync(b => b.ConnectionId == id, ct));
    }

    /// <summary>Первые строки листа — чтобы выбрать столбцы в привязке. Лист кэшируется на 3 минуты.</summary>
    public async Task<SheetPreview> PreviewAsync(Guid connectionId, string sheet, CancellationToken ct)
    {
        var connection = await db.DataSourceConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == connectionId, ct) ?? throw new KeyNotFoundException();
        // Ключ — с моментом последней проверки: после «Проверить» предпросмотр читает таблицу заново, а не из кэша.
        var document = await cache.GetOrCreateAsync($"datasource:{connection.Id}:{connection.UpdatedAt?.Ticks}:{connection.LastCheckedAt?.Ticks}:{sheet}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
            return await sheets.ReadAsync(Source(connection), [sheet], ct);
        }) ?? throw new DataSourceException("Таблица не прочитана.");
        ReportSheet found;
        try
        {
            found = ReportExtraction.Sheet(document, sheet);
        }
        catch (DataManagerException ex)
        {
            throw new DataSourceException(ex.Message);
        }

        var columns = Math.Min(Math.Max(found.TotalColumns, found.Rows.Take(15).Select(r => r.Count).DefaultIfEmpty(0).Max()), 40);
        return new SheetPreview(found.Name, found.Rows.Count, found.TotalColumns, Enumerable.Range(0, columns).Select(DataValues.ColumnLetter).ToList(),
            found.Rows.Take(15).Select(r => (IReadOnlyList<string>)r.Take(columns).Select(c => c.Length > 60 ? c[..60] + "…" : c).ToList()).ToList());
    }

    private void Apply(DataSourceConnection connection, ConnectionInput input, bool requireKey)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
        {
            throw new DataSourceException("Название — от 1 до 100 символов.");
        }

        var id = DataSourceRules.SpreadsheetId(input.Url) ?? throw new DataSourceException("Это не ссылка на таблицу Google: нужна ссылка вида docs.google.com/spreadsheets/d/…");
        var access = input.Access switch
        {
            "link" => SheetAccess.Link,
            "service_account" => SheetAccess.ServiceAccount,
            _ => throw new DataSourceException("Доступ — по ссылке или сервисным аккаунтом."),
        };

        connection.Name = name;
        connection.SpreadsheetId = id;
        connection.Url = $"https://docs.google.com/spreadsheets/d/{id}/edit";
        connection.Access = access;
        if (access == SheetAccess.ServiceAccount)
        {
            if (!string.IsNullOrWhiteSpace(input.ServiceAccountKey))
            {
                var key = ServiceAccountKey.Parse(input.ServiceAccountKey);
                connection.ProtectedKey = _protector.Protect(input.ServiceAccountKey.Trim());
                connection.ServiceAccountEmail = key.Email;
            }
            else if (requireKey)
            {
                throw new DataSourceException("Вставьте JSON-ключ сервисного аккаунта.");
            }
        }
        else
        {
            connection.ProtectedKey = null;
            connection.ServiceAccountEmail = null;
        }
    }

    private async Task CheckAsync(DataSourceConnection connection, CancellationToken ct)
    {
        connection.LastCheckedAt = clock.GetUtcNow();
        try
        {
            var list = await sheets.SheetsAsync(Source(connection), ct);
            connection.Sheets = JsonSerializer.Serialize(list, DataSourceRules.Json);
            connection.LastCheckOk = true;
            connection.LastCheckMessage = $"Таблица читается: листов {list.Count}.";
        }
        catch (DataSourceException ex)
        {
            connection.LastCheckOk = false;
            connection.LastCheckMessage = Clip(ex.Message);
        }
    }

    private SheetSource Source(DataSourceConnection c) =>
        new(c.SpreadsheetId, c.Access, c.Access == SheetAccess.ServiceAccount && c.ProtectedKey is { } key ? Unprotect(key) : null);

    private string? Unprotect(string value)
    {
        try
        {
            return _protector.Unprotect(value);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new DataSourceException("Ключ сервисного аккаунта не расшифровывается (сменились ключи сервера) — вставьте его заново.");
        }
    }

    private static ConnectionView ConnectionView(DataSourceConnection c, int bindings) => new(
        c.Id, c.Name, c.Url, c.SpreadsheetId, c.Access == SheetAccess.ServiceAccount ? "service_account" : "link", c.ServiceAccountEmail,
        c.LastCheckedAt, c.LastCheckOk, c.LastCheckMessage,
        string.IsNullOrWhiteSpace(c.Sheets) ? [] : JsonSerializer.Deserialize<List<SheetInfo>>(c.Sheets, DataSourceRules.Json) ?? [],
        bindings);

    // ---------- Привязки ----------

    public async Task<BindingView> SaveBindingAsync(Guid? id, BindingInput input, Guid userId, CancellationToken ct)
    {
        await RequireApplyAsync(userId, ct);
        var fields = input.Fields ?? [];
        if (DataSourceRules.FieldsError(input.Section, fields) is { } fieldsError)
        {
            throw new DataSourceException(fieldsError);
        }

        var schedule = string.IsNullOrWhiteSpace(input.Schedule) ? DataSourceRules.Manual : input.Schedule.Trim();
        if (DataSourceRules.ScheduleError(schedule, input.IntervalMinutes, input.DailyAt) is { } scheduleError)
        {
            throw new DataSourceException(scheduleError);
        }

        var (sheetList, sheetsError) = DataSourceRules.CleanSheets(input.Sheets);
        if (sheetsError is not null)
        {
            throw new DataSourceException(sheetsError);
        }

        if (input.HeaderRow is < 1 or > 1000)
        {
            throw new DataSourceException("Строка заголовков — от 1 до 1000.");
        }

        var connection = await db.DataSourceConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.ConnectionId, ct)
                         ?? throw new DataSourceException("Таблица не найдена — добавьте её в «Подключения».");

        // Одно поле — одна привязка: иначе две таблицы по очереди перезаписывали бы друг друга.
        var others = await db.DataSourceBindings.AsNoTracking().Where(b => b.Id != id).ToListAsync(ct);
        foreach (var other in others)
        {
            var taken = DataSourceRules.ReadFields(other.Fields).Select(f => f.Field).Intersect(fields.Select(f => f.Field)).FirstOrDefault();
            if (taken is not null)
            {
                throw new DataSourceException($"«{DataSourceCatalog.Field(taken)!.Label}» уже обновляется: {DataSourceRules.SheetsLabel(DataSourceRules.ReadSheets(other.Sheets))} " +
                                              $"(раздел «{DataSourceCatalog.Section(other.Section)?.Name ?? other.Section}»). Одно поле — один источник.");
            }
        }

        DataSourceBinding binding;
        if (id is { } existing)
        {
            binding = await db.DataSourceBindings.FirstOrDefaultAsync(b => b.Id == existing, ct) ?? throw new KeyNotFoundException();
            binding.UpdatedById = userId;
            binding.UpdatedAt = clock.GetUtcNow();
        }
        else
        {
            binding = new DataSourceBinding { Section = input.Section, Sheets = "[]", Fields = "[]", CreatedById = userId };
            db.DataSourceBindings.Add(binding);
        }

        binding.Section = input.Section;
        binding.ConnectionId = connection.Id;
        binding.Sheets = JsonSerializer.Serialize(sheetList, DataSourceRules.Json);
        binding.HeaderRow = input.HeaderRow;
        binding.Fields = JsonSerializer.Serialize(fields, DataSourceRules.Json);
        binding.Schedule = schedule;
        binding.IntervalMinutes = schedule == DataSourceRules.Interval ? input.IntervalMinutes : null;
        binding.DailyAt = schedule == DataSourceRules.Daily ? input.DailyAt : null;
        binding.Enabled = input.Enabled;
        binding.NextRunAt = input.Enabled ? DataSourceRules.NextRun(schedule, binding.IntervalMinutes, binding.DailyAt, clock.GetUtcNow()) : null;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), id is null ? "datasource.binding.created" : "datasource.binding.updated",
            nameof(DataSourceBinding), binding.Id.ToString(), new { binding.Section, Sheets = sheetList, Fields = fields.Select(f => f.Field), binding.Schedule }, ct);
        return View(binding, connection.Name);
    }

    public async Task DeleteBindingAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var binding = await db.DataSourceBindings.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new KeyNotFoundException();
        db.DataSourceBindings.Remove(binding);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "datasource.binding.deleted", nameof(DataSourceBinding), id.ToString(),
            new { binding.Section, Sheets = DataSourceRules.ReadSheets(binding.Sheets) }, ct);
    }

    public async Task<IReadOnlyList<RunView>> RunsAsync(Guid bindingId, CancellationToken ct)
    {
        var runs = await db.DataSourceSyncRuns.AsNoTracking()
            .Where(r => r.BindingId == bindingId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(30)
            .ToListAsync(ct);
        var userIds = runs.Where(r => r.UserId != null).Select(r => r.UserId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName.Length > 0 ? u.FullName : u.Login, ct);
        return runs.Select(r => new RunView(r.Id, r.Trigger, r.CreatedAt, r.FinishedAt, r.Status, r.Message,
            string.IsNullOrWhiteSpace(r.Details) ? [] : JsonSerializer.Deserialize<List<DataSourceFieldResult>>(r.Details, DataSourceRules.Json) ?? [],
            r.UserId is { } u ? users.GetValueOrDefault(u) : null)).ToList();
    }

    private BindingView View(DataSourceBinding b, string connectionName) => new(
        b.Id, b.Section, b.ConnectionId, connectionName, DataSourceRules.ReadSheets(b.Sheets), b.HeaderRow, DataSourceRules.ReadFields(b.Fields), b.Schedule, b.IntervalMinutes,
        b.DailyAt, b.Enabled, b.LastRunAt, b.LastStatus, b.LastMessage, b.NextRunAt, locks.IsRunning(b.Id));

    // ---------- Синхронизация ----------

    /// <summary>Синхронизации, у которых подошло время (фоновая служба раз в минуту).</summary>
    public async Task RunDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.DataSourceBindings.AsNoTracking()
            .Where(b => b.Enabled && b.NextRunAt != null && b.NextRunAt <= now)
            .OrderBy(b => b.NextRunAt)
            .Select(b => b.Id)
            .ToListAsync(ct);
        foreach (var id in due)
        {
            try
            {
                await RunAsync(id, "schedule", null, ct);
            }
            catch (Exception ex) when (ex is DataSourceException or KeyNotFoundException)
            {
                // Уже идёт по кнопке или привязку только что удалили — следующая попытка по расписанию.
                logger.LogInformation("Источники данных: привязка {Binding} пропущена: {Reason}", id, ex.Message);
            }
        }
    }

    /// <summary>
    /// Синхронизация привязки: читает таблицу и по каждому выбранному полю готовит план изменений AI Data Manager; без ошибок
    /// в строках — применяет и проверяет, с ошибками — ничего не пишет (план отменяется), без изменений — тоже.
    /// </summary>
    public async Task<RunView> RunAsync(Guid bindingId, string trigger, Guid? userId, CancellationToken ct)
    {
        if (!locks.TryEnter(bindingId))
        {
            throw new DataSourceException("Синхронизация этой привязки уже идёт — дождитесь её окончания.");
        }

        try
        {
            return await RunLockedAsync(bindingId, trigger, userId, ct);
        }
        finally
        {
            locks.Exit(bindingId);
        }
    }

    private async Task<RunView> RunLockedAsync(Guid bindingId, string trigger, Guid? userId, CancellationToken ct)
    {
        // Привязка — без отслеживания: применение плана AI Data Manager очищает трекер контекста; итог пишется в конце заново.
        db.ChangeTracker.Clear();
        var binding = await db.DataSourceBindings.AsNoTracking().Include(b => b.Connection).FirstOrDefaultAsync(b => b.Id == bindingId, ct)
                      ?? throw new KeyNotFoundException();
        if (userId is { } manual)
        {
            await RequireApplyAsync(manual, ct);
        }

        var run = new DataSourceSyncRun { BindingId = binding.Id, Trigger = trigger, Status = "running", UserId = userId ?? binding.CreatedById };
        var results = new List<DataSourceFieldResult>();
        string status;
        string message;
        try
        {
            var connection = binding.Connection ?? throw new DataSourceException("Таблица привязки удалена.");
            // Только листы привязки: из большой книги не читаются остальные листы.
            var document = await sheets.ReadAsync(Source(connection), DataSourceRules.ReadSheets(binding.Sheets), ct);
            var sheetNames = document.Sheets.Select(s => s.Name).ToList();

            // Изменения пишутся от имени того, кто настроил привязку (по кнопке — того, кто нажал): журнал AI Data Manager.
            var label = sheetNames.Count == 1 ? sheetNames[0] : $"листов {sheetNames.Count}";
            var actor = new DataActor(run.UserId!.Value, null, $"Google Sheets «{connection.Name}» / {label}");
            foreach (var field in DataSourceRules.ReadFields(binding.Fields))
            {
                results.Add(await SyncFieldAsync(binding, field, document, sheetNames, actor, trigger == "schedule", ct));
            }

            status = DataSourceRules.Overall(results.Select(r => r.Status).ToList());
            message = string.Join(" · ", results.Select(r => $"{r.Label}: {r.Message}"));
        }
        catch (DataSourceException ex)
        {
            status = "error";
            message = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Источники данных: синхронизация привязки {Binding} упала", binding.Id);
            status = "error";
            message = "Внутренняя ошибка OneBase — подробности в журнале ошибок.";
        }

        var now = clock.GetUtcNow();
        run.Status = status;
        run.Message = Clip(message);
        run.FinishedAt = now;
        run.Details = JsonSerializer.Serialize(results, DataSourceRules.Json);
        db.ChangeTracker.Clear();
        db.DataSourceSyncRuns.Add(run);
        var tracked = await db.DataSourceBindings.FirstOrDefaultAsync(b => b.Id == bindingId, CancellationToken.None);
        if (tracked is not null)
        {
            tracked.LastRunAt = now;
            tracked.LastStatus = status;
            tracked.LastMessage = run.Message;
            tracked.NextRunAt = tracked.Enabled ? DataSourceRules.NextRun(tracked.Schedule, tracked.IntervalMinutes, tracked.DailyAt, now) : null;
        }

        await db.SaveChangesAsync(CancellationToken.None);

        var name = run.UserId is { } u ? await db.Users.AsNoTracking().Where(x => x.Id == u).Select(x => x.FullName.Length > 0 ? x.FullName : x.Login).FirstOrDefaultAsync(ct) : null;
        return new RunView(run.Id, run.Trigger, run.CreatedAt, run.FinishedAt, run.Status, run.Message, results, name);
    }

    /// <summary>Значения поля из листов: как подготовить план изменений, и — у «текущего месяца» — месяц и отпечаток значений.</summary>
    private sealed record Extracted(Func<Task<DataChangeSetView>> Prepare, string? Period = null, string? Fingerprint = null);

    private async Task<DataSourceFieldResult> SyncFieldAsync(DataSourceBinding binding, DataSourceFieldBinding f, ReportDocument document,
        IReadOnlyList<string> sheetNames, DataActor actor, bool scheduled, CancellationToken ct)
    {
        var field = DataSourceCatalog.Field(f.Field);
        if (field is null)
        {
            return new DataSourceFieldResult(f.Field, f.Field, DataSourceRules.Error, "поле больше не поддерживается");
        }

        DataChangeSetView view;
        Extracted extracted;
        try
        {
            extracted = field.Kind switch
            {
                "plan" => PlanValues(binding, f, field, document, sheetNames, actor, ct),
                "profile" => ProfileValues(binding, f, field, document, sheetNames, actor, ct),
                _ => PnlValues(binding, f, document, sheetNames, actor, ct),
            };

            // Новый месяц, а в листе «текущий месяц» те же цифры, что записаны за прошлый: по расписанию не пишем — ждём обновления таблицы.
            // Кнопкой «Синхронизировать сейчас» — пишем: человек сам решил, что план нового месяца такой же.
            if (scheduled && extracted is { Period: { } period, Fingerprint: { } fingerprint })
            {
                var (lastPeriod, lastFingerprint) = await LastWrittenAsync(binding.Id, field.Key, ct);
                if (DataSourceRules.SameAsPreviousMonth(period, fingerprint, lastPeriod, lastFingerprint))
                {
                    return new DataSourceFieldResult(field.Key, field.Label, DataSourceRules.Waiting,
                        $"наступил новый месяц, а в таблице те же цифры, что записаны за {lastPeriod} — жду обновления таблицы. Если план не изменился, " +
                        "нажмите «Синхронизировать сейчас».");
                }
            }

            view = await extracted.Prepare();
        }
        catch (DataManagerException ex)
        {
            return new DataSourceFieldResult(field.Key, field.Label, DataSourceRules.Error, ex.Message);
        }

        try
        {
            var result = await WriteAsync(field, view, actor, ct);
            return result.Status is DataSourceRules.Ok or DataSourceRules.Unchanged
                ? result with { Period = extracted.Period, Fingerprint = extracted.Fingerprint }
                : result;
        }
        catch (DataManagerException ex)
        {
            // Отказ применения (нет права «AI Data Manager» у настроившего, план устарел) — понятная причина в журнале, а не «внутренняя ошибка».
            return new DataSourceFieldResult(field.Key, field.Label, "error", ex.Message, view.Id);
        }
    }

    /// <summary>План изменений поля: с ошибками строк или без изменений — отменяется; иначе применяется и проверяется.</summary>
    private async Task<DataSourceFieldResult> WriteAsync(DataSourceField field, DataChangeSetView view, DataActor actor, CancellationToken ct)
    {
        var conflicts = view.Items.Where(i => i.Action == DataChangeAction.Conflict).ToList();
        var counts = Counts(view);
        if (conflicts.Count > 0)
        {
            await changes.CancelAsync(view.Id, actor.UserId, ct);
            return new DataSourceFieldResult(field.Key, field.Label, "conflicts",
                $"не записано: строк с ошибками {conflicts.Count} — исправьте таблицу", view.Id, Conflicts: conflicts.Count,
                Problems: conflicts.Take(5).Select(c => $"{c.Label}: {c.Message}").ToList());
        }

        if (counts.Create + counts.Update + counts.Delete == 0)
        {
            await changes.CancelAsync(view.Id, actor.UserId, ct);
            return new DataSourceFieldResult(field.Key, field.Label, "unchanged", "без изменений");
        }

        var outcome = await applier.ApplyAsync(view.Id, actor.UserId, ct);
        if (outcome.Status != DataChangeSetStatus.Applied)
        {
            return new DataSourceFieldResult(field.Key, field.Label, "error", outcome.Message, view.Id, Problems: outcome.Problems.Take(5).ToList());
        }

        var check = await changes.VerifyAsync(view.Id, ct);
        var written = $"записано: новых {counts.Create}, изменено {counts.Update}, удалено {counts.Delete}";
        return check.Mismatches > 0
            ? new DataSourceFieldResult(field.Key, field.Label, "error", $"{written}, но проверка нашла расхождений: {check.Mismatches}", view.Id,
                counts.Create, counts.Update, counts.Delete, Problems: check.Problems.Take(5).Select(p => $"{p.Label}: ожидалось {p.Expected}, в базе {p.Actual}").ToList())
            : new DataSourceFieldResult(field.Key, field.Label, "ok", written, view.Id, counts.Create, counts.Update, counts.Delete);
    }

    /// <summary>
    /// План со всех листов: одно сопоставление столбцов; месяц — столбцом, значением, из названия листа или текущий месяц по Ташкенту
    /// (тогда — с отпечатком значений для защиты от копирования прошлого плана в новый месяц).
    /// </summary>
    private Extracted PlanValues(DataSourceBinding binding, DataSourceFieldBinding f, DataSourceField field, ReportDocument document,
        IReadOnlyList<string> sheetNames, DataActor actor, CancellationToken ct)
    {
        var m = f.Plan!;
        var (currentYear, currentMonth) = DataSourceRules.CurrentPeriod(clock.GetUtcNow());
        var fixedMonth = f.MonthFromSheet || f.CurrentMonth;
        var values = new List<PlanValue>();
        foreach (var sheet in sheetNames)
        {
            int? year = m.Year;
            int? month = m.Month;
            if (f.MonthFromSheet)
            {
                (year, month) = DataSourceRules.SheetMonth(sheet, m.Year);
            }
            else if (f.CurrentMonth)
            {
                (year, month) = (currentYear, currentMonth);
            }

            values.AddRange(ReportExtraction.PlanValues(document, new PlanSheetMapping(sheet, binding.HeaderRow, m.RegionColumn, m.ValueColumns,
                m.CategoryColumn, m.ValueColumn, f.CurrentMonth ? null : m.YearColumn, fixedMonth ? null : m.MonthColumn, year, month,
                m.Multiplier <= 0 ? 1 : m.Multiplier)));
        }

        if (values.Count == 0)
        {
            throw new DataManagerException("в листах нет строк со значениями плана");
        }

        return new Extracted(
            () => changes.PrepareRegionPlansAsync(actor, new RegionPlanRequest(field.PlanKind!.Value, f.Replace, null, null, values, Note(sheetNames)), ct),
            f.CurrentMonth ? DataSourceRules.PeriodKey(currentYear, currentMonth) : null,
            f.CurrentMonth ? DataSourceRules.Fingerprint(values.Select(v => $"{v.Region}|{v.Category}|{v.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? v.InvalidRaw}")) : null);
    }

    /// <summary>
    /// Дилер или СВР со всех листов. Один регион в нескольких строках (на одном листе или на разных) с одним значением — одна строка;
    /// с разными — ошибка поля: какое из значений верное, решает таблица, а не порядок строк.
    /// </summary>
    private Extracted ProfileValues(DataSourceBinding binding, DataSourceFieldBinding f, DataSourceField field, ReportDocument document,
        IReadOnlyList<string> sheetNames, DataActor actor, CancellationToken ct)
    {
        var all = sheetNames.SelectMany(sheet => DataSourceRules.ProfileValues(document, sheet, binding.HeaderRow, f.Profile!, field.ProfileField!)).ToList();
        if (all.Count == 0)
        {
            throw new DataManagerException("в листах нет строк с регионом и значением");
        }

        var values = new List<RegionProfileValue>();
        foreach (var group in all.GroupBy(v => DataValues.Normalize(v.Region)))
        {
            var distinct = group.Select(v => v.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinct.Count > 1)
            {
                throw new DataManagerException($"у региона «{group.First().Region}» в таблице разные значения: {string.Join(" / ", distinct)} " +
                                               $"({string.Join(", ", group.Select(v => v.SourceRef))})");
            }

            values.Add(group.First());
        }

        return new Extracted(() => changes.PrepareRegionProfilesAsync(actor, values, null, Note(sheetNames), ct));
    }

    private Extracted PnlValues(DataSourceBinding binding, DataSourceFieldBinding f, ReportDocument document, IReadOnlyList<string> sheetNames,
        DataActor actor, CancellationToken ct)
    {
        var (currentYear, currentMonth) = DataSourceRules.CurrentPeriod(clock.GetUtcNow());
        var values = new List<PnlRawValue>();
        foreach (var sheet in sheetNames)
        {
            var map = f.Pnl! with { Sheet = sheet, HeaderRow = binding.HeaderRow };
            if (f.MonthFromSheet)
            {
                var (year, month) = DataSourceRules.SheetMonth(sheet, map.Year);
                map = map with { MonthColumn = null, Year = year, Month = month };
            }
            else if (f.CurrentMonth)
            {
                map = map with { MonthColumn = null, YearColumn = null, Year = currentYear, Month = currentMonth };
            }

            values.AddRange(PnlExtraction.Values(document, map));
        }

        return new Extracted(
            () => changes.PreparePnlAsync(actor, new PnlRequest(f.Replace, null, null, values, null, null, Note(sheetNames)), ct),
            f.CurrentMonth ? DataSourceRules.PeriodKey(currentYear, currentMonth) : null,
            f.CurrentMonth ? DataSourceRules.Fingerprint(values.Select(v => $"{v.Dealer}|{v.Line}|{v.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? v.InvalidRaw}")) : null);
    }

    /// <summary>За какой месяц и с каким отпечатком поле последний раз записано (или совпало) — по журналу синхронизаций привязки.</summary>
    private async Task<(string? Period, string? Fingerprint)> LastWrittenAsync(Guid bindingId, string field, CancellationToken ct)
    {
        var details = await db.DataSourceSyncRuns.AsNoTracking()
            .Where(r => r.BindingId == bindingId && r.Details != null)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => r.Details!)
            .ToListAsync(ct);
        foreach (var json in details)
        {
            var hit = (JsonSerializer.Deserialize<List<DataSourceFieldResult>>(json, DataSourceRules.Json) ?? [])
                .FirstOrDefault(r => r.Field == field && r.Period is not null && r.Status is DataSourceRules.Ok or DataSourceRules.Unchanged);
            if (hit is not null)
            {
                return (hit.Period, hit.Fingerprint);
            }
        }

        return (null, null);
    }

    private static string Note(IReadOnlyList<string> sheetNames) => $"Синхронизация из Google Sheets: {DataSourceRules.SheetsLabel(sheetNames)}.";

    private static ChangeCounts Counts(DataChangeSetView view) =>
        view.Summary.TryGetProperty("counts", out var c) ? c.Deserialize<ChangeCounts>(DataTargets.Json) ?? new ChangeCounts(0, 0, 0, 0, 0) : new ChangeCounts(0, 0, 0, 0, 0);

    // ---------- Права ----------

    /// <summary>Синхронизация пишет данные планом изменений AI Data Manager — нужно его право (у администратора оно есть).</summary>
    private async Task RequireApplyAsync(Guid userId, CancellationToken ct)
    {
        if (!await CanApplyAsync(userId, ct))
        {
            throw new DataSourceException("Синхронизация записывает данные через AI Data Manager — нужно право «AI Data Manager».");
        }
    }

    private Task<bool> CanApplyAsync(Guid userId, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive)
            .AnyAsync(u => u.Roles.Any(r => r.Role.Permissions.Any(p => p.Code == Permissions.DataManage)), ct);

    private static string Clip(string text) => text.Length <= MaxMessage ? text : text[..MaxMessage] + "…";
}

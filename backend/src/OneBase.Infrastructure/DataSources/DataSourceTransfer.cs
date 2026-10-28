using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.DataSources;

/// <summary>
/// Файл переноса настроек источников данных (например, с рабочего компьютера на сервер): таблицы Google без ключей, привязки и дилеры регионов
/// (подпись строк первички — по ней план из таблицы находит регион; в таблицах Google справочника «регион → дилер» нет).
/// </summary>
public sealed record DataSourceExport(
    string Kind,
    int Version,
    DateTimeOffset ExportedAt,
    IReadOnlyList<DataSourceExportConnection> Connections,
    IReadOnlyList<DataSourceExportBinding> Bindings,
    IReadOnlyList<DataSourceExportDealer> Dealers)
{
    public const string FileKind = "onebase.data-sources";
    public const int CurrentVersion = 1;
}

/// <summary>Таблица Google: доступ «link» или «service_account»; ключ сервисного аккаунта в файл не попадает (только почта аккаунта).</summary>
public sealed record DataSourceExportConnection(string Name, string SpreadsheetId, string Url, string Access, string? ServiceAccountEmail);

/// <summary>Привязка: таблица — по SpreadsheetId; поля и расписание — как в OneBase.</summary>
public sealed record DataSourceExportBinding(
    string Section,
    string SpreadsheetId,
    IReadOnlyList<string> Sheets,
    int HeaderRow,
    IReadOnlyList<DataSourceFieldBinding> Fields,
    string Schedule,
    int? IntervalMinutes,
    string? DailyAt,
    bool Enabled);

/// <summary>
/// Дилер региона: регион — по id филиала Linko (у всех копий OneBase с одним Linko он один), название — для отчёта; Aliases — другие имена дилера
/// (прежние дилеры, второй дилер склада): по ним план из таблицы находит регион.
/// </summary>
public sealed record DataSourceExportDealer(long BranchId, string Region, string Dealer, IReadOnlyList<string>? Aliases = null);

/// <summary>Импорт: файл, ключ JSON сервисного аккаунта (для новых закрытых таблиц), DryRun — только показать, что будет сделано.</summary>
public sealed record DataSourceImportRequest(DataSourceExport? Settings, string? ServiceAccountKey, bool DryRun);

/// <summary>Строка отчёта импорта: что (connection, dealer, binding), имя, итог (created, updated, unchanged, skipped, error) и пояснение.</summary>
public sealed record DataSourceImportItem(string Kind, string Name, string Status, string Message);

public sealed record DataSourceImportResult(bool DryRun, IReadOnlyList<DataSourceImportItem> Items)
{
    public int Errors => Items.Count(i => i.Status == DataSourceTransfer.Error);
}

/// <summary>
/// Перенос настроек «Источников данных» файлом: экспорт без секретов и импорт в другую копию OneBase. Таблица находится по ID — уже
/// подключённая не создаётся заново (её ключ остаётся); привязка — по таблице, разделу и набору полей: найдена — обновляется, нет — создаётся
/// (правила те же, что в окне привязки: одно поле — один источник). Дилеры регионов пишутся до привязок: по ним план находит регион.
/// Данные не синхронизируются — привязки идут по своему расписанию или по кнопке «Синхронизировать».
/// </summary>
public sealed class DataSourceTransfer(OneBaseDbContext db, DataSourceService sources, FieldOwnership ownership, IAuditLogger audit)
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Unchanged = "unchanged";
    public const string Skipped = "skipped";
    public const string Error = "error";

    public async Task<DataSourceExport> ExportAsync(CancellationToken ct)
    {
        var connections = await db.DataSourceConnections.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var ids = connections.ToDictionary(c => c.Id, c => c.SpreadsheetId);
        var bindings = await db.DataSourceBindings.AsNoTracking().OrderBy(b => b.Section).ThenBy(b => b.CreatedAt).ToListAsync(ct);
        var dealers = await db.SalesRegions.AsNoTracking().Where(r => r.DealerName != null).OrderBy(r => r.Name).ToListAsync(ct);
        return new DataSourceExport(
            DataSourceExport.FileKind,
            DataSourceExport.CurrentVersion,
            DateTimeOffset.UtcNow,
            connections.Select(c => new DataSourceExportConnection(c.Name, c.SpreadsheetId, c.Url, AccessCode(c.Access), c.ServiceAccountEmail)).ToList(),
            bindings.Where(b => ids.ContainsKey(b.ConnectionId))
                .Select(b => new DataSourceExportBinding(b.Section, ids[b.ConnectionId], DataSourceRules.ReadSheets(b.Sheets), b.HeaderRow,
                    DataSourceRules.ReadFields(b.Fields), b.Schedule, b.IntervalMinutes, b.DailyAt, b.Enabled))
                .ToList(),
            dealers.Select(r => new DataSourceExportDealer(r.LinkoBranchId, r.Name, r.DealerName!, r.DealerAliases)).ToList());
    }

    public async Task<DataSourceImportResult> ImportAsync(DataSourceImportRequest request, Guid userId, CancellationToken ct)
    {
        var file = request.Settings ?? throw new DataSourceException("Файл настроек пуст.");
        if (file.Kind != DataSourceExport.FileKind || file.Version != DataSourceExport.CurrentVersion)
        {
            throw new DataSourceException("Это не файл настроек «Источников данных» OneBase (или он из более новой версии).");
        }

        // Импорт меняет таблицы, дилеров и привязки — как окно привязки, только с правом «AI Data Manager»; посмотреть план (DryRun) можно и без него.
        if (!request.DryRun && (await db.EffectivePermissionsAsync(userId, ct))?.Contains(Permissions.DataManage) != true)
        {
            throw new DataSourceException("Перенос настроек записывает привязки и дилеров — нужно право «AI Data Manager».");
        }

        var key = string.IsNullOrWhiteSpace(request.ServiceAccountKey) ? null : request.ServiceAccountKey.Trim();
        var items = new List<DataSourceImportItem>();

        // Таблицы: уже подключённая (тот же ID) остаётся как есть — со своим ключом и доступом.
        var existing = await db.DataSourceConnections.AsNoTracking().ToListAsync(ct);
        var connectionIds = existing.GroupBy(c => c.SpreadsheetId).ToDictionary(g => g.Key, g => g.First().Id);
        foreach (var c in file.Connections ?? [])
        {
            if (connectionIds.ContainsKey(c.SpreadsheetId))
            {
                var current = existing.First(x => x.SpreadsheetId == c.SpreadsheetId);
                items.Add(new("connection", c.Name, Unchanged, current.LastCheckOk == false
                    ? $"Уже подключена как «{current.Name}», но не читается: {current.LastCheckMessage}"
                    : $"Уже подключена как «{current.Name}»."));
                continue;
            }

            if (c.Access == "service_account" && key is null)
            {
                items.Add(new("connection", c.Name, Error,
                    $"Нужен ключ JSON сервисного аккаунта{(c.ServiceAccountEmail is null ? "" : $" ({c.ServiceAccountEmail})")} — таблица закрытая."));
                continue;
            }

            if (request.DryRun)
            {
                items.Add(new("connection", c.Name, Created, "Будет подключена и проверена."));
                connectionIds[c.SpreadsheetId] = Guid.Empty;
                continue;
            }

            try
            {
                var view = await sources.CreateConnectionAsync(new ConnectionInput(c.Name, c.Url, c.Access, c.Access == "service_account" ? key : null), userId, ct);
                connectionIds[c.SpreadsheetId] = view.Id;
                items.Add(new("connection", c.Name, Created, view.LastCheckOk == true ? "Подключена, читается." : $"Подключена, но не читается: {view.LastCheckMessage}"));
            }
            catch (DataSourceException ex)
            {
                items.Add(new("connection", c.Name, Error, ex.Message));
            }
        }

        // Дилеры — до привязок: план первички ищет регион по дилеру («Кахрамон Кукон» — Коканд).
        items.AddRange(await DealersAsync(file.Dealers ?? [], request.DryRun, userId, ct));

        var bindings = await db.DataSourceBindings.AsNoTracking().ToListAsync(ct);
        foreach (var b in file.Bindings ?? [])
        {
            var name = $"{DataSourceCatalog.Section(b.Section)?.Name ?? b.Section}: {string.Join(", ", b.Fields.Select(f => DataSourceCatalog.Field(f.Field)?.Label ?? f.Field))}";
            if (!connectionIds.TryGetValue(b.SpreadsheetId, out var connectionId))
            {
                items.Add(new("binding", name, Skipped, "Таблица привязки не подключена — см. строку таблицы выше."));
                continue;
            }

            var keys = b.Fields.Select(f => f.Field).Order().ToList();
            var same = bindings.FirstOrDefault(x => x.ConnectionId == connectionId && x.Section == b.Section
                                                    && DataSourceRules.ReadFields(x.Fields).Select(f => f.Field).Order().SequenceEqual(keys));
            var input = new BindingInput(b.Section, connectionId, b.Sheets, b.HeaderRow, b.Fields, b.Schedule, b.IntervalMinutes, b.DailyAt, b.Enabled);
            if (request.DryRun)
            {
                var taken = bindings.Where(x => x.Id != same?.Id)
                    .SelectMany(x => DataSourceRules.ReadFields(x.Fields).Select(f => f.Field))
                    .Intersect(keys)
                    .FirstOrDefault();
                items.Add(taken is not null
                    ? new("binding", name, Error, $"«{DataSourceCatalog.Field(taken)?.Label ?? taken}» уже обновляется другой привязкой — одно поле, один источник.")
                    : new("binding", name, same is null ? Created : Updated, same is null ? "Будет создана." : "Будет обновлена."));
                continue;
            }

            try
            {
                await sources.SaveBindingAsync(same?.Id, input, userId, ct);
                items.Add(new("binding", name, same is null ? Created : Updated,
                    $"{DataSourceRules.SheetsLabel(b.Sheets)}; синхронизация — {ScheduleText(b)}."));
            }
            catch (DataSourceException ex)
            {
                items.Add(new("binding", name, Error, ex.Message));
            }
        }

        if (!request.DryRun)
        {
            await audit.LogAsync(ActorType.User, userId.ToString(), "datasource.imported", "data_sources", null,
                new { Items = items.Select(i => new { i.Kind, i.Name, i.Status }) }, ct);
        }

        return new DataSourceImportResult(request.DryRun, items);
    }

    /// <summary>Дилеры регионов: регион — по филиалу Linko (иначе по названию); дилер, которого ведёт привязка Google Sheets, не меняется.</summary>
    private async Task<List<DataSourceImportItem>> DealersAsync(IReadOnlyList<DataSourceExportDealer> dealers, bool dryRun, Guid userId, CancellationToken ct)
    {
        var items = new List<DataSourceImportItem>();
        if (dealers.Count == 0)
        {
            return items;
        }

        var owned = (await ownership.ForeignAsync([RegionProfilePlanner.Dealer], null, ct)).Count > 0;
        var regions = await db.SalesRegions.ToListAsync(ct);
        var changed = 0;
        foreach (var d in dealers)
        {
            var region = regions.FirstOrDefault(r => r.LinkoBranchId == d.BranchId)
                         ?? regions.FirstOrDefault(r => string.Equals(r.Name, d.Region, StringComparison.OrdinalIgnoreCase));
            var dealer = d.Dealer.Trim();
            var aliases = (d.Aliases ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
            var sameAliases = region is not null && aliases.All(a => (region.DealerAliases ?? []).Contains(a, StringComparer.OrdinalIgnoreCase));
            if (region is null)
            {
                items.Add(new("dealer", $"{d.Region} — {dealer}", Skipped, "Такого региона здесь нет (филиал Linko не найден)."));
            }
            else if (region.DealerName == dealer && sameAliases)
            {
                items.Add(new("dealer", $"{region.Name} — {dealer}", Unchanged, "Уже задан."));
            }
            else if (owned)
            {
                items.Add(new("dealer", $"{region.Name} — {dealer}", Skipped, "Дилеров ведёт привязка Google Sheets — меняется там."));
            }
            else if (dealer.Length is 0 or > 200)
            {
                items.Add(new("dealer", $"{region.Name} — {dealer}", Error, "Название дилера — от 1 до 200 символов."));
            }
            else
            {
                var also = aliases.Count > 0 ? $" Другие имена: {string.Join(", ", aliases)}." : "";
                items.Add(new("dealer", $"{region.Name} — {dealer}", region.DealerName is null ? Created : Updated,
                    (region.DealerName is null ? (dryRun ? "Будет задан." : "Задан.")
                        : region.DealerName == dealer ? (dryRun ? "Будут добавлены другие имена." : "Добавлены другие имена.")
                        : $"{(dryRun ? "Будет заменён" : "Заменён")} (был «{region.DealerName}» — останется другим именем).") + also));
                if (!dryRun)
                {
                    region.ChangeDealer(dealer);
                    region.SetDealerAliases((region.DealerAliases ?? []).Concat(aliases));
                    region.UpdatedAt = DateTimeOffset.UtcNow;
                    changed++;
                }
            }
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(ActorType.User, userId.ToString(), "sales.region.dealers.imported", "sales_region", null, new { Count = changed }, ct);
        }

        return items;
    }

    private static string AccessCode(SheetAccess access) => access == SheetAccess.ServiceAccount ? "service_account" : "link";

    private static string ScheduleText(DataSourceExportBinding b) => b.Schedule switch
    {
        DataSourceRules.Interval => $"каждые {b.IntervalMinutes ?? DataSourceRules.DefaultIntervalMinutes} мин",
        DataSourceRules.Daily => $"ежедневно в {b.DailyAt}",
        _ => "вручную",
    } + (b.Enabled ? "" : " (выключена)");
}

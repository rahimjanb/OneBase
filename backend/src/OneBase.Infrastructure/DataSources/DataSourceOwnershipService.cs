using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Domain.AI;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.DataSources;

/// <summary>
/// Строка карты владения: какие данные, кто их ведёт (linko, google_sheets, onebase — вручную в OneBase, calculated — расчёт при показе),
/// откуда именно и когда источник последний раз успешно обновил или подтвердил их.
/// </summary>
public sealed record OwnershipRow(string Key, string Label, string Group, string Owner, string Source, string? Detail, DateTimeOffset? UpdatedAt,
    string? UpdatedNote);

public sealed record ConflictView(
    Guid Id, string Source, string SourceLabel, string Field, string FieldLabel, string? Entity, string? Period, string Kind, string KindLabel,
    string Message, string? SourceRef, string? Rule, string Decision, bool NeedsAction, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt,
    int Occurrences, DateTimeOffset? ResolvedAt, string? Binding);

/// <summary>Строка таблицы, которую синхронизация не записала: поле, что затронуто, период, причина, ячейка.</summary>
public sealed record ConflictDraft(string Field, string? Entity, string? Period, string Message, string? SourceRef);

/// <summary>
/// Карта владения данными и журнал конфликтов источников. Linko — факт (своя копия данных, планы ТП), Google Sheets — поля OneBase,
/// которые ведут включённые привязки, OneBase — остальные поля OneBase (AI Data Manager, настройки продаж), расчёт — показатели из плана и
/// факта (не хранятся). Конфликты строк таблицы открыты, пока строку не исправят: каждая синхронизация поля сверяет их с текущими.
/// </summary>
public sealed class DataSourceOwnershipService(OneBaseDbContext db, FieldOwnership ownership, TimeProvider clock)
{
    private const int OpenLimit = 200;
    private const int ClosedLimit = 100;
    private static readonly TimeSpan ClosedWindow = TimeSpan.FromDays(30);

    public async Task<IReadOnlyList<OwnershipRow>> MapAsync(CancellationToken ct)
    {
        var rows = new List<OwnershipRow>();
        var states = await db.LinkoSyncStates.AsNoTracking().ToDictionaryAsync(s => s.Entity, s => s.LastSuccessAt, ct);
        foreach (var (key, label, detail, entities) in SourceOfTruth.LinkoGroups)
        {
            // Обновлено — по самой давней из сущностей группы: группа свежая не раньше, чем её самая старая часть.
            var times = entities.Select(e => states.GetValueOrDefault(e)).ToList();
            var updated = times.All(t => t is not null) ? times.Min() : null;
            rows.Add(new OwnershipRow(key, label, "Факт", "linko", "Linko", detail, updated, updated is null ? "ещё не загружено" : "синхронизация Linko"));
        }

        var owners = await ownership.OwnersAsync(ct);
        var ownerIds = owners.Values.Select(o => o.BindingId).Distinct().ToList();
        var bindings = await db.DataSourceBindings.AsNoTracking()
            .Where(b => ownerIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, ct);
        foreach (var field in DataSourceCatalog.Fields)
        {
            if (owners.TryGetValue(field.Key, out var owner) && bindings.TryGetValue(owner.BindingId, out var binding))
            {
                var section = DataSourceCatalog.Section(owner.Section)?.Name ?? owner.Section;
                var synced = await LastSyncedAsync(binding.Id, field.Key, ct);
                rows.Add(new OwnershipRow(field.Key, field.Label, "Данные OneBase", "google_sheets", $"Google Sheets «{owner.Connection}»",
                    $"{DataSourceRules.SheetsLabel(owner.Sheets)} · раздел «{section}» · {ScheduleText(binding)}",
                    synced, synced is null ? "ещё не синхронизировалось" : "синхронизация проверила таблицу"));
            }
            else if (field.Kind is "ledger" or "terms" or "balance")
            {
                // Реестры взаиморасчётов загружаются только привязкой Google Sheets (копия листа) — без неё их нет.
                rows.Add(new OwnershipRow(field.Key, field.Label, "Данные OneBase", "google_sheets", "Google Sheets",
                    "не привязано: «Источники данных» → «Акт сверки» или «DSO» → «Привязать Google Sheets»", null, "ещё не загружено"));
            }
            else
            {
                var changed = await LastChangedAsync(field, ct);
                rows.Add(new OwnershipRow(field.Key, field.Label, "Данные OneBase", "onebase", "OneBase",
                    field.Kind == "profile" ? "AI Data Manager или «Настройки продаж → Регионы»" : "AI Data Manager (чат, файлы)",
                    changed, changed is null ? "через AI Data Manager не менялось" : "изменено через AI Data Manager"));
            }
        }

        rows.Add(new OwnershipRow("targets", "Цели", "Данные OneBase", "onebase", "OneBase", "«Настройки продаж → Цели»: конверсия визита, выручка на ТТ…",
            null, null));
        rows.AddRange(SourceOfTruth.Calculated.Select(m => new OwnershipRow(m.Key, m.Label, "Расчёт", m.Owner, m.Source, m.Detail, null, null)));
        return rows;
    }

    public Task<int> OpenCountAsync(CancellationToken ct) =>
        db.DataSourceConflicts.AsNoTracking().CountAsync(c => c.ResolvedAt == null && c.NeedsAction, ct);

    /// <summary>Открытые конфликты и закрытые за 30 дней.</summary>
    public async Task<IReadOnlyList<ConflictView>> ConflictsAsync(CancellationToken ct)
    {
        var since = clock.GetUtcNow() - ClosedWindow;
        var open = await db.DataSourceConflicts.AsNoTracking()
            .Where(c => c.ResolvedAt == null)
            .OrderByDescending(c => c.LastSeenAt)
            .Take(OpenLimit)
            .ToListAsync(ct);
        var closed = await db.DataSourceConflicts.AsNoTracking()
            .Where(c => c.ResolvedAt != null && c.LastSeenAt >= since)
            .OrderByDescending(c => c.LastSeenAt)
            .Take(ClosedLimit)
            .ToListAsync(ct);
        var ids = open.Concat(closed).Where(c => c.BindingId != null).Select(c => c.BindingId!.Value).Distinct().ToList();
        var bindings = await db.DataSourceBindings.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, b.Sheets, Connection = b.Connection!.Name })
            .ToDictionaryAsync(b => b.Id, b => $"«{b.Connection}» — {DataSourceRules.SheetsLabel(DataSourceRules.ReadSheets(b.Sheets))}", ct);
        return open.Concat(closed).Select(c => new ConflictView(
            c.Id, c.Source, ConflictSources.Label(c.Source), c.Field, DataSourceCatalog.Field(c.Field)?.Label ?? c.Field, c.Entity, c.Period, c.Kind,
            ConflictKinds.Label(c.Kind), c.Message, c.SourceRef, c.Rule, c.Decision, c.NeedsAction, c.CreatedAt, c.LastSeenAt, c.Occurrences, c.ResolvedAt,
            c.BindingId is { } b ? bindings.GetValueOrDefault(b) : null)).ToList();
    }

    /// <summary>Закрыть вручную (например, после исправления сопоставления). Если строка всё ещё с ошибкой, следующая синхронизация откроет новый.</summary>
    public async Task CloseAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var conflict = await db.DataSourceConflicts.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException();
        if (conflict.ResolvedAt is not null)
        {
            return;
        }

        conflict.ResolvedAt = clock.GetUtcNow();
        conflict.ResolvedById = userId;
        conflict.Decision = Cut($"{conflict.Decision} · закрыт вручную", 300)!;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Сверка строк с ошибками после синхронизации: новые — открываются, те же — отмечаются ещё раз, исправленные — закрываются. Только
    /// по полям fields, которые синхронизация прочитала до конца (записано, без изменений или ошибки строк). Сохраняет вызывающий.
    /// </summary>
    public async Task ReconcileAsync(Guid bindingId, string rule, IReadOnlyCollection<string> fields, IReadOnlyList<ConflictDraft> drafts, Guid? userId,
        CancellationToken ct)
    {
        if (fields.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var open = await db.DataSourceConflicts
            .Where(c => c.BindingId == bindingId && c.Kind == ConflictKinds.Row && c.ResolvedAt == null && fields.Contains(c.Field))
            .ToListAsync(ct);
        var seen = new HashSet<Guid>();
        var current = drafts.Where(d => fields.Contains(d.Field))
            .GroupBy(d => FieldOwnership.Fingerprint(bindingId, d.Field, ConflictKinds.Row, d.Entity, d.Message));
        foreach (var group in current)
        {
            var draft = group.First();
            var existing = open.FirstOrDefault(c => c.Fingerprint == group.Key);
            if (existing is not null)
            {
                existing.LastSeenAt = now;
                existing.Occurrences++;
                existing.SourceRef = Cut(draft.SourceRef, 300);
                seen.Add(existing.Id);
                continue;
            }

            db.DataSourceConflicts.Add(new DataSourceConflict
            {
                Source = ConflictSources.GoogleSheets,
                BindingId = bindingId,
                Field = draft.Field,
                Entity = Cut(draft.Entity, 300),
                Period = Cut(draft.Period, 32),
                Kind = ConflictKinds.Row,
                Message = Cut(draft.Message, 1000)!,
                SourceRef = Cut(draft.SourceRef, 300),
                Rule = Cut(rule, 300),
                Decision = "не записано: поле ждёт исправления таблицы, прежние значения OneBase сохранены",
                NeedsAction = true,
                Fingerprint = group.Key,
                LastSeenAt = now,
                UserId = userId,
            });
        }

        foreach (var fixedRow in open.Where(c => !seen.Contains(c.Id)))
        {
            fixedRow.ResolvedAt = now;
            fixedRow.Decision = "строка исправлена в таблице";
        }
    }

    /// <summary>Закрыть открытые конфликты привязки (fields null — все): привязку удалили или поле из неё убрали.</summary>
    public Task CloseForBindingAsync(Guid bindingId, IReadOnlyCollection<string>? fields, string decision, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.DataSourceConflicts
            .Where(c => c.BindingId == bindingId && c.ResolvedAt == null && (fields == null || fields.Contains(c.Field)))
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.ResolvedAt, now).SetProperty(c => c.Decision, decision), ct);
    }

    public static string ScheduleText(DataSourceBinding b) => b.Schedule switch
    {
        DataSourceRules.Interval => $"каждые {b.IntervalMinutes} мин",
        DataSourceRules.Daily => $"ежедневно в {b.DailyAt}",
        _ => "вручную",
    };

    /// <summary>Когда синхронизация привязки последний раз прочитала поле без ошибок (записала или подтвердила, что изменений нет).</summary>
    private async Task<DateTimeOffset?> LastSyncedAsync(Guid bindingId, string field, CancellationToken ct)
    {
        var runs = await db.DataSourceSyncRuns.AsNoTracking()
            .Where(r => r.BindingId == bindingId && r.Details != null && r.FinishedAt != null)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new { r.FinishedAt, r.Details })
            .ToListAsync(ct);
        foreach (var run in runs)
        {
            var results = JsonSerializer.Deserialize<List<DataSourceFieldResult>>(run.Details!, DataSourceRules.Json) ?? [];
            if (results.Any(r => r.Field == field && r.Status is DataSourceRules.Ok or DataSourceRules.Unchanged))
            {
                return run.FinishedAt;
            }
        }

        return null;
    }

    /// <summary>Когда поле последний раз изменено применённым планом AI Data Manager (из чата, файла или синхронизации).</summary>
    private async Task<DateTimeOffset?> LastChangedAsync(DataSourceField field, CancellationToken ct)
    {
        var applied = new[] { DataChangeSetStatus.Applied, DataChangeSetStatus.PartiallyRolledBack };
        if (field.Kind == "profile")
        {
            var suffix = ":" + field.ProfileField;
            return await db.DataChangeItems.AsNoTracking()
                .Where(i => i.EntityType == DataEntityTypes.RegionProfile && i.Status == DataChangeItemStatus.Applied && i.EntityKey.EndsWith(suffix))
                .MaxAsync(i => i.ChangeSet!.AppliedAt, ct);
        }

        var target = field.Kind == "plan" ? DataTargets.RegionPlans : DataTargets.Pnl;
        var kind = field.PlanKind?.ToString();
        return await db.DataChangeSets.AsNoTracking()
            .Where(s => s.Target == target && (kind == null || s.PlanKind == kind) && applied.Contains(s.Status))
            .MaxAsync(s => s.AppliedAt, ct);
    }

    private static string? Cut(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..(max - 1)] + "…";
}

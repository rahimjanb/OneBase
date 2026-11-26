using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales.Tajik;

/// <summary>
/// Регионы Таджикистана для отчёта: список регионов со словами в названиях точек, регион ТП по умолчанию, точки, закреплённые вручную.
/// Данные 1С при этом не меняются — меняется только то, к какому региону OneBase относит точку.
/// </summary>
public sealed class TajikSetupService(IAppDbContext db, TajikDataLoader loader, TajikDataVersion version, TimeProvider time)
{
    public const int MaxRegions = 200;
    public const int MaxKeywords = 50;

    public async Task<TajikSetupView> GetAsync(CancellationToken ct)
    {
        var regions = await loader.RegionsAsync(ct);
        var catalog = await loader.CatalogAsync(ct);
        var lines = await db.TajikSaleLines.AsNoTracking().Where(l => l.RemovedAt == null).ToListAsync(ct);
        var facts = lines.Select(l => TajikDataLoader.Fact(l, catalog, regions)).ToList();

        var stores = facts.GroupBy(f => f.StoreId)
            .Select(g =>
            {
                var last = g.OrderBy(f => f.Date).Last();
                var manual = regions.Stores.TryGetValue(last.Store, out var m) ? m.ToString() : null;
                return new TajikSetupStore(g.Key, last.Store, last.Agent, last.Region.Id, last.Region.Name, last.Region.Source, manual, g.Sum(f => f.Kg), last.Date);
            })
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var agents = stores.Where(s => s.Agent != null).GroupBy(s => s.Agent!)
            .Select(g => new TajikSetupAgent(g.Key, regions.Agents.TryGetValue(g.Key, out var r) ? r.ToString() : null, g.Count(),
                g.Count(s => s.Source == "none")))
            .Concat(regions.Agents.Keys.Where(a => stores.All(s => s.Agent != a)).Select(a => new TajikSetupAgent(a, regions.Agents[a].ToString(), 0, 0)))
            .OrderByDescending(a => a.Stores).ThenBy(a => a.Name)
            .ToList();

        var setupRegions = regions.Regions
            .Select(r => new TajikSetupRegion(r.Id.ToString(), r.Name, r.Keywords, stores.Count(s => s.RegionId == r.Id.ToString())))
            .ToList();
        return new TajikSetupView(setupRegions, agents, stores);
    }

    /// <summary>
    /// Сохранить список регионов целиком: новые (без Id) добавляются, изменённые обновляются, которых нет в списке — удаляются вместе с
    /// закреплениями. Названия — уникальны без учёта регистра.
    /// </summary>
    public async Task SaveRegionsAsync(IReadOnlyList<TajikRegionInput> input, Guid userId, CancellationToken ct)
    {
        if (input.Count > MaxRegions)
        {
            throw new ArgumentException($"Не больше {MaxRegions} регионов.");
        }

        var cleaned = input.Select((r, i) => (
                Id: Guid.TryParse(r.Id, out var g) ? g : (Guid?)null,
                Name: (r.Name ?? string.Empty).Trim(),
                Keywords: (r.Keywords ?? []).Select(k => (k ?? string.Empty).Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Order: i))
            .ToList();
        if (cleaned.Any(r => r.Name.Length == 0))
        {
            throw new ArgumentException("У каждого региона должно быть название.");
        }

        if (cleaned.Any(r => r.Name.Length > 100) || cleaned.Any(r => r.Keywords.Length > MaxKeywords || r.Keywords.Any(k => k.Length > 100)))
        {
            throw new ArgumentException($"Название региона — до 100 символов, слов — не больше {MaxKeywords}, каждое до 100 символов.");
        }

        var duplicate = cleaned.GroupBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Регион «{duplicate.Key}» указан дважды.");
        }

        if (cleaned.Any(r => r.Keywords.Any(k => TajikText.Normalize(k).Length == 0)))
        {
            throw new ArgumentException("Слово для поиска должно содержать буквы или цифры.");
        }

        var existing = await db.TajikRegions.ToListAsync(ct);
        var now = time.GetUtcNow().ToUniversalTime();
        var keep = cleaned.Where(r => r.Id is not null).Select(r => r.Id!.Value).ToHashSet();
        foreach (var gone in existing.Where(e => !keep.Contains(e.Id)))
        {
            db.TajikRegions.Remove(gone);
        }

        foreach (var r in cleaned)
        {
            var row = r.Id is { } id ? existing.FirstOrDefault(e => e.Id == id) : null;
            if (row is null)
            {
                row = new TajikRegion { Name = r.Name };
                db.TajikRegions.Add(row);
            }

            row.Name = r.Name;
            row.Keywords = r.Keywords;
            row.SortOrder = r.Order;
            row.UpdatedAt = now;
            row.UpdatedById = userId;
        }

        await db.SaveChangesAsync(ct);
        version.Bump();
    }

    /// <summary>Закрепить ТП или точку за регионом (RegionId null — снять). Точка — по id со страницы (хэш названия).</summary>
    public async Task SaveAssignmentAsync(TajikAssignmentInput input, Guid userId, CancellationToken ct)
    {
        var kind = input.Kind?.Trim().ToLowerInvariant();
        if (kind is not (TajikRegionAssignment.AgentKind or TajikRegionAssignment.StoreKind))
        {
            throw new ArgumentException("Закрепить можно ТП (agent) или точку (store).");
        }

        string? name;
        if (kind == TajikRegionAssignment.StoreKind)
        {
            var id = input.StoreId?.Trim() ?? string.Empty;
            var names = await db.TajikSaleLines.AsNoTracking().Where(l => l.RemovedAt == null).Select(l => l.Customer).Distinct().ToListAsync(ct);
            name = names.Select(n => n.Trim()).FirstOrDefault(n => TajikText.StoreId(n) == id);
        }
        else
        {
            var agent = input.Name?.Trim() ?? string.Empty;
            name = await db.TajikSaleLines.AsNoTracking().Where(l => l.Manager != null && l.Manager.Trim() == agent).Select(l => l.Manager!.Trim()).FirstOrDefaultAsync(ct)
                ?? await db.TajikRegionAssignments.AsNoTracking().Where(a => a.Kind == kind && a.Name == agent).Select(a => a.Name).FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new KeyNotFoundException(kind == TajikRegionAssignment.StoreKind ? "Точка не найдена в продажах 1С." : "ТП не найден в продажах 1С.");
        }

        var row = await db.TajikRegionAssignments.FirstOrDefaultAsync(a => a.Kind == kind && a.Name == name, ct);
        if (string.IsNullOrWhiteSpace(input.RegionId))
        {
            if (row is not null)
            {
                db.TajikRegionAssignments.Remove(row);
            }
        }
        else
        {
            if (!Guid.TryParse(input.RegionId, out var regionId) || !await db.TajikRegions.AnyAsync(r => r.Id == regionId, ct))
            {
                throw new KeyNotFoundException("Регион не найден.");
            }

            if (row is null)
            {
                row = new TajikRegionAssignment { Kind = kind, Name = name };
                db.TajikRegionAssignments.Add(row);
            }

            row.RegionId = regionId;
            row.UpdatedAt = time.GetUtcNow().ToUniversalTime();
            row.UpdatedById = userId;
        }

        await db.SaveChangesAsync(ct);
        version.Bump();
    }
}

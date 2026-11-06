using Microsoft.EntityFrameworkCore;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Twin;

namespace OneBase.Application.Twin;

/// <summary>
/// Привязки зданий двойника: какие отделы и склады находятся в здании — по постоянным идентификаторам (Department.Id,
/// ProcurementWarehouse.Id, Linko Stock.Id). Меняет администратор пользователей (как руководителей в «Настройки → Отделы»), каждое
/// изменение — в журнал аудита с «было/стало».
/// </summary>
public sealed partial class TwinService
{
    public const int MaxNameLength = 120;

    private static void RequireEdit(IReadOnlySet<string> permissions)
    {
        if (!permissions.Contains(Permissions.UsersManage))
        {
            throw new FieldForbiddenException("Привязки зданий меняет администратор пользователей.");
        }
    }

    /// <summary>Все здания модели: название (своё или из модели) и привязки; здание без строки в базе — из модели и без привязок.</summary>
    private async Task<List<TwinObjectView>> ObjectsAsync(CancellationToken ct)
    {
        var rows = await db.TwinObjects.AsNoTracking()
            .Select(o => new
            {
                o.Key,
                o.Name,
                o.UpdatedAt,
                o.CreatedAt,
                Links = o.Links.Select(l => new
                {
                    l.DepartmentId,
                    DepartmentCode = l.Department == null ? null : l.Department.Code,
                    DepartmentName = l.Department == null ? null : l.Department.Name,
                    l.WarehouseId,
                    WarehouseName = l.Warehouse == null ? null : l.Warehouse.Name,
                    l.LinkoStockId,
                    l.CreatedAt,
                }).ToList(),
            })
            .ToListAsync(ct);
        var stockIds = rows.SelectMany(r => r.Links).Where(l => l.LinkoStockId != null).Select(l => l.LinkoStockId!.Value).Distinct().ToList();
        var stocks = stockIds.Count == 0
            ? new Dictionary<long, string>()
            : await db.LinkoStocks.AsNoTracking().Where(s => stockIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var byKey = rows.ToDictionary(r => r.Key);

        return TwinSite.Buildings.Select(b =>
            {
                if (!byKey.TryGetValue(b.Key, out var row))
                {
                    return new TwinObjectView(b.Key, b.Name, b.Name, [], null);
                }

                var links = row.Links.OrderBy(l => l.CreatedAt)
                    .Select(l => l.DepartmentId is { } d
                        ? new TwinLinkView(TwinLinkKinds.Department, d.ToString(), l.DepartmentCode, l.DepartmentName ?? "")
                        : l.WarehouseId is { } w
                            ? new TwinLinkView(TwinLinkKinds.Warehouse, w.ToString(), null, l.WarehouseName ?? "")
                            : new TwinLinkView(TwinLinkKinds.Plant, l.LinkoStockId!.Value.ToString(), null,
                                stocks.GetValueOrDefault(l.LinkoStockId!.Value) ?? $"Linko #{l.LinkoStockId}"))
                    .ToList();
                return new TwinObjectView(b.Key, row.Name, b.Name, links, row.UpdatedAt ?? row.CreatedAt);
            })
            .ToList();
    }

    /// <summary>Что можно привязать: отделы, склады снабжения и склады Linko готовой продукции завода (настройка продаж «Склад завода»).</summary>
    public async Task<TwinOptions> OptionsAsync(IReadOnlySet<string> permissions, CancellationToken ct)
    {
        RequireEdit(permissions);
        var departments = (await DepartmentsAsync(ct)).Select(d => new TwinOption(d.Id.ToString(), d.Code, d.Name)).ToList();
        var warehouses = await db.ProcurementWarehouses.AsNoTracking().OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name)
            .Select(w => new TwinOption(w.Id.ToString(), w.Code, w.Name)).ToListAsync(ct);
        return new TwinOptions(departments, warehouses, await PlantsAsync(ct));
    }

    private async Task<List<TwinOption>> PlantsAsync(CancellationToken ct)
    {
        var stocks = await db.LinkoStocks.AsNoTracking().Select(s => new { s.Id, s.Name, s.Code }).ToListAsync(ct);
        return stocks.Where(s => salesOptions.IsFactoryStock(s.Name)).OrderBy(s => s.Name).Select(s => new TwinOption(s.Id.ToString(), s.Code, s.Name)).ToList();
    }

    /// <summary>
    /// Заменить привязки и название здания целиком. Название пустое — из модели. Отделы и склады — существующие; склад Linko — склад завода из
    /// настроек продаж. Повторы в списках не создают вторую привязку.
    /// </summary>
    public async Task<TwinObjectView> UpdateAsync(Guid actorId, IReadOnlySet<string> permissions, string key, TwinObjectInput input, CancellationToken ct)
    {
        RequireEdit(permissions);
        var building = TwinSite.Find(key) ?? throw new FieldNotFoundException("Здание не найдено.");
        var name = (input.Name ?? "").Trim();
        if (name.Length > MaxNameLength)
        {
            throw new FieldValidationException($"Название здания — не длиннее {MaxNameLength} символов.");
        }

        if (name.Length == 0)
        {
            name = building.Name;
        }

        var departmentIds = (input.DepartmentIds ?? []).Distinct().ToList();
        var warehouseIds = (input.WarehouseIds ?? []).Distinct().ToList();
        var plantIds = (input.PlantIds ?? []).Distinct().ToList();
        if (departmentIds.Count > 0 && await db.Departments.CountAsync(d => departmentIds.Contains(d.Id), ct) != departmentIds.Count)
        {
            throw new FieldValidationException("Отдел не найден — обновите страницу.");
        }

        if (warehouseIds.Count > 0 && await db.ProcurementWarehouses.CountAsync(w => warehouseIds.Contains(w.Id), ct) != warehouseIds.Count)
        {
            throw new FieldValidationException("Склад снабжения не найден — обновите страницу.");
        }

        if (plantIds.Count > 0)
        {
            var plants = (await PlantsAsync(ct)).Select(p => long.Parse(p.Id)).ToHashSet();
            if (!plantIds.All(plants.Contains))
            {
                throw new FieldValidationException("Склад Linko — только склад готовой продукции завода из настроек продаж.");
            }
        }

        var entity = await db.TwinObjects.Include(o => o.Links).FirstOrDefaultAsync(o => o.Key == building.Key, ct);
        var before = entity is null
            ? null
            : new
            {
                entity.Name,
                Departments = entity.Links.Where(l => l.DepartmentId != null).Select(l => l.DepartmentId!.Value).ToList(),
                Warehouses = entity.Links.Where(l => l.WarehouseId != null).Select(l => l.WarehouseId!.Value).ToList(),
                Plants = entity.Links.Where(l => l.LinkoStockId != null).Select(l => l.LinkoStockId!.Value).ToList(),
            };
        if (entity is null)
        {
            entity = new TwinObject { Key = building.Key, Name = name, UpdatedById = actorId };
            db.TwinObjects.Add(entity);
        }
        else
        {
            entity.Name = name;
            entity.UpdatedById = actorId;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Привязки, которых больше нет, удаляются; новые добавляются; оставшиеся не трогаются (их дата привязки — порядок в карточке).
        foreach (var link in entity.Links.ToList())
        {
            var keep = link.DepartmentId is { } d ? departmentIds.Contains(d)
                : link.WarehouseId is { } w ? warehouseIds.Contains(w)
                : link.LinkoStockId is { } s && plantIds.Contains(s);
            if (!keep)
            {
                db.TwinObjectLinks.Remove(link);
                entity.Links.Remove(link);
            }
        }

        foreach (var id in departmentIds.Where(id => entity.Links.All(l => l.DepartmentId != id)))
        {
            entity.Links.Add(new TwinObjectLink { DepartmentId = id });
        }

        foreach (var id in warehouseIds.Where(id => entity.Links.All(l => l.WarehouseId != id)))
        {
            entity.Links.Add(new TwinObjectLink { WarehouseId = id });
        }

        foreach (var id in plantIds.Where(id => entity.Links.All(l => l.LinkoStockId != id)))
        {
            entity.Links.Add(new TwinObjectLink { LinkoStockId = id });
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, actorId.ToString(), "twin.object.updated", "twin_object", building.Key,
            new { before, after = new { Name = name, Departments = departmentIds, Warehouses = warehouseIds, Plants = plantIds } }, ct);

        return (await ObjectsAsync(ct)).First(o => o.Key == building.Key);
    }
}

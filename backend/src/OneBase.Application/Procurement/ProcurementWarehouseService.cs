using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Audit;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

public sealed record ProcurementPersonOption(Guid Id, string Name);

/// <summary>
/// Склад в справочнике: код, название, вид, отдел кладовой цеха, адрес, по умолчанию, действует; Items — позиций с остатком, Keepers —
/// закреплённые кладовщики, Posts — текущий пользователь проводит операции по этому складу, Version — для правки.
/// </summary>
public sealed record ProcurementWarehouseView(
    Guid Id,
    string Code,
    string Name,
    ProcurementWarehouseKind Kind,
    Guid? DepartmentId,
    string? DepartmentName,
    string? Address,
    bool IsDefault,
    bool IsActive,
    int Items,
    IReadOnlyList<ProcurementPersonOption> Keepers,
    bool Posts,
    uint Version,
    ProcurementDataOwner DataOwner = ProcurementDataOwner.None,
    DateOnly? DataOwnerSince = null);

/// <summary>Склады и что нужно форме: кладовщики (право «Склад снабжения»), отделы для кладовой цеха, можно ли править (настройки снабжения).</summary>
public sealed record ProcurementWarehousesView(
    IReadOnlyList<ProcurementWarehouseView> Warehouses,
    IReadOnlyList<ProcurementPersonOption> KeeperOptions,
    IReadOnlyList<ProcurementDepartmentOption> Departments,
    bool CanEdit);

/// <summary>
/// Склад из формы; Version — с какой версии правили (другой — 409). DataOwner — кто ведёт данные склада в переходный период (null — не менять),
/// DataOwnerSince — с какой даты (по умолчанию — сегодня).
/// </summary>
public sealed record ProcurementWarehouseInput(
    string? Code,
    string? Name,
    ProcurementWarehouseKind Kind,
    Guid? DepartmentId = null,
    string? Address = null,
    bool IsActive = true,
    IReadOnlyList<Guid>? KeeperIds = null,
    uint? Version = null,
    ProcurementDataOwner? DataOwner = null,
    DateOnly? DataOwnerSince = null);

/// <summary>
/// Справочник складов снабжения («Настройки → Отделы → Снабжение»): смотрят все, кто видит склад или настраивает снабжение, заводит и
/// правит — право «Настройки снабжения». Склад по умолчанию (центральный) не отключается: на нём резервируют заявки и приходуют закупки.
/// Склад с остатком или резервом не отключается — сначала перемещение или списание. Изменения — в журнал аудита (было и стало).
/// </summary>
public sealed partial class ProcurementWarehouseService(IAppDbContext db, IAuditLogger audit)
{
    [GeneratedRegex(@"^[\p{L}\p{N}._-]+$")]
    private static partial Regex CodePattern();

    public async Task<ProcurementWarehousesView> ListAsync(ProcurementContext ctx, CancellationToken ct)
    {
        if (!ctx.SeesStock && !ctx.Configures)
        {
            throw new FieldForbiddenException("Склады снабжения видят кладовщик, снабжение и директор.");
        }

        var rows = await db.ProcurementWarehouses.AsNoTracking()
            .OrderByDescending(w => w.IsDefault).ThenByDescending(w => w.IsActive).ThenBy(w => w.Name)
            .Select(w => new
            {
                w.Id, w.Code, w.Name, w.Kind, w.DepartmentId, Department = w.Department == null ? null : w.Department.Name, w.Address, w.IsDefault, w.IsActive, w.Version,
                w.DataOwner, w.DataOwnerSince,
                Items = db.ProcurementStockBalances.Count(b => b.WarehouseId == w.Id && b.OnHand > 0),
                Keepers = w.Keepers.Select(k => new ProcurementPersonOption(k.UserId, k.User!.FullName)).ToList(),
            })
            .ToListAsync(ct);
        var keepers = await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct);
        var options = await db.Users.AsNoTracking().Where(u => keepers.Contains(u.Id)).OrderBy(u => u.FullName)
            .Select(u => new ProcurementPersonOption(u.Id, u.FullName)).ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new ProcurementDepartmentOption(d.Id, d.Code, d.Name)).ToListAsync(ct);
        return new ProcurementWarehousesView(
            rows.Select(w => new ProcurementWarehouseView(w.Id, w.Code, w.Name, w.Kind, w.DepartmentId, w.Department, w.Address, w.IsDefault, w.IsActive, w.Items,
                w.Keepers.OrderBy(k => k.Name).ToList(), w.IsActive && ctx.Posts(w.Id), w.Version, w.DataOwner, w.DataOwnerSince)).ToList(),
            options, departments, ctx.Configures);
    }

    public async Task<Guid> CreateAsync(ProcurementContext ctx, ProcurementWarehouseInput input, CancellationToken ct)
    {
        RequireSettings(ctx);
        var warehouse = new ProcurementWarehouse { Code = "", Name = "" };
        await ApplyAsync(warehouse, input, ct);
        db.ProcurementWarehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.warehouse.created", "procurement_warehouse", warehouse.Id.ToString(), Card(warehouse), ct);
        return warehouse.Id;
    }

    public async Task UpdateAsync(ProcurementContext ctx, Guid id, ProcurementWarehouseInput input, CancellationToken ct)
    {
        RequireSettings(ctx);
        var warehouse = await db.ProcurementWarehouses.Include(w => w.Keepers).FirstOrDefaultAsync(w => w.Id == id, ct)
                        ?? throw new FieldNotFoundException("Склад не найден.");
        if (input.Version is { } version && version != warehouse.Version)
        {
            throw new FieldConflictException("Склад уже изменили в другом окне — обновите страницу.");
        }

        var before = Card(warehouse);
        if (warehouse.IsActive && !input.IsActive)
        {
            if (warehouse.IsDefault)
            {
                throw new FieldConflictException("Склад по умолчанию не отключить: на нём резервируют заявки и приходуют закупки.");
            }

            var left = await db.ProcurementStockBalances.CountAsync(b => b.WarehouseId == id && (b.OnHand != 0 || b.Reserved != 0), ct);
            if (left > 0)
            {
                throw new FieldConflictException($"На складе «{warehouse.Name}» есть остаток ({left} поз.) — переместите или спишите его, затем отключите склад.");
            }
        }

        await ApplyAsync(warehouse, input, ct);
        warehouse.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.warehouse.updated", "procurement_warehouse", warehouse.Id.ToString(),
            new { before, after = Card(warehouse) }, ct);
    }

    private static void RequireSettings(ProcurementContext ctx)
    {
        if (!ctx.Configures)
        {
            throw new FieldForbiddenException("Склады заводит и меняет тот, у кого есть право «Настройки снабжения».");
        }
    }

    private static object Card(ProcurementWarehouse w) =>
        new
        {
            w.Code, w.Name, Kind = w.Kind.ToString(), w.DepartmentId, w.Address, w.IsActive, Keepers = w.Keepers.Select(k => k.UserId).Order().ToList(),
            DataOwner = w.DataOwner.ToString(), w.DataOwnerSince,
        };

    private async Task ApplyAsync(ProcurementWarehouse warehouse, ProcurementWarehouseInput input, CancellationToken ct)
    {
        var code = Text(input.Code, 32, "Код склада") ?? throw new FieldValidationException("Укажите код склада.");
        if (!CodePattern().IsMatch(code))
        {
            throw new FieldValidationException("Код склада — буквы, цифры, точка, дефис и подчёркивание, без пробелов.");
        }

        var name = Text(input.Name, 100, "Название склада") ?? throw new FieldValidationException("Укажите название склада.");
        var (lowerCode, lowerName) = (code.ToLower(), name.ToLower());
        if (await db.ProcurementWarehouses.AnyAsync(w => w.Id != warehouse.Id && w.Code.ToLower() == lowerCode, ct))
        {
            throw new FieldValidationException($"Код «{code}» уже у другого склада.");
        }

        if (await db.ProcurementWarehouses.AnyAsync(w => w.Id != warehouse.Id && w.Name.ToLower() == lowerName, ct))
        {
            throw new FieldValidationException($"Склад «{name}» уже есть.");
        }

        if (!Enum.IsDefined(input.Kind))
        {
            throw new FieldValidationException("Выберите вид склада.");
        }

        // Отдел — только у кладовой цеха: чья она.
        Guid? department = null;
        if (input.Kind == ProcurementWarehouseKind.Workshop && input.DepartmentId is { } dep)
        {
            department = await db.Departments.AnyAsync(d => d.Id == dep, ct) ? dep : throw new FieldValidationException("Отдел кладовой не найден.");
        }

        // Закрепить можно активного пользователя с правом «Склад снабжения» (уже закреплённого — оставить, даже если права сняли).
        var keeperIds = (input.KeeperIds ?? []).Distinct().ToList();
        var current = warehouse.Keepers.Select(k => k.UserId).ToHashSet();
        var allowed = (await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct)).ToHashSet();
        if (keeperIds.Any(k => !allowed.Contains(k) && !current.Contains(k)))
        {
            throw new FieldValidationException("Закрепить за складом можно только активного кладовщика (право «Склад снабжения»).");
        }

        warehouse.Code = code;
        warehouse.Name = name;
        warehouse.Kind = input.Kind;
        warehouse.DepartmentId = department;
        warehouse.Address = Text(input.Address, 300, "Адрес");
        warehouse.IsActive = warehouse.IsDefault || input.IsActive;
        // Владелец данных (D13): переключение — с датой; «не выбран» снимает дату.
        if (input.DataOwner is { } owner)
        {
            if (!Enum.IsDefined(owner))
            {
                throw new FieldValidationException("Выберите, кто ведёт данные склада.");
            }

            if (owner != warehouse.DataOwner || input.DataOwnerSince is not null)
            {
                warehouse.DataOwnerSince = owner == ProcurementDataOwner.None ? null : input.DataOwnerSince ?? FieldClock.Today;
            }

            warehouse.DataOwner = owner;
        }
        foreach (var gone in warehouse.Keepers.Where(k => !keeperIds.Contains(k.UserId)).ToList())
        {
            warehouse.Keepers.Remove(gone);
        }

        foreach (var added in keeperIds.Where(k => !current.Contains(k)))
        {
            warehouse.Keepers.Add(new ProcurementWarehouseKeeper { WarehouseId = warehouse.Id, UserId = added });
        }
    }

    private static string? Text(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= max ? text : throw new FieldValidationException($"{field} — до {max} символов.");
    }
}

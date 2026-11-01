using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;

namespace OneBase.Application.Procurement;

/// <summary>
/// Кому уведомления снабжения: директор (роль из базы), руководитель снабжения (руководитель отдела «Снабжение»), у кого право от роли
/// (снабжение, кладовщик). Администратор — техническая роль со всеми правами — в получатели «по праву» не попадает.
/// </summary>
internal static class ProcurementPeople
{
    public const string SupplyDepartment = "supply";

    public static Task<List<Guid>> DirectorsAsync(IAppDbContext db, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.IsActive && u.Roles.Any(x => x.Role.Name == SystemRoles.Director)).Select(u => u.Id).ToListAsync(ct);

    public static Task<List<Guid>> SupplyHeadsAsync(IAppDbContext db, CancellationToken ct) =>
        db.WorkDepartmentHeads.AsNoTracking().Where(h => h.Department!.Code == SupplyDepartment && h.User!.IsActive).Select(h => h.UserId).ToListAsync(ct);

    public static Task<List<Guid>> WithPermissionAsync(IAppDbContext db, string permission, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(x => x.Role.Name != SystemRoles.Admin && x.Role.Permissions.Any(p => p.Code == permission)))
            .Select(u => u.Id)
            .ToListAsync(ct);

    /// <summary>Руководство закупок: директор и руководитель снабжения.</summary>
    public static async Task<List<Guid>> LeadersAsync(IAppDbContext db, CancellationToken ct) =>
        [.. (await DirectorsAsync(db, ct)).Concat(await SupplyHeadsAsync(db, ct)).Distinct()];
}

using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;

namespace OneBase.Application.Procurement;

/// <summary>
/// Кому уведомления снабжения: директор (роль из базы), руководитель снабжения (руководитель отдела «Снабжение»), у кого есть право —
/// по итоговым правам: роль, разделы и права, отмеченные в карточке пользователя (снабжение, кладовщик, лаборатория). Администратор —
/// техническая роль со всеми правами — в получатели «по праву» не попадает (если право ему не отмечено галочкой).
/// </summary>
internal static class ProcurementPeople
{
    public const string SupplyDepartment = "supply";

    public static Task<List<Guid>> DirectorsAsync(IAppDbContext db, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.IsActive && u.Roles.Any(x => x.Role.Name == SystemRoles.Director)).Select(u => u.Id).ToListAsync(ct);

    public static Task<List<Guid>> SupplyHeadsAsync(IAppDbContext db, CancellationToken ct) =>
        db.WorkDepartmentHeads.AsNoTracking().Where(h => h.Department!.Code == SupplyDepartment && h.User!.IsActive).Select(h => h.UserId).ToListAsync(ct);

    public static Task<List<Guid>> WithPermissionAsync(IAppDbContext db, string permission, CancellationToken ct) =>
        db.ActiveUsersWithAsync(permission, ct, withoutAdmin: true);

    /// <summary>Аналитики закупок — роль «Аналитик».</summary>
    public static Task<List<Guid>> AnalystsAsync(IAppDbContext db, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.IsActive && u.Roles.Any(x => x.Role.Name == SystemRoles.AnalystRole)).Select(u => u.Id).ToListAsync(ct);

    /// <summary>Финансист: выбранный в маршруте согласования («Настройки → Отделы → Снабжение»); пока не выбран — сотрудники с ролью «Финансы».</summary>
    public static async Task<List<Guid>> FinanceAsync(IAppDbContext db, CancellationToken ct)
    {
        var chosen = await db.ProcurementSettings.AsNoTracking().Select(s => s.FinanceApproverId).FirstOrDefaultAsync(ct);
        if (chosen is { } id && await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.IsActive, ct))
        {
            return [id];
        }

        return await db.Users.AsNoTracking().Where(u => u.IsActive && u.Roles.Any(x => x.Role.Name == FinanceRole)).Select(u => u.Id).ToListAsync(ct);
    }

    /// <summary>Роль финансового отдела.</summary>
    public const string FinanceRole = "Финансы";

    /// <summary>Руководство закупок: директор и руководитель снабжения.</summary>
    public static async Task<List<Guid>> LeadersAsync(IAppDbContext db, CancellationToken ct) =>
        [.. (await DirectorsAsync(db, ct)).Concat(await SupplyHeadsAsync(db, ct)).Distinct()];
}

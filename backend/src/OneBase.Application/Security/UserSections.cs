using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Security;

/// <summary>
/// Раздел OneBase, который выбирается пользователю галочкой. Permissions — все права раздела (снимаются, если раздела нет);
/// Grant — что выдаётся, если раздел отмечен, а у роли его нет (у роли есть — остаются права роли: «Файлы» администратора остаются
/// со всеми отделами).
/// </summary>
public sealed record UserSection(string Key, string Title, string Description, IReadOnlyList<string> Permissions, IReadOnlyList<string> Grant);

/// <summary>
/// Разделы пользователя. Роль задаёт права на управление и настройки и разделы по умолчанию; у пользователя разделы можно отметить
/// галочками (User.Sections). Итоговые права — права роли без прав разделов плюс права отмеченных разделов: так видно в меню, на
/// страницах, в API и в ответах AI-сотрудников (все они берут права отсюда). Sections = null — разделы как у роли.
/// </summary>
public static class UserSections
{
    public const string Sales = "sales";
    public const string Finance = "finance";
    public const string Hr = "hr";
    public const string Tasks = "tasks";
    public const string Files = "files";
    public const string Consultant = "consultant";
    public const string Field = "field";
    public const string DataManager = "data";
    public const string Campaigns = "campaigns";

    public static readonly IReadOnlyList<UserSection> All =
    [
        new(Sales, "Продажи", "Вторичка, первичка, планы, АКБ, остатки, акции, KPI; AI отвечает по продажам, ассортименту и клиентам.",
            [Permissions.SalesRead, Permissions.SalesManage], [Permissions.SalesRead]),
        new(Finance, "Финансы", "Оплаты, P&L дилеров, акт сверки, DSO, суммы KPI; AI-сотрудник финансов.",
            [Permissions.FinanceRead], [Permissions.FinanceRead]),
        new(Hr, "HR", "Сотрудники, посещаемость, опоздания и табели Verifix; HR AI по всем сотрудникам.",
            [Permissions.HrRead], [Permissions.HrRead]),
        new(Tasks, "Задачи", "Задачи своего отдела и проектов; AI отвечает по задачам и просрочкам.",
            [Permissions.WorkTasks], [Permissions.WorkTasks]),
        new(Files, "Файлы", "Файлы своего отдела — просмотр и загрузка; документы базы знаний в ответах AI.",
            [Permissions.FilesRead, Permissions.FilesWrite, Permissions.FilesReadAll, Permissions.FilesManage], [Permissions.FilesRead, Permissions.FilesWrite]),
        new(Consultant, "AI-консультант", "Чат с консультантом и AI-сотрудниками отделов, Telegram-агент от имени пользователя.",
            [Permissions.AgentsRun], [Permissions.AgentsRun]),
        new(Field, "Sales Base", "Полевая команда: задачи агентам и супервайзерам, маршруты, визиты.",
            [Permissions.FieldPlan, Permissions.FieldUse, Permissions.FieldManage], [Permissions.FieldPlan]),
        new(DataManager, "AI Data Manager", "Загрузка отчётов и изменение планов, целей и дилеров с подтверждением.",
            [Permissions.DataManage], [Permissions.DataManage]),
        new(Campaigns, "Рассылки", "Рассылки в Telegram: создание, запуск и отмена.",
            [Permissions.TelegramCampaigns], [Permissions.TelegramCampaigns]),
    ];

    public static UserSection? Find(string? key) => All.FirstOrDefault(s => s.Key == key);

    /// <summary>Права, которые принадлежат разделам (остальные — только роль).</summary>
    public static readonly IReadOnlySet<string> SectionPermissions = All.SelectMany(s => s.Permissions).ToHashSet(StringComparer.Ordinal);

    /// <summary>Итоговые права: права роли, а если разделы выбраны — без прав невыбранных разделов и с правами выбранных.</summary>
    public static HashSet<string> Effective(IEnumerable<string> rolePermissions, IReadOnlyCollection<string>? sections)
    {
        var role = rolePermissions.ToHashSet(StringComparer.Ordinal);
        if (sections is null)
        {
            return role;
        }

        var result = role.Where(p => !SectionPermissions.Contains(p)).ToHashSet(StringComparer.Ordinal);
        foreach (var section in All.Where(s => sections.Contains(s.Key)))
        {
            var own = section.Permissions.Where(role.Contains).ToList();
            result.UnionWith(own.Count > 0 ? own : section.Grant);
        }

        return result;
    }

    /// <summary>Какие разделы открыты при этих правах (раздел открыт, если есть хоть одно его право) — в порядке каталога.</summary>
    public static List<string> Of(IEnumerable<string> permissions)
    {
        var set = permissions.ToHashSet(StringComparer.Ordinal);
        return All.Where(s => s.Permissions.Any(set.Contains)).Select(s => s.Key).ToList();
    }

    /// <summary>Только известные разделы, без повторов, в порядке каталога.</summary>
    public static List<string> Normalize(IEnumerable<string> keys)
    {
        var set = keys.Select(k => k.Trim()).ToHashSet(StringComparer.Ordinal);
        return All.Where(s => set.Contains(s.Key)).Select(s => s.Key).ToList();
    }

    /// <summary>
    /// Итоговые права пользователя из базы; null — пользователя нет или он отключён (includeInactive — права и у отключённого: чтобы
    /// понять, можно ли его менять).
    /// </summary>
    public static async Task<HashSet<string>?> EffectivePermissionsAsync(this IAppDbContext db, Guid userId, CancellationToken ct, bool includeInactive = false)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.Sections, Permissions = u.Roles.SelectMany(r => r.Role.Permissions.Select(p => p.Code)).ToList() })
            .FirstOrDefaultAsync(ct);
        return row is not null && (row.IsActive || includeInactive) ? Effective(row.Permissions, row.Sections) : null;
    }

    /// <summary>Активные пользователи, у которых итоговые права включают permission (уведомления, проверки «кто может»).</summary>
    public static async Task<List<Guid>> ActiveUsersWithAsync(this IAppDbContext db, string permission, CancellationToken ct)
    {
        var rows = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && (u.Sections != null || u.Roles.Any(r => r.Role.Permissions.Any(p => p.Code == permission))))
            .Select(u => new { u.Id, u.Sections, Permissions = u.Roles.SelectMany(r => r.Role.Permissions.Select(p => p.Code)).ToList() })
            .ToListAsync(ct);
        return rows.Where(r => Effective(r.Permissions, r.Sections).Contains(permission)).Select(r => r.Id).ToList();
    }
}

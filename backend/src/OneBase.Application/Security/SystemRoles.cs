namespace OneBase.Application.Security;

/// <summary>
/// Роли OneBase: администратор, директор и по роли на каждый отдел. Создаются при запуске, если их нет.
/// Назначить роль может только тот, у кого есть все её права, — так директор не выдаст себе права администратора.
/// </summary>
public static class SystemRoles
{
    public const string Admin = "Администратор";

    /// <summary>Прежнее название роли администратора — переименовывается при запуске.</summary>
    public const string LegacyAdmin = "Admin";

    public const string Director = "Директор";

    public sealed record Definition(string Name, string Description, string? DepartmentCode, IReadOnlyList<string> Permissions);

    /// <summary>Общее для сотрудников отделов: консультант и файлы.</summary>
    private static readonly string[] Employee = [Permissions.AgentsRun, Permissions.FilesRead, Permissions.FilesWrite];

    public static readonly IReadOnlyList<Definition> All =
    [
        new(Admin, "Полный доступ: все данные, пользователи и роли, интеграции и настройки AI.", null, Permissions.All),
        new(Director, "Все данные отделов, консультант, журнал аудита, пользователи и роли. Интеграции и настройки AI — у администратора.", null,
        [
            Permissions.UsersManage, Permissions.SalesRead, Permissions.FinanceRead, Permissions.HrRead, Permissions.FilesRead, Permissions.FilesWrite,
            Permissions.AgentsRun, Permissions.ApprovalsDecide, Permissions.AuditRead,
        ]),
        new("Продажи", "Сотрудник отдела продаж: раздел «Продажи», консультант, файлы.", "sales", [Permissions.SalesRead, .. Employee]),
        new("Финансы", "Сотрудник финансового отдела: оплаты и продажи, консультант, файлы.", "finance", [Permissions.FinanceRead, Permissions.SalesRead, .. Employee]),
        new("HR", "Сотрудник HR: кадровые данные, консультант, файлы.", "hr", [Permissions.HrRead, .. Employee]),
        new("Производство", "Сотрудник производства: остатки и продажи товаров (раздел «Продажи»), консультант, файлы.", "production", [Permissions.SalesRead, .. Employee]),
        new("Снабжение", "Сотрудник снабжения: остатки, поставщики и продажи товаров (раздел «Продажи»), консультант, файлы.", "supply", [Permissions.SalesRead, .. Employee]),
        new("Маркетинг", "Сотрудник маркетинга: продажи, ассортимент и клиенты (раздел «Продажи»), консультант, файлы.", "marketing", [Permissions.SalesRead, .. Employee]),
    ];

    /// <summary>Права, с которыми виден раздел «Настройки»; без них раздел скрыт.</summary>
    public static readonly IReadOnlyList<string> SettingsPermissions =
        [Permissions.UsersManage, Permissions.IntegrationsManage, Permissions.AiSettingsManage, Permissions.SalesManage];

    /// <summary>Порядок ролей в списках: администратор, директор, отделы.</summary>
    public static int OrderOf(string name)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Name == name)
            {
                return i;
            }
        }

        return All.Count;
    }

    /// <summary>Подписи прав для интерфейса.</summary>
    public static readonly IReadOnlyDictionary<string, string> PermissionLabels = new Dictionary<string, string>
    {
        [Permissions.UsersManage] = "Пользователи и роли",
        [Permissions.IntegrationsManage] = "Интеграции",
        [Permissions.AiSettingsManage] = "Настройки AI",
        [Permissions.SalesRead] = "Продажи",
        [Permissions.SalesManage] = "Продажи: настройки и синхронизация",
        [Permissions.FinanceRead] = "Финансы",
        [Permissions.HrRead] = "HR",
        [Permissions.FilesRead] = "Файлы: просмотр",
        [Permissions.FilesWrite] = "Файлы: загрузка",
        [Permissions.AgentsRun] = "AI-консультант",
        [Permissions.ApprovalsDecide] = "Подтверждение действий AI",
        [Permissions.AuditRead] = "Журнал аудита",
    };
}

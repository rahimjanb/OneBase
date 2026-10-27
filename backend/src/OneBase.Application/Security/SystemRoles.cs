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

    /// <summary>
    /// Администратор данных продаж: файлы отдела продаж и AI Data Manager (отчёты → планы, цели, дилеры — с подтверждением).
    /// Без пользователей, интеграций, настроек AI и разделов аналитики.
    /// </summary>
    public const string DataAdmin = "Админ 2";

    /// <summary>Роли Sales Base (sales.1base.uz).</summary>
    public const string FieldRm = "РМ (Sales Base)";

    public const string FieldSupervisor = "Супервайзер";
    public const string FieldAgent = "Агент";

    public sealed record Definition(string Name, string Description, string? DepartmentCode, IReadOnlyList<string> Permissions);

    /// <summary>Общее для сотрудников отделов: консультант, файлы и задачи.</summary>
    private static readonly string[] Employee = [Permissions.AgentsRun, Permissions.FilesRead, Permissions.FilesWrite, Permissions.WorkTasks];

    public static readonly IReadOnlyList<Definition> All =
    [
        new(Admin, "Полный доступ: все данные, пользователи и роли, интеграции и настройки AI.", null, Permissions.All),
        new(Director, "Все данные отделов, консультант, журнал аудита, пользователи и роли. Интеграции и настройки AI — у администратора.", null,
        [
            Permissions.UsersManage, Permissions.SalesRead, Permissions.FinanceRead, Permissions.HrRead, Permissions.FilesRead, Permissions.FilesWrite,
            Permissions.FilesReadAll,
            Permissions.AgentsRun, Permissions.ApprovalsDecide, Permissions.AuditRead, Permissions.FieldPlan, Permissions.WorkTasks,
        ]),
        new(DataAdmin, "Файлы отдела продаж и AI Data Manager: загрузка отчётов, изменение планов, целей и дилеров регионов с подтверждением. "
                       + "В «Настройках» — только AI Data Manager.", "sales",
            [Permissions.FilesRead, Permissions.FilesWrite, Permissions.DataManage]),
        new("Продажи", "Сотрудник отдела продаж: раздел «Продажи», консультант, файлы.", "sales", [Permissions.SalesRead, .. Employee]),
        new("Финансы", "Сотрудник финансового отдела: оплаты и продажи, консультант, файлы.", "finance", [Permissions.FinanceRead, Permissions.SalesRead, .. Employee]),
        new("HR", "Сотрудник HR: кадровые данные, консультант, файлы.", "hr", [Permissions.HrRead, .. Employee]),
        new("Производство", "Сотрудник производства: остатки и продажи товаров (раздел «Продажи»), консультант, файлы.", "production", [Permissions.SalesRead, .. Employee]),
        new("Снабжение", "Сотрудник снабжения: остатки, поставщики и продажи товаров (раздел «Продажи»), консультант, файлы.", "supply", [Permissions.SalesRead, .. Employee]),
        new("Маркетинг", "Сотрудник маркетинга: продажи, ассортимент и клиенты (раздел «Продажи»), консультант, файлы.", "marketing", [Permissions.SalesRead, .. Employee]),
        new(FieldRm, "Sales Base: вся структура полевых продаж — команды, точки, маршруты, задачи, KPI, решения по рекомендациям AI; аналитика продаж и консультант.", null,
            [Permissions.FieldUse, Permissions.FieldManage, Permissions.SalesRead, Permissions.AgentsRun, Permissions.WorkTasks]),
        new(FieldSupervisor, "Sales Base: своя команда — агенты, точки, маршруты, визиты, задачи и KPI команды.", null, [Permissions.FieldUse]),
        new(FieldAgent, "Sales Base с телефона: свой маршрут, точки, визиты, задачи, продажи и KPI.", null, [Permissions.FieldUse]),
    ];

    /// <summary>
    /// Права, которые добавляются уже созданным ролям при запуске (права ролей иначе задаются только при создании):
    /// так новые возможности доходят до существующих баз. Только добавление — снятые вручную права вернутся.
    /// </summary>
    public static readonly IReadOnlyList<(string Role, string Permission)> Upgrades =
    [
        (Director, Permissions.FieldPlan),
        (Director, Permissions.FilesReadAll),
        // «Задачи» стали отдельным правом (раньше их видели все с общими разделами) — у кого раздел был, у того и остаётся.
        (Director, Permissions.WorkTasks),
        ("Продажи", Permissions.WorkTasks),
        ("Финансы", Permissions.WorkTasks),
        ("HR", Permissions.WorkTasks),
        ("Производство", Permissions.WorkTasks),
        ("Снабжение", Permissions.WorkTasks),
        ("Маркетинг", Permissions.WorkTasks),
        (FieldRm, Permissions.WorkTasks),
    ];

    /// <summary>Права, с которыми виден раздел «Настройки»; без них раздел скрыт.</summary>
    public static readonly IReadOnlyList<string> SettingsPermissions =
        [Permissions.UsersManage, Permissions.IntegrationsManage, Permissions.AiSettingsManage, Permissions.DataManage, Permissions.SalesManage, Permissions.LogsRead,
            Permissions.TelegramManage];

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
        [Permissions.DataManage] = "AI Data Manager: изменение данных по отчётам",
        [Permissions.LogsRead] = "Журнал ошибок",
        [Permissions.SalesRead] = "Продажи",
        [Permissions.SalesManage] = "Продажи: настройки и синхронизация",
        [Permissions.FinanceRead] = "Финансы",
        [Permissions.HrRead] = "HR",
        [Permissions.FilesRead] = "Файлы: просмотр",
        [Permissions.FilesWrite] = "Файлы: загрузка",
        [Permissions.AgentsRun] = "AI-консультант",
        [Permissions.ApprovalsDecide] = "Подтверждение действий AI",
        [Permissions.AuditRead] = "Журнал аудита",
        [Permissions.FieldUse] = "Sales Base",
        [Permissions.FieldManage] = "Sales Base: управление",
        [Permissions.FieldPlan] = "Sales Base: задачи и маршруты",
        [Permissions.TelegramManage] = "Telegram: аккаунты и AI-агент",
        [Permissions.TelegramCampaigns] = "Рассылки в Telegram",
        [Permissions.WorkTasks] = "Задачи отделов",
    };
}

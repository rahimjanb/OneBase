namespace OneBase.Application.Security;

/// <summary>Коды разрешений RBAC. Выдаются ролям и попадают в JWT как claim "perm".</summary>
public static class Permissions
{
    public const string UsersManage = "users.manage";
    public const string FilesRead = "files.read";
    public const string FilesWrite = "files.write";
    /// <summary>«Файлы отделов»: все отделы и подключения к Windows (создать, сменить пароль, отозвать). Только у администратора.</summary>
    public const string FilesManage = "files.manage";
    public const string AgentsRun = "ai.agents.run";
    public const string ApprovalsDecide = "ai.approvals.decide";
    public const string AuditRead = "audit.read";
    public const string SalesRead = "sales.read";
    public const string SalesManage = "sales.manage";
    public const string IntegrationsManage = "integrations.manage";

    /// <summary>Финансовые данные (оплаты и др.) — в том числе в ответах AI.</summary>
    public const string FinanceRead = "finance.read";

    /// <summary>Кадровые данные — в том числе в ответах AI.</summary>
    public const string HrRead = "hr.read";

    /// <summary>«Настройки → AI»: провайдеры, ключи API, модели, агенты.</summary>
    public const string AiSettingsManage = "ai.settings.manage";

    /// <summary>«Настройки → Журнал ошибок»: ошибки Linko, синхронизации и сервера. Только у администратора.</summary>
    public const string LogsRead = "system.logs.read";

    /// <summary>Sales Base: работа агента, супервайзера, РМ. Что именно видно — решает участник Sales Base (FieldScope).</summary>
    public const string FieldUse = "field.use";

    /// <summary>Sales Base: управление — состав и команды, доступы, настройки, рекомендации AI всей организации.</summary>
    public const string FieldManage = "field.manage";

    /// <summary>
    /// Sales Base из OneBase: задачи агентам и супервайзерам, маршруты, данные всей организации — без управления составом,
    /// доступами и настройками (это field.manage). Для руководителей без карточки участника.
    /// </summary>
    public const string FieldPlan = "field.plan";

    public static readonly IReadOnlyList<string> All =
    [
        IntegrationsManage,
        AiSettingsManage,
        LogsRead,
        UsersManage,
        FilesRead,
        FilesWrite,
        FilesManage,
        AgentsRun,
        ApprovalsDecide,
        AuditRead,
        SalesRead,
        SalesManage,
        FinanceRead,
        HrRead,
        FieldUse,
        FieldManage,
        FieldPlan,
    ];
}

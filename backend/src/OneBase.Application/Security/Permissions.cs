namespace OneBase.Application.Security;

/// <summary>Коды разрешений RBAC. Выдаются ролям и попадают в JWT как claim "perm".</summary>
public static class Permissions
{
    public const string UsersManage = "users.manage";
    public const string FilesRead = "files.read";
    public const string FilesWrite = "files.write";
    /// <summary>«Файлы отделов»: все отделы и подключения к Windows (создать, сменить пароль, отозвать). Только у администратора.</summary>
    public const string FilesManage = "files.manage";
    /// <summary>«Файлы»: просмотр файлов всех отделов (папки отделов — только чтение). У администратора и директора.</summary>
    public const string FilesReadAll = "files.read.all";
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

    /// <summary>
    /// AI Data Manager: загрузка отчётов, анализ через Claude, чтение данных продаж внутри раздела и подтверждение изменений данных OneBase
    /// (планы регионов, цели, дилеры и СВР регионов, P&L дилеров). Других прав на продажи не требует — роль «Админ 2» работает только им.
    /// </summary>
    public const string DataManage = "ai.data.manage";

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

    /// <summary>Telegram: аккаунты, AI-агент, доступ пользователей Telegram, журнал агента.</summary>
    public const string TelegramManage = "telegram.manage";

    /// <summary>Рассылки в Telegram: создание, запуск, пауза, отмена.</summary>
    public const string TelegramCampaigns = "telegram.campaigns";

    /// <summary>«Задачи»: задачи отделов и проекты (что из них видно — решает WorkTaskService по отделу и руководству), и в ответах AI.</summary>
    public const string WorkTasks = "work.tasks";

    /// <summary>«Заявки»: создавать заявки на закупку и видеть свои (руководитель отдела — и заявки своего отдела). Не раздел пользователя — есть у всех ролей отделов.</summary>
    public const string ProcurementRequest = "procurement.request";

    /// <summary>«Заявки»: все отправленные заявки компании (снабжение, директор). Черновики видит только инициатор.</summary>
    public const string ProcurementRead = "procurement.read";

    /// <summary>Снабжение: обработка заявок — передать директору на согласование, вернуть на доработку.</summary>
    public const string ProcurementManage = "procurement.manage";

    /// <summary>Настройки снабжения: категории закупок, допуск без повторного согласования.</summary>
    public const string ProcurementSettings = "procurement.settings";

    /// <summary>
    /// Склад снабжения (кладовщик): каталог, приход, выдача отделам и по заявкам, списание, инвентаризация; видит заявки, в которых есть
    /// часть «со склада». Утверждать закупки и менять цены не может.
    /// </summary>
    public const string ProcurementWarehouse = "procurement.warehouse";

    /// <summary>
    /// Контроль качества сырья (лаборатория): очередь проверок, проверка документов, показатели и пробы, спецификации материалов; видит
    /// склад и партии. Решение «допустить / забраковать» — не право, а назначение в настройках качества.
    /// </summary>
    public const string QualityCheck = "quality.check";

    /// <summary>«Производство»: рецептуры, задания на выпуск, расход сырья и аналитика производства — просмотр.</summary>
    public const string ProductionRead = "production.read";

    /// <summary>Задания на выпуск (мастер, руководитель производства): создать, выпустить, закрыть с фактическим выходом, отменить.</summary>
    public const string ProductionManage = "production.manage";

    /// <summary>Рецептуры (технолог): создать, править черновик, ввести версию в действие, архивировать.</summary>
    public const string ProductionRecipes = "production.recipes";

    public static readonly IReadOnlyList<string> All =
    [
        IntegrationsManage,
        AiSettingsManage,
        DataManage,
        LogsRead,
        UsersManage,
        FilesRead,
        FilesWrite,
        FilesManage,
        FilesReadAll,
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
        TelegramManage,
        TelegramCampaigns,
        WorkTasks,
        ProcurementRequest,
        ProcurementRead,
        ProcurementManage,
        ProcurementSettings,
        ProcurementWarehouse,
        QualityCheck,
        ProductionRead,
        ProductionManage,
        ProductionRecipes,
    ];
}

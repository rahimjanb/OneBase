namespace OneBase.Application.Security;

/// <summary>
/// Право пользователя в разделе отдела или в настройках, которое отмечается галочкой: Key — код права (одно право — одна галочка), группа
/// (supply — снабжение и склад, production — производство, settings — настройки), подпись, что оно даёт; Requires — права, которые
/// включаются вместе с ним (закупки — и просмотр заявок); Needs — право раздела пользователя, без которого это право не действует
/// (настройки продаж — только с разделом «Продажи»), NeedsSection — код этого раздела.
/// </summary>
public sealed record UserRight(string Key, string Group, string Title, string Description, IReadOnlyList<string> Requires, string? Needs = null,
    string? NeedsSection = null);

/// <summary>Группа прав в окне пользователя.</summary>
public sealed record UserRightGroup(string Key, string Title, string Description);

/// <summary>
/// Права в разделах отделов и настройках — вторая часть доступа пользователя после разделов (UserSections). Роль даёт свои права; у
/// пользователя их можно отметить галочками (User.Rights): тогда права из этого списка — только отмеченные, остальные права роли остаются.
/// Rights = null — как у роли. По итоговым правам открываются меню, страницы и действия, им же приходят уведомления «по праву».
/// </summary>
public static class UserRights
{
    public const string Supply = "supply";
    public const string Production = "production";
    public const string Sales = "sales";
    public const string Settings = "settings";

    public static readonly IReadOnlyList<UserRightGroup> Groups =
    [
        new(Supply, "Снабжение и склад", "Заявки, закупки, склад и контроль качества сырья: что сотрудник видит и что может делать."),
        new(Production, "Производство", "Задания на выпуск, техкарты и сменные отчёты."),
        new(Sales, "Продажи", "Что сотрудник может делать в разделе «Продажи» сверх просмотра. Что он видит — охват продаж ниже."),
        new(Settings, "Настройки", "Какие страницы «Настроек» откроются сотруднику."),
    ];

    public static readonly IReadOnlyList<UserRight> All =
    [
        new(Permissions.ProcurementRequest, Supply, "Свои заявки", "Создавать заявки на закупку и следить за ними — «Заявки → Мои».", []),
        new(Permissions.ProcurementRead, Supply, "Все заявки и снабжение — просмотр",
            "Заявки всех отделов, обзор снабжения, тендеры, поставки, поставщики, склад и отчёты — без изменений.", []),
        new(Permissions.ProcurementManage, Supply, "Закупки", "«К закупке» и «Куплено», тендеры, поставщики, обработка заявок, отмена поставок.",
            [Permissions.ProcurementRead]),
        new(Permissions.ProcurementWarehouse, Supply, "Склад",
            "Остатки, приход, выдача и «К выдаче», приезд машин и приёмка с весом, партии, движения, инвентаризация.", []),
        new(Permissions.QualityCheck, Supply, "Качество сырья", "Лаборатория: проверка документов и показателей партий, пробы, спецификации.", []),
        new(Permissions.ProductionRead, Production, "Производство — просмотр", "Обзор, задания, техкарты, смены и отчёты — без изменений.", []),
        new(Permissions.ProductionManage, Production, "Задания на выпуск", "Создавать, выпускать и закрывать задания, утверждать смены.", [Permissions.ProductionRead]),
        new(Permissions.ProductionRecipes, Production, "Технолог", "Техкарты: этапы, рецептура, нормы, версии и утверждение.", [Permissions.ProductionRead]),
        new(Permissions.ProductionOperate, Production, "Ввод смен", "Сменные отчёты: замесы, выпечка, брак, упаковка.", [Permissions.ProductionRead]),
        new(Permissions.SalesExport, Sales, "Выгрузки Excel",
            "Скачивать остатки, акт автозаказа и обзор первички в Excel — только в пределах своего охвата продаж.", [], Permissions.SalesRead, UserSections.Sales),
        new(Permissions.UsersManage, Settings, "Пользователи и роли", "Сотрудники, роли и доступ; отделы и их руководители.", []),
        new(Permissions.ProcurementSettings, Settings, "Настройки снабжения", "Категории закупок, маршрут согласования, склады, контроль качества.", []),
        new(Permissions.IntegrationsManage, Settings, "Интеграции и источники данных", "Linko, Google Sheets и Verifix: подключения и обновление данных.", []),
        new(Permissions.AiSettingsManage, Settings, "Настройки AI", "Провайдеры, ключи, модели и доступ AI-агентов.", []),
        new(Permissions.TelegramManage, Settings, "Telegram", "Аккаунты Telegram и AI-агент в чатах.", []),
        new(Permissions.SalesManage, Settings, "Продажи: оргструктура и цели", "Синхронизация с Linko, направления, дилеры регионов, ТП и цели.", [],
            Permissions.SalesRead, UserSections.Sales),
        new(Permissions.LogsRead, Settings, "Журнал ошибок", "Ошибки интеграций и сервера, отмена идущей синхронизации.", []),
    ];

    /// <summary>Права, которые выбираются галочками (остальные права — только роль и разделы).</summary>
    public static readonly IReadOnlySet<string> Keys = All.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

    public static UserRight? Find(string? key) => All.FirstOrDefault(r => r.Key == key);

    /// <summary>Только известные права, с теми, что включаются вместе с ними, без повторов, в порядке каталога.</summary>
    public static List<string> Normalize(IEnumerable<string> keys)
    {
        var set = keys.Select(k => k.Trim()).Where(Keys.Contains).ToHashSet(StringComparer.Ordinal);
        foreach (var right in All.Where(r => set.Contains(r.Key)).ToList())
        {
            set.UnionWith(right.Requires);
        }

        return All.Where(r => set.Contains(r.Key)).Select(r => r.Key).ToList();
    }

    /// <summary>Какие права из списка есть при этих правах — в порядке каталога.</summary>
    public static List<string> Of(IEnumerable<string> permissions)
    {
        var set = permissions.ToHashSet(StringComparer.Ordinal);
        return All.Where(r => set.Contains(r.Key)).Select(r => r.Key).ToList();
    }

    /// <summary>
    /// Права отмечены (rights не null) — права из списка заменяются отмеченными; затем убираются права, которым не хватает раздела (настройки
    /// продаж без раздела «Продажи»).
    /// </summary>
    public static void Apply(HashSet<string> permissions, IReadOnlyCollection<string>? rights)
    {
        if (rights is not null)
        {
            permissions.ExceptWith(Keys);
            permissions.UnionWith(Normalize(rights));
        }

        foreach (var right in All.Where(r => r.Needs is not null && !permissions.Contains(r.Needs)))
        {
            permissions.Remove(right.Key);
        }
    }
}

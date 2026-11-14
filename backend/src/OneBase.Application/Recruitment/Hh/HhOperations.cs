namespace OneBase.Application.Recruitment.Hh;

/// <summary>
/// Операция API HH в каталоге Quota Guard: стоимость (free — услуги и лимиты не расходует; limit — бесплатно, но расходует дневной лимит
/// просмотров HH; paid — списывает платные услуги; unknown — бесплатность не подтверждена), разрешена ли автоматически (синхронизация) и
/// вручную (кнопкой HR), почему и источник. Всё, что не free, заблокировано — запрос в HH не уходит.
/// </summary>
public sealed record HhOperation(string Code, string Method, string Path, string Cost, bool Auto, bool Manual, string Why, string Source);

/// <summary>
/// Каталог операций HH (аудит 14.11.2026 по спецификации https://api.hh.ru/openapi/specification/public, статьям hh.ru/article/31102 и
/// hh.ru/article/28570, соглашению https://dev.hh.ru/admin/developer_agreement). Бизнес-правило: платные ресурсы HH не расходуются никогда;
/// бесплатность не подтверждена — операция считается платной и заблокирована. Новые методы добавляются сюда с источником — иначе вызвать нельзя.
/// </summary>
public static class HhOperations
{
    public const string Free = "free";
    public const string Limit = "limit";
    public const string Paid = "paid";
    public const string Unknown = "unknown";

    public const string Token = "oauth.token";
    public const string Me = "me";
    public const string Vacancies = "vacancies.active";
    public const string Negotiations = "negotiations.list";
    public const string Action = "negotiations.action";
    public const string ActionMessage = "negotiations.action.message";
    public const string ResumeView = "resume.view";
    public const string ResumeSearch = "resume.search";
    public const string Contacts = "resume.contacts";
    public const string Messages = "negotiations.messages";
    public const string InviteWithoutResponse = "negotiations.invite.new";
    public const string ResumeHistory = "resume.negotiations_history";
    public const string Limits = "employer.limits";

    private const string Spec = "api.hh.ru/openapi/specification/public";

    public static readonly IReadOnlyList<HhOperation> All =
    [
        new(Token, "POST", "/token", Free, true, true, "Вход и обновление токена OAuth — не платный метод.", Spec),
        new(Me, "GET", "/me", Free, true, true, "Кто вошёл: работодатель. В группы платных методов не входит.", Spec),
        new(Vacancies, "GET", "/employers/{id}/vacancies/active", Free, true, true, "Свои опубликованные вакансии. В группы платных методов не входит.", Spec),
        new(Negotiations, "GET", "/negotiations/response", Free, true, true,
            "Отклики на свои вакансии: с декабря 2022 работа с откликами не требует покупки услуг, «для работы с существующим откликом доступ не требуется». "
            + "Если HH всё же ответит, что нужна оплата (api_access_payment), — предохранитель останавливает синхронизацию.",
            "hh.ru/article/31102; " + Spec),
        new(Action, "PUT/POST", "url из actions[] отклика", Free, false, true,
            "Пригласить или отказать по существующему отклику — без списания. Только кнопкой HR и без текста сообщения.", Spec),
        new(ActionMessage, "PUT/POST", "url из actions[] + message", Unknown, false, false,
            "Сообщение кандидату через HH — запрещено правилом (сообщения через HH не отправляем); методы переписки в откликах устарели.", Spec),
        new(ResumeView, "GET", "/resumes/{id}", Limit, false, false,
            "Резюме откликнувшегося открывается без списания контакта, но расходует дневной лимит просмотров резюме (500 в сутки). Правило — лимиты не "
            + "расходовать: резюме HR открывает на hh.uz сам и загружает файлом. По вакансиям с оплатой за отклик (HH_PAY_FOR_PERFORMANCE) стоимость не подтверждена.",
            "hh.ru/article/31102; hh.ru/article/28570; " + Spec),
        new(ResumeSearch, "GET", "/resumes", Limit, false, false,
            "Поиск по базе резюме: без услуги — лимит 50 просмотров в сутки, открытие контактов платное. Бесплатного доступа ко всей базе нет.", "hh.ru/article/31102; " + Spec),
        new(Contacts, "GET", "ссылки контактов из resume.actions", Paid, false, false, "«При использовании этих ссылок будет списание контакта у клиента».", Spec),
        new(Messages, "GET/POST", "/negotiations/{id}/messages", Unknown, false, false, "Методы устарели и не поддерживаются; сообщения через HH правилом запрещены.", Spec),
        new(InviteWithoutResponse, "POST", "/negotiations/phone_interview", Paid, false, false,
            "Приглашение кандидата без отклика списывает контакт и требует доступа к базе резюме.", "hh.ru/article/28570; " + Spec),
        new(ResumeHistory, "GET", "/resumes/{id}/negotiations_history", Paid, false, false, "Помечен в спецификации как требующий платного доступа.", Spec),
        new(Limits, "GET", "/employers/{id}/managers/{id}/limits/resume, method_access, payable_api_actions", Unknown, false, false,
            "Справочные методы остатков: в платные группы не входят, но HH прямо не пишет, что они бесплатны — не вызываются.", Spec),
    ];

    public static HhOperation Get(string code) => All.FirstOrDefault(o => o.Code == code) ?? new HhOperation(code, "?", "?", Unknown, false, false,
        "Операции нет в каталоге — стоимость не подтверждена.", "—");

    /// <summary>Решение Quota Guard по каталогу: разрешено ли и почему нет. Перед отправкой проверяются ещё предохранитель и суточный лимит OneBase.</summary>
    public static (bool Allowed, string? Reason) Check(string code, bool manual)
    {
        var op = Get(code);
        if (op.Cost != Free)
        {
            var cost = op.Cost switch { Limit => "расходует лимиты HH", Paid => "платная операция HH", _ => "бесплатность не подтверждена" };
            return (false, $"Заблокировано: {cost}. {op.Why}");
        }

        return manual ? (op.Manual, op.Manual ? null : $"Заблокировано: операция только автоматическая. {op.Why}")
            : (op.Auto, op.Auto ? null : $"Заблокировано: операция выполняется только кнопкой HR. {op.Why}");
    }

    /// <summary>
    /// Ответы HH о деньгах и лимитах: нужна оплата или услуга, исчерпан лимит. Запрос при этом ничего не списал, но повторять его автоматически
    /// бессмысленно — предохранитель останавливает синхронизацию до решения HR.
    /// </summary>
    public static bool IsBillingError(string? type, string? value) =>
        type is "api_access_payment" or "action_must_be_payed" or "not_enough_purchased_services"
        || value is "view_limit_exceeded" or "daily_resume_search_limit_without_services_exceeded" or "no_available_service" or "quota_exceeded"
            or "not_enough_purchased_services" or "action_must_be_payed";

    /// <summary>Суточный предел запросов OneBase к HH (HH числовых лимитов не публикует; не делаем лишних запросов).</summary>
    public const int DailyCap = 2000;

    /// <summary>Пауза между запросами к HH.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(250);
}

/// <summary>Запрос к HH заблокирован Quota Guard до отправки (в HH ничего не ушло).</summary>
public sealed class HhBlockedException(string operation, string reason) : Exception(reason)
{
    public string Operation { get; } = operation;
}

/// <summary>
/// Кто вызывает HH: auto — синхронизация по расписанию, manual — действие HR (actorId). Задаётся на время вызова (AsyncLocal), Quota Guard
/// пишет его в журнал и проверяет по каталогу.
/// </summary>
public static class HhCallScope
{
    private static readonly AsyncLocal<(bool Manual, Guid? ActorId)?> Current = new();

    public static bool Manual => Current.Value?.Manual ?? false;

    public static Guid? ActorId => Current.Value?.ActorId;

    public static IDisposable Begin(bool manual, Guid? actorId)
    {
        var previous = Current.Value;
        Current.Value = (manual, actorId);
        return new Restore(() => Current.Value = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

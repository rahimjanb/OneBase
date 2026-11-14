using OneBase.Domain.Common;
using OneBase.Domain.Identity;

namespace OneBase.Domain.Recruitment;

public enum HiringRequestStatus
{
    /// <summary>Подана руководителем отдела, ждёт решения HR.</summary>
    Submitted,

    /// <summary>Одобрена: по ней открыта вакансия.</summary>
    Approved,

    Rejected,

    /// <summary>Отозвана автором до решения.</summary>
    Cancelled,
}

/// <summary>Заявка на сотрудника: отдел просит человека на должность; HR одобряет (открывается вакансия) или отклоняет с причиной.</summary>
public class HiringRequest : Entity
{
    /// <summary>Номер для людей (№ 12) — по порядку, выдаёт база.</summary>
    public int Number { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public required string Position { get; set; }
    public int Headcount { get; set; } = 1;

    /// <summary>Зачем: новая ставка, замена уволившегося, рост объёма…</summary>
    public string? Reason { get; set; }

    public string? Requirements { get; set; }
    public decimal? SalaryFrom { get; set; }
    public decimal? SalaryTo { get; set; }
    public DateOnly? DesiredStart { get; set; }

    public HiringRequestStatus Status { get; set; } = HiringRequestStatus.Submitted;

    public Guid CreatedById { get; set; }
    public Guid? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }

    /// <summary>Вакансия, открытая по заявке.</summary>
    public Guid? VacancyId { get; set; }
}

public enum VacancyStatus
{
    Draft,
    Open,

    /// <summary>Подбор приостановлен: новые отклики не ждём, кандидаты в работе остаются.</summary>
    Paused,

    Closed,
}

/// <summary>Вакансия: должность, отдел, требования, условия, зарплатная вилка, рекрутер. Code (V12) — короткий код для постов и сообщений.</summary>
public class Vacancy : Entity
{
    public int Number { get; set; }

    public required string Title { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid? HiringRequestId { get; set; }

    public string? Description { get; set; }
    public string? Requirements { get; set; }

    /// <summary>Условия работы: график, место, оформление, что даёт компания.</summary>
    public string? Conditions { get; set; }

    public decimal? SalaryFrom { get; set; }
    public decimal? SalaryTo { get; set; }
    public string? City { get; set; }

    /// <summary>full, part, shift, remote, temporary.</summary>
    public string? EmploymentType { get; set; }

    public int Headcount { get; set; } = 1;

    public VacancyStatus Status { get; set; } = VacancyStatus.Draft;

    /// <summary>Рекрутер HR, который ведёт вакансию.</summary>
    public Guid? RecruiterId { get; set; }

    public Guid CreatedById { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Вакансия на HH.uz, с которой берутся отклики (ID вакансии HH).</summary>
    public string? HhVacancyId { get; set; }

    /// <summary>Критерии оценки кандидатов AI — по одному в строке; пусто — стандартные (опыт, навыки, образование, достижения, зарплата, требования).</summary>
    public string? Criteria { get; set; }

    /// <summary>AI-рекрутер ведёт переписку с кандидатами этой вакансии (если его агент включён в «Настройки → ИИ-агенты»).</summary>
    public bool AiRecruiter { get; set; }

    /// <summary>Какой ИИ-агент (рекрутер) ведёт вакансию; null — рекрутер HR по умолчанию.</summary>
    public Guid? AgentId { get; set; }

    /// <summary>Вопросы кандидатам этой вакансии (по строке) — рекрутер задаёт их вместе с общими вопросами агента.</summary>
    public string? ScreeningQuestions { get; set; }

    public string Code => $"V{Number}";
}

/// <summary>
/// Кандидат — один на человека, из каких бы источников он ни пришёл (вручную, HH, Telegram). Персональные данные хранятся, пока идёт
/// подбор, и ещё срок хранения после того, как все его отклики закрыты (RetentionFrom); затем удаляются целиком вместе с резюме.
/// </summary>
public class Candidate : Entity
{
    public required string FullName { get; set; }
    public string? City { get; set; }

    /// <summary>Язык общения (предпочтительный): ru, uz.</summary>
    public string? Language { get; set; }

    /// <summary>Графика предпочтительного языка: Latn, Cyrl (у русского — Cyrl). Узбекский без графики — латиница.</summary>
    public string? PreferredScript { get; set; }

    /// <summary>Что показало последнее сообщение кандидата: ru-Cyrl, uz-Latn, uz-Cyrl (null — не понять: «ok», «1»).</summary>
    public string? LastDetectedLanguage { get; set; }

    /// <summary>Уверенность последнего определения, 0–1.</summary>
    public decimal? LanguageConfidence { get; set; }

    /// <summary>Кто выбрал язык: auto (по переписке), candidate (попросил сам), hr (выбрал сотрудник). Выбранный человеком меняет только новая просьба.</summary>
    public string? LanguageSource { get; set; }

    public string? Notes { get; set; }

    /// <summary>Последнее действие с кандидатом или от него (правка, отклик, этап, резюме, сообщение).</summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>
    /// С какого момента идёт срок хранения: закрытие последнего отклика или последняя активность (что позже). Null — идёт подбор (есть
    /// открытый отклик), удалять нельзя. Удаление — когда прошло RetentionDays из настроек: смена срока сразу действует на всех.
    /// </summary>
    public DateTimeOffset? RetentionFrom { get; set; }

    public Guid CreatedById { get; set; }

    // ---------- Сводка для AI-скрининга: из последнего разобранного резюме (обновляется при каждом разборе; прежнее не затирается пустым) ----------

    /// <summary>Желаемая должность (из резюме).</summary>
    public string? DesiredPosition { get; set; }

    /// <summary>Общий стаж, месяцев (сказано в резюме или выведено по датам мест работы).</summary>
    public int? ExperienceMonths { get; set; }

    /// <summary>Последнее место работы: «должность — компания».</summary>
    public string? LastJob { get; set; }

    /// <summary>Зарплатные ожидания, как в резюме.</summary>
    public string? SalaryExpectation { get; set; }

    /// <summary>Образование, кратко: «учебное заведение, степень, год».</summary>
    public string? Education { get; set; }

    /// <summary>Когда сводка заполнена из резюме.</summary>
    public DateTimeOffset? ProfileAt { get; set; }

    public List<CandidateContact> Contacts { get; set; } = [];
    public List<CandidateSource> Sources { get; set; } = [];
    public List<CandidateResume> Resumes { get; set; } = [];
    public List<CandidateApplication> Applications { get; set; } = [];
    public List<CandidateConsent> Consents { get; set; } = [];
    public List<CandidateSkill> Skills { get; set; } = [];
}

/// <summary>Профессиональный навык кандидата из разобранного резюме (для фильтра «навыки»); Normalized — нижний регистр без лишних пробелов.</summary>
public class CandidateSkill
{
    public long Id { get; set; }
    public Guid CandidateId { get; set; }
    public required string Name { get; set; }
    public required string Normalized { get; set; }
}

public enum CandidateContactKind
{
    Phone,
    Email,

    /// <summary>Telegram user ID (число) — надёжный идентификатор: username меняется.</summary>
    Telegram,

    TelegramUsername,
}

/// <summary>
/// Контакт кандидата, нормализованный (телефон +998…, e-mail в нижнем регистре, username без @). Verified — подтверждён источником
/// (написал с этого Telegram, указал в отклике HH); по подтверждённым контактам кандидаты объединяются, по неподтверждённым — только
/// подозрение на дубль, решает HR. По имени не объединяются никогда.
/// </summary>
public class CandidateContact : Entity
{
    public Guid CandidateId { get; set; }
    public CandidateContactKind Kind { get; set; }
    public required string Value { get; set; }
    public bool Verified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>Откуда контакт: manual, hh, telegram.</summary>
    public required string Source { get; set; }
}

/// <summary>Откуда кандидат: manual, hh, telegram, referral, other; ExternalId — его ID в источнике (уникально по источнику).</summary>
public class CandidateSource : Entity
{
    public Guid CandidateId { get; set; }
    public required string Source { get; set; }
    public string? ExternalId { get; set; }
    public string? Note { get; set; }
}

/// <summary>Что с документом: получен (ждёт скачивания из Telegram), сохранён, обрабатывается, готов, ошибка, отклонён (содержимое не того типа).</summary>
public enum DocumentStatus
{
    Received,
    Stored,
    Processing,
    Done,
    Failed,
    Rejected,
}

/// <summary>Шаг обработки: не нужен, ждёт, готово, ошибка, пропущен (выключено в настройках или AI не настроен).</summary>
public enum DocumentStepStatus
{
    NotNeeded,
    Pending,
    Done,
    Failed,
    Skipped,
}

/// <summary>
/// Документ кандидата (резюме, диплом, сертификат): оригинал — в MinIO (<c>recruitment/resumes/{кандидат}/{документ}</c>), в базе — только
/// сведения о нём, текст и разбор. Оригинал сохраняется до любой обработки и не перезаписывается: каждая версия — отдельный документ.
/// Из Telegram документ сначала «получен» (Status = Received, есть ссылка на сообщение), затем фоновое задание скачивает и сохраняет его.
/// </summary>
public class CandidateResume : Entity
{
    public Guid CandidateId { get; set; }

    /// <summary>Отклик, по которому документ пришёл (открытый в момент получения).</summary>
    public Guid? ApplicationId { get; set; }

    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string ObjectKey { get; set; }

    /// <summary>SHA-256 оригинала; null — файл ещё не скачан из Telegram.</summary>
    public string? Sha256 { get; set; }

    public string? Text { get; set; }

    /// <summary>Почему текст не извлечён (формат, скан без текста…); файл при этом есть.</summary>
    public string? TextError { get; set; }

    /// <summary>Текст распознал AI (фото или скан), а не взят из текстового слоя файла.</summary>
    public bool TextByAi { get; set; }

    /// <summary>Что за документ по мнению AI: resume, certificate, id_document (текст не сохраняется), other; null — не определялось.</summary>
    public string? DocumentType { get; set; }

    /// <summary>manual, hh, telegram.</summary>
    public required string Source { get; set; }

    public Guid? UploadedById { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Stored;

    /// <summary>Распознавание текста (текстовый слой или OCR моделью).</summary>
    public DocumentStepStatus OcrStatus { get; set; } = DocumentStepStatus.Pending;

    /// <summary>Разбор резюме AI (поля, сводка) и оценка под вакансию.</summary>
    public DocumentStepStatus AnalysisStatus { get; set; } = DocumentStepStatus.Pending;

    /// <summary>Тип по содержимому (сигнатуре): pdf, docx, doc, rtf, odt, txt, jpeg, png, webp; null — не проверялся или неизвестен.</summary>
    public string? DetectedType { get; set; }

    public int? PageCount { get; set; }

    /// <summary>Почему обработка остановилась (файл не скачался, содержимое не того типа, повреждён) — для HR; оригинал при этом сохранён.</summary>
    public string? ProcessingError { get; set; }

    /// <summary>Откуда скачать из Telegram: аккаунт, чат, сообщение.</summary>
    public Guid? TelegramAccountId { get; set; }

    public long? TelegramPeerId { get; set; }
    public int? TelegramMessageId { get; set; }

    /// <summary>Разбор резюме (JSON): поля с доказательством — цитатой из текста, «сказано в резюме» или «выведено», проверено ли.</summary>
    public string? Profile { get; set; }

    /// <summary>Краткая сводка резюме от AI.</summary>
    public string? Summary { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}

public enum ApplicationStage
{
    New,
    Screening,
    Interview,
    Offer,
    Hired,
    Rejected,
    Withdrawn,
}

/// <summary>Кандидат на вакансию (один раз на вакансию): этап найма и его история.</summary>
public class CandidateApplication : Entity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public Guid VacancyId { get; set; }
    public Vacancy? Vacancy { get; set; }

    public ApplicationStage Stage { get; set; } = ApplicationStage.New;
    public required string Source { get; set; }
    public DateTimeOffset StageChangedAt { get; set; }

    /// <summary>Почему отказ (для истории и аналитики; кандидату не показывается).</summary>
    public string? RejectReason { get; set; }

    /// <summary>Когда отклик закрыт (принят, отказ, отозвал); null — подбор идёт.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    public Guid? CreatedById { get; set; }

    /// <summary>Ответы кандидата на вопросы отбора (JSON-объект «вопрос → ответ»), которые узнал AI-рекрутер в переписке.</summary>
    public string? ScreeningAnswers { get; set; }

    /// <summary>
    /// Отметка HR в AI-скрининге: подходит, рекомендован, в резерве. Это решение человека (AI её не ставит); любая смена этапа её снимает —
    /// показывается последнее действие. Статус скрининга — из этапа и отметки (<c>ScreeningRules.Status</c>).
    /// </summary>
    public ApplicationMark Mark { get; set; } = ApplicationMark.None;
}

/// <summary>Отметка HR в AI-скрининге.</summary>
public enum ApplicationMark
{
    None,
    Suitable,
    Recommended,
    Reserve,
}

/// <summary>Переход этапа отклика: с какого на какой, кто и почему.</summary>
public class ApplicationEvent
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public ApplicationStage? FromStage { get; set; }
    public ApplicationStage ToStage { get; set; }

    /// <summary>Отметка HR, поставленная этим событием (подходит, рекомендован, в резерве, снята — None); null — событие этапа.</summary>
    public ApplicationMark? Mark { get; set; }

    public string? Comment { get; set; }
    public Guid? ActorId { get; set; }
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// Согласие кандидата на обработку персональных данных для подбора: канал, версия текста согласия, когда дано и когда отозвано.
/// Отзыв — данные кандидата удаляются (с ближайшим кругом службы хранения).
/// </summary>
public class CandidateConsent : Entity
{
    public Guid CandidateId { get; set; }

    /// <summary>recruitment — подбор на вакансии компании.</summary>
    public required string Purpose { get; set; }

    /// <summary>written, telegram, hh, email, verbal.</summary>
    public required string Channel { get; set; }

    public int TextVersion { get; set; }
    public DateTimeOffset GivenAt { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public Guid? RecordedById { get; set; }
}

public enum CandidateDuplicateStatus
{
    Open,
    Merged,
    Dismissed,
}

/// <summary>Подозрение на дубль: у двух кандидатов совпал неподтверждённый контакт. Объединяет или отклоняет HR.</summary>
public class CandidateDuplicate : Entity
{
    public Guid CandidateId { get; set; }
    public Guid OtherCandidateId { get; set; }
    public CandidateContactKind Kind { get; set; }
    public required string Value { get; set; }
    public CandidateDuplicateStatus Status { get; set; } = CandidateDuplicateStatus.Open;
    public Guid? ResolvedById { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>Настройки подбора (одна строка): срок хранения данных кандидата после закрытия подбора.</summary>
public class RecruitmentSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Дней хранения после закрытия последнего отклика (решение 08.11.2026 — неделя).</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Общий выключатель AI-рекрутера: пока выключен, AI никому не пишет (по умолчанию — выключен).</summary>
    public bool AiEnabled { get; set; }

    /// <summary>Как AI-рекрутер представляется («рекрутер компании …»); за человека себя не выдаёт.</summary>
    public string? AssistantName { get; set; }

    /// <summary>Напомнить кандидату, который не ответил, через столько часов (одно напоминание); 0 — не напоминать.</summary>
    public int FollowUpHours { get; set; } = 24;

    /// <summary>Рабочие часы собеседований (время Ташкента), длина слота и дни недели (1 — пн … 7 — вс).</summary>
    public TimeOnly InterviewDayStart { get; set; } = new(10, 0);

    public TimeOnly InterviewDayEnd { get; set; } = new(18, 0);
    public int InterviewSlotMinutes { get; set; } = 45;
    public string InterviewDays { get; set; } = "1,2,3,4,5,6";

    /// <summary>Оценивать резюме AI автоматически при появлении (платные вызовы) — по умолчанию нет, только по кнопке.</summary>
    public bool AutoScore { get; set; }

    /// <summary>Фото резюме и PDF-сканы без текста распознаёт AI (платный вызов на каждый такой файл). По умолчанию — да.</summary>
    public bool ReadScans { get; set; } = true;

    /// <summary>Разбирать резюме AI (поля, сводка) и оценивать под вакансию — платный вызов на резюме. Выключено — только контакты из текста. По умолчанию — да.</summary>
    public bool AnalyzeResumes { get; set; } = true;

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>Текст согласия на обработку данных — версиями: у согласия кандидата — версия, на которую он согласился.</summary>
public class RecruitmentConsentText
{
    public int Version { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
}

public enum PublicationStatus
{
    Scheduled,
    Sending,
    Sent,
    Failed,
    Cancelled,
}

/// <summary>Публикация вакансии в группу или канал через аккаунт Telegram HR: текст, когда, статус, ссылка на сообщение.</summary>
public class VacancyPublication : Entity
{
    public Guid VacancyId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Чат из telegram.Chats.</summary>
    public Guid ChatId { get; set; }

    public long PeerId { get; set; }
    public required string ChatTitle { get; set; }
    public string? ChatUsername { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public PublicationStatus Status { get; set; } = PublicationStatus.Scheduled;
    public Guid? OutgoingId { get; set; }
    public int? MessageId { get; set; }

    /// <summary>Ссылка на сообщение (t.me/канал/номер) — если чат её даёт.</summary>
    public string? Link { get; set; }

    public string? Error { get; set; }
    public Guid CreatedById { get; set; }
}

/// <summary>
/// Источник поиска кандидатов в Telegram — группа или канал, где аккаунт HR состоит: категория, следить ли за постами «ищу работу»
/// (лиды), публиковать ли туда вакансии. Участники группы не выгружаются; незнакомым OneBase сам не пишет.
/// </summary>
public class RecruitmentSource : Entity
{
    public Guid AccountId { get; set; }
    public Guid ChatId { get; set; }
    public long PeerId { get; set; }
    public required string Title { get; set; }

    /// <summary>jobs (вакансии и поиск работы), professional (профессиональное сообщество), channel (профильный канал), other.</summary>
    public required string Category { get; set; }

    public bool CollectLeads { get; set; } = true;
    public string? Note { get; set; }
    public Guid CreatedById { get; set; }
}

public enum LeadStatus
{
    New,

    /// <summary>HR решил связаться — кандидат заведён.</summary>
    Converted,

    Ignored,
}

/// <summary>
/// Пост в группе-источнике, похожий на поиск работы или резюме: кто, где, отрывок (до 500 символов), совпавшие слова. Писать человеку решает
/// HR; лиды без решения удаляются по сроку хранения.
/// </summary>
public class TelegramLead : Entity
{
    public Guid AccountId { get; set; }
    public long ChatPeerId { get; set; }
    public required string ChatTitle { get; set; }
    public int MessageId { get; set; }
    public long? FromUserId { get; set; }
    public string? FromUsername { get; set; }
    public string? FromName { get; set; }
    public required string Excerpt { get; set; }
    public required string Keywords { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public Guid? CandidateId { get; set; }
    public Guid? DecidedById { get; set; }
}

// ---------- HH.uz (этап 4) ----------

/// <summary>
/// Подключение HH.uz одного аккаунта работодателя (одна строка): приложение (client_id, client_secret — зашифрован), redirect_uri, почта
/// разработчика для HH-User-Agent, токены OAuth (зашифрованы), работодатель, итог последней синхронизации.
/// </summary>
public class HhConnection
{
    public int Id { get; set; } = 1;
    public string? ClientId { get; set; }
    public string? ProtectedClientSecret { get; set; }
    public string? RedirectUri { get; set; }

    /// <summary>Почта разработчика — HH требует её в заголовке HH-User-Agent («OneBase/1.0 (почта)»).</summary>
    public string? ContactEmail { get; set; }

    public bool Enabled { get; set; } = true;
    public string? ProtectedAccessToken { get; set; }
    public string? ProtectedRefreshToken { get; set; }
    public DateTimeOffset? TokenExpiresAt { get; set; }

    /// <summary>Хэш state текущего входа (защита от подделки ответа OAuth) и до когда он действует.</summary>
    public string? StateHash { get; set; }

    public DateTimeOffset? StateExpiresAt { get; set; }
    public string? EmployerId { get; set; }
    public string? EmployerName { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public Guid? ConnectedById { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public bool? LastSyncOk { get; set; }
    public string? LastSyncMessage { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Предохранитель расхода: HH ответил, что операция требует платной услуги или лимит исчерпан (api_access_payment, view_limit_exceeded…).
    /// Пока стоит — автоматическая синхронизация не ходит в HH; снимает HR («Проверить снова»).
    /// </summary>
    public DateTimeOffset? GuardTrippedAt { get; set; }

    public string? GuardReason { get; set; }

    /// <summary>
    /// Резюме, полученные через API HH, можно передавать AI (разбор и оценка). По умолчанию — нет: соглашение разработчика HH (п. 4.6) запрещает
    /// передавать полученные через сервис данные третьим сервисам; включает HR после подтверждения юриста. Резюме, загруженные HR файлом, — не про это.
    /// </summary>
    public bool AiOnHhData { get; set; }
}

/// <summary>
/// Журнал запросов к API HH (HeadHunterApiAuditLogs): каждая попытка — выполнена или заблокирована Quota Guard до отправки, операция и её
/// стоимость по каталогу, кто запустил (auto — синхронизация, manual — HR), ответ HH. Тело запросов и ответов не хранится (персональные данные).
/// </summary>
public class HhApiCall
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }

    /// <summary>Операция каталога (<c>HhOperations</c>): token, me, vacancies.active, negotiations.list, resume.view…</summary>
    public required string Operation { get; set; }

    public required string Method { get; set; }

    /// <summary>Путь без параметров запроса (ID в пути — вакансии, отклика, резюме HH).</summary>
    public required string Path { get; set; }

    /// <summary>free, paid, unknown — по каталогу операций.</summary>
    public required string Cost { get; set; }

    /// <summary>auto, manual.</summary>
    public required string Trigger { get; set; }

    /// <summary>false — заблокирован до отправки (в HH не ушёл).</summary>
    public bool Allowed { get; set; }

    public string? Reason { get; set; }
    public int? Status { get; set; }
    public string? ErrorType { get; set; }
    public int DurationMs { get; set; }
    public Guid? ActorId { get; set; }
}

/// <summary>Отклик HH на вакансию: связь с откликом OneBase, состояние на HH, доступные действия (их URL отдаёт HH), резюме получено ли.</summary>
public class HhNegotiation : Entity
{
    public required string NegotiationId { get; set; }
    public required string HhVacancyId { get; set; }
    public Guid VacancyId { get; set; }
    public Guid? CandidateId { get; set; }
    public Guid? ApplicationId { get; set; }
    public required string ResumeId { get; set; }
    public string? ResumeUrl { get; set; }
    public string? State { get; set; }

    /// <summary>Действия по отклику из ответа HH (JSON: id, name, method, url) — выполняются только они, адреса не придумываются.</summary>
    public string? ActionsJson { get; set; }

    public DateTimeOffset? HhUpdatedAt { get; set; }
    public DateTimeOffset? ResumeFetchedAt { get; set; }
    public string? LastError { get; set; }
}

// ---------- AI-рекрутер и оценка (этап 5) ----------

/// <summary>
/// Анализ кандидата под вакансию AI (одна строка — одна версия анализа; история рейтинга — строки по времени). Совет HR, а не решение.
/// Version 1 — прежняя оценка: критерии вакансии, балл 0–10, итог — среднее × 10. Version 2 — AI-скрининг: шесть критериев с весами
/// (опыт 25, навыки 25, требования 20, достижения 15, образование 10, преимущества 5), у каждого — баллы, объяснение и цитаты из резюме
/// (проверяются на сервере: без подтверждённой цитаты балл не выше половины), итог — сумма баллов, считается на сервере.
/// </summary>
public class CandidateScore : Entity
{
    public Guid ApplicationId { get; set; }
    public Guid CandidateId { get; set; }
    public int Overall { get; set; }
    public required string Summary { get; set; }

    /// <summary>JSON: [{ name, score (0–10), explanation, evidence, verified }]; в версии 2 ещё code, points, max, status, quotes, capped.</summary>
    public required string CriteriaJson { get; set; }

    public string? Risks { get; set; }
    public string? Model { get; set; }
    public Guid? RequestedById { get; set; }

    /// <summary>Версия схемы анализа: 1 — прежняя оценка, 2 — AI-скрининг.</summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Версия 2 (JSON): почему подходит, сильные и слабые стороны, недостающие требования, рекомендация AI, вопросы для собеседования,
    /// покрытие фактами, какое резюме анализировалось.
    /// </summary>
    public string? DetailsJson { get; set; }

    /// <summary>Уверенность модели 0–1 — отдельно от оценки соответствия, на итог не влияет.</summary>
    public decimal? Confidence { get; set; }
}

/// <summary>
/// Сравнение до пяти кандидатов одной вакансии: таблица строится на сервере из их анализов (факты, без вызова AI), AI даёт аргументированную
/// рекомендацию. Хранится, чтобы HR вернулся к нему; удаляется вместе с вакансией не будет — только по сроку хранения кандидатов.
/// </summary>
public class CandidateComparison : Entity
{
    public Guid VacancyId { get; set; }

    /// <summary>JSON-массив ID откликов в порядке выбора.</summary>
    public required string ApplicationIds { get; set; }

    /// <summary>JSON: таблица и рекомендация (<c>ComparisonView</c>).</summary>
    public required string ResultJson { get; set; }

    public string? Model { get; set; }
    public Guid? CreatedById { get; set; }
}

// ---------- Собеседования (этап 6) ----------

public class VacancyInterviewer
{
    public Guid VacancyId { get; set; }
    public Guid UserId { get; set; }
}

/// <summary>
/// Статус собеседования (в задании: PROPOSED, AWAITING_CANDIDATE_CONFIRMATION, CONFIRMED, RESCHEDULED, CANCELLED, COMPLETED, NO_SHOW).
/// Подтверждённое и перенесённое — действующие: у них событие календаря.
/// </summary>
public enum InterviewStatus
{
    /// <summary>Кандидату предложены варианты времени, ждём выбора. Это ещё не собеседование: время не занято.</summary>
    Proposed,

    Confirmed,
    Completed,
    Cancelled,
    NoShow,

    /// <summary>Время выбрано (HR предложил одно время), ждём «да» кандидата; время удерживается до HoldUntil, события календаря ещё нет.</summary>
    Awaiting,

    /// <summary>Перенесено на новое время (событие календаря перенесено); подтвердил ли кандидат новое время — CandidateConfirmation.</summary>
    Rescheduled,
}

/// <summary>Подтверждение кандидатом: ждём, подтвердил (сам в Telegram), отказался, подтвердил по словам HR (телефон, лично).</summary>
public enum CandidateConfirmation
{
    Pending,
    Confirmed,
    Declined,
    ByHr,
}

/// <summary>
/// Собеседование — единственная запись о нём: отклик, кандидат, вакансия, ответственный интервьюер и участники, время и часовой пояс, формат,
/// место или ссылка, статус, подтверждение кандидатом. Событие календаря OneBase — его отражение (CalendarEventId, одно на собеседование):
/// создаётся, переносится и удаляется только вместе с собеседованием.
/// </summary>
public class Interview : Entity
{
    public Guid ApplicationId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid VacancyId { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>office, online, phone.</summary>
    public required string Format { get; set; }

    public string? Location { get; set; }
    public InterviewStatus Status { get; set; } = InterviewStatus.Proposed;
    public string? ProposedSlotsJson { get; set; }
    public Guid? CalendarEventId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? RemindedAt { get; set; }
    public Guid? CreatedById { get; set; }

    /// <summary>Ответственный интервьюер.</summary>
    public Guid? InterviewerId { get; set; }

    /// <summary>Участники (JSON-массив ID пользователей, вместе с интервьюером) — их занятость проверяется, они — участники события.</summary>
    public string? Participants { get; set; }

    /// <summary>Часовой пояс собеседования (IANA), по умолчанию Asia/Tashkent.</summary>
    public string TimeZone { get; set; } = "Asia/Tashkent";

    public string? MeetingUrl { get; set; }
    public CandidateConfirmation CandidateConfirmation { get; set; } = CandidateConfirmation.Pending;

    /// <summary>Время удерживается за кандидатом до этого момента (Awaiting); дальше — свободно.</summary>
    public DateTimeOffset? HoldUntil { get; set; }

    /// <summary>Прежнее время (при переносе).</summary>
    public DateTimeOffset? PreviousStartsAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>Кто отменил: candidate, hr, system.</summary>
    public string? CancelledBy { get; set; }

    public string? CancelReason { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Версия строки (xmin): два одновременных подтверждения не создадут двух событий календаря.</summary>
    public uint Version { get; set; }
}

/// <summary>
/// Настройки собеседования вакансии (одна строка на вакансию; пусто — общие настройки подбора): ответственный интервьюер (участники —
/// интервьюеры вакансии), длительность, дни, часы, перерывы, минимальный срок записи, горизонт, формат, адрес, онлайн-ссылка, часовой пояс,
/// порог оценки для приглашения, инструкция кандидату.
/// </summary>
public class VacancyInterviewSettings
{
    public Guid VacancyId { get; set; }
    public Guid? InterviewerId { get; set; }
    public int? DurationMinutes { get; set; }

    /// <summary>Дни недели: 1 — пн … 7 — вс, через запятую.</summary>
    public string? Days { get; set; }

    public TimeOnly? DayStart { get; set; }
    public TimeOnly? DayEnd { get; set; }

    /// <summary>Перерывы: «13:00-14:00», несколько — через запятую.</summary>
    public string? Breaks { get; set; }

    public int? MinNoticeHours { get; set; }
    public int? MaxAdvanceDays { get; set; }

    /// <summary>office, online, phone.</summary>
    public string? Format { get; set; }

    public string? Address { get; set; }
    public string? MeetingUrl { get; set; }
    public string? TimeZone { get; set; }

    /// <summary>Приглашать, только если оценка AI под вакансию не ниже (0–100); null — решает разговор (вопросы отбора).</summary>
    public int? InviteMinScore { get; set; }

    /// <summary>Что взять и как пройти — кандидату и в событие календаря.</summary>
    public string? Instructions { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

public enum InterviewResult
{
    Passed,
    Rejected,
    AdditionalInterview,
    WaitingDecision,
}

/// <summary>
/// Итоги собеседования от сотрудника (одна запись на сотрудника и собеседование, правится): результат, оценки 1–5 (техническая, общение,
/// профессиональные качества), сильные и слабые стороны, комментарий, рекомендация, следующий этап. Решение о найме — за HR и руководством.
/// </summary>
public class InterviewFeedback : Entity
{
    public Guid InterviewId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid AuthorId { get; set; }
    public InterviewResult Result { get; set; }
    public int? Technical { get; set; }
    public int? Communication { get; set; }
    public int? Professional { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public string? Comments { get; set; }
    public string? Recommendation { get; set; }
    public ApplicationStage? NextStage { get; set; }
}

// ---------- Центр исключений (этап 7) ----------

public enum RecruitmentExceptionStatus
{
    Open,
    Resolved,
}

/// <summary>
/// Исключение для HR: AI или служба остановилась на конкретном диалоге, кандидате или вакансии и ждёт человека (просьба поговорить с
/// человеком, торг о зарплате, непонятный ответ, ошибка, проблема Telegram или HH, конфликт расписания, дубль). Останавливается только этот
/// диалог — остальные работают.
/// </summary>
public class RecruitmentException : Entity
{
    /// <summary>human_requested, salary, unclear, conditions, technical, telegram, hh, schedule, duplicate, send_failed.</summary>
    public required string Kind { get; set; }

    public required string Message { get; set; }
    public Guid? CandidateId { get; set; }
    public Guid? VacancyId { get; set; }
    public Guid? ConversationId { get; set; }

    /// <summary>Ключ, чтобы одна и та же проблема не плодила строки (например, «telegram:аккаунт»).</summary>
    public string? DedupKey { get; set; }

    public RecruitmentExceptionStatus Status { get; set; } = RecruitmentExceptionStatus.Open;
    public Guid? ResolvedById { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}

/// <summary>Сотрудник HR прочитал диалог Telegram до этого момента: входящие позже — непрочитанные (у каждого сотрудника свои).</summary>
public class DialogRead
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ReadAt { get; set; }
}

// ---------- Фоновые задания подбора ----------

public enum RecruitmentJobStatus
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <summary>
/// Фоновое задание подбора в очереди PostgreSQL (брокера в OneBase нет; как очередь исходящих Telegram): очередь (hr.resume.download,
/// hr.resume.ocr, hr.resume.analysis, hr.candidate.scoring, hr.interview.scheduling, hr.interview.notifications), ключ (одно и то же
/// задание не ставится дважды, пока не выполнено), над чем (RelatedId). Захват — условным UPDATE с арендой: упал сервер посреди задания —
/// аренда истекла, задание берётся снова. Ошибка — повтор с паузой, после MaxAttempts — Failed и исключение для HR.
/// </summary>
public class RecruitmentJob
{
    public long Id { get; set; }
    public required string Queue { get; set; }
    public required string Key { get; set; }
    public Guid RelatedId { get; set; }

    /// <summary>Небольшие параметры задания (JSON).</summary>
    public string? Payload { get; set; }

    public RecruitmentJobStatus Status { get; set; } = RecruitmentJobStatus.Pending;
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTimeOffset RunAfter { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

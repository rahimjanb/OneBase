using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment;

/// <summary>
/// Кто что может в подборе (проверяется в сервисе, интерфейс только показывает): право «Подбор: просмотр» — вакансии, кандидаты и все
/// заявки; «Подбор: управление» — всё менять и решать по заявкам; руководитель отдела (work.DepartmentHeads) — подавать и видеть заявки
/// своего отдела, без доступа к кандидатам.
/// </summary>
public sealed record RecruitmentContext(Guid UserId, bool CanRead, bool CanManage, IReadOnlyList<Guid> HeadOf)
{
    public bool SeesCandidates => CanRead || CanManage;

    public bool CanRequest => CanManage || HeadOf.Count > 0;

    public bool SeesRequests => SeesCandidates || HeadOf.Count > 0;
}

public sealed record RecruitmentOption(Guid Id, string Name, string? Code = null);

public sealed record RecruitmentContextView(
    bool CanRead,
    bool CanManage,
    bool CanRequest,
    IReadOnlyList<RecruitmentOption> RequestDepartments,
    IReadOnlyList<RecruitmentOption> Departments,
    IReadOnlyList<RecruitmentOption> Recruiters,
    int RetentionDays);

public sealed record RecruitmentUpload(string FileName, string? ContentType, long SizeBytes, Stream Content);

public sealed record RecruitmentDownload(string FileName, string ContentType, string ObjectKey);

// ---------- Заявки на сотрудника ----------

public sealed record HiringRequestInput(
    Guid DepartmentId,
    string Position,
    int Headcount,
    string? Reason,
    string? Requirements,
    decimal? SalaryFrom,
    decimal? SalaryTo,
    DateOnly? DesiredStart);

public sealed record HiringRequestView(
    Guid Id,
    int Number,
    Guid DepartmentId,
    string Department,
    string Position,
    int Headcount,
    string? Reason,
    string? Requirements,
    decimal? SalaryFrom,
    decimal? SalaryTo,
    DateOnly? DesiredStart,
    string Status,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionComment,
    Guid? VacancyId,
    string? VacancyCode,
    bool CanCancel,
    bool CanDecide);

public sealed record DecisionInput(string? Comment);

// ---------- Вакансии ----------

public sealed record VacancyInput(
    string Title,
    Guid DepartmentId,
    string? Description,
    string? Requirements,
    string? Conditions,
    decimal? SalaryFrom,
    decimal? SalaryTo,
    string? City,
    string? EmploymentType,
    int Headcount,
    Guid? RecruiterId);

public sealed record VacancyStatusInput(VacancyStatus Status);

public sealed record VacancyRow(
    Guid Id,
    string Code,
    string Title,
    string Department,
    string Status,
    int Headcount,
    decimal? SalaryFrom,
    decimal? SalaryTo,
    string? City,
    string? Recruiter,
    int Active,
    int Hired,
    DateTimeOffset CreatedAt,
    DateTimeOffset? OpenedAt,
    Guid DepartmentId,
    DateTimeOffset UpdatedAt,
    bool OnHh,
    bool InTelegram);

/// <summary>
/// Подписи кнопок разделов подбора — один лёгкий запрос на все кнопки: открытые вакансии, кандидаты в работе (у кого есть незакрытый отклик),
/// диалоги Telegram с непрочитанными у этого сотрудника, действующие публикации открытых вакансий, собеседования сегодня (Ташкент),
/// открытые исключения, подключён ли HH.
/// </summary>
public sealed record RecruitmentNavView(int OpenVacancies, int CandidatesInWork, int TelegramNew, int ActivePublications, int InterviewsToday, int OpenExceptions,
    bool HhConnected);

public sealed record VacancyApplicationRow(Guid ApplicationId, Guid CandidateId, string CandidateName, string Stage, string Source,
    DateTimeOffset StageChangedAt, string? Phone);

public sealed record VacancyView(
    Guid Id,
    string Code,
    string Title,
    Guid DepartmentId,
    string Department,
    string? Description,
    string? Requirements,
    string? Conditions,
    decimal? SalaryFrom,
    decimal? SalaryTo,
    string? City,
    string? EmploymentType,
    int Headcount,
    string Status,
    Guid? RecruiterId,
    string? Recruiter,
    Guid? HiringRequestId,
    int? HiringRequestNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyDictionary<string, int> Stages,
    IReadOnlyList<VacancyApplicationRow> Applications,
    bool AiRecruiter = false,
    string? Criteria = null,
    string? HhVacancyId = null,
    Guid? AgentId = null,
    string? ScreeningQuestions = null);

// ---------- Кандидаты ----------

public sealed record ContactInput(CandidateContactKind Kind, string Value);

/// <summary>Новый кандидат вручную: контакты — неподтверждённые (их ввёл сотрудник), источник, вакансия и согласие — по желанию.</summary>
public sealed record CandidateInput(
    string FullName,
    string? City,
    string? Language,
    string? Notes,
    IReadOnlyList<ContactInput>? Contacts,
    string? Source,
    string? SourceNote,
    Guid? VacancyId,
    string? ConsentChannel);

/// <summary>LanguageCode — язык переписки (ru-Cyrl, uz-Latn, uz-Cyrl или «auto» — по переписке); null — по-старому из Language (ru/uz).</summary>
public sealed record CandidateUpdateInput(string FullName, string? City, string? Language, string? Notes, string? LanguageCode = null);

/// <summary>Контакт при приёме кандидата: Verified — подтверждён источником (Telegram, с которого человек написал; контакт из отклика HH).</summary>
public sealed record IntakeContact(CandidateContactKind Kind, string Value, bool Verified);

/// <summary>
/// Приём кандидата из любого источника (вручную, HH, Telegram) — одна точка входа с дедупликацией: тот же внешний ID источника или
/// тот же подтверждённый контакт — тот же кандидат; совпал неподтверждённый контакт — подозрение на дубль для HR. Имя не сравнивается.
/// </summary>
public sealed record CandidateIntake(
    string FullName,
    string? City,
    string? Language,
    string? Notes,
    IReadOnlyList<IntakeContact> Contacts,
    string Source,
    string? ExternalId,
    string? SourceNote,
    Guid? VacancyId,
    string? ConsentChannel);

/// <summary>MatchedBy: null — новый кандидат; source — тот же ID в источнике; contact — тот же подтверждённый контакт.</summary>
public sealed record IntakeResult(Guid CandidateId, bool Created, string? MatchedBy, int Duplicates);

public sealed record CandidateQuery(string? Search, Guid? VacancyId, ApplicationStage? Stage, bool DuplicatesOnly, int Page = 1, int PageSize = 50);

public sealed record CandidateApplicationBrief(Guid VacancyId, string VacancyCode, string VacancyTitle, string Stage);

public sealed record CandidateRow(
    Guid Id,
    string FullName,
    string? City,
    string? Phone,
    IReadOnlyList<CandidateApplicationBrief> Applications,
    DateTimeOffset LastActivityAt,
    DateTimeOffset? DeleteAt,
    bool HasConsent,
    int OpenDuplicates,
    int Resumes);

public sealed record CandidatePage(IReadOnlyList<CandidateRow> Items, int Total, int Page, int PageSize);

public sealed record ContactView(Guid Id, string Kind, string Value, bool Verified, string Source);

public sealed record SourceView(string Source, string? ExternalId, string? Note, DateTimeOffset CreatedAt);

/// <summary>
/// Документ кандидата: статусы (Status — получен, сохранён, обрабатывается, готов, ошибка, отклонён; OcrStatus; AnalysisStatus), тип по содержимому,
/// разбор (Profile — поля с доказательствами), сводка, вакансия отклика, можно ли открыть в браузере (CanPreview — PDF, фото, текст).
/// </summary>
public sealed record ResumeView(Guid Id, string FileName, long SizeBytes, string Source, DateTimeOffset CreatedAt, bool HasText, string? TextError, string? TextPreview,
    bool TextByAi = false, string? DocumentType = null, bool CanReadAi = false, string Status = "Done", string OcrStatus = "NotNeeded",
    string AnalysisStatus = "Skipped", string? DetectedType = null, string? ContentType = null, int? PageCount = null, string? ProcessingError = null,
    string? Summary = null, Documents.ResumeProfile? Profile = null, string? VacancyCode = null, bool CanPreview = false, DateTimeOffset? ProcessedAt = null);

public sealed record ApplicationEventView(string? From, string To, string? Comment, string? Actor, DateTimeOffset At);

public sealed record ApplicationView(
    Guid Id,
    Guid VacancyId,
    string VacancyCode,
    string VacancyTitle,
    string VacancyStatus,
    string Stage,
    string Source,
    string? RejectReason,
    DateTimeOffset StageChangedAt,
    IReadOnlyList<ApplicationEventView> History,
    IReadOnlyDictionary<string, string>? Screening = null);

public sealed record ConsentView(Guid Id, string Channel, int TextVersion, DateTimeOffset GivenAt, DateTimeOffset? WithdrawnAt, string? RecordedBy);

public sealed record DuplicateView(Guid Id, Guid OtherCandidateId, string OtherName, string Kind, string Value);

public sealed record CandidateCard(
    Guid Id,
    string FullName,
    string? City,
    string? Language,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset? DeleteAt,
    int RetentionDays,
    bool HasConsent,
    IReadOnlyList<ContactView> Contacts,
    IReadOnlyList<SourceView> Sources,
    IReadOnlyList<ResumeView> Resumes,
    IReadOnlyList<ApplicationView> Applications,
    IReadOnlyList<ConsentView> Consents,
    IReadOnlyList<DuplicateView> Duplicates,
    CandidateLanguageView? LanguageInfo = null);

/// <summary>Язык переписки: код (ru-Cyrl, uz-Latn, uz-Cyrl; null — ещё не понятен), кто выбрал (auto, candidate, hr), последнее определение и уверенность.</summary>
public sealed record CandidateLanguageView(string? Code, string? Source, string? LastDetected, decimal? Confidence);

public sealed record ApplicationInput(Guid VacancyId);

public sealed record StageInput(ApplicationStage Stage, string? Comment, string? RejectReason);

public sealed record ConsentInput(string Channel);

// ---------- Настройки ----------

public sealed record RecruitmentSettingsView(int RetentionDays, int ConsentVersion, string ConsentText, DateTimeOffset? UpdatedAt);

public sealed record RecruitmentSettingsInput(int RetentionDays, string ConsentText);

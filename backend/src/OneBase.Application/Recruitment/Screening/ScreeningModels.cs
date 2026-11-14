using System.Text.Json;
using System.Text.Json.Serialization;
using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment.Screening;

/// <summary>Цитата из резюме, сообщений или заметок; Verified — найдена в тексте на сервере.</summary>
public sealed record EvidenceQuote(string Text, bool Verified);

/// <summary>
/// Критерий анализа (CriteriaJson версии 2). Name, Score (0–10), Explanation, Evidence, Verified — поля прежней оценки: их читают диалоги HR и
/// AI-рекрутер. Points из Max — баллы критерия; Status — met, partial, not_met, not_stated; Capped — балл урезан до половины (нет подтверждения).
/// </summary>
public sealed record ScreeningCriterion(
    string Code,
    string Name,
    int Max,
    decimal Points,
    int Score,
    string Status,
    string Explanation,
    string? Evidence,
    bool Verified,
    IReadOnlyList<EvidenceQuote> Quotes,
    bool Capped);

/// <summary>Вопрос для собеседования: type — technical, practical, experience, achievement, gap; reason — какой пробел или заявленный факт проверяем.</summary>
public sealed record InterviewQuestion(string Question, string Type, string? Reason);

/// <summary>DetailsJson версии 2.</summary>
public sealed record ScreeningDetails(
    string? WhyFits,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> Missing,
    string? RecommendationText,
    IReadOnlyList<InterviewQuestion> Questions,
    decimal Coverage,
    Guid? ResumeId,
    int HiddenProtected);

/// <summary>Анализ для экрана: версия 2 целиком; версия 1 (прежняя оценка) — итог, сводка и критерии по 10 баллов, без деталей.</summary>
public sealed record ScreeningAnalysisView(
    Guid Id,
    Guid ApplicationId,
    Guid CandidateId,
    int Version,
    int Overall,
    string Category,
    string CategoryName,
    string Recommendation,
    string Summary,
    string? WhyFits,
    IReadOnlyList<ScreeningCriterion> Criteria,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> Missing,
    string? RecommendationText,
    IReadOnlyList<InterviewQuestion> Questions,
    decimal? Confidence,
    decimal? Coverage,
    bool LowData,
    string? Risks,
    string? Model,
    DateTimeOffset CreatedAt,
    int? Delta);

public static class ScreeningJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Строка CandidateScores → анализ для экрана (обе версии). previous — итог предыдущего анализа того же отклика (для «+5 к прошлому»).</summary>
    public static ScreeningAnalysisView View(CandidateScore s, int? previous = null)
    {
        var category = ScreeningRules.Category(s.Overall);
        var delta = previous is { } p ? s.Overall - p : (int?)null;
        if (s.Version >= ScreeningRules.Version)
        {
            var criteria = Read<List<ScreeningCriterion>>(s.CriteriaJson) ?? [];
            var d = Read<ScreeningDetails>(s.DetailsJson);
            var coverage = d?.Coverage;
            return new ScreeningAnalysisView(s.Id, s.ApplicationId, s.CandidateId, s.Version, s.Overall, category, ScreeningRules.CategoryName(category),
                ScreeningRules.Recommendation(category), s.Summary, d?.WhyFits, criteria, d?.Strengths ?? [], d?.Weaknesses ?? [], d?.Missing ?? [],
                d?.RecommendationText, d?.Questions ?? [], s.Confidence, coverage, coverage < ScreeningRules.LowCoverage, s.Risks, s.Model, s.CreatedAt, delta);
        }

        var old = Read<List<CriterionScore>>(s.CriteriaJson) ?? [];
        var legacy = old.Select((c, i) => new ScreeningCriterion($"v1-{i}", c.Name, 10, c.Score, c.Score, c.Score > 0 ? "met" : "not_stated", c.Explanation,
            c.Evidence, c.Verified, c.Evidence is { } e ? [new EvidenceQuote(e, c.Verified)] : [], false)).ToList();
        return new ScreeningAnalysisView(s.Id, s.ApplicationId, s.CandidateId, s.Version, s.Overall, category, ScreeningRules.CategoryName(category),
            ScreeningRules.Recommendation(category), s.Summary, null, legacy, [], [], [], null, [], s.Confidence, null, false, s.Risks, s.Model, s.CreatedAt, delta);
    }

    private static T? Read<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

// ---------- Экран AI-скрининга ----------

/// <summary>Фильтры списка: вакансия, статус, оценка не ниже, город, навык, стаж от (мес.), желаемая должность, имя; порядок — score, recent, name.</summary>
public sealed record ScreeningQuery(
    Guid? VacancyId = null,
    string? Status = null,
    int? MinScore = null,
    string? City = null,
    string? Skill = null,
    int? MinExperienceMonths = null,
    string? Position = null,
    string? Search = null,
    string? Sort = null);

/// <summary>
/// Верхняя панель: кандидатов (людей), проанализировано (откликов с анализом), подходящих (последняя оценка AI не ниже 70), рекомендованных
/// (отметка HR), ожидают анализа (открытый отклик, есть текст резюме, анализа нет или резюме новее анализа, сейчас не анализируется).
/// </summary>
public sealed record ScreeningKpis(int Candidates, int Analyzed, int Suitable, int Recommended, int Pending);

/// <summary>Вакансия на панели: найдено откликов, проанализировано, подходящих, средняя оценка, последняя синхронизация HH, последний анализ.</summary>
public sealed record ScreeningVacancy(
    Guid Id,
    string Code,
    string Title,
    string Status,
    string? Requirements,
    string? Criteria,
    int Found,
    int Analyzed,
    int Suitable,
    decimal? AverageScore,
    DateTimeOffset? LastSyncAt,
    DateTimeOffset? LastAnalysisAt,
    bool OnHh);

public sealed record ScreeningRow(
    Guid ApplicationId,
    Guid CandidateId,
    string FullName,
    string? DesiredPosition,
    string? City,
    int? ExperienceMonths,
    string? LastJob,
    IReadOnlyList<string> Skills,
    string? SalaryExpectation,
    string? Education,
    Guid VacancyId,
    string VacancyCode,
    string VacancyTitle,
    string Status,
    string Stage,
    string Source,
    int? Overall,
    string? Category,
    int? Version,
    decimal? Confidence,
    DateTimeOffset? AnalyzedAt,
    bool Analyzing,
    bool Pending,
    string? Summary,
    string? HhUrl,
    DateTimeOffset AppliedAt,
    bool HasResume,
    string? AnalysisError = null);

/// <summary>Коротко о HH для экрана скрининга: подключение, синхронизация, бесплатные запросы и заблокированные платные операции за 30 дней.</summary>
public sealed record HhGuardBrief(bool Connected, bool Enabled, DateTimeOffset? LastSyncAt, bool? LastSyncOk, int FreeCalls, int BlockedCalls, string? GuardReason,
    DateTimeOffset? GuardTrippedAt, string? LastBlockReason);

public sealed record ScreeningPage(
    ScreeningKpis Kpis,
    IReadOnlyList<ScreeningVacancy> Vacancies,
    IReadOnlyList<ScreeningRow> Items,
    int Total,
    IReadOnlyList<string> Cities,
    HhGuardBrief Hh);

public sealed record ScreeningStatusInput(string Status, string? Comment, string? RejectReason);

/// <summary>Поставить анализ в очередь: выбранные отклики или все ожидающие вакансии (не больше 50 за раз — платные вызовы AI).</summary>
public sealed record ScreeningQueueInput(Guid? VacancyId, IReadOnlyList<Guid>? ApplicationIds);

public sealed record ScreeningQueueResult(int Queued, int Skipped, string Message);

/// <summary>Анализы кандидата по откликам: последний и история рейтинга (новые сверху).</summary>
public sealed record CandidateScreeningView(Guid ApplicationId, Guid VacancyId, string VacancyCode, string VacancyTitle, string Status, bool Analyzing, bool Pending,
    string? HhUrl, IReadOnlyList<ScreeningAnalysisView> History);

/// <summary>Ручной импорт текстом: вставленный текст резюме (законно полученный), имя — если известно.</summary>
public sealed record ScreeningTextImport(Guid VacancyId, string Text, string? FullName, string? ConsentChannel);

/// <summary>По ссылке на профиль: кандидат заводится с источником и ссылкой; резюме по ссылке не скачивается (HR загружает файл).</summary>
public sealed record ScreeningLinkImport(Guid VacancyId, string Url, string? FullName, string? ConsentChannel);

public sealed record ScreeningImportResult(Guid CandidateId, Guid? ApplicationId, bool Created, string? MatchedBy, string Message);

// ---------- Сравнение ----------

public sealed record CompareInput(Guid VacancyId, IReadOnlyList<Guid> ApplicationIds);

public sealed record ComparisonCandidate(
    Guid ApplicationId,
    Guid CandidateId,
    string FullName,
    int Overall,
    string Category,
    string Experience,
    IReadOnlyList<string> Skills,
    string Achievements,
    string Education,
    string Salary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string Fit,
    IReadOnlyDictionary<string, decimal> Points);

public sealed record ComparisonVerdict(Guid ApplicationId, string Verdict);

public sealed record ComparisonView(
    Guid Id,
    Guid VacancyId,
    string VacancyCode,
    string VacancyTitle,
    IReadOnlyList<ComparisonCandidate> Candidates,
    Guid? BestApplicationId,
    IReadOnlyList<Guid> Ranking,
    string Recommendation,
    IReadOnlyList<ComparisonVerdict> Verdicts,
    string? Model,
    DateTimeOffset CreatedAt);

public sealed record ComparisonBrief(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<string> Names, string? Best);

/// <summary>Сравнение кандидатов (реализация — OneBase.AI: таблица из анализов, рекомендация — модель через шлюз).</summary>
public interface ICandidateComparer
{
    Task<ComparisonView> CompareAsync(RecruitmentContext ctx, CompareInput input, CancellationToken ct);
}

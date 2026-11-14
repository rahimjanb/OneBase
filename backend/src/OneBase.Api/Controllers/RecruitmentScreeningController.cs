using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OneBase.Api.Auth;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Screening;

namespace OneBase.Api.Controllers;

/// <summary>
/// HR → «Подбор» → «AI-скрининг»: экран (панель, вакансии, кандидаты с оценкой и фильтрами), анализ кандидата (сразу или очередью),
/// статус скрининга, анализы кандидата с историей рейтинга, ручной импорт резюме (файл, текст, ссылка на профиль), сравнение до пяти
/// кандидатов. Права проверяет ScreeningService; платные вызовы AI — только по действию HR и под ограничением частоты «ai».
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/hr/recruitment/screening")]
public sealed class RecruitmentScreeningController(RecruitmentService recruitment, ScreeningService screening) : ControllerBase
{
    private Task<RecruitmentContext> ContextAsync(CancellationToken ct) =>
        recruitment.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet]
    public async Task<ScreeningPage> Page([FromQuery] Guid? vacancyId, [FromQuery] string? status, [FromQuery] int? minScore, [FromQuery] string? city,
        [FromQuery] string? skill, [FromQuery] int? minExperience, [FromQuery] string? position, [FromQuery] string? search, [FromQuery] string? sort,
        CancellationToken ct) =>
        await screening.PageAsync(await ContextAsync(ct), new ScreeningQuery(vacancyId, status, minScore, city, skill, minExperience, position, search, sort), ct);

    [HttpGet("candidates/{id:guid}")]
    public async Task<IReadOnlyList<CandidateScreeningView>> Candidate(Guid id, CancellationToken ct) => await screening.CandidateAsync(await ContextAsync(ct), id, ct);

    /// <summary>Проанализировать сейчас (платный вызов AI).</summary>
    [HttpPost("applications/{id:guid}/analyze")]
    [EnableRateLimiting("ai")]
    public async Task<ScreeningAnalysisView> Analyze(Guid id, CancellationToken ct) => await screening.AnalyzeAsync(await ContextAsync(ct), id, ct);

    /// <summary>Поставить анализ в очередь: выбранные или все ожидающие вакансии (до 50 за раз).</summary>
    [HttpPost("queue")]
    [EnableRateLimiting("ai")]
    public async Task<ScreeningQueueResult> Queue(ScreeningQueueInput input, CancellationToken ct) => await screening.QueueAsync(await ContextAsync(ct), input, ct);

    [HttpPost("applications/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, ScreeningStatusInput input, CancellationToken ct) =>
        Ok(new { status = await screening.SetStatusAsync(await ContextAsync(ct), id, input, ct) });

    // ---------- Ручной импорт ----------

    [HttpPost("import/file")]
    [RequestSizeLimit(RecruitmentRules.MaxResumeBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = RecruitmentRules.MaxResumeBytes + 1024 * 1024)]
    public async Task<ScreeningImportResult> ImportFile([FromForm] Guid vacancyId, [FromForm] string? consent, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw new FieldValidationException("Выберите файл резюме.");
        }

        await using var stream = file.OpenReadStream();
        return await screening.ImportFileAsync(await ContextAsync(ct), vacancyId, new RecruitmentUpload(file.FileName, file.ContentType, file.Length, stream),
            string.IsNullOrWhiteSpace(consent) ? null : consent, ct);
    }

    [HttpPost("import/text")]
    public async Task<ScreeningImportResult> ImportText(ScreeningTextImport input, CancellationToken ct) => await screening.ImportTextAsync(await ContextAsync(ct), input, ct);

    [HttpPost("import/link")]
    public async Task<ScreeningImportResult> ImportLink(ScreeningLinkImport input, CancellationToken ct) => await screening.ImportLinkAsync(await ContextAsync(ct), input, ct);

    // ---------- Сравнение ----------

    /// <summary>Сравнить 2–5 кандидатов одной вакансии (таблица — из анализов, рекомендация — AI, платный вызов).</summary>
    [HttpPost("compare")]
    [EnableRateLimiting("ai")]
    public async Task<ComparisonView> Compare(CompareInput input, [FromServices] IEnumerable<ICandidateComparer> comparers, CancellationToken ct) =>
        await (comparers.FirstOrDefault() ?? throw new FieldValidationException("AI не подключён — сравнение недоступно.")).CompareAsync(await ContextAsync(ct), input, ct);

    [HttpGet("vacancies/{id:guid}/comparisons")]
    public async Task<IReadOnlyList<ComparisonBrief>> Comparisons(Guid id, CancellationToken ct) => await screening.ComparisonsAsync(await ContextAsync(ct), id, ct);

    [HttpGet("comparisons/{id:guid}")]
    public async Task<ComparisonView> Comparison(Guid id, CancellationToken ct) => await screening.ComparisonAsync(await ContextAsync(ct), id, ct);
}

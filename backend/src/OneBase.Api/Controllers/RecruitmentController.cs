using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Domain.Recruitment;

namespace OneBase.Api.Controllers;

/// <summary>
/// HR → «Подбор»: заявки на сотрудников, вакансии, единая база кандидатов (контакты, резюме, отклики и этапы, согласия, дубли), настройки
/// срока хранения. Кто что может, решает RecruitmentService по правам и руководству отделом: нет права — 403, нет записи — 404.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/hr/recruitment")]
public sealed class RecruitmentController(RecruitmentService recruitment, IFileStorage storage) : ControllerBase
{
    private Task<RecruitmentContext> ContextAsync(CancellationToken ct) =>
        recruitment.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    public sealed record DuplicateInput(Guid KeepId, Guid ViewFor);

    [HttpGet("context")]
    public async Task<RecruitmentContextView> Context(CancellationToken ct) => await recruitment.ContextViewAsync(await ContextAsync(ct), ct);

    // ---------- Заявки на сотрудника ----------

    [HttpGet("requests")]
    public async Task<IReadOnlyList<HiringRequestView>> Requests([FromQuery] HiringRequestStatus? status, CancellationToken ct) =>
        await recruitment.RequestsAsync(await ContextAsync(ct), status, ct);

    [HttpPost("requests")]
    public async Task<HiringRequestView> CreateRequest(HiringRequestInput input, CancellationToken ct) =>
        await recruitment.CreateRequestAsync(await ContextAsync(ct), input, ct);

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<HiringRequestView> CancelRequest(Guid id, CancellationToken ct) => await recruitment.CancelRequestAsync(await ContextAsync(ct), id, ct);

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<HiringRequestView> ApproveRequest(Guid id, DecisionInput input, CancellationToken ct) =>
        await recruitment.ApproveRequestAsync(await ContextAsync(ct), id, input, ct);

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<HiringRequestView> RejectRequest(Guid id, DecisionInput input, CancellationToken ct) =>
        await recruitment.RejectRequestAsync(await ContextAsync(ct), id, input, ct);

    // ---------- Вакансии ----------

    [HttpGet("vacancies")]
    public async Task<IReadOnlyList<VacancyRow>> Vacancies([FromQuery] VacancyStatus? status, [FromQuery] string? search, CancellationToken ct) =>
        await recruitment.VacanciesAsync(await ContextAsync(ct), status, search, ct);

    [HttpGet("vacancies/{id:guid}")]
    public async Task<VacancyView> Vacancy(Guid id, CancellationToken ct) => await recruitment.VacancyAsync(await ContextAsync(ct), id, ct);

    [HttpPost("vacancies")]
    public async Task<VacancyView> CreateVacancy(VacancyInput input, CancellationToken ct) => await recruitment.CreateVacancyAsync(await ContextAsync(ct), input, ct);

    [HttpPut("vacancies/{id:guid}")]
    public async Task<VacancyView> UpdateVacancy(Guid id, VacancyInput input, CancellationToken ct) =>
        await recruitment.UpdateVacancyAsync(await ContextAsync(ct), id, input, ct);

    [HttpPost("vacancies/{id:guid}/status")]
    public async Task<VacancyView> VacancyStatus(Guid id, VacancyStatusInput input, CancellationToken ct) =>
        await recruitment.SetVacancyStatusAsync(await ContextAsync(ct), id, input.Status, ct);

    // ---------- Кандидаты ----------

    [HttpGet("candidates")]
    public async Task<CandidatePage> Candidates([FromQuery] string? search, [FromQuery] Guid? vacancyId, [FromQuery] ApplicationStage? stage,
        [FromQuery] bool duplicates = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        await recruitment.CandidatesAsync(await ContextAsync(ct), new CandidateQuery(search, vacancyId, stage, duplicates, page, pageSize), ct);

    [HttpGet("candidates/{id:guid}")]
    public async Task<CandidateCard> Candidate(Guid id, CancellationToken ct) => await recruitment.CandidateAsync(await ContextAsync(ct), id, ct);

    /// <summary>Новый кандидат. Тот же человек по подтверждённому контакту — откроется существующий (created = false, matchedBy).</summary>
    [HttpPost("candidates")]
    public async Task<IntakeResult> CreateCandidate(CandidateInput input, CancellationToken ct) => await recruitment.CreateCandidateAsync(await ContextAsync(ct), input, ct);

    [HttpPut("candidates/{id:guid}")]
    public async Task<CandidateCard> UpdateCandidate(Guid id, CandidateUpdateInput input, CancellationToken ct) =>
        await recruitment.UpdateCandidateAsync(await ContextAsync(ct), id, input, ct);

    /// <summary>Удалить все данные кандидата (резюме, контакты, отклики, согласия). Необратимо.</summary>
    [HttpDelete("candidates/{id:guid}")]
    public async Task<IActionResult> DeleteCandidate(Guid id, CancellationToken ct)
    {
        await recruitment.DeleteCandidateAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("candidates/{id:guid}/contacts")]
    public async Task<CandidateCard> AddContact(Guid id, ContactInput input, CancellationToken ct) => await recruitment.AddContactAsync(await ContextAsync(ct), id, input, ct);

    [HttpDelete("candidates/{id:guid}/contacts/{contactId:guid}")]
    public async Task<CandidateCard> RemoveContact(Guid id, Guid contactId, CancellationToken ct) =>
        await recruitment.RemoveContactAsync(await ContextAsync(ct), id, contactId, ct);

    [HttpPost("candidates/{id:guid}/resumes")]
    [RequestSizeLimit(RecruitmentRules.MaxResumeBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = RecruitmentRules.MaxResumeBytes + 1024 * 1024)]
    public async Task<CandidateCard> UploadResume(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw new FieldValidationException("Выберите файл резюме.");
        }

        await using var stream = file.OpenReadStream();
        return await recruitment.UploadResumeAsync(await ContextAsync(ct), id, new RecruitmentUpload(file.FileName, file.ContentType, file.Length, stream), ct);
    }

    /// <summary>Скачать резюме — всегда файлом, без кэширования (персональные данные).</summary>
    [HttpGet("resumes/{id:guid}")]
    public async Task<IActionResult> DownloadResume(Guid id, CancellationToken ct)
    {
        var file = await recruitment.DownloadResumeAsync(await ContextAsync(ct), id, ct);
        var buffer = new MemoryStream();
        await storage.DownloadAsync(file.ObjectKey, buffer, ct);
        buffer.Position = 0;
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(buffer, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Просмотр в браузере (PDF, фото, текст) — тем же правом, что и скачивание; остальное (Word, RTF) — только скачать. Без кэширования; картинка
    /// и текст — в «песочнице» (скрипты не выполняются).
    /// </summary>
    [HttpGet("resumes/{id:guid}/preview")]
    public async Task<IActionResult> PreviewResume(Guid id, CancellationToken ct)
    {
        var file = await recruitment.DownloadResumeAsync(await ContextAsync(ct), id, ct);
        if (file.ContentType == "application/octet-stream")
        {
            throw new FieldValidationException("Этот формат в браузере не открывается — скачайте файл.");
        }

        var buffer = new MemoryStream();
        await storage.DownloadAsync(file.ObjectKey, buffer, ct);
        buffer.Position = 0;
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        if (file.ContentType != "application/pdf")
        {
            Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";
        }

        Response.Headers.ContentDisposition = new System.Net.Mime.ContentDisposition { Inline = true, FileName = "document" + Path.GetExtension(file.FileName) }.ToString();
        return File(buffer, file.ContentType);
    }

    [HttpDelete("resumes/{id:guid}")]
    public async Task<CandidateCard> DeleteResume(Guid id, CancellationToken ct) => await recruitment.DeleteResumeAsync(await ContextAsync(ct), id, ct);

    /// <summary>«Распознать заново (AI)»: фото или скан читает AI заново, затем разбор (платные вызовы — по кнопке HR).</summary>
    [HttpPost("resumes/{id:guid}/read")]
    [EnableRateLimiting("ai")]
    public async Task<CandidateCard> ReadResume(Guid id, CancellationToken ct) => await recruitment.ReadResumeAgainAsync(await ContextAsync(ct), id, true, ct);

    /// <summary>«Обработать заново»: скачать из Telegram (если ещё не скачан), прочитать текст и разобрать — в фоне.</summary>
    [HttpPost("resumes/{id:guid}/reprocess")]
    [EnableRateLimiting("ai")]
    public async Task<CandidateCard> ReprocessResume(Guid id, CancellationToken ct) => await recruitment.ReadResumeAgainAsync(await ContextAsync(ct), id, false, ct);

    [HttpPost("candidates/{id:guid}/applications")]
    public async Task<CandidateCard> AddApplication(Guid id, ApplicationInput input, CancellationToken ct) =>
        await recruitment.AddApplicationAsync(await ContextAsync(ct), id, input, ct);

    [HttpPost("applications/{id:guid}/stage")]
    public async Task<CandidateCard> Stage(Guid id, StageInput input, CancellationToken ct) => await recruitment.ChangeStageAsync(await ContextAsync(ct), id, input, ct);

    [HttpPost("candidates/{id:guid}/consents")]
    public async Task<CandidateCard> RecordConsent(Guid id, ConsentInput input, CancellationToken ct) =>
        await recruitment.RecordConsentAsync(await ContextAsync(ct), id, input, ct);

    /// <summary>Отзыв согласия — данные кандидата удаляются сразу.</summary>
    [HttpPost("consents/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawConsent(Guid id, CancellationToken ct)
    {
        await recruitment.WithdrawConsentAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("duplicates/{id:guid}/merge")]
    public async Task<CandidateCard> Merge(Guid id, DuplicateInput input, CancellationToken ct) =>
        await recruitment.MergeDuplicateAsync(await ContextAsync(ct), id, input.KeepId, ct);

    [HttpPost("duplicates/{id:guid}/dismiss")]
    public async Task<CandidateCard> Dismiss(Guid id, DuplicateInput input, CancellationToken ct) =>
        await recruitment.DismissDuplicateAsync(await ContextAsync(ct), id, input.ViewFor, ct);

    // ---------- Настройки ----------

    [HttpGet("settings")]
    public async Task<RecruitmentSettingsView> Settings(CancellationToken ct) => await recruitment.SettingsAsync(await ContextAsync(ct), ct);

    [HttpPut("settings")]
    public async Task<RecruitmentSettingsView> SaveSettings(RecruitmentSettingsInput input, CancellationToken ct) =>
        await recruitment.SaveSettingsAsync(await ContextAsync(ct), input, ct);
}

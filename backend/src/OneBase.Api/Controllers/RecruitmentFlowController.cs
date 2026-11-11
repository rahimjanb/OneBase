using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Recruitment;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Recruitment;

namespace OneBase.Api.Controllers;

/// <summary>
/// HR → «Подбор»: AI-оценка кандидатов и тексты объявлений, собеседования (интервьюеры, свободное время, запись, статусы), центр исключений,
/// настройки AI-рекрутера и «миссии» вакансий, аналитика. Права — как у подбора.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/hr/recruitment")]
public sealed class RecruitmentFlowController(RecruitmentService recruitment, InterviewService interviews, RecruitmentExceptions exceptions,
    RecruitmentAutomation automation, ICandidateScorer scorer, IAppDbContext db, TelegramOutbox outbox) : ControllerBase
{
    private Task<RecruitmentContext> ContextAsync(CancellationToken ct) =>
        recruitment.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    public sealed record StatusInput(InterviewStatus Status);

    // ---------- Оценка и объявления ----------

    [HttpPost("applications/{id:guid}/score")]
    public async Task<ScoreView> Score(Guid id, CancellationToken ct) => await scorer.ScoreAsync(await ContextAsync(ct), id, ct);

    [HttpGet("candidates/{id:guid}/scores")]
    public async Task<IReadOnlyList<ScoreView>> Scores(Guid id, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        var rows = await db.CandidateScores.AsNoTracking().Where(s => s.CandidateId == id).OrderByDescending(s => s.CreatedAt).Take(20).ToListAsync(ct);
        return rows.Select(CandidateScoring.View).ToList();
    }

    [HttpPost("vacancies/{id:guid}/post-text")]
    public async Task<IActionResult> PostText(Guid id, PostTextInput input, CancellationToken ct) =>
        Ok(new { text = await scorer.PostTextAsync(await ContextAsync(ct), id, input, ct) });

    // ---------- Собеседования ----------

    /// <summary>Собеседования по виду: today, tomorrow, week, upcoming, completed, cancelled, rescheduled (без вида — по старому флагу upcoming).</summary>
    [HttpGet("interviews")]
    public async Task<IReadOnlyList<InterviewView>> Interviews([FromQuery] string? view, [FromQuery] bool upcoming = true, CancellationToken ct = default) =>
        await interviews.ListAsync(await ContextAsync(ct), view ?? (upcoming ? "upcoming" : "past"), ct);

    [HttpGet("interviews/{id:guid}")]
    public async Task<InterviewView> Interview(Guid id, CancellationToken ct) => await interviews.GetAsync(await ContextAsync(ct), id, ct);

    [HttpPost("interviews/{id:guid}/reschedule")]
    public async Task<InterviewView> Reschedule(Guid id, RescheduleInput input, CancellationToken ct) =>
        await interviews.RescheduleByHrAsync(await ContextAsync(ct), id, input, ct);

    [HttpPost("interviews/{id:guid}/cancel")]
    public async Task<InterviewView> Cancel(Guid id, CancelInput input, CancellationToken ct) => await interviews.CancelByHrAsync(await ContextAsync(ct), id, input, ct);

    /// <summary>Кандидат подтвердил время (по телефону, лично) — отметка HR.</summary>
    [HttpPost("interviews/{id:guid}/confirm")]
    public async Task<InterviewView> Confirm(Guid id, CancellationToken ct) => await interviews.ConfirmByHrAsync(await ContextAsync(ct), id, ct);

    [HttpPost("interviews/{id:guid}/feedback")]
    public async Task<InterviewView> Feedback(Guid id, FeedbackInput input, CancellationToken ct) =>
        await interviews.SaveFeedbackAsync(await ContextAsync(ct), id, input, recruitment, ct);

    [HttpGet("vacancies/{id:guid}/interview-settings")]
    public async Task<InterviewSettingsView> InterviewSettings(Guid id, CancellationToken ct) => await interviews.SettingsViewAsync(await ContextAsync(ct), id, ct);

    [HttpPut("vacancies/{id:guid}/interview-settings")]
    public async Task<InterviewSettingsView> SaveInterviewSettings(Guid id, InterviewSettingsInput input, CancellationToken ct) =>
        await interviews.SaveSettingsAsync(await ContextAsync(ct), id, input, ct);

    [HttpGet("candidates/{id:guid}/interviews")]
    public async Task<IReadOnlyList<InterviewView>> CandidateInterviews(Guid id, CancellationToken ct) => await interviews.ForCandidateAsync(await ContextAsync(ct), id, ct);

    [HttpGet("vacancies/{id:guid}/interviewers")]
    public async Task<IReadOnlyList<Guid>> Interviewers(Guid id, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        return await db.VacancyInterviewers.AsNoTracking().Where(i => i.VacancyId == id).Select(i => i.UserId).ToListAsync(ct);
    }

    [HttpPut("vacancies/{id:guid}/interviewers")]
    public async Task<IReadOnlyList<Guid>> SetInterviewers(Guid id, InterviewersInput input, CancellationToken ct) =>
        await interviews.SetInterviewersAsync(await ContextAsync(ct), id, input, ct);

    /// <summary>Свободное время интервьюеров вакансии: на день (date — все варианты дня) или ближайшие (по одному в день, 2 недели).</summary>
    [HttpGet("vacancies/{id:guid}/slots")]
    public async Task<IReadOnlyList<SlotView>> Slots(Guid id, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        return date is { } day ? await interviews.DaySlotsAsync(ctx, id, day, ct) : await interviews.FreeSlotsAsync(id, 12, 14, ct);
    }

    /// <summary>Отклики для окна «Новое собеседование» (поиск по кандидату, телефону, вакансии) — с действующим собеседованием, если оно уже есть.</summary>
    [HttpGet("interviews/bookable")]
    public async Task<IReadOnlyList<BookableView>> Bookable([FromQuery] string? search, CancellationToken ct) =>
        await interviews.BookableAsync(await ContextAsync(ct), search, ct);

    [HttpPost("interviews/book")]
    public async Task<IReadOnlyList<InterviewView>> Book(BookInput input, CancellationToken ct) => await interviews.BookByHrAsync(await ContextAsync(ct), input, ct);

    /// <summary>Пригласить: варианты времени — кандидату в его диалог Telegram на его языке (он выберет номером); диалога нет — варианты видны в карточке.</summary>
    [HttpPost("applications/{id:guid}/propose")]
    public async Task<IReadOnlyList<InterviewView>> Propose(Guid id, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Собеседования назначает HR.");
        }

        var (interview, _) = await interviews.InviteAsync(id, ctx.UserId, ct);
        return await interviews.ForCandidateAsync(ctx, interview.CandidateId, ct);
    }

    [HttpPost("interviews/{id:guid}/status")]
    public async Task<IReadOnlyList<InterviewView>> InterviewStatus(Guid id, StatusInput input, CancellationToken ct) =>
        await interviews.SetStatusAsync(await ContextAsync(ct), id, input.Status, ct);

    // ---------- Исключения ----------

    [HttpGet("exceptions")]
    public async Task<IReadOnlyList<ExceptionView>> Exceptions([FromQuery] RecruitmentExceptionStatus? status, CancellationToken ct) =>
        await exceptions.ListAsync(await ContextAsync(ct), status, ct);

    [HttpPost("exceptions/{id:guid}/resolve")]
    public async Task<IReadOnlyList<ExceptionView>> Resolve(Guid id, ResolveInput input, CancellationToken ct) => await exceptions.ResolveAsync(await ContextAsync(ct), id, input, ct);

    // ---------- AI-рекрутер, миссии вакансий, аналитика ----------

    [HttpGet("ai-settings")]
    public async Task<AiSettingsView> AiSettings(CancellationToken ct) => await automation.AiSettingsAsync(await ContextAsync(ct), ct);

    [HttpPut("ai-settings")]
    public async Task<AiSettingsView> SaveAiSettings(AiSettingsInput input, CancellationToken ct) => await automation.SaveAiSettingsAsync(await ContextAsync(ct), input, ct);

    [HttpPut("vacancies/{id:guid}/ai")]
    public async Task<VacancyView> VacancyAi(Guid id, VacancyAiInput input, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        await automation.SetVacancyAiAsync(ctx, id, input, ct);
        return await recruitment.VacancyAsync(ctx, id, ct);
    }

    /// <summary>ИИ-рекрутеры HR (для выбора у вакансии): имя, включён ли, по умолчанию ли. Настройки — «Настройки → ИИ-агенты».</summary>
    [HttpGet("recruiters")]
    public async Task<IReadOnlyList<OneBase.Application.Agents.AgentListItem>> Recruiters([FromServices] OneBase.Application.Agents.DialogAgentService agents, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        return await agents.RecruitersAsync(ct);
    }

    [HttpGet("analytics")]
    public async Task<AnalyticsView> Analytics(CancellationToken ct) => await automation.AnalyticsAsync(await ContextAsync(ct), ct);
}

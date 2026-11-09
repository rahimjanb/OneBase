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

    [HttpGet("interviews")]
    public async Task<IReadOnlyList<InterviewView>> Interviews([FromQuery] bool upcoming = true, CancellationToken ct = default) =>
        await interviews.ListAsync(await ContextAsync(ct), upcoming, ct);

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

    [HttpGet("vacancies/{id:guid}/slots")]
    public async Task<IReadOnlyList<SlotView>> Slots(Guid id, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        return await interviews.FreeSlotsAsync(id, 12, 14, ct);
    }

    [HttpPost("interviews/book")]
    public async Task<IReadOnlyList<InterviewView>> Book(BookInput input, CancellationToken ct) => await interviews.BookByHrAsync(await ContextAsync(ct), input, ct);

    /// <summary>Предложить кандидату варианты времени в его диалоге Telegram (если он есть) — он выберет номером.</summary>
    [HttpPost("applications/{id:guid}/propose")]
    public async Task<IReadOnlyList<InterviewView>> Propose(Guid id, CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Собеседования назначает HR.");
        }

        var (interview, slots) = await interviews.ProposeAsync(id, ctx.UserId, ct);
        var conversation = await db.TelegramConversations.AsNoTracking()
            .Where(c => c.Department == TelegramDepartments.Hr && c.RelatedType == RecruitmentService.CandidateRelation && c.RelatedId == interview.CandidateId)
            .OrderByDescending(c => c.LastMessageAt).FirstOrDefaultAsync(ct);
        if (conversation is not null && slots.Count > 0)
        {
            var uzbek = await db.Candidates.AsNoTracking().Where(c => c.Id == interview.CandidateId).Select(c => c.Language).FirstOrDefaultAsync(ct) == "uz";
            var text = (uzbek ? "Sizni suhbatga taklif qilamiz. Qulay vaqtni tanlang:\n" : "Приглашаем вас на собеседование. Выберите удобное время:\n")
                       + InterviewService.SlotsText(slots, uzbek) + (uzbek ? "\nVariant raqamini yozing." : "\nНапишите номер варианта.");
            await outbox.EnqueueAsync(new OutboxRequest(conversation.AccountId, conversation.PeerId, conversation.Id, TelegramDepartments.Hr, "manual", text,
                $"propose:{interview.Id:N}:{slots[0].StartsAt:yyyyMMddHHmm}", "user", ctx.UserId), ct);
        }

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

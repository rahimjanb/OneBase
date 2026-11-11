using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Recruitment.Jobs;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment.Documents;

/// <summary>
/// hr.resume.analysis — разбор резюме: поля из задания с доказательствами (модель AI, проверка цитат на сервере; без AI — только контакты из
/// текста), сводка; карточка кандидата дополняется проверенным (контакты, город, имя вместо ника), прежнее не затирается; похожий кандидат —
/// подозрение на дубль для HR; оценка под вакансию — задание hr.candidate.scoring; кандидату — продолжение разговора рекрутером (теперь он
/// видит резюме). Сертификат и документ личности не разбираются.
/// </summary>
public sealed class ResumeAnalysisJob(
    IAppDbContext db,
    RecruitmentService recruitment,
    IEnumerable<IResumeAnalyzer> analyzers,
    IEnumerable<IRecruiterAgent> agents,
    RecruitmentJobQueue jobs,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<ResumeAnalysisJob> logger) : IRecruitmentJobHandler
{
    public string Queue => RecruitmentQueues.Analysis;

    /// <summary>
    /// Резюме старше — разбор для HR как обычно, но кандидату ничего не уходит: ни продолжения разговора, ни автооценки с приглашением на
    /// собеседование (оценить можно кнопкой в карточке). Так резюме, которые ждали согласия до его отмены или которые HR разобрал заново,
    /// не превращаются в сообщения кандидатам спустя дни.
    /// </summary>
    public static readonly TimeSpan WriteToCandidateWithin = TimeSpan.FromHours(24);

    public async Task RunAsync(RecruitmentJob job, CancellationToken ct)
    {
        var resume = await db.CandidateResumes.FirstOrDefaultAsync(r => r.Id == job.RelatedId, ct);
        if (resume is null || resume.Text is null || resume.AnalysisStatus != DocumentStepStatus.Pending)
        {
            return;
        }

        var settings = await db.RecruitmentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? new RecruitmentSettings();
        var quality = ResumeProfiles.Quality(resume.Text, resume.TextByAi);
        var analyzer = analyzers.FirstOrDefault();
        ResumeProfile profile;
        if (!settings.AnalyzeResumes || analyzer is null || resume.DocumentType is "certificate" or "other")
        {
            profile = ResumeProfiles.ContactsOnly(resume.Text, quality);
            resume.AnalysisStatus = DocumentStepStatus.Skipped;
        }
        else
        {
            try
            {
                var vacancy = await VacancyAsync(resume, ct);
                profile = ResumeProfiles.Verify(await analyzer.AnalyzeAsync(resume.Text, vacancy, ct), resume.Text, quality);
                resume.AnalysisStatus = DocumentStepStatus.Done;
            }
            catch (ResumeAnalyzerUnavailableException ex)
            {
                logger.LogInformation("Подбор: разбор резюме {Document} без AI — {Reason}", resume.Id, ex.Message);
                profile = ResumeProfiles.ContactsOnly(resume.Text, quality);
                resume.AnalysisStatus = DocumentStepStatus.Skipped;
                resume.ProcessingError = ex.Message.Length <= 500 ? ex.Message : ex.Message[..500];
            }
        }

        resume.Profile = ResumeProfiles.Serialize(profile);
        resume.Summary = profile.Summary is { Length: > 0 } s ? (s.Length <= 2000 ? s : s[..2000]) : null;
        resume.Status = DocumentStatus.Done;
        resume.ProcessedAt = clock.GetUtcNow();
        resume.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var changes = await recruitment.EnrichFromResumeAsync(resume.CandidateId, profile, ct);
        await audit.LogAsync(ActorType.System, "recruitment", "recruitment.document.analyzed", nameof(Candidate), resume.CandidateId.ToString(),
            new { document = resume.Id, status = resume.AnalysisStatus.ToString(), quality, changes }, ct);

        var fresh = resume.CreatedAt > clock.GetUtcNow() - WriteToCandidateWithin;
        if (!fresh)
        {
            return; // давнее резюме — только разбор для HR
        }

        // Оценка под вакансию — по открытому отклику (для приглашения на собеседование).
        var application = resume.ApplicationId is { } a && await db.CandidateApplications.AnyAsync(x => x.Id == a && x.ClosedAt == null, ct)
            ? a
            : await db.CandidateApplications.AsNoTracking().Where(x => x.CandidateId == resume.CandidateId && x.ClosedAt == null)
                .OrderByDescending(x => x.StageChangedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (application is { } app && resume.AnalysisStatus == DocumentStepStatus.Done)
        {
            await jobs.EnqueueAsync(RecruitmentQueues.Scoring, $"score:{app:N}:{resume.Id:N}", app, ct: ct);
        }

        // Кандидату — продолжение разговора: рекрутер теперь видит резюме (в диалоге, который ведёт AI).
        if (resume.Source == "telegram" && resume.DocumentType is null or "resume")
        {
            foreach (var agent in agents)
            {
                await agent.DocumentReadyAsync(resume.Id, ct);
            }
        }
    }

    public async Task OnFailedAsync(RecruitmentJob job, string error, CancellationToken ct)
    {
        // Разбор не удался, а текст есть: документ готов без разбора — HR видит текст и оригинал.
        if (await db.CandidateResumes.FirstOrDefaultAsync(r => r.Id == job.RelatedId, ct) is { } resume && resume.Status != DocumentStatus.Done)
        {
            resume.AnalysisStatus = DocumentStepStatus.Failed;
            resume.Profile ??= resume.Text is null ? null : ResumeProfiles.Serialize(ResumeProfiles.ContactsOnly(resume.Text, ResumeProfiles.Quality(resume.Text, resume.TextByAi)));
            resume.Status = DocumentStatus.Done;
            resume.ProcessingError = $"Разбор AI не выполнен: {(error.Length <= 400 ? error : error[..400])}";
            resume.ProcessedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<string?> VacancyAsync(CandidateResume resume, CancellationToken ct)
    {
        var vacancy = await db.CandidateApplications.AsNoTracking().Where(a => a.CandidateId == resume.CandidateId && a.ClosedAt == null)
            .OrderByDescending(a => a.StageChangedAt).Select(a => a.Vacancy).FirstOrDefaultAsync(ct);
        return vacancy is null ? null : $"{vacancy.Title}. {vacancy.Requirements}".Trim();
    }
}

/// <summary>hr.candidate.scoring — оценка кандидата под вакансию (балл, объяснение, цитаты), затем — решение о приглашении на собеседование.</summary>
public sealed class CandidateScoringJob(IEnumerable<ICandidateScorer> scorers, IEnumerable<IInterviewInviter> inviters) : IRecruitmentJobHandler
{
    public string Queue => RecruitmentQueues.Scoring;

    public async Task RunAsync(RecruitmentJob job, CancellationToken ct)
    {
        if (scorers.FirstOrDefault() is not { } scorer)
        {
            return;
        }

        try
        {
            await scorer.ScoreAsync(new RecruitmentContext(Guid.Empty, true, true, []), job.RelatedId, ct);
        }
        catch (Field.FieldValidationException)
        {
            return; // отклик закрыт или нет текста — оценивать нечего
        }

        foreach (var inviter in inviters)
        {
            await inviter.AfterScoringAsync(job.RelatedId, ct);
        }
    }

    public Task OnFailedAsync(RecruitmentJob job, string error, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>После оценки: подходит ли кандидат для приглашения на собеседование (порог вакансии) — реализация на этапе собеседований.</summary>
public interface IInterviewInviter
{
    Task AfterScoringAsync(Guid applicationId, CancellationToken ct);
}

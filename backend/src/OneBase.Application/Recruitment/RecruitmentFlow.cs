using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Recruitment;

public sealed record CriterionScore(string Name, int Score, string Explanation, string? Evidence, bool Verified);

public sealed record ScoreView(Guid Id, Guid ApplicationId, int Overall, string Summary, IReadOnlyList<CriterionScore> Criteria, string? Risks, string? Model,
    DateTimeOffset CreatedAt);

public sealed record PostTextInput(Guid AccountId, string Audience);

/// <summary>Оценка кандидата и тексты объявлений AI (реализация — OneBase.AI, через шлюз Claude). Совет HR, не решение.</summary>
public interface ICandidateScorer
{
    Task<ScoreView> ScoreAsync(RecruitmentContext ctx, Guid applicationId, CancellationToken ct);

    Task<string> PostTextAsync(RecruitmentContext ctx, Guid vacancyId, PostTextInput input, CancellationToken ct);
}

/// <summary>
/// Настройки подбора, не относящиеся к переписке: собеседования, автооценка, распознавание фото и сканов. AI-рекрутер (включён ли, как
/// представляется, напоминание, ограничения) — в «Настройки → ИИ-агенты»: RecruiterAgentId и RecruiterEnabled — для ссылки и подсказки.
/// </summary>
public sealed record AiSettingsView(string InterviewDayStart, string InterviewDayEnd, int InterviewSlotMinutes, string InterviewDays, bool AutoScore, bool ReadScans,
    Guid? RecruiterAgentId, bool RecruiterEnabled, bool AnalyzeResumes = true);

public sealed record AiSettingsInput(string InterviewDayStart, string InterviewDayEnd, int InterviewSlotMinutes, string InterviewDays, bool AutoScore, bool ReadScans = true,
    bool AnalyzeResumes = true);

/// <summary>Миссия вакансии: ведёт ли AI переписку, какой агент-рекрутер (null — по умолчанию), критерии оценки, вопросы отбора.</summary>
public sealed record VacancyAiInput(bool AiRecruiter, string? Criteria, Guid? AgentId = null, string? ScreeningQuestions = null);

public sealed record FunnelRow(Guid VacancyId, string Code, string Title, string Status, int Total, IReadOnlyDictionary<string, int> Stages, int Interviews, int Hired,
    double? DaysToHire);

public sealed record SourceRow(string Source, int Candidates, int Hired);

public sealed record AnalyticsView(IReadOnlyList<FunnelRow> Vacancies, IReadOnlyList<SourceRow> Sources, int AiReplies, int Handoffs, int OpenExceptions,
    int Candidates, int Conversations, double? MedianReplyMinutes);

/// <summary>
/// Автономный подбор (этап 7): настройки AI-рекрутера, «миссии» вакансий (AI ведёт переписку, критерии оценки), напоминание кандидату, который
/// не ответил (одно), напоминания о собеседовании, проверка номеров HR (исключение, если номер отключился или Telegram его ограничил), аналитика.
/// </summary>
public sealed class RecruitmentAutomation(IAppDbContext db, TelegramOutbox outbox, InterviewService interviews, RecruitmentExceptions exceptions,
    IEnumerable<ICandidateScorer> scorers, Agents.DialogAgentService agents, IAuditLogger audit, TimeProvider clock, ILogger<RecruitmentAutomation> logger)
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private async Task<RecruitmentSettings> SettingsAsync(CancellationToken ct)
    {
        var settings = await db.RecruitmentSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
        if (settings is null)
        {
            settings = new RecruitmentSettings();
            db.RecruitmentSettings.Add(settings);
            await db.SaveChangesAsync(ct);
        }

        return settings;
    }

    public async Task<AiSettingsView> AiSettingsAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        var s = await SettingsAsync(ct);
        var recruiter = await agents.RecruiterForAsync(null, null, ct);
        return new AiSettingsView(s.InterviewDayStart.ToString("HH:mm", CultureInfo.InvariantCulture), s.InterviewDayEnd.ToString("HH:mm", CultureInfo.InvariantCulture),
            s.InterviewSlotMinutes, s.InterviewDays, s.AutoScore, s.ReadScans, recruiter?.Id, recruiter?.Enabled == true, s.AnalyzeResumes);
    }

    public async Task<AiSettingsView> SaveAiSettingsAsync(RecruitmentContext ctx, AiSettingsInput input, CancellationToken ct)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Настройки подбора меняет HR.");
        }

        if (!TimeOnly.TryParse(input.InterviewDayStart, CultureInfo.InvariantCulture, out var start) || !TimeOnly.TryParse(input.InterviewDayEnd, CultureInfo.InvariantCulture, out var end)
            || end <= start)
        {
            throw new FieldValidationException("Рабочие часы собеседований: начало раньше конца, в формате 10:00.");
        }

        if (input.InterviewSlotMinutes is < 15 or > 240)
        {
            throw new FieldValidationException("Длина собеседования — 15–240 минут.");
        }

        var days = input.InterviewDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => int.TryParse(d, out var n) ? n : 0).Where(n => n is >= 1 and <= 7).Distinct().Order().ToList();
        if (days.Count == 0)
        {
            throw new FieldValidationException("Выберите хотя бы один день недели для собеседований.");
        }

        var s = await SettingsAsync(ct);
        (s.InterviewDayStart, s.InterviewDayEnd, s.InterviewSlotMinutes) = (start, end, input.InterviewSlotMinutes);
        s.InterviewDays = string.Join(',', days);
        s.AutoScore = input.AutoScore;
        s.ReadScans = input.ReadScans;
        s.AnalyzeResumes = input.AnalyzeResumes;
        s.UpdatedAt = clock.GetUtcNow();
        s.UpdatedById = ctx.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.ai.settings", nameof(RecruitmentSettings), "1",
            new { s.AutoScore, s.ReadScans, s.AnalyzeResumes, s.InterviewSlotMinutes }, ct);
        return await AiSettingsAsync(ctx, ct);
    }

    public async Task SetVacancyAiAsync(RecruitmentContext ctx, Guid vacancyId, VacancyAiInput input, CancellationToken ct)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Миссию вакансии меняет HR.");
        }

        var vacancy = await db.Vacancies.FirstOrDefaultAsync(v => v.Id == vacancyId, ct) ?? throw new FieldNotFoundException("Вакансия не найдена.");
        if (input.AgentId is { } agentId
            && !await db.DialogAgents.AnyAsync(a => a.Id == agentId && a.Kind == Domain.AI.DialogAgentKinds.Recruiter && a.Department == TelegramDepartments.Hr, ct))
        {
            throw new FieldValidationException("Такого ИИ-рекрутера нет — выберите в списке или оставьте «по умолчанию».");
        }

        vacancy.AiRecruiter = input.AiRecruiter;
        vacancy.AgentId = input.AgentId;
        vacancy.Criteria = input.Criteria?.Trim() is { Length: > 0 } c ? (c.Length <= 2000 ? c : c[..2000]) : null;
        vacancy.ScreeningQuestions = input.ScreeningQuestions?.Trim() is { Length: > 0 } q ? (q.Length <= 2000 ? q : q[..2000]) : null;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.vacancy.ai", nameof(Vacancy), vacancyId.ToString(), new { input.AiRecruiter, input.AgentId }, ct);
    }

    /// <summary>Один круг автоматики: напоминания о собеседованиях, напоминание не ответившим, проверка номеров HR, автооценка (если включена).</summary>
    public async Task<(int Reminders, int FollowUps, int Scored)> RunAsync(CancellationToken ct)
    {
        var reminders = await interviews.RemindAsync(ct);
        var settings = await SettingsAsync(ct);
        var followUps = await FollowUpsAsync(ct);
        await MonitorAsync(ct);
        var scored = settings.AutoScore ? await AutoScoreAsync(ct) : 0;
        return (reminders, followUps, scored);
    }

    /// <summary>
    /// Напоминание кандидату, который не ответил (одно на паузу), — в рабочее время агента-рекрутера, через столько часов, сколько задано у него
    /// («Настройки → ИИ-агенты»; 0 — не напоминать): диалог ведёт AI, последнее сообщение — от AI и не напоминание, подбор по вакансии идёт,
    /// агент диалога (по вакансии и номеру) включён. Текст — шаблон (без платного вызова), на языке кандидата.
    /// </summary>
    private async Task<int> FollowUpsAsync(CancellationToken ct)
    {
        var all = await db.DialogAgents.AsNoTracking().Where(a => a.Department == TelegramDepartments.Hr && a.Kind == Domain.AI.DialogAgentKinds.Recruiter).ToListAsync(ct);
        var recruiters = all.Where(a => a.Enabled && a.FollowUpHours > 0).ToList();
        if (recruiters.Count == 0)
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        var cutoff = now.AddHours(-recruiters.Min(a => a.FollowUpHours));
        var conversations = await db.TelegramConversations.AsNoTracking()
            .Where(c => c.Department == TelegramDepartments.Hr && c.Mode == TelegramConversationMode.Ai && c.RelatedType == RecruitmentService.CandidateRelation && c.LastMessageAt < cutoff)
            .Take(100).ToListAsync(ct);
        var queued = 0;
        foreach (var c in conversations)
        {
            var last = await db.TelegramConversationMessages.AsNoTracking().Where(m => m.ConversationId == c.Id).OrderByDescending(m => m.SentAt).FirstOrDefaultAsync(ct);
            if (last is not { Direction: TelegramMessageDirection.Out, Author: "ai" })
            {
                continue;
            }

            var lastOutgoing = await db.TelegramOutgoingMessages.AsNoTracking().Where(m => m.ConversationId == c.Id && m.Status == TelegramOutboxStatus.Sent)
                .OrderByDescending(m => m.SentAt).Select(m => m.Kind).FirstOrDefaultAsync(ct);
            if (lastOutgoing == "followup")
            {
                continue;
            }

            var candidate = await db.Candidates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.RelatedId, ct);
            var application = await db.CandidateApplications.AsNoTracking().Include(a => a.Vacancy)
                .Where(a => a.CandidateId == c.RelatedId && a.ClosedAt == null).OrderByDescending(a => a.StageChangedAt).FirstOrDefaultAsync(ct);
            if (candidate is null || application?.Vacancy is not { } vacancy)
            {
                continue;
            }

            // Агент диалога — по вакансии и номеру, как при ответе (DialogAgentPick).
            var agent = Agents.DialogAgentPick.Recruiter(all, vacancy.AgentId, c.AccountId);
            if (agent is not { Enabled: true } || agent.FollowUpHours <= 0 || last.SentAt > now.AddHours(-agent.FollowUpHours) || !Agents.DialogAgentRules.InWork(agent, now))
            {
                continue; // агент выключен, рано или нерабочее время — напомним позже
            }

            var name = candidate.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is { Length: > 1 } first && !first.StartsWith('@')
                       && !first.StartsWith("Telegram", StringComparison.Ordinal) ? first : null;
            var text = RecruiterTexts.FollowUp(CandidateLanguages.Of(candidate.Language, candidate.PreferredScript) ?? CandidateLang.Ru, name, vacancy.Title);
            await outbox.EnqueueAsync(new OutboxRequest(c.AccountId, c.PeerId, c.Id, TelegramDepartments.Hr, "followup", text, $"followup:{c.Id:N}:{last.Id}", "ai"), ct);
            queued++;
        }

        return queued;
    }

    /// <summary>Номер HR отключился или Telegram ограничил его отправку — исключение для HR (одно, пока не решено).</summary>
    private async Task MonitorAsync(CancellationToken ct)
    {
        var hr = await db.AccountsServing(TelegramDepartments.Hr).AsNoTracking().Where(a => db.TelegramAccountDepartments.Any(d => d.AccountId == a.Id))
            .Select(a => new { a.Id, a.Name, a.Status, a.LastError }).ToListAsync(ct);
        var now = clock.GetUtcNow();
        foreach (var a in hr.Where(a => a.Status != TelegramAccountStatus.Connected))
        {
            await exceptions.RaiseAsync("telegram", $"Номер HR «{a.Name}» не подключён ({a.Status}){(a.LastError is { } e ? $": {e}" : string.Empty)}. Кандидатам никто не отвечает — переподключите в «Настройки → Telegram».",
                dedupKey: $"telegram:{a.Id:N}", ct: ct);
        }

        var paused = await db.TelegramAccountDepartments.AsNoTracking().Where(d => d.Department == TelegramDepartments.Hr && d.PausedUntil > now).ToListAsync(ct);
        foreach (var p in paused)
        {
            await exceptions.RaiseAsync("telegram", $"Telegram ограничил отправку с номера HR до {(p.PausedUntil!.Value + Tashkent):dd.MM HH:mm}: {p.PauseReason}",
                dedupKey: $"pause:{p.AccountId:N}:{p.PausedUntil:yyyyMMddHH}", ct: ct);
        }
    }

    private async Task<int> AutoScoreAsync(CancellationToken ct)
    {
        var scorer = scorers.FirstOrDefault();
        if (scorer is null)
        {
            return 0;
        }

        var due = await db.CandidateApplications.AsNoTracking()
            .Where(a => a.ClosedAt == null && !db.CandidateScores.Any(s => s.ApplicationId == a.Id) && db.CandidateResumes.Any(r => r.CandidateId == a.CandidateId && r.Text != null))
            .OrderBy(a => a.CreatedAt).Select(a => a.Id).Take(5).ToListAsync(ct);
        var scored = 0;
        foreach (var id in due)
        {
            try
            {
                await scorer.ScoreAsync(new RecruitmentContext(Guid.Empty, true, true, []), id, ct);
                scored++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Подбор: автооценка отклика {Application} не выполнена", id);
                break; // AI недоступен или заблокирован — до следующего круга
            }
        }

        return scored;
    }

    // ---------- Аналитика ----------

    public async Task<AnalyticsView> AnalyticsAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        var vacancies = await db.Vacancies.AsNoTracking().OrderByDescending(v => v.CreatedAt).Take(200).ToListAsync(ct);
        var ids = vacancies.Select(v => v.Id).ToList();
        var applications = await db.CandidateApplications.AsNoTracking().Where(a => ids.Contains(a.VacancyId))
            .Select(a => new { a.Id, a.VacancyId, a.Stage, a.CreatedAt, a.Source }).ToListAsync(ct);
        var hiredEvents = await db.ApplicationEvents.AsNoTracking().Where(e => e.ToStage == ApplicationStage.Hired).Select(e => new { e.ApplicationId, e.At }).ToListAsync(ct);
        var interviewApps = await db.Interviews.AsNoTracking().Where(i => i.Status != InterviewStatus.Proposed).Select(i => i.ApplicationId).Distinct().ToListAsync(ct);
        var funnel = vacancies.Select(v =>
        {
            var list = applications.Where(a => a.VacancyId == v.Id).ToList();
            var days = list.Where(a => a.Stage == ApplicationStage.Hired)
                .Select(a => hiredEvents.Where(e => e.ApplicationId == a.Id).Select(e => (e.At - a.CreatedAt).TotalDays).DefaultIfEmpty(double.NaN).Max())
                .Where(d => !double.IsNaN(d)).ToList();
            return new FunnelRow(v.Id, v.Code, v.Title, v.Status.ToString(), list.Count,
                Enum.GetValues<ApplicationStage>().ToDictionary(s => s.ToString(), s => list.Count(a => a.Stage == s)),
                list.Count(a => interviewApps.Contains(a.Id)), list.Count(a => a.Stage == ApplicationStage.Hired), days.Count == 0 ? null : Math.Round(days.Average(), 1));
        }).ToList();

        var sources = await db.CandidateSources.AsNoTracking().GroupBy(s => s.Source).Select(g => new { g.Key, Count = g.Select(x => x.CandidateId).Distinct().Count() }).ToListAsync(ct);
        var hiredBySource = applications.Where(a => a.Stage == ApplicationStage.Hired).GroupBy(a => a.Source).ToDictionary(g => g.Key, g => g.Count());
        var aiReplies = await db.TelegramOutgoingMessages.CountAsync(m => m.Department == TelegramDepartments.Hr && m.Author == "ai" && m.Status == TelegramOutboxStatus.Sent, ct);
        var handoffs = await db.RecruitmentExceptions.CountAsync(e => e.ConversationId != null, ct);
        var open = await db.RecruitmentExceptions.CountAsync(e => e.Status == RecruitmentExceptionStatus.Open, ct);

        // Время ответа кандидату: от входящего до следующего исходящего в диалоге (за 30 дней), медиана.
        var since = clock.GetUtcNow().AddDays(-30);
        var messages = await db.TelegramConversationMessages.AsNoTracking().Where(m => m.SentAt >= since).OrderBy(m => m.ConversationId).ThenBy(m => m.SentAt)
            .Select(m => new { m.ConversationId, m.Direction, m.SentAt }).Take(20000).ToListAsync(ct);
        var waits = new List<double>();
        foreach (var group in messages.GroupBy(m => m.ConversationId))
        {
            DateTimeOffset? waiting = null;
            foreach (var m in group)
            {
                if (m.Direction == TelegramMessageDirection.In)
                {
                    waiting ??= m.SentAt;
                }
                else if (waiting is { } w)
                {
                    waits.Add((m.SentAt - w).TotalMinutes);
                    waiting = null;
                }
            }
        }

        waits.Sort();
        return new AnalyticsView(funnel, sources.Select(s => new SourceRow(s.Key, s.Count, hiredBySource.GetValueOrDefault(s.Key))).OrderByDescending(s => s.Candidates).ToList(),
            aiReplies, handoffs, open, await db.Candidates.CountAsync(ct),
            await db.TelegramConversations.CountAsync(c => c.Department == TelegramDepartments.Hr && c.RelatedType == RecruitmentService.CandidateRelation, ct),
            waits.Count == 0 ? null : Math.Round(waits[waits.Count / 2], 1));
    }
}

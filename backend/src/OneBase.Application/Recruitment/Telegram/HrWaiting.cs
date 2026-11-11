using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Application.Telegram.Routing;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Domain.Work;

namespace OneBase.Application.Recruitment.Telegram;

/// <summary>
/// Кандидаты, которые ждут ответа HR: AI-рекрутер передал диалог человеку («передаю коллегам»), а HR ещё не ответил («Нужен HR»). Круг
/// автоматики подбора (раз в 5 минут):
/// <list type="bullet">
/// <item>диалог HR, где человек молчит час (ни сообщения кандидату — из OneBase или с телефона, ни переключения, ни новой передачи), снова ведёт
///   AI-рекрутер: открытое по диалогу решено системой, в ленте — «Диалог вернулся к AI», а на то, что кандидат написал за это время, AI отвечает
///   сразу — зная всю переписку, и то, что писал HR. AI-рекрутер диалога выключен — диалог остаётся у HR;</item>
/// <item>HR — напоминание «Ждут ответа HR: N кандидатов» (кто ждёт дольше всех), не чаще раза в 15 минут каждому; прежнее непрочитанное
///   напоминание отмечается прочитанным, чтобы в колокольчике было одно, свежее.</item>
/// </list>
/// </summary>
public sealed class HrWaiting(
    IAppDbContext db,
    RecruitmentExceptions exceptions,
    UserNotifier notifier,
    Agents.DialogAgentService agents,
    IEnumerable<IRecruiterAgent> recruiters,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<HrWaiting> logger)
{
    /// <summary>Через сколько минут молчания HR диалог возвращается AI-рекрутеру.</summary>
    public const int HandBackMinutes = 60;

    /// <summary>Напоминание HR — не чаще, минут.</summary>
    public const int RemindEveryMinutes = 15;

    /// <summary>Ссылка напоминаний: «Диалоги Telegram» с фильтром «Нужен HR» (по ней же находится прежнее напоминание).</summary>
    public const string RemindersLink = "/hr/recruitment/telegram?filter=need_hr";

    /// <summary>
    /// Сообщения кандидата старше — при возврате AI на них не отвечает: диалог давно лежал у HR (например, до этой настройки), отвечать спустя
    /// дни странно. Обычно кандидат писал не раньше часа назад.
    /// </summary>
    private static readonly TimeSpan ReplyWindow = TimeSpan.FromHours(3);

    public static string Candidates(int n) => $"{n} {Sales.Metrics.SalesFormat.Plural(n, "кандидат", "кандидата", "кандидатов")}";

    private static IQueryable<TelegramConversation> HrDialogs(IAppDbContext db) =>
        db.TelegramConversations.AsNoTracking().Where(c => c.Department == TelegramDepartments.Hr && c.Status == TelegramConversationStatus.Active
                                                           && c.ChatType == TelegramChatType.Private);

    /// <summary>Сколько диалогов «Нужен HR» (ведёт человек, по диалогу открыто исключение); except — без этого диалога.</summary>
    public static Task<int> CountAsync(IAppDbContext db, Guid? except, CancellationToken ct) =>
        db.RecruitmentExceptions.AsNoTracking().Where(e => e.Status == RecruitmentExceptionStatus.Open && e.ConversationId != null && e.ConversationId != except)
            .Join(HrDialogs(db).Where(c => c.Mode == TelegramConversationMode.Manual), e => e.ConversationId!.Value, c => c.Id, (e, c) => c.Id)
            .Distinct().CountAsync(ct);

    public async Task<(int Returned, int Reminded)> RunAsync(CancellationToken ct)
    {
        var returned = await HandBackAsync(ct);
        var reminded = await RemindAsync(ct);
        if (returned + reminded > 0)
        {
            logger.LogInformation("Подбор: диалогов вернулось к AI (HR не ответил за час) {Returned}, напоминаний HR о ждущих кандидатах {Reminded}", returned, reminded);
        }

        return (returned, reminded);
    }

    /// <summary>Диалоги, где HR молчит час, — снова AI-рекрутеру. Возвращает, сколько вернулось.</summary>
    public async Task<int> HandBackAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddMinutes(-HandBackMinutes);
        var manual = await HrDialogs(db)
            .Where(c => c.Mode == TelegramConversationMode.Manual && c.RelatedType == RecruitmentService.CandidateRelation && c.RelatedId != null
                        && (c.UpdatedAt ?? c.CreatedAt) < cutoff)
            .OrderBy(c => c.UpdatedAt ?? c.CreatedAt).Take(100)
            .Select(c => new { c.Id, c.AccountId, c.PeerId, CandidateId = c.RelatedId!.Value, Changed = c.UpdatedAt ?? c.CreatedAt })
            .ToListAsync(ct);
        var returned = 0;
        foreach (var c in manual)
        {
            // Последнее действие человека: переключение режима (Changed), передача AI → HR, сообщение HR из OneBase (и неотправленное) или с телефона.
            var fromOneBase = await db.TelegramOutgoingMessages.AsNoTracking().Where(m => m.ConversationId == c.Id && m.Author == "user")
                .MaxAsync(m => (DateTimeOffset?)m.CreatedAt, ct);
            var written = await db.TelegramConversationMessages.AsNoTracking()
                .Where(m => m.ConversationId == c.Id && m.Direction == TelegramMessageDirection.Out && (m.Author == "user" || m.Author == "phone"))
                .MaxAsync(m => (DateTimeOffset?)m.SentAt, ct);
            var handoff = await db.RecruitmentExceptions.AsNoTracking().Where(e => e.ConversationId == c.Id).MaxAsync(e => (DateTimeOffset?)e.CreatedAt, ct);
            var human = new[] { c.Changed, fromOneBase ?? c.Changed, written ?? c.Changed, handoff ?? c.Changed }.Max();
            if (human > cutoff)
            {
                continue;
            }

            var application = await db.CandidateApplications.AsNoTracking().Include(a => a.Vacancy)
                .Where(a => a.CandidateId == c.CandidateId && a.ClosedAt == null).OrderByDescending(a => a.StageChangedAt).FirstOrDefaultAsync(ct);
            if (await agents.RecruiterForAsync(application?.Vacancy?.AgentId, c.AccountId, ct) is not { Enabled: true })
            {
                continue; // AI-рекрутер этого диалога выключен — вернуть некому, диалог остаётся у HR
            }

            if (await db.TelegramConversations.Where(x => x.Id == c.Id && x.Mode == TelegramConversationMode.Manual)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Mode, TelegramConversationMode.Ai).SetProperty(x => x.UpdatedAt, now)
                        .SetProperty(x => x.UpdatedById, (Guid?)null), ct) == 0)
            {
                continue; // HR успел вернуть или ответить сам
            }

            await exceptions.ResolveConversationAsync(c.Id, null, $"HR не ответил за {HandBackMinutes} минут — диалог вернулся к AI-рекрутеру", ct);
            await audit.LogAsync(ActorType.System, "recruitment", "telegram.conversation.mode", nameof(TelegramConversation), c.Id.ToString(),
                new { accountId = c.AccountId, peerId = c.PeerId, from = "Manual", to = "Ai", reason = "hr_timeout" }, ct);
            returned++;

            // Кандидат писал, пока ждал HR, — AI отвечает сразу на всё, что после передачи.
            var last = await db.TelegramConversationMessages.AsNoTracking().Where(m => m.ConversationId == c.Id).OrderByDescending(m => m.SentAt)
                .Select(m => new { m.Direction, m.SentAt }).FirstOrDefaultAsync(ct);
            if (last is { Direction: TelegramMessageDirection.In } && last.SentAt > human && last.SentAt > now - ReplyWindow && recruiters.FirstOrDefault() is { } recruiter)
            {
                try
                {
                    await recruiter.ResumeAsync(c.Id, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Подбор: AI-рекрутер не ответил кандидату после возврата диалога {Conversation}", c.Id);
                }
            }
        }

        return returned;
    }

    /// <summary>Напоминание HR о кандидатах, которые ждут ответа. Возвращает, скольким сотрудникам ушло.</summary>
    public async Task<int> RemindAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await db.RecruitmentExceptions.AsNoTracking().Where(e => e.Status == RecruitmentExceptionStatus.Open && e.ConversationId != null)
            .Join(HrDialogs(db).Where(c => c.Mode == TelegramConversationMode.Manual), e => e.ConversationId!.Value, c => c.Id,
                (e, c) => new { c.Id, e.CreatedAt, c.RelatedType, c.RelatedId })
            .ToListAsync(ct);
        var waiting = rows.GroupBy(r => r.Id)
            .Select(g => (Since: g.Min(x => x.CreatedAt), Candidate: g.First().RelatedType == RecruitmentService.CandidateRelation ? g.First().RelatedId : null))
            .OrderBy(w => w.Since).ToList();
        // Только что переданный — HR уже получил уведомление о передаче; напоминание — когда кто-то ждёт дольше 15 минут.
        if (waiting.Count == 0 || now - waiting[0].Since < TimeSpan.FromMinutes(RemindEveryMinutes))
        {
            return 0;
        }

        var name = waiting[0].Candidate is { } id ? await db.Candidates.AsNoTracking().Where(x => x.Id == id).Select(x => x.FullName).FirstOrDefaultAsync(ct) : null;
        var title = $"Ждут ответа HR в Telegram: {Candidates(waiting.Count)}";
        var body = $"Дольше всех ждёт {name ?? "кандидат"} — {(int)(now - waiting[0].Since).TotalMinutes} мин. Не ответите в течение {HandBackMinutes} минут " +
                   "после передачи — диалог вернётся к AI-рекрутеру.";
        var recent = now.AddMinutes(-RemindEveryMinutes);
        var sent = 0;
        foreach (var user in await db.ActiveUsersWithAsync(Permissions.HrRecruitmentManage, ct))
        {
            if (await db.UserNotifications.AnyAsync(n => n.RecipientId == user && n.Link == RemindersLink && n.CreatedAt > recent, ct))
            {
                continue;
            }

            await db.UserNotifications.Where(n => n.RecipientId == user && n.Link == RemindersLink && n.ReadAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), ct);
            notifier.Notify(user, UserNotificationKind.RecruitmentAlert, title, body, RemindersLink, now);
            sent++;
        }

        if (sent > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return sent;
    }
}

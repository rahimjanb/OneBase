using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Work;
using OneBase.Domain.AI;
using OneBase.Domain.Telegram;
using OneBase.Domain.Work;

namespace OneBase.Application.Agents;

/// <summary>
/// Ответ агента перед отправкой. Template — готовый текст OneBase (подтверждение записи, варианты времени, «передаю сотруднику»): без
/// проверки черновиком и запрещёнными фразами (его написал сотрудник в настройках).
/// </summary>
public sealed record AgentReplyDraft(
    DialogAgent Agent,
    Guid ConversationId,
    Guid AccountId,
    long PeerId,
    string? PeerName,
    int? IncomingMessageId,
    string? Incoming,
    string Action,
    IReadOnlyList<string> Messages,
    bool Template = false,
    string? Model = null,
    int InputTokens = 0,
    int OutputTokens = 0,
    Guid? CandidateId = null,
    Guid? InterviewId = null);

public sealed record DeliveryResult(Guid ReplyId, DialogReplyStatus Status, string? BlockReason, IReadOnlyList<string> Parts);

/// <summary>
/// Доставка ответов ИИ-агентов: ограничения (запрещённые фразы, длина, без разметки) → черновик на проверку (режим «С проверкой») или очередь
/// исходящих — сразу, без паузы и «печатает…», в любое время; части ответа (1–3 сообщения) уходят одна за другой. Новое сообщение собеседника
/// отменяет ещё не ушедший ответ — агент ответит на всё сразу. Каждый ответ — в журнал агента.
/// </summary>
public sealed class DialogAgentDelivery(IAppDbContext db, TelegramOutbox outbox, UserNotifier notifier, TimeProvider clock)
{
    public const string RelatedType = "agent_reply";

    /// <summary>
    /// Собеседник написал ещё или сотрудник взял диалог: ещё не отправленные ответы AI в диалоге отменяются, черновики — «заменён». Ответ,
    /// часть которого уже ушла, остаётся отправленным.
    /// </summary>
    public async Task<int> SupersedeAsync(Guid conversationId, string reason, CancellationToken ct)
    {
        var pending = await db.TelegramOutgoingMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Author == "ai" && m.Status == TelegramOutboxStatus.Pending && m.Kind == "reply")
            .Select(m => new { m.Id, m.RelatedId }).ToListAsync(ct);
        var ids = pending.Select(p => p.Id).ToList();
        var cancelled = ids.Count == 0
            ? 0
            : await db.TelegramOutgoingMessages.Where(m => ids.Contains(m.Id) && m.Status == TelegramOutboxStatus.Pending)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, TelegramOutboxStatus.Cancelled).SetProperty(m => m.Error, reason)
                    .SetProperty(m => m.UpdatedAt, clock.GetUtcNow()), ct);

        var replyIds = pending.Where(p => p.RelatedId is not null).Select(p => p.RelatedId!.Value).Distinct().ToList();
        if (replyIds.Count > 0)
        {
            await db.DialogAgentReplies
                .Where(r => replyIds.Contains(r.Id) && (r.Status == DialogReplyStatus.Sent || r.Status == DialogReplyStatus.Approved)
                            && !db.TelegramOutgoingMessages.Any(m => m.RelatedType == RelatedType && m.RelatedId == r.Id && m.Status != TelegramOutboxStatus.Cancelled))
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, DialogReplyStatus.Superseded).SetProperty(r => r.UpdatedAt, clock.GetUtcNow()), ct);
        }

        await db.DialogAgentReplies.Where(r => r.ConversationId == conversationId && r.Status == DialogReplyStatus.Draft)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, DialogReplyStatus.Superseded).SetProperty(r => r.UpdatedAt, clock.GetUtcNow()), ct);
        return cancelled;
    }

    /// <summary>Ответов агента собеседнику за час и за сутки (отправленные, одобренные и черновики).</summary>
    public async Task<(int Hour, int Day)> CountAsync(Guid conversationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var dayAgo = now.AddDays(-1);
        var hourAgo = now.AddHours(-1);
        var times = await db.DialogAgentReplies.AsNoTracking()
            .Where(r => r.ConversationId == conversationId && r.CreatedAt >= dayAgo
                        && (r.Status == DialogReplyStatus.Sent || r.Status == DialogReplyStatus.Approved || r.Status == DialogReplyStatus.Draft))
            .Select(r => r.CreatedAt).ToListAsync(ct);
        return (times.Count(t => t >= hourAgo), times.Count);
    }

    /// <summary>Лимит ответов агента собеседнику исчерпан (за час или за сутки).</summary>
    public async Task<bool> OverLimitAsync(DialogAgent agent, Guid conversationId, CancellationToken ct)
    {
        var (hour, day) = await CountAsync(conversationId, ct);
        return hour >= agent.MaxRepliesPerHour || day >= agent.MaxRepliesPerDay;
    }

    /// <summary>Проверить ответ по ограничениям агента и отправить (или оставить черновиком / остановить).</summary>
    public async Task<DeliveryResult> DeliverAsync(AgentReplyDraft d, CancellationToken ct)
    {
        var agent = d.Agent;
        var parts = d.Template
            ? d.Messages.Select(DialogAgentRules.Plain).Where(m => m.Length > 0).ToList()
            : DialogAgentRules.Parts(d.Messages, agent.SplitMessages, agent.MaxReplyChars);
        var text = string.Join("\n\n", parts);
        var reply = new DialogAgentReply
        {
            AgentId = agent.Id,
            ConversationId = d.ConversationId,
            AccountId = d.AccountId,
            PeerId = d.PeerId,
            PeerName = Short(d.PeerName, 200),
            IncomingMessageId = d.IncomingMessageId,
            Incoming = Short(d.Incoming, 2000),
            Action = d.Action,
            Text = Short(text, 4000) ?? string.Empty,
            Model = Short(d.Model, 200),
            InputTokens = d.InputTokens,
            OutputTokens = d.OutputTokens,
            CandidateId = d.CandidateId,
            InterviewId = d.InterviewId,
        };

        if (parts.Count == 0)
        {
            reply.Status = DialogReplyStatus.Blocked;
            reply.BlockReason = "Пустой ответ.";
        }
        else if (!d.Template && DialogAgentRules.ForbiddenIn(text, agent.ForbiddenPhrases) is { } phrase)
        {
            reply.Status = DialogReplyStatus.Blocked;
            reply.BlockReason = $"В ответе запрещённая фраза «{phrase}» — ответ не отправлен.";
        }
        else if (!d.Template && agent.Mode == DialogAgentMode.Approval)
        {
            reply.Status = DialogReplyStatus.Draft;
        }
        else
        {
            reply.Status = DialogReplyStatus.Sent;
        }

        db.DialogAgentReplies.Add(reply);
        await db.SaveChangesAsync(ct);

        switch (reply.Status)
        {
            case DialogReplyStatus.Sent:
                await EnqueueAsync(reply, agent, parts, ct);
                break;
            case DialogReplyStatus.Draft:
                await NotifyAsync(agent, $"{agent.Name}: ответ ждёт проверки", $"{d.PeerName}: {Short(d.Incoming, 200)}", ct);
                break;
            case DialogReplyStatus.Blocked:
                await NotifyAsync(agent, $"{agent.Name}: ответ остановлен", reply.BlockReason, ct);
                break;
        }

        return new DeliveryResult(reply.Id, reply.Status, reply.BlockReason, parts);
    }

    /// <summary>Отправить ответ из журнала (черновик одобрен или остановленный ответ исправлен сотрудником).</summary>
    public async Task SendReviewedAsync(DialogAgentReply reply, DialogAgent agent, CancellationToken ct)
    {
        var parts = DialogAgentRules.Parts([reply.Text], agent.SplitMessages, DialogAgentRules.MaxReplyCharsLimit);
        await EnqueueAsync(reply, agent, parts, ct);
    }

    /// <summary>
    /// В очередь — сразу: части ответа стоят подряд (на миллисекунду друг за другом — порядок сохраняется), очередь отправляет их одну за другой
    /// без паузы между ними (<see cref="TelegramOutboxRunner"/>).
    /// </summary>
    private async Task EnqueueAsync(DialogAgentReply reply, DialogAgent agent, IReadOnlyList<string> parts, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        for (var i = 0; i < parts.Count; i++)
        {
            await outbox.EnqueueAsync(new OutboxRequest(reply.AccountId, reply.PeerId, reply.ConversationId, agent.Department, "reply", parts[i],
                $"{RelatedType}:{reply.Id:N}:{reply.Status}:{i}", "ai", RelatedType: RelatedType, RelatedId: reply.Id, NotBefore: now.AddMilliseconds(i)), ct);
        }
    }

    /// <summary>Уведомление тем, кто управляет агентами отдела: черновик ждёт проверки, ответ остановлен, собеседник просит человека.</summary>
    public async Task NotifyAsync(DialogAgent agent, string title, string? body, CancellationToken ct, string tab = "replies")
    {
        foreach (var user in await DialogAgentAccess.ManagersAsync(db, agent.Department, ct))
        {
            notifier.Notify(user, UserNotificationKind.AgentAlert, title, body, $"/settings/agents/{agent.Id}?tab={tab}");
        }

        await db.SaveChangesAsync(ct);
    }

    private static string? Short(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..(max - 1)] + "…";
}

/// <summary>
/// Кто управляет ИИ-агентами отдела: администратор AI — всеми; HR (ведёт подбор) — агентами HR; кто управляет Telegram — агентами продаж;
/// руководитель отдела — агентами своего отдела.
/// </summary>
public static class DialogAgentAccess
{
    public static async Task<IReadOnlySet<string>> DepartmentsAsync(IAppDbContext db, Guid userId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        if (permissions.Contains(Permissions.AiSettingsManage))
        {
            return (await db.Departments.AsNoTracking().Select(d => d.Code).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        }

        var codes = (await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.UserId == userId).Select(h => h.Department!.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        if (permissions.Contains(Permissions.HrRecruitmentManage))
        {
            codes.Add(Telegram.Routing.TelegramDepartments.Hr);
        }

        if (permissions.Contains(Permissions.TelegramManage))
        {
            codes.Add(Telegram.Routing.TelegramDepartments.Sales);
        }

        return codes;
    }

    /// <summary>Активные пользователи, которые управляют агентами отдела (для уведомлений).</summary>
    public static async Task<IReadOnlyList<Guid>> ManagersAsync(IAppDbContext db, string department, CancellationToken ct)
    {
        var users = new HashSet<Guid>(await db.ActiveUsersWithAsync(Permissions.AiSettingsManage, ct));
        if (department == Telegram.Routing.TelegramDepartments.Hr)
        {
            users.UnionWith(await db.ActiveUsersWithAsync(Permissions.HrRecruitmentManage, ct));
        }

        users.UnionWith(await db.WorkDepartmentHeads.AsNoTracking().Where(h => h.Department!.Code == department && h.User!.IsActive).Select(h => h.UserId).ToListAsync(ct));
        return users.ToList();
    }
}

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.Application.Abstractions;
using OneBase.Application.Agents;
using OneBase.Domain.AI;
using OneBase.Domain.Telegram;

namespace OneBase.AI.DialogAgents;

/// <summary>
/// ИИ-агент отдела «Помощник» (снабжение, финансы, маркетинг…): отвечает в Telegram от имени отдела по инструкции и готовым ответам из
/// «Настройки → ИИ-агенты»; чего не знает или что запрещено настройками — передаёт сотрудникам (диалог переходит к человеку, им приходит
/// уведомление). Данных OneBase не читает — только то, что ему написали в настройках. Ответ проходит ограничения и уходит с паузой.
/// </summary>
public sealed class AssistantAgent(
    IAppDbContext db,
    IAiGateway ai,
    DialogAgentService agents,
    DialogAgentDelivery delivery,
    TimeProvider clock,
    ILogger<AssistantAgent> logger) : IAssistantAgent
{
    private const int HistoryMessages = 24;

    public async Task ReplyAsync(AssistantTurn turn, CancellationToken ct)
    {
        var agent = await agents.AssistantForAsync(turn.Department, turn.AccountId, ct);
        if (agent is null)
        {
            return; // у отдела нет включённого агента — отвечают сотрудники
        }

        await delivery.SupersedeAsync(turn.ConversationId, "Собеседник написал ещё — ответ AI заменён новым", ct);
        var uzbek = DialogAgentPrompt.Uzbek(agent, turn.Text, null);
        var phrase = DialogAgentRules.HandoffIn(turn.Text, agent.HandoffPhrases);
        if (phrase is not null || Recruitment.RecruiterRules.AsksForHuman(turn.Text))
        {
            await HandoffAsync(agent, turn, phrase is null ? "Собеседник просит человека." : $"В сообщении — фраза «{phrase}» (по настройкам — сразу к сотруднику).",
                [HandoffText(agent, uzbek)], template: true, ct);
            return;
        }

        if (await delivery.OverLimitAsync(agent, turn.ConversationId, ct))
        {
            await ManualAsync(agent, turn, "Лимит ответов AI в диалоге исчерпан — нужен сотрудник.", ct);
            return;
        }

        var history = await db.TelegramConversationMessages.AsNoTracking().Where(m => m.ConversationId == turn.ConversationId)
            .OrderByDescending(m => m.SentAt).Take(HistoryMessages).OrderBy(m => m.SentAt).ToListAsync(ct);
        var answers = await db.DialogAgentAnswers.AsNoTracking().Where(a => a.AgentId == agent.Id).OrderBy(a => a.SortOrder).ToListAsync(ct);
        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, SystemPrompt(agent, answers)),
            new(LlmRole.System, $"{DialogAgentPrompt.Now(agent, clock.GetUtcNow())}\nСобеседник: {turn.PeerName ?? "без имени"}."),
        };
        foreach (var m in history)
        {
            var text = (m.Text ?? string.Empty) + (m.FileName is { } f ? $" [файл: {f}]" : string.Empty);
            if (text.Length > 0)
            {
                messages.Add(new LlmMessage(m.Direction == TelegramMessageDirection.In ? LlmRole.User : LlmRole.Assistant,
                    m.Direction == TelegramMessageDirection.In ? DialogAgentPrompt.Wrap(text) : text));
            }
        }

        if (history.Count == 0 || history[^1].Direction != TelegramMessageDirection.In)
        {
            messages.Add(new LlmMessage(LlmRole.User, DialogAgentPrompt.Wrap(turn.Text)));
        }

        AiCallResult result;
        try
        {
            result = await ai.CompleteAsync(messages, [], new AiCallOptions
            {
                Model = DialogAgentPrompt.Model(agent),
                JsonOutput = true,
                AllowToolCalls = false,
                MaxOutputTokens = 1200,
                CacheConversation = false,
                Context = new AiCallContext(null, "assistant", turn.ConversationId, $"agents.{turn.Department}.reply"),
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "ИИ-агент {Agent}: ответ модели не получен для диалога {Conversation}", agent.Id, turn.ConversationId);
            await ManualAsync(agent, turn, $"ИИ-агент не смог ответить: {ex.Message}", ct);
            return;
        }

        if (Parse(result.Text) is not { } decision)
        {
            await ManualAsync(agent, turn, "ИИ-агент не понял, как ответить (ответ модели не по формату) — нужен сотрудник.", ct);
            return;
        }

        var usage = (result.Model.ToString(), result.Usage.PromptTokens, result.Usage.OutputTokens);
        if (decision.Action == "handoff")
        {
            await HandoffAsync(agent, turn, decision.Reason ?? "Агент передал вопрос сотруднику.",
                decision.Messages.Count > 0 ? decision.Messages : [HandoffText(agent, uzbek)], template: false, ct, usage);
            return;
        }

        if (decision.Messages.Count > 0)
        {
            var sent = await Deliver(agent, turn, "reply", decision.Messages, template: false, usage, ct);
            if (sent.Status == DialogReplyStatus.Blocked)
            {
                await ManualAsync(agent, turn, sent.BlockReason ?? "Ответ остановлен ограничением.", ct, notify: false);
            }
        }
    }

    internal sealed record Decision(string Action, IReadOnlyList<string> Messages, string? Reason);

    internal static Decision? Parse(string? text)
    {
        if (JsonText.ExtractObject(text) is not { } json)
        {
            return null;
        }

        var action = JsonText.Str(json, "action")?.Trim().ToLowerInvariant();
        return action is "reply" or "handoff" ? new Decision(action, DialogAgentPrompt.Messages(json), JsonText.Str(json, "reason")) : null;
    }

    internal static string SystemPrompt(DialogAgent agent, IReadOnlyList<DialogAgentAnswer> answers)
    {
        var sb = new StringBuilder();
        sb.AppendLine(DialogAgentPrompt.Identity(agent, "сотрудник отдела компании"));
        sb.AppendLine("Ты отвечаешь в Telegram людям, которые пишут в отдел (клиенты, поставщики, партнёры, сотрудники), от имени отдела.");
        sb.AppendLine(DialogAgentPrompt.Honesty);
        sb.AppendLine(DialogAgentPrompt.Injection);
        sb.AppendLine("Данных учётной системы у тебя нет — отвечай только по инструкции отдела и готовым ответам. Остальное (статус заказа, оплаты, " +
                      "договоры, цены, сроки, если их нет ниже) — скажи, что уточнишь у коллег, и передай (action handoff).");
        sb.AppendLine("Жалоба, конфликт, срочный или денежный вопрос — вежливо скажи, что передаёшь ответственному сотруднику (action handoff).");
        sb.AppendLine();
        sb.AppendLine(DialogAgentPrompt.Style(agent));
        sb.AppendLine(DialogAgentPrompt.Restrictions(agent, "unclear"));
        if (DialogAgentPrompt.Instructions(agent) is { } instructions)
        {
            sb.AppendLine(instructions);
            sb.AppendLine();
        }

        if (DialogAgentPrompt.Answers(answers) is { } faq)
        {
            sb.AppendLine(faq);
        }

        sb.AppendLine("Ответ — только JSON без пояснений:");
        sb.AppendLine("{\"action\":\"reply|handoff\",\"messages\":[\"сообщение\",\"второе сообщение, если нужно\"],\"reason\":\"для сотрудника, кратко (при handoff)\"}");
        return sb.ToString();
    }

    private static string HandoffText(DialogAgent agent, bool uzbek) =>
        !string.IsNullOrWhiteSpace(agent.HandoffReply) ? agent.HandoffReply!
        : uzbek ? "Savolingizni mas'ul xodimga topshiraman — u shu yerda javob beradi." : "Передаю ваш вопрос ответственному сотруднику — он ответит вам здесь.";

    private Task<DeliveryResult> Deliver(DialogAgent agent, AssistantTurn turn, string action, IReadOnlyList<string> messages, bool template,
        (string Model, int Input, int Output)? usage, CancellationToken ct) =>
        delivery.DeliverAsync(new AgentReplyDraft(agent, turn.ConversationId, turn.AccountId, turn.PeerId, turn.PeerName, turn.MessageId,
            string.IsNullOrEmpty(turn.Text) && turn.FileName is { } f ? $"[файл: {f}]" : turn.Text, action, messages, template, usage?.Model, usage?.Input ?? 0,
            usage?.Output ?? 0), ct);

    private async Task HandoffAsync(DialogAgent agent, AssistantTurn turn, string reason, IReadOnlyList<string> messages, bool template, CancellationToken ct,
        (string Model, int Input, int Output)? usage = null)
    {
        await Deliver(agent, turn, "handoff", messages, template, usage, ct);
        await ManualAsync(agent, turn, reason, ct);
    }

    /// <summary>Диалог — сотруднику: AI в нём молчит, пока его не вернут; тем, кто управляет агентом, — уведомление.</summary>
    private async Task ManualAsync(DialogAgent agent, AssistantTurn turn, string reason, CancellationToken ct, bool notify = true)
    {
        await db.TelegramConversations.Where(c => c.Id == turn.ConversationId)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Mode, TelegramConversationMode.Manual).SetProperty(c => c.UpdatedAt, clock.GetUtcNow()), ct);
        if (notify)
        {
            await delivery.NotifyAsync(agent, $"{agent.Name}: нужен сотрудник", $"{turn.PeerName}: {reason}", ct, "dialogs");
        }
    }
}

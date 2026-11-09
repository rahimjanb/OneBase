using OneBase.Domain.AI;

namespace OneBase.Application.Agents;

/// <summary>
/// Кто открыл раздел и какими отделами управляет (null в Departments не бывает: пусто — ни одним). ManageAccounts — может настраивать номера
/// Telegram (право «Telegram» или «Настройки AI»): выбрать агенту любой номер.
/// </summary>
public sealed record AgentAccess(Guid UserId, IReadOnlySet<string> Departments, bool SalesTelegram, bool ManageAccounts = false)
{
    public bool Manages(string department) => Departments.Contains(department);
}

public sealed record AgentListItem(Guid Id, string Name, string Kind, bool Enabled, bool IsDefault, string Mode, string? DisplayName, int Drafts, int Blocked,
    int RepliesToday, DateTimeOffset? UpdatedAt);

/// <summary>Агент продаж у аккаунта Telegram (настраивается у аккаунта, показывается в разделе вместе с остальными).</summary>
public sealed record SalesAgentItem(Guid AccountId, string Name, string? Username, string Status, bool Enabled);

/// <summary>
/// Отдел в разделе «ИИ-агенты»: какие агенты можно создать (Kinds), агенты, агенты продаж (у продаж), сколько номеров Telegram работают на
/// отдел.
/// </summary>
public sealed record AgentDepartmentView(string Code, string Name, IReadOnlyList<string> Kinds, IReadOnlyList<AgentListItem> Agents,
    IReadOnlyList<SalesAgentItem> SalesAgents, int TelegramAccounts);

public sealed record AgentModelOption(string Provider, string Model, string Name);

public sealed record AgentAnswerView(Guid Id, string Question, string Answer, bool Enabled);

public sealed record AgentAnswerInput(string Question, string Answer, bool Enabled = true);

public sealed record AgentVacancyBrief(Guid Id, string Code, string Title, bool AiRecruiter);

/// <summary>
/// Номер Telegram для выбора у агента: состояние, работает ли он на отдел агента, кому на нём новые диалоги, занят ли агентом другого отдела,
/// можно ли его выбрать (у кого нет права настраивать номера — только номера, которые уже работают на отдел).
/// </summary>
public sealed record AgentAccountOption(Guid Id, string Name, string? Username, string? PhoneHint, string Status, bool Serves, string? DefaultDepartment,
    string? DefaultDepartmentName, string? TakenBy, bool Selectable);

public sealed record AgentView(
    Guid Id,
    string Department,
    string DepartmentName,
    string Kind,
    string Name,
    bool Enabled,
    bool IsDefault,
    string Mode,
    string? DisplayName,
    string? Intro,
    string Tone,
    string Language,
    string Length,
    bool Emoji,
    bool SplitMessages,
    string? Instructions,
    string? ScreeningQuestions,
    string? AllowedTopics,
    string? ForbiddenTopics,
    string? ForbiddenPhrases,
    string? HandoffPhrases,
    int MaxReplyChars,
    int MaxRepliesPerHour,
    int MaxRepliesPerDay,
    bool CanDiscussSalary,
    bool CanScheduleInterviews,
    string? HandoffReply,
    string? WorkFrom,
    string? WorkTo,
    string WorkDays,
    string OffHours,
    int ReplyDelayMinSeconds,
    int ReplyDelayMaxSeconds,
    bool Typing,
    int FollowUpHours,
    string? ModelProvider,
    string? Model,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<AgentAnswerView> Answers,
    IReadOnlyList<AgentModelOption> Models,
    IReadOnlyList<AgentVacancyBrief> Vacancies,
    int TelegramAccounts,
    int Drafts,
    Guid? AccountId = null,
    IReadOnlyList<AgentAccountOption>? Accounts = null,
    bool CanConfigureAccounts = false);

public sealed record AgentCreateInput(string Department, string Kind, string Name);

public sealed record AgentInput(
    string Name,
    bool Enabled,
    string Mode,
    string? DisplayName,
    string? Intro,
    string Tone,
    string Language,
    string Length,
    bool Emoji,
    bool SplitMessages,
    string? Instructions,
    string? ScreeningQuestions,
    string? AllowedTopics,
    string? ForbiddenTopics,
    string? ForbiddenPhrases,
    string? HandoffPhrases,
    int MaxReplyChars,
    int MaxRepliesPerHour,
    int MaxRepliesPerDay,
    bool CanDiscussSalary,
    bool CanScheduleInterviews,
    string? HandoffReply,
    string? WorkFrom,
    string? WorkTo,
    string WorkDays,
    string OffHours,
    int ReplyDelayMinSeconds,
    int ReplyDelayMaxSeconds,
    bool Typing,
    int FollowUpHours,
    string? ModelProvider,
    string? Model);

public sealed record AgentReplyView(Guid Id, Guid ConversationId, string? PeerName, string? Incoming, string Action, string Text, string Status, string? BlockReason,
    string? Rating, string? Correction, string? Model, DateTimeOffset CreatedAt, string? ReviewedBy, DateTimeOffset? ReviewedAt);

public sealed record AgentRepliesPage(IReadOnlyList<AgentReplyView> Items, int Drafts, int Blocked, int Bad);

public sealed record ReplyReviewInput(string? Text);

public sealed record ReplyRateInput(string? Rating, string? Correction, bool AddToAnswers);

public sealed record AgentConversationRow(Guid Id, Guid AccountId, string Name, string? Username, string Mode, string? LastText, DateTimeOffset? LastMessageAt,
    bool WaitsForAnswer);

public sealed record AgentMessageView(string Direction, string? Text, string? FileName, string Author, string? AuthorName, DateTimeOffset At, string Status, string? Error);

public sealed record AgentConversationView(Guid Id, Guid AccountId, string AccountName, string Name, string? Username, string Mode, IReadOnlyList<AgentMessageView> Messages);

public sealed record AgentSendInput(string Text, Guid ClientKey);

/// <summary>Сообщение в «Проверке» агента: роль (user — собеседник, agent — прежний ответ агента) и текст.</summary>
public sealed record AgentTestMessage(string Role, string Text);

public sealed record AgentTestInput(IReadOnlyList<AgentTestMessage> Messages, Guid? VacancyId);

/// <summary>Что ответил бы агент — без отправки: действие, сообщения, сработавшие ограничения, модель и токены.</summary>
public sealed record AgentTestResult(string Action, IReadOnlyList<string> Messages, string? HandoffReason, string? Blocked, IReadOnlyList<string> Notes, string? Model,
    int InputTokens, int OutputTokens);

/// <summary>Ход ИИ-агента отдела (не HR и не продажи): новое сообщение собеседника в диалоге, который ведёт AI.</summary>
public sealed record AssistantTurn(Guid AccountId, long PeerId, Guid ConversationId, string Department, int MessageId, string Text, string? FileName, string? PeerName,
    DateTimeOffset At);

/// <summary>ИИ-агент отдела (помощник): отвечает по инструкции и готовым ответам, сложное передаёт сотрудникам. Реализация — OneBase.AI.</summary>
public interface IAssistantAgent
{
    Task ReplyAsync(AssistantTurn turn, CancellationToken ct);
}

public static class DialogAgentLabels
{
    public static string Kind(string kind) => kind switch
    {
        DialogAgentKinds.Recruiter => "Рекрутер",
        DialogAgentKinds.Assistant => "Помощник отдела",
        _ => kind,
    };
}

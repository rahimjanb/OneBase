using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

/// <summary>Виды ИИ-агентов отделов, которые ведут переписку с людьми в Telegram.</summary>
public static class DialogAgentKinds
{
    /// <summary>Рекрутер (HR): кандидаты, вакансии, собеседования.</summary>
    public const string Recruiter = "recruiter";

    /// <summary>Помощник отдела: отвечает по инструкции и готовым ответам, сложное передаёт сотруднику.</summary>
    public const string Assistant = "assistant";

    public static readonly IReadOnlyList<string> All = [Recruiter, Assistant];
}

public enum DialogAgentMode
{
    /// <summary>Отвечает сам.</summary>
    Auto,

    /// <summary>Готовит черновик — уходит после того, как сотрудник его проверит.</summary>
    Approval,
}

/// <summary>
/// ИИ-агент отдела («Настройки → ИИ-агенты»): ведёт переписку в Telegram с кандидатами, клиентами, поставщиками — от имени отдела, по его
/// номерам Telegram. Все настройки агента — здесь: как представляется и пишет, что ему можно и нельзя, когда отвечает, готовые ответы.
/// Агент продаж (ответы сотрудникам по данным OneBase) настраивается у аккаунта Telegram и показывается в том же разделе.
/// </summary>
public class DialogAgent : Entity
{
    /// <summary>Отдел — код из <c>public.Departments</c> (hr, supply…).</summary>
    public required string Department { get; set; }

    /// <summary><see cref="DialogAgentKinds"/>.</summary>
    public required string Kind { get; set; }

    /// <summary>Название в OneBase: «Рекрутер продавцов».</summary>
    public required string Name { get; set; }

    /// <summary>Пока выключен — никому не пишет (по умолчанию выключен).</summary>
    public bool Enabled { get; set; }

    /// <summary>Агент отдела по умолчанию: ведёт диалоги, для которых не выбран другой (у рекрутера — вакансии без своего агента).</summary>
    public bool IsDefault { get; set; }

    public DialogAgentMode Mode { get; set; } = DialogAgentMode.Auto;

    /// <summary>
    /// Номер Telegram (<c>telegram.Accounts</c>), через который агент отвечает: агент ведёт только диалоги этого номера. null — любой номер
    /// отдела (если на номере нет своего агента).
    /// </summary>
    public Guid? AccountId { get; set; }

    // ---------- Как представляется и как пишет ----------

    /// <summary>Имя в переписке: «Дилноза». Пусто — без имени.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Кем представляется: «рекрутер компании Candy Gold». На прямой вопрос всё равно честно говорит, что он виртуальный помощник.</summary>
    public string? Intro { get; set; }

    /// <summary>friendly, neutral, formal.</summary>
    public string Tone { get; set; } = "friendly";

    /// <summary>auto (на языке собеседника), ru, uz.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>short, normal, detailed.</summary>
    public string Length { get; set; } = "short";

    /// <summary>Уместные эмодзи (не больше одного на сообщение).</summary>
    public bool Emoji { get; set; }

    /// <summary>Длинный ответ — несколькими короткими сообщениями, как пишут люди (до трёх).</summary>
    public bool SplitMessages { get; set; } = true;

    /// <summary>Миссия и как работать — своими словами.</summary>
    public string? Instructions { get; set; }

    /// <summary>Рекрутер: вопросы, которые он задаёт кандидату по одному (по строке на вопрос); вопросы вакансии добавляются к ним.</summary>
    public string? ScreeningQuestions { get; set; }

    // ---------- Ограничения ----------

    /// <summary>О чём можно говорить (пусто — о работе отдела).</summary>
    public string? AllowedTopics { get; set; }

    /// <summary>О чём нельзя говорить — такие вопросы агент передаёт сотруднику.</summary>
    public string? ForbiddenTopics { get; set; }

    /// <summary>Слова и фразы (по строке), которых не должно быть в ответе: такой ответ не уходит, сотрудник получает его на проверку.</summary>
    public string? ForbiddenPhrases { get; set; }

    /// <summary>Слова и фразы (по строке): сообщение с ними сразу передаётся сотруднику, агент не отвечает по существу.</summary>
    public string? HandoffPhrases { get; set; }

    /// <summary>Длина ответа — не больше, символов.</summary>
    public int MaxReplyChars { get; set; } = 700;

    /// <summary>Ответов одному собеседнику — не больше за час и за сутки; дальше — сотрудник.</summary>
    public int MaxRepliesPerHour { get; set; } = 20;

    public int MaxRepliesPerDay { get; set; } = 60;

    /// <summary>Рекрутер: можно называть вилку зарплаты из вакансии. Нет — вопросы о деньгах передаются HR.</summary>
    public bool CanDiscussSalary { get; set; } = true;

    /// <summary>Рекрутер: сам приглашает на собеседование (время — из календаря).</summary>
    public bool CanScheduleInterviews { get; set; } = true;

    /// <summary>Что сказать, передавая вопрос сотруднику; пусто — стандартная фраза.</summary>
    public string? HandoffReply { get; set; }

    // ---------- Когда и как быстро отвечает ----------

    /// <summary>Рабочее время (Ташкент); null — круглосуточно.</summary>
    public TimeOnly? WorkFrom { get; set; }

    public TimeOnly? WorkTo { get; set; }

    /// <summary>Рабочие дни: 1 — пн … 7 — вс, через запятую.</summary>
    public string WorkDays { get; set; } = "1,2,3,4,5,6,7";

    /// <summary>Вне рабочего времени: morning — ответ уходит в начале рабочего времени; reply — отвечать сразу.</summary>
    public string OffHours { get; set; } = "morning";

    /// <summary>Пауза перед ответом, секунд (случайная в этих пределах) — человек тоже не отвечает мгновенно.</summary>
    public int ReplyDelayMinSeconds { get; set; } = 20;

    public int ReplyDelayMaxSeconds { get; set; } = 90;

    /// <summary>Перед ответом показывать «печатает…».</summary>
    public bool Typing { get; set; } = true;

    /// <summary>Рекрутер: одно напоминание кандидату, который не ответил, через столько часов; 0 — не напоминать.</summary>
    public int FollowUpHours { get; set; } = 24;

    // ---------- Модель ----------

    /// <summary>Модель (провайдер и модель из «Настройки → AI»); пусто — основная.</summary>
    public string? ModelProvider { get; set; }

    public string? Model { get; set; }

    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>Готовый ответ агента: на такой вопрос — по смыслу такой ответ (утверждённый сотрудником факт или формулировка).</summary>
public class DialogAgentAnswer : Entity
{
    public Guid AgentId { get; set; }
    public required string Question { get; set; }
    public required string Answer { get; set; }
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid? CreatedById { get; set; }
}

public enum DialogReplyStatus
{
    /// <summary>Отправлен (или стоит в очереди на отправку).</summary>
    Sent,

    /// <summary>Черновик ждёт проверки сотрудником (режим «С проверкой»).</summary>
    Draft,

    /// <summary>Черновик одобрен (возможно, с правкой) и отправлен.</summary>
    Approved,

    /// <summary>Черновик отклонён — не отправлялся.</summary>
    Rejected,

    /// <summary>Ответ нарушил ограничение (запрещённая фраза) — не отправлен, ждёт сотрудника.</summary>
    Blocked,

    /// <summary>Собеседник написал ещё до отправки — ответ заменён новым.</summary>
    Superseded,
}

/// <summary>
/// Журнал ответов агента («Ответы» в настройках агента): на что отвечал, что ответил, отправлено ли, оценка и правка сотрудника. Персональные
/// данные кандидата удаляются вместе с ним; остальное — через 90 дней.
/// </summary>
public class DialogAgentReply : Entity
{
    public Guid AgentId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid AccountId { get; set; }
    public long PeerId { get; set; }
    public string? PeerName { get; set; }
    public int? IncomingMessageId { get; set; }

    /// <summary>Сообщение собеседника, на которое ответ.</summary>
    public string? Incoming { get; set; }

    /// <summary>reply, propose_interview, handoff.</summary>
    public required string Action { get; set; }

    /// <summary>Текст ответа; сообщения — через пустую строку.</summary>
    public required string Text { get; set; }

    public DialogReplyStatus Status { get; set; }

    /// <summary>Почему не отправлен (какое ограничение сработало).</summary>
    public string? BlockReason { get; set; }

    public string? Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>good, bad.</summary>
    public string? Rating { get; set; }

    /// <summary>Как надо было ответить (правка сотрудника).</summary>
    public string? Correction { get; set; }

    public Guid? ReviewedById { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Кандидат (для удаления по сроку хранения подбора).</summary>
    public Guid? CandidateId { get; set; }

    /// <summary>Предложенное собеседование (отклонённый черновик его отменяет).</summary>
    public Guid? InterviewId { get; set; }
}

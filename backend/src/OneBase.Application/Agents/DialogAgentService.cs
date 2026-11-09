using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Agents;

/// <summary>
/// «Настройки → ИИ-агенты»: агенты отделов — создать, настроить (как представляется и пишет, ограничения, рабочее время, готовые ответы),
/// журнал ответов (проверить черновик, исправить, оценить), диалоги отдела. Права — <see cref="DialogAgentAccess"/>: чужой отдел — 403.
/// </summary>
public sealed class DialogAgentService(
    IAppDbContext db,
    DialogAgentDelivery delivery,
    TelegramOutbox outbox,
    TelegramConversationService conversations,
    IAuditLogger audit,
    TimeProvider clock)
{
    public const int MaxAgentsPerDepartment = 10;
    public const int MaxAnswers = 200;

    public async Task<AgentAccess> AccessAsync(Guid userId, IReadOnlySet<string> permissions, CancellationToken ct) =>
        new(userId, await DialogAgentAccess.DepartmentsAsync(db, userId, permissions, ct), permissions.Contains(Security.Permissions.TelegramManage));

    /// <summary>Какие агенты можно создать в отделе: HR — рекрутеров; продажи — нет (агент продаж — у аккаунта Telegram); остальные — помощников.</summary>
    public static IReadOnlyList<string> KindsFor(string department) => department switch
    {
        TelegramDepartments.Hr => [DialogAgentKinds.Recruiter],
        TelegramDepartments.Sales => [],
        _ => [DialogAgentKinds.Assistant],
    };

    // ---------- Отделы и агенты ----------

    public async Task<IReadOnlyList<AgentDepartmentView>> DepartmentsAsync(AgentAccess access, CancellationToken ct)
    {
        await EnsureRecruiterAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(d => access.Departments.Contains(d.Code)).OrderBy(d => d.Name)
            .Select(d => new { d.Code, d.Name }).ToListAsync(ct);
        var codes = departments.Select(d => d.Code).ToList();
        var agents = await db.DialogAgents.AsNoTracking().Where(a => codes.Contains(a.Department)).OrderByDescending(a => a.IsDefault).ThenBy(a => a.Name).ToListAsync(ct);
        var ids = agents.Select(a => a.Id).ToList();
        var dayAgo = clock.GetUtcNow().AddDays(-1);
        var stats = await db.DialogAgentReplies.AsNoTracking().Where(r => ids.Contains(r.AgentId) && (r.Status == DialogReplyStatus.Draft || r.Status == DialogReplyStatus.Blocked || r.CreatedAt >= dayAgo))
            .GroupBy(r => r.AgentId)
            .Select(g => new
            {
                AgentId = g.Key,
                Drafts = g.Count(r => r.Status == DialogReplyStatus.Draft),
                Blocked = g.Count(r => r.Status == DialogReplyStatus.Blocked && r.ReviewedAt == null),
                Today = g.Count(r => r.CreatedAt >= dayAgo && (r.Status == DialogReplyStatus.Sent || r.Status == DialogReplyStatus.Approved)),
            })
            .ToListAsync(ct);
        var accounts = await db.TelegramAccountDepartments.AsNoTracking().Where(d => codes.Contains(d.Department)).GroupBy(d => d.Department)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        IReadOnlyList<SalesAgentItem> sales = [];
        if (access.SalesTelegram && codes.Contains(TelegramDepartments.Sales))
        {
            var salesAccounts = await db.AccountsServing(TelegramDepartments.Sales).AsNoTracking().OrderBy(a => a.Name).ToListAsync(ct);
            var accountIds = salesAccounts.Select(a => a.Id).ToList();
            var enabled = await db.TelegramAgentSettings.AsNoTracking().Where(s => accountIds.Contains(s.AccountId) && s.Enabled).Select(s => s.AccountId).ToListAsync(ct);
            sales = salesAccounts.Select(a => new SalesAgentItem(a.Id, a.Name, a.Username, a.Status.ToString(), enabled.Contains(a.Id))).ToList();
        }

        return departments.Select(d => new AgentDepartmentView(d.Code, d.Name, KindsFor(d.Code),
            agents.Where(a => a.Department == d.Code).Select(a =>
            {
                var s = stats.FirstOrDefault(x => x.AgentId == a.Id);
                return new AgentListItem(a.Id, a.Name, a.Kind, a.Enabled, a.IsDefault, a.Mode.ToString(), a.DisplayName, s?.Drafts ?? 0, s?.Blocked ?? 0, s?.Today ?? 0,
                    a.UpdatedAt ?? a.CreatedAt);
            }).ToList(),
            d.Code == TelegramDepartments.Sales ? sales : [],
            d.Code == TelegramDepartments.Sales && access.SalesTelegram ? sales.Count : accounts.FirstOrDefault(x => x.Key == d.Code)?.Count ?? 0)).ToList();
    }

    public async Task<AgentView> CreateAsync(AgentAccess access, AgentCreateInput input, CancellationToken ct)
    {
        var department = input.Department?.Trim() ?? string.Empty;
        Require(access, department);
        if (!await db.Departments.AnyAsync(d => d.Code == department, ct))
        {
            throw new FieldValidationException("Нет такого отдела.");
        }

        if (!KindsFor(department).Contains(input.Kind))
        {
            throw new FieldValidationException(department == TelegramDepartments.Sales
                ? "Агент продаж настраивается у номера Telegram: подключите номер в «Настройки → Telegram»."
                : "Такого агента в этом отделе создать нельзя.");
        }

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
        {
            throw new FieldValidationException("Название — от 1 до 100 символов.");
        }

        if (await db.DialogAgents.CountAsync(a => a.Department == department, ct) >= MaxAgentsPerDepartment)
        {
            throw new FieldValidationException($"В отделе — до {MaxAgentsPerDepartment} агентов.");
        }

        var agent = Defaults(department, input.Kind, name);
        agent.IsDefault = !await db.DialogAgents.AnyAsync(a => a.Department == department && a.Kind == input.Kind, ct);
        agent.CreatedById = access.UserId;
        db.DialogAgents.Add(agent);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, access.UserId.ToString(), "agents.created", nameof(DialogAgent), agent.Id.ToString(),
            new { department, input.Kind }, ct);
        return await GetAsync(access, agent.Id, ct);
    }

    /// <summary>Новый агент с настройками по умолчанию: выключен, пишет коротко и дружелюбно, отвечает с паузой, вне рабочего времени — утром.</summary>
    public static DialogAgent Defaults(string department, string kind, string name) => new()
    {
        Department = department,
        Kind = kind,
        Name = name,
        Enabled = false,
        WorkFrom = new TimeOnly(9, 0),
        WorkTo = new TimeOnly(19, 0),
        WorkDays = "1,2,3,4,5,6",
        ScreeningQuestions = kind == DialogAgentKinds.Recruiter
            ? "Есть ли у вас опыт работы на похожей должности? Сколько лет?\nКакую зарплату вы ожидаете?\nКогда вы готовы выйти на работу?"
            : null,
        HandoffPhrases = "жалоба\nсуд\nпрокуратура\nадвокат\nshikoyat",
    };

    public async Task<AgentView> GetAsync(AgentAccess access, Guid id, CancellationToken ct)
    {
        var agent = await AgentAsync(access, id, ct);
        var department = await db.Departments.AsNoTracking().Where(d => d.Code == agent.Department).Select(d => d.Name).FirstOrDefaultAsync(ct) ?? agent.Department;
        var answers = await db.DialogAgentAnswers.AsNoTracking().Where(a => a.AgentId == id).OrderBy(a => a.SortOrder).ThenBy(a => a.CreatedAt)
            .Select(a => new AgentAnswerView(a.Id, a.Question, a.Answer, a.Enabled)).ToListAsync(ct);
        var models = await db.AiModels.AsNoTracking().Where(m => m.Enabled && m.Available).OrderBy(m => m.ProviderCode).ThenBy(m => m.ModelId)
            .Select(m => new AgentModelOption(m.ProviderCode, m.ModelId, m.DisplayName ?? m.ModelId)).ToListAsync(ct);
        IReadOnlyList<AgentVacancyBrief> vacancies = agent.Kind == DialogAgentKinds.Recruiter
            ? (await db.Vacancies.AsNoTracking()
                .Where(v => (v.Status == VacancyStatus.Open || v.Status == VacancyStatus.Paused) && (v.AgentId == id || v.AgentId == null && agent.IsDefault))
                .OrderBy(v => v.Number).ToListAsync(ct))
                .Select(v => new AgentVacancyBrief(v.Id, v.Code, v.Title, v.AiRecruiter)).ToList()
            : [];
        var accounts = await db.TelegramAccountDepartments.CountAsync(d => d.Department == agent.Department, ct);
        var drafts = await db.DialogAgentReplies.CountAsync(r => r.AgentId == id && r.Status == DialogReplyStatus.Draft, ct);
        return new AgentView(agent.Id, agent.Department, department, agent.Kind, agent.Name, agent.Enabled, agent.IsDefault, agent.Mode.ToString(), agent.DisplayName,
            agent.Intro, agent.Tone, agent.Language, agent.Length, agent.Emoji, agent.SplitMessages, agent.Instructions, agent.ScreeningQuestions, agent.AllowedTopics,
            agent.ForbiddenTopics, agent.ForbiddenPhrases, agent.HandoffPhrases, agent.MaxReplyChars, agent.MaxRepliesPerHour, agent.MaxRepliesPerDay,
            agent.CanDiscussSalary, agent.CanScheduleInterviews, agent.HandoffReply, Time(agent.WorkFrom), Time(agent.WorkTo), agent.WorkDays, agent.OffHours,
            agent.ReplyDelayMinSeconds, agent.ReplyDelayMaxSeconds, agent.Typing, agent.FollowUpHours, agent.ModelProvider, agent.Model, agent.UpdatedAt ?? agent.CreatedAt,
            answers, models, vacancies, accounts, drafts);
    }

    private static string? Time(TimeOnly? t) => t?.ToString("HH:mm", CultureInfo.InvariantCulture);

    public async Task<AgentView> SaveAsync(AgentAccess access, Guid id, AgentInput input, CancellationToken ct)
    {
        var agent = await AgentAsync(access, id, ct, tracked: true);
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
        {
            throw new FieldValidationException("Название — от 1 до 100 символов.");
        }

        if (!Enum.TryParse<DialogAgentMode>(input.Mode, true, out var mode))
        {
            throw new FieldValidationException("Режим — «Отвечает сам» или «С проверкой».");
        }

        Check(DialogAgentRules.Tones.Contains(input.Tone), "Неизвестный тон.");
        Check(DialogAgentRules.Languages.Contains(input.Language), "Неизвестный язык.");
        Check(DialogAgentRules.Lengths.Contains(input.Length), "Неизвестная длина ответа.");
        Check(DialogAgentRules.OffHoursModes.Contains(input.OffHours), "Неизвестный режим вне рабочего времени.");
        Check(input.MaxReplyChars is >= DialogAgentRules.MinReplyChars and <= DialogAgentRules.MaxReplyCharsLimit,
            $"Длина ответа — от {DialogAgentRules.MinReplyChars} до {DialogAgentRules.MaxReplyCharsLimit} символов.");
        Check(input.MaxRepliesPerHour is >= 1 and <= 200 && input.MaxRepliesPerDay is >= 1 and <= 1000 && input.MaxRepliesPerDay >= input.MaxRepliesPerHour,
            "Лимит ответов: в час — 1–200, в сутки — 1–1000 и не меньше, чем в час.");
        Check(input.ReplyDelayMinSeconds is >= 0 and <= DialogAgentRules.MaxDelaySeconds && input.ReplyDelayMaxSeconds is >= 0 and <= DialogAgentRules.MaxDelaySeconds
              && input.ReplyDelayMaxSeconds >= input.ReplyDelayMinSeconds, $"Пауза перед ответом — 0–{DialogAgentRules.MaxDelaySeconds} секунд, «от» не больше «до».");
        Check(input.FollowUpHours is >= 0 and <= 168, "Напоминание — 0–168 часов.");

        TimeOnly? from = null, to = null;
        if (!string.IsNullOrWhiteSpace(input.WorkFrom) || !string.IsNullOrWhiteSpace(input.WorkTo))
        {
            Check(TimeOnly.TryParse(input.WorkFrom, CultureInfo.InvariantCulture, out var f) && TimeOnly.TryParse(input.WorkTo, CultureInfo.InvariantCulture, out var t) && f != t,
                "Рабочее время — начало и конец в формате 09:00 (или оба пусты — круглосуточно).");
            (from, to) = (TimeOnly.Parse(input.WorkFrom!, CultureInfo.InvariantCulture), TimeOnly.Parse(input.WorkTo!, CultureInfo.InvariantCulture));
        }

        var days = DialogAgentRules.Days(input.WorkDays);
        Check(days.Count > 0, "Выберите хотя бы один рабочий день.");

        var provider = input.ModelProvider?.Trim();
        var model = input.Model?.Trim();
        if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(model))
        {
            (provider, model) = (null, null);
        }
        else if (!await db.AiModels.AnyAsync(m => m.ProviderCode == provider && m.ModelId == model, ct))
        {
            throw new FieldValidationException($"Модели {model} нет в «Настройки → AI → Модели».");
        }

        var wasEnabled = agent.Enabled;
        agent.Name = name;
        agent.Enabled = input.Enabled;
        agent.Mode = mode;
        agent.DisplayName = Text(input.DisplayName, 60);
        agent.Intro = Text(input.Intro, 300);
        agent.Tone = input.Tone;
        agent.Language = input.Language;
        agent.Length = input.Length;
        agent.Emoji = input.Emoji;
        agent.SplitMessages = input.SplitMessages;
        agent.Instructions = Text(input.Instructions, 8000);
        agent.ScreeningQuestions = Text(input.ScreeningQuestions, 4000);
        agent.AllowedTopics = Text(input.AllowedTopics, 2000);
        agent.ForbiddenTopics = Text(input.ForbiddenTopics, 2000);
        agent.ForbiddenPhrases = Text(input.ForbiddenPhrases, 2000);
        agent.HandoffPhrases = Text(input.HandoffPhrases, 2000);
        agent.MaxReplyChars = input.MaxReplyChars;
        agent.MaxRepliesPerHour = input.MaxRepliesPerHour;
        agent.MaxRepliesPerDay = input.MaxRepliesPerDay;
        agent.CanDiscussSalary = input.CanDiscussSalary;
        agent.CanScheduleInterviews = input.CanScheduleInterviews;
        agent.HandoffReply = Text(input.HandoffReply, 500);
        agent.WorkFrom = from;
        agent.WorkTo = to;
        agent.WorkDays = string.Join(',', days.Order());
        agent.OffHours = input.OffHours;
        agent.ReplyDelayMinSeconds = input.ReplyDelayMinSeconds;
        agent.ReplyDelayMaxSeconds = input.ReplyDelayMaxSeconds;
        agent.Typing = input.Typing;
        agent.FollowUpHours = input.FollowUpHours;
        agent.ModelProvider = provider;
        agent.Model = model;
        agent.UpdatedAt = clock.GetUtcNow();
        agent.UpdatedById = access.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, access.UserId.ToString(), "agents.updated", nameof(DialogAgent), id.ToString(),
            new { wasEnabled, agent.Enabled, mode = agent.Mode.ToString(), model = agent.Model }, ct);
        return await GetAsync(access, id, ct);
    }

    public async Task DeleteAsync(AgentAccess access, Guid id, CancellationToken ct)
    {
        var agent = await AgentAsync(access, id, ct, tracked: true);
        await db.Vacancies.Where(v => v.AgentId == id).ExecuteUpdateAsync(u => u.SetProperty(v => v.AgentId, (Guid?)null), ct);
        db.DialogAgents.Remove(agent);
        await db.SaveChangesAsync(ct);
        if (agent.IsDefault && await db.DialogAgents.Where(a => a.Department == agent.Department && a.Kind == agent.Kind).OrderBy(a => a.CreatedAt).FirstOrDefaultAsync(ct) is { } next)
        {
            next.IsDefault = true;
            await db.SaveChangesAsync(ct);
        }

        await audit.LogAsync(ActorType.User, access.UserId.ToString(), "agents.deleted", nameof(DialogAgent), id.ToString(), new { agent.Department, agent.Kind }, ct);
    }

    /// <summary>Агент отдела по умолчанию: он ведёт диалоги (и вакансии), для которых не выбран другой.</summary>
    public async Task<AgentView> MakeDefaultAsync(AgentAccess access, Guid id, CancellationToken ct)
    {
        var agent = await AgentAsync(access, id, ct);
        await db.DialogAgents.Where(a => a.Department == agent.Department && a.Kind == agent.Kind)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.IsDefault, a => a.Id == id), ct);
        return await GetAsync(access, id, ct);
    }

    // ---------- Готовые ответы ----------

    public async Task<AgentView> AddAnswerAsync(AgentAccess access, Guid id, AgentAnswerInput input, CancellationToken ct)
    {
        await AgentAsync(access, id, ct);
        var (question, answer) = ValidAnswer(input);
        if (await db.DialogAgentAnswers.CountAsync(a => a.AgentId == id, ct) >= MaxAnswers)
        {
            throw new FieldValidationException($"Готовых ответов — до {MaxAnswers}.");
        }

        var order = await db.DialogAgentAnswers.Where(a => a.AgentId == id).Select(a => (int?)a.SortOrder).MaxAsync(ct) ?? 0;
        db.DialogAgentAnswers.Add(new DialogAgentAnswer { AgentId = id, Question = question, Answer = answer, Enabled = input.Enabled, SortOrder = order + 1, CreatedById = access.UserId });
        await db.SaveChangesAsync(ct);
        return await GetAsync(access, id, ct);
    }

    public async Task<AgentView> UpdateAnswerAsync(AgentAccess access, Guid id, Guid answerId, AgentAnswerInput input, CancellationToken ct)
    {
        await AgentAsync(access, id, ct);
        var row = await db.DialogAgentAnswers.FirstOrDefaultAsync(a => a.Id == answerId && a.AgentId == id, ct) ?? throw new FieldNotFoundException("Ответ не найден.");
        (row.Question, row.Answer) = ValidAnswer(input);
        row.Enabled = input.Enabled;
        row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return await GetAsync(access, id, ct);
    }

    public async Task<AgentView> DeleteAnswerAsync(AgentAccess access, Guid id, Guid answerId, CancellationToken ct)
    {
        await AgentAsync(access, id, ct);
        await db.DialogAgentAnswers.Where(a => a.Id == answerId && a.AgentId == id).ExecuteDeleteAsync(ct);
        return await GetAsync(access, id, ct);
    }

    private static (string Question, string Answer) ValidAnswer(AgentAnswerInput input)
    {
        var question = input.Question?.Trim() ?? string.Empty;
        var answer = input.Answer?.Trim() ?? string.Empty;
        if (question.Length is 0 or > 500 || answer.Length is 0 or > 2000)
        {
            throw new FieldValidationException("Вопрос — до 500 символов, ответ — до 2000, оба обязательны.");
        }

        return (question, answer);
    }

    // ---------- Журнал ответов ----------

    public async Task<AgentRepliesPage> RepliesAsync(AgentAccess access, Guid id, string? filter, CancellationToken ct)
    {
        await AgentAsync(access, id, ct);
        var query = db.DialogAgentReplies.AsNoTracking().Where(r => r.AgentId == id);
        query = filter switch
        {
            "drafts" => query.Where(r => r.Status == DialogReplyStatus.Draft),
            "blocked" => query.Where(r => r.Status == DialogReplyStatus.Blocked),
            "bad" => query.Where(r => r.Rating == "bad"),
            _ => query,
        };
        var rows = await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(ct);
        var reviewers = rows.Where(r => r.ReviewedById != null).Select(r => r.ReviewedById!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => reviewers.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var drafts = await db.DialogAgentReplies.CountAsync(r => r.AgentId == id && r.Status == DialogReplyStatus.Draft, ct);
        var blocked = await db.DialogAgentReplies.CountAsync(r => r.AgentId == id && r.Status == DialogReplyStatus.Blocked && r.ReviewedAt == null, ct);
        var bad = await db.DialogAgentReplies.CountAsync(r => r.AgentId == id && r.Rating == "bad", ct);
        return new AgentRepliesPage(rows.Select(r => new AgentReplyView(r.Id, r.ConversationId, r.PeerName, r.Incoming, r.Action, r.Text, r.Status.ToString(), r.BlockReason,
            r.Rating, r.Correction, r.Model, r.CreatedAt, r.ReviewedById is { } u ? names.GetValueOrDefault(u) : null, r.ReviewedAt)).ToList(), drafts, blocked, bad);
    }

    /// <summary>Черновик или остановленный ответ — отправить (с правкой сотрудника или как есть).</summary>
    public async Task<AgentRepliesPage> ApproveAsync(AgentAccess access, Guid replyId, ReplyReviewInput input, CancellationToken ct)
    {
        var (reply, agent) = await ReplyAsync(access, replyId, ct);
        if (reply.Status is not (DialogReplyStatus.Draft or DialogReplyStatus.Blocked))
        {
            throw new FieldConflictException("Этот ответ уже отправлен, заменён или отклонён.");
        }

        var text = input.Text?.Trim() is { Length: > 0 } edited ? edited : reply.Text;
        if (text.Length > 4000)
        {
            throw new FieldValidationException("Ответ длиннее 4000 символов.");
        }

        if (text != reply.Text)
        {
            reply.Correction = reply.Status == DialogReplyStatus.Draft ? text : reply.Correction;
            reply.Text = text;
        }

        reply.Status = DialogReplyStatus.Approved;
        reply.ReviewedById = access.UserId;
        reply.ReviewedAt = clock.GetUtcNow();
        reply.UpdatedAt = reply.ReviewedAt;
        await db.SaveChangesAsync(ct);
        await delivery.SendReviewedAsync(reply, agent, ct);
        await audit.LogAsync(ActorType.User, access.UserId.ToString(), "agents.reply.approved", nameof(DialogAgentReply), replyId.ToString(),
            new { agent = agent.Id, edited = reply.Correction is not null }, ct);
        return await RepliesAsync(access, agent.Id, null, ct);
    }

    /// <summary>Черновик или остановленный ответ — не отправлять (предложенное собеседование отменяется).</summary>
    public async Task<AgentRepliesPage> RejectAsync(AgentAccess access, Guid replyId, CancellationToken ct)
    {
        var (reply, agent) = await ReplyAsync(access, replyId, ct);
        if (reply.Status is not (DialogReplyStatus.Draft or DialogReplyStatus.Blocked))
        {
            throw new FieldConflictException("Этот ответ уже отправлен, заменён или отклонён.");
        }

        reply.Status = DialogReplyStatus.Rejected;
        reply.ReviewedById = access.UserId;
        reply.ReviewedAt = clock.GetUtcNow();
        if (reply.InterviewId is { } interviewId)
        {
            await db.Interviews.Where(i => i.Id == interviewId && i.Status == InterviewStatus.Proposed)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, InterviewStatus.Cancelled), ct);
        }

        await db.SaveChangesAsync(ct);
        return await RepliesAsync(access, agent.Id, null, ct);
    }

    /// <summary>Оценка ответа и правка «как надо было» — по желанию сразу в готовые ответы агента (дальше он отвечает так).</summary>
    public async Task<AgentRepliesPage> RateAsync(AgentAccess access, Guid replyId, ReplyRateInput input, CancellationToken ct)
    {
        var (reply, agent) = await ReplyAsync(access, replyId, ct);
        if (input.Rating is not (null or "good" or "bad"))
        {
            throw new FieldValidationException("Оценка — «хорошо» или «плохо».");
        }

        reply.Rating = input.Rating;
        reply.Correction = input.Correction?.Trim() is { Length: > 0 } c ? (c.Length <= 2000 ? c : c[..2000]) : null;
        reply.ReviewedById = access.UserId;
        reply.ReviewedAt = clock.GetUtcNow();
        if (input.AddToAnswers)
        {
            if (reply.Correction is null || string.IsNullOrWhiteSpace(reply.Incoming))
            {
                throw new FieldValidationException("Чтобы добавить в готовые ответы, напишите, как надо было ответить.");
            }

            var order = await db.DialogAgentAnswers.Where(a => a.AgentId == agent.Id).Select(a => (int?)a.SortOrder).MaxAsync(ct) ?? 0;
            db.DialogAgentAnswers.Add(new DialogAgentAnswer
            {
                AgentId = agent.Id,
                Question = reply.Incoming!.Length <= 500 ? reply.Incoming : reply.Incoming[..500],
                Answer = reply.Correction,
                SortOrder = order + 1,
                CreatedById = access.UserId,
            });
        }

        await db.SaveChangesAsync(ct);
        return await RepliesAsync(access, agent.Id, null, ct);
    }

    private async Task<(DialogAgentReply Reply, DialogAgent Agent)> ReplyAsync(AgentAccess access, Guid replyId, CancellationToken ct)
    {
        var reply = await db.DialogAgentReplies.FirstOrDefaultAsync(r => r.Id == replyId, ct) ?? throw new FieldNotFoundException("Ответ не найден.");
        var agent = await AgentAsync(access, reply.AgentId, ct);
        return (reply, agent);
    }

    // ---------- Диалоги отдела (помощник) ----------

    public async Task<IReadOnlyList<AgentConversationRow>> ConversationsAsync(AgentAccess access, Guid id, CancellationToken ct)
    {
        var agent = await AgentAsync(access, id, ct);
        var rows = await (from c in db.TelegramConversations.AsNoTracking()
                          where c.Department == agent.Department && c.ChatType == TelegramChatType.Private
                          join chat in db.TelegramChats.AsNoTracking() on new { c.AccountId, c.PeerId } equals new { chat.AccountId, chat.PeerId } into chats
                          from chat in chats.DefaultIfEmpty()
                          orderby c.LastMessageAt descending
                          select new { c, Title = chat != null ? chat.Title : null, Username = chat != null ? chat.Username : null })
            .Take(300).ToListAsync(ct);
        var ids = rows.Select(r => r.c.Id).ToList();
        var last = await db.TelegramConversationMessages.AsNoTracking().Where(m => ids.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId).Select(g => g.OrderByDescending(m => m.SentAt).Select(m => new { m.ConversationId, m.Text, m.Direction }).First()).ToListAsync(ct);
        return rows.Select(r =>
        {
            var l = last.FirstOrDefault(x => x.ConversationId == r.c.Id);
            return new AgentConversationRow(r.c.Id, r.c.AccountId, r.Title ?? r.c.PeerId.ToString(CultureInfo.InvariantCulture), r.Username, r.c.Mode.ToString(), l?.Text,
                r.c.LastMessageAt, l?.Direction == TelegramMessageDirection.In);
        }).ToList();
    }

    public async Task<AgentConversationView> ConversationAsync(AgentAccess access, Guid id, Guid conversationId, CancellationToken ct)
    {
        var agent = await AgentAsync(access, id, ct);
        var c = await db.TelegramConversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == conversationId && x.Department == agent.Department, ct)
                ?? throw new FieldNotFoundException("Диалог не найден.");
        var account = await db.TelegramAccounts.AsNoTracking().Where(a => a.Id == c.AccountId).Select(a => a.Name).FirstAsync(ct);
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == c.AccountId && x.PeerId == c.PeerId, ct);
        var messages = await db.TelegramConversationMessages.AsNoTracking().Where(m => m.ConversationId == conversationId).OrderByDescending(m => m.SentAt).Take(300).ToListAsync(ct);
        var pending = await db.TelegramOutgoingMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Status != TelegramOutboxStatus.Sent).OrderByDescending(m => m.CreatedAt).Take(50).ToListAsync(ct);
        var userIds = messages.Select(m => m.AuthorUserId).Concat(pending.Select(p => p.CreatedById)).Where(u => u != null).Select(u => u!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var list = messages.Select(m => new AgentMessageView(m.Direction.ToString(), m.Text, m.FileName, m.Author, m.AuthorUserId is { } u ? names.GetValueOrDefault(u) : null,
                m.SentAt, "Sent", null))
            .Concat(pending.Select(p => new AgentMessageView("Out", p.Text, null, p.Author, p.CreatedById is { } u ? names.GetValueOrDefault(u) : null,
                p.NextAttemptAt > p.CreatedAt ? p.NextAttemptAt : p.CreatedAt, p.Status.ToString(), p.Error)))
            .OrderBy(m => m.At).ToList();
        return new AgentConversationView(c.Id, c.AccountId, account, chat?.Title ?? c.PeerId.ToString(CultureInfo.InvariantCulture), chat?.Username, c.Mode.ToString(), list);
    }

    /// <summary>Ответ сотрудника из OneBase — через очередь исходящих отдела; сотрудник взял диалог — AI в нём молчит, неотправленный ответ AI отменяется.</summary>
    public async Task<AgentConversationView> SendAsync(AgentAccess access, Guid id, Guid conversationId, AgentSendInput input, CancellationToken ct)
    {
        var agent = await AgentAsync(access, id, ct);
        var c = await db.TelegramConversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == conversationId && x.Department == agent.Department, ct)
                ?? throw new FieldNotFoundException("Диалог не найден.");
        await delivery.SupersedeAsync(c.Id, "Ответил сотрудник", ct);
        await outbox.EnqueueAsync(new OutboxRequest(c.AccountId, c.PeerId, c.Id, agent.Department, "manual", input.Text ?? string.Empty,
            $"manual:{input.ClientKey:N}", "user", access.UserId), ct);
        if (c.Mode == TelegramConversationMode.Ai)
        {
            await conversations.SetModeAsync(c.AccountId, c.PeerId, TelegramConversationMode.Manual, access.UserId, ct);
        }

        return await ConversationAsync(access, id, conversationId, ct);
    }

    public async Task<AgentConversationView> SetModeAsync(AgentAccess access, Guid id, Guid conversationId, TelegramConversationMode mode, CancellationToken ct)
    {
        var agent = await AgentAsync(access, id, ct);
        var c = await db.TelegramConversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == conversationId && x.Department == agent.Department, ct)
                ?? throw new FieldNotFoundException("Диалог не найден.");
        if (mode == TelegramConversationMode.Manual)
        {
            await delivery.SupersedeAsync(c.Id, "Диалог взял сотрудник", ct);
        }

        await conversations.SetModeAsync(c.AccountId, c.PeerId, mode, access.UserId, ct);
        return await ConversationAsync(access, id, conversationId, ct);
    }

    // ---------- Для агентов ----------

    /// <summary>
    /// Рекрутер HR по умолчанию — создаётся один раз из прежних настроек подбора (включён ли AI-рекрутер, как представлялся, напоминание),
    /// чтобы переход на «ИИ-агенты» ничего не выключил и не включил.
    /// </summary>
    public async Task<DialogAgent> EnsureRecruiterAsync(CancellationToken ct)
    {
        if (await db.DialogAgents.FirstOrDefaultAsync(a => a.Department == TelegramDepartments.Hr && a.Kind == DialogAgentKinds.Recruiter && a.IsDefault, ct) is { } existing)
        {
            return existing;
        }

        if (await db.DialogAgents.Where(a => a.Department == TelegramDepartments.Hr && a.Kind == DialogAgentKinds.Recruiter).OrderBy(a => a.CreatedAt).FirstOrDefaultAsync(ct) is { } first)
        {
            first.IsDefault = true;
            await db.SaveChangesAsync(ct);
            return first;
        }

        var settings = await db.RecruitmentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? new RecruitmentSettings();
        var agent = Defaults(TelegramDepartments.Hr, DialogAgentKinds.Recruiter, "AI-рекрутер");
        agent.IsDefault = true;
        agent.Enabled = settings.AiEnabled;
        agent.Intro = settings.AssistantName;
        agent.FollowUpHours = settings.FollowUpHours;
        db.DialogAgents.Add(agent);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DialogAgents.Remove(agent);
            return await db.DialogAgents.FirstAsync(a => a.Department == TelegramDepartments.Hr && a.Kind == DialogAgentKinds.Recruiter, ct);
        }

        return agent;
    }

    /// <summary>Рекрутер вакансии: свой агент вакансии, иначе рекрутер HR по умолчанию. null — нет ни одного.</summary>
    public async Task<DialogAgent?> RecruiterForAsync(Guid? agentId, CancellationToken ct)
    {
        if (agentId is { } id && await db.DialogAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.Kind == DialogAgentKinds.Recruiter, ct) is { } own)
        {
            return own;
        }

        await EnsureRecruiterAsync(ct);
        return await db.DialogAgents.AsNoTracking()
            .Where(a => a.Department == TelegramDepartments.Hr && a.Kind == DialogAgentKinds.Recruiter).OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>ИИ-рекрутеры HR — для выбора у вакансии (подбор видит их без прав на настройки агентов).</summary>
    public async Task<IReadOnlyList<AgentListItem>> RecruitersAsync(CancellationToken ct)
    {
        await EnsureRecruiterAsync(ct);
        return await db.DialogAgents.AsNoTracking().Where(a => a.Department == TelegramDepartments.Hr && a.Kind == DialogAgentKinds.Recruiter)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Name)
            .Select(a => new AgentListItem(a.Id, a.Name, a.Kind, a.Enabled, a.IsDefault, a.Mode.ToString(), a.DisplayName, 0, 0, 0, a.UpdatedAt ?? a.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Помощник отдела: агент по умолчанию, если включён, иначе первый включённый. null — отдел отвечает сам.</summary>
    public async Task<DialogAgent?> AssistantForAsync(string department, CancellationToken ct) =>
        await db.DialogAgents.AsNoTracking().Where(a => a.Department == department && a.Kind == DialogAgentKinds.Assistant && a.Enabled)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt).FirstOrDefaultAsync(ct);

    private async Task<DialogAgent> AgentAsync(AgentAccess access, Guid id, CancellationToken ct, bool tracked = false)
    {
        var query = tracked ? db.DialogAgents : db.DialogAgents.AsNoTracking();
        var agent = await query.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new FieldNotFoundException("Агент не найден.");
        Require(access, agent.Department);
        return agent;
    }

    private static void Require(AgentAccess access, string department)
    {
        if (!access.Manages(department))
        {
            throw new FieldForbiddenException("Агентами этого отдела управляют его руководитель и администратор AI.");
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok)
        {
            throw new FieldValidationException(message);
        }
    }

    private static string? Text(string? text, int max) => text?.Trim() is { Length: > 0 } t ? (t.Length <= max ? t : t[..max]) : null;
}

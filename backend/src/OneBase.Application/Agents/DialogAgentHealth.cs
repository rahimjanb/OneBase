using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.AI;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Agents;

/// <summary>Проверка «почему агент может не отвечать»: ok, info, warn (ответ задержится или придёт не всем), bad (агент не ответит).</summary>
public sealed record AgentCheck(string Status, string Title, string? Detail = null);

/// <summary>
/// Последний диалог на номере агента: кто (имя — только у диалогов отдела агента или для администратора Telegram), когда, у какого отдела и
/// почему, режим и что было с ответом.
/// </summary>
public sealed record AgentHealthDialog(Guid Id, string Name, DateTimeOffset? At, string AccountName, string Department, bool Ours, string Reason, string Mode,
    string Outcome);

public sealed record AgentHealthView(IReadOnlyList<AgentCheck> Checks, IReadOnlyList<AgentHealthDialog> Dialogs, DateTimeOffset CheckedAt);

/// <summary>Настройки сервера, которые влияют на ответы агентов (читает контроллер из конфигурации).</summary>
public sealed record AgentServerFlags(bool TelegramEnabled, bool RoutingEnabled);

/// <summary>
/// «Почему агент может не отвечать» на странице агента: включён ли, номер и его подключение, кому на номере новые диалоги, пауза Telegram,
/// рабочее время, режим «С проверкой», вакансии (у рекрутера), очередь отправки и ошибки, последние диалоги номера — куда ушли и почему.
/// Только чтение, без вызова модели.
/// </summary>
public sealed class DialogAgentHealth(IAppDbContext db, DialogAgentService agents, TimeProvider clock)
{
    private const int DialogsShown = 10;
    private static readonly TimeSpan Tashkent = DialogAgentRules.Tashkent;

    public async Task<AgentHealthView> GetAsync(AgentAccess access, Guid id, AgentServerFlags flags, CancellationToken ct)
    {
        var agent = await agents.AgentAsync(access, id, ct);
        var now = clock.GetUtcNow();
        var names = await db.Departments.AsNoTracking().Select(d => new { d.Code, d.Name }).ToDictionaryAsync(d => d.Code, d => d.Name, ct);
        string Dept(string? code) => code is null ? "не распределён" : names.GetValueOrDefault(code, code);
        var checks = new List<AgentCheck>();

        checks.Add(agent.Enabled
            ? new AgentCheck("ok", "Агент включён")
            : new AgentCheck("bad", "Агент выключен — никому не пишет", "Отметьте «Агент включён» ниже и сохраните."));

        if (!flags.TelegramEnabled)
        {
            checks.Add(new AgentCheck("bad", "Telegram на этом сервере выключен", "Настройка сервера Telegram:Enabled=false: сообщения не принимаются и не отправляются."));
        }

        if (!flags.RoutingEnabled)
        {
            checks.Add(new AgentCheck("bad", "Распределение диалогов Telegram выключено",
                "Настройка сервера Telegram:Routing:Enabled=false: все сообщения уходят агенту продаж, агенты отделов их не получают."));
        }

        // Номера агента: выбранный у него или номера отдела, на которых нет своего агента того же вида.
        var rows = await db.TelegramAccountDepartments.AsNoTracking().ToListAsync(ct);
        List<Guid> numberIds;
        if (agent.AccountId is { } own)
        {
            numberIds = [own];
        }
        else
        {
            var boundElsewhere = await db.DialogAgents.AsNoTracking()
                .Where(a => a.Department == agent.Department && a.Kind == agent.Kind && a.Id != agent.Id && a.AccountId != null)
                .Select(a => a.AccountId!.Value).ToListAsync(ct);
            numberIds = rows.Where(r => r.Department == agent.Department && !boundElsewhere.Contains(r.AccountId)).Select(r => r.AccountId).Distinct().ToList();
        }

        var numbers = await db.TelegramAccounts.AsNoTracking().Where(a => numberIds.Contains(a.Id)).OrderBy(a => a.Name).ToListAsync(ct);
        if (numbers.Count == 0)
        {
            checks.Add(new AgentCheck("bad", "Нет номера Telegram",
                $"Ни один номер не работает на отдел «{Dept(agent.Department)}». Выберите номер в блоке «Номер Telegram» — агент начнёт отвечать на сообщения, пришедшие на него."));
        }

        foreach (var number in numbers)
        {
            checks.Add(number.Status == TelegramAccountStatus.Connected
                ? new AgentCheck("ok", $"Номер «{number.Name}» подключён",
                    number.LastActivityAt is { } seen ? $"Последнее событие Telegram — {Local(seen)}." : null)
                : new AgentCheck("bad", $"Номер «{number.Name}» не подключён ({StatusText(number.Status)})",
                    $"{(number.LastError is { } error ? error + ". " : string.Empty)}Сообщения на него не принимаются и не отправляются — переподключите в «Настройки → Telegram»."));

            var served = rows.Where(r => r.AccountId == number.Id).ToList();
            var mine = served.FirstOrDefault(r => r.Department == agent.Department);
            var fallback = served.Count == 0 ? TelegramDepartments.Sales : served.FirstOrDefault(r => r.IsDefault)?.Department;
            if (mine is null)
            {
                checks.Add(new AgentCheck("bad", $"Номер «{number.Name}» не работает на отдел «{Dept(agent.Department)}»",
                    $"Сообщения с него уходят {(served.Count == 0 ? "агенту продаж" : $"отделу «{Dept(fallback)}»")}. Выберите номер у агента ещё раз (нужно право настраивать номера) " +
                    "или отметьте отдел в «Настройки → Telegram → Подразделения»."));
                continue;
            }

            checks.Add(fallback == agent.Department
                ? new AgentCheck("ok", $"Новые диалоги на номере «{number.Name}» идут агенту")
                : new AgentCheck("warn", $"Новые диалоги на номере «{number.Name}» идут {(fallback is null ? "никому (не распределены)" : $"отделу «{Dept(fallback)}»")}",
                    agent.Kind == DialogAgentKinds.Recruiter
                        ? "Рекрутеру попадают только сообщения с кодом вакансии (#V12) и ответы на публикации вакансий. Выберите номер у агента ещё раз — новые диалоги пойдут ему."
                        : "Агенту попадают только диалоги, которые сотрудник передал отделу. Выберите номер у агента ещё раз — новые диалоги пойдут ему."));

            if (mine.PausedUntil is { } paused && paused > now)
            {
                checks.Add(new AgentCheck("bad", $"Telegram ограничил отправку с номера «{number.Name}» до {Local(paused)}", mine.PauseReason));
            }

            if (served.Any(r => r.Department == TelegramDepartments.Sales)
                && await db.TelegramUserPermissions.CountAsync(p => p.AccountId == number.Id, ct) is var salesUsers and > 0)
            {
                checks.Add(new AgentCheck("info", $"Люди из списка агента продаж ({salesUsers}) на номере «{number.Name}» остаются у продаж",
                    "Если проверяете агента со своего Telegram, а он есть в этом списке, — ответит агент продаж. Напишите с другого аккаунта."));
            }
        }

        // Когда отвечает.
        var pause = agent.ReplyDelayMaxSeconds > 0 ? $" Пауза перед ответом — {agent.ReplyDelayMinSeconds}–{agent.ReplyDelayMaxSeconds} с." : string.Empty;
        if (agent.WorkFrom is null || agent.WorkTo is null)
        {
            checks.Add(new AgentCheck("ok", "Отвечает круглосуточно", pause.Trim()));
        }
        else if (DialogAgentRules.InWork(agent, now))
        {
            checks.Add(new AgentCheck("ok", $"Сейчас рабочее время агента ({WorkTime(agent)})", pause.Trim()));
        }
        else if (agent.OffHours == "morning")
        {
            checks.Add(new AgentCheck("warn", $"Сейчас нерабочее время ({WorkTime(agent)})",
                $"Ответы уйдут в начале рабочего дня — {Local(DialogAgentRules.NextWorkStart(agent, now))}.{pause}"));
        }
        else
        {
            checks.Add(new AgentCheck("ok", "Нерабочее время, но агент отвечает сразу", pause.Trim()));
        }

        if (agent.Mode == DialogAgentMode.Approval)
        {
            var drafts = await db.DialogAgentReplies.CountAsync(r => r.AgentId == agent.Id && r.Status == DialogReplyStatus.Draft, ct);
            checks.Add(new AgentCheck("warn", "Режим «С проверкой»: ответы ждут одобрения сотрудника", $"Ждут проверки: {drafts} — вкладка «Ответы»."));
        }

        var blocked = await db.DialogAgentReplies.CountAsync(r => r.AgentId == agent.Id && r.Status == DialogReplyStatus.Blocked && r.ReviewedAt == null, ct);
        if (blocked > 0)
        {
            checks.Add(new AgentCheck("warn", $"Остановлено ограничениями: {blocked}", "Эти ответы не ушли — вкладка «Ответы», фильтр «Остановлены»."));
        }

        if (agent.Kind == DialogAgentKinds.Recruiter)
        {
            var open = await db.Vacancies.AsNoTracking().Where(v => v.Status == VacancyStatus.Open).OrderBy(v => v.Number)
                .Select(v => new { v.Number, v.Title }).ToListAsync(ct);
            checks.Add(open.Count > 0
                ? new AgentCheck("ok", $"Знает открытые вакансии OneBase: {open.Count}",
                    string.Join(", ", open.Take(12).Select(v => $"V{v.Number} «{v.Title}»")) + (open.Count > 12 ? $" и ещё {open.Count - 12}" : string.Empty))
                : new AgentCheck("warn", "Открытых вакансий нет", "Агент ответит кандидату, что сейчас набора нет. Откройте вакансию в HR → «Подбор»."));

            var weekAgo = now.AddDays(-7);
            var error = await db.RecruitmentExceptions.AsNoTracking()
                .Where(e => e.Kind == "technical" && e.Status == RecruitmentExceptionStatus.Open && e.CreatedAt >= weekAgo)
                .OrderByDescending(e => e.CreatedAt).Select(e => new { e.Message, e.CreatedAt }).FirstOrDefaultAsync(ct);
            if (error is not null)
            {
                checks.Add(new AgentCheck("bad", $"Ошибка ответа AI — {Local(error.CreatedAt)}", error.Message));
            }
        }

        // Отправка: очередь и неудачи за сутки.
        var dayAgo = now.AddDays(-1);
        var outgoing = await db.TelegramOutgoingMessages.AsNoTracking()
            .Where(m => numberIds.Contains(m.AccountId) && m.Department == agent.Department && m.Author == "ai"
                        && (m.Status == TelegramOutboxStatus.Pending || m.Status == TelegramOutboxStatus.Sending
                            || (m.Status == TelegramOutboxStatus.Failed || m.Status == TelegramOutboxStatus.Unknown) && m.CreatedAt >= dayAgo))
            .Select(m => new { m.Status, m.NextAttemptAt, m.Error, m.CreatedAt }).ToListAsync(ct);
        var pending = outgoing.Where(m => m.Status is TelegramOutboxStatus.Pending or TelegramOutboxStatus.Sending).ToList();
        if (pending.Count > 0)
        {
            checks.Add(new AgentCheck("info", $"В очереди на отправку: {pending.Count}", $"Ближайшее — {Local(pending.Min(m => m.NextAttemptAt))}."));
        }

        var failed = outgoing.Where(m => m.Status is TelegramOutboxStatus.Failed or TelegramOutboxStatus.Unknown).OrderByDescending(m => m.CreatedAt).ToList();
        if (failed.Count > 0)
        {
            checks.Add(new AgentCheck("bad", $"Не отправлено за сутки: {failed.Count}", failed[0].Error));
        }

        return new AgentHealthView(checks, await DialogsAsync(access, agent, numbers, Dept, ct), now);
    }

    /// <summary>Последние диалоги номеров агента — куда ушли и почему, что с ответом.</summary>
    private async Task<IReadOnlyList<AgentHealthDialog>> DialogsAsync(AgentAccess access, DialogAgent agent, IReadOnlyList<TelegramAccount> numbers,
        Func<string?, string> dept, CancellationToken ct)
    {
        var ids = numbers.Select(n => n.Id).ToList();
        var conversations = await db.TelegramConversations.AsNoTracking().Where(c => ids.Contains(c.AccountId) && c.ChatType == TelegramChatType.Private)
            .OrderByDescending(c => c.LastInboundAt ?? c.LastMessageAt).Take(DialogsShown).ToListAsync(ct);
        var keys = conversations.Select(c => c.Id).ToList();
        var peers = conversations.Select(c => c.PeerId).ToList();
        var chats = await db.TelegramChats.AsNoTracking().Where(c => ids.Contains(c.AccountId) && peers.Contains(c.PeerId))
            .Select(c => new { c.AccountId, c.PeerId, c.Title, c.Username }).ToListAsync(ct);
        var replies = await db.DialogAgentReplies.AsNoTracking().Where(r => keys.Contains(r.ConversationId))
            .GroupBy(r => r.ConversationId).Select(g => g.OrderByDescending(r => r.CreatedAt).Select(r => new { r.ConversationId, r.Status, r.CreatedAt }).First())
            .ToListAsync(ct);
        var queued = await db.TelegramOutgoingMessages.AsNoTracking()
            .Where(m => m.ConversationId != null && keys.Contains(m.ConversationId.Value) && m.Author == "ai" && m.Status == TelegramOutboxStatus.Pending)
            .GroupBy(m => m.ConversationId!.Value).Select(g => new { Id = g.Key, At = g.Min(m => m.NextAttemptAt) }).ToListAsync(ct);

        return conversations.Select(c =>
        {
            var ours = c.Department == agent.Department;
            var chat = chats.FirstOrDefault(x => x.AccountId == c.AccountId && x.PeerId == c.PeerId);
            var name = ours || access.ManageAccounts
                ? chat?.Title ?? (chat?.Username is { } u ? "@" + u : $"Telegram {c.PeerId}")
                : $"Собеседник …{c.PeerId.ToString(CultureInfo.InvariantCulture)[^Math.Min(4, c.PeerId.ToString(CultureInfo.InvariantCulture).Length)..]}";
            var reply = replies.FirstOrDefault(r => r.ConversationId == c.Id);
            var wait = queued.FirstOrDefault(q => q.Id == c.Id);
            var outcome = !ours ? $"отвечает отдел «{dept(c.Department)}»"
                : c.Status != TelegramConversationStatus.Active ? "диалог закрыт"
                : wait is not null ? $"ответ AI в очереди — уйдёт {Local(wait.At)}"
                : reply is not null ? reply.Status switch
                {
                    DialogReplyStatus.Sent or DialogReplyStatus.Approved => $"AI ответил — {Local(reply.CreatedAt)}",
                    DialogReplyStatus.Draft => "черновик AI ждёт проверки",
                    DialogReplyStatus.Blocked => "ответ AI остановлен ограничением",
                    DialogReplyStatus.Rejected => "черновик AI отклонён",
                    _ => "ответ AI заменён новым",
                }
                : c.Mode == TelegramConversationMode.Manual ? "ведёт сотрудник — AI молчит"
                : agent.Enabled ? "AI ещё не отвечал" : "AI не отвечал: агент выключен";
            return new AgentHealthDialog(c.Id, name, c.LastInboundAt ?? c.LastMessageAt, numbers.First(n => n.Id == c.AccountId).Name, dept(c.Department), ours,
                Reason(c.RouteReason), c.Mode.ToString(), outcome);
        }).ToList();
    }

    private static string Reason(string reason) => reason switch
    {
        "default" => "новые диалоги номера идут этому отделу",
        "claim" => "по первому сообщению (код вакансии или ответ на публикацию)",
        "conflict" => "в первом сообщении признаки двух отделов — решает сотрудник",
        "sales_user" => "человек из списка агента продаж",
        "no_default" => "у номера нет отдела для новых диалогов",
        "manual" => "назначил сотрудник",
        DialogAgentAccounts.AgentAccountReason => "номер выбран у агента",
        _ => reason,
    };

    private static string StatusText(TelegramAccountStatus status) => status switch
    {
        TelegramAccountStatus.New => "вход не выполнен",
        TelegramAccountStatus.CodeRequired => "ждёт код",
        TelegramAccountStatus.PasswordRequired => "ждёт пароль",
        TelegramAccountStatus.Disconnected => "отключён",
        TelegramAccountStatus.Error => "ошибка подключения",
        _ => status.ToString(),
    };

    private static string WorkTime(DialogAgent agent)
    {
        string[] days = ["пн", "вт", "ср", "чт", "пт", "сб", "вс"];
        var list = DialogAgentRules.Days(agent.WorkDays).Order().ToList();
        var range = list.Count == 7 ? "ежедневно"
            : list.Count > 1 && list[^1] - list[0] == list.Count - 1 ? $"{days[list[0] - 1]}–{days[list[^1] - 1]}"
            : string.Join(", ", list.Select(d => days[d - 1]));
        return $"{agent.WorkFrom:HH\\:mm}–{agent.WorkTo:HH\\:mm}, {range}";
    }

    private static string Local(DateTimeOffset at) => at.ToOffset(Tashkent).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
}

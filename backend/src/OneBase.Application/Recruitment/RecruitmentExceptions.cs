using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Domain.Work;

namespace OneBase.Application.Recruitment;

public sealed record ExceptionView(Guid Id, string Kind, string Message, Guid? CandidateId, string? CandidateName, Guid? VacancyId, string? VacancyCode,
    Guid? ConversationId, string Status, DateTimeOffset CreatedAt, string? ResolvedBy, DateTimeOffset? ResolvedAt, string? Resolution);

public sealed record ResolveInput(string? Resolution, bool ReturnToAi);

/// <summary>
/// Центр исключений подбора: всё, где AI или служба остановилась и ждёт человека. Открытое исключение с тем же ключом не дублируется; HR
/// получает уведомление. Исключение по диалогу переводит только этот диалог на человека — остальные работают.
/// </summary>
public sealed class RecruitmentExceptions(IAppDbContext db, UserNotifier notifier, IAuditLogger audit, TimeProvider clock)
{
    public static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>
    {
        ["human_requested"] = "Кандидат просит человека",
        ["salary"] = "Торг о зарплате",
        ["conditions"] = "Спорные условия",
        ["unclear"] = "Непонятный ответ",
        ["technical"] = "Техническая ошибка",
        ["telegram"] = "Проблема Telegram",
        ["hh"] = "Проблема HH.uz",
        ["schedule"] = "Конфликт расписания",
        ["duplicate"] = "Возможный дубль",
        ["send_failed"] = "Сообщение не отправлено",
        ["review"] = "Нужно решение HR",
        ["blocked"] = "Ответ AI остановлен ограничением",
    };

    /// <summary>Поднять исключение (или вернуть уже открытое с тем же ключом). Диалог — на человека.</summary>
    public async Task<Guid> RaiseAsync(string kind, string message, Guid? candidateId = null, Guid? vacancyId = null, Guid? conversationId = null,
        string? dedupKey = null, CancellationToken ct = default)
    {
        if (dedupKey is not null && await db.RecruitmentExceptions.AsNoTracking()
                .Where(e => e.DedupKey == dedupKey && e.Status == RecruitmentExceptionStatus.Open).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct) is { } open)
        {
            return open;
        }

        var now = clock.GetUtcNow();
        var row = new RecruitmentException
        {
            Kind = Kinds.ContainsKey(kind) ? kind : "technical",
            Message = message.Length <= 2000 ? message : message[..2000],
            CandidateId = candidateId,
            VacancyId = vacancyId,
            ConversationId = conversationId,
            DedupKey = dedupKey,
            CreatedAt = now,
        };
        db.RecruitmentExceptions.Add(row);
        var (title, body, link) = ($"Подбор: {Kinds.GetValueOrDefault(row.Kind, row.Kind)}", row.Message, "/hr/recruitment/exceptions");
        if (conversationId is { } c)
        {
            // Диалог — на человека; с этой минуты идёт час: HR не ответит — диалог вернётся к AI (HrWaiting).
            await db.TelegramConversations.Where(x => x.Id == c)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Mode, TelegramConversationMode.Manual).SetProperty(x => x.UpdatedAt, now), ct);
            var name = candidateId is { } cid ? await db.Candidates.AsNoTracking().Where(x => x.Id == cid).Select(x => x.FullName).FirstOrDefaultAsync(ct) : null;
            var waiting = await Telegram.HrWaiting.CountAsync(db, c, ct) + 1;
            (title, body, link) = ($"{name ?? "Кандидат"} ждёт ответа HR · {Telegram.HrDialogService.Reason(row.Kind)}",
                $"{row.Message}\nСейчас ждут ответа HR: {Telegram.HrWaiting.Candidates(waiting)}. Не ответите в течение {Telegram.HrWaiting.HandBackMinutes} минут — " +
                "диалог вернётся к AI-рекрутеру.", $"/hr/recruitment/telegram/{c}");
        }

        foreach (var user in await db.ActiveUsersWithAsync(Permissions.HrRecruitmentManage, ct))
        {
            notifier.Notify(user, UserNotificationKind.RecruitmentAlert, title, body, link, now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (dedupKey is not null)
        {
            db.ChangeTracker.Clear(); // то же исключение подняли параллельно
            return await db.RecruitmentExceptions.AsNoTracking().Where(e => e.DedupKey == dedupKey && e.Status == RecruitmentExceptionStatus.Open).Select(e => e.Id).FirstAsync(ct);
        }

        await audit.LogAsync(ActorType.System, "recruitment", "recruitment.exception.raised", nameof(RecruitmentException), row.Id.ToString(),
            new { row.Kind, candidate = candidateId, vacancy = vacancyId }, ct);
        return row.Id;
    }

    public async Task<IReadOnlyList<ExceptionView>> ListAsync(RecruitmentContext ctx, RecruitmentExceptionStatus? status, CancellationToken ct)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        var query = db.RecruitmentExceptions.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(e => e.Status == s);
        }

        var rows = await query.OrderByDescending(e => e.CreatedAt).Take(300).ToListAsync(ct);
        var candidateIds = rows.Where(r => r.CandidateId != null).Select(r => r.CandidateId!.Value).Distinct().ToList();
        var names = await db.Candidates.AsNoTracking().Where(c => candidateIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
        var vacancyIds = rows.Where(r => r.VacancyId != null).Select(r => r.VacancyId!.Value).Distinct().ToList();
        var codes = await db.Vacancies.AsNoTracking().Where(v => vacancyIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => "V" + v.Number, ct);
        var resolvers = rows.Where(r => r.ResolvedById != null).Select(r => r.ResolvedById!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => resolvers.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return rows.Select(e => new ExceptionView(e.Id, e.Kind, e.Message, e.CandidateId, e.CandidateId is { } c ? names.GetValueOrDefault(c) : null, e.VacancyId,
            e.VacancyId is { } v ? codes.GetValueOrDefault(v) : null, e.ConversationId, e.Status.ToString(), e.CreatedAt,
            e.ResolvedById is { } r ? users.GetValueOrDefault(r) : null, e.ResolvedAt, e.Resolution)).ToList();
    }

    /// <summary>
    /// HR ответил в диалоге или вернул его AI (userId — кто; null — система: HR не ответил за час, диалог вернулся к AI) — всё, что ждало человека
    /// по этому диалогу («Нужен HR»), решено: кем и как — в исключении и в аудите. Возвращает, сколько закрыто.
    /// </summary>
    public async Task<int> ResolveConversationAsync(Guid conversationId, Guid? userId, string resolution, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var closed = await db.RecruitmentExceptions.Where(e => e.ConversationId == conversationId && e.Status == RecruitmentExceptionStatus.Open)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.Status, RecruitmentExceptionStatus.Resolved).SetProperty(e => e.ResolvedById, userId)
                .SetProperty(e => e.ResolvedAt, now).SetProperty(e => e.Resolution, resolution), ct);
        if (closed > 0)
        {
            await audit.LogAsync(userId is null ? ActorType.System : ActorType.User, userId?.ToString() ?? "recruitment", "recruitment.exception.resolved",
                "TelegramConversation", conversationId.ToString(), new { closed, resolution }, ct);
        }

        return closed;
    }

    /// <summary>Решено человеком; ReturnToAi — диалог снова ведёт AI-рекрутер.</summary>

    public async Task<IReadOnlyList<ExceptionView>> ResolveAsync(RecruitmentContext ctx, Guid id, ResolveInput input, CancellationToken ct)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("Решать исключения может HR (право «Подбор персонала: управление»).");
        }

        var row = await db.RecruitmentExceptions.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new FieldNotFoundException("Исключение не найдено.");
        if (row.Status == RecruitmentExceptionStatus.Resolved)
        {
            throw new FieldConflictException("Исключение уже решено — обновите страницу.");
        }

        row.Status = RecruitmentExceptionStatus.Resolved;
        row.ResolvedById = ctx.UserId;
        row.ResolvedAt = clock.GetUtcNow();
        row.Resolution = input.Resolution?.Trim() is { Length: > 0 } r ? (r.Length <= 1000 ? r : r[..1000]) : null;
        if (input.ReturnToAi && row.ConversationId is { } c)
        {
            await db.TelegramConversations.Where(x => x.Id == c)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Mode, TelegramConversationMode.Ai).SetProperty(x => x.UpdatedAt, row.ResolvedAt), ct);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.exception.resolved", nameof(RecruitmentException), row.Id.ToString(),
            new { row.Kind, input.ReturnToAi }, ct);
        return await ListAsync(ctx, RecruitmentExceptionStatus.Open, ct);
    }
}

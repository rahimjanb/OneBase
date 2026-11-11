using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Agents;

/// <summary>
/// Какой агент ведёт диалог на номере Telegram. Агент с выбранным номером отвечает только на нём; агент без номера — на любом номере отдела,
/// где нет своего агента.
/// </summary>
public static class DialogAgentPick
{
    /// <summary>
    /// Рекрутер диалога на номере: агент, к которому привязан этот номер (включённый — первым), — он ведёт все диалоги номера со своими
    /// настройками, какая бы вакансия ни была у кандидата (вопросы отбора вакансии он всё равно задаёт). Своего агента у номера нет — агент
    /// вакансии без номера, иначе рекрутер HR без номера по умолчанию. Номер не известен (accountId = null) — агент вакансии, иначе по умолчанию.
    /// </summary>
    public static DialogAgent? Recruiter(IReadOnlyCollection<DialogAgent> recruiters, Guid? vacancyAgentId, Guid? accountId)
    {
        if (accountId is not null && recruiters.Where(a => a.AccountId == accountId).OrderByDescending(a => a.Enabled).ThenByDescending(a => a.IsDefault)
                .ThenBy(a => a.CreatedAt).FirstOrDefault() is { } numberAgent)
        {
            return numberAgent;
        }

        var usable = accountId is null ? recruiters : recruiters.Where(a => a.AccountId is null).ToList();
        if (vacancyAgentId is { } id && usable.FirstOrDefault(a => a.Id == id) is { } vacancyAgent)
        {
            return vacancyAgent;
        }

        return usable.OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt).FirstOrDefault();
    }

    /// <summary>Помощник отдела на номере: включённый агент этого номера, иначе включённый агент отдела без номера (по умолчанию — первым).</summary>
    public static DialogAgent? Assistant(IEnumerable<DialogAgent> enabled, Guid? accountId) =>
        enabled.Where(a => a.AccountId is null || a.AccountId == accountId)
            .OrderByDescending(a => accountId is not null && a.AccountId == accountId).ThenByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt)
            .FirstOrDefault();
}

public sealed record AgentAccountInput(Guid? AccountId);

/// <summary>
/// Номер Telegram у агента («Настройки → ИИ-агенты» → агент → «Номер Telegram»). Кто управляет номерами (Telegram или AI), выбирает любой
/// номер — он начинает работать на отдел агента, новые диалоги на нём идут отделу агента (номер без подразделений сохраняет продажи:
/// рассылки, автоотчёты, ответы агента продаж его людям). Руководитель отдела и HR выбирают только номера, которые уже работают на их
/// отдел, — настройки номера при этом не меняются. Номер у агента другого отдела выбрать нельзя.
/// </summary>
public sealed class DialogAgentAccounts(
    IAppDbContext db,
    DialogAgentService agents,
    TelegramDepartmentService departments,
    IAuditLogger audit,
    TimeProvider clock)
{
    /// <summary>Причина маршрута диалога, отданного отделу при выборе номера у агента.</summary>
    public const string AgentAccountReason = "agent_account";

    public async Task<AgentView> SetAsync(AgentAccess access, Guid id, AgentAccountInput input, CancellationToken ct)
    {
        var agent = await agents.AgentAsync(access, id, ct, tracked: true);
        var before = agent.AccountId;
        if (input.AccountId is { } accountId)
        {
            var account = await db.TelegramAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct)
                          ?? throw new FieldValidationException("Номер Telegram не найден.");
            var other = await db.DialogAgents.AsNoTracking().Where(a => a.AccountId == accountId && a.Department != agent.Department)
                .Select(a => new { a.Name, a.Department }).FirstOrDefaultAsync(ct);
            if (other is not null)
            {
                throw new FieldValidationException(
                    $"Номер «{account.Name}» уже выбран у агента «{other.Name}» (отдел «{await DepartmentNameAsync(other.Department, ct)}»). Выберите другой номер или уберите его у того агента.");
            }

            await ServeAsync(access, agent, account, ct);
        }

        agent.AccountId = input.AccountId;
        agent.UpdatedAt = clock.GetUtcNow();
        agent.UpdatedById = access.UserId;
        await db.SaveChangesAsync(ct);
        if (before != agent.AccountId)
        {
            await audit.LogAsync(ActorType.User, access.UserId.ToString(), "agents.account", nameof(DialogAgent), id.ToString(),
                new { before, after = agent.AccountId }, ct);
        }

        return await agents.GetAsync(access, id, ct);
    }

    /// <summary>Номер работает на отдел агента и отдаёт ему новые диалоги (у кого есть право настраивать номера).</summary>
    private async Task ServeAsync(AgentAccess access, DialogAgent agent, TelegramAccount account, CancellationToken ct)
    {
        var rows = await db.TelegramAccountDepartments.AsNoTracking().Where(d => d.AccountId == account.Id).ToListAsync(ct);
        var serves = rows.Any(r => r.Department == agent.Department);
        var isDefault = rows.Any(r => r.Department == agent.Department && r.IsDefault);
        if (serves && isDefault)
        {
            return;
        }

        if (!access.ManageAccounts)
        {
            if (!serves)
            {
                throw new FieldValidationException(
                    $"Номер «{account.Name}» не работает на отдел «{await DepartmentNameAsync(agent.Department, ct)}». Подключить его к отделу может администратор Telegram — здесь или в «Настройки → Telegram → Подразделения».");
            }

            return; // номер уже работает на отдел; кому новые диалоги — решает администратор Telegram
        }

        // Номер без подразделений был целиком у продаж — продажи остаются (рассылки, автоотчёты, агент продаж своим людям).
        var codes = rows.Select(r => r.Department).ToList();
        if (rows.Count == 0 && await db.Departments.AnyAsync(d => d.Code == TelegramDepartments.Sales, ct))
        {
            codes.Add(TelegramDepartments.Sales);
        }

        codes.Add(agent.Department);
        await departments.SaveAsync(account.Id, new TelegramDepartmentsInput(codes.Distinct(StringComparer.Ordinal).ToList(), agent.Department), access.UserId, ct);

        // Диалоги, которым некому было отвечать (у номера не было отдела по умолчанию), — отделу агента.
        var moved = await db.TelegramConversations
            .Where(c => c.AccountId == account.Id && c.Department == null && c.RouteReason == "no_default")
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Department, agent.Department).SetProperty(c => c.RouteReason, AgentAccountReason)
                .SetProperty(c => c.UpdatedAt, clock.GetUtcNow()).SetProperty(c => c.UpdatedById, access.UserId), ct);
        if (moved > 0)
        {
            await audit.LogAsync(ActorType.User, access.UserId.ToString(), "telegram.conversation.assigned", nameof(TelegramAccount), account.Id.ToString(),
                new { department = agent.Department, reason = AgentAccountReason, count = moved }, ct);
        }
    }

    private async Task<string> DepartmentNameAsync(string code, CancellationToken ct) =>
        await db.Departments.AsNoTracking().Where(d => d.Code == code).Select(d => d.Name).FirstOrDefaultAsync(ct) ?? code;
}

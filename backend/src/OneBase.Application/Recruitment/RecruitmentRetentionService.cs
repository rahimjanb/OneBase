using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Recruitment;

/// <summary>
/// Срок хранения данных кандидатов: подбор закончен (все отклики закрыты) и прошло RetentionDays (по умолчанию 7) без активности — данные
/// кандидата удаляются целиком, вместе с файлами резюме. Кандидат, у которого подбор идёт, не удаляется никогда. Сбой хранилища — кандидат
/// остаётся до следующего круга (файл не должен пережить свои данные).
/// </summary>
public sealed class RecruitmentRetentionService(IAppDbContext db, RecruitmentService recruitment, TimeProvider clock, ILogger<RecruitmentRetentionService> logger)
{
    /// <summary>Кандидатов за один круг — не больше (остальные — на следующем).</summary>
    public const int Batch = 200;

    /// <summary>Сколько дней хранится журнал ответов ИИ-агентов отделов (не кандидатов).</summary>
    public const int AgentRepliesDays = 90;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var days = await recruitment.RetentionDaysAsync(ct);
        var cutoff = clock.GetUtcNow().AddDays(-days);
        var expired = await db.Candidates.AsNoTracking().Where(c => c.RetentionFrom != null && c.RetentionFrom <= cutoff)
            .OrderBy(c => c.RetentionFrom).Select(c => c.Id).Take(Batch).ToListAsync(ct);
        var deleted = 0;
        foreach (var id in expired)
        {
            try
            {
                await recruitment.DeleteCandidateDataAsync(id, "retention", null, ct);
                deleted++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Подбор: данные кандидата {Candidate} не удалены по сроку хранения — повтор на следующем круге", id);
            }
        }

        // Лиды из групп, по которым HR не завёл кандидата, — тоже по сроку (это чужие посты с именами).
        var leads = await db.TelegramLeads.Where(l => l.CandidateId == null && l.CreatedAt <= cutoff).ExecuteDeleteAsync(ct);
        // Журнал ответов ИИ-агентов отделов (не кандидатов — их записи удаляются вместе с кандидатом) — 90 дней.
        await db.DialogAgentReplies.Where(r => r.CandidateId == null && r.CreatedAt <= clock.GetUtcNow().AddDays(-AgentRepliesDays)).ExecuteDeleteAsync(ct);

        if (leads > 0)
        {
            logger.LogInformation("Подбор: удалено {Count} лидов из групп Telegram — истёк срок хранения", leads);
        }

        if (deleted > 0)
        {
            logger.LogInformation("Подбор: удалены данные {Count} кандидатов — истёк срок хранения ({Days} дн. после завершения подбора)", deleted, days);
        }

        return deleted;
    }
}

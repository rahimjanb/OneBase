using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Telegram для консультанта, только чтение: автоотчёты (право telegram.manage) — расписание, получатели, запуски и неудачные отправки;
/// рассылки (telegram.campaigns или telegram.manage) — статусы и доставка. Сессии, токены и тексты сообщений не отдаются.
/// </summary>
internal sealed class GetTelegramTool(IAppDbContext db, IUserPermissions permissions, TimeProvider clock) : DataTool
{
    public override string Name => "get_telegram";
    public override string Title => "Telegram: автоотчёты и рассылки";
    public override string Source => KnowledgeSources.Telegram;
    public override string? RequiredPermission => Permissions.TelegramManage;
    public override IReadOnlyList<string>? AlternativePermissions => [Permissions.TelegramCampaigns];

    public override string UsageHint =>
        "Ушли ли автоотчёты в Telegram, кому они идут, когда следующий, почему не дошёл, как прошла рассылка — get_telegram.";

    public override string Description =>
        "Автоматические отчёты в Telegram: аккаунт и язык, 13 запусков шести отчётов (включён ли, время, следующий запуск, последний запуск с " +
        "итогом: отправлено / ошибок / пропущено), получатели (роль РМ/СВ/ТП, область, чаты, отчёты, включён ли), отправки за последние дни по " +
        "статусам и список неудачных и «неизвестно, дошёл ли» с причиной. Рассылки: название, статус, время, сколько получателей, отправлено, " +
        "ошибок, в очереди, последняя ошибка. Аргумент days — за сколько дней (1–30, по умолчанию 7).";

    public override JsonElement InputSchema { get; } = Schema(("days", new { type = "integer", description = "За сколько последних дней отправки (1–30, по умолчанию 7)." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var days = args.Int("days", 1, 30) ?? 7;
        IReadOnlySet<string> granted = context.UserId is { } user ? await permissions.GetAsync(user, ct) : new HashSet<string>();
        var reports = granted.Contains(Permissions.TelegramManage);
        var campaigns = reports || granted.Contains(Permissions.TelegramCampaigns);
        var now = clock.GetUtcNow();
        var since = now.AddDays(-days);

        return Data(new
        {
            Days = days,
            Reports = reports ? await ReportsAsync(now, since, ct) : null,
            Campaigns = campaigns ? await CampaignsAsync(since, ct) : null,
            Note = "Время — по Ташкенту. Автоотчёты видит только право «Telegram: управление», рассылки — «Telegram: рассылки».",
        }, new DataSource("OneBase → Настройки → Telegram", $"последние {days} дн.", "/settings/telegram/reports"));
    }

    private static string Local(DateTimeOffset? at) => at is { } a ? ReportSchedules.Local(a, ReportSchedules.DefaultTimeZone).ToString("yyyy-MM-dd HH:mm") : "";

    private async Task<object> ReportsAsync(DateTimeOffset now, DateTimeOffset since, CancellationToken ct)
    {
        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var account = settings?.AccountId is { } accountId ? await db.TelegramAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct) : null;
        var stored = await db.TelegramReportJobs.AsNoTracking().ToDictionaryAsync(j => j.Code, ct);
        var runs = (await db.TelegramReportRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(300).ToListAsync(ct))
            .GroupBy(r => r.Job).ToDictionary(g => g.Key, g => g.First());
        var recipients = await db.TelegramReportSubscriptions.AsNoTracking().Include(s => s.Chats).OrderBy(s => s.Role).ToListAsync(ct);
        var chatIds = recipients.SelectMany(r => r.Chats.Select(c => c.ChatId)).Distinct().ToArray();
        var chats = await db.TelegramChats.AsNoTracking().Where(c => chatIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var regionIds = recipients.Where(r => r.RegionId != null).Select(r => r.RegionId!.Value).Distinct().ToArray();
        var regions = await db.SalesRegions.AsNoTracking().Where(r => regionIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var deliveries = await db.TelegramReportDeliveries.AsNoTracking().Where(d => d.CreatedAt >= since && d.Report != null)
            .OrderByDescending(d => d.CreatedAt).ToListAsync(ct);

        string Title(string code) => ReportCatalog.Find(code) is { } info ? $"{ReportCatalog.Title(info.Report)} · {info.Time:HH\\:mm} — {ReportCatalog.SlotTitle(info.Report, info.Slot)}" : code;

        return new
        {
            Account = account is null ? null : new { account.Name, Status = account.Status.ToString(), account.Username },
            settings?.Language,
            settings?.NoPurchaseDays,
            Jobs = ReportCatalog.Jobs.Select(info =>
            {
                var job = stored.GetValueOrDefault(info.Code) ?? ReportCatalog.Default(info);
                var run = runs.GetValueOrDefault(info.Code);
                return new
                {
                    info.Code,
                    Title = $"{ReportCatalog.Title(info.Report)} — {ReportCatalog.SlotTitle(info.Report, info.Slot)}",
                    job.Enabled,
                    Time = job.Time.ToString("HH:mm"),
                    Days = ReportSchedules.IsoDays(job.Days),
                    Roles = info.Roles,
                    NextRun = job.Enabled ? Local(ReportSchedules.Next(job, now)) : null,
                    LastRun = run is null ? null : new { Period = run.Period.ToString("yyyy-MM-dd"), Started = Local(run.StartedAt), run.Status, run.Planned, run.Sent, run.Failed, run.Skipped, run.Error },
                };
            }),
            Recipients = recipients.Select(r => new
            {
                r.Role,
                Name = r.Title ?? r.Role switch
                {
                    TelegramReportRoles.Tp => $"ТП {r.AgentName}",
                    TelegramReportRoles.Rm => "РМ",
                    _ => $"{(r.RegionId is { } id ? regions.GetValueOrDefault(id) : null)} · СВ {r.SupervisorName}",
                },
                Region = r.RegionId is { } rid ? regions.GetValueOrDefault(rid) : null,
                r.Enabled,
                Chats = r.Chats.Select(c => chats.GetValueOrDefault(c.ChatId)).Where(c => c is not null)
                    .Select(c => new { c!.Title, Type = c.Type.ToString(), c.CanSend, Problem = c.CanSend ? null : c.CannotSendReason }),
                Reports = ReportCatalog.Parse(r.Reports),
            }),
            DeliveriesByStatus = deliveries.GroupBy(d => d.Status).ToDictionary(g => g.Key, g => g.Count()),
            Problems = deliveries.Where(d => d.Status is "failed" or "unknown" or "skipped").Take(30).Select(d => new
            {
                At = Local(d.SentAt ?? d.LastAttemptAt ?? d.CreatedAt),
                Report = Title(d.Report!),
                Period = d.Period.ToString("yyyy-MM-dd"),
                d.Trigger,
                Chat = d.RecipientTitle,
                Scope = d.ScopeTitle,
                d.Status,
                d.Error,
            }),
            LastSent = deliveries.Where(d => d.Status == "sent").Take(20).Select(d => new
            {
                At = Local(d.SentAt), Report = Title(d.Report!), Chat = d.RecipientTitle, Scope = d.ScopeTitle, d.Summary,
            }),
        };
    }

    private async Task<object> CampaignsAsync(DateTimeOffset since, CancellationToken ct)
    {
        var list = await db.TelegramCampaigns.AsNoTracking()
            .Where(c => c.CreatedAt >= since || c.Status == TelegramCampaignStatus.Scheduled || c.Status == TelegramCampaignStatus.Queued
                        || c.Status == TelegramCampaignStatus.Sending || c.Status == TelegramCampaignStatus.Paused)
            .OrderByDescending(c => c.CreatedAt)
            .Take(20)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Status,
                c.ScheduledAt,
                c.StartedAt,
                c.FinishedAt,
                c.LastError,
                Total = c.Recipients.Count,
                Sent = c.Recipients.Count(r => r.Status == TelegramRecipientStatus.Sent),
                Failed = c.Recipients.Count(r => r.Status == TelegramRecipientStatus.Failed),
                Pending = c.Recipients.Count(r => r.Status == TelegramRecipientStatus.Pending || r.Status == TelegramRecipientStatus.Sending),
            })
            .ToListAsync(ct);
        return list.Select(c => new
        {
            c.Name,
            Status = c.Status.ToString(),
            Scheduled = Local(c.ScheduledAt),
            Started = Local(c.StartedAt),
            Finished = Local(c.FinishedAt),
            c.Total,
            c.Sent,
            c.Failed,
            c.Pending,
            c.LastError,
        });
    }
}

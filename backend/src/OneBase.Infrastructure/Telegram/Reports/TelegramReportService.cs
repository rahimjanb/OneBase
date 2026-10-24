using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Audit;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// «Настройки → Telegram → Автоматические отчёты»: аккаунт, язык, порог «без закупа», обновление Linko перед отправкой; расписания 13
/// запусков шести отчётов (включаются отдельно — сохранение ничего не отправляет); получатели «роль → область → чаты → отчёты»;
/// предпросмотр, тест (только в выбранный чат), ручная отправка и повтор неудачных; история отправок и запусков. Иерархия — реальные данные
/// раздела «Продажи»; реальные цифры — только тому, у кого есть право на продажи (sales.read).
/// </summary>
public sealed class TelegramReportService(
    IAppDbContext db,
    TelegramReportSender sender,
    ReportEngine engine,
    IReportRenderer renderer,
    IAuditLogger audit,
    IConfiguration config,
    TimeProvider clock,
    DailyPlanService dailyPlans)
{
    private static readonly string[] Languages = ["ru", "uz"];
    private const int PageSize = 50;

    private const string NoSales = "Нет права на данные продаж (sales.read) — отчёты на реальных данных недоступны.";

    // ---------- Обзор ----------

    public async Task<ReportOverview> OverviewAsync(bool canUseSales, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var accounts = await db.TelegramAccounts.AsNoTracking().OrderBy(a => a.CreatedAt)
            .Select(a => new ReportAccountOption(a.Id, a.Name, a.Status.ToString(), a.Username)).ToListAsync(ct);
        var (teams, directions) = canUseSales ? await engine.StructureAsync(Today(), ct) : ([], []);
        var regions = teams.Select(r => new ReportRegionOption(r.Id, r.Name, r.DirectionId, r.Manager, r.SupervisorName,
                r.Supervisors.Select(s => new ReportPersonOption(s.Id, s.Name)).ToList(), r.Agents.Select(a => new ReportPersonOption(a.Id, a.Name)).ToList()))
            .ToList();
        var directionOptions = directions.Select(d => new ReportDirectionOption(d.Id, d.Name, d.Manager, teams.Count(t => t.DirectionId == d.Id))).ToList();
        var chats = settings.AccountId is { } account ? await ChatsAsync(account, ct) : [];
        var recipients = await RecipientsAsync(settings.AccountId, teams, directions, chats, ct);
        var since = clock.GetUtcNow().AddDays(-7);
        var statuses = await db.TelegramReportDeliveries.AsNoTracking().Where(d => d.CreatedAt >= since && d.Report != null).GroupBy(d => d.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var metrics = new ReportMetrics(statuses.GetValueOrDefault(TelegramReportSender.Sent), statuses.GetValueOrDefault(TelegramReportSender.Failed),
            statuses.GetValueOrDefault(TelegramReportSender.Skipped), statuses.GetValueOrDefault(TelegramReportSender.Unknown),
            statuses.GetValueOrDefault(TelegramReportSender.Pending) + statuses.GetValueOrDefault(TelegramReportSender.Sending));
        return new ReportOverview(SettingsView(settings), accounts, await JobsAsync(recipients, ct), recipients, directionOptions, regions, chats, canUseSales,
            config.GetValue("Telegram:Enabled", true), metrics);
    }

    private async Task<TelegramReportSettings> SettingsAsync(CancellationToken ct) =>
        await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new TelegramReportSettings();

    private static ReportSettingsView SettingsView(TelegramReportSettings s) => new(s.AccountId, s.Language, s.NoPurchaseDays, s.RefreshBeforeSend, s.UpdatedAt);

    private async Task<List<ReportJobView>> JobsAsync(IReadOnlyList<ReportRecipientView> recipients, CancellationToken ct)
    {
        var stored = await db.TelegramReportJobs.AsNoTracking().ToDictionaryAsync(j => j.Code, ct);
        var codes = ReportCatalog.Jobs.Select(j => j.Code).ToArray();
        var lastRuns = (await db.TelegramReportRuns.AsNoTracking().Where(r => codes.Contains(r.Job)).OrderByDescending(r => r.StartedAt).Take(200).ToListAsync(ct))
            .GroupBy(r => r.Job).ToDictionary(g => g.Key, g => g.First());
        var now = clock.GetUtcNow();
        return ReportCatalog.Jobs.Select(info =>
        {
            var job = stored.GetValueOrDefault(info.Code) ?? ReportCatalog.Default(info);
            var run = lastRuns.GetValueOrDefault(info.Code);
            return new ReportJobView(info.Code, ReportCatalog.Key(info.Report), ReportCatalog.Title(info.Report), info.Slot, ReportCatalog.SlotTitle(info.Report, info.Slot), info.Roles,
                job.Enabled, job.Time.ToString("HH:mm"), job.TimeZone, ReportSchedules.IsoDays(job.Days), job.CatchUpMinutes, ReportSchedules.Next(job, now),
                run is null ? null : new ReportRunBrief(run.Id, run.Period, run.StartedAt, run.FinishedAt, run.Status, run.Planned, run.Sent, run.Failed, run.Skipped, run.Error),
                recipients.Count(r => r.Enabled && r.Reports.Contains(info.Code)), job.UpdatedAt);
        }).ToList();
    }

    /// <summary>Чаты аккаунта: личные и группы; каналы — только где аккаунт может публиковать. LinkedUser — чей Telegram подтверждён кодом.</summary>
    private async Task<List<ReportChatOption>> ChatsAsync(Guid accountId, CancellationToken ct)
    {
        var chats = await db.TelegramChats.AsNoTracking()
            .Where(c => c.AccountId == accountId && (c.Type != TelegramChatType.Channel || c.CanSend))
            .OrderBy(c => c.Type).ThenBy(c => c.Title)
            .ToListAsync(ct);
        var linked = await (from p in db.TelegramUserPermissions.AsNoTracking()
                            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
                            where p.AccountId == accountId && p.TelegramUserId != null && p.VerifiedAt != null
                            select new { p.TelegramUserId, u.FullName })
            .ToListAsync(ct);
        var byPeer = linked.GroupBy(x => x.TelegramUserId!.Value).ToDictionary(g => g.Key, g => g.First().FullName);
        return chats.Select(c => new ReportChatOption(c.Id, c.Title, c.Type.ToString(), c.Username, c.PeerId, c.CanSend, c.CannotSendReason,
            c.Type == TelegramChatType.Private ? byPeer.GetValueOrDefault(c.PeerId) : null)).ToList();
    }

    private async Task<List<ReportRecipientView>> RecipientsAsync(Guid? accountId, IReadOnlyList<ReportRegionTeam> teams, IReadOnlyList<ReportDirection> directions,
        IReadOnlyList<ReportChatOption> accountChats, CancellationToken ct)
    {
        var rows = await db.TelegramReportSubscriptions.AsNoTracking().Include(s => s.Chats).OrderBy(s => s.Role).ThenBy(s => s.CreatedAt).ToListAsync(ct);
        var chatIds = rows.SelectMany(r => r.Chats.Select(c => c.ChatId)).Distinct().ToArray();
        var chats = await db.TelegramChats.AsNoTracking().Where(c => chatIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var linkedByChat = accountChats.Where(c => c.LinkedUser is not null).ToDictionary(c => c.Id, c => c.LinkedUser);
        var regionIds = rows.Where(r => r.RegionId != null).Select(r => r.RegionId!.Value).Distinct().ToArray();
        var regionNames = await db.SalesRegions.AsNoTracking().Where(r => regionIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var ids = rows.Select(r => r.Id).ToArray();
        var last = await db.TelegramReportDeliveries.AsNoTracking()
            .Where(d => d.SubscriptionId != null && ids.Contains(d.SubscriptionId.Value) && d.Report != null)
            .OrderByDescending(d => d.CreatedAt)
            .Take(800)
            .Select(d => new { d.SubscriptionId, d.Report, d.RecipientTitle, d.Status, d.CreatedAt, d.SentAt, d.Error })
            .ToListAsync(ct);
        string T(string ru) => ru;

        var views = new List<ReportRecipientView>();
        foreach (var s in rows)
        {
            var chatRefs = s.Chats.Select(c => chats.GetValueOrDefault(c.ChatId)).Where(c => c is not null)
                .Select(c => new ReportChatRef(c!.Id, c.Title, c.Type.ToString(), c.CanSend, c.PeerId, linkedByChat.GetValueOrDefault(c.Id))).ToList();
            string scopeTitle;
            int agents;
            string? problem = null;
            try
            {
                var resolved = teams.Count == 0 ? null : ReportScopes.Resolve(ReportScope.Of(s), teams, directions, T);
                scopeTitle = resolved?.Title ?? Fallback(s, regionNames);
                agents = resolved?.Agents.Count ?? 0;
            }
            catch (KeyNotFoundException ex)
            {
                scopeTitle = Fallback(s, regionNames);
                agents = 0;
                problem = ex.Message;
            }

            problem ??= ChatProblem(s, chatRefs, accountId, chats);
            var reports = ReportCatalog.ForRole(ReportCatalog.Parse(s.Reports), s.Role);
            if (problem is null && reports.Count == 0)
            {
                problem = "Не выбран ни один отчёт.";
            }

            var status = chatRefs.Count == 0 ? "none"
                : chatRefs.Any(c => c.Linked is not null && c.CanSend) ? "linked"
                : chatRefs.Any(c => c.Type == nameof(TelegramChatType.Private) && c.CanSend) ? "chat"
                : chatRefs.Any(c => c.CanSend) ? "group"
                : "blocked";
            var recent = last.Where(d => d.SubscriptionId == s.Id).GroupBy(d => (d.Report, d.RecipientTitle)).Select(g => g.First()).Take(6)
                .Select(d => new ReportDeliveryBrief(d.Report!, d.RecipientTitle, d.Status, d.SentAt ?? d.CreatedAt, d.Error)).ToList();
            views.Add(new ReportRecipientView(s.Id, s.Role, s.Title ?? scopeTitle, scopeTitle, s.DirectionId, s.RegionId, s.RegionId is { } rid ? regionNames.GetValueOrDefault(rid) : null,
                s.SupervisorId, s.AgentId, agents, chatRefs, status, reports, s.Enabled, problem, recent));
        }

        return views;
    }

    private static string Fallback(TelegramReportSubscription s, IReadOnlyDictionary<Guid, string> regions) => s.Role switch
    {
        TelegramReportRoles.Rm => s.Title ?? "РМ",
        TelegramReportRoles.Tp => $"ТП {s.AgentName ?? s.AgentId?.ToString()}",
        _ => string.Join(" · ", new[] { s.RegionId is { } r ? regions.GetValueOrDefault(r) : null, s.SupervisorName }.Where(x => !string.IsNullOrWhiteSpace(x))),
    };

    /// <summary>Почему отчёт уйдёт не во все чаты получателя.</summary>
    private static string? ChatProblem(TelegramReportSubscription s, IReadOnlyList<ReportChatRef> chats, Guid? accountId, IReadOnlyDictionary<Guid, TelegramChat> all)
    {
        if (chats.Count == 0)
        {
            return "Нет чатов: чаты удалены из диалогов аккаунта — добавьте другие.";
        }

        var blocked = s.Chats.Select(c => all.GetValueOrDefault(c.ChatId)).Where(c => c is not null && TelegramReportSender.ChatProblem(c, accountId) is not null).ToList();
        return blocked.Count == 0 ? null : $"Не уйдёт в: {string.Join(", ", blocked.Select(c => c!.Title))} — {TelegramReportSender.ChatProblem(blocked[0], accountId)}";
    }

    // ---------- Настройки и расписания ----------

    public async Task<ReportSettingsView> SaveSettingsAsync(ReportSettingsInput input, Guid userId, CancellationToken ct)
    {
        var language = input.Language?.Trim().ToLowerInvariant() ?? "ru";
        if (!Languages.Contains(language))
        {
            throw new ReportValidationException("Язык подписей — ru или uz.");
        }

        if (input.NoPurchaseDays is < 1 or > 365)
        {
            throw new ReportValidationException("Порог «без закупа» — от 1 до 365 дней.");
        }

        if (input.AccountId is { } account && !await db.TelegramAccounts.AnyAsync(a => a.Id == account, ct))
        {
            throw new ReportValidationException("Аккаунт Telegram не найден.");
        }

        var settings = await db.TelegramReportSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new TelegramReportSettings();
            db.TelegramReportSettings.Add(settings);
        }

        var changedAccount = settings.AccountId != input.AccountId;
        settings.AccountId = input.AccountId;
        settings.Language = language;
        settings.NoPurchaseDays = input.NoPurchaseDays ?? settings.NoPurchaseDays;
        settings.RefreshBeforeSend = input.RefreshBeforeSend ?? settings.RefreshBeforeSend;
        settings.UpdatedAt = clock.GetUtcNow();
        settings.UpdatedById = userId;
        if (changedAccount)
        {
            // Чаты принадлежат аккаунту: чаты прежнего аккаунта с новым не работают — убираем их у получателей явно, а не молча.
            var stale = await db.TelegramReportSubscriptionChats.Where(c => !db.TelegramChats.Any(x => x.Id == c.ChatId && x.AccountId == input.AccountId)).ToListAsync(ct);
            db.TelegramReportSubscriptionChats.RemoveRange(stale);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.settings", nameof(TelegramReportSettings), "1",
            new { settings.AccountId, settings.Language, settings.NoPurchaseDays, settings.RefreshBeforeSend }, ct);
        return SettingsView(settings);
    }

    /// <summary>
    /// Расписание запуска. Включение и смена времени «взводят» его с этого момента: время, которое сегодня уже прошло, не догоняется —
    /// после сохранения ничего не уходит сразу. Version растёт при каждом изменении.
    /// </summary>
    public async Task<ReportJobView> SaveJobAsync(string code, ReportJobInput input, Guid userId, CancellationToken ct)
    {
        var info = ReportCatalog.Find(code) ?? throw new KeyNotFoundException();
        var time = ReportSchedules.ParseTime(input.Time) ?? throw new ReportValidationException("Время — в формате ЧЧ:ММ, например 08:30.");
        var zone = string.IsNullOrWhiteSpace(input.TimeZone) ? ReportSchedules.DefaultTimeZone : input.TimeZone.Trim();
        if (ReportSchedules.Zone(zone) is null)
        {
            throw new ReportValidationException($"Часовой пояс «{zone}» не найден — укажите IANA, например Asia/Tashkent.");
        }

        var days = ReportSchedules.DaysOf(input.Days ?? []);
        if (input.Enabled && days == 0)
        {
            throw new ReportValidationException("Выберите хотя бы один день отправки.");
        }

        var catchUp = input.CatchUpMinutes ?? info.CatchUpMinutes;
        if (catchUp is < 0 or > ReportSchedules.MaxCatchUpMinutes)
        {
            throw new ReportValidationException("Догонять пропущенный запуск — от 0 до 1440 минут.");
        }

        var job = await db.TelegramReportJobs.FirstOrDefaultAsync(j => j.Code == code, ct);
        if (job is null)
        {
            job = ReportCatalog.Default(info);
            db.TelegramReportJobs.Add(job);
        }

        var now = clock.GetUtcNow();
        var changed = job.Enabled != input.Enabled || job.Time != time || job.TimeZone != zone || job.Days != days || job.CatchUpMinutes != catchUp;
        if (input.Enabled && (!job.Enabled || job.Time != time || job.TimeZone != zone || job.Days != days))
        {
            job.ArmedAt = now;
        }

        job.Enabled = input.Enabled;
        job.Time = time;
        job.TimeZone = zone;
        job.Days = days;
        job.CatchUpMinutes = catchUp;
        if (changed)
        {
            job.Version++;
        }

        job.UpdatedAt = now;
        job.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.job", nameof(TelegramReportJob), code,
            new { job.Enabled, Time = job.Time.ToString("HH:mm"), job.TimeZone, job.Days, job.CatchUpMinutes, job.Version }, ct);
        var settings = await SettingsAsync(ct);
        var (teams, directions) = await engine.StructureAsync(Today(), ct);
        var chats = settings.AccountId is { } account ? await ChatsAsync(account, ct) : [];
        return (await JobsAsync(await RecipientsAsync(settings.AccountId, teams, directions, chats, ct), ct)).First(j => j.Code == code);
    }

    // ---------- Получатели ----------

    /// <summary>
    /// Получатель: роль, область — из данных «Продаж» месяца (РМ — направление или вся страна, СВ — регион и его супервайзер, ТП — агент),
    /// чаты аккаунта, куда можно писать, отчёты, подходящие роли.
    /// </summary>
    public async Task<ReportRecipientView> SaveRecipientAsync(Guid? id, ReportRecipientInput input, bool canUseSales, Guid userId, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var role = input.Role?.Trim().ToLowerInvariant() ?? "";
        if (!TelegramReportRoles.All.Contains(role))
        {
            throw new ReportValidationException("Роль получателя — РМ, СВ или ТП.");
        }

        var scope = role switch
        {
            TelegramReportRoles.Rm => new ReportScope(role, DirectionId: input.DirectionId),
            TelegramReportRoles.Tp => new ReportScope(role, AgentId: input.AgentId ?? throw new ReportValidationException("Выберите ТП.")),
            _ => new ReportScope(role, RegionId: input.RegionId ?? throw new ReportValidationException("Выберите регион."), SupervisorId: input.SupervisorId),
        };
        var (teams, directions) = await engine.StructureAsync(Today(), ct);
        ResolvedScope resolved;
        try
        {
            resolved = ReportScopes.Resolve(scope, teams, directions, ru => ru);
        }
        catch (KeyNotFoundException ex)
        {
            throw new ReportValidationException(ex.Message);
        }

        var reports = ReportCatalog.ForRole(ReportCatalog.Parse(string.Join(',', input.Reports ?? [])), role);
        if (reports.Count == 0)
        {
            throw new ReportValidationException("Отметьте хотя бы один отчёт.");
        }

        var chatIds = (input.ChatIds ?? []).Distinct().ToList();
        if (chatIds.Count == 0)
        {
            throw new ReportValidationException("Выберите личный чат получателя или группу.");
        }

        var settings = await SettingsAsync(ct);
        var account = settings.AccountId ?? throw new ReportValidationException("Сначала выберите аккаунт Telegram для автоотчётов.");
        var chats = await db.TelegramChats.AsNoTracking().Where(c => chatIds.Contains(c.Id)).ToListAsync(ct);
        if (chats.Count != chatIds.Count)
        {
            throw new ReportValidationException("Чат не найден у аккаунта автоотчётов — обновите чаты.");
        }

        if (chats.Select(c => (Chat: c, Problem: TelegramReportSender.ChatProblem(c, account))).FirstOrDefault(x => x.Problem is not null) is { Chat: { } bad } x)
        {
            throw new ReportValidationException($"Чат «{bad.Title}»: {x.Problem}");
        }

        var row = id is { } existingId
            ? await db.TelegramReportSubscriptions.Include(s => s.Chats).FirstOrDefaultAsync(s => s.Id == existingId, ct) ?? throw new KeyNotFoundException()
            : new TelegramReportSubscription { CreatedById = userId };
        var team = role == TelegramReportRoles.Tp ? teams.First(t => t.Agents.Any(a => a.Id == input.AgentId)) : null;
        row.Role = role;
        row.Title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim()[..Math.Min(input.Title.Trim().Length, 200)];
        row.DirectionId = role == TelegramReportRoles.Rm ? input.DirectionId : null;
        row.RegionId = role switch { TelegramReportRoles.Sv => input.RegionId, TelegramReportRoles.Tp => team!.Id, _ => null };
        row.SupervisorId = role == TelegramReportRoles.Sv ? input.SupervisorId : null;
        row.SupervisorName = role == TelegramReportRoles.Sv && input.SupervisorId is { } sid ? teams.First(t => t.Id == input.RegionId).Supervisors.First(s => s.Id == sid).Name : null;
        row.AgentId = role == TelegramReportRoles.Tp ? input.AgentId : null;
        row.AgentName = resolved.Agent?.Name;
        row.Reports = string.Join(',', reports);
        row.Enabled = input.Enabled;
        row.Chats.RemoveAll(c => !chatIds.Contains(c.ChatId));
        foreach (var chatId in chatIds.Where(c => row.Chats.All(x => x.ChatId != c)))
        {
            row.Chats.Add(new TelegramReportSubscriptionChat { SubscriptionId = row.Id, ChatId = chatId });
        }

        if (id is null)
        {
            db.TelegramReportSubscriptions.Add(row);
        }
        else
        {
            row.UpdatedAt = clock.GetUtcNow();
            row.UpdatedById = userId;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), id is null ? "telegram.reports.recipient_created" : "telegram.reports.recipient_updated",
            nameof(TelegramReportSubscription), row.Id.ToString(),
            new { row.Role, row.DirectionId, row.RegionId, row.SupervisorId, row.AgentId, Chats = chatIds, row.Reports, row.Enabled }, ct);
        return (await RecipientsAsync(account, teams, directions, await ChatsAsync(account, ct), ct)).First(s => s.Id == row.Id);
    }

    public async Task DeleteRecipientAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await db.TelegramReportSubscriptions.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new KeyNotFoundException();
        db.TelegramReportSubscriptions.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.recipient_deleted", nameof(TelegramReportSubscription), id.ToString(), null, ct);
    }

    // ---------- Предпросмотр, тест, ручная отправка, повтор ----------

    public async Task<ReportPreview> PreviewAsync(ReportPreviewInput input, bool canUseSales, CancellationToken ct)
    {
        var (job, scope, date, at, language) = await TargetAsync(input, canUseSales, ct);
        var report = await sender.BuildAsync(job, scope, date, at, language, ct);
        var pages = renderer.Render(report);
        return new ReportPreview(pages.Select(p => "data:image/png;base64," + Convert.ToBase64String(p)).ToList(), report.Title, report.Scope, report.Caption,
            report.Summary, report.Notes, report.Problems, pages.Sum(p => (long)p.Length), report.Applies, report.SkipReason, report.Incomplete);
    }

    /// <summary>Тестовая отправка — только в выбранный чат (не получателям); на картинке «ТЕСТОВАЯ ОТПРАВКА». Повторов нет: результат сразу.</summary>
    public async Task<ReportSendResult> TestAsync(ReportTestInput input, bool canUseSales, Guid userId, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var account = settings.AccountId ?? throw new ReportValidationException("Сначала выберите аккаунт Telegram для автоотчётов.");
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.ChatId, ct);
        if (TelegramReportSender.ChatProblem(chat, account) is { } problem)
        {
            throw new ReportValidationException(problem);
        }

        var (job, scope, date, at, language) = await TargetAsync(input.Report, canUseSales, ct);
        var delivery = new TelegramReportDelivery
        {
            Report = job,
            Period = date,
            Trigger = "test",
            DataMode = "real",
            Role = scope.Role,
            ScopeKey = scope.Key,
            AccountId = account,
            ChatId = chat!.Id,
            PeerId = chat.PeerId,
            RecipientTitle = TelegramReportSender.Trim(chat.Title, 255),
            ScopeTitle = "…",
            RegionId = scope.RegionId,
            SupervisorId = scope.SupervisorId,
            CreatedById = userId,
        };
        db.TelegramReportDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.test", nameof(TelegramReportDelivery), delivery.Id.ToString(),
            new { job, Scope = scope.Key, chat.Title }, ct);
        return await sender.AttemptAsync(delivery, chat, token => sender.BuildAsync(job, scope, date, at, language, token), retry: false, ct, test: true);
    }

    /// <summary>
    /// Отправить отчёт получателю сейчас во все его чаты (реальные данные, сегодня). Уже отправлен сегодня в этот чат с этой областью —
    /// только с force: повтор явный. Временная ошибка — повтор автоматически (строка «в очереди»).
    /// </summary>
    public async Task<IReadOnlyList<ReportSendResult>> SendNowAsync(Guid recipientId, ReportSendInput input, bool canUseSales, Guid userId, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var recipient = await db.TelegramReportSubscriptions.AsNoTracking().Include(s => s.Chats).FirstOrDefaultAsync(s => s.Id == recipientId, ct)
                        ?? throw new KeyNotFoundException();
        var job = ReportCatalog.Find(input.Job)?.Code ?? throw new ReportValidationException("Неизвестный отчёт.");
        if (!ReportCatalog.Find(job)!.Roles.Contains(recipient.Role))
        {
            throw new ReportValidationException("Этот отчёт — не для роли получателя.");
        }

        var settings = await SettingsAsync(ct);
        var account = settings.AccountId ?? throw new ReportValidationException("Сначала выберите аккаунт Telegram для автоотчётов.");
        var period = Today();
        var scope = ReportScope.Of(recipient);
        var chatIds = recipient.Chats.Select(c => c.ChatId).ToArray();
        if (chatIds.Length == 0)
        {
            throw new ReportValidationException("У получателя нет чатов.");
        }

        var key = scope.Key;
        var previous = await db.TelegramReportDeliveries.AsNoTracking()
            .Where(d => d.Report == job && d.Period == period && d.ScopeKey == key && d.ChatId != null && chatIds.Contains(d.ChatId.Value) && d.Trigger != "test"
                        && (d.Status == TelegramReportSender.Sent || d.Status == TelegramReportSender.Sending || d.Status == TelegramReportSender.Pending))
            .ToListAsync(ct);
        if (previous.Any(d => d.Status is TelegramReportSender.Sending or TelegramReportSender.Pending))
        {
            throw new ReportConflictException("Этот отчёт получателю уже отправляется — дождитесь результата в истории.");
        }

        if (previous.FirstOrDefault(d => d.Status == TelegramReportSender.Sent) is { } sentBefore && !input.Force)
        {
            var at = sentBefore.SentAt is { } s ? ReportSchedules.Local(s, ReportSchedules.DefaultTimeZone).ToString("HH:mm") : "сегодня";
            throw new ReportConflictException($"«{ReportCatalog.Title(ReportCatalog.Find(job)!.Report)}» этому получателю сегодня уже отправлен ({at}). Отправить ещё раз?");
        }

        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.send", nameof(TelegramReportSubscription), recipientId.ToString(),
            new { job, input.Force }, ct);
        var chats = await db.TelegramChats.AsNoTracking().Where(c => chatIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var results = new List<ReportSendResult>();
        foreach (var chatId in chatIds)
        {
            var chat = chats.GetValueOrDefault(chatId);
            var delivery = new TelegramReportDelivery
            {
                Report = job,
                Period = period,
                Trigger = "manual",
                DataMode = "real",
                SubscriptionId = recipient.Id,
                Role = recipient.Role,
                ScopeKey = key,
                AccountId = account,
                ChatId = chatId,
                PeerId = chat?.PeerId,
                RecipientTitle = TelegramReportSender.Trim(chat?.Title ?? "—", 255),
                ScopeTitle = "…",
                RegionId = recipient.RegionId,
                SupervisorId = recipient.SupervisorId,
                CreatedById = userId,
            };
            db.TelegramReportDeliveries.Add(delivery);
            await db.SaveChangesAsync(ct);
            if (TelegramReportSender.ChatProblem(chat, account) is { } problem)
            {
                results.Add(await sender.SkipAsync(delivery, problem));
                continue;
            }

            results.Add(await sender.AttemptAsync(delivery, chat!, token => sender.BuildAsync(job, scope, period, null, null, token), retry: true, ct));
        }

        return results;
    }

    /// <summary>
    /// Повтор неудачной отправки: «ошибка» — сразу; «неизвестно» (тайм-аут посреди отправки) — только с force (администратор проверил чат).
    /// Тот же отчёт той же области в этот чат за этот день уже доставлен — отказ: дубль не уходит.
    /// </summary>
    public async Task<ReportSendResult> RetryAsync(Guid deliveryId, bool force, bool canUseSales, Guid userId, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var delivery = await db.TelegramReportDeliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct) ?? throw new KeyNotFoundException();
        if (delivery.Report is null || ReportScope.Parse(delivery.ScopeKey) is not { } scope)
        {
            throw new ReportValidationException("Это отправка прежнего вида — её нельзя повторить.");
        }

        if (delivery.Status is not (TelegramReportSender.Failed or TelegramReportSender.Unknown))
        {
            throw new ReportValidationException("Повторить можно только неудачную отправку.");
        }

        if (delivery.Status == TelegramReportSender.Unknown && !force)
        {
            throw new ReportConflictException("Неизвестно, дошёл ли отчёт: Telegram мог его принять. Проверьте чат — если отчёта там нет, подтвердите повтор.");
        }

        // Отправка по расписанию — одна на запуск, день, чат и область: если тот же отчёт туда уже дошёл, повтор дал бы дубль. Ручная отправка
        // и так явная (повтор за день — только с подтверждением).
        var delivered = delivery.Trigger == "schedule" && await db.TelegramReportDeliveries.AsNoTracking().AnyAsync(d => d.Id != delivery.Id
            && d.Report == delivery.Report && d.Period == delivery.Period && d.ChatId == delivery.ChatId && d.ScopeKey == delivery.ScopeKey && d.Trigger != "test"
            && d.Status == TelegramReportSender.Sent, ct);
        if (delivered)
        {
            throw new ReportConflictException("Этот отчёт в этот чат за этот день уже доставлен другой отправкой — повтор не нужен.");
        }

        var settings = await SettingsAsync(ct);
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == delivery.ChatId, ct);
        if (TelegramReportSender.ChatProblem(chat, settings.AccountId) is { } problem)
        {
            throw new ReportValidationException(problem);
        }

        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.retry", nameof(TelegramReportDelivery), deliveryId.ToString(), new { force }, ct);
        delivery.AccountId = settings.AccountId ?? delivery.AccountId;
        delivery.Error = null;
        var job = delivery.Report;
        var period = delivery.Period;
        return await sender.AttemptAsync(delivery, chat!, token => sender.BuildAsync(job, scope, period, null, null, token), retry: true, ct);
    }

    /// <summary>Запуск, область, день, момент формирования и язык предпросмотра или теста: явно или по получателю.</summary>
    private async Task<(string Job, ReportScope Scope, DateOnly Date, DateTimeOffset? At, string? Language)> TargetAsync(ReportPreviewInput input, bool canUseSales,
        CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var info = ReportCatalog.Find(input.Job) ?? throw new ReportValidationException("Выберите отчёт.");
        ReportScope scope;
        if (input.RecipientId is { } recipientId)
        {
            var s = await db.TelegramReportSubscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == recipientId, ct) ?? throw new KeyNotFoundException();
            scope = ReportScope.Of(s);
        }
        else
        {
            var role = input.Role?.Trim().ToLowerInvariant() ?? TelegramReportRoles.Sv;
            scope = role switch
            {
                TelegramReportRoles.Rm => new ReportScope(role, DirectionId: input.DirectionId),
                TelegramReportRoles.Tp => new ReportScope(role, AgentId: input.AgentId ?? throw new ReportValidationException("Выберите ТП.")),
                TelegramReportRoles.Sv => new ReportScope(role, RegionId: input.RegionId ?? throw new ReportValidationException("Выберите регион."), SupervisorId: input.SupervisorId),
                _ => throw new ReportValidationException("Роль — РМ, СВ или ТП."),
            };
        }

        if (!info.Roles.Contains(scope.Role))
        {
            throw new ReportValidationException("Этот отчёт — не для выбранной роли (напоминание 17:00 — только ТП).");
        }

        var job = await db.TelegramReportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Code == info.Code, ct) ?? ReportCatalog.Default(info);
        var now = ReportSchedules.Local(clock.GetUtcNow(), job.TimeZone);
        var date = input.Date ?? DateOnly.FromDateTime(now.DateTime);
        DateTimeOffset? at = null;
        if (ReportSchedules.ParseTime(input.Time) is { } time)
        {
            at = ReportSchedules.At(date, time, job.TimeZone);
        }
        else if (input.Time is { Length: > 0 })
        {
            throw new ReportValidationException("Время формирования — ЧЧ:ММ.");
        }
        else if (input.Date is { } d && d != DateOnly.FromDateTime(now.DateTime))
        {
            at = ReportSchedules.At(d, job.Time, job.TimeZone); // прошлый день — на время запуска
        }

        var language = input.Language?.Trim().ToLowerInvariant() is "uz" or "ru" ? input.Language.Trim().ToLowerInvariant() : null;
        return (info.Code, scope, date, at, language);
    }

    private DateOnly Today() => DateOnly.FromDateTime(ReportSchedules.Local(clock.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime);

    // ---------- Дневные планы ТП («ТП без заказов») и праздники ----------

    /// <summary>Дневные планы на день: действующие строки и история пересчётов; имена и регионы — из оргструктуры «Продаж» месяца.</summary>
    public async Task<ReportDailyPlans> DailyPlansAsync(DateOnly? date, bool canUseSales, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var day = date ?? Today();
        var rows = await db.TelegramReportDailyPlans.AsNoTracking().Where(p => p.Date == day).OrderBy(p => p.ComputedAt).ToListAsync(ct);
        var (teams, _) = await engine.StructureAsync(day, ct);
        var regionOf = teams.SelectMany(t => t.Agents.Select(a => (a.Id, t.Name))).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Name);
        var ids = rows.Select(r => r.LinkoUserId).Distinct().ToArray();
        var names = (await db.LinkoUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(ct)).ToDictionary(u => u.Id, u => u.DisplayName);
        var userIds = rows.Where(r => r.CreatedById != null).Select(r => r.CreatedById!.Value).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        ReportDailyPlanView View(TelegramReportDailyPlan p) => new(p.Id, p.LinkoUserId, names.GetValueOrDefault(p.LinkoUserId, $"ТП {p.LinkoUserId}"),
            regionOf.GetValueOrDefault(p.LinkoUserId), p.Date, p.MonthPlanSum, p.FactBeforeSum, p.RemainingSum, p.WorkDaysLeft, p.DailyPlanSum, p.Status, p.Note,
            p.Trigger, p.Reason, p.ComputedAt, p.SupersededAt, p.CreatedById is { } by ? users.GetValueOrDefault(by) : null);
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        var holidays = await dailyPlans.HolidaysAsync(monthStart, monthStart.AddMonths(1).AddDays(-1), ct);
        return new ReportDailyPlans(day, DailyPlanMath.IsWorkingDay(day, holidays), DailyPlanMath.WorkDaysLeft(day, holidays),
            rows.Where(r => r.SupersededAt == null).Select(View).OrderBy(v => v.Region).ThenBy(v => v.Agent).ToList(),
            rows.Where(r => r.SupersededAt != null).Select(View).OrderByDescending(v => v.ComputedAt).ToList());
    }

    /// <summary>Пересчёт администратором с причиной: прежние строки дня остаются историей, новые считаются по текущим данным.</summary>
    public async Task<ReportDailyPlans> RecalculateDailyPlansAsync(DailyPlanRecalcInput input, bool canUseSales, Guid userId, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var reason = input.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new ReportValidationException("Укажите причину пересчёта.");
        }

        var day = input.Date ?? Today();
        var added = await dailyPlans.RecalculateAsync(day, input.AgentId, reason[..Math.Min(reason.Length, 300)], userId, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.daily_plan_recalculated", nameof(TelegramReportDailyPlan), day.ToString("yyyy-MM-dd"),
            new { input.AgentId, reason, Rows = added.Count }, ct);
        return await DailyPlansAsync(day, true, ct);
    }

    /// <summary>Справочник праздников OneBase за год (нерабочие дни помимо воскресений).</summary>
    public async Task<IReadOnlyList<ReportHolidayView>> HolidaysAsync(int? year, CancellationToken ct)
    {
        var y = year ?? Today().Year;
        var (from, to) = (new DateOnly(y, 1, 1), new DateOnly(y, 12, 31));
        return await db.SalesHolidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date)
            .Select(h => new ReportHolidayView(h.Id, h.Date, h.Name)).ToListAsync(ct);
    }

    public async Task<ReportHolidayView> AddHolidayAsync(HolidayInput input, Guid userId, CancellationToken ct)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ReportValidationException("Укажите название праздника.");
        }

        if (input.Date.DayOfWeek == DayOfWeek.Sunday)
        {
            throw new ReportValidationException("Воскресенье и так нерабочий день — в справочник не нужно.");
        }

        if (await db.SalesHolidays.AnyAsync(h => h.Date == input.Date, ct))
        {
            throw new ReportConflictException("Эта дата уже в справочнике праздников.");
        }

        var row = new SalesHoliday { Date = input.Date, Name = name[..Math.Min(name.Length, 200)], CreatedById = userId };
        db.SalesHolidays.Add(row);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "sales.holiday_added", nameof(SalesHoliday), row.Id.ToString(), new { row.Date, row.Name }, ct);
        return new ReportHolidayView(row.Id, row.Date, row.Name);
    }

    public async Task DeleteHolidayAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await db.SalesHolidays.FirstOrDefaultAsync(h => h.Id == id, ct) ?? throw new KeyNotFoundException();
        db.SalesHolidays.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "sales.holiday_deleted", nameof(SalesHoliday), id.ToString(), new { row.Date, row.Name }, ct);
    }

    // ---------- История ----------

    public async Task<ReportHistory> HistoryAsync(ReportHistoryQuery query, CancellationToken ct)
    {
        var q = db.TelegramReportDeliveries.AsNoTracking().Where(d => d.Report != null);
        if (query.From is { } from)
        {
            q = q.Where(d => d.Period >= from);
        }

        if (query.To is { } to)
        {
            q = q.Where(d => d.Period <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Report))
        {
            var report = query.Report.Trim();
            q = report.Contains('.') ? q.Where(d => d.Report == report) : q.Where(d => d.Report!.StartsWith(report + "."));
        }

        if (query.RegionId is { } region)
        {
            q = q.Where(d => d.RegionId == region);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            q = q.Where(d => d.Status == query.Status);
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var rows = await q.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        var userIds = rows.Where(r => r.CreatedById != null && r.Trigger != "schedule").Select(r => r.CreatedById!.Value).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var items = rows.Select(d => new ReportDeliveryView(d.Id, d.CreatedAt, d.Report!, ReportCatalog.Find(d.Report) is { } info ? ReportCatalog.Title(info.Report) : d.Report!,
            d.Period, d.Trigger, d.Role, d.RecipientTitle, d.ScopeTitle, d.RegionId, d.Status, d.Attempts, d.SentAt, d.NextAttemptAt, d.Error, d.Pages, d.Bytes, d.Summary,
            d.DataAsOf, d.DurationMs, d.Trigger != "schedule" && d.CreatedById is { } u ? names.GetValueOrDefault(u) : null,
            d.Trigger != "test" && d.Status is TelegramReportSender.Failed or TelegramReportSender.Unknown && ReportScope.Parse(d.ScopeKey) is not null)).ToList();

        var runs = db.TelegramReportRuns.AsNoTracking();
        if (query.From is { } rf)
        {
            runs = runs.Where(r => r.Period >= rf);
        }

        if (query.To is { } rt)
        {
            runs = runs.Where(r => r.Period <= rt);
        }

        var runRows = await runs.OrderByDescending(r => r.StartedAt).Take(40).ToListAsync(ct);
        var runViews = runRows.Select(r => new ReportRunView(r.Id, r.Job, ReportCatalog.Find(r.Job) is { } info ? $"{ReportCatalog.Title(info.Report)} · {ReportCatalog.SlotTitle(info.Report, info.Slot)}" : r.Job,
            r.Period, r.ScheduledAt, r.StartedAt, r.FinishedAt, r.Status, r.Planned, r.Sent, r.Failed, r.Skipped, r.Error, r.DataAsOf)).ToList();
        return new ReportHistory(items, total, page, PageSize, runViews);
    }
}

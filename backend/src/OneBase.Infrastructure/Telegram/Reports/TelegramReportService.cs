using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// «Настройки → Telegram → Автоматические отчёты»: аккаунт и язык, три расписания (включаются отдельно — сохранение ничего не
/// отправляет), автоотправки «регион → супервайзер → чаты», предпросмотр, тестовая и ручная отправка, журнал. Регионы, супервайзеры и
/// агенты — реальные данные раздела «Продажи»; реальные цифры — только тому, у кого есть право на продажи (sales.read).
/// </summary>
public sealed class TelegramReportService(
    IAppDbContext db,
    TelegramReportSender sender,
    DailyReportBuilder builder,
    IDailyReportRenderer renderer,
    IAuditLogger audit,
    IConfiguration config,
    TimeProvider clock)
{
    private static readonly string[] Metrics = ["sum", "kg"];
    private static readonly string[] Languages = ["ru", "uz"];

    private const string NoSales = "Нет права на данные продаж (sales.read) — доступны только демонстрационные данные.";

    // ---------- Обзор ----------

    public async Task<ReportOverview> OverviewAsync(bool canUseSales, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var accounts = await db.TelegramAccounts.AsNoTracking().OrderBy(a => a.CreatedAt)
            .Select(a => new ReportAccountOption(a.Id, a.Name, a.Status.ToString(), a.Username)).ToListAsync(ct);
        var teams = canUseSales ? await builder.TeamsAsync(Today(), ct) : [];
        var regions = teams.Select(r => new ReportRegionOption(r.Id, r.Name, r.Manager, r.SupervisorName,
                r.Supervisors.Select(s => new ReportPersonOption(s.Id, s.Name)).ToList(), r.Agents.Select(a => a.Name).ToList()))
            .ToList();
        var chats = settings.AccountId is { } account ? await ChatsAsync(account, ct) : [];
        return new ReportOverview(new ReportSettingsView(settings.AccountId, settings.Language, settings.UpdatedAt), accounts, await SchedulesAsync(ct),
            await SubscriptionsAsync(settings.AccountId, teams, ct), regions, chats, canUseSales, config.GetValue("Telegram:Enabled", true));
    }

    private async Task<TelegramReportSettings> SettingsAsync(CancellationToken ct) =>
        await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new TelegramReportSettings();

    private async Task<List<ReportScheduleView>> SchedulesAsync(CancellationToken ct)
    {
        var stored = await db.TelegramReportSchedules.AsNoTracking().ToListAsync(ct);
        var now = clock.GetUtcNow();
        return Enum.GetValues<TelegramReportKind>()
            .Select(k => stored.FirstOrDefault(s => s.Kind == k) ?? DailyReportSchedules.Default(k))
            .Select(s => new ReportScheduleView(s.Kind.ToString(), s.Enabled, s.Time.ToString("HH:mm"), s.TimeZone, DailyReportSchedules.IsoDays(s.Days), s.Metric,
                DailyReportSchedules.Next(s, now), s.UpdatedAt))
            .ToList();
    }

    /// <summary>Чаты аккаунта: личные и группы; каналы — только где аккаунт может публиковать.</summary>
    private async Task<List<ReportChatOption>> ChatsAsync(Guid accountId, CancellationToken ct) =>
        await db.TelegramChats.AsNoTracking()
            .Where(c => c.AccountId == accountId && (c.Type != TelegramChatType.Channel || c.CanSend))
            .OrderBy(c => c.Type).ThenBy(c => c.Title)
            .Select(c => new ReportChatOption(c.Id, c.Title, c.Type.ToString(), c.Username, c.CanSend, c.CannotSendReason))
            .ToListAsync(ct);

    private async Task<List<ReportSubscriptionView>> SubscriptionsAsync(Guid? accountId, IReadOnlyList<ReportRegionTeam> teams, CancellationToken ct)
    {
        var rows = await db.TelegramReportSubscriptions.AsNoTracking().Include(s => s.Chats).OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var chatIds = rows.SelectMany(r => r.Chats.Select(c => c.ChatId)).Distinct().ToArray();
        var chats = await db.TelegramChats.AsNoTracking().Where(c => chatIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var regionIds = rows.Select(r => r.RegionId).Distinct().ToArray();
        var regionNames = await db.SalesRegions.AsNoTracking().Where(r => regionIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => new { r.Name, r.SupervisorName }, ct);
        var ids = rows.Select(r => r.Id).ToArray();
        var last = await db.TelegramReportDeliveries.AsNoTracking()
            .Where(d => d.SubscriptionId != null && ids.Contains(d.SubscriptionId.Value))
            .OrderByDescending(d => d.CreatedAt)
            .Take(500)
            .Select(d => new { d.SubscriptionId, d.Kind, d.RecipientTitle, d.Status, d.CreatedAt, d.SentAt, d.Error })
            .ToListAsync(ct);

        var views = new List<ReportSubscriptionView>();
        foreach (var s in rows)
        {
            var team = teams.FirstOrDefault(t => t.Id == s.RegionId);
            var region = team?.Name ?? regionNames.GetValueOrDefault(s.RegionId)?.Name ?? "—";
            var supervisor = s.SupervisorId is { } sid
                ? team?.Supervisors.FirstOrDefault(p => p.Id == sid)?.Name ?? s.SupervisorName ?? $"ID {sid}"
                : team?.SupervisorName ?? regionNames.GetValueOrDefault(s.RegionId)?.SupervisorName ?? "—";
            var chatRefs = s.Chats.Select(c => chats.GetValueOrDefault(c.ChatId)).Where(c => c is not null)
                .Select(c => new ReportChatRef(c!.Id, c.Title, c.Type.ToString(), c.CanSend)).ToList();
            var problem = ProblemOf(s, team, teams.Count > 0, chatRefs, accountId, chats);
            var recent = last.Where(d => d.SubscriptionId == s.Id).GroupBy(d => (d.Kind, d.RecipientTitle)).Select(g => g.First()).Take(6)
                .Select(d => new ReportDeliveryBrief(d.Kind.ToString(), d.RecipientTitle, d.Status, d.SentAt ?? d.CreatedAt, d.Error)).ToList();
            views.Add(new ReportSubscriptionView(s.Id, s.RegionId, region, s.SupervisorId, supervisor, team?.Agents.Count ?? 0, chatRefs, s.Morning, s.Midday, s.Evening,
                s.Enabled, problem, recent));
        }

        return views;
    }

    /// <summary>Почему автоотправка сейчас не уйдёт (или уйдёт не во все чаты).</summary>
    private static string? ProblemOf(TelegramReportSubscription s, ReportRegionTeam? team, bool teamsKnown, IReadOnlyList<ReportChatRef> chats, Guid? accountId,
        IReadOnlyDictionary<Guid, TelegramChat> all)
    {
        if (teamsKnown && team is null)
        {
            return "Регион не найден в данных продаж за этот месяц.";
        }

        if (team is not null && s.SupervisorId is { } sid && team.Supervisors.All(p => p.Id != sid))
        {
            return "Супервайзер больше не числится в этом регионе — выберите другого.";
        }

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
        settings.UpdatedAt = clock.GetUtcNow();
        settings.UpdatedById = userId;
        if (changedAccount)
        {
            // Чаты принадлежат аккаунту: чаты прежнего аккаунта с новым не работают — убираем их из автоотправок явно, а не молча.
            var stale = await db.TelegramReportSubscriptionChats.Where(c => !db.TelegramChats.Any(x => x.Id == c.ChatId && x.AccountId == input.AccountId)).ToListAsync(ct);
            db.TelegramReportSubscriptionChats.RemoveRange(stale);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.settings", nameof(TelegramReportSettings), "1", new { settings.AccountId, settings.Language }, ct);
        return new ReportSettingsView(settings.AccountId, settings.Language, settings.UpdatedAt);
    }

    /// <summary>
    /// Расписание вида отчёта. Включение и смена времени «взводят» его с этого момента: время, которое сегодня уже прошло, не догоняется —
    /// после сохранения ничего не уходит сразу.
    /// </summary>
    public async Task<ReportScheduleView> SaveScheduleAsync(TelegramReportKind kind, ReportScheduleInput input, Guid userId, CancellationToken ct)
    {
        var time = DailyReportSchedules.ParseTime(input.Time) ?? throw new ReportValidationException("Время — в формате ЧЧ:ММ, например 08:00.");
        var zone = string.IsNullOrWhiteSpace(input.TimeZone) ? DailyReportSchedules.DefaultTimeZone : input.TimeZone.Trim();
        if (DailyReportSchedules.Zone(zone) is null)
        {
            throw new ReportValidationException($"Часовой пояс «{zone}» не найден — укажите IANA, например Asia/Tashkent.");
        }

        var days = DailyReportSchedules.DaysOf(input.Days ?? []);
        if (input.Enabled && days == 0)
        {
            throw new ReportValidationException("Выберите хотя бы один день отправки.");
        }

        var metric = input.Metric?.Trim().ToLowerInvariant() ?? "sum";
        if (!Metrics.Contains(metric))
        {
            throw new ReportValidationException("Показатель — sum (сумы) или kg (килограммы).");
        }

        var schedule = await db.TelegramReportSchedules.FirstOrDefaultAsync(s => s.Kind == kind, ct);
        if (schedule is null)
        {
            schedule = DailyReportSchedules.Default(kind);
            db.TelegramReportSchedules.Add(schedule);
        }

        var now = clock.GetUtcNow();
        if (input.Enabled && (!schedule.Enabled || schedule.Time != time || schedule.TimeZone != zone || schedule.Days != days))
        {
            schedule.ArmedAt = now;
        }

        schedule.Enabled = input.Enabled;
        schedule.Time = time;
        schedule.TimeZone = zone;
        schedule.Days = days;
        schedule.Metric = metric;
        schedule.UpdatedAt = now;
        schedule.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.schedule", nameof(TelegramReportSchedule), kind.ToString(),
            new { schedule.Enabled, Time = schedule.Time.ToString("HH:mm"), schedule.TimeZone, schedule.Days, schedule.Metric }, ct);
        return new ReportScheduleView(kind.ToString(), schedule.Enabled, schedule.Time.ToString("HH:mm"), schedule.TimeZone, DailyReportSchedules.IsoDays(schedule.Days),
            schedule.Metric, DailyReportSchedules.Next(schedule, now), schedule.UpdatedAt);
    }

    // ---------- Автоотправки ----------

    /// <summary>Регион — из данных продаж месяца; супервайзер — этого региона (или без супервайзера); чаты — аккаунта, куда можно писать.</summary>
    public async Task<ReportSubscriptionView> SaveSubscriptionAsync(Guid? id, ReportSubscriptionInput input, bool canUseSales, Guid userId, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        if (!input.Morning && !input.Midday && !input.Evening)
        {
            throw new ReportValidationException("Отметьте хотя бы один отчёт: утро, обед или вечер.");
        }

        var chatIds = (input.ChatIds ?? []).Distinct().ToList();
        if (chatIds.Count == 0)
        {
            throw new ReportValidationException("Выберите хотя бы один чат или группу.");
        }

        var teams = await builder.TeamsAsync(Today(), ct);
        var team = teams.FirstOrDefault(t => t.Id == input.RegionId) ?? throw new ReportValidationException("Регион не найден в данных продаж за этот месяц.");
        string? supervisorName = null;
        if (input.SupervisorId is { } sid)
        {
            supervisorName = team.Supervisors.FirstOrDefault(s => s.Id == sid)?.Name ?? throw new ReportValidationException("Этот супервайзер не числится в выбранном регионе.");
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
        row.RegionId = input.RegionId;
        row.SupervisorId = input.SupervisorId;
        row.SupervisorName = supervisorName;
        row.Morning = input.Morning;
        row.Midday = input.Midday;
        row.Evening = input.Evening;
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
        await audit.LogAsync(ActorType.User, userId.ToString(), id is null ? "telegram.reports.subscription_created" : "telegram.reports.subscription_updated",
            nameof(TelegramReportSubscription), row.Id.ToString(),
            new { row.RegionId, row.SupervisorId, Chats = chatIds, row.Morning, row.Midday, row.Evening, row.Enabled }, ct);
        return (await SubscriptionsAsync(account, teams, ct)).First(s => s.Id == row.Id);
    }

    public async Task DeleteSubscriptionAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await db.TelegramReportSubscriptions.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new KeyNotFoundException();
        db.TelegramReportSubscriptions.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.subscription_deleted", nameof(TelegramReportSubscription), id.ToString(), null, ct);
    }

    // ---------- Предпросмотр, тест, ручная отправка ----------

    public async Task<ReportPreview> PreviewAsync(ReportPreviewInput input, bool canUseSales, CancellationToken ct)
    {
        var report = await BuildForAsync(input, canUseSales, check: false, ct);
        var pages = renderer.Render(report);
        return new ReportPreview(pages.Select(p => "data:image/png;base64," + Convert.ToBase64String(p)).ToList(), DailyReportText.Caption(report),
            DailyReportText.Summary(report), report.Notes, DailyReportMath.Verify(report), pages.Sum(p => (long)p.Length));
    }

    /// <summary>
    /// Тестовая отправка в выбранный чат. Демонстрационные данные — в любой чат аккаунта (на картинке «ТЕСТОВЫЕ ДАННЫЕ»); реальные — только
    /// с правом на продажи. Строка журнала — с режимом данных; повторов нет: результат сразу.
    /// </summary>
    public async Task<ReportSendResult> TestAsync(ReportTestInput input, bool canUseSales, Guid userId, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var account = settings.AccountId ?? throw new ReportValidationException("Сначала выберите аккаунт Telegram для автоотчётов.");
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.ChatId, ct);
        if (TelegramReportSender.ChatProblem(chat, account) is { } problem)
        {
            throw new ReportValidationException(problem);
        }

        var real = IsReal(input.Report);
        if (real && !canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var kind = KindOf(input.Report.Kind);
        var (regionId, supervisorId, supervisorName) = real ? await TargetOfAsync(input.Report, ct) : (Guid.Empty, null, null);
        var scopeTitle = real
            ? await sender.ScopeTitleAsync(regionId, supervisorId, supervisorName, ct)
            : string.IsNullOrWhiteSpace(input.Report.Region) ? "Демо: Ташкентский регион" : $"Демо: {input.Report.Region.Trim()}";
        var delivery = new TelegramReportDelivery
        {
            Kind = kind,
            Period = Today(),
            Trigger = "test",
            DataMode = real ? "real" : "demo",
            AccountId = account,
            ChatId = chat!.Id,
            PeerId = chat.PeerId,
            RecipientTitle = TelegramReportSender.Trim(chat.Title, 255),
            ScopeTitle = TelegramReportSender.Trim(scopeTitle, 255),
            RegionId = real ? regionId : null,
            SupervisorId = supervisorId,
            CreatedById = userId,
        };
        db.TelegramReportDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.test", nameof(TelegramReportDelivery), delivery.Id.ToString(),
            new { Kind = kind.ToString(), delivery.DataMode, chat.Title }, ct);
        return await sender.AttemptAsync(delivery, chat, token => BuildForAsync(input.Report, canUseSales, check: real, token), retry: false, ct);
    }

    /// <summary>
    /// Отправить отчёт автоотправки сейчас во все её чаты (реальные данные). Уже отправлен сегодня — только с force: повтор явный.
    /// Временная ошибка — повтор автоматически (строка «ожидает»).
    /// </summary>
    public async Task<IReadOnlyList<ReportSendResult>> SendNowAsync(Guid subscriptionId, ReportSendInput input, bool canUseSales, Guid userId, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var subscription = await db.TelegramReportSubscriptions.AsNoTracking().Include(s => s.Chats).FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
                           ?? throw new KeyNotFoundException();
        var kind = KindOf(input.Kind);
        var settings = await SettingsAsync(ct);
        var account = settings.AccountId ?? throw new ReportValidationException("Сначала выберите аккаунт Telegram для автоотчётов.");
        var period = Today();
        var chatIds = subscription.Chats.Select(c => c.ChatId).ToArray();
        if (chatIds.Length == 0)
        {
            throw new ReportValidationException("В автоотправке нет чатов.");
        }

        var previous = await db.TelegramReportDeliveries.AsNoTracking()
            .Where(d => d.SubscriptionId == subscriptionId && d.Kind == kind && d.Period == period && d.DataMode == "real"
                        && (d.Status == TelegramReportSender.Sent || d.Status == TelegramReportSender.Sending || d.Status == TelegramReportSender.Pending))
            .ToListAsync(ct);
        if (previous.Any(d => d.Status is TelegramReportSender.Sending or TelegramReportSender.Pending))
        {
            throw new ReportConflictException("Этот отчёт по автоотправке уже отправляется — дождитесь результата в журнале.");
        }

        if (previous.FirstOrDefault(d => d.Status == TelegramReportSender.Sent) is { } sentBefore && !input.Force)
        {
            var at = sentBefore.SentAt is { } s ? DailyReportSchedules.Local(s, DailyReportSchedules.DefaultTimeZone).ToString("HH:mm") : "сегодня";
            throw new ReportConflictException($"«{DailyReportTexts.Name(kind, "ru")}» по этой автоотправке сегодня уже отправлен ({at}). Отправить ещё раз?");
        }

        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.send", nameof(TelegramReportSubscription), subscriptionId.ToString(),
            new { Kind = kind.ToString(), input.Force }, ct);
        var scopeTitle = await sender.ScopeTitleAsync(subscription.RegionId, subscription.SupervisorId, subscription.SupervisorName, ct);
        var chats = await db.TelegramChats.AsNoTracking().Where(c => chatIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var results = new List<ReportSendResult>();
        foreach (var chatId in chatIds)
        {
            var chat = chats.GetValueOrDefault(chatId);
            var delivery = new TelegramReportDelivery
            {
                Kind = kind,
                Period = period,
                Trigger = "manual",
                DataMode = "real",
                SubscriptionId = subscription.Id,
                AccountId = account,
                ChatId = chatId,
                PeerId = chat?.PeerId,
                RecipientTitle = TelegramReportSender.Trim(chat?.Title ?? "—", 255),
                ScopeTitle = scopeTitle,
                RegionId = subscription.RegionId,
                SupervisorId = subscription.SupervisorId,
                CreatedById = userId,
            };
            db.TelegramReportDeliveries.Add(delivery);
            await db.SaveChangesAsync(ct);
            if (TelegramReportSender.ChatProblem(chat, account) is { } problem)
            {
                results.Add(await sender.FailAsync(delivery, problem));
                continue;
            }

            results.Add(await sender.AttemptAsync(delivery, chat!,
                token => sender.BuildRealAsync(kind, subscription.RegionId, subscription.SupervisorId, subscription.SupervisorName, period, check: false, token), retry: true, ct));
        }

        return results;
    }

    private async Task<DailyReport> BuildForAsync(ReportPreviewInput input, bool canUseSales, bool check, CancellationToken ct)
    {
        var kind = KindOf(input.Kind);
        var schedule = await db.TelegramReportSchedules.AsNoTracking().FirstOrDefaultAsync(s => s.Kind == kind, ct) ?? DailyReportSchedules.Default(kind);
        var settings = await SettingsAsync(ct);
        var metricText = input.Metric?.Trim().ToLowerInvariant() is { Length: > 0 } m ? m : schedule.Metric;
        var metric = metricText == "kg" ? ReportMetric.Kg : ReportMetric.Sum;
        var language = input.Language?.Trim().ToLowerInvariant() is "uz" or "ru" ? input.Language.Trim().ToLowerInvariant() : settings.Language;
        var now = DailyReportSchedules.Local(clock.GetUtcNow(), schedule.TimeZone);
        var date = DateOnly.FromDateTime(now.DateTime);
        if (!IsReal(input))
        {
            return DailyReportDemo.Build(kind, new DemoReportInput(input.Region, 1, input.Agents ?? 8), metric, language, date, now);
        }

        if (!canUseSales)
        {
            throw new ReportForbiddenException(NoSales);
        }

        var (regionId, supervisorId, supervisorName) = await TargetOfAsync(input, ct);
        return await builder.BuildAsync(kind, regionId, supervisorId, supervisorName, date, metric, language, now, check, ct);
    }

    /// <summary>Регион и супервайзер для реальных данных: явно или по автоотправке.</summary>
    private async Task<(Guid RegionId, long? SupervisorId, string? SupervisorName)> TargetOfAsync(ReportPreviewInput input, CancellationToken ct)
    {
        if (input.SubscriptionId is { } subscriptionId)
        {
            var s = await db.TelegramReportSubscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == subscriptionId, ct) ?? throw new KeyNotFoundException();
            return (s.RegionId, s.SupervisorId, s.SupervisorName);
        }

        return (input.RegionId ?? throw new ReportValidationException("Выберите регион."), input.SupervisorId, null);
    }

    private static bool IsReal(ReportPreviewInput input) => string.Equals(input.Mode, "real", StringComparison.OrdinalIgnoreCase);

    private static TelegramReportKind KindOf(string? kind) =>
        Enum.TryParse<TelegramReportKind>(kind, ignoreCase: true, out var k) ? k : throw new ReportValidationException("Вид отчёта — Morning, Midday или Evening.");

    private DateOnly Today() => DateOnly.FromDateTime(DailyReportSchedules.Local(clock.GetUtcNow(), DailyReportSchedules.DefaultTimeZone).DateTime);

    // ---------- Журнал ----------

    public async Task<IReadOnlyList<ReportDeliveryView>> DeliveriesAsync(string? kind, string? status, string? mode, CancellationToken ct)
    {
        var q = db.TelegramReportDeliveries.AsNoTracking();
        if (Enum.TryParse<TelegramReportKind>(kind, true, out var k))
        {
            q = q.Where(d => d.Kind == k);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(d => d.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            q = q.Where(d => d.DataMode == mode);
        }

        var rows = await q.OrderByDescending(d => d.CreatedAt).Take(300).ToListAsync(ct);
        var userIds = rows.Where(r => r.CreatedById != null && r.Trigger != "schedule").Select(r => r.CreatedById!.Value).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return rows.Select(d => new ReportDeliveryView(d.Id, d.CreatedAt, d.Kind.ToString(), d.Period, d.Trigger, d.DataMode, d.RecipientTitle, d.ScopeTitle, d.Status, d.Attempts,
            d.SentAt, d.NextAttemptAt, d.Error, d.Pages, d.Bytes, d.Summary, d.DataAsOf,
            d.Trigger != "schedule" && d.CreatedById is { } u ? names.GetValueOrDefault(u) : null)).ToList();
    }
}

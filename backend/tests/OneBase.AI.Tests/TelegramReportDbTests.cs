using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// Автоотчёты на PostgreSQL и подставном Telegram (настоящих отправок нет): регионы, супервайзеры и ТП — из раздела «Продажи»; получатели
/// РМ / СВ / ТП с проверкой области, чатов и отчётов роли; запуск по расписанию уходит в каждый чат один раз (повторный круг и второй
/// получатель той же области дубля не дают); напоминание 17:00 ТП с заказами — «пропущено»; запуск, пропущенный после простоя, — «пропущен»;
/// тест — только в выбранный чат; «неизвестно, дошёл ли» не повторяется само; повтор неудачных; история с фильтрами.
/// </summary>
public class TelegramReportDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private sealed record Seeded(Guid AccountId, Guid AdminId, Guid RegionId, Guid GroupChatId, Guid PrivateChatId, Guid AgentChatId, Guid OtherChatId, DateOnly Today);

    private const long SupervisorId = 71;
    private const long GroupPeer = -100_500;
    private const long PrivatePeer = 9_001;
    private const long AgentPeer = 9_002;
    private const long OtherPeer = 9_003;
    private static readonly int[] EveryDay = [1, 2, 3, 4, 5, 6, 7];

    private static DateTimeOffset Local(DateOnly day, int hour, int minute = 0) => new(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(5));

    /// <summary>То же время по Ташкенту в UTC — так его отдают часы сервера и так оно хранится в базе.</summary>
    private static DateTimeOffset Utc(DateOnly day, int hour, int minute = 0) => Local(day, hour, minute).ToUniversalTime();

    private async Task<Seeded> SeedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.TelegramReportDeliveries.ExecuteDeleteAsync();
        await db.TelegramReportRuns.ExecuteDeleteAsync();
        await db.TelegramReportJobs.ExecuteDeleteAsync();
        await db.TelegramReportSubscriptions.ExecuteDeleteAsync();
        await db.TelegramReportSettings.ExecuteDeleteAsync();
        fixture.Telegram.Reset();
        var today = FieldClock.Today;
        fixture.Clock.Now = Utc(today, 8);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var admin = new User { Login = $"rep-admin-{suffix}", FullName = "Администратор отчётов" };
        var shavkat = new User { Login = $"rep-sv-{suffix}", FullName = "Азиз Каримов (OneBase)" };
        db.Users.AddRange(admin, shavkat);

        // Хоразм (филиал 5): СВР — «Азиз Каримов» (в оргструктуре и в Linko), три ТП: у 61 и 62 сегодня заказы, у 63 — нет.
        var region = await db.SalesRegions.FirstAsync(r => r.LinkoBranchId == 5);
        region.SupervisorName = "Азиз Каримов";
        long[] agents = [61, 62, 63];
        await db.LinkoUsers.Where(u => u.Id == SupervisorId || agents.Contains(u.Id)).ExecuteDeleteAsync();
        db.LinkoUsers.AddRange(
            new LinkoUser { Id = SupervisorId, FirstName = "Азиз", SecondName = "Каримов", IsActive = true, JobName = "Супервайзер" },
            new LinkoUser { Id = 61, FirstName = "Бахтиёр", SecondName = "Юлдашев", IsActive = true, JobName = "Агент" },
            new LinkoUser { Id = 62, FirstName = "Дилшод", SecondName = "Рахимов", IsActive = true, JobName = "Агент" },
            new LinkoUser { Id = 63, FirstName = "Отабек", SecondName = "Назаров", IsActive = true, JobName = "Агент" });
        await db.SalesStaffPlans.Where(p => agents.Contains(p.LinkoUserId)).ExecuteDeleteAsync();
        var indicator = 1;
        foreach (var (agent, sum, kg) in new[] { (61L, 780_000_000m, 26_000m), (62L, 650_000_000m, 21_000m), (63L, 400_000_000m, 13_000m) })
        {
            db.SalesStaffPlans.AddRange(
                new SalesStaffPlan
                {
                    Year = today.Year, Month = today.Month, LinkoUserId = agent, IndicatorId = indicator++, IndicatorName = "Выручка", PlanType = StaffPlanTypes.SalesSum,
                    PlanAmount = sum, LoadedAt = DateTimeOffset.UtcNow,
                },
                new SalesStaffPlan
                {
                    Year = today.Year, Month = today.Month, LinkoUserId = agent, IndicatorId = indicator++, IndicatorName = "Кекс Хоразм", PlanType = StaffPlanTypes.SalesWeight,
                    PlanAmount = kg, LoadedAt = DateTimeOffset.UtcNow,
                });
        }

        // 800_000_099 и ТП 69 — из теста дневных планов: чтобы тесты не влияли друг на друга, сев начинает с чистого листа.
        long[] ids = Enumerable.Range(0, 12).Select(i => 800_000_001L + i).Append(800_000_099L).ToArray();
        await db.LinkoOrderLines.Where(l => ids.Contains(l.Id)).ExecuteDeleteAsync();
        await db.LinkoOrders.Where(o => ids.Contains(o.Id)).ExecuteDeleteAsync();
        await db.SalesStaffPlans.Where(p => p.LinkoUserId == 69).ExecuteDeleteAsync();
        await db.LinkoUsers.Where(u => u.Id == 69).ExecuteDeleteAsync();
        await db.TelegramReportDailyPlans.ExecuteDeleteAsync();
        var id = 800_000_001L;

        void Add(long agent, DateOnly day, int hour, decimal sum, decimal kg, long market, decimal? discount = null, string status = "delivered")
        {
            var at = day.ToDateTime(new TimeOnly(hour, 0));
            var accepted = status == "delivered"; // свежий заказ (not_delivered, cancelled) приёмки ещё не имеет
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = id, CreatedAt = at, CreatedDate = day, AcceptedAt = accepted ? at : null, AcceptedDate = accepted ? day : null, Status = status, MarketId = market,
                BranchId = 5, BranchName = "Хоразм", AgentId = agent, TotalPrice = sum, TotalWeight = kg,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine
            {
                Id = id, OrderId = id, ProductId = 100, Amount = kg, Price = sum / kg, TotalPrice = sum, TotalWeight = kg, TotalDiscount = discount,
            });
            id++;
        }

        // Вчера — команда региона в «Продажах»; 40 дней назад — последняя покупка точки 1102 (без закупа 30+ дней); сегодня — факт отчётов:
        // как в Linko днём, свежие заказы ещё не отгружены (not_delivered, без приёмки) — они и есть факт дня; отменённый — нет.
        foreach (var agent in agents)
        {
            Add(agent, today.AddDays(-1), 11, 1_000_000, 30, 1101);
        }

        Add(61, today.AddDays(-40), 11, 2_500_000, 80, 1102);
        Add(61, today, 9, 25_000_000, 400, 1101, discount: 1_250_000, status: "not_delivered");
        Add(62, today, 10, 15_000_000, 300, 1103, status: "not_delivered");
        Add(62, today, 11, 9_000_000, 100, 1103, status: "cancelled");

        await db.LinkoMarkets.Where(m => m.Id >= 1101 && m.Id <= 1104).ExecuteDeleteAsync();
        db.LinkoMarkets.AddRange(
            new LinkoMarket { Id = 1101, Name = "Магазин «Нур»", ResponsibleAgentId = 61, BranchId = 5, BranchName = "Хоразм" },
            new LinkoMarket { Id = 1102, Name = "Супермаркет «Бахор»", ResponsibleAgentId = 61, BranchId = 5, BranchName = "Хоразм" },
            new LinkoMarket { Id = 1103, Name = "Минимаркет «Сарбон»", ResponsibleAgentId = 62, BranchId = 5, BranchName = "Хоразм" },
            new LinkoMarket { Id = 1104, Name = "Тест магазин", ResponsibleAgentId = 61, BranchId = 5, BranchName = "Хоразм" });
        await db.LinkoVisits.Where(v => v.Id >= 700_001 && v.Id <= 700_010).ExecuteDeleteAsync();
        db.LinkoVisits.AddRange(
            new LinkoVisit { Id = 700_001, UserId = 61, MarketId = 1101, Day = today, Date = today.ToDateTime(new TimeOnly(9, 0)), IsInPlan = true, Status = "done" },
            new LinkoVisit { Id = 700_002, UserId = 61, MarketId = 1102, Day = today, Date = today.ToDateTime(new TimeOnly(0, 0)), IsInPlan = true, Status = "planned" },
            new LinkoVisit { Id = 700_003, UserId = 63, MarketId = 1101, Day = today, Date = today.ToDateTime(new TimeOnly(0, 0)), IsInPlan = true, Status = "planned" });

        foreach (var entity in new[] { "orders", "visits" })
        {
            var state = await db.LinkoSyncStates.FirstOrDefaultAsync(s => s.Entity == entity);
            if (state is null)
            {
                db.LinkoSyncStates.Add(state = new LinkoSyncState { Entity = entity });
            }

            state.LastSuccessAt = Utc(today, 7, 55);
        }

        var account = new TelegramAccount { Name = $"Отчёты {suffix}", Status = TelegramAccountStatus.Connected, CreatedById = admin.Id };
        db.TelegramAccounts.Add(account);
        var group = new TelegramChat { AccountId = account.Id, PeerId = GroupPeer, Type = TelegramChatType.Group, Title = "Хоразм — продажи" };
        var chat = new TelegramChat { AccountId = account.Id, PeerId = PrivatePeer, Type = TelegramChatType.Private, Title = "Азиз", AccessHash = 1 };
        var agentChat = new TelegramChat { AccountId = account.Id, PeerId = AgentPeer, Type = TelegramChatType.Private, Title = "Отабек", AccessHash = 2 };
        var other = new TelegramChat { AccountId = account.Id, PeerId = OtherPeer, Type = TelegramChatType.Private, Title = "Бахтиёр", AccessHash = 3 };
        var blocked = new TelegramChat
        {
            AccountId = account.Id, PeerId = 9_004, Type = TelegramChatType.Private, Title = "Заблокировал", CanSend = false, CannotSendReason = "Пользователь заблокировал аккаунт.",
        };
        db.TelegramChats.AddRange(group, chat, agentChat, other, blocked);
        // Личный чат Азиза — его Telegram подтверждён кодом привязки к пользователю OneBase.
        db.TelegramUserPermissions.Add(new TelegramUserPermission
        {
            AccountId = account.Id, TelegramUserId = PrivatePeer, Title = "Азиз", UserId = shavkat.Id, VerifiedAt = DateTimeOffset.UtcNow, CreatedById = admin.Id,
        });
        db.TelegramReportSettings.Add(new TelegramReportSettings { AccountId = account.Id });
        await db.SaveChangesAsync();
        fixture.Telegram.Sessions.Add(account.Id);
        scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().InvalidateHistory();
        return new Seeded(account.Id, admin.Id, region.Id, group.Id, chat.Id, agentChat.Id, other.Id, today);
    }

    private static ReportRecipientInput Sv(Seeded s, IReadOnlyList<Guid> chats, params string[] reports) =>
        new(TelegramReportRoles.Sv, null, null, s.RegionId, SupervisorId, null, chats, reports, true);

    private static ReportRecipientInput Tp(long agent, Guid chat, params string[] reports) =>
        new(TelegramReportRoles.Tp, null, null, null, null, agent, [chat], reports, true);

    private TelegramReportRunner Runner(IServiceProvider sp, Action? onRefresh = null)
    {
        var runner = sp.GetRequiredService<TelegramReportRunner>();
        runner.Delay = (_, _) => Task.CompletedTask;
        runner.Refresh = (_, _) =>
        {
            onRefresh?.Invoke();
            return Task.CompletedTask;
        };
        return runner;
    }

    [PostgresFact]
    public async Task Recipients_come_from_sales_structure_and_a_run_sends_once_to_every_chat()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TelegramReportService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();

        // Регионы, супервайзеры и ТП — реальные данные «Продаж»; без права на продажи — регионов нет.
        var overview = await service.OverviewAsync(canUseSales: true, default);
        var khorazm = Assert.Single(overview.Regions, r => r.Id == s.RegionId);
        Assert.Contains(khorazm.Supervisors, p => p.Id == SupervisorId && p.Name == "Азиз Каримов");
        Assert.Equal(["Бахтиёр Юлдашев", "Дилшод Рахимов", "Отабек Назаров"], khorazm.Agents.Select(a => a.Name).Order());
        Assert.Equal(ReportCatalog.Jobs.Count, overview.Jobs.Count);
        Assert.All(overview.Jobs, j => Assert.False(j.Enabled)); // по умолчанию ничего не отправляется
        Assert.Equal("Азиз Каримов (OneBase)", overview.Chats.Single(c => c.Id == s.PrivateChatId).LinkedUser);
        Assert.Empty((await service.OverviewAsync(canUseSales: false, default)).Regions);

        // Проверки на сервере: права на продажи, чаты, супервайзер региона, отчёты роли, заблокированный чат.
        var input = Sv(s, [s.GroupChatId, s.PrivateChatId], "daily.evening", "no-orders.evening", "no-orders.reminder");
        await Assert.ThrowsAsync<ReportForbiddenException>(() => service.SaveRecipientAsync(null, input, canUseSales: false, s.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientAsync(null, input with { ChatIds = [] }, true, s.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientAsync(null, input with { SupervisorId = 61 }, true, s.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientAsync(null, input with { Reports = ["no-orders.reminder"] }, true, s.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientAsync(null, Tp(999, s.AgentChatId, "daily.morning"), true, s.AdminId, default));
        var blockedChat = overview.Chats.Single(c => !c.CanSend).Id;
        var blockedError = await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientAsync(null, input with { ChatIds = [blockedChat] }, true, s.AdminId, default));
        Assert.Contains("заблокировал", blockedError.Message);

        var sv = await service.SaveRecipientAsync(null, input, true, s.AdminId, default);
        Assert.Equal(("sv", "Хоразм · СВ Азиз Каримов", 3, "linked"), (sv.Role, sv.Scope, sv.Agents, sv.TelegramStatus));
        Assert.Equal(["daily.evening", "no-orders.evening"], sv.Reports); // напоминание ТП у СВ отброшено
        Assert.Null(sv.Problem);
        // Второй получатель той же области в той же группе — группа получит отчёт один раз.
        await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId], "daily.evening"), true, s.AdminId, default);
        var tp63 = await service.SaveRecipientAsync(null, Tp(63, s.AgentChatId, "no-orders.reminder", "daily.evening"), true, s.AdminId, default);
        Assert.Equal(("tp", 1, "chat"), (tp63.Role, tp63.Agents, tp63.TelegramStatus));
        await service.SaveRecipientAsync(null, Tp(61, s.OtherChatId, "no-orders.reminder"), true, s.AdminId, default);

        // Включение в 17:00 «взводит» расписания с этого момента — сразу ничего не уходит.
        fixture.Clock.Now = Utc(s.Today, 16, 30);
        var evening = await service.SaveJobAsync("daily.evening", new ReportJobInput(true, "18:00", "Asia/Tashkent", EveryDay, null), s.AdminId, default);
        Assert.Equal(Local(s.Today, 18), evening.NextRunAt);
        await service.SaveJobAsync("no-orders.reminder", new ReportJobInput(true, "17:00", "Asia/Tashkent", EveryDay, null), s.AdminId, default);
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveJobAsync("daily.morning", new ReportJobInput(true, "8:61", null, EveryDay, null), s.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveJobAsync("daily.morning", new ReportJobInput(true, "08:30", null, [], null), s.AdminId, default));
        var refreshes = 0;
        var runner = Runner(sp, () => refreshes++);
        Assert.Equal(0, await runner.TickAsync(default));

        // 17:01 — напоминание: Отабеку (нет заказов) уходит, Бахтиёру (есть заказ) — «пропущено».
        fixture.Clock.Now = Utc(s.Today, 17, 1);
        Assert.Equal(1, await runner.TickAsync(default));
        Assert.Equal([AgentPeer], fixture.Telegram.Sent.Select(m => m.Peer.PeerId));
        var skipped = await db.TelegramReportDeliveries.AsNoTracking().SingleAsync(d => d.Report == "no-orders.reminder" && d.Status == "skipped");
        Assert.Contains("есть заказы", skipped.Error);
        Assert.Equal(1, refreshes); // данные Linko обновляются один раз перед запуском

        // 18:01 — итоги дня: СВ — в группу и личный чат (без дубля от второго получателя), ТП — в свой чат. Второй круг — ничего.
        fixture.Clock.Now = Utc(s.Today, 18, 1);
        Assert.Equal(3, await runner.TickAsync(default));
        Assert.Equal(0, await runner.TickAsync(default));
        var daily = fixture.Telegram.Sent.Where(m => m.Message.Text?.Contains("Дневной план") == true).ToList();
        Assert.Equal([GroupPeer, PrivatePeer, AgentPeer], daily.Select(m => m.Peer.PeerId).Order());
        foreach (var (_, _, message) in daily)
        {
            Assert.NotEmpty(message.Files);
            Assert.All(message.Files, f => Assert.True(f.Photo && f.ContentType == "image/png"));
        }

        Assert.Contains("Отабек Назаров", daily.Single(m => m.Peer.PeerId == AgentPeer).Message.Text);
        Assert.DoesNotContain("Бахтиёр", daily.Single(m => m.Peer.PeerId == AgentPeer).Message.Text); // ТП — только свои цифры
        var deliveries = await db.TelegramReportDeliveries.AsNoTracking().Where(d => d.Report == "daily.evening").ToListAsync();
        Assert.Equal(3, deliveries.Count);
        Assert.All(deliveries, d => Assert.Equal(("sent", "schedule"), (d.Status, d.Trigger)));
        Assert.True(await db.TelegramMaterials.AnyAsync(m => m.AccountId == s.AccountId && m.PeerId == GroupPeer && m.Kind == "report" && m.HasText));

        var runs = await db.TelegramReportRuns.AsNoTracking().ToDictionaryAsync(r => r.Job);
        Assert.Equal(("done", 3, 3, 0), (runs["daily.evening"].Status, runs["daily.evening"].Planned, runs["daily.evening"].Sent, runs["daily.evening"].Failed));
        Assert.Equal(("done", 2, 1, 1), (runs["no-orders.reminder"].Status, runs["no-orders.reminder"].Planned, runs["no-orders.reminder"].Sent, runs["no-orders.reminder"].Skipped));

        // Сервер «не работал» в 13:00 (полдень включён вчера): запуск старше догона не отправляется — «пропущен» в журнале запусков.
        await service.SaveJobAsync("daily.midday", new ReportJobInput(true, "13:00", "Asia/Tashkent", EveryDay, null), s.AdminId, default);
        await db.TelegramReportJobs.Where(j => j.Code == "daily.midday").ExecuteUpdateAsync(u => u.SetProperty(j => j.ArmedAt, Utc(s.Today.AddDays(-1), 9)));
        Assert.Equal(0, await runner.TickAsync(default));
        var missed = await db.TelegramReportRuns.AsNoTracking().SingleAsync(r => r.Job == "daily.midday");
        Assert.Equal("skipped", missed.Status);
        Assert.Contains("не работал", missed.Error);

        // История: фильтры по статусу и отчёту; запуски — с итогами.
        var history = await service.HistoryAsync(new ReportHistoryQuery(s.Today, s.Today, "no-orders", null, "skipped"), default);
        var row = Assert.Single(history.Items);
        Assert.Equal(("no-orders.reminder", "tp", false), (row.Report, row.Role, row.CanRetry));
        Assert.Contains(history.Runs, r => r.Job == "daily.midday" && r.Status == "skipped");
        Assert.Equal(4, (await service.HistoryAsync(new ReportHistoryQuery(null, null, null, s.RegionId, "sent"), default)).Total);

        var overviewAfter = await service.OverviewAsync(true, default);
        Assert.Equal(4, overviewAfter.Metrics.Sent);
        Assert.Equal(1, overviewAfter.Metrics.Skipped);
        Assert.Equal("done", overviewAfter.Jobs.Single(j => j.Code == "daily.evening").LastRun!.Status);
    }

    [PostgresFact]
    public async Task Test_goes_only_to_the_selected_chat_and_uncertain_or_failed_sends_are_retried_explicitly()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TelegramReportService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();
        fixture.Clock.Now = Utc(s.Today, 13, 5);

        var recipient = await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId, s.PrivateChatId], "daily.midday", "promo.midday"), true, s.AdminId, default);

        // Предпросмотр и тест — реальные данные, только с правом на продажи; тест — только в выбранный чат.
        var target = new ReportPreviewInput("daily.midday", null, null, null, null, null, recipient.Id, null, null, null);
        await Assert.ThrowsAsync<ReportForbiddenException>(() => service.PreviewAsync(target, false, default));
        var preview = await service.PreviewAsync(target, true, default);
        Assert.NotEmpty(preview.Pages);
        Assert.StartsWith("data:image/png;base64,", preview.Pages[0]);
        Assert.Equal("Хоразм · СВ Азиз Каримов", preview.Scope);
        // Акции — как на странице «Акции»: строка со скидкой без акции каталога — «Ручная скидка».
        var engine = sp.GetRequiredService<ReportEngine>();
        var promo = await engine.BuildAsync("promo.midday", new ReportScope("sv", RegionId: s.RegionId, SupervisorId: SupervisorId), s.Today, Local(s.Today, 13, 5), "ru", 30, default);
        Assert.Contains("Ручная скидка", promo.Plain());
        await Assert.ThrowsAsync<ReportValidationException>(() => service.PreviewAsync(target with { Job = "no-orders.reminder" }, true, default));

        var test = await service.TestAsync(new ReportTestInput(target, s.OtherChatId), true, s.AdminId, default);
        Assert.Equal("sent", test.Status);
        Assert.Equal([OtherPeer], fixture.Telegram.Sent.Select(m => m.Peer.PeerId));
        var testRow = await db.TelegramReportDeliveries.AsNoTracking().SingleAsync(d => d.Id == test.DeliveryId);
        Assert.Equal(("test", "daily.midday"), (testRow.Trigger, testRow.Report));

        // «Отправить сейчас» — во все чаты получателя; второй раз за день — только явно.
        var first = await service.SendNowAsync(recipient.Id, new ReportSendInput("daily.midday", false), true, s.AdminId, default);
        Assert.Equal(["sent", "sent"], first.Select(r => r.Status));
        await Assert.ThrowsAsync<ReportConflictException>(() => service.SendNowAsync(recipient.Id, new ReportSendInput("daily.midday", false), true, s.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SendNowAsync(recipient.Id, new ReportSendInput("no-orders.reminder", false), true, s.AdminId, default));

        // Тайм-аут посреди отправки: «неизвестно, дошёл ли» — автоповтора нет; повтор — только после подтверждения.
        fixture.Telegram.Fail = (peer, _) => peer.PeerId == GroupPeer ? new TelegramException("Telegram не ответил за 180 с — попробуйте ещё раз.", TelegramErrorKind.Transient) : null;
        var uncertain = await service.SendNowAsync(recipient.Id, new ReportSendInput("promo.midday", false), true, s.AdminId, default);
        var unknown = Assert.Single(uncertain, r => r.Status == "unknown");
        Assert.Single(uncertain, r => r.Status == "sent");
        var runner = Runner(sp);
        fixture.Clock.Now = Utc(s.Today, 13, 30);
        Assert.Equal(0, await runner.SendPendingAsync(default));
        fixture.Telegram.Fail = null;
        await Assert.ThrowsAsync<ReportConflictException>(() => service.RetryAsync(unknown.DeliveryId, force: false, true, s.AdminId, default));
        Assert.Equal("sent", (await service.RetryAsync(unknown.DeliveryId, force: true, true, s.AdminId, default)).Status);
        await Assert.ThrowsAsync<ReportValidationException>(() => service.RetryAsync(unknown.DeliveryId, force: true, true, s.AdminId, default)); // уже доставлен

        // Получатель недоступен — «ошибка» с причиной; повтор после устранения.
        fixture.Telegram.Fail = (peer, _) => peer.PeerId == PrivatePeer ? new TelegramException("USER_IS_BLOCKED", TelegramErrorKind.Permanent) : null;
        var blocked = await service.SendNowAsync(recipient.Id, new ReportSendInput("promo.midday", true), true, s.AdminId, default);
        var failed = Assert.Single(blocked, r => r.Status == "failed");
        Assert.Contains("Получатель недоступен", failed.Error);
        fixture.Telegram.Fail = null;
        Assert.Equal("sent", (await service.RetryAsync(failed.DeliveryId, force: false, true, s.AdminId, default)).Status);

        // Временная ошибка сети — строка ждёт и уходит повтором очереди.
        var failOnce = true;
        fixture.Telegram.Fail = (_, _) =>
        {
            if (!failOnce)
            {
                return null;
            }

            failOnce = false;
            return new TelegramException("Нет связи с Telegram: IOException", TelegramErrorKind.Transient);
        };
        var again = await service.SendNowAsync(recipient.Id, new ReportSendInput("daily.midday", true), true, s.AdminId, default);
        var pending = Assert.Single(again, r => r.Status == "pending");
        fixture.Clock.Now = pending.NextAttemptAt!.Value.AddSeconds(1);
        Assert.Equal(1, await runner.SendPendingAsync(default));
        var retried = await db.TelegramReportDeliveries.AsNoTracking().SingleAsync(d => d.Id == pending.DeliveryId);
        Assert.Equal(("sent", 2), (retried.Status, retried.Attempts));

        var history = await service.HistoryAsync(new ReportHistoryQuery(null, null, "daily.midday", null, null), default);
        Assert.Contains(history.Items, i => i.Trigger == "test" && !i.CanRetry);
        Assert.Equal("Администратор отчётов", history.Items.First(i => i.Trigger == "manual").By);
        Assert.All(history.Items, i => Assert.Equal("daily.midday", i.Report));

        // Удалённый получатель: строки в очереди не уходят — «пропущено».
        fixture.Telegram.Fail = (_, _) => new TelegramException("Нет связи с Telegram: IOException", TelegramErrorKind.Transient);
        var queued = await service.SendNowAsync(recipient.Id, new ReportSendInput("promo.midday", true), true, s.AdminId, default);
        fixture.Telegram.Fail = null;
        await service.DeleteRecipientAsync(recipient.Id, s.AdminId, default);
        fixture.Clock.Now = queued.Max(q => q.NextAttemptAt!.Value).AddSeconds(1);
        Assert.Equal(0, await runner.SendPendingAsync(default));
        Assert.All(await db.TelegramReportDeliveries.AsNoTracking().Where(d => queued.Select(q => q.DeliveryId).Contains(d.Id)).ToListAsync(),
            d => Assert.Equal(("skipped", "Получатель удалён."), (d.Status, d.Error)));
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Two_morning_runs_at_the_same_time_go_out_to_every_role_and_fresh_orders_count_as_the_fact()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TelegramReportService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();
        string[] reports = ["daily.morning", "no-purchase.morning", "daily.evening"];

        // Три роли: РМ (вся страна) — в личный чат, СВ — в группу и личный чат, ТП — в свой чат; у всех оба утренних отчёта и итог дня.
        await service.SaveRecipientAsync(null, new ReportRecipientInput(TelegramReportRoles.Rm, null, null, null, null, null, [s.OtherChatId], reports, true), true, s.AdminId, default);
        await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId, s.PrivateChatId], reports), true, s.AdminId, default);
        await service.SaveRecipientAsync(null, Tp(63, s.AgentChatId, reports), true, s.AdminId, default);

        // Включены в 8:00 — в 8:30 наступают два запуска сразу; оба уходят в каждый чат, друг другу не мешая.
        fixture.Clock.Now = Utc(s.Today, 8);
        await service.SaveJobAsync("daily.morning", new ReportJobInput(true, "08:30", "Asia/Tashkent", EveryDay, null), s.AdminId, default);
        await service.SaveJobAsync("no-purchase.morning", new ReportJobInput(true, "08:30", "Asia/Tashkent", EveryDay, null), s.AdminId, default);
        await service.SaveJobAsync("daily.evening", new ReportJobInput(true, "18:00", "Asia/Tashkent", EveryDay, null), s.AdminId, default);
        var runner = Runner(sp);
        Assert.Equal(0, await runner.TickAsync(default));

        fixture.Clock.Now = Utc(s.Today, 8, 31);
        Assert.Equal(8, await runner.TickAsync(default)); // 4 чата × 2 отчёта
        Assert.Equal(0, await runner.TickAsync(default));
        var runs = await db.TelegramReportRuns.AsNoTracking().ToDictionaryAsync(r => r.Job);
        Assert.Equal(("done", 4, 4, 0, 0), (runs["daily.morning"].Status, runs["daily.morning"].Planned, runs["daily.morning"].Sent, runs["daily.morning"].Failed, runs["daily.morning"].Skipped));
        Assert.Equal(("done", 4, 4, 0, 0), (runs["no-purchase.morning"].Status, runs["no-purchase.morning"].Planned, runs["no-purchase.morning"].Sent,
            runs["no-purchase.morning"].Failed, runs["no-purchase.morning"].Skipped));
        Assert.Equal(2, fixture.Telegram.Sent.Count(m => m.Peer.PeerId == GroupPeer)); // в группу — и «Дневной план», и «Клиенты без закупа»
        Assert.All(fixture.Telegram.Sent, m => Assert.NotEmpty(m.Message.Files));
        Assert.Equal(4, fixture.Telegram.Sent.Count(m => m.Message.Text?.Contains("Дневной план") == true));
        Assert.Equal(4, fixture.Telegram.Sent.Count(m => m.Message.Text?.Contains("Клиенты без закупа") == true));
        Assert.Contains(fixture.Telegram.Sent, m => m.Peer.PeerId == OtherPeer && m.Message.Text?.Contains("Вся страна") == true);

        // Итог дня: заказы, ещё не отгруженные со склада (not_delivered), — это и есть взятое за день; отменённый не считается.
        fixture.Clock.Now = Utc(s.Today, 18, 2);
        Assert.Equal(4, await runner.TickAsync(default));
        var evening = fixture.Telegram.Sent.Single(m => m.Peer.PeerId == GroupPeer && m.Message.Text?.Contains("Итог дня") == true).Message.Text!;
        Assert.Contains("40 000 000", evening); // 25 + 15 млн, без отменённых 9 млн
        Assert.Contains("заказов 2,", evening);
        Assert.DoesNotContain("проверьте обновление заказов", evening);
        var deliveries = await db.TelegramReportDeliveries.AsNoTracking().Where(d => d.Trigger == "schedule").ToListAsync();
        Assert.Equal(12, deliveries.Count);
        Assert.All(deliveries, d => Assert.Equal("sent", d.Status));
        fixture.Clock.Now = null;
    }

    /// <summary>
    /// Дневной план ТП («ТП без заказов»): утренняя задача фиксирует его на бизнес-дату, повтор дубля не создаёт, отчёт 13:00 берёт
    /// сохранённое значение, новый факт после расчёта план не меняет; пересчёт администратора — с причиной и историей; праздник — «—»;
    /// план, появившийся в Linko позже, учитывается с пометкой.
    /// </summary>
    [PostgresFact]
    public async Task Daily_plan_is_fixed_in_the_morning_reused_later_not_duplicated_and_recalculated_with_history()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var plans = sp.GetRequiredService<DailyPlanService>();
        var service = sp.GetRequiredService<TelegramReportService>();
        var engine = sp.GetRequiredService<ReportEngine>();
        await db.TelegramReportDailyPlans.ExecuteDeleteAsync();
        await db.SalesHolidays.ExecuteDeleteAsync();

        // Бизнес-дата — первый рабочий день месяца с сегодняшнего (воскресенье не подходит: план дня не считается).
        var monthStart = new DateOnly(s.Today.Year, s.Today.Month, 1);
        var day = s.Today;
        while (day.DayOfWeek == DayOfWeek.Sunday && day.Month == s.Today.Month)
        {
            day = day.AddDays(1);
        }

        fixture.Clock.Now = Utc(day, 8, 5);
        var holidays = new Dictionary<DateOnly, string>();

        // Утро: планы всем ТП оргструктуры; повтор ничего не добавляет и дубля не создаёт.
        Assert.True(await plans.PrepareAsync(day, default) >= 3);
        Assert.Equal(0, await plans.PrepareAsync(day, default));
        var first = await db.TelegramReportDailyPlans.AsNoTracking().SingleAsync(p => p.LinkoUserId == 61 && p.Date == day && p.SupersededAt == null);
        Assert.Equal(("morning", DailyPlanMath.Ok, 780_000_000m), (first.Trigger, first.Status, first.MonthPlanSum));
        Assert.Equal(DailyPlanMath.WorkDaysLeft(day, holidays), first.WorkDaysLeft);
        Assert.Equal(first.RemainingSum!.Value / first.WorkDaysLeft, first.DailyPlanSum);
        Assert.Equal(first.MonthPlanSum - first.FactBeforeSum, first.RemainingSum);

        // Факт по вчера вырос после утреннего расчёта — план дня остаётся прежним (и в отчёте 13:00 — он же).
        var late = day.AddDays(-1);
        db.LinkoOrders.Add(new LinkoOrder
        {
            Id = 800_000_099, CreatedAt = late.ToDateTime(new TimeOnly(15, 0)), CreatedDate = late, AcceptedAt = late.ToDateTime(new TimeOnly(16, 0)), AcceptedDate = late,
            Status = "delivered", MarketId = 1101, BranchId = 5, BranchName = "Хоразм", AgentId = 61, TotalPrice = 5_000_000, TotalWeight = 100,
        });
        db.LinkoOrderLines.Add(new LinkoOrderLine { Id = 800_000_099, OrderId = 800_000_099, ProductId = 100, Amount = 100, Price = 50_000, TotalPrice = 5_000_000, TotalWeight = 100 });
        await db.SaveChangesAsync();
        sp.GetRequiredService<SalesCacheSignal>().Invalidate();
        var again = await plans.EnsureAsync([61], day, DailyPlanService.Report, default);
        Assert.Equal(first.DailyPlanSum, again[61].DailyPlan);
        Assert.Equal(1, await db.TelegramReportDailyPlans.CountAsync(p => p.LinkoUserId == 61 && p.Date == day && p.SupersededAt == null));

        fixture.Clock.Now = Utc(day, 13, 4);
        var midday = await engine.BuildAsync("no-orders.midday", new ReportScope("sv", RegionId: s.RegionId, SupervisorId: SupervisorId), day, Local(day, 13, 4), "ru", 30, default);
        var row = midday.Tables[0].Rows.Single(r => r[1].Text == "Бахтиёр Юлдашев");
        Assert.Equal(ReportFormat.Number(first.DailyPlanSum), row[2].Text);
        // Факт к 13:00 — взятые заказы ТП за бизнес-дату до 13:00 (без отменённых); бизнес-дата может отличаться от дня сева, поэтому — из базы.
        var noon = day.ToDateTime(new TimeOnly(13, 0));
        var fact13 = await db.LinkoOrders.AsNoTracking().Where(o => o.AgentId == 61 && o.CreatedDate == day && o.Status != "cancelled" && o.CreatedAt < noon).SumAsync(o => o.TotalPrice);
        Assert.Equal(ReportFormat.Percent(fact13 / first.DailyPlanSum!.Value), row[5].Text);
        var total = midday.Tables[0].Total!;
        Assert.DoesNotContain("NaN", total[5].Text);

        // Пересчёт администратором: без причины — отказ; с причиной — прежняя строка в истории, новая с выросшим фактом и причиной.
        await Assert.ThrowsAsync<ReportValidationException>(() => service.RecalculateDailyPlansAsync(new DailyPlanRecalcInput(day, 61, " "), true, s.AdminId, default));
        var view = await service.RecalculateDailyPlansAsync(new DailyPlanRecalcInput(day, 61, "Догрузили вчерашние заказы"), true, s.AdminId, default);
        var current = view.Items.Single(p => p.AgentId == 61);
        Assert.Equal(("admin", "Догрузили вчерашние заказы", "Администратор отчётов"), (current.Trigger, current.Reason, current.By));
        Assert.Equal(first.FactBeforeSum + (late >= monthStart ? 5_000_000m : 0m), current.FactBeforeSum);
        Assert.Single(view.History, p => p.AgentId == 61 && p.SupersededAt != null && p.Trigger == "morning");
        Assert.Equal(1, await db.TelegramReportDailyPlans.CountAsync(p => p.LinkoUserId == 61 && p.Date == day && p.SupersededAt == null));

        // Праздник из справочника: воскресенье в него не добавить, повтор даты — конфликт; в праздник план дня — «—».
        await Assert.ThrowsAsync<ReportValidationException>(() => service.AddHolidayAsync(new HolidayInput(new DateOnly(2026, 10, 25), "Воскресенье"), s.AdminId, default));
        var holiday = await service.AddHolidayAsync(new HolidayInput(day, "Проверочный праздник"), s.AdminId, default);
        await Assert.ThrowsAsync<ReportConflictException>(() => service.AddHolidayAsync(new HolidayInput(day, "Ещё раз"), s.AdminId, default));
        Assert.Contains(await service.HolidaysAsync(day.Year, default), h => h.Id == holiday.Id);
        var offDay = await service.RecalculateDailyPlansAsync(new DailyPlanRecalcInput(day, null, "Праздник"), true, s.AdminId, default);
        Assert.False(offDay.WorkingDay);
        Assert.All(offDay.Items.Where(p => p.MonthPlanSum > 0), p => Assert.Equal(DailyPlanMath.DayOff, p.Status));
        var report = await engine.BuildAsync("no-orders.midday", new ReportScope("sv", RegionId: s.RegionId, SupervisorId: SupervisorId), day, Local(day, 13, 4), "ru", 30, default);
        Assert.All(report.Tables[0].Rows, r => Assert.Equal("—", r[2].Text));
        Assert.Contains(report.Notes, n => n.Contains("праздник"));
        await service.DeleteHolidayAsync(holiday.Id, s.AdminId, default);
        await service.RecalculateDailyPlansAsync(new DailyPlanRecalcInput(day, null, "Праздник снят"), true, s.AdminId, default);

        // ТП без плана месяца — «нет плана» (не ноль); план появился в Linko — новая строка с пометкой, прежняя в истории.
        await db.LinkoUsers.Where(u => u.Id == 69).ExecuteDeleteAsync();
        await db.SalesStaffPlans.Where(p => p.LinkoUserId == 69).ExecuteDeleteAsync();
        db.LinkoUsers.Add(new LinkoUser { Id = 69, FirstName = "Без", SecondName = "Плана", IsActive = true, JobName = "Агент" });
        await db.SaveChangesAsync();
        var noPlan = await plans.EnsureAsync([69], day, DailyPlanService.Report, default);
        Assert.Equal(DailyPlanMath.NoPlan, noPlan[69].Status);
        db.SalesStaffPlans.Add(new SalesStaffPlan
        {
            Year = day.Year, Month = day.Month, LinkoUserId = 69, IndicatorId = 9_069, IndicatorName = "Выручка", PlanType = StaffPlanTypes.SalesSum, PlanAmount = 100_000_000m,
            LoadedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        var appeared = await plans.EnsureAsync([69], day, DailyPlanService.Report, default);
        Assert.Equal((DailyPlanMath.Ok, 100_000_000m), (appeared[69].Status, appeared[69].MonthPlan));
        var rows69 = await db.TelegramReportDailyPlans.AsNoTracking().Where(p => p.LinkoUserId == 69 && p.Date == day).ToListAsync();
        Assert.Equal(["plan-appeared"], rows69.Where(p => p.SupersededAt == null).Select(p => p.Trigger));
        Assert.Single(rows69, p => p.SupersededAt != null && p.Status == DailyPlanMath.NoPlan);

        // Прошлый день: считается на лету, в базу не пишется.
        var past = day.AddDays(-7);
        await plans.EnsureAsync([61], past, DailyPlanService.Report, default);
        Assert.False(await db.TelegramReportDailyPlans.AnyAsync(p => p.Date == past));
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Rm_sees_regions_of_the_country_and_reports_render_for_every_role()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TelegramReportService>();
        fixture.Clock.Now = Utc(s.Today, 18, 5);
        var output = Environment.GetEnvironmentVariable("ONEBASE_REPORT_PNG"); // папка для просмотра картинок; в CI не задана

        foreach (var job in ReportCatalog.Jobs)
        {
            foreach (var (role, region, agent) in new[] { ("rm", (Guid?)null, (long?)null), ("sv", s.RegionId, null), ("tp", null, 61L) })
            {
                if (!job.Roles.Contains(role))
                {
                    continue;
                }

                var preview = await service.PreviewAsync(
                    new ReportPreviewInput(job.Code, role, null, region, role == "sv" ? SupervisorId : null, agent, null, s.Today, job.Time.ToString("HH:mm"), null), true, default);
                Assert.NotEmpty(preview.Pages);
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    for (var i = 0; i < preview.Pages.Count; i++)
                    {
                        await File.WriteAllBytesAsync(Path.Combine(output, $"{job.Code}-{role}-{i + 1}.png"), Convert.FromBase64String(preview.Pages[i].Split(',')[1]));
                    }
                }
            }
        }

        var rm = await service.PreviewAsync(new ReportPreviewInput("daily.evening", "rm", null, null, null, null, null, s.Today, null, null), true, default);
        Assert.Equal("Вся страна", rm.Scope);
        var engine = scope.ServiceProvider.GetRequiredService<ReportEngine>();
        var country = await engine.BuildAsync("daily.evening", new ReportScope("rm"), s.Today, Local(s.Today, 18, 5), "ru", 30, default);
        Assert.Contains("Хоразм", country.Plain());
        Assert.Contains("40 000 000", country.Plain()); // 25 + 15 млн сегодня

        // Без закупа 30+ дней: «Бахор» (40 дней), тестовая точка не считается.
        var silent = await engine.BuildAsync("no-purchase.morning", new ReportScope("tp", AgentId: 61), s.Today, Local(s.Today, 8, 30), "ru", 30, default);
        Assert.Contains("Супермаркет «Бахор»", silent.Plain());
        Assert.DoesNotContain("Тест магазин", silent.Plain());
        Assert.DoesNotContain("Магазин «Нур»", silent.Plain()); // покупал вчера
        fixture.Clock.Now = null;
    }
}

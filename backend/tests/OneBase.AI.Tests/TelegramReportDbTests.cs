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
/// Автоотчёты на PostgreSQL и подставном Telegram (настоящих отправок нет): регион, супервайзер и агенты — из раздела «Продажи» (оргструктура
/// и Linko, без Sales Base); автоотправка «регион → супервайзер → несколько чатов» по расписанию уходит в каждый чат один раз, с картинками
/// и материалом для агента; тест на демонстрационных данных; реальные данные — только с правом на продажи; ручная отправка с защитой от
/// повтора; повтор после временной ошибки сети.
/// </summary>
public class TelegramReportDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private sealed record Seeded(Guid AccountId, Guid AdminId, Guid RegionId, Guid GroupChatId, Guid PrivateChatId);

    private const long SupervisorId = 71;
    private const long GroupPeer = -100_500;
    private const long PrivatePeer = 9_001;

    private async Task<Seeded> SeedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.TelegramReportDeliveries.ExecuteDeleteAsync();
        await db.TelegramReportSubscriptions.ExecuteDeleteAsync();
        await db.TelegramReportSchedules.ExecuteDeleteAsync();
        await db.TelegramReportSettings.ExecuteDeleteAsync();
        fixture.Telegram.Reset();
        fixture.Clock.Now = null;

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var admin = new User { Login = $"rep-admin-{suffix}", FullName = "Администратор отчётов" };
        db.Users.Add(admin);

        // Хоразм (филиал 5): СВР — «Азиз Каримов» (в оргструктуре и в Linko), два агента с заказами и планами на сегодня.
        var region = await db.SalesRegions.FirstAsync(r => r.LinkoBranchId == 5);
        region.SupervisorName = "Азиз Каримов";
        var today = FieldClock.Today;
        await db.LinkoUsers.Where(u => u.Id == SupervisorId || u.Id == 61 || u.Id == 62).ExecuteDeleteAsync();
        db.LinkoUsers.AddRange(
            new LinkoUser { Id = SupervisorId, FirstName = "Азиз", SecondName = "Каримов", IsActive = true, JobName = "Супервайзер" },
            new LinkoUser { Id = 61, FirstName = "Бахтиёр", SecondName = "Юлдашев", IsActive = true, JobName = "Агент" },
            new LinkoUser { Id = 62, FirstName = "Дилшод", SecondName = "Рахимов", IsActive = true, JobName = "Агент" });
        await db.SalesStaffPlans.Where(p => p.LinkoUserId == 61 || p.LinkoUserId == 62).ExecuteDeleteAsync();
        foreach (var (agent, plan) in new[] { (61L, 780_000_000m), (62L, 650_000_000m) })
        {
            db.SalesStaffPlans.Add(new SalesStaffPlan
            {
                Year = today.Year, Month = today.Month, LinkoUserId = agent, IndicatorId = 1, IndicatorName = "Выручка", PlanType = StaffPlanTypes.SalesSum,
                PlanAmount = plan, LoadedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.LinkoOrderLines.Where(l => l.Id >= 800_000_001 && l.Id <= 800_000_010).ExecuteDeleteAsync();
        await db.LinkoOrders.Where(o => o.Id >= 800_000_001 && o.Id <= 800_000_010).ExecuteDeleteAsync();
        // Вчерашние продажи — команда региона в «Продажах» (сегодняшние приёмки войдут в отчётный день завтра); сегодняшние — факт отчёта.
        var id = 800_000_001L;
        foreach (var agent in new[] { 61L, 62L })
        {
            var yesterday = today.AddDays(-1);
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = id, CreatedAt = DateTime.Now.AddDays(-1), CreatedDate = yesterday, AcceptedAt = DateTime.Now.AddDays(-1), AcceptedDate = yesterday,
                Status = "delivered", MarketId = 1001, BranchId = 5, BranchName = "Хоразм", AgentId = agent, TotalPrice = 1_000_000, TotalWeight = 30,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = 100, Amount = 30, Price = 33_333, TotalPrice = 1_000_000, TotalWeight = 30 });
            id++;
        }

        foreach (var (agent, sum) in new[] { (61L, 25_000_000m), (62L, 15_000_000m) })
        {
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = id, CreatedAt = DateTime.Now, CreatedDate = today, AcceptedAt = DateTime.Now, AcceptedDate = today, Status = "delivered", MarketId = 1001,
                BranchId = 5, BranchName = "Хоразм", AgentId = agent, TotalPrice = sum, TotalWeight = 400,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = 100, Amount = 400, Price = sum / 400, TotalPrice = sum, TotalWeight = 400 });
            id++;
        }

        var orders = await db.LinkoSyncStates.FirstOrDefaultAsync(s => s.Entity == "orders");
        if (orders is null)
        {
            db.LinkoSyncStates.Add(orders = new LinkoSyncState { Entity = "orders" });
        }

        orders.LastSuccessAt = DateTimeOffset.UtcNow;

        var account = new TelegramAccount { Name = $"Отчёты {suffix}", Status = TelegramAccountStatus.Connected, CreatedById = admin.Id };
        db.TelegramAccounts.Add(account);
        var group = new TelegramChat { AccountId = account.Id, PeerId = GroupPeer, Type = TelegramChatType.Group, Title = "Хоразм — продажи" };
        var chat = new TelegramChat { AccountId = account.Id, PeerId = PrivatePeer, Type = TelegramChatType.Private, Title = "Шерзод", AccessHash = 1 };
        db.TelegramChats.AddRange(group, chat);
        db.TelegramReportSettings.Add(new TelegramReportSettings { AccountId = account.Id });
        await db.SaveChangesAsync();
        fixture.Telegram.Sessions.Add(account.Id);
        scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().InvalidateHistory();
        return new Seeded(account.Id, admin.Id, region.Id, group.Id, chat.Id);
    }

    [PostgresFact]
    public async Task Region_supervisor_and_agents_come_from_sales_and_a_schedule_sends_once_to_every_chat()
    {
        var seeded = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TelegramReportService>();

        // Регионы, супервайзеры и агенты — реальные данные «Продаж»: Хоразм, СВР Азиз Каримов, два агента.
        var overview = await service.OverviewAsync(canUseSales: true, default);
        var khorazm = Assert.Single(overview.Regions, r => r.Id == seeded.RegionId);
        Assert.Equal("Хоразм", khorazm.Name);
        Assert.Contains(khorazm.Supervisors, s => s.Id == SupervisorId && s.Name == "Азиз Каримов");
        Assert.Equal(["Бахтиёр Юлдашев", "Дилшод Рахимов"], khorazm.Agents.Order());
        Assert.Empty((await service.OverviewAsync(canUseSales: false, default)).Regions); // без права на продажи — регионов нет

        // Проверки: без права на продажи, без чатов, супервайзер не из региона — не сохраняется.
        var input = new ReportSubscriptionInput(seeded.RegionId, SupervisorId, [seeded.GroupChatId, seeded.PrivateChatId], false, false, true, true);
        await Assert.ThrowsAsync<ReportForbiddenException>(() => service.SaveSubscriptionAsync(null, input, canUseSales: false, seeded.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveSubscriptionAsync(null, input with { ChatIds = [] }, true, seeded.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveSubscriptionAsync(null, input with { SupervisorId = 61 }, true, seeded.AdminId, default));
        var saved = await service.SaveSubscriptionAsync(null, input, true, seeded.AdminId, default);
        Assert.Equal(("Хоразм", "Азиз Каримов", 2, 2), (saved.Region, saved.Supervisor, saved.Agents, saved.Chats.Count));
        Assert.Null(saved.Problem);

        // Вечер включили вчера; сейчас — минута после времени отправки по Ташкенту.
        var now = DateTimeOffset.UtcNow;
        var local = DailyReportSchedules.Local(now, "Asia/Tashkent");
        await service.SaveScheduleAsync(TelegramReportKind.Evening, new ReportScheduleInput(true, local.AddMinutes(-1).ToString("HH:mm"), "Asia/Tashkent", [1, 2, 3, 4, 5, 6, 7], "sum"),
            seeded.AdminId, default);
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await db.TelegramReportSchedules.Where(s => s.Kind == TelegramReportKind.Evening).ExecuteUpdateAsync(u => u.SetProperty(s => s.ArmedAt, now.AddDays(-1)));

        var runner = sp.GetRequiredService<TelegramReportRunner>();
        runner.Delay = (_, _) => Task.CompletedTask;
        Assert.Equal(2, await runner.TickAsync(default)); // одна автоотправка — два чата
        Assert.Equal(0, await runner.TickAsync(default)); // второй круг — без дублей

        Assert.Equal([GroupPeer, PrivatePeer], fixture.Telegram.Sent.Select(s => s.Peer.PeerId).Order());
        foreach (var (_, _, message) in fixture.Telegram.Sent)
        {
            Assert.All(message.Files, f => Assert.True(f.Photo && f.ContentType == "image/png"));
            Assert.Contains("Итоги дня", message.Text);
            Assert.Contains("Хоразм · Азиз Каримов", message.Text);
        }

        var deliveries = await db.TelegramReportDeliveries.AsNoTracking().ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, d => Assert.Equal(("sent", "schedule", "real"), (d.Status, d.Trigger, d.DataMode)));
        Assert.All(deliveries, d => Assert.Equal("Хоразм · Азиз Каримов", d.ScopeTitle));
        Assert.True(await db.TelegramMaterials.AnyAsync(m => m.AccountId == seeded.AccountId && m.PeerId == GroupPeer && m.Kind == "report" && m.HasText));

        // Отчёт: агенты региона, план на день — целые сумы, итог = сумма строк, факт — заказы сегодня.
        var builder = sp.GetRequiredService<DailyReportBuilder>();
        var report = await builder.BuildAsync(TelegramReportKind.Evening, seeded.RegionId, SupervisorId, null, FieldClock.Today, ReportMetric.Sum, "ru", local, false, default);
        Assert.Equal("Азиз Каримов", Assert.Single(report.Teams).Supervisor);
        Assert.Equal(2, report.Total.Agents);
        Assert.Equal(40_000_000, report.Total.Fact);
        Assert.Empty(DailyReportMath.Verify(report));
        Assert.All(report.Teams[0].Agents, a => Assert.Equal(Math.Round(a.Plan ?? 0), a.Plan ?? 0));
    }

    [PostgresFact]
    public async Task Test_is_demo_real_data_needs_sales_rights_and_manual_resend_is_explicit()
    {
        var seeded = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TelegramReportService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();

        // Тест по умолчанию — демонстрационные данные с пометкой; материала для агента нет.
        var demo = await service.TestAsync(new ReportTestInput(new ReportPreviewInput("Morning", "demo", null, 6, null, null, null, null, null), seeded.GroupChatId),
            canUseSales: false, seeded.AdminId, default);
        Assert.Equal("sent", demo.Status);
        Assert.Contains("ТЕСТОВЫЕ ДАННЫЕ", fixture.Telegram.Sent.Last().Message.Text);
        Assert.Equal("demo", (await db.TelegramReportDeliveries.AsNoTracking().SingleAsync(d => d.Id == demo.DeliveryId)).DataMode);
        Assert.False(await db.TelegramMaterials.AnyAsync(m => m.AccountId == seeded.AccountId && m.PeerId == GroupPeer && m.Kind == "report"));

        // Реальные данные — только с правом на продажи.
        var real = new ReportPreviewInput("Midday", "real", null, null, null, seeded.RegionId, SupervisorId, null, null);
        await Assert.ThrowsAsync<ReportForbiddenException>(() => service.TestAsync(new ReportTestInput(real, seeded.GroupChatId), false, seeded.AdminId, default));
        Assert.NotEmpty((await service.PreviewAsync(real, true, default)).Pages);

        // «Отправить сейчас» — во все чаты автоотправки; второй раз за день — только явно.
        var subscription = await service.SaveSubscriptionAsync(null,
            new ReportSubscriptionInput(seeded.RegionId, SupervisorId, [seeded.GroupChatId, seeded.PrivateChatId], true, true, false, true), true, seeded.AdminId, default);
        var first = await service.SendNowAsync(subscription.Id, new ReportSendInput("Morning", false), true, seeded.AdminId, default);
        Assert.Equal(["sent", "sent"], first.Select(r => r.Status));
        await Assert.ThrowsAsync<ReportConflictException>(() => service.SendNowAsync(subscription.Id, new ReportSendInput("Morning", false), true, seeded.AdminId, default));
        Assert.All(await service.SendNowAsync(subscription.Id, new ReportSendInput("Morning", true), true, seeded.AdminId, default), r => Assert.Equal("sent", r.Status));

        // Временная ошибка сети — строка ждёт и уходит повтором; второй чат уходит сразу.
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
        var midday = await service.SendNowAsync(subscription.Id, new ReportSendInput("Midday", false), true, seeded.AdminId, default);
        var pending = Assert.Single(midday, r => r.Status == "pending");
        Assert.Single(midday, r => r.Status == "sent");
        fixture.Clock.Now = pending.NextAttemptAt!.Value.AddSeconds(1);
        var runner = sp.GetRequiredService<TelegramReportRunner>();
        runner.Delay = (_, _) => Task.CompletedTask;
        Assert.Equal(1, await runner.SendPendingAsync(default));
        var retried = await db.TelegramReportDeliveries.AsNoTracking().SingleAsync(d => d.Id == pending.DeliveryId);
        Assert.Equal(("sent", 2), (retried.Status, retried.Attempts));
        fixture.Clock.Now = null;

        var journal = await service.DeliveriesAsync(null, null, null, default);
        Assert.Contains(journal, j => j.DataMode == "demo" && j.Trigger == "test");
        Assert.Equal(6, journal.Count(j => j.Trigger == "manual" && j.Status == "sent"));
    }
}

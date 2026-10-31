using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Sales;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// Сквозной сценарий еженедельного PDF на PostgreSQL и подставном Telegram (вымышленные данные): 3 региона × (СВ + 3 ТП), неделя 19–24.10.2026,
/// запуск в понедельник 26.10 в 09:00 — 3 PDF команды СВ и 9 личных карточек ТП, каждому свой; «только при полных данных» включено и данные
/// полные; строки в очереди переживают перезапуск службы (новый экземпляр отправляет их), повторный круг и повторная постановка дублей не дают.
/// </summary>
public class WeeklyPdfEndToEndTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Monday = new(2026, 10, 26);

    private static DateTimeOffset Utc(DateOnly day, int hour, int minute = 0) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(5)).ToUniversalTime();

    private sealed record Region(Guid Id, string Name, long[] Agents, Guid GroupChat, Guid[] AgentChats);

    private async Task<(Guid Admin, IReadOnlyList<Region> Regions)> SeedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        fixture.Telegram.Reset();
        fixture.Clock.Now = Utc(Monday, 8);

        var admin = new User { Login = $"e2e-{Guid.NewGuid():N}"[..20], FullName = "Администратор E2E" };
        db.Users.Add(admin);
        var account = new TelegramAccount { Name = "Отчёты E2E", Status = TelegramAccountStatus.Connected, CreatedById = admin.Id };
        db.TelegramAccounts.Add(account);

        var regions = new List<Region>();
        long orderId = 880_000_000, peer = 7_000;
        for (var r = 0; r < 3; r++)
        {
            var branch = 931L + r;
            var letter = "АБВ"[r];
            var region = new SalesRegion { LinkoBranchId = branch, Name = $"Регион {letter}", SupervisorName = $"СВ Регион {letter}", DealerName = $"Дилер {letter}" };
            db.SalesRegions.Add(region);
            var agents = Enumerable.Range(1, 3).Select(i => 9300L + r * 10 + i).ToArray();
            var group = new TelegramChat { AccountId = account.Id, PeerId = -(++peer), Type = TelegramChatType.Group, Title = $"Регион {letter} — продажи" };
            db.TelegramChats.Add(group);
            var chats = new List<Guid>();
            foreach (var (agent, i) in agents.Select((a, i) => (a, i)))
            {
                db.LinkoUsers.Add(new LinkoUser { Id = agent, FirstName = $"ТП {letter}{i + 1}", SecondName = "Вымышленный", IsActive = true, JobName = "Агент" });
                db.SalesStaffPlans.Add(new SalesStaffPlan
                {
                    Year = 2026, Month = 10, LinkoUserId = agent, IndicatorId = (int)(agent - 9000), IndicatorName = "Октябрь Тотал Вес", PlanType = StaffPlanTypes.SalesWeight,
                    PlanAmount = 600, FactAmount = 240 + 30 * i, LoadedAt = DateTimeOffset.UtcNow,
                });
                var market = agent * 10;
                db.LinkoMarkets.Add(new LinkoMarket { Id = market, Name = $"Точка {letter}{i + 1}", BranchId = branch, BranchName = region.Name, ResponsibleAgentId = agent });

                // Начало месяца, прошлая неделя (12–17.10) и отчётная (19–24.10): продажи каждого ТП.
                foreach (var (day, kg) in new[] { (new DateOnly(2026, 10, 5), 50m), (new DateOnly(2026, 10, 14), 40m + i), (new DateOnly(2026, 10, 21), 60m + 5 * i), (new DateOnly(2026, 10, 24), 20m) })
                {
                    var id = ++orderId;
                    var at = day.ToDateTime(new TimeOnly(11, 0));
                    db.LinkoOrders.Add(new LinkoOrder
                    {
                        Id = id, CreatedAt = at, CreatedDate = day, AcceptedAt = at, AcceptedDate = day, Status = "delivered", MarketId = market, BranchId = branch,
                        BranchName = region.Name, AgentId = agent, TotalPrice = kg * 40_000, TotalWeight = kg,
                    });
                    db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = 100, Amount = kg, Price = 40_000, TotalPrice = kg * 40_000, TotalWeight = kg });
                }

                var chat = new TelegramChat { AccountId = account.Id, PeerId = ++peer, Type = TelegramChatType.Private, Title = $"ТП {letter}{i + 1}", AccessHash = peer };
                db.TelegramChats.Add(chat);
                chats.Add(chat.Id);
            }

            regions.Add(new Region(region.Id, region.Name, agents, group.Id, chats.ToArray()));
        }

        // Заказы и визиты Linko обновлены в понедельник утром — после конца недели: данные полные.
        foreach (var entity in new[] { "orders", "visits" })
        {
            var state = await db.LinkoSyncStates.FirstOrDefaultAsync(s => s.Entity == entity);
            if (state is null)
            {
                db.LinkoSyncStates.Add(state = new LinkoSyncState { Entity = entity });
            }

            state.LastSuccessAt = Utc(Monday, 8, 55);
        }

        db.TelegramReportSettings.Add(new TelegramReportSettings { AccountId = account.Id, Language = "ru" });
        db.TelegramWeeklyPdfSettings.Add(new TelegramWeeklyPdfSettings { RequireCompleteData = true, SvTpCards = false });
        await db.SaveChangesAsync();
        fixture.Telegram.Sessions.Add(account.Id);
        scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().InvalidateHistory();
        return (admin.Id, regions);
    }

    private TelegramReportRunner Runner(IServiceProvider sp)
    {
        var runner = sp.GetRequiredService<TelegramReportRunner>();
        runner.Delay = (_, _) => Task.CompletedTask;
        runner.Refresh = (_, _) => Task.CompletedTask;
        return runner;
    }

    [PostgresFact]
    public async Task Three_supervisors_and_nine_agents_get_their_own_weekly_pdfs_once()
    {
        var (admin, regions) = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TelegramReportService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();

        // Получатели по оргструктуре «Продаж»: СВ региона — в группу региона, каждый ТП — в свой личный чат.
        foreach (var region in regions)
        {
            await service.SaveRecipientAsync(null, new ReportRecipientInput("sv", null, null, region.Id, null, null, [region.GroupChat], ["weekly-pdf.monday"], true), true, admin, default);
            foreach (var (agent, chat) in region.Agents.Zip(region.AgentChats))
            {
                await service.SaveRecipientAsync(null, new ReportRecipientInput("tp", null, null, null, null, agent, [chat], ["weekly-pdf.monday"], true), true, admin, default);
            }
        }

        await service.SaveJobAsync("weekly-pdf.monday", new ReportJobInput(true, "09:00", "Asia/Tashkent", [1], null), admin, default);

        // 09:01: запуск ставит 12 отправок; «служба перезапущена» до отправки — новый экземпляр отправляет их все.
        fixture.Clock.Now = Utc(Monday, 9, 1);
        Assert.Equal(12, await Runner(sp).EnqueueDueAsync(default));
        using (var restarted = fixture.Services.CreateScope())
        {
            Assert.Equal(12, await Runner(restarted.ServiceProvider).TickAsync(default));
        }

        // Ещё круг и повторная постановка — без дублей (ключ «запуск × день × чат × область»).
        Assert.Equal(0, await Runner(sp).TickAsync(default));
        Assert.Equal(0, await Runner(sp).EnqueueDueAsync(default));

        var sent = fixture.Telegram.Sent.Where(m => m.Message.Files.Any(f => f.ContentType == "application/pdf")).ToList();
        Assert.Equal(12, sent.Count);
        var chats = await db.TelegramChats.AsNoTracking().ToDictionaryAsync(c => c.PeerId);
        foreach (var region in regions)
        {
            // СВ: один PDF команды (10 страниц), подпись — неделя 19–24.10 и регион.
            var group = sent.Single(m => chats[m.Peer.PeerId].Id == region.GroupChat).Message;
            var team = Assert.Single(group.Files);
            Assert.StartsWith("weekly-sv-", team.FileName);
            Assert.Equal(10, WeeklyPdfService.Pages(team.Content));
            Assert.Contains("19.10–24.10.2026", group.Text);
            Assert.Contains(region.Name, group.Text);

            // ТП: своя карточка (2 страницы), в подписи — только он.
            foreach (var (agent, chatId) in region.Agents.Zip(region.AgentChats))
            {
                var message = sent.Single(m => chats[m.Peer.PeerId].Id == chatId).Message;
                var card = Assert.Single(message.Files);
                var name = chats.Values.Single(c => c.Id == chatId).Title;
                Assert.StartsWith("weekly-cards-", card.FileName);
                Assert.Equal(2, WeeklyPdfService.Pages(card.Content));
                Assert.Contains(name, message.Text);
                Assert.DoesNotContain(regions.SelectMany(r => r.AgentChats).Where(c => c != chatId).Select(c => chats.Values.Single(x => x.Id == c).Title), other => message.Text!.Contains(other + " "));
            }
        }

        // Журнал: 12 отправлено, этап «отправлен», данные полные; файлы в хранилище; запуск — «выполнен», 12 из 12.
        var rows = await db.TelegramReportDeliveries.AsNoTracking().Where(d => d.Report == "weekly-pdf.monday").ToListAsync();
        Assert.Equal(12, rows.Count);
        Assert.All(rows, d => Assert.Equal(("sent", WeeklyPdfService.StageSent, 1), (d.Status, d.Stage, d.Attempts)));
        Assert.All(rows, d => Assert.Contains(", полные.", d.Log));
        Assert.Equal(12, rows.Sum(d => WeeklyPdfService.FilesOf(d).Count));
        var run = await db.TelegramReportRuns.AsNoTracking().SingleAsync(r => r.Job == "weekly-pdf.monday");
        Assert.Equal(("done", 12, 12, 0), (run.Status, run.Planned, run.Sent, run.Failed));
    }
}

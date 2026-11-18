using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// «11. Итоги дня по категориям» на PostgreSQL: продажи категории за день в области получателя (СВ — регион и его ТП с планом дня, ТП —
/// только он, вся страна — регионы), утро — прошлый рабочий день, выбор категорий — общий с «Карточками категорий», отправка по расписанию —
/// одним альбомом по картинке на категорию.
/// </summary>
public partial class TelegramReportDbTests
{
    [PostgresFact]
    public async Task Category_day_results_show_the_day_sales_of_the_scope_with_day_plans_and_go_on_schedule()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var engine = sp.GetRequiredService<ReportEngine>();
        var service = sp.GetRequiredService<TelegramReportService>();
        var cards = sp.GetRequiredService<CategoryCardService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var yesterday = s.Today.AddDays(-1);
        var planned = yesterday.Month == s.Today.Month && DailyPlanMath.IsWorkingDay(yesterday, new Dictionary<DateOnly, string>()); // планы ТП — на месяц сегодняшнего дня

        // СВ Хоразма, итог вчерашнего дня: категории в порядке выбора, продажи только за этот день.
        var sv = await engine.BuildAsync("category-day.evening", new ReportScope(TelegramReportRoles.Sv, RegionId: s.RegionId, SupervisorId: SupervisorId), yesterday,
            Local(yesterday, 19), "ru", 30, default, ["кекс", "Бамбук"]);
        Assert.True(sv.Applies);
        Assert.Null(sv.CategorySales);
        var payload = Assert.IsType<CategoryDayCards>(sv.CategoryDay);
        Assert.Equal(["Кекс", "Бамбук"], payload.Cards.Select(c => c.Name));
        Assert.Equal(yesterday, payload.Day);
        var cake = payload.Cards[0];
        Assert.Equal((90m, 1), (cake.Kg, cake.Akb)); // три ТП по 30 кг «Кекс 1 кг» в одной точке
        Assert.True(cake.Orders >= 1);
        Assert.True(cake.Skus >= 1);
        Assert.Contains(cake.Top, x => x.Kg > 0 && x.MonthKg >= x.Kg);
        Assert.Equal(14, cake.Days.Count);
        Assert.Contains(cake.Days, d => d.Day == yesterday && d.Kg == 90);
        Assert.Equal(CategorySalesCards.Agents, cake.BreakdownKind);
        Assert.Equal(["Бахтиёр Юлдашев", "Дилшод Рахимов", "Отабек Назаров"], cake.Breakdown.Select(r => r.Name).Order());
        Assert.All(cake.Breakdown, r => Assert.Equal(30m, r.Kg));
        Assert.True(cake.MonthKg >= cake.Kg);
        if (planned)
        {
            // План дня — остаток плана месяца (26 000 + 21 000 + 13 000) ÷ рабочие дни с дня итога; выполнение — факт дня к нему.
            Assert.Equal(60_000m, cake.MonthPlanKg);
            Assert.NotNull(cake.DayPlanKg);
            Assert.Equal(cake.Kg / cake.DayPlanKg!.Value, cake.DayExecution);
            Assert.NotNull(cake.Breakdown.Single(r => r.Name == "Бахтиёр Юлдашев").DayPlanKg);
        }

        var bamboo = payload.Cards[1];
        Assert.Equal((0m, 0, (decimal?)null), (bamboo.Kg, bamboo.Akb, bamboo.DayPlanKg));
        Assert.Contains("Кекс: 90 кг за день", sv.Caption);
        Assert.Equal(2, sp.GetRequiredService<IReportRenderer>().Render(sv).Count);

        // ТП — только его цифры, без разбивки; вся страна утром — итог прошлого рабочего дня по регионам.
        var tp = await engine.BuildAsync("category-day.evening", new ReportScope(TelegramReportRoles.Tp, AgentId: 61), yesterday, Local(yesterday, 19), "ru", 30,
            default, ["Кекс"]);
        var own = Assert.Single(tp.CategoryDay!.Cards);
        Assert.Equal(30m, own.Kg);
        Assert.Null(own.BreakdownKind);

        var country = await engine.BuildAsync("category-day.evening", new ReportScope(TelegramReportRoles.Rm), yesterday, Local(yesterday, 19), "ru", 30, default, ["Кекс"]);
        var all = Assert.Single(country.CategoryDay!.Cards);
        Assert.Equal(CategorySalesCards.Regions, all.BreakdownKind);
        Assert.Contains(all.Breakdown, r => r.Name == "Хоразм" && r.Kg == 90);
        var morning = await engine.BuildAsync("category-day.morning", new ReportScope(TelegramReportRoles.Rm), s.Today, Local(s.Today, 9), "ru", 30, default, ["Кекс"]);
        Assert.Equal(CategoryCardChoice.PreviousWorkDay(s.Today), morning.CategoryDay!.Day);

        // Получатель отчёта 11 со своим выбором категорий (тот же, что у карточек): по расписанию — альбом из одной картинки «Кекс».
        var recipient = await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId], "category-day.evening"), true, s.AdminId, default);
        var view = await cards.SaveRecipientAsync(recipient.Id, new CategoryCardsInput(["Кекс"]), s.AdminId, default);
        Assert.Equal(["Кекс"], view.Recipients.Single(r => r.Id == recipient.Id).Categories);
        var preview = await service.PreviewAsync(new ReportPreviewInput("category-day.evening", null, null, null, null, null, recipient.Id, yesterday, null, null), true, default);
        Assert.Single(preview.Pages);

        fixture.Clock.Now = Utc(s.Today, 17, 0);
        await service.SaveJobAsync("category-day.evening", new ReportJobInput(true, "19:00", "Asia/Tashkent", EveryDay, null), s.AdminId, default);
        fixture.Clock.Now = Utc(s.Today, 19, 1);
        await Runner(sp).TickAsync(default);
        var sent = Assert.Single(fixture.Telegram.Sent, m => m.Peer.PeerId == GroupPeer && m.Message.Text!.StartsWith("11. Итоги дня по категориям", StringComparison.Ordinal));
        Assert.Single(sent.Message.Files, f => f.Photo);
        Assert.Contains("Кекс:", sent.Message.Text);
        Assert.DoesNotContain("Бамбук", sent.Message.Text);
        Assert.Equal(TelegramReportSender.Sent, await db.TelegramReportDeliveries.Where(d => d.Report == "category-day.evening" && d.SubscriptionId == recipient.Id)
            .Select(d => d.Status).SingleAsync());
    }
}

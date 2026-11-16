using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// «10. Карточки категорий» на PostgreSQL: данные страницы категории «Продаж» в области получателя (СВ — регион и его ТП с планами «Кекс
/// Хоразм», ТП — только он, вся страна — регионы), выбор категорий по умолчанию и у получателя (обычное сохранение получателя его не
/// стирает), отправка по расписанию — одним альбомом по картинке на категорию.
/// </summary>
public partial class TelegramReportDbTests
{
    [PostgresFact]
    public async Task Category_cards_show_the_category_of_the_scope_with_plans_and_go_with_the_recipients_choice()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var engine = sp.GetRequiredService<ReportEngine>();
        var service = sp.GetRequiredService<TelegramReportService>();
        var cards = sp.GetRequiredService<CategoryCardService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var yesterday = s.Today.AddDays(-1);
        var sameMonth = yesterday.Month == s.Today.Month; // планы ТП — на месяц сегодняшнего дня

        // СВ Хоразма, итог вчерашнего дня: категории в порядке выбора (регистр не важен, неизвестная — без карточки).
        var sv = await engine.BuildAsync("category-cards.evening", new ReportScope(TelegramReportRoles.Sv, RegionId: s.RegionId, SupervisorId: SupervisorId), yesterday,
            Local(yesterday, 18, 30), "ru", 30, default, ["кекс", "Бамбук", "Нет такой"]);
        Assert.True(sv.Applies);
        var payload = Assert.IsType<CategorySalesCards>(sv.CategorySales);
        Assert.Equal(["Кекс", "Бамбук"], payload.Cards.Select(c => c.Name));
        Assert.Equal(yesterday, payload.Day);
        var cake = payload.Cards[0];
        Assert.Equal(90m, cake.DayKg); // три ТП по 30 кг «Кекс 1 кг»
        Assert.Equal(1, cake.DayAkb); // одна точка
        Assert.True(cake.FactKg >= 90);
        Assert.Equal(CategorySalesCards.Agents, cake.BreakdownKind);
        Assert.Equal(["Бахтиёр Юлдашев", "Дилшод Рахимов", "Отабек Назаров"], cake.Breakdown.Select(r => r.Name).Order());
        if (sameMonth)
        {
            Assert.Equal(60_000m, cake.PlanKg); // планы ТП «Кекс Хоразм»: 26 000 + 21 000 + 13 000
            Assert.Null(cake.PlanShared);
            Assert.Equal(cake.FactKg / 60_000m, cake.Execution);
            Assert.Equal(26_000m, cake.Breakdown.Single(r => r.Name == "Бахтиёр Юлдашев").PlanKg);
        }

        Assert.Contains(cake.Days, d => d.Day == yesterday && d.Kg == 90);
        var bamboo = payload.Cards[1];
        Assert.Equal((0m, 0m, (decimal?)null), (bamboo.FactKg, bamboo.DayKg, bamboo.PlanKg));
        Assert.Contains("Кекс:", sv.Caption);
        Assert.Equal(2, sp.GetRequiredService<IReportRenderer>().Render(sv).Count);

        // ТП — только его цифры, без разбивки; вся страна — по регионам.
        var tp = await engine.BuildAsync("category-cards.evening", new ReportScope(TelegramReportRoles.Tp, AgentId: 61), yesterday, Local(yesterday, 18, 30), "ru", 30,
            default, ["Кекс"]);
        var own = Assert.Single(tp.CategorySales!.Cards);
        Assert.Equal(30m, own.DayKg);
        Assert.Null(own.BreakdownKind);
        if (sameMonth)
        {
            Assert.Equal(26_000m, own.PlanKg);
        }

        var country = await engine.BuildAsync("category-cards.evening", new ReportScope(TelegramReportRoles.Rm), yesterday, Local(yesterday, 18, 30), "ru", 30, default,
            ["Кекс"]);
        var all = Assert.Single(country.CategorySales!.Cards);
        Assert.Equal(CategorySalesCards.Regions, all.BreakdownKind);
        Assert.Contains(all.Breakdown, r => r.Name == "Хоразм" && r.Kg >= 90);

        // Выбор по умолчанию — в настройках (неизвестная категория — отказ); без выбора у получателя уходит он.
        await Assert.ThrowsAsync<ReportValidationException>(() => cards.SaveDefaultsAsync(new CategoryCardsInput(["Нет такой"]), s.AdminId, default));
        var view = await cards.SaveDefaultsAsync(new CategoryCardsInput(["бамбук", "Придзел"]), s.AdminId, default);
        Assert.Equal(["Бамбук", "Придзел"], view.Defaults);
        Assert.Contains(view.Categories, c => c.Name == "Помадка" && c.Alias == "Могуль");
        var defaults = await engine.BuildAsync("category-cards.morning", new ReportScope(TelegramReportRoles.Rm), s.Today, Local(s.Today, 9), "ru", 30, default);
        Assert.Equal(["Бамбук", "Придзел"], defaults.CategorySales!.Cards.Select(c => c.Name));
        Assert.Equal(CategoryCardChoice.PreviousWorkDay(s.Today), defaults.CategorySales.Day); // утро — итог прошлого рабочего дня

        // Получатель со своим выбором: обычное сохранение получателя (вкладка «Получатели») выбор не стирает.
        var recipient = await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId], "category-cards.evening"), true, s.AdminId, default);
        view = await cards.SaveRecipientAsync(recipient.Id, new CategoryCardsInput(["Кекс"]), s.AdminId, default);
        Assert.Equal(["Кекс"], view.Recipients.Single(r => r.Id == recipient.Id).Categories);
        await service.SaveRecipientAsync(recipient.Id, Sv(s, [s.GroupChatId], "category-cards.evening", "daily.evening"), true, s.AdminId, default);
        Assert.Equal("Кекс", await db.TelegramReportSubscriptions.Where(x => x.Id == recipient.Id).Select(x => x.Categories).SingleAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => cards.SaveRecipientAsync(Guid.NewGuid(), new CategoryCardsInput(["Кекс"]), s.AdminId, default));

        // Предпросмотр по получателю — его выбор; выбранные в предпросмотре категории важнее.
        var preview = await service.PreviewAsync(new ReportPreviewInput("category-cards.evening", null, null, null, null, null, recipient.Id, s.Today, null, null), true, default);
        Assert.Single(preview.Pages);
        var picked = await service.PreviewAsync(new ReportPreviewInput("category-cards.evening", "rm", null, null, null, null, null, s.Today, null, null,
            Categories: ["Кекс", "Помадка", "Шоколад"]), true, default);
        Assert.Equal(3, picked.Pages.Count);

        // По расписанию — одним альбомом: в групповой чат СВ ушла одна картинка (только «Кекс»).
        fixture.Clock.Now = Utc(s.Today, 16, 30);
        await service.SaveJobAsync("category-cards.evening", new ReportJobInput(true, "18:30", "Asia/Tashkent", EveryDay, null), s.AdminId, default);
        fixture.Clock.Now = Utc(s.Today, 18, 31);
        await Runner(sp).TickAsync(default);
        var sent = Assert.Single(fixture.Telegram.Sent, m => m.Peer.PeerId == GroupPeer && m.Message.Text!.StartsWith("10. Карточки категорий", StringComparison.Ordinal));
        Assert.Single(sent.Message.Files, f => f.Photo);
        Assert.Contains("Кекс:", sent.Message.Text);
        Assert.DoesNotContain("Бамбук", sent.Message.Text);
        Assert.Equal(TelegramReportSender.Sent, await db.TelegramReportDeliveries.Where(d => d.Report == "category-cards.evening" && d.SubscriptionId == recipient.Id)
            .Select(d => d.Status).SingleAsync());
    }
}

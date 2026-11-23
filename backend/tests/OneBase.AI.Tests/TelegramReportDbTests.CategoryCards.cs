using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Persistence.Migrations;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// «Карточки категорий» на PostgreSQL: выбор категорий по умолчанию и у получателя для «11. Итогов дня по категориям» (обычное сохранение
/// получателя выбор не стирает) и перенос прежнего отчёта «10. Карточки категорий» миграцией: подписки и включённые запуски 10 → 11, не
/// начатые отправки 10 — пропущены, повторный запуск ничего не меняет, перенесённый получатель получает итог дня по расписанию.
/// </summary>
public partial class TelegramReportDbTests
{
    [PostgresFact]
    public async Task Category_choice_goes_to_day_results_by_default_and_per_recipient()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var engine = sp.GetRequiredService<ReportEngine>();
        var service = sp.GetRequiredService<TelegramReportService>();
        var cards = sp.GetRequiredService<CategoryCardService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();

        // Выбор по умолчанию — в настройках (неизвестная категория — отказ); без выбора у получателя уходит он.
        await Assert.ThrowsAsync<ReportValidationException>(() => cards.SaveDefaultsAsync(new CategoryCardsInput(["Нет такой"]), s.AdminId, default));
        var view = await cards.SaveDefaultsAsync(new CategoryCardsInput(["бамбук", "Придзел"]), s.AdminId, default);
        Assert.Equal(["Бамбук", "Придзел"], view.Defaults);
        Assert.Contains(view.Categories, c => c.Name == "Помадка" && c.Alias == "Могуль");
        var defaults = await engine.BuildAsync("category-day.morning", new ReportScope(TelegramReportRoles.Rm), s.Today, Local(s.Today, 9), "ru", 30, default);
        Assert.Equal(["Бамбук", "Придзел"], defaults.CategoryDay!.Cards.Select(c => c.Name));
        Assert.Equal(CategoryCardChoice.PreviousWorkDay(s.Today), defaults.CategoryDay.Day); // утро — итог прошлого рабочего дня

        // Свой выбор получателя: обычное сохранение получателя (вкладка «Получатели») его не стирает.
        var recipient = await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId], "category-day.evening"), true, s.AdminId, default);
        view = await cards.SaveRecipientAsync(recipient.Id, new CategoryCardsInput(["Кекс"]), s.AdminId, default);
        Assert.Equal(["Кекс"], view.Recipients.Single(r => r.Id == recipient.Id).Categories);
        await service.SaveRecipientAsync(recipient.Id, Sv(s, [s.GroupChatId], "category-day.evening", "daily.evening"), true, s.AdminId, default);
        Assert.Equal("Кекс", await db.TelegramReportSubscriptions.Where(x => x.Id == recipient.Id).Select(x => x.Categories).SingleAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => cards.SaveRecipientAsync(Guid.NewGuid(), new CategoryCardsInput(["Кекс"]), s.AdminId, default));

        // Код убранного отчёта 10 в подписку больше не попадает.
        var stale = await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId], "category-cards.evening", "daily.evening"), true, s.AdminId, default);
        Assert.Equal("daily.evening", await db.TelegramReportSubscriptions.Where(x => x.Id == stale.Id).Select(x => x.Reports).SingleAsync());

        // Предпросмотр по получателю — его выбор; выбранные в предпросмотре категории важнее.
        var preview = await service.PreviewAsync(new ReportPreviewInput("category-day.evening", null, null, null, null, null, recipient.Id, s.Today, null, null), true, default);
        Assert.Single(preview.Pages);
        var picked = await service.PreviewAsync(new ReportPreviewInput("category-day.evening", "rm", null, null, null, null, null, s.Today, null, null,
            Categories: ["Кекс", "Помадка", "Шоколад"]), true, default);
        Assert.Equal(3, picked.Pages.Count);
    }

    [PostgresFact]
    public async Task Retired_month_cards_move_their_recipients_and_schedule_to_day_results()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var service = sp.GetRequiredService<TelegramReportService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();

        // Как в проде до обновления: вечерние карточки 10 включены в 18:30, утренние выключены; итогов дня 11 в расписании ещё нет.
        db.TelegramReportJobs.AddRange(
            new TelegramReportJob { Code = "category-cards.evening", Enabled = true, Time = new TimeOnly(18, 30), TimeZone = "Asia/Tashkent", Days = 63, CatchUpMinutes = 180, Version = 3 },
            new TelegramReportJob { Code = "category-cards.morning", Enabled = false, Time = new TimeOnly(9, 0), TimeZone = "Asia/Tashkent", Days = 63, CatchUpMinutes = 180 },
            new TelegramReportJob { Code = "daily.evening", Enabled = true, Time = new TimeOnly(18, 0), TimeZone = "Asia/Tashkent", Days = 63, CatchUpMinutes = 180, Version = 2 });
        TelegramReportSubscription Old(string reports, Guid chat, string? categories = null) => new()
        {
            Role = TelegramReportRoles.Sv, RegionId = s.RegionId, SupervisorId = SupervisorId, Reports = reports, Categories = categories, CreatedById = s.AdminId,
            Chats = [new TelegramReportSubscriptionChat { ChatId = chat }],
        };
        var evening = Old("daily.evening,category-cards.evening", s.GroupChatId, "Кекс");
        var both = Old("category-cards.morning, category-day.morning,category-cards.evening", s.PrivateChatId);
        var other = Old("daily.morning", s.GroupChatId);
        db.TelegramReportSubscriptions.AddRange(evening, both, other);
        TelegramReportDelivery Delivery(DateOnly period, string status) => new()
        {
            Report = "category-cards.evening", Period = period, Trigger = "schedule", DataMode = "real", Role = TelegramReportRoles.Sv, ScopeKey = $"old:{status}",
            AccountId = s.AccountId, ChatId = s.GroupChatId, RecipientTitle = "СВ Хоразм", ScopeTitle = "Хоразм", Status = status,
        };
        var queued = Delivery(s.Today, TelegramReportSender.Pending);
        var sent = Delivery(s.Today.AddDays(-1), TelegramReportSender.Sent);
        db.TelegramReportDeliveries.AddRange(queued, sent);
        await db.SaveChangesAsync();

        await Migrate(db);

        // Подписки: коды 10 → 11 того же момента, без повторов и в прежнем порядке; свой выбор категорий не тронут; чужие подписки — тоже.
        var rows = await db.TelegramReportSubscriptions.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Equal("daily.evening,category-day.evening", rows[evening.Id].Reports);
        Assert.Equal("Кекс", rows[evening.Id].Categories);
        Assert.Equal("category-day.morning,category-day.evening", rows[both.Id].Reports);
        Assert.Equal("daily.morning", rows[other.Id].Reports);
        Assert.DoesNotContain(rows.Values, x => x.Reports.Contains("category-cards", StringComparison.Ordinal));

        // Расписание: вечер 11 включён с временем, днями и поясом вечера 10; утро 10 не было включено — утро 11 не включается; запуски 10 выключены.
        var jobs = await db.TelegramReportJobs.AsNoTracking().ToDictionaryAsync(j => j.Code);
        var day = jobs["category-day.evening"];
        Assert.True(day.Enabled);
        Assert.Equal((new TimeOnly(18, 30), "Asia/Tashkent", 63, 180), (day.Time, day.TimeZone, day.Days, day.CatchUpMinutes));
        Assert.NotNull(day.ArmedAt);
        Assert.False(jobs.ContainsKey("category-day.morning"));
        Assert.False(jobs["category-cards.evening"].Enabled);
        Assert.Equal(4, jobs["category-cards.evening"].Version);
        Assert.Equal((true, 2), (jobs["daily.evening"].Enabled, jobs["daily.evening"].Version));

        // Журнал: не начатая отправка 10 — пропущена с причиной; отправленная — как была, с понятным названием в истории.
        var deliveries = await db.TelegramReportDeliveries.AsNoTracking().ToDictionaryAsync(d => d.Id);
        Assert.Equal(TelegramReportSender.Skipped, deliveries[queued.Id].Status);
        Assert.Contains("переведены на отчёт 11", deliveries[queued.Id].Error);
        Assert.Equal(TelegramReportSender.Sent, deliveries[sent.Id].Status);
        var history = await service.HistoryAsync(new ReportHistoryQuery(null, null, null, null, null), default);
        Assert.Contains(history.Items, d => d.Report == "category-cards.evening" && d.ReportTitle == "10. Карточки категорий (отчёт убран)");

        // Повторный запуск ничего не меняет.
        await Migrate(db);
        Assert.Equal(rows.Values.Select(x => x.Reports).Order(), (await db.TelegramReportSubscriptions.AsNoTracking().Select(x => x.Reports).ToListAsync()).Order());
        Assert.Equal(day.Version, (await db.TelegramReportJobs.AsNoTracking().SingleAsync(j => j.Code == "category-day.evening")).Version);

        // Если момент 11 уже включён — его время остаётся (утро 11 в 09:15), включённое утро 10 только выключается.
        await db.TelegramReportJobs.Where(j => j.Code == "category-cards.morning").ExecuteUpdateAsync(u => u.SetProperty(j => j.Enabled, true));
        db.TelegramReportJobs.Add(new TelegramReportJob { Code = "category-day.morning", Enabled = true, Time = new TimeOnly(9, 15), TimeZone = "Asia/Tashkent", Days = 63, CatchUpMinutes = 180 });
        await db.SaveChangesAsync();
        await Migrate(db);
        jobs = await db.TelegramReportJobs.AsNoTracking().ToDictionaryAsync(j => j.Code);
        Assert.Equal((true, new TimeOnly(9, 15)), (jobs["category-day.morning"].Enabled, jobs["category-day.morning"].Time));
        Assert.False(jobs["category-cards.morning"].Enabled);

        // В расписании и у получателей — только отчёт 11; по расписанию перенесённые получатели получают итог дня (в группу — только «Кекс»).
        var overview = await service.OverviewAsync(canUseSales: true, default);
        Assert.DoesNotContain(overview.Jobs, j => j.Code.StartsWith("category-cards.", StringComparison.Ordinal));
        Assert.Equal(2, overview.Jobs.Single(j => j.Code == "category-day.evening").Recipients);
        await db.TelegramReportJobs.Where(j => j.Code == "category-day.evening")
            .ExecuteUpdateAsync(u => u.SetProperty(j => j.ArmedAt, Utc(s.Today, 8)).SetProperty(j => j.Days, 127)); // миграция прошла утром
        fixture.Clock.Now = Utc(s.Today, 18, 31);
        await Runner(sp).TickAsync(default);
        var album = Assert.Single(fixture.Telegram.Sent, m => m.Peer.PeerId == GroupPeer && m.Message.Text!.StartsWith("11. Итоги дня по категориям", StringComparison.Ordinal));
        Assert.Single(album.Message.Files, f => f.Photo);
        Assert.Contains("Кекс:", album.Message.Text);
        Assert.DoesNotContain("Бамбук", album.Message.Text);
        Assert.Single(fixture.Telegram.Sent, m => m.Peer.PeerId == PrivatePeer && m.Message.Text!.StartsWith("11. Итоги дня по категориям", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Telegram.Sent, m => m.Message.Text?.StartsWith("10. Карточки категорий", StringComparison.Ordinal) == true);
        Assert.Equal([TelegramReportSender.Sent, TelegramReportSender.Sent], await db.TelegramReportDeliveries
            .Where(d => d.Report == "category-day.evening" && (d.SubscriptionId == evening.Id || d.SubscriptionId == both.Id)).Select(d => d.Status).ToListAsync());
    }

    /// <summary>SQL миграции TelegramCategoryCardsRetired — по порядку, как в Up.</summary>
    private static async Task Migrate(OneBaseDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(TelegramCategoryCardsRetired.Jobs);
        await db.Database.ExecuteSqlRawAsync(TelegramCategoryCardsRetired.Subscriptions);
        await db.Database.ExecuteSqlRawAsync(TelegramCategoryCardsRetired.Deliveries);
    }
}

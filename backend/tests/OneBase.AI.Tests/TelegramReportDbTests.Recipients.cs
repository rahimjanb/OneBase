using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Reports;
using OneBase.Application.Telegram.Weekly;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// «Автоотчёты → Получатели»: отключение с причиной и сроком (включение очищает, сам после срока не включается), супервайзер ТП — СВР его
/// региона, массовое действие одним сохранением (ошибка в одной строке — не сохраняется ни одна), пропуски за 7 дней по причинам, РМ —
/// пользователь с ролью «РМ» и регионами из профиля.
/// </summary>
public partial class TelegramReportDbTests
{
    [PostgresFact]
    public async Task Recipient_is_disabled_with_reason_and_term_and_enabling_clears_them()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TelegramReportService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();

        var tp = await service.SaveRecipientAsync(null, Tp(63, s.AgentChatId, "daily.evening"), true, s.AdminId, default);
        Assert.True(tp.Enabled);
        Assert.Null(tp.DisabledAt);
        Assert.Equal("Азиз Каримов", tp.Supervisor); // СВР региона ТП — как на его картинке

        // Срок в прошлом не принимается; причина и срок сохраняются, дата отключения — сейчас.
        var input = Tp(63, s.AgentChatId, "daily.evening") with { Enabled = false, DisabledReason = "  отпуск  ", DisabledUntil = s.Today.AddDays(-1) };
        var past = await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientAsync(tp.Id, input, true, s.AdminId, default));
        Assert.Contains("не раньше сегодняшнего", past.Message);
        var off = await service.SaveRecipientAsync(tp.Id, input with { DisabledUntil = s.Today.AddDays(5) }, true, s.AdminId, default);
        Assert.False(off.Enabled);
        Assert.Equal("отпуск", off.DisabledReason);
        Assert.Equal(s.Today.AddDays(5), off.DisabledUntil);
        Assert.Equal(fixture.Clock.Now, off.DisabledAt);

        // Правка выключенного не сдвигает дату отключения; прошедший срок не мешает сохранить, сам получатель не включается.
        fixture.Clock.Now = fixture.Clock.Now!.Value.AddDays(10);
        await db.TelegramReportSubscriptions.Where(x => x.Id == tp.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.DisabledUntil, s.Today.AddDays(1)));
        db.ChangeTracker.Clear(); // правка мимо отслеживания — как новый запрос
        var edited = await service.SaveRecipientAsync(tp.Id, Tp(63, s.AgentChatId, "daily.evening", "daily.midday") with
        {
            Enabled = false, DisabledReason = "отпуск", DisabledUntil = s.Today.AddDays(1),
        }, true, s.AdminId, default);
        Assert.False(edited.Enabled);
        Assert.Equal(off.DisabledAt, edited.DisabledAt);
        Assert.Equal(s.Today.AddDays(1), edited.DisabledUntil);

        var on = await service.SaveRecipientAsync(tp.Id, Tp(63, s.AgentChatId, "daily.evening"), true, s.AdminId, default);
        Assert.True(on.Enabled);
        Assert.Null(on.DisabledAt);
        Assert.Null(on.DisabledReason);
        Assert.Null(on.DisabledUntil);
        var row = await db.TelegramReportSubscriptions.AsNoTracking().SingleAsync(x => x.Id == tp.Id);
        Assert.Null(row.DisabledAt);
        Assert.Null(row.DisabledReason);
        Assert.Null(row.DisabledUntil);
    }

    [PostgresFact]
    public async Task Bulk_action_saves_every_recipient_at_once_or_none()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TelegramReportService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var sv = await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId], "daily.evening"), true, s.AdminId, default);
        var tp = await service.SaveRecipientAsync(null, Tp(63, s.AgentChatId, "daily.evening"), true, s.AdminId, default);

        // «Включить отчёт» обоим: «Поле боя» в 18:00 и чат группы.
        var saved = await service.SaveRecipientsAsync(new ReportRecipientsBulkInput(
        [
            new ReportRecipientBulkItem(sv.Id, Sv(s, [s.GroupChatId, s.PrivateChatId], "daily.evening", "battle.evening")),
            new ReportRecipientBulkItem(tp.Id, Tp(63, s.AgentChatId, "daily.evening", "battle.evening")),
        ]), true, s.AdminId, default);
        Assert.Equal(2, saved.Count);
        Assert.All(saved, r => Assert.Contains("battle.evening", r.Reports));
        Assert.Equal(2, saved.Single(r => r.Id == sv.Id).Chats.Count);

        // У второй строки нет чатов — не сохраняется ни одна, в ошибке — кто.
        var error = await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientsAsync(new ReportRecipientsBulkInput(
        [
            new ReportRecipientBulkItem(sv.Id, Sv(s, [s.GroupChatId], "daily.evening") with { Enabled = false, DisabledReason = "отпуск" }),
            new ReportRecipientBulkItem(tp.Id, new ReportRecipientInput(TelegramReportRoles.Tp, null, null, null, null, 63, [], ["daily.evening"], false)),
        ]), true, s.AdminId, default));
        Assert.StartsWith("«Отабек Назаров»: ", error.Message);
        Assert.Contains("чат", error.Message);
        var rows = await db.TelegramReportSubscriptions.AsNoTracking().Where(x => x.Id == sv.Id || x.Id == tp.Id).ToListAsync();
        Assert.All(rows, r => Assert.True(r.Enabled));
        Assert.All(rows, r => Assert.Contains("battle.evening", r.Reports));

        // Пусто, повтор, без права на продажи — отказ.
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientsAsync(new ReportRecipientsBulkInput([]), true, s.AdminId, default));
        await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientsAsync(new ReportRecipientsBulkInput(
            [new ReportRecipientBulkItem(sv.Id, Sv(s, [s.GroupChatId], "daily.evening")), new ReportRecipientBulkItem(sv.Id, Sv(s, [s.GroupChatId], "daily.evening"))]),
            true, s.AdminId, default));
        await Assert.ThrowsAsync<ReportForbiddenException>(() => service.SaveRecipientsAsync(new ReportRecipientsBulkInput(
            [new ReportRecipientBulkItem(sv.Id, Sv(s, [s.GroupChatId], "daily.evening"))]), false, s.AdminId, default));
    }

    [PostgresFact]
    public async Task Skipped_sends_of_the_week_are_counted_by_reason()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TelegramReportService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var now = fixture.Clock.Now!.Value;

        TelegramReportDelivery Skipped(string? error, int daysAgo = 1) => new()
        {
            Report = "daily.evening", Period = s.Today, Trigger = "schedule", DataMode = "real", AccountId = s.AccountId, RecipientTitle = "чат", ScopeTitle = "Хоразм",
            Status = TelegramReportSender.Skipped, Error = error, CreatedAt = now.AddDays(-daysAgo),
        };
        db.TelegramReportDeliveries.AddRange(
            Skipped("Чат не найден у аккаунта автоотчётов — обновите чаты в «Настройки → Telegram → Чаты»."),
            Skipped("Пользователь заблокировал аккаунт."),
            Skipped("чата больше нет в диалогах аккаунта"),
            Skipped("Получатель отключён или отписан от этого отчёта."),
            Skipped("Накопительной акции в этот день нет: акция выключена или день вне её периода."),
            Skipped("Заказы Linko не обновились к отправке — карточка акции не отправлена (в настройках акции: при несвежих данных пропускать)."),
            Skipped("Условие отчёта не выполнено."),
            Skipped("Чат не найден у аккаунта автоотчётов — обновите чаты в «Настройки → Telegram → Чаты».", daysAgo: 8)); // старше недели — не в счёт
        await db.SaveChangesAsync();

        // У получателя: последняя доставка — по всему журналу, ошибки и «неизвестно» — за 7 дней.
        var sv = await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId], "daily.evening"), true, s.AdminId, default);
        TelegramReportDelivery Of(string status, int daysAgo) => new()
        {
            Report = "daily.evening", Period = s.Today, Trigger = "schedule", DataMode = "real", AccountId = s.AccountId, RecipientTitle = "Хоразм — продажи",
            ScopeTitle = "Хоразм", SubscriptionId = sv.Id, Status = status, CreatedAt = now.AddDays(-daysAgo),
            SentAt = status == TelegramReportSender.Sent ? now.AddDays(-daysAgo).AddMinutes(1) : null,
        };
        db.TelegramReportDeliveries.AddRange(Of(TelegramReportSender.Sent, 20), Of(TelegramReportSender.Sent, 3), Of(TelegramReportSender.Failed, 2),
            Of(TelegramReportSender.Unknown, 1), Of(TelegramReportSender.Failed, 9));
        await db.SaveChangesAsync();

        var overview = await service.OverviewAsync(canUseSales: true, default);
        var recipient = Assert.Single(overview.Recipients, r => r.Id == sv.Id);
        Assert.Equal(now.AddDays(-3).AddMinutes(1), recipient.LastSentAt);
        Assert.Equal(2, recipient.FailedWeek);
        var metrics = overview.Metrics;
        Assert.Equal(7, metrics.Skipped);
        Assert.Equal(
            [new ReportSkipCount("no_chat", 3), new ReportSkipCount("disabled", 1), new ReportSkipCount("not_needed", 1), new ReportSkipCount("promo", 1),
                new ReportSkipCount("stale", 1)],
            metrics.SkippedBy);
    }

    [PostgresFact]
    public async Task Rm_recipient_is_a_user_with_the_rm_role_and_follows_the_regions_in_the_profile()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TelegramReportService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var user = new User { Login = $"rep-rm-{Guid.NewGuid():N}"[..20], FullName = "Рустам Алиев" };
        db.Users.Add(user);
        db.SalesUserScopes.Add(new SalesUserScope { UserId = user.Id, Level = SalesScopeLevel.Rm, RegionIds = [s.RegionId], UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        try
        {
            // В выборе РМ — пользователь с ролью «РМ»: регионы из профиля и ТП этих регионов.
            var option = Assert.Single((await service.OverviewAsync(true, default)).RmUsers!, u => u.Id == user.Id);
            Assert.Equal("Рустам Алиев", option.Name);
            Assert.Equal([s.RegionId], option.RegionIds);
            Assert.Equal(3, option.Agents);

            // Получатель — этот пользователь: направление не сохраняется, область — «РМ Рустам Алиев» по Хоразму.
            var rm = await service.SaveRecipientAsync(null, new ReportRecipientInput(TelegramReportRoles.Rm, null, Guid.NewGuid(), null, null, null, [s.PrivateChatId],
                ["daily.evening"], true, UserId: user.Id), true, s.AdminId, default);
            Assert.Equal(user.Id, rm.UserId);
            Assert.Null(rm.DirectionId);
            Assert.Equal("РМ Рустам Алиев", rm.Name);
            Assert.Equal(3, rm.Agents);
            Assert.Null(rm.Problem);
            var row = await db.TelegramReportSubscriptions.AsNoTracking().SingleAsync(x => x.Id == rm.Id);
            Assert.Equal(user.Id, row.UserId);
            Assert.Null(row.DirectionId);

            // Отчёт — по регионам из профиля; предпросмотр по получателю и по выбору «РМ — пользователь».
            var preview = await service.PreviewAsync(new ReportPreviewInput("daily.evening", null, null, null, null, null, rm.Id, s.Today, null, null), true, default);
            Assert.Equal("РМ Рустам Алиев", preview.Scope);
            var chosen = await service.PreviewAsync(new ReportPreviewInput("daily.evening", "rm", null, null, null, null, null, s.Today, null, null, user.Id), true, default);
            Assert.Equal("РМ Рустам Алиев", chosen.Scope);
            var engine = scope.ServiceProvider.GetRequiredService<ReportEngine>();
            var report = await engine.BuildAsync("daily.evening", ReportScope.Of(row), s.Today, Local(s.Today, 18, 5), "ru", 30, default);
            Assert.Contains("Хоразм", report.Plain());
            Assert.Contains("40 000 000", report.Plain()); // 25 + 15 млн сегодня — как у «Вся страна», других регионов с продажами в тесте нет

            // Еженедельный отчёт РМ-пользователя — роль «РМ» (не РОП), сумма его регионов.
            var weekly = await scope.ServiceProvider.GetRequiredService<WeeklyReportBuilder>().BuildAsync(ReportScope.Of(row), s.Today, false, default);
            Assert.Equal(TelegramReportRoles.Rm, weekly.Role);

            // В профиле поменяли регионы — отчёт идёт по новым: регион без ТП — получатель с причиной, а не «вся страна».
            var other = await db.SalesRegions.Where(r => r.Id != s.RegionId).Select(r => r.Id).FirstAsync();
            await db.SalesUserScopes.Where(x => x.UserId == user.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.RegionIds, new List<Guid> { other }));
            var moved = (await service.OverviewAsync(true, default)).Recipients.Single(r => r.Id == rm.Id);
            Assert.Contains("нет ТП", moved.Problem);

            // Роль РМ сняли — отчёт не уходит, причина видна в строке.
            await db.SalesUserScopes.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
            var lost = (await service.OverviewAsync(true, default)).Recipients.Single(r => r.Id == rm.Id);
            Assert.Contains("больше не РМ", lost.Problem);
            Assert.Equal("РМ Рустам Алиев", lost.Name); // в строке — по имени, а не «РМ»
            Assert.DoesNotContain((await service.OverviewAsync(true, default)).RmUsers!, u => u.Id == user.Id);
            await Assert.ThrowsAsync<ReportValidationException>(() => service.SaveRecipientAsync(rm.Id, new ReportRecipientInput(TelegramReportRoles.Rm, null, null, null,
                null, null, [s.PrivateChatId], ["daily.evening"], true, UserId: user.Id), true, s.AdminId, default));
        }
        finally
        {
            await db.SalesUserScopes.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        }
    }
}

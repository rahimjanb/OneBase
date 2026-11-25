using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Incentive;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// Накопительная акция с акциями ноября — всем только новая версия карточек: прежняя сводка «Команда · Twizos» у СВ и РМ не уходит (СВ — копии
/// новых карточек ТП, итог команды 19:30; РМ — итог зоны и сводка), запуск без положенного роли — «пропущено» с причиной «не требуется».
/// </summary>
public partial class TelegramReportDbTests
{
    [PostgresFact]
    public async Task With_november_promos_supervisor_and_manager_get_only_the_new_cards()
    {
        try
        {
            await OnlyNewCardsAsync();
        }
        finally
        {
            await CleanIncentiveAsync();
        }
    }

    private async Task OnlyNewCardsAsync()
    {
        var s = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TelegramReportService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await SeedIncentiveAsync(db, s.Today, enabled: true, IncentiveStaleData.Stamp);
        await WithNovemberPromosAsync(db, s.Today);
        fixture.Clock.Now = Utc(s.Today, 18, 5);

        await service.SaveRecipientAsync(null, Tp(61, s.OtherChatId, "incentive.evening"), true, s.AdminId, default);
        var sv = await service.SaveRecipientAsync(null, Sv(s, [s.GroupChatId], "incentive.evening", "incentive.midday"), true, s.AdminId, default);
        var rm = await service.SaveRecipientAsync(null, new ReportRecipientInput(TelegramReportRoles.Rm, null, null, null, null, null, [s.PrivateChatId],
            ["incentive.evening"], true), true, s.AdminId, default);

        // СВ в 18:00 — только копия новой карточки ТП 61 (подписан на акцию), прежней сводки команды нет (раньше — «3 ТП: накоплено …» первой картинкой).
        var group = Assert.Single(await service.SendNowAsync(sv.Id, new ReportSendInput("incentive.evening", true), true, s.AdminId, default));
        Assert.Equal("sent", group.Status);
        var sent = fixture.Telegram.Sent.Last(m => m.Peer.PeerId == GroupPeer).Message;
        Assert.StartsWith("9. Накопительная акция", sent.Text);
        Assert.DoesNotContain("ТП: накоплено", sent.Text);
        Assert.Equal(group.Pages, sent.Files.Count); // только картинки карточки ТП 61, без картинки сводки

        // СВ в 13:00 (копии — только 08:30 и 18:00) и РМ со «сводкой регионов» — ничего прежнего: пропуск с причиной, в разбивке — «не требуется».
        var midday = Assert.Single(await service.SendNowAsync(sv.Id, new ReportSendInput("incentive.midday", true), true, s.AdminId, default));
        var manager = Assert.Single(await service.SendNowAsync(rm.Id, new ReportSendInput("incentive.evening", true), true, s.AdminId, default));
        foreach (var skipped in new[] { midday, manager })
        {
            Assert.Equal("skipped", skipped.Status);
            Assert.Contains("прежняя сводка команды не отправляется", skipped.Error);
            Assert.Equal(ReportSkipReasons.NotNeeded, ReportSkipReasons.Key(skipped.Error));
        }

        Assert.DoesNotContain(fixture.Telegram.Sent, m => m.Peer.PeerId == PrivatePeer && m.Message.Text?.StartsWith("9. Накопительная акция") == true);
    }

    [Fact]
    public void Skip_reasons_written_in_uzbek_are_counted_by_their_russian_original()
    {
        const string none = "Накопительной акции в этот день нет: акция выключена или день вне её периода.";
        const string nothing = "Этой роли в этот запуск ничего не положено: с акциями ноября прежняя сводка команды не отправляется — у СВ итог команды в 18:30, у РМ итог недели зоны (сб 19:00) и сводка (пн 09:00).";
        foreach (var language in new[] { "ru", "uz", "uz-Cyrl" })
        {
            Assert.Equal(ReportSkipReasons.Promo, ReportSkipReasons.Key(ReportTexts.Get(language, none)));
            Assert.Equal(ReportSkipReasons.NotNeeded, ReportSkipReasons.Key(ReportTexts.Get(language, nothing)));
        }

        Assert.NotEqual(none, ReportTexts.Get("uz-Cyrl", none)); // перевод есть — иначе проверка выше ничего не проверяет
        Assert.Equal(ReportSkipReasons.NoChat, ReportSkipReasons.Key("Пользователь заблокировал аккаунт."));
    }
}

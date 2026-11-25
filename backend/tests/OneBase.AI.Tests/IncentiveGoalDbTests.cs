using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Incentive;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// «Цели на маршрут» v2 на PostgreSQL (ONEBASE_TEST_POSTGRES): миграция дописала раздел goals; цели маршрута по истории Linko (средний закуп Могул
/// до отсечки спящих и фасовка, действующий / новый Twizos, стойка в квоте региона и вне её, регион без Могула, точка без акции); итоги ✓ / ½ / ✗ по
/// заказам и визитам; вне маршрута — только заказы по акции; журнал кешбека — Могул и Twizos раздельно, сегмент на момент заказа, возврат, заказ без
/// доставки, повторная синхронизация без второго начисления, сегмент не меняется от заказа, догруженного задним числом.
/// </summary>
public class IncentiveGoalDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Today = new(2026, 11, 25); // среда
    private static readonly DateTimeOffset Morning = new(2026, 11, 25, 3, 30, 0, TimeSpan.Zero); // 08:30 Ташкент

    private const long T35 = 9_766_901, T140 = 9_766_902, T200 = 9_766_903, Cake = 9_766_904, Ym420 = 9_766_905, Ym1kg = 9_766_906, Stand = 9_766_907;
    private const long CakeType = 9_766_950, YmType = 9_766_951, EquipType = 9_766_952;
    private const long A = 9_766_001, B = 9_766_002, C = 9_766_003;

    [PostgresFact]
    public async Task Migration_adds_route_goals_v2_to_the_twizos_program()
    {
        using var scope = fixture.Services.CreateScope();
        var s = await scope.ServiceProvider.GetRequiredService<IncentiveService>().ProgramAsync("twizos-2026q4", default);
        Assert.NotNull(s);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(IncentivePromos.Goals2026, IncentiveSettings.Json),
            System.Text.Json.JsonSerializer.Serialize(s.Offers.Goals, IncentiveSettings.Json));
        var november = IncentivePromos.November2026;
        Assert.Equal((november with { Goals = IncentivePromos.Goals2026, Best = november.Best! with { End = new(2026, 11, 21) } }).ToJson(), s.Offers.ToJson());

        // Параметры v2 меняются без программиста: ошибки — понятной причиной, ничего не сохраняется; верные — сохраняются с журналом.
        var settings = scope.ServiceProvider.GetRequiredService<OneBase.Infrastructure.Telegram.Reports.IncentiveSettingsService>();
        var admin = Guid.NewGuid();
        var bad = await Assert.ThrowsAsync<OneBase.Infrastructure.Telegram.Reports.ReportValidationException>(() => settings.SavePromosAsync(s.Code,
            new IncentivePromos(Goals: s.Offers.Goals! with { SmallSet = "35x3+99x1", ThermosCheck = 10_000, NewPercent = 150 }), admin, default));
        Assert.Contains("наборы и лестница", bad.Message);
        Assert.Contains("пороги чека", bad.Message);
        Assert.Contains("0–100%", bad.Message);
        // Тестовая база — без справочника Linko прода: сохраняется только раздел целей v2 (товары стоек — из Linko, здесь их нет).
        var saved = await settings.SavePromosAsync(s.Code,
            new IncentivePromos(Goals: s.Offers.Goals! with { MogulGrowth = 0.25m, Ladder = ["140x1", "140x2 + 35x1"], StandProductIds = [] }), admin, default);
        var reread = await scope.ServiceProvider.GetRequiredService<IncentiveService>().ProgramAsync("twizos-2026q4", default);
        Assert.Equal((0.25m, "140x2+35x1"), (reread!.Offers.Goals!.MogulGrowth, reread.Offers.Goals.Ladder![1]));
        Assert.NotNull(saved);
    }

    [PostgresFact]
    public async Task Route_goals_marks_out_of_route_and_two_separate_cashback_ledgers()
    {
        fixture.Clock.Now = Morning;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var s = await SeedProgramAsync(db);
        var n = 9_766_100_000L;

        // Хоразм (филиал 5): три ТП с продажами месяца — команда региона; на складе 5 стоек (завод не в счёт) → квота ТП вверх(5 ÷ 3) = 2.
        await db.LinkoUsers.Where(u => u.Id == A || u.Id == B || u.Id == C).ExecuteDeleteAsync();
        db.LinkoUsers.AddRange(
            new LinkoUser { Id = A, FirstName = "Цель", SecondName = "Первый", IsActive = true, JobName = "Агент" },
            new LinkoUser { Id = B, FirstName = "Цель", SecondName = "Второй", IsActive = true, JobName = "Агент" },
            new LinkoUser { Id = C, FirstName = "Цель", SecondName = "Третий", IsActive = true, JobName = "Агент" });
        db.LinkoStocks.AddRange(new LinkoStock { Id = 9_766_801, Name = "Хоразм" }, new LinkoStock { Id = 9_766_802, Name = "Завод" });
        db.LinkoProductBalances.AddRange(new LinkoProductBalance { ProductId = Stand, StockId = 9_766_801, Balance = 5 },
            new LinkoProductBalance { ProductId = Stand, StockId = 9_766_802, Balance = 960 });

        // Точки ТП A: M1 — спящая (Могул) и новый Twizos; M2 — действующий Twizos; M3, M4, M5 — новые с крупным чеком (стойка двоим); M6 — без
        // акции; M9 — спящая в Самарканде (без Могула); M7 и M8 — не в маршруте дня.
        long m1 = 9_766_201, m2 = 9_766_202, m3 = 9_766_203, m4 = 9_766_204, m5 = 9_766_205, m6 = 9_766_206, m7 = 9_766_207, m8 = 9_766_208, m9 = 9_766_209;
        foreach (var (id, name, branch) in new[]
                 {
                     (m1, "Спящая Могул", "Хоразм"), (m2, "Действующий Twizos", "Хоразм"), (m3, "Крупный 2,5 млн", "Хоразм"), (m4, "Крупный 700 тыс.", "Хоразм"),
                     (m5, "Крупный 650 тыс.", "Хоразм"), (m6, "Маленький", "Хоразм"), (m7, "Вне маршрута Twizos", "Хоразм"), (m8, "Вне маршрута кекс", "Хоразм"),
                     (m9, "Спящая Самарканд", "Самарканд"),
                 })
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = id, Name = name, ResponsibleAgentId = A, BranchId = branch == "Хоразм" ? 5 : 9_766_999, BranchName = branch });
        }

        db.IncentiveSleepingMarkets.AddRange(new[] { m1, m9 }.Select(id => new IncentiveSleepingMarket { ProgramId = s.Id, MarketId = id, ListedOn = new(2026, 11, 6) }));
        foreach (var market in new[] { m1, m2, m3, m4, m5, m6, m9 })
        {
            db.LinkoVisits.Add(new LinkoVisit
            {
                Id = ++n, UserId = A, MarketId = market, Day = Today, Date = Today.ToDateTime(new TimeOnly(11, 0)), IsInPlan = true,
                Status = market == m4 ? "done" : "plan",
            });
        }

        LinkoOrder Order(long agent, long market, DateOnly day, int hour, string status, params (long Product, decimal Amount, decimal UnitKg, decimal Price)[] lines)
        {
            var o = new LinkoOrder
            {
                Id = ++n, AgentId = agent, MarketId = market, Status = status, CreatedDate = day, CreatedAt = day.ToDateTime(new TimeOnly(hour, 0)),
                AcceptedDate = status == "delivered" ? day : null, BranchId = market == m9 ? 9_766_999 : 5, BranchName = market == m9 ? "Самарканд" : "Хоразм",
                TotalPrice = lines.Sum(l => l.Amount * l.Price), TotalWeight = lines.Sum(l => l.Amount * l.UnitKg),
                Lines = lines.Select(l => new LinkoOrderLine
                {
                    Id = ++n, ProductId = l.Product, Amount = l.Amount, TotalWeight = l.Amount * l.UnitKg, OriginPrice = l.Price, Price = l.Price,
                    TotalPrice = l.Amount * l.Price,
                }).ToList(),
            };
            db.LinkoOrders.Add(o);
            return o;
        }

        // Продажи месяца команды (без Twizos), средний чек точек за 90 дней — по доставленным заказам.
        Order(B, m8, new(2026, 11, 2), 10, "delivered", (Cake, 1, 1, 1_000_000));
        Order(C, m8, new(2026, 11, 3), 10, "delivered", (Cake, 1, 1, 1_000_000));
        Order(A, m1, new(2026, 10, 20), 10, "delivered", (Cake, 1, 1, 200_000));
        Order(A, m3, new(2026, 10, 20), 10, "delivered", (Cake, 1, 1, 2_525_490));
        Order(A, m4, new(2026, 10, 20), 10, "delivered", (Cake, 1, 1, 700_920));
        Order(A, m5, new(2026, 10, 20), 10, "delivered", (Cake, 1, 1, 650_000));
        Order(A, m6, new(2026, 10, 20), 10, "delivered", (Cake, 1, 1, 50_000));
        Order(A, m9, new(2026, 10, 20), 10, "delivered", (Cake, 1, 1, 40_000));
        // Могул M1 до отсечки спящих: февраль — 16 пачек 420 г (6,72 кг), март — 8 пачек 1 кг; в среднем 7,36 кг, больше по кг — 1 кг.
        Order(A, m1, new(2026, 2, 10), 10, "delivered", (Ym420, 16, 0.42m, 17_700));
        Order(A, m1, new(2026, 3, 10), 10, "delivered", (Ym1kg, 8, 1, 40_000));
        Order(A, m9, new(2026, 3, 10), 10, "delivered", (Ym420, 16, 0.42m, 17_700));
        // Twizos M2 — с начала продаж одна накладная 162 800 (действующий).
        Order(A, m2, new(2026, 9, 10), 10, "delivered", (T35, 44, 0.035m, 3_700));

        // Сегодня до полудня: M1 — Могул 3 кор. 1 кг (YM-01) и Twizos 1 кор. 35 г (меньше набора), не доставлен; M2 — 1 кор. 140 г, доставлен,
        // потом возврат половины; M3 — стойка + 35 + 140 + 200 г; M4 — визит без заказа; M7 (вне маршрута) — 3 кор. 35 г; M8 — только кекс.
        var o1 = Order(A, m1, Today, 10, "not_delivered", (Ym1kg, 12, 1, 40_000), (T35, 28, 0.035m, 3_700));
        var o2 = Order(A, m2, Today, 10, "delivered", (T140, 26, 0.14m, 11_100));
        var o3 = Order(A, m3, Today, 10, "not_delivered", (T35, 28, 0.035m, 3_700), (T140, 26, 0.14m, 11_100), (T200, 20, 0.2m, 14_800), (Stand, 1, 0, 1));
        var o7 = Order(A, m7, Today, 11, "not_delivered", (T35, 84, 0.035m, 3_700));
        Order(A, m8, Today, 11, "not_delivered", (Cake, 1, 1, 90_000));
        // После полудня — второй заказ M3: точка уже брала Twizos утром — 5%.
        var o3b = Order(A, m3, Today, 15, "not_delivered", (T35, 28, 0.035m, 3_700));
        db.LinkoOrderReturns.Add(new LinkoOrderReturn
        {
            Id = ++n, Status = "delivered", CreatedDate = Today, AgentId = A, MarketId = m2,
            Lines = [new LinkoOrderReturnLine { Id = ++n, OrderId = o2.Id, ProductId = T140, Amount = 13, TotalWeight = 1.82m }],
        });
        await db.SaveChangesAsync();

        var goals = scope.ServiceProvider.GetRequiredService<IncentiveGoalService>();
        var quota = (await goals.StandQuotasAsync(s.Offers.Goals!, Today, default)).Single(q => q.Region == "Хоразм");
        Assert.Equal((5, 5, 3, 2, false), (quota.Stock, quota.Target, quota.Agents, quota.PerAgent, quota.Manual));

        // 08:30 — цели ставятся один раз на день, по коду ТТ.
        var routes = scope.ServiceProvider.GetRequiredService<IncentiveRouteService>();
        var morning = await routes.DayAsync(s, A, Today, new TimeOnly(8, 30), default);
        Assert.Equal([m1, m2, m3, m4, m5, m6, m9], morning.Stores.Select(x => x.MarketId));
        var g1 = morning.Stores[0].Goal!;
        Assert.Equal((7.36m, 2, "1 кг", 3, 480_000m, 446_400m, 44_640m),
            (g1.Mogul!.AvgKg, g1.Mogul.Months, g1.Mogul.Box, g1.Mogul.Boxes, g1.Mogul.Sum, g1.Mogul.AfterDiscount, g1.Mogul.Cashback));
        Assert.Equal((IncentiveTwizosSegment.New, "35x3", 24_864m), (g1.Twizos!.Segment, g1.Twizos.Set, g1.Twizos.Cashback)); // две акции — один визит
        var g2 = morning.Stores[1].Goal!.Twizos!;
        Assert.Equal((IncentiveTwizosSegment.Existing, 162_800m, "140x1", 288_600m, 14_430m), (g2.Segment, g2.AvgInvoice, g2.Set, g2.Sum, g2.Cashback));
        Assert.Equal((IncentiveTwizosSegment.Stand, 68_820m), (morning.Stores[2].Goal!.Twizos!.Segment, morning.Stores[2].Goal!.Twizos!.Cashback));
        Assert.Equal(IncentiveTwizosSegment.Stand, morning.Stores[3].Goal!.Twizos!.Segment);
        Assert.Equal((IncentiveTwizosSegment.New, "140x1+35x3", 599_400m), // квота 2 исчерпана — без стойки
            (morning.Stores[4].Goal!.Twizos!.Segment, morning.Stores[4].Goal!.Twizos!.Set, morning.Stores[4].Goal!.Twizos!.Sum));
        Assert.True(morning.Stores[5].Plain); // чек 50 000, Twizos не брала
        Assert.Null(morning.Stores[6].Goal!.Mogul); // Самарканд — YM-03, Могул не ставим
        Assert.Equal(7, await db.IncentiveRouteTargets.CountAsync(t => t.ProgramId == s.Id && t.AgentId == A && t.Date == Today && t.Goals != null));

        // 13:00 — отметки по заказам до полудня.
        var midday = await routes.DayAsync(s, A, Today, new TimeOnly(13, 0), default, persist: false);
        var f1 = midday.Stores[0].Fact!;
        Assert.Equal((IncentiveGoalStatus.Taken, 3, 44_640m, IncentiveGoalStatus.Partial), (f1.MogulStatus, f1.MogulBoxes, f1.MogulCashback, f1.TwizosStatus));
        Assert.Equal(IncentiveGoalStatus.Partial, midday.Stores[0].Status); // одна цель взята — оранжевый
        Assert.Equal((IncentiveGoalStatus.Taken, IncentiveGoalStatus.Taken), (midday.Stores[1].Fact!.TwizosStatus, midday.Stores[1].Status));
        Assert.Equal((IncentiveGoalStatus.Taken, true, IncentiveTwizosSegment.Stand, 68_820m),
            (midday.Stores[2].Fact!.TwizosStatus, midday.Stores[2].Fact!.Stand, midday.Stores[2].Fact!.TwizosSegment, midday.Stores[2].Fact!.TwizosCashback));
        Assert.Equal(IncentiveGoalStatus.Refused, midday.Stores[3].Status); // визит без заказа
        Assert.Equal(IncentiveGoalStatus.Ahead, midday.Stores[4].Status);
        var outside = Assert.Single(midday.OutOfRoute!); // кекс вне маршрута — не акция
        Assert.Equal((m7, 310_800m, IncentiveTwizosSegment.New, 24_864m), (outside.MarketId, outside.Sum, outside.Fact!.TwizosSegment, outside.Fact.TwizosCashback));

        // Журнал кешбека: Могул и Twizos отдельно, по строке на заказ.
        var ledger = scope.ServiceProvider.GetRequiredService<IncentiveCashbackLedger>();
        var first = await ledger.RefreshAsync(s, Today, default);
        Assert.Equal((6, 0, 1, 5), (first.Added, first.Updated, first.Mogul, first.Twizos));
        var rows = await db.IncentiveCashbackAccruals.AsNoTracking().Where(a => a.ProgramId == s.Id).ToListAsync();
        var mogul = rows.Single(r => r.Kind == IncentiveCashbackKind.Mogul);
        Assert.Equal((o1.Id, 480_000m, 446_400m, 44_640m, IncentiveCashbackStatus.Issued), (mogul.OrderId, mogul.ListSum, mogul.BaseSum, mogul.Amount, mogul.Status));
        var tw = rows.Where(r => r.Kind == IncentiveCashbackKind.Twizos).ToDictionary(r => r.OrderId);
        Assert.Equal((IncentiveTwizosSegment.New, 8_288m), (tw[o1.Id].Segment, tw[o1.Id].Amount));
        Assert.Equal((IncentiveTwizosSegment.Existing, 14_430m, 144_300m, 7_215m, IncentiveCashbackStatus.Confirmed, (DateOnly?)Today),
            (tw[o2.Id].Segment, tw[o2.Id].Issued, tw[o2.Id].NetSum, tw[o2.Id].Amount, tw[o2.Id].Status, tw[o2.Id].DeliveredOn)); // возврат половины
        Assert.Equal((IncentiveTwizosSegment.Stand, 10m, 68_820m, true), (tw[o3.Id].Segment, tw[o3.Id].Percent, tw[o3.Id].Amount, tw[o3.Id].Stand));
        Assert.Equal((IncentiveTwizosSegment.Existing, 5_180m), (tw[o3b.Id].Segment, tw[o3b.Id].Amount)); // после первой покупки — действующий
        Assert.Equal((IncentiveTwizosSegment.New, 24_864m, IncentiveCashbackStatus.Issued), (tw[o7.Id].Segment, tw[o7.Id].Amount, tw[o7.Id].Status));

        // Повторная синхронизация — ничего не добавляется и не меняется.
        var again = await ledger.RefreshAsync(s, Today, default);
        Assert.Equal((0, 0), (again.Added, again.Updated));
        Assert.Equal(6, await db.IncentiveCashbackAccruals.CountAsync(a => a.ProgramId == s.Id));

        // Задним числом догрузился заказ Twizos M7 от 01.11: сегмент заказа 25.11 остаётся «новый» (на момент заказа).
        Order(A, m7, new(2026, 11, 1), 10, "delivered", (T35, 28, 0.035m, 3_700));
        await db.SaveChangesAsync();
        await ledger.RefreshAsync(s, Today, default);
        var m7Row = await db.IncentiveCashbackAccruals.AsNoTracking().SingleAsync(a => a.ProgramId == s.Id && a.OrderId == o7.Id);
        Assert.Equal((IncentiveTwizosSegment.New, 8m, 24_864m), (m7Row.Segment, m7Row.Percent, m7Row.Amount));

        // Балансы — отдельно: Twizos оформлено 121 582 (из них доставлено 7 215 после возврата, ждёт доставки 107 152), Могул — 44 640.
        var twizos = (await ledger.BalancesAsync(s, IncentiveCashbackKind.Twizos, [A], Today, default))[A];
        Assert.Equal((121_582m, 107_152m, 7_215m, 0m), (twizos.Issued, twizos.Pending, twizos.Confirmed, twizos.ToPay));
        var mogulBalance = (await ledger.BalancesAsync(s, IncentiveCashbackKind.Mogul, [A], Today, default))[A];
        Assert.Equal((44_640m, 44_640m, 0m), (mogulBalance.Issued, mogulBalance.Pending, mogulBalance.Confirmed));

        // Неделя прошла — доставленное за 23–29.11 к выплате; выплата Twizos не трогает Могул.
        await scope.ServiceProvider.GetRequiredService<IncentiveResultsService>()
            .SetPayoutAsync(s, A, new DateOnly(2026, 11, 23), IncentivePayoutKind.Twizos, 5_000, new DateOnly(2026, 11, 30), "карта", null, default);
        twizos = (await ledger.BalancesAsync(s, IncentiveCashbackKind.Twizos, [A], new DateOnly(2026, 11, 30), default))[A];
        Assert.Equal((5_000m, 2_215m), (twizos.Paid, twizos.ToPay));
        mogulBalance = (await ledger.BalancesAsync(s, IncentiveCashbackKind.Mogul, [A], new DateOnly(2026, 11, 30), default))[A];
        Assert.Equal(0m, mogulBalance.Paid);

        // Семь карточек v2 через сборщик и рисовальщик: одна PNG 1080 px на карточку, отправка файлом.
        var builder = scope.ServiceProvider.GetRequiredService<IncentiveCardBuilder>();
        var me = new IncentiveAgent(A, "Цель Первый", "Хоразм");
        IncentiveAgent[] team = [me, new(B, "Цель Второй", "Хоразм"), new(C, "Цель Третий", "Хоразм")];
        var spec = new IncentiveTeamSpec(TelegramReportRoles.Sv, "СВР · Хоразм", [new IncentiveTeamRegionSpec("Хоразм", null, team)]);
        var renderer = new OneBase.Infrastructure.Telegram.Reports.SkiaReportRenderer();
        foreach (var (slot, date, specOf) in new (string, DateOnly, IncentiveTeamSpec?)[]
                 {
                     (IncentiveSlot.Morning, Today, null), (IncentiveSlot.Midday, Today, null), (IncentiveSlot.Evening, Today, null), (IncentiveSlot.Week, new(2026, 11, 28), null),
                     (IncentiveSlot.Month, new(2026, 11, 30), null), (IncentiveSlot.Team, Today, spec), (IncentiveSlot.Zone, new(2026, 11, 28), spec with { Role = TelegramReportRoles.Rm }),
                 })
        {
            var cards = await builder.BuildAsync(s, new IncentiveCardRequest(slot, date, "ru", team, specOf is null ? [me] : [], slot == IncentiveSlot.Midday ? new TimeOnly(13, 0) : null,
                specOf, Persist: false), default);
            Assert.True(cards.V2, slot);
            var (caption, summary) = IncentiveTexts.Caption(cards);
            Assert.Contains("Могул", caption);
            var document = new OneBase.Application.Telegram.Reports.ReportDocument("incentive." + slot, OneBase.Application.Telegram.Reports.ReportType.Incentive, "tp", "9", "Хоразм",
                null, date, $"{date:dd.MM.yyyy}", DateTimeOffset.UtcNow, null, "ru", [], [], [], [], caption, summary) { Incentive = cards, AsFiles = cards.V2 };
            var png = Assert.Single(renderer.Render(document));
            using var bitmap = SkiaSharp.SKBitmap.Decode(png);
            Assert.Equal(1080, bitmap.Width);
        }

        var day = (await builder.BuildAsync(s, new IncentiveCardRequest(IncentiveSlot.Evening, Today, "ru", team, [me], Persist: false), default)).Agents[0].Goals!.Day!;
        Assert.Equal((1, 1, 7, 1), (day.Totals.MogulGoals, day.Totals.Woken, day.Totals.Stores, day.Totals.Stands)); // стойка — в точке M3
        Assert.Equal(44_640m, day.Balances.Mogul.Issued); // оформлено за день — из журнала
        Assert.Equal(8_288m + 14_430m + 68_820m + 5_180m + 24_864m, day.Balances.Twizos.Issued);
        fixture.Clock.Now = null;
    }

    private static async Task<IncentiveSettings> SeedProgramAsync(OneBaseDbContext db)
    {
        LinkoProductType[] types =
        [
            new() { Id = 22, Name = "Снек" }, new() { Id = 26, Name = "Снек 140 г", ParentId = 22 }, new() { Id = 27, Name = "Снек 200 г", ParentId = 22 },
            new() { Id = 28, Name = "Снек 35 г", ParentId = 22 }, new() { Id = CakeType, Name = "Кекс цели" }, new() { Id = YmType, Name = "Помадка цели" },
            new() { Id = EquipType, Name = "Оборудование цели" },
        ];
        LinkoProduct[] products =
        [
            new() { Id = T35, Name = "Twizos 35 г", Code = "605", TypeId = 28 }, new() { Id = T140, Name = "Twizos 140 г", Code = "604", TypeId = 26 },
            new() { Id = T200, Name = "Twizos 200 г", Code = "603", TypeId = 27 }, new() { Id = Cake, Name = "Кекс", Code = "100", TypeId = CakeType },
            new() { Id = Ym420, Name = "Yamelly 420 г", Code = "480", TypeId = YmType }, new() { Id = Ym1kg, Name = "Yamelly 1 кг", Code = "467", TypeId = YmType },
            new() { Id = Stand, Name = "Стеллаж TWIZOS", TypeId = EquipType },
        ];
        var typeIds = types.Select(t => t.Id).ToArray();
        var haveTypes = await db.LinkoProductTypes.Where(t => typeIds.Contains(t.Id)).Select(t => t.Id).ToListAsync();
        db.LinkoProductTypes.AddRange(types.Where(t => !haveTypes.Contains(t.Id)));
        var productIds = products.Select(p => p.Id).ToArray();
        var haveProducts = await db.LinkoProducts.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        db.LinkoProducts.AddRange(products.Where(p => !haveProducts.Contains(p.Id)));
        await db.SaveChangesAsync();
        foreach (var id in new long[] { 26, 27, 28 })
        {
            await db.LinkoProductTypes.Where(t => t.Id == id).ExecuteUpdateAsync(u => u.SetProperty(t => t.ParentId, 22L));
        }

        var november = IncentivePromos.November2026;
        var promos = november with
        {
            Yamelly = november.Yamelly! with { ProductTypeIds = [YmType] },
            Goals = IncentivePromos.Goals2026 with { StandProductIds = [Stand] },
        };
        var s = IncentiveTests.Twizos();
        var program = new IncentiveProgram
        {
            Code = $"goals-{Guid.NewGuid():N}"[..30], Name = s.Name, Product = s.Product, ProductTypeIds = [22], StartDate = s.Start, EndDate = s.End, Enabled = false,
            TiersJson = System.Text.Json.JsonSerializer.Serialize(s.Tiers, IncentiveSettings.Json),
            MonthsJson = System.Text.Json.JsonSerializer.Serialize(s.Months, IncentiveSettings.Json),
            StoreSetsJson = System.Text.Json.JsonSerializer.Serialize(IncentivePromos.NovemberSets, IncentiveSettings.Json),
            PromosJson = promos.ToJson(),
        };
        db.IncentivePrograms.Add(program);
        await db.SaveChangesAsync();
        return IncentiveSettings.From(program);
    }
}

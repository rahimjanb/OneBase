using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Domain.Incentive;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// Акции ноября на PostgreSQL (ONEBASE_TEST_POSTGRES): миграция записала параметры в акцию Twizos; список спящих из Linko с историей; кэшбек YM-02
/// по статусам (свой список, порог, возврат, выплата); цели маршрута по шагам 1–4 и итоги точек по наборам, подаркам и Yamelly; продажа вне
/// маршрута; закрытие дня и TW-01 из итогов.
/// </summary>
public class IncentivePromoDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Today = new(2026, 11, 16); // понедельник
    private static readonly DateTimeOffset Morning = new(2026, 11, 16, 3, 30, 0, TimeSpan.Zero); // 08:30 Ташкент

    private const long P140 = 9_710_001, P35 = 9_710_002, Cake = 9_710_003;
    private const long YmType = 9_761_100, Ym420A = 9_761_001, Ym420B = 9_761_002, GiftType = 9_761_101, Mug = 9_761_201, Thermos = 9_761_202, Kettle = 9_761_203;

    [PostgresFact]
    public async Task Migration_writes_november_promos_into_the_twizos_program()
    {
        using var scope = fixture.Services.CreateScope();
        var s = await scope.ServiceProvider.GetRequiredService<IncentiveService>().ProgramAsync("twizos-2026q4", default);
        Assert.NotNull(s);
        Assert.False(s.Enabled);
        Assert.Equal(IncentivePromos.November2026.ToJson(), s.Offers.ToJson());
        Assert.Equal(["K35", "K140", "T", "C"], s.StoreSets.Select(x => x.Code!));
        Assert.Equal([310_800m, 288_600m, 392_200m, 599_400m], s.StoreSets.Select(x => x.Sum));
        Assert.Equal([1500m, 2400m, 4000m], s.Tiers.Select(t => t.Kg));
        Assert.Equal([1600m, 2500m, 4100m], s.TiersFor(true).Select(t => t.Kg));
        Assert.Equal("phone", s.Offers.Twizos!.TargetPrize);
    }

    [PostgresFact]
    public async Task Sleeping_list_comes_from_linko_keeps_woken_points_and_follows_late_data()
    {
        fixture.Clock.Now = Morning;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var s = await SeedProgramAsync(db, "sleep-linko");
        // 1 — брала Yamelly в августе (спящая); 2 — в августе и 12.10 (не спящая); 3 — не брала; 4 — заказ сентября отменён; 5 — «Завод»;
        // 6 — в июле (спящая, потом догрузится заказ октября); 7 — в июне (спящая, разбудят 10.11); 9 — новая точка Linko, брала в августе.
        long[] m = [9_762_001, 9_762_002, 9_762_003, 9_762_004, 9_762_005, 9_762_006, 9_762_007, 9_762_009];
        var mine = m.ToHashSet();
        foreach (var id in m.Where(id => id != 9_762_009))
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = id, Name = $"Точка {id}", ResponsibleAgentId = 9_762_900, BranchName = id == 9_762_005 ? "Завод" : "Андижон" });
        }

        var n = 9_762_500L;
        LinkoOrder Ym(long market, DateOnly day, string status = "delivered") => new()
        {
            Id = ++n, AgentId = 9_762_900, MarketId = market, Status = status, CreatedDate = day, CreatedAt = day.ToDateTime(new TimeOnly(10, 0)),
            AcceptedDate = status == "delivered" ? day : null, BranchName = market == 9_762_005 ? "Завод" : "Андижон",
            Lines = [new LinkoOrderLine { Id = ++n, ProductId = Ym420A, Amount = 8, TotalWeight = 3.36m, OriginPrice = 17_700, TotalPrice = 141_600 }],
        };
        db.LinkoOrders.AddRange(Ym(m[0], new(2026, 8, 15)), Ym(m[1], new(2026, 8, 15)), Ym(m[1], new(2026, 10, 12)), Ym(m[3], new(2026, 9, 1), "cancelled"),
            Ym(m[4], new(2026, 5, 1)), Ym(m[5], new(2026, 7, 1)), Ym(m[6], new(2026, 6, 1)));
        await db.SaveChangesAsync();
        var promos = scope.ServiceProvider.GetRequiredService<IncentivePromoService>();
        var results = scope.ServiceProvider.GetRequiredService<IncentiveResultsService>();
        async Task<long[]> Listed(DateOnly day) => (await promos.ListedAsync(s, day, default)).Where(mine.Contains).Order().ToArray();

        // Снимка нет — список считается по Linko на начало Yamelly (06.11), без записи.
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 1)), (s.Offers.Yamelly!.ListFrom, s.Offers.Yamelly.ListSince));
        Assert.Equal([m[0], m[5], m[6]], await Listed(new(2026, 11, 9)));
        Assert.Empty(await Listed(new(2026, 11, 5)));
        Assert.False(await db.IncentiveSleepingMarkets.AnyAsync(x => x.ProgramId == s.Id));

        // Первая карточка по расписанию записывает снимок на 06.11; повторно — не трогает.
        await results.EnsureSleepingAsync(s, new(2026, 11, 9), default);
        await results.EnsureSleepingAsync(s, new(2026, 11, 10), default);
        var saved = await db.IncentiveSleepingMarkets.AsNoTracking().Where(x => x.ProgramId == s.Id && mine.Contains(x.MarketId)).ToListAsync();
        Assert.Equal([m[0], m[5], m[6]], saved.Select(x => x.MarketId).Order());
        Assert.All(saved, x => Assert.Equal(new DateOnly(2026, 11, 6), x.ListedOn));

        // Неделя: 7 разбудили 10.11; у 6 догрузился заказ 20.10; в Linko появилась точка 9. Суббота 21.11 — список на понедельник 23.11.
        db.LinkoMarkets.Add(new LinkoMarket { Id = m[7], Name = "Новая", ResponsibleAgentId = 9_762_900, BranchName = "Андижон" });
        var late = Ym(m[5], new(2026, 10, 20));
        db.LinkoOrders.AddRange(Ym(m[6], new(2026, 11, 10)), late, Ym(m[7], new(2026, 8, 3)));
        await db.SaveChangesAsync();
        var week = await results.RefreshSleepingAsync(s, new(2026, 11, 21), new(2026, 11, 23), null, default);
        Assert.Equal((1, 1, 0), (week.Added, week.Removed, week.Restored));
        Assert.Equal([m[0], m[5], m[6]], await Listed(new(2026, 11, 16)));
        Assert.Equal([m[0], m[6], m[7]], await Listed(new(2026, 11, 23)));

        // Пересчёт без снимка (другая акция с тем же Yamelly): сначала снимок на 06.11 — разбуженная 10.11 точка 7 остаётся в списке с 06.11.
        var other = await SeedProgramAsync(db, "sleep-first");
        await results.RefreshSleepingAsync(other, new(2026, 11, 16), new(2026, 11, 16), null, default);
        var first = await db.IncentiveSleepingMarkets.AsNoTracking().SingleAsync(x => x.ProgramId == other.Id && x.MarketId == m[6]);
        Assert.Equal((new DateOnly(2026, 11, 6), (DateOnly?)null), (first.ListedOn, first.RemovedOn));

        // Заказ октября у 6 отменили — пересчёт вручную во вкладке «Акции» 23.11 возвращает её с этого дня.
        await db.LinkoOrders.Where(o => o.Id == late.Id).ExecuteUpdateAsync(u => u.SetProperty(o => o.Status, "cancelled"));
        fixture.Clock.Now = Morning.AddDays(7);
        var manual = await scope.ServiceProvider.GetRequiredService<IncentiveSleepingService>().RefreshAsync(s.Code, Guid.NewGuid(), default);
        Assert.Equal((0, 0, 1), (manual.Added, manual.Removed, manual.Restored));
        Assert.False(manual.Summary.Computed);
        Assert.Equal([m[0], m[5], m[6], m[7]], await Listed(new(2026, 11, 23)));
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Cashback_counts_only_own_sleeping_points_over_the_threshold_by_status()
    {
        fixture.Clock.Now = Morning;
        const long a = 9_763_001, b = 9_763_002;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var s = await SeedProgramAsync(db, "cashback");
        long[] m = [9_763_101, 9_763_102, 9_763_103, 9_763_104, 9_763_105, 9_763_106];
        for (var i = 0; i < m.Length; i++)
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = m[i], Name = $"Спящая {i + 1}", ResponsibleAgentId = i == 5 ? b : a, BranchName = "Андижон" });
        }

        // В списке: 1, 2, 4, 5 (ТП a) и 6 (ТП b); точка 3 — не спящая.
        db.IncentiveSleepingMarkets.AddRange(new[] { m[0], m[1], m[3], m[4], m[5] }.Select(id => new IncentiveSleepingMarket { ProgramId = s.Id, MarketId = id, ListedOn = new(2026, 11, 6) }));
        var tue = new DateOnly(2026, 11, 10);
        var n = 9_763_500L;
        LinkoOrder Ym(long agent, long market, DateOnly day, string status, params (long Product, decimal Packs)[] lines) => new()
        {
            Id = ++n, AgentId = agent, MarketId = market, Status = status, CreatedDate = day, CreatedAt = day.ToDateTime(new TimeOnly(10, 0)),
            AcceptedDate = status == "delivered" ? day.AddDays(1) : null, BranchName = "Андижон",
            Lines = lines.Select(l => new LinkoOrderLine { Id = ++n, ProductId = l.Product, Amount = l.Packs, TotalWeight = l.Packs * 0.42m, OriginPrice = 17_700, TotalPrice = l.Packs * 16_461 }).ToList(),
        };
        var o1 = Ym(a, m[0], tue, "delivered", (Ym420A, 8), (Ym420B, 8)); // 2 коробки 420 г — кэшбек 26 338, подтверждён
        var o2 = Ym(a, m[1], tue, "delivered", (Ym420A, 15)); // 15 пачек — порог не выполнен
        var o3 = Ym(a, m[2], tue, "delivered", (Ym420A, 16)); // не спящая
        var o4 = Ym(b, m[0], tue, "delivered", (Ym420A, 16)); // чужая точка (закреплена за a)
        var o5 = Ym(a, m[3], Today, "not_delivered", (Ym420A, 16)); // сегодня — оформлен, ждёт доставки
        var o6 = Ym(a, m[4], tue, "delivered", (Ym420A, 8), (Ym420B, 8)); // доставлен, вернули коробку — ниже порога, отменён
        db.LinkoOrders.AddRange(o1, o2, o3, o4, o5, o6);
        db.LinkoOrderReturns.Add(new LinkoOrderReturn
        {
            Id = ++n, Status = "delivered", CreatedDate = tue.AddDays(2), AgentId = a,
            Lines = [new LinkoOrderReturnLine { Id = ++n, OrderId = o6.Id, ProductId = Ym420A, Amount = 8, TotalWeight = 3.36m }],
        });
        await db.SaveChangesAsync();

        var promos = scope.ServiceProvider.GetRequiredService<IncentivePromoService>();
        var orders = await promos.YamellyOrdersAsync(s, [a, b], s.Offers.Yamelly!.Start, Today, null, default);
        Assert.Equal(new[] { o1.Id, o2.Id, o6.Id, o5.Id }.Order(), orders.Select(o => o.OrderId).Order());
        Assert.Equal((true, 26_338m, IncentiveCashbackStatus.Confirmed), (orders.Single(o => o.OrderId == o1.Id).Qualifies, orders.Single(o => o.OrderId == o1.Id).NetCashback,
            orders.Single(o => o.OrderId == o1.Id).CashbackStatus));
        Assert.Equal((false, IncentiveCashbackStatus.Cancelled), (orders.Single(o => o.OrderId == o2.Id).Qualifies, orders.Single(o => o.OrderId == o2.Id).CashbackStatus));
        Assert.Equal(IncentiveCashbackStatus.Issued, orders.Single(o => o.OrderId == o5.Id).CashbackStatus);
        Assert.Equal((IncentiveCashbackStatus.Cancelled, 141_600m), (orders.Single(o => o.OrderId == o6.Id).CashbackStatus, orders.Single(o => o.OrderId == o6.Id).NetListSum));

        var balance = (await promos.CashbackAsync(s, [a], Today, null, default))[a];
        Assert.Equal((4, 2), (balance.Listed, balance.Woken)); // разбужены 1 (o1) и 4 (o5); 5 — отменён, 2 — ниже порога
        Assert.Equal((52_676m, 26_338m, 26_338m, 26_338m), (balance.Issued, balance.Pending, balance.Confirmed, balance.Cancelled));
        Assert.Equal((26_338m, 2 * 26_338m), (balance.ToPay, balance.CanEarn)); // подтверждено на неделе 09.11 — к выплате
        var week = balance.Weeks.Single(w => w.Monday == new DateOnly(2026, 11, 9));
        Assert.Equal((1, 26_338m, 26_338m), (week.Woken, week.Issued, week.Confirmed));

        // Бухгалтерия отметила выплату 20 000 за неделю 09.11 — к выплате остаток.
        await scope.ServiceProvider.GetRequiredService<IncentiveResultsService>()
            .SetPayoutAsync(s, a, new DateOnly(2026, 11, 9), IncentivePayoutKind.Cashback, 20_000, new DateOnly(2026, 11, 17), "карта", null, default);
        balance = (await promos.CashbackAsync(s, [a], Today, null, default))[a];
        Assert.Equal((20_000m, 6_338m), (balance.Paid, balance.ToPay));

        var sleeping = (await promos.SleepingAsync(s, [a], Today, null, default))[a];
        Assert.Equal(4, sleeping.Count);
        Assert.Equal(tue, sleeping.Single(p => p.MarketId == m[0]).WokenOn);
        Assert.Equal(Today, sleeping.Single(p => p.MarketId == m[3]).WokenOn);
        Assert.Null(sleeping.Single(p => p.MarketId == m[4]).WokenOn);
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Route_goals_follow_steps_1_to_4_and_stores_are_judged_by_sets_gifts_and_yamelly()
    {
        fixture.Clock.Now = Morning;
        const long agent = 9_764_001, rival = 9_764_002;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var s = await SeedProgramAsync(db, "route-nov");
        // Маршрут: 1 — брала Twizos, чек 700 тыс.; 2 — 1,5 млн; 3 — 400 тыс. (спящая Yamelly); 4 — 100 тыс.; 5 — 50 тыс. Вне маршрута: O — 5 млн; X — 200 тыс.
        long[] r = [9_764_101, 9_764_102, 9_764_103, 9_764_104, 9_764_105];
        const long other = 9_764_201, outside = 9_764_301;
        decimal[] checks = [700_000, 1_500_000, 400_000, 100_000, 50_000];
        var n = 9_764_500L;
        var past = new DateOnly(2026, 10, 20);
        LinkoOrder History(long market, decimal sum, bool twizos) => new()
        {
            Id = ++n, AgentId = agent, MarketId = market, Status = "delivered", CreatedDate = past, CreatedAt = past.ToDateTime(new TimeOnly(10, 0)), AcceptedDate = past,
            TotalPrice = sum, BranchName = "Андижон",
            Lines = [new LinkoOrderLine { Id = ++n, ProductId = twizos ? P140 : Cake, Amount = 1, TotalWeight = twizos ? 0.14m : 1m, TotalPrice = sum }],
        };
        for (var i = 0; i < r.Length; i++)
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = r[i], Name = $"Маршрут {i + 1}", MarketTypeName = "Розница", BranchName = "Андижон", ResponsibleAgentId = agent, Lat = 41.10 - i * 0.01, Lon = 72.3 });
            db.LinkoVisits.Add(new LinkoVisit { Id = ++n, Date = Today.ToDateTime(TimeOnly.MinValue), Day = Today, Status = "planned", IsInPlan = true, MarketId = r[i], UserId = agent });
            db.LinkoOrders.Add(History(r[i], checks[i], i == 0));
        }

        db.LinkoMarkets.Add(new LinkoMarket { Id = other, Name = "Крупный вне маршрута", MarketTypeName = "Розница", BranchName = "Андижон", ResponsibleAgentId = agent });
        db.LinkoMarkets.Add(new LinkoMarket { Id = outside, Name = "Зашёл по пути", MarketTypeName = "Розница", BranchName = "Андижон", ResponsibleAgentId = rival });
        db.LinkoOrders.Add(History(other, 5_000_000, false));
        db.IncentiveSleepingMarkets.Add(new IncentiveSleepingMarket { ProgramId = s.Id, MarketId = r[2], ListedOn = new(2026, 11, 6) });
        await db.SaveChangesAsync();

        var routes = scope.ServiceProvider.GetRequiredService<IncentiveRouteService>();
        var morning = await routes.DayAsync(s, agent, Today, TimeOnly.MinValue, default, persist: true, norm: 30);
        Assert.Equal(r, morning.Stores.Select(x => x.MarketId));
        Assert.Equal(new string?[] { "C+K140", "C+T", "T+K140", "K35", null }, morning.Stores.Select(x => x.Sets is { Count: > 0 } sets ? string.Join("+", sets) : null));
        Assert.Equal([false, true, true, false, false], morning.Stores.Select(x => x.PlanAdded));
        Assert.Equal([false, false, true, false, false], morning.Stores.Select(x => x.Sleeping));
        Assert.True(morning.Stores[4].Plain);
        Assert.Equal(32.62m, morning.TargetKg);
        var extra = Assert.Single(morning.Extra!);
        Assert.Equal((other, "C+K140", 10.22m), (extra.MarketId, string.Join("+", extra.Sets!), extra.TargetKg));
        Assert.All(morning.Stores, x => Assert.Equal(BattleStoreStatus.Ahead, x.Status));

        // День: 1 — два заказа (кружка и чайник) — взяты оба; 2 — один заказ на кружку — ½; 3 — Yamelly 2 коробки (разбужена), Twizos нет — ½;
        // 4 — визит без заказа — отказ; 5 — обычный заказ; X — Twizos вне маршрута (засчитан). Соперник — 5 кг.
        LinkoOrder Sold(long market, long who, int hour, params (long Product, decimal Amount, decimal Kg, decimal Price)[] lines) => new()
        {
            Id = ++n, AgentId = who, MarketId = market, Status = "not_delivered", CreatedDate = IncentivePromoDbTests.Today,
            CreatedAt = IncentivePromoDbTests.Today.ToDateTime(new TimeOnly(hour, 0)), BranchName = "Андижон",
            Lines = lines.Select(l => new LinkoOrderLine { Id = ++n, ProductId = l.Product, Amount = l.Amount, TotalWeight = l.Kg, OriginPrice = l.Price, TotalPrice = l.Amount * l.Price }).ToList(),
        };
        db.LinkoOrders.AddRange(
            Sold(r[0], agent, 9, (P140, 26, 3.64m, 11_100)),
            Sold(r[0], agent, 10, (P140, 26, 3.64m, 11_100), (P35, 84, 2.94m, 3_700)),
            Sold(r[1], agent, 11, (P140, 26, 3.64m, 11_100)),
            Sold(r[2], agent, 11, (Ym420A, 16, 6.72m, 17_700)),
            Sold(r[4], agent, 12, (Cake, 1, 1m, 57_000)),
            Sold(outside, agent, 15, (P140, 26, 3.64m, 11_100)),
            Sold(r[0], rival, 10, (P35, 170, 5.95m, 3_700)));
        db.LinkoVisits.Add(new LinkoVisit { Id = ++n, Date = IncentivePromoDbTests.Today.ToDateTime(new TimeOnly(12, 30)), Day = IncentivePromoDbTests.Today, Status = "done", IsInPlan = true, MarketId = r[3], UserId = agent });
        await db.SaveChangesAsync();

        var midday = await routes.DayAsync(s, agent, IncentivePromoDbTests.Today, new TimeOnly(10, 30), default, norm: 30);
        Assert.Equal([BattleStoreStatus.Taken, BattleStoreStatus.Ahead, BattleStoreStatus.Ahead, BattleStoreStatus.Ahead, BattleStoreStatus.Ahead],
            midday.Stores.Select(x => x.Status));

        var evening = await routes.DayAsync(s, agent, IncentivePromoDbTests.Today, null, default, norm: 30);
        Assert.Equal([BattleStoreStatus.Taken, BattleStoreStatus.Partial, BattleStoreStatus.Partial, BattleStoreStatus.Refused, BattleStoreStatus.Taken],
            evening.Stores.Select(x => x.Status));
        Assert.Equal(["mug", "kettle"], evening.Stores[0].Gifts!);
        Assert.Equal((BattleStoreStatus.Refused, BattleStoreStatus.Taken, 26_338m), (evening.Stores[2].TwizosStatus, evening.Stores[2].YamellyStatus, evening.Stores[2].Cashback));
        Assert.True(evening.Stores[4].HasOrder);
        var sale = Assert.Single(evening.OutOfRoute!);
        Assert.Equal((outside, 3.64m, 288_600m, "mug"), (sale.MarketId, sale.Kg, sale.Sum, sale.Gifts.Single()));
        Assert.Equal((1, 1), (evening.SleepingPlanned, evening.SleepingWoken));
        Assert.Equal(3, evening.GiftsTaken["mug"]);
        Assert.Equal(1, evening.GiftsTaken["kettle"]);

        // Закрытие дня: итоги и TW-01 (ТП — 17,5 кг, соперник — 5,95 кг: меньше порога 6).
        var results = scope.ServiceProvider.GetRequiredService<IncentiveResultsService>();
        Assert.Equal(2, await results.CloseDayAsync(s, [agent, rival], IncentivePromoDbTests.Today, default));
        var row = await db.IncentiveDayResults.AsNoTracking().SingleAsync(x => x.ProgramId == s.Id && x.AgentId == agent);
        Assert.Equal((17.5m, 3.64m, 4, 3, 0, 1), (row.TwizosKg, row.OutOfRouteKg, row.Sets, row.Mugs, row.Thermoses, row.Kettles));
        Assert.Equal((1, 1, 26_338m), (row.SleepingPlanned, row.SleepingWoken, row.Cashback));
        Assert.Equal((1, true, 500_000m), (row.BestPlace!.Value, row.BestWinner, row.BestPrize));
        var lost = await db.IncentiveDayResults.AsNoTracking().SingleAsync(x => x.ProgramId == s.Id && x.AgentId == rival);
        Assert.Equal((5.95m, (int?)null, false), (lost.TwizosKg, lost.BestPlace, lost.BestWinner));

        // Закрытый день не пересчитывается по заказам: заказ соперника, внесённый задним числом, не меняет победу.
        db.LinkoOrders.Add(Sold(r[1], rival, 16, (P35, 900, 31.5m, 3_700)));
        await db.SaveChangesAsync();
        var promos = scope.ServiceProvider.GetRequiredService<IncentivePromoService>();
        var days = await promos.BestDaysAsync(s, [agent, rival], IncentivePromoDbTests.Today, IncentivePromoDbTests.Today, null, default);
        Assert.True(days[IncentivePromoDbTests.Today].Single(x => x.AgentId == agent).Winner);
        Assert.Equal(1, await results.CloseDayAsync(s, [rival], IncentivePromoDbTests.Today, default)); // повторное закрытие — перезапись
        fixture.Clock.Now = null;
    }

    /// <summary>Товары (Twizos 140 и 35 г, кекс, Yamelly 420 г, подарки) и акция теста с акциями ноября: Yamelly — тип теста, подарки — товары теста.</summary>
    private static async Task<IncentiveSettings> SeedProgramAsync(OneBaseDbContext db, string code)
    {
        LinkoProductType[] types =
        [
            new() { Id = 22, Name = "Снек" }, new() { Id = 26, Name = "Снек 140 г", ParentId = 22 }, new() { Id = 9_710_010, Name = "Кекс теста" },
            new() { Id = YmType, Name = "Помадка теста" }, new() { Id = GiftType, Name = "Бонус теста" },
        ];
        LinkoProduct[] products =
        [
            new() { Id = P140, Name = "Twizos 140 г", Code = "604", TypeId = 26 }, new() { Id = P35, Name = "Twizos 35 г", Code = "605", TypeId = 22 },
            new() { Id = Cake, Name = "Кекс", Code = "100", TypeId = 9_710_010 },
            new() { Id = Ym420A, Name = "Yamelly Арбуз 420 г", Code = "480", TypeId = YmType }, new() { Id = Ym420B, Name = "Yamelly Слива 420 г", Code = "481", TypeId = YmType },
            new() { Id = Mug, Name = "Бокал", TypeId = GiftType }, new() { Id = Thermos, Name = "Термос", TypeId = GiftType }, new() { Id = Kettle, Name = "Электрочайник", TypeId = GiftType },
        ];
        var typeIds = types.Select(t => t.Id).ToArray();
        var productIds = products.Select(p => p.Id).ToArray();
        var haveTypes = await db.LinkoProductTypes.Where(t => typeIds.Contains(t.Id)).Select(t => t.Id).ToListAsync();
        var haveProducts = await db.LinkoProducts.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        db.LinkoProductTypes.AddRange(types.Where(t => !haveTypes.Contains(t.Id)));
        db.LinkoProducts.AddRange(products.Where(p => !haveProducts.Contains(p.Id)));
        await db.SaveChangesAsync();
        await db.LinkoProductTypes.Where(t => t.Id == 26).ExecuteUpdateAsync(u => u.SetProperty(t => t.ParentId, 22L));

        var november = IncentivePromos.November2026;
        var promos = november with
        {
            Twizos = november.Twizos! with
            {
                Gifts = november.Twizos.GiftTiers.Select(g => g with { ProductIds = [g.Gift switch { "mug" => Mug, "thermos" => Thermos, _ => Kettle }] }).ToList(),
            },
            Yamelly = november.Yamelly! with { ProductTypeIds = [YmType] },
        };
        var s = IncentiveTests.Twizos();
        var program = new IncentiveProgram
        {
            Code = $"{code}-{Guid.NewGuid():N}"[..30], Name = s.Name, Product = s.Product, ProductTypeIds = [22], StartDate = s.Start, EndDate = s.End, Enabled = false,
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

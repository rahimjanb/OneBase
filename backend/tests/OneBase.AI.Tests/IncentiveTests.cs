using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using OneBase.Domain.Incentive;
using OneBase.Domain.Sales;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Накопительная акция Twizos без базы (ТЗ «Проект 3», раздел 6): ступени и один приз, рабочие дни с праздником и норма в день, ориентиры
/// месяцев, набор магазина по среднему закупу и по уровню «Поля боя», статус магазина, доля полудня, рейтинг розницы и опта.
/// </summary>
public class IncentiveTests
{
    private static readonly IReadOnlyDictionary<DateOnly, string> Holidays = new Dictionary<DateOnly, string> { [new(2026, 12, 8)] = "День Конституции" };

    internal static IncentiveSettings Twizos(params long[] wholesale) => new(
        Guid.NewGuid(), "twizos-test", "Накопительная акция Twizos", "Twizos", [22], new(2026, 10, 1), new(2026, 12, 31),
        [
            new(1500, "Планшет Samsung", "планшет", "tablet", "Samsung planshet", "planshet", "планшета"),
            new(2400, "Smart TV 55″", "телевизор", "tv", "Smart TV 55″", "televizor", "телевизора"),
            new(4000, "iPhone 17", "iPhone", "phone", "iPhone 17", "iPhone", "iPhone"),
        ],
        [new(2026, 10, 0.20m), new(2026, 11, 0.35m), new(2026, 12, 0.45m)],
        [
            new(5_000_000, "140 г + 200 г", 7.64m, "чайник Tefal", "600", "140 g + 200 g", "Tefal choynagi"),
            new(1_500_000, "140 г + 35 г", 4.62m, "термос", "400", "140 g + 35 g", "termos"),
            new(500_000, "140 г × 1 кор.", 3.64m, "кружка", "300", "140 g × 1 quti", "krujka"),
            new(0, "35 г × 1 кор.", 0.98m, ContentsUz: "35 g × 1 quti"),
        ],
        wholesale.ToHashSet(), true, true, true, IncentiveRmMode.Summary, IncentiveStaleData.Stamp, true);

    [Fact]
    public void One_prize_by_the_highest_tier_and_what_is_left_to_each()
    {
        var s = Twizos();
        Assert.Null(IncentiveRules.Reached(s.Tiers, 1499.9m));
        Assert.Equal("планшет", IncentiveRules.Next(s.Tiers, 1499.9m)!.Short);
        Assert.Equal("планшет", IncentiveRules.Reached(s.Tiers, 1500)!.Short);
        Assert.Equal("телевизор", IncentiveRules.Next(s.Tiers, 1500)!.Short);
        Assert.Equal("iPhone", IncentiveRules.Reached(s.Tiers, 4100)!.Short);
        Assert.Null(IncentiveRules.Next(s.Tiers, 4100));

        // Образец ТЗ: накоплено 515 кг — до планшета 985, до TV 1 885, до iPhone 3 485.
        var p = IncentiveRules.Progress(s, 515, new(2026, 11, 16), Holidays);
        Assert.Equal([985m, 1885m, 3485m], p.Tiers.Select(t => t.Left));
        Assert.Equal(985m, p.ToNext);
        Assert.Null(p.Reached);
    }

    [Fact]
    public void Norm_is_left_to_the_next_tier_over_working_days_to_the_end_without_holidays()
    {
        var s = Twizos();
        // 16.11–31.12: 40 дней пн–сб, 8 декабря — праздник → 39 (как на образце «39 раб. дней до 31.12»).
        var morning = IncentiveRules.Progress(s, 515, new(2026, 11, 16), Holidays);
        Assert.Equal(39, morning.WorkDaysLeft);
        Assert.Equal(26m, morning.Norm); // 985 ÷ 39 = 25,3 → вверх до целых
        // Вечером — со следующего дня: 38 дней.
        var evening = IncentiveRules.Progress(s, 553, new(2026, 11, 17), Holidays);
        Assert.Equal(38, evening.WorkDaysLeft);
        Assert.Equal(25m, evening.Norm); // 947 ÷ 38 = 24,9
        // Взят планшет — норма до телевизора; взяты все — нормы нет; акция кончилась — дней нет.
        Assert.Equal("телевизор", IncentiveRules.Progress(s, 1600, new(2026, 12, 1), Holidays).Next!.Short);
        Assert.Null(IncentiveRules.Progress(s, 4000, new(2026, 12, 1), Holidays).Norm);
        var after = IncentiveRules.Progress(s, 100, new(2027, 1, 1), Holidays);
        Assert.Equal((0, (decimal?)null), (after.WorkDaysLeft, after.Norm));
        // Воскресенье не рабочий день.
        Assert.Equal(1, IncentiveRules.WorkDays(new(2026, 11, 14), new(2026, 11, 15), Holidays));
    }

    [Fact]
    public void Month_guides_are_tier_shares()
    {
        var s = Twizos();
        var tablet = s.Tiers[0];
        Assert.Equal([300m, 525m, 675m], new[] { 10, 11, 12 }.Select(m => IncentiveRules.MonthGuide(s, tablet, 2026, m)!.Value));
        Assert.Null(IncentiveRules.MonthGuide(s, tablet, 2027, 1));
    }

    [Fact]
    public void Store_set_by_average_purchase_and_by_battle_level()
    {
        var s = Twizos();
        decimal Kg(decimal avg) => IncentiveRules.StoreSet(s.StoreSets, avg)!.Kg;
        Assert.Equal([7.64m, 7.64m, 4.62m, 4.62m, 3.64m, 3.64m, 0.98m, 0.98m],
            new[] { 6_000_000m, 5_000_000m, 4_999_999m, 1_500_000m, 1_200_000m, 500_000m, 499_999m, 0m }.Select(Kg));
        Assert.Equal("140 г + 35 г", IncentiveRules.SetForLevel(s.StoreSets, "400")!.Contents);
        Assert.Null(IncentiveRules.SetForLevel(s.StoreSets, "999"));
        Assert.Null(IncentiveRules.SetForLevel(s.StoreSets, null));
    }

    [Fact]
    public void Best_clients_week_target_is_sixty_percent_of_the_weekly_purchase_to_five_kg()
    {
        // ТЗ, раздел 6: средний недельный закуп за 60 дней × 0,6, до 5 кг, не меньше 5.
        Assert.Equal([5m, 5m, 20m, 35m], new[] { 0m, 40m, 300m, 520m }.Select(kg => IncentiveRules.WeekTarget(kg)));
    }

    [Fact]
    public void Store_status_and_midday_share()
    {
        Assert.Equal(BattleStoreStatus.Taken, IncentiveRules.StoreStatus(3.64m, 3.64m, true));
        Assert.Equal(BattleStoreStatus.Taken, IncentiveRules.StoreStatus(0.98m, 0.98m, false));
        Assert.Equal(BattleStoreStatus.Partial, IncentiveRules.StoreStatus(1.8m, 3.64m, true));
        Assert.Equal(BattleStoreStatus.Refused, IncentiveRules.StoreStatus(0, 3.64m, true));
        Assert.Equal(BattleStoreStatus.Ahead, IncentiveRules.StoreStatus(0, 3.64m, false));
        Assert.Equal(0.474m, Math.Round(IncentiveRules.MiddayShare(new(8, 30), new(13, 0), new(18, 0)), 3));
    }

    [Fact]
    public void Runs_follow_battle_in_the_catalog_and_month_end_runs_on_the_last_working_day()
    {
        Assert.Equal(["incentive.morning", "incentive.midday", "incentive.evening", "incentive.week", "incentive.rating", "incentive.month"],
            ReportCatalog.Jobs.Where(j => j.Report == ReportType.Incentive).Select(j => j.Code));
        Assert.True(ReportCatalog.Order("battle.evening") < ReportCatalog.Order("incentive.evening")); // в 18:00 — сначала «Поле боя»
        Assert.Equal([TelegramReportRoles.Rm, TelegramReportRoles.Sv], ReportCatalog.Find("incentive.rating")!.Roles); // ТП чужих цифр не получает
        Assert.Equal(["incentive.morning", "incentive.evening"], ReportCatalog.ForRole(["incentive.morning", "incentive.rating", "incentive.evening"], TelegramReportRoles.Tp));
        Assert.Equal(("9. Накопительная акция", "итог месяца"), (ReportCatalog.Title(ReportType.Incentive), ReportCatalog.SlotTitle(ReportType.Incentive, "month")));
        Assert.True(ReportCatalog.Find("incentive.midday")!.IsMidday);

        // Последний рабочий день месяца (пн–сб): 30.11 — понедельник, 31.12 — четверг, 31.05 — воскресенье → суббота 30.05.
        Assert.True(ReportSchedules.IsMonthEnd(new(2026, 11, 30)));
        Assert.False(ReportSchedules.IsMonthEnd(new(2026, 11, 28)));
        Assert.True(ReportSchedules.IsMonthEnd(new(2026, 12, 31)));
        Assert.True(ReportSchedules.IsMonthEnd(new(2026, 5, 30)));
        Assert.False(ReportSchedules.IsMonthEnd(new(2026, 5, 31)));
        var job = new TelegramReportJob
        {
            Code = "incentive.month", Enabled = true, Time = new(18, 0), TimeZone = ReportSchedules.DefaultTimeZone, Days = ReportSchedules.Monday, CatchUpMinutes = 360,
            ArmedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
        };
        // Дни недели у итога месяца не важны: следующий — 30.11 18:00, 28.11 (суббота) — нет, 30.11 — да.
        Assert.Equal(new DateTimeOffset(2026, 11, 30, 18, 0, 0, TimeSpan.FromHours(5)), ReportSchedules.Next(job, new DateTimeOffset(2026, 11, 5, 5, 0, 0, TimeSpan.Zero)));
        Assert.Null(ReportSchedules.Due(job, new DateTimeOffset(2026, 11, 28, 13, 5, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 11, 30), ReportSchedules.Due(job, new DateTimeOffset(2026, 11, 30, 13, 5, 0, TimeSpan.Zero))!.Period);
        // Обычный запуск — по дням недели, как раньше.
        Assert.NotNull(ReportSchedules.Due(new TelegramReportJob { Code = "incentive.evening", Enabled = true, Time = new(18, 0), TimeZone = ReportSchedules.DefaultTimeZone,
            Days = ReportSchedules.WorkDays, CatchUpMinutes = 180, ArmedAt = job.ArmedAt }, new DateTimeOffset(2026, 11, 28, 13, 5, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Stale_data_morning_needs_yesterday_evening_and_later_runs_need_today()
    {
        var morning = ReportCatalog.Find("incentive.morning")!;
        var evening = ReportCatalog.Find("incentive.evening")!;
        var day = new DateOnly(2026, 11, 16);
        DateTimeOffset At(int dayShift, int hour) => new(day.AddDays(dayShift).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.FromHours(5));
        Assert.False(IncentiveReport.IsStale(morning, day, At(-1, 19)));
        Assert.True(IncentiveReport.IsStale(morning, day, At(-1, 11)));
        Assert.True(IncentiveReport.IsStale(evening, day, At(-1, 19)));
        Assert.False(IncentiveReport.IsStale(evening, day, At(0, 17)));
        Assert.True(IncentiveReport.IsStale(evening, day, null));
    }

    [Fact]
    public void Retail_and_wholesale_are_ranked_separately()
    {
        var s = Twizos(3);
        IncentiveStanding A(long id, string name, decimal kg) => new(new(id, name, "Самарканд"), s.IsWholesale(id), kg, 0, 0, new Dictionary<DateOnly, decimal>());
        var standings = new IncentiveStandings(s, IncentiveMoment.Morning(new(2026, 11, 16)), [A(1, "Жахонгир", 515), A(2, "Жавохир", 442), A(3, "Опт", 900), A(4, "Аброр", 442)], null);
        Assert.Equal(["Жахонгир", "Аброр", "Жавохир"], standings.Rating(false).Select(a => a.Agent.Name));
        Assert.Equal((1, 3, 1), (standings.Rank(1)!.Value, standings.Rank(2)!.Value, standings.Rank(3)!.Value));
        Assert.Null(standings.Rank(99));
        // Общий зачёт — опт в одном рейтинге с розницей.
        var together = s with { SeparateWholesale = false };
        Assert.False(together.IsWholesale(3));
    }
}

/// <summary>
/// Накопительная акция на PostgreSQL: акция Twizos из миграции (по ТЗ, выключена); накоплено — доставлено по дате приёмки с начала акции по
/// вчера (подтипы Linko, без «Завода» и других товаров) − доставленные возвраты по этим заказам + заказы сегодня (13:00 — до полудня, отменённые
/// не считаются), по месяцам; рейтинг розницы и опта; цели магазинов маршрута — набор по закупу и по «Полю боя», «подключить / дозаказ»,
/// без офисов, один раз на день, статусы по заказам и визитам, прошедший день без постановки.
/// </summary>
public class IncentiveDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Today = new(2026, 11, 16); // понедельник
    private static readonly DateTimeOffset Morning = new(2026, 11, 16, 3, 30, 0, TimeSpan.Zero); // 08:30 Ташкент

    private const long P140 = 9_710_001, P35 = 9_710_002, Cake = 9_710_003;

    [PostgresFact]
    public async Task Seeded_twizos_program_follows_the_spec_and_is_off()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IncentiveService>();
        var s = await service.ProgramAsync("twizos-2026q4", default);
        Assert.NotNull(s);
        Assert.False(s.Enabled);
        Assert.Equal(("Twizos", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)), (s.Product, s.Start, s.End));
        Assert.Equal(new long[] { 22 }, s.ProductTypeIds);
        Assert.Equal([1500m, 2400m, 4000m], s.Tiers.Select(t => t.Kg));
        Assert.Equal(["Планшет Samsung", "Smart TV 55″", "iPhone 17"], s.Tiers.Select(t => t.Prize));
        Assert.Equal(["планшета", "телевизора", "iPhone"], s.Tiers.Select(t => t.Of!));
        Assert.Equal(["planshet", "televizor", "iPhone"], s.Tiers.Select(t => t.ShortUz!));
        Assert.Equal([0.20m, 0.35m, 0.45m], s.Months.Select(m => m.Share));
        Assert.Equal([7.64m, 4.62m, 3.64m, 0.98m], s.StoreSets.Select(x => x.Kg));
        Assert.Equal(new string?[] { "600", "400", "300", null }, s.StoreSets.Select(x => x.Level));
        Assert.Equal(16, s.Wholesale.Count);
        Assert.True(s.IsWholesale(403) && s.IsWholesale(511) && !s.IsWholesale(468));
        Assert.Equal((IncentiveRmMode.Summary, IncentiveStaleData.Stamp), (s.RmMode, s.StaleData));
    }

    [PostgresFact]
    public async Task Accumulated_is_delivered_minus_returns_by_yesterday_plus_orders_today()
    {
        fixture.Clock.Now = Morning;
        const long a = 9_700_001, b = 9_700_002, w = 9_700_003;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var program = await SeedAsync(db, "acc-test", [w]);
        var n = 9_730_000L;
        LinkoOrder Order(long agent, string status, DateOnly created, TimeOnly at, DateOnly? accepted, string branch, params (long Product, decimal Kg)[] lines) => new()
        {
            Id = ++n, AgentId = agent, MarketId = 9_799_999, Status = status, CreatedDate = created, CreatedAt = created.ToDateTime(at),
            AcceptedDate = accepted, AcceptedAt = accepted?.ToDateTime(new TimeOnly(12, 0)), BranchName = branch, TotalWeight = lines.Sum(l => l.Kg),
            Lines = lines.Select(l => new LinkoOrderLine { Id = ++n, ProductId = l.Product, Amount = 1, TotalWeight = l.Kg }).ToList(),
        };

        var o1 = Order(a, "delivered", new(2026, 10, 4), new(10, 0), new(2026, 10, 5), "Самарканд", (P140, 3.64m), (Cake, 10m)); // + 3,64 (кекс — не акция)
        var o2 = Order(a, "delivered", new(2026, 9, 30), new(10, 0), new(2026, 10, 1), "Самарканд", (P35, 0.98m)); // принят 1 октября — в зачёте
        var o3 = Order(a, "delivered", new(2026, 9, 29), new(10, 0), new(2026, 9, 30), "Самарканд", (P140, 3.64m)); // до акции
        var o4 = Order(a, "delivered", new(2026, 11, 9), new(10, 0), new(2026, 11, 10), "Завод", (P140, 100m)); // «Завод» — не вторичка
        var o5 = Order(a, "delivered", new(2026, 11, 14), new(10, 0), new(2026, 11, 15), "Самарканд", (P140, 7.28m)); // вчера
        var o6 = Order(a, "delivered", new(2026, 11, 14), new(11, 0), Today, "Самарканд", (P140, 3.64m)); // принят сегодня — ещё не «по вчера»
        var o7 = Order(a, "not_delivered", Today, new(10, 0), null, "Самарканд", (P140, 3.64m)); // сегодня до полудня
        var o8 = Order(a, "not_delivered", Today, new(14, 0), null, "Самарканд", (P35, 0.98m)); // сегодня после полудня
        var o9 = Order(a, "cancelled", Today, new(9, 0), null, "Самарканд", (P140, 3.64m)); // отменён
        db.LinkoOrders.AddRange(o1, o2, o3, o4, o5, o6, o7, o8, o9,
            Order(b, "delivered", new(2026, 11, 1), new(10, 0), new(2026, 11, 2), "Самарканд", (P140, 20m)),
            Order(w, "delivered", new(2026, 11, 2), new(10, 0), new(2026, 11, 3), "Самарканд", (P140, 50m)));
        LinkoOrderReturn Return(string status, DateOnly created, LinkoOrder order, decimal kg) => new()
        {
            Id = ++n, Status = status, CreatedDate = created, AgentId = a, TotalWeight = kg,
            Lines = [new LinkoOrderReturnLine { Id = ++n, OrderId = order.Id, ProductId = P140, Amount = 1, TotalWeight = kg }],
        };

        db.LinkoOrderReturns.AddRange(
            Return("delivered", new(2026, 11, 15), o5, 1.0m), // вычитается — из ноября
            Return("not_delivered", new(2026, 11, 15), o5, 2.0m), // не проведён
            Return("delivered", Today, o1, 0.5m)); // сегодня — ещё не «по вчера»
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IncentiveService>();
        IncentiveAgent[] agents = [new(a, "Жахонгир", "Самарканд"), new(b, "Жавохир", "Самарканд"), new(w, "Опт", "Самарканд")];

        var morning = await service.StandingsAsync(program, agents, IncentiveMoment.Morning(Today), default);
        var me = morning.Of(a)!;
        Assert.Equal((11.90m, 1.0m, 0m, 10.90m), (me.Delivered, me.Returned, me.Today, me.Total));
        Assert.Equal((4.62m, 6.28m), (me.Month(2026, 10), me.Month(2026, 11)));
        Assert.Equal(me.Total, me.Days.Values.Sum());

        var midday = await service.StandingsAsync(program, agents, IncentiveMoment.Day(Today, new TimeOnly(13, 0)), default);
        Assert.Equal((3.64m, 14.54m, 9.92m), (midday.Of(a)!.Today, midday.Of(a)!.Total, midday.Of(a)!.Month(2026, 11)));

        var evening = await service.StandingsAsync(program, agents, IncentiveMoment.Day(Today), default);
        Assert.Equal((4.62m, 15.52m), (evening.Of(a)!.Today, evening.Of(a)!.Total));

        // Рейтинг: розница — Жавохир (20) и Жахонгир; опт (50) — свой рейтинг.
        Assert.Equal(["Жавохир", "Жахонгир"], evening.Rating(false).Select(x => x.Agent.Name));
        Assert.Equal((2, 1, 1), (evening.Rank(a)!.Value, evening.Rank(b)!.Value, evening.Rank(w)!.Value));
        Assert.True(evening.Of(w)!.Wholesale);

        // Кг по дням недели — по взятым заказам (день ввода): в понедельник 3,64 + 0,98, в 13:00 — только 3,64.
        var week = await service.TakenAsync(program, [a], Today, Today.AddDays(5), null, default);
        Assert.Equal(4.62m, week[(a, Today)]);
        var noon = await service.TakenAsync(program, [a], Today, Today, new TimeOnly(13, 0), default);
        Assert.Equal(3.64m, noon[(a, Today)]);

        // После конца акции: доставлено по 31.12 (с заказом, принятым 16.11) − все проведённые возвраты; сегодняшних заказов нет.
        var late = await service.StandingsAsync(program, agents, IncentiveMoment.Day(new DateOnly(2027, 1, 10)), default);
        Assert.Equal((15.54m, 1.5m, 0m, 14.04m), (late.Of(a)!.Delivered, late.Of(a)!.Returned, late.Of(a)!.Today, late.Of(a)!.Total));
    }

    [PostgresFact]
    public async Task Route_targets_are_set_once_by_purchase_and_battle_and_judged_by_orders_and_visits()
    {
        fixture.Clock.Now = Morning;
        const long agent = 9_700_010;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var program = await SeedAsync(db, "route-test", []);
        var m = Enumerable.Range(1, 5).Select(i => 9_720_000L + i).ToArray();
        // 1 — закуп 6 млн/мес, брала Twizos; 2 — 2 млн, без Twizos; 3 — 1,2 млн, но «Поле боя» поставило набор 600; 4 — 0,2 млн; 5 — офис.
        decimal[] avg = [6_000_000, 2_000_000, 1_200_000, 200_000, 3_000_000];
        var n = 9_740_000L;
        for (var i = 0; i < 5; i++)
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = m[i], Name = $"Магазин {i + 1}", MarketTypeName = i == 4 ? "Office" : "Розница", ResponsibleAgentId = agent, Lat = 41.0 - i * 0.01, Lon = 69.0 });
            var past = new DateOnly(2026, 10, 10);
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = ++n, AgentId = agent, MarketId = m[i], Status = "delivered", CreatedDate = past, CreatedAt = past.ToDateTime(new TimeOnly(10, 0)), AcceptedDate = past,
                TotalPrice = avg[i] * 4, Lines = i == 0 ? [new LinkoOrderLine { Id = ++n, ProductId = P140, Amount = 1, TotalWeight = 3.64m }] : [],
            });
            db.LinkoVisits.Add(new LinkoVisit { Id = ++n, UserId = agent, MarketId = m[i], Status = "pending", IsInPlan = true, Day = Today, Date = Today.ToDateTime(TimeOnly.MinValue) });
        }

        db.BattleGoals.Add(new BattleGoal
        {
            Date = Today, Week = Today, AgentId = agent, MarketId = m[2], MarketName = "Магазин 3", Order = 1, RouteSource = BattleRouteSource.Linko,
            Kind = BattleGoalKind.Twizos, Sub = BattleGoalSub.Connect, Level = "600", TargetSum = 584_600,
        });
        await db.SaveChangesAsync();

        var routes = scope.ServiceProvider.GetRequiredService<IncentiveRouteService>();
        var morning = await routes.DayAsync(program, agent, Today, null, default);
        Assert.Equal(BattleRouteSource.Linko, morning.Source);
        Assert.Equal(5, morning.RoutePoints);
        Assert.Equal([m[0], m[1], m[2], m[3]], morning.Stores.Select(s => s.MarketId)); // офис — без цели, порядок — маршрут с севера
        Assert.Equal([1, 2, 3, 4], morning.Stores.Select(s => s.Order));
        Assert.Equal([7.64m, 4.62m, 7.64m, 0.98m], morning.Stores.Select(s => s.TargetKg));
        Assert.Equal([IncentiveTargetSub.Reorder, IncentiveTargetSub.Connect, IncentiveTargetSub.Connect, IncentiveTargetSub.Connect], morning.Stores.Select(s => s.Sub));
        Assert.Equal(new string?[] { null, null, "600", null }, morning.Stores.Select(s => s.BattleLevel));
        Assert.Equal(20.88m, morning.TargetKg);
        Assert.All(morning.Stores, s => Assert.Equal(BattleStoreStatus.Ahead, s.Status));

        // День: 1 — 8,26 кг (взят), 2 — 0,98 (частично), 3 — визит без заказа (отказ), 4 — 0,98 в 15:00 (в полдень — впереди, вечером — взят).
        db.LinkoOrders.AddRange(
            TodayOrder(++n, agent, m[0], new(10, 0), (P140, 3.64m), (P140, 3.64m), (P35, 0.98m)),
            TodayOrder(n += 10, agent, m[1], new(11, 0), (P35, 0.98m)),
            TodayOrder(n += 10, agent, m[3], new(15, 0), (P35, 0.98m)));
        db.LinkoVisits.Add(new LinkoVisit { Id = n += 10, UserId = agent, MarketId = m[2], Status = "done", IsInPlan = false, Day = Today, Date = Today.ToDateTime(new TimeOnly(12, 0)) });
        // Позже утра у магазина 2 появился Twizos в истории — цель дня не переставляется.
        db.LinkoOrders.Add(new LinkoOrder
        {
            Id = n += 10, AgentId = agent, MarketId = m[1], Status = "delivered", CreatedDate = new(2026, 11, 13), CreatedAt = new DateTime(2026, 11, 13, 10, 0, 0),
            AcceptedDate = new(2026, 11, 14), TotalPrice = 100_000_000, Lines = [new LinkoOrderLine { Id = n += 10, ProductId = P140, Amount = 1, TotalWeight = 3.64m }],
        });
        await db.SaveChangesAsync();

        var midday = await routes.DayAsync(program, agent, Today, new TimeOnly(13, 0), default);
        Assert.Equal([BattleStoreStatus.Taken, BattleStoreStatus.Partial, BattleStoreStatus.Refused, BattleStoreStatus.Ahead], midday.Stores.Select(s => s.Status));
        Assert.Equal([8.26m, 0.98m, 0m, 0m], midday.Stores.Select(s => s.Kg));
        Assert.Equal(morning.Stores.Select(s => (s.MarketId, s.TargetKg, s.Sub)), midday.Stores.Select(s => (s.MarketId, s.TargetKg, s.Sub))); // цели те же, что утром

        var evening = await routes.DayAsync(program, agent, Today, null, default);
        Assert.Equal(BattleStoreStatus.Taken, evening.Stores[3].Status);
        Assert.Equal((2, 1, 1, 1, 4), (evening.Taken, evening.Partial, evening.Refused, evening.Connected, evening.Passed));
        Assert.Equal(4, await db.IncentiveRouteTargets.CountAsync(t => t.ProgramId == program.Id && t.AgentId == agent));

        // Прошедший день без карточки — цели задним числом не ставятся; неделя — только поставленные дни.
        var saturday = await routes.DayAsync(program, agent, Today.AddDays(-2), null, default);
        Assert.Empty(saturday.Stores);
        var week = await routes.DaysAsync(program, agent, Today, Today.AddDays(5), null, default);
        Assert.Equal([4, 0, 0, 0, 0, 0], week.Select(d => d.Stores.Count));
        Assert.Equal(0, await db.IncentiveRouteTargets.CountAsync(t => t.ProgramId == program.Id && t.Date != Today));
    }

    [PostgresFact]
    public async Task Store_limit_keeps_battle_goals_first_then_bigger_stores_in_route_order()
    {
        fixture.Clock.Now = Morning;
        const long agent = 9_700_030;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var program = await SeedAsync(db, "limit-test", [], maxStores: 2);
        var m = Enumerable.Range(1, 4).Select(i => 9_720_200L + i).ToArray();
        decimal[] avg = [300_000, 6_000_000, 200_000, 2_000_000];
        var n = 9_760_000L;
        for (var i = 0; i < 4; i++)
        {
            db.LinkoMarkets.Add(new LinkoMarket { Id = m[i], Name = $"Точка {i + 1}", MarketTypeName = "Розница", ResponsibleAgentId = agent, Lat = 41.0 - i * 0.01, Lon = 69.0 });
            var past = new DateOnly(2026, 10, 10);
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = ++n, AgentId = agent, MarketId = m[i], Status = "delivered", CreatedDate = past, CreatedAt = past.ToDateTime(new TimeOnly(10, 0)), AcceptedDate = past,
                TotalPrice = avg[i] * 4,
            });
            db.LinkoVisits.Add(new LinkoVisit { Id = ++n, UserId = agent, MarketId = m[i], Status = "pending", IsInPlan = true, Day = Today, Date = Today.ToDateTime(TimeOnly.MinValue) });
        }

        // У точки 3 (мелкая) — цель «Поля боя»: она идёт первой, вторая — самая крупная (точка 2); на карточке — в порядке маршрута.
        db.BattleGoals.Add(new BattleGoal
        {
            Date = Today, Week = Today, AgentId = agent, MarketId = m[2], MarketName = "Точка 3", Order = 1, RouteSource = BattleRouteSource.Linko,
            Kind = BattleGoalKind.Twizos, Sub = BattleGoalSub.Connect, Level = "300", TargetSum = 288_600,
        });
        await db.SaveChangesAsync();

        var route = await scope.ServiceProvider.GetRequiredService<IncentiveRouteService>().DayAsync(program, agent, Today, null, default);
        Assert.Equal([m[1], m[2]], route.Stores.Select(x => x.MarketId));
        Assert.Equal([1, 2], route.Stores.Select(x => x.Order));
        Assert.Equal(4, route.RoutePoints); // в плане Linko 4 точки, с целью — 2
    }

    [PostgresFact]
    public async Task Cards_are_built_for_every_slot_from_real_calculations()
    {
        fixture.Clock.Now = Morning;
        const long a = 9_700_020, b = 9_700_021, w = 9_700_022, market = 9_720_100;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var program = await SeedAsync(db, "builder-test", [w]);
        var n = 9_750_000L;
        LinkoOrder Delivered(long agent, long? marketId, DateOnly accepted, decimal kg, decimal price = 0) => new()
        {
            Id = ++n, AgentId = agent, MarketId = marketId, Status = "delivered", CreatedDate = accepted.AddDays(-1), CreatedAt = accepted.AddDays(-1).ToDateTime(new TimeOnly(10, 0)),
            AcceptedDate = accepted, BranchName = "Самарканд", TotalPrice = price, TotalWeight = kg,
            Lines = [new LinkoOrderLine { Id = ++n, ProductId = P140, Amount = 1, TotalWeight = kg }],
        };

        db.LinkoMarkets.Add(new LinkoMarket { Id = market, Name = "Магазин на маршруте", MarketTypeName = "Розница", ResponsibleAgentId = a, Lat = 39.65, Lon = 66.96 });
        db.LinkoOrders.AddRange(
            Delivered(a, market, new DateOnly(2026, 10, 10), 100, 2_000_000), // октябрь; средний закуп ТТ 0,5 млн/мес, брала Twizos — дозаказ 140 г
            Delivered(a, null, new DateOnly(2026, 11, 10), 50),
            Delivered(b, null, new DateOnly(2026, 11, 5), 200),
            Delivered(w, null, new DateOnly(2026, 11, 5), 300),
            TodayOrder(++n, a, market, new TimeOnly(10, 0), (P140, 3.64m)));
        db.LinkoVisits.Add(new LinkoVisit { Id = ++n, UserId = a, MarketId = market, Status = "pending", IsInPlan = true, Day = Today, Date = Today.ToDateTime(TimeOnly.MinValue) });
        await db.SaveChangesAsync();

        var builder = scope.ServiceProvider.GetRequiredService<IncentiveCardBuilder>();
        IncentiveAgent[] all = [new(a, "Жахонгир", "Самарканд"), new(b, "Жавохир", "Самарканд"), new(w, "Опт", "Самарканд")];
        var me = all[0];

        var morning = (await builder.BuildAsync(program, new IncentiveCardRequest(IncentiveSlot.Morning, Today, "ru", all, [me]), default)).Agents.Single();
        Assert.Equal((150m, 0m, 2, 2), (morning.Total, morning.Today, morning.Rank!.Value, morning.Ranked));
        Assert.Equal((1350m, 39, 35m), (morning.Progress.ToNext!.Value, morning.Progress.WorkDaysLeft, morning.Progress.Norm!.Value)); // 8 декабря — праздник справочника
        Assert.Equal((morning.Progress.Total, morning.Progress.Norm, morning.Progress.WorkDaysLeft), (morning.DayStart.Total, morning.DayStart.Norm, morning.DayStart.WorkDaysLeft));
        var store = Assert.Single(morning.Route!.Stores);
        Assert.Equal((3.64m, IncentiveTargetSub.Reorder, BattleStoreStatus.Ahead), (store.TargetKg, store.Sub, store.Status));

        var evening = (await builder.BuildAsync(program, new IncentiveCardRequest(IncentiveSlot.Evening, Today, "ru", all, [me]), default)).Agents.Single();
        Assert.Equal((3.64m, 153.64m), (evening.Today, evening.Total));
        Assert.Equal((38, 36m, 35m), (evening.Progress.WorkDaysLeft, evening.Progress.Norm!.Value, evening.DayStart.Norm!.Value)); // норма дня — утренняя
        Assert.Equal(BattleStoreStatus.Taken, evening.Route!.Stores.Single().Status);

        var week = (await builder.BuildAsync(program, new IncentiveCardRequest(IncentiveSlot.Week, Today.AddDays(5), "ru", all, [me]), default)).Agents.Single();
        Assert.Equal(6, week.Week!.Days.Count);
        Assert.Equal((3.64m, 35m, 210m), (week.Week.Kg, week.Week.DayNorm!.Value, week.Week.Norm!.Value));
        Assert.Equal((2, 2), (week.Rank!.Value, week.RankBefore!.Value));
        Assert.Equal([1, 0, 0, 0, 0, 0], week.Week.Days.Select(d => d.Route.Stores.Count));
        // Лучшие клиенты за 60 дней: ТТ маршрута, 100 кг 10.10 → цель на неделю 5 кг; заказ без ТТ — не клиент.
        var best = Assert.Single(week.Week.TopMarkets!);
        Assert.Equal((market, 100m, 5m), (best.MarketId, best.Kg, best.WeekTarget));

        var month = (await builder.BuildAsync(program, new IncentiveCardRequest(IncentiveSlot.Month, new DateOnly(2026, 11, 30), "ru", all, [me]), default)).Agents.Single().Month!;
        Assert.Equal((50m, 100m, 525m, false), (month.Kg, month.PreviousKg, month.Guide!.Value, month.Final));
        Assert.Equal(5, month.Weeks.Count); // 1 ноября — воскресенье: без рабочих дней; 2–8, 9–15, 16–22, 23–29, 30
        Assert.Equal(50m, month.Weeks[1].Kg);

        var rating = (await builder.BuildAsync(program, new IncentiveCardRequest(IncentiveSlot.Rating, Today, "ru", all, []), default)).Rating!;
        Assert.Equal(["Жавохир", "Жахонгир"], rating.Retail.Select(r => r.Agent.Name));
        Assert.Equal(["Опт"], rating.Wholesale.Select(r => r.Agent.Name));

        var team = (await builder.BuildAsync(program, new IncentiveCardRequest(IncentiveSlot.Evening, Today, "ru", all, [],
            Team: new IncentiveTeamSpec("sv", "Самарканд", [new("Самарканд", "СВ", all)])), default)).Team!;
        Assert.Equal(["Опт", "Жавохир", "Жахонгир"], team.Agents.Select(r => r.Agent.Name));
        Assert.Equal((3.64m, 3.64m, true), (team.Today, team.RouteKg!.Value, team.Agents[0].Wholesale));
    }

    private static LinkoOrder TodayOrder(long id, long agent, long market, TimeOnly at, params (long Product, decimal Kg)[] lines) => new()
    {
        Id = id, AgentId = agent, MarketId = market, Status = "not_delivered", CreatedDate = Today, CreatedAt = Today.ToDateTime(at), TotalWeight = lines.Sum(l => l.Kg),
        Lines = lines.Select((l, i) => new LinkoOrderLine { Id = id * 10 + i, ProductId = l.Product, Amount = 1, TotalWeight = l.Kg }).ToList(),
    };

    /// <summary>Товары (Twizos 140 г — подтип «Снек», 35 г — сам «Снек», кекс — другой тип) и своя акция теста.</summary>
    private static async Task<IncentiveSettings> SeedAsync(OneBaseDbContext db, string code, long[] wholesale, int maxStores = 12)
    {
        // Типы и товары могли завести другие тесты этой базы — недостающие добавляются, подтип 140 г всегда под «Снеком».
        LinkoProductType[] types = [new() { Id = 22, Name = "Снек" }, new() { Id = 26, Name = "Снек 140 г", ParentId = 22 }, new() { Id = 9_710_010, Name = "Кекс теста" }];
        LinkoProduct[] products =
        [
            new() { Id = P140, Name = "Twizos 140 г", Code = "604", TypeId = 26 }, new() { Id = P35, Name = "Twizos 35 г", Code = "605", TypeId = 22 },
            new() { Id = Cake, Name = "Кекс", Code = "100", TypeId = 9_710_010 },
        ];
        var typeIds = types.Select(t => t.Id).ToArray();
        var productIds = products.Select(p => p.Id).ToArray();
        var haveTypes = await db.LinkoProductTypes.Where(t => typeIds.Contains(t.Id)).Select(t => t.Id).ToListAsync();
        var haveProducts = await db.LinkoProducts.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        db.LinkoProductTypes.AddRange(types.Where(t => !haveTypes.Contains(t.Id)));
        db.LinkoProducts.AddRange(products.Where(p => !haveProducts.Contains(p.Id)));
        await db.SaveChangesAsync();
        await db.LinkoProductTypes.Where(t => t.Id == 26).ExecuteUpdateAsync(u => u.SetProperty(t => t.ParentId, 22L));

        var s = IncentiveTests.Twizos(wholesale);
        var program = new IncentiveProgram
        {
            Code = code, Name = s.Name, Product = s.Product, ProductTypeIds = [22], StartDate = s.Start, EndDate = s.End, Enabled = true,
            TiersJson = System.Text.Json.JsonSerializer.Serialize(s.Tiers, IncentiveSettings.Json),
            MonthsJson = System.Text.Json.JsonSerializer.Serialize(s.Months, IncentiveSettings.Json),
            StoreSetsJson = System.Text.Json.JsonSerializer.Serialize(s.StoreSets, IncentiveSettings.Json),
            WholesaleAgentIds = wholesale, MaxStores = maxStores,
        };
        db.IncentivePrograms.Add(program);
        await db.SaveChangesAsync();
        return IncentiveSettings.From(program);
    }
}

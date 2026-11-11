using OneBase.AI.Analytik;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Readiness;
using OneBase.Application.Security;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Готовность данных Аналитика без базы: статус набора и причина простыми словами, что система уже сделала, пауза между повторами запуска,
/// утренняя догрузка перед ежедневным отчётом, баннер периода без данных (с последним загруженным месяцем), описание для AI-аналитика, права.
/// </summary>
public class AnalytikReadinessTests
{
    // 12.11.2026 10:00 по Ташкенту; последняя загрузка Linko — 30.10.2026 02:53 (как на рабочей базе без привязки Linko).
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(5));
    private static readonly DateTimeOffset LastLinko = new(2026, 10, 30, 2, 53, 0, TimeSpan.FromHours(5));
    private static readonly DateOnly Today = new(2026, 11, 12);

    private static SourceLink Linko(bool connected = true, bool auto = true, bool running = false) =>
        new(SourceCodes.Linko, "Linko", connected, auto, running, TimeSpan.FromMinutes(90), "/settings/integrations/sales/linko",
            connected ? null : "Linko не подключён: в «Настройки → Интеграции → Linko» не указан адрес или токен.",
            $"{Permissions.SalesManage}|{Permissions.IntegrationsManage}");

    private static SourceLink Verifix() =>
        new(SourceCodes.Verifix, "Verifix", true, true, false, TimeSpan.FromHours(6), "/settings/integrations/hr/verifix", null, Permissions.IntegrationsManage);

    private static DatasetReadiness Sales(SourceLink link, DateTimeOffset? at, string? error = null, string? action = null) =>
        DataReadiness.Evaluate("sales", "Продажи: заказы, возвраты, визиты", "продажи", "sales", Permissions.SalesRead, link, at, error, action, Now);

    private static DatasetReadiness Set(string code, string shortName, string module, string permission, SourceLink link, DateTimeOffset? at, string? action = null) =>
        DataReadiness.Evaluate(code, code, shortName, module, permission, link, at, null, action, Now);

    private static readonly DatasetReadiness OneBaseSet = new("onebase", "Производство, склад сырья, снабжение", "производство и склад", SourceCodes.OneBase,
        "production", null, ReadinessStatus.Ok, Now, false, null, null, null);

    [Fact]
    public void Status_says_why_the_data_lags()
    {
        var fresh = Sales(Linko(), Now.AddMinutes(-20));
        Assert.Equal(ReadinessStatus.Ok, fresh.Status);
        Assert.False(fresh.Lagging);

        var notConnected = Sales(Linko(connected: false), LastLinko);
        Assert.Equal(ReadinessStatus.NotConnected, notConnected.Status);
        Assert.True(notConnected.Lagging);
        Assert.Contains("не указан адрес или токен", notConnected.Reason);
        Assert.Equal(ReadinessStatus.NotConnected, Sales(Linko(connected: false), null).Status);

        Assert.Equal(ReadinessStatus.Updating, Sales(Linko(running: true), LastLinko).Status);
        var error = Sales(Linko(), Now.AddHours(-3), "Linko ответил 401: токен недействителен");
        Assert.Equal(ReadinessStatus.Error, error.Status);
        Assert.Contains("401", error.Reason);
        var off = Sales(Linko(auto: false), Now.AddHours(-3));
        Assert.Equal(ReadinessStatus.Stale, off.Status);
        Assert.Contains("кнопкой «Обновить»", off.Reason);
        Assert.Contains("по расписанию не прошло", Sales(Linko(), Now.AddHours(-3)).Reason);

        // «Что сделано» — только у отстающих данных.
        Assert.Null(Sales(Linko(), Now.AddMinutes(-5), action: "Аналитик запустил обновление 12.11.2026 09:55.").Action);
        Assert.NotNull(Sales(Linko(), Now.AddHours(-3), action: "Аналитик запустил обновление 12.11.2026 09:55.").Action);
    }

    [Fact]
    public void Action_text_lists_what_the_system_already_did()
    {
        var started = new KeeperAttempt(new DateTimeOffset(2026, 11, 12, 9, 5, 0, TimeSpan.FromHours(5)), "schedule", SyncStartResult.Started, 1);
        Assert.Equal("Аналитик запустил обновление 12.11.2026 09:05.", DataReadiness.ActionText(Linko(), started, null));
        Assert.Equal("Администраторы интеграций уведомлены 31.10.2026 09:00; после подключения данные догрузятся автоматически.",
            DataReadiness.ActionText(Linko(connected: false), null, new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.FromHours(5))));
        Assert.Equal("После включения обмена данные загрузятся автоматически.", DataReadiness.ActionText(Linko(auto: false), null, null));
        Assert.Null(DataReadiness.ActionText(Linko(), null, null));
    }

    [Fact]
    public void Retries_back_off_and_the_morning_window_precedes_the_daily_report()
    {
        Assert.Equal(TimeSpan.Zero, DataKeeper.Backoff(0));
        Assert.Equal(TimeSpan.FromMinutes(10), DataKeeper.Backoff(1));
        Assert.Equal(TimeSpan.FromMinutes(20), DataKeeper.Backoff(2));
        Assert.Equal(TimeSpan.FromMinutes(40), DataKeeper.Backoff(3));
        Assert.Equal(TimeSpan.FromMinutes(80), DataKeeper.Backoff(4));
        Assert.Equal(TimeSpan.FromMinutes(120), DataKeeper.Backoff(5));
        Assert.Equal(TimeSpan.FromMinutes(120), DataKeeper.Backoff(50));

        static DateTimeOffset At(int h, int m) => new(2026, 11, 12, h, m, 0, TimeSpan.FromHours(5));
        // Отчёт в 07:30: догрузка вчерашнего дня — с 06:45, ожидание данных — до 09:30.
        Assert.False(DataKeeper.InMorning(At(6, 44), "07:30"));
        Assert.True(DataKeeper.InMorning(At(6, 45), "07:30"));
        Assert.True(DataKeeper.InMorning(At(9, 30), "07:30"));
        Assert.False(DataKeeper.InMorning(At(9, 31), "07:30"));
        Assert.False(DataKeeper.InMorning(At(14, 0), "07:30"));
        // Сервер в UTC: 01:45 UTC — это 06:45 в Ташкенте.
        Assert.True(DataKeeper.InMorning(new DateTimeOffset(2026, 11, 12, 1, 45, 0, TimeSpan.Zero), "07:30"));
        // Через полночь: отчёт в 00:20 — окно с 23:35 до 02:20.
        Assert.True(DataKeeper.InMorning(At(23, 40), "00:20"));
        Assert.True(DataKeeper.InMorning(At(1, 0), "00:20"));
        Assert.False(DataKeeper.InMorning(At(12, 0), "00:20"));
        Assert.Equal(new TimeOnly(7, 30), DataKeeper.DailyAt("не время"));
    }

    [Fact]
    public void Period_after_the_last_load_gets_a_gap_with_the_last_loaded_month()
    {
        var link = Linko(connected: false);
        const string action = "Администраторы интеграций уведомлены 31.10.2026 09:00; после подключения данные догрузятся автоматически.";
        var sets = new[]
        {
            Sales(link, LastLinko, action: action),
            Set("payments", "оплаты", "finance", Permissions.FinanceRead, link, LastLinko.AddMinutes(-13), action),
            Set("stock", "остатки", "sales", Permissions.SalesRead, link, LastLinko.AddMinutes(-14), action),
            OneBaseSet,
        };

        var november = AnalytikPeriod.Of("month", null, null, Today);
        var gap = Assert.Single(DataReadiness.Gaps(sets, november, Now));
        Assert.Equal(SourceCodes.Linko, gap.Source);
        Assert.StartsWith("Продажи, оплаты и остатки за ", gap.Text);
        Assert.Contains("ещё не загружены: в OneBase данные по 30.10.2026 02:39.", gap.Text); // самая ранняя из трёх загрузок
        Assert.Contains("Linko не подключён", gap.Text);
        Assert.Equal(action, gap.Action);
        Assert.Null(gap.Href); // настройки подключения — только тем, кто может его исправить
        Assert.Equal("/settings/integrations/sales/linko", DataReadiness.Gaps(sets, november, Now, code => code == SourceCodes.Linko)[0].Href);
        Assert.Equal(new LatestPeriod("октябрь 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), gap.Latest);

        // Октябрь — данные по 30.10: период неполный, другой месяц не предлагается.
        var october = AnalytikPeriod.Of("custom", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Today);
        var partial = Assert.Single(DataReadiness.Gaps(sets, october, Now));
        Assert.Contains("неполные: данные по 30.10.2026 02:39.", partial.Text);
        Assert.Null(partial.Latest);

        // Сентябрь загружен целиком — нехватки нет; свежие данные текущего месяца — тоже.
        Assert.Empty(DataReadiness.Gaps(sets, AnalytikPeriod.Of("custom", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), Today), Now));
        Assert.Empty(DataReadiness.Gaps([Sales(Linko(), Now.AddMinutes(-10)), OneBaseSet], november, Now));

        // Планы месяца не заведены — отдельной строкой, только если период захватывает текущий месяц.
        var plans = new DatasetReadiness("plans", "Планы продаж на ноябрь", "планы", SourceCodes.Linko, "sales", Permissions.SalesRead, ReadinessStatus.Missing,
            null, true, "Планы на ноябрь ещё не заведены.", null, "/sales");
        Assert.Contains(DataReadiness.Gaps([plans], november, Now), g => g.Text == "Планы на ноябрь ещё не заведены." && g.Latest is null);
        Assert.Empty(DataReadiness.Gaps([plans], october, Now));
    }

    [Fact]
    public void Analyst_gets_what_lags_why_and_what_was_done_and_nothing_when_all_is_fresh()
    {
        const string action = "Администраторы интеграций уведомлены 31.10.2026 09:00; после подключения данные догрузятся автоматически.";
        var hr = DataReadiness.Evaluate("hr", "Посещаемость и опоздания", "посещаемость", "hr", Permissions.HrRead, Verifix(), Now.AddMinutes(-30), null, null, Now);
        var text = DataReadiness.Describe([Sales(Linko(connected: false), LastLinko, action: action), hr, OneBaseSet], Now);
        Assert.NotNull(text);
        Assert.Contains("Продажи: заказы, возвраты, визиты: данные по 30.10.2026 02:53. Linko не подключён", text);
        Assert.Contains($"Что сделано: {action}", text);
        Assert.Contains("Актуальны: посещаемость, производство и склад.", text);

        Assert.Null(DataReadiness.Describe([Sales(Linko(), Now.AddMinutes(-10)), hr, OneBaseSet], Now));
        // Обновление идёт — так и сказано.
        Assert.Contains("идёт обновление", DataReadiness.Describe([Sales(Linko(running: true), LastLinko)], Now));
    }

    [Fact]
    public void Readiness_is_shown_by_rights_and_refresh_only_to_those_who_can_fix_it()
    {
        var linko = Linko();
        var verifix = Verifix();
        var report = new ReadinessReport(Now, [linko, verifix],
        [
            Sales(linko, Now.AddHours(-3)),
            Set("payments", "оплаты", "finance", Permissions.FinanceRead, linko, Now.AddHours(-3)),
            DataReadiness.Evaluate("hr", "Посещаемость и опоздания", "посещаемость", "hr", Permissions.HrRead, verifix, Now.AddHours(-8), null, null, Now),
            OneBaseSet,
        ]);

        AnalytikPrincipal Org(params string[] permissions) => new(Guid.NewGuid(), new HashSet<string>([Permissions.AnalytikUse, .. permissions]), SalesRowScope.Organization);
        var regional = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse, Permissions.SalesRead, Permissions.FinanceRead, Permissions.SalesManage },
            new SalesRowScope(SalesRowScope.RegionLevel, [101], [Guid.NewGuid()], [], null, "регионы: Хоразм"));

        // Кадровик видит посещаемость и OneBase, но не обмен продаж; региональный — не видит наборов продаж и финансов компании.
        Assert.Equal(["hr", "onebase"], report.Visible(Org(Permissions.HrRead)).Select(d => d.Code));
        Assert.Equal(["onebase"], report.Visible(regional).Select(d => d.Code));
        Assert.Equal(["sales", "payments", "onebase"], report.Visible(Org(Permissions.SalesRead, Permissions.FinanceRead)).Select(d => d.Code));

        // Обновить Linko — управление продажами по всей компании или интеграции; Verifix — только интеграции.
        Assert.True(DataReadiness.CanManage(linko, Org(Permissions.SalesManage)));
        Assert.False(DataReadiness.CanManage(linko, regional));
        Assert.False(DataReadiness.CanManage(linko, Org(Permissions.SalesRead)));
        Assert.True(DataReadiness.CanManage(linko, Org(Permissions.IntegrationsManage)));
        Assert.False(DataReadiness.CanManage(verifix, Org(Permissions.SalesManage)));
        Assert.True(DataReadiness.CanManage(verifix, Org(Permissions.IntegrationsManage)));

        // Строки панели: кнопка и ссылка на подключение — только у тех, кто может исправить.
        var viewer = DashboardService.Freshness(report, Org(Permissions.SalesRead, Permissions.FinanceRead)).ToList();
        Assert.All(viewer, r => Assert.False(r.CanRefresh));
        Assert.All(viewer, r => Assert.Null(r.Href));
        var manager = DashboardService.Freshness(report, Org(Permissions.SalesRead, Permissions.SalesManage)).Single(r => r.Code == "sales");
        Assert.True(manager.CanRefresh);
        Assert.Equal("/settings/integrations/sales/linko", manager.Href);
        Assert.Equal(ReadinessStatus.Stale, manager.Status);
        Assert.Equal(SourceCodes.Linko, manager.Origin);
    }
}

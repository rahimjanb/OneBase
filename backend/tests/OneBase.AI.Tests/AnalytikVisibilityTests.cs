using OneBase.AI.Analytik;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Что видит пользователь с частью прав: находки проактивного анализа по своему региону и честная подпись оценки состояния (не «вся компания»,
/// если видны не все разделы).
/// </summary>
public sealed class AnalytikVisibilityTests
{
    private static readonly Guid Khorazm = Guid.NewGuid();
    private static readonly Guid Samarkand = Guid.NewGuid();

    private static AnalytikPrincipal Supervisor(params string[] extra) =>
        new(Guid.NewGuid(), new HashSet<string>([Permissions.AnalytikUse, Permissions.SalesRead, .. extra]),
            new SalesRowScope(SalesRowScope.RegionLevel, [101], [Khorazm], [], null, "регионы: Хоразм"));

    private static AiAlert Alert(string key, string? permission = null) => new()
    {
        Category = "sales", Rule = key.Split(':')[0], Key = key, Title = "t", Message = "m", RequiredPermission = permission,
    };

    [Fact]
    public void Region_supervisor_sees_only_alerts_of_own_regions()
    {
        var svr = Supervisor();

        Assert.True(RiskEngine.OwnRegionAlert(Alert($"sales.region.plan:{Khorazm}:2026-10"), svr));
        Assert.False(RiskEngine.OwnRegionAlert(Alert($"sales.region.plan:{Samarkand}:2026-10"), svr));
        // Находки по всей организации (план республики, группа регионов, остатки) — с цифрами других регионов, им не показываются.
        Assert.False(RiskEngine.OwnRegionAlert(Alert("sales.republic.plan:2026-10"), svr));
        Assert.False(RiskEngine.OwnRegionAlert(Alert("sales.region.plan.group:2026-10"), svr));
        Assert.False(RiskEngine.OwnRegionAlert(Alert("supply.stock.deficit"), svr));
    }

    [Fact]
    public void Own_region_alert_still_requires_its_permission()
    {
        var alert = Alert($"sales.assortment.gap:{Khorazm}:2026-10", Permissions.FinanceRead);

        Assert.False(RiskEngine.OwnRegionAlert(alert, Supervisor()));
        Assert.True(RiskEngine.OwnRegionAlert(alert, Supervisor(Permissions.FinanceRead)));
    }

    [Fact]
    public void Organization_users_are_not_matched_by_the_region_rule()
    {
        var director = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse, Permissions.SalesRead }, SalesRowScope.Organization);

        // У них общий путь видимости (все находки продаж), правило региона — только для области «регионы».
        Assert.False(RiskEngine.OwnRegionAlert(Alert($"sales.region.plan:{Khorazm}:2026-10"), director));
    }

    [Fact]
    public void Health_coverage_says_when_score_is_not_for_the_whole_company()
    {
        var period = AnalytikPeriod.Of("month", null, null, new DateOnly(2026, 11, 11));
        var hr = new HrBlock(period, [], [], [], []);
        var hrUser = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse }, SalesRowScope.Organization);

        Assert.Equal("Только по доступным вам данным: персонал.", DashboardService.Coverage(hrUser, null, null, null, null, hr));
        Assert.Contains("ни один раздел", DashboardService.Coverage(hrUser, null, null, null, null, null));
    }
}

using OneBase.Application.Field;
using OneBase.Domain.Field;

namespace OneBase.Sales.Tests;

/// <summary>Sales Base: изоляция данных, правила задач, геозона, маршрут, KPI, правила AI-планирования.</summary>
public class FieldScopeTests
{
    private static readonly Guid RmUser = Guid.NewGuid(), Sv1User = Guid.NewGuid(), Sv2User = Guid.NewGuid(), A1User = Guid.NewGuid(), A3User = Guid.NewGuid(), Admin = Guid.NewGuid();
    private static readonly Guid Rm = Guid.NewGuid(), RmBranch = Guid.NewGuid(), Sv1 = Guid.NewGuid(), Sv2 = Guid.NewGuid(), A1 = Guid.NewGuid(), A2 = Guid.NewGuid(), A3 = Guid.NewGuid(), A4 = Guid.NewGuid();
    private static readonly Guid T1 = Guid.NewGuid(), T2 = Guid.NewGuid();
    private static readonly Guid RmBranchUser = Guid.NewGuid();

    private static readonly List<FieldScopeBuilder.MemberRow> Members =
    [
        new(Rm, RmUser, null, FieldRole.Rm, null, [], true),
        new(RmBranch, RmBranchUser, null, FieldRole.Rm, null, [20], true),
        new(Sv1, Sv1User, 101, FieldRole.Supervisor, null, [], true),
        new(Sv2, Sv2User, 102, FieldRole.Supervisor, null, [], true),
        new(A1, A1User, 1, FieldRole.Agent, T1, [], true),
        new(A2, null, 2, FieldRole.Agent, T1, [], true),
        new(A3, A3User, 3, FieldRole.Agent, T2, [], true),
        new(A4, null, 4, FieldRole.Agent, T2, [], false),
    ];

    private static readonly List<FieldScopeBuilder.TeamRow> Teams = [new(T1, Sv1, 10, true), new(T2, Sv2, 20, true)];

    private static FieldScope Build(Guid user, bool use = true, bool manage = false) => FieldScopeBuilder.Build(user, use, manage, Members, Teams);

    [Fact]
    public void Supervisor_sees_only_own_team()
    {
        var scope = Build(Sv1User);

        Assert.Equal(FieldRole.Supervisor, scope.Role);
        Assert.Equal(new HashSet<Guid> { A1, A2 }, scope.AgentIds);
        Assert.False(scope.CanSeeAgent(A3));
        Assert.True(scope.CanPlanFor(A2));
        Assert.False(scope.CanPlanFor(A3));
        Assert.Equal(new[] { 1L, 2L }.Order(), scope.AgentLinkoIds.Order());
    }

    [Fact]
    public void Agent_sees_only_self_and_cannot_plan()
    {
        var scope = Build(A1User);

        Assert.Equal(new HashSet<Guid> { A1 }, scope.AgentIds);
        Assert.False(scope.CanSeeAgent(A2));
        Assert.False(scope.CanPlan);
        Assert.False(scope.CanPlanFor(A1));
        Assert.False(scope.CanSeeMember(Sv1));
    }

    [Fact]
    public void Rm_sees_whole_organization_without_branch_limits()
    {
        var scope = Build(RmUser, manage: true);

        Assert.True(scope.OrgWide);
        Assert.Equal(new HashSet<Guid> { A1, A2, A3 }, scope.AgentIds); // отключённый агент не в области
        Assert.True(scope.CanPlanFor(A3));
    }

    [Fact]
    public void Rm_with_branches_sees_only_their_teams()
    {
        var scope = Build(RmBranchUser, manage: true);

        Assert.False(scope.OrgWide);
        Assert.Equal(new HashSet<Guid> { A3 }, scope.AgentIds);
        Assert.True(scope.CanSeeMember(Sv2));
        Assert.False(scope.CanSeeMember(Sv1));
    }

    [Fact]
    public void Admin_without_member_and_with_manage_gets_organization()
    {
        var scope = Build(Admin, use: true, manage: true);

        Assert.Null(scope.MemberId);
        Assert.True(scope.OrgWide);
        Assert.Equal(3, scope.AgentIds.Count);
    }

    [Fact]
    public void User_without_member_and_without_manage_is_rejected()
    {
        Assert.Throws<FieldForbiddenException>(() => Build(Guid.NewGuid()));
    }

    [Fact]
    public void Member_without_permissions_is_rejected()
    {
        Assert.Throws<FieldForbiddenException>(() => Build(A1User, use: false, manage: false));
    }

    [Fact]
    public void Deactivated_member_loses_access()
    {
        var members = Members.Select(m => m.Id == A3 ? m with { IsActive = false } : m).ToList();

        Assert.Throws<FieldForbiddenException>(() => FieldScopeBuilder.Build(A3User, true, false, members, Teams));
    }

    [Fact]
    public void Agent_moved_to_other_team_leaves_supervisor_scope()
    {
        var members = Members.Select(m => m.Id == A2 ? m with { TeamId = T2 } : m).ToList();

        var sv1 = FieldScopeBuilder.Build(Sv1User, true, false, members, Teams);
        var sv2 = FieldScopeBuilder.Build(Sv2User, true, false, members, Teams);

        Assert.DoesNotContain(A2, sv1.AgentIds);
        Assert.Contains(A2, sv2.AgentIds);
    }

    [Fact]
    public void Deactivated_rm_with_manage_permission_loses_access()
    {
        // Иначе отключённый РМ с правом field.manage попал бы в ветку «администратор без карточки» и получил всю организацию.
        var members = Members.Select(m => m.Id == RmBranch ? m with { IsActive = false } : m).ToList();

        var error = Assert.Throws<FieldForbiddenException>(() => FieldScopeBuilder.Build(RmBranchUser, true, true, members, Teams));
        Assert.Contains("отключён", error.Message);
    }

    [Fact]
    public void Supervisor_with_manage_permission_does_not_manage_structure()
    {
        var scope = Build(Sv1User, manage: true);

        Assert.False(scope.CanManage);
        Assert.False(scope.CanManageOrg);
        Assert.Equal(new HashSet<Guid> { A1, A2 }, scope.AgentIds);
        Assert.False(FieldMemberService.CanManageMember(scope, A1, FieldRole.Agent));
    }

    [Fact]
    public void Only_organization_rm_manages_organization()
    {
        var org = Build(RmUser, manage: true);
        var branch = Build(RmBranchUser, manage: true);
        var admin = Build(Admin, manage: true);

        Assert.True(org.CanManageOrg);
        Assert.True(admin.CanManageOrg);
        Assert.True(branch.CanManage);
        Assert.False(branch.CanManageOrg);
        Assert.Equal(new[] { 20L }, branch.BranchIds);
        Assert.Throws<FieldForbiddenException>(() => FieldMemberService.RequireManageOrg(branch));
    }

    [Fact]
    public void Branch_rm_manages_only_agents_and_supervisors_of_their_zone()
    {
        var branch = Build(RmBranchUser, manage: true);
        var org = Build(RmUser, manage: true);

        Assert.True(FieldMemberService.CanManageMember(branch, A3, FieldRole.Agent));
        Assert.True(FieldMemberService.CanManageMember(branch, Sv2, FieldRole.Supervisor));
        Assert.False(FieldMemberService.CanManageMember(branch, A1, FieldRole.Agent));
        Assert.False(FieldMemberService.CanManageMember(branch, Sv1, FieldRole.Supervisor));
        Assert.False(FieldMemberService.CanManageMember(branch, Rm, FieldRole.Rm));
        Assert.False(FieldMemberService.CanManageMember(branch, RmBranch, FieldRole.Rm)); // себя — только имя и телефон, отдельной проверкой
        Assert.True(FieldMemberService.CanManageMember(org, RmBranch, FieldRole.Rm));
        Assert.True(FieldMemberService.CanManageMember(org, A1, FieldRole.Agent));
    }

    [Fact]
    public void Branch_rm_does_not_see_supervisor_who_also_leads_other_branch()
    {
        // Sv1 ведёт T1 (филиал 10) и T3 (филиал 20): РМ филиала 20 видит агентов T3, но не самого Sv1 — через него видна команда филиала 10.
        var t3 = Guid.NewGuid();
        var a5 = Guid.NewGuid();
        var teams = Teams.Append(new FieldScopeBuilder.TeamRow(t3, Sv1, 20, true)).ToList();
        var members = Members.Append(new FieldScopeBuilder.MemberRow(a5, null, 5, FieldRole.Agent, t3, [], true)).ToList();

        var scope = FieldScopeBuilder.Build(RmBranchUser, true, true, members, teams);

        Assert.Contains(a5, scope.AgentIds);
        Assert.DoesNotContain(A1, scope.AgentIds);
        Assert.False(scope.CanSeeMember(Sv1));
        Assert.True(scope.CanSeeMember(Sv2));
        Assert.False(FieldMemberService.CanManageMember(scope, Sv1, FieldRole.Supervisor));
    }
}

public class FieldTaskRulesTests
{
    [Fact]
    public void Assignee_moves_task_forward_but_cannot_verify_or_cancel()
    {
        Assert.True(FieldTaskRules.CanTransition(FieldTaskStatus.New, FieldTaskStatus.Accepted, isAssignee: true, canPlan: false));
        Assert.True(FieldTaskRules.CanTransition(FieldTaskStatus.Accepted, FieldTaskStatus.InProgress, true, false));
        Assert.True(FieldTaskRules.CanTransition(FieldTaskStatus.InProgress, FieldTaskStatus.Completed, true, false));
        Assert.True(FieldTaskRules.CanTransition(FieldTaskStatus.InProgress, FieldTaskStatus.Postponed, true, false));
        Assert.False(FieldTaskRules.CanTransition(FieldTaskStatus.Completed, FieldTaskStatus.Verified, true, false));
        Assert.False(FieldTaskRules.CanTransition(FieldTaskStatus.New, FieldTaskStatus.Cancelled, true, false));
    }

    [Fact]
    public void Planner_verifies_returns_and_cancels()
    {
        Assert.True(FieldTaskRules.CanTransition(FieldTaskStatus.Completed, FieldTaskStatus.Verified, false, true));
        Assert.True(FieldTaskRules.CanTransition(FieldTaskStatus.Completed, FieldTaskStatus.InProgress, false, true));
        Assert.True(FieldTaskRules.CanTransition(FieldTaskStatus.Postponed, FieldTaskStatus.Cancelled, false, true));
        Assert.False(FieldTaskRules.CanTransition(FieldTaskStatus.Verified, FieldTaskStatus.Cancelled, false, true));
        Assert.False(FieldTaskRules.CanTransition(FieldTaskStatus.New, FieldTaskStatus.Verified, false, true));
    }

    [Fact]
    public void Outsider_cannot_change_anything()
    {
        foreach (var from in Enum.GetValues<FieldTaskStatus>())
        {
            foreach (var to in Enum.GetValues<FieldTaskStatus>())
            {
                Assert.False(FieldTaskRules.CanTransition(from, to, false, false));
            }
        }
    }
}

public class FieldGeoTests
{
    // Ташкент, Амир Темур сквер и точка в ~110 м к северу.
    private const double Lat = 41.311081, Lon = 69.279737;

    [Fact]
    public void Distance_is_haversine_in_meters()
    {
        var d = FieldGeo.DistanceM(Lat, Lon, Lat + 0.001, Lon);

        Assert.InRange(d, 105, 117);
    }

    [Fact]
    public void Within_radius_is_ok_and_far_is_flagged()
    {
        Assert.Equal(FieldGeoStatus.Ok, FieldGeo.Check(Lat, Lon, Lat + 0.001, Lon, 10, radiusM: 150, toleranceM: 100).Status);
        Assert.Equal(FieldGeoStatus.Far, FieldGeo.Check(Lat, Lon, Lat + 0.01, Lon, 10, 150, 100).Status);
    }

    [Fact]
    public void Gps_inaccuracy_is_forgiven_up_to_tolerance()
    {
        // 220 м от точки при радиусе 150: с точностью 80 м — в пределах (150 + 80), с точностью 500 м — допуск ограничен 100 м.
        Assert.Equal(FieldGeoStatus.Ok, FieldGeo.Check(Lat, Lon, Lat + 0.002, Lon, 80, 150, 100).Status);
        Assert.Equal(FieldGeoStatus.Far, FieldGeo.Check(Lat, Lon, Lat + 0.003, Lon, 500, 150, 100).Status);
    }

    [Fact]
    public void Missing_gps_or_target_does_not_block()
    {
        Assert.Equal(FieldGeoStatus.NoGps, FieldGeo.Check(Lat, Lon, null, null, null, 150, 100).Status);
        Assert.Equal(FieldGeoStatus.NoTarget, FieldGeo.Check(0, 0, Lat, Lon, 10, 150, 100).Status);
    }
}

public class FieldRoutingTests
{
    private static FieldRouting.Stop S(long id, double lat, double lon) => new(id, lat, lon);

    [Fact]
    public void Order_visits_nearest_neighbors_and_keeps_unlocated_last()
    {
        var stops = new List<FieldRouting.Stop> { S(3, 41.30, 69.30), new(9, null, null), S(1, 41.10, 69.10), S(2, 41.20, 69.20) };

        var ordered = FieldRouting.Order(stops, 41.09, 69.09);

        Assert.Equal([1L, 2L, 3L, 9L], ordered.Select(s => s.MarketId));
    }

    [Fact]
    public void Two_opt_removes_crossing()
    {
        // Квадрат: обход по периметру короче, чем «бабочкой».
        var stops = new List<FieldRouting.Stop> { S(1, 41.0, 69.0), S(2, 41.0, 69.1), S(3, 41.1, 69.1), S(4, 41.1, 69.0) };
        var crossing = new List<FieldRouting.Stop> { stops[0], stops[2], stops[1], stops[3] };

        var ordered = FieldRouting.Order(crossing, 41.0, 69.0);

        Assert.True(FieldRouting.LengthKm(ordered) < FieldRouting.LengthKm(crossing));
    }

    [Fact]
    public void Schedule_adds_travel_and_visit_time()
    {
        var stops = new List<FieldRouting.Stop> { S(1, 41.0, 69.0), S(2, 41.0, 69.0) };

        var (times, total) = FieldRouting.Schedule(stops, new TimeOnly(9, 0), visitMinutes: 15, speedKmh: 25);

        Assert.Equal(new TimeOnly(9, 0), times[0]);
        Assert.Equal(new TimeOnly(9, 15), times[1]); // та же точка: дорога 0, визит 15 минут
        Assert.Equal(30, total);
    }

    [Fact]
    public void Insertion_goes_between_nearest_points()
    {
        var route = new List<FieldRouting.Stop> { S(1, 41.0, 69.0), S(2, 41.0, 69.2), S(3, 41.0, 69.4) };

        Assert.Equal(1, FieldRouting.InsertionIndex(route, S(9, 41.0, 69.1)));
        Assert.Equal(3, FieldRouting.InsertionIndex(route, S(9, 41.0, 69.6)));
        Assert.Equal(0, FieldRouting.InsertionIndex(route, S(9, 41.0, 68.8)));
    }
}

public class FieldKpiTests
{
    [Fact]
    public void Working_days_exclude_sundays()
    {
        Assert.Equal(26, FieldKpi.WorkingDays(2026, 9)); // сентябрь 2026: 30 дней, 4 воскресенья
        Assert.Equal(3, FieldKpi.ElapsedWorkingDays(new DateOnly(2026, 9, 3)));
    }

    [Fact]
    public void Pace_and_forecast()
    {
        var date = new DateOnly(2026, 9, 15); // 13 рабочих дней из 26 — половина
        Assert.Equal(0.5m, FieldKpi.ExpectedShare(date));
        Assert.Equal(20m, FieldKpi.BehindPace(300, 1000, date)); // 30% при ожидаемых 50%
        Assert.Equal(600m, FieldKpi.Forecast(300, date));
        Assert.Null(FieldKpi.BehindPace(300, null, date));
    }

    [Fact]
    public void Day_status_reflects_route_progress()
    {
        Assert.Equal(FieldKpi.AgentDayStatus.OnVisit, FieldKpi.DayStatus(true, 10, 2, 0, 1, 2, false, 11));
        Assert.Equal(FieldKpi.AgentDayStatus.Problem, FieldKpi.DayStatus(false, 9, 2, 4, 1, 2, false, 11)); // пропущено больше трети
        Assert.Equal(FieldKpi.AgentDayStatus.Finished, FieldKpi.DayStatus(false, 5, 4, 1, 3, 4, false, 17));
        Assert.Equal(FieldKpi.AgentDayStatus.NotStarted, FieldKpi.DayStatus(false, 5, 0, 0, 0, 0, false, 9));
        Assert.Equal(FieldKpi.AgentDayStatus.Problem, FieldKpi.DayStatus(false, 5, 0, 0, 0, 0, false, 15)); // к обеду ни визитов, ни заказов
        Assert.Equal(FieldKpi.AgentDayStatus.OnRoute, FieldKpi.DayStatus(false, 5, 2, 0, 2, 2, false, 12));
    }

    [Fact]
    public void Idle_sunday_is_not_a_problem()
    {
        Assert.Equal(FieldKpi.AgentDayStatus.NotStarted, FieldKpi.DayStatus(false, 0, 0, 0, 0, 0, true, 23, restDay: true));
        Assert.Equal(FieldKpi.AgentDayStatus.NotStarted, FieldKpi.DayStatus(false, 5, 0, 0, 0, 0, false, 16, restDay: true));
        Assert.Equal(FieldKpi.AgentDayStatus.Problem, FieldKpi.DayStatus(false, 5, 0, 0, 0, 0, true, 23)); // будний прошедший день без работы
    }
}

public class FieldPlanningRulesTests
{
    private static readonly FieldSettings Settings = new();
    private static readonly DateOnly Today = new(2026, 9, 20);
    private static readonly Guid Agent = Guid.NewGuid(), Supervisor = Guid.NewGuid();

    private static FieldPlanningRules.MarketSnapshot M(long id, decimal s30, decimal prev30, decimal s90, DateOnly? lastVisit, Guid? agent = null) =>
        new(id, $"Магазин {id}", agent ?? Agent, Supervisor, s30, prev30, s90, lastVisit, lastVisit, FieldPriority.Medium);

    [Fact]
    public void Sales_decline_of_31_percent_creates_visit_recommendation()
    {
        var d = FieldPlanningRules.Decline(M(145, 6_900_000, 10_000_000, 30_000_000, Today.AddDays(-3)), Settings, Today);

        Assert.NotNull(d);
        Assert.Equal(FieldRecommendationKind.SalesDecline, d.Kind);
        Assert.Contains("31%", d.Reason);
        Assert.Equal(Agent, d.AgentId);
        Assert.Equal(Today.AddDays(2), d.DueDate);
        Assert.InRange(d.Confidence, 0.5m, 0.95m);
    }

    [Fact]
    public void Small_decline_or_small_customer_is_ignored()
    {
        Assert.Null(FieldPlanningRules.Decline(M(1, 8_000_000, 10_000_000, 30_000_000, Today), Settings, Today)); // −20%
        Assert.Null(FieldPlanningRules.Decline(M(2, 100_000, 500_000, 900_000, Today), Settings, Today)); // меньше порога суммы
    }

    [Fact]
    public void New_customer_with_zero_threshold_does_not_divide_by_zero()
    {
        // Порог «падение — от суммы» 0, у клиента не было продаж в прошлые 30 дней: раньше — деление на ноль во всём прогоне.
        var settings = new FieldSettings { DeclineMinSales = 0 };

        Assert.Null(FieldPlanningRules.Decline(M(3, 2_000_000, 0, 2_000_000, Today), settings, Today));
    }

    [Fact]
    public void Lost_customer_wins_over_decline()
    {
        var drafts = FieldPlanningRules.ForMarkets([M(7, 0, 5_000_000, 9_000_000, Today.AddDays(-40))], Settings, Today);

        var d = Assert.Single(drafts);
        Assert.Equal(FieldRecommendationKind.LostCustomer, d.Kind);
        Assert.Equal(FieldPriority.High, d.Priority);
    }

    [Fact]
    public void Not_visited_for_two_weeks_with_sales_is_recommended()
    {
        Assert.NotNull(FieldPlanningRules.NotVisited(M(3, 1_000_000, 1_000_000, 3_000_000, Today.AddDays(-20)), Settings, Today));
        Assert.Null(FieldPlanningRules.NotVisited(M(6, 30_000, 30_000, 90_000, Today.AddDays(-30)), Settings, Today)); // мелкая точка
        Assert.Null(FieldPlanningRules.NotVisited(M(4, 1_000_000, 1_000_000, 3_000_000, Today.AddDays(-5)), Settings, Today));
        Assert.Null(FieldPlanningRules.NotVisited(M(5, 0, 0, 0, null), Settings, Today));
    }

    [Fact]
    public void At_most_five_recommendations_per_agent_highest_first()
    {
        var markets = Enumerable.Range(1, 9).Select(i => M(i, 0, 1_000_000m * i, 2_000_000m * i, null)).ToList();

        var drafts = FieldPlanningRules.ForMarkets(markets, Settings, Today);

        Assert.Equal(FieldPlanningRules.PerAgentLimit, drafts.Count);
        Assert.Equal(9, drafts[0].MarketId);
    }

    [Fact]
    public void Agent_behind_plan_goes_to_supervisor()
    {
        var drafts = FieldPlanningRules.ForAgents(
            [new FieldPlanningRules.AgentPace(Agent, "Азиз", Supervisor, 1000, 300), new FieldPlanningRules.AgentPace(Guid.NewGuid(), "Алишер", Supervisor, 1000, 650)],
            Settings,
            Today);

        var d = Assert.Single(drafts);
        Assert.Equal(FieldRecommendationKind.AgentBehindPlan, d.Kind);
        Assert.Equal(Supervisor, d.SupervisorId);
        Assert.Contains("30%", d.Title);
    }

    [Fact]
    public void No_plan_judgement_in_first_days_of_month()
    {
        Assert.Empty(FieldPlanningRules.ForAgents([new FieldPlanningRules.AgentPace(Agent, "Азиз", Supervisor, 1000, 0)], Settings, new DateOnly(2026, 9, 3)));
    }

    [Fact]
    public void Keys_deduplicate_by_rule_and_market()
    {
        var a = FieldPlanningRules.Decline(M(145, 5_000_000, 10_000_000, 30_000_000, Today), Settings, Today)!;
        var b = FieldPlanningRules.Decline(M(145, 4_000_000, 10_000_000, 30_000_000, Today), Settings, Today.AddDays(1))!;

        Assert.Equal(a.Key, b.Key);
    }
}

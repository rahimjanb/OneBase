using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Knowledge;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Field;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Инструменты задач отделов и Sales Base: схема аргументов, понятные ошибки аргументов, выбор отдела по доступу пользователя
/// и область видимости Sales Base (супервайзер находит только агентов своей команды). Без базы: ошибки аргументов возникают раньше обращения к сервисам.
/// </summary>
public class WorkFieldToolsTests
{
    private static GetDepartmentTasksTool DepartmentTasks() => new(null!, null!, null!);
    private static GetFieldTasksTool FieldTasks() => new(null!, null!, null!, null!, null!, null!);
    private static GetFieldRoutesTool FieldRoutes() => new(null!, null!, null!, null!, null!, null!);
    private static GetFieldRecommendationsTool FieldRecommendations() => new(null!, null!, null!, null!);

    private static DataTool[] All() => [DepartmentTasks(), FieldTasks(), FieldRoutes(), FieldRecommendations()];

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static Task<ToolResult> Run(ITool tool, object args, Guid? user = null) => tool.ExecuteAsync(new ToolContext("sales", user, 0), Json(args));

    [Fact]
    public void Schemas_are_objects_with_typed_and_described_optional_arguments()
    {
        foreach (var tool in All())
        {
            Assert.Matches("^[a-z_]+$", tool.Name);
            Assert.False(string.IsNullOrWhiteSpace(tool.Title));
            Assert.True(tool.Description.Length > 200, $"{tool.Name}: описание слишком короткое");
            Assert.Contains("yyyy-MM-dd", tool.Description);

            var schema = tool.InputSchema;
            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.False(schema.TryGetProperty("required", out _), $"{tool.Name}: все аргументы необязательны");
            var properties = schema.GetProperty("properties").EnumerateObject().ToList();
            Assert.Contains(properties, p => p.Name == "limit");
            foreach (var p in properties)
            {
                var type = p.Value.GetProperty("type").GetString();
                Assert.True(type is "string" or "integer", $"{tool.Name}.{p.Name}: тип {type}");
                Assert.False(string.IsNullOrWhiteSpace(p.Value.GetProperty("description").GetString()), $"{tool.Name}.{p.Name}: нет описания");
                if (p.Value.TryGetProperty("enum", out var values))
                {
                    Assert.Equal("string", type);
                    Assert.All(values.EnumerateArray(), v => Assert.Matches("^[a-z_]+$", v.GetString()!));
                }
            }
        }

        Assert.Equal(4, All().Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public void Enum_codes_and_russian_words_are_accepted()
    {
        foreach (var c in GetDepartmentTasksTool.Statuses)
        {
            Assert.Equal(c.Value, WorkFieldText.Pick(c.Code, "status", default, GetDepartmentTasksTool.Statuses));
        }

        foreach (var c in GetFieldTasksTool.Statuses)
        {
            Assert.Equal(c.Value, WorkFieldText.Pick(c.Code, "status", default, GetFieldTasksTool.Statuses));
        }

        foreach (var c in GetFieldRecommendationsTool.Kinds)
        {
            Assert.Equal(c.Value, WorkFieldText.Pick(c.Code, "kind", default, GetFieldRecommendationsTool.Kinds));
            Assert.Equal(c.Value, WorkFieldText.Pick(c.Value.ToString(), "kind", default, GetFieldRecommendationsTool.Kinds));
        }

        Assert.Equal(GetDepartmentTasksTool.StatusFilter.Overdue, WorkFieldText.Pick("Просроченные", "status", default, GetDepartmentTasksTool.Statuses));
        Assert.Equal(GetDepartmentTasksTool.StatusFilter.Cancelled, WorkFieldText.Pick("отменённые", "status", default, GetDepartmentTasksTool.Statuses));
        Assert.Equal(GetFieldTasksTool.StatusFilter.Postponed, WorkFieldText.Pick("отложенные", "status", default, GetFieldTasksTool.Statuses));
        Assert.Equal(GetFieldRecommendationsTool.StatusFilter.Rejected, WorkFieldText.Pick("отклонённые", "status", default, GetFieldRecommendationsTool.Statuses));
    }

    [Fact]
    public void Department_tasks_need_the_tasks_section_and_field_tools_field_rights()
    {
        var departments = DepartmentTasks();
        Assert.Equal(KnowledgeSources.WorkTasks, departments.Source);
        Assert.Equal(Permissions.WorkTasks, departments.RequiredPermission); // раздел «Задачи»; отделы внутри проверяет сервис
        foreach (var tool in new DataTool[] { FieldTasks(), FieldRoutes(), FieldRecommendations() })
        {
            Assert.Equal(KnowledgeSources.FieldSales, tool.Source);
            Assert.Equal(Permissions.FieldUse, tool.RequiredPermission);
            Assert.Contains(Permissions.FieldPlan, tool.AlternativePermissions!);
            Assert.Contains(Permissions.FieldManage, tool.AlternativePermissions!);
        }
    }

    [Fact]
    public async Task Wrong_arguments_are_explained_with_allowed_values()
    {
        var status = await Run(DepartmentTasks(), new { status = "срочные" });
        Assert.False(status.Success);
        Assert.Contains("Аргумент status: значения «срочные» нет", status.Content);
        foreach (var code in new[] { "open", "overdue", "done", "cancelled", "all" })
        {
            Assert.Contains(code, status.Content);
        }

        var fieldStatus = await Run(FieldTasks(), new { status = "забытые" });
        Assert.False(fieldStatus.Success);
        Assert.Contains("postponed (отложенные)", fieldStatus.Content);

        var recommendationStatus = await Run(FieldRecommendations(), new { status = "может быть" });
        Assert.Contains("pending (ждут решения)", recommendationStatus.Content);

        var kind = await Run(FieldRecommendations(), new { kind = "погода" });
        Assert.False(kind.Success);
        Assert.Contains("sales_decline (падение продаж)", kind.Content);
        Assert.Contains("reassign (точка без агента)", kind.Content);

        var date = await Run(FieldRoutes(), new { date = "21.10.2026" });
        Assert.False(date.Success);
        Assert.Contains("yyyy-MM-dd", date.Content);

        var period = await Run(FieldRoutes(), new { from = "2026-08-01", to = "2026-10-21" });
        Assert.Contains($"не больше {GetFieldRoutesTool.MaxDays} дней", period.Content);

        var reversed = await Run(FieldRoutes(), new { from = "2026-10-21", to = "2026-10-01" });
        Assert.Contains("раньше from", reversed.Content);

        var both = await Run(FieldRoutes(), new { date = "2026-10-20", from = "2026-10-01" });
        Assert.Contains("либо date", both.Content);

        var limit = await Run(DepartmentTasks(), new { limit = 0 });
        Assert.Contains("limit — от 1 до 100", limit.Content);

        var days = await Run(FieldTasks(), new { days = 1000 });
        Assert.Contains("days — от 1 до 180", days.Content);
    }

    [Fact]
    public async Task Nothing_is_read_without_a_user()
    {
        foreach (var tool in All())
        {
            var result = await Run(tool, new { });
            Assert.False(result.Success);
            Assert.Contains("от имени пользователя", result.Content);
        }
    }

    [Fact]
    public void Route_period_is_a_day_or_a_range()
    {
        var today = FieldClock.Today;
        Assert.Equal((today, today), GetFieldRoutesTool.Period(new ToolArgs(Json(new { }))));
        Assert.Equal((new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20)), GetFieldRoutesTool.Period(new ToolArgs(Json(new { date = "2026-10-20" }))));
        Assert.Equal((today.AddDays(-1), today.AddDays(-1)), GetFieldRoutesTool.Period(new ToolArgs(Json(new { date = "вчера" }))));
        Assert.Equal((today.AddDays(-6), today), GetFieldRoutesTool.Period(new ToolArgs(Json(new { from = WorkFieldText.Day(today.AddDays(-6)) }))));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7)), GetFieldRoutesTool.Period(new ToolArgs(Json(new { from = "2026-10-01", to = "2026-10-07" }))));
    }

    private static DepartmentView Dept(string code, string name, bool hasTasks, bool hasField = false) =>
        new(Guid.NewGuid(), code, name, hasTasks || hasField, hasTasks, hasTasks, hasField, 0, 0, 0);

    /// <summary>Сотрудник финансов с доступом к Sales Base: задачи видит только своего отдела.</summary>
    private static readonly DepartmentView[] Company =
    [
        Dept("sales", "Продажи", hasTasks: false, hasField: true),
        Dept("production", "Производство", hasTasks: false),
        Dept("finance", "Финансы", hasTasks: true),
        Dept("hr", "HR", hasTasks: false),
    ];

    private static string Single(string query) => Assert.Single(GetDepartmentTasksTool.SelectDepartments(Company, query)).Code;

    [Fact]
    public void Department_is_found_by_code_name_or_word_stem()
    {
        Assert.Equal("finance", Single("finance"));
        Assert.Equal("finance", Single("Финансы"));
        Assert.Equal("finance", Single("отдел финансов"));
        Assert.Equal("finance", Single("FIN"));
    }

    [Fact]
    public void Unknown_department_lists_departments()
    {
        var e = Assert.Throws<ToolArgumentException>(() => GetDepartmentTasksTool.SelectDepartments(Company, "склад"));
        Assert.Contains("отдела «склад» нет", e.Message);
        Assert.Contains("finance (Финансы)", e.Message);
        Assert.Contains("hr (HR)", e.Message);
    }

    [Fact]
    public void Department_without_access_is_refused_and_left_out_of_overview()
    {
        var e = Assert.Throws<ToolArgumentException>(() => GetDepartmentTasksTool.SelectDepartments(Company, "production"));
        Assert.Contains("Нет доступа к задачам отдела «Производство»", e.Message);
        Assert.Contains("Доступные отделы: finance (Финансы)", e.Message);

        // «Продажи»: задачи отдела закрыты, но полевая команда доступна — подсказка про get_field_tasks.
        var sales = Assert.Throws<ToolArgumentException>(() => GetDepartmentTasksTool.SelectDepartments(Company, "Продажи"));
        Assert.Contains("Нет доступа", sales.Message);
        Assert.Contains("get_field_tasks", sales.Message);

        Assert.Equal(new[] { "finance" }, GetDepartmentTasksTool.SelectDepartments(Company, null).Select(d => d.Code));

        var none = Assert.Throws<ToolArgumentException>(() => GetDepartmentTasksTool.SelectDepartments(Company.Where(d => d.Code != "finance").ToList(), null));
        Assert.Contains("нет доступа к задачам ни одного отдела", none.Message);
    }

    /// <summary>Две команды: Север (супервайзер — пользователь теста, агент Иванов) и Юг (агент Петров).</summary>
    private static (FieldDirectory.Snapshot Snapshot, Func<Guid, FieldScope> ScopeOf, Guid SupervisorUser, Guid IvanovUser, Guid Ivanov, Guid North) Field()
    {
        var supervisorUser = Guid.NewGuid();
        var ivanovUser = Guid.NewGuid();
        var supervisor = Guid.NewGuid();
        var otherSupervisor = Guid.NewGuid();
        var north = Guid.NewGuid();
        var south = Guid.NewGuid();
        var ivanov = Guid.NewGuid();
        FieldMemberInfo[] members =
        [
            new(supervisor, supervisorUser, null, FieldRole.Supervisor, "Супервайзер Север", null, null, [], true),
            new(otherSupervisor, Guid.NewGuid(), null, FieldRole.Supervisor, "Супервайзер Юг", null, null, [], true),
            new(ivanov, ivanovUser, 11, FieldRole.Agent, "Иванов Иван", null, north, [], true),
            new(Guid.NewGuid(), Guid.NewGuid(), 12, FieldRole.Agent, "Петров Пётр", null, south, [], true),
        ];
        FieldTeamInfo[] teams = [new(north, "Север", supervisor, 1, true), new(south, "Юг", otherSupervisor, 2, true)];
        var snapshot = new FieldDirectory.Snapshot(members.ToDictionary(m => m.Id), teams.ToDictionary(t => t.Id));
        FieldScope ScopeOf(Guid user) => FieldScopeBuilder.Build(user, canUse: true, canManage: false,
            members.Select(m => new FieldScopeBuilder.MemberRow(m.Id, m.UserId, m.LinkoUserId, m.Role, m.TeamId, m.BranchIds, m.IsActive)).ToList(),
            teams.Select(t => new FieldScopeBuilder.TeamRow(t.Id, t.SupervisorId, t.BranchId, t.IsActive)).ToList());
        return (snapshot, ScopeOf, supervisorUser, ivanovUser, ivanov, north);
    }

    [Fact]
    public void Supervisor_picks_only_agents_and_teams_of_own_zone()
    {
        var (snapshot, scopeOf, supervisorUser, _, ivanov, north) = Field();
        var scope = scopeOf(supervisorUser);

        Assert.Equal(ivanov, FieldToolScope.Member(scope, snapshot, "иванов", agentsOnly: true)!.Id);
        Assert.Equal(north, FieldToolScope.Team(scope, snapshot, "север")!.Id);

        var foreign = Assert.Throws<ToolArgumentException>(() => FieldToolScope.Member(scope, snapshot, "Петров", agentsOnly: true));
        Assert.Contains("«Петров» не найден", foreign.Message);
        Assert.Contains("Иванов Иван", foreign.Message);
        Assert.DoesNotContain("Петров Пётр", foreign.Message); // чужого агента не называем

        var foreignTeam = Assert.Throws<ToolArgumentException>(() => FieldToolScope.Team(scope, snapshot, "Юг"));
        Assert.Contains("Команды в зоне пользователя: Север", foreignTeam.Message);
    }

    [Fact]
    public void Agent_sees_only_himself()
    {
        var (snapshot, scopeOf, _, ivanovUser, ivanov, _) = Field();
        var scope = scopeOf(ivanovUser);

        Assert.Equal(ivanov, FieldToolScope.Member(scope, snapshot, "me", agentsOnly: true)!.Id);
        Assert.Throws<ToolArgumentException>(() => FieldToolScope.Member(scope, snapshot, "Петров", agentsOnly: true));
        Assert.Throws<ToolArgumentException>(() => FieldToolScope.Member(scope, snapshot, "Супервайзер", agentsOnly: false));
    }
}

/// <summary>
/// На настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): get_department_tasks отдаёт задачи только отделов,
/// которые пользователю открывает WorkTaskService, — сотрудник видит свой отдел, чужой — нет.
/// </summary>
public class WorkFieldDatabaseTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    [PostgresFact]
    public async Task Employee_sees_tasks_of_own_department_only()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var finance = new Department { Code = $"wf-fin-{suffix}", Name = $"Финансы {suffix}" };
        var supply = new Department { Code = $"wf-sup-{suffix}", Name = $"Снабжение {suffix}" };
        var role = new Role { Name = $"Сотрудник {suffix}" };
        role.Permissions.Add(new RolePermission { Code = Permissions.FilesRead });
        var accountant = new User { Login = $"wf-acc-{suffix}", FullName = "Бухгалтер Тестовый", Department = finance };
        accountant.Roles.Add(new UserRole { Role = role });
        var buyer = new User { Login = $"wf-buy-{suffix}", FullName = "Закупщик Тестовый", Department = supply };
        buyer.Roles.Add(new UserRole { Role = role });
        var today = FieldClock.Today;

        using (var seed = fixture.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.Users.AddRange(accountant, buyer);
            db.WorkTasks.AddRange(
                new WorkTask { Department = finance, Title = "Сверка оплат дилеров", AssigneeId = accountant.Id, CreatedById = buyer.Id, DueDate = today.AddDays(-3), Priority = WorkTaskPriority.High },
                new WorkTask
                {
                    Department = finance, Title = "Закрыть акты сентября", AssigneeId = accountant.Id, Status = WorkTaskStatus.Done,
                    CompletedAt = DateTimeOffset.UtcNow.AddDays(-2), Result = "Акты подписаны",
                },
                new WorkTask { Department = supply, Title = "Заказать упаковку", AssigneeId = buyer.Id, DueDate = today.AddDays(-1) });
            await db.SaveChangesAsync();
        }

        using var scope = fixture.Services.CreateScope();
        var tool = ActivatorUtilities.CreateInstance<GetDepartmentTasksTool>(scope.ServiceProvider);
        Task<ToolResult> Ask(User user, object args) => tool.ExecuteAsync(new ToolContext("finance", user.Id, 0), Json(args));

        var own = await Ask(accountant, new { department = finance.Code, status = "all" });
        Assert.True(own.Success, own.Content);
        Assert.Contains("Сверка оплат дилеров", own.Content);
        Assert.Contains("\"overdueDays\":3", own.Content);
        Assert.Contains("Акты подписаны", own.Content);
        Assert.Contains("\"done\":1", own.Content);
        Assert.Contains($"/tasks/{finance.Code}", JsonSerializer.Serialize(own.Sources));

        var foreign = await Ask(accountant, new { department = supply.Code });
        Assert.False(foreign.Success);
        Assert.Contains($"Нет доступа к задачам отдела «{supply.Name}»", foreign.Content);
        Assert.DoesNotContain("упаковку", foreign.Content);

        var overview = await Ask(accountant, new { status = "all" });
        Assert.True(overview.Success, overview.Content);
        Assert.Contains("Сверка оплат дилеров", overview.Content);
        Assert.DoesNotContain("упаковку", overview.Content);
        Assert.Contains(supply.Name, overview.Content); // отдел назван среди недоступных — без задач

        var mine = await Ask(accountant, new { assignee = "me", status = "overdue" });
        Assert.Contains("\"mine\":true", mine.Content);

        var buyerView = await Ask(buyer, new { status = "overdue" });
        Assert.True(buyerView.Success, buyerView.Content);
        Assert.Contains("Заказать упаковку", buyerView.Content);
        Assert.DoesNotContain("Сверка оплат", buyerView.Content);
    }

    /// <summary>
    /// Sales Base: супервайзер команды «Север» видит задачи, маршруты, визиты и рекомендации своих агентов, но не команды «Юг»;
    /// агент — только своё и без рекомендаций; пользователь без Sales Base — ничего.
    /// </summary>
    [PostgresFact]
    public async Task Field_tools_follow_the_users_zone()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var fieldRole = new Role { Name = $"Sales Base {suffix}" };
        fieldRole.Permissions.Add(new RolePermission { Code = Permissions.FieldUse });
        var officeRole = new Role { Name = $"Офис {suffix}" };
        officeRole.Permissions.Add(new RolePermission { Code = Permissions.FilesRead });
        User Person(string login, string name, Role role)
        {
            var user = new User { Login = $"{login}-{suffix}", FullName = name };
            user.Roles.Add(new UserRole { Role = role });
            return user;
        }

        var supervisorUser = Person("wf-sup", "Супервайзер Север", fieldRole);
        var ivanovUser = Person("wf-iv", "Иванов Иван", fieldRole);
        var outsider = Person("wf-out", "Бухгалтер", officeRole);
        var supervisor = new FieldMember { UserId = supervisorUser.Id, Role = FieldRole.Supervisor, FullName = "Супервайзер Север" };
        var north = new FieldTeam { Name = $"Север {suffix}", Supervisor = supervisor };
        var south = new FieldTeam { Name = $"Юг {suffix}" };
        var ivanov = new FieldMember { UserId = ivanovUser.Id, LinkoUserId = 7001, Role = FieldRole.Agent, FullName = "Иванов Иван", Team = north };
        var petrov = new FieldMember { LinkoUserId = 7002, Role = FieldRole.Agent, FullName = "Петров Пётр", Team = south };
        var today = FieldClock.Today;

        using (var seed = fixture.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.Users.AddRange(supervisorUser, ivanovUser, outsider);
            db.FieldMembers.AddRange(supervisor, ivanov, petrov);
            db.FieldTeams.AddRange(north, south);
            db.LinkoMarkets.AddRange(new LinkoMarket { Id = 9101, Name = "Магазин Ромашка" }, new LinkoMarket { Id = 9102, Name = "Магазин Лилия" },
                new LinkoMarket { Id = 9103, Name = "Магазин Астра" });
            db.FieldTasks.AddRange(
                new FieldTask
                {
                    Title = "Взять заказ в Ромашке", Description = "Продажи точки упали на 40% к прошлым 30 дням", AssignedTo = ivanov, MarketId = 9101,
                    CreatedByType = FieldActorType.Ai, DueDate = today.AddDays(-2),
                },
                new FieldTask
                {
                    Title = "Выложить кексы в Лилии", AssignedTo = ivanov, CreatedById = supervisor.Id, CreatedByType = FieldActorType.Supervisor,
                    Status = FieldTaskStatus.Completed, CompletedAt = DateTimeOffset.UtcNow.AddDays(-1), Result = "Выложено на две полки",
                },
                new FieldTask { Title = "Задача Петрова", AssignedTo = petrov, CreatedByType = FieldActorType.System, DueDate = today.AddDays(-5) });
            var visit = new FieldVisit
            {
                MarketId = 9101, Agent = ivanov, StartedAt = DateTimeOffset.UtcNow.AddMinutes(-40), EndedAt = DateTimeOffset.UtcNow.AddMinutes(-25),
                Status = FieldVisitStatus.Completed, Result = FieldVisitResult.Order, Amount = 1_500_000, Comment = "Взяли кекс и рулет", GeoStatus = FieldGeoStatus.Ok,
            };
            var route = new FieldRoute { Date = today, Agent = ivanov, Status = FieldRouteStatus.InProgress, Source = FieldRouteSource.Linko };
            route.Points.Add(new FieldRoutePoint { MarketId = 9101, Sequence = 1, Status = FieldPointStatus.Visited, VisitId = visit.Id });
            route.Points.Add(new FieldRoutePoint { MarketId = 9102, Sequence = 2, Status = FieldPointStatus.Skipped, Note = "Магазин закрыт на ремонт" });
            route.Points.Add(new FieldRoutePoint { MarketId = 9103, Sequence = 3 });
            visit.RouteId = route.Id;
            var southRoute = new FieldRoute { Date = today, Agent = petrov };
            southRoute.Points.Add(new FieldRoutePoint { MarketId = 9103, Sequence = 1, Status = FieldPointStatus.Skipped, Note = "Чужая причина" });
            db.FieldRoutes.AddRange(route, southRoute);
            db.FieldVisits.Add(visit);
            db.FieldRecommendations.AddRange(
                new FieldRecommendation
                {
                    Kind = FieldRecommendationKind.NotVisited, Key = $"nv-{suffix}", Title = "Посетить Магазин Астра", Reason = "Точку не посещали 21 день",
                    Confidence = 0.8m, AgentId = ivanov.Id, SupervisorId = supervisor.Id, MarketId = 9103,
                },
                new FieldRecommendation { Kind = FieldRecommendationKind.SalesDecline, Key = $"sd-{suffix}", Title = "Чужая рекомендация", Reason = "—", AgentId = petrov.Id });
            await db.SaveChangesAsync();
        }

        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tasks = ActivatorUtilities.CreateInstance<GetFieldTasksTool>(sp);
        var routes = ActivatorUtilities.CreateInstance<GetFieldRoutesTool>(sp);
        var recommendations = ActivatorUtilities.CreateInstance<GetFieldRecommendationsTool>(sp);
        Task<ToolResult> Ask(ITool tool, User user, object args) => tool.ExecuteAsync(new ToolContext("sales", user.Id, 0), Json(args));

        // Супервайзер: задачи своей команды с причиной и просрочкой, чужие — нет.
        var overdue = await Ask(tasks, supervisorUser, new { status = "overdue" });
        Assert.True(overdue.Success, overdue.Content);
        Assert.Contains("Взять заказ в Ромашке", overdue.Content);
        Assert.Contains("Магазин Ромашка", overdue.Content);
        Assert.Contains("\"overdueDays\":2", overdue.Content);
        Assert.Contains("Продажи точки упали на 40%", overdue.Content);
        Assert.DoesNotContain("Петрова", overdue.Content);

        var done = await Ask(tasks, supervisorUser, new { status = "done" });
        Assert.True(done.Success, done.Content);
        Assert.Contains("Выложено на две полки", done.Content);
        Assert.Contains("супервайзер Супервайзер Север", done.Content);

        var foreignAgent = await Ask(tasks, supervisorUser, new { agent = "Петров" });
        Assert.False(foreignAgent.Success);
        Assert.DoesNotContain("Петров Пётр", foreignAgent.Content);

        // Маршруты команды: охват, причина пропуска своей команды, чужой — нет.
        var team = await Ask(routes, supervisorUser, new { });
        Assert.True(team.Success, team.Content);
        Assert.Contains("Иванов Иван", team.Content);
        Assert.Contains("Магазин закрыт на ремонт", team.Content);
        Assert.DoesNotContain("Чужая причина", team.Content);
        Assert.DoesNotContain("Петров", team.Content);

        var agentRoute = await Ask(routes, supervisorUser, new { agent = "Иванов", date = WorkFieldText.Day(today) });
        Assert.True(agentRoute.Success, agentRoute.Content);
        Assert.Contains("\"skipReason\":\"Магазин закрыт на ремонт\"", agentRoute.Content);
        Assert.Contains("\"result\":\"заказ\"", agentRoute.Content);
        Assert.Contains("Взяли кекс и рулет", agentRoute.Content);
        Assert.Contains("Магазин Астра", agentRoute.Content);

        var week = await Ask(routes, supervisorUser, new { agent = "Иванов", from = WorkFieldText.Day(today.AddDays(-6)) });
        Assert.True(week.Success, week.Content);
        Assert.Contains("Магазин закрыт на ремонт", week.Content);

        var pending = await Ask(recommendations, supervisorUser, new { });
        Assert.True(pending.Success, pending.Content);
        Assert.Contains("Посетить Магазин Астра", pending.Content);
        Assert.Contains("давно не посещали", pending.Content);
        Assert.DoesNotContain("Чужая рекомендация", pending.Content);

        // Агент: только своё; рекомендации AI — не для агента.
        var own = await Ask(tasks, ivanovUser, new { status = "open" });
        Assert.True(own.Success, own.Content);
        Assert.Contains("Взять заказ в Ромашке", own.Content);
        Assert.DoesNotContain("Петрова", own.Content);
        var ownRoute = await Ask(routes, ivanovUser, new { });
        Assert.True(ownRoute.Success, ownRoute.Content);
        Assert.Contains("Магазин Лилия", ownRoute.Content);
        var agentRecommendations = await Ask(recommendations, ivanovUser, new { });
        Assert.False(agentRecommendations.Success);
        Assert.Contains("видят супервайзер и РМ", agentRecommendations.Content);

        // Без Sales Base — ничего.
        var none = await Ask(tasks, outsider, new { });
        Assert.False(none.Success);
        Assert.DoesNotContain("Ромашке", none.Content);
    }
}

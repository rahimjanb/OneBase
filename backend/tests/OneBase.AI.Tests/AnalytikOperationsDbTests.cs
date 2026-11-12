using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Analytik;
using OneBase.AI.Consultant;
using OneBase.AI.Llm;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Аналитик на настоящем PostgreSQL: план закупок по движениям склада (через сервисы снабжения), находки без повторов и уведомления только
/// при новой или выросшей критичности, фоновые задачи — один раз за период, отчёт показывает каждому только разделы по его правам.
/// </summary>
public sealed class AnalytikOperationsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private async Task<(User Keeper, Guid Category, Guid Department)> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var role = new Role { Name = $"Кладовщик {s}" };
        role.Permissions.Add(new RolePermission { Code = Permissions.ProcurementWarehouse });
        var keeper = new User { Login = $"an-kpr-{s}", FullName = "Кладовщик" };
        keeper.Roles.Add(new UserRole { Role = role });
        var dept = new Department { Code = $"an-{s}", Name = $"Цех {s}" };
        db.Users.Add(keeper);
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        var category = await db.ProcurementCategories.Where(c => c.Code == "components").Select(c => c.Id).SingleAsync();
        return (keeper, category, dept.Id);
    }

    private async Task<T> AsKeeper<T>(User keeper, Func<ProcurementStockService, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(keeper.Id, Keeper.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider.GetRequiredService<ProcurementStockService>(), ctx);
    }

    [PostgresFact]
    public async Task Procurement_plan_uses_ledger_consumption_and_receipt_price()
    {
        var (keeper, category, dept) = await SeedAsync();
        var sku = $"AN-{Guid.NewGuid():N}"[..12];
        var item = await AsKeeper(keeper, (s, c) => s.CreateItemAsync(c, new ProcurementCatalogInput(sku, $"Мука {sku}", category, "кг", null, null, 50), default));
        await AsKeeper(keeper, async (s, c) => await s.ReceiptAsync(c, new ProcurementStockOperationInput([new(item, 900, 12_000)], "Приход", "Н-1"), default));
        foreach (var qty in new[] { 100m, 200m, 150m })
        {
            await AsKeeper(keeper, async (s, c) => await s.IssueAsync(c, new([new(item, qty)], "В цех", DepartmentId: dept), default));
        }

        using var scope = fixture.Services.CreateScope();
        var plan = await scope.ServiceProvider.GetRequiredService<SupplyPlanner>().PlanAsync(AnalytikPrincipal.SystemPrincipal, CancellationToken.None, sku);
        var r = plan.Items.Single();
        Assert.Equal(450, r.OnHand);
        Assert.Equal(450, r.Available);
        // Расход 450 за 90 дней истории — 5 в день; цена — из прихода; срока поставки нет — принят по умолчанию и помечен.
        Assert.Equal(5, r.AvgDailyConsumption);
        Assert.Equal(12_000, r.UnitPrice);
        Assert.True(r.LeadTimeAssumed);
        Assert.Equal(90, r.DaysOfCover);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(91), r.ShortageDate);
        // Вся выдача — в один день: разброс большой (σ ≈ 47,4 кг/день), страховой запас 1,645 × σ × √7 ≈ 206 кг — больше минимума 50.
        // 90 дней: спрос 450 + страховой запас − доступно 450 = страховой запас; стоимость — по цене прихода.
        var need90 = r.Horizon(90)!;
        Assert.InRange(need90.SafetyStock, 206m, 207m);
        Assert.Equal(450, need90.Demand);
        Assert.Equal(Math.Ceiling(need90.SafetyStock * 1000) / 1000, need90.Required);
        Assert.Equal(Math.Round(need90.Required * 12_000, 0), need90.Cost);
        Assert.Equal("ok", plan.Warehouse.Single().Status);

        // Пользователь без прав снабжения, склада и производства плана не видит.
        var viewer = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse, Permissions.SalesRead }, SalesRowScope.Organization);
        await Assert.ThrowsAsync<QueryPlanException>(() => scope.ServiceProvider.GetRequiredService<SupplyPlanner>().PlanAsync(viewer, CancellationToken.None));
    }

    [PostgresFact]
    public async Task Insights_are_deduplicated_and_notified_once_per_severity()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        Guid analyst;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var role = new Role { Name = $"Аналитик склада {s}" };
            role.Permissions.Add(new RolePermission { Code = Permissions.AnalytikUse });
            role.Permissions.Add(new RolePermission { Code = Permissions.ProcurementRead });
            var user = new User { Login = $"an-{s}", FullName = "Аналитик склада" };
            user.Roles.Add(new UserRole { Role = role });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            analyst = user.Id;
        }

        var key = $"test.stockout:{s}";
        var rules = new HashSet<string> { "test.stockout" };
        InsightCandidate Candidate(string severity) => new(InsightKinds.Risk, "procurement", "test.stockout", key, severity, "Мука: запас закончится", "Тест",
            Recommendations: ["Заказать 100 кг"], RequiredPermission: "procurement.read|procurement.manage");

        async Task<(InsightRunResult Result, int Notifications)> Apply(params InsightCandidate[] found)
        {
            using var scope = fixture.Services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<InsightStore>().ApplyAsync(found, rules, true, CancellationToken.None);
            var count = await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().UserNotifications
                .CountAsync(n => n.RecipientId == analyst && n.Kind == UserNotificationKind.AgentAlert);
            return (result, count);
        }

        var first = await Apply(Candidate(Severities.Warning));
        Assert.Equal(1, first.Result.Created);
        Assert.Equal(1, first.Notifications);

        var repeat = await Apply(Candidate(Severities.Warning));
        Assert.Equal(0, repeat.Result.Created);
        Assert.Equal(1, repeat.Result.Updated);
        Assert.Equal(1, repeat.Notifications);

        var worse = await Apply(Candidate(Severities.Critical));
        Assert.Equal(2, worse.Notifications);

        var gone = await Apply();
        Assert.True(gone.Result.Resolved >= 1);
        using var check = fixture.Services.CreateScope();
        var row = await check.ServiceProvider.GetRequiredService<IAnalytikDb>().Insights.AsNoTracking().SingleAsync(i => i.Key == key);
        Assert.Equal(3, row.Occurrences);
        Assert.NotNull(row.ResolvedAt);

        // Без права находки её не видно.
        var store = check.ServiceProvider.GetRequiredService<InsightStore>();
        var outsider = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse }, SalesRowScope.Organization);
        Assert.DoesNotContain(await store.ListAsync(outsider, null, true, 100, CancellationToken.None), i => i.Rule == "test.stockout");

        // Качество данных уведомляет только критичным: предупреждение видно в «Рисках», но колокольчик не трогает.
        rules.Add("test.quality");
        InsightCandidate Quality(string severity) => new(InsightKinds.Quality, "procurement", "test.quality", $"test.quality:{s}", severity, "Нет минимумов", "Тест",
            RequiredPermission: Permissions.ProcurementRead);
        Assert.Equal(2, (await Apply(Quality(Severities.Warning))).Notifications);
        Assert.Equal(3, (await Apply(Quality(Severities.Critical))).Notifications);

        // Много новых находок за один проход — одно сводное уведомление, а не по одному на каждую.
        rules.Add("test.many");
        var many = Enumerable.Range(1, 5).Select(i => new InsightCandidate(InsightKinds.Risk, "procurement", "test.many", $"test.many:{s}:{i}", Severities.Warning,
            $"Позиция {i}: запас закончится", "Тест", RequiredPermission: Permissions.ProcurementRead)).ToArray();
        var digest = await Apply(many);
        Assert.Equal(4, digest.Notifications);
        Assert.Equal(1, digest.Result.Notified);
        using var last = fixture.Services.CreateScope();
        var newest = await last.ServiceProvider.GetRequiredService<OneBaseDbContext>().UserNotifications.AsNoTracking()
            .Where(n => n.RecipientId == analyst).OrderByDescending(n => n.CreatedAt).FirstAsync();
        Assert.StartsWith("Аналитик: новых рисков — 5", newest.Title);
    }

    [PostgresFact]
    public async Task Background_job_runs_once_per_period()
    {
        var key = $"test-{Guid.NewGuid():N}"[..20];
        using (var scope = fixture.Services.CreateScope())
        {
            var ok = await scope.ServiceProvider.GetRequiredService<AnalytikJobs>().RunAsync(new AnalytikJob("refresh", key), CancellationToken.None);
            var error = await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().JobRuns.AsNoTracking().Where(j => j.PeriodKey == key).Select(j => j.Error).SingleAsync();
            Assert.True(ok, error);
        }

        using (var scope = fixture.Services.CreateScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<AnalytikJobs>().RunAsync(new AnalytikJob("refresh", key), CancellationToken.None));
            var runs = await scope.ServiceProvider.GetRequiredService<IAnalytikDb>().JobRuns.AsNoTracking().Where(j => j.PeriodKey == key).ToListAsync();
            Assert.Equal("ok", Assert.Single(runs).Status);
        }

        // Расписание: каждые 30 минут и каждый час — всегда; ежедневная — после времени отчёта; недельная — по понедельникам; месячная — 1-го.
        var monday = new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);
        var due = AnalytikWorker.Due(monday, "07:30").Select(j => $"{j.Name}:{j.PeriodKey}").ToList();
        Assert.Equal(["refresh:2026-11-02T08:00", "hourly:2026-11-02T08", "daily:2026-11-02", "weekly:2026-W45"], due);
        Assert.Equal(["refresh:2026-12-01T03:30", "hourly:2026-12-01T03"],
            AnalytikWorker.Due(new DateTimeOffset(2026, 11, 30, 22, 40, 0, TimeSpan.Zero), "07:30").Select(j => $"{j.Name}:{j.PeriodKey}"));
        Assert.Contains("monthly:2026-12", AnalytikWorker.Due(new DateTimeOffset(2026, 12, 1, 4, 0, 0, TimeSpan.Zero), "07:30").Select(j => $"{j.Name}:{j.PeriodKey}"));
    }

    [PostgresFact]
    public async Task Scheduled_report_shows_each_reader_only_permitted_sections()
    {
        using var scope = fixture.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<AnalytikReportService>();
        var period = AnalytikPeriod.Of("custom", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), new DateOnly(2026, 11, 11));
        var row = await reports.CreateAsync("health", period, AnalytikPrincipal.SystemPrincipal, null, null, CancellationToken.None);

        var director = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse, Permissions.SalesRead, Permissions.FinanceRead }, SalesRowScope.Organization);
        var full = await reports.GetAsync(row.Id, director, CancellationToken.None);
        var sales = full!.Sections.Single(x => x.Key == "sales");
        Assert.Equal(900m + 500m, sales.Kpis.Single(k => k.Code == "sales.kg").Value);

        var hrOnly = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse, Permissions.HrRead }, SalesRowScope.Organization);
        var limited = await reports.GetAsync(row.Id, hrOnly, CancellationToken.None);
        Assert.DoesNotContain(limited!.Sections, x => x.Module is "sales" or "finance");

        var regional = director with { Sales = new SalesRowScope(SalesRowScope.RegionLevel, [5], [fixture.KhorazmId], [], null, "регионы: Хоразм") };
        Assert.DoesNotContain((await reports.GetAsync(row.Id, regional, CancellationToken.None))!.Sections, x => x.SalesScoped);

        // Отчёт пользователя виден только ему.
        var own = await reports.CreateAsync("sales", period, director, director.UserId, null, CancellationToken.None);
        Assert.Null(await reports.GetAsync(own.Id, hrOnly, CancellationToken.None));
        Assert.NotNull(await reports.GetAsync(own.Id, director, CancellationToken.None));
        Assert.NotEmpty(reports.Excel(full));
        Assert.NotEmpty(reports.Pdf(full));
    }

    private static AgentConfig Agent(string code) =>
        new(code, code, null, null, "", true, null, null, null, true, 0, null, Permissions.AnalytikUse, [AnalytikTools.Source]);

    [Fact]
    public void Analytik_routing_fallback_picks_analysts_by_words_and_never_routes_small_talk()
    {
        var available = new[] { "analyst.sales", "analyst.supply", "analyst.hr", AnalytikAgents.Bi }.Select(Agent).ToList();
        var failed = new RoutingDecision(available.Select(a => a.Code).ToList(), new Dictionary<string, string>(), "", true, AiTokenUsage.None);
        ConsultantTurn Turn(string q) => new(Guid.NewGuid(), Guid.NewGuid(), q, []);

        Assert.Equal(["analyst.supply"], AnalytikEngine.Enforce(failed, Turn("Сколько сырья нужно закупить на месяц?"), available).Agents);
        Assert.Equal([AnalytikAgents.Bi], AnalytikEngine.Enforce(failed, Turn("Исследуй таблицу twin.Objects и её связи"), available).Agents);
        Assert.Empty(AnalytikEngine.Enforce(failed, Turn("Привет!"), available).Agents);
        Assert.Equal(4, AnalytikEngine.Enforce(failed, Turn("Проанализируй всю компанию за месяц"), available).Agents.Count);

        // Ответ маршрутизатора сохраняется, но только доступные аналитики.
        var routed = new RoutingDecision(["analyst.hr", "analyst.finance"], new Dictionary<string, string> { ["analyst.hr"] = "опоздания" }, "по смыслу", false, AiTokenUsage.None);
        var result = AnalytikEngine.Enforce(routed, Turn("Кто чаще опаздывает?"), available);
        Assert.Equal(["analyst.hr"], result.Agents);
        Assert.Equal("опоздания", result.Tasks["analyst.hr"]);
    }

    [Fact]
    public void Consultant_and_analytik_teams_do_not_mix()
    {
        Assert.All(AgentDefaults.Departments, d => Assert.Equal(AgentTeams.Consultant, AgentTeams.Of(d.Code)));
        Assert.All(AnalytikAgents.All, a => Assert.Equal(AgentTeams.Analytik, AgentTeams.Of(a.Code)));
        Assert.All(AgentDefaults.Departments, d => Assert.DoesNotContain(AnalytikTools.Source, d.Sources));
        // Chief Analyst + 7 аналитиков отделов и данных + аналитик сценариев (What-If).
        Assert.Equal(9, AnalytikAgents.All.Count);
    }

    [PostgresFact]
    public async Task Half_shift_lateness_is_reported_as_a_schedule_problem()
    {
        // 40 дней «опоздания» на 10 часов (график не той смены) и 20 обычных опозданий на 10 минут — за последние 30 дней.
        var baseId = 8_000_000_000L + Random.Shared.Next(1_000_000) * 100L;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        for (var i = 0; i < 60; i++)
        {
            var date = today.AddDays(-(i % 20) - 1);
            var begin = date.ToDateTime(new TimeOnly(8, 0));
            db.HrTimesheetDays.Add(new Domain.Hr.HrTimesheetDay
            {
                Id = baseId + i, StaffId = baseId + i, VerifixEmployeeId = baseId + i, Date = date, DayKind = "W",
                PlanBegin = begin, PlanEnd = begin.AddHours(9), FirstIn = begin.AddMinutes(i < 40 ? 600 : 10),
            });
        }

        await db.SaveChangesAsync();
        try
        {
            var (found, evaluated) = await scope.ServiceProvider.GetRequiredService<OneBase.Application.Analytik.Quality.DataQualityEngine>().RunAsync(CancellationToken.None);
            Assert.Contains("quality.hr_schedule", evaluated);
            var finding = Assert.Single(found, f => f.Rule == "quality.hr_schedule");
            Assert.Equal(Severities.Warning, finding.Severity);
            Assert.True(finding.Impact >= 40, $"{finding.Impact}");
            Assert.Equal(Permissions.HrRead, finding.RequiredPermission);
        }
        finally
        {
            db.HrTimesheetDays.RemoveRange(await db.HrTimesheetDays.Where(x => x.Id >= baseId && x.Id < baseId + 100).ToListAsync());
            await db.SaveChangesAsync();
        }
    }

    [PostgresFact]
    public async Task Payments_gap_is_not_an_anomaly_until_payments_are_synced()
    {
        // Девять недель оплат в далёком будущем (другие тесты туда не пишут), за вчера оплат нет.
        var today = new DateOnly(2031, 3, 3);
        var from = today.AddDays(-64);
        var baseId = 9_000_000_000L + Random.Shared.Next(1_000_000) * 100L;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        for (var i = 0; i < 63; i++)
        {
            db.LinkoPayments.Add(new Domain.Sales.LinkoPayment
            {
                Id = baseId + i, Amount = 1_000_000m + i % 5 * 40_000m, Status = "accepted", CreatedDate = from.AddDays(i), Type = "cash",
            });
        }

        var sync = await db.LinkoSyncStates.FirstOrDefaultAsync(x => x.Entity == "payments");
        var created = sync is null;
        sync ??= db.LinkoSyncStates.Add(new Domain.Sales.LinkoSyncState { Entity = "payments" }).Entity;
        var previous = sync.LastSuccessAt;
        try
        {
            // Обмен прошёл до конца вчерашнего дня: нули за вчера — «данных нет», находки нет.
            sync.LastSuccessAt = new DateTimeOffset(2031, 3, 2, 12, 0, 0, TimeSpan.Zero);
            await db.SaveChangesAsync();
            var risk = scope.ServiceProvider.GetRequiredService<RiskEngine>();
            Assert.Empty(await risk.PaymentsAnomalyAsync(today, CancellationToken.None));

            // Обмен прошёл сегодня: вчерашний ноль — настоящее отклонение.
            sync.LastSuccessAt = new DateTimeOffset(2031, 3, 3, 4, 0, 0, TimeSpan.Zero);
            await db.SaveChangesAsync();
            var hit = Assert.Single(await risk.PaymentsAnomalyAsync(today, CancellationToken.None));
            Assert.Equal("finance.payments_anomaly:2031-03-02", hit.Key);
        }
        finally
        {
            if (created)
            {
                db.LinkoSyncStates.Remove(sync);
            }
            else
            {
                sync.LastSuccessAt = previous;
            }

            db.LinkoPayments.RemoveRange(await db.LinkoPayments.Where(x => x.Id >= baseId && x.Id < baseId + 100).ToListAsync());
            await db.SaveChangesAsync();
        }
    }
}

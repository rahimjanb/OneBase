using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik;
using OneBase.AI.Analytik.Simulation;
using OneBase.AI.Tools;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Simulation;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Analytik;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Симулятор What-If на настоящем PostgreSQL: снимок исходных данных из реальных таблиц (продажи Linko по SKU и регионам, техкарта, склад
/// сырья, утверждённые смены), сохранение и воспроизводимость сценария, пересчёт на актуальных данных, сравнение, права (разделы, область
/// регионов, чужие сценарии), изоляция компании (Tenant) и то, что симулятор не меняет ни одной бизнес-таблицы.
/// </summary>
public sealed class AnalytikSimulatorDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateOnly SeptemberEnd = new(2026, 9, 30);

    private static readonly string[] Director =
    [
        Permissions.AnalytikUse, Permissions.SalesRead, Permissions.FinanceRead, Permissions.ProductionRead, Permissions.ProcurementRead,
        Permissions.ProcurementManage,
    ];

    private sealed record Seeded(Guid Owner, Guid Flour, string Sku);

    /// <summary>
    /// Мука (12 000 сум/кг, приход 900 кг, выдано 450 кг), действующая техкарта «Кекс 1 кг» (товар Linko 100): 60 кг муки на 100 кг, без этапов —
    /// 0,6 кг/кг; утверждённые смены — 3 по 400 кг.
    /// </summary>
    private async Task<Seeded> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var role = new Role { Name = $"Директор сим {s}" };
        foreach (var code in Director.Append(Permissions.ProcurementWarehouse))
        {
            role.Permissions.Add(new RolePermission { Code = code });
        }

        var owner = new User { Login = $"sim-{s}", FullName = "Директор" };
        owner.Roles.Add(new UserRole { Role = role });
        var dept = new Department { Code = $"sim-{s}", Name = $"Цех {s}" };
        db.Users.Add(owner);
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        // Сырьё — через сервисы склада, как в жизни: приход с ценой и выдачи в цех (история расхода).
        var category = await db.ProcurementCategories.Where(c => c.Code == "components").Select(c => c.Id).SingleAsync();
        var stock = scope.ServiceProvider.GetRequiredService<ProcurementStockService>();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>()
            .ContextAsync(owner.Id, new HashSet<string> { Permissions.ProcurementWarehouse, Permissions.ProcurementRequest }, CancellationToken.None);
        var sku = $"SIM-{s}";
        var flour = await stock.CreateItemAsync(ctx, new ProcurementCatalogInput(sku, $"Мука {s}", category, "кг", null, null, 0), default);
        await stock.ReceiptAsync(ctx, new ProcurementStockOperationInput([new(flour, 900, 12_000)], "Приход", "Н-1"), default);
        foreach (var qty in new[] { 200m, 250m })
        {
            await stock.IssueAsync(ctx, new([new(flour, qty)], "В цех", DepartmentId: dept.Id), default);
        }

        // Действующая техкарта товара Linko 100, смены по ней (смены прежних тестов класса убираются — мощность считается по этим трём).
        await db.ProductionShifts.ExecuteDeleteAsync();
        await db.ProductionRecipes.Where(r => r.LinkoProductId == 100).ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, ProductionRecipeStatus.Archived));
        var recipe = new ProductionRecipe
        {
            Code = $"TK-{s}", Name = "Кекс 1 кг", OutputName = "Кекс 1 кг", OutputUnit = "кг", OutputQuantity = 100, Status = ProductionRecipeStatus.Active,
            ActivatedAt = DateTimeOffset.UtcNow, LinkoProductId = 100,
        };
        recipe.Lines.Add(new ProductionRecipeLine { ItemId = flour, LineNo = 1, Quantity = 60 });
        db.ProductionRecipes.Add(recipe);
        var warehouse = await db.ProcurementWarehouses.Select(w => w.Id).FirstAsync();
        var order = new ProductionOrder
        {
            Number = $"PRD-{s}", RecipeId = recipe.Id, DepartmentId = dept.Id, WarehouseId = warehouse, PlannedQty = 1200, PlannedDate = new DateOnly(2026, 11, 1),
            Status = ProductionOrderStatus.Completed, OutputQty = 1200, CreatedById = owner.Id,
        };
        db.ProductionOrders.Add(order);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        for (var i = 1; i <= 3; i++)
        {
            db.ProductionShifts.Add(new ProductionShift
            {
                Number = $"SHF-{s}-{i}", OrderId = order.Id, ShiftDate = today.AddDays(-i), ShiftNo = 1, OperatorId = owner.Id, CreatedById = owner.Id,
                Status = ProductionShiftStatus.Approved, GoodOutput = 400,
            });
        }

        await db.SaveChangesAsync();
        return new Seeded(owner.Id, flour, sku);
    }

    private static AnalytikPrincipal Principal(Guid user, IEnumerable<string> permissions, SalesRowScope? scope = null) =>
        new(user, permissions.ToHashSet(), scope ?? SalesRowScope.Organization);

    private T Service<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Отпечаток бизнес-данных: число строк и суммы ключевых таблиц продаж, склада, производства, закупок, планов, пользователей.</summary>
    private async Task<string> FingerprintAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = Service<OneBaseDbContext>(scope);
        var parts = new object?[]
        {
            await db.LinkoOrders.CountAsync(), await db.LinkoOrderLines.SumAsync(l => l.TotalPrice), await db.LinkoPayments.CountAsync(),
            await db.ProcurementStockBalances.SumAsync(b => b.OnHand), await db.ProcurementStockBalances.SumAsync(b => b.Reserved),
            await db.ProcurementStockMovements.CountAsync(), await db.ProcurementBatches.CountAsync(), await db.ProcurementPurchaseOrders.CountAsync(),
            await db.ProcurementRequests.CountAsync(), await db.ProductionOrders.CountAsync(), await db.ProductionShifts.CountAsync(),
            await db.ProductionRecipes.CountAsync(), await db.ProductionRecipeLines.SumAsync(l => l.Quantity), await db.SalesRegionPlans.CountAsync(),
            await db.SalesRegions.CountAsync(), await db.Users.CountAsync(), await db.ProcurementItems.SumAsync(i => i.MinQty),
        };
        return string.Join("|", parts);
    }

    [PostgresFact]
    public async Task Baseline_is_read_from_real_tables_with_recipe_chain()
    {
        var seed = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var p = Principal(seed.Owner, Director);
        var view = await Service<ScenarioService>(scope).BaselineAsync(p, September, SeptemberEnd, CancellationToken.None);

        // Продажи сентября из фикстуры: Хоразм 900 кг и Бухоро 500 кг кекса по 30 000 сум/кг; «Завод» — не регион вторички.
        Assert.Equal(1400, view.Summary.Kg);
        Assert.Equal(42_000_000, view.Summary.Revenue);
        Assert.Equal(2, view.Summary.Regions);
        Assert.Equal(1, view.Summary.ProductsWithRecipe);
        Assert.Equal(1, view.Summary.RecipeShare);
        // Мощность: типичная лучшая смена 400 кг × 1 смена × 3 рабочих дня из 3.
        Assert.Equal(400, view.Summary.CapacityKgDay);
        Assert.Contains(view.Choices.Items, i => i.Id == seed.Flour.ToString());
        Assert.Equal(["sales", "production", "materials", "finance"], view.Sections);

        var stored = SimulationJson.Read<ScenarioBaselineData>(await Service<OneBaseDbContext>(scope).Set<AnalytikScenarioBaseline>()
            .Where(b => b.Id == view.Id).Select(b => b.Data).SingleAsync());
        var cake = stored.Sales!.Products.Single(x => x.Id == 100);
        Assert.Equal(0.6m, cake.Norms.Single().PerKg);
        var flour = stored.Materials!.Single(m => m.ItemId == seed.Flour);
        Assert.Equal(450, flour.Available);
        Assert.Equal(12_000, flour.Price);
        Assert.Equal(5, flour.HistoryRate);
        Assert.True(flour.LeadTimeAssumed);

        // Сценарий на снимке: спрос +30 % — кекс 1820 кг за 30 дней, мука 0,6 кг/кг.
        var result = await Service<ScenarioService>(scope).RunAsync(p,
            new RunInput(view.Id, ScenarioKinds.Sales, 30, [new ScenarioParameter("sales.demand", 30)], null), CancellationToken.None);
        Assert.True(result.Valid);
        Assert.Equal(1820, result.Metric("sales.requested_kg")!.Scenario);
        Assert.Equal(Math.Round(1820 * 30_000m, 0), result.Metric("sales.revenue")!.Scenario);
        Assert.Equal(Math.Round(1820 * 0.6m / 30, 3), result.Materials.Single(m => m.ItemId == seed.Flour).Daily);
    }

    [PostgresFact]
    public async Task Saved_scenario_is_reproducible_until_recalculated_on_current_data()
    {
        // 12.11.2026 10:00 по Ташкенту: последний полный месяц — октябрь.
        fixture.Clock.Now = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(5));
        try
        {
            var seed = await SeedAsync();
            var p = Principal(seed.Owner, Director);
            ScenarioView saved;
            using (var scope = fixture.Services.CreateScope())
            {
                var service = Service<ScenarioService>(scope);
                var baseline = await service.BaselineAsync(p, September, SeptemberEnd, CancellationToken.None);
                saved = await service.SaveAsync(p, null, new ScenarioInput("Рост +30 %", ScenarioKinds.Combined, null, baseline.Id, 30,
                    [new("sales.demand", 30), new("materials.price", 15), new("production.capacity", 20), new("materials.lead_time", 7)], null), CancellationToken.None);
            }

            Assert.Equal(42_000_000, saved.Result!.Metric("sales.revenue")!.Base);
            Assert.Equal(1, saved.Revision);

            // Данные OneBase изменились (новые продажи в сентябре и октябре) — сохранённый сценарий не меняется.
            await AddOrderAsync(DataManagerDb.FakeLinkoIds + 501, 7, 100, 200, new DateOnly(2026, 9, 20));
            await AddOrderAsync(DataManagerDb.FakeLinkoIds + 502, 7, 100, 300, new DateOnly(2026, 10, 15));
            using (var scope = fixture.Services.CreateScope())
            {
                var reopened = await Service<ScenarioService>(scope).GetAsync(p, saved.Id, CancellationToken.None);
                Assert.Equal(SimulationJson.Write(saved.Result), SimulationJson.Write(reopened.Result));
                Assert.Equal(saved.Baseline.Id, reopened.Baseline.Id);
                Assert.Equal(1400, reopened.Baseline.Summary.Kg);
            }

            // «Пересчитать на актуальных данных»: базовый период — тот же один полный месяц, но последний (октябрь); прежний расчёт — в истории.
            using (var scope = fixture.Services.CreateScope())
            {
                var recalculated = await Service<ScenarioService>(scope).RecalculateAsync(p, saved.Id, CancellationToken.None);
                Assert.Equal(2, recalculated.Revision);
                Assert.NotEqual(saved.Baseline.Id, recalculated.Baseline.Id);
                Assert.Equal(new DateOnly(2026, 10, 1), recalculated.Baseline.From);
                Assert.Equal(new DateOnly(2026, 10, 31), recalculated.Baseline.To);
                // Октябрь — 31 день, горизонт — 30: база пересчитана пропорционально дням.
                Assert.Equal(Math.Round(300 * 30_000m * 30 / 31, 0), recalculated.Result!.Metric("sales.revenue")!.Base);
                var history = Assert.Single(recalculated.History);
                Assert.Equal(saved.Baseline.Id, history.BaselineId);
                Assert.Equal(September, history.From);
                Assert.Equal(42_000_000, history.Revenue);
                Assert.True(await Service<OneBaseDbContext>(scope).Set<AnalytikScenarioBaseline>()
                    .Where(b => b.Id == recalculated.Baseline.Id).Select(b => b.Pinned).SingleAsync());
            }
        }
        finally
        {
            await fixture.ResetAsync();
        }
    }

    [PostgresFact]
    public async Task Copy_archive_compare_and_audit()
    {
        var seed = await SeedAsync();
        var p = Principal(seed.Owner, Director);
        using var scope = fixture.Services.CreateScope();
        var service = Service<ScenarioService>(scope);
        var baseline = await service.BaselineAsync(p, September, SeptemberEnd, CancellationToken.None);
        var a = await service.SaveAsync(p, null, new ScenarioInput("База", ScenarioKinds.Sales, ScenarioPresets.Base, baseline.Id, 30, [], null), default);
        var b = await service.SaveAsync(p, null, new ScenarioInput("Цена +10 %", ScenarioKinds.Sales, null, baseline.Id, 30, [new("sales.price", 10)], null), default);
        var c = await service.CopyAsync(p, b.Id, null, default);
        Assert.Equal("Цена +10 % (копия)", c.Name);
        Assert.Equal(b.Id, c.CopiedFromId);
        c = await service.SaveAsync(p, c.Id, new ScenarioInput("Цена −5 %", ScenarioKinds.Sales, null, baseline.Id, 30, [new("sales.price", -5)], null), default);

        var comparison = await service.CompareAsync(p, [a.Id, b.Id, c.Id], default);
        var revenue = comparison.Rows.Single(r => r.Code == "sales.revenue");
        Assert.Equal([42_000_000m, 46_200_000m, 39_900_000m], revenue.Values.Select(v => v!.Value).ToArray());
        Assert.Equal([0m, 10m, -5m], revenue.DeltaPercent.Select(v => v!.Value).ToArray());
        Assert.Equal(4_200_000, revenue.Delta[1]);

        await service.ArchiveAsync(p, b.Id, true, default);
        Assert.DoesNotContain(await service.ListAsync(p, false, default), r => r.Id == b.Id);
        Assert.Contains(await service.ListAsync(p, true, default), r => r.Id == b.Id);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(p, b.Id, new ScenarioInput("x", ScenarioKinds.Sales, null, baseline.Id, 30, [], null), default));
        await service.ArchiveAsync(p, b.Id, false, default);

        var db = Service<OneBaseDbContext>(scope);
        var actions = await db.AuditLogs.Where(x => x.ActorId == seed.Owner.ToString() && x.Action.StartsWith("analytik.scenario.")).Select(x => x.Action).ToListAsync();
        Assert.Contains("analytik.scenario.create", actions);
        Assert.Contains("analytik.scenario.copy", actions);
        Assert.Contains("analytik.scenario.update", actions);
        Assert.Contains("analytik.scenario.archive", actions);
        Assert.Contains("analytik.scenario.restore", actions);
        Assert.Contains("analytik.scenario.compare", actions);
    }

    [PostgresFact]
    public async Task Rights_limit_sections_regions_and_other_peoples_scenarios()
    {
        var seed = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var service = Service<ScenarioService>(scope);
        var owner = Principal(seed.Owner, Director);
        var baseline = await service.BaselineAsync(owner, September, SeptemberEnd, default);
        var saved = await service.SaveAsync(owner, null, new ScenarioInput("Директор", ScenarioKinds.Sales, null, baseline.Id, 30, [new("sales.demand", 10)], null), default);

        // Без единого раздела — симулятор не строит снимок.
        var nobody = Principal(Guid.NewGuid(), [Permissions.AnalytikUse]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BaselineAsync(nobody, September, SeptemberEnd, default));

        // РМ Хоразма: только свой регион, без оплат точек (они видны только при доступе ко всей организации).
        var rmScope = new SalesRowScope(SalesRowScope.RegionLevel, [5], [fixture.KhorazmId], [], null, "Хоразм");
        var rm = Principal(Guid.NewGuid(), [Permissions.AnalytikUse, Permissions.SalesRead, Permissions.FinanceRead], rmScope);
        var rmBaseline = await service.BaselineAsync(rm, September, SeptemberEnd, default);
        Assert.Equal(900, rmBaseline.Summary.Kg);
        Assert.Equal(1, rmBaseline.Summary.Regions);
        Assert.Null(rmBaseline.Summary.CollectionRate);
        Assert.DoesNotContain("materials", rmBaseline.Sections);

        // Чужой сценарий и чужой снимок: не видны (как несуществующие) — ни открыть, ни посчитать на нём.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(rm, saved.Id, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RunAsync(rm, new RunInput(baseline.Id, ScenarioKinds.Sales, 30, [], null), default));
        Assert.DoesNotContain(await service.ListAsync(rm, false, default), r => r.Id == saved.Id);

        // Управляющий Аналитиком с теми же разделами — видит и может копировать, но не менять; управляющий с областью региона — не видит.
        var manager = Principal(Guid.NewGuid(), Director.Append(Permissions.AnalytikManage));
        Assert.Contains(await service.ListAsync(manager, false, default), r => r.Id == saved.Id && !r.Own);
        Assert.False((await service.GetAsync(manager, saved.Id, default)).Own);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ArchiveAsync(manager, saved.Id, true, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RecalculateAsync(manager, saved.Id, default));
        var narrowManager = Principal(Guid.NewGuid(), [Permissions.AnalytikUse, Permissions.AnalytikManage, Permissions.SalesRead], rmScope);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(narrowManager, saved.Id, default));
    }

    [PostgresFact]
    public async Task Scenarios_of_another_tenant_are_invisible()
    {
        var seed = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var service = Service<ScenarioService>(scope);
        var p = Principal(seed.Owner, Director);
        var baseline = await service.BaselineAsync(p, September, SeptemberEnd, default);
        var db = Service<OneBaseDbContext>(scope);
        var foreignBaseline = new AnalytikScenarioBaseline
        {
            Tenant = "other-company", CreatedById = seed.Owner, PeriodFrom = September, PeriodTo = SeptemberEnd, AsOf = SeptemberEnd, FormulaVersion = "sim-1",
            Sections = "sales", ScopeKey = "sales|organization||", Data = await db.Set<AnalytikScenarioBaseline>().Where(b => b.Id == baseline.Id).Select(b => b.Data).SingleAsync(),
            Sources = "[]", Hash = "x", Pinned = true,
        };
        var foreign = new AnalytikScenario
        {
            Tenant = "other-company", OwnerId = seed.Owner, Name = "Чужая компания", Kind = ScenarioKinds.Sales, Preset = ScenarioPresets.Custom,
            Baseline = foreignBaseline, HorizonDays = 30, Parameters = "[]", FormulaVersion = "sim-1",
        };
        db.Add(foreign);
        await db.SaveChangesAsync();

        Assert.DoesNotContain(await service.ListAsync(p, false, default), r => r.Id == foreign.Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(p, foreign.Id, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RunAsync(p, new RunInput(foreignBaseline.Id, ScenarioKinds.Sales, 30, [], null), default));
    }

    [PostgresFact]
    public async Task Simulation_never_changes_business_data()
    {
        var seed = await SeedAsync();
        var before = await FingerprintAsync();
        var p = Principal(seed.Owner, Director);
        using (var scope = fixture.Services.CreateScope())
        {
            var service = Service<ScenarioService>(scope);
            var baseline = await service.BaselineAsync(p, September, SeptemberEnd, default);
            var parameters = new List<ScenarioParameter>
            {
                new("sales.demand", 30), new("materials.price", 15), new("production.capacity", 20), new("materials.lead_time", 7), new("materials.stock", -50),
                new("procurement.volume", 50), new("procurement.min_lot", 1000, seed.Flour.ToString()), new("production.norms", 10),
            };
            await service.RunAsync(p, new RunInput(baseline.Id, ScenarioKinds.Combined, 90, parameters, null), default);
            var saved = await service.SaveAsync(p, null, new ScenarioInput("Всё сразу", ScenarioKinds.Combined, null, baseline.Id, 90, parameters, null), default);
            await service.CopyAsync(p, saved.Id, null, default);
            await service.RecalculateAsync(p, saved.Id, default);
            await service.ArchiveAsync(p, saved.Id, true, default);
        }

        Assert.Equal(before, await FingerprintAsync());
    }

    [PostgresFact]
    public async Task Ai_tool_turns_the_question_into_engine_parameters_with_names_resolved()
    {
        var seed = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var tool = Service<IToolRegistry>(scope).Find("analytik_simulate")!;
        var result = await tool.ExecuteAsync(new ToolContext(AnalytikAgents.Scenario, seed.Owner, 0), JsonSerializer.SerializeToElement(new
        {
            sales_change_percent = 25, raw_material_price_change_percent = 10, base_from = "2026-09-01", base_to = "2026-09-30",
            parameters = new[] { new { code = "sales.region", value = 10, target = "хоразм" } },
        }));
        Assert.True(result.Success, result.Content);
        var json = JsonDocument.Parse(result.Content).RootElement;

        // Хоразм 900 кг × 1,25 × 1,1 + Бухоро 500 кг × 1,25 — по 30 000 сум/кг; число посчитал движок, а не модель.
        var revenue = json.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("code").GetString() == "sales.revenue");
        Assert.Equal(42_000_000, revenue.GetProperty("base").GetDecimal());
        Assert.Equal((900 * 1.25m * 1.1m + 500 * 1.25m) * 30_000, revenue.GetProperty("scenario").GetDecimal());
        var region = json.GetProperty("parameters").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "sales.region");
        Assert.Equal(fixture.KhorazmId.ToString(), region.GetProperty("target").GetString());
        Assert.StartsWith("/analytik/simulator?draft=", json.GetProperty("openInSimulator").GetString());
        Assert.Contains("sales.revenue", result.Content, StringComparison.Ordinal);

        // Неизвестная цель — понятная ошибка со списком вариантов, а не выдуманный регион.
        var wrong = await tool.ExecuteAsync(new ToolContext(AnalytikAgents.Scenario, seed.Owner, 0), JsonSerializer.SerializeToElement(new
        {
            base_from = "2026-09-01", base_to = "2026-09-30", parameters = new[] { new { code = "sales.region", value = 10, target = "Марс" } },
        }));
        Assert.False(wrong.Success);
        Assert.Contains("Хоразм", wrong.Content, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task Ai_analysis_gets_only_engine_numbers_and_is_saved_for_the_owner()
    {
        var seed = await SeedAsync();
        var p = Principal(seed.Owner, Director);
        try
        {
            using var scope = fixture.Services.CreateScope();
            var service = Service<ScenarioService>(scope);
            var baseline = await service.BaselineAsync(p, September, SeptemberEnd, default);
            var saved = await service.SaveAsync(p, null, new ScenarioInput("Спрос +30 %", ScenarioKinds.Sales, null, baseline.Id, 30, [new("sales.demand", 30)], null), default);
            string? sent = null;
            fixture.Gateway.Script.Enqueue(messages =>
            {
                sent = messages[1].Content;
                return "**Основной вывод**: выручка 42 000 000 → 54 600 000 сум (+30 %).";
            });

            var text = await Service<ScenarioAnalyst>(scope).ExplainAsync(p, saved.Id, default);

            Assert.StartsWith("**Основной вывод**", text);
            var call = Assert.Single(fixture.Gateway.Calls);
            Assert.Equal(ScenarioAnalyst.Prompt, call.Messages[0].Content);
            Assert.Equal("analytik.scenario.explain", call.Options.Context.Purpose);
            Assert.Contains("54600000", sent);
            Assert.Contains("42000000", sent);
            Assert.Equal(text, (await service.GetAsync(p, saved.Id, default)).AiAnalysis);

            // Пересчёт на актуальных данных сбрасывает анализ — он относился к прежним числам.
            var recalculated = await service.RecalculateAsync(p, saved.Id, default);
            Assert.Null(recalculated.AiAnalysis);
        }
        finally
        {
            await fixture.ResetAsync();
        }
    }

    [PostgresFact]
    public async Task Export_writes_excel_and_pdf_with_every_section()
    {
        var seed = await SeedAsync();
        var p = Principal(seed.Owner, Director);
        using var scope = fixture.Services.CreateScope();
        var service = Service<ScenarioService>(scope);
        var baseline = await service.BaselineAsync(p, September, SeptemberEnd, default);
        var a = await service.SaveAsync(p, null, new ScenarioInput("Рост", ScenarioKinds.Combined, null, baseline.Id, 30,
            [new("sales.demand", 30), new("materials.price", 15), new("materials.lead_time", 7)], null), default);
        var b = await service.SaveAsync(p, null, new ScenarioInput("База", ScenarioKinds.Combined, ScenarioPresets.Base, baseline.Id, 30, [], null), default);
        var export = Service<ScenarioExport>(scope);

        var xlsx = await export.FileAsync(p, a.Id, "xlsx", [b.Id], default);
        Assert.EndsWith(".xlsx", xlsx.FileName);
        using (var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(xlsx.Content)))
        {
            var names = book.Worksheets.Select(w => w.Name).ToList();
            foreach (var sheet in new[] { "Сводка", "Изменённые параметры", "Исходные показатели", "График — деньги", "Ограничения", "Риски", "Рекомендации",
                         "Товары", "Регионы", "Сырьё и закупки", "Сравнение сценариев", "Источники и допущения" })
            {
                Assert.Contains(sheet, names);
            }

            Assert.Contains(book.Worksheet("Сводка").CellsUsed(), c => c.GetString() == "Рост");
        }

        var pdf = await export.FileAsync(p, a.Id, "pdf", [], default);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4));
        await Assert.ThrowsAsync<ArgumentException>(() => export.FileAsync(p, a.Id, "docx", [], default));
        var exports = await Service<OneBaseDbContext>(scope).AuditLogs.CountAsync(x => x.Action == "analytik.scenario.export" && x.EntityId == a.Id.ToString());
        Assert.Equal(2, exports);
    }

    [PostgresFact]
    public async Task Draft_baselines_are_purged_after_two_days_but_saved_ones_stay()
    {
        var seed = await SeedAsync();
        var p = Principal(seed.Owner, Director);
        using var scope = fixture.Services.CreateScope();
        var service = Service<ScenarioService>(scope);
        var draft = await service.BaselineAsync(p, September, SeptemberEnd, default);
        var kept = await service.BaselineAsync(p, September, new DateOnly(2026, 9, 29), default);
        await service.SaveAsync(p, null, new ScenarioInput("Сохранён", ScenarioKinds.Sales, null, kept.Id, 30, [], null), default);
        var db = Service<OneBaseDbContext>(scope);
        var old = DateTimeOffset.UtcNow.AddDays(-3);
        await db.Set<AnalytikScenarioBaseline>().Where(x => x.Id == draft.Id || x.Id == kept.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.CreatedAt, old));

        var purged = await Service<AnalytikJobs>(scope).PurgeDraftBaselinesAsync(default);

        Assert.True(purged >= 1);
        Assert.False(await db.Set<AnalytikScenarioBaseline>().AnyAsync(x => x.Id == draft.Id));
        Assert.True(await db.Set<AnalytikScenarioBaseline>().AnyAsync(x => x.Id == kept.Id));
    }

    private async Task AddOrderAsync(long id, long branch, long product, decimal kg, DateOnly date)
    {
        using var scope = fixture.Services.CreateScope();
        var db = Service<OneBaseDbContext>(scope);
        db.LinkoOrders.Add(new LinkoOrder
        {
            Id = id, CreatedAt = date.ToDateTime(new TimeOnly(8, 0)), CreatedDate = date, AcceptedAt = date.ToDateTime(new TimeOnly(9, 0)), AcceptedDate = date,
            Status = "delivered", MarketId = 7000 + id % 1000, BranchId = branch, BranchName = "Бухоро", AgentId = 77, TotalWeight = kg, TotalPrice = kg * 30_000,
        });
        db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = product, Amount = kg, Price = 30_000, TotalPrice = kg * 30_000, TotalWeight = kg });
        await db.SaveChangesAsync();
        Service<OneBase.Application.Sales.SalesCacheSignal>(scope).InvalidateHistory();
    }
}

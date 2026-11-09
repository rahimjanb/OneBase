using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace OneBase.AI.Tests;

/// <summary>
/// AI снабжения на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается) и сценарной модели — без платных вызовов: 12 инструментов
/// только читают, описаны и выданы Supply AI; на закупках сахара считаются рост цены к базе, сумма сверх базы, переплата против лучшей цены и
/// возможные причины; расходы к прошлому периоду, просрочка поставки, слабый поставщик, потребности для одного заказа, ниже минимума «в закупке»,
/// сравнение предложений, KPI снабженца; права — как на страницах (сотрудник — свои заявки, кладовщик — без цен, без прав — ничего); Supply AI
/// отвечает по результату инструмента, а в модель уходит выборка, а не база.
/// </summary>
public class ProcurementAiDbTests(ConsultantDb fixture, ITestOutputHelper output) : IClassFixture<ConsultantDb>
{
    internal static readonly string[] ToolNames =
    [
        "get_procurement_overview", "get_purchase_requests", "get_pending_approvals", "get_purchase_orders", "get_overdue_deliveries", "get_warehouse_stock",
        "get_supplier_history", "compare_supplier_offers", "get_procurement_expenses", "get_procurement_employee_kpi", "get_low_stock_items", "get_purchase_price_history",
    ];

    private sealed record Org(User Employee, User Other, User Supply, User Director, User Keeper, Guid Parts, Guid SupplierA, Guid SupplierB, string Tag)
    {
        public string NameA => $"ООО Альфа {Tag}";
        public string NameB => $"ООО Бета {Tag}";
    }

    private static JsonElement Args(object? value = null) => JsonSerializer.SerializeToElement(value ?? new { });

    private static T Get<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<ToolResult> Run(User user, string tool, object? args = null)
    {
        using var scope = fixture.Services.CreateScope();
        return await Get<ToolExecutor>(scope.ServiceProvider).ExecuteAsync(new ToolContext("supply", user.Id, 0), tool, Args(args));
    }

    private async Task<JsonElement> Data(User user, string tool, object? args = null)
    {
        var result = await Run(user, tool, args);
        Assert.True(result.Success, $"{tool}: {result.Content}");
        return JsonDocument.Parse(result.Content).RootElement.Clone();
    }

    private async Task<T> As<T>(User user, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var granted = await Get<IUserPermissions>(sp).GetAsync(user.Id);
        return await act(sp, await Get<ProcurementRequestService>(sp).ContextAsync(user.Id, granted, CancellationToken.None));
    }

    private Task As(User user, Func<IServiceProvider, ProcurementContext, Task> act) => As(user, async (sp, c) =>
    {
        await act(sp, c);
        return true;
    });

    private async Task<Org> SeedAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        Org org;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = Get<OneBaseDbContext>(scope.ServiceProvider);
            await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
            var supplyDept = await db.Departments.FirstOrDefaultAsync(d => d.Code == "supply");
            if (supplyDept is null)
            {
                supplyDept = new Department { Code = "supply", Name = "Снабжение" };
                db.Departments.Add(supplyDept);
            }

            var dept = new Department { Code = $"ai-{tag}", Name = $"Производство {tag}" };
            Role Role(string name, params string[] codes)
            {
                var role = new Role { Name = name };
                foreach (var code in codes)
                {
                    role.Permissions.Add(new RolePermission { Code = code });
                }

                return role;
            }

            var director = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == SystemRoles.Director);
            if (director is null)
            {
                director = new Role { Name = SystemRoles.Director };
                db.Roles.Add(director);
            }

            foreach (var code in new[] { Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings }.Where(c => director.Permissions.All(p => p.Code != c)))
            {
                director.Permissions.Add(new RolePermission { Code = code });
            }

            var employee = Role($"Сотрудник {tag}", Permissions.ProcurementRequest);
            var supply = Role($"Снабжение {tag}", Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage);
            var keeper = Role($"Кладовщик {tag}", Permissions.ProcurementRequest, Permissions.ProcurementWarehouse);
            User Person(string login, string name, Department department, Role role)
            {
                var user = new User { Login = $"{login}-{tag}", FullName = name, Department = department };
                user.Roles.Add(new UserRole { Role = role });
                return user;
            }

            org = new Org(Person("ai-emp", $"Технолог {tag}", dept, employee), Person("ai-oth", $"Чужой {tag}", supplyDept, employee),
                Person("ai-sup", $"Снабженец {tag}", supplyDept, supply), Person("ai-dir", $"Директор {tag}", dept, director),
                Person("ai-kpr", $"Кладовщик {tag}", supplyDept, keeper), Guid.Empty, Guid.Empty, Guid.Empty, tag);
            db.Users.AddRange(org.Employee, org.Other, org.Supply, org.Director, org.Keeper);
            await db.SaveChangesAsync();
            org = org with { Parts = await db.ProcurementCategories.Where(c => c.Code == "components").Select(c => c.Id).SingleAsync() };
            // Маршрут: у отдела заявителя нет руководителя — заявка сразу к финансисту (здесь им назначен директор), директор маршрута — роль, лимит 5 млн.
            var route = await db.ProcurementSettings.FirstOrDefaultAsync() ?? db.ProcurementSettings.Add(new ProcurementSettings()).Entity;
            (route.FinanceApproverId, route.DirectorApproverId, route.DirectorLimit) = (org.Director.Id, null, ProcurementSettings.DefaultDirectorLimit);
            await db.SaveChangesAsync();
        }

        ProcurementSupplierInput Supplier(string name) => new(name, null, null, null, null, null, null, null, null, null, 2, null, [org.Parts]);
        var a = await As(org.Supply, (sp, c) => Get<ProcurementSupplierService>(sp).CreateAsync(c, Supplier(org.NameA), default));
        var b = await As(org.Supply, (sp, c) => Get<ProcurementSupplierService>(sp).CreateAsync(c, Supplier(org.NameB), default));
        return org with { SupplierA = a, SupplierB = b };
    }

    /// <summary>Заявка от технолога → финансист (здесь — директор) согласует. Возвращает заявку, её номер и позицию.</summary>
    private async Task<(Guid Request, string Number, Guid Item)> ApprovedAsync(Org org, string name, decimal quantity, decimal estimate,
        ProcurementPriority priority = ProcurementPriority.Normal, Guid? itemId = null)
    {
        var request = await As(org.Employee, (sp, c) => Get<ProcurementRequestService>(sp).CreateAsync(c, new ProcurementRequestInput($"Закупка: {name}", null, priority,
            FieldClock.Today.AddDays(10), "Производство", null, [new(null, name, org.Parts, "пачка", quantity, estimate, ItemId: itemId)], Submit: true), default));
        await As(org.Director, (sp, c) => Get<ProcurementRequestService>(sp).ApproveAsync(c, request, "Согласовано", default));
        var detail = await As(org.Supply, (sp, c) => Get<ProcurementRequestService>(sp).GetAsync(c, request, default));
        return (request, detail.Number, detail.Items[0].Id);
    }

    /// <summary>Закупка: утверждённая заявка → заказ поставщику по цене с НДС → «заказан»; дата утверждения заказа — daysAgo дней назад.</summary>
    private async Task<(Guid Order, string Number)> BuyAsync(Org org, string name, decimal quantity, decimal price, Guid supplier, int daysAgo,
        ProcurementPriority priority = ProcurementPriority.Normal, int? expectedInDays = null)
    {
        var (_, _, item) = await ApprovedAsync(org, name, quantity, price + 1_000, priority);
        var order = await As(org.Supply, (sp, c) => Get<ProcurementOrderService>(sp).CreateAsync(c, new ProcurementOrderInput(supplier, null, org.Supply.Id,
            FieldClock.Today.AddDays(3), null, 0, 12, true, 0, null, [new ProcurementOrderLineInput(item, quantity, price)], Submit: true), default));
        await As(org.Supply, (sp, c) => Get<ProcurementOrderService>(sp).MarkOrderedAsync(c, order, null, default));
        using var scope = fixture.Services.CreateScope();
        var db = Get<OneBaseDbContext>(scope.ServiceProvider);
        var row = await db.ProcurementPurchaseOrders.SingleAsync(o => o.Id == order);
        row.ApprovedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        if (expectedInDays is { } days)
        {
            row.ExpectedDate = FieldClock.Today.AddDays(days);
        }

        await db.SaveChangesAsync();
        return (order, row.Number);
    }

    [PostgresFact]
    public async Task Procurement_tools_are_read_only_described_and_granted_to_supply_ai()
    {
        using var scope = fixture.Services.CreateScope();
        var registry = Get<IToolRegistry>(scope.ServiceProvider);

        Assert.Equal(ToolNames.Order(), registry.All.Where(t => t.Source == KnowledgeSources.Procurement).Select(t => t.Name).Order());
        foreach (var name in ToolNames)
        {
            var tool = Assert.IsAssignableFrom<DataTool>(registry.Find(name));
            Assert.False(tool.IsCritical);
            Assert.True(tool.Description.Length > 200, $"{name}: описание слишком короткое");
            var schema = tool.InputSchema;
            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.False(schema.TryGetProperty("required", out _), $"{name}: аргументы необязательны");
            foreach (var p in schema.GetProperty("properties").EnumerateObject())
            {
                Assert.True(p.Value.GetProperty("type").GetString() is "string" or "integer", $"{name}.{p.Name}");
                Assert.False(string.IsNullOrWhiteSpace(p.Value.GetProperty("description").GetString()), $"{name}.{p.Name}: нет описания");
                if (p.Value.TryGetProperty("enum", out var values))
                {
                    Assert.All(values.EnumerateArray(), v => Assert.Matches("^[a-z_]+$", v.GetString()!));
                }
            }
        }

        // Supply AI получает источник и инструменты снабжения при запуске (AiSeeder) — без миграции.
        var db = Get<OneBaseDbContext>(scope.ServiceProvider);
        var grants = await db.AgentToolGrants.Where(g => g.AgentCode == "supply" && g.Enabled).Select(g => g.ToolName).ToListAsync();
        Assert.All(ToolNames, name => Assert.Contains(name, grants));
        Assert.True(await db.AiAgentKnowledgeSources.AnyAsync(s => s.AgentCode == "supply" && s.SourceCode == KnowledgeSources.Procurement));
        Assert.DoesNotContain("Заказов поставщикам, закупочных цен и сроков поставки нет", AgentDefaults.Find("supply")!.Prompt);
        Assert.True(AgentDefaults.IsOutdatedPrompt("supply", AgentDefaults.OutdatedPrompts["supply"][0]));
    }

    [PostgresFact]
    public async Task Price_rise_overpay_and_recommendation_signals_come_from_purchases()
    {
        var org = await SeedAsync();
        var sugar = $"Сахар {org.Tag}";
        // База — покупка до периода (120 дней назад) у Альфы по 10 000; в периоде — срочно у Альфы по 12 000, потом у Беты по 11 000 (поставка просрочена).
        await BuyAsync(org, sugar, 100, 10_000, org.SupplierA, 120);
        await BuyAsync(org, sugar, 100, 12_000, org.SupplierA, 30, ProcurementPriority.Urgent);
        var late = await BuyAsync(org, sugar, 50, 11_000, org.SupplierB, 10, expectedInDays: -2);

        var prices = await Data(org.Supply, "get_purchase_price_history", new { category = "Комплектующие", search = org.Tag, min_change_percent = 10 });
        var item = Assert.Single(prices.GetProperty("items").EnumerateArray());
        Assert.Equal(10m, item.GetProperty("changePercent").GetDecimal());
        Assert.Equal(10_000m, item.GetProperty("baseline").GetProperty("price").GetDecimal());
        Assert.True(item.GetProperty("baseline").GetProperty("beforePeriod").GetBoolean());
        Assert.Equal(250_000m, item.GetProperty("extraCost").GetDecimal()); // (12 000 − 10 000) × 100 + (11 000 − 10 000) × 50
        Assert.Equal(100_000m, item.GetProperty("overpay").GetDecimal()); // (12 000 − 11 000) × 100 против лучшей цены периода
        Assert.Equal(org.NameB, item.GetProperty("cheapestSupplier").GetString());
        var reasons = string.Join("\n", item.GetProperty("possibleReasons").EnumerateArray().Select(r => r.GetString()));
        Assert.Contains("сменился поставщик", reasons);
        Assert.Contains("срочные", reasons);
        Assert.Contains("без сравнения коммерческих предложений: 2 из 2", reasons);
        Assert.Equal("get_purchase_price_history", prices.GetProperty("_meta").GetProperty("tool").GetString());
        Assert.Empty((await Data(org.Supply, "get_purchase_price_history", new { search = org.Tag, min_change_percent = 11 })).GetProperty("items").EnumerateArray());

        // Расходы у Альфы: в 90 днях — 1 200 000, в 90 днях до них — 1 000 000 (+20%).
        var today = FieldClock.Today;
        var spend = await Data(org.Director, "get_procurement_expenses", new { from = $"{today.AddDays(-89):yyyy-MM-dd}", to = $"{today:yyyy-MM-dd}", supplier = org.NameA });
        Assert.Equal(1_200_000m, spend.GetProperty("total").GetDecimal());
        Assert.Equal(1_000_000m, spend.GetProperty("previous").GetProperty("total").GetDecimal());
        Assert.Equal(20m, spend.GetProperty("previous").GetProperty("changePercent").GetDecimal());

        // Просрочка поставки Беты — на 2 дня; Бета в рейтинге — слабый поставщик с причиной.
        var overdue = await Data(org.Keeper, "get_overdue_deliveries");
        var row = Assert.Single(overdue.GetProperty("overdue").EnumerateArray(), o => o.GetProperty("number").GetString() == late.Number);
        Assert.Equal(2, row.GetProperty("daysOverdue").GetInt32());
        var suppliers = await Data(org.Director, "get_supplier_history", new { limit = 50 });
        var beta = Assert.Single(suppliers.GetProperty("items").EnumerateArray(), s => s.GetProperty("name").GetString() == org.NameB);
        Assert.Contains(beta.GetProperty("weakBecause").EnumerateArray(), w => w.GetString() == "просрочено сейчас: 1");

        // KPI снабженца: три заказа в работе, один просрочен.
        var kpi = await Data(org.Director, "get_procurement_employee_kpi", new { employee = org.Supply.FullName });
        var staff = Assert.Single(kpi.GetProperty("staff").EnumerateArray());
        Assert.Equal((3, 1), (staff.GetProperty("activeOrders").GetInt32(), staff.GetProperty("overdueOrders").GetInt32()));

        // Две утверждённые заявки «Сырья» без заказа — можно заказать одним заказом; мука есть на складе снабжения ниже минимума и уже в закупке.
        var sku = $"AI-{org.Tag}";
        var card = await As(org.Keeper, (sp, c) => Get<ProcurementStockService>(sp).CreateItemAsync(c,
            new ProcurementCatalogInput(sku, $"Мука {org.Tag}", org.Parts, "пачка", null, null, 10), default));
        var flour = await ApprovedAsync(org, $"Мука {org.Tag}", 40, 6_000, itemId: card);
        var salt = await ApprovedAsync(org, $"Соль {org.Tag}", 10, 6_000);
        var pending = await Data(org.Supply, "get_pending_approvals", new { limit = 50 });
        Assert.Contains(pending.GetProperty("canOrderTogether").EnumerateArray(), g =>
            g.GetProperty("requestNumbers").EnumerateArray().Select(n => n.GetString()).ToHashSet().IsSupersetOf([flour.Number, salt.Number]));
        var low = await Data(org.Keeper, "get_low_stock_items", new { limit = 50 });
        var lowItem = Assert.Single(low.GetProperty("items").EnumerateArray(), i => i.GetProperty("sku").GetString() == sku);
        Assert.Equal(10m, lowItem.GetProperty("shortToMinimum").GetDecimal());
        Assert.Equal([flour.Number], lowItem.GetProperty("inPurchase").EnumerateArray().Select(n => n.GetString()!));

        // Предложения по соли: Бета дешевле (4 500 против 5 000) — экономия против цены заявки (6 000 − 4 500) × 10.
        foreach (var (supplier, price) in new[] { (org.SupplierA, 5_000m), (org.SupplierB, 4_500m) })
        {
            await As(org.Supply, (sp, c) => Get<ProcurementOrderService>(sp).CreateOfferAsync(c, salt.Request,
                new ProcurementOfferInput(supplier, null, 3, 0, 12, true, null, null, null, null, [new ProcurementOfferLineInput(salt.Item, price)]), default));
        }

        var offers = await Data(org.Director, "compare_supplier_offers", new { request = salt.Number });
        var line = Assert.Single(offers.GetProperty("items").EnumerateArray());
        Assert.Equal((4_500m, org.NameB, 15_000m), (line.GetProperty("bestPrice").GetDecimal(), line.GetProperty("bestSupplier").GetString(), line.GetProperty("savingVsRequest").GetDecimal()));
        Assert.Equal(2, offers.GetProperty("offers").GetArrayLength());

        // Обзор директора: всё видно, сигналы указывают на рост цен и объединение заказов.
        var overview = await Data(org.Director, "get_procurement_overview");
        Assert.Equal("все заявки компании", overview.GetProperty("access").GetProperty("requests").GetString());
        Assert.True(overview.GetProperty("signals").GetProperty("priceRiseItems").GetInt32() >= 1);
        Assert.True(overview.GetProperty("signals").GetProperty("consolidationGroups").GetInt32() >= 1);
    }

    [PostgresFact]
    public async Task Tools_follow_the_users_access_like_the_pages()
    {
        var org = await SeedAsync();
        var mine = await ApprovedAsync(org, $"Картон {org.Tag}", 5, 1_000);
        await As(org.Other, (sp, c) => Get<ProcurementRequestService>(sp).CreateAsync(c, new ProcurementRequestInput($"Чужая заявка {org.Tag}", null, ProcurementPriority.Normal,
            null, "Склад", null, [new(null, $"Скотч {org.Tag}", org.Parts, "пачка", 1, 100)], Submit: true), default));

        // Сотрудник: обзор и свои заявки — да; чужой заявки не видно; заказы, цены, расходы — нет доступа.
        var overview = await Data(org.Employee, "get_procurement_overview");
        Assert.Equal("только свои заявки", overview.GetProperty("access").GetProperty("requests").GetString());
        Assert.False(overview.TryGetProperty("signals", out _));
        var requests = await Data(org.Employee, "get_purchase_requests", new { search = org.Tag });
        Assert.Equal([mine.Number], requests.GetProperty("requests").EnumerateArray().Select(r => r.GetProperty("number").GetString()!));
        foreach (var tool in new[] { "get_purchase_price_history", "get_procurement_expenses", "get_purchase_orders", "get_low_stock_items", "compare_supplier_offers" })
        {
            var denied = await Run(org.Employee, tool);
            Assert.False(denied.Success, tool);
            Assert.Equal(ToolFailures.Forbidden, denied.ErrorKind);
        }

        // Кладовщик: заказы, склад, поставщики — да; цены, расходы, KPI — нет; рейтинг поставщиков — реестр без разбора слабых.
        Assert.True((await Run(org.Keeper, "get_purchase_orders")).Success);
        Assert.True((await Run(org.Keeper, "get_warehouse_stock")).Success);
        foreach (var tool in new[] { "get_purchase_price_history", "get_procurement_expenses", "get_procurement_employee_kpi" })
        {
            Assert.Equal(ToolFailures.Forbidden, (await Run(org.Keeper, tool)).ErrorKind);
        }

        var registry = await Data(org.Keeper, "get_supplier_history");
        Assert.Contains("видят снабжение и директор", registry.GetProperty("note").GetString());
        var card = await Data(org.Keeper, "get_supplier_history", new { supplier = org.NameA });
        Assert.Equal(org.NameA, card.GetProperty("name").GetString());
        Assert.False(card.TryGetProperty("bankAccount", out _));

        // Директор (здесь — и финансист маршрута) видит заявку у финансиста; без прав снабжения — ни одного инструмента.
        await As(org.Employee, (sp, c) => Get<ProcurementRequestService>(sp).CreateAsync(c, new ProcurementRequestInput($"На согласование {org.Tag}", null, ProcurementPriority.Normal,
            null, "Производство", null, [new(null, $"Клей {org.Tag}", org.Parts, "пачка", 1, 100)], Submit: true), default));
        var pending = await Data(org.Director, "get_pending_approvals", new { limit = 50 });
        Assert.True(pending.GetProperty("financeApprovalRequests").GetProperty("total").GetInt32() >= 1);
        using (var scope = fixture.Services.CreateScope())
        {
            var none = await Get<ToolExecutor>(scope.ServiceProvider).ExecuteAsync(new ToolContext("supply", fixture.NoAccessId, 0), "get_procurement_overview", Args());
            Assert.Equal(ToolFailures.Forbidden, none.ErrorKind);
            var allowed = await Get<ToolExecutor>(scope.ServiceProvider).GetAllowedToolsAsync("supply", org.Employee.Id);
            Assert.Equal(["get_pending_approvals", "get_procurement_overview", "get_purchase_requests"],
                allowed.Where(t => t.Source == KnowledgeSources.Procurement).Select(t => t.Name).Order());
        }
    }

    [PostgresFact]
    public async Task Supply_ai_answers_a_price_question_from_the_tool_result_not_the_database()
    {
        var org = await SeedAsync();
        await BuyAsync(org, $"Сахар {org.Tag}", 100, 10_000, org.SupplierA, 120);
        await BuyAsync(org, $"Сахар {org.Tag}", 100, 12_000, org.SupplierB, 20);

        var model = fixture.Model.Reset();
        model.Then([new LlmToolCall("1", "get_purchase_price_history", Args(new { category = "Комплектующие", search = org.Tag, min_change_percent = 10 }))])
            .Then($$"""
                {"agent":"supply","status":"completed","summary":"Сахар {{org.Tag}} подорожал на 20%: с 10 000 до 12 000 сум.","findings":["Сахар {{org.Tag}}: цена выросла с 10 000 до 12 000 сум (+20%), сверх базы — 200 000 сум"],"metrics":[],"problems":[],"recommendations":["Запросить предложения у нескольких поставщиков"],"data_sources":[]}
                """);
        using var scope = fixture.Services.CreateScope();
        var run = await Get<AgentRunner>(scope.ServiceProvider).RunAsync(
            new AgentTask("supply", "Сравни закупки сырья за последние три месяца: что подорожало больше чем на 10%?", org.Supply.Id), null, CancellationToken.None);

        Assert.Equal("completed", run.Result.Status);
        Assert.Contains("get_purchase_price_history", run.Result.ToolsUsed);
        var result = Assert.Single(model.Calls[1], m => m.Role == LlmRole.Tool).Content;
        var json = JsonDocument.Parse(result[result.IndexOf('{')..]).RootElement;
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal((20m, 200_000m), (item.GetProperty("changePercent").GetDecimal(), item.GetProperty("extraCost").GetDecimal()));

        // Что уходит в модель: промпт сотрудника с подсказками снабжения и описания инструментов — один раз, результат — выборка.
        var system = string.Join("\n", model.Calls[0].Where(m => m.Role == LlmRole.System).Select(m => m.Content));
        Assert.Contains("get_procurement_overview", system);
        var tools = await Get<ToolExecutor>(scope.ServiceProvider).GetAllowedToolsAsync("supply", org.Supply.Id);
        var definitions = tools.Where(t => t.Source == KnowledgeSources.Procurement).Sum(t => t.Name.Length + t.Description.Length + t.InputSchema.GetRawText().Length);
        var registry = Get<IToolRegistry>(scope.ServiceProvider).All.Where(t => t is DataTool).ToList();
        var others = registry.Where(t => t.Source != KnowledgeSources.Procurement).ToList();
        output.WriteLine($"Промпт Supply AI: {system.Length} символов; описания 12 инструментов снабжения: {definitions} символов; результат инструмента: {result.Length} символов.");
        output.WriteLine($"Для сравнения: остальные {others.Count} инструментов данных — {others.Sum(t => t.Name.Length + t.Description.Length + t.InputSchema.GetRawText().Length)} символов.");
        Assert.True(result.Length < 6_000, $"результат инструмента — {result.Length} символов");
        Assert.True(definitions < 24_000, $"описания инструментов — {definitions} символов");
    }
}

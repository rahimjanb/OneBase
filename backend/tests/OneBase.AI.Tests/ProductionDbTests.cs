using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Integrations;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Производство и переходный период на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): версии рецептур (черновик правится,
/// действующая — нет, одна действующая и один черновик на код) и потребность по нормам; задание на выпуск — выдача только годных партий
/// (FEFO), сверх потребности — с причиной, возврат в ту же партию и не больше выданного, отмена только без сырья на руках, закрытие — норма на
/// факт выхода и перерасход; владелец данных склада (таблица / OneBase); импорт каталога; загрузка журнала из листа Google — тот же разбор,
/// повтор не проводит строки; обзор и отчёты производства. После операций — инвариант «позиция = сумма партий».
/// </summary>
public class ProductionDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Technologist = [Permissions.ProcurementRequest, Permissions.ProductionRecipes, Permissions.ProductionRead];
    private static readonly string[] Master = [Permissions.ProcurementRequest, Permissions.ProductionRead, Permissions.ProductionManage];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Boss = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] Employee = [Permissions.ProcurementRequest];

    private sealed record Org(string S, Department Shop, User Technologist, User Master, User Keeper, User Buyer, User Director, User Employee, Guid Raw, Guid Central);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var shop = new Department { Code = $"pd-{s}", Name = $"Цех {s}" };
        Role RoleWith(string name, params string[] codes)
        {
            var role = new Role { Name = name };
            foreach (var code in codes)
            {
                role.Permissions.Add(new RolePermission { Code = code });
            }

            return role;
        }

        User Person(string login, string name, Role? role = null)
        {
            var user = new User { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = shop };
            if (role is not null)
            {
                user.Roles.Add(new UserRole { Role = role });
            }

            return user;
        }

        var org = new Org(s, shop, Person("pd-tech", "Технолог"), Person("pd-mst", "Мастер", RoleWith($"Производство {s}", Permissions.ProductionManage)),
            Person("pd-kpr", "Кладовщик", RoleWith($"Кладовщик {s}", Permissions.ProcurementWarehouse)),
            Person("pd-buy", "Снабженец", RoleWith($"Снабжение {s}", Permissions.ProcurementManage)), Person("pd-dir", "Директор"), Person("pd-emp", "Сотрудник"), Guid.Empty,
            Guid.Empty);
        db.Users.AddRange(org.Technologist, org.Master, org.Keeper, org.Buyer, org.Director, org.Employee);
        await db.SaveChangesAsync();
        var raw = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync();
        var central = await db.ProcurementWarehouses.Where(w => w.IsDefault).Select(w => w.Id).SingleAsync();
        return org with { Raw = raw, Central = central };
    }

    private async Task<T> As<T>(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider, ctx);
    }

    private Task Do(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task> act) => As(user, permissions, async (sp, c) =>
    {
        await act(sp, c);
        return true;
    });

    private static ProductionRecipeService Recipes(IServiceProvider sp) => sp.GetRequiredService<ProductionRecipeService>();
    private static ProductionOrderService Orders(IServiceProvider sp) => sp.GetRequiredService<ProductionOrderService>();
    private static ProcurementStockService Stock(IServiceProvider sp) => sp.GetRequiredService<ProcurementStockService>();

    private Task<Guid> ItemAsync(Org org, string name, bool batches, ProcurementQualityControl control = ProcurementQualityControl.None, string sku = "") =>
        As(org.Keeper, Keeper, (sp, c) => Stock(sp).CreateItemAsync(c, new ProcurementCatalogInput(sku.Length > 0 ? sku : $"PD-{Guid.NewGuid():N}"[..14], $"{name} {org.S}", org.Raw,
            "кг", null, null, 0, TracksBatches: batches, ShelfLifeDays: 365, QualityControl: control), default));

    private Task ReceiveAsync(Org org, Guid item, decimal qty, params ProcurementBatchInput[] batches) =>
        As(org.Keeper, Keeper, (sp, c) => Stock(sp).ReceiptAsync(c, new ProcurementStockOperationInput(
            [new(item, qty, 10_000, Batches: batches.Length > 0 ? batches : null)], "Приход сырья"), default));

    private async Task InvariantAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var items = await db.ProcurementStockBalances.Where(b => b.Item!.TracksBatches).Select(b => new { b.WarehouseId, b.ItemId, b.OnHand }).ToListAsync();
        var batches = await db.ProcurementBatchBalances.GroupBy(b => new { b.WarehouseId, b.ItemId }).Select(g => new { g.Key.WarehouseId, g.Key.ItemId, Qty = g.Sum(b => b.OnHand) })
            .ToListAsync();
        foreach (var i in items)
        {
            Assert.Equal(i.OnHand, batches.FirstOrDefault(b => b.WarehouseId == i.WarehouseId && b.ItemId == i.ItemId)?.Qty ?? 0);
        }
    }

    private static ProductionRecipeInput Recipe(string code, string name, params (Guid Item, decimal Qty, decimal Loss)[] lines) =>
        new(code, name, null, $"{name} (выход)", "кг", 100, null, lines.Select(l => new ProductionRecipeLineInput(l.Item, l.Qty, l.Loss)).ToList());

    [PostgresFact]
    public async Task Recipe_versions_draft_is_edited_active_is_copied_and_requirement_matches_manual_calculation()
    {
        var org = await SeedAsync();
        var flour = await ItemAsync(org, "Мука", true);
        var sugar = await ItemAsync(org, "Сахар", false);
        await ReceiveAsync(org, flour, 30, new ProcurementBatchInput("F-1", null, FieldClock.Today.AddDays(200), 30));
        await ReceiveAsync(org, sugar, 5);
        var code = $"BR-{org.S}";

        // Правит только технолог; черновик v1 правится свободно.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Master, Master, (sp, c) => Recipes(sp).CreateAsync(c, Recipe(code, "Печенье", (flour, 60, 0)), default)));
        var v1 = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).CreateAsync(c, Recipe(code, "Печенье", (flour, 60, 0)), default));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Technologist, Technologist, (sp, c) => Recipes(sp).CreateAsync(c, Recipe(code, "Ещё", (flour, 1, 0)), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Do(org.Technologist, Technologist, (sp, c) =>
            Recipes(sp).UpdateAsync(c, v1, Recipe(code, "Печенье", (flour, 60, 0), (flour, 1, 0)), default))); // повтор позиции
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).UpdateAsync(c, v1, Recipe(code, "Печенье", (flour, 60, 2), (sugar, 12.5m, 0)), default));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).ActivateAsync(c, v1, default));

        // Действующая не правится; новая версия — копия в черновик (один на код), её ввод в действие отправляет v1 в архив.
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Technologist, Technologist, (sp, c) =>
            Recipes(sp).UpdateAsync(c, v1, Recipe(code, "Печенье", (flour, 50, 0)), default)));
        var v2 = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).NewVersionAsync(c, v1, default));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Technologist, Technologist, (sp, c) => Recipes(sp).NewVersionAsync(c, v1, default)));
        var draft = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, v2, default));
        Assert.Equal((2, ProductionRecipeStatus.Draft, 2), (draft.VersionNo, draft.Status, draft.Lines.Count));
        Assert.Equal(60m, draft.Lines.Single(l => l.ItemId == flour).Quantity);
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).UpdateAsync(c, v2, Recipe(code, "Печенье", (flour, 55, 2), (sugar, 12.5m, 0)) with { Version = draft.Version }, default));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).ActivateAsync(c, v2, default));
        var archived = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, v1, default));
        Assert.Equal(ProductionRecipeStatus.Archived, archived.Status);
        Assert.Equal([ProductionRecipeStatus.Active, ProductionRecipeStatus.Archived], archived.Versions.Select(v => v.Status));
        var list = await As(org.Master, Master, (sp, c) => Recipes(sp).ListAsync(c, code, null, default));
        Assert.Equal((v2, 2), (list.Items.Single().Id, list.Items.Single().Versions));

        // Потребность на 250 кг: мука 55 × 2,5 × 1,02 = 140,25; сахар 12,5 × 2,5 = 31,25. Доступно: мука 30, сахар 5.
        var need = await As(org.Master, Master, (sp, c) => Recipes(sp).RequirementAsync(c, v2, 250, org.Central, default));
        Assert.Equal([(140.25m, 30m, 110.25m), (31.25m, 5m, 26.25m)], need.Lines.Select(l => (l.Required, l.Available, l.Shortage)));
        Assert.Equal(140.25m, ProductionRules.Requirement(55, 250, 100, 2));
    }

    [PostgresFact]
    public async Task Production_order_issues_only_usable_batches_returns_to_the_same_batch_and_closes_with_the_norm()
    {
        var org = await SeedAsync();
        var flour = await ItemAsync(org, "Мука", true);
        var cocoa = await ItemAsync(org, "Какао", true, ProcurementQualityControl.Documents);
        var salt = await ItemAsync(org, "Соль", false);
        var today = FieldClock.Today;
        // Мука: две партии в сроке (FEFO — сначала F-B со сроком раньше) и просроченная F-X. Какао — на контроле. Соль без партий.
        await ReceiveAsync(org, flour, 100, new ProcurementBatchInput("F-A", null, today.AddDays(300), 60), new ProcurementBatchInput("F-B", null, today.AddDays(90), 40));
        await ReceiveAsync(org, cocoa, 20, new ProcurementBatchInput("C-1", null, today.AddDays(200), 20));
        await ReceiveAsync(org, salt, 10);
        var code = $"CK-{org.S}";
        var recipe = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).CreateAsync(c, Recipe(code, "Кекс", (flour, 50, 0), (cocoa, 5, 0), (salt, 1, 0)), default));

        // Задание — только по действующей версии и только производством.
        Task<Guid> Create(Guid r) => As(org.Master, Master, (sp, c) => Orders(sp).CreateAsync(c,
            new ProductionOrderInput(r, org.Shop.Id, org.Central, 100, today, org.Master.Id, null, Guid.NewGuid()), default));
        await Assert.ThrowsAsync<FieldConflictException>(() => Create(recipe));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).ActivateAsync(c, recipe, default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).CreateAsync(c,
            new ProductionOrderInput(recipe, org.Shop.Id, org.Central, 100, today, null, null), default)));
        var id = await Create(recipe);
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, id, new([new(flour, 10)]), default))); // не выпущено
        await Do(org.Master, Master, (sp, c) => Orders(sp).ReleaseAsync(c, id, default));
        Assert.True(await NotifiedAsync(org.Buyer, "Не хватает сырья под задание")); // какао на контроле — годного 0

        // Выдача: мастер не выдаёт; какао на контроле не выдать; мука — FEFO (сначала F-B), больше потребности — только с причиной.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Master, Master, (sp, c) => Orders(sp).IssueAsync(c, id, new([new(flour, 10)]), default)));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, id, new([new(cocoa, 5)]), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, id, new([new(flour, 55)]), default)));
        var key = Guid.NewGuid();
        var issue = await As(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, id, new([new(flour, 55), new(salt, 1)], "Пролили часть", "Мастер смены", key), default));
        var again = await As(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, id, new([new(flour, 55), new(salt, 1)], "Пролили часть", "Мастер смены", key), default));
        Assert.Equal(issue.DocumentId, again.DocumentId); // повтор формы не проводит второй раз
        Assert.StartsWith("PIS-", issue.Number);
        var o = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, id, default));
        Assert.Equal(ProductionOrderStatus.InProgress, o.Status);
        Assert.Equal([("F-B", 40m), ("F-A", 15m)], o.Holdings.Where(h => h.ItemId == flour).OrderBy(h => h.ExpiresOn).Select(h => (h.SupplierBatchNo!, h.Issued)));
        Assert.Equal((55m, 0m), (o.Lines.Single(l => l.ItemId == flour).Issued, o.Lines.Single(l => l.ItemId == flour).Left));

        // Возврат — в ту же партию и не больше выданного; позиции с партиями — с партией. Отмена с сырьём на руках — нельзя.
        var fb = o.Holdings.Single(h => h.SupplierBatchNo == "F-B").BatchId!.Value;
        var fa = o.Holdings.Single(h => h.SupplierBatchNo == "F-A").BatchId!.Value;
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).ReturnAsync(c, id, new([new(flour, 1)]), default)));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).ReturnAsync(c, id, new([new(flour, 16, fa)]), default)));
        await As(org.Keeper, Keeper, (sp, c) => Orders(sp).ReturnAsync(c, id, new([new(flour, 3, fa)], "Остаток смены"), default));
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Master, Master, (sp, c) => Orders(sp).CancelAsync(c, id, "Передумали", default)));
        await InvariantAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(48m, await db.ProcurementBatchBalances.Where(b => b.BatchId == fa).Select(b => b.OnHand).SingleAsync()); // 60 − 15 + 3
            Assert.Equal(0m, await db.ProcurementBatchBalances.Where(b => b.BatchId == fb).Select(b => b.OnHand).SingleAsync());
            // Выдача и возврат по заданию не сторнируются.
            var docId = await db.ProcurementStockDocuments.Where(d => d.ProductionOrderId == id && d.Kind == ProcurementStockDocumentKind.ProductionIssue).Select(d => d.Id).FirstAsync();
            await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Stock(sp).ReverseAsync(c, docId, new("Ошибка"), default)));
        }

        // Закрытие: выход 90 кг — норма муки 45, расход 52 (+15,6 % > 5 %) — перерасход; соль 0,9 при расходе 1 (+11 %) — тоже.
        await Do(org.Master, Master, (sp, c) => Orders(sp).CompleteAsync(c, id, new(90, "Смена 2"), default));
        o = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, id, default));
        var f = o.Lines.Single(l => l.ItemId == flour);
        Assert.Equal((ProductionOrderStatus.Completed, 90m), (o.Status, o.OutputQty!.Value));
        Assert.Equal((45m, 52m, 7m, 15.6m, true), (f.ActualNorm!.Value, f.Consumed, f.Deviation!.Value, f.DeviationPercent!.Value, f.Overconsumed));
        Assert.False(o.Lines.Single(l => l.ItemId == cocoa).Overconsumed); // не выдавали — расход 0
        Assert.True(await NotifiedAsync(org.Master, "Перерасход сырья по заданию"));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, id, new([new(salt, 1)]), default)));

        // Обзор и отчёты производства: закрытое задание с перерасходом, сверка партий — совпадает.
        var overview = await As(org.Director, Boss, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().OverviewAsync(c, ProcurementFilter.Of(today, today), null, default));
        Assert.True(overview.ClosedOrders >= 1 && overview.Overconsumed >= 2 && overview.OnControl.Count >= 1);
        Assert.Contains(overview.Deviations, d => d.OrderId == id && d.Overconsumed);
        foreach (var kind in ProductionAnalyticsService.Kinds.Select(k => k.Key))
        {
            var report = await As(org.Director, Boss, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().ReportAsync(c, kind, ProcurementFilter.Of(today, today), org.Central, default));
            Assert.NotEmpty(report.Sheets);
        }

        var reconcile = await As(org.Director, Boss, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().ReportAsync(c, "reconcile", ProcurementFilter.Of(today, today), org.Central, default));
        Assert.DoesNotContain(reconcile.Sheets[0].Rows, r => r[^1].GetString() == "расхождение");
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, (sp, c) =>
            sp.GetRequiredService<ProductionAnalyticsService>().OverviewAsync(c, ProcurementFilter.Of(today, today), null, default)));
    }

    [PostgresFact]
    public async Task Warehouse_data_owner_closes_manual_operations_or_the_import_and_a_google_sheet_loads_like_a_file()
    {
        var org = await SeedAsync();
        var sku = $"GS-{org.S}";
        var item = await ItemAsync(org, "Крахмал", false, sku: sku);
        Guid store;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var w = new ProcurementWarehouse { Code = $"gs-{org.S}", Name = $"Склад таблицы {org.S}", Kind = ProcurementWarehouseKind.RawMaterials };
            db.ProcurementWarehouses.Add(w);
            db.DataSourceConnections.Add(new DataSourceConnection { Id = ConnectionId, Name = $"Журнал склада {org.S}", SpreadsheetId = $"sheet-{org.S}", Url = "https://example.invalid" });
            await db.SaveChangesAsync();
            store = w.Id;
        }

        async Task Owner(ProcurementDataOwner owner)
        {
            await Do(org.Director, Boss, async (sp, c) =>
            {
                var view = await sp.GetRequiredService<ProcurementWarehouseService>().ListAsync(c, default);
                var w = view.Warehouses.Single(x => x.Id == store);
                await sp.GetRequiredService<ProcurementWarehouseService>().UpdateAsync(c, store,
                    new ProcurementWarehouseInput(w.Code, w.Name, w.Kind, null, null, true, [], w.Version, owner), default);
            });
        }

        // Склад «в таблице»: ручной приход — нельзя; загрузка листа — можно (тот же разбор, что у файла), повтор не проводит строки.
        await Owner(ProcurementDataOwner.Sheet);
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Stock(sp).ReceiptAsync(c,
            new ProcurementStockOperationInput([new(item, 5, 1000)], "Приход", WarehouseId: store), default)));
        var sheet = new ReportSheet("Журнал", [
            ["Дата", "П/Р", "Артикул", "Наименование", "Ед.", "Количество", "Цех"],
            [FieldClock.Today.AddDays(-2).ToString("dd.MM.yyyy"), "П", sku, $"Крахмал {org.S}", "кг", "40", ""],
            [FieldClock.Today.AddDays(-1).ToString("dd.MM.yyyy"), "Р", sku, $"Крахмал {org.S}", "кг", "15", $"Цех {org.S}"],
        ], 3, 7, false);
        var binding = await As(org.Keeper, Keeper, (sp, c) => Sheets(sp, sheet).SaveAsync(c, null, new ProcurementSheetImportInput(store, ConnectionId, "Журнал", "manual"), default));
        var check = await As(org.Keeper, Keeper, (sp, c) => Sheets(sp, sheet).RunAsync(c, binding, true, default));
        Assert.True(check.DryRun);
        var first = await As(org.Keeper, Keeper, (sp, c) => Sheets(sp, sheet).RunAsync(c, binding, false, default));
        var second = await As(org.Keeper, Keeper, (sp, c) => Sheets(sp, sheet).RunAsync(c, binding, false, default));
        Assert.Equal(0, second.Posted);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(25m, await db.ProcurementStockBalances.Where(b => b.WarehouseId == store && b.ItemId == item).Select(b => b.OnHand).SingleAsync());
            var saved = await db.ProcurementSheetImports.SingleAsync(s => s.Id == binding);
            Assert.Equal("ok", saved.LastStatus);
            Assert.Null(saved.NextRunAt); // кнопкой — без расписания
        }

        Assert.True(first.Posted > 0 || first.Mode == "opening");

        // Склад «в OneBase»: загрузка закрыта, ручные операции — открыты.
        await Owner(ProcurementDataOwner.OneBase);
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Sheets(sp, sheet).RunAsync(c, binding, false, default)));
        await As(org.Keeper, Keeper, (sp, c) => Stock(sp).ReceiptAsync(c, new ProcurementStockOperationInput([new(item, 5, 1000)], "Приход", WarehouseId: store), default));
    }

    private static readonly Guid ConnectionId = Guid.NewGuid();

    /// <summary>Загрузка из «таблицы Google» с подменённым чтением: лист — из теста, без сети.</summary>
    private static ProcurementSheetImportService Sheets(IServiceProvider sp, ReportSheet sheet) =>
        ActivatorUtilities.CreateInstance<ProcurementSheetImportService>(sp, new FakeSheets(sheet));

    private sealed class FakeSheets(ReportSheet sheet) : IStockSheetSource
    {
        public Task<IReadOnlyList<StockSheetConnection>> ConnectionsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StockSheetConnection>>([new StockSheetConnection(ConnectionId, "Журнал склада", ["Журнал"])]);

        public Task<(string Connection, ReportSheet Sheet)> ReadAsync(Guid connectionId, string name, CancellationToken ct) => Task.FromResult(("Журнал склада", sheet));
    }

    [PostgresFact]
    public async Task Catalog_import_checks_without_saving_then_creates_and_updates_by_sku_with_the_form_rules()
    {
        var org = await SeedAsync();
        var existing = await ItemAsync(org, "Ваниль", false, sku: $"CI-OLD-{org.S}");
        await ReceiveAsync(org, existing, 2);
        var csv = string.Join("\n",
            "Артикул;Наименование;Ед.;Срок хранения;Контроль качества;По партиям;Условия хранения",
            $"CI-NEW-{org.S};Патока {org.S};кг;180;документы;да;сухо",
            $"CI-OLD-{org.S};Ваниль {org.S} (новое имя);;;;;",
            $"CI-BAD-{org.S};Без единицы {org.S};;;;;",
            $"CI-OLD-{org.S};Повтор;кг;;;;",
            $"CI-QC-{org.S};Без партий {org.S};кг;;лаборатория;нет;");
        Task<ProcurementCatalogImportResult> Import(bool dryRun) => As(org.Keeper, Keeper, async (sp, c) =>
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            return await sp.GetRequiredService<ProcurementCatalogImportService>().ImportAsync(c, stream, "catalog.csv", default, dryRun);
        });
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, async (sp, c) =>
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            return await sp.GetRequiredService<ProcurementCatalogImportService>().ImportAsync(c, stream, "catalog.csv", default, true);
        }));

        var check = await Import(true);
        Assert.Equal((1, 1, 3), (check.Created, check.Updated, check.Skipped));
        using (var scope = fixture.Services.CreateScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementItems.AnyAsync(i => i.Sku == $"CI-NEW-{org.S}"));
        }

        var done = await Import(false);
        Assert.Equal((1, 1, 3), (done.Created, done.Updated, done.Skipped));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var created = await db.ProcurementItems.SingleAsync(i => i.Sku == $"CI-NEW-{org.S}");
            Assert.Equal((true, 180, ProcurementQualityControl.Documents, "сухо"), (created.TracksBatches, created.ShelfLifeDays!.Value, created.QualityControl, created.StorageConditions!));
            Assert.Equal($"Ваниль {org.S} (новое имя)", await db.ProcurementItems.Where(i => i.Id == existing).Select(i => i.Name).SingleAsync());
        }

        Assert.Equal(0, (await Import(false)).Updated); // повтор — без изменений
    }

    private async Task<bool> NotifiedAsync(User user, string title)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().UserNotifications.AnyAsync(n => n.RecipientId == user.Id && n.Title.StartsWith(title));
    }
}

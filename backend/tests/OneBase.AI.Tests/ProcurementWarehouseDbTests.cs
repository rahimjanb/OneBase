using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Несколько складов на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): справочник складов — только с правом настроек
/// снабжения, склад по умолчанию не отключается, склад с остатком — тоже; остаток по складу и по всем; перемещение — один документ, обе
/// стороны одной записью, только свободный остаток, повтор формы не проводит второй раз, одновременные перемещения не уходят в минус;
/// закреплённый кладовщик проводит операции только по своим складам (приход, перемещение, выдача по заявке, загрузка журнала); у каждого
/// склада своя первая загрузка журнала. Своя база: склады добавляются ко всей базе.
/// </summary>
public class ProcurementWarehouseDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Director = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];

    private sealed record Org(string S, Department Dept, User Employee, User Keeper, User Assigned, User Director, User Supply, Guid Category, Guid Central);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"wh-{s}", Name = $"Цех {s}" };
        var keeperRole = new Role { Name = $"Кладовщик {s}" };
        keeperRole.Permissions.Add(new RolePermission { Code = Permissions.ProcurementWarehouse });

        User Person(string login, string name, Role? role = null)
        {
            var u = new User { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = dept };
            if (role is not null)
            {
                u.Roles.Add(new UserRole { Role = role });
            }

            return u;
        }

        var org = new Org(s, dept, Person("wh-emp", "Сотрудник"), Person("wh-kpr", "Кладовщик", keeperRole), Person("wh-raw", "Кладовщик сырья", keeperRole),
            Person("wh-dir", "Директор"), Person("wh-sup", "Снабженец"), Guid.Empty, Guid.Empty);
        db.Users.AddRange(org.Employee, org.Keeper, org.Assigned, org.Director, org.Supply);
        await db.SaveChangesAsync();
        // Маршрут без согласующих: заявка только со склада утверждается сама и резервирует на складе по умолчанию.
        var settings = await db.ProcurementSettings.FirstOrDefaultAsync() ?? db.ProcurementSettings.Add(new ProcurementSettings()).Entity;
        (settings.FinanceApproverId, settings.DirectorApproverId) = (null, null);
        await db.SaveChangesAsync();
        var category = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync();
        var central = await db.ProcurementWarehouses.Where(w => w.IsDefault).Select(w => w.Id).SingleAsync();
        return org with { Category = category, Central = central };
    }

    private async Task<T> As<T>(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider, ctx);
    }

    private Task<T> Stock<T>(User user, string[] permissions, Func<ProcurementStockService, ProcurementContext, Task<T>> act) =>
        As(user, permissions, (sp, c) => act(sp.GetRequiredService<ProcurementStockService>(), c));

    private Task<T> Warehouses<T>(User user, string[] permissions, Func<ProcurementWarehouseService, ProcurementContext, Task<T>> act) =>
        As(user, permissions, (sp, c) => act(sp.GetRequiredService<ProcurementWarehouseService>(), c));

    private async Task Warehouses(User user, string[] permissions, Func<ProcurementWarehouseService, ProcurementContext, Task> act) =>
        await Warehouses(user, permissions, async (w, c) =>
        {
            await act(w, c);
            return true;
        });

    /// <summary>Склад «{name} {s}» с латинским кодом: сравнение кодов без учёта регистра не зависит от локали базы.</summary>
    private Task<Guid> NewWarehouseAsync(Org org, string name, ProcurementWarehouseKind kind = ProcurementWarehouseKind.RawMaterials, IReadOnlyList<Guid>? keepers = null) =>
        Warehouses(org.Director, Director, (w, c) => w.CreateAsync(c, new ProcurementWarehouseInput($"wh-{Guid.NewGuid():N}"[..14], $"{name} {org.S}", kind, KeeperIds: keepers), default));

    private Task<Guid> ItemAsync(Org org, string name) =>
        Stock(org.Keeper, Keeper, (s, c) => s.CreateItemAsync(c, new ProcurementCatalogInput($"WH-{Guid.NewGuid():N}"[..14], name, org.Category, "кг", null, null, 0), default));

    private Task<Guid> ReceiptAsync(Org org, Guid item, decimal qty, Guid? warehouse = null, User? keeper = null) =>
        Stock(keeper ?? org.Keeper, Keeper, (s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(item, qty, 9_000)], "Приход", WarehouseId: warehouse), default));

    private async Task<decimal> OnHandAsync(Guid warehouse, Guid item)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementStockBalances.AsNoTracking()
            .Where(b => b.WarehouseId == warehouse && b.ItemId == item).Select(b => b.OnHand).FirstOrDefaultAsync();
    }

    [PostgresFact]
    public async Task Warehouses_are_kept_with_the_settings_right_and_the_default_one_is_never_switched_off()
    {
        var org = await SeedAsync();

        // Список видит кладовщик (без права править), заводит склад только «Настройки снабжения».
        var view = await Warehouses(org.Keeper, Keeper, (w, c) => w.ListAsync(c, default));
        Assert.False(view.CanEdit);
        Assert.Contains(view.Warehouses, w => w.Id == org.Central && w.IsDefault && w.IsActive && w.Kind == ProcurementWarehouseKind.Central);
        Assert.Contains(view.KeeperOptions, k => k.Id == org.Keeper.Id);
        Assert.DoesNotContain(view.KeeperOptions, k => k.Id == org.Employee.Id);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Warehouses(org.Employee, Employee, (w, c) => w.ListAsync(c, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Warehouses(org.Keeper, Keeper, (w, c) =>
            w.CreateAsync(c, new ProcurementWarehouseInput("raw", "Склад сырья", ProcurementWarehouseKind.RawMaterials), default)));

        var raw = await NewWarehouseAsync(org, "Сырьё", keepers: [org.Assigned.Id]);
        var shop = await Warehouses(org.Director, Director, (w, c) => w.CreateAsync(c,
            new ProcurementWarehouseInput($"shop-{org.S}", $"Кладовая цеха {org.S}", ProcurementWarehouseKind.Workshop, org.Dept.Id, "Корпус 2"), default));

        view = await Warehouses(org.Director, Director, (w, c) => w.ListAsync(c, default));
        var created = view.Warehouses.Single(w => w.Id == raw);
        Assert.Equal(($"Сырьё {org.S}", ProcurementWarehouseKind.RawMaterials, true), (created.Name, created.Kind, created.IsActive));

        // Код и название — без повторов (код — без учёта регистра), код без пробелов; закрепить можно только кладовщика.
        await Assert.ThrowsAsync<FieldValidationException>(() => Warehouses(org.Director, Director, (w, c) =>
            w.CreateAsync(c, new ProcurementWarehouseInput(created.Code.ToUpperInvariant(), "Другой", ProcurementWarehouseKind.Other), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Warehouses(org.Director, Director, (w, c) =>
            w.CreateAsync(c, new ProcurementWarehouseInput($"x-{org.S}", created.Name, ProcurementWarehouseKind.Other), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Warehouses(org.Director, Director, (w, c) =>
            w.CreateAsync(c, new ProcurementWarehouseInput("два слова", "Третий", ProcurementWarehouseKind.Other), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Warehouses(org.Director, Director, (w, c) =>
            w.CreateAsync(c, new ProcurementWarehouseInput($"y-{org.S}", $"Четвёртый {org.S}", ProcurementWarehouseKind.Other, KeeperIds: [org.Employee.Id]), default)));
        Assert.Equal(org.Assigned.Id, Assert.Single(created.Keepers).Id);
        var workshop = view.Warehouses.Single(w => w.Id == shop);
        Assert.Equal((org.Dept.Id, org.Dept.Name, "Корпус 2"), (workshop.DepartmentId, workshop.DepartmentName, workshop.Address));

        // Склад по умолчанию не отключается; правка по устаревшей версии — 409; отключённый склад операций не принимает.
        var central = view.Warehouses.Single(w => w.Id == org.Central);
        await Assert.ThrowsAsync<FieldConflictException>(() => Warehouses(org.Director, Director, (w, c) =>
            w.UpdateAsync(c, org.Central, new ProcurementWarehouseInput(central.Code, central.Name, central.Kind, IsActive: false, Version: central.Version), default)));
        await Warehouses(org.Director, Director, (w, c) =>
            w.UpdateAsync(c, shop, new ProcurementWarehouseInput(workshop.Code, workshop.Name, workshop.Kind, workshop.DepartmentId, null, IsActive: false, Version: workshop.Version), default));
        await Assert.ThrowsAsync<FieldConflictException>(() => Warehouses(org.Director, Director, (w, c) =>
            w.UpdateAsync(c, shop, new ProcurementWarehouseInput(workshop.Code, workshop.Name, workshop.Kind, Version: workshop.Version), default)));
        var item = await ItemAsync(org, $"Мука {org.S}");
        await Assert.ThrowsAsync<FieldConflictException>(() => ReceiptAsync(org, item, 5, shop));

        // Снять закрепление — список кладовщиков склада пуст.
        await Warehouses(org.Director, Director, (w, c) =>
            w.UpdateAsync(c, raw, new ProcurementWarehouseInput(created.Code, created.Name, created.Kind, KeeperIds: [], Version: created.Version), default));
        Assert.Empty((await Warehouses(org.Director, Director, (w, c) => w.ListAsync(c, default))).Warehouses.Single(w => w.Id == raw).Keepers);
    }

    [PostgresFact]
    public async Task Stock_is_kept_per_warehouse_and_a_transfer_moves_free_stock_in_one_document()
    {
        var org = await SeedAsync();
        var raw = await NewWarehouseAsync(org, "Сырьё");
        var item = await ItemAsync(org, $"Сахар {org.S}");
        await ReceiptAsync(org, item, 100);
        await ReceiptAsync(org, item, 30, raw);

        // Остаток: по складу и сумма по всем складам; плитки склада — по его остатку.
        var all = await Stock(org.Supply, Supply, (s, c) => s.StockAsync(c, new ProcurementStockSearch($"Сахар {org.S}"), default));
        Assert.Equal(130m, Assert.Single(all.Items).OnHand);
        var onlyRaw = await Stock(org.Supply, Supply, (s, c) => s.StockAsync(c, new ProcurementStockSearch($"Сахар {org.S}", WarehouseId: raw), default));
        Assert.Equal(30m, Assert.Single(onlyRaw.Items).OnHand);
        var summary = await Stock(org.Supply, Supply, (s, c) => s.SummaryAsync(c, default, raw));
        Assert.Equal(1, summary.Items - summary.Empty);

        // Перемещение 40 с центрального на склад сырья: один документ TRF-, «ушло» и «пришло», итог 130 не изменился; повтор формы — тот же документ.
        var key = Guid.NewGuid();
        var transfer = new ProcurementTransferInput(org.Central, raw, [new(item, 40)], "В цех сырья", ClientKey: key);
        var doc1 = await Stock(org.Keeper, Keeper, (s, c) => s.TransferAsync(c, transfer, default));
        var doc2 = await Stock(org.Keeper, Keeper, (s, c) => s.TransferAsync(c, transfer, default));
        Assert.Equal(doc1, doc2);
        Assert.Equal((60m, 70m), (await OnHandAsync(org.Central, item), await OnHandAsync(raw, item)));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var doc = await db.ProcurementStockDocuments.AsNoTracking().SingleAsync(d => d.Id == doc1);
            Assert.Equal((ProcurementStockDocumentKind.Transfer, org.Central, raw), (doc.Kind, doc.WarehouseId, doc.TargetWarehouseId));
            Assert.StartsWith("TRF-", doc.Number);
            var moves = await db.ProcurementStockMovements.AsNoTracking().Where(m => m.DocumentId == doc1).OrderBy(m => m.Quantity).ToListAsync();
            Assert.Equal([(ProcurementMovementKind.TransferOut, org.Central, -40m, 100m, 60m), (ProcurementMovementKind.TransferIn, raw, 40m, 30m, 70m)],
                moves.Select(m => (m.Kind, m.WarehouseId, m.Quantity, m.BalanceBefore, m.BalanceAfter)));
        }

        var rawJournal = await Stock(org.Supply, Supply, (s, c) => s.MovementsAsync(c, new ProcurementMovementSearch(item, WarehouseId: raw), default));
        Assert.Equal([ProcurementMovementKind.TransferIn, ProcurementMovementKind.Receipt], rawJournal.Items.Select(m => m.Kind));
        Assert.Equal(($"Сырьё {org.S}", "Центральный склад"), (rawJournal.Items[0].WarehouseName, rawJournal.Items[0].CounterpartName));
        var card = await Stock(org.Supply, Supply, (s, c) => s.ItemAsync(c, item, default));
        Assert.Equal(130m, card.Item.OnHand);
        Assert.Equal([(org.Central, 60m), (raw, 70m)], card.Warehouses!.Select(w => (w.WarehouseId, w.OnHand)));
        Assert.Equal(70m, (await Stock(org.Supply, Supply, (s, c) => s.ItemAsync(c, item, default, raw))).Item.OnHand);

        // Резерв заявки на центральном складе не перемещается: 60 на складе, 50 в резерве — переместить можно 10.
        await As(org.Employee, Employee, (sp, c) => sp.GetRequiredService<ProcurementRequestService>().CreateAsync(c, new ProcurementRequestInput(
            "Сахар для цеха", null, ProcurementPriority.Normal, null, "Выпуск", null, [new(null, null, null, null, 50, 9_000, null, item, 50)], true), default));
        var reserved = await Assert.ThrowsAsync<FieldConflictException>(() =>
            Stock(org.Keeper, Keeper, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(org.Central, raw, [new(item, 20)]), default)));
        Assert.Contains("переместить можно 10", reserved.Message);
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            Stock(org.Keeper, Keeper, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(raw, raw, [new(item, 1)]), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            Stock(org.Keeper, Keeper, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(raw, org.Central, [new(item, 0)]), default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => // перемещает кладовщик
            Stock(org.Supply, Supply, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(raw, org.Central, [new(item, 1)]), default)));

        // Склад с остатком не отключается.
        var rawView = (await Warehouses(org.Director, Director, (w, c) => w.ListAsync(c, default))).Warehouses.Single(w => w.Id == raw);
        await Assert.ThrowsAsync<FieldConflictException>(() => Warehouses(org.Director, Director, (w, c) =>
            w.UpdateAsync(c, raw, new ProcurementWarehouseInput(rawView.Code, rawView.Name, rawView.Kind, IsActive: false, Version: rawView.Version), default)));
    }

    [PostgresFact]
    public async Task Concurrent_transfers_never_move_more_than_is_free()
    {
        var org = await SeedAsync();
        var raw = await NewWarehouseAsync(org, "Склад");
        var item = await ItemAsync(org, $"Крахмал {org.S}");
        await ReceiptAsync(org, item, 100);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                await Stock(org.Keeper, Keeper, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(org.Central, raw, [new(item, 30)]), default));
                return true;
            }
            catch (Exception e) when (e is FieldConflictException or DbUpdateConcurrencyException)
            {
                return false;
            }
        }));

        var moved = results.Count(ok => ok) * 30m;
        Assert.InRange(results.Count(ok => ok), 1, 3);
        Assert.Equal((100m - moved, moved), (await OnHandAsync(org.Central, item), await OnHandAsync(raw, item)));
    }

    [PostgresFact]
    public async Task A_keeper_assigned_to_warehouses_posts_only_there()
    {
        var org = await SeedAsync();
        var raw = await NewWarehouseAsync(org, "Сырьё", keepers: [org.Assigned.Id]);
        var item = await ItemAsync(org, $"Какао {org.S}");
        await ReceiptAsync(org, item, 50); // незакреплённый кладовщик — по всем складам

        var ctx = await As(org.Assigned, Keeper, (_, c) => Task.FromResult(c));
        Assert.Equal((true, false), (ctx.Posts(raw), ctx.Posts(org.Central)));
        var forbidden = await Assert.ThrowsAsync<FieldForbiddenException>(() => ReceiptAsync(org, item, 5, org.Central, org.Assigned));
        Assert.Contains("закреплены за другими складами", forbidden.Message);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => ReceiptAsync(org, item, 5, keeper: org.Assigned)); // склад не указан — по умолчанию (центральный)
        await ReceiptAsync(org, item, 20, raw, org.Assigned);
        await Assert.ThrowsAsync<FieldForbiddenException>(() =>
            Stock(org.Assigned, Keeper, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(org.Central, raw, [new(item, 5)]), default)));
        await Stock(org.Assigned, Keeper, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(raw, org.Central, [new(item, 5)]), default)); // получатель — любой склад
        Assert.Equal((55m, 15m), (await OnHandAsync(org.Central, item), await OnHandAsync(raw, item)));

        // Выдача по заявке — с центрального склада: закреплённый за складом сырья не выдаёт, незакреплённый — выдаёт.
        var requests = (Func<IServiceProvider, ProcurementRequestService>)(sp => sp.GetRequiredService<ProcurementRequestService>());
        var request = await As(org.Employee, Employee, (sp, c) => requests(sp).CreateAsync(c, new ProcurementRequestInput(
            "Какао для цеха", null, ProcurementPriority.Normal, null, "Выпуск", null, [new(null, null, null, null, 10, 9_000, null, item, 10)], true), default));
        var line = (await As(org.Keeper, Keeper, (sp, c) => requests(sp).GetAsync(c, request, default))).Items[0].Id;
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Assigned, Keeper, (sp, c) => requests(sp).IssueAsync(c, request, new([new(line, 10)], "Мастер", null), default)));
        await As(org.Keeper, Keeper, (sp, c) => requests(sp).IssueAsync(c, request, new([new(line, 10)], "Мастер", null), default));
        Assert.Equal(45m, await OnHandAsync(org.Central, item));
    }

    [PostgresFact]
    public async Task Each_warehouse_has_its_own_journal_import()
    {
        var org = await SeedAsync();
        var raw = await NewWarehouseAsync(org, "Журнал", keepers: [org.Assigned.Id]);
        var sku = $"J{org.S}-1";
        object[][] rows =
        [
            ["", "", "01.08.2025", "Приход", "01.08.2025", sku, sku, $"Мука {org.S}", "кг", 500, "Устав"],
            ["", "02.08.2025 9:00:00", "02.08.2025", "Расход", "01.08.2025", sku, sku, $"Мука {org.S}", "кг", 120, $"Цех {org.S}"],
        ];

        // Закреплённый за складом сырья загружает только на него; первая загрузка этого склада — начальные остатки.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => ImportAsync(org.Assigned, rows, org.Central));
        var first = await ImportAsync(org.Assigned, rows, raw);
        Assert.Equal(("opening", 2, $"Журнал {org.S}"), (first.Mode, first.Posted, first.Warehouse));
        var again = await ImportAsync(org.Assigned, rows, raw);
        Assert.Equal(("movements", 2, 0), (again.Mode, again.Known, again.Posted));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var item = await db.ProcurementItems.Where(i => i.Sku == sku).Select(i => i.Id).SingleAsync();
        Assert.Equal((380m, 0m), (await OnHandAsync(raw, item), await OnHandAsync(org.Central, item)));
        Assert.True(await db.ProcurementStockImports.AnyAsync(i => i.WarehouseId == raw));
    }

    private static readonly object[] Header = ["", "Дата заполнения", "Дата", "П/Р", "Партия", "Артикул/Хомашё", "Артикул", "Хомашё номи", "Ед. изм.", "Количество", "Цех ва склад номлари"];

    private async Task<ProcurementStockImportResult> ImportAsync(User keeper, object[][] rows, Guid warehouse)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Sheet1");
        object[][] all = [Header, .. rows];
        for (var r = 0; r < all.Length; r++)
        {
            for (var c = 0; c < all[r].Length; c++)
            {
                sheet.Cell(r + 1, c + 1).Value = all[r][c] is int n ? n : (string)all[r][c];
            }
        }

        var file = new MemoryStream();
        book.SaveAs(file);
        return await As(keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockImportService>()
            .ImportAsync(c, new MemoryStream(file.ToArray()), "Склад сырья.xlsx", default, false, warehouse));
    }
}

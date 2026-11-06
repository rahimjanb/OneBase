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
/// Партии и сроки годности на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): приход позиции с партиями — только с партией
/// и сроком; выдача — годные по FEFO, просроченная не выдаётся с первой минуты следующего дня по Ташкенту, но списывается; перемещение
/// сохраняет партию; резерв и выдача по заявке — только годное; ввод партий на имеющийся остаток; инвентаризация по партиям; сторно;
/// загрузка журнала с партиями и сроком; напоминания о сроке — по разу. После каждого сценария — остаток позиции = сумма её партий.
/// Своя база и часы теста: «сегодня» — середина дня, чтобы не зависеть от времени запуска.
/// </summary>
public class ProcurementBatchDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];

    private sealed record Org(string S, Department Dept, User Employee, User Keeper, Guid Category, Guid Central, Guid Raw);

    private DateOnly Today => FieldClock.LocalDate(fixture.Clock.Now ?? DateTimeOffset.UtcNow);

    /// <summary>Часы теста — на полдень дня day (Ташкент).</summary>
    private void At(DateOnly day, int hour = 12, int minute = 0) => fixture.Clock.Now = FieldClock.DayStartUtc(day).AddHours(hour).AddMinutes(minute);

    private async Task<Org> SeedAsync()
    {
        At(FieldClock.Today);
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"bt-{s}", Name = $"Цех {s}" };
        var keeperRole = new Role { Name = $"Кладовщик {s}" };
        keeperRole.Permissions.Add(new RolePermission { Code = Permissions.ProcurementWarehouse });
        var employee = new User { Login = $"bt-emp-{s}", FullName = $"Сотрудник {s}", Department = dept };
        var keeper = new User { Login = $"bt-kpr-{s}", FullName = $"Кладовщик {s}", Department = dept };
        keeper.Roles.Add(new UserRole { Role = keeperRole });
        db.Users.AddRange(employee, keeper);
        var raw = new ProcurementWarehouse { Code = $"bt-{s}", Name = $"Склад сырья {s}", Kind = ProcurementWarehouseKind.RawMaterials };
        db.ProcurementWarehouses.Add(raw);
        var settings = await db.ProcurementSettings.FirstOrDefaultAsync() ?? db.ProcurementSettings.Add(new ProcurementSettings()).Entity;
        (settings.FinanceApproverId, settings.DirectorApproverId) = (null, null);
        await db.SaveChangesAsync();
        var category = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync();
        var central = await db.ProcurementWarehouses.Where(w => w.IsDefault).Select(w => w.Id).SingleAsync();
        return new Org(s, dept, employee, keeper, category, central, raw.Id);
    }

    private async Task<T> As<T>(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider, ctx);
    }

    private Task<T> Stock<T>(Org org, Func<ProcurementStockService, ProcurementContext, Task<T>> act) =>
        As(org.Keeper, Keeper, (sp, c) => act(sp.GetRequiredService<ProcurementStockService>(), c));

    private Task<Guid> ItemAsync(Org org, string name, bool batches = true, int? shelfLife = null) =>
        Stock(org, (s, c) => s.CreateItemAsync(c, new ProcurementCatalogInput($"BT-{Guid.NewGuid():N}"[..14], $"{name} {org.S}", org.Category, "кг", null, null, 0,
            TracksBatches: batches, ShelfLifeDays: shelfLife), default));

    private static ProcurementBatchInput Batch(string no, DateOnly expires, decimal qty, DateOnly? made = null) => new(no, made, expires, qty);

    private Task<Guid> ReceiveAsync(Org org, Guid item, Guid? warehouse, params ProcurementBatchInput[] batches) =>
        Stock(org, (s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(item, batches.Sum(b => b.Quantity), 7_500, Batches: batches)], "Приход",
            WarehouseId: warehouse), default));

    private Task<Guid> IssueAsync(Org org, Guid item, decimal qty, Guid? batch = null, Guid? warehouse = null) =>
        Stock(org, (s, c) => s.IssueAsync(c, new ProcurementStockOperationInput([new(item, qty, BatchId: batch)], null, DepartmentId: org.Dept.Id, WarehouseId: warehouse), default));

    private async Task<List<(string Number, string? No, DateOnly Expires, Guid Warehouse, decimal OnHand)>> BatchesAsync(Guid item)
    {
        using var scope = fixture.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementBatchBalances.AsNoTracking().Where(b => b.ItemId == item)
                .OrderBy(b => b.Batch!.ExpiresOn).ThenBy(b => b.Batch!.Number).Select(b => new { b.Batch!.Number, b.Batch.SupplierBatchNo, b.Batch.ExpiresOn, b.WarehouseId, b.OnHand })
                .ToListAsync())
            .Select(b => (b.Number, b.SupplierBatchNo, b.ExpiresOn!.Value, b.WarehouseId, b.OnHand)).ToList();
    }

    private async Task<decimal> OnHandAsync(Guid item, Guid warehouse)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementStockBalances.AsNoTracking()
            .Where(b => b.ItemId == item && b.WarehouseId == warehouse).Select(b => b.OnHand).FirstOrDefaultAsync();
    }

    /// <summary>Инвариант: у позиций с партиями остаток позиции на каждом складе = сумма остатков её партий.</summary>
    private async Task InvariantAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var broken = await db.ProcurementStockBalances.AsNoTracking()
            .Where(b => db.ProcurementItems.Any(i => i.Id == b.ItemId && i.TracksBatches))
            .Where(b => b.OnHand != (db.ProcurementBatchBalances.Where(x => x.ItemId == b.ItemId && x.WarehouseId == b.WarehouseId).Sum(x => (decimal?)x.OnHand) ?? 0m))
            .CountAsync();
        Assert.Equal(0, broken);
    }

    [PostgresFact]
    public async Task A_batch_item_is_received_only_with_batches_and_an_expiry_date()
    {
        var org = await SeedAsync();
        try
        {
            var item = await ItemAsync(org, "Мука", shelfLife: 180);
            var today = Today;

            await Assert.ThrowsAsync<FieldValidationException>(() => // без партии
                Stock(org, (s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(item, 10)], null), default)));
            await Assert.ThrowsAsync<FieldValidationException>(() => ReceiveAsync(org, item, null, Batch("A", today.AddDays(-1), 10))); // просроченное не принимается
            await Assert.ThrowsAsync<FieldValidationException>(() => ReceiveAsync(org, item, null, new ProcurementBatchInput("B", null, null, 10))); // ни срока, ни даты производства
            await Assert.ThrowsAsync<FieldValidationException>(() => // сумма партий ≠ количеству
                Stock(org, (s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(item, 15, Batches: [Batch("A", today.AddDays(30), 10)])], null), default)));

            // Две партии: одна со сроком, у второй — по дате производства и сроку хранения позиции (180 дней).
            await ReceiveAsync(org, item, null, Batch("A-1", today.AddDays(30), 40), new ProcurementBatchInput("A-2", today.AddDays(-10), null, 60));
            var batches = await BatchesAsync(item);
            Assert.Equal([("A-1", today.AddDays(30), 40m), ("A-2", today.AddDays(170), 60m)], batches.Select(b => (b.No!, b.Expires, b.OnHand)));
            Assert.All(batches, b => Assert.StartsWith("LOT-", b.Number));
            Assert.Equal(100m, await OnHandAsync(item, org.Central));

            // Позицию с партиями не провести мимо партий (так закрыты «Куплено» и приёмка по заказу, пока у них нет партий).
            using (var scope = fixture.Services.CreateScope())
            {
                var ledger = scope.ServiceProvider.GetRequiredService<ProcurementStockLedger>();
                var balance = (await ledger.BalancesAsync(org.Central, [item], default))[item];
                var doc = new ProcurementStockDocument { Number = "X", WarehouseId = org.Central };
                var blocked = Assert.Throws<FieldConflictException>(() => ledger.Move(doc, balance, ProcurementMovementKind.Receipt, 5, "Мука"));
                Assert.Contains("ведётся по партиям", blocked.Message);
            }

            // Учёт партий не выключить, пока остаток не ноль.
            var card = await Stock(org, (s, c) => s.ItemAsync(c, item, default));
            Assert.True(card.Item.TracksBatches);
            await Assert.ThrowsAsync<FieldConflictException>(() => Stock(org, async (s, c) =>
            {
                await s.UpdateItemAsync(c, item, new ProcurementCatalogInput(card.Item.Sku, card.Item.Name, card.Item.CategoryId, "кг", null, null, 0, TracksBatches: false), default);
                return true;
            }));
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Issue_takes_usable_batches_by_fefo_and_an_expired_batch_is_blocked_from_the_next_minute()
    {
        var org = await SeedAsync();
        try
        {
            var day = Today;
            var item = await ItemAsync(org, "Сахар");
            await ReceiveAsync(org, item, null, Batch("LATE", day.AddDays(40), 50), Batch("SOON", day, 30), Batch("MID", day.AddDays(5), 20));

            // FEFO: сначала «SOON» (годна сегодня), потом «MID».
            await IssueAsync(org, item, 40);
            Assert.Equal([("SOON", 0m), ("MID", 10m), ("LATE", 50m)], (await BatchesAsync(item)).Select(b => (b.No!, b.OnHand)));

            // Ещё одна партия со сроком «сегодня»: в 23:59 её выдают, в 00:01 следующего дня — уже нет.
            await ReceiveAsync(org, item, null, Batch("TODAY", day, 15));
            var today = (await BatchesAsync(item)).Single(b => b.No == "TODAY");
            var todayId = await BatchIdAsync(today.Number);
            At(day, 23, 59);
            await IssueAsync(org, item, 1, todayId);
            At(day.AddDays(1), 0, 1);
            var expired = await Assert.ThrowsAsync<FieldConflictException>(() => IssueAsync(org, item, 1, todayId));
            Assert.Contains("просрочена", expired.Message);

            // Без выбора партии — только годное: 10 (MID) + 50 (LATE) = 60; просроченные 14 не выдаются и не входят в «доступно».
            var row = Assert.Single((await Stock(org, (s, c) => s.StockAsync(c, new ProcurementStockSearch($"Сахар {org.S}"), default))).Items);
            Assert.Equal((74m, 14m, 60m), (row.OnHand, row.Expired, row.Available));
            Assert.Contains((await Stock(org, (s, c) => s.StockAsync(c, new ProcurementStockSearch($"Сахар {org.S}", Filter: "expired"), default))).Items, r => r.ItemId == item);
            var tooMuch = await Assert.ThrowsAsync<FieldConflictException>(() => IssueAsync(org, item, 61));
            Assert.Contains("просрочено 14", tooMuch.Message);

            // Просроченную партию списывают (с причиной).
            await Stock(org, (s, c) => s.WriteOffAsync(c, new ProcurementStockOperationInput([new(item, 14, BatchId: todayId)], "Истёк срок годности"), default));
            row = Assert.Single((await Stock(org, (s, c) => s.StockAsync(c, new ProcurementStockSearch($"Сахар {org.S}"), default))).Items);
            Assert.Equal((60m, 0m, 60m), (row.OnHand, row.Expired, row.Available));
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    private async Task<Guid> BatchIdAsync(string number)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementBatches.Where(b => b.Number == number).Select(b => b.Id).SingleAsync();
    }

    [PostgresFact]
    public async Task A_transfer_keeps_the_batch_and_its_history()
    {
        var org = await SeedAsync();
        try
        {
            var item = await ItemAsync(org, "Какао");
            await ReceiveAsync(org, item, null, Batch("K-7", Today.AddDays(90), 100));
            var number = (await BatchesAsync(item)).Single().Number;
            var id = await BatchIdAsync(number);
            await Stock(org, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(org.Central, org.Raw, [new(item, 35, BatchId: id)]), default));

            // На складе сырья — та же партия: номер, номер производителя, срок.
            Assert.Equal([(number, "K-7", org.Central, 65m), (number, "K-7", org.Raw, 35m)],
                (await BatchesAsync(item)).OrderBy(b => b.Warehouse == org.Raw).Select(b => (b.Number, b.No!, b.Warehouse, b.OnHand)));
            var card = await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementBatchService>().CardAsync(c, id, default));
            Assert.Equal((100m, number, "ok"), (card.Batch.OnHand, card.Batch.Number, card.Batch.State));
            Assert.Equal(2, card.Warehouses.Count);
            Assert.Equal([ProcurementMovementKind.Receipt, ProcurementMovementKind.TransferOut, ProcurementMovementKind.TransferIn], card.Movements.Select(m => m.Kind).Order());
            Assert.All(card.Movements, m => Assert.Equal(number, m.BatchNumber));

            // Реестр склада сырья и варианты для формы.
            var list = await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementBatchService>().ListAsync(c, new ProcurementBatchSearch(item, org.Raw), default));
            Assert.Equal(35m, Assert.Single(list.Items).OnHand);
            var options = await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementBatchService>().OptionsAsync(c, item, org.Raw, default));
            Assert.True(Assert.Single(options).Usable);
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Requests_reserve_and_get_only_usable_stock()
    {
        var org = await SeedAsync();
        try
        {
            var day = Today;
            var item = await ItemAsync(org, "Масло");
            await ReceiveAsync(org, item, null, Batch("OLD", day, 20), Batch("NEW", day.AddDays(60), 30));
            At(day.AddDays(1)); // «OLD» просрочена: годного 30

            var requests = (Func<IServiceProvider, ProcurementRequestService>)(sp => sp.GetRequiredService<ProcurementRequestService>());
            ProcurementRequestInput Request(decimal qty) => new("Масло для цеха", null, ProcurementPriority.Normal, null, "Выпуск", null,
                [new(null, null, null, null, qty, 9_000, null, item, qty)], true);
            await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Employee, Employee, (sp, c) => requests(sp).CreateAsync(c, Request(31), default)));
            var request = await As(org.Employee, Employee, (sp, c) => requests(sp).CreateAsync(c, Request(25), default));

            // Резерв 25 из годных 30: без заявки выдать можно только 5, списать просроченную — можно.
            await Assert.ThrowsAsync<FieldConflictException>(() => IssueAsync(org, item, 6));
            await IssueAsync(org, item, 5);
            var line = (await As(org.Keeper, Keeper, (sp, c) => requests(sp).GetAsync(c, request, default))).Items[0].Id;
            await As(org.Keeper, Keeper, (sp, c) => requests(sp).IssueAsync(c, request, new([new(line, 25)], "Мастер", null), default));
            Assert.Equal([("OLD", 20m), ("NEW", 0m)], (await BatchesAsync(item)).Select(b => (b.No!, b.OnHand)));
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Batch_entry_turns_existing_stock_into_batches_and_inventory_counts_by_batch()
    {
        var org = await SeedAsync();
        try
        {
            var item = await ItemAsync(org, "Крахмал", batches: false);
            await Stock(org, (s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(item, 100)], null), default));
            await Stock(org, (s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(item, 20)], null, WarehouseId: org.Raw), default));
            var card = await Stock(org, (s, c) => s.ItemAsync(c, item, default));
            var input = new ProcurementCatalogInput(card.Item.Sku, card.Item.Name, card.Item.CategoryId, "кг", null, null, 0, TracksBatches: true);
            await Assert.ThrowsAsync<FieldConflictException>(() => Stock(org, async (s, c) => // с остатком — вводом партий
            {
                await s.UpdateItemAsync(c, item, input, default);
                return true;
            }));

            var day = Today;
            ProcurementBatchEntryWarehouse Central(decimal a, decimal b) => new(org.Central, [Batch("E-1", day.AddDays(-3), a), Batch("E-2", day.AddDays(20), b)]);
            await Assert.ThrowsAsync<FieldValidationException>(() => Stock(org, (s, c) => s.BatchEntryAsync(c, new ProcurementBatchEntryInput(item, [Central(60, 40)]), default))); // нет склада сырья
            await Assert.ThrowsAsync<FieldValidationException>(() => Stock(org, (s, c) =>
                s.BatchEntryAsync(c, new ProcurementBatchEntryInput(item, [Central(60, 30), new(org.Raw, [Batch("R-1", day.AddDays(9), 20)])]), default))); // 90 ≠ 100
            await Stock(org, (s, c) => s.BatchEntryAsync(c, new ProcurementBatchEntryInput(item, [Central(60, 40), new(org.Raw, [Batch("R-1", day.AddDays(9), 20)])]), default));

            // Остаток не изменился, позиция — по партиям; просроченная «E-1» введена как есть (её можно только списать или переместить).
            Assert.Equal((100m, 20m), (await OnHandAsync(item, org.Central), await OnHandAsync(item, org.Raw)));
            Assert.True((await Stock(org, (s, c) => s.ItemAsync(c, item, default))).Item.TracksBatches);
            var e1 = await BatchIdAsync((await BatchesAsync(item)).Single(b => b.No == "E-1").Number);

            // Инвентаризация — по партиям: без партии нельзя; факт «E-1» — 55.
            await Assert.ThrowsAsync<FieldValidationException>(() => Stock(org, (s, c) => s.InventoryAsync(c, new ProcurementStockOperationInput([new(item, 95)], "Пересчёт"), default)));
            await Stock(org, (s, c) => s.InventoryAsync(c, new ProcurementStockOperationInput([new(item, 55, BatchId: e1)], "Пересчёт"), default));
            Assert.Equal(95m, await OnHandAsync(item, org.Central));
            var journal = await Stock(org, (s, c) => s.MovementsAsync(c, new ProcurementMovementSearch(item, WarehouseId: org.Central), default));
            Assert.Equal([(ProcurementMovementKind.Adjustment, -5m), (ProcurementMovementKind.BatchEntry, 0m), (ProcurementMovementKind.BatchEntry, 0m)],
                journal.Items.Take(3).Select(m => (m.Kind, m.Quantity)));
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Reversal_mirrors_a_document_once_and_only_while_the_stock_is_there()
    {
        var org = await SeedAsync();
        try
        {
            var item = await ItemAsync(org, "Ваниль");
            var receipt = await ReceiveAsync(org, item, null, Batch("V-1", Today.AddDays(100), 10));
            var transfer = await Stock(org, (s, c) => s.TransferAsync(c, new ProcurementTransferInput(org.Central, org.Raw, [new(item, 4)]), default));

            // Приход не сторнировать, пока часть ушла перемещением; сначала сторно перемещения — склад сырья пустеет.
            await Assert.ThrowsAsync<FieldValidationException>(() => Stock(org, (s, c) => s.ReverseAsync(c, receipt, new ProcurementReversalInput(" "), default)));
            await Assert.ThrowsAsync<FieldConflictException>(() => Stock(org, (s, c) => s.ReverseAsync(c, receipt, new ProcurementReversalInput("Ошибка в накладной"), default)));
            await Stock(org, (s, c) => s.ReverseAsync(c, transfer, new ProcurementReversalInput("Не туда"), default));
            Assert.Equal((10m, 0m), (await OnHandAsync(item, org.Central), await OnHandAsync(item, org.Raw)));
            await Assert.ThrowsAsync<FieldConflictException>(() => Stock(org, (s, c) => s.ReverseAsync(c, transfer, new ProcurementReversalInput("Ещё раз"), default)));

            var reversal = await Stock(org, (s, c) => s.ReverseAsync(c, receipt, new ProcurementReversalInput("Ошибка в накладной"), default));
            Assert.Equal(0m, await OnHandAsync(item, org.Central));
            Assert.Equal(0m, (await BatchesAsync(item)).Sum(b => b.OnHand));
            var journal = await Stock(org, (s, c) => s.MovementsAsync(c, new ProcurementMovementSearch(item, WarehouseId: org.Central), default));
            Assert.Contains(journal.Items, m => m.DocumentId == receipt && m.ReversedBy != null);
            Assert.Contains(journal.Items, m => m.DocumentId == reversal && m.Kind == ProcurementMovementKind.Reversal && m.Quantity == -10);
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Journal_import_keeps_batches_and_expiry_dates()
    {
        var org = await SeedAsync();
        try
        {
            var day = Today;
            var sku = $"BJ{org.S}";
            string D(DateOnly d) => d.ToString("dd.MM.yyyy");
            object[][] rows =
            [
                ["", D(day.AddDays(-20)), "Приход", "P-1", sku, $"Мука {org.S}", "кг", 500, "", D(day.AddDays(60))],
                ["", D(day.AddDays(-15)), "Приход", "P-2", sku, $"Мука {org.S}", "кг", 300, "", D(day.AddDays(10))],
                ["", D(day.AddDays(-10)), "Расход", "", sku, $"Мука {org.S}", "кг", 350, $"Цех {org.S}", ""],
            ];

            // Первая загрузка: история по партиям — FEFO сначала из «P-2» (срок раньше): остаётся P-1 450.
            var first = await ImportAsync(org, rows);
            Assert.Equal(("opening", 1), (first.Mode, first.Batches));
            using (var scope = fixture.Services.CreateScope())
            {
                var item = await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementItems.SingleAsync(i => i.Sku == sku);
                Assert.True(item.TracksBatches);
                Assert.Equal([("P-1", day.AddDays(60), 450m)], (await BatchesAsync(item.Id)).Select(b => (b.No!, b.Expires, b.OnHand)));

                // Следующие строки: приход новой партии и выдача партией «P-1»; повтор файла ничего не проводит.
                object[][] more =
                [
                    .. rows,
                    ["", D(day.AddDays(-2)), "Приход", "P-3", sku, $"Мука {org.S}", "кг", 100, "", D(day.AddDays(200))],
                    ["", D(day.AddDays(-1)), "Расход", "P-1", sku, $"Мука {org.S}", "кг", 50, $"Цех {org.S}", ""],
                    ["", D(day.AddDays(-1)), "Приход", "P-4", sku, $"Мука {org.S}", "кг", 10, "", ""], // без срока — пропущена
                ];
                var next = await ImportAsync(org, more);
                Assert.Equal(("movements", 2, 1), (next.Mode, next.Posted, next.Batches));
                Assert.Contains(next.Errors, e => e.Contains("укажите срок годности"));
                Assert.Equal([("P-1", 400m), ("P-3", 100m)], (await BatchesAsync(item.Id)).Select(b => (b.No!, b.OnHand)));
                Assert.Equal(0, (await ImportAsync(org, more)).Posted);
                Assert.Equal(500m, await OnHandAsync(item.Id, org.Central));
            }

            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    private static readonly object[] Header = ["Дата заполнения", "Дата", "П/Р", "Партия", "Артикул", "Название", "Ед. изм.", "Количество", "Цех", "Срок годности"];

    private async Task<ProcurementStockImportResult> ImportAsync(Org org, object[][] rows)
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
        return await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockImportService>()
            .ImportAsync(c, new MemoryStream(file.ToArray()), "Склад сырья.xlsx", default, false, org.Central));
    }

    [PostgresFact]
    public async Task Expiry_reminders_are_sent_once_per_batch()
    {
        var org = await SeedAsync();
        try
        {
            var day = Today;
            var item = await ItemAsync(org, "Желатин");
            await ReceiveAsync(org, item, null, Batch("G-1", day.AddDays(3), 5), Batch("G-2", day, 5), Batch("G-3", day.AddDays(300), 5));
            At(day.AddDays(1)); // G-1 истекает (≤ 30 дней), G-2 просрочена, G-3 — рано
            using (var scope = fixture.Services.CreateScope())
            {
                var reminders = scope.ServiceProvider.GetRequiredService<ProcurementReminderService>();
                var first = await reminders.RemindAsync(Today, default);
                Assert.True(first.Expiring >= 1 && first.Expired >= 1);
                var again = await reminders.RemindAsync(Today, default);
                Assert.Equal((0, 0), (again.Expiring, again.Expired));
                var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
                Assert.True(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Keeper.Id && n.Title.StartsWith("Истекает срок годности")));
                Assert.True(await db.UserNotifications.AnyAsync(n => n.RecipientId == org.Keeper.Id && n.Title.StartsWith("Просрочено партий")));
                // Отметки — на самих партиях: G-1 — «истекает» на свой срок, G-2 — «просрочена», G-3 — ещё рано.
                var marks = await db.ProcurementBatches.AsNoTracking().Where(b => b.ItemId == item).ToDictionaryAsync(b => b.SupplierBatchNo!, b => (b.ExpiryWarnedFor, b.ExpiredNotifiedOn));
                Assert.Equal((day.AddDays(3), (DateOnly?)null), marks["G-1"]);
                Assert.Equal(((DateOnly?)null, day.AddDays(1)), marks["G-2"]);
                Assert.Equal(((DateOnly?)null, (DateOnly?)null), marks["G-3"]);

            }
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }
}

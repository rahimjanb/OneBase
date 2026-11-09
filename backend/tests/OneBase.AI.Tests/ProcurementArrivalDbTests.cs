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
/// Приезд машины и приёмка в порядке «качество → склад» на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается). Машина
/// приехала — склад не меняется, лаборатория проверяет строки до склада (проба только записывается); решено по всем — к приёмке; кладовщик
/// пишет места и вес — на склад идёт вес, тендеру засчитываются места, партии сразу допущены и связаны с проверкой; снабжению, аналитику и
/// финансисту — «принято на склад». Забраковано всё — возврат; отмена закрывает проверки; права, повтор формы и двойное нажатие. «Куплено —
/// привезёт поставщик»: покупка ждёт машину, принятое резервируется под заявку, остаток закрывает снабжение.
/// </summary>
public class ProcurementArrivalDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];
    private static readonly string[] Lab = [Permissions.ProcurementRequest, Permissions.QualityCheck];
    private static readonly string[] Analyst = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProductionRead];
    private static readonly string[] Employee = [Permissions.ProcurementRequest];

    private sealed record Org(string S, User Buyer, User Keeper, User Lab, User Analyst, User Financier, Guid Raw, Guid Central);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"ar-{s}", Name = $"Склад {s}" };
        Role RoleWith(string name, string code)
        {
            var role = new Role { Name = name };
            role.Permissions.Add(new RolePermission { Code = code });
            return role;
        }

        User Person(string login, string name, Role? role = null)
        {
            var user = new User { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = dept };
            if (role is not null)
            {
                user.Roles.Add(new UserRole { Role = role });
            }

            return user;
        }

        // Роль «Аналитик» — по имени; в тестовой базе ролей по умолчанию нет — создаётся один раз на базу.
        var analyst = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.AnalystRole) ?? new Role { Name = SystemRoles.AnalystRole };
        var org = new Org(s, Person("ar-buy", "Снабженец", RoleWith($"Снабжение {s}", Permissions.ProcurementManage)),
            Person("ar-kpr", "Кладовщик", RoleWith($"Кладовщик {s}", Permissions.ProcurementWarehouse)),
            Person("ar-lab", "Лаборант", RoleWith($"Лаборатория {s}", Permissions.QualityCheck)), Person("ar-an", "Аналитик", analyst), Person("ar-fin", "Финансист"),
            Guid.Empty, Guid.Empty);
        db.Users.AddRange(org.Buyer, org.Keeper, org.Lab, org.Analyst, org.Financier);
        var settings = await db.ProcurementSettings.FirstOrDefaultAsync() ?? db.ProcurementSettings.Add(new ProcurementSettings()).Entity;
        settings.FinanceApproverId = org.Financier.Id;
        await db.SaveChangesAsync();
        db.ProcurementQualityApprovers.RemoveRange(await db.ProcurementQualityApprovers.ToListAsync());
        db.ProcurementQualityApprovers.Add(new ProcurementQualityApprover { UserId = org.Lab.Id });
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

    private static ProcurementArrivalLineInput Ln(Guid line, decimal declared, string? lot = null, DateOnly? made = null) => new(line, declared, lot, made);

    private static ProcurementArrivalAcceptLine Ac(Guid line, decimal quantity, decimal? weight = null, string? comment = null) => new(line, quantity, weight, comment);

    private static ProcurementArrivalService A(IServiceProvider sp) => sp.GetRequiredService<ProcurementArrivalService>();
    private static ProcurementQualityService Q(IServiceProvider sp) => sp.GetRequiredService<ProcurementQualityService>();

    private async Task<T> DbAsync<T>(Func<OneBaseDbContext, Task<T>> read)
    {
        using var scope = fixture.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<OneBaseDbContext>());
    }

    private Task<bool> NotifiedAsync(User user, string title) => DbAsync(db => db.UserNotifications.AnyAsync(n => n.RecipientId == user.Id && n.Title.StartsWith(title)));

    private Task<decimal> OnHandAsync(Org org, Guid item) =>
        DbAsync(async db => await db.ProcurementStockBalances.Where(b => b.ItemId == item && b.WarehouseId == org.Central).Select(b => (decimal?)b.OnHand).SingleOrDefaultAsync() ?? 0m);

    /// <summary>Мука: партии, контроль «документы и лаборатория», единица закупки упак = 50 кг; спецификация — сертификат и влажность не выше 15 %.</summary>
    private async Task<Guid> FlourAsync(Org org)
    {
        var flour = await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c, new ProcurementCatalogInput(
            $"AR-{Guid.NewGuid():N}"[..14], $"Мука {org.S}", org.Raw, "кг", null, null, 0, TracksBatches: true, ShelfLifeDays: 180,
            QualityControl: ProcurementQualityControl.Lab, Units: [new("упак", 50)]), default));
        await Do(org.Lab, Lab, (sp, c) => Q(sp).UpdateSpecsAsync(c, flour, new ProcurementQualitySpecsInput([
            new(null, ProcurementQualitySpecKind.Document, "Сертификат качества"),
            new(null, ProcurementQualitySpecKind.Parameter, "Влажность", "%", null, 15m),
        ]), default));
        return flour;
    }

    private Task<Guid> SugarAsync(Org org) =>
        As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c, new ProcurementCatalogInput(
            $"AR-{Guid.NewGuid():N}"[..14], $"Сахар {org.S}", org.Raw, "кг", null, null, 0), default));

    private Task<Guid> TenderAsync(Org org, params ProcurementTenderLineInput[] lines) =>
        As(org.Buyer, Supply, (sp, c) => sp.GetRequiredService<ProcurementTenderService>().CreateAsync(c, new ProcurementTenderInput($"Сырьё {org.S}", "T-7",
            FieldClock.Today.AddDays(-5), null, $"ООО Зерно {org.S}", org.Raw, "Д-7", 2, FieldClock.Today.AddDays(1), null, lines, WarehouseId: org.Central), default));

    /// <summary>Проверка документов и лаборатория по спецификации: сертификат в порядке, влажность — значение, проба — сколько отобрали.</summary>
    private async Task CheckAsync(Org org, Guid check, decimal moisture, decimal? sample = null)
    {
        var card = await As(org.Lab, Lab, (sp, c) => Q(sp).CardAsync(c, check, default));
        var doc = card.Specs.Single(x => x.Kind == ProcurementQualitySpecKind.Document).Id;
        var param = card.Specs.Single(x => x.Kind == ProcurementQualitySpecKind.Parameter).Id;
        await Do(org.Lab, Lab, (sp, c) => Q(sp).DocumentsAsync(c, check, new([new(doc, true)]), default));
        await Do(org.Lab, Lab, (sp, c) => Q(sp).LabAsync(c, check, new([new(param, Value: moisture)], SampleQty: sample), default));
    }

    [PostgresFact]
    public async Task Truck_is_checked_by_the_lab_before_the_keeper_weighs_it_into_stock()
    {
        var org = await SeedAsync();
        var flour = await FlourAsync(org);
        var sugar = await SugarAsync(org);
        var tender = await TenderAsync(org, new($"Мука {org.S}", "упак", 200, 310_000, flour), new($"Сахар {org.S}", "кг", 1_000, 9_000, sugar));

        // Ближайшая поставка видна кладовщику (может отметить приезд) и лаборатории (только смотрит); сотруднику — нет.
        var expected = (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default))).Single();
        Assert.True(expected.CanArrive);
        Assert.Equal((false, 1, 200m, true), (expected.Overdue, expected.DaysLeft!.Value, expected.Items[0].Left, expected.Items[0].Weighed));
        Assert.Equal((ProcurementQualityControl.Lab, ProcurementQualityControl.None), (expected.Items[0].Control, expected.Items[1].Control));
        Assert.False((await As(org.Lab, Lab, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default))).Single().CanArrive);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Buyer, Employee, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default)));

        var flourLine = expected.Items[0].LineId;
        var sugarLine = expected.Items[1].LineId;
        var key = Guid.NewGuid();
        ProcurementArrivalInput Truck(params ProcurementArrivalLineInput[] lines) => new(tender, lines, VehicleNo: "01 A 123 BC", DriverName: "Алишер",
            Reference: "Накладная 15", ClientKey: key);
        var made = FieldClock.Today.AddDays(-3);

        // Отмечает приезд кладовщик; больше остатка по тендеру — нельзя; у позиции с партиями — срок обязателен.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Lab, Lab, (sp, c) => A(sp).RegisterAsync(c, Truck(Ln(flourLine, 10, "M-1", made)), default)));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, Truck(Ln(flourLine, 201, "M-1", made)), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, Truck(Ln(flourLine, 10, "M-1")), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, Truck(Ln(sugarLine, 10, "S-1")), default)));

        // Машина: мука двумя партиями (120 + 80 упак) и сахар 1 000 кг. Повтор формы — та же поставка.
        var truck = Truck(Ln(flourLine, 120, "M-1", made), Ln(flourLine, 80, "M-2", made), Ln(sugarLine, 1_000));
        var id = await As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, truck, default));
        Assert.Equal(id, await As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, truck, default)));
        var arrival = await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, id, default));
        Assert.Equal((ProcurementArrivalStatus.Quality, 3, 2, false), (arrival.Arrival.Status, arrival.Lines.Count, arrival.Arrival.Pending, arrival.Can.Accept));
        Assert.Equal([6_000m, 4_000m, 1_000m], arrival.Lines.Select(l => l.DeclaredBase));
        Assert.Equal(ProcurementBatchQuality.Approved, arrival.Lines[2].Quality); // сахар без контроля — сразу к приёмке

        // На склад ничего не пришло; проверки — без партии, со ссылкой на поставку; лаборатории и тому, кто вёл тендер, — уведомления.
        Assert.Equal((0m, 0m), (await OnHandAsync(org, flour), await OnHandAsync(org, sugar)));
        var checks = arrival.Lines.Take(2).Select(l => l.QualityCheckId!.Value).ToArray();
        Assert.True(await DbAsync(db => db.ProcurementQualityChecks.AllAsync(c => !checks.Contains(c.Id) || (c.BatchId == null && c.ArrivalId == id && c.ItemId == flour))));
        Assert.True(await NotifiedAsync(org.Lab, "Приехала машина"));
        Assert.True(await NotifiedAsync(org.Buyer, $"Машина приехала по тендеру"));
        var queue = await As(org.Lab, Lab, (sp, c) => Q(sp).ListAsync(c, new ProcurementQualitySearch(Search: arrival.Arrival.Number), default));
        Assert.Equal(2, queue.Total);
        Assert.All(queue.Items, r => Assert.Equal((null, arrival.Arrival.Number, 6_000m), (r.BatchId, r.ArrivalNumber, queue.Items[0].Received)));

        // Привезённое уже «на приёмке»: второй машиной муку сверх тендера не отметить.
        var again = (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default))).Single();
        Assert.Equal((200m, 0m, 1), (again.Items[0].OnArrival, again.Items[0].Left, again.OpenArrivals));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, Truck(Ln(flourLine, 1, "M-3", made)) with { ClientKey = null },
            default)));

        // До решения лаборатории — не принять; решает назначенный, кладовщик — нет.
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, id, new([]), default)));
        await CheckAsync(org, checks[0], 14.2m, sample: 2);
        await CheckAsync(org, checks[1], 16m);
        Assert.False(await DbAsync(db => db.ProcurementStockDocuments.AnyAsync(d => d.QualityCheckId == checks[0]))); // проба из машины — не списание
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Keeper, Keeper, (sp, c) => Q(sp).DecideAsync(c, checks[0], new("approve", null), default)));
        await Do(org.Lab, Lab, (sp, c) => Q(sp).DecideAsync(c, checks[0], new("approve", null), default));
        Assert.Equal(ProcurementArrivalStatus.Quality, (await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, id, default))).Arrival.Status);
        Assert.False(await NotifiedAsync(org.Keeper, "Можно принимать"));
        await Do(org.Lab, Lab, (sp, c) => Q(sp).DecideAsync(c, checks[1], new("reject", "Влажность 16 % — выше нормы"), default));

        // Решено по всем: к приёмке; кладовщику — «можно принимать», снабжению — вернуть забракованное.
        arrival = await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, id, default));
        Assert.Equal((ProcurementArrivalStatus.Ready, true, 1), (arrival.Arrival.Status, arrival.Can.Accept, arrival.Arrival.Rejected));
        Assert.Equal((2m, "Влажность 16 % — выше нормы"), (arrival.Lines[0].SampleQty!.Value, arrival.Lines[1].DecisionComment));
        Assert.True(await NotifiedAsync(org.Keeper, "Можно принимать"));
        Assert.True(await NotifiedAsync(org.Buyer, "Забраковано при приёмке"));

        // Приёмка: по каждой допущенной строке — места и вес (мука — весовая в упак), сахар — в кг одним числом.
        var (m1, m2, s1) = (arrival.Lines[0].Id, arrival.Lines[1].Id, arrival.Lines[2].Id);
        ProcurementArrivalAcceptInput Accept(params ProcurementArrivalAcceptLine[] lines) => new(lines, Version: arrival.Version);
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Lab, Lab, (sp, c) => A(sp).AcceptAsync(c, id, Accept(Ac(m1, 119, 5_940), Ac(s1, 1_000)), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, id, Accept(Ac(m1, 119, 5_940)), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, id, Accept(Ac(m1, 119), Ac(s1, 1_000)), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) =>
            A(sp).AcceptAsync(c, id, Accept(Ac(m1, 119, 5_940), Ac(m2, 80, 4_000), Ac(s1, 1_000)), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, id, Accept(Ac(m1, 119, 5_940), Ac(s1, 0)), default)));
        var receipt = await As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, id, Accept(Ac(m1, 119, 5_940), Ac(s1, 1_000)), default));
        Assert.Equal(receipt, await As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, id, Accept(Ac(m1, 119, 5_940), Ac(s1, 1_000)), default)));

        // На складе — вес (5 940 кг муки, а не 119 × 50), тендеру засчитаны места; партия допущена проверкой до склада.
        Assert.Equal((5_940m, 1_000m), (await OnHandAsync(org, flour), await OnHandAsync(org, sugar)));
        var batch = await DbAsync(db => db.ProcurementBatches.AsNoTracking().SingleAsync(b => b.ItemId == flour));
        Assert.Equal((ProcurementBatchQuality.Approved, "M-1", 6_200m, org.Lab.Id), (batch.QualityStatus, batch.SupplierBatchNo, batch.UnitCost!.Value, batch.QualityDecidedById!.Value));
        Assert.Equal(batch.Id, await DbAsync(db => db.ProcurementQualityChecks.Where(c => c.Id == checks[0]).Select(c => c.BatchId).SingleAsync()));
        arrival = await As(org.Buyer, Supply, (sp, c) => A(sp).GetAsync(c, id, default));
        Assert.Equal((ProcurementArrivalStatus.Accepted, receipt, 5_940m * 6_200 + 1_000m * 9_000), (arrival.Arrival.Status, arrival.Arrival.ReceiptId!.Value,
            arrival.Arrival.AcceptedTotal));
        Assert.Equal((119m, 5_940m, batch.Id), (arrival.Lines[0].AcceptedQty!.Value, arrival.Lines[0].AcceptedWeight!.Value, arrival.Lines[0].BatchId!.Value));
        Assert.Contains(arrival.History, h => h.Action == "accepted" && h.Text.Contains("недостача 60 кг"));
        var detail = await As(org.Buyer, Supply, (sp, c) => sp.GetRequiredService<ProcurementTenderService>().GetAsync(c, tender, default));
        Assert.Equal((119m, 81m, 1_000m, ProcurementTenderStatus.Contracted), (detail.Items[0].ReceivedQty, detail.Items[0].Left, detail.Items[1].ReceivedQty, detail.Status));
        Assert.Equal(81m, (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default))).Single().Items[0].Left);

        // «Принято на склад» — снабжению, аналитику и финансисту; принявшему — нет. Принятую поставку не отменить.
        foreach (var user in new[] { org.Buyer, org.Analyst, org.Financier })
        {
            Assert.True(await NotifiedAsync(user, "Принято на склад"));
        }

        Assert.False(await NotifiedAsync(org.Keeper, "Принято на склад"));
        Assert.NotNull(await As(org.Financier, Employee, (sp, c) => A(sp).GetAsync(c, id, default)));
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Keeper, Keeper, (sp, c) => A(sp).CancelAsync(c, id, new("Ошибка"), default)));

        // Рейтинг поставщика учитывает и забракованное при приезде.
        var supplier = detail.SupplierId;
        Assert.Equal((2, 1), await DbAsync(async db => (await db.ProcurementQualityChecks.CountAsync(c => c.SupplierId == supplier && c.Status == ProcurementQualityCheckStatus.Decided),
            await db.ProcurementQualityChecks.CountAsync(c => c.SupplierId == supplier && c.Decision == ProcurementBatchQuality.Rejected))));
    }

    [PostgresFact]
    public async Task Fully_rejected_truck_goes_back_and_cancel_closes_open_checks()
    {
        var org = await SeedAsync();
        var flour = await FlourAsync(org);
        var tender = await TenderAsync(org, new ProcurementTenderLineInput($"Мука {org.S}", "упак", 20, 300_000, flour));
        var line = (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default))).Single().Items[0].LineId;
        var made = FieldClock.Today.AddDays(-1);
        Task<Guid> TruckAsync(string lot) => As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, new(tender, [new(line, 10, lot, made)], VehicleNo: "10 B 777 AA"), default));

        // Забраковано всё — возврат: принимать нечего, кладовщику — «не разгружать».
        var first = await TruckAsync("B-1");
        var check = (await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, first, default))).Lines.Single().QualityCheckId!.Value;
        await Do(org.Lab, Lab, (sp, c) => Q(sp).DecideAsync(c, check, new("reject", "Нет сертификата"), default));
        Assert.Equal(ProcurementArrivalStatus.Returned, (await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, first, default))).Arrival.Status);
        Assert.True(await NotifiedAsync(org.Keeper, "Не разгружать"));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, first, new([]), default)));
        Assert.Equal(0m, await OnHandAsync(org, flour));
        Assert.Equal(20m, (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default))).Single().Items[0].Left);

        // Вторая машина отменена до проверки: проверка закрывается, остаток по тендеру снова свободен.
        var second = await TruckAsync("B-2");
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Lab, Lab, (sp, c) => A(sp).CancelAsync(c, second, new("Ошибка"), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Do(org.Keeper, Keeper, (sp, c) => A(sp).CancelAsync(c, second, new(" "), default)));
        await Do(org.Buyer, Supply, (sp, c) => A(sp).CancelAsync(c, second, new("Машина ушла на другой склад"), default));
        var cancelled = await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, second, default));
        Assert.Equal((ProcurementArrivalStatus.Cancelled, ProcurementQualityCheckStatus.Cancelled), (cancelled.Arrival.Status, cancelled.Lines.Single().CheckStatus!.Value));
        Assert.Equal(20m, (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default))).Single().Items[0].Left);
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Lab, Lab, (sp, c) => Q(sp).DecideAsync(c, cancelled.Lines.Single().QualityCheckId!.Value,
            new("approve", null), default)));

        // Списки: открытых нет, закрытые — обе.
        var open = await As(org.Keeper, Keeper, (sp, c) => A(sp).SearchAsync(c, new ProcurementArrivalSearch(SourceId: tender), default));
        var done = await As(org.Keeper, Keeper, (sp, c) => A(sp).SearchAsync(c, new ProcurementArrivalSearch("done", SourceId: tender), default));
        Assert.Equal((0, 2), (open.Total, done.Total));
    }

    [PostgresFact]
    public async Task Home_shows_each_role_its_part_of_the_truck_path()
    {
        var org = await SeedAsync();
        var flour = await FlourAsync(org);
        var tender = await TenderAsync(org, new ProcurementTenderLineInput($"Мука {org.S}", "упак", 40, 310_000, flour));
        var line = (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, tender, 10, default))).Single().Items[0].LineId;
        Task<ProcurementHomeView> Home(User user, string[] permissions) => As(user, permissions, (sp, c) => sp.GetRequiredService<ProcurementHomeService>().GetAsync(c, default));

        // До приезда: ближайшая поставка — у кладовщика (с «Машина приехала»), аналитика и финансиста маршрута; сотруднику — ничего.
        var keeper = await Home(org.Keeper, Keeper);
        Assert.True(keeper.Roles.Warehouse && keeper.SeesDeliveries && keeper.Stock is not null && keeper.Quality is null);
        Assert.Contains(keeper.Upcoming, d => d.SourceId == tender && d.CanArrive && d.DaysLeft == 1);
        Assert.Contains((await Home(org.Analyst, Analyst)).Upcoming, d => d.SourceId == tender && !d.CanArrive);
        Assert.True((await Home(org.Financier, Employee)).SeesDeliveries);
        var employee = await Home(org.Buyer, Employee);
        Assert.False(employee.SeesDeliveries);
        Assert.Empty(employee.Upcoming);

        // Машина приехала: у кладовщика — на приёмке, у лаборатории — проверка «в машине» на её шаге.
        var id = await As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, new(tender, [Ln(line, 40, "H-1", FieldClock.Today.AddDays(-2))], VehicleNo: "30 C 555 DA"), default));
        Assert.Contains((await Home(org.Keeper, Keeper)).Arrivals, a => a.Id == id && a.Status == ProcurementArrivalStatus.Quality);
        var lab = await Home(org.Lab, Lab);
        Assert.True(lab.Quality!.InTrucks >= 1 && lab.Quality.AtMyStep >= 1);
        var check = lab.Quality.Checks.First(c => c.ArrivalId == id);
        Assert.True(check.AtMyStep);
        Assert.Equal((2_000m, (string?)null), (check.Received, check.BatchNumber));

        // Решено и принято: у снабжения и аналитика — «принято на склад» за неделю.
        await CheckAsync(org, check.Id, 13m);
        await Do(org.Lab, Lab, (sp, c) => Q(sp).DecideAsync(c, check.Id, new("approve", null), default));
        var arrival = await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, id, default));
        await As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, id, new([Ac(arrival.Lines[0].Id, 40, 1_995)], Version: arrival.Version), default));
        foreach (var (user, permissions) in new[] { (org.Buyer, Supply), (org.Analyst, Analyst) })
        {
            var home = await Home(user, permissions);
            Assert.Contains(home.Accepted, a => a.Id == id && a.AcceptedTotal == 1_995m * 6_200);
            Assert.True(home.AcceptedAmount >= 1_995m * 6_200);
            Assert.DoesNotContain(home.Arrivals, a => a.Id == id);
            Assert.DoesNotContain(home.Upcoming, d => d.SourceId == tender); // по тендеру поставлено всё
        }

        Assert.NotNull((await Home(org.Buyer, Supply)).Requests.ToBuy);
    }

    [PostgresFact]
    public async Task Bought_raw_material_can_wait_for_the_truck_and_lands_reserved_for_the_request()
    {
        var org = await SeedAsync();
        var flour = await FlourAsync(org);
        var sugar = await SugarAsync(org);

        // Утверждённая заявка аналитика на сырьё: мука 2 000 кг и сахар 500 кг.
        var requestId = Guid.Empty;
        Guid flourLine, sugarLine;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var department = await db.Users.Where(u => u.Id == org.Analyst.Id).Select(u => u.DepartmentId!.Value).SingleAsync();
            var r = new ProcurementRequest
            {
                Number = $"PR-T-{org.S}", Title = $"Сырьё на неделю {org.S}", Kind = ProcurementRequestKind.Raw, DepartmentId = department, InitiatorId = org.Analyst.Id,
                Status = ProcurementRequestStatus.Approved, SubmittedAt = DateTimeOffset.UtcNow, ApprovedAt = DateTimeOffset.UtcNow,
            };
            r.Items.Add(new ProcurementRequestItem { LineNo = 1, Name = $"Мука {org.S}", ItemId = flour, CategoryId = org.Raw, UnitCode = "кг", Quantity = 2_000, EstimatedPrice = 6_200,
                Status = ProcurementItemStatus.ToPurchase });
            r.Items.Add(new ProcurementRequestItem { LineNo = 2, Name = $"Сахар {org.S}", ItemId = sugar, CategoryId = org.Raw, UnitCode = "кг", Quantity = 500, EstimatedPrice = 9_000,
                Status = ProcurementItemStatus.ToPurchase });
            db.ProcurementRequests.Add(r);
            await db.SaveChangesAsync();
            (requestId, flourLine, sugarLine) = (r.Id, r.Items.Single(i => i.LineNo == 1).Id, r.Items.Single(i => i.LineNo == 2).Id);
        }

        ProcurementPurchaseService P(IServiceProvider sp) => sp.GetRequiredService<ProcurementPurchaseService>();
        ProcurementPurchaseInput Bought(DateOnly? date, IReadOnlyList<ProcurementBatchInput>? batches = null) =>
            new([new(flourLine, 2_000, 6_300, batches), new(sugarLine, 500, 9_000)], null, $"ООО Мельница {org.S}", "Счёт 77", null, Guid.NewGuid(), date, org.Central);

        // Привезёт поставщик: дата не в прошлом, партии впишет кладовщик при приезде.
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Buyer, Supply, (sp, c) => P(sp).PurchaseAsync(c, requestId, Bought(FieldClock.Today.AddDays(-1)), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Buyer, Supply, (sp, c) => P(sp).PurchaseAsync(c, requestId,
            Bought(FieldClock.Today.AddDays(2), [new("Z-1", null, FieldClock.Today.AddDays(100), 2_000)]), default)));
        var input = Bought(FieldClock.Today.AddDays(2));
        var bought = await As(org.Buyer, Supply, (sp, c) => P(sp).PurchaseAsync(c, requestId, input, default));
        Assert.Equal(bought.OrderId, (await As(org.Buyer, Supply, (sp, c) => P(sp).PurchaseAsync(c, requestId, input, default))).OrderId); // повтор формы
        Assert.Equal(((Guid?)null, FieldClock.Today.AddDays(2), 2_000m * 6_300 + 500m * 9_000), (bought.ReceiptId, bought.DeliveryDate!.Value, bought.Total));

        // Склад не менялся; «к закупке» по заявке больше нет; кладовщику — «ожидается поставка», автору заявки — «привезёт».
        Assert.Equal((0m, 0m), (await OnHandAsync(org, flour), await OnHandAsync(org, sugar)));
        Assert.DoesNotContain((await As(org.Buyer, Supply, (sp, c) => P(sp).ToBuyAsync(c, default))).Requests, x => x.RequestId == requestId);
        Assert.True(await NotifiedAsync(org.Keeper, "Ожидается поставка"));
        Assert.True(await NotifiedAsync(org.Analyst, "Куплено по заявке"));

        // Ожидается покупка: мука — на лабораторию, сахар — сразу; прежняя приёмка по заказу для неё закрыта.
        var expected = (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, bought.OrderId, 10, default))).Single();
        Assert.Equal((ProcurementArrivalService.OrderKind, 2, true), (expected.Kind, expected.DaysLeft!.Value, expected.CanArrive));
        Assert.Equal((ProcurementQualityControl.Lab, ProcurementQualityControl.None), (expected.Items[0].Control, expected.Items[1].Control));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementReceiptService>().CreateAsync(c,
            new ProcurementReceiptInput(bought.OrderId, null, null, []), default)));
        var made = FieldClock.Today.AddDays(-2);
        var id = await As(org.Keeper, Keeper, (sp, c) => A(sp).RegisterAsync(c, new(null, [Ln(expected.Items[0].LineId, 2_000, "P-7", made), Ln(expected.Items[1].LineId, 500)],
            VehicleNo: "40 D 404 DA", OrderId: bought.OrderId), default));
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Buyer, Supply, (sp, c) =>
            sp.GetRequiredService<ProcurementOrderService>().CancelAsync(c, bought.OrderId, "Передумали", default)));

        // Лаборатория допускает муку; кладовщик взвешивает: 1 990 кг муки (недовес 10) и 500 кг сахара.
        var arrival = await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, id, default));
        Assert.Equal((ProcurementArrivalService.OrderKind, bought.OrderNumber, requestId), (arrival.Arrival.SourceKind, arrival.Arrival.SourceNumber, arrival.RequestId!.Value));
        var check = arrival.Lines[0].QualityCheckId!.Value;
        await CheckAsync(org, check, 13.5m);
        await Do(org.Lab, Lab, (sp, c) => Q(sp).DecideAsync(c, check, new("approve", null), default));
        arrival = await As(org.Keeper, Keeper, (sp, c) => A(sp).GetAsync(c, id, default));
        Assert.False(arrival.Lines[0].Weighed); // заявка в кг — места и есть вес
        await As(org.Keeper, Keeper, (sp, c) => A(sp).AcceptAsync(c, id, new([Ac(arrival.Lines[0].Id, 1_990, comment: "Недовес по весам склада"), Ac(arrival.Lines[1].Id, 500)],
            Version: arrival.Version), default));

        // На складе и под заявкой — к выдаче; покупка поставлена частично; автору заявки — «можно получить».
        Assert.Equal((1_990m, 500m), (await OnHandAsync(org, flour), await OnHandAsync(org, sugar)));
        var request = await As(org.Buyer, Supply, (sp, c) => sp.GetRequiredService<ProcurementRequestService>().GetAsync(c, requestId, default));
        Assert.Equal((1_990m, 1_990m, 500m, 500m), (request.Items[0].ReceivedQty, request.Items[0].ReservedLeft, request.Items[1].ReceivedQty, request.Items[1].ReservedLeft));
        Assert.True(await NotifiedAsync(org.Analyst, $"Принято на склад по заявке {request.Number}"));
        Assert.True(await NotifiedAsync(org.Financier, "Принято на склад"));
        var orders = (Func<IServiceProvider, ProcurementOrderService>)(sp => sp.GetRequiredService<ProcurementOrderService>());
        var order = await As(org.Buyer, Supply, (sp, c) => orders(sp).GetAsync(c, bought.OrderId, default));
        Assert.Equal((ProcurementOrderStatus.PartiallyDelivered, false), (order.Status, order.Can.Receive));
        var batch = await DbAsync(db => db.ProcurementBatches.AsNoTracking().SingleAsync(b => b.ItemId == flour));
        Assert.Equal((ProcurementBatchQuality.Approved, 6_300m), (batch.QualityStatus, batch.UnitCost!.Value));

        // Недовезённые 10 кг: снабжение закрывает остаток — покупки больше не ждут.
        Assert.Equal(10m, (await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, bought.OrderId, 10, default))).Single().Items[0].Left);
        await Do(org.Buyer, Supply, (sp, c) => orders(sp).CloseRemainderAsync(c, bought.OrderId, "Поставщик не довезёт", default));
        Assert.Empty(await As(org.Keeper, Keeper, (sp, c) => A(sp).ExpectedAsync(c, bought.OrderId, 10, default)));
    }
}

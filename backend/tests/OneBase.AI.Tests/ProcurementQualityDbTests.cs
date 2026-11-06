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
/// Контроль качества сырья на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): партия позиции с контролем после прихода —
/// «на контроле», не выдаётся и не входит в «доступно»; проверка идёт по порядку — документы, лаборатория (проба списывается), решение;
/// решает только назначенный (администратор и лаборант — нет); брак — с комментарием и возвратом поставщику, он виден в рейтинге
/// поставщика; блокировка и «срок не указан»; сторно прихода отменяет проверку; загрузка журнала ставит новую поставку на контроль.
/// После каждого сценария — остаток позиции = сумма её партий.
/// </summary>
public class ProcurementQualityDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];
    private static readonly string[] Lab = [Permissions.ProcurementRequest, Permissions.QualityCheck];
    private static readonly string[] Boss = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] Admin = [.. Permissions.All];

    private sealed record Org(string S, Department Dept, User Keeper, User Lab, User Decider, User Admin, User Boss, Guid Category, Guid Central, Guid Supplier);

    private DateOnly Today => FieldClock.LocalDate(fixture.Clock.Now ?? DateTimeOffset.UtcNow);

    private async Task<Org> SeedAsync()
    {
        fixture.Clock.Now = FieldClock.DayStartUtc(FieldClock.Today).AddHours(12);
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"qc-{s}", Name = $"Цех {s}" };
        Role RoleWith(string name, string permission)
        {
            var role = new Role { Name = name };
            role.Permissions.Add(new RolePermission { Code = permission });
            return role;
        }

        var keeperRole = RoleWith($"Кладовщик {s}", Permissions.ProcurementWarehouse);
        var labRole = RoleWith($"Лаборатория {s}", Permissions.QualityCheck);
        // Технический администратор: все права по роли «Администратор» — в назначенные по качеству не попадает.
        var adminRole = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == SystemRoles.Admin) ?? db.Roles.Add(new Role { Name = SystemRoles.Admin }).Entity;
        if (adminRole.Permissions.All(p => p.Code != Permissions.QualityCheck))
        {
            adminRole.Permissions.Add(new RolePermission { Code = Permissions.QualityCheck });
        }

        User Person(string login, string name, Role? role)
        {
            var user = new User { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = dept };
            if (role is not null)
            {
                user.Roles.Add(new UserRole { Role = role });
            }

            return user;
        }

        var keeper = Person("qc-kpr", "Кладовщик", keeperRole);
        var lab = Person("qc-lab", "Лаборант", labRole);
        var decider = Person("qc-dec", "Завлаб", labRole);
        var admin = Person("qc-adm", "Админ", adminRole);
        var boss = Person("qc-boss", "Директор", null);
        db.Users.AddRange(keeper, lab, decider, admin, boss);
        var supplier = new ProcurementSupplier { Name = $"Поставщик {s}" };
        db.ProcurementSuppliers.Add(supplier);
        await db.SaveChangesAsync();
        var category = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync();
        var central = await db.ProcurementWarehouses.Where(w => w.IsDefault).Select(w => w.Id).SingleAsync();
        var org = new Org(s, dept, keeper, lab, decider, admin, boss, category, central, supplier.Id);

        // Решает по качеству — только назначенный: администратора и кладовщика назначить нельзя.
        await Assert.ThrowsAsync<FieldValidationException>(() => Settings(org, [admin.Id]));
        await Assert.ThrowsAsync<FieldValidationException>(() => Settings(org, [keeper.Id]));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Act(org.Lab, Lab, (q, c) => q.UpdateSettingsAsync(c, new([org.Lab.Id]), default)));
        await Settings(org, [decider.Id]);
        return org;
    }

    private Task Settings(Org org, Guid[] ids) => As(org.Boss, Boss, async (sp, c) =>
    {
        await Q(sp).UpdateSettingsAsync(c, new ProcurementQualitySettingsInput(ids), default);
        return true;
    });

    private async Task<T> As<T>(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider, ctx);
    }

    private static ProcurementQualityService Q(IServiceProvider sp) => sp.GetRequiredService<ProcurementQualityService>();

    private Task<T> Stock<T>(Org org, Func<ProcurementStockService, ProcurementContext, Task<T>> act) =>
        As(org.Keeper, Keeper, (sp, c) => act(sp.GetRequiredService<ProcurementStockService>(), c));

    /// <summary>Действие лаборанта, назначенного или администратора с сервисом качества (результат не нужен).</summary>
    private Task Act(User user, string[] permissions, Func<ProcurementQualityService, ProcurementContext, Task> act) =>
        As(user, permissions, async (sp, c) =>
        {
            await act(Q(sp), c);
            return true;
        });

    private Task<Guid> ItemAsync(Org org, string name, ProcurementQualityControl control, bool batches = true) =>
        Stock(org, (s, c) => s.CreateItemAsync(c, new ProcurementCatalogInput($"QC-{Guid.NewGuid():N}"[..14], $"{name} {org.S}", org.Category, "кг", null, null, 0,
            TracksBatches: batches, QualityControl: control), default));

    private static ProcurementBatchInput Batch(string no, DateOnly expires, decimal qty, DateOnly? made = null) => new(no, made, expires, qty);

    private Task<Guid> ReceiveAsync(Org org, Guid item, ProcurementBatchInput batch, Guid? supplier = null) =>
        Stock(org, (s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(item, batch.Quantity, 8_000, Batches: [batch])], "Приход", SupplierId: supplier), default));

    private Task<Guid> IssueAsync(Org org, Guid item, decimal qty) =>
        Stock(org, (s, c) => s.IssueAsync(c, new ProcurementStockOperationInput([new(item, qty)], null, DepartmentId: org.Dept.Id), default));

    private async Task<ProcurementBatch> BatchAsync(Guid item, string no)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementBatches.AsNoTracking().SingleAsync(b => b.ItemId == item && b.SupplierBatchNo == no);
    }

    private async Task<ProcurementQualityCheck> CheckAsync(Guid batch)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementQualityChecks.AsNoTracking().OrderByDescending(c => c.CreatedAt)
            .FirstAsync(c => c.BatchId == batch);
    }

    private Task<ProcurementStockRow> RowAsync(Org org, string name) =>
        Stock(org, async (s, c) => Assert.Single((await s.StockAsync(c, new ProcurementStockSearch($"{name} {org.S}"), default)).Items));

    private async Task<bool> NotifiedAsync(User user, string title)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().UserNotifications.AnyAsync(n => n.RecipientId == user.Id && n.Title.StartsWith(title));
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
    public async Task A_controlled_batch_waits_for_documents_lab_and_a_designated_decision()
    {
        var org = await SeedAsync();
        try
        {
            var item = await ItemAsync(org, "Мука", ProcurementQualityControl.Lab);
            // Спецификация — лаборатория: документы и показатели; кладовщик её не правит, норма «от» не больше «до».
            ProcurementQualitySpecsInput Spec(params ProcurementQualitySpecInput[] lines) => new(lines);
            var spec = Spec(
                new(null, ProcurementQualitySpecKind.Document, "Сертификат качества"),
                new(null, ProcurementQualitySpecKind.Document, "Декларация", Required: false),
                new(null, ProcurementQualitySpecKind.Parameter, "Влажность", "%", Max: 14),
                new(null, ProcurementQualitySpecKind.Parameter, "Запах", Expected: "без посторонних запахов"));
            await Assert.ThrowsAsync<FieldForbiddenException>(() => Act(org.Keeper, Keeper, (q, c) => q.UpdateSpecsAsync(c, item, spec, default)));
            await Assert.ThrowsAsync<FieldValidationException>(() =>
                Act(org.Lab, Lab, (q, c) => q.UpdateSpecsAsync(c, item, Spec(new ProcurementQualitySpecInput(null, ProcurementQualitySpecKind.Parameter, "Белок", "%", 12, 10)), default)));
            await Act(org.Lab, Lab, (q, c) => q.UpdateSpecsAsync(c, item, spec, default));

            // Приход — партия «на контроле»: не выдаётся и не входит в «доступно»; лаборатории и назначенному — уведомление.
            await ReceiveAsync(org, item, Batch("M-1", Today.AddDays(120), 100));
            var batch = await BatchAsync(item, "M-1");
            Assert.Equal(ProcurementBatchQuality.Pending, batch.QualityStatus);
            var check = await CheckAsync(batch.Id);
            Assert.StartsWith("QC-", check.Number);
            Assert.Equal(ProcurementQualityCheckStatus.Documents, check.Status);
            Assert.True(await NotifiedAsync(org.Lab, "Партия на контроле качества") && await NotifiedAsync(org.Decider, "Партия на контроле качества"));
            var row = await RowAsync(org, "Мука");
            Assert.Equal((100m, 100m, 0m), (row.OnHand, row.OnControl, row.Available));
            var issue = await Assert.ThrowsAsync<FieldConflictException>(() => IssueAsync(org, item, 1));
            Assert.Contains("на контроле 100", issue.Message);
            var reserve = await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementRequestService>().CreateAsync(c,
                new ProcurementRequestInput("Мука для цеха", null, ProcurementPriority.Normal, null, "Выпуск", null, [new(null, null, null, null, 1, 9_000, null, item, 1)], true),
                default))); // под заявку не резервируется
            Assert.Contains("доступно 0", reserve.Message);

            // По порядку: допустить до проверок нельзя, лаборатория — только после документов в порядке.
            await Assert.ThrowsAsync<FieldConflictException>(() => Act(org.Decider, Lab, (q, c) => q.DecideAsync(c, check.Id, new("approve", "Всё хорошо"), default)));
            await Assert.ThrowsAsync<FieldConflictException>(() => Act(org.Lab, Lab, (q, c) => q.LabAsync(c, check.Id, new([]), default)));
            var specs = (await As(org.Lab, Lab, (sp, c) => Q(sp).SpecsAsync(c, item, default))).Lines.ToDictionary(l => l.Name, l => l.Id);
            await Assert.ThrowsAsync<FieldValidationException>(() => Act(org.Lab, Lab, (q, c) => q.DocumentsAsync(c, check.Id, new([]), default))); // сертификат не отмечен
            await Act(org.Lab, Lab, (q, c) => q.DocumentsAsync(c, check.Id, new([new(specs["Сертификат качества"], true)]), default));
            Assert.Equal(ProcurementQualityCheckStatus.Lab, (await CheckAsync(batch.Id)).Status);

            // Лаборатория: влажность вне нормы — «не соответствует»; проба 0,5 кг списывается с партии.
            await Assert.ThrowsAsync<FieldValidationException>(() => Act(org.Lab, Lab, (q, c) => q.LabAsync(c, check.Id, new([new(specs["Запах"], true)]), default)));
            await Act(org.Lab, Lab, (q, c) => q.LabAsync(c, check.Id,
                new([new(specs["Влажность"], Value: 15.2m), new(specs["Запах"], true)], Note: "Влажность выше нормы", SampleQty: 0.5m), default));
            check = await CheckAsync(batch.Id);
            Assert.Equal((ProcurementQualityCheckStatus.Decision, false, 0.5m), (check.Status, check.LabOk, check.SampleQty));
            Assert.Equal(99.5m, (await RowAsync(org, "Мука")).OnHand);
            var sample = (await Stock(org, (s, c) => s.MovementsAsync(c, new ProcurementMovementSearch(item, Kind: "WriteOff"), default))).Items.Single();
            Assert.Equal((check.Id, -0.5m), (sample.QualityCheckId!.Value, sample.Quantity));
            await Assert.ThrowsAsync<FieldConflictException>(() => Stock(org, (s, c) => s.ReverseAsync(c, sample.DocumentId, new ProcurementReversalInput("Ошибка"), default)));

            // Решает только назначенный: администратор со всеми правами и лаборант — нет; с отклонениями — только с комментарием.
            await Assert.ThrowsAsync<FieldForbiddenException>(() => Act(org.Admin, Admin, (q, c) => q.DecideAsync(c, check.Id, new("approve", "Под мою ответственность"), default)));
            await Assert.ThrowsAsync<FieldForbiddenException>(() => Act(org.Lab, Lab, (q, c) => q.DecideAsync(c, check.Id, new("approve", "Годится"), default)));
            await Assert.ThrowsAsync<FieldValidationException>(() => Act(org.Decider, Lab, (q, c) => q.DecideAsync(c, check.Id, new("approve", null), default)));
            var card = await As(org.Decider, Lab, (sp, c) => Q(sp).CardAsync(c, check.Id, default));
            Assert.True(card.Can.Approve && card.Can.Reject);
            Assert.Equal([true, null, false, true], card.Results.Select(r => r.Passed));
            await Act(org.Decider, Lab, (q, c) => q.DecideAsync(c, check.Id, new("approve", "Влажность в пределах допуска технолога", card.Version), default));
            await Assert.ThrowsAsync<FieldConflictException>(() => Act(org.Decider, Lab, (q, c) => q.DecideAsync(c, check.Id, new("reject", "Передумал"), default)));

            // Допущена — выдаётся; кладовщику — уведомление.
            Assert.Equal(ProcurementBatchQuality.Approved, (await BatchAsync(item, "M-1")).QualityStatus);
            await IssueAsync(org, item, 10);
            row = await RowAsync(org, "Мука");
            Assert.Equal((89.5m, 0m, 89.5m), (row.OnHand, row.OnControl, row.Available));
            Assert.True(await NotifiedAsync(org.Keeper, "Партия допущена"));
            var nav = await As(org.Lab, Lab, (sp, c) => Q(sp).NavAsync(c, default));
            Assert.True(nav.Sees && nav.Quality && !nav.Decides);
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task A_rejected_batch_is_returned_to_the_supplier_and_counts_in_the_rating()
    {
        var org = await SeedAsync();
        try
        {
            var item = await ItemAsync(org, "Сахар", ProcurementQualityControl.Documents);
            await ReceiveAsync(org, item, Batch("S-1", Today.AddDays(200), 40), org.Supplier);
            var batch = await BatchAsync(item, "S-1");
            var check = await CheckAsync(batch.Id);

            // Без документов в спецификации — итог целиком; «не в порядке» — с пояснением, и сразу к решению.
            await Assert.ThrowsAsync<FieldValidationException>(() => Act(org.Lab, Lab, (q, c) => q.DocumentsAsync(c, check.Id, new([]), default)));
            await Assert.ThrowsAsync<FieldValidationException>(() => Act(org.Lab, Lab, (q, c) => q.DocumentsAsync(c, check.Id, new([], false), default)));
            await Act(org.Lab, Lab, (q, c) => q.DocumentsAsync(c, check.Id, new([], false, "Номер партии в сертификате не совпадает"), default));
            Assert.Equal(ProcurementQualityCheckStatus.Decision, (await CheckAsync(batch.Id)).Status);
            await Assert.ThrowsAsync<FieldValidationException>(() => Act(org.Decider, Lab, (q, c) => q.DecideAsync(c, check.Id, new("reject", " "), default)));
            await Act(org.Decider, Lab, (q, c) => q.DecideAsync(c, check.Id, new("reject", "Документы не соответствуют партии"), default));

            // Забракованную не выдать; вернуть поставщику — только забракованную или заблокированную.
            Assert.Equal(ProcurementBatchQuality.Rejected, (await BatchAsync(item, "S-1")).QualityStatus);
            Assert.Contains("забраковано", (await Assert.ThrowsAsync<FieldConflictException>(() => IssueAsync(org, item, 1))).Message);
            Assert.Equal((40m, 40m, 0m), ((await RowAsync(org, "Сахар")).OnHand, (await RowAsync(org, "Сахар")).Blocked, (await RowAsync(org, "Сахар")).Available));
            var plain = await ItemAsync(org, "Соль", ProcurementQualityControl.None);
            await ReceiveAsync(org, plain, Batch("N-1", Today.AddDays(300), 5));
            var good = await BatchAsync(plain, "N-1");
            Assert.Equal(ProcurementBatchQuality.Approved, good.QualityStatus);
            await Assert.ThrowsAsync<FieldConflictException>(() =>
                Stock(org, (s, c) => s.SupplierReturnAsync(c, new ProcurementStockOperationInput([new(plain, 5, BatchId: good.Id)], "Не нужна"), default)));
            await Assert.ThrowsAsync<FieldValidationException>(() =>
                Stock(org, (s, c) => s.SupplierReturnAsync(c, new ProcurementStockOperationInput([new(item, 40, BatchId: batch.Id)], null), default)));
            var back = await Stock(org, (s, c) => s.SupplierReturnAsync(c, new ProcurementStockOperationInput([new(item, 40, BatchId: batch.Id)], "Брак по документам"), default));
            Assert.Equal(0m, (await RowAsync(org, "Сахар")).OnHand);
            using (var scope = fixture.Services.CreateScope())
            {
                var doc = await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().ProcurementStockDocuments.AsNoTracking().SingleAsync(d => d.Id == back);
                Assert.Equal((ProcurementStockDocumentKind.SupplierReturn, org.Supplier), (doc.Kind, doc.SupplierId!.Value));
                Assert.StartsWith("SRT-", doc.Number);
            }

            await Assert.ThrowsAsync<FieldConflictException>(() => Stock(org, (s, c) => s.ReverseAsync(c, back, new ProcurementReversalInput("Вернули обратно"), default)));

            // Рейтинг поставщика: решение по партии — как строка приёмки, брак — проблема.
            var supplier = await As(org.Boss, Boss, (sp, c) => sp.GetRequiredService<ProcurementSupplierService>().GetAsync(c, org.Supplier, default));
            Assert.Equal((1, 1, 0m), (supplier.Rating.QualityChecks, supplier.Rating.RejectedBatches, supplier.Rating.Claims!.Value));
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Blocking_stops_issue_and_a_batch_entered_without_expiry_waits_for_a_date()
    {
        var org = await SeedAsync();
        try
        {
            var item = await ItemAsync(org, "Какао", ProcurementQualityControl.None);
            await ReceiveAsync(org, item, Batch("C-1", Today.AddDays(90), 20));
            var batch = await BatchAsync(item, "C-1");

            // Блокирует назначенный, с причиной; заблокированную не выдать; снял — выдаётся.
            await Assert.ThrowsAsync<FieldForbiddenException>(() => Act(org.Lab, Lab, (q, c) => q.BlockAsync(c, batch.Id, new("Жалоба цеха"), default)));
            await Assert.ThrowsAsync<FieldValidationException>(() => Act(org.Decider, Lab, (q, c) => q.BlockAsync(c, batch.Id, new(null), default)));
            await Act(org.Decider, Lab, (q, c) => q.BlockAsync(c, batch.Id, new("Жалоба цеха на комки"), default));
            Assert.Contains("заблокировано 20", (await Assert.ThrowsAsync<FieldConflictException>(() => IssueAsync(org, item, 1))).Message);
            await Act(org.Decider, Lab, (q, c) => q.UnblockAsync(c, batch.Id, new("Повторный анализ в норме"), default));
            await IssueAsync(org, item, 5);

            // «Срок не указан» — только при вводе партий: партия заблокирована, пока назначенный не укажет срок.
            var old = await ItemAsync(org, "Крахмал", ProcurementQualityControl.None, batches: false);
            await Stock(org, (s, c) => s.ReceiptAsync(c, new ProcurementStockOperationInput([new(old, 30)], null), default));
            var made = Today.AddDays(-40);
            await Assert.ThrowsAsync<FieldValidationException>(() => ReceiveAsync(org, item, new ProcurementBatchInput("C-2", null, null, 3, ExpiryUnknown: true)));
            await Stock(org, (s, c) => s.BatchEntryAsync(c, new ProcurementBatchEntryInput(old, [new(org.Central, [new("K-0", made, null, 30, ExpiryUnknown: true)])]), default));
            var unknown = await BatchAsync(old, "K-0");
            Assert.Equal((ProcurementBatchQuality.Blocked, (DateOnly?)null, ProcurementQualityRules.UnknownExpiry), (unknown.QualityStatus, unknown.ExpiresOn, unknown.BlockReason));
            var row = await RowAsync(org, "Крахмал");
            Assert.Equal((30m, 30m, 0m), (row.OnHand, row.Blocked, row.Available));
            using (var scope = fixture.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<ProcurementReminderService>().RemindAsync(Today, default); // партия без срока не мешает напоминаниям
            }

            await Assert.ThrowsAsync<FieldValidationException>(() => Act(org.Decider, Lab, (q, c) => q.UnblockAsync(c, unknown.Id, new("Срок по документам"), default)));
            await Assert.ThrowsAsync<FieldValidationException>(() =>
                Act(org.Decider, Lab, (q, c) => q.UnblockAsync(c, unknown.Id, new("Срок по документам", made.AddDays(-1)), default)));
            await Act(org.Decider, Lab, (q, c) => q.UnblockAsync(c, unknown.Id, new("Срок по документам", Today.AddDays(60)), default));
            unknown = await BatchAsync(old, "K-0");
            Assert.Equal((ProcurementBatchQuality.Approved, Today.AddDays(60)), (unknown.QualityStatus, unknown.ExpiresOn!.Value));
            await IssueAsync(org, old, 30);
            var card = await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementBatchService>().CardAsync(c, unknown.Id, default));
            Assert.Equal(["Блокировка снята, срок годности — " + Today.AddDays(60).ToString("dd.MM.yyyy")], card.History!.Select(h => h.Text));
            await InvariantAsync();
        }
        finally
        {
            fixture.Clock.Now = null;
        }
    }

    [PostgresFact]
    public async Task Reversing_a_receipt_cancels_its_check_and_an_import_puts_a_new_delivery_under_control()
    {
        var org = await SeedAsync();
        try
        {
            var item = await ItemAsync(org, "Масло", ProcurementQualityControl.Documents);
            var receipt = await ReceiveAsync(org, item, Batch("O-1", Today.AddDays(100), 10));
            var batch = await BatchAsync(item, "O-1");
            await Stock(org, (s, c) => s.ReverseAsync(c, receipt, new ProcurementReversalInput("Не та накладная"), default));
            Assert.Equal(ProcurementQualityCheckStatus.Cancelled, (await CheckAsync(batch.Id)).Status);
            Assert.Equal(0, (await As(org.Lab, Lab, (sp, c) => Q(sp).ListAsync(c, new ProcurementQualitySearch(ItemId: item), default))).Total);

            // Загрузка журнала: первая — начальные остатки (уже на складе, без контроля), новая поставка — на контроль; выдать её до решения нельзя.
            var sku = (await RowAsync(org, "Масло")).Sku;
            var day = Today;
            string D(DateOnly d) => d.ToString("dd.MM.yyyy");
            object[][] rows = [["", D(day.AddDays(-10)), "Приход", "P-1", sku, $"Масло {org.S}", "кг", 50, "", D(day.AddDays(90))]];
            var first = await ImportAsync(org, rows);
            Assert.Equal("opening", first.Mode);
            Assert.Equal(ProcurementBatchQuality.Approved, (await BatchAsync(item, "P-1")).QualityStatus);
            object[][] more =
            [
                .. rows,
                ["", D(day.AddDays(-1)), "Приход", "P-2", sku, $"Масло {org.S}", "кг", 20, "", D(day.AddDays(150))],
                ["", D(day), "Расход", "P-2", sku, $"Масло {org.S}", "кг", 5, $"Цех {org.S}", ""],
            ];
            var next = await ImportAsync(org, more);
            Assert.Equal(1, next.Posted);
            Assert.Contains(next.Errors, e => e.Contains("строка загрузится в следующий раз"));
            var p2 = await BatchAsync(item, "P-2");
            Assert.Equal(ProcurementBatchQuality.Pending, p2.QualityStatus);
            Assert.Equal(ProcurementQualityCheckStatus.Documents, (await CheckAsync(p2.Id)).Status);
            Assert.True(await NotifiedAsync(org.Lab, "Партия на контроле качества"));
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
}

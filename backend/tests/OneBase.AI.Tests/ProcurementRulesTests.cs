using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.AI.Tests;

/// <summary>
/// Правила заявок на закупку (раздел «Заявки», снабжение): кто видит заявку, переходы статусов, обязательный комментарий, проверка
/// количества и цены, номера, права ролей.
/// </summary>
public class ProcurementRulesTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid MyDept = Guid.NewGuid();
    private static readonly Guid OtherDept = Guid.NewGuid();

    private static ProcurementContext Ctx(IEnumerable<string> permissions, params Guid[] heads) =>
        new(Me, permissions.ToHashSet(), MyDept, heads.ToHashSet());

    private static ProcurementRequest Request(Guid initiator, Guid department, ProcurementRequestStatus status) =>
        new() { Number = "PR-2026-00001", Title = "Бумага", InitiatorId = initiator, DepartmentId = department, Status = status };

    [Fact]
    public void Employee_sees_only_own_requests_and_drafts_stay_private()
    {
        var employee = Ctx([Permissions.ProcurementRequest]);
        var colleague = Guid.NewGuid();

        Assert.True(ProcurementRules.CanView(employee, Request(Me, MyDept, ProcurementRequestStatus.Draft)));
        Assert.False(ProcurementRules.CanView(employee, Request(colleague, MyDept, ProcurementRequestStatus.Submitted))); // коллега по отделу — нет

        var head = Ctx([Permissions.ProcurementRequest], MyDept);
        Assert.True(ProcurementRules.CanView(head, Request(colleague, MyDept, ProcurementRequestStatus.Submitted)));
        Assert.False(ProcurementRules.CanView(head, Request(colleague, MyDept, ProcurementRequestStatus.Draft))); // черновик — только инициатору
        Assert.False(ProcurementRules.CanView(head, Request(colleague, OtherDept, ProcurementRequestStatus.Submitted))); // чужой отдел

        var supply = Ctx([Permissions.ProcurementRead, Permissions.ProcurementManage]);
        Assert.True(ProcurementRules.CanView(supply, Request(colleague, OtherDept, ProcurementRequestStatus.Submitted)));
        Assert.False(ProcurementRules.CanView(supply, Request(colleague, OtherDept, ProcurementRequestStatus.Draft)));
    }

    [Theory]
    [InlineData(ProcurementRequestStatus.Draft, ProcurementRequestStatus.Submitted, true, false, true)]
    [InlineData(ProcurementRequestStatus.RevisionRequired, ProcurementRequestStatus.Submitted, true, false, true)]
    [InlineData(ProcurementRequestStatus.Draft, ProcurementRequestStatus.Submitted, false, true, false)] // снабжение не отправляет за инициатора
    [InlineData(ProcurementRequestStatus.Submitted, ProcurementRequestStatus.DirectorApproval, false, true, true)]
    [InlineData(ProcurementRequestStatus.Submitted, ProcurementRequestStatus.DirectorApproval, true, false, false)] // инициатор сам директору не передаёт
    [InlineData(ProcurementRequestStatus.Submitted, ProcurementRequestStatus.RevisionRequired, false, true, true)]
    [InlineData(ProcurementRequestStatus.DirectorApproval, ProcurementRequestStatus.Cancelled, true, false, true)]
    [InlineData(ProcurementRequestStatus.Submitted, ProcurementRequestStatus.Cancelled, false, true, false)]
    [InlineData(ProcurementRequestStatus.Approved, ProcurementRequestStatus.Cancelled, true, true, false)] // утверждённую не отменить
    [InlineData(ProcurementRequestStatus.Submitted, ProcurementRequestStatus.Approved, true, true, false)] // утверждает только директор
    [InlineData(ProcurementRequestStatus.DirectorApproval, ProcurementRequestStatus.Rejected, true, true, false)]
    [InlineData(ProcurementRequestStatus.DirectorApproval, ProcurementRequestStatus.Approved, true, true, false)] // снабжение и инициатор — нет
    [InlineData(ProcurementRequestStatus.Cancelled, ProcurementRequestStatus.Submitted, true, true, false)]
    [InlineData(ProcurementRequestStatus.HeadApproval, ProcurementRequestStatus.Cancelled, true, false, true)] // инициатор отменяет до утверждения
    [InlineData(ProcurementRequestStatus.FinanceApproval, ProcurementRequestStatus.Approved, true, true, false)] // ни инициатор, ни снабжение не согласуют
    public void Transitions_follow_the_roles(ProcurementRequestStatus from, ProcurementRequestStatus to, bool owner, bool manages, bool allowed)
    {
        Assert.Equal(allowed, ProcurementRules.CanTransition(from, to, owner, manages));
    }

    [Theory]
    [InlineData(ProcurementRequestStatus.HeadApproval, ProcurementRequestStatus.Approved, true)]
    [InlineData(ProcurementRequestStatus.FinanceApproval, ProcurementRequestStatus.Rejected, true)]
    [InlineData(ProcurementRequestStatus.DirectorApproval, ProcurementRequestStatus.Approved, true)]
    [InlineData(ProcurementRequestStatus.DirectorApproval, ProcurementRequestStatus.RevisionRequired, true)]
    [InlineData(ProcurementRequestStatus.Submitted, ProcurementRequestStatus.Approved, false)] // заявку прежнего шага сначала снабжение передаёт директору
    [InlineData(ProcurementRequestStatus.Approved, ProcurementRequestStatus.Rejected, false)]
    [InlineData(ProcurementRequestStatus.Submitted, ProcurementRequestStatus.DirectorApproval, false)] // согласующий не обрабатывает за снабжение
    public void Approver_decides_only_the_step_waiting_for_him(ProcurementRequestStatus from, ProcurementRequestStatus to, bool allowed)
    {
        Assert.Equal(allowed, ProcurementRules.CanTransition(from, to, owner: false, manages: false, decides: true));
        Assert.False(ProcurementRules.CanTransition(from, to, owner: false, manages: true, decides: false) && from != ProcurementRequestStatus.Submitted);
    }

    [Fact]
    public void Each_step_is_decided_by_its_own_approver()
    {
        var colleague = Guid.NewGuid();
        var head = Ctx([Permissions.ProcurementRequest], MyDept);
        var financier = new ProcurementContext(Me, new HashSet<string> { Permissions.ProcurementRequest }, OtherDept, new HashSet<Guid>(), IsFinancier: true);
        var director = new ProcurementContext(Me, new HashSet<string>(), OtherDept, new HashSet<Guid>(), IsDirector: true, ApprovesAsDirector: true);
        var roleOnly = new ProcurementContext(Me, new HashSet<string>(), OtherDept, new HashSet<Guid>(), IsDirector: true, ApprovesAsDirector: false);

        Assert.True(ProcurementRules.Decides(head, Request(colleague, MyDept, ProcurementRequestStatus.HeadApproval)));
        Assert.False(ProcurementRules.Decides(head, Request(colleague, OtherDept, ProcurementRequestStatus.HeadApproval))); // чужой отдел
        Assert.False(ProcurementRules.Decides(head, Request(colleague, MyDept, ProcurementRequestStatus.FinanceApproval)));
        Assert.True(ProcurementRules.Decides(financier, Request(colleague, MyDept, ProcurementRequestStatus.FinanceApproval)));
        Assert.False(ProcurementRules.Decides(financier, Request(colleague, MyDept, ProcurementRequestStatus.DirectorApproval)));
        Assert.True(ProcurementRules.Decides(director, Request(colleague, MyDept, ProcurementRequestStatus.DirectorApproval)));
        Assert.False(ProcurementRules.Decides(roleOnly, Request(colleague, MyDept, ProcurementRequestStatus.DirectorApproval))); // директор маршрута выбран другой
        // Финансист и директор маршрута видят отправленные заявки всех отделов (решают по ним), черновики — нет.
        Assert.True(ProcurementRules.CanView(financier, Request(colleague, MyDept, ProcurementRequestStatus.HeadApproval)));
        Assert.False(ProcurementRules.CanView(financier, Request(colleague, MyDept, ProcurementRequestStatus.Draft)));
        Assert.True(financier.CanOpen);
    }

    [Theory]
    // Заявка сотрудника — руководителю его отдела; заявка руководителя и отдела без руководителя — финансисту.
    [InlineData(false, true, true, ProcurementRequestStatus.HeadApproval)]
    [InlineData(true, true, true, ProcurementRequestStatus.FinanceApproval)]
    [InlineData(false, false, true, ProcurementRequestStatus.FinanceApproval)]
    // Всё со склада: сотрудник — к руководителю; руководитель — утверждена сама (закупки нет).
    [InlineData(false, true, false, ProcurementRequestStatus.HeadApproval)]
    [InlineData(true, true, false, ProcurementRequestStatus.Approved)]
    public void Route_starts_with_the_department_head_unless_the_head_asks(bool initiatorHeads, bool hasHeads, bool purchase, ProcurementRequestStatus first)
    {
        Assert.Equal(first, ProcurementRules.FirstStep(initiatorHeads, hasHeads, purchase));
    }

    [Theory]
    [InlineData(ProcurementRequestStatus.HeadApproval, true, 9_000_000, ProcurementRequestStatus.FinanceApproval)]
    [InlineData(ProcurementRequestStatus.HeadApproval, false, 0, ProcurementRequestStatus.Approved)] // всё со склада — финансисту нечего согласовывать
    [InlineData(ProcurementRequestStatus.FinanceApproval, true, 5_000_000, ProcurementRequestStatus.Approved)] // ровно лимит — без директора
    [InlineData(ProcurementRequestStatus.FinanceApproval, true, 5_000_001, ProcurementRequestStatus.DirectorApproval)]
    [InlineData(ProcurementRequestStatus.DirectorApproval, true, 9_000_000, ProcurementRequestStatus.Approved)]
    public void After_the_financier_only_sums_above_the_limit_go_to_the_director(ProcurementRequestStatus step, bool purchase, decimal total, ProcurementRequestStatus next)
    {
        Assert.Equal(next, ProcurementRules.NextStep(step, purchase, total, 5_000_000));
    }

    [Fact]
    public void Price_is_required_for_the_purchase_part()
    {
        ProcurementRequestItem Item(int line, decimal qty, decimal fromStock, decimal? price) =>
            new() { Name = $"Позиция {line}", LineNo = line, UnitCode = "шт", Quantity = qty, FromStock = fromStock, EstimatedPrice = price };

        Assert.Null(ProcurementRules.MissingPrice([Item(1, 10, 10, null), Item(2, 5, 0, 12_000)])); // всё со склада — цена не нужна
        Assert.Contains("Позиция 2 «Позиция 2»", ProcurementRules.MissingPrice([Item(1, 10, 0, 1_000), Item(2, 5, 0, null)]));
        Assert.NotNull(ProcurementRules.MissingPrice([Item(1, 10, 4, 0)])); // нулевая цена — как без цены
    }

    private static ProcurementApprovedLine Line(Guid id, decimal toPurchase, decimal? price, string name = "Бумага", decimal fromStock = 0) =>
        new(id, name, null, "пачка", toPurchase + fromStock, fromStock, toPurchase, price);

    [Fact]
    public void Material_changes_after_approval_need_the_director_again()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        List<ProcurementApprovedLine> approved = [Line(a, 60, 45_000), Line(b, 5, 300_000, "Картриджи")];

        Assert.Empty(ProcurementRules.MaterialChanges(approved, approved, 0));
        Assert.Empty(ProcurementRules.MaterialChanges(approved, [Line(a, 50, 45_000), Line(b, 5, 290_000, "Картриджи")], 0)); // уменьшение — не существенно
        Assert.Contains("к закупке 60 → 70", ProcurementRules.MaterialChanges(approved, [Line(a, 70, 45_000), approved[1]], 0).Single());
        Assert.Contains("цена", ProcurementRules.MaterialChanges(approved, [Line(a, 60, 46_000), approved[1]], 0).Single());
        Assert.Empty(ProcurementRules.MaterialChanges(approved, [Line(a, 60, 47_000), approved[1]], 5)); // +4,4 % при допуске 5 %
        Assert.Contains("удалена позиция «Картриджи»", ProcurementRules.MaterialChanges(approved, [approved[0]], 0));
        Assert.Contains("добавлена позиция «Папки»", ProcurementRules.MaterialChanges(approved, [.. approved, Line(Guid.NewGuid(), 30, null, "Папки")], 0));
        Assert.Contains("изменён товар", ProcurementRules.MaterialChanges(approved, [Line(a, 60, 45_000, "Бумага А3"), approved[1]], 0).Single());
        // Цены не было при утверждении, появилась — сумма к закупке выросла.
        Assert.NotEmpty(ProcurementRules.MaterialChanges([Line(a, 10, null)], [Line(a, 10, 1_000)], 0));
    }

    [Fact]
    public void Item_status_follows_stock_purchase_and_issue()
    {
        ProcurementRequestItem Item(decimal qty, decimal fromStock, decimal issued) =>
            new() { Name = "Бумага", UnitCode = "пачка", Quantity = qty, FromStock = fromStock, IssuedQty = issued };

        Assert.Equal(ProcurementItemStatus.Pending, ProcurementRules.ItemStatus(Item(100, 40, 0), submitted: false));
        Assert.Equal(ProcurementItemStatus.ToPurchase, ProcurementRules.ItemStatus(Item(100, 40, 0), submitted: true));
        Assert.Equal(ProcurementItemStatus.Reserved, ProcurementRules.ItemStatus(Item(30, 30, 0), submitted: true));
        Assert.Equal(ProcurementItemStatus.PartiallyIssued, ProcurementRules.ItemStatus(Item(100, 40, 40), submitted: true));
        Assert.Equal(ProcurementItemStatus.Issued, ProcurementRules.ItemStatus(Item(30, 30, 30), submitted: true));
        Assert.Equal(60m, Item(100, 40, 0).ToPurchase);
    }

    [Fact]
    public void Warehouse_keeper_sees_only_requests_with_stock_and_issues_only_live_ones()
    {
        var keeper = Ctx([Permissions.ProcurementRequest, Permissions.ProcurementWarehouse]);
        var other = Guid.NewGuid();
        Assert.True(ProcurementRules.CanView(keeper, Request(other, OtherDept, ProcurementRequestStatus.Submitted).WithStock()));
        Assert.False(ProcurementRules.CanView(keeper, Request(other, OtherDept, ProcurementRequestStatus.Submitted)));
        Assert.False(ProcurementRules.CanView(keeper, Request(other, OtherDept, ProcurementRequestStatus.Draft).WithStock()));
        Assert.True(keeper.SeesStock && !keeper.ReadsAll && !keeper.Manages);

        Assert.True(ProcurementRules.Issuable(ProcurementRequestStatus.Submitted));
        Assert.True(ProcurementRules.Issuable(ProcurementRequestStatus.PartiallyFulfilled));
        Assert.False(ProcurementRules.Issuable(ProcurementRequestStatus.RevisionRequired));
        Assert.False(ProcurementRules.Issuable(ProcurementRequestStatus.Fulfilled));

        var role = SystemRoles.All.Single(r => r.Name == SystemRoles.WarehouseRole);
        Assert.Contains(Permissions.ProcurementWarehouse, role.Permissions);
        Assert.Contains(Permissions.ProcurementRequest, role.Permissions);
        Assert.DoesNotContain(Permissions.ProcurementManage, role.Permissions); // цены и состав утверждённого не меняет
        Assert.DoesNotContain(Permissions.ProcurementRead, role.Permissions);
        Assert.Null(role.DepartmentCode);
    }

    [Fact]
    public void Order_totals_apply_discount_vat_on_top_and_delivery()
    {
        var (subtotal, vat, total) = ProcurementOrderRules.Totals([(60m, 40_000m), (2m, 250_000m)], 100_000m, 12m, pricesIncludeVat: false, delivery: 50_000m);
        Assert.Equal(2_900_000m, subtotal);
        Assert.Equal(336_000m, vat); // 12 % от 2 800 000
        Assert.Equal(3_186_000m, total);
        Assert.Equal(2_850_000m, ProcurementOrderRules.Totals([(60m, 40_000m), (2m, 250_000m)], 100_000m, 12m, true, 50_000m).Total);
        Assert.Equal(44_800m, ProcurementOrderRules.GrossPrice(40_000m, 12m, false));
    }

    [Fact]
    public void Order_within_the_approved_request_needs_no_director()
    {
        var paper = Guid.NewGuid();
        var toner = Guid.NewGuid();
        var approved = new Dictionary<Guid, ProcurementApprovedNeed>
        {
            [paper] = new(paper, "Бумага", 45_000m),
            [toner] = new(toner, "Картридж", null),
        };
        ProcurementOrderLine Paper(decimal price) => new(paper, "Бумага", 60, price);

        Assert.Empty(ProcurementOrderRules.NeedsDirector([Paper(44_000)], approved, 12, true, 60 * 44_000m, 0));
        Assert.Contains("цена", ProcurementOrderRules.NeedsDirector([Paper(46_000)], approved, 12, true, 60 * 46_000m, 0).Single());
        Assert.Empty(ProcurementOrderRules.NeedsDirector([Paper(46_000)], approved, 12, true, 60 * 46_000m, 5)); // +2,2 % при допуске 5 %
        Assert.Contains("цена", ProcurementOrderRules.NeedsDirector([Paper(41_000)], approved, 12, false, 60 * 45_920m, 0).Single()); // 41 000 + НДС > 45 000
        Assert.Contains("итог", ProcurementOrderRules.NeedsDirector([Paper(45_000)], approved, 0, true, 60 * 45_000m + 200_000m, 0).Single()); // доставка сверху
        Assert.Contains("не было цены", ProcurementOrderRules.NeedsDirector([new(toner, "Картридж", 2, 250_000)], approved, 0, true, 500_000m, 0).Single());
    }

    [Fact]
    public void Material_changes_of_an_approved_order_include_a_new_supplier()
    {
        var line = Guid.NewGuid();
        var a = Guid.NewGuid();
        var approved = new ProcurementOrderSnapshot(a, "ООО Бумага", [new(line, "Бумага", 60, 40_000)], 2_400_000);

        Assert.Empty(ProcurementOrderRules.MaterialChanges(approved, approved with { Lines = [new(line, "Бумага", 50, 39_000)], Total = 1_950_000 }, 0));
        Assert.Contains("сменился поставщик", ProcurementOrderRules.MaterialChanges(approved, approved with { SupplierId = Guid.NewGuid(), SupplierName = "ИП Канц" }, 0).Single());
        Assert.Contains("количество", ProcurementOrderRules.MaterialChanges(approved, approved with { Lines = [new(line, "Бумага", 70, 40_000)] }, 0).Single());
        Assert.Contains("цена", ProcurementOrderRules.MaterialChanges(approved, approved with { Lines = [new(line, "Бумага", 60, 41_000)] }, 0).Single());
        Assert.Empty(ProcurementOrderRules.MaterialChanges(approved, approved with { Lines = [new(line, "Бумага", 60, 41_000)], Total = 2_460_000 }, 5));
        Assert.Contains("итог", ProcurementOrderRules.MaterialChanges(approved, approved with { Total = 2_600_000 }, 5).Single()); // доставка подорожала
    }

    [Fact]
    public void Order_overdue_and_supplier_rating()
    {
        var today = new DateOnly(2026, 11, 1);
        Assert.True(ProcurementOrderRules.IsOverdue(ProcurementOrderStatus.Ordered, today.AddDays(-1), today));
        Assert.False(ProcurementOrderRules.IsOverdue(ProcurementOrderStatus.Ordered, today, today));
        Assert.False(ProcurementOrderRules.IsOverdue(ProcurementOrderStatus.Delivered, today.AddDays(-5), today));
        Assert.False(ProcurementOrderRules.IsOverdue(ProcurementOrderStatus.Draft, today.AddDays(-5), today)); // черновик ещё не ждёт поставки
        Assert.Null(ProcurementOrderRules.Rating(null, null, null));
        Assert.Equal(80, ProcurementOrderRules.Rating(100, 60, null));
        Assert.Equal(70, ProcurementOrderRules.Rating(100, 60, null, claims: 50, completeness: 70)); // претензии и полнота — после приёмки
        Assert.True(ProcurementOrderRules.Cancellable(ProcurementOrderStatus.Ordered));
        Assert.False(ProcurementOrderRules.Cancellable(ProcurementOrderStatus.PartiallyDelivered));
        Assert.False(ProcurementOrderRules.Editable(ProcurementOrderStatus.AwaitingApproval)); // ждёт директора — не правится
    }

    [Fact]
    public void Only_draft_and_revision_are_editable_and_return_needs_a_comment()
    {
        var editable = Enum.GetValues<ProcurementRequestStatus>().Where(ProcurementRules.Editable).ToList();
        Assert.Equal([ProcurementRequestStatus.Draft, ProcurementRequestStatus.RevisionRequired], editable);
        Assert.True(ProcurementRules.CommentRequired(ProcurementRequestStatus.RevisionRequired));
        Assert.True(ProcurementRules.CommentRequired(ProcurementRequestStatus.Rejected));
        Assert.False(ProcurementRules.CommentRequired(ProcurementRequestStatus.Cancelled));
    }

    [Fact]
    public void Every_status_has_a_russian_label_and_belongs_to_one_group()
    {
        foreach (var status in Enum.GetValues<ProcurementRequestStatus>())
        {
            Assert.NotEqual(status.ToString(), ProcurementRules.Label(status));
            var groups = (ProcurementRules.Open.Contains(status) ? 1 : 0) + (ProcurementRules.Closed.Contains(status) ? 1 : 0)
                         + (status == ProcurementRequestStatus.Draft ? 1 : 0);
            Assert.Equal(1, groups);
        }
    }

    [Theory]
    [InlineData(20, null, true)]
    [InlineData(2.5, 12000.50, true)]
    [InlineData(0, null, false)]
    [InlineData(-1, null, false)]
    [InlineData(1.2345, null, false)]
    [InlineData(5, -1.0, false)]
    [InlineData(5, 10.123, false)]
    public void Quantity_and_price_are_checked(decimal quantity, double? price, bool ok)
    {
        Assert.Equal(ok, ProcurementRules.CheckNumbers(quantity, price is null ? null : (decimal)price.Value) is null);
    }

    [Fact]
    public void Total_counts_only_priced_items_and_numbers_are_padded()
    {
        Assert.Equal(1_250m, ProcurementRules.Total([(20m, 50m), (5m, 50m), (30m, null)]));
        Assert.Equal("PR-2026-00125", ProcurementRules.Number("PR", 2026, 125));
        Assert.Equal("1 позиция", ProcurementRequestService.Positions(1));
        Assert.Equal("3 позиции", ProcurementRequestService.Positions(3));
        Assert.Equal("12 позиций", ProcurementRequestService.Positions(12));
        Assert.Equal("21 позиция", ProcurementRequestService.Positions(21));
    }

    [Fact]
    public void Roles_get_requests_supply_processes_and_only_admin_and_director_configure()
    {
        SystemRoles.Definition Role(string name) => SystemRoles.All.Single(r => r.Name == name);

        foreach (var name in new[] { SystemRoles.EmployeeRole, "Продажи", "Финансы", "HR", "Производство", "Снабжение", "Маркетинг" })
        {
            Assert.Contains(Permissions.ProcurementRequest, Role(name).Permissions);
            Assert.Contains((name, Permissions.ProcurementRequest), SystemRoles.Upgrades);
        }

        Assert.Contains(Permissions.ProcurementManage, Role("Снабжение").Permissions);
        Assert.Contains(Permissions.ProcurementRead, Role("Снабжение").Permissions);
        Assert.DoesNotContain(Permissions.ProcurementManage, Role("Финансы").Permissions);
        Assert.DoesNotContain(Permissions.ProcurementRead, Role(SystemRoles.EmployeeRole).Permissions);

        var director = Role(SystemRoles.Director).Permissions;
        Assert.Contains(Permissions.ProcurementRead, director);
        Assert.Contains(Permissions.ProcurementSettings, director);
        Assert.DoesNotContain(Permissions.ProcurementManage, director); // директор согласует, а не обрабатывает заявки

        // Права заявок — не раздел пользователя: ручной выбор разделов их не снимает.
        Assert.DoesNotContain(Permissions.ProcurementRequest, UserSections.SectionPermissions);
        Assert.Contains(Permissions.ProcurementRequest, UserSections.Effective(Role("Продажи").Permissions, [UserSections.Hr]));

        foreach (var permission in new[] { Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage, Permissions.ProcurementSettings })
        {
            Assert.True(SystemRoles.PermissionLabels.ContainsKey(permission));
        }
    }

    [Fact]
    public void Receipt_line_accepts_received_minus_defects_and_never_more_than_is_awaited()
    {
        Assert.Equal((60m, (string?)null), ProcurementOrderRules.ReceiptLine("Бумага", "пачка", 62, 2, 0, 100));
        Assert.Equal((0m, (string?)null), ProcurementOrderRules.ReceiptLine("Бумага", "пачка", 0, 0, 5, 40)); // только недостача
        Assert.Equal((40m, (string?)null), ProcurementOrderRules.ReceiptLine("Бумага", "пачка", 42, 2, 0, 40)); // брак не занимает остаток
        Assert.Contains("лишнее не принимается", ProcurementOrderRules.ReceiptLine("Бумага", "пачка", 41, 0, 0, 40).Error);
        Assert.Contains("брак", ProcurementOrderRules.ReceiptLine("Бумага", "пачка", 2, 3, 0, 40).Error);
        Assert.NotNull(ProcurementOrderRules.ReceiptLine("Бумага", "пачка", -1, 0, 0, 40).Error);
        Assert.NotNull(ProcurementOrderRules.ReceiptLine("Бумага", "пачка", 1.0001m, 0, 0, 40).Error);
        Assert.Equal(15m, ProcurementOrderRules.Remaining(100, 60, 25));
        Assert.Equal(0m, ProcurementOrderRules.Remaining(100, 100, 25));
    }

    [Fact]
    public void Delivery_status_follows_received_and_closed_quantities()
    {
        var ordered = ProcurementOrderStatus.Ordered;
        Assert.Equal(ordered, ProcurementOrderRules.DeliveryStatus(ordered, [(100m, 0m, 0m)]));
        Assert.Equal(ProcurementOrderStatus.PartiallyDelivered, ProcurementOrderRules.DeliveryStatus(ordered, [(100m, 60m, 0m), (20m, 0m, 0m)]));
        Assert.Equal(ProcurementOrderStatus.Delivered, ProcurementOrderRules.DeliveryStatus(ordered, [(100m, 60m, 40m), (20m, 20m, 0m)]));
        Assert.Equal(ProcurementItemStatus.Received, ProcurementRules.ItemStatus(new ProcurementRequestItem
        {
            Name = "Бумага", UnitCode = "пачка", Quantity = 100, FromStock = 40, OrderedQty = 60, ReceivedQty = 60,
        }, submitted: true));
    }

    [Fact]
    public void Procurement_notifications_go_to_the_tasks_section()
    {
        Assert.Equal(("procurement", NotificationCatalog.Tasks), NotificationCatalog.Of(UserNotificationKind.ProcurementRequest));
        Assert.Equal(("procurement", NotificationCatalog.Tasks), NotificationCatalog.Of(UserNotificationKind.ProcurementStatus));
    }
}

internal static class ProcurementTestExtensions
{
    public static ProcurementRequest WithStock(this ProcurementRequest r)
    {
        r.UsesStock = true;
        return r;
    }
}

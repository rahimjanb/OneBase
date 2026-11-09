using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Смены на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): оператор вводит смену по выпущенному заданию (автосохранение с
/// версией), закрыть — только полную (выпечка вне допуска — с причиной), утверждает мастер — не закрывший; утверждённая не правится,
/// корректировка заменяет её после утверждения; переработку не вернуть больше направленного в неё брака; расчёт смены по результатам;
/// стоимость брака — по цене партий, выданных в задание (без цены партии — материал назван, другая цена не подставляется).
/// </summary>
public class ProductionShiftDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Technologist = [Permissions.ProcurementRequest, Permissions.ProductionRecipes, Permissions.ProductionRead];
    private static readonly string[] Master = [Permissions.ProcurementRequest, Permissions.ProductionRead, Permissions.ProductionManage];
    private static readonly string[] Operator = [Permissions.ProcurementRequest, Permissions.ProductionRead, Permissions.ProductionOperate];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private sealed record Org(string S, Department Shop, User Technologist, User Chief, User Master, User Operator, User Keeper, Guid Raw, Guid Central);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var shop = new Department { Code = $"sh-{s}", Name = $"Цех {s}" };
        User Person(string login, string name) => new() { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = shop };
        var org = new Org(s, shop, Person("sh-tech", "Технолог"), Person("sh-chief", "Главный технолог"), Person("sh-mst", "Мастер"), Person("sh-op", "Оператор"),
            Person("sh-kpr", "Кладовщик"), Guid.Empty, Guid.Empty);
        db.Users.AddRange(org.Technologist, org.Chief, org.Master, org.Operator, org.Keeper);
        await db.SaveChangesAsync();
        return org with
        {
            Raw = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync(),
            Central = await db.ProcurementWarehouses.Where(w => w.IsDefault).Select(w => w.Id).SingleAsync(),
        };
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

    private static ProductionShiftService Shifts(IServiceProvider sp) => sp.GetRequiredService<ProductionShiftService>();

    private Task<Guid> ItemAsync(Org org, string name, string unit = "кг", bool batches = false) =>
        As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c,
            new ProcurementCatalogInput($"SH-{Guid.NewGuid():N}"[..14], $"{name} {org.S}", org.Raw, unit, null, null, 0, TracksBatches: batches, ShelfLifeDays: batches ? 90 : null),
            default));

    /// <summary>Приход партией: количество, цена за кг, годна до (дней от сегодня).</summary>
    private Task ReceiveAsync(Org org, Guid item, decimal qty, decimal price, int days) =>
        As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().ReceiptAsync(c, new ProcurementStockOperationInput(
            [new(item, qty, price, Batches: [new ProcurementBatchInput($"P-{days}", null, FieldClock.Today.AddDays(days), qty)])], "Приход сырья"), default));

    /// <summary>Печенье на 1 000 кг: замес 280 (мука 150, сахар 80, масло 50) → выпечка 87,5 % (86–89), брак «Подгорело» 2 % в переработку → глазурь 120 → упаковка: этикетка на упаковку, коробка на 20 по 0,25 кг.</summary>
    private async Task<(Guid Order, Guid Label, Guid Flour, Guid Burnt, Guid Sugar, Guid Butter)> OrderAsync(Org org)
    {
        var (flour, sugar, butter, glaze, label, box) = (await ItemAsync(org, "Мука", batches: true), await ItemAsync(org, "Сахар", batches: true),
            await ItemAsync(org, "Масло", batches: true), await ItemAsync(org, "Глазурь"), await ItemAsync(org, "Этикетка", "шт"), await ItemAsync(org, "Коробка", "шт"));
        var input = new ProductionRecipeInput($"SH-{org.S}", "Печенье", null, null, "кг", 1000, null,
        [
            new(flour, 150, StageKey: "mix", Basis: ProductionLineBasis.Batch), new(sugar, 80, StageKey: "mix", Basis: ProductionLineBasis.Batch),
            new(butter, 50, StageKey: "mix", Basis: ProductionLineBasis.Batch), new(glaze, 120, StageKey: "glaze"),
            new(label, 1, 0.5m, StageKey: "pack", Basis: ProductionLineBasis.Pack), new(box, 1, StageKey: "pack", Basis: ProductionLineBasis.Box),
        ], PackKg: 0.25m, PacksPerBox: 20,
        Stages:
        [
            new("mix", "Замес", ProductionStageKind.Mixing), new("bake", "Выпечка", ProductionStageKind.Baking, null, 87.5m, 86, 89, Reworkable: true),
            new("glaze", "Глазурь", ProductionStageKind.Glazing), new("pack", "Упаковка", ProductionStageKind.Packing),
        ],
        DefectTypes: [new("bake", "Подгорело", 2, Reworkable: true), new("pack", "Брак этикеток", 0.5m, ItemId: label)]);
        var recipe = await As(org.Technologist, Technologist, (sp, c) => sp.GetRequiredService<ProductionRecipeService>().CreateAsync(c, input, default));
        await Do(org.Technologist, Technologist, (sp, c) => sp.GetRequiredService<ProductionRecipeService>().SubmitAsync(c, recipe, default));
        await Do(org.Chief, Technologist, (sp, c) => sp.GetRequiredService<ProductionRecipeService>().ApproveAsync(c, recipe, default));
        var order = await As(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionOrderService>().CreateAsync(c,
            new ProductionOrderInput(recipe, org.Shop.Id, org.Central, 1000, FieldClock.Today, org.Master.Id, null), default));
        var burnt = (await As(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionRecipeService>().GetAsync(c, recipe, default))).DefectTypes!.Single(d => d.Name == "Подгорело").Id;
        return (order, label, flour, burnt, sugar, butter);
    }

    private static ProductionShiftInput Full(ProductionShiftDetail d, Guid label, Guid burnt, decimal bakeOut = 980, string? reason = null, decimal good = 1000,
        decimal reworkBack = 0, ProductionReworkStatus rework = ProductionReworkStatus.Sent) => new(d.ShiftDate, 1, "Линия 1", null, good, 4000, 200, null,
        [280, 280, 280, 280],
        [new(1, true, ReworkKg: reworkBack > 0 ? reworkBack : null), new(2, true, 1120, bakeOut, 4, null, reason), new(3, true), new(4, true)],
        [new(2, burnt, null, 20, rework)], [new(label, ProductionShiftMaterialKind.Defect, 40)], d.Version);

    /// <summary>
    /// Норма на выход 830 кг с нормативным браком — 4 замеса (мука 600), смена сделала 3 (мука 450), выдано 600: при закрытии перерасход меряется
    /// расходом по результатам смены, а не нормой с округлением до замеса, — +150 кг муки (33,3 %) отмечены, как в сверке; на панели норма 450.
    /// </summary>
    [PostgresFact]
    public async Task Closing_measures_overconsumption_against_the_shift_results_not_the_batch_rounded_norm()
    {
        var org = await SeedAsync();
        var (order, label, flour, burnt, sugar, butter) = await OrderAsync(org);
        var orders = (IServiceProvider sp) => sp.GetRequiredService<ProductionOrderService>();
        await Do(org.Master, Master, (sp, c) => orders(sp).ReleaseAsync(c, order, default));
        await ReceiveAsync(org, flour, 600, 5_000, 30);
        await Do(org.Keeper, Keeper, (sp, c) => orders(sp).IssueAsync(c, order, new([new(flour, 600)]), default));

        var id = await As(org.Operator, Operator, (sp, c) => Shifts(sp).CreateAsync(c, new ProductionShiftCreateInput(order, FieldClock.Today, 1), default));
        var d = await As(org.Operator, Operator, (sp, c) => Shifts(sp).GetAsync(c, id, default));
        await As(org.Operator, Operator, (sp, c) => Shifts(sp).SaveAsync(c, id, new ProductionShiftInput(d.ShiftDate, 1, null, null, 830, 3320, 166, null, [280, 280, 280],
            [new(1, true), new(2, true, 840, 735), new(3, true), new(4, true)], [], [], d.Version), default));
        await Do(org.Operator, Operator, (sp, c) => Shifts(sp).CloseAsync(c, id, default));
        await Do(org.Master, Master, (sp, c) => Shifts(sp).ApproveAsync(c, id, default));
        await Do(org.Master, Master, (sp, c) => orders(sp).CompleteAsync(c, order, new ProductionCompleteInput(830), default));

        var detail = await As(org.Master, Master, (sp, c) => orders(sp).GetAsync(c, order, default));
        var line = detail.Lines.Single(l => l.ItemId == flour);
        Assert.Equal((600m, 600m, 150m, 33.3m, true), (line.ActualNorm!.Value, line.Consumed, line.Deviation!.Value, line.DeviationPercent!.Value, line.Overconsumed));
        var check = detail.Check!.Lines.Single(l => l.ItemId == flour);
        Assert.Equal((600m, 450m, 150m), (check.NormOnFact!.Value, check.ByResults!.Value, check.Deviation!.Value));

        var dash = await As(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().TechnologistAsync(c, ProcurementFilter.Of(FieldClock.Today, FieldClock.Today),
            default));
        Assert.Contains(dash.NormFact, x => x.Name == $"Мука {org.S}" && x.Norm == 450 && x.Fact == 600);
        Assert.Contains(dash.Issues, i => i.Kind == "overconsumption" && i.Detail.Contains($"Мука {org.S}"));
    }

    [PostgresFact]
    public async Task Operator_enters_a_full_shift_master_approves_and_a_correction_replaces_it()
    {
        var org = await SeedAsync();
        var (order, label, flour, burnt, sugar, butter) = await OrderAsync(org);

        // Смену по невыпущенному заданию не открыть; оператор открывает после выпуска.
        Task<Guid> Open() => As(org.Operator, Operator, (sp, c) => Shifts(sp).CreateAsync(c, new ProductionShiftCreateInput(order, FieldClock.Today, 1, "Линия 1"), default));
        await Assert.ThrowsAsync<FieldConflictException>(Open);
        await Do(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionOrderService>().ReleaseAsync(c, order, default));

        // Сырьё партиями: мука 100 кг по 4 000 (годна 10 дней) и 500 кг по 6 000; сахар по 10 000, масло по 50 000. Выдано муки 200 — FEFO 100 + 100, в среднем 5 000.
        await ReceiveAsync(org, flour, 100, 4_000, 10);
        await ReceiveAsync(org, flour, 500, 6_000, 30);
        await ReceiveAsync(org, sugar, 300, 10_000, 30);
        await ReceiveAsync(org, butter, 100, 50_000, 30);
        await Do(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProductionOrderService>().IssueAsync(c, order, new([new(flour, 200), new(sugar, 80), new(butter, 50)]), default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Keeper, Keeper, (sp, c) => Shifts(sp).CreateAsync(c, new ProductionShiftCreateInput(order, null), default)));
        var id = await Open();
        Task<ProductionShiftDetail> Get(User u, string[] p) => As(u, p, (sp, c) => Shifts(sp).GetAsync(c, id, default));
        var empty = await Get(org.Operator, Operator);
        Assert.Equal(["Замес", "Выпечка", "Глазурь", "Упаковка"], empty.Template.Select(t => t.Name));
        Assert.Contains(empty.Problems, p => p.Contains("годный выход"));
        Assert.Contains(empty.Problems, p => p.Contains("замесы"));
        await Assert.ThrowsAsync<FieldValidationException>(() => Do(org.Operator, Operator, (sp, c) => Shifts(sp).CloseAsync(c, id, default)));

        // Выпечка 1 120 → 900 кг (80,4 %) — вне допуска 86–89: без причины не закрыть.
        var version = await As(org.Operator, Operator, (sp, c) => Shifts(sp).SaveAsync(c, id, Full(empty, label, burnt, bakeOut: 900), default));
        var low = await Get(org.Operator, Operator);
        Assert.Contains(low.Problems, p => p.Contains("вне допуска"));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Operator, Operator, (sp, c) => Shifts(sp).SaveAsync(c, id, Full(empty, label, burnt), default))); // старая версия
        await As(org.Operator, Operator, (sp, c) => Shifts(sp).SaveAsync(c, id, Full(low, label, burnt) with { Version = version }, default));
        var full = await Get(org.Operator, Operator);
        Assert.Empty(full.Problems);

        // Расчёт смены по результатам: замесы фактические — 4 × 280 → мука 600; брак выпечки 20 кг; этикеток 4 000 + 40 брака = 4 040.
        Assert.True(full.Calculation.Complete);
        Assert.True(full.Calculation.Batches!.FromActual);
        Assert.Equal(600m, full.Calculation.Lines.Single(l => l.ItemId == flour).Gross);
        Assert.Equal(20m, full.Calculation.Stages.Single(s => s.No == 2).DefectKg);
        Assert.Equal(4040m, full.Calculation.Lines.Single(l => l.ItemId == label).WithDefects);

        // Закрыл оператор — утверждает мастер; оператор не утверждает; утверждённую не правят.
        await Do(org.Operator, Operator, (sp, c) => Shifts(sp).CloseAsync(c, id, default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Operator, Operator, (sp, c) => Shifts(sp).ApproveAsync(c, id, default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => Do(org.Master, Master, (sp, c) => Shifts(sp).ReturnAsync(c, id, null, default)));
        await Do(org.Master, Master, (sp, c) => Shifts(sp).ApproveAsync(c, id, default));
        var approved = await Get(org.Operator, Operator);
        Assert.Equal(ProductionShiftStatus.Approved, approved.Status);
        Assert.True(approved.Can.Correct);
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Operator, Operator, (sp, c) => Shifts(sp).SaveAsync(c, id, Full(approved, label, burnt), default)));

        // Переработка: направлено 20 кг — вернуть в тесто 30 нельзя, 15 — можно.
        var second = await As(org.Operator, Operator, (sp, c) => Shifts(sp).CreateAsync(c, new ProductionShiftCreateInput(order, FieldClock.Today, 2), default));
        var s2 = await As(org.Operator, Operator, (sp, c) => Shifts(sp).GetAsync(c, second, default));
        var v2 = await As(org.Operator, Operator, (sp, c) => Shifts(sp).SaveAsync(c, second, Full(s2, label, burnt, good: 500, reworkBack: 30, rework: ProductionReworkStatus.Forbidden),
            default));
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Operator, Operator, (sp, c) => Shifts(sp).CloseAsync(c, second, default)));
        await As(org.Operator, Operator, (sp, c) => Shifts(sp).SaveAsync(c, second, Full(s2, label, burnt, good: 500, reworkBack: 15, rework: ProductionReworkStatus.Forbidden)
            with { Version = v2 }, default));
        await Do(org.Operator, Operator, (sp, c) => Shifts(sp).CloseAsync(c, second, default));
        Assert.Equal(15m, (await As(org.Operator, Operator, (sp, c) => Shifts(sp).GetAsync(c, second, default))).Stages.Single(x => x.StageNo == 1).ReworkKg);

        // Корректировка утверждённой: копия во вводе, без причины не закрыть; после утверждения исходная смена заменена.
        var fix = await As(org.Operator, Operator, (sp, c) => Shifts(sp).CorrectAsync(c, id, default));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Operator, Operator, (sp, c) => Shifts(sp).CorrectAsync(c, id, default)));
        var draft = await As(org.Operator, Operator, (sp, c) => Shifts(sp).GetAsync(c, fix, default));
        Assert.Equal((ProductionShiftKind.Correction, id, 1000m), (draft.Kind, draft.CorrectsId!.Value, draft.GoodOutput!.Value));
        Assert.Contains(draft.Problems, p => p.Contains("причину исправления"));
        await As(org.Operator, Operator, (sp, c) => Shifts(sp).SaveAsync(c, fix, Full(draft, label, burnt, good: 990) with { Reason = "Пересчитали поддоны" }, default));
        await Do(org.Operator, Operator, (sp, c) => Shifts(sp).CloseAsync(c, fix, default));
        await Do(org.Master, Master, (sp, c) => Shifts(sp).ApproveAsync(c, fix, default));
        Assert.Equal(ProductionShiftStatus.Superseded, (await Get(org.Master, Master)).Status);
        var list = await As(org.Master, Master, (sp, c) => Shifts(sp).SearchAsync(c, new ProductionShiftSearch(OrderId: order), default));
        Assert.Equal(3, list.Total);
        Assert.Equal(["Approved", "Closed", "Superseded"], list.Items.Select(x => x.Status.ToString()).Order());

        // Панель «Технолога» и отчёт брака — только утверждённые смены (корректировка вместо заменённой): 990 кг, брак 20 кг выпечки, выход 87,5 %.
        // Стоимость брака выпечки: 20 ÷ 0,875 = 22,857 кг теста — мука 12,245 × 5 000 + сахар 6,531 × 10 000 + масло 4,082 × 50 000 = 330 612,24 (глазури
        // и упаковки в нём нет); этикетки ведутся без партий — цены партии нет, стоимость не подставлена.
        var period = ProcurementFilter.Of(FieldClock.Today, FieldClock.Today);
        var dash = await As(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().TechnologistAsync(c, period, default));
        Assert.Equal((990m, 1000m, 99m, 20m, 1.98m, 87.5m), (dash.GoodKg, dash.PlanKg, dash.CompletionPercent!.Value, dash.DefectKg, dash.DefectPercent!.Value, dash.BakeYieldPercent!.Value));
        Assert.Equal(("Выпечка", 20m), (dash.DefectsByStage.Single().Name, dash.DefectsByStage.Single().Value));
        Assert.Equal((1, 1), (dash.ClosedShifts, dash.ActiveOrders));
        Assert.Equal(330_612.24m, dash.DefectCost);
        Assert.Equal([$"Этикетка {org.S}"], dash.DefectCostUnpriced);
        var defects = await As(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().ReportAsync(c, "defects", period, null, default));
        Assert.Equal(2, defects.Sheets[0].Rows.Count);
        var row = defects.Sheets[0].Rows[0];
        Assert.Equal(("Выпечка", "Подгорело", 20m, 2m, 1.98m, -0.02m, 330_612.24m), (row[6].GetString(), row[7].GetString(), row[9].GetDecimal(), row[12].GetDecimal(),
            row[13].GetDecimal(), row[14].GetDecimal(), row[16].GetDecimal()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, row[17].ValueKind);
        var labels = defects.Sheets[0].Rows[1];
        Assert.Equal(($"Брак материала: Этикетка {org.S}", 40m, "шт", $"Этикетка {org.S}"), (labels[7].GetString(), labels[10].GetDecimal(), labels[11].GetString(), labels[17].GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, labels[16].ValueKind);
        var output = await As(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().ReportAsync(c, "output", period, null, default));
        Assert.Equal(990m, Assert.Single(output.Sheets[0].Rows)[10].GetDecimal());
    }
}

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
/// Полуфабрикат через склад на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): начинка — задание по рецептуре с позицией
/// склада выхода; выдача сырья, смена со взвешиванием, закрытие; выход приходует только кладовщик — партией со сроком, второй раз не
/// приходуется; партия начинки уходит в два разных продукта, в минус не уходит, прослеживается по заданиям; сверка «план — норма на факт —
/// по результатам — факт склада — взвешено — отклонение»; цена партии начинки — сырьё задания по цене выданных партий.
/// </summary>
public class ProductionWarehouseFlowDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
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
        var shop = new Department { Code = $"wf-{s}", Name = $"Цех {s}" };
        User Person(string login, string name) => new() { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = shop };
        var org = new Org(s, shop, Person("wf-tech", "Технолог"), Person("wf-chief", "Главный технолог"), Person("wf-mst", "Мастер"), Person("wf-op", "Оператор"),
            Person("wf-kpr", "Кладовщик"), Guid.Empty, Guid.Empty);
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

    private static ProductionOrderService Orders(IServiceProvider sp) => sp.GetRequiredService<ProductionOrderService>();
    private static ProductionRecipeService Recipes(IServiceProvider sp) => sp.GetRequiredService<ProductionRecipeService>();

    private Task<Guid> ItemAsync(Org org, string name, bool batches = false) =>
        As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c,
            new ProcurementCatalogInput($"WF-{Guid.NewGuid():N}"[..14], $"{name} {org.S}", org.Raw, "кг", null, null, 0, TracksBatches: batches, ShelfLifeDays: batches ? 30 : null), default));

    /// <summary>Рецептура без этапов на 100 кг: строки (позиция, норма); выход — позиция склада (полуфабрикат) или без неё; утверждена.</summary>
    private async Task<Guid> RecipeAsync(Org org, string code, Guid? output, params (Guid Item, decimal Qty)[] lines)
    {
        var id = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).CreateAsync(c, new ProductionRecipeInput(code, code, null, null, "кг", 100, null,
            lines.Select(l => new ProductionRecipeLineInput(l.Item, l.Qty)).ToList(), OutputItemId: output), default));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, id, default));
        await Do(org.Chief, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, id, default));
        return id;
    }

    private async Task<Guid> ReleasedOrderAsync(Org org, Guid recipe, decimal planned)
    {
        var id = await As(org.Master, Master, (sp, c) => Orders(sp).CreateAsync(c, new ProductionOrderInput(recipe, org.Shop.Id, org.Central, planned, FieldClock.Today, null, null), default));
        await Do(org.Master, Master, (sp, c) => Orders(sp).ReleaseAsync(c, id, default));
        return id;
    }

    [PostgresFact]
    public async Task Semi_finished_filling_is_received_as_a_batch_once_and_used_in_two_products_with_the_norm_fact_check()
    {
        var org = await SeedAsync();
        var (sugar, cocoa, filling, flour) = (await ItemAsync(org, "Сахар", batches: true), await ItemAsync(org, "Какао", batches: true), await ItemAsync(org, "Начинка", batches: true),
            await ItemAsync(org, "Мука"));
        foreach (var (item, qty, price, batched) in new[] { (sugar, 500m, 10_000m, true), (cocoa, 500m, 40_000m, true), (flour, 500m, 10_000m, false) })
        {
            await As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().ReceiptAsync(c, new ProcurementStockOperationInput(
                [new(item, qty, price, Batches: batched ? [new ProcurementBatchInput("L-1", null, FieldClock.Today.AddDays(60), qty)] : null)], "Приход сырья"), default));
        }

        // Начинка: сахар 60 + какао 40 на 100 кг, выход — позиция склада «Начинка» (кг, по партиям, срок 30 дней).
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Technologist, Technologist, (sp, c) => Recipes(sp).CreateAsync(c,
            new ProductionRecipeInput($"FL0-{org.S}", "Начинка", null, null, "шт", 100, null, [new(sugar, 60)], OutputItemId: filling), default))); // шт → кг не переводится
        var fillRecipe = await RecipeAsync(org, $"FL-{org.S}", filling, (sugar, 60), (cocoa, 40));
        var fillOrder = await ReleasedOrderAsync(org, fillRecipe, 200);
        await Do(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, fillOrder, new([new(sugar, 120), new(cocoa, 80)]), default));

        // Смена: годный выход 195 кг, взвесили сахар — 119 кг; утверждает мастер.
        var shift = await As(org.Operator, Operator, (sp, c) => sp.GetRequiredService<ProductionShiftService>().CreateAsync(c, new ProductionShiftCreateInput(fillOrder, FieldClock.Today), default));
        await As(org.Operator, Operator, (sp, c) => sp.GetRequiredService<ProductionShiftService>().SaveAsync(c, shift, new ProductionShiftInput(FieldClock.Today, 1, null, null, 195, null, null,
            null, [], [], [], [new(sugar, ProductionShiftMaterialKind.Weighed, 119)]), default));
        await Do(org.Operator, Operator, (sp, c) => sp.GetRequiredService<ProductionShiftService>().CloseAsync(c, shift, default));
        await Do(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionShiftService>().ApproveAsync(c, shift, default));

        // Сверка до закрытия — по сменам: план сахара 120, норма на 195 кг — 117, факт склада 120, взвешено 119, отклонение +3 (2,6 %).
        var open = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, fillOrder, default));
        Assert.Equal(195m, open.ShiftsGoodOutput);
        Assert.Equal(("shifts", 195m, 1), (open.Check!.OutputSource, open.Check.Output, open.Check.Shifts));
        var sugarLine = open.Check.Lines.Single(l => l.ItemId == sugar);
        Assert.Equal((120m, 117m, 117m, 120m, 119m, 3m, 2.6m), (sugarLine.Plan, sugarLine.NormOnFact!.Value, sugarLine.ByResults!.Value, sugarLine.Fact, sugarLine.Weighed!.Value,
            sugarLine.Deviation!.Value, sugarLine.DeviationPercent!.Value));

        // Закрытие — мастер; выход приходует только кладовщик склада: партия начинки 195 кг со сроком 30 дней; второй раз — тот же документ.
        // Цена партии: сахар 120 × 10 000 + какао 80 × 40 000 = 4 400 000 ÷ 195 кг = 22 564,10 за кг.
        await Do(org.Master, Master, (sp, c) => Orders(sp).CompleteAsync(c, fillOrder, new ProductionCompleteInput(195), default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Master, Master, (sp, c) => Orders(sp).ReceiveOutputAsync(c, fillOrder, default)));
        Assert.True((await As(org.Keeper, Keeper, (sp, c) => Orders(sp).GetAsync(c, fillOrder, default))).Can.Receive);
        var receipt = await As(org.Keeper, Keeper, (sp, c) => Orders(sp).ReceiveOutputAsync(c, fillOrder, default));
        Assert.StartsWith("PRC-", receipt.Number);
        Assert.Equal(receipt.DocumentId, (await As(org.Keeper, Keeper, (sp, c) => Orders(sp).ReceiveOutputAsync(c, fillOrder, default))).DocumentId);
        // Права — раньше состояния: не кладовщику повтор не отдаёт документ, выдача по закрытому заданию — отказ по правам, а не «задание закрыто».
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Master, Master, (sp, c) => Orders(sp).ReceiveOutputAsync(c, fillOrder, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Master, Master, (sp, c) => Orders(sp).IssueAsync(c, fillOrder, new([new(sugar, 1)]), default)));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, fillOrder, new([new(sugar, 1)]), default)));
        var closed = await As(org.Keeper, Keeper, (sp, c) => Orders(sp).GetAsync(c, fillOrder, default));
        Assert.False(closed.Can.Receive);
        Assert.Equal((195m, receipt.Number, 22_564.10m), (closed.Output!.Quantity!.Value, closed.Output.DocumentNumber!, closed.Output.UnitCost!.Value));
        Assert.Equal("completion", closed.Check!.OutputSource);

        Guid batch;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var b = await db.ProcurementBatches.SingleAsync(x => x.ItemId == filling);
            Assert.Equal((FieldClock.Today.AddDays(30), ProcurementBatchQuality.Approved, $"{closed.Number}", 22_564.10m), (b.ExpiresOn!.Value, b.QualityStatus, b.SupplierBatchNo!,
                b.UnitCost!.Value));
            Assert.Equal(195m, await db.ProcurementStockBalances.Where(x => x.ItemId == filling).SumAsync(x => x.OnHand));
            batch = b.Id;
        }

        // Партия начинки — в два продукта: печенье (100 кг начинки) и вафли (95 кг). Больше нет — в минус не уходит.
        var cookie = await ReleasedOrderAsync(org, await RecipeAsync(org, $"CK-{org.S}", null, (flour, 50), (filling, 50)), 200);
        var wafer = await ReleasedOrderAsync(org, await RecipeAsync(org, $"WF-{org.S}", null, (flour, 50), (filling, 50)), 190);
        await Do(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, cookie, new([new(filling, 100)]), default));
        await Do(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, wafer, new([new(filling, 95)]), default));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, wafer, new([new(filling, 1)], "Не хватило"), default)));

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(0m, await db.ProcurementBatchBalances.Where(x => x.BatchId == batch).SumAsync(x => x.OnHand));
            // Происхождение и использование партии: приход из задания начинки, выдача в задания печенья и вафель.
            var trail = await db.ProcurementStockMovements.Where(m => m.BatchId == batch)
                .Select(m => new { m.Kind, m.Quantity, Order = m.Document!.ProductionOrderId }).ToListAsync();
            Assert.Equal([(ProcurementMovementKind.ProductionOutput, 195m, fillOrder)],
                trail.Where(m => m.Quantity > 0).Select(m => (m.Kind, m.Quantity, m.Order!.Value)));
            Assert.Equal(new[] { (cookie, -100m), (wafer, -95m) }.OrderBy(x => x.Item1), trail.Where(m => m.Quantity < 0).Select(m => (m.Order!.Value, m.Quantity)).OrderBy(x => x.Item1));
        }

        // Отчёты: полуфабрикат — произведено 195, передано 195 в печенье и вафли, остаток 0; расход сахара — норма 117, факт 120, взвешено 119, +3.
        var period = ProcurementFilter.Of(FieldClock.Today, FieldClock.Today);
        Task<OneBase.Application.Reports.AiReportContent> Report(string kind) =>
            As(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().ReportAsync(c, kind, period, null, default));
        var semi = Assert.Single((await Report("semi")).Sheets[0].Rows);
        Assert.Equal((195m, 195m, 0m), (semi[7].GetDecimal(), semi[8].GetDecimal(), semi[10].GetDecimal()));
        Assert.Contains($"CK-{org.S}", semi[9].GetString());
        Assert.Contains($"WF-{org.S}", semi[9].GetString());
        var sugarRow = (await Report("check")).Sheets[0].Rows.Single(r => r[6].GetString() == $"Сахар {org.S}");
        Assert.Equal((120m, 117m, 117m, 120m, 119m, 3m), (sugarRow[8].GetDecimal(), sugarRow[9].GetDecimal(), sugarRow[10].GetDecimal(), sugarRow[12].GetDecimal(),
            sugarRow[13].GetDecimal(), sugarRow[14].GetDecimal()));
        Assert.Equal(6, (await Report("recipes")).Sheets[0].Rows.Count(r => r[0].GetString()!.EndsWith(org.S)));
        Assert.Empty((await Report("packaging")).Sheets[0].Rows);
        var dash = await As(org.Master, Master, (sp, c) => sp.GetRequiredService<ProductionAnalyticsService>().TechnologistAsync(c, period, default));
        Assert.Equal((195m, 2), (dash.GoodKg, dash.ActiveOrders));
        Assert.Contains(dash.NormFact, x => x.Name == $"Сахар {org.S}" && x.Norm == 117 && x.Fact == 120);
    }
}

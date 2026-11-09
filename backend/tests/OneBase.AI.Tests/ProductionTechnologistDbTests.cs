using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Production;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// «Технолог» на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): технологическая карта с этапами, рецептурой замеса,
/// упаковкой и видами брака; утверждение (на утверждение — только карта без ошибок, свою версию не утвердить, вернуть с причиной);
/// утверждённая не правится; новая версия — копия этапов, компонентов и брака; задание по старой версии не меняется; сравнение версий;
/// задание и норма на факт — по расчёту цепочки (сахар на двух этапах — одна строка), снимок расчёта в задании.
/// </summary>
public class ProductionTechnologistDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Technologist = [Permissions.ProcurementRequest, Permissions.ProductionRecipes, Permissions.ProductionRead];
    private static readonly string[] Master = [Permissions.ProcurementRequest, Permissions.ProductionRead, Permissions.ProductionManage];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private sealed record Org(string S, Department Shop, User Technologist, User Chief, User Master, User Keeper, Guid Raw, Guid Central);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var shop = new Department { Code = $"tc-{s}", Name = $"Цех {s}" };
        User Person(string login, string name) => new() { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = shop };
        var org = new Org(s, shop, Person("tc-tech", "Технолог"), Person("tc-chief", "Главный технолог"), Person("tc-mst", "Мастер"), Person("tc-kpr", "Кладовщик"), Guid.Empty,
            Guid.Empty);
        db.Users.AddRange(org.Technologist, org.Chief, org.Master, org.Keeper);
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

    private Task<Guid> ItemAsync(Org org, string name, string unit = "кг") =>
        As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c,
            new ProcurementCatalogInput($"TC-{Guid.NewGuid():N}"[..14], $"{name} {org.S}", org.Raw, unit, null, null, 0), default));

    private sealed record Items(Guid Flour, Guid Sugar, Guid Butter, Guid Glaze, Guid Label, Guid Box);

    /// <summary>
    /// Печенье на 1 000 кг: замес 280 кг (мука flour, сахар 80, масло 50) → выпечка 87,5 %, брак «Подгорело» 2 % → глазурь 100 кг и сахар 20 кг →
    /// упаковка: этикетка на упаковку (потери 0,5 % + брак этикеток 0,5 %), коробка на 20 упаковок по 0,25 кг.
    /// </summary>
    private static ProductionRecipeInput Card(string code, Items i, decimal flour = 150, uint? version = null, ProductionRecipeLineInput? extra = null) => new(code,
        "Печенье глазированное", null, null, "кг", 1000, null,
        [
            new(i.Flour, flour, StageKey: "mix", Basis: ProductionLineBasis.Batch, Group: "Тесто"),
            new(i.Sugar, 80, StageKey: "mix", Basis: ProductionLineBasis.Batch, Group: "Тесто"),
            new(i.Butter, 50, StageKey: "mix", Basis: ProductionLineBasis.Batch, Group: "Тесто"),
            new(i.Glaze, 100, StageKey: "glaze"),
            new(i.Sugar, 20, StageKey: "glaze"),
            new(i.Label, 1, 0.5m, StageKey: "pack", Basis: ProductionLineBasis.Pack),
            new(i.Box, 1, StageKey: "pack", Basis: ProductionLineBasis.Box),
            .. extra is null ? Array.Empty<ProductionRecipeLineInput>() : [extra],
        ], version, TechCardNo: "ТК-01", PackKg: 0.25m, PacksPerBox: 20,
        Stages:
        [
            new("mix", "Замес", ProductionStageKind.Mixing),
            new("bake", "Выпечка", ProductionStageKind.Baking, "Печь 1", 87.5m, 86, 89, MoistureNorm: 4, MoistureTolerance: 0.5m),
            new("glaze", "Глазурь", ProductionStageKind.Glazing),
            new("pack", "Упаковка", ProductionStageKind.Packing),
        ],
        DefectTypes:
        [
            new("bake", "Подгорело", 2, Reworkable: true),
            new("bake", "Сломано", 0),
            new("pack", "Брак этикеток", 0.5m, ItemId: i.Label),
        ]);

    private async Task<Items> ItemsAsync(Org org) => new(await ItemAsync(org, "Мука"), await ItemAsync(org, "Сахар"), await ItemAsync(org, "Масло"), await ItemAsync(org, "Глазурь"),
        await ItemAsync(org, "Этикетка", "шт"), await ItemAsync(org, "Коробка", "шт"));

    [PostgresFact]
    public async Task Technologist_builds_a_staged_card_and_another_technologist_approves_it()
    {
        var org = await SeedAsync();
        var i = await ItemsAsync(org);
        var code = $"TC-{org.S}";

        // Карту ведёт технолог; одна позиция — один раз на этап (сахар и в тесте, и в глазури — можно).
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Master, Master, (sp, c) => Recipes(sp).CreateAsync(c, Card(code, i), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Technologist, Technologist, (sp, c) =>
            Recipes(sp).CreateAsync(c, Card(code, i, extra: new(i.Sugar, 5, StageKey: "glaze")), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Technologist, Technologist, (sp, c) =>
            Recipes(sp).CreateAsync(c, Card(code, i, extra: new(i.Glaze, 5, StageKey: "nope")), default)));
        var id = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).CreateAsync(c, Card(code, i), default));

        var card = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, id, default));
        Assert.Equal(["Замес", "Выпечка", "Глазурь", "Упаковка"], card.Stages!.Select(s => s.Name));
        Assert.Equal(2m, card.Stages!.Single(s => s.Name == "Выпечка").DefectPercent); // сумма брака продукта этапа
        Assert.Equal(280m, card.StandardBatchKg);
        Assert.Empty(card.Problems!);
        Assert.Equal(3, card.DefectTypes!.Count);
        Assert.Equal(2, card.Lines.Count(l => l.ItemId == i.Sugar));
        Assert.True(card.Can.Submit);
        Assert.Equal("ТК-01", card.TechCardNo);

        // Плановый расчёт на 1 000 кг: выпечь 1 000 ÷ 0,98; тесто 1 020,408 × 0,88 ÷ 0,875 = 1 026,239 кг → 4 замеса по 280 (частичный не
        // разрешён) → мука 600, сахар 320 + 20 = 340, масло 200; этикеток 4 000 × 1,01 = 4 040, коробок 200.
        var need = await As(org.Master, Master, (sp, c) => Recipes(sp).RequirementAsync(c, id, 1000, org.Central, default));
        Assert.Equal(1026.239m, need.Calculation!.Batches!.NeedKg);
        Assert.Equal(4, need.Calculation.Batches.Batches.Count);
        Assert.Equal([600m, 340m, 200m, 100m, 4040m, 200m], need.Lines.Select(l => l.Required));

        // Карта с ошибкой расчёта на утверждение не уходит.
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).UpdateAsync(c, id, Card(code, i) with { PacksPerBox = null, Version = card.Version }, default));
        Assert.Contains((await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, id, default))).Problems!, p => p.Contains("упаковок в коробке"));
        await Assert.ThrowsAsync<FieldValidationException>(() => Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, id, default)));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).UpdateAsync(c, id, Card(code, i), default));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, id, default));

        // На утверждении не правится; автор не утверждает свою версию; мастер не утверждает; утверждающий возвращает только с причиной.
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).UpdateAsync(c, id, Card(code, i), default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, id, default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Master, Master, (sp, c) => Recipes(sp).ApproveAsync(c, id, default)));
        var pending = await As(org.Chief, Technologist, (sp, c) => Recipes(sp).GetAsync(c, id, default));
        Assert.Equal(ProductionRecipeStatus.PendingApproval, pending.Status);
        Assert.True(pending.Can.Approve);
        Assert.False((await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, id, default))).Can.Approve);
        await Assert.ThrowsAsync<FieldValidationException>(() => Do(org.Chief, Technologist, (sp, c) => Recipes(sp).ReturnAsync(c, id, " ", default)));
        await Do(org.Chief, Technologist, (sp, c) => Recipes(sp).ReturnAsync(c, id, "Проверьте выход выпечки", default));
        Assert.Equal(ProductionRecipeStatus.Draft, (await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, id, default))).Status);
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, id, default));
        await Do(org.Chief, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, id, default));

        var approved = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, id, default));
        Assert.Equal(ProductionRecipeStatus.Active, approved.Status);
        Assert.Equal(FieldClock.Today, approved.EffectiveFrom);
        // История: создание, две правки (убрали и вернули «упаковок в коробке»), отправки, возврат с причиной, утверждение.
        Assert.Equal(["created", "updated", "updated", "submitted", "returned", "submitted", "activated"], approved.History.Select(h => h.Action));
        Assert.Contains("в коробке —", approved.History[1].Comment);
        Assert.Equal("Проверьте выход выпечки", approved.History[4].Comment);
        await Assert.ThrowsAsync<FieldConflictException>(() => Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).UpdateAsync(c, id, Card(code, i), default)));
    }

    [PostgresFact]
    public async Task New_version_copies_the_card_leaves_orders_on_the_old_version_unchanged_and_shows_the_difference()
    {
        var org = await SeedAsync();
        var i = await ItemsAsync(org);
        var code = $"TV-{org.S}";
        var v1 = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).CreateAsync(c, Card(code, i), default));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, v1, default));
        await Do(org.Chief, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, v1, default));

        // Задание по v1 на 1 000 кг: строки по позициям (сахар двух этапов — одна строка 340), снимок расчёта с замесами.
        var today = FieldClock.Today;
        var order = await As(org.Master, Master, (sp, c) => Orders(sp).CreateAsync(c, new ProductionOrderInput(v1, org.Shop.Id, org.Central, 1000, today, null, null,
            Guid.NewGuid()), default));
        var before = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, order, default));
        Assert.Equal(340m, before.Lines.Single(l => l.ItemId == i.Sugar).Required);
        Assert.Equal(4, before.Plan!.Batches!.Batches.Count);
        Assert.Equal(1000m, before.Plan.Quantity);

        // v2: мука 160 кг на замес (замес 290 кг). Копия — этапы, компоненты, брак, номер карты.
        var v2 = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).NewVersionAsync(c, v1, default));
        var draft = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, v2, default));
        Assert.Equal((4, 7, 3, "ТК-01"), (draft.Stages!.Count, draft.Lines.Count, draft.DefectTypes!.Count, draft.TechCardNo));
        Assert.Null(draft.EffectiveFrom);
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Technologist, Technologist, (sp, c) => Recipes(sp).NewVersionAsync(c, v1, default)));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).UpdateAsync(c, v2, Card(code, i, flour: 160, version: draft.Version), default));
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, v2, default));
        await Do(org.Chief, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, v2, default));
        Assert.Equal(ProductionRecipeStatus.Archived, (await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, v1, default))).Status);

        // Задание по v1 не изменилось: та же версия, строки и снимок.
        var after = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, order, default));
        Assert.Equal((v1, 1), (after.RecipeId, after.RecipeVersion));
        Assert.Equal(before.Lines.Select(l => (l.ItemId, l.Required)), after.Lines.Select(l => (l.ItemId, l.Required)));
        Assert.Equal(JsonSerializer.Serialize(before.Plan), JsonSerializer.Serialize(after.Plan));

        // Новое задание по v2: тесто 1 026,239 кг → 4 замеса по 290 = 1 160 кг → мука 640.
        var order2 = await As(org.Master, Master, (sp, c) => Orders(sp).CreateAsync(c, new ProductionOrderInput(v2, org.Shop.Id, org.Central, 1000, today, null, null), default));
        Assert.Equal(640m, (await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, order2, default))).Lines.Single(l => l.ItemId == i.Flour).Required);

        // Сравнение: изменилась норма муки на замес.
        var diff = await As(org.Master, Master, (sp, c) => Recipes(sp).CompareAsync(c, v1, v2, default));
        var flour = Assert.Single(diff.Changes);
        Assert.Equal(("Компонент", "Норма"), (flour.Section, flour.Field));
        Assert.Contains("Мука", flour.Subject);
        Assert.StartsWith("150 кг на замес", flour.Before);
        Assert.StartsWith("160 кг на замес", flour.After);

        // Закрытие по v1 с выходом 900 кг: норма на факт по цепочке — тесто 923,615 кг → 4 замеса: мука 600; сахар 320 + 18; этикеток 3 600 × 1,01.
        await Do(org.Master, Master, (sp, c) => Orders(sp).ReleaseAsync(c, order, default));
        await Do(org.Master, Master, (sp, c) => Orders(sp).CompleteAsync(c, order, new ProductionCompleteInput(900), default));
        var closed = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, order, default));
        Assert.Equal(600m, closed.Lines.Single(l => l.ItemId == i.Flour).ActualNorm);
        Assert.Equal(338m, closed.Lines.Single(l => l.ItemId == i.Sugar).ActualNorm);
        Assert.Equal(3636m, closed.Lines.Single(l => l.ItemId == i.Label).ActualNorm);
        Assert.Equal(180m, closed.Lines.Single(l => l.ItemId == i.Box).ActualNorm);
    }
}

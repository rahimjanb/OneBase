using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>План «Завод» итогами страны: раскладка по регионам в загрузчике продаж, переключатель плана и второй план рядом с выполнением.</summary>
public class FactoryPlanDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Factory_totals_become_region_plans_and_rop_plan_stands_next_to_them()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        // План РОП сентября: Хоразм — 600 кг Кекса и 100 кг Бамбука, Бухоро — 200 кг Кекса. Завод: 1000 кг Кекса и 50 кг Бамбука по стране.
        db.SalesRegionPlans.AddRange(
            new SalesRegionPlan { RegionId = fixture.KhorazmId, Kind = PlanKind.Rop, Year = 2026, Month = 9, CategoryId = 10, PlanKg = 600 },
            new SalesRegionPlan { RegionId = fixture.KhorazmId, Kind = PlanKind.Rop, Year = 2026, Month = 9, CategoryId = 4, PlanKg = 100 },
            new SalesRegionPlan { RegionId = fixture.BukharaId, Kind = PlanKind.Rop, Year = 2026, Month = 9, CategoryId = 10, PlanKg = 200 });
        db.SalesFactoryPlans.AddRange(
            new SalesFactoryPlan { Year = 2026, Month = 9, CategoryId = 10, PlanKg = 1000 },
            new SalesFactoryPlan { Year = 2026, Month = 9, CategoryId = 4, PlanKg = 50 });
        await db.SaveChangesAsync();
        sp.GetRequiredService<SalesCacheSignal>().Invalidate();
        var loader = sp.GetRequiredService<SalesDataLoader>();

        // «Настройки продаж»: Кекс — 750 Хоразму и 250 Бухоро (600 : 200), Бамбук — весь Хоразму (у Бухоро его в плане РОП нет).
        var view = await loader.FactoryPlanAsync(2026, 9);
        Assert.Equal((false, 2026, 9), (view.Explicit, view.BaseYear, view.BaseMonth));
        var khorazm = view.Regions.Single(r => r.Id == fixture.KhorazmId);
        Assert.Equal((750m, 50m), (khorazm.Factory[10], khorazm.Factory[4]));
        Assert.Equal(250m, view.Regions.Single(r => r.Id == fixture.BukharaId).Factory[10]);
        Assert.Equal((1000m, 50m, 800m), (view.Totals[10], view.Totals[4], view.RopTotals[10]));

        // Вторичка с планом «Завод»: план республики — итог завода, весь факт сентября (900 + 500 кг) к нему; рядом — план РОП.
        var factory = (await loader.LoadAsync(2026, 9, PlanKind.Factory)).Overview();
        Assert.Equal([PlanSources.Rop, PlanSources.Factory], factory.Period.AvailablePlans);
        Assert.Equal((PlanSources.Factory, 1050m, 1400m), (factory.Kpi.PlanSource, factory.Kpi.PlanKg, factory.Kpi.PlanFactKg));
        Assert.Equal((PlanSources.Rop, 900m), (factory.Kpi.AltPlan!.Source, factory.Kpi.AltPlan.PlanKg));
        Assert.Equal(Math.Round(1400m / 900m, 4), Math.Round(factory.Kpi.AltPlan.Execution!.Value, 4));

        var rop = (await loader.LoadAsync(2026, 9, PlanKind.Rop)).Overview();
        Assert.Equal((PlanSources.Rop, 900m), (rop.Kpi.PlanSource, rop.Kpi.PlanKg));
        Assert.Equal((PlanSources.Factory, 1050m), (rop.Kpi.AltPlan!.Source, rop.Kpi.AltPlan.PlanKg));

        // План «Завод», загруженный по регионам (таблица), важнее итогов: итоги не раскладываются.
        db.SalesRegionPlans.Add(new SalesRegionPlan { RegionId = fixture.KhorazmId, Kind = PlanKind.Factory, Year = 2026, Month = 9, CategoryId = 10, PlanKg = 300 });
        await db.SaveChangesAsync();
        sp.GetRequiredService<SalesCacheSignal>().Invalidate();
        Assert.True((await loader.FactoryPlanAsync(2026, 9)).Explicit);
        Assert.Equal(300m, (await loader.LoadAsync(2026, 9, PlanKind.Factory)).Overview().Kpi.PlanKg);
    }
}

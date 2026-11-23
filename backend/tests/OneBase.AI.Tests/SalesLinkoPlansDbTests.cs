using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Sales;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// На PostgreSQL: планы ТП из Linko (staff_balance) доходят до отчёта месяца так, как их читает «Полевой контроль» — план АКБ ТП из показателя
/// АКБ без категории (командный план СВР «… Супер» — не план ТП), «Тотал» по категориям не складывается со своими Могулем и Twizos.
/// Месяц — февраль 2024: других данных в нём нет, итог республики — только этот ТП.
/// </summary>
public class SalesLinkoPlansDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Akb_plan_and_category_plans_come_from_linko_indicators_without_double_total()
    {
        const long agent = 900_893, supervisor = 900_894;
        var id = 9_893_000L;
        SalesStaffPlan Row(long user, string type, string name, decimal plan) => new()
        {
            Year = 2024, Month = 2, LinkoUserId = user, IndicatorId = ++id, IndicatorName = name, PlanType = type, PlanAmount = plan, FactAmount = 0,
            LoadedAt = DateTimeOffset.UtcNow,
        };
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.SalesStaffPlans.AddRange(
                Row(agent, StaffPlanTypes.SalesWeight, "Февраль Тотал Вес 6 млн", 1_000),
                Row(agent, StaffPlanTypes.SalesWeight, "Февраль Могуль Вес 6 млн", 300),
                Row(agent, StaffPlanTypes.SalesWeight, "Февраль Twizos Вес 6 млн", 100),
                Row(agent, StaffPlanTypes.ActiveClients, "Февраль АКБ Ташкент", 80),
                Row(agent, StaffPlanTypes.ActiveClients, "Февраль АКБ Twizos Ташкент", 30),
                Row(supervisor, StaffPlanTypes.ActiveClients, "Февраль АКБ Вес Ташкент Супер", 500));
            await db.SaveChangesAsync();
        }

        using var read = fixture.Services.CreateScope();
        var analytics = await read.ServiceProvider.GetRequiredService<SalesDataLoader>().LoadAsync(2024, 2, CancellationToken.None);

        var view = analytics.Agent(agent);
        Assert.Equal(80m, view.AkbPlan!.Plan); // «АКБ Twizos» — план АКБ категории, не ТП
        var plans = view.CategoryPlan.Where(r => r.PlanKg != null).ToDictionary(r => r.Name, r => r.PlanKg!.Value);
        Assert.Equal(new Dictionary<string, decimal> { ["Помадка"] = 300, ["Придзел"] = 100, ["Остальные категории (без разбивки)"] = 600 }, plans);

        // Республика: план АКБ — только план ТП (командный план СВР 500 не прибавляется).
        Assert.Equal(80m, analytics.Republic(new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 29)).Kpi.AkbPlan!.Plan);
    }
}

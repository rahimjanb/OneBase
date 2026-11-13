using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Analytik;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;
using OneBase.Domain.Analytik;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// «Точность прошлых прогнозов» на PostgreSQL: оплаты точек в далёком будущем (другие тесты туда не пишут) — три прошлых месяца проверяются
/// на истории, сохранённый прогноз на месяц (сделан до его начала, сверен с фактом) заменяет проверку; без права «Финансы» — отказ.
/// </summary>
public sealed class AnalytikForecastBacktestDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Past_months_come_from_history_check_and_a_saved_month_forecast_replaces_it()
    {
        var first = new DateOnly(2031, 12, 1);
        var today = new DateOnly(2032, 6, 10);
        var baseId = 9_100_000_000L + Random.Shared.Next(1_000_000) * 1000L;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var adb = scope.ServiceProvider.GetRequiredService<IAnalytikDb>();
        var days = today.DayNumber - first.DayNumber;
        for (var i = 0; i < days; i++)
        {
            var d = first.AddDays(i);
            db.LinkoPayments.Add(new Domain.Sales.LinkoPayment
            {
                Id = baseId + i, Amount = d.DayOfWeek == DayOfWeek.Sunday ? 200_000m : 1_000_000m + i * 1_000m, Status = "accepted", CreatedDate = d, Type = "cash",
            });
        }

        var saved = new AnalytikForecast
        {
            Metric = "finance.payments", Granularity = "month", Method = "holt_winters", MadeOn = new DateOnly(2032, 3, 25), TargetDate = new DateOnly(2032, 4, 1),
            Horizon = 1, Predicted = 25_000_000m, Lower = 20_000_000m, Upper = 30_000_000m, Actual = 26_000_000m,
        };
        await db.SaveChangesAsync();
        fixture.Clock.Now = new DateTimeOffset(2032, 6, 10, 6, 0, 0, TimeSpan.Zero);
        try
        {
            var service = scope.ServiceProvider.GetRequiredService<ForecastService>();
            var finance = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse, Permissions.FinanceRead }, SalesRowScope.Organization);

            var view = await service.BacktestAsync("finance.payments", "day", finance, CancellationToken.None);
            Assert.Equal(new[] { new DateOnly(2032, 3, 1), new DateOnly(2032, 4, 1), new DateOnly(2032, 5, 1) }, view.Months.Select(m => m.Month));
            Assert.All(view.Months, m => Assert.False(m.Saved));
            Assert.All(view.Months, m => Assert.True(m.Accuracy > 0.9m, $"{m.Month}: {m.Accuracy}"));
            Assert.Equal(new DateOnly(2032, 3, 1), view.Past[0].Date);
            Assert.Equal(today.AddDays(-1), view.Past[^1].Date);
            Assert.Equal("сум", view.Unit);

            // Недели — только полные (с понедельника), суммы дневных прогнозов.
            var weeks = await service.BacktestAsync("finance.payments", "week", finance, CancellationToken.None);
            Assert.All(weeks.Past, w => Assert.Equal(DayOfWeek.Monday, w.Date.DayOfWeek));
            Assert.Equal(view.Past.Where(p => p.Date >= weeks.Past[0].Date && p.Date < weeks.Past[0].Date.AddDays(7)).Sum(p => p.Value), weeks.Past[0].Value);

            // Сохранённый прогноз на апрель — вместо проверки на истории.
            adb.Forecasts.Add(saved);
            await adb.SaveChangesAsync(CancellationToken.None);
            var withSaved = await service.BacktestAsync("finance.payments", "day", finance, CancellationToken.None);
            var april = Assert.Single(withSaved.Months, m => m.Saved);
            Assert.Equal(new DateOnly(2032, 4, 1), april.Month);
            Assert.Equal(25_000_000m, april.Forecast);
            Assert.Equal(26_000_000m, april.Fact);
            Assert.Equal(0.9615m, april.Accuracy);

            // Без «Финансов» — как у прогноза: отказ.
            var outsider = new AnalytikPrincipal(Guid.NewGuid(), new HashSet<string> { Permissions.AnalytikUse }, SalesRowScope.Organization);
            var denied = await Assert.ThrowsAsync<QueryPlanException>(() => service.BacktestAsync("finance.payments", "day", outsider, CancellationToken.None));
            Assert.Equal(QueryErrors.Denied, denied.Code);
        }
        finally
        {
            fixture.Clock.Now = null;
            if (saved.Id != Guid.Empty && await adb.Forecasts.AnyAsync(f => f.Id == saved.Id))
            {
                adb.Forecasts.Remove(saved);
                await adb.SaveChangesAsync(CancellationToken.None);
            }

            db.LinkoPayments.RemoveRange(await db.LinkoPayments.Where(x => x.Id >= baseId && x.Id < baseId + 1000).ToListAsync());
            await db.SaveChangesAsync();
        }
    }
}

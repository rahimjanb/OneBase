using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Security;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Proactive Intelligence на PostgreSQL (мир IntelligenceWorld): новая находка расследуется кодом — вклад регионов, групп и агентов в отклонение
/// дня; уверенность и класс вывода — в находке; история (создана, важность, расследована, закрыта, вернулась) — в analytik.InsightEvents.
/// </summary>
public sealed class AnalytikProactiveDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    private static InsightCandidate Anomaly(string severity, decimal impact) =>
        new(InsightKinds.Anomaly, "sales", "sales.daily_anomaly", $"sales.daily_anomaly:test:{Day:yyyy-MM-dd}", severity, "Выручка ниже обычной",
            "Выручка дня ниже обычной.", Hypotheses: ["Праздничный день"], Impact: impact, ImpactUnit: "сум", Evidence: new { Day },
            RequiredPermission: Permissions.SalesRead);

    private async Task<InsightRunResult> ApplyAsync(params InsightCandidate[] found)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<InsightStore>()
            .ApplyAsync(found, new HashSet<string> { "sales.daily_anomaly" }, false, default);
    }

    [PostgresFact]
    public async Task New_anomaly_is_investigated_by_code_and_its_history_is_kept()
    {
        var w = await IntelligenceWorld.SeedAsync(fixture);
        using (var clean = fixture.Services.CreateScope())
        {
            var adb = clean.ServiceProvider.GetRequiredService<IAnalytikDb>();
            await adb.InsightEvents.ExecuteDeleteAsync();
            await adb.Insights.Where(i => i.Rule == "sales.daily_anomaly").ExecuteDeleteAsync();
        }

        var first = await ApplyAsync(Anomaly(Severities.Warning, -645_000));
        Assert.Equal(1, first.Created);

        using (var scope = fixture.Services.CreateScope())
        {
            var adb = scope.ServiceProvider.GetRequiredService<IAnalytikDb>();
            var insight = await adb.Insights.AsNoTracking().SingleAsync(i => i.Rule == "sales.daily_anomaly" && i.ResolvedAt == null);
            Assert.NotNull(insight.InvestigatedAt);
            Assert.Equal(EvidenceClasses.Observed, insight.EvidenceClass);
            using var inv = JsonDocument.Parse(insight.Investigation!);
            var regions = inv.RootElement.GetProperty("drivers").EnumerateArray().Single(d => d.GetProperty("dimension").GetString() == "region");
            var top = regions.GetProperty("items")[0];
            Assert.Equal(w.Treated, top.GetProperty("key").GetString());
            // Эталон: вклад «Хорезма» в изменение выручки 1 октября к 24 сентября — по строкам мира.
            var expected = (double)(w.Revenue(0, Day, Day) - w.Revenue(0, Day.AddDays(-7), Day.AddDays(-7)));
            Assert.Equal(expected, top.GetProperty("delta").GetDouble(), 0);
            Assert.True(insight.Confidence is > 0.6m and < 0.9m, $"уверенность {insight.Confidence}");
            Assert.Contains("не доказанная причина", inv.RootElement.GetProperty("conclusion").GetString());
            // Расчёты авторасследования — в журнале, источник «proactive».
            Assert.True(await adb.Computations.CountAsync(c => c.Origin == "proactive" && c.Method == "drivers") >= 3);
        }

        await ApplyAsync(Anomaly(Severities.Critical, -900_000)); // выросла важность — событие и повторное расследование
        await ApplyAsync(); // условие исчезло — закрыта
        await ApplyAsync(Anomaly(Severities.Warning, -500_000)); // вернулась

        using var check = fixture.Services.CreateScope();
        var events = await check.ServiceProvider.GetRequiredService<IAnalytikDb>().InsightEvents.AsNoTracking().OrderBy(e => e.At).ThenBy(e => e.CreatedAt)
            .Select(e => e.Event).ToListAsync();
        Assert.Equal(["created", "investigated", "severity", "investigated", "resolved", "reopened", "investigated"], events);
    }

    [PostgresFact]
    public async Task Rules_without_data_investigation_get_confidence_from_confirmed_cause()
    {
        await IntelligenceWorld.SeedAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var investigator = scope.ServiceProvider.GetRequiredService<InsightInvestigator>();
        var confirmed = new OneBase.Domain.Analytik.AnalytikInsight
        {
            Kind = InsightKinds.Risk, Module = "supply", Rule = "supply.stockout", Key = "k", Severity = Severities.Warning, Title = "t", Description = "d",
            CauseConfirmed = true, Cause = "Запас закончится 20.11",
        };
        Assert.Null(await investigator.InvestigateAsync(confirmed, DateTimeOffset.UtcNow, default));
        Assert.Equal(0.8m, confirmed.Confidence);
        Assert.Equal(EvidenceClasses.Observed, confirmed.EvidenceClass);

        var guess = new OneBase.Domain.Analytik.AnalytikInsight
        {
            Kind = InsightKinds.Risk, Module = "hr", Rule = "hr.lateness_growth", Key = "k2", Severity = Severities.Warning, Title = "t", Description = "d",
            Hypotheses = "[\"Смена графика\"]",
        };
        await investigator.InvestigateAsync(guess, DateTimeOffset.UtcNow, default);
        Assert.Equal(0.3m, guess.Confidence);
        Assert.Equal(EvidenceClasses.Assumption, guess.EvidenceClass);
    }
}

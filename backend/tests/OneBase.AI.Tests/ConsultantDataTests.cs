using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Consultant;
using OneBase.AI.Grounding;
using OneBase.AI.Knowledge;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.DataSources;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.Integrations;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Консультант по данным: OOS и дозагрузка (get_stock_reload), свежесть источников в карте источников истины, правила поиска и пустых
/// значений в политике ответов.
/// </summary>
public class ConsultantDataTests
{
    [Fact]
    public void Stock_reload_reads_stock_with_sales_permission_and_explains_wrong_arguments()
    {
        var tool = new GetStockReloadTool(null!, null!, new SalesOptions());
        Assert.Equal(("get_stock_reload", KnowledgeSources.SalesStock, Permissions.SalesRead), (tool.Name, tool.Source, tool.RequiredPermission));
        Assert.Equal(["category", "product", "region", "filter", "limit"], tool.InputSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.Contains("рекомендуемый заказ", tool.Description);
        Assert.Contains("get_stock_reload", tool.UsageHint);
    }

    [Theory]
    [InlineData("""{"limit":61}""", "limit — от 1 до 60")]
    [InlineData("""{"filter":"everything"}""", "filter")]
    public async Task Stock_reload_arguments_are_checked_before_reading_stock(string json, string expected)
    {
        var result = await new GetStockReloadTool(null!, null!, new SalesOptions()).ExecuteAsync(new ToolContext("supply", null, 0), JsonDocument.Parse(json).RootElement);
        Assert.False(result.Success);
        Assert.Contains(expected, result.Content);
    }

    [Fact]
    public void Answer_policy_searches_before_no_data_and_tells_zero_from_missing()
    {
        var rules = ConsultantPrompts.QualityRules;
        Assert.Contains("не больше трёх попыток", rules);
        Assert.Contains("Не превращай отсутствие значения в ноль", rules);
        Assert.Contains("План не удалось подтвердить", rules);
        Assert.Contains("CategoryPlans", rules);
        Assert.Contains("get_stock_reload", rules);
        Assert.Contains("перечисли варианты", rules);
        Assert.DoesNotContain("Указывай источник и период для каждого факта", rules); // источники — в данных ответа, не в тексте
    }
}

/// <summary>Свежесть источников на базе: удачная и упавшая синхронизация Linko, привязанное и непривязанное поле таблиц.</summary>
public class SourceFreshnessDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Freshness_tells_ok_error_never_and_not_bound_apart()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await db.LinkoSyncStates.ExecuteDeleteAsync();
        await db.DataSourceBindings.ExecuteDeleteAsync();
        var now = DateTimeOffset.UtcNow;
        db.LinkoSyncStates.AddRange(
            new LinkoSyncState { Entity = "orders", LastRunAt = now, LastSuccessAt = now },
            new LinkoSyncState { Entity = "order_returns", LastRunAt = now, LastSuccessAt = now.AddHours(-1) },
            new LinkoSyncState { Entity = "visits", LastRunAt = now, LastSuccessAt = now.AddHours(-3), LastError = "Linko: нет ответа за 60 с" });
        var connection = new DataSourceConnection { Name = "План первички", SpreadsheetId = "1Test", Url = "1Test" };
        db.DataSourceConnections.Add(connection);
        db.DataSourceBindings.Add(new DataSourceBinding
        {
            Section = "primary", ConnectionId = connection.Id, Sheets = "[\"План\"]", Fields = $"[{{\"field\":\"{DataSourceCatalog.PlanPrimary}\"}}]",
            LastRunAt = now, LastStatus = "ok", Enabled = true,
        });
        await db.SaveChangesAsync();

        var truth = sp.GetRequiredService<SourceOfTruthService>();
        var rows = (await truth.FreshnessAsync()).ToDictionary(r => r.Key);
        Assert.Equal("ok", rows["linko_sales"].Status);
        Assert.Equal(now.AddHours(-1), rows["linko_sales"].UpdatedAt!.Value, TimeSpan.FromSeconds(1)); // самая старая из сущностей группы
        Assert.Equal("error", rows["linko_visits"].Status);
        Assert.Contains("нет ответа", rows["linko_visits"].Message);
        Assert.Equal("never", rows["linko_payments"].Status);
        Assert.Equal("ok", rows[DataSourceCatalog.PlanPrimary].Status);
        Assert.Contains("План первички", rows[DataSourceCatalog.PlanPrimary].Source);
        Assert.Equal("not_bound", rows[DataSourceCatalog.DealerPayments].Status); // реестр таблиц без привязки — данных нет

        // Инструмент карты источников отдаёт свежесть показателя вместе с владельцем.
        var json = await SourceOfTruthTool.RunAsync(JsonDocument.Parse("""{"metric":"визиты"}""").RootElement, truth, CancellationToken.None);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("freshness").GetProperty("status").GetString());
    }
}

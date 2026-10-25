using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Gateway;
using OneBase.AI.Providers;
using OneBase.Domain.AI;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Учёт вызовов в базе: кэш-токены пишутся отдельно, стоимость — по четырём ценам модели; цены Claude 5.5 проставлены миграцией.</summary>
public class UsageRecorderDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Migration_sets_claude_prices_and_recorder_stores_cache_tokens_priced_at_cache_rates()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();

        // Миграция AiUsageCacheTokensAndPrices: цены Claude 5.5 по прайсу Anthropic от 25.10.2026 — у моделей, где цена не была задана.
        string[] claude = ["claude-haiku-5-5", "claude-opus-5-5", "claude-sonnet-5-5"];
        var models = await db.AiModels.AsNoTracking().Where(m => m.ProviderCode == "anthropic" && claude.Contains(m.ModelId)).OrderBy(m => m.ModelId).ToListAsync();
        Assert.Equal(
            [("claude-haiku-5-5", 0.10m, 0.50m, 0.01m, 0.125m), ("claude-opus-5-5", 4m, 20m, 0.20m, 5m), ("claude-sonnet-5-5", 2m, 10m, 0.10m, 2.50m)],
            models.Select(m => (m.ModelId, m.InputPricePerMillion ?? -1, m.OutputPricePerMillion ?? -1, m.CacheReadPricePerMillion ?? -1, m.CacheWritePricePerMillion ?? -1)));

        var recorder = fixture.Services.GetRequiredService<IAiUsageRecorder>();
        var context = new AiCallContext(fixture.AdminId, "sales", null, "usage-test");
        var opus = new AiModelRef("anthropic", "claude-opus-5-5");

        // 1 000 входных ($0.004) + 20 000 из кэша ($0.004) + 2 000 в кэш ($0.01) + 500 выходных ($0.01) = $0.028 — не $0.102, как было бы по цене входа.
        await recorder.RecordAsync(new AiCallRecord(context, opus, new AiTokenUsage(1_000, 500, CacheReadTokens: 20_000, CacheWriteTokens: 2_000), 1234, true, null));
        var row = await db.AiUsage.AsNoTracking().SingleAsync(u => u.UserId == fixture.AdminId && u.Purpose == "usage-test" && u.CacheReadTokens == 20_000);
        Assert.Equal((1_000, 500, 20_000, 2_000), (row.InputTokens, row.OutputTokens, row.CacheReadTokens, row.CacheWriteTokens));
        Assert.Equal(0.028m, row.CostUsd);
        Assert.Equal(("anthropic", "claude-opus-5-5", "sales", 1234L, true), (row.Provider, row.Model, row.AgentCode, row.DurationMs, row.Success));

        // Модель без цен кэша: вызов с кэш-токенами остаётся без стоимости (а не считается по цене входа), без кэша — считается.
        var bare = new AiModel { ProviderCode = "anthropic", ModelId = "claude-usage-test-no-cache-price", Enabled = true, InputPricePerMillion = 4, OutputPricePerMillion = 20 };
        db.AiModels.Add(bare);
        await db.SaveChangesAsync();
        try
        {
            var bareRef = new AiModelRef("anthropic", bare.ModelId);
            await recorder.RecordAsync(new AiCallRecord(context, bareRef, new AiTokenUsage(1_000, 500, CacheReadTokens: 100), 10, true, null));
            await recorder.RecordAsync(new AiCallRecord(context, bareRef, new AiTokenUsage(1_000, 500), 10, true, null));
            var rows = await db.AiUsage.AsNoTracking().Where(u => u.Model == bare.ModelId).OrderBy(u => u.CacheReadTokens).ToListAsync();
            Assert.Equal([0.014m, null], rows.Select(u => u.CostUsd));
        }
        finally
        {
            db.AiModels.Remove(bare);
            await db.SaveChangesAsync();
        }
    }
}

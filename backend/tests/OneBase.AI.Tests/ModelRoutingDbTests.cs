using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.AI.Gateway;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Стоимость и токены ответа на настоящей PostgreSQL: все вызовы одного ответа связаны TurnId, чужие в сумму не попадают; вызов без цены модели
/// делает сумму неполной (null в журнале, а не ноль); причина выбора модели и применённые цены сохраняются. Эмулятор на уровне настроек
/// провайдеров подменяет сохранённый в OneBase настоящий ключ.
/// </summary>
public class ModelRoutingDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly AiModelRef Opus = new("anthropic", "claude-opus-5-5");
    private static readonly AiModelRef Sonnet = new("anthropic", "claude-sonnet-5-5");
    private static readonly AiModelRef Unpriced = new("anthropic", "claude-unpriced-test");

    [PostgresFact]
    public async Task Answer_cost_and_tokens_add_up_by_turn_and_a_call_without_price_is_marked_not_zeroed()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var recorder = sp.GetRequiredService<IAiUsageRecorder>();
        var conversation = Guid.NewGuid();
        var started = DateTimeOffset.UtcNow.AddSeconds(-1);
        var turn = AiTurnTimings.Start();
        var context = new AiCallContext(fixture.AdminId, "sales", conversation, "agent");

        await recorder.RecordAsync(new AiCallRecord(context, Sonnet, new AiTokenUsage(1_000, 200, 5_000, 0), 1_500, true, null, 10, 1, turn.Id, "модель сотрудников"));
        await recorder.RecordAsync(new AiCallRecord(context with { AgentCode = "director", Purpose = "consultant.answer" }, Opus, new AiTokenUsage(2_000, 500), 9_000,
            true, null, 0, 1, turn.Id, "сложный вопрос: сложность 3 ≥ 3"));
        // Другой ответ того же чата — не в этой сумме.
        await recorder.RecordAsync(new AiCallRecord(context, Sonnet, new AiTokenUsage(999_999, 0), 1, true, null, 0, 1, Guid.NewGuid()));

        var prices = await db.AiModels.AsNoTracking().Where(m => m.ProviderCode == "anthropic" && (m.ModelId == Sonnet.Model || m.ModelId == Opus.Model))
            .ToDictionaryAsync(m => m.ModelId, m => new AiModelPrices(m.InputPricePerMillion, m.OutputPricePerMillion, m.CacheReadPricePerMillion, m.CacheWritePricePerMillion));
        var expected = AiPricing.Cost(new AiTokenUsage(1_000, 200, 5_000, 0), prices[Sonnet.Model], Sonnet.Model)!.Value
                       + AiPricing.Cost(new AiTokenUsage(2_000, 500), prices[Opus.Model], Opus.Model)!.Value;
        Assert.Equal(expected, turn.CostUsd);
        Assert.False(turn.CostIncomplete);

        // Модель без цены: сумма — без неё и с пометкой «неполная».
        await recorder.RecordAsync(new AiCallRecord(context with { AgentCode = "director", Purpose = "consultant.review" }, Unpriced, new AiTokenUsage(100, 10), 100,
            true, null, 0, 1, turn.Id, "перепроверка: модель сложных ответов", true));
        Assert.Equal(expected, turn.CostUsd);
        Assert.True(turn.CostIncomplete);

        var rows = await db.AiUsage.AsNoTracking().Where(u => u.TurnId == turn.Id).OrderBy(u => u.CreatedAt).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(["модель сотрудников", "сложный вопрос: сложность 3 ≥ 3", "перепроверка: модель сложных ответов"], rows.Select(r => r.ModelReason));
        Assert.NotNull(rows[0].PriceBasis);
        Assert.Null(rows[2].PriceBasis);
        Assert.Null(rows[2].CostUsd);
        Assert.True(rows[2].UsedFallback);

        await sp.GetRequiredService<AiAuditService>().WriteAsync("consultant", fixture.AdminId, conversation, null, "Почему упали продажи?", started, 10_000, null,
            "ответ", null, CancellationToken.None, false, turn.View(), turn);
        var log = await db.AiAuditLogs.AsNoTracking().SingleAsync(l => l.TurnId == turn.Id);
        Assert.Null(log.CostUsd); // неполная стоимость не выдаётся за полную
        Assert.Equal(1_000 + 5_000 + 2_000 + 100, log.InputTokens);
        Assert.Equal(200 + 500 + 10, log.OutputTokens);
        using var routing = JsonDocument.Parse(log.Routing!);
        var total = routing.RootElement.GetProperty("total");
        Assert.Equal(3, total.GetProperty("calls").GetInt32());
        Assert.True(total.GetProperty("costIncomplete").GetBoolean());
        Assert.Equal(expected, total.GetProperty("costUsd").GetDecimal());
        Assert.True(routing.RootElement.GetProperty("costEstimated").GetBoolean());
        Assert.Equal(3, routing.RootElement.GetProperty("byModel").EnumerateObject().Count());
        Assert.Equal(1, routing.RootElement.GetProperty("byPurpose").GetProperty("agent").GetProperty("calls").GetInt32());
        Assert.Equal(1, routing.RootElement.GetProperty("fallbacks").GetInt32());
        Assert.DoesNotContain("Почему упали", log.Routing); // в сводке нет текста вопроса
    }

    [PostgresFact]
    public async Task Emulator_replaces_a_saved_real_key_and_url_for_every_consumer()
    {
        await fixture.ResetAsync();
        var store = fixture.Services.GetRequiredService<AiSettingsStore>();
        await store.SaveProviderAsync("anthropic", null, "sk-ant-real-test-key", true, fixture.AdminId);
        try
        {
            Assert.Equal("sk-ant-real-test-key", (await store.GetProviderAsync("anthropic"))!.ApiKey);

            var guarded = new AiSettingsStore(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture.Services.GetRequiredService<AiProviderRegistry>(),
                fixture.Services.GetRequiredService<AiEnvironment>(), fixture.Services.GetRequiredService<IDataProtectionProvider>(),
                NullLogger<AiSettingsStore>.Instance, new AiSafetyOptions("http://localhost:5098"));
            var state = await guarded.GetProviderAsync("anthropic");
            Assert.Equal(("http://localhost:5098", AiSafetyOptions.EmulatorKey, true), (state!.BaseUrl, state.ApiKey, state.IsReady));
            Assert.All(await guarded.GetProvidersAsync(), p => Assert.Equal(AiSafetyOptions.EmulatorKey, p.Value.ApiKey)); // настоящего ключа нет ни у одного провайдера
        }
        finally
        {
            await store.SaveProviderAsync("anthropic", null, "", true, fixture.AdminId);
        }
    }
}

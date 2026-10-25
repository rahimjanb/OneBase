using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Бюджет на AI и лимит одновременных вызовов моделей.</summary>
public class AiLimitsTests
{
    private static readonly AiModelRef Primary = new("openai", "main-model");

    [Fact]
    public void Budget_status_blocks_at_the_limit_and_warns_at_the_threshold()
    {
        var fine = new AiBudgetStatus(SpentToday: 3m, SpentMonth: 40m, DailyUsd: 10m, MonthlyUsd: 200m, WarnPercent: 80);
        Assert.False(fine.Exceeded);
        Assert.False(fine.DailyWarning || fine.MonthlyWarning);
        Assert.Null(fine.BlockMessage);

        var warn = fine with { SpentToday = 8m };
        Assert.True(warn.DailyWarning);
        Assert.False(warn.Exceeded);

        var day = fine with { SpentToday = 10m };
        Assert.True(day.DailyExceeded);
        Assert.Contains("Дневной бюджет на AI исчерпан", day.BlockMessage);

        var month = fine with { SpentMonth = 250m };
        Assert.Contains("Месячный бюджет на AI исчерпан", month.BlockMessage);

        // Без бюджета — никаких ограничений, сколько бы ни потратили.
        Assert.Null(new AiBudgetStatus(1_000m, 10_000m, null, null, 80).BlockMessage);
    }

    [Fact]
    public async Task Exhausted_budget_stops_calls_before_they_reach_the_provider()
    {
        var provider = new FakeProvider("openai");
        var gateway = new AiGateway(
            new StaticSettings(new AiSettingsSnapshot(true, Primary, null, null, null, null, 2048, false)),
            new AiProviderRegistry([provider]),
            new NullRecorder(),
            NullLogger<AiGateway>.Instance,
            new BlockedLimits("Дневной бюджет на AI исчерпан ($10 из $10)."));

        var error = await Assert.ThrowsAsync<LlmNotConfiguredException>(() => gateway.CompleteAsync([new(LlmRole.User, "Как продажи?")], [], new AiCallOptions()));

        Assert.Contains("Дневной бюджет", error.Message);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task Calls_over_the_concurrency_limit_wait_for_a_free_slot()
    {
        var settings = new StaticSettings(new AiSettingsSnapshot(true, Primary, null, null, null, null, 2048, false, MaxConcurrentCalls: 2));
        var limits = new AiLimits(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), settings, TimeProvider.System,
            NullLogger<AiLimits>.Instance);

        var first = await limits.EnterAsync();
        var second = await limits.EnterAsync();
        var third = limits.EnterAsync();
        await Task.Delay(100);
        Assert.False(third.IsCompleted); // третий ждёт

        first.Dispose();
        using var lease = await third.WaitAsync(TimeSpan.FromSeconds(5)); // место освободилось — третий пошёл
        first.Dispose(); // повторное освобождение того же места ничего не ломает
        second.Dispose();

        // 0 — без ограничения.
        var unlimited = new AiLimits(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new StaticSettings(new AiSettingsSnapshot(true, Primary, null, null, null, null, 2048, false, MaxConcurrentCalls: 0)), TimeProvider.System,
            NullLogger<AiLimits>.Instance);
        var leases = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => unlimited.EnterAsync()));
        Assert.Equal(20, leases.Length);
    }

    private sealed class StaticSettings(AiSettingsSnapshot settings) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<AiProviderState?>(new AiProviderState(code, code, $"https://{code}.test", false, true, "key", AiKeySource.OneBase, null));
    }

    private sealed class NullRecorder : IAiUsageRecorder
    {
        public Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class BlockedLimits(string message) : IAiLimits
    {
        public Task EnsureBudgetAsync(CancellationToken ct = default) => throw new LlmNotConfiguredException(message);

        public Task<IDisposable> EnterAsync(CancellationToken ct = default) => NoAiLimits.Instance.EnterAsync(ct);

        public Task SpentAsync(decimal costUsd, CancellationToken ct = default) => Task.CompletedTask;

        public Task<AiBudgetStatus> StatusAsync(CancellationToken ct = default) => Task.FromResult(new AiBudgetStatus(10, 10, 10, null, 80));
    }
}

/// <summary>Бюджет по записям ai.Usage в базе: траты дня, блокировка при исчерпании и одно уведомление администраторам AI.</summary>
public class AiBudgetDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Spending_the_daily_budget_blocks_new_calls_and_notifies_ai_admins_once()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();

        var role = new Role { Name = "AI-администратор теста бюджета" };
        role.Permissions.Add(new RolePermission { Code = Permissions.AiSettingsManage });
        var aiAdmin = new User { Login = "budget-ai-admin", FullName = "Администратор AI" };
        aiAdmin.Roles.Add(new UserRole { Role = role });
        db.Users.Add(aiAdmin);
        var settings = await db.AiSettings.FirstOrDefaultAsync(s => s.Id == AiSettings.SingletonId);
        if (settings is null)
        {
            settings = new AiSettings();
            db.AiSettings.Add(settings);
        }

        // Дневной бюджет $0.05; вызов Opus 5.5 ниже стоит $0.028 (вход, кэш, выход — по ценам миграции).
        settings.DailyBudgetUsd = 0.05m;
        settings.BudgetWarnPercent = 80;
        await db.SaveChangesAsync();
        fixture.Services.GetRequiredService<AiSettingsStore>().Invalidate();

        var limits = fixture.Services.GetRequiredService<IAiLimits>();
        var recorder = fixture.Services.GetRequiredService<IAiUsageRecorder>();
        var call = new AiCallRecord(new AiCallContext(aiAdmin.Id, null, null, "budget-test"), new AiModelRef("anthropic", "claude-opus-5-5"),
            new AiTokenUsage(1_000, 500, CacheReadTokens: 20_000, CacheWriteTokens: 2_000), 100, true, null);

        await limits.EnsureBudgetAsync(); // ещё ничего не потрачено
        await recorder.RecordAsync(call);
        var half = await limits.StatusAsync();
        Assert.Equal(0.028m, half.SpentToday);
        Assert.False(half.Exceeded);
        await limits.EnsureBudgetAsync();

        await recorder.RecordAsync(call);
        var spent = await limits.StatusAsync();
        Assert.Equal(0.056m, spent.SpentToday);
        Assert.True(spent.DailyExceeded);
        var error = await Assert.ThrowsAsync<LlmNotConfiguredException>(() => limits.EnsureBudgetAsync());
        Assert.Contains("Дневной бюджет на AI исчерпан", error.Message);

        // Ещё один вызов (например, уже начатый ход) — повторного уведомления нет.
        await recorder.RecordAsync(call);
        var notes = await db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == aiAdmin.Id).ToListAsync();
        var note = Assert.Single(notes);
        Assert.Equal((UserNotificationKind.AiBudget, "Дневной бюджет на AI исчерпан", "/settings/ai/usage"), (note.Kind, note.Title, note.Link));

        // Бюджет сняли — запросы снова идут.
        settings.DailyBudgetUsd = null;
        await db.SaveChangesAsync();
        fixture.Services.GetRequiredService<AiSettingsStore>().Invalidate();
        await limits.EnsureBudgetAsync();
    }
}

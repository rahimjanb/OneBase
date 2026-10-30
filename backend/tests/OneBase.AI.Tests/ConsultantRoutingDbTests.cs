using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Infrastructure;

namespace OneBase.AI.Tests;

/// <summary>
/// Ответ консультанта целиком на настоящей PostgreSQL — маршрутизатор, оркестратор, AI-сотрудник с настоящими инструментами и данными,
/// итоговый ответ с проверкой чисел; модель — подставная (по назначению вызова), настройки — свои у теста (база OneBase не меняется, сети нет).
/// Маршрутизатор и сотрудники — Sonnet по настройкам; итог — Sonnet на простой вопрос и Opus на поиск причин; перепроверка — исправление
/// той же моделью и один переход на Opus с теми же данными; данные недоступного пользователю отдела не запрашиваются.
/// </summary>
public class ConsultantRoutingDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly AiModelRef Opus = new("anthropic", "claude-opus-5-5");
    private static readonly AiModelRef Sonnet = new("anthropic", "claude-sonnet-5-5");

    private static readonly AiSettingsSnapshot Target = new(true, Opus, Sonnet, Sonnet, null, null, 16000, false, TaskRoutingEnabled: true, RouterMode: "on",
        Routing: AiModelRoutingSettings.From("on", Sonnet, "dynamic", Sonnet, Opus, "review", 3, 2, 1m));

    private ServiceProvider Services(RoutedModel model, AiSettingsSnapshot settings)
    {
        var config = fixture.Services.GetRequiredService<IConfiguration>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddInfrastructure(config);
        services.AddAi(config);
        services.AddSingleton<IFileStorage, MemoryStorage>();
        services.AddSingleton<OneBase.Application.DataSources.IGoogleSheetsClient>(new FakeSheets());
        services.AddSingleton<OneBase.Application.Telegram.ITelegramGateway>(new FakeTelegram());
        services.AddSingleton<OneBase.AI.DataManager.IDataManagerModel>(new FakeDataModel());
        services.AddSingleton<IAiGateway>(model);
        services.AddSingleton<IAiSettingsSource>(new Settings(settings));
        return services.BuildServiceProvider();
    }

    private async Task<(ConsultantAnswer Answer, AiTurnTimings Turn)> AskAsync(RoutedModel model, string question, AiSettingsSnapshot? settings = null)
    {
        await using var sp = Services(model, settings ?? Target);
        await AiSeeder.SeedAsync(sp);
        using var scope = sp.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IConsultantEngine>();
        var turn = AiTurnTimings.Start();
        var answer = await engine.AnswerAsync(new ConsultantTurn(fixture.AdminId, Guid.NewGuid(), question, []), _ => Task.CompletedTask, CancellationToken.None);
        return (answer, turn);
    }

    [PostgresFact]
    public async Task Router_and_agents_go_to_sonnet_and_the_final_answer_by_complexity()
    {
        await fixture.ResetAsync();

        var simple = new RoutedModel(["sales"]);
        var (plain, plainTurn) = await AskAsync(simple, "Как продажи за сентябрь?");
        Assert.Equal(Sonnet, simple.ModelOf("consultant.route"));
        Assert.All(simple.Calls.Where(c => c.Purpose == "agent"), c => Assert.Equal(Sonnet, c.Model));
        Assert.Equal(Sonnet, simple.ModelOf("consultant.answer"));
        Assert.Equal("anthropic/claude-sonnet-5-5", plain.Details.Model);
        Assert.Contains("стандартный вопрос", plainTurn.Routing!.FinalReason);
        Assert.Equal("completed", Assert.Single(plain.Details.Agents).Status);
        Assert.Contains("get_sales", plain.Details.Agents[0].ToolsUsed); // настоящий инструмент на данных теста

        var causal = new RoutedModel(["sales"]);
        var (why, whyTurn) = await AskAsync(causal, "Почему упали продажи в Хоразме в сентябре?");
        Assert.Equal(Sonnet, causal.ModelOf("consultant.route"));
        Assert.Equal(Opus, causal.ModelOf("consultant.answer"));
        Assert.Contains("сложный вопрос", whyTurn.Routing!.FinalReason);
        Assert.Contains("поиск причин", string.Join(" ", whyTurn.Routing.Complexity.Signals));
        Assert.Equal("anthropic/claude-opus-5-5", why.Details.Model);

        // Выключено — как раньше: всё основной моделью (null в запросе — основная из настроек шлюза).
        var off = new RoutedModel(["sales"]);
        await AskAsync(off, "Как продажи за сентябрь?", Target with { TaskRoutingEnabled = false, Routing = AiModelRoutingSettings.Off });
        Assert.All(off.Calls, c => Assert.Null(c.Model));
    }

    [PostgresFact]
    public async Task Unconfirmed_number_is_fixed_by_sonnet_then_once_by_opus_with_the_same_data_and_never_shown_as_fact()
    {
        await fixture.ResetAsync();
        var model = new RoutedModel(["sales"], final: (purpose, _) => "Итог: за сентябрь продано 9 999 кг.");

        var (answer, turn) = await AskAsync(model, "Сколько продали за сентябрь?");

        var finals = model.Calls.Where(c => c.Purpose.StartsWith("consultant.", StringComparison.Ordinal) && c.Purpose != "consultant.route").ToList();
        Assert.Equal([("consultant.answer", Sonnet), ("consultant.revise", Sonnet), ("consultant.review", Opus)], finals.Select(c => (c.Purpose, c.Model!)));
        Assert.Equal(finals[0].Messages[1].Content, finals[2].Messages[1].Content); // результаты сотрудников — те же при смене модели
        Assert.True(answer.Details.Grounding!.Warned);
        Assert.Contains("Не подтверждено данными OneBase", answer.Content);
        Assert.Equal((2, "anthropic/claude-opus-5-5"), (answer.Details.Grounding.Revisions, answer.Details.Grounding.EscalatedTo));
        Assert.Contains(turn.Reviews, r => r.Cause == AiReviewCauses.UnsupportedNumbers && r.Model == Opus.ToString());
    }

    [PostgresFact]
    public async Task Department_closed_to_the_user_is_not_asked_even_if_the_router_names_it()
    {
        await fixture.ResetAsync();
        // У администратора теста нет finance.read: финансист недоступен, хотя маршрутизатор его называет.
        var model = new RoutedModel(["sales", "finance"]);
        var (answer, _) = await AskAsync(model, "Почему упали продажи и прибыль?");

        Assert.DoesNotContain(model.Calls, c => c.Agent == "finance");
        Assert.DoesNotContain(answer.Details.Agents, a => a.Agent == "finance");
        Assert.Contains("нет доступа к данным", model.Calls.First(c => c.Purpose == "consultant.answer").Messages[1].Content);
    }

    /// <summary>Подставная модель по назначению вызова: маршрутизатор — заданные отделы; сотрудник — get_sales, затем итог JSON; ответ — текст.</summary>
    private sealed class RoutedModel(string[] agents, Func<string, int, string>? final = null) : IAiGateway
    {
        public ConcurrentQueue<(string Purpose, string? Agent, AiModelRef? Model, IReadOnlyList<LlmMessage> Messages)> Log { get; } = new();

        public IReadOnlyList<(string Purpose, string? Agent, AiModelRef? Model, IReadOnlyList<LlmMessage> Messages)> Calls => Log.ToList();

        public AiModelRef? ModelOf(string purpose) => Calls.First(c => c.Purpose == purpose).Model;

        public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

        public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
            CancellationToken cancellationToken = default)
        {
            var purpose = options.Context.Purpose;
            Log.Enqueue((purpose, options.Context.AgentCode, options.Model, messages.ToList()));
            var model = options.Model ?? Opus;
            AiCallResult Text(string text) => new(text, [], new AiTokenUsage(100, 50), model, false, 1, "end_turn");
            if (purpose == "consultant.route")
            {
                return Task.FromResult(Text(JsonSerializer.Serialize(new { agents, tasks = agents.ToDictionary(a => a, _ => "Продажи за сентябрь 2026"), reason = "тест" })));
            }

            if (purpose == "agent")
            {
                return Task.FromResult(messages.Any(m => m.Role == LlmRole.Tool)
                    ? Text("""{"agent":"sales","status":"completed","summary":"Данные о продажах получены.","findings":[],"metrics":[],"problems":[],"recommendations":[]}""")
                    : new AiCallResult(null, [new LlmToolCall("call_1", "get_sales", JsonSerializer.SerializeToElement(new { year = 2026, month = 9 }))],
                        new AiTokenUsage(100, 20), model, false, 1, "tool_use"));
            }

            var n = Calls.Count(c => c.Purpose.StartsWith("consultant.", StringComparison.Ordinal) && c.Purpose != "consultant.route") - 1;
            return Task.FromResult(Text(final?.Invoke(purpose, n) ?? "Итог: данные о продажах получены, подробности — в карточке отдела."));
        }

        public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Settings(AiSettingsSnapshot settings) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<AiProviderState?>(new AiProviderState(code, code, $"https://{code}.test", false, true, "key", AiKeySource.OneBase, null));
    }
}

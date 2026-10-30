using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Платная проверка — только по явному разрешению: ONEBASE_PAID_EVAL=1 и база проверки (ONEBASE_PAID_EVAL_DB).</summary>
public sealed class PaidEvalFactAttribute : FactAttribute
{
    public PaidEvalFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ONEBASE_PAID_EVAL") != "1" || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ONEBASE_PAID_EVAL_DB")))
        {
            Skip = "Платная проверка с настоящей моделью: только по явному разрешению (ONEBASE_PAID_EVAL=1, ONEBASE_PAID_EVAL_DB).";
        }
    }
}

/// <summary>
/// Парная платная проверка маршрутизации моделей на настоящих данных и настоящей модели: вариант A — настройки как сейчас, вариант B — целевая
/// конфигурация; варианты подставляются только в этом процессе (настройки в базе не меняются). Лимиты процесса — Ai:Safety:MaxCalls и
/// MaxCostUsd (ONEBASE_PAID_EVAL_MAX_CALLS / _MAX_USD), бюджет вопроса — у варианта B. Память, которую создал вопрос проверки, удаляется
/// после него: варианты не видят выводов друг друга, а память чатов пользователя не засоряется. Итог — JSON в ONEBASE_PAID_EVAL_OUT.
/// </summary>
public class PaidModelRoutingEval
{
    private static readonly AiModelRef Opus = new("anthropic", "claude-opus-5-5");
    private static readonly AiModelRef Sonnet = new("anthropic", "claude-sonnet-5-5");

    private sealed class Variant
    {
        public Func<AiSettingsSnapshot, AiSettingsSnapshot> Apply { get; set; } = s => s;
    }

    private sealed class VariantSettings(AiSettingsStore inner, Variant variant) : IAiSettingsSource
    {
        public async Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) => variant.Apply(await inner.GetSettingsAsync(ct));

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) => inner.GetProviderAsync(code, ct);
    }

    private static AiSettingsSnapshot Target(AiSettingsSnapshot s) => s with
    {
        Router = Sonnet,
        TaskRoutingEnabled = true,
        RouterMode = "on",
        Routing = AiModelRoutingSettings.From("on", Sonnet, "dynamic", Sonnet, Opus, "review", 3, 2, 0.6m),
    };

    [PaidEvalFact]
    public async Task Current_and_target_routing_on_the_same_questions()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/OneBase.Api"));
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root, "appsettings.json"))
            .AddJsonFile(Path.Combine(root, "appsettings.Development.json"), optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = Environment.GetEnvironmentVariable("ONEBASE_PAID_EVAL_DB"),
                ["Telegram:Enabled"] = "false",
                ["Sales:Sync:Enabled"] = "false",
                ["Ai:Safety:MaxCalls"] = Environment.GetEnvironmentVariable("ONEBASE_PAID_EVAL_MAX_CALLS") ?? "120",
                ["Ai:Safety:MaxCostUsd"] = Environment.GetEnvironmentVariable("ONEBASE_PAID_EVAL_MAX_USD") ?? "2.5",
            })
            .Build();
        var variant = new Variant();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(config);
        services.AddAi(config);
        services.AddSingleton<OneBase.Application.Abstractions.IFileStorage, MemoryStorage>();
        services.AddSingleton<OneBase.Application.Telegram.ITelegramGateway>(new FakeTelegram());
        services.AddSingleton<OneBase.AI.DataManager.IDataManagerModel>(new FakeDataModel());
        services.AddSingleton<IAiSettingsSource>(sp => new VariantSettings(sp.GetRequiredService<AiSettingsStore>(), variant));
        await using var sp = services.BuildServiceProvider();
        var userId = Guid.Parse(Environment.GetEnvironmentVariable("ONEBASE_PAID_EVAL_USER") ?? throw new InvalidOperationException("ONEBASE_PAID_EVAL_USER"));
        var report = new List<object>();

        // 1. Маршрутизатор на эталонном наборе: исходная модель (Opus) против Sonnet — те же сообщения, по вызову каждой модели на вопрос.
        variant.Apply = Target;
        using (var scope = Environment.GetEnvironmentVariable("ONEBASE_PAID_EVAL_SKIP_ROUTE") == "1" ? null : sp.CreateScope())
        if (scope is not null)
        {
            var run = await scope.ServiceProvider.GetRequiredService<RoutingEvaluation>().RunRouteAsync(userId, null, CancellationToken.None);
            report.Add(new { kind = "route-eval", run.Status, run.Total, run.Agree, run.Baseline, run.Candidate, run.Error,
                disagreements = run.Cases.Where(c => c.Baseline != c.Candidate || !c.BaselineCorrect || !c.CandidateCorrect) });
        }

        // 2. Полные ответы парой на одних данных.
        (string Question, IReadOnlyList<LlmMessage> History)[] questions =
        [
            ("Как продажи за октябрь?", []),
            ("Почему упали продажи в Самарканде в октябре?", []),
            ("Как продажи и задолженность дилеров по Хорезму в этом месяце?", []),
        ];
        foreach (var (question, history) in questions)
        {
            foreach (var (name, apply) in new (string, Func<AiSettingsSnapshot, AiSettingsSnapshot>)[] { ("A: как сейчас", s => s), ("B: целевая", Target) })
            {
                variant.Apply = apply;
                var conversation = Guid.NewGuid();
                var turn = AiTurnTimings.Start();
                var watch = Stopwatch.StartNew();
                object? result;
                try
                {
                    using var scope = sp.CreateScope();
                    var answer = await scope.ServiceProvider.GetRequiredService<IConsultantEngine>()
                        .AnswerAsync(new ConsultantTurn(userId, conversation, question, history), _ => Task.CompletedTask, CancellationToken.None);
                    var view = turn.View();
                    result = new
                    {
                        kind = "answer", variant = name, question, ms = watch.ElapsedMilliseconds, costUsd = turn.CostUsd, turn.CostIncomplete,
                        calls = view.Calls.Count,
                        byModel = view.Calls.GroupBy(c => c.Model).ToDictionary(g => g.Key, g => new
                        {
                            calls = g.Count(), input = g.Sum(c => c.InputTokens + c.CacheReadTokens + c.CacheWriteTokens), output = g.Sum(c => c.OutputTokens),
                        }),
                        byPurpose = view.Calls.GroupBy(c => c.Purpose).ToDictionary(g => g.Key, g => new { calls = g.Count(), ms = g.Sum(c => c.ProviderMs) }),
                        routing = turn.Routing,
                        reviews = turn.Reviews,
                        agents = answer.Details.Agents.Select(a => new { a.Agent, a.Status, tools = a.ToolsUsed, unverified = a.Unverified }),
                        grounding = answer.Details.Grounding,
                        text = answer.Content,
                    };
                }
                catch (Exception ex)
                {
                    result = new { kind = "answer", variant = name, question, ms = watch.ElapsedMilliseconds, costUsd = turn.CostUsd, error = ex.Message };
                }

                report.Add(result);
                using (var cleanup = sp.CreateScope())
                {
                    await cleanup.ServiceProvider.GetRequiredService<OneBaseDbContext>().AiMemories.Where(m => m.ConversationId == conversation).ExecuteDeleteAsync();
                }
            }
        }

        var output = Environment.GetEnvironmentVariable("ONEBASE_PAID_EVAL_OUT") ?? Path.Combine(Path.GetTempPath(), "onebase-paid-eval.json");
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
    }
}

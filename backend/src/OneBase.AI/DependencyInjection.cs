using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneBase.AI.Agents;
using OneBase.AI.Approvals;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Tools;

namespace OneBase.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration config)
    {
        // Ключи провайдеров задаются в «Настройки → AI»; переменные окружения — запасной вариант.
        services.AddSingleton(new AiEnvironment
        {
            Provider = config["Llm:Provider"] ?? config["LLM_PROVIDER"],
            ApiKey = config["Llm:ApiKey"] ?? config["LLM_API_KEY"],
            Model = config["Llm:Model"] ?? config["LLM_MODEL"],
            EmbeddingModel = config["Llm:EmbeddingModel"] ?? config["EMBEDDING_MODEL"],
            OpenAiApiKey = config["OPENAI_API_KEY"],
            AnthropicApiKey = config["ANTHROPIC_API_KEY"],
        });

        // Провайдеры LLM. Новый провайдер — ещё одна реализация IAiProvider.
        services.AddHttpClient(OpenAiProvider.ProviderCode, http => http.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient(AnthropicProvider.ProviderCode, http => http.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<IAiProvider, OpenAiProvider>();
        services.AddSingleton<IAiProvider, AnthropicProvider>();
        services.AddSingleton<AiProviderRegistry>();
        services.AddSingleton<AiSettingsStore>();
        services.AddSingleton<IAiSettingsSource>(sp => sp.GetRequiredService<AiSettingsStore>());

        services.TryAddSingleton<IAiUsageRecorder, NoUsageRecorder>();
        services.AddSingleton<AiGateway>();
        services.AddSingleton<IAiGateway>(sp => sp.GetRequiredService<AiGateway>());
        services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<AiGateway>());
        services.AddScoped<AiModelCatalog>();

        services.AddScoped<ITool, DelegateToAgentTool>();
        services.AddScoped<IToolRegistry, ToolRegistry>();
        services.AddScoped<ToolExecutor>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<IAiDirector, AiDirector>();

        return services;
    }
}

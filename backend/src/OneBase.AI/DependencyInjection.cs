using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneBase.AI.Agents;
using OneBase.AI.Approvals;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;

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

        // AI-сотрудники: настройки, права пользователя, выполнение задач.
        services.AddScoped<IUserPermissions, UserPermissions>();
        services.AddScoped<AiAgentStore>();
        services.AddScoped<AgentRunner>();

        // Консультант: чаты и движок ответа.
        // Консультант: маршрутизация → AI-сотрудники параллельно → единый ответ.
        services.AddScoped<OneBase.AI.Orchestration.AiRouter>();
        services.AddScoped<OneBase.AI.Orchestration.AgentOrchestrator>();
        services.AddScoped<IConsultantEngine, OneBase.AI.Orchestration.OrchestratedConsultantEngine>();
        services.AddScoped<ConsultantChatService>();

        services.AddScoped<ITool, DelegateToAgentTool>();

        // Инструменты данных OneBase — только чтение, с правами пользователя.
        services.AddScoped<ITool, GetSalesTool>();
        services.AddScoped<ITool, GetSalesByBranchTool>();
        services.AddScoped<ITool, GetSalesByEmployeeTool>();
        services.AddScoped<ITool, GetProblemAgentsTool>();
        services.AddScoped<ITool, GetProductsTool>();
        services.AddScoped<ITool, GetCustomersTool>();
        services.AddScoped<ITool, GetVisitsTool>();
        services.AddScoped<ITool, GetSalesKpiTool>();
        services.AddScoped<ITool, GetSalesTrendTool>();
        services.AddScoped<ITool, GetInventoryTool>();
        services.AddScoped<ITool, GetPrimaryShipmentsTool>();
        services.AddScoped<ITool, GetPaymentsTool>();
        services.AddScoped<ITool, GetSuppliersTool>();
        services.AddScoped<ITool, SearchKnowledgeTool>();

        // База знаний: документы, фрагменты, полнотекстовый и семантический поиск.
        services.AddScoped<OneBase.AI.Knowledge.KnowledgeService>();
        services.AddScoped<IToolRegistry, ToolRegistry>();
        services.AddScoped<ToolExecutor>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<IAiDirector, AiDirector>();

        return services;
    }
}

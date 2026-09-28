using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneBase.AI.Agents;
using OneBase.AI.Approvals;
using OneBase.AI.Llm;
using OneBase.AI.Tools;

namespace OneBase.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAi(this IServiceCollection services)
    {
        // Реальный провайдер регистрируется до AddAi и заменяет заглушку.
        services.TryAddSingleton<ILlmClient, NotConfiguredLlmClient>();

        services.AddScoped<ITool, DelegateToAgentTool>();
        services.AddScoped<IToolRegistry, ToolRegistry>();
        services.AddScoped<ToolExecutor>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<IAiDirector, AiDirector>();

        return services;
    }
}

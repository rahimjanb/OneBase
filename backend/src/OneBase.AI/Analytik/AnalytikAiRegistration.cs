using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Consultant;
using OneBase.AI.Monitoring;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Domain.AI;

namespace OneBase.AI.Analytik;

/// <summary>Службы AI-части раздела «Аналитик»: аналитика отделов, прогнозы, риски, отчёты, движок AI-аналитика, инструменты, фоновые задачи.</summary>
public static class AnalytikAiRegistration
{
    /// <summary>Ключ службы чатов «Аналитика» (ConsultantChatService с движком AnalytikEngine и своими чатами).</summary>
    public const string ChatKey = "analytik";

    public static IServiceCollection AddAnalytikAi(this IServiceCollection services, AnalytikOptions options, OneBase.Application.Recovery.RecoveryIsolation isolation)
    {
        services.AddScoped<SalesAnalytik>();
        services.AddScoped<FinanceAnalytik>();
        services.AddScoped<RiskEngine>();
        services.AddScoped<ForecastService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AnalytikReportService>();
        services.AddScoped<AnalytikJobs>();
        services.AddScoped<AnalytikEngine>();
        services.AddScoped<Intelligence.InvestigationEngine>();
        services.AddScoped<Intelligence.QualityService>();
        services.AddScoped<Intelligence.BenchmarkRunner>();
        services.AddScoped<Simulation.ScenarioBaselineBuilder>();
        services.AddScoped<Simulation.ScenarioService>();
        services.AddScoped<Simulation.ScenarioAnalyst>();
        services.AddScoped<Simulation.ScenarioExport>();
        services.AddScoped<Simulation.ScenarioAnalysisService>();
        services.AddKeyedScoped<ConsultantChatService>(ChatKey, (sp, _) => new ConsultantChatService(
            sp.GetRequiredService<IAppDbContext>(), sp.GetRequiredService<AnalytikEngine>(), sp.GetRequiredService<AiAuditService>(),
            sp.GetRequiredService<ConsultantAnswerCache>(), sp.GetRequiredService<IAuditLogger>(), sp.GetRequiredService<ILogger<ConsultantChatService>>())
        {
            Kind = ConversationKinds.Analytik,
        });

        services.AddScoped<ITool, FindTablesTool>();
        services.AddScoped<ITool, DescribeTableTool>();
        services.AddScoped<ITool, QueryTool>();
        services.AddScoped<ITool, ProfileColumnTool>();
        services.AddScoped<ITool, EntityTool>();
        services.AddScoped<ITool, DashboardTool>();
        services.AddScoped<ITool, SalesOverviewTool>();
        services.AddScoped<ITool, ProcurementPlanTool>();
        services.AddScoped<ITool, WarehouseTool>();
        services.AddScoped<ITool, ProductionTool>();
        services.AddScoped<ITool, FinanceTool>();
        services.AddScoped<ITool, HrTool>();
        services.AddScoped<ITool, ForecastTool>();
        services.AddScoped<ITool, RisksTool>();
        services.AddScoped<ITool, DataQualityTool>();
        services.AddScoped<ITool, ManagementReportTool>();
        services.AddScoped<ITool, Simulation.SimulateTool>();
        // Универсальный движок срезов — у консультанта и у аналитиков (источник sales.slices).
        services.AddScoped<ITool, AnalyticsQueryTool>();
        services.AddScoped<ITool, AnalyticsCapabilitiesTool>();
        // Advanced Intelligence: вычислительный движок и методы отделов — у аналитиков (источник analytik.intelligence).
        services.AddScoped<ITool, ComputeTool>();
        services.AddScoped<ITool, MethodsTool>();
        services.AddScoped<ITool, Simulation.ScenarioAnalysisTool>();
        services.AddScoped<ITool, DecisionsTool>();
        services.AddScoped<ITool, DecisionDraftTool>();

        // Копия для проверки восстановления (Recovery:Isolated) не рассылает уведомлений и не пишет отчётов — фоновые задачи выключены.
        if (options.WorkerEnabled && isolation.AllowOutbound<AnalytikWorker>("Аналитик: каталог, риски, прогнозы и отчёты по расписанию"))
        {
            services.AddHostedService<AnalytikWorker>();
        }

        return services;
    }
}

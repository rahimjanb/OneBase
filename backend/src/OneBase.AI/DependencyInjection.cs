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

        // Учёт вызовов моделей (токены, стоимость) и AI-журнал запросов.
        services.AddSingleton<IAiUsageRecorder, OneBase.AI.Monitoring.DbUsageRecorder>();
        services.AddScoped<OneBase.AI.Monitoring.AiAuditService>();
        services.AddSingleton<AiGateway>();
        services.AddSingleton<IAiGateway>(sp => sp.GetRequiredService<AiGateway>());
        services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<AiGateway>());

        // «Источники данных»: ИИ предлагает сопоставление столбцов Google Sheets с полями OneBase (только предложение — сохраняет человек).
        services.AddSingleton<OneBase.Application.DataSources.ISheetMappingAdvisor, DataSources.SheetMappingAdvisor>();
        services.AddScoped<AiModelCatalog>();

        // AI-сотрудники: настройки, права пользователя, выполнение задач.
        services.AddScoped<IUserPermissions, UserPermissions>();
        services.AddScoped<AiAgentStore>();
        services.AddScoped<SalesGlossary>();
        services.AddScoped<AgentRunner>();

        // Консультант: чаты и движок ответа.
        // Консультант: маршрутизация → AI-сотрудники параллельно → единый ответ.
        // Проактивный анализ: правила по данным OneBase по расписанию → AI Alerts.
        services.AddSingleton(new OneBase.AI.Proactive.ProactiveOptions
        {
            IntervalMinutes = int.TryParse(config["Ai:Proactive:IntervalMinutes"], out var interval) ? interval : 60,
        });
        services.AddSingleton<OneBase.AI.Proactive.ProactiveStatus>();
        services.AddScoped<OneBase.AI.Proactive.ProactiveAnalyzer>();
        services.AddHostedService<OneBase.AI.Proactive.ProactiveAnalysisWorker>();

        services.AddScoped<OneBase.AI.Memory.AiMemoryStore>();
        services.AddScoped<OneBase.AI.Orchestration.AiRouter>();
        services.AddScoped<OneBase.AI.Orchestration.AgentOrchestrator>();
        services.AddScoped<IConsultantEngine, OneBase.AI.Orchestration.OrchestratedConsultantEngine>();
        services.AddScoped<ConsultantChatService>();
        services.AddScoped<OneBase.AI.Reports.ReportService>();
        // Telegram: AI-агент аккаунтов — входящие из очереди TelegramInbox (её наполняет клиент Telegram из Infrastructure).
        services.AddScoped<OneBase.AI.Telegram.TelegramAgentService>();
        services.AddHostedService<OneBase.AI.Telegram.TelegramAgentWorker>();
        // Рассылки: мастер и управление (сервис), очередь в PostgreSQL (runner) и фоновая отправка.
        services.AddScoped<OneBase.AI.Telegram.TelegramCampaignService>();
        services.AddSingleton<OneBase.AI.Telegram.TelegramCampaignRunner>();
        services.AddHostedService<OneBase.AI.Telegram.TelegramCampaignWorker>();

        // Голос консультанта: вопрос голосом и озвучка ответа (OpenAI audio; ключ — из «Настройки → AI»).
        services.AddSingleton(config.GetSection(OneBase.AI.Voice.VoiceOptions.Section).Get<OneBase.AI.Voice.VoiceOptions>()
                              ?? new OneBase.AI.Voice.VoiceOptions());
        services.AddScoped<OneBase.AI.Voice.VoiceService>();

        // AI Data Manager: Claude (официальный SDK) + инструменты чтения и подготовки планов изменений.
        // Применение — только по подтверждению администратора (DataManagerChatService.ApplyAsync), у модели такого инструмента нет.
        services.AddSingleton<OneBase.AI.DataManager.IDataManagerModel, OneBase.AI.DataManager.ClaudeDataManagerModel>();
        services.AddScoped<OneBase.AI.DataManager.DataManagerTools>();
        services.AddScoped<OneBase.AI.DataManager.DataManagerChatService>();

        services.AddScoped<ITool, DelegateToAgentTool>();

        // Инструменты данных OneBase — только чтение, с правами пользователя.
        services.AddScoped<ITool, GetSalesTool>();
        services.AddScoped<ITool, GetSalesByBranchTool>();
        services.AddScoped<ITool, GetSalesByEmployeeTool>();
        services.AddScoped<ITool, GetProblemAgentsTool>();
        services.AddScoped<ITool, GetProductsTool>();
        services.AddScoped<ITool, FindProductsTool>();
        services.AddScoped<ITool, GetCustomersTool>();
        services.AddScoped<ITool, GetVisitsTool>();
        services.AddScoped<ITool, GetSalesKpiTool>();
        services.AddScoped<ITool, GetSalesTrendTool>();
        services.AddScoped<ITool, GetSkuSalesTool>();
        services.AddScoped<ITool, GetExportSalesTool>();
        services.AddScoped<ITool, GetDealerPnlTool>();
        services.AddScoped<ITool, GetInventoryTool>();
        services.AddScoped<ITool, GetOutstockTool>();
        services.AddScoped<ITool, GetFieldTeamTool>();
        services.AddScoped<ITool, GetFieldTasksTool>();
        services.AddScoped<ITool, GetFieldRoutesTool>();
        services.AddScoped<ITool, GetFieldRecommendationsTool>();
        services.AddScoped<ITool, GetDepartmentTasksTool>();
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

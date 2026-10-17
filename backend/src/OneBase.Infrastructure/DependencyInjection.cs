using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Minio;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Infrastructure.Linko;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Audit;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Sales;
using OneBase.Infrastructure.Storage;
using OneBase.Infrastructure.Vector;
using Qdrant.Client;
using StackExchange.Redis;

namespace OneBase.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // PostgreSQL — бизнес-данные и метаданные
        // Перехватчик сохранения ставит push-уведомления в очередь после успешного сохранения.
        services.AddSingleton<Push.PushQueue>();
        services.AddSingleton<Push.PushNotificationInterceptor>();
        services.AddDbContext<OneBaseDbContext>((sp, o) =>
            o.UseNpgsql(config.GetConnectionString("Postgres")).AddInterceptors(sp.GetRequiredService<Push.PushNotificationInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<OneBaseDbContext>());

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        // Redis — оперативное состояние, кэш, очереди. Подключение ленивое и не падает, если Redis недоступен.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(config.GetConnectionString("Redis") ?? "localhost:6379");
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        // MinIO — содержимое файлов
        var minio = config.GetSection(MinioOptions.Section).Get<MinioOptions>() ?? new MinioOptions();
        services.AddSingleton(minio);
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(minio.Endpoint)
            .WithCredentials(minio.AccessKey, minio.SecretKey)
            .WithSSL(minio.UseSsl)
            .Build());
        services.AddSingleton<IFileStorage, MinioFileStorage>();

        // «Файлы отделов»: сайт и подключение Windows (WebDAV) работают через одни и те же сервисы.
        services.AddSingleton(config.GetSection(OneBase.Application.Files.FilesOptions.Section).Get<OneBase.Application.Files.FilesOptions>()
                              ?? new OneBase.Application.Files.FilesOptions());
        services.AddScoped<Files.DepartmentFilesService>();
        services.AddScoped<Files.FileConnectionsService>();
        services.AddScoped<Files.FilePreviewService>();

        // Qdrant — векторы для RAG и семантической памяти
        var qdrant = config.GetSection(QdrantOptions.Section).Get<QdrantOptions>() ?? new QdrantOptions();
        services.AddSingleton(_ => new QdrantClient(qdrant.Host, qdrant.Port, qdrant.UseHttps, qdrant.ApiKey));
        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        // База знаний AI: текст из документов и полнотекстовый поиск PostgreSQL.
        services.AddSingleton<IDocumentTextExtractor, Knowledge.DocumentTextExtractor>();
        services.AddScoped<IKnowledgeFullTextSearch, Knowledge.KnowledgeFullTextSearch>();

        // Продажи: настройки и синхронизация с Linko SFA. Списки из настроек заменяют значения по умолчанию, а не дописываются к ним.
        var sales = SalesOptionsBinding.Load(config);
        services.AddSingleton(sales);
        services.TryAddSingleton(TimeProvider.System);

        // Секреты интеграций шифруются Data Protection; ключи хранятся в БД, чтобы переживать перезапуск контейнера.
        services.AddDataProtection()
            .SetApplicationName("OneBase")
            .PersistKeysToDbContext<OneBaseDbContext>();

        // Адрес и токен Linko задаются в «Настройки → Интеграции»; LINKO_BASE_URL / LINKO_TOKEN из .env — запасной вариант.
        var linko = config.GetSection(LinkoOptions.Section).Get<LinkoOptions>() ?? new LinkoOptions();
        linko.BaseUrl = config["LINKO_BASE_URL"] ?? string.Empty;
        linko.Token = config["LINKO_TOKEN"] ?? string.Empty;
        linko.PlanToken = config["LINKO_PLAN_TOKEN"] ?? string.Empty;
        services.AddSingleton(linko);
        services.AddSingleton(new LinkoThrottle(linko.Parallelism));
        services.AddSingleton<LinkoSettingsStore>();
        services.AddSingleton<LinkoSyncProgress>();
        services.AddHttpClient<LinkoClient>(http => http.Timeout = TimeSpan.FromSeconds(linko.TimeoutSeconds));
        services.AddScoped<LinkoSyncService>();
        services.AddScoped<LinkoVerifier>();
        services.AddScoped<ISalesHistoryReader, SalesHistoryReader>();
        services.AddMemoryCache();
        services.AddSingleton<SalesCacheSignal>();
        services.AddScoped<SalesDataLoader>();
        services.AddScoped<OneBase.Application.Sales.Stock.StockService>();
        services.AddScoped<OneBase.Application.Sales.Stock.OutstockService>();
        services.AddScoped<OneBase.Application.Sales.Primary.PrimaryService>();
        services.AddScoped<OneBase.Application.Sales.SkuSales.SkuSalesService>();
        services.AddScoped<OneBase.Application.Sales.Pnl.PnlService>();
        services.AddSingleton<LinkoSyncCoordinator>();

        // AI Data Manager: отчёты администратора → планы изменений данных OneBase (копия Linko не меняется).
        var dataManager = config.GetSection(OneBase.Application.DataManager.DataManagerOptions.Section)
                              .Get<OneBase.Application.DataManager.DataManagerOptions>()
                          ?? new OneBase.Application.DataManager.DataManagerOptions();
        if (config["DATA_MANAGER_MODEL"] is { Length: > 0 } dataManagerModel)
        {
            dataManager.Model = dataManagerModel;
        }

        services.AddSingleton(dataManager);
        services.AddSingleton<IReportReader, DataManager.ReportReader>();
        // Источники данных: таблицы Google Sheets → поля разделов продаж (через план изменений AI Data Manager), синхронизация по расписанию.
        // Строки таблиц Google — все: свой разбор xlsx без предела строк загрузок AI Data Manager (память бережёт предел размера книги в клиенте).
        var sheetReader = new DataManager.ReportReader(new OneBase.Application.DataManager.DataManagerOptions { MaxRowsPerSheet = int.MaxValue, MaxColumns = dataManager.MaxColumns });
        services.AddHttpClient(nameof(DataSources.GoogleSheetsClient), http => http.Timeout = TimeSpan.FromSeconds(60))
            .AddTypedClient<OneBase.Application.DataSources.IGoogleSheetsClient>((http, _) => new DataSources.GoogleSheetsClient(http, sheetReader, dataManager));
        services.AddSingleton<DataSources.DataSourceRunLocks>();
        services.AddScoped<DataSources.DataSourceOwnershipService>();
        // ИИ-сопоставление столбцов: пороги уверенности — DataSources:AutoMap (модель ИИ — ISheetMappingAdvisor из AddAi).
        services.AddSingleton(config.GetSection(OneBase.Application.DataSources.SheetMappingOptions.Section).Get<OneBase.Application.DataSources.SheetMappingOptions>()
                              ?? new OneBase.Application.DataSources.SheetMappingOptions());
        services.AddScoped<DataSources.DataSourceAutoMapService>();
        // AI Data Manager: подключённые таблицы Google Sheets — список, свежий лист, синхронизация привязки.
        services.AddScoped<OneBase.Application.DataSources.IDataSourceGateway, DataSources.DataSourceGateway>();
        services.AddScoped<DataSources.DataSourceService>();
        services.AddHostedService<DataSources.DataSourceSyncWorker>();

        // Отчёты AI-консультанта: файл Excel из листов отчёта.
        services.AddSingleton<OneBase.Application.Reports.IReportWorkbookWriter, Reports.ReportWorkbookWriter>();
        services.AddScoped<OneBase.Application.DataManager.DataFileService>();
        services.AddScoped<OneBase.Application.DataSources.FieldOwnership>();
        services.AddScoped<OneBase.Application.DataManager.DataChangeService>();
        services.AddScoped<OneBase.Application.DataManager.IDataChangeApplier, DataManager.DataChangeApplier>();

        // Журнал ошибок («Настройки → Журнал ошибок»): логгер кладёт в очередь, фоновая служба пишет в БД.
        services.AddSingleton<Logging.SystemLogQueue>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, Logging.SystemLoggerProvider>());
        services.AddHostedService<Logging.SystemLogWriter>();
        services.AddHostedService<LinkoSyncWorker>();

        // Sales Base (sales.1base.uz): полевые продажи поверх зеркала Linko.
        services.AddScoped<OneBase.Application.Field.ILinkoSalesProvider, OneBase.Application.Field.LinkoMirrorSalesProvider>();
        services.AddScoped<OneBase.Application.Field.FieldDirectory>();
        services.AddScoped<OneBase.Application.Field.FieldAccess>();
        services.AddScoped<OneBase.Application.Field.FieldAssignments>();
        services.AddScoped<OneBase.Application.Field.FieldNotifier>();
        services.AddScoped<OneBase.Application.Field.FieldSettingsStore>();
        services.AddScoped<OneBase.Application.Field.FieldMemberService>();
        services.AddScoped<OneBase.Application.Field.FieldRouteService>();
        services.AddScoped<OneBase.Application.Field.FieldCustomerService>();
        services.AddScoped<OneBase.Application.Field.FieldTaskService>();
        services.AddScoped<OneBase.Application.Field.FieldVisitService>();
        services.AddScoped<OneBase.Application.Field.FieldRecommendationService>();
        services.AddScoped<OneBase.Application.Field.FieldDashboardService>();
        services.AddScoped<Field.FieldStatsRefresher>();
        services.AddHostedService<Field.FieldWorker>();

        // Telegram: пользовательские аккаунты (MTProto, WTelegramClient) — один клиент на аккаунт на весь процесс; входящие → очередь агента.
        services.AddSingleton<OneBase.Application.Telegram.TelegramInbox>();
        services.AddSingleton<Telegram.TelegramCredentialsStore>();
        services.TryAddSingleton<OneBase.Application.Telegram.ITelegramGateway, Telegram.WTelegramGateway>();
        services.AddScoped<Telegram.TelegramAccountService>();
        services.AddScoped<OneBase.Application.Telegram.TelegramLinkService>();
        services.AddScoped<OneBase.Application.Telegram.TelegramMaterialService>();
        services.AddHostedService<Telegram.TelegramConnectionWorker>();

        // Раздел «Задачи» OneBase: задачи отделов и уведомления пользователей.
        services.AddScoped<OneBase.Application.Work.UserNotifier>();
        services.AddScoped<OneBase.Application.Work.WorkTaskService>();

        // Web Push (Lib.Net.Http.WebPush: aes128gcm + VAPID, как требует и Apple). Ключи VAPID — в базе, токены — в памяти
        // (Apple просит не обновлять JWT чаще раза в час); повтор по 429 — не больше двух раз.
        services.AddMemoryCache();
        services.AddMemoryVapidTokenCache();
        services.AddPushServiceClient(o => o.MaxRetriesAfter = 2);
        services.AddSingleton<Push.PushKeyProvider>();
        services.AddHostedService<Push.PushSender>();

        return services;
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minio;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Infrastructure.Linko;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Audit;
using OneBase.Infrastructure.Persistence;
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
        services.AddDbContext<OneBaseDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString("Postgres")));
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

        // Qdrant — векторы для RAG и семантической памяти
        var qdrant = config.GetSection(QdrantOptions.Section).Get<QdrantOptions>() ?? new QdrantOptions();
        services.AddSingleton(_ => new QdrantClient(qdrant.Host, qdrant.Port, qdrant.UseHttps, qdrant.ApiKey));
        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        // Продажи: настройки и синхронизация с Linko SFA
        var sales = config.GetSection(SalesOptions.Section).Get<SalesOptions>() ?? new SalesOptions();
        services.AddSingleton(sales);
        services.TryAddSingleton(TimeProvider.System);

        // Адрес и токен Linko — только из окружения (.env), не из appsettings.
        var linko = config.GetSection(LinkoOptions.Section).Get<LinkoOptions>() ?? new LinkoOptions();
        linko.BaseUrl = config["LINKO_BASE_URL"] ?? string.Empty;
        linko.Token = config["LINKO_TOKEN"] ?? string.Empty;
        services.AddSingleton(linko);
        services.AddHttpClient<LinkoClient>(http =>
        {
            if (linko.IsConfigured)
            {
                LinkoClient.Configure(http, linko);
            }
        });
        services.AddScoped<LinkoSyncService>();
        services.AddSingleton<LinkoSyncCoordinator>();
        services.AddHostedService<LinkoSyncWorker>();

        return services;
    }
}

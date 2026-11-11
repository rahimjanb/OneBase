using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Catalog;

namespace OneBase.Infrastructure.Analytik;

/// <summary>Службы раздела «Аналитик» (OneBase Intelligence): каталог базы, движок запросов, аналитика отделов, прогнозы, риски, отчёты, фон.</summary>
public static class AnalytikRegistration
{
    public static IServiceCollection AddAnalytik(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection("Analytik").Get<AnalytikOptions>() ?? new AnalytikOptions();
        services.AddSingleton(options);
        services.AddSingleton<IAnalytikSql, AnalytikSqlRunner>();
        services.AddSingleton<CatalogCache>();
        services.AddScoped<IAnalytikDb, AnalytikDb>();
        services.AddScoped<ISchemaReader, PostgresSchemaReader>();
        services.AddScoped<CatalogService>();
        services.AddScoped<OneBase.Application.Analytik.Query.AnalytikAccess>();
        services.AddScoped<OneBase.Application.Analytik.Query.QueryEngine>();
        services.AddScoped<OneBase.Application.Analytik.Supply.SupplyPlanner>();
        services.AddScoped<OneBase.Application.Analytik.Business.OperationsAnalytik>();
        services.AddScoped<OneBase.Application.Analytik.Risk.InsightStore>();
        services.AddScoped<OneBase.Application.Analytik.Quality.DataQualityEngine>();
        return services;
    }
}

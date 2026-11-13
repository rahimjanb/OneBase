using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Application.Sales;

namespace OneBase.Infrastructure.Sales;

/// <summary>
/// Первичный набор фото товаров (SeedData/product-photos рядом с сервером) — в хранилище после запуска: товарам, которым набор ещё не ставили.
/// Работает раз за запуск и ничего не перезаписывает (SalesProductPhotoService.SeedAsync). Хранилище недоступно — повторит при следующем
/// запуске; запуск сервера при этом не ждёт и не падает.
/// </summary>
public sealed class SalesProductPhotoSeedWorker(IServiceScopeFactory scopes, ILogger<SalesProductPhotoSeedWorker> logger) : BackgroundService
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "SeedData", "product-photos");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SalesProductPhotoService>().SeedAsync(Folder, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Фото товаров: набор при сервере не поставлен (повторится при следующем запуске)");
        }
    }
}

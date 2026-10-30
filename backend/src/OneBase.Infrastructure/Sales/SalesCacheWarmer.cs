using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Infrastructure.Sales;

/// <summary>
/// Прогрев отчётов продаж. После любого сброса кэша продаж (синхронизация Linko, загрузка Google Таблиц, менеджер данных, настройки
/// продаж — все они сбрасывают SalesCacheSignal) и после запуска сервера заранее считает то, что чаще всего нужно людям и
/// AI-консультанту: текущий месяц с частыми страницами, регионами и карточками категорий, прошлый месяц и остальные месяцы «Динамики продаж»,
/// затем шаги модулей (ISalesWarmStep: общие части остатков, первички и продаж по SKU для AI-инструментов).
/// Несколько сбросов подряд дают один прогрев (пауза Debounce); сброс во время прогрева прерывает его — посчитанное под старой
/// версией данных всё равно сразу устарело бы — и запускает новый. Месяцы считаются по одному; тот же месяц, запрошенный
/// пользователем во время прогрева, собирается один раз (SalesBuilds). Ошибка прогрева — в журнал, отчёт тогда посчитается по запросу.
/// </summary>
public sealed class SalesCacheWarmer(IServiceScopeFactory scopes, SalesCacheSignal signal, ILogger<SalesCacheWarmer> logger) : BackgroundService
{
    /// <summary>Пауза после сброса: загрузка таблицы или применение плана сбрасывают кэш несколько раз подряд.</summary>
    public TimeSpan Debounce { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>После запуска сервера: дать ему подняться (миграции, первая синхронизация по расписанию).</summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>Месяцев «Динамики продаж» по умолчанию (get_sales_trend): текущий, прошлый и ещё четыре.</summary>
    public const int TrendMonths = 6;

    private readonly SemaphoreSlim _wake = new(0);
    private CancellationTokenSource? _pass;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = ChangeToken.OnChange(() => signal.Token, OnInvalidated);
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await RunPassAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await _wake.WaitAsync(stoppingToken);
                await Task.Delay(Debounce, stoppingToken);
                while (_wake.CurrentCount > 0)
                {
                    await _wake.WaitAsync(stoppingToken); // сбросы за паузу — тот же прогрев
                }

                await RunPassAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void OnInvalidated()
    {
        _wake.Release();
        try
        {
            _pass?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        using var pass = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _pass = pass;
        try
        {
            await WarmAsync(pass.Token);
        }
        catch (OperationCanceledException) when (pass.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Прогрев отчётов продаж прерван новым обновлением данных — начнётся заново");
        }
        finally
        {
            _pass = null;
        }
    }

    /// <summary>
    /// Один прогрев: текущий месяц и частые страницы («Обзор», «Республика», «Проблемы», «Планы», «Товары и категории»), затем
    /// прошлый месяц и остальные месяцы динамики (страница «Республика» — из неё строится «Динамика продаж»); steps — затем шаги модулей
    /// (ISalesWarmStep). Ошибка — в журнал.
    /// </summary>
    public async Task WarmAsync(CancellationToken ct, bool steps = true)
    {
        var watch = Stopwatch.StartNew();
        var warmed = 0;
        try
        {
            using var scope = scopes.CreateScope();
            var loader = scope.ServiceProvider.GetRequiredService<SalesDataLoader>();
            var current = await loader.LoadAsync(null, null, ct);
            current.CachedOverview();
            foreach (var region in current.CachedRepublic(null, null).Regions)
            {
                current.CachedRegion(Guid.Parse(region.Id), null, null, "kg", null); // страницы регионов и команды ТП (get_sales_by_employee)
            }

            current.CachedProblems(null, null, false);
            current.CachedPlans();
            current.CachedAssortment(null, null); // «Товары и категории» и поиск товаров AI-консультанта
            var republic = new AssortmentScope(null, null, null, false);
            foreach (var category in current.CategoryGlossary())
            {
                ct.ThrowIfCancellationRequested();
                current.CachedCategory(category.CategoryId, republic); // карточки категорий и типов Linko в ответах find_products
            }

            warmed++;

            foreach (var month in (await loader.MonthsAsync(ct)).Take(TrendMonths))
            {
                ct.ThrowIfCancellationRequested();
                var analytics = await loader.LoadAsync(month.Year, month.Month, ct);
                if (ReferenceEquals(analytics, current))
                {
                    continue;
                }

                analytics.CachedOverview();
                analytics.CachedRepublic(null, null);
                warmed++;
            }

            var monthsMs = watch.ElapsedMilliseconds;

            // Общие части других отчётов (шаги модулей: инструменты AI — остатки, первичка, продажи по SKU): по одному, сбой шага — в журнал.
            var done = new List<string>();
            foreach (var step in steps ? scope.ServiceProvider.GetServices<ISalesWarmStep>() : [])
            {
                ct.ThrowIfCancellationRequested();
                var stepWatch = Stopwatch.StartNew();
                try
                {
                    await step.WarmAsync(ct);
                    done.Add($"{step.Name} {stepWatch.ElapsedMilliseconds}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Прогрев отчётов продаж: шаг «{Step}» не выполнен — посчитается по запросу", step.Name);
                }
            }

            logger.LogInformation("Прогрев отчётов продаж: месяцев {Months} за {Ms} мс, всего {Total} мс ({Steps})", warmed, monthsMs, watch.ElapsedMilliseconds,
                done.Count == 0 ? "без шагов" : string.Join(", ", done));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Не удалось заранее пересчитать отчёты продаж (месяцев готово: {Months}) — посчитаются по запросу", warmed);
        }
    }
}

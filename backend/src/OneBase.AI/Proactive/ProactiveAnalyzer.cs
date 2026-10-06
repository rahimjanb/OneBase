using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Tools.Data;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.Stock;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Sales;

namespace OneBase.AI.Proactive;

/// <summary>Находка до записи в базу.</summary>
public sealed record AlertCandidate(
    string Category, string Rule, string Key, AiAlertSeverity Severity, string Title, string Message, string? Recommendation, string? Href, string? RequiredPermission);

public sealed record ProactiveRunResult(DateTimeOffset At, int Active, int Created, int Resolved, IReadOnlyList<string> Errors);

/// <summary>
/// Проактивный анализ: правила по данным OneBase (без модели — цифры точные, проверка бесплатная).
/// Находит снижение продаж, риск невыполнения плана, заканчивающиеся запасы, проблемных агентов и возможности роста.
/// Только читает данные; в Linko ничего не отправляет.
/// </summary>
public sealed class ProactiveAnalyzer(
    IAppDbContext db,
    SalesDataLoader sales,
    StockService stock,
    PrimaryService primary,
    ProactiveStatus status,
    ILogger<ProactiveAnalyzer> logger)
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>До этого дня месяца сравнения с прошлым месяцем и прогноз слишком шумные.</summary>
    private const int MinWorkedDays = 5;

    public async Task<ProactiveRunResult> RunAsync(CancellationToken ct)
    {
        var found = new List<AlertCandidate>();
        var evaluated = new HashSet<string>();
        var errors = new List<string>();

        await Evaluate("Продажи", async () => found.AddRange(await SalesAsync(evaluated, ct)), errors);
        await Evaluate("Остатки", async () => found.AddRange(await StockAsync(evaluated, ct)), errors);
        await Evaluate("Первичка", async () => found.AddRange(await PrimaryAsync(evaluated, ct)), errors);

        var now = DateTimeOffset.UtcNow;
        var active = await db.AiAlerts.Where(a => a.ResolvedAt == null).ToListAsync(ct);
        var created = 0;
        foreach (var c in found.GroupBy(c => c.Key).Select(g => g.First()))
        {
            var alert = active.FirstOrDefault(a => a.Key == c.Key);
            if (alert is null)
            {
                db.AiAlerts.Add(new AiAlert
                {
                    Category = c.Category,
                    Rule = c.Rule,
                    Key = c.Key,
                    Severity = c.Severity,
                    Title = c.Title,
                    Message = c.Message,
                    Recommendation = c.Recommendation,
                    Href = c.Href,
                    RequiredPermission = c.RequiredPermission,
                    DetectedAt = now,
                    LastSeenAt = now,
                });
                created++;
            }
            else
            {
                alert.Severity = c.Severity;
                alert.Title = c.Title;
                alert.Message = c.Message;
                alert.Recommendation = c.Recommendation;
                alert.Href = c.Href;
                alert.LastSeenAt = now;
                alert.UpdatedAt = now;
            }
        }

        // Закрываем находки правил, которые проверены в этот раз и больше не срабатывают.
        var keys = found.Select(c => c.Key).ToHashSet();
        var resolved = 0;
        foreach (var alert in active.Where(a => evaluated.Contains(a.Rule) && !keys.Contains(a.Key)))
        {
            alert.ResolvedAt = now;
            resolved++;
        }

        await db.SaveChangesAsync(ct);
        var result = new ProactiveRunResult(now, keys.Count, created, resolved, errors);
        status.Set(result);
        return result;
    }

    private async Task Evaluate(string area, Func<Task> run, List<string> errors)
    {
        try
        {
            await run();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Проактивный анализ: не удалось проверить «{Area}»", area);
            errors.Add($"{area}: {ex.Message}");
        }
    }

    internal async Task<IReadOnlyList<AlertCandidate>> SalesAsync(HashSet<string> evaluated, CancellationToken ct)
    {
        var analytics = await sales.LoadAsync(null, null, ct);
        var republic = analytics.CachedRepublic(null, null);
        var overview = analytics.CachedOverview();
        var p = republic.Period;
        var month = $"{p.Year}-{p.Month:00}";
        var period = $"с 01.{p.Month:00} по {p.DataThrough:dd.MM.yyyy}";
        var query = DataTool.Query(p.Year, p.Month);
        var list = new List<AlertCandidate>();
        evaluated.UnionWith(["sales.region.drop", "sales.region.plan", "sales.republic.plan", "sales.region.opportunity", "sales.agents.critical", "sales.assortment.gap"]);

        if (p.WorkedDays >= MinWorkedDays)
        {
            foreach (var row in republic.SameDays.Where(r => r.KgBefore >= 1000 && r.KgDelta is <= -0.15m))
            {
                var drop = -row.KgDelta!.Value;
                list.Add(new AlertCandidate("sales", "sales.region.drop", $"sales.region.drop:{row.Id}:{month}",
                    drop >= 0.30m ? AiAlertSeverity.Critical : AiAlertSeverity.Warning,
                    $"Продажи в регионе {row.Name} снизились на {Pct(drop)}",
                    $"{period}: {Kg(row.KgNow)} против {Kg(row.KgBefore)} за те же дни прошлого месяца (−{Pct(drop)}). " +
                    $"Выручка {Money(row.SumNow)} ({Signed(row.SumDelta)}), АКБ {row.AkbNow} ({Signed(row.AkbDelta)}).",
                    "Разобрать с руководителем региона, какие ТП и категории дали снижение; проверить проблемных агентов и «молчащие» точки региона.",
                    $"/sales/regions/{row.Id}?{query}", Permissions.SalesRead));
            }

            // Месяц закрыт — пишем «не выполнен» (прогноза у закрытого месяца нет — берётся выполнение); идёт — «под угрозой»
            // с прогнозом на конец месяца.
            var closed = p.DataThrough.Day >= p.DaysInMonth;
            static decimal? Expected(UnitRow r) => r.ForecastExecution ?? r.Execution;
            var behind = republic.Regions.Where(r => r.PlanKg is > 0 && Expected(r) is < 0.9m).OrderBy(Expected).ToList();
            foreach (var r in behind.Where(r => Expected(r) < 0.75m))
            {
                var forecast = Expected(r)!.Value;
                list.Add(new AlertCandidate("sales", "sales.region.plan", $"sales.region.plan:{r.Id}:{month}", AiAlertSeverity.Critical,
                    closed ? $"{r.Name}: план месяца не выполнен — {Pct(forecast)}" : $"{r.Name}: план месяца под угрозой — прогноз {Pct(forecast)}",
                    closed
                        ? $"План {Kg(r.PlanKg!.Value)}, факт {Kg(r.FactKg)} ({Pct(r.Execution)}) {period}."
                        : $"План {Kg(r.PlanKg!.Value)}, факт {Kg(r.FactKg)} ({Pct(r.Execution)}) {period}; при текущем темпе к концу месяца — {Kg(r.ForecastKg ?? r.FactKg)}.",
                    closed
                        ? "Разобрать причины по ТП и категориям региона и заложить меры в план следующего месяца."
                        : "Проверить выполнение плана по ТП региона и категориям; определить, где добрать объём до конца месяца.",
                    $"/sales/regions/{r.Id}?{query}", Permissions.SalesRead));
            }

            var lagging = behind.Where(r => Expected(r) >= 0.75m).ToList();
            if (lagging.Count > 0)
            {
                list.Add(new AlertCandidate("sales", "sales.region.plan", $"sales.region.plan.group:{month}", AiAlertSeverity.Warning,
                    closed ? $"Ещё {lagging.Count} регионов не дотянули до 90% плана" : $"Ещё {lagging.Count} регионов идут ниже 90% плана",
                    string.Join("; ", lagging.Select(r => $"{r.Name} — {Pct(Expected(r))} ({Kg(r.FactKg)} из {Kg(r.PlanKg!.Value)})")) + ".",
                    "Сравнить с регионами, выполняющими план: страйк, АКБ на агента, ширина ассортимента.",
                    $"/sales/republic?{query}", Permissions.SalesRead));
            }

            foreach (var r in republic.Regions.Where(r => r.PlanKg is > 0 && Expected(r) is >= 1.1m))
            {
                list.Add(new AlertCandidate("sales", "sales.region.opportunity", $"sales.region.opportunity:{r.Id}:{month}", AiAlertSeverity.Opportunity,
                    $"{r.Name} перевыполняет план — прогноз {Pct(Expected(r))}",
                    $"План {Kg(r.PlanKg!.Value)}, прогноз {Kg(r.ForecastKg ?? r.FactKg)}; АКБ {r.Akb}, страйк {Pct(r.Strike)}.",
                    "Разобрать практики региона для других регионов; проверить, не занижен ли план.",
                    $"/sales/regions/{r.Id}?{query}", Permissions.SalesRead));
            }

            var k = republic.Kpi;
            var expected = k.ForecastExecution ?? k.Execution;
            if (k.PlanKg is > 0 && expected is < 0.95m)
            {
                list.Add(new AlertCandidate("sales", "sales.republic.plan", $"sales.republic.plan:{month}",
                    expected < 0.85m ? AiAlertSeverity.Critical : AiAlertSeverity.Warning,
                    p.DataThrough.Day >= p.DaysInMonth ? $"План продаж компании не выполнен — {Pct(expected)}" : $"План продаж компании под угрозой — прогноз {Pct(expected)}",
                    $"План {Kg(k.PlanKg.Value)}, факт {Kg(k.FactKg)} ({Pct(k.Execution)}) {period}" +
                    (p.DataThrough.Day >= p.DaysInMonth ? ". " : $", прогноз {Kg(k.ForecastKg ?? k.FactKg)}. ") +
                    $"Регионов с прогнозом ниже 90%: {republic.Regions.Count(r => r.PlanKg is > 0 && Expected(r) is < 0.9m)}.",
                    "Сосредоточиться на регионах с наибольшим отставанием в кг; проверить дефицит на складах этих регионов.",
                    $"/sales/republic?{query}", Permissions.SalesRead));
            }
        }

        if (overview.Flags.Critical > 0)
        {
            list.Add(new AlertCandidate("sales", "sales.agents.critical", $"sales.agents.critical:{month}",
                overview.Flags.Critical >= 10 ? AiAlertSeverity.Critical : AiAlertSeverity.Warning,
                $"Торговых представителей с критичными замечаниями: {overview.Flags.Critical}",
                $"Критичных — {overview.Flags.Critical}, с риском — {overview.Flags.Risk} из {overview.ActiveAgents} ТП ({DataTool.MonthName(p.Month)} {p.Year}): " +
                "низкий страйк, визиты без продаж, падение темпа.",
                "Разобрать список проблемных агентов с руководителями регионов, начиная с критичных.",
                $"/sales/problems?{query}", Permissions.SalesRead));
        }

        var assortment = analytics.CachedAssortment(null, null);
        foreach (var r in assortment.Regions.Where(r => r.SkuNotCarried >= 10).OrderByDescending(r => r.SkuNotCarried).Take(5))
        {
            list.Add(new AlertCandidate("sales", "sales.assortment.gap", $"sales.assortment.gap:{r.Id}:{month}", AiAlertSeverity.Opportunity,
                $"{r.Name}: {r.SkuNotCarried} SKU продаются в других регионах, но не здесь",
                $"В регионе продаётся {r.SkuSelling} SKU; ещё {r.SkuNotCarried} SKU, которые берут в других регионах, здесь в этом месяце не продавались.",
                "Проверить наличие этих SKU на складе региона и включить их в задачи ТП.",
                $"/sales/assortment?{query}&region={r.Id}", Permissions.SalesRead));
        }

        return list;
    }

    internal async Task<IReadOnlyList<AlertCandidate>> StockAsync(HashSet<string> evaluated, CancellationToken ct)
    {
        var view = await stock.GetAsync(null, ct);
        var list = new List<AlertCandidate>();
        evaluated.UnionWith(["supply.stock.deficit", "supply.stock.dead"]);
        var day = (view.SyncedAt ?? DateTimeOffset.UtcNow).ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-dd");

        var urgent = view.Items.Where(i => i.Status == StockStatuses.Deficit && i.DaysOfCover is < 7).OrderBy(i => i.DaysOfCover).ToList();
        if (view.Totals.Deficit > 0)
        {
            var names = string.Join("; ", view.Items.Where(i => i.Status == StockStatuses.Deficit).OrderBy(i => i.DaysOfCover).Take(5)
                .Select(i => $"{i.Name.Trim()} — {Days(i.DaysOfCover)}"));
            list.Add(new AlertCandidate("supply", "supply.stock.deficit", "supply.stock.deficit",
                urgent.Count >= 5 ? AiAlertSeverity.Critical : AiAlertSeverity.Warning,
                $"Запасы заканчиваются: {view.Totals.Deficit} SKU меньше чем на 15 дней продаж",
                $"Меньше недели продаж — {urgent.Count} SKU. Самые срочные: {names}.",
                "Заказать первичку по дефицитным SKU у завода; проверить остаток завода по этим товарам.",
                "/sales/stock", Permissions.SalesRead));
        }

        if (view.Totals.Dead >= 5)
        {
            list.Add(new AlertCandidate("supply", "supply.stock.dead", "supply.stock.dead", AiAlertSeverity.Warning,
                $"{view.Totals.Dead} SKU лежат на складах без продаж",
                $"Остаток есть, а продаж за {view.VelocityDays} дней нет (данные на {day}). Затоварка (больше 30 дней продаж) — ещё {view.Totals.Overstock} SKU.",
                "Решить судьбу неликвида: перераспределить между регионами, акция или возврат на завод.",
                "/sales/stock", Permissions.SalesRead));
        }

        return list;
    }

    internal async Task<IReadOnlyList<AlertCandidate>> PrimaryAsync(HashSet<string> evaluated, CancellationToken ct)
    {
        var view = await primary.GetAsync(null, null, ct);
        evaluated.Add("sales.primary.plan");
        if (view.PlanMonthKg is not > 0 || view.WorkedDays < MinWorkedDays)
        {
            return [];
        }

        var forecast = (view.ForecastKg ?? view.MonthTotal.Kg) / view.PlanMonthKg.Value;
        if (forecast >= 0.9m)
        {
            return [];
        }

        return
        [
            new AlertCandidate("sales", "sales.primary.plan", $"sales.primary.plan:{view.Year}-{view.Month:00}",
                forecast < 0.75m ? AiAlertSeverity.Critical : AiAlertSeverity.Warning,
                $"Первичка отстаёт от плана — прогноз {Pct(forecast)}",
                $"Отгружено дилерам {Kg(view.MonthTotal.Kg)} из плана {Kg(view.PlanMonthKg.Value)} ({DataTool.MonthName(view.Month)} {view.Year}), прогноз {Kg(view.ForecastKg ?? view.MonthTotal.Kg)}.",
                "Сверить заявки дилеров с остатками их складов; проверить, каким дилерам не отгружено по плану.",
                $"/sales/primary/republic?{DataTool.Query(view.Year, view.Month)}", Permissions.SalesRead),
        ];
    }

    private static string Kg(decimal kg) => kg >= 1000 ? $"{(kg / 1000).ToString("#,0.0", Ru)} т" : $"{kg.ToString("#,0", Ru)} кг";

    private static string Money(decimal sum) => sum >= 1_000_000 ? $"{(sum / 1_000_000).ToString("#,0.0", Ru)} млн сум" : $"{sum.ToString("#,0", Ru)} сум";

    private static string Pct(decimal? share) => share is null ? "—" : $"{(share.Value * 100).ToString("0", Ru)}%";

    private static string Signed(decimal? share) => share is null ? "—" : $"{(share.Value >= 0 ? "+" : "−")}{Math.Abs(share.Value * 100).ToString("0", Ru)}%";

    private static string Days(decimal? days) => days is null ? "—" : $"{days.Value.ToString("0.#", Ru)} дн.";
}

/// <summary>Последний запуск проактивного анализа (в памяти процесса).</summary>
public sealed class ProactiveStatus
{
    private volatile ProactiveRunResult? _last;

    public ProactiveRunResult? Last => _last;

    public void Set(ProactiveRunResult result) => _last = result;
}

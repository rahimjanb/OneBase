using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Tools.Data;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Forecasting;
using OneBase.Application.Analytik.Quality;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Sales;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Production;

namespace OneBase.AI.Analytik;

public sealed record RiskRunResult(InsightRunResult Insights, IReadOnlyList<string> Rules, IReadOnlyList<string> Errors, long DurationMs);

/// <summary>
/// Anomaly Detection &amp; Risk Engine — правила кодом, без модели: отклонение дневной выручки (с разбором по регионам — какой регион дал
/// отклонение, это подтверждено данными; остальное — гипотезы), риск остановки производства из-за сырья (план закупок), просрочки и перерасход
/// производства, рост цен сырья, сроки годности, брак, опоздания и отсутствия, отклонение оплат, качество данных. Находки продаж, которые уже
/// ищет проактивный анализ AI (ai.Alerts: падение регионов, план, первичка, проблемные агенты, остатки дилеров), здесь не дублируются —
/// раздел «Риски» показывает их рядом.
/// </summary>
public sealed class RiskEngine(
    IAppDbContext db,
    SalesAnalytik sales,
    SalesDataLoader loader,
    SupplyPlanner supply,
    ProductionAnalyticsService production,
    HrService hr,
    DataQualityEngine quality,
    InsightStore store,
    TimeProvider clock,
    ILogger<RiskEngine> logger)
{
    private static readonly AnalytikPrincipal SystemUser = AnalytikPrincipal.SystemPrincipal;

    private static readonly ProcurementContext SystemProcurement =
        new(Guid.Empty, new HashSet<string> { Permissions.ProductionRead, Permissions.ProcurementRead, Permissions.ProcurementManage }, null, new HashSet<Guid>(),
            IsDirector: true);

    public async Task<RiskRunResult> RunAsync(bool notify, CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var found = new List<InsightCandidate>();
        var evaluated = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();
        var today = FieldClock.LocalDate(clock.GetUtcNow());

        async Task Rule(string name, Func<Task> run, params string[] rules)
        {
            try
            {
                await run();
                evaluated.UnionWith(rules);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Аналитик: правило {Rule} не проверено", name);
                errors.Add($"{name}: {ex.Message}");
            }
        }

        await Rule("Продажи по дням", async () => found.AddRange(await SalesAnomalyAsync(today, ct)), "sales.daily_anomaly");
        await Rule("Оплаты по дням", async () => found.AddRange(await PaymentsAnomalyAsync(today, ct)), "finance.payments_anomaly");
        await Rule("Сырьё и закупки", async () => found.AddRange(await SupplyAsync(ct)), "supply.stockout", "supply.dead_stock", "supply.overdue_delivery");
        await Rule("Производство", async () => found.AddRange(await ProductionAsync(today, ct)),
            "production.overdue_orders", "production.overconsumption", "production.price_growth", "production.expiring", "production.expired", "production.defects");
        await Rule("Персонал", async () => found.AddRange(await HrAsync(today, ct)), "hr.lateness_growth", "hr.absence_growth");
        await Rule("Качество данных", async () =>
        {
            var (q, rules) = await quality.RunAsync(ct);
            found.AddRange(q);
            evaluated.UnionWith(rules);
        });

        var result = await store.ApplyAsync(found, evaluated, notify, ct);
        return new RiskRunResult(result, evaluated.Order().ToList(), errors, watch.ElapsedMilliseconds);
    }

    // ---- продажи ----

    /// <summary>
    /// Выручка последнего полного дня против того же дня недели за 8 недель (робастный z). Отклонение раскладывается по регионам — вклад каждого
    /// региона в разницу с его обычным уровнем (подтверждено данными); свежесть Linko проверяется — устаревшие данные называются причиной.
    /// </summary>
    internal async Task<IReadOnlyList<InsightCandidate>> SalesAnomalyAsync(DateOnly today, CancellationToken ct)
    {
        var day = today.AddDays(-1);
        var series = await sales.DailyAsync("revenue", day.AddDays(-63), day, SystemUser, ct);
        if (series.Count == 0 || series[^1].Date != day || AnomalyMath.Latest(series, "day") is not { } hit || Math.Abs(hit.Z) < AnomalyMath.Threshold)
        {
            return [];
        }

        var drivers = await RegionDriversAsync(day, ct);
        var orders = await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "orders", ct);
        var stale = orders?.LastSuccessAt is { } success && success < FieldClock.DayStartUtc(today);
        var down = hit.Direction == "down";
        var hypotheses = down
            ? new List<string> { "Праздничный или нерабочий день в части регионов", "Перебои с отгрузкой или наличием товара у дилеров", "Заказы ещё не приняты в Linko (приёмка позже)" }
            : new List<string> { "Акция или крупный заказ", "Перенос заказов с соседних дней" };
        return
        [
            new InsightCandidate(down ? InsightKinds.Anomaly : InsightKinds.Opportunity, "sales", "sales.daily_anomaly", $"sales.daily_anomaly:{day:yyyy-MM-dd}",
                down ? AnomalyMath.Severity(hit.Z) : Severities.Info,
                $"Выручка {day:dd.MM} {(down ? "ниже" : "выше")} обычной на {Math.Abs(hit.ChangePercent ?? 0):0.#}%",
                $"Выручка за {day:dd.MM.yyyy} — {hit.Value:N0} сум при обычных {hit.Expected:N0} сум для этого дня недели (медиана {hit.Baseline} прошлых недель)." +
                (drivers.Count > 0 ? " Основной вклад: " + string.Join("; ", drivers.Take(3).Select(d => d.Text)) + "." : ""),
                Cause: stale ? $"Данные заказов Linko не обновлялись с {orders!.LastSuccessAt!.Value.ToOffset(TimeSpan.FromHours(5)):dd.MM HH:mm} — цифры дня неполные." : null,
                CauseConfirmed: stale, Hypotheses: stale ? null : hypotheses, Impact: hit.Value - hit.Expected, ImpactUnit: "сум",
                Consequences: down ? "Если отклонение повторится, месячный план может быть не выполнен." : null,
                Recommendations: down
                    ? ["Разобрать день в «Продажи → Республика» по регионам из списка вклада", "Проверить с РМ этих регионов причину и отгрузки дилеров"]
                    : ["Проверить, за счёт чего рост, и можно ли повторить его в других регионах"],
                Evidence: new { Day = day, hit.Value, hit.Expected, hit.Z, hit.ChangePercent, hit.Basis, Drivers = drivers.Select(d => d.Text) },
                Href: $"/sales/republic?year={day.Year}&month={day.Month}", RequiredPermission: Permissions.SalesRead),
        ];
    }

    private async Task<List<(decimal Delta, string Text)>> RegionDriversAsync(DateOnly day, CancellationToken ct)
    {
        var months = (await loader.MonthsAsync(ct)).Select(m => (m.Year, m.Month)).ToHashSet();
        async Task<ISalesMonth?> Month(int y, int m, CancellationToken token) => months.Contains((y, m)) ? new AnalyticsMonth(await loader.LoadAsync(y, m, token)) : null;
        var target = await SalesPeriodEngine.AggregateAsync(new DateRange(day, day), new SalesScope(), Month, true, false, ct);
        var baseline = new List<PeriodSales>();
        for (var w = 1; w <= 8; w++)
        {
            baseline.Add(await SalesPeriodEngine.AggregateAsync(new DateRange(day.AddDays(-7 * w), day.AddDays(-7 * w)), new SalesScope(), Month, true, false, ct));
        }

        return target.Regions.Select(r =>
            {
                var usual = (decimal)AnomalyMath.Median(baseline.Select(b => (double)(b.Regions.FirstOrDefault(x => x.Id == r.Id)?.Figures.Revenue ?? 0)).ToArray());
                var delta = r.Figures.Revenue - usual;
                return (Delta: delta, Text: $"{r.Name} {(delta < 0 ? "−" : "+")}{Math.Abs(delta):N0} сум ({(usual == 0 ? "—" : $"{delta / usual * 100:+0;−0}%")})");
            })
            .Where(x => x.Delta != 0)
            .OrderBy(x => x.Delta)
            .ToList();
    }

    internal async Task<IReadOnlyList<InsightCandidate>> PaymentsAnomalyAsync(DateOnly today, CancellationToken ct)
    {
        var day = today.AddDays(-1);
        var from = day.AddDays(-63);
        // Оплаты за вчера ещё не загружены (обмен с Linko после конца дня не прошёл) — нули здесь значат «нет данных», а не «не платили».
        // Устаревший обмен показывает правило качества данных quality.linko_sync.
        var sync = await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "payments", ct);
        if (sync?.LastSuccessAt is not { } synced || synced < FieldClock.DayStartUtc(today))
        {
            return [];
        }

        var rows = await db.LinkoPayments.AsNoTracking()
            .Where(p => !p.IsDelete && p.Status == "accepted" && p.CreatedDate >= from && p.CreatedDate <= day)
            .GroupBy(p => p.CreatedDate!.Value).Select(g => new { Day = g.Key, Sum = g.Sum(p => p.Amount) }).ToListAsync(ct);
        if (rows.Count < 20)
        {
            return [];
        }

        var series = Enumerable.Range(0, 64).Select(i => from.AddDays(i)).Select(d => new SeriesPoint(d, rows.FirstOrDefault(r => r.Day == d)?.Sum ?? 0)).ToList();
        if (AnomalyMath.Latest(series, "day") is not { } hit || hit.Direction != "down" || Math.Abs(hit.Z) < AnomalyMath.Threshold)
        {
            return [];
        }

        return
        [
            new InsightCandidate(InsightKinds.Anomaly, "finance", "finance.payments_anomaly", $"finance.payments_anomaly:{day:yyyy-MM-dd}", Severities.Warning,
                $"Оплаты точек {day:dd.MM} ниже обычного на {Math.Abs(hit.ChangePercent ?? 0):0.#}%",
                $"Принято оплат на {hit.Value:N0} сум при обычных {hit.Expected:N0} сум для этого дня недели.",
                Hypotheses: ["Оплаты ещё не приняты в Linko", "Сдвиг инкассации"], Impact: hit.Value - hit.Expected, ImpactUnit: "сум",
                Recommendations: ["Сверить оплаты дня с кассой и банком"], Evidence: new { Day = day, hit.Value, hit.Expected, hit.Z },
                RequiredPermission: Permissions.FinanceRead),
        ];
    }

    // ---- снабжение и склад ----

    internal async Task<IReadOnlyList<InsightCandidate>> SupplyAsync(CancellationToken ct)
    {
        var plan = await supply.PlanAsync(SystemUser, ct);
        const string permission = "procurement.read|procurement.manage|procurement.warehouse|production.read|production.manage";
        var found = new List<InsightCandidate>();
        foreach (var r in plan.Items.Where(r => r.Risk is "critical" or "high").Take(40))
        {
            var need = r.Horizon(30)!;
            var recommendation = $"Заказать {need.Required:N2} {r.Unit} до {(r.OrderBy ?? plan.AsOf):dd.MM.yyyy}" +
                                 (r.Supplier is { } s ? $" (последний поставщик — {s})" : "") + (need.Cost is { } c ? $", ориентировочно {c:N0} сум" : ", цены нет");
            found.Add(new InsightCandidate(InsightKinds.Risk, "procurement", "supply.stockout", $"supply.stockout:{r.ItemId}",
                r.Risk == "critical" ? Severities.Critical : Severities.Warning,
                r.ShortageDate is { } d ? $"{r.Name}: запас закончится {d:dd.MM}" : $"{r.Name}: запаса нет",
                string.Join(" ", r.Reasons),
                Cause: r.ShortageDate is null ? null : $"Доступно {r.Available:N2} {r.Unit} при расходе {r.AvgDailyConsumption:N2} {r.Unit}/день" +
                                                       (r.CommittedTotal > 0 ? $" и заданиях на {r.CommittedTotal:N2} {r.Unit}" : ""),
                CauseConfirmed: r.ShortageDate is not null,
                Impact: need.Required, ImpactUnit: r.Unit,
                Consequences: r.Risk == "critical"
                    ? $"Поставка не успеет до дефицита (срок {r.LeadTimeDays} дн.) — производство с этим сырьём может остановиться."
                    : "Без заказа в ближайшие дни возникнет дефицит.",
                Recommendations: [recommendation, .. r.Assumptions.Take(2)],
                Evidence: new { r.Sku, r.OnHand, r.Reserved, r.Available, r.AvgDailyConsumption, r.ShortageDate, r.OrderBy, r.LeadTimeDays, r.LeadTimeAssumed, Need30 = need },
                Href: "/analytik/procurement", RequiredPermission: permission));
        }

        var dead = plan.Warehouse.Where(w => w.Status == "dead").ToList();
        if (dead.Count > 0)
        {
            var value = dead.Sum(w => w.Value ?? 0);
            found.Add(new InsightCandidate(InsightKinds.Risk, "procurement", "supply.dead_stock", "supply.dead_stock", Severities.Info,
                $"Сырьё без расхода 90 дней: {dead.Count} позиций",
                $"{dead.Count} позиций лежат на складе без выдачи 90 дней" + (value > 0 ? $" на {value:N0} сум (по известным ценам)." : "."),
                Hypotheses: ["Позиции больше не используются в техкартах", "Расход не проводится через склад"],
                Recommendations: ["Проверить, нужны ли позиции; неиспользуемые — вернуть поставщику, продать или списать по решению"],
                Impact: dead.Count, ImpactUnit: "позиций", Evidence: new { Items = dead.Take(15).Select(w => new { w.Sku, w.Name, w.OnHand, w.Unit, w.Value }) },
                Href: "/analytik/warehouse", RequiredPermission: permission));
        }

        var overdue = plan.Items.Where(r => r.Reasons.Any(x => x.StartsWith("Есть просроченные поставки", StringComparison.Ordinal))).ToList();
        if (overdue.Count > 0)
        {
            found.Add(new InsightCandidate(InsightKinds.Risk, "procurement", "supply.overdue_delivery", "supply.overdue_delivery", Severities.Warning,
                $"Просроченные поставки сырья: {overdue.Count} позиций", "Ожидаемая дата поставки прошла, а сырьё не принято на склад.",
                Recommendations: ["Связаться с поставщиками и уточнить новую дату", "При риске дефицита — заказать у другого поставщика"],
                Evidence: new { Items = overdue.Take(15).Select(r => r.Name) }, Href: "/procurement", RequiredPermission: permission));
        }

        return found;
    }

    // ---- производство ----

    internal async Task<IReadOnlyList<InsightCandidate>> ProductionAsync(DateOnly today, CancellationToken ct)
    {
        const string permission = "production.read|production.manage|procurement.read|procurement.manage";
        var overview = await production.OverviewAsync(SystemProcurement, new ProcurementFilter(today.AddDays(-30), today), null, ct);
        var found = new List<InsightCandidate>();
        if (overview.OverdueOrders > 0)
        {
            found.Add(new InsightCandidate(InsightKinds.Risk, "production", "production.overdue_orders", "production.overdue_orders",
                overview.OverdueOrders >= 3 ? Severities.Critical : Severities.Warning,
                $"Просрочены производственные задания: {overview.OverdueOrders}", "Плановая дата заданий прошла, а они не закрыты.",
                Hypotheses: ["Не хватило сырья", "Загрузка линий", "Задание выполнено, но не закрыто в OneBase"],
                Recommendations: ["Разобрать просроченные задания в «Производство → Задания»"], Impact: overview.OverdueOrders, ImpactUnit: "заданий",
                Href: "/production/orders", RequiredPermission: permission));
        }

        var over = overview.Deviations.Where(d => d.Overconsumed).ToList();
        if (over.Count > 0)
        {
            found.Add(new InsightCandidate(InsightKinds.Anomaly, "production", "production.overconsumption", "production.overconsumption", Severities.Warning,
                $"Перерасход сырья против нормы: {over.Count} строк заданий",
                "Выдано больше нормы техкарты: " + string.Join("; ", over.Take(5).Select(d => $"{d.Item} в {d.OrderNumber}: +{d.Deviation:N2} {d.UnitCode}" +
                                                                                               (d.Percent is { } p ? $" ({p:+0.#}%)" : ""))) + ".",
                Hypotheses: ["Потери выше нормы", "Норма техкарты занижена", "Выдача под несколько заданий"],
                Recommendations: ["Проверить нормы и фактические потери с технологом"], Impact: over.Sum(d => d.Deviation), ImpactUnit: "ед. сырья",
                Evidence: new { Lines = over.Take(10) }, Href: "/production/reports", RequiredPermission: permission));
        }

        foreach (var p in overview.Prices.Where(p => p.ChangePercent >= 15).Take(10))
        {
            found.Add(new InsightCandidate(InsightKinds.Anomaly, "procurement", "production.price_growth", $"production.price_growth:{p.Name}", Severities.Warning,
                $"{p.Name} подорожало на {p.ChangePercent:0.#}%", $"Цена прихода выросла с {p.FirstPrice:N0} до {p.LastPrice:N0} сум за {p.UnitCode} за 30 дней.",
                Hypotheses: ["Сменился поставщик", "Срочная закупка без сравнения предложений", "Рост рыночной цены"],
                Consequences: "Себестоимость продукции с этим сырьём растёт.", Recommendations: ["Сравнить предложения поставщиков (Supply Analyst)"],
                Impact: p.ChangePercent, ImpactUnit: "%", Evidence: p, Href: "/production", RequiredPermission: "procurement.read|procurement.manage"));
        }

        if (overview.Expired.Count > 0)
        {
            found.Add(new InsightCandidate(InsightKinds.Risk, "production", "production.expired", "production.expired", Severities.Critical,
                $"На складе просроченное сырьё: {overview.Expired.Count} партий", $"Партий с истёкшим сроком годности: {overview.Expired.Count} на {overview.Expired.Value:N0} сум.",
                Recommendations: ["Заблокировать и списать или вернуть просроченные партии"], Impact: overview.Expired.Value, ImpactUnit: "сум",
                Href: "/production/quality", RequiredPermission: permission));
        }

        if (overview.Expiring7.Count > 0)
        {
            found.Add(new InsightCandidate(InsightKinds.Risk, "production", "production.expiring", "production.expiring", Severities.Warning,
                $"Срок годности истекает в течение 7 дней: {overview.Expiring7.Count} партий", $"На {overview.Expiring7.Value:N0} сум.",
                Recommendations: ["Использовать эти партии в первую очередь (FEFO)"], Impact: overview.Expiring7.Value, ImpactUnit: "сум",
                Href: "/production/quality", RequiredPermission: permission));
        }

        var since = today.AddDays(-7);
        var shifts = await db.ProductionShifts.AsNoTracking().Where(s => s.ShiftDate >= since && s.Status == ProductionShiftStatus.Approved)
            .Select(s => new { s.GoodOutput, Defects = s.Defects.Sum(d => (decimal?)d.QuantityKg) ?? 0 }).ToListAsync(ct);
        var good = shifts.Sum(s => s.GoodOutput ?? 0);
        var defects = shifts.Sum(s => s.Defects);
        if (good + defects > 0 && defects / (good + defects) > 0.05m)
        {
            var rate = Math.Round(defects / (good + defects) * 100, 1);
            found.Add(new InsightCandidate(InsightKinds.Anomaly, "production", "production.defects", "production.defects", Severities.Warning,
                $"Брак за 7 дней {rate}%", $"Брак {defects:N1} кг при выпуске годного {good:N1} кг по утверждённым сменам.",
                Hypotheses: ["Нарушение режима этапа", "Качество сырья партии"], Recommendations: ["Разобрать брак по этапам в «Производство → Смены»"],
                Impact: rate, ImpactUnit: "%", Href: "/production/shifts", RequiredPermission: permission));
        }

        return found;
    }

    // ---- персонал ----

    internal async Task<IReadOnlyList<InsightCandidate>> HrAsync(DateOnly today, CancellationToken ct)
    {
        var ctx = new HrContext(Guid.Empty, true, false, null, []);
        var current = await hr.DeviationsAsync(ctx, today.AddDays(-7), today.AddDays(-1), null, null, null, ct);
        var previous = await hr.DeviationsAsync(ctx, today.AddDays(-14), today.AddDays(-8), null, null, null, ct);
        var found = new List<InsightCandidate>();
        foreach (var (kind, rule, title) in new[] { ("late", "hr.lateness_growth", "Опозданий"), ("absent", "hr.absence_growth", "Отсутствий") })
        {
            var now = current.Count(d => d.Kind == kind);
            var before = previous.Count(d => d.Kind == kind);
            if (now >= 10 && before > 0 && now >= before * 1.3m)
            {
                var top = current.Where(d => d.Kind == kind).GroupBy(d => d.Division ?? "—").OrderByDescending(g => g.Count()).Take(3)
                    .Select(g => $"{g.Key} — {g.Count()}");
                found.Add(new InsightCandidate(InsightKinds.Anomaly, "hr", rule, rule, Severities.Warning,
                    $"{title} за неделю больше на {(now - before) * 100m / before:0}%", $"{title} за 7 дней: {now} против {before} неделей раньше. Больше всего: {string.Join("; ", top)}.",
                    Hypotheses: ["Изменился график", "Сбой терминалов Verifix", "Сезонные причины"],
                    Recommendations: ["Разобрать отклонения по подразделениям в «HR → Отклонения»"], Impact: now - before, ImpactUnit: "случаев",
                    Href: "/hr", RequiredPermission: Permissions.HrRead));
            }
        }

        return found;
    }

    /// <summary>
    /// Находки для экрана: находки Аналитика и активные находки проактивного анализа AI (ai.Alerts) — по правам пользователя.
    /// </summary>
    public async Task<IReadOnlyList<InsightView>> ListAsync(AnalytikPrincipal principal, string? module, bool resolved, int take, CancellationToken ct)
    {
        var own = await store.ListAsync(principal, module, resolved, take, ct);
        if (resolved || (module is not null and not "sales"))
        {
            return own;
        }

        var alerts = await db.AiAlerts.AsNoTracking().Where(a => a.ResolvedAt == null).OrderByDescending(a => a.LastSeenAt).Take(200).ToListAsync(ct);
        var proactive = alerts
            .Where(a => InsightStore.Visible(a.RequiredPermission, "sales", principal) || OwnRegionAlert(a, principal))
            .Select(a => new InsightView(a.Id, a.Severity == AiAlertSeverity.Opportunity ? InsightKinds.Opportunity : InsightKinds.Risk, "sales", a.Rule,
                a.Severity switch { AiAlertSeverity.Critical => Severities.Critical, AiAlertSeverity.Warning => Severities.Warning, _ => Severities.Info },
                a.Title, a.Message, null, false, [], null, null, null, a.Recommendation is { } r ? [r] : [], null, a.Href, a.DetectedAt, a.LastSeenAt, a.ResolvedAt, 1,
                "proactive"));
        return own.Concat(proactive).OrderByDescending(i => Severities.Rank(i.Severity)).ThenByDescending(i => i.DetectedAt).Take(Math.Clamp(take, 1, 500)).ToList();
    }

    /// <summary>
    /// Находка проактивного анализа по одному региону ("правило:регион:месяц") видна РМ и супервайзеру этого региона: в ней только данные
    /// их региона. Находки по всей организации им не показываются (в них цифры других регионов).
    /// </summary>
    internal static bool OwnRegionAlert(AiAlert alert, AnalytikPrincipal principal)
    {
        if (principal.Sales.Level != SalesRowScope.RegionLevel
            || (alert.RequiredPermission is { } permission && !principal.HasAny(permission.Split('|', StringSplitOptions.RemoveEmptyEntries))))
        {
            return false;
        }

        var parts = alert.Key.Split(':');
        return parts.Length == 3 && Guid.TryParse(parts[1], out var region) && principal.Sales.RegionIds.Contains(region);
    }
}

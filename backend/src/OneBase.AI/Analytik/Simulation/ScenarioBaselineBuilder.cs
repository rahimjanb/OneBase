using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Readiness;
using OneBase.Application.Analytik.Simulation;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Sales.SkuSales;
using OneBase.Application.Security;
using OneBase.Domain.Analytik;
using OneBase.Domain.Procurement;

namespace OneBase.AI.Analytik.Simulation;

/// <summary>Снимок исходных данных, сохранённый в analytik.ScenarioBaselines: id и данные.</summary>
public sealed record StoredBaseline(Guid Id, ScenarioBaselineData Data);

/// <summary>
/// Снимок исходных данных сценария по реальной базе OneBase — только разделы, которые видит пользователь, и только его область продаж:
/// продажи (вторичка по SKU × регион, скидки, план месяца, агенты), техкарты (нормы на кг), производство (смены), сырьё (входы плана закупок),
/// финансы (оплаты точек, срок оплаты, расходы на закупки), персонал (Verifix), свежесть источников. Снимок сохраняется сразу (незакреплённым)
/// и кэшируется на 10 минут; строить снимки одновременно — не больше двух (тяжёлые запросы продаж). Ничего не меняет в бизнес-данных.
/// </summary>
public sealed class ScenarioBaselineBuilder(
    IAppDbContext db,
    IAnalytikDb adb,
    SkuSalesService sku,
    SalesAnalytik sales,
    SalesOptions salesOptions,
    SupplyPlanner supply,
    SimulationSources sources,
    HrService hr,
    ProcurementRequestService requests,
    ProcurementInsightService insight,
    PnlService pnl,
    DataReadiness readiness,
    AnalytikOptions options,
    IMemoryCache cache,
    TimeProvider clock)
{
    public const int MaxBaseDays = 366;
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim Gate = new(2, 2);

    public const string OtherRegion = "other";

    /// <summary>Меньше стольких дней общих с базой оплат — доля оплат и срок не считаются (слишком мало для оценки).</summary>
    public const int MinPaymentDays = 14;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    /// <summary>Ключ области автора снимка: разделы и область продаж — по нему проверяется, покрывают ли права другого пользователя снимок.</summary>
    public static string ScopeKey(AnalytikPrincipal p, IEnumerable<string> sections) =>
        $"{string.Join(",", sections.OrderBy(s => s, StringComparer.Ordinal))}|{p.Sales.Level}|" +
        $"{string.Join(",", p.Sales.RegionIds.Select(r => r.ToString()).OrderBy(r => r, StringComparer.Ordinal))}|{p.Sales.AgentId}";

    /// <summary>Разделы, которые войдут в снимок пользователя.</summary>
    public static IReadOnlyList<string> SectionsOf(AnalytikPrincipal p, bool hrAccess)
    {
        var list = new List<string>();
        if (SalesAnalytik.CanView(p))
        {
            list.Add("sales");
        }

        if (p.HasAny(OperationsAnalytik.ProductionPermissions))
        {
            list.Add("production");
        }

        if (SupplyPlanner.CanView(p))
        {
            list.Add("materials");
        }

        if (FinanceAnalytik.CanView(p))
        {
            list.Add("finance");
        }

        if (hrAccess)
        {
            list.Add("workforce");
        }

        return list;
    }

    /// <summary>Базовый период по умолчанию: последние 3 полных месяца до даты данных.</summary>
    public static (DateOnly From, DateOnly To) DefaultPeriod(DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        return (monthStart.AddMonths(-3), monthStart.AddDays(-1));
    }

    /// <summary>Снимок за базовый период; fresh — заново, без кэша (пересчёт на актуальных данных).</summary>
    public async Task<StoredBaseline> BuildAsync(AnalytikPrincipal p, DateOnly from, DateOnly to, CancellationToken ct, bool fresh = false)
    {
        var today = FieldClock.LocalDate(clock.GetUtcNow());
        if (to < from)
        {
            throw new ArgumentException("Дата «по» раньше даты «с».");
        }

        if (to > today)
        {
            throw new ArgumentException("Базовый период — только прошедшие дни (факт): дата «по» не позже сегодняшней.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxBaseDays)
        {
            throw new ArgumentException($"Базовый период — не больше {MaxBaseDays} дней.");
        }

        var hrContext = p.UserId is { } uid && !p.System ? await hr.ContextAsync(uid, p.Permissions, ct) : new HrContext(Guid.Empty, true, false, null, []);
        var sections = SectionsOf(p, hrContext.HasAccess);
        if (sections.Count == 0)
        {
            throw new UnauthorizedAccessException("Для симулятора нужен доступ хотя бы к одному разделу: продажи, производство, склад, финансы или персонал.");
        }

        var scopeKey = ScopeKey(p, sections);
        var key = $"analytik:sim:baseline:{options.Tenant}:{p.UserId}:{scopeKey}:{from:yyyyMMdd}:{to:yyyyMMdd}:{today:yyyyMMdd}";
        if (!fresh && cache.TryGetValue(key, out StoredBaseline? cached) && cached is not null)
        {
            return cached;
        }

        await Gate.WaitAsync(ct);
        try
        {
            if (!fresh && cache.TryGetValue(key, out cached) && cached is not null)
            {
                return cached;
            }

            var data = await CollectAsync(p, hrContext, sections, from, to, today, ct);
            var json = SimulationJson.Write(data);
            var row = new AnalytikScenarioBaseline
            {
                Tenant = options.Tenant, CreatedById = p.UserId, PeriodFrom = from, PeriodTo = to, AsOf = data.AsOf, FormulaVersion = data.FormulaVersion,
                Sections = string.Join(",", sections), ScopeKey = scopeKey, SalesScope = data.SalesScope, Data = json,
                Sources = SimulationJson.Write(data.Sources), Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(),
            };
            adb.ScenarioBaselines.Add(row);
            await adb.SaveChangesAsync(ct);
            var stored = new StoredBaseline(row.Id, data);
            cache.Set(key, stored, CacheFor);
            return stored;
        }
        finally
        {
            Gate.Release();
        }
    }

    internal async Task<ScenarioBaselineData> CollectAsync(AnalytikPrincipal p, HrContext hrContext, IReadOnlyList<string> sections, DateOnly from, DateOnly to,
        DateOnly today, CancellationToken ct)
    {
        var notes = new List<string>();
        BaselineSales? salesData = null;
        if (sections.Contains("sales"))
        {
            salesData = await SalesAsync(p, from, to, notes, ct);
        }

        BaselineProduction? production = null;
        if (sections.Contains("production"))
        {
            // Сегодняшний день ещё идёт — смены берутся по вчера.
            production = await sources.ProductionAsync(today.AddDays(-1), ct);
            notes.AddRange(production.Notes);
        }

        IReadOnlyList<BaselineMaterial>? materials = null;
        if (sections.Contains("materials"))
        {
            materials = await MaterialsAsync(p, salesData, notes, ct);
        }

        BaselineFinance? finance = null;
        if (sections.Contains("finance"))
        {
            finance = await FinanceAsync(p, from, to, salesData, ct);
            notes.AddRange(finance.Notes);
        }

        BaselineWorkforce? workforce = null;
        if (sections.Contains("workforce"))
        {
            var overview = await hr.OverviewAsync(hrContext, today, ct);
            var divisions = (await hr.DivisionsAsync(hrContext, ct)).Where(d => d.Employees > 0 && !d.Missing)
                .OrderByDescending(d => d.Employees).ThenBy(d => d.Name)
                .Select(d => new BaselineDivision(d.Id.ToString(), d.Name, d.Employees)).ToList();
            workforce = new BaselineWorkforce(overview.Active, hrContext.SeesAll, divisions);
        }

        var report = await readiness.CheckAsync(ct);
        var sourceRows = report.Visible(p)
            .Select(d => new BaselineSource(d.Code, d.Title, d.Status, d.UpdatedAt, d.Reason)).ToList();
        return new ScenarioBaselineData(ScenarioEngine.FormulaVersion, options.Tenant, from, to, today, clock.GetUtcNow().ToUniversalTime(), sections,
            p.Sales.Restricted ? p.Sales.Description : null, salesData, production, materials, finance, workforce, sourceRows, notes.Distinct().ToList());
    }

    private async Task<BaselineSales> SalesAsync(AnalytikPrincipal p, DateOnly from, DateOnly to, List<string> notes, CancellationToken ct)
    {
        var allRegions = await db.SalesRegions.AsNoTracking().ToListAsync(ct);
        var structure = SkuSalesService.RegionsOf(allRegions, salesOptions);
        var (agentsByBranch, through) = await sources.AgentsAsync(from, to, salesOptions.SoldStatuses, ct);

        if (p.Sales.Level == SalesRowScope.AgentLevel)
        {
            // Агент — только свои продажи итогом (по товарам и регионам область агента не разложить).
            var kg = (await sales.DailyAsync("kg", from, to, p, ct)).Sum(x => x.Value);
            var revenue = (await sales.DailyAsync("revenue", from, to, p, ct)).Sum(x => x.Value);
            notes.Add("Область — только ваши продажи: по товарам и регионам симулятор их не раскладывает, производство и сырьё от продаж не пересчитываются.");
            return new BaselineSales(kg, revenue, 0, through, p.Sales.Description, false, null, null,
                [new BaselineRegion("self", "Мои продажи", kg, revenue, null, 1)],
                [new BaselineProduct(0, "Все продажи", kg, revenue, 0, null, null, [], "итог продаж агента")],
                [new BaselineCell(0, "self", kg, revenue)]);
        }

        var aggregate = await sku.AggregateAsync(from, to, structure.BranchRegions, ct);
        var restricted = p.Sales.Level == SalesRowScope.RegionLevel;
        var regionIds = p.Sales.RegionIds.ToHashSet();
        var clamped = 0;
        var cells = aggregate.Months
            .Where(m => !restricted || (m.RegionId is { } r && regionIds.Contains(r)))
            .GroupBy(m => (m.ProductId, Region: m.RegionId?.ToString() ?? OtherRegion))
            .Select(g => (g.Key.ProductId, g.Key.Region, Kg: g.Sum(m => m.Kg), Revenue: g.Sum(m => m.Revenue)))
            .Where(c => c.Kg != 0 || c.Revenue != 0)
            .Select(c =>
            {
                if (c.Kg < 0 || c.Revenue < 0)
                {
                    clamped++;
                }

                return new BaselineCell(c.ProductId, c.Region, Math.Max(0, c.Kg), Math.Max(0, c.Revenue));
            })
            .Where(c => c.Kg > 0 || c.Revenue > 0)
            .OrderBy(c => c.ProductId).ThenBy(c => c.RegionId, StringComparer.Ordinal)
            .ToList();
        if (clamped > 0)
        {
            notes.Add($"Товаров с возвратами больше продаж за период (по регионам): {clamped} — в базе они приняты нулём.");
        }

        if (cells.Count == 0)
        {
            notes.Add($"Продаж за {AnalytikPeriod.LabelOf(from, to)} в OneBase нет{(through is null ? "" : $" (данные Linko по {through:dd.MM.yyyy})")}.");
        }

        var productIds = cells.Select(c => c.ProductId).Distinct().ToList();
        var names = await db.LinkoProducts.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var discounts = await sources.DiscountsAsync(from, to, restricted ? p.Sales.BranchIds : null, salesOptions.SoldStatuses, ct);
        var recipes = await sources.RecipeNormsAsync(productIds, ct);
        var products = cells.GroupBy(c => c.ProductId).Select(g =>
        {
            var d = discounts.GetValueOrDefault(g.Key);
            var share = d.Net + d.Discount > 0 ? Math.Round(d.Discount / (d.Net + d.Discount), 6) : 0;
            var recipe = recipes.GetValueOrDefault(g.Key);
            return new BaselineProduct(g.Key, names.GetValueOrDefault(g.Key) ?? $"Товар {g.Key}", g.Sum(c => c.Kg), g.Sum(c => c.Revenue), share, recipe?.RecipeId,
                recipe?.Code, recipe?.Norms ?? [], recipe is null ? "нет действующей техкарты, связанной с товаром" : recipe.Problem);
        }).OrderByDescending(x => x.Revenue).ThenBy(x => x.Id).ToList();
        foreach (var problem in recipes.Values.Select(r => r.Problem).OfType<string>().Distinct().Take(10))
        {
            notes.Add(problem);
        }

        // Регионы: названия, агенты с продажами (по филиалам региона), план месяца.
        var agentsByRegion = new Dictionary<string, int>();
        foreach (var (branch, count) in agentsByBranch)
        {
            var region = structure.BranchRegions.TryGetValue(branch, out var r) ? r.ToString() : OtherRegion;
            agentsByRegion[region] = agentsByRegion.GetValueOrDefault(region) + count;
        }

        var plan = await sales.PlanAsync(p, null, ct);
        var regionNames = structure.Regions.ToDictionary(r => r.Id.ToString(), r => r.Name);
        var regions = cells.GroupBy(c => c.RegionId).Select(g => new BaselineRegion(g.Key,
                regionNames.GetValueOrDefault(g.Key) ?? (g.Key == OtherRegion ? "Вне регионов" : g.Key), g.Sum(c => c.Kg), g.Sum(c => c.Revenue),
                plan?.Regions.FirstOrDefault(r => r.Id == g.Key)?.PlanKg, agentsByRegion.GetValueOrDefault(g.Key)))
            .OrderByDescending(r => r.Revenue).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        if (plan is null || plan.PlanKg is not > 0)
        {
            notes.Add("Планов продаж на месяц в OneBase нет — выполнение плана не считается.");
        }

        return new BaselineSales(cells.Sum(c => c.Kg), cells.Sum(c => c.Revenue),
            products.Sum(x => x.Revenue) > 0 ? Math.Round(products.Sum(x => x.Revenue * x.DiscountShare) / products.Sum(x => x.Revenue), 6) : 0,
            through, restricted ? p.Sales.Description : null, true, plan?.PlanKg is > 0 ? plan.PlanKg : null,
            plan is null ? null : $"план {plan.Month:00}.{plan.Year} ({plan.PlanSource})", regions, products, cells);
    }

    private async Task<IReadOnlyList<BaselineMaterial>> MaterialsAsync(AnalytikPrincipal p, BaselineSales? salesData, List<string> notes, CancellationToken ct)
    {
        var inputs = await supply.InputsAsync(p, ct);
        notes.AddRange(inputs.Notes);
        var recipeItems = (salesData?.Products ?? []).SelectMany(x => x.Norms).Select(n => n.ItemId).ToHashSet();
        var selected = inputs.Items.Where(i => recipeItems.Contains(i.ItemId) || i.DailyConsumption.Any(d => d.Value > 0) || i.Committed.Count > 0 || i.Incoming.Count > 0)
            .ToList();
        var ids = selected.Select(i => i.ItemId).ToList();

        // Потери строк техкарт по позиции (средневзвешенно) — для параметра «технологические потери».
        var lines = await db.ProductionRecipeLines.AsNoTracking()
            .Where(l => ids.Contains(l.ItemId) && l.Recipe!.Status == Domain.Production.ProductionRecipeStatus.Active)
            .Select(l => new { l.ItemId, l.Quantity, l.LossPercent })
            .ToListAsync(ct);
        var loss = lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key,
            g => g.Sum(l => l.Quantity) > 0 ? g.Sum(l => l.Quantity * l.LossPercent) / g.Sum(l => l.Quantity) : 0);

        // Другие поставщики позиции — по истории приходов: последняя цена, дата, срок (карточка или факт заказов).
        var receipts = await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => ids.Contains(m.ItemId) && m.Kind == ProcurementMovementKind.Receipt && m.UnitCost != null && m.UnitCost > 0 && m.Document!.SupplierId != null)
            .Select(m => new { m.ItemId, m.CreatedAt, m.UnitCost, SupplierId = m.Document!.SupplierId!.Value })
            .ToListAsync(ct);
        var supplierIds = receipts.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await db.ProcurementSuppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.LeadTimeDays }, ct);
        var observed = (await db.ProcurementPurchaseOrders.AsNoTracking()
                .Where(o => supplierIds.Contains(o.SupplierId) && o.OrderedAt != null && o.DeliveredAt != null)
                .Select(o => new { o.SupplierId, o.OrderedAt, o.DeliveredAt })
                .ToListAsync(ct))
            .GroupBy(o => o.SupplierId)
            .ToDictionary(g => g.Key, g => (int)Math.Ceiling(g.Average(o => (o.DeliveredAt!.Value - o.OrderedAt!.Value).TotalDays)));
        var alternatives = receipts.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.GroupBy(r => r.SupplierId)
            .Select(s =>
            {
                var last = s.OrderByDescending(r => r.CreatedAt).First();
                var info = suppliers.GetValueOrDefault(s.Key);
                return new BaselineSupplier(s.Key.ToString("N"), info?.Name ?? "Поставщик", last.UnitCost,
                    info?.LeadTimeDays ?? (observed.TryGetValue(s.Key, out var o) ? o : null), FieldClock.LocalDate(last.CreatedAt));
            })
            .OrderBy(s => s.Price ?? decimal.MaxValue).ThenBy(s => s.Name).ToList());

        return selected.OrderBy(i => i.ItemId).Select(i =>
        {
            var values = i.DailyConsumption.Select(d => (double)d.Value).ToList();
            var rate = values.Count > 0 ? (decimal)values.Average() : 0;
            return new BaselineMaterial(i.ItemId, i.Sku, i.Name, i.Unit, i.OnHand, i.Reserved, i.Unusable, Math.Max(0, i.OnHand - i.Reserved - i.Unusable), i.MinQty,
                Math.Round(rate, 6), (decimal)Math.Round(SupplyPlanMath.StdDev(values), 6), values.Count, i.Committed.Sum(c => c.Quantity),
                i.Incoming.Select(x => new BaselineIncoming(x.Quantity, x.Expected, x.Source)).ToList(),
                i.LeadTimeDays ?? options.DefaultLeadTimeDays, i.LeadTimeDays is null,
                i.LeadTimeSource ?? $"не задан — принят {options.DefaultLeadTimeDays} дн. (настройка Аналитика)", i.UnitPrice, i.PriceSource, i.Supplier,
                i.PurchaseFactor, i.PurchaseUnit, Math.Round(loss.GetValueOrDefault(i.ItemId), 4), ProductionCalc.MassFactor(i.Unit),
                alternatives.GetValueOrDefault(i.ItemId) ?? []);
        }).ToList();
    }

    private async Task<BaselineFinance> FinanceAsync(AnalytikPrincipal p, DateOnly from, DateOnly to, BaselineSales? salesData, CancellationToken ct)
    {
        var notes = new List<string>();
        decimal? payments = null, collection = null, lag = null;
        if (p.Sales.Restricted)
        {
            notes.Add("Оплаты точек видны только при доступе ко всей организации — денежный поток не считается.");
        }
        else
        {
            var facts = await sources.PaymentsAsync(from, to, ct);
            var loaded = facts.First is { } f && facts.Last is { } l && f <= from && l >= to.AddDays(-1);
            if (loaded)
            {
                payments = facts.Total;
                lag = facts.LagDays ?? 0;
                collection = salesData is { Revenue: > 0 } s ? Math.Round(facts.Total / s.Revenue, 6) : null;
            }
            else if (facts.First is { } first && facts.Last is { } last && Max(first, from) is var overlapFrom && Min(last, to) is var overlapTo
                     && overlapTo.DayNumber - overlapFrom.DayNumber + 1 >= MinPaymentDays)
            {
                // Оплаты загружены не за весь базовый период — доля оплат и срок считаются по общему с продажами отрезку (та же вторичка).
                var overlap = await sources.PaymentsAsync(overlapFrom, overlapTo, ct);
                var structure = SkuSalesService.RegionsOf(await db.SalesRegions.AsNoTracking().ToListAsync(ct), salesOptions);
                var revenue = (await sku.AggregateAsync(overlapFrom, overlapTo, structure.BranchRegions, ct)).Months.Sum(m => m.Revenue);
                payments = facts.Total;
                lag = overlap.LagDays ?? 0;
                collection = revenue > 0 ? Math.Round(overlap.Total / revenue, 6) : null;
                notes.Add($"Оплаты точек загружены с {first:dd.MM.yyyy} по {last:dd.MM.yyyy}: доля оплат к выручке и срок оплаты посчитаны за " +
                          $"{overlapFrom:dd.MM.yyyy} — {overlapTo:dd.MM.yyyy}.");
            }
            else
            {
                notes.Add(facts.First is null
                    ? "Оплат точек в OneBase нет — денежный поток не считается."
                    : $"Оплаты точек загружены с {facts.First:dd.MM.yyyy} по {facts.Last:dd.MM.yyyy} — с базовым периодом пересекаются меньше {MinPaymentDays} дней, денежный поток не считается.");
            }

            if (collection is not null && lag is 0 && facts.LagDays is null)
            {
                notes.Add("У оплат нет связи с заказами — срок оплаты принят 0 дней.");
            }
        }

        decimal? spend = null;
        if (p.HasAny([Permissions.ProcurementRead, Permissions.ProcurementManage]))
        {
            var ctx = p.System || p.UserId is not { } userId
                ? new ProcurementContext(Guid.Empty, new HashSet<string> { Permissions.ProcurementRead }, null, new HashSet<Guid>(), IsDirector: true)
                : await requests.ContextAsync(userId, p.Permissions, ct);
            spend = (await insight.ExpensesAsync(ctx, new ProcurementFilter(from, to), ct)).Total;
        }

        var months = (await pnl.MonthsAsync(ct)).Count;
        return new BaselineFinance(payments, collection, lag, spend, months, notes);
    }
}

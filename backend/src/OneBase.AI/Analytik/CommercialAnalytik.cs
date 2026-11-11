using Microsoft.EntityFrameworkCore;
using OneBase.AI.Tools.Data;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Forecasting;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Security;

namespace OneBase.AI.Analytik;

public sealed record SalesDay(DateOnly Date, decimal Revenue, decimal Kg, int Orders, int Akb);

public sealed record SalesRegionRow(string Id, string Name, decimal Revenue, decimal Kg, int Akb, int Orders, decimal? PreviousRevenue, decimal? ChangePercent,
    decimal? PlanKg, decimal? Execution, decimal? ForecastExecution);

/// <summary>План месяца: факт, план, выполнение, прогноз на конец месяца — как на странице «Продажи» (те же расчёты).</summary>
/// <summary>
/// План месяца как на странице «Продажи»: FactKg — весь факт, PlanFactKg — факт, сравнимый с планом (по тем, у кого есть план), выполнение и
/// прогноз выполнения — по нему.
/// </summary>
public sealed record SalesMonthPlan(int Year, int Month, DateOnly DataThrough, decimal FactKg, decimal? PlanKg, decimal? Execution, decimal? ForecastKg,
    decimal? ForecastExecution, decimal Revenue, string PlanSource, IReadOnlyList<SalesRegionRow> Regions, decimal? PlanFactKg = null);

public sealed record SalesBlock(
    AnalytikPeriod Period, DateOnly? DataThrough, bool Complete, IReadOnlyList<Kpi> Kpis, IReadOnlyList<SalesDay> Daily, IReadOnlyList<SalesRegionRow> Regions,
    SalesMonthPlan? Plan, string? Scope, IReadOnlyList<string> Notes, IReadOnlyList<string> Sources) : IAnalytikBlock;

/// <summary>
/// Продажи для «Аналитика» — теми же расчётами, что страницы «Продаж»: период — SalesPeriodEngine по месяцам SalesAnalytics (статусы, дата
/// приёмки, исключённые филиалы, возвраты), план месяца — витрина республики (план РОП или сумма планов ТП, прогноз = факт ÷ прошедшие дни ×
/// дни месяца). Область продаж: организация — всё, регион — только свои регионы (строки регионов и их сумма), агент — только свои продажи.
/// </summary>
public sealed class SalesAnalytik(SalesDataLoader loader, TimeProvider clock)
{
    public static bool CanView(AnalytikPrincipal p) => p.Has(Permissions.SalesRead);

    private async Task<HashSet<(int Year, int Month)>> MonthsAsync(CancellationToken ct) =>
        (await loader.MonthsAsync(ct)).Select(m => (m.Year, m.Month)).ToHashSet();

    private Func<int, int, CancellationToken, Task<ISalesMonth?>> Month(HashSet<(int, int)> available) =>
        async (year, month, ct) => available.Contains((year, month)) ? new AnalyticsMonth(await loader.LoadAsync(year, month, ct)) : null;

    /// <summary>Продажи периода области пользователя: итог (сумма регионов области), дни и регионы.</summary>
    private async Task<PeriodSales?> AggregateAsync(DateOnly from, DateOnly to, AnalytikPrincipal p, HashSet<(int, int)> available, CancellationToken ct)
    {
        var scope = p.Sales.Level == SalesRowScope.AgentLevel ? new SalesScope(Agent: p.Sales.AgentId ?? -1) : new SalesScope();
        return await SalesPeriodEngine.AggregateAsync(new DateRange(from, to), scope, Month(available), regions: true, agents: false, ct);
    }

    private static bool Visible(AnalytikPrincipal p, string regionId) =>
        !p.Sales.Restricted || (p.Sales.Level == SalesRowScope.RegionLevel && Guid.TryParse(regionId, out var id) && p.Sales.RegionIds.Contains(id))
        || p.Sales.Level == SalesRowScope.AgentLevel;

    public async Task<SalesBlock?> BlockAsync(AnalytikPeriod period, AnalytikPrincipal p, CancellationToken ct)
    {
        if (!CanView(p))
        {
            return null;
        }

        var available = await MonthsAsync(ct);
        var notes = new List<string>();
        if (available.Count == 0)
        {
            return new SalesBlock(period, null, false, [], [], [], null, Scope(p), ["Данных о продажах в OneBase ещё нет: Linko не загружался."], []);
        }

        var main = await AggregateAsync(period.From, period.To, p, available, ct);
        var previous = await AggregateAsync(period.PreviousFrom, period.PreviousTo, p, available, ct);
        var rows = main!.Regions.Where(r => Visible(p, r.Id)).ToList();
        var previousRows = previous!.Regions.Where(r => Visible(p, r.Id)).ToDictionary(r => r.Id);
        var regionLevel = p.Sales.Level == SalesRowScope.RegionLevel;
        var total = regionLevel ? rows.Aggregate(SalesFigures.Zero, (s, r) => s.Add(r.Figures)) : main.Total;
        var totalPrev = regionLevel ? previousRows.Values.Aggregate(SalesFigures.Zero, (s, r) => s.Add(r.Figures)) : previous.Total;
        var akb = regionLevel ? rows.Sum(r => r.Akb) : main.Akb;
        var akbPrev = regionLevel ? previousRows.Values.Sum(r => r.Akb) : previous.Akb;
        // Весь период без данных — показатели «нет данных», а не нули (ноль — это «продаж не было»).
        var covered = main.Missing.Sum(m => m.Days) < period.Days;
        if (main.Missing.Count > 0)
        {
            notes.Add(covered
                ? $"Нет данных за часть периода: {string.Join(", ", main.Missing.Select(m => m.Label))}."
                : $"За период {period.Label} данных о продажах в OneBase нет (Linko загружен по {(await LastDataAsync(available, ct))?.ToString("dd.MM.yyyy") ?? "—"}).");
        }

        if (!main.Complete && main.DataThrough is { } through)
        {
            notes.Add($"Данные по {through:dd.MM.yyyy} (последняя синхронизация Linko).");
        }

        if (regionLevel)
        {
            notes.Add($"Показаны только ваши регионы ({p.Sales.Description}); по дням — недоступно для части области, итоги — сумма регионов.");
        }

        const string src = "OneBase → Продажи (Linko: заказы и возвраты)";
        var avgCheck = total.Orders > 0 ? Math.Round(total.Revenue / total.Orders, 0) : (decimal?)null;
        var avgPrev = totalPrev.Orders > 0 ? Math.Round(totalPrev.Revenue / totalPrev.Orders, 0) : (decimal?)null;
        decimal? V(decimal value) => covered ? value : null;
        var noData = covered ? null : "нет данных за период";
        var kpis = new List<Kpi>
        {
            Kpi.Of("sales.revenue", "Выручка", V(total.Revenue), "сум", totalPrev.Revenue, KpiKinds.Fact, src, "/sales", noData),
            Kpi.Of("sales.kg", "Продажи", V(total.Kg), "кг", totalPrev.Kg, KpiKinds.Fact, src, "/sales", noData),
            Kpi.Of("sales.orders", "Заказы", V(total.Orders), "шт", totalPrev.Orders, KpiKinds.Fact, src, "/sales", noData),
            Kpi.Of("sales.akb", "АКБ (точки с покупкой)", V(akb), "точек", akbPrev, KpiKinds.Fact, src, "/sales", noData),
            Kpi.Of("sales.avg_check", "Средний чек", covered ? avgCheck : null, "сум", avgPrev, KpiKinds.Calculated, src, "/sales", noData),
            Kpi.Of("sales.returns", "Возвраты", V(Math.Abs(total.ReturnsKg)), "кг", Math.Abs(totalPrev.ReturnsKg), KpiKinds.Fact, src, "/sales", noData),
        };

        var regions = rows.Select(r =>
        {
            var prev = previousRows.GetValueOrDefault(r.Id);
            return new SalesRegionRow(r.Id, r.Name, r.Figures.Revenue, r.Figures.Kg, r.Akb, r.Figures.Orders, prev?.Figures.Revenue,
                prev is { Figures.Revenue: > 0 } pr ? Math.Round((r.Figures.Revenue - pr.Figures.Revenue) / pr.Figures.Revenue * 100, 1) : null, null, null, null);
        }).OrderByDescending(r => r.Revenue).ToList();

        var plan = await PlanAsync(p, available, ct);
        if (plan is not null)
        {
            regions = regions.Select(r => plan.Regions.FirstOrDefault(x => x.Id == r.Id) is { } pr
                ? r with { PlanKg = pr.PlanKg, Execution = pr.Execution, ForecastExecution = pr.ForecastExecution }
                : r).ToList();
            kpis.Add(Kpi.Of("sales.plan_execution", $"Выполнение плана ({DataTool.MonthName(plan.Month)})", plan.Execution, "%", null, KpiKinds.Calculated,
                $"OneBase → Продажи → план ({plan.PlanSource})", "/sales"));
            // Месяц плана — текущий или последний загруженный: подпись и пометка говорят, какой это месяц и по какой день факт.
            kpis.Add(Kpi.Of("sales.forecast_execution", $"Прогноз выполнения ({DataTool.MonthName(plan.Month)})", plan.ForecastExecution, "%", null, KpiKinds.Forecast,
                "OneBase → Продажи: факт ÷ прошедшие дни × дни месяца", "/sales", $"факт по {plan.DataThrough:dd.MM.yyyy}"));
        }

        var daily = regionLevel ? [] : main.Days.Select(d => new SalesDay(d.Date, d.Revenue, d.Kg, d.Orders, d.Akb)).ToList();
        return new SalesBlock(period, main.DataThrough, main.Complete, kpis, daily, regions, plan, Scope(p), notes,
            [src + ": за период и прошлый период", "OneBase → Продажи → планы и прогноз месяца"]);
    }

    private static string? Scope(AnalytikPrincipal p) => p.Sales.Restricted ? p.Sales.Description : null;

    /// <summary>План текущего месяца (последнего загруженного): республика или сумма своих регионов; у агента — нет (план агента — на его странице).</summary>
    public async Task<SalesMonthPlan?> PlanAsync(AnalytikPrincipal p, HashSet<(int Year, int Month)>? available, CancellationToken ct)
    {
        if (!CanView(p) || p.Sales.Level == SalesRowScope.AgentLevel)
        {
            return null;
        }

        available ??= await MonthsAsync(ct);
        if (available.Count == 0)
        {
            return null;
        }

        var today = FieldClock.LocalDate(clock.GetUtcNow());
        var (year, month) = available.Contains((today.Year, today.Month)) ? (today.Year, today.Month) : available.Max();
        var analytics = await loader.LoadAsync(year, month, ct);
        var view = analytics.CachedRepublic(null, null);
        var regions = view.Regions.Where(r => Visible(p, r.Id))
            .Select(r => new SalesRegionRow(r.Id, r.Name, r.Revenue, r.FactKg, r.Akb, 0, null, null, r.PlanKg, Pct(r.Execution), Pct(r.ForecastExecution)))
            .ToList();
        if (p.Sales.Restricted)
        {
            // Выполнение своих регионов — как у страницы: по факту, сравнимому с планом, то есть взвешенное планами регионов.
            var planned = regions.Where(r => r.PlanKg is > 0).ToList();
            var planKg = planned.Count == 0 ? (decimal?)null : planned.Sum(r => r.PlanKg!.Value);
            decimal? Weighted(Func<SalesRegionRow, decimal?> share) =>
                planKg is > 0 && planned.All(r => share(r) is not null) ? Math.Round(planned.Sum(r => share(r)!.Value * r.PlanKg!.Value) / planKg.Value, 1) : null;
            var forecast = view.Regions.Where(r => Visible(p, r.Id)).Sum(r => r.ForecastKg ?? 0);
            return new SalesMonthPlan(year, month, view.Period.DataThrough, regions.Sum(r => r.Kg), planKg, Weighted(r => r.Execution), forecast,
                Weighted(r => r.ForecastExecution), regions.Sum(r => r.Revenue), view.Kpi.PlanSource, regions,
                planKg is > 0 && Weighted(r => r.Execution) is { } e ? Math.Round(e / 100 * planKg.Value, 0) : null);
        }

        return new SalesMonthPlan(year, month, view.Period.DataThrough, view.Kpi.FactKg, view.Kpi.PlanKg, Pct(view.Kpi.Execution), view.Kpi.ForecastKg,
            Pct(view.Kpi.ForecastExecution), view.Kpi.Revenue, view.Kpi.PlanSource, regions, view.Kpi.PlanFactKg);
    }

    /// <summary>По какой день есть данные продаж (последний загруженный месяц).</summary>
    private async Task<DateOnly?> LastDataAsync(HashSet<(int Year, int Month)> available, CancellationToken ct)
    {
        if (available.Count == 0)
        {
            return null;
        }

        var (year, month) = available.Max();
        return (await loader.LoadAsync(year, month, ct)).Period.DataThrough;
    }

    /// <summary>Доля 0..1 → проценты.</summary>
    private static decimal? Pct(decimal? share) => share is { } s ? Math.Round(s * 100, 1) : null;

    /// <summary>Дневной ряд области пользователя (revenue, kg, orders, akb) — для прогноза и отклонений; дни без продаж — нули.</summary>
    public async Task<IReadOnlyList<SeriesPoint>> DailyAsync(string metric, DateOnly from, DateOnly to, AnalytikPrincipal p, CancellationToken ct)
    {
        if (!CanView(p))
        {
            throw new QueryPlanException(QueryErrors.Denied, "Нет доступа к продажам.");
        }

        var available = await MonthsAsync(ct);
        if (available.Count == 0)
        {
            return [];
        }

        var days = new Dictionary<DateOnly, SalesDay>();
        if (p.Sales.Level == SalesRowScope.RegionLevel)
        {
            foreach (var region in p.Sales.RegionIds)
            {
                var part = await SalesPeriodEngine.AggregateAsync(new DateRange(from, to), new SalesScope(Region: region), Month(available), false, false, ct);
                foreach (var d in part.Days)
                {
                    days[d.Date] = days.TryGetValue(d.Date, out var x)
                        ? new SalesDay(d.Date, x.Revenue + d.Revenue, x.Kg + d.Kg, x.Orders + d.Orders, x.Akb + d.Akb)
                        : new SalesDay(d.Date, d.Revenue, d.Kg, d.Orders, d.Akb);
                }
            }
        }
        else
        {
            var all = await AggregateAsync(from, to, p, available, ct);
            foreach (var d in all!.Days)
            {
                days[d.Date] = new SalesDay(d.Date, d.Revenue, d.Kg, d.Orders, d.Akb);
            }
        }

        var through = days.Count == 0 ? to : days.Keys.Max();
        var end = through < to ? through : to;
        return Enumerable.Range(0, Math.Max(0, end.DayNumber - from.DayNumber + 1)).Select(i => from.AddDays(i)).Select(d =>
        {
            var day = days.GetValueOrDefault(d);
            return new SeriesPoint(d, metric switch
            {
                "kg" => day?.Kg ?? 0,
                "orders" => day?.Orders ?? 0,
                "akb" => day?.Akb ?? 0,
                _ => day?.Revenue ?? 0,
            });
        }).ToList();
    }
}

public sealed record PaymentTypeRow(string Type, decimal Amount, int Count, decimal? PreviousAmount);

public sealed record FinanceBlock(AnalytikPeriod Period, IReadOnlyList<Kpi> Kpis, IReadOnlyList<PaymentTypeRow> Payments, IReadOnlyList<string> Notes,
    IReadOnlyList<string> Sources) : IAnalytikBlock;

/// <summary>
/// Финансы для «Аналитика» по тому, что есть в OneBase: оплаты точек (Linko, принятые, без удалённых), выручка продаж, расходы на закупки
/// (снабжение), P&amp;L дилеров (загруженные месяцы). Доходов и расходов компании, бюджетов, кредиторки и денежного потока нет — чистая
/// прибыль не считается, а прямо называется отсутствующей.
/// </summary>
public sealed class FinanceAnalytik(IAppDbContext db, ProcurementRequestService requests, ProcurementInsightService insight, PnlService pnl)
{
    public static bool CanView(AnalytikPrincipal p) => p.Has(Permissions.FinanceRead);

    public async Task<FinanceBlock?> BlockAsync(AnalytikPeriod period, AnalytikPrincipal p, SalesBlock? sales, CancellationToken ct)
    {
        if (!CanView(p))
        {
            return null;
        }

        var notes = new List<string>
        {
            "Расходов компании, бюджетов, кредиторской задолженности и денежного потока в OneBase нет (модуль «Финансы» не построен) — чистая прибыль и кассовые разрывы не считаются.",
        };
        var kpis = new List<Kpi>();
        var paymentRows = new List<PaymentTypeRow>();
        if (p.Sales.Restricted)
        {
            notes.Add("Оплаты точек показываются только при доступе ко всей организации.");
        }
        else
        {
            var payments = db.LinkoPayments.AsNoTracking().Where(x => !x.IsDelete && x.Status == "accepted" && x.CreatedDate != null);
            var current = await payments.Where(x => x.CreatedDate >= period.From && x.CreatedDate <= period.To)
                .GroupBy(x => x.PaymentType).Select(g => new { Type = g.Key, Amount = g.Sum(x => x.Amount), Count = g.Count() }).ToListAsync(ct);
            var previous = await payments.Where(x => x.CreatedDate >= period.PreviousFrom && x.CreatedDate <= period.PreviousTo)
                .GroupBy(x => x.PaymentType).Select(g => new { Type = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
            paymentRows = current.Select(c => new PaymentTypeRow(c.Type ?? "—", c.Amount, c.Count, previous.FirstOrDefault(x => x.Type == c.Type)?.Amount))
                .OrderByDescending(r => r.Amount).ToList();
            // Оплат за период в OneBase нет (загружены позже или ещё не загружены) — «нет данных», а не ноль (ноль — это «оплат не было»).
            // Прошлый период сравнивается, только если он целиком внутри загруженного.
            var first = await payments.MinAsync(x => x.CreatedDate, ct);
            var last = await payments.MaxAsync(x => x.CreatedDate, ct);
            var loaded = first is { } f && last is { } l && l >= period.From && f <= period.To;
            if (!loaded)
            {
                notes.Add(first is null
                    ? "Оплат точек в OneBase нет."
                    : $"Оплаты точек в OneBase загружены с {first:dd.MM.yyyy} по {last:dd.MM.yyyy} — за период {period.Label} данных нет.");
            }
            else if (first > period.From)
            {
                notes.Add($"Оплаты точек в OneBase — с {first:dd.MM.yyyy}: период покрыт не полностью.");
            }

            var comparable = first is { } since && since <= period.PreviousFrom;
            kpis.Add(Kpi.Of("finance.payments", "Оплаты точек (принятые)", loaded ? current.Sum(c => c.Amount) : null, "сум", comparable ? previous.Sum(c => c.Amount) : null,
                KpiKinds.Fact, "OneBase → Linko: оплаты точек", "/sales", loaded ? null : "нет данных за период"));
            if (loaded && sales?.Kpis.FirstOrDefault(k => k.Code == "sales.revenue") is { Value: > 0 } revenue)
            {
                kpis.Add(Kpi.Of("finance.collection", "Оплаты к выручке периода", Math.Round(current.Sum(c => c.Amount) / revenue.Value!.Value * 100, 1), "%", null,
                    KpiKinds.Calculated, "Оплаты точек ÷ выручка вторички", "/sales"));
            }
        }

        if (p.HasAny([Permissions.ProcurementRead, Permissions.ProcurementManage]))
        {
            var ctx = p.System || p.UserId is not { } userId
                ? new ProcurementContext(Guid.Empty, new HashSet<string> { Permissions.ProcurementRead }, null, new HashSet<Guid>(), IsDirector: true)
                : await requests.ContextAsync(userId, p.Permissions, ct);
            var expenses = await insight.ExpensesAsync(ctx, new ProcurementFilter(period.From, period.To), ct);
            // Закупки ведутся в OneBase с первого утверждённого заказа поставщику: до него — «нет данных», а не ноль.
            var started = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.ApprovedAt != null).MinAsync(o => o.ApprovedAt, ct);
            var since = started is { } st ? FieldClock.LocalDate(st) : (DateOnly?)null;
            var covered = since is { } sd && sd <= period.To;
            if (!covered)
            {
                notes.Add(since is null
                    ? "Утверждённых заказов поставщикам в OneBase ещё нет — расходы на закупки не считаются."
                    : $"Закупки ведутся в OneBase с {since:dd.MM.yyyy} — за период {period.Label} расходов на закупки в OneBase нет.");
            }

            var days = period.To.DayNumber - period.From.DayNumber + 1;
            kpis.Add(Kpi.Of("finance.procurement_spend", "Расходы на закупки", covered ? expenses.Total : null, "сум",
                since is { } s0 && s0 <= period.From.AddDays(-days) ? expenses.PreviousTotal : null, KpiKinds.Fact,
                "OneBase → Снабжение → Расходы", "/procurement", covered ? null : "нет данных за период"));
        }

        var months = await pnl.MonthsAsync(ct);
        notes.Add(months.Count == 0
            ? "P&L дилеров не загружен (AI Data Manager)."
            : $"P&L дилеров загружен за {months.Count} мес. — подробно в «Продажи → P&L» и у Finance Analyst.");
        notes.Add("Долги дилеров и DSO — в «Продажи → DSO» (реестры Google Sheets).");
        return new FinanceBlock(period, kpis, paymentRows, notes, ["OneBase → Linko: оплаты точек", "OneBase → Снабжение → Расходы", "OneBase → Продажи → P&L"]);
    }
}

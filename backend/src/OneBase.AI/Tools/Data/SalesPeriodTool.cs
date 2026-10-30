using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Продажи за любой период до года одним вызовом: несколько месяцев, квартал, полугодие, год, неделя или отрезок через границу месяца — и
/// сравнение с другим периодом. Считает SalesPeriodEngine из расчётов месяцев «Продаж» (RouteSplit): те же правила, АКБ периода — точки с
/// чистой покупкой за весь период, разница периодов — кодом, не моделью.
/// </summary>
internal sealed class GetSalesPeriodTool(SalesDataLoader loader, IAppDbContext db, TimeProvider clock) : SalesTool(loader)
{
    /// <summary>Узбекистан — UTC+5 без перехода на летнее время: «сегодня» и даты периода — по Ташкенту.</summary>
    internal static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    public override string Name => "get_sales_period";
    public override string Title => "Продажи за период";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string UsageHint =>
        "Продажи за несколько месяцев, квартал, полугодие, год, неделю через границу месяца или «с 25 сентября по 10 октября», и сравнение двух " +
        "периодов (к прошлому, к году назад) — get_sales_period: один вызов вместо вызова по каждому месяцу.";

    public override string Description =>
        "Вторичные продажи за период до 366 дней — по республике, направлению РМ, региону или ТП: итог (кг, выручка в сумах, заказы, возвраты, " +
        "АКБ — точки с чистой покупкой за весь период, не сумма АКБ месяцев), по месяцам, неделям или дням (group), регионы, ТП (agents=true) и " +
        "сравнение с другим периодом (compare: previous — предыдущий той же длины, year_ago — тот же год назад, или период) с разницей и % — " +
        "посчитаны кодом. period: «2026-09» месяц, «2026-Q3» квартал, «2026-H1» полугодие, «2026» год; или from и to (ГГГГ-ММ-ДД, включительно, " +
        "по Ташкенту). Coverage — полнота: месяцы без данных и по какой день данные. Правила — как в «Продажах»: заказы − возвраты по дате " +
        "приёмки, без «Завода».";

    public override JsonElement InputSchema { get; } = Schema(
        ("period", new { type = "string", description = "«2026-09», «2026-Q3», «2026-H1», «2026» или «2026-09-25..2026-10-10». Вместо from/to." }),
        DrillLookup.FromArg,
        DrillLookup.ToArg,
        ("compare", new { type = "string", description = "С чем сравнить: previous, year_ago или период («2026-Q2», «2026-08»)." }),
        ("group", new { type = "string", @enum = new[] { "month", "week", "day" }, description = "Разбивка: по месяцам, неделям (с понедельника) или дням." }),
        RegionArg,
        DirectionArg,
        DrillLookup.AgentArg,
        ("agents", new { type = "boolean", description = "Строки каждого ТП (у региона и ТП — всегда)." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var range = RangeOf(args, Today(clock));
        var available = (await Loader.MonthsAsync(ct)).Select(m => (m.Year, m.Month)).ToHashSet();
        if (available.Count == 0)
        {
            throw new ToolArgumentException("Данных о продажах в OneBase ещё нет: Linko не загружался.");
        }

        // Область — по месяцу конца периода (если он загружен) или по последнему месяцу: регионы и ТП одни и те же.
        var anchor = available.Contains((range.To.Year, range.To.Month)) ? (range.To.Year, range.To.Month) : available.Max();
        var analytics = await Loader.LoadAsync(anchor.Item1, anchor.Item2, ct);
        var republic = analytics.CachedRepublic(null, null);
        long? agentId = null;
        string? agentName = null;
        if (args.Str("agent") is { } agentQuery)
        {
            (agentId, agentName) = await DrillLookup.AgentAsync(db, analytics, agentQuery, ct);
        }

        var region = agentId is null ? FindRegion(republic, args.Str("region")) : null;
        var direction = agentId is null && region is null ? FindDirection(republic, args.Str("direction")) : null;
        var scope = new SalesScope(region is null ? null : Guid.Parse(region.Id), direction is null ? null : Guid.Parse(direction.Id), agentId);
        var scopeName = agentName ?? region?.Name ?? direction?.Name ?? "Республика";
        var agents = args.Bool("agents") ?? false;

        Task<ISalesMonth?> Month(int year, int month, CancellationToken token) => MonthAsync(available, year, month, token);
        var main = await SalesPeriodEngine.AggregateAsync(range, scope, Month, regions: true, agents, ct);
        var compareRange = CompareOf(args.Str("compare"), range);
        var other = compareRange is null ? null : await SalesPeriodEngine.AggregateAsync(compareRange, scope, Month, regions: false, agents: false, ct);

        var group = args.Str("group")?.ToLowerInvariant();
        var monthsShown = group == "month" || (group is null && main.Months.Count > 1);
        var sources = new List<DataSource> { new($"OneBase → Продажи → {scopeName} → за период", range.Label, $"/sales/republic?{Query(range.To.Year, range.To.Month)}") };
        if (compareRange is not null)
        {
            sources.Add(new DataSource($"OneBase → Продажи → {scopeName} → за период (сравнение)", compareRange.Label));
        }

        return Data(new
        {
            Period = Describe(range),
            Scope = scopeName,
            Coverage = Coverage(main),
            Total = Figures(main.Total, main.Akb),
            Months = monthsShown ? Parts(main.Months) : null,
            Weeks = group == "week" ? args.Top(Parts(main.Weeks), 60, "weeks") : null,
            Days = group == "day"
                ? args.Top(main.Days.Select(d => new { Date = DrillLookup.Date(d.Date), FactKg = R(d.Kg, 1), RevenueSum = R(d.Revenue), d.Orders, d.Akb }), 120, "days")
                : null,
            Regions = main.Regions.Count == 0 ? null : args.Top(main.Regions.Select(Row), 30, "regions"),
            Agents = main.Agents.Count == 0 ? null : args.Top(main.Agents.Select(Row), 40, "agents"),
            Compare = other is null
                ? null
                : new
                {
                    Period = Describe(other.Range),
                    Coverage = Coverage(other),
                    Total = Figures(other.Total, other.Akb),
                    Delta = Delta(main, other),
                },
            Note = "Границы периода — включительно, даты — по Ташкенту. Факт — заказы − возвраты по дате приёмки, без «Завода» (как в «Продажах»). " +
                   "АКБ периода и недели — точки с чистой покупкой за весь период или неделю (не сумма АКБ месяцев). Delta — период минус сравнение, " +
                   "% — к сравнению; посчитаны кодом. Coverage.Complete = false — часть периода без данных: итог только за дни с данными.",
        }, [.. sources]);
    }

    /// <summary>«Сегодня» по Ташкенту: в 23:30 UTC 30 сентября в Ташкенте уже 1 октября.</summary>
    internal static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(CompanyOffset).DateTime);

    /// <summary>Период из period или from/to; не длиннее года.</summary>
    internal static DateRange RangeOf(ToolArgs args, DateOnly today)
    {
        DateRange? range;
        if (args.Str("period") is { } text)
        {
            range = PeriodRanges.Parse(text)
                    ?? throw new ToolArgumentException($"Период «{text}» не распознан: «2026-09», «2026-Q3», «2026-H1», «2026» или from и to (ГГГГ-ММ-ДД).");
        }
        else if (DrillLookup.Day(args, "from") is { } from)
        {
            var to = DrillLookup.Day(args, "to") ?? today;
            range = from <= to ? new DateRange(from, to) : throw new ToolArgumentException("Аргумент from должен быть не позже to.");
        }
        else
        {
            throw new ToolArgumentException("Укажите period («2026-09», «2026-Q3», «2026-H1», «2026») или from и to (ГГГГ-ММ-ДД).");
        }

        return range.Days > PeriodRanges.MaxDays
            ? throw new ToolArgumentException($"Период — не больше {PeriodRanges.MaxDays} дней: {range.Label} — {range.Days} дн. Разбейте на части.")
            : range;
    }

    /// <summary>Период сравнения: previous — предыдущий той же длины, year_ago — тот же год назад, иначе — разбор периода.</summary>
    internal static DateRange? CompareOf(string? compare, DateRange range)
    {
        if (string.IsNullOrWhiteSpace(compare))
        {
            return null;
        }

        var other = compare.Trim().ToLowerInvariant() switch
        {
            "previous" or "prev" or "предыдущий" => PeriodRanges.Previous(range),
            "year_ago" or "year" or "год назад" => PeriodRanges.YearAgo(range),
            _ => PeriodRanges.Parse(compare) ?? throw new ToolArgumentException($"Период сравнения «{compare}» не распознан: previous, year_ago или период."),
        };
        return other.Days > PeriodRanges.MaxDays
            ? throw new ToolArgumentException($"Период — не больше {PeriodRanges.MaxDays} дней: {other.Label} — {other.Days} дн.")
            : other;
    }

    private async Task<ISalesMonth?> MonthAsync(HashSet<(int Year, int Month)> available, int year, int month, CancellationToken ct) =>
        available.Contains((year, month)) ? new AnalyticsMonth(await Loader.LoadAsync(year, month, ct)) : null;

    private static object Describe(DateRange r) => new { From = DrillLookup.Date(r.From), To = DrillLookup.Date(r.To), r.Days, r.Label };

    private static object Coverage(PeriodSales p) => new
    {
        p.Complete,
        DataThrough = p.DataThrough is { } d ? DrillLookup.Date(d) : null,
        Missing = p.Missing.Count == 0 ? null : p.Missing.Select(m => m.Label).ToList(),
    };

    private static object Figures(SalesFigures f, int akb) => new { FactKg = R(f.Kg, 1), RevenueSum = R(f.Revenue), f.Orders, Akb = akb, ReturnsKg = R(f.ReturnsKg, 1) };

    private static IReadOnlyList<object> Parts(IReadOnlyList<PeriodPart> parts) =>
        parts.Select((p, i) => (object)new
        {
            From = DrillLookup.Date(p.Range.From),
            To = DrillLookup.Date(p.Range.To),
            p.Range.Label,
            FactKg = R(p.Figures.Kg, 1),
            RevenueSum = R(p.Figures.Revenue),
            p.Figures.Orders,
            p.Akb,
            Partial = p.Partial ? true : (bool?)null,
            Missing = p.Missing ? true : (bool?)null,
            // К предыдущей части (месяцу или неделе), % — кодом; у части без данных — нет.
            VsPreviousKgPct = i > 0 && !p.Missing && !parts[i - 1].Missing ? Change(p.Figures.Kg, parts[i - 1].Figures.Kg) : null,
            VsPreviousRevenuePct = i > 0 && !p.Missing && !parts[i - 1].Missing ? Change(p.Figures.Revenue, parts[i - 1].Figures.Revenue) : null,
        }).ToList();

    private static object Row(PeriodRow r) => new { r.Name, r.Region, FactKg = R(r.Figures.Kg, 1), RevenueSum = R(r.Figures.Revenue), r.Figures.Orders, r.Akb };

    /// <summary>Разница периода и сравнения: абсолютная и в % к сравнению.</summary>
    internal static object Delta(PeriodSales main, PeriodSales other) => new
    {
        FactKg = R(main.Total.Kg - other.Total.Kg, 1),
        FactKgPct = Change(main.Total.Kg, other.Total.Kg),
        RevenueSum = R(main.Total.Revenue - other.Total.Revenue),
        RevenueSumPct = Change(main.Total.Revenue, other.Total.Revenue),
        Orders = main.Total.Orders - other.Total.Orders,
        OrdersPct = Change(main.Total.Orders, other.Total.Orders),
        Akb = main.Akb - other.Akb,
        AkbPct = Change(main.Akb, other.Akb),
    };

    /// <summary>Изменение в % к базе (до 0,1); база 0 — нет процента.</summary>
    internal static decimal? Change(decimal value, decimal basis) => basis == 0 ? null : Math.Round((value - basis) / Math.Abs(basis) * 100, 1);
}

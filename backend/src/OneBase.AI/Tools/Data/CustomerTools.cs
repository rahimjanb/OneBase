using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// «Кто не купил»: точки, которые брали категорию, бренд или товар в базовом периоде (прошлый месяц или до трёх месяцев) и ещё не взяли
/// в месяце отчёта, — списком, с ТП, объёмом базы, последней покупкой, покупками другого и визитами. get_customers даёт только
/// количество молчащих по регионам и ТП, без категорий и без списка точек.
/// </summary>
internal sealed class GetLapsedCustomersTool(SalesDataLoader loader, SalesOptions options, IAppDbContext db) : SalesTool(loader)
{
    internal const int DefaultLimit = 30;
    /// <summary>50 точек — около 13 000 символов: ответ помещается в окно результата инструмента (AgentRunner режет на 16 000).</summary>
    internal const int MaxLimit = 50;
    private const int MaxProducts = 60;

    public override string Name => "get_lapsed_customers";
    public override string Title => "Клиенты, которые не купили";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string Description =>
        "Клиенты — торговые точки, которые покупали категорию, бренд или товар в базовом периоде и ещё не купили их в выбранном месяце (или купили — status), " +
        "списком: точка, регион, ТП, кг и выручка в базе, место по объёму, последняя покупка, сколько взяли в этом месяце, покупают ли другие товары, визиты в этом месяце; " +
        "итоги — сколько точек в базе, сколько купили и не купили, их кг и выручка, новые точки. Примеры: «кто из клиентов по помадке, покупавших в сентябре, не купил в октябре», " +
        "«кто из крупнейших клиентов по могулу ещё не взял», «какие точки перестали брать Yamelly», «кто из точек ТП … не купил шоколад». " +
        "База — прошлый месяц (base_months — до 3 месяцев); largest — только N крупнейших точек базы по кг. Без category и product — все товары.";

    public override string UsageHint =>
        "Вопросы «кто не купил / перестал брать / ещё не взял / молчит / ушёл» и вопросы о крупнейших клиентах категории, бренда или товара — get_lapsed_customers: " +
        "он отдаёт список точек (get_customers — только количество по регионам и ТП, без категорий). «Крупнейшие» — largest (20, если число не названо), " +
        "«в сентябре … в октябре» — month = октябрь, база — прошлый месяц. Месяц не закончен — пиши «на {дату последних данных} ещё не купили».";

    public override JsonElement InputSchema { get; } = Schema(
        ("category", new { type = "string", description = "Категория или тип Linko: «помадка», «могул», «шоколад»; несколько — через запятую. Бренд или SKU — в product." }),
        ("product", new { type = "string", description = "Товар, бренд или артикул: «Yamelly», «ARBUZ», «480». Вместе с category — товары внутри категории." }),
        YearArg,
        ("month", new { type = "integer", description = "Месяц, в котором проверяем покупку (1–12). Без него — текущий; база — месяцы перед ним." }),
        RegionArg,
        ("agent", new { type = "string", description = "ТП: имя или id Linko — только точки, закреплённые за ним (кто продавал им больше всех в базе)." }),
        ("base_months", new { type = "integer", description = "База — сколько месяцев перед month: 1 (по умолчанию — прошлый месяц), 2 или 3." }),
        ("largest", new { type = "integer", description = "Только N крупнейших точек базы по кг (для «крупнейших клиентов»), 1–500." }),
        ("status", new { type = "string", @enum = new[] { "not_bought", "bought", "all" }, description = "not_bought — ещё не купили (по умолчанию), bought — купили, all — все точки базы." }),
        ("limit", new { type = "integer", description = $"Сколько точек в списке, по умолчанию {DefaultLimit}, до {MaxLimit}." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        // Аргументы — до чтения данных: ошибка в них не ждёт загрузки месяца.
        var baseMonths = args.Int("base_months", 1, 3) ?? 1;
        var largest = args.Int("largest", 1, 500);
        var limit = args.Int("limit", 1, MaxLimit) ?? DefaultLimit;
        var status = StatusOf(args.Str("status"));

        var analytics = await LoadAsync(args, ct);
        var republic = analytics.CachedRepublic(null, null);
        var region = FindRegion(republic, args.Str("region"));
        var p = analytics.Period;

        var notes = new List<string>();
        var pick = Categories(analytics, args.Str("category"));
        var (groups, categoryNames, linkoTypes, categoryMiss) = (pick.Groups, pick.Names, pick.Types, pick.Miss);
        if (pick.TypeNote is not null)
        {
            notes.Add(pick.TypeNote);
        }

        // «Yamelly» в category — бренд, а не категория: ищем товары по тому же названию.
        var productQuery = args.Str("product") ?? categoryMiss;
        var within = groups is not null ? analytics.ProductsIn(groups) : pick.TypeProducts;
        var products = productQuery is null ? null : Products(analytics, productQuery, within);
        if (categoryMiss is not null)
        {
            if (products is null or { Count: 0 })
            {
                var known = analytics.CategoryGlossary().Where(g => g.Configured).Select(g => g.Name);
                throw new ToolArgumentException($"Категории или товара «{categoryMiss}» нет. Категории: {string.Join(", ", known)}. Товар или бренд — в product.");
            }

            notes.Add($"Категории «{categoryMiss}» нет — взяты товары с таким названием.");
        }

        if (products is { Count: 0 })
        {
            throw new ToolArgumentException($"Товаров «{productQuery}»{(within is null ? "" : " в этой категории")} нет. Найти товар по названию — find_products.");
        }

        long? agentId = null;
        string? agentName = null;
        if (args.Str("agent") is { } agentQuery)
        {
            (var id, agentName) = await DrillLookup.AgentAsync(db, analytics, agentQuery, ct);
            agentId = id;
        }

        // База из нескольких месяцев: отчёт на два месяца раньше даёт ещё один или два месяца.
        SalesAnalytics? earlier = null;
        if (baseMonths > 1)
        {
            var start = new DateOnly(p.Year, p.Month, 1).AddMonths(-2);
            var months = await Loader.MonthsAsync(ct);
            if (months.Any(m => m.Year == start.Year && m.Month == start.Month))
            {
                earlier = await Loader.LoadAsync(start.Year, start.Month, ct);
            }
            else
            {
                notes.Add($"Данных за {MonthName(start.Month)} {start.Year} нет — база только за прошлый месяц.");
                baseMonths = 1;
            }
        }

        var query = new CustomerLapseQuery(groups, products?.Select(x => x.Id).ToHashSet() ?? pick.TypeProducts, region is null ? null : Guid.Parse(region.Id), agentId);
        var view = analytics.CustomerLapse(query, earlier, baseMonths - 1);
        var considered = largest is { } n ? view.Rows.Take(n).ToList() : view.Rows;
        var picked = considered.Where(r => status switch { Statuses.Bought => r.Bought, Statuses.All => true, _ => !r.Bought }).ToList();
        var shown = picked.Take(args.Cap(limit)).ToList();
        var notBought = considered.Where(r => !r.Bought).ToList();

        var complete = p.DataThrough >= new DateOnly(p.Year, p.Month, 1).AddMonths(1).AddDays(-1);
        if (!complete)
        {
            notes.Add($"Месяц не закончен: данные по {p.DataThrough:dd.MM.yyyy} — «ещё не купили» на эту дату.");
        }

        if (view.BaseCustomers == 0 && !view.NoData)
        {
            notes.Add($"За {BaseText(view.BaseFrom, view.BaseTo)} эти товары никто не покупал — сравнивать не с чем. Другой период — base_months или month.");
        }

        if (view.NoData)
        {
            notes.Add("За месяц в охвате нет ни одной покупки — это дыра в данных, а не «никто не купил».");
        }

        var scope = agentName ?? region?.Name ?? "Республика";
        return Data(new
        {
            Period = PeriodOf(p),
            Scope = scope,
            Filter = new
            {
                Categories = categoryNames,
                LinkoTypes = linkoTypes,
                Products = products?.Take(5).Select(x => x.Name),
                ProductsFound = products?.Count,
            },
            Base = BaseText(view.BaseFrom, view.BaseTo),
            Checked = $"{MonthName(p.Month)} {p.Year}{(complete ? "" : $" (по {p.DataThrough:dd.MM})")}",
            Summary = new
            {
                BaseCustomers = view.BaseCustomers,
                BaseKg = R(view.BaseKg),
                BaseRevenueSum = R(view.BaseRevenue),
                BoughtNow = view.Bought,
                NotBought = view.NotBought,
                NotBoughtSharePct = Pct(SalesMath.Ratio(view.NotBought, view.BaseCustomers)),
                NotBoughtBaseKg = R(view.NotBoughtBaseKg),
                NotBoughtBaseRevenueSum = R(view.NotBoughtBaseRevenue),
                NotBoughtButBuyOther = view.NotBoughtActive,
                NewCustomers = view.NewCustomers,
            },
            Largest = largest is null ? null : new
            {
                Considered = considered.Count,
                NotBought = notBought.Count,
                NotBoughtBaseKg = R(notBought.Sum(r => r.BaseKg)),
                ShareOfBaseKgPct = Pct(SalesMath.Ratio(considered.Sum(r => r.BaseKg), view.BaseKg)),
            },
            Status = status,
            Total = picked.Count,
            Shown = shown.Count,
            Customers = shown.Select(r => new
            {
                Store = r.Name,
                StoreId = r.MarketId,
                r.Rank,
                r.Region,
                Agent = r.Agent,
                BaseKg = R(r.BaseKg, 1),
                BaseRevenueSum = R(r.BaseRevenue),
                BaseMonths = baseMonths > 1 ? r.BaseMonths : (int?)null,
                LastPurchase = r.LastPurchase?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                NowKg = r.Bought ? R(r.NowKg, 1) : (decimal?)null,
                NowRevenueSum = r.Bought ? R(r.NowRevenue) : (decimal?)null,
                OtherNowRevenueSum = r.OtherNowRevenue > 0 ? R(r.OtherNowRevenue) : (decimal?)null,
                VisitsNow = r.VisitsNow,
                VisitsPlanned = r.VisitsPlanned,
                VisitsMissed = r.VisitsMissed,
                VisitsOutsideRoute = r.VisitsOutsideRoute,
                LastVisit = r.LastVisit?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                Summary = RowSummary(r, p),
            }),
            Notes = notes,
            Note = "Summary — строка точки целиком (купил ли, другие товары, визиты месяца): в таблице ответа бери столбцы из неё, пустых ячеек не оставляй. " +
                   "Rank — место точки в базе по кг (1 — крупнейшая). OtherNowRevenueSum — точка в этом месяце покупает другие товары (жива, но эти не взяла); " +
                   "VisitsNow — выполненные визиты в этом месяце (любым ТП), VisitsPlanned — визиты в маршруте по отчётный день, VisitsMissed — из них не выполнены, VisitsOutsideRoute — выполненные не по плану (входят в VisitsNow): " +
                   "«0 из 4 по маршруту» — ТП не дошёл, «в маршруте не было» — точку не планировали. Агент — ТП, который продал точке больше всех в базе. " +
                   "Список — первые Shown из Total; остальные — с бо́льшим limit, по региону или ТП. Точку подробно — get_store со StoreId.",
        }, Sources(p, region, groups, categoryNames));
    }

    /// <summary>
    /// Строка точки одной фразой — чтобы в таблице ответа не терялись столбцы: купил ли, что берёт ещё, визиты месяца отчёта
    /// («не купил; другие товары — 3 430 000 сум; визитов в октябре: 3, последний 22.10»).
    /// </summary>
    internal static string RowSummary(CustomerLapseRow r, PeriodInfo p)
    {
        var ru = CultureInfo.GetCultureInfo("ru-RU");
        var parts = new List<string>
        {
            r.Bought ? $"купил: {r.NowKg.ToString("N1", ru)} кг на {Math.Round(r.NowRevenue).ToString("N0", ru)} сум" : "не купил",
        };
        if (r.OtherNowRevenue > 0)
        {
            parts.Add($"другие товары — {Math.Round(r.OtherNowRevenue).ToString("N0", ru)} сум");
        }

        var route = r.VisitsPlanned > 0
            ? $" (по маршруту {r.VisitsPlanned}{(r.VisitsMissed > 0 ? $", не выполнено {r.VisitsMissed}" : "")}{Outside(r)})"
            : $" (в маршруте не было{Outside(r)})";
        parts.Add(r.VisitsNow > 0
            ? $"визитов в {MonthIn(p.Month)}: {r.VisitsNow}, последний {r.LastVisit:dd.MM}{route}"
            : $"визитов в {MonthIn(p.Month)}: 0{route}");
        return string.Join("; ", parts);
    }

    private static string Outside(CustomerLapseRow r) => r.VisitsOutsideRoute > 0 ? $"; вне маршрута {r.VisitsOutsideRoute}" : "";

    /// <summary>Месяц в предложном падеже: «в октябре», «в мае», «в марте».</summary>
    internal static string MonthIn(int month)
    {
        var name = MonthName(month);
        return name.EndsWith('ь') || name.EndsWith('й') ? name[..^1] + "е" : name + "е";
    }

    internal static class Statuses
    {
        public const string NotBought = "not_bought";
        public const string Bought = "bought";
        public const string All = "all";
    }

    internal static string StatusOf(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or Statuses.NotBought or "не купили" or "not bought" => Statuses.NotBought,
        Statuses.Bought or "купили" => Statuses.Bought,
        Statuses.All or "все" => Statuses.All,
        _ => throw new ToolArgumentException("status — not_bought (ещё не купили), bought (купили) или all (все точки базы)."),
    };

    /// <summary>«сентябрь 2026» или «июль – сентябрь 2026».</summary>
    internal static string BaseText(DateOnly from, DateOnly to) =>
        from.Year == to.Year && from.Month == to.Month ? $"{MonthName(to.Month)} {to.Year}"
        : from.Year == to.Year ? $"{MonthName(from.Month)} – {MonthName(to.Month)} {to.Year}"
        : $"{MonthName(from.Month)} {from.Year} – {MonthName(to.Month)} {to.Year}";

    /// <summary>
    /// Что выбрано словом из category: Groups — категории отчёта целиком (совпало название категории или её название из планов: «могул» —
    /// помадка); TypeProducts — только товары типов Linko, если слово совпало лишь с типом внутри категории («мармелад» в «Импорте»).
    /// Miss — слово, которому нет ни категории, ни типа.
    /// </summary>
    private sealed record CategoryPick(
        IReadOnlySet<long>? Groups, IReadOnlyList<string>? Names, IReadOnlyList<string>? Types, IReadOnlySet<long>? TypeProducts, string? Miss, string? TypeNote);

    /// <summary>
    /// Категории по названию — как find_products (название, типы Linko, названия из планов), но точнее: совпало название категории —
    /// берётся она одна (из настройки — прежде типов вне неё), и «шоколад» не тянет «Импорт» из-за типа «… шоколад» внутри; совпал только
    /// тип Linko — только его товары. Несколько слов — через запятую (Parts).
    /// </summary>
    private CategoryPick Categories(SalesAnalytics analytics, string? value)
    {
        if (value is null)
        {
            return new(null, null, null, null, null, null);
        }

        var glossary = analytics.CategoryGlossary().Where(g => g.CategoryId != "none").ToList();
        var named = new List<CategoryGlossaryRow>();
        var types = new List<(CategoryGlossaryRow Row, string Type)>();
        foreach (var part in Parts(value))
        {
            var byName = glossary
                .Select(g => (Row: g, Score: FindProductsTool.Aliases(options, g).Append(g.Name).Max(n => NameMatch.Score(n, part))))
                .Where(x => x.Score > 0)
                .ToList();
            if (byName.Count > 0)
            {
                var best = byName.Max(x => x.Score);
                var top = byName.Where(x => x.Score == best).Select(x => x.Row).ToList();
                named.AddRange(top.Any(r => r.Configured) ? top.Where(r => r.Configured) : top);
                continue;
            }

            var byType = glossary.SelectMany(g => g.LinkoTypes.Select(t => (Row: g, Type: t, Score: NameMatch.Score(t, part)))).Where(x => x.Score > 0).ToList();
            if (byType.Count == 0)
            {
                return new(null, null, null, null, part, null);
            }

            var bestType = byType.Max(x => x.Score);
            types.AddRange(byType.Where(x => x.Score == bestType).Select(x => (x.Row, x.Type)));
        }

        named = named.DistinctBy(r => r.CategoryId).ToList();
        var groups = named.Select(r => long.Parse(r.CategoryId, CultureInfo.InvariantCulture)).ToHashSet();
        var linkoTypes = named.SelectMany(r => r.LinkoTypes).Concat(types.Select(x => x.Type)).Distinct().Take(12).ToList();
        if (types.Count == 0)
        {
            return new(groups, named.Select(r => r.Name).ToList(), linkoTypes, null, null, null);
        }

        // Тип Linko — только его товары; вместе с категориями по названию — их товары тоже.
        var typeNames = types.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var products = analytics.ProductsOfTypes(typeNames).ToHashSet();
        products.UnionWith(analytics.ProductsIn(groups));
        var owners = types.Select(x => x.Row.Name).Distinct().ToList();
        var note = $"{string.Join(", ", typeNames.Select(x => $"«{x}»"))} — тип Linko внутри категории {string.Join(", ", owners.Select(o => $"«{o}»"))}: взяты только его товары.";
        return new(null, named.Select(r => r.Name).Concat(owners).Distinct().ToList(), linkoTypes, products, null, note);
    }

    /// <summary>Слова через запятую или «;»; запятая между цифрами — часть названия («Помадка 0,420 кг»).</summary>
    internal static IEnumerable<string> Parts(string value) =>
        ListSeparator.Split(value).Select(x => x.Trim()).Where(x => x.Length > 0);

    private static readonly Regex ListSeparator = new(@";|,(?!\d)", RegexOptions.CultureInvariant);

    /// <summary>Товары по названию или артикулу (точное совпадение артикула — прежде всего); within — только среди этих товаров (категории).</summary>
    private static List<ProductInfo> Products(SalesAnalytics analytics, string value, IReadOnlySet<long>? within)
    {
        var scored = analytics.ProductCatalog
            .Where(x => within is null || within.Contains(x.Id))
            .Select(x => (Info: x, Score: string.Equals(x.Code, value, StringComparison.OrdinalIgnoreCase) ? 4
                : Math.Max(NameMatch.Score(x.Name, value), x.Code is null ? 0 : NameMatch.Score(x.Code, value))))
            .Where(x => x.Score > 0)
            .ToList();
        if (scored.Count == 0)
        {
            return [];
        }

        var best = scored.Max(x => x.Score);
        return scored.Where(x => x.Score == best).Select(x => x.Info).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).Take(MaxProducts).ToList();
    }

    private static DataSource[] Sources(PeriodInfo p, UnitRow? region, IReadOnlySet<long>? groups, IReadOnlyList<string>? names)
    {
        var period = Query(p.Year, p.Month) + (region is null ? string.Empty : $"&region={region.Id}");
        if (groups is { Count: 1 } && names is { Count: 1 })
        {
            return [new DataSource($"OneBase → Продажи → Ассортимент → {names[0]}", PeriodText(p), $"/sales/assortment/category/{groups.First()}?{period}")];
        }

        return [region is null ? RepublicSource(p) : RegionSource(p, region)];
    }
}

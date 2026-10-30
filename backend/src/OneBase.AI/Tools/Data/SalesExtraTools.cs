using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.SkuSales;
using OneBase.Application.Security;

namespace OneBase.AI.Tools.Data;

/// <summary>Месяц «ГГГГ-ММ» в аргументах инструментов за период: разбор, сдвиг, подписи.</summary>
internal static class MonthRange
{
    private static readonly Regex Format = new(@"^(\d{4})-(\d{1,2})$", RegexOptions.CultureInvariant);

    /// <summary>Месяц из аргумента «ГГГГ-ММ»; null — аргумента нет. Неверный формат — ToolArgumentException с примером.</summary>
    public static (int Year, int Month)? Parse(ToolArgs args, string name)
    {
        var text = args.Str(name);
        if (text is null)
        {
            return null;
        }

        var match = Format.Match(text);
        if (match.Success)
        {
            var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (year is >= 2000 and <= 2100 && month is >= 1 and <= 12)
            {
                return (year, month);
            }
        }

        throw new ToolArgumentException($"Аргумент {name} — месяц в формате ГГГГ-ММ, например {DateTime.Today:yyyy-MM}; получено «{text}».");
    }

    public static (int Year, int Month) Shift((int Year, int Month) month, int months)
    {
        var date = new DateOnly(month.Year, month.Month, 1).AddMonths(months);
        return (date.Year, date.Month);
    }

    /// <summary>Номер месяца для сравнения и длины отрезка.</summary>
    public static int Index((int Year, int Month) month) => month.Year * 12 + month.Month - 1;

    /// <summary>Месяцев в отрезке [from; to] (меньше 1 — начало позже конца).</summary>
    public static int Length((int Year, int Month) from, (int Year, int Month) to) => Index(to) - Index(from) + 1;

    public static string Key((int Year, int Month) month) => $"{month.Year:D4}-{month.Month:D2}";

    /// <summary>«сентябрь 2026», «июль — сентябрь 2026», «ноябрь 2025 — январь 2026».</summary>
    public static string Label((int Year, int Month) from, (int Year, int Month) to) =>
        from == to ? $"{DataTool.MonthName(from.Month)} {from.Year}"
        : from.Year == to.Year ? $"{DataTool.MonthName(from.Month)} — {DataTool.MonthName(to.Month)} {to.Year}"
        : $"{DataTool.MonthName(from.Month)} {from.Year} — {DataTool.MonthName(to.Month)} {to.Year}";
}

/// <summary>
/// «Продажи → Продажи по SKU»: вторичка по SKU за отрезок месяцев — тот же SkuSalesService, что строит страницу (итоги, ABC, динамика
/// кварталов, регионы и точки товара). Регион и категории проверяются по списку страницы за тот же период.
/// </summary>
internal sealed class GetSkuSalesTool(SkuSalesService skuSales, SalesDataLoader loader) : DataTool
{
    /// <summary>Месяцев по умолчанию (последние, по месяц последних данных).</summary>
    internal const int DefaultMonths = 3;

    /// <summary>«Падающий» SKU: кг в день последнего квартала периода ниже первого на 10% и больше.</summary>
    internal const decimal FallingThreshold = -0.10m;

    private const int DefaultLimit = 30;
    private const int MaxLimit = 40;
    private const int MaxListed = 8;

    /// <summary>Найдено столько SKU или меньше — у каждого разбивка по регионам.</summary>
    private const int FoundWithRegions = 5;

    public override string Name => "get_sku_sales";
    public override string Title => "Продажи по SKU за период";
    public override string Source => KnowledgeSources.SalesSku;

    public override string Description =>
        "Продажи по SKU за период из месяцев (вторичка, как страница «Продажи → Продажи по SKU»): итоги — кг, выручка в сумах, цена за кг, SKU с продажами, регионы; " +
        "ABC по весу (A — первые 80% кг, B — до 95%, C — остальные), концентрация (главный SKU, доли топ-5/10/25), кг по месяцам, категории, регионы; " +
        "топ SKU по весу — кг, выручка, доля, ABC, точки (сколько ТТ купили SKU за период), регионов с продажами, динамика (кг в день последнего квартала периода к первому); " +
        "«новые» SKU (в первом квартале продаж не было) и падающие (−10% и больше). По умолчанию — последние 3 месяца по месяц последних данных " +
        "(если они в одном квартале — 4, чтобы считалась динамика), вся страна, все SKU. Фильтры: регион, категории, ТОП, группа ABC, поиск SKU по названию или коду.";

    public override string UsageHint =>
        "get_sku_sales — для вопросов о товарах за несколько месяцев: какие SKU растут или падают, что появилось нового, ABC-анализ, концентрация ассортимента, сравнение периодов. " +
        "Период — from/to в формате ГГГГ-ММ или months (последние N месяцев). Один товар за месяц с разбивкой по регионам и статусом — find_products.";

    public override JsonElement InputSchema { get; } = Schema(
        ("from", new { type = "string", description = "Первый месяц периода, ГГГГ-ММ (например 2026-07). Без from — последние months месяцев по to." }),
        ("to", new { type = "string", description = "Последний месяц периода, ГГГГ-ММ. Без to — месяц последних данных (идущий, неполный)." }),
        ("months", new { type = "integer", description = "Сколько последних месяцев взять, если from не задан: 1–24, по умолчанию 3." }),
        ("region", new { type = "string", description = "Регион: id или название (например «Самарканд»). Без региона — вся страна." }),
        ("category", new { type = "string", description = "Категории отчёта через запятую, например «Кекс,Помадка». Без категории — все." }),
        ("query", new { type = "string", description = "Поиск SKU по части названия или коду (кириллицей или латиницей): вернутся только подходящие SKU." }),
        ("top", new { type = "string", description = "Список ТОП: all — все SKU (по умолчанию), only — только ТОП-товары, not — кроме ТОПа." }),
        ("abc", new { type = "string", description = "Только группа ABC: A, B или C. Без abc — все группы." }),
        ("limit", new { type = "integer", description = $"Сколько SKU вернуть в топе по весу: 1–{MaxLimit}, по умолчанию {DefaultLimit}." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        // Аргументы — до чтения данных: ошибка формата не стоит расчёта отрезка.
        var fromArg = MonthRange.Parse(args, "from");
        var toArg = MonthRange.Parse(args, "to");
        var months = args.Int("months", 1, SkuSalesQuery.MaxMonths);
        var top = TopOf(args.Str("top"));
        var abc = AbcOf(args.Str("abc"));
        var limit = args.Int("limit", 1, MaxLimit) ?? DefaultLimit;
        var query = args.Str("query");

        var lastData = await loader.LastDataAsync(ct);
        if (lastData is null)
        {
            return Data(new { Note = "Продаж вторички в OneBase нет — синхронизация с Linko ещё не выполнялась." },
                new DataSource("OneBase → Продажи → Продажи по SKU", null, "/sales/sku-sales"));
        }

        var (from, to) = PeriodOf(fromArg, toArg, months, lastData.Value);

        // Без фильтров: список регионов и категорий за период — для проверки аргументов (отрезок кэшируется, второй расчёт — только страница).
        var all = await ViewAsync(from, to, null, null, null, null, ct);
        var region = FindRegion(all.Regions, args.Str("region"));
        var categories = FindCategories(all.Categories.Select(c => c.Name).ToList(), args.Str("category"));
        var view = region is null && top == SkuSalesTop.All && abc is null && categories.Count == 0
            ? all
            : await ViewAsync(from, to, region?.Id, top, abc, categories.Count == 0 ? null : string.Join(",", categories), ct);

        return Answer(view, MonthRange.Label(from, to), query, limit, args);
    }

    /// <summary>Ответ модели по странице за период: итоги, ABC, месяцы, категории, регионы, топ SKU, новые и падающие.</summary>
    internal static ToolResult Answer(SkuSalesView view, string label, string? query, int limit, ToolArgs args)
    {
        var national = view.RegionId is null;
        var rows = query is null ? view.Rows : Search(view.Rows, query);
        if (query is not null && rows.Count == 0)
        {
            throw new ToolArgumentException($"SKU по запросу «{query}» с продажами за {label} ({view.RegionName ?? "вся страна"}) нет. " +
                "Проверь название (можно кириллицей или латиницей) или найди товар через find_products.");
        }

        var dynamics = view.Quarters is not null;
        var monthly = !dynamics && view.Months.Count > 1; // динамики кварталов нет — кг по месяцам в строках
        var matrix = view.RegionMatrix.Rows.ToDictionary(r => r.ProductId);

        object Sku(SkuSalesRow r) => new
        {
            r.Rank,
            r.Name,
            Code = query is null ? null : r.Code, // найденным — код, чтобы различить похожие названия
            r.Category,
            Top = r.IsTop ? true : (bool?)null,
            Kg = R(r.Kg),
            RevenueSum = R(r.Revenue),
            SharePct = Pct(r.Share),
            r.Abc,
            Outlets = r.Akb,
            Regions = national ? r.Regions : (int?)null,
            DynamicsPct = Pct(r.Dynamics),
            New = r.IsNew ? true : (bool?)null,
            MonthsKg = monthly ? r.Months.Select(m => R(m)) : null,
            ByRegion = query is not null && rows.Count <= FoundWithRegions && matrix.TryGetValue(r.ProductId, out var regionRow) ? RegionsOf(view, regionRow) : null,
        };

        var allClasses = view.Abc is not (AbcGroups.A or AbcGroups.B or AbcGroups.C);
        decimal ClassKg(string group) => view.Rows.Where(r => r.Abc == group).Sum(r => r.Kg);

        return Data(new
        {
            Period = new
            {
                view.From,
                view.To,
                Name = label,
                DataThrough = view.DataThrough?.ToString("yyyy-MM-dd"),
                Months = view.Months.Count,
                LastMonthComplete = !view.Months[^1].Partial,
                Dynamics = view.Quarters is { } q ? $"кг в день: {q.Last} к {q.First}" : null,
            },
            Scope = view.RegionName ?? "Вся страна",
            Filters = new
            {
                Top = view.Top == SkuSalesTop.All ? null : view.Top == SkuSalesTop.Only ? "только ТОП" : "кроме ТОПа",
                Abc = allClasses ? null : view.Abc,
                Categories = view.SelectedCategories.Count == 0 ? null : view.SelectedCategories,
                Query = query,
            },
            Totals = new
            {
                Kg = R(view.Totals.Kg),
                RevenueSum = R(view.Totals.Revenue),
                PricePerKgSum = R(view.Totals.PricePerKg),
                SkusSold = view.Totals.Skus,
                SkusA = view.Totals.SkusA,
                Regions = national ? view.Totals.Regions : (int?)null,
                Top5SharePct = Pct(view.Totals.Top5Share),
                ShareOfAllCategoriesPct = view.SelectedCategories.Count > 0 ? Pct(view.Totals.ShareOfAll) : null,
                SkusOutsideReport = view.OutsideReport > 0 ? view.OutsideReport : (int?)null,
            },
            Abc = new[] { AbcGroups.A, AbcGroups.B, AbcGroups.C }.Select(g => new
            {
                Group = g,
                Skus = g == AbcGroups.A ? view.Facts.SkusA : g == AbcGroups.B ? view.Facts.SkusB : view.Facts.SkusC,
                Kg = allClasses ? R(ClassKg(g)) : (decimal?)null,
                KgSharePct = allClasses ? Pct(SalesMath.Ratio(ClassKg(g), view.Totals.Kg)) : null,
            }),
            Concentration = new
            {
                TopSku = view.Facts.TopName,
                TopSkuKgSharePct = Pct(view.Facts.TopKgShare),
                TopSkuRevenueSharePct = Pct(view.Facts.TopRevenueShare),
                Top5SharePct = Pct(view.Facts.Top5Share),
                Top10SharePct = Pct(view.Facts.Top10Share),
                Top25SharePct = Pct(view.Facts.Top25Share),
            },
            Months = view.MonthTotals.Select(m => new
            {
                Month = MonthRange.Key((m.Year, m.Month)),
                Kg = R(m.Kg),
                KgPerDay = R(m.KgPerDay, 1),
                Partial = m.Partial ? true : (bool?)null,
            }),
            Categories = view.Categories.Where(c => c.Kg != 0 || c.Revenue != 0 || c.Selected).Select(c => new
            {
                c.Name,
                Kg = R(c.Kg),
                RevenueSum = R(c.Revenue),
                KgSharePct = Pct(c.KgShare),
                c.Skus,
                PricePerKgSum = R(c.PricePerKg),
            }),
            ByRegion = national && query is null
                ? view.Regions.Select((r, i) => (r.Name, Kg: view.RegionMatrix.TotalKg[i], Revenue: view.RegionMatrix.TotalRevenue[i]))
                    .Where(x => x.Kg != 0 || x.Revenue != 0)
                    .Select(x => new { x.Name, Kg = R(x.Kg), RevenueSum = R(x.Revenue), SharePct = Pct(SalesMath.Ratio(x.Kg, view.RegionMatrix.Kg)) })
                : null,
            Found = query is null ? (int?)null : rows.Count,
            Skus = rows.TakeTop(args, limit, "skus").Select(Sku),
            New = dynamics
                ? rows.Where(r => r.IsNew).OrderByDescending(r => r.LastKgPerDay).TakeTop(args, MaxListed, "new").Select(r => new
                {
                    r.Name,
                    r.Category,
                    LastKgPerDay = R(r.LastKgPerDay, 1),
                    Outlets = r.Akb,
                    Regions = national ? r.Regions : (int?)null,
                })
                : null,
            Falling = dynamics
                ? rows.Where(r => r.Dynamics <= FallingThreshold).OrderByDescending(r => (r.FirstKgPerDay ?? 0) - (r.LastKgPerDay ?? 0)).TakeTop(args, MaxListed, "falling").Select(r => new
                {
                    r.Name,
                    r.Abc,
                    FirstKgPerDay = R(r.FirstKgPerDay, 1),
                    LastKgPerDay = R(r.LastKgPerDay, 1),
                    DynamicsPct = Pct(r.Dynamics),
                })
                : null,
            FallingCount = dynamics ? rows.Count(r => r.Dynamics <= FallingThreshold) : (int?)null,
            Note = "Вторичка нетто (заказы − возвраты), без «Завода» (экспорт и опт); сум, кг. ABC — по весу в охвате и фильтрах. " +
                "Outlets — ТТ, купившие SKU за период; Regions — регионов с продажами. " +
                (dynamics
                    ? "DynamicsPct — кг в день последнего квартала к первому; New — в первом квартале продаж не было; Falling — минус 10% и больше, по потере кг в день."
                    : "Период в одном квартале — динамики, новых и падающих нет (нужно два квартала, например months=4); MonthsKg — кг по месяцам.") +
                (view.Months[^1].Partial ? " Последний месяц неполный — сравнивай кг в день." : string.Empty),
        }, new DataSource(
            national ? "OneBase → Продажи → Продажи по SKU" : $"OneBase → Продажи → Продажи по SKU → {view.RegionName}",
            label + (view.DataThrough is { } d ? $", данные по {d:dd.MM.yyyy}" : string.Empty),
            Href(view)));
    }

    /// <summary>
    /// Период: to — месяц последних данных, from — months месяцев до to включительно (по умолчанию 3). Три месяца по умолчанию в одном
    /// квартале — четыре: динамика страницы сравнивает первый и последний квартал периода, у одного квартала её нет.
    /// </summary>
    internal static ((int Year, int Month) From, (int Year, int Month) To) PeriodOf(
        (int Year, int Month)? from, (int Year, int Month)? to, int? months, DateOnly lastData)
    {
        var end = to ?? (lastData.Year, lastData.Month);
        if (from is { } start)
        {
            return (start, end);
        }

        var begin = MonthRange.Shift(end, -((months ?? DefaultMonths) - 1));
        if (months is null && Quarter(begin) == Quarter(end))
        {
            begin = MonthRange.Shift(begin, -1);
        }

        return (begin, end);
    }

    private static (int, int) Quarter((int Year, int Month) month) => (month.Year, (month.Month - 1) / 3);

    /// <summary>Фильтр ТОП страницы: all | only | not.</summary>
    internal static string TopOf(string? value) => value?.ToLowerInvariant() switch
    {
        null or SkuSalesTop.All => SkuSalesTop.All,
        SkuSalesTop.Only => SkuSalesTop.Only,
        SkuSalesTop.Not => SkuSalesTop.Not,
        _ => throw new ToolArgumentException($"Аргумент top — all (все SKU), only (только ТОП) или not (кроме ТОПа); получено «{value}»."),
    };

    /// <summary>Группа ABC: A, B, C (кириллица тоже); all или пусто — все (null).</summary>
    internal static string? AbcOf(string? value) => value?.ToUpperInvariant() switch
    {
        null or "ALL" => null,
        "A" or "А" => AbcGroups.A,
        "B" or "В" => AbcGroups.B,
        "C" or "С" => AbcGroups.C,
        _ => throw new ToolArgumentException($"Аргумент abc — A, B или C (без abc — все группы); получено «{value}»."),
    };

    /// <summary>Регион по id или названию из списка страницы за период; null — вся страна. Неизвестный — ошибка со списком регионов.</summary>
    internal static SkuSalesRegionRef? FindRegion(IReadOnlyList<SkuSalesRegionRef> regions, string? region)
    {
        if (region is null)
        {
            return null;
        }

        return regions.FirstOrDefault(r => string.Equals(r.Id, region, StringComparison.OrdinalIgnoreCase))
            ?? regions.FirstOrDefault(r => string.Equals(r.Name, region, StringComparison.OrdinalIgnoreCase))
            ?? regions.FirstOrDefault(r => r.Name.Contains(region, StringComparison.OrdinalIgnoreCase) || region.Contains(r.Name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ToolArgumentException($"Региона «{region}» нет. Регионы: {string.Join(", ", regions.Select(r => r.Name))}.");
    }

    /// <summary>
    /// Категории отчёта через запятую → названия категорий страницы (без учёта регистра, кириллица или латиница). Неизвестная — ошибка
    /// со списком категорий с продажами за период.
    /// </summary>
    internal static IReadOnlyList<string> FindCategories(IReadOnlyList<string> available, string? categories)
    {
        if (categories is null)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var wanted in categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = available.FirstOrDefault(c => string.Equals(c, wanted, StringComparison.OrdinalIgnoreCase))
                ?? available.Select(c => (Name: c, Score: NameMatch.Score(c, wanted))).Where(x => x.Score > 0).OrderByDescending(x => x.Score).Select(x => x.Name).FirstOrDefault()
                ?? throw new ToolArgumentException($"Категории «{wanted}» в продажах за период нет. Категории: {string.Join(", ", available)}.");
            if (!result.Contains(match, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(match);
            }
        }

        return result;
    }

    /// <summary>SKU по запросу: название или код; остаются лучшие совпадения (как в find_products), в порядке рейтинга по весу.</summary>
    internal static IReadOnlyList<SkuSalesRow> Search(IReadOnlyList<SkuSalesRow> rows, string query)
    {
        var scored = rows.Select(r => (Row: r, Score: Math.Max(NameMatch.Score(r.Name, query), r.Code is null ? 0 : NameMatch.Score(r.Code, query))))
            .Where(x => x.Score > 0)
            .ToList();
        var best = scored.Count == 0 ? 0 : scored.Max(x => x.Score);
        return scored.Where(x => x.Score == best).Select(x => x.Row).ToList();
    }

    /// <summary>Регионы найденного SKU по всей стране: кг, выручка, точки — по убыванию веса.</summary>
    private static IEnumerable<object> RegionsOf(SkuSalesView view, SkuSalesRegionRow row) =>
        view.Regions.Select((r, i) => (r.Name, Kg: row.RegionKg[i], Revenue: row.RegionRevenue[i], Akb: row.RegionAkb[i]))
            .Where(x => x.Kg != 0 || x.Revenue != 0)
            .OrderByDescending(x => x.Kg)
            .Select(x => new { Region = x.Name, Kg = R(x.Kg), RevenueSum = R(x.Revenue), Outlets = x.Akb });

    private async Task<SkuSalesView> ViewAsync((int Year, int Month) from, (int Year, int Month) to, string? region, string? top, string? abc, string? cat, CancellationToken ct)
    {
        var result = await skuSales.GetAsync(MonthRange.Key(from), MonthRange.Key(to), region, top, abc, cat, ct);
        return result.View ?? throw new ToolArgumentException(result.Error ?? "«Продажи по SKU» за этот период не построены.");
    }

    /// <summary>Адрес страницы с теми же фильтрами.</summary>
    private static string Href(SkuSalesView view)
    {
        var query = new List<string> { $"from={view.From}", $"to={view.To}" };
        if (view.RegionId is { } region)
        {
            query.Add($"region={region}");
        }

        if (view.Top != SkuSalesTop.All)
        {
            query.Add($"top={view.Top}");
        }

        if (view.Abc is AbcGroups.A or AbcGroups.B or AbcGroups.C)
        {
            query.Add($"abc={view.Abc}");
        }

        if (view.SelectedCategories.Count > 0)
        {
            query.Add($"cat={Uri.EscapeDataString(string.Join(",", view.SelectedCategories))}");
        }

        return $"/sales/sku-sales?{string.Join("&", query)}";
    }
}

/// <summary>
/// Экспорт за месяц: по странам — «Первичка → Экспорт» (PrimaryService.GetExportAsync: заказы филиала «Завод» экспортным точкам),
/// и продажи филиала «Завод» — «Продажи → Экспорт и опт» (SalesAnalytics.CachedExport). Оба — отдельно от вторички республики.
/// </summary>
internal sealed class GetExportSalesTool(SalesDataLoader loader, PrimaryService primary) : SalesTool(loader)
{
    private const int MaxCountries = 20;
    private const int MaxBuyers = 12;
    private const int MaxAgents = 8;
    private const int MaxProducts = 8;
    private const int MaxOutletNames = 4;

    public override string Name => "get_export_sales";
    public override string Title => "Экспорт: страны и филиал «Завод»";
    public override string Source => KnowledgeSources.SalesExport;

    public override string Description =>
        "Экспорт за месяц (вес — кг, суммы — сум). По странам — отгрузки завода на экспорт («Первичка → Экспорт»: заказы филиала «Завод» экспортным точкам): " +
        "кг, сумма, доля, точки страны, прошлый месяц и изменение, с начала года; итог экспорта, прогноз, по месяцам года, категории. " +
        "Отдельно — продажи филиала «Завод» («Продажи → Экспорт и опт»: экспорт и крупный опт): кг, выручка, заказы, точки, прогноз, к прошлому месяцу, " +
        "покупатели (со страной, если точка экспортная), агенты, категории, товары, суммы в другой валюте. Плана экспорта в OneBase нет. Во вторичку республики экспорт не входит.";

    public override string UsageHint =>
        "get_export_sales — для вопросов об экспорте: страны, экспортные точки, динамика к прошлому месяцу, покупатели филиала «Завод». " +
        "get_sales показывает экспорт и опт только одной строкой, get_primary_shipments — экспорт с начала года одной цифрой.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var sales = analytics.CachedExport();
        var p = sales.Period;
        var ship = await primary.GetExportAsync(p.Year, p.Month, ct);

        // Прошлый месяц: в том же году — из месяцев года, у января — декабрь прошлого года.
        var previous = p.Month == 1 ? await primary.GetExportAsync(p.Year - 1, 12, ct) : null;
        return Answer(sales, ship, previous, args);
    }

    /// <summary>Ответ модели: страны и итоги «Первички → Экспорт» (previous — декабрь прошлого года для января) и «Продажи → Экспорт и опт».</summary>
    internal static ToolResult Answer(ExportView sales, PrimaryView ship, PrimaryView? previous, ToolArgs args)
    {
        var p = sales.Period;
        var prevByCountry = previous?.Dealers.ToDictionary(r => r.Id, r => r.Month) ?? [];
        PrimaryAmounts PrevOf(PrimaryRow r) => p.Month > 1 ? r.Months[p.Month - 2] : prevByCountry.GetValueOrDefault(r.Id) ?? PrimaryAmounts.NoBoxes;
        var prevTotal = p.Month > 1 ? ship.Months[p.Month - 2] : previous?.MonthTotal ?? PrimaryAmounts.NoBoxes;

        // Страны прошлого декабря без отгрузок в этом году — тоже строки (в этом месяце ноль).
        var countries = ship.Dealers.ToList();
        if (previous is not null)
        {
            countries.AddRange(previous.Dealers.Where(r => r.Month.Kg != 0 && countries.All(c => c.Id != r.Id))
                .Select(r => r with { Month = PrimaryAmounts.NoBoxes, Ytd = PrimaryAmounts.NoBoxes, MonthShare = new PrimaryShares(null, null, null, null), Months = [] }));
        }

        var countryOfOutlet = CountryOfOutlet(ship.Dealers.Concat(previous?.Dealers ?? []));
        var running = ship.Running;
        var s = sales.Summary;

        return Data(new
        {
            Year = p.Year,
            Month = MonthName(p.Month),
            Shipments = new
            {
                Source = "Первичка → Экспорт",
                DataThrough = ship.DataThrough?.ToString("yyyy-MM-dd"),
                MonthComplete = !running,
                Kg = R(ship.MonthTotal.Kg),
                Sum = R(ship.MonthTotal.SumFactory),
                ForecastKg = running ? R(ship.ForecastKg) : null,
                PrevMonthKg = R(prevTotal.Kg),
                PrevMonthSum = R(prevTotal.SumFactory),
                VsPrevMonthPct = running ? null : Pct(SalesMath.Delta(prevTotal.Kg, ship.MonthTotal.Kg)),
                ForecastVsPrevMonthPct = running ? Pct(SalesMath.Delta(prevTotal.Kg, ship.ForecastKg ?? ship.MonthTotal.Kg)) : null,
                Orders = ship.MonthTransfers,
                Countries = ship.MonthCounterparties,
                PlanKg = (decimal?)null,
            },
            Countries = countries
                .Where(r => r.Month.Kg != 0 || r.Month.SumFactory != 0 || PrevOf(r).Kg != 0)
                .OrderByDescending(r => r.Month.Kg).ThenByDescending(r => PrevOf(r).Kg)
                .TakeTop(args, MaxCountries, "countries")
                .Select(r =>
                {
                    var prev = PrevOf(r);
                    var outlets = OutletsOf(r.Sub);
                    return new
                    {
                        Country = r.Name,
                        Kg = R(r.Month.Kg),
                        Sum = R(r.Month.SumFactory),
                        SharePct = Pct(r.MonthShare.Kg),
                        PrevMonthKg = R(prev.Kg),
                        VsPrevMonthPct = running ? null : Pct(SalesMath.Delta(prev.Kg, r.Month.Kg)),
                        YtdKg = R(r.Ytd.Kg),
                        YtdSum = R(r.Ytd.SumFactory),
                        OutletsYtd = outlets.Count,
                        OutletNames = outlets.Take(MaxOutletNames),
                    };
                }),
            ByMonth = ship.MonthsWithData.Select(m => new { Month = MonthName(m), Kg = R(ship.Months[m - 1].Kg), Sum = R(ship.Months[m - 1].SumFactory) }),
            YearToDate = new
            {
                Kg = R(ship.Ytd.Kg),
                Sum = R(ship.Ytd.SumFactory),
                PricePerKgSum = R(ship.YtdPricePerKg),
                Countries = ship.Export.Counterparties,
                Orders = ship.Export.Transfers,
                ReturnsKg = R(ship.YtdReturnsKg),
            },
            Categories = ship.Categories.Where(c => c.Month.Kg != 0 || c.Month.SumFactory != 0).Select(c => new
            {
                c.Name,
                Kg = R(c.Month.Kg),
                Sum = R(c.Month.SumFactory),
                SharePct = Pct(c.MonthShare.Kg),
            }),
            ShipmentNotes = ship.Notes is { Count: > 0 } notes ? notes : null,
            FactoryBranch = s is null
                ? null
                : new
                {
                    Source = "Продажи → Экспорт и опт",
                    DataThrough = p.DataThrough.ToString("yyyy-MM-dd"),
                    MonthComplete = p.Closed,
                    FactKg = R(s.FactKg),
                    RevenueSum = R(s.Revenue),
                    s.Orders,
                    Outlets = s.Akb,
                    ForecastKg = p.Closed ? null : R(s.ForecastKg),
                    PrevMonthKg = R(s.PrevMonthKg),
                    VsPrevMonthPct = Pct(VsPrev(s.PrevMonthKg, s.FactKg, s.VsPrevMonth, p.Closed)),
                    BuyersWithSales = sales.Markets.Count(m => m.Kg != 0),
                    Buyers = sales.Markets.TakeTop(args, MaxBuyers, "buyers").Select(m => new
                    {
                        m.Name,
                        Country = countryOfOutlet.GetValueOrDefault(m.Name.Trim()),
                        Kg = R(m.Kg),
                        RevenueSum = R(m.Revenue),
                        m.Orders,
                        PrevMonthKg = R(m.PrevMonthKg),
                        VsPrevMonthPct = p.Closed ? Pct(SalesMath.Delta(m.PrevMonthKg, m.Kg)) : null,
                    }),
                    Agents = sales.Agents.TakeTop(args, MaxAgents, "agents").Select(a => new { a.Name, Kg = R(a.Kg), RevenueSum = R(a.Revenue), Outlets = a.Markets }),
                    Categories = sales.Categories.Where(c => c.FactKg != 0 || c.PrevMonthKg != 0).Select(c => new
                    {
                        c.Name,
                        Kg = R(c.FactKg),
                        RevenueSum = R(c.Revenue),
                        WeightSharePct = Pct(c.WeightShare),
                        PrevMonthKg = R(c.PrevMonthKg),
                        VsPrevMonthPct = Pct(VsPrev(c.PrevMonthKg, c.FactKg, c.VsPrevMonth, p.Closed)),
                    }),
                    TopProducts = sales.Products.OrderByDescending(x => x.Kg).TakeTop(args, MaxProducts, "topProducts")
                        .Select(x => new { x.Name, x.Category, Kg = R(x.Kg), RevenueSum = R(x.Revenue) }),
                    OtherCurrency = sales.OtherCurrency.Count == 0 ? null : sales.OtherCurrency.Select(c => new { c.Currency, Amount = R(c.Amount), c.Orders }),
                },
            Note = "Shipments и Countries — «Первичка → Экспорт»: заказы филиала «Завод» экспортным точкам (тип EXPORT), только доставленные, минус возвраты, " +
                "по дате приёмки; страна — по названию или адресу точки; Sum — сумма заказов в сумах (заказы в другой валюте дают вес без суммы); " +
                "OutletNames — точки страны с отгрузками с начала года. Плана экспорта в OneBase нет — выполнение не считается. " +
                "FactoryBranch — «Продажи → Экспорт и опт»: все продажи филиала «Завод» (экспорт и крупный опт) по правилам вторички, поэтому кг обычно больше, чем у экспорта; " +
                "Country у покупателя — если это экспортная точка. " +
                (running
                    ? "Месяц идёт: сравнение с прошлым месяцем — по прогнозу (ForecastVsPrevMonthPct), у стран — только кг прошлого месяца."
                    : "Месяц закрыт: VsPrevMonthPct — факт к факту прошлого месяца.") +
                (s is null ? " Продаж филиала «Завод» за месяц и прошлый месяц нет." : string.Empty),
        },
        new DataSource("OneBase → Продажи → Первичка → Экспорт",
            $"{MonthName(ship.Month)} {ship.Year}" + (ship.DataThrough is { } d ? $", данные по {d:dd.MM.yyyy}" : string.Empty),
            $"/sales/primary/export?{Query(ship.Year, ship.Month)}"),
        new DataSource("OneBase → Продажи → Экспорт и опт", PeriodText(p), $"/sales/export?{Query(p.Year, p.Month)}"));
    }

    /// <summary>К прошлому месяцу: у закрытого месяца — факт к факту, у идущего — как на странице (прогноз к факту прошлого месяца).</summary>
    internal static decimal? VsPrev(decimal prevKg, decimal factKg, decimal? running, bool closed) =>
        closed ? SalesMath.Delta(prevKg, factKg) : running;

    /// <summary>Точки страны — подпись строки «Первички → Экспорт»: названия через запятую.</summary>
    internal static IReadOnlyList<string> OutletsOf(string? sub) =>
        sub is null ? [] : sub.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Название экспортной точки → страна (по строкам «Первички → Экспорт»).</summary>
    internal static IReadOnlyDictionary<string, string> CountryOfOutlet(IEnumerable<PrimaryRow> countries)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in countries)
        {
            foreach (var outlet in OutletsOf(row.Sub))
            {
                map.TryAdd(outlet, row.Name);
            }
        }

        return map;
    }
}

/// <summary>
/// «Продажи → P&L дилеров»: прибыль завода на дилере — PnlService (данные sales.PnlValues, загружаются только через AI Data Manager;
/// итоги — по формулам PnlMath). Как у страницы, нужны права «Финансы» (RequiredPermission) и «Продажи» (проверка в RunAsync).
/// </summary>
internal sealed class GetDealerPnlTool(PnlService pnl, IUserPermissions permissions) : DataTool
{
    private const int MaxMonths = 24;
    private const int MaxDealers = 40;

    /// <summary>Найдено столько дилеров или меньше — у каждого все строки P&L и (в периоде) помесячно.</summary>
    private const int DetailedDealers = 3;

    private const string LoadHelp =
        "Данные P&L попадают в OneBase только через AI Data Manager (страница /data-manager): база продаж (сумма дилера и завода), реестр «Прочие расходы», " +
        "журнал логистики или готовая таблица P&L; загрузить может администратор с правом «AI Data Manager». Не придумывай цифры P&L.";

    public override string Name => "get_dealer_pnl";
    public override string Title => "P&L дилеров";
    public override string Source => KnowledgeSources.DealerPnl;
    public override string RequiredPermission => Permissions.FinanceRead;

    public override string Description =>
        "P&L дилеров (страница «Продажи → P&L дилеров») — прибыль завода на дилере, все суммы в сумах: продажи (выручка), себестоимость, валовая прибыль и маржа, " +
        "расходы (отдел продаж и маркетинга, ретробонусы, доставка и склад, административные, прочие, амортизация), прибыль после продаж и ROS (рентабельность продаж), " +
        "EBIT, чистая прибыль EAT (если загружены банковские услуги и налоги) — итог, по РМ и по дилерам с изменением к прошлому месяцу. " +
        "За месяц (year+month; без них — последний месяц с продажами и расходами, как на странице) или за период (from/to ГГГГ-ММ, либо year без month — " +
        "сумма загруженных месяцев и помесячная динамика). dealer — один дилер подробно. Если P&L за месяц не загружен — ответ скажет об этом (загрузка — через AI Data Manager).";

    public override string UsageHint =>
        "get_dealer_pnl — для вопросов о прибыльности дилеров, марже, расходах завода на дилера, убыточных дилерах. Если P&L за месяц не загружен — так и скажи " +
        "и не придумывай цифры: данные P&L загружают только через AI Data Manager.";

    public override JsonElement InputSchema { get; } = Schema(
        ("year", new { type = "integer", description = "Год, например 2026. С month — P&L месяца; без month — весь год (сумма загруженных месяцев)." }),
        ("month", new { type = "integer", description = "Месяц 1–12. Без year и month — последний месяц с продажами и расходами." }),
        ("from", new { type = "string", description = "Начало периода, ГГГГ-ММ: P&L за несколько месяцев (сумма и помесячно). Без to — по последний загруженный месяц." }),
        ("to", new { type = "string", description = "Конец периода, ГГГГ-ММ." }),
        ("dealer", new { type = "string", description = "Дилер: имя или часть имени, или регион (например «Самарканд») — только он, со всеми строками P&L." }));

    private sealed record Entry((int Year, int Month) Month, PnlGroupView Group, PnlDealerView Dealer);

    private sealed record DealerTotal(string Key, string Name, string? Region, PnlGroupView Group, IReadOnlyDictionary<string, decimal?> Values, IReadOnlyList<Entry> Months);

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        // Страница P&L — с правами «Продажи» и «Финансы»; «Финансы» проверяет Tool Registry (RequiredPermission), «Продажи» — здесь.
        if (context.UserId is { } userId && !(await permissions.GetAsync(userId, ct)).Contains(Permissions.SalesRead))
        {
            return ToolResult.Error("P&L дилеров открывается только с правами «Продажи» и «Финансы» — у пользователя нет права на продажи. Не используй и не угадывай эти данные.");
        }

        var year = args.Int("year", 2000, 2100);
        var month = args.Int("month", 1, 12);
        var from = MonthRange.Parse(args, "from");
        var to = MonthRange.Parse(args, "to");
        var dealer = args.Str("dealer");
        if (from is { } f && to is { } t && MonthRange.Length(f, t) is var length && (length < 1 || length > MaxMonths))
        {
            throw new ToolArgumentException(length < 1
                ? $"Начало периода ({MonthRange.Key(f)}) позже конца ({MonthRange.Key(t)})."
                : $"Период — не длиннее {MaxMonths} месяцев (выбрано {length}).");
        }

        var months = await pnl.MonthsAsync(ct);
        if (months.Count == 0)
        {
            return NotLoaded("P&L дилеров в OneBase ещё не загружен.", months);
        }

        var latest = (months[0].Year, months[0].Month);
        if (from is not null || to is not null)
        {
            // Только from — по последний загруженный месяц (from позже него — период из одного from: «не загружен»); только to — один месяц.
            var start = from ?? to!.Value;
            return await PeriodAsync(start, to ?? (MonthRange.Index(latest) >= MonthRange.Index(start) ? latest : start), months, dealer, args, ct);
        }

        if (year is { } y && month is null)
        {
            return await PeriodAsync((y, 1), (y, 12), months, dealer, args, ct);
        }

        // Месяц без года — последний загруженный год с этим месяцем.
        var selected = month is { } m ? (Year: year ?? months.FirstOrDefault(x => x.Month == m)?.Year ?? DateTime.Today.Year, Month: m) : ((int Year, int Month)?)null;
        if (selected is { } sel && !months.Any(x => x.Year == sel.Year && x.Month == sel.Month))
        {
            return NotLoaded($"P&L дилеров за {MonthName(sel.Month)} {sel.Year} в OneBase не загружен.", months);
        }

        return await MonthAsync(selected, months, dealer, args, ct);
    }

    private async Task<ToolResult> MonthAsync((int Year, int Month)? selected, IReadOnlyList<PnlMonthRef> months, string? dealer, ToolArgs args, CancellationToken ct)
    {
        var view = await pnl.GetAsync(selected?.Year, selected?.Month, ct);
        if (view.Year is not { } year || view.Month is not { } month)
        {
            return NotLoaded("P&L дилеров в OneBase ещё не загружен.", months);
        }

        var (py, pm) = MonthRange.Shift((year, month), -1);
        var prev = months.Any(x => x.Year == py && x.Month == pm) ? await pnl.GetAsync(py, pm, ct) : null;

        // Один найденный дилер — и его месяцы года (карточка дилера страницы).
        PnlDealerCard? card = null;
        if (dealer is not null && MatchDealers(EntriesOf(view), e => e.Dealer.Name, e => e.Dealer.Region, dealer, MonthRange.Label((year, month), (year, month))) is [var only])
        {
            card = await pnl.DealerAsync(only.Dealer.Name, year, month, ct);
        }

        return MonthAnswer(view, prev, months, dealer, card, args);
    }

    private static List<Entry> EntriesOf(PnlView view) =>
        view.Groups.SelectMany(g => g.Dealers.Select(d => new Entry((view.Year!.Value, view.Month!.Value), g, d))).ToList();

    /// <summary>Ответ модели за месяц: итог, РМ, дилеры с изменением к прошлому месяцу (prev), предупреждения о неполной загрузке.</summary>
    internal static ToolResult MonthAnswer(PnlView view, PnlView? prev, IReadOnlyList<PnlMonthRef> months, string? dealer, PnlDealerCard? card, ToolArgs args)
    {
        var (year, month) = (view.Year!.Value, view.Month!.Value);
        var current = months.FirstOrDefault(x => x.Year == year && x.Month == month) ?? new PnlMonthRef(year, month, true, !view.NoExpenses);
        var label = MonthRange.Label((year, month), (year, month));
        var prevRef = prev is null ? null : months.FirstOrDefault(x => x.Year == prev.Year && x.Month == prev.Month);
        var prevDealers = prev?.Groups.SelectMany(g => g.Dealers).GroupBy(d => d.Key).ToDictionary(g => g.Key, g => g.First().Values) ?? [];
        var prevGroups = prev?.Groups.ToDictionary(g => g.Id, g => g.Total) ?? [];

        var all = EntriesOf(view);
        var shown = dealer is null ? all : MatchDealers(all, e => e.Dealer.Name, e => e.Dealer.Region, dealer, label);
        var detailed = dealer is not null && shown.Count <= DetailedDealers;
        var warnings = Warnings(current, prevRef);
        IReadOnlyDictionary<string, decimal?>? Before(PnlDealerView d) => prevDealers.TryGetValue(d.Key, out var before) ? before : null;

        return Data(new
        {
            Period = new { Year = year, Month = MonthName(month), Mode = "месяц" },
            Total = Lines(view.Total),
            TotalVsPrevMonth = prev is null ? null : Change(view.Total, prev.Total),
            PrevMonth = prev is { Year: { } py, Month: { } pm } ? $"{MonthName(pm)} {py}" : null,
            DealersTotal = all.Count,
            // Как на странице: РМ (направления) по порядку, внутри — дилеры по убыванию продаж; с фильтром dealer — только найденные.
            Rm = dealer is null
                ? view.Groups.Select(g => new
                {
                    g.Name,
                    g.Manager,
                    Total = Short(g.Total),
                    VsPrevMonth = prevGroups.TryGetValue(g.Id, out var before) ? Change(g.Total, before) : null,
                    Dealers = g.Dealers.TakeTop(args, MaxDealers, "groups[].dealers").Select(d => DealerRow(d.Name, d.Region, null, d.Values, Before(d), false)),
                })
                : null,
            Dealers = dealer is null
                ? null
                : shown.OrderByDescending(e => e.Dealer.Values.GetValueOrDefault(PnlMath.Sales) ?? 0)
                    .TakeTop(args, MaxDealers, "dealers")
                    .Select(e => DealerRow(e.Dealer.Name, e.Dealer.Region, RmOf(e.Group), e.Dealer.Values, Before(e.Dealer), detailed)),
            DealerYear = card is null
                ? null
                : new
                {
                    card.Year,
                    Months = card.Months.Select(x => new
                    {
                        Month = MonthName(x.Month),
                        Sales = R(V(x.Values, PnlMath.Sales)),
                        Gross = R(V(x.Values, PnlMath.Gross)),
                        MarginPct = Pct(V(x.Values, PnlMath.Margin)),
                        Ebit = R(V(x.Values, PnlMath.Ebit)),
                        RosPct = Pct(V(x.Values, PnlMath.Ros)),
                    }),
                    YearTotal = Short(card.Total),
                },
            Warnings = warnings.Count == 0 ? null : warnings,
            LastLoad = LastLoad(view.LastLoad),
            AvailableMonths = months.Take(12).Select(MonthRef),
            Note = Explanation,
        }, new DataSource("OneBase → Продажи → P&L дилеров", label, $"/sales/pnl?pnl={MonthRange.Key((year, month))}"));
    }

    private async Task<ToolResult> PeriodAsync((int Year, int Month) start, (int Year, int Month) end, IReadOnlyList<PnlMonthRef> months, string? dealer,
        ToolArgs args, CancellationToken ct)
    {
        var length = MonthRange.Length(start, end);
        if (length < 1 || length > MaxMonths)
        {
            throw new ToolArgumentException(length < 1
                ? $"Начало периода ({MonthRange.Key(start)}) позже конца ({MonthRange.Key(end)})."
                : $"Период — не длиннее {MaxMonths} месяцев (выбрано {length}): укажи from и to.");
        }

        var label = MonthRange.Label(start, end);
        var inRange = months
            .Where(m => MonthRange.Index((m.Year, m.Month)) >= MonthRange.Index(start) && MonthRange.Index((m.Year, m.Month)) <= MonthRange.Index(end))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();
        if (inRange.Count == 0)
        {
            return NotLoaded($"P&L дилеров за {label} в OneBase не загружен.", months);
        }

        var views = new List<PnlView>();
        foreach (var m in inRange)
        {
            views.Add(await pnl.GetAsync(m.Year, m.Month, ct));
        }

        var entries = views.SelectMany(v => v.Groups.SelectMany(g => g.Dealers.Select(d => new Entry((v.Year!.Value, v.Month!.Value), g, d)))).ToList();
        var dealers = entries.GroupBy(e => e.Dealer.Key)
            .Select(g =>
            {
                var last = g.Last();
                return new DealerTotal(g.Key, last.Dealer.Name, last.Dealer.Region, last.Group, PnlMath.Sum(g.Select(e => e.Dealer.Values)), g.ToList());
            })
            .ToList();
        var shown = dealer is null ? dealers : MatchDealers(dealers, d => d.Name, d => d.Region, dealer, label);
        var detailed = dealer is not null && shown.Count <= DetailedDealers;
        var missing = Enumerable.Range(0, length)
            .Select(i => MonthRange.Shift(start, i))
            .Where(m => inRange.All(x => x.Year != m.Year || x.Month != m.Month))
            .Select(MonthRange.Key)
            .ToList();
        var warnings = inRange.Where(m => !m.HasExpenses || !m.HasSales)
            .Select(m => $"{MonthName(m.Month)} {m.Year}: " + (!m.HasExpenses ? "загружены только продажи — расходов нет, прибыль завышена" : "загружены только расходы — продаж и себестоимости нет"))
            .ToList();
        var lastMonth = inRange[^1];

        return Data(new
        {
            Period = new { From = MonthRange.Key(start), To = MonthRange.Key(end), Name = label, Mode = "период — сумма загруженных месяцев", MonthsLoaded = inRange.Count },
            MissingMonths = missing.Count == 0 ? null : missing,
            Total = Lines(PnlMath.Sum(views.Select(v => v.Total))),
            ByMonth = views.Select(v => new
            {
                Month = MonthRange.Key((v.Year!.Value, v.Month!.Value)),
                Sales = R(V(v.Total, PnlMath.Sales)),
                Gross = R(V(v.Total, PnlMath.Gross)),
                MarginPct = Pct(V(v.Total, PnlMath.Margin)),
                Expenses = R(ExpensesOf(v.Total)),
                Ebit = R(V(v.Total, PnlMath.Ebit)),
                RosPct = Pct(V(v.Total, PnlMath.Ros)),
            }),
            DealersTotal = dealers.Count,
            // РМ — по последнему месяцу периода, где был дилер; внутри — дилеры по убыванию продаж за период.
            Rm = dealer is null
                ? views.SelectMany(v => v.Groups).GroupBy(g => g.Id).Select(g =>
                {
                    var group = g.Last();
                    return new
                    {
                        group.Name,
                        group.Manager,
                        Total = Short(PnlMath.Sum(g.Select(x => x.Total))),
                        Dealers = dealers.Where(d => d.Group.Id == g.Key)
                            .OrderByDescending(d => d.Values.GetValueOrDefault(PnlMath.Sales) ?? 0)
                            .TakeTop(args, MaxDealers, "groups[].dealers")
                            .Select(d => DealerRow(d.Name, d.Region, null, d.Values, null, false, d.Months.Count < inRange.Count ? d.Months.Count : null)),
                    };
                })
                : null,
            Dealers = dealer is null
                ? null
                : shown.OrderByDescending(d => d.Values.GetValueOrDefault(PnlMath.Sales) ?? 0)
                    .TakeTop(args, MaxDealers, "dealers")
                    .Select(d => new
                    {
                        Row = DealerRow(d.Name, d.Region, RmOf(d.Group), d.Values, null, detailed, d.Months.Count),
                        ByMonth = detailed
                            ? d.Months.Select(e => new
                            {
                                Month = MonthRange.Key(e.Month),
                                Sales = R(V(e.Dealer.Values, PnlMath.Sales)),
                                Gross = R(V(e.Dealer.Values, PnlMath.Gross)),
                                MarginPct = Pct(V(e.Dealer.Values, PnlMath.Margin)),
                                Ebit = R(V(e.Dealer.Values, PnlMath.Ebit)),
                            })
                            : null,
                    }),
            Warnings = warnings.Count == 0 ? null : warnings,
            LastLoad = LastLoad(views[^1].LastLoad),
            AvailableMonths = months.Take(12).Select(MonthRef),
            Note = Explanation + " За период — сумма месяцев (маржа и ROS — от сумм); Months у дилера — сколько месяцев периода он есть в P&L.",
        }, new DataSource("OneBase → Продажи → P&L дилеров", $"{label}, сумма загруженных месяцев", $"/sales/pnl?pnl={MonthRange.Key((lastMonth.Year, lastMonth.Month))}"));
    }

    private const string Explanation =
        "Прибыль завода на дилере, сум. Gross = продажи − себестоимость, MarginPct = Gross ÷ продажи; Expenses = Gross − EBIT (отдел продаж и маркетинга − ретробонусы " +
        "+ доставка и склад + административные + прочие + амортизация); ROS = (Gross − отдел продаж и маркетинга + ретробонусы) ÷ продажи. " +
        "EAT (чистая прибыль) — только если загружены банковские услуги или налоги, иначе итог — EBIT. VsPrevMonth: суммы — %, маржа — п. п. Данные — из AI Data Manager.";

    private static ToolResult NotLoaded(string text, IReadOnlyList<PnlMonthRef> months) => Data(new
    {
        Loaded = false,
        Note = $"{text} {LoadHelp}",
        AvailableMonths = months.Count == 0 ? null : months.Take(12).Select(MonthRef),
    }, new DataSource("OneBase → Продажи → P&L дилеров", null, "/sales/pnl"));

    private static object MonthRef(PnlMonthRef m) => new
    {
        Month = MonthRange.Key((m.Year, m.Month)),
        Sales = m.HasSales ? (bool?)null : false,
        Expenses = m.HasExpenses ? (bool?)null : false,
    };

    private static List<string> Warnings(PnlMonthRef current, PnlMonthRef? previous)
    {
        var list = new List<string>();
        if (!current.HasExpenses)
        {
            list.Add("За месяц загружены только продажи — расходов (реестр «Прочие расходы», логистика) нет: прибыль и EBIT завышены.");
        }

        if (!current.HasSales)
        {
            list.Add("За месяц загружены только расходы — продаж и себестоимости нет.");
        }

        if (previous is null)
        {
            list.Add("P&L прошлого месяца не загружен — изменения к прошлому месяцу нет.");
        }
        else if (previous.HasExpenses != current.HasExpenses)
        {
            list.Add("В этом и прошлом месяце загружены разные строки (расходы есть только в одном) — изменение EBIT и прибыли не сопоставимо.");
        }

        return list;
    }

    private static object? LastLoad(PnlSourceInfo? load) => load is null
        ? null
        : new
        {
            load.Title,
            load.FileName,
            AppliedAt = load.AppliedAt?.ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            load.AppliedBy,
        };

    private static string RmOf(PnlGroupView group) => group.Id == PnlService.OtherGroup ? PnlService.OtherGroupName : group.Manager ?? group.Name;

    private static decimal? V(IReadOnlyDictionary<string, decimal?> values, string key) => values.GetValueOrDefault(key);

    /// <summary>Расходы после валовой прибыли = Валовая − EBIT (отдел продаж и маркетинга − ретробонусы + доставка + административные + прочие + амортизация).</summary>
    internal static decimal? ExpensesOf(IReadOnlyDictionary<string, decimal?> values) =>
        V(values, PnlMath.Gross) is { } gross && V(values, PnlMath.Ebit) is { } ebit ? gross - ebit : null;

    /// <summary>Все строки P&L: суммы — сум, проценты — с одним знаком.</summary>
    private static object Lines(IReadOnlyDictionary<string, decimal?> v) => new
    {
        Sales = R(V(v, PnlMath.Sales)),
        Cost = R(V(v, PnlMath.Cost)),
        Gross = R(V(v, PnlMath.Gross)),
        MarginPct = Pct(V(v, PnlMath.Margin)),
        SalesMarketing = R(V(v, PnlMath.Sm)),
        Retrobonuses = R(V(v, PnlMath.Retro)),
        AfterSales = R(V(v, PnlMath.After)),
        RosPct = Pct(V(v, PnlMath.Ros)),
        Delivery = R(V(v, PnlMath.Ship)),
        Admin = R(V(v, PnlMath.Admin)),
        Other = R(V(v, PnlMath.Other)),
        Amortization = R(V(v, PnlMath.Amort)),
        Expenses = R(ExpensesOf(v)),
        Ebit = R(V(v, PnlMath.Ebit)),
        Bank = R(V(v, PnlMath.Bank)),
        Taxes = R(V(v, PnlMath.Tax)),
        Eat = R(V(v, PnlMath.Eat)),
        Dividends = R(V(v, PnlMath.Div)),
        Reinvested = R(V(v, PnlMath.Reinv)),
    };

    /// <summary>Главное — для РМ и строки дилера в списке.</summary>
    private static object Short(IReadOnlyDictionary<string, decimal?> v) => new
    {
        Sales = R(V(v, PnlMath.Sales)),
        Cost = R(V(v, PnlMath.Cost)),
        Gross = R(V(v, PnlMath.Gross)),
        MarginPct = Pct(V(v, PnlMath.Margin)),
        Expenses = R(ExpensesOf(v)),
        Other = R(V(v, PnlMath.Other)),
        Ebit = R(V(v, PnlMath.Ebit)),
        RosPct = Pct(V(v, PnlMath.Ros)),
        Eat = R(V(v, PnlMath.Eat)),
    };

    /// <summary>Строка дилера: главное (все строки — в Details, когда дилер выбран), rm — у найденных по фильтру, months — месяцев периода с P&L дилера.</summary>
    private static object DealerRow(string name, string? region, string? rm, IReadOnlyDictionary<string, decimal?> v, IReadOnlyDictionary<string, decimal?>? before,
        bool detailed, int? months = null) => new
    {
        Name = name,
        Region = region,
        Rm = rm,
        Months = months,
        Sales = R(V(v, PnlMath.Sales)),
        Cost = R(V(v, PnlMath.Cost)),
        Gross = R(V(v, PnlMath.Gross)),
        MarginPct = Pct(V(v, PnlMath.Margin)),
        Expenses = R(ExpensesOf(v)),
        Ebit = R(V(v, PnlMath.Ebit)),
        RosPct = Pct(V(v, PnlMath.Ros)),
        Eat = R(V(v, PnlMath.Eat)),
        VsPrevMonth = before is null ? null : Change(v, before),
        Details = detailed ? Lines(v) : null,
    };

    /// <summary>Изменение к прошлому месяцу (PnlMath.Growth): суммы — %, маржа — п. п.</summary>
    private static object Change(IReadOnlyDictionary<string, decimal?> now, IReadOnlyDictionary<string, decimal?> before)
    {
        var growth = PnlMath.Growth(now, before);
        return new
        {
            SalesPct = Pct(growth[PnlMath.Sales]),
            GrossPct = Pct(growth[PnlMath.Gross]),
            EbitPct = Pct(growth[PnlMath.Ebit]),
            MarginPp = Pct(growth[PnlMath.Margin]),
        };
    }

    /// <summary>
    /// Дилеры по имени или региону (кириллица или латиница, часть имени) — лучшие совпадения. Не нашлось — ошибка со списком дилеров.
    /// </summary>
    internal static List<T> MatchDealers<T>(IReadOnlyList<T> all, Func<T, string> name, Func<T, string?> region, string query, string period)
    {
        var scored = all
            .Select(d => (Item: d, Score: Math.Max(NameMatch.Score(name(d), query), region(d) is { } r ? NameMatch.Score(r, query) : 0)))
            .Where(x => x.Score > 0)
            .ToList();
        if (scored.Count == 0)
        {
            throw new ToolArgumentException(
                $"Дилера «{query}» в P&L за {period} нет. Дилеры: {string.Join(", ", all.Select(name).Distinct().Order(StringComparer.CurrentCulture))}.");
        }

        var best = scored.Max(x => x.Score);
        return scored.Where(x => x.Score == best).Select(x => x.Item).ToList();
    }
}

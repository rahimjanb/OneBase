using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Сравнение названий товаров и категорий с запросом пользователя. Названия в Linko написаны вперемешку
/// кириллицей и латиницей («Конфеты помадные … POMADKA», «Yamelly Orange»), поэтому обе стороны приводятся
/// к одному ключу: строчные буквы, кириллица транслитерируется в латиницу, пробелы и знаки убираются.
/// </summary>
internal static class NameMatch
{
    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
        ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
    };

    private static readonly char[] Separators = [' ', ',', ';', '/', '«', '»', '"', '\'', '(', ')', '-', '—', '–', '?', '!', '.', ':', '\t', '\n'];

    /// <summary>Ключ сравнения: «Помадка 0,5 кг» → «pomadka05kg».</summary>
    public static string Key(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
        {
            if (Translit.TryGetValue(ch, out var latin))
            {
                sb.Append(latin);
            }
            else if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Основа слова для нестрогого совпадения: «помадка» → «pomad», чтобы находились «помадные».
    /// Короткие слова почти не укорачиваются, иначе совпадало бы всё подряд.
    /// </summary>
    public static string Stem(string key) => key.Length switch
    {
        >= 6 => key[..^2],
        >= 4 => key[..^1],
        _ => key,
    };

    /// <summary>Ключи слов запроса; совсем короткие (одна буква) отбрасываются.</summary>
    public static IReadOnlyList<string> Tokens(string query) =>
        query.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Key)
            .Where(k => k.Length >= 2)
            .Distinct()
            .ToList();

    /// <summary>
    /// Насколько название подходит под запрос: 3 — весь запрос входит в название, 2 — каждое слово запроса есть в названии,
    /// 1 — есть основы всех слов («помадка» ↔ «помадные»), 0 — не подходит.
    /// </summary>
    public static int Score(string name, string query)
    {
        var key = Key(name);
        var whole = Key(query);
        if (key.Length == 0 || whole.Length < 2)
        {
            return 0;
        }

        if (key.Contains(whole, StringComparison.Ordinal))
        {
            return 3;
        }

        var tokens = Tokens(query);
        if (tokens.Count == 0)
        {
            return 0;
        }

        if (tokens.All(t => key.Contains(t, StringComparison.Ordinal)))
        {
            return 2;
        }

        return tokens.All(t => key.Contains(Stem(t), StringComparison.Ordinal)) ? 1 : 0;
    }

    /// <summary>
    /// Слово словаря есть в вопросе: какое-то слово вопроса начинается с его основы («помадку» ← «помадка»).
    /// Именно с начала слова, а не подстрокой — иначе короткая основа («сок») находилась бы внутри «высокий».
    /// </summary>
    public static bool MentionedIn(IReadOnlyList<string> questionTokens, string word)
    {
        var stem = Stem(Key(word));
        return stem.Length >= 3 && questionTokens.Any(t => t.StartsWith(stem, StringComparison.Ordinal));
    }
}

/// <summary>
/// Поиск категории, типа Linko или товара по названию и продажи найденного за месяц. Без него модель не знает,
/// что «помадка» — категория компании, а «POMADKA» — товар в ней, и отвечает «данных нет», хотя они есть.
/// </summary>
internal sealed class FindProductsTool(SalesDataLoader loader, SalesOptions options) : SalesTool(loader)
{
    // Лимиты подобраны так, чтобы ответ по самой широкой категории помещался в окно результата инструмента (AgentRunner режет на 16 000 символов).
    private const int MaxCategories = 3;
    private const int MaxProducts = 8;
    private const int MaxSkus = 20;
    private const int MaxRegions = 10;
    private const int ProductsWithRegions = 2;

    /// <summary>Сколько лучших совпадений считать подробно: карточка товара с разбивкой по регионам — недешёвая.</summary>
    private const int Candidates = 16;

    /// <summary>Регион без единой покупки за месяц (дыра в выгрузке): не «нет продаж» и не «не возят».</summary>
    private const string NoDataStatus = "нет данных";

    public override string Name => "find_products";
    public override string Title => "Поиск товара или категории";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string Description =>
        "Найти категорию, тип или товар (SKU) по названию, части названия, бренду или артикулу — кириллицей или латиницей («помадка», «POMADKA», «Yamelly», «130»). " +
        "Возвращает продажи найденного за месяц по республике или региону: у категории — итоги и её SKU со статусами, у товара — факт, ТТ с товаром, дистрибуция, " +
        "прошлый месяц и разбивка по регионам. Вызывай всегда, когда в вопросе есть название товара, категории или бренда.";

    public override string UsageHint =>
        "Названия товаров, категорий, брендов и артикулы из вопроса сначала ищи инструментом find_products (по части названия, кириллицей или латиницей) — " +
        "не отвечай, что такого товара нет или данных по нему нет, пока не проверил.";

    public override JsonElement InputSchema { get; } = Schema(
        ("query", new { type = "string", description = "Что искать: название или часть названия категории, типа, товара, бренда или артикул." }),
        YearArg, MonthArg, RegionArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var query = args.Str("query") ?? throw new ToolArgumentException("Укажите query — название или часть названия товара или категории.");
        var analytics = await LoadAsync(args, ct);
        var republic = analytics.CachedRepublic(null, null);
        var region = FindRegion(republic, args.Str("region"));
        var scope = new AssortmentScope(null, region is null ? null : Guid.Parse(region.Id), null, false);
        var p = republic.Period;
        var period = Query(p.Year, p.Month) + (region is null ? string.Empty : $"&region={region.Id}");

        var glossary = analytics.CategoryGlossary();
        var categories = glossary
            .Select(g => (Row: g, Score: g.LinkoTypes.Append(g.Name).Concat(Aliases(options, g)).Max(n => NameMatch.Score(n, query))))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Row.Configured)
            .Take(MaxCategories)
            .ToList();

        // Совпавшие товары: сначала точные совпадения, внутри — по продажам в охвате, чтобы лидер не потерялся при обрезке.
        var kgById = analytics.CachedAssortment(null, scope.Region).Products.ToDictionary(x => x.ProductId, x => x.Kg);
        var products = analytics.ProductCatalog
            .Select(pr => (Info: pr, Score: Math.Max(NameMatch.Score(pr.Name, query), pr.Code is null ? 0 : NameMatch.Score(pr.Code, query)), Kg: kgById.GetValueOrDefault(pr.Id)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Kg)
            .ThenBy(x => x.Info.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (categories.Count == 0 && products.Count == 0)
        {
            return Data(new
            {
                Query = query,
                Found = false,
                Hint = "Ничего не найдено. Попробуйте часть слова, другое написание (кириллица или латиница) или артикул.",
                Categories = glossary.Where(g => g.Configured).Select(g => g.Name),
                OtherLinkoTypes = glossary.Where(g => !g.Configured).Select(g => g.Name),
            });
        }

        var sources = new List<DataSource>();
        var categoryItems = new List<object>();
        foreach (var (row, _) in categories)
        {
            var view = analytics.CachedCategory(row.CategoryId, scope);
            var card = view?.Card;
            if (view is not null && card is not null)
            {
                sources.Add(new DataSource($"OneBase → Продажи → Ассортимент → {view.Name}", PeriodText(p), $"/sales/assortment/category/{Uri.EscapeDataString(row.CategoryId)}?{period}"));
            }

            categoryItems.Add(new
            {
                row.CategoryId,
                row.Name,
                LinkoTypes = row.LinkoTypes,
                AlsoKnownAs = Aliases(options, row).ToList() is { Count: > 0 } aliases ? aliases : null,
                InReport = row.Configured,
                Sales = card is null
                    ? null
                    : new
                    {
                        FactKg = R(card.FactKg),
                        WeightSharePct = Pct(card.WeightShare),
                        RevenueSum = R(card.Revenue),
                        card.Akb,
                        DistributionPct = Pct(card.Distribution),
                        ForecastKg = R(card.ForecastKg),
                        PrevMonthKg = R(card.PrevMonthKg),
                        VsPrevMonthPct = Pct(card.VsPrevMonth),
                        card.SkuSold,
                        card.SkuTotal,
                        SilentSku = card.Silent,
                        LostSku = card.Lost,
                    },
                Note = card is null ? "Продаж этой категории в выбранном охвате и месяце нет." : null,
                Skus = card?.Skus.OrderByDescending(s => s.Revenue).ThenByDescending(s => s.PrevMonthKg).Take(MaxSkus).Select(s => new
                {
                    s.ProductId,
                    s.Name,
                    s.Code,
                    Status = StatusName(s.Status),
                    FactKg = R(s.FactKg),
                    RevenueSum = R(s.Revenue),
                    OutletsWithSku = s.Akb,
                    DistributionPct = Pct(s.Distribution),
                    PrevMonthKg = R(s.PrevMonthKg),
                }),
                SkusShown = card is null ? 0 : Math.Min(MaxSkus, card.Skus.Count),
                ByRegion = view?.Regions.Take(MaxRegions).Select(r => new
                {
                    r.Name,
                    Status = RegionStatusName(r),
                    Kg = r.NoData ? null : (decimal?)R(r.Kg),
                    RevenueSum = r.NoData ? null : (decimal?)R(r.Revenue),
                    SkuSelling = r.NoData ? null : (int?)r.SkuSelling,
                    SkuNotCarried = r.NoData ? null : (int?)r.SkuNotCarried,
                    SkuLost = r.NoData ? null : (int?)r.SkuLost,
                    OutletsWithPurchase = r.NoData ? null : (int?)r.Akb,
                }),
            });
        }

        // Карточки лучших совпадений; у первых двух — разбивка по регионам.
        var productViews = products
            .Take(Candidates)
            .Select(x => analytics.CachedProduct(x.Info.Id, scope))
            .OfType<ProductView>()
            .OrderByDescending(v => v.FactKg)
            .ThenByDescending(v => v.PrevMonthKg)
            .Take(MaxProducts)
            .ToList();
        foreach (var v in productViews.Take(ProductsWithRegions))
        {
            sources.Add(new DataSource($"OneBase → Продажи → Ассортимент → {v.Name}", PeriodText(p), $"/sales/assortment/product/{v.ProductId}?{period}"));
        }

        var productItems = productViews.Select((v, i) => new
        {
            v.ProductId,
            v.Name,
            v.Code,
            v.Category,
            Status = StatusName(v.Status),
            FactKg = R(v.FactKg),
            RevenueSum = R(v.Revenue),
            OutletsWithProduct = v.Tt,
            DistributionPct = Pct(v.Distribution),
            PricePerKg = R(v.PricePerKg),
            PrevMonthKg = R(v.PrevMonthKg),
            // Регион без покупок за месяц (NoData) — «нет данных», а не «нет продаж»: счётчиков у него нет.
            ByRegion = i < ProductsWithRegions
                ? v.Rows.Where(ShowRegion).Take(MaxRegions).Select(r => new
                {
                    r.Name,
                    Status = RegionStatusName(r),
                    Kg = r.NoData ? null : (decimal?)R(r.Kg),
                    RevenueSum = r.NoData ? null : (decimal?)R(r.Revenue),
                    OutletsWithProduct = r.NoData ? null : (int?)r.Tt,
                    DistributionPct = r.NoData ? null : Pct(r.Distribution),
                    PrevMonthKg = R(r.PrevMonthKg),
                })
                : null,
        });

        return Data(new
        {
            Period = PeriodOf(p),
            Scope = region?.Name ?? "Республика",
            Query = query,
            Found = true,
            Categories = categoryItems,
            ProductsFound = products.Count,
            Products = productItems,
            Note = "Статусы: «продаётся»; «пропал» — продавался в прошлом месяце, в этом нет; «не возят» — по республике идёт, здесь нет; «нет продаж». " +
                "«нет данных» у региона — в нём за месяц нет ни одной покупки (дыра в выгрузке), это не «нет продаж» и не «не возят». " +
                "Товары отсортированы по продажам в охвате. Если месяц ещё не закончен (Complete = false), для оценки спроса сравнивай с прошлым полным месяцем — вызови инструмент с month прошлого месяца.",
        }, sources.Count == 0 ? [RepublicSource(p, "OneBase → Продажи → Ассортимент")] : sources.ToArray());
    }

    /// <summary>Как категория называется в планах Linko («могул» — помадка); слова, уже входящие в название, не повторяются.</summary>
    internal static IEnumerable<string> Aliases(SalesOptions options, CategoryGlossaryRow row) =>
        row.Configured && options.PlanIndicatorCategories.TryGetValue(row.Name, out var words)
            ? words.Where(w => !NameMatch.Key(row.Name).Contains(NameMatch.Key(w), StringComparison.Ordinal))
            : [];

    internal static string StatusName(string status) => status switch
    {
        SkuStatuses.Selling => "продаётся",
        SkuStatuses.Lost => "пропал",
        SkuStatuses.Elsewhere => "не возят",
        _ => "нет продаж",
    };

    /// <summary>Статус региона в разбивке товара: у региона без покупок за месяц (NoData) — «нет данных», иначе статус артикула в нём.</summary>
    internal static string RegionStatusName(ProductBreakdownRow row) => row.NoData ? NoDataStatus : StatusName(row.Status);

    /// <summary>Статус региона в разбивке категории: «нет данных» у региона без покупок за месяц, иначе «есть покупки».</summary>
    internal static string RegionStatusName(AssortmentRegionRow row) => row.NoData ? NoDataStatus : "есть покупки";

    /// <summary>Регион показывается в разбивке товара, если у него есть продажи в этом или прошлом месяце — или нет данных вовсе (это тоже ответ).</summary>
    internal static bool ShowRegion(ProductBreakdownRow row) => row.NoData || row.Kg != 0 || row.PrevMonthKg != 0;
}

/// <summary>
/// Словарь названий компании для промптов: категории отчёта с типами Linko внутри и псевдонимами из планов, регионы продаж, число SKU.
/// С ним маршрутизатор и AI-сотрудники узнают «помадку» или «Хоразм» в вопросе, а не считают их незнакомыми словами.
/// Собирается из справочников тремя лёгкими запросами (без загрузки месяца продаж) и живёт в кэше десять минут.
/// Справочники могут быть пустыми или недоступными — тогда словаря нет; пользователю это не ошибка, но в журнал пишется.
/// </summary>
public sealed class SalesGlossary(IAppDbContext db, SalesOptions options, IMemoryCache cache, ILogger<SalesGlossary> logger)
{
    private const int MaxTypesPerCategory = 6;
    private const int MaxOtherTypes = 12;
    private const string CacheKey = "ai:sales-glossary";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private sealed record Snapshot(IReadOnlyList<CategoryGlossaryRow> Rows, IReadOnlyList<string> Regions, int Products);

    public async Task<string?> TextAsync(CancellationToken ct)
    {
        var snapshot = await LoadAsync(ct);
        if (snapshot is null || snapshot.Rows.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Словарь названий компании (узнавай их в вопросах, даже с опечатками и в другом написании):");
        var configured = snapshot.Rows.Where(r => r.Configured).ToList();
        if (configured.Count > 0)
        {
            sb.AppendLine($"- Категории товаров: {string.Join(", ", configured.Select(r => Describe(r, options)))}.");
        }

        var other = snapshot.Rows.Where(r => !r.Configured).Select(r => r.Name).ToList();
        if (other.Count > 0)
        {
            sb.AppendLine($"- Типы товаров Linko вне категорий отчёта: {string.Join(", ", other.Take(MaxOtherTypes))}{(other.Count > MaxOtherTypes ? "…" : string.Empty)}.");
        }

        sb.AppendLine($"- Товаров (SKU) в справочнике: {snapshot.Products}; их названия часто латиницей (POMADKA, Yamelly, Magnolia) — конкретный товар или бренд ищи инструментом find_products.");
        if (snapshot.Regions.Count > 0)
        {
            sb.AppendLine($"- Регионы продаж: {string.Join(", ", snapshot.Regions)}.");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Слова словаря — для запасного выбора сотрудников по ключевым словам.</summary>
    public async Task<IReadOnlyList<string>> KeywordsAsync(CancellationToken ct)
    {
        var snapshot = await LoadAsync(ct);
        if (snapshot is null)
        {
            return [];
        }

        return snapshot.Rows.SelectMany(r => r.LinkoTypes.Append(r.Name).Concat(FindProductsTool.Aliases(options, r)))
            .Concat(snapshot.Regions)
            .Select(n => n.Trim())
            .Where(n => n.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Describe(CategoryGlossaryRow row, SalesOptions options)
    {
        var parts = new List<string>();
        var aliases = FindProductsTool.Aliases(options, row).ToList();
        if (aliases.Count > 0)
        {
            parts.Add($"в планах Linko — «{string.Join("», «", aliases)}»");
        }

        var types = row.LinkoTypes.Take(MaxTypesPerCategory).ToList();
        if (types.Count > 0)
        {
            parts.Add($"типы Linko: {string.Join(", ", types)}{(row.LinkoTypes.Count > types.Count ? "…" : string.Empty)}");
        }

        return parts.Count == 0 ? row.Name : $"{row.Name} ({string.Join("; ", parts)})";
    }

    private async Task<Snapshot?> LoadAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out Snapshot? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
            var rows = SalesAnalytics.BuildGlossary(SalesCategories.Build(options.Categories, types), types);
            var regions = await db.SalesRegions.AsNoTracking().OrderBy(r => r.Name).Select(r => r.Name).ToListAsync(ct);
            var products = await db.LinkoProducts.CountAsync(ct);
            var snapshot = new Snapshot(rows, regions, products);
            cache.Set(CacheKey, snapshot, Ttl);
            return snapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Словарь названий компании для AI не собран — маршрутизатор и сотрудники работают без него");
            return null;
        }
    }
}

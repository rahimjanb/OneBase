using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.SkuSales;
using OneBase.Application.Security;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// get_sku_sales, get_export_sales, get_dealer_pnl без базы: схема аргументов, источник и право, разбор аргументов до чтения данных
/// (сервисы не нужны — null), понятные ошибки с перечнем допустимых значений (регион, категория, дилер), период по умолчанию.
/// </summary>
public class SalesExtraToolsTests
{
    private static readonly ToolContext Context = new("sales", null, 0);

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static DataTool[] Tools() => [new GetSkuSalesTool(null!, null!), new GetExportSalesTool(null!, null!), new GetDealerPnlTool(null!, null!)];

    private sealed class FakePermissions(params string[] granted) : IUserPermissions
    {
        public Task<IReadOnlySet<string>> GetAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(granted.ToHashSet());
    }

    [Fact]
    public void Schemas_are_objects_whose_arguments_have_type_and_russian_description()
    {
        foreach (var tool in Tools())
        {
            var schema = tool.InputSchema;
            Assert.Equal("object", schema.GetProperty("type").GetString());
            var properties = schema.GetProperty("properties").EnumerateObject().ToList();
            Assert.NotEmpty(properties);
            foreach (var p in properties)
            {
                Assert.Contains(p.Value.GetProperty("type").GetString(), new[] { "string", "integer" });
                Assert.Matches("[а-яА-Я]", p.Value.GetProperty("description").GetString()!);
            }

            Assert.Matches("[а-яА-Я]", tool.Description);
            Assert.Matches("^[a-z_]+$", tool.Name);
        }

        Assert.Equal(["from", "to", "months", "region", "category", "query", "top", "abc", "limit"],
            new GetSkuSalesTool(null!, null!).InputSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["year", "month"], new GetExportSalesTool(null!, null!).InputSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["year", "month", "from", "to", "dealer"], new GetDealerPnlTool(null!, null!).InputSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Tools_read_their_knowledge_sources_with_page_permissions()
    {
        var (sku, export, pnl) = (new GetSkuSalesTool(null!, null!), new GetExportSalesTool(null!, null!), new GetDealerPnlTool(null!, null!));
        Assert.Equal(("get_sku_sales", KnowledgeSources.SalesSku, Permissions.SalesRead), (sku.Name, sku.Source, sku.RequiredPermission));
        Assert.Equal(("get_export_sales", KnowledgeSources.SalesExport, Permissions.SalesRead), (export.Name, export.Source, export.RequiredPermission));
        Assert.Equal(("get_dealer_pnl", KnowledgeSources.DealerPnl, Permissions.FinanceRead), (pnl.Name, pnl.Source, pnl.RequiredPermission));
        Assert.All(Tools(), t => Assert.NotNull(KnowledgeSources.Find(t.Source!)));
        Assert.All(Tools(), t => Assert.False(t.IsCritical)); // только чтение
    }

    [Theory]
    [InlineData("""{"to":"сентябрь"}""", "Аргумент to — месяц в формате ГГГГ-ММ")]
    [InlineData("""{"from":"2026-13"}""", "Аргумент from — месяц в формате ГГГГ-ММ")]
    [InlineData("""{"months":30}""", "Аргумент months — от 1 до 24.")]
    [InlineData("""{"limit":0}""", "Аргумент limit — от 1 до 40.")]
    [InlineData("""{"top":"maybe"}""", "Аргумент top — all (все SKU), only (только ТОП) или not (кроме ТОПа)")]
    [InlineData("""{"abc":"D"}""", "Аргумент abc — A, B или C")]
    public async Task Sku_sales_rejects_bad_arguments_before_reading_data(string json, string error)
    {
        var result = await new GetSkuSalesTool(null!, null!).ExecuteAsync(Context, Args(json));
        Assert.False(result.Success);
        Assert.Contains(error, result.Content);
    }

    [Theory]
    [InlineData("""{"month":13}""", "Аргумент month — от 1 до 12.")]
    [InlineData("""{"year":"двадцать шестой"}""", "Аргумент year должен быть целым числом.")]
    public async Task Export_rejects_bad_period_before_reading_data(string json, string error)
    {
        var result = await new GetExportSalesTool(null!, null!).ExecuteAsync(Context, Args(json));
        Assert.False(result.Success);
        Assert.Contains(error, result.Content);
    }

    [Theory]
    [InlineData("""{"from":"2026-09","to":"2026-07"}""", "Начало периода (2026-09) позже конца (2026-07).")]
    [InlineData("""{"from":"2024-01","to":"2026-09"}""", "Период — не длиннее 24 месяцев (выбрано 33).")]
    [InlineData("""{"from":"сентябрь"}""", "Аргумент from — месяц в формате ГГГГ-ММ")]
    [InlineData("""{"month":13}""", "Аргумент month — от 1 до 12.")]
    public async Task Pnl_rejects_bad_period_before_reading_data(string json, string error)
    {
        var result = await new GetDealerPnlTool(null!, null!).ExecuteAsync(Context, Args(json));
        Assert.False(result.Success);
        Assert.Contains(error, result.Content);
    }

    [Fact]
    public async Task Pnl_needs_sales_permission_too_like_the_page()
    {
        var finance = new ToolContext("finance", Guid.NewGuid(), 0);
        var result = await new GetDealerPnlTool(null!, new FakePermissions(Permissions.FinanceRead)).ExecuteAsync(finance, Args("{}"));
        Assert.False(result.Success);
        Assert.Contains("правами «Продажи» и «Финансы»", result.Content);

        // С обоими правами проверка пропускает дальше — до разбора аргументов.
        var both = await new GetDealerPnlTool(null!, new FakePermissions(Permissions.FinanceRead, Permissions.SalesRead)).ExecuteAsync(finance, Args("""{"month":0}"""));
        Assert.Contains("Аргумент month — от 1 до 12.", both.Content);
    }

    [Fact]
    public void Unknown_region_gives_clear_error_with_region_list()
    {
        SkuSalesRegionRef[] regions = [new(Guid.NewGuid().ToString(), "Хоразм"), new(Guid.NewGuid().ToString(), "Бухоро"), new(Guid.NewGuid().ToString(), "Самарканд")];

        var error = Assert.Throws<ToolArgumentException>(() => GetSkuSalesTool.FindRegion(regions, "Андижон"));
        Assert.Equal("Региона «Андижон» нет. Регионы: Хоразм, Бухоро, Самарканд.", error.Message);

        Assert.Null(GetSkuSalesTool.FindRegion(regions, null)); // вся страна
        Assert.Equal("Самарканд", GetSkuSalesTool.FindRegion(regions, "самарканд")!.Name);
        Assert.Equal("Самарканд", GetSkuSalesTool.FindRegion(regions, "Самарк")!.Name);
        Assert.Equal("Бухоро", GetSkuSalesTool.FindRegion(regions, regions[1].Id.ToUpperInvariant())!.Name);
    }

    [Fact]
    public void Categories_match_page_names_in_any_script_and_unknown_lists_categories()
    {
        string[] available = ["Помадка", "Кекс", "Бамбук"];
        Assert.Equal(["Кекс", "Помадка"], GetSkuSalesTool.FindCategories(available, "кекс, POMADKA"));
        Assert.Empty(GetSkuSalesTool.FindCategories(available, null));

        var error = Assert.Throws<ToolArgumentException>(() => GetSkuSalesTool.FindCategories(available, "Кекс,Вафли"));
        Assert.Equal("Категории «Вафли» в продажах за период нет. Категории: Помадка, Кекс, Бамбук.", error.Message);
    }

    [Theory]
    [InlineData("2026-10-20", null, null, null, "2026-08", "2026-10")] // три месяца, два квартала — динамика есть
    [InlineData("2026-09-15", null, null, null, "2026-06", "2026-09")] // три месяца в одном квартале — четыре
    [InlineData("2026-03-10", null, null, null, "2025-12", "2026-03")]
    [InlineData("2026-09-15", null, null, 3, "2026-07", "2026-09")] // months задан явно — как есть
    [InlineData("2026-10-20", null, null, 6, "2026-05", "2026-10")]
    [InlineData("2026-10-20", "2026-01", "2026-06", null, "2026-01", "2026-06")]
    [InlineData("2026-10-20", null, "2026-06", null, "2026-03", "2026-06")]
    public void Default_period_is_last_three_months_with_two_quarters(string lastData, string? from, string? to, int? months, string expectedFrom, string expectedTo)
    {
        static (int, int)? Month(string? s) => s is null ? null : (int.Parse(s[..4]), int.Parse(s[5..]));
        var (start, end) = GetSkuSalesTool.PeriodOf(Month(from), Month(to), months, DateOnly.Parse(lastData));
        Assert.Equal((expectedFrom, expectedTo), (MonthRange.Key(start), MonthRange.Key(end)));
    }

    [Fact]
    public void Top_and_abc_filters_accept_page_values()
    {
        Assert.Equal(SkuSalesTop.All, GetSkuSalesTool.TopOf(null));
        Assert.Equal(SkuSalesTop.Only, GetSkuSalesTool.TopOf("ONLY"));
        Assert.Equal(SkuSalesTop.Not, GetSkuSalesTool.TopOf("not"));
        Assert.Null(GetSkuSalesTool.AbcOf(null));
        Assert.Null(GetSkuSalesTool.AbcOf("all"));
        Assert.Equal("A", GetSkuSalesTool.AbcOf("a"));
        Assert.Equal("C", GetSkuSalesTool.AbcOf("С")); // кириллица
    }

    [Fact]
    public void Sku_search_keeps_best_matches_by_name_or_code_in_rank_order()
    {
        static SkuSalesRow Row(long id, string name, string code) =>
            new(id, name, code, "Кекс", false, (int)id, 10, 100, 10, 0.1m, 0.1m, "A", 5, 2, null, null, null, false, []);
        SkuSalesRow[] rows = [Row(1, "Кекс 1 кг", "100"), Row(2, "Кекс шоколадный 0,5 кг", "101"), Row(3, "Конфеты помадные POMADKA 0,5 кг", "130")];

        Assert.Equal([1L, 2L], GetSkuSalesTool.Search(rows, "кекс").Select(r => r.ProductId));
        Assert.Equal([3L], GetSkuSalesTool.Search(rows, "pomadka").Select(r => r.ProductId));
        Assert.Equal([3L], GetSkuSalesTool.Search(rows, "130").Select(r => r.ProductId));
        Assert.Empty(GetSkuSalesTool.Search(rows, "вафли"));
    }

    [Fact]
    public void Export_country_outlets_and_month_comparison()
    {
        Assert.Equal(["Dushanbe Market", "Khujand Opt"], GetExportSalesTool.OutletsOf("Dushanbe Market, Khujand Opt"));
        Assert.Empty(GetExportSalesTool.OutletsOf(null));

        var zero = PrimaryAmounts.NoBoxes;
        var shares = new PrimaryShares(null, null, null, null);
        var row = new PrimaryRow("Таджикистан", "Таджикистан", "Dushanbe Market, Khujand Opt", zero, [], null, [], null, shares, null, null, zero, shares, null, null);
        var map = GetExportSalesTool.CountryOfOutlet([row]);
        Assert.Equal("Таджикистан", map["dushanbe market"]);
        Assert.False(map.ContainsKey("Оптовик Ташкент"));

        // Закрытый месяц — факт к факту прошлого месяца; идущий — значение страницы (прогноз к прошлому месяцу).
        Assert.Equal(0.2m, GetExportSalesTool.VsPrev(100, 120, 0.5m, closed: true));
        Assert.Equal(0.5m, GetExportSalesTool.VsPrev(100, 60, 0.5m, closed: false));
        Assert.Null(GetExportSalesTool.VsPrev(0, 60, null, closed: true));
    }

    [Fact]
    public void Dealer_filter_matches_name_or_region_and_unknown_lists_dealers()
    {
        (string Name, string? Region)[] dealers = [("Азизбек Хоразм", "Хоразм"), ("Бобур Самарканд", "Самарканд"), ("Мега Плюс", null)];
        Assert.Equal("Бобур Самарканд", Assert.Single(GetDealerPnlTool.MatchDealers(dealers, d => d.Name, d => d.Region, "самарканд", "сентябрь 2026")).Name);
        Assert.Equal("Азизбек Хоразм", Assert.Single(GetDealerPnlTool.MatchDealers(dealers, d => d.Name, d => d.Region, "Азизбек", "сентябрь 2026")).Name);

        var error = Assert.Throws<ToolArgumentException>(() => GetDealerPnlTool.MatchDealers(dealers, d => d.Name, d => d.Region, "Андижон", "сентябрь 2026"));
        Assert.Equal("Дилера «Андижон» в P&L за сентябрь 2026 нет. Дилеры: Азизбек Хоразм, Бобур Самарканд, Мега Плюс.", error.Message);
    }

    /// <summary>AgentRunner обрезает ответ инструмента на 16 000 символах — ответы по умолчанию должны помещаться с запасом.</summary>
    private const int AnswerBudget = 14_000;

    private static readonly ToolArgs NoArgs = new(Args("{}"));

    [Fact]
    public void Sku_sales_answer_fits_the_tool_result_window()
    {
        // 200 SKU с длинными названиями × 14 регионов × 3 месяца (два квартала): часть SKU новые, часть падает.
        var options = new SalesOptions();
        var types = new Dictionary<long, string> { [4] = "Бамбук", [10] = "Кекс", [9] = "Трубочки", [6] = "Печенье", [5] = "Шоколад", [7] = "Песочный", [8] = "Помадка" };
        var products = Enumerable.Range(1, 200).ToDictionary(i => (long)i,
            i => new ProductInfo(i, $"Конфеты помадные «Ассорти» с начинкой вишня-шоколад {i} 0,5 кг POMADKA MIX", $"{1000 + i}", types.Keys.ElementAt(i % types.Count)));
        var regions = Enumerable.Range(1, 14).Select(i => new RegionInfo(Guid.NewGuid(), i, $"Регион {i} Самарканд", null, null, null)).ToList();
        var months = SkuSalesReport.MonthsOf(2026, 8, 2026, 10, new DateOnly(2026, 10, 20));
        var sales = new List<SkuMonthSales>();
        foreach (var (id, _) in products)
        {
            foreach (var region in regions)
            {
                foreach (var m in months)
                {
                    var kg = id % 5 == 0 && m.Month < 10 ? 0 : (201 - id) * (id % 3 == 0 && m.Month == 10 ? 0.5m : 1m) * 10;
                    sales.Add(new SkuMonthSales(id, region.Id, m.Year, m.Month, kg, kg * 31_000));
                }
            }
        }

        var aggregate = new SkuSalesAggregate(sales, products.Keys.ToDictionary(p => p, _ => 1234),
            products.Keys.SelectMany(p => regions.Select(r => new SkuRegionAkb(p, r.Id, 88))).ToList());
        var catalog = new SkuSalesCatalog(products, SalesCategories.Build(options.Categories, types), regions, TopProductSet.Of(options));
        var view = SkuSalesReport.Build("2026-08", "2026-10", months, new DateOnly(2026, 10, 20), new SkuSalesFilter(null, SkuSalesTop.All, "all", []), catalog, aggregate);
        Assert.NotNull(view.Quarters);

        var result = GetSkuSalesTool.Answer(view, "август — октябрь 2026", null, 30, NoArgs);
        Assert.True(result.Success);
        Assert.True(result.Content.Length < AnswerBudget, $"Ответ get_sku_sales — {result.Content.Length} символов.");
        using var json = JsonDocument.Parse(result.Content);
        Assert.Equal(30, json.RootElement.GetProperty("skus").GetArrayLength());
        Assert.True(json.RootElement.GetProperty("new").GetArrayLength() > 0);
        Assert.True(json.RootElement.GetProperty("falling").GetArrayLength() > 0);
        Assert.Equal(14, json.RootElement.GetProperty("byRegion").GetArrayLength());
        Assert.Equal("/sales/sku-sales?from=2026-08&to=2026-10", result.Sources![0].Href);

        // Предел limit (40) — тоже в окне инструмента.
        var max = GetSkuSalesTool.Answer(view, "август — октябрь 2026", null, 40, NoArgs).Content.Length;
        Assert.True(max < 16_000, $"Ответ get_sku_sales с limit=40 — {max} символов.");
    }

    [Fact]
    public void Dealer_pnl_answer_fits_the_tool_result_window()
    {
        // 6 РМ × 6 дилеров со всеми строками P&L, прошлый месяц для динамики.
        static PnlView View(int month, decimal scale)
        {
            var groups = Enumerable.Range(1, 6).Select(g =>
            {
                var dealers = Enumerable.Range(1, 6).Select(d => new PnlDealerView($"dealer {g}-{d}", $"Дилер ООО «Торговый дом {g}-{d}» Самарканд", $"Регион {g}-{d}",
                    PnlMath.Compute(PnlMath.Inputs.ToDictionary(k => k, k => k == PnlMath.Sales ? 1_234_567_890m * scale : 12_345_678m * scale), PnlMath.Inputs.ToHashSet())))
                    .ToList();
                return new PnlGroupView($"{g}", $"Направление {g}", $"Менеджер РМ {g}", dealers, PnlMath.Sum(dealers.Select(d => d.Values)));
            }).ToList();
            return new PnlView(2026, month, [], PnlService.LineViews, groups, PnlMath.Sum(groups.Select(g => g.Total)), false, PnlMath.Inputs.ToList(),
                new PnlSourceInfo(Guid.NewGuid(), "P&L сентябрь 2026", "pnl_2026_09.xlsx", DateTimeOffset.UtcNow, "Администратор"));
        }

        var months = Enumerable.Range(1, 9).Reverse().Select(m => new PnlMonthRef(2026, m, true, true)).ToList();
        var result = GetDealerPnlTool.MonthAnswer(View(9, 1.1m), View(8, 1m), months, null, null, NoArgs);
        Assert.True(result.Success);
        Assert.True(result.Content.Length < AnswerBudget, $"Ответ get_dealer_pnl — {result.Content.Length} символов.");
        using var json = JsonDocument.Parse(result.Content);
        Assert.Equal(36, json.RootElement.GetProperty("dealersTotal").GetInt32());
        Assert.Equal(36, json.RootElement.GetProperty("rm").EnumerateArray().Sum(g => g.GetProperty("dealers").GetArrayLength()));
        Assert.Equal(10.0m, json.RootElement.GetProperty("totalVsPrevMonth").GetProperty("salesPct").GetDecimal());
        Assert.Equal(10.0m, json.RootElement.GetProperty("rm")[0].GetProperty("dealers")[0].GetProperty("vsPrevMonth").GetProperty("salesPct").GetDecimal());
        Assert.Equal("/sales/pnl?pnl=2026-09", result.Sources![0].Href);

        // Один дилер — со всеми строками P&L.
        var one = GetDealerPnlTool.MonthAnswer(View(9, 1.1m), View(8, 1m), months, "Торговый дом 3-4", null, NoArgs);
        using var single = JsonDocument.Parse(one.Content);
        Assert.Equal("Регион 3-4", single.RootElement.GetProperty("dealers")[0].GetProperty("region").GetString());
        Assert.True(single.RootElement.GetProperty("dealers")[0].GetProperty("details").TryGetProperty("amortization", out _));
    }

    [Fact]
    public void Export_answer_fits_the_tool_result_window()
    {
        var zero = PrimaryAmounts.NoBoxes;
        var shares = new PrimaryShares(0.05m, null, 0.05m, null);
        var months = Enumerable.Range(1, 12).Select(m => m <= 9 ? new PrimaryAmounts(10_000 + m, null, 250_000_000m + m, 0) : zero).ToList();
        PrimaryRow Row(string name, string? sub) =>
            new(name, name, sub, months[8], months, null, new decimal?[12], null, shares, null, null, months[0], shares, null, null);
        var countries = Enumerable.Range(1, 18)
            .Select(i => Row($"Страна экспорта {i}", string.Join(", ", Enumerable.Range(1, 10).Select(j => $"Торговая точка {i}-{j} Dushanbe LLC"))))
            .ToList();
        var categories = Enumerable.Range(1, 10).Select(i => Row($"Категория {i}", null)).ToList();
        var ship = new PrimaryView(
            Year: 2026, Month: 9, DataThrough: new DateOnly(2026, 9, 30), AsOf: new DateOnly(2026, 10, 20), Running: false, RunningMonth: 10,
            DaysInMonth: 30, WorkedDays: 30, SyncedAt: null, FactoryStock: null, ExportStock: null, DealerPriceList: null,
            Republic: new PrimaryCard(0, 0, 0, 0), Export: new PrimaryCard(90_000, 2_250_000_000, 18, 240),
            MonthTotal: months[8], PlanMonthKg: null, MonthExecution: null, MonthRemainingKg: null, MonthOverPlanKg: null,
            ForecastKg: null, ForecastExecution: null, MonthTransfers: 40, MonthCounterparties: 18, HasPlan: false,
            MonthsWithData: Enumerable.Range(1, 9).ToList(), Months: months, PlanMonths: new decimal?[12], MonthExecutions: new decimal?[12],
            Ytd: months[0], YtdMonths: 9, YtdArticles: 120, YtdReturnsKg: 300, YtdReturnLines: 4, YtdReturnsShare: 0.01m,
            PlanYtdKg: null, YtdExecution: null, YtdPricePerKg: 25_000, YtdMarkupSum: null, YtdMarkup: null, BoxesUnknownKg: 0,
            Categories: categories, CategoryTotal: categories[0], Dealers: countries, DealerGroups: [], DealerTotal: countries[0], Items: [],
            Clients: null, Calendar: null, Notes: ["Заказов в другой валюте за 2026 год: 3 — их вес учтён, сумма нет (курса в Linko нет)."]);

        var period = new PeriodInfo(2026, 9, new DateOnly(2026, 9, 30), 30, 30, new DateOnly(2026, 8, 31), "rop", ["rop"]);
        var cards = Enumerable.Range(1, 10).Select(i => new CategoryCard($"{i}", $"Категория {i}", 10, 20, 5_000, 0.1m, 150_000_000, 40, 0.5m, null, null, 4_500, null, 2, 1, []))
            .ToList();
        var sales = new ExportView(period, new ExcludedSummary(95_000, 2_400_000_000, 300, 80, null, 90_000, null), cards,
            Enumerable.Range(1, 60).Select(i => new ExportMarketRow(i, $"Торговая точка {i}-1 Dushanbe LLC", 1_500 - i, 37_500_000, 4, 1_400)).ToList(),
            Enumerable.Range(1, 15).Select(i => new ExportAgentRow(i, $"Агент экспорта {i}", 6_000, 150_000_000, 5)).ToList(),
            Enumerable.Range(1, 80).Select(i => new ProductRow(i, $"Конфеты помадные «Ассорти» {i} 0,5 кг", $"{i}", "Помадка", true, 1_000 - i, 25_000_000, 0.01m, 10, 0.1m)).ToList(),
            [new CurrencyTotal("USD", 125_000, 3)]);

        var result = GetExportSalesTool.Answer(sales, ship, null, NoArgs);
        Assert.True(result.Success);
        Assert.True(result.Content.Length < AnswerBudget, $"Ответ get_export_sales — {result.Content.Length} символов.");
        using var json = JsonDocument.Parse(result.Content);
        Assert.Equal(18, json.RootElement.GetProperty("countries").GetArrayLength());
        var first = json.RootElement.GetProperty("countries")[0];
        Assert.Equal(10, first.GetProperty("outletsYtd").GetInt32());
        Assert.Equal(10_009m, first.GetProperty("kg").GetDecimal());
        Assert.Equal(10_008m, first.GetProperty("prevMonthKg").GetDecimal()); // август из месяцев года
        Assert.Equal("Страна экспорта 1", json.RootElement.GetProperty("factoryBranch").GetProperty("buyers")[0].GetProperty("country").GetString());
        Assert.Equal(["/sales/primary/export?year=2026&month=9", "/sales/export?year=2026&month=9"], result.Sources!.Select(s => s.Href));
    }

    [Fact]
    public void Pnl_expenses_are_gross_minus_ebit()
    {
        var values = PnlMath.Compute(new Dictionary<string, decimal>
        {
            [PnlMath.Sales] = 1000, [PnlMath.Cost] = 700, [PnlMath.Sm] = 50, [PnlMath.Retro] = 10, [PnlMath.ShipPlant] = 20, [PnlMath.Admin] = 30, [PnlMath.Other] = 5,
        }, new HashSet<string>());
        // 50 − 10 + 20 + 30 + 5 = 95: валовая 300, EBIT 205.
        Assert.Equal(95m, GetDealerPnlTool.ExpensesOf(values));
        Assert.Null(GetDealerPnlTool.ExpensesOf(PnlMath.Compute(new Dictionary<string, decimal>(), new HashSet<string>())));
    }
}

/// <summary>
/// Те же инструменты на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускаются): данные фикстуры DataManagerDb (сентябрь 2026:
/// Хоразм 900 кг, Бухоро 500 кг «Кекс 1 кг») плюс экспортный заказ «Завода» в Душанбе и P&L дилера Хоразма за август и сентябрь.
/// </summary>
public class SalesExtraToolsDatabaseTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const long ExportId = 7001;

    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    private ToolContext User(Guid id) => new("sales", id, 0);

    private static async Task SeedAsync(OneBaseDbContext db)
    {
        if (!await db.LinkoOrders.AnyAsync(o => o.Id == ExportId))
        {
            // Экспортная точка филиала «Завод» (тип EXPORT): страна — по названию.
            db.LinkoMarkets.Add(new LinkoMarket { Id = ExportId, Name = "Dushanbe Market", MarketTypeName = "EXPORT", BranchId = 4, BranchName = "Завод" });
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = ExportId, CreatedAt = new DateTime(2026, 9, 10, 8, 0, 0), CreatedDate = new DateOnly(2026, 9, 10),
                AcceptedAt = new DateTime(2026, 9, 11, 9, 0, 0), AcceptedDate = new DateOnly(2026, 9, 11), Status = "delivered",
                MarketId = ExportId, BranchId = 4, BranchName = "Завод", AgentId = 70, TotalWeight = 300, TotalPrice = 300 * 25_000,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine
            {
                Id = ExportId, OrderId = ExportId, ProductId = 100, Amount = 300, Price = 25_000, TotalPrice = 300 * 25_000, TotalWeight = 300,
            });
        }

        foreach (var (month, sales, cost, admin) in new[] { (8, 800_000m, 600_000m, 40_000m), (9, 1_000_000m, 700_000m, 50_000m) })
        {
            foreach (var (line, amount) in new[] { (PnlMath.Sales, sales), (PnlMath.Cost, cost), (PnlMath.Admin, admin) })
            {
                db.SalesPnlValues.Add(new SalesPnlValue
                {
                    Year = 2026, Month = month, Dealer = "Азизбек Хоразм", DealerKey = PnlMath.DealerKey("Азизбек Хоразм"), Line = line, Amount = amount,
                });
            }
        }

        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Tools_read_the_same_data_as_the_pages()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await SeedAsync(sp.GetRequiredService<OneBaseDbContext>());
        sp.GetRequiredService<SalesCacheSignal>().InvalidateHistory();
        var loader = sp.GetRequiredService<SalesDataLoader>();
        var admin = User(fixture.AdminId);

        // Продажи по SKU: вторичка без «Завода», регион — по названию, неизвестный регион — ошибка со списком.
        var sku = new GetSkuSalesTool(sp.GetRequiredService<SkuSalesService>(), loader);
        var all = await sku.ExecuteAsync(admin, Args(new { from = "2026-09", to = "2026-09" }));
        Assert.True(all.Success, all.Content);
        using (var json = JsonDocument.Parse(all.Content))
        {
            Assert.Equal(1400m, json.RootElement.GetProperty("totals").GetProperty("kg").GetDecimal());
            Assert.Equal("Кекс 1 кг", json.RootElement.GetProperty("skus")[0].GetProperty("name").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("skus")[0].GetProperty("regions").GetInt32());
        }

        Assert.Equal("/sales/sku-sales?from=2026-09&to=2026-09", all.Sources![0].Href);

        var khorazm = await sku.ExecuteAsync(admin, Args(new { from = "2026-09", to = "2026-09", region = "хоразм" }));
        Assert.True(khorazm.Success, khorazm.Content);
        using (var json = JsonDocument.Parse(khorazm.Content))
        {
            Assert.Equal(900m, json.RootElement.GetProperty("totals").GetProperty("kg").GetDecimal());
            Assert.Equal("Хоразм", json.RootElement.GetProperty("scope").GetString());
        }

        var wrong = await sku.ExecuteAsync(admin, Args(new { region = "Самарканд" }));
        Assert.False(wrong.Success);
        Assert.Contains("Региона «Самарканд» нет. Регионы:", wrong.Content);
        Assert.Contains("Хоразм", wrong.Content);
        Assert.Contains("Бухоро", wrong.Content);

        // Экспорт: страна из «Первички → Экспорт», продажи филиала «Завод» — из «Продажи → Экспорт и опт».
        var export = await new GetExportSalesTool(loader, sp.GetRequiredService<PrimaryService>()).ExecuteAsync(admin, Args(new { year = 2026, month = 9 }));
        Assert.True(export.Success, export.Content);
        using (var json = JsonDocument.Parse(export.Content))
        {
            Assert.Equal(300m, json.RootElement.GetProperty("shipments").GetProperty("kg").GetDecimal());
            Assert.Equal("Таджикистан", json.RootElement.GetProperty("countries")[0].GetProperty("country").GetString());
            Assert.Equal(300m, json.RootElement.GetProperty("factoryBranch").GetProperty("factKg").GetDecimal());
        }

        Assert.Equal(["/sales/primary/export?year=2026&month=9", "/sales/export?year=2026&month=9"], export.Sources!.Select(s => s.Href));

        // P&L: по умолчанию — последний месяц с продажами и расходами; изменение к прошлому месяцу; нет месяца — так и сказано.
        var pnl = new GetDealerPnlTool(sp.GetRequiredService<PnlService>(), sp.GetRequiredService<IUserPermissions>());
        var month = await pnl.ExecuteAsync(admin, Args(new { }));
        Assert.True(month.Success, month.Content);
        using (var json = JsonDocument.Parse(month.Content))
        {
            var total = json.RootElement.GetProperty("total");
            Assert.Equal(300_000m, total.GetProperty("gross").GetDecimal());
            Assert.Equal(250_000m, total.GetProperty("ebit").GetDecimal());
            Assert.Equal(25.0m, json.RootElement.GetProperty("totalVsPrevMonth").GetProperty("salesPct").GetDecimal());
            var dealer = json.RootElement.GetProperty("rm")[0].GetProperty("dealers")[0];
            Assert.Equal("Азизбек Хоразм", dealer.GetProperty("name").GetString());
            Assert.Equal("Хоразм", dealer.GetProperty("region").GetString());
        }

        Assert.Equal("/sales/pnl?pnl=2026-09", month.Sources![0].Href);

        var period = await pnl.ExecuteAsync(admin, Args(new { from = "2026-08", to = "2026-09" }));
        Assert.True(period.Success, period.Content);
        using (var json = JsonDocument.Parse(period.Content))
        {
            Assert.Equal(1_800_000m, json.RootElement.GetProperty("total").GetProperty("sales").GetDecimal());
            Assert.Equal(2, json.RootElement.GetProperty("byMonth").GetArrayLength());
        }

        var missing = await pnl.ExecuteAsync(admin, Args(new { year = 2026, month = 7 }));
        Assert.True(missing.Success, missing.Content);
        Assert.Contains("P&L дилеров за июль 2026 в OneBase не загружен", missing.Content);
        Assert.Contains("AI Data Manager", missing.Content);

        var unknown = await pnl.ExecuteAsync(admin, Args(new { dealer = "Андижон" }));
        Assert.False(unknown.Success);
        Assert.Contains("Дилеры: Азизбек Хоразм", unknown.Content);

        // Без права «Продажи» (только файлы и AI Data Manager) P&L не отдаётся.
        var noSales = await pnl.ExecuteAsync(User(fixture.DataAdminId), Args(new { }));
        Assert.False(noSales.Success);
    }
}

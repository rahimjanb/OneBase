using System.Text.Json;
using System.Text.Json.Nodes;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Field;
using OneBase.Application.Hr;

namespace OneBase.AI.Tests;

/// <summary>
/// P1 «полнота данных» без базы и модели: большой результат не режется посередине JSON — урезаются строки списков с «_pagination», остальные
/// строки отдаются страницами из того же снимка без повторов и пропусков; «_meta» помечает выборку Top-N неполной и хранит источники;
/// периоды (месяц, квартал, полугодие, год, неделя через границу месяца) режутся подряд без пересечений и пропусков; продажи за период
/// складываются по месяцам, а АКБ — нет (точки за весь период); HR длиннее квартала — частями по 92 дня; даты — по Ташкенту.
/// </summary>
public class DataCompletenessTests
{
    private const int Budget = 40_000;

    /// <summary>Как результаты инструментов данных: кириллица без экранирования.</summary>
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Rows(int count, int pad = 0) => JsonSerializer.Serialize(new
    {
        total = new { rows = count, kg = Enumerable.Range(0, count).Sum(Kg) },
        agents = Enumerable.Range(0, count).Select(i => new { id = i, name = $"ТП №{i} Хоразм" + new string('·', pad), kg = Kg(i) }),
    }, Relaxed);

    private static decimal Kg(int i) => i % 97 + 0.5m;

    private static JsonElement Args(object args) => JsonSerializer.SerializeToElement(args);

    private static JsonObject Json(string text) => Assert.IsType<JsonObject>(JsonNode.Parse(text));

    /// <summary>Первые строки из урезанного результата и все следующие — страницами get_more_rows по next_offset, как это делает модель.</summary>
    private static (List<JsonNode> Rows, int Pages) ReadAll(ToolResultPager pager, string fitted, string key, string? listPath = null)
    {
        var path = listPath ?? key;
        var first = Json(fitted);
        var rows = first[key]!.AsArray().Select(r => r!.DeepClone()).ToList();
        var pagination = first["_pagination"];
        if (pagination is null)
        {
            return (rows, 0);
        }

        var list = pagination["lists"]!.AsArray().Single(l => (string)l!["path"]! == path)!;
        Assert.Equal(rows.Count, (int)list["returned_count"]!);
        var (id, offset, more, pages) = ((string)pagination["result_id"]!, (int)list["next_offset"]!, (bool)list["has_more"]!, 0);
        while (more)
        {
            var page = pager.Page(Args(new { result_id = id, path, offset }), Budget);
            Assert.True(page.Success, page.Content);
            Assert.True(page.Content.Length <= Budget);
            var p = Json(page.Content);
            Assert.Equal(offset, (int)p["offset"]!);
            rows.AddRange(p["rows"]!.AsArray().Select(r => r!.DeepClone()));
            (offset, more) = ((int)p["next_offset"]!, (bool)p["has_more"]!);
            pages++;
        }

        return (rows, pages);
    }

    // ---------- Большие результаты и страницы ----------

    [Fact]
    public void Json_over_40000_chars_stays_valid_json_with_pagination_and_untouched_totals()
    {
        var content = Rows(1000, pad: 40);
        Assert.True(content.Length > Budget);
        var pager = new ToolResultPager();

        var fitted = pager.Fit(content, Budget, out var pages);

        Assert.True(fitted.Length <= Budget, $"{fitted.Length}");
        var root = Json(fitted); // не обрезан посередине: разбирается целиком
        Assert.Equal(1000, (int)root["total"]!["rows"]!);
        Assert.Equal(Enumerable.Range(0, 1000).Sum(Kg), (decimal)root["total"]!["kg"]!);
        var meta = root["_pagination"]!;
        Assert.True((bool)meta["truncated"]!);
        Assert.Equal("r1", (string)meta["result_id"]!);
        var list = Assert.Single(meta["lists"]!.AsArray())!;
        Assert.Equal(("agents", 1000, true), ((string)list["path"]!, (int)list["total_count"]!, (bool)list["has_more"]!));
        var shown = root["agents"]!.AsArray().Count;
        Assert.Equal(shown, (int)list["returned_count"]!);
        Assert.Equal(shown, (int)list["next_offset"]!);
        Assert.InRange(shown, 1, 999);
        Assert.Equal(Single(pages).ReturnedCount, shown);
        // Показаны первые строки подряд, а не случайные.
        Assert.Equal(Enumerable.Range(0, shown), root["agents"]!.AsArray().Select(a => (int)a!["id"]!));
    }

    private static ListPage Single(IReadOnlyList<ListPage> pages) => Assert.Single(pages);

    [Theory]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void Every_row_arrives_once_in_order_across_pages_and_sums_match(int count)
    {
        var pager = new ToolResultPager();
        var content = Rows(count, pad: 400);

        var (rows, pages) = ReadAll(pager, pager.Fit(content, Budget, out _), "agents");

        Assert.True(pages >= 1);
        Assert.Equal(count, rows.Count);
        var ids = rows.Select(r => (int)r["id"]!).ToList();
        Assert.Equal(Enumerable.Range(0, count), ids); // без повторов, пропусков и перестановок
        Assert.Equal(count, ids.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, count).Sum(Kg), rows.Sum(r => (decimal)r["kg"]!));
        Assert.Equal(1, pager.Stored); // страницы — из одного снимка, без нового запроса
    }

    [Fact]
    public void Nested_lists_are_paged_by_path_and_root_arrays_by_items()
    {
        var pager = new ToolResultPager();
        var nested = JsonSerializer.Serialize(new
        {
            regions = Enumerable.Range(0, 3).Select(r => new
            {
                name = $"Регион {r}",
                agents = Enumerable.Range(0, 400).Select(i => new { id = i, name = $"ТП {r}-{i} " + new string('·', 60) }),
            }),
        }, Relaxed);

        var fitted = Json(pager.Fit(nested, Budget, out var pages));
        Assert.Equal(3, fitted["regions"]!.AsArray().Count); // урезаются строки самых больших списков, регионы на месте
        Assert.All(pages, p => Assert.Matches(@"^regions\[\d\]\.agents$", p.Path));
        var page = Json(pager.Page(Args(new { result_id = "r1", path = "regions[2].agents", offset = 395 }), Budget).Content);
        Assert.Equal([395, 396, 397, 398, 399], page["rows"]!.AsArray().Select(a => (int)a!["id"]!));
        Assert.Equal(400, (int)page["total_count"]!);
        Assert.False((bool)page["has_more"]!);

        // Корень — массив: строки в «items», путь страниц — пустой (сам массив).
        var array = JsonSerializer.Serialize(Enumerable.Range(0, 2000).Select(i => new { id = i, name = new string('·', 40) }), Relaxed);
        var (rows, _) = ReadAll(pager, pager.Fit(array, Budget, out _), "items", "");
        Assert.Equal(Enumerable.Range(0, 2000), rows.Select(r => (int)r["id"]!));
    }

    [Fact]
    public void Small_results_pass_unchanged_and_text_is_cut_at_a_line_with_a_note()
    {
        var pager = new ToolResultPager();
        var small = Rows(10);
        Assert.Equal(small, pager.Fit(small, Budget, out var none));
        Assert.Empty(none);
        // Длинный только из-за экранирования кириллицы (\uXXXX) — компактной записью целиком, без страниц.
        var escaped = JsonSerializer.Serialize(new { agents = Enumerable.Range(0, 300).Select(i => new { name = $"Тошкент вилояти ТП {i}" }) });
        Assert.True(escaped.Length > 20_000);
        var compact = pager.Fit(escaped, 20_000, out var whole);
        Assert.Empty(whole);
        Assert.Equal(300, Json(compact)["agents"]!.AsArray().Count);
        Assert.Null(Json(compact)["_pagination"]);
        Assert.Equal(0, pager.Stored);

        var text = string.Join('\n', Enumerable.Range(0, 5000).Select(i => $"строка {i}"));
        var cut = pager.Fit(text, 10_000, out _);
        Assert.True(cut.Length <= 10_000);
        Assert.Contains("текст сокращён", cut);
        Assert.Matches(@"строка \d+\n…", cut); // по границе строки
    }

    [Fact]
    public void Pages_of_unknown_results_or_lists_are_errors_not_empty_data()
    {
        var pager = new ToolResultPager();
        pager.Fit(Rows(2000, pad: 20), Budget, out _);

        var unknown = pager.Page(Args(new { result_id = "r9", path = "agents", offset = 0 }), Budget);
        Assert.Equal((false, ToolFailures.NotFound), (unknown.Success, unknown.ErrorKind));
        var wrongPath = pager.Page(Args(new { result_id = "r1", path = "dealers", offset = 0 }), Budget);
        Assert.Equal((false, ToolFailures.InvalidArguments), (wrongPath.Success, wrongPath.ErrorKind));
        var past = Json(pager.Page(Args(new { result_id = "r1", path = "agents", offset = 5000 }), Budget).Content);
        Assert.Equal((0, false), ((int)past["returned_count"]!, (bool)past["has_more"]!));
    }

    [Fact]
    public void Same_call_with_reordered_arguments_is_a_repeat_and_not_queried_again()
    {
        var a = ToolResultPager.CallKey("get_sales", Args(new { year = 2026, month = 9, region = "Хоразм " }));
        var b = ToolResultPager.CallKey("get_sales", JsonDocument.Parse("""{ "region": "Хоразм", "month": 9, "year": 2026 }""").RootElement);
        var other = ToolResultPager.CallKey("get_sales", Args(new { year = 2026, month = 8, region = "Хоразм" }));
        Assert.Equal(a, b);
        Assert.NotEqual(a, other);

        var pager = new ToolResultPager();
        Assert.False(pager.TryRepeat(a, out _, out _));
        pager.Remember(a, "{\"factKg\":1}", "r1");
        Assert.True(pager.TryRepeat(b, out var content, out var id));
        Assert.Equal(("{\"factKg\":1}", "r1"), (content, id));
    }

    [Fact]
    public void Context_budget_shrinks_with_each_result_but_never_below_the_minimum()
    {
        Assert.Equal(40_000, AgentRunner.Budget(0));
        Assert.Equal(40_000, AgentRunner.Budget(150_000));
        Assert.Equal(30_000, AgentRunner.Budget(170_000));
        Assert.Equal(8_000, AgentRunner.Budget(199_000));
        Assert.Equal(8_000, AgentRunner.Budget(1_000_000));
    }

    // ---------- «_meta»: выборка, все строки, источники ----------

    [Fact]
    public void Top_n_is_marked_incomplete_with_total_count_and_all_rows_returns_everything()
    {
        var tool = new GetHrDayTool(null!, null!);
        var arguments = Args(new { region = "Хоразм" });
        var source = new DataSource("OneBase → Продажи → Хоразм", "01.09.2026 — 30.09.2026", "/sales/republic?year=2026&month=9");

        var top = new ToolArgs(arguments);
        var shown = top.Top(Enumerable.Range(0, 100), 30, "agents").ToList();
        var meta = Json(tool.WithMeta(new ToolResult(true, JsonSerializer.Serialize(new { agents = shown }), null, [source]), top, arguments).Content)["_meta"]!;

        Assert.Equal(30, shown.Count);
        Assert.False((bool)meta["complete"]!);
        var list = Assert.Single(meta["lists"]!.AsArray())!;
        Assert.Equal(("agents", 100, 30, true, true), ((string)list["name"]!, (int)list["total_count"]!, (int)list["returned_count"]!, (bool)list["truncated"]!, (bool)list["has_more"]!));
        Assert.Contains(DataTool.AllRowsArg, (string)meta["note"]!);
        // Источник, период, фильтры и единицы — на месте после сборки результата.
        Assert.Equal(("OneBase → Продажи → Хоразм", "01.09.2026 — 30.09.2026"), ((string)meta["sources"]![0]!["title"]!, (string)meta["sources"]![0]!["period"]!));
        Assert.Equal("Хоразм", (string)meta["filters"]!["region"]!);
        Assert.Equal("get_hr_day", (string)meta["tool"]!);
        Assert.NotNull(meta["units"]);

        var all = new ToolArgs(arguments) { All = true };
        var every = all.Top(Enumerable.Range(0, 100), 30, "agents").ToList();
        var full = Json(tool.WithMeta(ToolResult.Ok(JsonSerializer.Serialize(new { agents = every })), all, arguments).Content)["_meta"]!;
        Assert.Equal(100, every.Count);
        Assert.True((bool)full["complete"]!);
        Assert.Null(full["note"]);

        var counted = new ToolArgs(arguments);
        counted.Count("orders", 250, 50);
        Assert.False((bool)Json(tool.WithMeta(ToolResult.Ok("{}"), counted, arguments).Content)["_meta"]!["complete"]!);
        Assert.Equal("ОШИБКА", tool.WithMeta(ToolResult.Error("ОШИБКА"), counted, arguments).Content); // ошибки — как есть
    }

    [Fact]
    public void Every_data_tool_schema_gets_the_all_rows_switch()
    {
        var schema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { region = new { type = "string" } }, required = new[] { "region" } });

        var with = AgentRunner.WithAllRows(schema);

        Assert.Equal("boolean", with.GetProperty("properties").GetProperty(DataTool.AllRowsArg).GetProperty("type").GetString());
        Assert.True(with.GetProperty("properties").TryGetProperty("region", out _));
        Assert.Equal("region", with.GetProperty("required")[0].GetString());
    }

    // ---------- Периоды ----------

    [Theory]
    [InlineData("2026-09", "2026-09-01", "2026-09-30")]
    [InlineData("2026-Q3", "2026-07-01", "2026-09-30")]
    [InlineData("2026-К4", "2026-10-01", "2026-12-31")]
    [InlineData("2026-H1", "2026-01-01", "2026-06-30")]
    [InlineData("2026 П2", "2026-07-01", "2026-12-31")]
    [InlineData("2026", "2026-01-01", "2026-12-31")]
    [InlineData("2026-09-25..2026-10-10", "2026-09-25", "2026-10-10")]
    [InlineData("2026-10-10 — 2026-09-25", "2026-09-25", "2026-10-10")]
    public void Periods_parse_months_quarters_halves_years_and_spans(string text, string from, string to)
    {
        Assert.Equal(new DateRange(DateOnly.Parse(from), DateOnly.Parse(to)), PeriodRanges.Parse(text));
    }

    [Theory]
    [InlineData("сентябрь")]
    [InlineData("2026-13")]
    [InlineData("2026-Q5")]
    [InlineData("")]
    public void Unknown_periods_are_not_guessed(string text) => Assert.Null(PeriodRanges.Parse(text));

    [Fact]
    public void Leap_year_quarter_and_half_lengths_are_exact()
    {
        Assert.Equal(366, PeriodRanges.Parse("2028")!.Days);
        Assert.Equal(92, PeriodRanges.Parse("2026-Q3")!.Days);
        Assert.Equal(90, PeriodRanges.Parse("2026-Q1")!.Days);
        Assert.Equal(91, PeriodRanges.Parse("2028-Q1")!.Days);
        Assert.Equal(181, PeriodRanges.Parse("2026-H1")!.Days);
    }

    [Fact]
    public void Span_across_a_month_boundary_splits_into_contiguous_months_and_iso_weeks()
    {
        var range = new DateRange(new DateOnly(2026, 9, 25), new DateOnly(2026, 10, 10));

        var months = PeriodRanges.ByMonth(range);
        Assert.Equal([new DateRange(new(2026, 9, 25), new(2026, 9, 30)), new DateRange(new(2026, 10, 1), new(2026, 10, 10))], months);
        PeriodRanges.EnsureContiguous(months, range);

        // 25.09.2026 — пятница: неделя с понедельника 28.09 по воскресенье 04.10 — одна часть через границу месяца.
        var weeks = PeriodRanges.ByWeek(range);
        Assert.Equal([new DateRange(new(2026, 9, 25), new(2026, 9, 27)), new DateRange(new(2026, 9, 28), new(2026, 10, 4)), new DateRange(new(2026, 10, 5), new(2026, 10, 10))], weeks);
        Assert.Equal(DayOfWeek.Monday, weeks[1].From.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, weeks[1].To.DayOfWeek);
        PeriodRanges.EnsureContiguous(weeks, range);
        Assert.Equal(range.Days, weeks.Sum(w => w.Days));
    }

    [Fact]
    public void Year_splits_into_quarter_chunks_without_overlaps_or_gaps_and_bad_parts_are_rejected()
    {
        var year = PeriodRanges.Parse("2026")!;
        var chunks = PeriodRanges.Chunks(year, 92);
        Assert.Equal([92, 92, 92, 89], chunks.Select(c => c.Days));
        PeriodRanges.EnsureContiguous(chunks, year);

        var sep = PeriodRanges.MonthRange(2026, 9);
        Assert.Throws<InvalidOperationException>(() => PeriodRanges.EnsureContiguous(
            [new DateRange(new(2026, 9, 1), new(2026, 9, 15)), new DateRange(new(2026, 9, 15), new(2026, 9, 30))], sep)); // пересечение
        Assert.Throws<InvalidOperationException>(() => PeriodRanges.EnsureContiguous(
            [new DateRange(new(2026, 9, 1), new(2026, 9, 14)), new DateRange(new(2026, 9, 16), new(2026, 9, 30))], sep)); // пропуск
        Assert.Throws<InvalidOperationException>(() => PeriodRanges.EnsureContiguous([new DateRange(new(2026, 9, 1), new(2026, 9, 29))], sep)); // не до конца
        Assert.Throws<InvalidOperationException>(() => PeriodRanges.EnsureContiguous([], sep));
    }

    [Fact]
    public void Comparison_periods_are_the_previous_months_or_the_same_length_and_a_year_ago()
    {
        Assert.Equal(PeriodRanges.Parse("2026-Q2"), PeriodRanges.Previous(PeriodRanges.Parse("2026-Q3")!));
        Assert.Equal(PeriodRanges.MonthRange(2026, 8), PeriodRanges.Previous(PeriodRanges.MonthRange(2026, 9)));
        Assert.Equal(PeriodRanges.MonthRange(2026, 2), PeriodRanges.Previous(PeriodRanges.MonthRange(2026, 3))); // 28 дней, а не 31
        Assert.Equal(new DateRange(new(2026, 9, 9), new(2026, 9, 24)), PeriodRanges.Previous(new DateRange(new(2026, 9, 25), new(2026, 10, 10))));
        Assert.Equal(PeriodRanges.Parse("2025-Q3"), PeriodRanges.YearAgo(PeriodRanges.Parse("2026-Q3")!));
        Assert.Equal(PeriodRanges.MonthRange(2027, 2), PeriodRanges.YearAgo(PeriodRanges.MonthRange(2028, 2))); // 29 февраля → 28-е
        Assert.Equal(new DateRange(new(2025, 9, 25), new(2025, 10, 10)), PeriodRanges.YearAgo(new DateRange(new(2026, 9, 25), new(2026, 10, 10))));
    }

    [Fact]
    public void Sales_period_arguments_accept_up_to_a_year_with_inclusive_bounds()
    {
        var today = new DateOnly(2026, 10, 30);
        Assert.Equal(PeriodRanges.Parse("2026-Q3"), GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { period = "2026-Q3" })), today));
        Assert.Equal(new DateRange(new(2026, 9, 25), new(2026, 10, 10)),
            GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { from = "2026-09-25", to = "2026-10-10" })), today));
        Assert.Equal(new DateRange(new(2026, 10, 1), today), GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { from = "2026-10-01" })), today));
        Assert.Equal(16, GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { period = "2026-09-25..2026-10-10" })), today).Days); // обе границы входят

        Assert.Contains("366", Assert.Throws<ToolArgumentException>(() =>
            GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { from = "2025-01-01", to = "2026-10-10" })), today)).Message);
        Assert.Throws<ToolArgumentException>(() => GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { from = "2026-10-10", to = "2026-09-25" })), today));
        Assert.Throws<ToolArgumentException>(() => GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { period = "осень" })), today));
        Assert.Throws<ToolArgumentException>(() => GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { })), today));

        var q3 = PeriodRanges.Parse("2026-Q3")!;
        Assert.Equal(PeriodRanges.Parse("2026-Q2"), GetSalesPeriodTool.CompareOf("previous", q3));
        Assert.Equal(PeriodRanges.Parse("2025-Q3"), GetSalesPeriodTool.CompareOf("year_ago", q3));
        Assert.Equal(PeriodRanges.MonthRange(2026, 8), GetSalesPeriodTool.CompareOf("2026-08", q3));
        Assert.Null(GetSalesPeriodTool.CompareOf(null, q3));
        Assert.Throws<ToolArgumentException>(() => GetSalesPeriodTool.CompareOf("прошлый раз", q3));

        Assert.Equal(20m, GetSalesPeriodTool.Change(120, 100));
        Assert.Equal(-25m, GetSalesPeriodTool.Change(90, 120));
        Assert.Equal(50m, GetSalesPeriodTool.Change(-5, -10));
        Assert.Null(GetSalesPeriodTool.Change(5, 0));
    }

    // ---------- Часовой пояс ----------

    [Fact]
    public void Today_is_taken_in_tashkent_so_late_utc_evening_is_already_the_next_month()
    {
        Assert.Equal(TimeSpan.FromHours(5), GetSalesPeriodTool.CompanyOffset);
        Assert.Equal(FieldClock.Offset, GetSalesPeriodTool.CompanyOffset);

        // 30.09.2026 19:30 UTC — в Ташкенте уже 01.10.2026 00:30.
        var clock = new TestClock { Now = new DateTimeOffset(2026, 9, 30, 19, 30, 0, TimeSpan.Zero) };
        var today = GetSalesPeriodTool.Today(clock);
        Assert.Equal(new DateOnly(2026, 10, 1), today);
        Assert.Equal(new DateRange(new(2026, 10, 1), new(2026, 10, 1)), GetSalesPeriodTool.RangeOf(new ToolArgs(Args(new { from = "2026-10-01" })), today));
        Assert.Equal(new DateOnly(2026, 9, 30), GetSalesPeriodTool.Today(new TestClock { Now = new DateTimeOffset(2026, 9, 30, 18, 59, 0, TimeSpan.Zero) }));

        var message = AgentRunner.UserMessage(new AgentTask("sales", "Продажи за сегодня?", Guid.Empty), clock.GetUtcNow().ToOffset(GetSalesPeriodTool.CompanyOffset));
        Assert.StartsWith("Сегодня 01.10.2026, 00:30 по Ташкенту.", message);
    }

    // ---------- Продажи за период: месяцы, недели, АКБ, покрытие ----------

    private static readonly Guid Khorazm = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Bukhara = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private sealed record Line(DateOnly Date, long Market, decimal Kg, decimal Revenue, long Agent = 1, Guid? Region = null);

    /// <summary>Месяц продаж из строк: отрезок и вес по точкам считаются по строкам, как RouteSplit — по заказам месяца.</summary>
    private sealed class FakeMonth(DateOnly dataThrough, params Line[] lines) : ISalesMonth
    {
        public DateOnly DataThrough => dataThrough;

        private IEnumerable<Line> In(DateOnly from, DateOnly to, SalesScope scope) => lines.Where(l => l.Date >= from && l.Date <= to
            && (scope.Region is null || (l.Region ?? Khorazm) == scope.Region) && (scope.Agent is null || l.Agent == scope.Agent));

        private static SalesFigures Figures(IEnumerable<Line> ls) =>
            new(ls.Count(l => l.Kg > 0), ls.Sum(l => l.Revenue), ls.Sum(l => l.Kg), -ls.Where(l => l.Kg < 0).Sum(l => l.Kg));

        public SalesSlice Slice(DateOnly from, DateOnly to, SalesScope scope, bool agents)
        {
            var ls = In(from, to, scope).ToList();
            return new SalesSlice(
                Figures(ls),
                ls.GroupBy(l => l.Date).Select(g => new SalesDayFigures(g.Key, g.Sum(l => l.Kg), g.Sum(l => l.Revenue), g.Count(l => l.Kg > 0),
                    g.GroupBy(l => l.Market).Count(m => m.Sum(l => l.Kg) > 0))).ToList(),
                ls.GroupBy(l => l.Region ?? Khorazm).Select(g => new SalesRowFigures(g.Key.ToString(), g.Key == Khorazm ? "Хоразм" : "Бухара", null, Figures(g))).ToList(),
                agents ? ls.GroupBy(l => l.Agent).Select(g => new SalesRowFigures(g.Key.ToString(), $"ТП {g.Key}", "Хоразм", Figures(g))).ToList() : []);
        }

        public IReadOnlyDictionary<long, decimal> Markets(DateOnly from, DateOnly to, SalesScope scope) =>
            In(from, to, scope).GroupBy(l => l.Market).ToDictionary(g => g.Key, g => g.Sum(l => l.Kg));
    }

    private static Func<int, int, CancellationToken, Task<ISalesMonth?>> Months(Dictionary<(int, int), FakeMonth> months, List<(int, int)>? loaded = null) =>
        (y, m, _) =>
        {
            loaded?.Add((y, m));
            return Task.FromResult<ISalesMonth?>(months.GetValueOrDefault((y, m)));
        };

    private static Task<PeriodSales> Aggregate(DateRange range, Dictionary<(int, int), FakeMonth> months, SalesScope? scope = null, bool agents = false) =>
        SalesPeriodEngine.AggregateAsync(range, scope ?? new SalesScope(), Months(months), regions: true, agents, CancellationToken.None);

    [Fact]
    public void Akb_is_the_count_of_markets_with_a_positive_net_weight()
    {
        Assert.Equal(2, SalesPeriodEngine.Akb(new Dictionary<long, decimal> { [1] = 10, [2] = 0.5m, [3] = 0, [4] = -2 }));
    }

    [Fact]
    public async Task Sales_across_a_month_boundary_sum_the_parts_but_akb_counts_markets_of_the_whole_period()
    {
        var months = new Dictionary<(int, int), FakeMonth>
        {
            [(2026, 9)] = new(new(2026, 9, 30), new Line(new(2026, 9, 29), 1, 10, 100), new Line(new(2026, 9, 30), 2, 5, 50), new Line(new(2026, 9, 30), 4, 5, 40)),
            [(2026, 10)] = new(new(2026, 10, 29), new Line(new(2026, 10, 1), 1, 20, 200), new Line(new(2026, 10, 2), 3, 8, 80), new Line(new(2026, 10, 2), 4, -5, -40)),
        };
        var range = new DateRange(new(2026, 9, 29), new(2026, 10, 2));

        var p = await Aggregate(range, months);

        Assert.True(p.Complete);
        Assert.Empty(p.Missing);
        Assert.Equal(new SalesFigures(5, 430, 43, 5), p.Total);
        // Точки 1, 2, 3; точка 4 вернула всё в октябре — за период её чистый вес 0. Сумма АКБ месяцев (3 + 2) была бы 5.
        Assert.Equal(3, p.Akb);
        Assert.Equal([3, 2], p.Months.Select(m => m.Akb));
        Assert.Equal([20m, 23m], p.Months.Select(m => m.Figures.Kg)); // 10 + 5 + 5 · 20 + 8 − 5
        Assert.Equal(p.Total.Kg, p.Months.Sum(m => m.Figures.Kg));
        Assert.Equal(range.Days, p.Days.Select(d => d.Date).Distinct().Count());
        Assert.Equal(new DateOnly(2026, 10, 2), p.DataThrough);
    }

    [Fact]
    public async Task Week_across_a_month_boundary_is_one_week_with_its_own_akb()
    {
        var months = new Dictionary<(int, int), FakeMonth>
        {
            [(2026, 9)] = new(new(2026, 9, 30), new Line(new(2026, 9, 28), 1, 10, 100), new Line(new(2026, 9, 30), 2, 5, 50)),
            [(2026, 10)] = new(new(2026, 10, 29), new Line(new(2026, 10, 2), 1, 20, 200), new Line(new(2026, 10, 4), 3, 8, 80), new Line(new(2026, 10, 5), 3, 1, 10)),
        };

        var p = await Aggregate(new DateRange(new(2026, 9, 28), new(2026, 10, 5)), months);

        Assert.Equal(2, p.Weeks.Count);
        var week = p.Weeks[0];
        Assert.Equal(new DateRange(new(2026, 9, 28), new(2026, 10, 4)), week.Range);
        Assert.Equal((43m, 430m, 4, 3), (week.Figures.Kg, week.Figures.Revenue, week.Figures.Orders, week.Akb)); // точки 1, 2, 3 — не 2 + 2
        Assert.False(week.Missing);
        Assert.Equal((1m, 1), (p.Weeks[1].Figures.Kg, p.Weeks[1].Akb));
        Assert.Equal(p.Total.Kg, p.Weeks.Sum(w => w.Figures.Kg));
    }

    [Fact]
    public async Task Two_and_three_months_compare_month_by_month_and_each_month_is_loaded_once()
    {
        var months = new Dictionary<(int, int), FakeMonth>
        {
            [(2026, 7)] = new(new(2026, 7, 31), new Line(new(2026, 7, 10), 1, 100, 1_000)),
            [(2026, 8)] = new(new(2026, 8, 31), new Line(new(2026, 8, 10), 1, 120, 1_300), new Line(new(2026, 8, 11), 2, 0, 0)),
            [(2026, 9)] = new(new(2026, 9, 30), new Line(new(2026, 9, 10), 2, 90, 900)),
        };
        var loaded = new List<(int, int)>();

        var three = await SalesPeriodEngine.AggregateAsync(new DateRange(new(2026, 7, 1), new(2026, 9, 30)), new SalesScope(), Months(months, loaded),
            regions: true, agents: false, CancellationToken.None);
        var two = await Aggregate(new DateRange(new(2026, 8, 1), new(2026, 9, 30)), months);

        Assert.Equal([(2026, 7), (2026, 8), (2026, 9)], loaded);
        Assert.Equal([100m, 120m, 90m], three.Months.Select(m => m.Figures.Kg));
        Assert.Equal(310m, three.Total.Kg);
        Assert.Equal(2, three.Akb);
        Assert.Equal(20m, GetSalesPeriodTool.Change(three.Months[1].Figures.Kg, three.Months[0].Figures.Kg));
        Assert.Equal(-25m, GetSalesPeriodTool.Change(three.Months[2].Figures.Kg, three.Months[1].Figures.Kg));
        Assert.Equal([120m, 90m], two.Months.Select(m => m.Figures.Kg));
        Assert.Equal(210m, two.Total.Kg);
    }

    [Fact]
    public async Task Missing_month_and_days_after_the_data_mark_the_period_incomplete()
    {
        var months = new Dictionary<(int, int), FakeMonth>
        {
            [(2026, 7)] = new(new(2026, 7, 31), new Line(new(2026, 7, 10), 1, 100, 1_000)),
            [(2026, 9)] = new(new(2026, 9, 30), new Line(new(2026, 9, 10), 1, 90, 900)),
            [(2026, 10)] = new(new(2026, 10, 15), new Line(new(2026, 10, 10), 1, 10, 100), new Line(new(2026, 10, 20), 1, 99, 990)),
        };

        var p = await Aggregate(new DateRange(new(2026, 7, 1), new(2026, 10, 31)), months);

        Assert.False(p.Complete);
        Assert.Equal([PeriodRanges.MonthRange(2026, 8), new DateRange(new(2026, 10, 16), new(2026, 10, 31))], p.Missing);
        Assert.Equal(200m, p.Total.Kg); // только дни с данными; строка 20.10 — после DataThrough
        Assert.Equal(new DateOnly(2026, 10, 15), p.DataThrough);
        var august = p.Months[1];
        Assert.True(august.Missing);
        Assert.Equal(SalesFigures.Zero, august.Figures);
        var october = p.Months[3];
        Assert.True(october.Partial);
        Assert.False(october.Missing);
        Assert.True(p.Weeks.Single(w => w.Range.From == new DateOnly(2026, 8, 3)).Missing);
    }

    [Fact]
    public async Task Region_and_agent_rows_add_up_across_months_with_period_akb()
    {
        var months = new Dictionary<(int, int), FakeMonth>
        {
            [(2026, 9)] = new(new(2026, 9, 30), new Line(new(2026, 9, 29), 1, 10, 100, Agent: 7), new Line(new(2026, 9, 30), 5, 3, 30, Agent: 8, Region: Bukhara)),
            [(2026, 10)] = new(new(2026, 10, 29), new Line(new(2026, 10, 1), 1, 20, 200, Agent: 7), new Line(new(2026, 10, 1), 2, 4, 40, Agent: 7)),
        };

        var p = await Aggregate(new DateRange(new(2026, 9, 29), new(2026, 10, 1)), months, agents: true);

        var khorazm = p.Regions.Single(r => r.Id == Khorazm.ToString());
        Assert.Equal((34m, 2), (khorazm.Figures.Kg, khorazm.Akb)); // точки 1 и 2, точка 1 — в обоих месяцах, считается один раз
        Assert.Equal((3m, 1), (p.Regions.Single(r => r.Id == Bukhara.ToString()).Figures.Kg, p.Regions.Single(r => r.Id == Bukhara.ToString()).Akb));
        var agent = p.Agents.Single(a => a.Id == "7");
        Assert.Equal((34m, 340m, 3, 2), (agent.Figures.Kg, agent.Figures.Revenue, agent.Figures.Orders, agent.Akb));
        Assert.Equal(p.Total.Kg, p.Regions.Sum(r => r.Figures.Kg));
        Assert.Equal(p.Total.Kg, p.Agents.Sum(a => a.Figures.Kg));

        // У региона строки регионов не дублируют область.
        var region = await Aggregate(new DateRange(new(2026, 9, 29), new(2026, 10, 1)), months, new SalesScope(Region: Khorazm));
        Assert.Empty(region.Regions);
        Assert.Equal(34m, region.Total.Kg);
    }

    // ---------- HR длиннее квартала ----------

    private static HrSummary Summary(int plan, int present, int late, int lateMinutes, int worked) =>
        new(plan, present, late, lateMinutes, 0, 0, plan - present, 0, 0, 0, worked, plan * 480);

    private static HrTimesheet Sheet(DateOnly from, int days, params HrTimesheetRow[] rows) =>
        new(from, from.AddDays(days - 1), Enumerable.Range(0, days).Select(from.AddDays).ToList(), rows, HrPeriods.Sum(rows.Select(r => r.Summary)),
            new HrSyncBrief(true, true, false, null, null, null, null, null, null, null));

    private static HrTimesheetRow Person(Guid id, string name, HrSummary s, params string[] codes) => new(id, name, "Отдел продаж", "Агент", s, codes);

    [Fact]
    public void Hr_periods_up_to_a_quarter_are_one_call_and_up_to_a_year_are_92_day_parts()
    {
        Assert.Equal(92, HrPeriods.ChunkDays);
        Assert.Equal(HrService.MaxReportDays, HrPeriods.ChunkDays);
        Assert.Equal(62, HrService.MaxPeriodDays); // предел страниц и API не меняется

        var quarter = HrPeriods.Split(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30))!;
        Assert.Equal([PeriodRanges.Parse("2026-Q3")], quarter);
        var year = HrPeriods.Split(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))!;
        Assert.Equal([92, 92, 92, 89], year.Select(p => p.Days));
        PeriodRanges.EnsureContiguous(year, PeriodRanges.Parse("2026")!);
        Assert.Equal(year, HrPeriods.Split(new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1))); // перепутанные границы
        Assert.Null(HrPeriods.Split(null, new DateOnly(2026, 10, 21))); // умолчание — у HrService
        Assert.Equal(FieldClock.Today, HrPeriods.Split(FieldClock.Today.AddDays(-5), null)![0].To);
        Assert.Contains("366", Assert.Throws<ToolArgumentException>(() => HrPeriods.Split(new DateOnly(2025, 1, 1), new DateOnly(2026, 10, 21))).Message);
    }

    [Fact]
    public void Hr_timesheets_of_parts_merge_into_one_period_with_summed_totals_and_day_codes_in_order()
    {
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var first = Sheet(new DateOnly(2026, 9, 29), 3,
            Person(a, "Алиев Бобур", Summary(3, 3, 1, 20, 1400), "ОП", "Я", "Я"),
            Person(b, "Уволенный Пётр", Summary(2, 1, 0, 0, 480), "Я", "ОТС", ""));
        var second = Sheet(new DateOnly(2026, 10, 2), 2,
            Person(a, "Алиев Бобур", Summary(2, 2, 0, 0, 960), "Я", "Я") with { Division = "Бухгалтерия" },
            Person(c, "Новый Сардор", Summary(1, 1, 1, 5, 470), "", "ОП"));

        var merged = HrPeriods.Merge([first, second]);

        Assert.Equal((new DateOnly(2026, 9, 29), new DateOnly(2026, 10, 3)), (merged.From, merged.To));
        Assert.Equal(Enumerable.Range(0, 5).Select(new DateOnly(2026, 9, 29).AddDays), merged.Dates);
        var aliev = merged.Rows.Single(r => r.EmployeeId == a);
        Assert.Equal(["ОП", "Я", "Я", "Я", "Я"], aliev.Codes);
        Assert.Equal(Summary(5, 5, 1, 20, 2360), aliev.Summary);
        Assert.Equal("Бухгалтерия", aliev.Division); // как сейчас — по последней части
        Assert.Equal(["Я", "ОТС", "", "", ""], merged.Rows.Single(r => r.EmployeeId == b).Codes); // уволен — дальше пусто
        Assert.Equal(["", "", "", "", "ОП"], merged.Rows.Single(r => r.EmployeeId == c).Codes); // принят позже — раньше пусто
        Assert.All(merged.Rows, r => Assert.Equal(merged.Dates.Count, r.Codes.Count));
        Assert.Equal(HrPeriods.Add(first.Total, second.Total), merged.Total);
        Assert.Equal(HrPeriods.Sum(merged.Rows.Select(r => r.Summary)), merged.Total);
        Assert.Same(first, HrPeriods.Merge([first]));

        Assert.Throws<InvalidOperationException>(() => HrPeriods.Merge([first, Sheet(new DateOnly(2026, 10, 1), 2)])); // пересечение
        Assert.Throws<InvalidOperationException>(() => HrPeriods.Merge([first, Sheet(new DateOnly(2026, 10, 3), 2)])); // пропуск
    }

    [Fact]
    public void Hr_deviations_of_parts_merge_newest_first_and_a_case_outside_its_part_is_an_error()
    {
        HrDeviation Case(DateOnly date, string employee) => new(date, Guid.Empty, employee, null, "late", 10, null, null, null, null, null);
        var parts = HrPeriods.Split(new DateOnly(2026, 6, 1), new DateOnly(2026, 10, 1))!;
        Assert.Equal(2, parts.Count);

        var merged = HrPeriods.Merge(parts, [[Case(new(2026, 6, 2), "Б"), Case(new(2026, 6, 2), "А")], [Case(new(2026, 9, 30), "В")]]);

        Assert.Equal([("В", new DateOnly(2026, 9, 30)), ("А", new DateOnly(2026, 6, 2)), ("Б", new DateOnly(2026, 6, 2))], merged.Select(d => (d.Employee, d.Date)));
        Assert.Throws<InvalidOperationException>(() => HrPeriods.Merge(parts, [[Case(new(2026, 9, 30), "В")], []]));
        Assert.Throws<ArgumentException>(() => HrPeriods.Merge(parts, [[]]));
    }
}

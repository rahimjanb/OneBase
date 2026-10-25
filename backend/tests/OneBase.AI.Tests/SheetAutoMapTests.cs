using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.DataSources;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.DataSources;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// ИИ-сопоставление столбцов Google Sheets с полями OneBase без базы: что модель видит, как разбирается её ответ, что отклоняется
/// (несуществующее поле, некорректный JSON, факт Linko, занятое поле), что требует подтверждения (неоднозначно, финансы, средняя
/// уверенность) и как из принятых соответствий собирается обычная привязка со снимком заголовков.
/// </summary>
public sealed class SheetMappingTests
{
    private static readonly DataCategoryRef[] Categories = [new(10, "Кекс", ["Кексы"]), new(4, "Бамбук", [])];

    private static SheetMappingRequest Request(string section, IReadOnlyDictionary<string, string>? blocked = null, params (string Header, string[] Samples)[] columns)
    {
        var info = DataSourceCatalog.Section(section)!;
        return new SheetMappingRequest(section, info.Name, "План 2026", "Лист1", ["Лист1"], 1,
            columns.Select((c, i) => new SheetColumnSample(DataValues.ColumnLetter(i), c.Header, c.Samples)).ToList(),
            info.Fields.Select(f => DataSourceCatalog.Field(f)!).SelectMany(MappingRoles.For).ToList(),
            Categories, blocked ?? new Dictionary<string, string>(), [], new DateOnly(2026, 10, 17));
    }

    internal static SheetMappingRequest Primary(IReadOnlyDictionary<string, string>? blocked = null) => Request("primary", blocked,
        ("Регион", ["Хоразм", "Бухоро"]), ("Категория", ["Кекс", "Бамбук"]), ("Месяц", ["сентябрь 2026", "сентябрь 2026"]),
        ("План, кг", ["1000", "500"]), ("Дилер", ["Азизбек Хоразм", "Мехриддин Бухоро"]), ("Фактические продажи", ["980", "450"]));

    private static RawSuggestion S(string column, string? target, double confidence, string? transform = null, string? category = null, bool confirm = false) =>
        new(column, target, confidence, "по заголовку и значениям", transform, category, confirm);

    private static RawMapping Raw(params RawSuggestion[] s) => new(s, [], [], "fake/mapper");

    private static readonly SheetMappingOptions Options = new();

    [Fact]
    public void The_model_sees_headers_samples_and_only_real_onebase_fields()
    {
        var json = JsonDocument.Parse(SheetMappingAdvisor.Request(Primary())).RootElement;
        Assert.Equal(["Регион", "Категория", "Месяц", "План, кг", "Дилер", "Фактические продажи"],
            json.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("header").GetString()));
        Assert.Equal("1000", json.GetProperty("columns")[3].GetProperty("samples")[0].GetString());
        var targets = json.GetProperty("targets").EnumerateArray().Select(t => t.GetProperty("key").GetString()).ToList();
        Assert.Equal(["plan_primary.region", "plan_primary.category", "plan_primary.value", "plan_primary.category_value", "plan_primary.month",
            "plan_primary.year", "dealer.region", "dealer.value"], targets);
        Assert.Contains(json.GetProperty("linkoOwns").EnumerateArray(), x => x.GetString()!.Contains("факт продаж"));
        Assert.Contains("Linko", SheetMappingAdvisor.Prompt);
    }

    [Fact]
    public async Task Advisor_parses_the_model_answer_and_rejects_text_that_is_not_json()
    {
        var gateway = new FakeGateway();
        gateway.Script.Enqueue(_ => """
            Вот сопоставление:
            ```json
            {"suggestions":[{"sourceColumn":"D","targetField":"plan_primary.value","confidence":"0,94","reason":"план в кг","transformType":"none",
              "requiresConfirmation":false,"warnings":[]}],"fields":[{"field":"plan_primary","monthSource":"column"}],"notes":["лист — план первички"]}
            ```
            """);
        var advisor = new SheetMappingAdvisor(gateway);
        var raw = await advisor.AdviseAsync(Primary(), Guid.NewGuid());
        var s = Assert.Single(raw.Suggestions);
        Assert.Equal(("D", "plan_primary.value", 0.94), (s.SourceColumn, s.TargetField, s.Confidence));
        Assert.Equal("column", Assert.Single(raw.Fields).MonthSource);
        Assert.Equal("fake/mapper", raw.Model);
        var (messages, options) = Assert.Single(gateway.Calls);
        Assert.True(options.JsonOutput);
        Assert.Contains("План, кг", messages[1].Content);
        Assert.Contains("plan_primary.value", messages[1].Content);

        gateway.Script.Enqueue(_ => "Не могу помочь с этим листом.");
        var notJson = await Assert.ThrowsAsync<SheetMappingException>(() => advisor.AdviseAsync(Primary(), Guid.NewGuid()));
        Assert.Contains("не в формате JSON", notJson.Message);
        gateway.Script.Enqueue(_ => """{"answer":"нет"}""");
        await Assert.ThrowsAsync<SheetMappingException>(() => advisor.AdviseAsync(Primary(), Guid.NewGuid()));

        // ИИ не настроен — понятная ошибка, а не пустое «успешное» сопоставление.
        var noKey = await Assert.ThrowsAsync<SheetMappingException>(() => advisor.AdviseAsync(Primary(), Guid.NewGuid()));
        Assert.Contains("нет ключа", noKey.Message);
    }

    [Fact]
    public void Clear_answer_becomes_a_binding_with_a_header_snapshot()
    {
        var raw = new RawMapping([
            S("A", "plan_primary.region", 0.95), S("A", "dealer.region", 0.9), S("B", "plan_primary.category", 0.9), S("C", "plan_primary.month", 0.93, "month_text"),
            S("D", "plan_primary.value", 0.94), S("E", "dealer.value", 0.9), S("F", null, 0),
        ], [new RawFieldSetting("plan_primary", "column", null, null)], [], "fake/mapper");
        var proposal = SheetMapping.Validate(Primary(), raw, Options);

        Assert.Equal(6, proposal.Suggestions.Count(s => s.Status == MappingStatuses.Accepted));
        Assert.Equal(MappingStatuses.Unmatched, proposal.Suggestions.Single(s => s.SourceColumn == "F").Status);
        var plan = proposal.Fields.Single(f => f.Field == DataSourceCatalog.PlanPrimary);
        Assert.Equal(("Регион", "Категория", "План, кг", "Месяц", 2026, 1m), (plan.Plan!.RegionColumn, plan.Plan.CategoryColumn, plan.Plan.ValueColumn,
            plan.Plan.MonthColumn, plan.Plan.Year, plan.Plan.Multiplier));
        Assert.Equal("План, кг", plan.Headers!["План, кг"]);
        Assert.Null(DataSourceRules.FieldsError("primary", proposal.Fields)); // та же модель привязки, что у ручного редактора
        var dealer = proposal.Fields.Single(f => f.Field == DataSourceCatalog.Dealer);
        Assert.Equal(("Регион", "Дилер"), (dealer.Profile!.RegionColumn, dealer.Profile.ValueColumn));
    }

    [Fact]
    public void Unknown_fields_facts_ambiguity_low_confidence_and_taken_fields_are_never_auto_accepted()
    {
        var blocked = new Dictionary<string, string> { [DataSourceCatalog.Dealer] = "поле уже ведёт привязка «Дилеры»" };
        var proposal = SheetMapping.Validate(Primary(blocked), Raw(
            S("F", "sales_actual.value", 0.97),                 // поля нет в OneBase — ИИ не может его создать
            S("F", "plan_primary.value", 0.96),                 // «Фактические продажи» → план: похоже на факт, только с проверкой
            S("D", "plan_primary.value", 0.95),                 // и «План, кг» туда же — неоднозначно
            S("A", "plan_primary.region", 0.4),                 // низкая уверенность — не сопоставлено
            S("E", "dealer.value", 0.99),                       // поле ведёт другая привязка
            S("Z", "plan_primary.month", 0.9)), Options);       // столбца нет в листе

        var fact = proposal.Suggestions.Single(s => s.SourceColumn == "F" && s.TargetField == "sales_actual.value");
        Assert.Equal(MappingStatuses.Rejected, fact.Status);
        Assert.Contains(fact.Warnings, w => w.Contains("ведёт Linko"));
        var factAsPlan = proposal.Suggestions.Single(s => s.SourceColumn == "F" && s.TargetField == "plan_primary.value");
        Assert.Equal(MappingStatuses.Review, factAsPlan.Status);
        Assert.Contains(factAsPlan.Warnings, w => w.Contains("похож на факт"));
        var plan = proposal.Suggestions.Single(s => s.SourceColumn == "D");
        Assert.Equal(MappingStatuses.Review, plan.Status);
        Assert.Contains(plan.Warnings, w => w.Contains("претендуют столбцы"));
        Assert.Equal(MappingStatuses.Unmatched, proposal.Suggestions.Single(s => s.SourceColumn == "A" && s.TargetField is not null).Status);
        Assert.Equal(MappingStatuses.Blocked, proposal.Suggestions.Single(s => s.SourceColumn == "E").Status);
        Assert.Equal(MappingStatuses.Rejected, proposal.Suggestions.Single(s => s.SourceColumn == "Z").Status);
        Assert.Empty(proposal.Fields); // ничего не выбрано сразу — сохранить можно только после проверки человеком
        Assert.Contains(proposal.Targets, t => t.Key == "dealer.value" && t.Blocked is not null);

        // Занятое поле не сохраняется и по решению человека; несуществующее — тоже.
        var (_, errors) = SheetMapping.Build(Primary(blocked), [new MappingDecision("E", "dealer.value"), new MappingDecision("F", "sales_actual.value")], []);
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("привязка «Дилеры»"));
    }

    [Fact]
    public void Sales_plan_maps_to_a_plan_field_tons_are_converted_and_pnl_always_needs_review()
    {
        var secondary = Request("secondary", null, ("Регион", ["Хоразм"]), ("План продаж, т", ["1,2"]), ("Факт продаж", ["1,1"]));
        var proposal = SheetMapping.Validate(secondary, new RawMapping(
            [S("A", "plan_rop.region", 0.95), S("B", "plan_rop.value", 0.93, "tons_to_kg"), S("C", null, 0.1)],
            [new RawFieldSetting("plan_rop", "current", null, null)], [], "fake/mapper"), Options);
        var rop = Assert.Single(proposal.Fields);
        Assert.Equal((DataSourceCatalog.PlanRop, 1000m, true, (string?)null), (rop.Field, rop.Plan!.Multiplier, rop.CurrentMonth, rop.Plan.MonthColumn));

        var pnl = Request("pnl", null, ("Контрагент", ["Бобур Самарканд"]), ("Статья", ["Продажи"]), ("Сумма", ["120000000"]), ("Месяц", ["2026-09"]));
        var finance = SheetMapping.Validate(pnl, Raw(S("A", "pnl.dealer", 0.99), S("B", "pnl.line", 0.99), S("C", "pnl.amount", 0.99), S("D", "pnl.month", 0.99)), Options);
        Assert.All(finance.Suggestions, s => Assert.Equal(MappingStatuses.Review, s.Status));
        Assert.Empty(finance.Fields);
        var (fields, errors) = SheetMapping.Build(pnl, finance.Suggestions.Select(s => new MappingDecision(s.SourceColumn, s.TargetField!)).ToList(), []);
        Assert.Empty(errors);
        Assert.Equal(("Контрагент", "Статья", "Сумма", "Месяц"), (fields[0].Pnl!.DealerColumn, fields[0].Pnl!.LineColumn, fields[0].Pnl!.ValueColumn, fields[0].Pnl!.MonthColumn));
    }

    [Fact]
    public void A_total_beside_category_columns_and_an_incomplete_field_are_not_preselected()
    {
        // Как лист «План Первичка данный»: год, месяц, контрагент, столбцы-категории и «ИТОГО»; столбца региона для дилера нет.
        var wide = Request("primary", null, ("Год", ["2026"]), ("Месяц", ["9"]), ("Контрагент", ["Азизбек Хоразм"]), ("КЕКС", ["4 000"]), ("БАМБУК", ["16 000"]),
            ("ИТОГО", ["20 000"]));
        var proposal = SheetMapping.Validate(wide, Raw(
            S("A", "plan_primary.year", 0.95), S("B", "plan_primary.month", 0.95), S("C", "plan_primary.region", 0.95), S("C", "dealer.value", 0.9),
            S("D", "plan_primary.category_value", 0.95, category: "КЕКС"), S("E", "plan_primary.category_value", 0.95, category: "Бамбук"),
            S("F", "plan_primary.value", 0.9)), Options);

        var total = proposal.Suggestions.Single(s => s.SourceColumn == "F");
        Assert.Equal(MappingStatuses.Review, total.Status);
        Assert.Contains(total.Warnings, w => w.Contains("задвоился"));
        var dealer = proposal.Suggestions.Single(s => s.TargetField == "dealer.value");
        Assert.Equal(MappingStatuses.Review, dealer.Status);
        Assert.Contains(dealer.Warnings, w => w.Contains("не хватает столбца региона"));

        // Сразу выбранное собирается в полное поле: регион, план по категориям (без итога), месяц и год из столбцов.
        var plan = Assert.Single(proposal.Fields);
        Assert.Equal(DataSourceCatalog.PlanPrimary, plan.Field);
        Assert.Equal([("КЕКС", "Кекс"), ("БАМБУК", "Бамбук")], plan.Plan!.ValueColumns!.Select(c => (c.Column, c.Category!)));
        Assert.Equal(("Контрагент", (string?)null, "Месяц", "Год"), (plan.Plan.RegionColumn, plan.Plan.ValueColumn, plan.Plan.MonthColumn, plan.Plan.YearColumn));
        Assert.Empty(proposal.FieldErrors);
    }

    [Fact]
    public void Period_moves_with_the_current_month_and_is_validated()
    {
        var now = new DateTimeOffset(2026, 10, 17, 6, 0, 0, TimeSpan.Zero);
        static string Show((DateOnly From, DateOnly To)? r) => r is { } x ? $"{x.From:yyyy-MM}..{x.To:yyyy-MM}" : "все";
        Assert.Equal("2026-10..2026-10", Show(DataSourceRules.PeriodRange(new PeriodFilter("current"), now)));
        Assert.Equal("2026-01..2026-12", Show(DataSourceRules.PeriodRange(new PeriodFilter("year"), now)));
        Assert.Equal("2026-08..2026-10", Show(DataSourceRules.PeriodRange(new PeriodFilter("last", 3), now)));
        Assert.Equal("2025-11..2026-02", Show(DataSourceRules.PeriodRange(new PeriodFilter("range", From: "2025-11", To: "2026-02"), now)));
        Assert.Equal("все", Show(DataSourceRules.PeriodRange(null, now)));
        Assert.Equal("2027-01..2027-12", Show(DataSourceRules.PeriodRange(new PeriodFilter("year"), new DateTimeOffset(2026, 12, 31, 20, 0, 0, TimeSpan.Zero)))); // 1 января по Ташкенту
        Assert.True(DataSourceRules.InPeriod(DataSourceRules.PeriodRange(new PeriodFilter("year"), now), 0, 0)); // нераспознанный месяц не теряется
        Assert.False(DataSourceRules.InPeriod(DataSourceRules.PeriodRange(new PeriodFilter("year"), now), 2025, 12));
        Assert.NotNull(DataSourceRules.PeriodError(new PeriodFilter("last", 30)));
        Assert.NotNull(DataSourceRules.PeriodError(new PeriodFilter("range", From: "2026-05", To: "2026-01")));
        Assert.NotNull(DataSourceRules.PeriodError(new PeriodFilter("range", From: "2023-01", To: "2026-01")));
        Assert.Null(DataSourceRules.PeriodError(new PeriodFilter("range", From: "2026-01", To: "2026-06")));
    }

    [Fact]
    public void Sheet_category_names_find_onebase_categories_by_synonym()
    {
        DataCategoryRef[] categories =
        [
            new(8, "Помадка", ["помадк", "могул", "Помадка 1 кг"]), new(9, "Трубочки", ["трубоч"]), new(5, "Шоколад", ["шоколад"]),
            new(11, "Импорт", ["импорт", "Импорт Шоколад"]),
        ];
        Assert.Equal("Помадка", SheetMapping.CategoryName(categories, "МОГУЛЬ"));
        Assert.Equal("Трубочки", SheetMapping.CategoryName(categories, "ТРУБОЧКА"));
        Assert.Equal("Импорт", SheetMapping.CategoryName(categories, "ИМПОРТ"));
        Assert.Equal("Импорт", SheetMapping.CategoryName(categories, "Импорт Шоколад")); // тип Linko — точный синоним
        Assert.Equal("Шоколад", SheetMapping.CategoryName(categories, "шоколад"));
        Assert.Null(SheetMapping.CategoryName(categories, "Оборудование"));

        // Новое поле из ИИ-сопоставления берёт текущий год, а не всю историю листа.
        var built = SheetMapping.Build(Primary(), [new MappingDecision("A", "plan_primary.region"), new MappingDecision("B", "plan_primary.category"),
            new MappingDecision("C", "plan_primary.month"), new MappingDecision("D", "plan_primary.value")], []);
        Assert.Equal("year", Assert.Single(built.Fields).Period!.Mode);
    }

    [Fact]
    public void Renamed_or_moved_columns_stop_the_sync_instead_of_reading_the_wrong_column()
    {
        ReportDocument Doc(params string[] header) =>
            new([new ReportSheet("Лист1", [header, ["Хоразм", "1000", "Кекс"]], 2, header.Length, false)]);
        var byHeader = new DataSourceFieldBinding(DataSourceCatalog.PlanRop, Headers: new Dictionary<string, string> { ["Регион"] = "Регион", ["План"] = "План" });
        var byLetter = new DataSourceFieldBinding(DataSourceCatalog.PlanRop, Headers: new Dictionary<string, string> { ["B"] = "План" });

        Assert.Null(DataSourceRules.StructureError(Doc("Регион", "План"), ["Лист1"], 1, byHeader));
        Assert.Null(DataSourceRules.StructureError(Doc("План", "Регион"), ["Лист1"], 1, byHeader)); // переставили — по заголовку всё на месте
        Assert.Contains("столбца «План» больше нет", DataSourceRules.StructureError(Doc("Регион", "План, кг"), ["Лист1"], 1, byHeader));
        Assert.Contains("в столбце B теперь «Категория»", DataSourceRules.StructureError(Doc("Регион", "Категория", "План"), ["Лист1"], 1, byLetter));
        Assert.Null(DataSourceRules.StructureError(Doc("Регион", "План"), ["Лист1"], 1, byHeader with { Headers = null })); // старая привязка — без сверки
    }
}

/// <summary>
/// ИИ-сопоставление целиком, на PostgreSQL: лист → предложение (подставной AI Gateway — настоящий сервис, проверки и сохранение) →
/// подтверждение → обычная привязка → импорт без ручного выбора столбцов; повторная синхронизация ИИ не вызывает; занятое поле не
/// перезаписывается; ИИ недоступен — ручной режим работает; столбцы переименовали — импорт останавливается.
/// </summary>
public class SheetAutoMapTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Header = ["Регион", "Категория", "Месяц", "План, кг", "Дилер"];

    private static ReportDocument Sheet(string[] header, params string[][] rows) =>
        new([new ReportSheet("План", new[] { header }.Concat(rows).Select(r => (IReadOnlyList<string>)r).ToList(), rows.Length + 1, header.Length, false)]);

    private const string Answer = """
        {"suggestions":[
          {"sourceColumn":"A","targetField":"plan_primary.region","confidence":0.95,"reason":"названия регионов"},
          {"sourceColumn":"A","targetField":"dealer.region","confidence":0.92,"reason":"тот же регион для дилера"},
          {"sourceColumn":"B","targetField":"plan_primary.category","confidence":0.9,"reason":"Кекс, Бамбук — категории OneBase"},
          {"sourceColumn":"C","targetField":"plan_primary.month","confidence":0.93,"reason":"месяц текстом","transformType":"month_text"},
          {"sourceColumn":"D","targetField":"plan_primary.value","confidence":0.94,"reason":"план в кг"},
          {"sourceColumn":"E","targetField":"dealer.value","confidence":0.9,"reason":"имена дилеров"}],
         "fields":[{"field":"plan_primary","monthSource":"column"}],"notes":["лист — план первички по регионам"]}
        """;

    private async Task<Guid> ConnectAsync(DataSourceService sources) =>
        (await sources.CreateConnectionAsync(new ConnectionInput("План 2026", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms", "link", null),
            fixture.AdminId, CancellationToken.None)).Id;

    private static AutoMapConfirmInput Confirm(Guid connection, SheetMappingProposal proposal, Guid? bindingId = null) => new(
        "primary", connection, ["План"], "План", 1, bindingId,
        proposal.Suggestions.Where(s => s.Status == MappingStatuses.Accepted).Select(s => new MappingDecision(s.SourceColumn, s.TargetField!, s.Category, s.TransformType)).ToList(),
        proposal.FieldSettings, "interval", 30, null, true);

    [PostgresFact]
    public async Task Confirmed_ai_mapping_is_saved_and_imports_without_choosing_columns_by_hand()
    {
        await fixture.ResetAsync();
        fixture.Sheets.Document = Sheet(Header, ["Хоразм", "Кекс", "сентябрь 2026", "1000", "Азизбек Хоразм"], ["Бухоро", "Бамбук", "сентябрь 2026", "500", "Мехриддин Бухоро"]);
        fixture.Gateway.Script.Enqueue(_ => Answer);
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var autoMap = scope.ServiceProvider.GetRequiredService<DataSourceAutoMapService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var connection = await ConnectAsync(sources);

        // Анализ: ИИ видит заголовки и реальные поля раздела; предложение — без записи данных.
        var proposal = await autoMap.ProposeAsync(new AutoMapInput("primary", connection, ["План"], null, 1, null), fixture.AdminId, CancellationToken.None);
        var (messages, _) = Assert.Single(fixture.Gateway.Calls);
        Assert.Contains("План, кг", messages[1].Content);
        Assert.Contains("plan_primary.value", messages[1].Content);
        Assert.Equal(6, proposal.Suggestions.Count(s => s.Status == MappingStatuses.Accepted));
        Assert.Equal("fake/mapper", proposal.Model);
        Assert.Empty(await db.DataSourceBindings.AsNoTracking().ToListAsync());
        Assert.Equal(0m, await db.SalesRegionPlans.AsNoTracking().SumAsync(p => p.PlanKg));

        // Подтверждение: обычная привязка со снимком заголовков; импорт — без ручного выбора столбцов.
        var saved = (await autoMap.ConfirmAsync(Confirm(connection, proposal), fixture.AdminId, CancellationToken.None)).Binding!;
        Assert.Equal([DataSourceCatalog.Dealer, DataSourceCatalog.PlanPrimary], saved.Fields.Select(f => f.Field).Order());
        Assert.All(saved.Fields, f => Assert.NotNull(f.Headers));
        var run = await sources.RunAsync(saved.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", run.Status);
        Assert.Equal(1500m, await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).SumAsync(p => p.PlanKg));
        Assert.Equal("Мехриддин Бухоро", await db.SalesRegions.AsNoTracking().Where(r => r.Name == "Бухоро").Select(r => r.DealerName).FirstAsync());

        // Синхронизация по сохранённому сопоставлению: ИИ больше не вызывается.
        Assert.Equal("unchanged", (await sources.RunAsync(saved.Id, "schedule", null, CancellationToken.None)).Status);
        Assert.Single(fixture.Gateway.Calls);

        // Столбец «План, кг» переименовали: импорт остановлен, ничего не записано, просят сопоставить заново.
        fixture.Sheets.Document = Sheet(["Регион", "Категория", "Месяц", "План, т", "Дилер"], ["Хоразм", "Кекс", "сентябрь 2026", "2", "Азизбек Хоразм"]);
        var changed = await sources.RunAsync(saved.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("error", changed.Status);
        Assert.Contains("структура листа изменилась", changed.Fields.Single(f => f.Field == DataSourceCatalog.PlanPrimary).Message);
        Assert.Equal(1500m, await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).SumAsync(p => p.PlanKg));

        // Повторный анализ существующей привязки показывает, что сохранённое сопоставление устарело.
        fixture.Gateway.Script.Enqueue(_ => Answer);
        var again = await autoMap.ProposeAsync(new AutoMapInput("primary", connection, ["План"], null, 1, saved.Id), fixture.AdminId, CancellationToken.None);
        Assert.Contains(again.Stale, s => s.Contains("План первички") && s.Contains("«План, кг»"));
    }

    /// <summary>
    /// Лист с историей за 30 месяцев: без периода — понятная ошибка «выберите период», ничего не записано; «текущий год» — только 2026;
    /// «последние 2 месяца» — обновляет сентябрь и октябрь, остальные месяцы не трогает; диапазон — добавляет свои месяцы.
    /// </summary>
    [PostgresFact]
    public async Task Period_takes_only_the_needed_months_and_keeps_the_rest()
    {
        await fixture.ResetAsync();
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 17, 6, 0, 0, TimeSpan.Zero);
        string[][] Rows(decimal recent) => Enumerable.Range(0, 30)
            .Select(i => new DateOnly(2024, 5, 1).AddMonths(i))
            .Select(m => new[] { "Хоразм", "Кекс", $"{m:yyyy-MM}", m >= new DateOnly(2026, 9, 1) ? recent.ToString(System.Globalization.CultureInfo.InvariantCulture) : "100", "" })
            .ToArray();
        fixture.Sheets.Document = Sheet(Header, Rows(100));
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var connection = await ConnectAsync(sources);
        DataSourceFieldBinding Field(PeriodFilter? period) => new(DataSourceCatalog.PlanPrimary,
            new PlanFieldMapping("Регион", "Категория", "План, кг", null, "Месяц", null, null, null), Period: period);
        BindingInput Input(PeriodFilter? period) => new("primary", connection, ["План"], 1, [Field(period)], "manual", null, null, true);
        async Task<(int Months, decimal Kg)> PlansAsync()
        {
            var plans = await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).ToListAsync();
            return (plans.Select(p => (p.Year, p.Month)).Distinct().Count(), plans.Sum(p => p.PlanKg));
        }

        var binding = await sources.SaveBindingAsync(null, Input(null), fixture.AdminId, CancellationToken.None);
        var all = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("error", all.Status);
        Assert.Contains("выберите в привязке", all.Fields.Single().Message);
        Assert.Equal((0, 0m), await PlansAsync());

        await sources.SaveBindingAsync(binding.Id, Input(new PeriodFilter("year")), fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);
        Assert.Equal((10, 1000m), await PlansAsync());

        // Последние 2 месяца: сентябрь и октябрь обновлены, январь–август 2026 не тронуты (лист «полный» только за свои месяцы периода).
        fixture.Sheets.Document = Sheet(Header, Rows(200));
        await sources.SaveBindingAsync(binding.Id, Input(new PeriodFilter("last", 2)), fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);
        Assert.Equal((10, 1200m), await PlansAsync());

        await sources.SaveBindingAsync(binding.Id, Input(new PeriodFilter("range", From: "2025-11", To: "2026-02")), fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);
        Assert.Equal((12, 1400m), await PlansAsync());
    }

    [PostgresFact]
    public async Task A_field_of_another_binding_is_not_taken_and_manual_mapping_works_without_ai()
    {
        await fixture.ResetAsync();
        fixture.Sheets.Document = Sheet(Header, ["Бухоро", "Кекс", "сентябрь 2026", "500", "Мехриддин Бухоро"]);
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var autoMap = scope.ServiceProvider.GetRequiredService<DataSourceAutoMapService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var connection = await ConnectAsync(sources);

        // Дилера уже ведёт другая привязка.
        var dealers = await sources.SaveBindingAsync(null, new BindingInput("secondary", connection, ["План"], 1,
            [new DataSourceFieldBinding(DataSourceCatalog.Dealer, Profile: new ProfileFieldMapping("Регион", "Дилер"))], "manual", null, null, true),
            fixture.AdminId, CancellationToken.None);
        fixture.Gateway.Script.Enqueue(_ => Answer);
        var proposal = await autoMap.ProposeAsync(new AutoMapInput("primary", connection, ["План"], null, 1, null), fixture.AdminId, CancellationToken.None);
        Assert.All(proposal.Suggestions.Where(s => s.TargetField?.StartsWith("dealer.") == true), s => Assert.Equal(MappingStatuses.Blocked, s.Status));
        Assert.DoesNotContain(proposal.Fields, f => f.Field == DataSourceCatalog.Dealer);

        // Подтвердить дилера всё равно нельзя: ошибка, ничего не сохранено, прежняя привязка не тронута.
        var withDealer = Confirm(connection, proposal) with { Decisions = [new MappingDecision("A", "dealer.region"), new MappingDecision("E", "dealer.value")] };
        var refused = await Assert.ThrowsAsync<DataSourceException>(() => autoMap.ConfirmAsync(withDealer, fixture.AdminId, CancellationToken.None));
        Assert.Contains("уже ведёт привязка", refused.Message);
        Assert.Equal([dealers.Id], await db.DataSourceBindings.AsNoTracking().Select(b => b.Id).ToListAsync());

        // ИИ недоступен (нет ключа): понятная ошибка — а ручное сопоставление сохраняется и импортирует как раньше.
        var noAi = await Assert.ThrowsAsync<DataSourceException>(() =>
            autoMap.ProposeAsync(new AutoMapInput("primary", connection, ["План"], null, 1, null), fixture.AdminId, CancellationToken.None));
        Assert.Contains("Ручное сопоставление работает", noAi.Message);
        var manual = await sources.SaveBindingAsync(null, new BindingInput("primary", connection, ["План"], 1,
            [new DataSourceFieldBinding(DataSourceCatalog.PlanPrimary, new PlanFieldMapping("Регион", "Категория", "План, кг", null, "Месяц", null, 2026, null))],
            "manual", null, null, true), fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", (await sources.RunAsync(manual.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);
        Assert.Equal(500m, await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).SumAsync(p => p.PlanKg));
    }
}

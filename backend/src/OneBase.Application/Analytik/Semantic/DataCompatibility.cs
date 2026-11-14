using OneBase.Application.Analytik.Query;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>Классы совместимости пары показателей (ТЗ Phase 2, Data Compatibility Engine).</summary>
public static class CompatibilityClasses
{
    public const string Direct = "DIRECTLY_COMPATIBLE";
    public const string AfterAggregation = "COMPATIBLE_AFTER_AGGREGATION";
    public const string WithLimitations = "COMPATIBLE_WITH_LIMITATIONS";
    public const string NotCompatible = "NOT_COMPATIBLE";
    public const string SourceMissing = "SOURCE_MISSING";
    public const string AccessDenied = "ACCESS_DENIED";

    /// <summary>Порядок «хуже — выше»: класс пары — худший из её проверок.</summary>
    public static int Rank(string c) => c switch
    {
        AccessDenied => 6,
        SourceMissing => 5,
        NotCompatible => 4,
        WithLimitations => 3,
        AfterAggregation => 2,
        _ => 1,
    };

    public static string Title(string c) => c switch
    {
        Direct => "Совместимы напрямую",
        AfterAggregation => "Совместимы после агрегации",
        WithLimitations => "Совместимы с ограничениями",
        NotCompatible => "Несовместимы",
        SourceMissing => "Нет источника",
        AccessDenied => "Нет доступа",
        _ => c,
    };
}

/// <summary>Проверка одного пункта: код (source, keys, grain, period, units, region, agent, duplicates, missing, access, cardinality, fan_out, meaning), класс, текст.</summary>
public sealed record CompatibilityAspect(string Code, string Class, string Text);

/// <summary>
/// Совместимость пары: класс (худший из пунктов), общие измерения, шаг, на котором их можно сравнивать, математическая связь (часть и целое,
/// формула), пункты проверки и совет — как исследовать правильно.
/// </summary>
public sealed record CompatibilityResult(
    string MetricA,
    string MetricB,
    string TitleA,
    string TitleB,
    string Class,
    IReadOnlyList<string> CommonDimensions,
    string? Grain,
    string? Dependency,
    IReadOnlyList<CompatibilityAspect> Aspects,
    string Advice);

/// <summary>
/// Показатель вне движка срезов — его дают инструменты отделов. Нужен, чтобы проверять пары «продажи × первичка», «дебиторка × продажи»,
/// «продажи × акции» до того, как модель попробует их смешать.
/// </summary>
public sealed record ExternalMetric(string Code, string Title, string Unit, string Source, string Tool, IReadOnlyList<string> Dimensions, string Grain, string Kind,
    string Permission, string Note);

/// <summary>
/// Data Compatibility Engine поверх реестра движка: можно ли сравнивать, соединять или коррелировать два показателя, на каком шаге и по каким
/// измерениям, с какими оговорками. Движок сам агрегирует каждый факт отдельно и соединяет по измерениям — сумма не размножается; здесь
/// проверяется бизнес-смысл: оплаты — не выручка, первичка — не вторичка, остаток на дату — не оборот за период, месячный план — не дневной факт.
/// </summary>
public static class DataCompatibility
{
    public static readonly IReadOnlyList<ExternalMetric> External =
    [
        new("primary_kg", "Первичка — отгрузки завода дилерам", "кг", "Google Sheets / AI Data Manager, Linko", "get_primary_shipments", ["date", "region", "product_group"],
            "month", "flow", Security.Permissions.SalesRead, "Регион — по складу дилера; отгрузка дилеру, а не продажа точке"),
        new("dealer_debt", "Дебиторская задолженность дилеров", "сум", "Журнал дилеров (FIFO)", "get_dso", ["dealer"], "snapshot", "stock",
            Security.Permissions.FinanceRead, "Долг на сейчас по FIFO; истории долга по дням нет"),
        new("promotions", "Акции Linko", "акций", "Linko: promotions", "get_promo; research promo_effect", ["date", "product", "product_group"], "period", "event",
            Security.Permissions.SalesRead, "Акция — товары и даты; строка заказа с акцией не связана"),
        new("linko_kpi_fact", "Факт KPI Linko (staff_balance)", "кг / сум", "Linko staff_balance", "get_sales_kpi", ["date", "agent"], "month", "flow",
            Security.Permissions.SalesRead, "Считается по правилам Linko, а не «Продаж»"),
        new("expenses", "Расходы компании", "сум", "—", "—", [], "—", "missing", Security.Permissions.FinanceRead, "Расходов в OneBase нет (модуль «Финансы» не построен)"),
    ];

    private static readonly string[] RegionDims = ["region", "direction", "manager", "supervisor", "dealer"];

    private sealed record Side(string Code, string Title, string Fact, string Unit, string Source, IReadOnlyList<string> Dims, string Grain, string Kind, string Permission,
        string RegionRule, bool Engine, string? Note);

    private static Side? Describe(string code)
    {
        var c = code.Trim().ToLowerInvariant();
        if (SemanticCatalog.Metric(c) is { } m)
        {
            var dims = SemanticCatalog.Dimensions.Where(d => SemanticCatalog.FactsOf([m]).All(f => d.Facts.Contains(f))).Select(d => d.Code).ToList();
            var (grain, kind, region) = m.Fact switch
            {
                SemanticCatalog.Plan => ("month", "flow", "регион плана (Google Sheets) или регион ТП"),
                SemanticCatalog.Stock => ("snapshot", "stock", "—"),
                SemanticCatalog.Payments => ("day", "flow", "по филиалу точки"),
                SemanticCatalog.Visits => ("day", "flow", "по филиалу точки"),
                _ => ("day", "flow", "по филиалу заказа"),
            };
            var source = m.Fact == SemanticCatalog.Plan ? "Google Sheets / Linko" : "Linko";
            return new Side(m.Code, m.Title, m.Fact, m.Unit, source, dims, grain, kind, m.Permission, region, true, m.Note);
        }

        return External.FirstOrDefault(e => e.Code == c) is { } e
            ? new Side(e.Code, e.Title, "external:" + e.Code, e.Unit, e.Source, e.Dimensions, e.Grain, e.Kind, e.Permission,
                e.Code == "primary_kg" ? "по складу дилера" : "—", false, e.Note)
            : null;
    }

    private static int GrainRank(string g) => g switch
    {
        "day" => 1,
        "week" => 2,
        "month" => 3,
        "quarter" => 4,
        "year" => 5,
        _ => 0,
    };

    /// <summary>
    /// Проверка пары. dimensions — по каким измерениям их хотят соединить (пусто — итоги или ряд по датам), grain — шаг; principal — права
    /// пользователя (null — без проверки прав); ranges — диапазоны данных фактов (null — без проверки периода).
    /// </summary>
    public static CompatibilityResult Check(string metricA, string metricB, IReadOnlyList<string>? dimensions = null, string? grain = null,
        AnalytikPrincipal? principal = null, IReadOnlyDictionary<string, (DateOnly? First, DateOnly? Last)>? ranges = null, DateOnly? from = null, DateOnly? to = null)
    {
        var a = Describe(metricA);
        var b = Describe(metricB);
        var aspects = new List<CompatibilityAspect>();
        if (a is null || b is null)
        {
            var unknown = a is null ? metricA : metricB;
            aspects.Add(new("source", CompatibilityClasses.SourceMissing,
                $"Показатель «{unknown}» не найден ни в движке срезов, ни среди источников инструментов (analytics_capabilities — что есть)."));
            return Result(metricA, metricB, a?.Title ?? metricA, b?.Title ?? metricB, aspects, [], null, null,
                "Возьмите показатель из реестра (analytics_capabilities) — данных под этим названием в OneBase нет.");
        }

        foreach (var s in new[] { a, b }.Where(s => s.Kind == "missing"))
        {
            aspects.Add(new("source", CompatibilityClasses.SourceMissing, $"«{s.Title}»: {s.Note}."));
        }

        // 10. Права.
        if (principal is not null)
        {
            foreach (var s in new[] { a, b }.Where(s => !principal.Has(s.Permission)))
            {
                aspects.Add(new("access", CompatibilityClasses.AccessDenied, $"Нет права на «{s.Title}» ({s.Permission})."));
            }

            if (principal.Sales.Restricted && (a.Fact == SemanticCatalog.Stock || b.Fact == SemanticCatalog.Stock))
            {
                aspects.Add(new("access", CompatibilityClasses.AccessDenied, "Остатки складов видны только при доступе ко всей организации."));
            }
        }

        // 1. Источник.
        // Разные системы сами по себе не ограничение (у каждой свои правила, они в sources) — класс понижают только реальные расхождения ниже.
        aspects.Add(new("source", CompatibilityClasses.Direct, a.Source == b.Source
            ? $"Один источник: {a.Source}."
            : $"Разные источники: {a.Title} — {a.Source}; {b.Title} — {b.Source}; правила отбора каждого — в источниках результата."));

        // 2 и 11. Общие ключи и кардинальность.
        var common = a.Dims.Intersect(b.Dims).ToList();
        var requested = (dimensions ?? []).Select(d => SemanticCatalog.Dimension(d)?.Code ?? d.Trim().ToLowerInvariant()).ToList();
        var missingDims = requested.Where(d => !common.Contains(d)).ToList();
        if (missingDims.Count > 0)
        {
            var who = missingDims.Select(d => !a.Dims.Contains(d) ? $"«{d}» нет у «{a.Title}»" : $"«{d}» нет у «{b.Title}»");
            aspects.Add(new("keys", CompatibilityClasses.NotCompatible,
                $"Нельзя соединить по {string.Join(", ", missingDims)}: {string.Join("; ", who)}. Общие измерения: {(common.Count == 0 ? "нет" : string.Join(", ", common))}."));
        }
        else
        {
            aspects.Add(new("keys", common.Count > 0 ? CompatibilityClasses.Direct : CompatibilityClasses.WithLimitations,
                common.Count > 0 ? $"Общие измерения: {string.Join(", ", common)}." : "Общих измерений нет — сравнимы только итоги."));
        }

        if (requested.Contains("product") || requested.Contains("product_group") || requested.Contains("sku"))
        {
            if (a.Fact == SemanticCatalog.Visits || b.Fact == SemanticCatalog.Visits || a.Fact == SemanticCatalog.Payments || b.Fact == SemanticCatalog.Payments)
            {
                aspects.Add(new("fan_out", CompatibilityClasses.NotCompatible,
                    "Визиты и оплаты не привязаны к товарам: разнести их по товарам или группам — значит размножить или выдумать суммы."));
            }
        }

        // 3. Шаг (гранулярность) и вид величины (поток или остаток).
        var g = (grain ?? (requested.Contains("date") ? "day" : null))?.Trim().ToLowerInvariant();
        string? workGrain = g;
        if (a.Kind == "stock" || b.Kind == "stock")
        {
            var stock = a.Kind == "stock" ? a : b;
            var other = a.Kind == "stock" ? b : a;
            if (requested.Contains("date") || g is not null)
            {
                aspects.Add(new("grain", CompatibilityClasses.NotCompatible,
                    $"«{stock.Title}» — снимок на сейчас (истории нет), «{other.Title}» — оборот за период: по датам их не сопоставить."));
            }
            else
            {
                aspects.Add(new("grain", CompatibilityClasses.WithLimitations,
                    $"«{stock.Title}» — остаток на дату, «{other.Title}» — оборот за период: сравнимы как отношение (дни запаса, оборачиваемость), не как суммы."));
            }

            workGrain = null;
        }
        else if (a.Kind == "event" || b.Kind == "event")
        {
            aspects.Add(new("grain", CompatibilityClasses.AfterAggregation,
                "Акция — событие с товарами и датами: продажи сопоставляются по её товарам за дни акции и такие же дни до неё (или с контрольной группой)."));
        }
        else
        {
            var coarse = new[] { a.Grain, b.Grain }.OrderByDescending(GrainRank).First();
            if (a.Grain != b.Grain)
            {
                var needs = g is null || GrainRank(g) < GrainRank(coarse);
                aspects.Add(new("grain", needs ? CompatibilityClasses.AfterAggregation : CompatibilityClasses.Direct,
                    $"«{(GrainRank(a.Grain) > GrainRank(b.Grain) ? a.Title : b.Title)}» есть только по шагу {coarse}: " +
                    (needs ? $"сравнивайте по {coarse} (суммы дней за {coarse}), месячное не делите по дням." : $"шаг {g} подходит.")));
                if (needs)
                {
                    workGrain = coarse;
                }
            }
            else
            {
                aspects.Add(new("grain", CompatibilityClasses.Direct, $"Одинаковый шаг: {a.Grain}."));
            }
        }

        // 4. Период.
        if (ranges is not null && a.Engine && b.Engine)
        {
            var ra = ranges.GetValueOrDefault(a.Fact);
            var rb = ranges.GetValueOrDefault(b.Fact);
            if (a.Kind != "stock" && b.Kind != "stock" && (ra.Last is null || rb.Last is null))
            {
                var empty = ra.Last is null ? a : b;
                aspects.Add(new("period", CompatibilityClasses.SourceMissing, $"«{empty.Title}»: в OneBase нет ни одной записи."));
            }
            else if (ra.First is { } fa && rb.First is { } fb && ra.Last is { } la && rb.Last is { } lb)
            {
                var start = fa > fb ? fa : fb;
                var end = la < lb ? la : lb;
                var wantFrom = from ?? start;
                var wantTo = to ?? end;
                aspects.Add(start > wantFrom || end < wantTo
                    ? new("period", CompatibilityClasses.WithLimitations,
                        $"Общий период данных: {start:dd.MM.yyyy} — {end:dd.MM.yyyy} («{a.Title}» с {fa:dd.MM.yyyy} по {la:dd.MM.yyyy}, «{b.Title}» с {fb:dd.MM.yyyy} по {lb:dd.MM.yyyy}).")
                    : new("period", CompatibilityClasses.Direct, $"Период {wantFrom:dd.MM.yyyy} — {wantTo:dd.MM.yyyy} покрыт обоими."));
            }
        }

        // 5. Единицы.
        // Разные единицы не мешают связи и динамике — запрещено только складывать и вычитать значения (это пишется в тексте).
        aspects.Add(new("units", CompatibilityClasses.Direct, a.Unit == b.Unit
            ? $"Одна единица: {a.Unit}."
            : $"Разные единицы ({a.Unit} и {b.Unit}): сравнивайте связь и динамику (корреляция, %), а не значения; складывать и вычитать нельзя."));

        // 6. Атрибуция региона.
        if (requested.Any(RegionDims.Contains))
        {
            aspects.Add(a.RegionRule == b.RegionRule
                ? new("region", CompatibilityClasses.Direct, $"Регион у обоих — {a.RegionRule}.")
                : new("region", CompatibilityClasses.WithLimitations,
                    $"Регион определяется по-разному: «{a.Title}» — {a.RegionRule}, «{b.Title}» — {b.RegionRule}; у точки или ТП другого филиала суммы региона разойдутся."));
        }

        // 7. Атрибуция агента.
        if (requested.Contains("agent"))
        {
            var payments = a.Fact == SemanticCatalog.Payments || b.Fact == SemanticCatalog.Payments;
            aspects.Add(payments
                ? new("agent", CompatibilityClasses.WithLimitations, "Агент оплаты — кто принял деньги, агент продажи — кто ввёл заказ: это могут быть разные люди.")
                : new("agent", CompatibilityClasses.Direct, "Агент — пользователь Linko документа у обоих."));
        }

        // 8, 9, 12. Дубли, пропуски, размножение сумм.
        if (a.Engine && b.Engine)
        {
            aspects.Add(new("duplicates", CompatibilityClasses.Direct,
                "Строки уникальны по id Linko; каждый факт агрегируется отдельно и соединяется по измерениям — сумма не размножается."));
            if (requested.Count > 0)
            {
                aspects.Add(new("missing", CompatibilityClasses.Direct,
                    "Строки без значения измерения (без региона, без группы) остаются отдельной строкой «Без …» и видны в checks среза."));
            }
        }
        else
        {
            aspects.Add(new("duplicates", CompatibilityClasses.WithLimitations,
                "Один из показателей дают инструменты отдела, а не движок: соединение — по итогам или общим измерениям, построчного ключа нет."));
        }

        // Бизнес-смысл.
        var pair = new HashSet<string> { a.Fact, b.Fact };
        if (pair.Contains(SemanticCatalog.Payments) && pair.Contains(SemanticCatalog.Sales))
        {
            aspects.Add(new("meaning", CompatibilityClasses.WithLimitations,
                "Оплаты — поступившие деньги, выручка — продажи: сравнивайте как отношение и запаздывание оплат; разница не равна долгу (начального сальдо нет)."));
        }

        if (pair.Contains("external:primary_kg") && (pair.Contains(SemanticCatalog.Sales) || pair.Contains(SemanticCatalog.Plan)))
        {
            aspects.Add(new("meaning", CompatibilityClasses.WithLimitations,
                "Первичка (отгрузка заводом дилеру) и вторичка (продажа дилера точкам) — разные потоки: не складываются; сравниваются помесячно как запас у дилера."));
        }

        if (pair.Contains("external:promotions"))
        {
            aspects.Add(new("meaning", CompatibilityClasses.WithLimitations,
                "Строка заказа с акцией в Linko не связана: продажи акции — продажи её товаров в её даты; эффект — только против контрольной группы (did)."));
        }

        if (pair.Contains("external:dealer_debt"))
        {
            aspects.Add(new("meaning", CompatibilityClasses.WithLimitations, "Долг дилера — снимок на сейчас по FIFO: с продажами сравним по дилерам, не по датам."));
        }

        if (pair.Contains("external:linko_kpi_fact") && pair.Contains(SemanticCatalog.Sales))
        {
            aspects.Add(new("meaning", CompatibilityClasses.WithLimitations, "Факт KPI Linko считается по правилам Linko и отличается от факта «Продаж» — не смешивайте."));
        }

        if (pair.Contains(SemanticCatalog.Plan) && pair.Contains(SemanticCatalog.Sales) && requested.Contains("product_group") && requested.Contains("agent"))
        {
            aspects.Add(new("meaning", CompatibilityClasses.NotCompatible, "План ТП Linko — без товарных групп, план регионов — без ТП: план по ТП × группе не существует."));
        }

        var dependency = a.Engine && b.Engine ? MetricPassports.Dependency(a.Code, b.Code) : null;
        if (dependency is not null)
        {
            aspects.Add(new("dependency", CompatibilityClasses.WithLimitations,
                $"Математическая связь: {dependency}. Для корреляции берите часть против остального (целое минус часть) или независимые показатели."));
        }

        var advice = Advice(a, b, aspects, workGrain, common);
        return Result(a.Code, b.Code, a.Title, b.Title, aspects, common, workGrain, dependency, advice);
    }

    private static CompatibilityResult Result(string a, string b, string ta, string tb, List<CompatibilityAspect> aspects, IReadOnlyList<string> common, string? grain,
        string? dependency, string advice)
    {
        var cls = aspects.Count == 0 ? CompatibilityClasses.Direct : aspects.MaxBy(x => CompatibilityClasses.Rank(x.Class))!.Class;
        return new CompatibilityResult(a, b, ta, tb, cls, common, grain, dependency, aspects, advice);
    }

    private static string Advice(Side a, Side b, List<CompatibilityAspect> aspects, string? grain, IReadOnlyList<string> common)
    {
        var worst = aspects.MaxBy(x => CompatibilityClasses.Rank(x.Class))!;
        return worst.Class switch
        {
            CompatibilityClasses.AccessDenied => "Данных нет в ваших правах — исследование возможно только по доступной части; права не обходятся.",
            CompatibilityClasses.SourceMissing => "Одного из показателей нет в OneBase — сравнение невозможно; назовите, какого источника не хватает.",
            CompatibilityClasses.NotCompatible when worst.Code == "grain" =>
                "Сравните текущий остаток с продажами за последние дни по товарам (дни запаса, analytics_query: stock_units, cover_days по product).",
            CompatibilityClasses.NotCompatible =>
                $"Соединяйте только по общим измерениям ({(common.Count == 0 ? "итоги периода" : string.Join(", ", common))}).",
            CompatibilityClasses.AfterAggregation when grain is not null => $"Приведите оба ряда к шагу {grain} (суммы за шаг) и только потом сравнивайте.",
            CompatibilityClasses.AfterAggregation => "Агрегируйте продажи по товарам и дням события, сравните с такими же днями до него или с контрольной группой.",
            CompatibilityClasses.WithLimitations => "Сравнение допустимо; ограничения из проверок назовите в ответе.",
            _ => "Сравнение и соединение допустимы без оговорок.",
        };
    }

    /// <summary>Оговорки для среза из нескольких фактов (движок добавляет их в notes): каждая пара фактов запроса, только ограничения.</summary>
    public static IReadOnlyList<string> Notes(IReadOnlyList<MetricDef> metrics, IReadOnlyList<string> dimensions, string grain)
    {
        var byFact = metrics.Where(m => !m.Calculated).GroupBy(m => m.Fact).Select(g => g.First()).ToList();
        var notes = new List<string>();
        for (var i = 0; i < byFact.Count; i++)
        {
            for (var j = i + 1; j < byFact.Count; j++)
            {
                var r = Check(byFact[i].Code, byFact[j].Code, dimensions, dimensions.Contains("date") ? grain : null);
                notes.AddRange(r.Aspects.Where(x => x.Code is "meaning" or "region" or "agent" && x.Class == CompatibilityClasses.WithLimitations).Select(x => x.Text));
            }
        }

        return notes.Distinct().ToList();
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Security;

namespace OneBase.Application.Analytik.Research;

/// <summary>Акция для исследования эффекта: id, название, даты, товары (id Linko).</summary>
public sealed record PromoInfo(long Id, string Name, DateOnly Begin, DateOnly Till, IReadOnlyList<long> Products);

/// <summary>Что доступно исследованию: диапазоны данных фактов, права пользователя, акции периода.</summary>
public sealed record ResearchContext(
    AnalytikPrincipal Principal,
    DateOnly Today,
    IReadOnlyDictionary<string, (DateOnly? First, DateOnly? Last)> Ranges,
    IReadOnlyList<PromoInfo> Promotions,
    DateTimeOffset? SalesSyncedAt = null)
{
    /// <summary>
    /// Последний полный день данных продаж: если заказы Linko в последний раз загружены в тот же день (по Ташкенту), что и последний день
    /// данных, этот день неполный (загрузка шла посреди дня) — исследование заканчивается накануне, иначе неполный день выглядит падением.
    /// </summary>
    public (DateOnly Through, bool Trimmed) LastFullDay()
    {
        var through = Ranges.GetValueOrDefault(SemanticCatalog.Sales).Last ?? Today;
        var partial = SalesSyncedAt is { } at && DateOnly.FromDateTime(at.ToOffset(TimeSpan.FromHours(5)).DateTime) <= through;
        return partial ? (through.AddDays(-1), true) : (through, false);
    }
}

/// <summary>Шаблон, найденный в вопросе, и период из него (если назван).</summary>
/// <summary>Шаблон по вопросу: вид, «последние N дней» или месяц из вопроса (From — To), почему выбран.</summary>
public sealed record ResearchMatch(string Kind, int? Days, string Reason, DateOnly? From = null, DateOnly? To = null);

/// <summary>
/// Research Planner: по вопросу выбирает шаблон исследования (без модели — по словам вопроса на русском и узбекском), по шаблону строит план
/// операций с зависимостями: какие срезы и расчёты нужны, что идёт параллельно, что необязательно. До выполнения проверяет доступность данных
/// (диапазоны фактов), права (финансы, область продаж) и совместимость — недоступное сразу помечается статусом с причиной, а не «данных нет».
/// </summary>
public static partial class ResearchPlanner
{
    public const int DefaultDays = 90;

    private static readonly string[] SalesMetrics = ["revenue", "kg", "orders", "outlets", "avg_check", "returns_revenue", "discount"];

    /// <summary>
    /// Шаблон по вопросу; null — вопрос не про исследование (обычный срез или отчёт) или про то, чего шаблон не сделает: конкретную связь
    /// («есть ли связь между визитами и выручкой по регионам») или точные даты («с 1 по 15 октября») — такой вопрос решает агент нужным
    /// расчётом (analytik_compute, analytics_query, analytics_research с датами). Месяц в вопросе («в октябре», «за прошлый месяц») — период
    /// исследования; today — сегодня по Ташкенту (для года месяца).
    /// </summary>
    public static ResearchMatch? Match(string? question, DateOnly? today = null)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        var q = question.ToLowerInvariant().Replace('ё', 'е');
        if (ExactDate().IsMatch(q) || ByWords(q) is not { } match)
        {
            return null;
        }

        var (from, to) = Months(q, today ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)));
        return from is null ? match : match with { Days = null, From = from, To = to };
    }

    /// <summary>Месяцы из вопроса: названия (RU, UZ) — с первого дня первого до последнего дня последнего; «прошлый» и «этот месяц».</summary>
    private static (DateOnly? From, DateOnly? To) Months(string q, DateOnly today)
    {
        if (q.Contains("прошл", StringComparison.Ordinal) && q.Contains("месяц", StringComparison.Ordinal)
            || q.Contains("o'tgan oy", StringComparison.Ordinal) || q.Contains("ўтган ой", StringComparison.Ordinal))
        {
            var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            return (first, first.AddMonths(1).AddDays(-1));
        }

        if (q.Contains("этом месяце", StringComparison.Ordinal) || q.Contains("этот месяц", StringComparison.Ordinal) || q.Contains("текущий месяц", StringComparison.Ordinal)
            || q.Contains("shu oy", StringComparison.Ordinal) || q.Contains("жорий ой", StringComparison.Ordinal) || q.Contains("joriy oy", StringComparison.Ordinal))
        {
            return (new DateOnly(today.Year, today.Month, 1), today);
        }

        var year = Year().Match(q) is { Success: true } y ? int.Parse(y.Value, CultureInfo.InvariantCulture) : (int?)null;
        var months = MonthName().Matches(q).Select(m => MonthOf(m.Value)).Where(m => m > 0)
            .Select(m => new DateOnly(year ?? (m <= today.Month ? today.Year : today.Year - 1), m, 1)).ToList();
        return months.Count == 0 ? (null, null) : (months.Min(), months.Max().AddMonths(1).AddDays(-1));
    }

    private static int MonthOf(string word) => word switch
    {
        _ when word.StartsWith("мар", StringComparison.Ordinal) || word.StartsWith("mar", StringComparison.Ordinal) => 3,
        _ when word.StartsWith("ма", StringComparison.Ordinal) || word.StartsWith("may", StringComparison.Ordinal) => 5,
        _ when word.StartsWith("янв", StringComparison.Ordinal) || word.StartsWith("yan", StringComparison.Ordinal) => 1,
        _ when word.StartsWith("фев", StringComparison.Ordinal) || word.StartsWith("fev", StringComparison.Ordinal) => 2,
        _ when word.StartsWith("апр", StringComparison.Ordinal) || word.StartsWith("apr", StringComparison.Ordinal) => 4,
        _ when word.StartsWith("июн", StringComparison.Ordinal) || word.StartsWith("iyun", StringComparison.Ordinal) => 6,
        _ when word.StartsWith("июл", StringComparison.Ordinal) || word.StartsWith("iyul", StringComparison.Ordinal) => 7,
        _ when word.StartsWith("авг", StringComparison.Ordinal) || word.StartsWith("avg", StringComparison.Ordinal) => 8,
        _ when word.StartsWith("сен", StringComparison.Ordinal) || word.StartsWith("sen", StringComparison.Ordinal) => 9,
        _ when word.StartsWith("окт", StringComparison.Ordinal) || word.StartsWith("okt", StringComparison.Ordinal) => 10,
        _ when word.StartsWith("ноя", StringComparison.Ordinal) || word.StartsWith("noy", StringComparison.Ordinal) => 11,
        _ when word.StartsWith("дек", StringComparison.Ordinal) || word.StartsWith("dek", StringComparison.Ordinal) => 12,
        _ => 0,
    };

    [GeneratedRegex(@"\b(?:январ|феврал|март|апрел|ма[йяе]\b|июн|июл|август|сентябр|октябр|ноябр|декабр|yanvar|fevral|mart|aprel|may\b|may oy|iyun|iyul|avgust|sent?yabr|sentabr|okt?yabr|oktabr|noyabr|dekabr)")]
    private static partial Regex MonthName();

    [GeneratedRegex(@"\b20\d\d\b")]
    private static partial Regex Year();

    /// <summary>Точные даты: 01.10, 1/10, «1 октября», «с 1 по 15 октября».</summary>
    [GeneratedRegex(@"\b\d{1,2}[./]\d{1,2}\b|\b\d{1,2}\s+(?:январ|феврал|марта|апрел|мая|июн|июл|август|сентябр|октябр|ноябр|декабр|yanvar|fevral|mart|aprel|may|iyun|iyul|avgust|sent|okt|noyabr|dekabr)")]
    private static partial Regex ExactDate();

    /// <summary>Вид исследования по словам вопроса; null — не исследование или конкретная связь (её считает агент).</summary>
    private static ResearchMatch? ByWords(string q)
    {
        int? days = Days().Match(q) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var d) ? Math.Clamp(d, 7, SemanticEngine.MaxDays) : null;
        days ??= q.Contains("за год") || q.Contains("yil") ? 365 : q.Contains("квартал") ? 92 : q.Contains("за месяц") || q.Contains("oy davomida") ? 30 : null;
        bool Has(params string[] words) => words.Any(q.Contains);

        if (Has("корреляц", "korrelyats", "корреляция", "взаимосвяз", "закономерн", "qonuniyat", "қонуният", "bog'liqlik", "боғлиқлик"))
        {
            // Конкретная связь («есть ли связь между визитами и выручкой», «посчитай корреляцию … по регионам») — не обзор всех корреляций:
            // шаблон посчитал бы всё сразу по всей компании и не ту пару; её считает агент расчётом с нужной парой, разрезом и периодом.
            var specific = Has("есть ли связь", "есть ли корреляц", "есть ли взаимосвяз", "посчитай корреляц", "рассчитай корреляц", "вычисли корреляц",
                               "коэффициент корреляц", "bog'liqlik bormi", "korrelyatsiya bormi", "боғлиқлик борми", "korrelyatsiyani hisobla", "корреляцияни ҳисобла")
                           || Has("по регион", "по тп", "по агент", "по торгов", "по товар", "по точк", "по филиал", "по дилер", "bo'yicha", "бўйича")
                           && !Has("все", "barcha", "барча");
            return specific ? null : new(ResearchKinds.Correlations, days, "корреляции, взаимосвязи, закономерности");
        }

        if (Has("аномал", "anomal", "необычн", "выброс"))
        {
            return new(ResearchKinds.Anomalies, days, "аномалии");
        }

        if (Has("акци", "промо", "aksiya", "акция", "promo") && Has("эффект", "анализ", "проанализ", "roi", "окупа", "samara", "самара", "tahlil", "таҳлил"))
        {
            return new(ResearchKinds.PromoEffect, days, "эффективность акций");
        }

        if (Has("визит", "tashrif", "ташриф") && Has("раст", "рост", "увелич", "o'sm", "ўсм") && Has("пада", "сниж", "умень", "tush", "туш", "kamay", "камай"))
        {
            return new(ResearchKinds.VisitsVsSales, days, "визиты растут, продажи падают");
        }

        if (Has("эффективн", "samaradorl", "самарадорл", "сравни", "solishtir", "солиштир") && Has("тп", "торгов", "агент", "agent", "представител")
            && Has("визит", "tashrif", "ташриф"))
        {
            return new(ResearchKinds.AgentEfficiency, days, "эффективность ТП по визитам и продажам");
        }

        if (Has("товарн", "групп", "категор", "kategoriya", "категория", "guruh", "гуруҳ") && Has("влия", "вклад", "сильнее", "ta'sir", "таъсир", "драйвер"))
        {
            return new(ResearchKinds.GroupInfluence, days, "влияние товарных групп");
        }

        if (Has("почему", "причин", "фактор", "из-за", "nega", "нега", "sabab", "сабаб", "omil", "омил")
            && Has("сниз", "упал", "паден", "просел", "уменьш", "сократ", "хуже", "tushdi", "tushib", "тушди", "kamaydi", "камайди", "пасай", "pasay"))
        {
            return new(ResearchKinds.DeclineDrivers, days, "причины снижения");
        }

        if (Has("состояни", "здоровь", "holati", "ҳолати", "холати") && Has("компан", "бизнес", "kompaniya", "компания", "всей", "butun", "бутун")
            || Has("основные риски", "главные риски", "asosiy xavf", "асосий хавф"))
        {
            return new(ResearchKinds.CompanyHealth, days, "состояние компании и риски");
        }

        return null;
    }

    [GeneratedRegex(@"(\d{1,3})\s*(?:дн|день|дня|kun|кун|сут)")]
    private static partial Regex Days();

    /// <summary>
    /// Акции в одни и те же даты с почти одинаковыми товарами (не меньше 80 % общих — одна механика для разных групп клиентов) по продажам
    /// неразличимы: строка заказа с акцией не связана, эффект и скидки считаются по товарам и датам. Такие акции проверяются один раз —
    /// под общим названием и с объединёнными товарами; иначе одно и то же изменение продаж и одни и те же скидки попадут в отчёт дважды.
    /// </summary>
    public static IReadOnlyList<PromoInfo> MergeSimilar(IEnumerable<PromoInfo> promos, DateOnly? from = null, DateOnly? to = null)
    {
        // Даты сравниваются в границах исследования: акции, начавшиеся до его начала, проверяются с первого дня периода — окно у них общее.
        (DateOnly, DateOnly) Window(PromoInfo p) => (from is { } f && p.Begin < f ? f : p.Begin, to is { } t && p.Till > t ? t : p.Till);
        var clusters = new List<(PromoInfo Head, HashSet<long> Products, List<string> Names)>();
        foreach (var p in promos.OrderByDescending(p => p.Products.Count).ThenBy(p => p.Id))
        {
            var set = p.Products.ToHashSet();
            var i = clusters.FindIndex(c => Window(c.Head) == Window(p)
                                            && (double)c.Products.Intersect(set).Count() / c.Products.Union(set).Count() >= 0.8);
            if (i < 0)
            {
                clusters.Add((p, set, [p.Name]));
                continue;
            }

            clusters[i].Products.UnionWith(set);
            if (!clusters[i].Names.Contains(p.Name, StringComparer.Ordinal))
            {
                clusters[i].Names.Add(p.Name);
            }
        }

        return clusters.Select(c => c.Head with { Name = MergedName(c.Names), Products = c.Products.Order().ToList() }).ToList();
    }

    /// <summary>Названия объединённых акций: две — через «/», больше — первые две и «ещё N».</summary>
    public static string MergedName(IReadOnlyList<string> names) =>
        names.Count <= 2 ? string.Join(" / ", names) : $"{names[0]} / {names[1]} и ещё {names.Count - 2}";

    /// <summary>Период исследования: конец — запрошенный день, но не позже последнего дня данных продаж; начало — по шаблону.</summary>
    public static (DateOnly From, DateOnly To) Period(ResearchRequest r, ResearchContext ctx, int defaultDays)
    {
        var (through, _) = ctx.LastFullDay();
        var to = r.To is { } t && t < through ? t : through;
        var from = r.From ?? to.AddDays(-(defaultDays - 1));
        if (from > to)
        {
            (from, to) = (to, from);
        }

        if (to.DayNumber - from.DayNumber + 1 > SemanticEngine.MaxDays)
        {
            from = to.AddDays(-(SemanticEngine.MaxDays - 1));
        }

        return (from, to);
    }

    private static bool Has(ResearchContext ctx, string fact) => ctx.Ranges.GetValueOrDefault(fact).Last is not null;

    public static ResearchPlan Build(ResearchRequest r, ResearchContext ctx)
    {
        var kind = ResearchKinds.All.Contains(r.Kind) ? r.Kind : throw new ComputeException($"Шаблона исследования «{r.Kind}» нет. Есть: {string.Join(", ", ResearchKinds.All)}.");
        var defaultDays = kind switch
        {
            ResearchKinds.DeclineDrivers or ResearchKinds.AgentEfficiency or ResearchKinds.VisitsVsSales or ResearchKinds.CompanyHealth => 30,
            ResearchKinds.Anomalies => 60,
            ResearchKinds.PromoEffect => 60,
            _ => DefaultDays,
        };
        if (r.Params?.GetValueOrDefault("days") is { } ds && int.TryParse(ds, out var dd))
        {
            defaultDays = Math.Clamp(dd, 7, SemanticEngine.MaxDays);
        }

        var (from, to) = Period(r, ctx, defaultDays);
        var len = to.DayNumber - from.DayNumber + 1;
        var cTo = from.AddDays(-1);
        var cFrom = cTo.AddDays(-(len - 1));
        var visits = Has(ctx, SemanticCatalog.Visits) && ctx.Principal.Has(Permissions.SalesRead);
        var payments = Has(ctx, SemanticCatalog.Payments) && ctx.Principal.Has(Permissions.FinanceRead);
        var b = new Builder(r, from, to, cFrom, cTo);
        var limitations = new List<string>();
        if (ctx.LastFullDay() is (var full, true) && (r.To is null || r.To > full))
        {
            limitations.Add($"Последний день данных ({full.AddDays(1):dd.MM.yyyy}) загружен из Linko не полностью — исследование по {full:dd.MM.yyyy}.");
        }

        if (!visits)
        {
            limitations.Add(Has(ctx, SemanticCatalog.Visits) ? "Визиты недоступны в ваших правах." : "Визитов в OneBase нет — визиты в исследование не вошли.");
        }

        if (!payments && kind is ResearchKinds.CompanyHealth or ResearchKinds.Anomalies)
        {
            limitations.Add(ctx.Principal.Has(Permissions.FinanceRead) ? "Оплат в OneBase нет." : "Оплаты не вошли: нет права на финансы.");
        }

        var metric = r.Metric is { Length: > 0 } rm && SemanticCatalog.Metric(rm) is not null ? rm.Trim().ToLowerInvariant() : "revenue";
        string objective;
        IReadOnlyList<string> expected;
        switch (kind)
        {
            case ResearchKinds.Correlations:
                objective = "Найти статистически подтверждённые связи между показателями продаж, визитов, регионами и товарными группами";
                b.Compute("c_sales", "Матрица корреляций показателей продаж", "correlation_matrix", SalesMetrics, grain: "day");
                if (visits)
                {
                    b.Compute("c_visits", "Матрица корреляций продаж и визитов", "correlation_matrix",
                        ["revenue", "orders", "outlets", "visits_done", "visits_with_order", "visit_strike"], grain: "day", optional: true);
                    b.Compute("c_lag", "Запаздывание: визиты → заказы", "lag", ["visits_done", "orders"], grain: "day", prms: new() { ["max_lag"] = 7 }, optional: true);
                }

                b.Compute("c_groups", "Товарные группы: каждая против итога и против остальных", "correlation_matrix", [metric], grain: "day",
                    prms: new() { ["members"] = "product_group", ["top"] = 12 });
                b.Compute("c_regions", "Регионы: каждый против итога и против остальных", "correlation_matrix", [metric], grain: "day",
                    prms: new() { ["members"] = "region", ["top"] = 12 }, optional: true);
                expected = ["Подтверждённые связи (q < 0,05) с коэффициентом и интервалом", "Связи с оговорками (часть и целое, общий тренд)", "Исследовательские гипотезы"];
                break;

            case ResearchKinds.DeclineDrivers:
                objective = "Найти, где и за счёт чего изменились продажи, и проверить гипотезы о причинах";
                b.Query("q_totals", "Показатели к прошлому периоду", [.. SalesMetrics], [], compare: true);
                b.Compute("c_region", "Вклад регионов в изменение", "drivers", [metric], dimension: "region", compare: true);
                b.Compute("c_group", "Вклад товарных групп в изменение", "drivers", [metric], dimension: "product_group", compare: true);
                b.Compute("c_agent", "Вклад ТП в изменение", "drivers", [metric], dimension: "agent", compare: true);
                b.Compute("c_market", "Вклад торговых точек в изменение", "drivers", [metric], dimension: "market", compare: true, optional: true);
                if (metric == "revenue")
                {
                    b.Compute("c_pvm", "Цена — объём — ассортимент", "pvm", [], compare: true, optional: true);
                }

                b.Compute("c_churn", "Отток точек", "churn", [], compare: true, optional: true);
                b.Compute("c_cp", "Когда началось изменение (точки перелома)", "change_points", [metric], grain: "day", optional: true,
                    from: to.AddDays(-Math.Max(len * 3, 60) + 1));
                if (visits)
                {
                    b.Query("q_visits", "Визиты к прошлому периоду", ["visits_planned", "visits_done", "visits_with_order", "visit_strike", "route_execution"], [],
                        compare: true, optional: true);
                }

                expected = ["Изменение показателей (факт)", "Где изменилось: регионы, группы, ТП, точки", "За счёт чего: точки, заказы, средний чек, цена, ассортимент, возвраты, визиты",
                    "Проверка каждой гипотезы"];
                break;

            case ResearchKinds.AgentEfficiency:
                objective = "Сравнить ТП по визитам и продажам: дисциплина маршрута, конверсия визита в заказ, продажи на визит";
                if (!visits)
                {
                    b.Unavailable("q_agents", "Визиты и продажи ТП", ResearchStatuses.DataNotFound, limitations[0]);
                    expected = [];
                    break;
                }

                b.Query("q_agents", "Визиты и продажи по ТП",
                    ["revenue", "kg", "orders", "outlets", "visits_planned", "visits_done", "visits_done_in_plan", "visits_with_order", "route_execution", "visit_strike"],
                    ["agent"], limit: 5000);
                b.Compute("c_corr", "Связь визитов и продаж по ТП", "correlation", ["revenue", "visits_done", "visit_strike", "route_execution"], dimension: "agent");
                b.Compute("c_reg", "Регрессия продаж ТП на визиты и страйк", "regression", ["revenue", "visits_done", "visit_strike"], dimension: "agent", optional: true);
                b.Compute("c_clusters", "Группы похожих ТП", "clusters", ["revenue", "visits_done", "visit_strike"], dimension: "agent",
                    prms: new() { ["k"] = 4 }, optional: true);
                expected = ["Рейтинг ТП: продажи на визит, страйк, выполнение маршрута", "Связь визитов с продажами", "Группы ТП и кому что улучшать"];
                break;

            case ResearchKinds.VisitsVsSales:
                objective = "Найти регионы, где визитов больше, а продажи ниже, и проверить устойчивость этих трендов";
                if (!visits)
                {
                    b.Unavailable("q_regions", "Визиты и продажи по регионам", ResearchStatuses.DataNotFound, limitations[0]);
                    expected = [];
                    break;
                }

                b.Query("q_regions", "Визиты и продажи регионов к прошлому периоду", ["visits_done", "visits_with_order", "visit_strike", "revenue", "kg", "orders", "outlets"],
                    ["region"], compare: true);
                b.Query("q_daily", "Визиты и выручка регионов по дням", ["visits_done", "revenue"], ["date", "region"], grain: "day", from: cFrom, limit: 20000);
                expected = ["Изменение визитов и продаж каждого региона", "Регионы «визиты растут — продажи падают» с проверкой трендов"];
                break;

            case ResearchKinds.GroupInfluence:
                objective = "Определить, какие товарные группы сильнее всего определяют общую выручку: доля, колебания, изменение, связь с остальными";
                b.Query("q_daily_groups", "Выручка групп по дням", [metric], ["date", "product_group"], grain: "day", limit: 20000);
                b.Query("q_share", "Доля групп и изменение к прошлому периоду", [metric], ["product_group"], compare: true);
                b.Compute("c_matrix", "Группа против итога и против остальных", "correlation_matrix", [metric], grain: "day",
                    prms: new() { ["members"] = "product_group", ["top"] = 12 }, optional: true);
                b.Compute("c_drivers", "Вклад групп в изменение", "drivers", [metric], dimension: "product_group", compare: true, optional: true);
                expected = ["Доля групп в выручке", "Вклад групп в колебания итога (ковариация)", "Вклад в изменение к прошлому периоду", "Связь группы с остальными группами"];
                break;

            case ResearchKinds.Anomalies:
                objective = "Найти аномалии во всех доступных показателях с учётом дня недели";
                b.Compute("c_sales", "Аномалии показателей продаж", "anomaly_scan", SalesMetrics, grain: "day", prms: new() { ["check_last"] = Math.Min(len, 30) });
                if (visits)
                {
                    b.Compute("c_visits", "Аномалии визитов", "anomaly_scan", ["visits_done", "visits_with_order", "visit_strike", "route_execution"], grain: "day",
                        prms: new() { ["check_last"] = Math.Min(len, 30) }, optional: true);
                }

                if (payments)
                {
                    b.Compute("c_payments", "Аномалии оплат", "anomaly_scan", ["payments"], grain: "day", prms: new() { ["check_last"] = Math.Min(len, 30) }, optional: true);
                }

                expected = ["Аномалии по каждому показателю (дата, значение, ожидаемое, методы)", "Сдвиги уровня"];
                break;

            case ResearchKinds.PromoEffect:
                objective = "Оценить эффект акций: прирост продаж к контрольной группе, расход на скидки и отдачу";
                var promos = ctx.Promotions.Where(p => p.Products.Count > 0).Take(5).ToList();
                if (!ctx.Principal.Sales.Restricted)
                {
                    b.Op(new ResearchOperation
                    {
                        Id = "p_page", Type = "promo", Title = "Эффект акций (страница «Акции»)", Source = "PromoService",
                        Parameters = new Dictionary<string, string> { ["month"] = $"{to.Year}-{to.Month}" }, Optional = true,
                    });
                }
                else
                {
                    limitations.Add("Страница «Акции» считает всю страну — при ограниченной области используется только проверка по вашим срезам.");
                }

                foreach (var p in promos)
                {
                    var begin = p.Begin < from ? from : p.Begin;
                    var preLen = Math.Clamp((Math.Min(p.Till.DayNumber, to.DayNumber) - begin.DayNumber) + 1, 7, 60);
                    var start = begin.AddDays(-preLen);
                    var end = p.Till < to ? p.Till : to;
                    if (end <= begin)
                    {
                        continue;
                    }

                    b.Compute($"c_did_{p.Id}", $"Разность разностей: «{p.Name}»", "did", ["kg"], dimension: "product",
                        prms: new() { ["change_date"] = begin.ToString("yyyy-MM-dd"), ["treatment"] = p.Products.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList() },
                        from: start, to: end, optional: true);
                    b.Query($"q_disc_{p.Id}", $"Скидки и продажи товаров «{p.Name}»", ["discount", "revenue", "kg"], [], from: begin, to: end, optional: true,
                        filters: [new AnalyticsFilter("product", "in", p.Products.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList())]);
                }

                if (promos.Count == 0 && ctx.Principal.Sales.Restricted)
                {
                    b.Unavailable("p_none", "Акции периода", ResearchStatuses.DataNotFound, "Акций с товарами в периоде нет.");
                }

                limitations.Add("Строка заказа с акцией в Linko не связана: продажи акции — продажи её товаров в её даты; ROI — по выручке (себестоимости в OneBase нет).");
                expected = ["Прирост продаж товаров акции к контрольной группе (разность разностей)", "Скидки на товары акции и отдача на сум скидки"];
                break;

            default:
                objective = "Оценить состояние компании и выделить основные риски";
                b.Query("q_kpi", "Ключевые показатели к прошлому периоду", ["revenue", "kg", "orders", "outlets", "avg_check", "returns_revenue"], [], compare: true);
                if (visits)
                {
                    b.Query("q_visits", "Визиты к прошлому периоду", ["visits_done", "visit_strike", "route_execution"], [], compare: true, optional: true);
                }

                if (payments)
                {
                    b.Query("q_pay", "Оплаты и выручка к прошлому периоду", ["payments", "revenue"], [], compare: true, optional: true);
                }

                var month = new DateOnly(to.Year, to.Month, 1);
                b.Query("q_plan", "План и прогноз месяца по регионам", ["kg", "plan_kg", "plan_execution", "forecast_execution"], ["region"], grain: "month",
                    from: month, to: to, optional: true);
                b.Compute("c_trend", "Тренд выручки за 90 дней", "trend", ["revenue"], grain: "day", from: to.AddDays(-89), optional: true);
                b.Compute("c_anom", "Аномалии ключевых показателей", "anomaly_scan", visits ? ["revenue", "orders", "outlets", "visits_done"] : ["revenue", "orders", "outlets"],
                    grain: "day", prms: new() { ["check_last"] = 14 }, from: to.AddDays(-89), optional: true);
                b.Compute("c_churn", "Отток точек", "churn", [], compare: true, optional: true);
                if (!ctx.Principal.Sales.Restricted)
                {
                    b.Query("q_stock", "Товары с малым запасом", ["stock_units", "cover_days"], ["product"], sort: [new AnalyticsSort("cover_days", "asc")], limit: 300,
                        optional: true, period: false);
                }

                b.Op(new ResearchOperation { Id = "p_risks", Type = "risks", Title = "Открытые риски Аналитика", Source = "InsightStore", Optional = true });
                limitations.Add("Расходов, себестоимости и P&L компании в OneBase нет — финансовое состояние оценивается по продажам и поступлениям.");
                expected = ["Ключевые показатели и их изменение", "План и прогноз месяца", "Тренд и аномалии", "Отток, запасы, открытые риски"];
                break;
        }

        var plan = new ResearchPlan
        {
            Kind = kind,
            UserQuestion = r.Question ?? ResearchKinds.Title(kind),
            Objective = objective,
            From = from,
            To = to,
            CompareFrom = b.UsesCompare ? cFrom : null,
            CompareTo = b.UsesCompare ? cTo : null,
            RequiredMetrics = b.Metrics.Distinct().ToList(),
            RequiredSources = b.Operations.Select(o => o.Source).Concat(b.Metrics.Select(m => SemanticCatalog.Metric(m)?.Fact ?? "")).Where(x => x.Length > 0).Distinct().ToList(),
            Operations = b.Operations,
            ExpectedResults = expected,
            ValidationRules =
            [
                "Каждое число вывода — из результата операции со статусом SUCCESS или PARTIAL_RESULT",
                "Связь — только после проверки значимости; при переборе пар — поправка Бенджамини — Хохберга",
                "Корреляция не выдаётся за причину; причина — только при проверке с контрольной группой",
                "Неполный срез (complete = false) понижает вывод и попадает в ограничения",
            ],
            AccessScope = ctx.Principal.Sales.Restricted ? ctx.Principal.Sales.Description : "вся организация",
            Limitations = limitations,
        };
        return plan;
    }

    /// <summary>Сборщик операций плана с общими условиями запроса (фильтры, период, сравнение).</summary>
    private sealed class Builder(ResearchRequest r, DateOnly from, DateOnly to, DateOnly cFrom, DateOnly cTo)
    {
        public List<ResearchOperation> Operations { get; } = [];
        public List<string> Metrics { get; } = [];
        public bool UsesCompare { get; private set; }

        public void Op(ResearchOperation op) => Operations.Add(op);

        public void Unavailable(string id, string title, string status, string reason) => Operations.Add(new ResearchOperation
        {
            Id = id, Type = "query", Title = title, Source = "SemanticEngine", Status = status, ErrorReason = reason, Optional = false,
        });

        public void Query(string id, string title, IReadOnlyList<string> metrics, IReadOnlyList<string> dims, bool compare = false, string? grain = null,
            DateOnly? from = null, DateOnly? to = null, int? limit = null, bool optional = false, IReadOnlyList<AnalyticsFilter>? filters = null,
            IReadOnlyList<AnalyticsSort>? sort = null, bool period = true)
        {
            Metrics.AddRange(metrics);
            UsesCompare |= compare;
            Operations.Add(new ResearchOperation
            {
                Id = id, Type = "query", Title = title, Source = "SemanticEngine", Optional = optional,
                Query = new AnalyticsQuery
                {
                    Metrics = metrics, Dimensions = dims, DateFrom = period ? from ?? from0 : null, DateTo = period ? to ?? to0 : null, Grain = grain,
                    Filters = Merge(r.Filters, filters), Compare = compare ? "custom" : null, CompareFrom = compare ? cFrom : null, CompareTo = compare ? cTo : null,
                    Limit = limit ?? 2000, Sort = sort,
                },
            });
        }

        public void Compute(string id, string title, string method, IReadOnlyList<string> metrics, string? dimension = null, string? grain = null, bool compare = false,
            Dictionary<string, object>? prms = null, bool optional = false, DateOnly? from = null, DateOnly? to = null)
        {
            Metrics.AddRange(metrics);
            UsesCompare |= compare;
            Operations.Add(new ResearchOperation
            {
                Id = id, Type = "compute", Title = title, Source = "ComputeService", Optional = optional, TimeoutSeconds = 120,
                Compute = new ComputeRequest
                {
                    Method = method,
                    Data = new ComputeData
                    {
                        Metrics = metrics, Dimension = dimension, Grain = grain, From = from ?? from0, To = to ?? to0, Filters = r.Filters,
                        CompareFrom = compare ? cFrom : null, CompareTo = compare ? cTo : null,
                    },
                    Params = prms?.ToDictionary(x => x.Key, x => JsonSerializer.SerializeToElement(x.Value)),
                },
            });
        }

        private readonly DateOnly from0 = from;
        private readonly DateOnly to0 = to;

        private static IReadOnlyList<AnalyticsFilter>? Merge(IReadOnlyList<AnalyticsFilter>? a, IReadOnlyList<AnalyticsFilter>? b) =>
            a is null ? b : b is null ? a : [.. a, .. b];
    }
}

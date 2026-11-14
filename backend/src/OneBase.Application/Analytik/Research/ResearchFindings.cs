using System.Globalization;
using System.Text.Json;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.Application.Analytik.Research;

/// <summary>Таблица для экрана исследования: id, заголовок, колонки (код, подпись, единица), строки.</summary>
public sealed record ResearchTable(string Id, string Title, IReadOnlyList<ResearchColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

public sealed record ResearchColumn(string Code, string Title, string? Unit = null);

/// <summary>Матрица корреляций для экрана: подписи рядов, коэффициенты, q-значения, число наблюдений.</summary>
public sealed record ResearchMatrix(string Id, string Title, IReadOnlyList<string> Titles, IReadOnlyList<IReadOnlyList<double?>> R, IReadOnlyList<IReadOnlyList<double?>> Q, int N);

/// <summary>Данные исследования для экрана: таблицы и матрицы.</summary>
public sealed record ResearchView(IReadOnlyList<ResearchTable> Tables, IReadOnlyList<ResearchMatrix> Matrices);

/// <summary>Выводы, гипотезы, ограничения и данные экрана, построенные из результатов операций.</summary>
public sealed record ResearchBuild(IReadOnlyList<ResearchFinding> Findings, IReadOnlyList<ResearchHypothesis> Hypotheses, IReadOnlyList<string> Limitations, ResearchView Data);

/// <summary>
/// Выводы исследования — кодом, из результатов операций: факты (значения срезов), расчёты (разложения, доли, вклад), статистика (значимые
/// тренды, сдвиги, причинная проверка), корреляции (значимые связи после поправки), гипотезы (проверенные кодом объяснения и исследовательские
/// связи) и прогнозы. Каждый вывод ссылается на операции, из которых взят. Модель только пересказывает их — чисел она не добавляет.
/// </summary>
public static class ResearchFindings
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private sealed class Ctx(ResearchPlan plan, ResearchData data)
    {
        public ResearchPlan Plan { get; } = plan;
        public ResearchData Data { get; } = data;
        public List<ResearchFinding> Findings { get; } = [];
        public List<ResearchHypothesis> Hypotheses { get; } = [];
        public List<string> Limitations { get; } = [];
        public List<ResearchTable> Tables { get; } = [];
        public List<ResearchMatrix> Matrices { get; } = [];

        public void Add(string section, string level, string text, params string[] evidence) => Findings.Add(new ResearchFinding(section, level, text, null, null, evidence));

        public void Value(string section, string level, string text, double? value, string? unit, params string[] evidence) =>
            Findings.Add(new ResearchFinding(section, level, text, value, unit, evidence));

        public AnalyticsResult? Q(string id) => Data.Queries.GetValueOrDefault(id);

        public JsonElement? C(string id) => Data.Computes.TryGetValue(id, out var c) && c.Result.ValueKind == JsonValueKind.Object ? c.Result : null;
    }

    public static ResearchBuild Build(ResearchPlan plan, ResearchData data)
    {
        var c = new Ctx(plan, data);
        switch (plan.Kind)
        {
            case ResearchKinds.Correlations:
                Correlations(c);
                break;
            case ResearchKinds.DeclineDrivers:
                Decline(c);
                break;
            case ResearchKinds.AgentEfficiency:
                Agents(c);
                break;
            case ResearchKinds.VisitsVsSales:
                VisitsVsSales(c);
                break;
            case ResearchKinds.GroupInfluence:
                Groups(c);
                break;
            case ResearchKinds.Anomalies:
                Anomalies(c);
                break;
            case ResearchKinds.PromoEffect:
                Promo(c);
                break;
            default:
                Health(c);
                break;
        }

        if (c.Findings.Count == 0)
        {
            c.Add("summary", EvidenceLevels.Unverified, "Выводов нет: ни одна операция исследования не дала данных — причины в ограничениях.");
        }

        c.Add("limitations", EvidenceLevels.Fact, $"Период: {plan.From:dd.MM.yyyy} — {plan.To:dd.MM.yyyy}" +
                                                  (plan.CompareFrom is { } cf ? $", сравнение с {cf:dd.MM.yyyy} — {plan.CompareTo:dd.MM.yyyy}" : "") +
                                                  $"; область: {plan.AccessScope}.");
        return new ResearchBuild(c.Findings, c.Hypotheses, c.Limitations.Distinct().ToList(), new ResearchView(c.Tables, c.Matrices));
    }

    // ---------- корреляции ----------

    private static void Correlations(Ctx c)
    {
        var tested = 0;
        var confirmedTotal = 0;
        // Пары повторяются в разных матрицах (выручка × заказы — и в продажах, и в визитах): вывод о паре — один раз.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool First(JsonElement p) => seen.Add(string.Join("×", new[] { S(p, "a") ?? "", S(p, "b") ?? "" }.Order(StringComparer.Ordinal)));
        foreach (var (id, title) in new[] { ("c_sales", "Показатели продаж"), ("c_visits", "Продажи и визиты"), ("c_groups", "Товарные группы"), ("c_regions", "Регионы") })
        {
            if (c.C(id) is not { } r)
            {
                continue;
            }

            tested += (int)(D(r, "tested") ?? 0);
            Matrix(c, id, title, r);
            var confirmed = A(r, "confirmed").ToList();
            confirmedTotal += confirmed.Count;
            var members = id is "c_groups" or "c_regions";
            foreach (var p in confirmed.Where(p => (!members || S(p, "kind") == "part_rest") && First(p)).Take(members ? 4 : 6))
            {
                var text = members
                    ? $"«{S(p, "a")}» и «{S(p, "b")}» меняются вместе: r = {N2(D(p, "r"))}{Ci(p)}, q = {Pv(D(p, "q"))}, n = {D(p, "n"):0} ({CorrelationAnalysis.Strength(D(p, "r") ?? 0)} связь)."
                    : $"«{S(p, "a")}» и «{S(p, "b")}»: r = {N2(D(p, "r"))}{Ci(p)}, q = {Pv(D(p, "q"))}, n = {D(p, "n"):0} — {CorrelationAnalysis.Strength(D(p, "r") ?? 0)} связь" +
                      (D(p, "deseasonalized") is { } ds ? $"; без дня недели — {N2(ds)}" : "") + (D(p, "detrended") is { } dt ? $"; без общего тренда — {N2(dt)}" : "") + ".";
                c.Value("correlations", EvidenceLevels.Correlation, text, D(p, "r"), "r", id);
            }

            if (members)
            {
                // Часть и целое: с итогом связь завышена — показываем, как группа движется относительно остальных.
                foreach (var p in A(r, "flagged").Where(p => S(p, "kind") == "part_whole").Take(3))
                {
                    var member = S(p, "a")!.EndsWith("итог", StringComparison.Ordinal) ? S(p, "b") : S(p, "a");
                    var rest = A(r, "confirmed").Concat(A(r, "flagged")).Concat(A(r, "exploratory"))
                        .FirstOrDefault(x => S(x, "kind") == "part_rest" && (S(x, "a") == member || S(x, "b") == member));
                    c.Add("patterns", EvidenceLevels.Hypothesis,
                        $"«{member}» связана с итогом (r = {N2(D(p, "r"))}), но это часть итога — связь завышена" +
                        (rest.ValueKind == JsonValueKind.Object ? $"; с остальными {(id == "c_groups" ? "группами" : "регионами")} r = {N2(D(rest, "r"))} (q = {Pv(D(rest, "q"))})." : "."), id);
                }
            }

            foreach (var p in A(r, "flagged").Where(p => S(p, "kind") != "part_whole" && First(p)).Take(3))
            {
                // Главная оговорка: общий цикл или тренд, затем математическая зависимость, затем остальные.
                var warnings = A(p, "warnings").Select(w => w.GetString() ?? "").ToList();
                var main = warnings.FirstOrDefault(w => w.StartsWith("Связь объясняется", StringComparison.Ordinal) || w.StartsWith("Без общего тренда", StringComparison.Ordinal))
                           ?? warnings.FirstOrDefault(w => w.StartsWith("Математическая", StringComparison.Ordinal)) ?? warnings.FirstOrDefault() ?? "";
                c.Add("patterns", EvidenceLevels.Hypothesis, $"«{S(p, "a")}» × «{S(p, "b")}»: r = {N2(D(p, "r"))}, но {main.TrimEnd('.').ToLowerInvariant()}.", id);
            }

            var cycle = A(r, "flagged").Count(p => A(p, "warnings").Any(w => (w.GetString() ?? "").StartsWith("Связь объясняется", StringComparison.Ordinal)));
            if (cycle > 0)
            {
                c.Add("significance", EvidenceLevels.Calculated,
                    $"{title}: {cycle} связей значимы только из-за общего цикла дня недели (без него не подтверждаются) — они отнесены к оговоркам, а не к подтверждённым.", id);
            }

            foreach (var p in A(r, "exploratory").Where(First).Take(3))
            {
                c.Add("hypotheses", EvidenceLevels.Hypothesis,
                    $"Исследовательская гипотеза: «{S(p, "a")}» × «{S(p, "b")}» r = {N2(D(p, "r"))} значима только без поправки (p = {Pv(D(p, "p"))}, q = {Pv(D(p, "q"))}).", id);
            }
        }

        if (c.C("c_lag") is { } lag && O(lag, "lag") is { } l && O(l, "best") is { } best && D(l, "bestAdjustedP") is { } lp)
        {
            c.Add("correlations", lp < EvidenceClasses.Alpha ? EvidenceLevels.Correlation : EvidenceLevels.Hypothesis,
                $"Визиты и заказы: сильнейшая связь при сдвиге {D(best, "lag"):0} дн. (r = {N2(D(best, "r"))}, p с поправкой = {Pv(lp)})" +
                (lp < EvidenceClasses.Alpha ? "." : " — не подтверждена."), "c_lag");
        }

        c.Add("significance", EvidenceLevels.Calculated,
            $"Проверено пар: {tested}; подтверждённых связей (q < 0,05 после поправки Бенджамини — Хохберга): {confirmedTotal}.", "c_sales");
        c.Add("summary", confirmedTotal > 0 ? EvidenceLevels.Correlation : EvidenceLevels.Calculated,
            confirmedTotal > 0
                ? $"Найдено {confirmedTotal} статистически подтверждённых связей из {tested} проверенных пар; сильнейшие — в разделе «Корреляции». Это совместное изменение, не причина."
                : $"Из {tested} проверенных пар ни одна связь не подтвердилась после поправки на множественные сравнения.");
        c.Limitations.Add("Корреляция — не причина: общий фактор (сезон, день недели, акция) может двигать оба показателя.");
    }

    private static void Matrix(Ctx c, string id, string title, JsonElement r)
    {
        if (O(r, "matrix") is not { } m)
        {
            return;
        }

        var titles = A(m, "titles").Select(t => t.GetString() ?? "").ToList();
        List<IReadOnlyList<double?>> Grid(string name) => A(m, name).Select(row => (IReadOnlyList<double?>)row.EnumerateArray()
            .Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null).ToList()).ToList();
        c.Matrices.Add(new ResearchMatrix(id, title, titles, Grid("r"), Grid("q"), (int)(D(m, "n") ?? 0)));
    }

    // ---------- причины снижения ----------

    private static void Decline(Ctx c)
    {
        var t = c.Q("q_totals");
        var metric = c.Plan.RequiredMetrics.FirstOrDefault(m => m is "revenue" or "kg" or "orders") ?? "revenue";
        double? Cur(string m) => t is null ? null : (double?)t.Totals.GetValueOrDefault(m);
        double? Prev(string m) => (double?)t?.CompareTotals?.GetValueOrDefault(m);
        double? Pct(string m) => Cur(m) is { } a && Prev(m) is { } b && b != 0 ? (a - b) / Math.Abs(b) * 100 : null;
        var mainPct = Pct("revenue");
        if (t is not null)
        {
            var rows = new List<IReadOnlyList<object?>>();
            foreach (var m in new[] { "revenue", "kg", "orders", "outlets", "avg_check", "returns_revenue", "discount" })
            {
                if (Cur(m) is not { } cur)
                {
                    continue;
                }

                var def = SemanticCatalog.Metric(m)!;
                rows.Add([def.Title, Prev(m), cur, Pct(m)]);
                if (m is "revenue" or "kg" or "orders" or "outlets" or "avg_check")
                {
                    c.Value("metrics", EvidenceLevels.Fact, $"{def.Title}: {Num(Prev(m))} → {Num(cur)} {def.Unit} ({Signed(Pct(m))}).", Pct(m), "%", "q_totals");
                }
            }

            c.Tables.Add(new ResearchTable("totals", "Показатели к прошлому периоду",
                [new("metric", "Показатель"), new("prev", "Было"), new("cur", "Стало"), new("pct", "Изменение", "%")], rows));
            c.Add("summary", EvidenceLevels.Fact, mainPct is { } mp
                ? mp < 0
                    ? $"Выручка снизилась на {N1(-mp)} %: {Num(Prev("revenue"))} → {Num(Cur("revenue"))} сум (с {c.Plan.From:dd.MM} по {c.Plan.To:dd.MM} к предыдущим {c.Plan.To.DayNumber - c.Plan.From.DayNumber + 1} дням)."
                    : $"Выручка не снизилась: {Signed(mp)} ({Num(Prev("revenue"))} → {Num(Cur("revenue"))} сум); ниже — что изменилось внутри."
                : "Изменение выручки не посчитано — нет данных одного из периодов.", "q_totals");
        }

        // Разложение изменения выручки (логарифмическое — части в сумме дают всё изменение).
        if (Cur("revenue") is > 0 && Prev("revenue") is > 0 && Cur("orders") is > 0 && Prev("orders") is > 0 && Cur("outlets") is > 0 && Prev("outlets") is > 0)
        {
            var total = Math.Log(Cur("revenue")!.Value / Prev("revenue")!.Value);
            var byOutlets = Math.Log(Cur("outlets")!.Value / Prev("outlets")!.Value);
            var byOrdersPerOutlet = Math.Log(Cur("orders")!.Value / Cur("outlets")!.Value / (Prev("orders")!.Value / Prev("outlets")!.Value));
            var byCheck = total - byOutlets - byOrdersPerOutlet;
            var pct = mainPct ?? 0;
            double Part(double x) => Math.Abs(total) < 1e-9 ? 0 : x / total * pct;
            c.Add("causes", EvidenceLevels.Calculated,
                $"Разложение изменения выручки ({Signed(pct)}): число покупающих точек {SignedPp(Part(byOutlets))}, заказов на точку {SignedPp(Part(byOrdersPerOutlet))}, " +
                $"средний чек {SignedPp(Part(byCheck))}", "q_totals");
            var parts = new[] { ("outlets", "Меньше покупающих точек (АКБ)", byOutlets), ("frequency", "Реже заказы на точку", byOrdersPerOutlet), ("check", "Ниже средний чек", byCheck) };
            foreach (var (code, statement, part) in parts)
            {
                var share = Math.Abs(total) < 1e-9 ? 0 : part / total;
                var status = total >= 0 ? (part < 0 ? "partially_confirmed" : "rejected")
                    : share >= 0.5 ? "confirmed" : share >= 0.2 ? "partially_confirmed" : "rejected";
                c.Hypotheses.Add(new ResearchHypothesis(code, statement, status, EvidenceLevels.Calculated,
                    $"доля в изменении выручки {N1(share * 100)} % (логарифмическое разложение)", ["q_totals"]));
            }
        }

        // Где изменилось.
        foreach (var (id, title, dim) in new[] { ("c_region", "Регионы", "регион"), ("c_group", "Товарные группы", "группа"), ("c_agent", "ТП", "ТП"), ("c_market", "Точки", "точка") })
        {
            if (c.C(id) is not { } r)
            {
                continue;
            }

            var items = A(r, "items").ToList();
            var delta = D(r, "delta") ?? 0;
            var against = items.Where(i => Math.Sign(D(i, "delta") ?? 0) == Math.Sign(delta) && delta != 0).Take(3).ToList();
            if (against.Count > 0)
            {
                c.Add("differences", EvidenceLevels.Calculated,
                    $"{title}: {(delta < 0 ? "снижение дали" : "рост дали")} " + string.Join(", ", against.Select(i =>
                        $"{S(i, "key")} {Num(D(i, "delta"))} ({Signed(D(i, "changePct"))}, {N1((D(i, "shareOfNet") ?? 0) * 100)} % изменения)")) + ".", id);
            }

            if (D(r, "top3ShareOfGross") is { } top3 && id != "c_market")
            {
                var concentrated = top3 >= 0.6;
                c.Hypotheses.Add(new ResearchHypothesis($"concentration_{id}", $"Изменение сосредоточено в нескольких: {title.ToLowerInvariant()}",
                    concentrated ? "confirmed" : top3 >= 0.4 ? "partially_confirmed" : "rejected", EvidenceLevels.Calculated,
                    $"три крупнейших участника дают {N1(top3 * 100)} % всех изменений", [id]));
            }

            c.Tables.Add(new ResearchTable(id, $"Вклад: {title.ToLowerInvariant()}",
                [new("key", dim), new("a", "Было"), new("b", "Стало"), new("delta", "Изменение"), new("pct", "Изменение, %", "%"), new("share", "Доля изменения, %", "%")],
                items.Take(30).Select(i => (IReadOnlyList<object?>)[S(i, "key"), D(i, "a"), D(i, "b"), D(i, "delta"), D(i, "changePct"), D(i, "shareOfNet") * 100]).ToList()));
        }

        if (c.C("c_pvm") is { } pvmRoot && O(pvmRoot, "pvm") is { } pvm)
        {
            var d = D(pvm, "delta") ?? 0;
            var effects = new[] { ("price", "Цена", D(pvm, "price") ?? 0), ("volume", "Объём", D(pvm, "volume") ?? 0), ("mix", "Ассортимент (структура)", D(pvm, "mix") ?? 0),
                ("new", "Новые позиции", D(pvm, "newItems") ?? 0), ("lost", "Выбывшие позиции", D(pvm, "lostItems") ?? 0) };
            c.Add("causes", EvidenceLevels.Calculated, $"Цена — объём — ассортимент: изменение выручки {Num(d)} = " +
                                                       string.Join(" + ", effects.Select(e => $"{e.Item2.ToLowerInvariant()} {Num(e.Item3)}")) + ".", "c_pvm");
            foreach (var (code, title, value) in effects.Where(e => e.Item1 is "price" or "volume" or "mix"))
            {
                var share = d == 0 ? 0 : value / d;
                var status = d < 0 && value < 0 ? share >= 0.5 ? "confirmed" : share >= 0.2 ? "partially_confirmed" : "rejected" : "rejected";
                c.Hypotheses.Add(new ResearchHypothesis($"pvm_{code}", $"Изменение — из-за: {title.ToLowerInvariant()}", status, EvidenceLevels.Calculated,
                    $"эффект {Num(value)} сум ({N1(share * 100)} % изменения)", ["c_pvm"]));
            }
        }

        if (Cur("returns_revenue") is { } rc && Prev("returns_revenue") is { } rp && Cur("revenue") is { } rv && Prev("revenue") is { } rvp)
        {
            var dRet = rc - rp;
            var dRev = rv - rvp;
            var share = dRev < 0 && dRet > 0 ? dRet / -dRev : 0;
            c.Hypotheses.Add(new ResearchHypothesis("returns", "Выросли возвраты", share >= 0.5 ? "confirmed" : share >= 0.15 ? "partially_confirmed" : "rejected",
                EvidenceLevels.Calculated, $"возвраты {Num(rp)} → {Num(rc)} сум ({(dRev < 0 ? $"{N1(share * 100)} % снижения" : "выручка не снизилась")})", ["q_totals"]));
        }

        if (c.Q("q_visits") is { } v)
        {
            double? Vc(string m) => (double?)v.Totals.GetValueOrDefault(m);
            double? Vp(string m) => (double?)v.CompareTotals?.GetValueOrDefault(m);
            double? Vpct(string m) => Vc(m) is { } a && Vp(m) is { } b && b != 0 ? (a - b) / Math.Abs(b) * 100 : null;
            c.Add("metrics", EvidenceLevels.Fact,
                $"Визиты: выполнено {Num(Vp("visits_done"))} → {Num(Vc("visits_done"))} ({Signed(Vpct("visits_done"))}); страйк {N1(Vp("visit_strike"))} → {N1(Vc("visit_strike"))} %; " +
                $"выполнение маршрута {N1(Vp("route_execution"))} → {N1(Vc("route_execution"))} %.", "q_visits");
            var fewer = Vpct("visits_done") is < -5;
            var weaker = Vc("visit_strike") is { } s1 && Vp("visit_strike") is { } s0 && s1 - s0 < -2;
            var ordersDown = Pct("orders") is < 0;
            c.Hypotheses.Add(new ResearchHypothesis("visits", "Меньше визитов или ниже их конверсия в заказ",
                ordersDown && (fewer || weaker) ? fewer && weaker ? "confirmed" : "partially_confirmed" : "rejected", EvidenceLevels.Correlation,
                $"визиты {Signed(Vpct("visits_done"))}, страйк {SignedPp(Vc("visit_strike") - Vp("visit_strike"))}, заказы {Signed(Pct("orders"))} — совпадение по времени, а не доказанная причина",
                ["q_visits", "q_totals"]));
        }

        if (c.C("c_churn") is { } churn)
        {
            var lostShare = D(churn, "lostShare") ?? 0;
            var riskShare = D(churn, "revenueAtRiskShare") ?? 0;
            c.Add("risks", EvidenceLevels.Calculated,
                $"Не купили в периоде {D(churn, "lost"):0} из {D(churn, "baseCustomers"):0} точек прошлого периода ({N1(lostShare * 100)} %); их выручка в прошлом периоде — {N1(riskShare * 100)} %.",
                "c_churn");
            c.Hypotheses.Add(new ResearchHypothesis("churn", "Ушли покупатели", riskShare >= 0.15 ? "confirmed" : riskShare >= 0.07 ? "partially_confirmed" : "rejected",
                EvidenceLevels.Calculated, $"не купили {N1(lostShare * 100)} % точек, их доля выручки {N1(riskShare * 100)} %", ["c_churn"]));
        }

        if (c.C("c_cp") is { } cp)
        {
            foreach (var p in A(cp, "points").Where(p => D(p, "p") < EvidenceClasses.Alpha).TakeLast(2))
            {
                c.Add("patterns", EvidenceLevels.Statistical,
                    $"Уровень сместился с {S(p, "date")}: {Num(D(p, "meanBefore"))} → {Num(D(p, "meanAfter"))} в день ({Signed(D(p, "shiftPct"))}, p = {Pv(D(p, "p"))}).", "c_cp");
            }
        }

        foreach (var h in c.Hypotheses.Where(h => h.Status == "confirmed").Take(4))
        {
            c.Add("recommendations", EvidenceLevels.Hypothesis, h.Id switch
            {
                "outlets" or "churn" => "Вернуть точки: список не купивших — в задачи ТП с приоритетом по прошлой выручке.",
                "frequency" => "Частота заказов упала — проверить маршруты и пропуски визитов в точках с падением.",
                "check" or "pvm_mix" => "Средний чек ниже — проверить наличие ключевых SKU и ассортимент в точках, дающих снижение.",
                "pvm_price" => "Эффект цены отрицательный — проверить скидки и прайс в регионах со снижением.",
                "pvm_volume" => "Падает объём — сверить с остатками: нет ли дефицита ходовых позиций.",
                "returns" => "Возвраты выросли — разобрать причины возвратов по точкам и товарам.",
                "visits" => "Визитов или конверсии меньше — разобрать ТП с падением страйка (исследование «Эффективность ТП»).",
                _ => $"Сосредоточиться на участниках, давших изменение ({h.Basis}).",
            }, h.Evidence.ToArray());
        }

        c.Limitations.Add("Разложение показывает, где и за счёт чего изменилась выручка, но не доказывает причину; «подтверждена» у гипотезы — по арифметике разложения.");
    }

    // ---------- эффективность ТП ----------

    private static void Agents(Ctx c)
    {
        if (c.Q("q_agents") is not { } q)
        {
            return;
        }

        var col = q.Columns.Select((x, i) => (x.Code, i)).ToDictionary(x => x.Code, x => x.i);
        double? V(object?[] row, string m) => col.TryGetValue(m, out var i) && row[i] is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;
        var agents = q.Rows.Select(r => new
        {
            Name = r[0]?.ToString() ?? "—", Revenue = V(r, "revenue") ?? 0, Done = V(r, "visits_done") ?? 0, Planned = V(r, "visits_planned") ?? 0,
            WithOrder = V(r, "visits_with_order") ?? 0, Route = V(r, "route_execution"), Strike = V(r, "visit_strike"), Outlets = V(r, "outlets") ?? 0, Orders = V(r, "orders") ?? 0,
        }).Where(a => a.Done > 0 || a.Revenue > 0).ToList();
        // В сравнение — пользователи, у которых визиты дают заказы: у кого с визитов нет ни одного заказа (супервайзеры, мерчендайзеры,
        // операторы, вводящие заказы без визита), «выручка на визит» смысла не имеет — они перечисляются отдельно.
        var withVisits = agents.Where(a => a.Done >= 20).ToList();
        var noOrders = withVisits.Where(a => a.WithOrder == 0).ToList();
        var active = withVisits.Where(a => a.WithOrder > 0)
            .Select(a => new { a.Name, a.Revenue, a.Done, a.Planned, a.Route, a.Strike, a.Outlets, PerVisit = a.Revenue / a.Done }).ToList();
        if (active.Count == 0)
        {
            c.Add("summary", EvidenceLevels.Fact, "ТП с 20 и более визитами за период нет — сравнивать эффективность не по чему.", "q_agents");
            return;
        }

        var medStrike = StatMath.Median(active.Where(a => a.Strike is not null).Select(a => a.Strike!.Value).DefaultIfEmpty(0).ToList());
        var medRoute = StatMath.Median(active.Where(a => a.Route is not null).Select(a => a.Route!.Value).DefaultIfEmpty(0).ToList());
        var medPer = StatMath.Median(active.Select(a => a.PerVisit).ToList());
        c.Add("summary", EvidenceLevels.Calculated,
            $"ТП с визитами (≥ 20 за период) и заказами с визитов: {active.Count}. Медиана: страйк {N1(medStrike)} %, выполнение маршрута {N1(medRoute)} %, " +
            $"выручка на визит {Num(medPer)} сум.", "q_agents");
        if (noOrders.Count > 0)
        {
            c.Add("differences", EvidenceLevels.Fact,
                $"С визитами, но без единого заказа с визита — {noOrders.Count} (в сравнение не вошли: вероятно, не ТП — супервайзеры, мерчендайзеры, операторы): " +
                string.Join(", ", noOrders.OrderByDescending(a => a.Done).Take(5).Select(a => $"{a.Name} ({a.Done:0} виз.)")) + (noOrders.Count > 5 ? "…" : "") + ".", "q_agents");
        }
        var best = active.OrderByDescending(a => a.PerVisit).Take(3).ToList();
        var worst = active.OrderBy(a => a.PerVisit).Take(3).ToList();
        c.Add("differences", EvidenceLevels.Calculated, "Больше всего выручки на визит: " + string.Join(", ", best.Select(a => $"{a.Name} — {Num(a.PerVisit)} (страйк {N1(a.Strike)} %)")) + ".", "q_agents");
        c.Add("differences", EvidenceLevels.Calculated, "Меньше всего выручки на визит: " + string.Join(", ", worst.Select(a => $"{a.Name} — {Num(a.PerVisit)} (страйк {N1(a.Strike)} %)")) + ".", "q_agents");
        var walkers = active.Where(a => a.Route >= Math.Max(80, medRoute) && a.Strike < medStrike * 0.7).OrderBy(a => a.Strike).Take(5).ToList();
        if (walkers.Count > 0)
        {
            c.Add("risks", EvidenceLevels.Calculated, "Маршрут выполняют, но заказов мало (страйк ниже 70 % медианы): " +
                                                      string.Join(", ", walkers.Select(a => $"{a.Name} (маршрут {N1(a.Route)} %, страйк {N1(a.Strike)} %)")) + ".", "q_agents");
        }

        var skippers = active.Where(a => a.Strike >= medStrike && a.Route < medRoute - 15).OrderBy(a => a.Route).Take(5).ToList();
        if (skippers.Count > 0)
        {
            c.Add("risks", EvidenceLevels.Calculated, "Продают хорошо, но пропускают маршрут: " +
                                                      string.Join(", ", skippers.Select(a => $"{a.Name} (маршрут {N1(a.Route)} %, страйк {N1(a.Strike)} %)")) + ".", "q_agents");
        }

        if (c.C("c_corr") is { } corr)
        {
            foreach (var r in A(corr, "reports"))
            {
                var status = S(r, "status");
                var level = S(r, "level") ?? EvidenceLevels.Unverified;
                c.Value("correlations", status is "valid" or "warning" ? level : EvidenceLevels.Unverified,
                    $"По ТП: «{Title(S(r, "metric_a"))}» × «{Title(S(r, "metric_b"))}» r = {N2(D(r, "coefficient"))} ({(D(r, "q_value") is { } qv ? $"q = {Pv(qv)}" : $"p = {Pv(D(r, "p_value"))}")}, " +
                    $"n = {D(r, "sample_size"):0}) — {(status is "valid" or "warning" ? "связь подтверждена" : "не подтверждена")}.", D(r, "coefficient"), "r", "c_corr");
            }
        }

        if (c.C("c_reg") is { } reg && A(reg, "coefficients").Skip(1).ToList() is { Count: > 0 } coefs)
        {
            c.Add("correlations", EvidenceLevels.Correlation, "Регрессия выручки ТП: " + string.Join("; ", coefs.Select(t =>
                $"{Title(S(t, "name"))} {Num(D(t, "coefficient"))} на единицу [{Num(D(t, "lower"))}; {Num(D(t, "upper"))}], p = {Pv(D(t, "p"))}")) +
                $"; R² = {N2(D(O(reg, "regression") ?? default, "rSquared"))}. Это связь в данных, а не эффект добавленного визита.", "c_reg");
        }

        c.Tables.Add(new ResearchTable("agents", "ТП: визиты и продажи",
            [new("agent", "ТП"), new("revenue", "Выручка", "сум"), new("done", "Визитов"), new("route", "Маршрут", "%"), new("strike", "Страйк", "%"),
             new("perVisit", "Выручка на визит", "сум"), new("outlets", "АКБ")],
            active.OrderByDescending(a => a.PerVisit).Select(a => (IReadOnlyList<object?>)[a.Name, a.Revenue, a.Done, a.Route, a.Strike, a.PerVisit, a.Outlets]).ToList()));
        if (walkers.Count > 0)
        {
            c.Add("recommendations", EvidenceLevels.Hypothesis, "ТП с выполненным маршрутом и низким страйком — разобрать, что предлагают в точке (ассортимент, акции, остатки дилера).", "q_agents");
        }

        if (skippers.Count > 0)
        {
            c.Add("recommendations", EvidenceLevels.Hypothesis, "ТП с хорошим страйком и пропусками маршрута — вернуть дисциплину маршрута: у них визит чаще даёт заказ.", "q_agents");
        }

        c.Limitations.Add("ТП с менее чем 20 визитами за период в сравнение не вошли (слишком мало данных).");
    }

    // ---------- визиты и продажи по регионам ----------

    private static void VisitsVsSales(Ctx c)
    {
        if (c.Q("q_regions") is not { } q)
        {
            return;
        }

        var col = q.Columns.Select((x, i) => (x.Code, i)).ToDictionary(x => x.Code, x => x.i);
        double? V(object?[] row, string m) => col.TryGetValue(m, out var i) && row[i] is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;
        var trends = new Dictionary<string, (TrendResult? Visits, TrendResult? Revenue)>();
        if (c.Q("q_daily") is { } daily)
        {
            var dc = daily.Columns.Select((x, i) => (x.Code, i)).ToDictionary(x => x.Code, x => x.i);
            foreach (var g in daily.Rows.GroupBy(r => r[dc["region"]]?.ToString() ?? "—"))
            {
                var ordered = g.OrderBy(r => r[dc["date"]]?.ToString()).ToList();
                if (ordered.Count < 10)
                {
                    continue;
                }

                TrendResult? T(string m) => ordered.Select(r => r[dc[m]] is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0).ToList() is var s && StatMath.Variance(s) > 0
                    ? SeriesMath.Trend(s) : null;
                trends[g.Key] = (T("visits_done"), T("revenue"));
            }
        }

        var regions = q.Rows.Select(r => new
        {
            Name = r[0]?.ToString() ?? "—", Visits = V(r, "visits_done"), VisitsPct = V(r, "visits_done__pct"), Revenue = V(r, "revenue"), RevenuePct = V(r, "revenue__pct"),
            Strike = V(r, "visit_strike"), StrikePrev = V(r, "visit_strike__prev"), OutletsPct = V(r, "outlets__pct"),
        }).ToList();
        var flagged = regions.Where(r => r.VisitsPct > 3 && r.RevenuePct < -3).OrderBy(r => r.RevenuePct).ToList();
        c.Add("summary", flagged.Count > 0 ? EvidenceLevels.Calculated : EvidenceLevels.Fact,
            flagged.Count > 0
                ? $"Регионов, где визитов больше, а выручка ниже, чем в прошлом периоде: {flagged.Count} — {string.Join(", ", flagged.Select(r => r.Name))}."
                : "Регионов, где визиты растут, а выручка падает (больше ±3 %), нет.", "q_regions");
        var compared = regions.Where(r => r.VisitsPct is not null && r.RevenuePct is not null).ToList();
        if (flagged.Count == 0 && compared.Count > 0)
        {
            // Совпадений нет — общая картина и ближайшие случаи, чтобы «нет» было видно на цифрах, а не только на слове.
            c.Add("metrics", EvidenceLevels.Calculated,
                $"Регионов в сравнении: {compared.Count}; визитов больше (> +3 %) — в {compared.Count(r => r.VisitsPct > 3)}, меньше (< −3 %) — в {compared.Count(r => r.VisitsPct < -3)}; " +
                $"выручка выше (> +3 %) — в {compared.Count(r => r.RevenuePct > 3)}, ниже (< −3 %) — в {compared.Count(r => r.RevenuePct < -3)}.", "q_regions");
            var closest = compared.OrderBy(r => r.RevenuePct - r.VisitsPct).Take(3).ToList();
            c.Add("differences", EvidenceLevels.Calculated,
                "Ближе всего к условию (выручка отстаёт от визитов сильнее всего): " +
                string.Join("; ", closest.Select(r => $"{r.Name} — визиты {Signed(r.VisitsPct)}, выручка {Signed(r.RevenuePct)}, страйк {N1(r.StrikePrev)} → {N1(r.Strike)} %")) + ".",
                "q_regions");
        }

        foreach (var r in flagged)
        {
            var tr = trends.GetValueOrDefault(r.Name);
            var sig = tr.Visits is { } tv && tr.Revenue is { } trv && tv.Slope > 0 && trv.Slope < 0 && tv.P < EvidenceClasses.Alpha && trv.P < EvidenceClasses.Alpha;
            c.Add("differences", sig ? EvidenceLevels.Statistical : EvidenceLevels.Calculated,
                $"{r.Name}: визиты {Signed(r.VisitsPct)}, выручка {Signed(r.RevenuePct)}, страйк {N1(r.StrikePrev)} → {N1(r.Strike)} %, АКБ {Signed(r.OutletsPct)}" +
                (sig ? $"; по дням оба тренда значимы (визиты p = {Pv(tr.Visits!.P)}, выручка p = {Pv(tr.Revenue!.P)})." : tr.Visits is not null ? "; по дням тренды не значимы — возможно, колебание." : "."),
                "q_regions", "q_daily");
            if (r.Strike < r.StrikePrev)
            {
                c.Hypotheses.Add(new ResearchHypothesis($"strike_{r.Name}", $"{r.Name}: визиты чаще заканчиваются без заказа", "confirmed", EvidenceLevels.Calculated,
                    $"страйк {N1(r.StrikePrev)} → {N1(r.Strike)} %", ["q_regions"]));
            }
        }

        c.Tables.Add(new ResearchTable("regions", "Регионы: визиты и выручка к прошлому периоду",
            [new("region", "Регион"), new("visits", "Визитов"), new("visitsPct", "Визиты, %", "%"), new("revenue", "Выручка", "сум"), new("revenuePct", "Выручка, %", "%"),
             new("strike", "Страйк", "%"), new("strikePrev", "Страйк был", "%")],
            regions.OrderBy(r => (r.RevenuePct ?? 0) - (r.VisitsPct ?? 0)).Select(r => (IReadOnlyList<object?>)[r.Name, r.Visits, r.VisitsPct, r.Revenue, r.RevenuePct, r.Strike, r.StrikePrev]).ToList()));
        if (flagged.Count > 0)
        {
            c.Add("recommendations", EvidenceLevels.Hypothesis, "В этих регионах визиты не превращаются в продажи — проверить предложение в точке, остатки дилера и цены, а не число визитов.", "q_regions");
        }

        c.Limitations.Add("Регион визита — по филиалу точки, регион продажи — по филиалу заказа: у точек другого филиала регионы могут не совпасть.");
    }

    // ---------- влияние товарных групп ----------

    private static void Groups(Ctx c)
    {
        var rows = new Dictionary<string, (double Share, double? VarShare, double? Delta, double? ContributionPct, double? RestR, double? RestQ)>();
        if (c.Q("q_share") is { } s)
        {
            var col = s.Columns.Select((x, i) => (x.Code, i)).ToDictionary(x => x.Code, x => x.i);
            var metric = s.Columns.First(x => x.Kind == "metric").Code;
            var total = (double)(s.Totals.GetValueOrDefault(metric) ?? 0);
            var prevTotal = (double?)s.CompareTotals?.GetValueOrDefault(metric);
            foreach (var r in s.Rows)
            {
                var v = r[col[metric]] is { } x ? Convert.ToDouble(x, CultureInfo.InvariantCulture) : 0;
                var d = col.TryGetValue(metric + "__delta", out var di) && r[di] is { } dx ? Convert.ToDouble(dx, CultureInfo.InvariantCulture) : (double?)null;
                rows[r[0]?.ToString() ?? "—"] = (total == 0 ? 0 : v / total, null, d, prevTotal is { } pt && total - pt != 0 && d is { } dd ? dd / (total - pt) * 100 : null, null, null);
            }
        }

        // Вклад в колебания итога: Cov(группа, итог) / Var(итог) — доли в сумме дают 100 %.
        if (c.Q("q_daily_groups") is { } daily)
        {
            var dc = daily.Columns.Select((x, i) => (x.Code, i)).ToDictionary(x => x.Code, x => x.i);
            var metric = daily.Columns.First(x => x.Kind == "metric").Code;
            var dates = daily.Rows.Select(r => r[dc["date"]]?.ToString() ?? "").Distinct().OrderBy(x => x).ToList();
            var idx = dates.Select((d, i) => (d, i)).ToDictionary(x => x.d, x => x.i);
            var series = daily.Rows.GroupBy(r => r[dc["product_group"]]?.ToString() ?? "—").ToDictionary(g => g.Key, g =>
            {
                var v = new double[dates.Count];
                foreach (var r in g)
                {
                    v[idx[r[dc["date"]]?.ToString() ?? ""]] += r[dc[metric]] is { } x ? Convert.ToDouble(x, CultureInfo.InvariantCulture) : 0;
                }

                return v;
            });
            var totalSeries = Enumerable.Range(0, dates.Count).Select(i => series.Values.Sum(v => v[i])).ToArray();
            var varT = dates.Count > 2 ? StatMath.Variance(totalSeries) : 0;
            if (varT > 0)
            {
                var meanT = totalSeries.Average();
                foreach (var (g, v) in series)
                {
                    var meanG = v.Average();
                    var cov = Enumerable.Range(0, v.Length).Sum(i => (v[i] - meanG) * (totalSeries[i] - meanT)) / (v.Length - 1);
                    var cur = rows.GetValueOrDefault(g);
                    rows[g] = cur with { VarShare = cov / varT * 100 };
                }
            }
        }

        if (c.C("c_matrix") is { } m)
        {
            Matrix(c, "c_matrix", "Группы: итог и остальные", m);
            foreach (var p in A(m, "confirmed").Concat(A(m, "flagged")).Concat(A(m, "exploratory")).Where(p => S(p, "kind") == "part_rest"))
            {
                var member = S(p, "a")!.StartsWith("Все, кроме", StringComparison.Ordinal) ? S(p, "b")! : S(p, "a")!;
                if (rows.TryGetValue(member, out var cur))
                {
                    rows[member] = cur with { RestR = D(p, "r"), RestQ = D(p, "q") };
                }
            }
        }

        if (rows.Count == 0)
        {
            return;
        }

        var byVar = rows.Where(x => x.Value.VarShare is not null).OrderByDescending(x => x.Value.VarShare).Take(3).ToList();
        var byShare = rows.OrderByDescending(x => x.Value.Share).Take(3).ToList();
        c.Add("summary", EvidenceLevels.Calculated,
            (byVar.Count > 0 ? $"Сильнее всего колебания общей выручки определяют: {string.Join(", ", byVar.Select(x => $"«{x.Key}» — {N1(x.Value.VarShare)} % дисперсии итога при доле {N1(x.Value.Share * 100)} % выручки"))}. " : "") +
            $"Крупнейшие по доле: {string.Join(", ", byShare.Select(x => $"«{x.Key}» {N1(x.Value.Share * 100)} %"))}.", "q_daily_groups", "q_share");
        foreach (var x in byVar.Where(x => x.Value.VarShare > x.Value.Share * 100 * 1.3))
        {
            c.Add("patterns", EvidenceLevels.Calculated,
                $"«{x.Key}» колеблет выручку сильнее своей доли: {N1(x.Value.VarShare)} % дисперсии при {N1(x.Value.Share * 100)} % выручки — нестабильная группа.", "q_daily_groups");
        }

        foreach (var x in rows.Where(x => x.Value.ContributionPct is not null).OrderByDescending(x => Math.Abs(x.Value.ContributionPct!.Value)).Take(3))
        {
            c.Add("differences", EvidenceLevels.Calculated, $"«{x.Key}»: изменение к прошлому периоду {Num(x.Value.Delta)} сум — {N1(x.Value.ContributionPct)} % изменения итога.", "q_share");
        }

        foreach (var x in rows.Where(x => x.Value.RestQ is < 0.05).OrderByDescending(x => Math.Abs(x.Value.RestR ?? 0)).Take(3))
        {
            c.Add("correlations", EvidenceLevels.Correlation, $"«{x.Key}» движется вместе с остальными группами: r = {N2(x.Value.RestR)} (q = {Pv(x.Value.RestQ)}) — общий спрос, а не сама группа.", "c_matrix");
        }

        c.Tables.Add(new ResearchTable("groups", "Товарные группы: доля, колебания, изменение",
            [new("group", "Группа"), new("share", "Доля выручки", "%"), new("varShare", "Доля колебаний итога", "%"), new("delta", "Изменение", "сум"),
             new("contribution", "Доля изменения итога", "%"), new("restR", "r с остальными"), new("restQ", "q")],
            rows.OrderByDescending(x => x.Value.VarShare ?? x.Value.Share * 100)
                .Select(x => (IReadOnlyList<object?>)[x.Key, x.Value.Share * 100, x.Value.VarShare, x.Value.Delta, x.Value.ContributionPct, x.Value.RestR, x.Value.RestQ]).ToList()));
        c.Limitations.Add("«Влияние» — арифметический вклад группы в итог и его колебания; корреляция группы с итогом завышена (часть целого), поэтому она сравнивается с остальными группами.");
    }

    // ---------- аномалии ----------

    private static void Anomalies(Ctx c)
    {
        var all = new List<(string Op, JsonElement A)>();
        var structural = new List<(string Op, JsonElement S)>();
        var checkedMetrics = 0;
        foreach (var id in new[] { "c_sales", "c_visits", "c_payments" })
        {
            if (c.C(id) is not { } r)
            {
                continue;
            }

            checkedMetrics += A(r, "metrics").Count();
            all.AddRange(A(r, "anomalies").Select(a => (id, a)));
            structural.AddRange(A(r, "structural").Select(s => (id, s)));
        }

        var ordered = all.OrderBy(a => S(a.A, "severity") == "critical" ? 0 : 1).ThenByDescending(a => S(a.A, "date")).ToList();
        c.Add("summary", ordered.Count > 0 ? EvidenceLevels.Statistical : EvidenceLevels.Fact,
            ordered.Count > 0
                ? $"Проверено показателей: {checkedMetrics}. Подтверждённых аномалий: {ordered.Count} (критичных {ordered.Count(a => S(a.A, "severity") == "critical")}); сдвигов уровня: {structural.Count}."
                : $"Проверено показателей: {checkedMetrics} — подтверждённых аномалий нет (с учётом дня недели).", all.Select(a => a.Op).Distinct().DefaultIfEmpty("c_sales").ToArray());
        foreach (var (op, a) in ordered.Take(10))
        {
            c.Value("patterns", EvidenceLevels.Statistical,
                $"{S(a, "date")} — «{S(a, "title")}»: {Num(D(a, "value"))} при обычных {Num(D(a, "expected"))} ({Signed(D(a, "deviationPct"))}); подтвердили: {string.Join(", ", A(a, "methods").Select(m => Method(m.GetString())))}.",
                D(a, "deviationPct"), "%", op);
        }

        foreach (var (op, s) in structural.Take(5))
        {
            c.Add("patterns", EvidenceLevels.Statistical, $"«{S(s, "title")}»: уровень сместился с {S(s, "date")} — {Num(D(s, "meanBefore"))} → {Num(D(s, "meanAfter"))} ({Signed(D(s, "shiftPct"))}, p = {Pv(D(s, "p"))}).", op);
        }

        c.Tables.Add(new ResearchTable("anomalies", "Аномалии",
            [new("date", "Дата"), new("metric", "Показатель"), new("value", "Значение"), new("expected", "Обычно"), new("pct", "Отклонение", "%"), new("severity", "Серьёзность"), new("methods", "Методы")],
            ordered.Select(x => (IReadOnlyList<object?>)[S(x.A, "date"), S(x.A, "title"), D(x.A, "value"), D(x.A, "expected"), D(x.A, "deviationPct"), S(x.A, "severity"),
                string.Join(", ", A(x.A, "methods").Select(m => Method(m.GetString())))]).ToList()));
        if (ordered.Any(a => A(a.A, "methods").Any(m => m.GetString() == "missing_data")))
        {
            c.Add("risks", EvidenceLevels.Statistical, "Есть дни с нулём там, где обычно есть данные: проверьте загрузку Linko за эти дни, прежде чем делать выводы о продажах.",
                ordered.Where(a => A(a.A, "methods").Any(m => m.GetString() == "missing_data")).Select(a => a.Op).Distinct().ToArray());
        }
    }

    private static string Method(string? code) => code switch
    {
        "z" => "z-оценка",
        "modified_z" => "модифицированная z",
        "iqr" => "IQR",
        "seasonal" => "сезонный остаток",
        "missing_data" => "пропали данные",
        "sharp_rise" => "резкий рост",
        "sharp_drop" => "резкое падение",
        _ => code ?? "—",
    };

    // ---------- акции ----------

    private static void Promo(Ctx c)
    {
        if (c.Data.Promo is { Count: > 0 } effects)
        {
            // Страница «Акции» считает акции месяца конца периода; проверка ниже (разность разностей) — акции всего периода. Месяц и даты в
            // тексте — чтобы одноимённые акции разных месяцев не выглядели как одна с разными цифрами.
            var month = new DateOnly(c.Plan.To.Year, c.Plan.To.Month, 1).ToString("MMMM yyyy", Ru);
            foreach (var e in effects.OrderByDescending(e => e.Discount).Take(6))
            {
                var growth = e.Growth is { } g ? Signed((double)g * 100) : "—";
                var control = e.ControlGrowth is { } cg ? Signed((double)cg * 100) : "—";
                c.Add("metrics", EvidenceLevels.Calculated,
                    $"«{e.Name}» (страница «Акции», {month}): кг в день {Num((double?)e.BaseKgDay)} → {Num((double?)e.CurKgDay)} ({growth}; контрольная группа {control}), " +
                    $"доп. {Num((double?)e.ExtraKg)} кг, скидки {Num((double)e.Discount)} сум.", "p_page");
                if (e.ReturnPerSum is { } rps)
                {
                    // Отдача страницы — разница с контрольной группой без проверки значимости: ориентир, не доказанный эффект акции.
                    c.Add("causes", EvidenceLevels.Hypothesis,
                        $"«{e.Name}» ({month}): отдача по расчёту страницы «Акции» — {N2((double)rps)} сум выручки на 1 сум скидки; эффект статистически не проверен — " +
                        "это ориентир, а не доказанный результат акции.", "p_page");
                }
            }

            c.Tables.Add(new ResearchTable("promo_page", "Эффект акций (как на странице «Акции»)",
                [new("name", "Акция"), new("status", "Статус"), new("base", "Кг/день до"), new("cur", "Кг/день сейчас"), new("growth", "Рост", "%"), new("control", "Контроль", "%"),
                 new("extra", "Доп. кг"), new("discount", "Скидки", "сум")],
                effects.Select(e => (IReadOnlyList<object?>)[e.Name, e.Status, e.BaseKgDay, e.CurKgDay, e.Growth * 100, e.ControlGrowth * 100, e.ExtraKg, e.Discount]).ToList()));
        }

        var rows = new List<IReadOnlyList<object?>>();
        // Проверки с одним окном и одинаковыми продажами и скидками товаров акции — это одни и те же проданные товары (акции различаются только
        // товарами, которых не продавали): один вывод под общим названием, а не два почти одинаковых с одной и той же суммой скидок.
        string SameSales(ResearchOperation op) => c.Q("q_disc_" + op.Id[6..]) is { } q && c.C(op.Id) is not null
            ? string.Join("|", op.Compute?.Data.From, op.Compute?.Data.To, q.Totals.GetValueOrDefault("discount"), q.Totals.GetValueOrDefault("revenue"), q.Totals.GetValueOrDefault("kg"))
            : op.Id;
        foreach (var twins in c.Plan.Operations.Where(o => o.Id.StartsWith("c_did_", StringComparison.Ordinal)).GroupBy(SameSales))
        {
            var op = twins.First();
            var ids = twins.Select(o => o.Id).ToArray();
            var name = ResearchPlanner.MergedName(twins.Select(o => o.Title.Replace("Разность разностей: ", "")).ToList());
            if (op.Compute?.Params?.TryGetValue("change_date", out var since) == true && DateOnly.TryParse(since.GetString(), CultureInfo.InvariantCulture, out var sinceDay)
                && op.Compute.Data.To is { } tillDay)
            {
                name += $" ({sinceDay:dd.MM} — {tillDay:dd.MM.yyyy})";
            }

            var disc = c.Q("q_disc_" + op.Id[6..]);
            var discount = disc is null ? null : (double?)disc.Totals.GetValueOrDefault("discount");
            var revenue = disc is null ? null : (double?)disc.Totals.GetValueOrDefault("revenue");
            var kg = disc is null ? null : (double?)disc.Totals.GetValueOrDefault("kg");
            if (c.C(op.Id) is not { } did || O(did, "result") is not { } r)
            {
                if (discount is not null)
                {
                    c.Add("metrics", EvidenceLevels.Fact, $"{name}: скидки на товары акции {Num(discount)} сум, выручка товаров {Num(revenue)} сум; эффект не проверен — {op.ErrorReason ?? "нет данных"}.", "q_disc_" + op.Id[6..]);
                }

                continue;
            }

            var effect = D(r, "effect") ?? 0;
            var p = D(r, "effectP") ?? 1;
            var preP = D(r, "preTrendP");
            var cls = S(r, "evidenceClass");
            var change = op.Compute?.Params?.TryGetValue("change_date", out var cd) == true && DateOnly.TryParse(cd.GetString(), CultureInfo.InvariantCulture, out var chd)
                ? chd : (DateOnly?)null;
            var days = op.Compute?.Data.To is { } end && change is { } ch ? end.DayNumber - ch.DayNumber + 1 : 0;
            var extraKg = effect * days;
            var price = kg is > 0 && revenue is { } rv ? rv / kg.Value : (double?)null;
            var extraRevenue = price is { } pr ? extraKg * pr : (double?)null;
            // Отдачу считаем только для эффекта, отличного от нуля: у незначимого эффекта «отдача» — шум, а не результат акции.
            var significant = p < EvidenceClasses.Alpha;
            var roi = significant && discount is > 0 && extraRevenue is { } er ? er / discount.Value : (double?)null;
            if (!significant)
            {
                extraRevenue = null;
            }

            var level = cls == EvidenceClasses.TestedCausal && significant ? EvidenceLevels.Statistical
                : significant ? EvidenceLevels.Correlation : EvidenceLevels.Hypothesis;
            c.Add("causes", level,
                $"{name}: эффект {Signed(D(r, "effectPct"))} к контрольной группе ({N1(effect)} кг в день, p = {Pv(p)}" +
                (preP is { } pp ? $", тренды до акции {(pp >= EvidenceClasses.Alpha ? "параллельны" : "расходились — сравнение ненадёжно")}" : "") + ")" +
                (discount is { } dsc ? $"; скидки на товары акции в эти дни {Num(dsc)} сум" : "") + (roi is { } ro ? $"; доп. выручка ≈ {Num(extraRevenue)} сум, отдача {N2(ro)} сум на 1 сум скидки" : "") +
                (level == EvidenceLevels.Statistical ? " — эффект подтверждён."
                    : significant ? " — изменение значимо, но тренды до акции расходились: причинный вывод ненадёжен."
                    : " — эффект статистически не отличим от нуля, отдача не считается."), ids);
            rows.Add([name, D(r, "effectPct"), p, preP, discount, extraRevenue, roi, EvidenceLevels.Title(level)]);
        }

        if (rows.Count > 0)
        {
            c.Tables.Add(new ResearchTable("promo_did", "Проверка эффекта (разность разностей)",
                [new("name", "Акция"), new("effect", "Эффект", "%"), new("p", "p"), new("pre", "p трендов до"), new("discount", "Скидки", "сум"), new("extra", "Доп. выручка", "сум"),
                 new("roi", "Отдача на 1 сум скидки"), new("level", "Уровень")], rows));
        }

        c.Add("summary", rows.Count + (c.Data.Promo?.Count ?? 0) > 0 ? EvidenceLevels.Calculated : EvidenceLevels.Unverified,
            rows.Count + (c.Data.Promo?.Count ?? 0) > 0
                ? $"Акций оценено: {Math.Max(rows.Count, c.Data.Promo?.Count ?? 0)}; статистически подтверждён эффект у {c.Findings.Count(f => f.Section == "causes" && f.Level == EvidenceLevels.Statistical)}."
                : "Акций с товарами и базой для сравнения в периоде нет.");
        c.Limitations.Add("Контрольная группа — остальные товары; на продажи товаров акции в те же дни могли влиять и другие события.");
    }

    // ---------- состояние компании ----------

    private static void Health(Ctx c)
    {
        var risks = new List<(int Rank, string Text, string Level, string Op)>();
        if (c.Q("q_kpi") is { } k)
        {
            double? Cur(string m) => (double?)k.Totals.GetValueOrDefault(m);
            double? Prev(string m) => (double?)k.CompareTotals?.GetValueOrDefault(m);
            double? Pct(string m) => Cur(m) is { } a && Prev(m) is { } b && b != 0 ? (a - b) / Math.Abs(b) * 100 : null;
            var rows = new List<IReadOnlyList<object?>>();
            foreach (var m in new[] { "revenue", "kg", "orders", "outlets", "avg_check", "returns_revenue" })
            {
                var def = SemanticCatalog.Metric(m)!;
                rows.Add([def.Title, Prev(m), Cur(m), Pct(m)]);
                c.Value("metrics", EvidenceLevels.Fact, $"{def.Title}: {Num(Prev(m))} → {Num(Cur(m))} {def.Unit} ({Signed(Pct(m))}).", Pct(m), "%", "q_kpi");
            }

            c.Tables.Add(new ResearchTable("kpi", "Ключевые показатели к прошлому периоду", [new("metric", "Показатель"), new("prev", "Было"), new("cur", "Стало"), new("pct", "Изменение", "%")], rows));
            c.Add("summary", EvidenceLevels.Fact,
                $"За {c.Plan.From:dd.MM} — {c.Plan.To:dd.MM}: выручка {Signed(Pct("revenue"))}, АКБ {Signed(Pct("outlets"))}, заказы {Signed(Pct("orders"))}, средний чек {Signed(Pct("avg_check"))} к предыдущему такому же периоду.",
                "q_kpi");
            if (Pct("revenue") is < -5)
            {
                risks.Add((1, $"Выручка снизилась на {N1(-Pct("revenue")!.Value)} % к прошлому периоду.", EvidenceLevels.Fact, "q_kpi"));
            }

            if (Pct("outlets") is < -5)
            {
                risks.Add((2, $"АКБ сократилась на {N1(-Pct("outlets")!.Value)} %.", EvidenceLevels.Fact, "q_kpi"));
            }

            if (Pct("returns_revenue") is > 20 && Cur("returns_revenue") > Cur("revenue") * 0.02)
            {
                risks.Add((3, $"Возвраты выросли на {N1(Pct("returns_revenue"))} %.", EvidenceLevels.Fact, "q_kpi"));
            }
        }

        if (c.Q("q_visits") is { } v)
        {
            double? Cur(string m) => (double?)v.Totals.GetValueOrDefault(m);
            double? Prev(string m) => (double?)v.CompareTotals?.GetValueOrDefault(m);
            c.Add("metrics", EvidenceLevels.Fact, $"Визиты: {Num(Prev("visits_done"))} → {Num(Cur("visits_done"))}; страйк {N1(Prev("visit_strike"))} → {N1(Cur("visit_strike"))} %; маршрут {N1(Prev("route_execution"))} → {N1(Cur("route_execution"))} %.", "q_visits");
            if (Cur("route_execution") is < 70)
            {
                risks.Add((4, $"Выполнение маршрута {N1(Cur("route_execution"))} % — ниже 70 %.", EvidenceLevels.Fact, "q_visits"));
            }
        }

        if (c.Q("q_pay") is { } pay)
        {
            var p1 = (double?)pay.Totals.GetValueOrDefault("payments");
            var r1 = (double?)pay.Totals.GetValueOrDefault("revenue");
            var p0 = (double?)pay.CompareTotals?.GetValueOrDefault("payments");
            var r0 = (double?)pay.CompareTotals?.GetValueOrDefault("revenue");
            var ratio = p1 is { } a && r1 is > 0 ? a / r1.Value * 100 : (double?)null;
            var ratio0 = p0 is { } b && r0 is > 0 ? b / r0.Value * 100 : (double?)null;
            c.Add("metrics", EvidenceLevels.Calculated, $"Оплаты точек — {N1(ratio)} % выручки периода (было {N1(ratio0)} %).", "q_pay");
            if (ratio < 85 && ratio < ratio0 - 5)
            {
                risks.Add((3, $"Поступления отстают от продаж: оплаты {N1(ratio)} % выручки против {N1(ratio0)} % в прошлом периоде.", EvidenceLevels.Calculated, "q_pay"));
            }
        }

        if (c.Q("q_plan") is { } plan)
        {
            var col = plan.Columns.Select((x, i) => (x.Code, i)).ToDictionary(x => x.Code, x => x.i);
            double? V(object?[] row, string m) => col.TryGetValue(m, out var i) && row[i] is { } x ? Convert.ToDouble(x, CultureInfo.InvariantCulture) : null;
            var regionCol = col.GetValueOrDefault("region");
            var behind = plan.Rows.Where(r => V(r, "forecast_execution") is < 90).OrderBy(r => V(r, "forecast_execution")).Take(5).ToList();
            var totalForecast = plan.Totals.GetValueOrDefault("forecast_execution");
            c.Add("metrics", EvidenceLevels.Forecast, $"План месяца: прогноз выполнения по темпу {N1((double?)totalForecast)} %" +
                                                     (behind.Count > 0 ? $"; ниже 90 %: {string.Join(", ", behind.Select(r => $"{r[regionCol]} {N1(V(r, "forecast_execution"))} %"))}." : "."), "q_plan");
            if (totalForecast is < 95)
            {
                risks.Add((1, $"План месяца под угрозой: прогноз по темпу {N1((double?)totalForecast)} %.", EvidenceLevels.Forecast, "q_plan"));
            }
        }

        if (c.C("c_trend") is { } trend && O(trend, "trend") is { } tr && D(tr, "p") is { } tp)
        {
            var slopePct = D(tr, "slopePctOfMean");
            c.Add("patterns", tp < EvidenceClasses.Alpha ? EvidenceLevels.Statistical : EvidenceLevels.Calculated,
                $"Тренд выручки за 90 дней: {Signed(slopePct)} от среднего в день (p = {Pv(tp)}){(tp < EvidenceClasses.Alpha ? "" : " — не значим")}.", "c_trend");
            if (tp < EvidenceClasses.Alpha && slopePct < 0)
            {
                risks.Add((2, $"Выручка устойчиво снижается: {Signed(slopePct)} в день от среднего (p = {Pv(tp)}).", EvidenceLevels.Statistical, "c_trend"));
            }
        }

        if (c.C("c_anom") is { } anom)
        {
            var list = A(anom, "anomalies").ToList();
            foreach (var a in list.Where(a => S(a, "severity") == "critical").Take(3))
            {
                risks.Add((3, $"Аномалия {S(a, "date")}: «{S(a, "title")}» {Num(D(a, "value"))} при обычных {Num(D(a, "expected"))}.", EvidenceLevels.Statistical, "c_anom"));
            }
        }

        if (c.C("c_churn") is { } churn && D(churn, "revenueAtRiskShare") is { } share)
        {
            c.Add("metrics", EvidenceLevels.Calculated, $"Отток: не купили {D(churn, "lost"):0} из {D(churn, "baseCustomers"):0} точек прошлого периода; их доля выручки {N1(share * 100)} %.", "c_churn");
            if (share >= 0.1)
            {
                risks.Add((2, $"Отток точек: под риском {N1(share * 100)} % выручки прошлого периода.", EvidenceLevels.Calculated, "c_churn"));
            }
        }

        if (c.Q("q_stock") is { } stock)
        {
            var col = stock.Columns.Select((x, i) => (x.Code, i)).ToDictionary(x => x.Code, x => x.i);
            var low = stock.Rows.Where(r => r[col["cover_days"]] is { } d && Convert.ToDouble(d, CultureInfo.InvariantCulture) < 7).ToList();
            if (low.Count > 0)
            {
                risks.Add((3, $"Запаса меньше чем на неделю у {low.Count} товаров (например, {string.Join(", ", low.Take(3).Select(r => r[0]))}).", EvidenceLevels.Calculated, "q_stock"));
            }
        }

        foreach (var r in c.Data.Risks ?? [])
        {
            risks.Add((r.Severity == "critical" ? 1 : 3, $"{r.Title}{(r.Impact is { } i ? $" (масштаб {Num((double)i)} {r.ImpactUnit})" : "")}.", EvidenceLevels.Fact, "p_risks"));
        }

        foreach (var r in risks.OrderBy(r => r.Rank).Take(10))
        {
            c.Add("risks", r.Level, r.Text, r.Op);
        }

        if (risks.Count == 0)
        {
            c.Add("risks", EvidenceLevels.Calculated, "Существенных рисков по доступным показателям не найдено.");
        }

        c.Add("recommendations", EvidenceLevels.Hypothesis, risks.Count > 0
            ? "Начать с первых рисков списка: по каждому — исследование причин (регионы, группы, ТП) и задачи ответственным."
            : "Держать текущий темп; следить за планом месяца и оттоком точек.");
    }

    // ---------- JSON и числа ----------

    private static double? D(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string? S(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Null ? null : v.ToString() : null;

    private static IEnumerable<JsonElement> A(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    private static JsonElement? O(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static string Title(string? metric) => metric is null ? "—" : SemanticCatalog.Metric(metric)?.Title ?? metric;

    private static string Num(double? v) => v is not { } x ? "—" : Math.Abs(x) >= 100 ? x.ToString("N0", Ru) : x.ToString("N1", Ru);

    private static string N1(double? v) => v is { } x ? x.ToString("N1", Ru) : "—";

    private static string N2(double? v) => v is { } x ? x.ToString("0.00", Ru) : "—";

    private static string Pv(double? p) => p is not { } x ? "—" : x < 0.001 ? "< 0,001" : x.ToString("0.000", Ru);

    private static string Signed(double? v) => v is not { } x ? "—" : (x >= 0 ? "+" : "") + x.ToString("N1", Ru) + " %";

    private static string SignedPp(double? v) => v is not { } x ? "—" : (x >= 0 ? "+" : "") + x.ToString("N1", Ru) + " п.п.";

    private static string Ci(JsonElement p) => O(p, "ci") is { } ci && D(ci, "lower") is { } lo && D(ci, "upper") is { } hi ? $" [{N2(lo)}; {N2(hi)}]" : "";
}

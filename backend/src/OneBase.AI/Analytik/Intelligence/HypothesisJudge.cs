using System.Globalization;
using System.Text.Json;
using OneBase.Application.Analytik.Compute;

namespace OneBase.AI.Analytik.Intelligence;

/// <summary>
/// Multi-Hypothesis Engine: статус гипотезы по результату расчёта — правилами кода, без модели. Корреляция и разложение изменения не
/// превращаются в причину: класс вывода берётся из расчёта (наблюдаемая, статистическая, проверенная причинная); «подтверждена» с классом
/// «наблюдаемая» значит «данные согласуются», а не «доказана причина».
/// </summary>
public static class HypothesisJudge
{
    /// <summary>Доля участника в изменении, с которой вклад — главный (подтверждена) и заметный (частично).</summary>
    public const double MainShare = 0.5, NoticeableShare = 0.2;

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static HypothesisVerdict Evaluate(PlannedHypothesis h, ComputeOutcome? o)
    {
        if (h.Test is null)
        {
            return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Assumption, [], [], ["Проверка расчётом не задана — гипотеза остаётся предположением."], null, null, o);
        }

        if (o is null || o.Status != "ok" || o.Result is null)
        {
            return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Assumption, [], [],
                [o?.Error ?? "Расчёт не выполнен.", .. (o?.Limitations ?? []).Take(2)], null, null, o);
        }

        var r = JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions);
        return o.Method switch
        {
            "drivers" => Drivers(h, o, r),
            "pvm" => Pvm(h, o, r),
            "before_after" => Change(h, o, r.GetProperty("result").GetProperty("test").GetProperty("diff").GetDouble(),
                r.GetProperty("result").GetProperty("test").GetProperty("p").GetDouble(), Pct(r.GetProperty("result").GetProperty("test"), "diffPct")),
            "did" => Change(h, o, r.GetProperty("result").GetProperty("effect").GetDouble(), r.GetProperty("result").GetProperty("effectP").GetDouble(),
                Pct(r.GetProperty("result"), "effectPct")),
            "trend" => Change(h, o, r.GetProperty("trend").GetProperty("slope").GetDouble(), r.GetProperty("trend").GetProperty("p").GetDouble(),
                Pct(r.GetProperty("trend"), "slopePctOfMean")),
            "correlation" => Correlation(h, o, r),
            "regression" => Regression(h, o, r),
            "lag" => Lag(h, o, r),
            "churn" => Churn(h, o, r),
            "mix" => Mix(h, o, r),
            "plan_variance" => PlanVariance(h, o, r),
            "anomalies" => Anomalies(h, o, r),
            _ => Verdict(h, HypothesisStatuses.Inconclusive, o.EvidenceClass ?? EvidenceClasses.Observed, o.Summary is { } s ? [s] : [], [],
                ["Метод описывает данные и не проверяет гипотезу — нужен drivers, pvm, before_after, did, correlation, regression или lag."], null, null, o),
        };
    }

    private static double? Pct(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static int Sign(string? direction) => direction?.Trim().ToLowerInvariant() switch
    {
        "down" or "decrease" or "negative" or "снижение" or "падение" => -1,
        "up" or "increase" or "positive" or "рост" => 1,
        _ => 0,
    };

    private static string N(double v, int digits = 1) => v.ToString("N" + digits, Ru);

    private static string P(double p) => p < 0.001 ? "< 0,001" : p.ToString("0.000", Ru);

    /// <summary>Вклад участника: доля в сумме |изменений| ≥ 0,5 — главный вклад, ≥ 0,2 — заметный; изменение в другую сторону — отклонена.</summary>
    private static HypothesisVerdict Drivers(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var dir = Sign(h.Expect.Direction);
        var delta = r.GetProperty("delta").GetDouble();
        var items = r.GetProperty("items").EnumerateArray().ToList();
        if (h.Expect.Member is not { Length: > 0 } member)
        {
            var ok = dir == 0 || Math.Sign(delta) == dir;
            return Verdict(h, ok ? HypothesisStatuses.Confirmed : HypothesisStatuses.Rejected, EvidenceClasses.Observed,
                ok ? [$"Итог изменился на {N(delta)}."] : [], ok ? [] : [$"Итог изменился в другую сторону: {N(delta)}."], [], delta, null, o);
        }

        var item = items.FirstOrDefault(i => Matches(i.GetProperty("key").GetString(), member));
        if (item.ValueKind == JsonValueKind.Undefined)
        {
            return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Observed, [], [], [$"«{member}» не найден среди участников среза."], null, null, o);
        }

        var d = item.GetProperty("delta").GetDouble();
        var share = item.GetProperty("shareOfGross").GetDouble();
        var key = item.GetProperty("key").GetString();
        if (dir != 0 && Math.Sign(d) != dir)
        {
            return Verdict(h, HypothesisStatuses.Rejected, EvidenceClasses.Observed, [], [$"«{key}» изменился в другую сторону: {N(d)}."], [], d, null, o);
        }

        var min = h.Expect.MinShare ?? MainShare;
        var status = share >= min ? HypothesisStatuses.Confirmed : share >= NoticeableShare ? HypothesisStatuses.PartiallyConfirmed : HypothesisStatuses.Rejected;
        var text = $"«{key}»: изменение {N(d)}, доля во всех изменениях {N(share * 100)} %.";
        return Verdict(h, status, EvidenceClasses.Observed, status == HypothesisStatuses.Rejected ? [] : [text], status == HypothesisStatuses.Rejected ? [text + " Вклад мал."] : [],
            ["Разложение показывает вклад, но не причину изменения участника."], d, null, o);
    }

    private static HypothesisVerdict Pvm(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var pvm = r.GetProperty("pvm");
        var effects = new Dictionary<string, double>
        {
            ["price"] = pvm.GetProperty("price").GetDouble(), ["volume"] = pvm.GetProperty("volume").GetDouble(), ["mix"] = pvm.GetProperty("mix").GetDouble(),
            ["new"] = pvm.GetProperty("newItems").GetDouble(), ["lost"] = pvm.GetProperty("lostItems").GetDouble(),
        };
        var delta = pvm.GetProperty("delta").GetDouble();
        var effect = h.Expect.Effect?.Trim().ToLowerInvariant() switch
        {
            "цена" => "price", "объём" or "объем" => "volume", "ассортимент" => "mix", { } e when effects.ContainsKey(e) => e, _ => null,
        };
        if (effect is null)
        {
            return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Observed, [o.Summary ?? ""], [], ["Не указан эффект PVM: price, volume, mix, new или lost."], null, null, o);
        }

        var v = effects[effect];
        var gross = effects.Values.Sum(Math.Abs);
        var share = gross > 0 ? Math.Abs(v) / gross : 0;
        var dir = Sign(h.Expect.Direction) is var sd and not 0 ? sd : Math.Sign(delta);
        var text = $"Эффект «{effect}» {N(v, 0)} из изменения выручки {N(delta, 0)} (доля во всех эффектах {N(share * 100)} %).";
        if (Math.Sign(v) != dir && v != 0)
        {
            return Verdict(h, HypothesisStatuses.Rejected, EvidenceClasses.Observed, [], [text + " Эффект действует в другую сторону."], [], v, "сум", o);
        }

        var status = share >= (h.Expect.MinShare ?? MainShare) ? HypothesisStatuses.Confirmed : share >= NoticeableShare ? HypothesisStatuses.PartiallyConfirmed : HypothesisStatuses.Rejected;
        return Verdict(h, status, EvidenceClasses.Observed, status == HypothesisStatuses.Rejected ? [] : [text], status == HypothesisStatuses.Rejected ? [text] : [], [], v, "сум", o);
    }

    /// <summary>
    /// Изменение со значимостью (до/после, разность разностей, тренд): значимо и в ожидаемую сторону — подтверждена (класс расчёта); в ожидаемую,
    /// но незначимо — не доказана; значимо в другую сторону — отклонена; незначимо и меньше 2 % — отклонена (заметного изменения нет).
    /// </summary>
    private static HypothesisVerdict Change(PlannedHypothesis h, ComputeOutcome o, double effect, double p, double? pct)
    {
        var dir = Sign(h.Expect.Direction);
        var significant = p < EvidenceClasses.Alpha;
        var sameSide = dir == 0 || Math.Sign(effect) == dir;
        var text = $"{o.Title}: {N(effect)}{(pct is { } x ? $" ({(x >= 0 ? "+" : "")}{N(x)} %)" : "")}, p = {P(p)}.";
        if (significant && sameSide)
        {
            // Разность разностей без проверенных параллельных трендов — только частично (статистическая связь, причина не проверена).
            var status = o.Method == "did" && o.EvidenceClass != EvidenceClasses.TestedCausal ? HypothesisStatuses.PartiallyConfirmed : HypothesisStatuses.Confirmed;
            return Verdict(h, status, o.EvidenceClass ?? EvidenceClasses.Statistical, [text], [], o.EvidenceClass == EvidenceClasses.TestedCausal
                ? []
                : ["Без контрольной группы с параллельными трендами причинность не доказана."], effect, null, o);
        }

        if (significant)
        {
            return Verdict(h, HypothesisStatuses.Rejected, o.EvidenceClass ?? EvidenceClasses.Statistical, [], [text + " Изменение в другую сторону."], [], effect, null, o);
        }

        if (pct is { } small && Math.Abs(small) < 2)
        {
            return Verdict(h, HypothesisStatuses.Rejected, EvidenceClasses.Observed, [], [text + " Заметного изменения нет."], [], effect, null, o);
        }

        return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Observed, sameSide ? [text] : [], sameSide ? [] : [text],
            ["Изменение статистически незначимо: нужен более длинный период или контрольная группа."], effect, null, o);
    }

    private static HypothesisVerdict Correlation(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var pair = r.GetProperty("pairs").EnumerateArray().FirstOrDefault();
        if (pair.ValueKind == JsonValueKind.Undefined)
        {
            return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Observed, [], [], ["Нет пары показателей для корреляции."], null, null, o);
        }

        var pearson = pair.GetProperty("pearson");
        return Related(h, o, pearson.GetProperty("r").GetDouble(), pearson.GetProperty("p").GetDouble(), pearson.GetProperty("n").GetInt32(), "r");
    }

    private static HypothesisVerdict Regression(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var terms = r.GetProperty("regression").GetProperty("terms").EnumerateArray().Skip(1).ToList();
        if (terms.Count == 0)
        {
            return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Observed, [], [], ["Нет признаков регрессии."], null, null, o);
        }

        var t = terms[0];
        return Related(h, o, t.GetProperty("coefficient").GetDouble(), t.GetProperty("p").GetDouble(), r.GetProperty("regression").GetProperty("n").GetInt32(), "коэффициент");
    }

    private static HypothesisVerdict Lag(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var lag = r.GetProperty("lag");
        if (!lag.TryGetProperty("best", out var best) || best.ValueKind != JsonValueKind.Object)
        {
            return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Observed, [], [], ["Связь со сдвигом не найдена."], null, null, o);
        }

        return Related(h, o, best.GetProperty("r").GetDouble(), lag.GetProperty("bestAdjustedP").GetDouble(), best.GetProperty("n").GetInt32(),
            $"r при сдвиге {best.GetProperty("lag").GetInt32()}");
    }

    /// <summary>Связь показателей: значима и нужного знака — подтверждена как статистическая связь (не причина).</summary>
    private static HypothesisVerdict Related(PlannedHypothesis h, ComputeOutcome o, double value, double p, int n, string label)
    {
        var dir = Sign(h.Expect.Direction);
        var text = $"{label} = {N(value, 2)}, p = {P(p)}, n = {n}.";
        var significant = p < EvidenceClasses.Alpha && n >= 8;
        if (significant && (dir == 0 || Math.Sign(value) == dir))
        {
            return Verdict(h, HypothesisStatuses.Confirmed, EvidenceClasses.Statistical, [text], [], ["Связь статистическая: причина не доказана."], value, null, o);
        }

        return significant
            ? Verdict(h, HypothesisStatuses.Rejected, EvidenceClasses.Statistical, [], [text + " Связь обратная."], [], value, null, o)
            : Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Observed, [], [text], ["Связь незначима на этих данных."], value, null, o);
    }

    private static HypothesisVerdict Churn(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var share = r.GetProperty("lostShare").GetDouble();
        var atRisk = r.GetProperty("revenueAtRisk").GetDouble();
        var min = h.Expect.MinShare ?? 0.1;
        var text = $"Не купили {r.GetProperty("lost").GetInt32()} из {r.GetProperty("baseCustomers").GetInt32()} ({N(share * 100)} %), выручка под риском {N(atRisk, 0)}.";
        return share >= min
            ? Verdict(h, HypothesisStatuses.Confirmed, EvidenceClasses.Observed, [text], [], [], atRisk, "сум", o)
            : Verdict(h, share > 0 ? HypothesisStatuses.PartiallyConfirmed : HypothesisStatuses.Rejected, EvidenceClasses.Observed, [], [text], [], atRisk, "сум", o);
    }

    private static HypothesisVerdict Mix(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var item = r.GetProperty("items").EnumerateArray().FirstOrDefault(i => h.Expect.Member is { } m && Matches(i.GetProperty("key").GetString(), m));
        if (item.ValueKind == JsonValueKind.Undefined)
        {
            return Verdict(h, HypothesisStatuses.Inconclusive, EvidenceClasses.Observed, [], [], ["Участник структуры не указан или не найден."], null, null, o);
        }

        var shift = item.GetProperty("shiftPp").GetDouble();
        var dir = Sign(h.Expect.Direction);
        var text = $"Доля «{item.GetProperty("key").GetString()}» изменилась на {N(shift)} п.п.";
        var ok = Math.Abs(shift) >= 1 && (dir == 0 || Math.Sign(shift) == dir);
        return Verdict(h, ok ? HypothesisStatuses.Confirmed : HypothesisStatuses.Rejected, EvidenceClasses.Observed, ok ? [text] : [], ok ? [] : [text], [], shift, "п.п.", o);
    }

    private static HypothesisVerdict PlanVariance(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var items = r.GetProperty("items").EnumerateArray().ToList();
        var item = h.Expect.Member is { } m ? items.FirstOrDefault(i => Matches(i.GetProperty("key").GetString(), m)) : default;
        var deviation = item.ValueKind == JsonValueKind.Object ? item.GetProperty("deviation").GetDouble() : r.GetProperty("deviation").GetDouble();
        var dir = Sign(h.Expect.Direction) is var d and not 0 ? d : -1;
        var text = $"Отклонение от плана {N(deviation, 0)} кг.";
        return Math.Sign(deviation) == dir
            ? Verdict(h, HypothesisStatuses.Confirmed, EvidenceClasses.Observed, [text], [], [], deviation, "кг", o)
            : Verdict(h, HypothesisStatuses.Rejected, EvidenceClasses.Observed, [], [text], [], deviation, "кг", o);
    }

    private static HypothesisVerdict Anomalies(PlannedHypothesis h, ComputeOutcome o, JsonElement r)
    {
        var hits = r.GetProperty("anomalies").GetArrayLength();
        return hits > 0
            ? Verdict(h, HypothesisStatuses.Confirmed, EvidenceClasses.Observed, [o.Summary ?? ""], [], ["Аномалия — отклонение от обычного уровня, а не причина."], hits, "шт", o)
            : Verdict(h, HypothesisStatuses.Rejected, EvidenceClasses.Observed, [], [o.Summary ?? "Аномалий нет."], [], 0, "шт", o);
    }

    /// <summary>Участник совпадает: подпись содержит название (без регистра) или название — подпись.</summary>
    public static bool Matches(string? label, string member) =>
        label is not null && (label.Contains(member.Trim(), StringComparison.OrdinalIgnoreCase) || member.Contains(label.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Уверенность в статусе: проверенная причинная — 0,85; статистическая — 0,75 (p &lt; 0,01 — 0,8); наблюдаемая — по доле вклада;
    /// частично — 0,45; не доказана — 0,2; отклонена — 0,6–0,7.
    /// </summary>
    private static HypothesisVerdict Verdict(PlannedHypothesis h, string status, string? cls, IReadOnlyList<string> supporting, IReadOnlyList<string> refuting,
        IReadOnlyList<string> needed, double? impact, string? unit, ComputeOutcome? o)
    {
        var confidence = status switch
        {
            HypothesisStatuses.Confirmed => cls switch
            {
                EvidenceClasses.TestedCausal => 0.85,
                EvidenceClasses.Statistical => 0.75,
                _ => 0.6,
            },
            HypothesisStatuses.PartiallyConfirmed => 0.45,
            HypothesisStatuses.Rejected => cls == EvidenceClasses.Statistical ? 0.7 : 0.6,
            _ => 0.2,
        };
        return new HypothesisVerdict(h, status, cls, confidence, supporting.Where(s => s.Length > 0).ToList(), refuting.Where(s => s.Length > 0).ToList(), needed,
            impact, unit, o?.Id, o?.Summary);
    }
}

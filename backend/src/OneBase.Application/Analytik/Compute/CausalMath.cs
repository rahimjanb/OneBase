namespace OneBase.Application.Analytik.Compute;

/// <summary>
/// Класс вывода о связи (Module 3): наблюдаемая зависимость (описание данных без проверки), статистически подтверждённая связь (проверка
/// значимости пройдена), проверенная причинная гипотеза (есть контрольная группа и параллельные тренды до события), предположение (данными
/// не проверялось). Корреляция никогда не становится «причиной» без контрольной группы.
/// </summary>
public static class EvidenceClasses
{
    public const string Observed = "observed";
    public const string Statistical = "statistical";
    public const string TestedCausal = "tested_causal";
    public const string Assumption = "assumption";

    public static readonly string[] All = [Observed, Statistical, TestedCausal, Assumption];

    public static string Title(string? cls) => cls switch
    {
        Observed => "наблюдаемая зависимость",
        Statistical => "статистически подтверждённая связь",
        TestedCausal => "проверенная причинная гипотеза",
        Assumption => "предположение, требует проверки",
        _ => "—",
    };

    /// <summary>Сила класса: чем выше, тем сильнее доказательство.</summary>
    public static int Rank(string? cls) => cls switch
    {
        TestedCausal => 3,
        Statistical => 2,
        Observed => 1,
        _ => 0,
    };

    /// <summary>Порог значимости проверок.</summary>
    public const double Alpha = 0.05;
}

/// <summary>До / после: сравнение средних (Уэлч), тренд до события и эффект с поправкой на тренд, класс вывода.</summary>
public sealed record BeforeAfterResult(TTestResult Test, TrendResult? PreTrend, double? TrendAdjustedEffect, double? TrendAdjustedPct, string EvidenceClass,
    IReadOnlyList<string> Notes);

/// <summary>
/// Разность разностей: средние групп до и после, оценка эффекта (DiD), её p (регрессия с взаимодействием), проверка параллельных трендов до
/// события (p разницы наклонов), класс вывода.
/// </summary>
public sealed record DidResult(double TreatedBefore, double TreatedAfter, double ControlBefore, double ControlAfter, double Effect, double? EffectPct,
    double EffectP, double? PreTrendP, string EvidenceClass, IReadOnlyList<string> Notes);

/// <summary>Причинно-следственные методы: до/после, разность разностей, класс вывода по силе проверки.</summary>
public static class CausalMath
{
    /// <summary>Минимум наблюдений в каждой части для статистического вывода.</summary>
    public const int MinPoints = 7;

    public static BeforeAfterResult BeforeAfter(IReadOnlyList<double> before, IReadOnlyList<double> after)
    {
        var test = StatMath.Welch(before, after);
        var notes = new List<string>();
        TrendResult? trend = null;
        double? adjusted = null, adjustedPct = null;
        if (before.Count >= 8)
        {
            trend = SeriesMath.Trend(before);
            if (trend.P < EvidenceClasses.Alpha)
            {
                // Продолжение тренда «до» на период «после»: эффект — отклонение от этой линии, а не от среднего «до».
                var projected = Enumerable.Range(before.Count, after.Count).Select(t => trend.Intercept + trend.Slope * t).Average();
                adjusted = StatMath.Mean(after) - projected;
                adjustedPct = projected != 0 ? adjusted / Math.Abs(projected) * 100 : null;
                notes.Add("До события был значимый тренд — эффект считан и от продолжения тренда.");
            }
        }

        var significant = test.P < EvidenceClasses.Alpha && before.Count >= MinPoints && after.Count >= MinPoints;
        if (before.Count < MinPoints || after.Count < MinPoints)
        {
            notes.Add($"Мало точек (до {before.Count}, после {after.Count}; нужно хотя бы {MinPoints} в каждой части) — вывод только наблюдаемый.");
        }

        // Тренд объясняет разницу (эффект от продолжения тренда мал относительно разницы средних) — значимость средних не о событии.
        if (significant && adjusted is { } adj && Math.Abs(adj) < Math.Abs(test.Diff) / 2)
        {
            significant = false;
            notes.Add("Разницу средних в основном объясняет тренд до события.");
        }

        notes.Add("Без контрольной группы изменение нельзя приписать событию: в тот же период могли действовать другие факторы.");
        return new BeforeAfterResult(test, trend, adjusted, adjustedPct, significant ? EvidenceClasses.Statistical : EvidenceClasses.Observed, notes);
    }

    /// <summary>
    /// Разность разностей по рядам равной длины: treated и control, событие — с индекса changeIndex. Регрессия y = b0 + b1·группа + b2·после +
    /// b3·группа·после, эффект — b3. Параллельные тренды проверяются на периоде «до»: y = a + b·t + c·группа + d·группа·t, p(d) ≥ 0,05.
    /// </summary>
    public static DidResult DifferenceInDifferences(IReadOnlyList<double> treated, IReadOnlyList<double> control, int changeIndex)
    {
        StatMath.Require(treated.Count == control.Count, "Ряды группы и контроля разной длины.");
        StatMath.Require(changeIndex >= 3 && treated.Count - changeIndex >= 3, "Нужно хотя бы 3 точки до и 3 после события.");
        var y = new List<double>();
        var group = new List<double>();
        var post = new List<double>();
        var inter = new List<double>();
        for (var i = 0; i < treated.Count; i++)
        {
            var p = i >= changeIndex ? 1d : 0d;
            y.Add(treated[i]);
            group.Add(1);
            post.Add(p);
            inter.Add(p);
            y.Add(control[i]);
            group.Add(0);
            post.Add(p);
            inter.Add(0);
        }

        var reg = RegressionMath.Ols(y, [group, post, inter], ["группа", "после", "группа×после"]);
        var effect = reg.Terms[3];
        var tb = treated.Take(changeIndex).Average();
        var ta = treated.Skip(changeIndex).Average();
        var cb = control.Take(changeIndex).Average();
        var ca = control.Skip(changeIndex).Average();

        double? preP = null;
        var notes = new List<string>();
        if (changeIndex >= 4)
        {
            var py = new List<double>();
            var pt = new List<double>();
            var pg = new List<double>();
            var pgt = new List<double>();
            for (var i = 0; i < changeIndex; i++)
            {
                py.Add(treated[i]);
                pt.Add(i);
                pg.Add(1);
                pgt.Add(i);
                py.Add(control[i]);
                pt.Add(i);
                pg.Add(0);
                pgt.Add(0);
            }

            try
            {
                preP = RegressionMath.Ols(py, [pt, pg, pgt], ["t", "группа", "группа×t"]).Terms[3].P;
            }
            catch (ComputeException)
            {
                preP = null;
            }
        }

        var parallel = preP is null or >= EvidenceClasses.Alpha;
        if (preP is null)
        {
            notes.Add("Параллельность трендов до события не проверена (мало точек «до»).");
        }
        else if (!parallel)
        {
            notes.Add("Тренды группы и контроля до события расходились — оценка эффекта ненадёжна.");
        }

        var significant = effect.P < EvidenceClasses.Alpha;
        var cls = significant && parallel && preP is not null ? EvidenceClasses.TestedCausal : significant ? EvidenceClasses.Statistical : EvidenceClasses.Observed;
        notes.Add(cls == EvidenceClasses.TestedCausal
            ? "Эффект проверен против контрольной группы; остаётся риск события, затронувшего только группу."
            : "Причинность не подтверждена.");
        var expected = tb + (ca - cb);
        return new DidResult(tb, ta, cb, ca, effect.Coefficient, expected != 0 ? effect.Coefficient / Math.Abs(expected) * 100 : null, effect.P, preP, cls,
            notes);
    }

    /// <summary>Класс вывода по корреляции: значимая — статистическая связь (не причина), иначе — наблюдаемая.</summary>
    public static string ForCorrelation(double p, int n) => p < EvidenceClasses.Alpha && n >= 8 ? EvidenceClasses.Statistical : EvidenceClasses.Observed;
}

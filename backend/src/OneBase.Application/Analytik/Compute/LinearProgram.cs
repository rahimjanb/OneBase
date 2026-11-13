namespace OneBase.Application.Analytik.Compute;

/// <summary>Ограничение задачи: Σ a_j·x_j ≤ limit.</summary>
public sealed record LpConstraint(string Name, IReadOnlyList<double> Coefficients, double Limit);

/// <summary>Переменная: название, вклад в цель за единицу, минимум и максимум.</summary>
public sealed record LpVariable(string Name, double Objective, double Min = 0, double? Max = null);

/// <summary>Ограничение в решении: использовано, предел, запас, связывающее ли, теневая цена (рост цели на единицу предела).</summary>
public sealed record LpBinding(string Name, double Used, double Limit, double Slack, bool Binding, double ShadowPrice);

/// <summary>Решение: статус (optimal | infeasible | unbounded), значение цели, переменные, ограничения, итераций.</summary>
public sealed record LpResult(string Status, double Objective, IReadOnlyList<(string Name, double Value)> Values, IReadOnlyList<LpBinding> Constraints,
    int Iterations, string? Message);

/// <summary>
/// Линейная оптимизация (симплекс-метод, правило Бленда — без зацикливания): максимум Σ c_j·x_j при Σ a_ij·x_j ≤ b_i и min_j ≤ x_j ≤ max_j.
/// Нижние границы переносятся в правую часть (x = min + x'); если минимумы не помещаются в ограничения — «нет допустимого решения».
/// Для задач ассортимента и загрузки производства (десятки переменных и ограничений).
/// </summary>
public static class LinearProgram
{
    private const double Eps = 1e-9;

    public static LpResult Maximize(IReadOnlyList<LpVariable> variables, IReadOnlyList<LpConstraint> constraints, int maxIterations = 10_000)
    {
        var n = variables.Count;
        StatMath.Require(n > 0, "Нет переменных для оптимизации.");
        StatMath.Require(constraints.All(c => c.Coefficients.Count == n), "Число коэффициентов ограничения не совпадает с числом переменных.");
        StatMath.Require(variables.All(v => v.Max is null || v.Max >= v.Min), "Максимум переменной меньше минимума.");

        // Строки: ограничения пользователя и верхние границы (x' ≤ max − min).
        var rows = new List<(string Name, double[] A, double B, bool User)>();
        foreach (var c in constraints)
        {
            var shifted = c.Limit - Enumerable.Range(0, n).Sum(j => c.Coefficients[j] * variables[j].Min);
            if (shifted < -Eps)
            {
                return new LpResult("infeasible", 0, [], [], 0, $"Минимальные объёмы не помещаются в ограничение «{c.Name}».");
            }

            rows.Add((c.Name, c.Coefficients.ToArray(), Math.Max(0, shifted), true));
        }

        for (var j = 0; j < n; j++)
        {
            if (variables[j].Max is { } max)
            {
                var a = new double[n];
                a[j] = 1;
                rows.Add(($"max:{variables[j].Name}", a, max - variables[j].Min, false));
            }
        }

        var m = rows.Count;
        // Таблица: m строк × (n + m + 1); базис — дополнительные переменные.
        var t = new double[m + 1, n + m + 1];
        for (var i = 0; i < m; i++)
        {
            for (var j = 0; j < n; j++)
            {
                t[i, j] = rows[i].A[j];
            }

            t[i, n + i] = 1;
            t[i, n + m] = rows[i].B;
        }

        for (var j = 0; j < n; j++)
        {
            t[m, j] = -variables[j].Objective;
        }

        var basis = Enumerable.Range(n, m).ToArray();
        var iterations = 0;
        while (true)
        {
            if (++iterations > maxIterations)
            {
                return new LpResult("infeasible", 0, [], [], iterations, "Превышено число итераций.");
            }

            // Бленд: первая переменная с отрицательной оценкой.
            var enter = -1;
            for (var j = 0; j < n + m; j++)
            {
                if (t[m, j] < -Eps)
                {
                    enter = j;
                    break;
                }
            }

            if (enter < 0)
            {
                break;
            }

            var leave = -1;
            var best = double.MaxValue;
            for (var i = 0; i < m; i++)
            {
                if (t[i, enter] > Eps)
                {
                    var ratio = t[i, n + m] / t[i, enter];
                    if (ratio < best - Eps || (Math.Abs(ratio - best) <= Eps && leave >= 0 && basis[i] < basis[leave]))
                    {
                        best = ratio;
                        leave = i;
                    }
                }
            }

            if (leave < 0)
            {
                return new LpResult("unbounded", double.PositiveInfinity, [], [], iterations, "Цель растёт без предела — не хватает ограничений.");
            }

            var pivot = t[leave, enter];
            for (var j = 0; j <= n + m; j++)
            {
                t[leave, j] /= pivot;
            }

            for (var i = 0; i <= m; i++)
            {
                if (i == leave || Math.Abs(t[i, enter]) <= Eps)
                {
                    continue;
                }

                var f = t[i, enter];
                for (var j = 0; j <= n + m; j++)
                {
                    t[i, j] -= f * t[leave, j];
                }
            }

            basis[leave] = enter;
        }

        var x = new double[n];
        for (var i = 0; i < m; i++)
        {
            if (basis[i] < n)
            {
                x[basis[i]] = t[i, n + m];
            }
        }

        var values = Enumerable.Range(0, n).Select(j => (variables[j].Name, x[j] + variables[j].Min)).ToList();
        var objective = values.Select((v, j) => v.Item2 * variables[j].Objective).Sum();
        var bindings = new List<LpBinding>();
        for (var i = 0; i < m; i++)
        {
            if (!rows[i].User)
            {
                continue;
            }

            var c = constraints[i];
            var used = Enumerable.Range(0, n).Sum(j => c.Coefficients[j] * values[j].Item2);
            var slack = c.Limit - used;
            bindings.Add(new LpBinding(c.Name, used, c.Limit, slack, Math.Abs(slack) <= 1e-6 * Math.Max(1, Math.Abs(c.Limit)), t[m, n + i]));
        }

        return new LpResult("optimal", objective, values, bindings, iterations, null);
    }
}

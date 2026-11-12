using System.Globalization;

namespace OneBase.Application.Analytik.Simulation;

/// <summary>
/// Заключение по сценарию, собранное кодом из посчитанных чисел (без модели и без платных вызовов): главный вывод, изменения показателей,
/// ограничения, риски, рекомендации, исходные данные, допущения. AI-аналитик потом может объяснить его своими словами — но числа те же.
/// </summary>
public static class ScenarioNarrative
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly string[] HeadlineMetrics = ["sales.revenue", "finance.gross_profit", "production.output", "procurement.budget", "finance.operating_profit"];

    internal static SimConclusion Build(ScenarioBaselineData b, SimulationResult r, SimState bs, SimState s, ScenarioParams p)
    {
        var changed = p.All.Where(x => ScenarioCatalog.Find(x.Code) is { Mode: not ParameterDefinition.Input } && (x.Value != 0 || x.Target is not null)).ToList();
        var critical = r.Constraints.Where(c => c.Severity == "critical").ToList();

        string headline;
        if (changed.Count == 0)
        {
            var revenue = r.Metric("sales.revenue");
            headline = $"Базовый сценарий на {r.HorizonDays} дн. ({Date(r.From)} — {Date(r.To)}) по фактическим данным за {Date(b.From)} — {Date(b.To)}" +
                       (revenue?.Base is { } rv ? $": выручка {Money(rv)}." : ".");
        }
        else
        {
            var parts = HeadlineMetrics.Select(r.Metric).Where(m => m is { Base: not null, Scenario: not null } && m.Delta != 0).Take(3)
                .Select(m => $"{Lower(m!.Title)} {Signed(m.DeltaPercent)} ({Value(m.Base, m.Unit)} → {Value(m.Scenario, m.Unit)})").ToList();
            headline = parts.Count > 0 ? Upper(string.Join(", ", parts)) + "." : "Заданные изменения не меняют главные показатели.";
            var notChecked = Unchecked(b, s);
            headline += critical.Count > 0
                ? $" Главное ограничение — {Lower(critical[0].Title)}."
                : r.Constraints.Any(c => c.Severity == "warning") ? $" Внимание: {Lower(r.Constraints.First(c => c.Severity == "warning").Title)}."
                : notChecked.Count > 0 ? $" Ограничения проверены не полностью: {string.Join("; ", notChecked)}."
                : " Сценарий выполним в текущих ограничениях.";
        }

        var changes = r.Metrics.Where(m => m is { Base: not null, Scenario: not null } && m.Delta != 0)
            .Take(12)
            .Select(m => $"{m.Title}: {Value(m.Base, m.Unit)} → {Value(m.Scenario, m.Unit)}" + (m.DeltaPercent is { } d ? $" ({Signed(d)})" : ""))
            .ToList();

        var constraints = r.Constraints.Select(c => $"{c.Title}. {c.Text}").ToList();
        var risks = r.Risks.Select(x => $"{x.Title}. {x.Text}").ToList();
        return new SimConclusion(headline, changes, constraints, risks, Recommendations(b, r, bs, s), Sources(b, r), Assumptions(b, r, bs, s));
    }

    /// <summary>
    /// Что движок не смог проверить — чтобы «выполним» не звучало, когда проверять было нечем: производство и сырьё без техкарт, мощность без
    /// смен, сырьё без доступа к складу.
    /// </summary>
    private static List<string> Unchecked(ScenarioBaselineData b, SimState s)
    {
        var list = new List<string>();
        if (s.HasSales && s.RecipeShare <= 0)
        {
            list.Add("производство и сырьё не проверены — ни один товар не связан с действующей техкартой");
        }
        else if (s.HasSales && s.RecipeShare < 0.999m)
        {
            list.Add($"производство и сырьё проверены для {Math.Round(s.RecipeShare * 100, 1).ToString("0.#", Ru)} % продаж (остальные товары без техкарт)");
        }

        if (s.HasRecipes && s.CapacityKg is null)
        {
            list.Add("мощность неизвестна (нет утверждённых смен и не введена)");
        }

        if (s.HasRecipes && b.Materials is null)
        {
            list.Add("сырьё вам не видно");
        }

        return list;
    }

    private static List<string> Recommendations(ScenarioBaselineData b, SimulationResult r, SimState bs, SimState s)
    {
        var list = new List<string>();
        if (s.CapacityKg is > 0 and var cap && s.CapacityRatio < 1)
        {
            var increase = (s.RequiredKg / cap - 1) * 100;
            var shifts = b.Production?.ShiftsPerDay is { } spd ? $" (сейчас {Math.Round(spd, 1).ToString("0.#", Ru)} смен в рабочий день, можно до 3)" : "";
            list.Add($"Чтобы выпустить весь спрос, мощность нужно увеличить ещё на {Math.Ceiling(increase).ToString("0", Ru)} %: дополнительная смена{shifts} или оборудование.");
        }

        foreach (var m in s.Materials.Values.Where(m => m.LostDays > 0).OrderByDescending(m => m.LostDays).Take(3))
        {
            var cost = m.Price is { } price ? $" (≈ {Money(m.Purchase * price)})" : "";
            list.Add($"Заказать «{m.Item.Name}» сегодня: {Qty(m.Purchase)} {m.Item.Unit}{cost}. Срок поставки {m.LeadTime} дн. — " +
                     "договоритесь о частичной поставке раньше или перераспределите выпуск.");
        }

        foreach (var m in s.Materials.Values.Where(m => m.LostDays == 0 && m.OrderLate && m.Daily > 0).Take(2))
        {
            list.Add($"Заказать «{m.Item.Name}» без задержки — запас на грани (заказ нужен был до {Date(m.OrderBy)}).");
        }

        if (s.CashHasOpening && s.CashGapDays > 0 && s.MinCashDay is { } day)
        {
            list.Add($"Подготовить финансирование {Money(s.FinancingNeed ?? 0)} к {Date(b.AsOf.AddDays(day))} или договориться об отсрочке поставщикам и " +
                     "более быстрой оплате покупателей.");
        }
        else if (!s.CashHasOpening && s.CashIn is not null && s.MinCash < 0)
        {
            list.Add($"Держать на счёте не меньше {Money(-s.MinCash!.Value)} — столько уходит в минус накопленный поток сценария.");
        }

        var m0 = Margin(bs);
        var m1 = Margin(s);
        if (m0 is { } before && m1 is { } after && after < before - 0.5m && s.SoldKg > 0 && AvgCost(s) is { } cost1 && before < 100)
        {
            var price1 = s.Products.Values.Where(x => x.CostPerKg is not null).Sum(x => x.Revenue) /
                         Math.Max(1, s.Products.Values.Where(x => x.CostPerKg is not null).Sum(x => x.SoldKg));
            var needed = cost1 / (1 - before / 100);
            if (price1 > 0 && needed > price1)
            {
                list.Add($"Чтобы сохранить валовую маржу {Math.Round(before, 1).ToString("0.#", Ru)} %, средняя цена кг должна быть выше ещё на " +
                         $"{Math.Round((needed / price1 - 1) * 100, 1).ToString("0.#", Ru)} %.");
            }
        }

        if (r.Risks.Any(x => x.Code == "elasticity"))
        {
            list.Add("Задайте эластичность спроса по цене, если знаете, как покупатели реагировали на прошлые изменения цен, — объём пересчитается.");
        }

        if (s.HasSales && s.RecipeShare < 0.999m)
        {
            list.Add($"Свяжите действующие техкарты с товарами Linko («Производство → Техкарты»): сейчас покрыто {Math.Round(s.RecipeShare * 100, 1).ToString("0.#", Ru)} % " +
                     "продаж — для остальных производство, сырьё и себестоимость не пересчитываются.");
        }

        if (s.Materials.Values.Any(m => m.Daily > 0 && m.Price is null))
        {
            list.Add("Проведите приходы сырья с ценой — без цен бюджет закупок и себестоимость неполные.");
        }

        if (list.Count == 0)
        {
            list.Add("Ограничений по мощности, сырью и деньгам в сценарии нет — его можно сравнить с другими и сохранить.");
        }

        return list;
    }

    private static List<string> Sources(ScenarioBaselineData b, SimulationResult r)
    {
        var list = new List<string>
        {
            $"Базовый период {Date(b.From)} — {Date(b.To)} ({b.Days} дн.), данные на {Date(b.AsOf)}; горизонт сценария {r.HorizonDays} дн. ({Date(r.From)} — {Date(r.To)}).",
        };
        if (b.Sales is { } sales)
        {
            list.Add($"Продажи: Linko, вторичка по правилам «Продажи по SKU»{(sales.DataThrough is { } d ? $", данные по {Date(d)}" : "")}" +
                     (sales.Scope is { } scope ? $"; область: {scope}" : "") + ".");
        }

        if (b.Materials is not null)
        {
            list.Add("Сырьё и закупки: OneBase → Снабжение (остатки, резерв, партии, движения, ожидаемые поставки, поставщики) — как в плане закупок Аналитика.");
        }

        if (b.Production is { } prod)
        {
            list.Add($"Производство: действующие техкарты и утверждённые смены ({prod.Shifts} за {prod.HistoryDays} дн.).");
        }

        if (b.Finance is not null)
        {
            list.Add("Финансы: Linko → оплаты точек (принятые), OneBase → Снабжение → расходы на закупки.");
        }

        if (b.Workforce is { } wf)
        {
            list.Add($"Персонал: Verifix — {wf.Employees} работающих{(wf.SeesAll ? "" : " в ваших подразделениях")}.");
        }

        list.AddRange(b.Sources.Where(x => x.Status is not "ok").Select(x => $"{x.Title}: {x.Note ?? x.Status}."));
        return list;
    }

    private static List<string> Assumptions(ScenarioBaselineData b, SimulationResult r, SimState bs, SimState s)
    {
        var list = new List<string>
        {
            $"Показатели базового периода пересчитаны на горизонт пропорционально дням ({r.HorizonDays} из {b.Days}).",
        };
        if (s.HasRecipes)
        {
            list.Add("Запас готовой продукции (её учитывает Linko) не моделируется: выпуск идёт на продажи.");
            list.Add(b.Production?.CapacityKgDay is not null && s.CapacityKg is not null && r.Inputs.All(i => i.Code != "capacity" || i.Origin != "введено пользователем")
                ? $"Мощность — оценка по утверждённым сменам: {b.Production.CapacitySource}."
                : s.CapacityKg is null ? "Мощность неизвестна — ограничение мощности не проверялось." : "Мощность введена пользователем.");
        }

        if (b.Materials is not null)
        {
            list.Add("Расход сырья в базе — больший из фактического по складу и расчётного по техкартам (без двойного учёта, как в плане закупок).");
            list.Add("Дефицитное сырьё ограничивает выпуск каждого товара по самой дефицитной позиции его техкарты; закупка заказывается сегодня и приходит через срок поставки.");
            if (s.Materials.Values.Any(m => m.Daily > 0 && m.Item.LeadTimeAssumed))
            {
                list.Add("Срок поставки части позиций не задан у поставщика — принят по умолчанию (настройка Аналитика).");
            }
        }

        list.AddRange(bs.Assumptions.Concat(s.Assumptions).Distinct());
        list.AddRange(b.Notes);
        return list.Distinct().ToList();
    }

    private static decimal? Margin(SimState s)
    {
        if (s.Cogs is null)
        {
            return null;
        }

        var revenue = s.Products.Values.Where(x => x.CostPerKg is not null).Sum(x => x.Revenue);
        return revenue > 0 ? (s.Gross ?? s.KnownGross) / revenue * 100 : null;
    }

    private static decimal? AvgCost(SimState s)
    {
        var known = s.Products.Values.Where(x => x.CostPerKg is not null && x.SoldKg > 0).ToList();
        var kg = known.Sum(x => x.SoldKg);
        return kg > 0 ? known.Sum(x => x.SoldKg * x.CostPerKg!.Value) / kg : null;
    }

    internal static string Value(decimal? v, string unit) => v is { } x
        ? unit switch
        {
            "%" => $"{Math.Round(x, 1).ToString("0.#", Ru)} %",
            "сум" => Money(x),
            _ => $"{Math.Round(x, x is > -100 and < 100 ? 2 : 0).ToString("#,0.##", Ru)} {unit}".TrimEnd(),
        }
        : "—";

    internal static string Money(decimal v) => $"{Math.Round(v, 0).ToString("#,0", Ru)} сум";

    private static string Qty(decimal v) => Math.Round(v, 3).ToString("#,0.###", Ru);

    private static string Signed(decimal? pct) => pct is { } p ? (p > 0 ? "+" : p < 0 ? "−" : "") + Math.Round(Math.Abs(p), 1).ToString("0.#", Ru) + " %" : "";

    private static string Date(DateOnly d) => d.ToString("dd.MM.yyyy", Ru);

    private static string Date(DateOnly? d) => d is { } x ? Date(x) : "—";

    private static string Lower(string s) => s.Length > 1 && !char.IsUpper(s[1]) ? char.ToLower(s[0], Ru) + s[1..] : s;

    private static string Upper(string s) => s.Length > 0 ? char.ToUpper(s[0], Ru) + s[1..] : s;
}

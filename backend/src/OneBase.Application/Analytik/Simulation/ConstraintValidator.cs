using System.Globalization;

namespace OneBase.Application.Analytik.Simulation;

/// <summary>
/// Проверки симулятора. До расчёта (<see cref="Validate"/>) — параметры: известный код, пределы, цель есть в снимке, нет повторов и двойного
/// учёта, сложность в пределах, для параметра есть данные. После расчёта (<see cref="Check"/>) — бизнес-ограничения результата: мощность,
/// сырьё и резерв, сроки поставки, партии, склад, финансовый лимит, технологические нормы.
/// </summary>
public static class ConstraintValidator
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static IReadOnlyList<ScenarioIssue> Validate(ScenarioBaselineData b, ScenarioDefinition def)
    {
        var issues = new List<ScenarioIssue>();
        void Error(string code, string text, ScenarioParameter? p = null) => issues.Add(new(ScenarioIssue.Error, code, text, p?.Code, p?.Target));
        void Warn(string code, string text, ScenarioParameter? p = null) => issues.Add(new(ScenarioIssue.Warning, code, text, p?.Code, p?.Target));

        if (!ScenarioKinds.All.Contains(def.Kind))
        {
            Error("kind", $"Неизвестный вид сценария «{def.Kind}».");
        }

        if (def.HorizonDays is < ScenarioCatalog.MinHorizonDays or > ScenarioCatalog.MaxHorizonDays)
        {
            Error("horizon", $"Горизонт — от {ScenarioCatalog.MinHorizonDays} до {ScenarioCatalog.MaxHorizonDays} дней.");
        }

        if (def.Parameters.Count > ScenarioCatalog.MaxParameters)
        {
            Error("complexity", $"Слишком много параметров ({def.Parameters.Count}); не больше {ScenarioCatalog.MaxParameters}.");
            return issues;
        }

        if (def.Parameters.Count(p => p.Target is not null) > ScenarioCatalog.MaxTargetedParameters)
        {
            Error("complexity", $"Точечных изменений (регион, товар, позиция) — не больше {ScenarioCatalog.MaxTargetedParameters}.");
        }

        var seen = new HashSet<(string, string?)>();
        foreach (var p in def.Parameters)
        {
            var d = ScenarioCatalog.Find(p.Code);
            if (d is null)
            {
                Error("unknown", $"Неизвестный параметр «{p.Code}».", p);
                continue;
            }

            if (!seen.Add((p.Code, p.Target)))
            {
                Error("duplicate", $"«{d.Title}» задан дважды{(p.Target is null ? "" : " для одной цели")} — оставьте одно значение.", p);
            }

            // Пределы: у параметров-выборов (поставщик, регион-образец, подразделение) значение не используется.
            if (!(d.Min == 0 && d.Max == 0) && (p.Value < d.Min || p.Value > d.Max))
            {
                Error("range", $"«{d.Title}»: от {Fmt(d.Min)} до {Fmt(d.Max)} {d.Unit}".TrimEnd() + ".", p);
            }

            if (d.Code == "production.shifts" && p.Value != Math.Floor(p.Value))
            {
                Error("range", "Смен в сутки — целое число от 1 до 3.", p);
            }

            if (d.TargetKind is null && p.Target is not null)
            {
                Error("target", $"«{d.Title}» относится ко всем — цель не указывается.", p);
            }
            else if (d.TargetKind is not null && p.Target is null && d.Code is not "workforce.headcount")
            {
                Error("target", $"Для «{d.Title}» выберите {TargetName(d.TargetKind)}.", p);
            }
            else if (d.TargetKind is not null && p.Target is not null && !TargetExists(b, d.TargetKind, p.Target))
            {
                Error("target", $"«{d.Title}»: {TargetName(d.TargetKind)} «{p.Target}» нет в исходных данных сценария.", p);
            }

            // Раздел без доступа (или без данных) — параметр не учитывается.
            if (MissingBlock(b, d.Code) is { } missing)
            {
                Warn("no_data", $"«{d.Title}» не учитывается: {missing}.", p);
            }
        }

        var values = def.Parameters.Where(p => ScenarioCatalog.Find(p.Code) is not null).ToList();
        decimal? Input(string code) => values.FirstOrDefault(p => p.Code == code)?.Value;

        // Двойной учёт: прямое изменение выручки вместе с драйверами продаж учло бы рост дважды.
        if (values.Any(p => p.Code == "finance.revenue" && p.Value != 0) && values.Any(p => ScenarioCatalog.SalesDrivers.Contains(p.Code) && p.Value != 0))
        {
            Error("double_count",
                "Выручка уже меняется через параметры продаж (спрос, цена, скидки, регионы, товары, новый регион) — «Выручка (без изменения продаж)» учла бы то же воздействие второй раз. Оставьте что-то одно.");
        }

        // Нужные исходные данные, которых нет в OneBase.
        var capacityKnown = (Input("input.capacity") ?? 0) > 0 || b.Production?.CapacityKgDay is > 0;
        if (!capacityKnown && values.Any(p => p.Code is "production.capacity" or "production.shifts" or "production.utilization" or "workforce.productivity" && p.Value != 0))
        {
            Warn("capacity_unknown",
                "Мощность производства неизвестна (утверждённых смен нет) — изменения мощности, смен и производительности не проверяются. Введите мощность в исходных данных.");
        }

        if ((Input("input.salary") ?? 0) <= 0 && values.Any(p => p.Code is "workforce.salary" or "workforce.headcount" or "expansion.staff" && p.Value != 0))
        {
            Warn("salary_unknown", "Средняя зарплата не введена — ФОТ не считается (в OneBase зарплат вне продаж нет).");
        }

        if ((Input("input.opex") ?? 0) <= 0 && values.Any(p => p.Code == "finance.expenses" && p.Value != 0))
        {
            Warn("opex_unknown", "Расходы компании не введены — изменение расходов не на что применить.");
        }

        if ((Input("input.cost_per_kg") ?? 0) <= 0 && values.Any(p => p.Code == "finance.cost" && p.Value != 0))
        {
            Warn("cost_unknown", "Себестоимость кг не введена — «Прочая себестоимость» не на что применить.");
        }

        if (values.Any(p => p.Code.StartsWith("expansion.", StringComparison.Ordinal) && p.Code != "expansion.plan" && (p.Value != 0 || p.Target is not null))
            && !values.Any(p => p.Code == "expansion.plan" && p.Value > 0))
        {
            Warn("expansion_plan", "План продаж нового региона не задан — новый регион не считается.");
        }

        if (values.Any(p => p.Code is "production.norms" or "production.norm_item" && p.Value < 0) || values.Any(p => p.Code == "production.defect" && p.Value < 0))
        {
            Warn("norms_below",
                "Нормы или брак ниже утверждённых техкарт — это допущение сценария; фактически такие нормы нужно согласовать с технологом.");
        }

        return issues;
    }

    private static string TargetName(string kind) => kind switch
    {
        "region" => "регион",
        "product" => "товар",
        "item" => "позицию сырья",
        "item_supplier" => "позицию и поставщика",
        "division" => "подразделение",
        _ => "цель",
    };

    private static bool TargetExists(ScenarioBaselineData b, string kind, string target) => kind switch
    {
        "region" => b.Sales?.Regions.Any(r => r.Id == target) == true,
        "product" => long.TryParse(target, out var id) && b.Sales?.Products.Any(p => p.Id == id) == true,
        "item" => Guid.TryParse(target, out var item) && b.Materials?.Any(m => m.ItemId == item) == true,
        "item_supplier" => SupplierOf(b, target) is not null,
        "division" => b.Workforce?.Divisions.Any(d => d.Id == target) == true,
        _ => false,
    };

    /// <summary>Цель «позиция/поставщик» → позиция и поставщик из истории приходов снимка.</summary>
    internal static (BaselineMaterial Item, BaselineSupplier Supplier)? SupplierOf(ScenarioBaselineData b, string target)
    {
        var slash = target.IndexOf('/');
        if (slash <= 0 || !Guid.TryParse(target[..slash], out var itemId))
        {
            return null;
        }

        var item = b.Materials?.FirstOrDefault(m => m.ItemId == itemId);
        var supplier = item?.Suppliers.FirstOrDefault(s => s.Key == target[(slash + 1)..]);
        return item is not null && supplier is not null ? (item, supplier) : null;
    }

    private static string? MissingBlock(ScenarioBaselineData b, string code)
    {
        var prefix = code[..code.IndexOf('.')];
        return prefix switch
        {
            "sales" or "expansion" when b.Sales is null => "продажи вам не видны или не загружены",
            "finance" when code == "finance.revenue" && b.Sales is null => "продажи вам не видны или не загружены",
            "finance" when code == "finance.customer_terms" && b.Finance?.CollectionRate is null => "оплаты точек вам не видны или не загружены",
            "production" when b.Sales is null || b.Sales.Products.All(p => p.Norms.Count == 0) =>
                b.Sales is null ? "продажи вам не видны или не загружены" : "ни один товар продаж не связан с действующей техкартой",
            "materials" or "procurement" when b.Materials is null => "склад сырья и закупки вам не видны",
            "workforce" when code == "workforce.headcount" && b.Workforce is null => "персонал вам не виден",
            _ => null,
        };
    }

    // ---------------- бизнес-ограничения результата ----------------

    /// <summary>Ограничения результата сценария (мощность, сырьё, сроки, партии, склад, финансы, нормы) — по посчитанному состоянию.</summary>
    internal static IReadOnlyList<SimConstraint> Check(SimState s, SimState baseState, ScenarioBaselineData b, ScenarioParams p)
    {
        var list = new List<SimConstraint>();
        var asOf = b.AsOf;

        if (s.CapacityKg is { } cap && s.CapacityRatio < 1)
        {
            list.Add(new("capacity", "critical", "Не хватает мощности производства",
                $"Нужно выпустить {Kg(s.RequiredKg)}, мощность за период — {Kg(cap)} (загрузка {Pct(s.Utilization)}). Выпуск урезан на {Kg(s.RequiredKg - s.PlannedKg)}" +
                (s.LostRevenue > 0 ? $"; недополученная выручка — {Money(s.LostRevenue)}." : ".")));
        }
        else if (s.Utilization is > 0.9m and <= 1m)
        {
            list.Add(new("capacity_high", "warning", "Мощность почти полностью занята",
                $"Загрузка {Pct(s.Utilization)}: любой простой или рост спроса сразу ограничит выпуск."));
        }
        else if (s.CapacityKg is null && s.RequiredKg > 0 && s.HasRecipes)
        {
            list.Add(new("capacity_unknown", "info", "Мощность не проверена",
                "Утверждённых смен нет и мощность не введена — ограничение мощности не проверялось."));
        }

        if (s.LabourBinding)
        {
            list.Add(new("labour", "warning", "Персонал ограничивает мощность",
                "Численность производственного подразделения меньше, чем нужно оборудованию: мощность посчитана по персоналу."));
        }

        foreach (var m in s.Materials.Values.Where(m => m.LostDays > 0).OrderByDescending(m => m.LostDays).Take(10))
        {
            list.Add(new("material_shortage", "critical", $"Дефицит сырья: {m.Item.Name}",
                $"Запас закончится {Date(m.Shortage)}; закупка придёт через {m.LeadTime} дн. ({Date(asOf.AddDays(m.ArrivalDay))}) — без сырья {Days(m.LostDays)}, " +
                "выпуск товаров с этой позицией сокращается.", m.Item.ItemId.ToString()));
        }

        foreach (var m in s.Materials.Values.Where(m => m.OrderLate && m.LostDays == 0 && m.Daily > 0).Take(5))
        {
            list.Add(new("order_late", "warning", $"Поздний заказ: {m.Item.Name}",
                $"Чтобы не было перерыва, заказ нужно было сделать до {Date(m.OrderBy)}; в горизонте дефицит перекрывают поставки.", m.Item.ItemId.ToString()));
        }

        if (s.Materials.Values.Any(m => m.Item.LeadTimeAssumed && m.Daily > 0) && p.Value("materials.lead_time") != 0)
        {
            list.Add(new("lead_time_assumed", "info", "Сроки поставки частично приняты по умолчанию",
                "У части позиций срок поставки не задан у поставщика — задержка добавлена к сроку по умолчанию."));
        }

        var lotSurplus = s.Materials.Values.Where(m => m.LotSurplus > 0 && m.Daily > 0).ToList();
        if (lotSurplus.Count > 0)
        {
            list.Add(new("min_lot", "info", "Минимальная партия больше потребности",
                $"Позиций: {lotSurplus.Count}. Закупка округлена вверх до партии — остаток на складе вырастет ({string.Join(", ", lotSurplus.Take(3).Select(m => m.Item.Name))})."));
        }

        if (p.Input("input.warehouse_kg") is > 0 and var capacityKg && s.PeakStockKg is { } peak && peak > capacityKg)
        {
            list.Add(new("warehouse", "critical", "Не хватит места на складе сырья",
                $"Пиковый запас {Kg(peak)} больше вместимости {Kg(capacityKg)}: закупку нужно разбить на поставки."));
        }

        if (p.Input("input.credit_limit") is > 0 and var limit && s.FinancingNeed is { } need && need > limit)
        {
            list.Add(new("credit_limit", "critical", "Потребность в финансировании больше лимита",
                $"Нужно {Money(need)}, доступно {Money(limit)} — не хватает {Money(need - limit)}."));
        }

        if (s.CashGapDays > 0 && s.CashHasOpening)
        {
            list.Add(new("cash_gap", "critical", "Кассовый разрыв",
                $"Деньги уходят в минус на {s.CashGapDays} дн.; минимум {Money(s.MinCash)} — {Date(s.MinCashDay is { } d ? asOf.AddDays(d) : null)}."));
        }

        if (s.SoldKg < s.RequestedKg - 0.5m && s.LostRevenue > 0)
        {
            list.Add(new("demand_not_revenue", "info", "Рост спроса не равен росту выручки",
                $"Запрошено {Kg(s.RequestedKg)}, обеспечено {Kg(s.SoldKg)}: ограничения производства и сырья съедают {Money(s.LostRevenue)} выручки."));
        }

        if (s.FgSurplusKg > 0.5m)
        {
            list.Add(new("fg_surplus", "info", "Выпуск больше спроса",
                $"Запас готовой продукции вырастет на {Kg(s.FgSurplusKg)} — на это уйдут сырьё и деньги."));
        }

        _ = baseState;
        return list;
    }

    internal static string Fmt(decimal v) => v.ToString("#,0.##", Ru);

    internal static string Kg(decimal v) => $"{Math.Round(v, 0).ToString("#,0", Ru)} кг";

    internal static string Money(decimal? v) => v is { } x ? $"{Math.Round(x, 0).ToString("#,0", Ru)} сум" : "—";

    internal static string Pct(decimal? share) => share is { } s ? $"{Math.Round(s * 100, 1).ToString("0.#", Ru)} %" : "—";

    internal static string Date(DateOnly? d) => d is { } x ? x.ToString("dd.MM.yyyy", Ru) : "—";

    internal static string Days(decimal days) => $"{Math.Round(days, 1).ToString("0.#", Ru)} дн.";
}

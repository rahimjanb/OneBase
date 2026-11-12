using System.Globalization;
using System.Text;
using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Simulation;
using OneBase.Application.Field;

namespace OneBase.AI.Analytik.Simulation;

/// <summary>
/// analytik_simulate — What-If для AI-аналитика: модель только переводит вопрос («продажи +25 %, сырьё +10 %») в параметры, всё считает
/// ScenarioEngine по снимку реальных данных пользователя (его разделы и область). В ответе — база и сценарий по показателям, ограничения, риски,
/// рекомендации, исходные данные, допущения и ссылка «Открыть в симуляторе» с этими параметрами. Сценарий не сохраняется (сохранит человек).
/// </summary>
public sealed class SimulateTool(QueryEngine engine, ScenarioBaselineBuilder builder, TimeProvider clock) : AnalytikTool(engine)
{
    public override string Name => "analytik_simulate";
    public override string Title => "Симулятор сценариев (What-If)";

    public override string Description =>
        "Что будет, если…: считает сценарий движком What-If по реальным данным OneBase (продажи по товарам и регионам, техкарты, мощность по " +
        "сменам, склад сырья и поставки, оплаты, персонал) — выручка, продажи с учётом ограничений, выпуск и загрузка, потребность в сырье, дефицит " +
        "и его даты, закупки и их бюджет, себестоимость и валовая прибыль, деньги и разрывы, ФОТ. Изменения — в процентах, п.п. или днях, как в " +
        "вопросе (рост — плюс, падение — минус). Финансовые показатели сам не считай: все числа — из результата. Без изменений — базовый сценарий.";

    /// <summary>Аргумент инструмента → параметр каталога.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Flat = new Dictionary<string, string>
    {
        ["sales_change_percent"] = "sales.demand",
        ["price_change_percent"] = "sales.price",
        ["discount_change_points"] = "sales.discount",
        ["price_elasticity"] = "sales.elasticity",
        ["plan_change_percent"] = "sales.plan",
        ["production_volume_change_percent"] = "production.volume",
        ["capacity_change_percent"] = "production.capacity",
        ["shifts_per_day"] = "production.shifts",
        ["defect_change_points"] = "production.defect",
        ["norms_change_percent"] = "production.norms",
        ["raw_material_price_change_percent"] = "materials.price",
        ["stock_change_percent"] = "materials.stock",
        ["material_loss_change_points"] = "materials.loss",
        ["delivery_delay_days"] = "materials.lead_time",
        ["purchase_volume_change_percent"] = "procurement.volume",
        ["safety_stock_change_percent"] = "procurement.safety",
        ["supplier_payment_days_change"] = "procurement.supplier_terms",
        ["revenue_change_percent"] = "finance.revenue",
        ["expenses_change_percent"] = "finance.expenses",
        ["customer_payment_days_change"] = "finance.customer_terms",
        ["headcount_change_percent"] = "workforce.headcount",
        ["salary_change_percent"] = "workforce.salary",
        ["productivity_change_percent"] = "workforce.productivity",
    };

    public override JsonElement InputSchema { get; } = SchemaOf();

    private static JsonElement SchemaOf()
    {
        var props = Flat.Select(kv =>
        {
            var d = ScenarioCatalog.Find(kv.Value)!;
            return (kv.Key, (object)new { type = "number", description = $"{d.Title}, {d.Unit} (от {d.Min} до {d.Max}). {d.Description}" });
        }).ToList();
        props.Add(("parameters", new
        {
            type = "array",
            description = "Остальные и точечные параметры: {code, value, target}. target — регион, товар, позиция сырья («позиция/поставщик» для " +
                          "procurement.supplier) или подразделение: id или название. Коды: sales.region, sales.product, production.norm_item, " +
                          "materials.price_item, materials.stock_item, procurement.min_lot, procurement.supplier, expansion.plan, expansion.demand, " +
                          "expansion.reference (target — регион), expansion.staff, expansion.opex, expansion.logistics, expansion.investment, исходные " +
                          "данные (их в OneBase нет — только если пользователь их назвал): input.capacity (кг/сут), input.salary (сум/мес), input.opex " +
                          "(сум/мес), input.cash, input.credit_limit, input.cost_per_kg, input.logistics_per_kg, input.supplier_terms, input.warehouse_kg.",
            items = new
            {
                type = "object",
                properties = new { code = new { type = "string" }, value = new { type = "number" }, target = new { type = "string" } },
                required = new[] { "code", "value" },
            },
        }));
        props.Add(("horizon_days", new { type = "integer", description = "Горизонт сценария в днях от даты данных (7–365), по умолчанию 30." }));
        props.Add(("base_from", new { type = "string", description = "Начало базового периода YYYY-MM-DD (по умолчанию — последние 3 полных месяца)." }));
        props.Add(("base_to", new { type = "string", description = "Конец базового периода YYYY-MM-DD." }));
        props.Add(("new_region_name", new { type = "string", description = "Название нового региона (для expansion.*)." }));
        return Schema(props.ToArray());
    }

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var principal = await PrincipalAsync(context, ct);
        var today = FieldClock.LocalDate(clock.GetUtcNow());
        var (defaultFrom, defaultTo) = ScenarioBaselineBuilder.DefaultPeriod(today);
        var from = Date(args.Str("base_from")) ?? defaultFrom;
        var to = Date(args.Str("base_to")) ?? (args.Str("base_from") is null ? defaultTo : today.AddDays(-1));
        var horizon = args.Int("horizon_days", ScenarioCatalog.MinHorizonDays, ScenarioCatalog.MaxHorizonDays) ?? 30;

        StoredBaseline baseline;
        try
        {
            baseline = await builder.BuildAsync(principal, from, to, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.InvalidArguments, ex.Message);
        }

        var parameters = Parameters(args, baseline.Data);
        var kind = parameters.Any(p => p.Code.StartsWith("expansion.", StringComparison.Ordinal)) && parameters.All(p => !p.Code.StartsWith("sales.", StringComparison.Ordinal))
            ? ScenarioKinds.Expansion
            : ScenarioKinds.Combined;
        var definition = new ScenarioDefinition(kind, horizon, parameters, args.Str("new_region_name"));
        var r = ScenarioEngine.Run(baseline.Data, definition);
        if (!r.Valid)
        {
            return ToolResult.Failure(Name, ToolFailures.InvalidArguments, string.Join(" ", r.Issues.Where(i => i.Severity == ScenarioIssue.Error).Select(i => i.Text)));
        }

        var b = baseline.Data;
        return Data(new
        {
            r.Conclusion.Headline,
            BasePeriod = $"{b.From:dd.MM.yyyy} — {b.To:dd.MM.yyyy}",
            DataAsOf = b.AsOf.ToString("dd.MM.yyyy"),
            Horizon = $"{r.HorizonDays} дн.: {r.From:dd.MM.yyyy} — {r.To:dd.MM.yyyy}",
            Parameters = parameters.Select(p => new { p.Code, Title = ScenarioCatalog.Find(p.Code)?.Title, p.Value, Unit = ScenarioCatalog.Find(p.Code)?.Unit, p.Target }),
            Metrics = r.Metrics.Where(m => m.Base is not null || m.Scenario is not null)
                .Select(m => new { m.Code, m.Title, m.Unit, m.Base, m.Scenario, m.Delta, m.DeltaPercent, m.Kind, m.Note }),
            Unavailable = r.Metrics.Where(m => m.Base is null && m.Scenario is null && m.Note is not null).Select(m => new { m.Title, m.Note }),
            Chain = r.Chain.Select(c => DependencyGraph.Find(c)?.Title ?? c),
            r.Constraints,
            r.Risks,
            r.Conclusion.Recommendations,
            LimitedProducts = r.Products.Where(x => x.Limit is not null).Take(10).Select(x => new { x.Name, x.RequestedKg, x.SoldKg, x.Limit }),
            Shortages = r.Materials.Where(m => m.LostDays > 0 || m.OrderLate).Take(10)
                .Select(m => new { m.Name, m.Unit, m.Daily, m.Available, m.ShortageDate, m.LostDays, m.OrderBy, m.LeadTimeDays, m.Purchase, m.PurchaseCost }),
            r.Expansion,
            Inputs = r.Inputs,
            Warnings = r.Issues.Where(i => i.Severity == ScenarioIssue.Warning).Select(i => i.Text),
            r.Conclusion.Assumptions,
            r.Conclusion.Sources,
            OpenInSimulator = Link(definition, b.From, b.To),
        }, new DataSource("OneBase → Аналитик → Симулятор (движок What-If)", $"{b.From:dd.MM.yyyy} — {b.To:dd.MM.yyyy}", Link(definition, b.From, b.To)));
    }

    /// <summary>Параметры из аргументов: плоские изменения и список; цель — id или название (регион, товар, позиция, поставщик, подразделение).</summary>
    internal static List<ScenarioParameter> Parameters(ToolArgs args, ScenarioBaselineData b)
    {
        var list = new List<ScenarioParameter>();
        foreach (var (arg, code) in Flat)
        {
            var d = ScenarioCatalog.Find(code)!;
            if (args.Dec(arg, d.Min, d.Max) is { } value && value != 0)
            {
                list.Add(new ScenarioParameter(code, value));
            }
        }

        if (args.Element("parameters") is { ValueKind: JsonValueKind.Array } array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var code = item.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : throw new ToolArgumentException("У параметра нет code.");
                var d = ScenarioCatalog.Find(code) ?? throw new ToolArgumentException($"Неизвестный параметр «{code}».");
                var value = item.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0;
                var target = item.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                list.RemoveAll(p => p.Code == code && p.Target is null && d.TargetKind is null);
                list.Add(new ScenarioParameter(code, value, d.TargetKind is null || target is null ? null : Resolve(b, d.TargetKind, target)));
            }
        }

        return list;
    }

    internal static string Resolve(ScenarioBaselineData b, string kind, string target)
    {
        var options = kind switch
        {
            "region" => (b.Sales?.Regions ?? []).Select(r => (r.Id, r.Name)).ToList(),
            "product" => (b.Sales?.Products ?? []).Select(p => (p.Id.ToString(CultureInfo.InvariantCulture), p.Name)).ToList(),
            "item" => (b.Materials ?? []).Select(m => (m.ItemId.ToString(), m.Name)).ToList(),
            "division" => (b.Workforce?.Divisions ?? []).Select(d => (d.Id, d.Name)).ToList(),
            "item_supplier" => (b.Materials ?? []).SelectMany(m => m.Suppliers.Select(s => ($"{m.ItemId}/{s.Key}", $"{m.Name}/{s.Name}"))).ToList(),
            _ => [],
        };
        var exact = options.FirstOrDefault(o => o.Item1 == target || string.Equals(o.Item2, target.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact.Item1 is not null)
        {
            return exact.Item1;
        }

        var words = target.Trim().ToLowerInvariant();
        var partial = options.Where(o => o.Item2.ToLowerInvariant().Contains(words) || words.Contains(o.Item2.ToLowerInvariant())).ToList();
        return partial.Count switch
        {
            1 => partial[0].Item1,
            0 => throw new ToolArgumentException($"«{target}» не найдено среди: {string.Join(", ", options.Take(15).Select(o => o.Item2))}."),
            _ => throw new ToolArgumentException($"«{target}» неоднозначно: {string.Join(", ", partial.Take(10).Select(o => o.Item2))} — уточните."),
        };
    }

    /// <summary>Ссылка на экран симулятора с этими параметрами (черновик в адресе, base64url JSON).</summary>
    internal static string Link(ScenarioDefinition d, DateOnly from, DateOnly to)
    {
        var json = JsonSerializer.Serialize(new { d.Kind, d.HorizonDays, d.Parameters, d.NewRegionName, From = from, To = to }, SimulationJson.Options);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"/analytik/simulator?draft={encoded}";
    }

    private static DateOnly? Date(string? s) => s is null ? null
        : DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : throw new ToolArgumentException($"Дата «{s}»: ожидается YYYY-MM-DD.");
}

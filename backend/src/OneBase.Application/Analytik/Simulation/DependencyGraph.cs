namespace OneBase.Application.Analytik.Simulation;

/// <summary>Узел графа влияния: код, название, от каких узлов зависит. Узлы «in.*» — входы (параметры сценария).</summary>
public sealed record GraphNode(string Code, string Title, IReadOnlyList<string> DependsOn);

/// <summary>
/// Граф влияния симулятора — явный и проверяемый: каждая зависимость между показателями записана здесь и соответствует формуле движка
/// (тест: параметр меняет только показатели узлов, достижимых от него). Модель зависимостей не придумывает — она только выбирает параметры.
/// Каталог данных Аналитика (CatalogService) находит таблицы, но формулы и связи показателей задаются только здесь.
/// </summary>
public static class DependencyGraph
{
    public static readonly IReadOnlyList<GraphNode> Nodes =
    [
        // входы
        new("in.demand", "Спрос", []),
        new("in.region", "Продажи регионов", []),
        new("in.product", "Продажи товаров", []),
        new("in.price", "Цена", []),
        new("in.elasticity", "Эластичность спроса по цене", []),
        new("in.discount", "Скидки", []),
        new("in.plan", "План продаж", []),
        new("in.expansion", "Новый регион", []),
        new("in.revenue", "Прямое изменение выручки", []),
        new("in.prod_volume", "Объём производства", []),
        new("in.capacity", "Мощность", []),
        new("in.shifts", "Смены", []),
        new("in.workforce", "Численность", []),
        new("in.productivity", "Производительность", []),
        new("in.defect", "Брак", []),
        new("in.norms", "Нормы расхода", []),
        new("in.loss", "Потери сырья", []),
        new("in.mat_price", "Цена сырья", []),
        new("in.supplier", "Поставщик", []),
        new("in.stock", "Запас сырья", []),
        new("in.lead_time", "Срок поставки", []),
        new("in.min_lot", "Минимальная партия", []),
        new("in.safety", "Страховой запас", []),
        new("in.purchase_volume", "Объём закупки", []),
        new("in.supplier_terms", "Отсрочка поставщикам", []),
        new("in.customer_terms", "Срок оплаты покупателей", []),
        new("in.salary", "Зарплаты", []),
        new("in.expenses", "Расходы компании", []),
        new("in.cost", "Прочая себестоимость", []),
        new("in.inputs", "Исходные данные", []),

        // продажи → производство → сырьё → закупки → деньги
        new("sales.requested", "Запрошенный объём продаж", ["in.demand", "in.region", "in.product", "in.elasticity", "in.expansion"]),
        new("sales.price", "Цена нетто", ["in.price", "in.discount", "in.expansion"]),
        new("production.required", "Требуемый выпуск", ["sales.requested", "in.prod_volume"]),
        new("production.capacity", "Мощность производства", ["in.capacity", "in.shifts", "in.workforce", "in.productivity", "in.defect", "in.inputs"]),
        new("production.planned", "Выпуск в пределах мощности", ["production.required", "production.capacity"]),
        new("materials.need", "Расход сырья", ["production.planned", "in.norms", "in.loss", "in.defect"]),
        new("materials.supply", "Запас и поставки сырья", ["in.stock", "in.lead_time", "in.supplier"]),
        new("materials.need_value", "Потребность в сырье, сум", ["materials.need", "in.mat_price", "in.supplier"]),
        new("procurement.purchase", "Закупка", ["materials.need", "materials.supply", "in.min_lot", "in.safety", "in.purchase_volume"]),
        new("materials.coverage", "Обеспечение сырьём", ["materials.need", "materials.supply", "procurement.purchase"]),
        new("production.output", "Выпуск", ["production.planned", "materials.coverage"]),
        new("sales.sold", "Продано", ["sales.requested", "production.output"]),
        new("sales.revenue", "Выручка", ["sales.sold", "sales.price", "in.revenue"]),
        new("sales.plan", "Выполнение плана", ["sales.sold", "in.plan", "in.expansion"]),
        new("cost.unit", "Себестоимость кг", ["in.mat_price", "in.supplier", "in.norms", "in.loss", "in.defect", "in.cost", "in.inputs"]),
        new("finance.cogs", "Себестоимость проданного", ["sales.sold", "cost.unit"]),
        new("finance.gross", "Валовая прибыль", ["sales.revenue", "finance.cogs"]),
        new("workforce.headcount", "Численность", ["in.workforce", "in.expansion"]),
        new("workforce.payroll", "ФОТ", ["workforce.headcount", "in.salary", "in.inputs"]),
        new("finance.logistics", "Логистика", ["sales.sold", "in.expansion", "in.inputs"]),
        new("finance.opex", "Расходы компании", ["in.expenses", "in.expansion", "in.inputs"]),
        new("finance.operating", "Операционная прибыль", ["finance.gross", "workforce.payroll", "finance.opex", "finance.logistics"]),
        new("procurement.budget", "Бюджет закупок", ["procurement.purchase", "in.mat_price", "in.supplier"]),
        new("inventory.stock", "Запас сырья на конец", ["materials.supply", "procurement.purchase", "materials.need", "materials.coverage", "in.mat_price", "in.supplier"]),
        new("finance.cash", "Денежный поток", ["sales.revenue", "in.customer_terms", "procurement.budget", "in.supplier_terms", "workforce.payroll", "finance.opex",
            "finance.logistics", "materials.supply"]),
        new("finance.working_capital", "Оборотный капитал", ["inventory.stock", "sales.revenue", "in.customer_terms", "procurement.budget", "in.supplier_terms",
            "materials.supply"]),
        new("finance.financing", "Потребность в финансировании", ["finance.cash", "in.inputs"]),
    ];

    private static readonly Dictionary<string, GraphNode> ByCode = Nodes.ToDictionary(n => n.Code, StringComparer.Ordinal);

    public static GraphNode? Find(string code) => ByCode.GetValueOrDefault(code);

    /// <summary>Порядок расчёта (топологическая сортировка). Цикл в графе — ошибка конфигурации.</summary>
    public static IReadOnlyList<string> Order()
    {
        var indegree = Nodes.ToDictionary(n => n.Code, n => n.DependsOn.Count, StringComparer.Ordinal);
        var dependents = Nodes.SelectMany(n => n.DependsOn.Select(d => (From: d, To: n.Code))).ToLookup(e => e.From, e => e.To);
        var queue = new Queue<string>(Nodes.Where(n => n.DependsOn.Count == 0).Select(n => n.Code));
        var order = new List<string>();
        while (queue.Count > 0)
        {
            var code = queue.Dequeue();
            order.Add(code);
            foreach (var next in dependents[code])
            {
                if (--indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        if (order.Count != Nodes.Count)
        {
            throw new InvalidOperationException("В графе зависимостей симулятора есть цикл: " +
                                                string.Join(", ", Nodes.Select(n => n.Code).Except(order)));
        }

        return order;
    }

    /// <summary>Узлы, которые пересчитываются от изменения входов <paramref name="inputs"/> (сами входы и все зависимые), в порядке расчёта.</summary>
    public static IReadOnlyList<string> Affected(IEnumerable<string> inputs)
    {
        var dependents = Nodes.SelectMany(n => n.DependsOn.Select(d => (From: d, To: n.Code))).ToLookup(e => e.From, e => e.To);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(inputs.Where(ByCode.ContainsKey));
        while (stack.Count > 0)
        {
            var code = stack.Pop();
            if (!seen.Add(code))
            {
                continue;
            }

            foreach (var next in dependents[code])
            {
                stack.Push(next);
            }
        }

        return Order().Where(seen.Contains).ToList();
    }

    /// <summary>
    /// Цепочка влияния параметров для экрана: коды пересчитываемых показателей (без входов) в порядке расчёта. Исходные данные (input.*)
    /// одинаковы для базы и сценария — разницы не дают и в цепочку не входят.
    /// </summary>
    public static IReadOnlyList<string> Chain(IEnumerable<ScenarioParameter> parameters)
    {
        var inputs = parameters.Select(p => ScenarioCatalog.Find(p.Code)).Where(d => d is { Mode: not ParameterDefinition.Input })
            .Select(d => d!.Node).Distinct().ToList();
        return Affected(inputs).Where(c => !c.StartsWith("in.", StringComparison.Ordinal)).ToList();
    }
}

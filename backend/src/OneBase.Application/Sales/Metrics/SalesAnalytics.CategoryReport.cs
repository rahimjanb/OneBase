namespace OneBase.Application.Sales.Metrics;

/// <summary>Продажи категории за один день в охвате: кг, сумма, ТТ с покупкой категории (АКБ дня), заказы и проданные артикулы.</summary>
public sealed record CategoryDayTotals(decimal Kg, decimal Revenue, int Akb, int Orders = 0, int Skus = 0);

/// <summary>Артикул категории за день в охвате: кг, сумма, ТТ с покупкой.</summary>
public sealed record CategoryDaySkuSales(long ProductId, string Name, string? Code, decimal Kg, decimal Revenue, int Akb);

// Данные для итогов дня по категориям автоотчётов Telegram («11. Итоги дня по категориям»): те же строки и правила, что у страницы
// категории «Продажи → Ассортимент → категория» (охват — AssortmentScope), плюс план категории охвата и продажи одного дня.
public sealed partial class SalesAnalytics
{
    /// <summary>Категории отчёта в порядке «Настроек продаж»: группа (отрицательный id) и название.</summary>
    public IReadOnlyList<(long Group, string Name)> ReportCategoryGroups() =>
        _cats.Mapping.Values.Where(g => SalesCategories.IsConfigured(g)).Distinct().OrderByDescending(g => g).Select(g => (g, _cats.NameOf(g))).ToList();

    /// <summary>Группа категории отчёта по названию из настройки («Придзел»); null — такой категории нет.</summary>
    public long? ReportCategoryGroup(string name) => _cats.GroupByName(name);

    /// <summary>
    /// План категории в охвате — как блок «План и факт по категориям» страниц «Продаж»: ТП — его планы Linko по категориям; регион,
    /// несколько регионов и республика — планы регионов (РОП), а без них — сумма планов ТП охвата. План на несколько категорий
    /// («Могуль + Шоколад») — строка с обоими названиями и CategoryId = null: отдельного плана категории в нём нет. Плана нет — null.
    /// </summary>
    public CategoryPlanFact? CategoryPlanOf(long group, AssortmentScope s)
    {
        List<CategoryPlanFact> rows;
        if (s.Agent is { } agent)
        {
            if (!HasAgent(agent))
            {
                return null;
            }

            rows = PlanFacts(CurrentCategoryPlans().Where(p => p.AgentId == agent).ToList(), _currentByAgent[agent].ToList());
        }
        else
        {
            IReadOnlySet<Guid>? regions = s.Region is { } r ? new HashSet<Guid> { r } : s.Regions?.ToHashSet();
            rows = CategoryPlansOf(regions);
        }

        var name = _cats.NameOf(group);
        return rows.FirstOrDefault(p => p.CategoryId == group && p.PlanKg != null)
               ?? rows.FirstOrDefault(p => p.CategoryId == null && p.PlanKg != null && p.Name.Split(" + ").Contains(name, StringComparer.Ordinal));
    }

    /// <summary>
    /// Продажи категории в охвате за день day (строки месяца с этой датой; день прошлого месяца — из его строк, раньше — нули); охвата нет —
    /// null. Заказы — разные заказы (возвраты не заказы), артикулы — с чистым весом больше нуля.
    /// </summary>
    public CategoryDayTotals? CategoryDayOf(long group, AssortmentScope s, DateOnly day)
    {
        if (ScopeOf(s) is not { } scope)
        {
            return null;
        }

        var lines = DayLines(scope, group, day);
        return new CategoryDayTotals(lines.Sum(l => l.Kg), lines.Sum(l => l.Revenue), SalesMath.Akb(lines),
            lines.Where(l => l.OrderId != null).Select(l => l.OrderId).Distinct().Count(),
            lines.Where(l => l.ProductId != null).GroupBy(l => l.ProductId).Count(g => g.Sum(l => l.Kg) > 0));
    }

    /// <summary>Килограммы категории в охвате с 1-го числа месяца по день перед day (факт до дня — для плана дня); охвата нет — 0.</summary>
    public decimal CategoryKgBefore(long group, AssortmentScope s, DateOnly day) =>
        ScopeOf(s) is { } scope ? scope.Now.Where(l => l.Date < day && GroupOf(l.CategoryId) == group).Sum(l => l.Kg) : 0;

    /// <summary>Артикулы категории, проданные в охвате за день, по убыванию кг (чистый вес больше нуля); охвата нет — пусто.</summary>
    public IReadOnlyList<CategoryDaySkuSales> CategoryDaySkus(long group, AssortmentScope s, DateOnly day)
    {
        if (ScopeOf(s) is not { } scope)
        {
            return [];
        }

        return DayLines(scope, group, day)
            .Where(l => l.ProductId != null)
            .GroupBy(l => l.ProductId!.Value)
            .Select(g => new CategoryDaySkuSales(g.Key, ProductName(g.Key), _d.Products.TryGetValue(g.Key, out var p) ? p.Code : null, g.Sum(l => l.Kg),
                g.Sum(l => l.Revenue), SalesMath.Akb(g)))
            .Where(x => x.Kg > 0)
            .OrderByDescending(x => x.Kg)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Строки категории охвата за день: этот месяц — его строки, прошлый — строки прошлого месяца, другие — пусто.</summary>
    private List<SaleLine> DayLines(ScopeData scope, long group, DateOnly day)
    {
        var source = (day.Year, day.Month) == (_d.Year, _d.Month) ? scope.Now
            : day.AddMonths(1) is var next && (next.Year, next.Month) == (_d.Year, _d.Month) ? scope.Before
            : [];
        return source.Where(l => l.Date == day && GroupOf(l.CategoryId) == group).ToList();
    }
}

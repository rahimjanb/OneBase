namespace OneBase.Application.Sales.Metrics;

/// <summary>Продажи категории за один день в охвате: кг, сумма, ТТ с покупкой категории (АКБ дня).</summary>
public sealed record CategoryDayTotals(decimal Kg, decimal Revenue, int Akb);

/// <summary>Продажи категории за день месяца (для столбиков карточки).</summary>
public sealed record CategoryDayPoint(DateOnly Day, decimal Kg);

// Данные для карточек категорий автоотчётов Telegram («10. Карточки категорий»): те же строки и правила, что у страницы категории
// «Продажи → Ассортимент → категория» (охват — AssortmentScope), плюс план категории охвата, итог дня и ряд по дням месяца.
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

    /// <summary>Продажи категории в охвате за день day (строки месяца с этой датой); охвата нет — null.</summary>
    public CategoryDayTotals? CategoryDayOf(long group, AssortmentScope s, DateOnly day)
    {
        if (ScopeOf(s) is not { } scope)
        {
            return null;
        }

        var lines = scope.Now.Where(l => l.Date == day && GroupOf(l.CategoryId) == group).ToList();
        return new CategoryDayTotals(lines.Sum(l => l.Kg), lines.Sum(l => l.Revenue), SalesMath.Akb(lines));
    }

    /// <summary>Килограммы категории по дням месяца с 1-го по отчётный день (дни без продаж — ноль); охвата нет — пусто.</summary>
    public IReadOnlyList<CategoryDayPoint> CategoryDailyKg(long group, AssortmentScope s)
    {
        if (ScopeOf(s) is not { } scope)
        {
            return [];
        }

        var first = new DateOnly(_d.Year, _d.Month, 1);
        var last = new DateOnly(_d.Year, _d.Month, _d.DaysInMonth);
        var through = _d.DataThrough < first ? first : _d.DataThrough > last ? last : _d.DataThrough;
        var byDay = scope.Now.Where(l => GroupOf(l.CategoryId) == group).GroupBy(l => l.Date).ToDictionary(g => g.Key, g => g.Sum(l => l.Kg));
        return Enumerable.Range(0, through.DayNumber - first.DayNumber + 1)
            .Select(i => first.AddDays(i))
            .Select(d => new CategoryDayPoint(d, byDay.GetValueOrDefault(d)))
            .ToList();
    }
}

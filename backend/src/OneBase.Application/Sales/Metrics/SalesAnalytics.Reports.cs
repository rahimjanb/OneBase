namespace OneBase.Application.Sales.Metrics;

/// <summary>Человек Linko в структуре региона: ID и имя без служебного номера.</summary>
public sealed record ReportPerson(long Id, string Name);

/// <summary>Вакансия региона в Linko («ТП3 вакант …»): за ней закреплены точки, но ТП нет — в план, базу и маршрут отчётов она не входит.</summary>
public sealed record ReportVacancy(long Id, string Name, int Markets);

/// <summary>
/// Порядок имён для людей: числа внутри — по значению («ТП2» раньше «ТП10»), остальное — по алфавиту без учёта регистра (ru-RU).
/// </summary>
public sealed class NaturalNameComparer : IComparer<string>
{
    public static readonly NaturalNameComparer Instance = new();

    private static readonly StringComparer Text = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), true);

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? -1 : 1;
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var si = i;
                var sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var a = x[si..i].TrimStart('0');
                var b = y[sj..j].TrimStart('0');
                var byValue = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
                if (byValue != 0)
                {
                    return byValue;
                }

                continue;
            }

            var ei = i;
            var ej = j;
            while (ei < x.Length && !char.IsDigit(x[ei])) ei++;
            while (ej < y.Length && !char.IsDigit(y[ej])) ej++;
            var byText = Text.Compare(x[i..ei], y[j..ej]);
            if (byText != 0)
            {
                return byText;
            }

            i = ei;
            j = ej;
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}

/// <summary>
/// Регион для автоотчётов: РМ направления, «СВР региона» из оргструктуры, супервайзеры Linko этого региона (как в разделе «KPI») и ТП
/// региона (как в команде региона: продажи месяца, профиль, визиты, план; без вакансий).
/// </summary>
public sealed record ReportRegionTeam(Guid Id, string Name, string? Manager, string? SupervisorName, IReadOnlyList<ReportPerson> Supervisors, IReadOnlyList<ReportPerson> Agents,
    Guid? DirectionId = null, string? DirectionName = null, IReadOnlyList<ReportVacancy>? Vacancies = null);

/// <summary>Направление (РМ) для автоотчётов: название и РМ из оргструктуры OneBase; каналы (базар, сети) — не направления РМ.</summary>
public sealed record ReportDirection(Guid Id, string Name, string? Manager);

// Автоотчёты в Telegram: структура «регион → супервайзер → агенты» из данных OneBase (оргструктура продаж и Linko), без Sales Base.
public sealed partial class SalesAnalytics
{
    public IReadOnlyList<ReportRegionTeam> CachedReportTeams() => View("report-teams", () => ReportTeams());

    /// <summary>
    /// Регионы месяца с командой. Супервайзер — пользователь Linko с показателем СВР, командным планом или должностью «Супервайзер»; его
    /// регион — как в «KPI» (профиль, «СВР региона» по имени, продажи и визиты, регион в должности). В Linko агент к супервайзеру не
    /// привязан, поэтому агенты — команда региона; у региона с двумя СВР агенты у обоих одни и те же.
    /// </summary>
    public IReadOnlyList<ReportRegionTeam> ReportTeams()
    {
        var indicators = _d.Indicators.ToLookup(i => i.AgentId);
        var supervisorIds = _d.Indicators.Where(i => KpiMath.IsSupervisorIndicator(i.FullName ?? i.Name)).Select(i => i.AgentId)
            .Concat(_d.TeamPlanStaff)
            .Concat(_d.Agents.Values.Where(a => a.IsActive && a.Job is { } job && job.Contains("Супервайзер", StringComparison.OrdinalIgnoreCase)).Select(a => a.Id))
            .Distinct()
            .ToList();
        var supervisorsByRegion = supervisorIds
            .Select(id => (Id: id, Region: SupervisorRegion(id, indicators[id]).Region))
            .Where(x => x.Region != NoRegionId)
            .ToLookup(x => x.Region, x => x.Id);
        var culture = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), true);

        return _regions.Values
            .Where(r => r.Id != NoRegionId)
            .Select(r =>
            {
                var direction = r.DirectionId is { } d && _directions.TryGetValue(d, out var x) ? x : null;
                var supervisors = supervisorsByRegion[r.Id].Distinct().Select(id => new ReportPerson(id, PersonName(AgentName(id)))).OrderBy(p => p.Name, NaturalNameComparer.Instance).ToList();
                // Команда месяца и ТП, продававшие в регионе в прошлом месяце, и закреплённые профилем: в начале месяца (отчётный день ещё в
                // прошлом месяце) команда месяца пуста, а отчёт по региону нужен с первого дня. Только действующие, без вакансий.
                var agents = TeamOf(r.Id)
                    .Concat(_previousByRegion[r.Id].Where(l => l.AgentId != null).Select(l => l.AgentId!.Value))
                    .Concat(_d.Agents.Values.Where(a => a.ProfileRegionId == r.Id).Select(a => a.Id))
                    .Distinct()
                    .Where(a => IsSalesRep(a) && !IsVacancy(a) && !supervisorIds.Contains(a) && (!_d.Agents.TryGetValue(a, out var info) || info.IsActive))
                    .Select(id => new ReportPerson(id, PersonName(AgentName(id))))
                    .OrderBy(p => p.Name, NaturalNameComparer.Instance) // ТП2 раньше ТП10
                    .ToList();
                // Вакансии Linko с точками региона: в отчёты не входят, но отчёт говорит, сколько точек без ТП.
                var vacancies = _d.Markets.Values
                    .Where(m => m.ResponsibleAgentId is { } a && IsVacancy(a) && BranchRegion(m.BranchId) == r.Id)
                    .GroupBy(m => m.ResponsibleAgentId!.Value)
                    .Select(g => new ReportVacancy(g.Key, PersonName(AgentName(g.Key)), g.Count()))
                    .OrderBy(v => v.Name, NaturalNameComparer.Instance)
                    .ToList();
                return new ReportRegionTeam(r.Id, r.Name, direction is { IsChannel: false } ? direction.ManagerName : null, r.Supervisor, supervisors, agents,
                    direction is { IsChannel: false } ? direction.Id : null, direction is { IsChannel: false } ? direction.Name : null, vacancies);
            })
            .Where(r => r.Agents.Count > 0 || r.Supervisors.Count > 0)
            .OrderBy(r => r.Name, culture)
            .ToList();
    }

    /// <summary>Направления РМ по порядку оргструктуры (без каналов).</summary>
    public IReadOnlyList<ReportDirection> ReportDirections() =>
        _directions.Values.Where(d => !d.IsChannel).OrderBy(d => d.SortOrder).ThenBy(d => d.Name).Select(d => new ReportDirection(d.Id, d.Name, d.ManagerName)).ToList();

    /// <summary>Категории отчёта в порядке «Настроек продаж».</summary>
    public IReadOnlyList<string> ReportCategoryNames() =>
        _cats.Mapping.Values.Where(g => SalesCategories.IsConfigured(g)).Distinct().OrderByDescending(g => g).Select(g => _cats.NameOf(g)).ToList();

    /// <summary>Категория отчёта товара; null — товар вне категорий отчёта (импорт, бонус) или неизвестен.</summary>
    public string? ReportCategoryOf(long productId) =>
        _d.Products.TryGetValue(productId, out var p) && _cats.GroupOf(p.CategoryId) is { } g && SalesCategories.IsConfigured(g) ? _cats.NameOf(g) : null;

    /// <summary>
    /// Месячные планы ТП по категориям отчёта, кг — показатели Linko по весу, отнесённые к категориям по названию. План на несколько категорий
    /// («Могуль + Шоколад») — одной строкой с обоими названиями.
    /// </summary>
    public IReadOnlyDictionary<long, IReadOnlyDictionary<string, decimal>> ReportCategoryPlans(int year, int month) =>
        _d.CategoryPlans
            .Where(p => p.Year == year && p.Month == month && p.Groups.Count > 0)
            .GroupBy(p => p.AgentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, decimal>)g
                .GroupBy(p => string.Join(" + ", p.Groups.Select(x => _cats.NameOf(x))))
                .ToDictionary(x => x.Key, x => x.Sum(p => p.PlanKg)));
}

namespace OneBase.Application.Sales.Metrics;

/// <summary>Человек Linko в структуре региона: ID и имя без служебного номера.</summary>
public sealed record ReportPerson(long Id, string Name);

/// <summary>
/// Регион для автоотчётов: РМ направления, «СВР региона» из оргструктуры, супервайзеры Linko этого региона (как в разделе «KPI») и ТП
/// региона (как в команде региона: продажи месяца, профиль, визиты, план; без вакансий).
/// </summary>
public sealed record ReportRegionTeam(Guid Id, string Name, string? Manager, string? SupervisorName, IReadOnlyList<ReportPerson> Supervisors, IReadOnlyList<ReportPerson> Agents);

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
                var supervisors = supervisorsByRegion[r.Id].Distinct().Select(id => new ReportPerson(id, PersonName(AgentName(id)))).OrderBy(p => p.Name, culture).ToList();
                // Команда месяца и ТП, продававшие в регионе в прошлом месяце, и закреплённые профилем: в начале месяца (отчётный день ещё в
                // прошлом месяце) команда месяца пуста, а отчёт по региону нужен с первого дня. Только действующие, без вакансий.
                var agents = TeamOf(r.Id)
                    .Concat(_previousByRegion[r.Id].Where(l => l.AgentId != null).Select(l => l.AgentId!.Value))
                    .Concat(_d.Agents.Values.Where(a => a.ProfileRegionId == r.Id).Select(a => a.Id))
                    .Distinct()
                    .Where(a => IsSalesRep(a) && !IsVacancy(a) && !supervisorIds.Contains(a) && (!_d.Agents.TryGetValue(a, out var info) || info.IsActive))
                    .Select(id => new ReportPerson(id, PersonName(AgentName(id))))
                    .OrderBy(p => p.Name, culture)
                    .ToList();
                return new ReportRegionTeam(r.Id, r.Name, direction is { IsChannel: false } ? direction.ManagerName : null, r.Supervisor, supervisors, agents);
            })
            .Where(r => r.Agents.Count > 0 || r.Supervisors.Count > 0)
            .OrderBy(r => r.Name, culture)
            .ToList();
    }
}

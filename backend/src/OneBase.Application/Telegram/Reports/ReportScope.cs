using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Область отчёта получателя: РМ — направление (null — вся страна), СВ — регион и супервайзер, ТП — агент. Key — часть ключа отправки:
/// один и тот же отчёт одной области в один чат уходит один раз, сколько бы получателей его ни запрашивали.
/// </summary>
public sealed record ReportScope(string Role, Guid? DirectionId = null, Guid? RegionId = null, long? SupervisorId = null, long? AgentId = null)
{
    public string Key => Role switch
    {
        TelegramReportRoles.Rm => $"rm:{DirectionId?.ToString("N") ?? "all"}",
        TelegramReportRoles.Tp => $"tp:{AgentId}",
        _ => $"sv:{RegionId:N}:{SupervisorId?.ToString() ?? "-"}",
    };

    public static ReportScope Of(TelegramReportSubscription s) => new(s.Role, s.DirectionId, s.RegionId, s.SupervisorId, s.AgentId);

    /// <summary>Область из ключа отправки («sv:…:…», «tp:123», «rm:all») — строка журнала самодостаточна; null — ключ не разобран.</summary>
    public static ReportScope? Parse(string? key)
    {
        var parts = (key ?? "").Split(':');
        return parts switch
        {
            [TelegramReportRoles.Rm, "all"] => new ReportScope(TelegramReportRoles.Rm),
            [TelegramReportRoles.Rm, var d] when Guid.TryParse(d, out var direction) => new ReportScope(TelegramReportRoles.Rm, direction),
            [TelegramReportRoles.Tp, var a] when long.TryParse(a, out var agent) => new ReportScope(TelegramReportRoles.Tp, AgentId: agent),
            [TelegramReportRoles.Sv, var r, var s] when Guid.TryParse(r, out var region) =>
                new ReportScope(TelegramReportRoles.Sv, RegionId: region, SupervisorId: long.TryParse(s, out var sup) ? sup : null),
            _ => null,
        };
    }
}

/// <summary>Регион области: супервайзер на картинке, РМ и ТП (у ТП — только он сам).</summary>
public sealed record ScopeRegion(Guid Id, string Name, string? Supervisor, string? Manager, IReadOnlyList<ReportPerson> Agents);

/// <summary>
/// Область, найденная в данных «Продаж» месяца: подпись («Ташкент · СВ Азиз Каримов»), регионы с ТП. В отчёт попадают только эти ТП —
/// права получателя проверяются здесь, при каждом построении отчёта, а не на странице.
/// </summary>
public sealed record ResolvedScope(ReportScope Scope, string Title, string? Manager, IReadOnlyList<ScopeRegion> Regions, ReportPerson? Agent)
{
    public IReadOnlyList<ReportPerson> Agents => Regions.SelectMany(r => r.Agents).DistinctBy(a => a.Id).ToList();
}

public static class ReportScopes
{
    /// <summary>
    /// Область в структуре месяца. РМ — регионы направления (все — без направления); СВ — регион (агенты — команда региона: в Linko ТП к СВ
    /// не привязан); ТП — его регион и он один. Нет такой области в данных месяца — KeyNotFoundException с понятной причиной.
    /// </summary>
    public static ResolvedScope Resolve(ReportScope scope, IReadOnlyList<ReportRegionTeam> teams, IReadOnlyList<ReportDirection> directions, Func<string, string> t)
    {
        switch (scope.Role)
        {
            case TelegramReportRoles.Rm:
            {
                var direction = scope.DirectionId is { } d ? directions.FirstOrDefault(x => x.Id == d) ?? throw new KeyNotFoundException("Направление РМ не найдено в оргструктуре продаж.") : null;
                var regions = teams.Where(r => direction is null || r.DirectionId == direction.Id).Select(Region).ToList();
                if (regions.Count == 0)
                {
                    throw new KeyNotFoundException("У направления нет регионов с ТП в данных продаж за этот месяц.");
                }

                var title = direction is null ? t("Вся страна") : $"{t("РМ")} {direction.Manager ?? direction.Name}";
                return new ResolvedScope(scope, direction is { Manager: not null } && direction.Name != direction.Manager ? $"{title} · {direction.Name}" : title,
                    direction?.Manager, regions, null);
            }

            case TelegramReportRoles.Tp:
            {
                var team = teams.FirstOrDefault(r => r.Agents.Any(a => a.Id == scope.AgentId))
                           ?? throw new KeyNotFoundException("ТП нет в командах регионов за этот месяц (нет продаж, плана и профиля).");
                var agent = team.Agents.First(a => a.Id == scope.AgentId);
                return new ResolvedScope(scope, $"{t("ТП")} {agent.Name} · {team.Name}", team.Manager,
                    [new ScopeRegion(team.Id, team.Name, team.SupervisorName, team.Manager, [agent])], agent);
            }

            default:
            {
                var team = teams.FirstOrDefault(r => r.Id == scope.RegionId) ?? throw new KeyNotFoundException("Регион не найден в данных продаж за этот месяц.");
                var supervisor = scope.SupervisorId is { } sid ? team.Supervisors.FirstOrDefault(s => s.Id == sid)?.Name : team.SupervisorName;
                if (scope.SupervisorId is not null && supervisor is null)
                {
                    throw new KeyNotFoundException("Супервайзер не относится к этому региону в данных продаж за этот месяц.");
                }

                var region = Region(team) with { Supervisor = supervisor ?? team.SupervisorName };
                var title = region.Supervisor is { Length: > 0 } s ? $"{team.Name} · {t("СВ")} {s}" : team.Name;
                return new ResolvedScope(scope, title, team.Manager, [region], null);
            }
        }

        static ScopeRegion Region(ReportRegionTeam r) => new(r.Id, r.Name, r.SupervisorName, r.Manager, r.Agents);
    }
}

using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Access;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Security;

namespace OneBase.Application.Analytik.Query;

/// <summary>
/// Область строк продаж пользователя для движка: organization — вся организация; region — филиалы Linko своих регионов (со старыми
/// филиалами, как в отчётах продаж), регионы и направления; agent — только свой ТП (linko.Users.Id).
/// </summary>
public sealed record SalesRowScope(string Level, IReadOnlyList<long> BranchIds, IReadOnlyList<Guid> RegionIds, IReadOnlyList<Guid> DirectionIds,
    long? AgentId, string Description)
{
    public const string OrganizationLevel = "organization";
    public const string RegionLevel = "region";
    public const string AgentLevel = "agent";

    public static readonly SalesRowScope Organization = new(OrganizationLevel, [], [], [], null, "вся организация");

    public bool Restricted => Level != OrganizationLevel;
}

/// <summary>
/// От чьего имени работает движок: итоговые права пользователя и его область продаж. System — фоновые задачи Аналитика (полный доступ к
/// открытым таблицам; находки и отчёты потом показываются каждому по его правам).
/// </summary>
public sealed record AnalytikPrincipal(Guid? UserId, IReadOnlySet<string> Permissions, SalesRowScope Sales, bool System = false)
{
    public static AnalytikPrincipal SystemPrincipal { get; } = new(null, new HashSet<string>(), SalesRowScope.Organization, true);

    public bool Has(string permission) => System || Permissions.Contains(permission);

    public bool HasAny(IEnumerable<string> permissions) => System || permissions.Any(Permissions.Contains);

    public bool Manager => Has(Security.Permissions.AnalytikManage);
}

/// <summary>Решение о таблице: можно ли её читать и с каким ограничением строк (записи правила ScopeRule, подходящие уровню области).</summary>
public sealed record TableDecision(bool Allowed, string? Reason, string? Message, IReadOnlyList<ScopeEntry> Scope)
{
    public static TableDecision Allow(IReadOnlyList<ScopeEntry>? scope = null) => new(true, null, null, scope ?? []);

    public static TableDecision Deny(string reason, string message) => new(false, reason, message, []);
}

/// <summary>Запись правила ограничения строк: вид (branch, region, direction, market, agent, parent, ref), колонка, родительская таблица.</summary>
public sealed record ScopeEntry(string Kind, string Column, string? Parent = null);

/// <summary>
/// Права в аналитическом движке. Каждая таблица проверяется при каждом запросе: закрытая — отказ; без права модуля — отказ; данные продаж
/// у пользователя с неполной областью — только с ограничением строк (регион / агент), а таблица, которую нельзя ограничить, — отказ.
/// Ограничение строк — предикатом в SQL (CatalogRules.ScopeKinds), а не фильтром на странице.
/// </summary>
public sealed class AnalytikAccess(IAppDbContext db, SalesAccess sales, SalesOptions salesOptions)
{
    public async Task<AnalytikPrincipal> ResolveAsync(Guid userId, CancellationToken ct)
    {
        var permissions = await db.EffectivePermissionsAsync(userId, ct) ?? [];
        var scope = await sales.ScopeAsync(userId, ct);
        return new AnalytikPrincipal(userId, permissions, await RowScopeAsync(scope, ct));
    }

    internal async Task<SalesRowScope> RowScopeAsync(SalesScope scope, CancellationToken ct)
    {
        if (scope.IsOrganization)
        {
            return SalesRowScope.Organization;
        }

        if (scope.IsAgent)
        {
            var agent = scope.AgentId ?? -1;
            return new SalesRowScope(SalesRowScope.AgentLevel, [], [], [], agent, $"только свои продажи (ТП {agent})");
        }

        var regions = await db.SalesRegions.AsNoTracking()
            .Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName))
            .ToListAsync(ct);
        var (_, aliases) = OldBranches.Merge(regions, salesOptions.OldBranchSuffix);
        var allowed = regions.Where(r => scope.RegionIds.Contains(r.Id)).ToList();
        var branches = allowed.Select(r => r.BranchId)
            .Concat(aliases.Where(a => scope.RegionIds.Contains(a.Value)).Select(a => a.Key))
            .Distinct().Order().ToList();
        var directions = scope.DirectionIds.Concat(allowed.Select(r => r.DirectionId).OfType<Guid>()).Distinct().ToList();
        return new SalesRowScope(SalesRowScope.RegionLevel, branches, allowed.Select(r => r.Id).ToList(), directions, null,
            "регионы: " + (allowed.Count == 0 ? "нет" : string.Join(", ", allowed.Select(r => r.Name))));
    }

    /// <summary>Можно ли пользователю читать таблицу в движке и с каким ограничением строк.</summary>
    public static TableDecision Decide(CatalogTableInfo table, AnalytikPrincipal p)
    {
        if (table.Closed)
        {
            return TableDecision.Deny("closed",
                $"Таблица {table.FullName} закрыта для аналитики (секреты, сессии, личная переписка или файлы с правами по папкам).");
        }

        if (!p.System && !p.Has(Permissions.AnalytikUse))
        {
            return TableDecision.Deny("no_permission", "Нет права на раздел «Аналитик».");
        }

        if (table.Permissions.Count > 0 && !p.HasAny(table.Permissions))
        {
            return TableDecision.Deny("no_permission",
                $"Нет доступа к данным «{table.Title ?? table.FullName}»: нужно право {string.Join(" или ", table.Permissions)}.");
        }

        if (p.Sales.Restricted && SalesData(table))
        {
            var entries = Applicable(table.ScopeRule, p.Sales.Level);
            return entries is null
                ? TableDecision.Deny("scope",
                    $"Таблица {table.FullName} недоступна при вашей области продаж ({p.Sales.Description}): её строки нельзя ограничить вашими регионами.")
                : TableDecision.Allow(entries);
        }

        return TableDecision.Allow();
    }

    /// <summary>Данные продаж, финансов дилеров и полевой команды — ограничиваются областью продаж.</summary>
    internal static bool SalesData(CatalogTableInfo t) =>
        t.Module is "sales" or "finance" or "field" || t.Permissions.Any(p => p is Permissions.SalesRead or Permissions.FinanceRead);

    /// <summary>Записи правила, подходящие уровню области; null — таблицу нельзя ограничить (отказ); пусто — справочник без ограничения.</summary>
    internal static IReadOnlyList<ScopeEntry>? Applicable(string? rule, string level)
    {
        var entries = Parse(rule);
        if (entries.Any(e => e.Kind == "ref"))
        {
            return [];
        }

        var kinds = level == SalesRowScope.AgentLevel
            ? new[] { "agent", "parent" }
            : new[] { "branch", "region", "direction", "market", "parent" };
        var applicable = entries.Where(e => kinds.Contains(e.Kind)).ToList();
        return applicable.Count == 0 ? null : applicable;
    }

    public static IReadOnlyList<ScopeEntry> Parse(string? rule) =>
        string.IsNullOrWhiteSpace(rule)
            ? []
            : rule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part =>
                {
                    var kind = part.Split(':', 2)[0];
                    var rest = part.Contains(':') ? part.Split(':', 2)[1] : "";
                    var parent = rest.Contains('>') ? rest.Split('>', 2)[1] : null;
                    var column = rest.Contains('>') ? rest.Split('>', 2)[0] : rest;
                    return new ScopeEntry(kind, column, parent);
                })
                .ToList();
}

using OneBase.Application.Analytik.Query;

namespace OneBase.Application.Analytik.Catalog;

/// <summary>Таблица в списке «Карты данных»: описание, объём, доступ для этого пользователя и почему нет.</summary>
public sealed record CatalogTableRow(
    string Schema, string Name, string FullName, string Module, string? Title, string? Description, string DescriptionSource, long Rows, bool RowsExact,
    long SizeBytes, int Columns, string Access, string? RequiredPermission, string? DateColumn, bool Available, string? Unavailable, int Relations,
    DateTimeOffset FirstSeenAt, DateTimeOffset? ChangedAt);

public sealed record CatalogColumnRow(
    string Name, string DataType, bool Nullable, bool PrimaryKey, bool Indexed, bool Unique, string Role, string? Title, string? Description, string Sensitivity);

public sealed record CatalogRelationRow(
    string Direction, string From, string FromColumn, string To, string ToColumn, string Kind, string Status, decimal? MatchRatio, int? Checked,
    string? TargetFilter, string? OtherTitle);

public sealed record CatalogTableDetail(
    CatalogTableRow Table, IReadOnlyList<CatalogColumnRow> Columns, IReadOnlyList<CatalogRelationRow> Relations, IReadOnlyList<MetricKnowledge> Metrics,
    string? ScopeRule);

public sealed record CatalogModuleNode(string Module, int Tables, long Rows, int Available);

public sealed record CatalogModuleEdge(string From, string To, int Relations);

public sealed record CatalogGraph(IReadOnlyList<CatalogModuleNode> Modules, IReadOnlyList<CatalogModuleEdge> Edges, int Tables, int Relations,
    int ConfirmedRelations, DateTimeOffset? RefreshedAt);

/// <summary>Представления каталога для экрана и инструментов AI: что есть в базе и что из этого доступно пользователю.</summary>
public static class CatalogViews
{
    public static CatalogTableRow Row(CatalogModel model, CatalogTableInfo t, AnalytikPrincipal principal)
    {
        var decision = AnalytikAccess.Decide(t, principal);
        return new CatalogTableRow(t.Schema, t.Name, t.FullName, t.Module, t.Title, t.Description, t.DescriptionSource, t.Rows, t.RowsExact, t.SizeBytes,
            t.Columns.Count, t.Access, t.RequiredPermission, t.DateColumn, decision.Allowed, decision.Message,
            model.RelationsOf(t.FullName).Count(r => r.Status == RelationStatus.Confirmed), t.FirstSeenAt, t.ChangedAt);
    }

    public static IReadOnlyList<CatalogTableRow> Rows(CatalogModel model, AnalytikPrincipal principal, string? module, string? query, bool availableOnly)
    {
        IEnumerable<CatalogTableInfo> tables = model.Tables;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var hits = CatalogSearch.Search(model, query, module, 60, includeClosed: principal.Manager);
            var text = query.Trim();
            tables = hits.Select(h => h.Table)
                .Concat(model.Tables.Where(t => t.FullName.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || (t.Title ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)))
                .DistinctBy(t => t.FullName);
        }
        else if (module is not null)
        {
            tables = tables.Where(t => t.Module == module);
        }

        var rows = tables.Select(t => Row(model, t, principal));
        if (availableOnly)
        {
            rows = rows.Where(r => r.Available);
        }

        return string.IsNullOrWhiteSpace(query)
            ? rows.OrderBy(r => r.Module, StringComparer.Ordinal).ThenByDescending(r => r.Rows).ThenBy(r => r.FullName, StringComparer.Ordinal).ToList()
            : rows.ToList();
    }

    /// <summary>
    /// Описание таблицы: колонки, связи в обе стороны, показатели, в которых она участвует. Колонки закрытой таблицы видит только управляющий
    /// Аналитиком (сама структура — не данные, но имена служебных полей обычному пользователю не нужны).
    /// </summary>
    public static CatalogTableDetail? Detail(CatalogModel model, string fullName, AnalytikPrincipal principal)
    {
        if (model.Find(fullName) is not { } t)
        {
            return null;
        }

        var row = Row(model, t, principal);
        var columns = t.Closed && !principal.Manager
            ? []
            : t.Columns.Select(c => new CatalogColumnRow(c.Name, c.DataType, c.Nullable, c.IsPrimaryKey, c.Indexed, c.IsUnique, c.Role, c.Title, c.Description,
                c.Sensitivity)).ToList();
        var relations = model.RelationsOf(t.FullName)
            .Select(r => new CatalogRelationRow(r.From == t.FullName ? "out" : "in", r.From, r.FromColumn, r.To, r.ToColumn, r.Kind, r.Status, r.MatchRatio,
                r.Checked, r.TargetFilter, model.Find(r.From == t.FullName ? r.To : r.From)?.Title))
            .OrderBy(r => r.Direction, StringComparer.Ordinal).ThenBy(r => r.Status != RelationStatus.Confirmed).ThenBy(r => r.FromColumn, StringComparer.Ordinal)
            .ToList();
        var metrics = CatalogKnowledge.Metrics.Where(m => m.Tables.Contains(t.FullName, StringComparison.Ordinal)
            || m.Tables.Contains($"{t.Schema}.*", StringComparison.Ordinal)).ToList();
        return new CatalogTableDetail(row, columns, relations, metrics, t.ScopeRule);
    }

    /// <summary>Граф модулей: сколько таблиц и строк в каждом, сколько подтверждённых связей между модулями.</summary>
    public static CatalogGraph Graph(CatalogModel model, AnalytikPrincipal principal)
    {
        var modules = model.Tables.GroupBy(t => t.Module)
            .Select(g => new CatalogModuleNode(g.Key, g.Count(), g.Sum(t => t.Rows), g.Count(t => AnalytikAccess.Decide(t, principal).Allowed)))
            .OrderByDescending(m => m.Rows).ToList();
        var moduleOf = model.Tables.ToDictionary(t => t.FullName, t => t.Module, StringComparer.Ordinal);
        var edges = model.Relations.Where(r => r.Status == RelationStatus.Confirmed && moduleOf.ContainsKey(r.From) && moduleOf.ContainsKey(r.To))
            .Select(r => (From: moduleOf[r.From], To: moduleOf[r.To]))
            .Where(e => e.From != e.To)
            .GroupBy(e => string.CompareOrdinal(e.From, e.To) < 0 ? (e.From, e.To) : (e.To, e.From))
            .Select(g => new CatalogModuleEdge(g.Key.Item1, g.Key.Item2, g.Count()))
            .OrderByDescending(e => e.Relations).ToList();
        return new CatalogGraph(modules, edges, model.Tables.Count, model.Relations.Count, model.Relations.Count(r => r.Status == RelationStatus.Confirmed),
            model.RefreshedAt);
    }
}

using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik.Catalog;

/// <summary>Колонка каталога в памяти — то, что нужно движку запросов и описанию таблицы.</summary>
public sealed record CatalogColumnInfo(
    string Name, int Ordinal, string DataType, bool Nullable, bool IsPrimaryKey, bool Indexed, string Role, string? Title, string? Description,
    string Sensitivity, bool IsUnique = false)
{
    public bool Visible => Catalog.Sensitivity.Visible(Sensitivity);
}

/// <summary>Таблица каталога в памяти: структура, доступ, описание, колонки.</summary>
public sealed record CatalogTableInfo(
    string Schema, string Name, string Kind, string Module, string? Title, string? Description, string DescriptionSource, long Rows, bool RowsExact,
    long SizeBytes, string Access, string? RequiredPermission, string? ScopeRule, string? DateColumn, string? PrimaryKey,
    DateTimeOffset FirstSeenAt, DateTimeOffset? ChangedAt, IReadOnlyList<CatalogColumnInfo> Columns)
{
    public string FullName => $"{Schema}.{Name}";

    public bool Closed => Access == CatalogAccess.Closed;

    public CatalogColumnInfo? Column(string name) => Columns.FirstOrDefault(c => c.Name == name)
        ?? Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Права таблицы: любое из перечисленных (через «|»).</summary>
    public IReadOnlyList<string> Permissions => RequiredPermission is { Length: > 0 } p ? p.Split('|', StringSplitOptions.RemoveEmptyEntries) : [];
}

/// <summary>Связь каталога в памяти (без удалённых).</summary>
public sealed record CatalogRelationInfo(
    string FromSchema, string FromTable, string FromColumn, string ToSchema, string ToTable, string ToColumn, string Kind, string Status,
    decimal? MatchRatio, int? Checked, string? TargetFilter)
{
    public string From => $"{FromSchema}.{FromTable}";
    public string To => $"{ToSchema}.{ToTable}";

    /// <summary>По связи можно соединять таблицы: внешний ключ или подтверждённая данными, одна колонка с каждой стороны.</summary>
    public bool Joinable => Status == RelationStatus.Confirmed && !FromColumn.Contains(',') && !ToColumn.Contains(',');
}

public static class RelationStatus
{
    public const string Confirmed = "confirmed";
    public const string Unverified = "unverified";
    public const string Rejected = "rejected";
}

/// <summary>Каталог целиком в памяти: таблицы по полному имени и связи. Строится из analytik.* и кэшируется (CatalogCache).</summary>
public sealed class CatalogModel
{
    public CatalogModel(IReadOnlyList<CatalogTableInfo> tables, IReadOnlyList<CatalogRelationInfo> relations, DateTimeOffset? refreshedAt)
    {
        Tables = tables;
        Relations = relations;
        RefreshedAt = refreshedAt;
        _byName = tables.ToDictionary(t => t.FullName, StringComparer.Ordinal);
        _byLower = tables.GroupBy(t => t.FullName.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
    }

    private readonly Dictionary<string, CatalogTableInfo> _byName;
    private readonly Dictionary<string, CatalogTableInfo> _byLower;

    public IReadOnlyList<CatalogTableInfo> Tables { get; }
    public IReadOnlyList<CatalogRelationInfo> Relations { get; }
    public DateTimeOffset? RefreshedAt { get; }

    /// <summary>Таблица по имени «schema.table» (регистр — как в базе; без совпадения — без учёта регистра).</summary>
    public CatalogTableInfo? Find(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var name = fullName.Replace("\"", "").Trim();
        return _byName.GetValueOrDefault(name) ?? _byLower.GetValueOrDefault(name.ToLowerInvariant());
    }

    /// <summary>Связи таблицы: исходящие и входящие.</summary>
    public IEnumerable<CatalogRelationInfo> RelationsOf(string fullName) =>
        Relations.Where(r => r.From == fullName || r.To == fullName);

    public static CatalogModel From(IReadOnlyList<CatalogTable> tables, IReadOnlyList<CatalogRelation> relations, DateTimeOffset? refreshedAt) =>
        new(tables.Where(t => t.RemovedAt == null).Select(Info).ToList(),
            relations.Where(r => r.RemovedAt == null).Select(r => new CatalogRelationInfo(r.FromSchema, r.FromTable, r.FromColumn, r.ToSchema, r.ToTable,
                r.ToColumn, r.Kind, r.Status, r.MatchRatio, r.Checked, r.TargetFilter)).ToList(),
            refreshedAt);

    public static CatalogTableInfo Info(CatalogTable t) => new(t.Schema, t.Name, t.Kind, t.Module, t.Title, t.Description, t.DescriptionSource,
        t.RowCount ?? t.RowEstimate, t.RowCount is not null, t.SizeBytes, t.Access, t.RequiredPermission, t.ScopeRule, t.DateColumn, t.PrimaryKey,
        t.FirstSeenAt, t.ChangedAt,
        t.Columns.OrderBy(c => c.Ordinal).Select(c => new CatalogColumnInfo(c.Name, c.Ordinal, c.DataType, c.Nullable, c.IsPrimaryKey, c.Indexed, c.Role,
            c.Title, c.Description, c.Sensitivity, c.IsUnique)).ToList());
}

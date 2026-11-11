namespace OneBase.Application.Analytik.Catalog;

/// <summary>Связь-кандидат: откуда, куда, вид (fk, manual, inferred), имя ограничения и условие на цель.</summary>
public sealed record RelationCandidate(
    string FromSchema, string FromTable, string FromColumn, string ToSchema, string ToTable, string ToColumn, string Kind,
    string? ConstraintName = null, string? TargetFilter = null)
{
    public string Key => $"{FromSchema}.{FromTable}.{FromColumn}>{ToSchema}.{ToTable}.{ToColumn}";
}

public static class RelationKinds
{
    public const string ForeignKey = "fk";
    public const string Manual = "manual";
    public const string Inferred = "inferred";
}

/// <summary>
/// Связи между таблицами без обращения к базе: внешние ключи, связи из знаний OneBase и выведенные по имени колонки «…Id» (форма имени
/// таблицы, суффикс, префикс таблицы, «…ById» → пользователь OneBase, «Linko…Id» → таблица Linko). Тип ключа должен совпадать (целое ↔ целое,
/// uuid ↔ uuid); при нескольких одинаково подходящих таблицах связь не выводится — угадывать нельзя. Выведенные связи потом проверяются
/// данными (CatalogService).
/// </summary>
public static class RelationInference
{
    /// <summary>Основы «…Id», которые указывают на пользователя OneBase (uuid).</summary>
    private static readonly HashSet<string> OneBaseUserStems = new(StringComparer.Ordinal)
    {
        "Actor", "Author", "Owner", "OwnerUser", "Interviewer", "Recruiter", "AssignedUser", "FullAccessUser", "AuthorUser", "Head",
    };

    /// <summary>Основы «…Id» типа bigint, которые указывают на сотрудника Linko (ТП, супервайзер).</summary>
    private static readonly HashSet<string> LinkoUserStems = new(StringComparer.Ordinal) { "Agent", "ResponsibleAgent", "Supervisor", "LinkoUser" };

    public static IReadOnlyList<RelationCandidate> Build(IReadOnlyList<SchemaTable> tables)
    {
        var result = new List<RelationCandidate>();
        var byName = tables.ToDictionary(t => t.FullName, StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var t in tables)
        {
            foreach (var fk in t.ForeignKeys)
            {
                result.Add(new RelationCandidate(t.Schema, t.Name, string.Join(",", fk.Columns), fk.RefSchema, fk.RefTable, string.Join(",", fk.RefColumns),
                    RelationKinds.ForeignKey, fk.Name));
                foreach (var c in fk.Columns)
                {
                    covered.Add($"{t.FullName}.{c}");
                }
            }
        }

        foreach (var k in CatalogKnowledge.Relations)
        {
            var (fs, ft, fc) = Split(k.From);
            var (ts, tt, tc) = Split(k.To);
            if (byName.TryGetValue($"{fs}.{ft}", out var from) && from.Column(fc) is not null
                && byName.TryGetValue($"{ts}.{tt}", out var to) && to.Column(tc) is not null
                && !covered.Contains(k.From))
            {
                result.Add(new RelationCandidate(fs, ft, fc, ts, tt, tc, RelationKinds.Manual, null, k.TargetFilter));
                covered.Add(k.From);
            }
        }

        var keyed = tables.Where(t => t.PrimaryKey.Count == 1 && t.Column(t.PrimaryKey[0]) is { } pk && CatalogRules.IsKeyType(pk.DataType)).ToList();
        foreach (var t in tables)
        {
            foreach (var c in t.Columns)
            {
                if (c.Name.Length <= 2 || !c.Name.EndsWith("Id", StringComparison.Ordinal) || !CatalogRules.IsKeyType(c.DataType)
                    || covered.Contains($"{t.FullName}.{c.Name}") || (t.PrimaryKey.Count == 1 && t.PrimaryKey[0] == c.Name))
                {
                    continue;
                }

                if (Resolve(t, c.Name[..^2], c.DataType, keyed) is { } target)
                {
                    result.Add(new RelationCandidate(t.Schema, t.Name, c.Name, target.Schema, target.Name, target.PrimaryKey[0], RelationKinds.Inferred));
                }
            }
        }

        return result;
    }

    /// <summary>Целевая таблица для основы имени колонки; null — не найдена или неоднозначна.</summary>
    internal static SchemaTable? Resolve(SchemaTable owner, string stem, string type, IReadOnlyList<SchemaTable> keyed)
    {
        var family = Family(type);
        IReadOnlyList<SchemaTable> Find(IEnumerable<string> names, string? schema = null)
        {
            var set = names.ToHashSet(StringComparer.Ordinal);
            return keyed.Where(k => set.Contains(k.Name) && (schema is null || k.Schema == schema) && Family(k.Column(k.PrimaryKey[0])!.DataType) == family).ToList();
        }

        SchemaTable? Pick(IReadOnlyList<SchemaTable> found)
        {
            var same = found.Where(f => f.Schema == owner.Schema).ToList();
            return same.Count == 1 ? same[0] : same.Count == 0 && found.Count == 1 ? found[0] : null;
        }

        if (stem == "Parent")
        {
            return keyed.FirstOrDefault(k => k.Schema == owner.Schema && k.Name == owner.Name);
        }

        if (stem.StartsWith("Linko", StringComparison.Ordinal) && stem.Length > 5)
        {
            var rest = stem[5..];
            return Pick(Find([Plural(rest), rest], "linko"));
        }

        if (family == "uuid" && (stem.EndsWith("By", StringComparison.Ordinal) || stem.EndsWith("ByUser", StringComparison.Ordinal) || OneBaseUserStems.Contains(stem)))
        {
            return Pick(Find(["Users"], "public"));
        }

        if (family == "int" && LinkoUserStems.Contains(stem))
        {
            return Pick(Find(["Users"], "linko"));
        }

        // Точное имя, затем «таблица + основа» (Products.TypeId → ProductTypes), затем всё более короткие суффиксы основы (FromStock → Stocks).
        if (Pick(Find([Plural(stem), stem])) is { } exact)
        {
            return exact;
        }

        var prefixed = Singular(owner.Name) + Plural(stem);
        if (Pick(Find([prefixed])) is { } withOwner)
        {
            return withOwner;
        }

        var words = Words(stem);
        for (var i = 1; i < words.Count; i++)
        {
            var suffix = string.Concat(words.Skip(i));
            if (Pick(Find([Plural(suffix), suffix])) is { } bySuffix)
            {
                return bySuffix;
            }
        }

        return null;
    }

    private static string Family(string type) => type == "uuid" ? "uuid" : "int";

    internal static string Plural(string word) =>
        word.EndsWith('y') && word.Length > 1 && !"aeiou".Contains(word[^2]) ? word[..^1] + "ies"
        : word.EndsWith('s') || word.EndsWith('x') || word.EndsWith("sh", StringComparison.Ordinal) || word.EndsWith("ch", StringComparison.Ordinal) ? word + "es"
        : word + "s";

    internal static string Singular(string word) =>
        word.EndsWith("ies", StringComparison.Ordinal) ? word[..^3] + "y"
        : word.EndsWith("sses", StringComparison.Ordinal) || word.EndsWith("xes", StringComparison.Ordinal) || word.EndsWith("ches", StringComparison.Ordinal)
          || word.EndsWith("shes", StringComparison.Ordinal) || word.EndsWith("uses", StringComparison.Ordinal) ? word[..^2]
        : word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1]
        : word;

    private static List<string> Words(string pascal)
    {
        var words = new List<string>();
        var start = 0;
        for (var i = 1; i < pascal.Length; i++)
        {
            if (char.IsUpper(pascal[i]) && !char.IsUpper(pascal[i - 1]))
            {
                words.Add(pascal[start..i]);
                start = i;
            }
        }

        words.Add(pascal[start..]);
        return words;
    }

    /// <summary>«schema.table.column» → части.</summary>
    public static (string Schema, string Table, string Column) Split(string target)
    {
        var parts = target.Split('.', 3);
        return parts.Length == 3 ? (parts[0], parts[1], parts[2]) : throw new ArgumentException($"Ожидается schema.table.column: {target}");
    }
}

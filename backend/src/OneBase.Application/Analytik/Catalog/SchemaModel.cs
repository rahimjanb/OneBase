namespace OneBase.Application.Analytik.Catalog;

/// <summary>Структура базы, прочитанная из pg_catalog за один проход.</summary>
public sealed record SchemaSnapshot(IReadOnlyList<SchemaTable> Tables, DateTimeOffset ReadAt);

/// <summary>Таблица или представление: вид, оценка строк, размер, комментарий, колонки, первичный ключ, внешние ключи, индексированные колонки.</summary>
public sealed record SchemaTable(
    string Schema,
    string Name,
    string Kind,
    long RowEstimate,
    long SizeBytes,
    string? Comment,
    IReadOnlyList<SchemaColumn> Columns,
    IReadOnlyList<string> PrimaryKey,
    IReadOnlyList<SchemaForeignKey> ForeignKeys,
    IReadOnlySet<string> IndexedColumns,
    IReadOnlySet<string>? UniqueColumns = null)
{
    /// <summary>Колонка уникальна: первичный ключ из одной колонки или уникальный индекс из одной этой колонки.</summary>
    public bool IsUnique(string column) => (PrimaryKey.Count == 1 && PrimaryKey[0] == column) || UniqueColumns?.Contains(column) == true;

    public string FullName => $"{Schema}.{Name}";

    public SchemaColumn? Column(string name) => Columns.FirstOrDefault(c => c.Name == name);
}

/// <summary>Колонка: имя, порядок, тип (format_type), допускает ли NULL, комментарий.</summary>
public sealed record SchemaColumn(string Name, int Ordinal, string DataType, bool Nullable, string? Comment);

/// <summary>Внешний ключ: имя ограничения, колонки, целевая таблица и её колонки.</summary>
public sealed record SchemaForeignKey(string Name, IReadOnlyList<string> Columns, string RefSchema, string RefTable, IReadOnlyList<string> RefColumns);

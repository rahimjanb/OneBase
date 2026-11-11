using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Catalog;

namespace OneBase.Infrastructure.Analytik;

/// <summary>
/// Структура базы из pg_catalog — четыре запроса на всю базу (таблицы, колонки, ключи, индексы), без обращения к самим данным. Видны все
/// пользовательские схемы, кроме системных и временных; секции партиционированных таблиц — частью родителя.
/// </summary>
internal sealed class PostgresSchemaReader(IAnalytikSql sql, TimeProvider clock) : ISchemaReader
{
    private const string UserSchemas =
        "n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\\_toast%' AND n.nspname NOT LIKE 'pg\\_temp%'";

    private static readonly SqlLimits Limits = new(200_000, 30);

    public async Task<SchemaSnapshot> ReadAsync(CancellationToken ct)
    {
        var tables = await sql.QueryAsync($"""
            SELECT n.nspname, c.relname, c.relkind::text,
                   greatest(c.reltuples, coalesce(s.n_live_tup, 0), 0)::bigint,
                   CASE WHEN c.relkind IN ('r', 'p', 'm') THEN pg_total_relation_size(c.oid) ELSE 0 END,
                   obj_description(c.oid, 'pg_class')
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_stat_user_tables s ON s.relid = c.oid
            WHERE c.relkind IN ('r', 'p', 'v', 'm') AND NOT c.relispartition AND {UserSchemas}
            """, [], Limits, ct);

        var columns = await sql.QueryAsync($"""
            SELECT n.nspname, c.relname, a.attname, a.attnum::int, format_type(a.atttypid, a.atttypmod), NOT a.attnotnull, col_description(c.oid, a.attnum)
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE a.attnum > 0 AND NOT a.attisdropped AND c.relkind IN ('r', 'p', 'v', 'm') AND NOT c.relispartition AND {UserSchemas}
            ORDER BY 1, 2, 4
            """, [], Limits, ct);

        var constraints = await sql.QueryAsync($"""
            SELECT n.nspname, c.relname, con.conname, con.contype::text,
                   ARRAY(SELECT a.attname::text FROM unnest(con.conkey) WITH ORDINALITY k(attnum, ord)
                         JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum ORDER BY k.ord),
                   rn.nspname, rc.relname,
                   ARRAY(SELECT a.attname::text FROM unnest(con.confkey) WITH ORDINALITY k(attnum, ord)
                         JOIN pg_attribute a ON a.attrelid = con.confrelid AND a.attnum = k.attnum ORDER BY k.ord)
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_class rc ON rc.oid = con.confrelid
            LEFT JOIN pg_namespace rn ON rn.oid = rc.relnamespace
            WHERE con.contype IN ('p', 'f') AND {UserSchemas}
            """, [], Limits, ct);

        var indexes = await sql.QueryAsync($"""
            SELECT n.nspname, c.relname, a.attname, bool_or(i.indisunique AND i.indnkeyatts = 1 AND i.indpred IS NULL)
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = i.indkey[0]
            WHERE {UserSchemas}
            GROUP BY 1, 2, 3
            """, [], Limits, ct);

        var columnsBy = columns.Rows
            .GroupBy(r => Key((string)r[0]!, (string)r[1]!))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SchemaColumn>)g
                .Select(r => new SchemaColumn((string)r[2]!, Convert.ToInt32(r[3]), (string)r[4]!, (bool)r[5]!, r[6] as string))
                .OrderBy(c => c.Ordinal).ToList());
        var pks = constraints.Rows.Where(r => (string)r[3]! == "p")
            .GroupBy(r => Key((string)r[0]!, (string)r[1]!))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)((string[])g.First()[4]!).ToList());
        var fks = constraints.Rows.Where(r => (string)r[3]! == "f" && r[5] is string && r[6] is string)
            .GroupBy(r => Key((string)r[0]!, (string)r[1]!))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SchemaForeignKey>)g
                .Select(r => new SchemaForeignKey((string)r[2]!, (string[])r[4]!, (string)r[5]!, (string)r[6]!, (string[])r[7]!)).ToList());
        var indexed = indexes.Rows
            .GroupBy(r => Key((string)r[0]!, (string)r[1]!))
            .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(r => (string)r[2]!).ToHashSet(StringComparer.Ordinal));
        var unique = indexes.Rows.Where(r => r[3] is true)
            .GroupBy(r => Key((string)r[0]!, (string)r[1]!))
            .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(r => (string)r[2]!).ToHashSet(StringComparer.Ordinal));

        var result = tables.Rows.Select(r =>
        {
            var key = Key((string)r[0]!, (string)r[1]!);
            return new SchemaTable(
                (string)r[0]!,
                (string)r[1]!,
                KindOf((string)r[2]!),
                Convert.ToInt64(r[3]),
                Convert.ToInt64(r[4]),
                r[5] as string,
                columnsBy.GetValueOrDefault(key) ?? [],
                pks.GetValueOrDefault(key) ?? [],
                fks.GetValueOrDefault(key) ?? [],
                indexed.GetValueOrDefault(key) ?? new HashSet<string>(),
                unique.GetValueOrDefault(key));
        }).OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();

        return new SchemaSnapshot(result, clock.GetUtcNow());
    }

    private static string Key(string schema, string table) => $"{schema}.{table}";

    private static string KindOf(string relkind) => relkind switch
    {
        "r" => "table",
        "p" => "partitioned",
        "v" => "view",
        "m" => "matview",
        _ => relkind,
    };
}

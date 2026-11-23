using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OneBase.Application.Analytik.Catalog;
using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik.Query;

/// <summary>Колонка результата свободного запроса: имя и тип PostgreSQL.</summary>
public sealed record SqlColumn(string Name, string Type);

/// <summary>Результат свободного запроса: колонки, строки (значения в JSON-виде), есть ли ещё строки, прочитанные таблицы, время.</summary>
public sealed record SqlQueryView(
    Guid QueryId, IReadOnlyList<SqlColumn> Columns, IReadOnlyList<object?[]> Rows, bool HasMore, IReadOnlyList<string> Tables, long ElapsedMs,
    DateTimeOffset At);

/// <summary>
/// Итог свободного запроса: ok, denied, invalid, too_heavy, timeout, error — и текст, безопасный для пользователя и модели. Access — отказ по правам
/// (роль, область продаж): переписыванием запроса его не обойти. Остальные отказы (скрытая колонка, функция, системная таблица) исправляются в запросе.
/// </summary>
public sealed record SqlOutcome(Guid QueryId, string Status, string? Error, SqlQueryView? Result, bool Access = false)
{
    public bool Ok => Status == "ok";
}

/// <summary>Отказ свободного запроса по правам пользователя на таблицу (SqlOutcome.Access).</summary>
internal sealed class SqlAccessDeniedException(string message) : Exception(message);

/// <summary>
/// Текст свободного запроса до PostgreSQL: один SELECT или WITH без комментариев и точки с запятой в середине, без экранированных строк
/// (E'…', U&amp;'…', $$…$$), без функций, читающих файлы, большие объекты, настройки или другие запросы (query_to_xml и т.п.). Разбор — тем же
/// порядком, что у PostgreSQL: строки, идентификаторы в кавычках, вложенные комментарии. Проверка до EXPLAIN нужна потому, что планировщик
/// может вычислить «стабильную» функцию с константами ещё при оценке плана.
/// </summary>
public static partial class SqlGuard
{
    public const int MaxLength = 20_000;

    /// <summary>Готовый к выполнению текст (без комментариев и завершающей «;») или отказ QueryPlanException.</summary>
    public static string Clean(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw Invalid("Пустой запрос: нужен один SELECT или WITH.");
        }

        if (sql.Length > MaxLength)
        {
            throw Invalid($"Запрос длиннее {MaxLength} символов — разбейте его на шаги.");
        }

        var exec = new StringBuilder(sql.Length);
        // skeleton — без строк, с именами в кавычках (для поиска функций: "query_to_xml"(…) — тоже вызов); bare — и без имён (для команд).
        var skeleton = new StringBuilder(sql.Length);
        var bare = new StringBuilder(sql.Length);
        var semicolon = false;
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';
            if (c == '-' && next == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }

                exec.Append(' ');
                skeleton.Append(' ');
                bare.Append(' ');
                continue;
            }

            if (c == '/' && next == '*')
            {
                var depth = 0;
                while (i < sql.Length)
                {
                    if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                    {
                        depth++;
                        i += 2;
                    }
                    else if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                    {
                        depth--;
                        i += 2;
                        if (depth == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        i++;
                    }
                }

                if (depth != 0)
                {
                    throw Invalid("Комментарий /* … */ не закрыт.");
                }

                exec.Append(' ');
                skeleton.Append(' ');
                bare.Append(' ');
                continue;
            }

            if (semicolon && !char.IsWhiteSpace(c))
            {
                throw Invalid("Только один запрос: после «;» ничего не должно быть.");
            }

            if (c == ';')
            {
                semicolon = true;
                i++;
                continue;
            }

            if (c == '$')
            {
                throw Invalid("Символ $ не поддерживается (параметры и строки $$…$$): пишите значения прямо в запросе в одинарных кавычках.");
            }

            if (c == '\'')
            {
                var prev = i > 0 ? sql[i - 1] : ' ';
                var before = i > 1 ? sql[i - 2] : ' ';
                if ((prev is 'E' or 'e') && !IsIdentChar(before))
                {
                    throw Invalid("Строки E'…' (с обратной косой чертой) не поддерживаются — используйте обычные '…'.");
                }

                if (prev == '&' && before is 'U' or 'u')
                {
                    throw Invalid("Строки U&'…' не поддерживаются.");
                }

                var start = i;
                i++;
                while (true)
                {
                    if (i >= sql.Length)
                    {
                        throw Invalid("Строка в одинарных кавычках не закрыта.");
                    }

                    if (sql[i] == '\'')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '\'')
                        {
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    i++;
                }

                exec.Append(sql, start, i - start);
                skeleton.Append("''");
                bare.Append("''");
                continue;
            }

            if (c == '"')
            {
                if (i > 1 && sql[i - 1] == '&' && sql[i - 2] is 'U' or 'u')
                {
                    throw Invalid("Идентификаторы U&\"…\" не поддерживаются.");
                }

                var start = i;
                var name = new StringBuilder();
                i++;
                while (true)
                {
                    if (i >= sql.Length)
                    {
                        throw Invalid("Имя в двойных кавычках не закрыто.");
                    }

                    if (sql[i] == '"')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '"')
                        {
                            name.Append('"');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    name.Append(sql[i]);
                    i++;
                }

                exec.Append(sql, start, i - start);
                skeleton.Append(' ').Append(name).Append(' ');
                bare.Append(" x ");
                continue;
            }

            exec.Append(c);
            skeleton.Append(c);
            bare.Append(c);
            i++;
        }

        var text = exec.ToString().Trim();
        var shape = skeleton.ToString().Trim().ToLowerInvariant();
        var plain = bare.ToString().Trim().ToLowerInvariant();
        if (text.Length == 0)
        {
            throw Invalid("Пустой запрос: нужен один SELECT или WITH.");
        }

        if (!StartsLikeQuery().IsMatch(plain))
        {
            throw Invalid("Разрешён только запрос на чтение: SELECT … или WITH … SELECT ….");
        }

        if (Forbidden().Match(shape) is { Success: true } f)
        {
            throw Denied($"Функция или команда «{f.Groups[1].Value.Trim()}» в запросе к данным не разрешена: она читает файлы, настройки, большие " +
                         "объекты, другие запросы или меняет состояние базы.");
        }

        if (Writes().Match(plain) is { Success: true } w)
        {
            throw Denied($"«{w.Value.Trim().ToUpperInvariant()}» — запрос только на чтение: без INTO, FOR UPDATE, COPY и изменения данных.");
        }

        return text;
    }

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '"' or '$';

    private static QueryPlanException Invalid(string message) => new(QueryErrors.Invalid, message);

    private static QueryPlanException Denied(string message) => new(QueryErrors.Denied, message);

    [GeneratedRegex(@"^\(*\s*(select|with)\b")]
    private static partial Regex StartsLikeQuery();

    [GeneratedRegex(@"\b(pg_[a-z0-9_]*|query_to_xml[a-z_]*|table_to_xml[a-z_]*|schema_to_xml[a-z_]*|database_to_xml[a-z_]*|cursor_to_xml[a-z_]*|ts_stat|ts_rewrite|dblink[a-z_]*|lo_[a-z_]+|loread|lowrite|set_config|current_setting|nextval|setval|currval|txid_[a-z_]*|xmlparse|xpath[a-z_]*|inet_server_[a-z_]*|inet_client_[a-z_]*|version|current_user|session_user|current_role|user)\s*\(")]
    private static partial Regex Forbidden();

    [GeneratedRegex(@"\b(into|copy|lock|insert|update|delete|merge|truncate|alter|drop|create|grant|revoke|vacuum|analyze|call|do|execute|prepare|listen|notify|refresh)\b(?!\s*\()")]
    private static partial Regex Writes();
}

/// <summary>Что читает запрос по плану EXPLAIN (VERBOSE): таблицы с псевдонимами, функции и колонки по таблицам, стоимость.</summary>
public sealed record SqlPlanFacts(
    IReadOnlyList<(string Schema, string Table, string Alias)> Relations, IReadOnlyList<string> Functions, IReadOnlyList<string> NodeTypes,
    IReadOnlyList<(string Alias, IReadOnlyList<string> Texts)> ScanTexts, IReadOnlyList<string> AllTexts, double Cost);

/// <summary>
/// Разбор EXPLAIN (VERBOSE, FORMAT JSON): после разбора PostgreSQL имена функций и колонок нормализованы (кавычки, регистр, U&amp;-escape уже
/// раскрыты), а представления и CTE раскрыты до таблиц. Отсюда — проверка таблиц по каталогу и правам, скрытых колонок и белого списка функций.
/// </summary>
public static partial class SqlPlanInspector
{
    /// <summary>Поля узла плана, которые не выражения (имена, виды узлов).</summary>
    private static readonly HashSet<string> NotExpressions = new(StringComparer.Ordinal)
    {
        "Node Type", "Relation Name", "Schema", "Alias", "Index Name", "Strategy", "Join Type", "Parent Relationship", "Subplan Name", "CTE Name",
        "Scan Direction", "Partial Mode", "Command", "Sort Method", "Operation", "Function Name", "Async Capable", "Inner Unique", "Parallel Aware",
        "Single Copy", "Sort Space Type", "Hash Agg Batches", "Disk Usage", "Peak Memory Usage", "Workers Planned", "Workers Launched",
    };

    /// <summary>Функции и выражения, разрешённые в запросе: агрегаты, оконные, математика, даты, строки, массивы, JSON, приведения типов.</summary>
    public static readonly IReadOnlySet<string> AllowedFunctions = new HashSet<string>(StringComparer.Ordinal)
    {
        // агрегаты и статистика
        "count", "sum", "avg", "min", "max", "stddev", "stddev_pop", "stddev_samp", "variance", "var_pop", "var_samp", "corr", "covar_pop",
        "covar_samp", "regr_slope", "regr_intercept", "regr_r2", "regr_count", "regr_avgx", "regr_avgy", "regr_sxx", "regr_syy", "regr_sxy",
        "percentile_cont", "percentile_disc", "mode", "string_agg", "array_agg", "bool_and", "bool_or", "every", "json_agg", "jsonb_agg",
        "json_object_agg", "jsonb_object_agg", "bit_and", "bit_or", "any_value",
        // оконные
        "row_number", "rank", "dense_rank", "percent_rank", "cume_dist", "ntile", "lag", "lead", "first_value", "last_value", "nth_value", "over",
        // математика
        "abs", "round", "trunc", "ceil", "ceiling", "floor", "sqrt", "cbrt", "power", "pow", "exp", "ln", "log", "log10", "mod", "sign", "div",
        "width_bucket", "scale", "pi", "degrees", "radians", "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "factorial", "gcd", "lcm",
        "random", "greatest", "least", "coalesce", "nullif",
        // даты
        "date_trunc", "date_part", "extract", "age", "now", "make_date", "make_time", "make_timestamp", "make_interval", "to_char", "to_date",
        "to_timestamp", "to_number", "timezone", "date_bin", "justify_days", "justify_hours", "justify_interval", "isfinite", "clock_timestamp",
        "statement_timestamp", "transaction_timestamp", "date",
        // строки
        "lower", "upper", "initcap", "length", "char_length", "character_length", "octet_length", "substring", "substr", "position", "strpos",
        "trim", "btrim", "ltrim", "rtrim", "replace", "translate", "split_part", "concat", "concat_ws", "format", "left", "right", "lpad", "rpad",
        "repeat", "reverse", "regexp_replace", "regexp_match", "regexp_matches", "regexp_split_to_array", "regexp_split_to_table", "regexp_like",
        "regexp_count", "regexp_substr", "string_to_array", "string_to_table", "array_to_string", "starts_with", "md5", "chr", "ascii",
        "quote_literal", "quote_ident", "normalize", "overlay",
        // массивы и множества
        "array", "array_length", "cardinality", "array_position", "array_positions", "array_remove", "array_append", "array_prepend", "array_cat",
        "array_upper", "array_lower", "array_dims", "array_ndims", "unnest", "generate_series", "generate_subscripts", "row", "exists", "any",
        "all", "some",
        // JSON
        "json_build_object", "jsonb_build_object", "json_build_array", "jsonb_build_array", "to_json", "to_jsonb", "row_to_json",
        "jsonb_array_elements", "json_array_elements", "jsonb_array_elements_text", "json_array_elements_text", "jsonb_each", "json_each",
        "jsonb_each_text", "json_each_text", "jsonb_extract_path_text", "json_extract_path_text", "jsonb_extract_path", "json_extract_path",
        "jsonb_typeof", "json_typeof", "jsonb_array_length", "json_array_length", "jsonb_object_keys", "json_object_keys", "jsonb_strip_nulls",
        // приведения типов
        "numeric", "decimal", "int2", "int4", "int8", "integer", "bigint", "smallint", "float4", "float8", "text", "varchar", "varying", "char",
        "character", "bpchar", "bool", "boolean", "timestamp", "timestamptz", "time", "timetz", "interval", "bit", "cast", "uuid",
    };

    public static SqlPlanFacts Read(string explainJson)
    {
        using var doc = JsonDocument.Parse(explainJson);
        var root = doc.RootElement[0].GetProperty("Plan");
        var relations = new List<(string, string, string)>();
        var functions = new List<string>();
        var nodeTypes = new List<string>();
        var scanTexts = new List<(string, IReadOnlyList<string>)>();
        var all = new List<string>();
        Walk(root);
        return new SqlPlanFacts(relations, functions.Distinct().ToList(), nodeTypes.Distinct().ToList(), scanTexts, all,
            root.TryGetProperty("Total Cost", out var cost) ? cost.GetDouble() : 0);

        void Walk(JsonElement node)
        {
            var texts = new List<string>();
            foreach (var p in node.EnumerateObject())
            {
                if (p.Name == "Plans")
                {
                    continue;
                }

                if (NotExpressions.Contains(p.Name))
                {
                    continue;
                }

                Collect(p.Value, texts);
            }

            all.AddRange(texts);
            nodeTypes.Add(node.TryGetProperty("Node Type", out var t) ? t.GetString() ?? "" : "");
            if (node.TryGetProperty("Relation Name", out var rel))
            {
                var schema = node.TryGetProperty("Schema", out var s) ? s.GetString() ?? "" : "";
                var alias = node.TryGetProperty("Alias", out var a) ? a.GetString() ?? rel.GetString()! : rel.GetString()!;
                relations.Add((schema, rel.GetString()!, alias));
                scanTexts.Add((alias, texts));
            }

            if (node.TryGetProperty("Function Name", out var fn))
            {
                var schema = node.TryGetProperty("Schema", out var s) ? s.GetString() : "pg_catalog";
                functions.Add(schema is null or "pg_catalog" ? fn.GetString()! : $"{schema}.{fn.GetString()}");
            }

            foreach (var text in texts)
            {
                foreach (Match m in FunctionCall().Matches(Strip(text)))
                {
                    functions.Add(m.Groups[1].Value);
                }
            }

            if (node.TryGetProperty("Plans", out var plans))
            {
                foreach (var child in plans.EnumerateArray())
                {
                    Walk(child);
                }
            }
        }
    }

    private static void Collect(JsonElement value, List<string> into)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                into.Add(value.GetString()!);
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    Collect(item, into);
                }

                break;
            case JsonValueKind.Object:
                foreach (var p in value.EnumerateObject())
                {
                    Collect(p.Value, into);
                }

                break;
        }
    }

    /// <summary>Выражение без строковых констант ('…' → '') и с именами в кавычках, заменёнными на x, в нижнем регистре — для поиска функций.</summary>
    internal static string Strip(string text) => QuotedName().Replace(Literal().Replace(text, "''"), "x").ToLowerInvariant();

    /// <summary>Выражение без строковых констант — для поиска колонок.</summary>
    internal static string NoLiterals(string text) => Literal().Replace(text, "''");

    /// <summary>Колонки, на которые ссылается выражение: «alias.col», «alias."Col"», а у узла одной таблицы — и без псевдонима.</summary>
    public static IEnumerable<(string? Alias, string Column)> ColumnRefs(string text)
    {
        foreach (Match m in ColumnRef().Matches(NoLiterals(text)))
        {
            var alias = m.Groups["qa"].Success ? m.Groups["qa"].Value.Replace("\"\"", "\"") : m.Groups["ba"].Success ? m.Groups["ba"].Value : null;
            var column = m.Groups["qc"].Success ? m.Groups["qc"].Value.Replace("\"\"", "\"") : m.Groups["bc"].Value;
            yield return (alias, column);
        }
    }

    [GeneratedRegex(@"'(?:[^']|'')*'")]
    private static partial Regex Literal();

    [GeneratedRegex(@"""(?:[^""]|"""")*""")]
    private static partial Regex QuotedName();

    [GeneratedRegex(@"(?<!\w)([a-z_][a-z0-9_]*)\(")]
    private static partial Regex FunctionCall();

    [GeneratedRegex(@"(?<![\w"".])(?:(?:""(?<qa>(?:[^""]|"""")+)""|(?<ba>[A-Za-z_][A-Za-z0-9_]*))\.)?(?:""(?<qc>(?:[^""]|"""")+)""|(?<bc>[A-Za-z_][A-Za-z0-9_]*|\*))")]
    private static partial Regex ColumnRef();
}

/// <summary>
/// Свободный запрос на чтение (analytik_sql): модель пишет один SELECT/WITH с оконными функциями, CTE и соединениями — то, чего нет в
/// типизированном плане (цепочки «что было после каждого заказа», интервалы, ранги, сравнение строки с соседней). Порядок: текст
/// (SqlGuard) → EXPLAIN (VERBOSE) → каждая таблица — из каталога и с правом пользователя (AnalytikAccess), закрытых таблиц и скрытых колонок
/// (секреты, персональные данные) нет, целая строка таблицы со скрытыми колонками не читается, функции — из белого списка, данные продаж при
/// неполной области — отказ (их строки ограничивает только типизированный план) → оценка стоимости → выполнение READ ONLY со statement_timeout
/// → журнал analytik.Queries (и отказы).
/// </summary>
public sealed class SqlQueryService(
    IAnalytikDb db,
    CatalogService catalog,
    QueryEngine engine,
    IAnalytikSql sql,
    AnalytikOptions options,
    TimeProvider clock,
    ILogger<SqlQueryService> logger)
{
    public const int DefaultRows = 200;

    public async Task<SqlOutcome> RunAsync(string? text, int? maxRows, QueryCaller caller, CancellationToken ct, AnalytikPrincipal? principal = null)
    {
        var watch = Stopwatch.StartNew();
        var log = new AnalytikQuery
        {
            UserId = caller.UserId, Tenant = options.Tenant, Origin = caller.Origin, AgentCode = caller.AgentCode, ConversationId = caller.ConversationId,
            Plan = JsonSerializer.Serialize(new { sql = text }), Sql = text, Status = "ok",
        };

        try
        {
            principal ??= await engine.PrincipalAsync(caller, ct);
            var clean = SqlGuard.Clean(text);
            var limit = Math.Clamp(maxRows ?? DefaultRows, 1, Math.Max(1, options.MaxRows));
            var wrapped = $"SELECT * FROM (\n{clean}\n) AS onebase_sql LIMIT {limit + 1}";
            log.Sql = wrapped;

            SqlResult explain;
            try
            {
                explain = await sql.QueryAsync("EXPLAIN (VERBOSE, FORMAT JSON) " + wrapped, [], new SqlLimits(1, 10), ct);
            }
            catch (AnalytikSqlException ex)
            {
                throw new QueryPlanException(QueryErrors.Invalid, $"PostgreSQL не принял запрос: {ex.Message}. Исправьте запрос и повторите.");
            }

            var facts = SqlPlanInspector.Read(explain.Rows.FirstOrDefault()?[0]?.ToString() ?? "[{\"Plan\":{}}]");
            var model = await catalog.ModelAsync(ct);
            var tables = Check(facts, model, principal);
            log.Tables = string.Join(",", tables.Select(t => t.FullName).Distinct());
            if (facts.Cost > options.MaxSqlCost)
            {
                return await FailAsync(log, watch, QueryErrors.TooHeavy,
                    $"Запрос слишком тяжёлый (оценка PostgreSQL {facts.Cost:N0} при пределе {options.MaxSqlCost:N0}): ограничьте период условием по дате, " +
                    "сгруппируйте раньше (в CTE) или соединяйте по индексированным колонкам.", ct);
            }

            SqlResult result;
            try
            {
                result = await sql.QueryAsync(wrapped, [], new SqlLimits(limit, options.SqlTimeoutSeconds), ct);
            }
            catch (AnalytikSqlException ex)
            {
                throw new QueryPlanException(QueryErrors.Invalid, $"Запрос не выполнен: {ex.Message}. Исправьте запрос и повторите.");
            }

            var rows = result.Rows.Select(r => r.Select(QueryEngine.Normalize).ToArray()).ToList();
            log.RowCount = rows.Count;
            log.Truncated = result.Truncated;
            log.DurationMs = watch.ElapsedMilliseconds;
            await SaveAsync(log, ct);
            return new SqlOutcome(log.Id, "ok", null, new SqlQueryView(log.Id,
                result.Columns.Select((c, i) => new SqlColumn(c, i < result.Types.Count ? result.Types[i] : "")).ToList(), rows, result.Truncated,
                tables.Select(t => t.FullName).Distinct().ToList(), result.ElapsedMs, clock.GetUtcNow()));
        }
        catch (QueryPlanException ex)
        {
            return await FailAsync(log, watch, ex.Code, ex.Message, ct);
        }
        catch (SqlAccessDeniedException ex)
        {
            return await FailAsync(log, watch, QueryErrors.Denied, ex.Message, ct, access: true);
        }
        catch (AnalytikTimeoutException ex)
        {
            return await FailAsync(log, watch, "timeout",
                $"{ex.Message} Самое медленное — подзапрос на каждую строку (SELECT … WHERE x.a = c.a внутри SELECT или EXISTS): замените его соединением " +
                "с заранее сгруппированным CTE или оконной функцией (count(*) OVER, lead/lag по событиям в одном потоке). Ещё — ограничьте период по дате " +
                "и группируйте раньше.", ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return await FailAsync(log, watch, QueryErrors.Denied, ex.Message, ct, access: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: свободный запрос не выполнен");
            return await FailAsync(log, watch, "error",
                "PostgreSQL не довёл запрос до конца (сбой исполнения, не права и не данные). Упростите запрос — меньше подзапросов на строку, группировка " +
                "раньше, короче период — и выполните снова.", ct);
        }
    }

    /// <summary>Таблицы, функции и колонки плана против каталога и прав; отказ — QueryPlanException с понятной причиной.</summary>
    internal static IReadOnlyList<CatalogTableInfo> Check(SqlPlanFacts facts, CatalogModel model, AnalytikPrincipal principal)
    {
        if (facts.NodeTypes.Any(t => t is "ModifyTable" or "LockRows" or "Foreign Scan" or "Custom Scan"))
        {
            throw new QueryPlanException(QueryErrors.Denied, "Запрос только на чтение: без изменения данных, блокировок и внешних таблиц.");
        }

        if (facts.Functions.FirstOrDefault(f => !SqlPlanInspector.AllowedFunctions.Contains(f)) is { } fn)
        {
            throw new QueryPlanException(QueryErrors.Denied,
                $"Функция «{fn}» в запросе не разрешена. Можно: агрегаты (sum, avg, count, percentile_cont, corr…), оконные (lag, lead, row_number, " +
                "rank…), даты (date_trunc, extract, age…), строки, математика, массивы, JSON.");
        }

        var tables = new List<CatalogTableInfo>();
        var byAlias = new Dictionary<string, CatalogTableInfo>(StringComparer.Ordinal);
        foreach (var (schema, name, alias) in facts.Relations)
        {
            var table = model.Find($"{schema}.{name}")
                ?? throw new QueryPlanException(QueryErrors.Denied,
                    $"Таблица {schema}.{name} не открыта для запросов (её нет в каталоге Аналитика: системная или служебная).");
            var decision = AnalytikAccess.Decide(table, principal);
            if (!decision.Allowed)
            {
                throw new SqlAccessDeniedException(decision.Message ?? $"Нет доступа к {table.FullName}.");
            }

            if (decision.Scope.Count > 0)
            {
                throw new SqlAccessDeniedException(
                    $"Свободный запрос к {table.FullName} — только при доступе ко всей организации; у вас — {principal.Sales.Description}. " +
                    "Для своей области возьмите analytik_query или analytics_query: ограничение строк ставится само.");
            }

            tables.Add(table);
            byAlias[alias] = table;
        }

        // Скрытые колонки: в выражениях узла таблицы (там колонки бывают и без псевдонима) и в любых выражениях плана с псевдонимом таблицы.
        foreach (var (alias, texts) in facts.ScanTexts)
        {
            if (!byAlias.TryGetValue(alias, out var table))
            {
                continue;
            }

            foreach (var (refAlias, column) in texts.SelectMany(SqlPlanInspector.ColumnRefs))
            {
                if (refAlias is null || refAlias == alias)
                {
                    Guard(table, column);
                }
            }
        }

        foreach (var (refAlias, column) in facts.AllTexts.SelectMany(SqlPlanInspector.ColumnRefs))
        {
            if (refAlias is not null && byAlias.TryGetValue(refAlias, out var table))
            {
                Guard(table, column);
            }
        }

        return tables;
    }

    private static void Guard(CatalogTableInfo table, string column)
    {
        if (column == "*")
        {
            if (table.Columns.Any(c => !c.Visible))
            {
                throw new QueryPlanException(QueryErrors.Denied,
                    $"Целая строка {table.FullName} (t.*, to_json(t)) не читается: в таблице есть скрытые колонки ({string.Join(", ", table.Columns.Where(c => !c.Visible).Select(c => c.Name))}). " +
                    $"Перечислите нужные колонки. {VisibleColumns(table)}");
            }

            return;
        }

        var info = table.Columns.FirstOrDefault(c => c.Name == column);
        if (info is null)
        {
            // Без псевдонима это может быть имя функции, типа или колонки другой таблицы — проверяем только известные колонки.
            return;
        }

        if (!info.Visible)
        {
            throw new QueryPlanException(QueryErrors.Denied,
                $"Колонка {table.FullName}.{info.Name} скрыта ({(info.Sensitivity == Sensitivity.Secret ? "секрет" : info.Sensitivity == Sensitivity.Personal ? "персональные данные" : "служебная")}) — " +
                $"её нельзя читать, фильтровать и соединять в запросе. Запрет только на эту колонку, не на запрос: перепишите без неё. {VisibleColumns(table)}");
        }
    }

    private static string VisibleColumns(CatalogTableInfo table)
    {
        var visible = table.Columns.Where(c => c.Visible).Select(c => c.Name).ToList();
        return visible.Count == 0 ? "" : $"Видимые колонки {table.FullName}: {string.Join(", ", visible.Take(40))}{(visible.Count > 40 ? ", …" : "")}.";
    }

    private async Task<SqlOutcome> FailAsync(AnalytikQuery log, Stopwatch watch, string status, string message, CancellationToken ct, bool access = false)
    {
        log.Status = status;
        log.Error = message.Length <= 2000 ? message : message[..2000];
        log.DurationMs = watch.ElapsedMilliseconds;
        await SaveAsync(log, ct);
        return new SqlOutcome(log.Id, status, message, null, access);
    }

    private async Task SaveAsync(AnalytikQuery log, CancellationToken ct)
    {
        try
        {
            db.Queries.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: свободный запрос не записан в журнал");
        }
    }
}

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.Application.Analytik.Catalog;

namespace OneBase.Application.Analytik.Query;

/// <summary>Пределы результата: строк списка и групп агрегата.</summary>
public sealed record QueryLimits(int MaxRows, int MaxGroups);

/// <summary>Колонка результата: подпись, вид (column, measure, bucket), откуда (schema.table.column или формула), бизнес-роль, тип.</summary>
public sealed record OutputColumn(string Name, string Kind, string Source, string Role, string DataType);

/// <summary>Построенный запрос: SQL (только SELECT, значения — параметрами), таблицы, колонки результата, ограничение строк, заметки.</summary>
public sealed record BuiltQuery(
    string Sql, IReadOnlyList<SqlParam> Parameters, IReadOnlyList<string> Tables, IReadOnlyList<OutputColumn> Columns, bool Aggregate, int Limit,
    int Offset, string? Scope, IReadOnlyList<string> Notes);

/// <summary>
/// Построитель SQL из типизированного плана. Гарантии: (1) имена таблиц и колонок — только из каталога, в кавычках; (2) значения — только
/// параметрами; (3) только SELECT; (4) каждая таблица проходит AnalytikAccess.Decide, скрытые колонки (персональные, секреты, служебные) нельзя
/// выбрать, отфильтровать или сгруппировать; (5) у пользователя с неполной областью продаж — предикат области на каждой таблице продаж;
/// (6) соединения — только по подтверждённым связям, неоднозначная связь — ошибка, а не догадка; (7) сумма, среднее и число строк не считаются
/// через соединение «один ко многим» (строки размножились бы); (8) предел строк и групп.
/// </summary>
public static partial class QueryBuilder
{
    private const int MaxJoinDepth = 3;
    private const int MaxInValues = 500;
    private const int MaxDefaultColumns = 20;

    private static readonly string[] Units = ["day", "week", "month", "quarter", "year"];
    private static readonly string[] Functions = ["sum", "avg", "min", "max", "count", "count_distinct"];

    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_ ]{0,62}$")]
    private static partial Regex AliasPattern();

    public static BuiltQuery Build(QueryPlan plan, CatalogModel catalog, AnalytikPrincipal principal, QueryLimits limits) =>
        new Builder(plan, catalog, principal, limits).Build();

    private sealed class Node(string alias, CatalogTableInfo table, TableDecision decision, bool fanOut, bool upReached)
    {
        /// <summary>Таблица достигнута шагом «вверх» (многие к одному): от неё нельзя идти «вниз» — это было бы «многие ко многим».</summary>
        public bool UpReached { get; } = upReached;
        public string Alias { get; } = alias;
        public CatalogTableInfo Table { get; } = table;
        public TableDecision Decision { get; } = decision;
        public bool FanOut { get; } = fanOut;
        public string? JoinSql { get; set; }
    }

    private sealed class Builder(QueryPlan plan, CatalogModel catalog, AnalytikPrincipal principal, QueryLimits limits)
    {
        private readonly List<Node> _nodes = [];
        private readonly List<SqlParam> _params = [];
        private readonly List<string> _notes = [];
        private readonly Dictionary<string, string> _scopeParams = new(StringComparer.Ordinal);
        private int _subqueries;

        public BuiltQuery Build()
        {
            var baseTable = catalog.Find(plan.From)
                ?? throw Invalid($"Таблица «{plan.From}» не найдена в каталоге. Найдите нужную таблицу поиском по каталогу (analytik_find_tables).");
            var root = AddNode(baseTable, false, false);
            foreach (var join in plan.Join ?? [])
            {
                JoinExplicit(join);
            }

            var measures = plan.Measures ?? [];
            var aggregate = measures.Count > 0;
            if (!aggregate && plan.Bucket is not null)
            {
                throw Invalid("Интервал времени (bucket) задаётся вместе с показателями (measures): по чему считать сумму или число строк.");
            }

            var select = new List<(string Sql, OutputColumn Out)>();
            var groupCount = 0;
            if (aggregate)
            {
                var groups = plan.GroupBy is { Count: > 0 } g ? g.Select(c => new PlanField(c)) : plan.Select ?? [];
                foreach (var field in groups)
                {
                    var (node, column) = Resolve(field.Column, "группировки");
                    select.Add(($"{Ref(node, column)}", Output(field.As, node, column, "column", select)));
                    groupCount++;
                }

                if (plan.Bucket is { } bucket)
                {
                    var unit = bucket.Unit.Trim().ToLowerInvariant();
                    if (!Units.Contains(unit))
                    {
                        throw Invalid($"Шаг интервала — day, week, month, quarter или year, а не «{bucket.Unit}».");
                    }

                    var (node, column) = Resolve(bucket.Column, "интервала");
                    if (!ColumnRoles.IsTemporal(column.Role) && column.DataType != "date" && !column.DataType.StartsWith("timestamp", StringComparison.Ordinal))
                    {
                        throw Invalid($"Интервал считается по дате, а {column.Name} — {column.DataType}.");
                    }

                    var name = Alias(bucket.As ?? unit, select);
                    select.Add(($"date_trunc('{unit}', {Ref(node, column)})::date",
                        new OutputColumn(name, "bucket", $"{node.Table.FullName}.{column.Name} по {unit}", ColumnRoles.Date, "date")));
                    groupCount++;
                }

                foreach (var m in measures)
                {
                    select.Add(Measure(m, select));
                }

                if (_nodes.Any(n => n.FanOut) && measures.Any(m => m.Fn.Trim().ToLowerInvariant() is "sum" or "avg" or "count"))
                {
                    var fan = _nodes.First(n => n.FanOut).Table.FullName;
                    throw Invalid($"Соединение с {fan} — «один ко многим»: строки размножатся, и сумма, среднее или число строк будут завышены. " +
                                  $"Возьмите более подробную таблицу базовой (from = {fan}) и подключите остальные к ней, либо используйте count_distinct, min, max.");
                }
            }
            else
            {
                var fields = plan.Select is { Count: > 0 } s
                    ? s
                    : root.Table.Columns.Where(c => c.Visible && c.Role != ColumnRoles.Json).Take(MaxDefaultColumns).Select(c => new PlanField(c.Name)).ToList();
                foreach (var field in fields)
                {
                    var (node, column) = Resolve(field.Column, "выбора");
                    select.Add((Ref(node, column), Output(field.As, node, column, "column", select)));
                }

                if (select.Count == 0)
                {
                    throw Invalid($"У таблицы {root.Table.FullName} нет колонок, доступных для показа.");
                }
            }

            var where = new List<string>();
            if (Scope(root) is { } rootScope)
            {
                where.Add(rootScope);
            }

            foreach (var f in plan.Filters ?? [])
            {
                where.Add(Filter(f));
            }

            var order = Order(select, aggregate, root);
            var max = aggregate ? limits.MaxGroups : limits.MaxRows;
            var limit = Math.Clamp(plan.Limit ?? (aggregate ? 200 : 100), 1, max);
            var offset = Math.Clamp(plan.Offset ?? 0, 0, 1_000_000);

            var sql = new StringBuilder();
            sql.Append("SELECT ").AppendJoin(", ", select.Select(s => $"{s.Sql} AS {CatalogService.Q(s.Out.Name)}"));
            sql.Append("\nFROM ").Append(Table(root.Table)).Append(' ').Append(root.Alias);
            foreach (var n in _nodes.Skip(1))
            {
                sql.Append('\n').Append(n.JoinSql);
            }

            if (where.Count > 0)
            {
                sql.Append("\nWHERE ").AppendJoin("\n  AND ", where.Select(w => $"({w})"));
            }

            if (aggregate && groupCount > 0)
            {
                sql.Append("\nGROUP BY ").AppendJoin(", ", Enumerable.Range(1, groupCount));
            }

            if (order.Length > 0)
            {
                sql.Append("\nORDER BY ").Append(order);
            }

            // На строку больше предела — так видно, что результат урезан (has_more).
            sql.Append("\nLIMIT ").Append(limit + 1).Append(" OFFSET ").Append(offset);

            var scope = principal.Sales.Restricted && _nodes.Any(n => n.Decision.Scope.Count > 0) ? principal.Sales.Description : null;
            return new BuiltQuery(sql.ToString(), _params, _nodes.Select(n => n.Table.FullName).ToList(), select.Select(s => s.Out).ToList(), aggregate,
                limit, offset, scope, _notes);
        }

        // ---- таблицы и соединения ----

        private Node AddNode(CatalogTableInfo table, bool fanOut, bool upReached)
        {
            var decision = AnalytikAccess.Decide(table, principal);
            if (!decision.Allowed)
            {
                throw new QueryPlanException(QueryErrors.Denied, decision.Message!);
            }

            if (_nodes.Count >= 8)
            {
                throw Invalid("В запросе больше 8 таблиц — разбейте его на части.");
            }

            var node = new Node($"t{_nodes.Count}", table, decision, fanOut, upReached);
            _nodes.Add(node);
            return node;
        }

        private static string Table(CatalogTableInfo t) => $"{CatalogService.Q(t.Schema)}.{CatalogService.Q(t.Name)}";

        /// <summary>«schema.Table» или «schema.Table via Column» — колонка связи у одной из уже подключённых таблиц или у подключаемой.</summary>
        private void JoinExplicit(string spec)
        {
            var parts = Regex.Split(spec.Trim(), @"\s+via\s+", RegexOptions.IgnoreCase);
            var table = FindTable(parts[0]);
            if (_nodes.Any(n => n.Table.FullName == table.FullName))
            {
                return;
            }

            if (parts.Length == 2)
            {
                var column = parts[1].Trim().Trim('"');
                var edges = catalog.Relations.Where(r => r.Joinable && r.FromColumn.Equals(column, StringComparison.OrdinalIgnoreCase)
                    && (r.To == table.FullName && _nodes.Any(n => n.Table.FullName == r.From)
                        || r.From == table.FullName && _nodes.Any(n => n.Table.FullName == r.To))).ToList();
                if (edges.Count != 1)
                {
                    throw Invalid($"Связи {table.FullName} через {column} с таблицами запроса нет.");
                }

                var r = edges[0];
                var down = r.From == table.FullName;
                Attach(table, _nodes.First(n => n.Table.FullName == (down ? r.To : r.From)), r, down);
                return;
            }

            EnsureNode(table);
        }

        private Node EnsureNode(CatalogTableInfo target)
        {
            if (_nodes.FirstOrDefault(n => n.Table.FullName == target.FullName) is { } existing)
            {
                return existing;
            }

            // Кратчайший путь по подтверждённым связям (до MaxJoinDepth шагов): короче лучше, при равной длине — меньше переходов «один ко
            // многим». Шаг «вниз» (к таблице, которая ссылается) — только от таблиц, достигнутых без шагов «вверх»: иначе две таблицы-соседки
            // соединились бы через общего родителя «многие ко многим» (заказы и визиты через точку). Два разных пути одной стоимости —
            // неоднозначно (StockTransfers.FromStockId и ToStockId → Stocks): нужна явная связь «via».
            var adjacency = Adjacency();
            var dist = _nodes.ToDictionary(n => n.Table.FullName, _ => 0, StringComparer.Ordinal);
            var best = new List<List<(CatalogRelationInfo Relation, bool Down)>>();
            var bestCost = int.MaxValue;
            var queue = new Queue<(string Table, bool Up, List<(CatalogRelationInfo Relation, bool Down)> Path, int Cost)>();
            foreach (var n in _nodes)
            {
                queue.Enqueue((n.Table.FullName, n.UpReached, [], 0));
            }

            while (queue.Count > 0)
            {
                var (at, up, path, cost) = queue.Dequeue();
                if (path.Count >= MaxJoinDepth || cost > bestCost)
                {
                    continue;
                }

                foreach (var (r, down) in adjacency.GetValueOrDefault(at) ?? [])
                {
                    var next = down ? r.From : r.To;
                    if ((down && up) || _nodes.Any(n => n.Table.FullName == next) || path.Any(p => p.Relation.From == next || p.Relation.To == next)
                        || catalog.Find(next) is null)
                    {
                        continue;
                    }

                    var stepCost = cost + 10 + (down || !UniqueTarget(r) ? 1 : 0);
                    if (dist.TryGetValue(next, out var known) && stepCost > known)
                    {
                        continue;
                    }

                    dist[next] = stepCost;
                    var step = new List<(CatalogRelationInfo, bool)>(path) { (r, down) };
                    if (next == target.FullName)
                    {
                        if (stepCost < bestCost)
                        {
                            bestCost = stepCost;
                            best = [step];
                        }
                        else if (stepCost == bestCost)
                        {
                            best.Add(step);
                        }
                    }
                    else
                    {
                        queue.Enqueue((next, up || !down, step, stepCost));
                    }
                }
            }

            if (best.Count == 0)
            {
                throw Invalid($"Не найдена подтверждённая связь между {string.Join(", ", _nodes.Select(n => n.Table.FullName))} и {target.FullName} " +
                              $"(до {MaxJoinDepth} шагов, без соединений «многие ко многим»). Посмотрите связи таблицы (analytik_describe_table) или выберите другую базовую таблицу.");
            }

            var distinct = best.Select(p => string.Join(" > ", p.Select(s => $"{s.Relation.From}.{s.Relation.FromColumn}"))).Distinct().ToList();
            if (distinct.Count > 1)
            {
                throw Invalid($"Связь с {target.FullName} неоднозначна: {string.Join(" или ", distinct)}. Укажите в join «{target.FullName} via <колонка>».");
            }

            Node? last = null;
            foreach (var (r, down) in best[0])
            {
                var fromTable = down ? r.To : r.From;
                var toTable = down ? r.From : r.To;
                last = _nodes.FirstOrDefault(n => n.Table.FullName == toTable)
                    ?? Attach(catalog.Find(toTable)!, _nodes.First(n => n.Table.FullName == fromTable), r, down);
            }

            return last!;
        }

        private Dictionary<string, List<(CatalogRelationInfo Relation, bool Down)>>? _adjacency;

        /// <summary>Соседи таблицы по подтверждённым связям: вверх (к таблице, на которую она ссылается) и вниз (к таблицам, которые ссылаются на неё).</summary>
        private Dictionary<string, List<(CatalogRelationInfo Relation, bool Down)>> Adjacency()
        {
            if (_adjacency is not null)
            {
                return _adjacency;
            }

            var adjacency = new Dictionary<string, List<(CatalogRelationInfo, bool)>>(StringComparer.Ordinal);
            foreach (var r in catalog.Relations.Where(r => r.Joinable).OrderBy(r => r.From, StringComparer.Ordinal).ThenBy(r => r.FromColumn, StringComparer.Ordinal))
            {
                Add(r.From, (r, false));
                Add(r.To, (r, true));
            }

            _adjacency = adjacency;
            return adjacency;

            void Add(string table, (CatalogRelationInfo, bool) edge)
            {
                if (!adjacency.TryGetValue(table, out var list))
                {
                    adjacency[table] = list = [];
                }

                list.Add(edge);
            }
        }

        /// <summary>
        /// Связь ведёт к одной строке цели: колонка цели — первичный ключ или уникальна; составной ключ справочника (hr.References: Kind +
        /// VerifixId) уникален вместе с условием связи.
        /// </summary>
        private bool UniqueTarget(CatalogRelationInfo r)
        {
            var table = catalog.Find(r.To);
            if (table?.Column(r.ToColumn) is not { } column)
            {
                return false;
            }

            if (r.TargetFilter is { } filter)
            {
                var filterColumn = filter.Split('=', 2)[0].Trim();
                var key = (table.PrimaryKey ?? "").Split(',', StringSplitOptions.TrimEntries);
                return key.Length == 2 && key.Contains(filterColumn) && key.Contains(column.Name);
            }

            return column.IsUnique || table.PrimaryKey == column.Name;
        }

        /// <summary>Подключить таблицу к родителю по связи. down — подключаемая таблица ссылается на родителя («один ко многим»).</summary>
        private Node Attach(CatalogTableInfo table, Node parent, CatalogRelationInfo r, bool down)
        {
            if (down && parent.UpReached)
            {
                throw Invalid($"Соединение {parent.Table.FullName} → {table.FullName} дало бы «многие ко многим» (через общую таблицу). Выберите другую базовую таблицу.");
            }

            var fanOut = down || !UniqueTarget(r);
            var node = AddNode(table, fanOut, parent.UpReached || !down);
            var on = down
                ? $"{node.Alias}.{CatalogService.Q(r.FromColumn)} = {parent.Alias}.{CatalogService.Q(r.ToColumn)}"
                : $"{node.Alias}.{CatalogService.Q(r.ToColumn)} = {parent.Alias}.{CatalogService.Q(r.FromColumn)}";
            if (r.TargetFilter is { } filter && filter.Split('=', 2) is [var column, var value])
            {
                var owner = down ? parent : node;
                if (owner.Table.Column(column.Trim()) is { } c)
                {
                    on += $" AND {owner.Alias}.{CatalogService.Q(c.Name)} = {Param(value.Trim())}";
                }
            }

            if (Scope(node) is { } scope)
            {
                on += $" AND {scope}";
            }

            node.JoinSql = $"LEFT JOIN {Table(table)} {node.Alias} ON {on}";
            if (fanOut)
            {
                _notes.Add($"{table.FullName} подключена связью «один ко многим» ({r.From}.{r.FromColumn} → {r.To}.{r.ToColumn}).");
            }

            return node;
        }

        private CatalogTableInfo FindTable(string reference)
        {
            var name = reference.Trim().Trim('"');
            if (catalog.Find(name) is { } exact)
            {
                return exact;
            }

            var byName = catalog.Tables.Where(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            var included = byName.Where(t => _nodes.Any(n => n.Table.FullName == t.FullName)).ToList();
            return included.Count == 1 ? included[0]
                : byName.Count == 1 ? byName[0]
                : byName.Count > 1 ? throw Invalid($"Таблица «{name}» есть в нескольких схемах: {string.Join(", ", byName.Select(t => t.FullName))} — укажите схему.")
                : throw Invalid($"Таблица «{name}» не найдена в каталоге.");
        }

        // ---- колонки ----

        private (Node Node, CatalogColumnInfo Column) Resolve(string reference, string purpose)
        {
            var parts = reference.Trim().Replace("\"", "").Split('.');
            CatalogTableInfo table;
            string columnName;
            switch (parts.Length)
            {
                case 1:
                    table = _nodes[0].Table;
                    columnName = parts[0];
                    break;
                case 2:
                    table = FindTable(parts[0]);
                    columnName = parts[1];
                    break;
                case 3:
                    table = FindTable($"{parts[0]}.{parts[1]}");
                    columnName = parts[2];
                    break;
                default:
                    throw Invalid($"Колонка «{reference}»: ожидается Column, Table.Column или schema.Table.Column.");
            }

            var column = table.Column(columnName)
                ?? throw Invalid($"У таблицы {table.FullName} нет колонки «{columnName}». Есть: {string.Join(", ", table.Columns.Where(c => c.Visible).Take(40).Select(c => c.Name))}.");
            if (!column.Visible)
            {
                throw new QueryPlanException(QueryErrors.Denied,
                    $"Колонка {table.FullName}.{column.Name} скрыта ({SensitivityText(column.Sensitivity)}) — её нельзя использовать для {purpose}.");
            }

            return (EnsureNode(table), column);
        }

        private static string SensitivityText(string sensitivity) => sensitivity switch
        {
            Sensitivity.Secret => "секрет: пароль, ключ или токен",
            Sensitivity.Personal => "персональные данные",
            _ => "служебная колонка",
        };

        private static string Ref(Node node, CatalogColumnInfo column) => $"{node.Alias}.{CatalogService.Q(column.Name)}";

        private OutputColumn Output(string? alias, Node node, CatalogColumnInfo column, string kind, List<(string Sql, OutputColumn Out)> taken)
        {
            var name = alias ?? (taken.Any(t => t.Out.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase)) ? $"{node.Table.Name}_{column.Name}" : column.Name);
            return new OutputColumn(Alias(name, taken), kind, $"{node.Table.FullName}.{column.Name}", column.Role, column.DataType);
        }

        private static string Alias(string name, List<(string Sql, OutputColumn Out)> taken)
        {
            var trimmed = name.Trim();
            if (!AliasPattern().IsMatch(trimmed))
            {
                throw Invalid($"Подпись «{name}»: только буквы, цифры, пробел и _, до 63 знаков.");
            }

            if (taken.Any(t => t.Out.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                throw Invalid($"Подпись «{trimmed}» повторяется.");
            }

            return trimmed;
        }

        private (string Sql, OutputColumn Out) Measure(PlanMeasure m, List<(string Sql, OutputColumn Out)> taken)
        {
            var fn = m.Fn.Trim().ToLowerInvariant();
            if (!Functions.Contains(fn))
            {
                throw Invalid($"Показатель «{m.Fn}»: sum, avg, min, max, count, count_distinct.");
            }

            if (m.Column is null)
            {
                return fn == "count"
                    ? ("count(*)", new OutputColumn(Alias(m.As ?? "count", taken), "measure", "count(*)", ColumnRoles.Count, "bigint"))
                    : throw Invalid($"Для {fn} нужна колонка.");
            }

            var (node, column) = Resolve(m.Column, "расчёта");
            var expr = Ref(node, column);
            var numeric = CatalogRules.IsNumericType(column.DataType);
            var temporal = column.DataType == "date" || column.DataType.StartsWith("timestamp", StringComparison.Ordinal);
            var (sql, role, type) = fn switch
            {
                "sum" when numeric => ($"sum({expr})", column.Role, "numeric"),
                "avg" when numeric => ($"avg({expr})", column.Role, "numeric"),
                "min" or "max" when numeric || temporal || CatalogRules.IsTextType(column.DataType) => ($"{fn}({expr})", column.Role, column.DataType),
                "count" => ($"count({expr})", ColumnRoles.Count, "bigint"),
                "count_distinct" => ($"count(DISTINCT {expr})", ColumnRoles.Count, "bigint"),
                _ => throw Invalid($"{fn} не считается по колонке {column.Name} типа {column.DataType}."),
            };
            var name = Alias(m.As ?? $"{fn}_{column.Name}", taken);
            return (sql, new OutputColumn(name, "measure", $"{fn}({node.Table.FullName}.{column.Name})", role, type));
        }

        // ---- фильтры ----

        private string Filter(PlanFilter f)
        {
            var (node, column) = Resolve(f.Column, "фильтра");
            var op = f.Op.Trim().ToLowerInvariant();
            var expr = Ref(node, column);
            switch (op)
            {
                case "is_null":
                    return $"{expr} IS NULL";
                case "not_null":
                    return $"{expr} IS NOT NULL";
                case "eq" or "ne" or "gt" or "gte" or "lt" or "lte":
                {
                    var value = Scalar(f, column);
                    var sign = op switch { "eq" => "=", "ne" => "<>", "gt" => ">", "gte" => ">=", "lt" => "<", _ => "<=" };
                    return $"{expr} {sign} {Param(value)}";
                }

                case "in" or "not_in":
                {
                    var values = Array(f, column, 1, MaxInValues);
                    return op == "in" ? $"{expr} = ANY({Param(values)})" : $"NOT ({expr} = ANY({Param(values)}))";
                }

                case "between":
                {
                    var values = Values(f, column);
                    if (values.Count != 2)
                    {
                        throw Invalid($"between для {column.Name}: нужны два значения [от, до].");
                    }

                    return $"{expr} BETWEEN {Param(values[0])} AND {Param(values[1])}";
                }

                case "contains" or "starts_with":
                {
                    if (!CatalogRules.IsTextType(column.DataType))
                    {
                        throw Invalid($"{op} — только для текста, а {column.Name} — {column.DataType}.");
                    }

                    var text = Scalar(f, column) as string ?? "";
                    var escaped = text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                    return $"{expr} ILIKE {Param(op == "contains" ? $"%{escaped}%" : $"{escaped}%")} ESCAPE '\\'";
                }

                default:
                    throw Invalid($"Условие «{f.Op}»: eq, ne, gt, gte, lt, lte, in, not_in, between, contains, starts_with, is_null, not_null.");
            }
        }

        private static object Scalar(PlanFilter f, CatalogColumnInfo column)
        {
            if (f.Value is not { } v || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                throw Invalid($"Для условия {f.Op} по {column.Name} нужно значение.");
            }

            return v.ValueKind == JsonValueKind.Array
                ? throw Invalid($"Для условия {f.Op} по {column.Name} нужно одно значение, а не список.")
                : Convert(v, column);
        }

        private static List<object> Values(PlanFilter f, CatalogColumnInfo column) =>
            f.Value is { ValueKind: JsonValueKind.Array } array
                ? array.EnumerateArray().Select(v => Convert(v, column)).ToList()
                : throw Invalid($"Для условия {f.Op} по {column.Name} нужен список значений.");

        private static object Array(PlanFilter f, CatalogColumnInfo column, int min, int max)
        {
            var values = Values(f, column);
            if (values.Count < min || values.Count > max)
            {
                throw Invalid($"Список значений для {column.Name}: от {min} до {max}.");
            }

            var type = ClrType(column.DataType);
            var typed = System.Array.CreateInstance(type, values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                typed.SetValue(values[i], i);
            }

            return typed;
        }

        private static Type ClrType(string dataType) => dataType switch
        {
            "bigint" => typeof(long),
            "integer" or "smallint" => typeof(int),
            "date" => typeof(DateOnly),
            "uuid" => typeof(Guid),
            "boolean" => typeof(bool),
            _ when dataType.StartsWith("timestamp", StringComparison.Ordinal) => typeof(DateTime),
            _ when CatalogRules.IsNumericType(dataType) => typeof(decimal),
            _ when CatalogRules.IsTextType(dataType) => typeof(string),
            _ => throw Invalid($"Фильтр по колонке типа {dataType} не поддерживается."),
        };

        /// <summary>Значение фильтра в тип колонки: число, дата, время, uuid, логическое, текст (до 500 знаков).</summary>
        internal static object Convert(JsonElement v, CatalogColumnInfo column)
        {
            var text = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString()!.Trim(),
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => throw Invalid($"Значение для {column.Name} должно быть числом, строкой или true/false."),
            };
            var type = ClrType(column.DataType);
            try
            {
                return type == typeof(long) ? long.Parse(text, CultureInfo.InvariantCulture)
                    : type == typeof(int) ? int.Parse(text, CultureInfo.InvariantCulture)
                    : type == typeof(decimal) ? decimal.Parse(text, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture)
                    : type == typeof(DateOnly) ? DateOnly.FromDateTime(DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces))
                    : type == typeof(DateTime) ? Time(text, column.DataType)
                    : type == typeof(Guid) ? Guid.Parse(text)
                    : type == typeof(bool) ? bool.Parse(text)
                    : text.Length <= 500 ? text : throw Invalid($"Значение для {column.Name} длиннее 500 знаков.");
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw Invalid($"«{text}» — не значение типа {column.DataType} для {column.Name}.");
            }
        }

        private static DateTime Time(string text, string dataType)
        {
            var parsed = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            return dataType.Contains("with time zone", StringComparison.Ordinal)
                ? parsed.UtcDateTime
                : DateTime.SpecifyKind(parsed.DateTime, DateTimeKind.Unspecified);
        }

        private string Param(object value)
        {
            var name = $"p{_params.Count}";
            _params.Add(new SqlParam(name, value));
            return "@" + name;
        }

        // ---- область продаж ----

        /// <summary>Предикат области продаж для таблицы (null — ограничения нет).</summary>
        private string? Scope(Node node)
        {
            if (!principal.Sales.Restricted || node.Decision.Scope.Count == 0)
            {
                return null;
            }

            return string.Join(" AND ", node.Decision.Scope.Select(e => Predicate(node.Alias, node.Table, e, 0)));
        }

        private string Predicate(string alias, CatalogTableInfo table, ScopeEntry e, int depth)
        {
            var column = table.Column(e.Column) ?? throw Invalid($"Правило области: нет колонки {table.FullName}.{e.Column}.");
            var expr = $"{alias}.{CatalogService.Q(column.Name)}";
            var s = principal.Sales;
            switch (e.Kind)
            {
                case "branch":
                    return $"{expr} = ANY({ScopeParam("branches", s.BranchIds.ToArray())})";
                case "region":
                    return $"{expr} = ANY({ScopeParam("regions", s.RegionIds.ToArray())})";
                case "direction":
                    return $"{expr} = ANY({ScopeParam("directions", s.DirectionIds.ToArray())})";
                case "agent":
                    return $"{expr} = {ScopeParam("agent", s.AgentId ?? -1L)}";
                case "market":
                {
                    var markets = catalog.Find("linko.Markets") ?? throw Invalid("Нет таблицы точек linko.Markets для области продаж.");
                    var m = $"sm{_subqueries++}";
                    return $"EXISTS (SELECT 1 FROM {Table(markets)} {m} WHERE {m}.\"Id\" = {expr} AND {m}.\"BranchId\" = ANY({ScopeParam("branches", s.BranchIds.ToArray())}))";
                }

                case "parent" when depth == 0 && e.Parent is { } parentName:
                {
                    var parent = catalog.Find(parentName) ?? throw Invalid($"Правило области: нет таблицы {parentName}.");
                    var entries = AnalytikAccess.Applicable(parent.ScopeRule, s.Level)
                        ?? throw new QueryPlanException(QueryErrors.Denied, $"Таблица {table.FullName} недоступна при вашей области продаж.");
                    var p = $"sp{_subqueries++}";
                    var key = parent.PrimaryKey ?? "Id";
                    var inner = entries.Count == 0 ? "" : " AND " + string.Join(" AND ", entries.Select(x => Predicate(p, parent, x, depth + 1)));
                    return $"EXISTS (SELECT 1 FROM {Table(parent)} {p} WHERE {p}.{CatalogService.Q(key)} = {expr}{inner})";
                }

                default:
                    throw new QueryPlanException(QueryErrors.Denied, $"Таблица {table.FullName} недоступна при вашей области продаж.");
            }
        }

        private string ScopeParam(string key, object value)
        {
            if (!_scopeParams.TryGetValue(key, out var name))
            {
                name = $"scope_{key}";
                _params.Add(new SqlParam(name, value));
                _scopeParams[key] = name;
            }

            return "@" + name;
        }

        // ---- сортировка ----

        private string Order(List<(string Sql, OutputColumn Out)> select, bool aggregate, Node root)
        {
            var parts = new List<string>();
            foreach (var o in plan.OrderBy ?? [])
            {
                var dir = (o.Dir ?? "asc").Trim().ToLowerInvariant() switch
                {
                    "asc" => "ASC",
                    "desc" => "DESC",
                    _ => throw Invalid($"Направление сортировки — asc или desc, а не «{o.Dir}»."),
                };
                var index = select.FindIndex(s => s.Out.Name.Equals(o.By.Trim(), StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    parts.Add($"{index + 1} {dir} NULLS LAST");
                    continue;
                }

                if (aggregate)
                {
                    throw Invalid($"Сортировка «{o.By}»: в запросе с показателями сортируйте по подписи колонки результата ({string.Join(", ", select.Select(s => s.Out.Name))}).");
                }

                var (node, column) = Resolve(o.By, "сортировки");
                parts.Add($"{Ref(node, column)} {dir} NULLS LAST");
            }

            if (parts.Count > 0)
            {
                return string.Join(", ", parts);
            }

            if (aggregate)
            {
                var bucket = select.FindIndex(s => s.Out.Kind == "bucket");
                var measure = select.FindIndex(s => s.Out.Kind == "measure");
                return bucket >= 0 ? $"{bucket + 1} ASC" : measure >= 0 ? $"{measure + 1} DESC NULLS LAST" : "1";
            }

            return root.Table.DateColumn is { } date && root.Table.Column(date) is { Visible: true } dc
                ? $"{root.Alias}.{CatalogService.Q(dc.Name)} DESC NULLS LAST"
                : root.Table.PrimaryKey is { } pk && !pk.Contains(',') && root.Table.Column(pk) is { } pkc
                    ? $"{root.Alias}.{CatalogService.Q(pkc.Name)}"
                    : "1";
        }

        private static QueryPlanException Invalid(string message) => new(QueryErrors.Invalid, message);
    }
}

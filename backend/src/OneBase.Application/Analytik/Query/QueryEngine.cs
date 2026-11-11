using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneBase.Application.Analytik.Catalog;
using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik.Query;

/// <summary>Кто запрашивает: пользователь (null — фоновая задача), откуда (chat, dashboard, api, job), AI-сотрудник и чат.</summary>
public sealed record QueryCaller(Guid? UserId, string Origin, string? AgentCode = null, Guid? ConversationId = null);

/// <summary>Результат запроса: колонки, строки (значения приведены к JSON-виду), есть ли ещё строки, таблицы, область, заметки, SQL.</summary>
public sealed record QueryResultView(
    Guid QueryId, IReadOnlyList<OutputColumn> Columns, IReadOnlyList<object?[]> Rows, bool HasMore, int Offset, long ElapsedMs,
    IReadOnlyList<string> Tables, string? Scope, IReadOnlyList<string> Notes, string Sql, DateTimeOffset At);

/// <summary>Итог запроса: ok, denied, invalid, too_heavy, timeout, error — и текст ошибки, безопасный для пользователя и модели.</summary>
public sealed record QueryOutcome(Guid QueryId, string Status, string? Error, QueryResultView? Result)
{
    public bool Ok => Status == "ok";
}

/// <summary>Профиль колонки: заполненность, число различных значений, диапазон, частые значения.</summary>
public sealed record ColumnProfile(
    string Table, string Column, string DataType, string Role, long Rows, long Filled, long Distinct, object? Min, object? Max,
    IReadOnlyList<ProfileValue> Top, string? Scope);

/// <summary>Частое значение колонки и сколько раз оно встречается.</summary>
public sealed record ProfileValue(object? Value, long Count);

/// <summary>«Всё о записи»: сама запись, записи, на которые она ссылается, и связанные записи других таблиц (число и последние).</summary>
public sealed record EntityView(
    string Table, object? Key, IReadOnlyDictionary<string, object?>? Record, IReadOnlyList<EntityLink> References, IReadOnlyList<EntityRelated> Related,
    IReadOnlyList<string> Hidden, string? Scope);

public sealed record EntityLink(string Column, string Table, object? Key, string? Label);

public sealed record EntityRelated(string Table, string Title, string Column, long Count, IReadOnlyList<OutputColumn> Columns, IReadOnlyList<object?[]> Latest);

/// <summary>
/// Universal Analytical Query Engine: план → проверка прав и построение SQL (QueryBuilder) → оценка стоимости (EXPLAIN) → выполнение только на
/// чтение (IAnalytikSql) → журнал analytik.Queries (каждый запрос, и отказ тоже). Права проверяются при каждом запросе — от имени того, кто
/// спрашивает; кэша результатов между пользователями нет.
/// </summary>
public sealed class QueryEngine(
    IAnalytikDb db,
    CatalogService catalog,
    AnalytikAccess access,
    IAnalytikSql sql,
    AnalytikOptions options,
    TimeProvider clock,
    ILogger<QueryEngine> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public Task<AnalytikPrincipal> PrincipalAsync(QueryCaller caller, CancellationToken ct) =>
        caller.UserId is { } id ? access.ResolveAsync(id, ct)
        : caller.Origin == "job" ? Task.FromResult(AnalytikPrincipal.SystemPrincipal)
        : throw new UnauthorizedAccessException("Запрос без пользователя — только для фоновых задач Аналитика.");

    public async Task<QueryOutcome> RunAsync(QueryPlan plan, QueryCaller caller, CancellationToken ct, AnalytikPrincipal? principal = null)
    {
        var watch = Stopwatch.StartNew();
        principal ??= await PrincipalAsync(caller, ct);
        var log = new AnalytikQuery
        {
            UserId = caller.UserId, Tenant = options.Tenant, Origin = caller.Origin, AgentCode = caller.AgentCode, ConversationId = caller.ConversationId,
            Plan = plan.ToJson(), Status = "ok",
        };

        BuiltQuery built;
        try
        {
            var model = await catalog.ModelAsync(ct);
            built = QueryBuilder.Build(plan, model, principal, new QueryLimits(options.MaxRows, options.MaxGroups));
            log.Sql = built.Sql;
            log.Tables = string.Join(",", built.Tables);
            log.Scope = built.Scope;
            log.Parameters = ParametersJson(built.Parameters);

            var cost = await CostAsync(built, ct);
            if (cost > options.MaxQueryCost)
            {
                return await FailAsync(log, watch, QueryErrors.TooHeavy,
                    $"Запрос слишком тяжёлый (оценка PostgreSQL {cost:N0} при пределе {options.MaxQueryCost:N0}): добавьте период или условия, " +
                    "сгруппируйте крупнее или возьмите готовый инструмент отдела.", ct);
            }

            var result = await sql.QueryAsync(built.Sql, built.Parameters, new SqlLimits(built.Limit, options.QueryTimeoutSeconds), ct);
            var rows = result.Rows.Select(r => r.Select(Normalize).ToArray()).ToList();
            log.RowCount = rows.Count;
            log.Truncated = result.Truncated;
            log.DurationMs = watch.ElapsedMilliseconds;
            await SaveAsync(log, ct);
            return new QueryOutcome(log.Id, "ok", null, new QueryResultView(log.Id, built.Columns, rows, result.Truncated, built.Offset, result.ElapsedMs,
                built.Tables, built.Scope, built.Notes, built.Sql, clock.GetUtcNow()));
        }
        catch (QueryPlanException ex)
        {
            return await FailAsync(log, watch, ex.Code == QueryErrors.Denied ? "denied" : ex.Code, ex.Message, ct);
        }
        catch (AnalytikTimeoutException ex)
        {
            return await FailAsync(log, watch, "timeout", ex.Message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: запрос не выполнен");
            return await FailAsync(log, watch, "error", "Запрос не выполнен из-за ошибки базы данных — попробуйте упростить условия.", ct);
        }
    }

    /// <summary>Оценка стоимости плана PostgreSQL (EXPLAIN без выполнения).</summary>
    private async Task<double> CostAsync(BuiltQuery built, CancellationToken ct)
    {
        var explain = await sql.QueryAsync("EXPLAIN (FORMAT JSON) " + built.Sql, built.Parameters, new SqlLimits(1, 10), ct);
        var text = explain.Rows.FirstOrDefault()?[0]?.ToString();
        if (text is null)
        {
            return 0;
        }

        using var doc = JsonDocument.Parse(text);
        return doc.RootElement[0].GetProperty("Plan").GetProperty("Total Cost").GetDouble();
    }

    private async Task<QueryOutcome> FailAsync(AnalytikQuery log, Stopwatch watch, string status, string message, CancellationToken ct)
    {
        log.Status = status;
        log.Error = message.Length <= 2000 ? message : message[..2000];
        log.DurationMs = watch.ElapsedMilliseconds;
        await SaveAsync(log, ct);
        return new QueryOutcome(log.Id, status, message, null);
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
            logger.LogWarning(ex, "Аналитик: запрос не записан в журнал");
        }
    }

    /// <summary>Параметры для журнала: списки области — первые 20 значений.</summary>
    private static string ParametersJson(IReadOnlyList<SqlParam> parameters) =>
        JsonSerializer.Serialize(parameters.ToDictionary(p => p.Name, p => p.Value is Array a && a.Length > 20
            ? (object)a.Cast<object>().Take(20).Append($"… всего {a.Length}").ToArray()
            : Normalize(p.Value)), Json);

    /// <summary>Значение базы — в JSON-вид: даты ISO, дробные — до 4 знаков, uuid — строкой.</summary>
    public static object? Normalize(object? value) => value switch
    {
        null => null,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : dt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
        decimal m => Math.Round(m, 4),
        double f => double.IsFinite(f) ? Math.Round(f, 4) : null,
        float f => float.IsFinite(f) ? Math.Round(f, 4) : null,
        Guid g => g.ToString(),
        TimeSpan ts => ts.ToString(),
        Array a => a.Cast<object?>().Select(Normalize).ToArray(),
        _ => value,
    };

    // ---- профиль колонки ----

    public async Task<ColumnProfile> ProfileAsync(string table, string column, QueryCaller caller, CancellationToken ct)
    {
        var principal = await PrincipalAsync(caller, ct);
        var model = await catalog.ModelAsync(ct);
        var t = model.Find(table) ?? throw new QueryPlanException(QueryErrors.Invalid, $"Таблица «{table}» не найдена.");
        var c = t.Column(column) ?? throw new QueryPlanException(QueryErrors.Invalid, $"У {t.FullName} нет колонки «{column}».");
        var comparable = CatalogRules.IsNumericType(c.DataType) || c.DataType == "date" || c.DataType.StartsWith("timestamp", StringComparison.Ordinal);
        var measures = new List<PlanMeasure> { new("count", null, "rows"), new("count", c.Name, "filled"), new("count_distinct", c.Name, "distinct_values") };
        if (comparable)
        {
            measures.Add(new PlanMeasure("min", c.Name, "min_value"));
            measures.Add(new PlanMeasure("max", c.Name, "max_value"));
        }

        var stats = await RunAsync(new QueryPlan { From = t.FullName, Measures = measures }, caller, ct, principal);
        if (!stats.Ok)
        {
            throw new QueryPlanException(stats.Status == "denied" ? QueryErrors.Denied : QueryErrors.Invalid, stats.Error!);
        }

        var row = stats.Result!.Rows[0];
        var distinct = Convert.ToInt64(row[2] ?? 0);
        IReadOnlyList<ProfileValue> top = [];
        if (distinct is > 0 and <= 5000 && c.Role is not ColumnRoles.Id)
        {
            var values = await RunAsync(new QueryPlan
            {
                From = t.FullName, GroupBy = [c.Name], Measures = [new PlanMeasure("count", null, "count")],
                OrderBy = [new PlanOrder("count", "desc")], Limit = 15,
            }, caller, ct, principal);
            if (values.Ok)
            {
                top = values.Result!.Rows.Select(r => new ProfileValue(r[0], Convert.ToInt64(r[1] ?? 0))).ToList();
            }
        }

        return new ColumnProfile(t.FullName, c.Name, c.DataType, c.Role, Convert.ToInt64(row[0] ?? 0), Convert.ToInt64(row[1] ?? 0), distinct,
            comparable ? row[3] : null, comparable ? row[4] : null, top, stats.Result.Scope);
    }

    // ---- всё о записи ----

    public async Task<EntityView> EntityAsync(string table, string key, QueryCaller caller, CancellationToken ct)
    {
        var principal = await PrincipalAsync(caller, ct);
        var model = await catalog.ModelAsync(ct);
        var t = model.Find(table) ?? throw new QueryPlanException(QueryErrors.Invalid, $"Таблица «{table}» не найдена.");
        var pk = t.PrimaryKey is { } p && !p.Contains(',') ? p : throw new QueryPlanException(QueryErrors.Invalid, $"У {t.FullName} нет простого первичного ключа.");
        var keyJson = JsonSerializer.SerializeToElement(key);
        var record = await RunAsync(new QueryPlan { From = t.FullName, Filters = [new PlanFilter(pk, "eq", keyJson)], Limit = 1 }, caller, ct, principal);
        if (!record.Ok)
        {
            throw new QueryPlanException(record.Status == "denied" ? QueryErrors.Denied : QueryErrors.Invalid, record.Error!);
        }

        if (record.Result!.Rows.Count == 0)
        {
            return new EntityView(t.FullName, key, null, [], [], [], record.Result.Scope);
        }

        var values = record.Result.Columns.Select((c, i) => (c.Name, Value: record.Result.Rows[0][i])).ToDictionary(x => x.Name, x => x.Value);
        var hidden = new List<string>();

        // Записи, на которые она ссылается (вверх): подпись — колонка-название цели.
        var references = new List<EntityLink>();
        foreach (var r in model.Relations.Where(r => r.Joinable && r.From == t.FullName).Take(15))
        {
            if (!values.TryGetValue(r.FromColumn, out var fk) || fk is null || model.Find(r.To) is not { } target)
            {
                continue;
            }

            var label = target.Columns.FirstOrDefault(c => c.Visible && c.Role == ColumnRoles.Name);
            if (!AnalytikAccess.Decide(target, principal).Allowed)
            {
                hidden.Add(target.FullName);
                continue;
            }

            string? text = null;
            if (label is not null)
            {
                var linked = await RunAsync(new QueryPlan
                {
                    From = target.FullName, Select = [new PlanField(label.Name)],
                    Filters = [new PlanFilter(r.ToColumn, "eq", JsonSerializer.SerializeToElement(fk.ToString()))], Limit = 1,
                }, caller, ct, principal);
                text = linked.Ok && linked.Result!.Rows.Count > 0 ? linked.Result.Rows[0][0]?.ToString() : null;
            }

            references.Add(new EntityLink(r.FromColumn, target.FullName, fk, text));
        }

        // Связанные записи (вниз): сколько их и последние — по самым наполненным таблицам.
        var related = new List<EntityRelated>();
        var incoming = model.Relations.Where(r => r.Joinable && r.To == t.FullName && values.ContainsKey(r.ToColumn))
            .Select(r => (Relation: r, Table: model.Find(r.From)))
            .Where(x => x.Table is not null)
            .OrderByDescending(x => x.Table!.Rows)
            .Take(15)
            .ToList();
        foreach (var (r, source) in incoming)
        {
            if (!AnalytikAccess.Decide(source!, principal).Allowed)
            {
                hidden.Add(source!.FullName);
                continue;
            }

            var filter = new PlanFilter(r.FromColumn, "eq", JsonSerializer.SerializeToElement(values[r.ToColumn]?.ToString()));
            var count = await RunAsync(new QueryPlan { From = source!.FullName, Measures = [new PlanMeasure("count", null, "count")], Filters = [filter] },
                caller, ct, principal);
            if (!count.Ok)
            {
                hidden.Add(source.FullName);
                continue;
            }

            var n = Convert.ToInt64(count.Result!.Rows[0][0] ?? 0);
            if (n == 0)
            {
                continue;
            }

            var columns = source.Columns.Where(c => c.Visible && c.Role is not (ColumnRoles.Json or ColumnRoles.Id) && c.Name != r.FromColumn)
                .Take(8).Select(c => new PlanField(c.Name)).ToList();
            var latest = await RunAsync(new QueryPlan { From = source.FullName, Select = columns, Filters = [filter], Limit = 5 }, caller, ct, principal);
            related.Add(new EntityRelated(source.FullName, source.Title ?? source.Name, r.FromColumn, n,
                latest.Ok ? latest.Result!.Columns : [], latest.Ok ? latest.Result!.Rows : []));
        }

        return new EntityView(t.FullName, key, values, references, related, hidden.Distinct().ToList(), record.Result.Scope);
    }
}

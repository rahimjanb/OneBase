using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik.Catalog;

/// <summary>Итог обновления каталога: что найдено и что изменилось.</summary>
public sealed record CatalogRefreshResult(
    Guid RunId, bool Unchanged, int Tables, int Columns, int Relations, int Added, int Changed, int Removed, int Verified,
    IReadOnlyList<string> Changes, long DurationMs);

/// <summary>Каталог в памяти процесса: перечитывается после обновления или описания и не реже раза в 10 минут.</summary>
public sealed class CatalogCache
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CatalogModel? _model;
    private long _version;
    private long _loadedVersion = -1;
    private DateTimeOffset _loadedAt;

    public void Invalidate() => Interlocked.Increment(ref _version);

    public async Task<CatalogModel> GetAsync(IAnalytikDb db, CancellationToken ct)
    {
        var version = Interlocked.Read(ref _version);
        if (_model is { } ready && _loadedVersion == version && DateTimeOffset.UtcNow - _loadedAt < MaxAge)
        {
            return ready;
        }

        await _gate.WaitAsync(ct);
        try
        {
            version = Interlocked.Read(ref _version);
            if (_model is { } again && _loadedVersion == version && DateTimeOffset.UtcNow - _loadedAt < MaxAge)
            {
                return again;
            }

            var tables = await db.CatalogTables.AsNoTracking().Include(t => t.Columns).Where(t => t.RemovedAt == null).ToListAsync(ct);
            var relations = await db.CatalogRelations.AsNoTracking().Where(r => r.RemovedAt == null).ToListAsync(ct);
            var refreshed = await db.CatalogRuns.AsNoTracking().Where(r => r.FinishedAt != null && r.Error == null)
                .OrderByDescending(r => r.StartedAt).Select(r => (DateTimeOffset?)r.FinishedAt).FirstOrDefaultAsync(ct);
            _model = CatalogModel.From(tables, relations, refreshed);
            _loadedVersion = version;
            _loadedAt = DateTimeOffset.UtcNow;
            return _model;
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// Data Intelligence Engine — каталог базы OneBase. Обновление читает структуру из pg_catalog (все схемы, таблицы, представления, колонки,
/// ключи, индексы), сравнивает отпечаток с прошлым запуском и, если схема изменилась, переписывает каталог: новые таблицы и колонки
/// добавляются сами, исчезнувшие помечаются удалёнными, изменения пишутся в запуск (analytik.CatalogRuns). Описание таблицы — ручное
/// (analytik.Annotations) → знания OneBase (CatalogKnowledge) → автоматическое. Связи — внешние ключи, знания и выведенные по именам; выведенные
/// и заданные проверяются данными (доля значений, нашедшихся в целевой таблице) и только подтверждённые используются для соединений.
/// Только читает бизнес-данные; пишет лишь в схему analytik.
/// </summary>
public sealed class CatalogService(
    IAnalytikDb db,
    ISchemaReader reader,
    IAnalytikSql sql,
    CatalogCache cache,
    AnalytikOptions options,
    TimeProvider clock,
    ILogger<CatalogService> logger)
{
    /// <summary>Как часто перепроверять связи данными.</summary>
    private static readonly TimeSpan VerifyEvery = TimeSpan.FromHours(24);

    /// <summary>Как часто пересчитывать точное число строк небольших таблиц.</summary>
    private static readonly TimeSpan CountEvery = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public Task<CatalogModel> ModelAsync(CancellationToken ct) => cache.GetAsync(db, ct);

    public async Task<CatalogRefreshResult> RefreshAsync(string trigger, Guid? userId, bool force, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var now = clock.GetUtcNow();
        var run = new CatalogRun { StartedAt = now, Trigger = trigger, StartedById = userId };
        db.CatalogRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            var snapshot = await reader.ReadAsync(ct);
            var fingerprint = CatalogRules.Fingerprint(snapshot.Tables);
            var previous = await db.CatalogRuns.AsNoTracking()
                .Where(r => r.Id != run.Id && r.FinishedAt != null && r.Error == null)
                .OrderByDescending(r => r.StartedAt).Select(r => r.Fingerprint).FirstOrDefaultAsync(ct);
            var existing = await db.CatalogTables.Include(t => t.Columns).ToListAsync(ct);
            var annotations = await db.CatalogAnnotations.AsNoTracking().ToListAsync(ct);
            var changes = new List<string>();
            int added = 0, changed = 0, removed = 0;
            var unchanged = !force && previous == fingerprint && existing.Count > 0;

            var byName = existing.ToDictionary(t => $"{t.Schema}.{t.Name}", StringComparer.Ordinal);
            foreach (var t in snapshot.Tables)
            {
                var row = byName.GetValueOrDefault(t.FullName);
                var tableFingerprint = CatalogRules.Fingerprint(t);
                if (row is null)
                {
                    row = new CatalogTable
                    {
                        Schema = t.Schema, Name = t.Name, Kind = t.Kind, Module = "other", Access = CatalogAccess.Closed, Fingerprint = tableFingerprint,
                        FirstSeenAt = now,
                    };
                    db.CatalogTables.Add(row);
                    byName[t.FullName] = row;
                    added++;
                    changes.Add($"+ {t.FullName}");
                }
                else if (row.RemovedAt is not null)
                {
                    row.RemovedAt = null;
                    row.ChangedAt = now;
                    added++;
                    changes.Add($"+ {t.FullName} (снова есть)");
                }
                else if (row.Fingerprint != tableFingerprint)
                {
                    row.ChangedAt = now;
                    changed++;
                    changes.Add($"~ {t.FullName}: {DescribeChange(row, t)}");
                }

                row.Fingerprint = tableFingerprint;
                row.Kind = t.Kind;
                row.LastSeenAt = now;
                row.RowEstimate = t.RowEstimate;
                row.SizeBytes = t.SizeBytes;
                row.Comment = Trim(t.Comment, 2000);
                row.ColumnCount = t.Columns.Count;
                Describe(row, t, annotations);
                SyncColumns(db, row, t, annotations);
            }

            var seen = snapshot.Tables.Select(t => t.FullName).ToHashSet(StringComparer.Ordinal);
            foreach (var row in existing.Where(r => r.RemovedAt is null && !seen.Contains($"{r.Schema}.{r.Name}")))
            {
                row.RemovedAt = now;
                removed++;
                changes.Add($"− {row.Schema}.{row.Name}");
            }

            await db.SaveChangesAsync(ct);
            var relationCount = await SyncRelationsAsync(snapshot, annotations, now, ct);
            var verified = await VerifyRelationsAsync(snapshot, now, force, ct);
            await CountRowsAsync(byName.Values.Where(r => r.RemovedAt is null).ToList(), now, force, ct);

            run.FinishedAt = clock.GetUtcNow();
            run.Fingerprint = fingerprint;
            run.Tables = snapshot.Tables.Count;
            run.Columns = snapshot.Tables.Sum(t => t.Columns.Count);
            run.Relations = relationCount;
            run.Added = added;
            run.Changed = changed;
            run.Removed = removed;
            run.Unchanged = unchanged && added == 0 && changed == 0 && removed == 0;
            run.Changes = changes.Count == 0 ? null : JsonSerializer.Serialize(changes.Take(500), Json);
            run.DurationMs = watch.ElapsedMilliseconds;
            await db.SaveChangesAsync(ct);
            cache.Invalidate();
            if (changes.Count > 0)
            {
                logger.LogInformation("Аналитик: каталог обновлён — таблиц {Tables}, новых {Added}, изменённых {Changed}, исчезнувших {Removed}",
                    run.Tables, added, changed, removed);
            }

            return new CatalogRefreshResult(run.Id, run.Unchanged, run.Tables, run.Columns, relationCount, added, changed, removed, verified, changes,
                run.DurationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: каталог не обновлён");
            run.FinishedAt = clock.GetUtcNow();
            run.Error = Trim(ex.Message, 2000);
            run.DurationMs = watch.ElapsedMilliseconds;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static string DescribeChange(CatalogTable row, SchemaTable t)
    {
        var before = row.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var after = t.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var plus = after.Except(before).ToList();
        var minus = before.Except(after).ToList();
        var parts = new List<string>();
        if (plus.Count > 0)
        {
            parts.Add("новые колонки " + string.Join(", ", plus));
        }

        if (minus.Count > 0)
        {
            parts.Add("убраны " + string.Join(", ", minus));
        }

        return parts.Count == 0 ? "изменены типы или ключи" : string.Join("; ", parts);
    }

    /// <summary>Модуль, доступ, название и описание таблицы: ручное описание → знания OneBase → автоматическое.</summary>
    private static void Describe(CatalogTable row, SchemaTable t, IReadOnlyList<CatalogAnnotation> annotations)
    {
        var policy = CatalogRules.Policy(t.Schema, t.Name);
        var known = CatalogKnowledge.Table(t.Schema, t.Name);
        var manual = annotations.FirstOrDefault(a => a.Kind == "table" && a.Target == t.FullName);

        row.Module = manual?.Module ?? policy.Module;
        row.Access = manual?.Access ?? policy.Access;
        row.RequiredPermission = manual?.RequiredPermission ?? policy.RequiredPermission;
        row.ScopeRule = policy.ScopeRule;
        row.PrimaryKey = t.PrimaryKey.Count == 0 ? null : string.Join(",", t.PrimaryKey);
        row.DateColumn = known?.DateColumn is { } kd && t.Column(kd) is not null ? kd : CatalogRules.DateColumnOf(t);

        if (manual is { Description: { Length: > 0 } } || manual is { Title: { Length: > 0 } })
        {
            row.Title = manual.Title ?? known?.Title ?? Capitalize(CatalogRules.Words(t.Name));
            row.Description = manual.Description ?? known?.Description ?? AutoDescription(t);
            row.DescriptionSource = "manual";
        }
        else if (known is not null)
        {
            row.Title = known.Title;
            row.Description = known.Description;
            row.DescriptionSource = "knowledge";
        }
        else
        {
            row.Title = Capitalize(CatalogRules.Words(t.Name));
            row.Description = Trim(t.Comment, 2000) ?? AutoDescription(t);
            row.DescriptionSource = "auto";
        }

        if (row.Access == CatalogAccess.Open && t.Columns.Any(c => CatalogRules.SensitivityOf(t.Schema, t.Name, c.Name) == Sensitivity.Personal))
        {
            row.Access = CatalogAccess.Personal;
        }
    }

    /// <summary>Автоописание по структуре: что в таблице (колонки-ключи, даты, суммы).</summary>
    internal static string AutoDescription(SchemaTable t)
    {
        var refs = t.Columns.Where(c => c.Name.EndsWith("Id", StringComparison.Ordinal) && c.Name != "Id").Select(c => c.Name[..^2]).Take(6).ToList();
        var dates = t.Columns.Where(c => c.DataType == "date" || c.DataType.StartsWith("timestamp", StringComparison.Ordinal)).Select(c => c.Name).Take(4).ToList();
        var parts = new List<string> { $"Таблица {t.FullName} ({t.Columns.Count} колонок)" };
        if (refs.Count > 0)
        {
            parts.Add("ссылки: " + string.Join(", ", refs));
        }

        if (dates.Count > 0)
        {
            parts.Add("даты: " + string.Join(", ", dates));
        }

        return string.Join("; ", parts) + ". Описание составлено автоматически по структуре.";
    }

    private static void SyncColumns(IAnalytikDb db, CatalogTable row, SchemaTable t, IReadOnlyList<CatalogAnnotation> annotations)
    {
        var current = row.Columns.ToDictionary(c => c.Name, StringComparer.Ordinal);
        foreach (var c in t.Columns)
        {
            var target = $"{t.FullName}.{c.Name}";
            var known = CatalogKnowledge.Column(t.Schema, t.Name, c.Name);
            var manual = annotations.FirstOrDefault(a => a.Kind == "column" && a.Target == target);
            var pk = t.PrimaryKey.Contains(c.Name);
            if (!current.TryGetValue(c.Name, out var col))
            {
                col = new CatalogColumn { Name = c.Name, DataType = c.DataType, Role = ColumnRoles.Other, Sensitivity = Sensitivity.None, TableId = row.Id };
                row.Columns.Add(col);
                // Ключ задан заранее (Guid v7): без явного Add EF принял бы новую колонку существующей таблицы за изменённую строку.
                db.CatalogColumns.Add(col);
            }

            col.Ordinal = c.Ordinal;
            col.DataType = c.DataType;
            col.Nullable = c.Nullable;
            col.IsPrimaryKey = pk;
            col.Indexed = t.IndexedColumns.Contains(c.Name);
            col.IsUnique = t.IsUnique(c.Name);
            col.Role = known?.Role ?? CatalogRules.RoleOf(c, pk);
            col.Title = manual?.Title ?? known?.Title;
            col.Description = manual?.Description ?? known?.Description ?? Trim(c.Comment, 1000);
            col.Comment = Trim(c.Comment, 1000);
            // Ручная чувствительность может только скрыть колонку или открыть «personal»; секрет не открывается никогда.
            var auto = CatalogRules.SensitivityOf(t.Schema, t.Name, c.Name);
            col.Sensitivity = auto == Sensitivity.Secret ? Sensitivity.Secret : manual?.Sensitivity ?? auto;
        }

        var names = t.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var gone in row.Columns.Where(c => !names.Contains(c.Name)).ToList())
        {
            row.Columns.Remove(gone);
        }
    }

    private async Task<int> SyncRelationsAsync(SchemaSnapshot snapshot, IReadOnlyList<CatalogAnnotation> annotations, DateTimeOffset now, CancellationToken ct)
    {
        var candidates = RelationInference.Build(snapshot.Tables).ToList();
        // Ручные связи администратора: «schema.table.column>schema.table.column».
        foreach (var a in annotations.Where(a => a.Kind == "relation"))
        {
            var parts = a.Target.Split('>', 2);
            if (parts.Length == 2 && TrySplit(parts[0], out var f) && TrySplit(parts[1], out var to)
                && snapshot.Tables.FirstOrDefault(t => t.Schema == f.Schema && t.Name == f.Table)?.Column(f.Column) is not null
                && snapshot.Tables.FirstOrDefault(t => t.Schema == to.Schema && t.Name == to.Table)?.Column(to.Column) is not null)
            {
                candidates.RemoveAll(c => c.FromSchema == f.Schema && c.FromTable == f.Table && c.FromColumn == f.Column && c.Kind != RelationKinds.ForeignKey);
                candidates.Add(new RelationCandidate(f.Schema, f.Table, f.Column, to.Schema, to.Table, to.Column, RelationKinds.Manual, null, a.Description is { } d
                    && d.StartsWith("filter:", StringComparison.Ordinal) ? d[7..].Trim() : null));
            }
        }

        var existing = await db.CatalogRelations.ToListAsync(ct);
        var byKey = existing.ToDictionary(Key, StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in candidates)
        {
            keys.Add(c.Key);
            if (!byKey.TryGetValue(c.Key, out var row))
            {
                row = new CatalogRelation
                {
                    FromSchema = c.FromSchema, FromTable = c.FromTable, FromColumn = c.FromColumn, ToSchema = c.ToSchema, ToTable = c.ToTable,
                    ToColumn = c.ToColumn, Kind = c.Kind, Status = RelationStatus.Unverified, FirstSeenAt = now,
                };
                db.CatalogRelations.Add(row);
                byKey[c.Key] = row;
            }

            if (row.Kind != c.Kind)
            {
                row.Kind = c.Kind;
                row.VerifiedAt = null;
            }

            row.ConstraintName = c.ConstraintName;
            row.TargetFilter = c.TargetFilter;
            row.LastSeenAt = now;
            row.RemovedAt = null;
            if (c.Kind == RelationKinds.ForeignKey)
            {
                row.Status = RelationStatus.Confirmed;
                row.MatchRatio = 1;
                row.VerifiedAt ??= now;
            }
        }

        foreach (var row in existing.Where(r => r.RemovedAt is null && !keys.Contains(Key(r))))
        {
            row.RemovedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return keys.Count;
    }

    private static string Key(CatalogRelation r) => $"{r.FromSchema}.{r.FromTable}.{r.FromColumn}>{r.ToSchema}.{r.ToTable}.{r.ToColumn}";

    private static bool TrySplit(string target, out (string Schema, string Table, string Column) parts)
    {
        var p = target.Trim().Split('.', 3);
        parts = p.Length == 3 ? (p[0], p[1], p[2]) : default;
        return p.Length == 3;
    }

    /// <summary>
    /// Проверка выведенных и заданных связей данными: до 500 различных значений колонки (большие таблицы — по выборке страниц) — сколько из
    /// них есть в целевой таблице. Выведенная связь подтверждается от 90 %, заданная знаниями или администратором — от 50 % (бизнес-связь,
    /// у части строк цели может не быть: старые филиалы, «Завод»). Меньше половины — отклонена. Пустая колонка — не проверена.
    /// </summary>
    private async Task<int> VerifyRelationsAsync(SchemaSnapshot snapshot, DateTimeOffset now, bool force, CancellationToken ct)
    {
        var due = await db.CatalogRelations
            .Where(r => r.RemovedAt == null && r.Kind != RelationKinds.ForeignKey && (force || r.VerifiedAt == null || r.VerifiedAt < now - VerifyEvery))
            .ToListAsync(ct);
        var tables = snapshot.Tables.ToDictionary(t => t.FullName, StringComparer.Ordinal);
        var verified = 0;
        var failed = new List<string>();
        foreach (var r in due)
        {
            if (!tables.TryGetValue($"{r.FromSchema}.{r.FromTable}", out var from) || !tables.TryGetValue($"{r.ToSchema}.{r.ToTable}", out var to)
                || from.Kind != "table" && from.Kind != "partitioned" && from.Kind != "matview")
            {
                continue;
            }

            try
            {
                var (sqlText, parameters) = VerificationSql(r, from, to);
                var result = await sql.QueryAsync(sqlText, parameters, new SqlLimits(1, 10), ct);
                var row = result.Rows.FirstOrDefault();
                var checkedCount = Convert.ToInt32(row?[0] ?? 0);
                var matched = Convert.ToInt32(row?[1] ?? 0);
                r.Checked = checkedCount;
                r.MatchRatio = checkedCount == 0 ? null : Math.Round((decimal)matched / checkedCount, 4);
                var threshold = r.Kind == RelationKinds.Inferred ? 0.9m : 0.5m;
                r.Status = checkedCount == 0 ? RelationStatus.Unverified
                    : r.MatchRatio >= threshold ? RelationStatus.Confirmed
                    : r.MatchRatio < 0.5m ? RelationStatus.Rejected
                    : RelationStatus.Unverified;
                r.VerifiedAt = now;
                verified++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                failed.Add($"{r.FromSchema}.{r.FromTable}.{r.FromColumn}: {ex.Message}");
            }
        }

        if (failed.Count > 0)
        {
            logger.LogWarning("Аналитик: не проверено связей {Count}: {Failed}", failed.Count, string.Join("; ", failed.Take(5)));
        }

        await db.SaveChangesAsync(ct);
        return verified;
    }

    /// <summary>SQL проверки связи. Имена — только из прочитанной структуры (не из ввода), значение условия цели — параметром.</summary>
    internal static (string Sql, IReadOnlyList<SqlParam> Parameters) VerificationSql(CatalogRelation r, SchemaTable from, SchemaTable to)
    {
        // Процент выборки — в инвариантной культуре: в русской «57,14» стало бы двумя аргументами TABLESAMPLE.
        var percent = Math.Clamp(20_000_000.0 / from.RowEstimate, 0.5, 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var sample = from.RowEstimate > 200_000 ? $" TABLESAMPLE SYSTEM ({percent})" : "";
        var parameters = new List<SqlParam>();
        var filter = "";
        if (r.TargetFilter is { } f && f.Split('=', 2) is [var column, var value] && to.Column(column.Trim()) is { } filterColumn)
        {
            filter = $" AND t.{Q(filterColumn.Name)} = @f";
            parameters.Add(new SqlParam("f", value.Trim()));
        }

        var text = $"""
            SELECT count(*)::int, count(*) FILTER (WHERE EXISTS (SELECT 1 FROM {Q(to.Schema)}.{Q(to.Name)} t WHERE t.{Q(r.ToColumn)} = x.v{filter}))::int
            FROM (SELECT DISTINCT s.{Q(r.FromColumn)} AS v FROM {Q(from.Schema)}.{Q(from.Name)} s{sample} WHERE s.{Q(r.FromColumn)} IS NOT NULL LIMIT 500) x
            """;
        return (text, parameters);
    }

    /// <summary>Точное число строк небольших таблиц (оценка ниже порога) — не чаще раза в 6 часов, остальным — оценка PostgreSQL.</summary>
    private async Task CountRowsAsync(IReadOnlyList<CatalogTable> tables, DateTimeOffset now, bool force, CancellationToken ct)
    {
        foreach (var t in tables.Where(t => t.Kind is "table" or "partitioned" && t.RowEstimate <= options.ExactCountThreshold
                     && (force || t.StatsAt is null || t.StatsAt < now - CountEvery)))
        {
            try
            {
                var result = await sql.QueryAsync($"SELECT count(*) FROM {Q(t.Schema)}.{Q(t.Name)}", [], new SqlLimits(1, 10), ct);
                t.RowCount = Convert.ToInt64(result.Rows[0][0]);
                t.StatsAt = now;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Аналитик: не посчитаны строки {Table}", $"{t.Schema}.{t.Name}");
            }
        }

        foreach (var t in tables.Where(t => t.RowEstimate > options.ExactCountThreshold))
        {
            t.RowCount = null;
            t.StatsAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    // ---- чтение каталога ----

    /// <summary>Ручное описание таблицы, колонки, связи или показателя. Сразу применяется к каталогу.</summary>
    public async Task<CatalogAnnotation> AnnotateAsync(string kind, string target, string? title, string? description, string? module, string? access,
        string? permission, string? sensitivity, Guid userId, CancellationToken ct)
    {
        if (kind is not ("table" or "column" or "relation" or "metric"))
        {
            throw new ArgumentException("Вид описания: table, column, relation или metric.");
        }

        if (access is not null and not (CatalogAccess.Open or CatalogAccess.Personal or CatalogAccess.Closed))
        {
            throw new ArgumentException("Доступ: open, personal или closed.");
        }

        if (sensitivity is not null and not (Sensitivity.None or Sensitivity.Personal or Sensitivity.Internal))
        {
            throw new ArgumentException("Чувствительность: none, personal или internal (секрет открыть нельзя).");
        }

        if (module is not null && !CatalogRules.Modules.Contains(module))
        {
            throw new ArgumentException("Неизвестный модуль: " + module);
        }

        target = target.Trim();
        var row = await db.CatalogAnnotations.FirstOrDefaultAsync(a => a.Kind == kind && a.Target == target, ct);
        if (row is null)
        {
            row = new CatalogAnnotation { Kind = kind, Target = target };
            db.CatalogAnnotations.Add(row);
        }

        row.Title = Blank(title);
        row.Description = Blank(description);
        row.Module = Blank(module);
        row.Access = Blank(access);
        row.RequiredPermission = Blank(permission);
        row.Sensitivity = Blank(sensitivity);
        row.UpdatedById = userId;
        row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>Последние запуски обновления каталога.</summary>
    public async Task<IReadOnlyList<CatalogRun>> RunsAsync(int take, CancellationToken ct) =>
        await db.CatalogRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(take).ToListAsync(ct);

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Trim(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Length <= max ? s : s[..max];

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Идентификатор PostgreSQL в кавычках (кавычки внутри удваиваются).</summary>
    public static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}

using System.Data;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using OneBase.Application.Analytik;
using OneBase.Domain.Analytik;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Analytik;

/// <summary>Таблицы схемы analytik поверх того же DbContext области (одно SaveChangesAsync с остальными изменениями).</summary>
internal sealed class AnalytikDb(OneBaseDbContext db) : IAnalytikDb
{
    public Microsoft.EntityFrameworkCore.DbSet<CatalogTable> CatalogTables => db.Set<CatalogTable>();
    public Microsoft.EntityFrameworkCore.DbSet<CatalogColumn> CatalogColumns => db.Set<CatalogColumn>();
    public Microsoft.EntityFrameworkCore.DbSet<CatalogRelation> CatalogRelations => db.Set<CatalogRelation>();
    public Microsoft.EntityFrameworkCore.DbSet<CatalogRun> CatalogRuns => db.Set<CatalogRun>();
    public Microsoft.EntityFrameworkCore.DbSet<CatalogAnnotation> CatalogAnnotations => db.Set<CatalogAnnotation>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikQuery> Queries => db.Set<AnalytikQuery>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikInsight> Insights => db.Set<AnalytikInsight>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikForecast> Forecasts => db.Set<AnalytikForecast>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikReport> Reports => db.Set<AnalytikReport>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikJobRun> JobRuns => db.Set<AnalytikJobRun>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikMetricSnapshot> MetricSnapshots => db.Set<AnalytikMetricSnapshot>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikScenarioBaseline> ScenarioBaselines => db.Set<AnalytikScenarioBaseline>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikScenario> Scenarios => db.Set<AnalytikScenario>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikComputation> Computations => db.Set<AnalytikComputation>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikInvestigation> Investigations => db.Set<AnalytikInvestigation>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikInsightEvent> InsightEvents => db.Set<AnalytikInsightEvent>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikDecision> Decisions => db.Set<AnalytikDecision>();
    public Microsoft.EntityFrameworkCore.DbSet<AnalytikBenchmarkRun> BenchmarkRuns => db.Set<AnalytikBenchmarkRun>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Выполнение SQL аналитического движка. Каждый запрос — в своей транзакции READ ONLY (запись невозможна на уровне PostgreSQL, даже если
/// текст SQL её содержит), со statement_timeout и lock_timeout, затем ROLLBACK. Подключение — ConnectionStrings:AnalytikReadOnly (отдельная роль
/// только на чтение, deploy/analytik-readonly.sql) или основное; имя приложения «onebase-analytik» видно в pg_stat_activity; пул — не больше
/// Analytik:MaxConcurrentQueries соединений, сверх — ожидание очереди, а не новые соединения.
/// </summary>
internal sealed class AnalytikSqlRunner : IAnalytikSql, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _slots;
    private readonly ILogger<AnalytikSqlRunner> _logger;

    public AnalytikSqlRunner(IConfiguration config, AnalytikOptions options, ILogger<AnalytikSqlRunner> logger)
    {
        _logger = logger;
        var dedicated = config.GetConnectionString("AnalytikReadOnly");
        DedicatedReadOnlyRole = !string.IsNullOrWhiteSpace(dedicated);
        var source = DedicatedReadOnlyRole ? dedicated! : config.GetConnectionString("Postgres") ?? "";
        var builder = new NpgsqlConnectionStringBuilder(source)
        {
            ApplicationName = "onebase-analytik",
            MaxPoolSize = Math.Max(1, options.MaxConcurrentQueries),
            // Время всего запроса держит statement_timeout; этот предел — страховка на случай, если сервер не ответит вовсе.
            CommandTimeout = Math.Max(5, options.QueryTimeoutSeconds + 10),
        };
        _connectionString = builder.ConnectionString;
        _slots = new SemaphoreSlim(Math.Max(1, options.MaxConcurrentQueries));
    }

    public bool DedicatedReadOnlyRole { get; }

    public async Task<SqlResult> QueryAsync(string sql, IReadOnlyList<SqlParam> parameters, SqlLimits limits, CancellationToken ct)
    {
        await _slots.WaitAsync(ct);
        var watch = Stopwatch.StartNew();
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var timeoutMs = Math.Clamp(limits.TimeoutSeconds, 1, 120) * 1000;
            await using (var setup = new NpgsqlCommand(
                             $"SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = {timeoutMs}; SET LOCAL lock_timeout = 2000; " +
                             "SET LOCAL idle_in_transaction_session_timeout = 60000;", connection, tx))
            {
                await setup.ExecuteNonQueryAsync(ct);
            }

            await using var command = new NpgsqlCommand(sql, connection, tx);
            foreach (var p in parameters)
            {
                command.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
            }

            var rows = new List<object?[]>();
            var truncated = false;
            string[] columns;
            string[] types;
            await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, ct))
            {
                columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                types = Enumerable.Range(0, reader.FieldCount).Select(reader.GetDataTypeName).ToArray();
                while (await reader.ReadAsync(ct))
                {
                    if (rows.Count >= limits.MaxRows)
                    {
                        truncated = true;
                        break;
                    }

                    var values = new object?[reader.FieldCount];
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }

                    rows.Add(values);
                }
            }

            await tx.RollbackAsync(CancellationToken.None);
            return new SqlResult(columns, types, rows, truncated, watch.ElapsedMilliseconds);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.QueryCanceled && !ct.IsCancellationRequested)
        {
            _logger.LogInformation("Аналитик: запрос остановлен по лимиту времени ({Seconds} с)", limits.TimeoutSeconds);
            throw new AnalytikTimeoutException($"Запрос не уложился в {limits.TimeoutSeconds} с — сузьте период или условия.", ex);
        }
        finally
        {
            _slots.Release();
        }
    }

    public void Dispose() => _slots.Dispose();
}

using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Analytik;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "analytik": каталог базы (таблицы, колонки, связи, запуски обнаружения, ручные описания), журнал аналитических запросов, находки
/// рисков и отклонений, прогнозы, отчёты, запуски фоновых задач и дневные агрегаты. Бизнес-данные разделов здесь не хранятся — Аналитик читает
/// их на месте. Имена ключей и индексов — явные, как в "twin" и "procurement".
/// </summary>
internal static class AnalytikModel
{
    private const string Schema = "analytik";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<CatalogTable>(e =>
        {
            e.ToTable("CatalogTables", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikCatalogTables");
            e.Property(x => x.Schema).HasMaxLength(63);
            e.Property(x => x.Name).HasMaxLength(63);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Module).HasMaxLength(32);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.DescriptionSource).HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Access).HasMaxLength(16);
            e.Property(x => x.RequiredPermission).HasMaxLength(300);
            e.Property(x => x.ScopeRule).HasMaxLength(64);
            e.Property(x => x.DateColumn).HasMaxLength(63);
            e.Property(x => x.PrimaryKey).HasMaxLength(300);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.HasIndex(x => new { x.Schema, x.Name }).IsUnique().HasDatabaseName("IX_AnalytikCatalogTables_Schema_Name");
            e.HasIndex(x => x.Module).HasDatabaseName("IX_AnalytikCatalogTables_Module");
            e.HasMany(x => x.Columns).WithOne(x => x.Table).HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AnalytikCatalogColumns_AnalytikCatalogTables_TableId");
        });

        b.Entity<CatalogColumn>(e =>
        {
            e.ToTable("CatalogColumns", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikCatalogColumns");
            e.Property(x => x.Name).HasMaxLength(63);
            e.Property(x => x.DataType).HasMaxLength(100);
            e.Property(x => x.Role).HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Sensitivity).HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasIndex(x => new { x.TableId, x.Name }).IsUnique().HasDatabaseName("IX_AnalytikCatalogColumns_TableId_Name");
        });

        b.Entity<CatalogRelation>(e =>
        {
            e.ToTable("CatalogRelations", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikCatalogRelations");
            e.Property(x => x.FromSchema).HasMaxLength(63);
            e.Property(x => x.FromTable).HasMaxLength(63);
            e.Property(x => x.FromColumn).HasMaxLength(300);
            e.Property(x => x.ToSchema).HasMaxLength(63);
            e.Property(x => x.ToTable).HasMaxLength(63);
            e.Property(x => x.ToColumn).HasMaxLength(300);
            e.Property(x => x.TargetFilter).HasMaxLength(200);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.ConstraintName).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.MatchRatio).HasPrecision(5, 4);
            e.HasIndex(x => new { x.FromSchema, x.FromTable, x.FromColumn, x.ToSchema, x.ToTable, x.ToColumn }).IsUnique()
                .HasDatabaseName("IX_AnalytikCatalogRelations_Edge");
            e.HasIndex(x => new { x.ToSchema, x.ToTable }).HasDatabaseName("IX_AnalytikCatalogRelations_To");
        });

        b.Entity<CatalogRun>(e =>
        {
            e.ToTable("CatalogRuns", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikCatalogRuns");
            e.Property(x => x.Trigger).HasMaxLength(16);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.Changes).HasColumnType("jsonb");
            e.Property(x => x.Error).HasMaxLength(2000);
            e.HasIndex(x => x.StartedAt).HasDatabaseName("IX_AnalytikCatalogRuns_StartedAt");
        });

        b.Entity<CatalogAnnotation>(e =>
        {
            e.ToTable("Annotations", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikAnnotations");
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Target).HasMaxLength(300);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Module).HasMaxLength(32);
            e.Property(x => x.Access).HasMaxLength(16);
            e.Property(x => x.RequiredPermission).HasMaxLength(300);
            e.Property(x => x.Sensitivity).HasMaxLength(16);
            e.HasIndex(x => new { x.Kind, x.Target }).IsUnique().HasDatabaseName("IX_AnalytikAnnotations_Kind_Target");
        });

        b.Entity<AnalytikQuery>(e =>
        {
            e.ToTable("Queries", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikQueries");
            e.Property(x => x.Tenant).HasMaxLength(64);
            e.Property(x => x.Origin).HasMaxLength(16);
            e.Property(x => x.AgentCode).HasMaxLength(64);
            e.Property(x => x.Plan).HasColumnType("jsonb");
            e.Property(x => x.Parameters).HasColumnType("jsonb");
            e.Property(x => x.Tables).HasMaxLength(2000);
            e.Property(x => x.Scope).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("IX_AnalytikQueries_UserId_CreatedAt");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_AnalytikQueries_CreatedAt");
        });

        b.Entity<AnalytikInsight>(e =>
        {
            e.ToTable("Insights", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikInsights");
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Module).HasMaxLength(32);
            e.Property(x => x.Rule).HasMaxLength(64);
            e.Property(x => x.Key).HasMaxLength(300);
            e.Property(x => x.Severity).HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Cause).HasMaxLength(2000);
            e.Property(x => x.Hypotheses).HasColumnType("jsonb");
            e.Property(x => x.Impact).HasPrecision(20, 3);
            e.Property(x => x.ImpactUnit).HasMaxLength(32);
            e.Property(x => x.Consequences).HasMaxLength(2000);
            e.Property(x => x.Recommendations).HasColumnType("jsonb");
            e.Property(x => x.Evidence).HasColumnType("jsonb");
            e.Property(x => x.Href).HasMaxLength(500);
            e.Property(x => x.RequiredPermission).HasMaxLength(300);
            e.Property(x => x.NotifiedSeverity).HasMaxLength(16);
            // Одна активная находка на ключ: повтор обновляет её, а не создаёт новую.
            e.HasIndex(x => x.Key).IsUnique().HasFilter("\"ResolvedAt\" IS NULL").HasDatabaseName("IX_AnalytikInsights_Key_Active");
            e.HasIndex(x => new { x.Module, x.DetectedAt }).HasDatabaseName("IX_AnalytikInsights_Module_DetectedAt");
        });

        b.Entity<AnalytikForecast>(e =>
        {
            e.ToTable("Forecasts", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikForecasts");
            e.Property(x => x.Metric).HasMaxLength(100);
            e.Property(x => x.Dimension).HasMaxLength(100);
            e.Property(x => x.Granularity).HasMaxLength(8);
            e.Property(x => x.Method).HasMaxLength(32);
            e.Property(x => x.Predicted).HasPrecision(20, 3);
            e.Property(x => x.Lower).HasPrecision(20, 3);
            e.Property(x => x.Upper).HasPrecision(20, 3);
            e.Property(x => x.Actual).HasPrecision(20, 3);
            e.HasIndex(x => new { x.Metric, x.Dimension, x.Granularity, x.MadeOn, x.TargetDate }).IsUnique()
                .HasDatabaseName("IX_AnalytikForecasts_Key");
        });

        b.Entity<AnalytikReport>(e =>
        {
            e.ToTable("Reports", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikReports");
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Content).HasColumnType("jsonb");
            e.HasIndex(x => new { x.Kind, x.CreatedAt }).HasDatabaseName("IX_AnalytikReports_Kind_CreatedAt");
        });

        b.Entity<AnalytikJobRun>(e =>
        {
            e.ToTable("JobRuns", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikJobRuns");
            e.Property(x => x.Job).HasMaxLength(16);
            e.Property(x => x.PeriodKey).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Summary).HasColumnType("jsonb");
            e.Property(x => x.Error).HasMaxLength(2000);
            e.HasIndex(x => new { x.Job, x.PeriodKey }).IsUnique().HasDatabaseName("IX_AnalytikJobRuns_Job_PeriodKey");
        });

        b.Entity<AnalytikMetricSnapshot>(e =>
        {
            e.ToTable("MetricSnapshots", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikMetricSnapshots");
            e.Property(x => x.Metric).HasMaxLength(100);
            e.Property(x => x.Dimension).HasMaxLength(100);
            e.Property(x => x.Value).HasPrecision(24, 4);
            e.Property(x => x.Unit).HasMaxLength(16);
            e.HasIndex(x => new { x.Metric, x.Dimension, x.Date }).IsUnique().HasDatabaseName("IX_AnalytikMetricSnapshots_Key");
        });
    }
}

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
            e.Property(x => x.Confidence).HasPrecision(5, 4);
            e.Property(x => x.EvidenceClass).HasMaxLength(16);
            e.Property(x => x.Investigation).HasColumnType("jsonb");
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

        b.Entity<AnalytikScenarioBaseline>(e =>
        {
            e.ToTable("ScenarioBaselines", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikScenarioBaselines");
            e.Property(x => x.Tenant).HasMaxLength(64);
            e.Property(x => x.FormulaVersion).HasMaxLength(16);
            e.Property(x => x.Sections).HasMaxLength(200);
            e.Property(x => x.ScopeKey).HasMaxLength(4000);
            e.Property(x => x.SalesScope).HasMaxLength(500);
            e.Property(x => x.Data).HasColumnType("jsonb");
            e.Property(x => x.Sources).HasColumnType("jsonb");
            e.Property(x => x.Hash).HasMaxLength(64);
            e.HasIndex(x => new { x.Tenant, x.CreatedById, x.CreatedAt }).HasDatabaseName("IX_AnalytikScenarioBaselines_Tenant_Author");
            e.HasIndex(x => new { x.Pinned, x.CreatedAt }).HasDatabaseName("IX_AnalytikScenarioBaselines_Pinned_CreatedAt");
        });

        b.Entity<AnalytikScenario>(e =>
        {
            e.ToTable("Scenarios", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikScenarios");
            e.Property(x => x.Tenant).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Preset).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.NewRegionName).HasMaxLength(200);
            e.Property(x => x.Parameters).HasColumnType("jsonb");
            e.Property(x => x.Result).HasColumnType("jsonb");
            e.Property(x => x.Headline).HasMaxLength(2000);
            e.Property(x => x.FormulaVersion).HasMaxLength(16);
            e.Property(x => x.History).HasColumnType("jsonb");
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.Tenant, x.OwnerId, x.Status }).HasDatabaseName("IX_AnalytikScenarios_Tenant_Owner_Status");
            e.HasOne(x => x.Baseline).WithMany().HasForeignKey(x => x.BaselineId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AnalytikScenarios_AnalytikScenarioBaselines_BaselineId");
        });

        ConfigureIntelligence(b);
    }

    /// <summary>
    /// Advanced Intelligence: журнал расчётов, исследования, история находок, решения и прогоны набора сложных вопросов. Внешних ключей нет:
    /// записи журнала живут независимо от того, на что ссылаются (исследование, находка, чат).
    /// </summary>
    private static void ConfigureIntelligence(ModelBuilder b)
    {
        b.Entity<AnalytikComputation>(e =>
        {
            e.ToTable("Computations", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikComputations");
            e.Property(x => x.Origin).HasMaxLength(16);
            e.Property(x => x.Method).HasMaxLength(48);
            e.Property(x => x.Domain).HasMaxLength(32);
            e.Property(x => x.Algorithm).HasMaxLength(300);
            e.Property(x => x.Version).HasMaxLength(16);
            e.Property(x => x.Parameters).HasColumnType("jsonb");
            e.Property(x => x.Input).HasColumnType("jsonb");
            e.Property(x => x.Result).HasColumnType("jsonb");
            e.Property(x => x.Sources).HasColumnType("jsonb");
            e.Property(x => x.Limitations).HasColumnType("jsonb");
            e.Property(x => x.EvidenceClass).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.Property(x => x.Scope).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("IX_AnalytikComputations_UserId_CreatedAt");
            e.HasIndex(x => x.InvestigationId).HasDatabaseName("IX_AnalytikComputations_InvestigationId");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_AnalytikComputations_CreatedAt");
        });

        b.Entity<AnalytikInvestigation>(e =>
        {
            e.ToTable("Investigations", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikInvestigations");
            e.Property(x => x.Origin).HasMaxLength(16);
            e.Property(x => x.Question).HasMaxLength(4000);
            e.Property(x => x.Goal).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Roles).HasColumnType("jsonb");
            e.Property(x => x.Plan).HasColumnType("jsonb");
            e.Property(x => x.Steps).HasColumnType("jsonb");
            e.Property(x => x.Hypotheses).HasColumnType("jsonb");
            e.Property(x => x.Contradictions).HasColumnType("jsonb");
            e.Property(x => x.Reviews).HasColumnType("jsonb");
            e.Property(x => x.Trace).HasColumnType("jsonb");
            e.Property(x => x.Limitations).HasColumnType("jsonb");
            e.Property(x => x.Summary).HasMaxLength(4000);
            e.Property(x => x.CostUsd).HasPrecision(18, 6);
            e.Property(x => x.FailureReason).HasMaxLength(64);
            e.HasIndex(x => new { x.UserId, x.StartedAt }).HasDatabaseName("IX_AnalytikInvestigations_UserId_StartedAt");
            e.HasIndex(x => x.StartedAt).HasDatabaseName("IX_AnalytikInvestigations_StartedAt");
            e.HasIndex(x => x.TurnId).HasDatabaseName("IX_AnalytikInvestigations_TurnId");
        });

        b.Entity<AnalytikInsightEvent>(e =>
        {
            e.ToTable("InsightEvents", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikInsightEvents");
            e.Property(x => x.Event).HasMaxLength(16);
            e.Property(x => x.Severity).HasMaxLength(16);
            e.Property(x => x.Impact).HasPrecision(20, 3);
            e.Property(x => x.Note).HasMaxLength(2000);
            e.Property(x => x.Data).HasColumnType("jsonb");
            e.HasIndex(x => new { x.InsightId, x.At }).HasDatabaseName("IX_AnalytikInsightEvents_InsightId_At");
        });

        b.Entity<AnalytikDecision>(e =>
        {
            e.ToTable("Decisions", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikDecisions");
            e.Property(x => x.Tenant).HasMaxLength(64);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Reason).HasMaxLength(4000);
            e.Property(x => x.Module).HasMaxLength(32);
            e.Property(x => x.Metric).HasColumnType("jsonb");
            e.Property(x => x.MetricTitle).HasMaxLength(300);
            e.Property(x => x.BaselineValue).HasPrecision(24, 4);
            e.Property(x => x.Baseline).HasColumnType("jsonb");
            e.Property(x => x.ExpectedDirection).HasMaxLength(8);
            e.Property(x => x.ExpectedChangePct).HasPrecision(12, 4);
            e.Property(x => x.ExpectedText).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Outcome).HasMaxLength(16);
            e.Property(x => x.ActualValue).HasPrecision(24, 4);
            e.Property(x => x.Actual).HasColumnType("jsonb");
            e.Property(x => x.Links).HasColumnType("jsonb");
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.Tenant, x.Status, x.ReviewOn }).HasDatabaseName("IX_AnalytikDecisions_Tenant_Status_ReviewOn");
            e.HasIndex(x => new { x.AuthorId, x.DecidedOn }).HasDatabaseName("IX_AnalytikDecisions_AuthorId_DecidedOn");
        });

        b.Entity<AnalytikBenchmarkRun>(e =>
        {
            e.ToTable("BenchmarkRuns", Schema);
            e.HasKey(x => x.Id).HasName("PK_AnalytikBenchmarkRuns");
            e.Property(x => x.Mode).HasMaxLength(16);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Score).HasPrecision(7, 4);
            e.Property(x => x.ByCategory).HasColumnType("jsonb");
            e.Property(x => x.Results).HasColumnType("jsonb");
            e.Property(x => x.CostUsd).HasPrecision(18, 6);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.HasIndex(x => x.StartedAt).HasDatabaseName("IX_AnalytikBenchmarkRuns_StartedAt");
        });
    }
}

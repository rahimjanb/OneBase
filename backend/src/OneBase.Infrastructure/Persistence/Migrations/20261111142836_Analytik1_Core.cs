using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analytik1_Core : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analytik");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "ai",
                table: "Conversations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "consultant");

            migrationBuilder.CreateTable(
                name: "Annotations",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Target = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Module = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Access = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RequiredPermission = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Sensitivity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikAnnotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRelations",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromSchema = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    FromTable = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    FromColumn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ToSchema = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    ToTable = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    ToColumn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TargetFilter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConstraintName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MatchRatio = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    Checked = table.Column<int>(type: "integer", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikCatalogRelations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRuns",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Tables = table.Column<int>(type: "integer", nullable: false),
                    Columns = table.Column<int>(type: "integer", nullable: false),
                    Relations = table.Column<int>(type: "integer", nullable: false),
                    Added = table.Column<int>(type: "integer", nullable: false),
                    Changed = table.Column<int>(type: "integer", nullable: false),
                    Removed = table.Column<int>(type: "integer", nullable: false),
                    Unchanged = table.Column<bool>(type: "boolean", nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    StartedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikCatalogRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogTables",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Schema = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    Name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Module = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DescriptionSource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RowEstimate = table.Column<long>(type: "bigint", nullable: false),
                    RowCount = table.Column<long>(type: "bigint", nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ColumnCount = table.Column<int>(type: "integer", nullable: false),
                    Access = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequiredPermission = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ScopeRule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DateColumn = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    PrimaryKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikCatalogTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Forecasts",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Granularity = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MadeOn = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Horizon = table.Column<int>(type: "integer", nullable: false),
                    Predicted = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: false),
                    Lower = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: false),
                    Upper = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: false),
                    Actual = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikForecasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Insights",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Module = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Cause = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CauseConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    Hypotheses = table.Column<string>(type: "jsonb", nullable: true),
                    Impact = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    ImpactUnit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Consequences = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Recommendations = table.Column<string>(type: "jsonb", nullable: true),
                    Evidence = table.Column<string>(type: "jsonb", nullable: true),
                    Href = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequiredPermission = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Occurrences = table.Column<int>(type: "integer", nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotifiedSeverity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikInsights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobRuns",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Job = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Summary = table.Column<string>(type: "jsonb", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikJobRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricSnapshots",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Metric = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikMetricSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Queries",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tenant = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AgentCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Plan = table.Column<string>(type: "jsonb", nullable: false),
                    Sql = table.Column<string>(type: "text", nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: true),
                    Tables = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    Truncated = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    JobRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogColumns",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    DataType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Nullable = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrimaryKey = table.Column<bool>(type: "boolean", nullable: false),
                    Indexed = table.Column<bool>(type: "boolean", nullable: false),
                    IsUnique = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Sensitivity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikCatalogColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalytikCatalogColumns_AnalytikCatalogTables_TableId",
                        column: x => x.TableId,
                        principalSchema: "analytik",
                        principalTable: "CatalogTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikAnnotations_Kind_Target",
                schema: "analytik",
                table: "Annotations",
                columns: new[] { "Kind", "Target" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikCatalogColumns_TableId_Name",
                schema: "analytik",
                table: "CatalogColumns",
                columns: new[] { "TableId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikCatalogRelations_Edge",
                schema: "analytik",
                table: "CatalogRelations",
                columns: new[] { "FromSchema", "FromTable", "FromColumn", "ToSchema", "ToTable", "ToColumn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikCatalogRelations_To",
                schema: "analytik",
                table: "CatalogRelations",
                columns: new[] { "ToSchema", "ToTable" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikCatalogRuns_StartedAt",
                schema: "analytik",
                table: "CatalogRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikCatalogTables_Module",
                schema: "analytik",
                table: "CatalogTables",
                column: "Module");

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikCatalogTables_Schema_Name",
                schema: "analytik",
                table: "CatalogTables",
                columns: new[] { "Schema", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikForecasts_Key",
                schema: "analytik",
                table: "Forecasts",
                columns: new[] { "Metric", "Dimension", "Granularity", "MadeOn", "TargetDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikInsights_Key_Active",
                schema: "analytik",
                table: "Insights",
                column: "Key",
                unique: true,
                filter: "\"ResolvedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikInsights_Module_DetectedAt",
                schema: "analytik",
                table: "Insights",
                columns: new[] { "Module", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikJobRuns_Job_PeriodKey",
                schema: "analytik",
                table: "JobRuns",
                columns: new[] { "Job", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikMetricSnapshots_Key",
                schema: "analytik",
                table: "MetricSnapshots",
                columns: new[] { "Metric", "Dimension", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikQueries_CreatedAt",
                schema: "analytik",
                table: "Queries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikQueries_UserId_CreatedAt",
                schema: "analytik",
                table: "Queries",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikReports_Kind_CreatedAt",
                schema: "analytik",
                table: "Reports",
                columns: new[] { "Kind", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Annotations",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "CatalogColumns",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "CatalogRelations",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "CatalogRuns",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "Forecasts",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "Insights",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "JobRuns",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "MetricSnapshots",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "Queries",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "Reports",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "CatalogTables",
                schema: "analytik");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "ai",
                table: "Conversations");
        }
    }
}

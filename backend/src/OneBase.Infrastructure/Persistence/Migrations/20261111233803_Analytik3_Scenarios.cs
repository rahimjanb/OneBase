using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analytik3_Scenarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScenarioBaselines",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tenant = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "date", nullable: false),
                    FormulaVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Sections = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SalesScope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    Sources = table.Column<string>(type: "jsonb", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Pinned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikScenarioBaselines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scenarios",
                schema: "analytik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tenant = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Preset = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    HorizonDays = table.Column<int>(type: "integer", nullable: false),
                    NewRegionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    Result = table.Column<string>(type: "jsonb", nullable: true),
                    Headline = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AiAnalysis = table.Column<string>(type: "text", nullable: true),
                    AiAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FormulaVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    History = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CopiedFromId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalytikScenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalytikScenarios_AnalytikScenarioBaselines_BaselineId",
                        column: x => x.BaselineId,
                        principalSchema: "analytik",
                        principalTable: "ScenarioBaselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikScenarioBaselines_Pinned_CreatedAt",
                schema: "analytik",
                table: "ScenarioBaselines",
                columns: new[] { "Pinned", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikScenarioBaselines_Tenant_Author",
                schema: "analytik",
                table: "ScenarioBaselines",
                columns: new[] { "Tenant", "CreatedById", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalytikScenarios_Tenant_Owner_Status",
                schema: "analytik",
                table: "Scenarios",
                columns: new[] { "Tenant", "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_BaselineId",
                schema: "analytik",
                table: "Scenarios",
                column: "BaselineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Scenarios",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "ScenarioBaselines",
                schema: "analytik");
        }
    }
}

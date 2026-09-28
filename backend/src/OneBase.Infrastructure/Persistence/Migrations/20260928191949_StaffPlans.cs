using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlanSecretHint",
                table: "Integrations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedPlanSecret",
                table: "Integrations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffPlans",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    LinkoUserId = table.Column<long>(type: "bigint", nullable: false),
                    IndicatorId = table.Column<long>(type: "bigint", nullable: false),
                    IndicatorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PlanType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    PlanAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    SalesAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    FactAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PlanForecast = table.Column<decimal>(type: "numeric", nullable: false),
                    FactPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    ForecastPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    LoadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffPlans_Year_Month_LinkoUserId_IndicatorId",
                schema: "sales",
                table: "StaffPlans",
                columns: new[] { "Year", "Month", "LinkoUserId", "IndicatorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffPlans",
                schema: "sales");

            migrationBuilder.DropColumn(
                name: "PlanSecretHint",
                table: "Integrations");

            migrationBuilder.DropColumn(
                name: "ProtectedPlanSecret",
                table: "Integrations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyPdfSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PdfLanguage",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeeklyPdfSettings",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RequireCompleteData = table.Column<bool>(type: "boolean", nullable: false),
                    SvTpCards = table.Column<bool>(type: "boolean", nullable: false),
                    GoalAkbFactor = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    GoalPromoFactor = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    GoalPromoMin = table.Column<int>(type: "integer", nullable: true),
                    ReturnShare15To30 = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    ReturnShare30Plus = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    SleepFromDays = table.Column<int>(type: "integer", nullable: true),
                    SleepDays = table.Column<int>(type: "integer", nullable: true),
                    DeficitDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyPdfSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyPdfSettings",
                schema: "telegram");

            migrationBuilder.DropColumn(
                name: "PdfLanguage",
                schema: "telegram",
                table: "ReportSubscriptions");
        }
    }
}

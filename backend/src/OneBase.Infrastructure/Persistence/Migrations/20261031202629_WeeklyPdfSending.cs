using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyPdfSending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeMoney",
                schema: "telegram",
                table: "WeeklyPdfSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true); // как в образцах: суммы КПУ и DSO в PDF — и у уже сохранённых настроек

            migrationBuilder.AddColumn<string>(
                name: "Files",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Log",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeMoney",
                schema: "telegram",
                table: "WeeklyPdfSettings");

            migrationBuilder.DropColumn(
                name: "Files",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.DropColumn(
                name: "Log",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.DropColumn(
                name: "Stage",
                schema: "telegram",
                table: "ReportDeliveries");
        }
    }
}

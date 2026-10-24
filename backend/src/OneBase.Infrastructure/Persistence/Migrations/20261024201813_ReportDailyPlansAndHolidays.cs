using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportDailyPlansAndHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportDailyPlans",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkoUserId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MonthPlanSum = table.Column<decimal>(type: "numeric", nullable: true),
                    FactBeforeSum = table.Column<decimal>(type: "numeric", nullable: false),
                    RemainingSum = table.Column<decimal>(type: "numeric", nullable: true),
                    WorkDaysLeft = table.Column<int>(type: "integer", nullable: false),
                    DailyPlanSum = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SupersededById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDailyPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date",
                schema: "sales",
                table: "Holidays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDailyPlans_Active",
                schema: "telegram",
                table: "ReportDailyPlans",
                columns: new[] { "LinkoUserId", "Date" },
                unique: true,
                filter: "\"SupersededAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDailyPlans_Date",
                schema: "telegram",
                table: "ReportDailyPlans",
                column: "Date");

            // Справочник праздников: государственные праздники Узбекистана с постоянной датой на 2026–2027 годы. Хайиты (Рамазан, Курбан)
            // назначаются указом на год — их вносит администратор («Настройки → Telegram → Автоотчёты → Дневной план»). Откат — вместе с таблицей.
            migrationBuilder.Sql("""
                INSERT INTO sales."Holidays" ("Id", "CreatedAt", "Date", "Name")
                SELECT gen_random_uuid(), now(), d::date, n
                FROM (VALUES
                    ('2026-01-01', 'Новый год'), ('2026-03-08', 'Международный женский день'), ('2026-03-21', 'Навруз'),
                    ('2026-05-09', 'День памяти и почестей'), ('2026-09-01', 'День независимости'), ('2026-10-01', 'День учителя и наставника'),
                    ('2026-12-08', 'День Конституции'),
                    ('2027-01-01', 'Новый год'), ('2027-03-08', 'Международный женский день'), ('2027-03-21', 'Навруз'),
                    ('2027-05-09', 'День памяти и почестей'), ('2027-09-01', 'День независимости'), ('2027-10-01', 'День учителя и наставника'),
                    ('2027-12-08', 'День Конституции')) AS v(d, n)
                ON CONFLICT ("Date") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Holidays",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "ReportDailyPlans",
                schema: "telegram");
        }
    }
}

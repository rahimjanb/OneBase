using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiBudgetAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Существующая строка настроек получает те же значения, что и новая: предупреждение на 80 % бюджета, до 8 одновременных вызовов.
            // Бюджет (DailyBudgetUsd, MonthlyBudgetUsd) — пустой: без ограничения, пока администратор его не задаст.
            migrationBuilder.AddColumn<int>(
                name: "BudgetWarnPercent",
                schema: "ai",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyBudgetUsd",
                schema: "ai",
                table: "Settings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxConcurrentCalls",
                schema: "ai",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyBudgetUsd",
                schema: "ai",
                table: "Settings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetWarnPercent",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DailyBudgetUsd",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "MaxConcurrentCalls",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "MonthlyBudgetUsd",
                schema: "ai",
                table: "Settings");
        }
    }
}

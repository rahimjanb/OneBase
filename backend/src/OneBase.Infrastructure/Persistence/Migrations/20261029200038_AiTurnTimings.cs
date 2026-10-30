using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiTurnTimings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                schema: "ai",
                table: "Usage",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QueueWaitMs",
                schema: "ai",
                table: "Usage",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Timings",
                schema: "ai",
                table: "AuditLogs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempt",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "QueueWaitMs",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "Timings",
                schema: "ai",
                table: "AuditLogs");
        }
    }
}

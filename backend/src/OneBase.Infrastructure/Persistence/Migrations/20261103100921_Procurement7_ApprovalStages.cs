using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement7_ApprovalStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcurementApprovals_RequestId_Round",
                schema: "procurement",
                table: "Approvals");

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                schema: "procurement",
                table: "Approvals",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // Решения до нового маршрута — директора (согласовывал только он).
                defaultValue: "director");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementApprovals_RequestId_Round_Stage",
                schema: "procurement",
                table: "Approvals",
                columns: new[] { "RequestId", "Round", "Stage" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcurementApprovals_RequestId_Round_Stage",
                schema: "procurement",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "Stage",
                schema: "procurement",
                table: "Approvals");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementApprovals_RequestId_Round",
                schema: "procurement",
                table: "Approvals",
                columns: new[] { "RequestId", "Round" },
                unique: true);
        }
    }
}

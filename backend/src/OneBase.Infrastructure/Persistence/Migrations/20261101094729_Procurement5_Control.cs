using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement5_Control : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EscalateDirectorDays",
                schema: "procurement",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "EscalateHeadDays",
                schema: "procurement",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Digests",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Sections = table.Column<string>(type: "jsonb", nullable: false),
                    Recipients = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementDigests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Escalations",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementEscalations_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "work",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_Status_PostedAt",
                schema: "procurement",
                table: "StockDocuments",
                columns: new[] { "Status", "PostedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_ApprovedAt",
                schema: "procurement",
                table: "PurchaseOrders",
                column: "ApprovedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_DeliveredAt",
                schema: "procurement",
                table: "PurchaseOrders",
                column: "DeliveredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_OrderedAt",
                schema: "procurement",
                table: "PurchaseOrders",
                column: "OrderedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAuditLog_DocType_ToStatus_CreatedAt",
                schema: "procurement",
                table: "AuditLog",
                columns: new[] { "DocType", "ToStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementDigests_Day",
                schema: "procurement",
                table: "Digests",
                column: "Day",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEscalations_TaskId_Level_DueDate",
                schema: "procurement",
                table: "Escalations",
                columns: new[] { "TaskId", "Level", "DueDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Digests",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Escalations",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_Status_PostedAt",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPurchaseOrders_ApprovedAt",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPurchaseOrders_DeliveredAt",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPurchaseOrders_OrderedAt",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementAuditLog_DocType_ToStatus_CreatedAt",
                schema: "procurement",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "EscalateDirectorDays",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "EscalateHeadDays",
                schema: "procurement",
                table: "Settings");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RawMaterialWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "procurement",
                table: "Warehouses",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                schema: "procurement",
                table: "Warehouses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "procurement",
                table: "Warehouses",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Существующий склад («Центральный склад») — центральный и действующий: поведение одного склада не меняется.
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "procurement",
                table: "Warehouses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Central");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "procurement",
                table: "Warehouses",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetWarehouseId",
                schema: "procurement",
                table: "StockDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WarehouseKeepers",
                schema: "procurement",
                columns: table => new
                {
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementWarehouseKeepers", x => new { x.WarehouseId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProcurementWarehouseKeepers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementWarehouseKeepers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementWarehouses_DepartmentId",
                schema: "procurement",
                table: "Warehouses",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockMovements_WarehouseId_CreatedAt",
                schema: "procurement",
                table: "StockMovements",
                columns: new[] { "WarehouseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_TargetWarehouseId",
                schema: "procurement",
                table: "StockDocuments",
                column: "TargetWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementWarehouseKeepers_UserId",
                schema: "procurement",
                table: "WarehouseKeepers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementStockDocuments_Warehouses_TargetWarehouseId",
                schema: "procurement",
                table: "StockDocuments",
                column: "TargetWarehouseId",
                principalSchema: "procurement",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementWarehouses_Departments_DepartmentId",
                schema: "procurement",
                table: "Warehouses",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementStockDocuments_Warehouses_TargetWarehouseId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementWarehouses_Departments_DepartmentId",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropTable(
                name: "WarehouseKeepers",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementWarehouses_DepartmentId",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockMovements_WarehouseId_CreatedAt",
                schema: "procurement",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_TargetWarehouseId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TargetWarehouseId",
                schema: "procurement",
                table: "StockDocuments");
        }
    }
}

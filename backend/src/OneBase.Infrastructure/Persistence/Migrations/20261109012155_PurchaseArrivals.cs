using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseArrivals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "procurement",
                table: "PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenderId",
                schema: "procurement",
                table: "Arrivals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                schema: "procurement",
                table: "Arrivals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenderItemId",
                schema: "procurement",
                table: "ArrivalLines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "OrderItemId",
                schema: "procurement",
                table: "ArrivalLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_WarehouseId",
                schema: "procurement",
                table: "PurchaseOrders",
                column: "WarehouseId",
                filter: "\"WarehouseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_OrderId",
                schema: "procurement",
                table: "Arrivals",
                column: "OrderId",
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementArrivals_Source",
                schema: "procurement",
                table: "Arrivals",
                sql: "(\"TenderId\" IS NULL) <> (\"OrderId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivalLines_OrderItemId",
                schema: "procurement",
                table: "ArrivalLines",
                column: "OrderItemId",
                filter: "\"OrderItemId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementArrivalLines_PurchaseOrderItems_OrderItemId",
                schema: "procurement",
                table: "ArrivalLines",
                column: "OrderItemId",
                principalSchema: "procurement",
                principalTable: "PurchaseOrderItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementArrivals_PurchaseOrders_OrderId",
                schema: "procurement",
                table: "Arrivals",
                column: "OrderId",
                principalSchema: "procurement",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPurchaseOrders_Warehouses_WarehouseId",
                schema: "procurement",
                table: "PurchaseOrders",
                column: "WarehouseId",
                principalSchema: "procurement",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementArrivalLines_PurchaseOrderItems_OrderItemId",
                schema: "procurement",
                table: "ArrivalLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementArrivals_PurchaseOrders_OrderId",
                schema: "procurement",
                table: "Arrivals");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPurchaseOrders_Warehouses_WarehouseId",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementPurchaseOrders_WarehouseId",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementArrivals_OrderId",
                schema: "procurement",
                table: "Arrivals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProcurementArrivals_Source",
                schema: "procurement",
                table: "Arrivals");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementArrivalLines_OrderItemId",
                schema: "procurement",
                table: "ArrivalLines");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "procurement",
                table: "Arrivals");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                schema: "procurement",
                table: "ArrivalLines");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenderId",
                schema: "procurement",
                table: "Arrivals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenderItemId",
                schema: "procurement",
                table: "ArrivalLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

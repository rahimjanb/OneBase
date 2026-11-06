using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RawMaterialPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                schema: "procurement",
                table: "Tenders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "procurement",
                table: "Tenders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemId",
                schema: "procurement",
                table: "TenderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQty",
                schema: "procurement",
                table: "TenderItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TenderId",
                schema: "procurement",
                table: "StockDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RawRouteEnabled",
                schema: "procurement",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplyApproverId",
                schema: "procurement",
                table: "Settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RawRoute",
                schema: "procurement",
                table: "Requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProduction",
                schema: "procurement",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Системные категории сырья — маршрута сырья (сам маршрут включается в настройках снабжения, по умолчанию выключен).
            migrationBuilder.Sql(
                """UPDATE procurement."Categories" SET "IsProduction" = TRUE WHERE "Code" IN ('raw_materials', 'production_materials');""");

            migrationBuilder.CreateTable(
                name: "ItemUnits",
                schema: "procurement",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementItemUnits", x => new { x.ItemId, x.UnitCode });
                    table.CheckConstraint("CK_ProcurementItemUnits_Factor", "\"Factor\" > 0");
                    table.ForeignKey(
                        name: "FK_ProcurementItemUnits_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementItemUnits_Units_UnitCode",
                        column: x => x.UnitCode,
                        principalSchema: "procurement",
                        principalTable: "Units",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_RequestId",
                schema: "procurement",
                table: "Tenders",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_WarehouseId",
                schema: "procurement",
                table: "Tenders",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderItems_ItemId",
                schema: "procurement",
                table: "TenderItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_TenderId",
                schema: "procurement",
                table: "StockDocuments",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_SupplyApproverId",
                schema: "procurement",
                table: "Settings",
                column: "SupplyApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItemUnits_UnitCode",
                schema: "procurement",
                table: "ItemUnits",
                column: "UnitCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Users_SupplyApproverId",
                schema: "procurement",
                table: "Settings",
                column: "SupplyApproverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementStockDocuments_Tenders_TenderId",
                schema: "procurement",
                table: "StockDocuments",
                column: "TenderId",
                principalSchema: "procurement",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementTenderItems_Items_ItemId",
                schema: "procurement",
                table: "TenderItems",
                column: "ItemId",
                principalSchema: "procurement",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementTenders_Requests_RequestId",
                schema: "procurement",
                table: "Tenders",
                column: "RequestId",
                principalSchema: "procurement",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementTenders_Warehouses_WarehouseId",
                schema: "procurement",
                table: "Tenders",
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
                name: "FK_Settings_Users_SupplyApproverId",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementStockDocuments_Tenders_TenderId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementTenderItems_Items_ItemId",
                schema: "procurement",
                table: "TenderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementTenders_Requests_RequestId",
                schema: "procurement",
                table: "Tenders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementTenders_Warehouses_WarehouseId",
                schema: "procurement",
                table: "Tenders");

            migrationBuilder.DropTable(
                name: "ItemUnits",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementTenders_RequestId",
                schema: "procurement",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementTenders_WarehouseId",
                schema: "procurement",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementTenderItems_ItemId",
                schema: "procurement",
                table: "TenderItems");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_TenderId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Settings_SupplyApproverId",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "RequestId",
                schema: "procurement",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "procurement",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "ItemId",
                schema: "procurement",
                table: "TenderItems");

            migrationBuilder.DropColumn(
                name: "ReceivedQty",
                schema: "procurement",
                table: "TenderItems");

            migrationBuilder.DropColumn(
                name: "TenderId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "RawRouteEnabled",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SupplyApproverId",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "RawRoute",
                schema: "procurement",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "IsProduction",
                schema: "procurement",
                table: "Categories");
        }
    }
}

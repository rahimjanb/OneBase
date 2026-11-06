using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RawMaterialBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BatchAfter",
                schema: "procurement",
                table: "StockMovements",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BatchBefore",
                schema: "procurement",
                table: "StockMovements",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                schema: "procurement",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesDocumentId",
                schema: "procurement",
                table: "StockDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "procurement",
                table: "StockDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpiryWarningDays",
                schema: "procurement",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeDays",
                schema: "procurement",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageConditions",
                schema: "procurement",
                table: "Items",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TracksBatches",
                schema: "procurement",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Batches",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierBatchNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManufacturedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpiryWarnedFor = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiredNotifiedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementBatches_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBatches_StockDocuments_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalSchema: "procurement",
                        principalTable: "StockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBatches_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "procurement",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BatchBalances",
                schema: "procurement",
                columns: table => new
                {
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnHand = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementBatchBalances", x => new { x.WarehouseId, x.BatchId });
                    table.CheckConstraint("CK_ProcurementBatchBalances_OnHand", "\"OnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProcurementBatchBalances_Batches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "procurement",
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBatchBalances_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBatchBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockMovements_BatchId_CreatedAt",
                schema: "procurement",
                table: "StockMovements",
                columns: new[] { "BatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_ReversesDocumentId",
                schema: "procurement",
                table: "StockDocuments",
                column: "ReversesDocumentId",
                unique: true,
                filter: "\"ReversesDocumentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_SupplierId",
                schema: "procurement",
                table: "StockDocuments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatchBalances_BatchId",
                schema: "procurement",
                table: "BatchBalances",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatchBalances_ItemId",
                schema: "procurement",
                table: "BatchBalances",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatchBalances_WarehouseId_ItemId",
                schema: "procurement",
                table: "BatchBalances",
                columns: new[] { "WarehouseId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatches_ItemId_ExpiresOn",
                schema: "procurement",
                table: "Batches",
                columns: new[] { "ItemId", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatches_ItemId_SupplierBatchNo",
                schema: "procurement",
                table: "Batches",
                columns: new[] { "ItemId", "SupplierBatchNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatches_Number",
                schema: "procurement",
                table: "Batches",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatches_SourceDocumentId",
                schema: "procurement",
                table: "Batches",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatches_SupplierId",
                schema: "procurement",
                table: "Batches",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementStockDocuments_StockDocuments_ReversesDocumentId",
                schema: "procurement",
                table: "StockDocuments",
                column: "ReversesDocumentId",
                principalSchema: "procurement",
                principalTable: "StockDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementStockDocuments_Suppliers_SupplierId",
                schema: "procurement",
                table: "StockDocuments",
                column: "SupplierId",
                principalSchema: "procurement",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementStockMovements_Batches_BatchId",
                schema: "procurement",
                table: "StockMovements",
                column: "BatchId",
                principalSchema: "procurement",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementStockDocuments_StockDocuments_ReversesDocumentId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementStockDocuments_Suppliers_SupplierId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementStockMovements_Batches_BatchId",
                schema: "procurement",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "BatchBalances",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Batches",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockMovements_BatchId_CreatedAt",
                schema: "procurement",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_ReversesDocumentId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_SupplierId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "BatchAfter",
                schema: "procurement",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BatchBefore",
                schema: "procurement",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BatchId",
                schema: "procurement",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReversesDocumentId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "ExpiryWarningDays",
                schema: "procurement",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ShelfLifeDays",
                schema: "procurement",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "StorageConditions",
                schema: "procurement",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TracksBatches",
                schema: "procurement",
                table: "Items");
        }
    }
}

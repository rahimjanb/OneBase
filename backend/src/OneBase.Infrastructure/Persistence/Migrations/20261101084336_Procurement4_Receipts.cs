using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement4_Receipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                schema: "procurement",
                table: "StockDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                schema: "procurement",
                table: "StockDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "procurement",
                table: "StockDocuments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Posted");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "procurement",
                table: "StockDocuments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQty",
                schema: "procurement",
                table: "RequestItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "procurement",
                table: "PurchaseOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                schema: "procurement",
                table: "PurchaseOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueSoonNotifiedFor",
                schema: "procurement",
                table: "PurchaseOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OverdueNotifiedFor",
                schema: "procurement",
                table: "PurchaseOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosedQty",
                schema: "procurement",
                table: "PurchaseOrderItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LowStockNotifiedAt",
                schema: "procurement",
                table: "Items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReceiptLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Received = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Defective = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Shortage = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Accepted = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClaimStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClaimResolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClaimResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimResolvedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementReceiptLines", x => x.Id);
                    table.CheckConstraint("CK_ProcurementReceiptLines_Quantities", "\"Received\" >= 0 AND \"Defective\" >= 0 AND \"Defective\" <= \"Received\" AND \"Shortage\" >= 0 AND \"Accepted\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptLines_PurchaseOrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptLines_StockDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "procurement",
                        principalTable: "StockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptLines_Users_ClaimResolvedById",
                        column: x => x.ClaimResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_OrderId_Status",
                schema: "procurement",
                table: "StockDocuments",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptLines_ClaimStatus",
                schema: "procurement",
                table: "ReceiptLines",
                column: "ClaimStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptLines_DocumentId_LineNo",
                schema: "procurement",
                table: "ReceiptLines",
                columns: new[] { "DocumentId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptLines_ItemId",
                schema: "procurement",
                table: "ReceiptLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptLines_OrderItemId",
                schema: "procurement",
                table: "ReceiptLines",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ClaimResolvedById",
                schema: "procurement",
                table: "ReceiptLines",
                column: "ClaimResolvedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementStockDocuments_PurchaseOrders_OrderId",
                schema: "procurement",
                table: "StockDocuments",
                column: "OrderId",
                principalSchema: "procurement",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementStockDocuments_PurchaseOrders_OrderId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropTable(
                name: "ReceiptLines",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_OrderId_Status",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "ReceivedQty",
                schema: "procurement",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DueSoonNotifiedFor",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedFor",
                schema: "procurement",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ClosedQty",
                schema: "procurement",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "LowStockNotifiedAt",
                schema: "procurement",
                table: "Items");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement2_WarehouseApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalRound",
                schema: "procurement",
                table: "Requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                schema: "procurement",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                schema: "procurement",
                table: "Requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedSnapshot",
                schema: "procurement",
                table: "Requests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseTotal",
                schema: "procurement",
                table: "Requests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "UsesStock",
                schema: "procurement",
                table: "Requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FromStock",
                schema: "procurement",
                table: "RequestItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IssuedQty",
                schema: "procurement",
                table: "RequestItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemId",
                schema: "procurement",
                table: "RequestItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseReason",
                schema: "procurement",
                table: "RequestItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Approvals",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DeciderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementApprovals_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "procurement",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementApprovals_Users_DeciderId",
                        column: x => x.DeciderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MinQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementItems_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "procurement",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementItems_Units_UnitCode",
                        column: x => x.UnitCode,
                        principalSchema: "procurement",
                        principalTable: "Units",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ReapprovalTolerancePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockReservations",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Issued = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementStockReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementWarehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockBalances",
                schema: "procurement",
                columns: table => new
                {
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnHand = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Reserved = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementStockBalances", x => new { x.WarehouseId, x.ItemId });
                    table.CheckConstraint("CK_ProcurementStockBalances_OnHand", "\"OnHand\" >= 0");
                    table.CheckConstraint("CK_ProcurementStockBalances_Reserved", "\"Reserved\" >= 0 AND \"Reserved\" <= \"OnHand\"");
                    table.ForeignKey(
                        name: "FK_ProcurementStockBalances_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementStockBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockDocuments",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Receiver = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientKey = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementStockDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementStockDocuments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementStockDocuments_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProcurementStockDocuments_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RequestItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementStockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementStockMovements_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementStockMovements_StockDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "procurement",
                        principalTable: "StockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequestItems_ItemId",
                schema: "procurement",
                table: "RequestItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementApprovals_DeciderId",
                schema: "procurement",
                table: "Approvals",
                column: "DeciderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementApprovals_RequestId_Round",
                schema: "procurement",
                table: "Approvals",
                columns: new[] { "RequestId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_Barcode",
                schema: "procurement",
                table: "Items",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_CategoryId",
                schema: "procurement",
                table: "Items",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_Sku",
                schema: "procurement",
                table: "Items",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_UnitCode",
                schema: "procurement",
                table: "Items",
                column: "UnitCode");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockBalances_ItemId",
                schema: "procurement",
                table: "StockBalances",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_ActorId",
                schema: "procurement",
                table: "StockDocuments",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_ActorId_ClientKey",
                schema: "procurement",
                table: "StockDocuments",
                columns: new[] { "ActorId", "ClientKey" },
                unique: true,
                filter: "\"ClientKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_DepartmentId",
                schema: "procurement",
                table: "StockDocuments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_Kind_CreatedAt",
                schema: "procurement",
                table: "StockDocuments",
                columns: new[] { "Kind", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_Number",
                schema: "procurement",
                table: "StockDocuments",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_RequestId",
                schema: "procurement",
                table: "StockDocuments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDocuments_WarehouseId",
                schema: "procurement",
                table: "StockDocuments",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockMovements_CreatedAt",
                schema: "procurement",
                table: "StockMovements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockMovements_DocumentId",
                schema: "procurement",
                table: "StockMovements",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockMovements_ItemId_CreatedAt",
                schema: "procurement",
                table: "StockMovements",
                columns: new[] { "ItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockReservations_ItemId_Status",
                schema: "procurement",
                table: "StockReservations",
                columns: new[] { "ItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockReservations_RequestId_Status",
                schema: "procurement",
                table: "StockReservations",
                columns: new[] { "RequestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockReservations_RequestItemId_Active",
                schema: "procurement",
                table: "StockReservations",
                column: "RequestItemId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementWarehouses_Code",
                schema: "procurement",
                table: "Warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRequestItems_Items_ItemId",
                schema: "procurement",
                table: "RequestItems",
                column: "ItemId",
                principalSchema: "procurement",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRequestItems_Items_ItemId",
                schema: "procurement",
                table: "RequestItems");

            migrationBuilder.DropTable(
                name: "Approvals",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Settings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "StockBalances",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "StockMovements",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "StockReservations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "StockDocuments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Warehouses",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRequestItems_ItemId",
                schema: "procurement",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "ApprovalRound",
                schema: "procurement",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "procurement",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                schema: "procurement",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ApprovedSnapshot",
                schema: "procurement",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "PurchaseTotal",
                schema: "procurement",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "UsesStock",
                schema: "procurement",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "FromStock",
                schema: "procurement",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "IssuedQty",
                schema: "procurement",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "ItemId",
                schema: "procurement",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "PurchaseReason",
                schema: "procurement",
                table: "RequestItems");
        }
    }
}

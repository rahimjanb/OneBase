using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Arrivals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BatchId",
                schema: "procurement",
                table: "QualityChecks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ArrivalId",
                schema: "procurement",
                table: "QualityChecks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemId",
                schema: "procurement",
                table: "QualityChecks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "procurement",
                table: "QualityChecks",
                type: "uuid",
                nullable: true);

            // Прежние проверки — у всех есть партия: позиция и поставщик проверки — её. Только заполнение новых полей, ничего не удаляется.
            migrationBuilder.Sql(
                """
                UPDATE procurement."QualityChecks" AS c
                SET "ItemId" = b."ItemId", "SupplierId" = b."SupplierId"
                FROM procurement."Batches" AS b
                WHERE b."Id" = c."BatchId";
                """);

            migrationBuilder.CreateTable(
                name: "Arrivals",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleNo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DriverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ArrivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RegisteredById = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientKey = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementArrivals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivals_StockDocuments_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "procurement",
                        principalTable: "StockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivals_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "procurement",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivals_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalSchema: "procurement",
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivals_Users_AcceptedById",
                        column: x => x.AcceptedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivals_Users_RegisteredById",
                        column: x => x.RegisteredById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivals_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArrivalLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArrivalId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    TenderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Declared = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DeclaredBase = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    SupplierBatchNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ManufacturedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Control = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    QualityCheckId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AcceptedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    AcceptedWeight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    StockQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementArrivalLines", x => x.Id);
                    table.CheckConstraint("CK_ProcurementArrivalLines_Quantities", "\"Declared\" > 0 AND (\"AcceptedQty\" IS NULL OR \"AcceptedQty\" >= 0) AND (\"AcceptedWeight\" IS NULL OR \"AcceptedWeight\" >= 0) AND (\"StockQty\" IS NULL OR \"StockQty\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ProcurementArrivalLines_Arrivals_ArrivalId",
                        column: x => x.ArrivalId,
                        principalSchema: "procurement",
                        principalTable: "Arrivals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivalLines_Batches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "procurement",
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivalLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivalLines_QualityChecks_QualityCheckId",
                        column: x => x.QualityCheckId,
                        principalSchema: "procurement",
                        principalTable: "QualityChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementArrivalLines_TenderItems_TenderItemId",
                        column: x => x.TenderItemId,
                        principalSchema: "procurement",
                        principalTable: "TenderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityChecks_ArrivalId",
                schema: "procurement",
                table: "QualityChecks",
                column: "ArrivalId",
                filter: "\"ArrivalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityChecks_ItemId",
                schema: "procurement",
                table: "QualityChecks",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityChecks_SupplierId",
                schema: "procurement",
                table: "QualityChecks",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivalLines_ArrivalId_LineNo",
                schema: "procurement",
                table: "ArrivalLines",
                columns: new[] { "ArrivalId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivalLines_BatchId",
                schema: "procurement",
                table: "ArrivalLines",
                column: "BatchId",
                filter: "\"BatchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivalLines_ItemId",
                schema: "procurement",
                table: "ArrivalLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivalLines_QualityCheckId",
                schema: "procurement",
                table: "ArrivalLines",
                column: "QualityCheckId",
                unique: true,
                filter: "\"QualityCheckId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivalLines_TenderItemId",
                schema: "procurement",
                table: "ArrivalLines",
                column: "TenderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_AcceptedById",
                schema: "procurement",
                table: "Arrivals",
                column: "AcceptedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_Number",
                schema: "procurement",
                table: "Arrivals",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_ReceiptId",
                schema: "procurement",
                table: "Arrivals",
                column: "ReceiptId",
                unique: true,
                filter: "\"ReceiptId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_RegisteredById",
                schema: "procurement",
                table: "Arrivals",
                column: "RegisteredById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_RegisteredById_ClientKey",
                schema: "procurement",
                table: "Arrivals",
                columns: new[] { "RegisteredById", "ClientKey" },
                unique: true,
                filter: "\"ClientKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_Status_ArrivedAt",
                schema: "procurement",
                table: "Arrivals",
                columns: new[] { "Status", "ArrivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_SupplierId",
                schema: "procurement",
                table: "Arrivals",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_TenderId",
                schema: "procurement",
                table: "Arrivals",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementArrivals_WarehouseId",
                schema: "procurement",
                table: "Arrivals",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementQualityChecks_Arrivals_ArrivalId",
                schema: "procurement",
                table: "QualityChecks",
                column: "ArrivalId",
                principalSchema: "procurement",
                principalTable: "Arrivals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementQualityChecks_Items_ItemId",
                schema: "procurement",
                table: "QualityChecks",
                column: "ItemId",
                principalSchema: "procurement",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementQualityChecks_Suppliers_SupplierId",
                schema: "procurement",
                table: "QualityChecks",
                column: "SupplierId",
                principalSchema: "procurement",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementQualityChecks_Arrivals_ArrivalId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementQualityChecks_Items_ItemId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementQualityChecks_Suppliers_SupplierId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.DropTable(
                name: "ArrivalLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Arrivals",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementQualityChecks_ArrivalId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementQualityChecks_ItemId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementQualityChecks_SupplierId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.DropColumn(
                name: "ArrivalId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.DropColumn(
                name: "ItemId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "procurement",
                table: "QualityChecks");

            migrationBuilder.AlterColumn<Guid>(
                name: "BatchId",
                schema: "procurement",
                table: "QualityChecks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

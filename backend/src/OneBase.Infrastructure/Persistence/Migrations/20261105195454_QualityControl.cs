using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QualityControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QualityCheckId",
                schema: "procurement",
                table: "StockDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityControl",
                schema: "procurement",
                table: "Items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ExpiresOn",
                schema: "procurement",
                table: "Batches",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "BlockReason",
                schema: "procurement",
                table: "Batches",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QualityDecidedAt",
                schema: "procurement",
                table: "Batches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QualityDecidedById",
                schema: "procurement",
                table: "Batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityStatus",
                schema: "procurement",
                table: "Batches",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.CreateTable(
                name: "QualityApprovers",
                schema: "procurement",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementQualityApprovers", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ProcurementQualityApprovers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QualityChecks",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Control = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DocumentsOk = table.Column<bool>(type: "boolean", nullable: true),
                    DocumentsNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DocumentsById = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SampleQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    LabOk = table.Column<bool>(type: "boolean", nullable: true),
                    LabNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LabById = table.Column<Guid>(type: "uuid", nullable: true),
                    LabAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementQualityChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementQualityChecks_Batches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "procurement",
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementQualityChecks_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProcurementQualityChecks_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualitySpecs",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Min = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Max = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Expected = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Method = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementQualitySpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementQualitySpecs_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityResults",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Min = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Max = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Expected = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementQualityResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementQualityResults_QualityChecks_CheckId",
                        column: x => x.CheckId,
                        principalSchema: "procurement",
                        principalTable: "QualityChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementQualityResults_QualitySpecs_SpecId",
                        column: x => x.SpecId,
                        principalSchema: "procurement",
                        principalTable: "QualitySpecs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_QualityCheckId",
                schema: "procurement",
                table: "StockDocuments",
                column: "QualityCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatches_QualityDecidedById",
                schema: "procurement",
                table: "Batches",
                column: "QualityDecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBatches_QualityStatus",
                schema: "procurement",
                table: "Batches",
                column: "QualityStatus",
                filter: "\"QualityStatus\" <> 'Approved'");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityChecks_BatchId_Open",
                schema: "procurement",
                table: "QualityChecks",
                column: "BatchId",
                unique: true,
                filter: "\"Status\" IN ('Documents', 'Lab', 'Decision')");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityChecks_DecidedById",
                schema: "procurement",
                table: "QualityChecks",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityChecks_Number",
                schema: "procurement",
                table: "QualityChecks",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityChecks_Status_CreatedAt",
                schema: "procurement",
                table: "QualityChecks",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityChecks_WarehouseId",
                schema: "procurement",
                table: "QualityChecks",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityResults_CheckId_LineNo",
                schema: "procurement",
                table: "QualityResults",
                columns: new[] { "CheckId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualityResults_SpecId",
                schema: "procurement",
                table: "QualityResults",
                column: "SpecId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualitySpecs_ItemId_LineNo",
                schema: "procurement",
                table: "QualitySpecs",
                columns: new[] { "ItemId", "LineNo" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementBatches_Users_QualityDecidedById",
                schema: "procurement",
                table: "Batches",
                column: "QualityDecidedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementStockDocuments_QualityChecks_QualityCheckId",
                schema: "procurement",
                table: "StockDocuments",
                column: "QualityCheckId",
                principalSchema: "procurement",
                principalTable: "QualityChecks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementBatches_Users_QualityDecidedById",
                schema: "procurement",
                table: "Batches");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementStockDocuments_QualityChecks_QualityCheckId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropTable(
                name: "QualityApprovers",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "QualityResults",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "QualityChecks",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "QualitySpecs",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_QualityCheckId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBatches_QualityDecidedById",
                schema: "procurement",
                table: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBatches_QualityStatus",
                schema: "procurement",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "QualityCheckId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "QualityControl",
                schema: "procurement",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "BlockReason",
                schema: "procurement",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "QualityDecidedAt",
                schema: "procurement",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "QualityDecidedById",
                schema: "procurement",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "QualityStatus",
                schema: "procurement",
                table: "Batches");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ExpiresOn",
                schema: "procurement",
                table: "Batches",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}

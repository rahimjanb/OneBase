using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement11_StockImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockImports",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Rows = table.Column<int>(type: "integer", nullable: false),
                    Posted = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    Documents = table.Column<int>(type: "integer", nullable: false),
                    LastDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementStockImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockImportRows",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementStockImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementStockImportRows_StockImports_ImportId",
                        column: x => x.ImportId,
                        principalSchema: "procurement",
                        principalTable: "StockImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockImportRows_ImportId",
                schema: "procurement",
                table: "StockImportRows",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockImportRows_WarehouseId_Key",
                schema: "procurement",
                table: "StockImportRows",
                columns: new[] { "WarehouseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockImports_WarehouseId_CreatedAt",
                schema: "procurement",
                table: "StockImports",
                columns: new[] { "WarehouseId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockImportRows",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "StockImports",
                schema: "procurement");
        }
    }
}

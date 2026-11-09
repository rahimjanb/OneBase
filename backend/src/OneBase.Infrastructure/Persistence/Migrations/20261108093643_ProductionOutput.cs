using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionOutput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_ProductionOrderId_Output",
                schema: "procurement",
                table: "StockDocuments",
                column: "ProductionOrderId",
                unique: true,
                filter: "\"Kind\" = 'ProductionOutput'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_ProductionOrderId_Output",
                schema: "procurement",
                table: "StockDocuments");
        }
    }
}

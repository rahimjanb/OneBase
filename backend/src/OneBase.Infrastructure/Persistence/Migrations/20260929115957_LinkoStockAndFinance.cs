using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkoStockAndFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contracts",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Number = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    IsDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: true),
                    CurrencyName = table.Column<string>(type: "text", nullable: true),
                    MarketId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentType = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    IsDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceListItems",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    PriceListId = table.Column<long>(type: "bigint", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: true),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceLists",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: true),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductBalances",
                schema: "linko",
                columns: table => new
                {
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    StockId = table.Column<long>(type: "bigint", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric", nullable: false),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBalances", x => new { x.ProductId, x.StockId });
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stocks",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FromStockId = table.Column<long>(type: "bigint", nullable: true),
                    ToStockId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GivenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcceptedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TotalWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    PriceListId = table.Column<long>(type: "bigint", nullable: true),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: true),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferLines",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TransferId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWeightNetto = table.Column<decimal>(type: "numeric", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_StockTransfers_TransferId",
                        column: x => x.TransferId,
                        principalSchema: "linko",
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedDate",
                schema: "linko",
                table: "Payments",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MarketId",
                schema: "linko",
                table: "Payments",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_PriceListId_ProductId",
                schema: "linko",
                table: "PriceListItems",
                columns: new[] { "PriceListId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBalances_StockId",
                schema: "linko",
                table: "ProductBalances",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_TransferId",
                schema: "linko",
                table: "StockTransferLines",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_AcceptedDate",
                schema: "linko",
                table: "StockTransfers",
                column: "AcceptedDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CreatedDate",
                schema: "linko",
                table: "StockTransfers",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_FromStockId",
                schema: "linko",
                table: "StockTransfers",
                column: "FromStockId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "PriceListItems",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "PriceLists",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "ProductBalances",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Providers",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Stocks",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "StockTransferLines",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "StockTransfers",
                schema: "linko");
        }
    }
}

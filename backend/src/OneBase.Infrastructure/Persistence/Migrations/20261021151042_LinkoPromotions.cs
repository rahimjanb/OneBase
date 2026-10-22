using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkoPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                schema: "linko",
                table: "OrderLines",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginPrice",
                schema: "linko",
                table: "OrderLines",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDiscount",
                schema: "linko",
                table: "OrderLines",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PromotionProducts",
                schema: "linko",
                columns: table => new
                {
                    PromotionId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionProducts", x => new { x.PromotionId, x.ProductId, x.Type });
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PromotionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true),
                    BeginDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TillDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsApplyAll = table.Column<bool>(type: "boolean", nullable: false),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionProducts_ProductId",
                schema: "linko",
                table: "PromotionProducts",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromotionProducts",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Promotions",
                schema: "linko");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                schema: "linko",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "OriginPrice",
                schema: "linko",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "TotalDiscount",
                schema: "linko",
                table: "OrderLines");
        }
    }
}

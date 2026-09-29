using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderAcceptedTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                schema: "linko",
                table: "Orders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AcceptedDate",
                schema: "linko",
                table: "Orders",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AcceptedDate",
                schema: "linko",
                table: "Orders",
                column: "AcceptedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_AcceptedDate",
                schema: "linko",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                schema: "linko",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AcceptedDate",
                schema: "linko",
                table: "Orders");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement6_ApprovalRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DirectorApproverId",
                schema: "procurement",
                table: "Settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DirectorLimit",
                schema: "procurement",
                table: "Settings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                // Уже сохранённые настройки получают лимит по умолчанию (5 млн сум), а не 0 — иначе каждая заявка шла бы директору.
                defaultValue: 5_000_000m);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceApproverId",
                schema: "procurement",
                table: "Settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_DirectorApproverId",
                schema: "procurement",
                table: "Settings",
                column: "DirectorApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_FinanceApproverId",
                schema: "procurement",
                table: "Settings",
                column: "FinanceApproverId");

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Users_DirectorApproverId",
                schema: "procurement",
                table: "Settings",
                column: "DirectorApproverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Users_FinanceApproverId",
                schema: "procurement",
                table: "Settings",
                column: "FinanceApproverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Settings_Users_DirectorApproverId",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropForeignKey(
                name: "FK_Settings_Users_FinanceApproverId",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Settings_DirectorApproverId",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Settings_FinanceApproverId",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DirectorApproverId",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DirectorLimit",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FinanceApproverId",
                schema: "procurement",
                table: "Settings");
        }
    }
}

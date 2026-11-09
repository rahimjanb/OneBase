using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DialogAgentAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                schema: "ai",
                table: "DialogAgents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DialogAgents_AccountId",
                schema: "ai",
                table: "DialogAgents",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_DialogAgents_Accounts_AccountId",
                schema: "ai",
                table: "DialogAgents",
                column: "AccountId",
                principalSchema: "telegram",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DialogAgents_Accounts_AccountId",
                schema: "ai",
                table: "DialogAgents");

            migrationBuilder.DropIndex(
                name: "IX_DialogAgents_AccountId",
                schema: "ai",
                table: "DialogAgents");

            migrationBuilder.DropColumn(
                name: "AccountId",
                schema: "ai",
                table: "DialogAgents");
        }
    }
}

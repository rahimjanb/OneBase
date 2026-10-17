using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelegramAccessAndReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CannotSendReason",
                schema: "telegram",
                table: "Chats",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FullAccessUserId",
                schema: "telegram",
                table: "AgentSettings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Persona",
                schema: "telegram",
                table: "AgentSettings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CannotSendReason",
                schema: "telegram",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "FullAccessUserId",
                schema: "telegram",
                table: "AgentSettings");

            migrationBuilder.DropColumn(
                name: "Persona",
                schema: "telegram",
                table: "AgentSettings");
        }
    }
}

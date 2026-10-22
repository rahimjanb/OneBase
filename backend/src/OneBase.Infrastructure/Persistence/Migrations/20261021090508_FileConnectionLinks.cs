using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FileConnectionLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LinkChangedAt",
                table: "FileConnections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkTokenHash",
                table: "FileConnections",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedLinkToken",
                table: "FileConnections",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileConnections_LinkTokenHash",
                table: "FileConnections",
                column: "LinkTokenHash",
                unique: true,
                filter: "\"LinkTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileConnections_LinkTokenHash",
                table: "FileConnections");

            migrationBuilder.DropColumn(
                name: "LinkChangedAt",
                table: "FileConnections");

            migrationBuilder.DropColumn(
                name: "LinkTokenHash",
                table: "FileConnections");

            migrationBuilder.DropColumn(
                name: "ProtectedLinkToken",
                table: "FileConnections");
        }
    }
}

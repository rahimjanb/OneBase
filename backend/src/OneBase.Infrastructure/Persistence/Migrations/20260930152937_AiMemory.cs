using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Memory",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Topic = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_UserId_AgentCode_CreatedAt",
                schema: "ai",
                table: "Memory",
                columns: new[] { "UserId", "AgentCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_UserId_ConversationId_CreatedAt",
                schema: "ai",
                table: "Memory",
                columns: new[] { "UserId", "ConversationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Memory",
                schema: "ai");
        }
    }
}

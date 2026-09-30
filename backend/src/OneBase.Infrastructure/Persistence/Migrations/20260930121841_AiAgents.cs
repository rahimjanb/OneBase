using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "AgentToolGrants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AgentKnowledgeSources",
                schema: "ai",
                columns: table => new
                {
                    AgentCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentKnowledgeSources", x => new { x.AgentCode, x.SourceCode });
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                schema: "ai",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SystemPrompt = table.Column<string>(type: "text", nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DepartmentCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequiredPermission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAgents", x => x.Code);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentKnowledgeSources",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "Agents",
                schema: "ai");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "AgentToolGrants");
        }
    }
}

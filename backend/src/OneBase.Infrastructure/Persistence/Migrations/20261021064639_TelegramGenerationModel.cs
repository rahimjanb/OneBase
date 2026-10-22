using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelegramGenerationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GenerationModel",
                schema: "telegram",
                table: "AgentSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationModelProvider",
                schema: "telegram",
                table: "AgentSettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GenerationModel",
                schema: "telegram",
                table: "AgentSettings");

            migrationBuilder.DropColumn(
                name: "GenerationModelProvider",
                schema: "telegram",
                table: "AgentSettings");
        }
    }
}

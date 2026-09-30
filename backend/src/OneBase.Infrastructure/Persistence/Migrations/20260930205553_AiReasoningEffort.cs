using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiReasoningEffort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReasoningEffort",
                schema: "ai",
                table: "Settings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // Новая настройка: у уже сохранённых настроек — низкая глубина рассуждений (быстрее), как и у новых.
            migrationBuilder.Sql("""UPDATE ai."Settings" SET "ReasoningEffort" = 'low';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReasoningEffort",
                schema: "ai",
                table: "Settings");
        }
    }
}

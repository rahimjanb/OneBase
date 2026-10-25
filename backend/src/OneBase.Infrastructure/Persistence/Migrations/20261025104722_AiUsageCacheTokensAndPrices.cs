using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiUsageCacheTokensAndPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CacheReadTokens",
                schema: "ai",
                table: "Usage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CacheWriteTokens",
                schema: "ai",
                table: "Usage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CacheReadPricePerMillion",
                schema: "ai",
                table: "Models",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CacheWritePricePerMillion",
                schema: "ai",
                table: "Models",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            // Цены Claude 5.x за 1 млн токенов по прайсу Anthropic (platform.claude.com/docs/en/about-claude/pricing) от 25.10.2026:
            // вход / выход / кэш-чтение / кэш-запись (5 минут). Только там, где цена ещё не задана, — введённое администратором не трогаем.
            // Haiku 5.5 — тариф до 100 000 токенов промпта; выше порога стоимость считается ×5 в коде (AiPricing).
            foreach (var (pattern, input, output, cacheRead, cacheWrite) in new[]
                     {
                         ("claude-opus-5-5%", "4", "20", "0.20", "5"),
                         ("claude-sonnet-5-5%", "2", "10", "0.10", "2.50"),
                         ("claude-haiku-5-5%", "0.10", "0.50", "0.01", "0.125"),
                         ("claude-fable-5-1%", "10", "50", "0.25", "12.50"),
                     })
            {
                migrationBuilder.Sql($"""
                    UPDATE ai."Models" SET
                        "InputPricePerMillion" = COALESCE("InputPricePerMillion", {input}),
                        "OutputPricePerMillion" = COALESCE("OutputPricePerMillion", {output}),
                        "CacheReadPricePerMillion" = COALESCE("CacheReadPricePerMillion", {cacheRead}),
                        "CacheWritePricePerMillion" = COALESCE("CacheWritePricePerMillion", {cacheWrite})
                    WHERE "ProviderCode" = 'anthropic' AND "ModelId" LIKE '{pattern}';
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CacheReadTokens",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "CacheWriteTokens",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "CacheReadPricePerMillion",
                schema: "ai",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "CacheWritePricePerMillion",
                schema: "ai",
                table: "Models");
        }
    }
}

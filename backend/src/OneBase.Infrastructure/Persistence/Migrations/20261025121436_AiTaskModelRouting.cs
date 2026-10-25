using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiTaskModelRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractMode",
                schema: "ai",
                table: "Settings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "off");

            migrationBuilder.AddColumn<string>(
                name: "ExtractModel",
                schema: "ai",
                table: "Settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractProvider",
                schema: "ai",
                table: "Settings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouterMode",
                schema: "ai",
                table: "Settings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "off");

            migrationBuilder.AddColumn<bool>(
                name: "TaskRoutingEnabled",
                schema: "ai",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ModelComparisons",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CaseCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Task = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BaselineModel = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    CandidateModel = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    BaselineResult = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CandidateResult = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Expected = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Agree = table.Column<bool>(type: "boolean", nullable: false),
                    Similarity = table.Column<double>(type: "double precision", nullable: false),
                    BaselineValid = table.Column<bool>(type: "boolean", nullable: false),
                    CandidateValid = table.Column<bool>(type: "boolean", nullable: false),
                    BaselineCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    CandidateCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    BaselineInputTokens = table.Column<int>(type: "integer", nullable: false),
                    BaselineCacheReadTokens = table.Column<int>(type: "integer", nullable: false),
                    BaselineCacheWriteTokens = table.Column<int>(type: "integer", nullable: false),
                    BaselineOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    BaselineCostUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: true),
                    BaselineMs = table.Column<long>(type: "bigint", nullable: false),
                    CandidateInputTokens = table.Column<int>(type: "integer", nullable: false),
                    CandidateCacheReadTokens = table.Column<int>(type: "integer", nullable: false),
                    CandidateCacheWriteTokens = table.Column<int>(type: "integer", nullable: false),
                    CandidateOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    CandidateCostUsd = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: true),
                    CandidateMs = table.Column<long>(type: "bigint", nullable: false),
                    CandidateError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelComparisons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelComparisons_Category_CreatedAt",
                schema: "ai",
                table: "ModelComparisons",
                columns: new[] { "Category", "CreatedAt" });

            // Прежнее поведение сохраняется: если модель маршрутизации уже выбрана, она и дальше выбирает сотрудников (флаг включён,
            // режим «Включено»). Где не выбрана — маршрутизация идёт основной моделью, как раньше (флаг выключен, режим «Выкл»).
            migrationBuilder.Sql("""
                UPDATE ai."Settings" SET "TaskRoutingEnabled" = TRUE, "RouterMode" = 'on'
                WHERE "RouterModel" IS NOT NULL AND "RouterModel" <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelComparisons",
                schema: "ai");

            migrationBuilder.DropColumn(
                name: "ExtractMode",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ExtractModel",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ExtractProvider",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "RouterMode",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TaskRoutingEnabled",
                schema: "ai",
                table: "Settings");
        }
    }
}

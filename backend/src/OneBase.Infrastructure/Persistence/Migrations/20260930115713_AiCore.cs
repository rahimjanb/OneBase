using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.CreateTable(
                name: "Models",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Available = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    InputPricePerMillion = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    OutputPricePerMillion = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    ProviderCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                schema: "ai",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProtectedApiKey = table.Column<string>(type: "text", nullable: true),
                    ApiKeyHint = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    LastTestAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastTestOk = table.Column<bool>(type: "boolean", nullable: true),
                    LastTestMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ModelsRefreshedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviders", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PrimaryProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PrimaryModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FallbackProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FallbackModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RouterProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RouterModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmbeddingProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Models_ProviderCode_ModelId",
                schema: "ai",
                table: "Models",
                columns: new[] { "ProviderCode", "ModelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Models",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "Providers",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "Settings",
                schema: "ai");
        }
    }
}

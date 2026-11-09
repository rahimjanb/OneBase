using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecipeImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeImportItemMaps",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Article = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecipeImportItemMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionRecipeImportItemMaps_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeImports",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Sheets = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppliedById = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecipeImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionRecipeImports_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeImportRecipes",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Sheet = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Column = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    HeaderRow = table.Column<int>(type: "integer", nullable: false),
                    Article = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BaseQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    BaseUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    BaseLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TotalCell = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    TotalRaw = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalFormula = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LossCell = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LossRaw = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LinkoProductId = table.Column<long>(type: "bigint", nullable: true),
                    LinkoSuggestedId = table.Column<long>(type: "bigint", nullable: true),
                    IsSemiFinished = table.Column<bool>(type: "boolean", nullable: false),
                    OutputItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Include = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Issues = table.Column<string>(type: "jsonb", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExistingRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppliedById = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecipeImportRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionRecipeImportRecipes_Imports_ImportId",
                        column: x => x.ImportId,
                        principalSchema: "production",
                        principalTable: "RecipeImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionRecipeImportRecipes_Items_OutputItemId",
                        column: x => x.OutputItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionRecipeImportRecipes_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "production",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RecipeImportLines",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportRecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Row = table.Column<int>(type: "integer", nullable: false),
                    Cell = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Article = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Weight = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Raw = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Formula = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Value = table.Column<decimal>(type: "numeric", nullable: true),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Match = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ManualValue = table.Column<decimal>(type: "numeric", nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecipeImportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionRecipeImportLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionRecipeImportLines_Recipes_ImportRecipeId",
                        column: x => x.ImportRecipeId,
                        principalSchema: "production",
                        principalTable: "RecipeImportRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeImportItemMaps_Article_Name",
                schema: "production",
                table: "RecipeImportItemMaps",
                columns: new[] { "Article", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImportItemMaps_ItemId",
                schema: "production",
                table: "RecipeImportItemMaps",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeImportLines_Article",
                schema: "production",
                table: "RecipeImportLines",
                column: "Article");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeImportLines_ImportRecipeId_Row",
                schema: "production",
                table: "RecipeImportLines",
                columns: new[] { "ImportRecipeId", "Row" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImportLines_ItemId",
                schema: "production",
                table: "RecipeImportLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeImportRecipes_ImportId_SourceKey_Column",
                schema: "production",
                table: "RecipeImportRecipes",
                columns: new[] { "ImportId", "SourceKey", "Column" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeImportRecipes_RecipeId",
                schema: "production",
                table: "RecipeImportRecipes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeImportRecipes_SourceKey",
                schema: "production",
                table: "RecipeImportRecipes",
                column: "SourceKey");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImportRecipes_OutputItemId",
                schema: "production",
                table: "RecipeImportRecipes",
                column: "OutputItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeImports_FileHash",
                schema: "production",
                table: "RecipeImports",
                column: "FileHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImports_CreatedById",
                schema: "production",
                table: "RecipeImports",
                column: "CreatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeImportItemMaps",
                schema: "production");

            migrationBuilder.DropTable(
                name: "RecipeImportLines",
                schema: "production");

            migrationBuilder.DropTable(
                name: "RecipeImportRecipes",
                schema: "production");

            migrationBuilder.DropTable(
                name: "RecipeImports",
                schema: "production");
        }
    }
}

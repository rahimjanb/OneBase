using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TechnologistStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionRecipes_Code_Draft",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_ProductionRecipeLines_RecipeId_ItemId",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.AddColumn<decimal>(
                name: "BatchMaxKg",
                schema: "production",
                table: "Recipes",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BatchMinKg",
                schema: "production",
                table: "Recipes",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                schema: "production",
                table: "Recipes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackKg",
                schema: "production",
                table: "Recipes",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PacksPerBox",
                schema: "production",
                table: "Recipes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PartialBatches",
                schema: "production",
                table: "Recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PieceKg",
                schema: "production",
                table: "Recipes",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                schema: "production",
                table: "Recipes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedById",
                schema: "production",
                table: "Recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechCardNo",
                schema: "production",
                table: "Recipes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnologistId",
                schema: "production",
                table: "Recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Basis",
                schema: "production",
                table: "RecipeLines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Output");

            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                schema: "production",
                table: "RecipeLines",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StageId",
                schema: "production",
                table: "RecipeLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitCode",
                schema: "production",
                table: "RecipeLines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitKg",
                schema: "production",
                table: "RecipeLines",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanSnapshot",
                schema: "production",
                table: "Orders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecipeStages",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageNo = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Area = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    YieldPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    YieldMinPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    YieldMaxPercent = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true),
                    DefectBase = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MoistureNorm = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    MoistureUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MoistureTolerance = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    Reworkable = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecipeStages", x => x.Id);
                    table.CheckConstraint("CK_ProductionRecipeStages_StageNo", "\"StageNo\" > 0");
                    table.CheckConstraint("CK_ProductionRecipeStages_Yield", "\"YieldPercent\" > 0 AND (\"YieldMinPercent\" IS NULL OR \"YieldMinPercent\" <= \"YieldPercent\") AND (\"YieldMaxPercent\" IS NULL OR \"YieldMaxPercent\" >= \"YieldPercent\")");
                    table.ForeignKey(
                        name: "FK_ProductionRecipeStages_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "production",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DefectTypes",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    NormPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Reworkable = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionDefectTypes", x => x.Id);
                    table.CheckConstraint("CK_ProductionDefectTypes_NormPercent", "\"NormPercent\" >= 0 AND \"NormPercent\" < 100");
                    table.ForeignKey(
                        name: "FK_ProductionDefectTypes_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionDefectTypes_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "production",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionDefectTypes_Stages_StageId",
                        column: x => x.StageId,
                        principalSchema: "production",
                        principalTable: "RecipeStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipes_Code_Open",
                schema: "production",
                table: "Recipes",
                column: "Code",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'PendingApproval')");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_SubmittedById",
                schema: "production",
                table: "Recipes",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_TechnologistId",
                schema: "production",
                table: "Recipes",
                column: "TechnologistId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductionRecipes_Batch",
                schema: "production",
                table: "Recipes",
                sql: "(\"BatchMinKg\" IS NULL OR \"BatchMinKg\" > 0) AND (\"BatchMaxKg\" IS NULL OR \"BatchMaxKg\" > 0) AND (\"BatchMinKg\" IS NULL OR \"BatchMaxKg\" IS NULL OR \"BatchMinKg\" <= \"BatchMaxKg\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductionRecipes_Packaging",
                schema: "production",
                table: "Recipes",
                sql: "(\"PieceKg\" IS NULL OR \"PieceKg\" > 0) AND (\"PackKg\" IS NULL OR \"PackKg\" > 0) AND (\"PacksPerBox\" IS NULL OR \"PacksPerBox\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeLines_RecipeId_StageId_ItemId",
                schema: "production",
                table: "RecipeLines",
                columns: new[] { "RecipeId", "StageId", "ItemId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeLines_StageId",
                schema: "production",
                table: "RecipeLines",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_UnitCode",
                schema: "production",
                table: "RecipeLines",
                column: "UnitCode");

            migrationBuilder.CreateIndex(
                name: "IX_DefectTypes_ItemId",
                schema: "production",
                table: "DefectTypes",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionDefectTypes_RecipeId",
                schema: "production",
                table: "DefectTypes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionDefectTypes_StageId_Name",
                schema: "production",
                table: "DefectTypes",
                columns: new[] { "StageId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeStages_RecipeId_StageNo",
                schema: "production",
                table: "RecipeStages",
                columns: new[] { "RecipeId", "StageNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionRecipeLines_Stages_StageId",
                schema: "production",
                table: "RecipeLines",
                column: "StageId",
                principalSchema: "production",
                principalTable: "RecipeStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionRecipeLines_Units_UnitCode",
                schema: "production",
                table: "RecipeLines",
                column: "UnitCode",
                principalSchema: "procurement",
                principalTable: "Units",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionRecipes_Users_SubmittedById",
                schema: "production",
                table: "Recipes",
                column: "SubmittedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionRecipes_Users_TechnologistId",
                schema: "production",
                table: "Recipes",
                column: "TechnologistId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRecipeLines_Stages_StageId",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRecipeLines_Units_UnitCode",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRecipes_Users_SubmittedById",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRecipes_Users_TechnologistId",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropTable(
                name: "DefectTypes",
                schema: "production");

            migrationBuilder.DropTable(
                name: "RecipeStages",
                schema: "production");

            migrationBuilder.DropIndex(
                name: "IX_ProductionRecipes_Code_Open",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_SubmittedById",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_TechnologistId",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductionRecipes_Batch",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductionRecipes_Packaging",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_ProductionRecipeLines_RecipeId_StageId_ItemId",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropIndex(
                name: "IX_ProductionRecipeLines_StageId",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropIndex(
                name: "IX_RecipeLines_UnitCode",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropColumn(
                name: "BatchMaxKg",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "BatchMinKg",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PackKg",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PacksPerBox",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PartialBatches",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PieceKg",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SubmittedById",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "TechCardNo",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "TechnologistId",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Basis",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropColumn(
                name: "GroupName",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropColumn(
                name: "StageId",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropColumn(
                name: "UnitCode",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropColumn(
                name: "UnitKg",
                schema: "production",
                table: "RecipeLines");

            migrationBuilder.DropColumn(
                name: "PlanSnapshot",
                schema: "production",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipes_Code_Draft",
                schema: "production",
                table: "Recipes",
                column: "Code",
                unique: true,
                filter: "\"Status\" = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeLines_RecipeId_ItemId",
                schema: "production",
                table: "RecipeLines",
                columns: new[] { "RecipeId", "ItemId" },
                unique: true);
        }
    }
}

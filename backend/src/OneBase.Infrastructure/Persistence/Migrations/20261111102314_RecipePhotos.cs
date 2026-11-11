using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecipePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipePhotos",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ThumbKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SmallKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Tint = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecipePhotos", x => x.Id);
                    table.CheckConstraint("CK_ProductionRecipePhotos_Size", "\"Width\" > 0 AND \"Height\" > 0 AND \"SizeBytes\" > 0");
                    table.ForeignKey(
                        name: "FK_ProductionRecipePhotos_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipePhotos_Code",
                schema: "production",
                table: "RecipePhotos",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipePhotos_FileId",
                schema: "production",
                table: "RecipePhotos",
                column: "FileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipePhotos_UploadedById",
                schema: "production",
                table: "RecipePhotos",
                column: "UploadedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipePhotos",
                schema: "production");
        }
    }
}

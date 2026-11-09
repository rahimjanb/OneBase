using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LanguageConfidence",
                schema: "recruitment",
                table: "Candidates",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageSource",
                schema: "recruitment",
                table: "Candidates",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastDetectedLanguage",
                schema: "recruitment",
                table: "Candidates",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredScript",
                schema: "recruitment",
                table: "Candidates",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LanguageConfidence",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "LanguageSource",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "LastDetectedLanguage",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "PreferredScript",
                schema: "recruitment",
                table: "Candidates");
        }
    }
}

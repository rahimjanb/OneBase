using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <summary>Привязка Google Sheets — несколько листов: столбец Sheet (один лист) → Sheets (jsonb, массив названий), прежний лист переносится.</summary>
    public partial class DataSourceSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sheets",
                table: "DataSourceBindings",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql("""UPDATE "DataSourceBindings" SET "Sheets" = jsonb_build_array("Sheet");""");

            migrationBuilder.DropColumn(
                name: "Sheet",
                table: "DataSourceBindings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sheet",
                table: "DataSourceBindings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""UPDATE "DataSourceBindings" SET "Sheet" = COALESCE("Sheets" ->> 0, '');""");

            migrationBuilder.DropColumn(
                name: "Sheets",
                table: "DataSourceBindings");
        }
    }
}

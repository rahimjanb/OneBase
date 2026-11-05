using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Incentive2_StoreLimitRecipientLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Язык картинок автоотчётов у получателя (пусто — общий язык настроек): существующие получатели получают пусто — ничего не меняется.
            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxStores",
                schema: "incentive",
                table: "Programs",
                type: "integer",
                nullable: false,
                defaultValue: 12); // магазинов с целью на маршруте дня — как на образце ТЗ
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "MaxStores",
                schema: "incentive",
                table: "Programs");
        }
    }
}

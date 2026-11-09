using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RawPurchaseRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawRouteSteps",
                schema: "procurement",
                table: "Settings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "procurement",
                table: "Requests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Regular");

            // Заявки, отправленные прежним маршрутом сырья (руководитель снабжения → директор), — тип «Закупка сырья»; их шаги остаются прежними.
            migrationBuilder.Sql("UPDATE procurement.\"Requests\" SET \"Kind\" = 'Raw' WHERE \"RawRoute\";");

            migrationBuilder.AddColumn<string>(
                name: "RouteSteps",
                schema: "procurement",
                table: "Requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawRouteSteps",
                schema: "procurement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "procurement",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RouteSteps",
                schema: "procurement",
                table: "Requests");
        }
    }
}

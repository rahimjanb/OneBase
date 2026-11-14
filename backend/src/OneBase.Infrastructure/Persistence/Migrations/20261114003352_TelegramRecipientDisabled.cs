using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelegramRecipientDisabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabledReason",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DisabledUntil",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisabledAt",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "DisabledReason",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "DisabledUntil",
                schema: "telegram",
                table: "ReportSubscriptions");
        }
    }
}

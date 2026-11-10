using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentDialogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileContentType",
                schema: "telegram",
                table: "OutgoingMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileKey",
                schema: "telegram",
                table: "OutgoingMessages",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "telegram",
                table: "OutgoingMessages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                schema: "telegram",
                table: "OutgoingMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DialogReads",
                schema: "recruitment",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogReads", x => new { x.ConversationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_DialogReads_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "telegram",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DialogReads",
                schema: "recruitment");

            migrationBuilder.DropColumn(
                name: "FileContentType",
                schema: "telegram",
                table: "OutgoingMessages");

            migrationBuilder.DropColumn(
                name: "FileKey",
                schema: "telegram",
                table: "OutgoingMessages");

            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "telegram",
                table: "OutgoingMessages");

            migrationBuilder.DropColumn(
                name: "FileSize",
                schema: "telegram",
                table: "OutgoingMessages");
        }
    }
}

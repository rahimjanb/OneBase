using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiAnswerGrounding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CorrectionOfMessageId",
                schema: "ai",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionVerdict",
                schema: "ai",
                table: "AuditLogs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grounding",
                schema: "ai",
                table: "AuditLogs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectionOfMessageId",
                schema: "ai",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrectionVerdict",
                schema: "ai",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Grounding",
                schema: "ai",
                table: "AuditLogs");
        }
    }
}

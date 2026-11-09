using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AnalyzeResumes",
                schema: "recruitment",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisStatus",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Skipped");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedType",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrStatus",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "NotNeeded");

            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessedAt",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Profile",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Done");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TelegramAccountId",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TelegramMessageId",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TelegramPeerId",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "bigint",
                nullable: true);

            // Уже сохранённые резюме: файл в хранилище, текст прочитан или нет — статусы по факту (только новые столбцы).
            migrationBuilder.Sql("UPDATE recruitment.\"CandidateResumes\" SET \"OcrStatus\" = CASE WHEN \"Text\" IS NULL THEN 'Failed' WHEN \"TextByAi\" THEN 'Done' ELSE 'NotNeeded' END;");

            migrationBuilder.CreateTable(
                name: "Jobs",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Queue = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RelatedId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    RunAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Queue_Key",
                schema: "recruitment",
                table: "Jobs",
                columns: new[] { "Queue", "Key" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Queue_Status_RunAfter",
                schema: "recruitment",
                table: "Jobs",
                columns: new[] { "Queue", "Status", "RunAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_RelatedId",
                schema: "recruitment",
                table: "Jobs",
                column: "RelatedId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs",
                schema: "recruitment");

            migrationBuilder.DropColumn(
                name: "AnalyzeResumes",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AnalysisStatus",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "DetectedType",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "OcrStatus",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "PageCount",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "Profile",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "Summary",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "TelegramAccountId",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "TelegramMessageId",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "TelegramPeerId",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}

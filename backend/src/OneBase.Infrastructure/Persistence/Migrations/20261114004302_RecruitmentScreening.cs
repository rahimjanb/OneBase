using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiOnHhData",
                schema: "recruitment",
                table: "HhConnection",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GuardReason",
                schema: "recruitment",
                table: "HhConnection",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GuardTrippedAt",
                schema: "recruitment",
                table: "HhConnection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Confidence",
                schema: "recruitment",
                table: "CandidateScores",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                schema: "recruitment",
                table: "CandidateScores",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "recruitment",
                table: "CandidateScores",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "DesiredPosition",
                schema: "recruitment",
                table: "Candidates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Education",
                schema: "recruitment",
                table: "Candidates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceMonths",
                schema: "recruitment",
                table: "Candidates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastJob",
                schema: "recruitment",
                table: "Candidates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProfileAt",
                schema: "recruitment",
                table: "Candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryExpectation",
                schema: "recruitment",
                table: "Candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mark",
                schema: "recruitment",
                table: "Applications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "Mark",
                schema: "recruitment",
                table: "ApplicationEvents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CandidateComparisons",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationIds = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateComparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateComparisons_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalSchema: "recruitment",
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSkills",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Normalized = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateSkills_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "recruitment",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HhApiCalls",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Cost = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Allowed = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    ErrorType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HhApiCalls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateComparisons_VacancyId_CreatedAt",
                schema: "recruitment",
                table: "CandidateComparisons",
                columns: new[] { "VacancyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSkills_CandidateId_Normalized",
                schema: "recruitment",
                table: "CandidateSkills",
                columns: new[] { "CandidateId", "Normalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSkills_Normalized",
                schema: "recruitment",
                table: "CandidateSkills",
                column: "Normalized");

            migrationBuilder.CreateIndex(
                name: "IX_HhApiCalls_Allowed_At",
                schema: "recruitment",
                table: "HhApiCalls",
                columns: new[] { "Allowed", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_HhApiCalls_At",
                schema: "recruitment",
                table: "HhApiCalls",
                column: "At");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateComparisons",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "CandidateSkills",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "HhApiCalls",
                schema: "recruitment");

            migrationBuilder.DropColumn(
                name: "AiOnHhData",
                schema: "recruitment",
                table: "HhConnection");

            migrationBuilder.DropColumn(
                name: "GuardReason",
                schema: "recruitment",
                table: "HhConnection");

            migrationBuilder.DropColumn(
                name: "GuardTrippedAt",
                schema: "recruitment",
                table: "HhConnection");

            migrationBuilder.DropColumn(
                name: "Confidence",
                schema: "recruitment",
                table: "CandidateScores");

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                schema: "recruitment",
                table: "CandidateScores");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "recruitment",
                table: "CandidateScores");

            migrationBuilder.DropColumn(
                name: "DesiredPosition",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "Education",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "ExperienceMonths",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "LastJob",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "ProfileAt",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "SalaryExpectation",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "Mark",
                schema: "recruitment",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Mark",
                schema: "recruitment",
                table: "ApplicationEvents");
        }
    }
}

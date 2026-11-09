using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Recruitment3_HhAiInterviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiRecruiter",
                schema: "recruitment",
                table: "Vacancies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Criteria",
                schema: "recruitment",
                table: "Vacancies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HhVacancyId",
                schema: "recruitment",
                table: "Vacancies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AiEnabled",
                schema: "recruitment",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AssistantName",
                schema: "recruitment",
                table: "Settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoScore",
                schema: "recruitment",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpHours",
                schema: "recruitment",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "InterviewDayEnd",
                schema: "recruitment",
                table: "Settings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(18, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "InterviewDayStart",
                schema: "recruitment",
                table: "Settings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(10, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "InterviewDays",
                schema: "recruitment",
                table: "Settings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "1,2,3,4,5,6");

            migrationBuilder.AddColumn<int>(
                name: "InterviewSlotMinutes",
                schema: "recruitment",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 45);

            migrationBuilder.CreateTable(
                name: "CandidateScores",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Overall = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CriteriaJson = table.Column<string>(type: "text", nullable: false),
                    Risks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateScores_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "recruitment",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Exceptions",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DedupKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exceptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HhConnection",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProtectedClientSecret = table.Column<string>(type: "text", nullable: true),
                    RedirectUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ProtectedAccessToken = table.Column<string>(type: "text", nullable: true),
                    ProtectedRefreshToken = table.Column<string>(type: "text", nullable: true),
                    TokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StateExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmployerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EmployerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConnectedById = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncOk = table.Column<bool>(type: "boolean", nullable: true),
                    LastSyncMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HhConnection", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HhNegotiations",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NegotiationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HhVacancyId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResumeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResumeUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ActionsJson = table.Column<string>(type: "text", nullable: true),
                    HhUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResumeFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HhNegotiations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Interviews",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProposedSlotsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CalendarEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemindedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interviews_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "recruitment",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VacancyInterviewers",
                schema: "recruitment",
                columns: table => new
                {
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyInterviewers", x => new { x.VacancyId, x.UserId });
                    table.ForeignKey(
                        name: "FK_VacancyInterviewers_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalSchema: "recruitment",
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateScores_ApplicationId_CreatedAt",
                schema: "recruitment",
                table: "CandidateScores",
                columns: new[] { "ApplicationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Exceptions_CandidateId",
                schema: "recruitment",
                table: "Exceptions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_Exceptions_DedupKey",
                schema: "recruitment",
                table: "Exceptions",
                column: "DedupKey",
                unique: true,
                filter: "\"Status\" = 'Open' AND \"DedupKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Exceptions_Status_CreatedAt",
                schema: "recruitment",
                table: "Exceptions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HhNegotiations_ApplicationId",
                schema: "recruitment",
                table: "HhNegotiations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_HhNegotiations_CandidateId",
                schema: "recruitment",
                table: "HhNegotiations",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_HhNegotiations_NegotiationId",
                schema: "recruitment",
                table: "HhNegotiations",
                column: "NegotiationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_ApplicationId",
                schema: "recruitment",
                table: "Interviews",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_CandidateId",
                schema: "recruitment",
                table: "Interviews",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_Status_StartsAt",
                schema: "recruitment",
                table: "Interviews",
                columns: new[] { "Status", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateScores",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Exceptions",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "HhConnection",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "HhNegotiations",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Interviews",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "VacancyInterviewers",
                schema: "recruitment");

            migrationBuilder.DropColumn(
                name: "AiRecruiter",
                schema: "recruitment",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "Criteria",
                schema: "recruitment",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "HhVacancyId",
                schema: "recruitment",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "AiEnabled",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AssistantName",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AutoScore",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FollowUpHours",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "InterviewDayEnd",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "InterviewDayStart",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "InterviewDays",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "InterviewSlotMinutes",
                schema: "recruitment",
                table: "Settings");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentInterviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                schema: "recruitment",
                table: "Interviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                schema: "recruitment",
                table: "Interviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                schema: "recruitment",
                table: "Interviews",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CandidateConfirmation",
                schema: "recruitment",
                table: "Interviews",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "recruitment",
                table: "Interviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HoldUntil",
                schema: "recruitment",
                table: "Interviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InterviewerId",
                schema: "recruitment",
                table: "Interviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingUrl",
                schema: "recruitment",
                table: "Interviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Participants",
                schema: "recruitment",
                table: "Interviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreviousStartsAt",
                schema: "recruitment",
                table: "Interviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "recruitment",
                table: "Interviews",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Asia/Tashkent");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "recruitment",
                table: "Interviews",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            // Уже назначенные собеседования кандидат выбирал сам (номер варианта) — подтверждены; только новые столбцы.
            migrationBuilder.Sql("UPDATE recruitment.\"Interviews\" SET \"CandidateConfirmation\" = 'Confirmed' WHERE \"Status\" IN ('Confirmed', 'Completed', 'NoShow');");

            migrationBuilder.CreateTable(
                name: "InterviewFeedback",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Result = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Technical = table.Column<int>(type: "integer", nullable: true),
                    Communication = table.Column<int>(type: "integer", nullable: true),
                    Professional = table.Column<int>(type: "integer", nullable: true),
                    Strengths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Weaknesses = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Comments = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Recommendation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NextStage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewFeedback_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalSchema: "recruitment",
                        principalTable: "Interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewSettings",
                schema: "recruitment",
                columns: table => new
                {
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Days = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DayStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DayEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Breaks = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MinNoticeHours = table.Column<int>(type: "integer", nullable: true),
                    MaxAdvanceDays = table.Column<int>(type: "integer", nullable: true),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MeetingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    InviteMinScore = table.Column<int>(type: "integer", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewSettings", x => x.VacancyId);
                    table.ForeignKey(
                        name: "FK_InterviewSettings_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalSchema: "recruitment",
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_CalendarEventId",
                schema: "recruitment",
                table: "Interviews",
                column: "CalendarEventId",
                unique: true,
                filter: "\"CalendarEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewFeedback_InterviewId_AuthorId",
                schema: "recruitment",
                table: "InterviewFeedback",
                columns: new[] { "InterviewId", "AuthorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewFeedback",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "InterviewSettings",
                schema: "recruitment");

            migrationBuilder.DropIndex(
                name: "IX_Interviews_CalendarEventId",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "CandidateConfirmation",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "HoldUntil",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "InterviewerId",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "MeetingUrl",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "Participants",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "PreviousStartsAt",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "recruitment",
                table: "Interviews");
        }
    }
}

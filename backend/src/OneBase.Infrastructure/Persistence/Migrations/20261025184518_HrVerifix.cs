using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrVerifix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.AddColumn<string>(
                name: "SettingsJson",
                table: "Integrations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifixId = table.Column<long>(type: "bigint", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MiddleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FullName = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    State = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    HiringDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DismissalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DivisionId = table.Column<long>(type: "bigint", nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    JobId = table.Column<long>(type: "bigint", nullable: true),
                    ScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    StaffId = table.Column<long>(type: "bigint", nullable: true),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NoteUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NoteUpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MissingSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrEmployees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "References",
                schema: "hr",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    VerifixId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    State = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClosedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DepartmentCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MissingSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrReferences", x => new { x.Kind, x.VerifixId });
                });

            migrationBuilder.CreateTable(
                name: "SyncRuns",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RetryOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Created = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<int>(type: "integer", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StepsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                schema: "hr",
                columns: table => new
                {
                    Entity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CoveredThrough = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CoveredFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    LastRows = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastErrorAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrSyncStates", x => x.Entity);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceEvents",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VerifixTrackId = table.Column<long>(type: "bigint", nullable: false),
                    VerifixEmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffId = table.Column<long>(type: "bigint", nullable: true),
                    DivisionId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    ScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    TrackType = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    MarkType = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    RawTime = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LocalTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MissingSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAttendanceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrAttendanceEvents_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetDays",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    VerifixEmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<long>(type: "bigint", nullable: true),
                    JobId = table.Column<long>(type: "bigint", nullable: true),
                    ScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    DayKind = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    PlanBegin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PlanEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BreakBegin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BreakEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PlanMinutes = table.Column<int>(type: "integer", nullable: true),
                    FirstIn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastOut = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InputLocationId = table.Column<long>(type: "bigint", nullable: true),
                    OutputLocationId = table.Column<long>(type: "bigint", nullable: true),
                    DoneMarks = table.Column<int>(type: "integer", nullable: true),
                    PlannedMarks = table.Column<int>(type: "integer", nullable: true),
                    FactsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MissingSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTimesheetDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrTimesheetDays_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrAttendanceEvents_EmployeeId_LocalTime",
                schema: "hr",
                table: "AttendanceEvents",
                columns: new[] { "EmployeeId", "LocalTime" });

            migrationBuilder.CreateIndex(
                name: "IX_HrAttendanceEvents_LocalDate_Employee",
                schema: "hr",
                table: "AttendanceEvents",
                columns: new[] { "LocalDate", "VerifixEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HrAttendanceEvents_VerifixTrackId",
                schema: "hr",
                table: "AttendanceEvents",
                column: "VerifixTrackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployees_Status_DivisionId",
                schema: "hr",
                table: "Employees",
                columns: new[] { "Status", "DivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployees_VerifixId",
                schema: "hr",
                table: "Employees",
                column: "VerifixId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrSyncRuns_SingleRunning",
                schema: "hr",
                table: "SyncRuns",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_HrSyncRuns_StartedAt",
                schema: "hr",
                table: "SyncRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HrTimesheetDays_Date_EmployeeId",
                schema: "hr",
                table: "TimesheetDays",
                columns: new[] { "Date", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HrTimesheetDays_StaffId_Date",
                schema: "hr",
                table: "TimesheetDays",
                columns: new[] { "StaffId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetDays_EmployeeId",
                schema: "hr",
                table: "TimesheetDays",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceEvents",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "References",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "SyncRuns",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "SyncStates",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "TimesheetDays",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "hr");

            migrationBuilder.DropColumn(
                name: "SettingsJson",
                table: "Integrations");
        }
    }
}

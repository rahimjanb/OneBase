using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkTasksProjectsSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                schema: "work",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedById",
                schema: "work",
                table: "Tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedReason",
                schema: "work",
                table: "Tasks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueSoonNotifiedFor",
                schema: "work",
                table: "Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                schema: "work",
                table: "Tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OverdueNotifiedFor",
                schema: "work",
                table: "Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Progress",
                schema: "work",
                table: "Tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                schema: "work",
                table: "Tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewRequestedAt",
                schema: "work",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                schema: "work",
                table: "Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuggestionId",
                schema: "work",
                table: "Tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DepartmentHeads",
                schema: "work",
                columns: table => new
                {
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkDepartmentHeads", x => new { x.DepartmentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkDepartmentHeads_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkDepartmentHeads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkProjects_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskAttachments",
                schema: "work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTaskAttachments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkTaskAttachments_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "work",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskChecklist",
                schema: "work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DoneById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskChecklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTaskChecklist_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "work",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskComments",
                schema: "work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTaskComments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkTaskComments_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "work",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskEvents",
                schema: "work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTaskEvents_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkTaskEvents_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "work",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskSuggestions",
                schema: "work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Problem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Facts = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    InsufficientData = table.Column<bool>(type: "boolean", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceHref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequiredPermission = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Model = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTaskSuggestions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkTaskSuggestions_Users_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                schema: "work",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsManager = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkProjectMembers", x => new { x.ProjectId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkProjectMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkProjectMembers_WorkProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "work",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_ArchivedAt",
                schema: "work",
                table: "Tasks",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_ProjectId",
                schema: "work",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_Status_DueDate",
                schema: "work",
                table: "Tasks",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDepartmentHeads_UserId",
                schema: "work",
                table: "DepartmentHeads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkProjectMembers_UserId",
                schema: "work",
                table: "ProjectMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkProjects_DepartmentId",
                schema: "work",
                table: "Projects",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_UploadedById",
                schema: "work",
                table: "TaskAttachments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskAttachments_TaskId",
                schema: "work",
                table: "TaskAttachments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskChecklist_TaskId_SortOrder",
                schema: "work",
                table: "TaskChecklist",
                columns: new[] { "TaskId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_AuthorId",
                schema: "work",
                table: "TaskComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskComments_TaskId_CreatedAt",
                schema: "work",
                table: "TaskComments",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvents_ActorId",
                schema: "work",
                table: "TaskEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskEvents_TaskId_CreatedAt",
                schema: "work",
                table: "TaskEvents",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskSuggestions_AssigneeId",
                schema: "work",
                table: "TaskSuggestions",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskSuggestions_DepartmentId",
                schema: "work",
                table: "TaskSuggestions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskSuggestions_Fingerprint",
                schema: "work",
                table: "TaskSuggestions",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskSuggestions_Status_DepartmentId",
                schema: "work",
                table: "TaskSuggestions",
                columns: new[] { "Status", "DepartmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_Projects_ProjectId",
                schema: "work",
                table: "Tasks",
                column: "ProjectId",
                principalSchema: "work",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Существующие данные: выполненные задачи — 100%; уже просроченные до этого выпуска считаются уведомлёнными — напоминания
            // придут о задачах, которые станут просроченными после него, а не всей прежней очередью в первый же запуск.
            migrationBuilder.Sql("""
                UPDATE work."Tasks" SET "Progress" = 100 WHERE "Status" = 'Done';
                UPDATE work."Tasks" SET "OverdueNotifiedFor" = "DueDate"
                WHERE "DueDate" IS NOT NULL AND "DueDate" < (now() AT TIME ZONE 'Asia/Tashkent')::date AND "Status" IN ('New', 'InProgress');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_Projects_ProjectId",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "DepartmentHeads",
                schema: "work");

            migrationBuilder.DropTable(
                name: "ProjectMembers",
                schema: "work");

            migrationBuilder.DropTable(
                name: "TaskAttachments",
                schema: "work");

            migrationBuilder.DropTable(
                name: "TaskChecklist",
                schema: "work");

            migrationBuilder.DropTable(
                name: "TaskComments",
                schema: "work");

            migrationBuilder.DropTable(
                name: "TaskEvents",
                schema: "work");

            migrationBuilder.DropTable(
                name: "TaskSuggestions",
                schema: "work");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "work");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_ArchivedAt",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_ProjectId",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_Status_DueDate",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ArchivedById",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "BlockedReason",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "DueSoonNotifiedFor",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedFor",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Progress",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ReviewRequestedAt",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "SuggestionId",
                schema: "work",
                table: "Tasks");
        }
    }
}

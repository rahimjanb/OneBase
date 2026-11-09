using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DialogAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgentId",
                schema: "recruitment",
                table: "Vacancies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreeningQuestions",
                schema: "recruitment",
                table: "Vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReadScans",
                schema: "recruitment",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: true); // распознавание фото и сканов резюме — по умолчанию включено (и у существующей строки настроек)

            migrationBuilder.AddColumn<int>(
                name: "TypingSeconds",
                schema: "telegram",
                table: "OutgoingMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TextByAi",
                schema: "recruitment",
                table: "CandidateResumes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScreeningAnswers",
                schema: "recruitment",
                table: "Applications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DialogAgents",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Department = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Intro = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Tone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Length = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Emoji = table.Column<bool>(type: "boolean", nullable: false),
                    SplitMessages = table.Column<bool>(type: "boolean", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ScreeningQuestions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AllowedTopics = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ForbiddenTopics = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ForbiddenPhrases = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HandoffPhrases = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MaxReplyChars = table.Column<int>(type: "integer", nullable: false),
                    MaxRepliesPerHour = table.Column<int>(type: "integer", nullable: false),
                    MaxRepliesPerDay = table.Column<int>(type: "integer", nullable: false),
                    CanDiscussSalary = table.Column<bool>(type: "boolean", nullable: false),
                    CanScheduleInterviews = table.Column<bool>(type: "boolean", nullable: false),
                    HandoffReply = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WorkFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    WorkTo = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    WorkDays = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OffHours = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReplyDelayMinSeconds = table.Column<int>(type: "integer", nullable: false),
                    ReplyDelayMaxSeconds = table.Column<int>(type: "integer", nullable: false),
                    Typing = table.Column<bool>(type: "boolean", nullable: false),
                    FollowUpHours = table.Column<int>(type: "integer", nullable: false),
                    ModelProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogAgents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DialogAgentAnswers",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogAgentAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogAgentAnswers_DialogAgents_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "ai",
                        principalTable: "DialogAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogAgentReplies",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    PeerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IncomingMessageId = table.Column<int>(type: "integer", nullable: true),
                    Incoming = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Action = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BlockReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Correction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    InterviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogAgentReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogAgentReplies_DialogAgents_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "ai",
                        principalTable: "DialogAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DialogAgentAnswers_AgentId_SortOrder",
                schema: "ai",
                table: "DialogAgentAnswers",
                columns: new[] { "AgentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogAgentReplies_AgentId_CreatedAt",
                schema: "ai",
                table: "DialogAgentReplies",
                columns: new[] { "AgentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogAgentReplies_CandidateId",
                schema: "ai",
                table: "DialogAgentReplies",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogAgentReplies_ConversationId_Status",
                schema: "ai",
                table: "DialogAgentReplies",
                columns: new[] { "ConversationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogAgents_Department_Kind",
                schema: "ai",
                table: "DialogAgents",
                columns: new[] { "Department", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DialogAgentAnswers",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "DialogAgentReplies",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "DialogAgents",
                schema: "ai");

            migrationBuilder.DropColumn(
                name: "AgentId",
                schema: "recruitment",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "ScreeningQuestions",
                schema: "recruitment",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "ReadScans",
                schema: "recruitment",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TypingSeconds",
                schema: "telegram",
                table: "OutgoingMessages");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "TextByAi",
                schema: "recruitment",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "ScreeningAnswers",
                schema: "recruitment",
                table: "Applications");
        }
    }
}

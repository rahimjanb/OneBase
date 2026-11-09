using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Recruitment2_Telegram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyLimit",
                schema: "telegram",
                table: "AccountDepartments",
                type: "integer",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<int>(
                name: "MinIntervalSeconds",
                schema: "telegram",
                table: "AccountDepartments",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "PauseReason",
                schema: "telegram",
                table: "AccountDepartments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedUntil",
                schema: "telegram",
                table: "AccountDepartments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<int>(type: "integer", nullable: true),
                    Direction = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Author = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "telegram",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatPeerId = table.Column<long>(type: "bigint", nullable: false),
                    ChatTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    FromUserId = table.Column<long>(type: "bigint", nullable: true),
                    FromUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Excerpt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Keywords = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutgoingMessages",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Department = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ReplyToMessageId = table.Column<int>(type: "integer", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MessageIds = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RelatedType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RelatedId = table.Column<Guid>(type: "uuid", nullable: true),
                    Author = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutgoingMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutgoingMessages_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Publications",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    ChatTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChatUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OutgoingId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<int>(type: "integer", nullable: true),
                    Link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publications_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalSchema: "recruitment",
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CollectLeads = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_AccountId_PeerId_MessageId_Direction",
                schema: "telegram",
                table: "ConversationMessages",
                columns: new[] { "AccountId", "PeerId", "MessageId", "Direction" },
                unique: true,
                filter: "\"MessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId_SentAt",
                schema: "telegram",
                table: "ConversationMessages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AccountId_ChatPeerId_MessageId",
                schema: "recruitment",
                table: "Leads",
                columns: new[] { "AccountId", "ChatPeerId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Status_CreatedAt",
                schema: "recruitment",
                table: "Leads",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingMessages_AccountId_Department_SentAt",
                schema: "telegram",
                table: "OutgoingMessages",
                columns: new[] { "AccountId", "Department", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingMessages_IdempotencyKey",
                schema: "telegram",
                table: "OutgoingMessages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingMessages_RelatedType_RelatedId",
                schema: "telegram",
                table: "OutgoingMessages",
                columns: new[] { "RelatedType", "RelatedId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingMessages_Status_NextAttemptAt",
                schema: "telegram",
                table: "OutgoingMessages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Publications_AccountId_PeerId_MessageId",
                schema: "recruitment",
                table: "Publications",
                columns: new[] { "AccountId", "PeerId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Publications_Status_ScheduledAt",
                schema: "recruitment",
                table: "Publications",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Publications_VacancyId_CreatedAt",
                schema: "recruitment",
                table: "Publications",
                columns: new[] { "VacancyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sources_AccountId_PeerId",
                schema: "recruitment",
                table: "Sources",
                columns: new[] { "AccountId", "PeerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMessages",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "Leads",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "OutgoingMessages",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "Publications",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Sources",
                schema: "recruitment");

            migrationBuilder.DropColumn(
                name: "DailyLimit",
                schema: "telegram",
                table: "AccountDepartments");

            migrationBuilder.DropColumn(
                name: "MinIntervalSeconds",
                schema: "telegram",
                table: "AccountDepartments");

            migrationBuilder.DropColumn(
                name: "PauseReason",
                schema: "telegram",
                table: "AccountDepartments");

            migrationBuilder.DropColumn(
                name: "PausedUntil",
                schema: "telegram",
                table: "AccountDepartments");
        }
    }
}

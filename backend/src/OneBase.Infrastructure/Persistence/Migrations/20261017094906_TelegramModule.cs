using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelegramModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "telegram");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProtectedPhone = table.Column<string>(type: "text", nullable: true),
                    PhoneHint = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProtectedSession = table.Column<string>(type: "text", nullable: true),
                    UpdateState = table.Column<string>(type: "text", nullable: true),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkCodes",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentExecutions",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomingMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Question = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Answer = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReplyMessageId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentExecutions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSettings",
                schema: "telegram",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoReply = table.Column<bool>(type: "boolean", nullable: false),
                    ReplyToUnknown = table.Column<bool>(type: "boolean", nullable: false),
                    ModelProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SystemPrompt = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Restrictions = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ResponseStyle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    MaxMessagesPerHour = table.Column<int>(type: "integer", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    ReplyDelaySeconds = table.Column<int>(type: "integer", nullable: false),
                    NoInfoReply = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AllowedAttachments = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaxFileMb = table.Column<int>(type: "integer", nullable: false),
                    AllowReports = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSettings", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_AgentSettings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Formatted = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DelaySeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Chats",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsChannelPeer = table.Column<bool>(type: "boolean", nullable: false),
                    AccessHash = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CanSend = table.Column<bool>(type: "boolean", nullable: false),
                    AgentEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chats_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomingMessages",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    FromUserId = table.Column<long>(type: "bigint", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomingMessages_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    HasText = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materials_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllowReports = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignAttachments",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "telegram",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignRecipients",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MessageIds = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignRecipients_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "telegram",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialChunks",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialChunks_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalSchema: "telegram",
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TelegramUserId",
                schema: "telegram",
                table: "Accounts",
                column: "TelegramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutions_AccountId_CreatedAt",
                schema: "telegram",
                table: "AgentExecutions",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutions_AccountId_TelegramUserId_CreatedAt",
                schema: "telegram",
                table: "AgentExecutions",
                columns: new[] { "AccountId", "TelegramUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_CampaignId",
                schema: "telegram",
                table: "CampaignAttachments",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_CampaignId_PeerId",
                schema: "telegram",
                table: "CampaignRecipients",
                columns: new[] { "CampaignId", "PeerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_Status_NextAttemptAt",
                schema: "telegram",
                table: "CampaignRecipients",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_AccountId",
                schema: "telegram",
                table: "Campaigns",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_CreatedAt",
                schema: "telegram",
                table: "Campaigns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Status_ScheduledAt",
                schema: "telegram",
                table: "Campaigns",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Chats_AccountId_PeerId",
                schema: "telegram",
                table: "Chats",
                columns: new[] { "AccountId", "PeerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomingMessages_AccountId_PeerId_MessageId",
                schema: "telegram",
                table: "IncomingMessages",
                columns: new[] { "AccountId", "PeerId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkCodes_CodeHash",
                schema: "telegram",
                table: "LinkCodes",
                column: "CodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_LinkCodes_UserId",
                schema: "telegram",
                table: "LinkCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialChunks_AccountId_PeerId",
                schema: "telegram",
                table: "MaterialChunks",
                columns: new[] { "AccountId", "PeerId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialChunks_MaterialId",
                schema: "telegram",
                table: "MaterialChunks",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_AccountId_PeerId_CreatedAt",
                schema: "telegram",
                table: "Materials",
                columns: new[] { "AccountId", "PeerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_AccountId_TelegramUserId",
                schema: "telegram",
                table: "UserPermissions",
                columns: new[] { "AccountId", "TelegramUserId" },
                unique: true,
                filter: "\"TelegramUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId",
                schema: "telegram",
                table: "UserPermissions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentExecutions",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "AgentSettings",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "CampaignAttachments",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "CampaignRecipients",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "Chats",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "IncomingMessages",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "LinkCodes",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "MaterialChunks",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "UserPermissions",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "Campaigns",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "Materials",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "telegram");
        }
    }
}

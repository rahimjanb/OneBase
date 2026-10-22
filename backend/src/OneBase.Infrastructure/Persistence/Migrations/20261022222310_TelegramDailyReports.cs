using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelegramDailyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportDeliveries",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DataMode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeerId = table.Column<long>(type: "bigint", nullable: true),
                    RecipientTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ScopeTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupervisorId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Pages = table.Column<int>(type: "integer", nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    MessageIds = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DataAsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportSchedules",
                schema: "telegram",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    Metric = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ArmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSchedules", x => x.Kind);
                });

            migrationBuilder.CreateTable(
                name: "ReportSettings",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSettings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReportSubscriptions",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<long>(type: "bigint", nullable: true),
                    SupervisorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Morning = table.Column<bool>(type: "boolean", nullable: false),
                    Midday = table.Column<bool>(type: "boolean", nullable: false),
                    Evening = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportSubscriptionChats",
                schema: "telegram",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSubscriptionChats", x => new { x.SubscriptionId, x.ChatId });
                    table.ForeignKey(
                        name: "FK_ReportSubscriptionChats_Chats_ChatId",
                        column: x => x.ChatId,
                        principalSchema: "telegram",
                        principalTable: "Chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReportSubscriptionChats_ReportSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "telegram",
                        principalTable: "ReportSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDeliveries_CreatedAt",
                schema: "telegram",
                table: "ReportDeliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDeliveries_Status_NextAttemptAt",
                schema: "telegram",
                table: "ReportDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDeliveries_SubscriptionId_ChatId_Kind_Period",
                schema: "telegram",
                table: "ReportDeliveries",
                columns: new[] { "SubscriptionId", "ChatId", "Kind", "Period" },
                unique: true,
                filter: "\"Trigger\" = 'schedule' AND \"SubscriptionId\" IS NOT NULL AND \"ChatId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSettings_AccountId",
                schema: "telegram",
                table: "ReportSettings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubscriptionChats_ChatId",
                schema: "telegram",
                table: "ReportSubscriptionChats",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubscriptions_RegionId",
                schema: "telegram",
                table: "ReportSubscriptions",
                column: "RegionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportDeliveries",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "ReportSchedules",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "ReportSettings",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "ReportSubscriptionChats",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "ReportSubscriptions",
                schema: "telegram");
        }
    }
}

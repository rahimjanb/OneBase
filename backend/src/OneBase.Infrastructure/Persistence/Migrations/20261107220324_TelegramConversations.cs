using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelegramConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountDepartments",
                schema: "telegram",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Department = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDepartments", x => new { x.AccountId, x.Department });
                    table.ForeignKey(
                        name: "FK_AccountDepartments_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<long>(type: "bigint", nullable: false),
                    ChatType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    Department = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RouteReason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AssignedAgent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RelatedId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastInboundAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations1", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "telegram",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AccountId_Department_LastMessageAt",
                schema: "telegram",
                table: "Conversations",
                columns: new[] { "AccountId", "Department", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AccountId_PeerId",
                schema: "telegram",
                table: "Conversations",
                columns: new[] { "AccountId", "PeerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_RelatedType_RelatedId",
                schema: "telegram",
                table: "Conversations",
                columns: new[] { "RelatedType", "RelatedId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountDepartments",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "telegram");
        }
    }
}

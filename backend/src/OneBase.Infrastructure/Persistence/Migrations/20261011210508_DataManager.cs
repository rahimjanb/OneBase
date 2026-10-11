using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DataManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataChangeSets",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Target = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PlanKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Summary = table.Column<string>(type: "jsonb", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppliedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Verification = table.Column<string>(type: "jsonb", nullable: true),
                    RolledBackAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RolledBackById = table.Column<Guid>(type: "uuid", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataChangeSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataConversations",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Transcript = table.Column<string>(type: "jsonb", nullable: false),
                    BusySince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataFiles",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataChangeItems",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Before = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    After = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    SourceRef = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataChangeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataChangeItems_DataChangeSets_ChangeSetId",
                        column: x => x.ChangeSetId,
                        principalSchema: "ai",
                        principalTable: "DataChangeSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataMessages",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Steps = table.Column<string>(type: "jsonb", nullable: true),
                    FileIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChangeSetIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataMessages_DataConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "ai",
                        principalTable: "DataConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeItems_ChangeSetId_Position",
                schema: "ai",
                table: "DataChangeItems",
                columns: new[] { "ChangeSetId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeItems_EntityKey_Status",
                schema: "ai",
                table: "DataChangeItems",
                columns: new[] { "EntityKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeSets_ConversationId",
                schema: "ai",
                table: "DataChangeSets",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeSets_RequestHash",
                schema: "ai",
                table: "DataChangeSets",
                column: "RequestHash");

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeSets_Status_CreatedAt",
                schema: "ai",
                table: "DataChangeSets",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DataConversations_UserId_LastMessageAt",
                schema: "ai",
                table: "DataConversations",
                columns: new[] { "UserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DataFiles_Sha256",
                schema: "ai",
                table: "DataFiles",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_DataFiles_UserId_CreatedAt",
                schema: "ai",
                table: "DataFiles",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DataMessages_ConversationId_CreatedAt",
                schema: "ai",
                table: "DataMessages",
                columns: new[] { "ConversationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataChangeItems",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "DataFiles",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "DataMessages",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "DataChangeSets",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "DataConversations",
                schema: "ai");
        }
    }
}

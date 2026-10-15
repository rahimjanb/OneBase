using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DataSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataSourceConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SpreadsheetId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Access = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProtectedKey = table.Column<string>(type: "text", nullable: true),
                    ServiceAccountEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastCheckOk = table.Column<bool>(type: "boolean", nullable: true),
                    LastCheckMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Sheets = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSourceBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Section = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sheet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeaderRow = table.Column<int>(type: "integer", nullable: false),
                    Fields = table.Column<string>(type: "jsonb", nullable: false),
                    Schedule = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: true),
                    DailyAt = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LastMessage = table.Column<string>(type: "character varying(1100)", maxLength: 1100, nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSourceBindings_DataSourceConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "DataSourceConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DataSourceSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(1100)", maxLength: 1100, nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceSyncRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSourceSyncRuns_DataSourceBindings_BindingId",
                        column: x => x.BindingId,
                        principalTable: "DataSourceBindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceBindings_ConnectionId",
                table: "DataSourceBindings",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceBindings_Enabled_NextRunAt",
                table: "DataSourceBindings",
                columns: new[] { "Enabled", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceBindings_Section",
                table: "DataSourceBindings",
                column: "Section");

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceSyncRuns_BindingId_CreatedAt",
                table: "DataSourceSyncRuns",
                columns: new[] { "BindingId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataSourceSyncRuns");

            migrationBuilder.DropTable(
                name: "DataSourceBindings");

            migrationBuilder.DropTable(
                name: "DataSourceConnections");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DataSourceOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceBindingId",
                schema: "ai",
                table: "DataChangeSets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataSourceConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BindingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Entity = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Rule = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Decision = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NeedsAction = table.Column<bool>(type: "boolean", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Occurrences = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceConflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSourceConflicts_DataSourceBindings_BindingId",
                        column: x => x.BindingId,
                        principalTable: "DataSourceBindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceConflicts_BindingId_Fingerprint",
                table: "DataSourceConflicts",
                columns: new[] { "BindingId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceConflicts_ResolvedAt_LastSeenAt",
                table: "DataSourceConflicts",
                columns: new[] { "ResolvedAt", "LastSeenAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataSourceConflicts");

            migrationBuilder.DropColumn(
                name: "SourceBindingId",
                schema: "ai",
                table: "DataChangeSets");
        }
    }
}

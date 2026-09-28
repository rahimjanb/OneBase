using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "AgentPlans",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkoUserId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    PlanKg = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Directions",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ManagerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Directions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Targets",
                schema: "sales",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Targets", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkoBranchId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DirectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupervisorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DealerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regions_Directions_DirectionId",
                        column: x => x.DirectionId,
                        principalSchema: "sales",
                        principalTable: "Directions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentProfiles",
                schema: "sales",
                columns: table => new
                {
                    LinkoUserId = table.Column<long>(type: "bigint", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsVacancy = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentProfiles", x => x.LinkoUserId);
                    table.ForeignKey(
                        name: "FK_AgentProfiles_Regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "sales",
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RegionPlans",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    PlanKg = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegionPlans_Regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "sales",
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlans_LinkoUserId_Kind_Year_Month_CategoryId",
                schema: "sales",
                table: "AgentPlans",
                columns: new[] { "LinkoUserId", "Kind", "Year", "Month", "CategoryId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AgentProfiles_RegionId",
                schema: "sales",
                table: "AgentProfiles",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegionPlans_RegionId_Kind_Year_Month_CategoryId",
                schema: "sales",
                table: "RegionPlans",
                columns: new[] { "RegionId", "Kind", "Year", "Month", "CategoryId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_Regions_DirectionId",
                schema: "sales",
                table: "Regions",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_LinkoBranchId",
                schema: "sales",
                table: "Regions",
                column: "LinkoBranchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPlans",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "AgentProfiles",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "RegionPlans",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "Targets",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "Regions",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "Directions",
                schema: "sales");
        }
    }
}

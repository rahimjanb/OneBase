using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesUserScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserScopes",
                schema: "sales",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DirectionIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    RegionIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    AgentId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserScopes", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserScopes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserScopes_AgentId",
                schema: "sales",
                table: "UserScopes",
                column: "AgentId");

            // Выгрузки Excel «Продаж» стали правом с галочкой (sales.export). У кого права уже отмечены галочками (Rights не null), а роль даёт
            // раздел «Продажи», выгрузки остаются — право дописывается в их список. Только добавление; остальные права и данные не меняются.
            migrationBuilder.Sql("""
                UPDATE "Users" u SET "Rights" = array_append(u."Rights", 'sales.export')
                WHERE u."Rights" IS NOT NULL
                  AND NOT ('sales.export' = ANY(u."Rights"))
                  AND EXISTS (SELECT 1 FROM "UserRole" ur JOIN "RolePermission" rp ON rp."RoleId" = ur."RoleId"
                              WHERE ur."UserId" = u."Id" AND rp."Code" = 'sales.read');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserScopes",
                schema: "sales");
        }
    }
}

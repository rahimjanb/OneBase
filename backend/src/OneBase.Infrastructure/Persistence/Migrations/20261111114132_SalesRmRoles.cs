using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesRmRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Роль Sales Base «РМ (Sales Base)» больше не открывает раздел «Продажи» OneBase: продажи по регионам — у ролей «РМ» и
            // «Супервайзер продаж» (создаются при запуске). Снимаются только эти два права у этой одной роли; пользователи и данные не меняются.
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission"
                WHERE "Code" IN ('sales.read', 'sales.export')
                  AND "RoleId" IN (SELECT "Id" FROM "Roles" WHERE "Name" = 'РМ (Sales Base)');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "RolePermission" ("RoleId", "Code")
                SELECT r."Id", c."Code" FROM "Roles" r CROSS JOIN (VALUES ('sales.read'), ('sales.export')) AS c("Code")
                WHERE r."Name" = 'РМ (Sales Base)'
                  AND NOT EXISTS (SELECT 1 FROM "RolePermission" rp WHERE rp."RoleId" = r."Id" AND rp."Code" = c."Code");
                """);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analytik2_AccessByRole : Migration
    {
        /// <summary>Роли, которым раздел «Аналитик» выдавался автоматически при запуске (SystemRoles.Upgrades до этой миграции).</summary>
        private const string Roles = "'Продажи', 'Финансы', 'HR', 'Производство', 'Снабжение', 'Маркетинг', 'РМ', 'Супервайзер продаж', 'Аналитик'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // «Аналитик» виден не всем: по умолчанию — директору (и администратору со всеми правами), остальным его открывают в окне пользователя
            // (раздел «Аналитик»). Снимается только право analytik.use у ролей, которым его раздал запуск сервера; пользователи, их отмеченные
            // разделы и данные Аналитика не меняются.
            migrationBuilder.Sql($"""
                DELETE FROM "RolePermission"
                WHERE "Code" = 'analytik.use'
                  AND "RoleId" IN (SELECT "Id" FROM "Roles" WHERE "Name" IN ({Roles}));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO "RolePermission" ("RoleId", "Code")
                SELECT r."Id", 'analytik.use' FROM "Roles" r
                WHERE r."Name" IN ({Roles})
                  AND NOT EXISTS (SELECT 1 FROM "RolePermission" rp WHERE rp."RoleId" = r."Id" AND rp."Code" = 'analytik.use');
                """);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analytik4_LinkoSyncRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // История загрузок Linko (журнал OneBase) — в схеме sales, чтобы «Очистить и загрузить заново» её не стирала. Ранняя сборка разработки
            // успела создать её как linko."SyncRuns" (миграция 20261112193022): такая таблица переносится со всеми строками, иначе создаётся новая.
            // Ничего не удаляется; запись о прежней миграции убирается из журнала миграций (её в коде больше нет).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('sales."LinkoSyncRuns"') IS NULL AND to_regclass('linko."SyncRuns"') IS NOT NULL THEN
                        ALTER TABLE linko."SyncRuns" SET SCHEMA sales;
                        ALTER TABLE sales."SyncRuns" RENAME TO "LinkoSyncRuns";
                        ALTER TABLE sales."LinkoSyncRuns" RENAME CONSTRAINT "PK_SyncRuns" TO "PK_LinkoSyncRuns";
                        ALTER INDEX sales."IX_SyncRuns_StartedAt" RENAME TO "IX_LinkoSyncRuns_StartedAt";
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS sales."LinkoSyncRuns" (
                    "Id" uuid NOT NULL,
                    "Mode" character varying(16) NOT NULL,
                    "StartedAt" timestamp with time zone NOT NULL,
                    "FinishedAt" timestamp with time zone NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "Rows" integer NOT NULL,
                    "Entities" jsonb NULL,
                    "EarliestChanged" date NULL,
                    "Error" character varying(2000) NULL,
                    CONSTRAINT "PK_LinkoSyncRuns" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_LinkoSyncRuns_StartedAt" ON sales."LinkoSyncRuns" ("StartedAt");
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112193022_Analytik4_LinkoSyncRuns';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkoSyncRuns",
                schema: "sales");
        }
    }
}

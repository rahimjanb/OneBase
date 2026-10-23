using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelegramReportsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RegionId",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<long>(
                name: "AgentId",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentName",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DirectionId",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reports",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "sv");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoPurchaseDays",
                schema: "telegram",
                table: "ReportSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "RefreshBeforeSend",
                schema: "telegram",
                table: "ReportSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<int>(
                name: "DurationMs",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Report",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RunId",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeKey",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportJobs",
                schema: "telegram",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    CatchUpMinutes = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ArmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportJobs", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "ReportRuns",
                schema: "telegram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Job = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Planned = table.Column<int>(type: "integer", nullable: false),
                    Sent = table.Column<int>(type: "integer", nullable: false),
                    Failed = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    DataAsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDeliveries_RunId",
                schema: "telegram",
                table: "ReportDeliveries",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDeliveries_ScheduleKey",
                schema: "telegram",
                table: "ReportDeliveries",
                columns: new[] { "Report", "Period", "ChatId", "ScopeKey" },
                unique: true,
                filter: "\"Trigger\" = 'schedule' AND \"Report\" IS NOT NULL AND \"ChatId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRuns_Job_Period",
                schema: "telegram",
                table: "ReportRuns",
                columns: new[] { "Job", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportRuns_StartedAt",
                schema: "telegram",
                table: "ReportRuns",
                column: "StartedAt");

            // Данные прежних автоотчётов переносятся, ничего не удаляется. Расписания утро/обед/вечер → запуски «План на день» (daily.*):
            // время, дни, пояс, включение и момент включения сохраняются.
            migrationBuilder.Sql("""
                INSERT INTO telegram."ReportJobs" ("Code", "Enabled", "Time", "TimeZone", "Days", "CatchUpMinutes", "Version", "ArmedAt", "UpdatedAt", "UpdatedById")
                SELECT 'daily.' || lower("Kind"), "Enabled", "Time", "TimeZone", "Days", CASE "Kind" WHEN 'Evening' THEN 180 ELSE 120 END, 1, "ArmedAt", "UpdatedAt", "UpdatedById"
                FROM telegram."ReportSchedules"
                WHERE "Kind" IN ('Morning', 'Midday', 'Evening')
                ON CONFLICT ("Code") DO NOTHING;
                """);

            // Автоотправки «регион → супервайзер» → получатели роли СВ с теми же отчётами (утро/обед/вечер → daily.*).
            migrationBuilder.Sql("""
                UPDATE telegram."ReportSubscriptions"
                SET "Reports" = concat_ws(',', CASE WHEN "Morning" THEN 'daily.morning' END, CASE WHEN "Midday" THEN 'daily.midday' END,
                                               CASE WHEN "Evening" THEN 'daily.evening' END)
                WHERE "Reports" = '';
                """);

            // Незавершённые отправки прежнего вида не продолжаются: «в очереди» — пропущены, «отправляется» — неизвестно, дошла ли.
            migrationBuilder.Sql("""
                UPDATE telegram."ReportDeliveries"
                SET "Status" = CASE "Status" WHEN 'sending' THEN 'unknown' ELSE 'skipped' END,
                    "NextAttemptAt" = NULL,
                    "Error" = CASE "Status" WHEN 'sending'
                        THEN 'Неизвестно, дошёл ли отчёт: отправка прервана обновлением автоотчётов. Проверьте чат.'
                        ELSE 'Отменено: автоотчёты обновлены, отправка прежнего вида не продолжается.' END
                WHERE "Report" IS NULL AND "Status" IN ('pending', 'sending');
                """);

            // Журнал прежних отправок — в новом виде (отчёт daily.*, роль СВ, ключ области): виден в истории и защищает от повторной отправки
            // того же дня. Ключ, который уже занят (две автоотправки одной области в один чат), — уникальный «legacy:…».
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           'daily.' || lower("Kind") AS report,
                           'sv:' || replace("RegionId"::text, '-', '') || ':' || coalesce("SupervisorId"::text, '-') AS scope,
                           row_number() OVER (PARTITION BY lower("Kind"), "Period", "ChatId", "RegionId", "SupervisorId", "Trigger" = 'schedule' ORDER BY "CreatedAt") AS n
                    FROM telegram."ReportDeliveries"
                    WHERE "Report" IS NULL AND "Kind" IS NOT NULL
                )
                UPDATE telegram."ReportDeliveries" d
                SET "Report" = r.report,
                    "Role" = 'sv',
                    "ScopeKey" = CASE WHEN d."RegionId" IS NOT NULL AND (d."Trigger" <> 'schedule' OR r.n = 1) THEN r.scope
                                      ELSE 'legacy:' || replace(d."Id"::text, '-', '') END
                FROM ranked r
                WHERE d."Id" = r."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Назад: расписания daily.* → утро/обед/вечер; получатели РМ и ТП и отправки новых отчётов в прежней схеме не существуют —
            // удаляются; отчёты получателя СВ → флаги утро/обед/вечер.
            migrationBuilder.Sql("""
                UPDATE telegram."ReportSchedules" s
                SET "Enabled" = j."Enabled", "Time" = j."Time", "TimeZone" = j."TimeZone", "Days" = j."Days", "ArmedAt" = j."ArmedAt",
                    "UpdatedAt" = j."UpdatedAt", "UpdatedById" = j."UpdatedById"
                FROM telegram."ReportJobs" j
                WHERE j."Code" = 'daily.' || lower(s."Kind");
                """);
            migrationBuilder.Sql("""
                DELETE FROM telegram."ReportSubscriptions" WHERE "Role" <> 'sv' OR "RegionId" IS NULL;
                UPDATE telegram."ReportSubscriptions"
                SET "Morning" = (',' || "Reports" || ',') LIKE '%,daily.morning,%',
                    "Midday" = (',' || "Reports" || ',') LIKE '%,daily.midday,%',
                    "Evening" = (',' || "Reports" || ',') LIKE '%,daily.evening,%';
                """);
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           initcap(split_part("Report", '.', 2)) AS kind,
                           "Trigger" = 'schedule' AND "SubscriptionId" IS NOT NULL AND "ChatId" IS NOT NULL AS keyed,
                           row_number() OVER (PARTITION BY "SubscriptionId", "ChatId", "Report", "Period", "Trigger" = 'schedule' ORDER BY "CreatedAt") AS n
                    FROM telegram."ReportDeliveries"
                    WHERE "Kind" IS NULL AND "Report" IN ('daily.morning', 'daily.midday', 'daily.evening')
                )
                UPDATE telegram."ReportDeliveries" d
                SET "Kind" = r.kind
                FROM ranked r
                WHERE d."Id" = r."Id"
                  AND (NOT r.keyed OR (r.n = 1 AND NOT EXISTS (
                      SELECT 1 FROM telegram."ReportDeliveries" o
                      WHERE o."Kind" = r.kind AND o."SubscriptionId" = d."SubscriptionId" AND o."ChatId" = d."ChatId" AND o."Period" = d."Period"
                        AND o."Trigger" = 'schedule')));
                DELETE FROM telegram."ReportDeliveries" WHERE "Kind" IS NULL;
                """);
            migrationBuilder.DropTable(
                name: "ReportJobs",
                schema: "telegram");

            migrationBuilder.DropTable(
                name: "ReportRuns",
                schema: "telegram");

            migrationBuilder.DropIndex(
                name: "IX_ReportDeliveries_RunId",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_ReportDeliveries_ScheduleKey",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.DropColumn(
                name: "AgentId",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "AgentName",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "DirectionId",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "Reports",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "telegram",
                table: "ReportSubscriptions");

            migrationBuilder.DropColumn(
                name: "NoPurchaseDays",
                schema: "telegram",
                table: "ReportSettings");

            migrationBuilder.DropColumn(
                name: "RefreshBeforeSend",
                schema: "telegram",
                table: "ReportSettings");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.DropColumn(
                name: "Report",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.DropColumn(
                name: "RunId",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.DropColumn(
                name: "ScopeKey",
                schema: "telegram",
                table: "ReportDeliveries");

            migrationBuilder.AlterColumn<Guid>(
                name: "RegionId",
                schema: "telegram",
                table: "ReportSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "telegram",
                table: "ReportDeliveries",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }
    }
}

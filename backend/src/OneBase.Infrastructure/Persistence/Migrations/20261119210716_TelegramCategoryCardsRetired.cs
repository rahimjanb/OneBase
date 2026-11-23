using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «10. Карточки категорий» (месяц категории) убран: всё, что уходило им, теперь уходит отчётом «11. Итоги дня по категориям» (только день
    /// итога). Схема не меняется — переносятся данные: включённые запуски 10 включают тот же момент 11 (если он ещё не включён — с временем,
    /// днями и поясом запуска 10) и выключаются; коды category-cards.* в подписках получателей заменяются на category-day.* (без повторов, в
    /// том же порядке); не начатые отправки отчёта 10 — пропущены с причиной. Выбор категорий получателя общий — остаётся как был. Повторный
    /// запуск ничего не меняет (кодов category-cards.* в подписках и включённых запусков 10 больше нет).
    /// </summary>
    public partial class TelegramCategoryCardsRetired : Migration
    {
        /// <summary>Расписание: запуск 10 включён — момент 11 включается (не был включён — с временем, днями и поясом 10), запуски 10 выключаются.</summary>
        public const string Jobs = """
            INSERT INTO telegram."ReportJobs" ("Code", "Enabled", "Time", "TimeZone", "Days", "CatchUpMinutes", "Version", "ArmedAt", "UpdatedAt", "UpdatedById")
            SELECT replace(o."Code", 'category-cards.', 'category-day.'), TRUE, o."Time", o."TimeZone", o."Days", o."CatchUpMinutes", 1, now(), now(), NULL
            FROM telegram."ReportJobs" o
            WHERE o."Code" IN ('category-cards.morning', 'category-cards.evening') AND o."Enabled"
            ON CONFLICT ("Code") DO UPDATE
            SET "Enabled" = TRUE, "Time" = EXCLUDED."Time", "TimeZone" = EXCLUDED."TimeZone", "Days" = EXCLUDED."Days",
                "CatchUpMinutes" = EXCLUDED."CatchUpMinutes", "Version" = telegram."ReportJobs"."Version" + 1, "ArmedAt" = now(), "UpdatedAt" = now(),
                "UpdatedById" = NULL
            WHERE NOT telegram."ReportJobs"."Enabled";

            UPDATE telegram."ReportJobs"
            SET "Enabled" = FALSE, "Version" = "Version" + 1, "UpdatedAt" = now()
            WHERE "Code" IN ('category-cards.morning', 'category-cards.evening') AND "Enabled";
            """;

        /// <summary>Подписки: category-cards.morning → category-day.morning, category-cards.evening → category-day.evening; без повторов, порядок прежний.</summary>
        public const string Subscriptions = """
            UPDATE telegram."ReportSubscriptions" s
            SET "Reports" = m.reports
            FROM (
                SELECT x."Id", string_agg(x.code, ',' ORDER BY x.first) AS reports
                FROM (
                    SELECT r."Id",
                           CASE btrim(c.code)
                               WHEN 'category-cards.morning' THEN 'category-day.morning'
                               WHEN 'category-cards.evening' THEN 'category-day.evening'
                               ELSE btrim(c.code)
                           END AS code,
                           min(c.n) AS first
                    FROM telegram."ReportSubscriptions" r
                    CROSS JOIN LATERAL unnest(string_to_array(r."Reports", ',')) WITH ORDINALITY AS c(code, n)
                    WHERE r."Reports" LIKE '%category-cards.%' AND btrim(c.code) <> ''
                    GROUP BY 1, 2
                ) x
                GROUP BY x."Id"
            ) m
            WHERE s."Id" = m."Id";
            """;

        /// <summary>Не начатые отправки отчёта 10 (очередь, повтор) — пропущены с причиной: такого отчёта больше нет.</summary>
        public const string Deliveries = """
            UPDATE telegram."ReportDeliveries"
            SET "Status" = 'skipped', "NextAttemptAt" = NULL,
                "Error" = 'Отчёт 10 «Карточки категорий» убран: получатели переведены на отчёт 11 «Итоги дня по категориям».'
            WHERE "Report" IN ('category-cards.morning', 'category-cards.evening') AND "Status" = 'pending';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Jobs);
            migrationBuilder.Sql(Subscriptions);
            migrationBuilder.Sql(Deliveries);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Данные не возвращаются: отчёт 11 есть и в прежней версии, получатели продолжат получать его.
        }
    }
}

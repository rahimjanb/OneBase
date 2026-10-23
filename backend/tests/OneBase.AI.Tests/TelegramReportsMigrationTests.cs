using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OneBase.Domain.Identity;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Миграция TelegramReportsV2 на настоящем PostgreSQL: расписания утро/обед/вечер становятся запусками «План на день», автоотправки —
/// получателями СВ с теми же отчётами, журнал — в новом виде (незавершённые строки не продолжаются); откат возвращает прежнюю схему.
/// </summary>
public class TelegramReportsMigrationTests
{
    private const string Before = "20261022225330_SwitchAiToClaude";
    private const string Reports = "20261023184746_TelegramReportsV2";

    private static async Task<object?> ScalarAsync(string connection, string sql)
    {
        await using var c = new NpgsqlConnection(connection);
        await c.OpenAsync();
        return await new NpgsqlCommand(sql, c).ExecuteScalarAsync();
    }

    private static async Task ExecAsync(string connection, string sql)
    {
        await using var c = new NpgsqlConnection(connection);
        await c.OpenAsync();
        await new NpgsqlCommand(sql, c).ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task Daily_report_settings_move_to_jobs_and_recipients_and_rollback_restores_them()
    {
        var admin = Environment.GetEnvironmentVariable(DataManagerDb.Variable)!;
        var database = $"onebase_tgrep_{Guid.NewGuid():N}"[..28];
        await ExecAsync(admin, $"CREATE DATABASE \"{database}\"");
        var connection = new NpgsqlConnectionStringBuilder(admin) { Database = database }.ConnectionString;
        var options = new DbContextOptionsBuilder<OneBaseDbContext>().UseNpgsql(connection).Options;
        try
        {
            var region = Guid.NewGuid();
            var subscription = Guid.NewGuid();
            Guid chatId;
            await using (var db = new OneBaseDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync(Before);
                // Пользователь, аккаунт и чат — таблицы, которые миграция не меняет.
                var user = new User { Login = "tg-reports-migration", FullName = "Администратор" };
                db.Users.Add(user);
                var account = new TelegramAccount { Name = "Отчёты", CreatedById = user.Id };
                db.TelegramAccounts.Add(account);
                var chat = new TelegramChat { AccountId = account.Id, PeerId = -100_700, Type = TelegramChatType.Group, Title = "Хоразм — продажи" };
                db.TelegramChats.Add(chat);
                await db.SaveChangesAsync();
                chatId = chat.Id;

                // Прежние автоотчёты: полдень включён (Пн–Сб, 13:30), автоотправка СВ «утро + вечер», журнал: доставлено и «в очереди».
                await ExecAsync(connection, $"""
                    INSERT INTO telegram."ReportSettings" ("Id", "AccountId", "Language") VALUES (1, '{account.Id}', 'uz');
                    INSERT INTO telegram."ReportSchedules" ("Kind", "Enabled", "Time", "TimeZone", "Days", "Metric", "ArmedAt")
                    VALUES ('Midday', true, '13:30', 'Asia/Tashkent', 63, 'sum', '2026-10-20T08:00:00Z'),
                           ('Evening', false, '18:00', 'Asia/Tashkent', 63, 'sum', NULL);
                    INSERT INTO telegram."ReportSubscriptions" ("Id", "RegionId", "SupervisorId", "SupervisorName", "Morning", "Midday", "Evening", "Enabled", "CreatedById", "CreatedAt")
                    VALUES ('{subscription}', '{region}', 71, 'Азиз Каримов', true, false, true, true, '{user.Id}', now());
                    INSERT INTO telegram."ReportSubscriptionChats" ("SubscriptionId", "ChatId") VALUES ('{subscription}', '{chat.Id}');
                    INSERT INTO telegram."ReportDeliveries" ("Id", "Kind", "Period", "Trigger", "DataMode", "SubscriptionId", "AccountId", "ChatId", "PeerId",
                        "RecipientTitle", "ScopeTitle", "RegionId", "SupervisorId", "Status", "Attempts", "Pages", "Bytes", "CreatedAt")
                    VALUES ('{Guid.NewGuid()}', 'Morning', '2026-10-22', 'schedule', 'real', '{subscription}', '{account.Id}', '{chat.Id}', -100700,
                            'Хоразм — продажи', 'Хоразм · Азиз Каримов', '{region}', 71, 'sent', 1, 1, 1000, now()),
                           ('{Guid.NewGuid()}', 'Evening', '2026-10-22', 'schedule', 'real', '{subscription}', '{account.Id}', '{chat.Id}', -100700,
                            'Хоразм — продажи', 'Хоразм · Азиз Каримов', '{region}', 71, 'pending', 1, 0, 0, now());
                    """);

                await db.GetService<IMigrator>().MigrateAsync(Reports);
            }

            await using (var db = new OneBaseDbContext(options))
            {
                var job = await db.TelegramReportJobs.SingleAsync(j => j.Code == "daily.midday");
                Assert.Equal((true, new TimeOnly(13, 30), 63, 120), (job.Enabled, job.Time, job.Days, job.CatchUpMinutes));
                Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), job.ArmedAt);
                Assert.False((await db.TelegramReportJobs.SingleAsync(j => j.Code == "daily.evening")).Enabled);
                Assert.Equal(2, await db.TelegramReportJobs.CountAsync()); // остальные запуски — по умолчанию (выключены), строк нет

                var settings = await db.TelegramReportSettings.SingleAsync();
                Assert.Equal(("uz", 30, true), (settings.Language, settings.NoPurchaseDays, settings.RefreshBeforeSend));

                var recipient = await db.TelegramReportSubscriptions.Include(s => s.Chats).SingleAsync();
                Assert.Equal(("sv", "daily.morning,daily.evening", region, 71L), (recipient.Role, recipient.Reports, recipient.RegionId!.Value, recipient.SupervisorId!.Value));
                Assert.Equal(chatId, Assert.Single(recipient.Chats).ChatId);

                var deliveries = await db.TelegramReportDeliveries.OrderBy(d => d.Kind).ToListAsync();
                var key = $"sv:{region:N}:71";
                Assert.Equal([("daily.evening", "skipped", key), ("daily.morning", "sent", key)], deliveries.Select(d => (d.Report!, d.Status, d.ScopeKey!)));
                Assert.All(deliveries, d => Assert.Equal("sv", d.Role));

                // Новое после обновления: РМ и отправка нового отчёта — в прежней схеме их нет, откат их убирает.
                job.Time = new TimeOnly(13, 15);
                db.TelegramReportSubscriptions.Add(new TelegramReportSubscription { Role = "rm", Reports = "daily.evening", CreatedById = recipient.CreatedById });
                db.TelegramReportDeliveries.Add(new TelegramReportDelivery
                {
                    Report = "no-orders.midday", Period = new DateOnly(2026, 10, 23), Trigger = "schedule", DataMode = "real", SubscriptionId = recipient.Id, Role = "sv",
                    ScopeKey = key, AccountId = settings.AccountId!.Value, ChatId = chatId, RecipientTitle = "Хоразм — продажи", ScopeTitle = "Хоразм", Status = "sent",
                });
                await db.SaveChangesAsync();

                await db.GetService<IMigrator>().MigrateAsync(Before);
            }

            Assert.Equal("13:15:00", await ScalarAsync(connection, """SELECT "Time"::text FROM telegram."ReportSchedules" WHERE "Kind" = 'Midday'"""));
            Assert.Equal(1L, await ScalarAsync(connection, """SELECT count(*) FROM telegram."ReportSubscriptions" """));
            Assert.Equal("t,f,t", await ScalarAsync(connection,
                """SELECT concat_ws(',', "Morning"::text, "Midday"::text, "Evening"::text) FROM telegram."ReportSubscriptions" """) is string flags
                ? string.Join(',', flags.Split(',').Select(f => f[..1]))
                : null);
            Assert.Equal("Evening,Morning", await ScalarAsync(connection, """SELECT string_agg("Kind", ',' ORDER BY "Kind") FROM telegram."ReportDeliveries" """));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecAsync(admin, $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
        }
    }
}

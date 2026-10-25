using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Миграция SwitchAiToClaude на настоящем PostgreSQL: настройки OpenAI переходят на Claude, откат возвращает прежние.</summary>
public class ClaudeSwitchMigrationTests
{
    private const string Before = "20261022222310_TelegramDailyReports";
    private const string Switch = "20261022225330_SwitchAiToClaude";

    [PostgresFact]
    public async Task OpenAi_models_move_to_claude_and_rollback_restores_them()
    {
        var admin = Environment.GetEnvironmentVariable(DataManagerDb.Variable)!;
        var database = $"onebase_claude_{Guid.NewGuid():N}"[..28];
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection).ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<OneBaseDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder(admin) { Database = database }.ConnectionString)
            .Options;
        try
        {
            Guid accountId;
            await using (var db = new OneBaseDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync(Before);
                // Настройки и модель — SQL по схеме той миграции: у сущностей AiSettings и AiModel уже есть колонки (бюджет, цены кэша),
                // которых в базе на этом шаге ещё нет.
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO ai."Settings" ("Id", "Enabled", "PrimaryProvider", "PrimaryModel", "RouterProvider", "RouterModel", "EmbeddingProvider",
                        "EmbeddingModel", "MaxOutputTokens", "ReasoningEffort")
                    VALUES (1, TRUE, 'openai', 'gpt-5', 'openai', 'gpt-5-mini', 'openai', 'text-embedding-3-small', 4096, 'low')
                    """);
                db.AiAgents.AddRange(
                    new AiAgent { Code = "sales", Name = "Продажи", ProviderCode = "openai", ModelId = "gpt-5-mini" },
                    new AiAgent { Code = "finance", Name = "Финансы", ProviderCode = "anthropic", ModelId = "claude-sonnet-4-6" });
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO ai."Models" ("Id", "ProviderCode", "ModelId", "Available", "Enabled", "InputPricePerMillion", "OutputPricePerMillion", "CreatedAt")
                    VALUES ({0}, 'anthropic', 'claude-opus-5-5', TRUE, FALSE, 5, 25, {1})
                    """, Guid.CreateVersion7(), DateTimeOffset.UtcNow);
                var user = new User { Login = "claude-switch", FullName = "Администратор" };
                db.Users.Add(user);
                var account = new TelegramAccount { Name = "Отдел продаж", CreatedById = user.Id };
                db.TelegramAccounts.Add(account);
                db.TelegramAgentSettings.Add(new TelegramAgentSettings
                {
                    AccountId = account.Id, ModelProvider = "openai", Model = "gpt-5", GenerationModelProvider = "openai", GenerationModel = "gpt-5",
                });
                await db.SaveChangesAsync();
                accountId = account.Id;

                await db.GetService<IMigrator>().MigrateAsync(Switch);
            }

            await using (var db = new OneBaseDbContext(options))
            {
                var s = await SettingsAsync(db);
                Assert.Equal(("anthropic", "claude-opus-5-5"), (s.PrimaryProvider, s.PrimaryModel));
                Assert.Equal(("anthropic", "claude-sonnet-5-5"), (s.FallbackProvider, s.FallbackModel));
                Assert.Equal(("anthropic", "claude-haiku-5-5"), (s.RouterProvider, s.RouterModel));
                Assert.Equal(("openai", "text-embedding-3-small"), (s.EmbeddingProvider, s.EmbeddingModel)); // эмбеддингов у Anthropic нет
                Assert.Equal(16000, s.MaxOutputTokens);

                var agents = await db.AiAgents.OrderBy(a => a.Code).ToListAsync();
                Assert.Equal([("finance", "anthropic", "claude-sonnet-4-6"), ("sales", null, null)], agents.Select(a => (a.Code, a.ProviderCode, a.ModelId)));

                var telegram = await db.TelegramAgentSettings.SingleAsync(t => t.AccountId == accountId);
                Assert.Equal((null, null, null, null), (telegram.ModelProvider, telegram.Model, telegram.GenerationModelProvider, telegram.GenerationModel));

                var models = await ModelsAsync(db);
                Assert.Equal(["claude-haiku-5-5", "claude-opus-5-5", "claude-sonnet-5-5"], models.Select(m => m.ModelId));
                Assert.All(models, m => Assert.True(m.Enabled && m.Available));
                Assert.Equal((5m, 25m), (models[1].InputPricePerMillion, models[1].OutputPricePerMillion)); // заданные цены не трогаются

                await db.GetService<IMigrator>().MigrateAsync(Before);
            }

            await using (var db = new OneBaseDbContext(options))
            {
                var s = await SettingsAsync(db);
                Assert.Equal(("openai", "gpt-5", null, null), (s.PrimaryProvider, s.PrimaryModel, s.FallbackProvider, s.FallbackModel));
                Assert.Equal(("openai", "gpt-5-mini", 4096), (s.RouterProvider, s.RouterModel, s.MaxOutputTokens));
                var sales = await db.AiAgents.SingleAsync(a => a.Code == "sales");
                Assert.Equal(("openai", "gpt-5-mini"), (sales.ProviderCode, sales.ModelId));
                var telegram = await db.TelegramAgentSettings.SingleAsync(t => t.AccountId == accountId);
                Assert.Equal(("openai", "gpt-5", "openai", "gpt-5"), (telegram.ModelProvider, telegram.Model, telegram.GenerationModelProvider, telegram.GenerationModel));
                var model = Assert.Single(await ModelsAsync(db));
                Assert.Equal(("claude-opus-5-5", false), (model.ModelId, model.Enabled));
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }

    private sealed class ModelRow
    {
        public string ModelId { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool Available { get; set; }
        public decimal? InputPricePerMillion { get; set; }
        public decimal? OutputPricePerMillion { get; set; }
    }

    private sealed class SettingsRow
    {
        public string? PrimaryProvider { get; set; }
        public string? PrimaryModel { get; set; }
        public string? FallbackProvider { get; set; }
        public string? FallbackModel { get; set; }
        public string? RouterProvider { get; set; }
        public string? RouterModel { get; set; }
        public string? EmbeddingProvider { get; set; }
        public string? EmbeddingModel { get; set; }
        public int MaxOutputTokens { get; set; }
    }

    /// <summary>Настройки AI по колонкам старой схемы: сущность AiSettings знает бюджет, который появляется лишь в поздней миграции.</summary>
    private static async Task<SettingsRow> SettingsAsync(OneBaseDbContext db) =>
        Assert.Single(await db.Database.SqlQueryRaw<SettingsRow>("""
            SELECT "PrimaryProvider", "PrimaryModel", "FallbackProvider", "FallbackModel", "RouterProvider", "RouterModel", "EmbeddingProvider",
                   "EmbeddingModel", "MaxOutputTokens"
            FROM ai."Settings"
            """).ToListAsync());

    /// <summary>Модели Anthropic по колонкам старой схемы: сущность AiModel знает цены кэша, которые появляются лишь в поздней миграции.</summary>
    private static Task<List<ModelRow>> ModelsAsync(OneBaseDbContext db) =>
        db.Database.SqlQueryRaw<ModelRow>("""
            SELECT "ModelId", "Enabled", "Available", "InputPricePerMillion", "OutputPricePerMillion"
            FROM ai."Models" WHERE "ProviderCode" = 'anthropic' ORDER BY "ModelId"
            """).ToListAsync();

    [PostgresFact]
    public async Task Fresh_database_starts_on_claude()
    {
        var admin = Environment.GetEnvironmentVariable(DataManagerDb.Variable)!;
        var database = $"onebase_claude_{Guid.NewGuid():N}"[..28];
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection).ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<OneBaseDbContext>()
                .UseNpgsql(new NpgsqlConnectionStringBuilder(admin) { Database = database }.ConnectionString)
                .Options;
            await using var db = new OneBaseDbContext(options);
            await db.Database.MigrateAsync();

            var s = await db.AiSettings.SingleAsync();
            Assert.Equal(("anthropic", "claude-opus-5-5", "anthropic", "claude-sonnet-5-5"), (s.PrimaryProvider, s.PrimaryModel, s.FallbackProvider, s.FallbackModel));
            Assert.Null(s.RouterProvider); // маршрутизация — основной моделью
            Assert.Equal((16000, AiReasoning.Low, true), (s.MaxOutputTokens, s.ReasoningEffort, s.Enabled));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }
}

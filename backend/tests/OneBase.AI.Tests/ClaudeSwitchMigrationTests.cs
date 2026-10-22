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
                db.AiSettings.Add(new AiSettings
                {
                    PrimaryProvider = "openai", PrimaryModel = "gpt-5", RouterProvider = "openai", RouterModel = "gpt-5-mini",
                    EmbeddingProvider = "openai", EmbeddingModel = "text-embedding-3-small", MaxOutputTokens = 4096,
                });
                db.AiAgents.AddRange(
                    new AiAgent { Code = "sales", Name = "Продажи", ProviderCode = "openai", ModelId = "gpt-5-mini" },
                    new AiAgent { Code = "finance", Name = "Финансы", ProviderCode = "anthropic", ModelId = "claude-sonnet-4-6" });
                db.AiModels.Add(new AiModel { ProviderCode = "anthropic", ModelId = "claude-opus-5-5", Enabled = false, InputPricePerMillion = 5, OutputPricePerMillion = 25 });
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
                var s = await db.AiSettings.SingleAsync();
                Assert.Equal(("anthropic", "claude-opus-5-5"), (s.PrimaryProvider, s.PrimaryModel));
                Assert.Equal(("anthropic", "claude-sonnet-5-5"), (s.FallbackProvider, s.FallbackModel));
                Assert.Equal(("anthropic", "claude-haiku-5-5"), (s.RouterProvider, s.RouterModel));
                Assert.Equal(("openai", "text-embedding-3-small"), (s.EmbeddingProvider, s.EmbeddingModel)); // эмбеддингов у Anthropic нет
                Assert.Equal(16000, s.MaxOutputTokens);

                var agents = await db.AiAgents.OrderBy(a => a.Code).ToListAsync();
                Assert.Equal([("finance", "anthropic", "claude-sonnet-4-6"), ("sales", null, null)], agents.Select(a => (a.Code, a.ProviderCode, a.ModelId)));

                var telegram = await db.TelegramAgentSettings.SingleAsync(t => t.AccountId == accountId);
                Assert.Equal((null, null, null, null), (telegram.ModelProvider, telegram.Model, telegram.GenerationModelProvider, telegram.GenerationModel));

                var models = await db.AiModels.Where(m => m.ProviderCode == "anthropic").OrderBy(m => m.ModelId).ToListAsync();
                Assert.Equal(["claude-haiku-5-5", "claude-opus-5-5", "claude-sonnet-5-5"], models.Select(m => m.ModelId));
                Assert.All(models, m => Assert.True(m.Enabled && m.Available));
                Assert.Equal((5m, 25m), (models[1].InputPricePerMillion, models[1].OutputPricePerMillion)); // заданные цены не трогаются

                await db.GetService<IMigrator>().MigrateAsync(Before);
            }

            await using (var db = new OneBaseDbContext(options))
            {
                var s = await db.AiSettings.SingleAsync();
                Assert.Equal(("openai", "gpt-5", null, null), (s.PrimaryProvider, s.PrimaryModel, s.FallbackProvider, s.FallbackModel));
                Assert.Equal(("openai", "gpt-5-mini", 4096), (s.RouterProvider, s.RouterModel, s.MaxOutputTokens));
                var sales = await db.AiAgents.SingleAsync(a => a.Code == "sales");
                Assert.Equal(("openai", "gpt-5-mini"), (sales.ProviderCode, sales.ModelId));
                var telegram = await db.TelegramAgentSettings.SingleAsync(t => t.AccountId == accountId);
                Assert.Equal(("openai", "gpt-5", "openai", "gpt-5"), (telegram.ModelProvider, telegram.Model, telegram.GenerationModelProvider, telegram.GenerationModel));
                var model = await db.AiModels.SingleAsync(m => m.ProviderCode == "anthropic");
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

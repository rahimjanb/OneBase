using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OneBase.AI.Gateway;
using OneBase.Api.Controllers;
using OneBase.Application.Abstractions;
using OneBase.Application.Recovery;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Integrations;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Push;
using OneBase.Infrastructure.Recovery;
using OneBase.Infrastructure.Telegram;

namespace OneBase.AI.Tests;

/// <summary>
/// Проверка восстановленной копии (GET /api/recovery/check) на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается), в режиме
/// изоляции — как на учебном восстановлении. Секреты сохраняются теми же сервисами, что в работе (настройки AI, Linko, Telegram, ключи
/// push), — проверка обязана их расшифровать; испорченный секрет и пропавший файл обязаны попасть в проблемы; значения секретов — нигде.
/// </summary>
public class RecoveryCheckDbTests : IAsyncLifetime
{
    private const string CheckToken = "drill-check-token-0123456789abcdef";
    private static readonly string[] SecretValues = ["sk-ant-drill-key-0001", "linko-drill-token-0002", "linko-drill-plan-0003", "fedcba9876543210fedcba9876543210",
        "verifix-drill-secret-0004", "{\"private_key\":\"drill-google-0005\"}", "+998900000006"];

    private string? _admin;
    private string? _database;
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable(DataManagerDb.Variable);
        if (string.IsNullOrWhiteSpace(_admin))
        {
            return;
        }

        _database = $"onebase_dr_test_{Guid.NewGuid():N}"[..28];
        await using (var connection = new NpgsqlConnection(_admin))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", connection).ExecuteNonQueryAsync();
        }

        var config = RecoveryIsolationTests.Config(isolated: true,
            ("ConnectionStrings:Postgres", new NpgsqlConnectionStringBuilder(_admin) { Database = _database }.ConnectionString),
            (RecoveryController.TokenKey, CheckToken));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddInfrastructure(config);
        services.AddAi(config);
        services.AddSingleton<IFileStorage, MemoryStorage>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OneBaseDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is null)
        {
            return;
        }

        await _services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
    }

    /// <summary>Как в работе: секреты — через сервисы настроек; то, что сохраняется сложнее (вход в Telegram, Google), — тем же назначением ключа.</summary>
    private async Task SeedAsync()
    {
        var sp = _services;
        var protection = sp.GetRequiredService<IDataProtectionProvider>();
        Guid userId;
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var user = new User { Login = "dr-admin", FullName = "Учебный администратор" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        await sp.GetRequiredService<AiSettingsStore>().SaveProviderAsync("anthropic", null, SecretValues[0], true, userId);
        await sp.GetRequiredService<LinkoSettingsStore>().SaveAsync("https://linko.example", SecretValues[1], SecretValues[2], true, userId);
        await sp.GetRequiredService<TelegramCredentialsStore>().SaveAsync(12345, SecretValues[3], userId);
        Assert.NotNull(await sp.GetRequiredService<PushKeyProvider>().GetAsync(CancellationToken.None));

        var storage = sp.GetRequiredService<IFileStorage>();
        await storage.PutAsync("knowledge/kept.pdf", new MemoryStream(Encoding.UTF8.GetBytes("kept")), 4, "application/pdf");

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.Integrations.Add(new IntegrationConnection
            {
                Code = "verifix", DepartmentCode = "hr",
                ProtectedSecret = protection.CreateProtector(ProtectionPurposes.VerifixClientSecret).Protect(SecretValues[4]),
            });
            db.DataSourceConnections.Add(new DataSourceConnection
            {
                Name = "Учебная таблица", SpreadsheetId = "drill", Url = "https://docs.google.com/spreadsheets/d/drill", Access = SheetAccess.ServiceAccount,
                ProtectedKey = protection.CreateProtector(ProtectionPurposes.GoogleServiceAccount).Protect(SecretValues[5]),
            });
            var account = new TelegramAccount
            {
                Name = "Учебный аккаунт", CreatedById = userId,
                ProtectedSession = Convert.ToBase64String(protection.CreateProtector(ProtectionPurposes.TelegramSession).Protect([1, 2, 3, 4])),
                // Испорчено: так выглядит секрет, зашифрованный ключом, которого в копии нет.
                ProtectedPhone = "CfDJ8-drill-not-decryptable",
            };
            db.TelegramAccounts.Add(account);
            db.AiKnowledgeDocuments.AddRange(
                Document("Есть в хранилище", "knowledge/kept.pdf", userId),
                Document("Пропал из хранилища", "knowledge/lost.pdf", userId));
            await db.SaveChangesAsync();
        }
    }

    private static AiKnowledgeDocument Document(string title, string key, Guid userId) => new()
    {
        Title = title, FileName = title + ".pdf", ContentType = "application/pdf", ObjectKey = key, Sha256 = new string('0', 64), UploadedById = userId,
    };

    [PostgresFact]
    public async Task Check_decrypts_every_secret_kind_and_finds_missing_files_without_revealing_values()
    {
        await SeedAsync();
        using var scope = _services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<RecoveryCheckService>().RunAsync();

        // Копия в изоляции: внешние службы выключены, Telegram не подключается.
        Assert.True(result.Isolated);
        Assert.False(result.TelegramEnabled);
        Assert.True(result.DisabledWorkers.Count >= 13);

        Assert.True(result.Database.AppliedMigrations > 0);
        Assert.Empty(result.Database.PendingMigrations);
        Assert.Equal(1, result.Counts["users"]);
        Assert.Equal(2, result.Counts["knowledgeDocuments"]);
        Assert.Equal(1, result.Counts["telegramAccounts"]);
        Assert.Null(result.DataProtection.Error);
        Assert.True(result.DataProtection.Stored >= 1);
        Assert.True(result.DataProtection.Active >= 1);

        var secrets = result.Secrets.ToDictionary(s => s.Type);
        (int Total, int Ok) Of(string type) => (secrets[type].Total, secrets[type].Ok);
        Assert.Equal((1, 1), Of("ai"));
        Assert.Equal((2, 2), Of("linko"));
        Assert.Equal((1, 1), Of("verifix"));
        Assert.Equal((1, 1), Of("telegram-api"));
        Assert.Equal((1, 1), Of("telegram-session"));
        Assert.Equal((1, 0), Of("telegram-phone"));
        Assert.Equal((1, 1), Of("google"));
        Assert.Equal((1, 1), Of("vapid"));
        Assert.Equal((0, 0), Of("file-passwords"));

        var knowledge = result.Files.Sources.Single(s => s.Source == "knowledge");
        Assert.Equal(2, knowledge.References);
        Assert.Equal(1, knowledge.Missing);
        Assert.Equal(["knowledge/lost.pdf"], knowledge.MissingSamples);
        Assert.Equal(1, result.Files.Objects);
        Assert.Null(result.Files.StorageError);
        Assert.True(result.Files.ReadOk);

        // Ровно две проблемы — испорченный номер Telegram и пропавший файл; Success для такой копии быть не может.
        Assert.False(result.Ok);
        Assert.Equal(2, result.Problems.Count);
        Assert.Contains("Telegram: номера аккаунтов: не расшифровано 1 из 1", result.Problems);
        Assert.Contains("База знаний AI: нет в хранилище 1 из 2", result.Problems);

        // Значения секретов не попадают в ответ ни в каком виде.
        var json = JsonSerializer.Serialize(result);
        Assert.All(SecretValues, value => Assert.DoesNotContain(value, json));
    }

    [PostgresFact]
    public async Task Check_endpoint_answers_restore_engine_by_token()
    {
        await SeedAsync();
        using var scope = _services.CreateScope();
        var controller = new RecoveryController(_services.GetRequiredService<RecoveryIsolation>(), _services.GetRequiredService<IConfiguration>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.HttpContext.Request.Headers[RecoveryController.TokenHeader] = CheckToken;

        var ok = Assert.IsType<OkObjectResult>(await controller.Check(scope.ServiceProvider.GetRequiredService<RecoveryCheckService>(),
            _services.GetRequiredService<AiSafetyOptions>(), CancellationToken.None));
        var json = JsonSerializer.Serialize(ok.Value, JsonSerializerOptions.Web);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.True(root.GetProperty("mode").GetProperty("isolated").GetBoolean());
        Assert.True(root.GetProperty("mode").GetProperty("aiPaidCallsBlocked").GetBoolean());
        Assert.False(root.GetProperty("mode").GetProperty("telegramEnabled").GetBoolean());
        Assert.Equal(2, root.GetProperty("problems").GetArrayLength());
        Assert.All(SecretValues, value => Assert.DoesNotContain(value, json));
    }
}

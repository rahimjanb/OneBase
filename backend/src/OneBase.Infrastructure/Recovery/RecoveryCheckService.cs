using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Recovery;
using OneBase.Infrastructure.Hr;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.Infrastructure.Recovery;

public sealed record RecoveryDatabaseCheck(int AppliedMigrations, string? LastMigration, IReadOnlyList<string> PendingMigrations);

/// <summary>Ключи Data Protection: строк в базе, действующих сейчас (не отозван, срок не истёк), ошибка чтения ключей.</summary>
public sealed record RecoveryKeysCheck(int Stored, int Active, string? Error);

/// <summary>Вид секрета: сколько сохранено, сколько расшифровалось. Значения не выводятся и не хранятся.</summary>
public sealed record RecoverySecretCheck(string Type, string Label, int Total, int Ok, int Failed);

/// <summary>Ссылки на файлы из одной таблицы: сколько разных ключей, скольких объектов нет в хранилище (и первые из них).</summary>
public sealed record RecoveryFileSource(string Source, string Label, int References, int Missing, IReadOnlyList<string> MissingSamples);

/// <summary>Хранилище: объектов всего (null — не прочитано), ошибка, ссылки по таблицам, пробное чтение одного файла (null — читать нечего).</summary>
public sealed record RecoveryFilesCheck(int? Objects, string? StorageError, IReadOnlyList<RecoveryFileSource> Sources, bool? ReadOk, string? ReadError);

public sealed record RecoveryCheckResult(
    DateTimeOffset CheckedAt,
    long ElapsedMs,
    bool Isolated,
    IReadOnlyList<IsolatedWorker> DisabledWorkers,
    bool TelegramEnabled,
    RecoveryDatabaseCheck Database,
    IReadOnlyDictionary<string, long> Counts,
    RecoveryKeysCheck DataProtection,
    IReadOnlyList<RecoverySecretCheck> Secrets,
    RecoveryFilesCheck Files,
    IReadOnlyList<string> Problems)
{
    public bool Ok => Problems.Count == 0;
}

/// <summary>
/// Проверка восстановленной копии изнутри приложения — для Health Check движка восстановления (и позже «Настройки → Резервные копии»):
/// видит ли приложение восстановленную базу (миграции, ключевые счётчики), есть ли ключи Data Protection и расшифровывается ли каждый вид
/// сохранённого секрета — это доказательство, что интеграции восстановимы; у каждой ссылки на файл в базе есть объект в хранилище, и файл
/// читается. Данные не меняются, наружу ничего не уходит, значения секретов не выводятся.
/// </summary>
public sealed class RecoveryCheckService(
    OneBaseDbContext db,
    IDataProtectionProvider protection,
    IKeyManager keys,
    IFileStorage storage,
    RecoveryIsolation isolation,
    IConfiguration config,
    ILogger<RecoveryCheckService> logger)
{
    /// <summary>Сколько недостающих ключей показывать по каждой таблице.</summary>
    public const int MissingSamples = 5;

    public async Task<RecoveryCheckResult> RunAsync(CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        var problems = new List<string>();

        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        var database = new RecoveryDatabaseCheck(applied.Count, applied.LastOrDefault(), pending);
        if (pending.Count > 0)
        {
            problems.Add($"Не применены миграции базы: {pending.Count}");
        }

        var counts = await CountsAsync(ct);
        var dataProtection = await KeysAsync(ct);
        var secrets = await SecretsAsync(ct);
        if (dataProtection.Error is { } keysError)
        {
            problems.Add($"Ключи Data Protection не читаются: {keysError}");
        }
        else if (dataProtection.Stored == 0 && secrets.Any(s => s.Total > 0))
        {
            problems.Add("В базе нет ключей Data Protection, а зашифрованные секреты есть — их не расшифровать");
        }

        problems.AddRange(secrets.Where(s => s.Failed > 0).Select(s => $"{s.Label}: не расшифровано {s.Failed} из {s.Total}"));

        var files = await FilesAsync(ct);
        if (files.StorageError is { } storageError)
        {
            problems.Add($"Хранилище файлов недоступно: {storageError}");
        }

        problems.AddRange(files.Sources.Where(s => s.Missing > 0).Select(s => $"{s.Label}: нет в хранилище {s.Missing} из {s.References}"));
        if (files.ReadOk == false)
        {
            problems.Add($"Файл не читается из хранилища: {files.ReadError}");
        }

        return new RecoveryCheckResult(DateTimeOffset.UtcNow, watch.ElapsedMilliseconds, isolation.Isolated, isolation.DisabledWorkers,
            WTelegramGateway.Enabled(config), database, counts, dataProtection, secrets, files, problems);
    }

    /// <summary>Ключевые числа для отчёта: что приложение видит ту же базу, что в копии (полная сверка всех таблиц — у движка восстановления).</summary>
    private async Task<IReadOnlyDictionary<string, long>> CountsAsync(CancellationToken ct) => new Dictionary<string, long>
    {
        ["users"] = await db.Users.LongCountAsync(ct),
        ["roles"] = await db.Roles.LongCountAsync(ct),
        ["sessions"] = await db.UserSessions.LongCountAsync(ct),
        ["departments"] = await db.Departments.LongCountAsync(ct),
        ["employees"] = await db.HrEmployees.LongCountAsync(ct),
        ["auditLog"] = await db.AuditLogs.LongCountAsync(ct),
        ["files"] = await db.Files.LongCountAsync(ct),
        ["workTasks"] = await db.WorkTasks.LongCountAsync(ct),
        ["knowledgeDocuments"] = await db.AiKnowledgeDocuments.LongCountAsync(ct),
        ["telegramAccounts"] = await db.TelegramAccounts.LongCountAsync(ct),
    };

    private async Task<RecoveryKeysCheck> KeysAsync(CancellationToken ct)
    {
        var stored = await db.DataProtectionKeys.CountAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var active = keys.GetAllKeys().Count(k => !k.IsRevoked && k.ActivationDate <= now && k.ExpirationDate > now);
            return new RecoveryKeysCheck(stored, active, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Проверка восстановления: ключи Data Protection не читаются");
            return new RecoveryKeysCheck(stored, 0, ex.Message);
        }
    }

    private async Task<IReadOnlyList<RecoverySecretCheck>> SecretsAsync(CancellationToken ct)
    {
        var integrations = await db.Integrations.AsNoTracking().Select(i => new { i.Code, i.ProtectedSecret, i.ProtectedPlanSecret }).ToListAsync(ct);
        var providers = await db.AiProviders.AsNoTracking().Select(p => new { p.Code, p.ProtectedApiKey }).ToListAsync(ct);
        var accounts = await db.TelegramAccounts.AsNoTracking().Select(a => new { a.ProtectedSession, a.ProtectedPhone }).ToListAsync(ct);
        var google = await db.DataSourceConnections.AsNoTracking().Select(c => c.ProtectedKey).ToListAsync(ct);
        var connections = await db.FileConnections.AsNoTracking().Select(c => new { c.ProtectedPassword, c.ProtectedLinkToken }).ToListAsync(ct);
        var userLinks = await db.FileUserLinks.AsNoTracking().Select(l => l.ProtectedLinkToken).ToListAsync(ct);
        var vapid = await db.PushKeys.AsNoTracking().Select(k => k.ProtectedPrivateKey).ToListAsync(ct);
        var hh = await db.HhConnections.AsNoTracking().Select(c => new { c.ProtectedClientSecret, c.ProtectedAccessToken, c.ProtectedRefreshToken }).ToListAsync(ct);

        IEnumerable<(string, string?)> Integration(string code, string purpose, bool plan = false) =>
            integrations.Where(i => i.Code == code).Select(i => (purpose, plan ? i.ProtectedPlanSecret : i.ProtectedSecret));

        IEnumerable<(string, string?)> All(string purpose, IEnumerable<string?> values) => values.Select(v => (purpose, v));

        return
        [
            Check("ai", "Ключи моделей AI", providers.Select(p => (ProtectionPurposes.AiProviderKey(p.Code), p.ProtectedApiKey))),
            Check("linko", "Linko: токены API", Integration(LinkoSettingsStore.Code, ProtectionPurposes.LinkoToken)
                .Concat(Integration(LinkoSettingsStore.Code, ProtectionPurposes.LinkoPlanToken, plan: true))),
            Check("verifix", "Verifix: client_secret", Integration(VerifixSettingsStore.Code, ProtectionPurposes.VerifixClientSecret)),
            Check("telegram-api", "Telegram: api_hash", Integration(TelegramCredentialsStore.Code, ProtectionPurposes.TelegramApiHash)),
            Check("telegram-session", "Telegram: сессии аккаунтов", All(ProtectionPurposes.TelegramSession, accounts.Select(a => a.ProtectedSession)), binary: true),
            Check("telegram-phone", "Telegram: номера аккаунтов", All(ProtectionPurposes.TelegramPhone, accounts.Select(a => a.ProtectedPhone))),
            Check("google", "Google: ключи сервисных аккаунтов", All(ProtectionPurposes.GoogleServiceAccount, google)),
            Check("file-passwords", "Папки отделов: пароли подключений", All(ProtectionPurposes.FileConnectionPassword, connections.Select(c => (string?)c.ProtectedPassword))),
            Check("file-links", "Папки отделов: ссылки подключения",
                All(ProtectionPurposes.FileConnectionLink, connections.Select(c => c.ProtectedLinkToken).Concat(userLinks))),
            Check("vapid", "Push: закрытый ключ VAPID", All(ProtectionPurposes.PushVapid, vapid)),
            Check("hh", "HH.uz: client_secret и токены", All(ProtectionPurposes.HhClientSecret, hh.Select(h => h.ProtectedClientSecret))
                .Concat(All(ProtectionPurposes.HhTokens, hh.SelectMany(h => new[] { h.ProtectedAccessToken, h.ProtectedRefreshToken })))),
        ];
    }

    /// <summary>Расшифровать каждое значение тем же назначением, что при сохранении; результат сразу отбрасывается.</summary>
    private RecoverySecretCheck Check(string type, string label, IEnumerable<(string Purpose, string? Value)> values, bool binary = false)
    {
        int total = 0, ok = 0;
        foreach (var (purpose, value) in values)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            total++;
            try
            {
                var protector = protection.CreateProtector(purpose);
                if (binary)
                {
                    protector.Unprotect(Convert.FromBase64String(value));
                }
                else
                {
                    protector.Unprotect(value);
                }

                ok++;
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // Не расшифровалось — считаем; ни значение, ни текст ошибки (в нём бывает фрагмент данных) не выводятся.
            }
        }

        return new RecoverySecretCheck(type, label, total, ok, total - ok);
    }

    private async Task<RecoveryFilesCheck> FilesAsync(CancellationToken ct)
    {
        var sources = new List<(string Source, string Label, List<string> Keys)>
        {
            ("files", "Файлы отделов", await db.FileVersions.AsNoTracking().Select(v => v.ObjectKey).ToListAsync(ct)),
            ("tasks", "Вложения задач", await db.WorkTaskAttachments.AsNoTracking().Select(a => a.ObjectKey).ToListAsync(ct)),
            ("procurement", "Документы снабжения", await db.ProcurementDocuments.AsNoTracking().Select(d => d.ObjectKey).ToListAsync(ct)),
            ("knowledge", "База знаний AI", await db.AiKnowledgeDocuments.AsNoTracking().Select(d => d.ObjectKey).ToListAsync(ct)),
            ("data-manager", "AI Data Manager: загруженные файлы", await db.DataFiles.AsNoTracking().Select(f => f.ObjectKey).ToListAsync(ct)),
            ("telegram-materials", "Telegram: материалы агента",
                await db.TelegramMaterials.AsNoTracking().Where(m => m.ObjectKey != null).Select(m => m.ObjectKey!).ToListAsync(ct)),
            ("telegram-campaigns", "Telegram: вложения рассылок", await db.TelegramCampaignAttachments.AsNoTracking().Select(a => a.ObjectKey).ToListAsync(ct)),
            ("field-photos", "Sales Base: фото визитов", await db.FieldVisits.AsNoTracking().Where(v => v.PhotoKey != null).Select(v => v.PhotoKey!).ToListAsync(ct)),
            ("recruitment-resumes", "Подбор: резюме кандидатов", await db.CandidateResumes.AsNoTracking().Where(r => r.Sha256 != null).Select(r => r.ObjectKey).ToListAsync(ct)),
            ("weekly-pdf", "Telegram: еженедельные PDF", await WeeklyPdfKeysAsync(ct)),
        };

        HashSet<string>? objects = new(StringComparer.Ordinal);
        string? storageError = null;
        try
        {
            await foreach (var key in storage.ListKeysAsync(ct))
            {
                objects.Add(key);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Проверка восстановления: список объектов хранилища не прочитан");
            storageError = ex.Message;
            objects = null;
        }

        var result = sources.Select(s =>
        {
            var references = s.Keys.Distinct(StringComparer.Ordinal).ToList();
            var missing = objects is null ? [] : references.Where(k => !objects.Contains(k)).Order(StringComparer.Ordinal).ToList();
            return new RecoveryFileSource(s.Source, s.Label, references.Count, missing.Count, missing.Take(MissingSamples).ToList());
        }).ToList();

        // Пробное чтение: первый файл, на который ссылается база и который есть в хранилище, читается приложением целиком.
        bool? readOk = null;
        string? readError = null;
        if (objects is not null && sources.SelectMany(s => s.Keys).FirstOrDefault(objects.Contains) is { } probe)
        {
            try
            {
                await storage.DownloadAsync(probe, Stream.Null, ct);
                readOk = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                readOk = false;
                readError = ex.Message;
            }
        }

        return new RecoveryFilesCheck(objects?.Count, storageError, result, readOk, readError);
    }

    /// <summary>Ключи еженедельных PDF лежат в JSON поставки (список файлов отчёта).</summary>
    private async Task<List<string>> WeeklyPdfKeysAsync(CancellationToken ct)
    {
        var rows = await db.TelegramReportDeliveries.AsNoTracking().Where(d => d.Files != null).Select(d => d.Files!).ToListAsync(ct);
        var keys = new List<string>();
        foreach (var json in rows)
        {
            try
            {
                keys.AddRange((JsonSerializer.Deserialize<List<WeeklyPdfService.StoredFile>>(json, JsonSerializerOptions.Web) ?? [])
                    .Select(f => f.Key).Where(k => !string.IsNullOrEmpty(k)));
            }
            catch (JsonException)
            {
                // Не список файлов — пропускаем.
            }
        }

        return keys;
    }
}

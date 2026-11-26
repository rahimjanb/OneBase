using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.Finance;
using OneBase.Finance.Integration.Contracts;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.OneC;

/// <summary>
/// Настройки интеграции 1С: строка «onec» в public."Integrations" (отдел «Финансы»). Несекретные настройки — в SettingsJson, принятая база 1С —
/// в DataSourceUrl, ключ службы — только SHA-256 хэш (в SettingsJson) и последние 4 символа (SecretHint). Пароль и API Secret 1С, если
/// подключение задано на странице «1С», — в SettingsJson только зашифрованными (ASP.NET Data Protection); расшифровываются лишь для службы.
/// Кэш — 30 секунд: изменения на другом экземпляре API доходят быстро.
/// </summary>
public sealed class OneCSettingsStore(IServiceScopeFactory scopes, TimeProvider clock, IDataProtectionProvider protection) : IOneCSettingsStore
{
    private const int MaxPem = 20_000;
    private readonly IDataProtector _password = protection.CreateProtector(ProtectionPurposes.OneCPassword);
    private readonly IDataProtector _secret = protection.CreateProtector(ProtectionPurposes.OneCApiSecret);

    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _write = new(1, 1);
    private volatile Cached? _cached;

    private sealed record Cached(OneCSettings Settings, string? TokenHash, DateTimeOffset At);

    /// <summary>Несекретные настройки в SettingsJson.</summary>
    private sealed class Data
    {
        public bool Paused { get; set; }
        public int IntervalMinutes { get; set; } = OneCSettings.DefaultInterval;
        public int BatchSize { get; set; } = OneCSettings.DefaultBatch;
        public bool AiEnabled { get; set; }
        public string? AcceptedInfobaseName { get; set; }
        public bool DemoData { get; set; }
        public string? PendingInfobaseId { get; set; }
        public string? PendingInfobaseName { get; set; }
        public string? TokenSha256 { get; set; }
        public DateTimeOffset? TokenCreatedAt { get; set; }

        // Подключение к 1С со страницы «1С».
        public string? BaseUrl { get; set; }
        public string? Username { get; set; }
        public string? ProtectedPassword { get; set; }
        public string? ProtectedApiSecret { get; set; }
        public string? ApiSecretHint { get; set; }
        public string? CaCertificatePem { get; set; }
        public DateTimeOffset? ConnectionUpdatedAt { get; set; }

        public List<string> OrganizationIds { get; set; } = [];
        public List<string> Modules { get; set; } = [];
        public DateOnly? HistoryFrom { get; set; }
    }

    public async Task<OneCSettings> GetAsync(CancellationToken ct = default) => (await LoadAsync(ct)).Settings;

    public async Task<OneCSettings> SaveAsync(OneCSettingsChange change, Guid userId, CancellationToken ct = default)
    {
        Validate(change);
        return await UpdateAsync((row, data) =>
        {
            data.Paused = change.Paused ?? data.Paused;
            data.IntervalMinutes = OneCSettings.ClampInterval(change.IntervalMinutes ?? data.IntervalMinutes);
            data.BatchSize = OneCSettings.ClampBatch(change.BatchSize ?? data.BatchSize);
            data.AiEnabled = change.AiEnabled ?? data.AiEnabled;
            ApplyConnection(change, data);
            if (change.OrganizationIds is { } orgs)
            {
                data.OrganizationIds = orgs.Select(o => o.Trim()).Where(o => o.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            }

            if (change.Modules is { } modules)
            {
                var normalized = OneCModules.Normalize(modules);
                data.Modules = normalized.Count == OneCModules.All.Count ? [] : normalized.ToList();
            }

            data.HistoryFrom = change.ClearHistoryFrom ? null : change.HistoryFrom ?? data.HistoryFrom;
            row.UpdatedById = userId;
        }, ct);
    }

    public async Task<OneCConnectionSecrets> ConnectionAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == OneCSettings.Code, ct);
        var data = Read(row?.SettingsJson);
        return new OneCConnectionSecrets(data.BaseUrl, data.Username, Unprotect(_password, data.ProtectedPassword), Unprotect(_secret, data.ProtectedApiSecret),
            data.CaCertificatePem, ConnectionVersion(data));
    }

    /// <summary>
    /// Проверка подключения со страницы «1С»: адрес — https без логина и пароля в нём (http — только этот компьютер), сертификат — PEM без
    /// закрытого ключа, длины полей ограничены. Ошибка — ArgumentException с текстом для администратора.
    /// </summary>
    public static void Validate(OneCSettingsChange change)
    {
        if (change.BaseUrl is { Length: > 0 } url)
        {
            if (url.Length > 500 || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            {
                throw new ArgumentException("Адрес HTTP API 1С — полный адрес https://…/hs/onebase/v1.");
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ArgumentException("Логин и пароль нельзя писать в адресе 1С — для них есть отдельные поля.");
            }

            if (uri.Scheme == "http" && !uri.IsLoopback)
            {
                throw new ArgumentException("Подключение к 1С — только по HTTPS (http — лишь для проверки на этом компьютере).");
            }
        }

        if (change.Username is { Length: > 200 } || change.Password is { Length: > 500 } || change.ApiSecret is { Length: > 500 })
        {
            throw new ArgumentException("Слишком длинный пользователь, пароль или API Secret 1С.");
        }

        if (change.CaCertificatePem is { Length: > 0 } pem)
        {
            if (pem.Length > MaxPem || pem.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Нужен только сертификат (открытая часть, PEM) — без закрытого ключа.");
            }

            try
            {
                using var _ = X509Certificate2.CreateFromPem(pem);
            }
            catch (CryptographicException)
            {
                throw new ArgumentException("Сертификат не читается: нужен файл .crt в формате PEM (-----BEGIN CERTIFICATE-----).");
            }
        }

        if (change.OrganizationIds is { Count: > 50 })
        {
            throw new ArgumentException("Не больше 50 организаций.");
        }
    }

    private void ApplyConnection(OneCSettingsChange change, Data data)
    {
        var before = (data.BaseUrl, data.Username, data.ProtectedPassword, data.ProtectedApiSecret, data.CaCertificatePem);
        if (change.BaseUrl is { } url)
        {
            data.BaseUrl = url.Trim().Length == 0 ? null : url.Trim().TrimEnd('/');
            if (data.BaseUrl is null)
            {
                // Подключение убрано со страницы: служба берёт его из своих переменных (если они есть).
                data.Username = null;
                data.ProtectedPassword = null;
                data.ProtectedApiSecret = null;
                data.ApiSecretHint = null;
                data.CaCertificatePem = null;
            }
        }

        if (data.BaseUrl is not null)
        {
            data.Username = change.Username is { } user ? user.Trim().Length == 0 ? null : user.Trim() : data.Username;
            if (change.Password is { } password)
            {
                data.ProtectedPassword = password.Length == 0 ? null : _password.Protect(password);
            }

            if (change.ApiSecret is { } secret)
            {
                data.ProtectedApiSecret = secret.Trim().Length == 0 ? null : _secret.Protect(secret.Trim());
                data.ApiSecretHint = secret.Trim().Length < 8 ? null : "••" + secret.Trim()[^2..];
            }

            if (change.CaCertificatePem is { } pem)
            {
                data.CaCertificatePem = pem.Trim().Length == 0 ? null : pem.Trim();
            }
        }

        if (before != (data.BaseUrl, data.Username, data.ProtectedPassword, data.ProtectedApiSecret, data.CaCertificatePem))
        {
            data.ConnectionUpdatedAt = clock.GetUtcNow();
        }
    }

    /// <summary>Версия подключения: от адреса, пользователя и времени изменения (секреты в неё не входят).</summary>
    private static string? ConnectionVersion(Data data) =>
        data.BaseUrl is null ? null
            : Hash($"{data.BaseUrl}|{data.Username}|{data.ConnectionUpdatedAt?.ToUnixTimeMilliseconds()}")[..16];

    private static string? Unprotect(IDataProtector protector, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return protector.Unprotect(value);
        }
        catch (CryptographicException)
        {
            // Ключи защиты данных сменились (новая установка без каталога ключей): секрет нужно ввести заново.
            return null;
        }
    }

    public async Task<string> CreateTokenAsync(Guid userId, CancellationToken ct = default)
    {
        // 32 случайных байта: в URL-безопасном base64 — 43 символа. Показывается один раз.
        var token = "obi_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        await UpdateAsync((row, data) =>
        {
            data.TokenSha256 = Hash(token);
            data.TokenCreatedAt = clock.GetUtcNow();
            row.SecretHint = "••••" + token[^4..];
            row.Enabled = true;
            row.UpdatedById = userId;
        }, ct);
        return token;
    }

    public async Task<bool> VerifyTokenAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
        {
            return false;
        }

        var stored = (await LoadAsync(ct)).TokenHash;
        if (stored is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(Hash(token)));
    }

    public async Task AcceptInfobaseAsync(string? infobaseId, string? infobaseName, bool demo, CancellationToken ct = default) =>
        await UpdateAsync((row, data) =>
        {
            row.DataSourceUrl = infobaseId;
            data.AcceptedInfobaseName = infobaseName;
            data.DemoData = infobaseId is not null && demo;
            if (data.PendingInfobaseId == infobaseId || infobaseId is null)
            {
                data.PendingInfobaseId = null;
                data.PendingInfobaseName = null;
            }
        }, ct);

    public async Task NotePendingInfobaseAsync(string infobaseId, string? infobaseName, CancellationToken ct = default) =>
        await UpdateAsync((_, data) =>
        {
            data.PendingInfobaseId = infobaseId;
            data.PendingInfobaseName = infobaseName;
        }, ct);

    private async Task<Cached> LoadAsync(CancellationToken ct)
    {
        if (_cached is { } cached && clock.GetUtcNow() - cached.At < CacheFor)
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == OneCSettings.Code, ct);
        var fresh = Build(row, Read(row?.SettingsJson));
        _cached = fresh;
        return fresh;
    }

    private async Task<OneCSettings> UpdateAsync(Action<IntegrationConnection, Data> change, CancellationToken ct)
    {
        await _write.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var row = await db.Integrations.FirstOrDefaultAsync(i => i.Code == OneCSettings.Code, ct);
            if (row is null)
            {
                row = new IntegrationConnection { Code = OneCSettings.Code, DepartmentCode = OneCSettings.Department, Enabled = false };
                db.Integrations.Add(row);
            }

            var data = Read(row.SettingsJson);
            change(row, data);
            row.SettingsJson = JsonSerializer.Serialize(data);
            row.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            var fresh = Build(row, data);
            _cached = fresh;
            return fresh.Settings;
        }
        finally
        {
            _write.Release();
        }
    }

    private Cached Build(IntegrationConnection? row, Data data) => new(new OneCSettings
    {
        Paused = data.Paused, IntervalMinutes = OneCSettings.ClampInterval(data.IntervalMinutes), BatchSize = OneCSettings.ClampBatch(data.BatchSize),
        AiEnabled = data.AiEnabled, AcceptedInfobaseId = row?.DataSourceUrl, AcceptedInfobaseName = data.AcceptedInfobaseName,
        PendingInfobaseId = data.PendingInfobaseId, PendingInfobaseName = data.PendingInfobaseName, DemoData = data.DemoData,
        HasToken = data.TokenSha256 is not null, TokenHint = data.TokenSha256 is null ? null : row?.SecretHint, TokenCreatedAt = data.TokenCreatedAt,
        UpdatedAt = row?.UpdatedAt,
        BaseUrl = data.BaseUrl, Username = data.Username, HasPassword = data.ProtectedPassword is not null, HasApiSecret = data.ProtectedApiSecret is not null,
        ApiSecretHint = data.ProtectedApiSecret is null ? null : data.ApiSecretHint, CaCertificatePem = data.CaCertificatePem,
        ConnectionUpdatedAt = data.ConnectionUpdatedAt, ConnectionVersion = ConnectionVersion(data),
        OrganizationIds = data.OrganizationIds, Modules = OneCModules.Normalize(data.Modules), HistoryFrom = data.HistoryFrom,
    }, data.TokenSha256, clock.GetUtcNow());

    private static Data Read(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? new Data() : JsonSerializer.Deserialize<Data>(json) ?? new Data();
        }
        catch (JsonException)
        {
            return new Data();
        }
    }

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

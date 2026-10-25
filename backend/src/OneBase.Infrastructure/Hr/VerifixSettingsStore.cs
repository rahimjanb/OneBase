using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Hr;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Hr;

public enum VerifixSecretSource
{
    None,

    /// <summary>Verifix__ClientId / Verifix__ClientSecret из окружения сервера.</summary>
    Environment,

    /// <summary>Задано в OneBase: «Настройки → Интеграции → HR → Verifix» (секрет зашифрован Data Protection).</summary>
    OneBase,
}

/// <summary>Несекретные настройки Verifix — JSON в Integrations.SettingsJson.</summary>
public sealed class VerifixSettingsData
{
    public string? ClientId { get; set; }

    /// <summary>ID организации (заголовок filial_id). С client credentials необязателен — организация задана в клиенте OAuth2.</summary>
    public long? FilialId { get; set; }

    public bool AutoSync { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Глубина первой загрузки отметок и табеля, дней.</summary>
    public int HistoryDays { get; set; } = 31;

    /// <summary>Сколько последних дней перечитывать каждую синхронизацию: терминал мог передать отметки позже, отметку могли исправить.</summary>
    public int OverlapDays { get; set; } = 3;

    /// <summary>Допуск опоздания и раннего ухода, минут.</summary>
    public int LateGraceMinutes { get; set; } = 0;

    public int EarlyLeaveGraceMinutes { get; set; } = 0;

    public string TimeZone { get; set; } = "Asia/Tashkent";
}

/// <summary>Действующее подключение к Verifix.</summary>
public sealed record VerifixConnection(
    string BaseUrl,
    string ClientId,
    string ClientSecret,
    long? FilialId,
    bool Enabled,
    VerifixSecretSource SecretSource,
    string? SecretHint,
    VerifixSettingsData Data)
{
    public bool HasCredentials => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public bool IsReady => Enabled && HasCredentials;
}

public sealed record VerifixSettingsInput(
    string? BaseUrl,
    string? ClientId,
    string? ClientSecret,
    long? FilialId,
    bool Enabled,
    bool AutoSync,
    int IntervalMinutes,
    int HistoryDays,
    int OverlapDays,
    int LateGraceMinutes,
    int EarlyLeaveGraceMinutes);

/// <summary>
/// Настройки подключения к Verifix: строка Integrations с кодом "verifix" (отдел HR). client_secret — только зашифрованный (Data Protection)
/// и наружу не отдаётся (видно лишь «••••» и 4 последних символа); иначе — из окружения сервера. Кэш в памяти сбрасывается при сохранении.
/// Включается явно: без «Включено» синхронизация не идёт, даже если ключи есть в окружении.
/// </summary>
public sealed class VerifixSettingsStore(
    IServiceScopeFactory scopes,
    VerifixOptions environment,
    IDataProtectionProvider dataProtection,
    ILogger<VerifixSettingsStore> logger) : IHrRules
{
    public const string Code = "verifix";
    public const string Department = "hr";

    public const int MinInterval = 15;
    public const int MaxInterval = 1440;
    public const int MaxHistoryDays = 92;

    private readonly IDataProtector _protector = dataProtection.CreateProtector("OneBase.Integrations.Verifix.ClientSecret");
    private volatile VerifixConnection? _cached;

    public async Task<VerifixConnection> GetAsync(CancellationToken ct = default)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == Code, ct);
        var data = Read(row?.SettingsJson);

        var secret = Unprotect(row?.ProtectedSecret);
        var source = VerifixSecretSource.OneBase;
        var hint = row?.SecretHint;
        if (string.IsNullOrEmpty(secret))
        {
            secret = environment.ClientSecret;
            source = string.IsNullOrEmpty(secret) ? VerifixSecretSource.None : VerifixSecretSource.Environment;
            hint = string.IsNullOrEmpty(secret) ? null : Hint(secret);
        }

        var baseUrl = Normalize(!string.IsNullOrWhiteSpace(row?.BaseUrl) ? row!.BaseUrl : environment.BaseUrl);
        var clientId = !string.IsNullOrWhiteSpace(data.ClientId) ? data.ClientId!.Trim() : environment.ClientId.Trim();
        var connection = new VerifixConnection(baseUrl, clientId, secret ?? string.Empty, data.FilialId, row?.Enabled ?? false, source, hint, data);
        _cached = connection;
        return connection;
    }

    /// <summary>Правила учёта времени для расчёта посещаемости.</summary>
    public async Task<HrRules> GetRulesAsync(CancellationToken ct = default)
    {
        var data = (await GetAsync(ct)).Data;
        return new HrRules(VerifixFormats.Zone(data.TimeZone), Math.Max(0, data.LateGraceMinutes), Math.Max(0, data.EarlyLeaveGraceMinutes));
    }

    public async Task<HrIntegrationState> GetStateAsync(CancellationToken ct = default)
    {
        var c = await GetAsync(ct);
        return new HrIntegrationState(c.HasCredentials, c.Enabled, c.Data.AutoSync, c.Data.IntervalMinutes);
    }

    /// <summary>Сохраняет настройки. ClientSecret: null — не менять, "" — удалить сохранённый в OneBase.</summary>
    public async Task SaveAsync(VerifixSettingsInput input, Guid userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await GetOrCreateAsync(db, ct);
        var data = Read(row.SettingsJson);

        var baseUrl = Normalize(input.BaseUrl);
        row.BaseUrl = baseUrl.Length == 0 || baseUrl == Normalize(environment.BaseUrl) ? null : baseUrl;
        row.Enabled = input.Enabled;
        data.ClientId = string.IsNullOrWhiteSpace(input.ClientId) ? null : input.ClientId.Trim();
        data.FilialId = input.FilialId is > 0 ? input.FilialId : null;
        data.AutoSync = input.AutoSync;
        data.IntervalMinutes = Math.Clamp(input.IntervalMinutes, MinInterval, MaxInterval);
        data.HistoryDays = Math.Clamp(input.HistoryDays, 1, MaxHistoryDays);
        data.OverlapDays = Math.Clamp(input.OverlapDays, 1, 14);
        data.LateGraceMinutes = Math.Clamp(input.LateGraceMinutes, 0, 120);
        data.EarlyLeaveGraceMinutes = Math.Clamp(input.EarlyLeaveGraceMinutes, 0, 120);
        row.SettingsJson = JsonSerializer.Serialize(data);

        if (input.ClientSecret is not null)
        {
            var secret = input.ClientSecret.Trim();
            row.ProtectedSecret = secret.Length == 0 ? null : _protector.Protect(secret);
            row.SecretHint = secret.Length == 0 ? null : Hint(secret);
        }

        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        _cached = null;
    }

    public async Task SaveTestResultAsync(bool ok, string message, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await GetOrCreateAsync(db, ct);
        row.LastTestAt = DateTimeOffset.UtcNow;
        row.LastTestOk = ok;
        row.LastTestMessage = message.Length > 500 ? message[..500] : message;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IntegrationConnection?> GetRowAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        return await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == Code, ct);
    }

    public static string Normalize(string? baseUrl) => (baseUrl ?? string.Empty).Trim().TrimEnd('/');

    /// <summary>Только https; http — лишь для адреса этого же компьютера (локальная проверка с подставным сервером).</summary>
    public static bool IsValidUrl(string baseUrl) =>
        Uri.TryCreate(Normalize(baseUrl), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    public static string Hint(string secret) => secret.Length <= 8 ? "••••" : "••••" + secret[^4..];

    private static VerifixSettingsData Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new VerifixSettingsData();
        }

        try
        {
            return JsonSerializer.Deserialize<VerifixSettingsData>(json) ?? new VerifixSettingsData();
        }
        catch (JsonException)
        {
            return new VerifixSettingsData();
        }
    }

    private static async Task<IntegrationConnection> GetOrCreateAsync(OneBaseDbContext db, CancellationToken ct)
    {
        var row = await db.Integrations.FirstOrDefaultAsync(i => i.Code == Code, ct);
        if (row is null)
        {
            row = new IntegrationConnection { Code = Code, DepartmentCode = Department, Enabled = false };
            db.Integrations.Add(row);
        }

        return row;
    }

    private string? Unprotect(string? protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Не удалось расшифровать client_secret Verifix — ключи Data Protection изменились. Задайте секрет заново.");
            return null;
        }
    }
}

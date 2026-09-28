using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Linko;

public enum LinkoSettingsSource
{
    None,

    /// <summary>LINKO_BASE_URL / LINKO_TOKEN из окружения (.env).</summary>
    Environment,

    /// <summary>Задано в OneBase: «Настройки → Интеграции → Продажи → Linko».</summary>
    OneBase,
}

public sealed record LinkoConnectionSettings(
    string BaseUrl,
    string Token,
    bool Enabled,
    LinkoSettingsSource TokenSource,
    string? TokenHint,
    string PlanToken = "",
    LinkoSettingsSource PlanTokenSource = LinkoSettingsSource.None,
    string? PlanTokenHint = null)
{
    public bool HasCredentials => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Token);

    /// <summary>Есть токен API планов (staff_balance).</summary>
    public bool HasPlanCredentials => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(PlanToken);

    /// <summary>Можно синхронизировать: включено и есть адрес с токеном.</summary>
    public bool IsReady => Enabled && HasCredentials;
}

/// <summary>
/// Действующие настройки подключения к Linko: из OneBase (БД, токен зашифрован), иначе — из окружения.
/// Кэшируются в памяти и сбрасываются при сохранении.
/// </summary>
public sealed class LinkoSettingsStore(
    IServiceScopeFactory scopes,
    LinkoOptions environment,
    IDataProtectionProvider dataProtection,
    ILogger<LinkoSettingsStore> logger)
{
    public const string Code = "linko";
    public const string Department = "sales";

    private readonly IDataProtector _protector = dataProtection.CreateProtector("OneBase.Integrations.Linko.Token");
    private readonly IDataProtector _planProtector = dataProtection.CreateProtector("OneBase.Integrations.Linko.PlanToken");
    private volatile LinkoConnectionSettings? _cached;

    public async Task<LinkoConnectionSettings> GetAsync(CancellationToken ct = default)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == Code, ct);

        var baseUrl = !string.IsNullOrWhiteSpace(row?.BaseUrl) ? row!.BaseUrl! : environment.BaseUrl;
        var token = Unprotect(_protector, row?.ProtectedSecret);
        var source = LinkoSettingsSource.OneBase;
        var hint = row?.SecretHint;

        if (string.IsNullOrEmpty(token))
        {
            token = environment.Token;
            source = string.IsNullOrEmpty(token) ? LinkoSettingsSource.None : LinkoSettingsSource.Environment;
            hint = string.IsNullOrEmpty(token) ? null : Hint(token);
        }

        var planToken = Unprotect(_planProtector, row?.ProtectedPlanSecret);
        var planSource = LinkoSettingsSource.OneBase;
        var planHint = row?.PlanSecretHint;
        if (string.IsNullOrEmpty(planToken))
        {
            planToken = environment.PlanToken;
            planSource = string.IsNullOrEmpty(planToken) ? LinkoSettingsSource.None : LinkoSettingsSource.Environment;
            planHint = string.IsNullOrEmpty(planToken) ? null : Hint(planToken);
        }

        var settings = new LinkoConnectionSettings(
            Normalize(baseUrl), token, row?.Enabled ?? true, source, hint, planToken, planSource, planHint);
        _cached = settings;
        return settings;
    }

    /// <summary>
    /// Сохраняет настройки. Токены: null — оставить текущий, "" — удалить сохранённый в OneBase (вернуться к .env).
    /// </summary>
    public async Task SaveAsync(string baseUrl, string? newToken, string? newPlanToken, bool enabled, Guid userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await GetOrCreateAsync(db, ct);

        row.BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : Normalize(baseUrl);
        row.Enabled = enabled;
        if (newToken is not null)
        {
            var token = newToken.Trim();
            row.ProtectedSecret = token.Length == 0 ? null : _protector.Protect(token);
            row.SecretHint = token.Length == 0 ? null : Hint(token);
        }

        if (newPlanToken is not null)
        {
            var planToken = newPlanToken.Trim();
            row.ProtectedPlanSecret = planToken.Length == 0 ? null : _planProtector.Protect(planToken);
            row.PlanSecretHint = planToken.Length == 0 ? null : Hint(planToken);
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

    /// <summary>Адрес без хвостового слэша; пусто — если адрес не задан.</summary>
    public static string Normalize(string? baseUrl) => (baseUrl ?? string.Empty).Trim().TrimEnd('/');

    public static bool IsValidUrl(string baseUrl) =>
        Uri.TryCreate(Normalize(baseUrl), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string Hint(string token) => token.Length <= 4 ? "••••" : "••••" + token[^4..];

    private static async Task<IntegrationConnection> GetOrCreateAsync(OneBaseDbContext db, CancellationToken ct)
    {
        var row = await db.Integrations.FirstOrDefaultAsync(i => i.Code == Code, ct);
        if (row is null)
        {
            row = new IntegrationConnection { Code = Code, DepartmentCode = Department };
            db.Integrations.Add(row);
        }

        return row;
    }

    private string? Unprotect(IDataProtector protector, string? protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return null;
        }

        try
        {
            return protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Не удалось расшифровать токен Linko — ключи Data Protection изменились. Задайте токен заново.");
            return null;
        }
    }
}

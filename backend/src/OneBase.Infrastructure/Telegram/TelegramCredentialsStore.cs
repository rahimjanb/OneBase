using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Telegram;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Telegram;

public sealed record TelegramCredentialsView(bool Configured, int? ApiId, string? ApiHashHint, DateTimeOffset? UpdatedAt);

/// <summary>
/// api_id и api_hash приложения Telegram (my.telegram.org) — в строке интеграций «telegram»: api_id — в BaseUrl, api_hash — зашифрован
/// (ASP.NET Data Protection), в интерфейсе — только последние символы. Не в .env.
/// </summary>
public sealed class TelegramCredentialsStore(IServiceScopeFactory scopes, IDataProtectionProvider protection)
{
    public const string Code = "telegram";

    private readonly IDataProtector _protector = protection.CreateProtector("OneBase.Telegram.ApiHash");
    private volatile TelegramApiCredentials? _cached;

    public async Task<TelegramApiCredentials?> GetAsync(CancellationToken ct = default)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == Code, ct);
        if (row is not { Enabled: true } || !int.TryParse(row.BaseUrl, out var apiId) || string.IsNullOrEmpty(row.ProtectedSecret))
        {
            return null;
        }

        try
        {
            _cached = new TelegramApiCredentials(apiId, _protector.Unprotect(row.ProtectedSecret));
            return _cached;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public async Task<TelegramCredentialsView> ViewAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == Code, ct);
        return new TelegramCredentialsView(row?.ProtectedSecret is not null && int.TryParse(row.BaseUrl, out _),
            int.TryParse(row?.BaseUrl, out var id) ? id : null, row?.SecretHint, row?.UpdatedAt);
    }

    /// <summary>Сохранить; apiHash null — оставить прежний.</summary>
    public async Task SaveAsync(int apiId, string? apiHash, Guid userId, CancellationToken ct = default)
    {
        if (apiId <= 0)
        {
            throw new TelegramException("api_id — число из my.telegram.org.", TelegramErrorKind.Config);
        }

        var hash = apiHash?.Trim();
        if (hash is not null && !System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-fA-F]{32}$"))
        {
            throw new TelegramException("api_hash — 32 шестнадцатеричных символа из my.telegram.org.", TelegramErrorKind.Config);
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.FirstOrDefaultAsync(i => i.Code == Code, ct);
        if (row is null)
        {
            if (hash is null)
            {
                throw new TelegramException("Укажите api_hash.", TelegramErrorKind.Config);
            }

            row = new IntegrationConnection { Code = Code, DepartmentCode = "system" };
            db.Integrations.Add(row);
        }

        row.BaseUrl = apiId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (hash is not null)
        {
            row.ProtectedSecret = _protector.Protect(hash);
            row.SecretHint = hash[^4..];
        }

        row.Enabled = true;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        _cached = null;
    }
}

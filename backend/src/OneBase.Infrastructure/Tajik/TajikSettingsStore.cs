using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Tajik;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Tajik;

/// <summary>
/// Настройки 1С Таджикистан: строка «tj1c» в Integrations. Ключ доступа — зашифрован (Data Protection), наружу — только последние 2 символа.
/// Адрес и ключ из OneBase главнее переменных TajikSales__BaseUrl / TajikSales__Token. Интервал и дата начала истории — в SettingsJson.
/// </summary>
public sealed class TajikSettingsStore(
    IServiceScopeFactory scopes,
    TajikSalesOptions environment,
    IDataProtectionProvider protection,
    TimeProvider time,
    ILogger<TajikSettingsStore> logger) : ITajikSettingsStore
{
    private readonly IDataProtector _protector = protection.CreateProtector(ProtectionPurposes.TajikToken);

    private sealed record Extra(int? IntervalMinutes, DateOnly? HistoryFrom);

    public async Task<TajikSettings> GetAsync(CancellationToken ct = default)
    {
        var row = await RowAsync(ct);
        var extra = Read(row?.SettingsJson);
        var saved = Unprotect(row?.ProtectedSecret);
        var token = saved ?? (string.IsNullOrWhiteSpace(environment.Token) ? null : environment.Token.Trim());
        var baseUrl = Normalize(!string.IsNullOrWhiteSpace(row?.BaseUrl) ? row!.BaseUrl : environment.BaseUrl);
        return new TajikSettings
        {
            BaseUrl = baseUrl,
            HasToken = token is not null,
            TokenHint = saved is not null ? row!.SecretHint : token is not null ? Hint(token) : null,
            TokenSource = saved is not null ? "onebase" : token is not null ? "env" : "none",
            Enabled = row?.Enabled ?? true,
            IntervalMinutes = TajikSettings.ClampInterval(extra.IntervalMinutes ?? TajikSettings.DefaultInterval),
            HistoryFrom = extra.HistoryFrom ?? TajikSettings.DefaultHistoryFrom,
            UpdatedAt = row?.UpdatedAt,
            LastTestAt = row?.LastTestAt,
            LastTestOk = row?.LastTestOk,
            LastTestMessage = row?.LastTestMessage,
        };
    }

    public async Task<string?> TokenAsync(CancellationToken ct = default)
    {
        var row = await RowAsync(ct);
        return Unprotect(row?.ProtectedSecret) ?? (string.IsNullOrWhiteSpace(environment.Token) ? null : environment.Token.Trim());
    }

    public async Task<TajikSettings> SaveAsync(TajikSettingsChange change, Guid userId, CancellationToken ct = default)
    {
        if (change.BaseUrl is { } url && url.Trim().Length > 0 && !IsValidUrl(url))
        {
            throw new ArgumentException("Адрес API должен начинаться с http:// или https:// — например http://62.60.246.93");
        }

        if (change.Token is { } t && t.Trim().Length > 500)
        {
            throw new ArgumentException("Ключ доступа слишком длинный.");
        }

        if (change.HistoryFrom is { } from && (from.Year < 2020 || from > TajikText.Today(time.GetUtcNow())))
        {
            throw new ArgumentException("Дата начала истории — не раньше 2020 года и не позже сегодняшнего дня.");
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.FirstOrDefaultAsync(i => i.Code == TajikSettings.Code, ct);
        if (row is null)
        {
            row = new IntegrationConnection { Code = TajikSettings.Code, DepartmentCode = TajikSettings.Department };
            db.Integrations.Add(row);
        }

        if (change.BaseUrl is not null)
        {
            row.BaseUrl = change.BaseUrl.Trim().Length == 0 ? null : Normalize(change.BaseUrl);
        }

        if (change.Token is not null)
        {
            var token = change.Token.Trim();
            row.ProtectedSecret = token.Length == 0 ? null : _protector.Protect(token);
            row.SecretHint = token.Length == 0 ? null : Hint(token);
        }

        if (change.Enabled is { } enabled)
        {
            row.Enabled = enabled;
        }

        var extra = Read(row.SettingsJson);
        extra = extra with
        {
            IntervalMinutes = change.IntervalMinutes is { } i ? TajikSettings.ClampInterval(i) : extra.IntervalMinutes,
            HistoryFrom = change.HistoryFrom ?? extra.HistoryFrom,
        };
        row.SettingsJson = JsonSerializer.Serialize(extra);
        row.UpdatedAt = time.GetUtcNow().ToUniversalTime();
        row.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    public async Task SaveTestResultAsync(bool ok, string message, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var row = await db.Integrations.FirstOrDefaultAsync(i => i.Code == TajikSettings.Code, ct);
        if (row is null)
        {
            row = new IntegrationConnection { Code = TajikSettings.Code, DepartmentCode = TajikSettings.Department };
            db.Integrations.Add(row);
        }

        row.LastTestAt = time.GetUtcNow().ToUniversalTime();
        row.LastTestOk = ok;
        row.LastTestMessage = message.Length > 500 ? message[..500] : message;
        await db.SaveChangesAsync(ct);
    }

    public static string Normalize(string? url) => (url ?? string.Empty).Trim().TrimEnd('/');

    public static bool IsValidUrl(string url) =>
        Uri.TryCreate(Normalize(url), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query);

    /// <summary>Подсказка — последние 2 символа (ключ короткий: 8 символов), у ключей короче 6 — только точки.</summary>
    private static string Hint(string token) => token.Length < 6 ? "••••" : "••••" + token[^2..];

    private async Task<IntegrationConnection?> RowAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        return await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Code == TajikSettings.Code, ct);
    }

    private static Extra Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Extra(null, null);
        }

        try
        {
            return JsonSerializer.Deserialize<Extra>(json) ?? new Extra(null, null);
        }
        catch (JsonException)
        {
            return new Extra(null, null);
        }
    }

    private string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(value);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "1С Таджикистан: ключ доступа не расшифровывается — ключи Data Protection изменились. Задайте ключ заново.");
            return null;
        }
    }
}

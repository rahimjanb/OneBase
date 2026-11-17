using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Finance;
using OneBase.Domain.Integrations;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.OneC;

/// <summary>
/// Настройки интеграции 1С: строка «onec» в public."Integrations" (отдел «Финансы»). Несекретные настройки — в SettingsJson, принятая база 1С —
/// в DataSourceUrl, ключ службы — только SHA-256 хэш (в SettingsJson) и последние 4 символа (SecretHint). Пароль 1С здесь не хранится вовсе.
/// Кэш — 30 секунд: изменения на другом экземпляре API доходят быстро.
/// </summary>
public sealed class OneCSettingsStore(IServiceScopeFactory scopes, TimeProvider clock) : IOneCSettingsStore
{
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
    }

    public async Task<OneCSettings> GetAsync(CancellationToken ct = default) => (await LoadAsync(ct)).Settings;

    public async Task<OneCSettings> SaveAsync(OneCSettingsChange change, Guid userId, CancellationToken ct = default) =>
        await UpdateAsync((row, data) =>
        {
            data.Paused = change.Paused ?? data.Paused;
            data.IntervalMinutes = OneCSettings.ClampInterval(change.IntervalMinutes ?? data.IntervalMinutes);
            data.BatchSize = OneCSettings.ClampBatch(change.BatchSize ?? data.BatchSize);
            data.AiEnabled = change.AiEnabled ?? data.AiEnabled;
            row.UpdatedById = userId;
        }, ct);

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

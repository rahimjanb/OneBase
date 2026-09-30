using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Gateway;

public enum AiKeySource
{
    None,

    /// <summary>Из окружения сервера (.env): OPENAI_API_KEY, ANTHROPIC_API_KEY или LLM_API_KEY.</summary>
    Environment,

    /// <summary>Сохранён в OneBase: «Настройки → AI → Провайдеры».</summary>
    OneBase,
}

/// <summary>Модель конкретного провайдера.</summary>
public sealed record AiModelRef(string Provider, string Model)
{
    public override string ToString() => $"{Provider}/{Model}";
}

/// <summary>Действующее подключение к провайдеру. ApiKey живёт только в памяти backend и наружу не отдаётся.</summary>
public sealed record AiProviderState(
    string Code,
    string Name,
    string BaseUrl,
    bool CustomBaseUrl,
    bool Enabled,
    string ApiKey,
    AiKeySource KeySource,
    string? KeyHint)
{
    public bool HasKey => ApiKey.Length > 0;
    public bool IsReady => Enabled && HasKey;
    public AiCredentials Credentials => new(ApiKey, BaseUrl);
}

public sealed record AiSettingsSnapshot(
    bool Enabled,
    AiModelRef? Primary,
    AiModelRef? Fallback,
    AiModelRef? Router,
    AiModelRef? Embedding,
    double? Temperature,
    int MaxOutputTokens,
    bool PrimaryFromEnvironment);

/// <summary>Запасные значения из окружения сервера (.env / docker-compose).</summary>
public sealed class AiEnvironment
{
    /// <summary>LLM_PROVIDER: openai | anthropic.</summary>
    public string? Provider { get; init; }

    /// <summary>LLM_API_KEY — ключ провайдера из LLM_PROVIDER.</summary>
    public string? ApiKey { get; init; }

    /// <summary>LLM_MODEL — основная модель, пока она не выбрана в OneBase.</summary>
    public string? Model { get; init; }

    /// <summary>EMBEDDING_MODEL — модель эмбеддингов провайдера LLM_PROVIDER.</summary>
    public string? EmbeddingModel { get; init; }

    public string? OpenAiApiKey { get; init; }
    public string? AnthropicApiKey { get; init; }

    public string? KeyFor(string provider)
    {
        var own = provider switch
        {
            OpenAiProvider.ProviderCode => OpenAiApiKey,
            AnthropicProvider.ProviderCode => AnthropicApiKey,
            _ => null,
        };
        return !string.IsNullOrWhiteSpace(own) ? own.Trim()
            : string.Equals(NormalizedProvider, provider, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(ApiKey) ? ApiKey.Trim()
            : null;
    }

    public string? NormalizedProvider => Provider?.Trim().ToLowerInvariant() switch
    {
        "openai" => OpenAiProvider.ProviderCode,
        "anthropic" or "claude" => AnthropicProvider.ProviderCode,
        _ => null,
    };
}

/// <summary>Известные провайдеры по коду.</summary>
public sealed class AiProviderRegistry(IEnumerable<IAiProvider> providers)
{
    private readonly IReadOnlyList<IAiProvider> _all = providers.ToList();

    public IReadOnlyList<IAiProvider> All => _all;

    public IAiProvider? Find(string? code) => _all.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.Ordinal));
}

/// <summary>Откуда AI Gateway берёт настройки и ключи.</summary>
public interface IAiSettingsSource
{
    Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default);
    Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default);
}

/// <summary>
/// Настройки AI и ключи провайдеров: из OneBase (БД, ключ зашифрован Data Protection), иначе — из окружения.
/// Кэшируются в памяти и сбрасываются при сохранении.
/// </summary>
public sealed class AiSettingsStore(
    IServiceScopeFactory scopes,
    AiProviderRegistry registry,
    AiEnvironment environment,
    IDataProtectionProvider dataProtection,
    ILogger<AiSettingsStore> logger) : IAiSettingsSource
{
    private volatile IReadOnlyDictionary<string, AiProviderState>? _providers;
    private volatile AiSettingsSnapshot? _settings;

    private IDataProtector Protector(string code) => dataProtection.CreateProtector($"OneBase.AI.ProviderKey.{code}");

    public async Task<IReadOnlyDictionary<string, AiProviderState>> GetProvidersAsync(CancellationToken ct = default)
    {
        if (_providers is { } cached)
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var rows = await db.AiProviders.AsNoTracking().ToDictionaryAsync(p => p.Code, ct);

        var result = new Dictionary<string, AiProviderState>(StringComparer.Ordinal);
        foreach (var provider in registry.All)
        {
            rows.TryGetValue(provider.Code, out var row);
            var key = Unprotect(provider.Code, row?.ProtectedApiKey);
            var source = AiKeySource.OneBase;
            var hint = row?.ApiKeyHint;
            if (string.IsNullOrEmpty(key))
            {
                key = environment.KeyFor(provider.Code) ?? string.Empty;
                source = key.Length == 0 ? AiKeySource.None : AiKeySource.Environment;
                hint = key.Length == 0 ? null : Hint(key);
            }

            var custom = !string.IsNullOrWhiteSpace(row?.BaseUrl);
            result[provider.Code] = new AiProviderState(
                provider.Code,
                provider.Name,
                custom ? NormalizeUrl(row!.BaseUrl) : provider.DefaultBaseUrl,
                custom,
                row?.Enabled ?? true,
                key,
                source,
                hint);
        }

        _providers = result;
        return result;
    }

    public async Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) =>
        (await GetProvidersAsync(ct)).GetValueOrDefault(code);

    public async Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default)
    {
        if (_settings is { } cached)
        {
            return cached;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var row = await db.AiSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == AiSettings.SingletonId, ct);

        var primary = Ref(row?.PrimaryProvider, row?.PrimaryModel);
        var fromEnvironment = false;
        if (primary is null && environment.NormalizedProvider is { } envProvider && !string.IsNullOrWhiteSpace(environment.Model))
        {
            primary = new AiModelRef(envProvider, environment.Model.Trim());
            fromEnvironment = true;
        }

        var embedding = Ref(row?.EmbeddingProvider, row?.EmbeddingModel);
        if (embedding is null && environment.NormalizedProvider is { } embeddingProvider && !string.IsNullOrWhiteSpace(environment.EmbeddingModel)
            && registry.Find(embeddingProvider)?.SupportsEmbeddings == true)
        {
            embedding = new AiModelRef(embeddingProvider, environment.EmbeddingModel.Trim());
        }

        var settings = new AiSettingsSnapshot(
            row?.Enabled ?? true,
            primary,
            Ref(row?.FallbackProvider, row?.FallbackModel),
            Ref(row?.RouterProvider, row?.RouterModel),
            embedding,
            row?.Temperature,
            row?.MaxOutputTokens ?? 4096,
            fromEnvironment);
        _settings = settings;
        return settings;
    }

    /// <summary>Сохраняет подключение. apiKey: null — не менять, "" — удалить сохранённый в OneBase (вернуться к .env).</summary>
    public async Task SaveProviderAsync(string code, string? baseUrl, string? apiKey, bool enabled, Guid userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var row = await ProviderRowAsync(db, code, ct);

        row.BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : NormalizeUrl(baseUrl);
        row.Enabled = enabled;
        if (apiKey is not null)
        {
            var key = apiKey.Trim();
            row.ProtectedApiKey = key.Length == 0 ? null : Protector(code).Protect(key);
            row.ApiKeyHint = key.Length == 0 ? null : Hint(key);
        }

        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task SaveTestResultAsync(string code, bool ok, string message, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var row = await ProviderRowAsync(db, code, ct);
        row.LastTestAt = DateTimeOffset.UtcNow;
        row.LastTestOk = ok;
        row.LastTestMessage = message.Length > 500 ? message[..500] : message;
        await db.SaveChangesAsync(ct);
    }

    public void Invalidate()
    {
        _providers = null;
        _settings = null;
    }

    public static bool IsValidUrl(string url) =>
        Uri.TryCreate(NormalizeUrl(url), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public static string NormalizeUrl(string? url) => (url ?? string.Empty).Trim().TrimEnd('/');

    internal static async Task<AiProvider> ProviderRowAsync(IAppDbContext db, string code, CancellationToken ct)
    {
        var row = await db.AiProviders.FirstOrDefaultAsync(p => p.Code == code, ct);
        if (row is null)
        {
            row = new AiProvider { Code = code };
            db.AiProviders.Add(row);
        }

        return row;
    }

    private static AiModelRef? Ref(string? provider, string? model) =>
        string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model) ? null : new AiModelRef(provider, model);

    private static string Hint(string key) => key.Length <= 8 ? "••••" : "••••" + key[^4..];

    private string? Unprotect(string code, string? protectedKey)
    {
        if (string.IsNullOrEmpty(protectedKey))
        {
            return null;
        }

        try
        {
            return Protector(code).Unprotect(protectedKey);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Не удалось расшифровать ключ AI-провайдера {Provider} — ключи Data Protection изменились. Задайте ключ заново.", code);
            return null;
        }
    }
}

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Gateway;

public sealed record AiProviderTestResult(bool Ok, string Message, int? Models, long ElapsedMs);

public sealed record AiModelTestResult(bool Ok, string Message, string? Reply, AiTokenUsage? Usage, long ElapsedMs, bool UsedFallback, string? Model);

/// <summary>Проверка подключения к провайдерам и список их моделей.</summary>
public sealed class AiModelCatalog(IAppDbContext db, AiSettingsStore store, AiProviderRegistry registry, IAiGateway gateway)
{
    /// <summary>
    /// Проверка ключа запросом списка моделей (GET, без генерации и без оплаты токенов).
    /// Пустые apiKey / baseUrl — берутся сохранённые. Ничего не сохраняет.
    /// </summary>
    public async Task<AiProviderTestResult> TestAsync(string code, string? apiKey, string? baseUrl, CancellationToken ct)
    {
        var provider = registry.Find(code) ?? throw new KeyNotFoundException(code);
        var state = await store.GetProviderAsync(code, ct);
        var credentials = new AiCredentials(
            string.IsNullOrWhiteSpace(apiKey) ? state?.ApiKey ?? string.Empty : apiKey.Trim(),
            string.IsNullOrWhiteSpace(baseUrl) ? state?.BaseUrl ?? provider.DefaultBaseUrl : AiSettingsStore.NormalizeUrl(baseUrl));

        var watch = Stopwatch.StartNew();
        try
        {
            var models = await provider.ListModelsAsync(credentials, ct);
            return new AiProviderTestResult(true, $"Подключение к {provider.Name} работает: доступно моделей — {models.Count}.", models.Count, watch.ElapsedMilliseconds);
        }
        catch (AiProviderException ex)
        {
            return new AiProviderTestResult(false, ex.Message, null, watch.ElapsedMilliseconds);
        }
    }

    /// <summary>Загружает список моделей с сохранённым ключом. Модели, пропавшие у провайдера, помечаются недоступными, но не удаляются.</summary>
    public async Task<int> RefreshAsync(string code, CancellationToken ct)
    {
        var provider = registry.Find(code) ?? throw new KeyNotFoundException(code);
        var state = await store.GetProviderAsync(code, ct);
        if (state is not { HasKey: true })
        {
            throw new AiProviderException(code, null, $"Сначала сохраните ключ API {provider.Name}.", false);
        }

        var models = await provider.ListModelsAsync(state.Credentials, ct);
        var existing = await db.AiModels.Where(m => m.ProviderCode == code).ToDictionaryAsync(m => m.ModelId, ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in models)
        {
            if (!seen.Add(m.Id))
            {
                continue;
            }

            if (!existing.TryGetValue(m.Id, out var row))
            {
                row = new AiModel { ProviderCode = code, ModelId = m.Id };
                db.AiModels.Add(row);
            }

            row.DisplayName = m.DisplayName ?? row.DisplayName;
            row.ProviderCreatedAt = m.CreatedAt ?? row.ProviderCreatedAt;
            if (!row.Available)
            {
                row.Available = true;
                row.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        foreach (var gone in existing.Values.Where(r => r.Available && !seen.Contains(r.ModelId)))
        {
            gone.Available = false;
            gone.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var providerRow = await AiSettingsStore.ProviderRowAsync(db, code, ct);
        providerRow.ModelsRefreshedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return seen.Count;
    }

    /// <summary>
    /// Короткий пробный запрос к модели (генерация — провайдер берёт оплату за несколько токенов).
    /// Проверяет модель, параметры и резервное переключение так же, как их использует консультант.
    /// </summary>
    public async Task<AiModelTestResult> TestModelAsync(AiModelRef? model, Guid userId, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await gateway.CompleteAsync(
                [
                    new LlmMessage(LlmRole.System, "Это проверка подключения OneBase. Ответь одним словом."),
                    new LlmMessage(LlmRole.User, "Ответь словом «готово»."),
                ],
                [],
                new AiCallOptions
                {
                    Model = model,
                    AllowFallback = model is null,
                    MaxOutputTokens = 64,
                    Context = new AiCallContext(userId, null, null, "settings.test"),
                },
                ct);
            var reply = result.Text?.Trim();
            return new AiModelTestResult(true,
                result.UsedFallback ? $"Основная модель не ответила, ответила резервная {result.Model}." : $"Модель {result.Model} отвечает.",
                reply is { Length: > 200 } ? reply[..200] : reply,
                result.Usage, watch.ElapsedMilliseconds, result.UsedFallback, result.Model.ToString());
        }
        catch (AiProviderException ex)
        {
            return new AiModelTestResult(false, ex.Message, null, null, watch.ElapsedMilliseconds, false, null);
        }
        catch (LlmNotConfiguredException ex)
        {
            return new AiModelTestResult(false, ex.Message, null, null, watch.ElapsedMilliseconds, false, null);
        }
    }
}

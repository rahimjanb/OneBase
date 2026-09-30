using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Gateway;
using OneBase.AI.Providers;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>«Настройки → AI»: провайдеры и ключи API, модели, общие настройки. Ключи наружу не отдаются.</summary>
[ApiController]
[Route("api/ai")]
[HasPermission(Permissions.AiSettingsManage)]
public sealed class AiSettingsController(
    IAppDbContext db,
    AiSettingsStore store,
    AiProviderRegistry registry,
    AiModelCatalog catalog,
    IAiGateway gateway,
    IAuditLogger audit) : ControllerBase
{
    public sealed record ProviderInput(string? BaseUrl, string? ApiKey, bool Enabled);

    public sealed record ProviderTestInput(string? BaseUrl, string? ApiKey);

    public sealed record ModelInput(bool Enabled, decimal? InputPricePerMillion, decimal? OutputPricePerMillion);

    public sealed record ModelRefInput(string? Provider, string? Model);

    public sealed record SettingsInput(
        bool Enabled,
        ModelRefInput? Primary,
        ModelRefInput? Fallback,
        ModelRefInput? Router,
        ModelRefInput? Embedding,
        double? Temperature,
        int MaxOutputTokens);

    [HttpGet("providers")]
    public async Task<IActionResult> Providers(CancellationToken ct) => Ok(await ProvidersViewAsync(ct));

    /// <summary>Сохранить адрес, ключ и включение. ApiKey: null — не менять, "" — удалить сохранённый в OneBase.</summary>
    [HttpPut("providers/{code}")]
    public async Task<IActionResult> SaveProvider(string code, ProviderInput input, CancellationToken ct)
    {
        if (registry.Find(code) is null)
        {
            return NotFound();
        }

        var baseUrl = input.BaseUrl?.Trim() ?? string.Empty;
        if (baseUrl.Length > 0 && !AiSettingsStore.IsValidUrl(baseUrl))
        {
            return BadRequest(new { error = "Адрес API должен начинаться с https://" });
        }

        await store.SaveProviderAsync(code, baseUrl, input.ApiKey, input.Enabled, User.GetUserId(), ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.provider.updated", "ai_provider", code,
            new { customBaseUrl = baseUrl.Length > 0, input.Enabled, keyChanged = input.ApiKey is not null, keyRemoved = input.ApiKey is "" }, ct);
        return Ok(await ProvidersViewAsync(ct));
    }

    /// <summary>Проверка ключа списком моделей. Пустые поля — сохранённые значения. Ничего не сохраняет, кроме отметки о проверке.</summary>
    [HttpPost("providers/{code}/test")]
    public async Task<ActionResult<AiProviderTestResult>> TestProvider(string code, ProviderTestInput input, CancellationToken ct)
    {
        if (registry.Find(code) is null)
        {
            return NotFound();
        }

        var result = await catalog.TestAsync(code, input.ApiKey, input.BaseUrl, ct);
        var testedSaved = string.IsNullOrWhiteSpace(input.ApiKey) && string.IsNullOrWhiteSpace(input.BaseUrl);
        if (testedSaved)
        {
            await store.SaveTestResultAsync(code, result.Ok, result.Message, ct);
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.provider.tested", "ai_provider", code,
            new { result.Ok, testedSaved }, ct);
        return result;
    }

    /// <summary>Загрузить список моделей у провайдера (с сохранённым ключом).</summary>
    [HttpPost("providers/{code}/models/refresh")]
    public async Task<IActionResult> RefreshModels(string code, CancellationToken ct)
    {
        if (registry.Find(code) is not { } provider)
        {
            return NotFound();
        }

        if (await store.GetProviderAsync(code, ct) is not { HasKey: true })
        {
            return BadRequest(new { error = $"Сначала сохраните ключ API {provider.Name}." });
        }

        try
        {
            var count = await catalog.RefreshAsync(code, ct);
            await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.models.refreshed", "ai_provider", code, new { count }, ct);
            return Ok(new { count, models = await ModelsViewAsync(ct) });
        }
        catch (AiProviderException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("models")]
    public async Task<IActionResult> Models(CancellationToken ct) => Ok(await ModelsViewAsync(ct));

    [HttpPut("models/{id:guid}")]
    public async Task<IActionResult> SaveModel(Guid id, ModelInput input, CancellationToken ct)
    {
        if (input.InputPricePerMillion is < 0 || input.OutputPricePerMillion is < 0)
        {
            return BadRequest(new { error = "Цена не может быть отрицательной." });
        }

        var model = await db.AiModels.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (model is null)
        {
            return NotFound();
        }

        model.Enabled = input.Enabled;
        model.InputPricePerMillion = input.InputPricePerMillion;
        model.OutputPricePerMillion = input.OutputPricePerMillion;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.model.updated", "ai_model", $"{model.ProviderCode}/{model.ModelId}",
            new { input.Enabled, input.InputPricePerMillion, input.OutputPricePerMillion }, ct);
        return Ok(await ModelsViewAsync(ct));
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await SettingsViewAsync(ct));

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(SettingsInput input, CancellationToken ct)
    {
        if (input.Temperature is < 0 or > 1)
        {
            return BadRequest(new { error = "Температура — от 0 до 1 (так её принимают и OpenAI, и Anthropic)." });
        }

        if (input.MaxOutputTokens is < 256 or > 64000)
        {
            return BadRequest(new { error = "Максимум токенов ответа — от 256 до 64 000." });
        }

        var refs = new List<(string Slot, ModelRefInput? Ref)>
        {
            ("основной", input.Primary), ("резервной", input.Fallback), ("маршрутизации", input.Router), ("эмбеддингов", input.Embedding),
        };
        foreach (var (slot, value) in refs)
        {
            if (Normalize(value) is not { } r)
            {
                continue;
            }

            if (registry.Find(r.Provider) is not { } provider)
            {
                return BadRequest(new { error = $"Провайдер {slot} модели не поддерживается." });
            }

            if (slot == "эмбеддингов" && !provider.SupportsEmbeddings)
            {
                return BadRequest(new { error = $"{provider.Name} не строит эмбеддинги — выберите модель OpenAI." });
            }

            if (!await db.AiModels.AnyAsync(m => m.ProviderCode == r.Provider && m.ModelId == r.Model, ct))
            {
                return BadRequest(new { error = $"Модели {r.Model} нет в списке {provider.Name} — обновите список моделей в разделе «Модели»." });
            }
        }

        var row = await db.AiSettings.FirstOrDefaultAsync(s => s.Id == AiSettings.SingletonId, ct);
        if (row is null)
        {
            row = new AiSettings();
            db.AiSettings.Add(row);
        }

        row.Enabled = input.Enabled;
        (row.PrimaryProvider, row.PrimaryModel) = Split(input.Primary);
        (row.FallbackProvider, row.FallbackModel) = Split(input.Fallback);
        (row.RouterProvider, row.RouterModel) = Split(input.Router);
        (row.EmbeddingProvider, row.EmbeddingModel) = Split(input.Embedding);
        row.Temperature = input.Temperature;
        row.MaxOutputTokens = input.MaxOutputTokens;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedById = User.GetUserId();
        await db.SaveChangesAsync(ct);
        store.Invalidate();

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.settings.updated", "ai_settings", "1",
            new
            {
                input.Enabled,
                primary = Normalize(input.Primary)?.ToString(),
                fallback = Normalize(input.Fallback)?.ToString(),
                router = Normalize(input.Router)?.ToString(),
                embedding = Normalize(input.Embedding)?.ToString(),
                input.Temperature,
                input.MaxOutputTokens,
            }, ct);
        return Ok(await SettingsViewAsync(ct));
    }

    /// <summary>
    /// Пробный запрос к модели: provider/model — конкретная модель без резерва; пусто — как у консультанта (основная и резерв).
    /// Провайдер берёт оплату за несколько токенов.
    /// </summary>
    [HttpPost("settings/test")]
    public async Task<ActionResult<AiModelTestResult>> TestModel(ModelRefInput input, CancellationToken ct)
    {
        var result = await catalog.TestModelAsync(Normalize(input), User.GetUserId(), ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.model.tested", "ai_model", Normalize(input)?.ToString() ?? "default",
            new { result.Ok, result.UsedFallback }, ct);
        return result;
    }

    private static AiModelRef? Normalize(ModelRefInput? input) =>
        string.IsNullOrWhiteSpace(input?.Provider) || string.IsNullOrWhiteSpace(input.Model)
            ? null
            : new AiModelRef(input.Provider.Trim(), input.Model.Trim());

    private static (string?, string?) Split(ModelRefInput? input) =>
        Normalize(input) is { } r ? (r.Provider, r.Model) : (null, null);

    private async Task<object> ProvidersViewAsync(CancellationToken ct)
    {
        var states = await store.GetProvidersAsync(ct);
        var rows = await db.AiProviders.AsNoTracking().ToDictionaryAsync(p => p.Code, ct);
        var counts = await db.AiModels.AsNoTracking()
            .GroupBy(m => m.ProviderCode)
            .Select(g => new { g.Key, Available = g.Count(m => m.Available), Enabled = g.Count(m => m.Available && m.Enabled) })
            .ToDictionaryAsync(x => x.Key, ct);

        return registry.All.Select(p =>
        {
            var state = states[p.Code];
            rows.TryGetValue(p.Code, out var row);
            counts.TryGetValue(p.Code, out var count);
            return new
            {
                p.Code,
                p.Name,
                p.DefaultBaseUrl,
                p.SupportsEmbeddings,
                BaseUrl = state.CustomBaseUrl ? state.BaseUrl : null,
                state.Enabled,
                state.HasKey,
                state.KeyHint,
                KeySource = state.KeySource.ToString(),
                Status = !state.HasKey ? "not_configured" : !state.Enabled ? "disabled" : row?.LastTestOk == false ? "error" : "connected",
                UpdatedAt = row?.UpdatedAt,
                LastTest = row?.LastTestAt is null ? null : new { At = row.LastTestAt, Ok = row.LastTestOk, Message = row.LastTestMessage },
                ModelsRefreshedAt = row?.ModelsRefreshedAt,
                ModelsAvailable = count?.Available ?? 0,
                ModelsEnabled = count?.Enabled ?? 0,
            };
        }).ToList();
    }

    private async Task<object> ModelsViewAsync(CancellationToken ct)
    {
        var models = await db.AiModels.AsNoTracking()
            .OrderBy(m => m.ProviderCode).ThenByDescending(m => m.ProviderCreatedAt).ThenBy(m => m.ModelId)
            .ToListAsync(ct);
        return models.Select(m => new
        {
            m.Id,
            Provider = m.ProviderCode,
            Model = m.ModelId,
            m.DisplayName,
            m.Available,
            m.Enabled,
            m.InputPricePerMillion,
            m.OutputPricePerMillion,
            CreatedAt = m.ProviderCreatedAt,
        });
    }

    private async Task<object> SettingsViewAsync(CancellationToken ct)
    {
        var settings = await store.GetSettingsAsync(ct);
        var row = await db.AiSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == AiSettings.SingletonId, ct);
        var readiness = await gateway.GetReadinessAsync(ct);
        return new
        {
            settings.Enabled,
            Primary = settings.Primary,
            settings.PrimaryFromEnvironment,
            Fallback = settings.Fallback,
            Router = settings.Router,
            Embedding = settings.Embedding,
            settings.Temperature,
            settings.MaxOutputTokens,
            Ready = readiness.Ready,
            ReadinessMessage = readiness.Message,
            UpdatedAt = row?.UpdatedAt,
        };
    }
}

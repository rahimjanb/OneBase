using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OneBase.AI.Providers;

/// <summary>Общая часть HTTP-провайдеров: отправка, разбор ответа и перевод сетевых ошибок в <see cref="AiProviderException"/>.</summary>
public abstract class HttpAiProvider(IHttpClientFactory httpFactory) : IAiProvider
{
    /// <summary>Сколько ждать ответа модели. Длинные ответы с инструментами генерируются минутами.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(180);

    public abstract string Code { get; }
    public abstract string Name { get; }
    public abstract string DefaultBaseUrl { get; }
    public virtual bool SupportsEmbeddings => false;

    public abstract Task<AiCompletion> CompleteAsync(AiCredentials credentials, AiCompletionRequest request, CancellationToken cancellationToken = default);

    public abstract Task<IReadOnlyList<AiProviderModel>> ListModelsAsync(AiCredentials credentials, CancellationToken cancellationToken = default);

    public virtual Task<(IReadOnlyList<float[]> Vectors, AiTokenUsage Usage)> EmbedAsync(
        AiCredentials credentials, string model, IReadOnlyList<string> inputs, CancellationToken cancellationToken = default) =>
        throw new AiProviderException(Code, null, $"{Name} не строит эмбеддинги — выберите модель эмбеддингов другого провайдера.", false);

    /// <summary>Заголовки авторизации провайдера.</summary>
    protected abstract void Authorize(HttpRequestHeaders headers, string apiKey);

    protected static string Endpoint(AiCredentials credentials, string path) => credentials.BaseUrl.TrimEnd('/') + path;

    protected Task<JsonDocument> PostAsync(AiCredentials credentials, string url, JsonNode body, CancellationToken ct) =>
        SendAsync(credentials, () => new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        }, ct);

    protected Task<JsonDocument> GetAsync(AiCredentials credentials, string url, CancellationToken ct) =>
        SendAsync(credentials, () => new HttpRequestMessage(HttpMethod.Get, url), ct);

    private async Task<JsonDocument> SendAsync(AiCredentials credentials, Func<HttpRequestMessage> build, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new AiProviderException(Code, null, $"Ключ API {Name} не задан — добавьте его в «Настройки → AI → Провайдеры».", false);
        }

        using var request = build();
        Authorize(request.Headers, credentials.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var http = httpFactory.CreateClient(Code);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderException(Code, null, $"{Name} не ответил за {Timeout.TotalSeconds:0} с.", true);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(Code, null, $"Нет связи с {Name}: {ProviderErrors.Scrub(ex.Message, credentials.ApiKey)}", true);
        }

        using (response)
        {
            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AiProviderException(Code, null, $"{Name} не ответил за {Timeout.TotalSeconds:0} с.", true);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw ProviderErrors.FromStatus(Code, (int)response.StatusCode, body, credentials.ApiKey);
            }

            try
            {
                return JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                throw new AiProviderException(Code, (int)response.StatusCode, $"{Name} вернул ответ не в формате JSON — проверьте адрес API.", false);
            }
        }
    }

    protected static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    protected static int Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;

    /// <summary>Аргументы инструмента как JSON-объект; битый JSON от модели — пустой объект.</summary>
    protected static JsonElement ParseArguments(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    return doc.RootElement.Clone();
                }
            }
            catch (JsonException)
            {
                // ниже — пустой объект
            }
        }

        return JsonSerializer.SerializeToElement(new { });
    }
}

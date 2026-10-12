using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OneBase.AI.Gateway;
using OneBase.AI.Providers;

namespace OneBase.AI.Voice;

/// <summary>Голос консультанта (секция «Ai:Voice»): модели OpenAI для распознавания речи и озвучки ответов.</summary>
public sealed class VoiceOptions
{
    public const string Section = "Ai:Voice";

    public string TranscribeModel { get; set; } = "gpt-4o-transcribe";

    public string SpeechModel { get; set; } = "gpt-4o-mini-tts";

    public string Voice { get; set; } = "coral";

    /// <summary>Как читать (только у моделей gpt-4o-*-tts).</summary>
    public string Instructions { get; set; } =
        "Говори по-русски, спокойно и чётко, деловым тоном, в среднем темпе. Числа и единицы читай полностью.";

    /// <summary>
    /// Подсказка распознаванию: термины продаж, которых нет в обычной речи. Только список — на тишине модель иногда «повторяет» подсказку,
    /// такой ответ отбрасывается (<see cref="VoiceService.IsPromptEcho"/>).
    /// </summary>
    public string TranscribePrompt { get; set; } =
        "РОП, РМ, СВР, ТП, АКБ, страйк, вторичка, первичка, Linko, Кекс, Бамбук, Помадка, Трубочки, Печенье, Шоколад, Песочный, Придзел, "
        + "Хоразм, Бухоро, Самарканд, Ташкент, Фаргона, Наманган.";

    public long MaxAudioBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Сколько символов ответа озвучивается (остальное — на экране).</summary>
    public int MaxSpeechChars { get; set; } = 12_000;
}

public sealed record VoiceStatus(bool Ready, string? Message, string? SpeechModel, string? TranscribeModel);

public sealed record SpeechPart(byte[] Audio, int Part, int Parts);

/// <summary>
/// Голос AI-консультанта через OpenAI (ключ — «Настройки → AI → Провайдеры → OpenAI» или OPENAI_API_KEY):
/// распознавание вопроса (audio/transcriptions) и озвучка ответа частями (audio/speech, mp3). Готовый звук кэшируется —
/// повторное прослушивание не стоит денег. Ключ в браузер не уходит, ошибки — по-русски и без ключа.
/// </summary>
public sealed class VoiceService(
    IAiSettingsSource settings,
    IHttpClientFactory httpFactory,
    IAiUsageRecorder usage,
    IMemoryCache cache,
    VoiceOptions options)
{
    private const string Provider = OpenAiProvider.ProviderCode;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan CacheTime = TimeSpan.FromHours(6);

    /// <summary>Расширения для форматов записи браузеров (Chrome — webm/opus, Safari — mp4/aac).</summary>
    private static readonly Dictionary<string, string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/webm"] = "webm",
        ["audio/ogg"] = "ogg",
        ["audio/mp4"] = "mp4",
        ["audio/x-m4a"] = "m4a",
        ["audio/m4a"] = "m4a",
        ["audio/aac"] = "m4a",
        ["audio/mpeg"] = "mp3",
        ["audio/mp3"] = "mp3",
        ["audio/wav"] = "wav",
        ["audio/x-wav"] = "wav",
        ["audio/wave"] = "wav",
    };

    public async Task<VoiceStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var snapshot = await settings.GetSettingsAsync(ct);
        if (!snapshot.Enabled)
        {
            return new VoiceStatus(false, "AI выключен в «Настройки → AI».", null, null);
        }

        var provider = await settings.GetProviderAsync(Provider, ct);
        return provider is { Enabled: true } && !string.IsNullOrWhiteSpace(provider.ApiKey)
            ? new VoiceStatus(true, null, options.SpeechModel, options.TranscribeModel)
            : new VoiceStatus(false, "Для голоса нужен ключ OpenAI: «Настройки → AI → Провайдеры → OpenAI». Пока работает голос браузера, если он есть.",
                null, null);
    }

    /// <summary>Текст вопроса из записи. Пустая запись или тишина — пустая строка.</summary>
    public async Task<string> TranscribeAsync(AiCallContext context, byte[] audio, string? contentType, CancellationToken ct = default)
    {
        if (audio.Length == 0)
        {
            throw new VoiceException("Запись пустая — проверьте микрофон.");
        }

        if (audio.Length > options.MaxAudioBytes)
        {
            throw new VoiceException($"Запись больше {options.MaxAudioBytes / 1024 / 1024} МБ — говорите короче.");
        }

        var mediaType = (contentType ?? "").Split(';')[0].Trim();
        if (!AudioExtensions.TryGetValue(mediaType, out var extension))
        {
            throw new VoiceException($"Формат записи {mediaType} не поддерживается.");
        }

        var credentials = await CredentialsAsync(ct);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(file, "file", $"question.{extension}");
        form.Add(new StringContent(options.TranscribeModel), "model");
        form.Add(new StringContent("json"), "response_format");
        form.Add(new StringContent(options.TranscribePrompt), "prompt");

        var body = await SendAsync(context, options.TranscribeModel, credentials, "/audio/transcriptions", form, ct);
        using var json = JsonDocument.Parse(body);
        var result = json.RootElement.TryGetProperty("text", out var text) ? (text.GetString() ?? "").Trim() : "";
        return IsPromptEcho(result, options.TranscribePrompt) ? "" : result;
    }

    /// <summary>На записи без речи модель распознавания иногда возвращает саму подсказку — это не вопрос.</summary>
    public static bool IsPromptEcho(string text, string prompt)
    {
        static string Letters(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var heard = Letters(text);
        return heard.Length > 0 && Letters(prompt).Contains(heard, StringComparison.Ordinal);
    }

    /// <summary>Часть озвучки ответа (mp3). key — постоянный ключ ответа для кэша (id сообщения).</summary>
    public async Task<SpeechPart> SpeechAsync(AiCallContext context, string key, string markdown, int part, CancellationToken ct = default)
    {
        var parts = Parts(markdown);
        if (parts.Count == 0)
        {
            throw new VoiceException("В ответе нечего читать вслух.");
        }

        if (part < 0 || part >= parts.Count)
        {
            throw new VoiceException($"Части {part + 1} нет: всего {parts.Count}.");
        }

        var cacheKey = $"voice:{key}:{options.SpeechModel}:{options.Voice}:{part}";
        if (cache.TryGetValue(cacheKey, out byte[]? cached) && cached is not null)
        {
            return new SpeechPart(cached, part, parts.Count);
        }

        var credentials = await CredentialsAsync(ct);
        var request = new Dictionary<string, object>
        {
            ["model"] = options.SpeechModel,
            ["voice"] = options.Voice,
            ["input"] = parts[part],
            ["response_format"] = "mp3",
        };
        if (options.SpeechModel.Contains("-tts", StringComparison.OrdinalIgnoreCase) && !options.SpeechModel.StartsWith("tts-", StringComparison.OrdinalIgnoreCase))
        {
            request["instructions"] = options.Instructions;
        }

        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var audio = await SendBytesAsync(context, options.SpeechModel, credentials, "/audio/speech", content, ct);
        cache.Set(cacheKey, audio, CacheTime);
        return new SpeechPart(audio, part, parts.Count);
    }

    /// <summary>Текст для чтения вслух, частями (для голоса браузера, когда ключа OpenAI нет).</summary>
    public IReadOnlyList<string> Parts(string markdown)
    {
        var text = SpeechText.From(markdown);
        if (text.Length > options.MaxSpeechChars)
        {
            var cut = text.LastIndexOf(". ", options.MaxSpeechChars, StringComparison.Ordinal);
            text = text[..(cut > 0 ? cut + 1 : options.MaxSpeechChars)] + " Остальное — на экране.";
        }

        return SpeechText.Split(text);
    }

    private async Task<AiCredentials> CredentialsAsync(CancellationToken ct)
    {
        var provider = await settings.GetProviderAsync(Provider, ct);
        return provider is { Enabled: true } && !string.IsNullOrWhiteSpace(provider.ApiKey)
            ? provider.Credentials
            : throw new VoiceException("Голос не настроен: нужен ключ OpenAI в «Настройки → AI → Провайдеры».");
    }

    private async Task<string> SendAsync(AiCallContext context, string model, AiCredentials credentials, string path, HttpContent content, CancellationToken ct) =>
        Encoding.UTF8.GetString(await SendBytesAsync(context, model, credentials, path, content, ct));

    private async Task<byte[]> SendBytesAsync(AiCallContext context, string model, AiCredentials credentials, string path, HttpContent content,
        CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var modelRef = new AiModelRef(Provider, model);
        using var request = new HttpRequestMessage(HttpMethod.Post, credentials.BaseUrl.TrimEnd('/') + path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        string? error = null;
        try
        {
            using var response = await httpFactory.CreateClient(Provider).SendAsync(request, timeout.Token);
            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var failure = ProviderErrors.FromStatus(Provider, (int)response.StatusCode, Encoding.UTF8.GetString(bytes), credentials.ApiKey);
                error = failure.Message;
                throw new VoiceException(failure.Message);
            }

            return bytes;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            error = $"OpenAI не ответил за {RequestTimeout.TotalSeconds:0} с.";
            throw new VoiceException(error);
        }
        catch (HttpRequestException ex)
        {
            error = $"Нет связи с OpenAI: {ProviderErrors.Scrub(ex.Message, credentials.ApiKey)}";
            throw new VoiceException(error);
        }
        finally
        {
            await usage.RecordAsync(new AiCallRecord(context, modelRef, AiTokenUsage.None, watch.ElapsedMilliseconds, error is null, error),
                CancellationToken.None);
        }
    }
}

/// <summary>Ошибка голоса: текст безопасен для показа пользователю.</summary>
public sealed class VoiceException(string message) : Exception(message);

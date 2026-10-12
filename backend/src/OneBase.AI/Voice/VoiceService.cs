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

    /// <summary>Мужской голос OpenAI с самым естественным звучанием (cedar); если OpenAI его не примет — <see cref="FallbackVoice"/>.</summary>
    public string Voice { get; set; } = "cedar";

    /// <summary>Запасной мужской голос — если основной у этой модели недоступен (ответ 400 про voice).</summary>
    public string FallbackVoice { get; set; } = "ash";

    /// <summary>Как читать (только у моделей gpt-4o-*-tts): живой человек, а не диктор.</summary>
    public string Instructions { get; set; } =
        "Голос: мужчина лет сорока, опытный руководитель продаж, рассказывает коллеге итоги — живо и по-человечески. "
        + "Говори по-русски естественно, как в обычном разговоре, а не как диктор или автоответчик: спокойный уверенный тон, тёплый тембр, "
        + "разговорный темп, естественные паузы между мыслями, лёгкое смысловое ударение на главных цифрах. "
        + "Без пафоса и театральности, без растягивания слов и ровной «роботной» интонации. "
        + "Числа произноси так, как их говорят вслух, единицы — полностью: «кг» — килограммов, «т» — тонн, «%» — процентов. "
        + "Сокращения АКБ, СВР, ТП, РМ читай по буквам, РОП — одним словом.";

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

        // Основной голос OpenAI не принял раньше — сразу запасной, без лишнего запроса.
        var voice = cache.TryGetValue(RejectedKey(options.Voice), out bool _) ? options.FallbackVoice : options.Voice;
        // Ключ — по тексту части, голосу и инструкциям: правка подготовки текста или смена голоса не отдаёт старый звук.
        var cacheKey = $"voice:{options.SpeechModel}:{voice}:{Hash(options.Instructions + "\u001f" + parts[part])}";
        if (cache.TryGetValue(cacheKey, out byte[]? cached) && cached is not null)
        {
            return new SpeechPart(cached, part, parts.Count);
        }

        var credentials = await CredentialsAsync(ct);
        byte[] audio;
        try
        {
            audio = await SynthesizeAsync(context, credentials, voice, parts[part], ct);
        }
        catch (VoiceException ex) when (ex.Status == 400 && ex.Detail.Contains("voice", StringComparison.OrdinalIgnoreCase)
                                        && voice != options.FallbackVoice && !string.IsNullOrWhiteSpace(options.FallbackVoice))
        {
            cache.Set(RejectedKey(options.Voice), true, TimeSpan.FromHours(12));
            voice = options.FallbackVoice;
            cacheKey = $"voice:{options.SpeechModel}:{voice}:{Hash(options.Instructions + "\u001f" + parts[part])}";
            audio = await SynthesizeAsync(context, credentials, voice, parts[part], ct);
        }

        cache.Set(cacheKey, audio, CacheTime);
        return new SpeechPart(audio, part, parts.Count);
    }

    private async Task<byte[]> SynthesizeAsync(AiCallContext context, AiCredentials credentials, string voice, string input, CancellationToken ct)
    {
        var request = new Dictionary<string, object>
        {
            ["model"] = options.SpeechModel,
            ["voice"] = voice,
            ["input"] = input,
            ["response_format"] = "mp3",
        };
        if (options.SpeechModel.Contains("-tts", StringComparison.OrdinalIgnoreCase) && !options.SpeechModel.StartsWith("tts-", StringComparison.OrdinalIgnoreCase))
        {
            request["instructions"] = options.Instructions;
        }

        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        return await SendBytesAsync(context, options.SpeechModel, credentials, "/audio/speech", content, ct);
    }

    private string RejectedKey(string voice) => $"voice:rejected:{options.SpeechModel}:{voice}";

    private static string Hash(string text) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];

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
                var body = Encoding.UTF8.GetString(bytes);
                var failure = ProviderErrors.FromStatus(Provider, (int)response.StatusCode, body, credentials.ApiKey);
                error = failure.Message;
                throw new VoiceException(failure.Message, (int)response.StatusCode, ProviderErrors.Scrub(body, credentials.ApiKey));
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

/// <summary>Ошибка голоса: текст безопасен для показа пользователю. Status и Detail — ответ OpenAI (без ключа), если он был.</summary>
public sealed class VoiceException(string message, int? status = null, string? detail = null) : Exception(message)
{
    public int? Status { get; } = status;

    public string Detail { get; } = detail ?? string.Empty;
}

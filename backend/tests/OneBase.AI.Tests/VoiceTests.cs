using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OneBase.AI.Gateway;
using OneBase.AI.Voice;

namespace OneBase.AI.Tests;

/// <summary>Голос консультанта: текст для чтения вслух, деление на части, запросы распознавания и озвучки к OpenAI.</summary>
public class VoiceTests
{
    private const string Answer = """
        ## Продажи за октябрь

        **Выполнение** плана — 24,5% → прогноз 95%. Подробнее [в разделе](https://onebase.test/sales).

        | Регион | План, кг | Факт, кг |
        |---|---:|---:|
        | Хоразм | 40 505 | 9 911 |
        | Бухоро | — | 6 610 |

        - Кекс растёт
        - Бамбук отстаёт

        ```
        код не читается
        ```
        """;

    [Fact]
    public void Markdown_becomes_plain_speech()
    {
        var text = SpeechText.From(Answer);

        Assert.Equal(
            "Продажи за октябрь. Выполнение плана — 24,5% — прогноз 95%. Подробнее в разделе. "
            + "Хоразм, План, кг: 40 505, Факт, кг: 9 911. Бухоро, Факт, кг: 6 610. Кекс растёт. Бамбук отстаёт.",
            text);
        Assert.DoesNotContain("код", text);
        Assert.DoesNotContain("http", text);
    }

    [Fact]
    public void Sources_are_not_read_aloud()
    {
        const string answer = """
            В Ташкенте **333 активных магазина** за 1–11 октября (источник: OneBase → Продажи).

            *Источник: прогноз Sales AI по данным `get_sales` за 1–11 октября.*

            Прогноз — 46,7 т. Источник: OneBase → Продажи → Ташкент.

            **Источники:**
            - OneBase → Продажи → Республика
            - get_sales_by_branch — регионы

            | Регион | Факт, кг | Источник |
            |---|---:|---|
            | Хоразм | 9 911 | get_sales |

            Итог: план под угрозой.
            """;

        Assert.Equal(
            "В Ташкенте 333 активных магазина за 1–11 октября. Прогноз — 46,7 т. Хоразм, Факт, кг: 9 911. Итог: план под угрозой.",
            SpeechText.From(answer));
    }

    [Fact]
    public async Task Rejected_voice_falls_back_to_male_backup_and_is_remembered()
    {
        var (service, http, _) = Create(body: "MP3");
        http.Script.Enqueue((HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Invalid value for 'voice': 'cedar'.\"}}"));

        var audio = await service.SpeechAsync(Context, "m1", "Первый ответ.", 0);

        Assert.Equal("MP3"u8.ToArray(), audio.Audio);
        Assert.Equal(new[] { "cedar", "ash" }, http.Bodies.Select(b => JsonDocument.Parse(b).RootElement.GetProperty("voice").GetString()!).ToArray());

        await service.SpeechAsync(Context, "m2", "Второй ответ.", 0);
        Assert.Equal("ash", JsonDocument.Parse(http.Bodies[^1]).RootElement.GetProperty("voice").GetString()); // сразу запасной, без лишнего запроса
        Assert.Equal(3, http.Requests.Count);
    }

    [Fact]
    public void First_part_is_short_others_fit_the_model_and_nothing_is_lost()
    {
        var sentence = "Регион Хоразм выполнил план по кексу на сто двадцать процентов, а по бамбуку отстаёт. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 60)).Trim();

        var parts = SpeechText.Split(text);

        Assert.True(parts[0].Length <= SpeechText.FirstPartChars);
        Assert.All(parts.Skip(1), p => Assert.True(p.Length <= SpeechText.PartChars));
        Assert.Equal(text, string.Join(" ", parts));
    }

    [Fact]
    public void Very_long_sentence_is_cut_at_a_space()
    {
        var text = string.Join(" ", Enumerable.Repeat("слово", 1000));
        var parts = SpeechText.Split(text);
        Assert.All(parts, p => Assert.True(p.Length <= SpeechText.PartChars));
        Assert.All(parts, p => Assert.DoesNotContain("  ", p));
        Assert.Equal(1000, parts.Sum(p => p.Split(' ').Length));
    }

    private static (VoiceService Service, CapturingHttp Http, Records Usage) Create(HttpStatusCode status = HttpStatusCode.OK, string body = "{\"text\":\" Как продажи в Хоразме? \"}",
        bool withKey = true)
    {
        var http = new CapturingHttp(status, body);
        var usage = new Records();
        var settings = new Settings(withKey);
        var service = new VoiceService(settings, http, usage, new MemoryCache(new MemoryCacheOptions()), new VoiceOptions());
        return (service, http, usage);
    }

    private static readonly AiCallContext Context = new(Guid.NewGuid(), "consultant", null, "voice");

    [Fact]
    public async Task Transcribe_sends_recording_with_model_and_prompt()
    {
        var (service, http, usage) = Create();

        var text = await service.TranscribeAsync(Context, [1, 2, 3], "audio/webm;codecs=opus");

        Assert.Equal("Как продажи в Хоразме?", text);
        var request = http.LastRequest!;
        Assert.Equal("https://openai.test/v1/audio/transcriptions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        var form = http.Bodies[^1];
        Assert.Contains("filename=question.webm", form);
        Assert.Contains("gpt-4o-transcribe", form);
        Assert.Contains("Хоразм", form); // подсказка с терминами
        Assert.True(Assert.Single(usage.Items).Success);
    }

    [Fact]
    public async Task Silence_that_echoes_the_prompt_is_treated_as_no_speech()
    {
        var (service, _, _) = Create(body: "{\"text\":\"Кекс, Бамбук, Помадка, Трубочки.\"}");

        Assert.Equal("", await service.TranscribeAsync(Context, [1, 2, 3], "audio/webm"));
        Assert.False(VoiceService.IsPromptEcho("Как продажи Кекса в Хоразме?", new VoiceOptions().TranscribePrompt));
    }

    [Theory]
    [InlineData("video/x-matroska")]
    [InlineData(null)]
    public async Task Unknown_or_empty_recordings_are_rejected(string? type)
    {
        var (service, http, _) = Create();
        await Assert.ThrowsAsync<VoiceException>(() => service.TranscribeAsync(Context, [1], type));
        await Assert.ThrowsAsync<VoiceException>(() => service.TranscribeAsync(Context, [], "audio/webm"));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Speech_reads_parts_and_caches_audio()
    {
        var (service, http, _) = Create(body: "MP3");

        var first = await service.SpeechAsync(Context, "message-1", Answer, 0);
        var again = await service.SpeechAsync(Context, "message-1", Answer, 0);

        Assert.Equal("MP3"u8.ToArray(), first.Audio);
        Assert.Equal(first.Audio, again.Audio);
        Assert.Single(http.Requests); // повтор — из кэша, без нового запроса
        using var json = JsonDocument.Parse(http.Bodies[^1]);
        Assert.Equal("gpt-4o-mini-tts", json.RootElement.GetProperty("model").GetString());
        Assert.Equal("cedar", json.RootElement.GetProperty("voice").GetString()); // мужской, самый естественный голос OpenAI
        Assert.Equal("mp3", json.RootElement.GetProperty("response_format").GetString());
        Assert.StartsWith("Продажи за октябрь.", json.RootElement.GetProperty("input").GetString());
        Assert.True(json.RootElement.TryGetProperty("instructions", out _));
        await Assert.ThrowsAsync<VoiceException>(() => service.SpeechAsync(Context, "message-1", Answer, first.Parts));
    }

    [Fact]
    public async Task Provider_errors_are_readable_and_hide_the_key()
    {
        var (service, _, usage) = Create(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Incorrect API key provided: sk-test-key\"}}");

        var error = await Assert.ThrowsAsync<VoiceException>(() => service.SpeechAsync(Context, "m", "Тест.", 0));

        Assert.DoesNotContain("sk-test-key", error.Message);
        Assert.False(Assert.Single(usage.Items).Success);
    }

    [Fact]
    public async Task Without_openai_key_voice_is_not_ready()
    {
        var (service, http, _) = Create(withKey: false);
        Assert.False((await service.GetStatusAsync()).Ready);
        await Assert.ThrowsAsync<VoiceException>(() => service.TranscribeAsync(Context, [1], "audio/mp4"));
        Assert.Empty(http.Requests);
    }

    private sealed class Settings(bool withKey) : IAiSettingsSource
    {
        public Task<AiSettingsSnapshot> GetSettingsAsync(CancellationToken ct = default) =>
            Task.FromResult(new AiSettingsSnapshot(true, null, null, null, null, null, 2048, false));

        public Task<AiProviderState?> GetProviderAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<AiProviderState?>(new AiProviderState(code, code, "https://openai.test/v1", true, true, withKey ? "sk-test-key" : "",
                AiKeySource.OneBase, null));
    }

    /// <summary>Заглушка HTTP, которая сохраняет тело запроса в момент отправки (потом сервис его освобождает).</summary>
    private sealed class CapturingHttp(HttpStatusCode status, string body) : IHttpClientFactory
    {
        public HttpStatusCode Status { get; } = status;
        public string Body { get; } = body;
        /// <summary>Ответы по очереди; когда закончатся — Status и Body.</summary>
        public Queue<(HttpStatusCode Status, string Body)> Script { get; } = new();
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public HttpRequestMessage? LastRequest => Requests.LastOrDefault();

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(CapturingHttp owner) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.Requests.Add(request);
                owner.Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
                var (status, body) = owner.Script.Count > 0 ? owner.Script.Dequeue() : (owner.Status, owner.Body);
                return new HttpResponseMessage(status) { Content = new StringContent(body) };
            }
        }
    }

    private sealed class Records : IAiUsageRecorder
    {
        public List<AiCallRecord> Items { get; } = [];

        public Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default)
        {
            Items.Add(record);
            return Task.CompletedTask;
        }
    }
}

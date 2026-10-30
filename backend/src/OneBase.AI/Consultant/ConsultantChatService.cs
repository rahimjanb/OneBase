using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.AI.Consultant;

/// <summary>Событие чата для потоковой отдачи в браузер.</summary>
public sealed record ChatEvent(string Type, object Data);

/// <summary>Сообщение чата. Answer — структура готового ответа консультанта для экрана (блоки отчёта); Content — тот же ответ Markdown-ом.</summary>
public sealed record ChatMessageView(
    Guid Id,
    string Role,
    string Status,
    string Content,
    JsonElement? Details,
    string? Error,
    DateTimeOffset CreatedAt,
    long DurationMs,
    StructuredAnswer? Answer = null);

/// <summary>Чат в истории. Source — web или telegram (вопросы из Telegram-бота идут в чат консультанта пользователя).</summary>
public sealed record ConversationSummary(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset LastMessageAt, string Source = "web");

/// <summary>
/// Контекст вопроса с экрана: период (yyyy-MM) и отдел (код и название). Уходит консультанту строкой к вопросу — в истории чата вопрос
/// остаётся как написан.
/// </summary>
public sealed record ChatContext(string? Period, string? Department, string? DepartmentName);

public sealed record ConversationView(Guid Id, string Title, DateTimeOffset CreatedAt, IReadOnlyList<ChatMessageView> Messages);

/// <summary>Ответ консультанта целиком (для каналов без потоковой отдачи): Ok=false — Error с текстом для пользователя.</summary>
public sealed record ConsultantReply(Guid ConversationId, bool Ok, string Content, string? Error, ConsultantDetails? Details, AiTokenUsage Usage, long DurationMs);

/// <summary>Чаты консультанта: история, сохранение вопросов и ответов. Пользователь видит только свои чаты.</summary>
public sealed class ConsultantChatService(
    IAppDbContext db,
    IConsultantEngine engine,
    AiAuditService audit,
    ConsultantAnswerCache answers,
    IAuditLogger auditLog,
    ILogger<ConsultantChatService> logger)
{
    public const int MaxQuestionLength = 4000;

    /// <summary>Сколько последних сообщений чата передаётся модели как контекст.</summary>
    private const int HistoryMessages = 12;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Начало названия чата, начатого в Telegram (TitleOf при первом вопросе из канала).</summary>
    public const string TelegramTitlePrefix = "Telegram · ";

    /// <summary>
    /// История чатов пользователя (последние 200). query — поиск по названию и тексту сообщений. Source — telegram у чатов, начатых в
    /// Telegram или привязанных к переписке в Telegram, иначе web.
    /// </summary>
    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, CancellationToken ct, string? query = null)
    {
        var chats = db.AiConversations.AsNoTracking().Where(c => c.UserId == userId && c.ArchivedAt == null);
        if (query?.Trim().ToLowerInvariant() is { Length: > 0 } text)
        {
            text = text.Length > 200 ? text[..200] : text;
            chats = chats.Where(c => c.Title.ToLower().Contains(text) || db.AiMessages.Any(m => m.ConversationId == c.Id && m.Content.ToLower().Contains(text)));
        }

        return await chats
            .OrderByDescending(c => c.LastMessageAt)
            .Take(200)
            .Select(c => new ConversationSummary(c.Id, c.Title, c.CreatedAt, c.LastMessageAt,
                c.Title.StartsWith(TelegramTitlePrefix) || db.TelegramUserPermissions.Any(p => p.ConversationId == c.Id) ? "telegram" : "web"))
            .ToListAsync(ct);
    }

    public async Task<ConversationView?> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var conversation = await db.AiConversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct);
        if (conversation is null)
        {
            return null;
        }

        var messages = await db.AiMessages.AsNoTracking()
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
        return new ConversationView(conversation.Id, conversation.Title, conversation.CreatedAt, messages.Select(View).ToList());
    }

    /// <summary>
    /// Текст ответа консультанта для озвучки — только из своего чата и только готовый ответ. У отчёта — «Главное» и рекомендации, у простого
    /// ответа — весь текст.
    /// </summary>
    public async Task<string?> AnswerTextAsync(Guid userId, Guid messageId, CancellationToken ct)
    {
        var message = await db.AiMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Role == AiMessageRole.Assistant && m.Status == AiMessageStatus.Completed
                                      && m.Conversation!.UserId == userId, ct);
        return message is null ? null : Structure(message) is { } answer ? AnswerStructure.SpeechText(answer, message.Content) : message.Content;
    }

    /// <summary>Оценка ответа (up/down) и «что не так» — в журнал аудита (ai.answer.feedback). Только по своему готовому ответу.</summary>
    public async Task<bool> FeedbackAsync(Guid userId, Guid messageId, string rating, string? comment, CancellationToken ct)
    {
        var conversationId = await db.AiMessages.AsNoTracking()
            .Where(m => m.Id == messageId && m.Role == AiMessageRole.Assistant && m.Conversation!.UserId == userId)
            .Select(m => (Guid?)m.ConversationId)
            .FirstOrDefaultAsync(ct);
        if (conversationId is null)
        {
            return false;
        }

        var text = comment?.Trim();
        await auditLog.LogAsync(ActorType.User, userId.ToString(), "ai.answer.feedback", "ai_message", messageId.ToString(),
            new { conversationId, rating, comment = string.IsNullOrEmpty(text) ? null : text.Length > 1000 ? text[..1000] : text }, ct);
        return true;
    }

    public async Task<bool> RenameAsync(Guid userId, Guid id, string title, CancellationToken ct)
    {
        var conversation = await db.AiConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct);
        if (conversation is null)
        {
            return false;
        }

        conversation.Title = TitleOf(title);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Убирает чат из истории. Сообщения остаются в базе для журнала AI.</summary>
    public async Task<bool> ArchiveAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var conversation = await db.AiConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct);
        if (conversation is null)
        {
            return false;
        }

        conversation.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Вопрос в чат: сохраняет вопрос, получает ответ консультанта и сохраняет его (или ошибку).
    /// События: start → progress… → done | error. KeyNotFoundException — чата нет или он чужой.
    /// </summary>
    public Task SendAsync(Guid userId, Guid? conversationId, string question, Func<ChatEvent, Task> emit, CancellationToken ct, ChatContext? context = null) =>
        RunAsync(userId, conversationId, question, null, emit, ct, context);

    /// <summary>
    /// Вопрос из другого канала (Telegram) от имени пользователя OneBase — с его правами, в его чате консультанта (виден и в OneBase).
    /// Ответ — целиком, без потоковых событий. KeyNotFoundException — чата нет, он чужой или в архиве.
    /// </summary>
    public async Task<ConsultantReply> AskAsync(Guid userId, Guid? conversationId, string question, ConsultantChannel channel, CancellationToken ct) =>
        await RunAsync(userId, conversationId, question, channel, _ => Task.CompletedTask, ct);

    private async Task<ConsultantReply> RunAsync(Guid userId, Guid? conversationId, string question, ConsultantChannel? channel, Func<ChatEvent, Task> emit,
        CancellationToken ct, ChatContext? context = null)
    {
        var timings = AiTurnTimings.Start();
        var prepare = timings.Stage("prepare");
        question = question.Trim();
        // Консультанту (маршрутизация, AI-сотрудники, кэш ответов, журнал) — вопрос с контекстом экрана; в чате — как написан.
        var asked = WithContext(question, context);
        AiConversation conversation;
        if (conversationId is { } id)
        {
            conversation = await db.AiConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct)
                ?? throw new KeyNotFoundException("Чат не найден.");
        }
        else
        {
            conversation = new AiConversation { UserId = userId, Title = channel is null ? TitleOf(question) : TitleOf($"Telegram · {question}") };
            db.AiConversations.Add(conversation);
        }

        var history = await HistoryAsync(conversation.Id, ct);
        // Последний ответ консультанта: если вопрос его оспаривает, проверка в журнале ссылается на него.
        var lastAnswerId = await db.AiMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id && m.Role == AiMessageRole.Assistant && m.Status == AiMessageStatus.Completed)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);

        var userMessage = new AiMessage { ConversationId = conversation.Id, Role = AiMessageRole.User, Content = question };
        db.AiMessages.Add(userMessage);
        conversation.LastMessageAt = userMessage.CreatedAt;
        await db.SaveChangesAsync(ct);

        prepare.Dispose();
        await emit(new ChatEvent("start", new { conversationId = conversation.Id, title = conversation.Title, userMessage = View(userMessage) }));

        var watch = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;
        ConsultantDetails? details = null;
        var fromCache = false;
        var reply = new AiMessage { ConversationId = conversation.Id, Role = AiMessageRole.Assistant, Content = string.Empty };
        try
        {
            // Тот же первый вопрос того же пользователя, а данные с тех пор не обновлялись, — ответ из кэша, без вызовов модели.
            string? cacheKey;
            CachedConsultantAnswer? cached = null;
            using (timings.Stage("answer.cache"))
            {
                cacheKey = await answers.KeyAsync(userId, asked, history.Count, channel, ct);
                if (cacheKey is not null)
                {
                    cached = await answers.GetAsync(cacheKey, ct);
                }
            }

            if (cached is not null)
            {
                reply.Content = cached.Content;
                reply.Details = cached.Details;
                details = JsonSerializer.Deserialize<ConsultantDetails>(cached.Details, Json);
                fromCache = true;
            }
            else
            {
                // Черновик итогового ответа — потоком только в чат OneBase (событие draft); другие каналы получают ответ целиком.
                var draft = channel is null ? new DraftEvents(emit, timings) : null;
                var answer = await engine.AnswerAsync(
                    new ConsultantTurn(userId, conversation.Id, asked, history, channel, lastAnswerId, draft is null ? null : draft.OnDraftAsync),
                    p => emit(new ChatEvent("progress", p)),
                    ct);
                reply.Content = answer.Content.Length > 0 ? answer.Content : "Модель вернула пустой ответ.";
                reply.Details = JsonSerializer.Serialize(answer.Details, Json);
                details = answer.Details;
                // Токены сообщения — весь промпт (вход, включая кэш промпта) и выход.
                reply.InputTokens = answer.Usage.PromptTokens;
                reply.OutputTokens = answer.Usage.OutputTokens;
                if (cacheKey is not null && answer.Content.Length > 0)
                {
                    await answers.StoreAsync(cacheKey, reply.Content, answer.Details, reply.Details, ct);
                }
            }
        }
        catch (Exception ex) when (ex is LlmNotConfiguredException or AiProviderException)
        {
            reply.Status = AiMessageStatus.Failed;
            reply.Error = Trim(ex.Message);
        }
        catch (OperationCanceledException)
        {
            reply.Status = AiMessageStatus.Failed;
            reply.Error = "Ответ не получен: время ожидания истекло или сервер останавливается.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Консультант: ошибка ответа в чате {ConversationId}", conversation.Id);
            reply.Status = AiMessageStatus.Failed;
            reply.Error = "Не удалось получить ответ из-за внутренней ошибки OneBase. Подробности — в журнале сервера.";
        }

        reply.DurationMs = watch.ElapsedMilliseconds;
        reply.CreatedAt = DateTimeOffset.UtcNow;
        using (timings.Stage("save"))
        {
            db.AiMessages.Add(reply);
            conversation.LastMessageAt = reply.CreatedAt;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var view = timings.View();
        var auditWatch = Stopwatch.StartNew();
        try
        {
            await audit.WriteAsync(channel?.Name ?? "consultant", userId, conversation.Id, reply.Id, asked, startedAt, reply.DurationMs, details,
                reply.Status == AiMessageStatus.Completed ? reply.Content : null, reply.Error, CancellationToken.None, fromCache, view, timings);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Консультант: не удалось записать AI-журнал для чата {ConversationId}", conversation.Id);
        }

        logger.LogInformation("Консультант: ответ {MessageId} за {Ms} мс ({Stages}; запись журнала {AuditMs} мс)", reply.Id, view.TotalMs,
            AiTimingsText.Summary(view), auditWatch.ElapsedMilliseconds);

        await emit(reply.Status == AiMessageStatus.Completed
            ? new ChatEvent("done", new { message = View(reply) })
            : new ChatEvent("error", new { message = View(reply), error = reply.Error }));
        return new ConsultantReply(conversation.Id, reply.Status == AiMessageStatus.Completed, reply.Content, reply.Error, details,
            new AiTokenUsage(reply.InputTokens, reply.OutputTokens), reply.DurationMs);
    }

    /// <summary>
    /// Черновик ответа в поток чата: мелкие фрагменты модели собираются в куски (не чаще DraftInterval или от DraftChars символов), служебные
    /// события черновика (сброс, проверка, исправление) уходят сразу, перед ними — накопленный текст (при сбросе — выбрасывается).
    /// </summary>
    private sealed class DraftEvents(Func<ChatEvent, Task> emit, AiTurnTimings timings)
    {
        private const int DraftChars = 48;
        private static readonly TimeSpan DraftInterval = TimeSpan.FromMilliseconds(60);

        private readonly StringBuilder _pending = new();
        private readonly Stopwatch _sinceSent = new();

        public async Task OnDraftAsync(ConsultantDraft draft)
        {
            if (draft.Kind == ConsultantDraft.TextKind)
            {
                _pending.Append(draft.Text);
                if (_pending.Length >= DraftChars || !_sinceSent.IsRunning || _sinceSent.Elapsed >= DraftInterval)
                {
                    await FlushAsync();
                }

                return;
            }

            if (draft.Kind is ConsultantDraft.Reset or ConsultantDraft.Revising)
            {
                _pending.Clear();
            }
            else
            {
                await FlushAsync();
            }

            await emit(new ChatEvent("draft", new { kind = draft.Kind }));
        }

        private async Task FlushAsync()
        {
            if (_pending.Length == 0)
            {
                return;
            }

            timings.Text();
            var text = _pending.ToString();
            _pending.Clear();
            _sinceSent.Restart();
            await emit(new ChatEvent("draft", new { kind = ConsultantDraft.TextKind, text }));
        }
    }

    private async Task<IReadOnlyList<LlmMessage>> HistoryAsync(Guid conversationId, CancellationToken ct)
    {
        var recent = await db.AiMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Status == AiMessageStatus.Completed)
            .OrderByDescending(m => m.CreatedAt)
            .Take(HistoryMessages)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync(ct);
        recent.Reverse();

        // Модели ждут, что история начинается с вопроса пользователя.
        var start = recent.FindIndex(m => m.Role == AiMessageRole.User);
        return start < 0
            ? []
            : recent.Skip(start).Select(m => new LlmMessage(m.Role == AiMessageRole.User ? LlmRole.User : LlmRole.Assistant, m.Content)).ToList();
    }

    private ChatMessageView View(AiMessage m) => new(
        m.Id,
        m.Role == AiMessageRole.User ? "user" : "assistant",
        m.Status == AiMessageStatus.Completed ? "completed" : "failed",
        m.Content,
        m.Details is null ? null : JsonDocument.Parse(m.Details).RootElement.Clone(),
        m.Error,
        m.CreatedAt,
        m.DurationMs,
        m.Role == AiMessageRole.Assistant && m.Status == AiMessageStatus.Completed ? Structure(m) : null);

    /// <summary>Структура ответа для экрана; сбой разбора не мешает показать ответ Markdown-ом.</summary>
    private StructuredAnswer? Structure(AiMessage m)
    {
        try
        {
            var details = m.Details is null ? null : JsonSerializer.Deserialize<ConsultantDetails>(m.Details, Json);
            return AnswerStructure.Build(m.Content, details, m.DurationMs, m.CreatedAt);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or RegexMatchTimeoutException or FormatException or OverflowException)
        {
            logger.LogWarning(ex, "Консультант: не удалось разобрать ответ {MessageId} на блоки", m.Id);
            return null;
        }
    }

    /// <summary>Вопрос с контекстом экрана: «(Контекст экрана, если в вопросе не сказано иное: период — октябрь 2026; отдел — Продажи.)».</summary>
    internal static string WithContext(string question, ChatContext? context)
    {
        var parts = new List<string>();
        if (context?.Period is { } period && DateOnly.TryParseExact($"{period}-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var month))
        {
            parts.Add($"период — {MonthNames[month.Month - 1]} {month.Year} (01.{month.Month:00}.{month.Year} — {DateTime.DaysInMonth(month.Year, month.Month)}.{month.Month:00}.{month.Year})");
        }

        if ((context?.DepartmentName ?? context?.Department)?.Trim() is { Length: > 0 and <= 60 } department)
        {
            parts.Add($"отдел — {department}");
        }

        return parts.Count == 0 ? question : $"{question}\n\n(Контекст экрана, если в вопросе не сказано иное: {string.Join("; ", parts)}.)";
    }

    private static readonly string[] MonthNames =
        ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    private static string TitleOf(string text)
    {
        var line = text.Trim().Split('\n', 2)[0].Trim();
        if (line.Length == 0)
        {
            return "Новый чат";
        }

        return line.Length <= 80 ? line : line[..80].TrimEnd() + "…";
    }

    private static string Trim(string text) => text.Length <= 1000 ? text : text[..1000];
}

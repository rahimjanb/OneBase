using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram;

/// <summary>Правила модуля Telegram: коды привязки, ошибки Telegram, повторы, итог рассылки.</summary>
public static partial class TelegramRules
{
    /// <summary>Код привязки действует 15 минут.</summary>
    public static readonly TimeSpan LinkCodeLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Попыток отправки одному получателю (временные ошибки), паузы между ними.</summary>
    public const int MaxAttempts = 3;

    public static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];

    /// <summary>«Отправляется» дольше этого после перезапуска — неизвестно, ушло ли: в ошибку, без автоповтора (без дубля).</summary>
    public static readonly TimeSpan StaleSending = TimeSpan.FromMinutes(10);

    /// <summary>Длина сообщения Telegram и подписи к файлу.</summary>
    public const int MaxMessageLength = 4096;
    public const int MaxCaptionLength = 1024;

    /// <summary>Файл рассылки — не больше (Telegram принимает до 2 ГБ, но рассылка — не файлообменник).</summary>
    public const long MaxAttachmentBytes = 50L * 1024 * 1024;

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Новый код привязки: «OB-» и 8 символов без похожих (0/O, 1/I).</summary>
    public static string NewLinkCode()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder("OB-");
        foreach (var b in bytes)
        {
            sb.Append(CodeAlphabet[b % CodeAlphabet.Length]);
        }

        return sb.ToString();
    }

    /// <summary>Хэш кода (в базе хранится только он).</summary>
    public static string HashLinkCode(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

    /// <summary>Код привязки в тексте сообщения.</summary>
    public static string? FindLinkCode(string? text) => text is null ? null : LinkCode().Match(text) is { Success: true } m ? m.Value.ToUpperInvariant() : null;

    [GeneratedRegex(@"\bOB-[A-Z0-9]{8}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkCode();

    /// <summary>Ошибка Telegram (RPC) по коду и тексту → вид ошибки и понятный текст.</summary>
    public static TelegramException Classify(int code, string rpc, int waitSeconds)
    {
        var name = rpc.ToUpperInvariant();
        if (code == 420 || name.StartsWith("FLOOD_WAIT", StringComparison.Ordinal) || name.StartsWith("SLOWMODE_WAIT", StringComparison.Ordinal))
        {
            var wait = Math.Max(waitSeconds, 1);
            return new TelegramException($"Telegram ограничил частоту: подождать {wait} с.", TelegramErrorKind.Flood, wait);
        }

        return name switch
        {
            "PEER_FLOOD" => new("Telegram ограничил аккаунт за сообщения незнакомым людям (PEER_FLOOD) — отправка остановлена.", TelegramErrorKind.Permanent),
            "USER_IS_BLOCKED" or "YOU_BLOCKED_USER" => new("Получатель заблокировал аккаунт (или аккаунт — его).", TelegramErrorKind.Permanent),
            "USER_PRIVACY_RESTRICTED" => new("Получатель запретил сообщения настройками приватности.", TelegramErrorKind.Permanent),
            "CHAT_WRITE_FORBIDDEN" or "CHAT_SEND_PLAIN_FORBIDDEN" or "CHAT_SEND_MEDIA_FORBIDDEN" or "CHAT_SEND_DOCS_FORBIDDEN" or "CHAT_RESTRICTED"
                or "CHAT_ADMIN_REQUIRED" => new("В этот чат аккаунту писать нельзя.", TelegramErrorKind.Permanent),
            "USER_BANNED_IN_CHANNEL" or "CHANNEL_PRIVATE" or "CHANNEL_INVALID" or "CHAT_ID_INVALID" or "PEER_ID_INVALID" or "USER_KICKED"
                => new("Чат недоступен аккаунту (удалён, приватный или аккаунт исключён).", TelegramErrorKind.Permanent),
            "USER_DEACTIVATED" or "INPUT_USER_DEACTIVATED" => new("Аккаунт получателя удалён.", TelegramErrorKind.Permanent),
            "MESSAGE_TOO_LONG" or "MEDIA_CAPTION_TOO_LONG" => new("Сообщение длиннее, чем принимает Telegram.", TelegramErrorKind.Permanent),
            "PHONE_CODE_INVALID" => new("Неверный код — проверьте и введите ещё раз.", TelegramErrorKind.Auth),
            "PHONE_CODE_EXPIRED" => new("Код истёк — нажмите «Подключить» ещё раз.", TelegramErrorKind.Auth),
            "PASSWORD_HASH_INVALID" => new("Неверный пароль двухэтапной защиты.", TelegramErrorKind.Auth),
            "PHONE_NUMBER_INVALID" => new("Неверный номер телефона.", TelegramErrorKind.Auth),
            "PHONE_NUMBER_BANNED" or "USER_DEACTIVATED_BAN" => new("Номер заблокирован в Telegram.", TelegramErrorKind.Auth),
            "AUTH_KEY_UNREGISTERED" or "SESSION_REVOKED" or "SESSION_EXPIRED" or "AUTH_KEY_DUPLICATED"
                => new("Сессия Telegram недействительна (отозвана или вход с другого места) — переподключите аккаунт.", TelegramErrorKind.Auth),
            "API_ID_INVALID" or "API_ID_PUBLISHED_FLOOD" => new("Telegram не принял api_id/api_hash — проверьте их в «Настройки → Telegram».", TelegramErrorKind.Config),
            _ when code >= 500 || code == -503 => new($"Telegram временно недоступен ({rpc}).", TelegramErrorKind.Transient),
            _ => new($"Telegram отклонил запрос: {rpc}.", TelegramErrorKind.Permanent),
        };
    }

    /// <summary>Пауза перед следующей попыткой (attempt — сколько уже было, с 1).</summary>
    public static TimeSpan RetryDelay(int attempt) => RetryDelays[Math.Clamp(attempt - 1, 0, RetryDelays.Length - 1)];

    /// <summary>Итог рассылки по статусам получателей: null — ещё идёт.</summary>
    public static TelegramCampaignStatus? Outcome(IReadOnlyCollection<TelegramRecipientStatus> statuses)
    {
        if (statuses.Any(s => s is TelegramRecipientStatus.Pending or TelegramRecipientStatus.Sending))
        {
            return null;
        }

        if (statuses.Count == 0 || statuses.All(s => s == TelegramRecipientStatus.Cancelled))
        {
            return TelegramCampaignStatus.Cancelled;
        }

        return statuses.Any(s => s == TelegramRecipientStatus.Failed) ? TelegramCampaignStatus.CompletedWithErrors : TelegramCampaignStatus.Completed;
    }

    /// <summary>
    /// Markdown ответа (как в чате OneBase) → HTML Telegram: **жирный**, *курсив*, `код`, [ссылка](url), заголовки — жирным, таблицы —
    /// моноширинным блоком (Telegram таблиц не рисует). Остальной текст экранируется.
    /// </summary>
    public static string MarkdownToTelegramHtml(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        var table = new List<string>();
        void FlushTable()
        {
            if (table.Count == 0)
            {
                return;
            }

            var rows = table.Where(r => !TableSeparator().IsMatch(r))
                .Select(r => r.Trim().Trim('|').Split('|').Select(c => Plain(c.Trim())).ToList())
                .ToList();
            var widths = Enumerable.Range(0, rows.Max(r => r.Count)).Select(i => rows.Max(r => i < r.Count ? r[i].Length : 0)).ToList();
            sb.Append("<pre>");
            foreach (var r in rows)
            {
                sb.Append(Escape(string.Join("  ", r.Select((c, i) => c.PadRight(widths[i]))).TrimEnd())).Append('\n');
            }

            sb.Length--;
            sb.Append("</pre>\n");
            table.Clear();
        }

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith('|'))
            {
                table.Add(line);
                continue;
            }

            FlushTable();
            var heading = Heading().Match(line);
            sb.Append(heading.Success ? $"<b>{Inline(heading.Groups[1].Value)}</b>" : Inline(Bullet().Replace(line, "• "))).Append('\n');
        }

        FlushTable();
        return sb.ToString().TrimEnd();
    }

    private static string Inline(string text)
    {
        var html = Escape(text);
        html = Link().Replace(html, m => $"<a href=\"{m.Groups[2].Value.Replace("\"", "%22")}\">{m.Groups[1].Value}</a>");
        html = Bold().Replace(html, "<b>$1</b>");
        html = Italic().Replace(html, "<i>$1</i>");
        html = Code().Replace(html, "<code>$1</code>");
        return html;
    }

    /// <summary>Экранирование для HTML Telegram: только &amp;, &lt;, &gt; (кириллица и «ёлочки» — как есть).</summary>
    public static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Plain(string text) => Bold().Replace(text, "$1").Replace("`", string.Empty);

    [GeneratedRegex(@"^\s*\|?\s*:?-{2,}")]
    private static partial Regex TableSeparator();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(.+)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(\s*)[-*]\s+")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"\[([^\]]+)\]\((https?://[^\s)]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"(?<![\*\w])\*(?!\s)(.+?)(?<!\s)\*(?![\*\w])")]
    private static partial Regex Italic();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex Code();

    /// <summary>Текст длиннее сообщения Telegram — на части по абзацам (без разрыва HTML-тегов внутри строки).</summary>
    public static IReadOnlyList<string> Split(string text, int max = MaxMessageLength)
    {
        if (text.Length <= max)
        {
            return [text];
        }

        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            if (current.Length + line.Length + 1 > max && current.Length > 0)
            {
                parts.Add(current.ToString().TrimEnd());
                current.Clear();
            }

            current.Append(line.Length > max ? line[..max] : line).Append('\n');
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString().TrimEnd());
        }

        return parts;
    }
}

/// <summary>
/// Материалы чата для режима «Только отправленные данные»: текст режется на фрагменты при сохранении, поиск идёт только по фрагментам
/// своего аккаунта и чата (условие в запросе к базе) — общего индекса и чужих чатов здесь нет.
/// </summary>
public static class TelegramMaterials
{
    public const int ChunkLength = 1500;

    public static IReadOnlyList<string> Chunks(string text)
    {
        var clean = text.Replace("\r\n", "\n").Trim();
        var chunks = new List<string>();
        var current = new StringBuilder();
        foreach (var paragraph in clean.Split("\n\n"))
        {
            if (current.Length + paragraph.Length > ChunkLength && current.Length > 0)
            {
                chunks.Add(current.ToString().Trim());
                current.Clear();
            }

            for (var i = 0; i < paragraph.Length; i += ChunkLength)
            {
                var piece = paragraph.Substring(i, Math.Min(ChunkLength, paragraph.Length - i));
                if (current.Length + piece.Length > ChunkLength && current.Length > 0)
                {
                    chunks.Add(current.ToString().Trim());
                    current.Clear();
                }

                current.Append(piece).Append("\n\n");
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString().Trim());
        }

        return chunks.Where(c => c.Length > 0).ToList();
    }

    /// <summary>
    /// Фрагменты материалов этого аккаунта и чата, подходящие к вопросу (совпадение основ слов); совпадений нет — последние фрагменты:
    /// модель сама решит, есть ли в них ответ. Других чатов и общего индекса здесь нет.
    /// </summary>
    public static async Task<IReadOnlyList<(TelegramMaterialChunk Chunk, string Title, DateTimeOffset SentAt)>> FindAsync(IAppDbContext db, Guid accountId, long peerId, string question,
        int top, CancellationToken ct)
    {
        var chunks = await db.TelegramMaterialChunks.AsNoTracking()
            .Where(c => c.AccountId == accountId && c.PeerId == peerId)
            .OrderByDescending(c => c.Id)
            .Take(3000)
            .ToListAsync(ct);
        if (chunks.Count == 0)
        {
            return [];
        }

        var ids = chunks.Select(c => c.MaterialId).Distinct().ToList();
        var titles = await db.TelegramMaterials.AsNoTracking()
            .Where(m => ids.Contains(m.Id) && m.AccountId == accountId && m.PeerId == peerId)
            .ToDictionaryAsync(m => m.Id, m => new { m.Title, m.CreatedAt }, ct);
        var words = Stems(question);
        var scored = chunks.Where(c => titles.ContainsKey(c.MaterialId))
            .Select(c => (Chunk: c, Score: words.Count == 0 ? 0 : Stems(c.Text).Count(words.Contains)))
            .ToList();
        var hits = scored.Where(s => s.Score > 0).OrderByDescending(s => s.Score).ThenByDescending(s => s.Chunk.Id).Take(top).ToList();
        if (hits.Count == 0)
        {
            hits = scored.Take(top).ToList();
        }

        return hits.Select(h => (h.Chunk, titles[h.Chunk.MaterialId].Title, titles[h.Chunk.MaterialId].CreatedAt)).ToList();
    }

    private static HashSet<string> Stems(string text) =>
        Regex.Split(text.ToLowerInvariant().Replace('ё', 'е'), @"[^\p{L}\p{N}]+")
            .Where(w => w.Length >= 3)
            .Select(w => w.Length > 6 ? w[..6] : w)
            .ToHashSet();
}

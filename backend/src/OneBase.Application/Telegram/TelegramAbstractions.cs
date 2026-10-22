using System.Threading.Channels;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram;

public enum TelegramErrorKind
{
    /// <summary>Не заданы api_id и api_hash или аккаунт не подключён.</summary>
    Config,

    /// <summary>Неверный или просроченный код, неверный пароль, сессия отозвана.</summary>
    Auth,

    /// <summary>FLOOD_WAIT: Telegram просит подождать WaitSeconds.</summary>
    Flood,

    /// <summary>Сеть, таймаут, внутренняя ошибка Telegram — можно повторить позже.</summary>
    Transient,

    /// <summary>Повтор не поможет: получатель заблокировал, нет прав писать, чат недоступен, аккаунт ограничен за спам.</summary>
    Permanent,
}

/// <summary>Ошибка Telegram — текст для администратора (без кодов входа, паролей и содержимого сообщений).</summary>
public sealed class TelegramException(string message, TelegramErrorKind kind, int? waitSeconds = null) : Exception(message)
{
    public TelegramErrorKind Kind { get; } = kind;
    public int? WaitSeconds { get; } = waitSeconds;
}

public sealed record TelegramApiCredentials(int ApiId, string ApiHash);

public enum TelegramLoginStep
{
    CodeRequired,
    PasswordRequired,
    Done,
}

public sealed record TelegramMe(long UserId, string? Username, string? DisplayName);

public sealed record TelegramLoginResult(TelegramLoginStep Step, TelegramMe? Me);

/// <summary>Куда писать: ID чата, тип, супергруппа/канал (InputPeerChannel) и access_hash, без которого Telegram не примет отправку.</summary>
public sealed record TelegramPeerRef(long PeerId, TelegramChatType Type, bool IsChannelPeer, long AccessHash);

/// <summary>Чат из диалогов аккаунта; CanSend=false — Reason объясняет почему (для интерфейса).</summary>
public sealed record TelegramDialog(TelegramPeerRef Peer, string Title, string? Username, bool CanSend, string? Reason = null);

/// <summary>Пользователь Telegram, найденный по username (ID и имя — от Telegram).</summary>
public sealed record TelegramResolvedUser(long UserId, string Username, string DisplayName, bool IsBot);

/// <summary>Состояние клиента аккаунта в памяти: подключён ли и когда Telegram в последний раз присылал события (приём сообщений жив).</summary>
public sealed record TelegramLiveStatus(bool Online, long Events, DateTimeOffset? LastEventAt, long Messages, DateTimeOffset? LastMessageAt);

/// <summary>Файл сообщения. Photo — картинкой в ленте (страницы автоотчёта; несколько — альбомом), а не документом.</summary>
public sealed record TelegramFile(string FileName, string ContentType, byte[] Content, bool Photo = false);

/// <summary>Исходящее сообщение: текст (Html — разметка Telegram HTML), файлы, ответ на сообщение.</summary>
public sealed record TelegramOutgoing(string? Text, bool Html, IReadOnlyList<TelegramFile> Files, int? ReplyToMessageId = null);

public sealed record TelegramSent(IReadOnlyList<int> MessageIds);

/// <summary>Вложение входящего сообщения (документ): скачивается по запросу агента, если тип и размер разрешены.</summary>
public sealed record TelegramIncomingFile(string FileName, string? MimeType, long Size);

/// <summary>
/// Входящее сообщение аккаунта. Outgoing — отправлено самим аккаунтом (агент такие не обрабатывает); MentionsMe — в группе к аккаунту
/// обратились (упоминание или ответ на его сообщение).
/// </summary>
public sealed record TelegramIncoming(
    Guid AccountId,
    TelegramPeerRef Chat,
    string ChatTitle,
    int MessageId,
    long? FromUserId,
    string? FromUsername,
    string? FromName,
    string Text,
    DateTimeOffset Date,
    bool Outgoing,
    bool MentionsMe,
    TelegramIncomingFile? File = null,
    bool FromBot = false);

/// <summary>
/// Пользовательский аккаунт Telegram через MTProto (реализация — Infrastructure, WTelegramClient; в тестах — подставная). Сессия хранится
/// зашифрованной в базе и наружу не отдаётся. Ошибки — TelegramException.
/// </summary>
public interface ITelegramGateway
{
    /// <summary>Начать вход по номеру: Telegram присылает код (в приложение или SMS). Прежняя сессия аккаунта сбрасывается.</summary>
    Task<TelegramLoginResult> StartLoginAsync(Guid accountId, string phone, CancellationToken ct = default);

    Task<TelegramLoginResult> SubmitCodeAsync(Guid accountId, string code, CancellationToken ct = default);

    Task<TelegramLoginResult> SubmitPasswordAsync(Guid accountId, string password, CancellationToken ct = default);

    /// <summary>Подключиться по сохранённой сессии (если ещё не подключён) и вернуть, кто мы.</summary>
    Task<TelegramMe> CheckAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Выйти из сессии в Telegram и забыть её.</summary>
    Task LogoutAsync(Guid accountId, CancellationToken ct = default);

    Task<IReadOnlyList<TelegramDialog>> DialogsAsync(Guid accountId, CancellationToken ct = default);

    Task<TelegramSent> SendAsync(Guid accountId, TelegramPeerRef peer, TelegramOutgoing message, CancellationToken ct = default);

    /// <summary>Скачать вложение входящего сообщения (null — уже недоступно).</summary>
    Task<byte[]?> DownloadAsync(Guid accountId, long peerId, int messageId, CancellationToken ct = default);

    /// <summary>Найти пользователя Telegram по username (null — такого нет или это не пользователь, а канал/группа).</summary>
    Task<TelegramResolvedUser?> ResolveUsernameAsync(Guid accountId, string username, CancellationToken ct = default);

    /// <summary>Состояние клиента в памяти (null — клиент не поднят).</summary>
    TelegramLiveStatus? LiveStatus(Guid accountId);

    /// <summary>«Печатает…» в чате (Telegram показывает около 5 секунд) — пока агент готовит ответ.</summary>
    Task TypingAsync(Guid accountId, TelegramPeerRef peer, CancellationToken ct = default);
}

/// <summary>Очередь входящих сообщений: клиент Telegram кладёт, агент забирает. Ограничена — при перегрузке старые не теряются в памяти бесконечно.</summary>
public sealed class TelegramInbox
{
    private readonly Channel<TelegramIncoming> _channel = Channel.CreateBounded<TelegramIncoming>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    public bool Post(TelegramIncoming message) => _channel.Writer.TryWrite(message);

    public ChannelReader<TelegramIncoming> Reader => _channel.Reader;
}

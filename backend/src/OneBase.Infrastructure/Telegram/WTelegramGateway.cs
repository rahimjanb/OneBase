using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.Application.Telegram;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using TL;
using WTelegram;

namespace OneBase.Infrastructure.Telegram;

/// <summary>
/// Пользовательские аккаунты Telegram через MTProto (WTelegramClient). На аккаунт — один клиент в памяти (вход, приём входящих,
/// отправка). Сессия — в базе, зашифрованная Data Protection (поверх шифрования самой библиотеки); коды и пароль не сохраняются.
/// Входящие — через UpdateManager (порядок, без пропусков; его состояние тоже в базе) → TelegramInbox. Ошибки RPC → TelegramException.
/// Рассчитано на один экземпляр backend (как развёрнут OneBase): две копии с одной сессией Telegram разорвёт.
/// </summary>
internal sealed class WTelegramGateway(
    IServiceScopeFactory scopes,
    TelegramCredentialsStore credentials,
    IDataProtectionProvider protection,
    TelegramInbox inbox,
    ILogger<WTelegramGateway> logger) : ITelegramGateway, IAsyncDisposable
{
    private static readonly JsonSerializerOptions StateJson = new() { IncludeFields = true };

    private readonly IDataProtector _protector = protection.CreateProtector("OneBase.Telegram.Session");
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();
    private readonly bool _logHooked = HookLog(logger);

    /// <summary>Журнал библиотеки — только предупреждения и ошибки (уровень ≥ 3); отладочный поток MTProto не пишем.</summary>
    private static bool HookLog(ILogger logger)
    {
        Helpers.Log = (level, text) =>
        {
            if (level >= 3)
            {
                logger.LogWarning("WTelegram: {Text}", text);
            }
        };
        return true;
    }

    /// <summary>Идёт ли интерактивный вход (номер → код → пароль): только тогда обработчик настроек отдаёт эти шаги методу Login().</summary>
    private sealed class LoginState
    {
        public volatile bool Interactive;
    }

    private sealed class Session(Guid accountId, Client client, SessionStore store, LoginState login)
    {
        public Guid AccountId { get; } = accountId;
        public Client Client { get; } = client;
        public SessionStore Store { get; } = store;
        public LoginState Login { get; } = login;
        public UpdateManager? Manager { get; set; }
        public ConcurrentDictionary<(long Peer, int Message), Document> Documents { get; } = new();
        public DateTimeOffset LastActivitySaved { get; set; }

        // Счётчики событий от Telegram — видно, что приём живой (статусы, прочтения, сообщения).
        public long Events;
        public long Messages;
        public DateTimeOffset? LastEventAt { get; set; }
        public DateTimeOffset? LastMessageAt { get; set; }

        /// <summary>1 — сохранение состояния приёма уже запланировано (сохраняем не чаще раза в несколько секунд).</summary>
        public int StateSaveScheduled;
    }

    /// <summary>Сколько ждать ответа Telegram на действие из настроек (проверка, поиск username, чаты): дальше — понятная ошибка.</summary>
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Отправка с файлами может идти дольше (загрузка до 50 МБ).</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromMinutes(3);

    /// <summary>Ждать ответа Telegram не дольше limit: зависший запрос не держит интерфейс и очередь (сам запрос библиотека доведёт или бросит).</summary>
    private static async Task<T> Limited<T>(Task<T> call, TimeSpan limit)
    {
        try
        {
            return await call.WaitAsync(limit);
        }
        catch (TimeoutException)
        {
            throw new TelegramException($"Telegram не ответил за {limit.TotalSeconds:0} с — попробуйте ещё раз; если повторяется, нажмите «Проверить».",
                TelegramErrorKind.Transient);
        }
    }

    // ---------- Вход ----------

    public async Task<TelegramLoginResult> StartLoginAsync(Guid accountId, string phone, CancellationToken ct = default) =>
        await LockedAsync(accountId, async () =>
        {
            await DropAsync(accountId);
            var session = await CreateAsync(accountId, fresh: true, ct);
            return await StepAsync(session, phone, ct);
        });

    public async Task<TelegramLoginResult> SubmitCodeAsync(Guid accountId, string code, CancellationToken ct = default) =>
        await LockedAsync(accountId, async () =>
        {
            var session = _sessions.GetValueOrDefault(accountId)
                          ?? throw new TelegramException("Вход не начат или сервер перезапускался — нажмите «Подключить» ещё раз.", TelegramErrorKind.Auth);
            return await StepAsync(session, code.Trim(), ct);
        });

    public async Task<TelegramLoginResult> SubmitPasswordAsync(Guid accountId, string password, CancellationToken ct = default) =>
        await LockedAsync(accountId, async () =>
        {
            var session = _sessions.GetValueOrDefault(accountId)
                          ?? throw new TelegramException("Вход не начат или сервер перезапускался — нажмите «Подключить» ещё раз.", TelegramErrorKind.Auth);
            return await StepAsync(session, password, ct);
        });

    /// <summary>Шаг входа: Login() возвращает, что нужно дальше (код, пароль), или null — вход выполнен.</summary>
    private async Task<TelegramLoginResult> StepAsync(Session session, string value, CancellationToken ct)
    {
        string? next;
        try
        {
            next = await Limited(session.Client.Login(value), TimeSpan.FromSeconds(60));
        }
        catch (RpcException ex)
        {
            throw Map(ex);
        }
        catch (WTException ex)
        {
            throw new TelegramException($"Вход не выполнен: {ex.Message}", TelegramErrorKind.Auth);
        }

        switch (next)
        {
            case null:
                session.Login.Interactive = false;
                var me = Me(session.Client.User);
                await StartUpdatesAsync(session, ct);
                return new TelegramLoginResult(TelegramLoginStep.Done, me);
            case "verification_code":
                return new TelegramLoginResult(TelegramLoginStep.CodeRequired, null);
            case "password":
                return new TelegramLoginResult(TelegramLoginStep.PasswordRequired, null);
            case "name":
                throw new TelegramException("Аккаунта с этим номером нет в Telegram — сначала зарегистрируйте его в приложении Telegram.", TelegramErrorKind.Auth);
            default:
                throw new TelegramException($"Telegram просит «{next}» — такой шаг входа OneBase не поддерживает.", TelegramErrorKind.Auth);
        }
    }

    public async Task<TelegramMe> CheckAsync(Guid accountId, CancellationToken ct = default) =>
        await LockedAsync(accountId, async () =>
        {
            var session = await ConnectedAsync(accountId, ct);
            try
            {
                // Лёгкий запрос к серверу: сессия жива и не отозвана.
                await Limited(session.Client.Users_GetFullUser(InputUser.Self), CallTimeout);
            }
            catch (RpcException ex)
            {
                throw Map(ex);
            }

            return Me(session.Client.User);
        });

    public async Task LogoutAsync(Guid accountId, CancellationToken ct = default) =>
        await LockedAsync(accountId, async () =>
        {
            if (_sessions.TryGetValue(accountId, out var session))
            {
                try
                {
                    await Limited(session.Client.Auth_LogOut(), CallTimeout);
                }
                catch (Exception ex) when (ex is RpcException or WTException or IOException or TelegramException)
                {
                    logger.LogInformation("Telegram {Account}: выход — сессия уже недействительна ({Type})", accountId, ex.GetType().Name);
                }
            }

            await DropAsync(accountId, discard: true);
            return true;
        });

    // ---------- Чаты и отправка ----------

    public async Task<IReadOnlyList<TelegramDialog>> DialogsAsync(Guid accountId, CancellationToken ct = default)
    {
        var session = await LockedAsync(accountId, () => ConnectedAsync(accountId, ct));
        Messages_Dialogs dialogs;
        try
        {
            dialogs = await Limited(session.Client.Messages_GetAllDialogs(), TimeSpan.FromSeconds(90));
        }
        catch (RpcException ex)
        {
            throw Map(ex);
        }

        session.Manager?.LoadDialogs(dialogs);
        var result = new List<TelegramDialog>();
        foreach (var dialog in dialogs.Dialogs)
        {
            switch (dialogs.UserOrChat(dialog.Peer))
            {
                case User u when !u.flags.HasFlag(User.Flags.self) && !u.flags.HasFlag(User.Flags.deleted):
                    // Ботам и служебному чату Telegram (коды входа, 777000) рассылки и ответы агента не нужны.
                    var userReason = u.id == 777000 ? "служебный чат Telegram"
                        : u.flags.HasFlag(User.Flags.bot) ? "это бот, а не человек"
                        : null;
                    result.Add(new TelegramDialog(new TelegramPeerRef(u.id, TelegramChatType.Private, false, u.access_hash),
                        Name(u), u.MainUsername, userReason is null, userReason));
                    break;
                case Channel ch:
                    var group = ch.IsGroup;
                    var reason = ChannelReason(ch, group);
                    result.Add(new TelegramDialog(new TelegramPeerRef(ch.id, group ? TelegramChatType.Group : TelegramChatType.Channel, true, ch.access_hash),
                        ch.Title, ch.MainUsername, reason is null, reason));
                    break;
                case Chat chat:
                    var chatReason = chat.flags.HasFlag(Chat.Flags.deactivated) ? "группа удалена или преобразована в супергруппу"
                        : chat.flags.HasFlag(Chat.Flags.left) ? "аккаунт вышел из группы"
                        : !chat.flags.HasFlag(Chat.Flags.creator) && chat.admin_rights is null
                          && chat.default_banned_rights?.flags.HasFlag(ChatBannedRights.Flags.send_messages) == true ? "в группе участникам запрещено писать"
                        : null;
                    result.Add(new TelegramDialog(new TelegramPeerRef(chat.id, TelegramChatType.Group, false, 0), chat.Title, null, chatReason is null, chatReason));
                    break;
                case ChatBase other:
                    result.Add(new TelegramDialog(new TelegramPeerRef(other.ID, TelegramChatType.Group, other is ChannelForbidden, 0), other.Title, null, false,
                        "нет доступа: аккаунт исключён или группа закрыта"));
                    break;
            }
        }

        return result;
    }

    /// <summary>Почему в канал или супергруппу писать нельзя (null — можно).</summary>
    private static string? ChannelReason(Channel ch, bool group)
    {
        var creator = ch.flags.HasFlag(Channel.Flags.creator);
        if (ch.flags.HasFlag(Channel.Flags.left))
        {
            return group ? "аккаунт вышел из группы" : "аккаунт не подписан на канал";
        }

        if (!group)
        {
            return creator || ch.admin_rights?.flags.HasFlag(ChatAdminRights.Flags.post_messages) == true
                ? null
                : "канал: публиковать может только администратор с правом публикации";
        }

        var banned = ch.banned_rights?.flags.HasFlag(ChatBannedRights.Flags.send_messages) == true
                     || ch.default_banned_rights?.flags.HasFlag(ChatBannedRights.Flags.send_messages) == true;
        return !creator && ch.admin_rights is null && banned ? "в группе аккаунту запрещено писать" : null;
    }

    public async Task<TelegramResolvedUser?> ResolveUsernameAsync(Guid accountId, string username, CancellationToken ct = default)
    {
        var session = await LockedAsync(accountId, () => ConnectedAsync(accountId, ct));
        try
        {
            var resolved = await Limited(session.Client.Contacts_ResolveUsername(username), CallTimeout);
            return resolved.User is User u
                ? new TelegramResolvedUser(u.id, u.MainUsername ?? username, Name(u), u.flags.HasFlag(User.Flags.bot))
                : null;
        }
        catch (RpcException ex) when (ex.Message is "USERNAME_NOT_OCCUPIED" or "USERNAME_INVALID")
        {
            return null;
        }
        catch (RpcException ex)
        {
            throw Map(ex);
        }
    }

    public TelegramLiveStatus? LiveStatus(Guid accountId) =>
        _sessions.TryGetValue(accountId, out var s)
            ? new TelegramLiveStatus(s.Client.User is not null && s.Manager is not null, Interlocked.Read(ref s.Events), s.LastEventAt,
                Interlocked.Read(ref s.Messages), s.LastMessageAt)
            : null;

    private static InputPeer InputOf(TelegramPeerRef peer) =>
        peer.Type == TelegramChatType.Private ? new InputPeerUser(peer.PeerId, peer.AccessHash)
        : peer.IsChannelPeer ? new InputPeerChannel(peer.PeerId, peer.AccessHash)
        : new InputPeerChat(peer.PeerId);

    public async Task TypingAsync(Guid accountId, TelegramPeerRef peer, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(accountId, out var session) || session.Client.User is null)
        {
            return;
        }

        try
        {
            await Limited(session.Client.Messages_SetTyping(InputOf(peer), new SendMessageTypingAction()), TimeSpan.FromSeconds(10));
        }
        catch (Exception ex) when (ex is RpcException or WTException or IOException or TelegramException)
        {
            // «Печатает» — украшение: его сбой ответ не останавливает.
        }
    }

    public async Task<TelegramSent> SendAsync(Guid accountId, TelegramPeerRef peer, TelegramOutgoing message, CancellationToken ct = default)
    {
        var session = await LockedAsync(accountId, () => ConnectedAsync(accountId, ct));
        var client = session.Client;
        var input = InputOf(peer);
        var ids = new List<int>();
        try
        {
            var text = message.Text ?? string.Empty;
            MessageEntity[]? entities = null;
            if (message.Html && text.Length > 0)
            {
                entities = client.HtmlToEntities(ref text);
            }

            var reply = message.ReplyToMessageId ?? 0;
            var files = message.Files;
            // Один файл с короткой подписью — одним сообщением; иначе текст отдельно, файлы — следом.
            var captionWithFile = files.Count == 1 && text.Length is > 0 and <= TelegramRules.MaxCaptionLength;
            if (text.Length > 0 && !captionWithFile)
            {
                var sent = await SendLimited(client.SendMessageAsync(input, text, reply_to_msg_id: reply, entities: entities));
                ids.Add(sent.id);
                reply = 0;
            }

            foreach (var file in files)
            {
                var uploaded = await Limited(client.UploadFileAsync(new MemoryStream(file.Content), file.FileName), SendTimeout);
                var sent = captionWithFile
                    ? await SendLimited(client.SendMediaAsync(input, text, uploaded, file.ContentType, reply, entities))
                    : await SendLimited(client.SendMediaAsync(input, null, uploaded, file.ContentType, reply));
                ids.Add(sent.id);
                reply = 0;
            }
        }
        catch (RpcException ex)
        {
            throw Map(ex);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or WTException)
        {
            throw new TelegramException($"Нет связи с Telegram: {ex.GetType().Name}", TelegramErrorKind.Transient);
        }

        await TouchAsync(session);
        return new TelegramSent(ids);
    }

    public async Task<byte[]?> DownloadAsync(Guid accountId, long peerId, int messageId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(accountId, out var session) || !session.Documents.TryRemove((peerId, messageId), out var document))
        {
            return null;
        }

        try
        {
            using var buffer = new MemoryStream();
            await Limited(session.Client.DownloadFileAsync(document, buffer), SendTimeout);
            return buffer.ToArray();
        }
        catch (RpcException ex)
        {
            throw Map(ex);
        }
    }

    // ---------- Сессии ----------

    /// <summary>Подключённый клиент аккаунта: в памяти или по сохранённой сессии (после перезапуска сервера).</summary>
    private async Task<Session> ConnectedAsync(Guid accountId, CancellationToken ct)
    {
        if (_sessions.TryGetValue(accountId, out var existing) && existing.Client.User is not null)
        {
            return existing;
        }

        await DropAsync(accountId);
        var session = await CreateAsync(accountId, fresh: false, ct);
        if (session.Store.IsEmpty)
        {
            await DropAsync(accountId);
            throw new TelegramException("Аккаунт не подключён — нажмите «Подключить».", TelegramErrorKind.Config);
        }

        try
        {
            // С сохранённой сессией вход не спрашивает код; если сессия отозвана — Telegram ответит ошибкой. Без повторного входа:
            // иначе библиотека вышла бы из сессии и начала вход заново (запросила бы код на телефон).
            await Limited(session.Client.LoginUserIfNeeded(reloginOnFailedResume: false), TimeSpan.FromSeconds(60));
        }
        catch (RpcException ex)
        {
            await DropAsync(accountId);
            throw Map(ex);
        }
        catch (TelegramException)
        {
            await DropAsync(accountId);
            throw;
        }
        catch (Exception ex) when (ex is WTException or ArgumentException or InvalidOperationException)
        {
            await DropAsync(accountId);
            throw new TelegramException("Сессия Telegram недействительна — переподключите аккаунт.", TelegramErrorKind.Auth);
        }

        await StartUpdatesAsync(session, ct);
        return session;
    }

    private async Task<Session> CreateAsync(Guid accountId, bool fresh, CancellationToken ct)
    {
        var api = await credentials.GetAsync(ct)
                  ?? throw new TelegramException("Не заданы api_id и api_hash — укажите их в «Настройки → Telegram» (my.telegram.org).", TelegramErrorKind.Config);
        byte[]? saved = null;
        long? knownUserId = null;
        if (!fresh)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var row = await db.TelegramAccounts.AsNoTracking().Where(a => a.Id == accountId)
                .Select(a => new { a.ProtectedSession, a.TelegramUserId }).FirstOrDefaultAsync(ct);
            var protectedSession = row?.ProtectedSession;
            knownUserId = row?.TelegramUserId;
            if (protectedSession is not null)
            {
                try
                {
                    saved = _protector.Unprotect(Convert.FromBase64String(protectedSession));
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    saved = null;
                }
            }
        }

        var store = new SessionStore(saved, bytes => SaveSessionAsync(accountId, bytes));
        var login = new LoginState { Interactive = fresh };
        var client = new Client(what => what switch
        {
            "api_id" => api.ApiId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "api_hash" => api.ApiHash,
            "device_model" => "OneBase",
            "app_version" => "OneBase 1.0",
            "system_lang_code" or "lang_code" => "ru",
            // При восстановлении сессии библиотека сверяет вошедшего пользователя с user_id (иначе — с номером телефона, которого мы не
            // даём): известный ID аккаунта, -1 — «любой, кто вошёл в эту сессию».
            "user_id" => (knownUserId ?? -1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            // Номер, код и пароль — только через Login(): пока идёт вход, отвечаем null, и Login() берёт их из своих шагов (он сначала
            // спрашивает этот обработчик). Вне входа библиотека просит их, только если сохранённая сессия не годится, — тогда ошибка,
            // а не ожидание ввода (иначе библиотека спросила бы код у консоли сервера).
            "phone_number" or "verification_code" or "password" or "first_name" or "last_name" or "name" or "email" or "email_verification_code"
                => login.Interactive ? null : throw new TelegramException("Сессия Telegram недействительна — переподключите аккаунт.", TelegramErrorKind.Auth),
            _ => null,
        }, store)
        {
            FloodRetryThreshold = 10,
            MaxAutoReconnects = 50,
        };
        var session = new Session(accountId, client, store, login);
        _sessions[accountId] = session;
        return session;
    }

    private async Task StartUpdatesAsync(Session session, CancellationToken ct)
    {
        if (session.Manager is not null)
        {
            return;
        }

        IDictionary<long, UpdateManager.MBoxState>? state = null;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var json = await db.TelegramAccounts.AsNoTracking().Where(a => a.Id == session.AccountId).Select(a => a.UpdateState).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    state = JsonSerializer.Deserialize<Dictionary<long, UpdateManager.MBoxState>>(json, StateJson);
                }
                catch (JsonException)
                {
                    state = null;
                }
            }
        }

        session.Manager = session.Client.WithUpdateManager(update => OnUpdateAsync(session, update), state);

        // Пользователи и чаты из диалогов — в память приёма: короткие входящие (Telegram шлёт их без данных отправителя, считая, что
        // клиент его знает) после перезапуска иначе не с кем было бы сопоставить.
        try
        {
            session.Manager.LoadDialogs(await Limited(session.Client.Messages_GetAllDialogs(), TimeSpan.FromSeconds(90)));
        }
        catch (Exception ex) when (ex is RpcException or WTException or IOException or TelegramException)
        {
            logger.LogWarning("Telegram {Account}: диалоги для приёма сообщений не загружены — {Error}", session.AccountId, ex.Message);
        }
    }

    /// <summary>Чат, которого нет в памяти приёма, — из сохранённого списка чатов аккаунта (там есть access_hash).</summary>
    private async Task<(TelegramPeerRef Peer, string Title)?> KnownChatAsync(Guid accountId, Peer peer)
    {
        var peerId = peer switch
        {
            PeerUser u => u.user_id,
            PeerChat c => c.chat_id,
            PeerChannel ch => ch.channel_id,
            _ => 0,
        };
        if (peerId == 0)
        {
            return null;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var chat = await db.TelegramChats.AsNoTracking().FirstOrDefaultAsync(c => c.AccountId == accountId && c.PeerId == peerId);
        return chat is null ? null : (new TelegramPeerRef(chat.PeerId, chat.Type, chat.IsChannelPeer, chat.AccessHash), chat.Title);
    }

    private async Task OnUpdateAsync(Session session, Update update)
    {
        try
        {
            Interlocked.Increment(ref session.Events);
            session.LastEventAt = DateTimeOffset.UtcNow;
            if (update is not UpdateNewMessage { message: Message m } || session.Manager is null)
            {
                return; // правки сообщений и прочие события агент не обрабатывает
            }

            var outgoing = m.flags.HasFlag(Message.Flags.out_);
            if (!outgoing)
            {
                Interlocked.Increment(ref session.Messages);
                session.LastMessageAt = DateTimeOffset.UtcNow;
            }

            var chat = session.Manager.UserOrChat(m.peer_id);
            var peer = chat switch
            {
                User u => new TelegramPeerRef(u.id, TelegramChatType.Private, false, u.access_hash),
                Channel ch => new TelegramPeerRef(ch.id, ch.IsGroup ? TelegramChatType.Group : TelegramChatType.Channel, true, ch.access_hash),
                ChatBase c => new TelegramPeerRef(c.ID, TelegramChatType.Group, false, 0),
                _ => null,
            };
            string? knownTitle = null;
            if (peer is null && await KnownChatAsync(session.AccountId, m.peer_id) is { } known)
            {
                (peer, knownTitle) = known;
            }

            logger.LogInformation("Telegram {Account}: сообщение {Message} в чате {Peer} ({Direction}){Unknown}", session.AccountId, m.id,
                peer?.PeerId ?? m.peer_id?.ID, outgoing ? "исходящее" : "входящее", peer is null ? " — чат неизвестен, пропущено" : string.Empty);
            if (peer is null)
            {
                return;
            }

            long? fromId = m.from_id is PeerUser fu ? fu.user_id : m.peer_id is PeerUser pu ? pu.user_id : null;
            var from = fromId is { } id ? session.Manager.Users.GetValueOrDefault(id) : null;
            TelegramIncomingFile? file = null;
            if (m.media is MessageMediaDocument { document: Document document })
            {
                session.Documents[(peer.PeerId, m.id)] = document;
                file = new TelegramIncomingFile(document.Filename ?? "file", document.mime_type, document.size);
                if (session.Documents.Count > 200)
                {
                    foreach (var old in session.Documents.Keys.Take(50).ToList())
                    {
                        session.Documents.TryRemove(old, out _);
                    }
                }
            }

            inbox.Post(new TelegramIncoming(
                session.AccountId,
                peer,
                chat is User cu ? Name(cu) : (chat as ChatBase)?.Title ?? knownTitle ?? peer.PeerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                m.id,
                fromId,
                from?.MainUsername,
                from is null ? null : Name(from),
                m.message ?? string.Empty,
                new DateTimeOffset(DateTime.SpecifyKind(m.Date, DateTimeKind.Utc)),
                m.flags.HasFlag(Message.Flags.out_) || fromId == session.Client.UserId,
                m.flags.HasFlag(Message.Flags.mentioned),
                file,
                from is not null && from.flags.HasFlag(User.Flags.bot)));
            ScheduleStateSave(session);
            await TouchAsync(session);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram {Account}: входящее не обработано", session.AccountId);
        }
    }

    /// <summary>
    /// Отправка: ждать ответа Telegram не дольше CallTimeout. Не дождались — неизвестно, ушло ли сообщение: ошибка без автоповтора
    /// (повтор мог бы отправить второй раз).
    /// </summary>
    private static async Task<T> SendLimited<T>(Task<T> call)
    {
        try
        {
            return await call.WaitAsync(CallTimeout);
        }
        catch (TimeoutException)
        {
            throw new TelegramException($"Telegram не подтвердил отправку за {CallTimeout.TotalSeconds:0} с — неизвестно, дошло ли сообщение; проверьте чат.",
                TelegramErrorKind.Permanent);
        }
    }

    /// <summary>
    /// Сохранить состояние приёма через несколько секунд, вне обработчика: UpdateManager вызывает обработчик, держа свой замок, а
    /// State ждёт тот же замок — чтение State внутри обработчика навсегда останавливало клиент (все запросы к Telegram зависали).
    /// </summary>
    private void ScheduleStateSave(Session session)
    {
        if (Interlocked.Exchange(ref session.StateSaveScheduled, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                Interlocked.Exchange(ref session.StateSaveScheduled, 0);
                await SaveStateAsync(session);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Telegram {Account}: состояние приёма не сохранено", session.AccountId);
            }
        });
    }

    /// <summary>Сессия изменилась — в базу (зашифрованной). Пишет библиотека: при входе и при смене ключей.</summary>
    private async Task SaveSessionAsync(Guid accountId, byte[] bytes)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var value = Convert.ToBase64String(_protector.Protect(bytes));
            await db.TelegramAccounts.Where(a => a.Id == accountId).ExecuteUpdateAsync(u => u.SetProperty(a => a.ProtectedSession, value));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram {Account}: сессия не сохранена", accountId);
        }
    }

    private async Task SaveStateAsync(Session session)
    {
        if (session.Manager is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(session.Manager.State, StateJson);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.TelegramAccounts.Where(a => a.Id == session.AccountId).ExecuteUpdateAsync(u => u.SetProperty(a => a.UpdateState, json));
    }

    private async Task TouchAsync(Session session)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - session.LastActivitySaved < TimeSpan.FromMinutes(1))
        {
            return;
        }

        session.LastActivitySaved = now;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.TelegramAccounts.Where(a => a.Id == session.AccountId).ExecuteUpdateAsync(u => u.SetProperty(a => a.LastActivityAt, now));
    }

    /// <summary>Закрыть клиент. discard — после выхода: отложенная запись сессии в базу отменяется.</summary>
    private async Task DropAsync(Guid accountId, bool discard = false)
    {
        if (_sessions.TryRemove(accountId, out var session))
        {
            if (discard)
            {
                session.Store.Discard();
            }

            session.Manager = null;
            await session.Client.DisposeAsync();
        }
    }

    private async Task<T> LockedAsync<T>(Guid accountId, Func<Task<T>> action)
    {
        var gate = _gates.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }

    private static TelegramMe Me(User? user) =>
        user is null ? throw new TelegramException("Telegram не вернул пользователя.", TelegramErrorKind.Auth) : new TelegramMe(user.id, user.MainUsername, Name(user));

    private static string Name(User user) =>
        string.Join(' ', new[] { user.first_name, user.last_name }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } name ? name : user.MainUsername ?? user.id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static TelegramException Map(RpcException ex) => TelegramRules.Classify(ex.Code, ex.Message, ex.X);

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _sessions.Keys.ToList())
        {
            await DropAsync(id);
        }
    }

    /// <summary>
    /// Хранилище сессии для WTelegramClient: библиотека читает начальное содержимое (Length, Read) и пишет обновлённое целиком (Write).
    /// Запись уходит в базу зашифрованной, не чаще раза в секунду; последнее состояние не теряется.
    /// </summary>
    internal sealed class SessionStore(byte[]? initial, Func<byte[], Task> save) : Stream
    {
        private byte[] _data = initial ?? [];
        private int _length = initial?.Length ?? 0;
        private Task? _pending;
        private DateTime _lastWrite = DateTime.MinValue;
        private readonly Lock _lock = new();
        private bool _discarded;

        public bool IsEmpty => _length == 0;

        public void Discard()
        {
            lock (_lock)
            {
                _discarded = true;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => 0; set { } }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                var n = Math.Min(count, _length);
                Array.Copy(_data, 0, buffer, offset, n);
                return n;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                _data = buffer[offset..(offset + count)];
                _length = count;
                if (_pending is not null || _discarded)
                {
                    return;
                }

                var wait = 1000 - (int)(DateTime.UtcNow - _lastWrite).TotalMilliseconds;
                _pending = Task.Delay(Math.Max(wait, 0)).ContinueWith(async _ =>
                {
                    byte[] snapshot;
                    lock (_lock)
                    {
                        _pending = null;
                        if (_discarded)
                        {
                            return;
                        }

                        snapshot = _data[.._length];
                        _lastWrite = DateTime.UtcNow;
                    }

                    await save(snapshot);
                }).Unwrap();
            }
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) { }
    }
}

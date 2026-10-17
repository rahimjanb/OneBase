using OneBase.AI.Consultant;
using OneBase.AI.Providers;
using OneBase.Application.Telegram;
using OneBase.Domain.Telegram;

namespace OneBase.AI.Tests;

/// <summary>
/// Подставной Telegram: вход по коду и паролю 2FA, «сессии» в памяти (переживают «перезапуск» — как сохранённая сессия), чаты, отправка
/// с журналом и сбоями по сценарию. Настоящих отправок нет.
/// </summary>
public sealed class FakeTelegram : ITelegramGateway
{
    public string Code { get; set; } = "24680";

    /// <summary>Пароль двухэтапной защиты; null — защиты нет.</summary>
    public string? Password { get; set; }

    public TelegramMe Me { get; set; } = new(777_000_111, "onebase_sales", "OneBase Продажи");

    public List<TelegramDialog> Dialogs { get; } = [];

    public List<(Guid Account, TelegramPeerRef Peer, TelegramOutgoing Message)> Sent { get; } = [];

    /// <summary>Сбой отправки: (чат, номер попытки в этот чат) → ошибка или null.</summary>
    public Func<TelegramPeerRef, int, TelegramException?>? Fail { get; set; }

    public Dictionary<long, int> Attempts { get; } = [];

    /// <summary>Сессии, отозванные «в Telegram».</summary>
    public HashSet<Guid> Revoked { get; } = [];

    public HashSet<Guid> Sessions { get; } = [];

    public Dictionary<(long Peer, int Message), byte[]> Files { get; } = [];

    /// <summary>Пользователи Telegram по username (поиск при добавлении в список).</summary>
    public Dictionary<string, TelegramResolvedUser> Users { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Перед каждой отправкой (например, нажать «Пауза» посреди рассылки).</summary>
    public Func<TelegramPeerRef, Task>? BeforeSend { get; set; }

    private readonly Dictionary<Guid, string> _step = [];
    private int _messageId = 5000;

    public void Reset()
    {
        Code = "24680";
        Password = null;
        Me = new TelegramMe(777_000_111, "onebase_sales", "OneBase Продажи");
        Dialogs.Clear();
        Sent.Clear();
        Fail = null;
        BeforeSend = null;
        Attempts.Clear();
        Revoked.Clear();
        Sessions.Clear();
        Files.Clear();
        Users.Clear();
        _step.Clear();
    }

    public Task<TelegramLoginResult> StartLoginAsync(Guid accountId, string phone, CancellationToken ct = default)
    {
        Sessions.Remove(accountId);
        _step[accountId] = "code";
        return Task.FromResult(new TelegramLoginResult(TelegramLoginStep.CodeRequired, null));
    }

    public Task<TelegramLoginResult> SubmitCodeAsync(Guid accountId, string code, CancellationToken ct = default)
    {
        if (_step.GetValueOrDefault(accountId) != "code")
        {
            throw new TelegramException("Вход не начат.", TelegramErrorKind.Auth);
        }

        if (code != Code)
        {
            throw TelegramRules.Classify(400, "PHONE_CODE_INVALID", 0);
        }

        if (Password is not null)
        {
            _step[accountId] = "password";
            return Task.FromResult(new TelegramLoginResult(TelegramLoginStep.PasswordRequired, null));
        }

        return Task.FromResult(Done(accountId));
    }

    public Task<TelegramLoginResult> SubmitPasswordAsync(Guid accountId, string password, CancellationToken ct = default)
    {
        if (_step.GetValueOrDefault(accountId) != "password")
        {
            throw new TelegramException("Вход не начат.", TelegramErrorKind.Auth);
        }

        return password == Password ? Task.FromResult(Done(accountId)) : throw TelegramRules.Classify(400, "PASSWORD_HASH_INVALID", 0);
    }

    private TelegramLoginResult Done(Guid accountId)
    {
        _step[accountId] = "done";
        Sessions.Add(accountId);
        return new TelegramLoginResult(TelegramLoginStep.Done, Me);
    }

    public Task<TelegramMe> CheckAsync(Guid accountId, CancellationToken ct = default)
    {
        if (Revoked.Contains(accountId))
        {
            Sessions.Remove(accountId);
            throw TelegramRules.Classify(401, "AUTH_KEY_UNREGISTERED", 0);
        }

        return Sessions.Contains(accountId) ? Task.FromResult(Me) : throw new TelegramException("Аккаунт не подключён.", TelegramErrorKind.Config);
    }

    public Task LogoutAsync(Guid accountId, CancellationToken ct = default)
    {
        Sessions.Remove(accountId);
        _step.Remove(accountId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TelegramDialog>> DialogsAsync(Guid accountId, CancellationToken ct = default) =>
        Sessions.Contains(accountId) ? Task.FromResult<IReadOnlyList<TelegramDialog>>(Dialogs.ToList()) : throw new TelegramException("Аккаунт не подключён.", TelegramErrorKind.Config);

    public async Task<TelegramSent> SendAsync(Guid accountId, TelegramPeerRef peer, TelegramOutgoing message, CancellationToken ct = default)
    {
        if (BeforeSend is not null)
        {
            await BeforeSend(peer);
        }

        if (!Sessions.Contains(accountId))
        {
            throw new TelegramException("Аккаунт не подключён.", TelegramErrorKind.Config);
        }

        var attempt = Attempts[peer.PeerId] = Attempts.GetValueOrDefault(peer.PeerId) + 1;
        if (Fail?.Invoke(peer, attempt) is { } error)
        {
            throw error;
        }

        Sent.Add((accountId, peer, message));
        var count = (string.IsNullOrEmpty(message.Text) ? 0 : 1) + message.Files.Count;
        return new TelegramSent(Enumerable.Range(0, Math.Max(count, 1)).Select(_ => ++_messageId).ToList());
    }

    public Task<byte[]?> DownloadAsync(Guid accountId, long peerId, int messageId, CancellationToken ct = default) =>
        Task.FromResult(Files.GetValueOrDefault((peerId, messageId)));

    public Task<TelegramResolvedUser?> ResolveUsernameAsync(Guid accountId, string username, CancellationToken ct = default) =>
        Task.FromResult(Users.GetValueOrDefault(username));

    /// <summary>Сколько раз агент показывал «печатает…».</summary>
    public int Typing { get; private set; }

    public Task TypingAsync(Guid accountId, TelegramPeerRef peer, CancellationToken ct = default)
    {
        Typing++;
        return Task.CompletedTask;
    }

    public TelegramLiveStatus? LiveStatus(Guid accountId) => Sessions.Contains(accountId) ? new TelegramLiveStatus(true, 0, null, 0, null) : null;
}

/// <summary>Подставной консультант OneBase: запоминает вопросы (с каким пользователем и каналом), отвечает по сценарию.</summary>
public sealed class FakeConsultant : IConsultantEngine
{
    public List<ConsultantTurn> Turns { get; } = [];

    public Func<ConsultantTurn, ConsultantAnswer>? Answer { get; set; }

    public Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, Func<ConsultantProgress, Task> progress, CancellationToken cancellationToken)
    {
        Turns.Add(turn);
        return Task.FromResult(Answer?.Invoke(turn)
                               ?? new ConsultantAnswer("**Продажи** за сентябрь: 1 400 кг.", new ConsultantDetails([], [], "fake/consultant", false),
                                   new AiTokenUsage(10, 5)));
    }
}

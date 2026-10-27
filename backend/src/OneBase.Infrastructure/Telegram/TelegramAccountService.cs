using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Telegram;

public sealed record TelegramAccountView(
    Guid Id,
    string Name,
    string? PhoneHint,
    string Status,
    long? TelegramUserId,
    string? Username,
    string? DisplayName,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? LastActivityAt,
    string? LastError,
    bool AgentEnabled,
    int Permissions,
    int Chats,
    TelegramLiveStatus? Live = null);

/// <summary>Кому отвечает агент: ReplyToAll — всем (не из списка — по отправленным данным); с чьими правами — полный доступ.</summary>
public sealed record TelegramAccessView(bool ReplyToAll, Guid? FullAccessUserId, string? FullAccessUserName);

public sealed record TelegramAccessInput(bool ReplyToAll, Guid? FullAccessUserId);

public sealed record TelegramOverview(TelegramCredentialsView Credentials, IReadOnlyList<TelegramAccountView> Accounts);

public sealed record TelegramLoginView(string Status, TelegramAccountView Account);

public sealed record TelegramSettingsInput(
    bool Enabled,
    bool AutoReply,
    bool ReplyToUnknown,
    string? ModelProvider,
    string? Model,
    string? SystemPrompt,
    string? Restrictions,
    string? ResponseStyle,
    string? Language,
    int MaxMessagesPerHour,
    int MaxOutputTokens,
    int ReplyDelaySeconds,
    string? NoInfoReply,
    string? AllowedAttachments,
    int MaxFileMb,
    bool AllowReports,
    string? Persona = null,
    string? GenerationModelProvider = null,
    string? GenerationModel = null);

public sealed record TelegramPermissionInput(
    long? TelegramUserId,
    string? Username,
    string? Title,
    TelegramAccessMode Mode,
    Guid? UserId,
    bool AllowReports,
    bool Enabled);

public sealed record TelegramPermissionView(
    Guid Id,
    long? TelegramUserId,
    string? Username,
    string? Title,
    string Mode,
    Guid? UserId,
    string? OneBaseUser,
    bool AllowReports,
    bool Enabled,
    DateTimeOffset? VerifiedAt,
    // active — права действуют; pending — Telegram ID определится по первому сообщению; no_rights — полному доступу не выбран пользователь OneBase; disabled.
    string State,
    DateTimeOffset CreatedAt);

public sealed record TelegramBulkInput(IReadOnlyList<Guid> Ids, string Action);

public sealed record TelegramOneBaseUser(Guid Id, string FullName, string Login, string? Position);

public sealed record TelegramChatView(
    Guid Id,
    long PeerId,
    string Type,
    string Title,
    string? Username,
    bool CanSend,
    bool AgentEnabled,
    DateTimeOffset? SyncedAt,
    string? Reason = null);

public sealed record TelegramExecutionView(
    Guid Id,
    DateTimeOffset CreatedAt,
    long PeerId,
    string? ChatTitle,
    long? TelegramUserId,
    string? Who,
    string Mode,
    string Status,
    string? Question,
    string? Answer,
    Guid? ReportId,
    string? Model,
    int InputTokens,
    int OutputTokens,
    long DurationMs,
    string? Error);

public sealed record TelegramLinkCodeView(string Code, DateTimeOffset ExpiresAt, IReadOnlyList<string> SendTo);

public sealed record TelegramLinkView(Guid PermissionId, string Account, string? AccountUsername, long? TelegramUserId, string? Username, string Mode,
    DateTimeOffset? VerifiedAt);

/// <summary>
/// «Настройки → Telegram»: ключи приложения, аккаунты (вход по номеру, коду и паролю 2FA; проверка, отключение), настройки агента,
/// список пользователей Telegram с режимами доступа, чаты аккаунта и журнал агента. Привязка Telegram к пользователю OneBase — кодом из
/// его профиля: полный доступ действует только после подтверждения. Номер и сессия — зашифрованы; коды и пароль не сохраняются и не пишутся в журнал.
/// </summary>
public sealed class TelegramAccountService(
    OneBaseDbContext db,
    ITelegramGateway gateway,
    TelegramCredentialsStore credentials,
    IDataProtectionProvider protection,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<TelegramAccountService> logger)
{
    private static readonly string[] Styles = ["short", "normal", "detailed"];
    private static readonly string[] Languages = ["auto", "ru", "uz", "en"];

    private readonly IDataProtector _phoneProtector = protection.CreateProtector("OneBase.Telegram.Phone");

    // ---------- Обзор и ключи приложения ----------

    public async Task<TelegramOverview> OverviewAsync(CancellationToken ct = default) =>
        new(await credentials.ViewAsync(ct), await AccountsAsync(ct));

    public async Task SaveCredentialsAsync(int apiId, string? apiHash, Guid userId, CancellationToken ct = default)
    {
        await credentials.SaveAsync(apiId, string.IsNullOrWhiteSpace(apiHash) ? null : apiHash, userId, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.credentials.saved", "Integration", TelegramCredentialsStore.Code,
            new { apiId, hashChanged = !string.IsNullOrWhiteSpace(apiHash) }, ct);
    }

    // ---------- Аккаунты ----------

    public async Task<IReadOnlyList<TelegramAccountView>> AccountsAsync(CancellationToken ct = default)
    {
        var accounts = await db.TelegramAccounts.AsNoTracking().OrderBy(a => a.CreatedAt).ToListAsync(ct);
        var ids = accounts.Select(a => a.Id).ToList();
        var agents = await db.TelegramAgentSettings.AsNoTracking().Where(s => ids.Contains(s.AccountId)).ToDictionaryAsync(s => s.AccountId, s => s.Enabled, ct);
        var permissions = await db.TelegramUserPermissions.AsNoTracking().Where(p => ids.Contains(p.AccountId))
            .GroupBy(p => p.AccountId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var chats = await db.TelegramChats.AsNoTracking().Where(c => ids.Contains(c.AccountId))
            .GroupBy(c => c.AccountId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return accounts.Select(a => View(a, agents.GetValueOrDefault(a.Id), permissions.GetValueOrDefault(a.Id), chats.GetValueOrDefault(a.Id))).ToList();
    }

    public async Task<TelegramAccountView> CreateAccountAsync(string name, Guid userId, CancellationToken ct = default)
    {
        var account = new TelegramAccount { Name = CleanName(name), CreatedById = userId };
        db.TelegramAccounts.Add(account);
        db.TelegramAgentSettings.Add(new TelegramAgentSettings { AccountId = account.Id, UpdatedById = userId, UpdatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.account.created", nameof(TelegramAccount), account.Id.ToString(), new { account.Name }, ct);
        return View(account, false, 0, 0);
    }

    public async Task<TelegramAccountView> RenameAccountAsync(Guid id, string name, Guid userId, CancellationToken ct = default)
    {
        var account = await AccountAsync(id, ct);
        account.Name = CleanName(name);
        account.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return await ViewAsync(account, ct);
    }

    /// <summary>Удалить аккаунт: сначала выход из Telegram. Аккаунт с рассылками не удаляется — их журнал нужен.</summary>
    public async Task DeleteAccountAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var account = await AccountAsync(id, ct);
        if (await db.TelegramCampaigns.AnyAsync(c => c.AccountId == id, ct))
        {
            throw new TelegramException("У аккаунта есть рассылки — их журнал нужен. Отключите аккаунт вместо удаления.", TelegramErrorKind.Config);
        }

        await SafeLogoutAsync(id, ct);
        db.TelegramAccounts.Remove(account);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.account.deleted", nameof(TelegramAccount), id.ToString(), new { account.Name }, ct);
    }

    // ---------- Вход ----------

    /// <summary>Шаг 1: номер телефона → Telegram присылает код (в приложение Telegram или SMS).</summary>
    public async Task<TelegramLoginView> StartLoginAsync(Guid id, string phone, Guid userId, CancellationToken ct = default)
    {
        var account = await AccountAsync(id, ct);
        var normalized = NormalizePhone(phone);
        account.ProtectedPhone = _phoneProtector.Protect(normalized);
        account.PhoneHint = "…" + normalized[^4..];
        account.ProtectedSession = null;
        account.UpdateState = null;
        account.TelegramUserId = null;
        account.Username = null;
        account.DisplayName = null;
        await db.SaveChangesAsync(ct);

        var result = await LoginStepAsync(account, () => gateway.StartLoginAsync(id, normalized, ct), ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.account.login_started", nameof(TelegramAccount), id.ToString(),
            new { account.PhoneHint, result.Status }, ct);
        return result;
    }

    /// <summary>Шаг 2: код из Telegram. Код не сохраняется и не пишется в журнал.</summary>
    public async Task<TelegramLoginView> SubmitCodeAsync(Guid id, string code, Guid userId, CancellationToken ct = default)
    {
        var account = await AccountAsync(id, ct);
        var digits = new string((code ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 4 or > 8)
        {
            throw new TelegramException("Код — цифры из сообщения Telegram.", TelegramErrorKind.Auth);
        }

        var result = await LoginStepAsync(account, () => gateway.SubmitCodeAsync(id, digits, ct), ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.account.code_submitted", nameof(TelegramAccount), id.ToString(), new { result.Status }, ct);
        return result;
    }

    /// <summary>Шаг 3 (если включена двухэтапная защита): пароль. Не сохраняется и не пишется в журнал.</summary>
    public async Task<TelegramLoginView> SubmitPasswordAsync(Guid id, string password, Guid userId, CancellationToken ct = default)
    {
        var account = await AccountAsync(id, ct);
        if (string.IsNullOrEmpty(password))
        {
            throw new TelegramException("Введите пароль двухэтапной защиты Telegram.", TelegramErrorKind.Auth);
        }

        var result = await LoginStepAsync(account, () => gateway.SubmitPasswordAsync(id, password, ct), ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.account.password_submitted", nameof(TelegramAccount), id.ToString(), new { result.Status }, ct);
        return result;
    }

    private async Task<TelegramLoginView> LoginStepAsync(TelegramAccount account, Func<Task<TelegramLoginResult>> step, CancellationToken ct)
    {
        TelegramLoginResult result;
        try
        {
            result = await step();
        }
        catch (TelegramException ex)
        {
            // Неверный код или пароль — шаг можно повторить; прочее — вход сорван.
            if (ex.Kind is not TelegramErrorKind.Auth || ex.Message.Contains("Код истёк", StringComparison.Ordinal))
            {
                account.Status = TelegramAccountStatus.Error;
            }

            account.LastError = ex.Message;
            account.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        account.LastError = null;
        account.UpdatedAt = clock.GetUtcNow();
        switch (result.Step)
        {
            case TelegramLoginStep.CodeRequired:
                account.Status = TelegramAccountStatus.CodeRequired;
                break;
            case TelegramLoginStep.PasswordRequired:
                account.Status = TelegramAccountStatus.PasswordRequired;
                break;
            default:
                var me = result.Me!;
                var twin = await db.TelegramAccounts.AsNoTracking()
                    .Where(a => a.Id != account.Id && a.TelegramUserId == me.UserId && a.Status == TelegramAccountStatus.Connected)
                    .Select(a => a.Name).FirstOrDefaultAsync(ct);
                if (twin is not null)
                {
                    // Две записи одного аккаунта Telegram отвечали бы на одно сообщение дважды.
                    await SafeLogoutAsync(account.Id, ct);
                    account.Status = TelegramAccountStatus.Error;
                    account.ProtectedSession = null;
                    account.LastError = $"Этот аккаунт Telegram уже подключён как «{twin}».";
                    await db.SaveChangesAsync(ct);
                    throw new TelegramException(account.LastError, TelegramErrorKind.Config);
                }

                account.Status = TelegramAccountStatus.Connected;
                account.TelegramUserId = me.UserId;
                account.Username = me.Username;
                account.DisplayName = me.DisplayName;
                account.ConnectedAt = clock.GetUtcNow();
                break;
        }

        await db.SaveChangesAsync(ct);
        if (account.Status == TelegramAccountStatus.Connected)
        {
            try
            {
                await SyncChatsAsync(account.Id, ct);
                await ResolvePendingAsync(account.Id, ct);
            }
            catch (TelegramException ex)
            {
                logger.LogWarning("Telegram {Account}: чаты после входа не загружены — {Error}", account.Id, ex.Message);
            }
        }

        return new TelegramLoginView(account.Status.ToString(), await ViewAsync(account, ct));
    }

    /// <summary>Проверить подключение: сессия жива, кто мы. Отозванная сессия → «Отключён».</summary>
    public async Task<TelegramAccountView> CheckAsync(Guid id, CancellationToken ct = default)
    {
        var account = await AccountAsync(id, ct);
        await RefreshAsync(account, ct);
        return await ViewAsync(account, ct);
    }

    /// <summary>
    /// Подключить по сохранённой сессии и обновить статус (при запуске сервера и периодически): приём входящих начинается здесь.
    /// false — сессия недействительна (аккаунт помечен «Отключён»); временная ошибка — TelegramException.
    /// </summary>
    public async Task<bool> ReconnectAsync(Guid id, CancellationToken ct = default) => await RefreshAsync(await AccountAsync(id, ct), ct);

    private async Task<bool> RefreshAsync(TelegramAccount account, CancellationToken ct)
    {
        try
        {
            var me = await gateway.CheckAsync(account.Id, ct);
            account.Status = TelegramAccountStatus.Connected;
            account.TelegramUserId = me.UserId;
            account.Username = me.Username;
            account.DisplayName = me.DisplayName;
            account.LastError = null;
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (TelegramException ex)
        {
            // Сессия отозвана или её нет — «Отключён». Но если просто не заданы ключи приложения, сессия цела: статус не трогаем,
            // только причина — после ввода api_id/api_hash аккаунт поднимется без повторного входа.
            var keysMissing = ex.Kind == TelegramErrorKind.Config && await credentials.GetAsync(CancellationToken.None) is null;
            var lost = ex.Kind == TelegramErrorKind.Auth || (ex.Kind == TelegramErrorKind.Config && !keysMissing);
            if (lost)
            {
                account.Status = TelegramAccountStatus.Disconnected;
            }

            account.LastError = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            if (lost)
            {
                return false;
            }

            throw;
        }
    }

    /// <summary>Отключить: выход из сессии в Telegram, сессия в базе стирается. Список пользователей и чаты остаются.</summary>
    public async Task<TelegramAccountView> DisconnectAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var account = await AccountAsync(id, ct);
        await SafeLogoutAsync(id, ct);
        account.Status = TelegramAccountStatus.Disconnected;
        account.ProtectedSession = null;
        account.UpdateState = null;
        account.LastError = null;
        account.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.account.disconnected", nameof(TelegramAccount), id.ToString(), new { account.Name }, ct);
        return await ViewAsync(account, ct);
    }

    private async Task SafeLogoutAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await gateway.LogoutAsync(id, ct);
        }
        catch (TelegramException ex)
        {
            logger.LogInformation("Telegram {Account}: выход — {Error}", id, ex.Message);
        }
    }

    // ---------- Настройки агента ----------

    public async Task<TelegramAgentSettings> SettingsAsync(Guid accountId, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        return await db.TelegramAgentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.AccountId == accountId, ct)
               ?? new TelegramAgentSettings { AccountId = accountId };
    }

    public async Task<TelegramAgentSettings> SaveSettingsAsync(Guid accountId, TelegramSettingsInput input, Guid userId, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        var settings = await db.TelegramAgentSettings.FirstOrDefaultAsync(s => s.AccountId == accountId, ct);
        if (settings is null)
        {
            settings = new TelegramAgentSettings { AccountId = accountId };
            db.TelegramAgentSettings.Add(settings);
        }

        var style = (input.ResponseStyle ?? "normal").Trim().ToLowerInvariant();
        var language = (input.Language ?? "auto").Trim().ToLowerInvariant();
        settings.Enabled = input.Enabled;
        settings.AutoReply = input.AutoReply;
        settings.ReplyToUnknown = input.ReplyToUnknown;
        // Модель — «провайдер/модель» (anthropic/claude-opus-5-5) или отдельно провайдер и модель; пусто — основная модель консультанта.
        var (provider, model) = ModelRef(input.Model, input.ModelProvider, "Модель — в виде «провайдер/модель», например anthropic/claude-opus-5-5; пусто — основная модель.");
        settings.ModelProvider = provider;
        settings.Model = model;
        // Модель генерации (отчёты, документы, письма) — так же «провайдер/модель», например anthropic/claude-opus-5-5; пусто — Claude из AI Data Manager.
        (settings.GenerationModelProvider, settings.GenerationModel) = ModelRef(input.GenerationModel, input.GenerationModelProvider,
            "Модель генерации — в виде «провайдер/модель», например anthropic/claude-opus-5-5; пусто — модель Claude из AI Data Manager.");
        settings.SystemPrompt = Trim(input.SystemPrompt, 8000);
        settings.Restrictions = Trim(input.Restrictions, 8000);
        settings.ResponseStyle = Styles.Contains(style) ? style : "normal";
        settings.Language = Languages.Contains(language) ? language : "auto";
        settings.MaxMessagesPerHour = Math.Clamp(input.MaxMessagesPerHour, 1, 500);
        settings.MaxOutputTokens = Math.Clamp(input.MaxOutputTokens, 200, 8000);
        settings.ReplyDelaySeconds = Math.Clamp(input.ReplyDelaySeconds, 0, 60);
        settings.NoInfoReply = Trim(input.NoInfoReply, 1000) ?? new TelegramAgentSettings().NoInfoReply;
        settings.AllowedAttachments = string.Join(',', (input.AllowedAttachments ?? string.Empty)
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('.').ToLowerInvariant())
            .Where(x => x.Length is > 0 and <= 8 && x.All(char.IsAsciiLetterOrDigit))
            .Distinct());
        settings.MaxFileMb = Math.Clamp(input.MaxFileMb, 1, 50);
        settings.AllowReports = input.AllowReports;
        settings.Persona = Trim(input.Persona, 1000);
        settings.UpdatedAt = clock.GetUtcNow();
        settings.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.agent.settings_saved", nameof(TelegramAgentSettings), accountId.ToString(),
            new { settings.Enabled, settings.AutoReply, settings.ReplyToUnknown, settings.Model, settings.AllowReports }, ct);
        return settings;
    }

    // ---------- Пользователи Telegram ----------

    public async Task<IReadOnlyList<TelegramPermissionView>> PermissionsAsync(Guid accountId, string? search, string? mode, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        var query = db.TelegramUserPermissions.AsNoTracking().Where(p => p.AccountId == accountId);
        if (Enum.TryParse<TelegramAccessMode>(mode, true, out var m))
        {
            query = query.Where(p => p.Mode == m);
        }

        var rows = await query.OrderBy(p => p.CreatedAt).ToListAsync(ct);
        var users = await UserNamesAsync(rows.Where(p => p.UserId is not null).Select(p => p.UserId!.Value), ct);
        var fullAccessUser = await FullAccessUserAsync(accountId, ct);
        var views = rows.Select(p => PermissionView(p, p.UserId is { } u ? users.GetValueOrDefault(u) : null, fullAccessUser));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().TrimStart('@');
            views = views.Where(v => (v.Username?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                                     || (v.Title?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                                     || (v.OneBaseUser?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                                     || v.TelegramUserId?.ToString(System.Globalization.CultureInfo.InvariantCulture) == s);
        }

        return views.ToList();
    }

    /// <summary>
    /// Добавить или изменить пользователя — по username Telegram. Username сразу ищется в Telegram (аккаунт подключён): права
    /// закрепляются за его Telegram ID, поэтому смена или передача username чужому права не переносит. Аккаунт не подключён — ID
    /// определится по первому сообщению с этого username. Смена Telegram ID или пользователя OneBase начинает чат консультанта заново.
    /// </summary>
    public async Task<TelegramPermissionView> SavePermissionAsync(Guid accountId, Guid? id, TelegramPermissionInput input, Guid userId, CancellationToken ct = default)
    {
        var account = await AccountAsync(accountId, ct);
        var username = NormalizeUsername(input.Username);
        if (input.TelegramUserId is null && username is null)
        {
            throw new TelegramException("Укажите username Telegram, например @ivan_petrov.", TelegramErrorKind.Config);
        }

        if (input.TelegramUserId is <= 0)
        {
            throw new TelegramException("Telegram ID — положительное число.", TelegramErrorKind.Config);
        }

        if (input.UserId is { } linked && !await db.Users.AnyAsync(u => u.Id == linked && u.IsActive, ct))
        {
            throw new TelegramException("Пользователь OneBase не найден или отключён.", TelegramErrorKind.Config);
        }

        var existing = id is { } existingId
            ? await db.TelegramUserPermissions.FirstOrDefaultAsync(p => p.Id == existingId && p.AccountId == accountId, ct) ?? throw new KeyNotFoundException()
            : null;

        // Новый или изменённый username — ищем в Telegram: права — за его ID. Строка создаётся только после проверки.
        var telegramUserId = input.TelegramUserId;
        var title = Trim(input.Title, 200);
        if (username is not null && (telegramUserId is null || username != existing?.Username) && account.Status == TelegramAccountStatus.Connected)
        {
            var found = await gateway.ResolveUsernameAsync(accountId, username, ct)
                        ?? throw new TelegramException($"Пользователь @{username} не найден в Telegram — проверьте username.", TelegramErrorKind.Config);
            if (found.IsBot)
            {
                throw new TelegramException($"@{username} — бот, а не человек.", TelegramErrorKind.Config);
            }

            telegramUserId = found.UserId;
            username = found.Username.ToLowerInvariant();
            title ??= existing?.Title ?? found.DisplayName;
        }

        var selfId = existing?.Id ?? Guid.Empty;
        var duplicate = await db.TelegramUserPermissions.AsNoTracking().AnyAsync(p => p.AccountId == accountId && p.Id != selfId
            && ((telegramUserId != null && p.TelegramUserId == telegramUserId) || (username != null && p.Username == username)), ct);
        if (duplicate)
        {
            throw new TelegramException("Этот пользователь Telegram уже есть в списке.", TelegramErrorKind.Config);
        }

        var permission = existing ?? new TelegramUserPermission { AccountId = accountId, CreatedById = userId };
        if (existing is null)
        {
            db.TelegramUserPermissions.Add(permission);
        }

        if (permission.TelegramUserId != telegramUserId || permission.UserId != input.UserId)
        {
            permission.VerifiedAt = null;
            permission.ConversationId = null;
        }

        permission.TelegramUserId = telegramUserId;
        permission.Username = username;
        permission.Title = title ?? permission.Title;
        permission.Mode = input.Mode;
        permission.UserId = input.UserId;
        permission.AllowReports = input.AllowReports;
        permission.Enabled = input.Enabled;
        permission.UpdatedById = userId;
        permission.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), id is null ? "telegram.permission.created" : "telegram.permission.updated",
            nameof(TelegramUserPermission), permission.Id.ToString(),
            new { accountId, permission.TelegramUserId, permission.Username, Mode = permission.Mode.ToString(), permission.UserId, permission.AllowReports, permission.Enabled }, ct);
        var users = await UserNamesAsync(permission.UserId is { } u ? [u] : [], ct);
        return PermissionView(permission, permission.UserId is { } uid ? users.GetValueOrDefault(uid) : null, await FullAccessUserAsync(accountId, ct));
    }

    /// <summary>
    /// Пользователи, добавленные по username, пока аккаунт был не подключён: найти их Telegram ID (после подключения, раз в 5 минут).
    /// Не найденные остаются ждать первого сообщения.
    /// </summary>
    public async Task<int> ResolvePendingAsync(Guid accountId, CancellationToken ct = default)
    {
        var pending = await db.TelegramUserPermissions.Where(p => p.AccountId == accountId && p.TelegramUserId == null && p.Username != null).Take(20).ToListAsync(ct);
        var resolved = 0;
        foreach (var p in pending)
        {
            var found = await gateway.ResolveUsernameAsync(accountId, p.Username!, ct);
            if (found is null || found.IsBot
                || await db.TelegramUserPermissions.AnyAsync(x => x.AccountId == accountId && x.TelegramUserId == found.UserId && x.Id != p.Id, ct))
            {
                continue;
            }

            p.TelegramUserId = found.UserId;
            p.Title ??= found.DisplayName;
            p.UpdatedAt = clock.GetUtcNow();
            resolved++;
        }

        await db.SaveChangesAsync(ct);
        return resolved;
    }

    // ---------- Кому отвечать ----------

    public async Task<TelegramAccessView> AccessAsync(Guid accountId, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        var settings = await db.TelegramAgentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.AccountId == accountId, ct) ?? new TelegramAgentSettings();
        var name = settings.FullAccessUserId is { } u ? (await UserNamesAsync([u], ct)).GetValueOrDefault(u) : null;
        return new TelegramAccessView(settings.ReplyToUnknown, settings.FullAccessUserId, name);
    }

    /// <summary>
    /// Кому агент отвечает: всем (тем, кого нет в списке, — по отправленным им данным) или только списку; и с чьими правами OneBase
    /// отвечает тем, у кого полный доступ (у пользователя должно быть право на AI-консультанта).
    /// </summary>
    public async Task<TelegramAccessView> SaveAccessAsync(Guid accountId, TelegramAccessInput input, Guid userId, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        if (input.FullAccessUserId is { } fullUser && !await CanAskConsultantAsync(fullUser, ct))
        {
            throw new TelegramException("У этого пользователя OneBase нет права на AI-консультанта (или он отключён) — выберите другого.", TelegramErrorKind.Config);
        }

        var settings = await db.TelegramAgentSettings.FirstOrDefaultAsync(s => s.AccountId == accountId, ct);
        if (settings is null)
        {
            settings = new TelegramAgentSettings { AccountId = accountId };
            db.TelegramAgentSettings.Add(settings);
        }

        settings.ReplyToUnknown = input.ReplyToAll;
        settings.FullAccessUserId = input.FullAccessUserId;
        settings.UpdatedAt = clock.GetUtcNow();
        settings.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.agent.access_saved", nameof(TelegramAgentSettings), accountId.ToString(),
            new { input.ReplyToAll, input.FullAccessUserId }, ct);
        return await AccessAsync(accountId, ct);
    }

    private async Task<bool> CanAskConsultantAsync(Guid userId, CancellationToken ct) =>
        (await OneBase.Application.Security.UserSections.EffectivePermissionsAsync(db, userId, ct))?.Contains(OneBase.Application.Security.Permissions.AgentsRun) == true;

    private async Task<Guid?> FullAccessUserAsync(Guid accountId, CancellationToken ct) =>
        await db.TelegramAgentSettings.AsNoTracking().Where(s => s.AccountId == accountId).Select(s => s.FullAccessUserId).FirstOrDefaultAsync(ct);

    public async Task DeletePermissionAsync(Guid accountId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var permission = await db.TelegramUserPermissions.FirstOrDefaultAsync(p => p.Id == id && p.AccountId == accountId, ct) ?? throw new KeyNotFoundException();
        db.TelegramUserPermissions.Remove(permission);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.permission.deleted", nameof(TelegramUserPermission), id.ToString(),
            new { accountId, permission.TelegramUserId, permission.Username }, ct);
    }

    /// <summary>Массовые действия: enable, disable, sent_only, delete. Полный доступ массово не выдаётся — только по одному, с пользователем OneBase.</summary>
    public async Task<int> BulkAsync(Guid accountId, TelegramBulkInput input, Guid userId, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        var ids = input.Ids.Distinct().Take(1000).ToList();
        var rows = await db.TelegramUserPermissions.Where(p => p.AccountId == accountId && ids.Contains(p.Id)).ToListAsync(ct);
        foreach (var p in rows)
        {
            switch (input.Action)
            {
                case "enable":
                    p.Enabled = true;
                    break;
                case "disable":
                    p.Enabled = false;
                    break;
                case "sent_only":
                    p.Mode = TelegramAccessMode.SentOnly;
                    break;
                case "delete":
                    db.TelegramUserPermissions.Remove(p);
                    break;
                default:
                    throw new TelegramException("Неизвестное действие.", TelegramErrorKind.Config);
            }

            p.UpdatedById = userId;
            p.UpdatedAt = clock.GetUtcNow();
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.permission.bulk", nameof(TelegramUserPermission), accountId.ToString(),
            new { input.Action, Count = rows.Count }, ct);
        return rows.Count;
    }

    /// <summary>Активные пользователи OneBase — для выбора, чьи права наследует полный доступ (имя, логин, должность; без контактов).</summary>
    public async Task<IReadOnlyList<TelegramOneBaseUser>> OneBaseUsersAsync(string? search, CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = "%" + search.Trim().Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(u => EF.Functions.ILike(u.FullName, s) || EF.Functions.ILike(u.Login, s));
        }

        return await query.OrderBy(u => u.FullName).Take(50).Select(u => new TelegramOneBaseUser(u.Id, u.FullName, u.Login, u.Position)).ToListAsync(ct);
    }

    // ---------- Чаты ----------

    /// <summary>Загрузить чаты аккаунта из Telegram: новые добавляются, известные обновляются, пропавшие помечаются «писать нельзя».</summary>
    public async Task<int> SyncChatsAsync(Guid accountId, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        return await TelegramChatSync.SyncAsync(db, gateway, accountId, clock.GetUtcNow(), ct);
    }

    /// <summary>Чаты загружались давно (или ни разу) — обновить: новые чаты и причины «писать нельзя» появляются сами.</summary>
    public async Task<bool> SyncChatsIfStaleAsync(Guid accountId, TimeSpan maxAge, CancellationToken ct = default)
    {
        var last = await db.TelegramChats.AsNoTracking().Where(c => c.AccountId == accountId).MaxAsync(c => c.SyncedAt, ct);
        var stale = last is null || clock.GetUtcNow() - last > maxAge
                    || await db.TelegramChats.AnyAsync(c => c.AccountId == accountId && !c.CanSend && c.CannotSendReason == null, ct);
        if (stale)
        {
            await SyncChatsAsync(accountId, ct);
        }

        return stale;
    }

    public async Task<IReadOnlyList<TelegramChatView>> ChatsAsync(Guid accountId, string? search, string? type, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        var query = db.TelegramChats.AsNoTracking().Where(c => c.AccountId == accountId);
        if (Enum.TryParse<TelegramChatType>(type, true, out var t))
        {
            query = query.Where(c => c.Type == t);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = "%" + search.Trim().TrimStart('@').Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(c => EF.Functions.ILike(c.Title, s) || (c.Username != null && EF.Functions.ILike(c.Username, s)));
        }

        return await query.OrderByDescending(c => c.CanSend).ThenBy(c => c.Title).Take(2000)
            .Select(c => new TelegramChatView(c.Id, c.PeerId, c.Type.ToString(), c.Title, c.Username, c.CanSend, c.AgentEnabled, c.SyncedAt, c.CannotSendReason))
            .ToListAsync(ct);
    }

    /// <summary>Включить агента в группе: там он отвечает только на упоминание или ответ на своё сообщение.</summary>
    public async Task<TelegramChatView> SetChatAgentAsync(Guid accountId, Guid chatId, bool enabled, Guid userId, CancellationToken ct = default)
    {
        var chat = await db.TelegramChats.FirstOrDefaultAsync(c => c.Id == chatId && c.AccountId == accountId, ct) ?? throw new KeyNotFoundException();
        chat.AgentEnabled = enabled;
        chat.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.chat.agent", nameof(TelegramChat), chatId.ToString(), new { accountId, chat.Title, enabled }, ct);
        return new TelegramChatView(chat.Id, chat.PeerId, chat.Type.ToString(), chat.Title, chat.Username, chat.CanSend, chat.AgentEnabled, chat.SyncedAt, chat.CannotSendReason);
    }

    // ---------- Журнал агента ----------

    public async Task<IReadOnlyList<TelegramExecutionView>> ExecutionsAsync(Guid accountId, string? status, CancellationToken ct = default)
    {
        await AccountAsync(accountId, ct);
        var query = db.TelegramAgentExecutions.AsNoTracking().Where(e => e.AccountId == accountId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status == status);
        }

        var rows = await query.OrderByDescending(e => e.CreatedAt).Take(200).ToListAsync(ct);
        var peers = rows.Select(r => r.PeerId).Distinct().ToList();
        var chats = await db.TelegramChats.AsNoTracking().Where(c => c.AccountId == accountId && peers.Contains(c.PeerId))
            .ToDictionaryAsync(c => c.PeerId, c => c.Title, ct);
        var tgUsers = rows.Where(r => r.TelegramUserId is not null).Select(r => r.TelegramUserId).Distinct().ToList();
        var who = await db.TelegramUserPermissions.AsNoTracking().Where(p => p.AccountId == accountId && tgUsers.Contains(p.TelegramUserId))
            .ToDictionaryAsync(p => p.TelegramUserId!.Value, p => p.Title ?? (p.Username != null ? "@" + p.Username : null), ct);
        return rows.Select(r => new TelegramExecutionView(r.Id, r.CreatedAt, r.PeerId, chats.GetValueOrDefault(r.PeerId), r.TelegramUserId,
            r.TelegramUserId is { } tu ? who.GetValueOrDefault(tu) : null, r.Mode, r.Status, r.Question, r.Answer, r.ReportId, r.Model,
            r.InputTokens, r.OutputTokens, r.DurationMs, r.Error)).ToList();
    }

    // ---------- Привязка Telegram к пользователю OneBase ----------

    /// <summary>
    /// Новый код привязки для пользователя OneBase (прежние неиспользованные гаснут). Пользователь отправляет его подключённому аккаунту
    /// в Telegram — так подтверждается, что этот Telegram принадлежит ему. Код в открытом виде показывается один раз.
    /// </summary>
    public async Task<TelegramLinkCodeView> CreateLinkCodeAsync(Guid userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var recent = await db.TelegramLinkCodes.CountAsync(c => c.UserId == userId && c.CreatedAt > now.AddHours(-1), ct);
        if (recent >= 10)
        {
            throw new TelegramException("Слишком много кодов за час — попробуйте позже.", TelegramErrorKind.Flood, 3600);
        }

        await db.TelegramLinkCodes.Where(c => c.UserId == userId && c.UsedAt == null && c.ExpiresAt > now)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.ExpiresAt, now), ct);
        var code = TelegramRules.NewLinkCode();
        var row = new TelegramLinkCode { UserId = userId, CodeHash = TelegramRules.HashLinkCode(code), ExpiresAt = now + TelegramRules.LinkCodeLifetime };
        db.TelegramLinkCodes.Add(row);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.link.code_created", nameof(TelegramLinkCode), row.Id.ToString(), null, ct);

        var sendTo = await db.TelegramAccounts.AsNoTracking()
            .Where(a => a.Status == TelegramAccountStatus.Connected)
            .Join(db.TelegramAgentSettings.Where(s => s.Enabled), a => a.Id, s => s.AccountId, (a, _) => a)
            .Select(a => a.Username != null ? "@" + a.Username : a.DisplayName ?? a.Name)
            .ToListAsync(ct);
        return new TelegramLinkCodeView(code, row.ExpiresAt, sendTo);
    }

    /// <summary>Привязки пользователя OneBase: в каких аккаунтах его Telegram подтверждён.</summary>
    public async Task<IReadOnlyList<TelegramLinkView>> LinksAsync(Guid userId, CancellationToken ct = default) =>
        await db.TelegramUserPermissions.AsNoTracking()
            .Where(p => p.UserId == userId && p.VerifiedAt != null)
            .OrderBy(p => p.VerifiedAt)
            .Select(p => new TelegramLinkView(p.Id, p.Account!.Name, p.Account.Username, p.TelegramUserId, p.Username, p.Mode.ToString(), p.VerifiedAt))
            .ToListAsync(ct);

    /// <summary>Пользователь сам отвязывает свой Telegram: полный доступ перестаёт действовать до новой привязки.</summary>
    public async Task UnlinkAsync(Guid userId, Guid permissionId, CancellationToken ct = default)
    {
        var permission = await db.TelegramUserPermissions.FirstOrDefaultAsync(p => p.Id == permissionId && p.UserId == userId, ct) ?? throw new KeyNotFoundException();
        permission.VerifiedAt = null;
        permission.ConversationId = null;
        permission.UpdatedAt = clock.GetUtcNow();
        permission.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.link.removed", nameof(TelegramUserPermission), permissionId.ToString(), null, ct);
    }

    // ---------- Вспомогательное ----------

    private async Task<TelegramAccount> AccountAsync(Guid id, CancellationToken ct) =>
        await db.TelegramAccounts.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new KeyNotFoundException();

    private async Task<TelegramAccountView> ViewAsync(TelegramAccount account, CancellationToken ct)
    {
        var agent = await db.TelegramAgentSettings.AsNoTracking().Where(s => s.AccountId == account.Id).Select(s => s.Enabled).FirstOrDefaultAsync(ct);
        var permissions = await db.TelegramUserPermissions.CountAsync(p => p.AccountId == account.Id, ct);
        var chats = await db.TelegramChats.CountAsync(c => c.AccountId == account.Id, ct);
        return View(account, agent, permissions, chats);
    }

    private TelegramAccountView View(TelegramAccount a, bool agent, int permissions, int chats) =>
        new(a.Id, a.Name, a.PhoneHint, a.Status.ToString(), a.TelegramUserId, a.Username, a.DisplayName, a.ConnectedAt, a.LastActivityAt, a.LastError,
            agent, permissions, chats, gateway.LiveStatus(a.Id));

    /// <summary>
    /// Действует ли строка: disabled — выключена; pending — Telegram ID ещё не известен (определится по первому сообщению с username);
    /// no_rights — полный доступ, но не выбран пользователь OneBase, с чьими правами отвечать (агент ответит по отправленным данным); active.
    /// </summary>
    public static string PermissionState(TelegramUserPermission p, Guid? fullAccessUserId) =>
        !p.Enabled ? "disabled"
        : p.TelegramUserId is null ? "pending"
        : p.Mode == TelegramAccessMode.Full && (p.UserId ?? fullAccessUserId) is null ? "no_rights"
        : "active";

    private static TelegramPermissionView PermissionView(TelegramUserPermission p, string? oneBaseUser, Guid? fullAccessUserId) =>
        new(p.Id, p.TelegramUserId, p.Username, p.Title, p.Mode.ToString(), p.UserId, oneBaseUser, p.AllowReports, p.Enabled, p.VerifiedAt,
            PermissionState(p, fullAccessUserId), p.CreatedAt);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return list.Count == 0 ? [] : await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static string CleanName(string? name)
    {
        var clean = Trim(name, 100);
        return clean ?? throw new TelegramException("Укажите название аккаунта (например, «Отдел продаж»).", TelegramErrorKind.Config);
    }

    public static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 10 or > 15)
        {
            throw new TelegramException("Номер телефона в международном формате, например +998 90 123 45 67.", TelegramErrorKind.Config);
        }

        return "+" + digits;
    }

    public static string? NormalizeUsername(string? username)
    {
        var clean = username?.Trim().TrimStart('@').ToLowerInvariant();
        if (string.IsNullOrEmpty(clean))
        {
            return null;
        }

        if (clean.StartsWith("https://t.me/", StringComparison.Ordinal) || clean.StartsWith("t.me/", StringComparison.Ordinal))
        {
            clean = clean[(clean.IndexOf("t.me/", StringComparison.Ordinal) + 5)..].Trim('/');
        }

        return System.Text.RegularExpressions.Regex.IsMatch(clean, "^[a-z0-9_]{4,32}$")
            ? clean
            : throw new TelegramException("Username Telegram — 4–32 символа: латиница, цифры, «_».", TelegramErrorKind.Config);
    }

    private static string? Trim(string? value, int max)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length > max ? t[..max] : t;
    }

    /// <summary>Модель из настроек: «провайдер/модель» одной строкой или отдельно; пусто — (null, null).</summary>
    private static (string? Provider, string? Model) ModelRef(string? value, string? providerValue, string error)
    {
        var model = Trim(value, 140);
        var provider = Trim(providerValue, 32);
        if (model is not null && model.Contains('/'))
        {
            provider = model[..model.IndexOf('/')].Trim().ToLowerInvariant();
            model = model[(model.IndexOf('/') + 1)..].Trim();
        }

        if (model is null)
        {
            return (null, null);
        }

        return provider is null ? throw new TelegramException(error, TelegramErrorKind.Config) : (provider, Trim(model, 100));
    }
}

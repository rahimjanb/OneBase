using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Hh;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Recruitment;

/// <summary>AiOnHhData: null — не менять (передавать ли AI резюме, полученные через API HH; включает HR после подтверждения юриста).</summary>
public sealed record HhSettingsInput(string? ClientId, string? ClientSecret, string? RedirectUri, string? ContactEmail, bool Enabled, bool? AiOnHhData = null);

public sealed record HhStatus(bool Configured, bool Connected, bool Enabled, string? ClientId, bool HasSecret, string? RedirectUri, string? ContactEmail,
    string? EmployerName, DateTimeOffset? TokenExpiresAt, DateTimeOffset? LastSyncAt, bool? LastSyncOk, string? LastSyncMessage, int LinkedVacancies,
    DateTimeOffset? GuardTrippedAt = null, string? GuardReason = null, bool AiOnHhData = false);

public sealed record HhVacancyLink(string HhVacancyId, string Name, string? Area, Guid? VacancyId, string? VacancyCode);

public sealed record HhSyncResult(int Vacancies, int Responses, int NewCandidates, int Errors, string Message);

public sealed record HhNegotiationView(Guid Id, string NegotiationId, string? State, DateTimeOffset? HhUpdatedAt, string? LastError, IReadOnlyList<HhAction> Actions);

/// <summary>
/// HH.uz для одного аккаунта работодателя: настройки приложения, вход OAuth (state проверяется, токены и client_secret — зашифрованы),
/// обновление токена после истечения, вакансии работодателя и связь с вакансиями OneBase, синхронизация откликов в единую базу кандидатов,
/// действия по отклику — только из actions[] ответа HH. Каждый запрос — через Quota Guard (<see cref="HhQuotaGuard"/>): платные и
/// расходующие лимиты операции (просмотр резюме, поиск, контакты, сообщения) не выполняются — кандидат из отклика заводится по бесплатным
/// полям списка (имя, должность, город, зарплата, стаж, ссылка на hh.uz), резюме HR загружает файлом.
/// </summary>
public sealed class HhService(
    OneBaseDbContext db,
    IHhApi api,
    HhQuotaGuard guard,
    IDataProtectionProvider protection,
    RecruitmentService recruitment,
    RecruitmentExceptions exceptions,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<HhService> logger)
{
    private const int MaxPages = 20;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _secret = protection.CreateProtector(ProtectionPurposes.HhClientSecret);
    private readonly IDataProtector _tokens = protection.CreateProtector(ProtectionPurposes.HhTokens);

    private static void Manage(RecruitmentContext ctx)
    {
        if (!ctx.CanManage)
        {
            throw new FieldForbiddenException("HH.uz настраивает HR (право «Подбор персонала: управление»).");
        }
    }

    private async Task<HhConnection> RowAsync(CancellationToken ct)
    {
        var row = await db.HhConnections.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (row is null)
        {
            row = new HhConnection();
            db.HhConnections.Add(row);
            await db.SaveChangesAsync(ct);
        }

        return row;
    }

    private HhApp App(HhConnection row) =>
        row.ClientId is { Length: > 0 } id && row.ProtectedClientSecret is { } secret && row.RedirectUri is { Length: > 0 } redirect && row.ContactEmail is { Length: > 0 } email
            ? new HhApp(id, _secret.Unprotect(secret), redirect, email)
            : throw new FieldValidationException("HH.uz не настроен: укажите client_id, client_secret, redirect_uri и почту разработчика.");

    public async Task<HhStatus> StatusAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        var row = await RowAsync(ct);
        return new HhStatus(row.ClientId is not null && row.ProtectedClientSecret is not null && row.RedirectUri is not null && row.ContactEmail is not null,
            row.ProtectedAccessToken is not null, row.Enabled, row.ClientId, row.ProtectedClientSecret is not null, row.RedirectUri, row.ContactEmail,
            row.EmployerName, row.TokenExpiresAt, row.LastSyncAt, row.LastSyncOk, row.LastSyncMessage,
            await db.Vacancies.CountAsync(v => v.HhVacancyId != null, ct), row.GuardTrippedAt, row.GuardReason, row.AiOnHhData);
    }

    /// <summary>Настройки приложения HH. Пустой client_secret — оставить прежний (наружу он не отдаётся).</summary>
    public async Task<HhStatus> SaveSettingsAsync(RecruitmentContext ctx, HhSettingsInput input, CancellationToken ct)
    {
        Manage(ctx);
        var row = await RowAsync(ct);
        var email = input.ContactEmail?.Trim();
        if (email is { Length: > 0 } && RecruitmentRules.Normalize(CandidateContactKind.Email, email) is null)
        {
            throw new FieldValidationException("Почта разработчика — в формате name@example.com (HH требует её в заголовке HH-User-Agent).");
        }

        var redirect = input.RedirectUri?.Trim();
        if (redirect is { Length: > 0 } && !(Uri.TryCreate(redirect, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            throw new FieldValidationException("redirect_uri — адрес https, как в настройках приложения на dev.hh.ru.");
        }

        var clientChanged = row.ClientId != input.ClientId?.Trim();
        row.ClientId = input.ClientId?.Trim() is { Length: > 0 } id ? id : null;
        if (!string.IsNullOrWhiteSpace(input.ClientSecret))
        {
            row.ProtectedClientSecret = _secret.Protect(input.ClientSecret.Trim());
            clientChanged = true;
        }

        row.RedirectUri = redirect is { Length: > 0 } ? redirect : null;
        row.ContactEmail = email is { Length: > 0 } ? email : null;
        row.Enabled = input.Enabled;
        row.AiOnHhData = input.AiOnHhData ?? row.AiOnHhData;
        row.UpdatedAt = clock.GetUtcNow();
        if (clientChanged)
        {
            // Другое приложение — прежние токены к нему не подходят.
            (row.ProtectedAccessToken, row.ProtectedRefreshToken, row.TokenExpiresAt) = (null, null, null);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.hh.settings", nameof(HhConnection), "1", new { row.ClientId, row.Enabled, clientChanged, row.AiOnHhData }, ct);
        return await StatusAsync(ctx, ct);
    }

    /// <summary>Начать вход: адрес hh.ru/oauth/authorize с одноразовым state (15 минут; в базе — только хэш).</summary>
    public async Task<string> StartConnectAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Manage(ctx);
        var row = await RowAsync(ct);
        var app = App(row);
        var state = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        row.StateHash = Hash(state);
        row.StateExpiresAt = clock.GetUtcNow().AddMinutes(15);
        await db.SaveChangesAsync(ct);
        return api.AuthorizeUrl(app.ClientId, app.RedirectUri, state);
    }

    /// <summary>Завершить вход: state совпал и не истёк — код меняется на токены, работодатель — из /me.</summary>
    public async Task<HhStatus> CompleteConnectAsync(RecruitmentContext ctx, string code, string state, CancellationToken ct)
    {
        Manage(ctx);
        var row = await RowAsync(ct);
        if (row.StateHash is null || row.StateHash != Hash(state ?? string.Empty) || row.StateExpiresAt < clock.GetUtcNow())
        {
            throw new FieldValidationException("Ответ HH не подходит к начатому входу (или прошло больше 15 минут) — нажмите «Подключить HH» ещё раз.");
        }

        var app = App(row);
        using var scope = HhCallScope.Begin(true, ctx.UserId);
        var tokens = await guard.RunAsync(HhOperations.Token, "/token", () => api.ExchangeCodeAsync(app, code, ct), ct);
        var me = await guard.RunAsync(HhOperations.Me, "/me", () => api.MeAsync(app, tokens.AccessToken, ct), ct);
        if (me.EmployerId is null)
        {
            throw new FieldValidationException("Вошли не работодателем: подключите HH под аккаунтом менеджера работодателя.");
        }

        Store(row, tokens);
        (row.StateHash, row.StateExpiresAt) = (null, null);
        (row.EmployerId, row.EmployerName, row.ConnectedAt, row.ConnectedById) = (me.EmployerId, me.EmployerName, clock.GetUtcNow(), ctx.UserId);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.hh.connected", nameof(HhConnection), "1", new { row.EmployerId, row.EmployerName }, ct);
        return await StatusAsync(ctx, ct);
    }

    public async Task<HhStatus> DisconnectAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Manage(ctx);
        var row = await RowAsync(ct);
        (row.ProtectedAccessToken, row.ProtectedRefreshToken, row.TokenExpiresAt, row.EmployerId, row.EmployerName) = (null, null, null, null, null);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.hh.disconnected", nameof(HhConnection), "1", null, ct);
        return await StatusAsync(ctx, ct);
    }

    private void Store(HhConnection row, HhTokens tokens)
    {
        row.ProtectedAccessToken = _tokens.Protect(tokens.AccessToken);
        row.ProtectedRefreshToken = string.IsNullOrEmpty(tokens.RefreshToken) ? row.ProtectedRefreshToken : _tokens.Protect(tokens.RefreshToken);
        row.TokenExpiresAt = tokens.ExpiresAt;
        row.UpdatedAt = clock.GetUtcNow();
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Действующий токен: истёк — обновляется (HH разрешает обновлять только истёкший, refresh-токен — одноразовый).</summary>
    private async Task<(HhApp App, string Token)> TokenAsync(HhConnection row, bool forceRefresh, CancellationToken ct)
    {
        var app = App(row);
        if (row.ProtectedAccessToken is null)
        {
            throw new FieldValidationException("HH.uz не подключён — нажмите «Подключить HH».");
        }

        if ((forceRefresh || row.TokenExpiresAt <= clock.GetUtcNow()) && row.ProtectedRefreshToken is { } refresh)
        {
            Store(row, await guard.RunAsync(HhOperations.Token, "/token", () => api.RefreshAsync(app, _tokens.Unprotect(refresh), ct), ct));
            await db.SaveChangesAsync(ct);
        }

        return (app, _tokens.Unprotect(row.ProtectedAccessToken));
    }

    /// <summary>Запрос к HH с токеном через Quota Guard; ответ «токен истёк» — обновить и повторить один раз.</summary>
    private async Task<T> CallAsync<T>(HhConnection row, string operation, string path, Func<HhApp, string, Task<T>> call, CancellationToken ct)
    {
        var (app, token) = await TokenAsync(row, false, ct);
        try
        {
            return await guard.RunAsync(operation, path, () => call(app, token), ct);
        }
        catch (HhApiException ex) when (ex.TokenExpired)
        {
            (app, token) = await TokenAsync(row, true, ct);
            return await guard.RunAsync(operation, path, () => call(app, token), ct);
        }
    }

    // ---------- Вакансии ----------

    public async Task<IReadOnlyList<HhVacancyLink>> VacanciesAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Manage(ctx);
        var row = await RowAsync(ct);
        var employer = row.EmployerId ?? throw new FieldValidationException("HH.uz не подключён — нажмите «Подключить HH».");
        using var scope = HhCallScope.Begin(true, ctx.UserId);
        var list = new List<HhVacancyRef>();
        for (var page = 0; page < MaxPages; page++)
        {
            var result = await CallAsync(row, HhOperations.Vacancies, $"/employers/{employer}/vacancies/active",
                (app, token) => api.ActiveVacanciesAsync(app, token, employer, page, ct), ct);
            list.AddRange(result.Items);
            if (page + 1 >= result.Pages)
            {
                break;
            }
        }

        var ids = list.Select(v => v.Id).ToList();
        var linked = await db.Vacancies.AsNoTracking().Where(v => v.HhVacancyId != null && ids.Contains(v.HhVacancyId)).ToListAsync(ct);
        return list.Select(v =>
        {
            var own = linked.FirstOrDefault(x => x.HhVacancyId == v.Id);
            return new HhVacancyLink(v.Id, v.Name, v.Area, own?.Id, own?.Code);
        }).ToList();
    }

    /// <summary>Связать вакансию OneBase с вакансией HH (hhVacancyId = null — отвязать). Одна вакансия HH — одной вакансии OneBase.</summary>
    public async Task LinkAsync(RecruitmentContext ctx, Guid vacancyId, string? hhVacancyId, CancellationToken ct)
    {
        Manage(ctx);
        var vacancy = await db.Vacancies.FirstOrDefaultAsync(v => v.Id == vacancyId, ct) ?? throw new FieldNotFoundException("Вакансия не найдена.");
        var hh = hhVacancyId?.Trim() is { Length: > 0 } h ? h : null;
        if (hh is not null && await db.Vacancies.AnyAsync(v => v.HhVacancyId == hh && v.Id != vacancyId, ct))
        {
            throw new FieldConflictException("Эта вакансия HH уже связана с другой вакансией OneBase.");
        }

        vacancy.HhVacancyId = hh;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.hh.linked", nameof(Vacancy), vacancyId.ToString(), new { hh }, ct);
    }

    // ---------- Синхронизация откликов ----------

    /// <summary>
    /// Отклики по связанным открытым вакансиям (бесплатный список): новый отклик — кандидат в единую базу (тот же ID резюме — тот же человек)
    /// по полям списка, отклик на вакансию; состояние и действия — обновляются. Резюме не загружается (просмотр расходует лимит HH) — попытка
    /// пишется в журнал как заблокированная. Ошибка HH — в исключения HR, остальные отклики продолжаются. actorId — синхронизацию запустил HR;
    /// null — по расписанию (стоит предохранитель — не ходим в HH вовсе).
    /// </summary>
    public async Task<HhSyncResult> SyncAsync(Guid? actorId, CancellationToken ct)
    {
        var row = await RowAsync(ct);
        if (!row.Enabled || row.ProtectedAccessToken is null)
        {
            return new HhSyncResult(0, 0, 0, 0, "HH.uz не подключён или выключен.");
        }

        if (actorId is null && row.GuardTrippedAt is not null)
        {
            return new HhSyncResult(0, 0, 0, 0, $"Синхронизация остановлена предохранителем: {row.GuardReason}");
        }

        using var scope = HhCallScope.Begin(actorId is not null, actorId);

        var vacancies = await db.Vacancies.AsNoTracking()
            .Where(v => v.HhVacancyId != null && (v.Status == VacancyStatus.Open || v.Status == VacancyStatus.Paused)).ToListAsync(ct);
        int responses = 0, created = 0, errors = 0;
        string? lastError = null;
        foreach (var vacancy in vacancies)
        {
            try
            {
                for (var page = 0; page < MaxPages; page++)
                {
                    var result = await CallAsync(row, HhOperations.Negotiations, "/negotiations/response",
                        (app, token) => api.ResponsesAsync(app, token, vacancy.HhVacancyId!, page, ct), ct);
                    foreach (var item in result.Items.Where(i => i.Id.Length > 0 && i.ResumeId.Length > 0))
                    {
                        responses++;
                        try
                        {
                            if (await NegotiationAsync(row, vacancy, item, actorId, ct))
                            {
                                created++;
                            }
                        }
                        catch (HhApiException ex)
                        {
                            errors++;
                            lastError = ex.Message;
                            await db.HhNegotiations.Where(n => n.NegotiationId == item.Id).ExecuteUpdateAsync(u => u.SetProperty(n => n.LastError, ex.Message), ct);
                        }
                    }

                    if (page + 1 >= result.Pages)
                    {
                        break;
                    }
                }
            }
            catch (HhApiException ex)
            {
                errors++;
                lastError = ex.Message;
                logger.LogWarning("HH: отклики вакансии {Vacancy} не получены — {Error}", vacancy.Code, ex.Message);
            }
            catch (HhBlockedException ex)
            {
                errors++;
                lastError = ex.Message;
                break; // Quota Guard остановил — дальше не идём
            }
        }

        if (lastError is not null)
        {
            await exceptions.RaiseAsync("hh", lastError, dedupKey: "hh:sync", ct: ct);
        }

        var message = errors == 0
            ? $"Вакансий {vacancies.Count}, откликов {responses}, новых кандидатов {created}."
            : $"Вакансий {vacancies.Count}, откликов {responses}, новых кандидатов {created}, ошибок {errors}: {lastError}";
        row.LastSyncAt = clock.GetUtcNow();
        row.LastSyncOk = errors == 0;
        row.LastSyncMessage = message.Length <= 1000 ? message : message[..1000];
        await db.SaveChangesAsync(ct);
        return new HhSyncResult(vacancies.Count, responses, created, errors, message);
    }

    /// <summary>Один отклик: новый — резюме и кандидат; известный — состояние и действия. Возвращает, заведён ли новый кандидат.</summary>
    private async Task<bool> NegotiationAsync(HhConnection row, Vacancy vacancy, HhNegotiationItem item, Guid? actorId, CancellationToken ct)
    {
        var negotiation = await db.HhNegotiations.FirstOrDefaultAsync(n => n.NegotiationId == item.Id, ct);
        if (negotiation is null)
        {
            negotiation = new HhNegotiation { NegotiationId = item.Id, HhVacancyId = vacancy.HhVacancyId!, VacancyId = vacancy.Id, ResumeId = item.ResumeId };
            db.HhNegotiations.Add(negotiation);
        }

        negotiation.State = item.State;
        negotiation.HhUpdatedAt = item.UpdatedAt;
        negotiation.ResumeUrl = item.ResumeUrl;
        negotiation.ActionsJson = JsonSerializer.Serialize(item.Actions, Json);
        negotiation.LastError = null;
        await db.SaveChangesAsync(ct);
        if (negotiation.CandidateId is not null && await db.Candidates.AnyAsync(c => c.Id == negotiation.CandidateId, ct))
        {
            return false;
        }

        // Полное резюме не загружаем: просмотр через API расходует дневной лимит просмотров HH (правило — лимиты не расходовать). Попытка — в
        // журнал как заблокированная (один раз на отклик: дальше кандидат уже заведён). Разрешит каталог — загрузится здесь.
        HhResume? resume = null;
        string? blocked = null;
        if (negotiation.ResumeFetchedAt is null && item.ResumeUrl is { } url)
        {
            try
            {
                resume = await CallAsync(row, HhOperations.ResumeView, new Uri(url).AbsolutePath, (app, token) => api.ResumeAsync(app, token, url, ct), ct);
                negotiation.ResumeFetchedAt = clock.GetUtcNow();
            }
            catch (HhBlockedException ex)
            {
                blocked = ex.Message;
            }
        }

        var name = string.Join(' ', new[] { resume?.LastName ?? item.LastName, resume?.FirstName ?? item.FirstName, resume?.MiddleName ?? item.MiddleName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var contacts = new List<IntakeContact>();
        foreach (var c in resume?.Contacts ?? [])
        {
            var kind = c.Kind.Contains("mail", StringComparison.OrdinalIgnoreCase) ? CandidateContactKind.Email
                : c.Kind is "phone" or "cell" or "work" or "home" ? CandidateContactKind.Phone
                : (CandidateContactKind?)null;
            if (kind is { } k && RecruitmentRules.Normalize(k, c.Value) is not null)
            {
                contacts.Add(new IntakeContact(k, c.Value!, c.Verified));
            }
        }

        var site = item.AlternateUrl ?? HhLinks.Resume(item.ResumeId);
        var intake = await recruitment.IntakeAsync(new CandidateIntake(name.Length > 0 ? name : $"Кандидат HH {item.ResumeId[..Math.Min(8, item.ResumeId.Length)]}",
            resume?.Area ?? item.Area, null, null, contacts, "hh", item.ResumeId, Cut(site, 500), vacancy.Id, "hh"), actorId, ct);
        negotiation.CandidateId = intake.CandidateId;
        negotiation.ApplicationId = await db.CandidateApplications.AsNoTracking().Where(a => a.CandidateId == intake.CandidateId && a.VacancyId == vacancy.Id)
            .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        negotiation.LastError = blocked is null ? null : "Резюме не загружено через API (расходует лимит просмотров HH): откройте его на hh.uz и загрузите файлом.";
        await db.SaveChangesAsync(ct);

        // Бесплатные поля списка — в сводку кандидата (прежнее не затирается).
        if (await db.Candidates.FirstOrDefaultAsync(c => c.Id == intake.CandidateId, ct) is { } candidate)
        {
            candidate.DesiredPosition ??= Cut(item.Title ?? resume?.Title, 200);
            candidate.SalaryExpectation ??= Cut(item.Salary, 100);
            candidate.ExperienceMonths ??= item.ExperienceMonths;
            await db.SaveChangesAsync(ct);
        }

        if (resume is { Text.Length: > 0 })
        {
            var bytes = Encoding.UTF8.GetBytes(resume.Text);
            try
            {
                await recruitment.AddResumeAsync(intake.CandidateId, new RecruitmentUpload($"HH — {resume.Title ?? "резюме"}.txt", "text/plain", bytes.Length, new MemoryStream(bytes)),
                    "hh", actorId, ct);
            }
            catch (FieldConflictException)
            {
                // то же резюме уже есть
            }
        }

        return intake.Created;
    }

    public async Task<IReadOnlyList<HhNegotiationView>> NegotiationsAsync(RecruitmentContext ctx, Guid candidateId, CancellationToken ct)
    {
        if (!ctx.SeesCandidates)
        {
            throw new FieldForbiddenException("Нет доступа к подбору.");
        }

        var rows = await db.HhNegotiations.AsNoTracking().Where(n => n.CandidateId == candidateId).ToListAsync(ct);
        return rows.Select(n => new HhNegotiationView(n.Id, n.NegotiationId, n.State, n.HhUpdatedAt, n.LastError, Actions(n))).ToList();
    }

    // ---------- Quota Guard ----------

    public async Task<HhGuardView> GuardAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Manage(ctx);
        return await guard.ViewAsync(ct);
    }

    /// <summary>«Проверить снова»: HR снимает предохранитель (например, после разговора с HH); следующий круг синхронизации идёт как обычно.</summary>
    public async Task<HhGuardView> ResetGuardAsync(RecruitmentContext ctx, CancellationToken ct)
    {
        Manage(ctx);
        var row = await RowAsync(ct);
        var reason = row.GuardReason;
        (row.GuardTrippedAt, row.GuardReason) = (null, null);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.hh.guard_reset", nameof(HhConnection), "1", new { reason }, ct);
        return await guard.ViewAsync(ct);
    }

    private static string? Cut(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    private static IReadOnlyList<HhAction> Actions(HhNegotiation n) =>
        string.IsNullOrEmpty(n.ActionsJson) ? [] : JsonSerializer.Deserialize<List<HhAction>>(n.ActionsJson, Json) ?? [];

    /// <summary>Действие по отклику на HH (пригласить, отказать…): только из списка, который HH отдал для этого отклика.</summary>
    public async Task<IReadOnlyList<HhNegotiationView>> ActionAsync(RecruitmentContext ctx, Guid negotiationRowId, string actionId, string? message, CancellationToken ct)
    {
        Manage(ctx);
        var negotiation = await db.HhNegotiations.FirstOrDefaultAsync(n => n.Id == negotiationRowId, ct) ?? throw new FieldNotFoundException("Отклик HH не найден.");
        var action = Actions(negotiation).FirstOrDefault(a => a.Id == actionId)
                     ?? throw new FieldValidationException("Этого действия HH для отклика не предлагает — обновите отклики (синхронизация).");
        var row = await RowAsync(ct);
        using var scope = HhCallScope.Begin(true, ctx.UserId);
        try
        {
            // Сообщение через HH — отдельная операция каталога (запрещена правилом); действие — только без текста.
            await CallAsync(row, string.IsNullOrWhiteSpace(message) ? HhOperations.Action : HhOperations.ActionMessage, new Uri(action.Url).AbsolutePath,
                async (app, token) =>
                {
                    await api.ExecuteActionAsync(app, token, action, null, ct);
                    return true;
                }, ct);
        }
        catch (HhBlockedException ex)
        {
            throw new FieldValidationException(ex.Message);
        }

        negotiation.State = action.Id;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "recruitment.hh.action", nameof(HhNegotiation), negotiation.Id.ToString(), new { action = action.Id }, ct);
        return await NegotiationsAsync(ctx, negotiation.CandidateId ?? Guid.Empty, ct);
    }
}

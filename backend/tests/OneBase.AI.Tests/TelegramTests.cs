using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Providers;
using OneBase.AI.Reports;
using OneBase.AI.Telegram;
using OneBase.Application.Reports;
using OneBase.Application.Security;
using OneBase.Application.Telegram;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Telegram на подставном клиенте (без сети и настоящих отправок): вход по коду и 2FA без хранения секретов, привязка личности кодом,
/// режимы доступа агента и изоляция материалов по чатам, защита от петель и повторов, рассылки — очередь, повторы, пауза, отмена,
/// перезапуск без дублей.
/// </summary>
public class TelegramTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly TelegramPeerRef Ivan = new(5001, TelegramChatType.Private, false, 11);
    private static readonly TelegramPeerRef Dealer = new(5002, TelegramChatType.Private, false, 22);
    private static readonly TelegramPeerRef Olga = new(5004, TelegramChatType.Private, false, 44);
    private static readonly TelegramPeerRef Group = new(9001, TelegramChatType.Group, true, 99);
    private static readonly TelegramPeerRef News = new(9002, TelegramChatType.Channel, true, 98);

    private const string NoInfo = "У меня нет подтверждённой информации по этому вопросу.";
    private const string ApiHash = "0123456789abcdef0123456789abcdef";

    private static TelegramSettingsInput Settings(bool replyToUnknown = false, int perHour = 30, bool reports = true) =>
        new(true, true, replyToUnknown, null, null, null, null, "normal", "auto", perHour, 1500, 0, NoInfo, "pdf,xlsx,csv,docx,txt", 20, reports);

    private TelegramIncoming Msg(Guid account, TelegramPeerRef chat, int id, long from, string text, bool mentions = false, bool outgoing = false,
        bool bot = false, TimeSpan? age = null, TelegramIncomingFile? file = null, string? username = null) =>
        new(account, chat, "чат", id, from, username ?? (from == 5001 ? "ivan" : null), from == 5001 ? "Иван" : "Гость", text,
            fixture.Clock.GetUtcNow() - (age ?? TimeSpan.Zero), outgoing, mentions, file, bot);

    /// <summary>Ключи приложения, аккаунт «Отдел продаж» подключён по коду, его чаты загружены, агент включён.</summary>
    private async Task<Guid> ConnectAsync(IServiceProvider sp, TelegramSettingsInput? settings = null)
    {
        fixture.Telegram.Dialogs.AddRange([
            new TelegramDialog(Ivan, "Иван Петров", "ivan", true),
            new TelegramDialog(Dealer, "Дилер Хорезм", null, true),
            new TelegramDialog(Olga, "Ольга", "olga", true),
            new TelegramDialog(Group, "Продажи — регионы", null, true),
            new TelegramDialog(News, "Новости компании", "news", false),
        ]);
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        await accounts.SaveCredentialsAsync(123456, ApiHash, fixture.AdminId);
        var account = await accounts.CreateAccountAsync("Отдел продаж", fixture.AdminId);
        await accounts.StartLoginAsync(account.Id, "+998 90 123 45 67", fixture.AdminId);
        await accounts.SubmitCodeAsync(account.Id, fixture.Telegram.Code, fixture.AdminId);
        await accounts.SaveSettingsAsync(account.Id, settings ?? Settings(), fixture.AdminId);
        return account.Id;
    }

    /// <summary>Сотрудник с правом на консультанта OneBase (полный доступ агента наследует его права).</summary>
    private static async Task<Guid> ManagerAsync(OneBaseDbContext db, bool canAsk = true)
    {
        var role = new Role { Name = $"Менеджер {Guid.NewGuid():N}"[..20] };
        role.Permissions.Add(new RolePermission { Code = Permissions.SalesRead });
        if (canAsk)
        {
            role.Permissions.Add(new RolePermission { Code = Permissions.AgentsRun });
        }

        var user = new User { Login = $"tg-{Guid.NewGuid():N}"[..20], FullName = "Менеджер Telegram" };
        user.Roles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> ReportAsync(OneBaseDbContext db, Guid userId)
    {
        var content = new AiReportContent("Продажи по регионам",
            [new AiReportSheet("Регионы", [new AiReportColumn("Регион", ReportFormats.Text), new AiReportColumn("Кг", ReportFormats.Kg)],
                [[JsonSerializer.SerializeToElement("Хорезм"), JsonSerializer.SerializeToElement(900)]])], ["Продажи"]);
        var report = new AiReport
        {
            UserId = userId,
            AgentCode = "sales",
            Title = content.Title,
            Content = JsonSerializer.Serialize(content, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            SheetCount = 1,
            RowCount = 1,
        };
        db.AiReports.Add(report);
        await db.SaveChangesAsync();
        return report.Id;
    }

    /// <summary>Вызов сервиса рассылок в своей области DI — как отдельный HTTP-запрос (свежий DbContext).</summary>
    private async Task<T> Campaigns<T>(Func<TelegramCampaignService, Task<T>> action)
    {
        using var scope = fixture.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TelegramCampaignService>());
    }

    // ---------- 1а: вход, секреты, проверка ----------

    [PostgresFact]
    public async Task Login_by_code_and_2fa_keeps_no_secrets_and_a_revoked_session_marks_the_account_disconnected()
    {
        await fixture.ResetAsync();
        fixture.Telegram.Password = "Kot-v-Sapogah-2026";
        fixture.Telegram.Dialogs.Add(new TelegramDialog(Ivan, "Иван Петров", "ivan", true));
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();

        await accounts.SaveCredentialsAsync(123456, ApiHash, fixture.AdminId);
        var overview = await accounts.OverviewAsync();
        Assert.True(overview.Credentials.Configured);
        Assert.Equal("cdef", overview.Credentials.ApiHashHint);
        Assert.DoesNotContain(ApiHash, (await db.Integrations.AsNoTracking().SingleAsync(i => i.Code == "telegram")).ProtectedSecret);

        var account = await accounts.CreateAccountAsync("Отдел продаж", fixture.AdminId);
        var step = await accounts.StartLoginAsync(account.Id, "+998 90 123 45 67", fixture.AdminId);
        Assert.Equal("CodeRequired", step.Status);
        Assert.Equal("…4567", step.Account.PhoneHint);

        // Неверный код — можно ввести снова (вход не сорван).
        var wrong = await Assert.ThrowsAsync<TelegramException>(() => accounts.SubmitCodeAsync(account.Id, "11111", fixture.AdminId));
        Assert.Equal(TelegramErrorKind.Auth, wrong.Kind);
        Assert.Equal(TelegramAccountStatus.CodeRequired, (await db.TelegramAccounts.AsNoTracking().SingleAsync()).Status);

        Assert.Equal("PasswordRequired", (await accounts.SubmitCodeAsync(account.Id, "24680", fixture.AdminId)).Status);
        await Assert.ThrowsAsync<TelegramException>(() => accounts.SubmitPasswordAsync(account.Id, "not-the-password", fixture.AdminId));
        var done = await accounts.SubmitPasswordAsync(account.Id, "Kot-v-Sapogah-2026", fixture.AdminId);
        Assert.Equal("Connected", done.Status);
        Assert.Equal("onebase_sales", done.Account.Username);
        Assert.Equal(1, done.Account.Chats); // чаты загружены сразу после входа

        // Ни код, ни пароль, ни номер, ни api_hash открытым текстом — ни в аккаунте, ни в журнале действий.
        var row = await db.TelegramAccounts.AsNoTracking().SingleAsync();
        Assert.DoesNotContain("998901234567", row.ProtectedPhone ?? string.Empty);
        var audit = string.Join('\n', (await db.AuditLogs.AsNoTracking().Where(a => a.Action.StartsWith("telegram.")).Select(a => a.Data).ToListAsync()).Select(d => d ?? string.Empty));
        Assert.DoesNotContain("24680", audit);
        Assert.DoesNotContain("Kot-v-Sapogah", audit);
        Assert.DoesNotContain("998901234567", audit);
        Assert.DoesNotContain(ApiHash, audit);

        // Сессию отозвали в Telegram («Устройства» → завершить) — проверка помечает аккаунт отключённым с причиной.
        fixture.Telegram.Revoked.Add(account.Id);
        var checkedView = await accounts.CheckAsync(account.Id);
        Assert.Equal("Disconnected", checkedView.Status);
        Assert.Contains("переподключите", checkedView.LastError);

        // Повторное подключение и отключение администратором: OneBase выходит из сессии.
        fixture.Telegram.Revoked.Clear();
        fixture.Telegram.Password = null;
        await accounts.StartLoginAsync(account.Id, "+998901234567", fixture.AdminId);
        Assert.Equal("Connected", (await accounts.SubmitCodeAsync(account.Id, "24680", fixture.AdminId)).Status);
        Assert.Equal("Disconnected", (await accounts.DisconnectAsync(account.Id, fixture.AdminId)).Status);
        Assert.DoesNotContain(account.Id, fixture.Telegram.Sessions);
    }

    // ---------- 1б: права по username и полный доступ ----------

    [PostgresFact]
    public async Task Rights_are_given_by_username_and_full_access_answers_with_the_chosen_onebase_users_rights()
    {
        await fixture.ResetAsync();
        fixture.Telegram.Users["ivan"] = new TelegramResolvedUser(5001, "Ivan", "Иван Петров", false);
        fixture.Telegram.Users["news_bot"] = new TelegramResolvedUser(7001, "news_bot", "Новости", true);
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var agent = sp.GetRequiredService<TelegramAgentService>();
        var accountId = await ConnectAsync(sp);
        var manager = await ManagerAsync(db);

        // Администратор пишет @username — OneBase находит человека в Telegram и закрепляет права за его ID.
        var ivan = await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(null, "@Ivan", null, TelegramAccessMode.Full, null, true, true), fixture.AdminId);
        Assert.Equal((5001L, "ivan", "Иван Петров"), (ivan.TelegramUserId!.Value, ivan.Username, ivan.Title));
        Assert.Equal("no_rights", ivan.State); // не выбран пользователь OneBase, с чьими правами отвечать
        var missing = await Assert.ThrowsAsync<TelegramException>(() =>
            accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(null, "nobody_here", null, TelegramAccessMode.SentOnly, null, false, true), fixture.AdminId));
        Assert.Contains("не найден", missing.Message);
        await Assert.ThrowsAsync<TelegramException>(() =>
            accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(null, "news_bot", null, TelegramAccessMode.SentOnly, null, false, true), fixture.AdminId));

        // Без пользователя OneBase полный доступ отвечает по отправленным данным — консультант не вызывается.
        fixture.Gateway.Script.Enqueue(_ => TelegramAgentService.NoInfoMarker);
        await agent.ProcessAsync(Msg(accountId, Ivan, 1, 5001, "Какие продажи за сентябрь?"), default);
        Assert.Empty(fixture.Consultant.Turns);
        Assert.Equal(NoInfo, fixture.Telegram.Sent[^1].Message.Text);

        // Пользователь OneBase без права на консультанта не подходит; с правом — полный доступ работает сразу, без кода привязки.
        var noRights = await ManagerAsync(db, canAsk: false);
        await Assert.ThrowsAsync<TelegramException>(() => accounts.SaveAccessAsync(accountId, new TelegramAccessInput(false, noRights), fixture.AdminId));
        var access = await accounts.SaveAccessAsync(accountId, new TelegramAccessInput(false, manager), fixture.AdminId);
        Assert.Equal("Менеджер Telegram", access.FullAccessUserName);
        Assert.Equal("active", (await accounts.PermissionsAsync(accountId, null, null)).Single().State);

        await agent.ProcessAsync(Msg(accountId, Ivan, 2, 5001, "Какие продажи за сентябрь?"), default);
        var turn = Assert.Single(fixture.Consultant.Turns);
        Assert.Equal(manager, turn.UserId);
        Assert.Equal("telegram", turn.Channel!.Name);
        Assert.Contains("Candy Gold", turn.Channel.Instructions);
        Assert.True(fixture.Telegram.Typing > 0); // пока готовился ответ — «печатает…»
        Assert.Contains("<b>Продажи</b>", fixture.Telegram.Sent[^1].Message.Text);
        var conversation = await db.AiConversations.AsNoTracking().SingleAsync(c => c.UserId == manager);
        Assert.StartsWith("Telegram", conversation.Title);
        await agent.ProcessAsync(Msg(accountId, Ivan, 3, 5001, "А за август?"), default);
        Assert.Equal(conversation.Id, fixture.Consultant.Turns[^1].ConversationId);
        Assert.NotEmpty(fixture.Consultant.Turns[^1].History);

        // Доступ меняется прямо в строке: «только отправленные» — консультант больше не вызывается.
        await accounts.SavePermissionAsync(accountId, ivan.Id, new TelegramPermissionInput(5001, "ivan", "Иван Петров", TelegramAccessMode.SentOnly, null, false, true), fixture.AdminId);
        fixture.Gateway.Script.Enqueue(_ => TelegramAgentService.NoInfoMarker);
        await agent.ProcessAsync(Msg(accountId, Ivan, 4, 5001, "Какие продажи за сентябрь?"), default);
        Assert.Equal(2, fixture.Consultant.Turns.Count);

        // Свой пользователь OneBase у строки (привязка кодом из профиля) сильнее общего.
        var other = await ManagerAsync(db);
        var code = await accounts.CreateLinkCodeAsync(other);
        await accounts.SavePermissionAsync(accountId, ivan.Id, new TelegramPermissionInput(5001, "ivan", "Иван Петров", TelegramAccessMode.Full, null, false, true), fixture.AdminId);
        await agent.ProcessAsync(Msg(accountId, Ivan, 5, 5001, $"мой код {code.Code.ToLowerInvariant()}"), default);
        Assert.Contains("Готово", fixture.Telegram.Sent[^1].Message.Text);
        await agent.ProcessAsync(Msg(accountId, Ivan, 6, 5001, "Какие продажи?"), default);
        Assert.Equal(other, fixture.Consultant.Turns[^1].UserId);

        // Код одноразовый: другой Telegram с тем же кодом ничего не получает.
        await agent.ProcessAsync(Msg(accountId, Dealer, 7, 5002, code.Code), default);
        Assert.Contains("не подходит", fixture.Telegram.Sent[^1].Message.Text);

        var journal = await accounts.ExecutionsAsync(accountId, null);
        Assert.Contains(journal, e => e.Mode == "full" && e.Status == "answered" && e.Who == "Иван Петров");
        Assert.Contains(journal, e => e.Mode == "link" && e.Status == "linked" && e.Question == "[код привязки]");
        Assert.DoesNotContain(journal, e => (e.Question ?? string.Empty).Contains(code.Code, StringComparison.OrdinalIgnoreCase));
    }

    [PostgresFact]
    public async Task Username_added_while_offline_binds_to_the_telegram_id_of_the_first_message()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var agent = sp.GetRequiredService<TelegramAgentService>();
        var accountId = await ConnectAsync(sp);
        await accounts.DisconnectAsync(accountId, fixture.AdminId);

        // Аккаунт не подключён — искать в Telegram не через что: ID определится по первому сообщению.
        var olga = await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(null, "olga", null, TelegramAccessMode.SentOnly, null, false, true), fixture.AdminId);
        Assert.Null(olga.TelegramUserId);
        Assert.Equal("pending", olga.State);

        await accounts.StartLoginAsync(accountId, "+998901234567", fixture.AdminId);
        await accounts.SubmitCodeAsync(accountId, fixture.Telegram.Code, fixture.AdminId);
        fixture.Gateway.Script.Enqueue(_ => "Здравствуйте! Я сотрудник компании Candy Gold, помогаю с отчётами.");
        await agent.ProcessAsync(Msg(accountId, Olga, 1, 5004, "Привет, ты кто?", username: "Olga"), default);
        Assert.Equal(5004, (await db.TelegramUserPermissions.AsNoTracking().SingleAsync(p => p.Id == olga.Id)).TelegramUserId);
        Assert.Contains("Candy Gold", fixture.Telegram.Sent[^1].Message.Text);

        // Username потом занял другой человек — права остались за Telegram ID Ольги.
        var stranger = new TelegramPeerRef(5005, TelegramChatType.Private, false, 55);
        await agent.ProcessAsync(Msg(accountId, stranger, 2, 5005, "Привет", username: "olga"), default);
        Assert.Equal("unknown_user", (await db.TelegramIncomingMessages.AsNoTracking().SingleAsync(m => m.PeerId == 5005)).Reason);
    }

    [PostgresFact]
    public async Task Full_access_sends_reports_only_when_allowed_reads_allowed_files_and_needs_the_consultant_right()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var agent = sp.GetRequiredService<TelegramAgentService>();
        var accountId = await ConnectAsync(sp);
        var manager = await ManagerAsync(db);
        var reportId = await ReportAsync(db, manager);
        var permission = await accounts.SavePermissionAsync(accountId, null,
            new TelegramPermissionInput(5001, null, "Иван", TelegramAccessMode.Full, manager, true, true), fixture.AdminId);
        fixture.Consultant.Answer = turn => new ConsultantAnswer("Отчёт готов.",
            new ConsultantDetails([], [], "fake/consultant", false, Reports: turn.Channel!.AllowReports ? [new ReportRef(reportId, "Продажи по регионам", 1, 1)] : null),
            new AiTokenUsage(10, 5));

        // Отчёты разрешены и аккаунту, и пользователю — «минутку», потом только файл Excel, без текста.
        await agent.ProcessAsync(Msg(accountId, Ivan, 1, 5001, "Сделай отчёт по продажам в Excel"), default);
        Assert.True(fixture.Consultant.Turns[^1].Channel!.AllowReports);
        Assert.Equal(2, fixture.Telegram.Sent.Count);
        Assert.Equal(GenerationIntent.Ack("auto", "Сделай отчёт по продажам в Excel", 1), fixture.Telegram.Sent[0].Message.Text);
        var file = Assert.Single(fixture.Telegram.Sent[^1].Message.Files);
        Assert.EndsWith(".xlsx", file.FileName);
        Assert.Null(fixture.Telegram.Sent[^1].Message.Text);
        Assert.Equal(reportId, (await accounts.ExecutionsAsync(accountId, "answered"))[0].ReportId);

        // Пользователю отчёты не разрешены — консультант получает запрет, файл не отправляется.
        await accounts.SavePermissionAsync(accountId, permission.Id, new TelegramPermissionInput(5001, null, "Иван", TelegramAccessMode.Full, manager, false, true), fixture.AdminId);
        var sent = fixture.Telegram.Sent.Count;
        await agent.ProcessAsync(Msg(accountId, Ivan, 2, 5001, "Сделай отчёт по продажам в Excel"), default);
        Assert.False(fixture.Consultant.Turns[^1].Channel!.AllowReports);
        Assert.Equal(sent + 2, fixture.Telegram.Sent.Count); // «минутку» и ответ текстом
        Assert.Equal("Отчёт готов.", fixture.Telegram.Sent[^1].Message.Text);
        Assert.Empty(fixture.Telegram.Sent[^1].Message.Files);

        // Файл от пользователя с полным доступом: разрешённый — текст уходит консультанту; запрещённый тип — отказ.
        fixture.Telegram.Files[(Ivan.PeerId, 3)] = Encoding.UTF8.GetBytes("План на ноябрь: Хорезм 1200 кг");
        await agent.ProcessAsync(Msg(accountId, Ivan, 3, 5001, "Проверь план", file: new TelegramIncomingFile("plan.txt", "text/plain", 40)), default);
        Assert.Contains("Хорезм 1200 кг", fixture.Consultant.Turns[^1].Question);
        var turns = fixture.Consultant.Turns.Count;
        await agent.ProcessAsync(Msg(accountId, Ivan, 4, 5001, "Посмотри", file: new TelegramIncomingFile("setup.exe", "application/octet-stream", 1000)), default);
        Assert.Equal(turns, fixture.Consultant.Turns.Count);
        Assert.Contains("не читаю", fixture.Telegram.Sent[^1].Message.Text);

        // У пользователя OneBase строки нет права на консультанта — полного доступа нет: ответ по отправленным данным.
        var noRights = await ManagerAsync(db, canAsk: false);
        await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(5002, null, "Дилер", TelegramAccessMode.Full, noRights, true, true), fixture.AdminId);
        fixture.Gateway.Script.Enqueue(_ => TelegramAgentService.NoInfoMarker);
        await agent.ProcessAsync(Msg(accountId, Dealer, 5, 5002, "Какие продажи?"), default);
        Assert.Equal(turns, fixture.Consultant.Turns.Count);
        Assert.Equal(NoInfo, fixture.Telegram.Sent[^1].Message.Text);
    }

    [PostgresFact]
    public async Task Request_for_a_document_gets_a_quick_ack_and_then_only_the_word_file()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var agent = sp.GetRequiredService<TelegramAgentService>();
        var accountId = await ConnectAsync(sp);
        var manager = await ManagerAsync(db);
        await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(5001, null, "Иван", TelegramAccessMode.Full, manager, true, true), fixture.AdminId);
        const string letter = "# Письмо дилеру о скидке\n\nУважаемый партнёр!\n\nС **1 ноября** скидка на кекс:\n\n- 15% — от 100 кг\n- 20% — от 500 кг\n\n| Объём | Скидка |\n|---|---|\n| 100 кг | 15% |\n| 500 кг | 20% |";
        fixture.Consultant.Answer = _ => new ConsultantAnswer(letter, new ConsultantDetails([], [], "fake/consultant", false), new AiTokenUsage(10, 5));

        // Просьба составить письмо: сразу «минутку», потом только файл Word — без текста в чат.
        await agent.ProcessAsync(Msg(accountId, Ivan, 1, 5001, "Составь письмо дилеру о скидке на кекс"), default);
        Assert.Contains("документ", fixture.Consultant.Turns[^1].Channel!.Instructions);
        Assert.Equal(2, fixture.Telegram.Sent.Count);
        Assert.Equal(GenerationIntent.Ack("auto", "Составь письмо дилеру о скидке на кекс", 1), fixture.Telegram.Sent[0].Message.Text);
        Assert.Null(fixture.Telegram.Sent[1].Message.Text);
        var file = Assert.Single(fixture.Telegram.Sent[1].Message.Files);
        Assert.Equal("Письмо дилеру о скидке.docx", file.FileName);
        using (var zip = new ZipArchive(new MemoryStream(file.Content)))
        {
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var xml = await reader.ReadToEndAsync();
            Assert.Contains("Уважаемый партнёр!", xml);
            Assert.Contains("500 кг", xml);
        }

        var execution = (await accounts.ExecutionsAsync(accountId, "answered"))[0];
        Assert.StartsWith("[Файл: Письмо дилеру о скидке.docx]", execution.Answer);

        // Документ сделать нельзя — «минутку», потом причина обычным текстом, без файла.
        fixture.Consultant.Answer = _ => new ConsultantAnswer($"{TelegramAgentService.NoDocMarker}: за сентябрь данных о скидках нет.",
            new ConsultantDetails([], [], "fake/consultant", false), new AiTokenUsage(10, 5));
        await agent.ProcessAsync(Msg(accountId, Ivan, 2, 5001, "Подготовь план продаж на ноябрь"), default);
        Assert.Equal("за сентябрь данных о скидках нет.", fixture.Telegram.Sent[^1].Message.Text);
        Assert.Empty(fixture.Telegram.Sent[^1].Message.Files);

        // Обычный вопрос — без «минутку» и без файла.
        fixture.Consultant.Answer = null;
        var sent = fixture.Telegram.Sent.Count;
        await agent.ProcessAsync(Msg(accountId, Ivan, 3, 5001, "Какие продажи за сентябрь?"), default);
        Assert.Equal(sent + 1, fixture.Telegram.Sent.Count);
        Assert.Contains("<b>Продажи</b>", fixture.Telegram.Sent[^1].Message.Text);

        // Узбекский — «минутку» по-узбекски; в «Проверке» — то же и предпросмотр документа, в Telegram ничего не уходит.
        fixture.Consultant.Answer = _ => new ConsultantAnswer(letter, new ConsultantDetails([], [], "fake/consultant", false), new AiTokenUsage(10, 5));
        await agent.ProcessAsync(Msg(accountId, Ivan, 4, 5001, "Dilerga chegirma haqida xat yozing"), default);
        Assert.Contains("daqiqa", fixture.Telegram.Sent[^2].Message.Text);
        sent = fixture.Telegram.Sent.Count;
        var permission = (await accounts.PermissionsAsync(accountId, null, null)).Single();
        var test = await agent.SimulateAsync(accountId, new AgentTestInput(null, permission.Id, "Составь письмо дилеру о скидке на кекс"), default);
        Assert.Equal(["Минутку, уже делаю."], test.Replies);
        Assert.Equal(["Письмо дилеру о скидке.docx"], test.Files);
        Assert.Contains("Уважаемый партнёр!", test.Document);
        Assert.Equal(sent, fixture.Telegram.Sent.Count);
    }

    // ---------- 1б: кому и когда агент отвечает ----------

    [PostgresFact]
    public async Task Agent_ignores_own_bot_old_channel_unknown_and_unaddressed_group_messages_and_answers_each_message_once()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var agent = sp.GetRequiredService<TelegramAgentService>();
        var accountId = await ConnectAsync(sp);
        var manager = await ManagerAsync(db);
        await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(5001, null, "Иван", TelegramAccessMode.SentOnly, null, false, true), fixture.AdminId);
        fixture.Gateway.Script.Enqueue(_ => "Акций в отправленных вам материалах нет.");
        fixture.Gateway.Script.Enqueue(_ => "Акций в отправленных вам материалах нет.");

        // Свои исходящие и сообщения ботов не обрабатываются вовсе.
        await agent.ProcessAsync(Msg(accountId, Ivan, 1, 5001, "Есть акции?", outgoing: true), default);
        await agent.ProcessAsync(Msg(accountId, Ivan, 2, 5001, "Есть акции?", bot: true), default);
        Assert.Empty(fixture.Telegram.Sent);
        Assert.Equal(0, await db.TelegramIncomingMessages.CountAsync());

        // Повторная доставка того же сообщения — один ответ.
        var message = Msg(accountId, Ivan, 10, 5001, "Есть акции?");
        await agent.ProcessAsync(message, default);
        await agent.ProcessAsync(message, default);
        Assert.Single(fixture.Telegram.Sent);
        Assert.Single(await db.TelegramAgentExecutions.AsNoTracking().ToListAsync());

        // После простоя: старое сообщение без ответа. Незнакомый (ответы всем выключены) — молча. Канал — никогда.
        await agent.ProcessAsync(Msg(accountId, Ivan, 11, 5001, "Есть акции?", age: TimeSpan.FromHours(2)), default);
        await agent.ProcessAsync(Msg(accountId, Dealer, 12, 5002, "Привет"), default);
        await agent.ProcessAsync(Msg(accountId, News, 13, 5001, "Новость", mentions: true), default);
        Assert.Single(fixture.Telegram.Sent);
        var reasons = await db.TelegramIncomingMessages.AsNoTracking().ToDictionaryAsync(m => m.MessageId, m => m.Reason);
        Assert.Equal(("too_old", "unknown_user", "channel"), (reasons[11], reasons[12], reasons[13]));

        // Другой подключённый аккаунт OneBase пишет этому — агенты не переписываются друг с другом.
        fixture.Telegram.Me = new TelegramMe(5003, "onebase_support", "OneBase Поддержка");
        var other = await accounts.CreateAccountAsync("Поддержка", fixture.AdminId);
        await accounts.StartLoginAsync(other.Id, "+998901112233", fixture.AdminId);
        await accounts.SubmitCodeAsync(other.Id, fixture.Telegram.Code, fixture.AdminId);
        await agent.ProcessAsync(Msg(accountId, new TelegramPeerRef(5003, TelegramChatType.Private, false, 33), 14, 5003, "Привет, коллега"), default);
        Assert.Equal("own_account", (await db.TelegramIncomingMessages.AsNoTracking().SingleAsync(m => m.MessageId == 14)).Reason);

        // Группа: агент выключен — молчит; включён — отвечает только на обращение, ответом на сообщение.
        var group = await db.TelegramChats.AsNoTracking().SingleAsync(c => c.AccountId == accountId && c.PeerId == Group.PeerId);
        await agent.ProcessAsync(Msg(accountId, Group, 20, 5001, "@onebase_sales есть акции?", mentions: true), default);
        await accounts.SetChatAgentAsync(accountId, group.Id, true, fixture.AdminId);
        await agent.ProcessAsync(Msg(accountId, Group, 21, 5001, "всем привет"), default);
        Assert.Single(fixture.Telegram.Sent);
        await agent.ProcessAsync(Msg(accountId, Group, 22, 5001, "@onebase_sales есть акции?", mentions: true), default);
        Assert.Equal(22, fixture.Telegram.Sent[^1].Message.ReplyToMessageId);

        // Код привязки, показанный в группе, гаснет: его видели все.
        var code = await accounts.CreateLinkCodeAsync(manager);
        await agent.ProcessAsync(Msg(accountId, Group, 23, 5001, code.Code, mentions: true), default);
        Assert.Contains("нельзя отправлять в группу", fixture.Telegram.Sent[^1].Message.Text);
        await agent.ProcessAsync(Msg(accountId, Ivan, 24, 5001, code.Code), default);
        Assert.Contains("истёк", fixture.Telegram.Sent[^1].Message.Text);

        reasons = await db.TelegramIncomingMessages.AsNoTracking().ToDictionaryAsync(m => m.MessageId, m => m.Reason);
        Assert.Equal(("group_disabled", "not_mentioned"), (reasons[20], reasons[21]));
    }

    // ---------- 1в: только отправленные данные ----------

    [PostgresFact]
    public async Task Sent_only_answers_only_from_materials_of_the_same_chat_and_says_honestly_when_there_is_nothing()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var agent = sp.GetRequiredService<TelegramAgentService>();
        var materials = sp.GetRequiredService<TelegramMaterialService>();
        var accountId = await ConnectAsync(sp, Settings(replyToUnknown: true, perHour: 3));
        await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(5001, null, "Иван", TelegramAccessMode.SentOnly, null, false, true), fixture.AdminId);
        await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(5002, null, "Дилер", TelegramAccessMode.SentOnly, null, false, true), fixture.AdminId);
        await materials.RecordTextAsync(accountId, Ivan.PeerId, "message", "Акция октября", "Акция: скидка 15% на кекс до 31 октября.", null, fixture.AdminId);
        await materials.RecordFileAsync(accountId, Dealer.PeerId, "file", "Прайс", "price.txt", null,
            Encoding.UTF8.GetBytes("Прайс для дилера: бамбук 400 г — 20 000 сум за пачку."), null, fixture.AdminId);

        // Дилер спрашивает о скидке на кекс: ИИ видит только материалы его чата (прайс), про акцию Ивана не знает → честное «нет данных».
        fixture.Gateway.Script.Enqueue(_ => TelegramAgentService.NoInfoMarker);
        await agent.ProcessAsync(Msg(accountId, Dealer, 30, 5002, "Какая скидка на кекс?"), default);
        var system = Assert.Single(fixture.Gateway.Calls).Messages[0].Content;
        Assert.Contains("бамбук", system);
        Assert.DoesNotContain("15%", system);
        Assert.Equal(NoInfo, fixture.Telegram.Sent[^1].Message.Text);

        // У Ивана акция есть — ответ по ней; чужой прайс в подсказку не попадает.
        fixture.Gateway.Script.Enqueue(_ => "Скидка **15%** на кекс действует до 31 октября.");
        await agent.ProcessAsync(Msg(accountId, Ivan, 31, 5001, "Какая скидка на кекс?"), default);
        system = fixture.Gateway.Calls[^1].Messages[0].Content;
        Assert.Contains("скидка 15%", system);
        Assert.DoesNotContain("бамбук", system);
        Assert.Contains("<b>15%</b>", fixture.Telegram.Sent[^1].Message.Text);

        // Следующий вопрос Ивана — с историей разговора (уточнения понятны).
        fixture.Gateway.Script.Enqueue(_ => "Да, на все виды кекса.");
        await agent.ProcessAsync(Msg(accountId, Ivan, 33, 5001, "А на все виды?"), default);
        var messages = fixture.Gateway.Calls[^1].Messages;
        Assert.Equal(("Какая скидка на кекс?", "Скидка **15%** на кекс действует до 31 октября.", "А на все виды?"),
            (messages[1].Content, messages[2].Content, messages[3].Content));

        // Незнакомый (ответы всем включены) в чате без материалов: ИИ знает, что материалов нет, — на вопрос о данных честное «нет данных».
        var guest = new TelegramPeerRef(5010, TelegramChatType.Private, false, 10);
        for (var i = 0; i < 3; i++)
        {
            fixture.Gateway.Script.Enqueue(_ => TelegramAgentService.NoInfoMarker);
        }

        await agent.ProcessAsync(Msg(accountId, guest, 40, 5010, "Сколько стоит кекс?"), default);
        Assert.Contains("Материалов в этом чате пока нет", fixture.Gateway.Calls[^1].Messages[0].Content);
        Assert.Equal(NoInfo, fixture.Telegram.Sent[^1].Message.Text);

        // Лимит 3 ответа в час: четвёртый вопрос — одно предупреждение, дальше молча (ИИ не вызывается).
        await agent.ProcessAsync(Msg(accountId, guest, 41, 5010, "А бамбук?"), default);
        await agent.ProcessAsync(Msg(accountId, guest, 42, 5010, "А вафли?"), default);
        var calls = fixture.Gateway.Calls.Count;
        await agent.ProcessAsync(Msg(accountId, guest, 43, 5010, "А печенье?"), default);
        await agent.ProcessAsync(Msg(accountId, guest, 44, 5010, "А пряники?"), default);
        Assert.Equal(calls, fixture.Gateway.Calls.Count);
        var guestReplies = fixture.Telegram.Sent.Where(s => s.Peer.PeerId == 5010).Select(s => s.Message.Text).ToList();
        Assert.Equal(4, guestReplies.Count);
        Assert.Contains("Слишком много", guestReplies[^1]);
        Assert.Equal(2, await db.TelegramAgentExecutions.CountAsync(e => e.TelegramUserId == 5010 && e.Status == "rate_limited"));

        // Файл от пользователя «только отправленные» — не принимается.
        await agent.ProcessAsync(Msg(accountId, Ivan, 32, 5001, "Посмотри файл", file: new TelegramIncomingFile("a.txt", "text/plain", 10)), default);
        Assert.Contains("только от тех, у кого полный доступ", fixture.Telegram.Sent[^1].Message.Text);
    }

    [PostgresFact]
    public async Task Plain_questions_use_the_answer_model_and_requests_to_compose_use_the_generation_model()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var agent = sp.GetRequiredService<TelegramAgentService>();
        var gpt = new AiModelRef("openai", "gpt-5");
        var claude = new AiModelRef("anthropic", "claude-opus-5-5");
        var accountId = await ConnectAsync(sp, Settings(replyToUnknown: true) with { Model = "openai/gpt-5", GenerationModel = "anthropic/claude-opus-5-5" });
        var saved = await accounts.SettingsAsync(accountId);
        Assert.Equal(("anthropic", "claude-opus-5-5"), (saved.GenerationModelProvider, saved.GenerationModel));

        // Обычный вопрос — модель ответов (OpenAI).
        fixture.Gateway.Script.Enqueue(_ => "Акций сейчас нет.");
        await agent.SimulateAsync(accountId, new AgentTestInput(null, null, "Какие сейчас акции?"), default);
        Assert.Equal((gpt, 1500), (fixture.Gateway.Calls[^1].Options.Model, fixture.Gateway.Calls[^1].Options.MaxOutputTokens));

        // Просьба составить письмо — модель генерации (Claude), и места для ответа больше.
        fixture.Gateway.Script.Enqueue(_ => "Уважаемый партнёр! С 1 ноября скидка 15% на кекс.");
        await agent.SimulateAsync(accountId, new AgentTestInput(null, null, "Составь письмо дилеру о скидке на кекс"), default);
        Assert.Equal((claude, 4000), (fixture.Gateway.Calls[^1].Options.Model, fixture.Gateway.Calls[^1].Options.MaxOutputTokens));

        // Полный доступ: обычный вопрос — консультант на модели ответов; отчёт — Claude и у ответа, и у AI-сотрудников, которые его собирают.
        var manager = await ManagerAsync(db);
        await accounts.SaveAccessAsync(accountId, new TelegramAccessInput(true, manager), fixture.AdminId);
        fixture.Telegram.Users["ivan"] = new TelegramResolvedUser(5001, "Ivan", "Иван Петров", false);
        var ivan = await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(null, "@Ivan", null, TelegramAccessMode.Full, null, true, true), fixture.AdminId);
        await agent.SimulateAsync(accountId, new AgentTestInput(null, ivan.Id, "Какие продажи за сентябрь?"), default);
        Assert.Equal((gpt, (AiModelRef?)null), (fixture.Consultant.Turns[^1].Channel!.Model, fixture.Consultant.Turns[^1].Channel!.AgentModel));
        await agent.SimulateAsync(accountId, new AgentTestInput(null, ivan.Id, "Собери отчёт по продажам за сентябрь по регионам"), default);
        Assert.Equal((claude, claude), (fixture.Consultant.Turns[^1].Channel!.Model, fixture.Consultant.Turns[^1].Channel!.AgentModel));

        // Модель генерации не задана и ключа Anthropic нет — просьба тоже идёт в модель ответов.
        await accounts.SaveSettingsAsync(accountId, Settings(replyToUnknown: true) with { Model = "openai/gpt-5" }, fixture.AdminId);
        fixture.Gateway.Script.Enqueue(_ => "Уважаемый партнёр!");
        await agent.SimulateAsync(accountId, new AgentTestInput(null, null, "Составь письмо дилеру о скидке на кекс"), default);
        Assert.Equal((gpt, 1500), (fixture.Gateway.Calls[^1].Options.Model, fixture.Gateway.Calls[^1].Options.MaxOutputTokens));
    }

    [PostgresFact]
    public async Task Agent_introduces_itself_explains_reports_knows_the_next_send_and_the_test_chat_sends_nothing()
    {
        await fixture.ResetAsync();
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        var agent = sp.GetRequiredService<TelegramAgentService>();
        var materials = sp.GetRequiredService<TelegramMaterialService>();
        var accountId = await ConnectAsync(sp, Settings(replyToUnknown: true));
        var chats = await db.TelegramChats.AsNoTracking().Where(c => c.AccountId == accountId).ToDictionaryAsync(c => c.PeerId, c => c.Id);
        await materials.RecordTextAsync(accountId, Ivan.PeerId, "report", "Отчёт по продажам", "Сентябрь: план 37 000 кг, факт 35 600 кг, выполнение 96%.", null, fixture.AdminId);

        // Ивану завтра запланирован отчёт — агент знает, когда следующая отправка.
        var campaign = await Campaigns(c => c.CreateAsync(new CampaignInput(accountId, "Отчёт за октябрь", null, "Отчёт", false, fixture.Clock.Now!.Value.AddDays(1), 5), fixture.AdminId));
        await Campaigns(c => c.AddRecipientsAsync(campaign.Id, [chats[Ivan.PeerId]]));
        Assert.Equal("Scheduled", (await Campaigns(c => c.LaunchAsync(campaign.Id, fixture.AdminId))).Status);

        // Проверка в настройках: тот же ответ, что в Telegram, но ничего не отправляется и не пишется в журнал.
        fixture.Gateway.Script.Enqueue(_ => "Следующий отчёт придёт 18.10 в 14:00.");
        var result = await agent.SimulateAsync(accountId,
            new AgentTestInput(chats[Ivan.PeerId], null, "Когда будет следующий отчёт?", [new AgentTestTurn("Привет", "Здравствуйте!")]), default);
        Assert.Equal(("sent_only", "answered", 1), (result.Mode, result.Status, result.Materials));
        Assert.Contains("18.10", result.Replies[0]);
        Assert.Empty(fixture.Telegram.Sent);
        Assert.Equal(0, await db.TelegramAgentExecutions.CountAsync());
        var call = fixture.Gateway.Calls[^1].Messages;
        var system = call[0].Content;
        Assert.Contains("сотрудник компании Candy Gold", system);
        Assert.Contains("не утверждай, что ты человек", system);
        Assert.Contains("насколько всё хорошо или плохо", system);
        Assert.Contains("«Отчёт за октябрь» — 18.10.2026 14:00", system);
        Assert.Contains("выполнение 96%", system);
        Assert.Equal(("Привет", "Здравствуйте!", "Когда будет следующий отчёт?"), (call[1].Content, call[2].Content, call[3].Content));

        // Своё представление агента — из настроек.
        await accounts.SaveSettingsAsync(accountId, Settings(replyToUnknown: true) with { Persona = "Анна, менеджер Candy Gold" }, fixture.AdminId);
        fixture.Gateway.Script.Enqueue(_ => "Я Анна, менеджер Candy Gold.");
        await agent.SimulateAsync(accountId, new AgentTestInput(chats[Ivan.PeerId], null, "Ты кто?"), default);
        Assert.Contains("Анна, менеджер Candy Gold", fixture.Gateway.Calls[^1].Messages[0].Content);

        // Без чата — по примеру отчёта из запроса (в базу не пишется).
        fixture.Gateway.Script.Enqueue(_ => "Выполнение 70% — заметно ниже плана.");
        var sample = await agent.SimulateAsync(accountId, new AgentTestInput(null, null, "Насколько всё плохо?", null, "Хорезм: план 100, факт 70"), default);
        Assert.Contains("Хорезм: план 100", fixture.Gateway.Calls[^1].Messages[0].Content);
        Assert.Contains("ниже плана", sample.Replies[0]);
        Assert.Equal(1, await db.TelegramMaterials.CountAsync());

        // Ответы всем выключены — незнакомому агент не ответил бы, и проверка это показывает (без вызова ИИ).
        await accounts.SaveAccessAsync(accountId, new TelegramAccessInput(false, null), fixture.AdminId);
        var calls = fixture.Gateway.Calls.Count;
        var ignored = await agent.SimulateAsync(accountId, new AgentTestInput(null, null, "Привет"), default);
        Assert.Equal("ignored", ignored.Status);
        Assert.Equal(calls, fixture.Gateway.Calls.Count);
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Chats_explain_why_writing_is_impossible_and_a_sent_campaign_can_be_repeated_as_new()
    {
        await fixture.ResetAsync();
        fixture.Telegram.Dialogs.Add(new TelegramDialog(new TelegramPeerRef(9003, TelegramChatType.Group, true, 97), "Закрытая группа", null, false,
            "в группе аккаунту запрещено писать"));
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var runner = fixture.Services.GetRequiredService<TelegramCampaignRunner>();
        runner.Delay = (_, _) => Task.CompletedTask;
        var accountId = await ConnectAsync(sp);
        var chats = await db.TelegramChats.AsNoTracking().Where(c => c.AccountId == accountId).ToDictionaryAsync(c => c.PeerId, c => c.Id);

        var campaign = await Campaigns(c => c.CreateAsync(new CampaignInput(accountId, "Отчёт", null, "Отчёт за сентябрь", false, null, 5), fixture.AdminId));
        var list = await Campaigns(c => c.ChatsAsync(campaign.Id, null, null));
        Assert.Equal("в группе аккаунту запрещено писать", list.Single(x => x.PeerId == 9003).Reason);
        Assert.Equal("аккаунт не может писать в этот чат", list.Single(x => x.PeerId == News.PeerId).Reason);
        Assert.Null(list.Single(x => x.PeerId == Ivan.PeerId).Reason);
        var skipped = await Campaigns(c => c.AddRecipientsAsync(campaign.Id, [chats[9003], chats[Ivan.PeerId]]));
        Assert.Equal((1, "аккаунт не может писать в этот чат"), (skipped.Added, skipped.Skipped.Single().Reason));

        await using (var file = new MemoryStream(Encoding.UTF8.GetBytes("Итого: 96% плана.")))
        {
            await Campaigns(c => c.AddFileAsync(campaign.Id, "итоги.txt", "text/plain", file, file.Length, fixture.AdminId));
        }

        await Campaigns(c => c.LaunchAsync(campaign.Id, fixture.AdminId));
        await runner.RunAllAsync(default);
        var done = await Campaigns(c => c.GetAsync(campaign.Id));
        Assert.Equal("Completed", done.Status);
        var downloaded = await Campaigns(c => c.AttachmentAsync(campaign.Id, done.Attachments.Single().Id));
        Assert.Equal(("итоги.txt", "Итого: 96% плана."), (downloaded.FileName, Encoding.UTF8.GetString(downloaded.Content)));

        // Отправленную не изменить — «Повторить как новую»: черновик с тем же текстом, своей копией файла и теми же получателями.
        var copy = await Campaigns(c => c.DuplicateAsync(campaign.Id, fixture.AdminId));
        Assert.Equal(("Draft", "Отчёт за сентябрь", 1), (copy.Status, copy.Text, copy.Counts.Pending));
        var copied = await Campaigns(c => c.AttachmentAsync(copy.Id, copy.Attachments.Single().Id));
        Assert.Equal(downloaded.Content, copied.Content);
        Assert.NotEqual(
            await db.TelegramCampaignAttachments.AsNoTracking().Where(a => a.CampaignId == campaign.Id).Select(a => a.ObjectKey).SingleAsync(),
            await db.TelegramCampaignAttachments.AsNoTracking().Where(a => a.CampaignId == copy.Id).Select(a => a.ObjectKey).SingleAsync());
        var listed = await Campaigns(c => c.ListAsync(null));
        Assert.Equal(("Отчёт за сентябрь", 1), (listed.Single(x => x.Id == copy.Id).TextPreview, listed.Single(x => x.Id == copy.Id).Files));
    }

    // ---------- 2: рассылки ----------

    [PostgresFact]
    public async Task Campaign_sends_each_recipient_once_retries_temporary_errors_and_records_materials()
    {
        await fixture.ResetAsync();
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var runner = fixture.Services.GetRequiredService<TelegramCampaignRunner>();
        runner.Delay = (_, _) => Task.CompletedTask;
        var accountId = await ConnectAsync(sp);
        var reportId = await ReportAsync(db, fixture.AdminId);
        var chats = await db.TelegramChats.AsNoTracking().Where(c => c.AccountId == accountId).ToDictionaryAsync(c => c.PeerId, c => c.Id);

        var campaign = await Campaigns(c => c.CreateAsync(new CampaignInput(accountId, "Акция октября", null, "**Скидка 15%** на кекс до 31 октября.", true, null, 5), fixture.AdminId));
        var added = await Campaigns(c => c.AddRecipientsAsync(campaign.Id, [chats[Ivan.PeerId], chats[Dealer.PeerId], chats[Olga.PeerId], chats[Ivan.PeerId], chats[News.PeerId]]));
        Assert.Equal(3, added.Added);
        Assert.Contains(added.Skipped, s => s.Reason.Contains("не может писать")); // канал без прав
        var again = await Campaigns(c => c.AddRecipientsAsync(campaign.Id, [chats[Ivan.PeerId]]));
        Assert.Equal((0, "уже в списке"), (again.Added, again.Skipped.Single().Reason));

        await using (var file = new MemoryStream(Encoding.UTF8.GetBytes("Условия акции: минимальный заказ 10 коробок.")))
        {
            await Campaigns(c => c.AddFileAsync(campaign.Id, "условия.txt", "text/plain", file, file.Length, fixture.AdminId));
        }

        await Campaigns(c => c.AddReportAsync(campaign.Id, reportId, fixture.AdminId));
        var preview = await Campaigns(c => c.PreviewAsync(campaign.Id));
        Assert.Contains("<b>Скидка 15%</b>", preview.Html);
        Assert.Equal(3, preview.Recipients);

        // Тест — в один чат, статусы рассылки не меняются.
        await Campaigns(c => c.TestAsync(campaign.Id, chats[Olga.PeerId], fixture.AdminId));
        Assert.Single(fixture.Telegram.Sent);
        Assert.All((await Campaigns(c => c.GetAsync(campaign.Id))).Recipients, r => Assert.Equal("Pending", r.Status));
        fixture.Telegram.Sent.Clear();
        fixture.Telegram.Attempts.Clear();

        // Дилеру — временная ошибка на первой попытке, Ольге — запрет приватности (повтор не поможет).
        fixture.Telegram.Fail = (peer, attempt) => peer.PeerId switch
        {
            5002 when attempt == 1 => TelegramRules.Classify(500, "INTERNAL_SERVER_ERROR", 0),
            5004 => TelegramRules.Classify(403, "USER_PRIVACY_RESTRICTED", 0),
            _ => null,
        };
        Assert.Equal("Queued", (await Campaigns(c => c.LaunchAsync(campaign.Id, fixture.AdminId))).Status);
        await runner.RunAllAsync(default);
        var view = await Campaigns(c => c.GetAsync(campaign.Id));
        var byPeer = view.Recipients.ToDictionary(r => r.PeerId);
        Assert.Equal(("Sent", "Pending", "Failed"), (byPeer[5001].Status, byPeer[5002].Status, byPeer[5004].Status));
        Assert.NotNull(byPeer[5002].NextAttemptAt);
        Assert.Contains("приватности", byPeer[5004].Error);
        Assert.Equal("Sending", view.Status);

        // Через минуту — повтор временной ошибки; рассылка завершена с ошибками.
        fixture.Clock.Now = fixture.Clock.Now!.Value.AddMinutes(1);
        await runner.RunAllAsync(default);
        view = await Campaigns(c => c.GetAsync(campaign.Id));
        Assert.Equal("CompletedWithErrors", view.Status);
        Assert.Equal((2, 1), (view.Counts.Sent, view.Counts.Failed));
        Assert.NotNull(view.FinishedAt);

        // Каждому — ровно одно отправление: текст подписью не помещается к двум файлам — текст и два файла одним вызовом.
        Assert.Equal([5001L, 5002L], fixture.Telegram.Sent.Select(s => s.Peer.PeerId).Order().ToArray());
        Assert.All(fixture.Telegram.Sent, s => Assert.Equal(2, s.Message.Files.Count));

        // Отправленное — материалы своего чата: текст и файл (с текстом), отчёт — файлом.
        var ivanMaterials = await db.TelegramMaterialChunks.AsNoTracking().Where(c => c.AccountId == accountId && c.PeerId == 5001).Select(c => c.Text).ToListAsync();
        Assert.Contains(ivanMaterials, t => t.Contains("Скидка 15%"));
        Assert.Contains(ivanMaterials, t => t.Contains("минимальный заказ 10 коробок"));
        // Ольге (ошибка) — только материалы тестовой отправки (текст и два файла), от неудачной — ничего.
        Assert.Equal(3, await db.TelegramMaterials.CountAsync(m => m.AccountId == accountId && m.PeerId == 5004));

        // «Повторить ошибки»: только ошибочные снова в очереди; отправленным повторно не уходит.
        fixture.Telegram.Fail = null;
        Assert.Equal("Queued", (await Campaigns(c => c.RetryFailedAsync(campaign.Id, fixture.AdminId))).Status);
        await runner.RunAllAsync(default);
        view = await Campaigns(c => c.GetAsync(campaign.Id));
        Assert.Equal(("Completed", 3), (view.Status, view.Counts.Sent));
        Assert.Equal(3, fixture.Telegram.Sent.Count);
        fixture.Clock.Now = null;
    }

    [PostgresFact]
    public async Task Campaign_pause_resume_cancel_flood_waits_and_restart_never_send_twice()
    {
        await fixture.ResetAsync();
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var runner = fixture.Services.GetRequiredService<TelegramCampaignRunner>();
        runner.Delay = (_, _) => Task.CompletedTask;
        var accountId = await ConnectAsync(sp);
        var chats = await db.TelegramChats.AsNoTracking().Where(c => c.AccountId == accountId && c.CanSend && c.Type == TelegramChatType.Private)
            .Select(c => c.Id).ToListAsync();

        async Task<Guid> NewAsync(string name, DateTimeOffset? at = null)
        {
            var created = await Campaigns(c => c.CreateAsync(new CampaignInput(accountId, name, null, $"Текст «{name}»", false, at, 5), fixture.AdminId));
            await Campaigns(c => c.AddRecipientsAsync(created.Id, chats));
            await Campaigns(c => c.LaunchAsync(created.Id, fixture.AdminId));
            return created.Id;
        }

        // Пауза посреди рассылки: текущая отправка завершается, остальные ждут; «Продолжить» — досылает без повторов.
        var paused = await NewAsync("Пауза");
        fixture.Telegram.BeforeSend = async _ =>
        {
            if (fixture.Telegram.Sent.Count == 1)
            {
                using var s = fixture.Services.CreateScope();
                await s.ServiceProvider.GetRequiredService<TelegramCampaignService>().PauseAsync(paused, fixture.AdminId);
            }
        };
        await runner.RunAllAsync(default);
        var view = await Campaigns(c => c.GetAsync(paused));
        Assert.Equal(("Paused", 2, 1), (view.Status, view.Counts.Sent, view.Counts.Pending));
        fixture.Telegram.BeforeSend = null;
        await runner.RunAllAsync(default);
        Assert.Equal(2, fixture.Telegram.Sent.Count); // на паузе не отправляет
        await Campaigns(c => c.ResumeAsync(paused, fixture.AdminId));
        await runner.RunAllAsync(default);
        view = await Campaigns(c => c.GetAsync(paused));
        Assert.Equal(("Completed", 3), (view.Status, view.Counts.Sent));
        Assert.Equal(3, fixture.Telegram.Sent.Count);

        // Отмена: неотправленные — «Отменено», ничего не уходит.
        var cancelled = await NewAsync("Отмена");
        view = await Campaigns(c => c.CancelAsync(cancelled, fixture.AdminId));
        Assert.Equal(("Cancelled", 3), (view.Status, view.Counts.Cancelled));
        await runner.RunAllAsync(default);
        Assert.Equal(3, fixture.Telegram.Sent.Count);

        // Перезапуск сервера посреди отправки: «Отправляется» → ошибка без автоповтора (неизвестно, ушло ли), остальные — по одному разу.
        var crashed = await NewAsync("Сбой");
        var stuck = await db.TelegramCampaignRecipients.Where(r => r.CampaignId == crashed).OrderBy(r => r.CreatedAt).Select(r => r.Id).FirstAsync();
        await db.TelegramCampaignRecipients.Where(r => r.Id == stuck)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, TelegramRecipientStatus.Sending).SetProperty(r => r.Attempts, 1));
        Assert.Equal(1, await runner.RecoverAsync(default));
        await runner.RunAllAsync(default);
        view = await Campaigns(c => c.GetAsync(crashed));
        Assert.Equal(("CompletedWithErrors", 2, 1), (view.Status, view.Counts.Sent, view.Counts.Failed));
        Assert.Contains("Неизвестно, дошло ли", view.Recipients.Single(r => r.Id == stuck).Error);
        Assert.Equal(5, fixture.Telegram.Sent.Count);

        // FLOOD_WAIT: ждём, сколько просит Telegram, попытка не считается; после ожидания — отправлено.
        var flood = await NewAsync("Флуд");
        var floods = 0;
        fixture.Telegram.Fail = (_, _) => floods++ == 0 ? TelegramRules.Classify(420, "FLOOD_WAIT_300", 300) : null;
        await runner.RunAllAsync(default);
        view = await Campaigns(c => c.GetAsync(flood));
        Assert.Equal(3, view.Counts.Pending);
        Assert.All(view.Recipients, r => Assert.Equal(0, r.Attempts));
        Assert.Contains("300", view.LastError);
        fixture.Clock.Now = fixture.Clock.Now!.Value.AddMinutes(6);
        await runner.RunAllAsync(default);
        Assert.Equal(("Completed", 3), ((await Campaigns(c => c.GetAsync(flood))).Status, (await Campaigns(c => c.GetAsync(flood))).Counts.Sent));

        // PEER_FLOOD (Telegram ограничил аккаунт за спам) — вся рассылка на паузе с причиной, получатели ждут.
        var spam = await NewAsync("Спам");
        fixture.Telegram.Fail = (_, _) => TelegramRules.Classify(400, "PEER_FLOOD", 0);
        await runner.RunAllAsync(default);
        view = await Campaigns(c => c.GetAsync(spam));
        Assert.Equal(("Paused", 3), (view.Status, view.Counts.Pending));
        Assert.Contains("PEER_FLOOD", view.LastError);

        // По расписанию: до времени не отправляется, после — уходит.
        fixture.Telegram.Fail = null;
        var scheduled = await NewAsync("Завтра", fixture.Clock.Now!.Value.AddHours(1));
        Assert.Equal("Scheduled", (await Campaigns(c => c.GetAsync(scheduled))).Status);
        var before = fixture.Telegram.Sent.Count;
        await runner.RunAllAsync(default);
        Assert.Equal(before, fixture.Telegram.Sent.Count);
        fixture.Clock.Now = fixture.Clock.Now!.Value.AddHours(2);
        await runner.RunAllAsync(default);
        Assert.Equal("Completed", (await Campaigns(c => c.GetAsync(scheduled))).Status);

        // Аккаунт отключили — очередь не шлёт, рассылка на паузе с понятной причиной.
        var offline = await NewAsync("Без аккаунта");
        await sp.GetRequiredService<TelegramAccountService>().DisconnectAsync(accountId, fixture.AdminId);
        await runner.RunAllAsync(default);
        view = await Campaigns(c => c.GetAsync(offline));
        Assert.Equal("Paused", view.Status);
        Assert.Contains("не подключён", view.LastError);
        fixture.Clock.Now = null;
    }
}

/// <summary>Правила модуля Telegram без базы: ошибки Telegram, повторы, итог рассылки, разметка, коды привязки, номера.</summary>
public class TelegramRulesTests
{
    [Theory]
    [InlineData(420, "FLOOD_WAIT_35", 35, TelegramErrorKind.Flood)]
    [InlineData(400, "PEER_FLOOD", 0, TelegramErrorKind.Permanent)]
    [InlineData(403, "USER_PRIVACY_RESTRICTED", 0, TelegramErrorKind.Permanent)]
    [InlineData(400, "USER_IS_BLOCKED", 0, TelegramErrorKind.Permanent)]
    [InlineData(403, "CHAT_WRITE_FORBIDDEN", 0, TelegramErrorKind.Permanent)]
    [InlineData(400, "PHONE_CODE_INVALID", 0, TelegramErrorKind.Auth)]
    [InlineData(401, "AUTH_KEY_UNREGISTERED", 0, TelegramErrorKind.Auth)]
    [InlineData(400, "API_ID_INVALID", 0, TelegramErrorKind.Config)]
    [InlineData(500, "INTERNAL_SERVER_ERROR", 0, TelegramErrorKind.Transient)]
    public void Telegram_errors_are_classified(int code, string rpc, int wait, TelegramErrorKind kind)
    {
        var error = TelegramRules.Classify(code, rpc, wait);
        Assert.Equal(kind, error.Kind);
        if (kind == TelegramErrorKind.Flood)
        {
            Assert.Equal(wait, error.WaitSeconds);
        }
    }

    [Fact]
    public void Campaign_outcome_and_retry_delays()
    {
        Assert.Null(TelegramRules.Outcome([TelegramRecipientStatus.Sent, TelegramRecipientStatus.Pending]));
        Assert.Equal(TelegramCampaignStatus.Completed, TelegramRules.Outcome([TelegramRecipientStatus.Sent, TelegramRecipientStatus.Cancelled]));
        Assert.Equal(TelegramCampaignStatus.CompletedWithErrors, TelegramRules.Outcome([TelegramRecipientStatus.Sent, TelegramRecipientStatus.Failed]));
        Assert.Equal(TelegramCampaignStatus.Cancelled, TelegramRules.Outcome([TelegramRecipientStatus.Cancelled]));
        Assert.Equal(TimeSpan.FromSeconds(30), TelegramRules.RetryDelay(1));
        Assert.Equal(TimeSpan.FromMinutes(10), TelegramRules.RetryDelay(9));
    }

    [Fact]
    public void Markdown_becomes_telegram_html_and_long_text_is_split()
    {
        var html = TelegramRules.MarkdownToTelegramHtml("## Итог\n**Хорезм** — 900 кг, *рост* <5%>\n- [отчёт](https://1base.uz/r)\n\n| Регион | Кг |\n|---|---|\n| Хорезм | 900 |");
        Assert.Contains("<b>Итог</b>", html);
        Assert.Contains("<b>Хорезм</b>", html);
        Assert.Contains("<i>рост</i>", html);
        Assert.Contains("&lt;5%&gt;", html);
        Assert.Contains("<a href=\"https://1base.uz/r\">отчёт</a>", html);
        Assert.Contains("<pre>Регион  Кг\nХорезм  900</pre>", html);
        var parts = TelegramRules.Split(string.Join('\n', Enumerable.Repeat(new string('я', 1000), 9)));
        Assert.Equal(3, parts.Count);
        Assert.All(parts, p => Assert.True(p.Length <= TelegramRules.MaxMessageLength));
    }

    /// <summary>Ответы API сериализуются (имена свойств без учёта регистра не совпадают: Username и UserName сломали бы ответ).</summary>
    [Theory]
    [InlineData(typeof(TelegramOverview))]
    [InlineData(typeof(TelegramLoginView))]
    [InlineData(typeof(TelegramPermissionView))]
    [InlineData(typeof(TelegramChatView))]
    [InlineData(typeof(TelegramExecutionView))]
    [InlineData(typeof(TelegramLinkCodeView))]
    [InlineData(typeof(TelegramLinkView))]
    [InlineData(typeof(TelegramOneBaseUser))]
    [InlineData(typeof(TelegramAgentSettings))]
    [InlineData(typeof(TelegramAccessView))]
    [InlineData(typeof(TelegramAccountView))]
    [InlineData(typeof(AgentTestResult))]
    [InlineData(typeof(CampaignView))]
    [InlineData(typeof(CampaignListItem))]
    [InlineData(typeof(CampaignChat))]
    [InlineData(typeof(CampaignPreview))]
    [InlineData(typeof(RecipientsResult))]
    [InlineData(typeof(UserReport))]
    public void Api_views_serialize_with_web_json_options(Type type) =>
        Assert.NotNull(new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() }
            .GetTypeInfo(type));

    /// <summary>Просьба что-то сформировать (отчёт, письмо, таблицу, план) — модель генерации; обычный вопрос — модель ответов.</summary>
    [Theory]
    [InlineData("Собери отчёт по продажам за сентябрь", true)]
    [InlineData("Составь письмо дилеру о скидке", true)]
    [InlineData("Сделай таблицу по регионам", true)]
    [InlineData("Подготовь план продаж на ноябрь", true)]
    [InlineData("Напиши пост для канала про новый кекс", true)]
    [InlineData("Viloyatlar bo'yicha jadval tuzib ber", true)]
    [InlineData("Dilerga xat yozing", true)]
    [InlineData("Продажи по регионам в Excel", true)]
    [InlineData("Нужен отчёт по продажам за сентябрь", true)]
    [InlineData("Пришли отчёт по Хорезму", true)]
    [InlineData("Hisobot kerak", true)]
    [InlineData("Когда будет следующий отчёт?", false)]
    [InlineData("Что в отчёте?", false)]
    [InlineData("Насколько всё плохо по отчёту?", false)]
    [InlineData("Hisobot qachon keladi?", false)]
    [InlineData("Какие продажи в Хорезме за сентябрь?", false)]
    [InlineData("Сколько осталось на складе?", false)]
    [InlineData("Собери данные по поставкам", false)]
    [InlineData("Напиши, кто сегодня не вышел на маршрут", false)]
    [InlineData("Xorazmda savdo qanday?", false)]
    [InlineData("", false)]
    public void Generation_requests_are_told_apart_from_questions(string question, bool generation) =>
        Assert.Equal(generation, GenerationIntent.Requested(question));

    [Theory]
    [InlineData("auto", "Составь письмо дилеру", "Минутку")]
    [InlineData("auto", "Viloyatlar bo'yicha jadval tuzib ber", "daqiqa")]
    [InlineData("auto", "Ҳисобот тайёрла", "daqiqa")]
    [InlineData("auto", "Please prepare a sales report", "One moment")]
    [InlineData("ru", "Prepare a report", "Минутку")]
    [InlineData("uz", "Сделай отчёт", "daqiqa")]
    public void Ack_is_in_the_language_of_the_chat(string language, string question, string expected) =>
        Assert.Contains(expected, GenerationIntent.Ack(language, question, 0));

    [Fact]
    public void Ack_phrase_varies_and_document_title_comes_from_the_heading()
    {
        Assert.Equal(3, Enumerable.Range(0, 6).Select(i => GenerationIntent.Ack("ru", "", i)).Distinct().Count());
        Assert.NotEmpty(GenerationIntent.Ack("ru", "", -1));
        Assert.Equal("План продаж на ноябрь", TelegramAgentService.DocumentTitle("# План продаж на **ноябрь**\n\nТекст", "вопрос"));
        Assert.Equal("Уважаемый партнёр!", TelegramAgentService.DocumentTitle("Уважаемый партнёр!\n\nТекст", "вопрос"));
        Assert.EndsWith("…", TelegramAgentService.DocumentTitle(string.Join(' ', Enumerable.Repeat("очень длинная строка", 10)), "вопрос"));
    }

    /// <summary>Документ Word из Markdown AI проходит проверку схемы Open XML (Word его откроет) и содержит текст, списки и таблицу.</summary>
    [Fact]
    public void Markdown_becomes_a_valid_word_document()
    {
        var markdown = "# Отчёт по Хорезму\n\nВыполнение **96%**, рост *к августу* +4% ([страница](https://1base.uz/sales)).\n\n## Что сделать\n\n1. Догрузить кекс\n2. Проверить `ТП-12`\n   - вложенный пункт\n\n> Важно: срок до 31.10\n\n---\n\n| Регион | Кг | % |\n|:---|---:|---:|\n| Хорезм | 35 600 | 96 |\n| Бухара | 12 000 |\n\n```\nкод\n```\nстрока\u0001с управляющим знаком";
        var bytes = new OneBase.Infrastructure.Reports.DocumentWriter().Write("Отчёт по Хорезму", markdown, DateTimeOffset.UtcNow);
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(document).Select(e => $"{e.Path?.XPath}: {e.Description}").ToList();
        Assert.Empty(errors);
        var text = document.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("Выполнение 96%", text);
        Assert.Contains("страница (https://1base.uz/sales)", text);
        Assert.Contains("Догрузить кекс", text);
        Assert.Contains("35 600", text);
        Assert.Equal("Отчёт по Хорезму", document.PackageProperties.Title);
        Assert.Single(document.MainDocumentPart.Document.Body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>());
    }

    [Fact]
    public void Link_codes_phones_and_usernames()
    {
        var code = TelegramRules.NewLinkCode();
        Assert.Matches("^OB-[A-HJ-NP-Z2-9]{8}$", code);
        Assert.Equal(code, TelegramRules.FindLinkCode($"вот мой код: {code.ToLowerInvariant()}!"));
        Assert.Null(TelegramRules.FindLinkCode("OB-123"));
        Assert.Equal(TelegramRules.HashLinkCode(code), TelegramRules.HashLinkCode(" " + code.ToLowerInvariant()));
        Assert.Equal("+998901234567", TelegramAccountService.NormalizePhone("+998 (90) 123-45-67"));
        Assert.Throws<TelegramException>(() => TelegramAccountService.NormalizePhone("12345"));
        Assert.Equal("ivan_petrov", TelegramAccountService.NormalizeUsername("https://t.me/Ivan_Petrov"));
        Assert.Throws<TelegramException>(() => TelegramAccountService.NormalizeUsername("иван"));
    }
}

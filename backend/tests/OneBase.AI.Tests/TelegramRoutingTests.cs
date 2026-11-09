using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.AI.Telegram;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Identity;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram;
using OneBase.Infrastructure.Telegram.Reports;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Маршрутизация входящих Telegram по подразделениям (этап 1 HR Recruitment): аккаунт без подразделений — всё у продаж, как раньше;
/// номер HR не доходит до агента продаж; «не распределён» — без ответа; признаки подразделения, конфликт и люди из списка продаж — к
/// человеку; хозяина меняет только человек, с аудитом; очередь чата строго по порядку, слоты подразделений не общие. Telegram и модель —
/// подставные: ничего не уходит наружу.
/// </summary>
public class TelegramRoutingTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly TelegramPeerRef Ivan = new(5001, TelegramChatType.Private, false, 11);
    private static readonly TelegramPeerRef Dealer = new(5002, TelegramChatType.Private, false, 22);
    private static readonly TelegramPeerRef Olga = new(5004, TelegramChatType.Private, false, 44);
    private static readonly TelegramPeerRef Group = new(9001, TelegramChatType.Group, true, 99);

    private const string ApiHash = "0123456789abcdef0123456789abcdef";

    private TelegramIncoming Msg(Guid account, TelegramPeerRef chat, int id, long from, string text, string? username = null, bool outgoing = false) =>
        new(account, chat, "чат", id, from, username, "Гость", text, fixture.Clock.GetUtcNow(), outgoing, false);

    /// <summary>Ключи приложения и подключённый аккаунт; агент продаж включён и отвечает всем (как самый «разговорчивый» случай).</summary>
    private async Task<Guid> ConnectAsync(IServiceProvider sp, string name, long telegramId)
    {
        if (fixture.Telegram.Dialogs.Count == 0)
        {
            fixture.Telegram.Dialogs.AddRange([
                new TelegramDialog(Ivan, "Иван Петров", "ivan", true),
                new TelegramDialog(Dealer, "Дилер Хорезм", null, true),
                new TelegramDialog(Olga, "Ольга", "olga", true),
                new TelegramDialog(Group, "Продажи — регионы", null, true),
            ]);
        }

        fixture.Telegram.Me = new TelegramMe(telegramId, $"onebase_{telegramId}", name);
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        await accounts.SaveCredentialsAsync(123456, ApiHash, fixture.AdminId);
        var account = await accounts.CreateAccountAsync(name, fixture.AdminId);
        await accounts.StartLoginAsync(account.Id, "+998 90 123 45 67", fixture.AdminId);
        await accounts.SubmitCodeAsync(account.Id, fixture.Telegram.Code, fixture.AdminId);
        await accounts.SaveSettingsAsync(account.Id,
            new TelegramSettingsInput(true, true, true, null, null, null, null, "normal", "auto", 30, 1500, 0, "Нет данных.", "pdf,txt", 20, false),
            fixture.AdminId);
        return account.Id;
    }

    /// <summary>Подразделения OneBase, как их создаёт DbSeeder (в тестовой базе их нет).</summary>
    private static async Task DepartmentsAsync(OneBaseDbContext db)
    {
        foreach (var (code, name) in new[] { ("sales", "Продажи"), ("hr", "HR"), ("production", "Производство") })
        {
            if (!await db.Departments.AnyAsync(d => d.Code == code))
            {
                db.Departments.Add(new Department { Code = code, Name = name });
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task SetDepartmentsAsync(IServiceProvider sp, Guid accountId, string? fallback, params string[] departments)
    {
        await DepartmentsAsync(sp.GetRequiredService<OneBaseDbContext>());
        await sp.GetRequiredService<TelegramDepartmentService>().SaveAsync(accountId, new TelegramDepartmentsInput(departments, fallback), fixture.AdminId);
    }

    private TelegramAgentWorker Worker(IServiceScopeFactory scopes, bool routing = true) =>
        new(new TelegramInbox(), scopes,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [TelegramAgentWorker.RoutingKey] = routing ? "true" : "false" }).Build(),
            NullLogger<TelegramAgentWorker>.Instance);

    private static async Task DrainAsync(TelegramAgentWorker worker)
    {
        for (var i = 0; i < 400 && worker.Pending > 0; i++)
        {
            await Task.Delay(25);
        }

        Assert.Equal(0, worker.Pending);
    }

    // ---------- аккаунт без подразделений и номер HR ----------

    [PostgresFact]
    public async Task Account_without_departments_keeps_every_message_with_the_sales_agent()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accountId = await ConnectAsync(sp, "Отдел продаж", 777_000_111);
        fixture.Gateway.Script.Enqueue(_ => "Здравствуйте! Чем помочь?");

        var worker = Worker(fixture.Services.GetRequiredService<IServiceScopeFactory>());
        await worker.EnqueueAsync(Msg(accountId, Dealer, 1, 5002, "Здравствуйте"), default);
        await DrainAsync(worker);

        // Ответ агента продаж — как до маршрутизации; диалоги не записываются вовсе.
        var sent = Assert.Single(fixture.Telegram.Sent);
        Assert.Equal(Dealer.PeerId, sent.Peer.PeerId);
        Assert.Equal("Здравствуйте! Чем помочь?", sent.Message.Text);
        Assert.Equal("processed", (await db.TelegramIncomingMessages.AsNoTracking().SingleAsync(m => m.AccountId == accountId)).Status);
        Assert.False(await db.TelegramConversations.AnyAsync());
        Assert.Equal(TelegramRoute.Legacy, await sp.GetRequiredService<TelegramMessageRouter>().RouteAsync(Msg(accountId, Dealer, 2, 5002, "Ещё"), default));
    }

    [PostgresFact]
    public async Task Hr_number_never_reaches_the_sales_agent_and_is_hidden_from_sales_pickers()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var salesId = await ConnectAsync(sp, "Отдел продаж", 777_000_111);
        var hrId = await ConnectAsync(sp, "HR", 777_000_222);
        await SetDepartmentsAsync(sp, hrId, "hr", "hr");

        // Агент продаж на номере HR включён и «отвечает всем» — и всё равно не получает сообщений: их получает обработчик HR (AI-рекрутер по
        // умолчанию выключен — никто не отвечает автоматически).
        var worker = Worker(fixture.Services.GetRequiredService<IServiceScopeFactory>());
        await worker.EnqueueAsync(Msg(hrId, Dealer, 1, 5002, "Здравствуйте, я по вакансии"), default);
        await worker.EnqueueAsync(Msg(hrId, Dealer, 2, 5002, "Вы тут?"), default);
        await DrainAsync(worker);

        Assert.Empty(fixture.Telegram.Sent);
        Assert.All(await db.TelegramIncomingMessages.AsNoTracking().Where(m => m.AccountId == hrId).ToListAsync(), m => Assert.Equal("hr", m.Reason));
        Assert.False(await db.TelegramAgentExecutions.AnyAsync(e => e.AccountId == hrId));
        var conversation = await db.TelegramConversations.AsNoTracking().SingleAsync(c => c.AccountId == hrId);
        Assert.Equal(("hr", "default", 5002L), (conversation.Department, conversation.RouteReason, conversation.TelegramUserId));
        Assert.NotNull(conversation.LastInboundAt);

        // Настройки: HR включён и по умолчанию, обработчика у него пока нет; продажи — с обработчиком.
        var view = await sp.GetRequiredService<TelegramDepartmentService>().GetAsync(hrId);
        Assert.True(view.Configured);
        Assert.Equal((true, true, true), view.Departments.Where(d => d.Code == "hr").Select(d => (d.Enabled, d.IsDefault, d.HasHandler)).Single());
        Assert.Equal((false, true), view.Departments.Where(d => d.Code == "sales").Select(d => (d.Enabled, d.HasHandler)).Single());
        var legacy = await sp.GetRequiredService<TelegramDepartmentService>().GetAsync(salesId);
        Assert.False(legacy.Configured);
        Assert.True(legacy.Departments.Single(d => d.Code == "sales").IsDefault);

        // Рассылки и автоотчёты продаж номер HR не предлагают и не принимают.
        var campaigns = sp.GetRequiredService<TelegramCampaignService>();
        Assert.Equal([salesId], (await campaigns.AccountsAsync()).Select(a => a.Id));
        await Assert.ThrowsAsync<TelegramException>(() => campaigns.CreateAsync(new CampaignInput(hrId, "Акция", null, "Текст", false, null, 5), fixture.AdminId));
        var reports = sp.GetRequiredService<TelegramReportService>();
        Assert.Equal([salesId], (await reports.OverviewAsync(false, default)).Accounts.Select(a => a.Id));
        await Assert.ThrowsAsync<ReportValidationException>(() => reports.SaveSettingsAsync(new ReportSettingsInput(hrId, "ru", 30, true), fixture.AdminId, default));
        await reports.SaveSettingsAsync(new ReportSettingsInput(salesId, "ru", 30, true), fixture.AdminId, default);
    }

    [PostgresFact]
    public async Task Message_in_a_dialog_led_by_a_human_is_still_saved_to_the_history_without_an_ai_reply()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var hrId = await ConnectAsync(sp, "HR", 777_000_222);
        await SetDepartmentsAsync(sp, hrId, "hr", "hr");
        var worker = Worker(fixture.Services.GetRequiredService<IServiceScopeFactory>());
        await worker.EnqueueAsync(Msg(hrId, Dealer, 1, 5002, "Здравствуйте"), default);
        await DrainAsync(worker);

        // HR взял диалог — следующее сообщение кандидата всё равно попадает в историю OneBase (раньше терялось), AI молчит.
        await sp.GetRequiredService<TelegramConversationService>().SetModeAsync(hrId, Dealer.PeerId, TelegramConversationMode.Manual, fixture.AdminId);
        await worker.EnqueueAsync(Msg(hrId, Dealer, 2, 5002, "Когда собеседование?"), default);
        await DrainAsync(worker);
        Assert.Contains("Когда собеседование?", await db.TelegramConversationMessages.AsNoTracking().Select(m => m.Text).ToListAsync());
        Assert.Empty(fixture.Telegram.Sent);
        Assert.Empty(await db.TelegramOutgoingMessages.AsNoTracking().ToListAsync());
    }

    [PostgresFact]
    public async Task Unassigned_dialog_gets_no_reply_from_anyone_until_a_human_assigns_it()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accountId = await ConnectAsync(sp, "Общий номер", 777_000_111);
        await SetDepartmentsAsync(sp, accountId, null, "sales", "hr");

        var worker = Worker(fixture.Services.GetRequiredService<IServiceScopeFactory>());
        await worker.EnqueueAsync(Msg(accountId, Dealer, 1, 5002, "Добрый день"), default);
        await DrainAsync(worker);
        Assert.Empty(fixture.Telegram.Sent);
        Assert.False(await db.TelegramIncomingMessages.AnyAsync());
        var unassigned = Assert.Single(await sp.GetRequiredService<TelegramConversationService>().ListAsync(accountId, TelegramConversationService.Unassigned));
        Assert.Equal(("no_default", "Дилер Хорезм"), (unassigned.RouteReason, unassigned.Title));

        // Человек отдал диалог продажам — следующее сообщение отвечает агент продаж.
        await sp.GetRequiredService<TelegramConversationService>().AssignAsync(accountId, Dealer.PeerId, "sales", fixture.AdminId);
        fixture.Gateway.Script.Enqueue(_ => "Здравствуйте!");
        await worker.EnqueueAsync(Msg(accountId, Dealer, 2, 5002, "Есть кто?"), default);
        await DrainAsync(worker);
        Assert.Equal("Здравствуйте!", Assert.Single(fixture.Telegram.Sent).Message.Text);
        Assert.Empty(await sp.GetRequiredService<TelegramConversationService>().ListAsync(accountId, TelegramConversationService.Unassigned));
    }

    // ---------- признаки подразделения ----------

    private sealed class CodeClaim(string department, string code) : ITelegramRouteClaim
    {
        public string Department => department;

        public ValueTask<bool> ClaimsAsync(TelegramIncoming message, CancellationToken ct) => ValueTask.FromResult(message.Text.Contains(code, StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task Claims_route_a_new_dialog_once_conflicts_go_to_a_human_and_sales_users_stay_with_sales()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var accountId = await ConnectAsync(sp, "Общий номер", 777_000_111);
        await SetDepartmentsAsync(sp, accountId, "sales", "sales", "hr");
        var router = new TelegramMessageRouter(db,
            [new CodeClaim("hr", "#V12"), new CodeClaim("sales", "#PRICE"), new CodeClaim("production", "#PROD")], NullLogger<TelegramMessageRouter>.Instance);

        // Признак HR в первом сообщении → диалог у HR; дальше и без признака — у HR, признак продаж хозяина не меняет.
        var stranger = new TelegramPeerRef(6001, TelegramChatType.Private, false, 61);
        Assert.Equal(("hr", "claim"), Pair(await router.RouteAsync(Msg(accountId, stranger, 1, 6001, "Здравствуйте, #V12"), default)));
        Assert.Equal(("hr", "claim"), Pair(await router.RouteAsync(Msg(accountId, stranger, 2, 6001, "А цены? #PRICE"), default)));
        Assert.Equal(1, await db.TelegramConversations.CountAsync(c => c.PeerId == 6001));

        // Признаки двух подразделений — «не распределён»; признак выключенного подразделения не считается.
        var both = new TelegramPeerRef(6002, TelegramChatType.Private, false, 62);
        Assert.Equal(((string?)null, "conflict"), Pair(await router.RouteAsync(Msg(accountId, both, 1, 6002, "#V12 и #PRICE"), default)));
        var prod = new TelegramPeerRef(6003, TelegramChatType.Private, false, 63);
        Assert.Equal(("sales", "default"), Pair(await router.RouteAsync(Msg(accountId, prod, 1, 6003, "#PROD"), default)));

        // Человек из списка агента продаж (по Telegram ID и ожидающий — по username) к HR сам не уходит — остаётся у продаж.
        var accounts = sp.GetRequiredService<TelegramAccountService>();
        await accounts.SavePermissionAsync(accountId, null, new TelegramPermissionInput(5001, null, "Иван", TelegramAccessMode.SentOnly, null, false, true), fixture.AdminId);
        db.TelegramUserPermissions.Add(new TelegramUserPermission { AccountId = accountId, Username = "olga", CreatedById = fixture.AdminId });
        await db.SaveChangesAsync();
        Assert.Equal(("sales", "sales_user"), Pair(await router.RouteAsync(Msg(accountId, Ivan, 1, 5001, "#V12 хочу к вам"), default)));
        Assert.Equal(("sales", "sales_user"), Pair(await router.RouteAsync(Msg(accountId, Olga, 1, 5004, "#V12", username: "@Olga"), default)));

        // Свои исходящие и боты диалог не открывают.
        var quiet = new TelegramPeerRef(6004, TelegramChatType.Private, false, 64);
        Assert.Equal(("sales", "service"), Pair(await router.RouteAsync(Msg(accountId, quiet, 1, 777_000_111, "#V12", outgoing: true), default)));
        Assert.False(await db.TelegramConversations.AnyAsync(c => c.PeerId == 6004));
    }

    private static (string? Department, string Reason) Pair(TelegramRoute route) => (route.Department, route.Reason);

    // ---------- ручное управление и аудит ----------

    [PostgresFact]
    public async Task Only_a_human_changes_the_owner_or_stops_the_ai_and_every_change_is_audited()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var since = await db.AuditLogs.MaxAsync(a => (long?)a.Id) ?? 0;
        var legacyId = await ConnectAsync(sp, "Отдел продаж", 777_000_111);
        var accountId = await ConnectAsync(sp, "Общий номер", 777_000_222);
        await SetDepartmentsAsync(sp, accountId, "sales", "sales", "hr");
        var conversations = sp.GetRequiredService<TelegramConversationService>();
        var router = sp.GetRequiredService<TelegramMessageRouter>();

        // Аккаунт без подразделений — назначать некому; подразделение не этого аккаунта — отказ; неизвестный чат — не найден.
        await Assert.ThrowsAsync<TelegramException>(() => conversations.AssignAsync(legacyId, Dealer.PeerId, "hr", fixture.AdminId));
        await Assert.ThrowsAsync<TelegramException>(() => conversations.AssignAsync(accountId, Dealer.PeerId, "production", fixture.AdminId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => conversations.AssignAsync(accountId, 424242, "hr", fixture.AdminId));

        // Передать HR чат, где ещё не писали; остановить AI — AI не отвечает; вернуть — отвечает.
        var assigned = await conversations.AssignAsync(accountId, Dealer.PeerId, "hr", fixture.AdminId);
        Assert.Equal(("hr", "manual"), (assigned.Department, assigned.RouteReason));
        Assert.True((await router.RouteAsync(Msg(accountId, Dealer, 1, 5002, "Привет"), default)).AutoReply);
        await conversations.SetModeAsync(accountId, Dealer.PeerId, TelegramConversationMode.Manual, fixture.AdminId);
        var manual = await router.RouteAsync(Msg(accountId, Dealer, 2, 5002, "Привет"), default);
        Assert.Equal(("hr", false), (manual.Department, manual.AutoReply));
        await conversations.SetModeAsync(accountId, Dealer.PeerId, TelegramConversationMode.Ai, fixture.AdminId);
        Assert.True((await router.RouteAsync(Msg(accountId, Dealer, 3, 5002, "Привет"), default)).AutoReply);

        // HR больше не работает с аккаунтом — его диалоги «не распределены», пока человек не решит.
        await SetDepartmentsAsync(sp, accountId, "sales", "sales");
        Assert.Equal(((string?)null, "department_disabled"), Pair(await router.RouteAsync(Msg(accountId, Dealer, 4, 5002, "Привет"), default)));
        await Assert.ThrowsAsync<TelegramException>(() => conversations.AssignAsync(accountId, Dealer.PeerId, "hr", fixture.AdminId));
        await conversations.AssignAsync(accountId, Dealer.PeerId, null, fixture.AdminId);

        var actions = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Id > since && (a.Action.StartsWith("telegram.conversation.") || a.Action == "telegram.account.departments"))
            .OrderBy(a => a.Id).Select(a => a.Action).ToListAsync();
        Assert.Equal(["telegram.account.departments", "telegram.conversation.assigned", "telegram.conversation.mode", "telegram.conversation.mode",
            "telegram.account.departments", "telegram.conversation.assigned"], actions);
    }

    [PostgresFact]
    public async Task Department_settings_are_validated()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await DepartmentsAsync(sp.GetRequiredService<OneBaseDbContext>());
        var accountId = await ConnectAsync(sp, "HR", 777_000_222);
        var departments = sp.GetRequiredService<TelegramDepartmentService>();

        await Assert.ThrowsAsync<TelegramException>(() => departments.SaveAsync(accountId, new TelegramDepartmentsInput([], null), fixture.AdminId));
        await Assert.ThrowsAsync<TelegramException>(() => departments.SaveAsync(accountId, new TelegramDepartmentsInput(["marketing-x"], null), fixture.AdminId));
        await Assert.ThrowsAsync<TelegramException>(() => departments.SaveAsync(accountId, new TelegramDepartmentsInput(["hr"], "sales"), fixture.AdminId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => departments.SaveAsync(Guid.NewGuid(), new TelegramDepartmentsInput(["hr"], "hr"), fixture.AdminId));

        // Смена «по умолчанию» с продаж на HR в одном сохранении; по умолчанию — не больше одного.
        await departments.SaveAsync(accountId, new TelegramDepartmentsInput(["sales", "hr"], "sales"), fixture.AdminId);
        var view = await departments.SaveAsync(accountId, new TelegramDepartmentsInput(["hr", "sales", "hr"], "hr"), fixture.AdminId);
        Assert.Equal(["hr"], view.Departments.Where(d => d.IsDefault).Select(d => d.Code));
        Assert.Equal(["hr", "sales"], view.Departments.Where(d => d.Enabled).Select(d => d.Code).Order());
    }

    // ---------- очередь чата и слоты подразделений ----------

    /// <summary>Подставной обработчик подразделения: записывает порядок, считает одновременные, может ждать «отпускания».</summary>
    private sealed class RecordingHandler(string department) : ITelegramDepartmentHandler
    {
        private int _running;

        public string Department => department;
        public ConcurrentQueue<(long Peer, int Message)> Handled { get; } = new();
        public TaskCompletionSource Release { get; set; } = CompletedGate();
        public int MaxConcurrent;
        public int Running => Volatile.Read(ref _running);

        private static TaskCompletionSource CompletedGate()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            gate.SetResult();
            return gate;
        }

        public async Task HandleAsync(TelegramIncoming message, TelegramRoute route, CancellationToken ct)
        {
            var now = Interlocked.Increment(ref _running);
            InterlockedMax(ref MaxConcurrent, now);
            try
            {
                await Task.Delay(Random.Shared.Next(1, 8), ct);
                await Release.Task.WaitAsync(ct);
                Handled.Enqueue((message.Chat.PeerId, message.MessageId));
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }
    }

    /// <summary>Области DI фикстуры, где обработчики подразделений — только подставные.</summary>
    private sealed class FakeHandlers(IServiceScopeFactory inner, params ITelegramDepartmentHandler[] handlers) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(inner.CreateScope(), handlers);

        private sealed class Scope(IServiceScope scope, ITelegramDepartmentHandler[] handlers) : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(IEnumerable<ITelegramDepartmentHandler>) ? handlers : scope.ServiceProvider.GetService(serviceType);

            public void Dispose() => scope.Dispose();
        }
    }

    [PostgresFact]
    public async Task Messages_of_one_chat_keep_their_order_and_departments_do_not_share_slots()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var salesId = await ConnectAsync(sp, "Отдел продаж", 777_000_111);
        var hrId = await ConnectAsync(sp, "HR", 777_000_222);
        await SetDepartmentsAsync(sp, hrId, "hr", "hr");
        var sales = new RecordingHandler("sales");
        var hr = new RecordingHandler("hr") { Release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var worker = Worker(new FakeHandlers(fixture.Services.GetRequiredService<IServiceScopeFactory>(), sales, hr));

        // HR занят: три кандидата пишут, HR думает — работают ровно 2 слота HR, третий ждёт.
        for (var i = 0; i < 3; i++)
        {
            await worker.EnqueueAsync(Msg(hrId, new TelegramPeerRef(7000 + i, TelegramChatType.Private, false, 70 + i), 1, 7000 + i, "Здравствуйте"), default);
        }

        for (var i = 0; i < 200 && hr.Running < 2; i++)
        {
            await Task.Delay(25);
        }

        // Продажи тем временем отвечают: десять сообщений одного чата — по одному и строго по порядку.
        for (var id = 1; id <= 10; id++)
        {
            await worker.EnqueueAsync(Msg(salesId, Dealer, id, 5002, $"Сообщение {id}"), default);
        }

        for (var i = 0; i < 400 && sales.Handled.Count < 10; i++)
        {
            await Task.Delay(25);
        }

        Assert.Equal(Enumerable.Range(1, 10), sales.Handled.Select(h => h.Message));
        Assert.Equal(1, sales.MaxConcurrent);
        Assert.Equal(2, hr.Running);
        Assert.Empty(hr.Handled);

        hr.Release.SetResult();
        await DrainAsync(worker);
        Assert.Equal(3, hr.Handled.Count);
        Assert.Equal(2, hr.MaxConcurrent);
    }

    [PostgresFact]
    public async Task Routing_switch_off_returns_every_message_to_the_sales_agent()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var hrId = await ConnectAsync(sp, "HR", 777_000_222);
        await SetDepartmentsAsync(sp, hrId, "hr", "hr");
        var sales = new RecordingHandler("sales");
        var hr = new RecordingHandler("hr");
        var worker = Worker(new FakeHandlers(fixture.Services.GetRequiredService<IServiceScopeFactory>(), sales, hr), routing: false);

        await worker.EnqueueAsync(Msg(hrId, Dealer, 1, 5002, "Здравствуйте"), default);
        await DrainAsync(worker);

        Assert.Single(sales.Handled);
        Assert.Empty(hr.Handled);
        Assert.False(await sp.GetRequiredService<OneBaseDbContext>().TelegramConversations.AnyAsync());
    }

    // ---------- «ответ на сообщение» из MTProto ----------

    [Theory]
    [InlineData(true, false, 7)]
    [InlineData(true, true, null)]
    [InlineData(false, false, null)]
    public void Reply_to_message_id_is_taken_only_for_replies_within_the_chat(bool hasMessage, bool otherChat, int? expected)
    {
        var flags = (hasMessage ? TL.MessageReplyHeader.Flags.has_reply_to_msg_id : 0)
                    | (otherChat ? TL.MessageReplyHeader.Flags.has_reply_to_peer_id : 0);
        var message = new TL.Message { reply_to = new TL.MessageReplyHeader { flags = flags, reply_to_msg_id = hasMessage ? 7 : 0 } };
        var gateway = typeof(TelegramAccountService).Assembly.GetType("OneBase.Infrastructure.Telegram.WTelegramGateway")!;
        var replyTo = gateway.GetMethod("ReplyTo", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(expected, (int?)replyTo.Invoke(null, [message]));
        Assert.Null((int?)replyTo.Invoke(null, [new TL.Message()]));
    }
}

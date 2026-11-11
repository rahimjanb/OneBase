using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Jobs;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Telegram;

namespace OneBase.AI.Tests;

/// <summary>
/// Обвязка тестов AI-рекрутера: номер HR (подставной Telegram), включённый рекрутер, чаты кандидатов, сообщения с вложениями, очередь исходящих
/// и фоновые задания подбора. Ни настоящих сообщений, ни платных вызовов: модель — FakeGateway, хранилище — в памяти.
/// </summary>
public sealed class HrHarness(DataManagerDb fixture, long firstPeer)
{
    private long _peer = firstPeer;

    public DataManagerDb Fixture => fixture;
    public Guid Account { get; private set; }
    public Guid Sales { get; private set; }
    public RecruitmentContext Hr => new(fixture.AdminId, true, true, []);

    public sealed record Chat(Guid Account, TelegramPeerRef Peer, long User)
    {
        public int Next;
    }

    public async Task<(IServiceScope Scope, OneBaseDbContext Db)> StartAsync(bool recruiter = true)
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.RecruitmentJobs.ExecuteDeleteAsync();
        await db.RecruitmentExceptions.ExecuteDeleteAsync();
        await db.Interviews.ExecuteDeleteAsync();
        await db.Candidates.ExecuteDeleteAsync();
        await db.Vacancies.ExecuteDeleteAsync();
        await db.RecruitmentSettings.ExecuteDeleteAsync();
        await db.CalendarEvents.ExecuteDeleteAsync();
        foreach (var (code, name) in new[] { ("sales", "Продажи"), ("hr", "HR") })
        {
            if (!await db.Departments.AnyAsync(d => d.Code == code))
            {
                db.Departments.Add(new Department { Code = code, Name = name });
            }
        }

        await db.SaveChangesAsync();
        Sales = (await db.Departments.FirstAsync(d => d.Code == "sales")).Id;
        fixture.Telegram.Me = new TelegramMe(777_000_555, "onebase_hr_docs", "HR");
        var accounts = scope.ServiceProvider.GetRequiredService<TelegramAccountService>();
        await accounts.SaveCredentialsAsync(123456, "0123456789abcdef0123456789abcdef", fixture.AdminId);
        var account = await accounts.CreateAccountAsync("HR", fixture.AdminId);
        await accounts.StartLoginAsync(account.Id, "+998 90 000 00 05", fixture.AdminId);
        await accounts.SubmitCodeAsync(account.Id, fixture.Telegram.Code, fixture.AdminId);
        await scope.ServiceProvider.GetRequiredService<TelegramDepartmentService>().SaveAsync(account.Id, new TelegramDepartmentsInput(["hr"], "hr"), fixture.AdminId);
        Account = account.Id;
        if (recruiter)
        {
            await RecruitmentFlowTests.EnableRecruiterAsync(scope.ServiceProvider);
        }

        return (scope, db);
    }

    public Chat NewChat()
    {
        var peer = ++_peer;
        return new Chat(Account, new TelegramPeerRef(peer, TelegramChatType.Private, false, 81), peer);
    }

    /// <summary>Сообщение кандидата (с файлом — документ или фото: байты «лежат» в подставном Telegram до скачивания).</summary>
    public async Task SayAsync(Chat chat, string text, string? file = null, byte[]? bytes = null, string? mime = null, bool photo = false)
    {
        var id = ++chat.Next;
        TelegramIncomingFile? document = null, picture = null;
        if (file is not null)
        {
            fixture.Telegram.Files[(chat.Peer.PeerId, id)] = bytes ?? [];
            var meta = new TelegramIncomingFile(file, mime, bytes?.LongLength ?? 0);
            if (photo)
            {
                picture = meta;
            }
            else
            {
                document = meta;
            }
        }

        var m = new TelegramIncoming(chat.Account, chat.Peer, "Кандидат", id, chat.User, null, "Кандидат Тестов", text, fixture.Clock.GetUtcNow(), false, false,
            document, Photo: picture);
        using var scope = fixture.Services.CreateScope();
        var route = await scope.ServiceProvider.GetRequiredService<TelegramMessageRouter>().RouteAsync(m, default);
        await scope.ServiceProvider.GetServices<ITelegramDepartmentHandler>().Single(h => h.Department == "hr").HandleAsync(m, route, default);
    }

    /// <summary>Кандидат поздоровался, рекрутер сразу ответил (согласия не спрашивает) — тест идёт к документам.</summary>
    public async Task HelloAsync(Chat chat)
    {
        fixture.Gateway.Script.Enqueue(_ => "{\"action\":\"reply\",\"messages\":[\"Здравствуйте! Расскажите, какая работа вам интересна?\"]}");
        await SayAsync(chat, "Здравствуйте, хочу работать у вас");
        await SendQueuedAsync();
    }

    public async Task SendQueuedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.TelegramAccountDepartments.ExecuteUpdateAsync(u => u.SetProperty(d => d.MinIntervalSeconds, 1));
        for (var i = 0; i < 30 && await db.TelegramOutgoingMessages.AnyAsync(m => m.Status == TelegramOutboxStatus.Pending); i++)
        {
            await db.TelegramOutgoingMessages.ExecuteUpdateAsync(u => u.SetProperty(m => m.SentAt, (DateTimeOffset?)null));
            await scope.ServiceProvider.GetRequiredService<TelegramOutboxRunner>().RunOnceAsync(default);
        }
    }

    /// <summary>Выполнить фоновые задания подбора (паузы перед повторами — сдвигом часов теста).</summary>
    public async Task<int> DrainAsync(bool includeRetries = false)
    {
        var runner = fixture.Services.GetRequiredService<RecruitmentJobRunner>();
        var done = await runner.DrainAsync(default);
        if (includeRetries)
        {
            for (var i = 0; i < 4; i++)
            {
                using var scope = fixture.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
                await db.RecruitmentJobs.Where(j => j.Status == RecruitmentJobStatus.Pending).ExecuteUpdateAsync(u => u.SetProperty(j => j.RunAfter, DateTimeOffset.UtcNow.AddMinutes(-1)));
                done += await runner.DrainAsync(default);
            }
        }

        return done;
    }

    public static async Task<List<string>> SentAsync(OneBaseDbContext db, Chat chat) =>
        await db.TelegramOutgoingMessages.AsNoTracking().Where(m => m.PeerId == chat.Peer.PeerId).OrderBy(m => m.CreatedAt).Select(m => m.Text).ToListAsync();

    public static async Task<Candidate> CandidateAsync(OneBaseDbContext db, Chat chat) =>
        await db.Candidates.AsNoTracking().Include(c => c.Contacts).Include(c => c.Resumes)
            .SingleAsync(c => c.Contacts.Any(x => x.Kind == CandidateContactKind.Telegram && x.Value == chat.User.ToString()));

    public async Task<Guid> OpenVacancyAsync(IServiceProvider sp, Action<Vacancy>? configure = null)
    {
        var service = sp.GetRequiredService<RecruitmentService>();
        var vacancy = await service.CreateVacancyAsync(Hr, new VacancyInput("Водитель-экспедитор", Sales, "Развозка товара по магазинам", "Права категории C, опыт от 2 лет",
            "Пн–сб, 9:00–18:00", 6_000_000, 8_000_000, "Ташкент", "full", 1, null), default);
        await service.SetVacancyStatusAsync(Hr, vacancy.Id, VacancyStatus.Open, default);
        if (configure is not null)
        {
            var db = sp.GetRequiredService<OneBaseDbContext>();
            var row = await db.Vacancies.SingleAsync(v => v.Id == vacancy.Id);
            configure(row);
            await db.SaveChangesAsync();
        }

        return vacancy.Id;
    }

    public bool Stored(string key) => fixture.Services.GetRequiredService<IFileStorage>().ListKeysAsync().ToBlockingEnumerable().Contains(key);
}

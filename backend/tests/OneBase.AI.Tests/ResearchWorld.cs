using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OneBase.AI;
using OneBase.AI.Agents;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Визит мира: регион, точка, день, в маршруте ли, выполнен ли.</summary>
public sealed record WorldVisit(int Region, long Market, DateOnly Day, bool InPlan, bool Done);

/// <summary>
/// Визиты и акция поверх IntelligenceWorld (продажи 01.07–31.10.2026). Визиты — сентябрь и октябрь, пн–сб, все пять точек региона по маршруту;
/// по понедельникам точка 3 не посещена (pending — «не посещено»). В регионе 0 в октябре точка 5 перестала покупать (продаж нет), а визитов стало
/// больше: внеплановые визиты в точки 1 и 2 по средам и пятницам — визиты растут, выручка падает. Акция «ИИ акция Б» — товар Б с 01.10.
/// </summary>
public static class ResearchWorld
{
    public const long VisitBase = 9_300_000_000;
    public const long PromoId = 9_300_001;

    /// <summary>Товар из акции «ИИ акция Б3», которого никто не продавал.</summary>
    public const long UnsoldProduct = 9_200_099;
    public static readonly DateOnly VisitsFrom = new(2026, 9, 1), VisitsTo = new(2026, 10, 31);

    public static IReadOnlyList<WorldVisit> Visits()
    {
        var list = new List<WorldVisit>();
        for (var d = VisitsFrom; d <= VisitsTo; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            for (var r = 0; r < 3; r++)
            {
                for (var m = 1; m <= 5; m++)
                {
                    var missed = m == 3 && d.DayOfWeek == DayOfWeek.Monday;
                    list.Add(new WorldVisit(r, IntelligenceWorld.Market(r, m), d, true, !missed));
                }

                if (r == 0 && d >= IntelligenceWorld.Event && d.DayOfWeek is DayOfWeek.Wednesday or DayOfWeek.Friday)
                {
                    list.Add(new WorldVisit(r, IntelligenceWorld.Market(r, 1), d, false, true));
                    list.Add(new WorldVisit(r, IntelligenceWorld.Market(r, 2), d, false, true));
                }
            }
        }

        return list;
    }

    public static async Task SeedAsync(IServiceProvider services, IntelligenceWorld world)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        for (var r = 0; r < 3; r++)
        {
            for (var m = 1; m <= 5; m++)
            {
                db.LinkoMarkets.Add(new LinkoMarket
                {
                    Id = IntelligenceWorld.Market(r, m), Name = $"ИИ точка {r}-{m} {world.Suffix}", BranchId = IntelligenceWorld.BranchBase + r, BranchName = world.Regions[r],
                    MarketTypeName = "RETAIL",
                });
            }
        }

        var id = VisitBase;
        foreach (var v in Visits())
        {
            db.LinkoVisits.Add(new LinkoVisit
            {
                Id = id++, Date = v.Day.ToDateTime(new TimeOnly(10, 0)), Day = v.Day, Status = v.Done ? "done" : "pending", IsInPlan = v.InPlan, MarketId = v.Market,
                UserId = IntelligenceWorld.AgentBase + v.Region,
            });
        }

        db.LinkoPromotions.Add(new LinkoPromotion
        {
            Id = PromoId, Name = "ИИ акция Б", Type = "discount", PromotionType = "promotion_all", DiscountType = "percent", Discount = 5,
            BeginDate = IntelligenceWorld.Event, TillDate = IntelligenceWorld.End,
        });
        db.LinkoPromotionProducts.Add(new LinkoPromotionProduct { PromotionId = PromoId, ProductId = IntelligenceWorld.ProductB, Type = "product" });
        // Та же механика для другой группы клиентов: те же товары и даты — по продажам неотличима и проверяется вместе с первой.
        db.LinkoPromotions.Add(new LinkoPromotion
        {
            Id = PromoId + 1, Name = "ИИ акция Б2", Type = "discount", PromotionType = "promotion_branches", DiscountType = "percent", Discount = 3,
            BeginDate = IntelligenceWorld.Event, TillDate = IntelligenceWorld.End,
        });
        db.LinkoPromotionProducts.Add(new LinkoPromotionProduct { PromotionId = PromoId + 1, ProductId = IntelligenceWorld.ProductB, Type = "product" });
        // Тот же товар Б и ещё один, которого не продавали: по списку товаров — другая акция (общих товаров половина), по продажам — та же.
        db.LinkoPromotions.Add(new LinkoPromotion
        {
            Id = PromoId + 2, Name = "ИИ акция Б3", Type = "discount", PromotionType = "promotion_branches", DiscountType = "percent", Discount = 4,
            BeginDate = IntelligenceWorld.Event, TillDate = IntelligenceWorld.End,
        });
        db.LinkoPromotionProducts.Add(new LinkoPromotionProduct { PromotionId = PromoId + 2, ProductId = IntelligenceWorld.ProductB, Type = "product" });
        db.LinkoPromotionProducts.Add(new LinkoPromotionProduct { PromotionId = PromoId + 2, ProductId = UnsoldProduct, Type = "product" });
        await db.SaveChangesAsync();
    }

    // ---------- эталоны визитов (правила «Календаря визитов») ----------

    public static IEnumerable<WorldVisit> Between(DateOnly from, DateOnly to) => Visits().Where(v => v.Day >= from && v.Day <= to);

    public static int Done(int region, DateOnly from, DateOnly to) => Between(from, to).Count(v => v.Region == region && v.Done);

    public static int Planned(int region, DateOnly from, DateOnly to) => Between(from, to).Count(v => v.Region == region && v.InPlan);

    public static int DoneInPlan(int region, DateOnly from, DateOnly to) => Between(from, to).Count(v => v.Region == region && v.Done && v.InPlan);

    public static int Missed(int region, DateOnly from, DateOnly to) => Between(from, to).Count(v => v.Region == region && v.InPlan && !v.Done);

    /// <summary>Выполненные визиты, в день которых у ТП в этой точке есть заказ (в мире — строка продаж точки за день).</summary>
    public static int WithOrder(IntelligenceWorld world, int region, DateOnly from, DateOnly to)
    {
        var orders = world.Between(from, to).Where(l => l.Region == region).Select(l => (l.Market, l.Date)).ToHashSet();
        return Between(from, to).Count(v => v.Region == region && v.Done && orders.Contains((v.Market, v.Day)));
    }
}

/// <summary>Модель-сценарий: шаги видят сообщения и параметры вызова (назначение, модель) — для сквозных тестов цепочки чата.</summary>
public sealed class FlowModel : IAiGateway
{
    private readonly Queue<Func<IReadOnlyList<LlmMessage>, AiCallOptions, AiCallResult>> _steps = new();
    private readonly object _gate = new();

    public List<(IReadOnlyList<LlmMessage> Messages, AiCallOptions Options, IReadOnlyList<LlmToolDefinition> Tools)> Calls { get; } = [];

    public FlowModel Reset()
    {
        lock (_gate)
        {
            _steps.Clear();
            Calls.Clear();
        }

        return this;
    }

    public FlowModel Text(string text) => Step((_, _) => Result(text, []));

    public FlowModel Tools(params LlmToolCall[] calls) => Step((_, _) => Result(null, calls));

    public FlowModel Step(Func<IReadOnlyList<LlmMessage>, AiCallOptions, AiCallResult> step)
    {
        lock (_gate)
        {
            _steps.Enqueue(step);
        }

        return this;
    }

    public static AiCallResult Result(string? text, IReadOnlyList<LlmToolCall> calls) =>
        new(text, calls, new AiTokenUsage(100, 50), new AiModelRef("fake", "flow"), false, 1, calls.Count > 0 ? "tool_use" : "stop");

    public static LlmToolCall Call(string id, string name, object args) => new(id, name, JsonSerializer.SerializeToElement(args));

    public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

    public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
        CancellationToken cancellationToken = default)
    {
        Func<IReadOnlyList<LlmMessage>, AiCallOptions, AiCallResult> step;
        var copy = messages.ToList();
        lock (_gate)
        {
            Calls.Add((copy, options, tools));
            if (_steps.Count == 0)
            {
                throw new LlmNotConfiguredException("AI не настроен (тест): сценарий модели закончился.");
            }

            step = _steps.Dequeue();
        }

        return Task.FromResult(step(copy, options));
    }

    public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>
/// База для тестов Statistics Engine, исследований и сквозной цепочки чата: настоящие сервисы OneBase, мир продаж, визитов и акции, модель-сценарий,
/// пользователи — администратор (продажи, финансы, HR, Аналитик), пользователь одного региона (СВР) и без прав.
/// </summary>
public sealed class ResearchDb : IAsyncLifetime
{
    private string? _admin;
    private string? _database;

    public ServiceProvider Services { get; private set; } = null!;
    public FlowModel Model { get; } = new();
    public IntelligenceWorld World { get; private set; } = null!;
    public Guid AdminId { get; private set; }
    public Guid RegionUserId { get; private set; }
    public Guid NoAccessId { get; private set; }
    public Guid RegionId { get; private set; }

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable(DataManagerDb.Variable);
        if (string.IsNullOrWhiteSpace(_admin))
        {
            return;
        }

        _database = $"onebase_ai_research_{Guid.NewGuid():N}"[..32];
        await using (var connection = new NpgsqlConnection(_admin))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", connection).ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_admin) { Database = _database };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = builder.ConnectionString,
            ["LINKO_BASE_URL"] = "https://linko.test",
            ["LINKO_TOKEN"] = "test-token",
            ["Verifix:WorkerEnabled"] = "false",
            ["Analytik:WorkerEnabled"] = "false",
            ["Analytik:FreshWaitSeconds"] = "0",
            ["Ai:AnswerCache:Minutes"] = "0",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(config);
        services.AddAi(config);
        services.AddSingleton<IFileStorage, MemoryStorage>();
        services.AddSingleton<IAiGateway>(Model);
        services.AddScoped<ITool, BrokenTestTool>();
        Services = services.BuildServiceProvider();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await db.Database.MigrateAsync();
        }

        World = await IntelligenceWorld.SeedAsync(Services, () => Task.CompletedTask);
        await ResearchWorld.SeedAsync(Services, World);

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var admin = new Role { Name = "Админ исследований" };
            foreach (var code in new[] { Permissions.SalesRead, Permissions.FinanceRead, Permissions.HrRead, Permissions.AnalytikUse, Permissions.AnalytikManage })
            {
                admin.Permissions.Add(new RolePermission { Code = code });
            }

            var regional = new Role { Name = "СВР региона" };
            foreach (var code in new[] { Permissions.SalesRead, Permissions.AnalytikUse })
            {
                regional.Permissions.Add(new RolePermission { Code = code });
            }

            var adminUser = new User { Login = "research-admin", FullName = "Администратор исследований" };
            adminUser.Roles.Add(new UserRole { Role = admin });
            var regionUser = new User { Login = "research-svr", FullName = "СВР Самарканда" };
            regionUser.Roles.Add(new UserRole { Role = regional });
            var noAccess = new User { Login = "research-none", FullName = "Без доступа" };
            db.Users.AddRange(adminUser, regionUser, noAccess);
            await db.SaveChangesAsync();
            AdminId = adminUser.Id;
            RegionUserId = regionUser.Id;
            NoAccessId = noAccess.Id;
            RegionId = await db.SalesRegions.Where(r => r.Name == World.Regions[1]).Select(r => r.Id).FirstAsync();
            db.SalesUserScopes.Add(new SalesUserScope { UserId = regionUser.Id, Level = SalesScopeLevel.Supervisor, RegionIds = [RegionId] });
            await db.SaveChangesAsync();
        }

        await AiSeeder.SeedAsync(Services);
    }

    public async Task DisposeAsync()
    {
        if (_database is null || _admin is null)
        {
            return;
        }

        await Services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
    }
}

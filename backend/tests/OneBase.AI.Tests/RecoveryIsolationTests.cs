using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneBase.AI;
using OneBase.AI.Gateway;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Recovery;
using OneBase.Application.Security;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Logging;
using OneBase.Infrastructure.Recovery;
using OneBase.Infrastructure.Sales;

namespace OneBase.AI.Tests;

/// <summary>
/// Режим изоляции восстановленной копии (Recovery:Isolated): ни одна служба, выходящая наружу, не регистрируется; внешний HTTP-запрос не
/// уходит в сеть ни из одного клиента; Telegram не подключается; платные вызовы моделей заблокированы. Без базы и без сети.
/// </summary>
public class RecoveryIsolationTests
{
    /// <summary>Службы, которые работают и в изоляции: только внутри процесса и своей базы.</summary>
    /// <summary>Удаление данных кандидатов по сроку хранения (internal-класс Infrastructure) — только база и своё хранилище.</summary>
    private static readonly Type RetentionWorker =
        typeof(OneBase.Infrastructure.Telegram.TelegramAccountService).Assembly.GetType("OneBase.Infrastructure.Recruitment.RecruitmentRetentionWorker")!;

    private static readonly Type[] Internal = [typeof(SystemLogWriter), typeof(SalesCacheWarmer), RetentionWorker];

    internal static IConfiguration Config(bool isolated, params (string Key, string? Value)[] extra)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=1;Database=none",
            [RecoveryIsolation.ConfigKey] = isolated ? "true" : null,
        };
        foreach (var (key, value) in extra)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static (ServiceCollection Services, RecoveryIsolation Isolation) Build(bool isolated)
    {
        var config = Config(isolated);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddInfrastructure(config);
        services.AddAi(config);
        return (services, (RecoveryIsolation)services.Single(d => d.ServiceType == typeof(RecoveryIsolation)).ImplementationInstance!);
    }

    /// <summary>Все фоновые службы OneBase (классы IHostedService в Infrastructure и AI).</summary>
    private static List<Type> AllWorkers() =>
        typeof(OneBase.Infrastructure.DependencyInjection).Assembly.GetTypes()
            .Concat(typeof(OneBase.AI.DependencyInjection).Assembly.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IHostedService).IsAssignableFrom(t))
            .ToList();

    private static HashSet<Type> Registered(IServiceCollection services) =>
        services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType is not null).Select(d => d.ImplementationType!).ToHashSet();

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    // Опечатка — тоже изоляция: лишний раз выключить интеграции безопаснее, чем написать людям из копии.
    [InlineData("tru", true)]
    public void Setting_is_parsed_safely(string? value, bool isolated) => Assert.Equal(isolated, RecoveryIsolation.Parse(value));

    [Fact]
    public void Isolated_copy_starts_no_outbound_worker()
    {
        var outbound = AllWorkers().Except(Internal).ToList();
        Assert.True(outbound.Count >= 13, $"Найдено фоновых служб: {outbound.Count}");

        // Вне изоляции каждая служба зарегистрирована — тест видит их все (прогрев кэша продаж регистрируется фабрикой и в этот список не входит).
        var (normal, normalIsolation) = Build(isolated: false);
        Assert.False(normalIsolation.Isolated);
        Assert.Empty(normalIsolation.DisabledWorkers);
        var registered = Registered(normal);
        Assert.All(outbound, t => Assert.Contains(t, registered));

        // В изоляции — ни одной. Новая служба, добавленная без isolation.AllowOutbound, уронит этот тест: её нужно либо выключать в
        // изоляции, либо (если она не выходит за пределы процесса и базы) добавить в Internal.
        var (isolated, isolation) = Build(isolated: true);
        Assert.Empty(Registered(isolated).Intersect(outbound));
        Assert.Equal(outbound.Select(t => t.Name).Order(), isolation.DisabledWorkers.Select(w => w.Type).Order());
        Assert.All(isolation.DisabledWorkers, w => Assert.False(string.IsNullOrWhiteSpace(w.Label)));

        // Что на самом деле запустит хост: экземпляры IHostedService из контейнера (службы самого фреймворка — не наши, их не считаем).
        using var sp = isolated.BuildServiceProvider();
        var started = sp.GetServices<IHostedService>().Select(s => s.GetType()).Where(t => t.Assembly.GetName().Name!.StartsWith("OneBase.")).ToList();
        Assert.Equal(Internal.Select(t => t.Name).Order(), started.Select(t => t.Name).Order());
    }

    [Fact]
    public async Task Isolated_copy_sends_no_external_http_request()
    {
        // Вне изоляции запрос доходит до основного обработчика клиента (здесь — счётчик вместо сети): проверка ниже честная.
        var (normal, _) = Build(isolated: false);
        var reached = new CountingHandler();
        normal.AddHttpClient<OneBase.Infrastructure.Linko.LinkoClient>().ConfigurePrimaryHttpMessageHandler(() => reached);
        using (var sp = normal.BuildServiceProvider())
        using (var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OneBase.Infrastructure.Linko.LinkoClient)))
        {
            await client.GetAsync("https://linko.example/api/orders");
        }

        Assert.Equal(1, reached.Calls);

        var (isolated, _) = Build(isolated: true);
        var blocked = new CountingHandler();
        isolated.AddHttpClient<OneBase.Infrastructure.Linko.LinkoClient>().ConfigurePrimaryHttpMessageHandler(() => blocked);
        using var isolatedSp = isolated.BuildServiceProvider();
        var factory = isolatedSp.GetRequiredService<IHttpClientFactory>();
        // Linko, Verifix, Google, провайдеры моделей, Web Push и безымянный клиент (голос, проверка подключения папок).
        foreach (var name in new[] { "LinkoClient", "VerifixClient", "GoogleSheetsClient", "anthropic", "openai", "PushServiceClient", "" })
        {
            using var client = factory.CreateClient(name);
            var error = await Assert.ThrowsAsync<RecoveryIsolatedException>(() => client.GetAsync("https://external.example/path"));
            Assert.Equal("external.example", error.Host);
            Assert.Contains("Recovery:Isolated", error.Message);
        }

        Assert.Equal(0, blocked.Calls);
    }

    [Fact]
    public void Isolated_copy_blocks_paid_ai_calls_and_telegram()
    {
        Assert.False(AiSafetyOptions.From(Config(isolated: false)).BlockPaidCalls);

        // В изоляции — блокировка даже при заданном эмуляторе: копия наружу не ходит вовсе.
        var safety = AiSafetyOptions.From(Config(isolated: true, ("Ai:Safety:EmulatorUrl", "http://emulator.local:9000")));
        Assert.True(safety.BlockPaidCalls);
        Assert.False(safety.Emulator);

        // Telegram: та же проверка, что у подключения аккаунтов, автоотчётов и экрана настроек (класс внутренний — через отражение).
        var gateway = typeof(OneBase.Infrastructure.Linko.LinkoClient).Assembly.GetType("OneBase.Infrastructure.Telegram.WTelegramGateway", throwOnError: true)!;
        bool Enabled(IConfiguration config) => (bool)gateway.GetMethod("Enabled")!.Invoke(null, [config])!;
        Assert.True(Enabled(Config(isolated: false)));
        Assert.False(Enabled(Config(isolated: true)));
        Assert.False(Enabled(Config(isolated: true, ("Telegram:Enabled", "true"))));
    }

    [Fact]
    public void Mode_endpoint_reports_isolation_and_banner()
    {
        var off = Assert.IsType<OkObjectResult>(new RecoveryController(new RecoveryIsolation(false), Config(false)).Mode());
        Assert.Contains("Isolated = False", off.Value!.ToString());

        var on = Assert.IsType<OkObjectResult>(new RecoveryController(new RecoveryIsolation(true), Config(true)).Mode());
        Assert.Contains(RecoveryIsolation.Banner, on.Value!.ToString());
    }

    [Theory]
    // Токен не настроен — по заголовку не пускает.
    [InlineData(null, "any-token", false, typeof(UnauthorizedResult))]
    // Короткий токен в настройке — тоже не пускает: угадать его проще.
    [InlineData("short-token", "short-token", false, typeof(UnauthorizedResult))]
    [InlineData("0123456789abcdef0123456789abcdef", "0123456789abcdef0123456789abcdeX", false, typeof(UnauthorizedResult))]
    [InlineData("0123456789abcdef0123456789abcdef", null, false, typeof(UnauthorizedResult))]
    // Вошедший пользователь без права «Журнал ошибок» — 403.
    [InlineData(null, null, true, typeof(ForbidResult))]
    public async Task Check_endpoint_requires_token_or_admin(string? configured, string? header, bool signedIn, Type expected)
    {
        var controller = new RecoveryController(new RecoveryIsolation(true), Config(true, (RecoveryController.TokenKey, configured)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        if (header is not null)
        {
            controller.HttpContext.Request.Headers[RecoveryController.TokenHeader] = header;
        }

        if (signedIn)
        {
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(OneBaseClaims.Permission, Permissions.SalesRead)], "test"));
        }

        // До проверки дело не доходит — сервис проверки не нужен.
        var result = await controller.Check(null!, AiSafetyOptions.None, CancellationToken.None);
        Assert.IsType(expected, result);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") });
        }
    }
}

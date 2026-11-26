using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker;
using OneBase.Finance.Integration.Worker.Api;
using OneBase.Finance.Integration.Worker.Mock;
using OneBase.Finance.Integration.Worker.Normalization;
using OneBase.Finance.Integration.Worker.OneC;
using OneBase.Finance.Integration.Worker.Sync;

// Служба интеграции 1С:Бухгалтерия → OneBase (контейнер onec-worker). Читает 1С, пишет только во внутренний API OneBase.
// Настройки — переменные окружения: OneC__*, OneBase__*, Integration__* (см. docs/finance.md). Пароли и токены в журнал не пишутся.
if (args.Contains("health"))
{
    // Проверка контейнера (healthcheck): служба отвечает и на связи с OneBase. В образе aspnet нет curl — проверяет сама служба.
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    try
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(',')[0] ?? "8081";
        var body = await http.GetStringAsync($"http://localhost:{port}/health");
        return body.Contains("\"status\":\"ok\"", StringComparison.Ordinal) ? 0 : 1;
    }
    catch (Exception)
    {
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);

var oneC = builder.Configuration.GetSection(OneCOptions.Section).Get<OneCOptions>() ?? new OneCOptions();
var oneBase = builder.Configuration.GetSection(OneBaseApiOptions.Section).Get<OneBaseApiOptions>() ?? new OneBaseApiOptions();
var integration = builder.Configuration.GetSection(IntegrationOptions.Section).Get<IntegrationOptions>() ?? new IntegrationOptions();
if (string.IsNullOrWhiteSpace(integration.WorkerId))
{
    integration.WorkerId = Environment.MachineName;
}

builder.Services.AddSingleton(oneC);
builder.Services.AddSingleton(oneBase);
builder.Services.AddSingleton(integration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WorkerStatus>();
builder.Services.AddSingleton<OneCNormalizer>();
builder.Services.AddSingleton<RetryPolicy>();
builder.Services.AddSingleton<ConnectionDiagnostics>();
builder.Services.AddSingleton<SyncEngine>();

var mock = oneC.Provider.Equals("mock", StringComparison.OrdinalIgnoreCase);
if (mock && builder.Environment.IsProduction() && !oneC.AllowMockInProduction)
{
    // Демо-данные не должны попасть в рабочую базу: в production тестовый источник — только с явным разрешением.
    Console.Error.WriteLine("Служба 1С: тестовый источник (OneC__Provider=mock) в production выключен. Для стенда задайте DOTNET_ENVIRONMENT=Staging или OneC__AllowMockInProduction=true.");
    return 2;
}

if (mock)
{
    builder.Services.AddSingleton<IOneCDataProvider>(sp => new OneCMockDataProvider(oneC.Mock, sp.GetRequiredService<TimeProvider>()));
}
else
{
    // Подключение — из переменных OneC__* или со страницы «1С» в OneBase (меняется без перезапуска службы).
    builder.Services.AddSingleton<SwitchableOneCDataProvider>();
    builder.Services.AddSingleton<IOneCDataProvider>(sp => sp.GetRequiredService<SwitchableOneCDataProvider>());
}

builder.Services.AddHttpClient<IOneBaseIntegrationApi, OneBaseIntegrationApi>(http =>
{
    http.BaseAddress = new Uri(oneBase.InternalApiUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(Math.Clamp(oneBase.TimeoutSeconds, 10, 900));
    http.DefaultRequestHeaders.Add(IntegrationHeaders.Token, oneBase.IntegrationToken);
});
builder.Services.AddHostedService<SyncWorker>();

var app = builder.Build();
if (string.IsNullOrWhiteSpace(oneBase.IntegrationToken))
{
    app.Logger.LogError("Служба 1С: не задан ключ службы (OneBase__IntegrationToken). Создайте его в «Настройки → Интеграции → Финансы → 1С».");
}

// Проверка контейнера: служба жива и на связи с OneBase (без адресов, логинов и ключей).
app.MapGet("/health", (WorkerStatus s, IOneCDataProvider p) => Results.Json(new
{
    status = s.LastHeartbeatOk is { } ok && DateTimeOffset.UtcNow - ok < TimeSpan.FromMinutes(5) ? "ok" : "no_onebase",
    version = WorkerStatus.Version,
    provider = p.Name,
    enabled = oneC.Enabled,
    busy = s.Busy,
    oneC = s.Connection?.Ok,
    paused = s.Settings.Paused,
    lastHeartbeat = s.LastHeartbeatOk,
    nextSync = s.NextSyncAt,
    lastRun = s.LastOutcome is null ? null : new { s.LastOutcome.Status, s.LastOutcome.Message },
}));

app.Run();
return 0;

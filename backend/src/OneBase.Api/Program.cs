using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using OneBase.AI;
using OneBase.AI.Agents;
using OneBase.Api;
using OneBase.Api.Auth;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;

DotEnv.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAi(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<OneBaseDbContext>("postgres");

// За nginx (и сервером Next.js при входе) IP клиента приходит в X-Forwarded-For. Порт API наружу не публикуется —
// до него доходят только nginx и web из сети docker, поэтому доверяем им. Лимит входа считается по настоящему IP.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    // Вопросы консультанту и задачи AI-сотрудникам стоят денег у провайдера — не больше 20 в минуту на пользователя.
    o.AddPolicy("ai", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst(OneBaseClaims.UserId)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseRateLimiter(); // после аутентификации — лимит AI считается по пользователю
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await DbSeeder.SeedAsync(app.Services);
    await AiSeeder.SeedAsync(app.Services);
}

// Аварийный доступ администратора (deploy/reset-admin.sh): пароль — из stdin, не из аргументов.
if (Array.IndexOf(args, "reset-admin") is var resetAt and >= 0)
{
    return await AdminAccess.ResetAsync(app.Services, resetAt + 1 < args.Length ? args[resetAt + 1] : null, Console.In);
}

if (args.Contains("linko-check"))
{
    return await LinkoCheck.RunAsync(app.Services, full: args.Contains("--full"));
}

if (Array.IndexOf(args, "linko-audit") is var auditAt and >= 0)
{
    return await LinkoCheck.AuditAsync(app.Services, auditAt + 1 < args.Length ? args[auditAt + 1] : null);
}

if (Array.IndexOf(args, "linko-fields") is var at and >= 0 && at + 1 < args.Length)
{
    return await LinkoCheck.FieldsAsync(app.Services, args[at + 1]);
}

app.Run();
return 0;

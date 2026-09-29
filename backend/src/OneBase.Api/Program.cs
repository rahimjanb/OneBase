using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using OneBase.AI;
using OneBase.Api;
using OneBase.Api.Auth;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;

DotEnv.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAi();
builder.Services.AddJwtAuth(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<OneBaseDbContext>("postgres");

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await DbSeeder.SeedAsync(app.Services);
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

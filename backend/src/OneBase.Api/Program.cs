using System.Threading.RateLimiting;
using OneBase.AI;
using OneBase.Api.Auth;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAi();
builder.Services.AddJwtAuth(builder.Configuration);

builder.Services.AddControllers();
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

app.Run();

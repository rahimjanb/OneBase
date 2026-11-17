using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OneBase.Finance.Integration.Worker;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Tests;

/// <summary>
/// HTTP-адаптер 1С против настоящего HTTP-сервера на этом компьютере (подставной «API 1С» по спецификации): авторизация, параметры, пути,
/// ошибки 401/403/404/409/429/503 и неверный JSON — с понятными видами ошибок; настройки — только https, без логина в адресе.
/// </summary>
public sealed class HttpProviderTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private string _url = null!;
    private readonly ConcurrentQueue<(string Path, string Query, string? Auth)> _requests = new();
    public Func<HttpContext, Task>? Handler { get; set; }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        _app.Run(async ctx =>
        {
            _requests.Enqueue((ctx.Request.Path.Value!, ctx.Request.QueryString.Value ?? "", ctx.Request.Headers.Authorization.ToString()));
            if (Handler is not null)
            {
                await Handler(ctx);
            }
        });
        await _app.StartAsync();
        _url = _app.Services.GetService(typeof(IServer)) is IServer server ? server.Features.Get<IServerAddressesFeature>()!.Addresses.First() : throw new InvalidOperationException();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private OneCHttpDataProvider Provider(Action<OneCOptions>? configure = null, string environment = "Development")
    {
        var options = new OneCOptions { BaseUrl = _url + "/buh/hs/onebase/v1", Username = "OneBase_ReadOnly", Password = "секрет-пароль", TimeoutSeconds = 5 };
        configure?.Invoke(options);
        return new OneCHttpDataProvider(options, new Env(environment));
    }

    private static Task Json(HttpContext ctx, string json, int status = 200)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        return ctx.Response.WriteAsync(json, Encoding.UTF8);
    }

    [Fact]
    public async Task Sends_basic_auth_paging_and_period_and_reads_the_page()
    {
        Handler = ctx => Json(ctx, """{"apiVersion":"1.0","items":[{"id":"d1","type":"sale","number":"1","date":"2026-10-15T10:00:00+05:00","organizationId":"o1","posted":true,"currency":"UZS","amount":10,"amountAccounting":10}],"page":{"limit":500,"nextCursor":"abc","hasMore":true},"totals":{"count":7,"amount":70}}""");
        using var provider = Provider();
        var page = await provider.GetDocumentsAsync("o1", ["sale", "sale_return"], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), new(500, "c1", true), default);

        Assert.Equal(("d1", true, 7), (page.Items[0].Id, page.HasMore, page.Totals!.Count));
        Assert.True(_requests.TryDequeue(out var r));
        Assert.Equal("/buh/hs/onebase/v1/documents", r.Path);
        foreach (var part in new[] { "organizationId=o1", "from=2026-10-01", "to=2026-10-31", "type=sale%2Csale_return", "limit=500", "cursor=c1", "includeTotals=true" })
        {
            Assert.Contains(part, r.Query);
        }

        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("OneBase_ReadOnly:секрет-пароль")), r.Auth);
    }

    [Fact]
    public async Task Token_and_custom_paths_are_supported()
    {
        Handler = ctx => Json(ctx, """{"apiVersion":"1.1","infobaseId":"ib-1","infobaseName":"Рабочая"}""");
        using var provider = Provider(o =>
        {
            o.Token = "t0k3n";
            o.Paths["ping"] = "/status/";
        });
        var ping = await provider.TestConnectionAsync(default);
        Assert.Equal(("1.1", "ib-1"), (ping.ApiVersion, ping.InfobaseId));
        Assert.True(_requests.TryDequeue(out var r));
        Assert.Equal(("/buh/hs/onebase/v1/status", "Bearer t0k3n"), (r.Path, r.Auth));
    }

    [Theory]
    [InlineData(401, null, OneCErrorKind.Unauthorized)]
    [InlineData(403, null, OneCErrorKind.Forbidden)]
    [InlineData(404, null, OneCErrorKind.NotFound)]
    [InlineData(409, "cursor_expired", OneCErrorKind.CursorExpired)]
    [InlineData(429, "rate_limited", OneCErrorKind.RateLimited)]
    [InlineData(503, "unavailable", OneCErrorKind.Unavailable)]
    [InlineData(500, "internal_error", OneCErrorKind.Server)]
    [InlineData(400, "invalid_parameter", OneCErrorKind.BadRequest)]
    public async Task Http_errors_map_to_clear_kinds_without_secrets(int status, string? code, OneCErrorKind kind)
    {
        Handler = ctx =>
        {
            if (status is 429 or 503)
            {
                ctx.Response.Headers.RetryAfter = "17";
            }

            return Json(ctx, "{\"error\":{\"code\":\"" + code + "\",\"message\":\"Пояснение 1С\"}}", status);
        };
        using var provider = Provider();
        var e = await Assert.ThrowsAsync<OneCException>(() => provider.GetModifiedRecordsAsync("10", 500, default));
        Assert.Equal(kind, e.Kind);
        Assert.DoesNotContain("секрет-пароль", e.Message);
        Assert.Contains("1С /changes", e.Message);
        if (status is 429 or 503)
        {
            Assert.Equal(TimeSpan.FromSeconds(17), e.RetryAfter);
        }
    }

    [Fact]
    public async Task Invalid_json_and_unreachable_server_are_transient()
    {
        Handler = ctx => Json(ctx, "<html>не JSON</html>");
        using var provider = Provider();
        var invalid = await Assert.ThrowsAsync<OneCException>(() => provider.GetAccountsAsync(new(10), default));
        Assert.Equal(OneCErrorKind.InvalidResponse, invalid.Kind);
        Assert.True(invalid.Transient);

        using var down = Provider(o => o.BaseUrl = "http://127.0.0.1:1/buh");
        var network = await Assert.ThrowsAsync<OneCException>(() => down.TestConnectionAsync(default));
        Assert.Equal(OneCErrorKind.Network, network.Kind);
        Assert.Contains("соединение отклонено", network.Message);
    }

    [Theory]
    [InlineData("", "Адрес 1С не задан")]
    [InlineData("ftp://192.168.9.141/buh", "не адрес https")]
    [InlineData("https://user:pass@192.168.9.141/buh", "Логин и пароль нельзя писать в адресе")]
    [InlineData("http://192.168.9.141/buh", "только по HTTPS")]
    public void Configuration_requires_https_and_no_credentials_in_url(string url, string message)
    {
        var error = OneCHttpDataProvider.CheckConfiguration(new OneCOptions { BaseUrl = url, Username = "u", Password = "p" }, production: true, out _);
        Assert.NotNull(error);
        Assert.Contains(message, error);
    }

    [Fact]
    public void Plain_http_is_allowed_only_locally_or_explicitly_outside_production()
    {
        var local = new OneCOptions { BaseUrl = "http://127.0.0.1:8080/buh", Username = "u" };
        Assert.Null(OneCHttpDataProvider.CheckConfiguration(local, production: true, out _));
        var lan = new OneCOptions { BaseUrl = "http://192.168.9.141/buh", Username = "u", AllowInsecureHttp = true };
        Assert.Null(OneCHttpDataProvider.CheckConfiguration(lan, production: false, out _));
        Assert.NotNull(OneCHttpDataProvider.CheckConfiguration(lan, production: true, out _));
        Assert.Contains("учётные данные", OneCHttpDataProvider.CheckConfiguration(new OneCOptions { BaseUrl = "https://192.168.9.141/buh" }, true, out _));
        Assert.Equal("https://192.168.9.141/buh/hs/onebase/v1", OneCHttpDataProvider.Display("https://192.168.9.141:443/buh/hs/onebase/v1/?x=1"));
    }

    [Fact]
    public async Task Misconfigured_provider_reports_configuration_error_on_every_call()
    {
        using var provider = Provider(o => o.BaseUrl = "http://192.168.9.141/buh", "Production");
        var e = await Assert.ThrowsAsync<OneCException>(() => provider.TestConnectionAsync(default));
        Assert.Equal(OneCErrorKind.Configuration, e.Kind);
        Assert.True(e.Fatal);
    }

    [Fact]
    public async Task Diagnostics_explain_each_step()
    {
        Handler = ctx => Json(ctx, """{"apiVersion":"1.0","infobaseId":"ib-1","infobaseName":"Рабочая","user":"OneBase_ReadOnly","readOnly":true}""");
        var options = new OneCOptions { BaseUrl = _url + "/buh/hs/onebase/v1", Username = "u", Password = "p" };
        using var provider = new OneCHttpDataProvider(options, new Env("Development"));
        var report = await new ConnectionDiagnostics(provider, options, new Env("Development"), TimeProvider.System).RunAsync(default);
        Assert.True(report.Ok, report.Message);
        Assert.Equal(["config", "tcp", "tls", "auth", "api"], report.Steps.Select(s => s.Code));
        Assert.Equal(("ib-1", true), (report.InfobaseId, report.AuthOk));

        var closed = new OneCOptions { BaseUrl = "http://127.0.0.1:1/buh", Username = "u" };
        using var downProvider = new OneCHttpDataProvider(closed, new Env("Development"));
        var down = await new ConnectionDiagnostics(downProvider, closed, new Env("Development"), TimeProvider.System).RunAsync(default);
        Assert.False(down.Ok);
        Assert.Equal("tcp", down.Steps[^1].Code);
        Assert.Contains("отклонил соединение", down.Message);
        Assert.Null(down.AuthOk);
    }
}

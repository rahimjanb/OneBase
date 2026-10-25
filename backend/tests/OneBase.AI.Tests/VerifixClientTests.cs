using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OneBase.Infrastructure.Hr;

namespace OneBase.AI.Tests;

/// <summary>Журнал в память — чтобы проверить, что секреты и токены в него не попадают.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Lines)
        {
            Lines.Add($"{logLevel}: {formatter(state, exception)} {exception?.Message}");
        }
    }
}

/// <summary>
/// Клиент Verifix на подставном HTTP-сервере: OAuth2 client credentials, заголовки project_code / filial_id / cursor / limit, пагинация
/// по next_cursor, повтор токена после 401, ограниченные повторы 429 и 5xx, без повторов 403, секреты не попадают в журнал и сообщения.
/// </summary>
public class VerifixClientTests
{
    private readonly FakeVerifix _fake = new();
    private readonly ListLogger<VerifixClient> _log = new();
    private readonly VerifixClient _client;

    public VerifixClientTests()
    {
        var options = new VerifixOptions { MaxAttempts = 3, RetryDelaysSeconds = [0], MaxRetryAfterSeconds = 0 };
        _client = new VerifixClient(new HttpClient(_fake), options, new VerifixTokenCache(), _log);
    }

    private static VerifixConnection Connection(string secret = FakeVerifix.ClientSecret, long? filial = null) =>
        new(FakeVerifix.BaseUrl, FakeVerifix.ClientId, secret, filial, true, VerifixSecretSource.OneBase, VerifixSettingsStore.Hint(secret), new VerifixSettingsData());

    private async Task<List<JsonObject>> ReadEmployeesAsync(VerifixConnection connection, int limit = 500)
    {
        var all = new List<JsonObject>();
        await _client.ListAsync<JsonObject>(connection, "core/employee$list", new { employee_ids = Array.Empty<long>() }, limit, page =>
        {
            all.AddRange(page);
            return Task.CompletedTask;
        }, CancellationToken.None);
        return all;
    }

    [Fact]
    public async Task Pages_follow_next_cursor_with_documented_headers_and_one_token()
    {
        for (var i = 1; i <= 1200; i++)
        {
            _fake.Employees.Add(FakeVerifix.Employee(i, "Имя", "Фамилия", 901));
        }

        var rows = await ReadEmployeesAsync(Connection(filial: 161));

        Assert.Equal(1200, rows.Count);
        var calls = _fake.CallsTo("core/employee$list");
        Assert.Equal(3, calls.Count);
        Assert.False(calls[0].Headers.ContainsKey("cursor")); // первый запрос — без cursor
        Assert.Equal(new[] { "500", "1000" }, calls.Skip(1).Select(c => c.Headers["cursor"]));
        Assert.All(calls, c =>
        {
            Assert.Equal("vhr", c.Headers["project_code"]);
            Assert.Equal("161", c.Headers["filial_id"]);
            Assert.Equal("500", c.Headers["limit"]);
            Assert.Equal("Bearer token-1", c.Headers["authorization"]);
        });
        Assert.Equal(1, _fake.TokenRequests);
        Assert.Contains("\"client_secret\"", _fake.CallsTo("security/oauth/token")[0].Body);

        // Токен живёт expires_in — повторная выгрузка без нового токена.
        await ReadEmployeesAsync(Connection(filial: 161));
        Assert.Equal(1, _fake.TokenRequests);
    }

    [Fact]
    public async Task Last_page_with_a_resume_cursor_stops_on_the_empty_page()
    {
        _fake.TrailingCursor = true;
        for (var i = 1; i <= 10; i++)
        {
            _fake.Employees.Add(FakeVerifix.Employee(i, "Имя", "Фамилия", 901));
        }

        var rows = await ReadEmployeesAsync(Connection(), limit: 100);

        Assert.Equal(10, rows.Count);
        Assert.Equal(2, _fake.CallsTo("core/employee$list").Count); // страница с данными и пустая
    }

    [Fact]
    public async Task Expired_token_is_renewed_once_then_auth_error_is_reported()
    {
        _fake.Employees.Add(FakeVerifix.Employee(1, "Имя", "Фамилия", 901));
        _fake.RejectApiTokens = 1;
        Assert.Single(await ReadEmployeesAsync(Connection()));
        Assert.Equal(2, _fake.TokenRequests);

        _fake.RejectApiTokens = 10;
        var ex = await Assert.ThrowsAsync<VerifixApiException>(() => ReadEmployeesAsync(Connection()));
        Assert.Equal(VerifixErrorKind.Auth, ex.Kind);
        Assert.False(ex.Transient);
    }

    [Fact]
    public async Task Rate_limit_and_server_errors_are_retried_a_limited_number_of_times()
    {
        _fake.Employees.Add(FakeVerifix.Employee(1, "Имя", "Фамилия", 901));
        _fake.Fail("core/employee$list", HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway);
        Assert.Single(await ReadEmployeesAsync(Connection()));
        Assert.Equal(3, _fake.CallsTo("core/employee$list").Count);

        _fake.Calls.Clear();
        _fake.Fail("core/employee$list", HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError);
        var ex = await Assert.ThrowsAsync<VerifixApiException>(() => ReadEmployeesAsync(Connection()));
        Assert.Equal(VerifixErrorKind.Server, ex.Kind);
        Assert.True(ex.Transient);
        Assert.Equal(3, _fake.CallsTo("core/employee$list").Count); // MaxAttempts = 3, не бесконечно

        _fake.Calls.Clear();
        _fake.Fail("core/employee$list", HttpStatusCode.Forbidden);
        var forbidden = await Assert.ThrowsAsync<VerifixApiException>(() => ReadEmployeesAsync(Connection()));
        Assert.Equal(VerifixErrorKind.Forbidden, forbidden.Kind);
        Assert.Single(_fake.CallsTo("core/employee$list")); // 403 не повторяется
        Assert.Contains("Внешние системы", VerifixClient.Human(forbidden));
    }

    [Fact]
    public async Task Wrong_secret_is_an_auth_error_and_secrets_never_reach_the_log()
    {
        const string wrong = "wrong-secret-value-987654321";
        var ex = await Assert.ThrowsAsync<VerifixApiException>(() => ReadEmployeesAsync(Connection(wrong)));
        Assert.Equal(VerifixErrorKind.Auth, ex.Kind);
        Assert.DoesNotContain(wrong, ex.Message);

        _fake.Employees.Add(FakeVerifix.Employee(1, "Имя", "Фамилия", 901));
        _fake.Fail("core/employee$list", HttpStatusCode.ServiceUnavailable);
        await ReadEmployeesAsync(Connection());

        Assert.NotEmpty(_log.Lines);
        Assert.All(_log.Lines, line =>
        {
            Assert.DoesNotContain(FakeVerifix.ClientSecret, line);
            Assert.DoesNotContain(wrong, line);
            Assert.DoesNotContain("token-", line);
        });
        Assert.Equal("••••cdef", VerifixSettingsStore.Hint(FakeVerifix.ClientSecret));
    }

    [Fact]
    public async Task Connection_test_reports_each_method_without_pretending_success()
    {
        _fake.SeedReferences();
        _fake.Employees.Add(FakeVerifix.Employee(1, "Имя", "Фамилия", 901));
        _fake.Fail("core/timesheet$export", HttpStatusCode.Forbidden);

        var result = await _client.TestAsync(Connection(), new DateOnly(2026, 10, 21), CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("Тестовая организация", result.Organization);
        Assert.Contains(result.Checks, c => c.Method == "Табель посещений" && !c.Ok);
        Assert.Contains(result.Checks, c => c.Method == "Сотрудники" && c.Ok);
        Assert.Contains("часть методов", result.Message);

        var denied = await _client.TestAsync(Connection("bad-secret-000000000"), new DateOnly(2026, 10, 21), CancellationToken.None);
        Assert.False(denied.Ok);
        Assert.Contains("не принял", denied.Message);

        var empty = await _client.TestAsync(Connection("") with { ClientSecret = "" }, new DateOnly(2026, 10, 21), CancellationToken.None);
        Assert.False(empty.Ok);
    }
}

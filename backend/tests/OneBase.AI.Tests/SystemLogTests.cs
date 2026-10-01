using Microsoft.Extensions.Logging;
using OneBase.Domain.Audit;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Logging;

namespace OneBase.AI.Tests;

public class SystemLogTests
{
    [Theory]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig", "Bearer ***")]
    [InlineData("Host=postgres;Password=s3cr3t-value;Database=onebase", "Password=***")]
    [InlineData("ключ sk-proj-ABCDEFGHIJKLMNOP отклонён", "sk-***")]
    [InlineData("https://linko.example/api?token=abc123def&page=2", "token=***")]
    public void Secrets_are_cut_from_log_text(string text, string expected)
    {
        var scrubbed = LogScrubber.Scrub(text);

        Assert.Contains(expected, scrubbed);
        Assert.DoesNotContain("s3cr3t", scrubbed);
        Assert.DoesNotContain("abc123def", scrubbed);
        Assert.DoesNotContain("ABCDEFGHIJKLMNOP", scrubbed);
        Assert.DoesNotContain("payload.sig", scrubbed);
    }

    [Fact]
    public void Plain_text_stays_as_is()
    {
        const string text = "Linko orders: сервер не принял токен (HTTP 401)";
        Assert.Equal(text, LogScrubber.Scrub(text));
    }

    [Theory]
    [InlineData("OneBase.Infrastructure.Linko.LinkoClient", false, SystemLogSources.Linko)]
    [InlineData("OneBase.Api.Controllers.IntegrationsController", false, SystemLogSources.Linko)]
    [InlineData("OneBase.Infrastructure.Linko.LinkoSyncService", true, SystemLogSources.Linko)] // ошибка API Linko во время синхронизации
    [InlineData("OneBase.Infrastructure.Linko.LinkoSyncService", false, SystemLogSources.Sync)]
    [InlineData("OneBase.Infrastructure.Linko.LinkoSyncCoordinator", false, SystemLogSources.Sync)]
    [InlineData("OneBase.AI.Consultant.ConsultantChatService", false, SystemLogSources.System)]
    [InlineData("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", false, SystemLogSources.System)]
    public void Source_is_linko_sync_or_system(string category, bool linkoApiError, string source)
    {
        Assert.Equal(source, SystemLogRules.SourceOf(category, linkoApiError ? new LinkoApiException("HTTP 401") : null));
    }

    [Fact]
    public void Linko_and_sync_keep_warnings_system_keeps_only_errors()
    {
        Assert.True(SystemLogRules.ShouldWrite(SystemLogSources.Linko, LogLevel.Warning));
        Assert.True(SystemLogRules.ShouldWrite(SystemLogSources.Sync, LogLevel.Warning));
        Assert.False(SystemLogRules.ShouldWrite(SystemLogSources.System, LogLevel.Warning));
        Assert.True(SystemLogRules.ShouldWrite(SystemLogSources.System, LogLevel.Error));
        Assert.True(SystemLogRules.Excluded("Microsoft.EntityFrameworkCore.Update"));
        Assert.False(SystemLogRules.Excluded("OneBase.Infrastructure.Linko.LinkoSyncService"));
    }
}

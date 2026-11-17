using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.Finance.Integration.Worker;
using OneBase.Finance.Integration.Worker.Api;
using OneBase.Finance.Integration.Worker.OneC;
using OneBase.Finance.Integration.Worker.Sync;

namespace OneBase.Finance.Tests;

/// <summary>Повторы с нарастающей паузой и Retry-After; проверка сертификата 1С без «принять любой».</summary>
public class RetryAndTlsTests
{
    private static (RetryPolicy Retry, List<TimeSpan> Delays) Policy(int attempts = 5)
    {
        var delays = new List<TimeSpan>();
        var retry = new RetryPolicy(new IntegrationOptions { MaxAttempts = attempts, BaseDelaySeconds = 2, MaxDelaySeconds = 60 }, NullLogger<RetryPolicy>.Instance);
        retry.Delay = (span, _) =>
        {
            delays.Add(span);
            return Task.CompletedTask;
        };
        return (retry, delays);
    }

    [Fact]
    public async Task Transient_errors_are_retried_with_growing_pause()
    {
        var (retry, delays) = Policy();
        var calls = 0;
        var result = await retry.ExecuteAsync("тест", _ =>
        {
            if (++calls < 4)
            {
                throw new OneCException(OneCErrorKind.Network, "нет сети");
            }

            return Task.FromResult(42);
        }, default);

        Assert.Equal((42, 4), (result, calls));
        Assert.Equal(3, delays.Count);
        Assert.InRange(delays[0].TotalSeconds, 1.6, 2.4);
        Assert.InRange(delays[1].TotalSeconds, 3.2, 4.8);
        Assert.InRange(delays[2].TotalSeconds, 6.4, 9.6);
    }

    [Fact]
    public async Task Retry_after_from_the_server_is_respected_but_capped()
    {
        var (retry, delays) = Policy();
        var calls = 0;
        await retry.ExecuteAsync("тест", _ => ++calls switch
        {
            1 => throw new OneCException(OneCErrorKind.Unavailable, "503", TimeSpan.FromSeconds(30)),
            2 => throw new OneCException(OneCErrorKind.RateLimited, "429", TimeSpan.FromHours(1)),
            _ => Task.FromResult(true),
        }, default);

        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)], delays);
    }

    [Theory]
    [InlineData(OneCErrorKind.Unauthorized)]
    [InlineData(OneCErrorKind.Tls)]
    [InlineData(OneCErrorKind.BadRequest)]
    [InlineData(OneCErrorKind.CursorExpired)]
    public async Task Authorization_tls_and_bad_requests_are_not_retried(OneCErrorKind kind)
    {
        var (retry, delays) = Policy();
        var calls = 0;
        await Assert.ThrowsAsync<OneCException>(() => retry.ExecuteAsync<bool>("тест", _ =>
        {
            calls++;
            throw new OneCException(kind, "ошибка");
        }, default));
        Assert.Equal((1, 0), (calls, delays.Count));
    }

    [Fact]
    public async Task Gives_up_after_max_attempts_and_onebase_conflict_is_not_retried()
    {
        var (retry, _) = Policy(attempts: 3);
        var calls = 0;
        await Assert.ThrowsAsync<OneCException>(() => retry.ExecuteAsync<bool>("тест", _ =>
        {
            calls++;
            throw new OneCException(OneCErrorKind.Timeout, "тайм-аут");
        }, default));
        Assert.Equal(3, calls);

        calls = 0;
        await Assert.ThrowsAsync<OneBaseApiException>(() => retry.ExecuteAsync<bool>("тест", _ =>
        {
            calls++;
            throw new OneBaseApiException(System.Net.HttpStatusCode.Conflict, "запуск закрыт", transient: false);
        }, default));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Cancellation_stops_retries()
    {
        var (retry, _) = Policy();
        using var cts = new CancellationTokenSource();
        var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry.ExecuteAsync<bool>("тест", _ =>
        {
            calls++;
            cts.Cancel();
            throw new OneCException(OneCErrorKind.Network, "нет сети");
        }, cts.Token));
        Assert.Equal(1, calls);
    }

    private static X509Certificate2 SelfSigned(string host)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(host);
        request.CertificateExtensions.Add(san.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    [Fact]
    public void Self_signed_certificate_is_accepted_only_with_its_file_and_matching_name()
    {
        using var cert = SelfSigned("onec.local");
        var roots = new X509Certificate2Collection { X509CertificateLoader.LoadCertificate(cert.Export(X509ContentType.Cert)) };

        Assert.False(OneCTls.Validate(cert, SslPolicyErrors.RemoteCertificateChainErrors, null, out var noFile));
        Assert.Contains("OneC__CaCertificatePath", noFile);
        Assert.True(OneCTls.Validate(cert, SslPolicyErrors.RemoteCertificateChainErrors, roots, out _));
        Assert.False(OneCTls.Validate(cert, SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors, roots, out var name));
        Assert.Contains("имя", name);

        using var other = SelfSigned("onec.local");
        Assert.False(OneCTls.Validate(other, SslPolicyErrors.RemoteCertificateChainErrors, roots, out var foreign));
        Assert.StartsWith("сертификат 1С не прошёл проверку", foreign);
        Assert.False(OneCTls.Validate(null, SslPolicyErrors.RemoteCertificateNotAvailable, roots, out _));
    }

    [Fact]
    public void Ca_file_from_pem_is_loaded_and_missing_file_is_a_configuration_error()
    {
        using var cert = SelfSigned("onec.local");
        var path = Path.Combine(Path.GetTempPath(), $"onec-ca-{Guid.NewGuid():N}.crt");
        File.WriteAllText(path, cert.ExportCertificatePem());
        try
        {
            Assert.Single(OneCTls.LoadRoots(path)!);
            Assert.Null(OneCTls.LoadRoots(" "));
            Assert.Equal(OneCErrorKind.Configuration, Assert.Throws<OneCException>(() => OneCTls.LoadRoots(path + ".нет")).Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

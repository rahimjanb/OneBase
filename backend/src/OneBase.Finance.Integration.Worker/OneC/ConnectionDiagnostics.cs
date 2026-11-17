using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Finance.Integration.Worker.OneC;

/// <summary>
/// Проверка подключения к 1С по шагам — понятная диагностика вместо «не работает»: настройки → сеть (TCP до порта) → TLS и сертификат →
/// авторизация и ответ API (/ping). Ничего не меняет ни в сети, ни на сервере 1С. Пароль и токен в отчёт не попадают.
/// </summary>
public sealed class ConnectionDiagnostics(IOneCDataProvider provider, OneCOptions options, IHostEnvironment environment, TimeProvider clock)
{
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(10);

    public async Task<ConnectionReport> RunAsync(CancellationToken ct)
    {
        var steps = new List<DiagnosticStep>();
        var report = new ConnectionReport { CheckedAt = clock.GetUtcNow(), BaseUrl = provider.DisplayUrl };
        if (provider.Name == "mock")
        {
            steps.Add(new DiagnosticStep("source", true, "Тестовый источник (демо-данные 1С): сеть и сервер 1С не используются.", 0));
            return await PingAsync(report with { Https = true }, steps, ct);
        }

        var error = OneCHttpDataProvider.CheckConfiguration(options, environment.IsProduction(), out var uri);
        if (error is not null || uri is null)
        {
            steps.Add(new DiagnosticStep("config", false, error ?? "Адрес 1С не задан.", 0));
            return report with { Ok = false, Message = error, Steps = steps };
        }

        report = report with { Host = uri.Host, Port = uri.Port, Https = uri.Scheme == "https" };
        steps.Add(new DiagnosticStep("config", true, $"Адрес {provider.DisplayUrl}, вход — {(string.IsNullOrWhiteSpace(options.Token) ? "пользователь 1С" : "токен")}.", 0));

        // 1. Сеть: TCP до порта публикации.
        var watch = Stopwatch.StartNew();
        using var tcp = new TcpClient();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(NetworkTimeout);
            await tcp.ConnectAsync(uri.Host, uri.Port, timeout.Token);
            steps.Add(new DiagnosticStep("tcp", true, $"Сеть: порт {uri.Port} на {uri.Host} отвечает ({watch.ElapsedMilliseconds} мс).", (int)watch.ElapsedMilliseconds));
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            var message = e switch
            {
                OperationCanceledException => $"Сеть: нет ответа от {uri.Host}:{uri.Port} за {NetworkTimeout.TotalSeconds:N0} с (тайм-аут). Возможные причины: брандмауэр Windows на сервере 1С, правило сети ESXi, веб-сервер не запущен. Доступ должен быть открыт только с адреса OneBase.",
                SocketException { SocketErrorCode: SocketError.ConnectionRefused } => $"Сеть: {uri.Host} отклонил соединение на порт {uri.Port} — на этом порту нет веб-сервера (IIS/Apache) или он остановлен.",
                SocketException { SocketErrorCode: SocketError.HostNotFound } => $"Сеть: адрес {uri.Host} не найден (DNS).",
                SocketException s => $"Сеть: {uri.Host}:{uri.Port} недоступен ({s.SocketErrorCode}).",
                _ => $"Сеть: {uri.Host}:{uri.Port} недоступен ({e.GetType().Name}).",
            };
            steps.Add(new DiagnosticStep("tcp", false, message, (int)watch.ElapsedMilliseconds));
            return report with { Ok = false, Message = message, Steps = steps };
        }

        // 2. TLS: сертификат проверяется так же, как при обмене.
        if (uri.Scheme == "https")
        {
            watch.Restart();
            X509Certificate2Collection? roots;
            try
            {
                roots = OneCTls.LoadRoots(options.CaCertificatePath);
            }
            catch (OneCException e)
            {
                steps.Add(new DiagnosticStep("tls", false, e.Message, 0));
                return report with { Ok = false, TlsValid = false, Message = e.Message, Steps = steps };
            }

            string? problem = null;
            X509Certificate? seen = null;
            try
            {
                await using var ssl = new SslStream(tcp.GetStream(), false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(NetworkTimeout);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = uri.Host,
                    RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                    {
                        seen = certificate;
                        return OneCTls.Validate(certificate, errors, roots, out problem);
                    },
                }, timeout.Token);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                problem ??= e is OperationCanceledException ? $"нет ответа TLS за {NetworkTimeout.TotalSeconds:N0} с" : e.Message;
            }

            using var cert = seen is null ? null : X509CertificateLoader.LoadCertificate(seen.GetRawCertData());
            report = report with { TlsValid = problem is null, CertificateSubject = cert?.Subject, CertificateExpires = cert is null ? null : new DateTimeOffset(cert.NotAfter.ToUniversalTime()) };
            if (problem is not null)
            {
                var message = "TLS: " + problem;
                steps.Add(new DiagnosticStep("tls", false, message, (int)watch.ElapsedMilliseconds));
                return report with { Ok = false, Message = message, Steps = steps };
            }

            steps.Add(new DiagnosticStep("tls", true, $"TLS: сертификат принят ({cert?.Subject}, действует до {cert?.NotAfter:dd.MM.yyyy}).", (int)watch.ElapsedMilliseconds));
        }
        else
        {
            steps.Add(new DiagnosticStep("tls", true, "TLS: не используется (http — только для проверки на этом компьютере).", 0));
        }

        return await PingAsync(report, steps, ct);
    }

    /// <summary>3. Авторизация и ответ API.</summary>
    private async Task<ConnectionReport> PingAsync(ConnectionReport report, List<DiagnosticStep> steps, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var ping = await provider.TestConnectionAsync(ct);
            var ms = (int)watch.ElapsedMilliseconds;
            if (string.IsNullOrWhiteSpace(ping.InfobaseId))
            {
                steps.Add(new DiagnosticStep("api", false, "API 1С ответил без идентификатора базы (infobaseId) — без него OneBase не может защитить данные от смешивания баз.", ms));
                return report with { Ok = false, AuthOk = true, ApiVersion = ping.ApiVersion, Message = steps[^1].Message, Steps = steps };
            }

            steps.Add(new DiagnosticStep("auth", true, $"Авторизация принята{(ping.User is null ? "" : $" (пользователь {ping.User})")}{(ping.ReadOnly == false ? " — внимание: пользователь не только для чтения" : "")}.", ms));
            steps.Add(new DiagnosticStep("api", true, $"API {ping.ApiVersion ?? "?"}: база «{ping.InfobaseName ?? ping.InfobaseId}», {ping.Configuration?.Name} {ping.Configuration?.Version}".TrimEnd(' ', ',') + ".", ms));
            return report with
            {
                Ok = true, AuthOk = true, InfobaseId = ping.InfobaseId, InfobaseName = ping.InfobaseName, ApiVersion = ping.ApiVersion,
                Configuration = ping.Configuration is null ? null : $"{ping.Configuration.Name} {ping.Configuration.Version}".Trim(),
                Message = provider.Name == "mock" ? "Подключено к тестовому источнику." : "Подключено к 1С.", Steps = steps,
            };
        }
        catch (OneCException e)
        {
            var ms = (int)watch.ElapsedMilliseconds;
            var auth = e.Kind is OneCErrorKind.Unauthorized ? false : e.Kind is OneCErrorKind.Forbidden ? true : (bool?)null;
            steps.Add(new DiagnosticStep(auth == false ? "auth" : "api", false, e.Message, ms));
            return report with { Ok = false, AuthOk = auth, TlsValid = e.Kind == OneCErrorKind.Tls ? false : report.TlsValid, Message = e.Message, Steps = steps };
        }
    }
}

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace OneBase.Finance.Integration.Worker.OneC;

/// <summary>
/// Проверка сертификата 1С. Никогда не «принять любой»: либо сертификат проходит обычную проверку системы, либо цепочка строится до явно
/// указанного корня (файл внутреннего центра сертификации или самоподписанный сертификат публикации). Имя сервера проверяется всегда.
/// </summary>
public static class OneCTls
{
    public static X509Certificate2Collection? LoadRoots(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            throw new OneCException(OneCErrorKind.Configuration, $"Файл сертификата 1С не найден: {path}");
        }

        var roots = new X509Certificate2Collection();
        try
        {
            roots.ImportFromPemFile(path);
        }
        catch (Exception)
        {
            roots.Add(X509CertificateLoader.LoadCertificateFromFile(path));
        }

        if (roots.Count == 0)
        {
            throw new OneCException(OneCErrorKind.Configuration, $"В файле {path} нет сертификата (нужен PEM или DER).");
        }

        return roots;
    }

    /// <summary>true — сертификат принят; problem — почему нет (по-русски, для страницы настроек).</summary>
    public static bool Validate(X509Certificate? certificate, SslPolicyErrors errors, X509Certificate2Collection? roots, out string? problem)
    {
        if (certificate is null || errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
            problem = "сервер 1С не предъявил сертификат";
            return false;
        }

        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            problem = "имя в сертификате не совпадает с адресом сервера 1С";
            return false;
        }

        if (errors == SslPolicyErrors.None)
        {
            problem = null;
            return true;
        }

        if (roots is null)
        {
            problem = "сертификат 1С выдан неизвестным центром сертификации — укажите его файл в OneC__CaCertificatePath";
            return false;
        }

        using var cert = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(roots);
        // Внутренний центр сертификации обычно без списка отзыва — отзыв не проверяется, остальное (срок, подпись, цепочка) — да.
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (chain.Build(cert))
        {
            problem = null;
            return true;
        }

        problem = "сертификат 1С не прошёл проверку: " + string.Join("; ", chain.ChainStatus.Select(s => s.StatusInformation.Trim()).Where(s => s.Length > 0).Distinct());
        return false;
    }

    public static SocketsHttpHandler CreateHandler(X509Certificate2Collection? roots, Action<X509Certificate?, string?>? observe = null) => new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(15),
        SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
            {
                var ok = Validate(certificate, errors, roots, out var problem);
                observe?.Invoke(certificate, problem);
                return ok;
            },
        },
    };
}

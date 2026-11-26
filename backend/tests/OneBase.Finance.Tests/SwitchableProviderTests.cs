using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Tests;

/// <summary>
/// Подключение к 1С со страницы «1С»: служба применяет его без перезапуска, если у неё нет своих переменных OneC__BaseUrl (они главнее);
/// адрес, вход и сертификат — со страницы, тайм-аут и пути методов — от службы; диагностика сообщает, откуда подключение.
/// </summary>
public sealed class SwitchableProviderTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task Connection_from_the_page_is_applied_without_restart_unless_the_service_has_its_own()
    {
        var env = new OneCOptions { TimeoutSeconds = 7, Paths = new() { ["ping"] = "status" } };
        using var provider = new SwitchableOneCDataProvider(env, new Env("Production"));
        Assert.Equal((SwitchableOneCDataProvider.NotConfigured, false), (provider.Source, provider.HasOwnConnection));
        var refused = await Assert.ThrowsAsync<OneCException>(() => provider.TestConnectionAsync(default));
        Assert.Equal(OneCErrorKind.Configuration, refused.Kind);

        Assert.True(provider.Apply(new WorkerConnection { BaseUrl = "https://192.168.9.141/buh/hs/onebase/v1", Username = "OneBase_ReadOnly", Password = "p", ApiSecret = "s", Version = "v1" }));
        Assert.Equal((SwitchableOneCDataProvider.FromOneBase, "v1", "https://192.168.9.141/buh/hs/onebase/v1"), (provider.Source, provider.Version, provider.DisplayUrl));
        Assert.Equal((7, "status", "s", "p"), (provider.Options.TimeoutSeconds, provider.Options.Paths["ping"], provider.Options.ApiSecret, provider.Options.Password));
        Assert.Equal("пользователь 1С + API Secret", ConnectionDiagnostics.Login(provider.Options));

        // Адрес убрали на странице — подключения нет.
        Assert.True(provider.Apply(new WorkerConnection { Version = null }));
        Assert.Equal(SwitchableOneCDataProvider.NotConfigured, provider.Source);

        // У службы свои переменные — страница «1С» их не меняет.
        using var own = new SwitchableOneCDataProvider(new OneCOptions { BaseUrl = "https://10.0.0.5/buh/hs/onebase/v1", Username = "u", Password = "p" }, new Env("Production"));
        Assert.False(own.Apply(new WorkerConnection { BaseUrl = "https://evil.example/hs", Username = "x", Version = "v2" }));
        Assert.Equal((SwitchableOneCDataProvider.FromEnvironment, "https://10.0.0.5/buh/hs/onebase/v1"), (own.Source, own.DisplayUrl));
    }

    [Fact]
    public void Certificate_from_the_page_is_a_trusted_root_and_garbage_is_a_configuration_error()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=1c.venkon.local", rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var roots = OneCTls.LoadRoots(null, cert.ExportCertificatePem());
        Assert.Single(roots!);
        Assert.Equal(OneCErrorKind.Configuration, Assert.Throws<OneCException>(() => OneCTls.LoadRoots(null, "-----BEGIN CERTIFICATE-----\nmusor\n-----END CERTIFICATE-----")).Kind);
        Assert.Null(OneCTls.LoadRoots(null, null));
    }

    [Fact]
    public void Modules_map_document_types_and_empty_means_all()
    {
        Assert.Equal(OneCModules.All, OneCModules.Normalize(null));
        Assert.Equal(OneCModules.All, OneCModules.Normalize(["нет такого"]));
        Assert.Equal([OneCModules.Sales, OneCModules.Money], OneCModules.Normalize(["money", " SALES "]));
        Assert.Equal(OneCModules.Sales, OneCModules.Of(OneCDocumentTypes.OrderCustomer));
        Assert.Equal(OneCModules.Purchases, OneCModules.Of(OneCDocumentTypes.OrderSupplier));
        Assert.Equal(OneCModules.Other, OneCModules.Of("неизвестный"));
        Assert.True(OneCModules.Allows([], OneCDocumentTypes.Production));
        Assert.False(OneCModules.Allows([OneCModules.Sales], OneCDocumentTypes.Production));
        Assert.All(OneCDocumentTypes.All, t => Assert.Contains(OneCModules.All, m => OneCModules.TypesOf(m).Contains(t))); // у каждого типа есть модуль
    }
}

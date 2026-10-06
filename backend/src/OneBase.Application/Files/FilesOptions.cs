namespace OneBase.Application.Files;

/// <summary>Настройки «Файлов отделов» (секция Files).</summary>
public sealed class FilesOptions
{
    public const string Section = "Files";

    /// <summary>
    /// Публичный домен подключения к Windows (переменная FILE_STORAGE_DOMAIN → Files:PublicDomain): «files.1base.uz». Пусто — подключение
    /// не настроено: адреса не показываются, проверка подключения это и сообщает.
    /// </summary>
    public string PublicDomain { get; set; } = "";

    /// <summary>Схема адреса. Windows отправляет пароль только по https — другое значение годится лишь для проверки на сервере.</summary>
    public string PublicScheme { get; set; } = "https";

    /// <summary>Наибольший файл через сайт, байт: как client_max_body_size основного сайта в nginx (200 МБ).</summary>
    public long MaxUploadBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>Наибольший файл через подключение Windows, байт (сама Windows по умолчанию пускает до 50 МБ, Cloudflare — до 100 МБ).</summary>
    public long MaxDavFileBytes { get; set; } = 4L * 1024 * 1024 * 1024;

    /// <summary>Корень WebDAV в адресе: «https://files.1base.uz»; null — домен не задан.</summary>
    public string? PublicRoot() =>
        string.IsNullOrWhiteSpace(PublicDomain) ? null : $"{PublicScheme.Trim()}://{PublicDomain.Trim().Trim('/')}";

    /// <summary>Адрес папки отдела для «Подключить сетевой диск»: «https://files.1base.uz/production».</summary>
    public string? FolderUrl(string departmentCode) => PublicRoot() is { } root ? $"{root}/{departmentCode}" : null;

    /// <summary>Тот же адрес в виде пути Windows: «\\files.1base.uz@SSL\production».</summary>
    public string? UncPath(string departmentCode)
    {
        if (string.IsNullOrWhiteSpace(PublicDomain))
        {
            return null;
        }

        // Порт в пути Windows пишется через «@»: \\host@SSL@8443\production.
        var parts = PublicDomain.Trim().Trim('/').Split(':', 2);
        var ssl = PublicScheme.Trim().Equals("https", StringComparison.OrdinalIgnoreCase) ? "@SSL" : "";
        var port = parts.Length == 2 ? "@" + parts[1] : "";
        return $@"\\{parts[0]}{ssl}{port}\{departmentCode}";
    }
}

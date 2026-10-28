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

    /// <summary>
    /// Сколько дней действует адрес без пароля (ссылка отдела и личная ссылка) от выпуска. Ссылка — как пароль: если она утекла, срок
    /// ограничивает ущерб; продление — «Новая ссылка» (старая перестаёт работать сразу).
    /// </summary>
    public int LinkLifetimeDays { get; set; } = 365;

    public DateTimeOffset LinkExpiry(DateTimeOffset from) => from.AddDays(Math.Clamp(LinkLifetimeDays, 1, 3650));

    /// <summary>Корень WebDAV в адресе: «https://files.1base.uz»; null — домен не задан.</summary>
    public string? PublicRoot() =>
        string.IsNullOrWhiteSpace(PublicDomain) ? null : $"{PublicScheme.Trim()}://{PublicDomain.Trim().Trim('/')}";

    /// <summary>
    /// Адрес без пароля — секретная ссылка отдела: «https://files.1base.uz/k/{код}/production». Word и Excel (Microsoft 365) блокируют
    /// вход по логину и паролю (Basic) на любой сервер, пока это не разрешено на компьютере; по ссылке пароль не спрашивается вовсе.
    /// </summary>
    /// <remarks>folder — код отдела, имя общей папки («Общие файлы») или пусто: личная ссылка открывает всё доступное сразу.</remarks>
    public string? LinkUrl(string token, string folder) =>
        PublicRoot() is { } root ? $"{root}/k/{token}" + (folder.Length > 0 ? "/" + Uri.EscapeDataString(folder) : "") : null;

    /// <summary>Тот же адрес путём Windows: «\\files.1base.uz@SSL\k\{код}\production».</summary>
    public string? LinkUncPath(string token, string folder) => UncPath(folder.Length > 0 ? $@"k\{token}\{folder}" : $@"k\{token}");

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

namespace OneBase.Application.Files;

/// <summary>
/// Имена файлов и папок «Файлов отделов» — по правилам Windows: имя приходит и из браузера, и из Проводника (WebDAV), а храниться
/// должно так, чтобы его можно было открыть в обоих местах. Пути в хранилище имя не задаёт (ключ объекта MinIO — id), поэтому
/// «../» не может выйти за пределы отдела; имя с «/», «\» или «..» всё равно отклоняется.
/// </summary>
public static class FileNames
{
    public const int MaxLength = 255;

    private static readonly char[] Invalid = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly HashSet<string> Reserved = new(
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).Select(i => $"COM{i}"), .. Enumerable.Range(1, 9).Select(i => $"LPT{i}")],
        StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["webp"] = "image/webp",
        ["gif"] = "image/gif",
        ["csv"] = "text/csv",
        ["txt"] = "text/plain",
        ["json"] = "application/json",
        ["xml"] = "application/xml",
        ["zip"] = "application/zip",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["xls"] = "application/vnd.ms-excel",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["doc"] = "application/msword",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["mp4"] = "video/mp4",
    };

    /// <summary>Имя без пробелов по краям — так его сохраняет и Windows.</summary>
    public static string Normalize(string? name) => (name ?? string.Empty).Trim();

    /// <summary>Почему имя нельзя сохранить, или null — можно. Имя проверяется уже нормализованным (<see cref="Normalize"/>).</summary>
    public static string? Error(string? name)
    {
        var value = Normalize(name);
        if (value.Length == 0)
        {
            return "Укажите название.";
        }

        if (value.Length > MaxLength)
        {
            return $"Название — не длиннее {MaxLength} символов.";
        }

        if (value is "." or ".." || value.EndsWith('.'))
        {
            return "Название не может быть «.», «..» или заканчиваться точкой.";
        }

        if (value.IndexOfAny(Invalid) >= 0 || value.Any(char.IsControl))
        {
            return "В названии нельзя использовать символы / \\ : * ? \" < > |.";
        }

        var stem = value.Split('.')[0].TrimEnd();
        return Reserved.Contains(stem) ? $"«{stem}» — служебное имя Windows, выберите другое." : null;
    }

    /// <summary>Расширение без точки в нижнем регистре; у имени без точки — пусто.</summary>
    public static string Extension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot <= 0 || dot == name.Length - 1 ? string.Empty : name[(dot + 1)..].ToLowerInvariant();
    }

    public static string ContentType(string name) =>
        ContentTypes.TryGetValue(Extension(name), out var type) ? type : "application/octet-stream";

    /// <summary>
    /// Служебные файлы Windows и Office (временные «~$отчёт.xlsx», «desktop.ini», «Thumbs.db»): через подключение они нужны Проводнику
    /// и Office, в списках сайта не показываются.
    /// </summary>
    public static bool IsHidden(string name) =>
        name.StartsWith("~$", StringComparison.Ordinal)
        || name.StartsWith(".~lock.", StringComparison.Ordinal)
        || (name.StartsWith('~') && name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
        || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
        || name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase)
        || name.Equals(".DS_Store", StringComparison.Ordinal);

    /// <summary>Картинки и PDF можно открыть прямо в браузере; остальное отдаётся только на скачивание.</summary>
    public static bool IsInlineSafe(string name) => Extension(name) is "png" or "jpg" or "jpeg" or "webp" or "gif" or "pdf";
}

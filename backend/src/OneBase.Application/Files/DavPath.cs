namespace OneBase.Application.Files;

/// <summary>
/// Пути WebDAV: «/production/Отчёты/2026/отчёт.xlsx» → отдел и имена по порядку. Сегменты «.» и «..» отклоняются (обход каталогов),
/// путь ищется по именам в базе, а не в файловой системе. publicBase — корень WebDAV в адресе клиента: «» за nginx (files.1base.uz/…),
/// «/dav» при обращении к API напрямую.
/// </summary>
public static class DavPath
{
    /// <summary>Сегменты уже раскодированного пути (HttpRequest.Path); null — путь с «.» или «..».</summary>
    public static IReadOnlyList<string>? Segments(string? path)
    {
        var segments = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(s => s is "." or ".." || s.Contains('\\')) ? null : segments;
    }

    /// <summary>
    /// Путь назначения MOVE/COPY из заголовка Destination (полный или относительный адрес, процент-кодированный) в сегменты от корня
    /// WebDAV; null — адрес не разобрать, он вне корня или содержит «..».
    /// </summary>
    public static IReadOnlyList<string>? FromDestination(string? destination, string publicBase)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return null;
        }

        // Путь берётся из исходной строки, а не из Uri.AbsolutePath: Uri сам схлопывает «%2E%2E», и «..» молча исчезли бы
        // вместо отказа.
        string path;
        if (Uri.TryCreate(destination, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            var start = destination.IndexOf('/', destination.IndexOf("://", StringComparison.Ordinal) + 3);
            path = start < 0 ? "/" : destination[start..];
        }
        else if (destination.StartsWith('/'))
        {
            path = destination;
        }
        else
        {
            return null;
        }

        path = path.Split('?', '#')[0];

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            return null;
        }

        var root = publicBase.TrimEnd('/');
        if (root.Length > 0)
        {
            if (!decoded.Equals(root, StringComparison.OrdinalIgnoreCase) && !decoded.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            decoded = decoded[root.Length..];
        }

        return Segments(decoded);
    }

    /// <summary>Адрес ресурса в ответе PROPFIND: каждый сегмент процент-кодирован, у папки — «/» в конце.</summary>
    public static string Href(string publicBase, IEnumerable<string> segments, bool collection)
    {
        var path = string.Join('/', segments.Select(Uri.EscapeDataString));
        var href = $"{publicBase.TrimEnd('/')}/{path}";
        return collection && !href.EndsWith('/') ? href + "/" : href;
    }
}

using System.Text;
using System.Text.RegularExpressions;

namespace OneBase.Application.Abstractions;

/// <summary>
/// Узбекский: латиница → кириллица по правилам орфографии 1995 года (то же правило — во frontend: src/i18n/cyrillic.ts; меняются вместе).
/// oʻ → ў, gʻ → ғ, sh → ш, ch → ч, ya / yo / yu / ye → я / ё / ю / е, e в начале слова и после гласной → э, тутуқ белгиси (ʼ) → ъ,
/// h → ҳ, q → қ, x → х. Русские заимствования — по корням («aksiya» → «акция», «sentabr» → «сентябр», «-tsiya» → «-ция»).
/// Латиницей остаются: подстановки {0} и {name}, ссылки, почта, @имена, идентификаторы (sales.read), бренды и технические сокращения (OneBase,
/// Linko, PDF, KPI), слова в верблюжьем регистре, английские слова (в узбекской латинице нет «c» без «h» и «w»), слова рядом с цифрами,
/// «P&amp;L», «Ctrl+C», «A–Z». Узбекские сокращения (TP, AKB, SVR, QQS, JShShIR) — кириллицей; окончание после латинского сокращения —
/// кириллицей («PDFni» → «PDFни»). Кириллица и цифры проходят как есть.
/// </summary>
public static partial class UzbekCyrillic
{
    private static readonly HashSet<string> Keep = new(StringComparer.OrdinalIgnoreCase)
    {
        "onebase", "linko", "telegram", "excel", "word", "pdf", "png", "jpg", "jpeg", "csv", "xlsx", "docx", "kpi", "dso", "sku", "id", "api", "url",
        "ai", "gpt", "openai", "claude", "anthropic", "google", "gmail", "verifix", "windows", "android", "ios", "iphone", "bitrix", "bitrix24", "qr",
        "sms", "pin", "ip", "http", "https", "pwa", "json", "sql", "markdown", "twizos", "email", "vpn", "usb", "docker", "chrome", "safari",
        "edge", "yandex", "cloudflare", "webdav", "smb", "nas", "mp3", "mp4", "zip", "html", "ssl", "dns", "utc", "gmt", "wi", "fi", "mb", "kb", "gb",
        "fcm", "apns", "macos", "linux", "nginx", "minio", "postgres", "postgresql", "sheets", "drive", "outlook", "whatsapp", "instagram",
        "facebook", "youtube", "skype", "zoom", "teams", "slack", "notion", "jira", "trello", "asana", "github", "gitlab", "hr", "crm", "erp", "sfa",
        "b2b", "b2c", "ceo", "cfo", "okr", "faq", "mvp", "ux", "ui", "abc", "xyz", "ebit", "ebitda", "roi", "enter", "esc", "tab", "shift", "ctrl",
        "alt", "cmd", "true", "false", "null", "env", "localhost", "oauth", "tools", "about", "reasoning", "sync", "month", "months", "bought", "year",
        "dealer", "assignee", "team", "reassign", "employee", "early", "read", "you", "appear", "data", "manager", "docs", "shoubox", "gps", "usd",
        "search", "oos", "spreadsheet", "spreadsheets", "yyyy", "system", "hklm", "vapid", "iana", "fifo", "peer", "flood", "export",
    };

    /// <summary>Узбекские сокращения со строчными буквами внутри (диграфы) — целиком.</summary>
    private static readonly (string Latin, string Cyrillic)[] Abbreviations = [("JShShIR", "ЖШШИР"), ("MChJ", "МЧЖ"), ("YaTT", "ЯТТ")];

    /// <summary>
    /// Узбекские сокращения ЗАГЛАВНЫМИ (2–3 буквы) — кириллицей; другие короткие (PDF, SKU, DOC) — латиницей. Слова заглавными от 4 букв
    /// («HAFTA», «ARBUZ») — кириллицей, кроме английских и технических (Keep).
    /// </summary>
    private static readonly HashSet<string> UzbekCaps =
    [
        "TP", "RM", "AKB", "ROP", "SVR", "SV", "SN", "KPU", "DMP", "TT", "TOP", "QQS", "STIR", "XATO", "SINOV", "JO'NATMASI", "DIQQAT", "OGOHLANTIRISH", "OK",
    ];

    /// <summary>Русские заимствования: латинский корень → кириллический (дальше — по правилам). Длинные — первыми.</summary>
    private static readonly (string Latin, string Cyrillic)[] Stems =
    [
        ("kompaniya", "компания"), ("sentabr", "сентябр"), ("oktabr", "октябр"), ("noyabr", "ноябр"), ("dekabr", "декабр"), ("yanvar", "январ"),
        ("fevral", "феврал"), ("aksiya", "акция"), ("reestr", "реестр"), ("iyun", "июн"), ("iyul", "июл"), ("iyn", "июн"), ("iyl", "июл"),
    ];

    /// <summary>Сочетания внутри слова: «-ksiya» → «-кция», «-tsiya» → «-ция».</summary>
    private static readonly (string Latin, string Cyrillic)[] Infixes = [("tsiya", "ция"), ("ksiya", "кция")];

    private static readonly Dictionary<char, string> Letters = new()
    {
        ['a'] = "а", ['b'] = "б", ['c'] = "с", ['d'] = "д", ['e'] = "е", ['f'] = "ф", ['g'] = "г", ['h'] = "ҳ", ['i'] = "и", ['j'] = "ж", ['k'] = "к",
        ['l'] = "л", ['m'] = "м", ['n'] = "н", ['o'] = "о", ['p'] = "п", ['q'] = "қ", ['r'] = "р", ['s'] = "с", ['t'] = "т", ['u'] = "у", ['v'] = "в",
        ['w'] = "в", ['x'] = "х", ['y'] = "й", ['z'] = "з",
    };

    private const string Apostrophes = "'ʻʼ’‘`";

    private static bool IsApostrophe(char c) => Apostrophes.Contains(c);

    /// <summary>Текст узбекской латиницей → узбекская кириллица; русский текст, цифры и защищённые куски не меняются.</summary>
    public static string Convert(string text)
    {
        if (string.IsNullOrEmpty(text) || !AnyLatin().IsMatch(text))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var last = 0;
        foreach (Match m in Protected().Matches(text))
        {
            result.Append(ConvertFree(text[last..m.Index])).Append(m.Value);
            last = m.Index + m.Length;
        }

        return result.Append(ConvertFree(text[last..])).ToString();
    }

    /// <summary>Кусок без защищённых мест: слова переводятся, слово рядом с цифрой («P50», «1Base») — как есть.</summary>
    private static string ConvertFree(string part) =>
        Word().Replace(part, m =>
            (m.Index > 0 && char.IsDigit(part[m.Index - 1])) || (m.Index + m.Length < part.Length && char.IsDigit(part[m.Index + m.Length]))
                ? m.Value
                : ConvertWord(m.Value));

    private static string ConvertWord(string word)
    {
        var lower = word.ToLowerInvariant();
        // «Linko'da», «OneBase'da»: бренд остаётся, узбекское окончание — кириллицей.
        var apostrophe = word.IndexOfAny(Apostrophes.ToCharArray());
        if (apostrophe > 0)
        {
            var head = word[..apostrophe];
            if (Keep.Contains(head) || Camel().IsMatch(head) || English().IsMatch(head))
            {
                return head + "'" + Tail(word[(apostrophe + 1)..]);
            }
        }

        if (Keep.Contains(word))
        {
            return word;
        }

        foreach (var (latin, cyrillic) in Abbreviations)
        {
            if (word.StartsWith(latin, StringComparison.Ordinal))
            {
                return cyrillic + Tail(word[latin.Length..]);
            }
        }

        var letters = new string(word.Where(char.IsAsciiLetter).ToArray());
        if (letters.Length > 1 && letters == letters.ToUpperInvariant())
        {
            var normalized = string.Concat(word.Select(c => IsApostrophe(c) ? '\'' : c));
            return UzbekCaps.Contains(normalized) || (letters.Length >= 4 && !English().IsMatch(word)) ? Upper(word) : word;
        }

        // Сокращение с окончанием: «TPlarining» → «ТПларининг», «PDFni» → «PDFни».
        if (CapsHead().Match(word) is { Success: true } capsHead)
        {
            var head = capsHead.Value;
            return (UzbekCaps.Contains(head) ? Upper(head) : head) + Tail(word[head.Length..]);
        }

        if (Camel().IsMatch(word) || English().IsMatch(word))
        {
            return word;
        }

        var cyr = ToCyrillic(lower);
        return char.IsAsciiLetterUpper(word[0]) ? char.ToUpperInvariant(cyr[0]) + cyr[1..] : cyr;
    }

    private static string Upper(string latin) => ToCyrillic(latin.ToLowerInvariant()).ToUpperInvariant();

    private static string Tail(string latin) => latin.Length == 0 ? "" : ToCyrillic(latin.ToLowerInvariant());

    /// <summary>Одно слово в нижнем регистре (буквы и апострофы внутри) → кириллица.</summary>
    private static string ToCyrillic(string lower)
    {
        foreach (var (latin, cyrillic) in Stems)
        {
            if (lower.StartsWith(latin, StringComparison.Ordinal))
            {
                var rest = ToCyrillic(lower[latin.Length..]);
                return cyrillic + (rest.StartsWith('э') ? "е" + rest[1..] : rest);
            }
        }

        var result = new StringBuilder(lower.Length);
        var prev = '\0';
        for (var i = 0; i < lower.Length;)
        {
            var infix = Infixes.FirstOrDefault(x => string.CompareOrdinal(lower, i, x.Latin, 0, x.Latin.Length) == 0);
            if (infix.Latin is not null)
            {
                result.Append(infix.Cyrillic);
                i += infix.Latin.Length;
                prev = 'a';
                continue;
            }

            var c = lower[i];
            var next = i + 1 < lower.Length ? lower[i + 1] : '\0';
            if (c is 'o' or 'g' && next != '\0' && IsApostrophe(next))
            {
                result.Append(c == 'o' ? "ў" : "ғ");
                prev = c == 'o' ? 'ў' : 'g';
                i += 2;
                continue;
            }

            if (IsApostrophe(c))
            {
                result.Append('ъ');
                prev = '\0';
                i++;
                continue;
            }

            if (c is 's' or 'c' && next == 'h')
            {
                result.Append(c == 's' ? "ш" : "ч");
                prev = 'h';
                i += 2;
                continue;
            }

            if (c == 'y' && next is 'a' or 'e' or 'u')
            {
                result.Append(next == 'a' ? "я" : next == 'u' ? "ю" : "е");
                prev = next;
                i += 2;
                continue;
            }

            if (c == 'y' && next == 'o' && !(i + 2 < lower.Length && IsApostrophe(lower[i + 2])))
            {
                result.Append('ё');
                prev = 'o';
                i += 2;
                continue;
            }

            if (c == 'e')
            {
                result.Append(i == 0 || prev is 'a' or 'e' or 'i' or 'o' or 'u' or 'ў' ? 'э' : 'е');
                prev = 'e';
                i++;
                continue;
            }

            result.Append(Letters.TryGetValue(c, out var letter) ? letter : c.ToString());
            prev = c;
            i++;
        }

        return result.ToString();
    }

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex AnyLatin();

    /// <summary>
    /// Не переводятся: {0} и {name}, ссылки, почта, @имена, идентификаторы через точку или подчёркивание (строчными, части от 2 знаков),
    /// «P&amp;L», «Ctrl+C», «A–Z», ключи настроек (Telegram:Enabled=false), коды ошибок (PEER_FLOOD).
    /// </summary>
    [GeneratedRegex(@"\{[^{}]*\}|https?://\S+|www\.\S+|[\w.+-]+@[\w-]+\.[\w.]+|@\w+|\b[a-z][a-z0-9]+(?:[._][A-Za-z0-9]{2,})+\b|\b[A-Z]&[A-Z]\b|\b(?:Ctrl|Alt|Shift|Cmd|Win)\+\S+|\bA[–-]Z\b|\b[A-Za-z]+:[A-Za-z]+=\S+|\b[A-Z][A-Z0-9]*_[A-Z0-9_]+\b")]
    private static partial Regex Protected();

    /// <summary>Слово: латинские буквы с апострофами внутри (oʻ, gʻ, maʼlumot).</summary>
    [GeneratedRegex("[A-Za-z](?:[A-Za-z]|['ʻʼ’‘`](?=[A-Za-z]))*")]
    private static partial Regex Word();

    [GeneratedRegex("[a-z][A-Z]")]
    private static partial Regex Camel();

    /// <summary>Английское слово: в узбекской латинице нет «c» без «h», «w», «ph», «ck» и «th» в начале.</summary>
    [GeneratedRegex("c(?!h)|w|ph|ck|^th", RegexOptions.IgnoreCase)]
    private static partial Regex English();

    [GeneratedRegex("^[A-Z]{2,}(?=[a-z])")]
    private static partial Regex CapsHead();
}

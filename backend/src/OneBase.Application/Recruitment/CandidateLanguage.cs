using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OneBase.Application.Recruitment;

/// <summary>Язык и графика переписки с кандидатом: русский, узбекский латиницей, узбекский кириллицей.</summary>
public enum CandidateLang
{
    Ru,
    UzLatn,
    UzCyrl,
}

/// <summary>
/// Что показало одно сообщение: язык (ru, uz или null — не понять), графика (Latn, Cyrl или null), код (ru-Cyrl, uz-Latn, uz-Cyrl или null) и
/// уверенность 0–1. Explicit — кандидат прямо попросил писать на этом языке («пишите кириллицей», «ruscha yozing»).
/// </summary>
public sealed record LanguageDetection(string? Language, string? Script, double Confidence, int Letters, CandidateLang? Explicit = null)
{
    public static readonly LanguageDetection None = new(null, null, 0, 0);

    /// <summary>Поддерживаемый код: ru-Cyrl, uz-Latn, uz-Cyrl; иначе null.</summary>
    public string? Code => Lang is { } lang ? CandidateLanguages.Code(lang) : null;

    public CandidateLang? Lang => (Language, Script) switch
    {
        ("ru", _) => CandidateLang.Ru,
        ("uz", "Latn") => CandidateLang.UzLatn,
        ("uz", "Cyrl") => CandidateLang.UzCyrl,
        _ => null,
    };
}

/// <summary>Предпочтение кандидата после сообщения: язык и графика, что показало сообщение, и сменилось ли предпочтение.</summary>
public sealed record LanguagePreference(CandidateLang? Preferred, string Source, LanguageDetection Detected, bool Changed);

/// <summary>Коды и подписи языка кандидата; выбор текста шаблона по языку.</summary>
public static class CandidateLanguages
{
    public const string RuCyrl = "ru-Cyrl";
    public const string UzLatn = "uz-Latn";
    public const string UzCyrl = "uz-Cyrl";

    public static readonly string[] Codes = [RuCyrl, UzLatn, UzCyrl];

    public static string Code(CandidateLang lang) => lang switch
    {
        CandidateLang.UzLatn => UzLatn,
        CandidateLang.UzCyrl => UzCyrl,
        _ => RuCyrl,
    };

    public static CandidateLang? FromCode(string? code) => code switch
    {
        RuCyrl or "ru" => CandidateLang.Ru,
        UzLatn => CandidateLang.UzLatn,
        UzCyrl => CandidateLang.UzCyrl,
        _ => null,
    };

    /// <summary>Язык из полей кандидата (Language — ru/uz, PreferredScript — Latn/Cyrl); узбекский без графики — латиница.</summary>
    public static CandidateLang? Of(string? language, string? script) => language switch
    {
        "ru" => CandidateLang.Ru,
        "uz" => script == "Cyrl" ? CandidateLang.UzCyrl : CandidateLang.UzLatn,
        _ => null,
    };

    public static (string Language, string Script) Fields(CandidateLang lang) => lang switch
    {
        CandidateLang.UzLatn => ("uz", "Latn"),
        CandidateLang.UzCyrl => ("uz", "Cyrl"),
        _ => ("ru", "Cyrl"),
    };

    public static bool Uzbek(CandidateLang lang) => lang != CandidateLang.Ru;

    /// <summary>Текст шаблона на языке кандидата.</summary>
    public static string Pick(CandidateLang lang, string ru, string uzLatn, string uzCyrl) => lang switch
    {
        CandidateLang.UzLatn => uzLatn,
        CandidateLang.UzCyrl => uzCyrl,
        _ => ru,
    };

    /// <summary>Для модели: «русский», «узбекский, латиница», «узбекский, кириллица».</summary>
    public static string Label(CandidateLang lang) => Pick(lang, "русский", "узбекский, латиница", "узбекский, кириллица");

    /// <summary>Короткие дни недели (пн … вс) на языке кандидата — без зависимости от ICU сервера.</summary>
    public static string Day(CandidateLang lang, DayOfWeek day)
    {
        var i = ((int)day + 6) % 7; // пн = 0
        return Pick(lang,
            new[] { "пн", "вт", "ср", "чт", "пт", "сб", "вс" }[i],
            new[] { "Du", "Se", "Ch", "Pa", "Ju", "Sh", "Ya" }[i],
            new[] { "Ду", "Се", "Чо", "Па", "Жу", "Ша", "Як" }[i]);
    }
}

/// <summary>
/// Определение языка и графики сообщения кандидата и закрепление языка переписки.
/// <para>
/// Графика — по буквам; если приметы языка есть только в словах одной графики, слова другой — имена и термины («Меня зовут Sardor», «знаю
/// Excel»). Язык внутри графики — по словам-приметам: у узбекского кириллицей — буквы ў, қ, ғ, ҳ и слова («иш», «маълумот», «қачон»), у
/// латиницы — o'/g' и слова («ish», «haqida», «qachon»), у русского — служебные и частые слова и буквы ы, щ. Приветствие весит вполовину:
/// «Ассалому алайкум, хочу узнать про вакансию» — русский.
/// </para>
/// <para>
/// Закрепление: короткое или неоднозначное сообщение («ok», «1», «да») язык не меняет. Меняют — явная просьба кандидата, длинное уверенное
/// сообщение на другом языке или два уверенных сообщения подряд на одном и том же другом языке. Язык, выбранный HR или самим кандидатом,
/// меняет только новая явная просьба.
/// </para>
/// </summary>
public sealed partial class LanguageDetectionService
{
    /// <summary>Уверенность, с которой одно длинное сообщение (от StrongLetters букв) меняет закреплённый язык.</summary>
    public const double StrongConfidence = 0.85;

    public const int StrongLetters = 15;

    /// <summary>Уверенность каждого из двух сообщений подряд, чтобы сменить язык; и чтобы задать первый язык.</summary>
    public const double SteadyConfidence = 0.6;

    public const double FirstConfidence = 0.5;

    private static readonly HashSet<string> UzLatnWords = new(StringComparer.Ordinal)
    {
        "ish", "ishga", "ishni", "ishlash", "ishlayman", "ishlaganman", "ishlamoqchiman", "haqida", "ma'lumot", "malumot", "bersangiz", "bering",
        "kerak", "kerakmi", "qachon", "qayerda", "qancha", "nechta", "bo'ladi", "boladi", "bo'lsa", "men", "siz", "sizga", "sizni", "bizga", "bor",
        "bormi", "yo'q", "yoq", "ha", "xa", "yaxshi", "maosh", "oylik", "suhbat", "suhbatga", "vaqt", "vaqti", "uchun", "va", "bu", "emas", "ham",
        "nima", "qanday", "kim", "iltimos", "kechirasiz", "tajriba", "tajribam", "tajribangiz", "yil", "yildan", "yubordim", "yuboraman", "mumkin",
        "bilan", "lekin", "endi", "hozir", "ertaga", "bugun", "qaysi", "vakansiya", "vakansiyasi", "vakansiyalar", "xohlayman", "istayman", "olaman",
        "tushunarli", "mayli", "roziman", "rozi", "qilaman", "qilish", "joy", "manzil", "bilaman", "o'qiganman", "rezyume", "rezyumeni", "kelaman",
        "keldim", "javob", "yozing", "qiziqaman", "qiziqtiradi",
    };

    private static readonly HashSet<string> UzCyrlWords = new(StringComparer.Ordinal)
    {
        "иш", "ишга", "ишни", "ишлаш", "ишлайман", "ишлаганман", "ишламоқчиман", "ҳақида", "хакида", "хақида", "маълумот", "малумот", "берсангиз",
        "беринг", "керак", "керакми", "қачон", "качон", "қаерда", "қанча", "канча", "нечта", "бўлади", "булади", "бўлса", "булса", "мен", "сиз",
        "сизга", "сизни", "бизга", "борми", "йўқ", "йук", "ҳа", "яхши", "маош", "ойлик", "суҳбат", "сухбат", "суҳбатга", "вақт", "вакт", "вақти",
        "учун", "ва", "эмас", "ҳам", "хам", "нима", "қандай", "кандай", "ким", "илтимос", "кечирасиз", "тажриба", "тажрибам", "йил", "йилдан",
        "юбордим", "юбораман", "мумкин", "билан", "лекин", "энди", "ҳозир", "хозир", "эртага", "бугун", "қайси", "кайси", "вакансияси",
        "хоҳлайман", "истайман", "оламан", "тушунарли", "майли", "розиман", "рози", "қиламан", "манзил", "биламан", "резюмени", "келаман",
        "жавоб", "ёзинг", "қизиқаман",
    };

    private static readonly HashSet<string> RuWords = new(StringComparer.Ordinal)
    {
        "здравствуйте", "здравствуй", "привет", "добрый", "доброе", "день", "вечер", "утро", "спасибо", "пожалуйста", "что", "как", "где", "когда",
        "почему", "это", "этот", "эта", "работа", "работу", "работы", "работать", "работал", "работала", "хочу", "хотел", "хотела", "могу", "можно",
        "нужно", "есть", "нет", "да", "не", "и", "в", "на", "с", "по", "о", "я", "вы", "мы", "мне", "вас", "вам", "меня", "ваш", "вашей", "подробнее",
        "расскажите", "узнать", "вакансии", "вакансию", "вакансией", "зарплата", "зарплату", "опыт", "опыта", "лет", "год", "года", "собеседование",
        "собеседования", "график", "адрес", "сколько", "какая", "какой", "какие", "свободно", "время", "удобно", "отправил", "отправила", "интересует",
        "интересно", "готов", "готова", "согласен", "согласна", "только", "уже", "еще", "ещё", "тоже", "также", "если", "или", "но", "за", "про",
        "знаю", "зовут", "меня", "водителем", "менеджером", "условия", "оформление", "смогу", "буду", "был", "была",
    };

    private static readonly HashSet<string> EnWords = new(StringComparer.Ordinal)
    {
        "hello", "hi", "the", "is", "are", "i", "you", "job", "please", "thanks", "thank", "what", "how", "my", "your", "work", "salary", "interview",
        "and", "for", "with", "want", "can",
    };

    /// <summary>Русский латиницей (транслит) — тоже русский: отвечаем по-русски.</summary>
    private static readonly HashSet<string> RuTranslitWords = new(StringComparer.Ordinal)
    {
        "zdravstvuyte", "zdravstvuite", "privet", "spasibo", "rabota", "rabotu", "kak", "chto", "gde", "kogda", "hochu", "mozhno", "vakansiya",
        "vakansii", "zarplata", "dobryy", "dobriy", "den", "pozhaluysta",
    };

    private static readonly HashSet<string> Greetings = new(StringComparer.Ordinal)
    {
        "assalomu", "alaykum", "assalom", "salom", "rahmat", "raxmat", "ассалому", "алайкум", "ассалом", "салом", "раҳмат", "рахмат",
    };

    public LanguageDetection Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return LanguageDetection.None;
        }

        var clean = Noise().Replace(text, " ");
        var words = Word().Matches(clean.ToLowerInvariant()).Select(m => NormalizeApostrophes(m.Value)).ToList();
        var explicitLang = ExplicitRequest(text);
        double uzLat = 0, enLat = 0, ruLat = 0, uzCyr = 0, ruCyr = 0;
        int latin = 0, cyrillic = 0;
        foreach (var w in words)
        {
            var weight = Greetings.Contains(w) ? 0.5 : 1;
            if (IsCyrillic(w))
            {
                cyrillic += w.Count(char.IsLetter);
                if (UzCyrlWords.Contains(w) || Greetings.Contains(w))
                {
                    uzCyr += weight;
                }

                if (w.IndexOfAny(['ў', 'қ', 'ғ', 'ҳ']) >= 0)
                {
                    uzCyr += 2; // этих букв в русском нет
                }

                if (RuWords.Contains(w))
                {
                    ruCyr += 1;
                }

                if (w.IndexOfAny(['ы', 'щ']) >= 0)
                {
                    ruCyr += 1; // в узбекской кириллице их нет (кроме заимствований)
                }
            }
            else
            {
                latin += w.Count(char.IsLetter);
                if (UzLatnWords.Contains(w) || Greetings.Contains(w))
                {
                    uzLat += weight;
                }

                if (w.Length > 2 && OzGz().IsMatch(w))
                {
                    uzLat += 1; // o'/g' — примета узбекской латиницы
                }

                if (EnWords.Contains(w))
                {
                    enLat += 1;
                }

                if (RuTranslitWords.Contains(w))
                {
                    ruLat += 1;
                }
            }
        }

        var letters = latin + cyrillic;
        if (letters == 0)
        {
            return explicitLang is { } only ? Explicit(only, 0) : LanguageDetection.None;
        }

        var latinMarkers = uzLat + enLat + ruLat;
        var cyrillicMarkers = uzCyr + ruCyr;

        // Графика: приметы языка только в одной — она; другая — имена и термины. Иначе — по большинству букв.
        string script;
        double share;
        if (latinMarkers > 0 && cyrillicMarkers == 0)
        {
            (script, share) = ("Latn", latin == letters ? 1 : Math.Max(0.9, (double)latin / letters));
        }
        else if (cyrillicMarkers > 0 && latinMarkers == 0)
        {
            (script, share) = ("Cyrl", cyrillic == letters ? 1 : Math.Max(0.9, (double)cyrillic / letters));
        }
        else
        {
            (script, share) = latin > cyrillic ? ("Latn", (double)latin / letters) : ("Cyrl", (double)cyrillic / letters);
        }

        string? language;
        double confidence;
        if (script == "Cyrl")
        {
            if (uzCyr + ruCyr == 0)
            {
                // Кириллица без примет — скорее русский; уверенность растёт с длиной.
                (language, confidence) = ("ru", share * Math.Min(0.8, 0.3 + cyrillic / 50.0));
            }
            else
            {
                (language, confidence) = Score(uzCyr, ruCyr, "uz", "ru", share);
            }
        }
        else
        {
            var other = Math.Max(enLat, ruLat);
            var otherLanguage = ruLat >= enLat && ruLat > 0 ? "ru" : "en";
            if (uzLat + other == 0)
            {
                (language, confidence) = (null, 0.2); // латиница без примет: имя, «ok», слово по-английски
            }
            else
            {
                (language, confidence) = Score(uzLat, other, "uz", otherLanguage, share);
                if (language == "ru")
                {
                    confidence *= 0.8; // русский транслитом — отвечаем по-русски, но не уверенно
                }
            }
        }

        // Короткое сообщение — не повод менять язык.
        if (letters < 4)
        {
            confidence = Math.Min(confidence, 0.4);
        }
        else if (letters < 8)
        {
            confidence = Math.Min(confidence, 0.6);
        }

        confidence = Math.Round(Math.Min(confidence, 0.99), 2);
        if (explicitLang is { } requested)
        {
            return Explicit(requested, letters);
        }

        return language is "en" or null
            ? new LanguageDetection(language, script, confidence, letters)
            : new LanguageDetection(language, language == "ru" ? "Cyrl" : script, confidence, letters);
    }

    private static LanguageDetection Explicit(CandidateLang lang, int letters)
    {
        var (language, script) = CandidateLanguages.Fields(lang);
        return new LanguageDetection(language, script, 0.99, letters, lang);
    }

    private static (string Language, double Confidence) Score(double a, double b, string aName, string bName, double share)
    {
        var p = a / (a + b);
        var certainty = Math.Abs(p - 0.5) * 2;
        var evidence = Math.Min(1, (a + b) / 3);
        return (p >= 0.5 ? aName : bName, share * (0.5 + 0.5 * certainty) * (0.55 + 0.45 * evidence));
    }

    /// <summary>
    /// Язык после сообщения кандидата: current — закреплённый (null — ещё нет), source — кто его выбрал (auto, candidate, hr), previous — что
    /// показало прошлое сообщение (код и уверенность).
    /// </summary>
    public LanguagePreference Decide(CandidateLang? current, string? source, string? previousCode, double? previousConfidence, string? text)
    {
        var detected = Detect(text);
        if (detected.Explicit is { } requested)
        {
            // «Пишите кириллицей» от того, с кем уже говорим по-русски, — это русский (он и так кириллицей), а не узбекский.
            if (requested == CandidateLang.UzCyrl && current == CandidateLang.Ru && !Uzbek().IsMatch(NormalizeApostrophes(text!.ToLowerInvariant())))
            {
                requested = CandidateLang.Ru;
                detected = detected with { Language = "ru", Script = "Cyrl", Explicit = CandidateLang.Ru };
            }

            return new LanguagePreference(requested, "candidate", detected, requested != current || source != "candidate");
        }

        var lang = detected.Lang;
        if (lang is null)
        {
            return new LanguagePreference(current, source ?? "auto", detected, false);
        }

        if (current is null)
        {
            return detected.Confidence >= FirstConfidence
                ? new LanguagePreference(lang, "auto", detected, true)
                : new LanguagePreference(null, "auto", detected, false);
        }

        if (lang == current || source is "candidate" or "hr")
        {
            return new LanguagePreference(current, source ?? "auto", detected, false);
        }

        var strong = detected.Confidence >= StrongConfidence && detected.Letters >= StrongLetters;
        var twice = detected.Confidence >= SteadyConfidence && previousCode == detected.Code && (previousConfidence ?? 0) >= SteadyConfidence;
        return strong || twice
            ? new LanguagePreference(lang, "auto", detected, true)
            : new LanguagePreference(current, "auto", detected, false);
    }

    /// <summary>
    /// Явная просьба писать на другом языке или другой графикой: «пишите кириллицей», «kirillda yozing», «lotincha», «ruscha yozing»,
    /// «по-русски, пожалуйста», «ўзбекча ёзинг». Упоминание языка без просьбы («знаю русский и узбекский») — не просьба.
    /// </summary>
    public static CandidateLang? ExplicitRequest(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 300)
        {
            return null;
        }

        var t = NormalizeApostrophes(text.ToLowerInvariant());
        if (!Ask().IsMatch(t))
        {
            return null;
        }

        var cyrillic = CyrillicScript().IsMatch(t);
        var latin = LatinScript().IsMatch(t);
        var russian = Russian().IsMatch(t);
        var uzbek = Uzbek().Match(t);
        if (cyrillic && !latin)
        {
            return russian && !uzbek.Success ? CandidateLang.Ru : CandidateLang.UzCyrl;
        }

        if (latin && !cyrillic)
        {
            return CandidateLang.UzLatn;
        }

        if (russian && !uzbek.Success)
        {
            return CandidateLang.Ru;
        }

        if (uzbek.Success && !russian)
        {
            // «ўзбекча» кириллицей — кириллица; «o'zbekcha» и «по-узбекски» — латиница (официальная графика).
            return uzbek.Value.StartsWith('ў') || uzbek.Value.StartsWith("узбекча", StringComparison.Ordinal)
                                               || uzbek.Value.StartsWith("узбек тил", StringComparison.Ordinal)
                ? CandidateLang.UzCyrl
                : CandidateLang.UzLatn;
        }

        return null;
    }

    private static bool IsCyrillic(string word) => word.Any(c => c is >= 'а' and <= 'я' or 'ё' or 'ў' or 'қ' or 'ғ' or 'ҳ');

    private static string NormalizeApostrophes(string s) =>
        s.Replace('ʻ', '\'').Replace('ʼ', '\'').Replace('‘', '\'').Replace('’', '\'').Replace('`', '\'').Replace('´', '\'');

    /// <summary>Ссылки, почта, @имена, коды вакансий (#V12) и числа — не язык.</summary>
    [GeneratedRegex(@"https?://\S+|\S+@\S+\.\S+|@\w+|#?[VvВв]-?\d+|\d+", RegexOptions.CultureInvariant)]
    private static partial Regex Noise();

    [GeneratedRegex(@"[\p{L}][\p{L}'ʻʼ‘’`´-]*", RegexOptions.CultureInvariant)]
    private static partial Regex Word();

    [GeneratedRegex(@"[og]'", RegexOptions.CultureInvariant)]
    private static partial Regex OzGz();

    [GeneratedRegex(@"пиш|напиш|говор|отвеч|общ|давайте|можно|лучше|удобнее|пожалуйста|yoz|ёз|gapir|гапир|javob|жавоб|muloqot|мулоқот|iltimos|илтимос|please")]
    private static partial Regex Ask();

    [GeneratedRegex(@"кирилл|kirill|krill")]
    private static partial Regex CyrillicScript();

    [GeneratedRegex(@"латин|lotin")]
    private static partial Regex LatinScript();

    [GeneratedRegex(@"по-?русски|на русском|rus tilida|ruscha|rus tilda|русча|рус тилида")]
    private static partial Regex Russian();

    [GeneratedRegex(@"по-?узбекски|на узбекском|o'zbekcha|o'zbek tilida|ozbekcha|uzbekcha|ўзбекча|ўзбек тилида|узбекча|узбек тилида")]
    private static partial Regex Uzbek();
}

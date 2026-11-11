namespace OneBase.Application.Recruitment;

/// <summary>
/// Как обратиться к кандидату: имя из ФИО («Рахимов Сардор», «Sardor Rahimov», «Сардор») и уважительное «Ака» / «Опа» (как пишут в
/// Узбекистане). Пол — по отчеству (…ович / …овна, o'g'li / qizi), фамилии (…ов / …ова) или имени (Дилноза, Гульнора, Нилуфар — «Опа»;
/// Сардор, Жасур — «Ака»). Не понятно — только имя; имени нет (@username, «Telegram 123») — null. Текст с обращением HR видит до отправки.
/// </summary>
public static class CandidateNames
{
    private static readonly string[] FemaleEndings = ["ova", "eva", "yeva", "ова", "ева", "ёва", "skaya", "ская"];
    private static readonly string[] MaleEndings = ["ov", "ev", "yev", "ов", "ев", "ёв", "skiy", "sky", "ский"];
    private static readonly string[] FemalePatronymic = ["ovna", "evna", "ichna", "овна", "евна", "ична"];
    private static readonly string[] MalePatronymic = ["ovich", "evich", "ович", "евич", "ьич"];
    private static readonly HashSet<string> FemaleWords = ["qizi", "kizi", "кизи", "қизи"];
    private static readonly HashSet<string> MaleWords = ["o'g'li", "ogli", "ugli", "угли", "ўғли", "оглы", "ogly"];

    /// <summary>Женские имена не на «а»/«я» — остальные женские узбекские и русские имена кончаются на «а»/«я» или содержат «гул».</summary>
    private static readonly HashSet<string> FemaleNames =
    [
        "nilufar", "нилуфар", "yulduz", "юлдуз", "saodat", "саодат", "gavhar", "гавҳар", "гавхар", "nargis", "наргис", "kumush", "кумуш",
        "mohinur", "моҳинур", "мохинур", "shahlo", "шаҳло", "шахло", "barno", "барно", "mahliyo", "маҳлиё", "махлиё", "nigor", "нигор",
        "oydin", "ойдин", "zebo", "зебо", "ra'no", "раъно", "rano", "рано", "sevinch", "севинч", "ruxsor", "рухсор", "любовь", "нинель",
    ];

    /// <summary>Мужские имена на гласную.</summary>
    private static readonly HashSet<string> MaleNames =
    [
        "musa", "муса", "isa", "иса", "mustafa", "мустафа", "hamza", "хамза", "ҳамза", "nikita", "никита", "ilya", "илья", "yahyo", "яҳё", "яхё",
        "muso", "мусо", "iso", "исо", "murtazo", "муртазо", "mustafo", "мустафо", "zakariyo", "закариё", "luqmon", "foma", "фома", "kuzma", "кузьма",
        "sasha", "саша", "misha", "миша", "vanya", "ваня", "petya", "петя", "kolya", "коля", "dima", "дима", "lyosha", "лёша", "алёша",
    ];

    private static string[] Words(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.TrimStart().StartsWith("Telegram ", StringComparison.Ordinal))
        {
            return [];
        }

        return fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim(',', '.', ';', '!', '?', '"', '«', '»', '(', ')'))
            .Where(w => w.Length > 0)
            .ToArray();
    }

    private static string Norm(string word) => word.ToLowerInvariant().Replace('ʻ', '\'').Replace('’', '\'').Replace('‘', '\'').Replace('`', '\'');

    private static bool Ends(string word, IEnumerable<string> endings) => endings.Any(e => word.EndsWith(e, StringComparison.Ordinal));

    private static bool IsSurname(string word)
    {
        var w = Norm(word);
        return w.Length >= 5 && w.All(c => char.IsLetter(c) || c is '-' or '\'') && (Ends(w, FemaleEndings) || Ends(w, MaleEndings));
    }

    private static bool IsPatronymic(string word)
    {
        var w = Norm(word);
        return Ends(w, FemalePatronymic) || Ends(w, MalePatronymic) || FemaleWords.Contains(w) || MaleWords.Contains(w);
    }

    /// <summary>Имя из ФИО: «Рахимов Сардор» → «Сардор», «Sardor Rahimov» → «Sardor»; @username, «Telegram 123» и одна фамилия — null.</summary>
    public static string? FirstName(string? fullName)
    {
        var words = Words(fullName);
        if (words.Length == 0 || words[0].StartsWith('@'))
        {
            return null;
        }

        var name = words.Length >= 2 && IsSurname(words[0]) && !IsSurname(words[1]) && !IsPatronymic(words[1]) ? words[1]
            : IsSurname(words[0]) || IsPatronymic(words[0]) ? null
            : words[0];
        if (name is null || name.Length < 2 || !name.All(c => char.IsLetter(c) || c is '-' or '\'' or 'ʻ' or '’'))
        {
            return null;
        }

        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>Женщина ли: true / false; не понятно — null.</summary>
    public static bool? Female(string? fullName)
    {
        var words = Words(fullName).Select(Norm).ToArray();
        foreach (var w in words)
        {
            if (FemaleWords.Contains(w) || Ends(w, FemalePatronymic))
            {
                return true;
            }

            if (MaleWords.Contains(w) || Ends(w, MalePatronymic))
            {
                return false;
            }
        }

        foreach (var w in words.Where(IsSurname))
        {
            return Ends(w, FemaleEndings);
        }

        if (FirstName(fullName) is not { } first)
        {
            return null;
        }

        var name = Norm(first);
        if (MaleNames.Contains(name))
        {
            return false;
        }

        if (FemaleNames.Contains(name) || name.Contains("gul", StringComparison.Ordinal) || name.Contains("гул", StringComparison.Ordinal))
        {
            return true;
        }

        var last = name[^1];
        return last switch
        {
            'a' or 'а' or 'я' => true,
            'o' or 'о' or 'e' or 'е' or 'u' or 'у' or 'ю' or 'ё' or 'э' or 'ы' => null,
            _ when char.IsLetter(last) => false, // согласная (и «и», «й»: Али, Николай) — мужское
            _ => null,
        };
    }

    /// <summary>Обращение: «Сардор Ака», «Dilnoza Opa» (графика — как у имени); пол не понятен — «Сардор»; имени нет — null.</summary>
    public static string? Address(string? fullName)
    {
        if (FirstName(fullName) is not { } name)
        {
            return null;
        }

        var cyrillic = name.Any(c => c is >= 'А' and <= 'я' or 'Ё' or 'ё' or 'ў' or 'Ў' or 'қ' or 'Қ' or 'ғ' or 'Ғ' or 'ҳ' or 'Ҳ');
        return Female(fullName) switch
        {
            true => $"{name} {(cyrillic ? "Опа" : "Opa")}",
            false => $"{name} {(cyrillic ? "Ака" : "Aka")}",
            null => name,
        };
    }
}

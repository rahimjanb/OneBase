using System.Text.RegularExpressions;
using OneBase.AI.Orchestration;

namespace OneBase.AI.Consultant;

/// <summary>Язык ответа консультанта — язык вопроса: русский, узбекский латиницей, узбекский кириллицей или другой (например, английский).</summary>
public enum AnswerLanguage
{
    Ru,
    UzLatn,
    UzCyrl,
    Other,
}

/// <summary>
/// Язык текста по буквам: больше латиницы — узбекский латиницей, если есть его приметы (o', g', «bo'yicha», «qancha», «ma'lumot»…), иначе
/// другой язык (английский): ответ — на нём же; кириллица с
/// буквами ў, қ, ғ, ҳ или узбекскими словами («бўйича», «қанча», «савдо», «режа», «маълумот», «мавжуд», «эмас») — узбекский кириллицей; иначе русский.
/// </summary>
public static partial class AnswerLanguages
{
    public static AnswerLanguage Of(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AnswerLanguage.Ru;
        }

        var latin = text.Count(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z');
        var cyrillic = text.Count(c => c is >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё' or 'ў' or 'Ў' or 'қ' or 'Қ' or 'ғ' or 'Ғ' or 'ҳ' or 'Ҳ');
        if (latin > cyrillic)
        {
            return UzbekLatin().IsMatch(RouteIntent.Normalize(text)) ? AnswerLanguage.UzLatn : AnswerLanguage.Other;
        }

        return UzbekCyrillic().IsMatch(text) ? AnswerLanguage.UzCyrl : AnswerLanguage.Ru;
    }

    /// <summary>Название языка для указания модели: «русский», «узбекский (латиница)», «узбекский (кириллица)».</summary>
    public static string Label(AnswerLanguage language) => language switch
    {
        AnswerLanguage.UzLatn => "узбекский (латиница)",
        AnswerLanguage.UzCyrl => "узбекский (кириллица)",
        AnswerLanguage.Other => "тот же, что у вопроса",
        _ => "русский",
    };

    [GeneratedRegex(@"[og]'\p{L}|\p{L}[og]'|(?<![\p{L}'])(?:va|bu|uchun|bo'yicha|qancha|nechta|qaysi|nima|kim|bugun|ishga|savdo|sotuv\p{L}*|reja\p{L}*|oy\p{L}*|mavjud|emas|yo'q|ma'lumot\p{L}*|hisobot\p{L}*|kechik\p{L}*|xodim\p{L}*|ombor\p{L}*|qoldiq\p{L}*|yoki|hali|davomat\p{L}*|hudud\p{L}*|viloyat\p{L}*)(?![\p{L}])")]
    private static partial Regex UzbekLatin();

    [GeneratedRegex(@"[ўқғҳЎҚҒҲ]|(?<![\p{L}])(?:бўйича|буйича|қанча|канча|нечта|савдо|режа\p{L}*|ходим\p{L}+|омбор\p{L}*|учун|ва|бу|нима|қайси|кайси|ҳудуд\p{L}*|худуд\p{L}*|вилоят\p{L}*|ой\p{L}*да|маълумот\p{L}*|мавжуд|эмас|ёки|ҳали|хали|бугун|ким|ишга|кечик\p{L}*|давомат\p{L}*|сотув\p{L}*|қачон|качон|нега|йўқ|йук|ҳисобот\p{L}*|хисобот\p{L}*|ойда|ойи)(?![\p{L}])", RegexOptions.IgnoreCase)]
    private static partial Regex UzbekCyrillic();
}

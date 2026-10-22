using System.Text.RegularExpressions;

namespace OneBase.AI.Telegram;

/// <summary>
/// Просьба что-то сформировать — отчёт, таблицу, документ, письмо, план, презентацию, разбор. Такие ответы Telegram-агент
/// генерирует моделью генерации (Claude) и присылает файлом, обычные — моделью ответов и текстом. Срабатывает на пару
/// «действие + что сделать» («сделай отчёт», «нужна таблица», «xat yozing») или на просьбу о файле («в Excel», «выгрузи»).
/// Вопрос об отчёте («когда следующий отчёт?», «что в отчёте?») и простой вопрос («как продажи в Хорезме?») сюда не попадают.
/// Русский и узбекский.
/// </summary>
public static partial class GenerationIntent
{
    public static bool Requested(string? question) =>
        question is { Length: > 0 } q && (FileWanted().IsMatch(q) || (Action().IsMatch(q) && Thing().IsMatch(q)));

    /// <summary>
    /// «Минутку, уже делаю» перед тем, как готовить документ: на языке из настроек агента, при «как в вопросе» — на языке сообщения.
    /// Фраза меняется от сообщения к сообщению (seed — номер сообщения), чтобы не звучать одинаково.
    /// </summary>
    public static string Ack(string? language, string question, int seed)
    {
        string[] phrases = (language is "ru" or "uz" or "en" ? language : LanguageOf(question)) switch
        {
            "uz" => ["Bir daqiqa, hozir tayyorlayman.", "Hozir tayyorlab beraman, bir daqiqa.", "Tayyorlayapman, bir daqiqa kuting."],
            "en" => ["One moment, working on it.", "On it, just a minute.", "Give me a minute, preparing it now."],
            _ => ["Минутку, уже делаю.", "Секунду, уже готовлю.", "Уже делаю, минутку."],
        };
        return phrases[(int)((uint)seed % (uint)phrases.Length)];
    }

    /// <summary>Язык сообщения: кириллица — русский (узбекская кириллица — узбекский), латиница с узбекскими словами — узбекский, иначе английский.</summary>
    public static string LanguageOf(string text)
    {
        if (UzbekCyrillic().IsMatch(text))
        {
            return "uz";
        }

        if (Cyrillic().IsMatch(text))
        {
            return "ru";
        }

        return UzbekLatin().IsMatch(text) ? "uz" : Latin().IsMatch(text) ? "en" : "ru";
    }

    [GeneratedRegex(
        @"\b(сдела(й|йте|ть)|состав(ь|ьте|ить)|подготов(ь|ьте|ить)|сформир(уй|уйте|овать)|напиш(и|ите)|написать|сгенерир(уй|уйте|овать)|созда(й|йте|ть)|оформ(и|ите|ить)|собер(и|ите)|собрать|набросай|распиш(и|ите)|дай(те)?|скинь(те)?|пришли(те)?|отправь(те)?|нуж(ен|на)|tayyorla\w*|tuz(ing|ib)?|yoz(ing|ib)?|yarat\w*|shakllantir\w*|yubor\w*|kerak|ber(ing)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Action();

    [GeneratedRegex(@"excel|[эе]кс[еэ]л|иксел|\bxlsx?\b|\bdocx?\b|\bворд\b|\bword\b|выгру[зж]|скача|файлом|fayl\s+(qilib|ko'rinishida)|spreadsheet",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileWanted();

    [GeneratedRegex(
        @"отч[её]т|таблиц|документ|письм|текст|\bпост\w{0,2}\b|объявлени|презентаци|план|сводк|справк|резюме|инструкци|регламент|предложени|анализ|разбор|график|диаграмм|файл|hisobot|\breport|jadval|hujjat|\bxat|matn|reja|taqdimot|tahlil|ma'lumotnoma",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Thing();

    [GeneratedRegex(@"[ўқғҳЎҚҒҲ]")]
    private static partial Regex UzbekCyrillic();

    [GeneratedRegex(@"[А-Яа-яЁё]")]
    private static partial Regex Cyrillic();

    [GeneratedRegex(@"[og][‘'ʻ’][a-z]|\b(va|uchun|bilan|menga|kerak|bering|ber|qil|qiling|qilib|hisobot\w*|tayyorla\w*|savdo\w*|jadval\w*|xat|yoz\w*|tuz\w*|bo'yicha|haqida|qancha|nima|qanday)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UzbekLatin();

    [GeneratedRegex(@"[A-Za-z]")]
    private static partial Regex Latin();
}

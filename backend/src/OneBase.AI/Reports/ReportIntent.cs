using System.Text.RegularExpressions;

namespace OneBase.AI.Reports;

/// <summary>
/// Просит ли пользователь отчёт-файл. Да — формат файла (Excel, xlsx, «в эксель»), выгрузка или скачивание («выгрузи», «скачать»), или слово
/// «отчёт» с действием («сделай», «сформируй», «нужен», «hisobot tayyorla») либо без вопроса («Отчёт по продажам за сентябрь»,
/// «Sentabr bo'yicha hisobot»). Нет — вопрос об отчётах («Какие отчёты отправлялись вчера?») и автоотчёты Telegram («Какие автоотчёты ушли?»):
/// это данные, а не просьба сформировать файл. Только при «да» AI-сотрудникам даётся инструмент отчёта. «Экспорт» — раздел продаж, а не
/// выгрузка: по нему отчёт не включается.
/// </summary>
public static partial class ReportIntent
{
    public static bool Requested(string? question)
    {
        if (question is not { Length: > 0 })
        {
            return false;
        }

        // Автоотчёт — рассылка Telegram, а не просьба о файле: само слово отчёт не включает.
        var text = AutoReport().Replace(question.Replace('ё', 'е').Replace('Ё', 'Е'), " ");
        if (FileFormat().IsMatch(text) || Export().IsMatch(text))
        {
            return true;
        }

        return ReportWord().IsMatch(text) && (Action().IsMatch(text) || !Question().IsMatch(text));
    }

    [GeneratedRegex(@"авто\s*-?\s*отчет\p{L}*|avto\s*-?\s*hisobot\p{L}*|автоҳисобот\p{L}*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AutoReport();

    [GeneratedRegex(@"excel|[эе]кс[еэ]л|иксел|\bxlsx?\b|spreadsheet", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileFormat();

    [GeneratedRegex(@"(?<!\p{L})(?:выгру[зж]|скача|скинь\s+файл)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Export();

    [GeneratedRegex(@"отчет|hisobot|ҳисобот|хисобот|\breport\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReportWord();

    [GeneratedRegex(
        @"(?<!\p{L})(?:сделай|сделать|сформируй|сформировать|составь|составить|подготовь|подготовить|собери|собрать|создай|создать|сгенерируй|" +
        @"построй|построить|пришли|прислать|скинь|дай|дайте|нужен|нужна|нужно|хочу|можешь|" +
        // узбекский — повелительное наклонение: «hisobot tayyorla(ng)», не «tayyorlangan» (подготовлен) и не «yuborildimi» (отправлен ли)
        @"tayyorla(?:ng)?|tuz(?:ing)?|yarat(?:ing)?|chiqar(?:ing)?|yubor(?:ing)?|kerak|ber(?:ing)?|" +
        @"тайёрла(?:нг)?|туз(?:инг)?|ярат(?:инг)?|чиқар(?:инг)?|юбор(?:инг)?|керак|бер(?:инг)?|make|create|generate|build|prepare)(?!\p{L})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Action();

    [GeneratedRegex(
        @"\?|(?<!\p{L})(?:какие|какой|какая|каких|когда|сколько|кто|где|почему|ушл\p{L}*|ушел|отправлял\p{L}*|отправил\p{L}*|приходил\p{L}*|" +
        @"qaysi|qachon|nechta|qancha|kim|nima|yuborildi\p{L}*|қайси|қачон|нечта|қанча|ким|нима)(?!\p{L})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Question();
}

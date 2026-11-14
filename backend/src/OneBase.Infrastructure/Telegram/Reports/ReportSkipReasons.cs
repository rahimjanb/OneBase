namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Причина пропуска отправки — для разбивки «Пропущено · 7 дней» («нет чата · 412, нет акции · 76»). Текст причины пишет код отправки:
/// не про чат — только перечисленные ниже (получатель выключен, условие отчёта, акция, несвежие или неполные данные); всё остальное —
/// ChatProblem (чат удалён, другой аккаунт, писать нельзя — причина из Telegram), то есть «нет чата».
/// </summary>
public static class ReportSkipReasons
{
    public const string NoChat = "no_chat";
    public const string Disabled = "disabled";
    public const string NotNeeded = "not_needed";
    public const string Promo = "promo";
    public const string Stale = "stale";

    public static string Key(string? error)
    {
        var text = error?.Trim() ?? "";
        if (text.Length == 0)
        {
            return NotNeeded;
        }

        if (text.StartsWith("Получатель ", StringComparison.Ordinal))
        {
            return Disabled; // «Получатель отключён или отписан от этого отчёта.», «Получатель удалён.»
        }

        if (text.Contains("не обновились", StringComparison.Ordinal) || text.StartsWith("Данные неполные", StringComparison.Ordinal))
        {
            return Stale; // заказы Linko не обновились к карточке акции; неполные данные еженедельного PDF
        }

        if (text.Contains("акци", StringComparison.OrdinalIgnoreCase))
        {
            return Promo; // акция выключена или день вне её периода, нет акций ноября, роли в этот запуск ничего не положено
        }

        return text.StartsWith("Условие отчёта", StringComparison.Ordinal) ? NotNeeded : NoChat;
    }

    /// <summary>Пропуски по причинам: больше — первыми.</summary>
    public static IReadOnlyList<ReportSkipCount> Count(IEnumerable<(string? Error, int Count)> rows) =>
        rows.GroupBy(r => Key(r.Error)).Select(g => new ReportSkipCount(g.Key, g.Sum(x => x.Count))).Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
}

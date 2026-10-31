using System.Globalization;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Weekly;

/// <summary>
/// Подписи еженедельного PDF: русский — ключ, узбекский (латиница) — словарь Uz; нет перевода — русский. Замечания и выводы модели
/// (WeeklyNote) превращаются в текст здесь — модель остаётся без языка.
/// </summary>
public static class WeeklyTexts
{
    /// <summary>Подпись на языке отчёта: «uz» — словарь Uz, иначе — общий словарь автоотчётов; «uz-Cyrl» — то же кириллицей.</summary>
    public static string T(string language, string ru) => language switch
    {
        "uz" => Uz.TryGetValue(ru, out var uz) ? uz : ReportTexts.Get("uz", ru),
        "uz-Cyrl" => Uz.TryGetValue(ru, out var latin) ? ReportTexts.Uzbek(language, latin) : ReportTexts.Get(language, ru),
        _ => ru,
    };

    public static string F(string language, string ru, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(language, ru), args.Select(a => a ?? "").ToArray());

    /// <summary>Изменение со знаком: +22%, −15% (минус — типографский), нет базы — «—».</summary>
    public static string Delta(decimal? change) => change is { } c
        ? (c >= 0 ? "+" : "−") + Math.Abs(Math.Round(c * 100, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "%"
        : "—";

    /// <summary>Текст замечания или вывода модели на языке отчёта; неизвестный код — null (не показывается).</summary>
    public static string? Note(WeeklyNote note, string language)
    {
        string A(string key) => note.Args.TryGetValue(key, out var v) ? v : "";
        static decimal? Dec(string s) => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
        return note.Code switch
        {
            "good" => A("grew") is { Length: > 0 } grew
                ? F(language, "Хорошо: {0} — неделя {1} к прошлой; лучший прогноз месяца — {2} ({3}).", grew, Delta(Dec(A("growth"))), A("best"),
                    ReportFormat.Percent(Dec(A("bestForecast"))))
                : F(language, "Хорошо: лучший прогноз месяца — {0} ({1}).", A("best"), ReportFormat.Percent(Dec(A("bestForecast")))),
            "bad" => F(language, "Плохо: прогноз месяца — {0} плана. Ниже всех: {1}.", ReportFormat.Percent(Dec(A("forecast"))),
                string.Join(", ", A("worst").Split("; ", StringSplitOptions.RemoveEmptyEntries).Select(w => w.Split('|')).Where(p => p.Length == 2)
                    .Select(p => $"{p[0]} {ReportFormat.Percent(Dec(p[1]))}"))),
            "focus" => A("current") is { Length: > 0 } current
                ? F(language, "Фокус до конца месяца: до 80% плана — {0} кг в день (сейчас {1} кг в день).", ReportFormat.Number(Dec(A("perDay"))), ReportFormat.Number(Dec(current)))
                : F(language, "Фокус до конца месяца: до 80% плана — {0} кг в день.", ReportFormat.Number(Dec(A("perDay")))),
            "week-partial" => F(language, "Неделя ещё идёт: данные по {0}, {1} из {2} рабочих дней.", A("through"), A("done"), A("total")),
            "dmp-missing" => T(language, "ДМП: данных о стеллажах в OneBase пока нет — загрузка инвентаря Linko будет добавлена."),
            "kpi-missing" => T(language, "KPI Linko по области не загружены — премии и пороги КПУ не показаны."),
            "dso-dealer-missing" => F(language, "DSO: дилер «{0}» ({1}) не найден в реестре оплат — сверить название дилера.", A("dealer"), A("region")),
            "orders-stale" => F(language, "Заказы Linko обновлялись {0} — более поздние заказы в отчёт не вошли.", A("asOf")),
            _ => null,
        };
    }

    /// <summary>Источник данных по ключу WeeklySources — для «неполные: …».</summary>
    public static string Source(string key, string language) => T(language, key switch
    {
        WeeklySources.Orders => "заказы Linko",
        WeeklySources.Visits => "визиты Linko",
        WeeklySources.Plans => "планы Linko",
        WeeklySources.Kpi => "KPI Linko",
        WeeklySources.Promo => "акции",
        WeeklySources.Stock => "остатки дилеров",
        WeeklySources.Dso => "DSO",
        WeeklySources.Dmp => "ДМП",
        _ => key,
    });

    /// <summary>
    /// Подпись к PDF в Telegram (до 1024 знаков): неделя и область, продажи недели к прошлой, месяц и прогноз, АКБ, «хорошо» и «плохо», цель
    /// следующей недели; ТП — личные цифры и цели. Неполные данные — пометка. cards — вторым файлом идут карточки ТП.
    /// </summary>
    public static string Caption(WeeklySupervisorReport r, string language, bool cards, bool test)
    {
        var lines = new List<string>();
        if (test)
        {
            lines.Add(T(language, "ТЕСТОВАЯ ОТПРАВКА"));
        }

        var week = $"{r.PeriodStart:dd.MM}–{r.PeriodEnd:dd.MM.yyyy}";
        var g = r.WeeklyGoals;
        var next = $"{g.WeekFrom:dd.MM}–{g.WeekTo:dd.MM}";
        if (r.Role == TelegramReportRoles.Tp)
        {
            var e = r.EmployeeMetrics.FirstOrDefault();
            var goal = g.Agents.FirstOrDefault();
            lines.Add(F(language, "Итоги недели {0} · {1}", week, e?.Name ?? r.ScopeName));
            if (e is not null)
            {
                lines.Add(F(language, "Неделя: {0} кг ({1} к прошлой) · месяц — {2} плана", ReportFormat.Number(e.WeekKg), Delta(e.WeekVsPrev), ReportFormat.Percent(e.Execution)));
                lines.Add(F(language, "АКБ недели {0} · визиты / заказы {1} / {2}", e.WeekAkb, e.WeekVisits, e.WeekOrders));
            }

            if (goal is not null)
            {
                lines.Add(F(language, "Цели на {0}: продажи {1} кг, АКБ {2}, ТТ по акциям {3}, вернуть спящих {4}", next, ReportFormat.Number(goal.SalesKg), goal.Akb, goal.Promo,
                    goal.Return15To30 + goal.Return30Plus));
            }
        }
        else
        {
            var who = r.Role switch { "rop" => T(language, "РОП"), TelegramReportRoles.Rm => T(language, "РМ"), _ => T(language, "СВ") };
            lines.Add($"{F(language, "Неделя {0}", week)} · {who} {r.ScopeName}{(r.SupervisorName is { Length: > 0 } sv ? " · " + sv : "")}");
            var s = r.SalesMetrics;
            lines.Add(F(language, "Продажи недели: {0} кг ({1} к прошлой)", ReportFormat.Number(s.WeekKg), Delta(s.WeekVsPrev)));
            lines.Add(F(language, "Месяц: {0} кг — {1} плана, прогноз {2}", ReportFormat.Number(s.MonthKg), ReportFormat.Percent(s.Execution), ReportFormat.Percent(s.ForecastExecution)));
            lines.Add(F(language, "АКБ месяца: {0} из {1}", ReportFormat.Number(s.MonthAkb), ReportFormat.Number(s.AkbPlan)));
            lines.AddRange(r.Recommendations.Where(n => n.Code is "good" or "bad").Select(n => Note(n, language)).OfType<string>());
            if (r.Role == TelegramReportRoles.Sv)
            {
                lines.Add(F(language, "Цель на {0}: продажи {1} кг, АКБ {2}, ТТ по акциям {3}", next, ReportFormat.Number(g.SalesKg), g.Akb, g.Promo));
            }

            if (cards)
            {
                lines.Add(T(language, "Карточки ТП — вторым файлом."));
            }
        }

        lines.Add(r.Incomplete.Count > 0
            ? F(language, "Данные на {0} — неполные: {1}.", r.DataThrough.ToString("dd.MM", CultureInfo.InvariantCulture), string.Join(", ", r.Incomplete.Select(k => Source(k, language))))
            : F(language, "Данные на {0}.", r.DataThrough.ToString("dd.MM", CultureInfo.InvariantCulture)));
        var text = string.Join("\n", lines);
        return text.Length > TelegramRules.MaxCaptionLength ? text[..(TelegramRules.MaxCaptionLength - 1)] + "…" : text;
    }

    private static readonly Dictionary<string, string> Uz = new()
    {
        ["заказы Linko"] = "Linko buyurtmalari",
        ["визиты Linko"] = "Linko tashriflari",
        ["планы Linko"] = "Linko rejalari",
        ["акции"] = "aksiyalar",
        ["остатки дилеров"] = "dilerlar qoldiqlari",
        ["ДМП"] = "DMP",
        ["ТЕСТОВАЯ ОТПРАВКА"] = "SINOV JO'NATMASI",
        ["Итоги недели {0} · {1}"] = "Hafta yakunlari {0} · {1}",
        ["Неделя: {0} кг ({1} к прошлой) · месяц — {2} плана"] = "Hafta: {0} kg (o'tganiga nisbatan {1}) · oy — rejaning {2}",
        ["АКБ недели {0} · визиты / заказы {1} / {2}"] = "Hafta AKB {0} · tashriflar / buyurtmalar {1} / {2}",
        ["Цели на {0}: продажи {1} кг, АКБ {2}, ТТ по акциям {3}, вернуть спящих {4}"] = "{0} maqsadlari: savdo {1} kg, AKB {2}, aksiyalar bo'yicha SN {3}, uxlayotganlarni qaytarish {4}",
        ["РОП"] = "ROP",
        ["РМ"] = "RM",
        ["СВ"] = "SV",
        ["Неделя {0}"] = "Hafta {0}",
        ["Продажи недели: {0} кг ({1} к прошлой)"] = "Hafta savdosi: {0} kg (o'tganiga nisbatan {1})",
        ["Месяц: {0} кг — {1} плана, прогноз {2}"] = "Oy: {0} kg — rejaning {1}, prognoz {2}",
        ["АКБ месяца: {0} из {1}"] = "Oy AKB: {1} dan {0}",
        ["Цель на {0}: продажи {1} кг, АКБ {2}, ТТ по акциям {3}"] = "{0} maqsadi: savdo {1} kg, AKB {2}, aksiyalar bo'yicha SN {3}",
        ["Карточки ТП — вторым файлом."] = "TP kartochkalari — ikkinchi faylda.",
        ["Данные на {0} — неполные: {1}."] = "Ma'lumotlar {0} holatiga — to'liq emas: {1}.",
        ["Данные на {0}."] = "Ma'lumotlar {0} holatiga.",
        ["Хорошо: {0} — неделя {1} к прошлой; лучший прогноз месяца — {2} ({3})."] = "Yaxshi: {0} — hafta o'tganiga nisbatan {1}; oyning eng yaxshi prognozi — {2} ({3}).",
        ["Хорошо: лучший прогноз месяца — {0} ({1})."] = "Yaxshi: oyning eng yaxshi prognozi — {0} ({1}).",
        ["Плохо: прогноз месяца — {0} плана. Ниже всех: {1}."] = "Yomon: oy prognozi — rejaning {0}. Eng pasti: {1}.",
        ["Фокус до конца месяца: до 80% плана — {0} кг в день (сейчас {1} кг в день)."] = "Diqqat — oy oxirigacha: rejaning 80% igacha — kuniga {0} kg (hozir kuniga {1} kg).",
        ["Фокус до конца месяца: до 80% плана — {0} кг в день."] = "Diqqat — oy oxirigacha: rejaning 80% igacha — kuniga {0} kg.",
        ["Неделя ещё идёт: данные по {0}, {1} из {2} рабочих дней."] = "Hafta hali tugamagan: ma'lumotlar {0} gacha, {2} ish kunidan {1}.",
        ["ДМП: данных о стеллажах в OneBase пока нет — загрузка инвентаря Linko будет добавлена."] = "DMP: OneBase'da stellajlar bo'yicha ma'lumot hozircha yo'q — Linko inventarini yuklash qo'shiladi.",
        ["KPI Linko по области не загружены — премии и пороги КПУ не показаны."] = "Hudud bo'yicha Linko KPI yuklanmagan — mukofotlar va KPU chegaralari ko'rsatilmagan.",
        ["DSO: дилер «{0}» ({1}) не найден в реестре оплат — сверить название дилера."] = "DSO: diler «{0}» ({1}) to'lovlar reestrida topilmadi — diler nomini tekshiring.",
        ["Заказы Linko обновлялись {0} — более поздние заказы в отчёт не вошли."] = "Linko buyurtmalari {0} da yangilangan — keyingi buyurtmalar hisobotga kirmadi.",
    };
}

/// <summary>
/// PDF еженедельного отчёта: страницы РОП / РМ / СВ (A4 альбомная) и карточки ТП (A4 книжная, по 2 страницы на ТП) по модели
/// WeeklySupervisorReport.
/// </summary>
public interface IWeeklyPdfRenderer
{
    byte[] Render(WeeklySupervisorReport report, string language);

    /// <summary>Страницы того же PDF картинками PNG — для предпросмотра в интерфейсе.</summary>
    IReadOnlyList<byte[]> RenderImages(WeeklySupervisorReport report, string language, int dpi = 110);

    /// <summary>Карточки ТП: «итоги недели» и «цели на неделю» каждого ТП отчёта (у отчёта ТП — только его).</summary>
    byte[] RenderCards(WeeklySupervisorReport report, string language);

    IReadOnlyList<byte[]> RenderCardImages(WeeklySupervisorReport report, string language, int dpi = 110);
}

/// <summary>Что строит PDF-отправка: team — страницы РОП / РМ / СВ, cards — карточки ТП.</summary>
public static class WeeklyPdfKinds
{
    public const string Team = "team";
    public const string Cards = "cards";
}

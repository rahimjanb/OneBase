using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Шесть отчётов отдела продаж (ТЗ «ИИ ассистент в ТГ 2», лист «Отчёты и рассылка»).</summary>
public enum ReportType
{
    /// <summary>1. Дневной план: план на день, промежуточный итог 13:00, итог дня 18:00.</summary>
    Daily,

    /// <summary>2. Клиенты без закупа N и более дней (по умолчанию 30).</summary>
    NoPurchase,

    /// <summary>3. ТП без заказов: контроль 13:00, напоминание ТП 17:00, итог 18:00.</summary>
    NoOrders,

    /// <summary>4. Заказы с акцией.</summary>
    Promo,

    /// <summary>5. Заказы по категориям товаров.</summary>
    Categories,

    /// <summary>6. Итоги недели (пн–сб): суббота и обновление в понедельник.</summary>
    Weekly,

    /// <summary>7. «Поле боя»: карточки целей ТП — маршрут дня 08:30, полдень 13:00, итог дня 18:00, итог недели в субботу (ТЗ v2).</summary>
    Battle,

    /// <summary>
    /// 9. Накопительная акция (Twizos, ТЗ «Проект 3»): маршрут дня 08:30, полдень 13:00, итог дня 18:00 (после «Поля боя»), итог недели и рейтинг в
    /// субботу, итог месяца в последний рабочий день месяца. ТП — личная карточка, СВ — копии карточек ТП и сводка команды, РМ — сводка регионов,
    /// канал — рейтинг (получатель «РМ · вся страна» с групповым чатом). Настройки и действующая акция — incentive.Programs.
    /// </summary>
    Incentive,

    /// <summary>
    /// 8. Еженедельный PDF (понедельник, за прошлую неделю пн–сб): РОП (РМ без направления) — страна, РМ — направление, СВ — регион и команда,
    /// ТП — личная карточка. Строится WeeklyReportBuilder и рисуется WeeklyPdfRenderer — не картинками ReportEngine.
    /// </summary>
    WeeklyPdf,
}

/// <summary>
/// Запуск отчёта: код «отчёт.момент», время и дни по умолчанию, кому он (роли), сколько минут после времени его ещё можно догнать
/// после простоя сервера. MonthEnd — только в последний рабочий день месяца (пн–сб), дни недели расписания для него не важны.
/// </summary>
public sealed record ReportJobInfo(string Code, ReportType Report, string Slot, TimeOnly Time, int Days, IReadOnlyList<string> Roles, int CatchUpMinutes,
    bool MonthEnd = false)
{
    /// <summary>Промежуточный срез дня (13:00): факт — заказы до времени запуска.</summary>
    public bool IsMidday => Slot is "midday" or "reminder";

    public bool IsMorning => Slot is "morning" or "monday";
}

/// <summary>
/// Каталог запусков. Расписание, время, дни и включение каждого хранятся в telegram.ReportJobs (TelegramReportJob); здесь — что это за
/// запуск и его значения по умолчанию.
/// </summary>
public static class ReportCatalog
{
    public const string Daily = "daily";
    public const string NoPurchase = "no-purchase";
    public const string NoOrders = "no-orders";
    public const string Promo = "promo";
    public const string Categories = "categories";
    public const string Weekly = "weekly";
    public const string Battle = "battle";
    public const string Incentive = "incentive";
    public const string WeeklyPdf = "weekly-pdf";

    private static readonly IReadOnlyList<string> Everyone = TelegramReportRoles.All;

    /// <summary>«Поле боя» — ТП лично и СВ (копии карточек своих ТП); РМ на тесте не шлём.</summary>
    private static readonly IReadOnlyList<string> Field = [TelegramReportRoles.Tp, TelegramReportRoles.Sv];

    /// <summary>Рейтинг акции — в канал (РМ «вся страна» с групповым чатом) и сводками СВ и РМ; ТП лично чужих цифр не получает.</summary>
    private static readonly IReadOnlyList<string> Managers = [TelegramReportRoles.Rm, TelegramReportRoles.Sv];

    public static readonly IReadOnlyList<ReportJobInfo> Jobs =
    [
        new("daily.morning", ReportType.Daily, "morning", new(8, 30), ReportSchedules.WorkDays, Everyone, 120),
        new("daily.midday", ReportType.Daily, "midday", new(13, 0), ReportSchedules.WorkDays, Everyone, 120),
        new("daily.evening", ReportType.Daily, "evening", new(18, 0), ReportSchedules.WorkDays, Everyone, 180),
        new("no-purchase.morning", ReportType.NoPurchase, "morning", new(8, 30), ReportSchedules.WorkDays, Everyone, 120),
        new("no-orders.midday", ReportType.NoOrders, "midday", new(13, 0), ReportSchedules.WorkDays, Everyone, 90),
        new("no-orders.reminder", ReportType.NoOrders, "reminder", new(17, 0), ReportSchedules.WorkDays, [TelegramReportRoles.Tp], 45),
        new("no-orders.evening", ReportType.NoOrders, "evening", new(18, 0), ReportSchedules.WorkDays, Everyone, 180),
        new("promo.midday", ReportType.Promo, "midday", new(13, 0), ReportSchedules.WorkDays, Everyone, 120),
        new("promo.evening", ReportType.Promo, "evening", new(18, 0), ReportSchedules.WorkDays, Everyone, 180),
        new("categories.midday", ReportType.Categories, "midday", new(13, 0), ReportSchedules.WorkDays, Everyone, 120),
        new("categories.evening", ReportType.Categories, "evening", new(18, 0), ReportSchedules.WorkDays, Everyone, 180),
        new("weekly.saturday", ReportType.Weekly, "saturday", new(18, 0), ReportSchedules.Saturday, Everyone, 240),
        new("weekly.monday", ReportType.Weekly, "monday", new(8, 30), ReportSchedules.Monday, Everyone, 360),
        new("battle.morning", ReportType.Battle, "morning", new(8, 30), ReportSchedules.WorkDays, Field, 120),
        new("battle.midday", ReportType.Battle, "midday", new(13, 0), ReportSchedules.WorkDays, Field, 120),
        new("battle.evening", ReportType.Battle, "evening", new(18, 0), ReportSchedules.WorkDays, Field, 180),
        new("battle.saturday", ReportType.Battle, "saturday", new(18, 0), ReportSchedules.Saturday, Field, 240),
        // Накопительная акция — после «Поля боя»: в одно время запуски ставятся в очередь в порядке каталога, карточка акции 18:00 уходит после
        // карточки «Поля боя» (ТЗ «Проект 3», раздел 5).
        new("incentive.morning", ReportType.Incentive, "morning", new(8, 30), ReportSchedules.WorkDays, Everyone, 120),
        new("incentive.midday", ReportType.Incentive, "midday", new(13, 0), ReportSchedules.WorkDays, Everyone, 120),
        new("incentive.evening", ReportType.Incentive, "evening", new(18, 0), ReportSchedules.WorkDays, Everyone, 180),
        new("incentive.week", ReportType.Incentive, "week", new(18, 0), ReportSchedules.Saturday, Everyone, 240),
        new("incentive.rating", ReportType.Incentive, "rating", new(18, 30), ReportSchedules.Saturday, Managers, 240),
        new("incentive.month", ReportType.Incentive, "month", new(18, 0), ReportSchedules.WorkDays, Everyone, 360, MonthEnd: true),
        new("weekly-pdf.monday", ReportType.WeeklyPdf, "monday", new(9, 0), ReportSchedules.Monday, Everyone, 360),
    ];

    /// <summary>Порядок запуска в каталоге: запуски в одно время ставятся в очередь в этом порядке («Поле боя» — раньше акции).</summary>
    public static int Order(string? code)
    {
        for (var i = 0; i < Jobs.Count; i++)
        {
            if (Jobs[i].Code == code)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <summary>Запуск рисуется PDF-документом (еженедельный PDF), а не картинками.</summary>
    public static bool IsPdf(string? code) => Find(code)?.Report == ReportType.WeeklyPdf;

    public static ReportJobInfo? Find(string? code) => Jobs.FirstOrDefault(j => j.Code == code);

    public static string Key(ReportType type) => type switch
    {
        ReportType.Daily => Daily,
        ReportType.NoPurchase => NoPurchase,
        ReportType.NoOrders => NoOrders,
        ReportType.Promo => Promo,
        ReportType.Categories => Categories,
        ReportType.Battle => Battle,
        ReportType.Incentive => Incentive,
        ReportType.WeeklyPdf => WeeklyPdf,
        _ => Weekly,
    };

    /// <summary>«1. Дневной план».</summary>
    public static string Title(ReportType type) => type switch
    {
        ReportType.Daily => "1. Дневной план",
        ReportType.NoPurchase => "2. Клиенты без закупа",
        ReportType.NoOrders => "3. ТП без заказов",
        ReportType.Promo => "4. Заказы с акцией",
        ReportType.Categories => "5. Заказы по категориям",
        ReportType.Battle => "7. Поле боя",
        ReportType.Incentive => "9. Накопительная акция",
        ReportType.WeeklyPdf => "8. Еженедельный PDF",
        _ => "6. Итоги недели",
    };

    /// <summary>«13:00 — промежуточный итог»: что за момент запуска (время — из расписания).</summary>
    /// <summary>Подпись запуска с учётом отчёта: «Клиенты без закупа · 08:30 — список на день», «ТП без заказов · 13:00 — контроль».</summary>
    public static string SlotTitle(ReportType report, string slot) => (report, slot) switch
    {
        (ReportType.NoPurchase, "morning") => "список на день",
        (ReportType.NoOrders, "midday") => "контроль",
        (ReportType.Weekly, "saturday") => "неделя пн–сб",
        (ReportType.Battle, "morning") => "маршрут дня",
        (ReportType.Battle, "midday") => "полдень",
        (ReportType.Battle, "evening") => "итог дня",
        (ReportType.Battle, "saturday") => "итог недели",
        (ReportType.Incentive, "morning") => "маршрут дня",
        (ReportType.Incentive, "midday") => "полдень",
        (ReportType.Incentive, "evening") => "итог дня",
        (ReportType.Incentive, "week") => "итог недели",
        (ReportType.Incentive, "rating") => "рейтинг акции",
        (ReportType.Incentive, "month") => "итог месяца",
        (ReportType.WeeklyPdf, "monday") => "PDF за прошлую неделю",
        _ => SlotTitle(slot),
    };

    public static string SlotTitle(string slot) => slot switch
    {
        "morning" => "план на день",
        "midday" => "промежуточный итог",
        "reminder" => "напоминание ТП",
        "evening" => "итог дня",
        "saturday" => "итоги недели",
        "monday" => "обновлённые итоги прошлой недели",
        _ => slot,
    };

    /// <summary>Коды запусков из строки «a,b,c»: только известные, без повторов.</summary>
    public static IReadOnlyList<string> Parse(string? codes) =>
        (codes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(c => Find(c) is not null).Distinct().ToList();

    /// <summary>Запуски, которые подходят роли (напоминание 17:00 — только ТП).</summary>
    public static IReadOnlyList<string> ForRole(IEnumerable<string> codes, string role) =>
        codes.Where(c => Find(c) is { } j && j.Roles.Contains(role)).Distinct().ToList();

    /// <summary>Расписание по умолчанию для кода (выключено: включает администратор).</summary>
    public static TelegramReportJob Default(ReportJobInfo job) => new()
    {
        Code = job.Code,
        Enabled = false,
        Time = job.Time,
        TimeZone = ReportSchedules.DefaultTimeZone,
        Days = job.Days,
        CatchUpMinutes = job.CatchUpMinutes,
    };
}

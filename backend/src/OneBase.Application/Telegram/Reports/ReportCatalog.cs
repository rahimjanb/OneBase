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
}

/// <summary>
/// Запуск отчёта: код «отчёт.момент», время и дни по умолчанию, кому он (роли), сколько минут после времени его ещё можно догнать
/// после простоя сервера.
/// </summary>
public sealed record ReportJobInfo(string Code, ReportType Report, string Slot, TimeOnly Time, int Days, IReadOnlyList<string> Roles, int CatchUpMinutes)
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

    private static readonly IReadOnlyList<string> Everyone = TelegramReportRoles.All;

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
    ];

    public static ReportJobInfo? Find(string? code) => Jobs.FirstOrDefault(j => j.Code == code);

    public static string Key(ReportType type) => type switch
    {
        ReportType.Daily => Daily,
        ReportType.NoPurchase => NoPurchase,
        ReportType.NoOrders => NoOrders,
        ReportType.Promo => Promo,
        ReportType.Categories => Categories,
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
        _ => "6. Итоги недели",
    };

    /// <summary>«13:00 — промежуточный итог»: что за момент запуска (время — из расписания).</summary>
    /// <summary>Подпись запуска с учётом отчёта: «Клиенты без закупа · 08:30 — список на день», «ТП без заказов · 13:00 — контроль».</summary>
    public static string SlotTitle(ReportType report, string slot) => (report, slot) switch
    {
        (ReportType.NoPurchase, "morning") => "список на день",
        (ReportType.NoOrders, "midday") => "контроль",
        (ReportType.Weekly, "saturday") => "неделя пн–сб",
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

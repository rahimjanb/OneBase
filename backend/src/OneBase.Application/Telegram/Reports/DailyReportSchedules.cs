using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Запуск по расписанию: день отчёта (в часовом поясе расписания) и момент, когда он наступил.</summary>
public sealed record ReportSlot(DateOnly Period, DateTimeOffset At);

/// <summary>
/// Правила расписаний автоотчётов (чистые функции): время по умолчанию 08:00, 13:00, 18:00 (Ташкент), дни недели битами (пн = 1 … вс = 64),
/// когда запуск наступил и сколько его можно догонять после простоя сервера.
/// </summary>
public static class DailyReportSchedules
{
    public const string DefaultTimeZone = "Asia/Tashkent";

    /// <summary>Пн–сб: в воскресенье агенты не работают (как в рабочих днях плана Sales Base).</summary>
    public const int WorkDays = 0b0111111;

    public const int AllDays = 0b1111111;

    /// <summary>
    /// Сервер был выключен во время запуска — отчёт уходит позже, но не позже этого: утренний план к обеду уже не нужен, а итоги дня ночью
    /// только путают.
    /// </summary>
    public static readonly TimeSpan CatchUp = TimeSpan.FromHours(2);

    public static readonly IReadOnlyDictionary<TelegramReportKind, TimeOnly> DefaultTimes = new Dictionary<TelegramReportKind, TimeOnly>
    {
        [TelegramReportKind.Morning] = new(8, 0),
        [TelegramReportKind.Midday] = new(13, 0),
        [TelegramReportKind.Evening] = new(18, 0),
    };

    public static TelegramReportSchedule Default(TelegramReportKind kind) => new()
    {
        Kind = kind,
        Enabled = false,
        Time = DefaultTimes[kind],
        TimeZone = DefaultTimeZone,
        Days = WorkDays,
        Metric = "sum",
    };

    /// <summary>Бит дня недели: пн = 1, вт = 2, … вс = 64.</summary>
    public static int Bit(DayOfWeek day) => 1 << (((int)day + 6) % 7);

    public static bool Includes(int days, DayOfWeek day) => (days & Bit(day)) != 0;

    /// <summary>Дни недели битами из списка 1–7 (пн–вс).</summary>
    public static int DaysOf(IEnumerable<int> isoDays) => isoDays.Where(d => d is >= 1 and <= 7).Distinct().Aggregate(0, (acc, d) => acc | (1 << (d - 1)));

    public static IReadOnlyList<int> IsoDays(int days) => Enumerable.Range(1, 7).Where(d => (days & (1 << (d - 1))) != 0).ToList();

    /// <summary>Часовой пояс по имени IANA (Asia/Tashkent); null — неизвестен.</summary>
    public static TimeZoneInfo? Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>Местное время расписания (без пояса — Ташкент).</summary>
    public static DateTimeOffset Local(DateTimeOffset utc, string? timeZone) =>
        Zone(timeZone) is { } zone ? TimeZoneInfo.ConvertTime(utc, zone) : utc.ToOffset(TimeSpan.FromHours(5));

    private static DateTimeOffset At(DateOnly day, TimeOnly time, string? timeZone)
    {
        var zone = Zone(timeZone);
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        var offset = zone?.GetUtcOffset(local) ?? TimeSpan.FromHours(5);
        return new DateTimeOffset(local, offset);
    }

    /// <summary>
    /// Запуск, который пора выполнить сейчас: день недели в расписании, время наступило не раньше включения (ArmedAt) и не дольше
    /// <see cref="CatchUp"/> назад. null — сейчас отправлять нечего.
    /// </summary>
    public static ReportSlot? Due(TelegramReportSchedule schedule, DateTimeOffset nowUtc)
    {
        if (!schedule.Enabled || schedule.ArmedAt is not { } armed)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(Local(nowUtc, schedule.TimeZone).DateTime);
        foreach (var day in new[] { today, today.AddDays(-1) })
        {
            var at = At(day, schedule.Time, schedule.TimeZone);
            if (at <= nowUtc && nowUtc - at <= CatchUp && at >= armed && Includes(schedule.Days, day.DayOfWeek))
            {
                return new ReportSlot(day, at);
            }
        }

        return null;
    }

    /// <summary>Следующий запуск (для интерфейса); null — расписание выключено или дни не выбраны.</summary>
    public static DateTimeOffset? Next(TelegramReportSchedule schedule, DateTimeOffset nowUtc)
    {
        if (!schedule.Enabled || schedule.Days == 0)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(Local(nowUtc, schedule.TimeZone).DateTime);
        for (var i = 0; i <= 7; i++)
        {
            var day = today.AddDays(i);
            var at = At(day, schedule.Time, schedule.TimeZone);
            if (at > nowUtc && Includes(schedule.Days, day.DayOfWeek))
            {
                return at;
            }
        }

        return null;
    }

    /// <summary>«08:00», «8:30» → время; иначе null.</summary>
    public static TimeOnly? ParseTime(string? text) =>
        TimeOnly.TryParseExact(text?.Trim(), ["H:mm", "HH:mm"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t) ? t : null;
}

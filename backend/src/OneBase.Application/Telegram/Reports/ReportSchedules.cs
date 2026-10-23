using System.Globalization;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Запуск по расписанию: день отчёта (в часовом поясе расписания) и момент, когда он наступил.</summary>
public sealed record ReportSlot(DateOnly Period, DateTimeOffset At);

/// <summary>
/// Правила расписаний автоотчётов (чистые функции): дни недели битами (пн = 1 … вс = 64), когда запуск наступил, сколько его можно
/// догонять после простоя (CatchUpMinutes запуска) и следующий запуск. Часовой пояс — IANA, по умолчанию Asia/Tashkent.
/// </summary>
public static class ReportSchedules
{
    public const string DefaultTimeZone = "Asia/Tashkent";

    /// <summary>Пн–сб: в воскресенье агенты не работают (рабочие дни плана — пн–сб).</summary>
    public const int WorkDays = 0b0111111;

    public const int AllDays = 0b1111111;
    public const int Monday = 0b0000001;
    public const int Saturday = 0b0100000;

    /// <summary>Догонять не дольше суток: «вчерашний» отчёт после долгого простоя не уходит.</summary>
    public const int MaxCatchUpMinutes = 24 * 60;

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

    /// <summary>Местное время расписания (без пояса — Ташкент, UTC+5).</summary>
    public static DateTimeOffset Local(DateTimeOffset utc, string? timeZone) =>
        Zone(timeZone) is { } zone ? TimeZoneInfo.ConvertTime(utc, zone) : utc.ToOffset(TimeSpan.FromHours(5));

    /// <summary>Момент «день + время» в часовом поясе.</summary>
    public static DateTimeOffset At(DateOnly day, TimeOnly time, string? timeZone)
    {
        var zone = Zone(timeZone);
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        var offset = zone?.GetUtcOffset(local) ?? TimeSpan.FromHours(5);
        return new DateTimeOffset(local, offset);
    }

    /// <summary>
    /// Запуск, который пора выполнить сейчас: день недели в расписании, время наступило не раньше включения (ArmedAt) и не дольше
    /// CatchUpMinutes назад. null — сейчас отправлять нечего.
    /// </summary>
    public static ReportSlot? Due(TelegramReportJob job, DateTimeOffset nowUtc)
    {
        if (!job.Enabled || job.ArmedAt is not { } armed)
        {
            return null;
        }

        var catchUp = TimeSpan.FromMinutes(Math.Clamp(job.CatchUpMinutes, 0, MaxCatchUpMinutes));
        var today = DateOnly.FromDateTime(Local(nowUtc, job.TimeZone).DateTime);
        foreach (var day in new[] { today, today.AddDays(-1) })
        {
            var at = At(day, job.Time, job.TimeZone);
            if (at <= nowUtc && nowUtc - at <= catchUp && at >= armed && Includes(job.Days, day.DayOfWeek))
            {
                return new ReportSlot(day, at.ToUniversalTime()); // в базу — только UTC
            }
        }

        return null;
    }

    /// <summary>
    /// Запуск, который сервер пропустил: время наступило после включения, но прошло больше CatchUpMinutes — отчёт уже не отправляется,
    /// в журнал запусков — «пропущен». null — пропущенного нет.
    /// </summary>
    public static ReportSlot? Missed(TelegramReportJob job, DateTimeOffset nowUtc)
    {
        if (!job.Enabled || job.ArmedAt is not { } armed)
        {
            return null;
        }

        var catchUp = TimeSpan.FromMinutes(Math.Clamp(job.CatchUpMinutes, 0, MaxCatchUpMinutes));
        var today = DateOnly.FromDateTime(Local(nowUtc, job.TimeZone).DateTime);
        foreach (var day in new[] { today, today.AddDays(-1) })
        {
            var at = At(day, job.Time, job.TimeZone);
            if (at <= nowUtc && nowUtc - at > catchUp && at >= armed && Includes(job.Days, day.DayOfWeek))
            {
                return new ReportSlot(day, at.ToUniversalTime()); // в базу — только UTC
            }
        }

        return null;
    }

    /// <summary>Следующий запуск (для интерфейса); null — расписание выключено или дни не выбраны.</summary>
    public static DateTimeOffset? Next(TelegramReportJob job, DateTimeOffset nowUtc)
    {
        if (!job.Enabled || job.Days == 0)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(Local(nowUtc, job.TimeZone).DateTime);
        for (var i = 0; i <= 7; i++)
        {
            var day = today.AddDays(i);
            var at = At(day, job.Time, job.TimeZone);
            if (at > nowUtc && Includes(job.Days, day.DayOfWeek))
            {
                return at;
            }
        }

        return null;
    }

    /// <summary>«08:00», «8:30» → время; иначе null.</summary>
    public static TimeOnly? ParseTime(string? text) =>
        TimeOnly.TryParseExact(text?.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

    /// <summary>
    /// Неделя отчёта «Итоги недели» (пн–сб): у субботы и любого дня пн–сб — неделя этого дня; у понедельника утреннего запуска
    /// (обновлённые итоги) — прошлая неделя; у воскресенья — завершившаяся неделя.
    /// </summary>
    public static (DateOnly Monday, DateOnly Saturday) Week(DateOnly day, bool previous)
    {
        var shift = ((int)day.DayOfWeek + 6) % 7; // пн = 0 … вс = 6
        var monday = day.AddDays(-shift);
        if (previous)
        {
            monday = monday.AddDays(-7);
        }

        return (monday, monday.AddDays(5));
    }

    /// <summary>Рабочие дни (пн–сб) отрезка.</summary>
    public static IEnumerable<DateOnly> WorkingDays(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Sunday)
            {
                yield return d;
            }
        }
    }
}

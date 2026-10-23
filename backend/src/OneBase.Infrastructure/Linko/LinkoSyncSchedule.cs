using OneBase.Application.Sales;
using OneBase.Domain.Sales;

namespace OneBase.Infrastructure.Linko;

/// <summary>
/// Правила автоматического обмена с Linko без людей: когда делать ежедневную сверку, когда изменение задевает давние месяцы (их кэш живёт
/// дольше) и когда обмен считать сломанным — данные устарели. Время — ташкентское (UTC+5), сервер может жить в UTC.
/// </summary>
public static class LinkoSyncSchedule
{
    public static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    /// <summary>Строка состояния ежедневной сверки (LastSuccessAt — когда была последняя).</summary>
    public const string ReconcileState = "reconcile";

    /// <summary>Строка состояния контроля обмена: LastError — текст открытой проблемы, по которой уже ушло уведомление.</summary>
    public const string AlertState = "alert";

    /// <summary>Данные, без которых отчёты врут: документы, остатки и перемещения — с подписью для уведомления.</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyEntities = new Dictionary<string, string>
    {
        ["orders"] = "заказы",
        ["order_returns"] = "возвраты",
        ["visits"] = "визиты",
        ["product_balances"] = "остатки",
        ["stock_transfers"] = "перемещения",
        ["reservations"] = "бронь",
    };

    /// <summary>Время запусков из Sales:Sync:Times («11:00», «17:00») — по Ташкенту, по порядку, без повторов; неразборчивое отбрасывается.</summary>
    public static IReadOnlyList<TimeOnly> ParseTimes(IEnumerable<string>? times) =>
        (times ?? [])
            .Select(t => TimeOnly.TryParseExact(t?.Trim(), ["H:mm", "HH:mm"], System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var v) ? v : (TimeOnly?)null)
            .OfType<TimeOnly>()
            .Distinct()
            .Order()
            .ToList();

    /// <summary>
    /// Расписание синхронизации: каждые Sales:Sync:IntervalMinutes минут от 00:00 по Ташкенту (5–720; 0 — без интервала) и время из
    /// Sales:Sync:Times — вместе, по порядку, без повторов.
    /// </summary>
    public static IReadOnlyList<TimeOnly> TimesOf(SalesSyncOptions sync)
    {
        var times = ParseTimes(sync.Times).ToList();
        if (sync.IntervalMinutes > 0)
        {
            var step = Math.Clamp(sync.IntervalMinutes, 5, 720);
            for (var minute = 0; minute < 24 * 60; minute += step)
            {
                times.Add(new TimeOnly(minute / 60, minute % 60));
            }
        }

        return times.Distinct().Order().ToList();
    }

    /// <summary>Ближайший запуск по расписанию строго после <paramref name="nowUtc"/>.</summary>
    public static DateTimeOffset NextRun(DateTimeOffset nowUtc, IReadOnlyList<TimeOnly> times)
    {
        var today = DateOnly.FromDateTime(nowUtc.ToOffset(CompanyOffset).DateTime);
        foreach (var day in new[] { today, today.AddDays(1) })
        {
            foreach (var t in times)
            {
                var at = new DateTimeOffset(day.ToDateTime(t), CompanyOffset);
                if (at > nowUtc)
                {
                    return at;
                }
            }
        }

        throw new InvalidOperationException("Расписание синхронизации пустое.");
    }

    /// <summary>Последний наступивший запуск по расписанию (не позже <paramref name="nowUtc"/>).</summary>
    public static DateTimeOffset LastSlot(DateTimeOffset nowUtc, IReadOnlyList<TimeOnly> times)
    {
        var today = DateOnly.FromDateTime(nowUtc.ToOffset(CompanyOffset).DateTime);
        foreach (var day in new[] { today, today.AddDays(-1) })
        {
            foreach (var t in times.Reverse())
            {
                var at = new DateTimeOffset(day.ToDateTime(t), CompanyOffset);
                if (at <= nowUtc)
                {
                    return at;
                }
            }
        }

        throw new InvalidOperationException("Расписание синхронизации пустое.");
    }

    /// <summary>
    /// Запуск по расписанию пропущен: после последнего наступившего времени данные не обновлялись (сервер был выключен или запуск не прошёл) —
    /// синхронизацию нужно догнать сразу, а не ждать следующего времени.
    /// </summary>
    public static bool Missed(DateTimeOffset nowUtc, DateTimeOffset? lastSuccess, IReadOnlyList<TimeOnly> times) =>
        times.Count > 0 && (lastSuccess is null || lastSuccess < LastSlot(nowUtc, times));

    /// <summary>Самый долгий промежуток между запусками (с переходом через полночь): дольше него без обновления данные устарели.</summary>
    public static TimeSpan LongestGap(IReadOnlyList<TimeOnly> times)
    {
        if (times.Count == 0)
        {
            return TimeSpan.FromDays(1);
        }

        var gaps = times.Zip(times.Skip(1), (a, b) => b - a).Append(TimeSpan.FromDays(1) - (times[^1] - times[0]));
        return gaps.Max();
    }

    /// <summary>Ежедневная сверка: раз в сутки, после <paramref name="hour"/> часов по Ташкенту, если сегодня её ещё не было.</summary>
    public static bool ReconcileDue(DateTimeOffset nowUtc, DateTimeOffset? lastReconcile, int hour)
    {
        var local = nowUtc.ToOffset(CompanyOffset);
        if (local.Hour < Math.Clamp(hour, 0, 23))
        {
            return false;
        }

        return lastReconcile is null || DateOnly.FromDateTime(lastReconcile.Value.ToOffset(CompanyOffset).DateTime) < DateOnly.FromDateTime(local.DateTime);
    }

    /// <summary>
    /// Изменение старше прошлого месяца задевает кэш давних месяцев (АКБ с начала года считается из него и живёт часами) — его нужно
    /// сбросить сразу, а не ждать полной загрузки. Прошлый и текущий месяц считаются из строк продаж и сбрасываются при любом обновлении.
    /// </summary>
    public static bool TouchesHistory(DateOnly? earliestChanged, DateTimeOffset nowUtc)
    {
        if (earliestChanged is not { } earliest)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(nowUtc.ToOffset(CompanyOffset).DateTime);
        return earliest < new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
    }

    /// <summary>
    /// Проблема обмена для уведомления: какие ключевые данные не обновлялись дольше <paramref name="staleAfter"/> (и почему, если
    /// Linko вернул ошибку), или сменился сервер Linko. null — обмен работает.
    /// </summary>
    public static string? Problem(IReadOnlyCollection<LinkoSyncState> states, DateTimeOffset nowUtc, TimeSpan staleAfter)
    {
        if (states.FirstOrDefault(s => s.Entity == LinkoSyncService.SourceState) is { LastError: { Length: > 0 } source })
        {
            return source;
        }

        var stale = KeyEntities
            .Select(e => (Label: e.Value, State: states.FirstOrDefault(s => s.Entity == e.Key)))
            .Where(x => x.State?.LastSuccessAt is not { } at || nowUtc - at > staleAfter)
            .ToList();
        if (stale.Count == 0)
        {
            return null;
        }

        var since = stale.Select(x => x.State?.LastSuccessAt).Where(x => x is not null).Min();
        var head = since is { } s
            ? $"Данные Linko не обновлялись с {s.ToOffset(CompanyOffset):dd.MM.yyyy HH:mm} (Ташкент): {string.Join(", ", stale.Select(x => x.Label))}."
            : $"Данные Linko ещё ни разу не загрузились: {string.Join(", ", stale.Select(x => x.Label))}.";
        var errors = stale.Where(x => !string.IsNullOrWhiteSpace(x.State?.LastError)).Select(x => $"{x.Label} — {x.State!.LastError}").Distinct().Take(3).ToList();
        return errors.Count == 0 ? head : $"{head} Ошибка: {string.Join("; ", errors)}";
    }
}

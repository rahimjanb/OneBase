using OneBase.Domain.Sales;

namespace OneBase.Infrastructure.Linko;

/// <summary>
/// Правила автоматического обмена с Linko без людей: когда делать ночную сверку, когда изменение задевает давние месяцы (их кэш живёт
/// дольше) и когда обмен считать сломанным — данные устарели. Время — ташкентское (UTC+5), сервер может жить в UTC.
/// </summary>
public static class LinkoSyncSchedule
{
    public static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    /// <summary>Строка состояния ночной сверки (LastSuccessAt — когда была последняя).</summary>
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
    };

    /// <summary>Ночная сверка: раз в сутки, после <paramref name="hour"/> часов по Ташкенту, если сегодня её ещё не было.</summary>
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

using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Hr;

namespace OneBase.Application.Analytik.Readiness;

/// <summary>Состояние набора данных для Аналитика.</summary>
public static class ReadinessStatus
{
    /// <summary>Данные свежие.</summary>
    public const string Ok = "ok";

    /// <summary>Сейчас идёт загрузка из источника.</summary>
    public const string Updating = "updating";

    /// <summary>Устарели: обновление по расписанию не прошло или автоматический обмен выключен.</summary>
    public const string Stale = "stale";

    /// <summary>Устарели: источник отвечает ошибкой.</summary>
    public const string Error = "error";

    /// <summary>Интеграция не подключена или выключена — данные сами не обновятся.</summary>
    public const string NotConnected = "not_connected";

    /// <summary>Данных за текущий месяц нет (планы продаж не заведены).</summary>
    public const string Missing = "missing";
}

/// <summary>Откуда приходят данные.</summary>
public static class SourceCodes
{
    public const string Linko = "linko";
    public const string Verifix = "verifix";

    /// <summary>Ведутся в самом OneBase (производство, склад, снабжение) — всегда актуальны.</summary>
    public const string OneBase = "onebase";
}

/// <summary>
/// Подключение внешней системы, из которой OneBase загружает данные: подключено ли, обновляется ли само по расписанию, идёт ли загрузка сейчас,
/// через сколько без обновления данные считаются устаревшими, где настройки и кто может запустить обновление (права через «|»).
/// </summary>
public sealed record SourceLink(string Code, string Title, bool Connected, bool AutoSync, bool Running, TimeSpan StaleAfter, string SettingsHref,
    string? Problem, string ManagePermission);

public enum SyncStartResult
{
    Started,
    AlreadyRunning,
    NotConnected,
    Failed,
}

/// <summary>Подключения к внешним системам для Аналитика (реализация — в инфраструктуре: Linko и Verifix).</summary>
public interface IAnalytikSourceSync
{
    Task<IReadOnlyList<SourceLink>> LinksAsync(CancellationToken ct);

    /// <summary>Запускает обновление в фоне и сразу отвечает.</summary>
    Task<SyncStartResult> StartAsync(string code, CancellationToken ct);

    /// <summary>
    /// Сообщает тем, кто может исправить подключение, что данные устарели и сами не обновятся (<paramref name="cause"/> — почему): один раз на
    /// проблему; когда данные снова свежие — сообщение о восстановлении. true — уведомление отправлено.
    /// </summary>
    Task<bool> AlertAsync(string code, string cause, CancellationToken ct);
}

/// <summary>
/// Готовность набора данных: статус, когда последний раз обновлён, отстаёт ли (дольше допустимого без обновления), почему и что система уже
/// сделала. Lagging — главное для ответа: данные источника есть только по UpdatedAt.
/// </summary>
public sealed record DatasetReadiness(string Code, string Title, string Short, string Source, string Module, string? RequiredPermission, string Status,
    DateTimeOffset? UpdatedAt, bool Lagging, string? Reason, string? Action, string? Href);

public sealed record ReadinessReport(DateTimeOffset CheckedAt, IReadOnlyList<SourceLink> Links, IReadOnlyList<DatasetReadiness> Datasets)
{
    /// <summary>Наборы, которые пользователь видит по правам (как находки Аналитика: право набора и область продаж).</summary>
    public IReadOnlyList<DatasetReadiness> Visible(AnalytikPrincipal p) =>
        Datasets.Where(d => InsightStore.Visible(d.RequiredPermission, d.Module, p)).ToList();

    public SourceLink? Link(string code) => Links.FirstOrDefault(l => l.Code == code);

    /// <summary>Может ли пользователь исправить подключение источника (кнопка «Обновить», ссылка на настройки).</summary>
    public bool CanManage(string code, AnalytikPrincipal p) => Link(code) is { } link && DataReadiness.CanManage(link, p);
}

/// <summary>Месяц, за который данные источника есть, — предложение показать его вместо периода без данных.</summary>
public sealed record LatestPeriod(string Label, DateOnly From, DateOnly To);

/// <summary>Нехватка данных в выбранном периоде: что не загружено, почему, что сделано, где исправить и какой период показать вместо.</summary>
public sealed record DataGap(string Source, string Text, string? Action, string? Href, LatestPeriod? Latest);

/// <summary>
/// Готовность данных для Аналитика: по каждому набору (продажи, оплаты, остатки, планы — из Linko; посещаемость — из Verifix; производство и склад —
/// из OneBase) — свежесть, причина отставания простыми словами и что уже сделано (запущено обновление, уведомлены администраторы). Отсюда берут
/// панель, ответы AI-аналитика и фоновый контроль (DataKeeper) — чтобы вопрос о периоде без данных не заканчивался «данных нет».
/// </summary>
public sealed class DataReadiness(IAppDbContext db, IAnalytikSourceSync sources, DataKeeperState keeper, TimeProvider clock)
{
    /// <summary>Сколько после запуска обновления считать его идущим, пока синхронизация не заняла очередь.</summary>
    public static readonly TimeSpan StartGrace = TimeSpan.FromSeconds(10);

    /// <summary>Строка контроля обмена Linko: LastError — открытая проблема, по которой ушло уведомление, LastRunAt — когда.</summary>
    public const string LinkoAlertState = "alert";

    /// <summary>Строка источника Linko: LastError — обмен целиком остановлен (сменился сервер Linko и т. п.).</summary>
    public const string LinkoSourceState = "source";

    /// <summary>Наборы Linko: код, название, коротко (для перечислений), сущность синхронизации, раздел, право.</summary>
    internal static readonly (string Code, string Title, string Short, string Entity, string Module, string Permission)[] LinkoSets =
    [
        ("sales", "Продажи: заказы, возвраты, визиты", "продажи", "orders", "sales", Permissions.SalesRead),
        ("payments", "Оплаты точек", "оплаты", "payments", "finance", Permissions.FinanceRead),
        ("stock", "Остатки у дилеров и на складах", "остатки", "product_balances", "sales", Permissions.SalesRead),
    ];

    private static readonly string[] Months =
        ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    public async Task<ReadinessReport> CheckAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // Только что запущенное обновление уже «идёт»: фоновая задача синхронизации могла ещё не занять очередь — без этого ответ не дождался бы
        // обновления, а следующий проход запустил бы его второй раз.
        var links = (await sources.LinksAsync(ct))
            .Select(l => !l.Running && keeper.Last(l.Code) is { Result: SyncStartResult.Started } a && now - a.At < StartGrace ? l with { Running = true } : l)
            .ToList();
        var sets = new List<DatasetReadiness>();

        if (links.FirstOrDefault(l => l.Code == SourceCodes.Linko) is { } linko)
        {
            var states = await db.LinkoSyncStates.AsNoTracking().ToListAsync(ct);
            var alert = states.FirstOrDefault(s => s.Entity == LinkoAlertState);
            // Отказ всего обмена (сменился сервер Linko и т. п.) записан в строке источника — это причина для всех наборов.
            var sourceError = states.FirstOrDefault(s => s.Entity == LinkoSourceState)?.LastError;
            var action = ActionText(linko, keeper.Last(linko.Code), alert?.LastError is { Length: > 0 } ? alert.LastRunAt : null);
            foreach (var d in LinkoSets)
            {
                var state = states.FirstOrDefault(s => s.Entity == d.Entity);
                sets.Add(Evaluate(d.Code, d.Title, d.Short, d.Module, d.Permission, linko, state?.LastSuccessAt,
                    sourceError is { Length: > 0 } ? sourceError : state?.LastError, action, now));
            }

            var sales = sets.First(d => d.Code == "sales");
            if (await PlansAsync(linko, sales.Lagging ? sales.UpdatedAt : null, now, ct) is { } plans)
            {
                sets.Add(plans);
            }
        }

        if (links.FirstOrDefault(l => l.Code == SourceCodes.Verifix) is { } verifix)
        {
            var runs = db.HrSyncRuns.AsNoTracking();
            var success = await runs.Where(r => r.Status == HrSyncStatus.Succeeded || r.Status == HrSyncStatus.Partial).MaxAsync(r => r.FinishedAt, ct);
            // Verifix, который ни разу не подключали, — не повод для предупреждений: в компании посещаемость может не вестись.
            if (verifix.Connected || success is not null)
            {
                var failed = await runs.Where(r => r.Status == HrSyncStatus.Failed && (success == null || r.StartedAt > success))
                    .OrderByDescending(r => r.StartedAt).Select(r => r.Message).FirstOrDefaultAsync(ct);
                sets.Add(Evaluate("hr", "Посещаемость и опоздания", "посещаемость", "hr", Permissions.HrRead, verifix, success, failed,
                    ActionText(verifix, keeper.Last(verifix.Code), null), now));
            }
        }

        sets.Add(new DatasetReadiness("onebase", "Производство, склад сырья, снабжение", "производство и склад", SourceCodes.OneBase, "production", null,
            ReadinessStatus.Ok, now, false, null, null, null));
        return new ReadinessReport(now, links, sets);
    }

    /// <summary>Статус набора из внешней системы по времени последнего удачного обновления и ошибке последней попытки.</summary>
    public static DatasetReadiness Evaluate(string code, string title, string shortName, string module, string? permission, SourceLink link,
        DateTimeOffset? success, string? error, string? action, DateTimeOffset now)
    {
        var lagging = success is not { } at || now - at > link.StaleAfter;
        var (status, reason) =
            !link.Connected ? (ReadinessStatus.NotConnected, link.Problem ?? $"{link.Title} не подключён.")
            : link.Running ? (ReadinessStatus.Updating, lagging ? $"Идёт загрузка свежих данных из {link.Title}." : null)
            : !lagging ? (ReadinessStatus.Ok, (string?)null)
            : error is { Length: > 0 } ? (ReadinessStatus.Error, $"{link.Title} отвечает ошибкой: {Short(error, 200)}")
            : !link.AutoSync ? (ReadinessStatus.Stale, $"Автоматический обмен с {link.Title} выключен — данные обновляются только кнопкой «Обновить».")
            : (ReadinessStatus.Stale, $"Обновление из {link.Title} по расписанию не прошло.");
        return new DatasetReadiness(code, title, shortName, link.Code, module, permission, status, success, lagging, reason, lagging ? action : null,
            link.SettingsHref);
    }

    /// <summary>Что система уже сделала по отставанию источника: запуск обновления Аналитиком и уведомление администраторов.</summary>
    public static string? ActionText(SourceLink link, KeeperAttempt? attempt, DateTimeOffset? alertedAt)
    {
        var parts = new List<string>();
        if (attempt is { } a)
        {
            parts.Add(a.Result switch
            {
                SyncStartResult.Started => $"Аналитик запустил обновление {Local(a.At)}",
                SyncStartResult.AlreadyRunning => $"обновление уже шло {Local(a.At)}",
                SyncStartResult.Failed => $"Аналитик не смог запустить обновление {Local(a.At)}",
                _ => $"обновление не запущено {Local(a.At)}: {link.Title} не подключён",
            });
        }

        if (alertedAt is { } at)
        {
            parts.Add($"администраторы интеграций уведомлены {Local(at)}");
        }

        if (!link.Connected || !link.AutoSync)
        {
            parts.Add(link.Connected
                ? "после включения обмена данные загрузятся автоматически"
                : "после подключения данные догрузятся автоматически");
        }

        if (parts.Count == 0)
        {
            return null;
        }

        var text = string.Join("; ", parts);
        return char.ToUpperInvariant(text[0]) + text[1..] + ".";
    }

    /// <summary>
    /// Планы продаж на текущий месяц: есть ли хоть один план (регионов, «Завод», ТП, Linko). Набор показывается, только если планы ведутся —
    /// в прошлом месяце они были. Без плана выполнение и прогноз выполнения не считаются.
    /// </summary>
    private async Task<DatasetReadiness?> PlansAsync(SourceLink linko, DateTimeOffset? linkoLagsSince, DateTimeOffset now, CancellationToken ct)
    {
        var today = FieldClock.LocalDate(now);
        var previous = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var title = $"Планы продаж на {Months[today.Month - 1]}";
        var (own, fromLinko) = await PlansOfAsync(today.Year, today.Month, ct);
        if (own || fromLinko)
        {
            return new DatasetReadiness("plans", title, "планы", SourceCodes.Linko, "sales", Permissions.SalesRead, ReadinessStatus.Ok, null, false, null, null,
                "/sales");
        }

        var (ownBefore, linkoBefore) = await PlansOfAsync(previous.Year, previous.Month, ct);
        if (!ownBefore && !linkoBefore)
        {
            return null;
        }

        var month = Months[today.Month - 1];
        // Планы прошлого месяца пришли из Linko, а Linko отстаёт, — планы не «не заведены», а ещё не загружены.
        var reason = linkoBefore && !ownBefore && (!linko.Connected || linkoLagsSince is not null)
            ? $"Планы на {month} приходят из Linko и появятся после загрузки его данных{(linkoLagsSince is { } t ? $" (сейчас данные Linko по {Local(t)})" : "")}. " +
              $"Выполнение плана за {month} пока не считается."
            : $"Планы на {month} ещё не заведены — ни в OneBase («Продажи → Планы»), ни в Linko. Выполнение плана и его прогноз не считаются.";
        return new DatasetReadiness("plans", title, "планы", SourceCodes.Linko, "sales", Permissions.SalesRead, ReadinessStatus.Missing, null, true, reason,
            null, "/sales");
    }

    private async Task<(bool Own, bool Linko)> PlansOfAsync(int year, int month, CancellationToken ct)
    {
        var own = await db.SalesRegionPlans.AnyAsync(p => p.Year == year && p.Month == month && p.PlanKg > 0, ct)
                  || await db.SalesFactoryPlans.AnyAsync(p => p.Year == year && p.Month == month, ct);
        var linko = await db.SalesStaffPlans.AnyAsync(p => p.Year == year && p.Month == month, ct)
                    || await db.LinkoKpiPlans.AnyAsync(p => p.Year == year && p.Month == month, ct);
        return (own, linko);
    }

    /// <summary>
    /// Нехватка данных в периоде: по источнику — наборы, которые отстают и обновлялись раньше конца периода (или сейчас, если период ещё идёт).
    /// Период целиком позже данных — предлагается месяц, за который данные есть. Планы — отдельно, если период захватывает текущий месяц.
    /// Ссылка на настройки подключения — только тем, кто может его исправить (<paramref name="canManage"/> по коду источника).
    /// </summary>
    public static IReadOnlyList<DataGap> Gaps(IEnumerable<DatasetReadiness> visible, AnalytikPeriod period, DateTimeOffset now, Func<string, bool>? canManage = null)
    {
        var today = FieldClock.LocalDate(now);
        var end = period.To < today ? FieldClock.DayStartUtc(period.To.AddDays(1)) : now;
        var gaps = new List<DataGap>();
        var list = visible.ToList();
        foreach (var source in list.Where(d => d.Lagging && d.Status != ReadinessStatus.Missing && d.Source != SourceCodes.OneBase && (d.UpdatedAt is null || d.UpdatedAt < end))
                     .GroupBy(d => d.Source))
        {
            var sets = source.ToList();
            var names = Join(sets.Select(d => d.Short).Distinct().ToList());
            var through = sets.Min(d => d.UpdatedAt);
            var first = sets[0];
            var none = through is null || FieldClock.LocalDate(through.Value) < period.From;
            var head = through is not { } t
                ? $"{Cap(names)}: данных в OneBase ещё нет."
                : none
                    ? $"{Cap(names)} за {period.Label} ещё не загружены: в OneBase данные по {Local(t)}."
                    : $"{Cap(names)} за {period.Label} неполные: данные по {Local(t)}.";
            LatestPeriod? latest = null;
            if (none && through is { } last)
            {
                var day = FieldClock.LocalDate(last);
                var from = new DateOnly(day.Year, day.Month, 1);
                latest = new LatestPeriod($"{Months[day.Month - 1]} {day.Year}", from, from.AddMonths(1).AddDays(-1));
            }

            gaps.Add(new DataGap(source.Key, $"{head} {first.Reason}".Trim(), first.Action, canManage?.Invoke(source.Key) == true ? first.Href : null, latest));
        }

        if (list.FirstOrDefault(d => d.Code == "plans" && d.Status == ReadinessStatus.Missing) is { } plans
            && period.From <= today && period.To >= new DateOnly(today.Year, today.Month, 1))
        {
            gaps.Add(new DataGap(plans.Source, plans.Reason ?? plans.Title, null, null, null));
        }

        return gaps;
    }

    /// <summary>
    /// Готовность данных для AI-аналитика: что отстаёт, по какую дату, почему и что сделано; актуальное — одной строкой. null — всё актуально.
    /// </summary>
    public static string? Describe(IReadOnlyList<DatasetReadiness> visible, DateTimeOffset now)
    {
        var behind = visible.Where(d => d.Lagging || d.Status is ReadinessStatus.NotConnected or ReadinessStatus.Missing).ToList();
        if (behind.Count == 0)
        {
            return null;
        }

        var lines = new List<string> { $"Готовность данных OneBase (проверено {Local(now)} по Ташкенту):" };
        foreach (var d in behind)
        {
            var at = d.UpdatedAt is { } u ? $"данные по {Local(u)}" : "данных ещё нет";
            lines.Add(d.Status switch
            {
                ReadinessStatus.Missing => $"- {d.Title}: {d.Reason}",
                ReadinessStatus.Updating => $"- {d.Title}: {at}; идёт обновление.",
                _ => $"- {d.Title}: {at}. {d.Reason}{(d.Action is { } a ? $" Что сделано: {a}" : "")}",
            });
        }

        var fresh = visible.Where(d => !behind.Contains(d)).Select(d => d.Short).ToList();
        if (fresh.Count > 0)
        {
            lines.Add($"- Актуальны: {string.Join(", ", fresh)}.");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Может ли пользователь запустить обновление источника и открыть его настройки: право подключения (ManagePermission); Linko по праву продаж —
    /// только с доступом ко всей компании (как кнопка «Обновить» в продажах), администратор интеграций — всегда.
    /// </summary>
    public static bool CanManage(SourceLink link, AnalytikPrincipal p) =>
        p.Has(Permissions.IntegrationsManage)
        || (p.HasAny(link.ManagePermission.Split('|', StringSplitOptions.RemoveEmptyEntries)) && !(link.Code == SourceCodes.Linko && p.Sales.Restricted));

    public static string Local(DateTimeOffset at) => at.ToOffset(FieldClock.Offset).ToString("dd.MM.yyyy HH:mm");

    private static string Join(IReadOnlyList<string> items) =>
        items.Count <= 1 ? string.Concat(items) : string.Join(", ", items.Take(items.Count - 1)) + " и " + items[^1];

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Short(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

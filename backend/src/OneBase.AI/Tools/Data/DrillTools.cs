using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Promo;
using OneBase.Application.Sales.Settlements;
using OneBase.Application.Security;
using OneBase.Domain.Sales;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Страница OneBase целиком для модели: те же данные, что видит пользователь. Длинные списки — первые строки и число всех
/// (поле «…Total» рядом со списком), дробные числа округлены, пустые поля убраны.
/// </summary>
internal static class PageJson
{
    public const string SharesNote = "Доли и выполнение (share, execution, ratio, conversion…) — от 0 до 1; суммы — в сумах, вес — в кг.";

    public static JsonNode? Of(object view, int cap)
    {
        var node = JsonSerializer.SerializeToNode(view, DataTool.Json);
        Trim(node, cap);
        return node;
    }

    private static void Trim(JsonNode? node, int cap)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var key in o.Select(p => p.Key).ToList())
                {
                    switch (o[key])
                    {
                        case JsonArray a:
                            if (a.Count > cap)
                            {
                                o[key + "Total"] = a.Count;
                                while (a.Count > cap)
                                {
                                    a.RemoveAt(a.Count - 1);
                                }
                            }

                            Trim(a, cap);
                            break;
                        case JsonValue v:
                            if (Rounded(v) is { } r)
                            {
                                o[key] = r;
                            }

                            break;
                        case var child:
                            Trim(child, cap);
                            break;
                    }
                }

                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    if (a[i] is JsonValue v)
                    {
                        if (Rounded(v) is { } r)
                        {
                            a[i] = r;
                        }
                    }
                    else
                    {
                        Trim(a[i], cap);
                    }
                }

                break;
        }
    }

    /// <summary>Дробное число: от 100 — один знак, меньше — четыре (доли). Целые и не числа — без изменений.</summary>
    private static JsonValue? Rounded(JsonValue v)
    {
        if (v.GetValueKind() != JsonValueKind.Number || !v.TryGetValue<decimal>(out var d) || d == decimal.Truncate(d))
        {
            return null;
        }

        return JsonValue.Create(Math.Round(d, Math.Abs(d) >= 100 ? 1 : 4));
    }
}

/// <summary>Поиск ТП и торговых точек по id или названию для инструментов детализации.</summary>
internal static class DrillLookup
{
    /// <summary>
    /// ТП по id Linko или имени (часть имени, кириллица или латиница). Сначала — те, у кого есть данные в отчёте месяца, затем активные.
    /// Несколько равных совпадений — ошибка со списком: модель уточнит.
    /// </summary>
    public static async Task<(long Id, string Name)> AgentAsync(IAppDbContext db, SalesAnalytics analytics, string? query, CancellationToken ct)
    {
        if (query is null)
        {
            throw new ToolArgumentException("Укажите агента: имя (или часть имени) или id Linko.");
        }

        var users = await db.LinkoUsers.AsNoTracking()
            .Select(u => new { u.Id, u.FirstName, u.SecondName, u.Username, u.IsActive, u.JobName })
            .ToListAsync(ct);
        string NameOf(long id, string? first, string? second, string? username) =>
            string.Join(' ', new[] { first, second }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n ? n : username ?? $"#{id}";

        if (long.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            var byId = users.FirstOrDefault(u => u.Id == id);
            return byId is not null || analytics.HasAgent(id)
                ? (id, byId is null ? $"#{id}" : NameOf(byId.Id, byId.FirstName, byId.SecondName, byId.Username))
                : throw new ToolArgumentException($"Пользователя Linko с id {id} нет.");
        }

        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Loose).Where(k => k.Length > 0).ToList();
        var found = users
            .Select(u => (u.Id, Name: NameOf(u.Id, u.FirstName, u.SecondName, u.Username), u.IsActive, u.JobName, Key: Loose($"{u.FirstName} {u.SecondName} {u.Username}")))
            .Where(u => words.Count > 0 && words.All(w => u.Key.Contains(w, StringComparison.Ordinal)))
            .OrderByDescending(u => analytics.HasAgent(u.Id))
            .ThenByDescending(u => u.IsActive)
            .ThenBy(u => u.Name)
            .ToList();
        if (found.Count == 0)
        {
            throw new ToolArgumentException($"Сотрудника «{query}» в Linko нет. Проверьте имя или передайте id Linko.");
        }

        var best = found.Where(u => analytics.HasAgent(u.Id) == analytics.HasAgent(found[0].Id) && u.IsActive == found[0].IsActive).ToList();
        if (best.Count > 1)
        {
            throw new ToolArgumentException(
                $"Под «{query}» подходят несколько сотрудников: {string.Join("; ", best.Take(10).Select(u => $"{u.Name} (id {u.Id}{(u.JobName is null ? "" : $", {u.JobName}")})"))}. " +
                "Уточните имя или передайте id.");
        }

        return (best[0].Id, best[0].Name);
    }

    /// <summary>
    /// Ключ имени для сравнения кириллицы с узбекской латиницей: «Жасур Хамидов» и «Jasur Xamidov» → «jasurhamidov», «G'ulom» = «Гулом».
    /// </summary>
    public static string Loose(string text) =>
        NameMatch.Key(text).Replace("zh", "j", StringComparison.Ordinal).Replace("kh", "h", StringComparison.Ordinal)
            .Replace('x', 'h').Replace('q', 'k').Replace('w', 'v');

    /// <summary>Торговые точки по id Linko, названию или адресу (часть текста), по алфавиту.</summary>
    public static async Task<List<LinkoMarket>> StoresAsync(IAppDbContext db, string query, int take, CancellationToken ct)
    {
        if (long.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return await db.LinkoMarkets.AsNoTracking().Where(m => m.Id == id).ToListAsync(ct);
        }

        var text = query.ToLowerInvariant();
        return await db.LinkoMarkets.AsNoTracking()
            .Where(m => m.Name.ToLower().Contains(text) || (m.Address != null && m.Address.ToLower().Contains(text)))
            .OrderBy(m => m.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>Имена пользователей Linko по id.</summary>
    public static async Task<Dictionary<long, string>> NamesAsync(IAppDbContext db, IEnumerable<long> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToArray();
        if (list.Length == 0)
        {
            return [];
        }

        var rows = await db.LinkoUsers.AsNoTracking().Where(u => list.Contains(u.Id)).Select(u => new { u.Id, u.FirstName, u.SecondName, u.Username }).ToListAsync(ct);
        return rows.ToDictionary(u => u.Id, u =>
            string.Join(' ', new[] { u.FirstName, u.SecondName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n ? n : u.Username ?? $"#{u.Id}");
    }

    /// <summary>День «ГГГГ-ММ-ДД».</summary>
    public static DateOnly? Day(ToolArgs args, string name) =>
        args.Str(name) is not { } text ? null
        : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d
        : throw new ToolArgumentException($"Аргумент {name} — дата в виде ГГГГ-ММ-ДД, например 2026-10-22.");

    /// <summary>Отрезок дней из date или from/to (не длиннее maxDays); без дат — последние days дней по сегодня.</summary>
    public static (DateOnly From, DateOnly To) Range(ToolArgs args, DateOnly today, int defaultDays, int maxDays)
    {
        if (Day(args, "date") is { } date)
        {
            return (date, date);
        }

        var to = Day(args, "to") ?? today;
        var from = Day(args, "from") ?? to.AddDays(1 - defaultDays);
        if (from > to)
        {
            throw new ToolArgumentException("Аргумент from должен быть не позже to.");
        }

        return to.DayNumber - from.DayNumber + 1 > maxDays
            ? throw new ToolArgumentException($"Отрезок — не длиннее {maxDays} дней: сузьте from/to.")
            : (from, to);
    }

    /// <summary>Сегодня в Узбекистане (UTC+5).</summary>
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(5));

    public static string Time(DateTime at) => at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static readonly (string, object) AgentArg = ("agent", new { type = "string", description = "ТП (агент): имя или часть имени, кириллицей или латиницей, либо id Linko." });

    public static readonly (string, object) DateArg = ("date", new { type = "string", description = "Один день ГГГГ-ММ-ДД. Вместо него можно from и to." });
    public static readonly (string, object) FromArg = ("from", new { type = "string", description = "С какого дня, ГГГГ-ММ-ДД." });
    public static readonly (string, object) ToArg = ("to", new { type = "string", description = "По какой день, ГГГГ-ММ-ДД; без него — по сегодня." });
}

/// <summary>Карточка ТП за месяц — как страница «Продажи → агент».</summary>
internal sealed class GetAgentTool(SalesDataLoader loader, IAppDbContext db) : SalesTool(loader)
{
    public override string Name => "get_agent";
    public override string Title => "Карточка ТП";
    public override string Source => KnowledgeSources.SalesTeam;

    public override string UsageHint =>
        "Вопрос о конкретном ТП («как работает …», «почему у … упало», «какие точки молчат у …») — сначала get_agent, затем при нужде get_agent_visits и get_orders.";

    public override string Description =>
        "Один торговый представитель за месяц — всё, что на его странице в OneBase: регион, план и факт (кг и выручка), выполнение, прогноз, АКБ, " +
        "выручка и кг на точку, заказы, визиты и визиты с заказом, конверсия, сумма на визит, средний чек (с медианой региона), категории и план по ним, " +
        "показатели Linko, замечания, молчащие точки (с выручкой прошлого месяца), новые точки, ассортимент, АКБ по месяцам, сравнение с теми же днями прошлого месяца.";

    public override JsonElement InputSchema { get; } = Schema(DrillLookup.AgentArg, YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var (id, name) = await DrillLookup.AgentAsync(db, analytics, args.Str("agent"), ct);
        var p = analytics.Period;
        if (!analytics.HasAgent(id))
        {
            throw new ToolArgumentException($"У {name} нет продаж, визитов и планов за {MonthName(p.Month)} {p.Year}.");
        }

        return Data(new { Period = PeriodOf(p), Note = PageJson.SharesNote, Agent = PageJson.Of(analytics.CachedAgent(id), args.Cap(30)) },
            new DataSource($"OneBase → Продажи → {name}", PeriodText(p), $"/sales/agents/{id}?{Query(p.Year, p.Month)}"));
    }
}

/// <summary>Визиты ТП по точкам: куда ходил, по маршруту или вне, с заказом или без.</summary>
internal sealed class GetAgentVisitsTool(SalesDataLoader loader, IAppDbContext db, TimeProvider clock) : SalesTool(loader)
{
    private const int MaxDays = 31;

    private sealed record VisitOrder(long Id, DateOnly CreatedDate, long? MarketId, string Status, decimal TotalPrice, decimal TotalWeight);

    public override string Name => "get_agent_visits";
    public override string Title => "Визиты ТП по точкам";
    public override string Source => KnowledgeSources.SalesVisits;

    public override string UsageHint =>
        "Куда ходил ТП, сколько визитов по маршруту и вне маршрута, какие точки пропустил, где был без заказа — get_agent_visits (день — date, отрезок — from/to).";

    public override string Description =>
        "Куда ходил торговый представитель: каждый визит Linko за день или отрезок до 31 дня — время, точка (название, адрес), по маршруту (в плане) " +
        "или вне маршрута, выполнен ли, был ли в этот день в точке заказ этого ТП (число, сумма, кг, статус). Итоги и разбивка по дням — полные " +
        "(не зависят от длины списка визитов): план, по маршруту, вне маршрута, не посещено, визиты с заказом по маршруту и вне, заказы (число, " +
        "сумма, кг, точки) с визитов по маршруту, вне маршрута и без визита. Заказы — введённые ТП в эти дни (любой статус, кроме отменённых и " +
        "недоставленных). Без дат — сегодня. Итоги «Продаж» (принятые заказы, выручка, АКБ) по маршруту за отрезок — get_visits.";

    public override JsonElement InputSchema { get; } = Schema(DrillLookup.AgentArg, DrillLookup.DateArg, DrillLookup.FromArg, DrillLookup.ToArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(new ToolArgs(default), ct);
        var (agentId, name) = await DrillLookup.AgentAsync(db, analytics, args.Str("agent"), ct);
        var (from, to) = DrillLookup.Range(args, DrillLookup.Today(clock), 1, MaxDays);

        var visits = await db.LinkoVisits.AsNoTracking()
            .Where(v => v.UserId == agentId && v.Day >= from && v.Day <= to)
            .OrderBy(v => v.Date)
            .ToListAsync(ct);
        var orders = await db.LinkoOrders.AsNoTracking()
            .Where(o => o.AgentId == agentId && o.CreatedDate >= from && o.CreatedDate <= to)
            .Select(o => new VisitOrder(o.Id, o.CreatedDate, o.MarketId, o.Status, o.TotalPrice, o.TotalWeight))
            .ToListAsync(ct);
        // Заказ «по маршруту» — в день ввода в этой точке был выполненный визит по плану (любого ТП, как календарь визитов), «вне маршрута» —
        // только визит без плана, иначе «без визита». Отменённые в итогах по маршруту не считаются («ещё не отгружен» — обычный взятый заказ).
        var orderMarkets = orders.Where(o => o.MarketId != null).Select(o => o.MarketId!.Value).Distinct().ToArray();
        var doneAt = (await db.LinkoVisits.AsNoTracking()
                .Where(v => v.Status == "done" && v.MarketId != null && orderMarkets.Contains(v.MarketId.Value) && v.Day >= from && v.Day <= to)
                .Select(v => new { Market = v.MarketId!.Value, v.Day, v.IsInPlan })
                .ToListAsync(ct))
            .GroupBy(v => (v.Market, v.Day))
            .ToDictionary(g => g.Key, g => g.Any(v => v.IsInPlan));
        string RouteOf(DateOnly day, long? market) =>
            market is { } m && doneAt.TryGetValue((m, day), out var inPlan) ? inPlan ? "byRoute" : "offRoute" : "noVisit";
        var taken = orders.Where(o => !OneBase.Application.Field.LinkoMirrorSalesProvider.NotTaken.Contains(o.Status)).ToList();
        object Orders(IEnumerable<VisitOrder> list)
        {
            var items = list.ToList();
            return new
            {
                Count = items.Count,
                SumUzs = R(items.Sum(o => o.TotalPrice)),
                Kg = R(items.Sum(o => o.TotalWeight), 1),
                Stores = items.Where(o => o.MarketId != null).Select(o => o.MarketId).Distinct().Count(),
            };
        }
        var marketIds = visits.Where(v => v.MarketId != null).Select(v => v.MarketId!.Value)
            .Concat(orders.Where(o => o.MarketId != null).Select(o => o.MarketId!.Value))
            .Distinct().ToArray();
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => marketIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name, m.Address, m.BranchName }).ToDictionaryAsync(m => m.Id, ct);
        var ordersAt = orders.Where(o => o.MarketId != null).ToLookup(o => (o.CreatedDate, o.MarketId!.Value));

        object? OrderOf(DateOnly day, long? market)
        {
            if (market is not { } m || !ordersAt.Contains((day, m)))
            {
                return null;
            }

            var list = ordersAt[(day, m)].ToList();
            return new { Count = list.Count, SumUzs = R(list.Sum(o => o.TotalPrice)), Kg = R(list.Sum(o => o.TotalWeight), 1), Statuses = list.Select(o => o.Status).Distinct() };
        }

        var done = visits.Where(v => v.Status == "done").ToList();
        var visitedAt = done.Where(v => v.MarketId != null).Select(v => (v.Day, v.MarketId!.Value)).ToHashSet();
        var withoutVisit = orders.Where(o => o.MarketId is not { } m || !visitedAt.Contains((o.CreatedDate, m))).ToList();

        var days = visits.GroupBy(v => v.Day).OrderBy(g => g.Key).Select(g => new
        {
            Date = DrillLookup.Date(g.Key),
            Planned = g.Count(v => v.IsInPlan),
            DoneByRoute = g.Count(v => v.IsInPlan && v.Status == "done"),
            DoneOffRoute = g.Count(v => !v.IsInPlan && v.Status == "done"),
            NotVisited = g.Count(v => v.IsInPlan && v.Status != "done"),
            VisitsWithOrder = g.Count(v => v.Status == "done" && v.MarketId is { } m && ordersAt.Contains((g.Key, m))),
            VisitsWithOrderByRoute = g.Count(v => v.IsInPlan && v.Status == "done" && v.MarketId is { } m && ordersAt.Contains((g.Key, m))),
            OrdersByRoute = Orders(taken.Where(o => o.CreatedDate == g.Key && RouteOf(o.CreatedDate, o.MarketId) == "byRoute")),
            OrdersOffRoute = Orders(taken.Where(o => o.CreatedDate == g.Key && RouteOf(o.CreatedDate, o.MarketId) == "offRoute")),
            OrdersNoVisit = Orders(taken.Where(o => o.CreatedDate == g.Key && RouteOf(o.CreatedDate, o.MarketId) == "noVisit")),
        }).ToList();

        return Data(new
        {
            Agent = name,
            AgentId = agentId,
            From = DrillLookup.Date(from),
            To = DrillLookup.Date(to),
            Totals = new
            {
                Planned = visits.Count(v => v.IsInPlan),
                DoneByRoute = done.Count(v => v.IsInPlan),
                DoneOffRoute = done.Count(v => !v.IsInPlan),
                DoneAll = done.Count,
                NotVisited = visits.Count(v => v.IsInPlan && v.Status != "done"),
                VisitsWithOrder = done.Count(v => v.MarketId is { } m && ordersAt.Contains((v.Day, m))),
                VisitsWithOrderByRoute = done.Count(v => v.IsInPlan && v.MarketId is { } m && ordersAt.Contains((v.Day, m))),
                VisitsWithOrderOffRoute = done.Count(v => !v.IsInPlan && v.MarketId is { } m && ordersAt.Contains((v.Day, m))),
                OrdersByRoute = Orders(taken.Where(o => RouteOf(o.CreatedDate, o.MarketId) == "byRoute")),
                OrdersOffRoute = Orders(taken.Where(o => RouteOf(o.CreatedDate, o.MarketId) == "offRoute")),
                OrdersNoVisit = Orders(taken.Where(o => RouteOf(o.CreatedDate, o.MarketId) == "noVisit")),
                OrdersTaken = Orders(taken),
                OrdersCancelled = orders.Count - taken.Count,
                Orders = orders.Count,
                OrdersSumUzs = R(orders.Sum(o => o.TotalPrice)),
                OrdersWithoutVisit = withoutVisit.Count,
                OrdersWithoutVisitSumUzs = R(withoutVisit.Sum(o => o.TotalPrice)),
            },
            Days = from == to ? null : days,
            Visits = visits.TakeTop(args, from == to ? 120 : 80, "visits").Select(v => new
            {
                Time = DrillLookup.Time(v.Date),
                StoreId = v.MarketId,
                Store = v.MarketId is { } m && markets.TryGetValue(m, out var s) ? s.Name : null,
                Address = v.MarketId is { } m2 && markets.TryGetValue(m2, out var s2) ? s2.Address : null,
                ByRoute = v.IsInPlan,
                v.Status, // done — выполнен; остальное (pending и др.) — не выполнен
                Order = v.Status == "done" ? OrderOf(v.Day, v.MarketId) : null,
            }),
            VisitsTotal = visits.Count,
            OrdersWithoutVisit = withoutVisit.TakeTop(args, 30, "ordersWithoutVisit").Select(o => new
            {
                o.Id,
                Date = DrillLookup.Date(o.CreatedDate),
                Store = o.MarketId is { } m && markets.TryGetValue(m, out var s) ? s.Name : null,
                o.Status,
                SumUzs = R(o.TotalPrice),
                Kg = R(o.TotalWeight, 1),
            }),
            Note = "ByRoute — визит из плана (маршрута) Linko; заказ визита — заказ этого ТП в этой точке, введённый в день визита (любой статус). " +
                   "Totals и Days — полные; Visits и OrdersWithoutVisit — первые строки списка (VisitsTotal — сколько всего). OrdersByRoute / OffRoute / " +
                   "NoVisit — заказы без отменённых и недоставленных, по визитам любого ТП в точке в день ввода (как календарь визитов).",
        }, new DataSource($"Linko → визиты {name}", from == to ? DrillLookup.Date(from) : $"{DrillLookup.Date(from)} — {DrillLookup.Date(to)}",
            $"/sales/agents/{agentId}"));
    }
}

/// <summary>Карточка торговой точки: продажи месяца, товары, визиты и последние заказы.</summary>
internal sealed class GetStoreTool(SalesDataLoader loader, IAppDbContext db) : SalesTool(loader)
{
    public override string Name => "get_store";
    public override string Title => "Карточка торговой точки";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string UsageHint =>
        "Вопрос о конкретном магазине (что берёт, кто ходит, когда последний заказ) — get_store; если вернулся список, вызови снова с id нужной точки.";

    public override string Description =>
        "Торговая точка (магазин) по названию, адресу или id Linko: регион, ответственный ТП, адрес и координаты, продажи за месяц (кг, выручка, " +
        "заказы, категории, товары, прошлый месяц), кто из ТП продавал, последние визиты (кто, когда, по маршруту ли) и последние заказы. " +
        "Если подходит несколько точек — список, чтобы выбрать.";

    public override JsonElement InputSchema { get; } = Schema(
        ("store", new { type = "string", description = "Точка: название или часть названия, часть адреса или id Linko." }), YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var query = args.Str("store") ?? throw new ToolArgumentException("Укажите точку: название, часть адреса или id.");
        var found = await DrillLookup.StoresAsync(db, query, 30, ct);
        if (found.Count == 0)
        {
            throw new ToolArgumentException($"Точки «{query}» в Linko нет.");
        }

        var exact = found.Where(m => string.Equals(m.Name.Trim(), query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (found.Count > 1 && exact.Count != 1)
        {
            var agents = await DrillLookup.NamesAsync(db, found.Where(m => m.ResponsibleAgentId != null).Select(m => m.ResponsibleAgentId!.Value), ct);
            return Data(new
            {
                Found = found.Count,
                Note = "Подходит несколько точек — вызовите снова с id нужной.",
                Stores = found.Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Address,
                    Branch = m.BranchName,
                    Agent = m.ResponsibleAgentId is { } a ? agents.GetValueOrDefault(a) : null,
                }),
            });
        }

        var market = exact.Count == 1 ? exact[0] : found[0];
        var analytics = await LoadAsync(args, ct);
        var p = analytics.Period;
        var visits = await db.LinkoVisits.AsNoTracking().Where(v => v.MarketId == market.Id).OrderByDescending(v => v.Date).Take(args.Cap(20)).ToListAsync(ct);
        var orders = await db.LinkoOrders.AsNoTracking().Where(o => o.MarketId == market.Id).OrderByDescending(o => o.CreatedAt).Take(args.Cap(20))
            .Select(o => new { o.Id, o.CreatedAt, o.AcceptedDate, o.Status, o.AgentId, o.TotalPrice, o.TotalWeight, o.DiscountPrice })
            .ToListAsync(ct);
        // Последние визиты и заказы — выборка: сколько их всего у точки, считает база (в «_meta» результата).
        args.Count("lastVisits", await db.LinkoVisits.AsNoTracking().CountAsync(v => v.MarketId == market.Id, ct), visits.Count);
        args.Count("lastOrders", await db.LinkoOrders.AsNoTracking().CountAsync(o => o.MarketId == market.Id, ct), orders.Count);
        var names = await DrillLookup.NamesAsync(db, visits.Where(v => v.UserId != null).Select(v => v.UserId!.Value)
            .Concat(orders.Where(o => o.AgentId != null).Select(o => o.AgentId!.Value))
            .Concat(market.ResponsibleAgentId is { } r ? new[] { r } : Array.Empty<long>()), ct);

        return Data(new
        {
            Period = PeriodOf(p),
            Note = PageJson.SharesNote,
            Store = new
            {
                market.Id,
                market.Name,
                market.Address,
                Type = market.MarketTypeName,
                Branch = market.BranchName,
                ResponsibleAgent = market.ResponsibleAgentId is { } ra ? names.GetValueOrDefault(ra) : null,
                market.Lat,
                market.Lon,
            },
            Month = analytics.HasMarket(market.Id) ? PageJson.Of(analytics.CachedStore(market.Id, null), args.Cap(30)) : null,
            LastVisits = visits.Select(v => new
            {
                Time = DrillLookup.Time(v.Date),
                Agent = v.UserId is { } u ? names.GetValueOrDefault(u) : null,
                ByRoute = v.IsInPlan,
                v.Status,
            }),
            LastOrders = orders.Select(o => new
            {
                o.Id,
                Created = DrillLookup.Time(o.CreatedAt),
                Accepted = o.AcceptedDate is { } d ? DrillLookup.Date(d) : null,
                o.Status,
                Agent = o.AgentId is { } a ? names.GetValueOrDefault(a) : null,
                SumUzs = R(o.TotalPrice),
                DiscountUzs = o.DiscountPrice == 0 ? (decimal?)null : R(o.DiscountPrice),
                Kg = R(o.TotalWeight, 1),
            }),
        }, new DataSource($"OneBase → Продажи → {market.Name}", PeriodText(p), $"/sales/stores/{market.Id}?{Query(p.Year, p.Month)}"));
    }
}

/// <summary>Заказы Linko по ТП, точке, региону и дням — с позициями.</summary>
internal sealed class GetOrdersTool(SalesDataLoader loader, IAppDbContext db, TimeProvider clock) : SalesTool(loader)
{
    private const int MaxDays = 62;

    public override string Name => "get_orders";
    public override string Title => "Заказы";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string UsageHint =>
        "Конкретные заказы, их состав, статусы и скидки — get_orders (lines=true — позиции; order — один заказ по номеру).";

    public override string Description =>
        "Отдельные заказы Linko: по ТП, торговой точке, региону, статусу и дням создания (до 62 дней; без дат — последние 7) или один заказ по номеру. " +
        "У заказа — дата создания и приёмки, статус, точка, ТП, сумма, скидка, вес; с lines=true или по номеру — позиции (товар, количество, " +
        "возврат, цена, сумма, кг). Итоги — по всем найденным заказам, по статусам. Факт продаж OneBase считает по приёмке и статусам доставки, " +
        "поэтому сумма списка может отличаться от факта.";

    public override JsonElement InputSchema { get; } = Schema(
        DrillLookup.AgentArg,
        ("store", new { type = "string", description = "Торговая точка: id Linko или название." }),
        RegionArg,
        ("status", new { type = "string", description = "Статус заказа Linko, например delivered, given, not_delivered." }),
        ("order", new { type = "integer", description = "Номер (id) одного заказа — с позициями." }),
        ("lines", new { type = "boolean", description = "true — с позициями заказов." }),
        DrillLookup.DateArg, DrillLookup.FromArg, DrillLookup.ToArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var q = db.LinkoOrders.AsNoTracking().AsQueryable();
        SalesAnalytics? analytics = null;
        async Task<SalesAnalytics> Analytics() => analytics ??= await LoadAsync(new ToolArgs(default), ct);
        string scope;
        var orderId = args.Int("order", 1, int.MaxValue);
        if (orderId is { } single)
        {
            q = q.Where(o => o.Id == single);
            scope = $"заказ {single}";
        }
        else
        {
            var (from, to) = DrillLookup.Range(args, DrillLookup.Today(clock), 7, MaxDays);
            q = q.Where(o => o.CreatedDate >= from && o.CreatedDate <= to);
            scope = from == to ? DrillLookup.Date(from) : $"{DrillLookup.Date(from)} — {DrillLookup.Date(to)}";

            if (args.Str("agent") is { } agentText)
            {
                var (agentId, agentName) = await DrillLookup.AgentAsync(db, await Analytics(), agentText, ct);
                q = q.Where(o => o.AgentId == agentId);
                scope += $", {agentName}";
            }

            if (args.Str("store") is { } storeText)
            {
                var stores = await DrillLookup.StoresAsync(db, storeText, 31, ct);
                if (stores.Count == 0 || stores.Count > 30)
                {
                    throw new ToolArgumentException(stores.Count == 0 ? $"Точки «{storeText}» нет." : $"Под «{storeText}» больше 30 точек — уточните или передайте id.");
                }

                var ids = stores.Select(s => s.Id).ToArray();
                q = q.Where(o => o.MarketId != null && ids.Contains(o.MarketId.Value));
                scope += stores.Count == 1 ? $", {stores[0].Name}" : $", точки «{storeText}» ({stores.Count})";
            }

            if (args.Str("region") is { } regionText)
            {
                var region = FindRegion((await Analytics()).CachedRepublic(null, null), regionText)!;
                var regionId = Guid.Parse(region.Id);
                var branch = await db.SalesRegions.AsNoTracking().Where(r => r.Id == regionId).Select(r => (long?)r.LinkoBranchId).FirstOrDefaultAsync(ct);
                q = q.Where(o => o.BranchId == branch);
                scope += $", {region.Name}";
            }

            if (args.Str("status") is { } status)
            {
                q = q.Where(o => o.Status == status);
                scope += $", статус {status}";
            }
        }

        var totals = await q.GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Orders = g.Count(), SumUzs = g.Sum(o => o.TotalPrice), Kg = g.Sum(o => o.TotalWeight) })
            .ToListAsync(ct);
        var orders = await q.OrderByDescending(o => o.CreatedAt).Take(args.Cap(50))
            .Select(o => new { o.Id, o.CreatedAt, o.AcceptedDate, o.DeliveryDate, o.Status, o.MarketId, o.AgentId, o.BranchName, o.TotalPrice, o.DiscountPrice, o.TotalWeight, o.IsFullReturn })
            .ToListAsync(ct);
        // Заказы — последние 50, итоги по статусам (totals) — по всем: сколько заказов всего, видно в «_meta».
        args.Count("orders", totals.Sum(t => t.Orders), orders.Count);
        if (orders.Count == 0)
        {
            return Data(new { Scope = scope, Orders = Array.Empty<object>(), Note = "Заказов с такими условиями нет." });
        }

        var withLines = orderId is not null || args.Report || args.Bool("lines") == true;
        var shown = orders.Select(o => o.Id).ToArray();
        var lines = withLines
            ? await db.LinkoOrderLines.AsNoTracking().Where(l => shown.Contains(l.OrderId)).ToListAsync(ct)
            : new List<LinkoOrderLine>();
        var productIds = lines.Where(l => l.ProductId != null).Select(l => l.ProductId!.Value).Distinct().ToArray();
        var products = await db.LinkoProducts.AsNoTracking().Where(p => productIds.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Code }).ToDictionaryAsync(p => p.Id, ct);
        var marketIds = orders.Where(o => o.MarketId != null).Select(o => o.MarketId!.Value).Distinct().ToArray();
        var markets = await db.LinkoMarkets.AsNoTracking().Where(m => marketIds.Contains(m.Id)).Select(m => new { m.Id, m.Name }).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var names = await DrillLookup.NamesAsync(db, orders.Where(o => o.AgentId != null).Select(o => o.AgentId!.Value), ct);
        var byOrder = lines.ToLookup(l => l.OrderId);

        return Data(new
        {
            Scope = scope,
            Total = new { Orders = totals.Sum(t => t.Orders), SumUzs = R(totals.Sum(t => t.SumUzs)), Kg = R(totals.Sum(t => t.Kg), 1) },
            ByStatus = totals.OrderByDescending(t => t.Orders).Select(t => new { t.Status, t.Orders, SumUzs = R(t.SumUzs), Kg = R(t.Kg, 1) }),
            Orders = orders.Select(o => new
            {
                o.Id,
                Created = DrillLookup.Time(o.CreatedAt),
                Accepted = o.AcceptedDate is { } a ? DrillLookup.Date(a) : null,
                Delivery = o.DeliveryDate is { } d ? DrillLookup.Date(d) : null,
                o.Status,
                StoreId = o.MarketId,
                Store = o.MarketId is { } m ? markets.GetValueOrDefault(m) : null,
                Agent = o.AgentId is { } ag ? names.GetValueOrDefault(ag) : null,
                Branch = o.BranchName,
                SumUzs = R(o.TotalPrice),
                DiscountUzs = o.DiscountPrice == 0 ? (decimal?)null : R(o.DiscountPrice),
                Kg = R(o.TotalWeight, 1),
                FullReturn = o.IsFullReturn ? true : (bool?)null,
                Lines = withLines
                    ? byOrder[o.Id].Select(l => new
                    {
                        Product = l.ProductId is { } pid && products.TryGetValue(pid, out var pr) ? pr.Name : null,
                        Code = l.ProductId is { } pid2 && products.TryGetValue(pid2, out var pr2) ? pr2.Code : null,
                        l.Amount,
                        ReturnAmount = l.ReturnAmount == 0 ? (decimal?)null : l.ReturnAmount,
                        PriceUzs = R(l.Price),
                        SumUzs = R(l.TotalPrice),
                        Kg = R(l.TotalWeight, 2),
                    })
                    : null,
            }),
            OrdersShown = orders.Count,
        }, new DataSource("Linko → заказы", scope, "/sales"));
    }
}

/// <summary>«KPI»: выполнение показателей Linko по людям, премии и фонд оплаты — деньги только с правом «Финансы».</summary>
internal sealed class GetKpiTool(SalesDataLoader loader, IUserPermissions permissions) : SalesTool(loader)
{
    public override string Name => "get_kpi";
    public override string Title => "KPI сотрудников";
    public override string Source => KnowledgeSources.SalesPlans;

    public override string Description =>
        "Страница «KPI»: показатели Linko по людям (ТП и супервайзеры) — план, продажи, возвраты, факт и выполнение по каждому показателю, " +
        "пороги премии; с правом «Финансы» — оклад, премии (начислено и прогноз) и фонд оплаты по регионам и направлениям. " +
        "Без региона и человека — итоги по регионам; с region — люди региона; с person — один человек.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg,
        ("person", new { type = "string", description = "Человек: имя или часть имени." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var analytics = await LoadAsync(args, ct);
        var money = context.UserId is { } user && (await permissions.GetAsync(user, ct)).Contains(Permissions.FinanceRead);
        var view = analytics.CachedKpi(money);
        var region = args.Str("region");
        var person = args.Str("person") is { } pt ? NameMatch.Key(pt) : null;
        var regions = view.Regions
            .Where(r => region is null || r.Name.Contains(region, StringComparison.OrdinalIgnoreCase) || region.Contains(r.Name, StringComparison.OrdinalIgnoreCase))
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Direction,
                r.Unit,
                r.Svr,
                r.Tp,
                r.FactKgSvr,
                r.FactKgTp,
                r.Now,
                r.Forecast,
                r.Columns,
                People = region is null && person is null ? null
                    : r.People.Where(p => person is null || NameMatch.Key(p.Name).Contains(person, StringComparison.Ordinal)).ToList(),
            })
            .Where(r => person is null || r.People is { Count: > 0 })
            .ToList();
        if (regions.Count == 0)
        {
            throw new ToolArgumentException(region is not null && person is null
                ? $"Региона «{region}» в KPI нет. Регионы: {string.Join(", ", view.Regions.Select(r => r.Name))}."
                : "Такого человека в KPI за месяц нет.");
        }

        return Data(new
        {
            Period = PeriodOf(view.Period),
            Note = PageJson.SharesNote + (money ? "" : " Денег (оклад, премии, фонд оплаты) нет: у пользователя нет права «Финансы»."),
            Summary = PageJson.Of(new { view.Money, view.Running, view.TpCount, view.SvrCount, view.FactKgTp, view.ForecastKgTp, view.Now, view.Forecast, view.Directions, view.MissingRegions }, 40),
            Regions = PageJson.Of(regions, args.Cap(region is null && person is null ? 40 : 60)),
        }, new DataSource("OneBase → Продажи → KPI", PeriodText(view.Period), $"/sales/kpi?{Query(view.Period.Year, view.Period.Month)}"));
    }
}

/// <summary>«Акции»: скидки и бонусы вторички по акциям Linko.</summary>
internal sealed class GetPromoTool(SalesDataLoader loader, PromoService promo) : SalesTool(loader)
{
    public override string Name => "get_promo";
    public override string Title => "Акции и скидки";
    public override string Source => KnowledgeSources.SalesSecondary;

    public override string Description =>
        "Страница «Акции»: скидки и бонусы вторички по акциям Linko за месяц — плитки, на что обратить внимание, выводы, акции (сумма скидки, " +
        "точки, заказы), эффект акций на продажи, регионы, ручные скидки, календарь, ТП и точки с наибольшими скидками. rm — одно направление (РМ).";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg,
        ("rm", new { type = "string", description = "Направление (РМ) — как на странице «Акции»; без него — вся страна." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var (year, month) = args.Period();
        var view = await promo.GetAsync(year is not null && month is not null ? $"{year}-{month}" : null, args.Str("rm"), [], ct)
            ?? throw new ToolArgumentException("Строк со скидками за год в OneBase нет.");
        return Data(new { Note = PageJson.SharesNote, Promo = PageJson.Of(view, args.Cap(25)) },
            new DataSource("OneBase → Продажи → Акции", $"{MonthName(view.Month.Month)} {view.Month.Year}", $"/sales/promo?month={view.Month.Key}"));
    }
}

/// <summary>«Акт сверки» с дилерами за месяц.</summary>
internal sealed class GetAktTool(SettlementsService settlements) : DataTool
{
    public override string Name => "get_akt";
    public override string Title => "Акт сверки";
    public override string Source => KnowledgeSources.FinancePayments;
    public override string RequiredPermission => Permissions.FinanceRead;

    public override string Description =>
        "Страница «Акт сверки»: взаиморасчёты с дилерами и контрагентами за месяц — остаток на начало, отгрузки и возвраты Linko, касса и банк, " +
        "расходы завода (ретробонус, ЗП, логистика, акции, кешбэк), остаток на конец, проверки акта. С dealer — карточка контрагента с документами, " +
        "оплатами и расходами по дням.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg,
        ("dealer", new { type = "string", description = "Дилер или контрагент: название или часть названия." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var (year, month) = args.Period();
        var view = AktCompute.Build(await settlements.BaseAsync(ct), year is not null && month is not null ? $"{year}-{month}" : null);
        var dealer = args.Str("dealer");
        var cards = dealer is null ? new List<AktCard>() : view.Cards.Where(c => c.Name.Contains(dealer, StringComparison.OrdinalIgnoreCase)).ToList();
        if (dealer is not null && cards.Count == 0)
        {
            throw new ToolArgumentException($"Контрагента «{dealer}» в акте нет. Есть: {string.Join(", ", view.Cards.Select(c => c.Name).Take(40))}.");
        }

        var groups = dealer is null
            ? view.Groups
            : view.Groups.Select(g => g with { Rows = g.Rows.Where(r => r.Name.Contains(dealer, StringComparison.OrdinalIgnoreCase)).ToList() })
                .Where(g => g.Rows.Count > 0).ToList();
        return Data(new
        {
            Note = PageJson.SharesNote,
            Akt = PageJson.Of(new
            {
                view.AsOf,
                view.Month,
                view.Arts,
                Groups = groups,
                Checks = view.Checks,
                Counterparties = dealer is null ? view.Cards.Select(c => c.Name) : null,
                Cards = cards,
                view.AnchorDate,
                view.PaymentsTo,
                view.ExpensesTo,
                view.HasPayments,
                view.HasExpenses,
                view.HasBalances,
            }, args.Cap(dealer is null ? 60 : 120)),
        }, new DataSource("OneBase → Продажи → Акт сверки", $"{MonthName(view.Month.Month)} {view.Month.Year}", $"/sales/akt?month={view.Month.Key}"));
    }
}

/// <summary>«DSO»: долги дилеров по FIFO, просрочка и прогноз оплат.</summary>
internal sealed class GetDsoTool(SettlementsService settlements) : DataTool
{
    public override string Name => "get_dso";
    public override string Title => "DSO — долги дилеров";
    public override string Source => KnowledgeSources.FinancePayments;
    public override string RequiredPermission => Permissions.FinanceRead;

    public override string Description =>
        "Страница «DSO»: долг дилеров по FIFO (неоплаченные отгрузки), просрочка по норме дней, оплачено и отгружено за месяц, ожидаемые оплаты " +
        "на неделю, дилеры (долг, просрочка, норма, факт дней), недели, оплаты по месяцам и каналы. С dealer — один дилер.";

    public override JsonElement InputSchema { get; } = Schema(("dealer", new { type = "string", description = "Дилер: название или часть названия." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var view = DsoCompute.Build(await settlements.BaseAsync(ct));
        var dealer = args.Str("dealer");
        object payload = dealer is null
            ? view
            : new
            {
                view.AsOf,
                view.DefaultNorm,
                Dealers = view.Dealers.Where(d => d.Name.Contains(dealer, StringComparison.OrdinalIgnoreCase)).ToList(),
                PayDealers = view.PayDealers.Where(d => d.Name.Contains(dealer, StringComparison.OrdinalIgnoreCase)).ToList(),
                view.PaymentsTo,
            };
        return Data(new { Note = PageJson.SharesNote, Dso = PageJson.Of(payload, args.Cap(40)) },
            new DataSource("OneBase → Продажи → DSO", $"на {view.AsOf:dd.MM.yyyy}", "/sales/dso"));
    }
}

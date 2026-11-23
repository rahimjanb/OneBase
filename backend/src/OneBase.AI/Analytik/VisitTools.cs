using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Visits;
using OneBase.Application.Security;

namespace OneBase.AI.Analytik;

/// <summary>Общее для инструментов визитов: кто спрашивает (его область «Продаж»), даты, ТП и регион по имени или id.</summary>
public abstract class VisitTool(QueryEngine engine, IAppDbContext db, SemanticContextLoader context) : DataTool
{
    public override int Level => ToolLevels.Analyze;
    public override string? Source => KnowledgeSources.SalesStats;
    public override string? RequiredPermission => Permissions.SalesRead;

    protected Task<AnalytikPrincipal> PrincipalAsync(ToolContext c, CancellationToken ct) =>
        engine.PrincipalAsync(new QueryCaller(c.UserId, c.Depth > 0 ? "investigation" : "chat", c.AgentCode, c.ConversationId), ct);

    protected static DateOnly? Date(string? s, string name) => s is null ? null
        : DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : throw new ToolArgumentException($"{name}: дата «{s}», ожидается YYYY-MM-DD.");

    /// <summary>ТП по id («403») или части имени («Салохиддин»): одно совпадение — он; несколько — подсказка выбрать.</summary>
    protected async Task<long> AgentAsync(string text, CancellationToken ct)
    {
        if (long.TryParse(text.Trim(), out var id))
        {
            return id;
        }

        var q = text.Trim().ToLower();
        var found = await db.LinkoUsers.AsNoTracking()
            .Where(u => ((u.FirstName ?? "") + " " + (u.SecondName ?? "")).ToLower().Contains(q) || (u.Username ?? "").ToLower().Contains(q))
            .OrderByDescending(u => u.IsActive).Take(6).ToListAsync(ct);
        return found.Count switch
        {
            0 => throw new ToolArgumentException($"ТП «{text}» в Linko не найден — укажите id или часть имени."),
            1 => found[0].Id,
            _ when found.Count(u => string.Equals(u.DisplayName, text.Trim(), StringComparison.OrdinalIgnoreCase)) == 1 =>
                found.First(u => string.Equals(u.DisplayName, text.Trim(), StringComparison.OrdinalIgnoreCase)).Id,
            _ => throw new ToolArgumentException($"По «{text}» несколько ТП: {string.Join("; ", found.Select(u => $"{u.Id} {u.DisplayName}"))} — укажите id."),
        };
    }

    /// <summary>Регион по id или части названия («Самарканд»).</summary>
    protected async Task<Guid> RegionAsync(string text, CancellationToken ct)
    {
        var ctx = await context.LoadAsync(ct);
        if (Guid.TryParse(text, out var id) && ctx.Regions.ContainsKey(id))
        {
            return id;
        }

        var found = ctx.Regions.Values.Where(r => r.Name.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return found.Count == 1 ? found[0].Id
            : found.FirstOrDefault(r => string.Equals(r.Name, text.Trim(), StringComparison.OrdinalIgnoreCase))?.Id
              ?? throw new ToolArgumentException(found.Count == 0
                  ? $"Региона «{text}» нет. Есть: {string.Join(", ", ctx.Regions.Values.Select(r => r.Name).Order())}."
                  : $"По «{text}» несколько регионов: {string.Join(", ", found.Select(r => r.Name))}.");
    }
}

/// <summary>
/// sales_visit_sequence — на какой по счёту визит ТП в точку приходится заказ: по всем парам «ТП × точка» периода сервер расставляет выполненные
/// визиты по порядку, сбрасывает счёт на заказе и отдаёт распределение (1-й, 2-й, 3-й, 4-й, 5+), конверсию по номеру визита, среднее и медиану
/// визитов до заказа — по организации, регионам и ТП. Без этого модель пыталась склеить цепочки из дневных срезов и не могла.
/// </summary>
public sealed class VisitSequenceTool(QueryEngine engine, IAppDbContext db, SemanticContextLoader context, VisitSequenceService sequences)
    : VisitTool(engine, db, context)
{
    public const string ToolName = "sales_visit_sequence";

    public override string Name => ToolName;
    public override string Title => "Номер визита, на котором заказ";

    public override string Description =>
        "На какой по счёту визит ТП в торговую точку приходится заказ (например, «ТП был в точке 4 раза, заказ — на третьем»): распределение " +
        "заказов по номеру визита после прошлого заказа (1-й, 2-й, 3-й, 4-й, 5+), конверсия визита по номеру (сколько визитов с таким номером " +
        "закончились заказом), среднее и медиана визитов до заказа, заказы без визита; итог, регионы и ТП, а также список пар «ТП × точка» " +
        "с самыми длинными сериями визитов без заказа (сколько визитов подряд, последний визит и последний заказ), и что бывает после крупного " +
        "или мелкого заказа: визиты и дни до следующего заказа и конверсия визитов по группам размера заказа, корреляция размера и паузы. Сервер считает по всем точкам периода (до 366 дней) по визитам и заказам Linko.";

    public override string? UsageHint =>
        "«На какой визит приходится заказ», «с какого визита точка заказывает», «сколько визитов до заказа», «конверсия первого/второго визита», " +
        "«что бывает после крупного заказа», «связь суммы заказа и следующих заказов» — sales_visit_sequence одним вызовом (а не дневные срезы " +
        "«день × ТП × точка»); размер заказа — его раздел OrderSize. Отвечай по его цифрам и правилу подсчёта.";

    public override JsonElement InputSchema { get; } = Schema(
        ("date_from", new { type = "string", description = "Начало периода YYYY-MM-DD (по умолчанию — 90 дней до вчера)." }),
        ("date_to", new { type = "string", description = "Конец периода YYYY-MM-DD (по умолчанию — вчера)." }),
        ("region", new { type = "string", description = "Регион: название («Самарканд») или id; пусто — вся доступная область." }),
        ("agent", new { type = "string", description = "ТП: id Linko или часть имени; пусто — все ТП области." }));

    protected override async Task<ToolResult> RunAsync(ToolContext c, ToolArgs args, CancellationToken ct)
    {
        var today = FieldClock.Today;
        var to = Date(args.Str("date_to"), "date_to") ?? today.AddDays(-1);
        var from = Date(args.Str("date_from"), "date_from") ?? to.AddDays(-89);
        var principal = await PrincipalAsync(c, ct);
        try
        {
            var region = args.Str("region") is { Length: > 0 } r ? await RegionAsync(r, ct) : (Guid?)null;
            var agent = args.Str("agent") is { Length: > 0 } a ? await AgentAsync(a, ct) : (long?)null;
            var report = await sequences.BuildAsync(principal.Sales, from, to, region, agent, ct);
            static object Group(VisitSequenceGroup g) => new
            {
                g.Name, g.Pairs, Visits = g.Visits, OrdersOnVisit = g.OrdersOnVisit, g.OrdersWithoutVisit,
                ByVisitNumber = g.Buckets.Select(b => new { Visit = b.Label, Orders = b.Orders, SharePct = Pct(b.Share), Reached = b.Reached, ConversionPct = Pct(b.Conversion) }),
                AverageVisitsToOrder = Round(g.AverageVisitsToOrder), MedianVisitsToOrder = g.MedianVisitsToOrder, FirstVisitSharePct = Pct(g.FirstVisitShare),
                OpenPairs = g.OpenPairs, AverageOpenStreak = Round(g.AverageOpenStreak),
            };
            var payload = new
            {
                Period = new { From = report.From.ToString("yyyy-MM-dd"), To = report.To.ToString("yyyy-MM-dd") },
                report.Rule,
                Total = Group(report.Total),
                Regions = args.Top(report.Regions, 30, "Regions").Select(Group),
                Agents = args.Top(report.Agents, 40, "Agents").Select(Group),
                OpenStreaks = args.Top(report.OpenStreaks, 30, "OpenStreaks").Select(x => new
                {
                    x.AgentId, x.Agent, x.MarketId, x.Market, x.Region, VisitsWithoutOrder = x.Visits,
                    LastVisit = x.LastVisit.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    LastOrder = x.LastOrder?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                }),
                report.OpenStreaksTotal,
                OrderSize = new
                {
                    report.OrderSize.Rule,
                    BySizeVsUsual = report.OrderSize.Relative.Select(Size),
                    ByKg = report.OrderSize.Absolute.Select(Size),
                    OrdersWithoutUsualSize = report.OrderSize.WithoutTypical,
                    Correlation = new
                    {
                        SizeVsVisitsToNext = Round(report.OrderSize.RatioVisitsSpearman), SizeVsVisitsPairs = report.OrderSize.RatioVisitsPairs,
                        SizeVsDaysToNext = Round(report.OrderSize.RatioDaysSpearman), SizeVsDaysPairs = report.OrderSize.RatioDaysPairs,
                        Method = "Ранговая корреляция Спирмена: «во сколько раз заказ больше обычного для пары» и визитов (дней) до следующего заказа. " +
                                 "|ρ| < 0,1 — связи практически нет, 0,1–0,3 — слабая, 0,3–0,5 — умеренная, больше 0,5 — сильная.",
                    },
                },
                report.Notes,
                Instructions = "Главное — распределение заказов по номеру визита (SharePct) и конверсия по номеру (ConversionPct): «из визитов, которые были " +
                               "третьими по счёту, заказом закончились N%». Reached — сколько раз дошли до этого номера. Правило подсчёта — коротко в ответе. " +
                               "OrdersWithoutVisit — заказы в дни без визита (в номера не входят). OpenStreaks — пары «ТП × точка», где после " +
                               "последнего заказа идут визиты без заказа (самые длинные серии; LastOrder null — заказа не было с начала окна): это " +
                               "ответ на «куда ходят без результата». Agents — ТП, больше заказов на визитах — выше; все строки — all_rows. " +
                               "OrderSize — «после крупного заказа», «связь суммы заказа и следующих заказов», «взяли много — потом не заказывают»: " +
                               "по группам размера (относительно обычного заказа пары и по кг) — сколько визитов и дней до следующего заказа, " +
                               "конверсия визитов после заказа по номеру (ConversionPct), сколько ещё ждут (Waiting), и корреляция. Сравни " +
                               "крупные с обычными цифрами. Другой разрез (по сумме, категориям, регионам, типам точек) — analytik_sql по " +
                               "linko.Orders и linko.Visits. Это наблюдение, а не причина.",
            };
            return new ToolResult(true, JsonSerializer.Serialize(payload, Json), null,
                [new DataSource("Linko — визиты и заказы ТП", $"{report.From:dd.MM.yyyy} — {report.To:dd.MM.yyyy}", null)]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ToolResult.Failure(Name, ToolFailures.InvalidArguments, ex.Message);
        }
    }

    private static double? Pct(double? share) => share is { } s ? Math.Round(s * 100, 1) : null;

    private static object Size(OrderSizeGroup g) => new
    {
        g.Label, g.Orders, AverageKg = Round(g.AverageKg), g.NextOrders, g.NextOnVisit, g.NextWithoutVisit, g.Waiting,
        ByVisitAfterOrder = g.Buckets.Select(b => new { Visit = b.Label, Orders = b.Orders, Reached = b.Reached, ConversionPct = Pct(b.Conversion), SharePct = Pct(b.Share) }),
        AverageVisitsToNext = Round(g.AverageVisitsToNext), MedianVisitsToNext = g.MedianVisitsToNext, NextOnFirstVisitPct = Pct(g.NextOnFirstVisitShare),
        AverageDaysToNext = Round(g.AverageDaysToNext), MedianDaysToNext = g.MedianDaysToNext,
    };

    private static double? Round(double? v) => v is { } x ? Math.Round(x, 2) : null;
}

/// <summary>
/// sales_agent_day — день ТП: куда ходил (точки по порядку отметок, по маршруту и вне, пропущенные и предстоящие точки маршрута), что продал (заказы
/// со строками), что не продал (визиты без заказа: последний заказ точки и что она обычно берёт; в заказе — чего из обычного не взяли), минуты
/// между отметками и итог дня. ТП видит только свой день, СВ — ТП своего региона.
/// </summary>
public sealed class AgentDayTool(QueryEngine engine, IAppDbContext db, SemanticContextLoader context, AgentDayService days, SalesDataLoader loader)
    : VisitTool(engine, db, context)
{
    public const string ToolName = "sales_agent_day";
    private const int LinesPerOrder = 12;

    public override string Name => ToolName;
    public override string Title => "День ТП: визиты и продажи";

    public override string Description =>
        "Подневная аналитика ТП за один день: куда ходил (точки по порядку отметок визитов, по маршруту и вне, пропущенные точки маршрута), что " +
        "продал (заказы по точкам: сумма, кг, товары), что не продал (визиты без заказа — когда точка заказывала в последний раз и что обычно " +
        "берёт; в заказе — чего из обычного не взяли), какой это визит в точку после её прошлого заказа, минуты между отметками, первая и последняя " +
        "отметка, страйк. Время на точке Linko не передаёт — только минуты между отметками.";

    public override string? UsageHint =>
        "«Что делал ТП вчера», «куда ходил», «что продал и что не продал», «сколько был на точке», «маршрут дня ТП» — sales_agent_day (агент — id или " +
        "часть имени, дата). Для нескольких дней — несколько вызовов.";

    public override JsonElement InputSchema { get; } = Schema(
        ("agent", new { type = "string", description = "ТП: id Linko или часть имени (обязательно)." }),
        ("date", new { type = "string", description = "День YYYY-MM-DD (по умолчанию — сегодня)." }));

    protected override async Task<ToolResult> RunAsync(ToolContext c, ToolArgs args, CancellationToken ct)
    {
        var text = args.Str("agent") ?? throw new ToolArgumentException("Нужен ТП: id Linko или часть имени.");
        var day = Date(args.Str("date"), "date") ?? FieldClock.Today;
        var agent = await AgentAsync(text, ct);
        var principal = await PrincipalAsync(c, ct);
        if (!await CanSeeAsync(principal.Sales, agent, day, ct))
        {
            return ToolResult.Failure(Name, ToolFailures.Forbidden, "Этот ТП вне вашей области «Продаж»: ТП видит только свой день, СВ и РМ — ТП своих регионов.");
        }

        var view = await days.BuildAsync(agent, day, ct);
        if (view is null)
        {
            return ToolResult.Failure(Name, ToolFailures.NotFound, $"ТП {agent} в Linko нет.");
        }

        var payload = new
        {
            Agent = new { Id = view.AgentId, Name = view.Agent },
            Date = view.Day.ToString("yyyy-MM-dd"),
            view.Summary,
            Stops = view.Stops.Select(s => new
            {
                s.No, s.Market, s.Address, s.MarketType, s.Status, s.InPlan, At = s.At?.ToString("HH:mm"), s.GapMinutes, s.VisitNumber,
                OrdersSum = s.Sum, OrdersKg = s.Kg,
                Orders = s.Orders.Select(o => new
                {
                    At = o.At.ToString("HH:mm"), o.Status, o.Sum, o.Kg,
                    Lines = o.Lines.Take(LinesPerOrder).Select(l => new { l.Product, l.Qty, l.Kg, l.Sum }), MoreLines = Math.Max(0, o.Lines.Count - LinesPerOrder),
                }),
                LastOrderDay = s.LastOrderDay?.ToString("yyyy-MM-dd"), s.Usual, s.UsualNotOrdered,
            }),
            view.Notes,
            VisitsSyncedAt = view.VisitsSyncedAt?.ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-dd HH:mm"),
            Link = $"/sales/agents/{view.AgentId}/day?date={view.Day:yyyy-MM-dd}",
            Instructions = "Status: done — визит выполнен, missed — точка маршрута пропущена, planned — ещё впереди, no_visit — заказ без визита. At — отметка " +
                           "Linko; GapMinutes — минуты с прошлой отметки (время на прошлой точке вместе с дорогой) — не называй это временем на точке. " +
                           "Что не продал — визиты без заказа (LastOrderDay, Usual) и UsualNotOrdered у заказов.",
        };
        return new ToolResult(true, JsonSerializer.Serialize(payload, Json), null,
            [new DataSource($"Продажи → ТП → День ({view.Agent})", view.Day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), payload.Link)]);
    }

    /// <summary>Как страница ТП «Продаж»: вся организация — любой; ТП — только он; регион — ТП, чей регион месяца в области.</summary>
    private async Task<bool> CanSeeAsync(SalesRowScope scope, long agent, DateOnly day, CancellationToken ct)
    {
        if (!scope.Restricted)
        {
            return true;
        }

        if (scope.Level == SalesRowScope.AgentLevel)
        {
            return scope.AgentId == agent;
        }

        var analytics = await loader.LoadAsync(day.Year, day.Month, ct);
        return analytics.HasAgent(agent) && Guid.TryParse(analytics.CachedAgent(agent).RegionId, out var region) && scope.RegionIds.Contains(region);
    }
}

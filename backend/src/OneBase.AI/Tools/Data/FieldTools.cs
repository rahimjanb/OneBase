using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Field;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Sales Base для консультанта: состояние команды за день — продажи против плана, визиты, маршруты, задачи, проблемы агентов
/// и рекомендации AI. Данные — в пределах зоны того, кто спрашивает (РМ — организация или филиалы, супервайзер — команда).
/// </summary>
internal sealed class GetFieldTeamTool(FieldAccess access, FieldDashboardService dashboards, FieldRecommendationService recommendations, IUserPermissions permissions) : DataTool
{
    public override string Name => "get_field_team";
    public override string Title => "Sales Base: работа команды за день";
    public override string Source => KnowledgeSources.FieldSales;
    public override string? RequiredPermission => Permissions.FieldUse;

    public override string Description =>
        "Полевые продажи (Sales Base) за день: заказы агентов против дневного плана, выполнение месячного плана, визиты, маршруты (посещено/пропущено), " +
        "задачи (открыто/просрочено), статус каждого агента (не начал/на маршруте/завершил/проблема) и блок «требует внимания», " +
        "а также рекомендации AI, ждущие решения (падение продаж точек, давно не посещали, отставание от плана). " +
        "Вопросы: «почему команда не выполняет план», «кто хуже выполняет маршрут», «какие точки требуют визита», «кому дать больше точек».";

    public override string? UsageHint => "Для вопросов о работе агентов, маршрутах, визитах и задачах полевой команды. Дата — по умолчанию сегодня; если данных за сегодня ещё нет, бери вчера.";

    public override JsonElement InputSchema { get; } = Schema(
        ("date", new { type = "string", description = "День yyyy-MM-dd. Без даты — сегодня." }),
        ("team", new { type = "string", description = "Команда: название (например «Ташкент»). Без команды — вся зона пользователя." }));

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var userId = context.UserId ?? throw new ToolArgumentException("Данные Sales Base отдаются только от имени пользователя.");
        var granted = await permissions.GetAsync(userId, ct);
        FieldScope scope;
        try
        {
            scope = await access.ResolveAsync(userId, granted.Contains(Permissions.FieldUse), granted.Contains(Permissions.FieldManage), ct);
        }
        catch (FieldForbiddenException e)
        {
            throw new ToolArgumentException(e.Message);
        }
        if (scope.IsAgent)
        {
            throw new ToolArgumentException("Обзор команды доступен супервайзеру и РМ.");
        }

        var date = DateOnly.TryParse(args.Str("date"), out var d) ? d : FieldClock.Today;
        var dashboard = await dashboards.DashboardAsync(scope, date, null, null, null, ct);
        var team = args.Str("team");
        var rows = dashboard.AgentRows.Where(r => team is null || (r.TeamName ?? string.Empty).Contains(team, StringComparison.OrdinalIgnoreCase)).ToList();
        var pending = await recommendations.ListAsync(scope, new FieldRecommendationQuery("pending", null, null, 1, 15), ct);

        return Data(new
        {
            Date = date.ToString("yyyy-MM-dd"),
            DataAsOf = dashboard.DataAsOf?.ToString("yyyy-MM-dd"),
            Totals = new
            {
                Agents = rows.Count,
                Active = rows.Count(r => r.OrdersToday > 0 || r.Visits > 0),
                SalesToday = R(rows.Sum(r => r.SumToday)),
                KgToday = R(rows.Sum(r => r.KgToday)),
                DailyPlanKg = R(rows.Sum(r => r.DailyPlanKg ?? 0)),
                MonthKg = R(rows.Sum(r => r.MonthKg)),
                MonthPlanKg = R(rows.Sum(r => r.PlanKg ?? 0)),
                ExpectedSharePct = Pct(dashboard.Month.Expected),
                Visits = rows.Sum(r => r.Visits),
                RoutePoints = rows.Sum(r => r.RoutePlanned),
                RouteVisited = rows.Sum(r => r.RouteVisited),
                RouteSkipped = rows.Sum(r => r.RouteSkipped),
                TasksOpen = rows.Sum(r => r.TasksOpen),
                TasksOverdue = rows.Sum(r => r.TasksOverdue),
            },
            Agents = rows.OrderBy(r => r.MonthShareKg ?? 9).Take(40).Select(r => new
            {
                r.Name,
                Team = r.TeamName,
                Status = r.Status.ToString(),
                SalesToday = R(r.SumToday),
                DayPlanSharePct = Pct(r.DayShareKg),
                MonthSharePct = Pct(r.MonthShareKg),
                BehindPacePp = R(r.BehindPp, 0),
                r.Visits,
                Route = r.RoutePlanned > 0 ? $"{r.RouteVisited}/{r.RoutePlanned}, пропущено {r.RouteSkipped}" : null,
                r.TasksOpen,
                r.TasksOverdue,
                Notes = r.StatusNote,
            }),
            Attention = dashboard.Attention.Select(a => new { a.Title, Details = string.Join("; ", a.Details) }),
            PendingRecommendations = pending.Items.Select(p => new { Kind = p.Kind.ToString(), p.Title, p.Reason, Agent = p.AgentName, ConfidencePct = Math.Round(p.Confidence * 100) }),
            Note = "Продажи за день — заказы агентов (дата создания, без отменённых); месяц — доставленные заказы Linko; план — staff_balance Linko в кг.",
        }, new DataSource("Sales Base → Дашборд команды", date.ToString("dd.MM.yyyy"), $"/field?date={date:yyyy-MM-dd}"));
    }
}

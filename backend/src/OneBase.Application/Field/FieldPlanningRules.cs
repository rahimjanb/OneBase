using System.Globalization;
using OneBase.Domain.Field;

namespace OneBase.Application.Field;

/// <summary>
/// Правила AI-планирования первого этапа: прозрачные, на цифрах Linko и Sales Base. Каждая рекомендация несёт причину,
/// исходные данные и уверенность. Чистые функции — проверяются тестами; сохранение и решения — в FieldRecommendationService.
/// </summary>
public static class FieldPlanningRules
{
    /// <summary>Сколько рекомендаций по точкам предлагать одному агенту за один прогон — чтобы не завалить супервайзера.</summary>
    public const int PerAgentLimit = 5;

    public sealed record MarketSnapshot(
        long MarketId,
        string Name,
        Guid? AgentId,
        Guid? SupervisorId,
        decimal Sales30,
        decimal SalesPrev30,
        decimal Sales90,
        DateOnly? LastOrderDate,
        DateOnly? LastVisitDate,
        FieldPriority Priority);

    public sealed record AgentPace(Guid AgentId, string Name, Guid? SupervisorId, decimal? PlanKg, decimal FactKg);

    public sealed record SkippedPoint(long MarketId, string Name, Guid AgentId, Guid? SupervisorId, DateOnly Date, string? Note);

    public sealed record OrphanMarket(long MarketId, string Name, string Reason, decimal Sales90, Guid SuggestedAgentId, string SuggestedAgentName, Guid? SupervisorId);

    public sealed record Draft(
        FieldRecommendationKind Kind,
        string Key,
        string Title,
        string Reason,
        object Source,
        decimal Confidence,
        Guid? AgentId,
        Guid? SupervisorId,
        long? MarketId,
        FieldPriority Priority,
        DateOnly? DueDate,
        decimal Score);

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static string Money(decimal v) => v switch
    {
        >= 1_000_000_000 => $"{(v / 1_000_000_000).ToString("0.##", Ru)} млрд",
        >= 1_000_000 => $"{(v / 1_000_000).ToString("0.#", Ru)} млн",
        >= 1_000 => $"{(v / 1_000).ToString("0", Ru)} тыс",
        _ => v.ToString("0", Ru),
    };

    /// <summary>Рекомендации по точкам: падение продаж, потерянный клиент, давно не посещали. Не больше PerAgentLimit на агента.</summary>
    public static List<Draft> ForMarkets(IEnumerable<MarketSnapshot> markets, FieldSettings settings, DateOnly today)
    {
        var drafts = new List<Draft>();
        foreach (var m in markets)
        {
            if (m.AgentId is null)
            {
                continue;
            }

            if (Lost(m, settings, today) is { } lost)
            {
                drafts.Add(lost);
            }
            else if (Decline(m, settings, today) is { } decline)
            {
                drafts.Add(decline);
            }
            else if (NotVisited(m, settings, today) is { } notVisited)
            {
                drafts.Add(notVisited);
            }
        }

        return drafts
            .GroupBy(d => d.AgentId)
            .SelectMany(g => g.OrderByDescending(d => d.Score).Take(PerAgentLimit))
            .OrderByDescending(d => d.Score)
            .ToList();
    }

    /// <summary>Продажи за 30 дней упали на DeclinePct% и больше относительно прошлых 30 дней.</summary>
    public static Draft? Decline(MarketSnapshot m, FieldSettings s, DateOnly today)
    {
        // SalesPrev30 = 0 (новый клиент) — падения нет; иначе при пороге 0 деление на ноль.
        if (m.SalesPrev30 <= 0 || m.SalesPrev30 < s.DeclineMinSales || m.Sales30 <= 0)
        {
            return null;
        }

        var drop = (m.SalesPrev30 - m.Sales30) / m.SalesPrev30;
        if (drop * 100 < s.DeclinePct)
        {
            return null;
        }

        var pct = Math.Round(drop * 100);
        return new Draft(
            FieldRecommendationKind.SalesDecline,
            $"decline:{m.MarketId}",
            $"Посетить «{m.Name}» в течение 2 дней",
            $"Продажи точки за 30 дней снизились на {pct}%: {Money(m.SalesPrev30)} → {Money(m.Sales30)} сум.",
            new { m.Sales30, m.SalesPrev30, m.Sales90, DropPct = pct, m.LastVisitDate, m.LastOrderDate },
            Math.Min(0.95m, Math.Round(0.55m + drop * 0.4m, 2)),
            m.AgentId,
            m.SupervisorId,
            m.MarketId,
            drop >= 0.5m ? FieldPriority.High : FieldPriority.Medium,
            today.AddDays(2),
            m.SalesPrev30 - m.Sales30);
    }

    /// <summary>Точка не покупает 30 дней, а до этого покупала заметно.</summary>
    public static Draft? Lost(MarketSnapshot m, FieldSettings s, DateOnly today)
    {
        if (m.Sales30 > 0 || m.SalesPrev30 < s.DeclineMinSales / 2)
        {
            return null;
        }

        var days = m.LastOrderDate is { } last ? today.DayNumber - last.DayNumber : (int?)null;
        return new Draft(
            FieldRecommendationKind.LostCustomer,
            $"lost:{m.MarketId}",
            $"Вернуть клиента «{m.Name}»",
            $"Точка не покупает {(days is { } d ? $"{d} дн." : "больше 30 дней")}, а за прошлые 30 дней купила на {Money(m.SalesPrev30)} сум.",
            new { m.Sales30, m.SalesPrev30, m.Sales90, DaysSinceOrder = days, m.LastVisitDate },
            0.8m,
            m.AgentId,
            m.SupervisorId,
            m.MarketId,
            FieldPriority.High,
            today.AddDays(3),
            m.SalesPrev30 * 1.2m);
    }

    /// <summary>Точка с продажами за 90 дней не посещалась NotVisitedDays дней и дольше.</summary>
    public static Draft? NotVisited(MarketSnapshot m, FieldSettings s, DateOnly today)
    {
        if (m.Sales90 <= 0)
        {
            return null;
        }

        var days = m.LastVisitDate is { } last ? today.DayNumber - last.DayNumber : (int?)null;
        if (days is { } d && d < s.NotVisitedDays)
        {
            return null;
        }

        // Мелкие точки (оборот меньше пятой части порога падения в месяц) не стоят отдельной задачи.
        var monthly = m.Sales90 / 3;
        if (monthly < s.DeclineMinSales / 5)
        {
            return null;
        }
        return new Draft(
            FieldRecommendationKind.NotVisited,
            $"notvisited:{m.MarketId}",
            $"Давно не посещали «{m.Name}»",
            days is { } n
                ? $"Последний визит {n} дн. назад; в среднем точка покупает на {Money(monthly)} сум в месяц."
                : $"Визитов нет; в среднем точка покупает на {Money(monthly)} сум в месяц.",
            new { DaysSinceVisit = days, m.LastVisitDate, m.Sales90, MonthlyAvg = Math.Round(monthly), m.Sales30 },
            days is null ? 0.6m : Math.Min(0.9m, Math.Round(0.5m + (decimal)days.Value / (s.NotVisitedDays * 4), 2)),
            m.AgentId,
            m.SupervisorId,
            m.MarketId,
            m.Priority is FieldPriority.High or FieldPriority.Urgent ? FieldPriority.High : FieldPriority.Medium,
            today.AddDays(3),
            monthly * 0.5m);
    }

    /// <summary>Агент отстаёт от темпа плана на BehindPlanPct п.п. и больше (не раньше 5-го рабочего дня месяца).</summary>
    public static List<Draft> ForAgents(IEnumerable<AgentPace> agents, FieldSettings s, DateOnly today)
    {
        var drafts = new List<Draft>();
        if (FieldKpi.ElapsedWorkingDays(today) < 5)
        {
            return drafts;
        }

        foreach (var a in agents)
        {
            if (FieldKpi.BehindPace(a.FactKg, a.PlanKg, today) is not { } behind || behind < s.BehindPlanPct)
            {
                continue;
            }

            var share = Math.Round(a.FactKg / a.PlanKg!.Value * 100);
            var expected = Math.Round(FieldKpi.ExpectedShare(today) * 100);
            drafts.Add(new Draft(
                FieldRecommendationKind.AgentBehindPlan,
                $"behind:{a.AgentId}:{today:yyyy-MM}",
                $"Агент {a.Name}: выполнение плана {share}%",
                $"К {today:dd.MM} выполнено {share}% месячного плана при ожидаемых {expected}%: {Math.Round(a.FactKg)} из {Math.Round(a.PlanKg.Value)} кг. Нужен разбор маршрута или совместный выезд.",
                new { a.PlanKg, a.FactKg, SharePct = share, ExpectedPct = expected, BehindPp = Math.Round(behind) },
                Math.Min(0.95m, Math.Round(0.6m + behind / 200, 2)),
                a.AgentId,
                a.SupervisorId,
                null,
                behind >= 35 ? FieldPriority.High : FieldPriority.Medium,
                today.AddDays(1),
                behind * 1000));
        }

        return drafts;
    }

    /// <summary>Пропущенные вчера (и раньше) точки маршрута — повторный визит.</summary>
    public static List<Draft> ForSkipped(IEnumerable<SkippedPoint> points, DateOnly today) =>
        points.Select(p => new Draft(
                FieldRecommendationKind.SkippedPoint,
                $"skipped:{p.MarketId}:{p.Date:yyyyMMdd}",
                $"Повторить визит в «{p.Name}»",
                $"Точка была в маршруте {p.Date:dd.MM}, но визит не состоялся{(string.IsNullOrWhiteSpace(p.Note) ? "." : $": {p.Note}")}",
                new { p.Date, p.Note },
                0.7m,
                p.AgentId,
                p.SupervisorId,
                p.MarketId,
                FieldPriority.Medium,
                today.AddDays(1),
                1))
            .ToList();

    /// <summary>Точка без действующего агента — кому её отдать.</summary>
    public static List<Draft> ForOrphans(IEnumerable<OrphanMarket> markets, DateOnly today) =>
        markets.Select(m => new Draft(
                FieldRecommendationKind.Reassign,
                $"reassign:{m.MarketId}",
                $"Назначить «{m.Name}» агенту {m.SuggestedAgentName}",
                $"{m.Reason}. За 90 дней точка купила на {Money(m.Sales90)} сум. Предлагаемый агент работает в этом филиале.",
                new { m.Sales90, m.Reason, SuggestedAgentId = m.SuggestedAgentId },
                0.65m,
                m.SuggestedAgentId,
                m.SupervisorId,
                m.MarketId,
                m.Sales90 > 0 ? FieldPriority.High : FieldPriority.Medium,
                today.AddDays(2),
                m.Sales90))
            .ToList();
}

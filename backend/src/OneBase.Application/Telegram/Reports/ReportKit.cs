using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Что нужно построителю отчёта: запуск, область (уже проверенная по данным «Продаж»), день, момент формирования, язык, данные, срез
/// «13:00» (время дневного промежуточного запуска) и срез запуска (Cutoff: факт — заказы, созданные до него). Peers — ТП региона для места ТП
/// в рейтинге недели (наружу — только место, без цифр коллег).
/// </summary>
public sealed record ReportContext(
    ReportJobInfo Job,
    ResolvedScope Scope,
    DateOnly Date,
    DateTimeOffset Now,
    string Language,
    ReportFacts Facts,
    TimeOnly MiddayCutoff,
    TimeOnly Cutoff,
    int NoPurchaseDays = 30,
    IReadOnlyDictionary<long, NoPurchaseAgent>? NoPurchase = null,
    TimeOnly? JobTime = null,
    IReadOnlyList<Sales.Metrics.ReportPerson>? Peers = null)
{
    public string T(string ru) => ReportTexts.Get(Language, ru);

    public string F(string ru, params object[] args) => ReportTexts.Format(Language, ru, args);

    public string Role => Scope.Scope.Role;

    /// <summary>Вечерний срез показывает и «13:00», и «18:00».</summary>
    public bool Evening => Job.Slot == "evening";

    /// <summary>Подпись среза: у промежуточных — момент формирования («13:00»), у вечернего (весь день) — время запуска («18:00»).</summary>
    public string CutoffText => (Cutoff == TimeOnly.MaxValue ? JobTime ?? Job.Time : Cutoff).ToString("HH:mm");
    public string MiddayText => MiddayCutoff.ToString("HH:mm");
}

/// <summary>Показатели одного ТП или региона за день: план, факт к 13:00 и к срезу, визиты, заказы.</summary>
public sealed record DayMetrics(
    decimal? PlanSum,
    decimal? PlanKg,
    int? VisitPlan,
    decimal FactSum13,
    decimal FactKg13,
    decimal FactSum,
    decimal FactKg,
    int Visits13,
    int Visits,
    int Orders13,
    int Orders,
    int Agents,
    int WithoutPlan,
    TimeOnly? LastOrder)
{
    public decimal? Share13 => ReportFormat.Share(FactSum13, PlanSum);
    public decimal? Share => ReportFormat.Share(FactSum, PlanSum);
    public decimal? ShareKg13 => ReportFormat.Share(FactKg13, PlanKg);
    public decimal? ShareKg => ReportFormat.Share(FactKg, PlanKg);

    /// <summary>Средняя цена плана, сум/кг.</summary>
    public decimal? PricePerKg => PlanSum is { } s && PlanKg is > 0 ? s / PlanKg.Value : null;

    public static DayMetrics Sum(IReadOnlyList<DayMetrics> parts) => new(
        parts.Any(p => p.PlanSum is not null) ? parts.Sum(p => p.PlanSum ?? 0) : null,
        parts.Any(p => p.PlanKg is not null) ? parts.Sum(p => p.PlanKg ?? 0) : null,
        parts.Any(p => p.VisitPlan is not null) ? parts.Sum(p => p.VisitPlan ?? 0) : null,
        parts.Sum(p => p.FactSum13), parts.Sum(p => p.FactKg13), parts.Sum(p => p.FactSum), parts.Sum(p => p.FactKg),
        parts.Sum(p => p.Visits13), parts.Sum(p => p.Visits), parts.Sum(p => p.Orders13), parts.Sum(p => p.Orders),
        parts.Sum(p => p.Agents), parts.Sum(p => p.WithoutPlan), parts.Max(p => p.LastOrder));
}

/// <summary>Общие расчёты построителей: показатели дня, строки областей, замечания о данных, подписи.</summary>
public static class ReportKit
{
    /// <summary>Показатели ТП за день. План визитов — визиты Linko в плане на день; известен, если на день загружен хоть один визит области.</summary>
    public static DayMetrics Day(ReportContext c, long agent, DateOnly day, bool visitPlanKnown)
    {
        var orders = c.Facts.OrdersOf(agent, day, day, c.Cutoff == TimeOnly.MaxValue ? null : c.Cutoff).ToList();
        var by13 = orders.Where(o => o.Time < c.MiddayCutoff).ToList();
        var visits = c.Facts.VisitsOf(agent, day, day).ToList();
        var done = visits.Where(v => v.Done && (c.Cutoff == TimeOnly.MaxValue || v.Time < c.Cutoff)).ToList();
        var planSum = Whole(c.Facts.DayPlanSum(agent, day));
        return new DayMetrics(
            planSum,
            Whole(c.Facts.DayPlanKg(agent, day)),
            visitPlanKnown ? visits.Count(v => v.InPlan) : null,
            by13.Sum(o => o.Sum), by13.Sum(o => o.Kg), orders.Sum(o => o.Sum), orders.Sum(o => o.Kg),
            done.Where(v => v.Time < c.MiddayCutoff).Select(v => v.MarketId).Distinct().Count(), done.Select(v => v.MarketId).Distinct().Count(),
            by13.Count, orders.Count, 1, planSum is null ? 1 : 0, orders.Count == 0 ? null : orders.Max(o => o.Time));
    }

    /// <summary>Есть ли на день хоть один визит у ТП области — иначе план визитов «не загружен», а не ноль.</summary>
    public static bool VisitPlanKnown(ReportContext c, DateOnly from, DateOnly to) =>
        c.Facts.Visits.Any(v => v.Day >= from && v.Day <= to);

    /// <summary>Строки отчёта: у РМ — регионы, у СВ — ТП региона, у ТП — он сам. Name — подпись строки.</summary>
    public static IReadOnlyList<(string Name, IReadOnlyList<ReportPerson> Agents)> Units(ReportContext c) => c.Role switch
    {
        TelegramReportRoles.Rm => c.Scope.Regions.Select(r => (r.Name, r.Agents)).ToList(),
        _ => c.Scope.Agents.Select(a => (a.Name, (IReadOnlyList<ReportPerson>)[a])).ToList(),
    };

    /// <summary>«Регион» у РМ, «ТП» у СВ.</summary>
    public static string UnitTitle(ReportContext c) => c.Role == TelegramReportRoles.Rm ? c.T("Регион") : c.T("ТП");

    /// <summary>Подпись итоговой строки: «Итого РМ …», «Итого по региону».</summary>
    public static string TotalTitle(ReportContext c) => c.Role == TelegramReportRoles.Rm ? c.T("Итого") : c.T("Итого по региону");

    /// <summary>
    /// Замечания о данных: заказы Linko не обновлялись после начала дня (факт неизвестен — отчёт неполный) или обновлялись давно
    /// (факт на время обновления). Возвращает, неполный ли отчёт.
    /// </summary>
    public static bool Freshness(ReportContext c, List<string> notes, bool needsFact)
    {
        if (!needsFact)
        {
            return false;
        }

        var dayStart = ReportSchedules.At(c.Date, TimeOnly.MinValue, null);
        if (c.Facts.OrdersAsOf is not { } at || at < dayStart)
        {
            notes.Add(c.Facts.OrdersAsOf is { } old
                ? c.F("Заказы Linko за этот день ещё не загружены (последнее обновление {0}) — факт неполный", old.ToOffset(c.Now.Offset).ToString("dd.MM HH:mm"))
                : c.T("Заказы Linko ещё не загружались — факт неизвестен"));
            return true;
        }

        if (c.Now - at > TimeSpan.FromMinutes(30))
        {
            notes.Add(c.F("Данные Linko на {0}: заказы, введённые позже, войдут в следующий отчёт", at.ToOffset(c.Now.Offset).ToString("HH:mm")));
        }

        return false;
    }

    /// <summary>Кто без плана в Linko — отдельной строкой замечаний.</summary>
    public static void WithoutPlan(ReportContext c, List<string> notes, IEnumerable<(string Name, DayMetrics M)> rows)
    {
        var names = rows.Where(r => r.M.PlanSum is null).Select(r => r.Name).ToList();
        if (names.Count > 0)
        {
            notes.Add(c.F("Нет плана на день в Linko: {0}", string.Join(", ", names.Take(10)) + (names.Count > 10 ? $" +{names.Count - 10}" : "")));
        }
    }

    public static string Money(decimal? v) => ReportFormat.Number(v);

    /// <summary>Таблица без колонки (колонка нужна только одной из ролей).</summary>
    public static ReportTable WithoutColumn(ReportTable table, int index) => table with
    {
        Columns = table.Columns.Where((_, i) => i != index).ToList(),
        Rows = table.Rows.Select(r => (IReadOnlyList<ReportCell>)r.Where((_, i) => i != index).ToList()).ToList(),
        Total = table.Total?.Where((_, i) => i != index).ToList(),
    };

    /// <summary>
    /// Целые сумы и килограммы: иначе итог (сумма точных значений) расходился бы на 1 с суммой показанных строк (план на день —
    /// месячный ÷ рабочие дни — дробный). Меньше 1 — с десятой.
    /// </summary>
    public static decimal? Whole(decimal? value) => value is { } v
        ? Math.Abs(v) is > 0 and < 1 ? Math.Round(v, 1, MidpointRounding.AwayFromZero) : Math.Round(v, 0, MidpointRounding.AwayFromZero)
        : null;

    /// <summary>Подпись картинки и сообщения: «1. Дневной план · 13:00 — промежуточный итог».</summary>
    public static string Title(ReportContext c, string? time = null) =>
        $"{c.T(ReportCatalog.Title(c.Job.Report))} · {time ?? (c.JobTime ?? c.Job.Time).ToString("HH:mm")} — {c.T(ReportCatalog.SlotTitle(c.Job.Report, c.Job.Slot))}";

    /// <summary>
    /// Готовый отчёт. Подпись сообщения всегда начинается строкой «Отчёт · Область · День»: без картинки (или в списке чатов) видно, что это
    /// и о ком.
    /// </summary>
    public static ReportDocument Document(ReportContext c, string title, string period, IReadOnlyList<ReportKpi> kpis, IReadOnlyList<ReportTable> tables,
        IReadOnlyList<string> problems, IReadOnlyList<string> notes, string caption, string summary, bool incomplete, bool applies = true, string? skip = null)
    {
        var head = $"{c.T(ReportCatalog.Title(c.Job.Report))} · {c.Scope.Title} · {period}";
        if (!caption.StartsWith(head, StringComparison.Ordinal))
        {
            caption = $"{head}\n{caption}";
        }

        return new(c.Job.Code, c.Job.Report, c.Role, title, c.Scope.Title, c.Scope.Manager, c.Date, period, c.Now, c.Facts.OrdersAsOf?.ToOffset(c.Now.Offset),
            c.Language, kpis, tables, problems, notes, caption, summary, incomplete, applies, skip);
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Артикул итога дня: продано за день (кг, сумма, ТТ), факт месяца по день итога, метка ТОП и главное фото (160 px; нет — null).</summary>
public sealed record CategoryDaySku(long ProductId, string Name, string? Code, decimal Kg, decimal Revenue, int Akb, decimal MonthKg, bool IsTop, byte[]? Photo);

/// <summary>Строка разбивки итога дня: регион (РМ и вся страна) или ТП (СВ) — кг за день, план дня категории и выполнение, сумма, АКБ, заказы.</summary>
public sealed record CategoryDayRow(string Name, decimal Kg, decimal? DayPlanKg, decimal? DayExecution, decimal Revenue, int Akb, int Orders);

/// <summary>
/// Итог дня одной категории в охвате получателя: продано за день (кг, сумма, ТТ, заказы, артикулы — из SkusInMonth, что продавались в
/// месяце), план дня (остаток плана месяца ÷ рабочие дни с дня итога, как «Дневной план»; PlanShared — план общий на несколько категорий,
/// PlanDone — план месяца уже выполнен), месяц по день итога и его выполнение, прошлый рабочий день и тот же день неделю назад, средний
/// рабочий день двух недель, килограммы по дням (14 дней по день итога), разбивка (BreakdownKind: regions, agents, null), артикулы дня с
/// фото и артикулы месяца, которых за день не было.
/// </summary>
public sealed record CategoryDayCard(
    string Name,
    string? Alias,
    decimal Kg,
    decimal Revenue,
    int Akb,
    int Orders,
    int Skus,
    int SkusInMonth,
    decimal? DayPlanKg,
    decimal? DayExecution,
    string? PlanShared,
    bool PlanDone,
    decimal MonthKg,
    decimal? MonthPlanKg,
    decimal? MonthExecution,
    DateOnly PrevDay,
    decimal PrevDayKg,
    DateOnly WeekAgo,
    decimal WeekAgoKg,
    decimal? AverageKg,
    IReadOnlyList<CategoryDayPoint> Days,
    string? BreakdownKind,
    IReadOnlyList<CategoryDayRow> Breakdown,
    IReadOnlyList<CategoryDaySku> Top,
    IReadOnlyList<string> Silent,
    int SilentMore);

/// <summary>
/// Итоги дня по категориям одного отчёта: момент («morning» — прошлый рабочий день, «evening» — день запуска), день итога, отчётный день
/// данных «Продаж» (DataThrough), праздник дня итога (null — рабочий день), карточки в порядке выбора — по картинке на категорию.
/// </summary>
public sealed record CategoryDayCards(string Slot, DateOnly Day, DateOnly DataThrough, string? Holiday, IReadOnlyList<CategoryDayCard> Cards);

/// <summary>Документ отчёта «11. Итоги дня по категориям» из готовых карточек: подпись, краткая сводка, замечания о данных. Без базы — проверяется тестами.</summary>
public static class CategoryDayReport
{
    private const int CaptionLimit = 1000;

    /// <summary>Изменение к прошлому: +12% / −8%; прошлого нет (0) — null.</summary>
    public static decimal? Change(decimal now, decimal before) => before > 0 ? now / before - 1 : null;

    public static ReportDocument Document(ReportJobInfo info, ResolvedScope scope, DateOnly date, CategoryDayCards cards, DateTimeOffset now, DateTimeOffset? dataAsOf,
        string language, TimeOnly? jobTime = null)
    {
        string T(string ru) => ReportTexts.Get(language, ru);
        string F(string ru, params object[] args) => ReportTexts.Format(language, ru, args);

        var title = $"{T(ReportCatalog.Title(info.Report))} · {(jobTime ?? info.Time).ToString("HH:mm", CultureInfo.InvariantCulture)} — {T(ReportCatalog.SlotTitle(info.Report, info.Slot))}";
        var period = cards.Day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var head = $"{T(ReportCatalog.Title(info.Report))} · {scope.Title} · {period}";
        var notes = new List<string>();
        var incomplete = cards.DataThrough < cards.Day;
        if (incomplete)
        {
            notes.Add(F("Продаж за {0} в данных ещё нет — месяц по {1}", period, cards.DataThrough.ToString("dd.MM", CultureInfo.InvariantCulture)));
        }

        if (cards.Holiday is { } holiday)
        {
            notes.Add(F("{0} — праздник «{1}»: план дня не считается", cards.Day.ToString("dd.MM", CultureInfo.InvariantCulture), holiday));
        }

        if (cards.Cards.Count == 0)
        {
            const string skip = "Условие отчёта: ни одной из выбранных категорий нет в «Настройках продаж» — выберите категории в «Карточках категорий».";
            return new ReportDocument(info.Code, info.Report, scope.Scope.Role, title, scope.Title, scope.Manager, date, period, now, dataAsOf, language, [], [], [], notes,
                head, "", incomplete, Applies: false, SkipReason: skip) { CategoryDay = cards };
        }

        var lines = cards.Cards.Select(c => c.DayExecution is { } e
                ? F("{0}: {1} кг за день — {2} плана дня; {3} ТТ", c.Name, ReportFormat.Kg(c.Kg), ReportFormat.Percent(e), ReportFormat.Number(c.Akb))
                : F("{0}: {1} кг за день; {2} ТТ", c.Name, ReportFormat.Kg(c.Kg), ReportFormat.Number(c.Akb)))
            .ToList();
        var caption = head;
        foreach (var line in lines)
        {
            if (caption.Length + line.Length + 1 > CaptionLimit)
            {
                break;
            }

            caption += "\n" + line;
        }

        var kpis = cards.Cards.Select(c => new ReportKpi(c.Name, $"{ReportFormat.Kg(c.Kg)} {T("кг")}",
            c.DayExecution is { } x ? F("{0} плана дня", ReportFormat.Percent(x)) : T("плана дня нет"),
            c.DayExecution is { } y ? y >= 1 ? ReportTone.Good : y >= ReportFormat.RedBelow ? ReportTone.Warn : ReportTone.Bad : ReportTone.Normal)).ToList();
        var summary = string.Join("; ", cards.Cards.Select(c => $"{c.Name} {ReportFormat.Kg(c.Kg)} {T("кг")}" +
                                                                 (c.DayExecution is { } e ? $" ({ReportFormat.Percent(e)})" : "")));
        return new ReportDocument(info.Code, info.Report, scope.Scope.Role, title, scope.Title, scope.Manager, date, period, now, dataAsOf, language, kpis, [], [], notes,
            caption, summary, incomplete) { CategoryDay = cards };
    }
}

/// <summary>
/// Итоги дня по категориям для области получателя — из «Продаж» месяца дня итога (строки продаж с этой датой, те же правила, что у страницы
/// категории): РМ — регионы, СВ — ТП региона (и те, кто за день категорию не продал), ТП — только он. Категории — выбор «Карточек категорий»
/// (по умолчанию или свой у получателя). План дня — как в «Дневном плане»: остаток плана категории на месяц ÷ рабочие дни (пн–сб без
/// праздников) с дня итога по конец месяца.
/// </summary>
public sealed class CategoryDayBuilder(IAppDbContext db, SalesDataLoader loader, IFileStorage storage, ILogger<CategoryDayBuilder> logger)
{
    private const int TopSkus = 8;
    private const int SilentShown = 6;
    private const int RecentDays = 14;
    private const int PhotoSize = 160;
    private static readonly TimeSpan PhotoBudget = TimeSpan.FromSeconds(20);

    public async Task<ReportDocument> BuildAsync(ReportJobInfo info, ResolvedScope resolved, DateOnly date, DateTimeOffset now, string language,
        IReadOnlyList<string>? chosen, TimeOnly jobTime, CancellationToken ct)
    {
        var day = info.IsMorning ? CategoryCardChoice.PreviousWorkDay(date) : date;
        var analytics = await loader.LoadAsync(day.Year, day.Month, ct);
        var holidays = await db.SalesHolidays.AsNoTracking().Where(h => h.Date >= day.AddDays(-45) && h.Date <= day.AddDays(45))
            .ToDictionaryAsync(h => h.Date, h => h.Name, ct);
        var names = chosen is { Count: > 0 }
            ? chosen
            : CategoryCardChoice.Parse(await db.TelegramReportSettings.AsNoTracking().Select(s => s.CategoryCards).FirstOrDefaultAsync(ct));
        var all = analytics.ReportCategoryGroups();
        var picked = names.Count == 0
            ? all
            : names.Select(analytics.ReportCategoryGroup).OfType<long>().Distinct().Select(g => all.FirstOrDefault(x => x.Group == g)).Where(x => x.Name is not null);
        var groups = picked.Take(CategoryCardChoice.Max).ToList();

        var scope = CategoryCardsBuilder.ScopeOf(resolved);
        var byRegions = resolved.Scope.Role == TelegramReportRoles.Rm && (resolved.Regions.Count > 1 || scope.Regions is null);
        var prevDay = PreviousWorkDay(day, holidays);
        var cards = new List<CategoryDayCard>();
        foreach (var (group, name) in groups)
        {
            var month = analytics.CachedCategory(group.ToString(CultureInfo.InvariantCulture), scope)?.Card;
            var totals = analytics.CategoryDayOf(group, scope, day) ?? new CategoryDayTotals(0, 0, 0);
            var plan = analytics.CategoryPlanOf(group, scope);
            var shared = plan is { CategoryId: null } ? plan.Name : null;
            var exact = shared is null ? plan : null;
            var before = analytics.CategoryKgBefore(group, scope, day);
            var dayPlan = DayPlan(exact?.PlanKg, before, day, holidays);
            var monthKg = before + totals.Kg;
            var recent = analytics.CategoryRecentDays(group, scope, day, RecentDays);
            var past = recent.Where(d => d.Day < day && DailyPlanMath.IsWorkingDay(d.Day, holidays)).ToList();
            IReadOnlyList<CategoryDayRow> breakdown = resolved.Scope.Role == TelegramReportRoles.Tp ? []
                : byRegions ? Rows(analytics, group, day, holidays, resolved.Regions.Select(r => (r.Name, new AssortmentScope(null, r.Id, null, false))))
                : Rows(analytics, group, day, holidays, resolved.Regions[0].Agents.Select(a => (a.Name, new AssortmentScope(null, null, a.Id, false))));

            var skus = analytics.CategoryDaySkus(group, scope, day);
            var monthSkus = (month?.Skus ?? []).Where(s => s.Status == SkuStatuses.Selling && s.FactKg > 0).ToList();
            var monthBySku = monthSkus.ToDictionary(s => s.ProductId);
            var sold = skus.Select(s => s.ProductId).ToHashSet();
            var silent = monthSkus.Where(s => !sold.Contains(s.ProductId)).OrderByDescending(s => s.FactKg).ThenBy(s => s.Name).ToList();
            cards.Add(new CategoryDayCard(
                name,
                CategoryCardChoice.AliasOf(name),
                totals.Kg,
                totals.Revenue,
                totals.Akb,
                totals.Orders,
                totals.Skus,
                monthSkus.Count,
                dayPlan?.DailyPlan,
                dayPlan?.DailyPlan is > 0 ? totals.Kg / dayPlan.DailyPlan.Value : null,
                shared,
                dayPlan?.Status == DailyPlanMath.Done,
                monthKg,
                exact?.PlanKg,
                ReportFormat.Share(monthKg, exact?.PlanKg),
                prevDay,
                analytics.CategoryDayOf(group, scope, prevDay)?.Kg ?? 0,
                day.AddDays(-7),
                analytics.CategoryDayOf(group, scope, day.AddDays(-7))?.Kg ?? 0,
                past.Count == 0 ? null : past.Average(d => d.Kg),
                recent,
                breakdown.Count == 0 ? null : byRegions ? CategorySalesCards.Regions : CategorySalesCards.Agents,
                breakdown,
                skus.Take(TopSkus).Select(s => new CategoryDaySku(s.ProductId, s.Name, s.Code, s.Kg, s.Revenue, s.Akb,
                    monthBySku.GetValueOrDefault(s.ProductId)?.FactKg ?? s.Kg, monthBySku.GetValueOrDefault(s.ProductId)?.IsTop ?? false, null)).ToList(),
                silent.Take(SilentShown).Select(s => s.Name).ToList(),
                Math.Max(0, silent.Count - SilentShown)));
        }

        cards = await WithPhotosAsync(cards, ct);
        var payload = new CategoryDayCards(info.Slot, day, analytics.Period.DataThrough, holidays.GetValueOrDefault(day), cards);
        var asOf = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        return CategoryDayReport.Document(info, resolved, date, payload, now, asOf?.ToOffset(now.Offset), language, jobTime);
    }

    /// <summary>Прошлый рабочий день перед day: пн–сб без праздников (не дальше двух недель назад).</summary>
    public static DateOnly PreviousWorkDay(DateOnly day, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var d = day.AddDays(-1);
        for (var i = 0; i < 14 && !DailyPlanMath.IsWorkingDay(d, holidays); i++)
        {
            d = d.AddDays(-1);
        }

        return d;
    }

    /// <summary>План дня категории: плана месяца нет, выходной или праздник — null; иначе — расчёт «Дневного плана».</summary>
    private static DailyPlanRow? DayPlan(decimal? monthPlan, decimal before, DateOnly day, IReadOnlyDictionary<DateOnly, string> holidays) =>
        monthPlan is > 0 && DailyPlanMath.Compute(monthPlan, before, day, holidays) is { Status: DailyPlanMath.Ok or DailyPlanMath.Done } row ? row : null;

    /// <summary>
    /// Строки разбивки: регионы или ТП — кг, план дня категории (их план месяца) и выполнение, сумма, АКБ, заказы. Без продаж за день и без
    /// плана — не показываются; без продаж, но с планом — внизу (кто категорию за день не продал).
    /// </summary>
    private static List<CategoryDayRow> Rows(SalesAnalytics analytics, long group, DateOnly day, IReadOnlyDictionary<DateOnly, string> holidays,
        IEnumerable<(string Name, AssortmentScope Scope)> items) =>
        items
            .Select(x =>
            {
                var totals = analytics.CategoryDayOf(group, x.Scope, day) ?? new CategoryDayTotals(0, 0, 0);
                var plan = analytics.CategoryPlanOf(group, x.Scope);
                var exact = plan is { CategoryId: not null } ? plan : null;
                var dayPlan = DayPlan(exact?.PlanKg, analytics.CategoryKgBefore(group, x.Scope, day), day, holidays)?.DailyPlan;
                return new CategoryDayRow(x.Name, totals.Kg, dayPlan, dayPlan is > 0 ? totals.Kg / dayPlan.Value : null, totals.Revenue, totals.Akb, totals.Orders);
            })
            .Where(r => r.Kg != 0 || r.DayPlanKg is not null)
            .OrderByDescending(r => r.Kg)
            .ThenByDescending(r => r.DayPlanKg ?? 0)
            .ToList();

    /// <summary>Главные фото артикулов дня (160 px): хранилище не отвечает — дальше без фото (на всё не больше 20 секунд).</summary>
    private async Task<List<CategoryDayCard>> WithPhotosAsync(List<CategoryDayCard> cards, CancellationToken ct)
    {
        var ids = cards.SelectMany(c => c.Top.Select(s => s.ProductId)).Distinct().ToList();
        if (ids.Count == 0)
        {
            return cards;
        }

        var files = (await db.SalesProductPhotos.AsNoTracking().Where(p => ids.Contains(p.ProductId))
                .OrderBy(p => p.ProductId).ThenBy(p => p.SortOrder).ThenBy(p => p.CreatedAt)
                .Select(p => new { p.ProductId, p.FileId }).ToListAsync(ct))
            .GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.First().FileId);
        var photos = new Dictionary<long, byte[]>();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(PhotoBudget);
        foreach (var (product, file) in files)
        {
            try
            {
                using var stream = new MemoryStream();
                await storage.DownloadAsync(SalesProductPhotoService.Key(file, PhotoSize), stream, budget.Token);
                photos[product] = stream.ToArray();
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Итоги дня по категориям: фото товара {Product} не загружено — дальше без фото", product);
                break;
            }
        }

        return cards.Select(c => c with { Top = c.Top.Select(s => s with { Photo = photos.GetValueOrDefault(s.ProductId) }).ToList() }).ToList();
    }
}

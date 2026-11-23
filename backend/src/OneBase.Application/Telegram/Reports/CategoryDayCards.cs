using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Артикул итога дня: продано за день (кг, сумма, ТТ), метка ТОП (товар из списка ТОП) и главное фото (160 px; нет — null).</summary>
public sealed record CategoryDaySku(long ProductId, string Name, string? Code, decimal Kg, decimal Revenue, int Akb, bool IsTop, byte[]? Photo);

/// <summary>Строка разбивки итога дня: регион (РМ и вся страна) или ТП (СВ) — кг за день, план дня категории и выполнение, сумма, АКБ, заказы.</summary>
public sealed record CategoryDayRow(string Name, decimal Kg, decimal? DayPlanKg, decimal? DayExecution, decimal Revenue, int Akb, int Orders);

/// <summary>
/// Итог дня одной категории в охвате получателя — только этот день, без месяца и других дней: продано за день (кг, сумма, ТТ, заказы,
/// артикулы), план дня (остаток плана месяца ÷ рабочие дни с дня итога, как «Дневной план»; PlanShared — план общий на несколько
/// категорий, PlanDone — план уже выполнен, план дня 0), разбивка за день (BreakdownKind: regions, agents, null) и артикулы дня с фото.
/// </summary>
public sealed record CategoryDayCard(
    string Name,
    string? Alias,
    decimal Kg,
    decimal Revenue,
    int Akb,
    int Orders,
    int Skus,
    decimal? DayPlanKg,
    decimal? DayExecution,
    string? PlanShared,
    bool PlanDone,
    string? BreakdownKind,
    IReadOnlyList<CategoryDayRow> Breakdown,
    IReadOnlyList<CategoryDaySku> Top);

/// <summary>
/// Итоги дня по категориям одного отчёта: момент («morning» — прошлый рабочий день, «evening» — день запуска), день итога, отчётный день
/// данных «Продаж» (DataThrough), праздник дня итога (null — рабочий день), карточки в порядке выбора — по картинке на категорию.
/// </summary>
public sealed record CategoryDayCards(string Slot, DateOnly Day, DateOnly DataThrough, string? Holiday, IReadOnlyList<CategoryDayCard> Cards)
{
    /// <summary>Разбивка карточки по регионам (РМ и вся страна).</summary>
    public const string Regions = "regions";

    /// <summary>Разбивка карточки по ТП (СВ).</summary>
    public const string Agents = "agents";
}

/// <summary>Документ отчёта «11. Итоги дня по категориям» из готовых карточек: подпись, краткая сводка, замечания о данных. Без базы — проверяется тестами.</summary>
public static class CategoryDayReport
{
    private const int CaptionLimit = 1000;

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
            notes.Add(F("Продаж за {0} в данных ещё нет — последние продажи за {1}", period, cards.DataThrough.ToString("dd.MM", CultureInfo.InvariantCulture)));
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
/// (по умолчанию или свой у получателя). На карточке только день итога; месяц нужен лишь для плана дня — как в «Дневном плане»: остаток
/// плана категории на месяц ÷ рабочие дни (пн–сб без праздников) с дня итога по конец месяца.
/// </summary>
public sealed class CategoryDayBuilder(IAppDbContext db, SalesDataLoader loader, IFileStorage storage, ILogger<CategoryDayBuilder> logger)
{
    private const int TopSkus = 8;
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

        var scope = ScopeOf(resolved);
        var byRegions = resolved.Scope.Role == TelegramReportRoles.Rm && (resolved.Regions.Count > 1 || scope.Regions is null);
        var cards = new List<CategoryDayCard>();
        foreach (var (group, name) in groups)
        {
            var totals = analytics.CategoryDayOf(group, scope, day) ?? new CategoryDayTotals(0, 0, 0);
            var plan = analytics.CategoryPlanOf(group, scope);
            var shared = plan is { CategoryId: null } ? plan.Name : null;
            var exact = shared is null ? plan : null;
            var dayPlan = DayPlan(exact?.PlanKg, analytics.CategoryKgBefore(group, scope, day), day, holidays);
            IReadOnlyList<CategoryDayRow> breakdown = resolved.Scope.Role == TelegramReportRoles.Tp ? []
                : byRegions ? Rows(analytics, group, day, holidays, resolved.Regions.Select(r => (r.Name, new AssortmentScope(null, r.Id, null, false))))
                : Rows(analytics, group, day, holidays, resolved.Regions[0].Agents.Select(a => (a.Name, new AssortmentScope(null, null, a.Id, false))));

            // Метка ТОП — товар из списка ТОП (свойство товара, не продажи месяца).
            var top = (analytics.CachedCategory(group.ToString(CultureInfo.InvariantCulture), scope)?.Card?.Skus ?? []).Where(s => s.IsTop)
                .Select(s => s.ProductId).ToHashSet();
            cards.Add(new CategoryDayCard(
                name,
                CategoryCardChoice.AliasOf(name),
                totals.Kg,
                totals.Revenue,
                totals.Akb,
                totals.Orders,
                totals.Skus,
                dayPlan?.DailyPlan,
                dayPlan?.DailyPlan is > 0 ? totals.Kg / dayPlan.DailyPlan.Value : null,
                shared,
                dayPlan?.Status == DailyPlanMath.Done,
                breakdown.Count == 0 ? null : byRegions ? CategoryDayCards.Regions : CategoryDayCards.Agents,
                breakdown,
                analytics.CategoryDaySkus(group, scope, day).Take(TopSkus)
                    .Select(s => new CategoryDaySku(s.ProductId, s.Name, s.Code, s.Kg, s.Revenue, s.Akb, top.Contains(s.ProductId), null)).ToList()));
        }

        cards = await WithPhotosAsync(cards, ct);
        var payload = new CategoryDayCards(info.Slot, day, analytics.Period.DataThrough, holidays.GetValueOrDefault(day), cards);
        var asOf = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        return CategoryDayReport.Document(info, resolved, date, payload, now, asOf?.ToOffset(now.Offset), language, jobTime);
    }

    /// <summary>Охват страницы категории для области: ТП — агент, СВ — регион, РМ — его регионы, вся страна — республика.</summary>
    public static AssortmentScope ScopeOf(ResolvedScope resolved) => resolved.Scope.Role switch
    {
        TelegramReportRoles.Tp => new AssortmentScope(null, null, resolved.Agent!.Id, false),
        TelegramReportRoles.Sv => new AssortmentScope(null, resolved.Regions[0].Id, null, false),
        _ when resolved.Scope.UserId is null && resolved.Scope.DirectionId is null => new AssortmentScope(null, null, null, false),
        _ => new AssortmentScope(null, null, null, false, resolved.Regions.Select(r => r.Id).ToList()),
    };

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

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>Артикул карточки категории: факт месяца, ТТ с товаром, прошлый месяц, метка ТОП и главное фото (160 px, webp; нет — null).</summary>
public sealed record CategorySalesSku(long ProductId, string Name, string? Code, decimal Kg, decimal Revenue, int Akb, decimal PrevMonthKg, bool IsTop, byte[]? Photo);

/// <summary>Строка разбивки карточки: регион (у РМ и всей страны) или ТП (у СВ): факт, план категории и выполнение, сумма, АКБ категории.</summary>
public sealed record CategorySalesRow(string Name, decimal Kg, decimal? PlanKg, decimal? Execution, decimal Revenue, int Akb);

/// <summary>
/// Карточка одной категории в охвате получателя — данные страницы «Продажи → Ассортимент → категория» и плана категории: факт и сумма
/// месяца, доля категории в весе охвата, план (PlanShared — план общий на несколько категорий, «Помадка + Шоколад»: выполнения отдельно
/// нет), выполнение, остаток, прогноз, АКБ и дистрибуция, SKU «продаётся из ассортимента», молчат и пропали, прогноз к прошлому месяцу,
/// итог дня, килограммы по дням, разбивка (BreakdownKind: regions — регионы, agents — ТП, null — нет), ТОП артикулов с фото и пропавшие.
/// </summary>
public sealed record CategorySalesCard(
    string Name,
    string? Alias,
    decimal FactKg,
    decimal Revenue,
    decimal? WeightShare,
    decimal? PlanKg,
    string? PlanShared,
    decimal? Execution,
    decimal? RemainingKg,
    decimal? ForecastKg,
    decimal? ForecastExecution,
    int Akb,
    decimal? Distribution,
    int SkuSold,
    int SkuTotal,
    int Silent,
    int Lost,
    decimal PrevMonthKg,
    decimal? VsPrevMonth,
    decimal DayKg,
    decimal DayRevenue,
    int DayAkb,
    IReadOnlyList<CategoryDayPoint> Days,
    string? BreakdownKind,
    IReadOnlyList<CategorySalesRow> Breakdown,
    IReadOnlyList<CategorySalesSku> Top,
    IReadOnlyList<string> LostSkus,
    int LostMore);

/// <summary>
/// Карточки категорий одного отчёта: момент («morning» — итог прошлого рабочего дня, «evening» — итог дня), день итога, месяц и отчётный
/// день данных «Продаж» (DataThrough), закрыт ли месяц, карточки в порядке выбора. Одна категория — одна картинка, все — одним альбомом.
/// </summary>
public sealed record CategorySalesCards(string Slot, DateOnly Day, int Year, int Month, DateOnly DataThrough, bool MonthClosed, IReadOnlyList<CategorySalesCard> Cards)
{
    public const string Regions = "regions";
    public const string Agents = "agents";
}

/// <summary>Выбор категорий карточек: названия из «Настроек продаж» через запятую; второе название категории в планах и у людей.</summary>
public static class CategoryCardChoice
{
    /// <summary>Сколько категорий в одной отправке: альбом Telegram — до 10 картинок.</summary>
    public const int Max = 10;

    /// <summary>Как категорию называют в планах Linko и в разговоре: «Помадка» — «Могуль», «Придзел» — Twizos.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Помадка"] = "Могуль",
        ["Придзел"] = "Twizos",
    };

    public static string? AliasOf(string name) => Aliases.GetValueOrDefault(name);

    /// <summary>«Придзел, Помадка» → [«Придзел», «Помадка»]: без пустых и повторов (регистр не важен), в заданном порядке.</summary>
    public static IReadOnlyList<string> Parse(string? text) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Список для хранения: пусто — null («как по умолчанию»).</summary>
    public static string? Format(IEnumerable<string>? names) =>
        names?.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList() is { Count: > 0 } list ? string.Join(",", list) : null;

    /// <summary>Утренний итог — за прошлый рабочий день (пн–сб): в понедельник — за субботу.</summary>
    public static DateOnly PreviousWorkDay(DateOnly date)
    {
        var day = date.AddDays(-1);
        return day.DayOfWeek == DayOfWeek.Sunday ? day.AddDays(-1) : day;
    }
}

/// <summary>Документ отчёта «10. Карточки категорий» из готовых карточек: подпись, краткая сводка, замечания о данных. Без базы — проверяется тестами.</summary>
public static class CategorySalesReport
{
    private const int CaptionLimit = 1000;

    public static ReportDocument Document(ReportJobInfo info, ResolvedScope scope, DateOnly date, CategorySalesCards cards, DateTimeOffset now, DateTimeOffset? dataAsOf,
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

        if (cards.Cards.Count == 0)
        {
            const string skip = "Условие отчёта: ни одной из выбранных категорий нет в «Настройках продаж» — выберите категории в «Карточках категорий».";
            return new ReportDocument(info.Code, info.Report, scope.Scope.Role, title, scope.Title, scope.Manager, date, period, now, dataAsOf, language, [], [], [], notes,
                head, "", incomplete, Applies: false, SkipReason: skip) { CategorySales = cards };
        }

        var lines = cards.Cards.Select(c => c.Execution is { } e
                ? F("{0}: {1} кг за месяц — {2} плана; за день {3} кг", c.Name, ReportFormat.Kg(c.FactKg), ReportFormat.Percent(e), ReportFormat.Kg(c.DayKg))
                : F("{0}: {1} кг за месяц; за день {2} кг", c.Name, ReportFormat.Kg(c.FactKg), ReportFormat.Kg(c.DayKg)))
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

        var kpis = cards.Cards.Select(c => new ReportKpi(c.Name, $"{ReportFormat.Kg(c.FactKg)} {T("кг")}",
            c.Execution is { } x ? F("{0} плана", ReportFormat.Percent(x)) : T("плана нет"),
            c.Execution is { } y ? y >= 1 ? ReportTone.Good : y >= ReportFormat.RedBelow ? ReportTone.Warn : ReportTone.Bad : ReportTone.Normal)).ToList();
        var summary = string.Join("; ", cards.Cards.Select(c => $"{c.Name} {ReportFormat.Kg(c.FactKg)} {T("кг")}" +
                                                                 (c.Execution is { } e ? $" ({ReportFormat.Percent(e)})" : "")));
        return new ReportDocument(info.Code, info.Report, scope.Scope.Role, title, scope.Title, scope.Manager, date, period, now, dataAsOf, language, kpis, [], [], notes,
            caption, summary, incomplete) { CategorySales = cards };
    }
}

/// <summary>
/// Данные карточек категорий для области получателя — из «Продаж» месяца дня итога (те же строки и правила, что у страницы категории):
/// РМ — его регионы (вся страна — республика) с разбивкой по регионам; СВ — регион с разбивкой по ТП; ТП — только он. Фото артикулов — из
/// хранилища: недоступно — карточка без фото, а не ошибка.
/// </summary>
public sealed class CategoryCardsBuilder(IAppDbContext db, SalesDataLoader loader, IFileStorage storage, ILogger<CategoryCardsBuilder> logger)
{
    private const int TopSkus = 8;
    private const int LostShown = 6;
    private const int PhotoSize = 160;
    private static readonly TimeSpan PhotoBudget = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Отчёт запуска: chosen — категории получателя (null — выбор по умолчанию из настроек, а без него — все категории отчёта). Утренний
    /// запуск — итог прошлого рабочего дня (в понедельник — субботы), вечерний — итог дня запуска.
    /// </summary>
    public async Task<ReportDocument> BuildAsync(ReportJobInfo info, ResolvedScope resolved, DateOnly date, DateTimeOffset now, string language,
        IReadOnlyList<string>? chosen, TimeOnly jobTime, CancellationToken ct)
    {
        var day = info.IsMorning ? CategoryCardChoice.PreviousWorkDay(date) : date;
        var analytics = await loader.LoadAsync(day.Year, day.Month, ct);
        var names = chosen is { Count: > 0 }
            ? chosen
            : CategoryCardChoice.Parse(await db.TelegramReportSettings.AsNoTracking().Select(s => s.CategoryCards).FirstOrDefaultAsync(ct));
        var all = analytics.ReportCategoryGroups();
        // Названия — в группы «Продаж» в порядке выбора; категории, которой в настройке больше нет (или у неё нет типов Linko), нет и карточки.
        var picked = names.Count == 0
            ? all
            : names.Select(analytics.ReportCategoryGroup).OfType<long>().Distinct().Select(g => all.FirstOrDefault(x => x.Group == g)).Where(x => x.Name is not null);
        var groups = picked.Take(CategoryCardChoice.Max).ToList();

        var scope = ScopeOf(resolved);
        var byRegions = resolved.Scope.Role == TelegramReportRoles.Rm && (resolved.Regions.Count > 1 || scope.Regions is null);
        var cards = new List<CategorySalesCard>();
        foreach (var (group, name) in groups)
        {
            var id = group.ToString(CultureInfo.InvariantCulture);
            var view = analytics.CachedCategory(id, scope);
            var card = view?.Card;
            var plan = analytics.CategoryPlanOf(group, scope);
            var dayTotals = analytics.CategoryDayOf(group, scope, day) ?? new CategoryDayTotals(0, 0, 0);
            var shared = plan is { CategoryId: null } ? plan.Name : null;
            var exact = shared is null ? plan : null;
            IReadOnlyList<CategorySalesRow> breakdown = resolved.Scope.Role == TelegramReportRoles.Tp ? []
                : byRegions ? RegionRows(analytics, group, id, view)
                : AgentRows(analytics, group, id, resolved.Regions[0].Agents);
            var skus = card?.Skus ?? [];
            var lost = skus.Where(s => s.Status == SkuStatuses.Lost).OrderByDescending(s => s.PrevMonthKg).ThenBy(s => s.Name).ToList();
            cards.Add(new CategorySalesCard(
                name,
                CategoryCardChoice.AliasOf(name),
                card?.FactKg ?? 0,
                card?.Revenue ?? 0,
                card?.WeightShare,
                plan?.PlanKg,
                shared,
                exact?.Execution,
                exact?.RemainingKg,
                card?.ForecastKg ?? exact?.ForecastKg,
                exact?.ForecastExecution,
                card?.Akb ?? 0,
                card?.Distribution,
                card?.SkuSold ?? 0,
                card?.SkuTotal ?? 0,
                card?.Silent ?? 0,
                card?.Lost ?? 0,
                card?.PrevMonthKg ?? 0,
                card?.VsPrevMonth,
                dayTotals.Kg,
                dayTotals.Revenue,
                dayTotals.Akb,
                analytics.CategoryDailyKg(group, scope),
                breakdown.Count == 0 ? null : byRegions ? CategorySalesCards.Regions : CategorySalesCards.Agents,
                breakdown,
                skus.Where(s => s.Status == SkuStatuses.Selling && s.FactKg > 0).OrderByDescending(s => s.FactKg).Take(TopSkus)
                    .Select(s => new CategorySalesSku(s.ProductId, s.Name, s.Code, s.FactKg, s.Revenue, s.Akb, s.PrevMonthKg, s.IsTop, null)).ToList(),
                lost.Take(LostShown).Select(s => s.Name).ToList(),
                Math.Max(0, lost.Count - LostShown)));
        }

        cards = await WithPhotosAsync(cards, ct);
        var period = analytics.Period;
        var payload = new CategorySalesCards(info.Slot, day, period.Year, period.Month, period.DataThrough, period.Closed, cards);
        var asOf = await db.LinkoSyncStates.AsNoTracking().Where(s => s.Entity == "orders").Select(s => s.LastSuccessAt).FirstOrDefaultAsync(ct);
        return CategorySalesReport.Document(info, resolved, date, payload, now, asOf?.ToOffset(now.Offset), language, jobTime);
    }

    /// <summary>Охват страницы категории для области: ТП — агент, СВ — регион, РМ — его регионы, вся страна — республика.</summary>
    public static AssortmentScope ScopeOf(ResolvedScope resolved) => resolved.Scope.Role switch
    {
        TelegramReportRoles.Tp => new AssortmentScope(null, null, resolved.Agent!.Id, false),
        TelegramReportRoles.Sv => new AssortmentScope(null, resolved.Regions[0].Id, null, false),
        _ when resolved.Scope.UserId is null && resolved.Scope.DirectionId is null => new AssortmentScope(null, null, null, false),
        _ => new AssortmentScope(null, null, null, false, resolved.Regions.Select(r => r.Id).ToList()),
    };

    /// <summary>Регионы категории (страница категории республики и групп регионов) с планом категории региона; без продаж и плана — не показываются.</summary>
    private static List<CategorySalesRow> RegionRows(SalesAnalytics analytics, long group, string id, CategoryView? view) =>
        (view?.Regions ?? [])
        .Select(r =>
        {
            var plan = Guid.TryParse(r.Id, out var region) ? analytics.CategoryPlanOf(group, new AssortmentScope(null, region, null, false)) : null;
            var exact = plan is { CategoryId: not null } ? plan : null;
            return new CategorySalesRow(r.Name, r.Kg, exact?.PlanKg, exact?.Execution, r.Revenue, r.Akb);
        })
        .Where(r => r.Kg != 0 || r.PlanKg is not null)
        .OrderByDescending(r => r.Kg)
        .ToList();

    /// <summary>ТП региона: факт категории, его план категории (Linko) и выполнение, сумма и АКБ категории; без продаж и плана — не показываются.</summary>
    private static List<CategorySalesRow> AgentRows(SalesAnalytics analytics, long group, string id, IReadOnlyList<ReportPerson> agents) =>
        agents
        .Select(a =>
        {
            var scope = new AssortmentScope(null, null, a.Id, false);
            var card = analytics.CachedCategory(id, scope)?.Card;
            var plan = analytics.CategoryPlanOf(group, scope);
            var exact = plan is { CategoryId: not null } ? plan : null;
            return new CategorySalesRow(a.Name, card?.FactKg ?? 0, exact?.PlanKg, exact?.Execution, card?.Revenue ?? 0, card?.Akb ?? 0);
        })
        .Where(r => r.Kg != 0 || r.PlanKg is not null)
        .OrderByDescending(r => r.Kg)
        .ToList();

    /// <summary>Главные фото артикулов ТОПа (160 px): хранилище не отвечает — дальше без фото (на всё не больше 20 секунд).</summary>
    private async Task<List<CategorySalesCard>> WithPhotosAsync(List<CategorySalesCard> cards, CancellationToken ct)
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
                logger.LogWarning(ex, "Карточки категорий: фото товара {Product} не загружено — дальше без фото", product);
                break;
            }
        }

        return cards.Select(c => c with { Top = c.Top.Select(s => s with { Photo = photos.GetValueOrDefault(s.ProductId) }).ToList() }).ToList();
    }
}

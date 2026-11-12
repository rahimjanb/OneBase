using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Audit;
using OneBase.Domain.Incentive;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>Акция для вкладки «Акции»: настройки, состояние (выключена, скоро, идёт, закончилась), рабочие дни до конца; акции ноября (нет — null).</summary>
public sealed record IncentiveProgramView(
    string Code,
    string Name,
    string Product,
    IReadOnlyList<long> ProductTypeIds,
    DateOnly Start,
    DateOnly End,
    IReadOnlyList<IncentiveTier> Tiers,
    IReadOnlyList<IncentiveMonthShare> Months,
    IReadOnlyList<IncentiveStoreSet> StoreSets,
    IReadOnlyList<long> WholesaleAgentIds,
    bool SeparateWholesale,
    bool SvCopies,
    bool SvSummary,
    string RmMode,
    string StaleData,
    bool Enabled,
    string State,
    int WorkDaysLeft,
    DateTimeOffset? UpdatedAt,
    int MaxStores,
    IncentivePromos? Promos = null,
    bool OnePhoto = false);

public sealed record IncentiveTypeOption(long Id, string Name, long? ParentId);

public sealed record IncentivePersonView(long Id, string Name);

/// <summary>Акции, типы товаров Linko (выбор товаров акции), имена оптовых ТП из Linko, картинки призов.</summary>
public sealed record IncentiveOverview(IReadOnlyList<IncentiveProgramView> Programs, IReadOnlyList<IncentiveTypeOption> ProductTypes,
    IReadOnlyList<IncentivePersonView> Agents, IReadOnlyList<string> Images);

/// <summary>Настройки акции; Code — только при создании («по образцу»).</summary>
public sealed record IncentiveProgramInput(
    string? Code,
    string? Name,
    string? Product,
    IReadOnlyList<long>? ProductTypeIds,
    DateOnly? Start,
    DateOnly? End,
    IReadOnlyList<IncentiveTier>? Tiers,
    IReadOnlyList<IncentiveMonthShare>? Months,
    IReadOnlyList<IncentiveStoreSet>? StoreSets,
    IReadOnlyList<long>? WholesaleAgentIds,
    bool SeparateWholesale,
    bool SvCopies,
    bool SvSummary,
    string? RmMode,
    string? StaleData,
    bool Enabled,
    int MaxStores = IncentiveRules.MaxStores,
    bool? OnePhoto = null);

/// <summary>Строка «Накоплено сейчас»: место в своей группе, ТП, регион, опт, накоплено (доставлено, возвраты, заказы сегодня), приз и до следующего.</summary>
public sealed record IncentiveStandingRow(int? Place, long AgentId, string Name, string? Region, bool Wholesale, decimal Total, decimal Delivered, decimal Returned,
    decimal Today, string? Reached, string? Next, decimal? ToNext);

public sealed record IncentiveStandingsView(string Code, DateOnly Date, DateTimeOffset? DataAsOf, int WorkDaysLeft, IReadOnlyList<IncentiveStandingRow> Rows);

/// <summary>
/// «Настройки → Telegram → Автоматические отчёты → Акции»: накопительные акции (Twizos и будущие) — период, товары, ступени и призы, ориентиры
/// месяцев, цели магазинов, оптовые ТП, что получают СВ и РМ, поведение при несвежих данных, включение; новая акция — по образцу; «накоплено
/// сейчас» по оргструктуре «Продаж» (с правом sales.read). Рассылка — запуски «9. Накопительная акция» в расписании автоотчётов.
/// </summary>
public sealed class IncentiveSettingsService(IAppDbContext db, IncentiveService incentive, ReportEngine engine, IAuditLogger audit, TimeProvider clock)
{
    public static readonly IReadOnlyList<string> Images = ["tablet", "tv", "phone", "gift"];

    /// <summary>Картинки подарков клиенту TW-02 (Assets/Gift_*.png).</summary>
    public static readonly IReadOnlyList<string> GiftImages = ["mug", "thermos", "kettle"];
    private static readonly Regex CodePattern = new("^[a-z0-9][a-z0-9-]{2,63}$", RegexOptions.Compiled);

    private DateOnly Today() => DateOnly.FromDateTime(ReportSchedules.Local(clock.GetUtcNow(), ReportSchedules.DefaultTimeZone).DateTime);

    public async Task<IncentiveOverview> OverviewAsync(CancellationToken ct)
    {
        var programs = await db.IncentivePrograms.AsNoTracking().OrderByDescending(p => p.StartDate).ThenBy(p => p.Code).ToListAsync(ct);
        var holidays = await HolidaysAsync(ct);
        var types = await db.LinkoProductTypes.AsNoTracking().OrderBy(t => t.Name).Select(t => new IncentiveTypeOption(t.Id, t.Name, t.ParentId)).ToListAsync(ct);
        var ids = programs.SelectMany(p => p.WholesaleAgentIds).Distinct().ToArray();
        var agents = (await db.LinkoUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.FirstName, u.SecondName }).ToListAsync(ct))
            .Select(u => new IncentivePersonView(u.Id, $"{u.FirstName} {u.SecondName}".Trim())).OrderBy(u => u.Name).ToList();
        return new IncentiveOverview(programs.Select(p => View(p, holidays)).ToList(), types, agents, Images);
    }

    public async Task<IncentiveProgramView> SaveAsync(string code, IncentiveProgramInput input, Guid userId, CancellationToken ct)
    {
        var program = await db.IncentivePrograms.FirstOrDefaultAsync(p => p.Code == code, ct) ?? throw new KeyNotFoundException("Акция не найдена — обновите страницу.");
        await ApplyAsync(program, input, ct);
        program.UpdatedAt = clock.GetUtcNow();
        program.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.incentive", nameof(IncentiveProgram), program.Code,
            new { program.Enabled, program.StartDate, program.EndDate, program.ProductTypeIds, program.TiersJson, program.WholesaleAgentIds, program.RmMode, program.StaleData, program.OnePhoto }, ct);
        return View(program, await HolidaysAsync(ct));
    }

    /// <summary>Новая акция (по образцу текущей): свой код, создаётся выключенной — включает администратор после проверки.</summary>
    public async Task<IncentiveProgramView> CreateAsync(IncentiveProgramInput input, Guid userId, CancellationToken ct)
    {
        var code = input.Code?.Trim().ToLowerInvariant() ?? "";
        if (!CodePattern.IsMatch(code))
        {
            throw new ReportValidationException("Код акции — латинские буквы, цифры и дефис, от 3 до 64 знаков: «twizos-2027q1».");
        }

        if (await db.IncentivePrograms.AnyAsync(p => p.Code == code, ct))
        {
            throw new ReportValidationException("Акция с таким кодом уже есть.");
        }

        var program = new IncentiveProgram { Code = code, Name = "", Product = "" };
        await ApplyAsync(program, input with { Enabled = false }, ct);
        program.UpdatedAt = clock.GetUtcNow();
        program.UpdatedById = userId;
        db.IncentivePrograms.Add(program);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.incentive.create", nameof(IncentiveProgram), program.Code,
            new { program.Name, program.StartDate, program.EndDate }, ct);
        return View(program, await HolidaysAsync(ct));
    }

    /// <summary>
    /// Акции ноября акции (YM-01…03, TW-01…03, бюджеты): проверка и сохранение в PromosJson; раздел null — акции нет. Наборы и ступени — в основной
    /// форме акции.
    /// </summary>
    public async Task<IncentiveProgramView> SavePromosAsync(string code, IncentivePromos input, Guid userId, CancellationToken ct)
    {
        var program = await db.IncentivePrograms.FirstOrDefaultAsync(p => p.Code == code, ct) ?? throw new KeyNotFoundException("Акция не найдена — обновите страницу.");
        var promos = Validate(input, IncentiveSettings.From(program));
        var types = new[] { promos.Yamelly?.ProductTypeIds, promos.Territory?.ProductTypeIds }.SelectMany(x => x ?? []).Distinct().ToArray();
        if (types.Length > 0 && await db.LinkoProductTypes.CountAsync(t => types.Contains(t.Id), ct) != types.Length)
        {
            throw new ReportValidationException("Типы товаров Yamelly и YM-03 — из справочника Linko.");
        }

        var gifts = (promos.Twizos?.Gifts ?? []).SelectMany(g => g.ProductIds ?? []).Distinct().ToArray();
        if (gifts.Length > 0 && await db.LinkoProducts.CountAsync(x => gifts.Contains(x.Id), ct) != gifts.Length)
        {
            throw new ReportValidationException("Товары подарков — ID товаров Linko («Бокал» 297, «Термос» 136, «Электрочайник» 333).");
        }

        program.PromosJson = promos.ToJson();
        program.UpdatedAt = clock.GetUtcNow();
        program.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.incentive.promos", nameof(IncentiveProgram), program.Code, new { program.PromosJson }, ct);
        return View(program, await HolidaysAsync(ct));
    }

    /// <summary>Проверка акций ноября: понятная причина (400) по каждому разделу, ничего не сохраняется. Текст — без лишних пробелов.</summary>
    internal static IncentivePromos Validate(IncentivePromos p, IncentiveSettings s)
    {
        var errors = new List<string>();
        bool Inside(DateOnly from, DateOnly to) => from <= to && from >= s.Start && to <= s.End;
        var codes = s.StoreSets.Select(x => x.Code).OfType<string>().ToHashSet();
        var tw = p.Twizos;
        if (tw is not null)
        {
            var gifts = (tw.Gifts ?? []).Select(g => g with { Name = g.Name?.Trim() ?? "", NameUz = Blank(g.NameUz) }).ToList();
            if (gifts.Count is < 1 or > 3 || gifts.Any(g => g.MinSum <= 0 || !GiftImages.Contains(g.Gift) || g.Name.Length == 0 || g.Stock < 0)
                || gifts.Select(g => g.Gift).Distinct().Count() != gifts.Count || gifts.Select(g => g.MinSum).Distinct().Count() != gifts.Count)
            {
                errors.Add("Подарки TW-02: от 1 до 3, у каждого своя картинка (кружка, термос, чайник) и своя сумма «от», название обязательно, выделено — не меньше нуля.");
            }

            bool BadRules(IReadOnlyList<IncentivePairRule>? rules) => rules is not null
                && (rules.Count > 5 || rules.Any(r => r.Sets.Count is < 1 or > 2 || r.Sets.Any(c => !codes.Contains(c)) || r.MaxCheck is <= 0)
                    || rules.Count(r => r.MaxCheck is null) > 1);
            if (BadRules(tw.Pairs) || BadRules(tw.ExtraPairs) || !codes.Contains(tw.StartSet) || !codes.Contains(tw.SmallStartSet))
            {
                errors.Add($"Пары и старт — наборы акции ({string.Join(", ", codes.Order())}); в паре 1–2 набора, порог чека больше нуля, «без верхней границы» — одна.");
            }

            if (tw.MinCheck < 0 || tw.SmallCheck < tw.MinCheck || tw.PairNewMinCheck < 0)
            {
                errors.Add("Пороги чека: «без цели до» не больше «старт 35 г до», все — не меньше нуля.");
            }

            if (tw.Reserve is < 1 or > 3 || tw.Conversion is <= 0 or > 1 || tw.ExtraMax is < 0 or > 20 || tw.CheckDays is < 7 or > 365 || tw.TopDays is < 7 or > 365)
            {
                errors.Add("Запас на отказы — от 1 до 3 (1,3 = +30%), конверсия — до 100%, добор вне маршрута — до 20 точек, окна чека и ТОП SKU — 7–365 дней.");
            }

            if (tw.TargetPrize is { } target && s.Tiers.All(t => t.Image != target))
            {
                errors.Add("Приз для расчёта нормы — одна из ступеней акции.");
            }

            tw = tw with { Gifts = gifts, TargetPrize = Blank(tw.TargetPrize) };
        }

        var ym = p.Yamelly;
        if (ym is not null)
        {
            if (!Inside(ym.Start, ym.End))
            {
                errors.Add("Yamelly: период внутри акции, начало не позже конца.");
            }

            if (ym.ProductTypeIds is not { Count: > 0 } || ym.Packings is not { Count: > 0 } packings
                || packings.Any(x => x.UnitKg <= 0 || x.PerBox is < 1 or > 100 || string.IsNullOrWhiteSpace(x.Name)))
            {
                errors.Add("Yamelly: типы товаров и фасовки (вес пачки больше нуля, пачек в коробке 1–100, название) обязательны.");
            }

            if (ym.DiscountPercent is < 0 or > 100 || ym.CashbackPercent is < 0 or > 100 || ym.MinBoxes is < 1 or > 50 || ym.WakeShare is < 0 or > 1 || ym.MinCashback < 0)
            {
                errors.Add("Yamelly: скидка и кэшбек — 0–100%, коробок — 1–50, цель «разбудить» — 0–100%, минимальный кэшбек — не меньше нуля.");
            }

            if (ym.ListFrom >= ym.ListSince || ym.ListSince > ym.Start)
            {
                errors.Add("Список спящих: «брали Yamelly с» раньше «не брали с», а «не брали с» — не позже начала Yamelly.");
            }

            ym = ym with { Product = string.IsNullOrWhiteSpace(ym.Product) ? "Yamelly" : ym.Product.Trim(), Packings = ym.Packings?.Select(x => x with { Name = x.Name.Trim() }).ToList() };
        }

        if (p.Best is { } b && (!Inside(b.Start, b.End) || b.RetailMinKg <= 0 || b.WholesaleMinKg <= 0 || b.Prize < 0))
        {
            errors.Add("TW-01: период внутри акции, пороги розницы и опта больше нуля, приз — не меньше нуля.");
        }

        var territory = p.Territory;
        if (territory is not null)
        {
            var list = (territory.Territories ?? []).Select(x => x with
            {
                Name = x.Name?.Trim() ?? "", Branches = (x.Branches ?? []).Select(br => br.Trim()).Where(br => br.Length > 0).Distinct().ToList(),
            }).ToList();
            if (!Inside(territory.Start, territory.End) || list.Count is < 1 or > 20 || list.Any(x => x.Name.Length == 0 || x.Branches.Count == 0 || x.PlanKg <= 0)
                || list.Select(x => x.Name).Distinct(StringComparer.CurrentCultureIgnoreCase).Count() != list.Count || territory.ProductTypeIds is not { Count: > 0 }
                || territory.Threshold is <= 0 or > 1 || territory.AgentMinKg < 0 || territory.AgentPrize < 0 || territory.SupervisorPrize < 0)
            {
                errors.Add("YM-03: период внутри акции, 1–20 территорий со своим названием, филиалами Linko и планом больше нуля, типы товаров, порог — до 100%, премии — не меньше нуля.");
            }

            territory = territory with { Territories = list };
        }

        if (p.Budget is { } budget && (budget.Cashback < 0 || budget.Best < 0 || budget.Territory < 0))
        {
            errors.Add("Бюджеты — не меньше нуля (0 — не задан).");
        }

        if (errors.Count > 0)
        {
            throw new ReportValidationException(string.Join(" ", errors));
        }

        return p with { Twizos = tw, Yamelly = ym, Territory = territory };
    }

    /// <summary>Накоплено сейчас (как в карточке 18:00): все ТП оргструктуры месяца, розница и опт — своими местами.</summary>
    public async Task<IncentiveStandingsView> StandingsAsync(string code, DateOnly? date, bool canUseSales, CancellationToken ct)
    {
        if (!canUseSales)
        {
            throw new ReportForbiddenException("Нет права на данные продаж (sales.read) — накопленное не показывается.");
        }

        var program = await incentive.ProgramAsync(code, ct) ?? throw new KeyNotFoundException("Акция не найдена — обновите страницу.");
        var day = date ?? Today();
        var (teams, _) = await engine.StructureAsync(day, ct);
        var agents = teams.SelectMany(t => t.Agents.Select(a => new IncentiveAgent(a.Id, a.Name, t.Name))).DistinctBy(a => a.Id).ToList();
        var standings = await incentive.StandingsAsync(program, agents, IncentiveMoment.Day(day), ct);
        var holidays = await incentive.HolidaysAsync(program, ct);
        IncentiveStandingRow Row(IncentiveStanding s, int place)
        {
            var tiers = program.TiersFor(s.Wholesale);
            var next = IncentiveRules.Next(tiers, s.Total);
            return new IncentiveStandingRow(s.Total > 0 ? place : null, s.Agent.Id, s.Agent.Name, s.Agent.Region, s.Wholesale, s.Total, s.Delivered, s.Returned, s.Today,
                IncentiveRules.Reached(tiers, s.Total)?.Prize, next?.Prize, next is null ? null : next.Kg - s.Total);
        }

        var rows = standings.Rating(false).Select((s, i) => Row(s, i + 1)).Concat(standings.Rating(true).Select((s, i) => Row(s, i + 1))).ToList();
        return new IncentiveStandingsView(program.Code, day, standings.OrdersAsOf, IncentiveRules.Progress(program, 0, day.AddDays(1), holidays).WorkDaysLeft, rows);
    }

    private async Task<IReadOnlyDictionary<DateOnly, string>> HolidaysAsync(CancellationToken ct) =>
        (await db.SalesHolidays.AsNoTracking().Select(h => new { h.Date, h.Name }).ToListAsync(ct)).GroupBy(h => h.Date).ToDictionary(g => g.Key, g => g.First().Name);

    private IncentiveProgramView View(IncentiveProgram p, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        var s = IncentiveSettings.From(p);
        var today = Today();
        var state = !p.Enabled ? "off" : today < p.StartDate ? "upcoming" : today > p.EndDate ? "finished" : "running";
        var days = today > p.EndDate ? 0 : IncentiveRules.WorkDays(today < p.StartDate ? p.StartDate : today, p.EndDate, holidays);
        var promos = s.Offers is { Twizos: null, Yamelly: null, Best: null, Territory: null, Budget: null } ? null : s.Offers;
        return new IncentiveProgramView(p.Code, p.Name, p.Product, p.ProductTypeIds, p.StartDate, p.EndDate, s.Tiers, s.Months,
            s.StoreSets.OrderBy(x => x.MinAvg).ToList(), p.WholesaleAgentIds, p.SeparateWholesale, p.SvCopies, p.SvSummary, p.RmMode,
            p.StaleData, p.Enabled, state, days, p.UpdatedAt, p.MaxStores, promos, p.OnePhoto);
    }

    /// <summary>Проверка и перенос настроек. Ошибка — понятная причина (400), ничего не сохраняется.</summary>
    private async Task ApplyAsync(IncentiveProgram p, IncentiveProgramInput input, CancellationToken ct)
    {
        var name = input.Name?.Trim() ?? "";
        var product = input.Product?.Trim() ?? "";
        if (name.Length is < 1 or > 200 || product.Length is < 1 or > 100)
        {
            throw new ReportValidationException("Название акции (до 200 знаков) и товар на карточках (до 100) обязательны.");
        }

        if (input.Start is not { } start || input.End is not { } end || end < start || end.DayNumber - start.DayNumber > 366)
        {
            throw new ReportValidationException("Период акции: начало не позже конца, не дольше года.");
        }

        var types = (input.ProductTypeIds ?? []).Distinct().ToArray();
        if (types.Length == 0 || await db.LinkoProductTypes.CountAsync(t => types.Contains(t.Id), ct) != types.Length)
        {
            throw new ReportValidationException("Выберите типы товаров Linko, которые копятся в акции (подтипы входят сами).");
        }

        // Порог опта (TW-03) — у ступени с той же картинкой, если форма его не прислала.
        var savedTiers = IncentiveSettings.From(p).Tiers;
        var tiers = (input.Tiers ?? []).Select(t => t with
        {
            Prize = t.Prize?.Trim() ?? "", Short = t.Short?.Trim() ?? "", Of = Blank(t.Of), PrizeUz = Blank(t.PrizeUz), ShortUz = Blank(t.ShortUz),
            WholesaleKg = t.WholesaleKg ?? savedTiers.FirstOrDefault(x => x.Image == t.Image)?.WholesaleKg,
        }).OrderBy(t => t.Kg).ToList();
        if (tiers.Any(t => t.WholesaleKg is <= 0))
        {
            throw new ReportValidationException("Порог опта у ступени — больше нуля (или пусто — как у розницы).");
        }
        if (tiers.Count is < 1 or > 5 || tiers.Any(t => t.Kg <= 0 || t.Prize.Length == 0 || t.Short.Length == 0 || !Images.Contains(t.Image))
            || tiers.Select(t => t.Kg).Distinct().Count() != tiers.Count)
        {
            throw new ReportValidationException("Ступени: от 1 до 5, порог в кг больше нуля и у каждой свой, приз и короткое название обязательны.");
        }

        var months = (input.Months ?? []).OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();
        if (months.Any(m => m.Share is < 0 or > 1 || m.Month is < 1 or > 12 || new DateOnly(m.Year, m.Month, 1) > end || new DateOnly(m.Year, m.Month, 1).AddMonths(1) <= start)
            || months.Select(m => (m.Year, m.Month)).Distinct().Count() != months.Count || months.Sum(m => m.Share) > 1.0001m)
        {
            throw new ReportValidationException("Ориентиры месяцев: месяцы периода акции, доли от 0 до 100%, вместе не больше 100%.");
        }

        var sets = (input.StoreSets ?? []).Select(x => x with
        {
            Contents = x.Contents?.Trim() ?? "", Prize = Blank(x.Prize), Level = Blank(x.Level), ContentsUz = Blank(x.ContentsUz), PrizeUz = Blank(x.PrizeUz),
            Code = Blank(x.Code), Gift = Blank(x.Gift),
        }).OrderByDescending(x => x.MinAvg).ToList();
        // Наборы акций ноября (с кодами K35, K140, T, C) выбирают правила чека, а не «закуп от»: форма без кодов их не затирает.
        var saved = IncentiveSettings.From(p).StoreSets;
        if (saved.Any(x => x.Code is not null) && sets.All(x => x.Code is null))
        {
            sets = saved.ToList();
        }

        if (sets.Any(x => x.Code is not null))
        {
            if (sets.Count is < 1 or > 8 || sets.Any(x => x.Code is null || x.Sum <= 0 || x.Kg <= 0 || x.Kg > 100 || x.Contents.Length == 0
                    || (x.Gift is not null && !GiftImages.Contains(x.Gift)))
                || sets.Select(x => x.Code).Distinct().Count() != sets.Count)
            {
                throw new ReportValidationException("Наборы: от 1 до 8, у каждого свой код, сумма и вес больше нуля (вес до 100 кг), состав обязателен, подарок — кружка, термос или чайник.");
            }
        }
        else if (sets.Count is < 1 or > 8 || sets.Any(x => x.MinAvg < 0 || x.Kg <= 0 || x.Kg > 100 || x.Contents.Length == 0)
            || sets.Select(x => x.MinAvg).Distinct().Count() != sets.Count || sets.All(x => x.MinAvg > 0))
        {
            throw new ReportValidationException("Цели магазинов: от 1 до 8 наборов, свой «закуп от» у каждого, один — от 0 сум; вес набора от 0 до 100 кг, состав обязателен.");
        }

        if (input.MaxStores is < 1 or > IncentiveRules.MaxStoresLimit)
        {
            throw new ReportValidationException($"Магазинов с целью на маршруте — от 1 до {IncentiveRules.MaxStoresLimit}.");
        }

        var rm = input.RmMode is IncentiveRmMode.Cards ? IncentiveRmMode.Cards : IncentiveRmMode.Summary;
        var stale = input.StaleData is IncentiveStaleData.Skip ? IncentiveStaleData.Skip : IncentiveStaleData.Stamp;
        if (input.Enabled)
        {
            var overlap = await db.IncentivePrograms.AsNoTracking()
                .Where(o => o.Enabled && o.Id != p.Id && o.StartDate <= end && o.EndDate >= start).Select(o => o.Name).FirstOrDefaultAsync(ct);
            if (overlap is not null)
            {
                throw new ReportValidationException($"На эти даты уже включена акция «{overlap}»: в один день карточки строятся по одной акции.");
            }
        }

        p.Name = name;
        p.Product = product;
        p.ProductTypeIds = types;
        p.StartDate = start;
        p.EndDate = end;
        p.TiersJson = JsonSerializer.Serialize(tiers, IncentiveSettings.Json);
        p.MonthsJson = JsonSerializer.Serialize(months, IncentiveSettings.Json);
        p.StoreSetsJson = JsonSerializer.Serialize(sets, IncentiveSettings.Json);
        p.WholesaleAgentIds = (input.WholesaleAgentIds ?? []).Where(id => id > 0).Distinct().ToArray();
        p.SeparateWholesale = input.SeparateWholesale;
        p.SvCopies = input.SvCopies;
        p.SvSummary = input.SvSummary;
        p.RmMode = rm;
        p.StaleData = stale;
        p.MaxStores = input.MaxStores;
        // Старая форма без поля — настройка остаётся прежней.
        p.OnePhoto = input.OnePhoto ?? p.OnePhoto;
        p.Enabled = input.Enabled;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Audit;
using OneBase.Domain.Incentive;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>Акция для вкладки «Акции»: настройки, состояние (выключена, скоро, идёт, закончилась) и рабочие дни до конца.</summary>
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
    int MaxStores);

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
    int MaxStores = IncentiveRules.MaxStores);

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
            new { program.Enabled, program.StartDate, program.EndDate, program.ProductTypeIds, program.TiersJson, program.WholesaleAgentIds, program.RmMode, program.StaleData }, ct);
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
            var next = IncentiveRules.Next(program.Tiers, s.Total);
            return new IncentiveStandingRow(s.Total > 0 ? place : null, s.Agent.Id, s.Agent.Name, s.Agent.Region, s.Wholesale, s.Total, s.Delivered, s.Returned, s.Today,
                IncentiveRules.Reached(program.Tiers, s.Total)?.Prize, next?.Prize, next is null ? null : next.Kg - s.Total);
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
        return new IncentiveProgramView(p.Code, p.Name, p.Product, p.ProductTypeIds, p.StartDate, p.EndDate, s.Tiers, s.Months, s.StoreSets.OrderBy(x => x.MinAvg).ToList(),
            p.WholesaleAgentIds, p.SeparateWholesale, p.SvCopies, p.SvSummary, p.RmMode, p.StaleData, p.Enabled, state, days, p.UpdatedAt, p.MaxStores);
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

        var tiers = (input.Tiers ?? []).Select(t => t with
        {
            Prize = t.Prize?.Trim() ?? "", Short = t.Short?.Trim() ?? "", Of = Blank(t.Of), PrizeUz = Blank(t.PrizeUz), ShortUz = Blank(t.ShortUz),
        }).OrderBy(t => t.Kg).ToList();
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
        }).OrderByDescending(x => x.MinAvg).ToList();
        if (sets.Count is < 1 or > 8 || sets.Any(x => x.MinAvg < 0 || x.Kg <= 0 || x.Kg > 100 || x.Contents.Length == 0)
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
        p.Enabled = input.Enabled;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

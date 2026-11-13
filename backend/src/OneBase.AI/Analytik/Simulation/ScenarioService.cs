using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Forecasting;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Simulation;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Domain.Analytik;
using OneBase.Domain.Audit;

namespace OneBase.AI.Analytik.Simulation;

public sealed record ChoiceItem(string Id, string Name, string? Hint = null);

/// <summary>Цели точечных параметров снимка: регионы, товары, позиции сырья, поставщики позиций («позиция/поставщик»), подразделения.</summary>
public sealed record BaselineChoices(IReadOnlyList<ChoiceItem> Regions, IReadOnlyList<ChoiceItem> Products, IReadOnlyList<ChoiceItem> Items,
    IReadOnlyList<ChoiceItem> Suppliers, IReadOnlyList<ChoiceItem> Divisions);

/// <summary>Исходные показатели снимка для экрана.</summary>
public sealed record BaselineSummary(decimal? Revenue, decimal? Kg, decimal? DiscountShare, int Products, int ProductsWithRecipe, decimal? RecipeShare, int Regions,
    decimal? PlanKgMonth, string? PlanSource, DateOnly? SalesThrough, int Materials, int MaterialsPriced, int MaterialsLeadAssumed, decimal? CapacityKgDay,
    string? CapacitySource, decimal? ShiftsPerDay, decimal? DefectRate, decimal? Payments, decimal? CollectionRate, decimal? PaymentLagDays,
    decimal? ProcurementSpend, int? Employees, int Divisions);

public sealed record BaselineView(Guid Id, DateOnly From, DateOnly To, int Days, DateOnly AsOf, DateTimeOffset CreatedAt, string FormulaVersion,
    IReadOnlyList<string> Sections, string? SalesScope, IReadOnlyList<BaselineSource> Sources, IReadOnlyList<string> Notes, BaselineSummary Summary,
    BaselineChoices Choices);

public sealed record ScenarioHistoryEntry(int Revision, DateTimeOffset At, Guid BaselineId, DateOnly From, DateOnly To, DateOnly AsOf, string FormulaVersion,
    string? Headline, decimal? Revenue, decimal? RevenueScenario);

public sealed record ScenarioRow(Guid Id, string Name, string Kind, string Preset, string Status, string? Headline, DateTimeOffset? CalculatedAt,
    DateOnly BaseFrom, DateOnly BaseTo, DateOnly AsOf, int HorizonDays, bool Own, int Revision, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    decimal? RevenueDeltaPercent, int Critical, bool HasAi);

public sealed record ScenarioView(Guid Id, string Name, string Kind, string Preset, string Status, int HorizonDays, string? NewRegionName,
    IReadOnlyList<ScenarioParameter> Parameters, SimulationResult? Result, string? AiAnalysis, DateTimeOffset? AiAt, DateTimeOffset? CalculatedAt,
    string FormulaVersion, bool FormulaOutdated, int Revision, IReadOnlyList<ScenarioHistoryEntry> History, bool Own, Guid? CopiedFromId,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, BaselineView Baseline);

public sealed record ScenarioInput(string Name, string Kind, string? Preset, Guid BaselineId, int HorizonDays, IReadOnlyList<ScenarioParameter> Parameters,
    string? NewRegionName);

public sealed record RunInput(Guid BaselineId, string Kind, int HorizonDays, IReadOnlyList<ScenarioParameter> Parameters, string? NewRegionName);

public sealed record ComparisonColumn(Guid Id, string Name, string Preset, string Kind, int HorizonDays, DateOnly AsOf, bool Valid, string? Headline);

public sealed record ComparisonRow(string Code, string Group, string Title, string Unit, IReadOnlyList<decimal?> Values, IReadOnlyList<decimal?> Delta,
    IReadOnlyList<decimal?> DeltaPercent);

/// <summary>Сравнение сценариев: колонки, строки показателей (значение сценария, разница с первым — абсолютная и в %), заметки.</summary>
public sealed record ScenarioComparison(IReadOnlyList<ComparisonColumn> Columns, IReadOnlyList<ComparisonRow> Rows, IReadOnlyList<string> Notes);

/// <summary>Слишком много расчётов одновременно — повторить позже.</summary>
public sealed class SimulationBusyException(string message) : Exception(message);

/// <summary>
/// Сценарии What-If: черновой расчёт по снимку (для экрана — без сохранения), сохранение, открытие, копия, правка, архив, пересчёт на актуальных
/// данных (новый снимок; прежний — в истории), сравнение, набор «базовый / оптимистичный / пессимистичный». Права — на сервере: сценарий видит
/// автор; управляющий Аналитиком — чужие, только если его права покрывают разделы и область снимка. Изменять сценарий может только автор.
/// Бизнес-данные не меняются — пишутся только таблицы analytik.Scenarios и analytik.ScenarioBaselines. Каждое действие — в журнале аудита.
/// </summary>
public sealed class ScenarioService(
    IAnalytikDb adb,
    ScenarioBaselineBuilder builder,
    SalesAnalytik sales,
    HrService hr,
    AnalytikOptions options,
    IAuditLogger audit,
    IMemoryCache cache,
    TimeProvider clock)
{
    private static readonly SemaphoreSlim RunGate = new(8, 8);

    /// <summary>Сейчас в UTC (PostgreSQL хранит timestamptz только с нулевым смещением).</summary>
    private DateTimeOffset Now() => clock.GetUtcNow().ToUniversalTime();

    public const int MaxCompare = 5;

    /// <summary>Сколько полных недель продаж берётся для прогноза оптимистичного и пессимистичного сценариев.</summary>
    public const int WeeksOfHistory = 26;

    public static readonly string[] ComparedMetrics =
    [
        "sales.revenue", "sales.kg", "finance.cogs", "finance.payroll", "finance.opex", "finance.gross_profit", "finance.operating_profit", "production.output",
        "production.utilization", "materials.need_value", "procurement.budget", "materials.end_stock_value", "materials.items_short", "finance.net_cash",
        "finance.min_cash", "finance.financing_need",
    ];

    // ---------------- снимок ----------------

    public async Task<BaselineView> BaselineAsync(AnalytikPrincipal p, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (defaultFrom, defaultTo) = ScenarioBaselineBuilder.DefaultPeriod(FieldClock.LocalDate(clock.GetUtcNow()));
        var stored = await builder.BuildAsync(p, from ?? defaultFrom, to ?? defaultTo, ct);
        cache.Set(DataKey(stored.Id), stored.Data, TimeSpan.FromMinutes(30));
        var row = await adb.ScenarioBaselines.AsNoTracking().FirstAsync(b => b.Id == stored.Id, ct);
        return View(row, stored.Data);
    }

    public async Task<BaselineView> BaselineByIdAsync(AnalytikPrincipal p, Guid id, CancellationToken ct)
    {
        var (row, data) = await LoadBaselineAsync(p, id, ct);
        return View(row, data);
    }

    /// <summary>Черновой расчёт по снимку — без сохранения (экран пересчитывает при каждом изменении параметров).</summary>
    public async Task<SimulationResult> RunAsync(AnalytikPrincipal p, RunInput input, CancellationToken ct)
    {
        var (_, data) = await LoadBaselineAsync(p, input.BaselineId, ct);
        return await SimulateAsync(data, new ScenarioDefinition(input.Kind, input.HorizonDays, input.Parameters ?? [], input.NewRegionName), ct);
    }

    /// <summary>Снимок для анализа сценария: сохранённый (id) или новый за период; права — как у расчёта.</summary>
    public async Task<(Guid Id, ScenarioBaselineData Data)> BaselineDataAsync(AnalytikPrincipal p, Guid? id, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (id is { } existing)
        {
            var (row, data) = await LoadBaselineAsync(p, existing, ct);
            return (row.Id, data);
        }

        var (defaultFrom, defaultTo) = ScenarioBaselineBuilder.DefaultPeriod(FieldClock.LocalDate(clock.GetUtcNow()));
        var stored = await builder.BuildAsync(p, from ?? defaultFrom, to ?? defaultTo, ct);
        cache.Set(DataKey(stored.Id), stored.Data, TimeSpan.FromMinutes(30));
        return (stored.Id, stored.Data);
    }

    /// <summary>Тяжёлый расчёт по снимку (анализ — много запусков движка) — в общей очереди расчётов сценариев.</summary>
    public async Task<T> GuardAsync<T>(Func<T> work, CancellationToken ct)
    {
        if (!await RunGate.WaitAsync(TimeSpan.FromSeconds(10), ct))
        {
            throw new SimulationBusyException("Сейчас считается слишком много сценариев — повторите через несколько секунд.");
        }

        try
        {
            return await Task.Run(work, ct);
        }
        finally
        {
            RunGate.Release();
        }
    }

    private async Task<SimulationResult> SimulateAsync(ScenarioBaselineData data, ScenarioDefinition def, CancellationToken ct)
    {
        if (!await RunGate.WaitAsync(TimeSpan.FromSeconds(10), ct))
        {
            throw new SimulationBusyException("Сейчас считается слишком много сценариев — повторите через несколько секунд.");
        }

        try
        {
            return ScenarioEngine.Run(data, def);
        }
        finally
        {
            RunGate.Release();
        }
    }

    // ---------------- сценарии ----------------

    public async Task<IReadOnlyList<ScenarioRow>> ListAsync(AnalytikPrincipal p, bool archived, CancellationToken ct)
    {
        var rows = await adb.Scenarios.AsNoTracking().Include(s => s.Baseline)
            .Where(s => s.Tenant == options.Tenant && (archived ? s.Status == AnalytikScenario.Archived : s.Status == AnalytikScenario.Active))
            .Where(s => s.OwnerId == p.UserId || p.Manager)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Take(300)
            .ToListAsync(ct);
        var coverage = p.Manager ? await CoverageAsync(p, ct) : null;
        return rows.Where(s => s.OwnerId == p.UserId || (coverage is not null && Covers(coverage, s.Baseline!)))
            .Select(s =>
            {
                var result = s.Result is null ? null : SimulationJson.Read<SimulationResult>(s.Result);
                return new ScenarioRow(s.Id, s.Name, s.Kind, s.Preset, s.Status, s.Headline, s.CalculatedAt, s.Baseline!.PeriodFrom, s.Baseline.PeriodTo,
                    s.Baseline.AsOf, s.HorizonDays, s.OwnerId == p.UserId, s.Revision, s.CreatedAt, s.UpdatedAt, result?.Metric("sales.revenue")?.DeltaPercent,
                    result?.Constraints.Count(c => c.Severity == "critical") ?? 0, s.AiAnalysis is not null);
            }).ToList();
    }

    public async Task<ScenarioView> GetAsync(AnalytikPrincipal p, Guid id, CancellationToken ct)
    {
        var s = await LoadAsync(p, id, ct);
        return await ViewAsync(p, s, ct);
    }

    public async Task<ScenarioView> SaveAsync(AnalytikPrincipal p, Guid? id, ScenarioInput input, CancellationToken ct)
    {
        var name = (input.Name ?? "").Trim();
        if (name.Length is 0 or > 200)
        {
            throw new ArgumentException("Название сценария — от 1 до 200 символов.");
        }

        var preset = input.Preset ?? ScenarioPresets.Custom;
        if (!ScenarioPresets.All.Contains(preset))
        {
            throw new ArgumentException("Набор сценария: base, optimistic, pessimistic или custom.");
        }

        var (baseline, data) = await LoadBaselineAsync(p, input.BaselineId, ct);
        var def = new ScenarioDefinition(input.Kind, input.HorizonDays, input.Parameters ?? [], Clean(input.NewRegionName));
        var result = await SimulateAsync(data, def, ct);
        if (!result.Valid)
        {
            throw new ArgumentException("Сценарий не сохранён: " + string.Join(" ", result.Issues.Where(i => i.Severity == ScenarioIssue.Error).Select(i => i.Text)));
        }

        AnalytikScenario s;
        var now = Now();
        if (id is { } existing)
        {
            s = await LoadAsync(p, existing, ct, track: true);
            Own(p, s);
            if (s.Status == AnalytikScenario.Archived)
            {
                throw new ArgumentException("Сценарий в архиве — сначала верните его из архива.");
            }

            if (s.BaselineId != baseline.Id)
            {
                AddHistory(s, await adb.ScenarioBaselines.AsNoTracking().FirstAsync(b => b.Id == s.BaselineId, ct));
                s.Revision++;
            }

            s.UpdatedAt = now;
        }
        else
        {
            s = new AnalytikScenario
            {
                Tenant = options.Tenant, OwnerId = p.UserId ?? throw new UnauthorizedAccessException("Сценарии сохраняет пользователь."), Name = name,
                Kind = input.Kind, Preset = preset, Parameters = "[]", FormulaVersion = ScenarioEngine.FormulaVersion,
            };
            adb.Scenarios.Add(s);
        }

        s.Name = name;
        s.Kind = input.Kind;
        s.Preset = preset;
        s.BaselineId = baseline.Id;
        s.HorizonDays = input.HorizonDays;
        s.NewRegionName = def.NewRegionName;
        s.Parameters = SimulationJson.Write(def.Parameters);
        Apply(s, result, now);
        var tracked = await adb.ScenarioBaselines.FirstAsync(b => b.Id == baseline.Id, ct);
        tracked.Pinned = true;
        await adb.SaveChangesAsync(ct);
        await AuditAsync(p, id is null ? "analytik.scenario.create" : "analytik.scenario.update", s, new { s.Kind, parameters = def.Parameters.Count }, ct);
        return await ViewAsync(p, s, ct);
    }

    public async Task<ScenarioView> CopyAsync(AnalytikPrincipal p, Guid id, string? name, CancellationToken ct)
    {
        var source = await LoadAsync(p, id, ct);
        var copy = new AnalytikScenario
        {
            Tenant = options.Tenant, OwnerId = p.UserId ?? throw new UnauthorizedAccessException("Сценарии сохраняет пользователь."),
            Name = Clean(name) is { } n ? n[..Math.Min(200, n.Length)] : Trim200($"{source.Name} (копия)"), Kind = source.Kind, Preset = ScenarioPresets.Custom,
            BaselineId = source.BaselineId, HorizonDays = source.HorizonDays, NewRegionName = source.NewRegionName, Parameters = source.Parameters,
            Result = source.Result, Headline = source.Headline, CalculatedAt = source.CalculatedAt, FormulaVersion = source.FormulaVersion,
            CopiedFromId = source.Id,
        };
        adb.Scenarios.Add(copy);
        await adb.SaveChangesAsync(ct);
        await AuditAsync(p, "analytik.scenario.copy", copy, new { from = source.Id }, ct);
        return await ViewAsync(p, copy, ct);
    }

    public async Task<ScenarioView> ArchiveAsync(AnalytikPrincipal p, Guid id, bool archive, CancellationToken ct)
    {
        var s = await LoadAsync(p, id, ct, track: true);
        Own(p, s);
        s.Status = archive ? AnalytikScenario.Archived : AnalytikScenario.Active;
        s.ArchivedAt = archive ? Now() : null;
        s.UpdatedAt = Now();
        await adb.SaveChangesAsync(ct);
        await AuditAsync(p, archive ? "analytik.scenario.archive" : "analytik.scenario.restore", s, null, ct);
        return await ViewAsync(p, s, ct);
    }

    /// <summary>
    /// «Пересчитать на актуальных данных»: новый снимок за такой же базовый период, сдвинутый к последним данным (целые месяцы — те же число
    /// последних полных месяцев, иначе — столько же дней по вчера), тот же сценарий; прежний расчёт уходит в историю.
    /// </summary>
    public async Task<ScenarioView> RecalculateAsync(AnalytikPrincipal p, Guid id, CancellationToken ct)
    {
        var s = await LoadAsync(p, id, ct, track: true);
        Own(p, s);
        var old = await adb.ScenarioBaselines.AsNoTracking().FirstAsync(b => b.Id == s.BaselineId, ct);
        var (from, to) = Shift(old.PeriodFrom, old.PeriodTo, FieldClock.LocalDate(clock.GetUtcNow()));
        var stored = await builder.BuildAsync(p, from, to, ct, fresh: true);
        cache.Set(DataKey(stored.Id), stored.Data, TimeSpan.FromMinutes(30));
        var parameters = SimulationJson.Read<List<ScenarioParameter>>(s.Parameters);
        var result = await SimulateAsync(stored.Data, new ScenarioDefinition(s.Kind, s.HorizonDays, parameters, s.NewRegionName), ct);
        AddHistory(s, old);
        s.Revision++;
        s.BaselineId = stored.Id;
        Apply(s, result, Now());
        s.AiAnalysis = null;
        s.AiAt = null;
        s.UpdatedAt = Now();
        var tracked = await adb.ScenarioBaselines.FirstAsync(b => b.Id == stored.Id, ct);
        tracked.Pinned = true;
        await adb.SaveChangesAsync(ct);
        await AuditAsync(p, "analytik.scenario.recalculate", s, new { baseline = stored.Id, from, to, result.Valid }, ct);
        return await ViewAsync(p, s, ct);
    }

    /// <summary>Сохранённый AI-анализ сценария (его пишет ScenarioAnalyst по кнопке).</summary>
    public async Task SaveAiAsync(AnalytikPrincipal p, Guid id, string text, CancellationToken ct)
    {
        var s = await LoadAsync(p, id, ct, track: true);
        Own(p, s);
        s.AiAnalysis = text;
        s.AiAt = Now();
        await adb.SaveChangesAsync(ct);
    }

    public static (DateOnly From, DateOnly To) Shift(DateOnly from, DateOnly to, DateOnly today)
    {
        var wholeMonths = from.Day == 1 && to.Day == DateTime.DaysInMonth(to.Year, to.Month);
        if (wholeMonths)
        {
            var months = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
            var end = new DateOnly(today.Year, today.Month, 1).AddDays(-1);
            return (new DateOnly(end.Year, end.Month, 1).AddMonths(-(months - 1)), end);
        }

        var days = to.DayNumber - from.DayNumber;
        var last = today.AddDays(-1);
        return (last.AddDays(-days), last);
    }

    // ---------------- сравнение ----------------

    public async Task<ScenarioComparison> CompareAsync(AnalytikPrincipal p, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count is < 2 or > MaxCompare)
        {
            throw new ArgumentException($"Для сравнения выберите от 2 до {MaxCompare} сценариев.");
        }

        var scenarios = new List<(AnalytikScenario S, SimulationResult R, AnalytikScenarioBaseline B)>();
        foreach (var id in distinct)
        {
            var s = await LoadAsync(p, id, ct);
            var b = await adb.ScenarioBaselines.AsNoTracking().FirstAsync(x => x.Id == s.BaselineId, ct);
            var r = s.Result is null ? null : SimulationJson.Read<SimulationResult>(s.Result);
            if (r is null)
            {
                throw new ArgumentException($"Сценарий «{s.Name}» не посчитан.");
            }

            scenarios.Add((s, r, b));
        }

        var notes = new List<string>();
        if (scenarios.Select(x => x.B.Id).Distinct().Count() > 1)
        {
            notes.Add("Сценарии посчитаны на разных снимках исходных данных: часть разницы — от данных, а не от параметров.");
        }

        if (scenarios.Select(x => x.S.HorizonDays).Distinct().Count() > 1)
        {
            notes.Add("У сценариев разные горизонты — суммы за период несопоставимы напрямую.");
        }

        var rows = new List<ComparisonRow>();
        foreach (var code in ComparedMetrics)
        {
            var metrics = scenarios.Select(x => x.R.Metric(code)).ToList();
            var first = metrics.FirstOrDefault(m => m is not null);
            if (first is null)
            {
                continue;
            }

            rows.Add(Row(code, first.Group, first.Title, first.Unit, metrics.Select(m => m?.Scenario).ToList()));
        }

        rows.Add(Row("risks.count", "risks", "Рисков", "шт", scenarios.Select(x => (decimal?)x.R.Risks.Count).ToList()));
        rows.Add(Row("constraints.critical", "risks", "Критичных ограничений", "шт",
            scenarios.Select(x => (decimal?)x.R.Constraints.Count(c => c.Severity == "critical")).ToList()));
        await AuditAsync(p, "analytik.scenario.compare", null, new { ids = distinct }, ct);
        return new ScenarioComparison(
            scenarios.Select(x => new ComparisonColumn(x.S.Id, x.S.Name, x.S.Preset, x.S.Kind, x.S.HorizonDays, x.B.AsOf, x.R.Valid, x.S.Headline)).ToList(),
            rows, notes);
    }

    internal static ComparisonRow Row(string code, string group, string title, string unit, IReadOnlyList<decimal?> values)
    {
        var first = values[0];
        var delta = values.Select(v => v is { } x && first is { } f ? x - f : (decimal?)null).ToList();
        var pct = values.Select(v => v is { } x && first is { } f && f != 0 ? Math.Round((x - f) / Math.Abs(f) * 100, 1) : (decimal?)null).ToList();
        return new ComparisonRow(code, group, title, unit, values, delta, pct);
    }

    // ---------------- набор: базовый / оптимистичный / пессимистичный ----------------

    /// <summary>
    /// Набор из трёх сценариев на снимке и параметрах исходного: базовый (без изменений, только исходные данные), оптимистичный и пессимистичный —
    /// спрос сдвинут на верхнюю и нижнюю границу прогноза недельных продаж (кг) на горизонт (Forecaster, 80 %): разброс — из ошибки прогноза по истории,
    /// а не выдуманный процент.
    /// </summary>
    public async Task<IReadOnlyList<ScenarioView>> CreateSetAsync(AnalytikPrincipal p, Guid id, CancellationToken ct)
    {
        var source = await LoadAsync(p, id, ct);
        var (baseline, data) = await LoadBaselineAsync(p, source.BaselineId, ct);
        if (!SalesAnalytik.CanView(p) || data.Sales is null)
        {
            throw new ArgumentException("Оптимистичный и пессимистичный сценарии строятся по прогнозу продаж — нужен доступ к продажам.");
        }

        // Прогноз недельных продаж (кг) по полным неделям до конца базы: дни недели не раздувают разброс суммы за горизонт.
        var history = await sales.DailyAsync("kg", data.To.AddDays(-7 * WeeksOfHistory - 6), data.To, p, ct);
        var weekly = history.GroupBy(d => d.Date.AddDays(-(((int)d.Date.DayOfWeek + 6) % 7)))
            .Where(g => g.Count() == 7)
            .OrderBy(g => g.Key)
            .Select(g => new SeriesPoint(g.Key, g.Sum(d => d.Value)))
            .ToList();
        if (weekly.Count(w => w.Value > 0) < 8)
        {
            throw new ArgumentException("Для прогноза продаж мало истории (меньше 8 полных недель с продажами) — набор не построен.");
        }

        var forecast = Forecaster.Forecast(weekly, "week", Math.Max(1, (int)Math.Ceiling(source.HorizonDays / 7.0)));
        if (forecast.Total <= 0)
        {
            throw new ArgumentException("Прогноз продаж нулевой — набор не построен.");
        }

        var up = Math.Round((forecast.TotalUpper / forecast.Total - 1) * 100, 1);
        var down = Math.Round((forecast.TotalLower / forecast.Total - 1) * 100, 1);
        var parameters = SimulationJson.Read<List<ScenarioParameter>>(source.Parameters);
        var inputs = parameters.Where(x => ScenarioCatalog.Find(x.Code)?.Mode == ParameterDefinition.Input).ToList();
        var demand = parameters.FirstOrDefault(x => x.Code == "sales.demand")?.Value ?? 0;
        List<ScenarioParameter> WithDemand(decimal band)
        {
            var value = Math.Clamp(Math.Round(((1 + demand / 100) * (1 + band / 100) - 1) * 100, 1), -90, 300);
            return parameters.Where(x => x.Code != "sales.demand").Append(new ScenarioParameter("sales.demand", value)).ToList();
        }

        var title = source.Name.Length > 150 ? source.Name[..150] : source.Name;
        var created = new List<ScenarioView>
        {
            await SaveAsync(p, null, new ScenarioInput($"{title} — базовый", source.Kind, ScenarioPresets.Base, baseline.Id, source.HorizonDays, inputs, source.NewRegionName), ct),
            await SaveAsync(p, null, new ScenarioInput($"{title} — оптимистичный (спрос {Signed(up)} к прогнозу)", source.Kind, ScenarioPresets.Optimistic, baseline.Id,
                source.HorizonDays, WithDemand(up), source.NewRegionName), ct),
            await SaveAsync(p, null, new ScenarioInput($"{title} — пессимистичный (спрос {Signed(down)} к прогнозу)", source.Kind, ScenarioPresets.Pessimistic,
                baseline.Id, source.HorizonDays, WithDemand(down), source.NewRegionName), ct),
        };
        return created;
    }

    private static string Signed(decimal v) => (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) + " %";

    // ---------------- доступ ----------------

    /// <summary>Что видит пользователь: разделы и область продаж (для проверки «права покрывают снимок»).</summary>
    internal sealed record Coverage(IReadOnlySet<string> Sections, AnalytikPrincipal Principal, bool HrAll);

    private async Task<Coverage> CoverageAsync(AnalytikPrincipal p, CancellationToken ct)
    {
        var ctx = p.UserId is { } uid && !p.System ? await hr.ContextAsync(uid, p.Permissions, ct) : null;
        return new Coverage(ScenarioBaselineBuilder.SectionsOf(p, ctx?.HasAccess ?? p.System).ToHashSet(), p, p.System || ctx?.SeesAll == true);
    }

    /// <summary>Права пользователя покрывают снимок: все его разделы видны, область продаж не уже, персонал — весь (если в снимке персонал).</summary>
    internal static bool Covers(Coverage c, AnalytikScenarioBaseline b)
    {
        var sections = b.Sections.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (!sections.All(c.Sections.Contains) || (sections.Contains("workforce") && !c.HrAll))
        {
            return false;
        }

        if (!sections.Contains("sales") || !c.Principal.Sales.Restricted)
        {
            return true;
        }

        var parts = b.ScopeKey.Split('|');
        var level = parts.Length > 1 ? parts[1] : "";
        var regions = parts.Length > 2 ? parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries) : [];
        var agent = parts.Length > 3 ? parts[3] : "";
        return c.Principal.Sales.Level switch
        {
            SalesRowScope.RegionLevel => level == SalesRowScope.RegionLevel && regions.All(r => c.Principal.Sales.RegionIds.Any(x => x.ToString() == r)),
            SalesRowScope.AgentLevel => level == SalesRowScope.AgentLevel && agent == c.Principal.Sales.AgentId?.ToString(),
            _ => false,
        };
    }

    private async Task<AnalytikScenario> LoadAsync(AnalytikPrincipal p, Guid id, CancellationToken ct, bool track = false)
    {
        var q = track ? adb.Scenarios : adb.Scenarios.AsNoTracking();
        var s = await q.Include(x => x.Baseline).FirstOrDefaultAsync(x => x.Id == id && x.Tenant == options.Tenant, ct);
        if (s is null || (s.OwnerId != p.UserId && !(p.Manager && Covers(await CoverageAsync(p, ct), s.Baseline!))))
        {
            // Чужой сценарий без права — как несуществующий: его существование не раскрывается.
            throw new KeyNotFoundException("Сценарий не найден.");
        }

        return s;
    }

    private static void Own(AnalytikPrincipal p, AnalytikScenario s)
    {
        if (s.OwnerId != p.UserId)
        {
            throw new UnauthorizedAccessException("Изменять сценарий может только его автор — сделайте копию.");
        }
    }

    private async Task<(AnalytikScenarioBaseline Row, ScenarioBaselineData Data)> LoadBaselineAsync(AnalytikPrincipal p, Guid id, CancellationToken ct)
    {
        var row = await adb.ScenarioBaselines.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.Tenant == options.Tenant, ct);
        if (row is null || (row.CreatedById != p.UserId && !(p.Manager && Covers(await CoverageAsync(p, ct), row))))
        {
            throw new KeyNotFoundException("Снимок исходных данных не найден — обновите страницу.");
        }

        if (!cache.TryGetValue(DataKey(id), out ScenarioBaselineData? data) || data is null)
        {
            data = SimulationJson.Read<ScenarioBaselineData>(row.Data);
            cache.Set(DataKey(id), data, TimeSpan.FromMinutes(30));
        }

        return (row, data);
    }

    private static string DataKey(Guid id) => $"analytik:sim:data:{id}";

    // ---------------- представление ----------------

    private async Task<ScenarioView> ViewAsync(AnalytikPrincipal p, AnalytikScenario s, CancellationToken ct)
    {
        var (row, data) = await LoadBaselineAsync(p, s.BaselineId, ct);
        var history = SimulationJson.Read<List<ScenarioHistoryEntry>>(s.History);
        return new ScenarioView(s.Id, s.Name, s.Kind, s.Preset, s.Status, s.HorizonDays, s.NewRegionName, SimulationJson.Read<List<ScenarioParameter>>(s.Parameters),
            s.Result is null ? null : SimulationJson.Read<SimulationResult>(s.Result), s.AiAnalysis, s.AiAt, s.CalculatedAt, s.FormulaVersion,
            s.FormulaVersion != ScenarioEngine.FormulaVersion, s.Revision, history, s.OwnerId == p.UserId, s.CopiedFromId, s.CreatedAt, s.UpdatedAt, View(row, data));
    }

    internal static BaselineView View(AnalytikScenarioBaseline row, ScenarioBaselineData d)
    {
        var sales = d.Sales;
        var withRecipe = sales?.Products.Count(x => x.Norms.Count > 0) ?? 0;
        var summary = new BaselineSummary(sales?.Revenue, sales?.Kg, sales?.DiscountShare, sales?.Products.Count ?? 0, withRecipe,
            sales is { Kg: > 0 } ? Math.Round(sales.Products.Where(x => x.Norms.Count > 0).Sum(x => x.Kg) / sales.Kg, 4) : null, sales?.Regions.Count ?? 0,
            sales?.PlanKgMonth, sales?.PlanSource, sales?.DataThrough, d.Materials?.Count ?? 0, d.Materials?.Count(m => m.Price is not null) ?? 0,
            d.Materials?.Count(m => m.LeadTimeAssumed) ?? 0, d.Production?.CapacityKgDay, d.Production?.CapacitySource, d.Production?.ShiftsPerDay,
            d.Production?.DefectRate, d.Finance?.Payments, d.Finance?.CollectionRate, d.Finance?.PaymentLagDays, d.Finance?.ProcurementSpend,
            d.Workforce?.Employees, d.Workforce?.Divisions.Count ?? 0);
        var items = d.Materials ?? [];
        var choices = new BaselineChoices(
            (sales?.Regions ?? []).Select(r => new ChoiceItem(r.Id, r.Name, null)).ToList(),
            (sales?.Products ?? []).Take(400).Select(x => new ChoiceItem(x.Id.ToString(), x.Name, x.Norms.Count > 0 ? x.RecipeCode : null)).ToList(),
            items.OrderBy(m => m.Name).Select(m => new ChoiceItem(m.ItemId.ToString(), m.Name, m.Unit)).ToList(),
            items.Where(m => m.Suppliers.Count > 0).OrderBy(m => m.Name)
                .SelectMany(m => m.Suppliers.Select(s => new ChoiceItem($"{m.ItemId}/{s.Key}", $"{m.Name} — {s.Name}",
                    s.Price is { } pr ? $"{Math.Round(pr, 2)} сум · {(s.LeadTimeDays is { } lt ? $"{lt} дн." : "срок не задан")}" : null)))
                .ToList(),
            (d.Workforce?.Divisions ?? []).Select(x => new ChoiceItem(x.Id, x.Name, $"{x.Employees} чел.")).ToList());
        return new BaselineView(row.Id, d.From, d.To, d.Days, d.AsOf, row.CreatedAt, d.FormulaVersion, d.Sections, d.SalesScope, d.Sources, d.Notes, summary, choices);
    }

    private static void Apply(AnalytikScenario s, SimulationResult result, DateTimeOffset now)
    {
        s.Result = SimulationJson.Write(result);
        s.Headline = result.Conclusion.Headline.Length > 2000 ? result.Conclusion.Headline[..2000] : result.Conclusion.Headline;
        s.CalculatedAt = now;
        s.FormulaVersion = ScenarioEngine.FormulaVersion;
    }

    private static void AddHistory(AnalytikScenario s, AnalytikScenarioBaseline previous)
    {
        var history = SimulationJson.Read<List<ScenarioHistoryEntry>>(s.History);
        var result = s.Result is null ? null : SimulationJson.Read<SimulationResult>(s.Result);
        history.Insert(0, new ScenarioHistoryEntry(s.Revision, s.CalculatedAt ?? s.CreatedAt, previous.Id, previous.PeriodFrom, previous.PeriodTo, previous.AsOf,
            s.FormulaVersion, s.Headline, result?.Metric("sales.revenue")?.Base, result?.Metric("sales.revenue")?.Scenario));
        s.History = SimulationJson.Write(history.Take(20).ToList());
    }

    private Task AuditAsync(AnalytikPrincipal p, string action, AnalytikScenario? s, object? data, CancellationToken ct) =>
        audit.LogAsync(ActorType.User, p.UserId?.ToString() ?? "system", action, s is null ? null : "analytik.scenario", s?.Id.ToString(), data, ct);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Trim200(string value) => value.Length > 200 ? value[..200] : value;
}

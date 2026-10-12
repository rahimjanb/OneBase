using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Primary;
using OneBase.Domain.AI;

namespace OneBase.Application.Sales.Pnl;

public sealed record PnlLineView(string Key, string Label, string Format, bool Minus, bool Detail, bool Input);

/// <param name="HasSales">Загружены продажи или себестоимость.</param>
/// <param name="HasExpenses">Загружена хотя бы одна строка расходов (реестр «Прочие расходы», логистика).</param>
public sealed record PnlMonthRef(int Year, int Month, bool HasSales, bool HasExpenses);

/// <param name="Region">Регион OneBase, к которому отнесён контрагент (по имени дилера региона или названию региона в имени).</param>
public sealed record PnlDealerView(string Key, string Name, string? Region, IReadOnlyDictionary<string, decimal?> Values);

/// <summary>Группа РМ (направление продаж) или «Прочие контрагенты».</summary>
public sealed record PnlGroupView(string Id, string Name, string? Manager, IReadOnlyList<PnlDealerView> Dealers, IReadOnlyDictionary<string, decimal?> Total);

/// <summary>Последняя загрузка P&L через AI Data Manager.</summary>
public sealed record PnlSourceInfo(Guid ChangeSetId, string Title, string? FileName, DateTimeOffset? AppliedAt, string? AppliedBy);

public sealed record PnlView(
    int? Year,
    int? Month,
    IReadOnlyList<PnlMonthRef> Months,
    IReadOnlyList<PnlLineView> Lines,
    IReadOnlyList<PnlGroupView> Groups,
    IReadOnlyDictionary<string, decimal?> Total,
    bool NoExpenses,
    IReadOnlyList<string> LoadedLines,
    PnlSourceInfo? LastLoad);

public sealed record PnlDealerMonth(int Month, IReadOnlyDictionary<string, decimal?> Values);

/// <summary>Карточка дилера: месяцы года, итог года (проценты от сумм) и темп роста выбранного месяца к предыдущему.</summary>
public sealed record PnlDealerCard(
    string Key,
    string Name,
    string? Region,
    int Year,
    IReadOnlyList<PnlLineView> Lines,
    IReadOnlyList<PnlDealerMonth> Months,
    IReadOnlyDictionary<string, decimal?> Total,
    int? GrowthMonth,
    string? GrowthBase,
    IReadOnlyDictionary<string, decimal?>? Growth);

/// <summary>Сверка строки «Продажи» P&L с первичкой OneBase (сумма отгрузки по цене дилера из Linko).</summary>
public sealed record PnlPrimaryRow(string Dealer, string? PrimaryName, decimal? PnlSales, decimal? PrimarySum, decimal? Diff, decimal? DiffShare, string? Problem);

public sealed record PnlPrimaryComparison(
    int Year,
    int Month,
    DateOnly? PrimaryDataThrough,
    IReadOnlyList<PnlPrimaryRow> Rows,
    decimal PnlTotal,
    decimal PrimaryMatchedTotal,
    decimal PrimaryDealerTotal,
    string Note);

/// <summary>
/// P&L дилеров: данные — sales."PnlValues" (исходные строки, загружаются только через AI Data Manager), расчёт — <see cref="PnlMath"/>.
/// Linko и синхронизация эти данные не трогают; демонстрационных данных нет.
/// </summary>
public sealed class PnlService(IAppDbContext db, SalesOptions salesOptions, PrimaryService primary)
{
    public const string OtherGroup = "other";
    public const string OtherGroupName = "Прочие контрагенты";

    public static IReadOnlyList<PnlLineView> LineViews { get; } = PnlMath.Lines
        .Select(l => new PnlLineView(l.Key, l.Label, l.Format switch
        {
            PnlFormat.Percent => "pct",
            PnlFormat.NotAvailable => "na",
            _ => "money",
        }, l.Minus, l.Detail, l.Input))
        .ToList();

    private sealed record Row(int Year, int Month, string Dealer, string DealerKey, string Line, decimal Amount);

    /// <summary>Месяцы с данными P&L (новые сверху).</summary>
    public async Task<IReadOnlyList<PnlMonthRef>> MonthsAsync(CancellationToken ct = default)
    {
        var rows = await db.SalesPnlValues.AsNoTracking()
            .GroupBy(v => new { v.Year, v.Month, v.Line })
            .Select(g => g.Key)
            .ToListAsync(ct);
        return rows.GroupBy(r => (r.Year, r.Month))
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g => new PnlMonthRef(g.Key.Year, g.Key.Month,
                g.Any(r => r.Line is PnlMath.Sales or PnlMath.Cost),
                g.Any(r => PnlMath.Expenses.Contains(r.Line))))
            .ToList();
    }

    /// <summary>
    /// P&L месяца по дилерам, сгруппированным по РМ. Без year/month — последний месяц, где есть и продажи, и расходы
    /// (неполный месяц только с продажами или только с логистикой не открывается по умолчанию), иначе последний с данными.
    /// </summary>
    public async Task<PnlView> GetAsync(int? year, int? month, CancellationToken ct = default)
    {
        var months = await MonthsAsync(ct);
        var lastLoad = await LastLoadAsync(ct);
        var selected = year is { } y && month is { } m
            ? months.FirstOrDefault(x => x.Year == y && x.Month == m) ?? new PnlMonthRef(y, m, false, false)
            : months.FirstOrDefault(x => x.HasSales && x.HasExpenses) ?? months.FirstOrDefault();
        if (selected is null)
        {
            return new PnlView(null, null, months, LineViews, [], PnlMath.Sum([]), false, [], lastLoad);
        }

        var rows = await RowsAsync(selected.Year, selected.Month, null, ct);
        var scope = rows.Select(r => r.Line).ToHashSet();
        var dealers = Dealers(rows, scope);
        var regions = await RegionsAsync(ct);
        var groups = Group(dealers, regions);
        return new PnlView(selected.Year, selected.Month, months, LineViews, groups,
            PnlMath.Sum(dealers.Select(d => d.Values)),
            NoExpenses: rows.Count > 0 && !rows.Any(r => PnlMath.Expenses.Contains(r.Line)),
            PnlMath.Lines.Where(l => scope.Contains(l.Key)).Select(l => l.Key).ToList(),
            lastLoad);
    }

    /// <summary>Карточка дилера за год: столбцы — месяцы года с данными P&L, «Итого», темп роста month к предыдущему месяцу.</summary>
    public async Task<PnlDealerCard?> DealerAsync(string dealer, int year, int? month, CancellationToken ct = default)
    {
        var key = PnlMath.DealerKey(dealer);
        var yearRows = await RowsAsync(year, null, null, ct);
        var own = yearRows.Where(r => r.DealerKey == key).ToList();
        if (own.Count == 0)
        {
            return null;
        }

        var name = own.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).First().Dealer;
        var monthList = yearRows.Select(r => r.Month).Distinct().Order().ToList();
        var byMonth = monthList.ToDictionary(m => m, m => Values(own.Where(r => r.Month == m).ToList(), Scope(yearRows, year, m)));
        var months = monthList.Select(m => new PnlDealerMonth(m, byMonth[m])).ToList();

        var growthMonth = month is { } gm && monthList.Contains(gm) ? gm : monthList.Where(m => own.Any(r => r.Month == m)).DefaultIfEmpty().Max();
        Dictionary<string, decimal?>? growth = null;
        string? growthBase = null;
        if (growthMonth > 0)
        {
            var (py, pm) = growthMonth == 1 ? (year - 1, 12) : (year, growthMonth - 1);
            var previousRows = py == year ? yearRows.Where(r => r.Month == pm).ToList() : await RowsAsync(py, pm, null, ct);
            var previousOwn = previousRows.Where(r => r.DealerKey == key).ToList();
            if (previousOwn.Count > 0)
            {
                growth = PnlMath.Growth(byMonth[growthMonth], Values(previousOwn, previousRows.Select(r => r.Line).ToHashSet()));
                growthBase = $"{DataEntityTypes.MonthLabel(year, growthMonth)} к {DataEntityTypes.MonthLabel(py, pm)}";
            }
        }

        var regions = await RegionsAsync(ct);
        return new PnlDealerCard(key, name, RegionOf(name, regions)?.Name, year, LineViews, months, PnlMath.Sum(byMonth.Values),
            growthMonth > 0 ? growthMonth : null, growthBase, growth);
    }

    /// <summary>
    /// Значения P&L месяца по дилерам (исходные и расчётные) — для AI Data Manager: просмотр текущих данных и проверка после применения.
    /// </summary>
    public async Task<(IReadOnlyList<PnlDealerView> Dealers, IReadOnlyDictionary<string, decimal?> Total)> MonthAsync(int year, int month, CancellationToken ct = default)
    {
        var rows = await RowsAsync(year, month, null, ct);
        var dealers = Dealers(rows, rows.Select(r => r.Line).ToHashSet());
        return (dealers, PnlMath.Sum(dealers.Select(d => d.Values)));
    }

    /// <summary>
    /// Сверка «Продаж» P&L с первичкой OneBase за месяц: первичка — отгрузка завода по цене дилера (перемещения и заказы Linko),
    /// контрагент сопоставляется по имени дилера, иначе по региону в имени. Только сравнение — ничего не меняется.
    /// </summary>
    public async Task<PnlPrimaryComparison> ComparePrimaryAsync(int year, int month, CancellationToken ct = default)
    {
        var rows = await RowsAsync(year, month, PnlMath.Sales, ct);
        var view = await primary.GetAsync(year, month, ct);
        var parties = view.Dealers.Where(d => d.Group == PrimaryGroups.Dealer || d.Group == PrimaryGroups.Direct).ToList();
        var regions = await RegionsAsync(ct);
        var used = new HashSet<string>();
        var result = new List<PnlPrimaryRow>();
        foreach (var dealer in rows.GroupBy(r => r.DealerKey).OrderByDescending(g => g.Sum(r => r.Amount)))
        {
            var name = dealer.First().Dealer;
            var sales = dealer.Sum(r => r.Amount);
            var match = parties.Where(p => PnlMath.DealerKey(p.Name) == dealer.Key).ToList();
            if (match.Count == 0 && RegionOf(name, regions) is { } region)
            {
                match = parties.Where(p => p.Group == PrimaryGroups.Dealer
                                           && (DataValues.Normalize(p.Sub ?? p.Name) == DataValues.Normalize(region.Name))).ToList();
            }

            if (match.Count != 1)
            {
                result.Add(new PnlPrimaryRow(name, null, sales, null, null, null,
                    match.Count == 0 ? "в первичке OneBase контрагент не найден" : $"подходит к нескольким: {string.Join(", ", match.Select(p => p.Name))}"));
                continue;
            }

            var party = match[0];
            used.Add(party.Id);
            var sum = party.Month.SumFactory;
            result.Add(new PnlPrimaryRow(name, party.Sub is null ? party.Name : $"{party.Name} ({party.Sub})", sales, sum, sales - sum,
                sum != 0 ? (sales - sum) / sum : null, null));
        }

        foreach (var party in parties.Where(p => !used.Contains(p.Id) && p.Month.SumFactory != 0))
        {
            result.Add(new PnlPrimaryRow("—", party.Sub is null ? party.Name : $"{party.Name} ({party.Sub})", null, party.Month.SumFactory, null, null,
                "в P&L нет продаж этого контрагента"));
        }

        return new PnlPrimaryComparison(year, month, view.DataThrough, result,
            result.Sum(r => r.PnlSales ?? 0),
            result.Where(r => r.PnlSales is not null).Sum(r => r.PrimarySum ?? 0),
            view.DealerTotal.Month.SumFactory,
            "Первичка OneBase — отгрузка завода по цене дилера из Linko (перемещения на склады дилеров и заказы «Завода» точкам), возвраты — минусом. "
            + "Расхождение — повод проверить отчёт или Linko; P&L и первичка не правятся автоматически."
            + (view.Running ? $" Месяц ещё идёт: первичка по {view.DataThrough:dd.MM.yyyy}." : ""));
    }

    private async Task<List<Row>> RowsAsync(int year, int? month, string? line, CancellationToken ct)
    {
        var query = db.SalesPnlValues.AsNoTracking().Where(v => v.Year == year);
        if (month is { } m)
        {
            query = query.Where(v => v.Month == m);
        }

        if (line is not null)
        {
            query = query.Where(v => v.Line == line);
        }

        return await query.Select(v => new Row(v.Year, v.Month, v.Dealer, v.DealerKey, v.Line, v.Amount)).ToListAsync(ct);
    }

    private static HashSet<string> Scope(IEnumerable<Row> rows, int year, int month) =>
        rows.Where(r => r.Year == year && r.Month == month).Select(r => r.Line).ToHashSet();

    private static Dictionary<string, decimal?> Values(IReadOnlyCollection<Row> rows, IReadOnlySet<string> scope) =>
        PnlMath.Compute(rows.GroupBy(r => r.Line).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount)), scope);

    private static List<PnlDealerView> Dealers(IReadOnlyCollection<Row> rows, IReadOnlySet<string> scope) =>
        rows.GroupBy(r => r.DealerKey)
            .Select(g => new PnlDealerView(g.Key, g.First().Dealer, null, Values(g.ToList(), scope)))
            .ToList();

    private sealed record RegionRef(string Name, string? Dealer, Guid? DirectionId, string? Direction, string? Manager, int SortOrder);

    private async Task<List<RegionRef>> RegionsAsync(CancellationToken ct)
    {
        var rows = await db.SalesRegions.AsNoTracking()
            .Select(r => new
            {
                r.Name, r.DealerName, r.DirectionId,
                Direction = r.Direction == null ? null : r.Direction.Name,
                Manager = r.Direction == null ? null : r.Direction.ManagerName,
                SortOrder = r.Direction == null ? int.MaxValue : r.Direction.SortOrder,
            })
            .ToListAsync(ct);
        return rows.Where(r => !salesOptions.IsExcludedBranch(r.Name) && !salesOptions.IsIgnoredBranch(r.Name))
            .Select(r => new RegionRef(r.Name, r.DealerName, r.DirectionId, r.Direction, r.Manager, r.SortOrder))
            .ToList();
    }

    /// <summary>Регион контрагента: дилер региона с тем же именем, иначе единственный регион, название которого есть в имени («Бобур Самарканд»).</summary>
    private static RegionRef? RegionOf(string dealer, IReadOnlyList<RegionRef> regions)
    {
        var key = PnlMath.DealerKey(dealer);
        var byDealer = regions.Where(r => r.Dealer is { } d && PnlMath.DealerKey(d) == key).ToList();
        if (byDealer.Count == 1)
        {
            return byDealer[0];
        }

        var words = key.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim('(', ')', ',', '.')).ToHashSet();
        var byName = regions.Where(r => words.Contains(DataValues.Normalize(r.Name))).ToList();
        return byName.Count == 1 ? byName[0] : null;
    }

    /// <summary>Группы РМ (направления по порядку), внутри — по убыванию продаж; без направления — «Прочие контрагенты» в конце.</summary>
    private static List<PnlGroupView> Group(IReadOnlyList<PnlDealerView> dealers, IReadOnlyList<RegionRef> regions)
    {
        var placed = dealers.Select(d =>
        {
            var region = RegionOf(d.Name, regions);
            return (Dealer: d with { Region = region?.Name }, Region: region);
        }).ToList();

        return placed
            .GroupBy(p => p.Region?.DirectionId is { } id ? id.ToString() : OtherGroup)
            .Select(g =>
            {
                var region = g.First().Region;
                var list = g.Select(p => p.Dealer).OrderByDescending(d => d.Values.GetValueOrDefault(PnlMath.Sales) ?? 0).ThenBy(d => d.Name).ToList();
                return (Sort: g.Key == OtherGroup ? int.MaxValue : region?.SortOrder ?? int.MaxValue - 1,
                    View: new PnlGroupView(g.Key, g.Key == OtherGroup ? OtherGroupName : region!.Direction!, g.Key == OtherGroup ? null : region!.Manager,
                        list, PnlMath.Sum(list.Select(d => d.Values))));
            })
            .OrderBy(g => g.Sort).ThenBy(g => g.View.Name)
            .Select(g => g.View)
            .ToList();
    }

    private async Task<PnlSourceInfo?> LastLoadAsync(CancellationToken ct)
    {
        var set = await db.DataChangeSets.AsNoTracking()
            .Where(s => s.Target == DataTargets.Pnl && (s.Status == DataChangeSetStatus.Applied || s.Status == DataChangeSetStatus.PartiallyRolledBack))
            .OrderByDescending(s => s.AppliedAt)
            .Select(s => new { s.Id, s.Title, s.FileId, s.AppliedAt, s.AppliedById })
            .FirstOrDefaultAsync(ct);
        if (set is null)
        {
            return null;
        }

        var file = set.FileId is { } f ? await db.DataFiles.AsNoTracking().Where(x => x.Id == f).Select(x => x.FileName).FirstOrDefaultAsync(ct) : null;
        var by = set.AppliedById is { } u ? await db.Users.AsNoTracking().Where(x => x.Id == u).Select(x => x.FullName).FirstOrDefaultAsync(ct) : null;
        return new PnlSourceInfo(set.Id, set.Title, file, set.AppliedAt, by);
    }
}

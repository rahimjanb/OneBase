using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Stock;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales.Primary;

/// <summary>Группы контрагентов первички.</summary>
public static class PrimaryGroups
{
    /// <summary>Склад дилера (регион): перемещения с завода и возвраты на завод.</summary>
    public const string Dealer = "dealer";

    /// <summary>Точка с заказами завода — базар, сеть, фирменный магазин (SalesOptions.PrimaryOrderBranches).</summary>
    public const string Direct = "direct";

    public static string NameOf(string group) => group == Direct ? "Прямые клиенты завода" : "Дилеры";
}

/// <summary>
/// Строка первички: документ (перемещение, заказ или возврат), контрагент (ключ строки таблицы), день, товар и суммы. Возврат — со знаком минус:
/// простое сложение даёт нетто (DOC-rules §3).
/// </summary>
public sealed record PrimaryShipment(string Doc, string Counterparty, DateOnly Date, long? ProductId, PrimaryAmounts Amounts, bool BoxesKnown, bool IsReturn = false);

/// <summary>Контрагент — строка таблицы: подписи, группа (PrimaryGroups; у стран — null) и план по месяцам (12 значений, null — плана нет).</summary>
public sealed record PrimaryParty(string Id, string Name, string? Sub, string? Group, IReadOnlyList<decimal?> PlanMonths);

/// <summary>Строка перемещения Linko: склады, даты (доставка, выдача, приёмка, создание), товар, количество, вес и сумма строки.</summary>
public sealed record PrimaryTransferLine(
    long TransferId,
    long? FromStockId,
    long? ToStockId,
    DateOnly? DeliveryDate,
    DateTime? GivenAt,
    DateTime? AcceptedAt,
    DateTime? CreatedAt,
    long? ProductId,
    decimal Amount,
    decimal Kg,
    decimal TotalPrice);

/// <summary>Строка заказа филиала первички (SalesOptions.PrimaryOrderBranches) с точкой Linko.</summary>
public sealed record PrimaryOrderLine(
    long OrderId,
    long MarketId,
    string MarketName,
    string? MarketType,
    string? Branch,
    string Status,
    DateOnly CreatedDate,
    string? Currency,
    long? ProductId,
    decimal Amount,
    decimal Kg,
    decimal TotalPrice);

/// <summary>Строка возврата по заказу филиала первички: деньги — количество × цена строки.</summary>
public sealed record PrimaryReturnLine(
    long ReturnId,
    long MarketId,
    string MarketName,
    string? MarketType,
    string? Branch,
    string Status,
    DateOnly CreatedDate,
    long? ProductId,
    decimal Amount,
    decimal Price,
    decimal Kg);

/// <summary>Заказы точек завода в первичке: строки, контрагенты (по точке) и заказы в другой валюте — id → дата (вес учтён, суммы нет).</summary>
public sealed record PrimaryDirect(IReadOnlyList<PrimaryShipment> Rows, IReadOnlyDictionary<string, PrimaryParty> Parties, IReadOnlyDictionary<long, DateOnly> OtherCurrencyOrders);

/// <summary>
/// Перемещения завода: Rows — отгрузки дилерам и их возвраты (строки первички); OtherStocks — перемещения на склады, которые не сводятся
/// ни к одному региону («Основной», «Оптом», «Нукус (интеграция учун)»): это не дилеры и не клиенты, в первичку они не входят,
/// контрагент строки — название склада.
/// </summary>
public sealed record PrimaryTransfers(List<PrimaryShipment> Rows, List<PrimaryShipment> OtherStocks);

/// <summary>Склад вне справочника регионов за год: название, перемещений с завода и на завод, кг нетто.</summary>
public sealed record PrimaryOtherStock(string Name, int Transfers, decimal Kg);

/// <summary>Параметры календаря отгрузок: период «с … по …» (дни месяца), день и контрагент разбора.</summary>
public sealed record PrimaryCalendarQuery(int? From = null, int? To = null, int? Day = null, string? Dealer = null);

/// <summary>Товар в разборе дня.</summary>
public sealed record PrimaryProduct(string Name, string? Code, string Category);

/// <summary>Оценка строки: сумма по цене продажи дилера (прайс SalesOptions.PrimaryDealerPriceList) и коробки по фасовке из названия.</summary>
public sealed class PrimaryPricing(IReadOnlyDictionary<long, decimal> dealerPrices, IReadOnlyDictionary<long, PackInfo?> packs, bool boxes = true)
{
    /// <summary>Только кг и сумма: у экспорта нет ни коробок (в строках заказа — штуки и кг), ни второй цены.</summary>
    public static PrimaryPricing WeightOnly { get; } = new(new Dictionary<long, decimal>(), new Dictionary<long, PackInfo?>(), boxes: false);

    /// <summary>Ноль для сумм: без коробок, если их нет.</summary>
    public PrimaryAmounts Zero => boxes ? PrimaryAmounts.Zero : PrimaryAmounts.NoBoxes;

    /// <summary>
    /// Суммы строки. Коробки — по весу коробки из названия: у весового товара (единица учёта — 1 кг) любой, у штучного — если в коробке целое
    /// число штук этой строки; иначе название не совпадает с тем, как товар учитывается, и коробки не считаются (BoxesKnown = false).
    /// </summary>
    public (PrimaryAmounts Amounts, bool BoxesKnown) Value(long? productId, decimal amount, decimal kg, decimal sum)
    {
        var dealerSum = productId is { } id && dealerPrices.TryGetValue(id, out var price) ? amount * price : 0;
        if (!boxes)
        {
            return (new PrimaryAmounts(kg, null, sum, dealerSum), false);
        }

        var pack = productId is { } p ? packs.GetValueOrDefault(p) : null;
        var unitKg = StockMath.UnitKg(kg, amount);
        var boxKg = pack?.BoxKg is { } b && unitKg is { } u && (Math.Abs(u - 1) < 0.001m || StockMath.BoxConsistent(b, u)) ? b : (decimal?)null;
        return (new PrimaryAmounts(kg, boxKg is { } bk ? kg / bk : 0, sum, dealerSum), boxKg is not null);
    }
}

/// <summary>Правила первички — чистые функции: дата, контрагенты, нетто, период по дате данных, строки таблиц, клиенты по месяцам и календарь.</summary>
public static class PrimaryMath
{
    /// <summary>Доля прошедших дней, после которой месяц считается закрытым (прогноза нет), — как в «Полевом контроле».</summary>
    public const decimal ClosedShare = 0.98m;

    public static readonly IReadOnlyList<decimal?> NoPlan = Enumerable.Repeat<decimal?>(null, 12).ToList();

    /// <summary>
    /// Дата перемещения в первичке: доставка (DeliveryDate — с ней сходится день таблицы первички), без неё — выдача, приёмка, создание.
    /// Аутсток датирует перемещения сам — по выдаче.
    /// </summary>
    public static DateOnly? TransferDate(DateOnly? delivery, DateTime? given, DateTime? accepted, DateTime? created) =>
        delivery ?? ((given ?? accepted ?? created) is { } at ? DateOnly.FromDateTime(at) : null);

    /// <summary>
    /// Перемещения → строки первички. Отгрузка — со склада завода на склад дилера (не завод и не экспорт), контрагент — склад получателя;
    /// возврат — со склада дилера на завод, контрагент — склад отправителя, суммы со знаком минус. Склад сводится к региону
    /// (SalesOptions.StockRegionName: «Коканд бозор» — Коканд); дилер — только склад, чей регион есть в справочнике регионов
    /// (isDealerRegion), dealerId — ключ строки дилера по региону. Склады вне справочника («Основной», «Нукус (интеграция учун)») — не дилеры:
    /// их перемещения — в OtherStocks, в первичку не входят. Перемещения завод ↔ экспорт и между складами дилеров не берутся.
    /// </summary>
    public static PrimaryTransfers Transfers(
        IEnumerable<PrimaryTransferLine> lines,
        IReadOnlySet<long> factory,
        IReadOnlySet<long> export,
        Func<long, string> stockName,
        SalesOptions options,
        Func<string, bool> isDealerRegion,
        Func<string, string> dealerId,
        PrimaryPricing pricing)
    {
        bool IsFactory(long? id) => id is { } s && factory.Contains(s);
        bool IsStock(long? id) => id is { } s && !factory.Contains(s) && !export.Contains(s);

        var rows = new List<PrimaryShipment>();
        var other = new List<PrimaryShipment>();
        foreach (var l in lines)
        {
            var outbound = IsFactory(l.FromStockId) && IsStock(l.ToStockId);
            var back = IsStock(l.FromStockId) && IsFactory(l.ToStockId);
            if ((!outbound && !back) || TransferDate(l.DeliveryDate, l.GivenAt, l.AcceptedAt, l.CreatedAt) is not { } date)
            {
                continue;
            }

            var (amounts, boxesKnown) = pricing.Value(l.ProductId, l.Amount, l.Kg, l.TotalPrice);
            var stock = stockName(outbound ? l.ToStockId!.Value : l.FromStockId!.Value);
            var region = options.StockRegionName(stock);
            var dealer = isDealerRegion(region);
            (dealer ? rows : other).Add(new PrimaryShipment($"t:{l.TransferId}", dealer ? dealerId(region) : stock.Trim(), date, l.ProductId,
                back ? amounts.Negate() : amounts, boxesKnown, back));
        }

        return new PrimaryTransfers(rows, other);
    }

    /// <summary>Склады вне справочника регионов за год — по строкам OtherStocks: перемещений и кг нетто, по убыванию кг.</summary>
    public static List<PrimaryOtherStock> OtherStocksOf(IEnumerable<PrimaryShipment> otherStocks, int year) =>
        otherStocks.Where(s => s.Date.Year == year)
            .GroupBy(s => s.Counterparty)
            .Select(g => new PrimaryOtherStock(g.Key, g.Select(s => s.Doc).Distinct().Count(), g.Sum(s => s.Amounts.Kg)))
            .OrderByDescending(s => s.Kg)
            .ThenBy(s => s.Name)
            .ToList();

    /// <summary>Ключ строки точки с заказами завода.</summary>
    public static string DirectId(long marketId) => $"market:{marketId}";

    /// <summary>
    /// Заказы филиалов первички (SalesOptions.PrimaryOrderBranches: «Завод», «К К Мерч») точкам не экспортного типа → строки первички:
    /// статусы PrimaryOrderStatuses (доставлен, отдан), дата — создание заказа (с ней сходится день таблицы первички), кг — вес строки, сумма —
    /// сумма строки в основной валюте (заказ в другой валюте даёт вес без суммы и считается отдельно), минус возвраты по строкам (статус
    /// ReturnStatuses, дата создания возврата, деньги = количество × цена). Точки PrimaryExcludedMarkets («Дегустатсия + Акция») не входят.
    /// Каждая точка — свой контрагент группы direct.
    /// </summary>
    public static PrimaryDirect DirectOrders(IEnumerable<PrimaryOrderLine> orders, IEnumerable<PrimaryReturnLine> returns, SalesOptions options, PrimaryPricing pricing)
    {
        var sold = options.PrimaryOrderStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var returned = options.ReturnStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<PrimaryShipment>();
        var parties = new Dictionary<string, PrimaryParty>();
        var otherCurrency = new Dictionary<long, DateOnly>();

        bool Counts(long market, string name, string? type, string? branch) =>
            options.IsPrimaryOrderBranch(branch) && !options.IsExportMarketType(type) && !options.IsPrimaryExcludedMarket(market, name);

        string Party(long market, string name, string? type, string? branch)
        {
            var id = DirectId(market);
            var sub = string.Join(" · ", new[] { type, branch }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
            parties.TryAdd(id, new PrimaryParty(id, name.Trim(), sub.Length == 0 ? null : sub, PrimaryGroups.Direct, NoPlan));
            return id;
        }

        foreach (var o in orders)
        {
            if (!sold.Contains(o.Status) || !Counts(o.MarketId, o.MarketName, o.MarketType, o.Branch))
            {
                continue;
            }

            var inBase = options.IsBaseCurrency(o.Currency);
            if (!inBase)
            {
                otherCurrency[o.OrderId] = o.CreatedDate;
            }

            var (amounts, boxesKnown) = pricing.Value(o.ProductId, o.Amount, o.Kg, inBase ? o.TotalPrice : 0);
            rows.Add(new PrimaryShipment($"o:{o.OrderId}", Party(o.MarketId, o.MarketName, o.MarketType, o.Branch), o.CreatedDate, o.ProductId, amounts, boxesKnown));
        }

        foreach (var r in returns)
        {
            if (!returned.Contains(r.Status) || !Counts(r.MarketId, r.MarketName, r.MarketType, r.Branch))
            {
                continue;
            }

            var (amounts, boxesKnown) = pricing.Value(r.ProductId, r.Amount, r.Kg, r.Amount * r.Price);
            rows.Add(new PrimaryShipment($"r:{r.ReturnId}", Party(r.MarketId, r.MarketName, r.MarketType, r.Branch), r.CreatedDate, r.ProductId,
                amounts.Negate(), boxesKnown, IsReturn: true));
        }

        return new PrimaryDirect(rows, parties, otherCurrency);
    }

    /// <summary>Строка дилера: подпись — дилер региона (sales."Regions".DealerName), если записан, иначе регион или склад; регион тогда — второй строкой.</summary>
    public static PrimaryParty DealerParty(string id, string region, string? dealerName, IReadOnlyList<decimal?> plans) =>
        string.IsNullOrWhiteSpace(dealerName)
            ? new PrimaryParty(id, region, null, PrimaryGroups.Dealer, plans)
            : new PrimaryParty(id, dealerName.Trim(), region, PrimaryGroups.Dealer, plans);

    /// <summary>План первички: строки sales."RegionPlans" вида Primary за год (планы РОП и «Завод» — планы вторички — не берутся).</summary>
    public static List<PlanRow> PlanRows(IEnumerable<RegionPlanRow> rows, int year) =>
        rows.Where(p => p.Kind == PlanKind.Primary && p.Year == year).Select(p => new PlanRow(p.RegionId, null, p.Month, p.CategoryId, p.PlanKg)).ToList();

    /// <summary>
    /// План строк дилеров по месяцам (12 значений, null — плана нет): регион плана → строка дилера (dealerOf; null — без строки), план региона
    /// за месяц — итог региона или сумма его категорий (SalesMath.PlanTotal); регионы одной строки складываются.
    /// </summary>
    public static Dictionary<string, IReadOnlyList<decimal?>> DealerPlans(IReadOnlyList<PlanRow> plans, Func<Guid, string?> dealerOf) =>
        plans.Where(p => p.RegionId is not null)
            .Select(p => (Dealer: dealerOf(p.RegionId!.Value), Plan: p))
            .Where(x => x.Dealer is not null)
            .GroupBy(x => x.Dealer!)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<decimal?>)Enumerable.Range(1, 12)
                    .Select(m => PlanSum(g.Where(x => x.Plan.Month == m).GroupBy(x => x.Plan.RegionId).Select(r => SalesMath.PlanTotal(r.Select(x => x.Plan)))))
                    .ToList());

    /// <summary>
    /// Отчётный день первички: вчера по местному времени (cutoff), но не позже дня синхронизации (syncedOn) — за день после неё данных ещё нет.
    /// </summary>
    public static DateOnly DataLimit(DateOnly cutoff, DateOnly? syncedOn) => syncedOn is { } s && s < cutoff ? s : cutoff;

    /// <summary>
    /// Дата данных первички: последний день со строкой не позже отчётного дня (DataLimit — вчера и не позже синхронизации): строки с датой
    /// в будущем её не сдвигают. null — таких дней нет.
    /// </summary>
    public static DateOnly? AsOf(IEnumerable<DateOnly> days, DateOnly cutoff, DateOnly? syncedOn)
    {
        var limit = DataLimit(cutoff, syncedOn);
        DateOnly? best = null;
        foreach (var day in days)
        {
            if (day <= limit && (best is null || day > best))
            {
                best = day;
            }
        }

        return best;
    }

    /// <summary>
    /// Строки отчёта — не позже отчётного дня limit (как salesEnd во вторичке): сегодняшние и будущие (плановая доставка) в факт, прогноз,
    /// календарь и «данные по» не входят — войдут, когда их день станет отчётным. After — сколько строк отрезано.
    /// </summary>
    public static (List<PrimaryShipment> Rows, int After) ReportRows(IEnumerable<PrimaryShipment> rows, DateOnly limit)
    {
        var kept = new List<PrimaryShipment>();
        var after = 0;
        foreach (var row in rows)
        {
            if (row.Date <= limit)
            {
                kept.Add(row);
            }
            else
            {
                after++;
            }
        }

        return (kept, after);
    }

    /// <summary>
    /// Доля прошедшего месяца по дате данных (как «Полевой контроль»): месяц раньше месяца asOf закрыт (1); месяц asOf — день asOf ÷ дней
    /// в месяце, больше 0,98 — закрыт (1); месяц позже или без даты данных — 0: данных за него ещё нет.
    /// </summary>
    public static decimal MonthShare(int year, int month, DateOnly? asOf)
    {
        if (asOf is not { } day)
        {
            return 0;
        }

        var start = new DateOnly(year, month, 1);
        var current = new DateOnly(day.Year, day.Month, 1);
        if (start != current)
        {
            return start < current ? 1 : 0;
        }

        var share = (decimal)day.Day / DateTime.DaysInMonth(year, month);
        return share > ClosedShare ? 1 : share;
    }

    /// <summary>Месяц идёт: это месяц даты данных и прошло не больше 98% его дней. Прогноз и пометка «идёт» — только у него.</summary>
    public static bool IsRunning(int year, int month, DateOnly? asOf) => MonthShare(year, month, asOf) is > 0 and < 1;

    /// <summary>Какой месяц года идёт; null — ни один.</summary>
    public static int? RunningMonth(int year, DateOnly? asOf) => asOf is { } day && day.Year == year && IsRunning(year, day.Month, asOf) ? day.Month : null;

    /// <summary>Отработано дней («темп по N-е число»): у идущего месяца — день даты данных, у месяца с отгрузками — все дни, иначе 0.</summary>
    public static int WorkedDays(int year, int month, DateOnly? asOf, bool hasData) =>
        IsRunning(year, month, asOf) ? asOf!.Value.Day : hasData ? DateTime.DaysInMonth(year, month) : 0;

    /// <summary>Прогноз на конец месяца = факт ÷ доля прошедшего месяца — только у идущего; у закрытого это факт (null).</summary>
    public static decimal? Forecast(decimal fact, int year, int month, DateOnly? asOf) =>
        IsRunning(year, month, asOf) ? SalesMath.Forecast(fact, asOf!.Value.Day, DateTime.DaysInMonth(year, month)) : null;

    public static PrimaryAmounts SumOf(IEnumerable<PrimaryAmounts> source, PrimaryAmounts zero) => source.Aggregate(zero, (a, s) => a.Add(s));

    /// <summary>Суммы по 12 месяцам года.</summary>
    public static IReadOnlyList<PrimaryAmounts> ByMonth(IEnumerable<PrimaryShipment> rows, PrimaryAmounts zero)
    {
        var byMonth = rows.ToLookup(r => r.Date.Month);
        return Enumerable.Range(1, 12).Select(m => SumOf(byMonth[m].Select(r => r.Amounts), zero)).ToList();
    }

    /// <summary>Доля в итоге по каждой единице; null — итог 0 или коробок нет.</summary>
    public static PrimaryShares Shares(PrimaryAmounts part, PrimaryAmounts total) => new(
        SalesMath.Ratio(part.Kg, total.Kg),
        part.Boxes is { } boxes && total.Boxes is { } all ? SalesMath.Ratio(boxes, all) : null,
        SalesMath.Ratio(part.SumFactory, total.SumFactory),
        SalesMath.Ratio(part.SumDealer, total.SumDealer));

    /// <summary>Сумма планов; null — ни у одного плана нет.</summary>
    public static decimal? PlanSum(IEnumerable<decimal?> plans)
    {
        var known = plans.Where(p => p is not null).ToList();
        return known.Count == 0 ? null : known.Sum();
    }

    /// <summary>Цена за кг по цене дилера: сумма ÷ кг.</summary>
    public static decimal? PricePerKg(PrimaryAmounts a) => SalesMath.Ratio(a.SumFactory, a.Kg);

    /// <summary>Наценка дилера: сумма по цене продажи дилера ÷ сумма по цене дилера − 1; null — одной из сумм нет.</summary>
    public static decimal? Markup(PrimaryAmounts a) => a.SumFactory != 0 && a.SumDealer != 0 ? a.SumDealer / a.SumFactory - 1 : null;

    /// <summary>
    /// Строка таблицы: месяц, 12 месяцев, с начала года (месяцы по выбранный), доли в итогах таблицы, план, выполнение = факт ÷ план,
    /// «осталось» = план − факт (не меньше 0) — за месяц и с начала года.
    /// </summary>
    public static PrimaryRow Row(
        string id,
        string name,
        string? sub,
        string? group,
        IReadOnlyList<PrimaryAmounts> months,
        IReadOnlyList<decimal?> planMonths,
        int month,
        PrimaryAmounts zero,
        PrimaryAmounts monthTotal,
        PrimaryAmounts ytdTotal)
    {
        var current = months[month - 1];
        var plan = planMonths[month - 1];
        var ytd = SumOf(months.Take(month), zero);
        var planYtd = PlanSum(planMonths.Take(month));
        return new PrimaryRow(
            id,
            name,
            sub,
            current,
            months,
            plan,
            planMonths,
            group,
            Shares(current, monthTotal),
            SalesMath.Ratio(current.Kg, plan),
            plan is { } p ? SalesMath.Remaining(p, current.Kg) : null,
            ytd,
            Shares(ytd, ytdTotal),
            planYtd,
            SalesMath.Ratio(ytd.Kg, planYtd));
    }

    /// <summary>Итог строк таблицы (или группы) — той же формы, что строка: суммы по месяцам и планов.</summary>
    public static PrimaryRow Total(string id, string name, string? group, IReadOnlyCollection<PrimaryRow> rows, int month, PrimaryAmounts zero, PrimaryAmounts monthTotal, PrimaryAmounts ytdTotal) =>
        Row(id, name, null, group,
            Enumerable.Range(0, 12).Select(i => SumOf(rows.Select(r => r.Months[i]), zero)).ToList(),
            Enumerable.Range(0, 12).Select(i => PlanSum(rows.Select(r => r.PlanMonths[i]))).ToList(),
            month, zero, monthTotal, ytdTotal);

    /// <summary>
    /// Строки контрагентов: у кого есть строки первички в году или план (строка с планом остаётся и без факта, DOC-filters §3). Сначала дилеры,
    /// потом прямые клиенты; внутри — по убыванию веса за год, затем плана месяца.
    /// </summary>
    public static List<PrimaryRow> PartyRows(
        IReadOnlyList<PrimaryShipment> yearRows,
        IReadOnlyDictionary<string, PrimaryParty> parties,
        int month,
        PrimaryAmounts zero,
        PrimaryAmounts monthTotal,
        PrimaryAmounts ytdTotal)
    {
        var byParty = yearRows.ToLookup(r => r.Counterparty);
        return byParty.Select(g => g.Key)
            .Concat(parties.Values.Where(p => p.PlanMonths.Any(v => v is not null)).Select(p => p.Id))
            .Distinct()
            .Select(id =>
            {
                var party = parties.GetValueOrDefault(id) ?? new PrimaryParty(id, id, null, null, NoPlan);
                return Row(party.Id, party.Name, party.Sub, party.Group, ByMonth(byParty[id], zero), party.PlanMonths, month, zero, monthTotal, ytdTotal);
            })
            .OrderBy(r => r.Group == PrimaryGroups.Direct ? 1 : 0)
            .ThenByDescending(r => r.Months.Sum(m => m.Kg))
            .ThenByDescending(r => r.PlanMonthKg ?? 0)
            .ToList();
    }

    /// <summary>Итоги групп контрагентов (дилеры, прямые клиенты) — по строке на группу, в том же порядке.</summary>
    public static List<PrimaryRow> GroupTotals(IReadOnlyList<PrimaryRow> rows, int month, PrimaryAmounts zero, PrimaryAmounts monthTotal, PrimaryAmounts ytdTotal) =>
        rows.Where(r => r.Group is not null)
            .GroupBy(r => r.Group!)
            .OrderBy(g => g.Key == PrimaryGroups.Direct ? 1 : 0)
            .Select(g => Total(g.Key, PrimaryGroups.NameOf(g.Key), g.Key, g.ToList(), month, zero, monthTotal, ytdTotal))
            .ToList();

    /// <summary>
    /// «Клиенты по месяцам» (DOC §9.3) за месяцы с 1-го по выбранный: АКБ — контрагентов с нетто кг за месяц больше нуля, у категории — с нетто
    /// кг категории больше нуля; кг — нетто; сумма — нетто по цене дилера. Месяц без строк — null. Категории без отгрузок за эти месяцы
    /// не показываются; порядок — по весу. categoryOf — товар → категория отчёта (ключ и название, как в таблице категорий).
    /// Среднее за месяц (Average) — по закрытым месяцам с данными: идущий месяц (lastPartial — последний) не в счёт, неполный месяц занижал бы его.
    /// </summary>
    public static PrimaryClients Clients(int year, int month, bool lastPartial, IReadOnlyList<PrimaryShipment> yearRows, Func<long?, (string Id, string Name)> categoryOf)
    {
        var months = Enumerable.Range(1, month).ToList();
        var closed = lastPartial ? months.Count - 1 : months.Count;
        var rows = yearRows.Where(r => r.Date.Month <= month).Select(r => (Row: r, Category: categoryOf(r.ProductId))).ToList();
        var byMonth = rows.ToLookup(x => x.Row.Date.Month);
        var categories = rows.GroupBy(x => x.Category.Id)
            .Where(g => g.GroupBy(x => x.Row.Date.Month).Any(m => m.Sum(x => x.Row.Amounts.Kg) > 0))
            .OrderByDescending(g => g.Sum(x => x.Row.Amounts.Kg))
            .Select(g => g.First().Category)
            .ToList();

        static decimal Active(IEnumerable<PrimaryShipment> source) => source.GroupBy(r => r.Counterparty).Count(g => g.Sum(r => r.Amounts.Kg) > 0);

        PrimaryClientsMetric Metric(Func<IEnumerable<PrimaryShipment>, decimal> value)
        {
            decimal? Of(int m, string? category)
            {
                var list = byMonth[m].ToList();
                return list.Count == 0 ? null : value(list.Where(x => category is null || x.Category.Id == category).Select(x => x.Row));
            }

            var total = months.Select(m => Of(m, null)).ToList();
            return new PrimaryClientsMetric(
                year,
                months,
                lastPartial,
                total,
                categories.Select(c =>
                {
                    var values = months.Select(m => Of(m, c.Id)).ToList();
                    return new PrimaryClientSeries(c.Id, c.Name, values, SalesMath.Average(values.Take(closed)));
                }).ToList(),
                SalesMath.Average(total.Take(closed)));
        }

        return new PrimaryClients(Metric(Active), Metric(r => r.Sum(x => x.Amounts.Kg)), Metric(r => r.Sum(x => x.Amounts.SumFactory)));
    }

    /// <summary>
    /// Календарь отгрузок месяца (DOC-filters §3): контрагент × день за период «с … по …» — по умолчанию все дни с отгрузкой, перепутанные границы
    /// меняются местами, дни вне месяца не берутся. Колонки — дни периода с отгрузкой, строки — только контрагенты с отгрузкой в периоде (по
    /// убыванию веса), итоги строк, дней и периода, «Дней» строки — дни с отгрузкой. Разбор — при выбранном дне и/или контрагенте: товары дня
    /// (без дня — периода), только этого контрагента; без контрагента — по каждому, кому отгружали.
    /// </summary>
    public static PrimaryCalendar Calendar(
        IReadOnlyList<PrimaryShipment> monthRows,
        int daysInMonth,
        PrimaryCalendarQuery? query,
        IReadOnlyDictionary<string, PrimaryParty> parties,
        Func<long?, PrimaryProduct> product,
        PrimaryAmounts zero)
    {
        int? Valid(int? day) => day is { } d && d >= 1 && d <= daysInMonth ? d : null;
        var monthDays = monthRows.Select(r => r.Date.Day).Distinct().Order().ToList();
        if (monthDays.Count == 0)
        {
            return new PrimaryCalendar([], null, null, [], [], [], zero, null, null, null, null);
        }

        var from = Valid(query?.From) ?? monthDays[0];
        var to = Valid(query?.To) ?? monthDays[^1];
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var inRange = monthRows.Where(r => r.Date.Day >= from && r.Date.Day <= to).ToList();
        var days = monthDays.Where(d => d >= from && d <= to).ToList();
        var rows = inRange
            .GroupBy(r => r.Counterparty)
            .Select(g =>
            {
                var byDay = g.ToLookup(r => r.Date.Day);
                var cells = days.Select(d => byDay[d].Any() ? SumOf(byDay[d].Select(r => r.Amounts), zero) : null).ToList();
                var party = parties.GetValueOrDefault(g.Key);
                return new PrimaryCalendarRow(g.Key, party?.Name ?? g.Key, party?.Sub, cells, SumOf(g.Select(r => r.Amounts), zero), cells.Count(c => c is not null));
            })
            .OrderByDescending(r => r.Total.Kg)
            .ToList();
        var byDayAll = inRange.ToLookup(r => r.Date.Day);

        var day = Valid(query?.Day);
        var dealer = query?.Dealer is { Length: > 0 } picked && monthRows.Any(r => r.Counterparty == picked) ? picked : null;
        List<PrimaryCalendarItem>? detail = null;
        PrimaryAmounts? detailTotal = null;
        if (day is not null || dealer is not null)
        {
            var lines = monthRows
                .Where(r => (day is { } d ? r.Date.Day == d : r.Date.Day >= from && r.Date.Day <= to) && (dealer is null || r.Counterparty == dealer))
                .ToList();
            detail = lines
                .GroupBy(r => (Party: dealer is null ? r.Counterparty : null, r.ProductId, r.IsReturn))
                .Select(g =>
                {
                    var item = product(g.Key.ProductId);
                    var party = g.Key.Party is { } id ? parties.GetValueOrDefault(id)?.Name ?? id : null;
                    return new PrimaryCalendarItem(g.Key.Party, party, g.Key.ProductId, item.Name, item.Code, item.Category, g.Key.IsReturn, SumOf(g.Select(r => r.Amounts), zero));
                })
                .OrderByDescending(i => Math.Abs(i.Amounts.Kg))
                .ToList();
            detailTotal = SumOf(lines.Select(r => r.Amounts), zero);
        }

        return new PrimaryCalendar(
            monthDays,
            from,
            to,
            days,
            rows,
            days.Select(d => SumOf(byDayAll[d].Select(r => r.Amounts), zero)).ToList(),
            SumOf(inRange.Select(r => r.Amounts), zero),
            day,
            dealer,
            detail,
            detailTotal);
    }
}

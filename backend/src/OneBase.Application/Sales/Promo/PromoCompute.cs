using System.Globalization;
using System.Text.RegularExpressions;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales.Promo;

/// <summary>
/// Расчёт вкладки «Акции» по формулам «Полевого контроля» (main.js, renderPromo): плитки, «Внимание», выводы, «По акциям», эффект, регионы
/// с раскрытием, ручные скидки, календарь, агенты, магазины, «кто ждёт акцию», товары, по месяцам. Фильтры: РМ и отмеченные акции.
/// </summary>
public static partial class PromoCompute
{
    public const decimal ManualLimit = 0.01m;
    private const decimal ManualSanity = 0.15m;
    private const int HistoryMonths = 6;
    private static readonly StringComparer Russian = StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), true);

    [GeneratedRegex(@"^\d+\s+")]
    private static partial Regex Code();

    /// <summary>Имя агента без кода Linko в начале: «371 Санжар Сатторов» → «Санжар Сатторов».</summary>
    public static string WithoutCode(string name) => Code().Replace(name.Trim(), "") is { Length: > 0 } x ? x : name.Trim();

    public static PromoView Build(PromoBase data, PromoMonth month, string? rm, IReadOnlyCollection<long> selected, PromoSalesBasis basis)
    {
        var mk = month.Key;
        var monthIndex = data.Months.ToList().FindIndex(m => m.Key == mk);
        var sDay = Math.Min(basis.DataDay, month.Days);
        // «Кг (сум) в день» идущего месяца — по прошедшим дням: неполный сегодняшний день не считается целым (1-го числа — 0, роста ещё нет).
        var paceDay = basis.ElapsedDays is { } elapsed && elapsed < sDay ? elapsed : sDay;
        var pk = selected.ToHashSet();
        bool RegOk(string region) => rm is null || (data.RmOfRegion.TryGetValue(region, out var r) && r == rm);
        bool PkOk(long promo) => pk.Count == 0 || pk.Contains(promo);
        string Agent(long? id) => id is { } a && data.Agents.TryGetValue(a, out var n) ? n : id is { } x ? $"Агент {x}" : "—";
        string Market(long? id) => id is { } m && data.Markets.TryGetValue(m, out var n) ? n : id is { } x ? $"Точка {x}" : "—";
        PromoGroup? Promo(long id) => data.Promos.GetValueOrDefault(id);
        string PromoName(long id) => id == PromoMatch.Manual ? "Без акции (ручная скидка)" : Promo(id)?.Name ?? $"Акция {id}";

        var noPk = month.Rows.Where(r => RegOk(r.Region)).ToList();
        var rows = noPk.Where(r => PkOk(r.Promo)).ToList();

        bool Out(PromoRow r)
        {
            if (Promo(r.Promo) is not { Till: { } till } p)
            {
                return false;
            }

            var day = new DateOnly(month.Year, month.Month, r.Day);
            return (p.Begin is { } b && day < b) || day > till.AddDays(PromoMatch.Grace);
        }

        // ---------- Плитки ----------
        var total = rows.Sum(r => r.Discount);
        var manual = rows.Where(r => r.Promo == PromoMatch.Manual).Sum(r => r.Discount);
        var outSum = rows.Where(Out).Sum(r => r.Discount);
        var pending = month.Pending.Where(p => RegOk(p.Region) && PkOk(p.Promo)).Sum(p => p.Discount);
        var sumTo = rows.Where(r => r.Day <= sDay).Sum(r => r.Discount);
        // Продажи регионов месяца — вторичка OneBase с регионами-каналами (как снимок «Полевого контроля»); без неё — агрегат по SKU.
        var monthSales = basis.RegionRevenue.Count > 0 ? basis.RegionRevenue : data.RegionSales.GetValueOrDefault(mk);
        var sales = monthSales?.Where(x => RegOk(x.Key)).Sum(x => x.Value);
        var tiles = new PromoTiles(total, total - manual, manual, rows.Where(r => r.Promo != PromoMatch.Manual).Select(r => r.Promo).Distinct().Count(), outSum, pending,
            sumTo, sales, sales is > 0 ? sumTo / sales : null, sDay, month.Days);

        // ---------- По акциям ----------
        var promoTotal = noPk.Sum(r => r.Discount);
        var byPromo = noPk.GroupBy(r => r.Promo)
            .Select(g =>
            {
                var p = Promo(g.Key);
                return new PromoByPromo(g.Key, PromoName(g.Key), g.Key == PromoMatch.Manual ? "none" : p?.Kind ?? "discount", p?.Begin, p?.Till,
                    g.Select(r => r.Region).Distinct().Count(), g.Select(r => r.Market).Distinct().Count(), g.Sum(r => r.Qty), g.Sum(r => r.Discount),
                    promoTotal > 0 ? g.Sum(r => r.Discount) / promoTotal : 0, g.Where(Out).Sum(r => r.Discount), pk.Contains(g.Key));
            })
            .OrderByDescending(x => x.Sum)
            .ToList();
        PromoTotals? selectedTotals = null;
        if (pk.Count > 0)
        {
            var sel = noPk.Where(r => pk.Contains(r.Promo)).ToList();
            selectedTotals = new PromoTotals(sel.Select(r => r.Region).Distinct().Count(), sel.Select(r => r.Market).Distinct().Count(), sel.Sum(r => r.Qty),
                sel.Sum(r => r.Discount), promoTotal > 0 ? sel.Sum(r => r.Discount) / promoTotal : 0, sel.Where(Out).Sum(r => r.Discount));
        }

        // ---------- Эффект акций ----------
        var (effDay, effects) = Effects(data, month, noPk, byPromo, RegOk, PkOk, basis.DataDay, paceDay);

        // ---------- Регионы ----------
        var prev = monthIndex > 0 ? data.Months[monthIndex - 1] : null;
        var prevSales = prev is null ? null : data.RegionSales.GetValueOrDefault(prev.Key);
        var promoRows = rows.Where(r => r.Promo != PromoMatch.Manual && r.Day <= sDay).ToList();
        var regionIdByName = data.RegionNames.ToDictionary(x => x.Value, x => x.Key);
        var regionStats = rows.GroupBy(r => r.Region)
            .Select(g =>
            {
                var sl = monthSales?.GetValueOrDefault(g.Key) is { } x and not 0 ? x : (decimal?)null;
                var pv = prevSales?.GetValueOrDefault(g.Key) is { } y and not 0 ? y : (decimal?)null;
                decimal? growth = sl is { } a && pv is { } b && prev is not null && paceDay > 0 ? (a / paceDay) / (b / prev.Days) - 1 : null;
                return (Region: g.Key, S: g.Sum(r => r.Discount), STo: g.Where(r => r.Day <= sDay).Sum(r => r.Discount),
                    Man: g.Where(r => r.Promo == PromoMatch.Manual).Sum(r => r.Discount), Sl: sl, Growth: growth,
                    P: sl is { } s ? g.Where(r => r.Day <= sDay).Sum(r => r.Discount) / s : (decimal?)null);
            })
            .OrderByDescending(x => x.S)
            .ToList();

        var regions = regionStats
            .Select(x =>
            {
                var list = promoRows.Where(r => r.Region == x.Region).ToList();
                var ps = list.Sum(r => r.Sale);
                var cost = list.Sum(r => r.Discount);
                var regionId = regionIdByName.GetValueOrDefault(x.Region);
                return new PromoRegionRow(x.Region, data.RmOfRegion.GetValueOrDefault(x.Region), x.Sl, x.Growth, ps, x.Sl is > 0 ? ps / x.Sl : null,
                    x.Sl is { } sl ? Math.Max(0, sl - ps) : null, basis.MogulByRegion.GetValueOrDefault(regionId), list.Select(r => r.Market).Distinct().Count(),
                    cost, x.Sl is > 0 ? cost / x.Sl : null, AgentsOf(x.Region, regionId, list));
            })
            .OrderByDescending(r => r.PromoSale)
            .ThenByDescending(r => r.Sales ?? 0)
            .ToList();
        var regionTotals = new PromoRegionTotals(regions.Sum(r => r.Sales ?? 0), promoRows.Sum(r => r.Sale),
            regions.Sum(r => r.Sales ?? 0) is > 0 and var all ? promoRows.Sum(r => r.Sale) / all : null,
            regions.Sum(r => r.Mogul), promoRows.Select(r => r.Market).Distinct().Count(), promoRows.Sum(r => r.Discount),
            regions.Sum(r => r.Sales ?? 0) is > 0 and var all2 ? promoRows.Sum(r => r.Discount) / all2 : null);

        IReadOnlyList<PromoAgentRow> AgentsOf(string region, Guid regionId, IReadOnlyList<PromoRow> list)
        {
            var withRows = list.GroupBy(r => r.Agent).ToDictionary(g => g.Key, g => g.ToList());
            var ids = withRows.Keys
                .Concat(basis.AgentRegion.Where(a => a.Value == regionId && basis.AgentRevenue.GetValueOrDefault(a.Key) > 0).Select(a => (long?)a.Key))
                .Distinct();
            return ids
                .Select(id =>
                {
                    var own = withRows.GetValueOrDefault(id) ?? [];
                    decimal? sl = id is { } a && basis.AgentRevenue.TryGetValue(a, out var v) && v != 0 ? v : null;
                    var ps = own.Sum(r => r.Sale);
                    var cost = own.Sum(r => r.Discount);
                    var promos = own.GroupBy(r => r.Promo)
                        .Select(g =>
                        {
                            var per = g.GroupBy(r => r.Market).ToDictionary(x => x.Key, x => x.Sum(r => r.Sale));
                            var c = g.Sum(r => r.Discount);
                            var shops = g.GroupBy(r => r.Market)
                                .Select(s =>
                                {
                                    var sale = s.Sum(r => r.Sale);
                                    var disc = s.Sum(r => r.Discount);
                                    decimal? purchase = s.Key is { } m && data.MarketPurchases.TryGetValue(m, out var byMonth) && byMonth.TryGetValue(mk, out var p) ? Math.Round(p / 1000) * 1000 : null;
                                    return new PromoShopCell(s.Key, Market(s.Key), purchase, sale, purchase is > 0 ? sale / purchase : null, disc, sale + disc > 0 ? disc / (sale + disc) : null);
                                })
                                .OrderByDescending(s => s.PromoSale)
                                .ToList();
                            return new PromoAgentPromo(g.Key, PromoName(g.Key), per.Count, per.Count > 0 ? per.Values.Sum() / per.Count : 0, per.Count > 0 ? per.Values.Max() : 0,
                                per.Values.Sum(), c, per.Values.Sum() + c > 0 ? c / (per.Values.Sum() + c) : null, shops);
                        })
                        .OrderByDescending(p => p.PromoSale)
                        .ToList();
                    return new PromoAgentRow(id, id is null ? "—" : Agent(id), sl, ps, sl is > 0 ? ps / sl : null, sl is { } s ? Math.Max(0, s - ps) : null,
                        id is { } ag ? basis.MogulByAgent.GetValueOrDefault(ag) : 0, own.Select(r => r.Market).Distinct().Count(), cost, sl is > 0 ? cost / sl : null, promos);
                })
                .Where(a => a.Sales is not null || a.Promos.Count > 0)
                .OrderByDescending(a => a.PromoSale)
                .ThenByDescending(a => a.Sales ?? 0)
                .ToList();
        }

        // ---------- Ручные скидки по агентам и «Внимание» ----------
        var histMonths = data.Months.Take(monthIndex + 1).TakeLast(HistoryMonths).ToList();
        var manualHist = histMonths.ToDictionary(
            m => m.Key,
            m => m.Rows.Where(r => r.Promo == PromoMatch.Manual && RegOk(r.Region)).GroupBy(r => (r.Agent, r.Region)).ToDictionary(g => g.Key, g => g.Sum(r => r.Discount)));
        var manualRows = noPk.Where(r => r.Promo == PromoMatch.Manual)
            .GroupBy(r => (r.Agent, r.Region))
            .Select(g =>
            {
                var sTo = g.Where(r => r.Day <= sDay).Sum(r => r.Discount);
                decimal? sl = g.Key.Agent is { } a && basis.AgentRevenue.TryGetValue(a, out var v) && v != 0 ? v : null;
                decimal? p = sl is { } s && sTo / s < ManualSanity ? sTo / s : null; // от 15% — чужой ID, не показываем
                var history = histMonths.Select(m => manualHist[m.Key].GetValueOrDefault(g.Key)).ToList();
                return new PromoManualRow(Agent(g.Key.Agent), g.Key.Region, g.Sum(r => r.Discount), sl, p, g.Select(r => r.Market).Distinct().Count(), history,
                    history.Count(x => x > 0));
            })
            .OrderByDescending(r => r.Sum)
            .ToList();
        var attention = manualRows.Where(r => r.Pct is { } p && p >= ManualLimit)
            .OrderByDescending(r => r.Pct)
            .Select(r => new PromoAttention(WithoutCode(r.Agent), r.Region, r.Sum, r.Pct!.Value, r.MonthsWith, histMonths.Count))
            .ToList();

        // ---------- Календарь ----------
        var calendar = Calendar(month, rows, data, Agent, PromoName);

        // ---------- Агенты, магазины, товары ----------
        var agentsTop = rows.GroupBy(r => (r.Agent, r.Region))
            .Select(g => new PromoAgentTop(Agent(g.Key.Agent), g.Key.Region, g.Sum(r => r.Discount), g.Where(r => r.Promo == PromoMatch.Manual).Sum(r => r.Discount),
                g.Select(r => r.Market).Distinct().Count(), total > 0 ? g.Sum(r => r.Discount) / total : 0))
            .OrderByDescending(a => a.Sum)
            .ToList();
        var shopsTop = rows.GroupBy(r => r.Market)
            .Select(g => new PromoShopTop(g.Key, Market(g.Key), g.First().Region,
                g.GroupBy(r => r.Agent).OrderByDescending(x => x.Sum(r => r.Discount)).Select(x => Agent(x.Key)).FirstOrDefault(),
                g.Sum(r => r.Discount),
                g.GroupBy(r => r.Promo).OrderByDescending(x => x.Sum(r => r.Discount)).Take(2).Select(x => PromoName(x.Key)).ToList()))
            .OrderByDescending(s => s.Sum)
            .ToList();
        var skuRows = month.Sku.Where(s => RegOk(s.Region) && PkOk(s.Promo)).ToList();
        var skuTotal = skuRows.Sum(s => s.Discount);
        var products = skuRows.GroupBy(s => s.ProductId)
            .Select(g => new PromoProductTop(data.Products.GetValueOrDefault(g.Key) ?? $"Товар {g.Key}", g.Sum(s => s.Qty), g.Sum(s => s.Discount),
                skuTotal > 0 ? g.Sum(s => s.Discount) / skuTotal : 0))
            .OrderByDescending(p => p.Sum)
            .ToList();

        // ---------- Кто ждёт акцию ----------
        var waiting = histMonths.Count >= 3 ? Waiting(data, histMonths, RegOk, Agent, Market) : new WaitingResult([], 0, 0, 0);

        // ---------- По месяцам ----------
        var byMonth = data.Months.Take(monthIndex + 1)
            .Select(m =>
            {
                var list = m.Rows.Where(r => RegOk(r.Region) && PkOk(r.Promo)).ToList();
                var day = m.Key == mk ? sDay : m.Days;
                var sl = (m.Key == mk ? monthSales : data.RegionSales.GetValueOrDefault(m.Key))?.Where(x => RegOk(x.Key)).Sum(x => x.Value);
                return new PromoByMonth(m.Key, m.Year, m.Month, sl, list.Sum(r => r.Discount), sl is > 0 ? list.Where(r => r.Day <= day).Sum(r => r.Discount) / sl : null,
                    list.Where(r => r.Promo == PromoMatch.Manual).Sum(r => r.Discount), m.Key == mk);
            })
            .ToList();

        // ---------- Выводы ----------
        var conclusions = Conclusions(tiles, byPromo, regionStats.Select(x => (x.Region, x.Man, x.Growth, x.P)).ToList(), rows, effects, manualRows, histMonths.Count,
            waiting.Shops, waiting.Only, waiting.Wait, waiting.OnlyTotal, PromoName, Out);

        var options = data.Months.Select(m => new PromoMonthOption(m.Key, m.Year, m.Month, m.Days, m.Days < DateTime.DaysInMonth(m.Year, m.Month))).ToList();
        var rmOptions = data.RmOrder.Where(r => month.Rows.Any(x => data.RmOfRegion.GetValueOrDefault(x.Region) == r)).ToList();
        return new PromoView(
            data.AsOf,
            options,
            options.Single(o => o.Key == mk),
            rmOptions,
            rm,
            pk.Order().ToList(),
            tiles,
            attention,
            conclusions,
            byPromo,
            selectedTotals,
            promoTotal,
            effDay,
            effects,
            sDay,
            regions,
            regionTotals,
            histMonths.Select(m => m.Key).ToList(),
            manualRows.Take(60).ToList(),
            calendar,
            agentsTop.Take(100).ToList(),
            agentsTop.Count,
            shopsTop.Take(100).ToList(),
            shopsTop.Count,
            histMonths.Select(m => m.Key).ToList(),
            waiting.Shops,
            waiting.Only,
            waiting.Wait,
            products.Take(100).ToList(),
            products.Count,
            byMonth);
    }

    /// <summary>
    /// «Эффект акций»: кг/день товаров акции в её регионах против среднего за 3 месяца до старта, с поправкой на тренд остальных товаров тех же
    /// регионов (если акция накрыла почти весь ассортимент — те же товары в регионах без акции). Акции, начавшиеся раньше прошлого месяца, не
    /// оцениваются: «до акции» в данных нет.
    /// </summary>
    private static (int Day, IReadOnlyList<PromoEffect> Effects) Effects(PromoBase data, PromoMonth month, IReadOnlyList<PromoRow> noPk, IReadOnlyList<PromoByPromo> byPromo,
        Func<string, bool> regOk, Func<long, bool> pkOk, int dataDay, decimal paceDay)
    {
        var year = month.Year;
        var mi = month.Month - 1;
        var effDay = Math.Min(dataDay, month.Days);
        // Дни месяцев для «кг в день»; у месяца акции — прошедшие (paceDay: с долей неполного сегодняшнего).
        var days = Enumerable.Range(1, 12).Select(m => m < month.Month ? DateTime.DaysInMonth(year, m) : m == month.Month ? Math.Min(paceDay, effDay) : 0m).ToArray();
        var regionIds = data.RegionNames.ToDictionary(x => x.Value, x => x.Key);

        // кг и выручка: (товар, регион, месяц) и итоги (регион, месяц).
        var cells = new Dictionary<(long, Guid, int), (decimal Kg, decimal Rev)>();
        var totals = new Dictionary<(Guid, int), decimal>();
        foreach (var x in data.Sku.Months.Where(x => x.Year == year && x.RegionId is not null))
        {
            var key = (x.ProductId, x.RegionId!.Value, x.Month - 1);
            var c = cells.GetValueOrDefault(key);
            cells[key] = (c.Kg + x.Kg, c.Rev + x.Revenue);
            totals[(x.RegionId.Value, x.Month - 1)] = totals.GetValueOrDefault((x.RegionId.Value, x.Month - 1)) + x.Kg;
        }

        var allRegions = data.RegionNames.Keys.ToList();
        decimal Kg(IReadOnlySet<long> skus, IEnumerable<Guid> regions, int i, bool invert = false) =>
            regions.Sum(r => invert
                ? totals.GetValueOrDefault((r, i)) - skus.Sum(s => cells.GetValueOrDefault((s, r, i)).Kg)
                : skus.Sum(s => cells.GetValueOrDefault((s, r, i)).Kg));
        decimal Rev(IReadOnlySet<long> skus, IEnumerable<Guid> regions, int i) => regions.Sum(r => skus.Sum(s => cells.GetValueOrDefault((s, r, i)).Rev));

        var result = new List<PromoEffect>();
        foreach (var x in byPromo.Where(p => p.Id != PromoMatch.Manual && pkOk(p.Id)))
        {
            var skus = month.Sku.Where(s => regOk(s.Region) && s.Promo == x.Id).Select(s => s.ProductId).ToHashSet();
            var regs = noPk.Where(r => r.Promo == x.Id).Select(r => r.Region).Distinct().Where(regionIds.ContainsKey).Select(r => regionIds[r]).ToList();
            var disc = noPk.Where(r => r.Promo == x.Id && r.Day <= effDay).Sum(r => r.Discount);
            var st = mi;
            if (x.Begin is { } b)
            {
                st = b.Year < year ? -99 : Math.Min(mi, b.Month - 1);
            }

            if (st < mi - 1)
            {
                result.Add(new PromoEffect(x.Id, x.Name, "long", regs.Count, skus.Count, null, null, null, null, null, disc, null));
                continue;
            }

            var baseMonths = new[] { st - 3, st - 2, st - 1 }.Where(i => i >= 0).ToList();
            if (baseMonths.Count == 0)
            {
                result.Add(new PromoEffect(x.Id, x.Name, "nobase", regs.Count, skus.Count, null, null, null, null, null, disc, null));
                continue;
            }

            if (regs.Count == 0 || skus.Count == 0 || days[mi] == 0)
            {
                continue;
            }

            var bd = baseMonths.Sum(i => days[i]);
            decimal BaseDay(IReadOnlySet<long> s, IEnumerable<Guid> r, bool invert = false) => bd > 0 ? baseMonths.Sum(i => Kg(s, r, i, invert)) / bd : 0;
            var bs = BaseDay(skus, regs);
            var kgM = Kg(skus, regs, mi);
            var cur = kgM / days[mi];

            // Контроль: остальные товары тех же регионов; если их база мала (акция почти на всё) — те же товары в других регионах.
            var others = allRegions.Except(regs).ToList();
            var cb = BaseDay(skus, regs, invert: true);
            var controlKg = Kg(skus, regs, mi, invert: true);
            if (cb < 0.33m * bs)
            {
                cb = BaseDay(skus, others);
                controlKg = Kg(skus, others, mi);
            }

            var g2 = cb > 0 ? controlKg / days[mi] / cb - 1 : 0;
            decimal? g = bs > 0 ? cur / bs - 1 : null;
            var extra = (cur - bs * (1 + g2)) * days[mi];
            var price = kgM > 0 ? Rev(skus, regs, mi) / kgM : 0;
            result.Add(new PromoEffect(x.Id, x.Name, "ok", regs.Count, skus.Count, bs, cur, g, g2, extra, disc, disc > 0 ? extra * price / disc : null));
        }

        return (effDay, result.OrderByDescending(e => e.Discount).Take(40).ToList());
    }

    private static PromoCalendar Calendar(PromoMonth month, IReadOnlyList<PromoRow> rows, PromoBase data, Func<long?, string> agent, Func<long, string> promoName)
    {
        var days = month.Days;
        PromoCalendarRow Row(string key, string name, string? sub, IReadOnlyList<PromoRow> list, IReadOnlyList<PromoCalendarRow> children)
        {
            var sale = new decimal[days];
            var discount = new decimal[days];
            var qty = new decimal[days];
            var akb = new int[days];
            foreach (var g in list.GroupBy(r => r.Day).Where(g => g.Key >= 1 && g.Key <= days))
            {
                sale[g.Key - 1] = g.Sum(r => r.Sale);
                discount[g.Key - 1] = g.Sum(r => r.Discount);
                qty[g.Key - 1] = g.Sum(r => r.Qty);
                akb[g.Key - 1] = g.Select(r => r.Market).Distinct().Count();
            }

            return new PromoCalendarRow(key, name, sub, sale, discount, qty, akb, list.Select(r => r.Market).Distinct().Count(), children);
        }

        var byRegion = rows.GroupBy(r => r.Region)
            .Select(g =>
            {
                var agents = g.GroupBy(r => r.Agent)
                    .Select(a => Row($"{g.Key}|{a.Key}", WithoutCode(agent(a.Key)), a.Key is { } id ? $"ID {id}" : null, a.ToList(), []))
                    .OrderByDescending(a => a.Discount.Sum())
                    .ToList();
                var rm = data.RmOfRegion.GetValueOrDefault(g.Key);
                return Row(g.Key, g.Key, rm is null ? $"{agents.Count} ТП" : $"{rm} · {agents.Count} ТП", g.ToList(), agents);
            })
            .OrderByDescending(r => r.Discount.Sum())
            .ToList();
        var byPromo = rows.GroupBy(r => r.Promo)
            .Select(g => Row(g.Key.ToString(CultureInfo.InvariantCulture), promoName(g.Key),
                g.Key == PromoMatch.Manual ? "none" : data.Promos.GetValueOrDefault(g.Key)?.Kind ?? "discount", g.ToList(), []))
            .OrderByDescending(r => r.Discount.Sum())
            .ToList();
        var sundays = Enumerable.Range(1, days).Where(d => new DateOnly(month.Year, month.Month, d).DayOfWeek == DayOfWeek.Sunday).ToList();
        return new PromoCalendar(days, sundays, byRegion, byPromo, Row("total", "Итого", null, rows, []));
    }

    private sealed record WaitingResult(IReadOnlyList<PromoWaitingShop> Shops, int Only, int Wait, decimal OnlyTotal);

    /// <summary>
    /// «Кто ждёт акцию» за до 6 месяцев: «всегда со скидкой» — в ≥80% месяцев с закупкой была скидка (от 3 таких месяцев); «под акцию» — ≥2 мес.
    /// без скидки, а со скидкой берёт в 2+ раза больше. По 60 крупнейших каждого признака.
    /// </summary>
    private static WaitingResult Waiting(PromoBase data, IReadOnlyList<PromoMonth> months, Func<string, bool> regOk, Func<long?, string> agent, Func<long?, string> market)
    {
        var discounts = new Dictionary<long, (decimal[] D, Dictionary<long?, decimal> Agents, string Region)>();
        for (var k = 0; k < months.Count; k++)
        {
            foreach (var r in months[k].Rows.Where(r => r.Market is not null && regOk(r.Region)))
            {
                if (!discounts.TryGetValue(r.Market!.Value, out var e))
                {
                    e = (new decimal[months.Count], new Dictionary<long?, decimal>(), r.Region);
                    discounts[r.Market.Value] = e;
                }

                e.D[k] += r.Discount;
                e.Agents[r.Agent] = e.Agents.GetValueOrDefault(r.Agent) + r.Discount;
            }
        }

        var found = new List<(PromoWaitingShop Shop, decimal Total)>();
        foreach (var (id, e) in discounts)
        {
            var s = months.Select(m => data.MarketPurchases.TryGetValue(id, out var byMonth) && byMonth.TryGetValue(m.Key, out var v) ? Math.Round(v / 1000) * 1000 : 0).ToArray();
            var buy = Enumerable.Range(0, months.Count).Where(k => s[k] > 0).ToList();
            var dm = buy.Where(k => e.D[k] > 0).ToList();
            var nd = buy.Where(k => e.D[k] <= 0).ToList();
            if (buy.Count == 0 || dm.Count == 0)
            {
                continue;
            }

            var aD = dm.Average(k => s[k]);
            var aN = nd.Count > 0 ? nd.Average(k => s[k]) : 0;
            string? kind = buy.Count >= 3 && (decimal)dm.Count / buy.Count >= 0.8m ? "only"
                : buy.Count >= 3 && nd.Count >= 2 && aN > 0 && aD >= 2 * aN ? "wait"
                : null;
            if (kind is null)
            {
                continue;
            }

            var tot = buy.Sum(k => s[k]);
            found.Add((new PromoWaitingShop(id, market(id), e.Region, agent(e.Agents.MaxBy(a => a.Value).Key), kind, dm.Count, buy.Count, aD, aN,
                tot > 0 ? e.D.Sum() / tot : null, s.Select(v => v > 0 ? v : (decimal?)null).ToList(), Enumerable.Range(0, months.Count).Select(k => s[k] > 0 && e.D[k] > 0).ToList()), tot));
        }

        var only = found.Where(f => f.Shop.Kind == "only").OrderByDescending(f => f.Total).ToList();
        var wait = found.Where(f => f.Shop.Kind == "wait").OrderByDescending(f => f.Total).ToList();
        return new WaitingResult(only.Take(60).Concat(wait.Take(60)).Select(f => f.Shop).ToList(), only.Count, wait.Count, only.Sum(f => f.Total));
    }

    private static string Money(decimal value) => "m:" + value.ToString(CultureInfo.InvariantCulture);
    private static string Pct(decimal? value, int digits) => value is { } v ? $"p{digits}:" + v.ToString(CultureInfo.InvariantCulture) : "—";

    /// <summary>
    /// Выводы — порядок «Полевого контроля»: C1, C2, C3, C4, C5, C6, C7, C9, C8. Параметры: «m:» — сумма, «pN:» — доля с N знаками, «n:» — число;
    /// формирует страница на языке интерфейса.
    /// </summary>
    private static IReadOnlyList<PromoConclusion> Conclusions(
        PromoTiles tiles,
        IReadOnlyList<PromoByPromo> byPromo,
        IReadOnlyList<(string Region, decimal Man, decimal? Growth, decimal? P)> regions,
        IReadOnlyList<PromoRow> rows,
        IReadOnlyList<PromoEffect> effects,
        IReadOnlyList<PromoManualRow> manual,
        int histMonths,
        IReadOnlyList<PromoWaitingShop> waiting,
        int only,
        int wait,
        decimal onlyTotal,
        Func<long, string> promoName,
        Func<PromoRow, bool> isOut)
    {
        var list = new List<PromoConclusion>();
        static PromoConclusion C(string text, params (string Key, string Value)[] args) => new(text, args.ToDictionary(a => a.Key, a => a.Value));

        if (byPromo.FirstOrDefault() is { } top && tiles.Total > 0)
        {
            list.Add(C("Больше всего ушло на «{promo}» — {sum}, {share} всех скидок.", ("promo", top.Name), ("sum", Money(top.Sum)), ("share", Pct(top.Sum / tiles.Total, 0))));
        }

        // regions — кортежи (значимый тип): MaxBy пустого списка бросает исключение, а не возвращает default.
        if (regions.Where(r => r.P is not null).ToList() is { Count: > 0 } priced && priced.MaxBy(r => r.P) is { P: { } p } best)
        {
            list.Add(C("Выше всех доля скидок к продажам у региона {region}: {pct}.", ("region", best.Region), ("pct", Pct(p, 2))));
        }

        if (tiles.Manual > 0 && regions.Count > 0 && regions.MaxBy(r => r.Man) is { } man)
        {
            list.Add(C("Без акции (скидку поставили вручную) — {sum}, {share} всех скидок; больше всего у региона {region} ({regionSum}).",
                ("sum", Money(tiles.Manual)), ("share", Pct(tiles.Total > 0 ? tiles.Manual / tiles.Total : 0, 0)), ("region", man.Region), ("regionSum", Money(man.Man))));
        }

        if (tiles.Out > 0 && rows.Where(isOut).GroupBy(r => r.Promo).MaxBy(g => g.Sum(r => r.Discount)) is { } outTop)
        {
            list.Add(C("Вне срока акции принято {sum}, больше всего по «{promo}».", ("sum", Money(tiles.Out)), ("promo", promoName(outTop.Key))));
        }

        var rated = effects.Where(e => e.Status == "ok").ToList();
        if (rated.Where(e => e.Discount >= Math.Max(1_000_000, tiles.Total * 0.01m) && e.ReturnPerSum is not null && e.ExtraKg > 0).MaxBy(e => e.ReturnPerSum) is { } payback)
        {
            list.Add(C("Лучше всего окупилась «{promo}»: продажи товаров акции {growth} к базе, на 1 сум скидки — {ret} сум дополнительной выручки.",
                ("promo", payback.Name), ("growth", payback.Growth is { } g ? "s0:" + g.ToString(CultureInfo.InvariantCulture) : "—"),
                ("ret", "n1:" + payback.ReturnPerSum!.Value.ToString(CultureInfo.InvariantCulture))));
        }

        var noGrowth = rated.Where(e => e.ExtraKg <= 0).ToList();
        if (noGrowth.Count > 0)
        {
            list.Add(C("Без прироста продаж: {n} акц., скидок {sum} (крупнейшая — «{promo}»). Их стоит пересмотреть.",
                ("n", "n:" + noGrowth.Count), ("sum", Money(noGrowth.Sum(e => e.Discount))), ("promo", noGrowth.MaxBy(e => e.Discount)!.Name)));
        }

        if (histMonths >= 3)
        {
            var steady = manual.Where(m => m.MonthsWith == histMonths).ToList();
            if (steady.Count > 0)
            {
                list.Add(C("Ручные скидки ставят каждый месяц {months} мес. подряд — {n} агент(ов): {names}.",
                    ("months", "n:" + histMonths), ("n", "n:" + steady.Count), ("names", string.Join(", ", steady.Take(3).Select(m => $"{WithoutCode(m.Agent)} ({m.Region})")))));
            }

            if (only + wait > 0)
            {
                list.Add(C("За {months} мес. {n} магазин(ов) почти каждый месяц покупают со скидкой (закупки {sum}); ещё {k} в месяцы со скидкой берут в 2+ раза больше — ждут акцию.",
                    ("months", "n:" + histMonths), ("n", "n:" + only), ("sum", Money(onlyTotal)), ("k", "n:" + wait)));
            }
        }

        var withP = regions.Where(r => r.P is not null).ToList();
        if (withP.Count > 0)
        {
            var avg = withP.Average(r => r.P!.Value);
            var weak = withP.Where(r => r.P > avg && r.Growth is { } g && g <= 0).OrderByDescending(r => r.P).Take(4).ToList();
            if (weak.Count > 0)
            {
                list.Add(C("Скидок больше среднего ({avg}), а продажи к прошлому месяцу не выросли: {list}.",
                    ("avg", Pct(avg, 2)),
                    ("list", string.Join(", ", weak.Select(r => $"{r.Region} ({(r.P!.Value * 100).ToString("0.00", CultureInfo.GetCultureInfo("ru-RU"))}%, {(r.Growth!.Value * 100).ToString("0", CultureInfo.GetCultureInfo("ru-RU"))}%)")))));
            }
        }

        return list;
    }
}

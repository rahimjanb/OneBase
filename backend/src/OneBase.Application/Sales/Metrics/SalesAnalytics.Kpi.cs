namespace OneBase.Application.Sales.Metrics;

// Раздел «KPI»: KPI-показатели Linko по людям — план, факт, выполнение, премии и фонд оплаты, по регионам и направлениям.
public sealed partial class SalesAnalytics
{
    public KpiView CachedKpi(bool money) => View($"kpi:{money}", () => Kpi(money));

    /// <summary>
    /// Люди с показателями Linko за месяц. ТП — регион агента (профиль, продажи, визиты); СВР (показатель «… Супер») — профиль, затем
    /// «СВР региона» по имени, затем его продажи и визиты, затем регион в названии должности или показателя. Факт ТП — кг вторички агента
    /// за месяц, факт СВР — кг вторички всех ТП региона из раздела (один раз на регион, без удвоения при двух СВР).
    /// </summary>
    public KpiView Kpi(bool money)
    {
        var share = KpiMath.MonthShare(_d.WorkedDays, _d.DaysInMonth);
        var running = share < 1;
        decimal? M(decimal value) => money ? value : null;

        var byPerson = _d.Indicators.Where(i => i.Plan != 0 || i.Fact != 0).GroupBy(i => i.AgentId).ToList();
        var people = byPerson.Select(g =>
        {
            var svr = g.Any(i => KpiMath.IsSupervisorIndicator(i.FullName ?? i.Name)) || _d.TeamPlanStaff.Contains(g.Key);
            var (region, source) = svr ? SupervisorRegion(g.Key, g) : AgentRegion(g.Key);
            return (Id: g.Key, Items: g.ToList(), Svr: svr, Region: region, Source: source);
        }).ToList();

        var regionRows = new List<KpiRegion>();
        foreach (var group in people.GroupBy(p => p.Region))
        {
            var regionId = group.Key;
            var items = group.SelectMany(p => p.Items).ToList();

            // Колонки: метка показателя; две метки с разной единицей у одного человека (Сырдарья: «Тотал» в сумах и в кг) — две колонки.
            var labels = items.Select(i => (Label: KpiMath.Label(i.FullName ?? i.Name), Unit: KpiMath.UnitOf(i.PlanType))).Distinct().ToList();
            var duplicated = labels.GroupBy(l => l.Label).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
            string KeyOf(string label, string unit) => duplicated.Contains(label) ? $"{label} · {unit}" : label;
            var columns = KpiMath.Order(labels.Select(l => l.Label))
                .SelectMany(label => labels.Where(l => l.Label == label).OrderBy(l => l.Unit == KpiMath.Sum ? 0 : 1).ThenBy(l => l.Unit, StringComparer.Ordinal))
                .Select(l => new KpiColumn(KeyOf(l.Label, l.Unit), l.Label, l.Unit))
                .ToList();

            var tpKg = group.Where(p => !p.Svr).Sum(p => StatsOf(p.Id).Kg);
            var rows = group
                .Select(p => Person(p.Id, p.Items, p.Svr, p.Svr ? tpKg : StatsOf(p.Id).Kg, p.Source, regionId, share, running, money, KeyOf))
                .OrderBy(p => p.Role == "СВР" ? 0 : 1)
                .ThenBy(p => p.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), true))
                .ToList();

            KpiMoney Money(Func<KpiPerson, decimal?> premium, Func<KpiPerson, decimal?> salary)
            {
                var svrRows = rows.Where(r => r.Role == "СВР").ToList();
                var tpRows = rows.Where(r => r.Role != "СВР").ToList();
                return new KpiMoney(
                    M(svrRows.Sum(r => premium(r) ?? 0)), M(svrRows.Sum(r => salary(r) ?? 0)),
                    M(tpRows.Sum(r => premium(r) ?? 0)), M(tpRows.Sum(r => salary(r) ?? 0)),
                    M(rows.Sum(r => premium(r) ?? 0)), M(rows.Sum(r => salary(r) ?? 0)));
            }

            // Единица региона — как в «Полевом контроле»: план «Тотал» (или первого показателя) в сумах — «сум», иначе «кг».
            var main = rows.SelectMany(r => r.Cells).FirstOrDefault(c => c.Key.StartsWith("Тотал", StringComparison.Ordinal)) ?? rows.SelectMany(r => r.Cells).FirstOrDefault();
            var known = regionId != NoRegionId && _regions.ContainsKey(regionId);
            regionRows.Add(new KpiRegion(
                known ? regionId.ToString() : "none",
                known ? _regions[regionId].Name : "Без региона",
                known && _regions[regionId].DirectionId is { } d && _directions.TryGetValue(d, out var direction) ? direction.ManagerName ?? direction.Name : null,
                main?.Unit == KpiMath.Sum ? KpiMath.Sum : KpiMath.Kg,
                rows.Count(r => r.Role == "СВР"),
                rows.Count(r => r.Role != "СВР"),
                rows.Any(r => r.Role == "СВР") ? tpKg : 0,
                tpKg,
                Money(r => r.PremiumNow, r => r.SalaryNow),
                Money(r => r.PremiumForecast, r => r.SalaryForecast),
                columns,
                rows));
        }

        // «По регионам» — по фонду оплаты (без денег — по факту ТП); «Без региона» — последним.
        var ordered = regionRows
            .OrderBy(r => r.Id == "none" ? 1 : 0)
            .ThenByDescending(r => money ? r.Now.Payroll ?? 0 : r.FactKgTp)
            .ToList();

        var directions = ordered
            .Where(r => r.Id != "none")
            .GroupBy(r => _regions[Guid.Parse(r.Id)].DirectionId is { } d && _directions.ContainsKey(d) ? d : (Guid?)null)
            .OrderBy(g => g.Key is { } d ? _directions[d].SortOrder : int.MaxValue)
            .ThenBy(g => g.Key is { } d ? _directions[d].Name : "")
            .Select(g => new KpiDirection(
                g.Key is { } d ? _directions[d].ManagerName ?? _directions[d].Name : "Вне направлений",
                g.Select(r => r.Id).ToList(),
                g.Sum(r => r.Tp),
                M(g.Sum(r => r.Now.Premium ?? 0)), M(g.Sum(r => r.Forecast.Premium ?? 0)),
                M(g.Sum(r => r.Now.Payroll ?? 0)), M(g.Sum(r => r.Forecast.Payroll ?? 0))))
            .ToList();

        // Регионы с продажами за месяц, где в Linko нет ни одного KPI: для них премия не считается.
        var withKpi = regionRows.Select(r => r.Id).ToHashSet();
        var missing = _currentByRegion
            .Where(g => g.Key != NoRegionId && _regions.ContainsKey(g.Key) && !withKpi.Contains(g.Key.ToString()) && g.Sum(l => l.Kg) > 0)
            .Select(g => _regions[g.Key])
            .Where(r => r.DirectionId is not { } d || !_directions.TryGetValue(d, out var x) || !x.IsChannel)
            .OrderBy(r => r.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), true))
            .Select(r => r.Name)
            .ToList();

        var all = regionRows.SelectMany(r => r.People).ToList();
        KpiMoney Totals(Func<KpiPerson, decimal?> premium, Func<KpiPerson, decimal?> salary) => new(
            M(all.Where(p => p.Role == "СВР").Sum(p => premium(p) ?? 0)), M(all.Where(p => p.Role == "СВР").Sum(p => salary(p) ?? 0)),
            M(all.Where(p => p.Role != "СВР").Sum(p => premium(p) ?? 0)), M(all.Where(p => p.Role != "СВР").Sum(p => salary(p) ?? 0)),
            M(all.Sum(p => premium(p) ?? 0)), M(all.Sum(p => salary(p) ?? 0)));

        var factTp = all.Where(p => p.Role != "СВР").Sum(p => p.FactKg);
        return new KpiView(
            Period,
            money,
            running,
            share,
            _d.IndicatorsLoadedAt,
            all.Count(p => p.Role != "СВР"),
            all.Count(p => p.Role == "СВР"),
            factTp,
            running ? factTp / share : null,
            Totals(p => p.PremiumNow, p => p.SalaryNow),
            Totals(p => p.PremiumForecast, p => p.SalaryForecast),
            ordered,
            directions,
            missing,
            _regions.Values
                .Where(r => r.DirectionId is not { } d || !_directions.TryGetValue(d, out var x) || !x.IsChannel)
                .OrderBy(r => r.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), true))
                .Select(r => new KpiRegionOption(r.Id.ToString(), r.Name))
                .ToList());
    }

    private KpiPerson Person(long id, IReadOnlyList<StaffIndicator> items, bool svr, decimal factKg, string source, Guid regionId, decimal share, bool running,
        bool money, Func<string, string, string> keyOf)
    {
        decimal? M(decimal value) => money ? value : null;
        var cells = items
            .Select(i =>
            {
                var unit = KpiMath.UnitOf(i.PlanType);
                var levels = _d.IndicatorLevels.GetValueOrDefault(i.IndicatorId) ?? [];
                var (threshold, bonus) = KpiMath.BonusLevel(levels);
                var pct = KpiMath.Pct(i.Fact, i.Plan);

                // Прогноз: факт ÷ доля месяца (АКБ не растягивается — точки не складываются по дням), выполнение без округления.
                var factForecast = running && unit != KpiMath.Akb ? i.Fact / share : i.Fact;
                var ratioForecast = i.Plan == 0 ? 0 : factForecast / i.Plan;
                var premiumForecast = running ? Math.Round(KpiMath.Premium(levels, ratioForecast), 0, MidpointRounding.AwayFromZero) : i.Bonus;
                return new KpiCell(
                    keyOf(KpiMath.Label(i.FullName ?? i.Name), unit),
                    i.IndicatorId,
                    i.FullName ?? i.Name,
                    unit,
                    i.Plan,
                    i.Sales,
                    i.Return,
                    i.Fact,
                    pct,
                    KpiMath.Tone(pct, threshold),
                    i.Level,
                    threshold,
                    M(bonus),
                    M(i.Bonus),
                    factForecast,
                    ratioForecast,
                    KpiMath.Tone(ratioForecast, threshold),
                    M(premiumForecast));
            })
            .ToList();

        var fix = items.Max(i => i.Salary);
        var premiumNow = cells.Sum(c => c.PremiumNow ?? 0);
        var premiumForecast = cells.Sum(c => c.PremiumForecast ?? 0);
        return new KpiPerson(
            id,
            PersonName(AgentName(id)),
            svr ? "СВР" : "ТП",
            regionId != NoRegionId && _regions.TryGetValue(regionId, out var region) ? region.Name : null,
            source,
            factKg,
            M(fix),
            M(cells.Sum(c => c.Bonus100 ?? 0)),
            M(premiumNow),
            M(premiumForecast),
            M(fix + premiumNow),
            M(fix + premiumForecast),
            cells);
    }

    /// <summary>Регион ТП — как во всём разделе продаж: профиль агента, продажи, визиты.</summary>
    private (Guid Region, string Source) AgentRegion(long agent)
    {
        var region = _agentRegion.GetValueOrDefault(agent, NoRegionId);
        if (region == NoRegionId)
        {
            return (NoRegionId, "none");
        }

        return (region, _d.Agents.TryGetValue(agent, out var info) && info.ProfileRegionId == region ? "profile" : "sales");
    }

    /// <summary>
    /// Регион СВР: в Linko у пользователя нет филиала. По порядку: профиль агента (указан вручную), «СВР региона» по имени, продажи и визиты
    /// (как у ТП), название региона в должности или в названии показателя («… Наманган Супер»).
    /// </summary>
    private (Guid Region, string Source) SupervisorRegion(long agent, IEnumerable<StaffIndicator> items)
    {
        if (_d.Agents.TryGetValue(agent, out var info) && info.ProfileRegionId is { } profile && _regions.ContainsKey(profile))
        {
            return (profile, "profile");
        }

        var name = Normalize(AgentName(agent));
        var bySupervisor = _regions.Values.Where(r => r.Supervisor is { Length: > 0 } s && Normalize(s) == name).ToList();
        if (bySupervisor.Count == 1)
        {
            return (bySupervisor[0].Id, "supervisor");
        }

        var activity = _agentRegion.GetValueOrDefault(agent, NoRegionId);
        if (activity != NoRegionId)
        {
            return (activity, "sales");
        }

        var text = string.Join(" ", items.Select(i => i.FullName ?? i.Name).Prepend(info?.Job ?? ""));
        var byText = _regions.Values
            .Where(r => r.DirectionId is not { } d || !_directions.TryGetValue(d, out var x) || !x.IsChannel)
            .Where(r => RegionAliases(r.Name).Any(alias => text.Contains(alias, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return byText.Count == 1 ? (byText[0].Id, "name") : (NoRegionId, "none");
    }

    /// <summary>Имя без номера, с которым Linko хранит пользователей («370 SHahnoza opa» → «SHahnoza opa»; номер — в подписи «ID 370»).</summary>
    private static string PersonName(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"^\d+\s+", "") is { Length: > 0 } clean ? clean : name.Trim();

    private static string Normalize(string name) => string.Join(" ", name.ToLowerInvariant().Replace('ё', 'е').Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Как регион пишут в должностях и показателях Linko: «Андижон» — «Андижан», «Жиззах» — «Джиззах», «Сырдарья» — «Сырдаре».</summary>
    private static IEnumerable<string> RegionAliases(string region)
    {
        yield return region;
        var stem = region.Length > 5 ? region[..^1] : region;
        yield return stem;
        if (region.StartsWith('Ж'))
        {
            yield return "Дж" + region[1..^1];
        }

        if (region.EndsWith("он", StringComparison.Ordinal))
        {
            yield return region[..^2] + "ан";
        }
    }
}

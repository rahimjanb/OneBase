using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;

namespace OneBase.Application.Sales.Stock;

/// <summary>
/// Остаток SKU на одном складе: штуки как в Linko, кг, коробки и стоимость; скорость без поправки (RawKgPerDay) и с поправкой на аутсток
/// (KgPerDay; ZeroDays — зачтённые дни в нуле закрытого месяца), та же скорость в коробках и деньгах (BoxesPerDay, SumPerDay — для матрицы
/// в выбранной единице); запас на 15 дней в кг, коробках и деньгах; рекомендуемый заказ (OrderKg — вверх до целой коробки; у завода —
/// заказ у завода). Все переводы в коробки, штуки и деньги сделаны здесь — страница ничего не пересчитывает.
/// </summary>
public sealed record StockCell(
    decimal Pieces,
    decimal? Kg,
    decimal? Boxes,
    decimal? ValueSum,
    decimal? KgPerDay,
    decimal? RawKgPerDay,
    decimal? BoxesPerDay,
    decimal? SumPerDay,
    int ZeroDays,
    decimal? DaysOfCover,
    decimal? Need15Kg,
    decimal? Need15Boxes,
    decimal? Need15Sum,
    decimal OrderKg,
    decimal? OrderBoxes,
    decimal? OrderPieces,
    decimal? OrderSum,
    string Status);

public static class StockStatuses
{
    /// <summary>Хватит меньше чем на 15 дней продаж.</summary>
    public const string Deficit = "deficit";

    /// <summary>Хватит больше чем на 30 дней продаж.</summary>
    public const string Overstock = "overstock";

    /// <summary>Остаток лежит, а продаж за базовый период не было.</summary>
    public const string Dead = "dead";

    public const string Ok = "ok";

    /// <summary>Нет веса штуки — дни запаса не посчитать.</summary>
    public const string Unknown = "unknown";

    /// <summary>Своей скорости продаж нет (склад экспорта) — статуса нет.</summary>
    public const string None = "none";

    public static string Of(decimal? kg, decimal? kgPerDay)
    {
        if (kg is null)
        {
            return Unknown;
        }

        if (kgPerDay is not > 0)
        {
            return kg > 0 ? Dead : Ok;
        }

        var days = kg.Value / kgPerDay.Value;
        return days < StockMath.OrderCoverDays ? Deficit : days > 30 ? Overstock : Ok;
    }
}

/// <summary>Охваты рекомендуемого остатка: страна (склады дилеров), РМ, регион, завод, экспорт.</summary>
public static class StockScopes
{
    public const string Country = "country";
    public const string Direction = "rm";
    public const string Region = "region";
    public const string Plant = "plant";
    public const string Export = "export";
}

/// <summary>
/// Товар в рекомендуемом остатке по выбранному охвату. Price — входная цена дилера за единицу учёта на начало месяца; ValueSum — остаток × цена;
/// KgPerDay — скорость с поправкой на аутсток, RawKgPerDay — без; Need15Kg и Need30Kg — скорость × горизонт; OrderKg — рекомендуемый заказ
/// дилера (у охвата «Завод» — заказ у завода); PackKg — фасовка из названия (фильтр); Top — товар из списка ТОП.
/// </summary>
public sealed record StockItem(
    long ProductId,
    string Name,
    string? Code,
    string Category,
    bool InReport,
    bool Top,
    decimal? UnitKg,
    string UnitKgSource,
    decimal? BoxKg,
    string BoxNote,
    decimal? PackKg,
    decimal Pieces,
    decimal? Kg,
    decimal? Boxes,
    decimal? KgPerDay,
    decimal? RawKgPerDay,
    decimal? BoxesPerDay,
    decimal? SumPerDay,
    decimal? DaysOfCover,
    decimal? Need15Kg,
    decimal? Need15Boxes,
    decimal? Need15Sum,
    decimal? Need30Kg,
    decimal? Price,
    decimal? ValueSum,
    decimal OrderKg,
    decimal? OrderBoxes,
    decimal? OrderPieces,
    decimal? OrderSum,
    string Status,
    IReadOnlyDictionary<string, StockCell> Regions,
    StockCell? Factory);

/// <summary>Товар, которого в таблице нет: вне категорий отчёта (бонус, подарочные наборы, импорт) или без веса единицы.</summary>
public sealed record StockExcluded(long ProductId, string Name, string? Code, string Category, string Reason, decimal Pieces, decimal? Kg, decimal FactoryPieces);

public static class StockExcludedReasons
{
    public const string OutsideReport = "outsideReport";
    public const string WithoutWeight = "withoutWeight";
}

/// <summary>Плитка категории по отфильтрованным строкам (до фильтра по категориям): где запас кончается, а где лежит мёртвым грузом.</summary>
public sealed record StockCategoryTile(string Name, int Skus, decimal Kg, decimal ValueSum, decimal KgPerDay, decimal? DaysOfCover, int Deficit, decimal OrderKg, bool Selected);

/// <summary>
/// Итоги по отфильтрованным строкам: кг, коробки, скорость (с поправкой и без; в коробках и деньгах — суммой по строкам, где они есть)
/// и дни запаса, запас на 15 дней в трёх единицах, SKU по статусам; ValueSum — стоимость запаса по входной цене (только SKU с ценой),
/// WithoutPrice — SKU с остатком без цены; ApproxWeight — SKU с весом единицы из названия; WithoutWeight — товары без веса единицы
/// (в таблицу не входят); OrderKg/OrderSum/OrderSkus — рекомендуемый заказ.
/// </summary>
public sealed record StockTotals(
    decimal Pieces,
    decimal? Kg,
    decimal? Boxes,
    decimal? KgPerDay,
    decimal? RawKgPerDay,
    decimal? BoxesPerDay,
    decimal? SumPerDay,
    decimal? DaysOfCover,
    decimal? Need15Kg,
    decimal? Need15Boxes,
    decimal? Need15Sum,
    int Skus,
    int Deficit,
    int Overstock,
    int Dead,
    int WithoutWeight,
    decimal ValueSum,
    int WithoutPrice,
    int ApproxWeight,
    decimal OrderKg,
    decimal OrderBoxes,
    decimal OrderSum,
    int OrderSkus);

/// <summary>Поправка скорости на аутсток по стране: скорость до и после, пары с зачтёнными днями в нуле, изменение в долях.</summary>
public sealed record StockCorrection(int Year, int Month, decimal RawKgPerDay, decimal KgPerDay, int Pairs, decimal? Change);

public sealed record StockDirection(string Id, string Name);

/// <summary>Запрос страницы: охват (country | rm:id | region:id | plant | export), поиск, категории, фасовки, ТОП (all|only|not), статус.</summary>
public sealed record StockQuery(
    string? Scope = null,
    string? Query = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<decimal>? Packs = null,
    string? Top = null,
    string? Status = null);

public sealed record StockView(
    DateTimeOffset? SyncedAt,
    DateOnly SnapshotDate,
    int VelocityDays,
    DateOnly VelocityFrom,
    DateOnly VelocityTo,
    DateOnly UnitWeightFrom,
    string? PriceList,
    DateOnly PriceAsOf,
    string Scope,
    string ScopeName,
    string? Query,
    IReadOnlyList<string> SelectedCategories,
    IReadOnlyList<decimal> SelectedPacks,
    string Top,
    bool TopConfigured,
    string Status,
    IReadOnlyList<StockRegion> Regions,
    IReadOnlyList<StockRegion> ScopeRegions,
    IReadOnlyList<StockDirection> Directions,
    StockRegion? Factory,
    StockRegion? Export,
    IReadOnlyList<string> Categories,
    IReadOnlyList<decimal> Packs,
    IReadOnlyList<StockCategoryTile> CategoryTiles,
    IReadOnlyList<StockItem> Items,
    IReadOnlyList<OtherStock> OtherStocks,
    IReadOnlyList<StockExcluded> Excluded,
    int ExcludedOutsideReport,
    int ExcludedWithoutWeight,
    StockTotals Totals,
    StockTotals? FactoryTotals,
    IReadOnlyDictionary<string, StockTotals> RegionTotals,
    StockCorrection Correction);

/// <summary>Товар с клетками всех складов дилеров, завода и экспорта — до выбора охвата и фильтров.</summary>
public sealed record StockBaseItem(
    StockProduct Product,
    string Category,
    bool InReport,
    bool Top,
    IReadOnlyDictionary<string, StockCell> Regions,
    StockCell? Factory,
    StockCell? Export);

/// <summary>Рекомендуемый остаток до охвата и фильтров; строится один раз на снимок и кэшируется. TopConfigured — список ТОП задан (Sales:TopProducts).</summary>
public sealed record StockBase(
    StockSnapshot Snapshot,
    int VelocityDays,
    DateOnly VelocityFrom,
    DateOnly VelocityTo,
    bool TopConfigured,
    IReadOnlyList<StockDirection> Directions,
    IReadOnlyList<StockBaseItem> Items,
    IReadOnlyList<StockExcluded> Excluded,
    StockCorrection Correction);

/// <summary>
/// Рекомендуемый остаток: снимок остатков Linko (StockSnapshot: штуки → кг по весу единицы, коробки, стоимость по входной цене на начало
/// месяца) и скорость продаж — вторичка нетто возвратов за Sales:StockVelocityDays (87) дней до последнего полного дня, по региону склада,
/// с поправкой на дни в нуле из аутстока за последний закрытый месяц (OutstockService). Рекомендуемый заказ дилера — до запаса на 15 дней
/// вверх до коробки; заказ у завода — заказы всех дилеров минус остаток завода. «Вся страна» — только склады дилеров; завод и экспорт —
/// отдельные охваты. Фильтры и итоги по отфильтрованным строкам считаются здесь (Compose), страница только показывает.
/// </summary>
public sealed class StockService(IAppDbContext db, SalesOptions options, OutstockService outstock, IMemoryCache cache, SalesCacheSignal signal)
{
    /// <summary>Страна или один склад региона — без фильтров (AI-инструменты, проактивные проверки).</summary>
    public Task<StockView> GetAsync(string? regionId, CancellationToken ct = default) =>
        QueryAsync(new StockQuery(regionId is null ? null : $"{StockScopes.Region}:{regionId}"), ct);

    public async Task<StockView> QueryAsync(StockQuery q, CancellationToken ct = default)
    {
        var b = await SalesViewCache.GetAsync(cache, signal, "sales:stock:base", () => BuildBaseAsync(ct));
        return Compose(b, q);
    }

    /// <summary>Фасовки из адреса: «3,2.5,0.18».</summary>
    public static IReadOnlyList<decimal> ParsePacks(string? pack) =>
        string.IsNullOrWhiteSpace(pack)
            ? []
            : pack.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => decimal.TryParse(p.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null)
                .Where(d => d is > 0)
                .Select(d => Math.Round(d!.Value, 4))
                .Distinct()
                .ToList();

    /// <summary>Охват и фильтры поверх базы: строки, итоги, плитки категорий — всё по отфильтрованному набору.</summary>
    public static StockView Compose(StockBase b, StockQuery q)
    {
        var s = b.Snapshot;
        var (scope, scopeId) = ParseScope(q.Scope);
        List<StockRegion> scopeRegions = scope switch
        {
            StockScopes.Region => s.Regions.Where(r => string.Equals(r.Id, scopeId, StringComparison.OrdinalIgnoreCase)).ToList(),
            StockScopes.Direction => s.Regions.Where(r => r.DirectionId is { } d && string.Equals(d, scopeId, StringComparison.OrdinalIgnoreCase)).ToList(),
            StockScopes.Plant or StockScopes.Export => [],
            _ => s.Regions.ToList(),
        };
        var scopeName = scope switch
        {
            StockScopes.Region => scopeRegions.FirstOrDefault()?.Name ?? "Регион не найден",
            StockScopes.Direction => b.Directions.FirstOrDefault(d => string.Equals(d.Id, scopeId, StringComparison.OrdinalIgnoreCase))?.Name ?? "РМ не найден",
            StockScopes.Plant => s.Factory?.Name ?? "Завод",
            StockScopes.Export => s.Export?.Name ?? "Экспорт",
            _ => "Вся страна",
        };
        var scopeKey = scope is StockScopes.Region or StockScopes.Direction ? $"{scope}:{scopeId}" : scope;
        var scopeIds = scopeRegions.Select(r => r.Id).ToHashSet();

        var rows = b.Items.Select(i => Row(i, scope, scopeIds)).Where(r => r is not null).Select(r => r!).ToList();

        // Фильтры: поиск, ТОП, статус, фасовка — и категории; плитки категорий и список фасовок — до фильтра по категориям.
        var top = q.Top?.Trim().ToLowerInvariant() is "only" or "not" ? q.Top.Trim().ToLowerInvariant() : "all";
        var status = q.Status?.Trim().ToLowerInvariant() is StockStatuses.Deficit or StockStatuses.Overstock or StockStatuses.Dead or StockStatuses.Ok ? q.Status.Trim().ToLowerInvariant() : "all";
        var packs = (q.Packs ?? []).Select(p => Math.Round(p, 4)).Distinct().OrderBy(p => p).ToList();
        var chosen = (q.Categories ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var searched = StockMath.Search(rows, q.Query, r => r.Code, r => r.Name)
            .Where(r => top == "all" || r.Top == (top == "only"))
            .Where(r => status == "all" || r.Status == status)
            .Where(r => packs.Count == 0 || (r.PackKg is { } p && packs.Contains(p)))
            .ToList();
        var filtered = chosen.Count == 0 ? searched : searched.Where(r => chosen.Contains(r.Category)).ToList();
        filtered = filtered.OrderByDescending(r => r.Kg ?? 0).ThenBy(r => r.Name).ToList();

        var tiles = searched.GroupBy(r => r.Category)
            .Select(g =>
            {
                var kg = g.Sum(r => r.Kg ?? 0);
                var perDay = g.Sum(r => r.KgPerDay ?? 0);
                return new StockCategoryTile(g.Key, g.Count(), kg, g.Sum(r => r.ValueSum ?? 0), perDay, perDay > 0 ? kg / perDay : null,
                    g.Count(r => r.Status == StockStatuses.Deficit), g.Sum(r => r.OrderKg), chosen.Contains(g.Key));
            })
            .OrderByDescending(t => t.Kg)
            .ThenBy(t => t.Name)
            .ToList();

        var withoutWeight = b.Excluded.Count(e => e.Reason == StockExcludedReasons.WithoutWeight);
        var factoryTotals = s.Factory is null ? null : Totals(filtered.Where(r => r.Factory is not null).Select(r => CellRow(r, r.Factory!)), withoutWeight);
        // Итоги по складам охвата — для подвала матрицы: по отфильтрованным строкам, у каждого склада по его клеткам.
        var regionTotals = scopeRegions.ToDictionary(
            r => r.Id,
            r => Totals(filtered.Where(x => x.Regions.ContainsKey(r.Id)).Select(x => CellRow(x, x.Regions[r.Id])), withoutWeight));

        return new StockView(
            s.SyncedAt,
            s.SnapshotDate,
            b.VelocityDays,
            b.VelocityFrom,
            b.VelocityTo,
            s.UnitWeightFrom,
            s.PriceList,
            s.PriceAsOf,
            scopeKey,
            scopeName,
            string.IsNullOrWhiteSpace(q.Query) ? null : q.Query.Trim(),
            (q.Categories ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            packs,
            top,
            b.TopConfigured,
            status,
            s.Regions,
            scopeRegions,
            b.Directions,
            s.Factory,
            s.Export,
            rows.Select(r => r.Category).Distinct().OrderBy(c => c, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"), true)).ToList(),
            rows.Where(r => r.PackKg is not null).Select(r => r.PackKg!.Value).Distinct().OrderBy(p => p).ToList(),
            tiles,
            filtered,
            s.OtherStocks,
            b.Excluded,
            b.Excluded.Count(e => e.Reason == StockExcludedReasons.OutsideReport),
            withoutWeight,
            Totals(filtered.Select(r => new TotalRow(r.Pieces, r.Kg, r.Boxes, r.KgPerDay, r.RawKgPerDay, r.BoxesPerDay, r.SumPerDay, r.Need15Kg, r.Need15Boxes, r.Need15Sum,
                r.Status, r.ValueSum, r.Price is not null, r.UnitKgSource == WeightSources.Name, r.OrderKg, r.OrderBoxes, r.OrderSum)), withoutWeight),
            factoryTotals,
            regionTotals,
            b.Correction);
    }

    /// <summary>Строка итогов по одной клетке склада (завод, склад региона в матрице): статус — по её остатку и скорости.</summary>
    private static TotalRow CellRow(StockItem r, StockCell c) =>
        new(c.Pieces, c.Kg, c.Boxes, c.KgPerDay, c.RawKgPerDay, c.BoxesPerDay, c.SumPerDay, c.Need15Kg, c.Need15Boxes, c.Need15Sum,
            c.Status, c.ValueSum, r.Price is not null, r.UnitKgSource == WeightSources.Name, c.OrderKg, c.OrderBoxes, c.OrderSum);

    /// <summary>Охват из строки запроса: «rm:id», «region:id», «plant», «export»; пусто или неизвестно — страна.</summary>
    public static (string Scope, string? Id) ParseScope(string? scope)
    {
        var v = scope?.Trim() ?? string.Empty;
        var colon = v.IndexOf(':');
        var kind = (colon < 0 ? v : v[..colon]).ToLowerInvariant();
        var id = colon < 0 ? null : v[(colon + 1)..].Trim();
        return kind switch
        {
            StockScopes.Region when !string.IsNullOrEmpty(id) => (StockScopes.Region, id),
            StockScopes.Direction when !string.IsNullOrEmpty(id) => (StockScopes.Direction, id),
            StockScopes.Plant => (StockScopes.Plant, null),
            StockScopes.Export => (StockScopes.Export, null),
            _ => (StockScopes.Country, null),
        };
    }

    /// <summary>Строка таблицы в охвате: сумма клеток складов дилеров охвата, клетка завода или клетка экспорта; null — ни остатка, ни продаж.</summary>
    private static StockItem? Row(StockBaseItem i, string scope, HashSet<string> scopeIds)
    {
        var p = i.Product;
        StockCell total;
        IReadOnlyDictionary<string, StockCell> regions;
        if (scope == StockScopes.Plant)
        {
            if (i.Factory is not { } f)
            {
                return null;
            }

            total = f;
            regions = new Dictionary<string, StockCell>();
        }
        else if (scope == StockScopes.Export)
        {
            if (i.Export is not { } e || e.Kg is not > 0)
            {
                return null;
            }

            total = e;
            regions = new Dictionary<string, StockCell>();
        }
        else
        {
            var cells = i.Regions.Where(kv => scopeIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            regions = cells;
            var kg = cells.Values.Sum(c => c.Kg ?? 0);
            var perDay = cells.Values.Sum(c => c.KgPerDay ?? 0);
            var need15 = perDay * StockMath.OrderCoverDays;
            var order = cells.Values.Sum(c => c.OrderKg);
            total = new StockCell(
                cells.Values.Sum(c => c.Pieces),
                kg,
                StockMath.Boxes(kg, p.BoxKg),
                p.Price is null ? null : cells.Values.Sum(c => c.ValueSum ?? 0),
                perDay,
                cells.Values.Sum(c => c.RawKgPerDay ?? 0),
                StockMath.Boxes(perDay, p.BoxKg),
                StockMath.SumOfKg(perDay, p.UnitKg, p.Price),
                cells.Values.Sum(c => c.ZeroDays),
                perDay > 0 ? kg / perDay : null,
                need15,
                StockMath.Boxes(need15, p.BoxKg),
                StockMath.SumOfKg(need15, p.UnitKg, p.Price),
                order,
                StockMath.Boxes(order, p.BoxKg),
                p.UnitKg is { } u && u > 0 ? order / u : null,
                StockMath.SumOfKg(order, p.UnitKg, p.Price),
                StockStatuses.Of(kg, perDay));
        }

        if (total.Kg is not > 0 && total.KgPerDay is not > 0)
        {
            return null; // в охвате ни остатка, ни продаж
        }

        var status = scope == StockScopes.Export ? StockStatuses.None : StockStatuses.Of(total.Kg, total.KgPerDay);
        return new StockItem(
            p.Id,
            p.Name,
            p.Code,
            i.Category,
            i.InReport,
            i.Top,
            p.UnitKg,
            p.UnitKgSource,
            p.BoxKg,
            p.BoxNote,
            p.PackKg,
            total.Pieces,
            total.Kg,
            total.Boxes,
            total.KgPerDay,
            total.RawKgPerDay,
            total.BoxesPerDay,
            total.SumPerDay,
            total.DaysOfCover,
            total.Need15Kg,
            total.Need15Boxes,
            total.Need15Sum,
            total.KgPerDay is { } d ? d * 30 : null,
            p.Price,
            total.ValueSum,
            total.OrderKg,
            total.OrderBoxes,
            total.OrderPieces,
            total.OrderSum,
            status,
            regions,
            i.Factory);
    }

    public async Task<StockBase> BuildBaseAsync(CancellationToken ct = default)
    {
        var s = await StockSnapshotBuilder.GetAsync(cache, signal, db, options, ct);
        var sold = options.SoldStatuses;
        var returned = options.ReturnStatuses;
        var excluded = options.NotSecondaryBranchesLower(); // скорость — вторичка: без «Завода» и пропускаемых филиалов
        var days = Math.Max(1, options.StockVelocityDays);

        // База скорости: StockVelocityDays дней до последнего полного дня данных — вчера или последней приёмки не позже вчера.
        var cutoff = SecondarySales.ReportCutoff(DateTimeOffset.UtcNow);
        var lastAccepted = await db.LinkoOrders.AsNoTracking()
            .Where(o => sold.Contains(o.Status) && o.AcceptedDate != null && o.AcceptedDate <= cutoff && (o.BranchName == null || !excluded.Contains(o.BranchName.ToLower())))
            .MaxAsync(o => o.AcceptedDate, ct);
        var (velocityFrom, velocityTo) = StockMath.VelocityWindow(cutoff, lastAccepted, days);

        var orders = await (
                from l in db.LinkoOrderLines
                join o in db.LinkoOrders on l.OrderId equals o.Id
                where sold.Contains(o.Status) && o.AcceptedDate >= velocityFrom && o.AcceptedDate <= velocityTo && l.ProductId != null
                      && (o.BranchName == null || !excluded.Contains(o.BranchName.ToLower()))
                group l by new { o.BranchId, l.ProductId } into g
                select new { g.Key.BranchId, ProductId = g.Key.ProductId!.Value, Kg = g.Sum(x => x.TotalWeight) })
            .ToListAsync(ct);
        // Возвраты — по строкам документа, дата создания возврата (другой даты у возврата нет), как во вторичке.
        var returns = await (
                from l in db.LinkoOrderReturnLines
                join r in db.LinkoOrderReturns on l.ReturnId equals r.Id
                where returned.Contains(r.Status) && r.CreatedDate >= velocityFrom && r.CreatedDate <= velocityTo && l.ProductId != null
                      && (r.BranchName == null || !excluded.Contains(r.BranchName.ToLower()))
                group l by new { r.BranchId, l.ProductId } into g
                select new { g.Key.BranchId, ProductId = g.Key.ProductId!.Value, Kg = g.Sum(x => x.TotalWeight) })
            .ToListAsync(ct);

        var regionOfBranch = s.RegionOfBranch();
        var baseKg = new Dictionary<(string Region, long Product), decimal>();
        foreach (var (branch, product, kg) in orders.Select(o => (o.BranchId, o.ProductId, o.Kg)).Concat(returns.Select(r => (r.BranchId, r.ProductId, -r.Kg))))
        {
            if (branch is { } bId && regionOfBranch.TryGetValue(bId, out var region))
            {
                var key = (region, product);
                baseKg[key] = baseKg.GetValueOrDefault(key) + kg;
            }
        }

        // Дни в нуле по парам «регион × товар» за последний закрытый месяц — из аутстока (только склады дилеров, только проданное в месяце).
        var outs = await outstock.BaseAsync(s.ClosedYear, s.ClosedMonth, ct);
        var zeroDays = outs.Pairs.ToDictionary(p => (p.RegionId, p.ProductId), p => p.ZeroDays);

        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var categories = SalesCategories.Build(options.Categories, types);
        var top = TopProductSet.Of(options);
        var directionRows = await db.SalesDirections.AsNoTracking().Select(d => new { d.Id, d.Name, d.SortOrder }).ToListAsync(ct);
        var used = s.Regions.Select(r => r.DirectionId).Where(d => d is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directions = directionRows.Where(d => used.Contains(d.Id.ToString())).OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
            .Select(d => new StockDirection(d.Id.ToString(), d.Name.Trim())).ToList();

        var dealerStockIds = s.Regions.Select(r => r.StockId).ToHashSet();
        var withBalance = s.Pieces.Keys.Where(k => dealerStockIds.Contains(k.StockId) || k.StockId == s.Factory?.StockId || k.StockId == s.Export?.StockId).Select(k => k.ProductId);
        var productIds = withBalance.Concat(baseKg.Where(kv => kv.Value > 0).Select(kv => kv.Key.Product)).Distinct().ToList();

        var items = new List<StockBaseItem>();
        var excludedItems = new List<StockExcluded>();
        decimal rawCountry = 0, correctedCountry = 0;
        var correctedPairs = 0;
        foreach (var productId in productIds)
        {
            var product = s.Products.GetValueOrDefault(productId) ?? new StockProduct(productId, $"Товар {productId}", null, null, null, null, WeightSources.None, null, "товара нет в справочнике", null, null);
            var group = categories.GroupOf(product.TypeId);
            var category = categories.NameOf(group);
            var inReport = SalesCategories.IsConfigured(group);
            var dealerPieces = s.Regions.Sum(r => s.PiecesOf(productId, r.StockId));
            if (!inReport || product.UnitKg is null)
            {
                excludedItems.Add(new StockExcluded(productId, product.Name, product.Code, category,
                    inReport ? StockExcludedReasons.WithoutWeight : StockExcludedReasons.OutsideReport,
                    dealerPieces, StockMath.Kg(dealerPieces, product.UnitKg), s.Factory is { } fs ? s.PiecesOf(productId, fs.StockId) : 0));
                continue;
            }

            var cells = new Dictionary<string, StockCell>();
            decimal countryRaw = 0, countryCorrected = 0, dealerOrders = 0;
            foreach (var region in s.Regions)
            {
                var kg87 = baseKg.GetValueOrDefault((region.Id, productId));
                var raw = Math.Max(0, kg87) / days;
                var (corrected, z) = StockMath.CorrectedKgPerDay(kg87, days, zeroDays.GetValueOrDefault((region.Id, productId)));
                var cell = Cell(s, product, region.StockId, corrected, raw, z, corrected, null);
                cells[region.Id] = cell;
                countryRaw += raw;
                countryCorrected += corrected;
                dealerOrders += cell.OrderKg;
                if (z > 0)
                {
                    correctedPairs++;
                }
            }

            rawCountry += countryRaw;
            correctedCountry += countryCorrected;
            var factory = s.Factory is { } f ? Cell(s, product, f.StockId, countryCorrected, countryRaw, 0, null, dealerOrders) : null;
            var export = s.Export is { } e ? Cell(s, product, e.StockId, null, null, 0, null, null) : null;
            items.Add(new StockBaseItem(product, category, inReport, top.Contains(product.Code), cells, factory, export));
        }

        return new StockBase(
            s,
            days,
            velocityFrom,
            velocityTo,
            top.Configured,
            directions,
            items.OrderBy(i => i.Product.Name).ToList(),
            excludedItems.OrderBy(e => e.Reason).ThenByDescending(e => e.Pieces).ThenBy(e => e.Name).ToList(),
            new StockCorrection(s.ClosedYear, s.ClosedMonth, rawCountry, correctedCountry, correctedPairs, rawCountry > 0 ? correctedCountry / rawCountry - 1 : null));
    }

    /// <summary>
    /// Клетка склада: у дилера заказ — до запаса на 15 дней по скорости с поправкой (orderSpeed), у завода — заказы дилеров минус его остаток
    /// (dealerOrdersKg), у экспорта ни скорости, ни заказа.
    /// </summary>
    private static StockCell Cell(StockSnapshot s, StockProduct p, long stockId, decimal? kgPerDay, decimal? rawKgPerDay, int zeroDays, decimal? orderSpeed, decimal? dealerOrdersKg)
    {
        var pieces = s.PiecesOf(p.Id, stockId);
        var kg = StockMath.Kg(pieces, p.UnitKg) ?? 0;
        var order = orderSpeed is { } speed ? StockMath.OrderKg(speed, kg, p.BoxKg)
            : dealerOrdersKg is { } demand ? StockMath.FactoryOrderKg(demand, kg)
            : 0;
        var orderPieces = p.UnitKg is { } u && u > 0 ? order / u : (decimal?)null;
        var need15 = kgPerDay is { } d ? d * StockMath.OrderCoverDays : (decimal?)null;
        return new StockCell(
            pieces,
            kg,
            StockMath.Boxes(kg, p.BoxKg),
            StockMath.ValueSum(pieces, p.Price),
            kgPerDay,
            rawKgPerDay,
            StockMath.Boxes(kgPerDay, p.BoxKg),
            StockMath.SumOfKg(kgPerDay, p.UnitKg, p.Price),
            zeroDays,
            kgPerDay is > 0 ? kg / kgPerDay.Value : null,
            need15,
            StockMath.Boxes(need15, p.BoxKg),
            StockMath.SumOfKg(need15, p.UnitKg, p.Price),
            order,
            StockMath.Boxes(order, p.BoxKg),
            orderPieces,
            StockMath.SumOfKg(order, p.UnitKg, p.Price),
            kgPerDay is null && rawKgPerDay is null ? StockStatuses.None : StockStatuses.Of(kg, kgPerDay)); // у экспорта скорости нет — статуса нет
    }

    /// <summary>Строка для итогов: у склада завода PerDay — скорость всей страны.</summary>
    private sealed record TotalRow(
        decimal Pieces,
        decimal? Kg,
        decimal? Boxes,
        decimal? PerDay,
        decimal? RawPerDay,
        decimal? BoxesPerDay,
        decimal? SumPerDay,
        decimal? Need15Kg,
        decimal? Need15Boxes,
        decimal? Need15Sum,
        string? Status,
        decimal? Value,
        bool HasPrice,
        bool ApproxWeight,
        decimal OrderKg,
        decimal? OrderBoxes,
        decimal? OrderSum)
    {
        public bool HasStock => Pieces != 0;
    }

    private static StockTotals Totals(IEnumerable<TotalRow> source, int withoutWeight)
    {
        var rows = source.ToList();
        var kg = rows.Sum(r => r.Kg ?? 0);
        var perDay = rows.Sum(r => r.PerDay ?? 0);
        var raw = rows.Sum(r => r.RawPerDay ?? 0);
        return new StockTotals(
            rows.Sum(r => r.Pieces),
            kg,
            rows.Sum(r => r.Boxes ?? 0),
            perDay == 0 ? null : perDay,
            raw == 0 ? null : raw,
            rows.Sum(r => r.BoxesPerDay ?? 0),
            rows.Sum(r => r.SumPerDay ?? 0),
            perDay > 0 ? kg / perDay : null,
            rows.Sum(r => r.Need15Kg ?? 0),
            rows.Sum(r => r.Need15Boxes ?? 0),
            rows.Sum(r => r.Need15Sum ?? 0),
            rows.Count,
            rows.Count(r => r.Status == StockStatuses.Deficit),
            rows.Count(r => r.Status == StockStatuses.Overstock),
            rows.Count(r => r.Status == StockStatuses.Dead),
            withoutWeight,
            rows.Sum(r => r.Value ?? 0),
            rows.Count(r => r.HasStock && !r.HasPrice),
            rows.Count(r => r.HasStock && r.ApproxWeight),
            rows.Sum(r => r.OrderKg),
            rows.Sum(r => r.OrderBoxes ?? 0),
            rows.Sum(r => r.OrderSum ?? 0),
            rows.Count(r => r.OrderKg > 0));
    }
}

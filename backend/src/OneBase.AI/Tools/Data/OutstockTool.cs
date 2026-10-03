using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Sales.Stock;

namespace OneBase.AI.Tools.Data;

/// <summary>Аутсток: дни без товара у дилеров и упущенные продажи — по восстановленному назад от снимка Linko остатку.</summary>
internal sealed class GetOutstockTool(OutstockService outstock) : DataTool
{
    private static readonly (string, object) ScopeArg = ("scope", new
    {
        type = "string",
        description = "Какие товары: «top» — только ТОП-товары (так по умолчанию и на странице), «all» — все SKU, «rest» — кроме ТОПа.",
    });

    public override string Name => "get_outstock";
    public override string Title => "Аутсток: дни без товара и упущенные продажи";
    public override string Source => KnowledgeSources.SalesStock;

    public override string Description =>
        "Аутсток за месяц: сколько недопродали из-за того, что у дилера не было товара (остаток по дням восстановлен назад от снимка Linko). " +
        "Итоги и «к факту» (упущенные кг к проданным), потери по категориям, по регионам с дилером и тройкой самых дорогих дыр, по товарам, " +
        "ядро потерь (пары «товар × регион», дающие 80% упущенного), хронические пары, чья потеря — недовоз дилеру (на заводе товар был) или завод. " +
        "По умолчанию — ТОП-товары; scope=all — все SKU. По республике или региону.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg, ScopeArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var (year, month) = args.Period();
        var scope = args.Str("scope")?.Trim().ToLowerInvariant();
        var all = await outstock.GetAsync(year, month, null, scope, null, ct);
        var regionArg = args.Str("region");
        string? regionId = null;
        if (regionArg is not null)
        {
            var region = all.Regions.FirstOrDefault(r => string.Equals(r.Id, regionArg, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Name, regionArg, StringComparison.OrdinalIgnoreCase))
                ?? all.Regions.FirstOrDefault(r => r.Name.Contains(regionArg, StringComparison.OrdinalIgnoreCase))
                ?? throw new ToolArgumentException($"Региона «{regionArg}» нет. Регионы: {string.Join(", ", all.Regions.Select(r => r.Name))}.");
            regionId = region.Id;
        }

        var view = regionId is null ? all : await outstock.GetAsync(year, month, regionId, scope, null, ct);
        var everything = view.Scope == OutstockScope.All ? view : await outstock.GetAsync(year, month, regionId, OutstockScope.All, null, ct);
        var t = view.Totals;
        var scopeName = regionId is null ? "Республика" : view.Regions.First(r => r.Id == regionId).Name;
        object Pair(OutstockPair p) => new
        {
            p.Region,
            p.Product,
            p.Category,
            p.Top,
            p.ZeroDays,
            p.DealerDays,
            p.FactoryDays,
            LostKg = R(p.LostKg),
            LostSum = R(p.LostSum),
            PeriodKg = R(p.PeriodKg),
            PerDayKg = R(p.PerDayKg, 1),
            AvgPricePerKg = R(p.AvgPrice),
            p.Chronic,
            p.Core,
        };

        return Data(new
        {
            Period = new { view.Year, MonthName = MonthName(view.Month), From = view.From.ToString("yyyy-MM-dd"), To = view.To.ToString("yyyy-MM-dd"), view.Days, SnapshotDate = view.SnapshotDate.ToString("yyyy-MM-dd") },
            Scope = scopeName,
            Products = view.Scope switch
            {
                OutstockScope.All => "все SKU",
                OutstockScope.Rest => "товары вне ТОПа",
                _ => view.TopHint,
            },
            Totals = new
            {
                LostKg = R(t.LostKg),
                LostSum = R(t.LostSum),
                SoldKg = R(t.SoldKg),
                LossSharePct = Pct(t.LossShare),
                PairsSold = t.Pairs,
                PairsWithLoss = t.PairsWithLoss,
                ProductsWithLoss = t.ProductsWithLoss,
                RegionsWithLoss = t.RegionsWithLoss,
                ZeroDays = t.ZeroDays,
                CorePairs = t.CorePairs,
                CoreSum = R(t.CoreSum),
                ChronicPairs = t.Chronic,
                DealerLossSum = R(t.DealerLossSum),
                FactoryLossSum = R(t.FactoryLossSum),
                DealerDaysPct = t.ZeroDays > 0 ? Math.Round(100m * t.DealerDays / t.ZeroDays) : (decimal?)null,
                NegativeCellsPct = t.NegativeSharePct,
            },
            AllSkuTotals = view.Scope == OutstockScope.All ? null : new { LostKg = R(everything.Totals.LostKg), LostSum = R(everything.Totals.LostSum), PairsWithLoss = everything.Totals.PairsWithLoss },
            Categories = view.Categories.Select(c => new { c.Name, LostSum = R(c.LostSum), ShareOfLossPct = Pct(c.Share), LostKg = R(c.LostKg), LossSharePct = Pct(c.LossShare), c.Pairs, c.ZeroDays }),
            ByRegion = view.ByRegion.Take(12).Select(r => new
            {
                r.Name,
                r.Dealer,
                SoldKg = R(r.SoldKg),
                LostKg = R(r.LostKg),
                LostSum = R(r.LostSum),
                LossSharePct = Pct(r.LossShare),
                r.ZeroDays,
                r.Pairs,
                DealerLossSum = R(r.DealerLossSum),
                FactoryLossSum = R(r.FactoryLossSum),
                Top = r.Top.Select(p => new { p.Name, LostSum = R(p.LostSum), ShareOfRegionPct = Pct(p.Share) }),
            }),
            TopProducts = view.ByProduct.Take(15).Select(p => new { p.Name, p.Category, p.Top, SoldKg = R(p.SoldKg), ZeroDaysPct = Pct(p.ZeroShare), LostKg = R(p.LostKg), LostSum = R(p.LostSum), p.Regions }),
            Core = view.Pairs.Where(p => p.Core).Take(15).Select(Pair),
            Chronic = view.Pairs.Where(p => p.Chronic).OrderByDescending(p => p.LostSum).Take(10).Select(Pair),
            Note = "Остаток по дням восстановлен назад от снимка Linko: утро = вечер + продано − привезено; минус считается нулём. " +
                "Упущено = дней в нуле × средние продажи в день × средняя цена; «к факту» — упущенные кг к проданным. " +
                (view.FactoryKnown
                    ? "Недовоз — на заводе в тот день товар был; выпуска цехов в Linko нет, поэтому доля потерь завода — оценка снизу."
                    : "Склад завода в Linko не найден — все потери отнесены к дилерам."),
        }, new DataSource(regionId is null ? "OneBase → Продажи → Аутсток" : $"OneBase → Продажи → Аутсток → {scopeName}",
            $"{MonthName(view.Month)} {view.Year}, снимок остатков {view.SnapshotDate:dd.MM.yyyy}",
            $"/sales/outstock?{Query(view.Year, view.Month)}&scope={view.Scope}{(regionId is null ? string.Empty : $"&region={regionId}")}"));
    }
}

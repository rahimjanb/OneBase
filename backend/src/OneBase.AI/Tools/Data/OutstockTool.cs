using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Sales.Stock;

namespace OneBase.AI.Tools.Data;

/// <summary>Аутсток: дни без товара у дилеров и упущенные продажи — по восстановленному назад от снимка Linko остатку.</summary>
internal sealed class GetOutstockTool(OutstockService outstock) : DataTool
{
    public override string Name => "get_outstock";
    public override string Title => "Аутсток: дни без товара и упущенные продажи";
    public override string Source => KnowledgeSources.SalesStock;

    public override string Description =>
        "Аутсток за месяц: пары «товар × регион», у которых дилер стоял без товара (остаток по дням восстановлен назад от снимка Linko), " +
        "дни в нуле, упущенные кг и сумы, ядро потерь (пары, дающие 80% упущенного), хронические пары (в нуле полмесяца и дольше), " +
        "чья потеря — недовоз дилеру (на заводе товар был) или завод. По республике или региону.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg, RegionArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var (year, month) = args.Period();
        var all = await outstock.GetAsync(year, month, null, ct);
        var regionArg = args.Str("region");
        string? regionId = null;
        if (regionArg is not null)
        {
            var region = all.Regions.FirstOrDefault(r => string.Equals(r.Id, regionArg, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Name, regionArg, StringComparison.OrdinalIgnoreCase))
                ?? all.Regions.FirstOrDefault(r => r.Name.Contains(regionArg, StringComparison.OrdinalIgnoreCase))
                ?? throw new ToolArgumentException($"Региона «{regionArg}» нет. Регионы: {string.Join(", ", all.Regions.Select(r => r.Name))}.");
            regionId = region.Id;
        }

        var view = regionId is null ? all : await outstock.GetAsync(year, month, regionId, ct);
        var t = view.Totals;
        object Pair(OutstockPair p) => new
        {
            p.Region,
            p.Product,
            p.Category,
            p.ZeroDays,
            DealerDays = p.DealerDays,
            FactoryDays = p.FactoryDays,
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
            Scope = regionId is null ? "Республика" : view.Regions.First(r => r.Id == regionId).Name,
            Totals = new
            {
                LostKg = R(t.LostKg),
                LostSum = R(t.LostSum),
                PairsSold = t.Pairs,
                PairsWithLoss = t.PairsWithLoss,
                CorePairs = t.CorePairs,
                CoreSum = R(t.CoreSum),
                ChronicPairs = t.Chronic,
                DealerLossSum = R(t.DealerLossSum),
                FactoryLossSum = R(t.FactoryLossSum),
                NegativeCellsPct = t.NegativeSharePct,
            },
            Core = view.Pairs.Where(p => p.Core).Take(15).Select(Pair),
            Chronic = view.Pairs.Where(p => p.Chronic).OrderByDescending(p => p.LostSum).Take(10).Select(Pair),
            ByRegion = view.ByRegion.Take(12).Select(g => new { g.Name, g.Pairs, g.CorePairs, g.Chronic, LostKg = R(g.LostKg), LostSum = R(g.LostSum), DealerLossSum = R(g.DealerLossSum), FactoryLossSum = R(g.FactoryLossSum) }),
            Note = "Остаток по дням восстановлен назад от снимка Linko: утро = вечер + продано − привезено; минус считается нулём. Упущено = дней в нуле × средние продажи в день × средняя цена. " +
                (view.FactoryKnown
                    ? "Недовоз — на заводе в тот день товар был; выпуска цехов в Linko нет, поэтому доля потерь завода — оценка снизу."
                    : "Склад завода в Linko не найден — все потери отнесены к дилерам."),
        }, new DataSource(regionId is null ? "OneBase → Продажи → Аутсток" : $"OneBase → Продажи → Аутсток → {view.Regions.First(r => r.Id == regionId).Name}",
            $"{MonthName(view.Month)} {view.Year}, снимок остатков {view.SnapshotDate:dd.MM.yyyy}",
            $"/sales/outstock?{Query(view.Year, view.Month)}{(regionId is null ? string.Empty : $"&region={regionId}")}"));
    }
}

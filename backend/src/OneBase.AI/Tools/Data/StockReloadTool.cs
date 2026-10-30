using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Stock;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Нет в наличии (OOS) и дозагрузка по дилерам: пары «склад дилера (регион) × SKU» категории или товара — остаток, свободный остаток (без
/// брони), скорость продаж, дни покрытия, дни в нуле, статус, рекомендуемый заказ. Расчёт — тот же, что «Рек. остаток» (StockService):
/// скорость — вторичка за Sales:StockVelocityDays дней с поправкой на дни в нуле прошлого закрытого месяца, заказ — до запаса на
/// StockMath.OrderCoverDays дней вверх до коробки. Своих формул инструмент не вводит.
/// </summary>
internal sealed class GetStockReloadTool(StockService stock, IAppDbContext db, SalesOptions options) : DataTool
{
    internal const int DefaultLimit = 40;
    internal const int MaxLimit = 60;

    internal static readonly IReadOnlyList<ArgChoice<string>> Filters =
    [
        new("order", "order", "есть заказ", "дозагрузка", "дозаказ", "нужен заказ"),
        new("oos", "oos", "нет в наличии", "нет остатка", "аутсток", "ноль"),
        new("deficit", "deficit", "дефицит", "мало"),
        new("all", "all", "все"),
    ];

    public override string Name => "get_stock_reload";
    public override string Title => "Нет в наличии и дозагрузка по дилерам";
    public override string Source => KnowledgeSources.SalesStock;

    public override string Description =>
        "OOS (нет в наличии) и дозагрузка по дилерам: для категории («помадка», «могул»), товара или артикула — каждая пара «склад дилера (регион) × SKU»: " +
        "остаток кг и коробки, свободный остаток (без брони), продажи кг в день, дни покрытия, дни в нуле за прошлый закрытый месяц, статус " +
        "(нет в наличии — остатка нет при продажах; дефицит — меньше 15 дней; норма; затоварка; не продаётся), рекомендуемый заказ дилера (кг, коробки, сумма). " +
        "Расчёт тот же, что страница «Рек. остаток»: скорость за 87 дней с поправкой на дни без товара, заказ — до запаса на 15 дней вверх до коробки. " +
        "Фильтр filter: order — где нужен заказ (по умолчанию), oos — нет в наличии, deficit — дефицит, all — все пары.";

    public override string UsageHint =>
        "«OOS / нет в наличии / сколько дозагрузить / что заказать дилеру» по категории или SKU — get_stock_reload (пары дилер × SKU с рекомендуемым заказом). " +
        "Сводка складов без разбивки — get_inventory; упущенные продажи из-за отсутствия товара за месяц — get_outstock.";

    public override JsonElement InputSchema { get; } = Schema(
        ("category", new { type = "string", description = "Категория отчёта: «помадка», «могул», «шоколад». Без неё — все категории." }),
        ("product", new { type = "string", description = "Товар или артикул: «ARBUZ», «480». Вместе с category — внутри категории." }),
        RegionArg,
        ("filter", new { type = "string", @enum = WorkFieldText.Codes(Filters), description = "order — где нужен заказ (по умолчанию), oos — нет в наличии, deficit — меньше 15 дней запаса, all — все пары." }),
        ("limit", new { type = "integer", description = $"Сколько пар в списке: по умолчанию {DefaultLimit}, до {MaxLimit}." }));

    private static readonly Dictionary<string, string> StatusText = new()
    {
        [StockStatuses.Deficit] = "дефицит (меньше 15 дней)",
        [StockStatuses.Overstock] = "затоварка (больше 30 дней)",
        [StockStatuses.Dead] = "не продаётся (остаток есть, продаж нет)",
        [StockStatuses.Ok] = "норма",
        [StockStatuses.Empty] = "нет ни остатка, ни продаж",
        [StockStatuses.Unknown] = "вес единицы неизвестен — запас не посчитать",
        [StockStatuses.None] = "—",
    };

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var limit = args.Int("limit", 1, MaxLimit) ?? DefaultLimit;
        var filter = WorkFieldText.Pick(args.Str("filter"), "filter", "order", Filters);

        var all = await stock.QueryAsync(new StockQuery(), ct);
        var categories = Categories(all.Categories, args.Str("category"));
        var view = await stock.QueryAsync(new StockQuery(Query: args.Str("product"), Categories: categories), ct);

        var regions = view.Regions;
        if (args.Str("region") is { } regionArg)
        {
            regions = regions.Where(r => string.Equals(r.Id, regionArg, StringComparison.OrdinalIgnoreCase) || r.Name.Contains(regionArg, StringComparison.OrdinalIgnoreCase)).ToList();
            if (regions.Count == 0)
            {
                throw new ToolArgumentException($"Склада региона «{regionArg}» нет. Склады регионов: {string.Join(", ", view.Regions.Select(r => r.Name))}.");
            }
        }

        if (view.Items.Count == 0)
        {
            throw new ToolArgumentException(args.Str("product") is { } p
                ? $"Товара «{p}»{(categories is null ? "" : " в этой категории")} в остатках нет. Найти товар — find_products."
                : "В остатках нет товаров этой категории.");
        }

        var dealers = await DealersAsync(regions.Select(r => r.Id), ct);
        var pairs = view.Items
            .SelectMany(i => regions.Where(r => i.Regions.ContainsKey(r.Id)).Select(r => (Item: i, Region: r, Cell: i.Regions[r.Id])))
            .ToList();
        bool Oos((StockItem Item, StockRegion Region, StockCell Cell) x) => x.Cell.Kg is <= 0 && x.Cell.KgPerDay > 0;
        var picked = pairs.Where(x => filter switch
            {
                "oos" => Oos(x),
                "deficit" => x.Cell.Status == StockStatuses.Deficit,
                "all" => true,
                _ => x.Cell.OrderKg > 0 || Oos(x),
            })
            .OrderByDescending(Oos)
            .ThenBy(x => x.Cell.DaysOfCover ?? decimal.MaxValue)
            .ThenByDescending(x => x.Cell.OrderKg)
            .ToList();
        var shown = picked.TakeTop(args, limit, "rows").ToList();

        return Data(new
        {
            SnapshotAt = view.SyncedAt?.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM.yyyy HH:mm"),
            VelocityPeriod = $"{view.VelocityFrom:dd.MM.yyyy} – {view.VelocityTo:dd.MM.yyyy} ({view.VelocityDays} дн.)",
            Categories = categories ?? (IReadOnlyList<string>)["все"],
            Scope = regions.Count == view.Regions.Count ? "все склады дилеров" : string.Join(", ", regions.Select(r => r.Name)),
            Filter = filter,
            Summary = new
            {
                Sku = view.Items.Count,
                Pairs = pairs.Count,
                OutOfStock = pairs.Count(Oos),
                Deficit = pairs.Count(x => x.Cell.Status == StockStatuses.Deficit),
                NeedOrder = pairs.Count(x => x.Cell.OrderKg > 0),
                OrderKg = R(pairs.Sum(x => x.Cell.OrderKg)),
                OrderBoxes = R(pairs.Sum(x => x.Cell.OrderBoxes ?? 0)),
                OrderSum = R(pairs.Sum(x => x.Cell.OrderSum ?? 0)),
                ByRegion = regions
                    .Select(r => (Region: r, Cells: pairs.Where(x => x.Region.Id == r.Id).ToList()))
                    .Where(x => x.Cells.Any(c => c.Cell.OrderKg > 0 || Oos(c)))
                    .Select(x => new
                    {
                        Region = x.Region.Name,
                        Dealer = dealers.GetValueOrDefault(x.Region.Id),
                        OutOfStock = x.Cells.Count(Oos),
                        OrderKg = R(x.Cells.Sum(c => c.Cell.OrderKg)),
                        OrderBoxes = R(x.Cells.Sum(c => c.Cell.OrderBoxes ?? 0)),
                    })
                    .OrderByDescending(x => x.OrderKg),
            },
            Total = picked.Count,
            Shown = shown.Count,
            Rows = shown.Select(x => new
            {
                Region = x.Region.Name,
                Dealer = dealers.GetValueOrDefault(x.Region.Id),
                Sku = x.Item.Name,
                x.Item.Code,
                x.Item.Category,
                StockKg = R(x.Cell.Kg, 1),
                StockBoxes = R(x.Cell.Boxes, 1),
                FreeKg = x.Cell.ReservedKg > 0 ? R(x.Cell.FreeKg, 1) : null,
                ReservedKg = x.Cell.ReservedKg > 0 ? R(x.Cell.ReservedKg, 1) : (decimal?)null,
                KgPerDay = R(x.Cell.KgPerDay, 2),
                DaysOfCover = R(x.Cell.DaysOfCover, 1),
                ZeroDaysLastMonth = x.Cell.ZeroDays,
                OutOfStock = Oos(x),
                Status = StatusText.GetValueOrDefault(x.Cell.Status, x.Cell.Status),
                OrderKg = R(x.Cell.OrderKg, 1),
                OrderBoxes = R(x.Cell.OrderBoxes, 0),
                OrderSum = R(x.Cell.OrderSum),
            }),
            Note = $"Нет в наличии (OutOfStock) — остатка на складе нет, а продажи за период скорости были. Рекомендуемый заказ — до запаса на {StockMath.OrderCoverDays} дней " +
                   "продаж, вверх до целой коробки; скорость — с поправкой на дни в нуле (ZeroDaysLastMonth — дни без товара в прошлом закрытом месяце). " +
                   "Свободный остаток — без брони заказов. Остатки — снимок Linko на SnapshotAt.",
        }, new DataSource("OneBase → Продажи → Рек. остаток", view.SyncedAt is { } at ? $"на {at.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy HH:mm}" : null, "/sales/stock"));
    }

    /// <summary>Категории отчёта по слову: название категории, название из планов («могул» — помадка); нет совпадения — ошибка со списком.</summary>
    private IReadOnlyList<string>? Categories(IReadOnlyList<string> known, string? value)
    {
        if (value is null)
        {
            return null;
        }

        var matched = known.Where(c => NameMatch.Score(c, value) > 0
                                       || (options.PlanIndicatorCategories.TryGetValue(c, out var words) && words.Any(w => NameMatch.Score(w, value) > 0)))
            .ToList();
        return matched.Count > 0
            ? matched
            : throw new ToolArgumentException($"Категории «{value}» в остатках нет. Категории: {string.Join(", ", known)}. Товар или бренд — в product.");
    }

    /// <summary>Дилер склада: регион склада — регион OneBase (id), его дилер из «Настроек продаж».</summary>
    private async Task<Dictionary<string, string>> DealersAsync(IEnumerable<string> regionIds, CancellationToken ct)
    {
        var ids = regionIds.Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
        return await db.SalesRegions.AsNoTracking().Where(r => ids.Contains(r.Id) && r.DealerName != null)
            .ToDictionaryAsync(r => r.Id.ToString(), r => r.DealerName!, ct);
    }
}

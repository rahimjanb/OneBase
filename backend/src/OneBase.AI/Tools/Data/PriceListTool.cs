using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Прайс-листы Linko (linko.PriceLists, PriceListItems): цена товара в каждом прайс-листе — дилерском, магазинном и других, как их ведёт Linko.
/// Это цены прайса, а не фактические цены продаж. Справочник читается из нашей копии Linko, месяц продаж не загружается.
/// </summary>
internal sealed class GetPricesTool(IAppDbContext db, SalesOptions options) : DataTool
{
    internal const int DefaultLimit = 20;
    internal const int MaxLimit = 60;

    public override string Name => "get_prices";
    public override string Title => "Прайс-листы Linko";
    public override string Source => KnowledgeSources.SalesPrimary;

    public override string Description =>
        "Цены товаров по прайс-листам Linko (дилерский, магазинный и другие — как их ведёт Linko): по каждому найденному товару — цена в каждом прайс-листе " +
        "и валюта; без товара — список прайс-листов с числом товаров. Поиск товара — product (название, бренд или артикул: «ARBUZ», «Yamelly», «480»), " +
        "category (категория или тип Linko: «помадка»), price_list (часть названия прайс-листа). Это цены прайса, а не фактические: выручка и цена за кг " +
        "по продажам — get_sku_sales и find_products.";

    public override JsonElement InputSchema { get; } = Schema(
        ("product", new { type = "string", description = "Товар, бренд или артикул: «ARBUZ», «Yamelly», «480»." }),
        ("category", new { type = "string", description = "Категория отчёта или тип Linko: «помадка», «шоколад», «Помадка 1 кг»." }),
        ("price_list", new { type = "string", description = "Прайс-лист: часть названия. Без него — все прайс-листы." }),
        ("limit", new { type = "integer", description = $"Сколько товаров: по умолчанию {DefaultLimit}, до {MaxLimit}." }));

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var limit = args.Int("limit", 1, MaxLimit) ?? DefaultLimit;
        var productQuery = args.Str("product");
        var categoryQuery = args.Str("category");
        var listQuery = args.Str("price_list");

        var lists = await db.LinkoPriceLists.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code }).ToListAsync(ct);
        var currencies = await db.LinkoCurrencies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var pickedLists = listQuery is null ? lists : lists.Where(l => NameMatch.Score(l.Name, listQuery) > 0 || (l.Code is not null && NameMatch.Score(l.Code, listQuery) > 0)).ToList();
        if (pickedLists.Count == 0)
        {
            throw new ToolArgumentException($"Прайс-листа «{listQuery}» нет. Прайс-листы: {string.Join(", ", lists.Select(l => l.Name).Order())}.");
        }

        var listIds = pickedLists.Select(l => l.Id).ToList();
        var source = new DataSource("Linko · Прайс-листы", null, null);
        if (productQuery is null && categoryQuery is null)
        {
            var counts = await db.LinkoPriceListItems.AsNoTracking()
                .Where(i => i.PriceListId != null && listIds.Contains(i.PriceListId.Value) && i.ProductId != null)
                .GroupBy(i => i.PriceListId!.Value)
                .Select(g => new { Id = g.Key, Products = g.Select(i => i.ProductId).Distinct().Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Products, ct);
            return Data(new
            {
                PriceLists = pickedLists.OrderBy(l => l.Name).Select(l => new { l.Name, l.Code, Products = counts.GetValueOrDefault(l.Id) }),
                Note = "Цены конкретного товара — с product или category.",
            }, source);
        }

        // Категории — как в отчёте продаж: тип Linko → категория отчёта (подтипы — в категории родителя).
        var types = await LinkoTypeTree.LoadAsync(db, ct);
        var cats = SalesCategories.Build(options.Categories, types);
        var products = (await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToListAsync(ct))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Code,
                Category = cats.NameOf(cats.GroupOf(p.TypeId)),
                Type = p.TypeId is { } t ? types.Names.GetValueOrDefault(t) : null,
            })
            .ToList();
        if (categoryQuery is not null)
        {
            products = products.Where(p => NameMatch.Score(p.Category, categoryQuery) > 0 || (p.Type is not null && NameMatch.Score(p.Type, categoryQuery) > 0)
                                           || options.PlanIndicatorCategories.TryGetValue(p.Category, out var words) && words.Any(w => NameMatch.Score(w, categoryQuery) > 0))
                .ToList();
        }

        if (productQuery is not null)
        {
            var scored = products
                .Select(p => (Item: p, Score: string.Equals(p.Code, productQuery, StringComparison.OrdinalIgnoreCase) ? 4
                    : Math.Max(NameMatch.Score(p.Name, productQuery), p.Code is null ? 0 : NameMatch.Score(p.Code, productQuery))))
                .Where(x => x.Score > 0)
                .ToList();
            var best = scored.Count == 0 ? 0 : scored.Max(x => x.Score);
            products = scored.Where(x => x.Score == best).Select(x => x.Item).ToList();
        }

        if (products.Count == 0)
        {
            throw new ToolArgumentException("Таких товаров в справочнике Linko нет. Найти товар по части названия — find_products.");
        }

        var ids = products.Select(p => p.Id).ToList();
        var items = await db.LinkoPriceListItems.AsNoTracking()
            .Where(i => i.ProductId != null && ids.Contains(i.ProductId.Value) && i.PriceListId != null && listIds.Contains(i.PriceListId.Value))
            .Select(i => new { Product = i.ProductId!.Value, List = i.PriceListId!.Value, i.Price, i.CurrencyId, i.Tm })
            .ToListAsync(ct);
        // В прайсе бывают повторы товара — берётся последняя запись (по времени изменения Linko).
        var prices = items.GroupBy(i => (i.Product, i.List)).ToDictionary(g => g.Key, g => g.MaxBy(i => i.Tm)!);
        var listName = pickedLists.ToDictionary(l => l.Id, l => l.Name);
        var withPrice = products.Where(p => pickedLists.Any(l => prices.ContainsKey((p.Id, l.Id)))).ToList();
        var shown = withPrice.OrderBy(p => p.Category).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Take(args.Cap(limit)).ToList();

        return Data(new
        {
            ProductsFound = products.Count,
            WithPrice = withPrice.Count,
            Shown = shown.Count,
            Products = shown.Select(p => new
            {
                p.Name,
                p.Code,
                p.Category,
                Prices = pickedLists.Where(l => prices.ContainsKey((p.Id, l.Id)))
                    .Select(l => prices[(p.Id, l.Id)])
                    .Select(x => new
                    {
                        PriceList = listName[x.List],
                        Price = R(x.Price, 2),
                        Currency = x.CurrencyId is { } c ? currencies.GetValueOrDefault(c) : null,
                    })
                    .OrderBy(x => x.PriceList),
            }),
            NoPrice = products.Count - withPrice.Count,
            Note = "Цены из прайс-листов Linko на момент последней синхронизации; цена за единицу товара (коробку или штуку — как в Linko). " +
                   "Товары без цены в выбранных прайс-листах не показаны (NoPrice).",
        }, source);
    }
}

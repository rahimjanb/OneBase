using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Primary;
using OneBase.Application.Sales.Stock;
using OneBase.Application.Security;

namespace OneBase.AI.Tools.Data;

internal sealed class GetInventoryTool(StockService stock) : DataTool
{
    public override string Name => "get_inventory";
    public override string Title => "Рекомендуемый остаток на складах";
    public override string Source => KnowledgeSources.SalesStock;

    public override string Description =>
        "Рекомендуемый остаток: запас на складах регионов (кг, коробки, стоимость по входной цене дилера) и скорость продаж за 90 дней: дни покрытия, " +
        "запас на 15 и 30 дней, дефицит (меньше 15 дней продаж), затоварка (больше 30 дней), «не продаётся» (остаток есть, продаж нет); " +
        "отдельно — склад завода (его запас меряется скоростью всей страны). Можно по одному региону.";

    public override JsonElement InputSchema { get; } = Schema(RegionArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var all = await stock.GetAsync(null, ct);
        var regionArg = args.Str("region");
        StockRegion? region = null;
        if (regionArg is not null)
        {
            region = all.Regions.FirstOrDefault(r => string.Equals(r.Id, regionArg, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Name, regionArg, StringComparison.OrdinalIgnoreCase))
                ?? all.Regions.FirstOrDefault(r => r.Name.Contains(regionArg, StringComparison.OrdinalIgnoreCase))
                ?? throw new ToolArgumentException($"Склада региона «{regionArg}» нет. Склады регионов: {string.Join(", ", all.Regions.Select(r => r.Name))}.");
        }

        var view = region is null ? all : await stock.GetAsync(region.Id, ct);
        var known = view.Items.Where(i => i.Status != StockStatuses.Unknown).ToList();
        object Item(StockItem i) => new
        {
            i.Name,
            i.Category,
            Kg = R(i.Kg),
            Boxes = R(i.Boxes),
            KgPerDay = R(i.KgPerDay, 1),
            DaysOfCover = R(i.DaysOfCover, 1),
            NeedFor15DaysKg = R(i.Need15Kg),
            NeedFor30DaysKg = R(i.Need30Kg),
            PricePerUnit = R(i.Price),
            ValueSum = R(i.ValueSum),
        };

        return Data(new
        {
            SyncedAt = view.SyncedAt,
            Scope = region?.Name ?? "Все склады регионов",
            VelocityPeriod = $"{view.VelocityFrom:dd.MM.yyyy} – {view.VelocityTo:dd.MM.yyyy} ({view.VelocityDays} дн.)",
            Totals = Totals(view.Totals),
            Factory = view.FactoryTotals is { } f ? Totals(f) : null,
            Deficit = known.Where(i => i.Status == StockStatuses.Deficit).OrderBy(i => i.DaysOfCover).Take(15).Select(Item),
            Overstock = known.Where(i => i.Status == StockStatuses.Overstock).OrderByDescending(i => i.DaysOfCover).Take(10).Select(Item),
            NotSelling = known.Where(i => i.Status == StockStatuses.Dead).OrderByDescending(i => i.Kg).Take(10).Select(Item),
            ItemsWithoutWeight = view.Totals.WithoutWeight,
            ItemsWithoutPrice = view.Totals.WithoutPrice,
            PriceList = view.PriceList,
            Note = "Остатки Linko — в штуках; кг и коробки пересчитаны по весу штуки и коробки. Дни покрытия = кг ÷ продажи кг в день за период скорости. " +
                "Стоимость = штуки × входная цена дилера за единицу учёта (без пересчёта через вес).",
        }, new DataSource(region is null ? "OneBase → Продажи → Рек. остаток" : $"OneBase → Продажи → Рек. остаток → {region.Name}",
            view.SyncedAt is { } at ? $"на {at.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy HH:mm}" : null,
            region is null ? "/sales/stock" : $"/sales/stock?region={region.Id}"));
    }

    private static object Totals(StockTotals t) => new
    {
        Kg = R(t.Kg),
        Boxes = R(t.Boxes),
        KgPerDay = R(t.KgPerDay, 1),
        DaysOfCover = R(t.DaysOfCover, 1),
        DeficitSku = t.Deficit,
        OverstockSku = t.Overstock,
        NotSellingSku = t.Dead,
        ValueSum = R(t.ValueSum),
        SkuWithoutPrice = t.WithoutPrice,
    };
}

internal sealed class GetPrimaryShipmentsTool(PrimaryService primary) : DataTool
{
    public override string Name => "get_primary_shipments";
    public override string Title => "Первичка: отгрузки завода дилерам";
    public override string Source => KnowledgeSources.SalesPrimary;

    public override string Description =>
        "Первичка — отгрузки завода дилерам и точкам завода (базары, сети, фирменный магазин): за месяц (кг, коробки, сумма по цене дилера и по цене продажи дилера), " +
        "прогноз по дате данных, план первички (если загружен в OneBase), по месяцам года, с начала года, по контрагентам и категориям, возвраты (уже вычтены). " +
        "Это спрос дилеров, а не выпуск производства.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var (year, month) = args.Period();
        var view = await primary.GetAsync(year, month, ct);
        object Amounts(PrimaryAmounts a) => new { Kg = R(a.Kg), Boxes = R(a.Boxes), FactorySum = R(a.SumFactory), DealerSum = R(a.SumDealer) };
        var period = $"{MonthName(view.Month)} {view.Year}" + (view.DataThrough is { } d ? $", данные по {d:dd.MM.yyyy}" : string.Empty);

        return Data(new
        {
            view.Year,
            Month = MonthName(view.Month),
            DataThrough = view.DataThrough?.ToString("yyyy-MM-dd"),
            view.WorkedDays,
            view.DaysInMonth,
            MonthTotal = Amounts(view.MonthTotal),
            PlanMonthKg = R(view.PlanMonthKg),
            ExecutionPct = Pct(view.MonthExecution),
            ForecastKg = R(view.ForecastKg),
            Transfers = view.MonthTransfers,
            ByMonth = view.MonthsWithData.Select(m => new { Month = MonthName(m), Kg = R(view.Months[m - 1].Kg), FactorySum = R(view.Months[m - 1].SumFactory), PlanKg = R(view.PlanMonths[m - 1]) }),
            YearToDate = Amounts(view.Ytd),
            PlanYtdKg = R(view.PlanYtdKg),
            view.YtdArticles,
            ReturnsYtdKg = R(view.YtdReturnsKg),
            Dealers = view.Dealers.OrderByDescending(r => r.Month.Kg).Select(r => new
            {
                r.Name,
                Detail = r.Sub, // регион дилера или тип точки и филиал прямого клиента
                Group = r.Group == PrimaryGroups.Direct ? "прямой клиент завода" : "дилер",
                Kg = R(r.Month.Kg),
                FactorySum = R(r.Month.SumFactory),
                PlanKg = R(r.PlanMonthKg),
                ExecutionPct = Pct(r.MonthExecution),
            }),
            Categories = view.Categories.OrderByDescending(r => r.Month.Kg).Select(r => new { r.Name, Kg = R(r.Month.Kg), FactorySum = R(r.Month.SumFactory) }),
            Export = new { Kg = R(view.Export.Kg), FactorySum = R(view.Export.SumFactory), view.Export.Counterparties, Note = "с начала года" },
            Note = "FactorySum — сумма по цене дилера (цена перемещения или заказа: по ней дилер берёт товар у завода); DealerSum — по прайсу продажи дилера. " +
                "Цены завода в Linko нет. Возвраты вычтены: суммы — нетто.",
        }, new DataSource("OneBase → Продажи → Первичка", period, $"/sales/primary/republic?{Query(view.Year, view.Month)}"));
    }
}

internal sealed class GetPaymentsTool(IAppDbContext db) : DataTool
{
    public override string Name => "get_payments";
    public override string Title => "Оплаты торговых точек";
    public override string Source => KnowledgeSources.FinancePayments;
    public override string RequiredPermission => Permissions.FinanceRead;

    public override string Description =>
        "Оплаты торговых точек из Linko по месяцам: сумма и число оплат, наличные и банк, принятые и не принятые; за выбранный месяц — по филиалам. " +
        "Это поступления от клиентов, а не расходы и не прибыль.";

    public override JsonElement InputSchema { get; } = Schema(YearArg, MonthArg);

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var payments = db.LinkoPayments.AsNoTracking().Where(p => !p.IsDelete && p.CreatedDate != null);
        var byMonth = await payments
            .GroupBy(p => new { p.CreatedDate!.Value.Year, p.CreatedDate!.Value.Month, p.PaymentType, p.Status, p.CurrencyName })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.PaymentType, g.Key.Status, g.Key.CurrencyName, Sum = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(ct);
        if (byMonth.Count == 0)
        {
            return Data(new { Note = "Оплат в OneBase нет — синхронизация оплат из Linko ещё не выполнялась." });
        }

        var (year, month) = args.Period();
        var last = byMonth.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).First();
        var y = year ?? last.Year;
        var m = month ?? last.Month;
        if (!byMonth.Any(x => x.Year == y && x.Month == m))
        {
            var first = byMonth.OrderBy(x => x.Year).ThenBy(x => x.Month).First();
            throw new ToolArgumentException(
                $"Оплат за {MonthName(m)} {y} нет. Оплаты есть с {MonthName(first.Month)} {first.Year} по {MonthName(last.Month)} {last.Year}.");
        }

        var from = new DateOnly(y, m, 1);
        var to = from.AddMonths(1);
        var branches = await payments
            .Where(p => p.CreatedDate >= from && p.CreatedDate < to && p.Status == "accepted")
            .Join(db.LinkoMarkets, p => p.MarketId, mk => mk.Id, (p, mk) => new { p.Amount, mk.BranchName })
            .GroupBy(x => x.BranchName)
            .Select(g => new { Branch = g.Key, Sum = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync(ct);
        var dataThrough = await payments.Where(p => p.CreatedDate >= from && p.CreatedDate < to).MaxAsync(p => p.CreatedDate, ct);

        return Data(new
        {
            Year = y,
            Month = MonthName(m),
            DataThrough = dataThrough?.ToString("yyyy-MM-dd"),
            Months = byMonth
                .GroupBy(x => new { x.Year, x.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    g.Key.Year,
                    Month = MonthName(g.Key.Month),
                    AcceptedSum = R(g.Where(x => x.Status == "accepted").Sum(x => x.Sum)),
                    NotAcceptedSum = R(g.Where(x => x.Status != "accepted").Sum(x => x.Sum)),
                    CashSum = R(g.Where(x => x.PaymentType == "cash").Sum(x => x.Sum)),
                    BankSum = R(g.Where(x => x.PaymentType == "bank").Sum(x => x.Sum)),
                    Payments = g.Sum(x => x.Count),
                    Currencies = g.Select(x => x.CurrencyName).Distinct(),
                }),
            AcceptedByBranch = branches.OrderByDescending(b => b.Sum).Select(b => new { Branch = b.Branch ?? "без филиала", Sum = R(b.Sum), Payments = b.Count }),
            Note = "Суммы — в валюте оплаты (как в Linko). Оплаты есть не за все месяцы: загружаются только за окно синхронизации.",
        }, new DataSource("Linko → Оплаты (зеркало OneBase)", $"{MonthName(m)} {y}"));
    }
}

internal sealed class GetSuppliersTool(IAppDbContext db) : DataTool
{
    public override string Name => "get_suppliers";
    public override string Title => "Поставщики";
    public override string Source => KnowledgeSources.SupplyProviders;

    public override string Description =>
        "Справочник поставщиков из Linko (только названия). Заказов поставщикам, закупочных цен и сроков поставки в OneBase нет.";

    public override JsonElement InputSchema { get; } = Schema();

    protected override async Task<ToolResult> RunAsync(ToolArgs args, CancellationToken ct)
    {
        var names = await db.LinkoProviders.AsNoTracking().OrderBy(p => p.Name).Select(p => p.Name).ToListAsync(ct);
        return Data(new
        {
            Count = names.Count,
            Suppliers = names,
            Note = "Кроме названий, данных о поставщиках (закупки, цены, сроки, качество) в OneBase нет.",
        }, new DataSource("Linko → Поставщики (зеркало OneBase)"));
    }
}

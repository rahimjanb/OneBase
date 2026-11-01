using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Цены закупок за период: фильтры категории, поставщика и поиск по названию позиции.</summary>
public sealed record ProcurementPriceQuery(DateOnly From, DateOnly To, Guid? CategoryId = null, Guid? SupplierId = null, string? Search = null);

/// <summary>Покупка: дата утверждения заказа, заказ, поставщик, количество, цена за единицу с НДС, срочная заявка, заказ из коммерческого предложения.</summary>
public sealed record ProcurementPricePoint(DateOnly Date, string OrderNumber, string SupplierName, decimal Quantity, decimal Price, bool Urgent, bool FromOffer);

/// <summary>
/// Цена позиции за период. База сравнения — последняя покупка до периода (за год), а если её нет — первая покупка периода. ChangePercent —
/// последняя цена к базе; ExtraCost — Σ (цена − база) × количество по покупкам после базы (минус — экономия); Overpay — Σ (цена − лучшая
/// цена периода) × количество; SupplierChanged — последняя покупка у другого поставщика, чем база.
/// </summary>
public sealed record ProcurementPriceItem(
    string Name,
    string UnitCode,
    string CategoryName,
    int Purchases,
    decimal Quantity,
    decimal Amount,
    decimal? BaselinePrice,
    DateOnly? BaselineDate,
    bool BaselineBeforePeriod,
    string? BaselineSupplier,
    decimal FirstPrice,
    decimal LastPrice,
    decimal MinPrice,
    decimal MaxPrice,
    decimal AvgPrice,
    decimal? ChangePercent,
    decimal ExtraCost,
    decimal Overpay,
    string CheapestSupplier,
    IReadOnlyList<string> Suppliers,
    bool SupplierChanged,
    int UrgentPurchases,
    int OfferPurchases,
    IReadOnlyList<ProcurementPricePoint> Points);

public sealed record ProcurementPriceReport(DateOnly From, DateOnly To, int Lines, decimal Amount, IReadOnlyList<ProcurementPriceItem> Items);

/// <summary>Поставщик с закупками за период: рейтинг (за всё время) и его части, просрочки, опоздания, претензии и почему он слабый.</summary>
public sealed record ProcurementSupplierEfficiency(
    Guid Id,
    string Name,
    int Orders,
    decimal Total,
    int? Rating,
    decimal? OnTime,
    decimal? Quality,
    decimal? Prices,
    decimal? Completeness,
    int Overdue,
    int Late,
    int Finished,
    int ClaimLines,
    IReadOnlyList<string> Weak);

/// <summary>Потребности одной категории из нескольких утверждённых заявок, ещё не заказанные: их можно заказать одним заказом.</summary>
public sealed record ProcurementConsolidation(string CategoryName, int Requests, int Lines, decimal? EstimatedTotal, IReadOnlyList<string> RequestNumbers, IReadOnlyList<string> Departments);

/// <summary>Расходы на закупки за период (позиции утверждённых заказов с НДС, без скидки и доставки) и тот же по длине период до него.</summary>
public sealed record ProcurementExpenses(
    DateOnly From,
    DateOnly To,
    decimal Total,
    int Orders,
    int Lines,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    decimal PreviousTotal,
    IReadOnlyList<ProcurementAmount> ByDepartment,
    IReadOnlyList<ProcurementAmount> ByCategory,
    IReadOnlyList<ProcurementAmount> BySupplier,
    IReadOnlyList<ProcurementAmount> ByMonth);

/// <summary>
/// Аналитика закупок для AI и рекомендаций — только чтение: история закупочных цен (база, рост, переплата против лучшей цены периода,
/// повторные закупки), расходы по разрезам, эффективность поставщиков и потребности, которые можно заказать вместе. Видят снабжение и
/// директор — как отчёты. Цена — за единицу с НДС, дата — утверждение заказа (как в «Обзоре» и отчётах снабжения).
/// </summary>
public sealed class ProcurementInsightService(
    IAppDbContext db,
    ProcurementAnalyticsService analytics,
    ProcurementSupplierService suppliers,
    ProcurementOrderService orders)
{
    /// <summary>Сколько дней до начала периода искать прошлую цену — базу сравнения.</summary>
    public const int BaselineDays = 365;

    /// <summary>Покупок за период, с которых позиция — «повторная закупка».</summary>
    public const int RepeatPurchases = 3;

    private static void RequireRead(ProcurementContext ctx)
    {
        if (!ctx.ReadsAll && !ctx.IsDirector)
        {
            throw new FieldForbiddenException("Аналитику закупок видят снабжение и директор.");
        }
    }

    public async Task<ProcurementPriceReport> PricesAsync(ProcurementContext ctx, ProcurementPriceQuery q, CancellationToken ct)
    {
        RequireRead(ctx);
        var f = ProcurementFilter.Of(q.From.AddDays(-BaselineDays), q.To, category: q.CategoryId, supplier: q.SupplierId);
        var lines = analytics.Lines(f);
        if (q.Search is { Length: > 0 } search)
        {
            var text = search.Trim().ToLower();
            lines = lines.Where(i => i.Name.ToLower().Contains(text));
        }

        var rows = await lines.Select(i => new
            {
                At = i.Order!.ApprovedAt!.Value,
                i.Order.Number,
                Supplier = i.Order.Supplier!.Name,
                i.RequestItem!.ItemId,
                i.Name,
                i.UnitCode,
                Category = i.RequestItem.Category!.Name,
                i.Quantity,
                Price = i.Order.PricesIncludeVat ? i.UnitPrice : i.UnitPrice * (1 + i.Order.VatRate / 100),
                Urgent = i.RequestItem.Request!.Priority == ProcurementPriority.Urgent || i.RequestItem.Request.Priority == ProcurementPriority.High,
                FromOffer = i.Order.OfferId != null,
            })
            .ToListAsync(ct);

        var items = new List<ProcurementPriceItem>();
        foreach (var g in rows.GroupBy(r => r.ItemId is { } id ? id.ToString() : $"{Key(r.Name)}|{r.UnitCode}"))
        {
            var all = g.Select(r => new { Date = FieldClock.LocalDate(r.At), r.At, r.Number, r.Supplier, r.Name, r.UnitCode, r.Category, r.Quantity, Price = Math.Round(r.Price, 2), r.Urgent, r.FromOffer })
                .OrderBy(r => r.At).ThenBy(r => r.Number, StringComparer.Ordinal).ToList();
            var period = all.Where(r => r.Date >= q.From).ToList();
            if (period.Count == 0)
            {
                continue;
            }

            var before = all.LastOrDefault(r => r.Date < q.From);
            var baseline = before ?? (period.Count >= 2 ? period[0] : null);
            var compared = before is null ? period.Skip(1) : period;
            var last = period[^1];
            var min = period.MinBy(r => r.Price)!;
            var quantity = period.Sum(r => r.Quantity);
            var amount = period.Sum(r => r.Quantity * r.Price);
            items.Add(new ProcurementPriceItem(
                last.Name,
                last.UnitCode,
                last.Category,
                period.Count,
                quantity,
                Math.Round(amount, 2),
                baseline?.Price,
                baseline?.Date,
                before is not null,
                baseline?.Supplier,
                period[0].Price,
                last.Price,
                min.Price,
                period.Max(r => r.Price),
                quantity > 0 ? Math.Round(amount / quantity, 2) : last.Price,
                baseline is { Price: > 0 } b ? Math.Round((last.Price - b.Price) / b.Price * 100, 1) : null,
                baseline is null ? 0 : Math.Round(compared.Sum(r => (r.Price - baseline.Price) * r.Quantity), 2),
                Math.Round(period.Sum(r => (r.Price - min.Price) * r.Quantity), 2),
                min.Supplier,
                period.Select(r => r.Supplier).Distinct().ToList(),
                baseline is not null && baseline.Supplier != last.Supplier,
                period.Count(r => r.Urgent),
                period.Count(r => r.FromOffer),
                period.TakeLast(12).Select(r => new ProcurementPricePoint(r.Date, r.Number, r.Supplier, r.Quantity, r.Price, r.Urgent, r.FromOffer)).ToList()));
        }

        var inPeriod = rows.Where(r => FieldClock.LocalDate(r.At) >= q.From).ToList();
        return new ProcurementPriceReport(q.From, q.To, inPeriod.Count, Math.Round(inPeriod.Sum(r => r.Quantity * r.Price), 2),
            items.OrderByDescending(i => i.Amount).ToList());
    }

    public async Task<ProcurementExpenses> ExpensesAsync(ProcurementContext ctx, ProcurementFilter f, CancellationToken ct)
    {
        RequireRead(ctx);
        var rows = await analytics.Lines(f).Select(i => new
            {
                i.OrderId,
                At = i.Order!.ApprovedAt!.Value,
                Department = i.RequestItem!.Request!.Department!.Name,
                Category = i.RequestItem.Category!.Name,
                Supplier = i.Order.Supplier!.Name,
                Amount = i.Order.PricesIncludeVat ? i.Quantity * i.UnitPrice : i.Quantity * i.UnitPrice * (1 + i.Order.VatRate / 100),
            })
            .ToListAsync(ct);
        var days = f.To.DayNumber - f.From.DayNumber + 1;
        var previous = f with { From = f.From.AddDays(-days), To = f.From.AddDays(-1) };
        var previousTotal = await analytics.Lines(previous)
            .SumAsync(i => (decimal?)(i.Order!.PricesIncludeVat ? i.Quantity * i.UnitPrice : i.Quantity * i.UnitPrice * (1 + i.Order.VatRate / 100)), ct) ?? 0m;

        static List<ProcurementAmount> By(IEnumerable<(string Key, string Label, decimal Amount)> source) =>
            source.GroupBy(x => x.Key)
                .Select(g => new ProcurementAmount(g.Key, g.First().Label, Math.Round(g.Sum(x => x.Amount), 2), g.Count()))
                .OrderByDescending(a => a.Amount)
                .ToList();

        var months = rows.Select(r => FieldClock.LocalDate(r.At) is var d ? ($"{d:yyyy-MM}", $"{d:MM.yyyy}", r.Amount) : default);
        return new ProcurementExpenses(
            f.From,
            f.To,
            Math.Round(rows.Sum(r => r.Amount), 2),
            rows.Select(r => r.OrderId).Distinct().Count(),
            rows.Count,
            previous.From,
            previous.To,
            Math.Round(previousTotal, 2),
            By(rows.Select(r => (r.Department, r.Department, r.Amount))),
            By(rows.Select(r => (r.Category, r.Category, r.Amount))),
            By(rows.Select(r => (r.Supplier, r.Supplier, r.Amount))),
            By(months).OrderBy(a => a.Key).ToList());
    }

    /// <summary>Поставщики с утверждёнными заказами за период — по сумме; Weak — почему поставщик слабый (рейтинг, опоздания, просрочки, претензии).</summary>
    public async Task<List<ProcurementSupplierEfficiency>> SuppliersAsync(ProcurementContext ctx, ProcurementFilter f, CancellationToken ct)
    {
        RequireRead(ctx);
        var placed = ProcurementOrderRules.Placed;
        var (from, to) = (f.FromUtc, f.ToUtc);
        var stats = await db.ProcurementPurchaseOrders.AsNoTracking()
            .Where(o => placed.Contains(o.Status) && o.ApprovedAt >= from && o.ApprovedAt < to)
            .GroupBy(o => new { o.SupplierId, o.Supplier!.Name })
            .Select(g => new { g.Key.SupplierId, g.Key.Name, Orders = g.Count(), Total = g.Sum(o => o.Total) })
            .ToListAsync(ct);
        var ratings = await suppliers.RatingsAsync(stats.Select(s => s.SupplierId).ToList(), ct);
        return stats.Select(s =>
            {
                var r = ratings[s.SupplierId];
                var weak = new List<string>();
                if (r.Overall is < 60)
                {
                    weak.Add($"рейтинг {r.Overall} из 100");
                }

                if (r.FinishedOrders > 0 && r.LateOrders * 10 >= r.FinishedOrders * 3)
                {
                    weak.Add($"опоздал с {r.LateOrders} из {r.FinishedOrders} поставок");
                }

                if (r.OverdueOrders > 0)
                {
                    weak.Add($"просрочено сейчас: {r.OverdueOrders}");
                }

                if (r.ClaimLines > 0)
                {
                    weak.Add($"претензии: {r.ClaimLines} строк приёмки с браком или недостачей");
                }

                return new ProcurementSupplierEfficiency(s.SupplierId, s.Name, s.Orders, Math.Round(s.Total, 2), r.Overall, r.OnTime, r.Quality, r.Prices, r.Completeness,
                    r.OverdueOrders, r.LateOrders, r.FinishedOrders, r.ClaimLines, weak);
            })
            .OrderByDescending(s => s.Total)
            .ToList();
    }

    /// <summary>Потребности утверждённых заявок, ещё не заказанные, по категориям: категория из двух и больше заявок — кандидат в один заказ.</summary>
    public async Task<List<ProcurementConsolidation>> ConsolidationAsync(ProcurementContext ctx, CancellationToken ct)
    {
        RequireRead(ctx);
        var needs = await orders.NeedsAsync(ctx, null, ct);
        return needs.GroupBy(n => n.CategoryName)
            .Where(g => g.Select(n => n.RequestId).Distinct().Count() >= 2)
            .Select(g => new ProcurementConsolidation(
                g.Key,
                g.Select(n => n.RequestId).Distinct().Count(),
                g.Count(),
                g.Any(n => n.EstimatedPrice is not null) ? Math.Round(g.Where(n => n.EstimatedPrice is not null).Sum(n => n.Remaining * n.EstimatedPrice!.Value), 2) : null,
                g.Select(n => n.RequestNumber).Distinct().ToList(),
                g.Select(n => n.DepartmentName).Distinct().ToList()))
            .OrderByDescending(c => c.Lines)
            .ToList();
    }

    /// <summary>KPI сотрудников снабжения за период (как блок руководителя снабжения в «Обзоре»).</summary>
    public async Task<ProcurementHeadBoard> StaffAsync(ProcurementContext ctx, ProcurementFilter f, CancellationToken ct)
    {
        RequireRead(ctx);
        return await analytics.HeadAsync(f, ct);
    }

    /// <summary>Название позиции без регистра и лишних пробелов — одна позиция без карточки склада.</summary>
    internal static string Key(string name) => string.Join(' ', name.Trim().ToLowerInvariant().Replace('ё', 'е').Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

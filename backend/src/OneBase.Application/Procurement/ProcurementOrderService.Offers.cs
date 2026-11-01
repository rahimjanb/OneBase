using Microsoft.EntityFrameworkCore;
using OneBase.Application.Field;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

public sealed record ProcurementOfferLineInput(Guid RequestItemId, decimal UnitPrice, decimal? Quantity = null, string? Note = null);

/// <summary>Коммерческое предложение из формы: поставщик, цены по позициям, доставка, НДС, условия оплаты, гарантия, срок действия, качество 1–5.</summary>
public sealed record ProcurementOfferInput(
    Guid SupplierId,
    DateOnly? ValidUntil,
    int? DeliveryDays,
    decimal DeliveryCost,
    decimal VatRate,
    bool PricesIncludeVat,
    string? PaymentTerms,
    string? Warranty,
    int? Quality,
    string? Comment,
    IReadOnlyList<ProcurementOfferLineInput>? Lines);

/// <summary>Цена предложения по позиции: за единицу, с НДС (для сравнения), сумма; BestPrice — самая низкая цена с НДС среди предложений.</summary>
public sealed record ProcurementOfferLineView(Guid RequestItemId, string Name, string UnitCode, decimal Quantity, decimal UnitPrice, decimal GrossPrice, decimal Sum, string? Note, bool BestPrice);

public sealed record ProcurementOfferView(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    int? SupplierRating,
    int SupplierOrders,
    int SupplierOverdue,
    DateOnly? ValidUntil,
    bool Expired,
    int? DeliveryDays,
    decimal DeliveryCost,
    decimal VatRate,
    bool PricesIncludeVat,
    string? PaymentTerms,
    string? Warranty,
    int? Quality,
    string? Comment,
    ProcurementOfferStatus Status,
    decimal Subtotal,
    decimal Vat,
    decimal Total,
    int Covered,
    bool BestTotal,
    bool Fastest,
    IReadOnlyList<ProcurementOfferLineView> Lines,
    IReadOnlyList<ProcurementDocumentView> Documents,
    string? CreatedByName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ProcurementOfferOrder> Orders);

public sealed record ProcurementOfferOrder(Guid Id, string Number, ProcurementOrderStatus Status);

public sealed record ProcurementComparisonItem(Guid RequestItemId, string Name, string UnitCode, decimal ToPurchase, decimal OrderedQty, decimal? EstimatedPrice);

/// <summary>
/// Сравнение предложений по заявке: позиции «к закупке» и предложения с ценами, итогами, доставкой, сроком, условиями, качеством и
/// историей поставщика (рейтинг, заказы, просрочки). Лучшие цена, итог (среди покрывающих все позиции) и срок отмечены; выбирает
/// снабжение, согласует директор.
/// </summary>
public sealed record ProcurementComparison(
    Guid RequestId,
    string Number,
    string Title,
    ProcurementRequestStatus Status,
    IReadOnlyList<ProcurementComparisonItem> Items,
    IReadOnlyList<ProcurementOfferView> Offers,
    bool CanEdit,
    bool CanOrder);

public sealed partial class ProcurementOrderService
{
    private static void RequireBuyer(ProcurementContext ctx)
    {
        if (!ctx.ReadsAll && !ctx.IsDirector)
        {
            throw new FieldForbiddenException("Коммерческие предложения видят снабжение и директор.");
        }
    }

    public async Task<ProcurementComparison> CompareAsync(ProcurementContext ctx, Guid requestId, ProcurementSupplierService suppliers, CancellationToken ct)
    {
        RequireBuyer(ctx);
        var r = await db.ProcurementRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId, ct) ?? throw new FieldNotFoundException("Заявка не найдена.");
        if (r.Status == ProcurementRequestStatus.Draft)
        {
            throw new FieldForbiddenException("Черновик заявки видит только инициатор.");
        }

        var items = await db.ProcurementRequestItems.AsNoTracking()
            .Where(i => i.RequestId == requestId && i.Status != ProcurementItemStatus.Cancelled && i.Quantity > i.FromStock)
            .OrderBy(i => i.LineNo)
            .Select(i => new ProcurementComparisonItem(i.Id, i.Name, i.UnitCode, i.Quantity - i.FromStock, i.OrderedQty, i.EstimatedPrice))
            .ToListAsync(ct);
        var offers = await db.ProcurementOffers.AsNoTracking().Include(o => o.Items).Include(o => o.Supplier)
            .Where(o => o.RequestId == requestId).OrderBy(o => o.CreatedAt).ToListAsync(ct);
        var supplierIds = offers.Select(o => o.SupplierId).Distinct().ToList();
        var ratings = await suppliers.RatingsAsync(supplierIds, ct);
        var placed = ProcurementOrderRules.Active.Where(s => s is not (ProcurementOrderStatus.Draft or ProcurementOrderStatus.AwaitingApproval)).ToArray();
        var orderCounts = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => supplierIds.Contains(o.SupplierId) && placed.Contains(o.Status))
            .GroupBy(o => o.SupplierId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var offerIds = offers.Select(o => o.Id).ToList();
        var orders = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.OfferId != null && offerIds.Contains(o.OfferId.Value))
            .Select(o => new { OfferId = o.OfferId!.Value, o.Id, o.Number, o.Status }).ToListAsync(ct);
        var creators = await db.Users.AsNoTracking().Where(u => offers.Select(o => o.CreatedById).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var docs = new Dictionary<Guid, List<ProcurementDocumentView>>();
        foreach (var offer in offers)
        {
            docs[offer.Id] = await documents.ListAsync(ctx, ProcurementOrderRules.OfferDoc, offer.Id, ct);
        }

        var names = items.ToDictionary(i => i.RequestItemId);
        // Лучшие цена, итог и срок — только среди неотклонённых и только когда есть из чего выбирать (два предложения и больше).
        var live = offers.Where(o => o.Status != ProcurementOfferStatus.Rejected).ToList();
        var compare = live.Count > 1;
        var gross = live.SelectMany(o => o.Items.Select(i => (i.RequestItemId, Price: ProcurementOrderRules.GrossPrice(i.UnitPrice, o.VatRate, o.PricesIncludeVat))))
            .GroupBy(x => x.RequestItemId).ToDictionary(g => g.Key, g => g.Min(x => x.Price));
        var full = live.Where(o => items.All(i => o.Items.Any(x => x.RequestItemId == i.RequestItemId))).ToList();
        var bestTotal = (full.Count > 0 ? full : live).Select(o => (decimal?)o.Total).Min();
        var fastest = live.Where(o => o.DeliveryDays != null).Select(o => o.DeliveryDays).Min();
        var today = FieldClock.Today;

        var views = offers.Select(o =>
        {
            var (subtotal, vat, total) = ProcurementOrderRules.Totals(o.Items.Select(i => (i.Quantity, i.UnitPrice)), 0, o.VatRate, o.PricesIncludeVat, o.DeliveryCost);
            var rating = ratings.GetValueOrDefault(o.SupplierId);
            return new ProcurementOfferView(o.Id, o.SupplierId, o.Supplier!.Name, rating?.Overall, orderCounts.GetValueOrDefault(o.SupplierId), rating?.OverdueOrders ?? 0,
                o.ValidUntil, o.ValidUntil is { } until && until < today, o.DeliveryDays, o.DeliveryCost, o.VatRate, o.PricesIncludeVat, o.PaymentTerms, o.Warranty,
                o.Quality, o.Comment, o.Status, subtotal, vat, total, o.Items.Count(i => names.ContainsKey(i.RequestItemId)),
                compare && live.Contains(o) && total == bestTotal && (full.Count == 0 || full.Contains(o)),
                compare && live.Contains(o) && o.DeliveryDays is { } days && days == fastest,
                o.Items.Where(i => names.ContainsKey(i.RequestItemId)).OrderBy(i => items.FindIndex(x => x.RequestItemId == i.RequestItemId)).Select(i =>
                {
                    var g = ProcurementOrderRules.GrossPrice(i.UnitPrice, o.VatRate, o.PricesIncludeVat);
                    return new ProcurementOfferLineView(i.RequestItemId, names[i.RequestItemId].Name, names[i.RequestItemId].UnitCode, i.Quantity, i.UnitPrice, g,
                        Math.Round(i.Quantity * i.UnitPrice, 2), i.Note, compare && live.Contains(o) && g == gross[i.RequestItemId]);
                }).ToList(),
                docs[o.Id], o.CreatedById is { } c ? creators.GetValueOrDefault(c) : null, o.CreatedAt,
                orders.Where(x => x.OfferId == o.Id).Select(x => new ProcurementOfferOrder(x.Id, x.Number, x.Status)).ToList());
        }).ToList();
        return new ProcurementComparison(r.Id, r.Number, r.Title, r.Status, items, views, ctx.Manages && Offerable.Contains(r.Status),
            ctx.Manages && Orderable.Contains(r.Status) && items.Any(i => i.ToPurchase > i.OrderedQty));
    }

    public Task<Guid> CreateOfferAsync(ProcurementContext ctx, Guid requestId, ProcurementOfferInput input, CancellationToken ct) =>
        SaveOfferAsync(ctx, requestId, null, input, ct);

    /// <summary>Правка предложения — пока по нему нет действующего заказа.</summary>
    public async Task UpdateOfferAsync(ProcurementContext ctx, Guid offerId, ProcurementOfferInput input, CancellationToken ct)
    {
        var requestId = await db.ProcurementOffers.AsNoTracking().Where(o => o.Id == offerId).Select(o => (Guid?)o.RequestId).FirstOrDefaultAsync(ct)
                        ?? throw new FieldNotFoundException("Предложение не найдено.");
        await SaveOfferAsync(ctx, requestId, offerId, input, ct);
    }

    private async Task<Guid> SaveOfferAsync(ProcurementContext ctx, Guid requestId, Guid? offerId, ProcurementOfferInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var r = await db.ProcurementRequests.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == requestId, ct) ?? throw new FieldNotFoundException("Заявка не найдена.");
        if (!Offerable.Contains(r.Status))
        {
            throw new FieldConflictException($"Заявка «{ProcurementRules.Label(r.Status)}» — предложения по ней не собираются.");
        }

        var supplier = await db.ProcurementSuppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == input.SupplierId, ct) ?? throw new FieldValidationException("Выберите поставщика.");
        ProcurementOffer? offer = null;
        if (offerId is { } id)
        {
            offer = await db.ProcurementOffers.Include(o => o.Items).FirstAsync(o => o.Id == id, ct);
            var active = ProcurementOrderRules.Active;
            if (await db.ProcurementPurchaseOrders.AnyAsync(o => o.OfferId == id && active.Contains(o.Status), ct))
            {
                throw new FieldConflictException("По предложению уже есть заказ — меняйте заказ.");
            }
        }

        if (!supplier.IsActive && offer?.SupplierId != supplier.Id)
        {
            throw new FieldValidationException($"Поставщик «{supplier.Name}» отключён.");
        }

        if (input.Quality is < 1 or > 5)
        {
            throw new FieldValidationException("Качество — от 1 до 5.");
        }

        if (input.DeliveryDays is < 0 or > 365)
        {
            throw new FieldValidationException("Срок поставки — от 0 до 365 дней.");
        }

        if (input.VatRate is < 0 or > 50)
        {
            throw new FieldValidationException("НДС — от 0 до 50 %.");
        }

        CheckMoney(input.DeliveryCost, "Доставка");
        var lines = (input.Lines ?? []).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Укажите цены хотя бы по одной позиции.");
        }

        if (lines.GroupBy(l => l.RequestItemId).Any(g => g.Count() > 1))
        {
            throw new FieldValidationException("Позиция повторяется — укажите её одной строкой.");
        }

        var items = r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled && i.ToPurchase > 0).ToDictionary(i => i.Id);
        var parsed = new List<ProcurementOfferItem>();
        foreach (var line in lines)
        {
            var item = items.GetValueOrDefault(line.RequestItemId) ?? throw new FieldValidationException("Позиция не из части «к закупке» этой заявки.");
            var quantity = line.Quantity ?? item.ToPurchase;
            if (quantity <= 0 || decimal.Round(quantity, 3) != quantity)
            {
                throw new FieldValidationException($"«{item.Name}»: количество — больше нуля.");
            }

            if (line.UnitPrice < 0 || line.UnitPrice > ProcurementRules.MaxPrice || decimal.Round(line.UnitPrice, 2) != line.UnitPrice)
            {
                throw new FieldValidationException($"«{item.Name}»: цена — не отрицательная, не больше двух знаков после запятой.");
            }

            parsed.Add(new ProcurementOfferItem { RequestItemId = item.Id, UnitPrice = line.UnitPrice, Quantity = quantity, Note = Text(line.Note, 500, "Примечание") });
        }

        var isNew = offer is null;
        offer ??= new ProcurementOffer { RequestId = r.Id, CreatedById = ctx.UserId };
        offer.SupplierId = supplier.Id;
        offer.ValidUntil = input.ValidUntil;
        offer.DeliveryDays = input.DeliveryDays;
        offer.DeliveryCost = input.DeliveryCost;
        offer.VatRate = input.VatRate;
        offer.PricesIncludeVat = input.PricesIncludeVat;
        offer.PaymentTerms = Text(input.PaymentTerms, 500, "Условия оплаты") ?? supplier.PaymentTerms;
        offer.Warranty = Text(input.Warranty, 200, "Гарантия");
        offer.Quality = input.Quality;
        offer.Comment = Text(input.Comment, 2000, "Комментарий");
        foreach (var old in offer.Items.ToList())
        {
            offer.Items.Remove(old);
            db.ProcurementOfferItems.Remove(old);
        }

        foreach (var item in parsed)
        {
            item.OfferId = offer.Id;
            offer.Items.Add(item);
            db.ProcurementOfferItems.Add(item);
        }

        offer.Total = ProcurementOrderRules.Totals(parsed.Select(i => (i.Quantity, i.UnitPrice)), 0, offer.VatRate, offer.PricesIncludeVat, offer.DeliveryCost).Total;
        if (isNew)
        {
            db.ProcurementOffers.Add(offer);
        }
        else
        {
            offer.UpdatedAt = Now;
        }

        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementRules.RequestDoc, DocId = r.Id, ActorId = ctx.UserId, Action = "offer",
            Text = $"{(isNew ? "Добавлено" : "Изменено")} предложение «{supplier.Name}»: {ProcurementRequestService.Positions(parsed.Count)}, итог {ProcurementRules.Money(offer.Total)} сум",
        });
        await db.SaveChangesAsync(ct);
        return offer.Id;
    }

    /// <summary>Отклонить предложение или вернуть в рассмотрение (снабжение); выбранное заказом — не трогается.</summary>
    public async Task SetOfferStatusAsync(ProcurementContext ctx, Guid offerId, bool rejected, CancellationToken ct)
    {
        RequireManage(ctx);
        var offer = await db.ProcurementOffers.Include(o => o.Supplier).FirstOrDefaultAsync(o => o.Id == offerId, ct) ?? throw new FieldNotFoundException("Предложение не найдено.");
        var active = ProcurementOrderRules.Active;
        if (rejected && await db.ProcurementPurchaseOrders.AnyAsync(o => o.OfferId == offerId && active.Contains(o.Status), ct))
        {
            throw new FieldConflictException("По предложению есть заказ — сначала отмените заказ.");
        }

        offer.Status = rejected ? ProcurementOfferStatus.Rejected : ProcurementOfferStatus.Proposed;
        offer.UpdatedAt = Now;
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = ProcurementRules.RequestDoc, DocId = offer.RequestId, ActorId = ctx.UserId, Action = "offer",
            Text = rejected ? $"Предложение «{offer.Supplier!.Name}» отклонено" : $"Предложение «{offer.Supplier!.Name}» снова рассматривается",
        });
        await db.SaveChangesAsync(ct);
    }
}

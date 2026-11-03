using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

/// <summary>Куплено по позиции заявки: сколько и почём за единицу (сум).</summary>
public sealed record ProcurementPurchaseLineInput(Guid RequestItemId, decimal Quantity, decimal Price);

/// <summary>
/// «Куплено» по заявке из формы: позиции (сколько и почём), где купили — поставщик из справочника (SupplierId) или название магазина
/// (SupplierName: есть такой в справочнике — он, нет — добавляется), номер чека или накладной, комментарий, ключ повтора формы.
/// </summary>
public sealed record ProcurementPurchaseInput(
    IReadOnlyList<ProcurementPurchaseLineInput>? Lines,
    Guid? SupplierId,
    string? SupplierName,
    string? Reference,
    string? Comment,
    Guid? ClientKey = null);

/// <summary>Что получилось: покупка (заказ PO-, сразу закрыт), приход на склад (RC-), сумма.</summary>
public sealed record ProcurementPurchaseResult(Guid OrderId, string OrderNumber, Guid ReceiptId, string ReceiptNumber, decimal Total);

/// <summary>Позиция «к закупке»: сколько осталось купить, согласованная цена за единицу, карточка склада (если связана).</summary>
public sealed record ProcurementToBuyLine(
    Guid RequestItemId,
    int LineNo,
    string Name,
    string CategoryName,
    string UnitCode,
    decimal Left,
    decimal? Price,
    string? Sku,
    string? Comment);

/// <summary>Утверждённая заявка, по которой снабжению осталось что-то купить; Overdue — желаемая дата прошла; Total — остаток к закупке, сум.</summary>
public sealed record ProcurementToBuy(
    Guid RequestId,
    string Number,
    string Title,
    ProcurementPriority Priority,
    ProcurementRequestStatus Status,
    string DepartmentName,
    string InitiatorName,
    DateOnly? DesiredDate,
    DateTimeOffset? ApprovedAt,
    bool Overdue,
    decimal Total,
    IReadOnlyList<ProcurementToBuyLine> Lines);

/// <summary>Экран «К закупке»: заявки и может ли пользователь отметить «Куплено».</summary>
public sealed record ProcurementToBuyView(bool CanBuy, IReadOnlyList<ProcurementToBuy> Requests);

/// <summary>
/// Закупка по согласованной заявке без заказов и приёмки: снабженец покупает сам и отмечает «Куплено» — сколько и почём, где купили. Одной
/// записью: покупка (заказ PO-, помечен Direct, сразу закрыт — по нему считаются расходы, отчёты и поставщики), приход на склад (RC-) по
/// цене покупки, резерв под заявку — товар «К выдаче», выдаёт кладовщик. Позиция без карточки склада приходуется на карточку с тем же
/// названием и единицей, а если такой нет — на новую (артикул SKU-год-NNNNN). Дороже согласованного (сверх допуска) — покупка проходит,
/// финансисту уведомление. Повтор формы (ключ) не покупает второй раз; одновременные покупки по одной позиции не превышают «к закупке».
/// </summary>
public sealed class ProcurementPurchaseService(
    IAppDbContext db,
    IProcurementNumbers numbers,
    ProcurementStockLedger ledger,
    UserNotifier notifier,
    IAuditLogger audit)
{
    /// <summary>Префикс артикула карточки склада, созданной при покупке.</summary>
    public const string SkuPrefix = "SKU";

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    // ---------- К закупке ----------

    /// <summary>
    /// Заявки к закупке: утверждённые и частично выполненные, где у позиции «к закупке» больше, чем уже куплено или заказано. Сначала срочные,
    /// потом по желаемой дате (без даты — в конце), потом по дате утверждения. Видят снабжение и директор; «Куплено» — снабжение.
    /// </summary>
    public async Task<ProcurementToBuyView> ToBuyAsync(ProcurementContext ctx, CancellationToken ct)
    {
        if (!ctx.ReadsAll && !ctx.IsDirector)
        {
            throw new FieldForbiddenException("Заявки к закупке видят снабжение и директор.");
        }

        var orderable = ProcurementOrderService.Orderable;
        var today = FieldClock.Today;
        var rows = await db.ProcurementRequests.AsNoTracking()
            .Where(r => orderable.Contains(r.Status) && r.Items.Any(i => i.Status != ProcurementItemStatus.Cancelled && i.Quantity - i.FromStock > i.OrderedQty))
            .OrderByDescending(r => r.Priority == ProcurementPriority.Urgent ? 3 : r.Priority == ProcurementPriority.High ? 2 : r.Priority == ProcurementPriority.Normal ? 1 : 0)
            .ThenBy(r => r.DesiredDate == null).ThenBy(r => r.DesiredDate).ThenBy(r => r.ApprovedAt)
            .Select(r => new
            {
                r.Id, r.Number, r.Title, r.Priority, r.Status, Department = r.Department!.Name, Initiator = r.Initiator!.FullName, r.DesiredDate, r.ApprovedAt,
                Lines = r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled && i.Quantity - i.FromStock > i.OrderedQty).OrderBy(i => i.LineNo)
                    .Select(i => new ProcurementToBuyLine(i.Id, i.LineNo, i.Name, i.Category!.Name, i.UnitCode, i.Quantity - i.FromStock - i.OrderedQty, i.EstimatedPrice,
                        i.Item == null ? null : i.Item.Sku, i.Comment))
                    .ToList(),
            })
            .ToListAsync(ct);
        var list = rows.Select(r => new ProcurementToBuy(r.Id, r.Number, r.Title, r.Priority, r.Status, r.Department, r.Initiator, r.DesiredDate, r.ApprovedAt,
            r.DesiredDate is { } d && d < today, ProcurementRules.Total(r.Lines.Select(l => (l.Left, l.Price))), r.Lines)).ToList();
        return new ProcurementToBuyView(ctx.Manages, list);
    }

    // ---------- Куплено ----------

    public Task<ProcurementPurchaseResult> PurchaseAsync(ProcurementContext ctx, Guid requestId, ProcurementPurchaseInput input, CancellationToken ct) =>
        ledger.RetryAsync(async () =>
        {
            if (!ctx.Manages)
            {
                throw new FieldForbiddenException("Отмечает покупку снабжение.");
            }

            if (input.ClientKey is { } key
                && await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.CreatedById == ctx.UserId && o.ClientKey == key && o.Direct)
                    .Select(o => new { o.Id, o.Number, o.Total, Receipt = db.ProcurementStockDocuments.Where(d => d.OrderId == o.Id).Select(d => new { d.Id, d.Number }).First() })
                    .FirstOrDefaultAsync(ct) is { } done)
            {
                return new ProcurementPurchaseResult(done.Id, done.Number, done.Receipt.Id, done.Receipt.Number, done.Total);
            }

            var r = await db.ProcurementRequests.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == requestId, ct) ?? throw new FieldNotFoundException("Заявка не найдена.");
            if (!ProcurementRules.CanView(ctx, r))
            {
                throw new FieldNotFoundException("Заявка не найдена.");
            }

            if (!ProcurementOrderService.Orderable.Contains(r.Status))
            {
                throw new FieldConflictException(r.Status is ProcurementRequestStatus.Fulfilled or ProcurementRequestStatus.Cancelled or ProcurementRequestStatus.Rejected
                    ? $"Заявка «{ProcurementRules.Label(r.Status)}» — покупать по ней больше нечего."
                    : $"Заявка «{ProcurementRules.Label(r.Status)}» — купить можно после согласования.");
            }

            var lines = (input.Lines ?? []).Where(l => l.Quantity != 0).ToList();
            if (lines.Count == 0)
            {
                throw new FieldValidationException("Укажите, что куплено: количество и цену хотя бы по одной позиции.");
            }

            if (lines.GroupBy(l => l.RequestItemId).Any(g => g.Count() > 1))
            {
                throw new FieldValidationException("Позиция повторяется — укажите её одной строкой.");
            }

            var bought = new List<(ProcurementRequestItem Item, decimal Quantity, decimal Price)>();
            foreach (var line in lines)
            {
                var item = r.Items.FirstOrDefault(i => i.Id == line.RequestItemId) ?? throw new FieldValidationException("Позиция не из этой заявки.");
                if (item.Status == ProcurementItemStatus.Cancelled)
                {
                    throw new FieldValidationException($"«{item.Name}»: позиция отменена — покупать её не нужно.");
                }

                if (line.Quantity < 0 || line.Quantity > ProcurementRules.MaxQuantity || decimal.Round(line.Quantity, 3) != line.Quantity)
                {
                    throw new FieldValidationException($"«{item.Name}»: количество — больше нуля, не больше трёх знаков после запятой.");
                }

                var left = item.ToPurchase - item.OrderedQty;
                if (line.Quantity > left)
                {
                    throw new FieldConflictException(left > 0
                        ? $"«{item.Name}»: осталось купить {ProcurementRules.Quantity(left)} {item.UnitCode} — больше не оприходовать."
                        : $"«{item.Name}»: уже куплено всё, что нужно по заявке.");
                }

                if (line.Price <= 0 || line.Price > ProcurementRules.MaxPrice || decimal.Round(line.Price, 2) != line.Price)
                {
                    throw new FieldValidationException($"«{item.Name}»: укажите цену за {item.UnitCode} — больше нуля, не больше двух знаков после запятой.");
                }

                bought.Add((item, line.Quantity, line.Price));
            }

            var reference = Text(input.Reference, 100, "Номер чека или накладной");
            var comment = Text(input.Comment, 1000, "Комментарий");
            var (supplier, newSupplier) = await ProcurementSupplierService.ResolveAsync(db, ctx, input.SupplierId, input.SupplierName, "Укажите, где купили: магазин или поставщика.", ct);
            var cards = await CardsAsync(bought.Select(b => b.Item).ToList(), ct);

            var order = new ProcurementPurchaseOrder
            {
                Number = await numbers.NextAsync(ProcurementOrderRules.OrderPrefix, ct),
                SupplierId = supplier.Id,
                ResponsibleId = ctx.UserId,
                CreatedById = ctx.UserId,
                Status = ProcurementOrderStatus.Completed,
                Direct = true,
                OrderDate = FieldClock.Today,
                PricesIncludeVat = true,
                ApprovedAt = Now,
                OrderedAt = Now,
                DeliveredAt = Now,
                CompletedAt = Now,
                Comment = comment,
                ClientKey = input.ClientKey,
            };
            var no = 0;
            foreach (var (item, quantity, price) in bought)
            {
                order.Items.Add(new ProcurementPurchaseOrderItem
                {
                    OrderId = order.Id, LineNo = ++no, RequestItemId = item.Id, Name = item.Name, UnitCode = item.UnitCode, Quantity = quantity, UnitPrice = price,
                    ReceivedQty = quantity,
                });
            }

            order.Subtotal = order.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2));
            order.Total = order.Subtotal;
            db.ProcurementPurchaseOrders.Add(order);

            var warehouse = await ledger.DefaultWarehouseIdAsync(ct);
            var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.Receipt, warehouse, ctx.UserId, ct);
            doc.OrderId = order.Id;
            doc.RequestId = r.Id;
            doc.DepartmentId = r.DepartmentId;
            doc.PostedAt = Now;
            doc.Reference = reference;
            doc.Comment = comment;

            var itemIds = bought.Select(b => b.Item.Id).ToList();
            var active = await db.ProcurementStockReservations
                .Where(x => itemIds.Contains(x.RequestItemId) && x.Status == ProcurementReservationStatus.Active).ToDictionaryAsync(x => x.RequestItemId, ct);
            var balances = await ledger.BalancesAsync(warehouse, cards.Values.Select(c => c.Id), ct);
            foreach (var (orderItem, (item, quantity, price)) in order.Items.Zip(bought))
            {
                var card = cards[item.Id];
                var line = new ProcurementReceiptLine
                {
                    DocumentId = doc.Id, LineNo = orderItem.LineNo, OrderItemId = orderItem.Id, ItemId = card.Id, Received = quantity, Accepted = quantity,
                };
                doc.ReceiptLines.Add(line);
                db.ProcurementReceiptLines.Add(line);
                item.Request = r;
                item.OrderedQty += quantity;
                ledger.ReceiveForRequest(doc, balances[card.Id], item, quantity, price, item.Name, active);
            }

            var settings = await db.ProcurementSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            var tolerance = settings?.ReapprovalTolerancePercent ?? 0m;
            var dearer = bought.Where(b => b.Item.EstimatedPrice is { } approved && b.Price > approved * (1 + tolerance / 100)).ToList();
            var summary = string.Join("; ", bought.Select(b =>
                $"{b.Item.Name} — {ProcurementRules.Quantity(b.Quantity)} {b.Item.UnitCode} × {ProcurementRules.Money(b.Price)} = {ProcurementRules.Money(Math.Round(b.Quantity * b.Price, 2))} сум"));
            var dearerText = dearer.Count == 0
                ? null
                : "Дороже согласованного: " + string.Join("; ", dearer.Select(b =>
                    $"{b.Item.Name} — {ProcurementRules.Money(b.Price)} вместо {ProcurementRules.Money(b.Item.EstimatedPrice!.Value)} сум за {b.Item.UnitCode}"));

            Log(ctx, ProcurementRules.RequestDoc, r.Id, "purchased",
                $"Куплено у «{supplier.Name}» ({order.Number}, приход {doc.Number}{(reference is null ? "" : $", чек {reference}")}): {summary}; итого {ProcurementRules.Money(order.Total)} сум"
                + (dearerText is null ? "" : $". {dearerText}"), comment);
            Log(ctx, ProcurementOrderRules.OrderDoc, order.Id, "created",
                $"Куплено по заявке {r.Number} у «{supplier.Name}»: {ProcurementRequestService.Positions(order.Items.Count)}, {ProcurementRules.Money(order.Total)} сум — сразу на склад ({doc.Number})",
                comment, nameof(ProcurementOrderStatus.Completed));
            Log(ctx, ProcurementOrderRules.ReceiptDoc, doc.Id, "posted", $"Куплено по заявке {r.Number} и оприходовано: {summary}", comment,
                nameof(ProcurementStockDocumentStatus.Posted));
            if (newSupplier)
            {
                Log(ctx, ProcurementOrderRules.SupplierDoc, supplier.Id, "created", $"Поставщик добавлен при покупке по заявке {r.Number}", null);
            }

            Notify(ctx, r.InitiatorId, UserNotificationKind.ProcurementStatus, $"Куплено по заявке {r.Number} — можно получить на складе", summary, $"/requests/{r.Id}");
            foreach (var keeper in await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct))
            {
                Notify(ctx, keeper, UserNotificationKind.ProcurementRequest, $"К выдаче: куплено по заявке {r.Number}", $"{r.Title} · {summary}", "/procurement/issue");
            }

            if (dearerText is not null && settings?.FinanceApproverId is { } financier)
            {
                Notify(ctx, financier, UserNotificationKind.ProcurementStatus, $"Куплено дороже согласованного: заявка {r.Number}", dearerText, $"/requests/{r.Id}");
            }

            await db.SaveChangesAsync(ct);
            foreach (var card in cards.Values.Where(c => c.Created).DistinctBy(c => c.Id))
            {
                await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.item.created", "procurement_item", card.Id.ToString(),
                    new { card.Sku, card.Name, source = $"purchase {order.Number}" }, ct);
            }

            return new ProcurementPurchaseResult(order.Id, order.Number, doc.Id, doc.Number, order.Total);
        });

    private sealed record Card(Guid Id, string Sku, string Name, bool Created);

    /// <summary>
    /// Карточки склада для позиций: связанная с позицией; иначе действующая с тем же названием и единицей (без учёта регистра); иначе новая —
    /// название, категория и единица из заявки, артикул SKU-год-NNNNN.
    /// </summary>
    private async Task<Dictionary<Guid, Card>> CardsAsync(List<ProcurementRequestItem> items, CancellationToken ct)
    {
        var linkedIds = items.Select(i => i.ItemId).OfType<Guid>().Distinct().ToList();
        var linked = await db.ProcurementItems.AsNoTracking().Where(i => linkedIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var names = items.Where(i => i.ItemId is null).Select(i => i.Name.Trim().ToLower()).Distinct().ToList();
        var byName = await db.ProcurementItems.AsNoTracking().Where(i => i.IsActive && names.Contains(i.Name.ToLower())).OrderBy(i => i.CreatedAt).ToListAsync(ct);
        var result = new Dictionary<Guid, Card>();
        var created = new Dictionary<(string, string), Card>();
        foreach (var item in items)
        {
            if (item.ItemId is { } itemId)
            {
                var card = linked.GetValueOrDefault(itemId) ?? throw new FieldConflictException($"«{item.Name}»: карточка склада не найдена.");
                result[item.Id] = new Card(card.Id, card.Sku, card.Name, false);
                continue;
            }

            var key = (item.Name.Trim().ToLower(), item.UnitCode);
            if (byName.FirstOrDefault(c => c.Name.ToLower() == key.Item1 && c.UnitCode == item.UnitCode) is { } same)
            {
                result[item.Id] = new Card(same.Id, same.Sku, same.Name, false);
                continue;
            }

            if (!created.TryGetValue(key, out var fresh))
            {
                var entity = new ProcurementItem
                {
                    Sku = await numbers.NextAsync(SkuPrefix, ct), Name = item.Name.Trim(), CategoryId = item.CategoryId, UnitCode = item.UnitCode,
                };
                db.ProcurementItems.Add(entity);
                fresh = new Card(entity.Id, entity.Sku, entity.Name, true);
                created[key] = fresh;
            }

            result[item.Id] = fresh;
        }

        return result;
    }

    private void Notify(ProcurementContext ctx, Guid recipient, UserNotificationKind kind, string title, string body, string link)
    {
        if (recipient != ctx.UserId)
        {
            notifier.Notify(recipient, kind, title, Cut(body, 500), link);
        }
    }

    private void Log(ProcurementContext ctx, string docType, Guid docId, string action, string text, string? comment, string? toStatus = null) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = docType, DocId = docId, ActorId = ctx.UserId, Action = action, ToStatus = toStatus, Text = Cut(text, 1000),
            Comment = comment is null ? null : Cut(comment, 2000),
        });

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string? Text(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= max ? text : throw new FieldValidationException($"{field} — до {max} символов.");
    }
}

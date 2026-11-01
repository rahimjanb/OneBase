using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Строка заказа для расчётов и снимка утверждения: позиция заявки, количество, цена за единицу.</summary>
public sealed record ProcurementOrderLine(Guid RequestItemId, string Name, decimal Quantity, decimal UnitPrice);

/// <summary>Утверждённые условия заказа (снимок): поставщик, строки, итог.</summary>
public sealed record ProcurementOrderSnapshot(Guid SupplierId, string SupplierName, IReadOnlyList<ProcurementOrderLine> Lines, decimal Total);

/// <summary>Утверждённая потребность для строки заказа: позиция заявки и её предполагаемая цена (null — цены в заявке не было).</summary>
public sealed record ProcurementApprovedNeed(Guid RequestItemId, string Name, decimal? EstimatedPrice);

/// <summary>
/// Правила заказов поставщикам (чистые функции — проверяются тестами): итоги, когда заказ укладывается в утверждённую директором заявку
/// (утверждается сам), а когда решает директор, существенные изменения после утверждения, просрочка.
/// </summary>
public static class ProcurementOrderRules
{
    public const string OrderPrefix = "PO";
    public const string OrderDoc = "order";
    public const string OfferDoc = "offer";
    public const string SupplierDoc = "supplier";
    public const string ReceiptDoc = "receipt";

    /// <summary>Действующие заказы — их количество занимает часть «к закупке» заявки (всё, кроме отменённых).</summary>
    public static readonly ProcurementOrderStatus[] Active =
    [
        ProcurementOrderStatus.Draft, ProcurementOrderStatus.AwaitingApproval, ProcurementOrderStatus.Approved, ProcurementOrderStatus.Ordered,
        ProcurementOrderStatus.PartiallyDelivered, ProcurementOrderStatus.Delivered, ProcurementOrderStatus.Completed,
    ];

    /// <summary>Закупки (обязательство перед поставщиком): утверждённые заказы и дальше, кроме отменённых — по ним считаются расходы.</summary>
    public static readonly ProcurementOrderStatus[] Placed =
    [
        ProcurementOrderStatus.Approved, ProcurementOrderStatus.Ordered, ProcurementOrderStatus.PartiallyDelivered, ProcurementOrderStatus.Delivered,
        ProcurementOrderStatus.Completed,
    ];

    /// <summary>Ждут поставки: утверждён, заказан, поставлен частично — по ним считаются просрочки и ожидаемые поставки.</summary>
    public static readonly ProcurementOrderStatus[] Live = [ProcurementOrderStatus.Approved, ProcurementOrderStatus.Ordered, ProcurementOrderStatus.PartiallyDelivered];

    /// <summary>Меняется снабжением: черновик; утверждённый и заказанный — с проверкой на повторное согласование.</summary>
    public static bool Editable(ProcurementOrderStatus status) =>
        status is ProcurementOrderStatus.Draft or ProcurementOrderStatus.Approved or ProcurementOrderStatus.Ordered;

    /// <summary>Отменяется, пока не началась поставка.</summary>
    public static bool Cancellable(ProcurementOrderStatus status) =>
        status is ProcurementOrderStatus.Draft or ProcurementOrderStatus.AwaitingApproval or ProcurementOrderStatus.Approved or ProcurementOrderStatus.Ordered;

    public static bool IsOverdue(ProcurementOrderStatus status, DateOnly? expected, DateOnly today) => Live.Contains(status) && expected is { } d && d < today;

    /// <summary>Принимаются на склад: заказанные у поставщика и поставленные частично.</summary>
    public static readonly ProcurementOrderStatus[] Receivable = [ProcurementOrderStatus.Ordered, ProcurementOrderStatus.PartiallyDelivered];

    /// <summary>Ждёт поставки по строке заказа: количество − получено − закрыто без поставки (не меньше нуля).</summary>
    public static decimal Remaining(decimal quantity, decimal received, decimal closed) => Math.Max(0, quantity - received - closed);

    /// <summary>
    /// Строка приёмки: пришло, из них брак, недостача по накладной; ждёт поставки по заказу. Принято на склад = пришло − брак — не больше
    /// того, что ждёт поставки (лишнее не принимается). Возвращает принятое или текст ошибки.
    /// </summary>
    public static (decimal Accepted, string? Error) ReceiptLine(string name, string unit, decimal received, decimal defective, decimal shortage, decimal remaining)
    {
        foreach (var (value, label) in new[] { (received, "пришло"), (defective, "брак"), (shortage, "недостача") })
        {
            if (value < 0 || value > ProcurementRules.MaxQuantity || decimal.Round(value, 3) != value)
            {
                return (0, $"«{name}»: {label} — не меньше нуля, не больше трёх знаков после запятой.");
            }
        }

        if (defective > received)
        {
            return (0, $"«{name}»: брак ({ProcurementRules.Quantity(defective)}) больше, чем пришло ({ProcurementRules.Quantity(received)}).");
        }

        var accepted = received - defective;
        if (accepted > remaining)
        {
            return (0, $"«{name}»: ждёт поставки {ProcurementRules.Quantity(remaining)} {unit}, принять {ProcurementRules.Quantity(accepted)} нельзя — лишнее не принимается.");
        }

        return (accepted, null);
    }

    /// <summary>Статус поставки по строкам заказа: всё получено или закрыто — поставлен, что-то получено — поставлен частично, иначе — как был.</summary>
    public static ProcurementOrderStatus DeliveryStatus(ProcurementOrderStatus current, IEnumerable<(decimal Quantity, decimal Received, decimal Closed)> lines)
    {
        var list = lines.ToList();
        if (list.Count > 0 && list.All(l => l.Received + l.Closed >= l.Quantity))
        {
            return ProcurementOrderStatus.Delivered;
        }

        return list.Any(l => l.Received > 0) ? ProcurementOrderStatus.PartiallyDelivered : current;
    }

    /// <summary>Итоги: позиции (кол-во × цена) − скидка (не ниже нуля) + НДС сверху (если цены без НДС) + доставка.</summary>
    public static (decimal Subtotal, decimal Vat, decimal Total) Totals(IEnumerable<(decimal Quantity, decimal Price)> lines, decimal discount, decimal vatRate,
        bool pricesIncludeVat, decimal delivery)
    {
        var subtotal = lines.Sum(l => Math.Round(l.Quantity * l.Price, 2));
        var net = Math.Max(0, subtotal - discount);
        var vat = pricesIncludeVat ? 0m : Math.Round(net * vatRate / 100m, 2);
        return (subtotal, vat, net + vat + delivery);
    }

    /// <summary>Цена за единицу с НДС — с ней сравнивается предполагаемая цена заявки.</summary>
    public static decimal GrossPrice(decimal price, decimal vatRate, bool pricesIncludeVat) =>
        pricesIncludeVat ? price : Math.Round(price * (1 + vatRate / 100m), 2);

    /// <summary>
    /// Почему заказ нужно согласовать с директором (пусто — укладывается в утверждённую заявку и утверждается сам): в заявке не было
    /// цены, цена за единицу (с НДС) или итог заказа (со скидкой, НДС и доставкой) выше утверждённых сверх допуска.
    /// </summary>
    public static List<string> NeedsDirector(IReadOnlyList<ProcurementOrderLine> lines, IReadOnlyDictionary<Guid, ProcurementApprovedNeed> approved,
        decimal vatRate, bool pricesIncludeVat, decimal total, decimal tolerancePercent)
    {
        var reasons = new List<string>();
        var limit = 1 + tolerancePercent / 100m;
        decimal approvedSum = 0;
        foreach (var line in lines)
        {
            var need = approved.GetValueOrDefault(line.RequestItemId);
            if (need?.EstimatedPrice is not { } estimate)
            {
                reasons.Add($"«{line.Name}»: в заявке не было цены");
                continue;
            }

            approvedSum += Math.Round(line.Quantity * estimate, 2);
            var gross = GrossPrice(line.UnitPrice, vatRate, pricesIncludeVat);
            if (gross > estimate * limit)
            {
                reasons.Add($"«{line.Name}»: цена {ProcurementRules.Money(gross)} выше утверждённой {ProcurementRules.Money(estimate)}");
            }
        }

        if (reasons.Count == 0 && total > approvedSum * limit)
        {
            reasons.Add($"итог заказа {ProcurementRules.Money(total)} выше утверждённой суммы {ProcurementRules.Money(approvedSum)}");
        }

        return reasons;
    }

    /// <summary>
    /// Существенные изменения утверждённого заказа (пусто — несущественные): сменился поставщик, добавлена или удалена позиция, выросло
    /// количество, цена или итог выросли сверх допуска. Уменьшение — не существенно.
    /// </summary>
    public static List<string> MaterialChanges(ProcurementOrderSnapshot approved, ProcurementOrderSnapshot current, decimal tolerancePercent)
    {
        var reasons = new List<string>();
        if (approved.SupplierId != current.SupplierId)
        {
            reasons.Add($"сменился поставщик: {approved.SupplierName} → {current.SupplierName}");
        }

        var before = approved.Lines.ToDictionary(l => l.RequestItemId);
        var after = current.Lines.ToDictionary(l => l.RequestItemId);
        foreach (var gone in approved.Lines.Where(l => !after.ContainsKey(l.RequestItemId)))
        {
            reasons.Add($"удалена позиция «{gone.Name}»");
        }

        var limit = 1 + tolerancePercent / 100m;
        foreach (var line in current.Lines)
        {
            if (!before.TryGetValue(line.RequestItemId, out var old))
            {
                reasons.Add($"добавлена позиция «{line.Name}»");
                continue;
            }

            if (line.Quantity > old.Quantity)
            {
                reasons.Add($"«{line.Name}»: количество {ProcurementRules.Quantity(old.Quantity)} → {ProcurementRules.Quantity(line.Quantity)}");
            }

            if (line.UnitPrice > old.UnitPrice * limit)
            {
                reasons.Add($"«{line.Name}»: цена {ProcurementRules.Money(old.UnitPrice)} → {ProcurementRules.Money(line.UnitPrice)}");
            }
        }

        if (reasons.Count == 0 && current.Total > approved.Total * limit)
        {
            reasons.Add($"итог {ProcurementRules.Money(approved.Total)} → {ProcurementRules.Money(current.Total)}");
        }

        return reasons;
    }

    public static string Label(ProcurementOrderStatus status) => status switch
    {
        ProcurementOrderStatus.Draft => "Черновик",
        ProcurementOrderStatus.AwaitingApproval => "Ждёт директора",
        ProcurementOrderStatus.Approved => "Утверждён",
        ProcurementOrderStatus.Ordered => "Заказан",
        ProcurementOrderStatus.PartiallyDelivered => "Поставлен частично",
        ProcurementOrderStatus.Delivered => "Поставлен",
        ProcurementOrderStatus.Completed => "Закрыт",
        ProcurementOrderStatus.Cancelled => "Отменён",
        _ => status.ToString(),
    };

    /// <summary>
    /// Рейтинг поставщика 0–100 — среднее того, что есть: сроки (доля заказов, поставленных в срок, из тех, чей срок наступил), качество
    /// (оценка в предложениях), соблюдение цен (заказ не дороже предложения), претензии (доля строк приёмки без брака и недостачи), полнота
    /// (доля заказанного, что поставлено по завершённым заказам). Нет данных ни по одной составляющей — null.
    /// </summary>
    public static int? Rating(decimal? onTime, decimal? quality, decimal? prices, decimal? claims = null, decimal? completeness = null)
    {
        var parts = new[] { onTime, quality, prices, claims, completeness }.OfType<decimal>().ToList();
        return parts.Count == 0 ? null : (int)Math.Round(parts.Average());
    }
}

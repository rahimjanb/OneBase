using System.Globalization;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Номера документов снабжения: PR-2026-00125. Реализация — в инфраструктуре (счётчик PostgreSQL без повторов).</summary>
public interface IProcurementNumbers
{
    Task<string> NextAsync(string prefix, CancellationToken ct);
}

/// <summary>
/// Кто спрашивает: пользователь, его права (из запроса), отдел (из базы), отделы, которыми он руководит (руководители отделов в «Задачах»),
/// и роль «Директор» (из базы): утверждает только директор — не право, которое получил бы и администратор, а роль.
/// </summary>
public sealed record ProcurementContext(Guid UserId, IReadOnlySet<string> Permissions, Guid? DepartmentId, IReadOnlySet<Guid> HeadOf, bool IsDirector = false)
{
    /// <summary>Видит все отправленные заявки компании: снабжение, директор.</summary>
    public bool ReadsAll => Permissions.Contains(Security.Permissions.ProcurementRead) || Manages;

    /// <summary>Обрабатывает заявки: передаёт директору, возвращает на доработку, правит утверждённые (снабжение).</summary>
    public bool Manages => Permissions.Contains(Security.Permissions.ProcurementManage);

    /// <summary>Настраивает категории и правила закупок.</summary>
    public bool Configures => Permissions.Contains(Security.Permissions.ProcurementSettings);

    /// <summary>Склад: приход, выдача, списание, инвентаризация, каталог (кладовщик).</summary>
    public bool Warehouse => Permissions.Contains(Security.Permissions.ProcurementWarehouse);

    /// <summary>Создаёт свои заявки.</summary>
    public bool CanCreate => Permissions.Contains(Security.Permissions.ProcurementRequest) || ReadsAll;

    /// <summary>Раздел «Заявки» открыт.</summary>
    public bool CanOpen => CanCreate || Manages || Configures || Warehouse || IsDirector;

    /// <summary>Раздел «Снабжение» (склад) открыт: кладовщик, снабжение, директор.</summary>
    public bool SeesStock => Warehouse || ReadsAll;

    public bool Heads(Guid departmentId) => HeadOf.Contains(departmentId);
}

/// <summary>Позиция утверждённой заявки (снимок): с ним сравнивается правка — существенное изменение идёт на повторное согласование.</summary>
public sealed record ProcurementApprovedLine(Guid Id, string Name, Guid? ItemId, string Unit, decimal Quantity, decimal FromStock, decimal ToPurchase, decimal? Price);

/// <summary>Правила заявок на закупку (чистые функции — проверяются тестами): доступ, переходы статусов, номера, подписи, проверка позиций.</summary>
public static class ProcurementRules
{
    public const string RequestPrefix = "PR";
    public const string RequestDoc = "request";

    /// <summary>Префиксы складских документов: приход, выдача, списание, инвентаризация.</summary>
    public static string Prefix(ProcurementStockDocumentKind kind) => kind switch
    {
        ProcurementStockDocumentKind.Receipt => "RC",
        ProcurementStockDocumentKind.Issue => "ISS",
        ProcurementStockDocumentKind.WriteOff => "WO",
        _ => "INV",
    };

    public const int MaxItems = 100;
    public const decimal MaxQuantity = 1_000_000_000m;
    public const decimal MaxPrice = 100_000_000_000m;

    /// <summary>В работе: отправлены и ещё не закрыты.</summary>
    public static readonly ProcurementRequestStatus[] Open =
    [
        ProcurementRequestStatus.Submitted, ProcurementRequestStatus.DirectorApproval, ProcurementRequestStatus.RevisionRequired,
        ProcurementRequestStatus.Approved, ProcurementRequestStatus.PartiallyFulfilled,
    ];

    public static readonly ProcurementRequestStatus[] Closed =
        [ProcurementRequestStatus.Fulfilled, ProcurementRequestStatus.Rejected, ProcurementRequestStatus.Cancelled];

    public static string Number(string prefix, int year, long n) => $"{prefix}-{year}-{n:D5}";

    /// <summary>
    /// Видит заявку: инициатор — всегда; отправленную (не черновик) — руководитель её отдела, те, кто видит все заявки (снабжение, директор),
    /// и кладовщик — если в ней есть часть «со склада».
    /// </summary>
    public static bool CanView(ProcurementContext ctx, ProcurementRequest r) =>
        r.InitiatorId == ctx.UserId
        || (r.Status != ProcurementRequestStatus.Draft && (ctx.ReadsAll || ctx.Heads(r.DepartmentId) || ctx.IsDirector || (ctx.Warehouse && r.UsesStock)));

    public static bool IsOwner(ProcurementContext ctx, ProcurementRequest r) => r.InitiatorId == ctx.UserId;

    /// <summary>Править позиции и поля можно в черновике и после возврата на доработку — утверждённая заявка незаметно не меняется.</summary>
    public static bool Editable(ProcurementRequestStatus status) => status is ProcurementRequestStatus.Draft or ProcurementRequestStatus.RevisionRequired;

    /// <summary>
    /// Переходы статусов. Инициатор: отправить черновик или исправленную заявку, отменить до утверждения. Снабжение: передать отправленную
    /// директору или вернуть на доработку. Директор (роль, не право): утвердить, отклонить, вернуть на доработку заявку на согласовании.
    /// Повторное согласование после существенной правки утверждённой заявки — не кнопка, а следствие правки (MaterialChanges).
    /// </summary>
    public static bool CanTransition(ProcurementRequestStatus from, ProcurementRequestStatus to, bool owner, bool manages, bool director = false) => (from, to) switch
    {
        _ when from == to => false,
        (ProcurementRequestStatus.Draft or ProcurementRequestStatus.RevisionRequired, ProcurementRequestStatus.Submitted) => owner,
        (ProcurementRequestStatus.Submitted, ProcurementRequestStatus.DirectorApproval) => manages,
        (ProcurementRequestStatus.Submitted, ProcurementRequestStatus.RevisionRequired) => manages,
        (ProcurementRequestStatus.DirectorApproval, ProcurementRequestStatus.Approved or ProcurementRequestStatus.Rejected or ProcurementRequestStatus.RevisionRequired) => director,
        (ProcurementRequestStatus.Draft or ProcurementRequestStatus.Submitted or ProcurementRequestStatus.RevisionRequired
            or ProcurementRequestStatus.DirectorApproval, ProcurementRequestStatus.Cancelled) => owner,
        _ => false,
    };

    /// <summary>Со склада выдаётся по отправленной заявке (часть «со склада» не ждёт директора) и по утверждённой, пока она не выполнена.</summary>
    public static bool Issuable(ProcurementRequestStatus status) =>
        status is ProcurementRequestStatus.Submitted or ProcurementRequestStatus.DirectorApproval or ProcurementRequestStatus.Approved
            or ProcurementRequestStatus.PartiallyFulfilled;

    /// <summary>Утверждённую заявку правит снабжение (цена, количество, состав) — существенное изменение отправляет её директору повторно.</summary>
    public static bool ApprovedEditable(ProcurementRequestStatus status) =>
        status is ProcurementRequestStatus.Approved or ProcurementRequestStatus.PartiallyFulfilled;

    /// <summary>
    /// Существенные изменения утверждённой заявки (пусто — несущественные): добавлена или удалена позиция, сменился товар, выросло
    /// количество к закупке, цена за единицу или сумма к закупке выросли больше допуска (%). Уменьшение — не существенно.
    /// </summary>
    public static List<string> MaterialChanges(IReadOnlyList<ProcurementApprovedLine> approved, IReadOnlyList<ProcurementApprovedLine> current, decimal tolerancePercent)
    {
        var reasons = new List<string>();
        var before = approved.ToDictionary(l => l.Id);
        var after = current.ToDictionary(l => l.Id);
        foreach (var gone in approved.Where(l => !after.ContainsKey(l.Id)))
        {
            reasons.Add($"удалена позиция «{gone.Name}»");
        }

        foreach (var line in current)
        {
            if (!before.TryGetValue(line.Id, out var old))
            {
                reasons.Add($"добавлена позиция «{line.Name}»");
                continue;
            }

            if (old.Name != line.Name || old.ItemId != line.ItemId || old.Unit != line.Unit)
            {
                reasons.Add($"изменён товар «{old.Name}» → «{line.Name}»");
            }

            if (line.ToPurchase > old.ToPurchase)
            {
                reasons.Add($"«{line.Name}»: к закупке {Quantity(old.ToPurchase)} → {Quantity(line.ToPurchase)} {line.Unit}");
            }

            if (Grew(old.Price, line.Price, tolerancePercent))
            {
                reasons.Add($"«{line.Name}»: цена {(old.Price is { } p ? Money(p) : "—")} → {Money(line.Price ?? 0)}");
            }
        }

        decimal PurchaseSum(IEnumerable<ProcurementApprovedLine> lines) => lines.Sum(l => Sum(l.ToPurchase, l.Price) ?? 0m);
        var (was, now) = (PurchaseSum(approved), PurchaseSum(current));
        if (reasons.Count == 0 && Grew(was, now, tolerancePercent))
        {
            reasons.Add($"сумма к закупке {Money(was)} → {Money(now)}");
        }

        return reasons;
    }

    /// <summary>Выросло больше допуска: цены не было, а появилась — тоже рост (директор утверждал без неё).</summary>
    private static bool Grew(decimal? was, decimal? now, decimal tolerancePercent) => (was, now) switch
    {
        (_, null) => false,
        (null, { } n) => n > 0,
        ({ } w, { } n) when w <= 0 => n > 0,
        ({ } w, { } n) => (n - w) / w * 100m > tolerancePercent,
    };

    /// <summary>
    /// Статус позиции по её количествам: выдана (вся), выдана частично; у отправленной заявки — поступила на склад (закупленное принято
    /// целиком), заказана, к закупке (есть часть к закупке) или зарезервирована (вся со склада); у неотправленной — ожидает. Отмена заявки
    /// ставит «Отменена» отдельно.
    /// </summary>
    public static ProcurementItemStatus ItemStatus(ProcurementRequestItem item, bool submitted) =>
        item.Quantity > 0 && item.IssuedQty >= item.Quantity ? ProcurementItemStatus.Issued
        : item.IssuedQty > 0 ? ProcurementItemStatus.PartiallyIssued
        : !submitted ? ProcurementItemStatus.Pending
        : item.ToPurchase > 0 && item.ReceivedQty >= item.ToPurchase ? ProcurementItemStatus.Received
        : item.ToPurchase > 0 && item.OrderedQty >= item.ToPurchase ? ProcurementItemStatus.Ordered
        : item.ToPurchase > 0 ? ProcurementItemStatus.ToPurchase
        : ProcurementItemStatus.Reserved;

    /// <summary>Возврат на доработку и отклонение — только с комментарием.</summary>
    public static bool CommentRequired(ProcurementRequestStatus to) => to is ProcurementRequestStatus.RevisionRequired or ProcurementRequestStatus.Rejected;

    public static string Label(ProcurementRequestStatus status) => status switch
    {
        ProcurementRequestStatus.Draft => "Черновик",
        ProcurementRequestStatus.Submitted => "Отправлена",
        ProcurementRequestStatus.DirectorApproval => "Ожидает директора",
        ProcurementRequestStatus.Approved => "Утверждена",
        ProcurementRequestStatus.Rejected => "Отклонена",
        ProcurementRequestStatus.RevisionRequired => "Требуется доработка",
        ProcurementRequestStatus.PartiallyFulfilled => "Частично выполнена",
        ProcurementRequestStatus.Fulfilled => "Выполнена",
        ProcurementRequestStatus.Cancelled => "Отменена",
        _ => status.ToString(),
    };

    public static string Label(ProcurementPriority priority) => priority switch
    {
        ProcurementPriority.Low => "Низкий",
        ProcurementPriority.High => "Высокий",
        ProcurementPriority.Urgent => "Срочный",
        _ => "Обычный",
    };

    public static string Label(ProcurementItemStatus status) => status switch
    {
        ProcurementItemStatus.Cancelled => "Отменена",
        ProcurementItemStatus.Reserved => "Зарезервирована",
        ProcurementItemStatus.ToPurchase => "К закупке",
        ProcurementItemStatus.PartiallyIssued => "Выдана частично",
        ProcurementItemStatus.Issued => "Выдана",
        ProcurementItemStatus.Ordered => "Заказана",
        ProcurementItemStatus.Received => "Поступила на склад",
        _ => "Ожидает",
    };

    /// <summary>Сумма позиции: количество × цена, если цена указана.</summary>
    public static decimal? Sum(decimal quantity, decimal? price) => price is { } p ? Math.Round(quantity * p, 2) : null;

    /// <summary>Предполагаемая сумма заявки — по позициям с ценой (отменённые не считаются).</summary>
    public static decimal Total(IEnumerable<(decimal Quantity, decimal? Price)> items) => items.Sum(i => Sum(i.Quantity, i.Price) ?? 0m);

    /// <summary>Проверка числа позиции: количество больше нуля (до 3 знаков), цена не отрицательная (до 2 знаков). Ошибка — текст, иначе null.</summary>
    public static string? CheckNumbers(decimal quantity, decimal? price)
    {
        if (quantity <= 0)
        {
            return "Количество должно быть больше нуля.";
        }

        if (quantity > MaxQuantity || decimal.Round(quantity, 3) != quantity)
        {
            return "Количество — до миллиарда, не больше трёх знаков после запятой.";
        }

        if (price is { } p && (p < 0 || p > MaxPrice || decimal.Round(p, 2) != p))
        {
            return "Цена — не отрицательная, до 100 млрд, не больше двух знаков после запятой.";
        }

        return null;
    }

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>«1 250 000» — для уведомлений и истории.</summary>
    public static string Money(decimal value) => value.ToString("#,0.##", Ru);

    public static string Quantity(decimal value) => value.ToString("#,0.###", Ru);
}

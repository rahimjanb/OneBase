using System.Text;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Reports;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

public sealed record ProcurementFile(string FileName, string ContentType, byte[] Content);

public sealed partial class ProcurementRequestService
{
    public const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>
    /// Заявка документом Word: реквизиты (статус, отдел, инициатор, приоритет, даты), позиции таблицей с суммами и разбивкой «со склада /
    /// к закупке», причина и комментарий, решения директора, заказы поставщикам, строки для подписей. Права — как у карточки заявки (кто её
    /// не видит, получает 403). Язык подписей — язык интерфейса скачивающего.
    /// </summary>
    public async Task<ProcurementFile> DocumentAsync(ProcurementContext ctx, Guid id, IDocumentWriter writer, CancellationToken ct)
    {
        var r = await GetAsync(ctx, id, ct);
        var uz = UiLanguage.IsUzbek;
        string L(string ru, string uzText) => uz ? UiLanguage.Uzbek(uzText) : ru;
        string Cell(string? text) => string.IsNullOrWhiteSpace(text) ? "—" : text.Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();
        string Stamp(DateTimeOffset? at) => at is { } v ? v.ToOffset(FieldClock.Offset).ToString("dd.MM.yyyy HH:mm") : "—";

        var md = new StringBuilder();
        md.AppendLine($"# {L("Заявка на закупку", "Xarid arizasi")} {r.Number}");
        md.AppendLine();
        md.AppendLine($"**{Cell(r.Title)}**");
        md.AppendLine();
        md.AppendLine($"| {L("Реквизит", "Rekvizit")} | {L("Значение", "Qiymat")} |");
        md.AppendLine("|---|---|");
        md.AppendLine($"| {L("Статус", "Holat")} | {Cell(L(ProcurementRules.Label(r.Status), StatusUz(r.Status)))} |");
        md.AppendLine($"| {L("Отдел", "Bo'lim")} | {Cell(r.DepartmentName)} |");
        md.AppendLine($"| {L("Инициатор", "Tashabbuskor")} | {Cell(r.InitiatorPosition is { } position ? $"{r.InitiatorName}, {position}" : r.InitiatorName)} |");
        md.AppendLine($"| {L("Приоритет", "Ustuvorlik")} | {Cell(L(ProcurementRules.Label(r.Priority), PriorityUz(r.Priority)))} |");
        md.AppendLine($"| {L("Желаемая дата", "Kerakli sana")} | {(r.DesiredDate is { } desired ? desired.ToString("dd.MM.yyyy") : "—")} |");
        md.AppendLine($"| {L("Создана", "Yaratilgan")} | {Stamp(r.CreatedAt)} |");
        md.AppendLine($"| {L("Отправлена", "Yuborilgan")} | {Stamp(r.SubmittedAt)} |");
        if (r.ApprovedAt is not null)
        {
            md.AppendLine($"| {L("Утверждена", "Tasdiqlangan")} | {Stamp(r.ApprovedAt)}{(r.ApprovedByName is { } by ? $", {Cell(by)}" : "")} |");
        }

        md.AppendLine();
        md.AppendLine($"## {L("Позиции", "Pozitsiyalar")}");
        md.AppendLine();
        md.AppendLine($"| № | {L("Наименование", "Nomi")} | {L("Кол-во", "Miqdor")} | {L("Ед.", "Birl.")} | {L("Цена, сум", "Narx, so'm")} | {L("Сумма, сум", "Summa, so'm")} | {L("Со склада", "Ombordan")} | {L("К закупке", "Xaridga")} |");
        md.AppendLine("|---|---|---|---|---|---|---|---|");
        var active = r.Items.Where(i => i.Status != ProcurementItemStatus.Cancelled).ToList();
        foreach (var i in active)
        {
            var name = Cell(i.Name) + (i.Sku is { } sku ? $" ({Cell(sku)})" : "") + (i.Comment is { } note ? $" — {Cell(note)}" : "");
            md.AppendLine($"| {i.LineNo} | {name} | {ProcurementRules.Quantity(i.Quantity)} | {Cell(i.UnitName)} | {(i.EstimatedPrice is { } p ? ProcurementRules.Money(p) : "—")} | " +
                          $"{(i.Sum is { } s ? ProcurementRules.Money(s) : "—")} | {(i.FromStock > 0 ? ProcurementRules.Quantity(i.FromStock) : "—")} | {(i.ToPurchase > 0 ? ProcurementRules.Quantity(i.ToPurchase) : "—")} |");
        }

        md.AppendLine($"|  | **{L("Итого", "Jami")}** |  |  |  | **{ProcurementRules.Money(r.EstimatedTotal)}** |  |  |");
        md.AppendLine();
        md.AppendLine($"{L("Позиций", "Pozitsiyalar")}: {active.Count}. {L("К закупке на сумму", "Xarid summasi")}: {ProcurementRules.Money(r.PurchaseTotal)} {L("сум", "so'm")}.");
        var cancelled = r.Items.Count - active.Count;
        if (cancelled > 0)
        {
            md.AppendLine($"{L("Отменённых позиций", "Bekor qilingan pozitsiyalar")}: {cancelled}.");
        }

        if (!string.IsNullOrWhiteSpace(r.Reason))
        {
            md.AppendLine();
            md.AppendLine($"## {L("Причина закупки", "Xarid sababi")}");
            md.AppendLine();
            md.AppendLine(r.Reason.Trim());
        }

        if (!string.IsNullOrWhiteSpace(r.Comment))
        {
            md.AppendLine();
            md.AppendLine($"## {L("Комментарий", "Izoh")}");
            md.AppendLine();
            md.AppendLine(r.Comment.Trim());
        }

        if (r.Approvals is { Count: > 0 } approvals)
        {
            md.AppendLine();
            md.AppendLine($"## {L("Решения директора", "Direktor qarorlari")}");
            md.AppendLine();
            foreach (var a in approvals)
            {
                var decision = a.Decision switch
                {
                    ProcurementApprovalDecision.Approved => L("Утверждена", "Tasdiqlandi"),
                    ProcurementApprovalDecision.Rejected => L("Отклонена", "Rad etildi"),
                    _ => L("Возвращена на доработку", "Qayta ishlashga qaytarildi"),
                };
                md.AppendLine($"- {L("Раунд", "Bosqich")} {a.Round}: **{decision}** — {Cell(a.DeciderName)}, {Stamp(a.CreatedAt)}{(a.Comment is { } c ? $". {Cell(c)}" : "")}");
            }
        }

        if (r.Orders is { Count: > 0 } orders)
        {
            md.AppendLine();
            md.AppendLine($"## {L("Заказы поставщикам", "Yetkazib beruvchilarga buyurtmalar")}");
            md.AppendLine();
            md.AppendLine($"| {L("Заказ", "Buyurtma")} | {L("Поставщик", "Yetkazib beruvchi")} | {L("Статус", "Holat")} | {L("Итог, сум", "Jami, so'm")} | {L("Ожидается", "Kutilmoqda")} |");
            md.AppendLine("|---|---|---|---|---|");
            foreach (var o in orders)
            {
                md.AppendLine($"| {o.Number} | {Cell(o.SupplierName)} | {Cell(L(ProcurementOrderRules.Label(o.Status), OrderStatusUz(o.Status)))} | {ProcurementRules.Money(o.Total)} | " +
                              $"{(o.ExpectedDate is { } e ? e.ToString("dd.MM.yyyy") : "—")} |");
            }
        }

        md.AppendLine();
        md.AppendLine($"## {L("Подписи", "Imzolar")}");
        md.AppendLine();
        md.AppendLine($"{L("Инициатор", "Tashabbuskor")}: …………………………… / {Cell(r.InitiatorName)} /");
        md.AppendLine();
        md.AppendLine($"{L("Руководитель отдела", "Bo'lim rahbari")}: …………………………… / …………………………… /");
        md.AppendLine();
        md.AppendLine($"{L("Директор", "Direktor")}: …………………………… / …………………………… /");
        var content = writer.Write($"{L("Заявка", "Ariza")} {r.Number} — {r.Title}", md.ToString(), DateTimeOffset.UtcNow);
        return new ProcurementFile($"{r.Number}.docx", DocxType, content);
    }

    internal static string StatusUz(ProcurementRequestStatus status) => status switch
    {
        ProcurementRequestStatus.Draft => "Qoralama",
        ProcurementRequestStatus.Submitted => "Yuborilgan",
        ProcurementRequestStatus.HeadApproval => "Bo'lim rahbarini kutmoqda",
        ProcurementRequestStatus.FinanceApproval => "Moliyachini kutmoqda",
        ProcurementRequestStatus.SupplyApproval => "Ta'minot rahbarini kutmoqda",
        ProcurementRequestStatus.AnalystApproval => "Tahlilchini kutmoqda",
        ProcurementRequestStatus.DirectorApproval => "Direktorni kutmoqda",
        ProcurementRequestStatus.Approved => "Tasdiqlangan",
        ProcurementRequestStatus.Rejected => "Rad etilgan",
        ProcurementRequestStatus.RevisionRequired => "Qayta ishlash kerak",
        ProcurementRequestStatus.PartiallyFulfilled => "Qisman bajarilgan",
        ProcurementRequestStatus.Fulfilled => "Bajarilgan",
        ProcurementRequestStatus.Cancelled => "Bekor qilingan",
        _ => status.ToString(),
    };

    internal static string PriorityUz(ProcurementPriority priority) => priority switch
    {
        ProcurementPriority.Low => "Past",
        ProcurementPriority.High => "Yuqori",
        ProcurementPriority.Urgent => "Shoshilinch",
        _ => "Oddiy",
    };

    internal static string OrderStatusUz(ProcurementOrderStatus status) => status switch
    {
        ProcurementOrderStatus.Draft => "Qoralama",
        ProcurementOrderStatus.AwaitingApproval => "Direktorni kutmoqda",
        ProcurementOrderStatus.Approved => "Tasdiqlangan",
        ProcurementOrderStatus.Ordered => "Buyurtma qilingan",
        ProcurementOrderStatus.PartiallyDelivered => "Qisman yetkazilgan",
        ProcurementOrderStatus.Delivered => "Yetkazilgan",
        ProcurementOrderStatus.Completed => "Yopilgan",
        ProcurementOrderStatus.Cancelled => "Bekor qilingan",
        _ => status.ToString(),
    };
}

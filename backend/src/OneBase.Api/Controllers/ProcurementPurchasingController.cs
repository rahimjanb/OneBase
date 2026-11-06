using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;

namespace OneBase.Api.Controllers;

/// <summary>
/// Снабжение — закупки: поставщики (реестр, карточка, рейтинг, импорт из Excel), заявки к закупке и «Куплено» (покупка сразу на склад),
/// журнал тендерных закупок,
/// коммерческие предложения и их сравнение, заказы
/// поставщикам (только из утверждённой потребности, согласование директором, статусы, история), приёмка товара (черновик →
/// подтверждение, брак, недостача, претензии), документы. Доступ решают сервисы по
/// правам из токена и роли «Директор» из базы — никогда по параметрам запроса.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/procurement")]
public sealed class ProcurementPurchasingController(
    ProcurementRequestService requests,
    ProcurementSupplierService suppliers,
    ProcurementOrderService orders,
    ProcurementReceiptService receipts,
    ProcurementPurchaseService purchases,
    ProcurementTenderService tenders,
    ProcurementDocumentService documents,
    IFileStorage storage) : ControllerBase
{
    private Task<ProcurementContext> ContextAsync(CancellationToken ct) =>
        requests.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    // ---------- Поставщики ----------

    [HttpGet("suppliers")]
    public async Task<PageResult<ProcurementSupplierRow>> Suppliers(
        [FromQuery] string? q = null,
        [FromQuery] Guid? category = null,
        [FromQuery] string? active = "true",
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await suppliers.SearchAsync(await ContextAsync(ct),
            new ProcurementSupplierSearch(q, category, active switch { "false" => false, "all" => null, _ => true }, sort, desc, page, pageSize), ct);

    [HttpGet("suppliers/options")]
    public async Task<List<ProcurementSupplierRow>> SupplierOptions(CancellationToken ct) => await suppliers.OptionsAsync(await ContextAsync(ct), ct);

    [HttpGet("suppliers/{id:guid}")]
    public async Task<ProcurementSupplierCard> Supplier(Guid id, CancellationToken ct) => await suppliers.GetAsync(await ContextAsync(ct), id, ct);

    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier(ProcurementSupplierInput input, CancellationToken ct) =>
        Ok(new { id = await suppliers.CreateAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("suppliers/{id:guid}")]
    public async Task<IActionResult> UpdateSupplier(Guid id, ProcurementSupplierInput input, CancellationToken ct)
    {
        await suppliers.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    /// <summary>
    /// Импорт поставщиков из Excel (.xlsx) или CSV: по ИНН (без ИНН — по названию) создаёт или обновляет. dryRun=true — проверка файла:
    /// что и куда попадёт, без сохранения.
    /// </summary>
    [HttpPost("suppliers/import")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ProcurementSupplierImportResult> ImportSuppliers(IFormFile? file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new FieldValidationException("Выберите файл Excel или CSV.");
        }

        await using var stream = file.OpenReadStream();
        return await suppliers.ImportAsync(await ContextAsync(ct), stream, file.FileName, ct, dryRun);
    }

    // ---------- Документы поставщиков, предложений, заказов ----------

    [HttpGet("documents")]
    public async Task<List<ProcurementDocumentView>> Documents([FromQuery] string owner, [FromQuery] Guid id, CancellationToken ct) =>
        await documents.ListAsync(await ContextAsync(ct), owner, id, ct);

    [HttpPost("documents")]
    [RequestSizeLimit(ProcurementRequestService.MaxAttachmentBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProcurementRequestService.MaxAttachmentBytes + 1024 * 1024)]
    public async Task<IActionResult> Attach([FromQuery] string owner, [FromQuery] Guid id, [FromQuery] string? kind, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw new FieldValidationException("Выберите файл.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(new { id = await documents.AttachAsync(await ContextAsync(ct), owner, id, new ProcurementUpload(file.FileName, file.Length, stream), kind, storage, ct) });
    }

    // ---------- Предложения ----------

    /// <summary>Сравнение предложений по заявке: позиции «к закупке» и предложения с лучшими ценой, итогом и сроком.</summary>
    [HttpGet("requests/{id:guid}/offers")]
    public async Task<ProcurementComparison> Offers(Guid id, CancellationToken ct) => await orders.CompareAsync(await ContextAsync(ct), id, suppliers, ct);

    [HttpPost("requests/{id:guid}/offers")]
    public async Task<IActionResult> CreateOffer(Guid id, ProcurementOfferInput input, CancellationToken ct) =>
        Ok(new { id = await orders.CreateOfferAsync(await ContextAsync(ct), id, input, ct) });

    [HttpPut("offers/{id:guid}")]
    public async Task<IActionResult> UpdateOffer(Guid id, ProcurementOfferInput input, CancellationToken ct)
    {
        await orders.UpdateOfferAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("offers/{id:guid}/reject")]
    public async Task<IActionResult> RejectOffer(Guid id, CancellationToken ct)
    {
        await orders.SetOfferStatusAsync(await ContextAsync(ct), id, true, ct);
        return NoContent();
    }

    [HttpPost("offers/{id:guid}/restore")]
    public async Task<IActionResult> RestoreOffer(Guid id, CancellationToken ct)
    {
        await orders.SetOfferStatusAsync(await ContextAsync(ct), id, false, ct);
        return NoContent();
    }

    // ---------- К закупке и «Куплено» ----------

    /// <summary>Утверждённые заявки, по которым снабжению осталось что-то купить (снабжение, директор).</summary>
    [HttpGet("to-buy")]
    public async Task<ProcurementToBuyView> ToBuy(CancellationToken ct) => await purchases.ToBuyAsync(await ContextAsync(ct), ct);

    /// <summary>«Куплено» по заявке (снабжение): покупка сразу приходуется на склад и резервируется под заявку — к выдаче.</summary>
    [HttpPost("requests/{id:guid}/purchase")]
    public async Task<ProcurementPurchaseResult> Purchase(Guid id, ProcurementPurchaseInput input, CancellationToken ct) =>
        await purchases.PurchaseAsync(await ContextAsync(ct), id, input, ct);

    // ---------- Тендерные закупки ----------

    /// <summary>Журнал тендерных закупок: q, status (waiting, overdue, delivered, cancelled), from/to (дата тендера), supplier, страница.</summary>
    [HttpGet("tenders")]
    public async Task<ProcurementTenderList> Tenders(
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? supplier = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        await tenders.SearchAsync(await ContextAsync(ct), new ProcurementTenderSearch(q, status, from, to, supplier, page, pageSize), ct);

    [HttpGet("tenders/{id:guid}")]
    public async Task<ProcurementTenderDetail> Tender(Guid id, CancellationToken ct) => await tenders.GetAsync(await ContextAsync(ct), id, ct);

    [HttpPost("tenders")]
    public async Task<IActionResult> CreateTender(ProcurementTenderInput input, CancellationToken ct) =>
        Ok(new { id = await tenders.CreateAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("tenders/{id:guid}")]
    public async Task<IActionResult> UpdateTender(Guid id, ProcurementTenderInput input, CancellationToken ct)
    {
        await tenders.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    /// <summary>Поставлено: дата (по умолчанию — сегодня).</summary>
    /// <summary>Поставка по тендеру на склад (кладовщик): приход партиями, у позиций — «поставлено».</summary>
    [HttpPost("tenders/{id:guid}/delivery")]
    public async Task<IActionResult> TenderDelivery(Guid id, ProcurementTenderDeliveryInput input, CancellationToken ct) =>
        Ok(new { id = await tenders.DeliverAsync(await ContextAsync(ct), id, input, ct) });

    [HttpPost("tenders/{id:guid}/delivered")]
    public async Task<IActionResult> TenderDelivered(Guid id, DeliveredBody body, CancellationToken ct)
    {
        await tenders.DeliveredAsync(await ContextAsync(ct), id, body.Date, ct);
        return NoContent();
    }

    /// <summary>Отменить запись — причина обязательна; запись остаётся в журнале.</summary>
    [HttpPost("tenders/{id:guid}/cancel")]
    public async Task<IActionResult> CancelTender(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await tenders.CancelAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    // ---------- Заказы ----------

    /// <summary>Заказы: status (open, awaiting, overdue, expected, partial, done, cancelled или статусы), supplier, responsible, q, from/to, сортировка.</summary>
    [HttpGet("orders")]
    public async Task<PageResult<ProcurementOrderRow>> Orders(
        [FromQuery] string? status = null,
        [FromQuery] Guid? supplier = null,
        [FromQuery] Guid? responsible = null,
        [FromQuery] string? q = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        await orders.SearchAsync(await ContextAsync(ct), new ProcurementOrderSearch(status, supplier, responsible, q, from, to, sort, desc, page, pageSize), ct);

    [HttpGet("orders/summary")]
    public async Task<ProcurementOrderSummary> OrdersSummary(CancellationToken ct) => await orders.SummaryAsync(await ContextAsync(ct), ct);

    /// <summary>Утверждённые потребности, ещё не заказанные целиком (для нового заказа).</summary>
    [HttpGet("orders/needs")]
    public async Task<List<ProcurementNeedRow>> Needs([FromQuery] Guid? request, CancellationToken ct) => await orders.NeedsAsync(await ContextAsync(ct), request, ct);

    [HttpGet("orders/responsibles")]
    public async Task<List<ProcurementUserOption>> Responsibles(CancellationToken ct) => await orders.ResponsiblesAsync(await ContextAsync(ct), ct);

    [HttpGet("orders/{id:guid}")]
    public async Task<ProcurementOrderDetail> Order(Guid id, CancellationToken ct) => await orders.GetAsync(await ContextAsync(ct), id, ct);

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(ProcurementOrderInput input, CancellationToken ct) =>
        Ok(new { id = await orders.CreateAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("orders/{id:guid}")]
    public async Task<IActionResult> UpdateOrder(Guid id, ProcurementOrderInput input, CancellationToken ct)
    {
        await orders.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/submit")]
    public async Task<IActionResult> SubmitOrder(Guid id, CancellationToken ct)
    {
        await orders.SubmitAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    /// <summary>Решения по заказу — только директор (роль из базы).</summary>
    [HttpPost("orders/{id:guid}/approve")]
    public async Task<IActionResult> ApproveOrder(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await orders.ApproveAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/reject")]
    public async Task<IActionResult> RejectOrder(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await orders.RejectAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/return")]
    public async Task<IActionResult> ReturnOrder(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await orders.ReturnAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/ordered")]
    public async Task<IActionResult> MarkOrdered(Guid id, OrderedBody body, CancellationToken ct)
    {
        await orders.MarkOrderedAsync(await ContextAsync(ct), id, body.OrderDate, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await orders.CancelAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    /// <summary>Закрыть недопоставленный остаток — поставщик больше не довезёт (причина обязательна).</summary>
    [HttpPost("orders/{id:guid}/close-remainder")]
    public async Task<IActionResult> CloseRemainder(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await orders.CloseRemainderAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    [HttpPost("orders/{id:guid}/complete")]
    public async Task<IActionResult> CompleteOrder(Guid id, CancellationToken ct)
    {
        await orders.CompleteAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    // ---------- Приёмка ----------

    /// <summary>Заказы, которые ждут приёмки (заказаны или поставлены частично).</summary>
    [HttpGet("receipts/expected")]
    public async Task<List<ProcurementExpectedRow>> ExpectedReceipts(CancellationToken ct) => await receipts.ExpectedAsync(await ContextAsync(ct), ct);

    [HttpGet("receipts")]
    public async Task<PageResult<ProcurementReceiptRow>> Receipts(
        [FromQuery] string? status = null,
        [FromQuery] Guid? order = null,
        [FromQuery] Guid? supplier = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        await receipts.SearchAsync(await ContextAsync(ct), new ProcurementReceiptSearch(status, order, supplier, q, page, pageSize), ct);

    /// <summary>Строки заказа для новой приёмки: заказано, получено, ждёт поставки, карточка склада.</summary>
    [HttpGet("receipts/form")]
    public async Task<ProcurementReceiptForm> ReceiptForm([FromQuery] Guid order, CancellationToken ct) => await receipts.FormAsync(await ContextAsync(ct), order, ct);

    [HttpGet("receipts/{id:guid}")]
    public async Task<ProcurementReceiptDetail> Receipt(Guid id, CancellationToken ct) => await receipts.GetAsync(await ContextAsync(ct), id, ct);

    /// <summary>Новая приёмка: черновик (склад не меняется) или сразу подтверждённая (post = true).</summary>
    [HttpPost("receipts")]
    public async Task<IActionResult> CreateReceipt(ProcurementReceiptInput input, CancellationToken ct) =>
        Ok(new { id = await receipts.CreateAsync(await ContextAsync(ct), input, ct) });

    [HttpPut("receipts/{id:guid}")]
    public async Task<IActionResult> UpdateReceipt(Guid id, ProcurementReceiptInput input, CancellationToken ct)
    {
        await receipts.UpdateAsync(await ContextAsync(ct), id, input, ct);
        return NoContent();
    }

    /// <summary>Подтвердить приёмку: приход на склад, резерв под заявки, статус поставки заказа.</summary>
    [HttpPost("receipts/{id:guid}/post")]
    public async Task<IActionResult> PostReceipt(Guid id, VersionBody body, CancellationToken ct)
    {
        await receipts.PostAsync(await ContextAsync(ct), id, body.Version, ct);
        return NoContent();
    }

    [HttpDelete("receipts/{id:guid}")]
    public async Task<IActionResult> DeleteReceipt(Guid id, CancellationToken ct)
    {
        await receipts.DeleteAsync(await ContextAsync(ct), id, ct);
        return NoContent();
    }

    /// <summary>Закрыть претензию поставщику решением (снабжение).</summary>
    [HttpPost("claims/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveClaim(Guid id, ProcurementController.CommentBody body, CancellationToken ct)
    {
        await receipts.ResolveClaimAsync(await ContextAsync(ct), id, body.Comment, ct);
        return NoContent();
    }

    public sealed record OrderedBody(DateOnly? OrderDate);

    public sealed record DeliveredBody(DateOnly? Date);

    public sealed record VersionBody(uint? Version);
}

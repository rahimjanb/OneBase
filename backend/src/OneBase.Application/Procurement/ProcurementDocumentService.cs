using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Work;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>
/// Документы поставщиков, коммерческих предложений, заказов, приёмок и тендерных закупок (договор, КП, счёт, накладная, акт, платёжное
/// поручение, протокол тендера), партий и проверок качества (сертификат, декларация, протокол испытаний): файл в
/// MinIO (procurement/{тип}/{владелец}/{файл}), описание — в базе. Видят: поставщика, заказ и приёмку — снабжение, директор, кладовщик;
/// предложение — снабжение и директор; партию — все, кто видит склад; проверку качества — все, кто видит контроль качества. Прикрепляет
/// снабжение (к заказу и приёмке — и кладовщик: накладные, акты; к партии и проверке — кладовщик, лаборатория и назначенные по качеству).
/// Удаляет загрузивший или снабжение.
/// Вложения заявок — в ProcurementRequestService.
/// </summary>
public sealed class ProcurementDocumentService(IAppDbContext db)
{
    /// <summary>
    /// Виды: договор, коммерческое предложение, счёт, накладная, акт (приёмки), платёжное поручение (пока только файл — оплаты придут с
    /// финансами), протокол итогов тендера; сертификат качества, декларация соответствия, протокол испытаний, спецификация; прочее.
    /// </summary>
    private static readonly HashSet<string> Kinds =
        ["contract", "offer", "invoice", "waybill", "act", "payment", "protocol", "certificate", "declaration", "lab_report", "specification", "other"];

    public async Task<List<ProcurementDocumentView>> ListAsync(ProcurementContext ctx, string ownerType, Guid ownerId, CancellationToken ct)
    {
        await RequireViewAsync(ctx, ownerType, ownerId, ct);
        var docs = await db.ProcurementDocuments.AsNoTracking().Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId).OrderBy(d => d.CreatedAt)
            .Select(d => new { Doc = d, By = d.UploadedBy == null ? null : d.UploadedBy.FullName })
            .ToListAsync(ct);
        return docs.Select(x => new ProcurementDocumentView(x.Doc.Id, x.Doc.FileName, x.Doc.ContentType, x.Doc.SizeBytes, x.By, x.Doc.CreatedAt,
            CanDelete(ctx, x.Doc), x.Doc.Kind)).ToList();
    }

    public async Task<Guid> AttachAsync(ProcurementContext ctx, string ownerType, Guid ownerId, ProcurementUpload upload, string? kind, IFileStorage storage, CancellationToken ct)
    {
        await RequireViewAsync(ctx, ownerType, ownerId, ct);
        var quality = ownerType is ProcurementQualityService.BatchDoc or ProcurementQualityService.CheckDoc;
        if (!(ctx.Manages || (ownerType is ProcurementOrderRules.OrderDoc or ProcurementOrderRules.ReceiptDoc && ctx.Warehouse)
              || (quality && (ctx.Warehouse || ctx.Quality || ctx.DecidesQuality))))
        {
            throw new FieldForbiddenException("Документы прикрепляет снабжение (к заказу и приёмке — и кладовщик, к партии и проверке качества — кладовщик и лаборатория).");
        }

        if (upload.SizeBytes <= 0)
        {
            throw new FieldValidationException("Файл пустой.");
        }

        if (upload.SizeBytes > ProcurementRequestService.MaxAttachmentBytes)
        {
            throw new FieldValidationException($"Файл больше {ProcurementRequestService.MaxAttachmentBytes / 1024 / 1024} МБ.");
        }

        var name = WorkTaskService.SafeFileName(upload.FileName);
        if (!ProcurementRequestService.AllowedTypes.TryGetValue(Path.GetExtension(name), out var contentType))
        {
            throw new FieldValidationException("Можно прикрепить PDF, Word, Excel, PowerPoint, картинку, текст или ZIP.");
        }

        if (await db.ProcurementDocuments.CountAsync(d => d.OwnerType == ownerType && d.OwnerId == ownerId, ct) >= 50)
        {
            throw new FieldValidationException("Документов — до 50.");
        }

        var doc = new ProcurementDocument
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            Kind = kind is not null && Kinds.Contains(kind) ? kind : null,
            FileName = name,
            ContentType = contentType,
            SizeBytes = upload.SizeBytes,
            ObjectKey = string.Empty,
            UploadedById = ctx.UserId,
        };
        doc.ObjectKey = $"procurement/{ownerType}s/{ownerId:N}/{doc.Id:N}";
        await storage.PutAsync(doc.ObjectKey, upload.Content, upload.SizeBytes, contentType, ct);
        db.ProcurementDocuments.Add(doc);
        Log(ctx, ownerType, ownerId, $"Документ: {name}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(doc.ObjectKey, CancellationToken.None);
            throw;
        }

        return doc.Id;
    }

    /// <summary>Файл к скачиванию; null — документ заявки (его отдаёт ProcurementRequestService со своими правилами).</summary>
    public async Task<ProcurementDownload?> DownloadAsync(ProcurementContext ctx, Guid documentId, CancellationToken ct)
    {
        var doc = await db.ProcurementDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, ct) ?? throw new FieldNotFoundException("Файл не найден.");
        if (doc.OwnerType == ProcurementRules.RequestDoc)
        {
            return null;
        }

        await RequireViewAsync(ctx, doc.OwnerType, doc.OwnerId, ct);
        return new ProcurementDownload(doc.FileName,
            ProcurementRequestService.AllowedTypes.GetValueOrDefault(Path.GetExtension(doc.FileName), "application/octet-stream"), doc.ObjectKey);
    }

    /// <summary>Удалить документ; false — документ заявки (удаляет ProcurementRequestService).</summary>
    public async Task<bool> DeleteAsync(ProcurementContext ctx, Guid documentId, IFileStorage storage, CancellationToken ct)
    {
        var doc = await db.ProcurementDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct) ?? throw new FieldNotFoundException("Файл не найден.");
        if (doc.OwnerType == ProcurementRules.RequestDoc)
        {
            return false;
        }

        await RequireViewAsync(ctx, doc.OwnerType, doc.OwnerId, ct);
        if (!CanDelete(ctx, doc))
        {
            throw new FieldForbiddenException("Удалить документ может тот, кто его загрузил, или снабжение.");
        }

        db.ProcurementDocuments.Remove(doc);
        Log(ctx, doc.OwnerType, doc.OwnerId, $"Удалён документ: {doc.FileName}");
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(doc.ObjectKey, ct);
        return true;
    }

    private static bool CanDelete(ProcurementContext ctx, ProcurementDocument d) => d.UploadedById == ctx.UserId || ctx.Manages;

    /// <summary>Видит ли пользователь владельца документа; нет владельца — 404, не видит — 403.</summary>
    private async Task RequireViewAsync(ProcurementContext ctx, string ownerType, Guid ownerId, CancellationToken ct)
    {
        var buyer = ctx.ReadsAll || ctx.IsDirector;
        switch (ownerType)
        {
            case ProcurementOrderRules.SupplierDoc:
                if (!await db.ProcurementSuppliers.AnyAsync(s => s.Id == ownerId, ct))
                {
                    throw new FieldNotFoundException("Поставщик не найден.");
                }

                if (!buyer && !ctx.Warehouse)
                {
                    throw new FieldForbiddenException("Поставщиков видят снабжение, директор и кладовщик.");
                }

                break;
            case ProcurementOrderRules.OfferDoc:
                if (!await db.ProcurementOffers.AnyAsync(o => o.Id == ownerId, ct))
                {
                    throw new FieldNotFoundException("Предложение не найдено.");
                }

                if (!buyer)
                {
                    throw new FieldForbiddenException("Коммерческие предложения видят снабжение и директор.");
                }

                break;
            case ProcurementOrderRules.OrderDoc:
                if (!await db.ProcurementPurchaseOrders.AnyAsync(o => o.Id == ownerId, ct))
                {
                    throw new FieldNotFoundException("Заказ не найден.");
                }

                if (!buyer && !ctx.Warehouse)
                {
                    throw new FieldForbiddenException("Заказы поставщикам видят снабжение, директор и кладовщик.");
                }

                break;
            case ProcurementOrderRules.ReceiptDoc:
                if (!await db.ProcurementStockDocuments.AnyAsync(d => d.Id == ownerId && d.OrderId != null, ct))
                {
                    throw new FieldNotFoundException("Приёмка не найдена.");
                }

                if (!buyer && !ctx.Warehouse)
                {
                    throw new FieldForbiddenException("Приёмку видят кладовщик, снабжение и директор.");
                }

                break;
            case ProcurementQualityService.BatchDoc:
                if (!await db.ProcurementBatches.AnyAsync(b => b.Id == ownerId, ct))
                {
                    throw new FieldNotFoundException("Партия не найдена.");
                }

                if (!ctx.SeesStock)
                {
                    throw new FieldForbiddenException("Документы партии видят кладовщик, лаборатория, снабжение и директор.");
                }

                break;
            case ProcurementQualityService.CheckDoc:
                if (!await db.ProcurementQualityChecks.AnyAsync(c => c.Id == ownerId, ct))
                {
                    throw new FieldNotFoundException("Проверка качества не найдена.");
                }

                if (!ctx.SeesQuality)
                {
                    throw new FieldForbiddenException("Документы проверки качества видят лаборатория, кладовщик, снабжение и директор.");
                }

                break;
            case ProcurementTenderService.Doc:
                if (!await db.ProcurementTenders.AnyAsync(t => t.Id == ownerId, ct))
                {
                    throw new FieldNotFoundException("Тендерная закупка не найдена.");
                }

                // Кладовщик принимает поставку по тендеру — видит его документы (договор, накладные).
                if (!buyer && !ctx.Warehouse)
                {
                    throw new FieldForbiddenException("Тендерные закупки видят снабжение, директор и кладовщик.");
                }

                break;
            default:
                throw new FieldNotFoundException("Документ не найден.");
        }
    }

    private void Log(ProcurementContext ctx, string ownerType, Guid ownerId, string text) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog { DocType = ownerType, DocId = ownerId, ActorId = ctx.UserId, Action = "document", Text = text });
}

using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Procurement;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "procurement": заявки на закупку и их позиции, категории, единицы измерения, счётчики номеров, история и вложения.
/// Имена ключей и индексов — явные, как в "work" и "hr".
/// </summary>
internal static class ProcurementModel
{
    public const string Schema = "procurement";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<ProcurementRequest>(e =>
        {
            e.ToTable("Requests", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementRequests");
            e.Property(x => x.Number).HasMaxLength(32);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.EstimatedTotal).HasPrecision(18, 2);
            e.Property(x => x.PurchaseTotal).HasPrecision(18, 2);
            e.Property(x => x.ApprovedSnapshot).HasColumnType("jsonb");
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.Number).IsUnique().HasDatabaseName("IX_ProcurementRequests_Number");
            e.HasIndex(x => new { x.Status, x.DepartmentId }).HasDatabaseName("IX_ProcurementRequests_Status_DepartmentId");
            e.HasIndex(x => new { x.InitiatorId, x.Status }).HasDatabaseName("IX_ProcurementRequests_InitiatorId_Status");
            e.HasIndex(x => new { x.DepartmentId, x.CreatedAt }).HasDatabaseName("IX_ProcurementRequests_DepartmentId_CreatedAt");
            e.HasIndex(x => new { x.InitiatorId, x.ClientKey }).IsUnique().HasFilter("\"ClientKey\" IS NOT NULL")
                .HasDatabaseName("IX_ProcurementRequests_InitiatorId_ClientKey");
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementRequests_Departments_DepartmentId");
            e.HasOne(x => x.Initiator).WithMany().HasForeignKey(x => x.InitiatorId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementRequests_Users_InitiatorId");
            e.HasMany(x => x.Items).WithOne(x => x.Request).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementRequestItems_Requests_RequestId");
        });

        b.Entity<ProcurementRequestItem>(e =>
        {
            e.ToTable("RequestItems", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementRequestItems");
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.UnitCode).HasMaxLength(16);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.EstimatedPrice).HasPrecision(18, 2);
            e.Property(x => x.Comment).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.FromStock).HasPrecision(18, 3);
            e.Property(x => x.IssuedQty).HasPrecision(18, 3);
            e.Property(x => x.OrderedQty).HasPrecision(18, 3);
            e.Property(x => x.ReceivedQty).HasPrecision(18, 3);
            e.Property(x => x.PurchaseReason).HasMaxLength(500);
            e.Ignore(x => x.ToPurchase);
            e.HasIndex(x => x.ItemId).HasDatabaseName("IX_ProcurementRequestItems_ItemId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementRequestItems_Items_ItemId");
            e.HasIndex(x => new { x.RequestId, x.LineNo }).HasDatabaseName("IX_ProcurementRequestItems_RequestId_LineNo");
            e.HasIndex(x => x.CategoryId).HasDatabaseName("IX_ProcurementRequestItems_CategoryId");
            e.HasIndex(x => x.UnitCode).HasDatabaseName("IX_ProcurementRequestItems_UnitCode");
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementRequestItems_Categories_CategoryId");
            e.HasOne<ProcurementUnit>().WithMany().HasForeignKey(x => x.UnitCode).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementRequestItems_Units_UnitCode");
        });

        b.Entity<ProcurementCategory>(e =>
        {
            e.ToTable("Categories", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementCategories");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Code).HasMaxLength(32);
            e.HasIndex(x => x.Code).IsUnique().HasFilter("\"Code\" IS NOT NULL").HasDatabaseName("IX_ProcurementCategories_Code");
            e.HasIndex(x => x.Name).IsUnique().HasDatabaseName("IX_ProcurementCategories_Name");
        });

        b.Entity<ProcurementUnit>(e =>
        {
            e.ToTable("Units", Schema);
            e.HasKey(x => x.Code).HasName("PK_ProcurementUnits");
            e.Property(x => x.Code).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(50);
        });

        b.Entity<ProcurementCounter>(e =>
        {
            e.ToTable("Counters", Schema);
            e.HasKey(x => new { x.Prefix, x.Year }).HasName("PK_ProcurementCounters");
            e.Property(x => x.Prefix).HasMaxLength(8);
        });

        b.Entity<ProcurementAuditLog>(e =>
        {
            e.ToTable("AuditLog", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementAuditLog");
            e.Property(x => x.DocType).HasMaxLength(32);
            e.Property(x => x.Action).HasMaxLength(32);
            e.Property(x => x.FromStatus).HasMaxLength(24);
            e.Property(x => x.ToStatus).HasMaxLength(24);
            e.Property(x => x.Text).HasMaxLength(1000);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Changes).HasColumnType("jsonb");
            e.HasIndex(x => new { x.DocType, x.DocId, x.CreatedAt }).HasDatabaseName("IX_ProcurementAuditLog_DocType_DocId_CreatedAt");
            e.HasIndex(x => x.ActorId).HasDatabaseName("IX_ProcurementAuditLog_ActorId");
            // Аналитика: заявки, перешедшие в «Выполнена» за период.
            e.HasIndex(x => new { x.DocType, x.ToStatus, x.CreatedAt }).HasDatabaseName("IX_ProcurementAuditLog_DocType_ToStatus_CreatedAt");
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProcurementAuditLog_Users_ActorId");
        });

        b.Entity<ProcurementDocument>(e =>
        {
            e.ToTable("Documents", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementDocuments");
            e.Property(x => x.OwnerType).HasMaxLength(32);
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.ObjectKey).HasMaxLength(300);
            e.HasIndex(x => new { x.OwnerType, x.OwnerId }).HasDatabaseName("IX_ProcurementDocuments_OwnerType_OwnerId");
            e.HasIndex(x => x.UploadedById).HasDatabaseName("IX_ProcurementDocuments_UploadedById");
            e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProcurementDocuments_Users_UploadedById");
        });

        b.Entity<ProcurementItem>(e =>
        {
            e.ToTable("Items", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementItems");
            e.Property(x => x.Sku).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.UnitCode).HasMaxLength(16);
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.MinQty).HasPrecision(18, 3);
            e.HasIndex(x => x.Sku).IsUnique().HasDatabaseName("IX_ProcurementItems_Sku");
            e.HasIndex(x => x.Barcode).HasDatabaseName("IX_ProcurementItems_Barcode");
            e.HasIndex(x => x.CategoryId).HasDatabaseName("IX_ProcurementItems_CategoryId");
            e.HasIndex(x => x.UnitCode).HasDatabaseName("IX_ProcurementItems_UnitCode");
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementItems_Categories_CategoryId");
            e.HasOne<ProcurementUnit>().WithMany().HasForeignKey(x => x.UnitCode).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementItems_Units_UnitCode");
        });

        b.Entity<ProcurementWarehouse>(e =>
        {
            e.ToTable("Warehouses", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementWarehouses");
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_ProcurementWarehouses_Code");
        });

        b.Entity<ProcurementStockBalance>(e =>
        {
            e.ToTable("StockBalances", Schema, t =>
            {
                t.HasCheckConstraint("CK_ProcurementStockBalances_OnHand", "\"OnHand\" >= 0");
                t.HasCheckConstraint("CK_ProcurementStockBalances_Reserved", "\"Reserved\" >= 0 AND \"Reserved\" <= \"OnHand\"");
            });
            e.HasKey(x => new { x.WarehouseId, x.ItemId }).HasName("PK_ProcurementStockBalances");
            e.Property(x => x.OnHand).HasPrecision(18, 3);
            e.Property(x => x.Reserved).HasPrecision(18, 3);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.ItemId).HasDatabaseName("IX_ProcurementStockBalances_ItemId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementStockBalances_Items_ItemId");
            e.HasOne<ProcurementWarehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementStockBalances_Warehouses_WarehouseId");
        });

        b.Entity<ProcurementStockDocument>(e =>
        {
            e.ToTable("StockDocuments", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementStockDocuments");
            e.Property(x => x.Number).HasMaxLength(32);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Version).IsRowVersion();
            e.Property(x => x.Receiver).HasMaxLength(200);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasIndex(x => x.Number).IsUnique().HasDatabaseName("IX_ProcurementStockDocuments_Number");
            e.HasIndex(x => new { x.Kind, x.CreatedAt }).HasDatabaseName("IX_ProcurementStockDocuments_Kind_CreatedAt");
            e.HasIndex(x => x.RequestId).HasDatabaseName("IX_ProcurementStockDocuments_RequestId");
            e.HasIndex(x => new { x.OrderId, x.Status }).HasDatabaseName("IX_ProcurementStockDocuments_OrderId_Status");
            e.HasIndex(x => new { x.Status, x.PostedAt }).HasDatabaseName("IX_ProcurementStockDocuments_Status_PostedAt");
            e.HasIndex(x => x.DepartmentId).HasDatabaseName("IX_ProcurementStockDocuments_DepartmentId");
            e.HasIndex(x => x.ActorId).HasDatabaseName("IX_ProcurementStockDocuments_ActorId");
            e.HasIndex(x => new { x.ActorId, x.ClientKey }).IsUnique().HasFilter("\"ClientKey\" IS NOT NULL")
                .HasDatabaseName("IX_ProcurementStockDocuments_ActorId_ClientKey");
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementStockDocuments_Departments_DepartmentId");
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProcurementStockDocuments_Users_ActorId");
            e.HasOne<ProcurementWarehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementStockDocuments_Warehouses_WarehouseId");
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementStockDocuments_PurchaseOrders_OrderId");
            e.HasMany(x => x.ReceiptLines).WithOne(x => x.Document).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementReceiptLines_StockDocuments_DocumentId");
        });

        b.Entity<ProcurementDigest>(e =>
        {
            e.ToTable("Digests", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementDigests");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Summary).HasMaxLength(1000);
            e.Property(x => x.Sections).HasColumnType("jsonb");
            e.HasIndex(x => x.Day).IsUnique().HasDatabaseName("IX_ProcurementDigests_Day");
        });

        b.Entity<ProcurementEscalation>(e =>
        {
            e.ToTable("Escalations", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementEscalations");
            e.HasIndex(x => new { x.TaskId, x.Level, x.DueDate }).IsUnique().HasDatabaseName("IX_ProcurementEscalations_TaskId_Level_DueDate");
            e.HasOne<OneBase.Domain.Work.WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementEscalations_Tasks_TaskId");
        });

        b.Entity<ProcurementReceiptLine>(e =>
        {
            e.ToTable("ReceiptLines", Schema, t =>
            {
                t.HasCheckConstraint("CK_ProcurementReceiptLines_Quantities",
                    "\"Received\" >= 0 AND \"Defective\" >= 0 AND \"Defective\" <= \"Received\" AND \"Shortage\" >= 0 AND \"Accepted\" >= 0");
            });
            e.HasKey(x => x.Id).HasName("PK_ProcurementReceiptLines");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Received).HasPrecision(18, 3);
            e.Property(x => x.Defective).HasPrecision(18, 3);
            e.Property(x => x.Shortage).HasPrecision(18, 3);
            e.Property(x => x.Accepted).HasPrecision(18, 3);
            e.Property(x => x.Comment).HasMaxLength(500);
            e.Property(x => x.ClaimStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.ClaimResolution).HasMaxLength(1000);
            e.HasIndex(x => new { x.DocumentId, x.LineNo }).HasDatabaseName("IX_ProcurementReceiptLines_DocumentId_LineNo");
            e.HasIndex(x => x.OrderItemId).HasDatabaseName("IX_ProcurementReceiptLines_OrderItemId");
            e.HasIndex(x => x.ItemId).HasDatabaseName("IX_ProcurementReceiptLines_ItemId");
            e.HasIndex(x => x.ClaimStatus).HasDatabaseName("IX_ProcurementReceiptLines_ClaimStatus");
            e.HasOne(x => x.OrderItem).WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementReceiptLines_PurchaseOrderItems_OrderItemId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementReceiptLines_Items_ItemId");
            e.HasOne<OneBase.Domain.Identity.User>().WithMany().HasForeignKey(x => x.ClaimResolvedById).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProcurementReceiptLines_Users_ClaimResolvedById");
        });

        b.Entity<ProcurementStockMovement>(e =>
        {
            e.ToTable("StockMovements", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementStockMovements");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.BalanceBefore).HasPrecision(18, 3);
            e.Property(x => x.BalanceAfter).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.HasIndex(x => new { x.ItemId, x.CreatedAt }).HasDatabaseName("IX_ProcurementStockMovements_ItemId_CreatedAt");
            e.HasIndex(x => x.DocumentId).HasDatabaseName("IX_ProcurementStockMovements_DocumentId");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_ProcurementStockMovements_CreatedAt");
            e.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementStockMovements_StockDocuments_DocumentId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementStockMovements_Items_ItemId");
        });

        b.Entity<ProcurementStockImport>(e =>
        {
            e.ToTable("StockImports", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementStockImports");
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.WarehouseId, x.CreatedAt }).HasDatabaseName("IX_ProcurementStockImports_WarehouseId_CreatedAt");
        });

        b.Entity<ProcurementStockImportRow>(e =>
        {
            e.ToTable("StockImportRows", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementStockImportRows");
            e.Property(x => x.Key).HasMaxLength(64);
            e.HasIndex(x => new { x.WarehouseId, x.Key }).IsUnique().HasDatabaseName("IX_ProcurementStockImportRows_WarehouseId_Key");
            e.HasIndex(x => x.ImportId).HasDatabaseName("IX_ProcurementStockImportRows_ImportId");
            e.HasOne<ProcurementStockImport>().WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementStockImportRows_StockImports_ImportId");
        });

        b.Entity<ProcurementStockReservation>(e =>
        {
            e.ToTable("StockReservations", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementStockReservations");
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.Issued).HasPrecision(18, 3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.RequestId, x.Status }).HasDatabaseName("IX_ProcurementStockReservations_RequestId_Status");
            e.HasIndex(x => new { x.ItemId, x.Status }).HasDatabaseName("IX_ProcurementStockReservations_ItemId_Status");
            // Активный резерв на позицию заявки — один: повторная отправка не резервирует второй раз.
            e.HasIndex(x => x.RequestItemId).IsUnique().HasFilter("\"Status\" = 'Active'")
                .HasDatabaseName("IX_ProcurementStockReservations_RequestItemId_Active");
        });

        b.Entity<ProcurementApproval>(e =>
        {
            e.ToTable("Approvals", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementApprovals");
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.PurchaseTotal).HasPrecision(18, 2);
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
            e.Property(x => x.Stage).HasMaxLength(16);
            // Одно решение на шаг раунда: повторное нажатие согласующего не создаёт второе.
            e.HasIndex(x => new { x.RequestId, x.Round, x.Stage }).IsUnique().HasDatabaseName("IX_ProcurementApprovals_RequestId_Round_Stage");
            e.HasIndex(x => x.DeciderId).HasDatabaseName("IX_ProcurementApprovals_DeciderId");
            e.HasOne(x => x.Decider).WithMany().HasForeignKey(x => x.DeciderId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementApprovals_Users_DeciderId");
            e.HasOne<ProcurementRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementApprovals_Requests_RequestId");
        });

        b.Entity<ProcurementSettings>(e =>
        {
            e.ToTable("Settings", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementSettings");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ReapprovalTolerancePercent).HasPrecision(5, 2);
            e.Property(x => x.DirectorLimit).HasPrecision(18, 2);
            // Согласующие — пользователи OneBase; удалённый пользователь снимается с маршрута, а не ломает настройки.
            e.HasOne<OneBase.Domain.Identity.User>().WithMany().HasForeignKey(x => x.FinanceApproverId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<OneBase.Domain.Identity.User>().WithMany().HasForeignKey(x => x.DirectorApproverId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ProcurementSupplier>(e =>
        {
            e.ToTable("Suppliers", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementSuppliers");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.LegalName).HasMaxLength(300);
            e.Property(x => x.Inn).HasMaxLength(20);
            e.Property(x => x.ContactPerson).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.BankName).HasMaxLength(200);
            e.Property(x => x.BankMfo).HasMaxLength(20);
            e.Property(x => x.BankAccount).HasMaxLength(40);
            e.Property(x => x.PaymentTerms).HasMaxLength(500);
            e.Property(x => x.Products).HasMaxLength(2000);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.Inn).IsUnique().HasFilter("\"Inn\" IS NOT NULL").HasDatabaseName("IX_ProcurementSuppliers_Inn");
            e.HasIndex(x => x.Name).HasDatabaseName("IX_ProcurementSuppliers_Name");
            e.HasMany(x => x.Categories).WithOne(x => x.Supplier).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementSupplierCategories_Suppliers_SupplierId");
        });

        b.Entity<ProcurementSupplierCategory>(e =>
        {
            e.ToTable("SupplierCategories", Schema);
            e.HasKey(x => new { x.SupplierId, x.CategoryId }).HasName("PK_ProcurementSupplierCategories");
            e.HasIndex(x => x.CategoryId).HasDatabaseName("IX_ProcurementSupplierCategories_CategoryId");
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementSupplierCategories_Categories_CategoryId");
        });

        b.Entity<ProcurementOffer>(e =>
        {
            e.ToTable("Offers", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementOffers");
            e.Property(x => x.DeliveryCost).HasPrecision(18, 2);
            e.Property(x => x.VatRate).HasPrecision(5, 2);
            e.Property(x => x.PaymentTerms).HasMaxLength(500);
            e.Property(x => x.Warranty).HasMaxLength(200);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => x.RequestId).HasDatabaseName("IX_ProcurementOffers_RequestId");
            e.HasIndex(x => x.SupplierId).HasDatabaseName("IX_ProcurementOffers_SupplierId");
            e.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementOffers_Requests_RequestId");
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementOffers_Suppliers_SupplierId");
            e.HasMany(x => x.Items).WithOne(x => x.Offer).HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementOfferItems_Offers_OfferId");
        });

        b.Entity<ProcurementOfferItem>(e =>
        {
            e.ToTable("OfferItems", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementOfferItems");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => x.OfferId).HasDatabaseName("IX_ProcurementOfferItems_OfferId");
            e.HasIndex(x => x.RequestItemId).HasDatabaseName("IX_ProcurementOfferItems_RequestItemId");
            e.HasOne<ProcurementRequestItem>().WithMany().HasForeignKey(x => x.RequestItemId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementOfferItems_RequestItems_RequestItemId");
        });

        b.Entity<ProcurementPurchaseOrder>(e =>
        {
            e.ToTable("PurchaseOrders", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementPurchaseOrders");
            e.Property(x => x.Number).HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.PaymentTerms).HasMaxLength(500);
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.Property(x => x.VatRate).HasPrecision(5, 2);
            e.Property(x => x.DeliveryCost).HasPrecision(18, 2);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.VatAmount).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.ApprovedSnapshot).HasColumnType("jsonb");
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.Number).IsUnique().HasDatabaseName("IX_ProcurementPurchaseOrders_Number");
            e.HasIndex(x => new { x.Status, x.ExpectedDate }).HasDatabaseName("IX_ProcurementPurchaseOrders_Status_ExpectedDate");
            e.HasIndex(x => new { x.SupplierId, x.Status }).HasDatabaseName("IX_ProcurementPurchaseOrders_SupplierId_Status");
            e.HasIndex(x => new { x.ResponsibleId, x.Status }).HasDatabaseName("IX_ProcurementPurchaseOrders_ResponsibleId_Status");
            e.HasIndex(x => x.OfferId).HasDatabaseName("IX_ProcurementPurchaseOrders_OfferId");
            // Аналитика по периодам: закупки (утверждение), отправка поставщику, поставка.
            e.HasIndex(x => x.ApprovedAt).HasDatabaseName("IX_ProcurementPurchaseOrders_ApprovedAt");
            e.HasIndex(x => x.OrderedAt).HasDatabaseName("IX_ProcurementPurchaseOrders_OrderedAt");
            e.HasIndex(x => x.DeliveredAt).HasDatabaseName("IX_ProcurementPurchaseOrders_DeliveredAt");
            e.HasIndex(x => new { x.CreatedById, x.ClientKey }).IsUnique().HasFilter("\"ClientKey\" IS NOT NULL")
                .HasDatabaseName("IX_ProcurementPurchaseOrders_CreatedById_ClientKey");
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementPurchaseOrders_Suppliers_SupplierId");
            e.HasOne(x => x.Responsible).WithMany().HasForeignKey(x => x.ResponsibleId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementPurchaseOrders_Users_ResponsibleId");
            e.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementPurchaseOrderItems_PurchaseOrders_OrderId");
        });

        b.Entity<ProcurementPurchaseOrderItem>(e =>
        {
            e.ToTable("PurchaseOrderItems", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementPurchaseOrderItems");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.UnitCode).HasMaxLength(16);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.ReceivedQty).HasPrecision(18, 3);
            e.Property(x => x.ClosedQty).HasPrecision(18, 3);
            e.HasIndex(x => x.OrderId).HasDatabaseName("IX_ProcurementPurchaseOrderItems_OrderId");
            e.HasIndex(x => x.RequestItemId).HasDatabaseName("IX_ProcurementPurchaseOrderItems_RequestItemId");
            e.HasOne(x => x.RequestItem).WithMany().HasForeignKey(x => x.RequestItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementPurchaseOrderItems_RequestItems_RequestItemId");
        });

        // Журнал тендерных закупок: записи снабжения (сырьё для производства), на склад не приходуются.
        b.Entity<ProcurementTender>(e =>
        {
            e.ToTable("Tenders", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementTenders");
            e.Property(x => x.Number).HasMaxLength(32);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.TenderNo).HasMaxLength(100);
            e.Property(x => x.ContractNo).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.Number).IsUnique().HasDatabaseName("IX_ProcurementTenders_Number");
            e.HasIndex(x => x.TenderDate).HasDatabaseName("IX_ProcurementTenders_TenderDate");
            e.HasIndex(x => new { x.Status, x.DeliveryDate }).HasDatabaseName("IX_ProcurementTenders_Status_DeliveryDate");
            e.HasIndex(x => new { x.SupplierId, x.TenderDate }).HasDatabaseName("IX_ProcurementTenders_SupplierId_TenderDate");
            e.HasIndex(x => x.CategoryId).HasDatabaseName("IX_ProcurementTenders_CategoryId");
            e.HasIndex(x => x.ResponsibleId).HasDatabaseName("IX_ProcurementTenders_ResponsibleId");
            e.HasIndex(x => new { x.ResponsibleId, x.ClientKey }).IsUnique().HasFilter("\"ClientKey\" IS NOT NULL")
                .HasDatabaseName("IX_ProcurementTenders_ResponsibleId_ClientKey");
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementTenders_Suppliers_SupplierId");
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementTenders_Categories_CategoryId");
            e.HasOne(x => x.Responsible).WithMany().HasForeignKey(x => x.ResponsibleId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementTenders_Users_ResponsibleId");
            e.HasMany(x => x.Items).WithOne(x => x.Tender).HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementTenderItems_Tenders_TenderId");
        });

        b.Entity<ProcurementTenderItem>(e =>
        {
            e.ToTable("TenderItems", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementTenderItems");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.UnitCode).HasMaxLength(16);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasIndex(x => x.TenderId).HasDatabaseName("IX_ProcurementTenderItems_TenderId");
        });

        b.Entity<ProcurementOrderApproval>(e =>
        {
            e.ToTable("OrderApprovals", Schema);
            e.HasKey(x => x.Id).HasName("PK_ProcurementOrderApprovals");
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
            e.HasIndex(x => new { x.OrderId, x.Round }).IsUnique().HasDatabaseName("IX_ProcurementOrderApprovals_OrderId_Round");
            e.HasIndex(x => x.DeciderId).HasDatabaseName("IX_ProcurementOrderApprovals_DeciderId");
            e.HasOne(x => x.Decider).WithMany().HasForeignKey(x => x.DeciderId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProcurementOrderApprovals_Users_DeciderId");
            e.HasOne<ProcurementPurchaseOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProcurementOrderApprovals_PurchaseOrders_OrderId");
        });
    }
}

using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Production;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "production": рецептуры (версии, этапы, виды брака, строки) и задания на выпуск (строки потребности и расхода, снимок расчёта). Сырьё — позиции склада снабжения
/// (procurement.Items), выдача и возврат — складские документы снабжения со ссылкой на задание.
/// </summary>
internal static class ProductionModel
{
    public const string Schema = "production";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<ProductionRecipe>(e =>
        {
            e.ToTable("Recipes", Schema, t =>
            {
                t.HasCheckConstraint("CK_ProductionRecipes_OutputQuantity", "\"OutputQuantity\" > 0");
                t.HasCheckConstraint("CK_ProductionRecipes_VersionNo", "\"VersionNo\" > 0");
                t.HasCheckConstraint("CK_ProductionRecipes_Batch",
                    "(\"BatchMinKg\" IS NULL OR \"BatchMinKg\" > 0) AND (\"BatchMaxKg\" IS NULL OR \"BatchMaxKg\" > 0) AND (\"BatchMinKg\" IS NULL OR \"BatchMaxKg\" IS NULL OR \"BatchMinKg\" <= \"BatchMaxKg\")");
                t.HasCheckConstraint("CK_ProductionRecipes_Packaging",
                    "(\"PieceKg\" IS NULL OR \"PieceKg\" > 0) AND (\"PackKg\" IS NULL OR \"PackKg\" > 0) AND (\"PacksPerBox\" IS NULL OR \"PacksPerBox\" > 0)");
            });
            e.HasKey(x => x.Id).HasName("PK_ProductionRecipes");
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.OutputName).HasMaxLength(200);
            e.Property(x => x.OutputUnit).HasMaxLength(16);
            e.Property(x => x.OutputQuantity).HasPrecision(18, 3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.TechCardNo).HasMaxLength(32);
            e.Property(x => x.BatchMinKg).HasPrecision(18, 3);
            e.Property(x => x.BatchMaxKg).HasPrecision(18, 3);
            e.Property(x => x.PieceKg).HasPrecision(18, 6);
            e.Property(x => x.PackKg).HasPrecision(18, 6);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.Code, x.VersionNo }).IsUnique().HasDatabaseName("IX_ProductionRecipes_Code_VersionNo");
            // Одна действующая версия и одна неутверждённая (черновик или на утверждении) на код.
            e.HasIndex(x => x.Code).IsUnique().HasFilter("\"Status\" = 'Active'").HasDatabaseName("IX_ProductionRecipes_Code_Active");
            e.HasIndex(x => x.Code).IsUnique().HasFilter("\"Status\" IN ('Draft', 'PendingApproval')").HasDatabaseName("IX_ProductionRecipes_Code_Open");
            e.HasOne(x => x.ActivatedBy).WithMany().HasForeignKey(x => x.ActivatedById).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProductionRecipes_Users_ActivatedById");
            e.HasOne(x => x.Technologist).WithMany().HasForeignKey(x => x.TechnologistId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProductionRecipes_Users_TechnologistId");
            e.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProductionRecipes_Users_SubmittedById");
            e.HasMany(x => x.Stages).WithOne(x => x.Recipe).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductionRecipeStages_Recipes_RecipeId");
            e.HasMany(x => x.DefectTypes).WithOne(x => x.Recipe).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductionDefectTypes_Recipes_RecipeId");
            e.HasOne(x => x.OutputItem).WithMany().HasForeignKey(x => x.OutputItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionRecipes_Items_OutputItemId");
            e.HasOne<OneBase.Domain.Procurement.ProcurementUnit>().WithMany().HasForeignKey(x => x.OutputUnit)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ProductionRecipes_Units_OutputUnit");
            e.HasMany(x => x.Lines).WithOne(x => x.Recipe).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductionRecipeLines_Recipes_RecipeId");
        });

        b.Entity<ProductionRecipeLine>(e =>
        {
            e.ToTable("RecipeLines", Schema, t =>
            {
                t.HasCheckConstraint("CK_ProductionRecipeLines_Quantity", "\"Quantity\" > 0");
                t.HasCheckConstraint("CK_ProductionRecipeLines_LossPercent", "\"LossPercent\" >= 0 AND \"LossPercent\" <= 100");
            });
            e.HasKey(x => x.Id).HasName("PK_ProductionRecipeLines");
            e.Property(x => x.Quantity).HasPrecision(18, 6);
            e.Property(x => x.LossPercent).HasPrecision(5, 2);
            e.Property(x => x.Comment).HasMaxLength(500);
            e.Property(x => x.Basis).HasConversion<string>().HasMaxLength(16).HasDefaultValue(ProductionLineBasis.Output);
            e.Property(x => x.GroupName).HasMaxLength(64);
            e.Property(x => x.UnitCode).HasMaxLength(16);
            e.Property(x => x.UnitKg).HasPrecision(18, 6);
            // Позиция — один раз на этап (у рецептуры без этапов — один раз): сахар может быть и в тесте, и в глазури.
            e.HasIndex(x => new { x.RecipeId, x.StageId, x.ItemId }).IsUnique().AreNullsDistinct(false).HasDatabaseName("IX_ProductionRecipeLines_RecipeId_StageId_ItemId");
            e.HasIndex(x => x.ItemId).HasDatabaseName("IX_ProductionRecipeLines_ItemId");
            e.HasIndex(x => x.StageId).HasDatabaseName("IX_ProductionRecipeLines_StageId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionRecipeLines_Items_ItemId");
            e.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductionRecipeLines_Stages_StageId");
            e.HasOne<OneBase.Domain.Procurement.ProcurementUnit>().WithMany().HasForeignKey(x => x.UnitCode).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionRecipeLines_Units_UnitCode");
        });

        b.Entity<ProductionRecipeStage>(e =>
        {
            e.ToTable("RecipeStages", Schema, t =>
            {
                t.HasCheckConstraint("CK_ProductionRecipeStages_StageNo", "\"StageNo\" > 0");
                t.HasCheckConstraint("CK_ProductionRecipeStages_Yield",
                    "\"YieldPercent\" > 0 AND (\"YieldMinPercent\" IS NULL OR \"YieldMinPercent\" <= \"YieldPercent\") AND (\"YieldMaxPercent\" IS NULL OR \"YieldMaxPercent\" >= \"YieldPercent\")");
            });
            e.HasKey(x => x.Id).HasName("PK_ProductionRecipeStages");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DefectBase).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Area).HasMaxLength(100);
            e.Property(x => x.YieldPercent).HasPrecision(7, 3);
            e.Property(x => x.YieldMinPercent).HasPrecision(7, 3);
            e.Property(x => x.YieldMaxPercent).HasPrecision(7, 3);
            e.Property(x => x.MoistureNorm).HasPrecision(9, 3);
            e.Property(x => x.MoistureUnit).HasMaxLength(16);
            e.Property(x => x.MoistureTolerance).HasPrecision(9, 3);
            e.Property(x => x.Comment).HasMaxLength(500);
            e.HasIndex(x => new { x.RecipeId, x.StageNo }).IsUnique().HasDatabaseName("IX_ProductionRecipeStages_RecipeId_StageNo");
        });

        b.Entity<ProductionShift>(e =>
        {
            e.ToTable("Shifts", Schema, t =>
            {
                t.HasCheckConstraint("CK_ProductionShifts_ShiftNo", "\"ShiftNo\" BETWEEN 1 AND 9");
                t.HasCheckConstraint("CK_ProductionShifts_Output",
                    "(\"GoodOutput\" IS NULL OR \"GoodOutput\" >= 0) AND (\"Packs\" IS NULL OR \"Packs\" >= 0) AND (\"Boxes\" IS NULL OR \"Boxes\" >= 0)");
            });
            e.HasKey(x => x.Id).HasName("PK_ProductionShifts");
            e.Property(x => x.Number).HasMaxLength(32);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Line).HasMaxLength(100);
            e.Property(x => x.GoodOutput).HasPrecision(18, 3);
            e.Property(x => x.Packs).HasPrecision(18, 3);
            e.Property(x => x.Boxes).HasPrecision(18, 3);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.Number).IsUnique().HasDatabaseName("IX_ProductionShifts_Number");
            e.HasIndex(x => new { x.OrderId, x.ShiftDate }).HasDatabaseName("IX_ProductionShifts_OrderId_ShiftDate");
            e.HasIndex(x => new { x.Status, x.ShiftDate }).HasDatabaseName("IX_ProductionShifts_Status_ShiftDate");
            // Открытая корректировка у смены одна.
            e.HasIndex(x => x.CorrectsId).IsUnique().HasFilter("\"CorrectsId\" IS NOT NULL AND \"Status\" IN ('Draft', 'Closed')")
                .HasDatabaseName("IX_ProductionShifts_CorrectsId_Open");
            e.HasIndex(x => new { x.CreatedById, x.ClientKey }).IsUnique().HasFilter("\"ClientKey\" IS NOT NULL").HasDatabaseName("IX_ProductionShifts_CreatedById_ClientKey");
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ProductionShifts_Orders_OrderId");
            e.HasOne<ProductionShift>().WithMany().HasForeignKey(x => x.CorrectsId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ProductionShifts_Shifts_CorrectsId");
            e.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ProductionShifts_Users_OperatorId");
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_ProductionShifts_Users_SupervisorId");
            e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_ProductionShifts_Users_ApprovedById");
            e.HasMany(x => x.Batches).WithOne().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ProductionShiftBatches_Shifts_ShiftId");
            e.HasMany(x => x.Stages).WithOne().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ProductionShiftStages_Shifts_ShiftId");
            e.HasMany(x => x.Defects).WithOne().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ProductionShiftDefects_Shifts_ShiftId");
            e.HasMany(x => x.Materials).WithOne().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ProductionShiftMaterials_Shifts_ShiftId");
        });

        b.Entity<ProductionShiftBatch>(e =>
        {
            e.ToTable("ShiftBatches", Schema, t => t.HasCheckConstraint("CK_ProductionShiftBatches_MassKg", "\"MassKg\" > 0"));
            e.HasKey(x => x.Id).HasName("PK_ProductionShiftBatches");
            e.Property(x => x.MassKg).HasPrecision(18, 3);
            e.HasIndex(x => new { x.ShiftId, x.No }).IsUnique().HasDatabaseName("IX_ProductionShiftBatches_ShiftId_No");
        });

        b.Entity<ProductionShiftStage>(e =>
        {
            e.ToTable("ShiftStages", Schema, t => t.HasCheckConstraint("CK_ProductionShiftStages_Kg",
                "(\"InputKg\" IS NULL OR \"InputKg\" >= 0) AND (\"OutputKg\" IS NULL OR \"OutputKg\" >= 0) AND (\"ReworkKg\" IS NULL OR \"ReworkKg\" >= 0)"));
            e.HasKey(x => x.Id).HasName("PK_ProductionShiftStages");
            e.Property(x => x.InputKg).HasPrecision(18, 3);
            e.Property(x => x.OutputKg).HasPrecision(18, 3);
            e.Property(x => x.Moisture).HasPrecision(9, 3);
            e.Property(x => x.ReworkKg).HasPrecision(18, 3);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasIndex(x => new { x.ShiftId, x.StageNo }).IsUnique().HasDatabaseName("IX_ProductionShiftStages_ShiftId_StageNo");
        });

        b.Entity<ProductionShiftDefect>(e =>
        {
            e.ToTable("ShiftDefects", Schema, t => t.HasCheckConstraint("CK_ProductionShiftDefects_QuantityKg", "\"QuantityKg\" > 0"));
            e.HasKey(x => x.Id).HasName("PK_ProductionShiftDefects");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.QuantityKg).HasPrecision(18, 3);
            e.Property(x => x.Rework).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.ShiftId).HasDatabaseName("IX_ProductionShiftDefects_ShiftId");
            e.HasOne<ProductionDefectType>().WithMany().HasForeignKey(x => x.DefectTypeId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProductionShiftDefects_DefectTypes_DefectTypeId");
        });

        b.Entity<ProductionShiftMaterial>(e =>
        {
            e.ToTable("ShiftMaterials", Schema, t => t.HasCheckConstraint("CK_ProductionShiftMaterials_Quantity", "\"Quantity\" > 0"));
            e.HasKey(x => x.Id).HasName("PK_ProductionShiftMaterials");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.HasIndex(x => x.ShiftId).HasDatabaseName("IX_ProductionShiftMaterials_ShiftId");
            e.HasIndex(x => x.ItemId).HasDatabaseName("IX_ProductionShiftMaterials_ItemId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ProductionShiftMaterials_Items_ItemId");
        });

        b.Entity<ProductionDefectType>(e =>
        {
            e.ToTable("DefectTypes", Schema, t => t.HasCheckConstraint("CK_ProductionDefectTypes_NormPercent", "\"NormPercent\" >= 0 AND \"NormPercent\" < 100"));
            e.HasKey(x => x.Id).HasName("PK_ProductionDefectTypes");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.NormPercent).HasPrecision(5, 2);
            e.Property(x => x.Comment).HasMaxLength(500);
            e.HasIndex(x => new { x.StageId, x.Name }).IsUnique().HasDatabaseName("IX_ProductionDefectTypes_StageId_Name");
            e.HasIndex(x => x.RecipeId).HasDatabaseName("IX_ProductionDefectTypes_RecipeId");
            e.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductionDefectTypes_Stages_StageId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionDefectTypes_Items_ItemId");
        });

        b.Entity<ProductionOrder>(e =>
        {
            e.ToTable("Orders", Schema, t =>
            {
                t.HasCheckConstraint("CK_ProductionOrders_PlannedQty", "\"PlannedQty\" > 0");
                t.HasCheckConstraint("CK_ProductionOrders_OutputQty", "\"OutputQty\" IS NULL OR \"OutputQty\" >= 0");
            });
            e.HasKey(x => x.Id).HasName("PK_ProductionOrders");
            e.Property(x => x.Number).HasMaxLength(32);
            e.Property(x => x.PlannedQty).HasPrecision(18, 3);
            e.Property(x => x.OutputQty).HasPrecision(18, 3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.PlanSnapshot).HasColumnType("jsonb");
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.Number).IsUnique().HasDatabaseName("IX_ProductionOrders_Number");
            e.HasIndex(x => new { x.Status, x.PlannedDate }).HasDatabaseName("IX_ProductionOrders_Status_PlannedDate");
            e.HasIndex(x => x.RecipeId).HasDatabaseName("IX_ProductionOrders_RecipeId");
            e.HasIndex(x => new { x.CreatedById, x.ClientKey }).IsUnique().HasFilter("\"ClientKey\" IS NOT NULL")
                .HasDatabaseName("IX_ProductionOrders_CreatedById_ClientKey");
            e.HasOne(x => x.Recipe).WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionOrders_Recipes_RecipeId");
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionOrders_Departments_DepartmentId");
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionOrders_Warehouses_WarehouseId");
            e.HasOne(x => x.Responsible).WithMany().HasForeignKey(x => x.ResponsibleId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProductionOrders_Users_ResponsibleId");
            e.HasOne<OneBase.Domain.Identity.User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionOrders_Users_CreatedById");
            e.HasMany(x => x.Lines).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductionOrderLines_Orders_OrderId");
        });

        b.Entity<ProductionOrderLine>(e =>
        {
            e.ToTable("OrderLines", Schema, t =>
            {
                t.HasCheckConstraint("CK_ProductionOrderLines_Issued", "\"IssuedQty\" >= 0 AND \"ReturnedQty\" >= 0 AND \"ReturnedQty\" <= \"IssuedQty\"");
            });
            e.HasKey(x => x.Id).HasName("PK_ProductionOrderLines");
            e.Property(x => x.NormQty).HasPrecision(18, 6);
            e.Property(x => x.LossPercent).HasPrecision(5, 2);
            e.Property(x => x.RequiredQty).HasPrecision(18, 3);
            e.Property(x => x.IssuedQty).HasPrecision(18, 3);
            e.Property(x => x.ReturnedQty).HasPrecision(18, 3);
            e.Property(x => x.ActualNormQty).HasPrecision(18, 3);
            e.HasIndex(x => new { x.OrderId, x.ItemId }).IsUnique().HasDatabaseName("IX_ProductionOrderLines_OrderId_ItemId");
            e.HasIndex(x => x.ItemId).HasDatabaseName("IX_ProductionOrderLines_ItemId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionOrderLines_Items_ItemId");
        });
    }
}

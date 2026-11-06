using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Production;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "production": рецептуры (версии и строки) и задания на выпуск (строки потребности и расхода). Сырьё — позиции склада снабжения
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
            });
            e.HasKey(x => x.Id).HasName("PK_ProductionRecipes");
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.OutputName).HasMaxLength(200);
            e.Property(x => x.OutputUnit).HasMaxLength(16);
            e.Property(x => x.OutputQuantity).HasPrecision(18, 3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.Code, x.VersionNo }).IsUnique().HasDatabaseName("IX_ProductionRecipes_Code_VersionNo");
            // Одна действующая версия и один черновик на код.
            e.HasIndex(x => x.Code).IsUnique().HasFilter("\"Status\" = 'Active'").HasDatabaseName("IX_ProductionRecipes_Code_Active");
            e.HasIndex(x => x.Code).IsUnique().HasFilter("\"Status\" = 'Draft'").HasDatabaseName("IX_ProductionRecipes_Code_Draft");
            e.HasOne(x => x.ActivatedBy).WithMany().HasForeignKey(x => x.ActivatedById).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProductionRecipes_Users_ActivatedById");
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
            e.HasIndex(x => new { x.RecipeId, x.ItemId }).IsUnique().HasDatabaseName("IX_ProductionRecipeLines_RecipeId_ItemId");
            e.HasIndex(x => x.ItemId).HasDatabaseName("IX_ProductionRecipeLines_ItemId");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductionRecipeLines_Items_ItemId");
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

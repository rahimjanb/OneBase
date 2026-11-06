using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Twin;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "twin": здания цифрового двойника и их привязки к отделам и складам по постоянным идентификаторам. Данные разделов здесь не хранятся —
/// двойник читает их из сервисов разделов. Имена ключей и индексов — явные, как в "work", "hr" и "procurement".
/// </summary>
internal static class TwinModel
{
    private const string Schema = "twin";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<TwinObject>(e =>
        {
            e.ToTable("Objects", Schema);
            e.HasKey(x => x.Id).HasName("PK_TwinObjects");
            e.Property(x => x.Key).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(120);
            e.HasIndex(x => x.Key).IsUnique().HasDatabaseName("IX_TwinObjects_Key");
            e.HasMany(x => x.Links).WithOne(x => x.Object).HasForeignKey(x => x.ObjectId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_TwinObjectLinks_TwinObjects_ObjectId");
        });

        b.Entity<TwinObjectLink>(e =>
        {
            e.ToTable("ObjectLinks", Schema, t => t.HasCheckConstraint("CK_TwinObjectLinks_OneTarget",
                "(CASE WHEN \"DepartmentId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"WarehouseId\" IS NULL THEN 0 ELSE 1 END)"
                + " + (CASE WHEN \"LinkoStockId\" IS NULL THEN 0 ELSE 1 END) = 1"));
            e.HasKey(x => x.Id).HasName("PK_TwinObjectLinks");
            e.HasIndex(x => x.ObjectId).HasDatabaseName("IX_TwinObjectLinks_ObjectId");
            e.HasIndex(x => x.DepartmentId).HasDatabaseName("IX_TwinObjectLinks_DepartmentId");
            e.HasIndex(x => x.WarehouseId).HasDatabaseName("IX_TwinObjectLinks_WarehouseId");
            // Отдел или склад удалить нельзя, пока он привязан к зданию: сначала снять привязку в двойнике.
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_TwinObjectLinks_Departments_DepartmentId");
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_TwinObjectLinks_ProcurementWarehouses_WarehouseId");
        });
    }
}

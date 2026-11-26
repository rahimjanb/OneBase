using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Sales;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "tajik": продажи и остаток дилера в Таджикистане из его 1С (только чтение, 1С → OneBase) и регионы отчёта «Экспорт → Таджикистан».
/// Суммы — numeric(18,2) в сомони, цена — numeric(18,4), количество — numeric(18,3): как отдаёт 1С.
/// </summary>
internal static class TajikModel
{
    private const string Schema = "tajik";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<TajikSaleLine>(e =>
        {
            e.ToTable("SaleLines", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.DocNumber).HasMaxLength(64);
            e.Property(x => x.Customer).HasMaxLength(500);
            e.Property(x => x.Manager).HasMaxLength(200);
            e.Property(x => x.ProductCode).HasMaxLength(64);
            e.Property(x => x.ProductName).HasMaxLength(500);
            e.Property(x => x.Qty).HasPrecision(18, 3);
            e.Property(x => x.Price).HasPrecision(18, 4);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.Property(x => x.DiscountPercent).HasPrecision(9, 4);
            e.HasIndex(x => x.DocDate).HasDatabaseName("IX_TajikSaleLines_DocDate");
            e.HasIndex(x => new { x.DocNumber, x.DocDate }).HasDatabaseName("IX_TajikSaleLines_Doc");
            e.HasIndex(x => x.Customer).HasDatabaseName("IX_TajikSaleLines_Customer");
        });

        b.Entity<TajikStockItem>(e =>
        {
            e.ToTable("StockItems", Schema);
            e.HasKey(x => new { x.Date, x.ProductCode, x.Warehouse });
            e.Property(x => x.ProductCode).HasMaxLength(64);
            e.Property(x => x.Warehouse).HasMaxLength(200);
            e.Property(x => x.ProductName).HasMaxLength(500);
            e.Property(x => x.Qty).HasPrecision(18, 3);
        });

        b.Entity<TajikRegion>(e =>
        {
            e.ToTable("Regions", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique().HasDatabaseName("IX_TajikRegions_Name");
        });

        b.Entity<TajikRegionAssignment>(e =>
        {
            e.ToTable("RegionAssignments", Schema);
            e.HasKey(x => new { x.Kind, x.Name });
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(500);
            e.HasOne<TajikRegion>().WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TajikSyncRun>(e =>
        {
            e.ToTable("SyncRuns", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Trigger).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasIndex(x => x.StartedAt).HasDatabaseName("IX_TajikSyncRuns_StartedAt");
        });
    }
}

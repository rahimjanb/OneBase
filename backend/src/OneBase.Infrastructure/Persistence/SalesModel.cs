using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Sales;

namespace OneBase.Infrastructure.Persistence;

/// <summary>Конфигурация таблиц продаж: зеркало Linko (схема linko) и данные OneBase (схема sales).</summary>
internal static class SalesModel
{
    public static void ConfigureLinko(ModelBuilder b)
    {
        b.Entity<LinkoUser>(e =>
        {
            e.ToTable("Users", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Ignore(x => x.DisplayName);
        });

        b.Entity<LinkoMarket>(e =>
        {
            e.ToTable("Markets", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ResponsibleAgentId);
            e.HasIndex(x => x.BranchId);
        });

        b.Entity<LinkoProduct>(e =>
        {
            e.ToTable("Products", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoProductType>(e =>
        {
            e.ToTable("ProductTypes", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoBorder>(e =>
        {
            e.ToTable("Borders", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoMarketUser>(e =>
        {
            e.ToTable("MarketUsers", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.MarketId);
        });

        b.Entity<LinkoOrder>(e =>
        {
            e.ToTable("Orders", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
            e.Property(x => x.AcceptedAt).HasColumnType("timestamp without time zone"); // местное время Linko, без пояса
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId);
            e.HasIndex(x => x.CreatedDate);
            e.HasIndex(x => x.DeliveryDate);
            e.HasIndex(x => x.AcceptedDate);
            e.HasIndex(x => x.AgentId);
            e.HasIndex(x => x.MarketId);
        });

        b.Entity<LinkoOrderLine>(e =>
        {
            e.ToTable("OrderLines", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ProductId);
        });

        b.Entity<LinkoOrderReturn>(e =>
        {
            e.ToTable("OrderReturns", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ReturnId);
            e.HasIndex(x => x.CreatedDate);
        });

        b.Entity<LinkoOrderReturnLine>(e =>
        {
            e.ToTable("OrderReturnLines", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoVisit>(e =>
        {
            e.ToTable("Visits", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Date).HasColumnType("timestamp without time zone");
            e.HasIndex(x => x.Day);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<LinkoKpiPlan>(e =>
        {
            e.ToTable("KpiPlans", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoSyncState>(e =>
        {
            e.ToTable("SyncState", "linko");
            e.HasKey(x => x.Entity);
            e.Property(x => x.Entity).HasMaxLength(64);
        });

        b.Entity<LinkoStock>(e =>
        {
            e.ToTable("Stocks", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoProductBalance>(e =>
        {
            e.ToTable("ProductBalances", "linko");
            e.HasKey(x => new { x.ProductId, x.StockId });
            e.HasIndex(x => x.StockId);
        });

        b.Entity<LinkoStockTransfer>(e =>
        {
            e.ToTable("StockTransfers", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
            e.Property(x => x.GivenAt).HasColumnType("timestamp without time zone");
            e.Property(x => x.AcceptedAt).HasColumnType("timestamp without time zone");
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.TransferId);
            e.HasIndex(x => x.CreatedDate);
            e.HasIndex(x => x.AcceptedDate);
            e.HasIndex(x => x.FromStockId);
        });

        b.Entity<LinkoStockTransferLine>(e =>
        {
            e.ToTable("StockTransferLines", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoPayment>(e =>
        {
            e.ToTable("Payments", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AcceptedAt).HasColumnType("timestamp without time zone");
            e.HasIndex(x => x.CreatedDate);
            e.HasIndex(x => x.MarketId);
        });

        b.Entity<LinkoPriceList>(e =>
        {
            e.ToTable("PriceLists", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoPriceListItem>(e =>
        {
            e.ToTable("PriceListItems", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.PriceListId, x.ProductId });
        });

        b.Entity<LinkoProvider>(e =>
        {
            e.ToTable("Providers", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoCurrency>(e =>
        {
            e.ToTable("Currencies", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<LinkoContract>(e =>
        {
            e.ToTable("Contracts", "linko");
            e.Property(x => x.Id).ValueGeneratedNever();
        });
    }

    public static void ConfigureSales(ModelBuilder b)
    {
        b.Entity<SalesDirection>(e =>
        {
            e.ToTable("Directions", "sales");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ManagerName).HasMaxLength(200);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        });

        b.Entity<SalesRegion>(e =>
        {
            e.ToTable("Regions", "sales");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.SupervisorName).HasMaxLength(200);
            e.Property(x => x.DealerName).HasMaxLength(200);
            e.HasIndex(x => x.LinkoBranchId).IsUnique();
            e.HasOne(x => x.Direction).WithMany(x => x.Regions).HasForeignKey(x => x.DirectionId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SalesAgentProfile>(e =>
        {
            e.ToTable("AgentProfiles", "sales");
            e.HasKey(x => x.LinkoUserId);
            e.Property(x => x.LinkoUserId).ValueGeneratedNever();
            e.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SalesRegionPlan>(e =>
        {
            e.ToTable("RegionPlans", "sales");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RegionId, x.Kind, x.Year, x.Month, x.CategoryId }).IsUnique().AreNullsDistinct(false);
        });

        b.Entity<SalesAgentPlan>(e =>
        {
            e.ToTable("AgentPlans", "sales");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.LinkoUserId, x.Kind, x.Year, x.Month, x.CategoryId }).IsUnique().AreNullsDistinct(false);
        });

        b.Entity<SalesStaffPlan>(e =>
        {
            e.ToTable("StaffPlans", "sales");
            e.Property(x => x.IndicatorName).HasMaxLength(300);
            e.Property(x => x.PlanType).HasMaxLength(64);
            e.HasIndex(x => new { x.Year, x.Month, x.LinkoUserId, x.IndicatorId }).IsUnique();
        });

        b.Entity<SalesTarget>(e =>
        {
            e.ToTable("Targets", "sales");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(64);
        });
    }
}

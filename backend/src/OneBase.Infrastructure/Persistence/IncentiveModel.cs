using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Incentive;

namespace OneBase.Infrastructure.Persistence;

/// <summary>Схема "incentive": накопительные акции для ТП (Twizos) и цели магазинов маршрута дня в кг.</summary>
internal static class IncentiveModel
{
    private const string Schema = "incentive";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<IncentiveProgram>(e =>
        {
            e.ToTable("Programs", Schema);
            e.HasKey(x => x.Id).HasName("PK_IncentivePrograms");
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_IncentivePrograms_Code");
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Product).HasMaxLength(100);
            e.Property(x => x.TiersJson).HasColumnType("jsonb");
            e.Property(x => x.MonthsJson).HasColumnType("jsonb");
            e.Property(x => x.StoreSetsJson).HasColumnType("jsonb");
            e.Property(x => x.RmMode).HasMaxLength(16);
            e.Property(x => x.StaleData).HasMaxLength(16);
        });

        b.Entity<IncentiveRouteTarget>(e =>
        {
            e.ToTable("RouteTargets", Schema);
            e.HasKey(x => x.Id).HasName("PK_IncentiveRouteTargets");
            e.HasIndex(x => new { x.ProgramId, x.AgentId, x.Date, x.MarketId }).IsUnique().HasDatabaseName("IX_IncentiveRouteTargets_Program_Agent_Date_Market");
            e.HasIndex(x => new { x.ProgramId, x.Date }).HasDatabaseName("IX_IncentiveRouteTargets_Program_Date");
            e.HasOne<IncentiveProgram>().WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.MarketName).HasMaxLength(300);
            e.Property(x => x.RouteSource).HasMaxLength(16);
            e.Property(x => x.Sub).HasMaxLength(16);
            e.Property(x => x.Set).HasMaxLength(100);
            e.Property(x => x.Prize).HasMaxLength(100);
            e.Property(x => x.BattleLevel).HasMaxLength(8);
            e.Property(x => x.TargetKg).HasPrecision(18, 3);
            e.Property(x => x.AvgMonth).HasPrecision(18, 2);
        });
    }
}

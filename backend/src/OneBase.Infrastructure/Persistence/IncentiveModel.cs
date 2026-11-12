using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Incentive;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "incentive": накопительные акции для ТП (Twizos), цели магазинов маршрута дня, список спящих Yamelly (Приложение 1), итоги дня по
/// акциям и отметки выплат.
/// </summary>
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
            e.Property(x => x.PromosJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
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
            e.Property(x => x.Sets).HasMaxLength(32);
            e.Property(x => x.Placement).HasMaxLength(16).HasDefaultValue(IncentivePlacement.Route);
            e.Property(x => x.AvgCheck).HasPrecision(18, 2);
        });

        b.Entity<IncentiveSleepingMarket>(e =>
        {
            e.ToTable("SleepingMarkets", Schema);
            e.HasKey(x => x.Id).HasName("PK_IncentiveSleepingMarkets");
            e.HasIndex(x => new { x.ProgramId, x.MarketId }).IsUnique().HasDatabaseName("IX_IncentiveSleepingMarkets_Program_Market");
            e.HasOne<IncentiveProgram>().WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.FilePriority).HasMaxLength(100);
        });

        b.Entity<IncentiveDayResult>(e =>
        {
            e.ToTable("DayResults", Schema);
            e.HasKey(x => x.Id).HasName("PK_IncentiveDayResults");
            e.HasIndex(x => new { x.ProgramId, x.AgentId, x.Date }).IsUnique().HasDatabaseName("IX_IncentiveDayResults_Program_Agent_Date");
            e.HasIndex(x => new { x.ProgramId, x.Date }).HasDatabaseName("IX_IncentiveDayResults_Program_Date");
            e.HasOne<IncentiveProgram>().WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.TwizosKg).HasPrecision(18, 3);
            e.Property(x => x.OutOfRouteKg).HasPrecision(18, 3);
            e.Property(x => x.Cashback).HasPrecision(18, 2);
            e.Property(x => x.BestPrize).HasPrecision(18, 2);
        });

        b.Entity<IncentivePayout>(e =>
        {
            e.ToTable("Payouts", Schema);
            e.HasKey(x => x.Id).HasName("PK_IncentivePayouts");
            e.HasIndex(x => new { x.ProgramId, x.AgentId, x.WeekStart, x.Kind }).IsUnique().HasDatabaseName("IX_IncentivePayouts_Program_Agent_Week_Kind");
            e.HasOne<IncentiveProgram>().WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Note).HasMaxLength(500);
        });
    }
}

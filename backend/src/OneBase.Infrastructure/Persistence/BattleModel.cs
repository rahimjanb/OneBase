using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Battle;

namespace OneBase.Infrastructure.Persistence;

/// <summary>Схема "battle": цели ТП на магазины маршрута по дням («Поле боя»). Одна цель вида на магазин в день.</summary>
internal static class BattleModel
{
    private const string Schema = "battle";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<BattleGoal>(e =>
        {
            e.ToTable("Goals", Schema);
            e.HasKey(x => x.Id).HasName("PK_BattleGoals");
            e.HasIndex(x => new { x.AgentId, x.Week, x.Date }).HasDatabaseName("IX_BattleGoals_Agent_Week_Date");
            e.HasIndex(x => new { x.AgentId, x.Date, x.MarketId, x.Kind }).IsUnique().HasDatabaseName("IX_BattleGoals_Agent_Date_Market_Kind");
            e.Property(x => x.MarketName).HasMaxLength(300);
            e.Property(x => x.RouteSource).HasMaxLength(16);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Sub).HasMaxLength(16);
            e.Property(x => x.Level).HasMaxLength(8);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.ProductCode).HasMaxLength(50);
            e.Property(x => x.TargetSum).HasPrecision(18, 2);
            e.Property(x => x.SecondSum).HasPrecision(18, 2);
            e.Property(x => x.TakenSum).HasPrecision(18, 2);
            e.Property(x => x.AvgMonth).HasPrecision(18, 2);
        });
    }
}

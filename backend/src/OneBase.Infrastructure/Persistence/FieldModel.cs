using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Field;
using OneBase.Domain.Identity;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Sales Base — схема field. Отдельная от linko/sales: перезагрузка Linko и restore-linko.sh очищают те схемы, а маршруты,
/// визиты и задачи должны сохраняться. Точки, сотрудники и филиалы Linko — по их long-идентификаторам, без внешних ключей.
/// </summary>
public static class FieldModel
{
    private const string Schema = "field";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<FieldMember>(e =>
        {
            e.ToTable("Members", Schema);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.LinkoUserId).IsUnique();
            e.HasIndex(x => new { x.Role, x.IsActive });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Team).WithMany(x => x.Agents).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<FieldTeam>(e =>
        {
            e.ToTable("Teams", Schema);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.SupervisorId);
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<FieldCustomer>(e =>
        {
            e.ToTable("Customers", Schema);
            e.HasKey(x => x.MarketId);
            e.Property(x => x.MarketId).ValueGeneratedNever();
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.MonthlyTarget).HasPrecision(18, 2);
            e.Property(x => x.ContactPerson).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.Note).HasMaxLength(2000);
            e.HasIndex(x => x.AssignedAgentId);
            e.HasOne<FieldMember>().WithMany().HasForeignKey(x => x.AssignedAgentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<FieldCustomerStats>(e =>
        {
            e.ToTable("CustomerStats", Schema);
            e.HasKey(x => x.MarketId);
            e.Property(x => x.MarketId).ValueGeneratedNever();
            foreach (var p in new[] { nameof(FieldCustomerStats.Sales7), nameof(FieldCustomerStats.Sales30), nameof(FieldCustomerStats.Sales90), nameof(FieldCustomerStats.SalesPrev30), nameof(FieldCustomerStats.Kg30) })
            {
                e.Property(p).HasPrecision(18, 2);
            }

            e.HasIndex(x => x.Sales30);
            e.HasIndex(x => x.LastVisitDate);
        });

        b.Entity<FieldRoute>(e =>
        {
            e.ToTable("Routes", Schema);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DistanceKm).HasPrecision(10, 2);
            e.HasIndex(x => new { x.AgentId, x.Date }).IsUnique();
            e.HasIndex(x => x.Date);
            e.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FieldRoutePoint>(e =>
        {
            e.ToTable("RoutePoints", Schema);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasIndex(x => new { x.RouteId, x.Sequence });
            e.HasIndex(x => x.MarketId);
            e.HasOne(x => x.Route).WithMany(x => x.Points).HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FieldVisit>(e =>
        {
            e.ToTable("Visits", Schema);

            // Имена ключа и индексов — явно: таблица linko."Visits" уже занимает PK_Visits, иначе EF переименует чужой ключ.
            e.HasKey(x => x.Id).HasName("PK_FieldVisits");
            e.Property(x => x.GeoStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Result).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.PhotoKey).HasMaxLength(300);
            e.HasIndex(x => new { x.AgentId, x.StartedAt }).HasDatabaseName("IX_FieldVisits_AgentId_StartedAt");
            e.HasIndex(x => new { x.MarketId, x.StartedAt }).HasDatabaseName("IX_FieldVisits_MarketId_StartedAt");
            // Один идущий визит на участника — гарантия базы: двойное нажатие «Начать визит» не создаст второй.
            e.HasIndex(x => x.AgentId).IsUnique().HasFilter("\"Status\" = 'InProgress'").HasDatabaseName("IX_FieldVisits_AgentId_InProgress");
            e.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_FieldVisits_Members_AgentId");
        });

        b.Entity<FieldJointVisit>(e =>
        {
            e.ToTable("JointVisits", Schema);
            e.Property(x => x.Objective).HasMaxLength(500);
            e.Property(x => x.Result).HasMaxLength(2000);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.Date);
        });

        b.Entity<FieldJointVisitParticipant>(e =>
        {
            e.ToTable("JointVisitParticipants", Schema);
            e.HasKey(x => new { x.JointVisitId, x.MemberId });
            e.HasOne(x => x.JointVisit).WithMany(x => x.Participants).HasForeignKey(x => x.JointVisitId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.MemberId);
        });

        b.Entity<FieldTask>(e =>
        {
            e.ToTable("Tasks", Schema);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Result).HasMaxLength(2000);
            e.Property(x => x.CreatedByType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.AssignedToId, x.Status });
            e.HasIndex(x => new { x.SupervisorId, x.Status });
            e.HasIndex(x => x.DueDate);
            e.HasIndex(x => x.MarketId);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.Cascade);
            // Автор из OneBase без карточки участника; пользователь удалён — подпись пропадает, задача остаётся.
            e.HasIndex(x => x.CreatedByUserId);
            e.HasOne<OneBase.Domain.Identity.User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_FieldTasks_Users_CreatedByUserId");
        });

        b.Entity<FieldRecommendation>(e =>
        {
            e.ToTable("Recommendations", Schema);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Key).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.Property(x => x.SourceData).HasColumnType("jsonb");
            e.Property(x => x.Confidence).HasPrecision(4, 3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.Status, x.SupervisorId });
            e.HasIndex(x => new { x.Key, x.Status });
            e.HasIndex(x => x.AgentId);
        });

        b.Entity<FieldNotification>(e =>
        {
            e.ToTable("Notifications", Schema);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Body).HasMaxLength(2000);
            e.Property(x => x.Link).HasMaxLength(300);
            e.HasIndex(x => new { x.RecipientId, x.ReadAt });
            e.HasIndex(x => new { x.RecipientId, x.CreatedAt });
            e.HasOne<FieldMember>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FieldSettings>(e =>
        {
            e.ToTable("Settings", Schema);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.DeclineMinSales).HasPrecision(18, 2);
        });
    }
}

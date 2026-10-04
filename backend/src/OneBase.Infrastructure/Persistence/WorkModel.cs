using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Work;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "work": задачи отделов, уведомления пользователей OneBase, подписки Web Push и ключи VAPID.
/// Имена ключей и индексов — явные: в других схемах уже есть таблицы Tasks и Notifications.
/// </summary>
internal static class WorkModel
{
    private const string Schema = "work";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<WorkTask>(e =>
        {
            e.ToTable("Tasks", Schema);
            e.HasKey(x => x.Id).HasName("PK_WorkTasks");
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Result).HasMaxLength(2000);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.DepartmentId, x.Status }).HasDatabaseName("IX_WorkTasks_DepartmentId_Status");
            e.HasIndex(x => new { x.AssigneeId, x.Status }).HasDatabaseName("IX_WorkTasks_AssigneeId_Status");
            e.HasIndex(x => x.CreatedById).HasDatabaseName("IX_WorkTasks_CreatedById");
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkTasks_Departments_DepartmentId");
            e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTasks_Users_AssigneeId");
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTasks_Users_CreatedById");
        });

        b.Entity<UserNotification>(e =>
        {
            e.ToTable("Notifications", Schema);
            e.HasKey(x => x.Id).HasName("PK_UserNotifications");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Body).HasMaxLength(2000);
            e.Property(x => x.Link).HasMaxLength(500);
            e.HasIndex(x => new { x.RecipientId, x.ReadAt }).HasDatabaseName("IX_UserNotifications_RecipientId_ReadAt");
            e.HasIndex(x => new { x.RecipientId, x.CreatedAt }).HasDatabaseName("IX_UserNotifications_RecipientId_CreatedAt");
            e.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_UserNotifications_Users_RecipientId");
        });

        b.Entity<PushSubscription>(e =>
        {
            e.ToTable("PushSubscriptions", Schema);
            e.HasKey(x => x.Id).HasName("PK_PushSubscriptions");
            e.Property(x => x.Endpoint).HasMaxLength(1000);
            e.Property(x => x.P256dh).HasMaxLength(200);
            e.Property(x => x.Auth).HasMaxLength(100);
            e.Property(x => x.Origin).HasMaxLength(200);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.HasIndex(x => x.Endpoint).IsUnique().HasDatabaseName("IX_PushSubscriptions_Endpoint");
            e.HasIndex(x => x.UserId).HasDatabaseName("IX_PushSubscriptions_UserId");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_PushSubscriptions_Users_UserId");
        });

        b.Entity<PushKeys>(e =>
        {
            e.ToTable("PushKeys", Schema);
            e.HasKey(x => x.Id).HasName("PK_PushKeys");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.PublicKey).HasMaxLength(200);
            e.Property(x => x.ProtectedPrivateKey).HasMaxLength(2000);
            e.Property(x => x.Subject).HasMaxLength(200);
        });
    }
}

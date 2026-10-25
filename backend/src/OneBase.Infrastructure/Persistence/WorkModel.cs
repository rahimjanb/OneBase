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
            e.Property(x => x.BlockedReason).HasMaxLength(500);
            e.HasIndex(x => x.ProjectId).HasDatabaseName("IX_WorkTasks_ProjectId");
            e.HasIndex(x => new { x.Status, x.DueDate }).HasDatabaseName("IX_WorkTasks_Status_DueDate");
            e.HasIndex(x => x.ArchivedAt).HasDatabaseName("IX_WorkTasks_ArchivedAt");
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTasks_Projects_ProjectId");
            e.HasMany(x => x.Checklist).WithOne(x => x.Task).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkTaskChecklist_WorkTasks_TaskId");
        });

        b.Entity<WorkTaskChecklistItem>(e =>
        {
            e.ToTable("TaskChecklist", Schema);
            e.HasKey(x => x.Id).HasName("PK_WorkTaskChecklist");
            e.Property(x => x.Title).HasMaxLength(300);
            e.HasIndex(x => new { x.TaskId, x.SortOrder }).HasDatabaseName("IX_WorkTaskChecklist_TaskId_SortOrder");
        });

        b.Entity<WorkTaskComment>(e =>
        {
            e.ToTable("TaskComments", Schema);
            e.HasKey(x => x.Id).HasName("PK_WorkTaskComments");
            e.Property(x => x.Text).HasMaxLength(4000);
            e.HasIndex(x => new { x.TaskId, x.CreatedAt }).HasDatabaseName("IX_WorkTaskComments_TaskId_CreatedAt");
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkTaskComments_WorkTasks_TaskId");
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTaskComments_Users_AuthorId");
        });

        b.Entity<WorkTaskAttachment>(e =>
        {
            e.ToTable("TaskAttachments", Schema);
            e.HasKey(x => x.Id).HasName("PK_WorkTaskAttachments");
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.ObjectKey).HasMaxLength(300);
            e.HasIndex(x => x.TaskId).HasDatabaseName("IX_WorkTaskAttachments_TaskId");
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkTaskAttachments_WorkTasks_TaskId");
            e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTaskAttachments_Users_UploadedById");
        });

        b.Entity<WorkTaskEvent>(e =>
        {
            e.ToTable("TaskEvents", Schema);
            e.HasKey(x => x.Id).HasName("PK_WorkTaskEvents");
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Text).HasMaxLength(1000);
            e.HasIndex(x => new { x.TaskId, x.CreatedAt }).HasDatabaseName("IX_WorkTaskEvents_TaskId_CreatedAt");
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkTaskEvents_WorkTasks_TaskId");
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTaskEvents_Users_ActorId");
        });

        b.Entity<WorkProject>(e =>
        {
            e.ToTable("Projects", Schema);
            e.HasKey(x => x.Id).HasName("PK_WorkProjects");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => x.DepartmentId).HasDatabaseName("IX_WorkProjects_DepartmentId");
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkProjects_Departments_DepartmentId");
            e.HasMany(x => x.Members).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkProjectMembers_WorkProjects_ProjectId");
        });

        b.Entity<WorkProjectMember>(e =>
        {
            e.ToTable("ProjectMembers", Schema);
            e.HasKey(x => new { x.ProjectId, x.UserId }).HasName("PK_WorkProjectMembers");
            e.HasIndex(x => x.UserId).HasDatabaseName("IX_WorkProjectMembers_UserId");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkProjectMembers_Users_UserId");
        });

        b.Entity<WorkDepartmentHead>(e =>
        {
            e.ToTable("DepartmentHeads", Schema);
            e.HasKey(x => new { x.DepartmentId, x.UserId }).HasName("PK_WorkDepartmentHeads");
            e.HasIndex(x => x.UserId).HasDatabaseName("IX_WorkDepartmentHeads_UserId");
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkDepartmentHeads_Departments_DepartmentId");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkDepartmentHeads_Users_UserId");
        });

        b.Entity<WorkTaskSuggestion>(e =>
        {
            e.ToTable("TaskSuggestions", Schema);
            e.HasKey(x => x.Id).HasName("PK_WorkTaskSuggestions");
            e.Property(x => x.Fingerprint).HasMaxLength(300);
            e.Property(x => x.SourceKind).HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Problem).HasMaxLength(2000);
            e.Property(x => x.Rationale).HasMaxLength(4000);
            e.Property(x => x.Facts).HasMaxLength(4000);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.SourceHref).HasMaxLength(500);
            e.Property(x => x.RequiredPermission).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(250);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            // Один источник за период — одно предложение: повторный запуск ничего не дублирует, отклонённое не возвращается.
            e.HasIndex(x => x.Fingerprint).IsUnique().HasDatabaseName("IX_WorkTaskSuggestions_Fingerprint");
            e.HasIndex(x => new { x.Status, x.DepartmentId }).HasDatabaseName("IX_WorkTaskSuggestions_Status_DepartmentId");
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WorkTaskSuggestions_Departments_DepartmentId");
            e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WorkTaskSuggestions_Users_AssigneeId");
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

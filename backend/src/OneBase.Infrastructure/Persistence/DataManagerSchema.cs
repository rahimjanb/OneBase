using Microsoft.EntityFrameworkCore;
using OneBase.Domain.AI;

namespace OneBase.Infrastructure.Persistence;

/// <summary>AI Data Manager: чаты, загруженные отчёты, планы изменений и их строки — схема "ai".</summary>
internal static class DataManagerSchema
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<DataConversation>(e =>
        {
            e.ToTable("DataConversations", AiSchema.Schema);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Transcript).HasColumnType("jsonb");
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.UserId, x.LastMessageAt });
            e.HasMany(x => x.Messages).WithOne(x => x.Conversation).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DataMessage>(e =>
        {
            e.ToTable("DataMessages", AiSchema.Schema);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Steps).HasColumnType("jsonb");
            e.Property(x => x.FileIds).HasMaxLength(2000);
            e.Property(x => x.ChangeSetIds).HasMaxLength(2000);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        });

        b.Entity<DataFile>(e =>
        {
            e.ToTable("DataFiles", AiSchema.Schema);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.ObjectKey).HasMaxLength(500);
            e.Property(x => x.Summary).HasColumnType("jsonb");
            e.HasIndex(x => x.Sha256);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        b.Entity<DataChangeSet>(e =>
        {
            e.ToTable("DataChangeSets", AiSchema.Schema);
            e.Property(x => x.FileSha256).HasMaxLength(64);
            e.Property(x => x.Target).HasMaxLength(32);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Period).HasMaxLength(32);
            e.Property(x => x.PlanKind).HasMaxLength(16);
            e.Property(x => x.Mode).HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Summary).HasColumnType("jsonb");
            e.Property(x => x.RequestHash).HasMaxLength(64);
            e.Property(x => x.Verification).HasColumnType("jsonb");
            e.Property(x => x.Error).HasMaxLength(2000);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.ConversationId);
            e.HasIndex(x => x.RequestHash);
            e.HasMany(x => x.Items).WithOne(x => x.ChangeSet).HasForeignKey(x => x.ChangeSetId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DataChangeItem>(e =>
        {
            e.ToTable("DataChangeItems", AiSchema.Schema);
            e.Property(x => x.EntityType).HasMaxLength(32);
            e.Property(x => x.EntityKey).HasMaxLength(200);
            e.Property(x => x.Label).HasMaxLength(300);
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Before).HasMaxLength(400);
            e.Property(x => x.After).HasMaxLength(400);
            e.Property(x => x.SourceRef).HasMaxLength(1000);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.HasIndex(x => new { x.ChangeSetId, x.Position });
            e.HasIndex(x => new { x.EntityKey, x.Status });
        });
    }
}

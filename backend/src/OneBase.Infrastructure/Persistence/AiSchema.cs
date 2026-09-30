using Microsoft.EntityFrameworkCore;
using OneBase.Domain.AI;

namespace OneBase.Infrastructure.Persistence;

/// <summary>РўР°Р±Р»РёС†С‹ AI-РєРѕРЅСЃСѓР»СЊС‚Р°РЅС‚Р° Рё AI-СЃРѕС‚СЂСѓРґРЅРёРєРѕРІ вЂ” СЃС…РµРјР° "ai".</summary>
internal static class AiSchema
{
    public const string Schema = "ai";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<AiProvider>(e =>
        {
            e.ToTable("Providers", Schema);
            e.HasKey(x => x.Code).HasName("PK_AiProviders");
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.BaseUrl).HasMaxLength(500);
            e.Property(x => x.ApiKeyHint).HasMaxLength(16);
            e.Property(x => x.LastTestMessage).HasMaxLength(500);
        });

        b.Entity<AiModel>(e =>
        {
            e.ToTable("Models", Schema);
            e.Property(x => x.ProviderCode).HasMaxLength(32);
            e.Property(x => x.ModelId).HasMaxLength(200);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.InputPricePerMillion).HasPrecision(12, 4);
            e.Property(x => x.OutputPricePerMillion).HasPrecision(12, 4);
            e.HasIndex(x => new { x.ProviderCode, x.ModelId }).IsUnique();
        });

        b.Entity<AiSettings>(e =>
        {
            e.ToTable("Settings", Schema);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.PrimaryProvider).HasMaxLength(32);
            e.Property(x => x.PrimaryModel).HasMaxLength(200);
            e.Property(x => x.FallbackProvider).HasMaxLength(32);
            e.Property(x => x.FallbackModel).HasMaxLength(200);
            e.Property(x => x.RouterProvider).HasMaxLength(32);
            e.Property(x => x.RouterModel).HasMaxLength(200);
            e.Property(x => x.EmbeddingProvider).HasMaxLength(32);
            e.Property(x => x.EmbeddingModel).HasMaxLength(200);
        });

        b.Entity<AiConversation>(e =>
        {
            e.ToTable("Conversations", Schema);
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasIndex(x => new { x.UserId, x.LastMessageAt });
        });

        b.Entity<AiMessage>(e =>
        {
            e.ToTable("Messages", Schema);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Details).HasColumnType("jsonb");
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        });

        b.Entity<AiAgent>(e =>
        {
            e.ToTable("Agents", Schema);
            e.HasKey(x => x.Code).HasName("PK_AiAgents");
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Role).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.ProviderCode).HasMaxLength(32);
            e.Property(x => x.ModelId).HasMaxLength(200);
            e.Property(x => x.DepartmentCode).HasMaxLength(64);
            e.Property(x => x.RequiredPermission).HasMaxLength(100);
        });

        b.Entity<AiAgentKnowledgeSource>(e =>
        {
            e.ToTable("AgentKnowledgeSources", Schema);
            e.HasKey(x => new { x.AgentCode, x.SourceCode });
            e.Property(x => x.AgentCode).HasMaxLength(64);
            e.Property(x => x.SourceCode).HasMaxLength(64);
        });

        b.Entity<AiKnowledgeDocument>(e =>
        {
            e.ToTable("KnowledgeDocuments", Schema);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.FileName).HasMaxLength(300);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.ObjectKey).HasMaxLength(512);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.DepartmentCode).HasMaxLength(64);
            e.Property(x => x.RequiredPermission).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.Property(x => x.EmbeddingModel).HasMaxLength(250);
            e.HasIndex(x => x.Sha256);
        });

        b.Entity<AiKnowledgeChunk>(e =>
        {
            e.ToTable("KnowledgeChunks", Schema);
            e.HasOne<AiKnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.DocumentId, x.Index }).IsUnique();
        });

        b.Entity<AiMemory>(e =>
        {
            e.ToTable("Memory", Schema);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.AgentCode).HasMaxLength(64);
            e.Property(x => x.Topic).HasMaxLength(500);
            e.Property(x => x.Data).HasColumnType("jsonb");
            e.HasIndex(x => new { x.UserId, x.ConversationId, x.CreatedAt });
            e.HasIndex(x => new { x.UserId, x.AgentCode, x.CreatedAt });
        });

        b.Entity<AiAlert>(e =>
        {
            e.ToTable("Alerts", Schema);
            e.Property(x => x.Category).HasMaxLength(32);
            e.Property(x => x.Rule).HasMaxLength(64);
            e.Property(x => x.Key).HasMaxLength(200);
            e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.Recommendation).HasMaxLength(2000);
            e.Property(x => x.Href).HasMaxLength(500);
            e.Property(x => x.RequiredPermission).HasMaxLength(100);
            e.HasIndex(x => x.Key);
            e.HasIndex(x => new { x.ResolvedAt, x.Severity });
        });
    }
}

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
    }
}

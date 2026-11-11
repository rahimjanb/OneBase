using Microsoft.EntityFrameworkCore;
using OneBase.Domain.AI;

namespace OneBase.Infrastructure.Persistence;

/// <summary>ИИ-агенты отделов («Настройки → ИИ-агенты»): агенты, готовые ответы, журнал ответов — схема ai.</summary>
internal static class DialogAgentModel
{
    public const string Schema = "ai";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<DialogAgent>(e =>
        {
            e.ToTable("DialogAgents", Schema);
            e.Property(x => x.Department).HasMaxLength(32);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DisplayName).HasMaxLength(60);
            e.Property(x => x.Intro).HasMaxLength(300);
            e.Property(x => x.Tone).HasMaxLength(16);
            e.Property(x => x.Language).HasMaxLength(8);
            e.Property(x => x.Length).HasMaxLength(16);
            e.Property(x => x.Instructions).HasMaxLength(8000);
            e.Property(x => x.ScreeningQuestions).HasMaxLength(4000);
            e.Property(x => x.AllowedTopics).HasMaxLength(2000);
            e.Property(x => x.ForbiddenTopics).HasMaxLength(2000);
            e.Property(x => x.ForbiddenPhrases).HasMaxLength(2000);
            e.Property(x => x.HandoffPhrases).HasMaxLength(2000);
            e.Property(x => x.HandoffReply).HasMaxLength(500);
            e.Property(x => x.WorkDays).HasMaxLength(20);
            e.Property(x => x.ModelProvider).HasMaxLength(32);
            e.Property(x => x.Model).HasMaxLength(200);
            e.HasIndex(x => new { x.Department, x.Kind });
            e.HasOne<OneBase.Domain.Telegram.TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.AccountId);
        });

        b.Entity<DialogAgentAnswer>(e =>
        {
            e.ToTable("DialogAgentAnswers", Schema);
            e.Property(x => x.Question).HasMaxLength(500);
            e.Property(x => x.Answer).HasMaxLength(2000);
            e.HasOne<DialogAgent>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AgentId, x.SortOrder });
        });

        b.Entity<DialogAgentReply>(e =>
        {
            e.ToTable("DialogAgentReplies", Schema);
            e.Property(x => x.PeerName).HasMaxLength(200);
            e.Property(x => x.Incoming).HasMaxLength(2000);
            e.Property(x => x.Action).HasMaxLength(24);
            e.Property(x => x.Text).HasMaxLength(4000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.BlockReason).HasMaxLength(500);
            e.Property(x => x.Model).HasMaxLength(200);
            e.Property(x => x.Rating).HasMaxLength(8);
            e.Property(x => x.Correction).HasMaxLength(2000);
            e.HasOne<DialogAgent>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AgentId, x.CreatedAt });
            e.HasIndex(x => new { x.ConversationId, x.Status });
            e.HasIndex(x => x.CandidateId);
        });
    }
}

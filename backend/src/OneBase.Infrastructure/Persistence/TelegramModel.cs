using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Telegram;

namespace OneBase.Infrastructure.Persistence;

/// <summary>Схема telegram: аккаунты, агент, разрешения, чаты, материалы, входящие, журнал агента, рассылки, автоотчёты.</summary>
internal static class TelegramModel
{
    public const string Schema = "telegram";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<TelegramAccount>(e =>
        {
            e.ToTable("Accounts", Schema);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.PhoneHint).HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasIndex(x => x.TelegramUserId);
        });

        b.Entity<TelegramAgentSettings>(e =>
        {
            e.ToTable("AgentSettings", Schema);
            e.HasKey(x => x.AccountId);
            e.HasOne<TelegramAccount>().WithOne().HasForeignKey<TelegramAgentSettings>(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.ModelProvider).HasMaxLength(32);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.GenerationModelProvider).HasMaxLength(32);
            e.Property(x => x.GenerationModel).HasMaxLength(100);
            e.Property(x => x.SystemPrompt).HasMaxLength(8000);
            e.Property(x => x.Restrictions).HasMaxLength(8000);
            e.Property(x => x.ResponseStyle).HasMaxLength(16);
            e.Property(x => x.Language).HasMaxLength(8);
            e.Property(x => x.NoInfoReply).HasMaxLength(1000);
            e.Property(x => x.AllowedAttachments).HasMaxLength(200);
            e.Property(x => x.Persona).HasMaxLength(1000);
        });

        b.Entity<TelegramUserPermission>(e =>
        {
            e.ToTable("UserPermissions", Schema);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            // Один Telegram user ID — одна строка на аккаунт: права не размножаются и не переходят по username.
            e.HasIndex(x => new { x.AccountId, x.TelegramUserId }).IsUnique().HasFilter("\"TelegramUserId\" IS NOT NULL");
            e.HasIndex(x => x.UserId);
        });

        b.Entity<TelegramLinkCode>(e =>
        {
            e.ToTable("LinkCodes", Schema);
            e.Property(x => x.CodeHash).HasMaxLength(64);
            e.HasIndex(x => x.CodeHash);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<TelegramChat>(e =>
        {
            e.ToTable("Chats", Schema);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(255);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.CannotSendReason).HasMaxLength(200);
            e.HasOne<TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AccountId, x.PeerId }).IsUnique();
        });

        b.Entity<TelegramMaterial>(e =>
        {
            e.ToTable("Materials", Schema);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ObjectKey).HasMaxLength(500);
            e.HasOne<TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AccountId, x.PeerId, x.CreatedAt });
        });

        b.Entity<TelegramMaterialChunk>(e =>
        {
            e.ToTable("MaterialChunks", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.HasOne<TelegramMaterial>().WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AccountId, x.PeerId });
        });

        // Маршрутизация: подразделения аккаунта и хозяин каждого диалога (нет строк у аккаунта — всё у продаж, как раньше).
        b.Entity<TelegramAccountDepartment>(e =>
        {
            e.ToTable("AccountDepartments", Schema);
            e.HasKey(x => new { x.AccountId, x.Department });
            e.Property(x => x.Department).HasMaxLength(32);
            e.Property(x => x.PauseReason).HasMaxLength(500);
            e.Property(x => x.DailyLimit).HasDefaultValue(200);
            e.Property(x => x.MinIntervalSeconds).HasDefaultValue(5);
            // Одно подразделение по умолчанию на аккаунт — правило TelegramDepartmentService (единственный, кто пишет в таблицу).
            e.HasOne<TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TelegramConversation>(e =>
        {
            e.ToTable("Conversations", Schema);
            e.Property(x => x.ChatType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Department).HasMaxLength(32);
            e.Property(x => x.RouteReason).HasMaxLength(24);
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.AssignedAgent).HasMaxLength(64);
            e.Property(x => x.RelatedType).HasMaxLength(32);
            e.HasOne<TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AccountId, x.PeerId }).IsUnique();
            e.HasIndex(x => new { x.AccountId, x.Department, x.LastMessageAt });
            e.HasIndex(x => new { x.RelatedType, x.RelatedId });
        });

        b.Entity<TelegramConversationMessage>(e =>
        {
            e.ToTable("ConversationMessages", Schema);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(8);
            e.Property(x => x.Text).HasMaxLength(8000);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.Author).HasMaxLength(16);
            e.HasOne<TelegramConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ConversationId, x.SentAt });
            // Повторная доставка входящего не даёт второй строки.
            e.HasIndex(x => new { x.AccountId, x.PeerId, x.MessageId, x.Direction }).IsUnique().HasFilter("\"MessageId\" IS NOT NULL");
        });

        b.Entity<TelegramOutgoingMessage>(e =>
        {
            e.ToTable("OutgoingMessages", Schema);
            e.Property(x => x.Department).HasMaxLength(32);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Text).HasMaxLength(4096);
            e.Property(x => x.IdempotencyKey).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.MessageIds).HasMaxLength(500);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.Property(x => x.RelatedType).HasMaxLength(32);
            e.Property(x => x.Author).HasMaxLength(16);
            e.Property(x => x.FileKey).HasMaxLength(300);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.FileContentType).HasMaxLength(200);
            e.HasOne<TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
            e.HasIndex(x => new { x.AccountId, x.Department, x.SentAt });
            e.HasIndex(x => new { x.RelatedType, x.RelatedId });
        });

        b.Entity<TelegramIncomingMessage>(e =>
        {
            e.ToTable("IncomingMessages", Schema);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Reason).HasMaxLength(200);
            e.HasOne<TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AccountId, x.PeerId, x.MessageId }).IsUnique();
        });

        b.Entity<TelegramAgentExecution>(e =>
        {
            e.ToTable("AgentExecutions", Schema);
            e.Property(x => x.Mode).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Question).HasMaxLength(4000);
            e.Property(x => x.Answer).HasMaxLength(8000);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasOne<TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AccountId, x.CreatedAt });
            e.HasIndex(x => new { x.AccountId, x.TelegramUserId, x.CreatedAt });
        });

        b.Entity<TelegramCampaign>(e =>
        {
            e.ToTable("Campaigns", Schema);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Text).HasMaxLength(4096);
            e.Property(x => x.TimeZone).HasMaxLength(64);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Status, x.ScheduledAt });
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<TelegramCampaignRecipient>(e =>
        {
            e.ToTable("CampaignRecipients", Schema);
            e.Property(x => x.Title).HasMaxLength(255);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.Property(x => x.MessageIds).HasMaxLength(500);
            e.HasOne(x => x.Campaign).WithMany(x => x.Recipients).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            // Один чат — один раз в рассылке: повторный запуск и «повторить ошибки» не создают второе отправление.
            e.HasIndex(x => new { x.CampaignId, x.PeerId }).IsUnique();
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
        });

        b.Entity<TelegramCampaignAttachment>(e =>
        {
            e.ToTable("CampaignAttachments", Schema);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.ObjectKey).HasMaxLength(500);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasOne(x => x.Campaign).WithMany(x => x.Attachments).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        });

        // Автоотчёты: настройки (одна строка), расписания трёх видов, получатели, журнал отправок.
        b.Entity<TelegramReportSettings>(e =>
        {
            e.ToTable("ReportSettings", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Language).HasMaxLength(8);
            e.HasOne<TelegramAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<TelegramWeeklyPdfSettings>(e =>
        {
            e.ToTable("WeeklyPdfSettings", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.GoalAkbFactor).HasPrecision(6, 3);
            e.Property(x => x.GoalPromoFactor).HasPrecision(6, 3);
            e.Property(x => x.ReturnShare15To30).HasPrecision(6, 3);
            e.Property(x => x.ReturnShare30Plus).HasPrecision(6, 3);
            e.Property(x => x.DeficitDays).HasPrecision(6, 2);
        });

        b.Entity<TelegramReportSchedule>(e =>
        {
            e.ToTable("ReportSchedules", Schema);
            e.HasKey(x => x.Kind);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.TimeZone).HasMaxLength(64);
            e.Property(x => x.Metric).HasMaxLength(8);
        });

        // Расписания запусков (код «отчёт.запуск») и журнал запусков: один запуск на код и день.
        b.Entity<TelegramReportJob>(e =>
        {
            e.ToTable("ReportJobs", Schema);
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(40);
            e.Property(x => x.TimeZone).HasMaxLength(64);
        });

        b.Entity<TelegramReportRun>(e =>
        {
            e.ToTable("ReportRuns", Schema);
            e.Property(x => x.Job).HasMaxLength(40);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasIndex(x => new { x.Job, x.Period }).IsUnique();
            e.HasIndex(x => x.StartedAt);
        });

        b.Entity<TelegramReportDailyPlan>(e =>
        {
            e.ToTable("ReportDailyPlans", Schema);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Trigger).HasMaxLength(16);
            e.Property(x => x.Note).HasMaxLength(300);
            e.Property(x => x.Reason).HasMaxLength(300);
            // Одна действующая строка на ТП и день: второй экземпляр службы или повторный круг дубля не создадут; история — с SupersededAt.
            e.HasIndex(x => new { x.LinkoUserId, x.Date }).IsUnique().HasFilter("\"SupersededAt\" IS NULL").HasDatabaseName("IX_ReportDailyPlans_Active");
            e.HasIndex(x => x.Date);
        });

        b.Entity<TelegramReportSubscription>(e =>
        {
            e.ToTable("ReportSubscriptions", Schema);
            e.Property(x => x.Role).HasMaxLength(8);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.SupervisorName).HasMaxLength(200);
            e.Property(x => x.AgentName).HasMaxLength(200);
            e.Property(x => x.Reports).HasMaxLength(1000);
            e.Property(x => x.PdfLanguage).HasMaxLength(8);
            e.Property(x => x.Language).HasMaxLength(8);
            // Регион и направление — без внешнего ключа: схема sales своя (как у остальных ссылок Telegram на данные продаж).
            e.HasIndex(x => x.RegionId);
            e.HasMany(x => x.Chats).WithOne().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TelegramReportSubscriptionChat>(e =>
        {
            e.ToTable("ReportSubscriptionChats", Schema);
            e.HasKey(x => new { x.SubscriptionId, x.ChatId });
            // Чат пропал из диалогов аккаунта — уходит и из автоотправок.
            e.HasOne<TelegramChat>().WithMany().HasForeignKey(x => x.ChatId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TelegramReportDelivery>(e =>
        {
            e.ToTable("ReportDeliveries", Schema);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Report).HasMaxLength(40);
            e.Property(x => x.Role).HasMaxLength(8);
            e.Property(x => x.ScopeKey).HasMaxLength(120);
            e.Property(x => x.Trigger).HasMaxLength(16);
            e.Property(x => x.DataMode).HasMaxLength(8);
            e.Property(x => x.RecipientTitle).HasMaxLength(255);
            e.Property(x => x.ScopeTitle).HasMaxLength(255);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.Property(x => x.MessageIds).HasMaxLength(500);
            e.Property(x => x.Summary).HasMaxLength(500);
            e.Property(x => x.Files).HasMaxLength(2000);
            e.Property(x => x.Stage).HasMaxLength(16);
            e.Property(x => x.Log).HasMaxLength(4000);
            // Прежние строки дневного отчёта: один отчёт автоотправки за день в один чат.
            e.HasIndex(x => new { x.SubscriptionId, x.ChatId, x.Kind, x.Period }).IsUnique()
                .HasFilter("\"Trigger\" = 'schedule' AND \"SubscriptionId\" IS NOT NULL AND \"ChatId\" IS NOT NULL");
            // Ключ отправки по расписанию: запуск × день × чат × область — один раз, даже если человек в нескольких получателях
            // или служба запущена в двух экземплярах.
            e.HasIndex(x => new { x.Report, x.Period, x.ChatId, x.ScopeKey }).IsUnique()
                .HasFilter("\"Trigger\" = 'schedule' AND \"Report\" IS NOT NULL AND \"ChatId\" IS NOT NULL")
                .HasDatabaseName("IX_ReportDeliveries_ScheduleKey");
            e.HasIndex(x => x.RunId);
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
            e.HasIndex(x => x.CreatedAt);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Identity;
using OneBase.Domain.Recruitment;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема recruitment — подбор персонала: заявки на сотрудников, вакансии, единая база кандидатов (контакты, источники, резюме, согласия,
/// подозрения на дубли), отклики с историей этапов, настройки и тексты согласия.
/// </summary>
internal static class RecruitmentModel
{
    public const string Schema = "recruitment";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<HiringRequest>(e =>
        {
            e.ToTable("HiringRequests", Schema);
            e.Property(x => x.Number).UseIdentityByDefaultColumn();
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Position).HasMaxLength(200);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Requirements).HasMaxLength(4000);
            e.Property(x => x.SalaryFrom).HasPrecision(18, 2);
            e.Property(x => x.SalaryTo).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DecisionComment).HasMaxLength(1000);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.CreatedById);
        });

        b.Entity<Vacancy>(e =>
        {
            e.ToTable("Vacancies", Schema);
            e.Ignore(x => x.Code);
            e.Property(x => x.Number).UseIdentityByDefaultColumn();
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(8000);
            e.Property(x => x.Requirements).HasMaxLength(4000);
            e.Property(x => x.Conditions).HasMaxLength(4000);
            e.Property(x => x.SalaryFrom).HasPrecision(18, 2);
            e.Property(x => x.SalaryTo).HasPrecision(18, 2);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.EmploymentType).HasMaxLength(16);
            e.Property(x => x.HhVacancyId).HasMaxLength(50);
            e.Property(x => x.Criteria).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<HiringRequest>().WithMany().HasForeignKey(x => x.HiringRequestId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        b.Entity<Candidate>(e =>
        {
            e.ToTable("Candidates", Schema);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Language).HasMaxLength(8);
            e.Property(x => x.PreferredScript).HasMaxLength(4);
            e.Property(x => x.LastDetectedLanguage).HasMaxLength(8);
            e.Property(x => x.LanguageConfidence).HasPrecision(3, 2);
            e.Property(x => x.LanguageSource).HasMaxLength(12);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.HasIndex(x => x.LastActivityAt);
            e.HasIndex(x => x.RetentionFrom);
        });

        b.Entity<CandidateContact>(e =>
        {
            e.ToTable("CandidateContacts", Schema);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Value).HasMaxLength(200);
            e.Property(x => x.Source).HasMaxLength(16);
            e.HasOne<Candidate>().WithMany(c => c.Contacts).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Kind, x.Value });
            // Подтверждённый контакт принадлежит одному кандидату: второй кандидат с ним — тот же человек (объединение при приёме).
            e.HasIndex(x => new { x.Kind, x.Value }).IsUnique().HasFilter("\"Verified\"").HasDatabaseName("IX_CandidateContacts_Verified");
            e.HasIndex(x => new { x.CandidateId, x.Kind, x.Value }).IsUnique();
        });

        b.Entity<CandidateSource>(e =>
        {
            e.ToTable("CandidateSources", Schema);
            e.Property(x => x.Source).HasMaxLength(16);
            e.Property(x => x.ExternalId).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne<Candidate>().WithMany(c => c.Sources).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique().HasFilter("\"ExternalId\" IS NOT NULL");
            e.HasIndex(x => x.CandidateId);
        });

        b.Entity<CandidateResume>(e =>
        {
            e.ToTable("CandidateResumes", Schema);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.ObjectKey).HasMaxLength(300);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.TextError).HasMaxLength(500);
            e.Property(x => x.Source).HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.OcrStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.AnalysisStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DetectedType).HasMaxLength(8);
            e.Property(x => x.ProcessingError).HasMaxLength(500);
            e.Property(x => x.Profile).HasColumnType("jsonb");
            e.Property(x => x.Summary).HasMaxLength(2000);
            e.HasOne<Candidate>().WithMany(c => c.Resumes).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
            // Тот же файл у кандидата — один документ; файл, ещё не скачанный из Telegram (Sha256 = null), уникальности не мешает.
            e.HasIndex(x => new { x.CandidateId, x.Sha256 }).IsUnique();
        });

        b.Entity<DialogRead>(e =>
        {
            e.ToTable("DialogReads", Schema);
            e.HasKey(x => new { x.ConversationId, x.UserId });
            e.HasOne<OneBase.Domain.Telegram.TelegramConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecruitmentJob>(e =>
        {
            e.ToTable("Jobs", Schema);
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Queue).HasMaxLength(40);
            e.Property(x => x.Key).HasMaxLength(120);
            e.Property(x => x.Payload).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasIndex(x => new { x.Queue, x.Status, x.RunAfter });
            e.HasIndex(x => x.RelatedId);
            // Одно и то же задание не ставится дважды, пока не выполнено.
            e.HasIndex(x => new { x.Queue, x.Key }).IsUnique().HasFilter("\"Status\" IN ('Pending', 'Running')");
        });

        b.Entity<CandidateApplication>(e =>
        {
            e.ToTable("Applications", Schema);
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Source).HasMaxLength(16);
            e.Property(x => x.RejectReason).HasMaxLength(1000);
            e.HasOne(x => x.Candidate).WithMany(c => c.Applications).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Vacancy).WithMany().HasForeignKey(x => x.VacancyId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CandidateId, x.VacancyId }).IsUnique();
            e.HasIndex(x => new { x.VacancyId, x.Stage });
        });

        b.Entity<ApplicationEvent>(e =>
        {
            e.ToTable("ApplicationEvents", Schema);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.FromStage).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.ToStage).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasOne<CandidateApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ApplicationId, x.At });
        });

        b.Entity<CandidateConsent>(e =>
        {
            e.ToTable("CandidateConsents", Schema);
            e.Property(x => x.Purpose).HasMaxLength(32);
            e.Property(x => x.Channel).HasMaxLength(16);
            e.HasOne<Candidate>().WithMany(c => c.Consents).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CandidateId);
        });

        b.Entity<CandidateDuplicate>(e =>
        {
            e.ToTable("CandidateDuplicates", Schema);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Value).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne<Candidate>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Candidate>().WithMany().HasForeignKey(x => x.OtherCandidateId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CandidateId, x.OtherCandidateId, x.Kind, x.Value }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        b.Entity<VacancyPublication>(e =>
        {
            e.ToTable("Publications", Schema);
            e.Property(x => x.ChatTitle).HasMaxLength(200);
            e.Property(x => x.ChatUsername).HasMaxLength(64);
            e.Property(x => x.Text).HasMaxLength(4096);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Link).HasMaxLength(300);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasOne<Vacancy>().WithMany().HasForeignKey(x => x.VacancyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.VacancyId, x.CreatedAt });
            e.HasIndex(x => new { x.AccountId, x.PeerId, x.MessageId });
            e.HasIndex(x => new { x.Status, x.ScheduledAt });
        });

        b.Entity<RecruitmentSource>(e =>
        {
            e.ToTable("Sources", Schema);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(16);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.AccountId, x.PeerId }).IsUnique();
        });

        b.Entity<TelegramLead>(e =>
        {
            e.ToTable("Leads", Schema);
            e.Property(x => x.ChatTitle).HasMaxLength(200);
            e.Property(x => x.FromUsername).HasMaxLength(64);
            e.Property(x => x.FromName).HasMaxLength(200);
            e.Property(x => x.Excerpt).HasMaxLength(500);
            e.Property(x => x.Keywords).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.AccountId, x.ChatPeerId, x.MessageId }).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        b.Entity<HhConnection>(e =>
        {
            e.ToTable("HhConnection", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ClientId).HasMaxLength(200);
            e.Property(x => x.RedirectUri).HasMaxLength(500);
            e.Property(x => x.ContactEmail).HasMaxLength(200);
            e.Property(x => x.StateHash).HasMaxLength(64);
            e.Property(x => x.EmployerId).HasMaxLength(50);
            e.Property(x => x.EmployerName).HasMaxLength(200);
            e.Property(x => x.LastSyncMessage).HasMaxLength(1000);
        });

        b.Entity<HhNegotiation>(e =>
        {
            e.ToTable("HhNegotiations", Schema);
            e.Property(x => x.NegotiationId).HasMaxLength(50);
            e.Property(x => x.HhVacancyId).HasMaxLength(50);
            e.Property(x => x.ResumeId).HasMaxLength(100);
            e.Property(x => x.ResumeUrl).HasMaxLength(500);
            e.Property(x => x.State).HasMaxLength(50);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasIndex(x => x.NegotiationId).IsUnique();
            e.HasIndex(x => x.ApplicationId);
            e.HasIndex(x => x.CandidateId);
        });

        b.Entity<CandidateScore>(e =>
        {
            e.ToTable("CandidateScores", Schema);
            e.Property(x => x.Summary).HasMaxLength(2000);
            e.Property(x => x.Risks).HasMaxLength(2000);
            e.Property(x => x.Model).HasMaxLength(100);
            e.HasOne<CandidateApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ApplicationId, x.CreatedAt });
        });

        b.Entity<VacancyInterviewer>(e =>
        {
            e.ToTable("VacancyInterviewers", Schema);
            e.HasKey(x => new { x.VacancyId, x.UserId });
            e.HasOne<Vacancy>().WithMany().HasForeignKey(x => x.VacancyId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Interview>(e =>
        {
            e.ToTable("Interviews", Schema);
            e.Property(x => x.Format).HasMaxLength(16);
            e.Property(x => x.Location).HasMaxLength(300);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.ProposedSlotsJson).HasMaxLength(2000);
            e.Property(x => x.Participants).HasMaxLength(2000);
            e.Property(x => x.TimeZone).HasMaxLength(40).HasDefaultValue("Asia/Tashkent");
            e.Property(x => x.MeetingUrl).HasMaxLength(500);
            e.Property(x => x.CandidateConfirmation).HasConversion<string>().HasMaxLength(16).HasDefaultValue(CandidateConfirmation.Pending);
            e.Property(x => x.CancelledBy).HasMaxLength(16);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<CandidateApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Status, x.StartsAt });
            e.HasIndex(x => x.CandidateId);
            // Одно событие календаря — одно собеседование: повтор подтверждения не создаёт второго события.
            e.HasIndex(x => x.CalendarEventId).IsUnique().HasFilter("\"CalendarEventId\" IS NOT NULL");
        });

        b.Entity<VacancyInterviewSettings>(e =>
        {
            e.ToTable("InterviewSettings", Schema);
            e.HasKey(x => x.VacancyId);
            e.HasOne<Vacancy>().WithOne().HasForeignKey<VacancyInterviewSettings>(x => x.VacancyId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Days).HasMaxLength(20);
            e.Property(x => x.Breaks).HasMaxLength(200);
            e.Property(x => x.Format).HasMaxLength(16);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.MeetingUrl).HasMaxLength(500);
            e.Property(x => x.TimeZone).HasMaxLength(40);
            e.Property(x => x.Instructions).HasMaxLength(1000);
        });

        b.Entity<InterviewFeedback>(e =>
        {
            e.ToTable("InterviewFeedback", Schema);
            e.Property(x => x.Result).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.NextStage).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Strengths).HasMaxLength(2000);
            e.Property(x => x.Weaknesses).HasMaxLength(2000);
            e.Property(x => x.Comments).HasMaxLength(4000);
            e.Property(x => x.Recommendation).HasMaxLength(2000);
            e.HasOne<Interview>().WithMany().HasForeignKey(x => x.InterviewId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.InterviewId, x.AuthorId }).IsUnique();
        });

        b.Entity<RecruitmentException>(e =>
        {
            e.ToTable("Exceptions", Schema);
            e.Property(x => x.Kind).HasMaxLength(24);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.DedupKey).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Resolution).HasMaxLength(1000);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.DedupKey).IsUnique().HasFilter("\"Status\" = 'Open' AND \"DedupKey\" IS NOT NULL");
            e.HasIndex(x => x.CandidateId);
        });

        b.Entity<RecruitmentSettings>(e =>
        {
            e.ToTable("Settings", Schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AssistantName).HasMaxLength(200);
            e.Property(x => x.InterviewDays).HasMaxLength(20);
            e.Property(x => x.InterviewSlotMinutes).HasDefaultValue(45);
            e.Property(x => x.InterviewDays).HasDefaultValue("1,2,3,4,5,6");
            e.Property(x => x.InterviewDayStart).HasDefaultValue(new TimeOnly(10, 0));
            e.Property(x => x.InterviewDayEnd).HasDefaultValue(new TimeOnly(18, 0));
        });

        b.Entity<RecruitmentConsentText>(e =>
        {
            e.ToTable("ConsentTexts", Schema);
            e.HasKey(x => x.Version);
            e.Property(x => x.Version).ValueGeneratedNever();
            e.Property(x => x.Text).HasMaxLength(8000);
        });
    }
}

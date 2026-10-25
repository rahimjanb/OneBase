using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Integrations;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;
using OneBase.Domain.Files;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Domain.Work;

namespace OneBase.Infrastructure.Persistence;

public sealed class OneBaseDbContext(DbContextOptions<OneBaseDbContext> options)
    : DbContext(options), IAppDbContext, IDataProtectionKeyContext
{
    // Интеграции и ключи шифрования их секретов
    public DbSet<IntegrationConnection> Integrations => Set<IntegrationConnection>();
    public DbSet<DataSourceConnection> DataSourceConnections => Set<DataSourceConnection>();
    public DbSet<DataSourceBinding> DataSourceBindings => Set<DataSourceBinding>();
    public DbSet<DataSourceSyncRun> DataSourceSyncRuns => Set<DataSourceSyncRun>();
    public DbSet<DataSourceConflict> DataSourceConflicts => Set<DataSourceConflict>();

    public DbSet<OneBase.Domain.Telegram.TelegramAccount> TelegramAccounts => Set<OneBase.Domain.Telegram.TelegramAccount>();
    public DbSet<OneBase.Domain.Telegram.TelegramAgentSettings> TelegramAgentSettings => Set<OneBase.Domain.Telegram.TelegramAgentSettings>();
    public DbSet<OneBase.Domain.Telegram.TelegramUserPermission> TelegramUserPermissions => Set<OneBase.Domain.Telegram.TelegramUserPermission>();
    public DbSet<OneBase.Domain.Telegram.TelegramLinkCode> TelegramLinkCodes => Set<OneBase.Domain.Telegram.TelegramLinkCode>();
    public DbSet<OneBase.Domain.Telegram.TelegramChat> TelegramChats => Set<OneBase.Domain.Telegram.TelegramChat>();
    public DbSet<OneBase.Domain.Telegram.TelegramMaterial> TelegramMaterials => Set<OneBase.Domain.Telegram.TelegramMaterial>();
    public DbSet<OneBase.Domain.Telegram.TelegramMaterialChunk> TelegramMaterialChunks => Set<OneBase.Domain.Telegram.TelegramMaterialChunk>();
    public DbSet<OneBase.Domain.Telegram.TelegramIncomingMessage> TelegramIncomingMessages => Set<OneBase.Domain.Telegram.TelegramIncomingMessage>();
    public DbSet<OneBase.Domain.Telegram.TelegramAgentExecution> TelegramAgentExecutions => Set<OneBase.Domain.Telegram.TelegramAgentExecution>();
    public DbSet<OneBase.Domain.Telegram.TelegramCampaign> TelegramCampaigns => Set<OneBase.Domain.Telegram.TelegramCampaign>();
    public DbSet<OneBase.Domain.Telegram.TelegramCampaignRecipient> TelegramCampaignRecipients => Set<OneBase.Domain.Telegram.TelegramCampaignRecipient>();
    public DbSet<OneBase.Domain.Telegram.TelegramCampaignAttachment> TelegramCampaignAttachments => Set<OneBase.Domain.Telegram.TelegramCampaignAttachment>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportSettings> TelegramReportSettings => Set<OneBase.Domain.Telegram.TelegramReportSettings>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportSchedule> TelegramReportSchedules => Set<OneBase.Domain.Telegram.TelegramReportSchedule>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportSubscription> TelegramReportSubscriptions => Set<OneBase.Domain.Telegram.TelegramReportSubscription>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportSubscriptionChat> TelegramReportSubscriptionChats => Set<OneBase.Domain.Telegram.TelegramReportSubscriptionChat>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportDelivery> TelegramReportDeliveries => Set<OneBase.Domain.Telegram.TelegramReportDelivery>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportJob> TelegramReportJobs => Set<OneBase.Domain.Telegram.TelegramReportJob>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportRun> TelegramReportRuns => Set<OneBase.Domain.Telegram.TelegramReportRun>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportDailyPlan> TelegramReportDailyPlans => Set<OneBase.Domain.Telegram.TelegramReportDailyPlan>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<FileItem> Files => Set<FileItem>();
    public DbSet<FileVersion> FileVersions => Set<FileVersion>();
    public DbSet<ResourcePermission> ResourcePermissions => Set<ResourcePermission>();
    public DbSet<FileConnection> FileConnections => Set<FileConnection>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();

    public DbSet<AgentToolGrant> AgentToolGrants => Set<AgentToolGrant>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    // AI: провайдеры, модели, настройки
    public DbSet<AiProvider> AiProviders => Set<AiProvider>();
    public DbSet<AiModel> AiModels => Set<AiModel>();
    public DbSet<AiSettings> AiSettings => Set<AiSettings>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiReport> AiReports => Set<AiReport>();
    public DbSet<AiAgent> AiAgents => Set<AiAgent>();
    public DbSet<AiAgentKnowledgeSource> AiAgentKnowledgeSources => Set<AiAgentKnowledgeSource>();
    public DbSet<AiKnowledgeDocument> AiKnowledgeDocuments => Set<AiKnowledgeDocument>();
    public DbSet<AiKnowledgeChunk> AiKnowledgeChunks => Set<AiKnowledgeChunk>();
    public DbSet<AiMemory> AiMemories => Set<AiMemory>();
    public DbSet<AiAlert> AiAlerts => Set<AiAlert>();
    public DbSet<AiUsage> AiUsage => Set<AiUsage>();
    public DbSet<AiAuditLog> AiAuditLogs => Set<AiAuditLog>();
    public DbSet<AiModelComparison> AiModelComparisons => Set<AiModelComparison>();

    public DbSet<DataConversation> DataConversations => Set<DataConversation>();
    public DbSet<DataMessage> DataMessages => Set<DataMessage>();
    public DbSet<DataFile> DataFiles => Set<DataFile>();
    public DbSet<DataChangeSet> DataChangeSets => Set<DataChangeSet>();
    public DbSet<DataChangeItem> DataChangeItems => Set<DataChangeItem>();

    // Linko (зеркало SFA)
    public DbSet<LinkoUser> LinkoUsers => Set<LinkoUser>();
    public DbSet<LinkoMarket> LinkoMarkets => Set<LinkoMarket>();
    public DbSet<LinkoProduct> LinkoProducts => Set<LinkoProduct>();
    public DbSet<LinkoProductType> LinkoProductTypes => Set<LinkoProductType>();
    public DbSet<LinkoBorder> LinkoBorders => Set<LinkoBorder>();
    public DbSet<LinkoMarketUser> LinkoMarketUsers => Set<LinkoMarketUser>();
    public DbSet<LinkoOrder> LinkoOrders => Set<LinkoOrder>();
    public DbSet<LinkoOrderLine> LinkoOrderLines => Set<LinkoOrderLine>();
    public DbSet<LinkoOrderReturn> LinkoOrderReturns => Set<LinkoOrderReturn>();
    public DbSet<LinkoOrderReturnLine> LinkoOrderReturnLines => Set<LinkoOrderReturnLine>();
    public DbSet<LinkoVisit> LinkoVisits => Set<LinkoVisit>();
    public DbSet<LinkoKpiPlan> LinkoKpiPlans => Set<LinkoKpiPlan>();
    public DbSet<LinkoSyncState> LinkoSyncStates => Set<LinkoSyncState>();
    public DbSet<LinkoPromotion> LinkoPromotions => Set<LinkoPromotion>();
    public DbSet<LinkoPromotionProduct> LinkoPromotionProducts => Set<LinkoPromotionProduct>();
    public DbSet<LinkoStock> LinkoStocks => Set<LinkoStock>();
    public DbSet<LinkoProductBalance> LinkoProductBalances => Set<LinkoProductBalance>();
    public DbSet<LinkoReservation> LinkoReservations => Set<LinkoReservation>();
    public DbSet<LinkoStockTransfer> LinkoStockTransfers => Set<LinkoStockTransfer>();
    public DbSet<LinkoStockTransferLine> LinkoStockTransferLines => Set<LinkoStockTransferLine>();
    public DbSet<LinkoPayment> LinkoPayments => Set<LinkoPayment>();
    public DbSet<LinkoPriceList> LinkoPriceLists => Set<LinkoPriceList>();
    public DbSet<LinkoPriceListItem> LinkoPriceListItems => Set<LinkoPriceListItem>();
    public DbSet<LinkoProvider> LinkoProviders => Set<LinkoProvider>();
    public DbSet<LinkoCurrency> LinkoCurrencies => Set<LinkoCurrency>();
    public DbSet<LinkoContract> LinkoContracts => Set<LinkoContract>();

    // Продажи: оргструктура, планы, цели
    public DbSet<SalesDirection> SalesDirections => Set<SalesDirection>();
    public DbSet<SalesRegion> SalesRegions => Set<SalesRegion>();
    public DbSet<SalesAgentProfile> SalesAgentProfiles => Set<SalesAgentProfile>();
    public DbSet<SalesRegionPlan> SalesRegionPlans => Set<SalesRegionPlan>();
    public DbSet<SalesAgentPlan> SalesAgentPlans => Set<SalesAgentPlan>();
    public DbSet<SalesTarget> SalesTargets => Set<SalesTarget>();
    public DbSet<SalesPnlValue> SalesPnlValues => Set<SalesPnlValue>();
    public DbSet<SalesStaffPlan> SalesStaffPlans => Set<SalesStaffPlan>();
    public DbSet<SalesHoliday> SalesHolidays => Set<SalesHoliday>();
    public DbSet<SalesStaffIndicator> SalesStaffIndicators => Set<SalesStaffIndicator>();
    public DbSet<SalesDealerLedgerEntry> SalesDealerLedger => Set<SalesDealerLedgerEntry>();
    public DbSet<SalesDealerTerm> SalesDealerTerms => Set<SalesDealerTerm>();

    // Sales Base (полевые продажи)
    public DbSet<FieldMember> FieldMembers => Set<FieldMember>();
    public DbSet<FieldTeam> FieldTeams => Set<FieldTeam>();
    public DbSet<FieldCustomer> FieldCustomers => Set<FieldCustomer>();
    public DbSet<FieldCustomerStats> FieldCustomerStats => Set<FieldCustomerStats>();
    public DbSet<FieldRoute> FieldRoutes => Set<FieldRoute>();
    public DbSet<FieldRoutePoint> FieldRoutePoints => Set<FieldRoutePoint>();
    public DbSet<FieldVisit> FieldVisits => Set<FieldVisit>();
    public DbSet<FieldJointVisit> FieldJointVisits => Set<FieldJointVisit>();
    public DbSet<FieldJointVisitParticipant> FieldJointVisitParticipants => Set<FieldJointVisitParticipant>();
    public DbSet<FieldTask> FieldTasks => Set<FieldTask>();
    public DbSet<FieldRecommendation> FieldRecommendations => Set<FieldRecommendation>();
    public DbSet<FieldNotification> FieldNotifications => Set<FieldNotification>();
    public DbSet<FieldSettings> FieldSettings => Set<FieldSettings>();

    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<PushKeys> PushKeys => Set<PushKeys>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        SalesModel.ConfigureLinko(b);
        SalesModel.ConfigureSales(b);
        AiSchema.Configure(b);
        DataManagerSchema.Configure(b);
        FieldModel.Configure(b);
        WorkModel.Configure(b);
        TelegramModel.Configure(b);

        // «Настройки → Источники данных»: таблицы Google, привязки разделов к листам и журнал синхронизаций.
        b.Entity<DataSourceConnection>(e =>
        {
            e.ToTable("DataSourceConnections");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.SpreadsheetId).HasMaxLength(200);
            e.Property(x => x.Url).HasMaxLength(500);
            e.Property(x => x.Access).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ServiceAccountEmail).HasMaxLength(320);
            e.Property(x => x.LastCheckMessage).HasMaxLength(1000);
            e.Property(x => x.Sheets).HasColumnType("jsonb");
        });

        b.Entity<DataSourceBinding>(e =>
        {
            e.ToTable("DataSourceBindings");
            e.Property(x => x.Section).HasMaxLength(32);
            e.Property(x => x.Sheets).HasColumnType("jsonb");
            e.Property(x => x.Fields).HasColumnType("jsonb");
            e.Property(x => x.Schedule).HasMaxLength(16);
            e.Property(x => x.DailyAt).HasMaxLength(5);
            e.Property(x => x.LastStatus).HasMaxLength(16);
            e.Property(x => x.LastMessage).HasMaxLength(1100);
            e.HasOne(x => x.Connection).WithMany().HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Section);
            e.HasIndex(x => new { x.Enabled, x.NextRunAt });
        });

        b.Entity<DataSourceSyncRun>(e =>
        {
            e.ToTable("DataSourceSyncRuns");
            e.Property(x => x.Trigger).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Message).HasMaxLength(1100);
            e.Property(x => x.Details).HasColumnType("jsonb");
            e.HasOne(x => x.Binding).WithMany(x => x.Runs).HasForeignKey(x => x.BindingId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.BindingId, x.CreatedAt });
        });

        // Журнал конфликтов источников: удаление привязки конфликты не удаляет (они закрываются, история остаётся).
        b.Entity<DataSourceConflict>(e =>
        {
            e.ToTable("DataSourceConflicts");
            e.Property(x => x.Source).HasMaxLength(16);
            e.Property(x => x.Field).HasMaxLength(32);
            e.Property(x => x.Entity).HasMaxLength(300);
            e.Property(x => x.Period).HasMaxLength(32);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.SourceRef).HasMaxLength(300);
            e.Property(x => x.Rule).HasMaxLength(300);
            e.Property(x => x.Decision).HasMaxLength(300);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.HasOne<DataSourceBinding>().WithMany().HasForeignKey(x => x.BindingId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.ResolvedAt, x.LastSeenAt });
            e.HasIndex(x => new { x.BindingId, x.Fingerprint });
        });

        b.Entity<IntegrationConnection>(e =>
        {
            e.ToTable("Integrations");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.DepartmentCode).HasMaxLength(64);
            e.Property(x => x.BaseUrl).HasMaxLength(500);
            e.Property(x => x.SecretHint).HasMaxLength(16);
            e.Property(x => x.LastTestMessage).HasMaxLength(500);
        });

        // Identity
        b.Entity<Department>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Login).HasMaxLength(64);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.Position).HasMaxLength(150);
            e.HasIndex(x => x.Login).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Department).WithMany(x => x.Users).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<UserSession>(e =>
        {
            e.Property(x => x.UserAgent).HasMaxLength(300);
            e.Property(x => x.Ip).HasMaxLength(64);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Role>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(x => x.Roles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId);
        });

        b.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.Code });
            e.Property(x => x.Code).HasMaxLength(100);
            e.HasOne(x => x.Role).WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId);
        });

        // Files
        b.Entity<Folder>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(255);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.ParentId, x.Name });
            e.HasIndex(x => new { x.DepartmentId, x.ParentId });
            // Корневая папка отдела — одна (создаётся при первом открытии; гонка двух запросов упрётся в индекс).
            e.HasIndex(x => x.DepartmentId).IsUnique().HasFilter("\"ParentId\" IS NULL AND \"DepartmentId\" IS NOT NULL")
                .HasDatabaseName("IX_Folders_DepartmentRoot");
        });

        b.Entity<FileItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(255);
            e.HasOne(x => x.Folder).WithMany(x => x.Files).HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.FolderId, x.Name });
            e.HasIndex(x => new { x.DepartmentId, x.DeletedAt });
        });

        b.Entity<FileVersion>(e =>
        {
            e.Property(x => x.ObjectKey).HasMaxLength(512);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasOne(x => x.FileItem).WithMany(x => x.Versions).HasForeignKey(x => x.FileItemId);
            e.HasIndex(x => new { x.FileItemId, x.Number }).IsUnique();
        });

        b.Entity<ResourcePermission>(e =>
        {
            e.Property(x => x.PrincipalType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.PrincipalId).HasMaxLength(100);
            e.HasOne<Folder>().WithMany(x => x.Permissions).HasForeignKey(x => x.FolderId);
            e.HasOne<FileItem>().WithMany(x => x.Permissions).HasForeignKey(x => x.FileItemId);
            e.HasIndex(x => new { x.PrincipalType, x.PrincipalId });
            e.ToTable(t => t.HasCheckConstraint(
                "CK_ResourcePermissions_SingleTarget",
                "(\"FolderId\" IS NULL) <> (\"FileItemId\" IS NULL)"));
        });

        b.Entity<FileConnection>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.Access).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.LinkTokenHash).HasMaxLength(64);
            e.HasIndex(x => x.LinkTokenHash).IsUnique().HasFilter("\"LinkTokenHash\" IS NOT NULL");
            // У отдела одно действующее подключение; отозванные остаются для журнала.
            e.HasIndex(x => x.DepartmentId).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
        });

        // Audit
        b.Entity<SystemLog>(e =>
        {
            e.Property(x => x.Level).HasMaxLength(16);
            e.Property(x => x.Source).HasMaxLength(16);
            e.Property(x => x.Category).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(4000);
            e.Property(x => x.Exception).HasMaxLength(16000);
            e.Property(x => x.TraceId).HasMaxLength(64);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => new { x.Source, x.Timestamp });
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.ActorType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ActorId).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.EntityType).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.Data).HasColumnType("jsonb");
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        // AI
        b.Entity<AgentToolGrant>(e =>
        {
            e.Property(x => x.AgentCode).HasMaxLength(64);
            e.Property(x => x.ToolName).HasMaxLength(100);
            e.HasIndex(x => new { x.AgentCode, x.ToolName }).IsUnique();
            e.Property(x => x.Enabled).HasDefaultValue(true);
        });

        b.Entity<ApprovalRequest>(e =>
        {
            e.Property(x => x.AgentCode).HasMaxLength(64);
            e.Property(x => x.ToolName).HasMaxLength(100);
            e.Property(x => x.Arguments).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.Status);
        });
    }
}

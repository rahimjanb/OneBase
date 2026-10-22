using Microsoft.EntityFrameworkCore;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;
using OneBase.Domain.Files;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;

namespace OneBase.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<Role> Roles { get; }
    DbSet<Department> Departments { get; }

    DbSet<Folder> Folders { get; }
    DbSet<FileItem> Files { get; }
    DbSet<FileVersion> FileVersions { get; }
    DbSet<ResourcePermission> ResourcePermissions { get; }
    DbSet<FileConnection> FileConnections { get; }

    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemLog> SystemLogs { get; }

    DbSet<AgentToolGrant> AgentToolGrants { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }

    DbSet<AiProvider> AiProviders { get; }
    DbSet<AiModel> AiModels { get; }
    DbSet<AiSettings> AiSettings { get; }
    DbSet<AiConversation> AiConversations { get; }
    DbSet<AiMessage> AiMessages { get; }
    DbSet<AiReport> AiReports { get; }
    DbSet<AiAgent> AiAgents { get; }
    DbSet<AiAgentKnowledgeSource> AiAgentKnowledgeSources { get; }
    DbSet<AiKnowledgeDocument> AiKnowledgeDocuments { get; }
    DbSet<AiKnowledgeChunk> AiKnowledgeChunks { get; }
    DbSet<AiMemory> AiMemories { get; }
    DbSet<AiAlert> AiAlerts { get; }
    DbSet<AiUsage> AiUsage { get; }
    DbSet<AiAuditLog> AiAuditLogs { get; }

    // AI Data Manager: чаты, отчёты, планы изменений
    DbSet<DataConversation> DataConversations { get; }
    DbSet<DataMessage> DataMessages { get; }
    DbSet<DataFile> DataFiles { get; }
    DbSet<DataChangeSet> DataChangeSets { get; }
    DbSet<DataChangeItem> DataChangeItems { get; }

    DbSet<LinkoUser> LinkoUsers { get; }
    DbSet<LinkoMarket> LinkoMarkets { get; }
    DbSet<LinkoProduct> LinkoProducts { get; }
    DbSet<LinkoProductType> LinkoProductTypes { get; }
    DbSet<LinkoMarketUser> LinkoMarketUsers { get; }
    DbSet<LinkoOrder> LinkoOrders { get; }
    DbSet<LinkoOrderLine> LinkoOrderLines { get; }
    DbSet<LinkoOrderReturn> LinkoOrderReturns { get; }
    DbSet<LinkoOrderReturnLine> LinkoOrderReturnLines { get; }
    DbSet<LinkoVisit> LinkoVisits { get; }
    DbSet<LinkoKpiPlan> LinkoKpiPlans { get; }
    DbSet<LinkoSyncState> LinkoSyncStates { get; }
    DbSet<LinkoPromotion> LinkoPromotions { get; }
    DbSet<LinkoPromotionProduct> LinkoPromotionProducts { get; }
    DbSet<LinkoStock> LinkoStocks { get; }
    DbSet<LinkoProductBalance> LinkoProductBalances { get; }
    DbSet<LinkoReservation> LinkoReservations { get; }
    DbSet<LinkoStockTransfer> LinkoStockTransfers { get; }
    DbSet<LinkoStockTransferLine> LinkoStockTransferLines { get; }
    DbSet<LinkoPayment> LinkoPayments { get; }
    DbSet<LinkoPriceList> LinkoPriceLists { get; }
    DbSet<LinkoPriceListItem> LinkoPriceListItems { get; }
    DbSet<LinkoProvider> LinkoProviders { get; }
    DbSet<LinkoCurrency> LinkoCurrencies { get; }
    DbSet<LinkoContract> LinkoContracts { get; }

    DbSet<SalesDirection> SalesDirections { get; }
    DbSet<SalesRegion> SalesRegions { get; }
    DbSet<SalesAgentProfile> SalesAgentProfiles { get; }
    DbSet<SalesRegionPlan> SalesRegionPlans { get; }
    DbSet<SalesAgentPlan> SalesAgentPlans { get; }
    DbSet<SalesTarget> SalesTargets { get; }
    DbSet<SalesPnlValue> SalesPnlValues { get; }
    DbSet<SalesStaffPlan> SalesStaffPlans { get; }
    DbSet<SalesStaffIndicator> SalesStaffIndicators { get; }
    DbSet<SalesDealerLedgerEntry> SalesDealerLedger { get; }
    DbSet<SalesDealerTerm> SalesDealerTerms { get; }

    DbSet<FieldMember> FieldMembers { get; }
    DbSet<FieldTeam> FieldTeams { get; }
    DbSet<FieldCustomer> FieldCustomers { get; }
    DbSet<FieldCustomerStats> FieldCustomerStats { get; }
    DbSet<FieldRoute> FieldRoutes { get; }
    DbSet<FieldRoutePoint> FieldRoutePoints { get; }
    DbSet<FieldVisit> FieldVisits { get; }
    DbSet<FieldJointVisit> FieldJointVisits { get; }
    DbSet<FieldJointVisitParticipant> FieldJointVisitParticipants { get; }
    DbSet<FieldTask> FieldTasks { get; }
    DbSet<FieldRecommendation> FieldRecommendations { get; }
    DbSet<FieldNotification> FieldNotifications { get; }
    DbSet<FieldSettings> FieldSettings { get; }

    DbSet<OneBase.Domain.Work.WorkTask> WorkTasks { get; }
    DbSet<OneBase.Domain.Work.UserNotification> UserNotifications { get; }
    DbSet<OneBase.Domain.Work.PushSubscription> PushSubscriptions { get; }
    DbSet<OneBase.Domain.Work.PushKeys> PushKeys { get; }

    // Источники данных: привязки Google Sheets определяют владельца полей OneBase; конфликты — журнал отказов и строк с ошибками.
    DbSet<OneBase.Domain.Integrations.DataSourceConnection> DataSourceConnections { get; }
    DbSet<OneBase.Domain.Integrations.DataSourceBinding> DataSourceBindings { get; }
    DbSet<OneBase.Domain.Integrations.DataSourceConflict> DataSourceConflicts { get; }

    // Telegram: аккаунты, агент, разрешения, чаты, материалы, журнал агента, рассылки.
    DbSet<OneBase.Domain.Telegram.TelegramAccount> TelegramAccounts { get; }
    DbSet<OneBase.Domain.Telegram.TelegramAgentSettings> TelegramAgentSettings { get; }
    DbSet<OneBase.Domain.Telegram.TelegramUserPermission> TelegramUserPermissions { get; }
    DbSet<OneBase.Domain.Telegram.TelegramLinkCode> TelegramLinkCodes { get; }
    DbSet<OneBase.Domain.Telegram.TelegramChat> TelegramChats { get; }
    DbSet<OneBase.Domain.Telegram.TelegramMaterial> TelegramMaterials { get; }
    DbSet<OneBase.Domain.Telegram.TelegramMaterialChunk> TelegramMaterialChunks { get; }
    DbSet<OneBase.Domain.Telegram.TelegramIncomingMessage> TelegramIncomingMessages { get; }
    DbSet<OneBase.Domain.Telegram.TelegramAgentExecution> TelegramAgentExecutions { get; }
    DbSet<OneBase.Domain.Telegram.TelegramCampaign> TelegramCampaigns { get; }
    DbSet<OneBase.Domain.Telegram.TelegramCampaignRecipient> TelegramCampaignRecipients { get; }
    DbSet<OneBase.Domain.Telegram.TelegramCampaignAttachment> TelegramCampaignAttachments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

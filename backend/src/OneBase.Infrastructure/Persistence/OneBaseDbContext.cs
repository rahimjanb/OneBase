using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OneBase.Domain.Integrations;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Field;
using OneBase.Domain.Files;
using OneBase.Domain.Hr;
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
    public DbSet<OneBase.Domain.Telegram.TelegramAccountDepartment> TelegramAccountDepartments => Set<OneBase.Domain.Telegram.TelegramAccountDepartment>();
    public DbSet<OneBase.Domain.Telegram.TelegramConversation> TelegramConversations => Set<OneBase.Domain.Telegram.TelegramConversation>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportSettings> TelegramReportSettings => Set<OneBase.Domain.Telegram.TelegramReportSettings>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportSchedule> TelegramReportSchedules => Set<OneBase.Domain.Telegram.TelegramReportSchedule>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportSubscription> TelegramReportSubscriptions => Set<OneBase.Domain.Telegram.TelegramReportSubscription>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportSubscriptionChat> TelegramReportSubscriptionChats => Set<OneBase.Domain.Telegram.TelegramReportSubscriptionChat>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportDelivery> TelegramReportDeliveries => Set<OneBase.Domain.Telegram.TelegramReportDelivery>();
    public DbSet<OneBase.Domain.Telegram.TelegramReportJob> TelegramReportJobs => Set<OneBase.Domain.Telegram.TelegramReportJob>();
    public DbSet<OneBase.Domain.Telegram.TelegramWeeklyPdfSettings> TelegramWeeklyPdfSettings => Set<OneBase.Domain.Telegram.TelegramWeeklyPdfSettings>();
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
    public DbSet<FileLock> FileLocks => Set<FileLock>();
    public DbSet<FileUserLink> FileUserLinks => Set<FileUserLink>();

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
    public DbSet<AiDailyPlan> AiDailyPlans => Set<AiDailyPlan>();
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
    public DbSet<SalesFactoryPlan> SalesFactoryPlans => Set<SalesFactoryPlan>();
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
    public DbSet<WorkTaskChecklistItem> WorkTaskChecklist => Set<WorkTaskChecklistItem>();
    public DbSet<WorkTaskComment> WorkTaskComments => Set<WorkTaskComment>();
    public DbSet<WorkTaskAttachment> WorkTaskAttachments => Set<WorkTaskAttachment>();
    public DbSet<WorkTaskEvent> WorkTaskEvents => Set<WorkTaskEvent>();
    public DbSet<WorkProject> WorkProjects => Set<WorkProject>();
    public DbSet<WorkProjectMember> WorkProjectMembers => Set<WorkProjectMember>();
    public DbSet<WorkDepartmentHead> WorkDepartmentHeads => Set<WorkDepartmentHead>();
    public DbSet<WorkTaskSuggestion> WorkTaskSuggestions => Set<WorkTaskSuggestion>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarEventParticipant> CalendarEventParticipants => Set<CalendarEventParticipant>();
    public DbSet<OneBase.Domain.Procurement.ProcurementRequest> ProcurementRequests => Set<OneBase.Domain.Procurement.ProcurementRequest>();
    public DbSet<OneBase.Domain.Procurement.ProcurementRequestItem> ProcurementRequestItems => Set<OneBase.Domain.Procurement.ProcurementRequestItem>();
    public DbSet<OneBase.Domain.Procurement.ProcurementCategory> ProcurementCategories => Set<OneBase.Domain.Procurement.ProcurementCategory>();
    public DbSet<OneBase.Domain.Procurement.ProcurementUnit> ProcurementUnits => Set<OneBase.Domain.Procurement.ProcurementUnit>();
    public DbSet<OneBase.Domain.Procurement.ProcurementCounter> ProcurementCounters => Set<OneBase.Domain.Procurement.ProcurementCounter>();
    public DbSet<OneBase.Domain.Procurement.ProcurementAuditLog> ProcurementAuditLogs => Set<OneBase.Domain.Procurement.ProcurementAuditLog>();
    public DbSet<OneBase.Domain.Procurement.ProcurementDocument> ProcurementDocuments => Set<OneBase.Domain.Procurement.ProcurementDocument>();
    public DbSet<OneBase.Domain.Procurement.ProcurementItem> ProcurementItems => Set<OneBase.Domain.Procurement.ProcurementItem>();
    public DbSet<OneBase.Domain.Procurement.ProcurementWarehouse> ProcurementWarehouses => Set<OneBase.Domain.Procurement.ProcurementWarehouse>();
    public DbSet<OneBase.Domain.Procurement.ProcurementWarehouseKeeper> ProcurementWarehouseKeepers => Set<OneBase.Domain.Procurement.ProcurementWarehouseKeeper>();
    public DbSet<OneBase.Domain.Procurement.ProcurementBatch> ProcurementBatches => Set<OneBase.Domain.Procurement.ProcurementBatch>();
    public DbSet<OneBase.Domain.Procurement.ProcurementBatchBalance> ProcurementBatchBalances => Set<OneBase.Domain.Procurement.ProcurementBatchBalance>();
    public DbSet<OneBase.Domain.Procurement.ProcurementQualitySpec> ProcurementQualitySpecs => Set<OneBase.Domain.Procurement.ProcurementQualitySpec>();
    public DbSet<OneBase.Domain.Procurement.ProcurementQualityCheck> ProcurementQualityChecks => Set<OneBase.Domain.Procurement.ProcurementQualityCheck>();
    public DbSet<OneBase.Domain.Procurement.ProcurementQualityResult> ProcurementQualityResults => Set<OneBase.Domain.Procurement.ProcurementQualityResult>();
    public DbSet<OneBase.Domain.Procurement.ProcurementQualityApprover> ProcurementQualityApprovers => Set<OneBase.Domain.Procurement.ProcurementQualityApprover>();
    public DbSet<OneBase.Domain.Procurement.ProcurementItemUnit> ProcurementItemUnits => Set<OneBase.Domain.Procurement.ProcurementItemUnit>();
    public DbSet<OneBase.Domain.Procurement.ProcurementStockBalance> ProcurementStockBalances => Set<OneBase.Domain.Procurement.ProcurementStockBalance>();
    public DbSet<OneBase.Domain.Procurement.ProcurementStockDocument> ProcurementStockDocuments => Set<OneBase.Domain.Procurement.ProcurementStockDocument>();
    public DbSet<OneBase.Domain.Procurement.ProcurementReceiptLine> ProcurementReceiptLines => Set<OneBase.Domain.Procurement.ProcurementReceiptLine>();
    public DbSet<OneBase.Domain.Procurement.ProcurementDigest> ProcurementDigests => Set<OneBase.Domain.Procurement.ProcurementDigest>();
    public DbSet<OneBase.Domain.Procurement.ProcurementEscalation> ProcurementEscalations => Set<OneBase.Domain.Procurement.ProcurementEscalation>();
    public DbSet<OneBase.Domain.Procurement.ProcurementStockMovement> ProcurementStockMovements => Set<OneBase.Domain.Procurement.ProcurementStockMovement>();
    public DbSet<OneBase.Domain.Procurement.ProcurementStockReservation> ProcurementStockReservations => Set<OneBase.Domain.Procurement.ProcurementStockReservation>();
    public DbSet<OneBase.Domain.Procurement.ProcurementApproval> ProcurementApprovals => Set<OneBase.Domain.Procurement.ProcurementApproval>();
    public DbSet<OneBase.Domain.Procurement.ProcurementSettings> ProcurementSettings => Set<OneBase.Domain.Procurement.ProcurementSettings>();
    public DbSet<OneBase.Domain.Procurement.ProcurementSupplier> ProcurementSuppliers => Set<OneBase.Domain.Procurement.ProcurementSupplier>();
    public DbSet<OneBase.Domain.Procurement.ProcurementSupplierCategory> ProcurementSupplierCategories => Set<OneBase.Domain.Procurement.ProcurementSupplierCategory>();
    public DbSet<OneBase.Domain.Procurement.ProcurementOffer> ProcurementOffers => Set<OneBase.Domain.Procurement.ProcurementOffer>();
    public DbSet<OneBase.Domain.Procurement.ProcurementOfferItem> ProcurementOfferItems => Set<OneBase.Domain.Procurement.ProcurementOfferItem>();
    public DbSet<OneBase.Domain.Procurement.ProcurementPurchaseOrder> ProcurementPurchaseOrders => Set<OneBase.Domain.Procurement.ProcurementPurchaseOrder>();
    public DbSet<OneBase.Domain.Procurement.ProcurementPurchaseOrderItem> ProcurementPurchaseOrderItems => Set<OneBase.Domain.Procurement.ProcurementPurchaseOrderItem>();
    public DbSet<OneBase.Domain.Procurement.ProcurementTender> ProcurementTenders => Set<OneBase.Domain.Procurement.ProcurementTender>();
    public DbSet<OneBase.Domain.Procurement.ProcurementSheetImport> ProcurementSheetImports => Set<OneBase.Domain.Procurement.ProcurementSheetImport>();
    public DbSet<OneBase.Domain.Production.ProductionRecipe> ProductionRecipes => Set<OneBase.Domain.Production.ProductionRecipe>();
    public DbSet<OneBase.Domain.Production.ProductionRecipeLine> ProductionRecipeLines => Set<OneBase.Domain.Production.ProductionRecipeLine>();
    public DbSet<OneBase.Domain.Production.ProductionRecipeStage> ProductionRecipeStages => Set<OneBase.Domain.Production.ProductionRecipeStage>();
    public DbSet<OneBase.Domain.Production.ProductionDefectType> ProductionDefectTypes => Set<OneBase.Domain.Production.ProductionDefectType>();
    public DbSet<OneBase.Domain.Production.ProductionShift> ProductionShifts => Set<OneBase.Domain.Production.ProductionShift>();
    public DbSet<OneBase.Domain.Production.ProductionShiftBatch> ProductionShiftBatches => Set<OneBase.Domain.Production.ProductionShiftBatch>();
    public DbSet<OneBase.Domain.Production.ProductionShiftStage> ProductionShiftStages => Set<OneBase.Domain.Production.ProductionShiftStage>();
    public DbSet<OneBase.Domain.Production.ProductionShiftDefect> ProductionShiftDefects => Set<OneBase.Domain.Production.ProductionShiftDefect>();
    public DbSet<OneBase.Domain.Production.ProductionShiftMaterial> ProductionShiftMaterials => Set<OneBase.Domain.Production.ProductionShiftMaterial>();
    public DbSet<OneBase.Domain.Production.ProductionRecipeImport> ProductionRecipeImports => Set<OneBase.Domain.Production.ProductionRecipeImport>();
    public DbSet<OneBase.Domain.Production.ProductionRecipeImportRecipe> ProductionRecipeImportRecipes => Set<OneBase.Domain.Production.ProductionRecipeImportRecipe>();
    public DbSet<OneBase.Domain.Production.ProductionRecipeImportLine> ProductionRecipeImportLines => Set<OneBase.Domain.Production.ProductionRecipeImportLine>();
    public DbSet<OneBase.Domain.Production.ProductionRecipeImportItemMap> ProductionRecipeImportItemMaps => Set<OneBase.Domain.Production.ProductionRecipeImportItemMap>();
    public DbSet<OneBase.Domain.Production.ProductionOrder> ProductionOrders => Set<OneBase.Domain.Production.ProductionOrder>();
    public DbSet<OneBase.Domain.Production.ProductionOrderLine> ProductionOrderLines => Set<OneBase.Domain.Production.ProductionOrderLine>();
    public DbSet<OneBase.Domain.Procurement.ProcurementStockImport> ProcurementStockImports => Set<OneBase.Domain.Procurement.ProcurementStockImport>();
    public DbSet<OneBase.Domain.Procurement.ProcurementStockImportRow> ProcurementStockImportRows => Set<OneBase.Domain.Procurement.ProcurementStockImportRow>();
    public DbSet<OneBase.Domain.Procurement.ProcurementTenderItem> ProcurementTenderItems => Set<OneBase.Domain.Procurement.ProcurementTenderItem>();
    public DbSet<OneBase.Domain.Procurement.ProcurementOrderApproval> ProcurementOrderApprovals => Set<OneBase.Domain.Procurement.ProcurementOrderApproval>();
    public DbSet<OneBase.Domain.Procurement.ProcurementArrival> ProcurementArrivals => Set<OneBase.Domain.Procurement.ProcurementArrival>();
    public DbSet<OneBase.Domain.Procurement.ProcurementArrivalLine> ProcurementArrivalLines => Set<OneBase.Domain.Procurement.ProcurementArrivalLine>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<PushKeys> PushKeys => Set<PushKeys>();

    public DbSet<HrEmployee> HrEmployees => Set<HrEmployee>();
    public DbSet<HrReference> HrReferences => Set<HrReference>();
    public DbSet<HrAttendanceEvent> HrAttendanceEvents => Set<HrAttendanceEvent>();
    public DbSet<HrTimesheetDay> HrTimesheetDays => Set<HrTimesheetDay>();
    public DbSet<HrSyncRun> HrSyncRuns => Set<HrSyncRun>();
    public DbSet<HrSyncState> HrSyncStates => Set<HrSyncState>();
    public DbSet<OneBase.Domain.Battle.BattleGoal> BattleGoals => Set<OneBase.Domain.Battle.BattleGoal>();
    public DbSet<OneBase.Domain.Incentive.IncentiveProgram> IncentivePrograms => Set<OneBase.Domain.Incentive.IncentiveProgram>();
    public DbSet<OneBase.Domain.Incentive.IncentiveRouteTarget> IncentiveRouteTargets => Set<OneBase.Domain.Incentive.IncentiveRouteTarget>();
    public DbSet<OneBase.Domain.Twin.TwinObject> TwinObjects => Set<OneBase.Domain.Twin.TwinObject>();
    public DbSet<OneBase.Domain.Twin.TwinObjectLink> TwinObjectLinks => Set<OneBase.Domain.Twin.TwinObjectLink>();
    public DbSet<OneBase.Domain.Recruitment.HiringRequest> HiringRequests => Set<OneBase.Domain.Recruitment.HiringRequest>();
    public DbSet<OneBase.Domain.Recruitment.Vacancy> Vacancies => Set<OneBase.Domain.Recruitment.Vacancy>();
    public DbSet<OneBase.Domain.Recruitment.Candidate> Candidates => Set<OneBase.Domain.Recruitment.Candidate>();
    public DbSet<OneBase.Domain.Recruitment.CandidateContact> CandidateContacts => Set<OneBase.Domain.Recruitment.CandidateContact>();
    public DbSet<OneBase.Domain.Recruitment.CandidateSource> CandidateSources => Set<OneBase.Domain.Recruitment.CandidateSource>();
    public DbSet<OneBase.Domain.Recruitment.CandidateResume> CandidateResumes => Set<OneBase.Domain.Recruitment.CandidateResume>();
    public DbSet<OneBase.Domain.Recruitment.RecruitmentJob> RecruitmentJobs => Set<OneBase.Domain.Recruitment.RecruitmentJob>();
    public DbSet<OneBase.Domain.Recruitment.VacancyInterviewSettings> VacancyInterviewSettings => Set<OneBase.Domain.Recruitment.VacancyInterviewSettings>();
    public DbSet<OneBase.Domain.Recruitment.InterviewFeedback> InterviewFeedback => Set<OneBase.Domain.Recruitment.InterviewFeedback>();

    public async Task<OneBase.Application.Abstractions.IAppTransaction> LockedTransactionAsync(IReadOnlyCollection<long> keys, CancellationToken ct)
    {
        var tx = await Database.BeginTransactionAsync(ct);
        foreach (var key in keys.Distinct().Order())
        {
            await Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [key], ct);
        }

        return new AppTransaction(tx);
    }

    private sealed class AppTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) : OneBase.Application.Abstractions.IAppTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
    public DbSet<OneBase.Domain.Recruitment.CandidateApplication> CandidateApplications => Set<OneBase.Domain.Recruitment.CandidateApplication>();
    public DbSet<OneBase.Domain.Recruitment.ApplicationEvent> ApplicationEvents => Set<OneBase.Domain.Recruitment.ApplicationEvent>();
    public DbSet<OneBase.Domain.Recruitment.CandidateConsent> CandidateConsents => Set<OneBase.Domain.Recruitment.CandidateConsent>();
    public DbSet<OneBase.Domain.Recruitment.CandidateDuplicate> CandidateDuplicates => Set<OneBase.Domain.Recruitment.CandidateDuplicate>();
    public DbSet<OneBase.Domain.Recruitment.RecruitmentSettings> RecruitmentSettings => Set<OneBase.Domain.Recruitment.RecruitmentSettings>();
    public DbSet<OneBase.Domain.Recruitment.RecruitmentConsentText> RecruitmentConsentTexts => Set<OneBase.Domain.Recruitment.RecruitmentConsentText>();
    public DbSet<OneBase.Domain.Recruitment.VacancyPublication> VacancyPublications => Set<OneBase.Domain.Recruitment.VacancyPublication>();
    public DbSet<OneBase.Domain.Recruitment.RecruitmentSource> RecruitmentSources => Set<OneBase.Domain.Recruitment.RecruitmentSource>();
    public DbSet<OneBase.Domain.Recruitment.TelegramLead> TelegramLeads => Set<OneBase.Domain.Recruitment.TelegramLead>();
    public DbSet<OneBase.Domain.Telegram.TelegramConversationMessage> TelegramConversationMessages => Set<OneBase.Domain.Telegram.TelegramConversationMessage>();
    public DbSet<OneBase.Domain.Telegram.TelegramOutgoingMessage> TelegramOutgoingMessages => Set<OneBase.Domain.Telegram.TelegramOutgoingMessage>();
    public DbSet<OneBase.Domain.Recruitment.HhConnection> HhConnections => Set<OneBase.Domain.Recruitment.HhConnection>();
    public DbSet<OneBase.Domain.Recruitment.HhNegotiation> HhNegotiations => Set<OneBase.Domain.Recruitment.HhNegotiation>();
    public DbSet<OneBase.Domain.Recruitment.CandidateScore> CandidateScores => Set<OneBase.Domain.Recruitment.CandidateScore>();
    public DbSet<OneBase.Domain.Recruitment.VacancyInterviewer> VacancyInterviewers => Set<OneBase.Domain.Recruitment.VacancyInterviewer>();
    public DbSet<OneBase.Domain.Recruitment.Interview> Interviews => Set<OneBase.Domain.Recruitment.Interview>();
    public DbSet<OneBase.Domain.Recruitment.RecruitmentException> RecruitmentExceptions => Set<OneBase.Domain.Recruitment.RecruitmentException>();
    public DbSet<OneBase.Domain.AI.DialogAgent> DialogAgents => Set<OneBase.Domain.AI.DialogAgent>();
    public DbSet<OneBase.Domain.AI.DialogAgentAnswer> DialogAgentAnswers => Set<OneBase.Domain.AI.DialogAgentAnswer>();
    public DbSet<OneBase.Domain.AI.DialogAgentReply> DialogAgentReplies => Set<OneBase.Domain.AI.DialogAgentReply>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        SalesModel.ConfigureLinko(b);
        SalesModel.ConfigureSales(b);
        AiSchema.Configure(b);
        DataManagerSchema.Configure(b);
        FieldModel.Configure(b);
        WorkModel.Configure(b);
        HrModel.Configure(b);
        BattleModel.Configure(b);
        IncentiveModel.Configure(b);
        TelegramModel.Configure(b);
        ProcurementModel.Configure(b);
        ProductionModel.Configure(b);
        TwinModel.Configure(b);
        RecruitmentModel.Configure(b);
        DialogAgentModel.Configure(b);

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
            e.Property(x => x.SettingsJson).HasColumnType("jsonb");
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
            e.Property(x => x.Sections).HasColumnType("text[]");
            e.Property(x => x.Rights).HasColumnType("text[]");
            e.Property(x => x.NotificationMutes).HasColumnType("text[]");
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
            e.HasIndex(x => new { x.SharedRootId, x.ParentId });
        });

        b.Entity<FileItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(255);
            e.HasOne(x => x.Folder).WithMany(x => x.Files).HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.FolderId, x.Name });
            e.HasIndex(x => new { x.DepartmentId, x.DeletedAt });
            e.HasIndex(x => new { x.SharedRootId, x.DeletedAt });
        });

        b.Entity<FileLock>(e =>
        {
            e.Property(x => x.Token).HasMaxLength(100);
            e.Property(x => x.Owner).HasMaxLength(300);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => new { x.FileItemId, x.ExpiresAt });
            e.HasOne<FileItem>().WithMany().HasForeignKey(x => x.FileItemId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FileUserLink>(e =>
        {
            e.Property(x => x.LinkTokenHash).HasMaxLength(64);
            e.HasIndex(x => x.LinkTokenHash).IsUnique();
            // У сотрудника одна действующая личная ссылка; отозванные остаются для журнала.
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
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

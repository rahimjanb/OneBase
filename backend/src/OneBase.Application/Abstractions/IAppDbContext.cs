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
    DbSet<AiDailyPlan> AiDailyPlans { get; }
    DbSet<AiAgent> AiAgents { get; }
    DbSet<AiAgentKnowledgeSource> AiAgentKnowledgeSources { get; }
    DbSet<AiKnowledgeDocument> AiKnowledgeDocuments { get; }
    DbSet<AiKnowledgeChunk> AiKnowledgeChunks { get; }
    DbSet<AiMemory> AiMemories { get; }
    DbSet<AiAlert> AiAlerts { get; }
    DbSet<AiUsage> AiUsage { get; }
    DbSet<AiAuditLog> AiAuditLogs { get; }
    DbSet<AiModelComparison> AiModelComparisons { get; }

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
    DbSet<SalesUserScope> SalesUserScopes { get; }
    DbSet<SalesRegionPlan> SalesRegionPlans { get; }
    DbSet<SalesFactoryPlan> SalesFactoryPlans { get; }
    DbSet<SalesAgentPlan> SalesAgentPlans { get; }
    DbSet<SalesTarget> SalesTargets { get; }
    DbSet<SalesPnlValue> SalesPnlValues { get; }
    DbSet<SalesStaffPlan> SalesStaffPlans { get; }
    DbSet<SalesHoliday> SalesHolidays { get; }
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
    DbSet<OneBase.Domain.Work.WorkTaskChecklistItem> WorkTaskChecklist { get; }
    DbSet<OneBase.Domain.Work.WorkTaskComment> WorkTaskComments { get; }
    DbSet<OneBase.Domain.Work.WorkTaskAttachment> WorkTaskAttachments { get; }
    DbSet<OneBase.Domain.Work.WorkTaskEvent> WorkTaskEvents { get; }
    DbSet<OneBase.Domain.Work.WorkProject> WorkProjects { get; }
    DbSet<OneBase.Domain.Work.WorkProjectMember> WorkProjectMembers { get; }
    DbSet<OneBase.Domain.Work.WorkDepartmentHead> WorkDepartmentHeads { get; }
    DbSet<OneBase.Domain.Work.WorkTaskSuggestion> WorkTaskSuggestions { get; }
    DbSet<OneBase.Domain.Work.UserNotification> UserNotifications { get; }
    DbSet<OneBase.Domain.Work.CalendarEvent> CalendarEvents { get; }
    DbSet<OneBase.Domain.Work.CalendarEventParticipant> CalendarEventParticipants { get; }

    // Снабжение: заявки на закупку, позиции, категории, единицы, история, вложения.
    DbSet<OneBase.Domain.Procurement.ProcurementRequest> ProcurementRequests { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementRequestItem> ProcurementRequestItems { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementCategory> ProcurementCategories { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementUnit> ProcurementUnits { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementAuditLog> ProcurementAuditLogs { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementDocument> ProcurementDocuments { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementItem> ProcurementItems { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementWarehouse> ProcurementWarehouses { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementWarehouseKeeper> ProcurementWarehouseKeepers { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementBatch> ProcurementBatches { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementBatchBalance> ProcurementBatchBalances { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementQualitySpec> ProcurementQualitySpecs { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementQualityCheck> ProcurementQualityChecks { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementQualityResult> ProcurementQualityResults { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementQualityApprover> ProcurementQualityApprovers { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementItemUnit> ProcurementItemUnits { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementStockBalance> ProcurementStockBalances { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementStockDocument> ProcurementStockDocuments { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementReceiptLine> ProcurementReceiptLines { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementDigest> ProcurementDigests { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementEscalation> ProcurementEscalations { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementStockMovement> ProcurementStockMovements { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementStockReservation> ProcurementStockReservations { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementApproval> ProcurementApprovals { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementSettings> ProcurementSettings { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementSupplier> ProcurementSuppliers { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementSupplierCategory> ProcurementSupplierCategories { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementOffer> ProcurementOffers { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementOfferItem> ProcurementOfferItems { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementPurchaseOrder> ProcurementPurchaseOrders { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementPurchaseOrderItem> ProcurementPurchaseOrderItems { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementTender> ProcurementTenders { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementSheetImport> ProcurementSheetImports { get; }
    DbSet<OneBase.Domain.Production.ProductionRecipe> ProductionRecipes { get; }
    DbSet<OneBase.Domain.Production.ProductionRecipeLine> ProductionRecipeLines { get; }
    DbSet<OneBase.Domain.Production.ProductionRecipeStage> ProductionRecipeStages { get; }
    DbSet<OneBase.Domain.Production.ProductionDefectType> ProductionDefectTypes { get; }
    DbSet<OneBase.Domain.Production.ProductionShift> ProductionShifts { get; }
    DbSet<OneBase.Domain.Production.ProductionShiftBatch> ProductionShiftBatches { get; }
    DbSet<OneBase.Domain.Production.ProductionShiftStage> ProductionShiftStages { get; }
    DbSet<OneBase.Domain.Production.ProductionShiftDefect> ProductionShiftDefects { get; }
    DbSet<OneBase.Domain.Production.ProductionShiftMaterial> ProductionShiftMaterials { get; }
    DbSet<OneBase.Domain.Production.ProductionOrder> ProductionOrders { get; }
    DbSet<OneBase.Domain.Production.ProductionOrderLine> ProductionOrderLines { get; }
    DbSet<OneBase.Domain.Production.ProductionRecipeImport> ProductionRecipeImports { get; }
    DbSet<OneBase.Domain.Production.ProductionRecipeImportRecipe> ProductionRecipeImportRecipes { get; }
    DbSet<OneBase.Domain.Production.ProductionRecipeImportLine> ProductionRecipeImportLines { get; }
    DbSet<OneBase.Domain.Production.ProductionRecipeImportItemMap> ProductionRecipeImportItemMaps { get; }
    DbSet<OneBase.Domain.Production.ProductionRecipePhoto> ProductionRecipePhotos { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementStockImport> ProcurementStockImports { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementStockImportRow> ProcurementStockImportRows { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementTenderItem> ProcurementTenderItems { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementOrderApproval> ProcurementOrderApprovals { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementArrival> ProcurementArrivals { get; }
    DbSet<OneBase.Domain.Procurement.ProcurementArrivalLine> ProcurementArrivalLines { get; }
    DbSet<OneBase.Domain.Work.PushSubscription> PushSubscriptions { get; }
    DbSet<OneBase.Domain.Work.PushKeys> PushKeys { get; }

    // HR: сотрудники, справочники, отметки и табель из Verifix, журнал синхронизации.
    DbSet<OneBase.Domain.Hr.HrEmployee> HrEmployees { get; }
    DbSet<OneBase.Domain.Hr.HrReference> HrReferences { get; }
    DbSet<OneBase.Domain.Hr.HrAttendanceEvent> HrAttendanceEvents { get; }
    DbSet<OneBase.Domain.Hr.HrTimesheetDay> HrTimesheetDays { get; }
    DbSet<OneBase.Domain.Hr.HrSyncRun> HrSyncRuns { get; }
    DbSet<OneBase.Domain.Hr.HrSyncState> HrSyncStates { get; }
    DbSet<OneBase.Domain.Battle.BattleGoal> BattleGoals { get; }

    // Накопительные акции для ТП (Twizos): настройки акции и цели магазинов маршрута дня в кг.
    DbSet<OneBase.Domain.Incentive.IncentiveProgram> IncentivePrograms { get; }
    DbSet<OneBase.Domain.Incentive.IncentiveRouteTarget> IncentiveRouteTargets { get; }

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
    DbSet<OneBase.Domain.Telegram.TelegramAccountDepartment> TelegramAccountDepartments { get; }
    DbSet<OneBase.Domain.Telegram.TelegramConversation> TelegramConversations { get; }

    // Автоотчёты в Telegram: настройки, расписания, получатели, журнал отправок.
    DbSet<OneBase.Domain.Telegram.TelegramReportSettings> TelegramReportSettings { get; }
    DbSet<OneBase.Domain.Telegram.TelegramReportSchedule> TelegramReportSchedules { get; }
    DbSet<OneBase.Domain.Telegram.TelegramReportSubscription> TelegramReportSubscriptions { get; }
    DbSet<OneBase.Domain.Telegram.TelegramReportSubscriptionChat> TelegramReportSubscriptionChats { get; }
    DbSet<OneBase.Domain.Telegram.TelegramReportDelivery> TelegramReportDeliveries { get; }
    DbSet<OneBase.Domain.Telegram.TelegramReportJob> TelegramReportJobs { get; }
    DbSet<OneBase.Domain.Telegram.TelegramWeeklyPdfSettings> TelegramWeeklyPdfSettings { get; }
    DbSet<OneBase.Domain.Telegram.TelegramReportRun> TelegramReportRuns { get; }
    DbSet<OneBase.Domain.Telegram.TelegramReportDailyPlan> TelegramReportDailyPlans { get; }

    // Цифровой двойник: здания модели территории и их привязки к отделам и складам.
    DbSet<OneBase.Domain.Twin.TwinObject> TwinObjects { get; }
    DbSet<OneBase.Domain.Twin.TwinObjectLink> TwinObjectLinks { get; }

    // Подбор персонала: заявки, вакансии, кандидаты (контакты, источники, резюме, согласия, дубли), отклики и их история, настройки.
    DbSet<OneBase.Domain.Recruitment.HiringRequest> HiringRequests { get; }
    DbSet<OneBase.Domain.Recruitment.Vacancy> Vacancies { get; }
    DbSet<OneBase.Domain.Recruitment.Candidate> Candidates { get; }
    DbSet<OneBase.Domain.Recruitment.CandidateContact> CandidateContacts { get; }
    DbSet<OneBase.Domain.Recruitment.CandidateSource> CandidateSources { get; }
    DbSet<OneBase.Domain.Recruitment.CandidateResume> CandidateResumes { get; }
    DbSet<OneBase.Domain.Recruitment.RecruitmentJob> RecruitmentJobs { get; }
    DbSet<OneBase.Domain.Recruitment.VacancyInterviewSettings> VacancyInterviewSettings { get; }
    DbSet<OneBase.Domain.Recruitment.InterviewFeedback> InterviewFeedback { get; }
    DbSet<OneBase.Domain.Recruitment.DialogRead> DialogReads { get; }

    /// <summary>
    /// Транзакция с блокировками PostgreSQL на ключи (advisory, до конца транзакции, по возрастанию ключей — без взаимных блокировок): запись
    /// собеседований без двойного бронирования, даже если серверов OneBase несколько. Без CommitAsync — откат.
    /// </summary>
    Task<IAppTransaction> LockedTransactionAsync(IReadOnlyCollection<long> keys, CancellationToken ct);
    DbSet<OneBase.Domain.Recruitment.CandidateApplication> CandidateApplications { get; }
    DbSet<OneBase.Domain.Recruitment.ApplicationEvent> ApplicationEvents { get; }
    DbSet<OneBase.Domain.Recruitment.CandidateConsent> CandidateConsents { get; }
    DbSet<OneBase.Domain.Recruitment.CandidateDuplicate> CandidateDuplicates { get; }
    DbSet<OneBase.Domain.Recruitment.RecruitmentSettings> RecruitmentSettings { get; }
    DbSet<OneBase.Domain.Recruitment.RecruitmentConsentText> RecruitmentConsentTexts { get; }
    DbSet<OneBase.Domain.Recruitment.VacancyPublication> VacancyPublications { get; }
    DbSet<OneBase.Domain.Recruitment.RecruitmentSource> RecruitmentSources { get; }
    DbSet<OneBase.Domain.Recruitment.TelegramLead> TelegramLeads { get; }
    DbSet<OneBase.Domain.Telegram.TelegramConversationMessage> TelegramConversationMessages { get; }
    DbSet<OneBase.Domain.Telegram.TelegramOutgoingMessage> TelegramOutgoingMessages { get; }
    DbSet<OneBase.Domain.Recruitment.HhConnection> HhConnections { get; }
    DbSet<OneBase.Domain.Recruitment.HhNegotiation> HhNegotiations { get; }
    DbSet<OneBase.Domain.Recruitment.CandidateScore> CandidateScores { get; }
    DbSet<OneBase.Domain.Recruitment.VacancyInterviewer> VacancyInterviewers { get; }
    DbSet<OneBase.Domain.Recruitment.Interview> Interviews { get; }
    DbSet<OneBase.Domain.Recruitment.RecruitmentException> RecruitmentExceptions { get; }
    DbSet<OneBase.Domain.AI.DialogAgent> DialogAgents { get; }
    DbSet<OneBase.Domain.AI.DialogAgentAnswer> DialogAgentAnswers { get; }
    DbSet<OneBase.Domain.AI.DialogAgentReply> DialogAgentReplies { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Отслеживаемые сущности — чтобы отбросить несохранённые строки после конфликта уникального индекса (второй экземпляр службы).</summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }
}

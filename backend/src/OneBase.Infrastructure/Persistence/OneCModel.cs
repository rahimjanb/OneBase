using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneBase.Domain.OneC;

namespace OneBase.Infrastructure.Persistence;

/// <summary>
/// Схема "onec": данные 1С:Бухгалтерии (факты источника) и состояние синхронизации 1С → OneBase. Управленческие данные OneBase (планы,
/// бюджеты, прогнозы) здесь не хранятся. Ключ каждой загруженной записи — источник, организация, тип и идентификатор 1С (уникальный индекс).
/// Суммы — numeric(20,2), количество — numeric(20,3), курсы — numeric(20,6): как в 1С, без округления.
/// </summary>
internal static class OneCModel
{
    private const string Schema = "onec";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<OneCOrganization>(e =>
        {
            Record(e, "Organizations");
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.FullName).HasMaxLength(500);
            e.Property(x => x.Inn).HasMaxLength(20);
            e.Property(x => x.VatCode).HasMaxLength(30);
            e.Property(x => x.AccountingCurrency).HasMaxLength(3);
        });

        b.Entity<OneCAccount>(e =>
        {
            Record(e, "Accounts");
            e.HasIndex(x => x.Code).HasDatabaseName("IX_OneCAccounts_Code");
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.ParentCode).HasMaxLength(20);
            e.Property(x => x.Type).HasMaxLength(16);
            e.Property(x => x.Subconto).HasColumnType("jsonb");
        });

        b.Entity<OneCCounterparty>(e =>
        {
            Record(e, "Counterparties");
            e.HasIndex(x => x.Inn).HasDatabaseName("IX_OneCCounterparties_Inn");
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.FullName).HasMaxLength(500);
            e.Property(x => x.Inn).HasMaxLength(20);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.LegalForm).HasMaxLength(16);
            e.Property(x => x.ParentSourceId).HasMaxLength(64);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(100);
            e.Property(x => x.BankAccounts).HasColumnType("jsonb");
        });

        b.Entity<OneCContract>(e =>
        {
            Record(e, "Contracts");
            e.HasIndex(x => x.CounterpartySourceId).HasDatabaseName("IX_OneCContracts_Counterparty");
            e.Property(x => x.CounterpartySourceId).HasMaxLength(64);
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.Number).HasMaxLength(100);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Currency).HasMaxLength(3);
        });

        b.Entity<OneCCurrency>(e =>
        {
            Record(e, "Currencies");
            e.Property(x => x.Code).HasMaxLength(3);
            e.Property(x => x.NumericCode).HasMaxLength(3);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<OneCExchangeRate>(e =>
        {
            Record(e, "ExchangeRates");
            e.HasIndex(x => new { x.Currency, x.Date }).HasDatabaseName("IX_OneCExchangeRates_Currency_Date");
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Rate).HasPrecision(20, 6);
            e.Property(x => x.RateSource).HasMaxLength(100);
        });

        b.Entity<OneCReference>(e =>
        {
            Record(e, "References");
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.ParentSourceId).HasMaxLength(64);
            e.Property(x => x.Attributes).HasColumnType("jsonb");
        });

        b.Entity<OneCItem>(e =>
        {
            Record(e, "Items");
            e.HasIndex(x => x.Code).HasDatabaseName("IX_OneCItems_Code");
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.FullName).HasMaxLength(500);
            e.Property(x => x.Article).HasMaxLength(100);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.ParentSourceId).HasMaxLength(64);
            e.Property(x => x.VatRate).HasPrecision(5, 2);
            e.Property(x => x.Ikpu).HasMaxLength(30);
            e.Property(x => x.Barcode).HasMaxLength(50);
        });

        b.Entity<OneCDocument>(e =>
        {
            Record(e, "Documents");
            e.HasIndex(x => new { x.SourceOrganizationId, x.DocumentDate }).HasDatabaseName("IX_OneCDocuments_Organization_Date");
            e.HasIndex(x => new { x.Type, x.DocumentDate }).HasDatabaseName("IX_OneCDocuments_Type_Date");
            e.HasIndex(x => x.CounterpartySourceId).HasDatabaseName("IX_OneCDocuments_Counterparty");
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.SourceType).HasMaxLength(150);
            e.Property(x => x.Number).HasMaxLength(50);
            foreach (var id in new[] { nameof(OneCDocument.CounterpartySourceId), nameof(OneCDocument.ContractSourceId), nameof(OneCDocument.WarehouseSourceId),
                         nameof(OneCDocument.DepartmentSourceId), nameof(OneCDocument.BasisSourceId), nameof(OneCDocument.CashFlowItemSourceId),
                         nameof(OneCDocument.BankAccountSourceId), nameof(OneCDocument.CashDeskSourceId) })
            {
                e.Property(id).HasMaxLength(64);
            }

            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Amount).HasPrecision(20, 2);
            e.Property(x => x.VatAmount).HasPrecision(20, 2);
            e.Property(x => x.AmountAccounting).HasPrecision(20, 2);
            e.Property(x => x.RateValue).HasPrecision(20, 6);
            e.Property(x => x.RateSource).HasMaxLength(100);
            e.Property(x => x.Purpose).HasMaxLength(1000);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_OneCDocumentLines_OneCDocuments_DocumentId");
        });

        b.Entity<OneCDocumentLine>(e =>
        {
            e.ToTable("DocumentLines", Schema);
            e.HasKey(x => x.Id).HasName("PK_OneCDocumentLines");
            e.HasIndex(x => new { x.DocumentId, x.LineNumber }).IsUnique().HasDatabaseName("IX_OneCDocumentLines_Document_Line");
            e.HasIndex(x => x.ItemSourceId).HasDatabaseName("IX_OneCDocumentLines_Item");
            e.Property(x => x.ItemSourceId).HasMaxLength(64);
            e.Property(x => x.Quantity).HasPrecision(20, 3);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.Price).HasPrecision(20, 4);
            e.Property(x => x.Amount).HasPrecision(20, 2);
            e.Property(x => x.VatRate).HasPrecision(5, 2);
            e.Property(x => x.VatAmount).HasPrecision(20, 2);
            e.Property(x => x.CostAmount).HasPrecision(20, 2);
            e.Property(x => x.Account).HasMaxLength(20);
            e.Property(x => x.WarehouseSourceId).HasMaxLength(64);
            e.Property(x => x.BatchSourceId).HasMaxLength(64);
        });

        b.Entity<OneCEntry>(e =>
        {
            Record(e, "Entries");
            e.HasIndex(x => x.RegistrarSourceId).HasDatabaseName("IX_OneCEntries_Registrar");
            e.HasIndex(x => new { x.SourceOrganizationId, x.PeriodDate }).HasDatabaseName("IX_OneCEntries_Organization_Period");
            e.HasIndex(x => new { x.DebitAccount, x.PeriodDate }).HasDatabaseName("IX_OneCEntries_Debit_Period");
            e.HasIndex(x => new { x.CreditAccount, x.PeriodDate }).HasDatabaseName("IX_OneCEntries_Credit_Period");
            e.HasIndex(x => x.DebitCounterpartyId).HasDatabaseName("IX_OneCEntries_DebitCounterparty");
            e.HasIndex(x => x.CreditCounterpartyId).HasDatabaseName("IX_OneCEntries_CreditCounterparty");
            e.Property(x => x.RegistrarSourceId).HasMaxLength(64);
            e.Property(x => x.RegistrarType).HasMaxLength(32);
            e.Property(x => x.RegistrarSourceType).HasMaxLength(150);
            e.Property(x => x.RegistrarNumber).HasMaxLength(50);
            e.Property(x => x.Amount).HasPrecision(20, 2);
            e.Property(x => x.Content).HasMaxLength(500);
            e.Property(x => x.DepartmentSourceId).HasMaxLength(64);
            Side(e, "Debit");
            Side(e, "Credit");
            e.Property(x => x.RateValue).HasPrecision(20, 6);
            e.Property(x => x.RateSource).HasMaxLength(100);
        });

        b.Entity<OneCBalance>(e =>
        {
            Record(e, "Balances");
            e.HasIndex(x => new { x.SourceOrganizationId, x.Period, x.Account }).HasDatabaseName("IX_OneCBalances_Organization_Period_Account");
            e.HasIndex(x => x.CounterpartyId).HasDatabaseName("IX_OneCBalances_Counterparty");
            e.Property(x => x.Account).HasMaxLength(20);
            e.Property(x => x.Subconto).HasColumnType("jsonb");
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.CounterpartyId).HasMaxLength(64);
            e.Property(x => x.ItemId).HasMaxLength(64);
            e.Property(x => x.WarehouseId).HasMaxLength(64);
            foreach (var part in new[] { "Opening", "Turnover", "Closing" })
            {
                e.Property($"{part}Debit").HasPrecision(20, 2);
                e.Property($"{part}Credit").HasPrecision(20, 2);
                e.Property($"{part}CurrencyDebit").HasPrecision(20, 2);
                e.Property($"{part}CurrencyCredit").HasPrecision(20, 2);
                e.Property($"{part}QuantityDebit").HasPrecision(20, 3);
                e.Property($"{part}QuantityCredit").HasPrecision(20, 3);
            }
        });

        b.Entity<OneCChange>(e =>
        {
            e.ToTable("Changes", Schema);
            e.HasKey(x => x.Id).HasName("PK_OneCChanges");
            e.HasIndex(x => x.At).HasDatabaseName("IX_OneCChanges_At");
            e.HasIndex(x => new { x.EntityType, x.SourceEntityId }).HasDatabaseName("IX_OneCChanges_Entity");
            e.Property(x => x.BatchKey).HasMaxLength(100);
            e.Property(x => x.EntityType).HasMaxLength(48);
            e.Property(x => x.SourceOrganizationId).HasMaxLength(64);
            e.Property(x => x.SourceEntityId).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(16);
            e.Property(x => x.Changes).HasColumnType("jsonb");
            e.Property(x => x.SourceVersion).HasMaxLength(64);
        });

        b.Entity<OneCSyncRun>(e =>
        {
            e.ToTable("SyncRuns", Schema);
            e.HasKey(x => x.Id).HasName("PK_OneCSyncRuns");
            e.HasIndex(x => x.StartedAt).HasDatabaseName("IX_OneCSyncRuns_StartedAt");
            // Не больше одного идущего запуска — даже если служб интеграции запущено две.
            e.HasIndex(x => x.Status).IsUnique().HasFilter("\"Status\" = 'Running'").HasDatabaseName("IX_OneCSyncRuns_SingleRunning");
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Trigger).HasMaxLength(16);
            e.Property(x => x.Provider).HasMaxLength(16);
            e.Property(x => x.WorkerId).HasMaxLength(100);
            e.Property(x => x.InfobaseId).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Step).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.StepsJson).HasColumnType("jsonb");
        });

        b.Entity<OneCSyncCheckpoint>(e =>
        {
            e.ToTable("SyncCheckpoints", Schema);
            e.HasKey(x => new { x.Stream, x.OrganizationId }).HasName("PK_OneCSyncCheckpoints");
            e.Property(x => x.Stream).HasMaxLength(64);
            e.Property(x => x.OrganizationId).HasMaxLength(64);
            e.Property(x => x.Cursor).HasMaxLength(2000);
            e.Property(x => x.BatchKey).HasMaxLength(100);
        });

        b.Entity<OneCImportBatch>(e =>
        {
            e.ToTable("ImportBatches", Schema);
            e.HasKey(x => x.Id).HasName("PK_OneCImportBatches");
            e.HasIndex(x => x.BatchKey).IsUnique().HasDatabaseName("IX_OneCImportBatches_BatchKey");
            e.HasIndex(x => x.RunId).HasDatabaseName("IX_OneCImportBatches_RunId");
            e.Property(x => x.BatchKey).HasMaxLength(100);
            e.Property(x => x.Stream).HasMaxLength(64);
            e.Property(x => x.OrganizationId).HasMaxLength(64);
            e.Property(x => x.RejectionsJson).HasColumnType("jsonb");
        });

        b.Entity<OneCSyncFailure>(e =>
        {
            e.ToTable("SyncFailures", Schema);
            e.HasKey(x => x.Id).HasName("PK_OneCSyncFailures");
            // Одна и та же нерешённая ошибка — одна строка (повторы увеличивают Attempts).
            e.HasIndex(x => x.Fingerprint).IsUnique().HasFilter("\"Status\" IN ('Open', 'Retrying')").HasDatabaseName("IX_OneCSyncFailures_OpenFingerprint");
            e.HasIndex(x => new { x.Status, x.LastAt }).HasDatabaseName("IX_OneCSyncFailures_Status_LastAt");
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Stream).HasMaxLength(64);
            e.Property(x => x.OrganizationId).HasMaxLength(64);
            e.Property(x => x.Cursor).HasMaxLength(2000);
            e.Property(x => x.EntityType).HasMaxLength(48);
            e.Property(x => x.SourceEntityId).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(2000);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
        });

        b.Entity<OneCHistoricalImport>(e =>
        {
            e.ToTable("HistoricalImports", Schema);
            e.HasKey(x => x.Id).HasName("PK_OneCHistoricalImports");
            // Одна незавершённая историческая загрузка.
            e.HasIndex(x => x.Status).IsUnique().HasFilter("\"Status\" IN ('Pending', 'Running')").HasDatabaseName("IX_OneCHistoricalImports_SingleActive");
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.PositionStream).HasMaxLength(32);
            e.Property(x => x.PositionCursor).HasMaxLength(2000);
            e.Property(x => x.ChecksJson).HasColumnType("jsonb");
            e.Property(x => x.Message).HasMaxLength(2000);
        });

        b.Entity<OneCWorkerCommand>(e =>
        {
            e.ToTable("WorkerCommands", Schema);
            e.HasKey(x => x.Id).HasName("PK_OneCWorkerCommands");
            e.HasIndex(x => new { x.Status, x.RequestedAt }).HasDatabaseName("IX_OneCWorkerCommands_Status_RequestedAt");
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.ResultMessage).HasMaxLength(2000);
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
        });

        b.Entity<OneCWorkerState>(e =>
        {
            e.ToTable("WorkerStates", Schema);
            e.HasKey(x => x.WorkerId).HasName("PK_OneCWorkerStates");
            e.Property(x => x.WorkerId).HasMaxLength(100);
            e.Property(x => x.Version).HasMaxLength(50);
            e.Property(x => x.Provider).HasMaxLength(16);
            e.Property(x => x.BaseUrl).HasMaxLength(500);
            e.Property(x => x.ConnectionJson).HasColumnType("jsonb");
        });
    }

    /// <summary>Общие колонки записи источника и уникальный ключ источника.</summary>
    private static void Record<T>(EntityTypeBuilder<T> e, string table) where T : OneCRecord
    {
        var name = "OneC" + table;
        e.ToTable(table, Schema);
        e.HasKey(x => x.Id).HasName("PK_" + name);
        e.HasIndex(x => new { x.SourceSystem, x.SourceOrganizationId, x.SourceEntityType, x.SourceEntityId }).IsUnique()
            .HasDatabaseName($"IX_{name}_Source");
        e.Property(x => x.SourceSystem).HasMaxLength(8);
        e.Property(x => x.SourceOrganizationId).HasMaxLength(64);
        e.Property(x => x.SourceEntityType).HasMaxLength(48);
        e.Property(x => x.SourceEntityId).HasMaxLength(100);
        e.Property(x => x.SourceVersion).HasMaxLength(64);
        e.Property(x => x.ContentHash).HasMaxLength(64);
    }

    private static void Side(EntityTypeBuilder<OneCEntry> e, string side)
    {
        e.Property($"{side}Account").HasMaxLength(20);
        e.Property($"{side}Subconto").HasColumnType("jsonb");
        e.Property($"{side}Currency").HasMaxLength(3);
        e.Property($"{side}CurrencyAmount").HasPrecision(20, 2);
        e.Property($"{side}Quantity").HasPrecision(20, 3);
        e.Property($"{side}CounterpartyId").HasMaxLength(64);
        e.Property($"{side}ItemId").HasMaxLength(64);
        e.Property($"{side}WarehouseId").HasMaxLength(64);
    }
}

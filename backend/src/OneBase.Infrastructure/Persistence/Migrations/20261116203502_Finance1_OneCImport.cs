using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Finance1_OneCImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "onec");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ParentCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OffBalance = table.Column<bool>(type: "boolean", nullable: false),
                    IsCurrency = table.Column<bool>(type: "boolean", nullable: false),
                    IsQuantity = table.Column<bool>(type: "boolean", nullable: false),
                    Subconto = table.Column<string>(type: "jsonb", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Balances",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    Account = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Subconto = table.Column<string>(type: "jsonb", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    CounterpartyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WarehouseId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningDebit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    OpeningCredit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    OpeningCurrencyDebit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    OpeningCurrencyCredit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    OpeningQuantityDebit = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    OpeningQuantityCredit = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    TurnoverDebit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    TurnoverCredit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    TurnoverCurrencyDebit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    TurnoverCurrencyCredit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    TurnoverQuantityDebit = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    TurnoverQuantityCredit = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    ClosingDebit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    ClosingCredit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    ClosingCurrencyDebit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    ClosingCurrencyCredit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    ClosingQuantityDebit = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    ClosingQuantityCredit = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Changes",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartySourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentTermDays = table.Column<int>(type: "integer", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCContracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Counterparties",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FullName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Inn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LegalForm = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ParentSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankAccounts = table.Column<string>(type: "jsonb", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCCounterparties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NumericCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCCurrencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Posted = table.Column<bool>(type: "boolean", nullable: false),
                    CounterpartySourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContractSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WarehouseSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DepartmentSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    AmountAccounting = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    RateValue = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    RateMultiplier = table.Column<int>(type: "integer", nullable: true),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RateSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BasisSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CashFlowItemSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BankAccountSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CashDeskSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entries",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrarSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RegistrarType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RegistrarSourceType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RegistrarNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RegistrarDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    Content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DepartmentSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DebitAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DebitSubconto = table.Column<string>(type: "jsonb", nullable: false),
                    DebitCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    DebitCurrencyAmount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    DebitQuantity = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    DebitCounterpartyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DebitItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DebitWarehouseId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreditAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreditSubconto = table.Column<string>(type: "jsonb", nullable: false),
                    CreditCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    CreditCurrencyAmount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    CreditQuantity = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: true),
                    CreditCounterpartyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreditItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreditWarehouseId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RateValue = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    RateMultiplier = table.Column<int>(type: "integer", nullable: true),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RateSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Multiplier = table.Column<int>(type: "integer", nullable: false),
                    RateSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalImports",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    To = table.Column<DateOnly>(type: "date", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReferencesDone = table.Column<bool>(type: "boolean", nullable: false),
                    PositionPeriod = table.Column<DateOnly>(type: "date", nullable: true),
                    PositionOrganization = table.Column<int>(type: "integer", nullable: false),
                    PositionStream = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PositionCursor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PeriodsTotal = table.Column<int>(type: "integer", nullable: false),
                    PeriodsDone = table.Column<int>(type: "integer", nullable: false),
                    Records = table.Column<long>(type: "bigint", nullable: false),
                    ChecksJson = table.Column<string>(type: "jsonb", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCHistoricalImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stream = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Records = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Unchanged = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<int>(type: "integer", nullable: false),
                    Rejected = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    RejectionsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FullName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Article = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ParentSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Ikpu = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FullName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Inn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VatCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AccountingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "References",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ParentSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    Attributes = table.Column<string>(type: "jsonb", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceOrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncCheckpoints",
                schema: "onec",
                columns: table => new
                {
                    Stream = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Rows = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCSyncCheckpoints", x => new { x.Stream, x.OrganizationId });
                });

            migrationBuilder.CreateTable(
                name: "SyncFailures",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Stream = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Period = table.Column<DateOnly>(type: "date", nullable: true),
                    Cursor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    SourceEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCSyncFailures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRuns",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    HistoricalImportId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetryOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InfobaseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Step = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Created = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Unchanged = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<int>(type: "integer", nullable: false),
                    Rejected = table.Column<int>(type: "integer", nullable: false),
                    Batches = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StepsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerCommands",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TakenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCWorkerCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerStates",
                schema: "onec",
                columns: table => new
                {
                    WorkerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Busy = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConnectionJson = table.Column<string>(type: "jsonb", nullable: true),
                    ConnectionCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCWorkerStates", x => x.WorkerId);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLines",
                schema: "onec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(20,3)", precision: 20, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    VatAmount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    CostAmount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    Account = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WarehouseSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BatchSourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneCDocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OneCDocumentLines_OneCDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "onec",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OneCAccounts_Code",
                schema: "onec",
                table: "Accounts",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_OneCAccounts_Source",
                schema: "onec",
                table: "Accounts",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCBalances_Counterparty",
                schema: "onec",
                table: "Balances",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_OneCBalances_Organization_Period_Account",
                schema: "onec",
                table: "Balances",
                columns: new[] { "SourceOrganizationId", "Period", "Account" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCBalances_Source",
                schema: "onec",
                table: "Balances",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCChanges_At",
                schema: "onec",
                table: "Changes",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_OneCChanges_Entity",
                schema: "onec",
                table: "Changes",
                columns: new[] { "EntityType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCContracts_Counterparty",
                schema: "onec",
                table: "Contracts",
                column: "CounterpartySourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OneCContracts_Source",
                schema: "onec",
                table: "Contracts",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCCounterparties_Inn",
                schema: "onec",
                table: "Counterparties",
                column: "Inn");

            migrationBuilder.CreateIndex(
                name: "IX_OneCCounterparties_Source",
                schema: "onec",
                table: "Counterparties",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCCurrencies_Source",
                schema: "onec",
                table: "Currencies",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCDocumentLines_Document_Line",
                schema: "onec",
                table: "DocumentLines",
                columns: new[] { "DocumentId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCDocumentLines_Item",
                schema: "onec",
                table: "DocumentLines",
                column: "ItemSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OneCDocuments_Counterparty",
                schema: "onec",
                table: "Documents",
                column: "CounterpartySourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OneCDocuments_Organization_Date",
                schema: "onec",
                table: "Documents",
                columns: new[] { "SourceOrganizationId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCDocuments_Source",
                schema: "onec",
                table: "Documents",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCDocuments_Type_Date",
                schema: "onec",
                table: "Documents",
                columns: new[] { "Type", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCEntries_Credit_Period",
                schema: "onec",
                table: "Entries",
                columns: new[] { "CreditAccount", "PeriodDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCEntries_CreditCounterparty",
                schema: "onec",
                table: "Entries",
                column: "CreditCounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_OneCEntries_Debit_Period",
                schema: "onec",
                table: "Entries",
                columns: new[] { "DebitAccount", "PeriodDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCEntries_DebitCounterparty",
                schema: "onec",
                table: "Entries",
                column: "DebitCounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_OneCEntries_Organization_Period",
                schema: "onec",
                table: "Entries",
                columns: new[] { "SourceOrganizationId", "PeriodDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCEntries_Registrar",
                schema: "onec",
                table: "Entries",
                column: "RegistrarSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OneCEntries_Source",
                schema: "onec",
                table: "Entries",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCExchangeRates_Currency_Date",
                schema: "onec",
                table: "ExchangeRates",
                columns: new[] { "Currency", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCExchangeRates_Source",
                schema: "onec",
                table: "ExchangeRates",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCHistoricalImports_SingleActive",
                schema: "onec",
                table: "HistoricalImports",
                column: "Status",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_OneCImportBatches_BatchKey",
                schema: "onec",
                table: "ImportBatches",
                column: "BatchKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCImportBatches_RunId",
                schema: "onec",
                table: "ImportBatches",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_OneCItems_Code",
                schema: "onec",
                table: "Items",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_OneCItems_Source",
                schema: "onec",
                table: "Items",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCOrganizations_Source",
                schema: "onec",
                table: "Organizations",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCReferences_Source",
                schema: "onec",
                table: "References",
                columns: new[] { "SourceSystem", "SourceOrganizationId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneCSyncFailures_OpenFingerprint",
                schema: "onec",
                table: "SyncFailures",
                column: "Fingerprint",
                unique: true,
                filter: "\"Status\" IN ('Open', 'Retrying')");

            migrationBuilder.CreateIndex(
                name: "IX_OneCSyncFailures_Status_LastAt",
                schema: "onec",
                table: "SyncFailures",
                columns: new[] { "Status", "LastAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OneCSyncRuns_SingleRunning",
                schema: "onec",
                table: "SyncRuns",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_OneCSyncRuns_StartedAt",
                schema: "onec",
                table: "SyncRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OneCWorkerCommands_Status_RequestedAt",
                schema: "onec",
                table: "WorkerCommands",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Balances",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Changes",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Contracts",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Counterparties",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "DocumentLines",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Entries",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "ExchangeRates",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "HistoricalImports",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "ImportBatches",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "References",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "SyncCheckpoints",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "SyncFailures",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "SyncRuns",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "WorkerCommands",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "WorkerStates",
                schema: "onec");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "onec");
        }
    }
}

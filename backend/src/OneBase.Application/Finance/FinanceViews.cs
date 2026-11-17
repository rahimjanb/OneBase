namespace OneBase.Application.Finance;

// Ответы финансовых отчётов (раздел «Финансы» и AI). В каждом — FinanceMeta: откуда цифры, за какой период, когда синхронизированы.
// Все суммы — в валюте учёта (UZS) из 1С, без пересчёта по текущему курсу; валютные суммы — отдельными полями с кодом валюты.

/// <summary>Отбор отчёта: организация 1С (null — все организации, сводно) и период (даты по Ташкенту, включительно).</summary>
public sealed record FinanceFilter(string? OrganizationId, DateOnly From, DateOnly To)
{
    /// <summary>Предыдущий период той же длины — для сравнения.</summary>
    public FinanceFilter Previous()
    {
        var days = To.DayNumber - From.DayNumber;
        var to = From.AddDays(-1);
        // Целые месяцы сравниваются с целыми месяцами (октябрь 1–31 → сентябрь 1–30).
        if (From.Day == 1 && To.AddDays(1).Day == 1)
        {
            var months = (To.Year - From.Year) * 12 + To.Month - From.Month + 1;
            var start = From.AddMonths(-months);
            return this with { From = start, To = to };
        }

        // Месяц к дате (1–17 ноября) — с теми же днями прошлого месяца (1–17 октября): начисления конца месяца не искажают сравнение.
        if (From.Day == 1 && From.Year == To.Year && From.Month == To.Month)
        {
            return this with { From = From.AddMonths(-1), To = To.AddMonths(-1) };
        }

        return this with { From = to.AddDays(-days), To = to };
    }
}

/// <summary>Происхождение цифр: всегда «факт 1С», с временем синхронизации и последней датой данных.</summary>
public sealed record FinanceMeta(
    string Source, string Kind, bool Demo, DateTimeOffset? SyncedAt, string? Infobase, string? OrganizationId, string? OrganizationName,
    DateOnly From, DateOnly To, DateOnly? DataThrough, bool HasData, string? Note);

public sealed record FinanceOrganization(string Id, string Name, string? Inn, string Currency);

public sealed record PnlValue(string Code, string Title, string Kind, decimal Value, decimal? Previous);

public sealed record MonthPoint(DateOnly Month, decimal Revenue, decimal Expenses, decimal NetProfit);

public sealed record AmountRow(string Key, string Title, decimal Amount, decimal? Previous = null, decimal? Quantity = null, string? Currency = null, decimal? CurrencyAmount = null);

public sealed record FinanceOverview(
    FinanceMeta Meta, IReadOnlyList<PnlValue> Pnl, IReadOnlyList<MonthPoint> Months, decimal CashTotal, IReadOnlyList<AmountRow> Cash,
    decimal Receivables, decimal Payables, decimal AdvancesGiven, decimal AdvancesReceived, IReadOnlyList<AmountRow> Inventory,
    DateOnly? BalancesMonth, IReadOnlyList<AmountRow> ExpenseGrowth);

public sealed record PnlLineView(string Code, string Title, string Kind, IReadOnlyList<decimal> Months, decimal Total, decimal? Previous);

public sealed record FinancePnl(
    FinanceMeta Meta, IReadOnlyList<DateOnly> Months, IReadOnlyList<PnlLineView> Lines, IReadOnlyList<AmountRow> ExpensesByAccount,
    IReadOnlyList<AmountRow> ExpensesByDepartment);

public sealed record CashFlowRow(string Code, string Title, bool Inflow, IReadOnlyList<decimal> Months, decimal Total);

public sealed record FinanceCash(
    FinanceMeta Meta, IReadOnlyList<DateOnly> Months, IReadOnlyList<CashFlowRow> Flows, decimal Inflow, decimal Outflow,
    IReadOnlyList<AmountRow> Balances, DateOnly? BalancesMonth);

public sealed record SettlementRow(
    string CounterpartyId, string Name, string? Inn, decimal Balance, decimal Overdue, DateOnly? OldestDue, int? MaxOverdueDays,
    IReadOnlyDictionary<string, decimal> Buckets, bool TermsKnown);

public sealed record FinanceSettlements(
    FinanceMeta Meta, string Kind, DateOnly? BalancesMonth, decimal Total, decimal Overdue, IReadOnlyDictionary<string, decimal> Buckets,
    decimal Advances, IReadOnlyList<SettlementRow> Rows, string Method);

public sealed record TrialRow(
    string Account, string Name, decimal OpeningDebit, decimal OpeningCredit, decimal TurnoverDebit, decimal TurnoverCredit,
    decimal ClosingDebit, decimal ClosingCredit);

public sealed record FinanceTrialBalance(FinanceMeta Meta, IReadOnlyList<TrialRow> Rows, TrialRow Total, string Method);

public sealed record SubcontoBalance(string Kind, string? Id, string Name, decimal Closing, decimal? Quantity, string? Currency, decimal? CurrencyAmount);

public sealed record FinanceAccount(
    FinanceMeta Meta, string Account, string Name, string Type, IReadOnlyList<string> Subconto, TrialRow? Totals,
    IReadOnlyList<SubcontoBalance> Breakdown, IReadOnlyList<MonthTurnover> Monthly);

public sealed record MonthTurnover(DateOnly Month, decimal Debit, decimal Credit);

public sealed record EntryView(
    Guid Id, DateOnly Date, string DebitAccount, string? DebitName, string CreditAccount, string? CreditName, decimal Amount, string? Content,
    string? Counterparty, string? Currency, decimal? CurrencyAmount, decimal? Quantity, Guid? DocumentId, string RegistrarType, string? RegistrarNumber,
    string? RegistrarSourceType, IReadOnlyList<SubcontoValue> Subconto);

public sealed record SubcontoValue(string Side, int Index, string Type, string? Name);

public sealed record FinanceEntries(FinanceMeta Meta, IReadOnlyList<EntryView> Items, int Total, int Page, int PageSize, decimal Amount);

public sealed record DocumentLineView(int Line, string? Item, decimal Quantity, string? Unit, decimal? Price, decimal Amount, decimal? VatAmount, decimal? CostAmount, string? Account);

public sealed record FinanceDocument(
    Guid Id, string Type, string SourceType, string Number, DateOnly Date, bool Posted, bool Deleted, string? Organization, string? Counterparty,
    string? Contract, string? Warehouse, string Currency, decimal Amount, decimal? VatAmount, decimal AmountAccounting, decimal? Rate, int? RateMultiplier,
    DateOnly? RateDate, string? RateSource, DateOnly? DueDate, string? Purpose, string? Comment, IReadOnlyList<DocumentLineView> Lines,
    IReadOnlyList<EntryView> Entries, string? Version, DateTimeOffset ImportedAt, DateTimeOffset UpdatedAt, int Changes);

public sealed record FinanceOperations(
    FinanceMeta Meta, IReadOnlyList<DateOnly> Months, IReadOnlyList<decimal> Sales, IReadOnlyList<decimal> Purchases, IReadOnlyList<decimal> CostOfSales,
    IReadOnlyList<decimal> ProductionCosts, IReadOnlyList<AmountRow> TopCustomers, IReadOnlyList<AmountRow> TopSuppliers, IReadOnlyList<AmountRow> TopItems,
    IReadOnlyList<AmountRow> SalesByCurrency, IReadOnlyList<AmountRow> Production, IReadOnlyList<AmountRow> InventoryByWarehouse,
    IReadOnlyList<AmountRow> Inventory, IReadOnlyList<UnitCostRow> UnitCosts, IReadOnlyList<RateRow> Rates, DateOnly? BalancesMonth);

public sealed record UnitCostRow(string ItemId, string Item, string? Unit, IReadOnlyList<decimal?> Months);

public sealed record RateRow(string Currency, decimal? First, decimal? Last, DateOnly? LastDate, decimal? ChangePercent, string? Source);

public sealed record Anomaly(string Kind, string Title, string Detail, decimal? Amount, DateOnly? Date, string? Link, int Severity);

public sealed record FinanceAnomalies(FinanceMeta Meta, IReadOnlyList<Anomaly> Items, IReadOnlyList<string> Checks);

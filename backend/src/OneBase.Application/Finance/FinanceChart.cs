namespace OneBase.Application.Finance;

/// <summary>
/// Правила плана счетов бухгалтерского учёта Узбекистана (НСБУ №21) для отчётов OneBase по данным 1С. Счета — 4-значные, группы — по первым
/// цифрам. Отчёты считаются по проводкам 1С; закрытие счетов на финансовый результат (корреспонденция с 99xx) и переносы внутри счетов
/// доходов и расходов (9xxx ↔ 9xxx) в отчёты не входят — это не хозяйственные операции.
/// </summary>
public static class FinanceChart
{
    // Денежные средства: касса, расчётный и валютные счета, специальные счета, эквиваленты, переводы в пути.
    public static readonly string[] Cash = ["50", "51", "52", "55", "56", "57"];

    // Расчёты.
    public static readonly string[] Receivables = ["40"];
    public static readonly string[] AdvancesGiven = ["43"];
    public static readonly string[] Payables = ["60"];
    public static readonly string[] AdvancesReceived = ["63"];

    // Запасы: материалы, основное производство (НЗП), готовая продукция, товары.
    public static readonly string[] Materials = ["10"];
    public static readonly string[] WorkInProgress = ["20", "21", "23"];
    public static readonly string[] FinishedGoods = ["28"];
    public static readonly string[] Goods = ["29"];
    public static readonly string[] Inventory = [.. Materials, .. WorkInProgress, .. FinishedGoods, .. Goods];

    // Доходы и расходы.
    public static readonly string[] Revenue = ["901", "902", "903"];
    public static readonly string[] RevenueReductions = ["904", "905"];
    public static readonly string[] CostOfSales = ["91"];
    public static readonly string[] Selling = ["941"];
    public static readonly string[] Administrative = ["942"];
    public static readonly string[] OtherOperating = ["943"];
    public static readonly string[] FutureDeductible = ["944"];
    public static readonly string[] PeriodExpenses = ["94"];
    public static readonly string[] OtherIncome = ["93"];
    public static readonly string[] FinancialIncome = ["95"];
    public static readonly string[] FinancialExpense = ["96"];
    public static readonly string[] Extraordinary = ["97"];
    public static readonly string[] IncomeTax = ["981"];
    public static readonly string[] OtherTaxes = ["982"];
    public static readonly string[] Expenses = ["91", "94", "96", "98"];

    /// <summary>Курсовые разницы: доходы 9540 и убытки 9620 — в движении денег не участвуют.</summary>
    public static readonly string[] ExchangeDifferences = ["954", "962"];

    public static bool Is(string? account, string[] prefixes) => account is not null && prefixes.Any(account.StartsWith);

    /// <summary>Закрытие на финансовый результат или перенос внутри доходов и расходов — не хозяйственная операция.</summary>
    public static bool IsClosing(string debit, string credit) =>
        debit.StartsWith("99", StringComparison.Ordinal) || credit.StartsWith("99", StringComparison.Ordinal)
        || (debit.StartsWith('9') && credit.StartsWith('9'));

    /// <summary>Строки отчёта о финансовых результатах (форма №2): код, название, вид (доход, расход, итог).</summary>
    public static readonly IReadOnlyList<(string Code, string Title, string Kind)> PnlLines =
    [
        ("revenue", "Выручка от реализации", "income"),
        ("reductions", "Возвраты и скидки", "expense"),
        ("net_revenue", "Чистая выручка", "total"),
        ("cost_of_sales", "Себестоимость реализованной продукции", "expense"),
        ("gross_profit", "Валовая прибыль", "total"),
        ("selling", "Расходы по реализации", "expense"),
        ("administrative", "Административные расходы", "expense"),
        ("other_operating", "Прочие операционные расходы", "expense"),
        ("future_deductible", "Расходы, вычитаемые в будущем", "expense"),
        ("other_income", "Прочие доходы от основной деятельности", "income"),
        ("operating_profit", "Прибыль от основной деятельности", "total"),
        ("financial_income", "Доходы от финансовой деятельности", "income"),
        ("financial_expense", "Расходы по финансовой деятельности", "expense"),
        ("extraordinary", "Чрезвычайные прибыли и убытки", "income"),
        ("profit_before_tax", "Прибыль до уплаты налога", "total"),
        ("income_tax", "Налог на прибыль", "expense"),
        ("other_taxes", "Прочие налоги и сборы из прибыли", "expense"),
        ("net_profit", "Чистая прибыль", "total"),
    ];

    /// <summary>Обороты по паре счетов (дебет, кредит) за месяц — из этого строится отчёт о финансовых результатах.</summary>
    public sealed record Turnover(string Debit, string Credit, DateOnly Month, decimal Amount);

    /// <summary>Строки отчёта за один набор оборотов. Доходы и расходы — положительные числа, итоги — со знаком.</summary>
    public static Dictionary<string, decimal> Pnl(IEnumerable<Turnover> turnovers)
    {
        decimal Dt(string[] p, IEnumerable<Turnover> t) => t.Where(x => Is(x.Debit, p)).Sum(x => x.Amount) - t.Where(x => Is(x.Credit, p)).Sum(x => x.Amount);
        decimal Kt(string[] p, IEnumerable<Turnover> t) => -Dt(p, t);

        var rows = turnovers.Where(t => !IsClosing(t.Debit, t.Credit)).ToList();
        var revenue = Kt(Revenue, rows);
        var reductions = Dt(RevenueReductions, rows);
        var net = revenue - reductions;
        var cost = Dt(CostOfSales, rows);
        var gross = net - cost;
        var selling = Dt(Selling, rows);
        var admin = Dt(Administrative, rows);
        var otherOperating = Dt(OtherOperating, rows);
        var future = Dt(FutureDeductible, rows);
        var otherIncome = Kt(OtherIncome, rows);
        var operating = gross - selling - admin - otherOperating - future + otherIncome;
        var finIncome = Kt(FinancialIncome, rows);
        var finExpense = Dt(FinancialExpense, rows);
        var extraordinary = Kt(Extraordinary, rows);
        var beforeTax = operating + finIncome - finExpense + extraordinary;
        var tax = Dt(IncomeTax, rows);
        var otherTaxes = Dt(OtherTaxes, rows);
        return new Dictionary<string, decimal>
        {
            ["revenue"] = revenue, ["reductions"] = reductions, ["net_revenue"] = net, ["cost_of_sales"] = cost, ["gross_profit"] = gross,
            ["selling"] = selling, ["administrative"] = admin, ["other_operating"] = otherOperating, ["future_deductible"] = future,
            ["other_income"] = otherIncome, ["operating_profit"] = operating, ["financial_income"] = finIncome, ["financial_expense"] = finExpense,
            ["extraordinary"] = extraordinary, ["profit_before_tax"] = beforeTax, ["income_tax"] = tax, ["other_taxes"] = otherTaxes,
            ["net_profit"] = beforeTax - tax - otherTaxes,
        };
    }

    /// <summary>Статья движения денег по корреспондирующему счёту (поступление или выбытие).</summary>
    public static (string Code, string Title) CashCategory(string counterAccount, bool inflow) => counterAccount switch
    {
        _ when counterAccount.StartsWith("40") || counterAccount.StartsWith("63") => inflow ? ("customers", "От покупателей") : ("customer_refunds", "Возвраты покупателям"),
        _ when counterAccount.StartsWith("60") || counterAccount.StartsWith("43") => inflow ? ("supplier_refunds", "Возвраты от поставщиков") : ("suppliers", "Поставщикам и подрядчикам"),
        _ when counterAccount.StartsWith("64") => ("taxes", "Налоги и сборы"),
        _ when counterAccount.StartsWith("65") => ("social", "Страхование и социальные платежи"),
        _ when counterAccount.StartsWith("67") => ("payroll", "Оплата труда"),
        _ when counterAccount.StartsWith("66") => ("dividends", "Дивиденды"),
        _ when counterAccount.StartsWith("68") || counterAccount.StartsWith("78") || counterAccount.StartsWith("69") => ("loans", "Кредиты и займы"),
        _ when counterAccount.StartsWith("42") || counterAccount.StartsWith("47") || counterAccount.StartsWith("48") => ("other_settlements", "Прочие расчёты (подотчёт, персонал, прочие дебиторы)"),
        _ when counterAccount.StartsWith("01") || counterAccount.StartsWith("08") || counterAccount.StartsWith("04") => ("investments", "Основные средства и вложения"),
        _ when counterAccount.StartsWith("06") || counterAccount.StartsWith("58") => ("financial_investments", "Финансовые вложения"),
        _ when counterAccount.StartsWith("94") || counterAccount.StartsWith("91") || counterAccount.StartsWith("96") => ("expenses", "Расходы напрямую"),
        _ when counterAccount.StartsWith("9") => ("other_income", "Прочие доходы"),
        _ => ("other", "Прочее"),
    };

    /// <summary>Название группы запасов по счёту.</summary>
    public static string InventoryGroup(string account) => account switch
    {
        _ when Is(account, Materials) => "Материалы",
        _ when Is(account, WorkInProgress) => "Незавершённое производство",
        _ when Is(account, FinishedGoods) => "Готовая продукция",
        _ when Is(account, Goods) => "Товары",
        _ => "Прочее",
    };
}

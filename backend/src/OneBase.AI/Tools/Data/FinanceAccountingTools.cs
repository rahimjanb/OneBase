using System.Globalization;
using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Finance;
using OneBase.Application.Security;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// Инструменты «Бухгалтерия 1С» для AI-консультанта и аналитиков: только чтение фактов 1С через FinanceReportService. Доступ — право «Финансы»
/// и включённый администратором доступ AI к данным 1С (данные уходят во внешнюю модель). В каждом ответе — источник (факт 1С или демо),
/// период, отбор, время синхронизации и ссылка на раздел «Финансы» (открывается только с правом «Финансы»). Субконто сотрудников и физлиц
/// модель не получает (скрыты отчётами).
/// </summary>
internal abstract class FinanceToolBase(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : DataTool
{
    protected FinanceReportService Reports { get; } = reports;

    public override string Source => KnowledgeSources.FinanceAccounting;
    public override string RequiredPermission => Permissions.FinanceRead;

    public override string UsageHint =>
        "Бухгалтерия 1С (finance_*): факт компании — P&L, расходы, деньги, долги, счета, проводки, себестоимость, запасы. Суммы — сумы (UZS) из 1С без пересчёта; " +
        "называй источник «1С:Бухгалтерия» и дату синхронизации из ответа, не смешивай с данными Linko без оговорки. Демо-данные (meta.demo) — так и скажи.";

    protected static readonly (string, object) FromArg = ("from", new { type = "string", description = "Начало периода ГГГГ-ММ-ДД. Без него — по году и месяцу, иначе текущий месяц." });
    protected static readonly (string, object) ToArg = ("to", new { type = "string", description = "Конец периода ГГГГ-ММ-ДД включительно." });
    protected static readonly (string, object) OrgArg = ("organization", new { type = "string", description = "Организация 1С: название или id. Без неё — все организации сводно." });

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        if (!(await settings.GetAsync(ct)).AiEnabled)
        {
            return ToolResult.Error("Доступ AI к данным 1С выключен администратором (Настройки → Интеграции → Финансы → 1С). Скажи об этом пользователю; цифры 1С не называй.");
        }

        var filter = await FilterAsync(args, ct);
        return await RunAsync(filter, args, ct);
    }

    protected abstract Task<ToolResult> RunAsync(FinanceFilter filter, ToolArgs args, CancellationToken ct);

    /// <summary>Период: from/to, или year/month, или текущий месяц по Ташкенту; организация — по названию или id.</summary>
    protected async Task<FinanceFilter> FilterAsync(ToolArgs args, CancellationToken ct)
    {
        var today = OneCFormat.LocalDate(clock.GetUtcNow());
        DateOnly from, to;
        if (Date(args.Str("from")) is { } f)
        {
            from = f;
            to = Date(args.Str("to")) ?? today;
        }
        else
        {
            var (year, month) = args.Period();
            if (year is { } y && month is { } m)
            {
                from = new DateOnly(y, m, 1);
                to = from.AddMonths(1).AddDays(-1);
            }
            else if (year is { } yearOnly)
            {
                from = new DateOnly(yearOnly, 1, 1);
                to = new DateOnly(yearOnly, 12, 31);
            }
            else
            {
                from = new DateOnly(today.Year, today.Month, 1);
                to = today;
            }
        }

        if (to > today)
        {
            to = today;
        }

        if (from > to)
        {
            throw new ToolArgumentException("Начало периода позже конца.");
        }

        string? org = null;
        if (args.Str("organization") is { Length: > 0 } wanted)
        {
            var orgs = await Reports.OrganizationsAsync(ct);
            var match = orgs.FirstOrDefault(o => o.Id == wanted) ?? orgs.FirstOrDefault(o => o.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase));
            org = match?.Id ?? throw new ToolArgumentException($"Организации «{wanted}» в 1С нет. Есть: {string.Join(", ", orgs.Select(o => o.Name))}.");
        }

        return new FinanceFilter(org, from, to);
    }

    private static DateOnly? Date(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    protected static string PeriodText(FinanceFilter f) => $"{f.From:dd.MM.yyyy}–{f.To:dd.MM.yyyy}";

    /// <summary>Ссылка на раздел «Финансы» с тем же отбором — «перейти к исходным данным».</summary>
    protected static string Link(string path, FinanceFilter f, params (string Key, string? Value)[] extra)
    {
        var parts = new List<string> { $"from={f.From:yyyy-MM-dd}", $"to={f.To:yyyy-MM-dd}" };
        if (f.OrganizationId is { } org)
        {
            parts.Add("organization=" + Uri.EscapeDataString(org));
        }

        parts.AddRange(extra.Where(e => e.Value is not null).Select(e => $"{e.Key}={Uri.EscapeDataString(e.Value!)}"));
        return $"{path}?{string.Join("&", parts)}";
    }

    /// <summary>Происхождение: источник, демо, синхронизация, данные по, период, организация, отбор, ссылка.</summary>
    protected static object Meta(FinanceMeta m, string link, object? filters = null) => new
    {
        Source = m.Source,
        Kind = "Факт 1С:Бухгалтерии (не план, не прогноз и не данные Linko)",
        Demo = m.Demo,
        SyncedAt = m.SyncedAt?.ToOffset(OneCFormat.Offset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DataThrough = m.DataThrough?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Period = new { From = m.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), To = m.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
        Organization = m.OrganizationName ?? "все организации",
        Filters = filters,
        Currency = "UZS (валюта учёта 1С)",
        Link = link,
        m.Note,
    };

    protected static ToolResult NoData(FinanceMeta m) =>
        Data(new { Note = m.Note ?? "Данных 1С за период нет.", Meta = Meta(m, "/finance") });

    protected static DataSource Src(string section, FinanceFilter f, string link) => new($"1С:Бухгалтерия → Финансы: {section}", PeriodText(f), link);

    protected static decimal? ChangePct(decimal now, decimal? before) => before is null or 0 ? null : Math.Round((now - before.Value) / Math.Abs(before.Value) * 100, 1);
}

internal sealed class FinanceOverviewTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_overview";
    public override string Title => "Финансы 1С: обзор";

    public override string Description =>
        "Финансовый обзор компании по 1С за период: выручка, себестоимость, валовая и чистая прибыль (с прошлым периодом той же длины), деньги на счетах и в " +
        "кассе, дебиторская и кредиторская задолженность, запасы, помесячно 12 месяцев, где выросли расходы. Для вопросов «какова выручка/прибыль», " +
        "«сколько денег», «сколько нам должны».";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, YearArg, MonthArg, OrgArg);

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        var o = await Reports.OverviewAsync(f, ct);
        if (!o.Meta.HasData)
        {
            return NoData(o.Meta);
        }

        var link = Link("/finance", f);
        return Data(new
        {
            Pnl = o.Pnl.Select(p => new { p.Code, p.Title, Sum = R(p.Value), PreviousSum = R(p.Previous), ChangePct = ChangePct(p.Value, p.Previous) }),
            CashSum = R(o.CashTotal),
            Cash = o.Cash.Select(c => new { Account = c.Key, c.Title, Sum = R(c.Amount), c.Currency, CurrencySum = c.CurrencyAmount is null ? (decimal?)null : R(c.CurrencyAmount.Value, 2) }),
            ReceivablesSum = R(o.Receivables),
            PayablesSum = R(o.Payables),
            AdvancesGivenSum = R(o.AdvancesGiven),
            AdvancesReceivedSum = R(o.AdvancesReceived),
            Inventory = o.Inventory.Select(i => new { Group = i.Title, Sum = R(i.Amount) }),
            BalancesAsOf = o.BalancesMonth is { } bm ? bm.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            Months = o.Months.Select(m => new { Month = m.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture), RevenueSum = R(m.Revenue), ExpensesSum = R(m.Expenses), NetProfitSum = R(m.NetProfit) }),
            ExpenseGrowth = o.ExpenseGrowth.Select(g => new { Account = g.Key, g.Title, Sum = R(g.Amount), PreviousSum = R(g.Previous), ChangePct = ChangePct(g.Amount, g.Previous) }),
            Meta = Meta(o.Meta, link),
        }, Src("обзор", f, link));
    }
}

internal sealed class FinancePnlTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_pnl";
    public override string Title => "Финансы 1С: доходы и расходы";

    public override string Description =>
        "Отчёт о финансовых результатах (форма №2) по 1С за период: строки P&L помесячно и итогом, прошлый период той же длины, расходы по счетам с " +
        "ростом и по подразделениям. Для «как изменились расходы», «какие расходы выросли», «прибыль по месяцам», «финансовый отчёт за период».";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, YearArg, MonthArg, OrgArg);

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        var p = await Reports.PnlAsync(f, ct);
        if (!p.Meta.HasData)
        {
            return NoData(p.Meta);
        }

        var link = Link("/finance/pnl", f);
        var months = p.Months.Select(m => m.ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList();
        return Data(new
        {
            Months = months,
            Lines = p.Lines.Select(l => new
            {
                l.Code, l.Title, l.Kind, Sum = R(l.Total), PreviousSum = R(l.Previous), ChangePct = ChangePct(l.Total, l.Previous),
                ByMonth = months.Count > 1 ? months.Zip(l.Months, (m, v) => new { Month = m, Sum = R(v) }) : null,
            }),
            ExpensesByAccount = args.Top(p.ExpensesByAccount, 15, "expenses_by_account")
                .Select(e => new { Account = e.Key, e.Title, Sum = R(e.Amount), PreviousSum = R(e.Previous), ChangePct = ChangePct(e.Amount, e.Previous) }),
            ExpensesByDepartment = p.ExpensesByDepartment.Select(e => new { Department = e.Title, Sum = R(e.Amount) }),
            Method = "Выручка, себестоимость и расходы — по проводкам 1С на счетах 90–98; закрытие месяца на 9910 и переносы между счетами доходов и расходов не учитываются.",
            Meta = Meta(p.Meta, link),
        }, Src("доходы и расходы", f, link));
    }
}

internal sealed class FinanceCompareTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_compare";
    public override string Title => "Финансы 1С: сравнение периодов";

    public override string Description =>
        "Сравнить два периода по 1С: строки P&L и расходы по счетам — период A (from/to) против периода B (compare_from/compare_to; без них — прошлый " +
        "период той же длины). Для «сравни октябрь с сентябрём», «как изменилась прибыль год к году».";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, YearArg, MonthArg, OrgArg,
        ("compare_from", new { type = "string", description = "Начало периода сравнения ГГГГ-ММ-ДД." }),
        ("compare_to", new { type = "string", description = "Конец периода сравнения ГГГГ-ММ-ДД." }));

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        var compare = f.Previous();
        if (DateOnly.TryParseExact(args.Str("compare_from"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var cf)
            && DateOnly.TryParseExact(args.Str("compare_to"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ctTo) && cf <= ctTo)
        {
            compare = f with { From = cf, To = ctTo };
        }

        var a = await Reports.PnlAsync(f, ct);
        var b = await Reports.PnlAsync(compare, ct);
        if (!a.Meta.HasData)
        {
            return NoData(a.Meta);
        }

        var expenses = await Reports.ExpenseChangesAsync(f, compare, 20, ct);
        var link = Link("/finance/pnl", f);
        return Data(new
        {
            PeriodA = PeriodText(f),
            PeriodB = PeriodText(compare),
            Lines = a.Lines.Select(l =>
            {
                var before = b.Lines.First(x => x.Code == l.Code).Total;
                return new { l.Code, l.Title, SumA = R(l.Total), SumB = R(before), DeltaSum = R(l.Total - before), ChangePct = ChangePct(l.Total, before) };
            }),
            ExpenseChanges = expenses.Select(e => new { Account = e.Key, e.Title, SumA = R(e.Amount), SumB = R(e.Previous ?? 0), DeltaSum = R(e.Amount - (e.Previous ?? 0)), ChangePct = ChangePct(e.Amount, e.Previous) }),
            Meta = Meta(a.Meta, link, new { compare = PeriodText(compare) }),
        }, Src("сравнение периодов", f, link), Src("период сравнения", compare, Link("/finance/pnl", compare)));
    }
}

internal sealed class FinanceCashFlowTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_cash_flow";
    public override string Title => "Финансы 1С: движение денег";

    public override string Description =>
        "Движение денег по 1С за период: поступления и выплаты по статьям (покупатели, поставщики, зарплата, налоги, кредиты, дивиденды…) помесячно, " +
        "чистый поток, остатки на счетах и в кассе (валютные — в валюте и в сумах). Переводы между своими счетами и курсовые разницы не учитываются.";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, YearArg, MonthArg, OrgArg);

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        var c = await Reports.CashAsync(f, ct);
        if (!c.Meta.HasData)
        {
            return NoData(c.Meta);
        }

        var link = Link("/finance/cash", f);
        return Data(new
        {
            InflowSum = R(c.Inflow),
            OutflowSum = R(c.Outflow),
            NetFlowSum = R(c.Inflow - c.Outflow),
            Flows = c.Flows.Select(x => new { x.Code, x.Title, Direction = x.Inflow ? "поступление" : "выплата", Sum = R(x.Total) }),
            Balances = c.Balances.Select(b => new { Account = b.Key, b.Title, Sum = R(b.Amount), b.Currency, CurrencySum = b.CurrencyAmount is null ? (decimal?)null : R(b.CurrencyAmount.Value, 2) }),
            BalancesAsOf = c.BalancesMonth is { } bm ? bm.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            Meta = Meta(c.Meta, link),
        }, Src("деньги", f, link));
    }
}

internal sealed class FinanceSettlementsTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_settlements";
    public override string Title => "Финансы 1С: дебиторка и кредиторка";

    public override string Description =>
        "Задолженность по контрагентам на дату (to) по 1С: kind=receivables — нам должны покупатели (счёт 40), payables — мы должны поставщикам (60). " +
        "Сумма, просрочка по срокам (не наступил, 1–30, 31–60, 61–90, >90 дней, срок не задан), авансы. Просрочка — оценка FIFO по документам и " +
        "срокам оплаты. Для «сколько дебиторка», «кто просрочил оплату», «кому мы должны».";

    public override JsonElement InputSchema { get; } = Schema(ToArg, OrgArg,
        ("kind", new { type = "string", @enum = new[] { "receivables", "payables" }, description = "receivables — дебиторка (по умолчанию), payables — кредиторка." }),
        ("overdue_only", new { type = "boolean", description = "Только контрагенты с просрочкой." }));

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        var kind = args.Str("kind") == "payables" ? "payables" : "receivables";
        var s = await Reports.SettlementsAsync(f, kind, 500, ct);
        if (!s.Meta.HasData)
        {
            return NoData(s.Meta);
        }

        var rows = args.Bool("overdue_only") == true ? s.Rows.Where(r => r.Overdue > 0).ToList() : s.Rows.ToList();
        var link = Link("/finance/settlements", f, ("kind", kind));
        return Data(new
        {
            Kind = kind == "receivables" ? "дебиторская задолженность (нам должны)" : "кредиторская задолженность (мы должны)",
            AsOf = s.BalancesMonth is { } bm ? (bm.AddMonths(1).AddDays(-1) < f.To ? bm.AddMonths(1).AddDays(-1) : f.To).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            TotalSum = R(s.Total),
            OverdueSum = R(s.Overdue),
            AdvancesSum = R(s.Advances),
            Buckets = s.Buckets.ToDictionary(b => b.Key, b => R(b.Value)),
            Counterparties = args.Top(rows, 20, "counterparties").Select(r => new
            {
                r.Name, r.Inn, BalanceSum = R(r.Balance), OverdueSum = R(r.Overdue), r.MaxOverdueDays,
                OldestDue = r.OldestDue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.TermsKnown,
                Link = Link("/finance/entries", f, ("counterparty", r.CounterpartyId), ("account", kind == "receivables" ? "40" : "60")),
            }),
            s.Method,
            Meta = Meta(s.Meta, link, new { kind, overdue_only = args.Bool("overdue_only") == true }),
        }, Src("расчёты", f, link));
    }
}

internal sealed class FinanceAccountsTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_accounts";
    public override string Title => "Финансы 1С: счета и обороты";

    public override string Description =>
        "Счета 1С за период: без account — оборотно-сальдовая ведомость и счета с наибольшими оборотами; с account (например 4010, 5110, 9420) — " +
        "сальдо и обороты счёта, обороты по месяцам и сальдо по аналитике (контрагенты, номенклатура, склады). Для «какие счета с наибольшими " +
        "оборотами», «остаток на счёте», «обороты по 9420».";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, YearArg, MonthArg, OrgArg,
        ("account", new { type = "string", description = "Код счёта, например 4010. Без него — все счета." }));

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        if (args.Str("account") is { Length: > 0 } code)
        {
            var a = await Reports.AccountAsync(f, code.Trim(), ct) ?? throw new ToolArgumentException($"Счёта {code} нет в плане счетов 1С.");
            var link = Link($"/finance/accounts/{a.Account}", f);
            return Data(new
            {
                a.Account, a.Name, a.Type, Subconto = a.Subconto,
                OpeningSum = a.Totals is { } t ? R(t.OpeningDebit - t.OpeningCredit) : (decimal?)null,
                TurnoverDebitSum = a.Totals is { } t2 ? R(t2.TurnoverDebit) : (decimal?)null,
                TurnoverCreditSum = a.Totals is { } t3 ? R(t3.TurnoverCredit) : (decimal?)null,
                ClosingSum = a.Totals is { } t4 ? R(t4.ClosingDebit - t4.ClosingCredit) : (decimal?)null,
                Monthly = a.Monthly.Select(m => new { Month = m.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture), DebitSum = R(m.Debit), CreditSum = R(m.Credit) }),
                Breakdown = args.Top(a.Breakdown, 20, "breakdown").Select(b => new { b.Kind, b.Name, ClosingSum = R(b.Closing), Quantity = R(b.Quantity, 3), b.Currency, CurrencySum = R(b.CurrencyAmount, 2) }),
                Meta = Meta(a.Meta, link, new { account = a.Account }),
            }, Src($"счёт {a.Account}", f, link));
        }

        var trial = await Reports.TrialBalanceAsync(f, ct);
        if (!trial.Meta.HasData)
        {
            return NoData(trial.Meta);
        }

        var all = Link("/finance/accounts", f);
        return Data(new
        {
            TopByTurnover = args.Top(trial.Rows.Where(r => r.Account.Length >= 4).OrderByDescending(r => r.TurnoverDebit + r.TurnoverCredit), 15, "top_by_turnover")
                .Select(r => new { r.Account, r.Name, TurnoverDebitSum = R(r.TurnoverDebit), TurnoverCreditSum = R(r.TurnoverCredit), ClosingSum = R(r.ClosingDebit - r.ClosingCredit) }),
            TotalTurnoverSum = R(trial.Total.TurnoverDebit),
            trial.Method,
            Meta = Meta(trial.Meta, all),
        }, Src("счета", f, all));
    }
}

internal sealed class FinanceOperationsTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_operations";
    public override string Title => "Финансы 1С: продажи, закупки, себестоимость, склад";

    public override string Description =>
        "По документам и остаткам 1С за период: продажи и закупки помесячно, себестоимость продаж, затраты производства, крупнейшие покупатели и " +
        "поставщики, продажи по номенклатуре и валютам, себестоимость единицы товара по месяцам, запасы по складам и группам, выпуск продукции, " +
        "курсы валют. Для «динамика продаж», «как менялась себестоимость», «какие склады с наибольшими остатками».";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, YearArg, MonthArg, OrgArg);

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        var o = await Reports.OperationsAsync(f, ct);
        if (!o.Meta.HasData)
        {
            return NoData(o.Meta);
        }

        var link = Link("/finance/operations", f);
        var months = o.Months.Select(m => m.ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList();
        object Rows(IEnumerable<AmountRow> rows) => rows.Select(r => new { Name = r.Title, Sum = R(r.Amount), Quantity = R(r.Quantity, 3) });
        return Data(new
        {
            Months = months.Select((m, i) => new { Month = m, SalesSum = R(o.Sales[i]), PurchasesSum = R(o.Purchases[i]), CostOfSalesSum = R(o.CostOfSales[i]), ProductionCostSum = R(o.ProductionCosts[i]) }),
            TopCustomers = Rows(o.TopCustomers),
            TopSuppliers = Rows(o.TopSuppliers),
            TopItems = Rows(o.TopItems),
            SalesByCurrency = o.SalesByCurrency.Select(c => new { c.Currency, Sum = R(c.Amount), CurrencySum = R(c.CurrencyAmount, 2) }),
            UnitCosts = o.UnitCosts.Select(u => new { u.Item, u.Unit, ByMonth = months.Zip(u.Months, (m, v) => new { Month = m, CostPerUnitSum = R(v, 2) }) }),
            InventoryByWarehouse = Rows(o.InventoryByWarehouse),
            Inventory = Rows(o.Inventory),
            InventoryAsOf = o.BalancesMonth is { } bm ? bm.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            Production = Rows(o.Production),
            Rates = o.Rates.Select(r => new { r.Currency, First = r.First, Last = r.Last, LastDate = r.LastDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.ChangePercent, r.Source }),
            Meta = Meta(o.Meta, link),
        }, Src("продажи и склад", f, link));
    }
}

internal sealed class FinanceAnomaliesTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_anomalies";
    public override string Title => "Финансы 1С: отклонения";

    public override int Level => ToolLevels.Analyze;

    public override string Description =>
        "Отклонения и подозрительные изменения в 1С за период: резкий рост расходов по счетам, крупные разовые проводки, исправления и отмена " +
        "документов прошлых месяцев, возможные двойные платежи, новые контрагенты с крупными суммами. Это сигналы для проверки, а не выводы о нарушениях.";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, YearArg, MonthArg, OrgArg);

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        var a = await Reports.AnomaliesAsync(f, ct);
        if (!a.Meta.HasData)
        {
            return NoData(a.Meta);
        }

        var link = Link("/finance/anomalies", f);
        return Data(new
        {
            Items = args.Top(a.Items, 20, "items").Select(i => new { i.Kind, i.Title, i.Detail, Sum = R(i.Amount), Date = i.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), i.Severity, i.Link }),
            Checks = a.Checks,
            Note = "Сигналы для проверки бухгалтером — не утверждай, что это ошибка или нарушение.",
            Meta = Meta(a.Meta, link),
        }, Src("отклонения", f, link));
    }
}

internal sealed class FinanceEntriesTool(FinanceReportService reports, IOneCSettingsStore settings, TimeProvider clock) : FinanceToolBase(reports, settings, clock)
{
    public override string Name => "finance_entries";
    public override string Title => "Финансы 1С: проводки";

    public override string Description =>
        "Проводки 1С за период с отбором по счёту (account, по началу кода: 40, 4010, 94), контрагенту (counterparty — id из других finance_* " +
        "инструментов) или документу: дата, Дт, Кт, сумма, содержание, аналитика, документ. Для расшифровки цифры до первичных проводок.";

    public override JsonElement InputSchema { get; } = Schema(FromArg, ToArg, YearArg, MonthArg, OrgArg,
        ("account", new { type = "string", description = "Счёт или его начало (40, 4010, 9420)." }),
        ("counterparty", new { type = "string", description = "Идентификатор контрагента 1С." }));

    protected override async Task<ToolResult> RunAsync(FinanceFilter f, ToolArgs args, CancellationToken ct)
    {
        var account = args.Str("account");
        var counterparty = args.Str("counterparty");
        var page = await Reports.EntriesAsync(f, account, counterparty, null, 1, args.All ? 200 : 30, ct);
        if (!page.Meta.HasData)
        {
            return NoData(page.Meta);
        }

        args.Count("entries", page.Total, page.Items.Count);
        var link = Link("/finance/entries", f, ("account", account), ("counterparty", counterparty));
        return Data(new
        {
            TotalCount = page.Total,
            TotalSum = R(page.Amount),
            Entries = page.Items.Select(e => new
            {
                Date = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.DebitAccount, e.CreditAccount, Sum = R(e.Amount, 2), e.Content, e.Counterparty,
                e.Currency, CurrencySum = R(e.CurrencyAmount, 2), Document = $"{e.RegistrarSourceType ?? e.RegistrarType} № {e.RegistrarNumber}",
                Analytics = string.Join(" · ", e.Subconto.Where(s => s.Name is not null).Select(s => s.Name)),
                Link = e.DocumentId is { } id ? $"/finance/documents/{id}" : null,
            }),
            Meta = Meta(page.Meta, link, new { account, counterparty }),
        }, Src("проводки", f, link));
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.OneC;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Application.Finance;

/// <summary>
/// Финансовые отчёты OneBase по данным 1С: только чтение схемы onec. Факты 1С не пересчитываются и не смешиваются с управленческими данными
/// OneBase: в каждом ответе — источник «1С:Бухгалтерия (факт)», период и время синхронизации. Суммы — в валюте учёта из 1С.
/// </summary>
public sealed class FinanceReportService(IAppDbContext db, IOneCSettingsStore settings, TimeProvider clock)
{
    public const string SourceTitle = "1С:Бухгалтерия";

    // ---------- Общие ----------

    public async Task<IReadOnlyList<FinanceOrganization>> OrganizationsAsync(CancellationToken ct) =>
        await db.OneCOrganizations.AsNoTracking().Where(o => !o.IsDeleted).OrderBy(o => o.Name)
            .Select(o => new FinanceOrganization(o.SourceEntityId, o.Name, o.Inn, o.AccountingCurrency)).ToListAsync(ct);

    public async Task<FinanceMeta> MetaAsync(FinanceFilter f, CancellationToken ct, string? note = null)
    {
        var s = await settings.GetAsync(ct);
        var synced = await db.OneCSyncRuns.AsNoTracking().Where(r => r.Status == RunStatuses.Succeeded && r.Kind != RunKinds.Test)
            .MaxAsync(r => (DateTimeOffset?)r.FinishedAt, ct);
        var today = OneCFormat.LocalDate(clock.GetUtcNow());
        var through = await Entries(f with { From = DateOnly.MinValue, To = today }).MaxAsync(e => (DateOnly?)e.PeriodDate, ct);
        var org = f.OrganizationId is null ? null : await db.OneCOrganizations.AsNoTracking()
            .Where(o => o.SourceEntityId == f.OrganizationId).Select(o => o.Name).FirstOrDefaultAsync(ct);
        var hasData = await db.OneCEntries.AnyAsync(ct) || await db.OneCDocuments.AnyAsync(ct);
        var dimensions = await DimensionsAsync(f, ct);
        var dimensionNote = f.ItemId is not null ? "Отбор по номенклатуре: проводки — по ней; документы — содержащие её (сумма документа целиком)."
            : f.DepartmentId is not null ? "Отбор по подразделению: в оборотно-сальдовой ведомости подразделения нет — остатки показаны без него."
            : null;
        return new FinanceMeta(s.DemoData ? SourceTitle + " — тестовый источник (демо-данные)" : SourceTitle, "fact", s.DemoData, synced,
            s.AcceptedInfobaseName, f.OrganizationId, org, f.From, f.To, through, hasData,
            note ?? (hasData ? dimensionNote : "Данных 1С в OneBase пока нет: подключение и загрузка — в «Настройки → Интеграции → Финансы → 1С»."),
            dimensions);
    }

    /// <summary>Названия применённых отборов (контрагент, номенклатура, склад, подразделение) — для подписи отчёта.</summary>
    private async Task<List<FinanceDimension>> DimensionsAsync(FinanceFilter f, CancellationToken ct)
    {
        var list = new List<FinanceDimension>();
        if (f.CounterpartyId is { } c)
        {
            list.Add(new FinanceDimension("counterparty", c, await db.OneCCounterparties.AsNoTracking().Where(x => x.SourceEntityId == c).Select(x => x.Name).FirstOrDefaultAsync(ct) ?? c));
        }

        if (f.ItemId is { } i)
        {
            list.Add(new FinanceDimension("item", i, await db.OneCItems.AsNoTracking().Where(x => x.SourceEntityId == i).Select(x => x.Name).FirstOrDefaultAsync(ct) ?? i));
        }

        foreach (var (kind, id) in new[] { (OneCEntityTypes.Warehouse, f.WarehouseId), (OneCEntityTypes.Department, f.DepartmentId) })
        {
            if (id is not null)
            {
                list.Add(new FinanceDimension(kind, id, await db.OneCReferences.AsNoTracking().Where(x => x.SourceEntityType == kind && x.SourceEntityId == id)
                    .Select(x => x.Name).FirstOrDefaultAsync(ct) ?? id));
            }
        }

        return list;
    }

    /// <summary>Значения для отборов: контрагенты, номенклатура, склады, подразделения 1С по началу названия, кода или ИНН (до limit).</summary>
    public async Task<IReadOnlyList<FinanceDimension>> DimensionOptionsAsync(string kind, string? query, int limit, CancellationToken ct)
    {
        var q = (query ?? "").Trim();
        var lower = q.ToLowerInvariant();
        limit = Math.Clamp(limit, 1, 50);
        return kind switch
        {
            "counterparty" => await db.OneCCounterparties.AsNoTracking()
                .Where(x => !x.IsDeleted && (q == "" || x.Name.ToLower().Contains(lower) || x.Inn == q || x.Code == q))
                .OrderBy(x => x.Name).Take(limit).Select(x => new FinanceDimension(kind, x.SourceEntityId, x.Name)).ToListAsync(ct),
            "item" => await db.OneCItems.AsNoTracking()
                .Where(x => !x.IsDeleted && !x.IsGroup && (q == "" || x.Name.ToLower().Contains(lower) || x.Code == q || x.Article == q))
                .OrderBy(x => x.Name).Take(limit).Select(x => new FinanceDimension(kind, x.SourceEntityId, x.Name)).ToListAsync(ct),
            OneCEntityTypes.Warehouse or OneCEntityTypes.Department => await db.OneCReferences.AsNoTracking()
                .Where(x => !x.IsDeleted && x.SourceEntityType == kind && !x.IsGroup && (q == "" || x.Name.ToLower().Contains(lower)))
                .OrderBy(x => x.Name).Take(limit).Select(x => new FinanceDimension(kind, x.SourceEntityId, x.Name)).ToListAsync(ct),
            _ => [],
        };
    }

    private IQueryable<OneCEntry> Entries(FinanceFilter f) =>
        db.OneCEntries.AsNoTracking().Where(e => !e.IsDeleted && e.Active && e.PeriodDate >= f.From && e.PeriodDate <= f.To
            && (f.OrganizationId == null || e.SourceOrganizationId == f.OrganizationId)
            && (f.CounterpartyId == null || e.DebitCounterpartyId == f.CounterpartyId || e.CreditCounterpartyId == f.CounterpartyId)
            && (f.ItemId == null || e.DebitItemId == f.ItemId || e.CreditItemId == f.ItemId)
            && (f.WarehouseId == null || e.DebitWarehouseId == f.WarehouseId || e.CreditWarehouseId == f.WarehouseId)
            && (f.DepartmentId == null || e.DepartmentSourceId == f.DepartmentId));

    private IQueryable<OneCDocument> Documents(FinanceFilter f) =>
        db.OneCDocuments.AsNoTracking().Where(d => !d.IsDeleted && d.Posted && d.DocumentDate >= f.From && d.DocumentDate <= f.To
            && (f.OrganizationId == null || d.SourceOrganizationId == f.OrganizationId)
            && (f.CounterpartyId == null || d.CounterpartySourceId == f.CounterpartyId)
            && (f.DepartmentId == null || d.DepartmentSourceId == f.DepartmentId)
            && (f.WarehouseId == null || d.WarehouseSourceId == f.WarehouseId || d.Lines.Any(l => l.WarehouseSourceId == f.WarehouseId))
            && (f.ItemId == null || d.Lines.Any(l => l.ItemSourceId == f.ItemId)));

    /// <summary>Строки ведомости с отбором по организации, контрагенту, номенклатуре и складу (подразделения в ведомости нет).</summary>
    private IQueryable<OneCBalance> Balances(FinanceFilter f) =>
        db.OneCBalances.AsNoTracking().Where(b => !b.IsDeleted && (f.OrganizationId == null || b.SourceOrganizationId == f.OrganizationId)
            && (f.CounterpartyId == null || b.CounterpartyId == f.CounterpartyId)
            && (f.ItemId == null || b.ItemId == f.ItemId)
            && (f.WarehouseId == null || b.WarehouseId == f.WarehouseId));

    public static IReadOnlyList<DateOnly> MonthsOf(FinanceFilter f)
    {
        var list = new List<DateOnly>();
        for (var m = OneCFormat.MonthStart(f.From); m <= f.To; m = m.AddMonths(1))
        {
            list.Add(m);
        }

        return list;
    }

    private async Task<List<FinanceChart.Turnover>> PnlTurnoversAsync(FinanceFilter f, CancellationToken ct) =>
        (await Entries(f).Where(e => e.DebitAccount.StartsWith("9") || e.CreditAccount.StartsWith("9"))
            .GroupBy(e => new { e.DebitAccount, e.CreditAccount, e.PeriodDate.Year, e.PeriodDate.Month })
            .Select(g => new { g.Key.DebitAccount, g.Key.CreditAccount, g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct))
        .Select(x => new FinanceChart.Turnover(x.DebitAccount, x.CreditAccount, new DateOnly(x.Year, x.Month, 1), x.Amount)).ToList();

    private async Task<Dictionary<string, string>> AccountNamesAsync(CancellationToken ct) =>
        (await db.OneCAccounts.AsNoTracking().Where(a => !a.IsDeleted).Select(a => new { a.Code, a.Name }).ToListAsync(ct))
        .GroupBy(a => a.Code).ToDictionary(g => g.Key, g => g.First().Name);

    private async Task<Dictionary<string, (string Name, string? Inn)>> CounterpartiesAsync(IEnumerable<string?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i is not null).Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        return (await db.OneCCounterparties.AsNoTracking().Where(c => list.Contains(c.SourceEntityId)).Select(c => new { c.SourceEntityId, c.Name, c.Inn }).ToListAsync(ct))
            .GroupBy(c => c.SourceEntityId).ToDictionary(g => g.Key, g => (g.First().Name, g.First().Inn));
    }

    private async Task<Dictionary<string, string>> ReferenceNamesAsync(IEnumerable<string?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i is not null).Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        return (await db.OneCReferences.AsNoTracking().Where(r => list.Contains(r.SourceEntityId)).Select(r => new { r.SourceEntityId, r.Name }).ToListAsync(ct))
            .GroupBy(r => r.SourceEntityId).ToDictionary(g => g.Key, g => g.First().Name);
    }

    private async Task<Dictionary<string, (string Name, string? Unit)>> ItemsAsync(IEnumerable<string?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i is not null).Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        return (await db.OneCItems.AsNoTracking().Where(i => list.Contains(i.SourceEntityId)).Select(i => new { i.SourceEntityId, i.Name, i.Unit }).ToListAsync(ct))
            .GroupBy(i => i.SourceEntityId).ToDictionary(g => g.Key, g => (g.First().Name, g.First().Unit));
    }

    // ---------- Остатки (оборотно-сальдовая ведомость 1С) ----------

    /// <summary>Последний месяц с остатками 1С не позже конца периода.</summary>
    private async Task<DateOnly?> BalancesMonthAsync(FinanceFilter f, CancellationToken ct)
    {
        var limit = OneCFormat.MonthStart(f.To);
        return await Balances(f).Where(b => b.Period <= limit).MaxAsync(b => (DateOnly?)b.Period, ct);
    }

    private IQueryable<OneCBalance> BalancesOf(FinanceFilter f, DateOnly month) => Balances(f).Where(b => b.Period == month);

    /// <summary>Сальдо на конец месяца по группам счетов: Дт − Кт.</summary>
    private async Task<decimal> ClosingAsync(FinanceFilter f, DateOnly? month, string[] prefixes, CancellationToken ct)
    {
        if (month is not { } m)
        {
            return 0;
        }

        var rows = await BalancesOf(f, m).GroupBy(b => b.Account)
            .Select(g => new { Account = g.Key, Debit = g.Sum(x => x.ClosingDebit), Credit = g.Sum(x => x.ClosingCredit) }).ToListAsync(ct);
        return rows.Where(r => FinanceChart.Is(r.Account, prefixes)).Sum(r => r.Debit - r.Credit);
    }

    // ---------- Обзор ----------

    public async Task<FinanceOverview> OverviewAsync(FinanceFilter f, CancellationToken ct)
    {
        var meta = await MetaAsync(f, ct);
        var previous = f.Previous();
        var current = FinanceChart.Pnl(await PnlTurnoversAsync(f, ct));
        var before = FinanceChart.Pnl(await PnlTurnoversAsync(previous, ct));
        var pnl = FinanceChart.PnlLines.Select(l => new PnlValue(l.Code, l.Title, l.Kind, current[l.Code], before[l.Code])).ToList();

        // 12 месяцев до конца периода — для графика.
        var yearFilter = f with { From = OneCFormat.MonthStart(f.To).AddMonths(-11) };
        var turnovers = await PnlTurnoversAsync(yearFilter, ct);
        var months = MonthsOf(yearFilter).Select(m =>
        {
            var p = FinanceChart.Pnl(turnovers.Where(t => t.Month == m));
            var expenses = p["cost_of_sales"] + p["selling"] + p["administrative"] + p["other_operating"] + p["future_deductible"] + p["financial_expense"];
            return new MonthPoint(m, p["net_revenue"], expenses, p["net_profit"]);
        }).ToList();

        var month = await BalancesMonthAsync(f, ct);
        var cash = new List<AmountRow>();
        var inventory = new List<AmountRow>();
        decimal receivables = 0, payables = 0, advancesGiven = 0, advancesReceived = 0;
        if (month is { } bm)
        {
            var names = await AccountNamesAsync(ct);
            var rows = await BalancesOf(f, bm).GroupBy(b => new { b.Account, b.Currency })
                .Select(g => new { g.Key.Account, g.Key.Currency, Debit = g.Sum(x => x.ClosingDebit), Credit = g.Sum(x => x.ClosingCredit), CurDebit = g.Sum(x => x.ClosingCurrencyDebit ?? 0), CurCredit = g.Sum(x => x.ClosingCurrencyCredit ?? 0) })
                .ToListAsync(ct);
            cash = rows.Where(r => FinanceChart.Is(r.Account, FinanceChart.Cash) && r.Debit - r.Credit != 0)
                .Select(r => new AmountRow(r.Account, names.GetValueOrDefault(r.Account, r.Account), r.Debit - r.Credit, Currency: r.Currency,
                    CurrencyAmount: r.Currency is null || r.Currency == "UZS" ? null : r.CurDebit - r.CurCredit))
                .OrderByDescending(r => r.Amount).ToList();
            inventory = rows.Where(r => FinanceChart.Is(r.Account, FinanceChart.Inventory)).GroupBy(r => FinanceChart.InventoryGroup(r.Account))
                .Select(g => new AmountRow(g.Key, g.Key, g.Sum(r => r.Debit - r.Credit))).Where(r => r.Amount != 0).OrderByDescending(r => r.Amount).ToList();

            // Расчёты считаются по контрагентам: долг одного не гасится авансом другого.
            var byCounterparty = await BalancesOf(f, bm).Where(b => b.Account.StartsWith("40") || b.Account.StartsWith("43") || b.Account.StartsWith("60") || b.Account.StartsWith("63"))
                .GroupBy(b => new { Group = b.Account.Substring(0, 2), b.CounterpartyId })
                .Select(g => new { g.Key.Group, Balance = g.Sum(x => x.ClosingDebit - x.ClosingCredit) }).ToListAsync(ct);
            receivables = byCounterparty.Where(r => r.Group == "40" && r.Balance > 0).Sum(r => r.Balance);
            advancesGiven = byCounterparty.Where(r => r.Group == "43" && r.Balance > 0).Sum(r => r.Balance);
            payables = byCounterparty.Where(r => r.Group == "60" && r.Balance < 0).Sum(r => -r.Balance);
            advancesReceived = byCounterparty.Where(r => r.Group == "63" && r.Balance < 0).Sum(r => -r.Balance);
        }

        var growth = await ExpenseChangesAsync(f, previous, 5, ct);
        return new FinanceOverview(meta, pnl, months, cash.Sum(c => c.Amount), cash, receivables, payables, advancesGiven, advancesReceived, inventory, month,
            growth.Where(g => g.Amount > (g.Previous ?? 0)).ToList(), await KpisAsync(f, meta.DataThrough, ct));
    }

    /// <summary>
    /// Показатели за день, с начала месяца и с начала года на дату конца периода (не позже последних проводок): чистая выручка и валовая
    /// прибыль — по проводкам счетов доходов и расходов (как P&L), закупки — по документам поступления за вычетом возвратов поставщикам
    /// (в валюте учёта), затраты на производство — Дт 20. С теми же отборами, что и отчёт.
    /// </summary>
    public async Task<FinanceKpis> KpisAsync(FinanceFilter f, DateOnly? dataThrough, CancellationToken ct)
    {
        var asOf = dataThrough is { } d && d < f.To ? d : f.To;
        var periods = new[] { (From: asOf, To: asOf), (From: new DateOnly(asOf.Year, asOf.Month, 1), To: asOf), (From: new DateOnly(asOf.Year, 1, 1), To: asOf) };
        var values = new Dictionary<string, decimal[]> { ["sales"] = new decimal[3], ["gross_profit"] = new decimal[3], ["purchases"] = new decimal[3], ["production"] = new decimal[3] };
        for (var i = 0; i < periods.Length; i++)
        {
            var p = f with { From = periods[i].From, To = periods[i].To };
            var pnl = FinanceChart.Pnl(await PnlTurnoversAsync(p, ct));
            values["sales"][i] = pnl["net_revenue"];
            values["gross_profit"][i] = pnl["gross_profit"];
            values["purchases"][i] = await Documents(p).Where(x => x.Type == OneCDocumentTypes.Purchase || x.Type == OneCDocumentTypes.PurchaseReturn)
                .SumAsync(x => x.Type == OneCDocumentTypes.Purchase ? x.AmountAccounting : -x.AmountAccounting, ct);
            values["production"][i] = await Entries(p).Where(e => e.DebitAccount.StartsWith("20")).SumAsync(e => e.Amount, ct);
        }

        FinanceKpi KpiOf(string code, string title) => new(code, title, values[code][0], values[code][1], values[code][2]);
        return new FinanceKpis(asOf, [KpiOf("sales", "Продажи (чистая выручка)"), KpiOf("gross_profit", "Валовая прибыль"), KpiOf("purchases", "Закупки"),
            KpiOf("production", "Затраты на производство")]);
    }

    // ---------- Доходы и расходы ----------

    public async Task<FinancePnl> PnlAsync(FinanceFilter f, CancellationToken ct)
    {
        var meta = await MetaAsync(f, ct);
        var months = MonthsOf(f);
        var turnovers = await PnlTurnoversAsync(f, ct);
        var previous = FinanceChart.Pnl(await PnlTurnoversAsync(f.Previous(), ct));
        var byMonth = months.Select(m => FinanceChart.Pnl(turnovers.Where(t => t.Month == m))).ToList();
        var total = FinanceChart.Pnl(turnovers);
        var lines = FinanceChart.PnlLines
            .Select(l => new PnlLineView(l.Code, l.Title, l.Kind, byMonth.Select(p => p[l.Code]).ToList(), total[l.Code], previous[l.Code])).ToList();

        var expenses = await ExpenseChangesAsync(f, f.Previous(), 30, ct);
        var departments = await Entries(f).Where(e => e.DebitAccount.StartsWith("94") && !e.CreditAccount.StartsWith("99") && !e.CreditAccount.StartsWith("9"))
            .GroupBy(e => e.DepartmentSourceId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var deptNames = await ReferenceNamesAsync(departments.Select(d => d.Id), ct);
        var byDepartment = departments.OrderByDescending(d => d.Amount)
            .Select(d => new AmountRow(d.Id ?? "-", d.Id is null ? "Без подразделения" : deptNames.GetValueOrDefault(d.Id, d.Id), d.Amount)).ToList();
        return new FinancePnl(meta, months, lines, expenses, byDepartment);
    }

    /// <summary>Расходы по счетам (себестоимость, расходы периода, финансовые, налог) за период и за период сравнения — по росту.</summary>
    public async Task<List<AmountRow>> ExpenseChangesAsync(FinanceFilter f, FinanceFilter compare, int top, CancellationToken ct)
    {
        async Task<Dictionary<string, decimal>> SumAsync(FinanceFilter x)
        {
            var rows = await Entries(x).Where(e => e.DebitAccount.StartsWith("91") || e.DebitAccount.StartsWith("94") || e.DebitAccount.StartsWith("96") || e.DebitAccount.StartsWith("98"))
                .GroupBy(e => new { e.DebitAccount, e.CreditAccount }).Select(g => new { g.Key.DebitAccount, g.Key.CreditAccount, Amount = g.Sum(e => e.Amount) }).ToListAsync(ct);
            return rows.Where(r => !FinanceChart.IsClosing(r.DebitAccount, r.CreditAccount)).GroupBy(r => r.DebitAccount).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        }

        var now = await SumAsync(f);
        var before = await SumAsync(compare);
        var names = await AccountNamesAsync(ct);
        return now.Keys.Union(before.Keys)
            .Select(k => new AmountRow(k, names.GetValueOrDefault(k, k), now.GetValueOrDefault(k), before.GetValueOrDefault(k)))
            .OrderByDescending(r => r.Amount - (r.Previous ?? 0)).ThenByDescending(r => r.Amount).Take(top).ToList();
    }

    // ---------- Деньги ----------

    public async Task<FinanceCash> CashAsync(FinanceFilter f, CancellationToken ct)
    {
        var meta = await MetaAsync(f, ct);
        var months = MonthsOf(f);
        var rows = await Entries(f).Where(e => e.DebitAccount.StartsWith("5") || e.CreditAccount.StartsWith("5"))
            .GroupBy(e => new { e.DebitAccount, e.CreditAccount, e.PeriodDate.Year, e.PeriodDate.Month })
            .Select(g => new { g.Key.DebitAccount, g.Key.CreditAccount, g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);

        var flows = new Dictionary<(string Code, bool Inflow), (string Title, decimal[] Months)>();
        foreach (var r in rows)
        {
            var debitCash = FinanceChart.Is(r.DebitAccount, FinanceChart.Cash);
            var creditCash = FinanceChart.Is(r.CreditAccount, FinanceChart.Cash);
            if (debitCash == creditCash)
            {
                continue; // перевод между своими счетами (касса ↔ банк) — не движение денег компании
            }

            if (FinanceChart.Is(debitCash ? r.CreditAccount : r.DebitAccount, FinanceChart.ExchangeDifferences))
            {
                continue; // переоценка валюты и курсовые разницы — деньги не двигались
            }

            var inflow = debitCash;
            var (code, title) = FinanceChart.CashCategory(inflow ? r.CreditAccount : r.DebitAccount, inflow);
            var index = months.ToList().IndexOf(new DateOnly(r.Year, r.Month, 1));
            if (index < 0)
            {
                continue;
            }

            if (!flows.TryGetValue((code, inflow), out var flow))
            {
                flow = (title, new decimal[months.Count]);
                flows[(code, inflow)] = flow;
            }

            flow.Months[index] += r.Amount;
        }

        var list = flows.Select(x => new CashFlowRow(x.Key.Code, x.Value.Title, x.Key.Inflow, x.Value.Months, x.Value.Months.Sum()))
            .OrderByDescending(x => x.Inflow).ThenByDescending(x => x.Total).ToList();

        var month = await BalancesMonthAsync(f, ct);
        var balances = new List<AmountRow>();
        if (month is { } bm)
        {
            var names = await AccountNamesAsync(ct);
            balances = (await BalancesOf(f, bm).GroupBy(b => new { b.Account, b.Currency })
                    .Select(g => new { g.Key.Account, g.Key.Currency, Amount = g.Sum(x => x.ClosingDebit - x.ClosingCredit), Cur = g.Sum(x => (x.ClosingCurrencyDebit ?? 0) - (x.ClosingCurrencyCredit ?? 0)) })
                    .ToListAsync(ct))
                .Where(r => FinanceChart.Is(r.Account, FinanceChart.Cash) && r.Amount != 0)
                .Select(r => new AmountRow(r.Account, names.GetValueOrDefault(r.Account, r.Account), r.Amount, Currency: r.Currency,
                    CurrencyAmount: r.Currency is null || r.Currency == "UZS" ? null : r.Cur))
                .OrderByDescending(r => r.Amount).ToList();
        }

        return new FinanceCash(meta, months, list, list.Where(x => x.Inflow).Sum(x => x.Total), list.Where(x => !x.Inflow).Sum(x => x.Total), balances, month);
    }

    // ---------- Расчёты с контрагентами ----------

    public static readonly IReadOnlyList<string> AgingBuckets = ["not_due", "1_30", "31_60", "61_90", "over_90", "no_term"];

    /// <summary>
    /// Дебиторка (receivables) или кредиторка (payables) по контрагентам на конец последнего месяца с остатками 1С. Просрочка — оценка по FIFO:
    /// оплаты гасят самые старые отгрузки (поступления), непогашенный остаток раскладывается по документам с конца; срок — из документа (срок оплаты)
    /// или из договора (отсрочка в днях); без срока — «срок не задан».
    /// </summary>
    public async Task<FinanceSettlements> SettlementsAsync(FinanceFilter f, string kind, int top, CancellationToken ct)
    {
        var receivable = kind != "payables";
        var meta = await MetaAsync(f, ct);
        var month = await BalancesMonthAsync(f, ct);
        var method = "Остатки — из оборотно-сальдовой ведомости 1С; просрочка — оценка FIFO по документам " + (receivable ? "реализации" : "поступления")
            + " и срокам оплаты (документ или договор).";
        var empty = AgingBuckets.ToDictionary(b => b, _ => 0m);
        if (month is not { } bm)
        {
            return new FinanceSettlements(meta with { Note = meta.Note ?? "Остатков 1С по расчётам пока нет — загрузите историю." }, kind, null, 0, 0, empty, 0, [], method);
        }

        var main = receivable ? "40" : "60";
        var advance = receivable ? "63" : "43";
        var rows = await BalancesOf(f, bm).Where(b => b.Account.StartsWith(main) || b.Account.StartsWith(advance))
            .GroupBy(b => new { b.CounterpartyId, Main = b.Account.StartsWith(main) })
            .Select(g => new { g.Key.CounterpartyId, g.Key.Main, Balance = g.Sum(x => x.ClosingDebit - x.ClosingCredit) }).ToListAsync(ct);
        var debts = rows.Where(r => r.Main).Select(r => (r.CounterpartyId, Debt: receivable ? r.Balance : -r.Balance)).Where(r => r.Debt > 0)
            .OrderByDescending(r => r.Debt).ToList();
        var advances = rows.Where(r => !r.Main).Sum(r => receivable ? -r.Balance : r.Balance);

        var asOf = bm.AddMonths(1).AddDays(-1) < f.To ? bm.AddMonths(1).AddDays(-1) : f.To;
        var today = OneCFormat.LocalDate(clock.GetUtcNow());
        if (asOf > today)
        {
            asOf = today;
        }

        var ids = debts.Where(d => d.CounterpartyId is not null).Select(d => d.CounterpartyId!).ToList();
        var types = receivable ? new[] { OneCDocumentTypes.Sale } : [OneCDocumentTypes.Purchase];
        var since = asOf.AddYears(-3);
        var docs = await db.OneCDocuments.AsNoTracking()
            .Where(d => !d.IsDeleted && d.Posted && types.Contains(d.Type) && d.CounterpartySourceId != null && ids.Contains(d.CounterpartySourceId)
                && d.DocumentDate <= asOf && d.DocumentDate >= since && (f.OrganizationId == null || d.SourceOrganizationId == f.OrganizationId))
            .Select(d => new { d.CounterpartySourceId, d.DocumentDate, d.DueDate, d.ContractSourceId, d.AmountAccounting }).ToListAsync(ct);
        var contractIds = docs.Select(d => d.ContractSourceId).Where(c => c is not null).Distinct().ToList();
        var terms = (await db.OneCContracts.AsNoTracking().Where(c => contractIds.Contains(c.SourceEntityId)).Select(c => new { c.SourceEntityId, c.PaymentTermDays }).ToListAsync(ct))
            .GroupBy(c => c.SourceEntityId).ToDictionary(g => g.Key, g => g.First().PaymentTermDays);
        var names = await CounterpartiesAsync(debts.Select(d => d.CounterpartyId), ct);

        var totalBuckets = AgingBuckets.ToDictionary(b => b, _ => 0m);
        var result = new List<SettlementRow>();
        foreach (var (id, debt) in debts)
        {
            var buckets = AgingBuckets.ToDictionary(b => b, _ => 0m);
            var left = debt;
            DateOnly? oldest = null;
            int? maxDays = null;
            var termsKnown = true;
            foreach (var d in docs.Where(x => x.CounterpartySourceId == id).OrderByDescending(x => x.DocumentDate))
            {
                if (left <= 0)
                {
                    break;
                }

                var part = Math.Min(left, d.AmountAccounting);
                left -= part;
                var due = d.DueDate ?? (d.ContractSourceId is { } c && terms.GetValueOrDefault(c) is int days ? d.DocumentDate.AddDays(days) : null);
                string bucket;
                if (due is not { } dueDate)
                {
                    bucket = "no_term";
                    termsKnown = false;
                }
                else
                {
                    var overdue = asOf.DayNumber - dueDate.DayNumber;
                    bucket = overdue <= 0 ? "not_due" : overdue <= 30 ? "1_30" : overdue <= 60 ? "31_60" : overdue <= 90 ? "61_90" : "over_90";
                    if (overdue > 0)
                    {
                        oldest = oldest is null || dueDate < oldest ? dueDate : oldest;
                        maxDays = Math.Max(maxDays ?? 0, overdue);
                    }
                }

                buckets[bucket] += part;
            }

            if (left > 0)
            {
                // Долг старше документов за 3 года (или начальные остатки) — срок неизвестен.
                buckets["no_term"] += left;
                termsKnown = false;
            }

            foreach (var (b, v) in buckets)
            {
                totalBuckets[b] += v;
            }

            var overdueSum = buckets["1_30"] + buckets["31_60"] + buckets["61_90"] + buckets["over_90"];
            var name = id is null ? (receivable ? "Без контрагента" : "Без контрагента") : names.TryGetValue(id, out var n) ? n.Name : id;
            result.Add(new SettlementRow(id ?? "-", name, id is not null && names.TryGetValue(id, out var nn) ? nn.Inn : null, debt, overdueSum, oldest, maxDays, buckets, termsKnown));
        }

        var total = result.Sum(r => r.Balance);
        return new FinanceSettlements(meta, receivable ? "receivables" : "payables", bm, total,
            totalBuckets["1_30"] + totalBuckets["31_60"] + totalBuckets["61_90"] + totalBuckets["over_90"], totalBuckets, advances,
            result.Take(Math.Clamp(top, 1, 500)).ToList(), method);
    }

    // ---------- Оборотно-сальдовая ведомость ----------

    public async Task<FinanceTrialBalance> TrialBalanceAsync(FinanceFilter f, CancellationToken ct)
    {
        var meta = await MetaAsync(f, ct);
        var names = await AccountNamesAsync(ct);
        var first = OneCFormat.MonthStart(f.From);
        var last = OneCFormat.MonthStart(f.To);
        var rows = await Balances(f)
            .Where(b => b.Period >= first && b.Period <= last)
            .GroupBy(b => new { b.Account, b.Period })
            .Select(g => new
            {
                g.Key.Account, g.Key.Period, OD = g.Sum(x => x.OpeningDebit), OC = g.Sum(x => x.OpeningCredit), TD = g.Sum(x => x.TurnoverDebit),
                TC = g.Sum(x => x.TurnoverCredit), CD = g.Sum(x => x.ClosingDebit), CC = g.Sum(x => x.ClosingCredit),
            }).ToListAsync(ct);

        string method;
        List<TrialRow> result;
        if (rows.Count > 0)
        {
            method = "Оборотно-сальдовая ведомость 1С по месяцам: сальдо на начало — первого месяца, обороты — сумма месяцев, сальдо на конец — последнего месяца.";
            result = rows.GroupBy(r => r.Account).Select(g =>
            {
                var start = g.MinBy(x => x.Period)!;
                var end = g.MaxBy(x => x.Period)!;
                return new TrialRow(g.Key, names.GetValueOrDefault(g.Key, g.Key), start.OD, start.OC, g.Sum(x => x.TD), g.Sum(x => x.TC), end.CD, end.CC);
            }).OrderBy(r => r.Account, StringComparer.Ordinal).ToList();
        }
        else
        {
            // Остатков нет — только обороты из проводок (сальдо неизвестно).
            method = "Остатков 1С за период нет: показаны только обороты по проводкам, сальдо не рассчитывается.";
            var debit = await Entries(f).GroupBy(e => e.DebitAccount).Select(g => new { Account = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
            var credit = await Entries(f).GroupBy(e => e.CreditAccount).Select(g => new { Account = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
            result = debit.Select(d => d.Account).Union(credit.Select(c => c.Account)).Select(a => new TrialRow(a, names.GetValueOrDefault(a, a), 0, 0,
                    debit.Where(d => d.Account == a).Sum(d => d.Amount), credit.Where(c => c.Account == a).Sum(c => c.Amount), 0, 0))
                .OrderBy(r => r.Account, StringComparer.Ordinal).ToList();
        }

        var total = new TrialRow("total", "Итого", result.Sum(r => r.OpeningDebit), result.Sum(r => r.OpeningCredit), result.Sum(r => r.TurnoverDebit),
            result.Sum(r => r.TurnoverCredit), result.Sum(r => r.ClosingDebit), result.Sum(r => r.ClosingCredit));
        return new FinanceTrialBalance(meta, result, total, method);
    }

    public async Task<FinanceAccount?> AccountAsync(FinanceFilter f, string code, CancellationToken ct)
    {
        var account = await db.OneCAccounts.AsNoTracking().Where(a => a.Code == code && !a.IsDeleted).FirstOrDefaultAsync(ct);
        if (account is null)
        {
            return null;
        }

        var meta = await MetaAsync(f, ct);
        var trial = await TrialBalanceAsync(f, ct);
        var month = await BalancesMonthAsync(f, ct);
        var breakdown = new List<SubcontoBalance>();
        if (month is { } bm)
        {
            var rows = await BalancesOf(f, bm).Where(b => b.Account == code).ToListAsync(ct);
            foreach (var r in rows)
            {
                var sub = JsonSerializer.Deserialize<List<ImportSubconto>>(r.Subconto, OneCFormat.Json) ?? [];
                var first = sub.OrderBy(s => s.Index).FirstOrDefault();
                breakdown.Add(new SubcontoBalance(OneCFormat.SubcontoKind(first?.Type) ?? first?.Type ?? "-", first?.Id,
                    Personal(first) ? "Сотрудник (скрыто)" : first?.Name ?? "Без аналитики", r.ClosingDebit - r.ClosingCredit,
                    r.ClosingQuantityDebit is null && r.ClosingQuantityCredit is null ? null : (r.ClosingQuantityDebit ?? 0) - (r.ClosingQuantityCredit ?? 0),
                    r.Currency, r.ClosingCurrencyDebit is null && r.ClosingCurrencyCredit is null ? null : (r.ClosingCurrencyDebit ?? 0) - (r.ClosingCurrencyCredit ?? 0)));
            }

            breakdown = breakdown.GroupBy(b => (b.Kind, b.Id, b.Currency))
                .Select(g => g.First() with { Closing = g.Sum(x => x.Closing), Quantity = g.Any(x => x.Quantity is not null) ? g.Sum(x => x.Quantity ?? 0) : null, CurrencyAmount = g.Any(x => x.CurrencyAmount is not null) ? g.Sum(x => x.CurrencyAmount ?? 0) : null })
                .Where(b => b.Closing != 0 || (b.Quantity ?? 0) != 0).OrderByDescending(b => Math.Abs(b.Closing)).Take(200).ToList();
        }

        var debit = await Entries(f).Where(e => e.DebitAccount == code).GroupBy(e => new { e.PeriodDate.Year, e.PeriodDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var credit = await Entries(f).Where(e => e.CreditAccount == code).GroupBy(e => new { e.PeriodDate.Year, e.PeriodDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var monthly = MonthsOf(f).Select(m => new MonthTurnover(m,
            debit.Where(d => d.Year == m.Year && d.Month == m.Month).Sum(d => d.Amount), credit.Where(c => c.Year == m.Year && c.Month == m.Month).Sum(c => c.Amount))).ToList();
        var subconto = (JsonSerializer.Deserialize<List<ImportSubcontoType>>(account.Subconto, OneCFormat.Json) ?? []).OrderBy(s => s.Index).Select(s => s.Type).ToList();
        return new FinanceAccount(meta, account.Code, account.Name, account.Type, subconto, trial.Rows.FirstOrDefault(r => r.Account == code), breakdown, monthly);
    }

    private static bool Personal(ImportSubconto? s) => OneCFormat.SubcontoKind(s?.Type) == "person";

    // ---------- Проводки и документы ----------

    public async Task<FinanceEntries> EntriesAsync(FinanceFilter f, string? account, string? counterpartyId, string? registrarId, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = (Math.Max(1, page), Math.Clamp(pageSize, 10, 200));
        var meta = await MetaAsync(f, ct);
        var q = Entries(f);
        if (!string.IsNullOrWhiteSpace(account))
        {
            q = q.Where(e => e.DebitAccount.StartsWith(account) || e.CreditAccount.StartsWith(account));
        }

        if (!string.IsNullOrWhiteSpace(counterpartyId))
        {
            q = q.Where(e => e.DebitCounterpartyId == counterpartyId || e.CreditCounterpartyId == counterpartyId);
        }

        if (!string.IsNullOrWhiteSpace(registrarId))
        {
            q = q.Where(e => e.RegistrarSourceId == registrarId);
        }

        var total = await q.CountAsync(ct);
        var amount = await q.SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var rows = await q.OrderByDescending(e => e.PeriodDate).ThenBy(e => e.RegistrarSourceId).ThenBy(e => e.LineNumber)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new FinanceEntries(meta, await EntryViewsAsync(rows, ct), total, page, pageSize, amount);
    }

    private async Task<List<EntryView>> EntryViewsAsync(List<OneCEntry> rows, CancellationToken ct)
    {
        var names = await AccountNamesAsync(ct);
        var counterparties = await CounterpartiesAsync(rows.SelectMany(r => new[] { r.DebitCounterpartyId, r.CreditCounterpartyId }), ct);
        var registrars = rows.Select(r => r.RegistrarSourceId).Distinct().ToList();
        var docs = (await db.OneCDocuments.AsNoTracking().Where(d => registrars.Contains(d.SourceEntityId)).Select(d => new { d.SourceEntityId, d.SourceOrganizationId, d.Id }).ToListAsync(ct))
            .GroupBy(d => (d.SourceEntityId, d.SourceOrganizationId)).ToDictionary(g => g.Key, g => g.First().Id);
        return rows.Select(r =>
        {
            var cp = r.DebitCounterpartyId ?? r.CreditCounterpartyId;
            var subconto = Sub("debit", r.DebitSubconto).Concat(Sub("credit", r.CreditSubconto)).ToList();
            return new EntryView(r.Id, r.PeriodDate, r.DebitAccount, names.GetValueOrDefault(r.DebitAccount), r.CreditAccount, names.GetValueOrDefault(r.CreditAccount),
                r.Amount, r.Content, cp is null ? null : counterparties.TryGetValue(cp, out var c) ? c.Name : null,
                r.DebitCurrency ?? r.CreditCurrency, r.DebitCurrencyAmount ?? r.CreditCurrencyAmount, r.DebitQuantity ?? r.CreditQuantity,
                docs.TryGetValue((r.RegistrarSourceId, r.SourceOrganizationId), out var docId) ? docId : null, r.RegistrarType, r.RegistrarNumber, r.RegistrarSourceType, subconto);
        }).ToList();
    }

    private static IEnumerable<SubcontoValue> Sub(string side, string json) =>
        (JsonSerializer.Deserialize<List<ImportSubconto>>(json, OneCFormat.Json) ?? [])
        .Select(s => new SubcontoValue(side, s.Index, s.Type, Personal(s) ? "Сотрудник (скрыто)" : s.Name));

    public async Task<FinanceDocument?> DocumentAsync(Guid id, CancellationToken ct)
    {
        var d = await db.OneCDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null)
        {
            return null;
        }

        var org = await db.OneCOrganizations.AsNoTracking().Where(o => o.SourceEntityId == d.SourceOrganizationId).Select(o => o.Name).FirstOrDefaultAsync(ct);
        var cp = await CounterpartiesAsync([d.CounterpartySourceId], ct);
        var refs = await ReferenceNamesAsync([d.WarehouseSourceId], ct);
        var contract = d.ContractSourceId is null ? null : await db.OneCContracts.AsNoTracking().Where(c => c.SourceEntityId == d.ContractSourceId).Select(c => c.Name).FirstOrDefaultAsync(ct);
        var items = await ItemsAsync(d.Lines.Select(l => l.ItemSourceId), ct);
        var entries = await db.OneCEntries.AsNoTracking().Where(e => e.RegistrarSourceId == d.SourceEntityId && e.SourceOrganizationId == d.SourceOrganizationId && !e.IsDeleted)
            .OrderBy(e => e.LineNumber).ToListAsync(ct);
        var changes = await db.OneCChanges.CountAsync(c => c.SourceEntityId == d.SourceEntityId, ct);
        return new FinanceDocument(d.Id, d.Type, d.SourceType, d.Number, d.DocumentDate, d.Posted, d.IsDeleted, org,
            d.CounterpartySourceId is null ? null : cp.TryGetValue(d.CounterpartySourceId, out var c) ? c.Name : d.CounterpartySourceId, contract,
            d.WarehouseSourceId is null ? null : refs.GetValueOrDefault(d.WarehouseSourceId, d.WarehouseSourceId), d.Currency, d.Amount, d.VatAmount,
            d.AmountAccounting, d.RateValue, d.RateMultiplier, d.RateDate, d.RateSource, d.DueDate, d.Purpose, d.Comment,
            d.Lines.OrderBy(l => l.LineNumber).Select(l => new DocumentLineView(l.LineNumber, l.ItemSourceId is null ? null : items.TryGetValue(l.ItemSourceId, out var i) ? i.Name : l.ItemSourceId,
                l.Quantity, l.Unit, l.Price, l.Amount, l.VatAmount, l.CostAmount, l.Account)).ToList(),
            await EntryViewsAsync(entries, ct), d.SourceVersion, d.ImportedAt, d.UpdatedAt, changes);
    }

    // ---------- Продажи, закупки, себестоимость, склад, производство, валюты ----------

    public async Task<FinanceOperations> OperationsAsync(FinanceFilter f, CancellationToken ct)
    {
        var meta = await MetaAsync(f, ct);
        var months = MonthsOf(f);
        var docTypes = new[] { OneCDocumentTypes.Sale, OneCDocumentTypes.SaleReturn, OneCDocumentTypes.Purchase, OneCDocumentTypes.PurchaseReturn };
        var docMonths = await Documents(f).Where(d => docTypes.Contains(d.Type))
            .GroupBy(d => new { d.Type, d.DocumentDate.Year, d.DocumentDate.Month })
            .Select(g => new { g.Key.Type, g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.AmountAccounting) }).ToListAsync(ct);
        decimal Month(DateOnly m, string plus, string minus) =>
            docMonths.Where(x => x.Year == m.Year && x.Month == m.Month && x.Type == plus).Sum(x => x.Amount)
            - docMonths.Where(x => x.Year == m.Year && x.Month == m.Month && x.Type == minus).Sum(x => x.Amount);
        var sales = months.Select(m => Month(m, OneCDocumentTypes.Sale, OneCDocumentTypes.SaleReturn)).ToList();
        var purchases = months.Select(m => Month(m, OneCDocumentTypes.Purchase, OneCDocumentTypes.PurchaseReturn)).ToList();

        var turnovers = await PnlTurnoversAsync(f, ct);
        var cost = months.Select(m => FinanceChart.Pnl(turnovers.Where(t => t.Month == m))["cost_of_sales"]).ToList();
        var production = await Entries(f).Where(e => e.DebitAccount.StartsWith("20")).GroupBy(e => new { e.PeriodDate.Year, e.PeriodDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var productionCosts = months.Select(m => production.Where(x => x.Year == m.Year && x.Month == m.Month).Sum(x => x.Amount)).ToList();

        async Task<List<AmountRow>> TopCounterpartiesAsync(string plus, string minus)
        {
            var rows = await Documents(f).Where(d => (d.Type == plus || d.Type == minus) && d.CounterpartySourceId != null)
                .GroupBy(d => d.CounterpartySourceId).Select(g => new
                {
                    Id = g.Key!, Amount = g.Sum(x => x.Type == plus ? x.AmountAccounting : -x.AmountAccounting),
                }).OrderByDescending(x => x.Amount).Take(10).ToListAsync(ct);
            var names = await CounterpartiesAsync(rows.Select(r => r.Id), ct);
            return rows.Select(r => new AmountRow(r.Id, names.TryGetValue(r.Id, out var n) ? n.Name : r.Id, r.Amount)).ToList();
        }

        var topCustomers = await TopCounterpartiesAsync(OneCDocumentTypes.Sale, OneCDocumentTypes.SaleReturn);
        var topSuppliers = await TopCounterpartiesAsync(OneCDocumentTypes.Purchase, OneCDocumentTypes.PurchaseReturn);

        // Товары: сумма строки переводится в валюту учёта долей от суммы документа — по курсу самого документа, без текущего курса.
        var lines = from l in db.OneCDocumentLines.AsNoTracking()
                    join d in Documents(f) on l.DocumentId equals d.Id
                    where d.Type == OneCDocumentTypes.Sale && d.Amount != 0 && l.ItemSourceId != null
                    select new { l.ItemSourceId, l.Quantity, l.CostAmount, Amount = Math.Round(l.Amount / d.Amount * d.AmountAccounting, 2), d.DocumentDate.Year, d.DocumentDate.Month };
        var topItemRows = await lines.GroupBy(x => x.ItemSourceId).Select(g => new { Id = g.Key!, Amount = g.Sum(x => x.Amount), Quantity = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Amount).Take(10).ToListAsync(ct);
        var itemNames = await ItemsAsync(topItemRows.Select(r => r.Id), ct);
        var topItems = topItemRows.Select(r => new AmountRow(r.Id, itemNames.TryGetValue(r.Id, out var n) ? n.Name : r.Id, Math.Round(r.Amount, 2), Quantity: r.Quantity)).ToList();

        var topIds = topItemRows.Take(5).Select(r => r.Id).ToList();
        var costRows = await lines.Where(x => topIds.Contains(x.ItemSourceId!) && x.CostAmount != null)
            .GroupBy(x => new { x.ItemSourceId, x.Year, x.Month })
            .Select(g => new { g.Key.ItemSourceId, g.Key.Year, g.Key.Month, Cost = g.Sum(x => x.CostAmount ?? 0), Quantity = g.Sum(x => x.Quantity) }).ToListAsync(ct);
        var unitCosts = topIds.Select(id => new UnitCostRow(id, itemNames.TryGetValue(id, out var n) ? n.Name : id, itemNames.TryGetValue(id, out var u) ? u.Unit : null,
            months.Select(m => costRows.FirstOrDefault(c => c.ItemSourceId == id && c.Year == m.Year && c.Month == m.Month) is { Quantity: > 0 } c
                ? (decimal?)Math.Round(c.Cost / c.Quantity, 2) : null).ToList())).ToList();

        var byCurrency = await Documents(f).Where(d => d.Type == OneCDocumentTypes.Sale).GroupBy(d => d.Currency)
            .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.AmountAccounting), Original = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var salesByCurrency = byCurrency.OrderByDescending(x => x.Amount)
            .Select(x => new AmountRow(x.Currency, x.Currency, x.Amount, Currency: x.Currency, CurrencyAmount: x.Original)).ToList();

        var outputRows = await (from l in db.OneCDocumentLines.AsNoTracking()
                                join d in Documents(f) on l.DocumentId equals d.Id
                                where d.Type == OneCDocumentTypes.Production && l.ItemSourceId != null
                                group l by l.ItemSourceId into g
                                select new { Id = g.Key!, Quantity = g.Sum(x => x.Quantity), Cost = g.Sum(x => x.CostAmount ?? x.Amount) })
            .OrderByDescending(x => x.Cost).Take(15).ToListAsync(ct);
        var outputNames = await ItemsAsync(outputRows.Select(r => r.Id), ct);
        var productionRows = outputRows.Select(r => new AmountRow(r.Id, outputNames.TryGetValue(r.Id, out var n) ? n.Name : r.Id, r.Cost, Quantity: r.Quantity)).ToList();

        var month = await BalancesMonthAsync(f, ct);
        var inventory = new List<AmountRow>();
        var byWarehouse = new List<AmountRow>();
        if (month is { } bm)
        {
            var stock = (await BalancesOf(f, bm).Where(b => b.Account.StartsWith("10") || b.Account.StartsWith("2"))
                    .Select(b => new { b.Account, b.WarehouseId, Amount = b.ClosingDebit - b.ClosingCredit }).ToListAsync(ct))
                .Where(b => FinanceChart.Is(b.Account, FinanceChart.Inventory)).ToList();
            inventory = stock.GroupBy(s => FinanceChart.InventoryGroup(s.Account)).Select(g => new AmountRow(g.Key, g.Key, g.Sum(s => s.Amount)))
                .Where(r => r.Amount != 0).OrderByDescending(r => r.Amount).ToList();
            var warehouseNames = await ReferenceNamesAsync(stock.Select(s => s.WarehouseId), ct);
            byWarehouse = stock.GroupBy(s => s.WarehouseId).Select(g => new AmountRow(g.Key ?? "-", g.Key is null ? "Без склада" : warehouseNames.GetValueOrDefault(g.Key, g.Key), g.Sum(s => s.Amount)))
                .Where(r => r.Amount != 0).OrderByDescending(r => r.Amount).ToList();
        }

        var rateRows = await db.OneCExchangeRates.AsNoTracking().Where(r => !r.IsDeleted && r.Date >= f.From.AddDays(-10) && r.Date <= f.To)
            .Select(r => new { r.Currency, r.Date, r.Rate, r.Multiplier, r.RateSource }).ToListAsync(ct);
        var rates = rateRows.GroupBy(r => r.Currency).Select(g =>
        {
            var first = g.Where(r => r.Date <= f.From).MaxBy(r => r.Date) ?? g.MinBy(r => r.Date)!;
            var last = g.MaxBy(r => r.Date)!;
            var a = first.Rate / first.Multiplier;
            var b = last.Rate / last.Multiplier;
            return new RateRow(g.Key, Math.Round(a, 4), Math.Round(b, 4), last.Date, a == 0 ? null : Math.Round((b - a) / a * 100, 2), last.RateSource);
        }).OrderBy(r => r.Currency).ToList();

        return new FinanceOperations(meta, months, sales, purchases, cost, productionCosts, topCustomers, topSuppliers, topItems, salesByCurrency,
            productionRows, byWarehouse, inventory, unitCosts, rates, month);
    }

    // ---------- Отклонения ----------

    /// <summary>
    /// Отклонения и подозрительные изменения за период: рост расходов к прошлому периоду, крупные разовые проводки, исправления документов
    /// закрытых месяцев, возможные двойные платежи, новые контрагенты с крупными суммами. Это сигналы для проверки, а не выводы.
    /// </summary>
    public async Task<FinanceAnomalies> AnomaliesAsync(FinanceFilter f, CancellationToken ct)
    {
        var meta = await MetaAsync(f, ct);
        var items = new List<Anomaly>();
        var checks = new List<string>();

        checks.Add("Рост расходов по счетам больше чем на 30 % и на 10 млн к прошлому периоду той же длины");
        foreach (var row in await ExpenseChangesAsync(f, f.Previous(), 50, ct))
        {
            var before = row.Previous ?? 0;
            var delta = row.Amount - before;
            if (delta > 10_000_000m && (before == 0 || delta / before > 0.3m))
            {
                items.Add(new Anomaly("expense_growth", $"Расходы по счёту {row.Key} выросли", $"{row.Title}: {before:N0} → {row.Amount:N0}" + (before == 0 ? "" : $" (+{delta / before * 100:N0} %)"),
                    delta, null, $"/finance/accounts/{row.Key}", delta > 100_000_000m ? 3 : 2));
            }
        }

        checks.Add("Разовые проводки крупнее обычных: больше среднего по счёту + 4 стандартных отклонения и в 1,5 раза больше самой крупной такой же проводки (та же пара счетов) за 3 предыдущих месяца");
        var stats = await Entries(f).GroupBy(e => e.DebitAccount).Where(g => g.Count() >= 30)
            .Select(g => new { Account = g.Key, Avg = g.Average(x => (double)x.Amount), Sq = g.Average(x => (double)x.Amount * (double)x.Amount), Max = g.Max(x => x.Amount) }).ToListAsync(ct);
        foreach (var s in stats)
        {
            // Статистика — в double: квадраты сумм в numeric вышли бы за 28 знаков decimal.
            var sd = Math.Sqrt(Math.Max(0, s.Sq - s.Avg * s.Avg));
            var limit = (decimal)(s.Avg + 4 * sd);
            if (sd == 0 || s.Max <= limit)
            {
                continue;
            }

            var big = await Entries(f).Where(e => e.DebitAccount == s.Account && e.Amount > limit).OrderByDescending(e => e.Amount).Take(3).ToListAsync(ct);
            foreach (var e in big)
            {
                // Регулярные крупные проводки (начисление зарплаты, закрытие месяца) не сигнал: сравниваем с той же парой счетов за 3 месяца.
                var history = f with { From = f.From.AddMonths(-3), To = f.From.AddDays(-1) };
                var usual = await Entries(history).Where(x => x.DebitAccount == e.DebitAccount && x.CreditAccount == e.CreditAccount).MaxAsync(x => (decimal?)x.Amount, ct);
                if (usual is { } max && e.Amount <= max * 1.5m)
                {
                    continue;
                }

                items.Add(new Anomaly("large_entry", $"Крупная проводка Дт {e.DebitAccount} Кт {e.CreditAccount}",
                    $"{e.Amount:N0} при обычной {(decimal)s.Avg:N0}; {e.RegistrarSourceType ?? e.RegistrarType} № {e.RegistrarNumber}" + (e.Content is null ? "" : $" — {e.Content}"),
                    e.Amount, e.PeriodDate, $"/finance/entries?registrar={Uri.EscapeDataString(e.RegistrarSourceId)}", 2));
            }
        }

        checks.Add("Исправления, перепроведение и отмена документов прошлых месяцев (по журналу изменений за период)");
        var start = OneCFormat.DayStart(f.From);
        var end = OneCFormat.DayStart(f.To.AddDays(1));
        var changes = await db.OneCChanges.AsNoTracking().Where(c => c.At >= start && c.At < end && (c.EntityType == OneCEntityTypes.Document || c.EntityType == OneCEntityTypes.Entry)
                && c.Action != "created").OrderByDescending(c => c.At).Take(500).ToListAsync(ct);
        var changedIds = changes.Select(c => c.SourceEntityId).Distinct().ToList();
        var changedDocs = await db.OneCDocuments.AsNoTracking().Where(d => changedIds.Contains(d.SourceEntityId) && (f.OrganizationId == null || d.SourceOrganizationId == f.OrganizationId))
            .Select(d => new { d.Id, d.SourceEntityId, d.DocumentDate, d.Number, d.SourceType, d.AmountAccounting }).ToListAsync(ct);
        foreach (var c in changes.GroupBy(c => c.SourceEntityId))
        {
            var doc = changedDocs.FirstOrDefault(d => d.SourceEntityId == c.Key);
            var change = c.First();
            if (doc is null || OneCFormat.MonthStart(doc.DocumentDate) >= OneCFormat.MonthStart(OneCFormat.LocalDate(change.At)))
            {
                continue;
            }

            var action = change.Action switch { "cancelled" => "отменено проведение", "deleted" => "удалён", "reposted" => "перепроведён", _ => "изменён" };
            items.Add(new Anomaly("closed_period_change", $"Документ прошлого месяца {action}",
                $"{doc.SourceType} № {doc.Number} от {doc.DocumentDate:dd.MM.yyyy} — {action} {change.At.ToOffset(OneCFormat.Offset):dd.MM.yyyy HH:mm}",
                doc.AmountAccounting, doc.DocumentDate, $"/finance/documents/{doc.Id}", change.Action is "cancelled" or "deleted" ? 3 : 2));
        }

        checks.Add("Возможные двойные платежи: одинаковая сумма тому же контрагенту в течение 3 дней");
        var payments = await Documents(f).Where(d => (d.Type == OneCDocumentTypes.BankOut || d.Type == OneCDocumentTypes.CashOut) && d.CounterpartySourceId != null)
            .Select(d => new { d.Id, d.CounterpartySourceId, d.AmountAccounting, d.DocumentDate, d.Number }).ToListAsync(ct);
        var names = await CounterpartiesAsync(payments.Select(p => p.CounterpartySourceId), ct);
        foreach (var group in payments.GroupBy(p => (p.CounterpartySourceId, p.AmountAccounting)).Where(g => g.Count() > 1))
        {
            var ordered = group.OrderBy(p => p.DocumentDate).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].DocumentDate.DayNumber - ordered[i - 1].DocumentDate.DayNumber <= 3)
                {
                    var name = names.TryGetValue(group.Key.CounterpartySourceId!, out var n) ? n.Name : group.Key.CounterpartySourceId;
                    items.Add(new Anomaly("duplicate_payment", $"Возможный двойной платёж: {name}",
                        $"№ {ordered[i - 1].Number} от {ordered[i - 1].DocumentDate:dd.MM} и № {ordered[i].Number} от {ordered[i].DocumentDate:dd.MM} — по {group.Key.AmountAccounting:N0}",
                        group.Key.AmountAccounting, ordered[i].DocumentDate, $"/finance/documents/{ordered[i].Id}", 2));
                    break;
                }
            }
        }

        checks.Add("Новые контрагенты периода с оплатами или поступлениями больше 50 млн");
        var firstSeen = await db.OneCDocuments.AsNoTracking().Where(d => !d.IsDeleted && d.Posted && d.CounterpartySourceId != null && (f.OrganizationId == null || d.SourceOrganizationId == f.OrganizationId))
            .GroupBy(d => d.CounterpartySourceId).Select(g => new { Id = g.Key!, First = g.Min(x => x.DocumentDate) })
            .Where(x => x.First >= f.From && x.First <= f.To).ToListAsync(ct);
        var newIds = firstSeen.Select(x => x.Id).ToList();
        var newTotals = await Documents(f).Where(d => newIds.Contains(d.CounterpartySourceId!)).GroupBy(d => d.CounterpartySourceId)
            .Select(g => new { Id = g.Key!, Amount = g.Sum(x => x.AmountAccounting) }).Where(x => x.Amount > 50_000_000m).ToListAsync(ct);
        var newNames = await CounterpartiesAsync(newTotals.Select(x => x.Id), ct);
        foreach (var x in newTotals)
        {
            items.Add(new Anomaly("new_counterparty", $"Новый контрагент с крупными суммами: {(newNames.TryGetValue(x.Id, out var n) ? n.Name : x.Id)}",
                $"Первый документ — {firstSeen.First(s => s.Id == x.Id).First:dd.MM.yyyy}, сумма документов за период {x.Amount:N0}", x.Amount,
                firstSeen.First(s => s.Id == x.Id).First, $"/finance/entries?counterparty={Uri.EscapeDataString(x.Id)}", 1));
        }

        return new FinanceAnomalies(meta, items.OrderByDescending(i => i.Severity).ThenByDescending(i => i.Amount ?? 0).Take(50).ToList(), checks);
    }
}

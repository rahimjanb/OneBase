using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Application.Abstractions;
using OneBase.Application.DataSources;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Sales.Primary;

namespace OneBase.Application.Sales.Settlements;

/// <summary>
/// Книги контрагентов на дату и откуда данные: первичка Linko, реестры Google Sheets (последняя синхронизация). ActGroups — группы акта
/// бухгалтерии по порядку листа («» — строки до первого заголовка).
/// </summary>
public sealed record SettlementsBase(
    DateOnly AsOf,
    IReadOnlyList<DealerBook> Books,
    bool HasPayments,
    bool HasExpenses,
    bool HasBalances,
    bool HasTerms,
    DateOnly? PaymentsFrom,
    DateOnly? PaymentsTo,
    DateOnly? ExpensesTo,
    IReadOnlyList<string>? ActGroups = null,
    DateOnly? Today = null,
    string ShipmentsSource = SettlementsBase.Linko)
{
    public const string Linko = "linko";

    /// <summary>Отгрузки — из таблицы ГП (привязка «Отгрузки дилерам (ГП)»).</summary>
    public const string Sheets = "sheets";
}

/// <summary>
/// Взаиморасчёты с дилерами для «Акта сверки» и DSO: отгрузки и возвраты — таблица ГП («Сотув маълумотлар Базаси», сумма дилера), если привязано
/// поле «Отгрузки дилерам (ГП)», иначе первичка OneBase из Linko (перемещения завод → склады дилеров и заказы прямых клиентов, сумма Linko);
/// оплаты и расходы завода на дилера — реестры Google Sheets («Kassa Bank», «Прочие расходы»),
/// опорный остаток — акт сверки бухгалтерии (или «Ostatka»), норма оплаты — «Ostatka». Контрагенты сводятся по имени (дилер региона
/// OneBase = контрагент таблиц).
/// </summary>
public sealed class SettlementsService(IAppDbContext db, PrimaryService primary, IMemoryCache cache, SalesCacheSignal signal, TimeProvider time)
{
    public Task<SettlementsBase> BaseAsync(CancellationToken ct = default) =>
        SalesViewCache.GetAsync(cache, signal, "sales:settlements:base", () => BuildAsync(ct));

    private async Task<SettlementsBase> BuildAsync(CancellationToken ct)
    {
        var (rows, parties, primaryAsOf) = await primary.LedgerAsync(ct);
        var ledger = await db.SalesDealerLedger.AsNoTracking().ToListAsync(ct);
        var terms = await db.SalesDealerTerms.AsNoTracking().ToListAsync(ct);
        var today = DateOnly.FromDateTime(time.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).DateTime);
        var payments = ledger.Where(e => e.Field == DataSourceCatalog.DealerPayments).ToList();
        var expenses = ledger.Where(e => e.Field == DataSourceCatalog.DealerExpenses).ToList();
        var balances = ledger.Where(e => e.Field == DataSourceCatalog.DealerBalances).ToList();
        // Отгрузки ГП привязаны — акт и DSO считаются по ним, как у бухгалтерии; иначе — первичка Linko.
        var sheetShipments = ledger.Where(e => e.Field == DataSourceCatalog.DealerShipments).ToList();
        var fromSheets = sheetShipments.Count > 0;
        var shipmentsAsOf = fromSheets ? sheetShipments.Max(e => e.Date) : primaryAsOf;
        var asOf = new[] { shipmentsAsOf, payments.Count > 0 ? payments.Max(p => p.Date) : null }.Where(d => d is not null).Select(d => d!.Value).DefaultIfEmpty(today).Max();
        if (asOf > today)
        {
            asOf = today;
        }

        var names = new Dictionary<string, (string Name, string Group)>();
        var docsByKey = new Dictionary<string, List<SettlementDoc>>();
        void AddDoc(string key, string name, string group, SettlementDoc doc)
        {
            names.TryAdd(key, (name, group));
            if (!docsByKey.TryGetValue(key, out var list))
            {
                docsByKey[key] = list = [];
            }

            list.Add(doc);
        }

        if (fromSheets)
        {
            // Строки ГП → документ дня: контрагент × дата × отгрузка или возврат (номеров документов в таблице нет). Группа — как у первички
            // (дилер региона или прямой клиент Linko с тем же именем), иначе прямой клиент.
            var groupByKey = parties.Values.GroupBy(p => PnlMath.DealerKey(p.Name)).ToDictionary(g => g.Key, g => g.First().Group);
            foreach (var g in sheetShipments.Where(e => e.Date <= asOf).GroupBy(e => (e.DealerKey, e.Date, Return: e.Amount < 0)))
            {
                var amount = Math.Abs(g.Sum(e => e.Amount));
                if (amount == 0)
                {
                    continue;
                }

                var group = groupByKey.GetValueOrDefault(g.Key.DealerKey) == PrimaryGroups.Dealer ? "dealer" : "direct";
                AddDoc(g.Key.DealerKey, g.First().Dealer, group, new SettlementDoc(g.Key.Date, amount, $"ГП {g.Key.Date:dd.MM.yyyy}", g.Key.Return));
            }
        }
        else
        {
            // Документы первички по контрагенту: строки товаров → документ (отгрузка или возврат), сумма Linko.
            var docs = rows
                .Where(r => r.Date <= asOf)
                .GroupBy(r => (r.Counterparty, r.Doc, r.Date, r.IsReturn))
                .Select(g => (g.Key.Counterparty, Doc: new SettlementDoc(g.Key.Date, Math.Abs(g.Sum(r => r.Amounts.SumFactory)), g.Key.Doc, g.Key.IsReturn)))
                .Where(x => x.Doc.Amount != 0)
                .ToList();
            foreach (var (party, doc) in docs)
            {
                var info = parties.GetValueOrDefault(party);
                var name = info?.Name ?? party;
                AddDoc(PnlMath.DealerKey(name), PnlMath.DealerName(name), info?.Group == PrimaryGroups.Dealer ? "dealer" : "direct", doc);
            }
        }

        foreach (var e in ledger.Where(e => e.Field != DataSourceCatalog.DealerShipments))
        {
            names.TryAdd(e.DealerKey, (e.Dealer, "other"));
        }

        foreach (var t in terms)
        {
            // Контрагенты с условиями оплаты («Ostatka») — дилеры (и прямые клиенты на условиях дилера: «Мурод бозор/Д63»).
            names[t.DealerKey] = (names.TryGetValue(t.DealerKey, out var known) ? known.Name : t.Dealer, "dealer");
        }

        // Акт бухгалтерии на последнюю дату: строки по порядку листа; имя может стоять в двух группах («накт пул бозор» в «Ритейл» и «Долги») —
        // остатки складываются, группа — строки с большим остатком (последней при равных).
        var actDate = balances.Where(b => b.Date <= asOf).Select(b => (DateOnly?)b.Date).Max();
        var act = balances.Where(b => b.Date == actDate).OrderBy(b => SheetRow(b.SourceRef)).ToList();
        var actGroups = act.Select(b => b.Category ?? "").Distinct().ToList();
        var actBooks = act.GroupBy(b => b.DealerKey).ToDictionary(g => g.Key, g => (
            Amount: g.Sum(b => b.Amount),
            Group: g.Aggregate((best, b) => Math.Abs(b.Amount) >= Math.Abs(best.Amount) ? b : best).Category ?? ""));

        var books = names.Select(x =>
            {
                var key = x.Key;
                var term = terms.FirstOrDefault(t => t.DealerKey == key);
                var balance = actBooks.TryGetValue(key, out var inAct) ? inAct : ((decimal Amount, string Group)?)null;
                DateOnly? anchorDate = balance is not null ? actDate : term?.OpeningDate;
                decimal? anchorAmount = balance?.Amount ?? (term?.OpeningDate is not null ? term.Opening : null);
                return new DealerBook(
                    key,
                    x.Value.Name,
                    x.Value.Group,
                    term?.NormDays,
                    term is not null,
                    anchorAmount is null ? null : anchorDate,
                    anchorAmount,
                    balance is not null ? "act" : anchorAmount is not null ? "terms" : null,
                    (docsByKey.GetValueOrDefault(key) ?? []).OrderBy(d => d.Date).ToList(),
                    payments.Where(p => p.DealerKey == key).OrderBy(p => p.Date)
                        .Select(p => new SettlementPayment(p.Date, p.Amount, SettlementMath.IsCash(p.Kind), p.Comment ?? p.Kind)).ToList(),
                    expenses.Where(p => p.DealerKey == key).OrderBy(p => p.Date)
                        .Select(p => new SettlementExpense(p.Date, p.Amount, SettlementMath.CodeOf(p.Kind), SettlementMath.ArtOf(SettlementMath.CodeOf(p.Kind)), p.Comment)).ToList(),
                    balance?.Group);
            })
            .ToList();

        return new SettlementsBase(asOf, books, payments.Count > 0, expenses.Count > 0, actDate is not null, terms.Count > 0,
            payments.Count > 0 ? payments.Min(p => p.Date) : null, payments.Count > 0 ? payments.Max(p => p.Date) : null,
            expenses.Count > 0 ? expenses.Max(p => p.Date) : null, actGroups, today, fromSheets ? SettlementsBase.Sheets : SettlementsBase.Linko);
    }

    /// <summary>Номер строки листа из «АКТ-СВЕРКА!E61» (порядок строк акта).</summary>
    private static int SheetRow(string? sourceRef)
    {
        var digits = sourceRef is null ? "" : new string(sourceRef.Reverse().TakeWhile(char.IsAsciiDigit).Reverse().ToArray());
        return int.TryParse(digits, out var row) ? row : int.MaxValue;
    }
}

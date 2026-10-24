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
    DateOnly? Today = null);

/// <summary>
/// Взаиморасчёты с дилерами для «Акта сверки» и DSO: отгрузки и возвраты — первичка OneBase из Linko (перемещения завод → склады дилеров и
/// заказы прямых клиентов, сумма Linko), оплаты и расходы завода на дилера — реестры Google Sheets («Kassa Bank», «Прочие расходы»),
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
        var asOf = new[] { primaryAsOf, payments.Count > 0 ? payments.Max(p => p.Date) : null }.Where(d => d is not null).Select(d => d!.Value).DefaultIfEmpty(today).Max();
        if (asOf > today)
        {
            asOf = today;
        }

        // Документы первички по контрагенту: строки товаров → документ (отгрузка или возврат), сумма Linko.
        var docs = rows
            .Where(r => r.Date <= asOf)
            .GroupBy(r => (r.Counterparty, r.Doc, r.Date, r.IsReturn))
            .Select(g => (g.Key.Counterparty, Doc: new SettlementDoc(g.Key.Date, Math.Abs(g.Sum(r => r.Amounts.SumFactory)), g.Key.Doc, g.Key.IsReturn)))
            .Where(x => x.Doc.Amount != 0)
            .ToList();

        var names = new Dictionary<string, (string Name, string Group)>();
        var docsByKey = new Dictionary<string, List<SettlementDoc>>();
        foreach (var (party, doc) in docs)
        {
            var info = parties.GetValueOrDefault(party);
            var name = info?.Name ?? party;
            var key = PnlMath.DealerKey(name);
            names.TryAdd(key, (PnlMath.DealerName(name), info?.Group == PrimaryGroups.Dealer ? "dealer" : "direct"));
            if (!docsByKey.TryGetValue(key, out var list))
            {
                docsByKey[key] = list = [];
            }

            list.Add(doc);
        }

        foreach (var e in ledger)
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
            expenses.Count > 0 ? expenses.Max(p => p.Date) : null, actGroups, today);
    }

    /// <summary>Номер строки листа из «АКТ-СВЕРКА!E61» (порядок строк акта).</summary>
    private static int SheetRow(string? sourceRef)
    {
        var digits = sourceRef is null ? "" : new string(sourceRef.Reverse().TakeWhile(char.IsAsciiDigit).Reverse().ToArray());
        return int.TryParse(digits, out var row) ? row : int.MaxValue;
    }
}

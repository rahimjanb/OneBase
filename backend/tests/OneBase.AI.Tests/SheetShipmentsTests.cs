using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.DataSources;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Settlements;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Акт сверки и DSO: отгрузки ГП («Отгрузки дилерам (ГП)») заменяют первичку Linko; без них — Linko, как раньше.</summary>
public class SheetShipmentsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static SalesDealerLedgerEntry Entry(string field, DateOnly date, string dealer, decimal amount, string? kind = null) => new()
    {
        Field = field,
        Date = date,
        Dealer = dealer,
        DealerKey = dealer.ToLowerInvariant(),
        Amount = amount,
        Kind = kind,
        SourceRef = "test",
    };

    [PostgresFact]
    public async Task Settlements_use_sheet_shipments_when_they_are_bound_and_linko_otherwise()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        await db.SalesDealerLedger.ExecuteDeleteAsync();
        db.SalesDealerLedger.Add(Entry(DataSourceCatalog.DealerPayments, new DateOnly(2026, 9, 20), "Дилер Хоразм", 300, "Банк"));
        await db.SaveChangesAsync();

        // Без отгрузок ГП — первичка Linko.
        var linko = await sp.GetRequiredService<SettlementsService>().BaseAsync();
        Assert.Equal(SettlementsBase.Linko, linko.ShipmentsSource);

        db.SalesDealerLedger.AddRange(
            Entry(DataSourceCatalog.DealerShipments, new DateOnly(2026, 9, 1), "Дилер Хоразм", 1000, "Продажа"),
            Entry(DataSourceCatalog.DealerShipments, new DateOnly(2026, 9, 1), "Дилер Хоразм", 500, "Продажа"),
            Entry(DataSourceCatalog.DealerShipments, new DateOnly(2026, 9, 5), "Дилер Хоразм", -200, "Возврат"));
        await db.SaveChangesAsync();
        sp.GetRequiredService<SalesCacheSignal>().Invalidate();

        var sheets = await sp.GetRequiredService<SettlementsService>().BaseAsync();
        Assert.Equal(SettlementsBase.Sheets, sheets.ShipmentsSource);
        var book = sheets.Books.Single(b => b.Key == "дилер хоразм");
        // Строки дня складываются в документ: 1500 отгрузки 01.09 и 200 возврата 05.09; оплаты — из Kassa Bank, как раньше.
        Assert.Equal([(new DateOnly(2026, 9, 1), 1500m, false), (new DateOnly(2026, 9, 5), 200m, true)], book.Docs.Select(d => (d.Date, d.Amount, d.Return)));
        Assert.Equal(300m, Assert.Single(book.Payments).Amount);
        Assert.DoesNotContain(sheets.Books, b => b.Docs.Any(d => !d.Doc.StartsWith("ГП"))); // документов Linko нет

        var akt = AktCompute.Build(sheets, "2026-9");
        Assert.Equal(SettlementsBase.Sheets, akt.ShipmentsSource);
        Assert.Equal(SettlementsBase.Sheets, DsoCompute.Build(sheets).ShipmentsSource);
    }
}

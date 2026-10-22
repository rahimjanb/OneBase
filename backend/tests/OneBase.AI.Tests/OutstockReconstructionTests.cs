using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Stock;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Остаток дилера по дням назад от снимка (аутсток и поправка скорости «Рек. остатка») на PostgreSQL. Как в сверке с «Полевым
/// контролем» (Хоразм, товар 039): возврат магазина после месяца и приход, принятый на день позже выдачи, скрывали дни без товара.
/// Учитываются: возвраты магазинов дилеру, приход в день приёмки, перемещения дилера другому дилеру.
/// </summary>
public class OutstockReconstructionTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const long Base = 8_100_000;

    [PostgresFact]
    public async Task Store_returns_receipts_on_acceptance_and_dealer_transfers_reveal_days_without_stock()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var states = await db.LinkoSyncStates.Where(s => s.Entity == "product_balances" || s.Entity == "orders").ToListAsync();
        var saved = states.ToDictionary(s => s.Entity, s => s.LastSuccessAt);
        try
        {
            // Снимок 2 октября 10:00 по Ташкенту: у дилера 20 шт. (10 кг) товара по 0,5 кг.
            var snapshot = new DateTimeOffset(2026, 10, 2, 5, 0, 0, TimeSpan.Zero);
            foreach (var entity in new[] { "product_balances", "orders" })
            {
                var state = states.FirstOrDefault(s => s.Entity == entity);
                if (state is null)
                {
                    db.LinkoSyncStates.Add(new LinkoSyncState { Entity = entity, LastSuccessAt = snapshot });
                }
                else
                {
                    state.LastSuccessAt = snapshot;
                }
            }

            db.SalesRegions.Add(new SalesRegion { Name = "Сверкаобод", LinkoBranchId = 7001 });
            db.SalesRegions.Add(new SalesRegion { Name = "Соседобод", LinkoBranchId = 7002 });
            db.LinkoStocks.AddRange(
                new LinkoStock { Id = Base + 1, Name = "Сверкаобод" },
                new LinkoStock { Id = Base + 2, Name = "Соседобод" },
                new LinkoStock { Id = Base + 3, Name = "Завод" });
            db.LinkoProducts.Add(new LinkoProduct { Id = Base + 10, Name = "039 Шоколад проверочный", Code = "T-039" });
            db.LinkoProductBalances.Add(new LinkoProductBalance { ProductId = Base + 10, StockId = Base + 1, Balance = 20 });

            // Продажи магазинам: 1.09 — 5 кг, 1.10 — 2 кг; 1.10 магазин вернул 2 кг.
            Order(db, DataManagerDb.FakeLinkoIds + 81, new DateTime(2026, 9, 1, 10, 0, 0), 10, 5);
            Order(db, DataManagerDb.FakeLinkoIds + 82, new DateTime(2026, 10, 1, 12, 0, 0), 4, 2);
            db.LinkoOrderReturns.Add(new LinkoOrderReturn
            {
                Id = Base + 20, CreatedDate = new DateOnly(2026, 10, 1), Status = "delivered", BranchId = 7001, BranchName = "Сверкаобод", TotalWeight = 2,
                Lines = [new LinkoOrderReturnLine { Id = Base + 21, ReturnId = Base + 20, ProductId = Base + 10, Amount = 4, TotalWeight = 2 }],
            });

            // Завод → дилер: выдано 20.09 в 15:00, принято 21.09 в 10:00 — 10 кг. Дилер → соседний дилер: 10.09 — 3 кг.
            Transfer(db, Base + 30, Base + 3, Base + 1, new DateTime(2026, 9, 20, 15, 0, 0), new DateTime(2026, 9, 21, 10, 0, 0), 20, 10);
            Transfer(db, Base + 31, Base + 1, Base + 2, new DateTime(2026, 9, 10, 9, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0), 6, 3);
            await db.SaveChangesAsync();
            sp.GetRequiredService<SalesCacheSignal>().Invalidate();

            var outs = await sp.GetRequiredService<OutstockService>().BaseAsync(2026, 9);
            var pair = Assert.Single(outs.Pairs, p => p.ProductId == Base + 10);
            // Назад от 10 кг: 1.10 продали 2 и вернули 2 → 10; 21.09 приняли 10 → утром 0; 10.09 отдали соседу 3 → утром 3; 1.09 продали 5 → 8.
            // Без товара — с 11.09 по 21.09: 11 дней. Раньше (без возврата, с приходом в день выдачи и без перемещения) — ни одного.
            Assert.Equal(11, pair.ZeroDays);
            Assert.Equal("111111111100000000000111111111", pair.Days);
        }
        finally
        {
            await db.LinkoStockTransferLines.Where(l => l.TransferId >= Base && l.TransferId < Base + 100).ExecuteDeleteAsync();
            await db.LinkoStockTransfers.Where(t => t.Id >= Base && t.Id < Base + 100).ExecuteDeleteAsync();
            await db.LinkoOrderReturnLines.Where(l => l.ReturnId >= Base && l.ReturnId < Base + 100).ExecuteDeleteAsync();
            await db.LinkoOrderReturns.Where(r => r.Id >= Base && r.Id < Base + 100).ExecuteDeleteAsync();
            await db.LinkoProductBalances.Where(b => b.ProductId == Base + 10).ExecuteDeleteAsync();
            await db.LinkoProducts.Where(p => p.Id == Base + 10).ExecuteDeleteAsync();
            await db.LinkoStocks.Where(s => s.Id >= Base && s.Id < Base + 100).ExecuteDeleteAsync();
            foreach (var (entity, at) in saved)
            {
                await db.LinkoSyncStates.Where(s => s.Entity == entity).ExecuteUpdateAsync(u => u.SetProperty(s => s.LastSuccessAt, at));
            }

            await fixture.ResetAsync();
        }
    }

    private static void Order(OneBaseDbContext db, long id, DateTime accepted, decimal amount, decimal kg)
    {
        db.LinkoOrders.Add(new LinkoOrder
        {
            Id = id, CreatedAt = accepted.AddHours(-2), CreatedDate = DateOnly.FromDateTime(accepted), Status = "delivered", BranchId = 7001, BranchName = "Сверкаобод",
            AcceptedAt = accepted, AcceptedDate = DateOnly.FromDateTime(accepted), TotalWeight = kg, TotalPrice = kg * 10_000,
        });
        db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = Base + 10, Amount = amount, TotalWeight = kg, TotalPrice = kg * 10_000 });
    }

    private static void Transfer(OneBaseDbContext db, long id, long from, long to, DateTime given, DateTime accepted, decimal amount, decimal kg)
    {
        db.LinkoStockTransfers.Add(new LinkoStockTransfer
        {
            Id = id, Status = "accepted", FromStockId = from, ToStockId = to, CreatedAt = given.AddDays(-1), CreatedDate = DateOnly.FromDateTime(given.AddDays(-1)),
            GivenAt = given, AcceptedAt = accepted,
        });
        db.LinkoStockTransferLines.Add(new LinkoStockTransferLine { Id = id, TransferId = id, ProductId = Base + 10, Amount = amount, TotalWeight = kg });
    }
}

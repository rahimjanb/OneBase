using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Sales;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>Строка эталона: регион (индекс и название), день, товар (0 — А, 1 — Б), точка, кг, выручка.</summary>
public sealed record WorldLine(int Region, string RegionName, DateOnly Date, int Product, long Market, decimal Kg, decimal Revenue);

/// <summary>
/// Тестовый мир Advanced Intelligence с известными закономерностями — и эталон: те же строки в памяти, по которым ответы считаются
/// независимо от SQL движка.
/// <list type="bullet">
/// <item>Регионы «Хорезм» (филиал 971, группа-«лечение»), «Самарканд» (972), «Бухара» (973) — по 5 точек и одному агенту.</item>
/// <item>Товары: А (группа «ИИ-группа А», 20 000 сум/кг) и Б («ИИ-группа Б», 30 000 сум/кг, с 1 октября — 33 000).</item>
/// <item>1 июля — 31 октября 2026: кг в день = база × день недели (вс 0,5, сб 0,8) × шум ±2 %, поровну по точкам.</item>
/// <item>С 1 октября в «Хорезме» точка 5 перестала покупать, точки 1–4 берут 87,5 % прежнего — регион теряет 30 % объёма.</item>
/// </list>
/// </summary>
public sealed class IntelligenceWorld
{
    public const long BranchBase = 971;
    public const long TypeA = 9_200_001, TypeB = 9_200_002;
    public const long ProductA = 9_200_011, ProductB = 9_200_012;
    public const long AgentBase = 9_200_101;
    public const long MarketBase = 9_200_200;
    public const decimal PriceA = 20_000, PriceB = 30_000, PriceBOctober = 33_000;
    public static readonly DateOnly Start = new(2026, 7, 1), End = new(2026, 10, 31), Event = new(2026, 10, 1);
    public static readonly string GroupA = "ИИ-группа А", GroupB = "ИИ-группа Б";

    private static readonly decimal[,] Base = { { 100, 50 }, { 120, 60 }, { 80, 40 } };

    public required string Suffix { get; init; }
    public required string[] Regions { get; init; }
    public required IReadOnlyList<WorldLine> Lines { get; init; }

    public string Treated => Regions[0];

    public static long Market(int region, int m) => MarketBase + region * 10 + m;

    public static decimal Price(int product, DateOnly date) => product == 0 ? PriceA : date >= Event ? PriceBOctober : PriceB;

    /// <summary>Строки мира (детерминированно, без базы).</summary>
    public static IReadOnlyList<WorldLine> Generate(string[] regions)
    {
        var lines = new List<WorldLine>();
        for (var date = Start; date <= End; date = date.AddDays(1))
        {
            var wd = date.DayOfWeek switch { DayOfWeek.Sunday => 0.5m, DayOfWeek.Saturday => 0.8m, _ => 1m };
            for (var r = 0; r < 3; r++)
            {
                for (var p = 0; p < 2; p++)
                {
                    var rng = new SplitMix64((ulong)(date.DayNumber * 31 + r * 7 + p));
                    var noise = 1 + 0.04m * ((decimal)rng.NextDouble() - 0.5m);
                    var total = Base[r, p] * wd * noise;
                    for (var m = 1; m <= 5; m++)
                    {
                        var share = 0.2m;
                        if (r == 0 && date >= Event)
                        {
                            share = m == 5 ? 0 : 0.2m * 0.875m;
                        }

                        var kg = Math.Round(total * share, 2);
                        if (kg <= 0)
                        {
                            continue;
                        }

                        lines.Add(new WorldLine(r, regions[r], date, p, Market(r, m), kg, kg * Price(p, date)));
                    }
                }
            }
        }

        return lines;
    }

    /// <summary>Мир в базе фикстуры: старые тестовые заказы и регионы (филиалы ≥ 900) удаляются, мир записывается заново.</summary>
    public static Task<IntelligenceWorld> SeedAsync(DataManagerDb fixture) => SeedAsync(fixture.Services, fixture.ResetAsync);

    /// <summary>Мир в базе: reset убирает прежние тестовые заказы и регионы (филиалы ≥ 900).</summary>
    public static async Task<IntelligenceWorld> SeedAsync(IServiceProvider services, Func<Task> reset)
    {
        await reset();
        var s = Guid.NewGuid().ToString("N")[..5];
        string[] regions = [$"Хорезм {s}", $"Самарканд {s}", $"Бухара {s}"];
        var lines = Generate(regions);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        foreach (var (id, name) in new[] { (TypeA, GroupA), (TypeB, GroupB) })
        {
            if (!await db.LinkoProductTypes.AnyAsync(t => t.Id == id))
            {
                db.LinkoProductTypes.Add(new LinkoProductType { Id = id, Name = name });
            }
        }

        foreach (var (id, name, type) in new[] { (ProductA, "ИИ товар А", TypeA), (ProductB, "ИИ товар Б", TypeB) })
        {
            if (!await db.LinkoProducts.AnyAsync(p => p.Id == id))
            {
                db.LinkoProducts.Add(new LinkoProduct { Id = id, Name = name, Code = $"T{id}", TypeId = type });
            }
        }

        for (var r = 0; r < 3; r++)
        {
            db.SalesRegions.Add(new SalesRegion { LinkoBranchId = BranchBase + r, Name = regions[r] });
        }

        await db.SaveChangesAsync();
        var next = DataManagerDb.FakeLinkoIds + 700_000;
        foreach (var l in lines)
        {
            var id = next++;
            var product = l.Product == 0 ? ProductA : ProductB;
            var price = Price(l.Product, l.Date);
            db.LinkoOrders.Add(new LinkoOrder
            {
                Id = id, CreatedAt = l.Date.ToDateTime(new TimeOnly(8, 0)), CreatedDate = l.Date, AcceptedAt = l.Date.ToDateTime(new TimeOnly(9, 0)),
                AcceptedDate = l.Date, Status = "delivered", MarketId = l.Market, BranchId = BranchBase + l.Region, BranchName = l.RegionName,
                AgentId = AgentBase + l.Region, TotalWeight = l.Kg, TotalPrice = l.Revenue,
            });
            db.LinkoOrderLines.Add(new LinkoOrderLine { Id = id, OrderId = id, ProductId = product, Amount = l.Kg, Price = price, TotalPrice = l.Revenue, TotalWeight = l.Kg });
        }

        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<SalesCacheSignal>().InvalidateHistory();
        return new IntelligenceWorld { Suffix = s, Regions = regions, Lines = lines };
    }

    // ---------- Эталон ----------

    public IEnumerable<WorldLine> Between(DateOnly from, DateOnly to) => Lines.Where(l => l.Date >= from && l.Date <= to);

    public decimal Revenue(int region, DateOnly from, DateOnly to) => Between(from, to).Where(l => l.Region == region).Sum(l => l.Revenue);

    public decimal Kg(int region, DateOnly from, DateOnly to) => Between(from, to).Where(l => l.Region == region).Sum(l => l.Kg);

    /// <summary>Кг региона (или нескольких) по дням периода.</summary>
    public IReadOnlyList<double> DailyKg(IReadOnlyCollection<int> regions, DateOnly from, DateOnly to)
    {
        var days = new List<double>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            days.Add((double)Lines.Where(l => l.Date == d && regions.Contains(l.Region)).Sum(l => l.Kg));
        }

        return days;
    }

    /// <summary>Разность разностей по дневным кг: (лечение после − до) − (контроль после − до).</summary>
    public double DidKg(DateOnly from, DateOnly to)
    {
        var t = DailyKg([0], from, to);
        var c = DailyKg([1, 2], from, to);
        var idx = Event.DayNumber - from.DayNumber;
        return (t.Skip(idx).Average() - t.Take(idx).Average()) - (c.Skip(idx).Average() - c.Take(idx).Average());
    }
}

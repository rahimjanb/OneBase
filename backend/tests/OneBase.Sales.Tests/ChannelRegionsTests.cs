using Microsoft.Extensions.Configuration;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Sales;

namespace OneBase.Sales.Tests;

/// <summary>
/// Регионы-каналы (Sales:ChannelRegions): точки завода — базар и сети — из филиалов «Завод» и «К К Мерч» считаются во вторичке
/// только в закрытом месяце, в своём виртуальном филиале, без АКБ.
/// </summary>
public class ChannelRegionsTests
{
    private const long Havas = 26614, Murod = 26576, Bolajon = 69891, Export = 26777, Shop = 100;

    private static readonly SalesOptions Options = new()
    {
        ChannelRegions =
        [
            new SalesChannelRegion { Name = "Урикзор", Direction = "Базар", Markets = [Murod] },
            new SalesChannelRegion { Name = "Сети", Direction = "КА", Description = "Ключевые клиенты", Markets = [Havas, Bolajon] },
        ],
    };

    private static readonly DateOnly Sep1 = new(2026, 9, 1), Sep30 = new(2026, 9, 30);

    private static RawOrderLine Order(long id, string branch, long market, decimal kg, string status = "given", string accepted = "2026-09-11") =>
        new(id, status, new DateOnly(2026, 9, 10), null, DateOnly.Parse(accepted), 5, branch, 188, market, 500, 4, kg, kg * 1000);

    private static RawReturnLine Return(long id, string branch, long market, decimal kg) =>
        new(id, "delivered", new DateOnly(2026, 9, 12), 5, branch, 188, market, 500, 4, kg, kg * 1000);

    [Fact]
    public void Closed_month_moves_channel_orders_from_factory_branches_into_their_channel_region()
    {
        var result = SecondarySales.Build(
            [
                Order(1, "Завод", Havas, 100),
                Order(2, "К К Мерч", Bolajon, 20),
                Order(3, "Завод", Murod, 300, status: "delivered"),
                Order(4, "Завод", Export, 50),       // экспорт — по-прежнему вне вторички
                Order(5, "К К Мерч", 555, 7),        // не канал — «К К Мерч» во вторичку не входит
                Order(6, "Самарканд", Shop, 10),
            ],
            [Return(7, "Завод", Havas, 5)], [], Sep1, Sep30, Options);

        Assert.Equal(115m, result.Lines.Where(l => l.BranchId == SalesOptions.ChannelBranchId(1)).Sum(l => l.Kg)); // Сети: 100 + 20 − 5
        Assert.Equal(300m, result.Lines.Where(l => l.BranchId == SalesOptions.ChannelBranchId(0)).Sum(l => l.Kg)); // Урикзор
        Assert.Equal(10m, result.Lines.Where(l => l.BranchId == 5 && l.MarketId == Shop).Sum(l => l.Kg));
        Assert.Equal([4L], result.Excluded.Select(l => l.OrderId!.Value).ToArray());
        Assert.DoesNotContain(result.Lines, l => l.MarketId == 555);
    }

    [Fact]
    public void Running_month_keeps_channel_orders_out_of_the_secondary()
    {
        // Отчётный день 28.09 — месяц не закрыт: каналы не подключаются, «Завод» — в исключённых, «К К Мерч» — нигде.
        var result = SecondarySales.Build(
            [Order(1, "Завод", Havas, 100), Order(2, "К К Мерч", Bolajon, 20), Order(3, "Самарканд", Shop, 10)],
            [], [], Sep1, new DateOnly(2026, 9, 28), Options);

        Assert.Equal([Shop], result.Lines.Select(l => l.MarketId!.Value).ToArray());
        Assert.Equal([1L], result.Excluded.Select(l => l.OrderId!.Value).ToArray());
        Assert.False(SecondarySales.ChannelsOn(Sep1, new DateOnly(2026, 9, 28), Options));
        Assert.True(SecondarySales.ChannelsOn(Sep1, Sep30, Options));
    }

    [Fact]
    public void Channel_starts_from_its_first_month()
    {
        // «Урикзор» — с августа 2026 (как в «Полевом контроле»): июль закрыт, но канал ещё не начался — заказ «Завода» остаётся вне вторички.
        var options = new SalesOptions { ChannelRegions = [new SalesChannelRegion { Name = "Урикзор", Markets = [Murod], Since = "2026-08" }] };
        var july = SecondarySales.Build([Order(1, "Завод", Murod, 300, accepted: "2026-07-15")], [], [], new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), options);
        var august = SecondarySales.Build([Order(2, "Завод", Murod, 300, accepted: "2026-08-15")], [], [], new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), options);

        Assert.Empty(july.Lines);
        Assert.Equal(-1, Assert.Single(august.Lines).BranchId);
        Assert.Null(new SalesChannelRegion { Since = "август" }.SinceMonth()); // не «гггг-ММ» — без ограничения
    }

    [Fact]
    public void Channel_market_from_a_dealer_branch_stays_a_dealer_sale()
    {
        // Канал — только заказы из филиалов первички («Завод», «К К Мерч»); заказ той же точки у дилера остаётся в его регионе.
        var result = SecondarySales.Build([Order(1, "Ташкент", Havas, 40)], [], [], Sep1, Sep30, Options);

        Assert.Equal(5, Assert.Single(result.Lines).BranchId);
    }

    [Fact]
    public void Channel_points_are_not_counted_in_akb_or_sku_outlets()
    {
        SaleLine Line(long market, long branch, decimal kg) => new(Sep1, 1, market, branch, 4, 500, kg, kg * 1000, 1);
        var lines = new[] { Line(Shop, 5, 10), Line(Havas, SalesOptions.ChannelBranchId(1), 100), Line(Murod, SalesOptions.ChannelBranchId(0), 30) };

        Assert.Equal(1, SalesMath.Akb(lines));
        Assert.Equal(1, SalesMath.SkuTt(lines));
        Assert.Equal([Shop], SalesMath.PositiveSkus(lines).Keys.ToArray());
    }

    [Fact]
    public void Structure_adds_channel_regions_with_stable_ids_and_reuses_a_configured_direction()
    {
        var bazaar = new SalesDirection { Id = Guid.NewGuid(), Name = "базар", Kind = DirectionKind.Channel, SortOrder = 5 };
        var region = new SalesRegion { Id = Guid.NewGuid(), LinkoBranchId = 11, Name = "Термез" };

        var first = SalesStructure.Build([bazaar], [region], new HashSet<long>(), null, Options.ChannelRegions);
        var second = SalesStructure.Build([bazaar], [region], new HashSet<long>(), null, Options.ChannelRegions);

        var urikzor = Assert.Single(first.Regions, r => r.Name == "Урикзор");
        Assert.Equal(-1, urikzor.BranchId);
        Assert.Equal(bazaar.Id, urikzor.DirectionId); // «Базар» уже заведён в «Настройках продаж» — без учёта регистра
        var seti = Assert.Single(first.Regions, r => r.Name == "Сети");
        Assert.Equal(-2, seti.BranchId);
        var ka = Assert.Single(first.Directions, d => d.Id == seti.DirectionId);
        Assert.True(ka.IsChannel);
        Assert.Equal(("КА", "Ключевые клиенты"), (ka.Name, ka.Description));
        Assert.Equal(first.Regions.Select(r => r.Id), second.Regions.Select(r => r.Id)); // id не меняются между загрузками
        Assert.Equal(urikzor.Id, first.RegionOf(urikzor.Id));
    }

    [Fact]
    public void Channel_regions_bind_from_json_like_appsettings()
    {
        const string json = """
            { "Sales": { "ChannelRegions": [
              { "Name": "Урикзор", "Direction": "Базар", "Markets": [ 26782, 26576 ], "Since": "2026-08" },
              { "Name": "Сети", "Direction": "КА", "Markets": [ 26614 ] } ] } }
            """;
        var options = SalesOptionsBinding.Load(new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build());

        Assert.Equal(["Урикзор", "Сети"], options.ChannelRegions.Select(c => c.Name).ToArray());
        Assert.Equal((-1L, "Урикзор", (DateOnly?)new DateOnly(2026, 8, 1)), options.ChannelOf(26576));
        Assert.Equal((-2L, "Сети", (DateOnly?)null), options.ChannelOf(26614));
        Assert.Null(options.ChannelOf(1));
        Assert.Empty(new SalesOptions().ChannelRegions); // в коде каналов нет — только в настройках
    }
}

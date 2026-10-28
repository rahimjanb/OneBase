using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.DataSources;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Перенос настроек «Источников данных» файлом (рабочий компьютер → сервер): таблицы без ключей, привязки, дилеры регионов с другими именами;
/// план из таблицы находит регион и по другому имени дилера (второй дилер того же склада).
/// </summary>
public class DataSourceTransferDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Header = ["Год", "Месяц", "Контрагент", "КЕКС"];

    [PostgresFact]
    public async Task Settings_move_by_file_without_keys_and_the_plan_finds_the_region_by_another_dealer_name()
    {
        await fixture.ResetAsync();
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 17, 6, 0, 0, TimeSpan.Zero);
        // Хоразм: до октября — дилер «Дилер Хоразм», в октябре — второй дилер того же склада «Новый Ургенч»; Бухоро — по названию региона.
        string[][] rows = [["2026", "9", "Дилер Хоразм", "100"], ["2026", "10", "Новый Ургенч", "120"], ["2026", "10", "Бухоро", "50"]];
        fixture.Sheets.Document = new ReportDocument([new ReportSheet("План Первичка данный",
            new[] { Header }.Concat(rows).Select(r => (IReadOnlyList<string>)r).ToList(), 4, 4, false)]);

        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var sources = sp.GetRequiredService<DataSourceService>();
        var transfer = sp.GetRequiredService<DataSourceTransfer>();
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var khorazm = await db.SalesRegions.SingleAsync(r => r.Id == fixture.KhorazmId);
        khorazm.ChangeDealer("Дилер Хоразм");
        khorazm.SetDealerAliases(["Новый Ургенч"]);
        await db.SaveChangesAsync();

        var connection = await sources.CreateConnectionAsync(new ConnectionInput("План первички", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms", "link", null),
            fixture.AdminId, CancellationToken.None);
        var plan = new DataSourceFieldBinding(DataSourceCatalog.PlanPrimary,
            new PlanFieldMapping("Контрагент", null, null, [new PlanColumn("КЕКС", "Кекс")], "Месяц", "Год", null, null),
            Period: new PeriodFilter(DataSourceRules.PeriodYear));
        await sources.SaveBindingAsync(null, new BindingInput("primary", connection.Id, ["План Первичка данный"], 1, [plan], "interval", 60, null, true),
            fixture.AdminId, CancellationToken.None);

        // Экспорт: таблица без ключа, привязка, дилеры с другими именами.
        var file = await transfer.ExportAsync(CancellationToken.None);
        var json = JsonSerializer.Serialize(file, DataSourceRules.Json);
        Assert.DoesNotContain("ProtectedKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private_key", json);
        Assert.Equal(("link", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms"), (Assert.Single(file.Connections).Access, file.Connections[0].SpreadsheetId));
        Assert.Equal([DataSourceCatalog.PlanPrimary], Assert.Single(file.Bindings).Fields.Select(f => f.Field));
        var dealer = file.Dealers.Single(d => d.Region == "Хоразм");
        Assert.Equal("Дилер Хоразм", dealer.Dealer);
        Assert.Equal(["Новый Ургенч"], dealer.Aliases);
        var parsed = JsonSerializer.Deserialize<DataSourceExport>(json, DataSourceRules.Json)!; // файл читается обратно

        // «Сервер»: ни таблиц, ни привязок, ни дилеров.
        await db.DataSourceBindings.ExecuteDeleteAsync();
        await db.DataSourceConnections.ExecuteDeleteAsync();
        await db.SalesRegions.ExecuteUpdateAsync(s => s.SetProperty(r => r.DealerName, (string?)null).SetProperty(r => r.DealerAliases, (List<string>?)null));
        db.ChangeTracker.Clear(); // как новый запрос на сервере: регионы читаются из базы, а не из кэша контекста

        var dry = await transfer.ImportAsync(new DataSourceImportRequest(parsed, null, DryRun: true), fixture.AdminId, CancellationToken.None);
        Assert.Equal(0, dry.Errors);
        Assert.Contains(dry.Items, i => i.Kind == "connection" && i.Status == DataSourceTransfer.Created);
        Assert.Contains(dry.Items, i => i.Kind == "binding" && i.Status == DataSourceTransfer.Created);
        Assert.False(await db.DataSourceConnections.AnyAsync()); // проверка ничего не пишет
        Assert.Null((await db.SalesRegions.AsNoTracking().SingleAsync(r => r.Id == fixture.KhorazmId)).DealerName);

        var done = await transfer.ImportAsync(new DataSourceImportRequest(parsed, null, DryRun: false), fixture.AdminId, CancellationToken.None);
        Assert.Equal(0, done.Errors);
        var imported = await db.SalesRegions.AsNoTracking().SingleAsync(r => r.Id == fixture.KhorazmId);
        Assert.Equal("Дилер Хоразм", imported.DealerName);
        Assert.Equal(["Новый Ургенч"], imported.DealerAliases);
        var binding = await db.DataSourceBindings.AsNoTracking().SingleAsync();

        // Повтор: таблица и дилер — без изменений, привязка обновляется на месте, новой не появляется.
        var again = await transfer.ImportAsync(new DataSourceImportRequest(parsed, null, DryRun: false), fixture.AdminId, CancellationToken.None);
        Assert.Equal(DataSourceTransfer.Unchanged, again.Items.Single(i => i.Kind == "connection").Status);
        Assert.Equal(DataSourceTransfer.Unchanged, again.Items.Single(i => i.Kind == "dealer" && i.Name.StartsWith("Хоразм")).Status);
        Assert.Equal(DataSourceTransfer.Updated, again.Items.Single(i => i.Kind == "binding").Status);
        Assert.Equal(1, await db.DataSourceBindings.CountAsync());

        // Синхронизация: Хоразм — сентябрь по дилеру и октябрь по другому имени; Бухоро — по названию региона.
        var run = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", run.Status);
        var plans = await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary)
            .Select(p => new { p.RegionId, p.Month, p.PlanKg }).ToListAsync();
        Assert.Equal([(9, 100m), (10, 120m)], plans.Where(p => p.RegionId == fixture.KhorazmId).OrderBy(p => p.Month).Select(p => (p.Month, p.PlanKg)));
        Assert.Equal(50m, plans.Single(p => p.RegionId == fixture.BukharaId).PlanKg);

        // Закрытая таблица без ключа — строка с ошибкой, остальное идёт дальше; чужой файл — понятная ошибка; без права — отказ.
        var closed = parsed with
        {
            Connections = [parsed.Connections[0] with { SpreadsheetId = "1ZzClosedSheet0000000000000000000000000000", Access = "service_account" }],
            Bindings = [],
            Dealers = [],
        };
        var noKey = await transfer.ImportAsync(new DataSourceImportRequest(closed, null, DryRun: false), fixture.AdminId, CancellationToken.None);
        Assert.Contains("ключ JSON", Assert.Single(noKey.Items).Message);
        await Assert.ThrowsAsync<DataSourceException>(() =>
            transfer.ImportAsync(new DataSourceImportRequest(parsed with { Kind = "other" }, null, false), fixture.AdminId, CancellationToken.None));
        await Assert.ThrowsAsync<DataSourceException>(() =>
            transfer.ImportAsync(new DataSourceImportRequest(parsed, null, DryRun: false), fixture.ViewerId, CancellationToken.None));
    }
}

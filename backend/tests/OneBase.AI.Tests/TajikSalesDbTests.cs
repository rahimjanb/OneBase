using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneBase.Api.Auth;
using OneBase.Api.Controllers;
using OneBase.Application.Sales.Tajik;
using OneBase.Application.Security;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Tajik;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Подставной API 1С Таджикистан: ответы /sales и /sklad задаёт тест; запоминает адреса запросов.</summary>
public sealed class FakeTajikApi : HttpMessageHandler
{
    public Func<HttpRequestMessage, (HttpStatusCode Status, string Body)>? Sales { get; set; }
    public Func<HttpRequestMessage, (HttpStatusCode Status, string Body)>? Stock { get; set; }
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        Assert.Equal(HttpMethod.Get, request.Method);
        var handler = request.RequestUri!.AbsolutePath.EndsWith("/sklad") ? Stock : Sales;
        var (status, body) = handler?.Invoke(request) ?? (HttpStatusCode.NotFound, "{}");
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    public static string Ok(IEnumerable<object> items, int? count = null)
    {
        var list = items.ToList();
        return JsonSerializer.Serialize(new { status = "ok", count = count ?? list.Count, items = list });
    }

    public static object Sale(string number, string date, string customer, string manager, string code, string name, decimal qty, decimal price, decimal discount = 0) =>
        new { number, date, customer, manager, code, name, qty, price, amount = qty * price - discount, discount, discountPercent = discount == 0 ? 0 : Math.Round(discount / (qty * price) * 100, 2) };
}

/// <summary>
/// 1С Таджикистан → OneBase на тестовой PostgreSQL: загрузка продаж и остатка, изменённые и пропавшие накладные (не удаляются), защита от
/// неполного ответа, ключ не попадает наружу; «Экспорт → Таджикистан»: регионы по словам, ТП и вручную, категории по товару Linko, вес единицы.
/// </summary>
public sealed class TajikSalesDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const string Token = "tj-test-key-57";
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.FromHours(5));

    private const string Kulob = "Муким  стоянка Шариф Кулоб";
    private const string Shahrituz = "Дилафруз бозор Шахритус";
    private const string Market = "Нисо 2 праход Хочи Шариф";

    // Кекс (Linko 100, тип «Кекс»), бамбук 400 г × 10 (Linko 200, тип «Бамбук», цен Linko нет — штука), помадка без номера (категория по слову).
    private static object Cake(string n, string d, string store, decimal qty) => FakeTajikApi.Sale(n, d, store, "Хамроев Фарход", "НФ-1", "100 Кекс Нилло 1,5 кг", qty, 69.35m);
    private static object Stix(string n, string d, string store, decimal qty) => FakeTajikApi.Sale(n, d, store, "Хамроев Фарход", "НФ-2", "200.Бамбук STIX -400гр*10шт", qty, 11m);

    private static readonly object[] Stock =
    [
        new { code = "НФ-1", name = "100 Кекс Нилло 1,5 кг", warehouse = "Основной склад", qty = 30 },
        new { code = "НФ-3", name = "Помадка Ягода 4кг-Росип", warehouse = "Основной склад", qty = 5 },
    ];

    private T Get<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    private async Task<FakeTajikApi> ResetAsync()
    {
        fixture.Clock.Now = Now;
        using var scope = fixture.Services.CreateScope();
        var db = Get<OneBaseDbContext>(scope);
        await db.TajikSaleLines.ExecuteDeleteAsync();
        await db.TajikStockItems.ExecuteDeleteAsync();
        await db.TajikSyncRuns.ExecuteDeleteAsync();
        await db.TajikRegionAssignments.ExecuteDeleteAsync();
        await db.TajikRegions.Where(r => r.Name == "Душанбе").ExecuteDeleteAsync();
        await Get<ITajikSettingsStore>(scope).SaveAsync(new TajikSettingsChange("http://tj.test", Token, true), fixture.AdminId);
        return new FakeTajikApi();
    }

    private async Task<TajikSyncRun> SyncAsync(FakeTajikApi api, string trigger = TajikSyncService.Manual)
    {
        using var scope = fixture.Services.CreateScope();
        var service = new TajikSyncService(Get<OneBaseDbContext>(scope), Get<ITajikSettingsStore>(scope), new TajikApiClient(new HttpClient(api)),
            Get<TajikDataVersion>(scope), fixture.Clock, NullLogger<TajikSyncService>.Instance);
        return await service.RunAsync(trigger, default);
    }

    private async Task<List<TajikSaleLine>> LinesAsync()
    {
        using var scope = fixture.Services.CreateScope();
        return await Get<OneBaseDbContext>(scope).TajikSaleLines.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
    }

    [Fact]
    public async Task Sales_and_stock_load_and_changed_or_missing_documents_are_kept_as_history()
    {
        if (fixture.Services is null) return;
        var api = await ResetAsync();
        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(
        [
            // Октябрь — раньше окна: API с неверной датой так отвечает, строка не загружается.
            Cake("НФНФ-0", "15-10-2026", Kulob, 1),
            Cake("НФНФ-1", "03-11-2026", Kulob, 10),
            Stix("НФНФ-1", "03-11-2026", Kulob, 20),
            FakeTajikApi.Sale("НФНФ-2", "05-11-2026", Shahrituz, "Тагоев Яхё", "НФ-3", "Помадка Ягода 4кг-Росип", 2, 134.4m, discount: 8.8m),
            Cake("НФНФ-3", "20-11-2026", Market, 4),
        ]));
        api.Stock = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(Stock));

        var first = await SyncAsync(api);
        Assert.Equal(("ok", 3, 0, 0, 4, 2), (first.Status, first.Added, first.Changed, first.Removed, first.Lines, first.StockRows));
        Assert.Equal(new DateOnly(2026, 11, 1), first.From);
        // Первая загрузка — с даты начала истории; ключ — в запросе, дата — ДД-ММ-ГГГГ.
        Assert.Contains(api.Requests, u => u.AbsolutePath == "/sales" && u.Query.Contains("from=01-11-2026") && u.Query.Contains("token=" + Token));
        Assert.Equal(4, (await LinesAsync()).Count);

        // Вторая: в НФНФ-1 другое количество, НФНФ-2 без изменений, НФНФ-3 пропала, НФНФ-4 новая. Окно — с 1-го числа прошлого месяца, но не
        // раньше даты начала истории (01.11).
        api.Requests.Clear();
        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(
        [
            Cake("НФНФ-1", "03-11-2026", Kulob, 12),
            Stix("НФНФ-1", "03-11-2026", Kulob, 20),
            FakeTajikApi.Sale("НФНФ-2", "05-11-2026", Shahrituz, "Тагоев Яхё", "НФ-3", "Помадка Ягода 4кг-Росип", 2, 134.4m, discount: 8.8m),
            FakeTajikApi.Sale("НФНФ-4", "25-11-2026", Market, "Курбонов Ахмадчон", "НФ-1", "100 Кекс Нилло 1,5 кг", 6, 69.35m),
        ]));
        var second = await SyncAsync(api);
        Assert.Equal(("ok", 1, 1, 1), (second.Status, second.Added, second.Changed, second.Removed));
        Assert.Equal(new DateOnly(2026, 11, 1), second.From);
        Assert.Contains(api.Requests, u => u.Query.Contains("from=01-11-2026"));

        var lines = await LinesAsync();
        var active = lines.Where(l => l.RemovedAt == null).ToList();
        Assert.Equal(4, active.Count);
        Assert.Equal(12m, active.Single(l => l.DocNumber == "НФНФ-1" && l.ProductCode == "НФ-1").Qty);
        // Прежние строки изменённой и пропавшей накладных остались — с отметкой.
        Assert.Equal(3, lines.Count(l => l.RemovedAt != null));
        Assert.Contains(lines, l => l.DocNumber == "НФНФ-3" && l.RemovedAt != null);

        // Тот же ответ ещё раз — ничего не меняется (округление до точности столбцов не даёт ложных «изменений»).
        var same = await SyncAsync(api);
        Assert.Equal((0, 0, 0), (same.Added, same.Changed, same.Removed));

        // API отдаёт строки накладной в другом порядке (так бывает у настоящего API) — это не изменение.
        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(
        [
            FakeTajikApi.Sale("НФНФ-4", "25-11-2026", Market, "Курбонов Ахмадчон", "НФ-1", "100 Кекс Нилло 1,5 кг", 6, 69.35m),
            Stix("НФНФ-1", "03-11-2026", Kulob, 20),
            FakeTajikApi.Sale("НФНФ-2", "05-11-2026", Shahrituz, "Тагоев Яхё", "НФ-3", "Помадка Ягода 4кг-Росип", 2, 134.4m, discount: 8.8m),
            Cake("НФНФ-1", "03-11-2026", Kulob, 12),
        ]));
        var reordered = await SyncAsync(api);
        Assert.Equal((0, 0, 0), (reordered.Added, reordered.Changed, reordered.Removed));

        // Строка без даты не читается: пропавшие накладные при этом не помечаются.
        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(
        [
            Cake("НФНФ-1", "03-11-2026", Kulob, 12),
            Stix("НФНФ-1", "03-11-2026", Kulob, 20),
            new { number = "НФНФ-9", customer = Kulob, code = "НФ-1", qty = 1, amount = 1 },
        ]));
        var partialRead = await SyncAsync(api);
        Assert.Equal(("ok", 1, 0), (partialRead.Status, partialRead.Rejected, partialRead.Removed));
        Assert.Equal(4, (await LinesAsync()).Count(l => l.RemovedAt == null));
    }

    [Fact]
    public async Task Wrong_key_truncated_answer_and_stock_failure_change_nothing_and_never_show_the_key()
    {
        if (fixture.Services is null) return;
        var api = await ResetAsync();
        api.Sales = _ => (HttpStatusCode.Forbidden, """{"status":"error","error":"Access denied: invalid or missing token"}""");
        var denied = await SyncAsync(api);
        Assert.Equal("failed", denied.Status);
        Assert.Contains("не приняла ключ", denied.Error);
        Assert.DoesNotContain(Token, denied.Error);

        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok([Cake("НФНФ-1", "03-11-2026", Kulob, 1)], count: 5));
        var truncated = await SyncAsync(api);
        Assert.Equal("failed", truncated.Status);
        Assert.Contains("ответ неполный", truncated.Error);
        Assert.Empty(await LinesAsync());

        // Продажи загрузились, остаток — нет: запуск «частичный», продажи остаются.
        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok([Cake("НФНФ-1", "03-11-2026", Kulob, 1)]));
        api.Stock = _ => (HttpStatusCode.InternalServerError, "oops");
        var partial = await SyncAsync(api);
        Assert.Equal("partial", partial.Status);
        Assert.Contains("Остаток склада", partial.Error);
        Assert.Single(await LinesAsync());

        // 1С вдруг вернула меньше половины накладных окна — ничего не помечается пропавшим.
        api.Stock = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(Stock));
        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(Enumerable.Range(1, 30).Select(i => Cake($"Д-{i}", "10-11-2026", Kulob, i))));
        Assert.Equal("ok", (await SyncAsync(api)).Status);
        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(Enumerable.Range(1, 5).Select(i => Cake($"Д-{i}", "10-11-2026", Kulob, i))));
        var shrunk = await SyncAsync(api);
        Assert.Equal("failed", shrunk.Status);
        Assert.Contains("неполный ответ", shrunk.Error);
        Assert.Equal(30, (await LinesAsync()).Count(l => l.RemovedAt == null));
    }

    [Fact]
    public async Task Key_is_encrypted_and_never_returned()
    {
        if (fixture.Services is null) return;
        await ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var db = Get<OneBaseDbContext>(scope);
        var row = await db.Integrations.AsNoTracking().SingleAsync(i => i.Code == TajikSettings.Code);
        Assert.NotNull(row.ProtectedSecret);
        Assert.DoesNotContain(Token, row.ProtectedSecret);
        Assert.Equal("••••57", row.SecretHint);

        var settings = Get<ITajikSettingsStore>(scope);
        Assert.Equal(Token, await settings.TokenAsync());
        var view = await settings.GetAsync();
        Assert.Equal(("onebase", true, true), (view.TokenSource, view.Configured, view.Insecure));

        var controller = new TajikIntegrationController(settings, Get<TajikApiClient>(scope), Get<TajikSyncCoordinator>(scope), db,
            Get<OneBase.Application.Abstractions.IAuditLogger>(scope), fixture.Clock);
        var details = Assert.IsType<OkObjectResult>(await controller.Details(default));
        Assert.DoesNotContain(Token, JsonSerializer.Serialize(details.Value));

        // Неверный адрес не сохраняется; пустой ключ — удаляет сохранённый.
        await Assert.ThrowsAsync<ArgumentException>(() => settings.SaveAsync(new TajikSettingsChange(BaseUrl: "ftp://tj.test"), fixture.AdminId));
        await Assert.ThrowsAsync<ArgumentException>(() => settings.SaveAsync(new TajikSettingsChange(BaseUrl: "http://tj.test/sales?token=x"), fixture.AdminId));
        Assert.False((await settings.SaveAsync(new TajikSettingsChange(Token: ""), fixture.AdminId)).HasToken);
    }

    [Fact]
    public async Task Regions_stores_and_categories_of_the_tajik_export()
    {
        if (fixture.Services is null) return;
        var api = await ResetAsync();
        api.Sales = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(
        [
            Cake("НФНФ-1", "03-11-2026", Kulob, 12),
            Stix("НФНФ-1", "03-11-2026", Kulob, 20),
            FakeTajikApi.Sale("НФНФ-2", "05-11-2026", Shahrituz, "Тагоев Яхё", "НФ-3", "Помадка Ягода 4кг-Росип", 2, 134.4m, discount: 8.8m),
            FakeTajikApi.Sale("НФНФ-4", "25-11-2026", Market, "Курбонов Ахмадчон", "НФ-1", "100 Кекс Нилло 1,5 кг", 6, 69.35m),
        ]));
        api.Stock = _ => (HttpStatusCode.OK, FakeTajikApi.Ok(Stock));
        Assert.Equal("ok", (await SyncAsync(api)).Status);

        using (var scope = fixture.Services.CreateScope())
        {
            var view = await Get<TajikSalesService>(scope).OverviewAsync(null, null, default);
            Assert.Equal((2026, 11, new DateOnly(2026, 11, 25)), (view.Period.Year, view.Period.Month, view.Period.DataThrough));
            // Вес: кекс 1,5 кг (из названия) × 18 = 27; STIX — штука 0,4 кг × 20 = 8; помадка 4 кг × 2 = 8.
            Assert.Equal(43m, view.Totals.Kg);
            Assert.Equal((3, 3, 3), (view.Totals.Documents, view.Totals.Stores, view.Totals.Agents));
            Assert.Equal(12 * 69.35m + 20 * 11m + 2 * 134.4m - 8.8m + 6 * 69.35m, view.Totals.Amount);
            Assert.Null(view.Prev); // данных за октябрь нет — сравнивать не с чем

            Assert.Equal(["Кекс", "Бамбук", "Помадка"], view.Categories.Select(c => c.Name));
            var stix = view.Products.Single(p => p.Code == "НФ-2");
            Assert.Equal((0.4m, TajikWeights.Ambiguous, "Бамбук 400 г"), (stix.UnitKg, stix.WeightSource, stix.LinkoName));

            // Кулоб и Шахритуз — по слову в названии; рынок «Хочи Шариф» — без региона.
            Assert.Equal(["Кулоб", "Шахритуз", TajikRegionResolver.NoneName], view.Regions.Select(r => r.Name));
            Assert.Equal((1, 9m), (view.Unresolved.Stores, view.Unresolved.Kg));

            // Остаток: 30 кексов при продажах 18 за 30 дней — на 50 дней; помадки 5 при продажах 2 — на 75.
            var cake = view.Stock.Single(s => s.Code == "НФ-1");
            Assert.Equal((45m, 18m, 50m), (cake.Kg, cake.SoldQty30, cake.DaysOfCover));
            Assert.Equal((20m, 75m), (view.Stock.Single(s => s.Code == "НФ-3").Kg!.Value, view.Stock.Single(s => s.Code == "НФ-3").DaysOfCover!.Value));
        }

        // Новый регион «Душанбе»: ТП рынка — туда по умолчанию; точку Шахритуза закрепили вручную туда же.
        using (var scope = fixture.Services.CreateScope())
        {
            var setup = Get<TajikSetupService>(scope);
            var before = await setup.GetAsync(default);
            await setup.SaveRegionsAsync(before.Regions.Select(r => new TajikRegionInput(r.Id, r.Name, r.Keywords)).Append(new TajikRegionInput(null, "Душанбе", [])).ToList(),
                fixture.AdminId, default);
            var dushanbe = (await setup.GetAsync(default)).Regions.Single(r => r.Name == "Душанбе").Id;
            await setup.SaveAssignmentAsync(new TajikAssignmentInput("agent", "Курбонов Ахмадчон", null, dushanbe), fixture.AdminId, default);
            await setup.SaveAssignmentAsync(new TajikAssignmentInput("store", null, TajikText.StoreId(Shahrituz), dushanbe), fixture.AdminId, default);

            var after = await setup.GetAsync(default);
            Assert.Equal(("agent", "Душанбе"), (after.Stores.Single(s => s.Name == Market).Source, after.Stores.Single(s => s.Name == Market).RegionName));
            Assert.Equal(("manual", dushanbe), (after.Stores.Single(s => s.Name == Shahrituz).Source, after.Stores.Single(s => s.Name == Shahrituz).ManualRegionId));
            Assert.Equal(dushanbe, after.Agents.Single(a => a.Name == "Курбонов Ахмадчон").RegionId);

            await Assert.ThrowsAsync<ArgumentException>(() => setup.SaveRegionsAsync([new TajikRegionInput(null, "А", []), new TajikRegionInput(null, "а", [])], fixture.AdminId, default));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => setup.SaveAssignmentAsync(new TajikAssignmentInput("store", null, "нет-такой", dushanbe), fixture.AdminId, default));
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var sales = Get<TajikSalesService>(scope);
            var view = await sales.OverviewAsync(2026, 11, default);
            Assert.Equal(["Кулоб", "Душанбе"], view.Regions.Select(r => r.Name));
            Assert.Equal(0, view.Unresolved.Stores);

            // Регион Кулоб с фильтром «Кекс»: точка и товар с точками, где продан.
            var kulobId = view.Regions.Single(r => r.Name == "Кулоб").Id;
            var region = await sales.RegionAsync(kulobId, 2026, 11, "Кекс", default);
            Assert.NotNull(region);
            Assert.Equal((18m, "Кекс"), (region!.Totals.Kg, region.Category));
            var store = Assert.Single(region.Stores);
            Assert.Equal((Kulob, "name"), (store.Name, store.RegionSource));
            Assert.Equal(Kulob, Assert.Single(Assert.Single(region.Products).Where!).Name);
            Assert.Equal(["Кекс", "Бамбук"], region.Categories.Select(c => c.Name));

            var page = await sales.StoreAsync(store.Id, 2026, 11, default);
            Assert.NotNull(page);
            var doc = Assert.Single(page!.Documents);
            Assert.Equal(("НФНФ-1", 2, 26m), (doc.Number, doc.Lines.Count, doc.Kg));
            Assert.Single(page.Months);

            Assert.Null(await sales.RegionAsync(Guid.NewGuid().ToString(), 2026, 11, null, default));
            Assert.Null(await sales.StoreAsync("нет-такой", 2026, 11, default));
        }
    }

    [Fact]
    public void Pages_are_for_the_whole_organization_and_settings_need_their_rights()
    {
        static string? Permission(MemberInfo m) => m.GetCustomAttribute<HasPermissionAttribute>()?.Policy?.Replace(PermissionPolicyProvider.Prefix, "");
        Assert.Equal(Permissions.SalesRead, Permission(typeof(TajikSalesController)));
        Assert.NotNull(typeof(TajikSalesController).GetCustomAttribute<SalesOrganizationOnlyAttribute>());
        Assert.Equal(Permissions.SalesManage, Permission(typeof(TajikSalesController).GetMethod(nameof(TajikSalesController.SaveRegions))!));
        Assert.Equal(Permissions.SalesManage, Permission(typeof(TajikSalesController).GetMethod(nameof(TajikSalesController.SaveAssignment))!));
        Assert.Equal(Permissions.IntegrationsManage, Permission(typeof(TajikIntegrationController)));
    }
}

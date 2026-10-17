using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Domain.AI;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.DataSources;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// Linko и Google Sheets вместе, на настоящем PostgreSQL: у каждого поля один владелец; синхронизации независимы и в любом порядке (и
/// одновременно) не трогают данные друг друга; повторы не плодят дублей; недоступность или пустой ответ одного источника не стирает
/// данные; выполнение плана считается из плана Google Sheets и факта Linko один раз; строки с ошибками и попытки записать чужое поле —
/// в журнале конфликтов.
/// </summary>
public class SourceOwnershipTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Header = ["Регион", "Категория", "Месяц", "План, кг", "Дилер"];
    private static readonly DateTime September = new(2026, 9, 20, 10, 0, 0);

    private static PlanFieldMapping PlanColumns => new("Регион", "Категория", "План, кг", null, "Месяц", null, null, null);
    private static DataSourceFieldBinding RopField => new(DataSourceCatalog.PlanRop, PlanColumns);
    private static DataSourceFieldBinding PrimaryField => new(DataSourceCatalog.PlanPrimary, PlanColumns);
    private static DataSourceFieldBinding DealerField => new(DataSourceCatalog.Dealer, Profile: new ProfileFieldMapping("Регион", "Дилер"));

    private static ReportDocument Sheet(params string[][] rows) =>
        new([new ReportSheet("План", new[] { Header }.Concat(rows).Select(r => (IReadOnlyList<string>)r).ToList(), rows.Length + 1, Header.Length, false)]);

    private async Task<BindingView> BindAsync(DataSourceService sources, string section, params DataSourceFieldBinding[] fields)
    {
        var connection = await sources.CreateConnectionAsync(new ConnectionInput("План 2026", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms", "link", null),
            fixture.AdminId, CancellationToken.None);
        return await sources.SaveBindingAsync(null, new BindingInput(section, connection.Id, ["План"], 1, fields, "interval", 30, null, true),
            fixture.AdminId, CancellationToken.None);
    }

    /// <summary>Синхронизация Linko без ошибок; иначе — какие сущности и почему.</summary>
    private static void Synced(LinkoSyncReport report) =>
        Assert.True(report.Success, string.Join("; ", report.Entities.Where(e => e.Error is not null).Select(e => $"{e.Entity}: {e.Error}")));

    /// <summary>Факт Linko, кг, сентябрь 2026, по филиалу.</summary>
    private static Task<decimal> FactAsync(OneBaseDbContext db, long branch) =>
        db.LinkoOrders.AsNoTracking().Where(o => o.BranchId == branch && o.CreatedDate.Month == 9).SumAsync(o => o.TotalWeight);

    private static Task<decimal> PlanAsync(OneBaseDbContext db, PlanKind kind) =>
        db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == kind).SumAsync(p => p.PlanKg);

    private static Task<string?> DealerAsync(OneBaseDbContext db, string region) =>
        db.SalesRegions.AsNoTracking().Where(r => r.Name == region).Select(r => r.DealerName).FirstAsync();

    /// <summary>Выполнение плана региона, пересчитанное OneBase заново (проверка операции): факт Linko ÷ план.</summary>
    private static async Task<decimal> ExecutionAsync(IServiceProvider sp, Guid changeSetId, string region)
    {
        var changes = sp.GetRequiredService<DataChangeService>();
        await changes.VerifyAsync(changeSetId);
        var kpi = (await changes.GetAsync(changeSetId))!.Verification!.Value.GetProperty("kpi")[0];
        return kpi.GetProperty("regions").EnumerateArray().Single(r => r.GetProperty("name").GetString() == region).GetProperty("executionAfter").GetDecimal();
    }

    [PostgresFact]
    public async Task Sheets_plan_and_Linko_fact_live_side_by_side_in_any_order()
    {
        await fixture.ResetAsync();
        fixture.Sheets.Document = Sheet(["Хоразм", "Кекс", "2026-09", "1000", "Азизбек Хоразм"], ["Бухоро", "Кекс", "2026-09", "500", "Мехриддин Бухоро"]);
        fixture.Linko.Orders.Add(FakeLinko.Order(DataManagerDb.FakeLinkoIds + 1, 5, "Хоразм", September, 100));
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var sources = sp.GetRequiredService<DataSourceService>();
        var linko = sp.GetRequiredService<LinkoSyncService>();
        var db = sp.GetRequiredService<OneBaseDbContext>();

        // Сначала Linko — факт: 900 кг уже было, 100 пришло.
        Synced(await linko.SyncAsync(LinkoSyncMode.Incremental));
        Assert.Equal(1000m, await FactAsync(db, 5));

        // Затем Google Sheets — план РОП и дилер; факт Linko остаётся.
        var binding = await BindAsync(sources, "secondary", RopField, DealerField);
        var run = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", run.Status);
        Assert.Equal(1000m, await FactAsync(db, 5));
        Assert.Equal(1500m, await PlanAsync(db, PlanKind.Rop));
        Assert.Equal("Мехриддин Бухоро", await DealerAsync(db, "Бухоро"));
        var ropSet = run.Fields.Single(f => f.Field == DataSourceCatalog.PlanRop).ChangeSetId!.Value;
        Assert.Equal(1m, await ExecutionAsync(sp, ropSet, "Хоразм")); // факт Linko 1000 ÷ план Google Sheets 1000

        // Linko ещё раз, тот же заказ: дублей нет, план и дилер из Google Sheets на месте, выполнение без двойного учёта.
        Synced(await linko.SyncAsync(LinkoSyncMode.Incremental));
        Assert.Equal(1, await db.LinkoOrders.CountAsync(o => o.Id == DataManagerDb.FakeLinkoIds + 1));
        Assert.Equal(1000m, await FactAsync(db, 5));
        Assert.Equal(1500m, await PlanAsync(db, PlanKind.Rop));
        Assert.Equal("Мехриддин Бухоро", await DealerAsync(db, "Бухоро"));
        Assert.Equal(1m, await ExecutionAsync(sp, ropSet, "Хоразм"));

        // Google Sheets ещё раз: без изменений, факт на месте.
        Assert.Equal("unchanged", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);
        Assert.Equal(1000m, await FactAsync(db, 5));

        // Оба источника одновременно: новый заказ Linko и новый план Бухоро — сохраняются оба.
        fixture.Linko.Orders.Add(FakeLinko.Order(DataManagerDb.FakeLinkoIds + 2, 7, "Бухоро", September, 50));
        fixture.Sheets.Document = Sheet(["Хоразм", "Кекс", "2026-09", "1000", "Азизбек Хоразм"], ["Бухоро", "Кекс", "2026-09", "600", "Мехриддин Бухоро"]);
        using var other = fixture.Services.CreateScope();
        var otherSources = other.ServiceProvider.GetRequiredService<DataSourceService>();
        var both = await Task.WhenAll(
            Task.Run(async () => (await linko.SyncAsync(LinkoSyncMode.Incremental)).Success ? "ok" : "linko error"),
            Task.Run(async () => (await otherSources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status));
        Assert.Equal(["ok", "ok"], both);
        Assert.Equal(550m, await FactAsync(db, 7));
        Assert.Equal(1600m, await PlanAsync(db, PlanKind.Rop));
        Assert.Equal(1000m, await FactAsync(db, 5));
    }

    [PostgresFact]
    public async Task An_outage_or_an_empty_answer_of_one_source_keeps_the_data_of_both()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 9, 25, 6, 0, 0, TimeSpan.Zero);
        fixture.Clock.Now = now;
        fixture.Sheets.Document = Sheet(["Хоразм", "Кекс", "2026-09", "1000", "Азизбек Хоразм"], ["Бухоро", "Кекс", "2026-09", "500", "Мехриддин Бухоро"]);
        fixture.Linko.Orders.Add(FakeLinko.Order(DataManagerDb.FakeLinkoIds + 1, 5, "Хоразм", September, 100));
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var linko = scope.ServiceProvider.GetRequiredService<LinkoSyncService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        Synced(await linko.SyncAsync(LinkoSyncMode.Incremental));
        var binding = await BindAsync(sources, "secondary", RopField, DealerField);
        Assert.Equal("ok", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);

        // Linko недоступен: его синхронизация с ошибками, а план и дилер из Google Sheets на месте и Google Sheets работает дальше.
        fixture.Linko.Down = true;
        Assert.False((await linko.SyncAsync(LinkoSyncMode.Incremental)).Success);
        Assert.Equal(1000m, await FactAsync(db, 5));
        fixture.Sheets.Document = Sheet(["Хоразм", "Кекс", "2026-09", "1000", "Азизбек Хоразм"], ["Бухоро", "Кекс", "2026-09", "700", "Мехриддин Бухоро"]);
        Assert.Equal("ok", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);
        Assert.Equal(1700m, await PlanAsync(db, PlanKind.Rop));

        // Google недоступен по расписанию: ошибка, данные на месте, повтор через 5 минут; вторая ошибка подряд — уже по расписанию.
        fixture.Linko.Down = false;
        fixture.Sheets.Failure = "Google не ответил вовремя — попробуйте ещё раз.";
        var failed = await sources.RunAsync(binding.Id, "schedule", null, CancellationToken.None);
        Assert.Equal("error", failed.Status);
        Assert.Contains("Повтор через 5 минут", failed.Message);
        Assert.Equal(now.AddMinutes(5), (await db.DataSourceBindings.AsNoTracking().FirstAsync(b => b.Id == binding.Id)).NextRunAt);
        var again = await sources.RunAsync(binding.Id, "schedule", null, CancellationToken.None);
        Assert.DoesNotContain("Повтор", again.Message);
        Assert.Equal(now.AddMinutes(30), (await db.DataSourceBindings.AsNoTracking().FirstAsync(b => b.Id == binding.Id)).NextRunAt);
        Assert.Equal(1700m, await PlanAsync(db, PlanKind.Rop));
        Assert.Equal("Мехриддин Бухоро", await DealerAsync(db, "Бухоро"));

        // Linko тем временем работает: сбой Google его не останавливает.
        fixture.Linko.Orders.Add(FakeLinko.Order(DataManagerDb.FakeLinkoIds + 2, 5, "Хоразм", September, 20));
        Synced(await linko.SyncAsync(LinkoSyncMode.Incremental));
        Assert.Equal(1020m, await FactAsync(db, 5));

        // Google вернулся, но лист пустой (только заголовок): ничего не удаляется.
        fixture.Sheets.Failure = null;
        fixture.Sheets.Document = Sheet();
        var empty = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("error", empty.Status);
        Assert.Equal(1700m, await PlanAsync(db, PlanKind.Rop));
        Assert.Equal("Мехриддин Бухоро", await DealerAsync(db, "Бухоро"));
        Assert.Equal(1020m, await FactAsync(db, 5));
    }

    [PostgresFact]
    public async Task Chat_and_manual_edits_cannot_overwrite_a_field_kept_in_google_sheets()
    {
        await fixture.ResetAsync();
        fixture.Sheets.Document = Sheet(["Бухоро", "Кекс", "2026-09", "500", "Мехриддин Бухоро"]);
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var sources = sp.GetRequiredService<DataSourceService>();
        var changes = sp.GetRequiredService<DataChangeService>();
        var ownership = sp.GetRequiredService<FieldOwnership>();
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var binding = await BindAsync(sources, "primary", PrimaryField, DealerField);
        Assert.Equal("ok", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);

        // Чат AI Data Manager: план первички и дилера ведёт таблица — план изменений не готовится.
        var chat = new DataActor(fixture.AdminId, null);
        var plan = new RegionPlanRequest(PlanKind.Primary, false, null, null, [new PlanValue("Бухоро", "Кекс", 2026, 9, 900m, "сообщение")], null);
        var refused = await Assert.ThrowsAsync<DataManagerException>(() => changes.PrepareRegionPlansAsync(chat, plan));
        Assert.Contains("ведёт Google Sheets «План 2026»", refused.Message);
        await Assert.ThrowsAsync<DataManagerException>(() =>
            changes.PrepareRegionProfilesAsync(chat, [new RegionProfileValue("Бухоро", "dealer", "Другой дилер", "сообщение")], null, null));

        // План РОП и СВР таблица не ведёт — их чат меняет как раньше.
        Assert.Equal(DataChangeSetStatus.Draft, (await changes.PrepareRegionPlansAsync(chat, plan with { Kind = PlanKind.Rop })).Status);
        Assert.Equal(DataChangeSetStatus.Draft,
            (await changes.PrepareRegionProfilesAsync(chat, [new RegionProfileValue("Бухоро", "supervisor", "Каримов", "сообщение")], null, null)).Status);

        // Ручная правка («Настройки продаж → Регионы») — та же проверка.
        await Assert.ThrowsAsync<DataManagerException>(() =>
            ownership.EnsureAsync([DataSourceCatalog.Dealer], null, ConflictSources.Manual, fixture.AdminId, "Бухоро", null));
        Assert.Empty(await ownership.ForeignAsync([DataSourceCatalog.Supervisor], null));

        // Значения таблицы на месте; отказы — в журнале: события закрыты, вмешательство не нужно.
        Assert.Equal(500m, await PlanAsync(db, PlanKind.Primary));
        Assert.Equal("Мехриддин Бухоро", await DealerAsync(db, "Бухоро"));
        var refusals = await db.DataSourceConflicts.AsNoTracking().Where(c => c.Kind == ConflictKinds.Owner).OrderBy(c => c.CreatedAt).ToListAsync();
        Assert.Equal([ConflictSources.DataManager, ConflictSources.DataManager, ConflictSources.Manual], refusals.Select(c => c.Source));
        Assert.All(refusals, c => Assert.True(c.ResolvedAt is not null && !c.NeedsAction));
        Assert.Equal([DataSourceCatalog.PlanPrimary, DataSourceCatalog.Dealer, DataSourceCatalog.Dealer], refusals.Select(c => c.Field));

        // План из чата, подготовленный, пока привязка была выключена, после её включения не применяется.
        var input = new BindingInput("primary", binding.ConnectionId, ["План"], 1, [PrimaryField, DealerField], "interval", 30, null, false);
        await sources.SaveBindingAsync(binding.Id, input, fixture.AdminId, CancellationToken.None);
        var draft = await changes.PrepareRegionPlansAsync(chat, plan);
        await sources.SaveBindingAsync(binding.Id, input with { Enabled = true }, fixture.AdminId, CancellationToken.None);
        var late = await Assert.ThrowsAsync<DataManagerException>(() => sp.GetRequiredService<IDataChangeApplier>().ApplyAsync(draft.Id, fixture.AdminId));
        Assert.Contains("ведёт Google Sheets", late.Message);
        Assert.Equal(DataChangeSetStatus.Draft, (await db.DataChangeSets.AsNoTracking().FirstAsync(s => s.Id == draft.Id)).Status);
        Assert.Equal(500m, await PlanAsync(db, PlanKind.Primary));

        // Карта владения: план первички и дилер — Google Sheets, план РОП и СВР — OneBase, факт — Linko, выполнение — расчёт.
        var overview = await sources.OverviewAsync(fixture.AdminId, CancellationToken.None);
        var owners = overview.Ownership.ToDictionary(r => r.Key, r => r.Owner);
        Assert.Equal("google_sheets", owners[DataSourceCatalog.PlanPrimary]);
        Assert.Equal("google_sheets", owners[DataSourceCatalog.Dealer]);
        Assert.Equal("onebase", owners[DataSourceCatalog.PlanRop]);
        Assert.Equal("onebase", owners[DataSourceCatalog.Supervisor]);
        Assert.Equal("linko", owners["linko_sales"]);
        Assert.Equal("calculated", owners["kpi"]);
        Assert.NotNull(overview.Ownership.Single(r => r.Key == DataSourceCatalog.PlanPrimary).UpdatedAt);
    }

    [PostgresFact]
    public async Task Rows_with_errors_stay_in_the_conflict_log_until_the_table_is_fixed()
    {
        await fixture.ResetAsync();
        fixture.Sheets.Document = Sheet(["Хоразм", "Кекс", "2026-09", "1000", ""], ["Хоразмм", "Кекс", "2026-09", "300", ""]);
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var log = scope.ServiceProvider.GetRequiredService<DataSourceOwnershipService>();
        var linko = scope.ServiceProvider.GetRequiredService<LinkoSyncService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var binding = await BindAsync(sources, "primary", PrimaryField);

        // Опечатка в регионе: поле не записано, конфликт открыт и ждёт исправления таблицы.
        Assert.Equal("conflicts", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);
        var open = Assert.Single(await log.ConflictsAsync(CancellationToken.None));
        Assert.Equal((ConflictKinds.Row, DataSourceCatalog.PlanPrimary, true, (DateTimeOffset?)null), (open.Kind, open.Field, open.NeedsAction, open.ResolvedAt));
        Assert.Contains("Хоразмм", open.Entity);
        Assert.Contains("Google Sheets «План 2026»", open.Rule);
        Assert.Contains("«План 2026»", open.Binding);
        Assert.Equal(1, (await sources.OverviewAsync(fixture.AdminId, CancellationToken.None)).OpenConflicts);
        Assert.Equal(0m, await PlanAsync(db, PlanKind.Primary));

        // Та же ошибка при следующей синхронизации — та же запись, а не новая.
        await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal(2, Assert.Single(await log.ConflictsAsync(CancellationToken.None)).Occurrences);

        // Таблицу исправили: план записан, конфликт закрыт сам.
        fixture.Sheets.Document = Sheet(["Хоразм", "Кекс", "2026-09", "1000", ""], ["Бухоро", "Кекс", "2026-09", "300", ""]);
        Assert.Equal("ok", (await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None)).Status);
        var fixedRow = Assert.Single(await log.ConflictsAsync(CancellationToken.None));
        Assert.NotNull(fixedRow.ResolvedAt);
        Assert.Equal("строка исправлена в таблице", fixedRow.Decision);
        Assert.Equal(1300m, await PlanAsync(db, PlanKind.Primary));

        // Одинаковое название при разных ID Linko не склеивается: второй филиал «Хоразм» — отдельный регион, строка «Хоразм» неоднозначна.
        fixture.Linko.Orders.Add(FakeLinko.Order(DataManagerDb.FakeLinkoIds + 5, 905, "Хоразм", September, 10));
        Synced(await linko.SyncAsync(LinkoSyncMode.Incremental));
        Assert.Equal(2, await db.SalesRegions.CountAsync(r => r.Name == "Хоразм"));
        fixture.Sheets.Document = Sheet(["Хоразм", "Кекс", "2026-09", "1100", ""], ["Бухоро", "Кекс", "2026-09", "300", ""]);
        var ambiguous = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("conflicts", ambiguous.Status);
        Assert.Contains(ambiguous.Fields.Single().Problems!, p => p.Contains("нескольким регионам"));
        Assert.Equal(1300m, await PlanAsync(db, PlanKind.Primary));

        // Привязку удалили: её открытые конфликты закрыты, записанные данные остались.
        await sources.DeleteBindingAsync(binding.Id, fixture.AdminId, CancellationToken.None);
        var all = await log.ConflictsAsync(CancellationToken.None);
        Assert.All(all, c => Assert.NotNull(c.ResolvedAt));
        Assert.Contains(all, c => c.Decision == "привязка удалена");
        Assert.Equal(1300m, await PlanAsync(db, PlanKind.Primary));
    }
}

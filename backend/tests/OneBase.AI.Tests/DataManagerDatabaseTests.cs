using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OneBase.AI.Consultant;
using OneBase.AI.DataManager;
using OneBase.AI.Gateway;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Identity;
using OneBase.Domain.Sales;
using OneBase.Infrastructure;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Тест на настоящем PostgreSQL: нужна переменная ONEBASE_TEST_POSTGRES — строка подключения пользователя с правом CREATE DATABASE
/// (например, к локальному docker compose). Тест создаёт отдельную базу onebase_dm_test_…, применяет все миграции и удаляет её в конце.
/// Без переменной — пропускается.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DataManagerDb.Variable)))
        {
            Skip = $"Нет {DataManagerDb.Variable} — тесты AI Data Manager на PostgreSQL пропущены.";
        }
    }
}

/// <summary>Подставная модель Claude: заранее заданные ответы, запись всех запросов.</summary>
public sealed class FakeDataModel : IDataManagerModel
{
    public Queue<Func<IReadOnlyList<TranscriptMessage>, ModelReply>> Script { get; } = new();
    public List<List<TranscriptMessage>> Calls { get; } = [];

    public Task<AiReadiness> GetReadinessAsync(CancellationToken ct = default) => Task.FromResult(new AiReadiness(true, null));

    public Task<ModelReply> NextAsync(string system, IReadOnlyList<TranscriptMessage> transcript, IReadOnlyList<DataToolSpec> tools,
        AiCallContext context, CancellationToken ct = default)
    {
        Assert.Contains(tools, t => t.Name == "prepare_region_plan_update");
        Assert.DoesNotContain(tools, t => t.Name.Contains("apply", StringComparison.Ordinal)); // применить может только человек
        Calls.Add(transcript.ToList());
        return Task.FromResult(Script.Count == 0 ? Text("Готово.") : Script.Dequeue()(transcript));
    }

    public static ModelReply Tool(string name, object input) => new([
        new TranscriptBlock { Type = "thinking", Thinking = "", Signature = "sig" },
        new TranscriptBlock { Type = "tool_use", Id = $"tu_{Guid.NewGuid():N}", Name = name, Input = JsonSerializer.SerializeToElement(input) },
    ], "tool_use", 100, 20, "fake");

    public static ModelReply Text(string text) => new([TranscriptBlock.FromText(text)], "end_turn", 100, 20, "fake");

    /// <summary>id плана из последнего результата prepare_* в переписке.</summary>
    public static string LastChangeSetId(IReadOnlyList<TranscriptMessage> transcript) =>
        transcript.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").Select(b => Regex.Match(b.Text ?? "", "\"id\":\"([0-9a-f-]{36})\""))
            .Last(m => m.Success).Groups[1].Value;
}

public sealed class MemoryStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = [];

    public async Task PutAsync(string objectKey, Stream content, long sizeBytes, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _files[objectKey] = buffer.ToArray();
    }

    public async Task DownloadAsync(string objectKey, Stream destination, CancellationToken ct = default) =>
        await destination.WriteAsync(_files[objectKey], ct);

    public Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        _files.Remove(objectKey);
        return Task.CompletedTask;
    }
}

/// <summary>Подставная таблица Google: лист(ы), которые «вернул» Google, и счётчик чтений.</summary>
public sealed class FakeSheets : OneBase.Application.DataSources.IGoogleSheetsClient
{
    public ReportDocument Document { get; set; } = new([]);
    public int Reads { get; private set; }

    public Task<IReadOnlyList<OneBase.Application.DataSources.SheetInfo>> SheetsAsync(OneBase.Application.DataSources.SheetSource source, CancellationToken ct = default)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<OneBase.Application.DataSources.SheetInfo>>(
            Document.Sheets.Select(s => new OneBase.Application.DataSources.SheetInfo(s.Name, s.TotalRows, s.TotalColumns)).ToList());
    }

    public Task<ReportDocument> ReadAsync(OneBase.Application.DataSources.SheetSource source, IReadOnlyCollection<string> sheets, CancellationToken ct = default)
    {
        Reads++;
        var names = OneBase.Application.DataSources.DataSourceRules.ResolveSheets(Document.Sheets.Select(s => s.Name).ToList(), sheets);
        return Task.FromResult(new ReportDocument(names.Select(n => Document.Sheets.First(s => s.Name == n)).ToList()));
    }
}

/// <summary>Часы теста: Now задан — «сейчас» это он (смена месяца в синхронизации), иначе — настоящее время.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset? Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now ?? base.GetUtcNow();
}

public sealed class DataManagerDb : IAsyncLifetime
{
    public const string Variable = "ONEBASE_TEST_POSTGRES";

    private string? _admin;
    private string? _database;

    public ServiceProvider Services { get; private set; } = null!;
    public FakeDataModel Model { get; } = new();
    public FakeSheets Sheets { get; } = new();
    public TestClock Clock { get; } = new();
    public Guid AdminId { get; private set; }
    public Guid ViewerId { get; private set; }
    public Guid DataAdminId { get; private set; }
    public Guid KhorazmId { get; private set; }
    public Guid BukharaId { get; private set; }

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(_admin))
        {
            return;
        }

        _database = $"onebase_dm_test_{Guid.NewGuid():N}"[..28];
        await using (var connection = new NpgsqlConnection(_admin))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", connection).ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_admin) { Database = _database };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = builder.ConnectionString,
            ["DataManager:RetryDelaySeconds"] = "0",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddInfrastructure(config);
        services.AddAi(config);
        services.AddSingleton<IDataManagerModel>(Model);
        services.AddSingleton<IFileStorage, MemoryStorage>();
        services.AddSingleton<OneBase.Application.DataSources.IGoogleSheetsClient>(Sheets);
        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.Database.MigrateAsync();
        await SeedAsync(db);
    }

    private async Task SeedAsync(OneBaseDbContext db)
    {
        var admin = new Role { Name = "Админ теста" };
        foreach (var code in new[] { Permissions.DataManage, Permissions.SalesManage, Permissions.SalesRead })
        {
            admin.Permissions.Add(new RolePermission { Code = code });
        }

        // Без ai.data.manage — даже с правами на продажи AI Data Manager недоступен.
        var viewer = new Role { Name = "Наблюдатель теста" };
        viewer.Permissions.Add(new RolePermission { Code = Permissions.SalesManage });
        viewer.Permissions.Add(new RolePermission { Code = Permissions.SalesRead });

        // Как «Админ 2»: файлы и AI Data Manager, без прав на раздел «Продажи».
        var dataAdmin = new Role { Name = "Админ 2 теста" };
        foreach (var code in new[] { Permissions.FilesRead, Permissions.FilesWrite, Permissions.DataManage })
        {
            dataAdmin.Permissions.Add(new RolePermission { Code = code });
        }

        var adminUser = new User { Login = "dm-admin", FullName = "Администратор" };
        adminUser.Roles.Add(new UserRole { Role = admin });
        var viewerUser = new User { Login = "dm-viewer", FullName = "Наблюдатель" };
        viewerUser.Roles.Add(new UserRole { Role = viewer });
        var dataAdminUser = new User { Login = "dm-admin2", FullName = "Админ 2" };
        dataAdminUser.Roles.Add(new UserRole { Role = dataAdmin });
        db.Users.AddRange(adminUser, viewerUser, dataAdminUser);
        AdminId = adminUser.Id;
        ViewerId = viewerUser.Id;
        DataAdminId = dataAdminUser.Id;

        var khorazm = new SalesRegion { LinkoBranchId = 5, Name = "Хоразм", DealerName = "Азизбек Хоразм" };
        var bukhara = new SalesRegion { LinkoBranchId = 7, Name = "Бухоро" };
        db.SalesRegions.AddRange(khorazm, bukhara, new SalesRegion { LinkoBranchId = 4, Name = "Завод" });
        KhorazmId = khorazm.Id;
        BukharaId = bukhara.Id;

        db.LinkoProductTypes.AddRange(new LinkoProductType { Id = 10, Name = "Кекс" }, new LinkoProductType { Id = 4, Name = "Бамбук" });
        db.LinkoProducts.AddRange(new LinkoProduct { Id = 100, Name = "Кекс 1 кг", Code = "100", TypeId = 10 },
            new LinkoProduct { Id = 200, Name = "Бамбук 400 г", Code = "200", TypeId = 4 });

        // Продажи сентября 2026: Хоразм 900 кг, Бухоро 500 кг — чтобы OneBase посчитал выполнение плана.
        var id = 1L;
        foreach (var (branch, name, kg) in new[] { (5L, "Хоразм", 900m), (7L, "Бухоро", 500m) })
        {
            var order = new LinkoOrder
            {
                Id = id, CreatedAt = new DateTime(2026, 9, 10, 8, 0, 0), CreatedDate = new DateOnly(2026, 9, 10),
                AcceptedAt = new DateTime(2026, 9, 11, 9, 0, 0), AcceptedDate = new DateOnly(2026, 9, 11), Status = "delivered",
                MarketId = 1000 + id, BranchId = branch, BranchName = name, AgentId = 50 + id, TotalWeight = kg, TotalPrice = kg * 30_000,
            };
            db.LinkoOrders.Add(order);
            db.LinkoOrderLines.Add(new LinkoOrderLine
            {
                Id = id, OrderId = order.Id, ProductId = 100, Amount = kg, Price = 30_000, TotalPrice = kg * 30_000, TotalWeight = kg,
            });
            id++;
        }

        await db.SaveChangesAsync();
    }

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.DataChangeSets.ExecuteDeleteAsync();
        await db.DataMessages.ExecuteDeleteAsync();
        await db.DataFiles.ExecuteDeleteAsync();
        await db.DataConversations.ExecuteDeleteAsync();
        await db.SalesRegionPlans.ExecuteDeleteAsync();
        await db.SalesPnlValues.ExecuteDeleteAsync();
        await db.DataSourceSyncRuns.ExecuteDeleteAsync();
        await db.DataSourceBindings.ExecuteDeleteAsync();
        await db.DataSourceConnections.ExecuteDeleteAsync();
        await db.SalesRegions.Where(r => r.Name == "Бухоро").ExecuteUpdateAsync(u => u.SetProperty(r => r.DealerName, (string?)null));
        Clock.Now = null;
        await db.UserNotifications.ExecuteDeleteAsync();
        Model.Script.Clear();
        Model.Calls.Clear();
        scope.ServiceProvider.GetRequiredService<OneBase.Application.Sales.SalesCacheSignal>().InvalidateHistory();
    }

    public async Task DisposeAsync()
    {
        if (_database is null || _admin is null)
        {
            return;
        }

        await Services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
    }
}

/// <summary>AI Data Manager целиком: загрузка отчёта → ход модели с инструментами → план → подтверждение → проверка → откат.</summary>
public class DataManagerDatabaseTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const string Report = "Регион;Кекс;Бамбук\nХоразм;1 000;300\nБухоро;400,5;200\nИтого;1400,5;500\n";
    private const string Corrected = "Регион;Кекс;Бамбук\nХоразм;1 000;300\nБухоро;450;200\n";

    private static readonly object Mapping = new
    {
        headerRow = 1,
        regionColumn = "Регион",
        valueColumns = new[] { new { column = "Кекс" }, new { column = "Бамбук" } },
        year = 2026,
        month = 9,
    };

    private async Task<Guid> UploadAsync(IServiceProvider sp, string content, string name = "plan.csv", Guid? user = null) =>
        (await sp.GetRequiredService<DataFileService>()
            .UploadAsync(user ?? fixture.AdminId, null, name, new MemoryStream(Encoding.UTF8.GetBytes(content)), CancellationToken.None)).Id;

    private static Func<ChatEvent, Task> Collect(List<ChatEvent> events) => e =>
    {
        events.Add(e);
        return Task.CompletedTask;
    };

    /// <summary>Сценарий модели: прочитать отчёт, подготовить план, ответить; после подтверждения — проверить и доложить.</summary>
    private void ScriptPrepare(Guid fileId)
    {
        fixture.Model.Script.Enqueue(_ => FakeDataModel.Tool("read_report", new { fileId }));
        fixture.Model.Script.Enqueue(_ => FakeDataModel.Tool("prepare_region_plan_update", new { kind = "rop", mode = "upsert", fileId, mapping = Mapping }));
        fixture.Model.Script.Enqueue(_ => FakeDataModel.Text("Подготовил план: 4 новые строки, подтвердите."));
    }

    private void ScriptVerify()
    {
        fixture.Model.Script.Enqueue(t => FakeDataModel.Tool("get_change_set", new { id = Regex.Match(t[^1].Content[0].Text ?? "", "план ([0-9a-f-]{36})").Groups[1].Value }));
        fixture.Model.Script.Enqueue(_ => FakeDataModel.Text("Применено, расхождений нет."));
    }

    private async Task<(Guid ConversationId, DataChangeSetView Set)> PrepareViaChatAsync(IServiceProvider sp, string content)
    {
        var fileId = await UploadAsync(sp, content);
        ScriptPrepare(fileId);
        var events = new List<ChatEvent>();
        await sp.GetRequiredService<DataManagerChatService>()
            .SendAsync(fixture.AdminId, null, "Обнови план РОП на сентябрь по отчёту", [fileId], Collect(events), CancellationToken.None);
        Assert.Contains(events, e => e.Type == "done");
        var set = Assert.IsType<DataChangeSetView>(events.Last(e => e.Type == "changeset").Data);
        return (set.ConversationId!.Value, set);
    }

    private async Task<Dictionary<(Guid, long?), decimal>> PlansAsync(IServiceProvider sp) =>
        await sp.GetRequiredService<IAppDbContext>().SalesRegionPlans.AsNoTracking()
            .Where(p => p.Year == 2026 && p.Month == 9 && p.Kind == PlanKind.Rop)
            .ToDictionaryAsync(p => (p.RegionId, p.CategoryId), p => p.PlanKg);

    [PostgresFact]
    public async Task FullFlow_ReportToPlanToConfirmToVerifiedKpi()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var (conversationId, set) = await PrepareViaChatAsync(sp, Report);

        Assert.Equal(DataChangeSetStatus.Draft, set.Status);
        Assert.Equal(4, set.Items.Count(i => i.Action == DataChangeAction.Create));
        Assert.Equal("400.5", set.Items.Single(i => i.Label.StartsWith("Бухоро · Кекс", StringComparison.Ordinal)).After);
        Assert.Empty(await PlansAsync(sp)); // до подтверждения ничего не записано

        ScriptVerify();
        var events = new List<ChatEvent>();
        var outcome = await sp.GetRequiredService<DataManagerChatService>().ApplyAsync(fixture.AdminId, set.Id, Collect(events), CancellationToken.None);

        Assert.Equal(DataChangeSetStatus.Applied, outcome.Status);
        Assert.Equal(4, outcome.Applied);
        var plans = await PlansAsync(sp);
        Assert.Equal(1000m, plans[(fixture.KhorazmId, 10)]);
        Assert.Equal(400.5m, plans[(fixture.BukharaId, 10)]);

        // Проверка: значения в базе и пересчитанный OneBase план региона совпадают с отчётом.
        var applied = (await sp.GetRequiredService<DataChangeService>().GetAsync(set.Id))!;
        var verification = applied.Verification!.Value;
        Assert.Equal(0, verification.GetProperty("mismatches").GetInt32());
        var kpi = verification.GetProperty("kpi")[0];
        Assert.Equal("Rop", kpi.GetProperty("planSourceBefore").GetString(), ignoreCase: true);
        var khorazm = kpi.GetProperty("regions").EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Хоразм");
        Assert.Equal(1300m, khorazm.GetProperty("planAfter").GetDecimal()); // план региона в расчёте = 1000 + 300
        Assert.Equal(900m / 1300m, khorazm.GetProperty("executionAfter").GetDecimal(), 6);

        Assert.Contains(events, e => e.Type == "event");
        Assert.Contains(events, e => e.Type == "done");

        // Переписка с моделью корректна: роли чередуются, на каждый tool_use есть tool_result.
        var conversation = await sp.GetRequiredService<IAppDbContext>().DataConversations.AsNoTracking().SingleAsync(c => c.Id == conversationId);
        var transcript = DataManagerChatService.Transcript(conversation);
        for (var i = 1; i < transcript.Count; i++)
        {
            Assert.NotEqual(transcript[i - 1].Role, transcript[i].Role);
        }

        var uses = transcript.SelectMany(m => m.Content).Where(b => b.Type == "tool_use").Select(b => b.Id).ToHashSet();
        var results = transcript.SelectMany(m => m.Content).Where(b => b.Type == "tool_result").Select(b => b.ToolUseId).ToHashSet();
        Assert.Equal(uses, results);
        Assert.Null(conversation.BusySince);
    }

    [PostgresFact]
    public async Task SameReportAgain_CreatesNothing_AndApplyingTwiceDoesNotDuplicate()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var (_, set) = await PrepareViaChatAsync(sp, Report);
        var applier = sp.GetRequiredService<IDataChangeApplier>();

        // Двойное нажатие и параллельный запрос: применяется ровно один раз.
        var first = applier.ApplyAsync(set.Id, fixture.AdminId);
        using var other = fixture.Services.CreateScope();
        var second = other.ServiceProvider.GetRequiredService<IDataChangeApplier>().ApplyAsync(set.Id, fixture.AdminId);
        var outcomes = await Task.WhenAll(first, second);
        Assert.Single(outcomes, o => o.Applied == 4);
        Assert.Contains(outcomes, o => o.Message.Contains("уже применён", StringComparison.Ordinal) || o.Status == DataChangeSetStatus.Applied);
        Assert.Equal(4, (await PlansAsync(sp)).Count);
        Assert.Equal(DataChangeSetStatus.Applied, (await applier.ApplyAsync(set.Id, fixture.AdminId)).Status);
        Assert.Equal(4, (await PlansAsync(sp)).Count);

        // Тот же файл ещё раз: узнаётся по SHA-256, план — без изменений, применять нечего.
        var again = await sp.GetRequiredService<DataFileService>().UploadAsync(fixture.AdminId, null, "plan (1).csv",
            new MemoryStream(Encoding.UTF8.GetBytes(Report)), CancellationToken.None);
        Assert.Contains(again.Duplicates, d => d.AppliedChangeSets.Contains(set.Id));
        var repeat = await sp.GetRequiredService<DataChangeService>().PrepareRegionPlansAsync(new DataActor(fixture.AdminId, null),
            new RegionPlanRequest(PlanKind.Rop, false, again.Id, ToMapping(), null, null));
        Assert.All(repeat.Items, i => Assert.Equal(DataChangeAction.Unchanged, i.Action));
        Assert.False(repeat.CanApply);
        await Assert.ThrowsAsync<DataManagerException>(() => applier.ApplyAsync(repeat.Id, fixture.AdminId));
        Assert.Equal(4, (await PlansAsync(sp)).Count);
    }

    [PostgresFact]
    public async Task CorrectedReport_ShowsOnlyChangedRows_WithPreviousVersion()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var (_, set) = await PrepareViaChatAsync(sp, Report);
        await sp.GetRequiredService<IDataChangeApplier>().ApplyAsync(set.Id, fixture.AdminId);

        var fileId = await UploadAsync(sp, Corrected, "plan v2.csv");
        var corrected = await sp.GetRequiredService<DataChangeService>().PrepareRegionPlansAsync(new DataActor(fixture.AdminId, null),
            new RegionPlanRequest(PlanKind.Rop, false, fileId, ToMapping(), null, null));

        var update = Assert.Single(corrected.Items, i => i.Action == DataChangeAction.Update);
        Assert.Equal(("400.5", "450"), (update.Before, update.After));
        Assert.Equal(3, corrected.Items.Count(i => i.Action == DataChangeAction.Unchanged));
        var previous = corrected.Summary.GetProperty("previous");
        Assert.Equal(set.Id, previous.GetProperty("changeSetId").GetGuid());
        Assert.Equal(1, previous.GetProperty("correctedRows").GetInt32());
    }

    [PostgresFact]
    public async Task DataChangedAfterPreparation_PlanIsStale_NothingWritten()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var (_, set) = await PrepareViaChatAsync(sp, Report);

        // Другой администратор успел завести план Хоразма по Кексу.
        var db = sp.GetRequiredService<IAppDbContext>();
        db.SalesRegionPlans.Add(new SalesRegionPlan { RegionId = fixture.KhorazmId, Kind = PlanKind.Rop, Year = 2026, Month = 9, CategoryId = 10, PlanKg = 777 });
        await db.SaveChangesAsync();

        var outcome = await sp.GetRequiredService<IDataChangeApplier>().ApplyAsync(set.Id, fixture.AdminId);

        Assert.Equal(DataChangeSetStatus.Stale, outcome.Status);
        Assert.Contains(outcome.Problems, p => p.Contains("Хоразм · Кекс", StringComparison.Ordinal));
        var plans = await PlansAsync(sp);
        Assert.Single(plans);
        Assert.Equal(777m, plans[(fixture.KhorazmId, 10)]);
    }

    [PostgresFact]
    public async Task CancelledPlanCannotBeApplied()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var (_, set) = await PrepareViaChatAsync(sp, Report);

        var cancelled = await sp.GetRequiredService<DataManagerChatService>().CancelAsync(fixture.AdminId, set.Id, _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(DataChangeSetStatus.Cancelled, cancelled.Status);
        await Assert.ThrowsAsync<DataManagerException>(() => sp.GetRequiredService<IDataChangeApplier>().ApplyAsync(set.Id, fixture.AdminId));
        Assert.Empty(await PlansAsync(sp));
    }

    /// <summary>P&L: реестр «Прочие расходы» → план → подтверждение → P&L раздела пересчитан и сверен → повтор без дублей → откат.</summary>
    [PostgresFact]
    public async Task Pnl_RegistryToConfirmedVerifiedPnl_RollbackRestoresEmpty()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var chat = sp.GetRequiredService<DataManagerChatService>();
        var pnl = sp.GetRequiredService<PnlService>();
        const string registry = "Год;Месяц;Сумма;Диллер;Статья\n2026;9;1 000;Азизбек Хоразм;601-Дилер бонуслари\n2026;9;500;азизбек  хоразм;609-Акция\n"
                                + "2026;9;300;Азизбек Хоразм;606-Логистика\n2026;9;200;Каримов;339-Утилизация\n";
        var mapping = new
        {
            headerRow = 1, dealerColumn = "Диллер", lineColumn = "Статья", valueColumn = "Сумма", yearColumn = "Год", monthColumn = "Месяц", sumRows = true,
        };

        async Task<DataChangeSetView> PrepareAsync()
        {
            var fileId = await UploadAsync(sp, registry, "prochie.csv");
            fixture.Model.Script.Enqueue(_ => FakeDataModel.Tool("prepare_pnl_update", new { mode = "upsert", fileId, mapping }));
            fixture.Model.Script.Enqueue(_ => FakeDataModel.Text("Подготовил загрузку P&L."));
            var events = new List<ChatEvent>();
            await chat.SendAsync(fixture.AdminId, null, "Загрузи прочие расходы за сентябрь в P&L", [fileId], Collect(events), CancellationToken.None);
            return Assert.IsType<DataChangeSetView>(events.Last(e => e.Type == "changeset").Data);
        }

        var set = await PrepareAsync();
        Assert.Equal(DataTargets.Pnl, set.Target);
        Assert.Equal(3, set.Items.Count(i => i.Action == DataChangeAction.Create));
        Assert.Empty((await pnl.GetAsync(null, null)).Months); // до подтверждения P&L пуст — демо-данных нет

        ScriptVerify();
        var outcome = await chat.ApplyAsync(fixture.AdminId, set.Id, Collect([]), CancellationToken.None);
        Assert.Equal(DataChangeSetStatus.Applied, outcome.Status);

        var view = await pnl.GetAsync(null, null);
        Assert.Equal((2026, 9), (view.Year!.Value, view.Month!.Value));
        var azizbek = Assert.Single(view.Groups.SelectMany(g => g.Dealers), d => d.Name == "Азизбек Хоразм");
        Assert.Equal("Хоразм", azizbek.Region); // дилер региона из справочника OneBase
        Assert.Equal(1500m, azizbek.Values[PnlMath.Sm]);
        Assert.Equal(300m, azizbek.Values[PnlMath.Ship]);
        Assert.Equal(-2000m, view.Total[PnlMath.Ebit]);
        Assert.Equal(set.Id, view.LastLoad!.ChangeSetId);

        var verification = (await sp.GetRequiredService<DataChangeService>().GetAsync(set.Id))!.Verification!.Value;
        Assert.Equal(0, verification.GetProperty("mismatches").GetInt32());
        Assert.Equal(-2000m, verification.GetProperty("pnl")[0].GetProperty("after").GetProperty("ebit").GetDecimal());

        // Тот же реестр ещё раз: изменений нет, применять нечего.
        var again = await PrepareAsync();
        Assert.False(again.CanApply);
        Assert.All(again.Items, i => Assert.Equal(DataChangeAction.Unchanged, i.Action));

        var rollback = await chat.RollbackAsync(fixture.AdminId, set.Id, Collect([]), CancellationToken.None);
        Assert.Equal(DataChangeSetStatus.RolledBack, rollback.Status);
        Assert.Empty((await pnl.GetAsync(null, null)).Months);
    }

    [PostgresFact]
    public async Task Rollback_RestoresOnlyRowsNobodyChangedSince()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var (_, set) = await PrepareViaChatAsync(sp, Report);
        var applier = sp.GetRequiredService<IDataChangeApplier>();
        await applier.ApplyAsync(set.Id, fixture.AdminId);

        // После применения кто-то поменял план Бухоро по Бамбуку — его правка должна остаться.
        var db = sp.GetRequiredService<IAppDbContext>();
        var row = await db.SalesRegionPlans.SingleAsync(p => p.RegionId == fixture.BukharaId && p.CategoryId == 4 && p.Month == 9);
        row.PlanKg = 250;
        await db.SaveChangesAsync();

        var outcome = await applier.RollbackAsync(set.Id, fixture.AdminId);

        Assert.Equal(DataChangeSetStatus.PartiallyRolledBack, outcome.Status);
        Assert.Equal(3, outcome.Applied);
        var plans = await PlansAsync(sp);
        Assert.Equal(250m, Assert.Single(plans).Value);
        var view = (await sp.GetRequiredService<DataChangeService>().GetAsync(set.Id))!;
        Assert.Single(view.Items, i => i.Status == DataChangeItemStatus.RollbackConflict);
    }

    [PostgresFact]
    public async Task WriteErrorRollsBackWholePlan()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var (_, set) = await PrepareViaChatAsync(sp, Report);

        // Регион удалили между подготовкой и подтверждением: запись падает — ни одной строки не должно остаться.
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var region = new SalesRegion { LinkoBranchId = 77, Name = "Временный" };
        db.SalesRegions.Add(region);
        await db.SaveChangesAsync();
        await db.DataChangeItems.Where(i => i.ChangeSetId == set.Id && i.Label.StartsWith("Бухоро · Бамбук"))
            .ExecuteUpdateAsync(u => u.SetProperty(i => i.EntityKey, RegionPlanPlanner.Key(PlanKind.Rop, region.Id, 2026, 9, 4)));
        await db.SalesRegions.Where(r => r.Id == region.Id).ExecuteDeleteAsync();

        await Assert.ThrowsAsync<DataManagerException>(() => sp.GetRequiredService<IDataChangeApplier>().ApplyAsync(set.Id, fixture.AdminId));

        Assert.Empty(await PlansAsync(sp));
        using var check = fixture.Services.CreateScope();
        var status = await check.ServiceProvider.GetRequiredService<IAppDbContext>().DataChangeSets.AsNoTracking()
            .Where(s => s.Id == set.Id).Select(s => s.Status).SingleAsync();
        Assert.Equal(DataChangeSetStatus.Draft, status); // транзакция откатилась целиком
    }

    [PostgresFact]
    public async Task OnlyDataManagePermissionCanPrepareAndApply_FilesArePrivate()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var fileId = await UploadAsync(sp, Report);
        var tools = sp.GetRequiredService<DataManagerTools>();
        var viewer = new DataToolContext(fixture.ViewerId, Guid.NewGuid(), new HashSet<string> { Permissions.SalesManage, Permissions.SalesRead });

        var prepare = await tools.ExecuteAsync(viewer, "prepare_region_plan_update",
            JsonSerializer.SerializeToElement(new { kind = "rop", mode = "upsert", fileId, mapping = Mapping }), CancellationToken.None);
        Assert.True(prepare.IsError);
        Assert.Contains("нет права", prepare.Content);

        // «Админ 2» работает в AI Data Manager, но чужой файл не видит.
        var dataAdmin = new DataToolContext(fixture.DataAdminId, Guid.NewGuid(),
            new HashSet<string> { Permissions.FilesRead, Permissions.FilesWrite, Permissions.DataManage });
        var read = await tools.ExecuteAsync(dataAdmin, "read_report", JsonSerializer.SerializeToElement(new { fileId }), CancellationToken.None);
        Assert.True(read.IsError);
        Assert.Contains("не найден", read.Content);

        var (_, set) = await PrepareViaChatAsync(sp, Report);
        await Assert.ThrowsAsync<DataManagerException>(() => sp.GetRequiredService<IDataChangeApplier>().ApplyAsync(set.Id, fixture.ViewerId));
        Assert.Empty(await PlansAsync(sp));

        // Права на раздел «Продажи» для подтверждения не нужны — достаточно ai.data.manage.
        var applied = await sp.GetRequiredService<IDataChangeApplier>().ApplyAsync(set.Id, fixture.DataAdminId);
        Assert.Equal(DataChangeSetStatus.Applied, applied.Status);
        Assert.Equal(4, (await PlansAsync(sp)).Count);

        // replace без подтверждения полноты отчёта — отказ.
        var admin = new DataToolContext(fixture.AdminId, Guid.NewGuid(), new HashSet<string> { Permissions.DataManage, Permissions.SalesRead, Permissions.SalesManage });
        var replace = await tools.ExecuteAsync(admin, "prepare_region_plan_update",
            JsonSerializer.SerializeToElement(new { kind = "rop", mode = "replace", fileId, mapping = Mapping }), CancellationToken.None);
        Assert.True(replace.IsError);
        Assert.Contains("completePeriod", replace.Content);
    }

    [PostgresFact]
    public async Task ClaudeApiError_TurnFailsCleanly_NoToolsRepeated()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var fileId = await UploadAsync(sp, Report);
        fixture.Model.Script.Enqueue(_ => FakeDataModel.Tool("read_report", new { fileId }));
        fixture.Model.Script.Enqueue(_ => throw new DataManagerModelException("Claude API временно недоступен. Повторите позже.", transient: true));
        fixture.Model.Script.Enqueue(_ => throw new DataManagerModelException("Claude API временно недоступен. Повторите позже.", transient: true));

        var events = new List<ChatEvent>();
        await sp.GetRequiredService<DataManagerChatService>()
            .SendAsync(fixture.AdminId, null, "Обнови план", [fileId], Collect(events), CancellationToken.None);

        Assert.Contains(events, e => e.Type == "error");
        Assert.Equal(3, fixture.Model.Calls.Count); // read_report выполнен один раз, затем запрос и один повтор
        var db = sp.GetRequiredService<IAppDbContext>();
        var conversation = await db.DataConversations.AsNoTracking().SingleAsync();
        Assert.Null(conversation.BusySince);
        var transcript = DataManagerChatService.Transcript(conversation);
        Assert.Equal("assistant", transcript[^1].Role);
        Assert.StartsWith("(Ход прерван", transcript[^1].Content[0].Text);
        var answer = await db.DataMessages.AsNoTracking().SingleAsync(m => m.Role == DataMessageRole.Assistant);
        Assert.Equal(DataMessageStatus.Failed, answer.Status);

        // Следующее сообщение продолжает ту же переписку без ошибок формата.
        fixture.Model.Script.Enqueue(_ => FakeDataModel.Text("Продолжаю."));
        await sp.GetRequiredService<DataManagerChatService>()
            .SendAsync(fixture.AdminId, conversation.Id, "Повтори", [], Collect(events), CancellationToken.None);
        Assert.Equal("done", events[^1].Type);
    }

    [PostgresFact]
    public async Task LinkoIsReadOnly_FactOnlyCompared_ExcludedBranchIsConflict()
    {
        await fixture.ResetAsync();
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IAppDbContext>();
        var ordersBefore = await db.LinkoOrders.CountAsync();
        var fileId = await UploadAsync(sp, "Регион;Факт, кг\nХоразм;950\nБухоро;500\nЗавод;10000\n", "fact.csv");
        var tools = sp.GetRequiredService<DataManagerTools>();
        var admin = new DataToolContext(fixture.AdminId, Guid.NewGuid(), new HashSet<string> { Permissions.DataManage, Permissions.SalesRead, Permissions.SalesManage });

        var compare = await tools.ExecuteAsync(admin, "compare_report_fact", JsonSerializer.SerializeToElement(new
        {
            fileId, headerRow = 1, regionColumn = "A", valueColumn = "B", metric = "kg", year = 2026, month = 9,
        }), CancellationToken.None);

        Assert.False(compare.IsError, compare.Content);
        using var json = JsonDocument.Parse(compare.Content);
        var khorazm = json.RootElement.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("region").GetString() == "Хоразм");
        Assert.Equal(900m, khorazm.GetProperty("oneBase").GetDecimal());
        Assert.Equal(50m, khorazm.GetProperty("diff").GetDecimal());
        var factory = json.RootElement.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("reportRegion").GetString() == "Завод");
        Assert.Contains("не найден", factory.GetProperty("problem").GetString());

        var plan = await sp.GetRequiredService<DataChangeService>().PrepareRegionPlansAsync(new DataActor(fixture.AdminId, null),
            new RegionPlanRequest(PlanKind.Rop, false, null, null, [new PlanValue("Завод", "Кекс", 2026, 9, 100, "чат")], null));
        Assert.Equal(DataChangeAction.Conflict, Assert.Single(plan.Items).Action);
        Assert.Equal(ordersBefore, await db.LinkoOrders.CountAsync());
    }

    private static PlanSheetMapping ToMapping() =>
        new(null, 1, "Регион", [new PlanColumn("Кекс", null), new PlanColumn("Бамбук", null)], null, null, null, null, 2026, 9);
}

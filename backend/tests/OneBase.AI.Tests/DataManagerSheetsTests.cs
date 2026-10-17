using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.DataManager;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.DataSources;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// AI Data Manager и подключённые таблицы Google Sheets: «бери план первички из такой-то таблицы» — модель находит таблицу, читает лист
/// в чат (дальше как с отчётом), берёт только нужный период; поле, которое ведёт привязка, обновляет её синхронизацией — сразу, по
/// подтверждённому при настройке сопоставлению.
/// </summary>
public class DataManagerSheetsTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Header = ["Регион", "Категория", "Месяц", "План, кг", "Дилер"];

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    /// <summary>Результат инструмента без \uXXXX — для проверки текста.</summary>
    private static string Text(string content) => System.Text.RegularExpressions.Regex.Unescape(content);

    [PostgresFact]
    public async Task Data_manager_takes_the_needed_period_from_a_connected_google_sheet()
    {
        await fixture.ResetAsync();
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 17, 6, 0, 0, TimeSpan.Zero);
        // История за 30 месяцев: май 2024 — октябрь 2026, Хоразм 100 и Бухоро 50 кг кекса в месяц.
        var rows = Enumerable.Range(0, 30).Select(i => new DateOnly(2024, 5, 1).AddMonths(i))
            .SelectMany(m => new[] { new[] { "Хоразм", "Кекс", $"{m:yyyy-MM}", "100", "" }, new[] { "Бухоро", "Кекс", $"{m:yyyy-MM}", "50", "" } });
        fixture.Sheets.Document = new ReportDocument([new ReportSheet("План", new[] { Header }.Concat(rows).Select(r => (IReadOnlyList<string>)r).ToList(), 61, 5, false)]);
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var sources = sp.GetRequiredService<DataSourceService>();
        var tools = sp.GetRequiredService<DataManagerTools>();
        var db = sp.GetRequiredService<OneBaseDbContext>();
        var connection = await sources.CreateConnectionAsync(new ConnectionInput("План первички 2026", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms", "link", null),
            fixture.AdminId, CancellationToken.None);
        var conversation = new DataConversation { UserId = fixture.AdminId, Title = "План из Google Sheets" };
        db.DataConversations.Add(conversation);
        await db.SaveChangesAsync();
        var context = new DataToolContext(fixture.AdminId, conversation.Id, new HashSet<string> { Permissions.DataManage });

        // Модель находит таблицу и читает лист в чат файлом.
        var list = await tools.ExecuteAsync(context, "list_google_sheets", Json(new { }), CancellationToken.None);
        Assert.False(list.IsError);
        Assert.Contains("План первички 2026", Text(list.Content));
        var loaded = await tools.ExecuteAsync(context, "load_google_sheet", Json(new { connectionId = connection.Id }), CancellationToken.None);
        Assert.False(loaded.IsError, loaded.Content);
        var fileId = JsonDocument.Parse(loaded.Content).RootElement.GetProperty("file").GetProperty("id").GetString()!;
        var read = await tools.ExecuteAsync(context, "read_report", Json(new { fileId, rowCount = 3 }), CancellationToken.None);
        Assert.Contains("План, кг", Text(read.Content));

        // Вся история — больше 24 месяцев: понятная ошибка; нужный период (2026) — план только этих месяцев.
        object Plan(string? from, string? to) => new
        {
            kind = "primary", mode = "upsert", fileId,
            mapping = new { headerRow = 1, regionColumn = "Регион", categoryColumn = "Категория", valueColumn = "План, кг", monthColumn = "Месяц" },
            fromMonth = from, toMonth = to,
        };
        var history = await tools.ExecuteAsync(context, "prepare_region_plan_update", Json(Plan(null, null)), CancellationToken.None);
        Assert.True(history.IsError);
        Assert.Contains("24", Text(history.Content));
        var year = await tools.ExecuteAsync(context, "prepare_region_plan_update", Json(Plan("2026-01", "2026-10")), CancellationToken.None);
        Assert.False(year.IsError, year.Content);
        var draft = await db.DataChangeSets.AsNoTracking().Include(s => s.Items).OrderByDescending(s => s.CreatedAt).FirstAsync();
        Assert.Equal(DataChangeSetStatus.Draft, draft.Status);
        Assert.Equal(20, draft.Items.Count(i => i.Action == DataChangeAction.Create)); // 10 месяцев × 2 региона
        Assert.Equal("2026-01…2026-10", draft.Period);

        // План первички ведёт привязка: чат его напрямую не меняет — обновляет синхронизацией привязки, сразу.
        var binding = await sources.SaveBindingAsync(null, new BindingInput("primary", connection.Id, ["План"], 1,
            [new DataSourceFieldBinding(DataSourceCatalog.PlanPrimary, new PlanFieldMapping("Регион", "Категория", "План, кг", null, "Месяц", null, null, null),
                Period: new PeriodFilter(DataSourceRules.PeriodYear))], "interval", 30, null, true), fixture.AdminId, CancellationToken.None);
        var refused = await tools.ExecuteAsync(context, "prepare_region_plan_update", Json(Plan("2026-01", "2026-10")), CancellationToken.None);
        Assert.True(refused.IsError);
        Assert.Contains("ведёт Google Sheets", Text(refused.Content));
        var list2 = await tools.ExecuteAsync(context, "list_google_sheets", Json(new { }), CancellationToken.None);
        Assert.Contains(binding.Id.ToString(), list2.Content);
        Assert.Contains("текущий год", Text(list2.Content));

        var synced = await tools.ExecuteAsync(context, "sync_google_sheet_binding", Json(new { bindingId = binding.Id }), CancellationToken.None);
        Assert.False(synced.IsError, synced.Content);
        Assert.Equal("ok", JsonDocument.Parse(synced.Content).RootElement.GetProperty("status").GetString());
        var plans = await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).ToListAsync();
        Assert.Equal((10, 1500m), (plans.Select(p => (p.Year, p.Month)).Distinct().Count(), plans.Sum(p => p.PlanKg)));
        Assert.Equal("chat", (await db.DataSourceSyncRuns.AsNoTracking().SingleAsync(r => r.BindingId == binding.Id)).Trigger);
    }
}

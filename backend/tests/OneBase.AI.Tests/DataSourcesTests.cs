using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Domain.AI;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.DataSources;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Правила источников данных: ссылка на таблицу, расписание, поля привязки, дилеры из листа, ключ сервисного аккаунта.</summary>
public sealed class DataSourceRulesTests
{
    private static ReportDocument Doc(params string[][] rows) =>
        new([new ReportSheet("План", rows.Select(r => (IReadOnlyList<string>)r).ToList(), rows.Length, rows.Max(r => r.Length), false)]);

    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms/edit#gid=0", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms")]
    [InlineData("docs.google.com/spreadsheets/d/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms")]
    [InlineData(" 1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms ", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms")]
    [InlineData("https://example.com/table", null)]
    [InlineData("", null)]
    public void Spreadsheet_id_comes_from_the_link_or_is_the_id_itself(string input, string? expected) =>
        Assert.Equal(expected, DataSourceRules.SpreadsheetId(input));

    [Fact]
    public void Next_run_follows_the_interval_or_the_daily_time_in_Tashkent()
    {
        var now = new DateTimeOffset(2026, 10, 15, 4, 30, 0, TimeSpan.Zero); // 09:30 в Ташкенте
        Assert.Null(DataSourceRules.NextRun("manual", null, null, now));
        Assert.Equal(now.AddMinutes(30), DataSourceRules.NextRun("interval", 30, null, now));
        Assert.Equal(now.AddMinutes(5), DataSourceRules.NextRun("interval", 1, null, now)); // не чаще 5 минут
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero), DataSourceRules.NextRun("daily", null, "10:00", now)); // сегодня 10:00
        Assert.Equal(new DateTimeOffset(2026, 10, 16, 3, 0, 0, TimeSpan.Zero), DataSourceRules.NextRun("daily", null, "08:00", now)); // завтра 08:00
        Assert.NotNull(DataSourceRules.ScheduleError("daily", null, "25:00"));
        Assert.NotNull(DataSourceRules.ScheduleError("interval", 2, null));
        Assert.Null(DataSourceRules.ScheduleError("interval", 15, null));
    }

    [Fact]
    public void Fields_must_belong_to_the_section_have_columns_and_appear_once()
    {
        var plan = new DataSourceFieldBinding(DataSourceCatalog.PlanPrimary, new PlanFieldMapping("Регион", "Категория", "План", null, "Месяц", null, null, null));
        Assert.Null(DataSourceRules.FieldsError("primary", [plan]));
        Assert.Contains("не относится", DataSourceRules.FieldsError("secondary", [plan]));
        Assert.Contains("хотя бы одно", DataSourceRules.FieldsError("primary", []));
        Assert.Contains("дважды", DataSourceRules.FieldsError("primary", [plan, plan]));
        Assert.Contains("месяц", DataSourceRules.FieldsError("primary", [plan with { Plan = plan.Plan! with { MonthColumn = null } }]));
        Assert.Contains("столбцы региона", DataSourceRules.FieldsError("primary", [new DataSourceFieldBinding(DataSourceCatalog.Dealer)]));
        Assert.Empty(DataSourceCatalog.Section("outstock")!.Fields); // аутсток — только Linko
    }

    [Fact]
    public void Current_month_is_Tashkent_and_the_fingerprint_ignores_order()
    {
        Assert.Equal((2026, 11), DataSourceRules.CurrentPeriod(new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.Zero))); // 01:00 1 ноября в Ташкенте
        Assert.Equal(DataSourceRules.Fingerprint(["Хоразм|Кекс|1000", "Бухоро|Кекс|400"]), DataSourceRules.Fingerprint(["бухоро|кекс|400", "Хоразм|Кекс|1000"]));
        Assert.NotEqual(DataSourceRules.Fingerprint(["Хоразм|Кекс|1000"]), DataSourceRules.Fingerprint(["Хоразм|Кекс|1200"]));
        Assert.True(DataSourceRules.SameAsPreviousMonth("2026-11", "A", "2026-10", "A"));
        Assert.False(DataSourceRules.SameAsPreviousMonth("2026-11", "B", "2026-10", "A")); // таблицу обновили
        Assert.False(DataSourceRules.SameAsPreviousMonth("2026-10", "A", "2026-10", "A")); // тот же месяц
        Assert.False(DataSourceRules.SameAsPreviousMonth("2026-11", "A", null, null)); // первая синхронизация
        Assert.Equal("waiting", DataSourceRules.Overall(["ok", "waiting"]));
        Assert.Equal("error", DataSourceRules.Overall(["waiting", "error", "ok"]));
        Assert.Equal("unchanged", DataSourceRules.Overall(["unchanged"]));
    }

    [Fact]
    public void Dealer_values_skip_empty_cells_and_totals()
    {
        var doc = Doc(["Регион", "Дилер"], ["Хоразм", "Азизбек Хоразм"], ["Бухоро", ""], ["", "Без региона"], ["Итого", "—"]);
        var values = DataSourceRules.ProfileValues(doc, "План", 1, new ProfileFieldMapping("Регион", "B"), "dealer");
        var one = Assert.Single(values);
        Assert.Equal(("Хоразм", "dealer", "Азизбек Хоразм", "План!B2"), (one.Region, one.Field, one.Value, one.SourceRef));
    }

    [Fact]
    public void Service_account_key_signs_a_jwt_for_drive_readonly()
    {
        using var rsa = RSA.Create(2048);
        var json = JsonSerializer.Serialize(new
        {
            type = "service_account",
            client_email = "onebase@project.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
            token_uri = "https://oauth2.googleapis.com/token",
        });
        var key = ServiceAccountKey.Parse(json);
        var jwt = GoogleSheetsClient.Jwt(key, new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));

        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);
        static byte[] Decode(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
        using var claims = JsonDocument.Parse(Decode(parts[1]));
        Assert.Equal("onebase@project.iam.gserviceaccount.com", claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal("https://www.googleapis.com/auth/drive.readonly", claims.RootElement.GetProperty("scope").GetString());
        Assert.True(rsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.Throws<DataSourceException>(() => ServiceAccountKey.Parse("{\"type\":\"user\"}"));
        Assert.Throws<DataSourceException>(() => ServiceAccountKey.Parse("не json"));
    }
}

/// <summary>
/// Клиент Google Sheets на подставном HTTP: сервисным аккаунтом читаются только выбранные листы (Sheets API), значения — в записи разбора xlsx,
/// отказ Google объясняется его ответом, а не «нет доступа» на всё; книга больше 10 МБ — выгрузкой docs.google.com с токеном.
/// </summary>
public sealed class GoogleSheetsClientTests
{
    private const string Id = "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms";

    private static JsonElement Range(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Values_match_the_xlsx_reading_dates_numbers_and_errors()
    {
        // 46266 — 1 сентября 2026; 0.5 — время 12:00; 1.5 — длительность 36:00:00.
        var serial = Range("""{"values":[["Регион","Месяц","План",""],["Хоразм",46266,1234.5,true],["Бухоро",0.5,"#N/A",1.5]]}""");
        var shown = Range("""{"values":[["Регион","Месяц","План",""],["Хоразм","01.09.2026",1234.5,true],["Бухоро","12:00:00","#N/A","36:00:00"]]}""");
        var sheet = GoogleSheetsClient.Sheet("План", serial, shown, 1000, 26, 100, 10);

        Assert.Equal(new[] { "Хоразм", "2026-09-01", "1234.5", "да" }, sheet.Rows[1]);
        Assert.Equal(new[] { "Бухоро", "12:00:00", "#ОШИБКА", "36:00:00" }, sheet.Rows[2]);
        Assert.Equal((3, 4, false), (sheet.TotalRows, sheet.TotalColumns, sheet.Truncated));
    }

    [Fact]
    public void Rows_and_columns_over_the_limit_are_cut_and_marked()
    {
        var rows = string.Join(",", Enumerable.Range(1, 4).Select(i => $"[{i},2,3]"));
        var range = Range($$"""{"values":[{{rows}}]}""");
        var sheet = GoogleSheetsClient.Sheet("Лист", range, range, 5000, 40, 3, 2);

        Assert.Equal(3, sheet.Rows.Count);
        Assert.All(sheet.Rows, r => Assert.Equal(2, r.Count));
        Assert.Equal((5000, 40, true), (sheet.TotalRows, sheet.TotalColumns, sheet.Truncated));
        Assert.Empty(GoogleSheetsClient.Sheet("Пусто", Range("{}"), Range("{}"), 1000, 26, 3, 2).Rows);
    }

    [Fact]
    public void Unpacked_size_counts_only_the_sheets_of_the_book()
    {
        using var buffer = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, size) in new[] { ("xl/worksheets/sheet1.xml", 3000), ("xl/sharedStrings.xml", 500), ("xl/styles.xml", 9999) })
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(new byte[size]);
            }
        }

        Assert.Equal(3500, GoogleSheetsClient.UnpackedSize(buffer.ToArray()));
        Assert.Equal(0, GoogleSheetsClient.UnpackedSize("не zip"u8.ToArray()));
    }

    [Fact]
    public async Task Service_account_reads_only_the_chosen_sheets_through_the_sheets_api()
    {
        var google = new FakeGoogle();
        google.On("sheets.googleapis.com/v4/spreadsheets/" + Id + "?", 200, """{"sheets":[{"properties":{"title":"DSO","sheetType":"GRID","gridProperties":{"rowCount":11000,"columnCount":115}}},{"properties":{"title":"План","sheetType":"GRID","gridProperties":{"rowCount":1000,"columnCount":26}}},{"properties":{"title":"Диаграмма","sheetType":"OBJECT"}}]}""");
        google.On("values:batchGet", 200, """{"valueRanges":[{"range":"'План'!A1:Z1000","values":[["Регион","План"],["Хоразм",1000]]}]}""");
        var (client, source) = Client(google);

        var sheets = await client.SheetsAsync(source);
        Assert.Equal(new[] { "DSO", "План" }, sheets.Select(s => s.Name));

        var document = await client.ReadAsync(source, ["план"]);
        var sheet = Assert.Single(document.Sheets);
        Assert.Equal("План", sheet.Name);
        Assert.Equal(new[] { "Хоразм", "1000" }, sheet.Rows[1]);
        var reads = google.Requests.Where(r => r.Contains("values:batchGet")).ToList();
        Assert.Equal(2, reads.Count); // даты числом и даты строкой
        Assert.All(reads, r => Assert.Contains("ranges='План'!A1:BI20001", r));
        Assert.DoesNotContain(google.Requests, r => r.Contains("DSO"));

        var missing = await Assert.ThrowsAsync<DataSourceException>(() => client.ReadAsync(source, ["Нет такого"]));
        Assert.Contains("Листы: DSO, План", missing.Message);
    }

    [Fact]
    public async Task Refusal_quotes_google_instead_of_blaming_access_for_everything()
    {
        var google = new FakeGoogle();
        google.On("sheets.googleapis.com/v4/spreadsheets/", 403, """{"error":{"code":403,"message":"The caller does not have permission","status":"PERMISSION_DENIED"}}""");
        var (client, source) = Client(google);

        var error = await Assert.ThrowsAsync<DataSourceException>(() => client.SheetsAsync(source));
        Assert.Contains("поделитесь ею с onebase@project.iam.gserviceaccount.com", error.Message);
        Assert.Contains("The caller does not have permission", error.Message);
    }

    [Fact]
    public async Task Book_over_the_drive_export_limit_is_read_through_the_docs_export_with_the_token()
    {
        // Sheets API выключен в проекте → файл через Drive; files.export отвечает «больше 10 МБ» → выгрузка docs.google.com.
        var google = new FakeGoogle();
        google.On("sheets.googleapis.com", 403, """{"error":{"code":403,"message":"Google Sheets API has not been used in project","status":"PERMISSION_DENIED","details":[{"@type":"type.googleapis.com/google.rpc.ErrorInfo","reason":"SERVICE_DISABLED"}]}}""");
        google.On("/export?mimeType=", 403, """{"error":{"code":403,"message":"This file is too large to be exported.","errors":[{"message":"This file is too large to be exported.","domain":"global","reason":"exportSizeLimitExceeded"}]}}""");
        google.On("docs.google.com/spreadsheets/d/" + Id + "/export?format=xlsx", 200, "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var (client, source) = Client(google);

        var document = await client.ReadAsync(source, ["План"]);
        Assert.Equal("План", Assert.Single(document.Sheets).Name);
        var docs = google.Requests.Single(r => r.Contains("docs.google.com"));
        Assert.Contains(docs, google.Authorized);
    }

    private static (GoogleSheetsClient Client, SheetSource Source) Client(FakeGoogle google)
    {
        using var rsa = RSA.Create(2048);
        var key = JsonSerializer.Serialize(new
        {
            type = "service_account",
            // Своя почта на тест: токены кэшируются по почте сервисного аккаунта.
            client_email = "onebase@project.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
            token_uri = $"https://oauth2.googleapis.com/token/{Guid.NewGuid():N}",
        });
        google.On("oauth2.googleapis.com/token", 200, """{"access_token":"test-token","expires_in":3600}""");
        var reader = new FixedReader(new ReportDocument([
            new ReportSheet("DSO", [["Дата"]], 1, 1, false),
            new ReportSheet("План", [["Регион", "План"]], 1, 2, false),
        ]));
        var client = new GoogleSheetsClient(new HttpClient(google), reader, new DataManagerOptions());
        return (client, new SheetSource(Id, OneBase.Domain.Integrations.SheetAccess.ServiceAccount, key));
    }

    private sealed class FixedReader(ReportDocument document) : IReportReader
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".xlsx"];

        public ReportDocument Read(Stream content, string fileName) => document;
    }

    /// <summary>Подставной Google: ответ по первой подходящей части адреса; запоминает запросы (адрес без %-кодирования) и какие шли с токеном.</summary>
    private sealed class FakeGoogle : HttpMessageHandler
    {
        private readonly List<(string Part, int Status, string Body, string Type)> _routes = [];
        public List<string> Requests { get; } = [];
        public List<string> Authorized { get; } = [];

        public void On(string part, int status, string body, string type = "application/json") => _routes.Add((part, status, body, type));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);
            Requests.Add(url);
            if (request.Headers.Authorization?.Parameter == "test-token")
            {
                Authorized.Add(url);
            }

            var route = _routes.FirstOrDefault(r => url.Contains(r.Part, StringComparison.Ordinal));
            var response = new HttpResponseMessage((System.Net.HttpStatusCode)(route.Part is null ? 404 : route.Status))
            {
                Content = new StringContent(route.Body ?? "{}", Encoding.UTF8, route.Type ?? "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}

/// <summary>
/// Синхронизация Google Sheets на настоящем PostgreSQL: обновляются только выбранные поля, повтор без изменений ничего не пишет,
/// строки с ошибкой не записываются, одно поле — одна привязка, расписание запускает только подошедшие привязки.
/// </summary>
public class DataSourceSyncTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static ReportDocument Sheet(params string[][] rows) =>
        new([new ReportSheet("План", rows.Select(r => (IReadOnlyList<string>)r).ToList(), rows.Length, rows.Max(r => r.Length), false)]);

    private static readonly string[] Header = ["Регион", "Категория", "Месяц", "План, кг", "Дилер"];

    private static DataSourceFieldBinding PlanField => new(DataSourceCatalog.PlanPrimary,
        new PlanFieldMapping("Регион", "Категория", "План, кг", null, "Месяц", null, null, null));

    private static DataSourceFieldBinding DealerField => new(DataSourceCatalog.Dealer, Profile: new ProfileFieldMapping("Регион", "Дилер"));

    [PostgresFact]
    public async Task Only_selected_fields_are_written_and_errors_write_nothing()
    {
        await fixture.ResetAsync();
        fixture.Sheets.Document = Sheet(Header,
            ["Хоразм", "Кекс", "2026-09", "1 000", "Азизбек Хоразм"],
            ["Бухоро", "Бамбук", "сентябрь 2026", "500", "Мехриддин Бухоро"]);
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();

        var connection = await sources.CreateConnectionAsync(new ConnectionInput("План первички", "https://docs.google.com/spreadsheets/d/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms/edit", "link", null),
            fixture.AdminId, CancellationToken.None);
        Assert.True(connection.LastCheckOk);
        Assert.Equal("План", Assert.Single(connection.Sheets).Name);

        // Выбран только план первички: дилер из того же листа не трогается.
        var binding = await sources.SaveBindingAsync(null, new BindingInput("primary", connection.Id, ["План"], 1, [PlanField], "interval", 30, null, true),
            fixture.AdminId, CancellationToken.None);
        Assert.NotNull(binding.NextRunAt);

        var first = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", first.Status);
        var plans = await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).ToListAsync();
        Assert.Equal(1500m, plans.Sum(p => p.PlanKg));
        Assert.All(plans, p => Assert.Equal((2026, 9), (p.Year, p.Month)));
        Assert.Null(await db.SalesRegions.AsNoTracking().Where(r => r.Name == "Бухоро").Select(r => r.DealerName).FirstAsync());
        var set = await db.DataChangeSets.AsNoTracking().FirstAsync(s => s.Id == first.Fields[0].ChangeSetId);
        Assert.Equal(DataChangeSetStatus.Applied, set.Status); // запись — планом изменений AI Data Manager, видна в журнале
        Assert.Contains("Google Sheets «План первички» / План", set.Title);

        // Повтор без изменений таблицы ничего не пишет.
        var again = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("unchanged", again.Status);

        // Добавили поле «Дилер» — теперь обновляется и он.
        await sources.SaveBindingAsync(binding.Id, new BindingInput("primary", connection.Id, ["План"], 1, [PlanField, DealerField], "manual", null, null, true),
            fixture.AdminId, CancellationToken.None);
        var withDealer = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", withDealer.Status);
        Assert.Equal(["unchanged", "ok"], withDealer.Fields.Select(f => f.Status));
        Assert.Equal("Мехриддин Бухоро", await db.SalesRegions.AsNoTracking().Where(r => r.Name == "Бухоро").Select(r => r.DealerName).FirstAsync());

        // Неизвестный регион в таблице: план не записывается совсем, пока таблицу не исправят.
        fixture.Sheets.Document = Sheet(Header,
            ["Хоразм", "Кекс", "2026-09", "2 000", "Азизбек Хоразм"],
            ["Ташкентская обл.", "Бамбук", "2026-09", "700", ""]);
        var broken = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("conflicts", broken.Status);
        var planResult = broken.Fields.Single(f => f.Field == DataSourceCatalog.PlanPrimary);
        Assert.Equal(1, planResult.Conflicts);
        Assert.Contains(planResult.Problems!, p => p.Contains("Ташкентская"));
        Assert.Equal(1500m, (await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).ToListAsync()).Sum(p => p.PlanKg));

        var runs = await sources.RunsAsync(binding.Id, CancellationToken.None);
        Assert.Equal(["conflicts", "ok", "unchanged", "ok"], runs.Select(r => r.Status));
        Assert.Equal("conflicts", (await db.DataSourceBindings.AsNoTracking().FirstAsync(b => b.Id == binding.Id)).LastStatus);
    }

    /// <summary>
    /// Лист на месяц: «Сентябрь 2026» и «Октябрь 2026» с одинаковой разметкой и без столбца месяца — месяц из названия листа;
    /// «все листы» подхватывают и новый лист; лист без месяца в названии — ошибка поля; разные дилеры региона на листах — ошибка.
    /// </summary>
    [PostgresFact]
    public async Task Several_sheets_are_read_together_with_the_month_from_the_sheet_name()
    {
        await fixture.ResetAsync();
        string[] header = ["Регион", "Категория", "План", "Дилер"];
        ReportSheet Tab(string name, params string[][] rows) =>
            new(name, new[] { header }.Concat(rows).Select(r => (IReadOnlyList<string>)r).ToList(), rows.Length + 1, 4, false);
        fixture.Sheets.Document = new ReportDocument([
            Tab("Сентябрь 2026", ["Хоразм", "Кекс", "1000", "Азизбек Хоразм"], ["Бухоро", "Кекс", "400", "Мехриддин Бухоро"]),
            Tab("Октябрь 2026", ["Хоразм", "Кекс", "1100", "Азизбек Хоразм"]),
            Tab("Справка", ["—", "—", "—", "—"]),
        ]);
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var connection = await sources.CreateConnectionAsync(new ConnectionInput("План по месяцам", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms", "link", null),
            fixture.AdminId, CancellationToken.None);
        var byMonth = new DataSourceFieldBinding(DataSourceCatalog.PlanPrimary, new PlanFieldMapping("Регион", "Категория", "План", null, null, null, 2026, null),
            MonthFromSheet: true);

        var binding = await sources.SaveBindingAsync(null, new BindingInput("primary", connection.Id, ["Сентябрь 2026", "Октябрь 2026", " Сентябрь 2026 "], 1,
            [byMonth, DealerField], "manual", null, null, true), fixture.AdminId, CancellationToken.None);
        Assert.Equal(["Сентябрь 2026", "Октябрь 2026"], binding.Sheets); // без повторов

        var run = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", run.Status);
        var plans = await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).ToListAsync();
        Assert.Equal(1400m, plans.Where(p => p.Month == 9).Sum(p => p.PlanKg));
        Assert.Equal(1100m, plans.Where(p => p.Month == 10).Sum(p => p.PlanKg));
        Assert.Equal("Мехриддин Бухоро", await db.SalesRegions.AsNoTracking().Where(r => r.Name == "Бухоро").Select(r => r.DealerName).FirstAsync());

        // «Все листы»: лист «Справка» без месяца в названии — план не пишется, ошибка понятна.
        await sources.SaveBindingAsync(binding.Id, new BindingInput("primary", connection.Id, ["Сентябрь 2026", DataSourceRules.AllSheets], 1, [byMonth],
            "manual", null, null, true), fixture.AdminId, CancellationToken.None);
        var all = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("error", all.Status);
        Assert.Contains("Справка", all.Fields.Single().Message);

        // Пропавший лист и разные дилеры одного региона на листах.
        fixture.Sheets.Document = new ReportDocument([
            Tab("Сентябрь 2026", ["Бухоро", "Кекс", "400", "Мехриддин Бухоро"]),
            Tab("Октябрь 2026", ["Бухоро", "Кекс", "450", "Новый дилер"]),
        ]);
        await sources.SaveBindingAsync(binding.Id, new BindingInput("primary", connection.Id, ["Сентябрь 2026", "Октябрь 2026"], 1, [DealerField],
            "manual", null, null, true), fixture.AdminId, CancellationToken.None);
        var dealers = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("error", dealers.Status);
        Assert.Contains("разные значения", dealers.Fields.Single().Message);
        Assert.Equal("Мехриддин Бухоро", await db.SalesRegions.AsNoTracking().Where(r => r.Name == "Бухоро").Select(r => r.DealerName).FirstAsync());

        await sources.SaveBindingAsync(binding.Id, new BindingInput("primary", connection.Id, ["Ноябрь 2026"], 1, [DealerField], "manual", null, null, true),
            fixture.AdminId, CancellationToken.None);
        var missing = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("error", missing.Status);
        Assert.Contains("нет листов «Ноябрь 2026»", missing.Message);
        Assert.Equal("Выберите хотя бы один лист таблицы.", DataSourceRules.CleanSheets([]).Error);
    }

    /// <summary>
    /// Лист «План» всегда про текущий месяц: в октябре пишет октябрь; 1 ноября по расписанию те же цифры в ноябрь не копирует (ждёт
    /// обновления таблицы), кнопкой — пишет; обновлённая таблица по расписанию идёт в ноябрь. Начало месяца без плана — уведомление.
    /// </summary>
    [PostgresFact]
    public async Task Current_month_moves_to_the_new_month_without_copying_the_old_plan()
    {
        await fixture.ResetAsync();
        string[] header = ["Регион", "Категория", "План"];
        ReportDocument Plan(string khorazm) =>
            new([new ReportSheet("План", new[] { header, ["Хоразм", "Кекс", khorazm], ["Бухоро", "Кекс", "400"] }.Select(r => (IReadOnlyList<string>)r).ToList(), 3, 3, false)]);
        fixture.Sheets.Document = Plan("1000");
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 20, 6, 0, 0, TimeSpan.Zero);
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var connection = await sources.CreateConnectionAsync(new ConnectionInput("План", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms", "link", null),
            fixture.AdminId, CancellationToken.None);
        var current = new DataSourceFieldBinding(DataSourceCatalog.PlanPrimary, new PlanFieldMapping("Регион", "Категория", "План", null, null, null, null, null),
            CurrentMonth: true);
        var binding = await sources.SaveBindingAsync(null, new BindingInput("primary", connection.Id, ["План"], 1, [current], "daily", null, "07:00", true),
            fixture.AdminId, CancellationToken.None);
        async Task<decimal> Month(int month) =>
            (await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary && p.Year == 2026 && p.Month == month).ToListAsync()).Sum(p => p.PlanKg);
        async Task<string?> Scheduled()
        {
            await db.DataSourceBindings.Where(b => b.Id == binding.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.NextRunAt, fixture.Clock.Now!.Value.AddMinutes(-1)));
            await sources.RunDueAsync(CancellationToken.None);
            return (await db.DataSourceBindings.AsNoTracking().FirstAsync(b => b.Id == binding.Id)).LastStatus;
        }

        Assert.Equal("ok", await Scheduled());
        Assert.Equal(1400m, await Month(10));

        // 1 ноября, 07:00 по Ташкенту: таблицу ещё не обновили — в ноябрь ничего не пишется, плана нет — уведомление (одно).
        fixture.Clock.Now = new DateTimeOffset(2026, 11, 1, 2, 0, 0, TimeSpan.Zero);
        Assert.Equal("waiting", await Scheduled());
        Assert.Equal(0m, await Month(11));
        Assert.Equal(1400m, await Month(10));
        await sources.CheckMonthPlansAsync(CancellationToken.None);
        await sources.CheckMonthPlansAsync(CancellationToken.None);
        var notice = Assert.Single(await db.UserNotifications.AsNoTracking().Where(n => n.RecipientId == fixture.AdminId).ToListAsync());
        Assert.Equal("Ноябрь 2026: «План первички» ещё не загружен", notice.Title);
        Assert.Equal("/settings/data-sources", notice.Link);
        Assert.Contains(await db.UserNotifications.AsNoTracking().Select(n => n.RecipientId).ToListAsync(), id => id == fixture.DataAdminId); // у кого право AI Data Manager
        Assert.DoesNotContain(await db.UserNotifications.AsNoTracking().Select(n => n.RecipientId).ToListAsync(), id => id == fixture.ViewerId);
        var month = (await sources.OverviewAsync(fixture.AdminId, CancellationToken.None)).MonthPlans.Single(p => p.Field == DataSourceCatalog.PlanPrimary);
        Assert.Equal(("Ноябрь 2026", 0, 2), (month.Period, month.Regions, month.PreviousRegions));

        // Таблицу обновили — по расписанию план идёт в ноябрь.
        fixture.Sheets.Document = Plan("1200");
        Assert.Equal("ok", await Scheduled());
        Assert.Equal(1600m, await Month(11));
        Assert.Equal(1400m, await Month(10)); // октябрь не тронут

        // План не изменился, а месяц новый: кнопка пишет как есть.
        fixture.Clock.Now = new DateTimeOffset(2026, 12, 1, 3, 0, 0, TimeSpan.Zero);
        Assert.Equal("waiting", await Scheduled());
        var manual = await sources.RunAsync(binding.Id, "manual", fixture.AdminId, CancellationToken.None);
        Assert.Equal("ok", manual.Status);
        Assert.Equal(1600m, await Month(12));
    }

    [PostgresFact]
    public async Task One_field_has_one_source_and_the_schedule_runs_only_due_bindings()
    {
        await fixture.ResetAsync();
        fixture.Sheets.Document = Sheet(Header, ["Хоразм", "Кекс", "2026-09", "800", "Азизбек Хоразм"]);
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<DataSourceService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var connection = await sources.CreateConnectionAsync(new ConnectionInput("План", "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms", "link", null),
            fixture.AdminId, CancellationToken.None);

        var due = await sources.SaveBindingAsync(null, new BindingInput("primary", connection.Id, ["План"], 1, [PlanField], "interval", 15, null, true),
            fixture.AdminId, CancellationToken.None);
        var error = await Assert.ThrowsAsync<DataSourceException>(() => sources.SaveBindingAsync(null,
            new BindingInput("plans", connection.Id, ["План"], 1, [PlanField], "manual", null, null, true), fixture.AdminId, CancellationToken.None));
        Assert.Contains("уже обновляется", error.Message);
        await Assert.ThrowsAsync<DataSourceException>(() => sources.SaveBindingAsync(null,
            new BindingInput("primary", connection.Id, ["План"], 1, [DealerField], "manual", null, null, true), fixture.ViewerId, CancellationToken.None)); // без права AI Data Manager

        var manual = await sources.SaveBindingAsync(null, new BindingInput("secondary", connection.Id, ["План"], 1, [DealerField], "manual", null, null, true),
            fixture.AdminId, CancellationToken.None);
        Assert.Null(manual.NextRunAt);

        await db.DataSourceBindings.Where(b => b.Id == due.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.NextRunAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await sources.RunDueAsync(CancellationToken.None);

        var ran = await db.DataSourceBindings.AsNoTracking().FirstAsync(b => b.Id == due.Id);
        Assert.Equal("ok", ran.LastStatus);
        Assert.True(ran.NextRunAt > DateTimeOffset.UtcNow.AddMinutes(10)); // следующая — через 15 минут
        Assert.Equal("schedule", (await sources.RunsAsync(due.Id, CancellationToken.None)).Single().Trigger);
        Assert.Null((await db.DataSourceBindings.AsNoTracking().FirstAsync(b => b.Id == manual.Id)).LastRunAt); // вручную — не по расписанию
        Assert.Equal(800m, (await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).ToListAsync()).Sum(p => p.PlanKg));

        await Assert.ThrowsAsync<DataSourceException>(() => sources.DeleteConnectionAsync(connection.Id, fixture.AdminId, CancellationToken.None)); // используется

        // Настроивший привязку потерял право «AI Data Manager»: по расписанию — понятная причина, а не «внутренняя ошибка».
        fixture.Sheets.Document = Sheet(Header, ["Хоразм", "Кекс", "2026-09", "900", "Азизбек Хоразм"]);
        await db.DataSourceBindings.Where(b => b.Id == due.Id).ExecuteUpdateAsync(u => u
            .SetProperty(b => b.CreatedById, fixture.ViewerId)
            .SetProperty(b => b.NextRunAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await sources.RunDueAsync(CancellationToken.None);
        var denied = await db.DataSourceBindings.AsNoTracking().FirstAsync(b => b.Id == due.Id);
        Assert.Equal("error", denied.LastStatus);
        Assert.Contains("AI Data Manager", denied.LastMessage);
        Assert.Equal(800m, (await db.SalesRegionPlans.AsNoTracking().Where(p => p.Kind == PlanKind.Primary).ToListAsync()).Sum(p => p.PlanKg)); // не записано
    }
}

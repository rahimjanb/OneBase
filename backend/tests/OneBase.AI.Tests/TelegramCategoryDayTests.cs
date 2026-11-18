using System.Text.RegularExpressions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Telegram.Reports;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>Образцовые итоги дня по категориям для тестов без базы (каталог, подпись, картинки, переводы).</summary>
public static class CategoryDaySamples
{
    /// <summary>
    /// Две категории: «Кекс» с планом дня, разбивкой (rows строк; последняя — без продаж за день) и артикулами дня, «Бамбук» без плана и без
    /// продаж за день. Имена — как у категорий отчёта (данные, они не переводятся).
    /// </summary>
    public static CategoryDayCards Cards(string slot, DateOnly day, string role, int rows = 4, bool photos = true, DateOnly? through = null, string? holiday = null)
    {
        var days = Enumerable.Range(0, 14).Select(i => day.AddDays(i - 13))
            .Select((d, i) => new CategoryDayPoint(d, d.DayOfWeek == DayOfWeek.Sunday ? 0 : i == 13 ? 640.5m : 380 + i * 22 + (i % 3) * 1_400)).ToList();
        var kind = role switch
        {
            TelegramReportRoles.Rm => CategorySalesCards.Regions,
            TelegramReportRoles.Sv => CategorySalesCards.Agents,
            _ => null,
        };
        IReadOnlyList<CategoryDayRow> breakdown = kind is null
            ? []
            : Enumerable.Range(0, rows).Select(i =>
            {
                var kg = i == rows - 1 ? 0 : 260m - i * 40;
                decimal? plan = i % 3 == 2 ? null : 220;
                return new CategoryDayRow(kind == CategorySalesCards.Regions ? $"Регион {i + 1}" : $"Агент {i + 1}", kg, plan, plan is { } p ? kg / p : null,
                    kg * 33_000, Math.Max(0, 40 - i * 5), Math.Max(0, 44 - i * 5));
            }).ToList();
        var photo = photos ? CategoryCardSamples.Photo() : null;
        var prev = CategoryDayBuilder.PreviousWorkDay(day, new Dictionary<DateOnly, string>());
        var cake = new CategoryDayCard("Кекс", null, 640.5m, 21_300_000, 96, 104, 12, 14, 610, 640.5m / 610, null, false, 12_345.6m, 15_000, 12_345.6m / 15_000,
            prev, 580, day.AddDays(-7), 702, 598.3m, days, kind, breakdown,
            Enumerable.Range(0, 8).Select(i => new CategoryDaySku(100 + i, $"Кекс «Nillo» шоколадный с начинкой {i + 1}, 1,5 кг", $"51{i}", 120 - i * 11,
                4_000_000 - i * 300_000, 30 - i * 2, 1_800 - i * 120, i < 3, i == 5 ? null : photo)).ToList(),
            ["Кекс «Мини» 0,3 кг", "Кекс «Классик» 2 кг"], 1);
        var bamboo = new CategoryDayCard("Бамбук", null, 0, 0, 0, 0, 0, 6, null, null, null, false, 0, null, null, prev, 0, day.AddDays(-7), 0, null,
            days.Select(d => d with { Kg = 0 }).ToList(), null, [], [], [], 0);
        return new CategoryDayCards(slot, day, through ?? day, holiday, [cake, bamboo]);
    }
}

/// <summary>«11. Итоги дня по категориям» без базы: каталог, прошлый рабочий день, документ и подпись, картинки 1080 px, переводы подписей.</summary>
public class TelegramCategoryDayTests
{
    private static readonly DateOnly Day = new(2026, 11, 16);
    private static readonly SkiaReportRenderer Renderer = new();

    private static readonly ResolvedScope Country = new(new ReportScope(TelegramReportRoles.Rm), "Вся страна", null,
        [new ScopeRegion(Guid.NewGuid(), "Хоразм", "Азиз Каримов", null, [new ReportPerson(61, "Бахтиёр Юлдашев")])], null);

    private static ReportDocument Document(string job, CategoryDayCards cards, string language = "ru") =>
        CategoryDayReport.Document(ReportCatalog.Find(job)!, Country, Day, cards, new DateTimeOffset(Day.ToDateTime(new TimeOnly(19, 0)), TimeSpan.FromHours(5)),
            new DateTimeOffset(Day.ToDateTime(new TimeOnly(18, 55)), TimeSpan.FromHours(5)), language);

    [Fact]
    public void Catalog_has_morning_and_evening_day_results_for_every_role_switched_off_by_default()
    {
        var jobs = ReportCatalog.Jobs.Where(j => j.Report == ReportType.CategoryDay).ToList();
        Assert.Equal(["category-day.morning", "category-day.evening"], jobs.Select(j => j.Code));
        Assert.Equal(["09:00", "19:00"], jobs.Select(j => j.Time.ToString("HH:mm")));
        Assert.All(jobs, j => Assert.Equal(TelegramReportRoles.All, j.Roles));
        Assert.All(jobs, j => Assert.Equal(ReportSchedules.WorkDays, j.Days));
        Assert.All(jobs, j => Assert.False(ReportCatalog.Default(j).Enabled));
        Assert.True(jobs[0].IsMorning);
        Assert.False(jobs[1].IsMorning);
        Assert.Equal("category-day", ReportCatalog.Key(ReportType.CategoryDay));
        Assert.Equal("11. Итоги дня по категориям", ReportCatalog.Title(ReportType.CategoryDay));
        Assert.Equal("итог прошлого рабочего дня", ReportCatalog.SlotTitle(ReportType.CategoryDay, "morning"));
        // Итоги дня — после карточек категорий того же утра (очередь — в порядке каталога).
        Assert.True(ReportCatalog.Order("category-day.morning") > ReportCatalog.Order("category-cards.morning"));
    }

    [Fact]
    public void Previous_work_day_skips_sundays_and_holidays_and_change_needs_a_base()
    {
        var none = new Dictionary<DateOnly, string>();
        Assert.Equal(new DateOnly(2026, 11, 14), CategoryDayBuilder.PreviousWorkDay(new DateOnly(2026, 11, 16), none)); // понедельник → суббота
        Assert.Equal(new DateOnly(2026, 11, 17), CategoryDayBuilder.PreviousWorkDay(new DateOnly(2026, 11, 18), none));
        var holidays = new Dictionary<DateOnly, string> { [new DateOnly(2026, 11, 14)] = "Праздник" };
        Assert.Equal(new DateOnly(2026, 11, 13), CategoryDayBuilder.PreviousWorkDay(new DateOnly(2026, 11, 16), holidays));
        Assert.Equal(0.1m, CategoryDayReport.Change(110, 100));
        Assert.Equal(-0.25m, CategoryDayReport.Change(75, 100));
        Assert.Null(CategoryDayReport.Change(5, 0));
    }

    [Fact]
    public void Document_caption_names_every_category_with_day_kg_day_plan_and_outlets()
    {
        var doc = Document("category-day.evening", CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Rm));
        Assert.True(doc.Applies);
        Assert.False(doc.Incomplete);
        Assert.Equal("11. Итоги дня по категориям · 19:00 — итог дня", doc.Title);
        Assert.StartsWith("11. Итоги дня по категориям · Вся страна · 16.11.2026\n", doc.Caption);
        Assert.Contains($"Кекс: {ReportFormat.Kg(640.5m)} кг за день — 105% плана дня; 96 ТТ", doc.Caption);
        Assert.Contains("Бамбук: 0 кг за день; 0 ТТ", doc.Caption);
        Assert.True(doc.Caption.Length <= 1024);
        Assert.Equal(["Кекс", "Бамбук"], doc.Kpis.Select(k => k.Label));
        Assert.Equal((ReportTone.Good, ReportTone.Normal), (doc.Kpis[0].Tone, doc.Kpis[1].Tone));
        Assert.Equal("плана дня нет", doc.Kpis[1].Hint);
        Assert.NotNull(doc.CategoryDay);
        Assert.Null(doc.CategorySales);

        // День итога позже отчётного дня «Продаж» — пометка «неполные» и замечание; праздник — замечание, плана дня нет.
        var late = Document("category-day.morning", CategoryDaySamples.Cards("morning", Day, TelegramReportRoles.Rm, through: Day.AddDays(-2), holiday: "Новый год"));
        Assert.True(late.Incomplete);
        Assert.Contains(late.Notes, n => n.Contains("Продаж за 16.11.2026 в данных ещё нет — месяц по 14.11", StringComparison.Ordinal));
        Assert.Contains(late.Notes, n => n.Contains("16.11 — праздник «Новый год»: план дня не считается", StringComparison.Ordinal));
        Assert.Equal(2, Renderer.Render(late).Count);

        // Ни одной категории — не отправляется; причина в журнале — «условие отчёта», а не «нет чата».
        var none = Document("category-day.evening", CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Rm) with { Cards = [] });
        Assert.False(none.Applies);
        Assert.Equal(ReportSkipReasons.NotNeeded, ReportSkipReasons.Key(none.SkipReason));
        Assert.Empty(Renderer.Render(none));
    }

    [Fact]
    public void Every_category_renders_as_one_1080_px_card_with_photos_test_strip_and_capped_breakdown()
    {
        foreach (var role in TelegramReportRoles.All)
        {
            var pages = Renderer.Render(Document("category-day.evening", CategoryDaySamples.Cards("evening", Day, role)));
            Assert.Equal(2, pages.Count); // по картинке на категорию — одним альбомом
            foreach (var png in pages)
            {
                using var bitmap = SKBitmap.Decode(png);
                Assert.Equal(1080, bitmap.Width);
                Assert.InRange(bitmap.Height, 900, 4_000);
                Assert.True(bitmap.Width + bitmap.Height <= 10_000); // предел фото Telegram
                Assert.True(png.Length < 10 * 1024 * 1024);
            }
        }

        // Фото артикулов рисуются: без них картинка другая.
        var withPhotos = Renderer.Render(Document("category-day.evening", CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Sv)))[0];
        var withoutPhotos = Renderer.Render(Document("category-day.evening", CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Sv, photos: false)))[0];
        Assert.NotEqual(Convert.ToBase64String(withPhotos), Convert.ToBase64String(withoutPhotos));

        // Тест — с полосой «ТЕСТОВАЯ ОТПРАВКА» (картинка выше).
        var doc = Document("category-day.evening", CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Sv));
        using (var plain = SKBitmap.Decode(Renderer.Render(doc)[0]))
        using (var test = SKBitmap.Decode(Renderer.Render(doc with { Test = true })[0]))
        {
            Assert.Equal(plain.Height + 52, test.Height);
        }

        // Длинная разбивка — не больше 24 строк и «и ещё N»: 60 регионов и 25 дают одну высоту.
        using var many = SKBitmap.Decode(Renderer.Render(Document("category-day.evening", CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Rm, rows: 60)))[0]);
        using var capped = SKBitmap.Decode(Renderer.Render(Document("category-day.evening", CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Rm, rows: 25)))[0]);
        Assert.Equal(capped.Height, many.Height);
    }

    [Fact]
    public void Uzbek_documents_translate_every_label_and_cyrillic_has_no_latin_except_brands()
    {
        foreach (var job in new[] { "category-day.morning", "category-day.evening" })
        {
            var uz = Document(job, CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Rm, through: Day.AddDays(-1), holiday: "Байрам"), "uz");
            var texts = new[] { uz.Title, uz.Caption, uz.Summary }.Concat(uz.Kpis.SelectMany(k => new[] { k.Label, k.Value, k.Hint ?? "" })).Concat(uz.Notes)
                .Select(t => t.Replace("Вся страна", "").Replace("Кекс", "").Replace("Бамбук", "").Replace("Байрам", ""));
            Assert.DoesNotContain(texts, t => t.Any(ch => ch is >= 'А' and <= 'я' or 'Ё' or 'ё'));
            Assert.StartsWith("11. Toifalar bo'yicha kun yakunlari", uz.Title);
            Assert.NotEmpty(Renderer.Render(uz));

            var cyr = Document(job, CategoryDaySamples.Cards("evening", Day, TelegramReportRoles.Rm), "uz-Cyrl");
            Assert.DoesNotContain(new[] { cyr.Title, cyr.Caption, cyr.Summary }, t => Regex.IsMatch(t, "[A-Za-z]{2,}"));
            Assert.NotEmpty(Renderer.Render(cyr));
        }
    }

    [Fact]
    public void Every_day_card_label_has_an_uzbek_translation()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "src", "OneBase.Infrastructure")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        var source = File.ReadAllText(Path.Combine(root, "src", "OneBase.Infrastructure", "Telegram", "Reports", "CategoryDayRenderer.cs"))
                     + File.ReadAllText(Path.Combine(root, "src", "OneBase.Application", "Telegram", "Reports", "CategoryDayCards.cs"));
        var keys = Regex.Matches(source, @"\b[TF]\(""((?:[^""\\]|\\.)*)""").Select(m => Regex.Unescape(m.Groups[1].Value)).Distinct().ToList();
        keys.AddRange([ReportCatalog.Title(ReportType.CategoryDay), ReportCatalog.SlotTitle(ReportType.CategoryDay, "morning"),
            ReportCatalog.SlotTitle(ReportType.CategoryDay, "evening")]);
        Assert.True(keys.Count > 30);
        var missing = keys.Where(k => !ReportTexts.Keys.Contains(k)).ToList();
        Assert.True(missing.Count == 0, "Нет перевода:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }
}

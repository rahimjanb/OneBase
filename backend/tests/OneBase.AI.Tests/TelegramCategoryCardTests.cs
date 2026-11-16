using System.Text.RegularExpressions;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Telegram.Reports;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>Образцовые карточки категорий для тестов без базы (каталог, подпись, картинки, переводы).</summary>
public static class CategoryCardSamples
{
    /// <summary>Маленькое фото товара (webp, как в хранилище фото товаров).</summary>
    public static byte[] Photo()
    {
        using var bitmap = new SKBitmap(160, 160);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SandyBrown);
            using var paint = new SKPaint { Color = SKColors.DarkRed, IsAntialias = true };
            canvas.DrawCircle(80, 80, 50, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        return data.ToArray();
    }

    /// <summary>
    /// Две категории: «Кекс» с планом, разбивкой и ТОПом (rows — строк разбивки), «Бамбук» без плана и без продаж за день. Имена — как у
    /// категорий отчёта (данные, они не переводятся).
    /// </summary>
    public static CategorySalesCards Cards(string slot, DateOnly day, string role, int rows = 3, bool photos = true, DateOnly? through = null)
    {
        var days = Enumerable.Range(0, day.Day).Select(i => new CategoryDayPoint(new DateOnly(day.Year, day.Month, 1).AddDays(i), i % 7 == 6 ? 0 : 400 + i * 15)).ToList();
        var kind = role switch
        {
            TelegramReportRoles.Rm => CategorySalesCards.Regions,
            TelegramReportRoles.Sv => CategorySalesCards.Agents,
            _ => null,
        };
        IReadOnlyList<CategorySalesRow> breakdown = kind is null
            ? []
            : Enumerable.Range(0, rows).Select(i => new CategorySalesRow(kind == CategorySalesCards.Regions ? $"Регион {i + 1}" : $"Агент {i + 1}", 3_000 - i * 70,
                i % 4 == 3 ? null : 3_500 - i * 40, i % 4 == 3 ? null : (3_000m - i * 70) / (3_500m - i * 40), 90_000_000 - i * 1_000_000, 120 - i)).ToList();
        var photo = photos ? Photo() : null;
        var cake = new CategorySalesCard("Кекс", null, 12_345.6m, 410_500_000, 0.34m, 15_000, null, 0.823m, 2_654.4m, 16_100, 1.073m, 812, 0.41m, 14, 18, 4, 2,
            15_700, 1.025m, 640.5m, 21_300_000, 96, days, kind, breakdown,
            Enumerable.Range(0, 8).Select(i => new CategorySalesSku(100 + i, $"Кекс «Nillo» шоколадный с начинкой {i + 1}, 1,5 кг", $"51{i}", 1_800 - i * 120,
                60_000_000 - i * 4_000_000, 300 - i * 20, 1_700 - i * 100, i < 3, i == 5 ? null : photo)).ToList(),
            ["Кекс «Мини» 0,3 кг", "Кекс «Классик» 2 кг"], 1);
        var bamboo = new CategorySalesCard("Бамбук", null, 0, 0, null, null, null, null, null, null, null, 0, null, 0, 6, 6, 0, 0, null, 0, 0, 0,
            days.Select(d => d with { Kg = 0 }).ToList(), null, [], [], [], 0);
        return new CategorySalesCards(slot, day, day.Year, day.Month, through ?? day, false, [cake, bamboo]);
    }
}

/// <summary>«10. Карточки категорий» без базы: каталог, документ и подпись, картинки 1080 px, выбор категорий, переводы подписей.</summary>
public class TelegramCategoryCardTests
{
    private static readonly DateOnly Day = new(2026, 11, 16);
    private static readonly SkiaReportRenderer Renderer = new();

    private static readonly ResolvedScope Country = new(new ReportScope(TelegramReportRoles.Rm), "Вся страна", null,
        [new ScopeRegion(Guid.NewGuid(), "Хоразм", "Азиз Каримов", null, [new ReportPerson(61, "Бахтиёр Юлдашев")])], null);

    private static ReportDocument Document(string job, CategorySalesCards cards, string language = "ru") =>
        CategorySalesReport.Document(ReportCatalog.Find(job)!, Country, Day, cards, new DateTimeOffset(Day.ToDateTime(new TimeOnly(18, 30)), TimeSpan.FromHours(5)),
            new DateTimeOffset(Day.ToDateTime(new TimeOnly(18, 25)), TimeSpan.FromHours(5)), language);

    [Fact]
    public void Catalog_has_morning_and_evening_category_cards_for_every_role_switched_off_by_default()
    {
        var jobs = ReportCatalog.Jobs.Where(j => j.Report == ReportType.CategoryCards).ToList();
        Assert.Equal(["category-cards.morning", "category-cards.evening"], jobs.Select(j => j.Code));
        Assert.Equal(["09:00", "18:30"], jobs.Select(j => j.Time.ToString("HH:mm")));
        Assert.All(jobs, j => Assert.Equal(TelegramReportRoles.All, j.Roles));
        Assert.All(jobs, j => Assert.Equal(ReportSchedules.WorkDays, j.Days));
        Assert.All(jobs, j => Assert.False(ReportCatalog.Default(j).Enabled));
        Assert.True(jobs[0].IsMorning);
        Assert.Equal("category-cards", ReportCatalog.Key(ReportType.CategoryCards));
        Assert.Equal("10. Карточки категорий", ReportCatalog.Title(ReportType.CategoryCards));
        Assert.Equal(["category-cards.evening"], ReportCatalog.Parse("category-cards.evening,unknown.job"));
    }

    [Fact]
    public void Choice_keeps_order_drops_duplicates_and_morning_reports_the_previous_work_day()
    {
        Assert.Equal(["Придзел", "помадка"], CategoryCardChoice.Parse(" Придзел, помадка ,Помадка,,")); // повтор в другом регистре — тот же
        Assert.Null(CategoryCardChoice.Format([" ", ""]));
        Assert.Equal("Придзел,Помадка", CategoryCardChoice.Format(["Придзел", "придзел", "Помадка"]));
        Assert.Equal("Могуль", CategoryCardChoice.AliasOf("помадка"));
        Assert.Null(CategoryCardChoice.AliasOf("Кекс"));
        Assert.Equal(new DateOnly(2026, 11, 14), CategoryCardChoice.PreviousWorkDay(new DateOnly(2026, 11, 16))); // понедельник → суббота
        Assert.Equal(new DateOnly(2026, 11, 17), CategoryCardChoice.PreviousWorkDay(new DateOnly(2026, 11, 18)));
        Assert.Equal(new DateOnly(2026, 10, 31), CategoryCardChoice.PreviousWorkDay(new DateOnly(2026, 11, 2)));
    }

    [Fact]
    public void Document_caption_names_every_category_with_month_plan_and_day()
    {
        var doc = Document("category-cards.evening", CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Rm));
        Assert.True(doc.Applies);
        Assert.False(doc.Incomplete);
        Assert.Equal("10. Карточки категорий · 18:30 — итог дня", doc.Title);
        Assert.StartsWith("10. Карточки категорий · Вся страна · 16.11.2026\n", doc.Caption);
        Assert.Contains($"Кекс: {ReportFormat.Kg(12_345.6m)} кг за месяц — 82% плана; за день {ReportFormat.Kg(640.5m)} кг", doc.Caption);
        Assert.Contains("Бамбук: 0 кг за месяц; за день 0 кг", doc.Caption);
        Assert.True(doc.Caption.Length <= 1024);
        Assert.Equal(["Кекс", "Бамбук"], doc.Kpis.Select(k => k.Label));
        Assert.Equal(ReportTone.Warn, doc.Kpis[0].Tone);
        Assert.Contains($"Кекс {ReportFormat.Kg(12_345.6m)} кг (82%)", doc.Summary);
        Assert.NotNull(doc.CategorySales);

        // День итога позже отчётного дня «Продаж» — пометка «неполные» и замечание.
        var late = Document("category-cards.morning", CategoryCardSamples.Cards("morning", Day, TelegramReportRoles.Rm, through: Day.AddDays(-2)));
        Assert.True(late.Incomplete);
        Assert.Contains(late.Notes, n => n.Contains("Продаж за 16.11.2026 в данных ещё нет — месяц по 14.11", StringComparison.Ordinal));

        // Ни одной категории — не отправляется; причина в журнале — «условие отчёта», а не «нет чата».
        var none = Document("category-cards.evening", CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Rm) with { Cards = [] });
        Assert.False(none.Applies);
        Assert.Equal(ReportSkipReasons.NotNeeded, ReportSkipReasons.Key(none.SkipReason));
        Assert.Empty(Renderer.Render(none));
    }

    [Fact]
    public void Every_category_renders_as_one_1080_px_card_with_photos_test_strip_and_capped_breakdown()
    {
        foreach (var role in TelegramReportRoles.All)
        {
            var doc = Document("category-cards.evening", CategoryCardSamples.Cards("evening", Day, role));
            var pages = Renderer.Render(doc);
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
        var withPhotos = Renderer.Render(Document("category-cards.evening", CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Sv)))[0];
        var withoutPhotos = Renderer.Render(Document("category-cards.evening", CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Sv, photos: false)))[0];
        Assert.NotEqual(Convert.ToBase64String(withPhotos), Convert.ToBase64String(withoutPhotos));

        // Тест — с полосой «ТЕСТОВАЯ ОТПРАВКА» (картинка выше и другая).
        var doc2 = Document("category-cards.evening", CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Sv));
        using (var plain = SKBitmap.Decode(Renderer.Render(doc2)[0]))
        using (var test = SKBitmap.Decode(Renderer.Render(doc2 with { Test = true })[0]))
        {
            Assert.Equal(plain.Height + 52, test.Height);
        }

        // Длинная разбивка — не больше 24 строк и «и ещё N»: 60 регионов и 24 дают одну высоту.
        using var many = SKBitmap.Decode(Renderer.Render(Document("category-cards.evening", CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Rm, rows: 60)))[0]);
        using var capped = SKBitmap.Decode(Renderer.Render(Document("category-cards.evening", CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Rm, rows: 25)))[0]);
        Assert.Equal(capped.Height, many.Height);
    }

    [Fact]
    public void Uzbek_documents_translate_every_label_and_cyrillic_has_no_latin_except_brands()
    {
        foreach (var job in new[] { "category-cards.morning", "category-cards.evening" })
        {
            var uz = Document(job, CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Rm, through: Day.AddDays(-1)), "uz");
            var texts = new[] { uz.Title, uz.Caption, uz.Summary }.Concat(uz.Kpis.SelectMany(k => new[] { k.Label, k.Value, k.Hint ?? "" })).Concat(uz.Notes)
                .Select(t => t.Replace("Вся страна", "").Replace("Кекс", "").Replace("Бамбук", ""));
            Assert.DoesNotContain(texts, t => t.Any(ch => ch is >= 'А' and <= 'я' or 'Ё' or 'ё'));
            Assert.StartsWith("10. Toifa kartochkalari", uz.Title);

            var cyr = Document(job, CategoryCardSamples.Cards("evening", Day, TelegramReportRoles.Rm), "uz-Cyrl");
            Assert.DoesNotContain(new[] { cyr.Title, cyr.Caption, cyr.Summary }, t => Regex.IsMatch(t, "[A-Za-z]{2,}"));
            Assert.NotEmpty(Renderer.Render(cyr));
        }
    }

    [Fact]
    public void Every_category_card_label_has_an_uzbek_translation()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "src", "OneBase.Infrastructure")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        var source = File.ReadAllText(Path.Combine(root, "src", "OneBase.Infrastructure", "Telegram", "Reports", "CategoryCardRenderer.cs"))
                     + File.ReadAllText(Path.Combine(root, "src", "OneBase.Application", "Telegram", "Reports", "CategorySalesCards.cs"));
        var keys = Regex.Matches(source, @"\b[TF]\(""((?:[^""\\]|\\.)*)""").Select(m => Regex.Unescape(m.Groups[1].Value)).Distinct().ToList();
        keys.AddRange([ReportCatalog.Title(ReportType.CategoryCards), ReportCatalog.SlotTitle(ReportType.CategoryCards, "morning")]);
        Assert.True(keys.Count > 30);
        var missing = keys.Where(k => !ReportTexts.Keys.Contains(k)).ToList();
        Assert.True(missing.Count == 0, "Нет перевода:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }
}

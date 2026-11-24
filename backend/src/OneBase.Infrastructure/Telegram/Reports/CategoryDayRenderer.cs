using System.Globalization;
using OneBase.Application.Telegram.Reports;
using SkiaSharp;
using C = OneBase.Infrastructure.Telegram.Reports.FarmColors;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// «11. Итоги дня по категориям»: по картинке 1080 px на категорию, в стиле карточек Candy Gold, — только день итога, без месяца и других
/// дней. Сверху вниз: категория и её второе название, область и день итога; продано за день, выполнение плана дня и полоса «факт — план
/// дня»; плитки (ТТ и заказы, артикулы, цена за кг); регионы (РМ) или ТП (СВ) за день с планом дня; артикулы дня с фото. Высота — по
/// содержимому. Выбраны товары категории — в шапке «выбрано товаров: N», плана дня нет (план — у всей категории).
/// </summary>
internal sealed class CategoryDayRenderer(SkiaReportRenderer.Typefaces faces, string language, bool test) : FarmPainter(faces, language, test)
{
    private const float Gap = 20;
    private const float HeaderHeight = 176;
    private const float NoteHeight = 54;
    private const float HeroHeight = 254;
    private const float HeroShort = 190;
    private const float CountersHeight = 108;
    private const float TableRow = 46;
    private const int TableMax = 24;
    private const float SkuRow = 104;
    private const float FooterHeight = 64;
    private const float CardInner = 28;

    public static IReadOnlyList<byte[]> Render(ReportDocument document, CategoryDayCards cards, SkiaReportRenderer.Typefaces faces) =>
        cards.Cards.Select(card => new CategoryDayRenderer(faces, document.Language, document.Test).Draw(document, cards, card)).ToList();

    private byte[] Draw(ReportDocument doc, CategoryDayCards cards, CategoryDayCard card)
    {
        var rows = card.Breakdown.Take(TableMax).ToList();
        var more = card.Breakdown.Count - rows.Count;
        var notes = doc.Notes.Take(2).ToList();
        var top = IsTest ? 52f : 0f;
        var hero = HeroOf(card);
        var height = top + 24 + HeaderHeight + notes.Count * (NoteHeight + Gap) + hero + Gap + CountersHeight + Gap
                     + (rows.Count > 0 ? TableHeight(rows.Count, more) + Gap : 0)
                     + (card.Top.Count > 0 ? TopHeight(card.Top.Count) + Gap : 0)
                     + FooterHeight;

        using var surface = SKSurface.Create(new SKImageInfo(Width, (int)Math.Ceiling(height)));
        var c = surface.Canvas;
        c.Clear(C.Background);
        if (IsTest)
        {
            TestStrip(c);
        }

        var y = top + 24;
        DrawHeader(c, y, doc, cards, card);
        y += HeaderHeight;
        foreach (var note in notes)
        {
            DrawNote(c, y, note);
            y += NoteHeight + Gap;
        }

        DrawHero(c, y, card, hero);
        y += hero + Gap;
        DrawCounters(c, y, card);
        y += CountersHeight + Gap;
        if (rows.Count > 0)
        {
            DrawTable(c, y, card, rows, more);
            y += TableHeight(rows.Count, more) + Gap;
        }

        if (card.Top.Count > 0)
        {
            DrawTop(c, y, card);
            y += TopHeight(card.Top.Count) + Gap;
        }

        var source = doc.DataAsOf is { } at ? F("OneBase · данные Linko на {0}", at.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture)) : "OneBase";
        TextCenter(c, source, Width / 2f, y + 6, 18, Faces.Medium, C.Ink3);
        return Png(surface);
    }

    /// <summary>Блок «Продано за день»: с планом дня — с полосой «факт — план дня», без него — короче.</summary>
    private static float HeroOf(CategoryDayCard card) => card.DayExecution is null ? HeroShort : HeroHeight;

    private static float TableHeight(int rows, int more) => 70 + 40 + rows * TableRow + (more > 0 ? TableRow : 0) + 14;

    private static float TopHeight(int count) => 70 + count * SkuRow + 10;

    // ---------- Шапка ----------

    private void DrawHeader(SKCanvas c, float top, ReportDocument doc, CategoryDayCards cards, CategoryDayCard card)
    {
        Spaced(c, "CANDY GOLD · " + T("ИТОГ ДНЯ КАТЕГОРИИ"), Pad, top + 6, 20, Faces.Bold, C.Brand, 4);
        var nameWidth = Math.Min(Measure(card.Name, Faces.Bold, 60), Inner - 220);
        TextFit(c, card.Name, Pad, top + 40, Inner - 220, 60, Faces.Bold, C.Ink);
        if (card.Alias is { } alias && nameWidth + 40 < Inner - 220)
        {
            TextFit(c, "· " + alias, Pad + nameWidth + 16, top + 64, Inner - 220 - nameWidth - 16, 32, Faces.SemiBold, C.Ink3);
        }

        var scope = card.Picked is { } picked ? $"{doc.Scope} · {F("выбрано товаров: {0}", picked)}" : doc.Scope;
        TextFit(c, scope, Pad, top + 122, Inner - 220, 26, Faces.Medium, C.Ink2);
        // Дата и день недели — за какой день итог (утром в понедельник — суббота).
        Pill(c, Width - Pad - 190, top + 34, 190, 76, DayMonth(cards.Day), Weekday(cards.Day) + " · " + T("итог дня"), active: true);
    }

    private void DrawNote(SKCanvas c, float top, string note)
    {
        using var bg = Fill(C.PillActiveBg);
        c.DrawRoundRect(new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + NoteHeight), 16), bg);
        TextFit(c, note, Pad + 22, top + 14, Inner - 44, 22, Faces.SemiBold, C.Red);
    }

    // ---------- Продано за день и план дня ----------

    private void DrawHero(SKCanvas c, float top, CategoryDayCard card, float height)
    {
        CardBox(c, Pad, top, Inner, height);
        var left = Pad + CardInner;
        var right = Width - Pad - CardInner;
        Text(c, T("Продано за день"), left, top + 24, 22, Faces.Medium, C.Ink2);
        Text(c, $"{ReportFormat.Kg(card.Kg)} {T("кг")}", left, top + 56, 62, Faces.Bold, card.Kg > 0 ? C.Ink : C.Ink3);
        Text(c, card.Kg != 0 || card.Revenue != 0 ? $"{ReportFormat.Number(card.Revenue)} {T("сум")}" : T("За день продаж категории нет"), left, top + 134, 26,
            Faces.Medium, C.Ink2);

        if (card.DayExecution is not { } execution)
        {
            // Плана дня нет: выбраны товары (план — у всей категории), план уже выполнен (план дня 0), план общий на несколько категорий —
            // какой, или плана нет.
            var label = card.Picked is not null ? T("план — у всей категории")
                : card.PlanDone ? T("план уже выполнен") : card.PlanShared is null ? T("плана дня нет") : T("план общий");
            TextRight(c, label, right, top + 60, 30, Faces.SemiBold, card.PlanDone ? C.Green : C.Ink3);
            if (!card.PlanDone && card.PlanShared is { } shared)
            {
                TextRight(c, "«" + shared + "»", right, top + 104, 20, Faces.Medium, C.Ink3);
            }

            return;
        }

        TextRight(c, ReportFormat.Percent(execution), right, top + 52, 62, Faces.Bold, Tone(execution));
        TextRight(c, T("плана дня"), right, top + 130, 22, Faces.Medium, C.Ink3);

        // Полоса: факт дня к плану дня, отметка плана (100%); шкала — до 150%, если факт выше плана.
        var barX = left;
        var barW = right - left;
        var barY = top + 182;
        var scale = (double)Math.Clamp(Math.Max(1m, execution), 1m, 1.5m);
        using (var track = Fill(C.Track))
        {
            c.DrawRoundRect(new SKRoundRect(new SKRect(barX, barY, barX + barW, barY + 16), 8), track);
        }

        if (execution > 0)
        {
            using var fill = Fill(Tone(execution));
            var w = (float)(barW * Math.Min(1, (double)execution / scale));
            c.DrawRoundRect(new SKRoundRect(new SKRect(barX, barY, barX + Math.Max(w, 16), barY + 16), 8), fill);
        }

        var planX = (float)(barX + barW / scale);
        using (var mark = Stroke(C.Ink, 3))
        {
            c.DrawLine(planX, barY - 8, planX, barY + 24, mark);
        }

        var dayPlan = card.DayPlanKg ?? 0;
        Text(c, F("План дня {0} кг", ReportFormat.Kg(dayPlan)), left, barY + 30, 20, Faces.Medium, C.Ink2);
        var gap = card.Kg - dayPlan;
        TextRight(c, gap >= 0 ? F("сверх плана {0} кг", ReportFormat.Kg(gap)) : F("не хватило {0} кг", ReportFormat.Kg(-gap)), right, barY + 30, 20,
            Faces.SemiBold, gap >= 0 ? C.Green : C.Red);
    }

    // ---------- Плитки ----------

    private void DrawCounters(SKCanvas c, float top, CategoryDayCard card)
    {
        var price = card.Kg > 0 ? card.Revenue / card.Kg : (decimal?)null;
        Counters(c, top,
            (T("АКБ дня, точек"), ReportFormat.Number(card.Akb), C.Ink, card.Orders > 0 ? F("заказов {0}", card.Orders) : null),
            (T("Артикулов за день"), ReportFormat.Number(card.Skus), C.Ink, null),
            (T("Цена, сум/кг"), ReportFormat.Number(price), C.Ink, null));
    }

    // ---------- Регионы или ТП ----------

    private void DrawTable(SKCanvas c, float top, CategoryDayCard card, IReadOnlyList<CategoryDayRow> rows, int more)
    {
        CardBox(c, Pad, top, Inner, TableHeight(rows.Count, more));
        var left = Pad + CardInner;
        var right = Width - Pad - CardInner;
        Text(c, card.BreakdownKind == CategoryDayCards.Regions ? T("Регионы за день") : T("ТП за день"), left, top + 22, 24, Faces.Bold, C.Ink);
        var planned = rows.Any(r => r.DayPlanKg is not null);
        if (planned)
        {
            TextRight(c, T("красным — ниже 80% плана дня"), right, top + 26, 18, Faces.Medium, C.Ink3);
        }

        // Колонки справа налево: заказы, АКБ, сумма, %, план дня, факт; имя — остальное. Плана дня нет ни у одной строки — без его колонок.
        var columns = new List<(string Title, float Width, Func<CategoryDayRow, (string Text, SKTypeface Face, SKColor Color, float Size)> Cell)>
        {
            (T("Заказы"), 92, r => (ReportFormat.Number(r.Orders), Faces.Medium, C.Ink2, 22)),
            (T("АКБ"), 76, r => (ReportFormat.Number(r.Akb), Faces.Medium, C.Ink2, 22)),
            (T("Сумма"), 166, r => (ReportFormat.Short(r.Revenue, Language), Faces.Medium, C.Ink2, 20)),
        };
        if (planned)
        {
            columns.Add(("%", 76, r => (ReportFormat.Percent(r.DayExecution), Faces.Bold, r.DayExecution is { } e ? Tone(e) : C.Ink3, 22)));
            columns.Add((T("План дня"), 124, r => (ReportFormat.Kg(r.DayPlanKg), Faces.Medium, C.Ink2, 22)));
        }

        columns.Add((T("Факт, кг"), 120, r => (ReportFormat.Kg(r.Kg), Faces.SemiBold, r.Kg > 0 ? C.Ink : C.Red, 22)));
        var edges = new float[columns.Count];
        var x = right;
        for (var i = 0; i < columns.Count; i++)
        {
            edges[i] = x;
            x -= columns[i].Width;
        }

        var nameWidth = x - left - 12;
        var head = top + 70;
        Text(c, card.BreakdownKind == CategoryDayCards.Regions ? T("Регион") : T("ТП"), left, head, 18, Faces.SemiBold, C.Ink3);
        for (var i = 0; i < columns.Count; i++)
        {
            // Заголовок не шире колонки («Buyurtmalar» на узбекском) — мельче, а не поверх соседнего.
            var size = 18f;
            while (size > 12 && Measure(columns[i].Title, Faces.SemiBold, size) > columns[i].Width - 10)
            {
                size -= 1;
            }

            TextRight(c, columns[i].Title, edges[i], head + (18 - size) / 2, size, Faces.SemiBold, C.Ink3);
        }

        var y = head + 40;
        using var line = Stroke(C.Border, 1.5f);
        foreach (var row in rows)
        {
            c.DrawLine(left, y - 6, right, y - 6, line);
            TextFit(c, row.Name, left, y + 4, nameWidth, 22, Faces.Medium, row.Kg > 0 ? C.Ink : C.Ink2);
            for (var i = 0; i < columns.Count; i++)
            {
                // Значение не шире колонки («425,5 тыс. сум») — мельче, а не поверх соседней.
                var (text, face, color, size) = columns[i].Cell(row);
                var fit = size;
                while (fit > 14 && Measure(text, face, fit) > columns[i].Width - 12)
                {
                    fit -= 1;
                }

                TextRight(c, text, edges[i], y + 4 + (22 - fit) / 2, fit, face, color);
            }

            y += TableRow;
        }

        if (more > 0)
        {
            c.DrawLine(left, y - 6, right, y - 6, line);
            Text(c, F("и ещё {0} — на странице категории в «Продажах»", more), left, y + 6, 20, Faces.Medium, C.Ink3);
        }
    }

    // ---------- Артикулы дня ----------

    private void DrawTop(SKCanvas c, float top, CategoryDayCard card)
    {
        CardBox(c, Pad, top, Inner, TopHeight(card.Top.Count));
        var left = Pad + CardInner;
        var right = Width - Pad - CardInner;
        Text(c, card.Skus > card.Top.Count ? F("Артикулы дня: ТОП-{0} из {1}", card.Top.Count, card.Skus) : T("Артикулы дня"), left, top + 22, 24, Faces.Bold, C.Ink);
        TextRight(c, T("кг · ТТ · сумма"), right, top + 26, 18, Faces.Medium, C.Ink3);
        var y = top + 70;
        using var line = Stroke(C.Border, 1.5f);
        for (var i = 0; i < card.Top.Count; i++)
        {
            var sku = card.Top[i];
            if (i > 0)
            {
                c.DrawLine(left, y - 6, right, y - 6, line);
            }

            DrawPhoto(c, sku.Photo, left, y + 4, 84);
            var textLeft = left + 104;
            var textWidth = right - textLeft - 230;
            var ty = y + 6;
            foreach (var text in Wrap(sku.Name, Faces.SemiBold, 22, textWidth, 2))
            {
                Text(c, text, textLeft, ty, 22, Faces.SemiBold, C.Ink);
                ty += 28;
            }

            var meta = sku.Code is { Length: > 0 } code ? F("арт. {0}", code) : "";
            if (sku.IsTop)
            {
                meta = meta.Length > 0 ? meta + " · " + T("ТОП") : T("ТОП");
            }

            if (meta.Length > 0)
            {
                Text(c, meta, textLeft, ty + 4, 17, Faces.Medium, sku.IsTop ? C.Brand : C.Ink3);
            }

            TextRight(c, $"{ReportFormat.Kg(sku.Kg)} {T("кг")}", right, y + 6, 28, Faces.Bold, C.Ink);
            TextRight(c, F("{0} ТТ · {1}", ReportFormat.Number(sku.Akb), ReportFormat.Short(sku.Revenue, Language)), right, y + 44, 18, Faces.Medium, C.Ink2);
            y += SkuRow;
        }
    }

    private void DrawPhoto(SKCanvas c, byte[]? photo, float x, float y, float size)
    {
        var rect = new SKRoundRect(new SKRect(x, y, x + size, y + size), 14);
        using (var bg = Fill(C.Track))
        {
            c.DrawRoundRect(rect, bg);
        }

        using var bitmap = photo is null ? null : SKBitmap.Decode(photo);
        if (bitmap is null)
        {
            TextCenter(c, T("нет фото"), x + size / 2, y + size / 2 - 9, 14, Faces.Medium, C.Ink3);
            return;
        }

        c.Save();
        c.ClipRoundRect(rect, antialias: true);
        DrawPack(c, bitmap, x + 4, y + 4, size - 8, size - 8);
        c.Restore();
    }

    /// <summary>Цвет выполнения: от 100% — зелёный, 80–99% — оранжевый, ниже 80% — красный (порог ТЗ).</summary>
    private static SKColor Tone(decimal share) => share >= 1 ? C.Green : share >= ReportFormat.RedBelow ? C.Orange : C.Red;
}

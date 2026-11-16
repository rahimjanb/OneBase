using System.Globalization;
using OneBase.Application.Telegram.Reports;
using SkiaSharp;
using C = OneBase.Infrastructure.Telegram.Reports.FarmColors;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// «10. Карточки категорий»: по картинке 1080 px на категорию, в стиле карточек Candy Gold. Сверху вниз: категория и её второе название
/// («Помадка · Могуль»), область и день итога; факт месяца, выполнение и полоса «факт — план — прогноз»; плитки (за день, АКБ и
/// дистрибуция, SKU в продаже, доля в весе, прошлый месяц, цена за кг); килограммы по дням; регионы (РМ) или ТП (СВ) с планом категории;
/// ТОП артикулов с фото; пропавшие артикулы. Высота — по содержимому (длинная разбивка — не больше 24 строк и «ещё N»).
/// </summary>
internal sealed class CategoryCardRenderer(SkiaReportRenderer.Typefaces faces, string language, bool test) : FarmPainter(faces, language, test)
{
    private const float Gap = 20;
    private const float HeaderHeight = 176;
    private const float NoteHeight = 54;
    private const float HeroHeight = 254;
    private const float CountersHeight = 108 * 2 + 16;
    private const float ChartHeight = 240;
    private const float TableRow = 46;
    private const int TableMax = 24;
    private const float SkuRow = 104;
    private const float LostRow = 34;
    private const float FooterHeight = 64;
    private const float CardInner = 28;

    public static IReadOnlyList<byte[]> Render(ReportDocument document, CategorySalesCards cards, SkiaReportRenderer.Typefaces faces) =>
        cards.Cards.Select(card => new CategoryCardRenderer(faces, document.Language, document.Test).Draw(document, cards, card)).ToList();

    private byte[] Draw(ReportDocument doc, CategorySalesCards cards, CategorySalesCard card)
    {
        var rows = card.Breakdown.Take(TableMax).ToList();
        var more = card.Breakdown.Count - rows.Count;
        var lostCount = card.LostSkus.Count + card.LostMore;
        var top = IsTest ? 52f : 0f;
        var height = top + 24 + HeaderHeight + (doc.Notes.Count > 0 ? NoteHeight + Gap : 0) + HeroHeight + Gap + CountersHeight + Gap + ChartHeight + Gap
                     + (rows.Count > 0 ? TableHeight(rows.Count, more) + Gap : 0)
                     + (card.Top.Count > 0 ? 70 + card.Top.Count * SkuRow + 10 + Gap : 0)
                     + (lostCount > 0 ? 100 + card.LostSkus.Count * LostRow + (card.LostMore > 0 ? LostRow : 0) + Gap : 0)
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
        if (doc.Notes.Count > 0)
        {
            DrawNote(c, y, doc.Notes[0]);
            y += NoteHeight + Gap;
        }

        DrawHero(c, y, cards, card);
        y += HeroHeight + Gap;
        DrawCounters(c, y, cards, card);
        y += CountersHeight + Gap;
        DrawChart(c, y, cards, card);
        y += ChartHeight + Gap;
        if (rows.Count > 0)
        {
            DrawTable(c, y, card, rows, more);
            y += TableHeight(rows.Count, more) + Gap;
        }

        if (card.Top.Count > 0)
        {
            DrawTop(c, y, card);
            y += 70 + card.Top.Count * SkuRow + 10 + Gap;
        }

        if (lostCount > 0)
        {
            DrawLost(c, y, card, lostCount);
            y += 100 + card.LostSkus.Count * LostRow + (card.LostMore > 0 ? LostRow : 0) + Gap;
        }

        var source = doc.DataAsOf is { } at
            ? F("OneBase · данные Linko на {0} · месяц по {1}", at.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture), DayMonth(cards.DataThrough))
            : F("OneBase · месяц по {0}", DayMonth(cards.DataThrough));
        TextCenter(c, source, Width / 2f, y + 6, 18, Faces.Medium, C.Ink3);
        return Png(surface);
    }

    private static float TableHeight(int rows, int more) => 70 + 40 + rows * TableRow + (more > 0 ? TableRow : 0) + 14;

    // ---------- Шапка ----------

    private void DrawHeader(SKCanvas c, float top, ReportDocument doc, CategorySalesCards cards, CategorySalesCard card)
    {
        Spaced(c, "CANDY GOLD · " + T("КАРТОЧКА КАТЕГОРИИ"), Pad, top + 6, 20, Faces.Bold, C.Brand, 4);
        var nameWidth = Math.Min(Measure(card.Name, Faces.Bold, 60), Inner - 220);
        TextFit(c, card.Name, Pad, top + 40, Inner - 220, 60, Faces.Bold, C.Ink);
        if (card.Alias is { } alias && nameWidth + 40 < Inner - 220)
        {
            TextFit(c, "· " + alias, Pad + nameWidth + 16, top + 64, Inner - 220 - nameWidth - 16, 32, Faces.SemiBold, C.Ink3);
        }

        TextFit(c, doc.Scope, Pad, top + 122, Inner - 220, 26, Faces.Medium, C.Ink2);
        // Дата на плашке говорит, за какой день итог (утром в понедельник — суббота).
        Pill(c, Width - Pad - 190, top + 34, 190, 76, DayMonth(cards.Day), T("итог дня"), active: true);
    }

    private void DrawNote(SKCanvas c, float top, string note)
    {
        using var bg = Fill(C.PillActiveBg);
        c.DrawRoundRect(new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + NoteHeight), 16), bg);
        TextFit(c, note, Pad + 22, top + 14, Inner - 44, 22, Faces.SemiBold, C.Red);
    }

    // ---------- Факт, план, прогноз ----------

    private void DrawHero(SKCanvas c, float top, CategorySalesCards cards, CategorySalesCard card)
    {
        CardBox(c, Pad, top, Inner, HeroHeight);
        var left = Pad + CardInner;
        var right = Width - Pad - CardInner;
        Text(c, T("Факт месяца"), left, top + 24, 22, Faces.Medium, C.Ink2);
        Text(c, $"{ReportFormat.Kg(card.FactKg)} {T("кг")}", left, top + 56, 62, Faces.Bold, C.Ink);
        Text(c, $"{ReportFormat.Number(card.Revenue)} {T("сум")}", left, top + 134, 26, Faces.Medium, C.Ink2);

        if (card.Execution is { } execution)
        {
            TextRight(c, ReportFormat.Percent(execution), right, top + 52, 62, Faces.Bold, Tone(execution));
            TextRight(c, T("плана"), right, top + 130, 22, Faces.Medium, C.Ink3);
        }
        else
        {
            TextRight(c, card.PlanShared is null ? T("плана нет") : T("план общий"), right, top + 70, 30, Faces.SemiBold, C.Ink3);
        }

        // Полоса: факт к плану, отметка плана (100%) и прогноза; шкала — до 150%, если факт или прогноз выше плана.
        var barX = left;
        var barW = right - left;
        var barY = top + 182;
        var scale = (double)Math.Clamp(Math.Max(1m, Math.Max(card.Execution ?? 0, card.ForecastExecution ?? 0)), 1m, 1.5m);
        using (var track = Fill(C.Track))
        {
            c.DrawRoundRect(new SKRoundRect(new SKRect(barX, barY, barX + barW, barY + 16), 8), track);
        }

        if (card.Execution is { } e && e > 0)
        {
            using var fill = Fill(Tone(e));
            var w = (float)(barW * Math.Min(1, (double)e / scale));
            c.DrawRoundRect(new SKRoundRect(new SKRect(barX, barY, barX + Math.Max(w, 16), barY + 16), 8), fill);
        }

        if (card.PlanKg is not null && card.Execution is not null)
        {
            var planX = (float)(barX + barW / scale);
            using var mark = Stroke(C.Ink, 3);
            c.DrawLine(planX, barY - 8, planX, barY + 24, mark);
        }

        if (card.ForecastExecution is { } f && !cards.MonthClosed)
        {
            var fx = (float)(barX + barW * Math.Min(1, (double)f / scale));
            using var tick = Fill(C.Ink2);
            using var path = new SKPath();
            path.MoveTo(fx - 9, barY - 14);
            path.LineTo(fx + 9, barY - 14);
            path.LineTo(fx, barY - 2);
            path.Close();
            c.DrawPath(path, tick);
        }

        var planText = card.PlanShared is { } shared
            ? F("План общий «{0}»: {1} кг", shared, ReportFormat.Kg(card.PlanKg))
            : card.PlanKg is { } plan
                ? card.RemainingKg is > 0 ? F("План {0} кг · осталось {1} кг", ReportFormat.Kg(plan), ReportFormat.Kg(card.RemainingKg)) : F("План {0} кг", ReportFormat.Kg(plan))
                : T("План категории на месяц не загружен");
        TextFit(c, planText, left, barY + 30, barW * 0.55f, 20, Faces.Medium, C.Ink2);
        var forecastText = cards.MonthClosed ? T("месяц закрыт")
            : card.ForecastKg is not { } forecast ? ""
            : card.ForecastExecution is { } fe ? F("Прогноз {0} кг · {1} плана", ReportFormat.Kg(forecast), ReportFormat.Percent(fe))
            : F("Прогноз {0} кг", ReportFormat.Kg(forecast));
        TextRight(c, forecastText, right, barY + 30, 20, Faces.SemiBold, card.ForecastExecution is { } fx2 ? Tone(fx2) : C.Ink2);
    }

    // ---------- Плитки ----------

    private void DrawCounters(SKCanvas c, float top, CategorySalesCards cards, CategorySalesCard card)
    {
        Counters(c, top,
            (F("За день {0}", DayMonth(cards.Day)), $"{ReportFormat.Kg(card.DayKg)} {T("кг")}", C.Ink,
                card.DayRevenue != 0 ? ReportFormat.Short(card.DayRevenue, Language) : null),
            (T("АКБ, точек"), ReportFormat.Number(card.Akb), C.Ink, card.Distribution is { } d ? F("дистрибуция {0}", ReportFormat.Percent(d)) : null),
            (T("SKU в продаже"), $"{card.SkuSold} / {card.SkuTotal}", C.Ink, card.Silent > 0 ? F("молчат {0}", card.Silent) : null));
        var price = card.FactKg > 0 ? card.Revenue / card.FactKg : (decimal?)null;
        Counters(c, top + 108 + 16,
            (T("Доля в весе"), ReportFormat.Percent(card.WeightShare), C.Ink, null),
            (T("Прошлый месяц, кг"), ReportFormat.Kg(card.PrevMonthKg), C.Ink,
                card.VsPrevMonth is { } v && !cards.MonthClosed ? F("прогноз {0}", ReportFormat.Percent(v)) : null),
            (T("Цена, сум/кг"), ReportFormat.Number(price), C.Ink, null));
    }

    // ---------- По дням ----------

    private void DrawChart(SKCanvas c, float top, CategorySalesCards cards, CategorySalesCard card)
    {
        CardBox(c, Pad, top, Inner, ChartHeight);
        var left = Pad + CardInner;
        var right = Width - Pad - CardInner;
        Text(c, T("По дням, кг"), left, top + 22, 24, Faces.Bold, C.Ink);
        var days = card.Days;
        if (days.Count == 0 || days.All(d => d.Kg <= 0))
        {
            TextCenter(c, T("В этом месяце продаж категории пока нет"), Width / 2f, top + 120, 22, Faces.Medium, C.Ink3);
            return;
        }

        var best = days.MaxBy(d => d.Kg)!;
        TextRight(c, F("лучший день {0} — {1} кг", DayMonth(best.Day), ReportFormat.Kg(best.Kg)), right, top + 26, 20, Faces.Medium, C.Ink2);
        var max = (float)Math.Max(1, days.Max(d => d.Kg));
        var areaTop = top + 76;
        var areaH = 116f;
        var step = (right - left) / Math.Max(days.Count, 1);
        var barW = Math.Max(4, step * 0.68f);
        for (var i = 0; i < days.Count; i++)
        {
            var d = days[i];
            var h = d.Kg <= 0 ? 0 : Math.Max(3, (float)d.Kg / max * areaH);
            var x = left + i * step + (step - barW) / 2;
            var color = d.Day == cards.Day ? C.Brand : d.Day.DayOfWeek == DayOfWeek.Sunday ? C.Track : C.Amber;
            using var paint = Fill(color);
            c.DrawRoundRect(new SKRoundRect(new SKRect(x, areaTop + areaH - h, x + barW, areaTop + areaH), Math.Min(4, barW / 2)), paint);
            var day = d.Day.Day;
            if (day == 1 || day % 5 == 0 || i == days.Count - 1)
            {
                TextCenter(c, day.ToString(CultureInfo.InvariantCulture), x + barW / 2, areaTop + areaH + 10, 16, d.Day == cards.Day ? Faces.Bold : Faces.Medium,
                    d.Day == cards.Day ? C.Brand : C.Ink3);
            }
        }
    }

    // ---------- Регионы или ТП ----------

    private void DrawTable(SKCanvas c, float top, CategorySalesCard card, IReadOnlyList<CategorySalesRow> rows, int more)
    {
        CardBox(c, Pad, top, Inner, TableHeight(rows.Count, more));
        var left = Pad + CardInner;
        var right = Width - Pad - CardInner;
        Text(c, card.BreakdownKind == CategorySalesCards.Regions ? T("Регионы") : T("ТП"), left, top + 22, 24, Faces.Bold, C.Ink);
        if (rows.Any(r => r.PlanKg is not null))
        {
            TextRight(c, T("красным — ниже 80% плана"), right, top + 26, 18, Faces.Medium, C.Ink3);
        }

        // Колонки справа налево: АКБ, сумма, %, план, факт; имя — остальное. Плана нет ни у одной строки — без колонок плана и процента.
        var planned = rows.Any(r => r.PlanKg is not null);
        var columns = new List<(string Title, float Width, Func<CategorySalesRow, (string Text, SKTypeface Face, SKColor Color, float Size)> Cell)>
        {
            (T("АКБ"), 80, r => (ReportFormat.Number(r.Akb), Faces.Medium, C.Ink2, 22)),
            (T("Сумма"), 176, r => (ReportFormat.Short(r.Revenue, Language), Faces.Medium, C.Ink2, 20)),
        };
        if (planned)
        {
            columns.Add(("%", 80, r => (ReportFormat.Percent(r.Execution), Faces.Bold, r.Execution is { } e ? Tone(e) : C.Ink3, 22)));
            columns.Add((T("План, кг"), 128, r => (ReportFormat.Kg(r.PlanKg), Faces.Medium, C.Ink2, 22)));
        }

        columns.Add((T("Факт, кг"), 128, r => (ReportFormat.Kg(r.Kg), Faces.SemiBold, C.Ink, 22)));
        var edges = new float[columns.Count];
        var x = right;
        for (var i = 0; i < columns.Count; i++)
        {
            edges[i] = x;
            x -= columns[i].Width;
        }

        var nameWidth = x - left - 12;
        var head = top + 70;
        Text(c, card.BreakdownKind == CategorySalesCards.Regions ? T("Регион") : T("ТП"), left, head, 18, Faces.SemiBold, C.Ink3);
        for (var i = 0; i < columns.Count; i++)
        {
            TextRight(c, columns[i].Title, edges[i], head, 18, Faces.SemiBold, C.Ink3);
        }

        var y = head + 40;
        using var line = Stroke(C.Border, 1.5f);
        foreach (var row in rows)
        {
            c.DrawLine(left, y - 6, right, y - 6, line);
            TextFit(c, row.Name, left, y + 4, nameWidth, 22, Faces.Medium, C.Ink);
            for (var i = 0; i < columns.Count; i++)
            {
                var (text, face, color, size) = columns[i].Cell(row);
                TextRight(c, text, edges[i], y + 4 + (22 - size) / 2, size, face, color);
            }

            y += TableRow;
        }

        if (more > 0)
        {
            c.DrawLine(left, y - 6, right, y - 6, line);
            Text(c, F("и ещё {0} — на странице категории в «Продажах»", more), left, y + 6, 20, Faces.Medium, C.Ink3);
        }
    }

    // ---------- ТОП артикулов ----------

    private void DrawTop(SKCanvas c, float top, CategorySalesCard card)
    {
        CardBox(c, Pad, top, Inner, 70 + card.Top.Count * SkuRow + 10);
        var left = Pad + CardInner;
        var right = Width - Pad - CardInner;
        Text(c, T("ТОП артикулов месяца"), left, top + 22, 24, Faces.Bold, C.Ink);
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
            var lines = Wrap(sku.Name, Faces.SemiBold, 22, textWidth, 2);
            var ty = y + 6;
            foreach (var text in lines)
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
            TextRight(c, F("прошлый месяц {0} кг", ReportFormat.Kg(sku.PrevMonthKg)), right, y + 70, 16, Faces.Medium, C.Ink3);
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

    // ---------- Пропавшие ----------

    private void DrawLost(SKCanvas c, float top, CategorySalesCard card, int count)
    {
        CardBox(c, Pad, top, Inner, 100 + card.LostSkus.Count * LostRow + (card.LostMore > 0 ? LostRow : 0) - 14);
        var left = Pad + CardInner;
        Text(c, F("Пропали из продаж: {0}", count), left, top + 22, 24, Faces.Bold, C.Red);
        Text(c, T("продавались в прошлом месяце, в этом — нет"), left, top + 56, 18, Faces.Medium, C.Ink3);
        var y = top + 90;
        foreach (var name in card.LostSkus)
        {
            TextFit(c, "• " + name, left, y, Inner - 2 * CardInner, 21, Faces.Medium, C.Ink);
            y += LostRow;
        }

        if (card.LostMore > 0)
        {
            Text(c, F("и ещё {0}", card.LostMore), left, y, 20, Faces.Medium, C.Ink3);
        }
    }

    /// <summary>Цвет выполнения: от 100% — зелёный, 80–99% — оранжевый, ниже 80% — красный (порог ТЗ).</summary>
    private static SKColor Tone(decimal share) => share >= 1 ? C.Green : share >= ReportFormat.RedBelow ? C.Orange : C.Red;
}

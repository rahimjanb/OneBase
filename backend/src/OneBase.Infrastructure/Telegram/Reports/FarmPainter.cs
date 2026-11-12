using System.Globalization;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Reports;
using SkiaSharp;
using C = OneBase.Infrastructure.Telegram.Reports.FarmColors;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>Цвета карточек «поля» (Candy Gold): светлый фон, оранжевые и зелёные акценты, жёлтый итог, карта маршрута.</summary>
internal static class FarmColors
{
    public static readonly SKColor Background = SKColor.Parse("#FFF8EC");
    public static readonly SKColor Card = SKColors.White;
    public static readonly SKColor Border = SKColor.Parse("#EDE3D0");
    public static readonly SKColor Brand = SKColor.Parse("#D63A3A");
    public static readonly SKColor Ink = SKColor.Parse("#2B2118");
    public static readonly SKColor Ink2 = SKColor.Parse("#6B5E52");
    public static readonly SKColor Ink3 = SKColor.Parse("#9A8E82");
    public static readonly SKColor Orange = SKColor.Parse("#E8740C");
    public static readonly SKColor Pink = SKColor.Parse("#D4387C");
    public static readonly SKColor Green = SKColor.Parse("#2E9E4F");
    public static readonly SKColor Red = SKColor.Parse("#E04A4A");
    public static readonly SKColor Amber = SKColor.Parse("#F2A21B");
    public static readonly SKColor Yellow = SKColor.Parse("#FFE066");
    public static readonly SKColor Track = SKColor.Parse("#F1E7D3");
    public static readonly SKColor Map = SKColor.Parse("#CDEBA2");
    public static readonly SKColor MapBorder = SKColor.Parse("#B9DD8E");
    public static readonly SKColor Road = SKColor.Parse("#E9D8B4");
    public static readonly SKColor RoadEdge = SKColor.Parse("#D9C49A");
    public static readonly SKColor Tree = SKColor.Parse("#6CBF4B");
    public static readonly SKColor TreeDark = SKColor.Parse("#4F9E35");
    public static readonly SKColor Trunk = SKColor.Parse("#9C6B3C");
    public static readonly SKColor House = SKColor.Parse("#F6E7C9");
    public static readonly SKColor HouseEdge = SKColor.Parse("#C9A977");
    public static readonly SKColor Window = SKColor.Parse("#9FD3F5");
    public static readonly SKColor Door = SKColor.Parse("#8B5A2B");
    public static readonly SKColor Sign = SKColor.Parse("#6B4A21");
    public static readonly SKColor Test = SKColor.Parse("#E0ECFF");
    public static readonly SKColor TestInk = SKColor.Parse("#1D4ED8");
    public static readonly SKColor PillActiveBg = SKColor.Parse("#FFE9E9");

    /// <summary>Точка без цели и без итога (акции ноября) — серый навес.</summary>
    public const string PlainStatus = "plain";

    public static SKColor Awning(string status) => status switch
    {
        PlainStatus => SKColor.Parse("#BDB3A6"),
        BattleStoreStatus.Taken => Green,
        BattleStoreStatus.Partial => Amber,
        BattleStoreStatus.Refused => Red,
        _ => SKColor.Parse("#F06292"),
    };
}

/// <summary>Картинки карточек из сборки (Telegram/Reports/Assets): пачки Twizos и Yamelly, призы акций.</summary>
internal static class FarmAssets
{
    private static readonly Lazy<Dictionary<string, SKBitmap>> Loaded = new(Load);

    public static IReadOnlyDictionary<string, SKBitmap> Images => Loaded.Value;

    private static Dictionary<string, SKBitmap> Load()
    {
        var result = new Dictionary<string, SKBitmap>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(FarmAssets).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("OneBase.Battle.", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is not null && SKBitmap.Decode(stream) is { } bitmap)
            {
                result[name["OneBase.Battle.".Length..].Replace(".png", "")] = bitmap;
            }
        }

        return result;
    }
}

/// <summary>
/// Общий рисовальщик карточек «поля» (1080 px): шапка «CANDY GOLD · ПОЛЕ БОЯ» с шагами дня или плашкой, счётчики, поле маршрута с домиками,
/// дорогой, деревьями и героем, жёлтый итог, текст (подгонка, перенос, символы вне шрифта). Карточки «Поля боя» и накопительной акции.
/// </summary>
internal abstract class FarmPainter(SkiaReportRenderer.Typefaces faces, string language, bool test)
{
    public const int Width = 1080;
    protected const float Pad = 36;
    protected const float Inner = Width - 2 * Pad;

    protected SkiaReportRenderer.Typefaces Faces { get; } = faces;

    protected bool IsTest { get; } = test;

    protected string Language { get; } = language;

    protected string T(string ru) => ReportTexts.Get(Language, ru);

    protected string F(string ru, params object[] args) => ReportTexts.Format(Language, ru, args);

        protected static string Money(decimal v) => ReportFormat.Number(v);

        protected string Weekday(DateOnly d) => T(d.DayOfWeek switch
        {
            DayOfWeek.Monday => "Пн", DayOfWeek.Tuesday => "Вт", DayOfWeek.Wednesday => "Ср", DayOfWeek.Thursday => "Чт",
            DayOfWeek.Friday => "Пт", DayOfWeek.Saturday => "Сб", _ => "Вс",
        });

        protected static string DayMonth(DateOnly d) => d.ToString("dd.MM", CultureInfo.InvariantCulture);

        // ---------- Общие блоки ----------

        protected void TestStrip(SKCanvas c)
        {
            using var paint = Fill(C.Test);
            c.DrawRect(0, 0, Width, 52, paint);
            var label = T("ТЕСТОВАЯ ОТПРАВКА");
            Spaced(c, label, (Width - Measure(label, Faces.Bold, 20) - 3 * label.Length) / 2, 14, 20, Faces.Bold, C.TestInk, 3);
        }

        protected void Header(SKCanvas c, float top, string title, string subtitle, string slot, int chunk, int chunks, string? badge = null, string? badgeHint = null)
        {
            Spaced(c, "CANDY GOLD · " + T("ПОЛЕ БОЯ"), Pad, top + 30, 20, Faces.Bold, C.Brand, 4);
            SlotIcon(c, Pad + 2, top + 70, slot);
            TextFit(c, title, Pad + 70, top + 56, Inner - 70 - 360, 54, Faces.Bold, C.Ink);
            TextFit(c, subtitle, Pad, top + 122, Inner - 60, 26, Faces.Medium, C.Ink2);
            if (chunks > 1)
            {
                TextRight(c, F("{0} из {1}", chunk + 1, chunks), Width - Pad, top + 126, 22, Faces.SemiBold, C.Ink3);
            }

            // Неделя, месяц, рейтинг — плашка справа (место, неделя, процент) вместо шагов дня.
            if (slot == "saturday" || badge is not null)
            {
                if (badge is not null)
                {
                    Pill(c, Width - Pad - 150, top + 30, 150, 70, badge, badgeHint ?? "", active: true);
                }

                return;
            }

            // Шаги дня: 08:30 старт · 13:00 полдень (16:00 итоги) · 18:00 итог — активный выделен.
            var pills = new[]
            {
                ("08:30", T("старт"), "morning"), slot == "afternoon" ? ("16:00", T("итоги"), "afternoon") : ("13:00", T("полдень"), "midday"), ("18:00", T("итог"), "evening"),
            };
            var x = Width - Pad - 3 * 110 - 2 * 10;
            foreach (var (time, label, code) in pills)
            {
                Pill(c, x, top + 30, 110, 70, time, label, code == slot);
                x += 120;
            }
        }

        protected void Pill(SKCanvas c, float x, float y, float w, float h, string main, string sub, bool active)
        {
            using var bg = Fill(active ? C.PillActiveBg : C.Card);
            using var edge = Stroke(active ? C.Brand : C.Border, active ? 3 : 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 16);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            TextCenter(c, main, x + w / 2, y + 10, 24, Faces.Bold, active ? C.Brand : C.Ink3);
            TextCenter(c, sub, x + w / 2, y + 42, 15, Faces.Medium, active ? C.Brand : C.Ink3);
        }

        /// <summary>Значок заголовка: утро — флажок старта, полдень — солнце, вечер — финишный флаг, неделя — кубок.</summary>
        protected void SlotIcon(SKCanvas c, float x, float y, string slot)
        {
            using var ink = Fill(C.Ink);
            using var brand = Fill(C.Brand);
            using var amber = Fill(C.Amber);
            switch (slot)
            {
                case "team":
                    // Щит.
                    using (var shield = new SKPath())
                    {
                        shield.MoveTo(x + 27, y);
                        shield.LineTo(x + 50, y + 8);
                        shield.LineTo(x + 47, y + 32);
                        shield.QuadTo(x + 42, y + 48, x + 27, y + 56);
                        shield.QuadTo(x + 12, y + 48, x + 7, y + 32);
                        shield.LineTo(x + 4, y + 8);
                        shield.Close();
                        using var steel = Fill(SKColor.Parse("#9DB7C9"));
                        c.DrawPath(shield, steel);
                        using var rim = Stroke(SKColor.Parse("#5E7C91"), 3);
                        c.DrawPath(shield, rim);
                    }

                    break;
                case "zone" or "summary":
                    // Карта: три сложенные полосы.
                    using (var land = Fill(SKColor.Parse("#7CC36A")))
                    using (var sea = Fill(SKColor.Parse("#5DADE2")))
                    {
                        c.DrawRect(x + 2, y + 8, 16, 42, sea);
                        c.DrawRect(x + 18, y + 4, 17, 42, land);
                        c.DrawRect(x + 35, y + 8, 17, 42, sea);
                        c.DrawCircle(x + 26, y + 22, 7, brand);
                    }

                    break;
                case "midday" or "afternoon":
                    c.DrawCircle(x + 26, y + 26, 18, amber);
                    using (var ray = Stroke(C.Amber, 4))
                    {
                        for (var i = 0; i < 8; i++)
                        {
                            var a = i * Math.PI / 4;
                            c.DrawLine(x + 26 + (float)Math.Cos(a) * 24, y + 26 + (float)Math.Sin(a) * 24, x + 26 + (float)Math.Cos(a) * 30, y + 26 + (float)Math.Sin(a) * 30, ray);
                        }
                    }

                    break;
                case "evening":
                    c.DrawRect(x + 8, y + 2, 5, 52, ink);
                    for (var r = 0; r < 3; r++)
                    {
                        for (var col = 0; col < 4; col++)
                        {
                            using var cell = Fill((r + col) % 2 == 0 ? C.Ink : SKColors.White);
                            c.DrawRect(x + 13 + col * 10, y + 4 + r * 10, 10, 10, cell);
                        }
                    }

                    break;
                case "saturday":
                    Trophy(c, x + 26, y + 28, 1.6f);
                    break;
                case "week":
                    c.DrawRect(x + 8, y + 2, 5, 52, ink);
                    for (var r = 0; r < 3; r++)
                    {
                        for (var col = 0; col < 4; col++)
                        {
                            using var cell = Fill((r + col) % 2 == 0 ? C.Ink : SKColors.White);
                            c.DrawRect(x + 13 + col * 10, y + 4 + r * 10, 10, 10, cell);
                        }
                    }

                    break;
                case "month":
                    // Корона.
                    using (var crown = new SKPath())
                    {
                        crown.MoveTo(x + 4, y + 44);
                        crown.LineTo(x + 2, y + 12);
                        crown.LineTo(x + 16, y + 26);
                        crown.LineTo(x + 27, y + 4);
                        crown.LineTo(x + 38, y + 26);
                        crown.LineTo(x + 52, y + 12);
                        crown.LineTo(x + 50, y + 44);
                        crown.Close();
                        c.DrawPath(crown, amber);
                    }

                    c.DrawCircle(x + 27, y + 34, 4, brand);
                    break;
                case "rating":
                    // Медаль на ленте.
                    using (var ribbon = new SKPath())
                    {
                        ribbon.MoveTo(x + 14, y);
                        ribbon.LineTo(x + 24, y);
                        ribbon.LineTo(x + 32, y + 22);
                        ribbon.LineTo(x + 22, y + 22);
                        ribbon.Close();
                        ribbon.MoveTo(x + 40, y);
                        ribbon.LineTo(x + 30, y);
                        ribbon.LineTo(x + 22, y + 22);
                        ribbon.LineTo(x + 32, y + 22);
                        ribbon.Close();
                        c.DrawPath(ribbon, brand);
                    }

                    c.DrawCircle(x + 27, y + 36, 16, amber);
                    using (var rim = Stroke(SKColor.Parse("#C8821A"), 3))
                    {
                        c.DrawCircle(x + 27, y + 36, 11, rim);
                    }

                    break;
                default:
                    c.DrawRect(x + 8, y + 2, 5, 52, ink);
                    using (var flag = new SKPath())
                    {
                        flag.MoveTo(x + 13, y + 4);
                        flag.LineTo(x + 54, y + 16);
                        flag.LineTo(x + 13, y + 30);
                        flag.Close();
                        c.DrawPath(flag, brand);
                    }

                    break;
            }
        }

        /// <summary>Три счётчика; Sub — мелкая строка под значением справа (у «Магазины с целями» — сколько точек в маршруте).</summary>
        protected void Counters(SKCanvas c, float top, params (string Label, string Value, SKColor Color, string? Sub)[] items)
        {
            var w = (Inner - 2 * 16) / 3;
            for (var i = 0; i < items.Length; i++)
            {
                var x = Pad + i * (w + 16);
                CardBox(c, x, top, w, 108);
                TextFit(c, items[i].Label, x + 22, top + 18, w - 44, 20, Faces.Medium, C.Ink2);
                using var font = Font(Faces.Bold, 44);
                using var paint = Fill(items[i].Color);
                var value = Clean(items[i].Value);
                c.DrawText(value, x + 22, TopBaseline(font, top + 46), SKTextAlign.Left, font, paint);
                if (items[i].Sub is { } sub)
                {
                    var left = x + 22 + font.MeasureText(value) + 14;
                    TextFit(c, sub, left, top + 66, x + w - 18 - left, 16, Faces.Medium, C.Ink3);
                }
            }
        }

        /// <summary>Приз набора: кружка (300), термос (400), чайник (600) — простыми фигурами.</summary>
        protected void PrizeIcon(SKCanvas c, float cx, float cy, string level)
        {
            using var body = Fill(level == "600" ? SKColor.Parse("#9AA5B1") : level == "400" ? C.Brand : SKColor.Parse("#F4B942"));
            using var edge = Stroke(C.Ink2, 2);
            switch (level)
            {
                case "400":
                    c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 9, cy - 20, cx + 9, cy + 20), 5), body);
                    c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 7, cy - 24, cx + 7, cy - 16), 3), edge);
                    break;
                case "600":
                    c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 18, cy - 8, cx + 14, cy + 18), 8), body);
                    c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 10, cy - 14, cx + 6, cy - 6), 3), body);
                    using (var spout = new SKPath())
                    {
                        spout.MoveTo(cx + 12, cy - 2);
                        spout.LineTo(cx + 24, cy - 12);
                        spout.LineTo(cx + 24, cy - 4);
                        spout.LineTo(cx + 14, cy + 6);
                        spout.Close();
                        c.DrawPath(spout, body);
                    }

                    c.DrawArc(new SKRect(cx - 16, cy - 24, cx + 8, cy - 2), 200, 140, false, edge);
                    break;
                default:
                    c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 14, cy - 16, cx + 10, cy + 16), 4), body);
                    c.DrawArc(new SKRect(cx + 6, cy - 8, cx + 22, cy + 8), 270, 180, false, edge);
                    break;
            }
        }

        protected void Trophy(SKCanvas c, float cx, float cy, float s)
        {
            using var gold = Fill(C.Amber);
            using var dark = Fill(SKColor.Parse("#C8821A"));
            using var cup = new SKPath();
            cup.MoveTo(cx - 14 * s, cy - 16 * s);
            cup.LineTo(cx + 14 * s, cy - 16 * s);
            cup.LineTo(cx + 9 * s, cy + 4 * s);
            cup.LineTo(cx - 9 * s, cy + 4 * s);
            cup.Close();
            c.DrawPath(cup, gold);
            c.DrawRect(cx - 3 * s, cy + 4 * s, 6 * s, 7 * s, dark);
            c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 11 * s, cy + 11 * s, cx + 11 * s, cy + 16 * s), 2 * s), dark);
            using var handle = Stroke(C.Amber, 3 * s);
            c.DrawArc(new SKRect(cx - 24 * s, cy - 14 * s, cx - 8 * s, cy - 2 * s), 90, 180, false, handle);
            c.DrawArc(new SKRect(cx + 8 * s, cy - 14 * s, cx + 24 * s, cy - 2 * s), 270, 180, false, handle);
        }

        // ---------- Поле маршрута ----------

        /// <summary>Магазин на поле маршрута: статус (навес и значок) и номер.</summary>
        protected readonly record struct FarmStop(string Status, int Order);

        /// <summary>
        /// Поле маршрута: зелёная карта, домики по два в ряд вдоль дороги змейкой, пройденный путь зелёными следами (кроме утра), деревья,
        /// выноски (рисует наследник: индекс магазина и центр домика), герой у старта, следующего магазина или финиша, подпись страницы.
        /// Маршрут на нескольких картинках: total — магазинов во всём маршруте (подпись «магазины 1–16» и на первой части), hero — герой на
        /// этой части (<see cref="HeroOnPage"/>: один на весь маршрут, а не в конце каждой картинки).
        /// </summary>
        protected void Farm(SKCanvas c, float top, float height, IReadOnlyList<FarmStop> stops, IReadOnlyList<float> rowHeights, string slot, int offset,
            Action<int, float, float> callout, int total = 0, bool hero = true)
        {
            using var bg = Fill(C.Map);
            using var edge = Stroke(C.MapBorder, 3);
            var rect = new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + height), 24);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);

            // Центры домиков: два в ряд, дорога змейкой.
            var leftX = Pad + 330f;
            var rightX = Width - Pad - 330f;
            var points = new List<(float X, float Y, int Index)>();
            var y = top + 48;
            for (var r = 0; r < rowHeights.Count; r++)
            {
                var cy = y + rowHeights[r] / 2;
                var pair = Math.Max(0, Math.Min(2, stops.Count - r * 2));
                var leftFirst = r % 2 == 0;
                for (var i = 0; i < pair; i++)
                {
                    var x = (i == 0) == leftFirst ? leftX : rightX;
                    points.Add((x, cy, r * 2 + i));
                }

                y += rowHeights[r];
            }

            // Дорога.
            if (points.Count > 1)
            {
                using var path = new SKPath();
                path.MoveTo(points[0].X - 70, points[0].Y + 30);
                for (var i = 0; i < points.Count; i++)
                {
                    var (x, py, _) = points[i];
                    if (i > 0 && Math.Abs(points[i - 1].Y - py) > 1 && Math.Abs(points[i - 1].X - x) < 1)
                    {
                        // Поворот между рядами — дугой у края.
                        var side = x > Width / 2 ? 1 : -1;
                        path.CubicTo(x + side * 150, points[i - 1].Y + 30, x + side * 150, py + 30, x, py + 30);
                    }
                    else
                    {
                        path.LineTo(x, py + 30);
                    }
                }

                path.LineTo(points[^1].X + (points[^1].X > Width / 2 ? 70 : -70), points[^1].Y + 30);
                using var roadEdge = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 34, Color = C.RoadEdge, IsAntialias = true, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
                using var road = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 26, Color = C.Road, IsAntialias = true, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
                c.DrawPath(path, roadEdge);
                c.DrawPath(path, road);
                if (slot != "morning")
                {
                    // Пройденный путь — зелёные следы до последнего пройденного магазина.
                    var lastPassed = points.Select((p, i) => (p, i)).Where(x => stops[x.p.Index].Status is not (BattleStoreStatus.Ahead or C.PlainStatus)).Select(x => x.i).DefaultIfEmpty(-1).Max();
                    if (lastPassed >= 0)
                    {
                        using var steps = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 7, Color = C.Green, IsAntialias = true, StrokeCap = SKStrokeCap.Round, PathEffect = SKPathEffect.CreateDash([2, 16], 0) };
                        using var done = new SKPath();
                        done.MoveTo(points[0].X - 70, points[0].Y + 30);
                        for (var i = 0; i <= lastPassed; i++)
                        {
                            done.LineTo(points[i].X, points[i].Y + 30);
                        }

                        c.DrawPath(done, steps);
                    }
                }
            }

            // Деревья между домиками.
            for (var r = 0; r < rowHeights.Count; r++)
            {
                var cy = top + 48 + rowHeights.Take(r).Sum() + rowHeights[r] / 2;
                Tree(c, Width / 2 - 30, cy - 20, 0.9f);
                Tree(c, Width / 2 + 34, cy + 6, 0.7f);
            }

            // Домики и выноски.
            var heroAt = -1;
            if (slot is "midday" or "afternoon")
            {
                heroAt = points.FindIndex(p => stops[p.Index].Status is BattleStoreStatus.Ahead or C.PlainStatus);
            }

            foreach (var p in points)
            {
                var status = stops[p.Index].Status;
                House(c, p.X, p.Y, 1f, status, slot == "morning" ? null : status, stops[p.Index].Order);
                callout(p.Index, p.X, p.Y);
            }

            if (points.Count > 0 && hero)
            {
                static (float, float) Beside((float X, float Y, int Index) p) => (p.X + (p.X > Width / 2 ? -96 : 96), p.Y + 6);
                var at = slot switch
                {
                    "morning" => Beside(points[0]),
                    "evening" => Beside(points[^1]),
                    _ => Beside(heroAt >= 0 ? points[heroAt] : points[^1]),
                };
                Hero(c, at.Item1, at.Item2);
            }

            var part = offset > 0 || total > offset + stops.Count;
            TextRight(c, part ? F("магазины {0}–{1}", offset + 1, offset + stops.Count) : "", Width - Pad - 20, top + 14, 15, Faces.Medium, C.Ink3);
        }

        /// <summary>
        /// Герой на части маршрута [from, from + count) из statuses (весь маршрут): утром — у старта (первая часть), вечером — у финиша (последняя),
        /// днём — у первого непройденного магазина, все пройдены — у финиша.
        /// </summary>
        protected static bool HeroOnPage(string slot, IReadOnlyList<string> statuses, int from, int count)
        {
            if (statuses.Count == 0 || count <= 0)
            {
                return false;
            }

            var last = from + count >= statuses.Count;
            if (slot == "morning")
            {
                return from == 0;
            }

            if (slot == "evening")
            {
                return last;
            }

            var next = statuses.ToList().FindIndex(s => s is BattleStoreStatus.Ahead or C.PlainStatus);
            return next < 0 ? last : next >= from && next < from + count;
        }

        protected void House(SKCanvas c, float cx, float cy, float s, string status, string? badgeStatus, int number)
        {
            var w = 112 * s;
            var h = 90 * s;
            var x = cx - w / 2;
            var y = cy - h / 2;
            using var body = Fill(C.House);
            using var edge = Stroke(C.HouseEdge, 2 * s);
            c.DrawRoundRect(new SKRoundRect(new SKRect(x, y + 10 * s, x + w, y + h), 6 * s), body);
            c.DrawRoundRect(new SKRoundRect(new SKRect(x, y + 10 * s, x + w, y + h), 6 * s), edge);
            // Вывеска и навес: цвет навеса — статус.
            using var sign = Fill(C.Sign);
            c.DrawRoundRect(new SKRoundRect(new SKRect(x + 6 * s, y + 14 * s, x + w - 6 * s, y + 30 * s), 3 * s), sign);
            if (s >= 0.8f)
            {
                TextCenter(c, T("МАГАЗИН"), cx, y + 15 * s, 11 * s, Faces.Bold, SKColor.Parse("#FFE9A8"));
            }

            var awning = C.Awning(status);
            var stripes = 7;
            var sw = (w + 8 * s) / stripes;
            for (var i = 0; i < stripes; i++)
            {
                using var stripe = Fill(i % 2 == 0 ? awning : SKColors.White);
                var sx = x - 4 * s + i * sw;
                using var p = new SKPath();
                p.MoveTo(sx, y + 32 * s);
                p.LineTo(sx + sw, y + 32 * s);
                p.LineTo(sx + sw, y + 44 * s);
                p.ArcTo(new SKRect(sx, y + 38 * s, sx + sw, y + 50 * s), 0, 180, false);
                p.Close();
                c.DrawPath(p, stripe);
            }

            using var win = Fill(C.Window);
            using var door = Fill(C.Door);
            c.DrawRoundRect(new SKRoundRect(new SKRect(x + 12 * s, y + 54 * s, x + 40 * s, y + 76 * s), 3 * s), win);
            c.DrawRoundRect(new SKRoundRect(new SKRect(x + w - 40 * s, y + 54 * s, x + w - 12 * s, y + 76 * s), 3 * s), win);
            c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 10 * s, y + 56 * s, cx + 10 * s, y + h), 3 * s), door);

            // Значок: номер (впереди) или ✓ ½ ✕; без номера и статуса — без значка (недостроенный дом «города героя»).
            if (badgeStatus is null && number <= 0)
            {
                return;
            }

            var bx = x - 2 * s;
            var by = y + 2 * s;
            var r = 17 * s;
            var (badgeColor, badgeText, badgeInk) = badgeStatus switch
            {
                BattleStoreStatus.Taken => (C.Green, "✓", SKColors.White),
                BattleStoreStatus.Partial => (C.Amber, "½", SKColors.White),
                BattleStoreStatus.Refused => (C.Red, "×", SKColors.White),
                _ => (SKColors.White, number.ToString(CultureInfo.InvariantCulture), C.Ink),
            };
            using var badge = Fill(badgeColor);
            using var badgeEdge = Stroke(C.Ink, 2 * s);
            c.DrawCircle(bx, by, r, badge);
            c.DrawCircle(bx, by, r, badgeEdge);
            if (badgeStatus == BattleStoreStatus.Refused)
            {
                // Крест — линиями: в шрифте Inter нет «✕».
                using var cross = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3.5f * s, StrokeCap = SKStrokeCap.Round };
                var d = 6.5f * s;
                c.DrawLine(bx - d, by - d, bx + d, by + d, cross);
                c.DrawLine(bx - d, by + d, bx + d, by - d, cross);
            }
            else
            {
                TextCenter(c, badgeText, bx, by - 11 * s, 17 * s, Faces.Bold, badgeInk);
            }
        }

        protected static void DrawPack(SKCanvas c, SKBitmap pack, float x, float y, float w, float h)
        {
            var scale = Math.Min(w / pack.Width, h / pack.Height);
            var dw = pack.Width * scale;
            var dh = pack.Height * scale;
            using var paint = new SKPaint { IsAntialias = true };
            c.DrawBitmap(pack, new SKRect(x + (w - dw) / 2, y + (h - dh) / 2, x + (w - dw) / 2 + dw, y + (h - dh) / 2 + dh), paint);
        }

        protected void Tree(SKCanvas c, float cx, float cy, float s)
        {
            using var trunk = Fill(C.Trunk);
            using var crown = Fill(C.Tree);
            using var dark = Fill(C.TreeDark);
            c.DrawRect(cx - 4 * s, cy, 8 * s, 18 * s, trunk);
            c.DrawCircle(cx, cy - 8 * s, 18 * s, dark);
            c.DrawCircle(cx - 4 * s, cy - 12 * s, 14 * s, crown);
        }

        /// <summary>Герой-ТП: кепка, лицо, красная куртка, сумка.</summary>
        protected void Hero(SKCanvas c, float cx, float cy)
        {
            using var skin = Fill(SKColor.Parse("#F3C9A5"));
            using var red = Fill(C.Brand);
            using var cap = Fill(SKColor.Parse("#B52C2C"));
            using var dark = Fill(C.Ink);
            using var bag = Fill(C.Amber);
            c.DrawCircle(cx, cy - 26, 14, skin);
            c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 15, cy - 44, cx + 15, cy - 32), 6), cap);
            c.DrawRect(cx - 15, cy - 36, 22, 5, cap);
            c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 14, cy - 12, cx + 14, cy + 22), 7), red);
            c.DrawRoundRect(new SKRoundRect(new SKRect(cx - 12, cy + 20, cx - 3, cy + 40), 3), dark);
            c.DrawRoundRect(new SKRoundRect(new SKRect(cx + 3, cy + 20, cx + 12, cy + 40), 3), dark);
            c.DrawRoundRect(new SKRoundRect(new SKRect(cx + 12, cy - 2, cx + 30, cy + 16), 4), bag);
        }

        protected void Footer(SKCanvas c, float top, string text)
        {
            using var bg = Fill(C.Yellow);
            c.DrawRoundRect(new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + 92), 20), bg);
            var lines = Wrap(text, Faces.Bold, 22, Inner - 48, 2);
            var y = top + (92 - lines.Count * 28) / 2;
            foreach (var line in lines)
            {
                TextCenter(c, line, Width / 2, y, 22, Faces.Bold, C.Ink);
                y += 28;
            }
        }

        protected void CardBox(SKCanvas c, float x, float y, float w, float h)
        {
            using var bg = Fill(C.Card);
            using var edge = Stroke(C.Border, 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 22);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
        }

        protected static byte[] Png(SKSurface surface)
        {
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }

        // ---------- Текст ----------

        protected static SKFont Font(SKTypeface face, float size) => new(face, size) { Edging = SKFontEdging.Antialias, Subpixel = true };
        protected static SKPaint Fill(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        protected static SKPaint Stroke(SKColor color, float width) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width };
        protected static float TopBaseline(SKFont font, float top) => top - font.Metrics.Ascent * 0.92f;

        protected float Measure(string text, SKTypeface face, float size)
        {
            using var font = Font(face, size);
            return font.MeasureText(Clean(text));
        }

        protected void Text(SKCanvas c, string text, float x, float top, float size, SKTypeface face, SKColor color)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            c.DrawText(Clean(text), x, TopBaseline(font, top), SKTextAlign.Left, font, paint);
        }

        protected void TextRight(SKCanvas c, string text, float right, float top, float size, SKTypeface face, SKColor color)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            c.DrawText(Clean(text), right, TopBaseline(font, top), SKTextAlign.Right, font, paint);
        }

        protected void TextCenter(SKCanvas c, string text, float cx, float top, float size, SKTypeface face, SKColor color)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            c.DrawText(Clean(text), cx, TopBaseline(font, top), SKTextAlign.Center, font, paint);
        }

        protected void TextFit(SKCanvas c, string text, float x, float top, float width, float size, SKTypeface face, SKColor color)
        {
            var clean = Clean(text);
            using var font = Font(face, size);
            while (font.MeasureText(clean) > width && font.Size > size * 0.72f)
            {
                font.Size -= 0.5f;
            }

            var fitted = font.MeasureText(clean) <= width ? clean : Ellipsis(clean, font, width);
            using var paint = Fill(color);
            c.DrawText(fitted, x, TopBaseline(font, top) + (size - font.Size) * 0.35f, SKTextAlign.Left, font, paint);
        }

        protected static string Ellipsis(string text, SKFont font, float width)
        {
            var t = text.TrimEnd('…', ' ');
            while (t.Length > 1 && font.MeasureText(t + "…") > width)
            {
                t = t[..^1];
            }

            return t.TrimEnd() + "…";
        }

        protected List<string> Wrap(string text, SKTypeface face, float size, float width, int maxLines)
        {
            var clean = Clean(text);
            using var font = Font(face, size);
            if (font.MeasureText(clean) <= width)
            {
                return [clean];
            }

            var lines = new List<string>();
            var current = "";
            var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var used = 0;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (font.MeasureText(candidate) <= width || current.Length == 0)
                {
                    current = candidate;
                    used++;
                    continue;
                }

                lines.Add(current);
                current = word;
                used++;
                if (lines.Count == maxLines - 1)
                {
                    break;
                }
            }

            var rest = string.Join(' ', words.Skip(used - 1));
            lines.Add(font.MeasureText(rest) <= width ? rest : Ellipsis(rest, font, width));
            return lines.Where(l => l.Length > 0).Take(maxLines).ToList();
        }

        protected void Spaced(SKCanvas c, string text, float x, float top, float size, SKTypeface face, SKColor color, float spacing)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            var baseline = TopBaseline(font, top);
            foreach (var ch in Clean(text))
            {
                var s = ch.ToString();
                c.DrawText(s, x, baseline, SKTextAlign.Left, font, paint);
                x += font.MeasureText(s) + spacing;
            }
        }

        /// <summary>Символы вне шрифта (эмодзи в названиях Linko) убираются; галочка, крест и ½ есть в Inter.</summary>
        protected string Clean(string text)
        {
            if (text.Length == 0)
            {
                return text;
            }

            var face = Faces.Regular;
            var sb = new System.Text.StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsSurrogate(text[i]) && !char.IsSurrogatePair(text, i))
                {
                    continue;
                }

                var cp = char.ConvertToUtf32(text, i);
                if (char.IsSurrogatePair(text, i))
                {
                    i++;
                }

                if (cp is '\n' or '\r' or '\t')
                {
                    sb.Append(' ');
                }
                else if (face.ContainsGlyph(cp))
                {
                    sb.Append(char.ConvertFromUtf32(cp));
                }
            }

            return sb.ToString();
        }
}

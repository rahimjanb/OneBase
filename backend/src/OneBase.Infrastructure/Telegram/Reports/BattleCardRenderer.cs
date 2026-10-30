using System.Globalization;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using SkiaSharp;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Карточки «Поля боя» (ТЗ v2, образцы «Карточки/»): PNG шириной 1080 px, светлый фон, «CANDY GOLD · ПОЛЕ БОЯ», ТП и дата, счётчики, бонус
/// недели лестницей, призовые наборы, карта маршрута — магазины-домики вдоль дороги с целями в выносках, статус навесом и значком, герой на
/// дороге; суббота — «6 полян». Домики, деревья, герой и призы рисуются фигурами, пачки товаров — PNG из сборки. Высота — по числу магазинов;
/// больше StoresPerPage магазинов — продолжение второй картинкой. У СВ — карточка каждого ТП своей страницей.
/// </summary>
internal static class BattleCardRenderer
{
    public const int Width = 1080;
    private const float Pad = 36;
    private const float Inner = Width - 2 * Pad;
    /// <summary>Магазинов на картинке дня у ТП; у СВ (несколько ТП) — вся карточка ТП одной картинкой, чтобы альбом не превысил 10 фото.</summary>
    private const int StoresPerPage = 10;
    private const int StoresPerPageSv = 20;
    private const float RowHeight = 196;

    private static readonly Lazy<Dictionary<string, SKBitmap>> Images = new(LoadImages);

    private static class C
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

        public static SKColor Awning(string status) => status switch
        {
            BattleStoreStatus.Taken => Green,
            BattleStoreStatus.Partial => Amber,
            BattleStoreStatus.Refused => Red,
            _ => SKColor.Parse("#F06292"),
        };
    }

    public static IReadOnlyList<byte[]> Render(ReportDocument doc, BattleCards cards, SkiaReportRenderer.Typefaces faces)
    {
        var painter = new Painter(doc, cards, faces);
        var pages = new List<byte[]>();
        if (cards.IsWeek)
        {
            pages.AddRange(cards.Weeks.Take(10).Select(painter.Week));
        }
        else
        {
            var perPage = cards.Days.Count > 1 ? StoresPerPageSv : StoresPerPage;
            foreach (var day in cards.Days.Take(10))
            {
                var chunks = Math.Max(1, (int)Math.Ceiling(day.Stores.Count / (double)perPage));
                for (var i = 0; i < chunks; i++)
                {
                    pages.Add(painter.Day(day, i, chunks, perPage));
                }
            }
        }

        return pages;
    }

    private static Dictionary<string, SKBitmap> LoadImages()
    {
        var result = new Dictionary<string, SKBitmap>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(BattleCardRenderer).Assembly;
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

    /// <summary>Пачка для цели: Твизос — вкус по коду товара (Kabob по умолчанию), Могул — Yamelly 420 г по магазину.</summary>
    private static SKBitmap? Pack(BattleGoalView goal, long marketId)
    {
        var images = Images.Value;
        if (goal.Kind == BattleGoalKind.Twizos)
        {
            var file = goal.ProductCode switch
            {
                "601" or "602" => "Twizos_Dengiz_tuzi",
                "613" or "614" => "Twizos_Smetana_va_piyoz",
                "610" or "611" => "Twizos_Smetana_va_kokatlar",
                "607" or "608" => "Twizos_Pishloq",
                "616" or "617" => "Twizos_Kolbaska_chili",
                _ => "Twizos_Kabob",
            };
            return images.GetValueOrDefault(file);
        }

        string[] mogul = ["Mogul_Yamelly_Arbuz_420g", "Mogul_Yamelly_Sliva_420g", "Mogul_Yamelly_Tropik_420g", "Mogul_Yamelly_Vesna_420g", "Mogul_Yamelly_Yagoda_420g", "Mogul_Yamelly_Zima_420g"];
        return images.GetValueOrDefault(mogul[(int)(Math.Abs(marketId) % mogul.Length)]);
    }

    private sealed class Painter(ReportDocument doc, BattleCards cards, SkiaReportRenderer.Typefaces faces)
    {
        private readonly string _lang = cards.Language;

        private string T(string ru) => ReportTexts.Get(_lang, ru);
        private string F(string ru, params object[] args) => ReportTexts.Format(_lang, ru, args);

        private static string Money(decimal v) => ReportFormat.Number(v);

        private string Weekday(DateOnly d) => T(d.DayOfWeek switch
        {
            DayOfWeek.Monday => "Пн", DayOfWeek.Tuesday => "Вт", DayOfWeek.Wednesday => "Ср", DayOfWeek.Thursday => "Чт",
            DayOfWeek.Friday => "Пт", DayOfWeek.Saturday => "Сб", _ => "Вс",
        });

        private static string DayMonth(DateOnly d) => d.ToString("dd.MM", CultureInfo.InvariantCulture);

        // ---------- Карточка дня ----------

        public byte[] Day(BattleDay day, int chunk, int chunks, int perPage)
        {
            var stores = day.Stores.Skip(chunk * perPage).Take(perPage).ToList();
            var rows = Math.Max(1, (stores.Count + 1) / 2);
            var first = chunk == 0;
            var strip = doc.Test ? 52f : 0f;
            var headerBottom = strip + (first ? 170f : 150f);
            var countersTop = headerBottom;
            var bonusTop = countersTop + 124;
            var prizesTop = bonusTop + 186;
            var mapTop = first ? prizesTop + 150 : headerBottom;
            var rowHeights = Enumerable.Range(0, rows).Select(r => RowHeightOf(stores.Skip(r * 2).Take(2).ToList())).ToList();
            var mapHeight = 48 + rowHeights.Sum() + 40;
            var footerTop = mapTop + mapHeight + 20;
            var height = (int)Math.Ceiling(footerTop + 92 + Pad);

            using var surface = SKSurface.Create(new SKImageInfo(Width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            var c = surface.Canvas;
            c.Clear(C.Background);
            if (doc.Test)
            {
                TestStrip(c);
            }

            var slot = cards.Slot;
            var title = slot switch { "midday" => T("Полдень"), "evening" => T("Итог дня"), _ => T("Маршрут дня") };
            Header(c, strip, title, $"{day.AgentName} · {day.Region ?? ""} · {Weekday(day.Date)} {DayMonth(day.Date)}", slot, chunk, chunks);

            if (first)
            {
                Counters(c, countersTop,
                    (T("Твизос — взято"), $"{day.TwizosTaken} / {day.TwizosTotal}", C.Orange, null),
                    (T("Могул — взято"), $"{day.MogulTaken} / {day.MogulTotal}", C.Pink, null),
                    (T("Магазины с целями"), $"{day.StoresPassed} / {day.Stores.Count}", C.Ink,
                        day.RoutePoints > day.Stores.Count ? F("из {0} точек маршрута", day.RoutePoints) : null));
                var weekLabel = slot switch
                {
                    "morning" => day.Date.DayOfWeek == DayOfWeek.Monday ? T("старт") : F("после {0}", Weekday(day.Date.AddDays(-1)).ToLowerInvariant()),
                    "midday" => F("{0}, полдень", Weekday(day.Date).ToLowerInvariant()),
                    _ => F("после {0}", Weekday(day.Date).ToLowerInvariant()),
                };
                Bonus(c, bonusTop, day.Week, F("неделя {0}–{1} · {2}", DayMonth(BattleRules.Monday(day.Date)), DayMonth(BattleRules.Monday(day.Date).AddDays(5)), weekLabel));
                Prizes(c, prizesTop, day.MogulActive);
            }

            MapPanel(c, mapTop, mapHeight, stores, rowHeights, day, slot, chunk * perPage);
            Footer(c, footerTop, DayVerdict(day, slot, chunk, chunks));
            return Png(surface);
        }

        private string DayVerdict(BattleDay day, string slot, int chunk, int chunks)
        {
            if (chunk < chunks - 1)
            {
                return F("Продолжение маршрута — на следующей картинке ({0} из {1})", chunk + 2, chunks);
            }

            var first = day.Stores.FirstOrDefault();
            var next = day.Week.NextStep;
            var toBonus = next is null ? T("все ступени бонуса взяты") : F("до {0} сум ещё {1}", Money(next.Sum), day.Week.ToNextStep);
            return slot switch
            {
                "morning" => F("Сегодня {0} магазинов · {1} целей · старт с магазина 1 «{2}»", day.Stores.Count, day.Total, first?.Name ?? "—"),
                "midday" => F("Полдень: {0} из {1} целей · впереди {2} магазинов · {3}", day.Taken, day.Total, day.Stores.Count - day.StoresPassed, toBonus),
                _ => day.Refusals > 0
                    ? F("НА МУШКЕ · {0} из {1} целей · {2} отказов — эти магазины в маршруте следующего {3} · {4}", day.Taken, day.Total, day.Refusals,
                        Weekday(day.Date).ToLowerInvariant(), toBonus)
                    : F("Итог дня: {0} из {1} целей · {2}", day.Taken, day.Total, toBonus),
            };
        }

        // ---------- Карточка недели ----------

        public byte[] Week(BattleWeek week)
        {
            var strip = doc.Test ? 52f : 0f;
            var countersTop = strip + 170;
            var panelTop = countersTop + 124;
            var maxStores = Math.Max(1, week.Days.Max(d => d.Stores.Count));
            var houseRows = (int)Math.Ceiling(Math.Min(maxStores, 20) / 4.0);
            var tileHeight = 76 + houseRows * 58 + 16;
            var panelHeight = 60 + 2 * tileHeight + 3 * 16;
            var bonusTop = panelTop + panelHeight + 20;
            var footerTop = bonusTop + 186;
            var height = (int)Math.Ceiling(footerTop + 92 + Pad);

            using var surface = SKSurface.Create(new SKImageInfo(Width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            var c = surface.Canvas;
            c.Clear(C.Background);
            if (doc.Test)
            {
                TestStrip(c);
            }

            Header(c, strip, T("Итог недели"), $"{week.AgentName} · {week.Region ?? ""} · {DayMonth(week.Monday)}–{DayMonth(week.Saturday)}", "saturday", 0, 1,
                week.Rank is { } r ? F("{0}-е место", r) : null, week.Participants > 0 ? F("из {0}", week.Participants) : null);
            var best = week.BestDay;
            Counters(c, countersTop,
                (T("Твизос — взято"), $"{week.TwizosTaken} / {week.TwizosTotal}", C.Orange, null),
                (T("Могул — взято"), $"{week.MogulTaken} / {week.MogulTotal}", C.Pink, null),
                (T("Лучший день"), best is null ? "—" : $"{Weekday(best.Date)} · {ReportFormat.Percent(best.Share)}", C.Green, null));

            // 6 полян.
            CardBox(c, Pad, panelTop, Inner, panelHeight);
            Text(c, T("6 дней — 6 полян"), Pad + 24, panelTop + 18, 26, faces.Bold, C.Ink);
            var tileWidth = (Inner - 2 * 16 - 2 * 16) / 3;
            for (var i = 0; i < 6; i++)
            {
                var day = week.Days[i];
                var x = Pad + 16 + (i % 3) * (tileWidth + 16);
                var y = panelTop + 60 + (i / 3) * (tileHeight + 16);
                WeekTile(c, x, y, tileWidth, tileHeight, day);
            }

            var akb = week.Progress.NewAkbOk
                ? F("новых АКБ: {0} (нужно ≥ {1} ✓)", week.Progress.NewTaken, Math.Min(cardsMinNewAkb, week.Progress.NewTotal))
                : F("новых АКБ: {0} (нужно ≥ {1}) — бонус не начислен", week.Progress.NewTaken, Math.Min(cardsMinNewAkb, week.Progress.NewTotal));
            Bonus(c, bonusTop, week.Progress, akb);
            Footer(c, footerTop, F("Бонус недели: {0} сум · {1} целей · выплата после проверки доставки в понедельник", Money(week.Progress.Bonus),
                ReportFormat.Percent(week.Progress.Share)));
            return Png(surface);
        }

        private int cardsMinNewAkb => 5;

        private void WeekTile(SKCanvas c, float x, float y, float w, float h, BattleWeekDay day)
        {
            using var bg = Fill(C.Map);
            using var edge = Stroke(C.MapBorder, 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 18);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            Text(c, Weekday(day.Date), x + 16, y + 12, 26, faces.Bold, C.Ink);
            var share = day.Total == 0 ? (decimal?)null : day.Share;
            TextRight(c, day.Total == 0 ? "—" : ReportFormat.Percent(share), x + w - 16, y + 12, 26, faces.Bold, share is null ? C.Ink3 : share >= 0.5m ? C.Green : share >= 0.3m ? C.Amber : C.Red);
            var goals = day.Stores.SelectMany(s => s.Goals).ToList();
            Text(c, F("Твизос {0}/{1} · Могул {2}/{3}", goals.Count(g => g.Kind == BattleGoalKind.Twizos && g.IsTaken), goals.Count(g => g.Kind == BattleGoalKind.Twizos),
                goals.Count(g => g.Kind == BattleGoalKind.Mogul && g.IsTaken), goals.Count(g => g.Kind == BattleGoalKind.Mogul)), x + 16, y + 44, 15, faces.Medium, C.Ink2);
            if (day.Stores.Count == 0)
            {
                Text(c, T("целей нет"), x + 16, y + 80, 16, faces.Medium, C.Ink3);
                return;
            }

            var cell = (w - 32) / 4;
            foreach (var (store, i) in day.Stores.Take(20).Select((s, i) => (s, i)))
            {
                var cx = x + 16 + (i % 4) * cell + cell / 2;
                var cy = y + 76 + (i / 4) * 58 + 26;
                House(c, cx, cy, 0.42f, store.Status, store.Status == BattleStoreStatus.Ahead ? null : store.Status, store.Order);
            }
        }

        // ---------- Общие блоки ----------

        private void TestStrip(SKCanvas c)
        {
            using var paint = Fill(C.Test);
            c.DrawRect(0, 0, Width, 52, paint);
            var label = T("ТЕСТОВАЯ ОТПРАВКА");
            Spaced(c, label, (Width - Measure(label, faces.Bold, 20) - 3 * label.Length) / 2, 14, 20, faces.Bold, C.TestInk, 3);
        }

        private void Header(SKCanvas c, float top, string title, string subtitle, string slot, int chunk, int chunks, string? badge = null, string? badgeHint = null)
        {
            Spaced(c, "CANDY GOLD · " + T("ПОЛЕ БОЯ"), Pad, top + 30, 20, faces.Bold, C.Brand, 4);
            SlotIcon(c, Pad + 2, top + 70, slot);
            TextFit(c, title, Pad + 70, top + 56, Inner - 70 - 360, 54, faces.Bold, C.Ink);
            TextFit(c, subtitle, Pad, top + 122, Inner - 60, 26, faces.Medium, C.Ink2);
            if (chunks > 1)
            {
                TextRight(c, F("{0} из {1}", chunk + 1, chunks), Width - Pad, top + 126, 22, faces.SemiBold, C.Ink3);
            }

            if (slot == "saturday")
            {
                if (badge is not null)
                {
                    Pill(c, Width - Pad - 150, top + 30, 150, 70, badge, badgeHint ?? "", active: true);
                }

                return;
            }

            // Шаги дня: 08:30 старт · 13:00 полдень · 18:00 итог — активный выделен.
            var pills = new[] { ("08:30", T("старт"), "morning"), ("13:00", T("полдень"), "midday"), ("18:00", T("итог"), "evening") };
            var x = Width - Pad - 3 * 110 - 2 * 10;
            foreach (var (time, label, code) in pills)
            {
                Pill(c, x, top + 30, 110, 70, time, label, code == slot);
                x += 120;
            }
        }

        private void Pill(SKCanvas c, float x, float y, float w, float h, string main, string sub, bool active)
        {
            using var bg = Fill(active ? C.PillActiveBg : C.Card);
            using var edge = Stroke(active ? C.Brand : C.Border, active ? 3 : 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 16);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            TextCenter(c, main, x + w / 2, y + 10, 24, faces.Bold, active ? C.Brand : C.Ink3);
            TextCenter(c, sub, x + w / 2, y + 42, 15, faces.Medium, active ? C.Brand : C.Ink3);
        }

        /// <summary>Значок заголовка: утро — флажок старта, полдень — солнце, вечер — финишный флаг, неделя — кубок.</summary>
        private void SlotIcon(SKCanvas c, float x, float y, string slot)
        {
            using var ink = Fill(C.Ink);
            using var brand = Fill(C.Brand);
            using var amber = Fill(C.Amber);
            switch (slot)
            {
                case "midday":
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
        private void Counters(SKCanvas c, float top, params (string Label, string Value, SKColor Color, string? Sub)[] items)
        {
            var w = (Inner - 2 * 16) / 3;
            for (var i = 0; i < items.Length; i++)
            {
                var x = Pad + i * (w + 16);
                CardBox(c, x, top, w, 108);
                TextFit(c, items[i].Label, x + 22, top + 18, w - 44, 20, faces.Medium, C.Ink2);
                using var font = Font(faces.Bold, 44);
                using var paint = Fill(items[i].Color);
                var value = Clean(items[i].Value);
                c.DrawText(value, x + 22, TopBaseline(font, top + 46), SKTextAlign.Left, font, paint);
                if (items[i].Sub is { } sub)
                {
                    var left = x + 22 + font.MeasureText(value) + 14;
                    TextFit(c, sub, left, top + 66, x + w - 18 - left, 16, faces.Medium, C.Ink3);
                }
            }
        }

        private void Bonus(SKCanvas c, float top, BattleWeekProgress p, string leftNote)
        {
            CardBox(c, Pad, top, Inner, 170);
            Trophy(c, Pad + 44, top + 36, 1f);
            Text(c, T("Бонус недели"), Pad + 84, top + 18, 26, faces.Bold, C.Ink);
            TextRight(c, $"{p.Taken} {T("из")} {p.Total} · {ReportFormat.Percent(p.Total == 0 ? null : p.Share)}", Width - Pad - 24, top + 20, 24, faces.Bold, C.Ink);

            var barX = Pad + 24;
            var barW = Inner - 48;
            var barY = top + 72;
            using var track = Fill(C.Track);
            c.DrawRoundRect(new SKRoundRect(new SKRect(barX, barY, barX + barW, barY + 14), 7), track);
            var share = p.Total == 0 ? 0f : (float)p.Share;
            if (share > 0)
            {
                using var fill = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(barX, 0), new SKPoint(barX + barW, 0), [SKColor.Parse("#FFD24A"), C.Amber], SKShaderTileMode.Clamp) };
                c.DrawRoundRect(new SKRoundRect(new SKRect(barX, barY, barX + barW * Math.Min(1, share), barY + 14), 7), fill);
            }

            using var tick = Stroke(C.Ink2, 2);
            foreach (var step in cards.BonusSteps)
            {
                var x = barX + barW * step.Percent / 100f;
                c.DrawLine(x, barY - 6, x, barY + 20, tick);
                TextCenter(c, $"{step.Percent}% · {BattleRules.GoalsFor(step, p.Total)}", x, barY + 26, 15, faces.SemiBold, C.Ink2);
                TextCenter(c, Money(step.Sum), x, barY + 44, 15, faces.Bold, C.Ink2);
            }

            Text(c, leftNote, barX, top + 138, 17, faces.Medium, C.Ink3);
            var right = p.NextStep is { } next ? F("до {0} сум — ещё {1} целей", Money(next.Sum), p.ToNextStep) : T("все ступени бонуса взяты");
            TextRight(c, right, Width - Pad - 24, top + 138, 17, faces.Bold, C.Ink);
        }

        private void Prizes(SKCanvas c, float top, bool mogulActive)
        {
            CardBox(c, Pad, top, Inner, 132);
            var w = (Inner - 2 * 16 - 2 * 12) / 3;
            for (var i = 0; i < cards.Prizes.Count && i < 3; i++)
            {
                var prize = cards.Prizes[i];
                var x = Pad + 16 + i * (w + 12);
                using var bg = Fill(SKColor.Parse("#FFF4E3"));
                c.DrawRoundRect(new SKRoundRect(new SKRect(x, top + 14, x + w, top + 86), 14), bg);
                PrizeIcon(c, x + 36, top + 50, prize.Level);
                TextFit(c, F("Твизос {0} тыс", prize.Level), x + 68, top + 18, w - 76, 22, faces.Bold, C.Ink);
                // Набор «300 тыс» по ТЗ — закуп 288 600 сум: приз — за накладную Твизос (до скидки) от суммы набора, показываем её.
                TextFit(c, prize.Contents, x + 68, top + 45, w - 76, 15, faces.Medium, C.Ink3);
                TextFit(c, $"{F("от {0} сум", Money(prize.Sum))} → {T(prize.Prize)}", x + 68, top + 64, w - 76, 15, faces.SemiBold, C.Ink2);
            }

            TextFit(c, mogulActive ? cards.MogulLine : T("Сетка Могул в Linko сегодня не действует — целей Могул нет"), Pad + 20, top + 98, Inner - 40, 17, faces.Medium, mogulActive ? C.Ink3 : C.Red);
        }

        /// <summary>Приз набора: кружка (300), термос (400), чайник (600) — простыми фигурами.</summary>
        private void PrizeIcon(SKCanvas c, float cx, float cy, string level)
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

        private void Trophy(SKCanvas c, float cx, float cy, float s)
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

        // ---------- Карта маршрута ----------

        private float RowHeightOf(IReadOnlyList<BattleStore> pair) =>
            Math.Max(RowHeight, pair.Count == 0 ? RowHeight : pair.Max(s => CalloutHeight(s) + 28));

        private float CalloutHeight(BattleStore store)
        {
            var evaluated = cards.Slot != "morning";
            return 16 + 26 + store.Goals.Count * (evaluated ? 64 : 46) + 8;
        }

        private void MapPanel(SKCanvas c, float top, float height, IReadOnlyList<BattleStore> stores, IReadOnlyList<float> rowHeights, BattleDay day, string slot, int offset)
        {
            using var bg = Fill(C.Map);
            using var edge = Stroke(C.MapBorder, 3);
            var rect = new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + height), 24);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);

            // Центры домиков: два в ряд, дорога змейкой.
            var leftX = Pad + 330f;
            var rightX = Width - Pad - 330f;
            var points = new List<(float X, float Y, BattleStore Store)>();
            var y = top + 48;
            for (var r = 0; r < rowHeights.Count; r++)
            {
                var cy = y + rowHeights[r] / 2;
                var pair = stores.Skip(r * 2).Take(2).ToList();
                var leftFirst = r % 2 == 0;
                for (var i = 0; i < pair.Count; i++)
                {
                    var x = (i == 0) == leftFirst ? leftX : rightX;
                    points.Add((x, cy, pair[i]));
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
                    var lastPassed = points.Select((p, i) => (p, i)).Where(x => x.p.Store.Status != BattleStoreStatus.Ahead).Select(x => x.i).DefaultIfEmpty(-1).Max();
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
            if (slot == "midday")
            {
                heroAt = points.FindIndex(p => p.Store.Status == BattleStoreStatus.Ahead);
            }

            foreach (var (p, i) in points.Select((p, i) => (p, i)))
            {
                var status = p.Store.Status;
                House(c, p.X, p.Y, 1f, status, slot == "morning" ? null : status, p.Store.Order);
                var left = p.X < Width / 2;
                var cw = 250f;
                var cx = left ? Pad + 20 : Width - Pad - 20 - cw;
                Callout(c, cx, p.Y - CalloutHeight(p.Store) / 2 + 10, cw, p.Store, slot);
            }

            if (points.Count > 0)
            {
                static (float, float) Beside((float X, float Y, BattleStore Store) p) => (p.X + (p.X > Width / 2 ? -96 : 96), p.Y + 6);
                var hero = slot switch
                {
                    "morning" => Beside(points[0]),
                    "evening" => Beside(points[^1]),
                    _ => Beside(heroAt >= 0 ? points[heroAt] : points[^1]),
                };
                Hero(c, hero.Item1, hero.Item2);
            }

            TextRight(c, offset > 0 ? F("магазины {0}–{1}", offset + 1, offset + stores.Count) : "", Width - Pad - 20, top + 14, 15, faces.Medium, C.Ink3);
        }

        private void House(SKCanvas c, float cx, float cy, float s, string status, string? badgeStatus, int number)
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
                TextCenter(c, T("МАГАЗИН"), cx, y + 15 * s, 11 * s, faces.Bold, SKColor.Parse("#FFE9A8"));
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

            // Значок: номер (впереди) или ✓ ½ ✕.
            var bx = x - 2 * s;
            var by = y + 2 * s;
            var r = 17 * s;
            var (badgeColor, badgeText, badgeInk) = badgeStatus switch
            {
                BattleStoreStatus.Taken => (C.Green, "✓", SKColors.White),
                BattleStoreStatus.Partial => (C.Amber, "½", SKColors.White),
                BattleStoreStatus.Refused => (C.Red, "✕", SKColors.White),
                _ => (SKColors.White, number.ToString(CultureInfo.InvariantCulture), C.Ink),
            };
            using var badge = Fill(badgeColor);
            using var badgeEdge = Stroke(C.Ink, 2 * s);
            c.DrawCircle(bx, by, r, badge);
            c.DrawCircle(bx, by, r, badgeEdge);
            TextCenter(c, badgeText, bx, by - 11 * s, 17 * s, faces.Bold, badgeInk);
        }

        private void Callout(SKCanvas c, float x, float y, float w, BattleStore store, string slot)
        {
            var h = CalloutHeight(store);
            var evaluated = slot != "morning";
            using var bg = Fill(store.Status == BattleStoreStatus.Refused && evaluated ? SKColor.Parse("#FDECEC") : C.Card);
            using var edge = Stroke(C.Border, 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 14);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            TextFit(c, $"{store.Order}. {store.Name}", x + 12, y + 12, w - 24, 19, faces.Bold, C.Ink);
            var gy = y + 42;
            foreach (var g in store.Goals)
            {
                var color = g.Kind == BattleGoalKind.Twizos ? C.Orange : C.Pink;
                if (Pack(g, store.MarketId) is { } pack)
                {
                    DrawPack(c, pack, x + 12, gy, 34, 40);
                }

                var tx = x + 54;
                var label = g.Kind == BattleGoalKind.Twizos
                    ? g.Sub == BattleGoalSub.Connect ? T("Твизос · подключить") : F("Твизос · набор {0}", g.Level)
                    : g.Sub == BattleGoalSub.Connect ? F("Могул {0} · подкл.", g.Level) : F("Могул {0} · объём", g.Level);
                TextFit(c, label, tx, gy, w - 66, 16, faces.SemiBold, color);
                var sum = g.Kind == BattleGoalKind.Twizos
                    ? cards.Prizes.FirstOrDefault(p => p.Level == g.Level) is { } prize
                        ? $"{F("от {0} сум", Money(g.TargetSum))} → {T(prize.Prize)}"
                        : F("от {0} сум", Money(g.TargetSum))
                    : $"{Money(g.TargetSum)} → {g.Percent}%";
                TextFit(c, sum, tx, gy + 20, w - 66, 16, faces.Bold, C.Ink);
                if (evaluated)
                {
                    var (text, tone) = g.Status switch
                    {
                        BattleGoalStatus.Taken => ($"✓ {Money(g.TakenSum ?? 0)}" + (g.TakenPercent is { } tp ? $" · {tp}%" : ""), C.Green),
                        BattleGoalStatus.Refused => ($"✕ {T("отказ")}", C.Red),
                        _ => ($"· {T("впереди")}", C.Ink3),
                    };
                    TextFit(c, text, tx, gy + 40, w - 66, 15, faces.SemiBold, tone);
                }

                gy += evaluated ? 64 : 46;
            }
        }

        private static void DrawPack(SKCanvas c, SKBitmap pack, float x, float y, float w, float h)
        {
            var scale = Math.Min(w / pack.Width, h / pack.Height);
            var dw = pack.Width * scale;
            var dh = pack.Height * scale;
            using var paint = new SKPaint { IsAntialias = true };
            c.DrawBitmap(pack, new SKRect(x + (w - dw) / 2, y + (h - dh) / 2, x + (w - dw) / 2 + dw, y + (h - dh) / 2 + dh), paint);
        }

        private void Tree(SKCanvas c, float cx, float cy, float s)
        {
            using var trunk = Fill(C.Trunk);
            using var crown = Fill(C.Tree);
            using var dark = Fill(C.TreeDark);
            c.DrawRect(cx - 4 * s, cy, 8 * s, 18 * s, trunk);
            c.DrawCircle(cx, cy - 8 * s, 18 * s, dark);
            c.DrawCircle(cx - 4 * s, cy - 12 * s, 14 * s, crown);
        }

        /// <summary>Герой-ТП: кепка, лицо, красная куртка, сумка.</summary>
        private void Hero(SKCanvas c, float cx, float cy)
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

        private void Footer(SKCanvas c, float top, string text)
        {
            using var bg = Fill(C.Yellow);
            c.DrawRoundRect(new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + 92), 20), bg);
            var lines = Wrap(text, faces.Bold, 22, Inner - 48, 2);
            var y = top + (92 - lines.Count * 28) / 2;
            foreach (var line in lines)
            {
                TextCenter(c, line, Width / 2, y, 22, faces.Bold, C.Ink);
                y += 28;
            }
        }

        private void CardBox(SKCanvas c, float x, float y, float w, float h)
        {
            using var bg = Fill(C.Card);
            using var edge = Stroke(C.Border, 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 22);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
        }

        private static byte[] Png(SKSurface surface)
        {
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }

        // ---------- Текст ----------

        private static SKFont Font(SKTypeface face, float size) => new(face, size) { Edging = SKFontEdging.Antialias, Subpixel = true };
        private static SKPaint Fill(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        private static SKPaint Stroke(SKColor color, float width) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width };
        private static float TopBaseline(SKFont font, float top) => top - font.Metrics.Ascent * 0.92f;

        private float Measure(string text, SKTypeface face, float size)
        {
            using var font = Font(face, size);
            return font.MeasureText(Clean(text));
        }

        private void Text(SKCanvas c, string text, float x, float top, float size, SKTypeface face, SKColor color)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            c.DrawText(Clean(text), x, TopBaseline(font, top), SKTextAlign.Left, font, paint);
        }

        private void TextRight(SKCanvas c, string text, float right, float top, float size, SKTypeface face, SKColor color)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            c.DrawText(Clean(text), right, TopBaseline(font, top), SKTextAlign.Right, font, paint);
        }

        private void TextCenter(SKCanvas c, string text, float cx, float top, float size, SKTypeface face, SKColor color)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            c.DrawText(Clean(text), cx, TopBaseline(font, top), SKTextAlign.Center, font, paint);
        }

        private void TextFit(SKCanvas c, string text, float x, float top, float width, float size, SKTypeface face, SKColor color)
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

        private static string Ellipsis(string text, SKFont font, float width)
        {
            var t = text.TrimEnd('…', ' ');
            while (t.Length > 1 && font.MeasureText(t + "…") > width)
            {
                t = t[..^1];
            }

            return t.TrimEnd() + "…";
        }

        private List<string> Wrap(string text, SKTypeface face, float size, float width, int maxLines)
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

        private void Spaced(SKCanvas c, string text, float x, float top, float size, SKTypeface face, SKColor color, float spacing)
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
        private string Clean(string text)
        {
            if (text.Length == 0)
            {
                return text;
            }

            var face = faces.Regular;
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
}

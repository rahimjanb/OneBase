using System.Globalization;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Battle;
using SkiaSharp;
using C = OneBase.Infrastructure.Telegram.Reports.FarmColors;

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

    /// <summary>Пачка для цели: Твизос — вкус по коду товара (Kabob по умолчанию), Могул — Yamelly 420 г по магазину.</summary>
    private static SKBitmap? Pack(BattleGoalView goal, long marketId)
    {
        var images = FarmAssets.Images;
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

    private sealed class Painter(ReportDocument doc, BattleCards cards, SkiaReportRenderer.Typefaces faces) : FarmPainter(faces, cards.Language, doc.Test)
    {
        // ---------- Карточка дня ----------

        public byte[] Day(BattleDay day, int chunk, int chunks, int perPage)
        {
            var stores = day.Stores.Skip(chunk * perPage).Take(perPage).ToList();
            var rows = Math.Max(1, (stores.Count + 1) / 2);
            var first = chunk == 0;
            var strip = IsTest ? 52f : 0f;
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
            if (IsTest)
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
            var strip = IsTest ? 52f : 0f;
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
            if (IsTest)
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
            Text(c, T("6 дней — 6 полян"), Pad + 24, panelTop + 18, 26, Faces.Bold, C.Ink);
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
            Text(c, Weekday(day.Date), x + 16, y + 12, 26, Faces.Bold, C.Ink);
            var share = day.Total == 0 ? (decimal?)null : day.Share;
            TextRight(c, day.Total == 0 ? "—" : ReportFormat.Percent(share), x + w - 16, y + 12, 26, Faces.Bold, share is null ? C.Ink3 : share >= 0.5m ? C.Green : share >= 0.3m ? C.Amber : C.Red);
            var goals = day.Stores.SelectMany(s => s.Goals).ToList();
            Text(c, F("Твизос {0}/{1} · Могул {2}/{3}", goals.Count(g => g.Kind == BattleGoalKind.Twizos && g.IsTaken), goals.Count(g => g.Kind == BattleGoalKind.Twizos),
                goals.Count(g => g.Kind == BattleGoalKind.Mogul && g.IsTaken), goals.Count(g => g.Kind == BattleGoalKind.Mogul)), x + 16, y + 44, 15, Faces.Medium, C.Ink2);
            if (day.Stores.Count == 0)
            {
                Text(c, T("целей нет"), x + 16, y + 80, 16, Faces.Medium, C.Ink3);
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

        private void Bonus(SKCanvas c, float top, BattleWeekProgress p, string leftNote)
        {
            CardBox(c, Pad, top, Inner, 170);
            Trophy(c, Pad + 44, top + 36, 1f);
            Text(c, T("Бонус недели"), Pad + 84, top + 18, 26, Faces.Bold, C.Ink);
            TextRight(c, $"{p.Taken} {T("из")} {p.Total} · {ReportFormat.Percent(p.Total == 0 ? null : p.Share)}", Width - Pad - 24, top + 20, 24, Faces.Bold, C.Ink);

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
                TextCenter(c, $"{step.Percent}% · {BattleRules.GoalsFor(step, p.Total)}", x, barY + 26, 15, Faces.SemiBold, C.Ink2);
                TextCenter(c, Money(step.Sum), x, barY + 44, 15, Faces.Bold, C.Ink2);
            }

            Text(c, leftNote, barX, top + 138, 17, Faces.Medium, C.Ink3);
            var right = p.NextStep is { } next ? F("до {0} сум — ещё {1} целей", Money(next.Sum), p.ToNextStep) : T("все ступени бонуса взяты");
            TextRight(c, right, Width - Pad - 24, top + 138, 17, Faces.Bold, C.Ink);
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
                TextFit(c, F("Твизос {0} тыс", prize.Level), x + 68, top + 18, w - 76, 22, Faces.Bold, C.Ink);
                // Набор «300 тыс» по ТЗ — закуп 288 600 сум: приз — за накладную Твизос (до скидки) от суммы набора, показываем её.
                TextFit(c, prize.Contents, x + 68, top + 45, w - 76, 15, Faces.Medium, C.Ink3);
                TextFit(c, $"{F("от {0} сум", Money(prize.Sum))} → {T(prize.Prize)}", x + 68, top + 64, w - 76, 15, Faces.SemiBold, C.Ink2);
            }

            TextFit(c, mogulActive ? cards.MogulLine : T("Сетка Могул в Linko сегодня не действует — целей Могул нет"), Pad + 20, top + 98, Inner - 40, 17, Faces.Medium, mogulActive ? C.Ink3 : C.Red);
        }

        // ---------- Карта маршрута ----------

        private float RowHeightOf(IReadOnlyList<BattleStore> pair) =>
            Math.Max(RowHeight, pair.Count == 0 ? RowHeight : pair.Max(s => CalloutHeight(s) + 28));

        private float CalloutHeight(BattleStore store)
        {
            var evaluated = cards.Slot != "morning";
            return 16 + 26 + store.Goals.Count * (evaluated ? 64 : 46) + 8;
        }

        private void MapPanel(SKCanvas c, float top, float height, IReadOnlyList<BattleStore> stores, IReadOnlyList<float> rowHeights, BattleDay day, string slot, int offset) =>
            Farm(c, top, height, stores.Select(s => new FarmStop(s.Status, s.Order)).ToList(), rowHeights, slot, offset, (i, x, y) =>
            {
                var left = x < Width / 2;
                var cw = 250f;
                var cx = left ? Pad + 20 : Width - Pad - 20 - cw;
                Callout(c, cx, y - CalloutHeight(stores[i]) / 2 + 10, cw, stores[i], slot);
            });

        private void Callout(SKCanvas c, float x, float y, float w, BattleStore store, string slot)
        {
            var h = CalloutHeight(store);
            var evaluated = slot != "morning";
            using var bg = Fill(store.Status == BattleStoreStatus.Refused && evaluated ? SKColor.Parse("#FDECEC") : C.Card);
            using var edge = Stroke(C.Border, 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 14);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            TextFit(c, $"{store.Order}. {store.Name}", x + 12, y + 12, w - 24, 19, Faces.Bold, C.Ink);
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
                TextFit(c, label, tx, gy, w - 66, 16, Faces.SemiBold, color);
                var sum = g.Kind == BattleGoalKind.Twizos
                    ? cards.Prizes.FirstOrDefault(p => p.Level == g.Level) is { } prize
                        ? $"{F("от {0} сум", Money(g.TargetSum))} → {T(prize.Prize)}"
                        : F("от {0} сум", Money(g.TargetSum))
                    : $"{Money(g.TargetSum)} → {g.Percent}%";
                TextFit(c, sum, tx, gy + 20, w - 66, 16, Faces.Bold, C.Ink);
                if (evaluated)
                {
                    var (text, tone) = g.Status switch
                    {
                        BattleGoalStatus.Taken => ($"✓ {Money(g.TakenSum ?? 0)}" + (g.TakenPercent is { } tp ? $" · {tp}%" : ""), C.Green),
                        BattleGoalStatus.Refused => ($"× {T("отказ")}", C.Red), // «✕» в Inter нет — «×»
                        _ => ($"· {T("впереди")}", C.Ink3),
                    };
                    TextFit(c, text, tx, gy + 40, w - 66, 15, Faces.SemiBold, tone);
                }

                gy += evaluated ? 64 : 46;
            }
        }
    }
}

using System.Globalization;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Incentive;
using SkiaSharp;
using C = OneBase.Infrastructure.Telegram.Reports.FarmColors;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Карточки накопительной акции (ТЗ «Проект 3», образцы «Карточки/» новой серии): PNG шириной 1080 px в стиле «Поля боя». 08:30 «Маршрут дня» —
/// накоплено, до призов, сегодня / цель маршрута, дорога к призам, поле с магазинами и целями в кг; 13:00 «Полдень» — полоса «кг сегодня» с
/// меткой полудня и статусами; 18:00 «Итог дня» — статусы, итог, дорога к призам; суббота — «6 полян» и рейтинг; конец месяца — «город героя».
/// Сводка команды (СВ — ТП списком, РМ — регионы и лучшие ТП) — первой картинкой. Призы — вырезки из флаера акции, пачки — PNG из сборки.
/// </summary>
internal static class IncentiveCardRenderer
{
    /// <summary>Магазинов на картинке дня у ТП; копии у СВ — вся карточка ТП одной картинкой.</summary>
    private const int StoresPerPage = 12;
    private const int StoresPerCopy = 20;
    private const int TeamRowsPerPage = 20;
    private const int MaxPages = 20;
    /// <summary>Ряд поля: выноска акции ниже, чем у «Поля боя» (одна цель на магазин).</summary>
    private const float RowHeight = 172;

    public static IReadOnlyList<byte[]> Render(IncentiveCards cards, SkiaReportRenderer.Typefaces faces, bool test)
    {
        var painter = new Painter(cards, faces, test);
        var pages = new List<byte[]>();
        if (cards.Team is { } team)
        {
            pages.AddRange(painter.Team(team));
        }

        if (cards.Rating is { } rating)
        {
            pages.Add(painter.Rating(rating));
        }

        var copies = cards.Agents.Count > 1 || cards.Team is not null;
        foreach (var card in cards.Agents)
        {
            switch (cards.Slot)
            {
                case IncentiveSlot.Week when card.Week is not null:
                    pages.Add(painter.Week(card));
                    break;
                case IncentiveSlot.Month when card.Month is not null:
                    pages.Add(painter.Month(card));
                    break;
                case var slot when IncentiveSlot.IsDay(slot):
                {
                    var perPage = copies ? StoresPerCopy : StoresPerPage;
                    var count = card.Route?.Stores.Count ?? 0;
                    var chunks = Math.Max(1, (int)Math.Ceiling(count / (double)perPage));
                    for (var i = 0; i < chunks; i++)
                    {
                        pages.Add(painter.Day(card, i, chunks, perPage));
                    }

                    break;
                }
            }
        }

        return pages.Take(MaxPages).ToList();
    }

    private sealed class Painter(IncentiveCards cards, SkiaReportRenderer.Typefaces faces, bool test) : FarmPainter(faces, cards.Language, test)
    {
        private const float HeaderHeight = 170;
        private const float StatsHeight = 124;
        private const float RoadHeight = 320;
        private const float Gap = 20;

        private IncentiveSettings P => cards.Program;

        private static readonly string[] Packs = ["Twizos_Kabob", "Twizos_Smetana_va_piyoz", "Twizos_Smetana_va_kokatlar", "Twizos_Dengiz_tuzi", "Twizos_Pishloq", "Twizos_Kolbaska_chili"];

        private string Kg(decimal v) => $"{IncentiveTexts.Kg(v)} {T("кг")}";
        private string Kg1(decimal v) => $"{IncentiveTexts.Kg1(v)} {T("кг")}";
        private string To(IncentiveTier tier) => IncentiveTexts.To(tier, Language);
        private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.CurrentCulture) + s[1..];
        private string StartText => $"{P.Start.Day} {IncentiveTexts.MonthOf(P.Start.Month, Language)}";
        private string EndText => $"{P.End.Day} {IncentiveTexts.MonthOf(P.End.Month, Language)}";
        private float StaleHeight => cards.Stale ? 60 : 0;

        // ---------- Карточка дня ----------

        public byte[] Day(IncentiveAgentCard card, int chunk, int chunks, int perPage)
        {
            var slot = cards.Slot;
            var all = card.Route?.Stores ?? [];
            var stores = all.Skip(chunk * perPage).Take(perPage).ToList();
            var first = chunk == 0;
            var last = chunk == chunks - 1;
            var rows = Math.Max(1, (stores.Count + 1) / 2);
            var mapHeight = 48 + rows * RowHeight + 40;
            var strip = IsTest ? 52f : 0f;

            var y = strip + HeaderHeight + StaleHeight;
            var statsTop = y;
            if (first)
            {
                y += StatsHeight + Gap;
            }

            var roadTop = slot == IncentiveSlot.Morning && first ? y : -1f;
            if (roadTop >= 0)
            {
                y += RoadHeight + Gap;
            }

            var todayTop = slot == IncentiveSlot.Midday && first ? y : -1f;
            if (todayTop >= 0)
            {
                y += 136 + Gap;
            }

            var mapTop = y;
            y += mapHeight + Gap;
            var roadAfter = slot == IncentiveSlot.Evening && last ? y : -1f;
            if (roadAfter >= 0)
            {
                y += RoadHeight + Gap;
            }

            var footerTop = y;
            using var surface = Surface(footerTop + 92 + Pad, out var c);
            var title = slot switch { IncentiveSlot.Midday => T("Полдень"), IncentiveSlot.Evening => T("Итог дня"), _ => T("Маршрут дня") };
            Header(c, strip, $"{title} · {P.Product}", $"{card.Agent.Name} · {card.Agent.Region ?? ""} · {Weekday(cards.Date)} {DayMonth(cards.Date)}", slot, chunk, chunks);
            StaleBanner(c, strip + HeaderHeight);
            if (first)
            {
                AgentStats(c, statsTop, card);
            }

            if (roadTop >= 0)
            {
                PrizeRoad(c, roadTop, card.Total);
            }

            if (todayTop >= 0)
            {
                TodayBar(c, todayTop, card);
            }

            var evaluated = slot != IncentiveSlot.Morning;
            Farm(c, mapTop, mapHeight, stores.Select(s => new FarmStop(s.Status, s.Order)).ToList(), Enumerable.Repeat(RowHeight, rows).ToList(), slot, chunk * perPage,
                (i, x, cy) =>
                {
                    // Уже, чем у «Поля боя»: значок статуса на домике слева не закрывается.
                    const float w = 232f;
                    var cx = x < Width / 2 ? Pad + 20 : Width - Pad - 20 - w;
                    Callout(c, cx, cy - CalloutHeight(evaluated) / 2 + 10, w, stores[i], evaluated);
                });
            if (evaluated && card.Route is { Stores.Count: > 0 } route)
            {
                TextFit(c, F("✓ взято {0} · ½ частично {1} · × отказ {2} · подключено {3} · дозаказ {4}", route.Taken, route.Partial, route.Refused, route.Connected,
                    route.Reordered), Pad + 20, mapTop + 14, Inner - 240, 16, Faces.SemiBold, C.Ink2);
            }

            if (stores.Count == 0)
            {
                TextCenter(c, T("Маршрута на этот день нет: в Linko нет плана визитов и точек ТП"), Width / 2, mapTop + 90, 20, Faces.SemiBold, C.Ink2);
            }

            if (roadAfter >= 0)
            {
                PrizeRoad(c, roadAfter, card.Total);
            }

            Footer(c, footerTop, last ? DayFooter(card) : F("Продолжение маршрута — на следующей картинке ({0} из {1})", chunk + 2, chunks));
            return Png(surface);
        }

        private string DayFooter(IncentiveAgentCard card)
        {
            var route = card.Route;
            var norm = card.DayStart.Norm;
            var next = card.Progress.Next;
            switch (cards.Slot)
            {
                case IncentiveSlot.Morning:
                {
                    // Призы клиенту — одним куском (неразрывные пробелы): перенос строки — перед списком, а не внутри.
                    var prizes = string.Join("\u00A0/\u00A0", P.StoreSets.OrderBy(s => s.Kg).Select(s => IncentiveTexts.StoreSet(P, s.Contents, s.Prize, Language).Prize).OfType<string>()
                        .Distinct().Select(x => x.Replace(' ', '\u00A0')));
                    var head = F("{0} магазинов · цель маршрута {1}", route?.Stores.Count ?? 0, Kg(route?.TargetKg ?? 0));
                    var normText = norm is { } n ? " · " + F("норма дня {0}", Kg(n)) : "";
                    return prizes.Length > 0 ? $"{head}{normText} · {F("приз-набор клиенту: {0}", prizes)}" : head + normText;
                }

                case IncentiveSlot.Midday:
                {
                    var ahead = route?.Stores.Count(s => s.Status == BattleStoreStatus.Ahead) ?? 0;
                    return norm is { } n
                        ? F("До 18:00 ещё {0} магазинов · до нормы дня ещё {1}", ahead, Kg(Math.Max(0, n - card.Today)))
                        : F("До 18:00 ещё {0} магазинов", ahead);
                }

                default:
                {
                    if (next is null)
                    {
                        return F("Сегодня +{0} · накоплено {1} · все призы акции взяты", Kg(card.Today), Kg(card.Total));
                    }

                    // Норма вечером — уже на завтра; выполнена ли сегодня — по норме на начало дня.
                    var dayNorm = card.DayStart.Norm;
                    var met = dayNorm is { } mn && card.Today >= mn ? T("норма ✓") : dayNorm is { } m2 ? F("норма {0}", Kg(m2)) : T("норма —");
                    return F("Сегодня +{0} ({1}) · накоплено {2} · {3} {4}", Kg(card.Today), met, Kg(card.Total), To(next), Kg(card.Progress.ToNext ?? 0));
                }
            }
        }

        /// <summary>Три счётчика ТП: накоплено с начала акции (по месяцам), до ближайшего приза (и до остальных), сегодня / цель маршрута и норма.</summary>
        private void AgentStats(SKCanvas c, float top, IncentiveAgentCard card)
        {
            var p = card.Progress;
            var accumulated = (F("Накоплено с {0}", StartText), Kg(card.Total), C.Orange, IncentiveTexts.MonthsLine(P, card.Standing, cards.Date, Language));
            var others = p.Tiers.Where(t => !t.Reached && t.Tier != p.Next).Select(t => $"{To(t.Tier)} {IncentiveTexts.Kg(t.Left)}");
            var toNext = p.Next is { } next
                ? (Capital(To(next)), Kg(p.ToNext ?? 0), C.Pink, string.Join(" · ", others))
                : (T("Все призы взяты"), IncentiveTexts.Short(p.Reached!, Language), C.Green, IncentiveTexts.Prize(p.Reached!, Language));
            var target = card.Route?.TargetKg;
            var today = target is > 0 ? $"{IncentiveTexts.Kg(card.Today)} / {Kg(target.Value)}" : Kg(card.Today);
            var day = card.DayStart;
            var normLine = day.Norm is { } n ? F("норма дня {0} · {1} раб. дней до {2}", Kg(n), day.WorkDaysLeft, DayMonth(P.End)) : F("{0} раб. дней до {1}", day.WorkDaysLeft, DayMonth(P.End));
            Stats(c, top, accumulated, toNext, (T("Сегодня"), today, C.Green, normLine));
        }

        private void TodayBar(SKCanvas c, float top, IncentiveAgentCard card)
        {
            CardBox(c, Pad, top, Inner, 136);
            var target = card.Route?.TargetKg ?? 0;
            Text(c, F("Кг {0} сегодня", P.Product), Pad + 24, top + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, target > 0 ? F("{0} из {1}", IncentiveTexts.Kg(card.Today), Kg(target)) : Kg(card.Today), Width - Pad - 24, top + 18, 24, Faces.Bold, C.Ink);
            var x = Pad + 24;
            var w = Inner - 48;
            var y = top + 66;
            using var track = Fill(C.Track);
            c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + w, y + 22), 11), track);
            var share = target > 0 ? (float)Math.Min(1, card.Today / target) : 0;
            if (share > 0)
            {
                using var fill = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(x, 0), new SKPoint(x + w, 0), [SKColor.Parse("#FFD24A"), C.Amber], SKShaderTileMode.Clamp) };
                c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + Math.Max(22, w * share), y + 22), 11), fill);
            }

            using var mark = Stroke(C.Ink, 4);
            var mx = x + w * (float)cards.MiddayShare;
            c.DrawLine(mx, y - 7, mx, y + 29, mark);
            Text(c, T("чёрная метка — где нужно быть к полудню"), x, top + 102, 16, Faces.Medium, C.Ink3);
        }

        private static float CalloutHeight(bool evaluated) => 16 + 26 + (evaluated ? 84 : 64) + 8;

        private void Callout(SKCanvas c, float x, float y, float w, IncentiveStore store, bool evaluated)
        {
            var h = CalloutHeight(evaluated);
            using var bg = Fill(store.Status == BattleStoreStatus.Refused && evaluated ? SKColor.Parse("#FDECEC") : C.Card);
            using var edge = Stroke(C.Border, 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 14);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            TextFit(c, $"{store.Order}. {store.Name}", x + 12, y + 12, w - 24, 19, Faces.Bold, C.Ink);
            var gy = y + 42;
            if (P.Product.Contains("Twizos", StringComparison.OrdinalIgnoreCase) && FarmAssets.Images.GetValueOrDefault(Packs[(int)(Math.Abs(store.MarketId) % Packs.Length)]) is { } pack)
            {
                DrawPack(c, pack, x + 12, gy + 6, 34, 46);
            }

            var tx = x + 54;
            var (contents, _, level) = IncentiveTexts.StoreSet(P, store.Set, store.Prize, Language);
            var label = store.Sub == IncentiveTargetSub.Connect ? F("{0} · подключить", P.Product) : F("{0} · дозаказ", P.Product);
            TextFit(c, label, tx, gy, w - 66, 16, Faces.SemiBold, C.Orange);
            var kg = Kg1(store.TargetKg);
            Text(c, kg, tx, gy + 20, 18, Faces.Bold, C.Ink);
            if (level is not null)
            {
                c.Save();
                c.Translate(tx + Measure(kg, Faces.Bold, 18) + 16, gy + 31);
                c.Scale(0.55f);
                PrizeIcon(c, 0, 0, level);
                c.Restore();
            }

            TextFit(c, contents, tx, gy + 44, w - 66, 14, Faces.Medium, C.Ink3);
            if (evaluated)
            {
                var (text, tone) = store.Status switch
                {
                    BattleStoreStatus.Taken => ($"✓ {Kg1(store.Kg)}", C.Green),
                    BattleStoreStatus.Partial => ($"½ {Kg1(store.Kg)}", C.Amber),
                    BattleStoreStatus.Refused => ($"× {T("отказ")}", C.Red),
                    _ => ($"· {T("впереди")}", C.Ink3),
                };
                TextFit(c, text, tx, gy + 64, w - 66, 15, Faces.SemiBold, tone);
            }
        }

        // ---------- Неделя: 6 полян ----------

        public byte[] Week(IncentiveAgentCard card)
        {
            var week = card.Week!;
            var strip = IsTest ? 52f : 0f;
            var statsTop = strip + HeaderHeight + StaleHeight;
            var panelTop = statsTop + StatsHeight + Gap;
            // Высота ряда полян — по самой населённой поляне ряда (пн–ср, чт–сб).
            float TileHeight(int row)
            {
                var stores = Math.Max(1, week.Days.Skip(row * 3).Take(3).Select(d => d.Route.Stores.Count).DefaultIfEmpty(1).Max());
                return 76 + (int)Math.Ceiling(Math.Min(stores, 20) / 4.0) * 58 + 16;
            }

            var tileHeights = new[] { TileHeight(0), TileHeight(1) };
            var panelHeight = 60 + tileHeights[0] + tileHeights[1] + 3 * 16;
            var top = week.TopMarkets ?? [];
            var topTop = panelTop + panelHeight + Gap;
            var topHeight = top.Count > 0 ? 60 + 34 + top.Count * 42 + 16 : 0;
            var roadTop = topTop + (topHeight > 0 ? topHeight + Gap : 0);
            var footerTop = roadTop + RoadHeight + Gap;
            using var surface = Surface(footerTop + 92 + Pad, out var c);
            var weekNo = (week.Monday.Day - 1) / 7 + 1;
            Header(c, strip, F("Итог недели · {0}", P.Product), $"{card.Agent.Name} · {card.Agent.Region ?? ""} · {DayMonth(week.Monday)}–{DayMonth(week.Saturday)}", IncentiveSlot.Week, 0, 1,
                F("Нед {0}", weekNo), IncentiveTexts.MonthOf(week.Monday.Month, Language));
            StaleBanner(c, strip + HeaderHeight);

            var rankSub = card.RankBefore is { } before && card.Rank is { } now
                ? before > now ? F("▲ {0} к прошлой неделе", before - now) : before < now ? F("▼ {0} к прошлой неделе", now - before) : T("как на прошлой неделе")
                : F("из {0} ТП", card.Ranked);
            Stats(c, statsTop,
                (T("За неделю"), Kg(week.Kg), week.Norm is { } wn && week.Kg >= wn ? C.Green : C.Orange, week.Norm is { } n ? F("норма недели {0}", Kg(n)) : ""),
                (T("Накоплено"), Kg(card.Total), C.Green, F("с {0}", StartText)),
                (card.Wholesale ? T("Место среди оптовых") : T("Место в акции"), card.Rank?.ToString(CultureInfo.InvariantCulture) ?? "—", C.Pink, rankSub));

            CardBox(c, Pad, panelTop, Inner, panelHeight);
            Text(c, T("6 дней — 6 полян"), Pad + 24, panelTop + 18, 26, Faces.Bold, C.Ink);
            var tileWidth = (Inner - 2 * 16 - 2 * 16) / 3;
            for (var i = 0; i < 6 && i < week.Days.Count; i++)
            {
                var x = Pad + 16 + (i % 3) * (tileWidth + 16);
                var y = panelTop + 60 + (i / 3) * (tileHeights[0] + 16);
                WeekTile(c, x, y, tileWidth, tileHeights[i / 3], week.Days[i], week.DayNorm);
            }

            if (top.Count > 0)
            {
                TopMarkets(c, topTop, topHeight, top);
            }

            PrizeRoad(c, roadTop, card.Total);
            var next = card.Progress.Next;
            Footer(c, footerTop, next is null
                ? F("Все призы акции взяты · накоплено {0}", Kg(card.Total))
                : card.Progress.Norm is { } norm
                    ? F("{0} {1} · держи {2}+ кг в день — и {3} твой к {4}", Capital(To(next)), Kg(card.Progress.ToNext ?? 0), IncentiveTexts.Kg(norm), IncentiveTexts.Short(next, Language), EndText)
                    : F("{0} {1}", Capital(To(next)), Kg(card.Progress.ToNext ?? 0)));
            return Png(surface);
        }

        /// <summary>«Дозакажи Twizos»: лучшие клиенты ТП за 60 дней и цель каждого на следующую неделю (ТЗ, раздел 6).</summary>
        private void TopMarkets(SKCanvas c, float top, float height, IReadOnlyList<IncentiveTopMarket> rows)
        {
            CardBox(c, Pad, top, Inner, height);
            Text(c, F("Дозакажи {0} — лучшие клиенты за 60 дней", P.Product), Pad + 24, top + 18, 24, Faces.Bold, C.Ink);
            var header = top + 62;
            Text(c, T("Магазин"), Pad + 24, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("За 60 дней"), Pad + 760, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("Цель на неделю"), Width - Pad - 24, header, 15, Faces.SemiBold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            for (var i = 0; i < rows.Count; i++)
            {
                var y = header + 32 + i * 42;
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                TextFit(c, $"{i + 1}. {rows[i].Name}", Pad + 24, y + 11, 600, 18, Faces.Medium, C.Ink);
                TextRight(c, Kg(rows[i].Kg), Pad + 760, y + 11, 18, Faces.Medium, C.Ink);
                TextRight(c, $"+{Kg(rows[i].WeekTarget)}", Width - Pad - 24, y + 11, 19, Faces.Bold, C.Green);
            }
        }

        private void WeekTile(SKCanvas c, float x, float y, float w, float h, IncentiveWeekDay day, decimal? norm)
        {
            using var bg = Fill(C.Map);
            using var edge = Stroke(C.MapBorder, 2);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 18);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            Text(c, Weekday(day.Date), x + 16, y + 12, 26, Faces.Bold, C.Ink);
            var met = norm is { } n && day.Kg >= n;
            TextRight(c, day.Kg > 0 ? Kg(day.Kg) : "—", x + w - 16, y + 12, 26, Faces.Bold, day.Kg <= 0 ? C.Ink3 : met ? C.Green : C.Amber);
            var normText = met ? T("норма ✓") : norm is { } nn ? F("норма {0}", Kg(nn)) : "";
            var stores = day.Route.Stores;
            TextFit(c, string.Join(" · ", new[] { normText, F("магазинов {0}", stores.Count) }.Where(t => t.Length > 0)), x + 16, y + 44, w - 32, 15, Faces.Medium, C.Ink2);
            if (stores.Count == 0)
            {
                Text(c, T("маршрута не было"), x + 16, y + 80, 16, Faces.Medium, C.Ink3);
                return;
            }

            var cell = (w - 32) / 4;
            foreach (var (store, i) in stores.Take(20).Select((s, i) => (s, i)))
            {
                var cx = x + 16 + (i % 4) * cell + cell / 2;
                var cy = y + 76 + (i / 4) * 58 + 26;
                House(c, cx, cy, 0.42f, store.Status, store.Status == BattleStoreStatus.Ahead ? null : store.Status, store.Order);
            }
        }

        // ---------- Месяц: город героя ----------

        public byte[] Month(IncentiveAgentCard card)
        {
            var month = card.Month!;
            var p = card.Progress;
            var strip = IsTest ? 52f : 0f;
            var panelTop = strip + HeaderHeight + StaleHeight;
            var weeks = month.Weeks;
            var count = Math.Max(1, weeks.Count);
            var tileWidth = (Inner - 32 - (count - 1) * 16) / count;
            var scale = Math.Min(0.46f, (tileWidth - 24) / 3 / 130f);
            var houseStep = 90 * scale + 22;
            var tileHeight = 56 + 4 * houseStep + 78;
            var panelHeight = 64 + tileHeight + 24;
            var roadTop = panelTop + panelHeight + Gap;
            var statsTop = roadTop + RoadHeight + Gap;
            var footerTop = statsTop + StatsHeight + Gap;
            using var surface = Surface(footerTop + 92 + Pad, out var c);

            var badge = p.Next is { } next ? ReportFormat.Percent(card.Total / next.Kg) : "✓";
            var hint = p.Next is { } n2 ? To(n2) : IncentiveTexts.Short(p.Reached!, Language);
            Header(c, strip, Capital(F("Итог {0} · {1}", IncentiveTexts.MonthOf(month.Month, Language), P.Product)), $"{card.Agent.Name} · {card.Agent.Region ?? ""}", IncentiveSlot.Month, 0, 1, badge, hint);
            StaleBanner(c, strip + HeaderHeight);

            CardBox(c, Pad, panelTop, Inner, panelHeight);
            Text(c, T("Город героя — 12 домов = норма недели"), Pad + 24, panelTop + 18, 24, Faces.Bold, C.Ink);
            using (var field = Fill(C.Map))
            {
                c.DrawRoundRect(new SKRoundRect(new SKRect(Pad + 12, panelTop + 60, Width - Pad - 12, panelTop + panelHeight - 12), 18), field);
            }

            for (var i = 0; i < weeks.Count; i++)
            {
                var week = weeks[i];
                var x = Pad + 16 + i * (tileWidth + 16);
                var y = panelTop + 64;
                using var tile = Fill(SKColor.Parse("#E6F5D3"));
                using var tileEdge = Stroke(C.MapBorder, 2);
                var r = new SKRoundRect(new SKRect(x + 4, y + 4, x + tileWidth - 4, y + tileHeight - 4), 16);
                c.DrawRoundRect(r, tile);
                c.DrawRoundRect(r, tileEdge);
                TextCenter(c, F("Неделя {0}", week.Number), x + tileWidth / 2, y + 16, 22, Faces.Bold, C.Ink);
                // Дом построен за каждую полную 1/12 нормы недели (98% — 11 домов, а не 12).
                var built = week.Share is { } sh ? (int)Math.Min(12, Math.Floor(sh * 12)) : 0;
                var cellW = (tileWidth - 24) / 3;
                for (var hIndex = 0; hIndex < 12; hIndex++)
                {
                    var hx = x + 12 + (hIndex % 3) * cellW + cellW / 2;
                    var hy = y + 56 + (hIndex / 3) * houseStep + 90 * scale / 2;
                    if (hIndex < built)
                    {
                        House(c, hx, hy, scale, BattleStoreStatus.Taken, BattleStoreStatus.Taken, hIndex + 1);
                    }
                    else
                    {
                        using var faded = new SKPaint { Color = SKColors.White.WithAlpha(80) };
                        c.SaveLayer(faded);
                        House(c, hx, hy, scale, BattleStoreStatus.Ahead, null, 0);
                        c.Restore();
                    }
                }

                var percentTop = y + 56 + 4 * houseStep + 4;
                var color = week.Share is >= 1 ? C.Green : week.Share is >= 0.5m ? C.Amber : C.Red;
                TextCenter(c, week.Share is { } s2 ? ReportFormat.Percent(s2) : "—", x + tileWidth / 2, percentTop, 30, Faces.Bold, week.Share is null ? C.Ink3 : color);
                TextCenter(c, Kg(week.Kg), x + tileWidth / 2, percentTop + 40, 17, Faces.SemiBold, C.Ink2);
            }

            PrizeRoad(c, roadTop, card.Total);
            MonthStats(c, statsTop, card);
            Footer(c, footerTop, MonthFooter(card));
            return Png(surface);
        }

        private void MonthStats(SKCanvas c, float top, IncentiveAgentCard card)
        {
            var month = card.Month!;
            var p = card.Progress;
            var monthName = IncentiveTexts.MonthName(month.Month, Language);
            if (month.Final || p.WorkDaysLeft == 0)
            {
                var won = p.Reached;
                Stats(c, top,
                    (T("Итог акции"), Kg(card.Total), C.Orange, IncentiveTexts.MonthsLine(P, card.Standing, cards.Date, Language)),
                    (T("Приз"), won is null ? T("не взят") : IncentiveTexts.Short(won, Language), won is null ? C.Red : C.Green,
                        won is null ? p.Next is { } miss ? F("{0} не хватило {1}", To(miss), Kg(p.ToNext ?? 0)) : "" : T("после проверки доставки — в январе")),
                    (card.Wholesale ? T("Место среди оптовых") : T("Место в акции"), card.Rank?.ToString(CultureInfo.InvariantCulture) ?? "—", C.Pink, F("из {0} ТП", card.Ranked)));
                return;
            }

            var months = new List<string>();
            for (var m = new DateOnly(P.Start.Year, P.Start.Month, 1); m <= cards.Date; m = m.AddMonths(1))
            {
                months.Add(IncentiveTexts.MonthName(m.Month, Language));
            }

            var change = month.PreviousKg > 0 ? $" · {ReportFormat.Signed(Math.Round((month.Kg / month.PreviousKg - 1) * 100))}% {T("к прошлому месяцу")}" : "";
            // Остаток до приза — на все рабочие дни до конца акции: «Декабрь до планшета», только если следующий месяц — последний.
            var nextDate = new DateOnly(month.Year, month.Month, 1).AddMonths(1);
            var lastMonthNext = nextDate.Year == P.End.Year && nextDate.Month == P.End.Month;
            var nextMonth = Capital(IncentiveTexts.MonthName(nextDate.Month, Language));
            var open = p.Tiers.Where(t => !t.Reached).Take(2).ToList();
            (string, string, SKColor, string?) Left(IncentiveTierLeft t, SKColor color)
            {
                var perDay = p.WorkDaysLeft > 0 ? F("≈ {0} в день", Kg(Math.Ceiling(t.Left / p.WorkDaysLeft))) : "";
                return lastMonthNext
                    ? ($"{nextMonth} {To(t.Tier)}", Kg(t.Left), color, perDay)
                    : (Capital(To(t.Tier)), Kg(t.Left), color, $"{perDay} · {F("{0} раб. дней до {1}", p.WorkDaysLeft, DayMonth(P.End))}");
            }
            var second = open.Count > 0 ? Left(open[0], C.Pink) : (T("Все призы взяты"), IncentiveTexts.Short(p.Reached!, Language), C.Green, (string?)null);
            var third = open.Count > 1 ? Left(open[1], C.Ink)
                : (card.Wholesale ? T("Место среди оптовых") : T("Место в акции"), card.Rank?.ToString(CultureInfo.InvariantCulture) ?? "—", C.Pink, F("из {0} ТП", card.Ranked));
            Stats(c, top, (Capital(string.Join(" + ", months)), Kg(card.Total), C.Orange, $"{monthName} {Kg(month.Kg)}{change}"), second, third);
        }

        private string MonthFooter(IncentiveAgentCard card)
        {
            var month = card.Month!;
            var p = card.Progress;
            if (month.Final || p.WorkDaysLeft == 0)
            {
                return p.Reached is { } won
                    ? F("Акция завершена: {0} · приз — {1}", Kg(card.Total), IncentiveTexts.Prize(won, Language))
                    : F("Акция завершена: {0} · итог — по доставленным накладным", Kg(card.Total));
            }

            var monthName = Capital(IncentiveTexts.MonthName(month.Month, Language));
            var nextDate = new DateOnly(month.Year, month.Month, 1).AddMonths(1);
            var nextMonth = Capital(IncentiveTexts.MonthName(nextDate.Month, Language));
            var nextMonthLower = IncentiveTexts.MonthName(nextDate.Month, Language);
            if (p.Next is not { } next)
            {
                return F("{0} сделан! Все призы акции взяты — накоплено {1}", monthName, Kg(card.Total));
            }

            var finalNext = nextDate.Year == P.End.Year && nextDate.Month == P.End.Month;
            return finalNext
                ? F("{0} сделан! {1} — финал: {2} {3}", monthName, nextMonth, Kg(p.ToNext ?? 0), To(next))
                : F("{0} сделан! Впереди {1}: {2} {3}", monthName, nextMonthLower, Kg(p.ToNext ?? 0), To(next));
        }

        // ---------- Рейтинг ----------

        public byte[] Rating(IncentiveRating rating)
        {
            var strip = IsTest ? 52f : 0f;
            var tableTop = strip + HeaderHeight + StaleHeight;
            var rowsHeight = 60 + 34 + Math.Max(1, rating.Retail.Count) * 44 + 20;
            var wholesaleTop = tableTop + rowsHeight + Gap;
            var wholesaleHeight = rating.Wholesale.Count > 0 ? 60 + 34 + rating.Wholesale.Count * 44 + 20 : 0;
            var prizesTop = wholesaleTop + (wholesaleHeight > 0 ? wholesaleHeight + Gap : 0);
            var footerTop = prizesTop + 140 + Gap;
            using var surface = Surface(footerTop + 92 + Pad, out var c);
            var subtitle = F("{0} · накопительно с {1} · на {2}", P.Product, StartText, DayMonth(cards.Date));
            Header(c, strip, T("Рейтинг акции"), rating.Scope is { Length: > 0 } scope ? $"{scope} · {subtitle}" : subtitle, IncentiveSlot.Rating, 0, 1,
                F("ТОП-{0}", Math.Max(1, rating.Retail.Count)), T("розница"));
            StaleBanner(c, strip + HeaderHeight);
            RatingTable(c, tableTop, rowsHeight, T("Кто ближе всех к призам"), rating.Retail);
            if (wholesaleHeight > 0)
            {
                RatingTable(c, wholesaleTop, wholesaleHeight, T("Оптовые агенты — свой рейтинг"), rating.Wholesale);
            }

            PrizeTiles(c, prizesTop);
            Footer(c, footerTop, F("До {0} — {1} рабочих дней · каждый кг приближает приз", EndText, rating.WorkDaysLeft));
            return Png(surface);
        }

        private void RatingTable(SKCanvas c, float top, float height, string title, IReadOnlyList<IncentiveRatingRow> rows)
        {
            CardBox(c, Pad, top, Inner, height);
            Text(c, title, Pad + 24, top + 18, 24, Faces.Bold, C.Ink);
            var header = top + 62;
            TextCenter(c, T("Место"), Pad + 56, header, 15, Faces.SemiBold, C.Ink3);
            Text(c, T("Агент"), Pad + 104, header, 15, Faces.SemiBold, C.Ink3);
            Text(c, T("Регион"), Pad + 500, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("Кг"), Pad + 760, header, 15, Faces.SemiBold, C.Ink3);
            Text(c, T("Статус"), Pad + 790, header, 15, Faces.SemiBold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            if (rows.Count == 0)
            {
                Text(c, T("Накоплений пока нет"), Pad + 104, header + 44, 18, Faces.Medium, C.Ink3);
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var y = header + 32 + i * 44;
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                if (row.Place == 1)
                {
                    using var lead = Fill(SKColor.Parse("#FFF1C2"));
                    c.DrawRect(Pad + 20, y + 1, Inner - 40, 43, lead);
                }

                var face = row.Place == 1 ? Faces.Bold : Faces.Medium;
                if (row.Place <= 3)
                {
                    using var medal = Fill(row.Place switch { 1 => SKColor.Parse("#F2B705"), 2 => SKColor.Parse("#B8C0C8"), _ => SKColor.Parse("#CD8B4E") });
                    c.DrawCircle(Pad + 56, y + 22, 15, medal);
                    TextCenter(c, row.Place.ToString(CultureInfo.InvariantCulture), Pad + 56, y + 12, 16, Faces.Bold, SKColors.White);
                }
                else
                {
                    TextCenter(c, row.Place.ToString(CultureInfo.InvariantCulture), Pad + 56, y + 11, 18, Faces.SemiBold, C.Ink);
                }

                TextFit(c, row.Agent.Name, Pad + 104, y + 11, 380, 18, face, C.Ink);
                TextFit(c, row.Agent.Region ?? "", Pad + 500, y + 11, 200, 18, face, C.Ink);
                TextRight(c, IncentiveTexts.Kg(row.Kg), Pad + 760, y + 11, 19, Faces.Bold, C.Ink);
                var status = row.Next is { } next
                    ? row.Reached is { } won ? $"{IncentiveTexts.Short(won, Language)} ✓ · {To(next)} {IncentiveTexts.Kg(row.ToNext ?? 0)}" : $"{To(next)} {Kg(row.ToNext ?? 0)}"
                    : $"{IncentiveTexts.Short(row.Reached!, Language)} ✓";
                TextFit(c, status, Pad + 790, y + 13, Inner - 790 - 24, 15, row.Reached is not null ? Faces.Bold : Faces.Medium, row.Reached is not null ? C.Green : C.Ink2);
            }
        }

        private void PrizeTiles(SKCanvas c, float top)
        {
            CardBox(c, Pad, top, Inner, 140);
            var tiers = P.Tiers.Take(3).ToList();
            var w = (Inner - 2 * 16 - (tiers.Count - 1) * 12) / Math.Max(1, tiers.Count);
            for (var i = 0; i < tiers.Count; i++)
            {
                var x = Pad + 16 + i * (w + 12);
                using var bg = Fill(SKColor.Parse("#FFF4E3"));
                c.DrawRoundRect(new SKRoundRect(new SKRect(x, top + 16, x + w, top + 124), 14), bg);
                using var white = Fill(C.Card);
                c.DrawRoundRect(new SKRoundRect(new SKRect(x + 10, top + 26, x + 120, top + 114), 10), white);
                if (PrizeImage(tiers[i]) is { } image)
                {
                    DrawPack(c, image, x + 14, top + 30, 102, 80);
                }

                TextFit(c, IncentiveTexts.Prize(tiers[i], Language), x + 132, top + 40, w - 142, 22, Faces.Bold, C.Ink);
                Text(c, Kg(tiers[i].Kg), x + 132, top + 76, 18, Faces.Medium, C.Ink2);
            }
        }

        // ---------- Сводка команды ----------

        public IEnumerable<byte[]> Team(IncentiveTeam team)
        {
            if (team.Role == "rm")
            {
                yield return TeamManager(team);
                yield break;
            }

            var agents = team.Agents;
            var chunks = Math.Max(1, (int)Math.Ceiling(agents.Count / (double)TeamRowsPerPage));
            for (var i = 0; i < chunks; i++)
            {
                yield return TeamSupervisor(team, agents.Skip(i * TeamRowsPerPage).Take(TeamRowsPerPage).ToList(), i, chunks);
            }
        }

        private (string Title, string Slot, string? Badge, string? Hint) TeamHeader()
        {
            var date = cards.Date;
            return cards.Slot switch
            {
                IncentiveSlot.Week => (F("Неделя команды · {0}", P.Product), IncentiveSlot.Week, F("Нед {0}", (BattleRules.Monday(date).Day - 1) / 7 + 1), IncentiveTexts.MonthOf(date.Month, Language)),
                IncentiveSlot.Month => (F("Месяц команды · {0}", P.Product), IncentiveSlot.Month, Capital(IncentiveTexts.MonthName(date.Month, Language)), date.Year.ToString(CultureInfo.InvariantCulture)),
                _ => (F("Команда · {0}", P.Product), cards.Slot, null, null),
            };
        }

        private void TeamStats(SKCanvas c, float top, IncentiveTeam team)
        {
            var count = team.Agents.Count;
            var second = cards.Slot switch
            {
                IncentiveSlot.Morning => (T("Цель маршрутов сегодня"), Kg(team.RouteKg ?? 0), C.Green, F("{0} ТП", count)),
                IncentiveSlot.Midday or IncentiveSlot.Evening => (T("Сегодня командой"), team.RouteKg is > 0 ? $"{IncentiveTexts.Kg(team.Today)} / {Kg(team.RouteKg.Value)}" : Kg(team.Today), C.Green,
                    T("из цели маршрутов")),
                _ => (T("Темп ниже нормы"), F("{0} ТП", team.Behind), team.Behind > 0 ? C.Red : C.Green, F("из {0}", count)),
            };
            Stats(c, top,
                (T("Накоплено командой"), Kg(team.Total), C.Orange, count > 0 ? F("{0} ТП · в среднем {1}", count, Kg(team.Total / count)) : ""),
                second,
                (T("У порога / взяли приз"), $"{team.Near} / {team.Reached}", C.Pink, T("у порога — от 80% до ближайшего приза")));
        }

        private byte[] TeamSupervisor(IncentiveTeam team, IReadOnlyList<IncentiveTeamRow> rows, int chunk, int chunks)
        {
            var strip = IsTest ? 52f : 0f;
            var statsTop = strip + HeaderHeight + StaleHeight;
            var first = chunk == 0;
            var tableTop = first ? statsTop + StatsHeight + Gap : statsTop;
            var tableHeight = 60 + 34 + Math.Max(1, rows.Count) * 42 + 20;
            var footerTop = tableTop + tableHeight + Gap;
            using var surface = Surface(footerTop + 92 + Pad, out var c);
            var (title, slot, badge, hint) = TeamHeader();
            Header(c, strip, title, $"{team.Title} · {Weekday(cards.Date)} {DayMonth(cards.Date)}", slot, chunk, chunks, badge, hint);
            StaleBanner(c, strip + HeaderHeight);
            if (first)
            {
                TeamStats(c, statsTop, team);
            }

            CardBox(c, Pad, tableTop, Inner, tableHeight);
            Text(c, T("ТП команды"), Pad + 24, tableTop + 18, 24, Faces.Bold, C.Ink);
            var header = tableTop + 62;
            var day = cards.Slot == IncentiveSlot.Morning ? T("Цель дня") : IncentiveSlot.IsDay(cards.Slot) ? T("Сегодня") : T("Темп");
            TextCenter(c, "№", Pad + 44, header, 15, Faces.SemiBold, C.Ink3);
            Text(c, T("ТП"), Pad + 76, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("Накоплено"), Pad + 500, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, day, Pad + 620, header, 15, Faces.SemiBold, C.Ink3);
            Text(c, T("До приза"), Pad + 650, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("Норма / темп"), Width - Pad - 24, header, 15, Faces.SemiBold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var y = header + 32 + i * 42;
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                TextCenter(c, (chunk * TeamRowsPerPage + i + 1).ToString(CultureInfo.InvariantCulture), Pad + 44, y + 11, 17, Faces.SemiBold, C.Ink2);
                TextFit(c, row.Agent.Name + (row.Wholesale ? $" · {T("опт")}" : ""), Pad + 76, y + 11, 330, 18, Faces.SemiBold, C.Ink);
                TextRight(c, IncentiveTexts.Kg(row.Total), Pad + 500, y + 11, 18, Faces.Bold, C.Ink);
                var dayValue = cards.Slot == IncentiveSlot.Morning ? row.RouteKg is { } rk ? IncentiveTexts.Kg(rk) : "—"
                    : IncentiveSlot.IsDay(cards.Slot) ? IncentiveTexts.Kg(row.Today) : IncentiveTexts.Kg(row.Pace);
                TextRight(c, dayValue, Pad + 620, y + 11, 18, Faces.Medium, C.Ink);
                var prize = row.Progress.Next is { } next ? $"{To(next)} {IncentiveTexts.Kg(row.Progress.ToNext ?? 0)}" : $"{IncentiveTexts.Short(row.Progress.Reached!, Language)} ✓";
                TextFit(c, prize, Pad + 650, y + 12, 200, 16, Faces.Medium, row.Near || row.Reached ? C.Green : C.Ink2);
                var norm = row.Progress.Norm is { } n ? $"{IncentiveTexts.Kg(n)} / {IncentiveTexts.Kg(row.Pace)}" : $"— / {IncentiveTexts.Kg(row.Pace)}";
                TextRight(c, norm, Width - Pad - 24, y + 11, 17, Faces.Bold, row.Behind ? C.Red : C.Green);
            }

            var leader = team.Agents.FirstOrDefault();
            Footer(c, footerTop, chunk < chunks - 1
                ? F("Продолжение списка — на следующей картинке ({0} из {1})", chunk + 2, chunks)
                : leader is null ? T("В команде нет ТП акции")
                : F("Темп ниже нормы у {0} из {1} ТП · лидер — {2}, {3}", team.Behind, team.Agents.Count, leader.Agent.Name, Kg(leader.Total)));
            return Png(surface);
        }

        private byte[] TeamManager(IncentiveTeam team)
        {
            var strip = IsTest ? 52f : 0f;
            var statsTop = strip + HeaderHeight + StaleHeight;
            var regionsTop = statsTop + StatsHeight + Gap;
            var regionsHeight = 60 + 34 + Math.Max(1, team.Regions.Count) * 42 + 20;
            var top = team.Agents.Take(10).ToList();
            var bestTop = regionsTop + regionsHeight + Gap;
            var bestHeight = 60 + 34 + Math.Max(1, top.Count) * 42 + 20;
            var footerTop = bestTop + bestHeight + Gap;
            using var surface = Surface(footerTop + 92 + Pad, out var c);
            var (title, slot, badge, hint) = TeamHeader();
            Header(c, strip, title, $"{team.Title} · {Weekday(cards.Date)} {DayMonth(cards.Date)}", slot, 0, 1, badge, hint);
            StaleBanner(c, strip + HeaderHeight);
            TeamStats(c, statsTop, team);

            CardBox(c, Pad, regionsTop, Inner, regionsHeight);
            Text(c, T("Регионы"), Pad + 24, regionsTop + 18, 24, Faces.Bold, C.Ink);
            var header = regionsTop + 62;
            var day = cards.Slot == IncentiveSlot.Morning ? T("Цель дня") : T("Сегодня");
            Text(c, T("Регион · СВ"), Pad + 24, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("ТП"), Pad + 470, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("Накоплено"), Pad + 600, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, day, Pad + 720, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("Отстают"), Pad + 830, header, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("У порога / взяли"), Width - Pad - 24, header, 15, Faces.SemiBold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            for (var i = 0; i < team.Regions.Count; i++)
            {
                var region = team.Regions[i];
                var y = header + 32 + i * 42;
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                TextFit(c, region.Supervisor is { Length: > 0 } sv ? $"{region.Name} · {sv}" : region.Name, Pad + 24, y + 11, 380, 18, Faces.SemiBold, C.Ink);
                TextRight(c, region.Agents.Count.ToString(CultureInfo.InvariantCulture), Pad + 470, y + 11, 18, Faces.Medium, C.Ink);
                TextRight(c, IncentiveTexts.Kg(region.Total), Pad + 600, y + 11, 18, Faces.Bold, C.Ink);
                var dayValue = cards.Slot == IncentiveSlot.Morning ? region.RouteKg is { } rk ? IncentiveTexts.Kg(rk) : "—" : IncentiveTexts.Kg(region.Today);
                TextRight(c, dayValue, Pad + 720, y + 11, 18, Faces.Medium, C.Ink);
                TextRight(c, region.Behind.ToString(CultureInfo.InvariantCulture), Pad + 830, y + 11, 18, Faces.Bold, region.Behind > 0 ? C.Red : C.Green);
                TextRight(c, $"{region.Near} / {region.Reached}", Width - Pad - 24, y + 11, 18, Faces.Bold, C.Ink);
            }

            CardBox(c, Pad, bestTop, Inner, bestHeight);
            Text(c, T("Лучшие ТП"), Pad + 24, bestTop + 18, 24, Faces.Bold, C.Ink);
            var bestHeader = bestTop + 62;
            TextCenter(c, "№", Pad + 44, bestHeader, 15, Faces.SemiBold, C.Ink3);
            Text(c, T("ТП"), Pad + 76, bestHeader, 15, Faces.SemiBold, C.Ink3);
            TextRight(c, T("Накоплено"), Pad + 640, bestHeader, 15, Faces.SemiBold, C.Ink3);
            Text(c, T("До приза"), Pad + 680, bestHeader, 15, Faces.SemiBold, C.Ink3);
            for (var i = 0; i < top.Count; i++)
            {
                var row = top[i];
                var y = bestHeader + 32 + i * 42;
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                TextCenter(c, (i + 1).ToString(CultureInfo.InvariantCulture), Pad + 44, y + 11, 17, Faces.SemiBold, C.Ink2);
                TextFit(c, row.Agent.Region is { Length: > 0 } reg ? $"{row.Agent.Name} · {reg}" : row.Agent.Name, Pad + 76, y + 11, 440, 18, Faces.SemiBold, C.Ink);
                TextRight(c, IncentiveTexts.Kg(row.Total), Pad + 640, y + 11, 18, Faces.Bold, C.Ink);
                var prize = row.Progress.Next is { } next ? $"{To(next)} {Kg(row.Progress.ToNext ?? 0)}" : $"{IncentiveTexts.Short(row.Progress.Reached!, Language)} ✓";
                TextFit(c, prize, Pad + 680, y + 12, Inner - 680 - 24, 16, Faces.Medium, row.Near || row.Reached ? C.Green : C.Ink2);
            }

            var leader = team.Agents.FirstOrDefault();
            Footer(c, footerTop, leader is null ? T("В регионах нет ТП акции")
                : F("Темп ниже нормы у {0} из {1} ТП · у порога {2} · взяли приз {3} · лидер — {4}, {5}", team.Behind, team.Agents.Count, team.Near, team.Reached,
                    leader.Agent.Name, Kg(leader.Total)));
            return Png(surface);
        }

        // ---------- Общие блоки акции ----------

        private SKSurface Surface(float height, out SKCanvas canvas)
        {
            var surface = SKSurface.Create(new SKImageInfo(Width, (int)Math.Ceiling(height), SKColorType.Rgba8888, SKAlphaType.Premul));
            canvas = surface.Canvas;
            canvas.Clear(C.Background);
            if (IsTest)
            {
                TestStrip(canvas);
            }

            return surface;
        }

        /// <summary>Данные Linko не обновились к отправке — отметка с временем последнего обновления (последний подтверждённый результат).</summary>
        private void StaleBanner(SKCanvas c, float top)
        {
            if (!cards.Stale)
            {
                return;
            }

            using var bg = Fill(SKColor.Parse("#FFF0D6"));
            using var edge = Stroke(C.Amber, 2);
            var rect = new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + 44), 12);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            var at = cards.DataAsOf is { } asOf ? ReportSchedules.Local(asOf, ReportSchedules.DefaultTimeZone).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture) : "—";
            TextCenter(c, F("Данные Linko на {0} — свежее обновление не пришло", at), Width / 2, top + 11, 18, Faces.SemiBold, SKColor.Parse("#8A5A00"));
        }

        /// <summary>Три счётчика: подпись, крупное значение, мелкая строка под ним.</summary>
        private void Stats(SKCanvas c, float top, params (string Label, string Value, SKColor Color, string? Sub)[] items)
        {
            var w = (Inner - 2 * 16) / 3;
            for (var i = 0; i < items.Length && i < 3; i++)
            {
                var x = Pad + i * (w + 16);
                CardBox(c, x, top, w, StatsHeight);
                TextFit(c, items[i].Label, x + 20, top + 16, w - 40, 19, Faces.Medium, C.Ink2);
                TextFit(c, items[i].Value, x + 20, top + 42, w - 40, 42, Faces.Bold, items[i].Color);
                if (items[i].Sub is { Length: > 0 } sub)
                {
                    TextFit(c, sub, x + 20, top + 94, w - 40, 15, Faces.Medium, C.Ink3);
                }
            }
        }

        private static SKBitmap? PrizeImage(IncentiveTier tier) => FarmAssets.Images.GetValueOrDefault("Prize_" + tier.Image);

        /// <summary>
        /// Дорога к призам: полоса от 0 до порога старшего приза (+8%), флажки ступеней (взятые — зелёные), картинки призов над флажками, названия под
        /// дорогой, жёлтые следы до накопленного и герой на нём.
        /// </summary>
        private void PrizeRoad(SKCanvas c, float top, decimal total)
        {
            CardBox(c, Pad, top, Inner, RoadHeight);
            Trophy(c, Pad + 44, top + 36, 1f);
            Text(c, T("Дорога к призам"), Pad + 84, top + 18, 26, Faces.Bold, C.Ink);
            TextRight(c, F("накоплено {0}", Kg(total)), Width - Pad - 24, top + 22, 20, Faces.SemiBold, C.Ink2);
            var tiers = P.Tiers;
            if (tiers.Count == 0)
            {
                return;
            }

            var x0 = Pad + 40f;
            var x1 = Width - Pad - 40f;
            var max = tiers.Max(t => t.Kg) * 1.08m;
            float X(decimal kg) => x0 + (x1 - x0) * (float)(Math.Clamp(kg, 0, max) / max);
            var barY = top + 232;
            using var bar = Fill(SKColor.Parse("#F0D6A8"));
            c.DrawRoundRect(new SKRoundRect(new SKRect(x0 - 16, barY, x1 + 16, barY + 30), 15), bar);
            using var dot = Fill(C.Amber);
            for (var x = x0 + 4; x <= X(total) - 4; x += 18)
            {
                c.DrawCircle(x, barY + 15, 5.5f, dot);
            }

            foreach (var tier in tiers)
            {
                var fx = X(tier.Kg);
                var reached = total >= tier.Kg;
                using var pole = Fill(C.Trunk);
                c.DrawRect(fx - 2, barY - 70, 4, 86, pole);
                using var flag = Fill(reached ? C.Green : SKColor.Parse("#C9B48C"));
                using (var pennant = new SKPath())
                {
                    pennant.MoveTo(fx + 2, barY - 70);
                    pennant.LineTo(fx + 94, barY - 70);
                    pennant.LineTo(fx + 84, barY - 57);
                    pennant.LineTo(fx + 94, barY - 44);
                    pennant.LineTo(fx + 2, barY - 44);
                    pennant.Close();
                    c.DrawPath(pennant, flag);
                }

                Text(c, Kg(tier.Kg), fx + 8, barY - 66, 15, Faces.Bold, SKColors.White);
                if (PrizeImage(tier) is { } image)
                {
                    var iw = 150f;
                    var ix = Math.Clamp(fx - iw / 2, Pad + 12, Width - Pad - 12 - iw);
                    DrawPack(c, image, ix, top + 60, iw, 92);
                }

                var name = IncentiveTexts.Prize(tier, Language) + (reached ? " ✓" : "");
                TextCenter(c, name, Math.Clamp(fx, Pad + 100, Width - Pad - 100), barY + 44, 19, Faces.Bold, reached ? C.Green : C.Ink);
            }

            Hero(c, Math.Max(x0 + 10, X(total)), barY - 26);
        }
    }
}

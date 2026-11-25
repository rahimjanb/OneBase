using System.Globalization;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Domain.Incentive;
using SkiaSharp;
using C = OneBase.Infrastructure.Telegram.Reports.FarmColors;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Семь карточек «Целей на маршрут» v2 (ТЗ Улугбека v2 от 24.11.2026, образцы 1–7): ТП 08:30 «Маршрут дня», 13:00 «Полдень», 18:00 «Итог дня»,
/// сб «Итог недели», 30.11 «Итог месяца»; СВР 18:30 «Итог дня команды»; РМ сб 19:00 «Итог недели зоны». Те же фигуры «Поля боя»: шапка, плитки,
/// поле маршрута с домиками; в выноске — цель Могул (розовая) и Twizos (оранжевая 5%, зелёная 8%, фиолетовая 10%), два баланса кешбека, жёлтый
/// итог. Карточка — одна PNG 1080 px любой высоты: отправляется файлом без сжатия.
/// </summary>
internal static partial class IncentiveCardRenderer
{
    private sealed partial class Painter
    {
        private const float GoalCallout = 230;
        private static readonly SKColor MogulInk = SKColor.Parse("#D6336C");
        private static readonly SKColor ExistingInk = SKColor.Parse("#E8731A");
        private static readonly SKColor NewInk = SKColor.Parse("#2E9E4F");
        private static readonly SKColor StandInk = SKColor.Parse("#7B3FE4");
        private static readonly SKColor Grey = SKColor.Parse("#9A8E82");

        private IncentiveGoalsPromo? G => P.Offers.Goals;

        private string Clients(int n) => Plural(n, "{0} клиент", "{0} клиента", "{0} клиентов");

        private string Wins(int n) => Plural(n, "{0} победа", "{0} победы", "{0} побед");

        private static string Pct(decimal share) => Math.Round(share * 100).ToString("0", CultureInfo.InvariantCulture);

        private static string Num(decimal v) => IncentiveTexts.Kg(v);

        private static SKColor SegmentInk(string? segment) => segment switch
        {
            IncentiveTwizosSegment.Stand => StandInk,
            IncentiveTwizosSegment.Existing => ExistingInk,
            _ => NewInk,
        };

        private string SegmentLabel(string segment, decimal percent) => segment switch
        {
            IncentiveTwizosSegment.Stand => F("новый + стойка · {0}%", Num(percent)),
            IncentiveTwizosSegment.Existing => F("действующий · {0}%", Num(percent)),
            _ => F("новый · {0}%", Num(percent)),
        };

        /// <summary>Набор «140x8+35x2» → «8 кор. 140 г + 2 кор. 35 г».</summary>
        private string SetText(string set) => string.Join(" + ", IncentiveGoalRules.ParseSet(set)
            .Select(b => F("{0} кор. {1}", b.Count, Units(G?.Box(b.Box)?.Name ?? $"{b.Box} г"))));

        /// <summary>Цветная плашка (сегмент, «спящий −7%»); возвращает ширину.</summary>
        private float Chip(SKCanvas c, float x, float y, string text, SKColor color)
        {
            var w = Measure(text, Faces.Bold, 11.5f) + 12;
            using var bg = Fill(color);
            c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + w, y + 19), 6), bg);
            Text(c, text, x + 6, y + 3, 11.5f, Faces.Bold, SKColors.White);
            return w;
        }

        // ---------- Карточка дня ТП ----------

        public byte[] GoalDay(IncentiveAgentCard card, IncentiveGoalDay day, bool compact)
        {
            var slot = cards.Slot;
            var evaluated = slot != IncentiveSlot.Morning;
            var route = day.Route;
            var stores = route.Stores;
            var outside = evaluated ? route.OutOfRoute ?? [] : [];
            var t = day.Totals;
            var strip = IsTest ? 52f : 0f;
            var head = strip + HeaderHeight + StaleHeight;

            var blocks = new List<Block> { new(StatsHeight, (c, y) => GoalDayTiles(c, y, t)) };
            if (slot == IncentiveSlot.Morning && !compact)
            {
                var offers = GoalOffersHeight(day);
                blocks.Add(new Block(offers, (c, y) => GoalOffers(c, y, offers, day)));
                var how = GoalHowHeight(t);
                blocks.Add(new Block(how, (c, y) => GoalHow(c, y, how, t)));
            }

            var rows = new List<float>();
            for (var i = 0; i < stores.Count; i += 2)
            {
                var h = Math.Max(GoalCalloutHeight(stores[i], evaluated), i + 1 < stores.Count ? GoalCalloutHeight(stores[i + 1], evaluated) : 0);
                rows.Add(Math.Max(150, h + 26));
            }

            var mapHeight = stores.Count > 0 ? 88 + rows.Sum() : 140;
            var after = new List<Block>();
            if (outside.Count > 0)
            {
                var height = OutsideHeight(outside);
                after.Add(new Block(height, (c, y) => Outside(c, y, height, outside)));
            }

            after.Add(new Block(BalanceHeight, (c, y) => Balance(c, y, day.Balances, BalanceNote(slot), T("Оформлено"))));
            var footer = GoalDayFooter(t);
            var height0 = head + blocks.Sum(b => b.Height + Gap) + mapHeight + Gap + after.Sum(b => b.Height + Gap) + FooterHeight(footer) + Pad;
            using var surface = Surface(height0, out var canvas);
            var title = slot switch { IncentiveSlot.Midday => T("Полдень"), IncentiveSlot.Afternoon => T("Итоги 16:00"), IncentiveSlot.Evening => T("Итог дня"), _ => T("Маршрут дня") };
            Header(canvas, strip, title, $"{AgentTitle(card.Agent)} · {Weekday(cards.Date)} {DayMonth(cards.Date)}", slot, 0, 1);
            StaleBanner(canvas, strip + HeaderHeight);
            var top = head;
            foreach (var block in blocks)
            {
                block.Draw(canvas, top);
                top += block.Height + Gap;
            }

            if (stores.Count == 0)
            {
                using var bg = Fill(C.Map);
                using var edge = Stroke(C.MapBorder, 3);
                var rect = new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + 140), 24);
                canvas.DrawRoundRect(rect, bg);
                canvas.DrawRoundRect(rect, edge);
                TextCenter(canvas, T("Маршрута на этот день нет: в Linko нет плана визитов и точек ТП"), Width / 2, top + 56, 20, Faces.SemiBold, C.Ink2);
            }
            else
            {
                var statuses = stores.Select(GoalStopStatus).ToList();
                Farm(canvas, top, mapHeight, stores.Select(s => new FarmStop(GoalStopStatus(s), s.Order)).ToList(), rows, slot, 0, (i, x, cy) =>
                {
                    var h = GoalCalloutHeight(stores[i], evaluated);
                    var cx = x < Width / 2 ? Pad + 12 : Width - Pad - 12 - GoalCallout;
                    GoalCalloutBox(canvas, cx, cy - h / 2 + 4, stores[i], evaluated);
                }, stores.Count, HeroOnPage(slot, statuses, 0, stores.Count));
            }

            top += mapHeight + Gap;
            foreach (var block in after)
            {
                block.Draw(canvas, top);
                top += block.Height + Gap;
            }

            PromoFooter(canvas, top, footer);
            return Png(surface);
        }

        /// <summary>Домик точки: без цели и без итога — серый.</summary>
        private static string GoalStopStatus(IncentiveStore s) => s.Plain && s.Status == BattleStoreStatus.Ahead ? PlainStatus : s.Status;

        private string BalanceNote(string slot) => slot == IncentiveSlot.Evening
            ? T("оформлено сегодня → ждёт доставки → выплата пн–вт")
            : T("выплата пн–вт за прошлую неделю");

        private void GoalDayTiles(SKCanvas c, float top, IncentiveGoalTotals t)
        {
            var mogul = P.Offers.Yamelly?.CashbackPercent ?? 10;
            var range = G is { } g ? F("{0}–{1}%", Num(g.ExistingPercent), Num(g.StandPercent)) : "";
            switch (cards.Slot)
            {
                case IncentiveSlot.Morning:
                    Stats(c, top,
                        (T("Спящие Могул на маршруте"), $"0 / {t.MogulGoals}", MogulInk, F("кешбек до {0} сум", Money(t.MogulGoalCashback))),
                        (T("Twizos — клиенты с целью"), $"0 / {t.TwizosGoals}", ExistingInk, F("кешбек до {0} сум", Money(t.TwizosGoalCashback))),
                        (T("Можно заработать сегодня"), Money(t.MogulGoalCashback + t.TwizosGoalCashback), C.Green, F("Могул {0}% + Twizos {1}", Num(mogul), range)));
                    return;
                case IncentiveSlot.Midday or IncentiveSlot.Afternoon:
                    Stats(c, top,
                        (T("Спящие Могул"), $"{t.Woken} / {t.MogulGoals}", MogulInk, t.MogulCashback > 0
                            ? F("кешбек +{0} · ждёт доставки", Money(t.MogulCashback))
                            : F("кешбек до {0} сум", Money(t.MogulGoalCashback))),
                        (T("Twizos сегодня"), $"{t.TwizosClients} / {t.TwizosGoals}", ExistingInk,
                            t.Gifts.Count > 0 ? F("{0} · подарков: {1}", Kg1(t.TwizosKg), GiftsLine(t.Gifts)) : Kg1(t.TwizosKg)),
                        (T("Точек пройдено"), $"{t.Passed} / {t.Stores}", C.Ink, F("кешбек Twizos +{0}", Money(t.TwizosCashback))));
                    return;
                default:
                    Stats(c, top,
                        (T("Спящие Могул"), $"{t.Woken} / {t.MogulGoals}", MogulInk, F("кешбек дня +{0}", Money(t.MogulCashback))),
                        (T("Twizos за день"), Clients(t.TwizosClients), ExistingInk, F("{0} · кешбек +{1}", Kg1(t.TwizosKg), Money(t.TwizosCashback))),
                        (T("Заработал сегодня"), Money(t.Cashback), C.Green, T("Могул + Twizos · после доставки")));
                    return;
            }
        }

        private string GoalDayFooter(IncentiveGoalTotals t)
        {
            switch (cards.Slot)
            {
                case IncentiveSlot.Morning:
                    return F("{0} · {1} · {2}", Stores(t.Stores), F("спящих Могул {0}", t.MogulGoals), F("целей Twizos {0}", t.TwizosGoals))
                           + "\n" + F("одна точка — один визит, все предложения · можно заработать {0} сум", Money(t.MogulGoalCashback + t.TwizosGoalCashback));
                case IncentiveSlot.Midday or IncentiveSlot.Afternoon:
                    return F("Заработано {0} сум · осталось {1}", Money(t.Cashback), Stores(Math.Max(0, t.Stores - t.Passed)))
                           + "\n" + T("где взяли меньше цели — предложи добрать до следующего подарка");
                default:
                {
                    var gifts = t.Gifts.Count > 0 ? F("подарков сегодня: {0}", GiftsLine(t.Gifts)) : T("подарков сегодня нет");
                    if (t.Stands > 0)
                    {
                        gifts += " · " + F("стоек {0}", t.Stands);
                    }

                    return (t.Refused.Count > 0
                               ? F("НА МУШКЕ · отказы в ТТ {0} — в маршрут через неделю", string.Join(", ", t.Refused.Take(12)))
                               : F("Итог дня: {0} сум кешбека · отказов нет", Money(t.Cashback)))
                           + "\n" + gifts;
                }
            }
        }

        // ---------- «Что предлагаем» и «Как собраны цели» ----------

        private static float GoalOffersHeight(IncentiveGoalDay day) => 300;

        private void GoalOffers(SKCanvas c, float top, float height, IncentiveGoalDay day)
        {
            CardBox(c, Pad, top, Inner, height);
            Text(c, T("Что предлагаем клиенту и что получаешь ты"), Pad + 24, top + 18, 22, Faces.Bold, C.Ink);
            if (G is { } g)
            {
                TextRight(c, F("акции до {0}", DayMonth(g.End)), Width - Pad - 24, top + 24, 15, Faces.SemiBold, C.Ink2);
            }

            var y = top + 62;
            var ym = P.Offers.Yamelly;
            var mogul = ym is not null && !(G?.MogulExcluded(day.Region) ?? false);
            var leftW = mogul ? (Inner - 40 - 14) * 0.4f : 0;
            var rightX = Pad + 20 + (mogul ? leftW + 14 : 0);
            var rightW = Width - Pad - 20 - rightX;
            var boxH = height - 62 - 18;
            if (mogul && ym is not null && G is { } gm)
            {
                using (var bg = Fill(Rose))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(Pad + 20, y, Pad + 20 + leftW, y + boxH), 14), bg);
                }

                var x = Pad + 34;
                var w = leftW - 28;
                TextFit(c, T("Могул · только спящим из списка"), x, y + 12, w, 17, Faces.Bold, MogulInk);
                var ly = y + 40;
                foreach (var line in Wrap(F("Клиенту: −{0}% на весь Могул при {1} целых коробках одной фасовки", Num(ym.DiscountPercent), ym.MinBoxes), Faces.Medium, 13.5f, w, 3))
                {
                    Text(c, line, x, ly, 13.5f, Faces.Medium, C.Ink2);
                    ly += 19;
                }

                TextFit(c, F("Тебе: кешбек {0}% от суммы после скидки", Num(ym.CashbackPercent)), x, ly, w, 13.5f, Faces.SemiBold, C.Ink);
                ly += 22;
                TextFit(c, F("Цель = средний закуп в месяц × {0}", Num(1 + gm.MogulGrowth)), x, ly, w, 13.5f, Faces.Medium, C.Ink2);
                ly += 30;
                if (gm.MogulBoxes?.FirstOrDefault() is { } box)
                {
                    var sum = box.Price * ym.MinBoxes;
                    var after = IncentivePromoRules.AfterDiscount(ym, sum);
                    if (FarmAssets.Images.GetValueOrDefault(YamellyPacks[0]) is { } pack)
                    {
                        DrawPack(c, pack, x, ly - 4, 34, 40);
                    }

                    TextFit(c, F("{0} кор. {1}: {2} → {3}", ym.MinBoxes, Units(box.Name), Money(sum), Money(after)), x + 42, ly, w - 42, 13.5f, Faces.SemiBold, C.Ink);
                    TextFit(c, $"+{Money(IncentiveGoalRules.Cashback(after, ym.CashbackPercent))}", x + 42, ly + 20, w - 42, 14, Faces.Bold, C.Green);
                }
            }

            using (var bg = Fill(Cream))
            {
                c.DrawRoundRect(new SKRoundRect(new SKRect(rightX, y, rightX + rightW, y + boxH), 14), bg);
            }

            var rx = rightX + 14;
            var rw = rightW - 28;
            TextFit(c, T("Twizos · подарок клиенту + кешбек тебе"), rx, y + 12, rw, 17, Faces.Bold, ExistingInk);
            var gifts = P.Offers.Twizos?.GiftTiers ?? [];
            var gw = gifts.Count == 0 ? 0 : (rw - (gifts.Count - 1) * 10) / gifts.Count;
            for (var i = 0; i < gifts.Count; i++)
            {
                var gx = rx + i * (gw + 10);
                using (var white = Fill(C.Card))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(gx, y + 40, gx + gw, y + 124), 10), white);
                }

                if (FarmAssets.Images.GetValueOrDefault("Gift_" + gifts[i].Gift) is { } img)
                {
                    DrawPack(c, img, gx + gw / 2 - 22, y + 44, 44, 40);
                }

                TextCenter(c, GiftName(gifts[i].Gift), gx + gw / 2, y + 86, 13, Faces.Bold, C.Ink);
                TextCenter(c, F("от {0}", Money(gifts[i].MinSum)), gx + gw / 2, y + 104, 12, Faces.Medium, C.Ink2);
            }

            if (G is { } g2)
            {
                var chips = new[]
                {
                    (Num(g2.ExistingPercent) + "%", T("действующий клиент"), ExistingInk),
                    (Num(g2.NewPercent) + "%", T("новый клиент"), NewInk),
                    (Num(g2.StandPercent) + "%", F("новый + стойка · от {0}", Money(g2.StandCheck)), StandInk),
                };
                var cw = (rw - 2 * 8) / 3;
                for (var i = 0; i < chips.Length; i++)
                {
                    var cx = rx + i * (cw + 8);
                    using var bg = Fill(chips[i].Item3);
                    c.DrawRoundRect(new SKRoundRect(new SKRect(cx, y + 134, cx + cw, y + 190), 10), bg);
                    Text(c, chips[i].Item1, cx + 10, y + 138, 20, Faces.Bold, SKColors.White);
                    TextFit(c, chips[i].Item2, cx + 10, y + 166, cw - 20, 11.5f, Faces.Medium, SKColors.White);
                }
            }

            var top35 = day.Top35.Take(2).Select(s => Sku(s).Name).ToList();
            var top140 = day.Top140.Take(2).Select(s => Sku(s).Name).ToList();
            if (top35.Count + top140.Count > 0)
            {
                TextFit(c, F("ТОП SKU: {0}", string.Join(" · ", new[] { string.Join(", ", top35), string.Join(", ", top140) }.Where(x => x.Length > 0))),
                    rx, y + 198, rw, 12.5f, Faces.Medium, C.Ink2);
            }
        }

        private static float GoalHowHeight(IncentiveGoalTotals t) => 62 + 6 * 34 + 14;

        private void GoalHow(SKCanvas c, float top, float height, IncentiveGoalTotals t)
        {
            CardBox(c, Pad, top, Inner, height);
            DrawCompass(c, Pad + 36, top + 32);
            Text(c, T("Как собраны цели на сегодня"), Pad + 60, top + 18, 22, Faces.Bold, C.Ink);
            TextRight(c, F("{0} на маршруте", Stores(t.Stores)), Width - Pad - 24, top + 24, 15, Faces.SemiBold, C.Ink2);
            var g = G;
            var rows = new List<(string Head, string Text, SKColor Ink, int Count)>
            {
                (T("Могул"), F("спящие из списка → средний закуп в месяц × {0}, мин. {1} коробки", Num(1 + (g?.MogulGrowth ?? 0.2m)), P.Offers.Yamelly?.MinBoxes ?? 2), MogulInk, t.MogulGoals),
                (F("Twizos {0}%", Num(g?.ExistingPercent ?? 5)), F("уже берут → обычный закуп × {0}, не меньше следующего подарка", Num(1 + (g?.TwizosGrowth ?? 0.3m))), ExistingInk,
                    t.Segments.GetValueOrDefault(IncentiveTwizosSegment.Existing)),
                (F("Twizos {0}%", Num(g?.NewPercent ?? 8)), T("новые → старт по среднему чеку (кружка / термос / чайник)"), NewInk, t.Segments.GetValueOrDefault(IncentiveTwizosSegment.New)),
                (F("Twizos {0}%", Num(g?.StandPercent ?? 10)), F("новые с чеком от {0} → стойка + набор с чайником", Money(g?.StandCheck ?? 600_000)), StandInk,
                    t.Segments.GetValueOrDefault(IncentiveTwizosSegment.Stand)),
                (T("Без акции"), T("обычный заказ (серые)"), Grey, t.Plain),
                ("", T("В одной ТТ и Могул, и Twizos — один визит, два предложения"), C.Ink, t.Both),
            };
            using var line = Stroke(C.Border, 1.5f);
            var y = top + 62;
            foreach (var (headText, text, ink, count) in rows)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                var x = Pad + 24;
                if (headText.Length > 0)
                {
                    Text(c, headText, x, y + 8, 15, Faces.Bold, ink);
                    x += Measure(headText, Faces.Bold, 15) + 8;
                }

                TextFit(c, (headText.Length > 0 ? "· " : "") + text, x, y + 9, Width - Pad - 110 - x, 14, Faces.Medium, C.Ink2);
                TextRight(c, F("{0} ТТ", count), Width - Pad - 24, y + 8, 15, Faces.Bold, C.Ink);
                y += 34;
            }
        }

        // ---------- Выноска точки ----------

        private List<string> GoalName(IncentiveStore s) =>
            Wrap($"{s.Order}. {(string.IsNullOrWhiteSpace(s.Name) ? F("ТТ {0}", s.MarketId) : s.Name.Trim())}", Faces.Bold, 15, GoalCallout - 24, 2);

        private float GoalCalloutHeight(IncentiveStore s, bool evaluated)
        {
            var extra = (GoalName(s).Count - 1) * 19;
            var goal = s.Goal;
            if (goal is null || s.Plain)
            {
                return 66 + extra;
            }

            var h = 12f + 22 + extra;
            if (goal.Mogul is not null)
            {
                h += 6 + 24 + 4 * 19 + (evaluated && s.Fact?.MogulStatus is { } ms && ms != BattleStoreStatus.Ahead ? 19 : 0);
            }

            if (goal.Twizos is not null)
            {
                h += (goal.Mogul is not null ? 10 : 6) + 24 + 19 + 19 + 22 + 19 + (evaluated && s.Fact?.TwizosStatus is { } ts && ts != BattleStoreStatus.Ahead ? 19 : 0);
            }

            return h + 10;
        }

        private void GoalCalloutBox(SKCanvas c, float x, float y, IncentiveStore s, bool evaluated)
        {
            const float w = GoalCallout;
            var h = GoalCalloutHeight(s, evaluated);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 14);
            var name = GoalName(s);
            if (s.Goal is null || s.Plain)
            {
                using var paper = Fill(Paper);
                using var dash = new SKPaint { Color = PaperEdge, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, PathEffect = SKPathEffect.CreateDash([6, 4], 0) };
                c.DrawRoundRect(rect, paper);
                c.DrawRoundRect(rect, dash);
                for (var i = 0; i < name.Count; i++)
                {
                    TextFit(c, name[i], x + 12, y + 10 + i * 19, w - 24, 15, Faces.SemiBold, C.Ink2);
                }

                var ly = y + 34 + (name.Count - 1) * 19;
                var plain = s.AvgCheck > 0 ? F("без акции · обычный заказ ~{0}", Check(s.AvgCheck)) : T("без акции · обычный заказ");
                TextFit(c, plain, x + 12, ly, w - 24 - (evaluated && s.HasOrder ? 70 : 0), 12.5f, Faces.Medium, C.Ink3);
                if (evaluated && s.HasOrder)
                {
                    TextRight(c, $"✓ {T("заказ")}", x + w - 12, ly, 12.5f, Faces.SemiBold, C.Green);
                }

                return;
            }

            using (var bg = Fill(evaluated && s.Status == BattleStoreStatus.Refused ? Refused : C.Card))
            using (var edge = Stroke(C.Border, 2))
            {
                c.DrawRoundRect(rect, bg);
                c.DrawRoundRect(rect, edge);
            }

            for (var i = 0; i < name.Count; i++)
            {
                TextFit(c, name[i], x + 12, y + 10 + i * 19, w - 24, 15, Faces.Bold, C.Ink);
            }

            var cy = y + 34 + (name.Count - 1) * 19;
            var tx = x + 12;
            var tw = w - 24;
            var fact = s.Fact ?? IncentiveGoalFact.None;
            if (s.Goal.Mogul is { } m && P.Offers.Yamelly is { } ym)
            {
                cy += 6;
                if (FarmAssets.Images.GetValueOrDefault(YamellyPacks[(int)(Math.Abs(s.MarketId) % YamellyPacks.Length)]) is { } pack)
                {
                    DrawPack(c, pack, tx, cy - 4, 20, 24);
                }

                Text(c, T("Могул"), tx + 24, cy, 14, Faces.Bold, C.Ink);
                Chip(c, tx + 24 + Measure(T("Могул"), Faces.Bold, 14) + 6, cy, F("спящий · −{0}%", Num(ym.DiscountPercent)), MogulInk);
                cy += 24;
                TextFit(c, F("в среднем {0} кг/мес → цель {1} кг", IncentiveTexts.Kg1(m.AvgKg), IncentiveTexts.Kg1(m.TargetKg)), tx, cy, tw, 12.5f, Faces.Medium, C.Ink2);
                cy += 19;
                TextFit(c, F("{0} кор. {1} · {2}", m.Boxes, Units(m.Box), Kg1(m.Kg)), tx, cy, tw, 14, Faces.Bold, C.Ink);
                cy += 19;
                TextFit(c, F("{0} → {1} со скидкой", Money(m.Sum), Money(m.AfterDiscount)), tx, cy, tw, 12.5f, Faces.Medium, C.Ink2);
                cy += 19;
                TextFit(c, F("+{0} тебе · {1}%", Money(m.Cashback), Num(ym.CashbackPercent)), tx, cy, tw, 13.5f, Faces.Bold, C.Green);
                cy += 19;
                if (evaluated && fact.MogulStatus is { } ms && ms != BattleStoreStatus.Ahead)
                {
                    var (text, tone) = ms switch
                    {
                        BattleStoreStatus.Taken => (F("✓ взял · кешбек +{0}", Money(fact.MogulCashback)), C.Green),
                        BattleStoreStatus.Partial => (F("½ взял {0} кор. · кешбек +{1}", fact.MogulBoxes, Money(fact.MogulCashback)), C.Amber),
                        _ => ($"× {T("отказ")}", C.Red),
                    };
                    TextFit(c, text, tx, cy, tw, 12.5f, Faces.SemiBold, tone);
                    cy += 19;
                }
            }

            if (s.Goal.Twizos is not { } t)
            {
                return;
            }

            if (s.Goal.Mogul is not null)
            {
                using var dash = new SKPaint { Color = PaperEdge, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, PathEffect = SKPathEffect.CreateDash([4, 4], 0) };
                c.DrawLine(tx, cy + 4, x + w - 12, cy + 4, dash);
                cy += 10;
            }
            else
            {
                cy += 6;
            }

            if (FarmAssets.Images.GetValueOrDefault(Packs[(int)(Math.Abs(s.MarketId) % Packs.Length)]) is { } twizos)
            {
                DrawPack(c, twizos, tx, cy - 4, 20, 26);
            }

            Text(c, "Twizos", tx + 24, cy, 14, Faces.Bold, C.Ink);
            Chip(c, tx + 24 + Measure("Twizos", Faces.Bold, 14) + 6, cy, SegmentLabel(t.Segment, t.Percent), SegmentInk(t.Segment));
            cy += 24;
            var hint = t.Segment switch
            {
                IncentiveTwizosSegment.Existing => F("обычно берёт {0} · +{1}%", Money(t.AvgInvoice ?? 0), Pct(G?.TwizosGrowth ?? 0.3m)),
                IncentiveTwizosSegment.Stand => F("ставим стойку · от {0}", Money(G?.StandCheck ?? 600_000)),
                _ => T("ещё не брал Twizos — старт"),
            };
            TextFit(c, hint, tx, cy, tw, 12.5f, Faces.Medium, C.Ink2);
            cy += 19;
            TextFit(c, "• " + SetText(t.Set), tx, cy, tw, 12.5f, Faces.Medium, C.Ink);
            cy += 19;
            var money = F("{0} · {1}", Money(t.Sum), Kg1(t.Kg));
            TextFit(c, money, tx, cy, tw - 34, 14, Faces.Bold, C.Ink);
            if (t.Gift is { } g && FarmAssets.Images.GetValueOrDefault("Gift_" + g) is { } gift)
            {
                DrawPack(c, gift, x + w - 12 - 28, cy - 3, 28, 22);
            }

            cy += 22;
            TextFit(c, F("+{0} тебе · {1}%", Money(t.Cashback), Num(t.Percent)), tx, cy, tw, 13.5f, Faces.Bold, C.Green);
            cy += 19;
            if (evaluated && fact.TwizosStatus is { } ts && ts != BattleStoreStatus.Ahead)
            {
                var (text, tone) = ts switch
                {
                    BattleStoreStatus.Taken => (F("✓ взял · {0} · кешбек +{1}", Kg1(fact.TwizosKg), Money(fact.TwizosCashback)), C.Green),
                    BattleStoreStatus.Partial => (F("½ взял {0} · {1} · кешбек +{2}", Money(fact.TwizosSum), Kg1(fact.TwizosKg), Money(fact.TwizosCashback)), C.Amber),
                    _ => ($"× {T("отказ")}", C.Red),
                };
                TextFit(c, text, tx, cy, tw, 12.5f, Faces.SemiBold, tone);
            }
        }

        // ---------- Вне маршрута ----------

        private static float OutsideHeight(IReadOnlyList<IncentiveOutOfRouteSale> sold) => 64 + (float)Math.Ceiling(Math.Min(sold.Count, 10) / 2.0) * 150 + (sold.Count > 10 ? 28 : 0) + 10;

        private void Outside(SKCanvas c, float top, float height, IReadOnlyList<IncentiveOutOfRouteSale> sold)
        {
            using (var bg = Fill(SKColor.Parse("#E3F4D6")))
            using (var edge = new SKPaint { Color = SKColor.Parse("#9CC97C"), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, PathEffect = SKPathEffect.CreateDash([10, 8], 0) })
            {
                var rect = new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + height), 24);
                c.DrawRoundRect(rect, bg);
                c.DrawRoundRect(rect, edge);
            }

            PlusIcon(c, Pad + 34, top + 32, SKColor.Parse("#3D7A2A"));
            Text(c, T("Вне маршрута — продажи по акции, засчитаны"), Pad + 58, top + 20, 20, Faces.Bold, SKColor.Parse("#3D7A2A"));
            var colW = (Inner - 40 - 20) / 2;
            for (var i = 0; i < sold.Count && i < 10; i++)
            {
                var x = Pad + 20 + (i % 2) * (colW + 20);
                var y = top + 64 + (i / 2) * 150;
                var o = sold[i];
                House(c, x + 48, y + 64, 0.62f, BattleStoreStatus.Taken, BattleStoreStatus.Taken, 0);
                var bx = x + 100;
                var bw = colW - 100;
                CardBox(c, bx, y + 4, bw, 132);
                TextFit(c, string.IsNullOrWhiteSpace(o.Name) ? F("ТТ {0}", o.MarketId) : o.Name.Trim(), bx + 12, y + 14, bw - 24, 15, Faces.Bold, C.Ink);
                var f = o.Fact ?? IncentiveGoalFact.None;
                var ly = y + 40;
                if (f.TwizosKg > 0)
                {
                    Text(c, "Twizos", bx + 12, ly, 13.5f, Faces.Bold, C.Ink);
                    if (f.TwizosSegment is { } seg)
                    {
                        var percent = seg switch
                        {
                            IncentiveTwizosSegment.Stand => G?.StandPercent ?? 10,
                            IncentiveTwizosSegment.Existing => G?.ExistingPercent ?? 5,
                            _ => G?.NewPercent ?? 8,
                        };
                        Chip(c, bx + 12 + Measure("Twizos", Faces.Bold, 13.5f) + 6, ly - 1, SegmentLabel(seg, percent), SegmentInk(seg));
                    }

                    ly += 24;
                    TextFit(c, F("{0} · {1}", Money(o.Sum), Kg1(f.TwizosKg)), bx + 12, ly, bw - 24, 14, Faces.Bold, C.Ink);
                    ly += 20;
                    TextFit(c, F("✓ взял · кешбек +{0}", Money(f.TwizosCashback)), bx + 12, ly, bw - 24, 13, Faces.SemiBold, C.Green);
                    ly += 20;
                }

                if (f.MogulCashback > 0)
                {
                    TextFit(c, F("Могул · спящий разбужен · кешбек +{0}", Money(f.MogulCashback)), bx + 12, ly, bw - 24, 13, Faces.SemiBold, MogulInk);
                }
            }

            if (sold.Count > 10)
            {
                TextFit(c, F("и ещё {0}", Points(sold.Count - 10)), Pad + 24, top + height - 34, Inner - 48, 15, Faces.SemiBold, C.Ink2);
            }
        }

        // ---------- Кешбек · баланс ----------

        private const float BalanceHeight = 64 + 34 + 2 * 46 + 12;

        private void Balance(SKCanvas c, float top, IncentiveGoalBalances b, string note, string issuedLabel, float? height = null)
        {
            CardBox(c, Pad, top, Inner, height ?? BalanceHeight);
            CashbackTitle(c, top, note, T("Кешбек · баланс"));
            BalanceRows(c, top + 64, b, issuedLabel);
        }

        private void BalanceRows(SKCanvas c, float y, IncentiveGoalBalances b, string issuedLabel)
        {
            float[] cols = [Pad + 430, Pad + 620, Pad + 810, Width - Pad - 24];
            Text(c, T("Акция"), Pad + 24, y, 13, Faces.Bold, C.Ink3);
            TextRight(c, issuedLabel, cols[0], y, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Ждёт доставки"), cols[1], y, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("К выплате"), cols[2], y, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Можно ещё"), cols[3], y, 13, Faces.Bold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            var rows = new[]
            {
                (F("Могул · {0}%", Num(P.Offers.Yamelly?.CashbackPercent ?? 10)), b.Mogul),
                (G is { } g ? F("Twizos · {0}–{1}%", Num(g.ExistingPercent), Num(g.StandPercent)) : "Twizos", b.Twizos),
            };
            var ry = y + 24;
            foreach (var (label, row) in rows)
            {
                c.DrawLine(Pad + 20, ry, Width - Pad - 20, ry, line);
                Text(c, label, Pad + 24, ry + 13, 17, Faces.Bold, C.Ink);
                TextRight(c, Money(row.Issued), cols[0], ry + 13, 17, Faces.Bold, C.Ink);
                TextRight(c, Money(row.Pending), cols[1], ry + 13, 17, Faces.Bold, C.Ink);
                TextRight(c, Money(row.ToPay), cols[2], ry + 13, 17, Faces.Bold, C.Green);
                TextRight(c, row.CanEarn is { } can ? Money(can) : "—", cols[3], ry + 13, 17, Faces.Bold, C.Orange);
                ry += 46;
            }
        }

        // ---------- Итог недели ТП ----------

        private int WeekNumber(DateOnly saturday) => (saturday.Day - 1) / 7 + 1;

        public byte[] GoalWeek(IncentiveAgentCard card, IncentiveGoalWeek week)
        {
            var strip = IsTest ? 52f : 0f;
            var head = strip + HeaderHeight + StaleHeight;
            var dayRows = new List<float>();
            var days = Enumerable.Range(0, 6).Select(i => week.Monday.AddDays(i)).ToList();
            for (var r = 0; r < 2; r++)
            {
                var max = days.Skip(r * 3).Take(3).Select(d => week.Days.FirstOrDefault(x => x.Date == d)?.Route.Stores.Count ?? 0).DefaultIfEmpty(0).Max();
                dayRows.Add(92 + Math.Max(1, (int)Math.Ceiling(max / 4.0)) * 60 + 14);
            }

            var gridHeight = 70 + dayRows.Sum() + 16 + 14;
            var weeksHeight = week.Weeks.Count == 0 ? 0 : 40 + week.Weeks.Count * 40;
            var balanceHeight = BalanceHeight + weeksHeight;
            var footer = GoalWeekFooter(week);
            using var surface = Surface(head + StatsHeight + Gap + gridHeight + Gap + balanceHeight + Gap + FooterHeight(footer) + Pad, out var c);
            Header(c, strip, T("Итог недели"), $"{AgentTitle(card.Agent)} · {DayMonth(week.Monday)}–{DayMonth(week.Saturday)}", IncentiveSlot.Week, 0, 1,
                F("Нед {0}", WeekNumber(week.Saturday)), week.Last ? T("последняя") : $"{DayMonth(week.Monday)}–{DayMonth(week.Saturday)}");
            StaleBanner(c, strip + HeaderHeight);
            var y = head;
            Stats(c, y,
                (T("Спящие Могул разбужено"), $"{week.Woken} / {week.MogulGoals}", MogulInk, F("кешбек недели {0}", Money(week.MogulWeek))),
                (T("Twizos клиентов"), $"{week.TwizosClients} / {week.TwizosGoals}", ExistingInk, F("из них новых {0} · стоек {1} · {2}", week.TwizosNew, week.Stands, Kg(week.TwizosKg))),
                (T("Кешбек недели"), Money(week.MogulWeek + week.TwizosWeek), C.Green, F("Могул {0} + Twizos {1}", Money(week.MogulWeek), Money(week.TwizosWeek))));
            y += StatsHeight + Gap;

            CardBox(c, Pad, y, Inner, gridHeight);
            Text(c, T("6 дней — 6 полян"), Pad + 24, y + 18, 22, Faces.Bold, C.Ink);
            TextRight(c, T("✓ взял · ½ меньше цели · × отказ · серые — без акции"), Width - Pad - 24, y + 24, 14, Faces.SemiBold, C.Ink2);
            var tileW = (Inner - 40 - 2 * 14) / 3;
            var ty = y + 70;
            for (var r = 0; r < 2; r++)
            {
                for (var i = 0; i < 3; i++)
                {
                    var date = days[r * 3 + i];
                    GoalWeekTile(c, Pad + 20 + i * (tileW + 14), ty, tileW, dayRows[r] - 14, date, week.Days.FirstOrDefault(x => x.Date == date));
                }

                ty += dayRows[r];
            }

            y += gridHeight + Gap;
            Balance(c, y, week.Balances, F("доставка подтверждена → выплата пн–вт {0}", DayMonth(week.Monday.AddDays(7))), T("Оформлено"), balanceHeight);
            if (week.Weeks.Count > 0)
            {
                var wy = y + BalanceHeight - 6;
                Text(c, T("Неделя"), Pad + 24, wy, 13, Faces.Bold, C.Ink3);
                TextRight(c, T("Могул"), Pad + 480, wy, 13, Faces.Bold, C.Ink3);
                TextRight(c, "Twizos", Pad + 700, wy, 13, Faces.Bold, C.Ink3);
                TextRight(c, T("Выплачено"), Width - Pad - 24, wy, 13, Faces.Bold, C.Ink3);
                using var line = Stroke(C.Border, 1.5f);
                var ry = wy + 24;
                foreach (var row in week.Weeks)
                {
                    c.DrawLine(Pad + 20, ry, Width - Pad - 20, ry, line);
                    Text(c, $"{DayMonth(row.Monday)}–{DayMonth(row.Sunday)}", Pad + 24, ry + 10, 16, Faces.Medium, C.Ink);
                    TextRight(c, row.Mogul > 0 ? Money(row.Mogul) : "—", Pad + 480, ry + 10, 16, Faces.Bold, C.Ink);
                    TextRight(c, row.Twizos > 0 ? Money(row.Twizos) : "—", Pad + 700, ry + 10, 16, Faces.Bold, C.Ink);
                    var (text, tone) = row.Left <= 0 && row.Paid > 0 ? ($"{Money(row.Paid)} ✓", C.Green) : (F("к выплате {0}", Money(row.Left)), C.Orange);
                    TextRight(c, text, Width - Pad - 24, ry + 10, 16, Faces.Bold, tone);
                    ry += 40;
                }
            }

            y += balanceHeight + Gap;
            PromoFooter(c, y, footer);
            return Png(surface);
        }

        private void GoalWeekTile(SKCanvas c, float x, float y, float w, float h, DateOnly date, IncentiveGoalWeekDay? day)
        {
            using (var bg = Fill(day is null ? Paper : C.Map))
            using (var edge = Stroke(day is null ? PaperEdge : C.MapBorder, 2.5f))
            {
                var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 16);
                c.DrawRoundRect(rect, bg);
                c.DrawRoundRect(rect, edge);
            }

            Text(c, Weekday(date), x + 14, y + 10, 22, Faces.Bold, C.Ink);
            if (day is null)
            {
                TextFit(c, T("маршрута нет"), x + 14, y + 44, w - 28, 14, Faces.Medium, C.Ink3);
                return;
            }

            var t = day.Totals;
            var share = t.Share;
            TextRight(c, $"{Pct(share)}%", x + w - 14, y + 12, 20, Faces.Bold, share >= 0.6m ? C.Green : share >= 0.4m ? C.Orange : C.Red);
            TextFit(c, F("{0} ТТ · Могул {1}/{2} · Twizos {3}/{4}", t.Stores, t.Woken, t.MogulGoals, t.TwizosClients, t.TwizosGoals), x + 14, y + 42, w - 28, 13,
                Faces.SemiBold, C.Ink2);
            var stores = day.Route.Stores;
            var cellW = (w - 20) / 4;
            for (var i = 0; i < stores.Count; i++)
            {
                var cx = x + 10 + (i % 4) * cellW + cellW / 2;
                var cy = y + 92 + (i / 4) * 60 + 20;
                var status = GoalStopStatus(stores[i]);
                House(c, cx, cy, 0.5f, status, status == PlainStatus || status == BattleStoreStatus.Ahead ? null : status, 0);
            }
        }

        private string GoalWeekFooter(IncentiveGoalWeek week)
        {
            var m = week.Balances.Mogul.ToPay;
            var t = week.Balances.Twizos.ToPay;
            var first = F("К выплате пн–вт: Могул {0} + Twizos {1} = {2} сум", Money(m), Money(t), Money(m + t));
            var second = week.Last && G is { } g
                ? F("{0} — последний день акций: {1} в списке ещё ждут", DayMonth(g.End), F("спящих Могул {0}", week.SleepingLeft))
                : F("на следующей неделе: {0} в списке ещё ждут", F("спящих Могул {0}", week.SleepingLeft));
            return first + "\n" + second;
        }

        // ---------- Итог месяца ТП ----------

        public byte[] GoalMonth(IncentiveAgentCard card, IncentiveGoalMonth month)
        {
            var strip = IsTest ? 52f : 0f;
            var head = strip + HeaderHeight + StaleHeight;
            var weeksHeight = 64 + Math.Max(1, month.MogulWeeks.Count) * 44 + 12;
            var earnRows = 2 + (month.Best is not null ? 1 : 0) + 1;
            var earnHeight = 64 + 32 + earnRows * 44 + 12;
            var footer = GoalMonthFooter(month);
            using var surface = Surface(head + StatsHeight + Gap + weeksHeight + Gap + earnHeight + Gap + FooterHeight(footer) + Pad, out var c);
            var monthName = IncentiveTexts.MonthName(cards.Date.Month, Language);
            var start = P.Offers.Yamelly?.Start ?? new DateOnly(cards.Date.Year, cards.Date.Month, 1);
            Header(c, strip, T("Итог месяца"), $"{AgentTitle(card.Agent)} · {monthName}", IncentiveSlot.Month, 0, 1, Capital(monthName),
                $"{DayMonth(start)}–{DayMonth(cards.Date)}");
            StaleBanner(c, strip + HeaderHeight);
            var y = head;
            var share = month.Listed == 0 ? 0 : (decimal)month.Woken / month.Listed;
            var accrued = month.Mogul.Accrued + month.Twizos.Accrued + (month.Best?.Accrued ?? 0);
            Stats(c, y,
                (T("Спящих Могул разбужено"), $"{month.Woken} / {month.Listed}", MogulInk,
                    F("{0}% списка · цель {1}%", Pct(share), Pct(month.WakeShare)) + (share >= month.WakeShare && month.Listed > 0 ? " ✓" : "")),
                (F("Twizos с {0}", DayMonth(month.TwizosFrom)), Clients(month.TwizosClients), ExistingInk,
                    F("новых {0} · стоек {1} · {2}", month.TwizosNew, month.Stands, Kg(month.TwizosKg))),
                (F("Доп. заработок за {0}", monthName), Money(accrued), C.Green, month.Best is not null ? T("кешбеки + лучший агент дня") : T("кешбек Могул + Twizos")));
            y += StatsHeight + Gap;

            CardBox(c, Pad, y, Inner, weeksHeight);
            Text(c, T("Могул по неделям"), Pad + 24, y + 18, 22, Faces.Bold, C.Ink);
            TextRight(c, T("разбужено спящих · кешбек"), Width - Pad - 24, y + 24, 15, Faces.SemiBold, C.Ink2);
            var max = Math.Max(1, month.MogulWeeks.Select(w => w.Woken).DefaultIfEmpty(0).Max());
            var wy = y + 64;
            const float barX = Pad + 170, barW = 520;
            foreach (var w in month.MogulWeeks)
            {
                Text(c, w.From == w.To ? DayMonth(w.From) : $"{DayMonth(w.From)}–{DayMonth(w.To)}", Pad + 24, wy + 8, 17, Faces.Bold, C.Ink);
                using (var track = Fill(C.Track))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, wy + 10, barX + barW, wy + 30), 10), track);
                }

                if (w.Woken > 0)
                {
                    using var fill = Fill(MogulInk);
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, wy + 10, barX + Math.Max(20, barW * w.Woken / max), wy + 30), 10), fill);
                }

                TextRight(c, F("{0} · {1}", Points(w.Woken), Money(w.Cashback)), Width - Pad - 24, wy + 8, 17, Faces.Bold, C.Ink);
                wy += 44;
            }

            y += weeksHeight + Gap;
            CardBox(c, Pad, y, Inner, earnHeight);
            Text(c, F("Мой заработок за {0}", monthName), Pad + 24, y + 18, 22, Faces.Bold, C.Ink);
            TextRight(c, T("начислено · выплачено · остаток"), Width - Pad - 24, y + 24, 15, Faces.SemiBold, C.Ink2);
            var hy = y + 64;
            Text(c, T("Акция"), Pad + 24, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Начислено"), Pad + 640, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Выплачено"), Pad + 830, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Остаток"), Width - Pad - 24, hy, 13, Faces.Bold, C.Ink3);
            var rows = new List<(string Label, IncentiveGoalEarn Earn)>
            {
                (F("Могул · кешбек {0}% · {1}", Num(P.Offers.Yamelly?.CashbackPercent ?? 10), Points(month.Woken)), month.Mogul),
                (F("Twizos · кешбек {0}–{1}% · {2}", Num(G?.ExistingPercent ?? 5), Num(G?.StandPercent ?? 10), Clients(month.TwizosClients)), month.Twizos),
            };
            if (month.Best is { } best)
            {
                rows.Add((month.BestUntil is { } until ? F("Лучший агент дня · по {0} · {1}", DayMonth(until), Wins(month.BestWins)) : F("Лучший агент дня · {0}", Wins(month.BestWins)), best));
            }

            using var line = Stroke(C.Border, 1.5f);
            var ry = hy + 24;
            foreach (var (label, earn) in rows)
            {
                c.DrawLine(Pad + 20, ry, Width - Pad - 20, ry, line);
                TextFit(c, label, Pad + 24, ry + 12, 440, 16, Faces.Medium, C.Ink);
                TextRight(c, Money(earn.Accrued), Pad + 640, ry + 12, 16, Faces.Bold, C.Ink);
                TextRight(c, Money(earn.Paid), Pad + 830, ry + 12, 16, Faces.Bold, C.Ink);
                TextRight(c, Money(earn.Left), Width - Pad - 24, ry + 12, 16, Faces.Bold, C.Ink);
                ry += 44;
            }

            c.DrawLine(Pad + 20, ry, Width - Pad - 20, ry, line);
            var paid = rows.Sum(r => r.Earn.Paid);
            var left = rows.Sum(r => r.Earn.Left);
            Text(c, T("Итого"), Pad + 24, ry + 12, 16, Faces.Bold, C.Ink);
            TextRight(c, Money(rows.Sum(r => r.Earn.Accrued)), Pad + 640, ry + 12, 16, Faces.Bold, C.Ink);
            TextRight(c, Money(paid), Pad + 830, ry + 12, 16, Faces.Bold, C.Ink);
            TextRight(c, Money(left), Width - Pad - 24, ry + 12, 16, Faces.Bold, C.Green);
            y += earnHeight + Gap;
            PromoFooter(c, y, footer);
            return Png(surface);
        }

        private string GoalMonthFooter(IncentiveGoalMonth month)
        {
            var left = month.Mogul.Left + month.Twizos.Left + (month.Best?.Left ?? 0);
            return F("Остаток {0} сум — выплата в пн–вт после доставки", Money(left)) + "\n" + T("Спасибо за работу в акциях месяца!");
        }

        // ---------- Итог дня команды СВР ----------

        public byte[] GoalTeam(IncentiveGoalTeam team)
        {
            var strip = IsTest ? 52f : 0f;
            var head = strip + HeaderHeight + StaleHeight;
            var rows = team.Rows;
            var tableHeight = 60 + 34 + Math.Max(1, rows.Count) * 52 + 12;
            var stockHeight = 60 + 34 + team.Stock.Count * 44 + 12;
            var footer = GoalTeamFooter(team);
            using var surface = Surface(head + StatsHeight + Gap + tableHeight + Gap + stockHeight + Gap + BalanceHeight + Gap + FooterHeight(footer) + Pad, out var c);
            Header(c, strip, T("Итог дня команды"), $"{team.Title} · {F("{0} ТП", rows.Count)} · {Weekday(cards.Date)} {DayMonth(cards.Date)}", IncentiveSlot.Team, 0, 1,
                DayMonth(cards.Date), T("итог дня"));
            StaleBanner(c, strip + HeaderHeight);
            var y = head;
            var woken = rows.Sum(r => r.Totals.Woken);
            var goalsM = rows.Sum(r => r.Totals.MogulGoals);
            var clients = rows.Sum(r => r.Totals.TwizosClients);
            var goalsT = rows.Sum(r => r.Totals.TwizosGoals);
            var best = rows.FirstOrDefault(r => r.Totals.Woken + r.Totals.TwizosClients > 0);
            Stats(c, y,
                (T("Спящие Могул разбужено"), $"{woken} / {goalsM}", MogulInk, F("кешбек команды {0}", Money(rows.Sum(r => r.Totals.MogulCashback)))),
                (T("Twizos клиентов"), $"{clients} / {goalsT}", ExistingInk, F("новых {0} · стоек {1} · кешбек {2}", rows.Sum(r => r.Totals.TwizosNew),
                    rows.Sum(r => r.Totals.Stands), Money(rows.Sum(r => r.Totals.TwizosCashback)))),
                best is null
                    ? (T("Лучший сегодня"), "—", C.Ink3, T("пока без результата"))
                    : (T("Лучший сегодня"), ShortName(Person(best.Agent.Name)), C.Green, F("{0} спящих · {1} клиентов Twizos", best.Totals.Woken, best.Totals.TwizosClients)));
            y += StatsHeight + Gap;

            CardBox(c, Pad, y, Inner, tableHeight);
            Text(c, T("Команда сегодня"), Pad + 24, y + 18, 22, Faces.Bold, C.Ink);
            TextRight(c, T("сортировка по результату"), Width - Pad - 24, y + 24, 15, Faces.SemiBold, C.Ink2);
            var hy = y + 64;
            const float sleepX = Pad + 470, barX = Pad + 500, barW = 170, mogulX = Pad + 830;
            Text(c, T("ТП"), Pad + 24, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Спящие"), sleepX, hy, 13, Faces.Bold, C.Ink3);
            Text(c, "Twizos", barX, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Кешбек Могул"), mogulX, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Кешбек Twizos"), Width - Pad - 24, hy, 13, Faces.Bold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            var ry = hy + 24;
            if (rows.Count == 0)
            {
                c.DrawLine(Pad + 20, ry, Width - Pad - 20, ry, line);
                Text(c, T("В команде нет ТП акции"), Pad + 24, ry + 14, 17, Faces.Medium, C.Ink3);
            }

            foreach (var row in rows)
            {
                var t = row.Totals;
                c.DrawLine(Pad + 20, ry, Width - Pad - 20, ry, line);
                StatusDot(c, Pad + 36, ry + 26, row.Status);
                TextFit(c, Person(row.Agent.Name), Pad + 58, ry + 15, sleepX - Pad - 120, 17, Faces.Medium, C.Ink);
                TextRight(c, $"{t.Woken}/{t.MogulGoals}", sleepX, ry + 15, 17, Faces.Bold, MogulInk);
                using (var track = Fill(C.Track))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, ry + 12, barX + barW, ry + 22), 5), track);
                }

                var share = t.TwizosGoals > 0 ? (float)Math.Min(1, (decimal)t.TwizosClients / t.TwizosGoals) : t.TwizosClients > 0 ? 1f : 0f;
                if (share > 0)
                {
                    using var fill = Fill(C.Orange);
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, ry + 12, barX + Math.Max(10, barW * share), ry + 22), 5), fill);
                }

                Text(c, F("{0} из {1} клиентов", t.TwizosClients, t.TwizosGoals), barX, ry + 28, 12.5f, Faces.Medium, C.Ink3);
                TextRight(c, Money(t.MogulCashback), mogulX, ry + 15, 17, Faces.Bold, C.Ink);
                TextRight(c, Money(t.TwizosCashback), Width - Pad - 24, ry + 15, 17, Faces.Bold, t.TwizosCashback > 0 ? C.Green : C.Ink3);
                ry += 52;
            }

            y += tableHeight + Gap;
            CardBox(c, Pad, y, Inner, stockHeight);
            Text(c, T("Подарки и стойки Twizos"), Pad + 24, y + 18, 22, Faces.Bold, C.Ink);
            TextRight(c, T("выдано сегодня · остаток у дилера"), Width - Pad - 24, y + 24, 15, Faces.SemiBold, C.Ink2);
            var sy = y + 64;
            Text(c, T("Подарок"), Pad + 24, sy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Выдано"), Pad + 760, sy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Остаток"), Width - Pad - 24, sy, 13, Faces.Bold, C.Ink3);
            sy += 24;
            foreach (var item in team.Stock)
            {
                c.DrawLine(Pad + 20, sy, Width - Pad - 20, sy, line);
                var label = item.Item == "stand"
                    ? item.Target is { } target ? F("Стойка Twizos · цель региона {0}", target) : T("Стойка Twizos")
                    : Capital(GiftName(item.Item));
                Text(c, label, Pad + 24, sy + 12, 17, Faces.Medium, C.Ink);
                TextRight(c, item.Issued.ToString(CultureInfo.InvariantCulture), Pad + 760, sy + 12, 17, Faces.Bold, C.Ink);
                TextRight(c, item.Left is { } left ? left.ToString(CultureInfo.InvariantCulture) : "—", Width - Pad - 24, sy + 12, 17, Faces.Bold, C.Ink);
                sy += 44;
            }

            y += stockHeight + Gap;
            Balance(c, y, team.Balances, T("команда · оформлено сегодня"), T("Оформлено"));
            y += BalanceHeight + Gap;
            PromoFooter(c, y, footer);
            return Png(surface);
        }

        private string GoalTeamFooter(IncentiveGoalTeam team)
        {
            // Отстающие — ×, кроме лучшего дня (он в плитке «Лучший сегодня»), слабейшие первыми.
            var best = team.Rows.FirstOrDefault(r => r.Totals.Woken + r.Totals.TwizosClients > 0);
            var weak = team.Rows.Where(r => r.Status == BattleStoreStatus.Refused && r != best)
                .OrderBy(r => r.Totals.Woken + r.Totals.TwizosClients).ThenBy(r => r.Totals.Cashback).ToList();
            if (weak.Count == 0)
            {
                var all = team.Rows.Sum(r => r.Totals.MogulGoals + r.Totals.TwizosGoals);
                var done = team.Rows.Sum(r => r.Totals.Woken + r.Totals.TwizosClients);
                return F("Команда взяла {0}% целей дня · кешбек {1} сум", all == 0 ? "0" : Pct(Math.Min(1, (decimal)done / all)), Money(team.Rows.Sum(r => r.Totals.Cashback)))
                       + "\n" + T("отказы по Twizos — разобрать возражения на планёрке");
            }

            var names = string.Join(", ", weak.Take(3).Select(r => ShortName(Person(r.Agent.Name))));
            return F("{0} — {1} спящих и {2} клиентов Twizos", names, weak.Sum(r => r.Totals.Woken), weak.Sum(r => r.Totals.TwizosClients))
                   + "\n" + F("завтра: выйти в поле с {0} · отказы по Twizos — разобрать возражения", ShortName(Person(weak[0].Agent.Name)));
        }

        // ---------- Итог недели зоны РМ ----------

        public byte[] GoalZone(IncentiveGoalZone zone)
        {
            var strip = IsTest ? 52f : 0f;
            var head = strip + HeaderHeight + StaleHeight;
            var regions = zone.Regions;
            var tableHeight = 60 + 34 + Math.Max(1, regions.Count) * 50 + 12;
            var footer = GoalZoneFooter(zone);
            using var surface = Surface(head + StatsHeight + Gap + tableHeight + Gap + BalanceHeight + Gap + FooterHeight(footer) + Pad, out var c);
            var subtitle = $"{zone.Title} · {string.Join(" · ", regions.Take(6).Select(r => r.Name))}{(regions.Count > 6 ? " …" : "")} · {DayMonth(zone.From)}–{DayMonth(zone.To)}";
            Header(c, strip, T("Итог недели зоны"), subtitle, IncentiveSlot.Zone, 0, 1, F("Нед {0}", WeekNumber(zone.To)), zone.Last ? T("последняя") : DayMonth(zone.To));
            StaleBanner(c, strip + HeaderHeight);
            var y = head;
            var listed = regions.Sum(r => r.Listed);
            var woken = regions.Sum(r => r.Woken);
            var toPayM = zone.Balances.Mogul.ToPay;
            var toPayT = zone.Balances.Twizos.ToPay;
            Stats(c, y,
                (T("Спящие Могул разбужено"), Money(woken), MogulInk, F("{0}% списка зоны · цель {1}%", Pct(listed == 0 ? 0 : (decimal)woken / listed), Pct(zone.WakeShare))),
                (T("Twizos клиентов"), Money(regions.Sum(r => r.TwizosClients)), ExistingInk, F("новых {0} · стоек {1}/{2} · {3}", regions.Sum(r => r.TwizosNew),
                    regions.Sum(r => r.StandsTotal), regions.Sum(r => r.StandsWeek), Kg(regions.Sum(r => r.TwizosKg)))),
                (T("К выплате ТП"), Mln(toPayM + toPayT), C.Green, F("Могул {0} + Twizos {1}", Mln(toPayM), Mln(toPayT))));
            y += StatsHeight + Gap;

            CardBox(c, Pad, y, Inner, tableHeight);
            Text(c, T("Регионы"), Pad + 24, y + 18, 22, Faces.Bold, C.Ink);
            TextRight(c, T("стойки: всего / новые за неделю · остаток подарков у дилеров"), Width - Pad - 24, y + 24, 14, Faces.SemiBold, C.Ink2);
            var hy = y + 64;
            const float barX = Pad + 190, barW = 170, clientsX = Pad + 560, standsX = Pad + 680, giftsX = Pad + 790;
            Text(c, T("Регион"), Pad + 24, hy, 13, Faces.Bold, C.Ink3);
            Text(c, T("Спящие Могул"), barX, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Twizos клиентов"), clientsX, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Стойки"), standsX, hy, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Подарков"), giftsX, hy, 13, Faces.Bold, C.Ink3);
            Text(c, T("Остаток"), giftsX + 18, hy, 13, Faces.Bold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            var ry = hy + 24;
            foreach (var r in regions)
            {
                c.DrawLine(Pad + 20, ry, Width - Pad - 20, ry, line);
                TextFit(c, r.Name, Pad + 24, ry + 14, 150, 17, Faces.Bold, C.Ink);
                using (var track = Fill(C.Track))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, ry + 12, barX + barW, ry + 22), 5), track);
                }

                if (r.WokenShare > 0)
                {
                    using var fill = Fill(MogulInk);
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, ry + 12, barX + Math.Max(10, barW * (float)Math.Min(1, r.WokenShare)), ry + 22), 5), fill);
                }

                Text(c, F("{0}% · {1}", Pct(r.WokenShare), Points(r.Woken)), barX, ry + 28, 12.5f, Faces.Medium, C.Ink3);
                TextRight(c, Money(r.TwizosClients), clientsX, ry + 14, 17, Faces.Bold, C.Ink);
                TextRight(c, $"{r.StandsTotal} / {r.StandsWeek}", standsX, ry + 14, 17, Faces.Bold, C.Ink);
                TextRight(c, Money(r.Gifts), giftsX, ry + 14, 17, Faces.Bold, C.Ink);
                var stock = (P.Offers.Twizos?.GiftTiers ?? []).Select(g => $"{Initial(GiftName(g.Gift))} {r.GiftStock.GetValueOrDefault(g.Gift)}");
                TextFit(c, string.Join(" · ", stock), giftsX + 18, ry + 16, Width - Pad - 24 - giftsX - 18, 13, Faces.Medium, C.Ink2);
                ry += 50;
            }

            y += tableHeight + Gap;
            Balance(c, y, zone.Balances, F("неделя {0}–{1} · выплата пн–вт", DayMonth(zone.From), DayMonth(zone.To)), T("Оформлено"));
            y += BalanceHeight + Gap;
            PromoFooter(c, y, footer);
            return Png(surface);
        }

        private string GoalZoneFooter(IncentiveGoalZone zone)
        {
            var weak = zone.Regions.Where(r => r.Listed > 0).OrderBy(r => r.WokenShare).ThenBy(r => r.TwizosNew).FirstOrDefault();
            var first = weak is null
                ? F("Зона: {0} клиентов Twizos за неделю", zone.Regions.Sum(r => r.TwizosClients))
                : F("{0}: спящие {1}% и {2} новых клиентов Twizos — разбор с СВР", weak.Name, Pct(weak.WokenShare), weak.TwizosNew);
            var low = zone.Regions
                .SelectMany(r => (P.Offers.Twizos?.GiftTiers ?? []).Select(g => (Region: r.Name, Gift: g.Gift, Left: r.GiftStock.GetValueOrDefault(g.Gift))))
                .Where(x => x.Left < 10)
                .OrderBy(x => x.Left)
                .FirstOrDefault();
            var second = low.Region is null ? T("подарков на складах дилеров хватает") : F("подарки «{0}» в {1} заканчиваются ({2}) — дозаказ", GiftName(low.Gift), low.Region, low.Left);
            return first + "\n" + second;
        }
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Incentive;
using SkiaSharp;
using C = OneBase.Infrastructure.Telegram.Reports.FarmColors;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Карточки ТП с акциями ноября (образцы «Карточки ТК», ТЗ раздел 5): те же фигуры «Поля боя», новые блоки. 08:30 — плитки «Накоплено · Спящие
/// Yamelly на маршруте · Twizos — цели дня», дорога к призам с фото и «ещё N кг», «Что предлагаем клиенту» (подарки, Yamelly −7%, пары, ТОП SKU),
/// «Как собраны цели», все точки маршрута (без цели — серые) с выносками по предложениям, «Добери до нормы». 13:00 — продажи вне маршрута и
/// итоги точек. 18:00 — итоги, кэшбек YM-02 и «На мушке». Неделя — 6 полян с процентами, кэшбек по неделям. Месяц — месяцы накопительной и
/// «Мой заработок». Картинка не выше 2 560 px (Telegram ужимает фото по длинной стороне): длинный маршрут продолжается следующей картинкой.
/// </summary>
internal static partial class IncentiveCardRenderer
{
    private sealed partial class Painter
    {
        private const float MaxPageHeight = 2560;
        /// <summary>Ширина выноски: слева от домика остаётся место под значок с номером (он выступает за домик на 19 px).</summary>
        private const float CalloutWidth = 230;
        private const float ContinueFooter = 92;
        private const string PlainStatus = FarmColors.PlainStatus;

        private static readonly SKColor Cream = SKColor.Parse("#FFF4E2");
        private static readonly SKColor Rose = SKColor.Parse("#FDE6EF");
        private static readonly SKColor Mint = SKColor.Parse("#EEF8EA");
        private static readonly SKColor MintEdge = SKColor.Parse("#2E9E4F");
        private static readonly SKColor Butter = SKColor.Parse("#FFF4D0");
        private static readonly SKColor Refused = SKColor.Parse("#FDECEC");
        private static readonly SKColor Paper = SKColor.Parse("#F4EFE7");
        private static readonly SKColor PaperEdge = SKColor.Parse("#CBBFAE");

        private static readonly string[] YamellyPacks =
            ["Mogul_Yamelly_Arbuz_420g", "Mogul_Yamelly_Sliva_420g", "Mogul_Yamelly_Tropik_420g", "Mogul_Yamelly_Vesna_420g", "Mogul_Yamelly_Yagoda_420g", "Mogul_Yamelly_Zima_420g"];

        private IncentivePromos O => P.Offers;

        // ---------- Карточка дня ----------

        private sealed record Block(float Height, Action<SKCanvas, float> Draw);

        public IEnumerable<byte[]> PromoDay(IncentiveAgentCard card, bool compact)
        {
            var slot = cards.Slot;
            var route = card.Route;
            var stores = route?.Stores ?? [];
            var evaluated = slot != IncentiveSlot.Morning;
            var strip = IsTest ? 52f : 0f;
            var head = strip + HeaderHeight + StaleHeight;

            var top = new List<Block> { new(StatsHeight, (c, y) => PromoDayTiles(c, y, card)) };
            if (slot == IncentiveSlot.Morning)
            {
                top.Add(new Block(RoadHeight, (c, y) => PromoRoad(c, y, card, card.Progress.Next is { } next
                    ? F("накоплено {0} · {1} {2}", Kg(card.Total), To(next), Kg(card.Progress.ToNext ?? 0))
                    : F("накоплено {0}", Kg(card.Total)))));
                if (!compact && O.Twizos is not null)
                {
                    var offers = OffersHeight(card);
                    top.Add(new Block(offers, (c, y) => Offers(c, y, offers, card)));
                }

                if (!compact && card.Promo?.Goals is { } goals)
                {
                    var height = GoalsHeight(goals);
                    top.Add(new Block(height, (c, y) => Goals(c, y, height, goals)));
                }
            }
            else if (slot == IncentiveSlot.Evening)
            {
                top.Add(new Block(RoadHeight, (c, y) => PromoRoad(c, y, card, F("накоплено {0} · +{1} сегодня", Kg(card.Total), Kg(card.Today)))));
            }
            else if (route?.OutOfRoute is { Count: > 0 } sold)
            {
                var height = SoldHeight(sold);
                top.Add(new Block(height, (c, y) => OutOfRoute(c, y, height, sold)));
            }

            var bottom = new List<Block>();
            if ((slot == IncentiveSlot.Evening ? route?.Extra?.Where(x => x.Status != BattleStoreStatus.Ahead).ToList() : route?.Extra) is { Count: > 0 } extra)
            {
                var height = ExtraHeight(extra, evaluated);
                bottom.Add(new Block(height, (c, y) => Extra(c, y, height, extra, card, evaluated)));
            }

            if (slot == IncentiveSlot.Evening && route?.OutOfRoute is { Count: > 0 } late)
            {
                var height = SoldHeight(late);
                bottom.Add(new Block(height, (c, y) => OutOfRoute(c, y, height, late)));
            }

            if (slot == IncentiveSlot.Evening && card.Promo?.Cashback is { } balance)
            {
                bottom.Add(new Block(176, (c, y) => CashbackDay(c, y, card, balance)));
            }

            var footer = PromoDayFooter(card);
            var footerHeight = FooterHeight(footer);
            var rows = new List<float>();
            for (var i = 0; i < stores.Count; i += 2)
            {
                var h = Math.Max(CalloutHeight(stores[i], evaluated), i + 1 < stores.Count ? CalloutHeight(stores[i + 1], evaluated) : 0);
                rows.Add(Math.Max(150, h + 26));
            }

            // Раскладка по картинкам: первая — плитки и блоки над полем, последняя — блоки под полем и итог.
            var topHeight = top.Sum(b => b.Height + Gap);
            var bottomHeight = bottom.Sum(b => b.Height + Gap);
            const float mapChrome = 88 + Gap;
            var emptyHeight = stores.Count == 0 ? 140 + Gap : 0;
            var pages = new List<(int From, int To)>();
            if (P.OnePhoto)
            {
                // «Одной картинкой» (настройка акции): весь маршрут и все блоки — на одной, без продолжения.
                pages.Add((0, rows.Count));
            }

            for (var r = 0; !P.OnePhoto && pages.Count < 8;)
            {
                var budget = MaxPageHeight - head - Pad - (pages.Count == 0 ? topHeight + emptyHeight : 0);
                var rest = rows.Skip(r).Sum();
                if ((r < rows.Count ? mapChrome + rest : 0) + bottomHeight + footerHeight <= budget || r >= rows.Count)
                {
                    pages.Add((r, rows.Count));
                    break;
                }

                var used = mapChrome + ContinueFooter;
                var n = 0;
                while (r + n < rows.Count && used + rows[r + n] <= budget)
                {
                    used += rows[r + n];
                    n++;
                }

                n = Math.Max(1, n);
                pages.Add((r, r + n));
                r += n;
            }

            if (pages[^1].To < rows.Count)
            {
                pages[^1] = (pages[^1].From, rows.Count);
            }

            var title = slot switch { IncentiveSlot.Midday => T("Полдень"), IncentiveSlot.Afternoon => T("Итоги 16:00"), IncentiveSlot.Evening => T("Итог дня"), _ => T("Маршрут дня") };
            var subtitle = $"{AgentTitle(card.Agent)} · {Weekday(cards.Date)} {DayMonth(cards.Date)}";
            var statuses = stores.Select(StopStatus).ToList();
            for (var p = 0; p < pages.Count; p++)
            {
                var (from, to) = pages[p];
                var first = p == 0;
                var last = p == pages.Count - 1;
                var pageRows = rows.Skip(from).Take(to - from).ToList();
                var mapHeight = pageRows.Count > 0 ? 88 + pageRows.Sum() : 0;
                var height = head + (first ? topHeight + emptyHeight : 0) + (mapHeight > 0 ? mapHeight + Gap : 0) + (last ? bottomHeight + footerHeight : ContinueFooter) + Pad;
                using var surface = Surface(height, out var c);
                Header(c, strip, title, subtitle, slot, p, pages.Count);
                StaleBanner(c, strip + HeaderHeight);
                var y = head;
                if (first)
                {
                    foreach (var block in top)
                    {
                        block.Draw(c, y);
                        y += block.Height + Gap;
                    }

                    if (stores.Count == 0)
                    {
                        using var bg = Fill(C.Map);
                        using var edge = Stroke(C.MapBorder, 3);
                        var rect = new SKRoundRect(new SKRect(Pad, y, Width - Pad, y + 140), 24);
                        c.DrawRoundRect(rect, bg);
                        c.DrawRoundRect(rect, edge);
                        TextCenter(c, T("Маршрута на этот день нет: в Linko нет плана визитов и точек ТП"), Width / 2, y + 56, 20, Faces.SemiBold, C.Ink2);
                        y += 140 + Gap;
                    }
                }

                if (mapHeight > 0)
                {
                    var page = stores.Skip(from * 2).Take((to - from) * 2).ToList();
                    Farm(c, y, mapHeight, page.Select(s => new FarmStop(StopStatus(s), s.Order)).ToList(), pageRows, slot, from * 2, (i, x, cy) =>
                    {
                        var h = CalloutHeight(page[i], evaluated);
                        var cx = x < Width / 2 ? Pad + 12 : Width - Pad - 12 - CalloutWidth;
                        PromoCallout(c, cx, cy - h / 2 + 4, page[i], evaluated);
                    }, stores.Count, HeroOnPage(slot, statuses, from * 2, page.Count));
                    y += mapHeight + Gap;
                }

                if (last)
                {
                    foreach (var block in bottom)
                    {
                        block.Draw(c, y);
                        y += block.Height + Gap;
                    }

                    PromoFooter(c, y, footer);
                }
                else
                {
                    Footer(c, y, F("Продолжение маршрута — на следующей картинке ({0} из {1})", p + 2, pages.Count));
                }

                yield return Png(surface);
            }
        }

        /// <summary>Домик точки: без цели и без итога — серый.</summary>
        private static string StopStatus(IncentiveStore s) => s.Plain && s.Status == BattleStoreStatus.Ahead ? PlainStatus : s.Status;

        private void PromoDayTiles(SKCanvas c, float top, IncentiveAgentCard card)
        {
            var promo = card.Promo!;
            var route = card.Route;
            var goalsKg = route?.TargetKg ?? 0;
            var norm = card.DayStart.Norm;
            var target = card.DayStart.NormTier;
            var normTo = target is not null && norm is { } n ? F("норма {0} {1}", To(target), Kg(n)) : "";
            var accumulated = (F("Накоплено с {0}", StartText), Kg(card.Total), C.Orange, (string?)IncentiveTexts.MonthsLine(P, card.Standing, cards.Date, Language));
            var planned = route?.SleepingPlanned ?? 0;
            var woken = route?.SleepingWoken ?? 0;
            var ym = O.Yamelly;
            (string, string, SKColor, string?) Sleeping(string label, string sub) => (label, $"{woken} / {planned}", C.Pink, sub);
            (string, string, SKColor, string?)? sleeping = null;
            switch (cards.Slot)
            {
                case IncentiveSlot.Morning:
                {
                    if (ym is not null && promo.Sleeping is { } sv)
                    {
                        sleeping = Sleeping(T("Спящие Yamelly на маршруте"), planned > 0
                            ? F("кэшбек до {0} сум", Money(planned * ym.MinCashback))
                            : F("в списке {0} · разбужено {1}", sv.Listed, sv.Woken));
                    }

                    var reserve = O.Twizos is { } tw ? Math.Round((tw.Reserve - 1) * 100) : 0;
                    var goalsSub = normTo.Length > 0 && reserve > 0 ? F("{0} + запас {1}% на отказы", normTo, reserve) : normTo;
                    var goals = (F("{0} — цели дня", P.Product), $"{IncentiveTexts.Kg(card.Today)} / {Kg(goalsKg)}", C.Green, (string?)goalsSub);
                    Stats(c, top, accumulated, sleeping ?? ToPrize(card), goals);
                    return;
                }

                case IncentiveSlot.Midday or IncentiveSlot.Afternoon:
                {
                    if (ym is not null)
                    {
                        sleeping = Sleeping(T("Спящие Yamelly"), promo.CashbackToday > 0
                            ? F("кэшбек +{0} · ждёт доставки", Money(promo.CashbackToday))
                            : F("кэшбек до {0} сум", Money(planned * ym.MinCashback)));
                    }

                    var today = (F("{0} сегодня", P.Product), goalsKg > 0 ? $"{IncentiveTexts.Kg(card.Today)} / {Kg(goalsKg)}" : Kg(card.Today), C.Orange, (string?)normTo);
                    var passed = (T("Точек пройдено"), $"{route?.Passed ?? 0} / {route?.Stores.Count ?? 0}", C.Ink,
                        (string?)(promo.Gifts.Count > 0 ? F("подарков: {0}", GiftsLine(promo.Gifts)) : ""));
                    Stats(c, top, sleeping ?? accumulated, today, passed);
                    return;
                }

                default:
                {
                    if (ym is not null)
                    {
                        sleeping = Sleeping(T("Спящие Yamelly"), F("кэшбек дня +{0}", Money(promo.CashbackToday)));
                    }

                    var day = (F("{0} за день", P.Product), Kg(card.Today), C.Orange,
                        (string?)(norm is { } dn ? F("наборов {0} · норма {1}", promo.Sets, Kg(dn)) : F("наборов {0}", promo.Sets)));
                    var total = (F("Накоплено с {0}", StartText), Kg(card.Total), C.Green,
                        (string?)(card.Progress.Norm is { } tn ? F("завтра норма {0} в день", Kg(tn)) : T("все призы акции взяты")));
                    Stats(c, top, sleeping ?? accumulated, day, total);
                    return;
                }
            }
        }

        private (string, string, SKColor, string?) ToPrize(IncentiveAgentCard card)
        {
            var p = card.Progress;
            return p.Next is { } next
                ? (Capital(To(next)), Kg(p.ToNext ?? 0), C.Pink, string.Join(" · ", p.Tiers.Where(t => !t.Reached && t.Tier != next).Select(t => $"{To(t.Tier)} {IncentiveTexts.Kg(t.Left)}")))
                : (T("Все призы взяты"), IncentiveTexts.Short(p.Reached!, Language), C.Green, IncentiveTexts.Prize(p.Reached!, Language));
        }

        private string GiftsLine(IReadOnlyDictionary<string, int> gifts) =>
            string.Join(" · ", gifts.OrderByDescending(g => g.Value).Select(g => $"{GiftName(g.Key)} {g.Value}"));

        private string GiftName(string gift) =>
            O.Twizos?.GiftByImage(gift) is { } tier ? Language == "ru" || tier.NameUz is null ? tier.Name : ReportTexts.Uzbek(Language, tier.NameUz, tier.Name) : gift;

        private string PromoDayFooter(IncentiveAgentCard card)
        {
            var route = card.Route;
            var promo = card.Promo!;
            var stores = route?.Stores ?? [];
            var norm = card.DayStart.Norm;
            switch (cards.Slot)
            {
                case IncentiveSlot.Morning:
                {
                    var sets = stores.Sum(s => s.Sets?.Count ?? 0);
                    var head = F("{0} · {1} {2} ≈ {3}", Stores(stores.Count), Plural(sets, "{0} набор", "{0} набора", "{0} наборов"), P.Product, Kg(route?.TargetKg ?? 0));
                    if (route?.SleepingPlanned is > 0 and var sleeping && O.Yamelly is { } ym)
                    {
                        head += " · " + F("{0} {1}", Sleepers(sleeping), ym.Product);
                    }

                    return O.Twizos is null ? head : head + "\n" + F("кто брал {0} — предлагай пару подарков · новый клиент — начни с кружки", P.Product);
                }

                case IncentiveSlot.Midday or IncentiveSlot.Afternoon:
                {
                    var ahead = stores.Count(s => s.Status == BattleStoreStatus.Ahead);
                    var head = norm is { } n
                        ? F("Сделано {0} из нормы {1} · осталось {2}", Kg(card.Today), IncentiveTexts.Kg(n), Stores(ahead))
                        : F("Сделано {0} · осталось {1}", Kg(card.Today), Stores(ahead));
                    var halves = stores.Count(s => s.Sets is { Count: > 1 } && s.GiftsTaken == 1);
                    return halves > 0 ? head + "\n" + T("где взяли только один подарок — дожми второй заказ до 18:00") : head;
                }

                default:
                {
                    var parts = new List<string>();
                    var refused = stores.Where(s => s.Status == BattleStoreStatus.Refused && !s.Plain).Select(s => s.Order.ToString(CultureInfo.InvariantCulture)).ToList();
                    if (refused.Count > 0)
                    {
                        parts.Add(F("На мушке · отказы в ТТ {0} — в маршрут следующей недели", string.Join(", ", refused)));
                    }

                    if (promo.Gifts.Count > 0)
                    {
                        var outside = route?.OutOfRoute?.SelectMany(o => o.Gifts).ToList() ?? [];
                        var line = F("подарков: {0}", GiftsLine(promo.Gifts));
                        parts.Add(outside.Count > 0 ? line + " · " + F("из них вне маршрута {0}", outside.Count) : line);
                    }

                    if (promo.Best is { } best && O.Best is { } b && b.Covers(cards.Date))
                    {
                        parts.Add(best.Place is { } place
                            ? F("TW-01: {0}, сейчас {1}-е место · итог TW-01 — в 19:00", Kg(best.Kg), place)
                            : F("TW-01: {0} — порог {1} · итог TW-01 — в 19:00", Kg(best.Kg), Kg(best.MinKg)));
                    }

                    if (parts.Count == 0)
                    {
                        return F("Сегодня +{0} · накоплено {1}", Kg(card.Today), Kg(card.Total));
                    }

                    return parts.Count > 2 ? parts[0] + "\n" + string.Join(" · ", parts.Skip(1)) : string.Join("\n", parts);
                }
            }
        }

        // ---------- Выноска точки ----------

        private IReadOnlyList<IncentiveStoreSet> SetsOf(IncentiveStore s)
        {
            var sets = (s.Sets ?? []).Select(P.SetByCode).OfType<IncentiveStoreSet>().ToList();
            if (sets.Count == 0 && !s.Plain && s.TargetKg > 0 && !string.IsNullOrEmpty(s.Set))
            {
                sets.Add(new IncentiveStoreSet(0, s.Set, s.TargetKg, s.Prize));
            }

            return sets;
        }

        private bool TwizosShown(IncentiveStore s, bool evaluated) => evaluated && (s.TwizosStatus ?? s.Status) != BattleStoreStatus.Ahead;

        private static bool YamellyShown(IncentiveStore s, bool evaluated) => evaluated && s.YamellyStatus is { } ys && ys != BattleStoreStatus.Ahead;

        /// <summary>Строка названия в выноске (длинное название — следующей строкой, а не мелким шрифтом с «…»).</summary>
        private const float CalloutNameLine = 20;

        /// <summary>«9. KOZIMAKA OPTOM YANGI DO'KON»: номер и название точки — до трёх строк шириной выноски.</summary>
        private List<string> CalloutName(IncentiveStore s) =>
            Wrap($"{s.Order}. {Name(s.Name, s.MarketId)}", Faces.Bold, 16, CalloutWidth - 24, 3);

        private float CalloutHeight(IncentiveStore s, bool evaluated)
        {
            var extra = (CalloutName(s).Count - 1) * CalloutNameLine;
            if (s.Plain)
            {
                return 66 + extra;
            }

            var h = 12f + 22 + extra;
            if (s.Sleeping)
            {
                h += 6 + 20 + 19 + (YamellyShown(s, evaluated) ? 19 : 0);
            }

            var sets = SetsOf(s);
            if (sets.Count > 0)
            {
                h += 6 + 20 + sets.Count * 21 + 22 + (TwizosShown(s, evaluated) ? 19 : 0);
            }

            return h + 10;
        }

        private (string Contents, string Gift) SetTexts(IncentiveStoreSet set)
        {
            var (contents, prize, _) = IncentiveTexts.StoreSet(P, set.Contents, set.Prize, Language);
            return (contents, prize ?? "");
        }

        private void PromoCallout(SKCanvas c, float x, float y, IncentiveStore s, bool evaluated)
        {
            const float w = CalloutWidth;
            var h = CalloutHeight(s, evaluated);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 14);
            if (s.Plain)
            {
                using var paper = Fill(Paper);
                using var dash = new SKPaint { Color = PaperEdge, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, PathEffect = SKPathEffect.CreateDash([6, 4], 0) };
                c.DrawRoundRect(rect, paper);
                c.DrawRoundRect(rect, dash);
                var plainName = CalloutName(s);
                for (var i = 0; i < plainName.Count; i++)
                {
                    TextFit(c, plainName[i], x + 12, y + 10 + i * CalloutNameLine, w - 24, 16, Faces.SemiBold, C.Ink2);
                }

                var ly = y + 36 + (plainName.Count - 1) * CalloutNameLine;
                var line = s.AvgCheck > 0 ? F("обычный заказ ~{0}", Check(s.AvgCheck)) : T("обычный заказ");
                TextFit(c, line, x + 12, ly, w - 24 - (evaluated && s.HasOrder ? 70 : 0), 13, Faces.Medium, C.Ink3);
                if (evaluated && s.HasOrder)
                {
                    TextRight(c, $"✓ {T("заказ")}", x + w - 12, ly, 13, Faces.SemiBold, C.Green);
                }

                return;
            }

            using (var bg = Fill(evaluated && s.Status == BattleStoreStatus.Refused ? Refused : C.Card))
            using (var edge = Stroke(C.Border, 2))
            {
                c.DrawRoundRect(rect, bg);
                c.DrawRoundRect(rect, edge);
            }

            var name = CalloutName(s);
            for (var i = 0; i < name.Count; i++)
            {
                TextFit(c, name[i], x + 12, y + 10 + i * CalloutNameLine, w - 24, 16, Faces.Bold, C.Ink);
            }

            var cy = y + 34 + (name.Count - 1) * CalloutNameLine;
            var tx = x + 48;
            var tw = w - 60;
            if (s.Sleeping && O.Yamelly is { } ym)
            {
                cy += 6;
                if (FarmAssets.Images.GetValueOrDefault(YamellyPacks[(int)(Math.Abs(s.MarketId) % YamellyPacks.Length)]) is { } pack)
                {
                    DrawPack(c, pack, x + 10, cy, 32, 38);
                }

                TextFit(c, F("{0} · спящий −{1}%", ym.Product, IncentiveTexts.Kg(ym.DiscountPercent)), tx, cy, tw, 14, Faces.SemiBold, C.Pink);
                cy += 20;
                var packing = Units(ym.Packings?.FirstOrDefault()?.Name ?? "");
                TextFit(c, F("{0} кор. {1} · +{2} тебе", ym.MinBoxes, packing, Money(ym.MinCashback)), tx, cy, tw, 13.5f, Faces.SemiBold, C.Ink);
                cy += 19;
                if (YamellyShown(s, evaluated))
                {
                    var (text, tone) = s.YamellyStatus switch
                    {
                        BattleStoreStatus.Taken => (F("✓ взял · кэшбек +{0}", Money(s.Cashback)), C.Green),
                        BattleStoreStatus.Partial => (F("½ меньше {0} коробок", ym.MinBoxes), C.Amber),
                        _ => ($"× {T("отказ")}", C.Red),
                    };
                    TextFit(c, text, tx, cy, tw, 13, Faces.SemiBold, tone);
                    cy += 19;
                }
            }

            var sets = SetsOf(s);
            if (sets.Count == 0)
            {
                return;
            }

            cy += 6;
            if (FarmAssets.Images.GetValueOrDefault(Packs[(int)(Math.Abs(s.MarketId) % Packs.Length)]) is { } twizos)
            {
                DrawPack(c, twizos, x + 10, cy, 32, 44);
            }

            var gifts = sets.Select(set => SetTexts(set).Gift).Where(g => g.Length > 0).ToList();
            var label = sets.Count > 1 ? $"{P.Product} · {string.Join(" + ", gifts)}"
                : s.Sub == IncentiveTargetSub.Connect ? F("{0} · старт · {1}", P.Product, gifts.FirstOrDefault() ?? "")
                : $"{P.Product} · {gifts.FirstOrDefault() ?? T("дозаказ")}";
            TextFit(c, label, tx, cy, tw, 14, Faces.SemiBold, C.Orange);
            cy += 20;
            for (var i = 0; i < sets.Count; i++)
            {
                var set = sets[i];
                if (sets.Count > 1)
                {
                    using var ring = Stroke(C.Orange, 1.6f);
                    c.DrawCircle(tx + 7, cy + 8, 7.5f, ring);
                    TextCenter(c, (i + 1).ToString(CultureInfo.InvariantCulture), tx + 7, cy + 1.5f, 10.5f, Faces.Bold, C.Orange);
                }
                else
                {
                    using var dot = Fill(C.Orange);
                    c.DrawCircle(tx + 5, cy + 8, 3, dot);
                }

                TextFit(c, SetTexts(set).Contents, tx + 20, cy, tw - 20 - 30, 13.5f, Faces.Medium, C.Ink);
                if (set.Gift is { } g && FarmAssets.Images.GetValueOrDefault("Gift_" + g) is { } gift)
                {
                    DrawPack(c, gift, x + w - 12 - 28, cy - 3, 28, 22);
                }

                cy += 21;
            }

            var sum = sets.Sum(x2 => x2.Sum);
            var money = sum > 0 ? Money(sum) : Kg1(s.TargetKg);
            Text(c, money, tx, cy, 15, Faces.Bold, C.Ink);
            var after = tx + Measure(money, Faces.Bold, 15) + 6;
            if (sum > 0)
            {
                Text(c, $"· {Kg1(s.TargetKg)}", after, cy + 2, 12.5f, Faces.Medium, C.Ink3);
            }

            if (s.PlanAdded)
            {
                PlanPill(c, x + w - 12, cy);
            }

            cy += 22;
            if (TwizosShown(s, evaluated))
            {
                var status = s.TwizosStatus ?? s.Status;
                var taken = (s.Gifts ?? []).Select(GiftName).ToList();
                var (text, tone) = status switch
                {
                    BattleStoreStatus.Taken => (sets.Count > 1 ? F("✓ взял оба · {0}", Kg1(s.Kg)) : F("✓ взял · {0}", Kg1(s.Kg)), C.Green),
                    BattleStoreStatus.Partial => (taken.Count > 0 ? F("½ взял только {0} · {1}", string.Join(" + ", taken), Kg1(s.Kg)) : F("½ меньше набора · {0}", Kg1(s.Kg)), C.Amber),
                    _ => ($"× {T("отказ")}", C.Red),
                };
                TextFit(c, text, tx, cy, tw, 13, Faces.SemiBold, tone);
            }
        }

        /// <summary>Зелёная плашка «+ план» (правый край — right).</summary>
        private void PlanPill(SKCanvas c, float right, float top)
        {
            var label = T("+ план");
            var w = Measure(label, Faces.Bold, 11.5f) + 12;
            using var bg = Fill(C.Green);
            c.DrawRoundRect(new SKRoundRect(new SKRect(right - w, top + 1, right, top + 19), 6), bg);
            TextCenter(c, label, right - w / 2, top + 4, 11.5f, Faces.Bold, SKColors.White);
        }

        /// <summary>Число со словом в нужной форме: 1 точка, 2 точки, 5 точек (узбекский — одна форма в словаре).</summary>
        private string Plural(int n, string one, string few, string many)
        {
            var (d, h) = (n % 10, n % 100);
            var form = d == 1 && h != 11 ? one : d is >= 2 and <= 4 && h is < 12 or > 14 ? few : many;
            return F(form, n);
        }

        /// <summary>Имя точки Linko заглавными (в Linko — то заглавными, то строчными: на карточке одинаково); пустое — «ТТ 12345».</summary>
        private string Name(string name, long id) => string.IsNullOrWhiteSpace(name) ? F("ТТ {0}", id) : name.Trim().ToUpperInvariant();

        /// <summary>Фасовка из настроек («420 г») на языке карточки.</summary>
        private string Units(string text) => Language == "ru" ? text : text.Replace(" кг", " kg").Replace(" г", " g");

        private string Points(int n) => Plural(n, "{0} точка", "{0} точки", "{0} точек");

        private string Stores(int n) => Plural(n, "{0} магазин", "{0} магазина", "{0} магазинов");

        private string Sleepers(int n) => Plural(n, "{0} спящая", "{0} спящие", "{0} спящих");

        private string Check(decimal sum) => sum >= 1_000_000
            ? F("{0} млн", (Math.Round(sum / 100_000) / 10).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ','))
            : F("{0} тыс.", Math.Round(sum / 1000).ToString("0", CultureInfo.InvariantCulture));

        // ---------- Блоки дня ----------

        /// <summary>Дорога к призам TW-03: фото призов, флажки порогов своей группы, «ещё N кг» под каждым, заливка до накопленного и герой.</summary>
        private void PromoRoad(SKCanvas c, float top, IncentiveAgentCard card, string note) => PromoRoad(c, top, card.Progress, card.Total, card.Wholesale, note);

        private void PromoRoad(SKCanvas c, float top, IncentiveProgress progress, decimal total, bool wholesale, string note)
        {
            CardBox(c, Pad, top, Inner, RoadHeight);
            Trophy(c, Pad + 40, top + 34, 0.9f);
            Text(c, $"{T("Дорога к призам")} · TW-03", Pad + 76, top + 18, 24, Faces.Bold, C.Ink);
            TextFit(c, note, Pad + 500, top + 22, Inner - 524, 17, Faces.SemiBold, C.Ink2);
            var tiers = progress.Tiers.Count > 0 ? progress.Tiers : P.TiersFor(wholesale).Select(t => new IncentiveTierLeft(t, Math.Max(0, t.Kg - total))).ToList();
            if (tiers.Count == 0)
            {
                return;
            }

            var x0 = Pad + 40f;
            var x1 = Width - Pad - 112f;
            var max = tiers.Max(t => t.Tier.Kg);
            float X(decimal kg) => x0 + (x1 - x0) * (float)(Math.Clamp(kg, 0, max) / max);
            var barY = top + 212;
            using (var band = Fill(SKColor.Parse("#F1D9A8")))
            {
                c.DrawRoundRect(new SKRoundRect(new SKRect(x0 - 16, barY, x1 + 60, barY + 28), 14), band);
            }

            var fx = Math.Max(x0 + 24, X(total));
            using (var fill = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(x0, 0), new SKPoint(fx, 0), [SKColor.Parse("#F6B13A"), C.Orange], SKShaderTileMode.Clamp) })
            {
                c.DrawRoundRect(new SKRoundRect(new SKRect(x0 - 16, barY, fx, barY + 28), 14), fill);
            }

            foreach (var t in tiers)
            {
                var x = X(t.Tier.Kg);
                using var pole = Fill(C.Trunk);
                c.DrawRect(x - 2, barY - 64, 4, 76, pole);
                using (var flag = Fill(t.Reached ? C.Green : SKColor.Parse("#CDB796")))
                using (var pennant = new SKPath())
                {
                    pennant.MoveTo(x + 2, barY - 64);
                    pennant.LineTo(x + 92, barY - 64);
                    pennant.LineTo(x + 82, barY - 52);
                    pennant.LineTo(x + 92, barY - 40);
                    pennant.LineTo(x + 2, barY - 40);
                    pennant.Close();
                    c.DrawPath(pennant, flag);
                }

                Text(c, Kg(t.Tier.Kg), x + 8, barY - 60, 14, Faces.Bold, SKColors.White);
                if ((FarmAssets.Images.GetValueOrDefault("PrizePhoto_" + t.Tier.Image) ?? PrizeImage(t.Tier)) is { } image)
                {
                    const float iw = 170f;
                    var ix = Math.Clamp(x - iw / 2, Pad + 12, Width - Pad - 12 - iw);
                    // Фото — над флажком с зазором: низ фото (top + 132) выше флажка (top + 148).
                    DrawPack(c, image, ix, top + 50, iw, 82);
                }

                var cx = Math.Clamp(x, Pad + 100, Width - Pad - 100);
                TextCenter(c, IncentiveTexts.Prize(t.Tier, Language), cx, barY + 40, 18, Faces.Bold, t.Reached ? C.Green : C.Ink);
                TextCenter(c, t.Reached ? $"✓ {T("взят")}" : F("ещё {0}", Kg(t.Left)), cx, barY + 64, 14, Faces.Medium, t.Reached ? C.Green : C.Ink3);
            }

            Hero(c, fx, barY - 26);
        }

        private float OffersHeight(IncentiveAgentCard card)
        {
            var lines = PairLines().Sum(l => Wrap(l, Faces.Medium, 15.5f, Inner - 64, 3).Count);
            var top = (card.Promo?.TopByAkb.Count > 0 ? 34 : 0) + (card.Promo?.TopByKg.Count > 0 ? 34 : 0);
            return 62 + 104 + 14 + (lines > 0 ? 20 + lines * 24 : 0) + (top > 0 ? 10 + top : 0) + 18;
        }

        private List<string> PairLines()
        {
            if (O.Twizos is not { } tw)
            {
                return [];
            }

            var lines = new List<string>();
            var pairs = tw.PairRules.Select(rule => rule.Sets.Select(P.SetByCode).OfType<IncentiveStoreSet>().ToList()).Where(x => x.Count > 0)
                .Select(sets => $"{string.Join(" + ", sets.Select(x => SetTexts(x).Gift))} {Money(sets.Sum(x => x.Sum))}").Distinct().ToList();
            if (pairs.Count > 0)
            {
                lines.Add(F("Пара подарков в одной ТТ = 2 заказа: {0}", string.Join(" · ", pairs)));
            }

            if (P.SetByCode(tw.StartSet) is { } start)
            {
                var (contents, gift) = SetTexts(start);
                var line = F("новый клиент {0} — старт: {1} ({2} · {3})", P.Product, gift, contents, Money(start.Sum));
                if (P.SetByCode(tw.SmallStartSet) is { } small)
                {
                    line += " · " + F("чек до {0} — {1} ({2})", Check(tw.SmallCheck), SetTexts(small).Contents, Money(small.Sum));
                }

                lines.Add(line);
            }

            return lines;
        }

        /// <summary>«Что предлагаем клиенту»: подарки с фото и набором, Yamelly −7% спящему, пары и старт, ТОП SKU региона.</summary>
        private void Offers(SKCanvas c, float top, float height, IncentiveAgentCard card)
        {
            CardBox(c, Pad, top, Inner, height);
            if (FarmAssets.Images.GetValueOrDefault("Gift_mug") is { } icon)
            {
                DrawPack(c, icon, Pad + 22, top + 16, 34, 30);
            }

            Text(c, T("Что предлагаем клиенту"), Pad + 64, top + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, T("подарок вручает водитель · на каждый заказ"), Width - Pad - 24, top + 24, 16, Faces.SemiBold, C.Ink2);
            var tw = O.Twizos!;
            var tiers = tw.GiftTiers;
            var ym = O.Yamelly;
            var units = tiers.Count + (ym is not null ? 1.4f : 0);
            var gap = 12f;
            var unit = (Inner - 40 - gap * (tiers.Count + (ym is not null ? 1 : 0) - 1)) / Math.Max(1, units);
            var x = Pad + 20;
            var y = top + 62;
            foreach (var tier in tiers)
            {
                using (var bg = Fill(Cream))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + unit, y + 104), 14), bg);
                }

                if (FarmAssets.Images.GetValueOrDefault("Gift_" + tier.Gift) is { } photo)
                {
                    DrawPack(c, photo, x + 8, y + 14, 64, 76);
                }

                var set = P.StoreSets.Where(s => s.Gift == tier.Gift && s.Sum > 0).OrderBy(s => s.Sum).FirstOrDefault();
                var tx = x + 80;
                var w = unit - 88;
                TextFit(c, Capital(GiftName(tier.Gift)), tx, y + 10, w, 18, Faces.Bold, C.Ink);
                TextFit(c, F("от {0}", Money(tier.MinSum)), tx, y + 34, w, 13.5f, Faces.Medium, C.Ink2);
                if (set is not null)
                {
                    TextFit(c, SetTexts(set).Contents, tx, y + 54, w, 13.5f, Faces.Medium, C.Ink2);
                    TextFit(c, Money(set.Sum), tx, y + 74, w, 16, Faces.Bold, C.Orange);
                }

                x += unit + gap;
            }

            if (ym is not null)
            {
                var w = Width - Pad - 20 - x;
                using (var bg = Fill(Rose))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + w, y + 104), 14), bg);
                }

                if (FarmAssets.Images.GetValueOrDefault(YamellyPacks[0]) is { } pack)
                {
                    DrawPack(c, pack, x + 8, y + 14, 56, 76);
                }

                var tx = x + 72;
                TextFit(c, F("{0} −{1}% спящему", ym.Product, IncentiveTexts.Kg(ym.DiscountPercent)), tx, y + 10, w - 80, 17, Faces.Bold, C.Pink);
                TextFit(c, F("{0} целые коробки одной фасовки", ym.MinBoxes), tx, y + 36, w - 80, 13.5f, Faces.Medium, C.Ink2);
                TextFit(c, F("скидка на весь {0} в накладной", ym.Product), tx, y + 56, w - 80, 13.5f, Faces.Medium, C.Ink2);
                TextFit(c, F("тебе кэшбек +{0}", Money(ym.MinCashback)), tx, y + 76, w - 80, 15, Faces.Bold, C.Green);
            }

            y += 104 + 14;
            var lines = PairLines().SelectMany(l => Wrap(l, Faces.Medium, 15.5f, Inner - 64, 3)).ToList();
            if (lines.Count > 0)
            {
                using (var bg = Fill(Cream))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(Pad + 20, y, Width - Pad - 20, y + 20 + lines.Count * 24), 12), bg);
                }

                var ly = y + 10;
                foreach (var line in lines)
                {
                    Text(c, line, Pad + 32, ly, 15.5f, Faces.Medium, C.Ink);
                    ly += 24;
                }

                y += 20 + lines.Count * 24 + 10;
            }

            var promo = card.Promo!;
            void TopLine(string label, IReadOnlyList<IncentiveTopSku> skus)
            {
                if (skus.Count == 0)
                {
                    return;
                }

                Text(c, label, Pad + 24, y + 6, 15.5f, Faces.Bold, C.Ink);
                var lx = Pad + 24 + Measure(label, Faces.Bold, 15.5f) + 10;
                foreach (var (sku, i) in skus.Select((s, i) => (s, i)))
                {
                    var (name, image) = Sku(sku);
                    if (image is not null && FarmAssets.Images.GetValueOrDefault(image) is { } pack)
                    {
                        DrawPack(c, pack, lx, y, 22, 28);
                        lx += 26;
                    }

                    var text = i < skus.Count - 1 ? name + " ·" : name;
                    Text(c, text, lx, y + 6, 15.5f, Faces.Medium, C.Ink);
                    lx += Measure(text, Faces.Medium, 15.5f) + 10;
                }

                y += 34;
            }

            TopLine(F("ТОП по АКБ ({0}):", card.Agent.Region ?? ""), promo.TopByAkb);
            TopLine(T("ТОП по весу:"), promo.TopByKg);
        }

        /// <summary>SKU Twizos коротко: «35 г Шашлык» и пачка вкуса.</summary>
        private (string Name, string? Image) Sku(IncentiveTopSku sku)
        {
            var name = sku.Name.ToLowerInvariant();
            (string Ru, string Uz, string Image)? flavor = name switch
            {
                _ when name.Contains("shashlik") || name.Contains("шашлык") => ("Шашлык", "Shashlik", "Twizos_Kabob"),
                _ when name.Contains("piyoz") || name.Contains("лук") => ("Сметана-лук", "Smetana-piyoz", "Twizos_Smetana_va_piyoz"),
                _ when name.Contains("ko'kat") || name.Contains("kokat") || name.Contains("ko‘kat") || name.Contains("зелен") => ("Сметана-зелень", "Smetana-ko‘kat", "Twizos_Smetana_va_kokatlar"),
                _ when name.Contains("dengiz") || name.Contains("соль") => ("Морская соль", "Dengiz tuzi", "Twizos_Dengiz_tuzi"),
                _ when name.Contains("chili") || name.Contains("чили") => ("Колбаски чили", "Chili kolbasa", "Twizos_Kolbaska_chili"),
                _ when name.Contains("pishloq") || name.Contains("сыр") => ("Сыр", "Pishloq", "Twizos_Pishloq"),
                _ => null,
            };
            var grams = Regex.Match(sku.Name, @"(\d+)\s*(г|гр|g)\b", RegexOptions.IgnoreCase) is { Success: true } m ? m.Groups[1].Value
                : sku.UnitKg > 0 ? Math.Round(sku.UnitKg * 1000).ToString("0", CultureInfo.InvariantCulture) : null;
            var weight = grams is null ? "" : $"{grams} {T("г")} ";
            return flavor is { } f ? ($"{weight}{(Language == "ru" ? f.Ru : ReportTexts.Uzbek(Language, f.Uz, f.Ru))}", f.Image) : (sku.Name, null);
        }

        private static float GoalsHeight(IncentiveGoalsSummary goals) => 62 + (5 + (goals.Extra > 0 ? 1 : 0)) * 38 + 50 + 16;

        /// <summary>«Как собраны цели на сегодня»: точек по шагам ТЗ 4.2 и итог целей против нормы с запасом.</summary>
        private void Goals(SKCanvas c, float top, float height, IncentiveGoalsSummary goals)
        {
            CardBox(c, Pad, top, Inner, height);
            DrawCompass(c, Pad + 38, top + 32);
            Text(c, T("Как собраны цели на сегодня"), Pad + 64, top + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, F("{0} на маршруте", Stores(goals.Stores)), Width - Pad - 24, top + 24, 16, Faces.SemiBold, C.Ink2);
            var tw = O.Twizos;
            var start = tw is not null && P.SetByCode(tw.StartSet) is { } st ? SetTexts(st).Gift : "";
            var rows = new List<(string Label, int Count, bool Muted, bool Plan)>
            {
                (F("Брали {0} → пара подарков в одной ТТ (2 заказа)", P.Product), goals.Pairs, false, false),
                (start.Length > 0 ? F("Не брали {0} → старт: {1}", P.Product, start) : F("Не брали {0} → старт", P.Product), goals.Starts, false, false),
                (T("Не хватало до нормы → новым с большим чеком сразу пара"), goals.PlanAdded, false, true),
                (O.Yamelly is { } ym ? F("{0} −{1}% — спящие из списка", ym.Product, IncentiveTexts.Kg(ym.DiscountPercent)) : T("Спящие из списка"), goals.Sleeping, false, false),
                (T("Без цели — обычный заказ (серые)"), goals.Plain, true, false),
            };
            if (goals.Extra > 0)
            {
                rows.Add((F("Добор вне маршрута — {0}", Kg(goals.ExtraKg)), goals.Extra, false, false));
            }

            using var line = Stroke(C.Border, 1.5f);
            var y = top + 62;
            foreach (var row in rows)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                Text(c, row.Label, Pad + 24, y + 10, 17, Faces.Medium, row.Muted ? C.Ink3 : C.Ink);
                if (row.Plan)
                {
                    PlanPill(c, Pad + 24 + Measure(row.Label, Faces.Medium, 17) + 12 + Measure(T("+ план"), Faces.Bold, 11.5f) + 12, y + 11);
                }

                TextRight(c, F("{0} ТТ", row.Count), Width - Pad - 24, y + 10, 17, Faces.Bold, row.Muted ? C.Ink3 : C.Ink);
                y += 38;
            }

            c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
            var reserve = tw is not null ? IncentiveTexts.Kg(tw.Reserve) : "1";
            var label = goals.Norm is { } n ? F("Итого цели {0} на маршруте · норма {1} × {2}", P.Product, Kg(n), reserve) : F("Итого цели {0} на маршруте", P.Product);
            Text(c, label, Pad + 24, y + 14, 18, Faces.Bold, C.Ink);
            var value = goals.Needed is { } need ? F("{0} из {1}", IncentiveTexts.Kg(goals.GoalsKg), Kg(need)) : Kg(goals.GoalsKg);
            TextRight(c, value, Width - Pad - 24, y + 14, 19, Faces.Bold, goals.Needed is { } nd && goals.GoalsKg + goals.ExtraKg >= nd ? C.Green : C.Orange);
        }

        private void DrawCompass(SKCanvas c, float cx, float cy)
        {
            using var ring = Stroke(C.Amber, 3);
            c.DrawCircle(cx, cy, 13, ring);
            using var needle = Fill(C.Brand);
            using var path = new SKPath();
            path.MoveTo(cx + 7, cy - 7);
            path.LineTo(cx + 2, cy + 2);
            path.LineTo(cx - 7, cy + 7);
            path.LineTo(cx - 2, cy - 2);
            path.Close();
            c.DrawPath(path, needle);
        }

        private static float ExtraHeight(IReadOnlyList<IncentiveStore> extra, bool evaluated) => 62 + extra.Count * (evaluated ? 84 : 66) + 8;

        /// <summary>«Добери до нормы — клиенты вне маршрута»: точки ТП с других дней с большим чеком и пара подарков.</summary>
        private void Extra(SKCanvas c, float top, float height, IReadOnlyList<IncentiveStore> extra, IncentiveAgentCard card, bool evaluated)
        {
            CardBox(c, Pad, top, Inner, height);
            PlusIcon(c, Pad + 34, top + 32, C.Ink);
            Text(c, T("Добери до нормы — клиенты вне маршрута"), Pad + 56, top + 18, 24, Faces.Bold, C.Ink);
            if (card.Promo?.Goals is { Needed: { } need } goals && need > goals.GoalsKg)
            {
                TextRight(c, F("не хватает {0} · звонок или доп. визит", Kg(need - goals.GoalsKg)), Width - Pad - 24, top + 24, 16, Faces.SemiBold, C.Ink2);
            }

            var y = top + 60;
            var rowHeight = evaluated ? 84f : 66f;
            foreach (var s in extra)
            {
                var rect = new SKRoundRect(new SKRect(Pad + 20, y, Width - Pad - 20, y + rowHeight - 10), 12);
                using (var bg = Fill(SKColor.Parse("#E3F4D6")))
                using (var dash = new SKPaint { Color = MintEdge, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, PathEffect = SKPathEffect.CreateDash([8, 5], 0) })
                {
                    c.DrawRoundRect(rect, bg);
                    c.DrawRoundRect(rect, dash);
                }

                if (FarmAssets.Images.GetValueOrDefault(Packs[(int)(Math.Abs(s.MarketId) % Packs.Length)]) is { } pack)
                {
                    DrawPack(c, pack, Pad + 30, y + 8, 30, 40);
                }

                var sets = SetsOf(s);
                var head = s.AvgCheck > 0 ? $"{Name(s.Name, s.MarketId)} · {F("чек {0}", Check(s.AvgCheck))}" : Name(s.Name, s.MarketId);
                if (s.Sub == IncentiveTargetSub.Connect)
                {
                    head += " · " + F("{0} не брал", P.Product);
                }

                TextFit(c, head, Pad + 72, y + 8, Inner - 72 - 200, 16, Faces.Bold, C.Ink);
                var offer = $"{string.Join(" + ", sets.Select(x => SetTexts(x).Gift))} · {Money(sets.Sum(x => x.Sum))} · {Kg1(s.TargetKg)}";
                TextFit(c, offer, Pad + 72, y + 32, Inner - 72 - 200, 15, Faces.SemiBold, C.Orange);
                var gx = Width - Pad - 40f;
                foreach (var set in sets.AsEnumerable().Reverse())
                {
                    if (set.Gift is { } g && FarmAssets.Images.GetValueOrDefault("Gift_" + g) is { } gift)
                    {
                        DrawPack(c, gift, gx - 44, y + 8, 44, 40);
                        gx -= 50;
                    }
                }

                if (evaluated && (s.TwizosStatus ?? s.Status) is var st && st != BattleStoreStatus.Ahead)
                {
                    var (text, tone) = st switch
                    {
                        BattleStoreStatus.Taken => (F("✓ взял · {0}", Kg1(s.Kg)), C.Green),
                        BattleStoreStatus.Partial => (F("½ взял только {0} · {1}", string.Join(" + ", (s.Gifts ?? []).Select(GiftName)), Kg1(s.Kg)), C.Amber),
                        _ => ($"× {T("отказ")}", C.Red),
                    };
                    TextFit(c, text, Pad + 72, y + 54, Inner - 100, 14, Faces.SemiBold, tone);
                }

                y += rowHeight;
            }
        }

        private void PlusIcon(SKCanvas c, float cx, float cy, SKColor color)
        {
            using var plus = Fill(color);
            c.DrawRect(cx - 11, cy - 3.5f, 22, 7, plus);
            c.DrawRect(cx - 3.5f, cy - 11, 7, 22, plus);
        }

        private static float SoldHeight(IReadOnlyList<IncentiveOutOfRouteSale> sold) => 62 + Math.Min(5, sold.Count) * 62 + (sold.Count > 5 ? 26 : 0) + 8;

        /// <summary>«Продажа вне маршрута — засчитана»: точка, Twizos и сумма, подарок, кэшбек за спящую.</summary>
        private void OutOfRoute(SKCanvas c, float top, float height, IReadOnlyList<IncentiveOutOfRouteSale> sold)
        {
            CardBox(c, Pad, top, Inner, height);
            PlusIcon(c, Pad + 34, top + 32, C.Ink);
            Text(c, T("Продажа вне маршрута — засчитана"), Pad + 56, top + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, T("в цель дня и в накопительную"), Width - Pad - 24, top + 24, 16, Faces.SemiBold, C.Ink2);
            var y = top + 60;
            foreach (var s in sold.Take(5))
            {
                var rect = new SKRoundRect(new SKRect(Pad + 20, y, Width - Pad - 20, y + 52), 12);
                using (var bg = Fill(SKColor.Parse("#E3F4D6")))
                using (var dash = new SKPaint { Color = MintEdge, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, PathEffect = SKPathEffect.CreateDash([8, 5], 0) })
                {
                    c.DrawRoundRect(rect, bg);
                    c.DrawRoundRect(rect, dash);
                }

                if (FarmAssets.Images.GetValueOrDefault(Packs[(int)(Math.Abs(s.MarketId) % Packs.Length)]) is { } pack)
                {
                    DrawPack(c, pack, Pad + 30, y + 6, 28, 40);
                }

                var parts = new List<string> { Name(s.Name, s.MarketId) };
                if (s.Kg > 0)
                {
                    parts.Add(F("{0} {1} · {2} сум", P.Product, Kg1(s.Kg), Money(s.Sum)));
                }

                if (s.Cashback > 0 && O.Yamelly is { } ym)
                {
                    parts.Add(F("{0} · кэшбек +{1}", ym.Product, Money(s.Cashback)));
                }

                TextFit(c, string.Join(" · ", parts), Pad + 68, y + 16, Inner - 68 - 210, 16, Faces.SemiBold, C.Ink);
                var gift = s.Gifts.FirstOrDefault();
                if (gift is not null)
                {
                    if (FarmAssets.Images.GetValueOrDefault("Gift_" + gift) is { } photo)
                    {
                        DrawPack(c, photo, Width - Pad - 190, y + 6, 40, 40);
                    }

                    TextFit(c, $"{GiftName(gift)} ✓", Width - Pad - 144, y + 16, 110, 16, Faces.Bold, C.Green);
                }

                y += 62;
            }

            if (sold.Count > 5)
            {
                Text(c, F("и ещё {0}", sold.Count - 5), Pad + 24, y, 15, Faces.Medium, C.Ink3);
            }
        }

        private void CashbackTitle(SKCanvas c, float top, string note)
        {
            using (var coin = Fill(C.Amber))
            {
                c.DrawCircle(Pad + 36, top + 32, 13, coin);
            }

            TextCenter(c, "$", Pad + 36, top + 22, 17, Faces.Bold, SKColors.White);
            Text(c, T("Кэшбек YM-02 · баланс"), Pad + 60, top + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, note, Width - Pad - 24, top + 24, 16, Faces.SemiBold, C.Ink2);
        }

        private void CashbackCells(SKCanvas c, float top, params (string Label, string Value, SKColor Ink, bool Yellow)[] cells)
        {
            var gap = 12f;
            var w = (Inner - 40 - gap * (cells.Length - 1)) / cells.Length;
            for (var i = 0; i < cells.Length; i++)
            {
                var x = Pad + 20 + i * (w + gap);
                using (var bg = Fill(cells[i].Yellow ? Butter : Mint))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(x, top, x + w, top + 84), 12), bg);
                }

                TextFit(c, cells[i].Label, x + 14, top + 12, w - 28, 14, Faces.Medium, SKColor.Parse("#4C6B52"));
                TextFit(c, cells[i].Value, x + 14, top + 38, w - 28, 27, Faces.Bold, cells[i].Ink);
            }
        }

        private void CashbackDay(SKCanvas c, float top, IncentiveAgentCard card, IncentiveCashbackBalance balance)
        {
            CardBox(c, Pad, top, Inner, 176);
            CashbackTitle(c, top, T("выплата пн–вт за прошлую неделю"));
            var remaining = Math.Max(0, balance.Listed - balance.Woken);
            CashbackCells(c, top + 66,
                (T("Оформлено сегодня"), Money(card.Promo!.CashbackToday), C.Ink, false),
                (T("Ждёт доставки"), Money(balance.Pending), C.Ink, false),
                (T("Выплачено"), Money(balance.Paid), C.Ink, false),
                (F("Можно ещё · {0}", Points(remaining)), Money(balance.CanEarn), C.Ink, true));
        }

        private List<string> FooterLines(string text) =>
            text.Split('\n').SelectMany(part => Wrap(part, Faces.Bold, 22, Inner - 48, 2)).Take(4).ToList();

        private float FooterHeight(string text) => Math.Max(92, 36 + FooterLines(text).Count * 30);

        /// <summary>Жёлтый итог в 1–4 строки.</summary>
        private void PromoFooter(SKCanvas c, float top, string text)
        {
            var lines = FooterLines(text);
            var height = FooterHeight(text);
            using var shadow = Fill(SKColor.Parse("#E6B820"));
            c.DrawRoundRect(new SKRoundRect(new SKRect(Pad, top + 5, Width - Pad, top + height + 5), 20), shadow);
            using var bg = Fill(SKColor.Parse("#FFD84A"));
            c.DrawRoundRect(new SKRoundRect(new SKRect(Pad, top, Width - Pad, top + height), 20), bg);
            var y = top + (height - lines.Count * 30) / 2 + 2;
            foreach (var line in lines)
            {
                TextCenter(c, line, Width / 2, y, 22, Faces.Bold, C.Ink);
                y += 30;
            }
        }

        // ---------- Неделя ----------

        public byte[] PromoWeek(IncentiveAgentCard card)
        {
            var week = card.Week!;
            var promo = card.Promo!;
            var strip = IsTest ? 52f : 0f;
            var statsTop = strip + HeaderHeight + StaleHeight;
            var panelTop = statsTop + StatsHeight + Gap;
            float TileHeight(int row)
            {
                var stores = Math.Max(1, week.Days.Skip(row * 3).Take(3).Select(d => d.Route.Stores.Count).DefaultIfEmpty(1).Max());
                return 92 + (int)Math.Ceiling(Math.Min(stores, 28) / 4.0) * 52 + 12;
            }

            var tileHeights = new[] { TileHeight(0), TileHeight(1) };
            var panelHeight = 64 + tileHeights[0] + tileHeights[1] + 3 * 14;
            var balance = promo.Cashback;
            var weeks = balance?.Weeks.Where(w => w.Monday <= week.Monday).ToList() ?? [];
            var cashTop = panelTop + panelHeight + Gap;
            var cashHeight = balance is null ? 0 : 66 + 84 + 16 + 34 + Math.Max(1, weeks.Count) * 40 + 12;
            var roadTop = cashTop + (cashHeight > 0 ? cashHeight + Gap : 0);
            var footerTop = roadTop + RoadHeight + Gap;
            var footer = PromoWeekFooter(card);
            using var surface = Surface(footerTop + FooterHeight(footer) + Pad + 6, out var c);
            var weekNo = (week.Monday.Day - 1) / 7 + 1;
            Header(c, strip, T("Итог недели"), $"{AgentTitle(card.Agent)} · {DayMonth(week.Monday)}–{DayMonth(week.Saturday)}", IncentiveSlot.Week, 0, 1,
                F("Нед {0}", weekNo), IncentiveTexts.MonthOf(week.Monday.Month, Language));
            StaleBanner(c, strip + HeaderHeight);

            var days = promo.Days.ToDictionary(d => d.Date);
            var planned = promo.Days.Sum(d => d.SleepingPlanned);
            var woken = promo.Days.Sum(d => d.SleepingWoken);
            var bestDay = week.Days.Where(d => d.Kg > 0).OrderByDescending(d => d.Kg).FirstOrDefault();
            var first = O.Yamelly is not null
                ? (T("Спящие разбужено"), $"{woken} / {planned}", C.Pink, (string?)F("кэшбек недели {0}", Money(promo.Days.Sum(d => d.Cashback))))
                : (T("Накоплено"), Kg(card.Total), C.Green, (string?)F("с {0}", StartText));
            var second = (F("{0} за неделю", P.Product), Kg(week.Kg), C.Orange,
                (string?)(week.Norm is { } wn ? F("наборов {0} · норма {1}", promo.Sets, Kg(wn)) : F("наборов {0}", promo.Sets)));
            var third = bestDay is null
                ? (T("Лучший день"), "—", C.Ink3, (string?)"")
                : (T("Лучший день"), $"{Weekday(bestDay.Date)} · {Kg(bestDay.Kg)}", C.Green,
                    (string?)(days.GetValueOrDefault(bestDay.Date)?.BestWinner == true && O.Best is { } b ? F("победа TW-01 · +{0}", Money(b.Prize))
                        : promo.Best is { Wins: > 0 } bw ? F("побед TW-01 за неделю: {0}", bw.Wins) : ""));
            Stats(c, statsTop, first, second, third);

            CardBox(c, Pad, panelTop, Inner, panelHeight);
            Text(c, T("6 дней — 6 полян"), Pad + 24, panelTop + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, T("✓ взял · ½ меньше цели · × отказ · серые — без цели"), Width - Pad - 24, panelTop + 24, 15, Faces.SemiBold, C.Ink2);
            var tileWidth = (Inner - 2 * 16 - 2 * 14) / 3;
            for (var i = 0; i < 6 && i < week.Days.Count; i++)
            {
                var x = Pad + 16 + (i % 3) * (tileWidth + 14);
                var y = panelTop + 64 + (i / 3) * (tileHeights[0] + 14);
                PromoWeekTile(c, x, y, tileWidth, tileHeights[i / 3], week.Days[i], days.GetValueOrDefault(week.Days[i].Date));
            }

            if (balance is not null)
            {
                CashbackWeek(c, cashTop, cashHeight, balance, weeks, promo, week);
            }

            PromoRoad(c, roadTop, card, F("накоплено {0} · +{1} за неделю", Kg(card.Total), Kg(week.Kg)));
            PromoFooter(c, footerTop, footer);
            return Png(surface);
        }

        private void PromoWeekTile(SKCanvas c, float x, float y, float w, float h, IncentiveWeekDay day, IncentivePromoDay? promo)
        {
            using var bg = Fill(C.Map);
            using var edge = Stroke(C.MapBorder, 3);
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 18);
            c.DrawRoundRect(rect, bg);
            c.DrawRoundRect(rect, edge);
            Text(c, Weekday(day.Date), x + 14, y + 10, 22, Faces.Bold, C.Ink);
            var stores = day.Route.Stores;
            // День впереди или без маршрута и продаж — «—», а не красные 0%.
            var share = day.Date > cards.Date || (stores.Count == 0 && day.Kg <= 0) ? null : promo?.Share;
            var tone = share is >= 0.7m ? C.Green : share is >= 0.4m ? C.Orange : C.Red;
            TextRight(c, share is { } sh ? ReportFormat.Percent(sh) : day.Kg > 0 ? Kg(day.Kg) : "—", x + w - 14, y + 10, 22, Faces.Bold, share is null ? C.Ink3 : tone);
            var parts = new List<string> { F("{0} ТТ", stores.Count) };
            if (O.Yamelly is { } ym && promo is not null)
            {
                parts.Add($"{ym.Product} {promo.SleepingWoken}/{promo.SleepingPlanned}");
            }

            parts.Add($"{P.Product} {Kg(day.Kg)}");
            if (promo is not null)
            {
                parts.Add(F("наборов {0}", promo.Sets));
            }

            // Части строки не рвутся внутри («наборов 21» — целиком).
            var lines = Wrap(string.Join(" · ", parts.Select(x => x.Replace(' ', '\u00A0'))), Faces.SemiBold, 13, w - 28, 2);
            var ly = y + 40;
            foreach (var line in lines)
            {
                Text(c, line, x + 14, ly, 13, Faces.SemiBold, SKColor.Parse("#4C6B52"));
                ly += 17;
            }

            if (stores.Count == 0)
            {
                Text(c, day.Date > cards.Date ? T("день впереди") : T("маршрута не было"), x + 14, y + 86, 15, Faces.Medium, C.Ink3);
                return;
            }

            var cell = (w - 20) / 4;
            foreach (var (store, i) in stores.Take(28).Select((s, i) => (s, i)))
            {
                var cx = x + 10 + (i % 4) * cell + cell / 2;
                var cy = y + 92 + (i / 4) * 52 + 22;
                var status = StopStatus(store);
                House(c, cx, cy, 0.42f, status, status is BattleStoreStatus.Ahead or PlainStatus ? null : status, 0);
            }

            if (stores.Count > 28)
            {
                TextRight(c, $"+{stores.Count - 28}", x + w - 12, y + h - 26, 14, Faces.Bold, C.Ink2);
            }
        }

        private void CashbackWeek(SKCanvas c, float top, float height, IncentiveCashbackBalance balance, IReadOnlyList<IncentiveCashbackWeek> weeks, IncentivePromoView promo,
            IncentiveWeekView week)
        {
            CardBox(c, Pad, top, Inner, height);
            var payMonday = week.Monday.AddDays(7);
            CashbackTitle(c, top, F("доставка подтверждена → выплата пн–вт {0}–{1}", payMonday.Day.ToString(CultureInfo.InvariantCulture), DayMonth(payMonday.AddDays(1))));
            var issued = promo.Days.Sum(d => d.Cashback);
            var woken = promo.Days.Sum(d => d.SleepingWoken);
            var thisWeek = weeks.FirstOrDefault(w => w.Monday == week.Monday);
            var remaining = Math.Max(0, balance.Listed - balance.Woken);
            CashbackCells(c, top + 66,
                (F("Оформлено · {0}", Points(woken)), Money(issued), C.Ink, false),
                (T("Подтверждено → к выплате"), Money(thisWeek?.ToPay ?? 0), C.Green, false),
                (T("Ждёт доставки"), Money(balance.Pending), C.Ink, false),
                (F("Можно ещё · {0}", Points(remaining)), Money(balance.CanEarn), C.Ink, true));
            var header = top + 66 + 84 + 16;
            var columns = new[] { Pad + 24, Pad + 380, Pad + 590, Pad + 790, Width - Pad - 24 };
            Text(c, T("Неделя"), columns[0], header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Оформлено"), columns[1], header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Подтверждено"), columns[2], header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Выплачено"), columns[3], header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("К выплате"), columns[4], header, 13, Faces.Bold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            var y = header + 24;
            if (weeks.Count == 0)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                Text(c, T("Заказов спящим пока нет"), columns[0], y + 11, 16, Faces.Medium, C.Ink3);
                return;
            }

            foreach (var w in weeks)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                var sunday = w.Monday.AddDays(6);
                var from = O.Yamelly is { } ym && ym.Start > w.Monday ? ym.Start : w.Monday;
                Text(c, $"{from.Day:00}–{DayMonth(sunday.AddDays(-1))}", columns[0], y + 11, 16, Faces.Medium, C.Ink);
                TextRight(c, Money(w.Issued), columns[1], y + 11, 16, Faces.Bold, C.Ink);
                TextRight(c, Money(w.Confirmed), columns[2], y + 11, 16, Faces.Bold, C.Ink);
                TextRight(c, w.Paid > 0 ? Money(w.Paid) : "—", columns[3], y + 11, 16, Faces.Bold, C.Ink);
                TextRight(c, Money(w.ToPay), columns[4], y + 11, 16, Faces.Bold, w.ToPay > 0 ? C.Green : C.Ink);
                y += 40;
            }
        }

        private string PromoWeekFooter(IncentiveAgentCard card)
        {
            var promo = card.Promo!;
            var week = card.Week!;
            var lines = new List<string>();
            var cashback = promo.Cashback?.Weeks.Where(w => w.Monday <= week.Monday).Sum(w => w.ToPay) ?? 0;
            var best = promo.Earnings is { } e ? Math.Max(0, e.BestAccrued - e.BestPaid) : 0;
            if (cashback > 0 || best > 0)
            {
                lines.Add(F("К выплате пн–вт: кэшбек {0} + TW-01 {1} = {2} сум", Money(cashback), Money(best), Money(cashback + best)));
            }

            var next = week.Monday.AddDays(7);
            var goals = new List<string>();
            if (promo.Sleeping is { } sv && O.Yamelly is { } ym && ym.Covers(next))
            {
                goals.Add(F("{0} ({1} в день)", Sleepers(sv.Remaining), sv.VisitsPerDay));
            }

            // Цель недели — кг в день до приза × 6 рабочих дней (пн–сб).
            var p = card.Progress;
            decimal Week(IncentiveTier tier) => Math.Ceiling((p.Tiers.FirstOrDefault(t => t.Tier == tier)?.PerDay ?? 0) * 6);
            if (p.NormTier is { } target && Week(target) > 0)
            {
                goals.Add($"{P.Product} {Kg(Week(target))} {To(target)}");
            }

            if (p.Next is { } nearest && nearest != p.NormTier && Week(nearest) > 0)
            {
                goals.Add(F("или {0} {1}", Kg(Week(nearest)), To(nearest)));
            }

            if (goals.Count > 0)
            {
                lines.Add(F("Цели на {0}–{1}: {2}", next.Day.ToString("00", CultureInfo.InvariantCulture), DayMonth(next.AddDays(5)), string.Join(" · ", goals)));
            }

            return lines.Count > 0 ? string.Join("\n", lines) : F("Все призы акции взяты · накоплено {0}", Kg(card.Total));
        }

        // ---------- Месяц ----------

        public byte[] PromoMonth(IncentiveAgentCard card)
        {
            var month = card.Month!;
            var promo = card.Promo!;
            var p = card.Progress;
            var strip = IsTest ? 52f : 0f;
            var statsTop = strip + HeaderHeight + StaleHeight;
            var months = new List<DateOnly>();
            for (var m = new DateOnly(P.Start.Year, P.Start.Month, 1); m <= P.End; m = m.AddMonths(1))
            {
                months.Add(m);
            }

            var monthsTop = statsTop + StatsHeight + Gap;
            var monthsHeight = 62 + months.Count * 48 + 40;
            var roadTop = monthsTop + monthsHeight + Gap;
            var earnTop = roadTop + RoadHeight + Gap;
            var earnHeight = promo.Earnings is null ? 0 : 62 + 34 + 3 * 40 + 12;
            var footerTop = earnTop + (earnHeight > 0 ? earnHeight + Gap : 0);
            var footer = PromoMonthFooter(card);
            using var surface = Surface(footerTop + FooterHeight(footer) + Pad + 6, out var c);
            var name = Capital(IncentiveTexts.MonthName(month.Month, Language));
            var first = new DateOnly(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            var from = O.Yamelly is { } ymp && ymp.Start > first && ymp.Start <= last ? ymp.Start : first;
            var to = P.End < last ? P.End : last;
            Header(c, strip, month.Final ? T("Итог акции") : T("Итог месяца"), $"{AgentTitle(card.Agent)} · {IncentiveTexts.MonthName(month.Month, Language)}",
                IncentiveSlot.Month, 0, 1, name, $"{from.Day:00}–{DayMonth(to)}");
            StaleBanner(c, strip + HeaderHeight);

            var sleeping = promo.Sleeping is { } sv && O.Yamelly is not null
                ? (T("Спящих разбужено"), $"{sv.Woken} / {sv.Listed}", C.Pink,
                    (string?)(sv.Woken >= sv.WakeTarget && sv.WakeTarget > 0 ? F("цель месяца {0} — ✓ выполнено", sv.WakeTarget) : F("цель месяца {0}", sv.WakeTarget)))
                : (T("Накоплено"), Kg(card.Total), C.Green, (string?)F("с {0}", StartText));
            var twizos = (F("{0} за {1}", P.Product, IncentiveTexts.MonthName(month.Month, Language)), Kg(month.Kg), C.Orange, (string?)F("наборов с подарком {0}", promo.Sets));
            var earned = promo.Earnings is { } e
                ? (T("Доп. заработок"), Money(e.Accrued), C.Green, (string?)T("кэшбек + победы TW-01"))
                : (card.Wholesale ? T("Место среди оптовых") : T("Место в акции"), card.Rank?.ToString(CultureInfo.InvariantCulture) ?? "—", C.Pink, (string?)F("из {0} ТП", card.Ranked));
            Stats(c, statsTop, sleeping, twizos, earned);

            MonthsBox(c, monthsTop, monthsHeight, card, months);
            PromoRoad(c, roadTop, card, F("накоплено {0} · акция до {1}", Kg(card.Total), EndText));
            if (promo.Earnings is { } earnings)
            {
                Earnings(c, earnTop, earnHeight, earnings, promo, month);
            }

            PromoFooter(c, footerTop, footer);
            return Png(surface);
        }

        /// <summary>«Месяцы накопительной»: кг по месяцам акции на шкале первого приза; будущие — сколько нужно.</summary>
        private void MonthsBox(SKCanvas c, float top, float height, IncentiveAgentCard card, IReadOnlyList<DateOnly> months)
        {
            CardBox(c, Pad, top, Inner, height);
            Text(c, T("Месяцы накопительной"), Pad + 24, top + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, F("с {0} · {1}", DayMonth(P.Start), Kg(card.Total)), Width - Pad - 24, top + 24, 17, Faces.SemiBold, C.Ink2);
            var p = card.Progress;
            var scaleTier = p.Tiers.FirstOrDefault()?.Tier ?? P.Tiers.FirstOrDefault();
            var scale = scaleTier?.Kg ?? Math.Max(1, card.Total);
            var current = new DateOnly(cards.Date.Year, cards.Date.Month, 1);
            var y = top + 62;
            foreach (var m in months)
            {
                var kg = card.Standing.Month(m.Year, m.Month);
                Text(c, Capital(IncentiveTexts.MonthName(m.Month, Language)), Pad + 24, y + 8, 19, Faces.Bold, C.Ink);
                var bx = Pad + 150f;
                var bw = 520f;
                using (var track = Fill(C.Track))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(bx, y + 6, bx + bw, y + 32), 13), track);
                }

                if (kg > 0)
                {
                    using var fill = Fill(m < current ? C.Green : C.Amber);
                    c.DrawRoundRect(new SKRoundRect(new SKRect(bx, y + 6, bx + Math.Max(40, bw * (float)Math.Min(1, kg / scale)), y + 32), 13), fill);
                }

                var value = m <= current ? F("{0} факт", Kg1(kg)) + (m < current ? " ✓" : "")
                    : p.Next is { } next ? F("нужно {0} {1}", Kg(p.ToNext ?? 0), To(next)) : "—";
                TextRight(c, value, Width - Pad - 24, y + 8, 17, Faces.Bold, m <= current ? C.Ink : C.Ink2);
                y += 48;
            }

            if (scaleTier is not null)
            {
                Text(c, F("шкала — {0} ({1}) · {2} раб. дней до {3}", Kg(scale), IncentiveTexts.Short(scaleTier, Language), p.WorkDaysLeft, DayMonth(P.End)), Pad + 24, y + 4, 14,
                    Faces.Medium, C.Ink3);
            }
        }

        private void Earnings(SKCanvas c, float top, float height, IncentiveEarnings e, IncentivePromoView promo, IncentiveMonthView month)
        {
            CardBox(c, Pad, top, Inner, height);
            using (var coin = Fill(C.Amber))
            {
                c.DrawCircle(Pad + 36, top + 32, 13, coin);
            }

            TextCenter(c, "$", Pad + 36, top + 22, 17, Faces.Bold, SKColors.White);
            Text(c, F("Мой заработок за {0}", IncentiveTexts.MonthName(month.Month, Language)), Pad + 60, top + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, T("выплачено + к выплате"), Width - Pad - 24, top + 24, 16, Faces.SemiBold, C.Ink2);
            var header = top + 66;
            var columns = new[] { Pad + 24, Pad + 650, Pad + 830, Width - Pad - 24 };
            Text(c, T("Акция"), columns[0], header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Начислено"), columns[1], header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Выплачено"), columns[2], header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Остаток"), columns[3], header, 13, Faces.Bold, C.Ink3);
            var rows = new[]
            {
                (F("YM-02 кэшбек · {0}", Points(promo.Sleeping?.Woken ?? 0)), e.CashbackAccrued, e.CashbackPaid, false),
                (F("TW-01 лучший агент дня · {0}", Plural(e.BestWins, "{0} победа", "{0} победы", "{0} побед")), e.BestAccrued, e.BestPaid, false),
                (T("Итого"), e.Accrued, e.Paid, true),
            };
            using var line = Stroke(C.Border, 1.5f);
            var y = header + 24;
            foreach (var (label, accrued, paid, total) in rows)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                var face = total ? Faces.Bold : Faces.Medium;
                Text(c, label, columns[0], y + 11, 17, face, C.Ink);
                TextRight(c, Money(accrued), columns[1], y + 11, 17, Faces.Bold, C.Ink);
                TextRight(c, Money(paid), columns[2], y + 11, 17, Faces.Bold, C.Ink);
                var left = Math.Max(0, accrued - paid);
                TextRight(c, Money(left), columns[3], y + 11, 17, Faces.Bold, total && left > 0 ? C.Green : C.Ink);
                y += 40;
            }
        }

        private string PromoMonthFooter(IncentiveAgentCard card)
        {
            var month = card.Month!;
            var p = card.Progress;
            if (month.Final || p.WorkDaysLeft == 0)
            {
                return MonthFooter(card);
            }

            var lines = new List<string>();
            var next = new DateOnly(month.Year, month.Month, 1).AddMonths(1);
            var nextName = Capital(IncentiveTexts.MonthName(next.Month, Language));
            if (p.Tiers.FirstOrDefault(t => !t.Reached) is { } open)
            {
                var perDay = open.PerDay ?? (p.WorkDaysLeft > 0 ? open.Left / p.WorkDaysLeft : 0);
                lines.Add(F("{0}: {1} {2} — {3} в день", nextName, To(open.Tier), Kg(open.Left), Kg1(perDay)));
            }
            else
            {
                lines.Add(F("Все призы акции взяты — накоплено {0}", Kg(card.Total)));
            }

            if (card.Promo?.Sleeping is { Remaining: > 0 } sv && O.Yamelly is { } ym && ym.End > cards.Date)
            {
                lines.Add(F("{0} ещё в списке — кэшбек до {1} сум", Sleepers(sv.Remaining), Money(sv.Remaining * ym.MinCashback)));
            }

            return string.Join("\n", lines);
        }
    }
}

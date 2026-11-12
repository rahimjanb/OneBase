using System.Globalization;
using OneBase.Application.Telegram.Battle;
using OneBase.Application.Telegram.Incentive;
using OneBase.Application.Telegram.Reports;
using SkiaSharp;
using C = OneBase.Infrastructure.Telegram.Reports.FarmColors;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Карточки руководителей по акциям ноября (образцы 6 и 7, ТЗ раздел 6): СВР 19:30 — «Итог дня команды» (ТП по результату: спящие, Twizos к
/// цели, наборы, кэшбек; накопительная TW-03 команды; кто отстаёт), РМ суббота 19:00 — «Итог недели зоны» (регионы: % спящих, Twizos, подарки
/// выдано и остаток на складах, победы TW-01; к выплате ТП; лидер зоны на дороге к призам), понедельник 09:00 — «Сводка акций» (то же по всей
/// области получателя за прошлую неделю, YM-03 по территориям и расход бюджета акций).
/// </summary>
internal static partial class IncentiveCardRenderer
{
    private sealed partial class Painter
    {
        private static readonly SKColor Muted = SKColor.Parse("#4C6B52");

        // ---------- Итог дня команды (СВР) ----------

        public byte[] TeamDay(IncentiveTeamDay team)
        {
            var strip = IsTest ? 52f : 0f;
            var statsTop = strip + HeaderHeight + StaleHeight;
            var rows = team.Rows;
            var tableTop = statsTop + StatsHeight + Gap;
            var tableHeight = 60 + 34 + Math.Max(1, rows.Count) * 50 + 12;
            var totalTop = tableTop + tableHeight + Gap;
            var totalHeight = 60 + 34 + Math.Max(1, rows.Count) * 40 + 12;
            var footer = TeamDayFooter(team);
            var footerTop = totalTop + totalHeight + Gap;
            using var surface = Surface(footerTop + FooterHeight(footer) + Pad + 6, out var c);
            Header(c, strip, T("Итог дня команды"), $"{team.Title} · {F("{0} ТП", rows.Count)} · {Weekday(cards.Date)} {DayMonth(cards.Date)}", IncentiveSlot.Team, 0, 1,
                DayMonth(cards.Date), T("итог дня"));
            StaleBanner(c, strip + HeaderHeight);

            var first = O.Yamelly is not null
                ? (T("Спящие разбужено"), $"{team.SleepingWoken} / {team.SleepingPlanned}", C.Pink, (string?)F("кэшбек команды {0}", Money(team.Cashback)))
                : (T("Накоплено командой"), Kg(rows.Sum(r => r.Total)), C.Green, (string?)F("{0} ТП", rows.Count));
            var gifts = team.Gifts.Count > 0 ? " · " + F("подарков: {0}", GiftsShort(team.Gifts)) : "";
            var second = (F("{0} команды", P.Product), Kg(team.Kg), C.Orange, (string?)(F("наборов {0}", team.Sets) + gifts));
            var leader = rows.Where(r => r.BestPlace is not null).OrderBy(r => r.BestPlace).FirstOrDefault();
            var third = O.Best is null || leader is null
                ? (T("TW-01 сегодня"), "—", C.Ink3, (string?)(O.Best is { } b0 ? F("порог {0} кг · победителя в команде нет", IncentiveTexts.Kg(b0.RetailMinKg)) : ""))
                : (T("TW-01 сегодня"), ShortName(leader.Agent.Name), leader.BestWinner ? C.Green : C.Ink, (string?)F("{0} · {1}-е место {2}", Kg(leader.Kg), leader.BestPlace!.Value,
                    leader.Wholesale ? T("Рынок и опт") : T("Розница")));
            Stats(c, statsTop, first, second, third);

            // Команда сегодня.
            CardBox(c, Pad, tableTop, Inner, tableHeight);
            Text(c, T("Команда сегодня"), Pad + 24, tableTop + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, T("сортировка по результату"), Width - Pad - 24, tableTop + 24, 16, Faces.SemiBold, C.Ink2);
            var header = tableTop + 64;
            const float sleepX = Pad + 520, barX = Pad + 540, barW = 220, setsX = Pad + 860;
            Text(c, T("ТП"), Pad + 24, header, 13, Faces.Bold, C.Ink3);
            if (O.Yamelly is not null)
            {
                TextRight(c, T("Спящие"), sleepX, header, 13, Faces.Bold, C.Ink3);
            }

            Text(c, P.Product, barX, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Наборы"), setsX, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Кэшбек"), Width - Pad - 24, header, 13, Faces.Bold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            var y = header + 24;
            if (rows.Count == 0)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                Text(c, T("В команде нет ТП акции"), Pad + 24, y + 14, 17, Faces.Medium, C.Ink3);
            }

            foreach (var row in rows)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                StatusDot(c, Pad + 36, y + 25, row.Status);
                TextFit(c, row.Agent.Name, Pad + 58, y + 14, sleepX - Pad - 130, 17, Faces.Medium, C.Ink);
                if (O.Yamelly is not null)
                {
                    TextRight(c, $"{row.SleepingWoken}/{row.SleepingPlanned}", sleepX, y + 14, 17, Faces.Bold, C.Pink);
                }

                using (var track = Fill(C.Track))
                {
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, y + 12, barX + barW, y + 22), 5), track);
                }

                var share = row.Goal is > 0 ? (float)Math.Min(1, row.Kg / row.Goal.Value) : row.Kg > 0 ? 1f : 0f;
                if (share > 0)
                {
                    using var fill = Fill(C.Orange);
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, y + 12, barX + Math.Max(10, barW * share), y + 22), 5), fill);
                }

                Text(c, row.Goal is { } goal ? F("{0} из ~{1}", Kg1(row.Kg), IncentiveTexts.Kg(goal)) : Kg1(row.Kg), barX, y + 28, 12.5f, Faces.Medium, C.Ink3);
                TextRight(c, row.Sets.ToString(CultureInfo.InvariantCulture), setsX, y + 14, 17, Faces.Bold, C.Ink);
                TextRight(c, Money(row.Cashback), Width - Pad - 24, y + 14, 17, Faces.Bold, row.Cashback > 0 ? C.Green : C.Ink3);
                y += 50;
            }

            // Накопительная TW-03 команды.
            CardBox(c, Pad, totalTop, Inner, totalHeight);
            Trophy(c, Pad + 40, totalTop + 34, 0.9f);
            Text(c, T("Накопительная TW-03 · команда"), Pad + 76, totalTop + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, F("с {0}", DayMonth(P.Start)), Width - Pad - 24, totalTop + 24, 16, Faces.SemiBold, C.Ink2);
            var tier = rows.Select(r => r.Progress.Next).OfType<IncentiveTier>().GroupBy(t => t.Image).OrderByDescending(g => g.Count()).Select(g => g.First()).FirstOrDefault();
            var th = totalTop + 64;
            Text(c, T("ТП"), Pad + 24, th, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Накоплено"), Pad + 560, th, 13, Faces.Bold, C.Ink3);
            TextRight(c, tier is null ? T("До приза") : Capital(To(tier)), Pad + 780, th, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Норма/день"), Width - Pad - 24, th, 13, Faces.Bold, C.Ink3);
            var ty = th + 24;
            foreach (var row in rows.OrderByDescending(r => r.Total).ThenBy(r => r.Agent.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                c.DrawLine(Pad + 20, ty, Width - Pad - 20, ty, line);
                TextFit(c, row.Agent.Name + (row.Wholesale ? $" · {T("опт")}" : ""), Pad + 24, ty + 11, 440, 17, Faces.Medium, C.Ink);
                TextRight(c, IncentiveTexts.Kg1(row.Total), Pad + 560, ty + 11, 17, Faces.Bold, C.Ink);
                var left = tier is null ? null : row.Progress.Tiers.FirstOrDefault(t => t.Tier.Image == tier.Image);
                TextRight(c, left is null ? "—" : left.Reached ? "✓" : IncentiveTexts.Kg(left.Left), Pad + 780, ty + 11, 17, Faces.Bold, left?.Reached == true ? C.Green : C.Ink);
                TextRight(c, left?.PerDay is { } perDay ? IncentiveTexts.Kg1(perDay) : "—", Width - Pad - 24, ty + 11, 17, Faces.Bold, C.Ink);
                ty += 40;
            }

            PromoFooter(c, footerTop, footer);
            return Png(surface);
        }

        private void StatusDot(SKCanvas c, float cx, float cy, string status)
        {
            var color = status switch { BattleStoreStatus.Taken => C.Green, BattleStoreStatus.Refused => C.Red, _ => C.Amber };
            using var dot = Fill(color);
            c.DrawCircle(cx, cy, 11, dot);
            if (status == BattleStoreStatus.Refused)
            {
                using var cross = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round };
                c.DrawLine(cx - 4, cy - 4, cx + 4, cy + 4, cross);
                c.DrawLine(cx - 4, cy + 4, cx + 4, cy - 4, cross);
            }
            else
            {
                TextCenter(c, status == BattleStoreStatus.Taken ? "✓" : "½", cx, cy - 8, 13, Faces.Bold, SKColors.White);
            }
        }

        /// <summary>Имя ТП коротко для итога: «andijon 4 Мухаммаджон» → «Мухаммаджон».</summary>
        private static string ShortName(string name)
        {
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var last = words.LastOrDefault(w => w.Any(char.IsLetter) && !w.All(char.IsAsciiLetterLower));
            return last ?? name;
        }

        private static string Initial(string name) => name.Length > 0 ? name[..1] : "";

        /// <summary>«к 7 · т 6 · ч 5»: первая буква подарка и штук.</summary>
        private string GiftsShort(IReadOnlyDictionary<string, int> gifts) =>
            string.Join(" · ", gifts.OrderByDescending(g => g.Value).Select(g => $"{Initial(GiftName(g.Key))} {g.Value}"));

        private string TeamDayFooter(IncentiveTeamDay team)
        {
            var lagging = team.Rows.Where(r => r.SleepingWoken == 0 && r.Kg < 5).Select(r => ShortName(r.Agent.Name)).Take(3).ToList();
            var lines = new List<string>();
            if (lagging.Count > 0)
            {
                lines.Add(F("{0} — 0 спящих и меньше 5 кг {1}", string.Join(", ", lagging), P.Product));
            }

            if (team.Refused > 0)
            {
                lines.Add(F("отказов по {0}: {1} — разобрать возражения завтра", P.Product, team.Refused));
            }

            return lines.Count > 0 ? string.Join("\n", lines)
                : F("Команда: {0} {1} · разбужено {2} · кэшбек {3}", Kg(team.Kg), P.Product, team.SleepingWoken, Money(team.Cashback));
        }

        // ---------- Итог недели зоны (РМ) и сводка руководства ----------

        public IEnumerable<byte[]> Zone(IncentiveZone zone)
        {
            var summary = cards.Slot == IncentiveSlot.Summary;
            var strip = IsTest ? 52f : 0f;
            var head = strip + HeaderHeight + StaleHeight;
            var regionsHeight = 60 + 34 + Math.Max(1, zone.Regions.Count) * 48 + 12;
            var territories = zone.Territories ?? [];
            var territoryHeight = territories.Count > 0 ? 60 + 34 + territories.Count * 40 + 12 : 0;
            var budgetHeight = zone.Budget is not null ? 60 + 34 + 3 * 40 + 12 : 0;
            var footer = ZoneFooter(zone);
            var footerHeight = FooterHeight(footer);
            var blocks = new List<(float Height, Action<SKCanvas, float> Draw)> { (StatsHeight, (c, y) => ZoneTiles(c, y, zone)) };
            if (zone.Budget is { } budget)
            {
                blocks.Add((budgetHeight, (c, y) => Budget(c, y, budgetHeight, budget)));
            }

            if (territories.Count > 0)
            {
                blocks.Add((territoryHeight, (c, y) => Territories(c, y, territoryHeight, territories)));
            }

            var regions = (regionsHeight, (Action<SKCanvas, float>)((c, y) => Regions(c, y, regionsHeight, zone)));
            if (!summary && zone.Leader is not null && zone.LeaderProgress is { } lp)
            {
                blocks.Add(regions);
                blocks.Add((RoadHeight, (c, y) => PromoRoad(c, y, lp, zone.LeaderTotal, false, F("лидер зоны: {0} — {1}", zone.Leader!.Name, Kg(zone.LeaderTotal)))));
            }
            else
            {
                blocks.Add(regions);
            }

            // Не выше 2 560 px: что не поместилось — следующей картинкой (у сводки по всей стране — таблица регионов).
            var pages = new List<List<(float Height, Action<SKCanvas, float> Draw)>> { new() };
            var used = head + Pad + footerHeight;
            foreach (var block in blocks)
            {
                if (pages[^1].Count > 0 && used + block.Height + Gap > MaxPageHeight)
                {
                    pages.Add([]);
                    used = head + Pad + footerHeight;
                }

                pages[^1].Add(block);
                used += block.Height + Gap;
            }

            var title = summary ? T("Сводка акций") : T("Итог недели зоны");
            var subtitle = $"{zone.Title} · {DayMonth(zone.From)}–{DayMonth(zone.To)}";
            var weekNo = (zone.From.Day - 1) / 7 + 1;
            for (var p = 0; p < pages.Count; p++)
            {
                var height = head + pages[p].Sum(b => b.Height + Gap) + (p == pages.Count - 1 ? footerHeight : ContinueFooter) + Pad + 6;
                using var surface = Surface(height, out var c);
                Header(c, strip, title, subtitle, summary ? IncentiveSlot.Summary : IncentiveSlot.Zone, p, pages.Count,
                    summary ? $"{Weekday(cards.Date)} {DayMonth(cards.Date)}" : F("Нед {0}", weekNo), summary ? T("сводка") : IncentiveTexts.MonthOf(zone.From.Month, Language));
                StaleBanner(c, strip + HeaderHeight);
                var y = head;
                foreach (var block in pages[p])
                {
                    block.Draw(c, y);
                    y += block.Height + Gap;
                }

                if (p == pages.Count - 1)
                {
                    PromoFooter(c, y, footer);
                }
                else
                {
                    Footer(c, y, F("Продолжение — на следующей картинке ({0} из {1})", p + 2, pages.Count));
                }

                yield return Png(surface);
            }
        }

        private string Mln(decimal v) => Math.Abs(v) >= 1_000_000
            ? F("{0} млн", (Math.Round(v / 100_000) / 10).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ','))
            : Money(v);

        private void ZoneTiles(SKCanvas c, float top, IncentiveZone zone)
        {
            var goal = O.Yamelly?.WakeShare;
            var first = O.Yamelly is not null
                ? (T("Спящие разбужено"), Money(zone.Woken), C.Pink,
                    (string?)(zone.WokenShare is { } share ? F("{0} списка · цель {1}", ReportFormat.Percent(share), goal is { } g ? ReportFormat.Percent(g) : "—") : ""))
                : (T("Накоплено"), Kg(zone.TotalKg), C.Green, (string?)F("с {0}", StartText));
            var second = (F("{0} за неделю", P.Product), Kg(zone.WeekKg), C.Orange,
                (string?)(cards.Slot == IncentiveSlot.Summary ? F("наборов {0} · с {1} всего {2}", zone.WeekSets, DayMonth(P.Start), Kg(zone.TotalKg)) : F("наборов с подарком {0}", zone.WeekSets)));
            (string, string, SKColor, string?) third;
            if (zone.Budget is { } b)
            {
                var spent = b.CashbackAccrued + b.BestAccrued;
                var plan = b.CashbackPlan + b.BestPlan;
                third = (T("Расход бюджета"), Mln(spent), plan > 0 && spent > plan ? C.Red : C.Green,
                    plan > 0 ? F("из {0} · {1}", Mln(plan), ReportFormat.Percent(spent / plan)) : T("кэшбек + TW-01 · бюджет не задан"));
            }
            else
            {
                third = (T("К выплате ТП"), Mln(zone.CashbackToPay + zone.BestToPay), C.Green, F("кэшбек {0} + TW-01 {1}", Mln(zone.CashbackToPay), Mln(zone.BestToPay)));
            }

            Stats(c, top, first, second, third);
        }

        private void Regions(SKCanvas c, float top, float height, IncentiveZone zone)
        {
            CardBox(c, Pad, top, Inner, height);
            Text(c, T("Регионы"), Pad + 24, top + 18, 24, Faces.Bold, C.Ink);
            TextRight(c, T("остаток подарков на складах филиалов"), Width - Pad - 24, top + 24, 16, Faces.SemiBold, C.Ink2);
            const float sleepX = Pad + 200, twX = Pad + 520, giftX = Pad + 640, stockX = Pad + 660;
            var header = top + 64;
            Text(c, T("Регион"), Pad + 24, header, 13, Faces.Bold, C.Ink3);
            if (O.Yamelly is not null)
            {
                Text(c, T("Спящие"), sleepX, header, 13, Faces.Bold, C.Ink3);
            }

            TextRight(c, P.Product, twX, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Подарков"), giftX, header, 13, Faces.Bold, C.Ink3);
            Text(c, T("Остаток"), stockX, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, "TW-01", Width - Pad - 24, header, 13, Faces.Bold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            var y = header + 24;
            if (zone.Regions.Count == 0)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                Text(c, T("В регионах нет ТП акции"), Pad + 24, y + 14, 17, Faces.Medium, C.Ink3);
            }

            var tiers = O.Twizos?.GiftTiers ?? [];
            foreach (var r in zone.Regions)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                TextFit(c, r.Name, Pad + 24, y + 14, sleepX - Pad - 40, 17, Faces.Bold, C.Ink);
                if (O.Yamelly is not null)
                {
                    const float bw = 150;
                    using (var track = Fill(C.Track))
                    {
                        c.DrawRoundRect(new SKRoundRect(new SKRect(sleepX, y + 10, sleepX + bw, y + 20), 5), track);
                    }

                    if (r.WokenShare is > 0 and var share)
                    {
                        using var fill = Fill(C.Pink);
                        c.DrawRoundRect(new SKRoundRect(new SKRect(sleepX, y + 10, sleepX + Math.Max(10, bw * (float)Math.Min(1, share)), y + 20), 5), fill);
                    }

                    Text(c, r.WokenShare is { } ws ? $"{ReportFormat.Percent(ws)} · {Points(r.Woken)}" : "—", sleepX, y + 26, 12.5f, Faces.Medium, C.Ink3);
                }

                TextRight(c, Kg(r.WeekKg), twX, y + 14, 17, Faces.Bold, r.WeekShare is < 1 ? C.Ink : C.Green);
                TextRight(c, r.Gifts.Values.Sum().ToString(CultureInfo.InvariantCulture), giftX, y + 14, 17, Faces.Bold, C.Ink);
                var stock = tiers.Count == 0 || r.Stock.Count == 0 ? "—"
                    : string.Join(" · ", tiers.Select(t => $"{Initial(GiftName(t.Gift))} {Math.Round(r.Stock.GetValueOrDefault(t.Gift)).ToString("0", CultureInfo.InvariantCulture)}"));
                TextFit(c, stock, stockX, y + 16, Width - Pad - 90 - stockX, 14, Faces.Medium, Low(r) is not null ? C.Red : C.Ink2);
                TextRight(c, r.BestWins.ToString(CultureInfo.InvariantCulture), Width - Pad - 24, y + 14, 17, Faces.Bold, C.Ink);
                y += 48;
            }
        }

        /// <summary>Подарок, которого на складах региона меньше, чем выдано за неделю (или уже нет при выдаче) — заканчивается.</summary>
        private string? Low(IncentiveZoneRegion r) =>
            r.Stock.Count == 0 ? null : r.Gifts.Where(g => g.Value > 0 && r.Stock.GetValueOrDefault(g.Key) < g.Value).Select(g => g.Key).FirstOrDefault();

        private void Territories(SKCanvas c, float top, float height, IReadOnlyList<IncentiveTerritoryState> territories)
        {
            CardBox(c, Pad, top, Inner, height);
            Text(c, T("YM-03 · план по территориям"), Pad + 24, top + 18, 24, Faces.Bold, C.Ink);
            var threshold = O.Territory?.Threshold ?? 0.85m;
            TextRight(c, F("порог {0}", ReportFormat.Percent(threshold)), Width - Pad - 24, top + 24, 16, Faces.SemiBold, C.Ink2);
            var header = top + 64;
            Text(c, T("Территория"), Pad + 24, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("План, кг"), Pad + 520, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Продано, кг"), Pad + 760, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("% плана"), Width - Pad - 24, header, 13, Faces.Bold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            var y = header + 24;
            foreach (var t in territories)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                Text(c, t.Name, Pad + 24, y + 11, 17, Faces.Medium, C.Ink);
                TextRight(c, IncentiveTexts.Kg(t.PlanKg), Pad + 520, y + 11, 17, Faces.Bold, C.Ink);
                TextRight(c, IncentiveTexts.Kg(t.Kg), Pad + 760, y + 11, 17, Faces.Bold, C.Ink);
                TextRight(c, ReportFormat.Percent(t.Share) + (t.Share >= threshold ? " ✓" : ""), Width - Pad - 24, y + 11, 17, Faces.Bold, t.Share >= threshold ? C.Green : C.Ink);
                y += 40;
            }
        }

        private void Budget(SKCanvas c, float top, float height, IncentiveBudgetView b)
        {
            CardBox(c, Pad, top, Inner, height);
            Text(c, T("Бюджет акций · с начала"), Pad + 24, top + 18, 24, Faces.Bold, C.Ink);
            var header = top + 64;
            Text(c, T("Статья"), Pad + 24, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Начислено"), Pad + 600, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Выплачено"), Pad + 790, header, 13, Faces.Bold, C.Ink3);
            TextRight(c, T("Бюджет"), Width - Pad - 24, header, 13, Faces.Bold, C.Ink3);
            using var line = Stroke(C.Border, 1.5f);
            var y = header + 24;
            var tiers = O.Twizos?.GiftTiers ?? [];
            var given = tiers.Count > 0 ? string.Join(" · ", tiers.Select(t => $"{Initial(GiftName(t.Gift))} {b.GiftsGiven.GetValueOrDefault(t.Gift)}")) : "—";
            var allotted = tiers.Count > 0 && b.GiftsAllotted.Count > 0 ? string.Join(" · ", tiers.Select(t => $"{Initial(GiftName(t.Gift))} {b.GiftsAllotted.GetValueOrDefault(t.Gift)}")) : "—";
            var rows = new (string Label, string Accrued, string Paid, string Plan)[]
            {
                (T("YM-02 кэшбек"), Money(b.CashbackAccrued), Money(b.CashbackPaid), b.CashbackPlan > 0 ? Money(b.CashbackPlan) : "—"),
                (T("TW-01 лучший агент дня"), Money(b.BestAccrued), Money(b.BestPaid), b.BestPlan > 0 ? Money(b.BestPlan) : "—"),
                (T("TW-02 подарки, шт"), given, "", allotted),
            };
            foreach (var row in rows)
            {
                c.DrawLine(Pad + 20, y, Width - Pad - 20, y, line);
                Text(c, row.Label, Pad + 24, y + 11, 17, Faces.Medium, C.Ink);
                TextRight(c, row.Accrued, Pad + 600, y + 11, 17, Faces.Bold, C.Ink);
                TextRight(c, row.Paid, Pad + 790, y + 11, 17, Faces.Bold, C.Ink);
                TextRight(c, row.Plan, Width - Pad - 24, y + 11, 17, Faces.Bold, C.Ink2);
                y += 40;
            }
        }

        private string ZoneFooter(IncentiveZone zone)
        {
            var lines = new List<string>();
            var goal = O.Yamelly?.WakeShare;
            var weak = zone.Regions.Where(r => (goal is { } g && r.WokenShare is { } s && s < g) && r.WeekShare is < 1).Select(r => r.Name).Take(3).ToList();
            if (weak.Count > 0)
            {
                lines.Add(F("{0}: спящие ниже цели и {1} ниже нормы — разбор с СВР", string.Join(", ", weak), P.Product));
            }

            var low = zone.Regions.Select(r => (r.Name, Gift: Low(r))).Where(x => x.Gift is not null).Take(2).ToList();
            if (low.Count > 0)
            {
                lines.Add(string.Join(" · ", low.Select(x => F("{0}: заканчиваются подарки «{1}» — дозаказ", x.Name, GiftName(x.Gift!)))));
            }

            if (zone.Territories is { Count: > 0 } territories && O.Territory is { } t)
            {
                var below = territories.Where(x => x.Share < t.Threshold).Select(x => $"{x.Name} {ReportFormat.Percent(x.Share)}").ToList();
                if (below.Count > 0)
                {
                    lines.Add(F("YM-03 ниже порога: {0}", string.Join(", ", below)));
                }
            }

            return lines.Count > 0 ? string.Join("\n", lines.Take(3))
                : F("Разбужено {0} · {1} {2} за неделю · к выплате ТП {3}", Money(zone.Woken), Kg(zone.WeekKg), P.Product, Mln(zone.CashbackToPay + zone.BestToPay));
        }
    }
}
